using System.Data;
using Application.Interfaces.Database;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WebApi.Common.Health;

// Readiness probe: opens a SQL connection through the same factory the app uses.
// Opening validates network reachability + authentication without running any
// ad-hoc query (honouring the "stored procedures only" data-access rule).
internal sealed class DatabaseHealthCheck(ISqlConnectionFactory connectionFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            return connection.State == ConnectionState.Open
                ? HealthCheckResult.Healthy("Database connection established.")
                : HealthCheckResult.Unhealthy("Database connection could not be opened.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database unreachable.", ex);
        }
    }
}
