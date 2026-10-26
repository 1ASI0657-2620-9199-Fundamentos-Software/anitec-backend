namespace Anitec.Platform.Shared.Domain.Model;

/// <summary>
///     Identity of the authenticated user that performs a request, taken from the validated token
///     and never from a value supplied by the client.
/// </summary>
/// <param name="Id">The user id.</param>
/// <param name="Role">The user role ("Rancher" or "Veterinarian").</param>
public record CallerContext(int Id, string Role)
{
    public const string RancherRole = "Rancher";
    public const string VeterinarianRole = "Veterinarian";

    public bool IsRancher => Role.Equals(RancherRole, StringComparison.OrdinalIgnoreCase);

    public bool IsVeterinarian => Role.Equals(VeterinarianRole, StringComparison.OrdinalIgnoreCase);
}
