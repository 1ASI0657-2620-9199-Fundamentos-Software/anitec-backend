namespace Anitec.Platform.Devices.Interfaces.Rest.Resources;

/// <summary>
///     A reading sent by a physical device. When <paramref name="RecordedAt" /> is omitted the server time is used.
/// </summary>
public record TelemetryReadingResource(string Type, decimal Value, string Unit, DateTime? RecordedAt);
