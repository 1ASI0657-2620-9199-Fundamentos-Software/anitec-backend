using System.Net;
using System.Text.Json;
using Anitec.Platform.Notifications.Application.Internal.OutboundServices;
using Anitec.Platform.Notifications.Infrastructure.Email;
using Anitec.Platform.Tests.Features.StepDefinitions;
using Microsoft.Extensions.Options;

namespace Anitec.Platform.Tests.Notifications;

/// <summary>The adapter of Resend: it never calls the real API in the tests, a fake handler answers.</summary>
public class ResendEmailNotificationServiceTests
{
    private const string FakeKey = "re_test_not_a_real_key_0123456789";
    private static readonly AlertEmail Email = new("rancher@example.com", "AniTec: aviso", "Texto del aviso");

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls.Add((request, request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return await answer(request, cancellationToken);
        }
    }

    private static ResendEmailNotificationService NewService(FakeHandler handler, ResendOptions? options = null)
    {
        return new ResendEmailNotificationService(new HttpClient(handler),
            Options.Create(options ?? new ResendOptions { ApiKey = FakeKey }),
            new CapturingLogger<ResendEmailNotificationService>());
    }

    private static Task<HttpResponseMessage> Answer(HttpStatusCode status, string body = "{}")
    {
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    [Fact]
    public async Task WithoutAKey_NothingIsSent_AndNoCallIsMade()
    {
        var handler = new FakeHandler((_, _) => Answer(HttpStatusCode.OK));

        var result = await NewService(handler, new ResendOptions { ApiKey = "" }).SendAsync(Email, CancellationToken.None);

        Assert.Equal(EmailSendStatus.Disabled, result.Status);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task ASuccessfulCall_SendsTheMessageToTheApiWithTheKeyAsABearerToken()
    {
        var handler = new FakeHandler((_, _) => Answer(HttpStatusCode.OK, "{\"id\":\"abc\"}"));

        var result = await NewService(handler).SendAsync(Email, CancellationToken.None);

        Assert.Equal(EmailSendStatus.Sent, result.Status);
        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal("https://api.resend.com/emails", request.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal(FakeKey, request.Headers.Authorization.Parameter);

        using var json = JsonDocument.Parse(body);
        Assert.Equal("AniTec <onboarding@resend.dev>", json.RootElement.GetProperty("from").GetString());
        Assert.Equal("rancher@example.com", json.RootElement.GetProperty("to")[0].GetString());
        Assert.Equal("AniTec: aviso", json.RootElement.GetProperty("subject").GetString());
        Assert.Equal("Texto del aviso", json.RootElement.GetProperty("text").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ARefusal_IsAFailure_WithTheStatusCodeAndWithoutTheBodyOrTheKey(HttpStatusCode status)
    {
        var handler = new FakeHandler((_, _) => Answer(status, "{\"message\":\"rancher@example.com is not allowed\"}"));

        var result = await NewService(handler).SendAsync(Email, CancellationToken.None);

        Assert.Equal(EmailSendStatus.Failed, result.Status);
        Assert.Contains(((int)status).ToString(), result.Reason);
        Assert.DoesNotContain("rancher@example.com", result.Reason);
        Assert.DoesNotContain(FakeKey, result.Reason);
    }

    [Fact]
    public async Task WhenTheProviderCannotBeReached_ItIsAFailure_NotAnException()
    {
        var handler = new FakeHandler((_, _) => throw new HttpRequestException("no route to host"));

        var result = await NewService(handler).SendAsync(Email, CancellationToken.None);

        Assert.Equal(EmailSendStatus.Failed, result.Status);
        Assert.DoesNotContain(FakeKey, result.Reason);
    }

    [Fact]
    public async Task WhenTheProviderDoesNotAnswerInTime_ItIsAFailure()
    {
        var handler = new FakeHandler(async (request, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var result = await NewService(handler, new ResendOptions { ApiKey = FakeKey, TimeoutSeconds = 1 })
            .SendAsync(Email, CancellationToken.None);

        Assert.Equal(EmailSendStatus.Failed, result.Status);
        Assert.Contains("did not answer", result.Reason);
    }

    [Fact]
    public async Task WhenTheApplicationIsStopping_TheCancellationIsNotHidden()
    {
        using var stopping = new CancellationTokenSource();
        var handler = new FakeHandler(async (request, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        stopping.CancelAfter(100);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NewService(handler).SendAsync(Email, stopping.Token));
    }
}
