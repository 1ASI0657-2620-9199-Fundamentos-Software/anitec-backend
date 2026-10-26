namespace Anitec.Platform.Devices.Domain.Model.Queries;

public record AuthenticateDeviceQuery(int DeviceId, string ApiKey);
