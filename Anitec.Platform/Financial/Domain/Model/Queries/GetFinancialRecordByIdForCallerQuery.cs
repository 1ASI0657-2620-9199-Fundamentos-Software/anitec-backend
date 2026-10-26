using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Financial.Domain.Model.Queries;

public record GetFinancialRecordByIdForCallerQuery(int Id, CallerContext Caller);
