sealed class Workflow(string prompt, string mode, string source, List<Step> steps)
{
    public object Sync { get; } = new();
    public string? Owner
    { get; set; }
    public string Language { get; set; } = "id";
    public string TimeZone { get; set; } = "Asia/Jakarta";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Prompt { get; } = prompt;
    public string Mode { get; } = mode;
    public string Source { get; } = source;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "planned";
    public List<Step> Steps { get; } = steps;
    public object Snapshot()
    {
        lock (Sync)
            return new
            {
                Id,
                Prompt,
                Mode,
                Source,
                CreatedAt,
                Status,
                Language,
                TimeZone,
                Steps = Steps.Select(s => new { s.Id, s.Agent, s.Title, s.Detail, s.Status, s.Result, s.Name, s.Action, s.FolderId, s.FolderName, s.Start, s.End, s.IncludeLinks, s.CreateMeet, s.MeetUrl, s.ResultUrl, s.Participants, s.Attendees, s.RecipientIssues, s.ReminderMinutes }).ToArray()
            };
    }
}
