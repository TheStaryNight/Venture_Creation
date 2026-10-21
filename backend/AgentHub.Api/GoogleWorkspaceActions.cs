using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

static class GoogleWorkspaceActions
{
    public static async Task<(string Message, string Url, string? MeetUrl)> Execute(Step step, string? owner, GoogleTokenStore store, HttpClient client, CancellationToken ct)
    {
        if (PlanContract.Validate([step]) is string error)
            throw new ActionFailure(error);
        if (owner is null)
            throw new ActionFailure("Masuk dengan Google terlebih dahulu.");
        await store.Refresh(owner, client, ct);
        var scope = step.Agent == "gmail" ? GoogleConnection.GmailScope : GoogleConnection.DriveScope;
        if (!store.TryGet(owner, out var token) || token.ExpiresAt <= DateTimeOffset.UtcNow || !token.Scopes.Contains(scope))
            throw new ActionFailure("Hubungkan ulang Google dan setujui izin Gmail/Docs.");
        if (step.Agent == "gmail")
        {
            if (step.RecipientIssues.Length > 0 || step.Attendees.Length is < 1 or > 200 || step.Attendees.Any(e => e.Contains('\r') || e.Contains('\n') || !MailAddress.TryCreate(e, out var address) || address.Address != e))
                throw new ActionFailure("Pilih penerima email yang valid sebelum melanjutkan.");
            var mime = "To: " + string.Join(", ", step.Attendees.Distinct(StringComparer.OrdinalIgnoreCase)) + "\r\nSubject: =?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(step.Name)) + "?=\r\nMIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\n" + Convert.ToBase64String(Encoding.UTF8.GetBytes(step.Detail), Base64FormattingOptions.InsertLineBreaks);
            var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(mime)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            bool draft = step.Action == "create_draft";
            using var result = await Post("https://gmail.googleapis.com/gmail/v1/users/me/" + (draft ? "drafts" : "messages/send"), draft ? new
            {
                message = new
                {
                    raw
                }
            } : (object)new
            {
                raw
            }, token, client, ct);
            return (draft ? "Draft tersimpan di Gmail. Email belum dikirim." : "Email berhasil dikirim ke penerima yang disetujui.", draft ? "https://mail.google.com/mail/u/0/#drafts" : "https://mail.google.com/mail/u/0/#sent", null);
        }
        if (step.Agent != "docs")
            throw new ActionFailure("Tindakan tidak didukung.");
        if (step.Action == "create_document_in_folder" && string.IsNullOrEmpty(step.FolderId))
            throw new ActionFailure("Pilih folder tujuan dokumen terlebih dahulu.");
        var metadata = new Dictionary<string, object> { ["name"] = step.Name, ["mimeType"] = "application/vnd.google-apps.document" };
        if (step.Action == "create_document_in_folder")
            metadata["parents"] = new[] { step.FolderId };
        using var created = await Post("https://www.googleapis.com/drive/v3/files?fields=id", metadata, token, client, ct);
        var id = created.RootElement.GetProperty("id").GetString()!;
        var url = "https://docs.google.com/document/d/" + Uri.EscapeDataString(id) + "/edit";
        try
        {
            using var inserted = await Post("https://docs.googleapis.com/v1/documents/" + Uri.EscapeDataString(id) + ":batchUpdate", new
            {
                requests = new[] { new { insertText = new { location = new { index = 1 }, text = step.Detail } } }
            }, token, client, ct);
        }
        catch (Exception ex) when (ex is ActionFailure or HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new ActionFailure("Dokumen sudah dibuat, tetapi isi belum dapat dipastikan. Buka dokumen sebelum mengulang agar tidak membuat duplikat.", url);
        }
        return ("Dokumen dan isinya sudah dibuat. Izin berbagi tidak diubah.", url, null);
    }
    private static async Task<JsonDocument> Post(string url, object payload, GoogleTokens token, HttpClient client, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new ActionFailure((int)response.StatusCode switch
            {
                401 => "Sesi Google kedaluwarsa. Hubungkan ulang akun.",
                403 => "Google menolak akses. Aktifkan Gmail/Docs/Drive API dan setujui izin Google. Folder tujuan juga harus bisa diakses Orbit; pilih folder yang dibuat Orbit jika folder lama belum diizinkan.",
                404 => "Folder atau sumber Google tidak ditemukan atau belum dapat diakses Orbit.",
                _ => "Hasil tindakan Google belum dapat dipastikan. Periksa Gmail atau Drive sebelum mengulang pekerjaan."
            });
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
}
