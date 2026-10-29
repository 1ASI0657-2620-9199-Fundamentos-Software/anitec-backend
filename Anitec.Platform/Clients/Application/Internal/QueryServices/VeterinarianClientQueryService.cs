using Anitec.Platform.Clients.Application.QueryServices;
using Anitec.Platform.Clients.Domain.Model;
using Anitec.Platform.Clients.Domain.Model.Entities;
using Anitec.Platform.Clients.Domain.Model.Queries;
using Anitec.Platform.Clients.Domain.Repositories;

namespace Anitec.Platform.Clients.Application.Internal.QueryServices;

public class VeterinarianClientQueryService(IVeterinarianClientRepository repository) : IVeterinarianClientQueryService
{
    public async Task<IEnumerable<VeterinarianClient>> Handle(
        GetVeterinarianClientsByVeterinarianIdQuery query,
        CancellationToken cancellationToken)
    {
        return await repository.FindByVeterinarianIdAsync(query.VeterinarianId, cancellationToken);
    }

    public async Task<IEnumerable<VeterinarianClient>> Handle(
        GetVeterinarianRequestsByRancherIdQuery query,
        CancellationToken cancellationToken)
    {
        return await repository.FindByRancherIdAsync(query.RancherId, cancellationToken);
    }

    public async Task<IReadOnlySet<int>> Handle(
        GetAcceptedRancherIdsByVeterinarianIdQuery query,
        CancellationToken cancellationToken)
    {
        var relationships = await repository.FindByVeterinarianIdAsync(query.VeterinarianId, cancellationToken);
        return relationships
            .Where(relationship => VeterinarianClientStatus.IsAccepted(relationship.Status))
            .Select(relationship => relationship.RancherId)
            .ToHashSet();
    }

    public async Task<IReadOnlySet<int>> Handle(GetRelatedUserIdsQuery query, CancellationToken cancellationToken)
    {
        var caller = query.Caller;

        if (caller.IsVeterinarian)
        {
            var asVeterinarian = await repository.FindByVeterinarianIdAsync(caller.Id, cancellationToken);
            return asVeterinarian.Where(IsOpen).Select(relationship => relationship.RancherId).ToHashSet();
        }

        if (caller.IsRancher)
        {
            var asRancher = await repository.FindByRancherIdAsync(caller.Id, cancellationToken);
            return asRancher.Where(IsOpen).Select(relationship => relationship.VeterinarianId).ToHashSet();
        }

        return new HashSet<int>();
    }

    private static bool IsOpen(VeterinarianClient relationship)
    {
        return !string.Equals(relationship.Status, VeterinarianClientStatus.Rejected, StringComparison.OrdinalIgnoreCase);
    }
}
