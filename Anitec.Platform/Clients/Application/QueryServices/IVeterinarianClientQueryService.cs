using Anitec.Platform.Clients.Domain.Model.Entities;
using Anitec.Platform.Clients.Domain.Model.Queries;

namespace Anitec.Platform.Clients.Application.QueryServices;

public interface IVeterinarianClientQueryService
{
    Task<IEnumerable<VeterinarianClient>> Handle(GetVeterinarianClientsByVeterinarianIdQuery query, CancellationToken cancellationToken);

    /// <summary>
    ///     Gets every access request addressed to a rancher, whatever its status.
    /// </summary>
    Task<IEnumerable<VeterinarianClient>> Handle(GetVeterinarianRequestsByRancherIdQuery query, CancellationToken cancellationToken);

    /// <summary>
    ///     Gets the ids of the ranchers that explicitly approved the veterinarian (status Accepted).
    /// </summary>
    Task<IReadOnlySet<int>> Handle(GetAcceptedRancherIdsByVeterinarianIdQuery query, CancellationToken cancellationToken);
}
