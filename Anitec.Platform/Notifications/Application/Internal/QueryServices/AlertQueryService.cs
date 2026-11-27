using Anitec.Platform.Notifications.Application.QueryServices;
using Anitec.Platform.Notifications.Domain.Model.Entities;
using Anitec.Platform.Notifications.Domain.Model.Queries;
using Anitec.Platform.Notifications.Domain.Repositories;

namespace Anitec.Platform.Notifications.Application.Internal.QueryServices;

public class AlertQueryService(IAlertRepository repository) : IAlertQueryService
{
    public const int MaxLimit = 500;

    public async Task<IReadOnlyList<Alert>> Handle(GetAlertsForCallerQuery query, CancellationToken cancellationToken)
    {
        if (!query.Caller.IsRancher) return [];

        var limit = query.Limit is < 1 or > MaxLimit ? 200 : query.Limit;
        return await repository.ListByOwnerAsync(query.Caller.Id, query.Status, limit, cancellationToken);
    }

    public async Task<Alert?> Handle(GetAlertByIdForCallerQuery query, CancellationToken cancellationToken)
    {
        if (!query.Caller.IsRancher) return null;

        var alert = await repository.FindByIdAsync(query.Id, cancellationToken);
        return alert is not null && alert.OwnerId == query.Caller.Id ? alert : null;
    }
}
