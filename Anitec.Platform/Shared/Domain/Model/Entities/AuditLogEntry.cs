namespace Anitec.Platform.Shared.Domain.Model.Entities;

/// <summary>
///     One row per sensitive operation (US-074, TS-028). The table is append-only: rows are added and read, never
///     changed or removed, and the database context refuses to save a change to one of them.
/// </summary>
public class AuditLogEntry
{
    public long Id { get; set; }

    /// <summary>Id of the request that caused the operation, the same one that appears in the logs (QAS-10).</summary>
    public string? CorrelationId { get; set; }

    public int ActorUserId { get; set; }

    public string ActorRole { get; set; } = string.Empty;

    /// <summary>The rancher whose data was touched. It decides who can read the row.</summary>
    public int OwnerUserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    public DateTime OccurredAt { get; set; }

    public string Result { get; set; } = AuditResults.Success;

    /// <summary>Small JSON with extra context (for example the ids of a bulk deletion). Never secrets.</summary>
    public string? DetailsJson { get; set; }
}

public static class AuditResults
{
    public const string Success = "Success";
}

/// <summary>The operations that are written to the audit log.</summary>
public static class AuditActions
{
    public const string RequestVeterinarianAccess = "RequestVeterinarianAccess";
    public const string ApproveVeterinarianAccess = "ApproveVeterinarianAccess";
    public const string RejectVeterinarianAccess = "RejectVeterinarianAccess";
    public const string RemoveVeterinarianClient = "RemoveVeterinarianClient";
    public const string DeleteHerd = "DeleteHerd";
    public const string DeleteAnimal = "DeleteAnimal";
    public const string DeleteAnimals = "DeleteAnimals";
    public const string CreateHealthEvent = "CreateHealthEvent";
    public const string UpdateHealthEvent = "UpdateHealthEvent";
    public const string DeleteHealthEvent = "DeleteHealthEvent";
    public const string DeleteFinancialRecord = "DeleteFinancialRecord";
    public const string DeleteDevice = "DeleteDevice";
    public const string IssueDeviceApiKey = "IssueDeviceApiKey";
}
