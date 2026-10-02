using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddHttpClient("external", client => client.Timeout = TimeSpan.FromSeconds(45));
builder.Services.AddSingleton<WorkflowStore>();
builder.Services.AddHostedService<WorkflowWorker>();
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/connections", (IConfiguration config) => new {
    ai = !string.IsNullOrWhiteSpace(config["OpenAI:ApiKey"]) && !string.IsNullOrWhiteSpace(config["OpenAI:Model"]),
    services = new[] {
        new { id = "drive", name = "Google Drive", connected = false, detail = "OAuth belum diimplementasikan" },
        new { id = "calendar", name = "Google Calendar", connected = false, detail = "OAuth belum diimplementasikan" },
        new { id = "discord", name = "Discord", connected = Webhook.IsValid(config["Discord:WebhookUrl"]), detail = "Webhook dikonfigurasi oleh pemilik server" }
    }
});
app.MapGet("/api/workflows", (WorkflowStore store) => store.Items.Values.OrderByDescending(w => w.CreatedAt).Select(w => w.Snapshot()));
app.MapGet("/api/workflows/{id:guid}", (Guid id, WorkflowStore store) =>
    store.Items.TryGetValue(id, out var w) ? Results.Ok(w.Snapshot()) : Results.NotFound());

app.MapPost("/api/workflows/plan", async (PlanRequest request, WorkflowStore store, IConfiguration config, IHttpClientFactory clients, CancellationToken ct) => {
    if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 2000)
        return Results.BadRequest(new { error = "Isi instruksi antara 1–2.000 karakter." });
    if (request.Mode is not ("demo" or "live")) return Results.BadRequest(new { error = "Mode tidak valid." });
    var aiEnabled = !string.IsNullOrWhiteSpace(config["OpenAI:ApiKey"]) && !string.IsNullOrWhiteSpace(config["OpenAI:Model"]);
    List<Step> steps;
    var source = "template";
    if (aiEnabled) {
        try {
            steps = await Planner.Create(request.Prompt, config, clients.CreateClient("external"), ct);
            source = "ai";
        } catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException) {
            return Results.Problem(statusCode: 502, title: "Planner AI belum berhasil merespons. Periksa konfigurasi atau coba lagi.");
        }
    } else {
        if (request.Mode == "live") return Results.BadRequest(new { error = "Mode live memerlukan konfigurasi OpenAI. Mode demo memakai template contoh." });
        steps = [
            new("drive", "Buat folder proyek", "Contoh: folder Proyek AI Kelompok untuk menyimpan hasil kerja."),
            new("calendar", "Jadwalkan diskusi kelompok", "Contoh: jadwal belum ditetapkan. Mode demo tidak membuat event."),
            new("discord", "Bagikan rangkuman", "Contoh: kirim rangkuman persiapan proyek ke channel kelompok.")
        ];
    }
    if (steps.Count is < 1 or > 5 || steps.Any(s => s.Agent is not ("drive" or "calendar" or "discord") || string.IsNullOrWhiteSpace(s.Title) || string.IsNullOrWhiteSpace(s.Detail)))
        return Results.BadRequest(new { error = "Rencana tidak valid. Coba instruksi yang lebih spesifik." });
    if (request.Mode == "live" && (steps.Any(s => s.Agent != "discord") || !Webhook.IsValid(config["Discord:WebhookUrl"])))
        return Results.BadRequest(new { error = "Live saat ini mendukung pengiriman pesan Discord saja, setelah webhook dikonfigurasi. Gunakan mode demo untuk Drive dan Calendar." });
    var workflow = new Workflow(request.Prompt.Trim(), request.Mode, source, steps);
    store.Items[workflow.Id] = workflow;
    return Results.Ok(workflow.Snapshot());
});
app.MapPost("/api/workflows/{id:guid}/execute", (Guid id, WorkflowStore store) => {
    if (!store.Items.TryGetValue(id, out var workflow)) return Results.NotFound();
    lock (workflow.Sync) {
        if (workflow.Status != "planned") return Results.Conflict(new { error = "Rencana ini sudah dijalankan. Lihat status pekerjaan." });
        workflow.Status = "queued";
        store.Queue.Writer.TryWrite(id);
    }
    return Results.Accepted($"/api/workflows/{id}", workflow.Snapshot());
});
app.MapFallbackToFile("index.html");
app.Run();

