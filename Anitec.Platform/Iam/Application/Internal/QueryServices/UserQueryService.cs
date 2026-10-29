using Anitec.Platform.Clients.Application.QueryServices;
using Anitec.Platform.Clients.Domain.Model.Queries;
using Anitec.Platform.Iam.Application.QueryServices;
using Anitec.Platform.Iam.Domain.Model.Aggregates;
using Anitec.Platform.Iam.Domain.Model.Queries;
using Anitec.Platform.Iam.Domain.Repositories;

namespace Anitec.Platform.Iam.Application.Internal.QueryServices;

/**
 * <summary>
 *     The user query service implementation class
 * </summary>
 * <remarks>
 *     This class is used to handle user queries
 * </remarks>
 */
public class UserQueryService(
    IUserRepository userRepository,
    IVeterinarianClientQueryService veterinarianClientQueryService) : IUserQueryService
{
    /**
     * <summary>
     *     Handle get user by id query
     * </summary>
     * <param name="query">The query object containing the user id to search</param>
     * <param name="cancellationToken">The cancellation token</param>
     * <returns>The user</returns>
     */
    public async Task<User?> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        return await userRepository.FindByIdAsync(query.Id, cancellationToken);
    }

    /**
     * <summary>
     *     Handle get all users query
     * </summary>
     * <param name="query">The query object for getting all users</param>
     * <param name="cancellationToken">The cancellation token</param>
     * <returns>The users</returns>
     */
    public async Task<IEnumerable<User>> Handle(GetAllUsersQuery query, CancellationToken cancellationToken)
    {
        return await userRepository.ListAsync(cancellationToken);
    }

    /**
     * <summary>
     *     Handle get user by username query
     * </summary>
     * <param name="query">The query object containing the username to search</param>
     * <param name="cancellationToken">The cancellation token</param>
     * <returns>The user</returns>
     */
    public async Task<User?> Handle(GetUserByUsernameQuery query, CancellationToken cancellationToken)
    {
        return await userRepository.FindByUsernameAsync(query.Username, cancellationToken);
    }

    public async Task<IEnumerable<User>> Handle(GetUsersForCallerQuery query, CancellationToken cancellationToken)
    {
        var visibleIds = (await veterinarianClientQueryService.Handle(
            new GetRelatedUserIdsQuery(query.Caller), cancellationToken)).ToHashSet();
        visibleIds.Add(query.Caller.Id);

        var users = await userRepository.ListAsync(cancellationToken);
        return users.Where(user => visibleIds.Contains(user.Id)).ToList();
    }

    public async Task<User?> Handle(GetUserByIdForCallerQuery query, CancellationToken cancellationToken)
    {
        var users = await Handle(new GetUsersForCallerQuery(query.Caller), cancellationToken);
        return users.FirstOrDefault(user => user.Id == query.Id);
    }
}
