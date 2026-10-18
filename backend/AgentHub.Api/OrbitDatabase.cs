using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

// A single API/worker replica owns the advisory lock. All application queries stay server-side.
sealed class OrbitDatabase : IDisposable {
    private readonly NpgsqlDataSource? source;
    private NpgsqlConnection? lease;
    private readonly byte[]? key;
    public bool Enabled => source is not null;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public OrbitDatabase(IConfiguration config) {
        var connection = config.GetConnectionString("Orbit");
        if (string.IsNullOrWhiteSpace(connection)) return;
        key = Convert.FromBase64String(config["Security:EncryptionKey"] ?? "");
        if (key.Length != 32) throw new InvalidOperationException("Security:EncryptionKey must be a stable base64-encoded 32-byte key.");
        var settings = new NpgsqlConnectionStringBuilder(connection) { MaxPoolSize = 10, Timeout = 15, CommandTimeout = 30, KeepAlive = 30, IncludeErrorDetail = false };
        source = NpgsqlDataSource.Create(settings.ConnectionString);
    }
    public async Task Initialize(CancellationToken ct = default) {
        if (source is null) return;
        lease = await source.OpenConnectionAsync(ct);
        using var claim = new NpgsqlCommand("select pg_try_advisory_lock(7418236501)", lease);
        if (!(bool)(await claim.ExecuteScalarAsync(ct))!) throw new InvalidOperationException("Orbit currently supports one backend replica. Another instance owns the database worker lock.");
        // Schema is applied explicitly by --migrate, never implicitly by a public request.
        using var version = source.CreateCommand("select version from orbit.schema_versions where version=1");
        if (await version.ExecuteScalarAsync(ct) is null) throw new InvalidOperationException("Apply the Orbit database migration first.");
    }
    public async Task Migrate(CancellationToken ct = default) {
        if (source is null) throw new InvalidOperationException("ConnectionStrings:Orbit is missing.");
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Database", "001_initial.sql"), ct);
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }
    private NpgsqlCommand Command(string sql, params (string, object?)[] parameters) {
        var command = source!.CreateCommand(sql);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }
    public void EnsureUser(string owner) {
        if (!Enabled) return;
        using var cmd = Command("insert into orbit.users(id) values (@owner) on conflict do nothing; insert into orbit.workspaces(owner_id) values (@owner) on conflict do nothing", ("owner", owner));
        cmd.ExecuteNonQuery();
    }
    public List<Workflow> LoadWorkflows() {
        var items = new List<Workflow>();
        if (!Enabled) return items;
        using var cmd = Command("select owner_id, payload::text from orbit.workflows");
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            var workflow = JsonSerializer.Deserialize<Workflow>(reader.GetString(1), Json)!;
            workflow.Owner = reader.GetString(0);
            items.Add(workflow);
        }
        return items;
    }
    public void SaveWorkflow(Workflow workflow) {
        if (!Enabled) return;
        if (workflow.Owner is null) throw new InvalidOperationException("A database workflow needs an authenticated owner.");
        using var cmd = Command("insert into orbit.workflows(id, owner_id, payload) values (@id,@owner,@payload::jsonb) on conflict(id) do update set payload=excluded.payload, updated_at=now() where orbit.workflows.owner_id=excluded.owner_id",
            ("id", workflow.Id), ("owner", workflow.Owner), ("payload", JsonSerializer.Serialize(workflow.Snapshot(), Json)));
        cmd.ExecuteNonQuery();
    }
    public void VerifyWorkerLease() {
        if (!Enabled) return;
        using var command = new NpgsqlCommand("select 1", lease!);
        command.ExecuteScalar(); // Never execute effects after losing the single-worker connection.
    }
    public bool QueueWorkflow(Workflow workflow) {
        if (source is null) return true;
        using var connection = source.OpenConnection();
        using var tx = connection.BeginTransaction();
        if (workflow.Mode == "live" && workflow.Steps.Any(s => s.Agent is "drive" or "calendar")) {
            using var reserve = new NpgsqlCommand("update orbit.workspaces set trial_workflows=trial_workflows+1 where owner_id=@owner and trial_started_at + interval '7 days'>now() and trial_workflows<10 returning trial_workflows", connection, tx);
            reserve.Parameters.AddWithValue("owner", workflow.Owner!);
            if (reserve.ExecuteScalar() is null) return false;
        }
        using var save = new NpgsqlCommand("update orbit.workflows set payload=@payload::jsonb,updated_at=now() where id=@id and owner_id=@owner and payload->>'status'='planned'", connection, tx);
        save.Parameters.AddWithValue("payload", JsonSerializer.Serialize(workflow.Snapshot(), Json));
        save.Parameters.AddWithValue("id", workflow.Id); save.Parameters.AddWithValue("owner", workflow.Owner!);
        if (save.ExecuteNonQuery() != 1) return false;
        tx.Commit(); return true;
    }
    public (JsonElement State, long Revision) ReadWorkspace(string owner) {
        using var cmd = Command("select state::text, revision from orbit.workspaces where owner_id=@owner", ("owner", owner));
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) throw new InvalidOperationException("Missing workspace.");
        return (JsonSerializer.Deserialize<JsonElement>(reader.GetString(0)), reader.GetInt64(1));
    }
    public long? SaveWorkspace(string owner, long revision, JsonElement state) {
        using var cmd = Command("update orbit.workspaces set state=@state::jsonb, revision=revision+1, updated_at=now() where owner_id=@owner and revision=@revision returning revision",
            ("state", state.GetRawText()), ("owner", owner), ("revision", revision));
        return cmd.ExecuteScalar() is long next ? next : null;
    }
    public void SaveConnection<T>(string owner, string service, T value) {
        if (!Enabled) return;
        using var cmd = Command("insert into orbit.connections(owner_id,service,encrypted_payload) values (@owner,@service,@payload) on conflict(owner_id,service) do update set encrypted_payload=excluded.encrypted_payload, updated_at=now()",
            ("owner", owner), ("service", service), ("payload", Encrypt(JsonSerializer.Serialize(value, Json), owner + ":" + service)));
        cmd.ExecuteNonQuery();
    }
    public T? ReadConnection<T>(string owner, string service) {
        if (!Enabled) return default;
        using var cmd = Command("select encrypted_payload from orbit.connections where owner_id=@owner and service=@service", ("owner", owner), ("service", service));
        var value = cmd.ExecuteScalar() as string;
        return value is null ? default : JsonSerializer.Deserialize<T>(Decrypt(value, owner + ":" + service), Json);
    }
    public bool ReservePlan(string owner) {
        using var cmd = Command("insert into orbit.monthly_usage(owner_id,month,plans) values (@owner,date_trunc('month',now() at time zone 'UTC')::date,1) on conflict(owner_id,month) do update set plans=orbit.monthly_usage.plans+1 where orbit.monthly_usage.plans<20 returning plans", ("owner", owner));
        return cmd.ExecuteScalar() is not null;
    }
    public bool StartTrial(string owner) {
        using var cmd = Command("update orbit.workspaces set trial_started_at=now() where owner_id=@owner and trial_started_at is null returning owner_id", ("owner", owner));
        return cmd.ExecuteScalar() is not null;
    }
    public bool TrialActive(string owner) {
        using var cmd = Command("select coalesce(trial_started_at + interval '7 days' > now() and trial_workflows<10,false) from orbit.workspaces where owner_id=@owner", ("owner", owner));
        return cmd.ExecuteScalar() is true;
    }
    public bool ReserveTrialWorkflow(string owner) {
        using var cmd = Command("update orbit.workspaces set trial_workflows=trial_workflows+1 where owner_id=@owner and trial_started_at + interval '7 days'>now() and trial_workflows<10 returning trial_workflows", ("owner", owner));
        return cmd.ExecuteScalar() is not null;
    }
    public object Billing(string owner) {
        using var cmd = Command("select trial_started_at,trial_workflows,coalesce((select plans from orbit.monthly_usage where owner_id=@owner and month=date_trunc('month',now() at time zone 'UTC')::date),0) from orbit.workspaces where owner_id=@owner", ("owner", owner));
        using var reader = cmd.ExecuteReader(); reader.Read();
        var started = reader.IsDBNull(0) ? (DateTime?)null : reader.GetDateTime(0);
        var used = reader.GetInt32(1);
        var active = started is not null && started.Value.AddDays(7) > DateTime.UtcNow && used < 10;
        return new { plan = active ? "pro" : "free", trialAccess = active, trialAvailable = started is null, trialEndsAt = started?.AddDays(7), trialRemaining = Math.Max(0,10-used), plansRemaining = Math.Max(0,20-reader.GetInt32(2)), developmentAccess = false, simulationAccess = false, checkoutEnabled = false, monthlyPrice = 29000, currency = "IDR" };
    }
    private string Encrypt(string text, string purpose) {
        var nonce = RandomNumberGenerator.GetBytes(12); var data = Encoding.UTF8.GetBytes(text); var cipher = new byte[data.Length]; var tag = new byte[16];
        using var aes = new AesGcm(key!,16); aes.Encrypt(nonce,data,cipher,tag,Encoding.UTF8.GetBytes(purpose));
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }
    private string Decrypt(string text, string purpose) {
        var bytes = Convert.FromBase64String(text); var plain = new byte[bytes.Length-28];
        using var aes = new AesGcm(key!,16); aes.Decrypt(bytes.AsSpan(0,12),bytes.AsSpan(28),bytes.AsSpan(12,16),plain,Encoding.UTF8.GetBytes(purpose));
        return Encoding.UTF8.GetString(plain);
    }
    public void Dispose() { lease?.Dispose(); source?.Dispose(); }
}
