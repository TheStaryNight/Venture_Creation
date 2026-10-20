using System.Net.Http.Headers;
using System.Text.Json;

static class GoogleActions {
    public static async Task<(string Message, string Url, string? MeetUrl)> Execute(Step step, string? connectionId, GoogleTokenStore store, HttpClient client, CancellationToken ct) {
        if (step.Agent == "drive") {
            if (step.Action is "use_existing_folder" or "list_folder_files") return await DriveFolders.Execute(step, connectionId, store, client, ct);
            if (step.Action != "create_folder") throw new ActionFailure("Rencana Drive lama harus disusun ulang agar tindakan jelas.");
        }
        if (connectionId is not null) await store.Refresh(connectionId, client, ct);
        var scope = step.Agent == "drive" ? GoogleConnection.DriveScope : GoogleConnection.CalendarScope;
        if (connectionId is null || !store.TryGet(connectionId, out var token) || token.ExpiresAt <= DateTimeOffset.UtcNow || !token.Scopes.Contains(scope))
            throw new ActionFailure("Koneksi Google kedaluwarsa atau izin belum diberikan. Hubungkan Google lagi, lalu buat rencana baru.");
        using var request = new HttpRequestMessage(HttpMethod.Post, step.Agent == "drive"
            ? "https://www.googleapis.com/drive/v3/files?fields=id"
            : "https://www.googleapis.com/calendar/v3/calendars/primary/events?sendUpdates=" + (step.Attendees.Length > 0 ? "all" : "none") + (step.CreateMeet ? "&conferenceDataVersion=1" : ""));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        request.Content = step.Agent == "drive"
            ? JsonContent.Create(new { name = step.Name, mimeType = "application/vnd.google-apps.folder" })
            : JsonContent.Create(CalendarPayload(step));
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new ActionFailure((int)response.StatusCode switch {
            401 => "Akses Google ditolak atau kedaluwarsa. Hubungkan akun Google lagi.",
            403 => "Google menolak izin. Pastikan API Drive/Calendar aktif dan izin akun telah disetujui.",
            429 => "Batas permintaan Google tercapai. Langkah berikutnya dihentikan.",
            _ => $"Google mengembalikan HTTP {(int)response.StatusCode}. Periksa layanan sebelum mengulang pekerjaan."
        });
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var id = body.RootElement.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(id)) throw new ActionFailure("Google menerima permintaan tetapi ID hasil tidak tersedia. Periksa layanan sebelum mengulang.");
        if (step.Agent == "drive") return ("Folder dibuat di My Drive. Izin berbagi folder belum diubah.", "https://drive.google.com/drive/folders/" + Uri.EscapeDataString(id), null);
        var url = body.RootElement.GetProperty("htmlLink").GetString();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host is not ("www.google.com" or "calendar.google.com"))
            throw new ActionFailure("Event dibuat tetapi tautan hasil tidak valid. Periksa Calendar sebelum mengulang.");
        if (!step.CreateMeet) return ("Meeting dibuat di kalender utama. Undangan dikirim ke peserta yang tercantum.", url!, null);
        try {
            var current = body.RootElement.Clone();
            for (var attempt = 0; attempt < 5; attempt++) {
                var meetUrl = MeetLink(current);
                if (meetUrl is not null) return ("Meeting dan tautan Google Meet sudah dibuat. Undangan dikirim ke peserta yang tercantum.", url!, meetUrl);
                if (current.TryGetProperty("conferenceData", out var conference) && conference.TryGetProperty("createRequest", out var creation)
                    && creation.TryGetProperty("status", out var status) && status.GetProperty("statusCode").GetString() == "failure") break;
                if (attempt == 4) break;
                await Task.Delay(1000, ct);
                using var poll = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/calendar/v3/calendars/primary/events/" + Uri.EscapeDataString(id));
                poll.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
                using var update = await client.SendAsync(poll, ct);
                update.EnsureSuccessStatusCode();
                using var data = JsonDocument.Parse(await update.Content.ReadAsStringAsync(ct));
                current = data.RootElement.Clone();
            }
        } catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or TaskCanceledException) {
            throw new ActionFailure("Acara sudah dibuat, tetapi tautan Meet belum dapat dikonfirmasi. Buka acara untuk memeriksa sebelum mengulang.", url);
        }
        throw new ActionFailure("Acara sudah dibuat, tetapi Google belum menyediakan tautan Meet. Periksa acara dan dukungan Meet pada akunmu sebelum mengulang. Langkah berikutnya dihentikan.", url);
    }
    private static object CalendarPayload(Step step) {
        var payload = new Dictionary<string, object> {
            ["id"] = step.Id.ToString("N"), ["summary"] = step.Name,
            ["start"] = new { dateTime = step.Start }, ["end"] = new { dateTime = step.End }
        };
        if (step.Attendees.Length > 0) payload["attendees"] = step.Attendees.Select(email => new { email }).ToArray();
        if (step.CreateMeet) payload["conferenceData"] = new { createRequest = new {
            requestId = step.Id.ToString("N"), conferenceSolutionKey = new { type = "hangoutsMeet" }
        } };
        return payload;
    }
    private static string? MeetLink(JsonElement root) {
        if (!root.TryGetProperty("conferenceData", out var conference) || !conference.TryGetProperty("entryPoints", out var points)) return null;
        foreach (var point in points.EnumerateArray()) {
            if (point.GetProperty("entryPointType").GetString() == "video" && point.TryGetProperty("uri", out var value)
                && Uri.TryCreate(value.GetString(), UriKind.Absolute, out var link) && link.Scheme == "https" && link.Host == "meet.google.com") return link.AbsoluteUri;
        }
        return null;
    }
    public static string DiscordContent(Step step, IEnumerable<Step> prior) => step.Detail + (step.IncludeLinks
        ? "\n\n" + string.Join("\n", prior.Where(s => s.Status == "completed" && s.ResultUrl is not null).SelectMany(s => new[] { s.Action == "list_folder_files" ? s.Result : null, s.ResultUrl, s.MeetUrl }.Where(value => value is not null))) : "");
}
sealed class ActionFailure(string message, string? resultUrl = null) : Exception(message) { public string? ResultUrl { get; } = resultUrl; }
