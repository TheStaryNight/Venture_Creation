using Microsoft.Extensions.Configuration;

static class DeletionChecks
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "orbit-delete-check-" + Guid.NewGuid());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Local:WorkflowDirectory"] = directory
        }).Build();
        static void Check(bool value) { if (!value) throw new Exception("Deletion check failed."); }
        try
        {
            var store = new WorkflowStore(config: config);
            var workflow = new Workflow("Local restart fixture", "demo", "template", [new("discord", "Test", "Test")]) { Owner = "owner-a" };
            store.Items[workflow.Id] = workflow;
            store.Persist(workflow);
            var restored = new WorkflowStore(config: config);
            restored.Restore();
            Check(restored.Items[workflow.Id].Owner == "owner-a");
            Check(!restored.Delete(workflow.Id, "owner-b"));
            restored.Items[workflow.Id].Status = "queued";
            try { restored.Delete(workflow.Id, "owner-a"); throw new Exception("Queued deletion accepted."); }
            catch (ActionFailure) { }
            restored.Items[workflow.Id].Status = "completed";
            Check(restored.Delete(workflow.Id, "owner-a"));
            var afterDelete = new WorkflowStore(config: config);
            afterDelete.Restore();
            Check(!afterDelete.Items.ContainsKey(workflow.Id));
            Console.WriteLine("PASS: local workflow restart persistence, deletion persistence, owner isolation and running-work protection.");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
