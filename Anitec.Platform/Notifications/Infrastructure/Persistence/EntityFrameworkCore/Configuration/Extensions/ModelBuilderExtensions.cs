using Anitec.Platform.Notifications.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Anitec.Platform.Notifications.Infrastructure.Persistence.EntityFrameworkCore.Configuration.Extensions;

public static class ModelBuilderExtensions
{
    public static void ApplyNotificationsConfiguration(this ModelBuilder builder)
    {
        var dateOnly = new ValueConverter<DateOnly, DateTime>(
            value => value.ToDateTime(TimeOnly.MinValue),
            value => DateOnly.FromDateTime(value));

        builder.Entity<Alert>().HasKey(alert => alert.Id);
        builder.Entity<Alert>().Property(alert => alert.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Entity<Alert>().Property(alert => alert.SourceType).IsRequired().HasMaxLength(20);
        builder.Entity<Alert>().Property(alert => alert.Title).IsRequired().HasMaxLength(120);
        builder.Entity<Alert>().Property(alert => alert.Status).IsRequired().HasMaxLength(20);
        builder.Entity<Alert>().Property(alert => alert.ResolutionReason).HasMaxLength(300);
        builder.Entity<Alert>().Property(alert => alert.SourceDueDate).HasConversion(dateOnly);
        builder.Entity<Alert>().Property(alert => alert.DueDate).HasConversion(dateOnly);

        // One alert per source and due date, even when two replicas scan at the same moment.
        builder.Entity<Alert>()
            .HasIndex(alert => new { alert.SourceType, alert.SourceId, alert.SourceDueDate })
            .IsUnique();
        // The list of a rancher.
        builder.Entity<Alert>().HasIndex(alert => new { alert.OwnerId, alert.Status, alert.DueDate });
    }
}
