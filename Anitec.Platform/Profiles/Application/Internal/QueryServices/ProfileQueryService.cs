using Anitec.Platform.Profiles.Application.QueryServices;
using Anitec.Platform.Profiles.Domain.Model.Aggregates;
using Anitec.Platform.Profiles.Domain.Model.Queries;
using Anitec.Platform.Profiles.Domain.Repositories;

namespace Anitec.Platform.Profiles.Application.Internal.QueryServices;

/// <summary>
///     Profile query service
/// </summary>
/// <param name="profileRepository">
///     Profile repository
/// </param>
public class ProfileQueryService(IProfileRepository profileRepository) : IProfileQueryService
{
    /// <inheritdoc />
    public async Task<IEnumerable<Profile>> Handle(GetAllProfilesQuery query, CancellationToken cancellationToken)
    {
        return await profileRepository.ListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Profile?> Handle(GetProfileByEmailQuery query, CancellationToken cancellationToken)
    {
        return await profileRepository.FindProfileByEmailAsync(query.Email, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Profile?> Handle(GetProfileByIdQuery query, CancellationToken cancellationToken)
    {
        return await profileRepository.FindByIdAsync(query.ProfileId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Profile>> Handle(GetProfilesForCallerQuery query, CancellationToken cancellationToken)
    {
        var profiles = await profileRepository.ListAsync(cancellationToken);
        return profiles.Where(profile => profile.OwnerId == query.Caller.Id).ToList();
    }

    /// <inheritdoc />
    public async Task<Profile?> Handle(GetProfileByIdForCallerQuery query, CancellationToken cancellationToken)
    {
        var profile = await profileRepository.FindByIdAsync(query.ProfileId, cancellationToken);
        return profile is not null && profile.OwnerId == query.Caller.Id ? profile : null;
    }
}
