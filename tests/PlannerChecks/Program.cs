using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

static void Check(bool condition) { if (!condition) throw new Exception("Planner check failed."); }
IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
await WorkspaceChecks.Run();
var settings = AiSettings.Read(Config(new() { ["Gemini:ApiKey"] = "test-only-key", ["OpenAI:ApiKey"] = "old-key", ["OpenAI:Model"] = "Default" }));
Check(settings.Ready && settings.Provider == "Gemini");
Check(AiSettings.Read(Config(new() { ["AI:Provider"] = "Gemini" })).Ready == false);
Check(AiSettings.Read(Config(new() { ["AI:Provider"] = "unknown" })).Error != null);
Check(AiSettings.Read(Config(new() { ["Gemini:ApiKey"] = "test", ["Gemini:Model"] = "Default" })).Error != null);
var plan = """{"steps":[{"agent":"discord","title":"Kirim pesan","detail":"Halo tim","name":"","start":"","end":"","includeLinks":false,"createMeet":false}]}""";
var handler = new FakeHandler(HttpStatusCode.OK, JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = "stop", message = new { content = plan } } } }));
var steps = await GeminiPlanner.Create("Kirim pesan Halo tim", settings, new HttpClient(handler), default);
Check(steps.Count == 1 && steps[0].Agent == "discord");
Check(handler.Uri == "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions");
Check(handler.Auth == "Bearer test-only-key");
using var payload = JsonDocument.Parse(handler.Body!);
Check(payload.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("strict").GetBoolean());
Check(!handler.Body!.Contains("test-only-key"));
foreach (var code in new[] { 400, 401, 403, 404, 429, 500 }) {
    try { await GeminiPlanner.Create("test", settings, new HttpClient(new FakeHandler((HttpStatusCode)code, "SECRET RESPONSE")), default); throw new Exception("Expected failure"); }
    catch (PlannerException ex) { Check(!ex.Message.Contains("SECRET")); }
}

