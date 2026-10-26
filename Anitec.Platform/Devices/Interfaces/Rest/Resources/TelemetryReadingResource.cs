namespace Anitec.Platform.Devices.Interfaces.Rest.Resources;

/// <summary>
///     A reading sent by a physical device. The server stamps the time of reception.
/// </summary>
public record TelemetryReadingResource(string Type, decimal Value, string Unit);
