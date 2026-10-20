using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

static class AccountChecks {
    public static async Task Run() {
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        var account = new AccountData();
        var a = new Contact(Guid.NewGuid(), "Alex", "alex@example.com");
        var b = new Contact(Guid.NewGuid(), "Alex", "other@example.com");
        account.Contacts.AddRange([a, b]);
        account.Groups.Add(new(Guid.NewGuid(), "Grup A", [a.Id, b.Id]));
        var step = new Step("calendar", "Meeting", "Diskusi") { Participants = ["Grup A", "alex@example.com"], ReminderMinutes = 30 };
        AccountFeatures.Resolve(step, account, "Meeting Grup A invite alex@example.com");
        Check(step.Attendees.Length == 2 && step.RecipientIssues.Length == 0, "Group recipients should deduplicate explicit emails");
        account.Contacts.Clear();
        Check(step.Attendees.Length == 2, "Snapshot must not follow group edits");
        account.Contacts.AddRange([a, b]);
        var ambiguous = new Step("calendar", "Meeting", "Diskusi") { Participants = ["Alex", "Missing", "invented@example.com"] };
        AccountFeatures.Resolve(ambiguous, account, "Meeting Alex dan Missing");
        Check(ambiguous.Attendees.Length == 0 && ambiguous.RecipientIssues.Length == 3 && ambiguous.RecipientIssues[0].Choices.Length == 2, "Ambiguous and fabricated recipients must never resolve automatically");
        account.Groups.Add(new(Guid.NewGuid(), "Empty", []));
        var empty = new Step("calendar", "Meeting", "Diskusi") { Participants = ["Empty"] };
        AccountFeatures.Resolve(empty, account, "Meeting Empty");
        Check(empty.RecipientIssues.Length == 1, "Empty groups need clarification");
        var json = """{"clarification":"","steps":[{"agent":"calendar","title":"Meeting","detail":"Diskusi","name":"Meeting","action":"","start":"2030-10-21T07:00:00+07:00","end":"2030-10-21T09:00:00+07:00","includeLinks":false,"createMeet":true,"participants":["Grup A"],"reminderMinutes":30}]}""";
        var parsed = PlanContract.Parse(json);
        Check(PlanContract.Validate(parsed) is null && parsed[0].ReminderMinutes == 30, "Meeting plan schema");
        Check(PlanContract.Validate([new("discord", "No", "No") { ReminderMinutes = 30 }]) is not null, "Reminders need a meeting");
        try { PlanContract.Parse("""{"clarification":"Jam berapa meeting selesai?","steps":[]}"""); throw new Exception("Expected clarification"); }
        catch (PlannerException ex) { Check(ex.Message == "Jam berapa meeting selesai?", "Clarification should reach user"); }
        using var db = new OrbitDatabase(new ConfigurationBuilder().Build());
        db.UpdateAccount("a", v => { v.Contacts.Add(a); v.Preferences = new("en", "dark", "Asia/Jakarta"); });
        Check(db.ReadAccount("b").Contacts.Count == 0 && db.ReadAccount("a").Preferences.Language == "en", "Account settings and contacts are private");
        var copy = db.ReadAccount("a"); copy.Contacts.Clear();
        Check(db.ReadAccount("a").Contacts.Count == 1, "Reads must not mutate account data");
        var tokens = new GoogleTokenStore();
        tokens.Save("test", new("access", null, DateTimeOffset.UtcNow.AddHours(1), [GoogleConnection.CalendarScope]));
        var handler = new InvitationHandler();
        var meeting = new Step("calendar", "Meeting", "Diskusi") { Name = "Review", Start = "2030-10-21T07:00:00+07:00", End = "2030-10-21T09:00:00+07:00", Attendees = ["alex@example.com"] };
        await GoogleActions.Execute(meeting, "test", tokens, new HttpClient(handler), default);
        Check(handler.Url!.Contains("sendUpdates=all"), "Calendar must notify invited attendees");
        using var body = JsonDocument.Parse(handler.Body!);
        Check(body.RootElement.GetProperty("attendees")[0].GetProperty("email").GetString() == "alex@example.com", "Calendar attendees payload");
        var now = DateTimeOffset.UtcNow;
        var job = new ReminderDelivery(Guid.NewGuid(), "Reminder", "https://discord.com/api/webhooks/test/token", now.AddMinutes(5));
        foreach (var (code, expected) in new[] { (200, "sent"), (403, "failed"), (429, "failed"), (500, "needs_review") }) {
            var sender = new ReminderHandler(code);
            Check(await ReminderWorker.Deliver(job, new HttpClient(sender), now, default) == expected && sender.Count == 1, "Delivery result and no blind retry");
        }
        var skipped = new ReminderHandler(200);
        Check(await ReminderWorker.Deliver(job, new HttpClient(skipped), now.AddMinutes(6), default) == "missed" && skipped.Count == 0, "No reminders after meeting starts");
        Check(await ReminderWorker.Deliver(job, new HttpClient(new ReminderHandler(0)), now, default) == "needs_review", "Network uncertainty requires manual review");
        Console.WriteLine("PASS: invitation payload, private contacts/settings, group snapshots/deduplication, ambiguity, clarification, reminder expiry and uncertain delivery.");
    }
    public static async Task RunDatabase() {
        var connection = Environment.GetEnvironmentVariable("ORBIT_ACCOUNT_TEST_CONNECTION");
        if (string.IsNullOrEmpty(connection)) { Console.WriteLine("SKIP: account database checks (ORBIT_ACCOUNT_TEST_CONNECTION not set)."); return; }
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:Orbit"] = connection, ["Security:EncryptionKey"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
        }).Build();
        using var db = new OrbitDatabase(config);
        var a = "account-test:" + Guid.NewGuid(); var b = "account-test:" + Guid.NewGuid();
        static void Check(bool value) { if (!value) throw new Exception("Account database check failed"); }
        try {
            db.EnsureUser(a); db.EnsureUser(b);
            var contact = new Contact(Guid.NewGuid(), "Fixture", "fixture@example.com");
            db.UpdateAccount(a, v => { v.Contacts.Add(contact); v.Preferences = new("en", "dark", "Asia/Jakarta"); });
            Check(db.ReadAccount(b).Contacts.Count == 0);
            using var restarted = new OrbitDatabase(config);
            Check(restarted.ReadAccount(a).Contacts.Single().Email == contact.Email && restarted.ReadAccount(a).Preferences.Language == "en");
            await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Task.Run(() => db.UpdateAccount(a, v => v.Contacts.Add(new(Guid.NewGuid(), "Concurrent " + i, $"test{i}@example.com"))))));
            Check(db.ReadAccount(a).Contacts.Count == 7);
            db.SaveConnection(a, "discord", "https://discord.com/api/webhooks/test/not-a-real-token");
            var step = new Step("calendar", "Fixture meeting", "Test") { Name = "Fixture meeting", Start = DateTimeOffset.UtcNow.AddDays(2).ToString("O"), ReminderMinutes = 30, ResultUrl = "https://calendar.google.com/" };
            var w = new Workflow("Fixture", "live", "ai", [step]) { Owner = a };
            db.SaveWorkflow(w); db.ScheduleReminder(w, step); db.ScheduleReminder(w, step);
            Check(db.Reminders(a).Count == 1 && db.Reminders(b).Count == 0);
            Check(restarted.Reminders(a).Single().Status == "pending");
            Check(!db.CancelReminder(b, step.Id) && db.CancelReminder(a, step.Id) && !db.CancelReminder(a, step.Id));
            var demo = new Workflow("Fixture", "demo", "ai", [new("calendar", "Test", "Test") { ReminderMinutes = 30 }]) { Owner = a };
            db.ScheduleReminder(demo, demo.Steps[0]);
            Check(db.Reminders(a).Count == 1);
            Console.WriteLine("PASS: PostgreSQL contacts/preferences survive restart; concurrent writes, owner isolation, durable reminder deduplication/cancellation and demo isolation.");
        } finally {
            await using var conn = new Npgsql.NpgsqlConnection(connection); await conn.OpenAsync();
            using var clean = new Npgsql.NpgsqlCommand("delete from orbit.reminders where owner_id in (@a,@b); delete from orbit.workflows where owner_id in (@a,@b); delete from orbit.connections where owner_id in (@a,@b); delete from orbit.workspaces where owner_id in (@a,@b); delete from orbit.users where id in (@a,@b)", conn);
            clean.Parameters.AddWithValue("a", a); clean.Parameters.AddWithValue("b", b); await clean.ExecuteNonQueryAsync();
        }
    }
    sealed class InvitationHandler : HttpMessageHandler {
        public string? Url, Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            Url = request.RequestUri!.ToString(); Body = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent("""{"id":"event","htmlLink":"https://calendar.google.com/calendar/event?eid=test"}""") };
        }
    }
    sealed class ReminderHandler(int status) : HttpMessageHandler {
        public int Count;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            Count++; if (status == 0) throw new HttpRequestException("Uncertain");
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
        }
    }
}
