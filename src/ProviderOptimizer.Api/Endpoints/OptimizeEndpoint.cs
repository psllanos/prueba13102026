using ProviderOptimizer.Api.Contracts;
using ProviderOptimizer.Application;

namespace ProviderOptimizer.Api.Endpoints;

/// <summary>POST /optimize - selects the optimal provider and records the case.</summary>
internal static class OptimizeEndpoint
{
    public static IEndpointRouteBuilder MapOptimizeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapPost("/optimize", HandleAsync)
            .WithName("Optimize")
            .WithSummary("Selects the optimal provider for an assistance request and records the case.")
            .Produces<OptimizePayload>(StatusCodes.Status200OK)
            .Produces<ProblemPayload>(StatusCodes.Status400BadRequest)
            .Produces<ProblemPayload>(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        OptimizeRequest? request,
        OptimizeProviderHandler handler,
        SearchOptions options,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new ProblemPayload("invalid_request", "A request body is required."));
        }

        Domain.AssistanceRequest domainRequest;

        try
        {
            domainRequest = PayloadMapper.ToDomain(request, options);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new ProblemPayload("invalid_request", exception.Message));
        }

        var outcome = await handler.HandleAsync(domainRequest, cancellationToken);

        if (!outcome.HasSelection)
        {
            return Results.Conflict(new ProblemPayload(
                "no_provider_available",
                "No provider matched the requested service type, location and search radius. Nothing was recorded."));
        }

        return Results.Ok(PayloadMapper.ToPayload(outcome));
    }
}
