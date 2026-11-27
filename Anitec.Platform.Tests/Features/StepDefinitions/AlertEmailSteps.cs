using Anitec.Platform.Notifications.Application.Internal.CommandServices;
using Anitec.Platform.Notifications.Application.Internal.OutboundServices;
using Anitec.Platform.Notifications.Domain.Model.Entities;
using Anitec.Platform.Notifications.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Reqnroll;

namespace Anitec.Platform.Tests.Features.StepDefinitions;

public class AlertEmailWorld
{
    public Dictionary<int, string?> Addresses { get; } = [];
    public List<AlertEmail> Sent { get; } = [];
    public int ProviderCalls { get; set; }
    public EmailSendStatus ProviderAnswer { get; set; } = EmailSendStatus.Sent;
}

[Binding]
public class AlertEmailSteps(AlertsWorld alerts, AlertEmailWorld world)
{
    [Given(@"rancher (\d+) has the e-mail address ""(.*)""")]
    public void GivenTheAddress(int rancherId, string address)
    {
        world.Addresses[rancherId] = address;
    }

    [Given(@"rancher (\d+) has no e-mail address")]
    public void GivenNoAddress(int rancherId)
    {
        world.Addresses[rancherId] = null;
    }

    [Given(@"the e-mail provider fails")]
    public void GivenTheProviderFails()
    {
        world.ProviderAnswer = EmailSendStatus.Failed;
    }

    [Given(@"the e-mail provider works again")]
    public void GivenTheProviderWorks()
    {
        world.ProviderAnswer = EmailSendStatus.Sent;
    }

    [Given(@"the e-mail provider is not configured")]
    public void GivenTheProviderIsNotConfigured()
    {
        world.ProviderAnswer = EmailSendStatus.Disabled;
    }

    [Given(@"the alert e-mails are sent")]
    [When(@"the alert e-mails are sent")]
    public async Task WhenTheEmailsAreSent()
    {
        await using var db = alerts.NewDb();

        var provider = Substitute.For<IEmailNotificationService>();
        provider.SendAsync(Arg.Any<AlertEmail>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            world.ProviderCalls++;
            var email = call.Arg<AlertEmail>();
            switch (world.ProviderAnswer)
            {
                case EmailSendStatus.Sent:
                    world.Sent.Add(email);
                    return Task.FromResult(EmailSendResult.Sent);
                case EmailSendStatus.Disabled:
                    world.ProviderCalls--;
                    return Task.FromResult(EmailSendResult.Disabled);
                default:
                    return Task.FromResult(EmailSendResult.Failed("Resend answered 500"));
            }
        });

        var lookup = Substitute.For<IOwnerEmailLookup>();
        lookup.FindEmailAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(world.Addresses.GetValueOrDefault(call.Arg<int>())));

        var notifier = new AlertNotifier(new AlertRepository(db), new UnitOfWork(db), provider, lookup, alerts.Clock,
            Microsoft.Extensions.Options.Options.Create(alerts.Options), NullLogger<AlertNotifier>.Instance);
        await notifier.NotifyPendingAsync(CancellationToken.None);
    }

    [Then(@"(\d+) e-mails? (?:was|were) sent to ""(.*)""")]
    public void ThenEmailsWereSent(int count, string address)
    {
        Assert.Equal(count, world.Sent.Count(email => email.To == address));
    }

    [Then(@"the e-mail mentions ""(.*)""")]
    public void ThenTheEmailMentions(string text)
    {
        var email = world.Sent.First();
        Assert.Contains(text, email.Subject + email.TextBody);
    }

    [Then(@"the e-mail provider was called (\d+) times?")]
    public void ThenTheProviderWasCalled(int count)
    {
        Assert.Equal(count, world.ProviderCalls);
    }

    [Then(@"the alert ""(.*)"" is marked as e-mailed")]
    public async Task ThenTheAlertIsEmailed(string title)
    {
        Assert.NotNull((await Stored(title)).NotifiedAt);
    }

    [Then(@"the alert ""(.*)"" is not marked as e-mailed")]
    public async Task ThenTheAlertIsNotEmailed(string title)
    {
        Assert.Null((await Stored(title)).NotifiedAt);
    }

    [Then(@"the alert ""(.*)"" has (\d+) e-mail attempts?")]
    public async Task ThenTheAlertHasAttempts(string title, int attempts)
    {
        Assert.Equal(attempts, (await Stored(title)).NotificationAttempts);
    }

    private async Task<Alert> Stored(string title)
    {
        await using var db = alerts.NewDb();
        return await db.Set<Alert>().AsNoTracking().FirstAsync(alert => alert.Title == title);
    }
}
