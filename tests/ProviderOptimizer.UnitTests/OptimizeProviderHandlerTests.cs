using ProviderOptimizer.Application;
using ProviderOptimizer.Application.Ports;
using ProviderOptimizer.Domain;
using Xunit;

namespace ProviderOptimizer.UnitTests;

/// <summary>
/// Verifies the application use cases against fake ports, including the rule that nothing is
/// persisted when no provider is selected.
/// </summary>
public sealed class OptimizeProviderHandlerTests
{
    private const string ProviderA = "11111111-1111-1111-1111-111111111111";
    private const string ProviderB = "22222222-2222-2222-2222-222222222222";

    private static readonly GeoPoint Origin = GeoPoint.Create(0d, 0d);
    private static readonly ScoringWeights Weights = ScoringWeights.Create(0.5d, 0.2d, 0.1d, 0.2d);
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_successful_optimization_persists_exactly_one_case()
    {
        var cases = new RecordingCaseRepository();
        var handler = NewHandler(
            [NewProvider(ProviderA, 0.01d), NewProvider(ProviderB, 0.05d)],
            cases);

        var outcome = await handler.HandleAsync(NewRequest());

        Assert.True(outcome.HasSelection);
        var stored = Assert.Single(cases.Saved);
        Assert.Equal(outcome.Case!.CaseId, stored.CaseId);
        Assert.Equal(outcome.Provider!.Id, stored.ProviderId);
        Assert.Equal(ServiceType.Locksmith, stored.ServiceType);
        Assert.Equal(Now, stored.CreatedAt);
    }

    [Fact]
    public async Task Persisted_breakdown_matches_the_returned_one()
    {
        var cases = new RecordingCaseRepository();
        var handler = NewHandler([NewProvider(ProviderA, 0.01d), NewProvider(ProviderB, 0.05d)], cases);

        var outcome = await handler.HandleAsync(NewRequest());

        var stored = Assert.Single(cases.Saved);
        Assert.Equal(
            outcome.Breakdown!.Contributions.Select(contribution => contribution.Factor),
            stored.Breakdown.Contributions.Select(contribution => contribution.Factor));
        Assert.Equal(outcome.Breakdown.Total, stored.Breakdown.Total, precision: 12);
    }

    [Fact]
    public async Task An_empty_candidate_set_persists_nothing()
    {
        var cases = new RecordingCaseRepository();
        // Only out-of-radius providers, so eligibility yields nothing.
        var handler = NewHandler([NewProvider(ProviderA, 0.5d), NewProvider(ProviderB, 0.6d)], cases);

        var outcome = await handler.HandleAsync(NewRequest());

        Assert.False(outcome.HasSelection);
        Assert.Null(outcome.Case);
        Assert.Null(outcome.Provider);
        Assert.Null(outcome.Breakdown);
        Assert.Empty(cases.Saved);
    }

    [Fact]
    public async Task An_unavailable_provider_is_never_selected()
    {
        var cases = new RecordingCaseRepository();
        var onlyCandidate = NewProvider(ProviderA, 0.01d, available: false);
        var handler = NewHandler([onlyCandidate], cases);

        var outcome = await handler.HandleAsync(NewRequest());

        Assert.False(outcome.HasSelection);
        Assert.Empty(cases.Saved);
    }

    [Fact]
    public async Task Listing_available_providers_scores_and_records_nothing()
    {
        var cases = new RecordingCaseRepository();
        var providers = new FakeProviderRepository([NewProvider(ProviderA, 0.01d), NewProvider(ProviderB, 0.05d)]);
        var handler = new ListAvailableProvidersHandler(providers);

        var candidates = await handler.HandleAsync(NewRequest());

        Assert.Equal(2, candidates.Count);
        Assert.Empty(cases.Saved);
    }

    private static OptimizeProviderHandler NewHandler(
        IReadOnlyCollection<Provider> providers,
        RecordingCaseRepository cases)
        => new(
            new FakeProviderRepository(providers),
            cases,
            new ProviderScoringPolicy(),
            new FixedClock(),
            Weights);

    private static AssistanceRequest NewRequest()
        => AssistanceRequest.Create(
            ServiceType.Locksmith,
            CustomerLocation.FromCoordinates(Origin.Latitude, Origin.Longitude),
            searchRadiusKm: 10d);

    private static Provider NewProvider(string id, double latitude, bool available = true)
        => Provider.Create(
            Guid.Parse(id),
            "provider-" + id[..4],
            ServiceType.Locksmith,
            GeoPoint.Create(latitude, 0d),
            etaMinutes: 10,
            rating: 4d,
            isAvailable: available);

    /// <summary>
    /// Returns every provider it holds, deliberately ignoring the service type: the eligibility
    /// rules live in the application path, so the double must not reproduce them.
    /// </summary>
    private sealed class FakeProviderRepository : IProviderRepository
    {
        private readonly IReadOnlyCollection<Provider> _providers;

        public FakeProviderRepository(IReadOnlyCollection<Provider> providers) => _providers = providers;

        public Task<IReadOnlyCollection<Provider>> FindByServiceTypeAsync(
            ServiceType serviceType,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_providers);
    }

    private sealed class RecordingCaseRepository : IAssistanceCaseRepository
    {
        public List<AssistanceCase> Saved { get; } = [];

        public Task SaveAsync(AssistanceCase assistanceCase, CancellationToken cancellationToken = default)
        {
            Saved.Add(assistanceCase);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
