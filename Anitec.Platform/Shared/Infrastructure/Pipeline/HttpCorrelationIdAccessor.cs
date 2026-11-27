using Anitec.Platform.Shared.Application.AuditLogging;
using Anitec.Platform.Shared.Infrastructure.Pipeline.Middleware.Components;

namespace Anitec.Platform.Shared.Infrastructure.Pipeline;

/// <summary>Reads the id that <see cref="CorrelationIdMiddleware" /> stored for the request that is running.</summary>
public class HttpCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor) : ICorrelationIdAccessor
{
    public string? CorrelationId =>
        httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.ItemKey] as string;
}