record PlanRequest(string Prompt, string Mode);
sealed class Step(string agent, string title, string detail) {
    public Guid Id { get; } = Guid.NewGuid();
    public string Agent { get; } = agent;
    public string Title { get; } = title;
    public string Detail { get; } = detail;
    public string Status { get; set; } = "pending";
    public string? Result { get; set; }
}
sealed class Workflow(string prompt, string mode, string source, List<Step> steps) {
    public object Sync { get; } = new();
    public Guid Id { get; } = Guid.NewGuid();
    public string Prompt { get; } = prompt;
    public string Mode { get; } = mode;
    public string Source { get; } = source;
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "planned";
    public List<Step> Steps { get; } = steps;
    public object Snapshot() { lock (Sync) return new { Id, Prompt, Mode, Source, CreatedAt, Status,
        Steps = Steps.Select(s => new { s.Id, s.Agent, s.Title, s.Detail, s.Status, s.Result }).ToArray() }; }
}
sealed class WorkflowStore {
    public ConcurrentDictionary<Guid, Workflow> Items { get; } = new();
    public Channel<Guid> Queue { get; } = Channel.CreateUnbounded<Guid>();
}
static class Webhook {
    public static bool IsValid(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host == "discord.com" && uri.AbsolutePath.StartsWith("/api/webhooks/");
}
sealed class WorkflowWorker(WorkflowStore store, IHttpClientFactory clients, IConfiguration config) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        await foreach (var id in store.Queue.Reader.ReadAllAsync(stoppingToken)) {
            var workflow = store.Items[id];
            lock (workflow.Sync) workflow.Status = "running";
            foreach (var step in workflow.Steps) {
                lock (workflow.Sync) step.Status = "running";
                try {
                    string result;
                    if (workflow.Mode == "demo") {
                        await Task.Delay(900, stoppingToken);
                        result = "Simulasi selesai. Tidak ada data yang dibuat atau pesan yang dikirim ke layanan.";
                    } else {
                        var url = config["Discord:WebhookUrl"];
                        if (step.Agent != "discord" || !Webhook.IsValid(url)) throw new InvalidOperationException();
                        if (step.Detail.Length > 2000) throw new InvalidOperationException();
                        using var response = await clients.CreateClient("external").PostAsJsonAsync(url!.Split('?')[0] + "?wait=true", new {
                            content = step.Detail, allowed_mentions = new { parse = Array.Empty<string>() }
                        }, stoppingToken);
                        response.EnsureSuccessStatusCode();
                        result = "Discord mengonfirmasi pesan berhasil dibuat.";
                    }
                    lock (workflow.Sync) { step.Status = "completed"; step.Result = result; }
                } catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException) {
                    lock (workflow.Sync) {
                        step.Status = "failed";
                        step.Result = "Tidak dapat mengonfirmasi hasil. Periksa layanan sebelum mencoba ulang agar tidak membuat pesan ganda.";
                        foreach (var remaining in workflow.Steps.Where(s => s.Status == "pending")) remaining.Status = "skipped";
                        workflow.Status = "failed";
                    }
                    break;
                }
            }
            lock (workflow.Sync) if (workflow.Status != "failed") workflow.Status = "completed";
        }
    }
}
static class Planner {
    public static async Task<List<Step>> Create(string prompt, IConfiguration config, HttpClient client, CancellationToken ct) {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new("Bearer", config["OpenAI:ApiKey"]);
        request.Content = JsonContent.Create(new {
            model = config["OpenAI:Model"], store = false,
            instructions = "Susun rencana singkat bahasa Indonesia sesuai instruksi pengguna. Hanya tindakan drive (buat folder), calendar (buat event), discord (kirim pesan). Maksimal 5 langkah. Jangan menambahkan tindakan yang tidak diminta. Detail discord harus berisi teks pesan lengkap yang akan dikirim. Jangan mengklaim tindakan sudah dilakukan. Jangan mengarang tanggal, tautan, atau ID hasil. Jika informasi kurang, jelaskan dalam detail. Untuk permintaan tidak didukung, kembalikan steps kosong.",
            input = prompt,
            text = new { format = new { type = "json_schema", name = "workflow_plan", strict = true, schema = new {
                type = "object", properties = new { steps = new { type = "array", items = new {
                    type = "object", properties = new {
                        agent = new { type = "string", @enum = new[] { "drive", "calendar", "discord" } },
                        title = new { type = "string" }, detail = new { type = "string" }
                    }, required = new[] { "agent", "title", "detail" }, additionalProperties = false
                } } }, required = new[] { "steps" }, additionalProperties = false
            } } }
        });
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var text = document.RootElement.GetProperty("output").EnumerateArray()
            .Where(o => o.TryGetProperty("type", out var type) && type.GetString() == "message")
            .SelectMany(o => o.GetProperty("content").EnumerateArray())
            .First(c => c.GetProperty("type").GetString() == "output_text").GetProperty("text").GetString();
        using var plan = JsonDocument.Parse(text!);
        return plan.RootElement.GetProperty("steps").EnumerateArray().Select(s => new Step(
            s.GetProperty("agent").GetString()!, s.GetProperty("title").GetString()!, s.GetProperty("detail").GetString()!)).ToList();
    }
}
