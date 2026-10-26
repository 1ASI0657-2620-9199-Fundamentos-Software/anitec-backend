using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Sanitary.Domain.Model.Queries;

/// <summary>
///     Health events of the animals the caller may see.
/// </summary>
public record GetHealthEventsForCallerQuery(CallerContext Caller);
