using Anitec.Platform.Notifications.Application.Internal.OutboundServices;
using Anitec.Platform.Profiles.Application.QueryServices;
using Anitec.Platform.Profiles.Domain.Model.Queries;

namespace Anitec.Platform.Notifications.Infrastructure.Contacts;

/// <summary>
///     The e-mail address of a rancher is the one of the profile that the rancher created for itself. It goes through
///     the public query service of Profiles, not through its tables.
/// </summary>
public class ProfileOwnerEmailLookup(IProfileQueryService profiles) : IOwnerEmailLookup
{
    public async Task<string?> FindEmailAsync(int userId, CancellationToken cancellationToken)
    {
        var profile = await profiles.Handle(new GetProfileByOwnerIdQuery(userId), cancellationToken);
        var address = profile?.EmailAddress;
        return string.IsNullOrWhiteSpace(address) ? null : address;
    }
}
