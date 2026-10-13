using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ProviderOptimizer.IntegrationTests;

/// <summary>
/// Hosts the real application, migrations on startup included, against the collection's throwaway
/// Postgres. The production startup path is what runs here, not a reduced test harness.
/// </summary>
public sealed class OptimizerApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public OptimizerApiFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseSetting("ConnectionStrings:OptimizerDb", _connectionString);
}
