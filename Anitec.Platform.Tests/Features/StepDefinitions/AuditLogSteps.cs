using System.Reflection;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Attributes;
using Anitec.Platform.Shared.Application.AuditLogging;
using Anitec.Platform.Shared.Domain.Model;
using Anitec.Platform.Shared.Domain.Model.Entities;
using Anitec.Platform.Shared.Domain.Repositories;
using Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Anitec.Platform.Shared.Interfaces.Rest;
using Anitec.Platform.Tests.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Reqnroll;

namespace Anitec.Platform.Tests.Features.StepDefinitions;

/// <summary>A clock that the scenarios move by hand.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }

    public void Advance(TimeSpan span)
    {
        _now += span;
    }
}

public sealed class FixedCorrelationId : ICorrelationIdAccessor
{
    public string? CorrelationId { get; set; }
}

public sealed class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Lines { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Lines.Add((logLevel, formatter(state, exception), exception));
    }
}

public class AuditWorld
{
    public AppDbContext Db { get; } = new(TestDb.NewOptions());
    public ManualTimeProvider Clock { get; } = new();
    public FixedCorrelationId Correlation { get; } = new();
    public CapturingLogger<AuditLogger> Log { get; } = new();
    public bool StorageDown { get; set; }
    public Exception? ThrownByLogger { get; set; }
    public Exception? ThrownByChange { get; set; }

    public AuditLogger CreateLogger()
    {
        IAuditLogRepository repository = StorageDown
            ? ThrowingRepository()
            : new AuditLogRepository(Db);
        return new AuditLogger(repository, new UnitOfWork(Db), Correlation, Clock, Log);
    }

    public AuditLogQueryService CreateQueryService()
    {
        return new AuditLogQueryService(new AuditLogRepository(Db));
    }

    private static IAuditLogRepository ThrowingRepository()
    {
        var repository = Substitute.For<IAuditLogRepository>();
        repository.AddAsync(Arg.Any<AuditLogEntry>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("database is down"));
        return repository;
    }
}

[Binding]
public class AuditLogSteps(AuditWorld world)
{
    [Given(@"the request carries the correlation id ""(.*)""")]
    public void GivenTheCorrelationId(string correlationId)
    {
        world.Correlation.CorrelationId = correlationId;
    }

    [Given(@"the audit storage is not available")]
    public void GivenTheStorageIsNotAvailable()
    {
        world.StorageDown = true;
    }

    [Given(@"user (\d+) with role ""(.*)"" performed ""(.*)"" on ""(.*)"" (\d+) of rancher (\d+)")]
    [When(@"user (\d+) with role ""(.*)"" performs ""(.*)"" on ""(.*)"" (\d+) of rancher (\d+)")]
    public async Task WhenTheUserPerforms(int userId, string role, string action, string entityType, int entityId,
        int rancherId)
    {
        try
        {
            await world.CreateLogger().RecordAsync(
                new AuditRecord(new CallerContext(userId, role), rancherId, action, entityType, entityId),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            world.ThrownByLogger = exception;
        }
    }

    [When(@"one minute passes")]
    public void WhenOneMinutePasses()
    {
        world.Clock.Advance(TimeSpan.FromMinutes(1));
    }

    [When(@"someone tries to change the stored entry")]
    public async Task WhenSomeoneChangesTheEntry()
    {
        var entry = await world.Db.Set<AuditLogEntry>().FirstAsync();
        entry.Action = "SomethingElse";
        world.ThrownByChange = await Record.ExceptionAsync(() => world.Db.SaveChangesAsync());
    }

    [When(@"someone tries to remove the stored entry")]
    public async Task WhenSomeoneRemovesTheEntry()
    {
        var entry = await world.Db.Set<AuditLogEntry>().FirstAsync();
        world.Db.Remove(entry);
        world.ThrownByChange = await Record.ExceptionAsync(() => world.Db.SaveChangesAsync());
    }

    [Then(@"rancher (\d+) sees (\d+) audit entr(?:y|ies)")]
    public async Task ThenTheRancherSees(int rancherId, int count)
    {
        var entries = await world.CreateQueryService().Handle(new GetAuditLogForOwnerQuery(rancherId),
            CancellationToken.None);
        Assert.Equal(count, entries.Count);
    }

    [Then(@"rancher (\d+) sees (\d+) audit entr(?:y|ies) when filtering by action ""(.*)""")]
    public async Task ThenTheRancherSeesFiltered(int rancherId, int count, string action)
    {
        var entries = await world.CreateQueryService().Handle(new GetAuditLogForOwnerQuery(rancherId, action),
            CancellationToken.None);
        Assert.Equal(count, entries.Count);
        Assert.All(entries, entry => Assert.Equal(action, entry.Action));
    }

    [Then(@"the newest entry of rancher (\d+) has actor (\d+) with role ""(.*)"" and action ""(.*)""")]
    public async Task ThenTheNewestEntryHasActor(int rancherId, int actorId, string role, string action)
    {
        var newest = await Newest(rancherId);
        Assert.Equal(actorId, newest.ActorUserId);
        Assert.Equal(role, newest.ActorRole);
        Assert.Equal(action, newest.Action);
    }

    [Then(@"the newest entry of rancher (\d+) has the correlation id ""(.*)""")]
    public async Task ThenTheNewestEntryHasTheCorrelationId(int rancherId, string correlationId)
    {
        Assert.Equal(correlationId, (await Newest(rancherId)).CorrelationId);
    }

    [Then(@"the newest entry of rancher (\d+) has the result ""(.*)""")]
    public async Task ThenTheNewestEntryHasTheResult(int rancherId, string result)
    {
        Assert.Equal(result, (await Newest(rancherId)).Result);
    }

    [Then(@"the change is rejected because the log is append-only")]
    public void ThenTheChangeIsRejected()
    {
        var exception = Assert.IsType<InvalidOperationException>(world.ThrownByChange);
        Assert.Contains("append-only", exception.Message);
    }

    [Then(@"no error reaches the caller")]
    public void ThenNoErrorReachesTheCaller()
    {
        Assert.Null(world.ThrownByLogger);
    }

    [Then(@"the failure is written to the log")]
    public void ThenTheFailureIsWrittenToTheLog()
    {
        var line = Assert.Single(world.Log.Lines, item => item.Level == LogLevel.Error);
        Assert.Contains("DeleteAnimal", line.Message);
        Assert.IsType<InvalidOperationException>(line.Exception);
    }

    [Then(@"the audit log controller only offers reading")]
    public void ThenTheControllerOnlyOffersReading()
    {
        var verbs = typeof(AuditLogController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(attribute => attribute.HttpMethods)
            .Distinct()
            .ToList();

        Assert.Equal(["GET"], verbs);
    }

    [Then(@"only ranchers can read the audit log")]
    public void ThenOnlyRanchersCanRead()
    {
        var authorize = typeof(AuditLogController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        var roles = (string[])typeof(AuthorizeAttribute)
            .GetField("_roles", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(authorize)!;
        Assert.Equal(["Rancher"], roles);
    }

    private async Task<AuditLogEntry> Newest(int rancherId)
    {
        var entries = await world.CreateQueryService().Handle(new GetAuditLogForOwnerQuery(rancherId),
            CancellationToken.None);
        return entries.First();
    }
}
