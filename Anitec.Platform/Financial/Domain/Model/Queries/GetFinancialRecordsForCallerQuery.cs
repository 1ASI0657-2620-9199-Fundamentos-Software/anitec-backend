using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Financial.Domain.Model.Queries;

/// <summary>
///     Financial records owned by the caller. Only ranchers own financial records.
/// </summary>
public record GetFinancialRecordsForCallerQuery(CallerContext Caller);
