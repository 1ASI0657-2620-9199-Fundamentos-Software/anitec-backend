using Anitec.Platform.Notifications.Application;
using Anitec.Platform.Notifications.Application.Internal.CommandServices;
using Anitec.Platform.Notifications.Domain.Model;
using Anitec.Platform.Notifications.Domain.Model.Commands;
using Anitec.Platform.Notifications.Domain.Model.Entities;
using Anitec.Platform.Notifications.Domain.Repositories;
using Anitec.Platform.Notifications.Infrastructure.Scheduling;
using Anitec.Platform.Shared.Domain.Repositories;
using Anitec.Platform.Tests.Features.StepDefinitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Anitec.Platform.Tests.Notifications;

/// <summary>The races between replicas and the behaviour of the background process (QAS-03).</summary>
public class AlertSchedulingTests
{
    private static readonly CreateAlertCommand Command = new(AlertSourceTypes.HealthEvent, 7, 1, "Vacuna", 5,
        new DateOnly(2026, 11, 20));

    private static AlertCommandService NewService(IAlertRepository repository, IUnitOfWork unitOfWork)
    {
        return new AlertCommandService(repository, unitOfWork, new ManualTimeProvider(),
            Options.Create(new NotificationsOptions()), NullLogger<AlertCommandService>.Instance);
    }

    [Fact]
    public async Task WhenAnotherReplicaStoredTheSameAlertFirst_TheOneThatLost_ForgetsItsCopyAndReportsItExists()
    {
        var repository = Substitute.For<IAlertRepository>();
        // The check passes (nobody had stored it yet) but the unique index rejects the insert; afterwards it exists.
        repository.ExistsForSourceAsync(default!, default, default, default).ReturnsForAnyArgs(false, true);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.CompleteAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new DbUpdateException("duplicate key"));

        var result = await NewService(repository, unitOfWork).Handle(Command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationsError.AlertAlreadyExists, result.Error);
        repository.Received(1).Remove(Arg.Any<Alert>());
    }

    [Fact]
    public async Task WhenTheInsertFailsForAnotherReason_TheErrorIsNotHidden()
    {
        var repository = Substitute.For<IAlertRepository>();
        repository.ExistsForSourceAsync(default!, default, default, default).ReturnsForAnyArgs(false);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.CompleteAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new DbUpdateException("database is down"));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            NewService(repository, unitOfWork).Handle(Command, CancellationToken.None));
    }

    [Fact]
    public async Task APassThatFails_IsLogged_AndDoesNotStopTheProcess()
    {
        var scan = Substitute.For<IDueDateScanService>();
        scan.ScanAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("database is down"));
        var logger = new CapturingLogger<DueDateScanningService>();
        var service = NewHostedService(scan, new NotificationsOptions(), logger);

        await service.RunOnceAsync(CancellationToken.None);

        var line = Assert.Single(logger.Lines, item => item.Level == LogLevel.Error);
        Assert.IsType<InvalidOperationException>(line.Exception);
    }

    [Fact]
    public async Task APassThatWorks_LogsHowManyAlertsItCreated()
    {
        var scan = Substitute.For<IDueDateScanService>();
        scan.ScanAsync(Arg.Any<CancellationToken>()).Returns(new DueDateScanResult([new Alert(), new Alert()], []));
        var logger = new CapturingLogger<DueDateScanningService>();

        await NewHostedService(scan, new NotificationsOptions(), logger).RunOnceAsync(CancellationToken.None);

        Assert.Contains(logger.Lines, item => item.Level == LogLevel.Information && item.Message.Contains("2 alerts created"));
    }

    [Fact]
    public async Task WhenItIsTurnedOff_TheScannerNeverRuns()
    {
        var scan = Substitute.For<IDueDateScanService>();
        var service = NewHostedService(scan, new NotificationsOptions { Enabled = false },
            new CapturingLogger<DueDateScanningService>());

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));

        await scan.DidNotReceive().ScanAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenItIsTurnedOn_TheScannerRunsInTheBackground_AndStopsWithTheApplication()
    {
        var scan = Substitute.For<IDueDateScanService>();
        scan.ScanAsync(Arg.Any<CancellationToken>()).Returns(new DueDateScanResult([], []));
        var service = NewHostedService(scan, new NotificationsOptions { StartDelaySeconds = 0, ScanIntervalMinutes = 1 },
            new CapturingLogger<DueDateScanningService>());

        await service.StartAsync(CancellationToken.None);
        for (var i = 0; i < 50 && scan.ReceivedCalls().Count() == 0; i++) await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        Assert.True(scan.ReceivedCalls().Any(), "the scanner did not run in the background");
        Assert.True(service.ExecuteTask!.IsCompleted);
    }

    private static DueDateScanningService NewHostedService(IDueDateScanService scan, NotificationsOptions options,
        ILogger<DueDateScanningService> logger)
    {
        var services = new ServiceCollection().AddSingleton(scan)
            .AddSingleton(Substitute.For<IAlertNotifier>()).BuildServiceProvider();
        return new DueDateScanningService(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(options),
            logger);
    }
}
