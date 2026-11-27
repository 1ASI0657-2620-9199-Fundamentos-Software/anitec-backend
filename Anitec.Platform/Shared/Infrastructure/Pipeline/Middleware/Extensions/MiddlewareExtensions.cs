using Anitec.Platform.Shared.Infrastructure.Pipeline.Middleware.Components;

namespace Anitec.Platform.Shared.Infrastructure.Pipeline.Middleware.Extensions;

/// <summary>
///     Middleware extensions
/// </summary>
public static class MiddlewareExtensions
{
    /// <summary>
    ///     Use the correlation id middleware. It goes first in the pipeline so that the exception handler, the
    ///     authorization and every log line of the request run inside its logging scope.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }

    /**
     * <summary>
     *     Use the global exception handler middleware
     * </summary>
     * <param name="builder">The application builder</param>
     * <returns>The application builder</returns>
     */
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
