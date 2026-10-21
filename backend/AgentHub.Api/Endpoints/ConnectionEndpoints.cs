using System.Security.Claims;

static class ConnectionEndpoints
{
    public static void MapConnectionEndpoints(this WebApplication app, OrbitDatabase database)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/api/connections", (IConfiguration config, HttpContext context, GoogleTokenStore google) => new
        {
            googleConfigured = GoogleConnection.Configured(config),
            ai = AiSettings.Read(config).Ready,
            aiProvider = AiSettings.Read(config).Provider,
            services = new[] {
        new { id = "gmail", name = "Gmail", connected = google.HasScope(context, GoogleConnection.GmailScope), detail = "Draft dan kirim email setelah persetujuan" },
        new { id = "docs", name = "Google Docs", connected = google.HasScope(context, GoogleConnection.DriveScope), detail = "Buat dokumen berisi teks" },
        new { id = "drive", name = "Google Drive", connected = google.HasScope(context, GoogleConnection.DriveScope), detail = "Membuat folder di My Drive" },
        new { id = "calendar", name = "Google Calendar", connected = google.HasScope(context, GoogleConnection.CalendarScope), detail = "Membuat event di kalender utama" },
        new { id = "meet", name = "Google Meet", connected = google.HasScope(context, GoogleConnection.CalendarScope), detail = "Tautan meeting melalui Google Calendar" },
        new { id = "discord", name = "Discord", connected = Webhook.IsValid(database.Enabled ? database.ReadConnection<string>(context.User.FindFirstValue("orbit_google_connection")!, "discord") : config["Discord:WebhookUrl"]), detail = "Webhook dikonfigurasi oleh pemilik server" }
    }
        });

    }
}
