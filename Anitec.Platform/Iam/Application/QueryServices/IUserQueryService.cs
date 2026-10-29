using Anitec.Platform.Iam.Domain.Model.Aggregates;
using Anitec.Platform.Iam.Domain.Model.Queries;

namespace Anitec.Platform.Iam.Application.QueryServices;

/**
 * <summary>
 *     The user query service interface
 * </summary>
 * <remarks>
 *     This service contract specifies handling behavior used to query users
 * </remarks>
 */
public interface IUserQueryService
{
    /**
     * <summary>
     *     Handle get user by id query
     * </summary>
     * <param name="query">The get user by id query</param>
     * <param name="cancellationToken">The cancellation token</param>
     * <returns>The user if found, null otherwise</returns>
     */
    Task<User?> Handle(GetUserByIdQuery query, CancellationToken cancellationToken);

    /**
     * <summary>
     *     Handle get all users query. It does not filter by caller, so it is reserved for internal
     *     aggregations that apply their own rule; API endpoints must use <see cref="GetUsersForCallerQuery" />.
     * </summary>
     * <param name="query">The get all users query</param>
     * <param name="cancellationToken">The cancellation token</param>
     * <returns>The list of users</returns>
     */
    Task<IEnumerable<User>> Handle(GetAllUsersQuery query, CancellationToken cancellationToken);

    /**
     * <summary>
     *     Handle get user by username query
     * </summary>
     * <param name="query">The get user by username query</param>
     * <param name="cancellationToken">The cancellation token</param>
     * <returns>The user if found, null otherwise</returns>
     */
    Task<User?> Handle(GetUserByUsernameQuery query, CancellationToken cancellationToken);

    /**
     * <summary>
     *     Handle get users for the caller: the caller itself and the users it has an open relationship with
     * </summary>
     */
    Task<IEnumerable<User>> Handle(GetUsersForCallerQuery query, CancellationToken cancellationToken);

    /**
     * <summary>
     *     Handle get user by id for the caller. It returns null when the user is neither the caller nor
     *     related to it.
     * </summary>
     */
    Task<User?> Handle(GetUserByIdForCallerQuery query, CancellationToken cancellationToken);
}
