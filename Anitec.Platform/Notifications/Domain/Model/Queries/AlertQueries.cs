using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Notifications.Domain.Model.Queries;

/// <summary>The alerts of the caller. Only ranchers have alerts; any other role gets an empty list.</summary>
public record GetAlertsForCallerQuery(CallerContext Caller, string? Status = null, int Limit = 200);

/// <summary>An alert by id, or null when it does not exist or is not the caller's.</summary>
public record GetAlertByIdForCallerQuery(int Id, CallerContext Caller);
