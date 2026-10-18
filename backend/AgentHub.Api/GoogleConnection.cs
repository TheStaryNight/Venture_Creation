using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

// Local-development OAuth connection. Tokens stay in server memory, not in localStorage or API responses.
static class GoogleConnection {
    public const string Callback = "http://127.0.0.1:5080/api/google/callback";
    public static string Frontend(IConfiguration config) => (config["Deployment:FrontendUrl"] ?? "http://127.0.0.1:5173").TrimEnd('/') + "/";
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
            options.Scope.Add("openid");
            options.Scope.Add("email");
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
            options.Events.OnCreatingTicket = async context => {
                if (string.IsNullOrWhiteSpace(context.AccessToken)) throw new InvalidOperationException("Missing Google access token.");
                using var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
                userRequest.Headers.Authorization = new("Bearer", context.AccessToken);
                using var userResponse = await context.Backchannel.SendAsync(userRequest, context.HttpContext.RequestAborted);
                userResponse.EnsureSuccessStatusCode();
                using var profile = System.Text.Json.JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync());
                var id = "google:" + profile.RootElement.GetProperty("sub").GetString()!;
                context.HttpContext.RequestServices.GetRequiredService<OrbitDatabase>().EnsureUser(id);
                var scopes = context.TokenResponse.Response!.RootElement.TryGetProperty("scope", out var scope) ? scope.GetString() ?? "" : "";
                var token = new GoogleTokens(context.AccessToken, context.RefreshToken, DateTimeOffset.UtcNow.Add(context.ExpiresIn ?? TimeSpan.FromHours(1)), scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                context.HttpContext.RequestServices.GetRequiredService<GoogleTokenStore>().Save(id, token);
                context.Identity!.AddClaim(new Claim("orbit_google_connection", id));
            };
            options.Events.OnRemoteFailure = context => {
                context.HandleResponse();
                context.Response.Redirect(Frontend(builder.Configuration) + "?google=failed");
                return Task.CompletedTask;
            };
        });
    }

    public static void MapGoogleConnection(this WebApplication app) {
        app.MapGet("/api/google/connect", (IConfiguration config, HttpContext context) => {
            if (!Configured(config)) return Results.BadRequest(new { error = "Isi Google__ClientId dan Google__ClientSecret pada backend, lalu restart." });
            // Enter directly on the backend origin so the callback URI is deterministic.
            if (!config.GetValue<bool>("Deployment:Public") && context.Request.Host.Value != "127.0.0.1:5080") return Results.Redirect("http://127.0.0.1:5080/api/google/connect");
            return Results.Challenge(new AuthenticationProperties { RedirectUri = Frontend(config) + "?google=connected" }, new[] { "Google" });
        });
    }
}
sealed record GoogleTokens(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt, string[] Scopes);
sealed class GoogleTokenStore(OrbitDatabase? database = null, IConfiguration? config = null) {
    private readonly ConcurrentDictionary<string, SemaphoreSlim> refreshLocks = new();
    public async Task Refresh(string owner, HttpClient client, CancellationToken ct) {
        if (!TryGet(owner, out var token) || token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1) || string.IsNullOrEmpty(token.RefreshToken) || config is null || !GoogleConnection.Configured(config)) return;
        var gate = refreshLocks.GetOrAdd(owner, _ => new SemaphoreSlim(1,1));
        await gate.WaitAsync(ct);
        try {
            TryGet(owner, out token!);
            if (token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1)) return;
            using var response = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string,string> {
                ["client_id"] = config["Google:ClientId"]!, ["client_secret"] = config["Google:ClientSecret"]!,
                ["refresh_token"] = token.RefreshToken!, ["grant_type"] = "refresh_token"
            }), ct);
            if (!response.IsSuccessStatusCode) throw new ActionFailure("Sesi Google perlu diperbarui. Hubungkan ulang akun Google.");
            using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var access = json.RootElement.GetProperty("access_token").GetString();
            if (string.IsNullOrWhiteSpace(access)) throw new ActionFailure("Google belum memberikan akses baru. Hubungkan ulang akun.");
            Save(owner, token with { AccessToken = access, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(json.RootElement.GetProperty("expires_in").GetInt32()) });
        } finally { gate.Release(); }
    }
    public void Save(string owner, GoogleTokens token) { database?.SaveConnection(owner, "google", token); Items[owner] = token; }
    public bool TryGet(string owner, out GoogleTokens token) {
        if (Items.TryGetValue(owner, out token!)) return true;
        token = database?.ReadConnection<GoogleTokens>(owner, "google")!;
        if (token is null) return false;
        Items[owner] = token; return true;
    }
    public ConcurrentDictionary<string, GoogleTokens> Items { get; } = new();
    public bool HasScope(HttpContext context, string scope) {
        var id = context.User.FindFirstValue("orbit_google_connection");
        return id is not null && TryGet(id, out var token) && (token.ExpiresAt > DateTimeOffset.UtcNow || !string.IsNullOrEmpty(token.RefreshToken)) && token.Scopes.Contains(scope);
    }
}
