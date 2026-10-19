using System.Security.Claims;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
builder.Services.AddSingleton<OrbitDatabase>();
if (Environment.GetEnvironmentVariable("PORT") is string port) builder.WebHost.UseUrls("http://0.0.0.0:" + port);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 300_000);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddHttpClient("external", client => client.Timeout = TimeSpan.FromSeconds(45));
builder.Services.AddSingleton<WorkflowStore>();
builder.Services.AddSingleton<FeatureAccess>();
builder.Services.AddHostedService<WorkflowWorker>();
builder.AddGoogleConnection();
var app = builder.Build();
var database = app.Services.GetRequiredService<OrbitDatabase>();
if (args.Contains("--migrate")) { await database.Migrate(); Console.WriteLine("Orbit migration applied."); return; }
await database.Initialize();
app.Services.GetRequiredService<WorkflowStore>().Restore();
app.ConfigureDeployment(database);
app.UseAuthentication();
app.UseWorkspaceAccess(database);
app.MapWorkspaceEndpoints(database);
app.MapGoogleConnection();
app.MapDriveFolders();
app.MapGet("/api/billing", (FeatureAccess access, HttpContext context) => database.Enabled ? database.Billing(context.User.FindFirstValue("orbit_google_connection")!) : access.Snapshot());
app.MapPost("/api/billing/simulate-payment", (FeatureAccess access, HttpContext context) => {
    if (database.Enabled || app.Configuration.GetValue<bool>("Deployment:Public")) return Results.StatusCode(403);
    if (context.Connection.RemoteIpAddress is not { } address || !System.Net.IPAddress.IsLoopback(address)) return Results.StatusCode(403);
    access.ActivateSimulation();
    return Results.Ok(access.Snapshot());
});
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/connections", (IConfiguration config, HttpContext context, GoogleTokenStore google) => new {
    googleConfigured = GoogleConnection.Configured(config),
    ai = AiSettings.Read(config).Ready, aiProvider = AiSettings.Read(config).Provider,
    services = new[] {
        new { id = "drive", name = "Google Drive", connected = google.HasScope(context, GoogleConnection.DriveScope), detail = "Membuat folder di My Drive" },
        new { id = "calendar", name = "Google Calendar", connected = google.HasScope(context, GoogleConnection.CalendarScope), detail = "Membuat event di kalender utama" },
        new { id = "meet", name = "Google Meet", connected = google.HasScope(context, GoogleConnection.CalendarScope), detail = "Tautan meeting melalui Google Calendar" },
        new { id = "discord", name = "Discord", connected = Webhook.IsValid(database.Enabled ? database.ReadConnection<string>(context.User.FindFirstValue("orbit_google_connection")!, "discord") : config["Discord:WebhookUrl"]), detail = "Webhook dikonfigurasi oleh pemilik server" }
    }
});
app.MapGet("/api/workflows", (WorkflowStore store, HttpContext context) => store.Items.Values.Where(w => w.Owner == context.User.FindFirstValue("orbit_google_connection")).OrderByDescending(w => w.CreatedAt).Select(w => w.Snapshot()));
app.MapGet("/api/workflows/{id:guid}", (Guid id, WorkflowStore store, HttpContext context) =>
    store.Items.TryGetValue(id, out var w) && w.Owner == context.User.FindFirstValue("orbit_google_connection") ? Results.Ok(w.Snapshot()) : Results.NotFound());

