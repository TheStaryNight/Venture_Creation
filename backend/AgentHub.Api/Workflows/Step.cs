sealed class Step(string agent, string title, string detail)
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Agent { get; } = agent;
    public string Title { get; } = title;
    public string Detail { get; } = detail;
    public string Status { get; set; } = "pending";
    public string? Result
    { get; set; }
    public string Name { get; init; } = "";
    public string Action { get; init; } = "";
    public string FolderId { get; set; } = "";
    public string FolderName { get; set; } = "";
    public string Start { get; init; } = "";
    public string End { get; init; } = "";
    public bool IncludeLinks
    { get; init; }
    public bool CreateMeet
    { get; init; }
    public string[] Participants { get; init; } = [];
    public string[] Attendees { get; set; } = [];
    public RecipientIssue[] RecipientIssues { get; set; } = [];
    public int? ReminderMinutes
    { get; set; }
    public string? MeetUrl
    { get; set; }
    public string? ResultUrl
    { get; set; }
}
