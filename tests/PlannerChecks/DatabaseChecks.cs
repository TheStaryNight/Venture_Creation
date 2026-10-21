using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;

static class DatabaseChecks {
    // Explicit opt-in; runs only against the connection chosen by the developer.
    // Creates isolated fixtures and deletes only those fixture owners in finally.
    public static async Task Run() {
        var connection = Environment.GetEnvironmentVariable("ORBIT_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) { Console.WriteLine("SKIP: PostgreSQL integration checks (ORBIT_TEST_CONNECTION not set)."); return; }
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["ConnectionStrings:Orbit"] = connection,
            ["Security:EncryptionKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }).Build();
        using var db = new OrbitDatabase(config);
        await db.Initialize();
        var a = "test:" + Guid.NewGuid().ToString("N"); var b = "test:" + Guid.NewGuid().ToString("N");
        static void Check(bool value) { if (!value) throw new Exception("Database integration check failed."); }
        try {
            db.EnsureUser(a); db.EnsureUser(b);
            var initial = db.ReadWorkspace(a);
            var state = JsonSerializer.Deserialize<JsonElement>("{\"workspace\":\"Only A\",\"projects\":[],\"draft\":\"saved\"}");
            Check(db.SaveWorkspace(a, initial.Revision, state) == initial.Revision + 1);
            Check(db.SaveWorkspace(a, initial.Revision, state) is null);
            Check(db.ReadWorkspace(b).State.GetProperty("workspace").GetString() != "Only A");
            db.SaveConnection(a, "discord", "fixture-secret");
            Check(db.ReadConnection<string>(a,"discord") == "fixture-secret");
            Check(db.ReadConnection<string>(b,"discord") is null);
            var allowed = await Task.WhenAll(Enumerable.Range(0,30).Select(_ => Task.Run(() => db.ReservePlan(a))));
            Check(allowed.Count(x => x) == 20);
            Check(db.StartTrial(a) && !db.StartTrial(a) && db.TrialActive(a) && !db.TrialActive(b));
            var queued = new Workflow("Fixture", "live", "ai", [new("drive","Folder","Folder") { Name = "Fixture" }]) { Owner = a };
            db.SaveWorkflow(queued); queued.Status = "queued";
            Check(db.QueueWorkflow(queued) && !db.QueueWorkflow(queued));
            var slots = await Task.WhenAll(Enumerable.Range(0,20).Select(_ => Task.Run(() => db.ReserveTrialWorkflow(a))));
            Check(slots.Count(x => x) == 9 && !db.TrialActive(a));
            var restored = db.LoadWorkflows().Single(w => w.Id == queued.Id);
            Check(restored.Owner == a && restored.Status == "queued" && restored.Steps[0].Name == "Fixture");
            db.DeleteWorkflow(b, queued.Id);
            Check(db.LoadWorkflows().Any(w => w.Id == queued.Id));
            db.DeleteWorkflow(a, queued.Id);
            Check(!db.LoadWorkflows().Any(w => w.Id == queued.Id));
            using var second = new OrbitDatabase(config);
            try { await second.Initialize(); throw new Exception("Worker lock was not enforced."); }
            catch (InvalidOperationException) { }
            Console.WriteLine("PASS: PostgreSQL persistence, owner isolation, revision conflicts, atomic quotas/trial queue and exclusive worker lock.");
        } finally {
            await using var cleanup = new NpgsqlConnection(connection); await cleanup.OpenAsync();
            using var cmd = new NpgsqlCommand("delete from orbit.workflows where owner_id in (@a,@b); delete from orbit.connections where owner_id in (@a,@b); delete from orbit.monthly_usage where owner_id in (@a,@b); delete from orbit.workspaces where owner_id in (@a,@b); delete from orbit.users where id in (@a,@b)", cleanup);
            cmd.Parameters.AddWithValue("a",a); cmd.Parameters.AddWithValue("b",b); await cmd.ExecuteNonQueryAsync();
        }
    }
}
