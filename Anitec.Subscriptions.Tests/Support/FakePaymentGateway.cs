using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Anitec.Subscriptions.Application.Internal.OutboundServices;

namespace Anitec.Subscriptions.Tests.Support;

/// <summary>
///     Payment gateway for tests. It keeps the checkout sessions in memory, lets a test mark a session as paid and
///     signs and verifies webhooks with a fake secret, so the whole flow runs without Stripe and without network.
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    private const string WebhookSecret = "fake-webhook-secret";

    private readonly object _lock = new();
    private readonly List<CheckoutSessionRequest> _requests = [];
    private readonly ConcurrentDictionary<string, CheckoutSessionDetails> _sessions = new();

    /// <summary>Makes every call fail as when the environment has no Stripe keys.</summary>
    public bool NotConfigured { get; set; }

    /// <summary>Makes the creation and the reading of sessions fail as when Stripe is down.</summary>
    public bool Failing { get; set; }

    public IReadOnlyList<CheckoutSessionRequest> Requests
    {
        get
        {
            lock (_lock) return _requests.ToList();
        }
    }

    public Task<CheckoutSession> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        lock (_lock) _requests.Add(request);

        var id = $"cs_test_{Guid.NewGuid():N}";
        _sessions[id] = new CheckoutSessionDetails(id, false, request.UserId, request.PlanId, null, null);
        return Task.FromResult(new CheckoutSession(id, $"https://checkout.test/{id}"));
    }

    public Task<CheckoutSessionDetails?> GetCheckoutSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        return Task.FromResult(_sessions.TryGetValue(sessionId, out var details) ? details : null);
    }

    public PaymentWebhookEvent ParseWebhook(string payload, string? signatureHeader)
    {
        if (NotConfigured) throw new PaymentGatewayNotConfiguredException("StripeSettings:WebhookSecret is not configured.");
        if (string.IsNullOrWhiteSpace(signatureHeader) || signatureHeader != Sign(payload))
            throw new InvalidWebhookSignatureException("The webhook signature could not be verified.");

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var sessionId = root.GetProperty("sessionId").GetString()!;
        _sessions.TryGetValue(sessionId, out var details);
        return new PaymentWebhookEvent(root.GetProperty("id").GetString()!, root.GetProperty("type").GetString()!, details);
    }

    /// <summary>The user pays a session that this fake opened (or one with the given metadata).</summary>
    public void MarkPaid(string sessionId)
    {
        _sessions.AddOrUpdate(sessionId,
            _ => throw new InvalidOperationException("Unknown session."),
            (_, current) => current with
            {
                IsPaid = true,
                CustomerId = $"cus_test_{Guid.NewGuid():N}",
                SubscriptionId = current.SubscriptionId ?? $"sub_test_{Guid.NewGuid():N}"
            });
    }

    /// <summary>A paid session that was not opened by this service: it has no user or plan in its metadata.</summary>
    public string AddForeignPaidSession()
    {
        var id = $"cs_test_{Guid.NewGuid():N}";
        _sessions[id] = new CheckoutSessionDetails(id, true, null, null, "cus_foreign", $"sub_foreign_{Guid.NewGuid():N}");
        return id;
    }

    /// <summary>Builds the body of a webhook about a session and its valid signature.</summary>
    public (string Payload, string Signature) BuildWebhook(string sessionId, string type = "checkout.session.completed")
    {
        var payload = JsonSerializer.Serialize(new { id = $"evt_test_{Guid.NewGuid():N}", type, sessionId });
        return (payload, Sign(payload));
    }

    public static string Sign(string payload)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload + WebhookSecret)));
    }

    private void ThrowIfUnavailable()
    {
        if (NotConfigured) throw new PaymentGatewayNotConfiguredException("StripeSettings:SecretKey is not configured.");
        if (Failing) throw new PaymentGatewayException("Stripe is not reachable.");
    }
}
