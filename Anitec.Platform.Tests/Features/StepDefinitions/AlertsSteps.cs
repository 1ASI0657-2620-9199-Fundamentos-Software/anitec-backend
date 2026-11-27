using System.Globalization;
using Anitec.Platform.Activities.Application.QueryServices;
using Anitec.Platform.Activities.Domain.Model.Entities;
using Anitec.Platform.Activities.Domain.Model.Queries;
using Anitec.Platform.Livestock.Application.QueryServices;
using Anitec.Platform.Livestock.Domain.Model.Queries;
using Anitec.Platform.Notifications.Application;
using Anitec.Platform.Notifications.Application.Internal.CommandServices;
using Anitec.Platform.Notifications.Application.Internal.QueryServices;
using Anitec.Platform.Notifications.Domain.Model;
using Anitec.Platform.Notifications.Domain.Model.Commands;
using Anitec.Platform.Notifications.Domain.Model.Entities;
using Anitec.Platform.Notifications.Domain.Model.Queries;
using Anitec.Platform.Notifications.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Anitec.Platform.Sanitary.Application.QueryServices;
using Anitec.Platform.Sanitary.Domain.Model.Entities;
using Anitec.Platform.Sanitary.Domain.Model.Queries;
using Anitec.Platform.Shared.Application.Model;
using Anitec.Platform.Shared.Domain.Model;
using Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Anitec.Platform.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Reqnroll;

namespace Anitec.Platform.Tests.Features.StepDefinitions;

public class AlertsWorld
{
    private readonly DbContextOptions<AppDbContext> _dbOptions = TestDb.NewOptions();

    public ManualTimeProvider Clock { get; } = new();
    public List<HealthEvent> HealthEvents { get; } = [];
    public List<FarmActivity> Activities { get; } = [];
    public Dictionary<int, int> AnimalOwners { get; } = [];
    public NotificationsOptions Options { get; } = new();

    public DueDateScanResult? LastScan { get; set; }
    public List<Alert> CreatedByScans { get; } = [];
    public Result<Alert>? LastResult { get; set; }

    /// <summary>Each replica has its own database context over the same database.</summary>
    public AppDbContext NewDb()
    {
        return new AppDbContext(_dbOptions);
    }

    public AlertCommandService NewCommandService(AppDbContext db)
    {
        return new AlertCommandService(new AlertRepository(db), new UnitOfWork(db), Clock, Microsoft.Extensions.Options.Options.Create(Options),
            NullLogger<AlertCommandService>.Instance);
    }

    public AlertQueryService NewQueryService(AppDbContext db)
    {
        return new AlertQueryService(new AlertRepository(db));
    }

    public DueDateScanService NewScanService(AppDbContext db)
    {
        var healthEvents = Substitute.For<IHealthEventQueryService>();
        healthEvents.Handle(Arg.Any<GetAllHealthEventsQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IEnumerable<HealthEvent>>(HealthEvents.ToList()));

        var activities = Substitute.For<IFarmActivityQueryService>();
        activities.Handle(Arg.Any<GetAllFarmActivitiesQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IEnumerable<FarmActivity>>(Activities.ToList()));

        var animals = Substitute.For<IAnimalQueryService>();
        animals.Handle(Arg.Any<GetAnimalOwnerIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<int?>(
                AnimalOwners.TryGetValue(call.Arg<GetAnimalOwnerIdQuery>().AnimalId, out var owner) ? owner : null));

        return new DueDateScanService(healthEvents, activities, animals, NewCommandService(db), Clock,
            Microsoft.Extensions.Options.Options.Create(Options));
    }
}

[Binding]
public class AlertsSteps(AlertsWorld world)
{
    private static CallerContext Rancher(int id)
    {
        return new CallerContext(id, CallerContext.RancherRole);
    }

    private async Task<Alert> AlertTitledAsync(string title)
    {
        await using var db = world.NewDb();
        return await db.Set<Alert>().AsNoTracking().FirstAsync(alert => alert.Title == title);
    }

