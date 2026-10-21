using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

static class WorkspaceEndpoints
{
    public static void ConfigureDeployment(this WebApplication app, OrbitDatabase database)
    {
        var isPublic = app.Configuration.GetValue<bool>("Deployment:Public");
        if (!isPublic)
            return;
        if (!database.Enabled || !GoogleConnection.Configured(app.Configuration))
            throw new InvalidOperationException("Public deployment requires PostgreSQL and Google OAuth configuration.");
        var frontend = new Uri(GoogleConnection.Frontend(app.Configuration));
        if (frontend.Scheme != "https" || frontend.IsLoopback)
            throw new InvalidOperationException("Public frontend must use an HTTPS domain.");
        // All browser calls, including OAuth callback, enter through the Vercel /api rewrite.
        // Use a configured canonical origin rather than trusting arbitrary forwarded headers.
        app.Use(async (context, next) =>
        {
            context.Request.Scheme = "https";
            context.Request.Host = new HostString(frontend.Authority);
            if (context.Request.Method is not ("GET" or "HEAD" or "OPTIONS") && context.Request.Path != "/api/google/callback"
                && (context.Request.Headers["X-Orbit-Request"] != "1" || context.Request.Headers.Origin != frontend.GetLeftPart(UriPartial.Authority)))
            {
                context.Response.StatusCode = 403;
                return;
            }
            await next();
        });
    }
    public static void UseWorkspaceAccess(this WebApplication app, OrbitDatabase database)
    {
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? "";
            if (database.Enabled && path.StartsWith("/api/") && path is not ("/api/health" or "/api/session" or "/api/google/connect" or "/api/google/callback"))
            {
                var owner = context.User.FindFirstValue("orbit_google_connection");
                if (owner is null)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Masuk dengan Google terlebih dahulu."
                    });
                    return;
                }
            }
            try
            {
                await next();
            }
            catch (Npgsql.NpgsqlException) when (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Database belum dapat dihubungi. Perubahan belum dikonfirmasi; coba lagi."
                });
            }
        });
    }
    public static void MapWorkspaceEndpoints(this WebApplication app, OrbitDatabase database)
    {
        app.MapGet("/api/session", (HttpContext context) => new
        {
            database = database.Enabled,
            authenticated = context.User.FindFirstValue("orbit_google_connection") is not null,
            owner = context.User.FindFirstValue("orbit_google_connection"),
            loginUrl = app.Configuration.GetValue<bool>("Deployment:Public") ? "/api/google/connect" : "http://127.0.0.1:5080/api/google/connect"
        });
        app.MapPost("/api/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync();
            return Results.Ok(new
            {
                success = true
            });
        });
        app.MapGet("/api/workspace", (HttpContext context) =>
        {
            if (!database.Enabled)
                return Results.NotFound();
            var saved = database.ReadWorkspace(context.User.FindFirstValue("orbit_google_connection")!);
            return Results.Ok(new
            {
                state = saved.State,
                revision = saved.Revision
            });
        });
        app.MapPost("/api/workspace", (WorkspaceRequest request, HttpContext context) =>
        {
            if (!database.Enabled)
                return Results.NotFound();
            if (!Valid(request.State))
                return Results.BadRequest(new
                {
                    error = "Data workspace tidak valid atau melebihi batas penyimpanan."
                });
            var revision = database.SaveWorkspace(context.User.FindFirstValue("orbit_google_connection")!, request.Revision, request.State);
            return revision is null ? Results.Conflict(new
            {
                error = "Workspace berubah di tab/perangkat lain. Unduh salinan data lokal, lalu muat ulang sebelum mengedit kembali."
            }) : Results.Ok(new
            {
                revision
            });
        });
        app.MapPost("/api/billing/start-trial", (HttpContext context) =>
        {
            if (!database.Enabled)
                return Results.BadRequest(new
                {
                    error = "Trial membutuhkan database."
                });
            var owner = context.User.FindFirstValue("orbit_google_connection")!;
            database.StartTrial(owner); // One-time, idempotent: never resets an expired trial.
            return Results.Ok(database.Billing(owner));
        });
        app.MapPost("/api/connections/discord", (DiscordRequest request, HttpContext context) =>
        {
            if (!database.Enabled)
                return Results.BadRequest(new
                {
                    error = "Penyimpanan koneksi memerlukan database."
                });
            if (!Webhook.IsValid(request.Url) || request.Url.Length > 512)
                return Results.BadRequest(new
                {
                    error = "URL webhook Discord tidak valid."
                });
            database.SaveConnection(context.User.FindFirstValue("orbit_google_connection")!, "discord", request.Url);
            return Results.Ok(new
            {
                connected = true
            });
        });
    }
    internal static bool Valid(JsonElement state)
    {
        try
        {
            if (state.GetRawText().Length > 256_000)
                return false;
            if (state.GetProperty("workspace").GetString() is not { Length: > 0 and <= 60 })
                return false;
            if (state.GetProperty("draft").GetString() is not { Length: <= 2000 })
                return false;
            var projects = state.GetProperty("projects");
            if (projects.GetArrayLength() > 100)
                return false;
            var ids = new HashSet<string>();
            foreach (var p in projects.EnumerateArray())
            {
                if (!Guid.TryParse(p.GetProperty("id").GetString(), out var id) || !ids.Add(id.ToString()))
                    return false;
                if (p.GetProperty("name").GetString() is not { Length: > 0 and <= 80 } || p.GetProperty("description").GetString() is not { Length: <= 300 }
                    || p.GetProperty("draft").GetString() is not { Length: <= 2000 } || p.GetProperty("deadline").GetString() is not { Length: <= 10 })
                    return false;
                var tasks = p.GetProperty("tasks");
                if (tasks.GetArrayLength() > 200)
                    return false;
                foreach (var task in tasks.EnumerateArray())
                {
                    if (!Guid.TryParse(task.GetProperty("id").GetString(), out _) || task.GetProperty("title").GetString() is not { Length: > 0 and <= 200 })
                        return false;
                    _ = task.GetProperty("done").GetBoolean();
                }
            }
            return true;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException) { return false; }
    }
}
record WorkspaceRequest(long Revision, JsonElement State);
record DiscordRequest(string Url);
