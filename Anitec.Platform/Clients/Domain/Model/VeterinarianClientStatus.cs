namespace Anitec.Platform.Clients.Domain.Model;

/// <summary>
///     States of the relationship between a veterinarian and a rancher.
/// </summary>
public static class VeterinarianClientStatus
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";

    public static bool IsAccepted(string? status)
    {
        return string.Equals(status, Accepted, StringComparison.OrdinalIgnoreCase);
    }
}
