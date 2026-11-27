using Anitec.Platform.Shared.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Interceptors;

/// <summary>
///     Keeps the audit log append-only (US-074, "Integridad"): saving a change or a deletion of an
///     <see cref="AuditLogEntry" /> fails, whatever code asks for it.
/// </summary>
public sealed class AuditLogImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Check(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Check(DbContext? context)
    {
        if (context is null) return;

        if (context.ChangeTracker.Entries<AuditLogEntry>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("The audit log is append-only: its entries cannot be changed or removed.");
    }
}
