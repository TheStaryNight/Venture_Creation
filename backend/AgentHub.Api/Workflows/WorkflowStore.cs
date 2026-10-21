using System.Collections.Concurrent;
using System.Threading.Channels;

sealed class WorkflowStore(OrbitDatabase? database = null)
{
    public void Persist(Workflow workflow) => database?.SaveWorkflow(workflow);
    public void Restore()
    {
        foreach (var workflow in database?.LoadWorkflows() ?? [])
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
