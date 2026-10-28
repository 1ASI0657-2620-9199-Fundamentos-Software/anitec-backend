using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Metrics.Domain.Model.Queries;

/// <summary>Readings of the devices the caller may see.</summary>
public record GetDeviceMetricsForCallerQuery(CallerContext Caller);
