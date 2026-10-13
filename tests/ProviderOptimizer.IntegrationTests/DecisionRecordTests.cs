using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProviderOptimizer.Domain;
using ProviderOptimizer.Infrastructure.Persistence;
using Xunit;

namespace ProviderOptimizer.IntegrationTests;

/// <summary>
/// Task 4.6 in the task list asks for a test proving that no case survives a decision-record write
/// failure. The decision is a required jsonb column on the case row, so the two cannot be written
/// separately at all. This test proves that directly: the database itself refuses a case row that
/// carries no decision.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DecisionRecordTests
{
    private readonly PostgresFixture _fixture;

    public DecisionRecordTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task The_database_refuses_a_case_row_that_carries_no_decision()
    {
        var providerId = Guid.NewGuid();
        await _fixture.SeedProvidersAsync(
            PostgresFixture.Provider(providerId, "locksmith", latitude: 0d));

        await using var context = _fixture.CreateContext();

        var caseId = Guid.NewGuid();
        var serviceType = "locksmith";
        var latitude = 0d;
        var longitude = 0d;
        var createdAt = DateTimeOffset.UnixEpoch;

        // Deliberately omits the breakdown column: this is the only way a case could ever reach the
        // store without its decision record.
        var failure = await Assert.ThrowsAsync<PostgresException>(async () =>
            await context.Database.ExecuteSqlAsync(
                $"INSERT INTO assistance_cases (case_id, service_type, latitude, longitude, provider_id, created_at) VALUES ({caseId}, {serviceType}, {latitude}, {longitude}, {providerId}, {createdAt})"));

        Assert.Equal(PostgresErrorCodes.NotNullViolation, failure.SqlState);

        await using var verification = _fixture.CreateContext();
        var exists = await verification.AssistanceCases
            .AsNoTracking()
            .AnyAsync(record => record.CaseId == caseId);

        Assert.False(exists);
    }

    [Fact]
    public async Task A_case_and_its_decision_become_visible_together()
    {
        var providerId = Guid.NewGuid();
        await _fixture.SeedProvidersAsync(
            PostgresFixture.Provider(providerId, "locksmith", latitude: 0d));

        var breakdown = new ScoreBreakdown(
        [
            new ScoreFactorContribution("eta", 4d, 0.5d, 0.5d),
            new ScoreFactorContribution("rating", 4.2d, 0.5d, 0.5d)
        ]);

        var assistanceCase = AssistanceCase.Create(
            Guid.NewGuid(),
            ServiceType.Locksmith,
            CustomerLocation.FromCoordinates(0d, 0d),
            providerId,
            breakdown,
            DateTimeOffset.UnixEpoch);

        await using (var writing = _fixture.CreateContext())
        {
            await new AssistanceCaseRepository(writing).SaveAsync(assistanceCase);
        }

        await using var reading = _fixture.CreateContext();
        var stored = await reading.AssistanceCases
            .AsNoTracking()
            .SingleAsync(record => record.CaseId == assistanceCase.CaseId);

        // Whatever is read back from the store always carries a populated decision.
        Assert.NotEmpty(stored.Breakdown.Contributions);
        Assert.Equal(breakdown.Total, stored.Breakdown.Total, precision: 12);
    }
}
