using Anitec.Platform.Profiles.Domain.Model.Aggregates;
using Anitec.Platform.Profiles.Domain.Model.Queries;

namespace Anitec.Platform.Profiles.Application.QueryServices;

/// <summary>
///     Profile query service
/// </summary>
public interface IProfileQueryService
{
    /// <summary>
    ///     Handle get all profiles. It does not filter by caller, so it is reserved for internal use; API
    ///     endpoints must use <see cref="GetProfilesForCallerQuery" />.
    /// </summary>
    /// <param name="query">
    ///     The <see cref="GetAllProfilesQuery" /> query
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    ///     A list of <see cref="Profile" /> objects
    /// </returns>
    Task<IEnumerable<Profile>> Handle(GetAllProfilesQuery query, CancellationToken cancellationToken);

    /// <summary>
    ///     Handle get profile by email
    /// </summary>
    /// <param name="query">
    ///     The <see cref="GetProfileByEmailQuery" /> query
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    ///     A <see cref="Profile" /> object or null
    /// </returns>
    Task<Profile?> Handle(GetProfileByEmailQuery query, CancellationToken cancellationToken);

    /// <summary>
    ///     Handle get profile by id
    /// </summary>
    /// <param name="query">
    ///     The <see cref="GetProfileByIdQuery" /> query
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    ///     A <see cref="Profile" /> object or null
    /// </returns>
    Task<Profile?> Handle(GetProfileByIdQuery query, CancellationToken cancellationToken);

    /// <summary>The profiles that the caller created.</summary>
    Task<IEnumerable<Profile>> Handle(GetProfilesForCallerQuery query, CancellationToken cancellationToken);

    /// <summary>A profile by id, or null when the caller did not create it.</summary>
    Task<Profile?> Handle(GetProfileByIdForCallerQuery query, CancellationToken cancellationToken);
}
