using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Metrics.Domain.Model.Queries;

public record GetDeviceMetricByIdForCallerQuery(int Id, CallerContext Caller);
