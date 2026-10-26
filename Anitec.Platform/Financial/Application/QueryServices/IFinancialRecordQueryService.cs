using Anitec.Platform.Financial.Domain.Model.Entities;
using Anitec.Platform.Financial.Domain.Model.Queries;

namespace Anitec.Platform.Financial.Application.QueryServices;

public interface IFinancialRecordQueryService
{
    Task<FinancialRecord?> Handle(GetFinancialRecordByIdQuery query, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns every record without authorization filtering. Reserved for internal use;
    ///     API endpoints must use <see cref="GetFinancialRecordsForCallerQuery" />.
    /// </summary>
    Task<IEnumerable<FinancialRecord>> Handle(GetAllFinancialRecordsQuery query, CancellationToken cancellationToken);

    Task<IEnumerable<FinancialRecord>> Handle(GetFinancialRecordsForCallerQuery query, CancellationToken cancellationToken);
    Task<FinancialRecord?> Handle(GetFinancialRecordByIdForCallerQuery query, CancellationToken cancellationToken);
}
