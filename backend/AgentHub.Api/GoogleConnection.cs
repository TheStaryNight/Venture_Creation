using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

// Local-development OAuth connection. Tokens stay in server memory, not in localStorage or API responses.
static class GoogleConnection {
    public const string Callback = "http://127.0.0.1:5080/api/google/callback";
    public const string Frontend = "http://127.0.0.1:5173/";
    public const string DriveScope = "https://www.googleapis.com/auth/drive.file";
    public const string CalendarScope = "https://www.googleapis.com/auth/calendar.events.owned";
    public static bool Configured(IConfiguration config) => !string.IsNullOrWhiteSpace(config["Google:ClientId"]) && !string.IsNullOrWhiteSpace(config["Google:ClientSecret"]);

    public static void AddGoogleConnection(this WebApplicationBuilder builder) {
        builder.Services.AddSingleton<GoogleTokenStore>();
        var protection = builder.Services.AddDataProtection().SetApplicationName("Orbit.Local")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".local", "keys")));
        if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
        var auth = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options => {
                options.Cookie.Name = "Orbit.Google.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            });
        if (!Configured(builder.Configuration)) return;
        auth.AddOAuth("Google", options => {
            options.ClientId = builder.Configuration["Google:ClientId"]!;
            options.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
            options.CallbackPath = "/api/google/callback";
            options.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
            options.TokenEndpoint = "https://oauth2.googleapis.com/token";
            options.UsePkce = true;
            options.Scope.Add(DriveScope);
            options.Scope.Add(CalendarScope);
            options.SaveTokens = false;
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Events.OnRedirectToAuthorizationEndpoint = context => {
                context.Response.Redirect(QueryHelpers.AddQueryString(context.RedirectUri, new Dictionary<string, string?> {
                    ["access_type"] = "offline", ["prompt"] = "consent"
                }));
                return Task.CompletedTask;
            };
            options.Events.OnCreatingTicket = context => {
                if (string.IsNullOrWhiteSpace(context.AccessToken)) throw new InvalidOperationException("Missing Google access token.");
                var id = Guid.NewGuid().ToString("N");
                var scopes = context.TokenResponse.Response!.RootElement.TryGetProperty("scope", out var scope) ? scope.GetString() ?? "" : "";
                var token = new GoogleTokens(context.AccessToken, context.RefreshToken, DateTimeOffset.UtcNow.Add(context.ExpiresIn ?? TimeSpan.FromHours(1)), scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                context.HttpContext.RequestServices.GetRequiredService<GoogleTokenStore>().Items[id] = token;
                context.Identity!.AddClaim(new Claim("orbit_google_connection", id));
                return Task.CompletedTask;
            };
            options.Events.OnRemoteFailure = context => {
                context.HandleResponse();
                context.Response.Redirect(Frontend + "?google=failed");
                return Task.CompletedTask;
            };
        });
    }

    public static void MapGoogleConnection(this WebApplication app) {
        app.MapGet("/api/google/connect", (IConfiguration config, HttpContext context) => {
            if (!Configured(config)) return Results.BadRequest(new { error = "Isi Google__ClientId dan Google__ClientSecret pada backend, lalu restart." });
            // Enter directly on the backend origin so the callback URI is deterministic.
            if (context.Request.Host.Value != "127.0.0.1:5080") return Results.Redirect("http://127.0.0.1:5080/api/google/connect");
            return Results.Challenge(new AuthenticationProperties { RedirectUri = Frontend + "?google=connected" }, new[] { "Google" });
        });
    }
}
sealed record GoogleTokens(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt, string[] Scopes);
sealed class GoogleTokenStore {
    public ConcurrentDictionary<string, GoogleTokens> Items { get; } = new();
    public bool HasScope(HttpContext context, string scope) {
        var id = context.User.FindFirstValue("orbit_google_connection");
        return id is not null && Items.TryGetValue(id, out var token) && token.ExpiresAt > DateTimeOffset.UtcNow && token.Scopes.Contains(scope);
    }
}