app.MapPost("/api/workflows/plan", async (PlanRequest request, WorkflowStore store, IConfiguration config, IHttpClientFactory clients, HttpContext context, GoogleTokenStore google, FeatureAccess access, CancellationToken ct) => {
    if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 2000)
        return Results.BadRequest(new { error = "Isi instruksi antara 1–2.000 karakter." });
    if (request.Mode is not ("demo" or "live")) return Results.BadRequest(new { error = "Mode tidak valid." });
    var settings = AiSettings.Read(config); if (settings.Error is not null) return Results.BadRequest(new { error = settings.Error }); var aiEnabled = settings.Ready;
    List<Step> steps;
    var source = "template";
    if (aiEnabled) {
        if (database.Enabled && !database.ReservePlan(context.User.FindFirstValue("orbit_google_connection")!)) return Results.Json(new { error = "Kuota 20 penyusunan AI bulan ini habis. Kuota direset awal bulan UTC.", code = "quota_exceeded" }, statusCode: 429);
        try {
            steps = await Planner.Create(request.Prompt, config, clients.CreateClient("external"), ct);
            source = "ai";
        } catch (PlannerException ex) {
            return Results.Problem(statusCode: 502, title: ex.Message);
        } catch (TaskCanceledException) when (!ct.IsCancellationRequested) {
            return Results.Problem(statusCode: 504, title: "Layanan AI tidak merespons dalam 45 detik. Coba lagi atau pilih model lain yang tersedia di akun.");
        } catch (HttpRequestException ex) {
            var message = ex.StatusCode is null
                ? "Koneksi ke layanan AI gagal sebelum respons diterima. Periksa internet, DNS, proxy, atau koneksi TLS pada komputer backend."
                : $"Layanan AI mengembalikan HTTP {(int)ex.StatusCode}. Periksa akses API dan konfigurasi provider.";
            return Results.Problem(statusCode: 502, title: message);
        } catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException) {
            return Results.Problem(statusCode: 502, title: "Respons AI tidak berisi rencana JSON yang valid atau lengkap. Coba instruksi lebih singkat atau model lain yang mendukung Structured Outputs.");
        }
    } else {
        if (request.Mode == "live") return Results.BadRequest(new { error = "Pengiriman nyata memerlukan konfigurasi Gemini atau OpenAI. Uji tanpa AI memakai template contoh." });
        steps = [
            new("drive", "Buat folder proyek", "Contoh: folder Proyek AI Kelompok untuk menyimpan hasil kerja.") { Action = "create_folder" },
            new("calendar", "Jadwalkan diskusi kelompok", "Contoh: jadwal belum ditetapkan. Mode demo tidak membuat event."),
            new("discord", "Bagikan rangkuman", "Contoh: kirim rangkuman persiapan proyek ke channel kelompok.")
        ];
    }
    if (steps.Count is < 1 or > 5 || steps.Any(s => s.Agent is not ("drive" or "calendar" or "discord") || string.IsNullOrWhiteSpace(s.Title) || string.IsNullOrWhiteSpace(s.Detail)))
        return Results.BadRequest(new { error = "Rencana tidak valid. Coba instruksi yang lebih spesifik." });
    if (source == "ai" && PlanContract.Validate(steps) is string validation) return Results.BadRequest(new { error = validation });
    if (request.Mode == "live") {
        if (access.Blocks(steps, context.User.FindFirstValue("orbit_google_connection"))) return FeatureAccess.UpgradeRequired();
        if (steps.Any(s => s.Agent == "discord") && !Webhook.IsValid(database.Enabled ? database.ReadConnection<string>(context.User.FindFirstValue("orbit_google_connection")!, "discord") : config["Discord:WebhookUrl"])) return Results.BadRequest(new { error = "Konfigurasi webhook Discord belum valid." });
        if (steps.Any(s => s.Agent == "drive") && !google.HasScope(context, GoogleConnection.DriveScope)) return Results.BadRequest(new { error = "Hubungkan akun Google dan berikan izin Drive terlebih dahulu." });
        if (steps.Any(s => s.Agent == "calendar") && !google.HasScope(context, GoogleConnection.CalendarScope)) return Results.BadRequest(new { error = "Hubungkan akun Google dan berikan izin Calendar terlebih dahulu." });
    }
    var workflow = new Workflow(request.Prompt.Trim(), request.Mode, source, steps) { Owner = context.User.FindFirstValue("orbit_google_connection") };
    store.Persist(workflow);
    store.Items[workflow.Id] = workflow;
    return Results.Ok(workflow.Snapshot());
});
app.MapPost("/api/workflows/{id:guid}/execute", (Guid id, ExecuteRequest request, WorkflowStore store, HttpContext context, FeatureAccess access) => {
    if (!store.Items.TryGetValue(id, out var workflow) || workflow.Owner != context.User.FindFirstValue("orbit_google_connection")) return Results.NotFound();
    if (workflow.Mode == "live" && access.Blocks(workflow.Steps, workflow.Owner)) return FeatureAccess.UpgradeRequired();
    lock (workflow.Sync) {
        if (workflow.Status != "planned") return Results.Conflict(new { error = "Rencana ini sudah dijalankan. Lihat status pekerjaan." });
        if (workflow.Mode == "live" && workflow.Steps.Any(s => s.Agent == "drive" && s.Action == "create_folder") && !request.ConfirmNewFolder)
            return Results.BadRequest(new { error = "Konfirmasi pembuatan folder baru. Untuk folder yang sudah ada, susun ulang instruksi dengan gunakan folder lama." });
        if (workflow.Steps.Any(s => s.Agent == "drive" && (s.Action is not ("create_folder" or "use_existing_folder" or "list_folder_files") || (s.Action != "create_folder" && string.IsNullOrEmpty(s.FolderId)))))
            return Results.BadRequest(new { error = "Pilih folder untuk setiap langkah Drive yang memakai folder lama. Rencana lama tanpa jenis tindakan harus disusun ulang." });
        workflow.Status = "queued";
        try {
            if (!database.QueueWorkflow(workflow)) { workflow.Status = "planned"; return FeatureAccess.UpgradeRequired(); }
        } catch { workflow.Status = "planned"; throw; }
        store.Queue.Writer.TryWrite(id);
    }
    return Results.Accepted($"/api/workflows/{id}", workflow.Snapshot());
});
app.MapFallbackToFile("index.html");
app.Run();

