namespace Anitec.Platform.Clients.Domain.Model.Commands;

/// <summary>
///     A veterinarian asks a rancher for access. The relationship starts as <c>Pending</c>.
/// </summary>
public record CreateVeterinarianClientCommand(
    int VeterinarianId,
    int RancherId,
    string Status = VeterinarianClientStatus.Pending);
