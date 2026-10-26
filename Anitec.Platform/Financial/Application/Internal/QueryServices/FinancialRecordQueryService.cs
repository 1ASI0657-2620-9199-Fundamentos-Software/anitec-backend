using Anitec.Platform.Financial.Application.QueryServices;
using Anitec.Platform.Financial.Domain.Model.Entities;
using Anitec.Platform.Financial.Domain.Model.Queries;
using Anitec.Platform.Financial.Domain.Repositories;

namespace Anitec.Platform.Financial.Application.Internal.QueryServices;

public class FinancialRecordQueryService(IFinancialRecordRepository repository) : IFinancialRecordQueryService
{
    public async Task<FinancialRecord?> Handle(GetFinancialRecordByIdQuery query, CancellationToken cancellationToken)
    {
        return await repository.FindByIdAsync(query.Id, cancellationToken);
    }

    public async Task<IEnumerable<FinancialRecord>> Handle(GetAllFinancialRecordsQuery query, CancellationToken cancellationToken)
    {
        return await repository.ListAsync(cancellationToken);
    }

    public async Task<IEnumerable<FinancialRecord>> Handle(GetFinancialRecordsForCallerQuery query, CancellationToken cancellationToken)
    {
        if (!query.Caller.IsRancher) return [];

        var records = await repository.ListAsync(cancellationToken);
        return records.Where(record => record.OwnerId == query.Caller.Id).ToList();
    }

    public async Task<FinancialRecord?> Handle(GetFinancialRecordByIdForCallerQuery query, CancellationToken cancellationToken)
    {
        if (!query.Caller.IsRancher) return null;

        var record = await repository.FindByIdAsync(query.Id, cancellationToken);
        return record is not null && record.OwnerId == query.Caller.Id ? record : null;
    }
}
