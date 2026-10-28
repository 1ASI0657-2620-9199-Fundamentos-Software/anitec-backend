using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Devices.Domain.Model.Queries;

public record GetDeviceByIdForCallerQuery(int Id, CallerContext Caller);
