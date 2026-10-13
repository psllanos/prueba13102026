using Microsoft.EntityFrameworkCore;
using ProviderOptimizer.Domain;
using ProviderOptimizer.Infrastructure.Persistence;
using ProviderOptimizer.Infrastructure.Persistence.Records;
using Xunit;

namespace ProviderOptimizer.IntegrationTests;

/// <summary>
/// Proves that a case and its decision record really land in the store, together, and that the
/// referential rule between a case and its provider is enforced by the database itself.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AssistanceCaseRepositoryTests
{
    private readonly PostgresFixture _fixture;

    public AssistanceCaseRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SaveAsync_persists_the_case_with_its_decision()
    {
        var providerId = await SeedProviderAsync();

        var breakdown = new ScoreBreakdown(
        [
            new ScoreFactorContribution("eta", 5d, 0.5d, 0.5d),
            new ScoreFactorContribution("distance", 1.1d, 0.2d, 0.2d),
            new ScoreFactorContribution("availability", 1d, 0.1d, 0.1d),
            new ScoreFactorContribution("rating", 4.8d, 0.2d, 0.2d)
        ]);

        var assistanceCase = AssistanceCase.Create(
            Guid.NewGuid(),
            ServiceType.Locksmith,
            CustomerLocation.FromCoordinates(-34.6037d, -58.3816d),
            providerId,
            breakdown,
            new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));

        await using (var writing = _fixture.CreateContext())
        {
            await new AssistanceCaseRepository(writing).SaveAsync(assistanceCase);
        }

        await using var reading = _fixture.CreateContext();
        var stored = await reading.AssistanceCases
            .AsNoTracking()
            .SingleAsync(record => record.CaseId == assistanceCase.CaseId);

        Assert.Equal(providerId, stored.ProviderId);
        Assert.Equal("locksmith", stored.ServiceType);
        Assert.Equal(assistanceCase.CreatedAt, stored.CreatedAt);
        Assert.Equal(breakdown.Total, stored.Breakdown.Total, precision: 12);
        Assert.Equal(
            breakdown.Contributions.Select(contribution => contribution.Factor),
            stored.Breakdown.Contributions.Select(contribution => contribution.Factor));
    }

    [Fact]
    public async Task The_breakdown_survives_as_jsonb_in_the_case_row()
    {
        var providerId = await SeedProviderAsync();
        var caseId = Guid.NewGuid();

        var breakdown = new ScoreBreakdown([new ScoreFactorContribution("eta", 3d, 1d, 1d)]);

        await using (var writing = _fixture.CreateContext())
        {
            await new AssistanceCaseRepository(writing).SaveAsync(AssistanceCase.Create(
                caseId,
                ServiceType.Battery,
                CustomerLocation.FromCoordinates(0d, 0d),
                providerId,
                breakdown,
                DateTimeOffset.UnixEpoch));
        }

        await using var reading = _fixture.CreateContext();

        var rows = await reading.Database
            .SqlQuery<int>(
                $"SELECT jsonb_array_length(breakdown) AS \"Value\" FROM assistance_cases WHERE case_id = {caseId}")
            .ToListAsync();

        Assert.Equal(1, Assert.Single(rows));
    }

    [Fact]
    public async Task The_database_refuses_a_case_whose_provider_does_not_exist()
    {
        var orphan = AssistanceCase.Create(
            Guid.NewGuid(),
            ServiceType.Locksmith,
            CustomerLocation.FromCoordinates(0d, 0d),
            Guid.NewGuid(),
            new ScoreBreakdown([new ScoreFactorContribution("eta", 1d, 1d, 1d)]),
            DateTimeOffset.UnixEpoch);

        await using var context = _fixture.CreateContext();

        await Assert.ThrowsAsync<DbUpdateException>(
            () => new AssistanceCaseRepository(context).SaveAsync(orphan));

        await using var verification = _fixture.CreateContext();
        var exists = await verification.AssistanceCases
            .AsNoTracking()
            .AnyAsync(record => record.CaseId == orphan.CaseId);

        Assert.False(exists);
    }

    private async Task<Guid> SeedProviderAsync()
    {
        var providerId = Guid.NewGuid();

        await using var context = _fixture.CreateContext();
        context.Providers.Add(new ProviderRecord
        {
            Id = providerId,
            Name = $"seed-{providerId:N}",
            ServiceType = "locksmith",
            Latitude = -34.6037d,
            Longitude = -58.3816d,
            EtaMinutes = 5,
            Rating = 4.8d,
            IsAvailable = true
        });
        await context.SaveChangesAsync();

        return providerId;
    }
}
