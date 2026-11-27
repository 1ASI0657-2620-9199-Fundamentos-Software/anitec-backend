using Anitec.Platform.Shared.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration.Extensions;

/// <summary>
///     Model builder extensions for the database context
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    ///     Configures the audit log (table <c>audit_logs</c>, after the plural rule of the naming convention).
    /// </summary>
    public static void ApplyAuditConfiguration(this ModelBuilder builder)
    {
        builder.Entity<AuditLogEntry>().HasKey(entry => entry.Id);
        builder.Entity<AuditLogEntry>().Property(entry => entry.Id).ValueGeneratedOnAdd();
        builder.Entity<AuditLogEntry>().Property(entry => entry.CorrelationId).HasMaxLength(64);
        builder.Entity<AuditLogEntry>().Property(entry => entry.ActorRole).IsRequired().HasMaxLength(20);
        builder.Entity<AuditLogEntry>().Property(entry => entry.Action).IsRequired().HasMaxLength(60);
        builder.Entity<AuditLogEntry>().Property(entry => entry.EntityType).IsRequired().HasMaxLength(40);
        builder.Entity<AuditLogEntry>().Property(entry => entry.Result).IsRequired().HasMaxLength(20);
        builder.Entity<AuditLogEntry>().Property(entry => entry.DetailsJson).HasMaxLength(2000);
        // The rancher reads its own rows, newest first.
        builder.Entity<AuditLogEntry>().HasIndex(entry => new { entry.OwnerUserId, entry.OccurredAt });
        // Reconstructs the operations of one request (QAS-10).
        builder.Entity<AuditLogEntry>().HasIndex(entry => entry.CorrelationId);
    }

    /// <summary>
    ///     Use snake case naming convention for the database context
    /// </summary>
    /// <param name="builder">
    ///     The model builder for the database context
    /// </param>
    public static void UseSnakeCaseNamingConvention(this ModelBuilder builder)
    {
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (!string.IsNullOrEmpty(tableName)) entity.SetTableName(tableName.ToPlural().ToSnakeCase());

            foreach (var property in entity.GetProperties())
                property.SetColumnName(property.GetColumnName().ToSnakeCase());

            foreach (var key in entity.GetKeys())
            {
                var keyName = key.GetName();
                if (!string.IsNullOrEmpty(keyName)) key.SetName(keyName.ToSnakeCase());
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                var foreignKeyName = foreignKey.GetConstraintName();
                if (!string.IsNullOrEmpty(foreignKeyName)) foreignKey.SetConstraintName(foreignKeyName.ToSnakeCase());
            }

            foreach (var index in entity.GetIndexes())
            {
                var indexDatabaseName = index.GetDatabaseName();
                if (!string.IsNullOrEmpty(indexDatabaseName)) index.SetDatabaseName(indexDatabaseName.ToSnakeCase());
            }
        }
    }
}
