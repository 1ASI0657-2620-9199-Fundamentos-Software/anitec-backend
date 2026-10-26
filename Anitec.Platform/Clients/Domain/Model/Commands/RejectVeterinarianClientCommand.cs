namespace Anitec.Platform.Clients.Domain.Model.Commands;

/// <summary>
///     The rancher rejects a pending request or revokes the access of a veterinarian.
/// </summary>
public record RejectVeterinarianClientCommand(int VeterinarianId, int RancherId);
