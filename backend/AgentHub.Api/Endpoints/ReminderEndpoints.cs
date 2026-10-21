static class ReminderEndpoints
{
    public static void MapReminderEndpoints(this WebApplication app, OrbitDatabase database)
    {
        app.MapGet("/api/reminders", (HttpContext c) => database.Reminders(AccountFeatures.Owner(c)));
        app.MapPost("/api/reminders/{id:guid}/cancel", (Guid id, HttpContext c) => database.CancelReminder(AccountFeatures.Owner(c), id) ? Results.Ok(new { cancelled = true }) : Results.Conflict(new { error = "Reminder tidak tersedia atau sudah diproses." }));

    }
}