var folder = new Step("drive", "Buat folder", "Folder proyek") { Name = "user testing", Action = "create_folder" };
var calendar = new Step("calendar", "Buat meeting", "Diskusi") { Name = "Diskusi proyek", Start = "2026-10-08T15:00:00+07:00", End = "2026-10-08T16:00:00+07:00" };
var discord = new Step("discord", "Bagikan link", "Halo tim") { IncludeLinks = true };
Check(PlanContract.Validate([folder, calendar, discord]) is null);
Check(PlanContract.Validate([discord, folder]) is not null);
Check(PlanContract.Validate([new("drive", "Folder", "Buat folder")]) is not null);
Check(PlanContract.Validate([new("calendar", "Acara", "Meeting") { Name = "Diskusi", Start = "2026-10-08T15:00:00", End = calendar.End }]) is not null);
Check(PlanContract.Validate([new("calendar", "Acara", "Meeting") { Name = "Diskusi", Start = calendar.End, End = calendar.Start }]) is not null);
var tokens = new GoogleTokenStore();
tokens.Items["owner"] = new("fake-access", null, DateTimeOffset.UtcNow.AddMinutes(20), [GoogleConnection.DriveScope, GoogleConnection.CalendarScope]);
var driveHandler = new FakeHandler(HttpStatusCode.OK, "{\"id\":\"folder123\"}");
var created = await GoogleActions.Execute(folder, "owner", tokens, new HttpClient(driveHandler), default);
Check(created.Url == "https://drive.google.com/drive/folders/folder123");
using (var body = JsonDocument.Parse(driveHandler.Body!)) {
    Check(body.RootElement.GetProperty("name").GetString() == "user testing");
    Check(body.RootElement.GetProperty("mimeType").GetString() == "application/vnd.google-apps.folder");
}
Check(driveHandler.Auth == "Bearer fake-access" && !driveHandler.Body!.Contains("fake-access"));
var calendarHandler = new FakeHandler(HttpStatusCode.OK, "{\"id\":\"event123\",\"htmlLink\":\"https://www.google.com/calendar/event?eid=123\"}");
await GoogleActions.Execute(calendar, "owner", tokens, new HttpClient(calendarHandler), default);
using (var body = JsonDocument.Parse(calendarHandler.Body!)) {
    Check(body.RootElement.GetProperty("start").GetProperty("dateTime").GetString() == calendar.Start);
    Check(body.RootElement.GetProperty("end").GetProperty("dateTime").GetString() == calendar.End);
    Check(!body.RootElement.TryGetProperty("attendees", out _));
}
Check(calendarHandler.Uri!.EndsWith("/primary/events?sendUpdates=none"));
foreach (var owner in new string?[] { null, "someone-else", "expired", "missing-scope" }) {
    tokens.Items["expired"] = new("fake", null, DateTimeOffset.UtcNow.AddMinutes(-1), [GoogleConnection.DriveScope]);
    tokens.Items["missing-scope"] = new("fake", null, DateTimeOffset.UtcNow.AddMinutes(20), []);
    var blocked = new FakeHandler(HttpStatusCode.OK, "{}");
    try { await GoogleActions.Execute(folder, owner, tokens, new HttpClient(blocked), default); throw new Exception("Expected token rejection"); }
    catch (ActionFailure) { Check(blocked.Uri is null); }
}
foreach (var code in new[] { 401, 403, 429, 500 }) {
    try { await GoogleActions.Execute(folder, "owner", tokens, new HttpClient(new FakeHandler((HttpStatusCode)code, "SECRET")), default); throw new Exception("Expected API rejection"); }
    catch (ActionFailure ex) { Check(!ex.Message.Contains("SECRET")); }
}
// Exercise the actual queued worker across Drive -> Calendar -> Discord using mock HTTP only.
var pro = new FeatureAccess(Config(new() { ["Billing:DevelopmentPro"] = "true" }), new TestEnvironment());
var free = new FeatureAccess(Config(new()), new TestEnvironment());
Check(free.Blocks([folder]) && !free.Blocks([new("discord", "Message", "Hello")]));
Check(!pro.Blocks([folder]));
Check(new FeatureAccess(Config(new() { ["Billing:DevelopmentPro"] = "true" }), new TestEnvironment { EnvironmentName = "Production" }).Blocks([folder]));
var store = new WorkflowStore();
var workflow = new Workflow("Buat folder, meeting dan kirim tautan", "live", "ai", [folder, calendar, discord]) { Owner = "owner" };
store.Items[workflow.Id] = workflow;
var routing = new RoutingHandler(false);
using (var worker = new WorkflowWorker(store, new FakeFactory(routing), Config(new() { ["Discord:WebhookUrl"] = "https://discord.com/api/webhooks/test/test" }), tokens, pro)) {
    await worker.StartAsync(default);
    store.Queue.Writer.TryWrite(workflow.Id);
    await WaitDone(workflow);
    await worker.StopAsync(default);
}
Check(workflow.Status == "completed" && routing.Requests == 3);
Check(routing.DiscordBody!.Contains("https://drive.google.com/drive/folders/folder123"));
Check(routing.DiscordBody.Contains("https://www.google.com/calendar/event?eid=123"));
using (var sent = JsonDocument.Parse(routing.DiscordBody)) Check(sent.RootElement.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength() == 0);
var failed = new Workflow("Failure test", "live", "ai", [new("drive", "Folder", "Folder") { Name = "Test", Action = "create_folder" }, new("discord", "Message", "Hello")]) { Owner = "owner" };
store.Items[failed.Id] = failed;
var rejected = new RoutingHandler(true);
using (var worker = new WorkflowWorker(store, new FakeFactory(rejected), Config(new()), tokens, pro)) {
    await worker.StartAsync(default); store.Queue.Writer.TryWrite(failed.Id); await WaitDone(failed); await worker.StopAsync(default);
}
Check(failed.Status == "failed" && failed.Steps[1].Status == "skipped" && rejected.Requests == 1);

