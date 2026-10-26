namespace Anitec.Platform.Devices.Interfaces.Rest.Resources;

/// <summary>
///     The API key of a device. It is returned only once, when it is issued.
/// </summary>
public record DeviceApiKeyResource(int DeviceId, string ApiKey, string Header);
