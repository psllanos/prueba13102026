using ProviderOptimizer.Api.Contracts;
using ProviderOptimizer.Application;

namespace ProviderOptimizer.Api.Endpoints;

/// <summary>GET /providers/available - eligibility only, no scoring and no writes.</summary>
internal static class AvailableProvidersEndpoint
{
    public static IEndpointRouteBuilder MapAvailableProvidersEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapGet("/providers/available", HandleAsync)
            .WithName("ListAvailableProviders")
            .WithSummary("Lists the providers eligible for a service type within a radius of a location.")
            .Produces<IReadOnlyList<ProviderPayload>>(StatusCodes.Status200OK)
            .Produces<ProblemPayload>(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        string? serviceType,
        double? latitude,
        double? longitude,
        double? radiusKm,
        ListAvailableProvidersHandler handler,
        SearchOptions options,
        CancellationToken cancellationToken)
    {
        Domain.AssistanceRequest domainRequest;

        try
        {
            domainRequest = PayloadMapper.ToDomain(serviceType, latitude, longitude, radiusKm, options);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new ProblemPayload("invalid_request", exception.Message));
        }

        var providers = await handler.HandleAsync(domainRequest, cancellationToken);

        return Results.Ok(providers.Select(PayloadMapper.ToPayload).ToList());
    }
}