var meet = new Step("calendar", "Meeting", "Buat Meet") { Name = "Diskusi", Start = calendar.Start, End = calendar.End, CreateMeet = true };
Check(PlanContract.Validate([new("discord", "Test", "Test") { CreateMeet = true }]) is not null);
var meetHandler = new MeetHandler(false);
var meetResult = await GoogleActions.Execute(meet, "owner", tokens, new HttpClient(meetHandler), default);
Check(meetResult.MeetUrl == "https://meet.google.com/abc-defg-hij" && meetHandler.Requests == 2);
using (var body = JsonDocument.Parse(meetHandler.CreateBody!)) {
    Check(body.RootElement.GetProperty("conferenceData").GetProperty("createRequest").GetProperty("conferenceSolutionKey").GetProperty("type").GetString() == "hangoutsMeet");
}
Check(meetHandler.CreateUri!.Contains("conferenceDataVersion=1"));
meet.Status = "completed"; meet.ResultUrl = meetResult.Url; meet.MeetUrl = meetResult.MeetUrl;
Check(GoogleActions.DiscordContent(discord, [meet]).Contains(meet.MeetUrl!));
try { await GoogleActions.Execute(meet, "owner", tokens, new HttpClient(new MeetHandler(true)), default); throw new Exception("Expected Meet failure"); }
catch (ActionFailure ex) { Check(ex.ResultUrl == "https://www.google.com/calendar/event?eid=123"); }
var deniedWorkflow = new Workflow("Google without Pro", "live", "ai", [new("drive", "Folder", "Folder") { Name = "Test", Action = "create_folder" }]) { Owner = "owner" };
store.Items[deniedWorkflow.Id] = deniedWorkflow;
var deniedHttp = new RoutingHandler(false);
using (var worker = new WorkflowWorker(store, new FakeFactory(deniedHttp), Config(new()), tokens, free)) {
    await worker.StartAsync(default); store.Queue.Writer.TryWrite(deniedWorkflow.Id); await WaitDone(deniedWorkflow); await worker.StopAsync(default);
}
Check(deniedWorkflow.Status == "failed" && deniedHttp.Requests == 0);
var manual = new FeatureAccess(Config(new()), new TestEnvironment { EnvironmentName = "Production" });
Check(!manual.Pro); manual.ActivateSimulation(); manual.ActivateSimulation();
Check(manual.Pro && manual.SimulationActive && !manual.Blocks([folder, calendar]));
var persisted = new Workflow("Persist test", "live", "ai", [folder, calendar]) { Owner = "google:test", Status = "queued" };
var restored = JsonSerializer.Deserialize<Workflow>(JsonSerializer.Serialize(persisted.Snapshot(), OrbitDatabase.Json), OrbitDatabase.Json)!;
Check(restored.Id == persisted.Id && restored.CreatedAt == persisted.CreatedAt && restored.Status == "queued");
Check(restored.Steps[0].Id == folder.Id && restored.Steps[0].Name == folder.Name);
Check(WorkspaceEndpoints.Valid(JsonSerializer.Deserialize<JsonElement>("{\"workspace\":\"Orbit\",\"draft\":\"\",\"projects\":[]}")));
Check(!WorkspaceEndpoints.Valid(JsonSerializer.Deserialize<JsonElement>("{\"workspace\":123,\"draft\":\"\",\"projects\":[]}")));
Console.WriteLine("PASS: manual simulated Pro activation, planner/schema, Google payloads, timestamp validation, token isolation/expiry/scopes, real-result link forwarding, queued execution, failure stop, Meet polling/link forwarding and Pro gates (mock HTTP).");
await DriveChecks.Run();
await AccountChecks.Run();
await AccountChecks.RunDatabase();
try { await DatabaseChecks.Run(); }
catch (Exception e) {
    Console.Error.WriteLine($"FAIL: database integration checks ({e.GetType().Name}).");
    if (e is Npgsql.PostgresException postgres) Console.Error.WriteLine($"PostgreSQL SQLSTATE: {postgres.SqlState}");
    if (e is Npgsql.NpgsqlException)
        Console.Error.WriteLine("Check database connectivity, credentials and the trusted SSL root certificate. Secret connection details are omitted.");
    Environment.ExitCode = 1;
}
static async Task WaitDone(Workflow w) {
    var deadline = DateTime.UtcNow.AddSeconds(5);
    while (DateTime.UtcNow < deadline) { lock(w.Sync) { if (w.Status is "completed" or "failed") return; } await Task.Delay(10); }
    throw new Exception("Worker did not finish");
}
sealed class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory { public HttpClient CreateClient(string name) => new(handler, false); }
sealed class RoutingHandler(bool fail) : HttpMessageHandler {
    public int Requests; public string? DiscordBody;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        Requests++;
        if (fail) return new(HttpStatusCode.Forbidden) { Content = new StringContent("private error") };
        string body;
        if (request.RequestUri!.Host == "discord.com") { DiscordBody = await request.Content!.ReadAsStringAsync(ct); body = "{}"; }
        else if (request.RequestUri.AbsolutePath.Contains("/drive/")) body = "{\"id\":\"folder123\"}";
        else body = "{\"id\":\"event123\",\"htmlLink\":\"https://www.google.com/calendar/event?eid=123\"}";
        return new(HttpStatusCode.OK) { Content = new StringContent(body) };
    }
}
sealed class FakeHandler(HttpStatusCode status, string response) : HttpMessageHandler {
    public string? Uri, Auth, Body;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        Uri = request.RequestUri!.ToString(); Auth = request.Headers.Authorization!.ToString(); Body = await request.Content!.ReadAsStringAsync(ct);
        return new HttpResponseMessage(status) { Content = new StringContent(response) };
    }
}

sealed class TestEnvironment : IHostEnvironment { public string EnvironmentName { get; set; } = "Development"; public string ApplicationName { get; set; } = "Tests"; public string ContentRootPath { get; set; } = "."; public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider(); }

sealed class MeetHandler(bool fail) : HttpMessageHandler {
    public int Requests; public string? CreateBody, CreateUri;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        Requests++;
        if (request.Method == HttpMethod.Post) { CreateBody = await request.Content!.ReadAsStringAsync(ct); CreateUri = request.RequestUri!.ToString(); }
        var body = JsonSerializer.Serialize(new {
            id = "event123", htmlLink = "https://www.google.com/calendar/event?eid=123",
            conferenceData = new { createRequest = new { status = new { statusCode = fail ? "failure" : Requests == 1 ? "pending" : "success" } },
                entryPoints = Requests == 1 ? Array.Empty<object>() : new object[] { new { entryPointType = "video", uri = "https://meet.google.com/abc-defg-hij" } } }
        });
        return new(HttpStatusCode.OK) { Content = new StringContent(body) };
    }
}
