using Npgsql;
using System.Text.Json;
using System.Collections.Concurrent;

sealed partial class OrbitDatabase {
    private readonly ConcurrentDictionary<string, AccountData> localAccounts = new();
    private readonly object accountLock = new();
    public AccountData ReadAccount(string owner) {
        if (!Enabled) { lock (accountLock) return JsonSerializer.Deserialize<AccountData>(JsonSerializer.Serialize(localAccounts.GetOrAdd(owner, _ => new()), Json), Json)!; }
        using var cmd = Command("select account_data::text from orbit.workspaces where owner_id=@owner", ("owner", owner));
        return JsonSerializer.Deserialize<AccountData>((string?)cmd.ExecuteScalar() ?? "{}", Json)!;
    }
    public void UpdateAccount(string owner, Action<AccountData> update) {
        if (!Enabled) { lock (accountLock) { var a = ReadAccount(owner); update(a); localAccounts[owner] = a; } return; }
        using var connection = source!.OpenConnection(); using var tx = connection.BeginTransaction();
        using var read = new NpgsqlCommand("select account_data::text from orbit.workspaces where owner_id=@owner for update", connection, tx);
        read.Parameters.AddWithValue("owner", owner);
        var account = JsonSerializer.Deserialize<AccountData>((string?)read.ExecuteScalar() ?? throw new InvalidOperationException("Missing account"), Json)!;
        update(account);
        using var write = new NpgsqlCommand("update orbit.workspaces set account_data=@data::jsonb where owner_id=@owner", connection, tx);
        write.Parameters.AddWithValue("owner", owner); write.Parameters.AddWithValue("data", JsonSerializer.Serialize(account, Json));
        write.ExecuteNonQuery(); tx.Commit();
    }
    public void ScheduleReminder(Workflow w, Step s) {
        if (!Enabled || w.Mode != "live" || s.ReminderMinutes is null) return;
        var start = DateTimeOffset.Parse(s.Start).ToUniversalTime();
        var url = ReadConnection<string>(w.Owner!, "discord");
        if (!Webhook.IsValid(url)) throw new ActionFailure("Meeting sudah dibuat, tetapi reminder gagal disimpan karena Discord belum terhubung.", s.ResultUrl);
        var content = (w.Language == "en" ? "Meeting reminder: " : "Pengingat meeting: ") + s.Name + "\n" + s.Start + "\n" + (s.MeetUrl ?? s.ResultUrl);
        using var cmd = Command("""
            insert into orbit.reminders(id,owner_id,workflow_id,title,due_at,meeting_at,content,encrypted_webhook,status)
            values(@id,@owner,@workflow,@title,@due,@meeting,@content,@webhook,'pending') on conflict(id) do nothing
            """, ("id", s.Id), ("owner", w.Owner), ("workflow", w.Id), ("title", s.Name),
            ("due", start.AddMinutes(-s.ReminderMinutes.Value)), ("meeting", start),
            ("content", content), ("webhook", Encrypt(url!, w.Owner + ":reminder")));
        cmd.ExecuteNonQuery();
    }
    public List<ReminderView> Reminders(string owner) {
        if (!Enabled) return [];
        using var cmd = Command("select id,workflow_id,title,due_at,meeting_at,status from orbit.reminders where owner_id=@owner order by due_at desc limit 200", ("owner", owner));
        using var reader = cmd.ExecuteReader(); var result = new List<ReminderView>();
        while (reader.Read()) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3), reader.GetFieldValue<DateTimeOffset>(4), reader.GetString(5)));
        return result;
    }
    public bool CancelReminder(string owner, Guid id) {
        if (!Enabled) return false;
        using var cmd = Command("update orbit.reminders set status='cancelled' where id=@id and owner_id=@owner and status='pending'", ("id", id), ("owner", owner));
        return cmd.ExecuteNonQuery() == 1;
    }
    public void RecoverReminders() {
        if (!Enabled) return;
        using var cmd = Command("update orbit.reminders set status='needs_review' where status='sending'");
        cmd.ExecuteNonQuery();
    }
    public ReminderDelivery? ClaimReminder() {
        if (!Enabled) return null;
        using (var expired = Command("update orbit.reminders set status='missed' where status='pending' and meeting_at<=now()")) expired.ExecuteNonQuery();
        using var cmd = Command("""
            update orbit.reminders set status='sending'
            where id=(select id from orbit.reminders where status='pending' and due_at<=now() and meeting_at>now()
                order by due_at limit 1 for update skip locked)
            returning id,owner_id,content,encrypted_webhook,meeting_at
            """);
        using var r = cmd.ExecuteReader();
        return r.Read() ? new(r.GetGuid(0), r.GetString(2), Decrypt(r.GetString(3), r.GetString(1) + ":reminder"), r.GetFieldValue<DateTimeOffset>(4)) : null;
    }
    public void FinishReminder(Guid id, string status) {
        using var cmd = Command("update orbit.reminders set status=@status where id=@id and status='sending'", ("id", id), ("status", status));
        cmd.ExecuteNonQuery();
    }
}
sealed record ReminderView(Guid Id, Guid WorkflowId, string Title, DateTimeOffset DueAt, DateTimeOffset MeetingAt, string Status);
sealed record ReminderDelivery(Guid Id, string Content, string Webhook, DateTimeOffset MeetingAt);

sealed class ReminderWorker(OrbitDatabase database, IHttpClientFactory clients, ILogger<ReminderWorker> logger) : BackgroundService {
    internal static async Task<string> Deliver(ReminderDelivery job, HttpClient client, DateTimeOffset now, CancellationToken ct) {
        if (job.MeetingAt <= now) return "missed";
        if (!Webhook.IsValid(job.Webhook)) return "failed";
        try {
            using var response = await client.PostAsJsonAsync(job.Webhook.Split('?')[0] + "?wait=true",
                new { content = job.Content, allowed_mentions = new { parse = Array.Empty<string>() } }, ct);
            return response.IsSuccessStatusCode ? "sent" : (int)response.StatusCode is >= 400 and < 500 ? "failed" : "needs_review";
        } catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return "needs_review"; }
    }
    protected override async Task ExecuteAsync(CancellationToken ct) {
        if (!database.Enabled) return;
        database.RecoverReminders();
        while (!ct.IsCancellationRequested) {
            try {
                database.VerifyWorkerLease();
                for (var i = 0; i < 100 && !ct.IsCancellationRequested; i++) {
                    var job = database.ClaimReminder(); if (job is null) break;
                    var status = await Deliver(job, clients.CreateClient("external"), DateTimeOffset.UtcNow, ct);
                    database.FinishReminder(job.Id, status);
                }
            } catch (NpgsqlException) { logger.LogWarning("Reminder database unavailable; polling will resume."); }
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }
}
