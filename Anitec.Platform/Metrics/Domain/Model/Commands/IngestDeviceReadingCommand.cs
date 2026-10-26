namespace Anitec.Platform.Metrics.Domain.Model.Commands;

/// <summary>
///     A reading sent by a device. <paramref name="Now" /> is the server time stamped on the reading and
///     <paramref name="MinInterval" /> the shortest time allowed between two readings of the same device.
/// </summary>
public record IngestDeviceReadingCommand(
    int DeviceId,
    string Type,
    decimal Value,
    string Unit,
    DateTime Now,
    TimeSpan MinInterval);
