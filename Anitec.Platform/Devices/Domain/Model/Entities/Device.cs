using Anitec.Platform.Devices.Domain.Model.Commands;

namespace Anitec.Platform.Devices.Domain.Model.Entities;

public class Device
{
    public Device()
    {
        Name = string.Empty;
        Type = string.Empty;
        SerialNumber = string.Empty;
        Status = string.Empty;
    }

    public Device(CreateDeviceCommand command)
    {
        Name = command.Name;
        Type = command.Type;
        SerialNumber = command.SerialNumber;
        Status = command.Status;
        HerdId = command.HerdId;
        AnimalId = command.AnimalId;

    }

    public int Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string SerialNumber { get; set; }
    public string Status { get; set; }
    public int? HerdId { get; set; }
    public int? AnimalId { get; set; }

    /// <summary>
    ///     BCrypt hash of the API key that the physical device uses to authenticate. The key itself is
    ///     shown once when it is issued and is never stored.
    /// </summary>
    public string? ApiKeyHash { get; set; }
}
