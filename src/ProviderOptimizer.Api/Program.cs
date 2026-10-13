using Microsoft.EntityFrameworkCore;
using ProviderOptimizer.Api;
using ProviderOptimizer.Api.Endpoints;
using ProviderOptimizer.Application;
using ProviderOptimizer.Application.Ports;
using ProviderOptimizer.Domain;
using ProviderOptimizer.Infrastructure;
using ProviderOptimizer.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("OptimizerDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'OptimizerDb' is required. Set the ConnectionStrings__OptimizerDb environment variable.");
}

// Startup fails on a missing, negative or all-zero weight set: that is a misconfiguration, not a default.
var weights = ScoringWeights.FromConfiguration(
    builder.Configuration.GetValue<double?>("Scoring:Eta"),
    builder.Configuration.GetValue<double?>("Scoring:Distance"),
    builder.Configuration.GetValue<double?>("Scoring:Availability"),
    builder.Configuration.GetValue<double?>("Scoring:Rating"));

var searchOptions = new SearchOptions(
    builder.Configuration.GetValue<double?>("Search:DefaultRadiusKm") ?? 10d);

builder.Services.AddSingleton(weights);
builder.Services.AddSingleton(searchOptions);
builder.Services.AddSingleton<IProviderScoringPolicy, ProviderScoringPolicy>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddOptimizerPersistence(connectionString);
builder.Services.AddScoped<OptimizeProviderHandler>();
builder.Services.AddScoped<ListAvailableProvidersHandler>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<OptimizerDbContext>().Database.MigrateAsync();
}

app.MapOptimizeEndpoint();
app.MapAvailableProvidersEndpoint();

await app.RunAsync();

// Exposed so the integration tests can host the real application.
public partial class Program { }
