using ProviderOptimizer.Application;
using ProviderOptimizer.Api.Contracts;
using ProviderOptimizer.Domain;

namespace ProviderOptimizer.Api.Endpoints;

/// <summary>Maps the domain and application shapes onto the wire contract.</summary>
internal static class PayloadMapper
{
    public static AssistanceRequest ToDomain(OptimizeRequest request, SearchOptions options)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceType))
        {
            throw new ArgumentException("serviceType is required.", nameof(request));
        }

        if (request.Location is null)
        {
            throw new ArgumentException("location is required.", nameof(request));
        }

        return AssistanceRequest.Create(
            ServiceType.Parse(request.ServiceType),
            CustomerLocation.FromCoordinates(request.Location.Latitude, request.Location.Longitude),
            request.SearchRadiusKm ?? options.DefaultRadiusKm);
    }

    public static AssistanceRequest ToDomain(
        string? serviceType,
        double? latitude,
        double? longitude,
        double? radiusKm,
        SearchOptions options)
    {
        if (string.IsNullOrWhiteSpace(serviceType))
        {
            throw new ArgumentException("serviceType is required.", nameof(serviceType));
        }

        return AssistanceRequest.Create(
            ServiceType.Parse(serviceType),
            CustomerLocation.FromCoordinates(latitude, longitude),
            radiusKm ?? options.DefaultRadiusKm);
    }

    public static ProviderPayload ToPayload(Provider provider)
        => new(
            provider.Id,
            provider.Name,
            provider.ServiceType.Value,
            provider.Location.Latitude,
            provider.Location.Longitude,
            provider.Rating,
            provider.EtaMinutes,
            provider.IsAvailable);

    public static OptimizePayload ToPayload(OptimizeOutcome outcome)
        => new(
            outcome.Case!.CaseId,
            ToPayload(outcome.Provider!),
            outcome.Breakdown!.Total,
            outcome.Breakdown.Contributions
                .Select(contribution => new ScoreFactorPayload(
                    contribution.Factor,
                    contribution.Raw,
                    contribution.Weight,
                    contribution.Contribution))
                .ToList(),
            outcome.Case.CreatedAt);
}
