namespace Anitec.Platform.Clients.Interfaces.Rest.Resources;

/// <summary>
///     Access request of a veterinarian as the rancher sees it.
/// </summary>
public record VeterinarianRequestResource(
    int Id,
    int VeterinarianId,
    int RancherId,
    string VeterinarianName,
    string Status,
    DateTime RequestedAt,
    DateTime? AcceptedAt);
