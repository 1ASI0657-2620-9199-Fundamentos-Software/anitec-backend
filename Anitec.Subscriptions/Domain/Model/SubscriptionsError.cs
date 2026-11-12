namespace Anitec.Subscriptions.Domain.Model;

public enum SubscriptionsError
{
    SubscriptionPlanNotFound,
    SubscriptionNotFound,

    /// <summary>The checkout session does not exist or belongs to another user.</summary>
    CheckoutNotFound,

    /// <summary>The checkout session exists but the payment was not collected.</summary>
    CheckoutNotPaid,

    /// <summary>The session has no user or plan in its metadata, so it was not opened by this service.</summary>
    CheckoutMetadataIncomplete
}
