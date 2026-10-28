using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Devices.Domain.Model.Queries;

/// <summary>Devices installed on a herd or an animal that the caller may see.</summary>
public record GetDevicesForCallerQuery(CallerContext Caller);
