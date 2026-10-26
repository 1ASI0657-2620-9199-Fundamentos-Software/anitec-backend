using Anitec.Platform.Clients.Domain.Model.Commands;
using Anitec.Platform.Shared.Domain.Model.Entities;

namespace Anitec.Platform.Clients.Domain.Model.Entities;

/// <summary>
///     Relationship between a veterinarian and a rancher. The veterinarian requests access and the
///     rancher decides: until the relationship is <c>Accepted</c> the veterinarian sees no data of the rancher.
/// </summary>
public class VeterinarianClient : IAuditableEntity
{
    public VeterinarianClient()
    {
        Status = VeterinarianClientStatus.Pending;
        RequestedAt = DateTime.UtcNow;
    }

    public VeterinarianClient(CreateVeterinarianClientCommand command)
    {
        VeterinarianId = command.VeterinarianId;
        RancherId = command.RancherId;
        Status = string.IsNullOrWhiteSpace(command.Status) ? VeterinarianClientStatus.Pending : command.Status;
        RequestedAt = DateTime.UtcNow;
        AcceptedAt = VeterinarianClientStatus.IsAccepted(Status) ? DateTime.UtcNow : null;
    }

    public int Id { get; set; }
    public int VeterinarianId { get; set; }
    public int RancherId { get; set; }
    public string Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsPending => string.Equals(Status, VeterinarianClientStatus.Pending, StringComparison.OrdinalIgnoreCase);
    public bool IsAccepted => VeterinarianClientStatus.IsAccepted(Status);
    public bool IsRejected => string.Equals(Status, VeterinarianClientStatus.Rejected, StringComparison.OrdinalIgnoreCase);

    /// <summary>The rancher accepts a pending request.</summary>
    public void Approve()
    {
        Status = VeterinarianClientStatus.Accepted;
        AcceptedAt = DateTime.UtcNow;
    }

    /// <summary>The rancher rejects a pending request or revokes an accepted one.</summary>
    public void Reject()
    {
        Status = VeterinarianClientStatus.Rejected;
        AcceptedAt = null;
    }

    /// <summary>The veterinarian asks again after a rejection.</summary>
    public void RequestAgain()
    {
        Status = VeterinarianClientStatus.Pending;
        RequestedAt = DateTime.UtcNow;
        AcceptedAt = null;
    }
}
