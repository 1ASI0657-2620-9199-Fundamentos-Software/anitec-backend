using Anitec.Subscriptions.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Anitec.Subscriptions.Infrastructure.Persistence.EntityFrameworkCore.Configuration.Extensions;

public static class ModelBuilderExtensions
{
    public static void ApplySubscriptionsConfiguration(this ModelBuilder builder)
    {
        builder.Entity<SubscriptionPlan>().HasKey(p => p.Id);
        builder.Entity<SubscriptionPlan>().Property(p => p.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Entity<SubscriptionPlan>().Property(p => p.Name).IsRequired().HasMaxLength(80);
        builder.Entity<SubscriptionPlan>().Property(p => p.Price).HasPrecision(10, 2);
        builder.Entity<SubscriptionPlan>().Property(p => p.StripePriceId).HasMaxLength(120);

        // The plans travel with the migration, because the seeder of the core only runs in Development and a
        // production database would otherwise have no plan to buy. The checkout builds the Stripe price from
        // the amount of the plan, so StripePriceId stays empty.
        builder.Entity<SubscriptionPlan>().HasData(
            new SubscriptionPlan { Id = 1, Name = "Ganadero Basico", Price = 49, StripePriceId = string.Empty, MaxAnimals = 80, IsActive = true },
            new SubscriptionPlan { Id = 2, Name = "Ganadero Pro", Price = 99, StripePriceId = string.Empty, MaxAnimals = 250, IsActive = true },
            new SubscriptionPlan { Id = 3, Name = "Veterinario Profesional", Price = 129, StripePriceId = string.Empty, MaxAnimals = 1000, IsActive = true });

        builder.Entity<Subscription>().HasKey(s => s.Id);
        builder.Entity<Subscription>().Property(s => s.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Entity<Subscription>().Property(s => s.StripeCustomerId).HasMaxLength(120);
        builder.Entity<Subscription>().Property(s => s.StripeSubscriptionId).HasMaxLength(120);
        builder.Entity<Subscription>().Property(s => s.Status).IsRequired().HasMaxLength(40);
        builder.Entity<Subscription>().Property(s => s.StartedAt)
            .HasConversion(new ValueConverter<DateOnly, DateTime>(
                value => value.ToDateTime(TimeOnly.MinValue),
                value => DateOnly.FromDateTime(value)));
        builder.Entity<Subscription>().Property(s => s.EndsAt)
            .HasConversion(new ValueConverter<DateOnly?, DateTime?>(
                value => value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue) : null,
                value => value.HasValue ? DateOnly.FromDateTime(value.Value) : null));
        builder.Entity<Subscription>()
            .HasOne<SubscriptionPlan>()
            .WithMany()
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Payment>().HasKey(p => p.Id);
        builder.Entity<Payment>().Property(p => p.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Entity<Payment>().Property(p => p.Amount).HasPrecision(10, 2);
        builder.Entity<Payment>().Property(p => p.Currency).IsRequired().HasMaxLength(10);
        builder.Entity<Payment>().Property(p => p.Provider).IsRequired().HasMaxLength(40);
        builder.Entity<Payment>().Property(p => p.ProviderPaymentId).IsRequired().HasMaxLength(120);
        builder.Entity<Payment>().Property(p => p.Status).IsRequired().HasMaxLength(40);
        builder.Entity<Payment>()
            .HasOne<Subscription>()
            .WithMany()
            .HasForeignKey(p => p.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

