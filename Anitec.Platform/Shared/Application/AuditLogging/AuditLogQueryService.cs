using Anitec.Platform.Shared.Domain.Model.Entities;
using Anitec.Platform.Shared.Domain.Repositories;

namespace Anitec.Platform.Shared.Application.AuditLogging;

/// <summary>Reads the entries of one rancher. There is no way to read the entries of someone else.</summary>
/// <param name="OwnerUserId">The authenticated rancher; never a value that the client sends.</param>
public record GetAuditLogForOwnerQuery(
    int OwnerUserId,
    string? Action = null,
    string? EntityType = null,
    DateTime? From = null,
    DateTime? To = null,
    int Limit = AuditLogQueryService.DefaultLimit);

public interface IAuditLogQueryService
{
    Task<IReadOnlyList<AuditLogEntry>> Handle(GetAuditLogForOwnerQuery query, CancellationToken cancellationToken);
}

public class AuditLogQueryService(IAuditLogRepository repository) : IAuditLogQueryService
{
    public const int DefaultLimit = 100;
    public const int MaxLimit = 500;

    public Task<IReadOnlyList<AuditLogEntry>> Handle(GetAuditLogForOwnerQuery query, CancellationToken cancellationToken)
    {
        var limit = query.Limit is < 1 or > MaxLimit ? DefaultLimit : query.Limit;
        return repository.ListByOwnerAsync(query.OwnerUserId, query.Action, query.EntityType, query.From, query.To,
            limit, cancellationToken);
    }
}
