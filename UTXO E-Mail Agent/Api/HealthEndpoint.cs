using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace UTXO_E_Mail_Agent.Api;

public static class HealthEndpoint
{
    public static void MapHealthEndpoints(this WebApplication app, string version)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", version = version }))
            .WithName("HealthCheck")
            .WithSummary("Health check")
            .Produces(StatusCodes.Status200OK);
    }
}
