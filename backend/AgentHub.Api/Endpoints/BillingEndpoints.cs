using System.Security.Claims;

static class BillingEndpoints
{
    public static void MapBillingEndpoints(this WebApplication app, OrbitDatabase database)
    {
        app.MapGet("/api/billing", (FeatureAccess access, HttpContext context) => database.Enabled ? database.Billing(context.User.FindFirstValue("orbit_google_connection")!) : access.Snapshot());
        app.MapPost("/api/billing/simulate-payment", (FeatureAccess access, HttpContext context) =>
        {
            if (database.Enabled || app.Configuration.GetValue<bool>("Deployment:Public"))
                return Results.StatusCode(403);
            if (context.Connection.RemoteIpAddress is not { } address || !System.Net.IPAddress.IsLoopback(address))
                return Results.StatusCode(403);
            access.ActivateSimulation();
            return Results.Ok(access.Snapshot());
        });

    }
}
