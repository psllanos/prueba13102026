using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ProviderOptimizer.Domain;
using ProviderOptimizer.Infrastructure.Persistence.Records;

namespace ProviderOptimizer.Infrastructure.Persistence;

/// <summary>EF Core model for providers, assistance cases and their decision records.</summary>
public sealed class OptimizerDbContext : DbContext
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly ValueComparer<ScoreBreakdown> BreakdownComparer = new(
        (left, right) => AreEqual(left, right),
        breakdown => breakdown == null
            ? 0
            : breakdown.Contributions.Aggregate(17, (hash, contribution) => HashCode.Combine(hash, contribution)),
        breakdown => breakdown);

    public OptimizerDbContext(DbContextOptions<OptimizerDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProviderRecord> Providers => Set<ProviderRecord>();

    public DbSet<AssistanceCaseRecord> AssistanceCases => Set<AssistanceCaseRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<ProviderRecord>(entity =>
        {
            entity.ToTable("providers");
            entity.HasKey(provider => provider.Id);
            entity.Property(provider => provider.Id).HasColumnName("id");
            entity.Property(provider => provider.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(provider => provider.ServiceType).HasColumnName("service_type").HasMaxLength(32).IsRequired();
            entity.Property(provider => provider.Latitude).HasColumnName("latitude");
            entity.Property(provider => provider.Longitude).HasColumnName("longitude");
            entity.Property(provider => provider.EtaMinutes).HasColumnName("eta_minutes");
            entity.Property(provider => provider.Rating).HasColumnName("rating");
            entity.Property(provider => provider.IsAvailable).HasColumnName("is_available");
            entity.HasIndex(provider => provider.ServiceType).HasDatabaseName("ix_providers_service_type");
        });

        modelBuilder.Entity<AssistanceCaseRecord>(entity =>
        {
            entity.ToTable("assistance_cases");
            entity.HasKey(assistanceCase => assistanceCase.CaseId);
            entity.Property(assistanceCase => assistanceCase.CaseId).HasColumnName("case_id");
            entity.Property(assistanceCase => assistanceCase.ServiceType).HasColumnName("service_type").HasMaxLength(32).IsRequired();
            entity.Property(assistanceCase => assistanceCase.Latitude).HasColumnName("latitude");
            entity.Property(assistanceCase => assistanceCase.Longitude).HasColumnName("longitude");
            entity.Property(assistanceCase => assistanceCase.ProviderId).HasColumnName("provider_id");
            entity.Property(assistanceCase => assistanceCase.CreatedAt).HasColumnName("created_at");

            // The decision record shares the case row as jsonb: a case without its decision is not
            // representable, so the requirement holds structurally rather than transactionally.
            var breakdownProperty = entity
                .Property(assistanceCase => assistanceCase.Breakdown)
                .HasColumnName("breakdown")
                .HasColumnType("jsonb")
                .HasConversion(
                    value => JsonSerializer.Serialize(value.Contributions, JsonOptions),
                    json => new ScoreBreakdown(
                        JsonSerializer.Deserialize<List<ScoreFactorContribution>>(json, JsonOptions)
                        ?? new List<ScoreFactorContribution>()))
                .IsRequired();

            breakdownProperty.Metadata.SetValueComparer(BreakdownComparer);

            entity
                .HasOne<ProviderRecord>()
                .WithMany()
                .HasForeignKey(assistanceCase => assistanceCase.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static bool AreEqual(ScoreBreakdown? left, ScoreBreakdown? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Total.Equals(right.Total) && left.Contributions.SequenceEqual(right.Contributions);
    }
}
