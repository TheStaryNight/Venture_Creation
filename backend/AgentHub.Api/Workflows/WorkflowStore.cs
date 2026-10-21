using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Text.Json;

sealed class WorkflowStore(OrbitDatabase? database = null, IConfiguration? config = null)
{
    private string? LocalDirectory => database?.Enabled == true ? null : config?["Local:WorkflowDirectory"];
    public void Persist(Workflow workflow)
    {
        if (database?.Enabled == true) { database.SaveWorkflow(workflow); return; }
        if (string.IsNullOrWhiteSpace(LocalDirectory)) return;
        Directory.CreateDirectory(LocalDirectory);
        var path = Path.Combine(LocalDirectory, workflow.Id + ".json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(workflow, OrbitDatabase.Json));
        File.Move(path + ".tmp", path, overwrite: true);
    }
    public bool Delete(Guid id, string? owner)
    {
        if (!Items.TryGetValue(id, out var workflow) || workflow.Owner != owner) return false;
        lock (workflow.Sync)
        {
            if (workflow.Status is "queued" or "running")
                throw new ActionFailure("Percakapan masih berjalan. Tunggu sampai selesai sebelum menghapusnya.");
            if (database?.Enabled == true) database.DeleteWorkflow(owner!, id);
            else if (!string.IsNullOrWhiteSpace(LocalDirectory)) File.Delete(Path.Combine(LocalDirectory, id + ".json"));
            workflow.Status = "deleted";
            Items.TryRemove(id, out _);
            return true;
        }
    }
    private IEnumerable<Workflow> Load()
    {
        if (database?.Enabled == true) return database.LoadWorkflows();
        if (string.IsNullOrWhiteSpace(LocalDirectory) || !Directory.Exists(LocalDirectory)) return [];
        return Directory.GetFiles(LocalDirectory, "*.json")
            .Select(path => JsonSerializer.Deserialize<Workflow>(File.ReadAllText(path), OrbitDatabase.Json)!);
    }
    public void Restore()
    {
        foreach (var workflow in Load())
        {
            if (workflow.Status == "running")
            {
                workflow.Status = "failed";
                foreach (var step in workflow.Steps)
                {
                    if (step.Status == "running")
                    {
                        step.Status = "failed";
                        step.Result = "Server berhenti saat pekerjaan berjalan. Periksa hasil di layanan sebelum membuat rencana baru.";
                    }
                    else if (step.Status == "pending")
                        step.Status = "skipped";
                }
                Persist(workflow);
            }
            Items[workflow.Id] = workflow;
            if (workflow.Status == "queued")
                Queue.Writer.TryWrite(workflow.Id);
        }
    }
    public ConcurrentDictionary<Guid, Workflow> Items { get; } = new();
    public Channel<Guid> Queue { get; } = Channel.CreateUnbounded<Guid>();
}