record PlanRequest(string Prompt, string Mode);
record ExecuteRequest(bool ConfirmNewFolder = false);
sealed class Step(string agent, string title, string detail) {
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Agent { get; } = agent;
    public string Title { get; } = title;
    public string Detail { get; } = detail;
    public string Status { get; set; } = "pending";
    public string? Result { get; set; }
    public string Name { get; init; } = "";
    public string Action { get; init; } = "";
    public string FolderId { get; set; } = "";
    public string FolderName { get; set; } = "";
    public string Start { get; init; } = "";
    public string End { get; init; } = "";
    public bool IncludeLinks { get; init; }
    public bool CreateMeet { get; init; }
    public string? MeetUrl { get; set; }
    public string? ResultUrl { get; set; }
}
sealed class Workflow(string prompt, string mode, string source, List<Step> steps) {
    public object Sync { get; } = new();
    public string? Owner { get; set; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Prompt { get; } = prompt;
    public string Mode { get; } = mode;
    public string Source { get; } = source;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "planned";
    public List<Step> Steps { get; } = steps;
    public object Snapshot() { lock (Sync) return new { Id, Prompt, Mode, Source, CreatedAt, Status,
        Steps = Steps.Select(s => new { s.Id, s.Agent, s.Title, s.Detail, s.Status, s.Result, s.Name, s.Action, s.FolderId, s.FolderName, s.Start, s.End, s.IncludeLinks, s.CreateMeet, s.MeetUrl, s.ResultUrl }).ToArray() }; }
}
sealed class WorkflowStore(OrbitDatabase? database = null) {
    public void Persist(Workflow workflow) => database?.SaveWorkflow(workflow);
    public void Restore() {
        foreach (var workflow in database?.LoadWorkflows() ?? []) {
            if (workflow.Status == "running") {
                workflow.Status = "failed";
                foreach (var step in workflow.Steps) {
                    if (step.Status == "running") { step.Status = "failed"; step.Result = "Server berhenti saat pekerjaan berjalan. Periksa hasil di layanan sebelum membuat rencana baru."; }
                    else if (step.Status == "pending") step.Status = "skipped";
                }
                Persist(workflow);
            }
            Items[workflow.Id] = workflow;
            if (workflow.Status == "queued") Queue.Writer.TryWrite(workflow.Id);
        }
    }
    public ConcurrentDictionary<Guid, Workflow> Items { get; } = new();
    public Channel<Guid> Queue { get; } = Channel.CreateUnbounded<Guid>();
}
static class Webhook {
    public static bool IsValid(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host == "discord.com" && uri.AbsolutePath.StartsWith("/api/webhooks/");
}
sealed class WorkflowWorker(WorkflowStore store, IHttpClientFactory clients, IConfiguration config, GoogleTokenStore google, FeatureAccess access, OrbitDatabase? database = null) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        await foreach (var id in store.Queue.Reader.ReadAllAsync(stoppingToken)) {
            database?.VerifyWorkerLease();
            var workflow = store.Items[id];
            lock (workflow.Sync) { workflow.Status = "running"; store.Persist(workflow); }
            foreach (var step in workflow.Steps) {
                database?.VerifyWorkerLease();
                lock (workflow.Sync) { step.Status = "running"; store.Persist(workflow); }
                try {
                    string result;
                    if (workflow.Mode == "demo") {
                        await Task.Delay(900, stoppingToken);
                        result = "Simulasi selesai. Tidak ada data yang dibuat atau pesan yang dikirim ke layanan.";
                    } else if (step.Agent is "drive" or "calendar") {
                        if (database?.Enabled != true && access.Blocks([step], workflow.Owner)) throw new ActionFailure("Integrasi Google memerlukan Orbit Pro.");
                        var output = await GoogleActions.Execute(step, workflow.Owner, google, clients.CreateClient("external"), stoppingToken);
                        result = output.Message;
                        lock (workflow.Sync) { step.ResultUrl = output.Url; step.MeetUrl = output.MeetUrl; }
                    } else {
                        var url = database?.Enabled == true ? database.ReadConnection<string>(workflow.Owner!, "discord") : config["Discord:WebhookUrl"];
                        if (step.Agent != "discord" || !Webhook.IsValid(url)) throw new InvalidOperationException();
                        var content = GoogleActions.DiscordContent(step, workflow.Steps);
                        if (content.Length > 2000) throw new ActionFailure("Pesan beserta tautan melebihi batas Discord. Hasil Google tetap tersedia pada langkah sebelumnya.");
                        using var response = await clients.CreateClient("external").PostAsJsonAsync(url!.Split('?')[0] + "?wait=true", new {
                            content, allowed_mentions = new { parse = Array.Empty<string>() }
                        }, stoppingToken);
                        response.EnsureSuccessStatusCode();
                        result = "Discord mengonfirmasi pesan berhasil dibuat.";
                    }
                    lock (workflow.Sync) { step.Status = "completed"; step.Result = result; store.Persist(workflow); }
                } catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException or ActionFailure or JsonException or KeyNotFoundException) {
                    lock (workflow.Sync) {
                        step.Status = "failed";
                        if (ex is ActionFailure action) step.ResultUrl = action.ResultUrl;
                        step.Result = ex is ActionFailure ? ex.Message : "Tidak dapat mengonfirmasi hasil. Periksa layanan sebelum mencoba ulang agar tidak membuat pesan ganda.";
                        foreach (var remaining in workflow.Steps.Where(s => s.Status == "pending")) remaining.Status = "skipped";
                        workflow.Status = "failed";
                        store.Persist(workflow);
                    }
                    break;
                }
            }
            lock (workflow.Sync) if (workflow.Status != "failed") { workflow.Status = "completed"; store.Persist(workflow); }
        }
    }
}
static class Planner {
    public static async Task<List<Step>> Create(string prompt, IConfiguration config, HttpClient client, CancellationToken ct) {
        var settings = AiSettings.Read(config); if (settings.Provider == "Gemini") return await GeminiPlanner.Create(prompt, settings, client, ct); using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new("Bearer", config["OpenAI:ApiKey"]);
        request.Content = JsonContent.Create(new {
            model = config["OpenAI:Model"], store = false,
            instructions = PlanContract.Instructions,
            input = prompt,
            text = new { format = new { type = "json_schema", name = "workflow_plan", strict = true, schema = PlanContract.Schema } }
        });
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var text = document.RootElement.GetProperty("output").EnumerateArray()
            .Where(o => o.TryGetProperty("type", out var type) && type.GetString() == "message")
            .SelectMany(o => o.GetProperty("content").EnumerateArray())
            .First(c => c.GetProperty("type").GetString() == "output_text").GetProperty("text").GetString();
        return PlanContract.Parse(text!);
    }
}