    [Given(@"today is (\d+-\d+-\d+)")]
    public void GivenTodayIs(string date)
    {
        world.Clock.SetDate(DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    [Given(@"animal (\d+) belongs to rancher (\d+)")]
    public void GivenTheAnimalBelongsTo(int animalId, int rancherId)
    {
        world.AnimalOwners[animalId] = rancherId;
    }

    [Given(@"health event (\d+) of animal (\d+) with type ""(.*)"" is due on (\d+-\d+-\d+)")]
    public void GivenAHealthEventIsDue(int id, int animalId, string type, string dueDate)
    {
        world.HealthEvents.Add(new HealthEvent
        {
            Id = id,
            AnimalId = animalId,
            Type = type,
            NextDueDate = DateOnly.ParseExact(dueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
        });
    }

    [Given(@"activity (\d+) of rancher (\d+) titled ""(.*)"" with status ""(.*)"" is dated (\d+-\d+-\d+)")]
    public void GivenAnActivity(int id, int rancherId, string title, string status, string date)
    {
        world.Activities.Add(new FarmActivity
        {
            Id = id,
            OwnerId = rancherId,
            Title = title,
            Status = status,
            Date = DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture)
        });
    }

    [Given(@"activity (\d+) without owner titled ""(.*)"" with status ""(.*)"" is dated (\d+-\d+-\d+)")]
    public void GivenAnActivityWithoutOwner(int id, string title, string status, string date)
    {
        world.Activities.Add(new FarmActivity
        {
            Id = id,
            OwnerId = null,
            Title = title,
            Status = status,
            Date = DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture)
        });
    }

    [Given(@"the scanner runs")]
    [When(@"the scanner runs")]
    public async Task WhenTheScannerRuns()
    {
        await RunScannerAsync();
    }

    [When(@"the scanner runs on replica ""(.*)""")]
    public async Task WhenTheScannerRunsOnReplica(string replica)
    {
        // Each replica has its own database context and its own services; only the database is shared.
        await RunScannerAsync();
    }

    private async Task RunScannerAsync()
    {
        await using var db = world.NewDb();
        world.LastScan = await world.NewScanService(db).ScanAsync(CancellationToken.None);
        world.CreatedByScans.AddRange(world.LastScan.Created);
    }

    [When(@"the due date of health event (\d+) moves to (\d+-\d+-\d+)")]
    public void WhenTheDueDateMoves(int id, string dueDate)
    {
        world.HealthEvents.Single(item => item.Id == id).NextDueDate =
            DateOnly.ParseExact(dueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    [When(@"(\d+) days pass")]
    public void WhenDaysPass(int days)
    {
        world.Clock.Advance(TimeSpan.FromDays(days));
    }

    [Given(@"rancher (\d+) confirms the alert ""(.*)""")]
    [When(@"rancher (\d+) confirms the alert ""(.*)""")]
    public async Task WhenTheRancherConfirms(int rancherId, string title)
    {
        var alert = await AlertTitledAsync(title);
        await using var db = world.NewDb();
        world.LastResult = await world.NewCommandService(db)
            .Handle(new ConfirmAlertCommand(alert.Id, Rancher(rancherId)), CancellationToken.None);
    }

    [Given(@"rancher (\d+) snoozes the alert ""(.*)"" for (\d+) days with the reason ""(.*)""")]
    [When(@"rancher (\d+) snoozes the alert ""(.*)"" for (\d+) days with the reason ""(.*)""")]
    public async Task WhenTheRancherSnoozes(int rancherId, string title, int days, string reason)
    {
        var alert = await AlertTitledAsync(title);
        await using var db = world.NewDb();
        world.LastResult = await world.NewCommandService(db)
            .Handle(new SnoozeAlertCommand(alert.Id, Rancher(rancherId), reason, days), CancellationToken.None);
    }

    [When(@"rancher (\d+) closes the alert ""(.*)"" with the reason ""(.*)""")]
    public async Task WhenTheRancherCloses(int rancherId, string title, string reason)
    {
        var alert = await AlertTitledAsync(title);
        await using var db = world.NewDb();
        world.LastResult = await world.NewCommandService(db)
            .Handle(new CloseAlertCommand(alert.Id, Rancher(rancherId), reason), CancellationToken.None);
    }

    [Then(@"(\d+) alerts? (?:is|are) created")]
    public void ThenAlertsAreCreated(int count)
    {
        Assert.Equal(count, world.CreatedByScans.Count);
    }

    [Then(@"rancher (\d+) has (\d+) alerts?")]
    public async Task ThenTheRancherHasAlerts(int rancherId, int count)
    {
        await using var db = world.NewDb();
        var alerts = await world.NewQueryService(db)
            .Handle(new GetAlertsForCallerQuery(Rancher(rancherId)), CancellationToken.None);
        Assert.Equal(count, alerts.Count);
    }

    [Then(@"rancher (\d+) has (\d+) alerts? with status ""(.*)"" titled ""(.*)""")]
    public async Task ThenTheRancherHasAnAlert(int rancherId, int count, string status, string title)
    {
        await using var db = world.NewDb();
        var alerts = await world.NewQueryService(db)
            .Handle(new GetAlertsForCallerQuery(Rancher(rancherId)), CancellationToken.None);
        Assert.Equal(count, alerts.Count(alert => alert.Status == status && alert.Title == title));
        Assert.Equal(count, alerts.Count);
    }

    [Then(@"veterinarian (\d+) has (\d+) alerts?")]
    public async Task ThenTheVeterinarianHasAlerts(int id, int count)
    {
        await using var db = world.NewDb();
        var alerts = await world.NewQueryService(db)
            .Handle(new GetAlertsForCallerQuery(new CallerContext(id, CallerContext.VeterinarianRole)),
                CancellationToken.None);
        Assert.Equal(count, alerts.Count);
    }

    [Then(@"rancher (\d+) sees the alert ""(.*)"" as overdue")]
    public async Task ThenTheAlertIsOverdue(int rancherId, string title)
    {
        var alert = await AlertTitledAsync(title);
        Assert.True(alert.IsOverdue(world.Options.Today(world.Clock)));
    }

    [Then(@"the alert ""(.*)"" has status ""(.*)""")]
    public async Task ThenTheAlertHasStatus(string title, string status)
    {
        Assert.Equal(status, (await AlertTitledAsync(title)).Status);
    }

    [Then(@"the alert ""(.*)"" has status ""(.*)"" and is due on (\d+-\d+-\d+)")]
    public async Task ThenTheAlertHasStatusAndDate(string title, string status, string dueDate)
    {
        var alert = await AlertTitledAsync(title);
        Assert.Equal(status, alert.Status);
        Assert.Equal(DateOnly.ParseExact(dueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture), alert.DueDate);
        Assert.False(string.IsNullOrWhiteSpace(alert.ResolutionReason));
    }

    [Then(@"the alert ""(.*)"" was resolved by user (\d+)")]
    public async Task ThenTheAlertWasResolvedBy(string title, int userId)
    {
        var alert = await AlertTitledAsync(title);
        Assert.Equal(userId, alert.ResolvedByUserId);
        Assert.NotNull(alert.ResolvedAt);
    }

    [Then(@"the request is refused because a reason is required")]
    public void ThenReasonRequired()
    {
        AssertRefused(NotificationsError.ReasonRequired);
    }

    [Then(@"the request is refused because the days are not valid")]
    public void ThenDaysNotValid()
    {
        AssertRefused(NotificationsError.InvalidDays);
    }

    [Then(@"the request is refused because the transition is not allowed")]
    public void ThenTransitionNotAllowed()
    {
        AssertRefused(NotificationsError.InvalidTransition);
    }

    [Then(@"the request is refused because the alert was not found")]
    public void ThenNotFound()
    {
        AssertRefused(NotificationsError.AlertNotFound);
    }

    private void AssertRefused(NotificationsError error)
    {
        Assert.NotNull(world.LastResult);
        Assert.True(world.LastResult.IsFailure);
        Assert.Equal(error, world.LastResult.Error);
    }
}
