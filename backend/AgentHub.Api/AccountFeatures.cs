using System.Security.Claims;
using System.Net.Mail;
using System.Text.Json;

sealed record AccountPreferences(string Language = "id", string Theme = "system", string TimeZone = "");
sealed record Contact(Guid Id, string Name, string Email);
sealed record ContactGroup(Guid Id, string Name, Guid[] Members);
sealed class AccountData {
    public AccountPreferences Preferences { get; set; } = new();
    public List<Contact> Contacts { get; set; } = [];
    public List<ContactGroup> Groups { get; set; } = [];
}
sealed record RecipientChoice(string Id, string Label, string[] Emails);
sealed record RecipientIssue(string Name, RecipientChoice[] Choices);
sealed record MeetingReview(Dictionary<string, string>? Choices, int? ReminderMinutes);

static class AccountFeatures {
    public static bool Email(string value) => value.Length <= 254 && MailAddress.TryCreate(value, out var mail) && mail.Address == value && value.Contains('@');
    public static string Owner(HttpContext c) => c.User.FindFirstValue("orbit_google_connection") ?? "local";
    public static void MapAccountFeatures(this WebApplication app, OrbitDatabase db) {
        app.MapGet("/api/preferences", (HttpContext c) => db.ReadAccount(Owner(c)).Preferences);
        app.MapPost("/api/preferences", (AccountPreferences input, HttpContext c) => {
            if (input.Language is not ("id" or "en") || input.Theme is not ("light" or "dark" or "system") || string.IsNullOrWhiteSpace(input.TimeZone) || input.TimeZone.Length > 100)
                return Results.BadRequest(new { error = "Pilih bahasa, tema, dan zona waktu yang tersedia." });
            try { TimeZoneInfo.FindSystemTimeZoneById(input.TimeZone); }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { return Results.BadRequest(new { error = "Zona waktu tidak dikenali." }); }
            db.UpdateAccount(Owner(c), a => a.Preferences = input);
            return Results.Ok(input);
        });
        app.MapGet("/api/contacts", (HttpContext c) => db.ReadAccount(Owner(c)).Contacts);
        app.MapGet("/api/groups", (HttpContext c) => db.ReadAccount(Owner(c)).Groups);
        app.MapPost("/api/contacts", (Contact input, HttpContext c) => {
            var name = input.Name?.Trim() ?? ""; var email = input.Email?.Trim() ?? "";
            if (name.Length is < 1 or > 100 || !Email(email)) return Results.BadRequest(new { error = "Isi nama dan alamat email yang benar." });
            var contact = input with { Id = input.Id == Guid.Empty ? Guid.NewGuid() : input.Id, Name = name, Email = email };
            try {
                db.UpdateAccount(Owner(c), a => {
                    if (input.Id != Guid.Empty && !a.Contacts.Any(x => x.Id == input.Id)) throw new ActionFailure("Kontak tidak ditemukan.");
                    if (a.Contacts.Any(x => x.Id != contact.Id && x.Email.Equals(email, StringComparison.OrdinalIgnoreCase))) throw new ActionFailure("Email ini sudah ada di kontak.");
                    if (input.Id == Guid.Empty && a.Contacts.Count >= 500) throw new ActionFailure("Maksimal 500 kontak per akun.");
                    a.Contacts.RemoveAll(x => x.Id == contact.Id); a.Contacts.Add(contact);
                });
                return Results.Ok(contact);
            } catch (ActionFailure ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
        app.MapPost("/api/contacts/{id:guid}/delete", (Guid id, HttpContext c) => {
            db.UpdateAccount(Owner(c), a => { a.Contacts.RemoveAll(x => x.Id == id); a.Groups = a.Groups.Select(g => g with { Members = g.Members.Where(m => m != id).ToArray() }).ToList(); });
            return Results.Ok(new { deleted = true });
        });
        app.MapPost("/api/groups", (ContactGroup input, HttpContext c) => {
            var name = input.Name?.Trim() ?? "";
            if (name.Length is < 1 or > 100 || input.Members is null) return Results.BadRequest(new { error = "Isi nama grup dan pilih anggotanya." });
            var group = input with { Id = input.Id == Guid.Empty ? Guid.NewGuid() : input.Id, Name = name, Members = input.Members.Distinct().ToArray() };
            try {
                db.UpdateAccount(Owner(c), a => {
                    if (input.Id != Guid.Empty && !a.Groups.Any(g => g.Id == input.Id)) throw new ActionFailure("Grup tidak ditemukan.");
                    if (a.Groups.Any(g => g.Id != group.Id && g.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new ActionFailure("Nama grup sudah dipakai.");
                    if (group.Members.Any(id => !a.Contacts.Any(x => x.Id == id))) throw new ActionFailure("Anggota grup harus berasal dari kontak akunmu.");
                    if (input.Id == Guid.Empty && a.Groups.Count >= 100) throw new ActionFailure("Maksimal 100 grup per akun.");
                    a.Groups.RemoveAll(g => g.Id == group.Id); a.Groups.Add(group);
                });
                return Results.Ok(group);
            } catch (ActionFailure ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
        app.MapPost("/api/groups/{id:guid}/delete", (Guid id, HttpContext c) => {
            db.UpdateAccount(Owner(c), a => a.Groups.RemoveAll(g => g.Id == id));
            return Results.Ok(new { deleted = true });
        });
        app.MapPost("/api/workflows/{id:guid}/steps/{stepId:guid}/meeting", (Guid id, Guid stepId, MeetingReview input, WorkflowStore store, HttpContext c) => {
            if (!store.Items.TryGetValue(id, out var w) || w.Owner != c.User.FindFirstValue("orbit_google_connection")) return Results.NotFound();
            lock (w.Sync) {
                if (w.Status != "planned") return Results.Conflict(new { error = "Meeting ini sudah disetujui." });
                var s = w.Steps.FirstOrDefault(s => s.Id == stepId && s.Agent == "calendar");
                if (s is null) return Results.NotFound();
                if (input.ReminderMinutes is < 0 or > 10080) return Results.BadRequest(new { error = "Reminder harus antara 0 dan 10.080 menit sebelum meeting." });
                var selected = new List<string>(s.Attendees);
                var remaining = new List<RecipientIssue>();
                foreach (var issue in s.RecipientIssues) {
                    if (input.Choices?.TryGetValue(issue.Name, out var choiceId) == true) {
                        var choice = issue.Choices.FirstOrDefault(x => x.Id == choiceId);
                        if (choice is null || choice.Emails.Length == 0) return Results.BadRequest(new { error = "Pilih peserta yang tersedia atau lengkapi kontak lalu susun ulang." });
                        selected.AddRange(choice.Emails);
                    } else remaining.Add(issue);
                }
                var oldEmails = s.Attendees; var oldIssues = s.RecipientIssues; var oldMinutes = s.ReminderMinutes;
                s.Attendees = selected.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (s.Attendees.Length > 200) { s.Attendees = oldEmails; return Results.BadRequest(new { error = "Maksimal 200 peserta per meeting." }); }
                s.RecipientIssues = remaining.ToArray(); s.ReminderMinutes = input.ReminderMinutes;
                try { store.Persist(w); } catch { s.Attendees = oldEmails; s.RecipientIssues = oldIssues; s.ReminderMinutes = oldMinutes; throw; }
                return Results.Ok(w.Snapshot());
            }
        });
    }
    public static void Resolve(Step step, AccountData account, string prompt) {
        var emails = new List<string>(); var issues = new List<RecipientIssue>();
        foreach (var reference in step.Participants.Distinct(StringComparer.OrdinalIgnoreCase)) {
            if (Email(reference)) {
                // The model may extract an explicit address, never invent a destination.
                if (System.Text.RegularExpressions.Regex.Matches(prompt, @"[^\s<>,;:]+@[^\s<>,;]+").Any(m => m.Value.TrimEnd('.', '!', '?', ')').Equals(reference, StringComparison.OrdinalIgnoreCase))) emails.Add(reference);
                else issues.Add(new(reference, []));
                continue;
            }
            var choices = account.Contacts.Where(c => c.Name.Equals(reference, StringComparison.OrdinalIgnoreCase))
                .Select(c => new RecipientChoice("contact:" + c.Id, c.Name + " — " + c.Email, [c.Email]))
                .Concat(account.Groups.Where(g => g.Name.Equals(reference, StringComparison.OrdinalIgnoreCase)).Select(g =>
                    new RecipientChoice("group:" + g.Id, g.Name, account.Contacts.Where(c => g.Members.Contains(c.Id)).Select(c => c.Email).ToArray()))).ToArray();
            if (choices.Length == 1 && choices[0].Emails.Length > 0) emails.AddRange(choices[0].Emails);
            else issues.Add(new(reference, choices));
        }
        step.Attendees = emails.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); step.RecipientIssues = issues.ToArray();
    }
    public static string PlannerContext(AccountPreferences p) {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrEmpty(p.TimeZone) ? "Asia/Jakarta" : p.TimeZone);
        return $" Output language: {(p.Language == "en" ? "English" : "natural, friendly Indonesian")}. User timezone: {zone.Id}. Current local date/time: {TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone):O}. Resolve relative dates using this timezone. ";
    }
}
