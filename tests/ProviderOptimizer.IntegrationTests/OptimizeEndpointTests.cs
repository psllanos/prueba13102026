using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ProviderOptimizer.Api.Contracts;
using Xunit;

namespace ProviderOptimizer.IntegrationTests;

/// <summary>
/// Drives <c>POST /optimize</c> over real HTTP against the real application and a real Postgres.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OptimizeEndpointTests
{
    private readonly PostgresFixture _fixture;

    public OptimizeEndpointTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Optimize_returns_one_scored_provider_and_records_the_case()
    {
        await _fixture.ResetAsync();

        var providerId = Guid.NewGuid();
        await _fixture.SeedProvidersAsync(
            PostgresFixture.Provider(providerId, "locksmith", latitude: 0.01d, etaMinutes: 5, rating: 4.8d));

        var client = _fixture.ApiFactory.CreateClient();

        var response = await client.PostAsJsonAsync("/optimize", new
        {
            serviceType = "locksmith",
            location = new { latitude = 0d, longitude = 0d },
            searchRadiusKm = 10d
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<OptimizePayload>();
        Assert.NotNull(payload);
        Assert.Equal(providerId, payload.SelectedProvider.Id);
        Assert.Equal("locksmith", payload.SelectedProvider.ServiceType);
        Assert.Equal(4, payload.Breakdown.Count);
        Assert.Equal(
            payload.Breakdown.Sum(factor => factor.Contribution),
            payload.TotalScore,
            precision: 12);

        await using var context = _fixture.CreateContext();
        var stored = await context.AssistanceCases
            .AsNoTracking()
            .SingleAsync(record => record.CaseId == payload.CaseId);

        Assert.Equal(providerId, stored.ProviderId);
        Assert.Equal(payload.TotalScore, stored.Breakdown.Total, precision: 12);

        // Postgres timestamptz keeps microseconds, so compare to sub-millisecond precision.
        Assert.True(
            (payload.DecidedAt - stored.CreatedAt).Duration() < TimeSpan.FromMilliseconds(1),
            $"decidedAt {payload.DecidedAt:O} did not survive the round trip (stored {stored.CreatedAt:O}).");
    }

    [Fact]
    public async Task Optimize_returns_409_and_records_nothing_when_no_provider_is_eligible()
    {
        await _fixture.ResetAsync();

        // Roughly 111 km away, requested with a 1 km radius: nothing is eligible.
        await _fixture.SeedProvidersAsync(
            PostgresFixture.Provider(Guid.NewGuid(), "locksmith", latitude: 1.0d));

        await using var before = _fixture.CreateContext();
        var casesBefore = await before.AssistanceCases.CountAsync();

        var client = _fixture.ApiFactory.CreateClient();
        var response = await client.PostAsJsonAsync("/optimize", new
        {
            serviceType = "locksmith",
            location = new { latitude = 0d, longitude = 0d },
            searchRadiusKm = 1d
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        Assert.NotNull(problem);
        Assert.Equal("no_provider_available", problem.Error);

        await using var after = _fixture.CreateContext();
        Assert.Equal(casesBefore, await after.AssistanceCases.CountAsync());
    }

    [Fact]
    public async Task Optimize_returns_400_for_an_unresolvable_location()
    {
        var client = _fixture.ApiFactory.CreateClient();

        var response = await client.PostAsJsonAsync("/optimize", new
        {
            serviceType = "locksmith",
            location = new { latitude = 999d, longitude = 0d }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        Assert.Equal("invalid_request", problem!.Error);
    }

    [Fact]
    public async Task Optimize_returns_400_for_an_unknown_service_type()
    {
        var client = _fixture.ApiFactory.CreateClient();

        var response = await client.PostAsJsonAsync("/optimize", new
        {
            serviceType = "helicopter",
            location = new { latitude = 0d, longitude = 0d }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        Assert.Equal("invalid_request", problem!.Error);
    }

    [Fact]
    public async Task Optimize_returns_400_when_the_location_is_missing()
    {
        var client = _fixture.ApiFactory.CreateClient();

        var response = await client.PostAsJsonAsync("/optimize", new { serviceType = "locksmith" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
