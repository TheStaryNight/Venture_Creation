using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

static class DriveFolders {
    const string FolderType = "application/vnd.google-apps.folder";
    public static void MapDriveFolders(this WebApplication app) {
        app.MapGet("/api/google/folders", async (string? search, string? pageToken, HttpContext context, GoogleTokenStore tokens, IHttpClientFactory clients, CancellationToken ct) => {
            var owner = context.User.FindFirstValue("orbit_google_connection");
            if (owner is null) return Results.Unauthorized();
            if ((search?.Length ?? 0) > 200 || (pageToken?.Length ?? 0) > 4096) return Results.BadRequest(new { error = "Pencarian terlalu panjang." });
            try {
                var client = clients.CreateClient("external");
                var token = await Token(owner, tokens, client, ct);
                var query = $"trashed = false and mimeType = '{FolderType}'";
                if (!string.IsNullOrWhiteSpace(search)) query += " and name contains '" + search.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
                using var json = await Get("files?q=" + Uri.EscapeDataString(query) + "&pageSize=50&orderBy=name&fields=nextPageToken,files(id,name,modifiedTime)&supportsAllDrives=true&includeItemsFromAllDrives=true" + (string.IsNullOrEmpty(pageToken) ? "" : "&pageToken=" + Uri.EscapeDataString(pageToken)), token, client, ct);
                return Results.Ok(json.RootElement.Clone());
            } catch (ActionFailure ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return Results.Problem(statusCode: 502, title: "Daftar folder Google belum dapat dimuat. Coba lagi."); }
        });
        app.MapPost("/api/workflows/{id:guid}/steps/{stepId:guid}/folder", async (Guid id, Guid stepId, FolderSelection selection, WorkflowStore workflows, GoogleTokenStore tokens, IHttpClientFactory clients, HttpContext context, CancellationToken ct) => {
            var owner = context.User.FindFirstValue("orbit_google_connection");
            if (owner is null) return Results.Unauthorized();
            if (!workflows.Items.TryGetValue(id, out var workflow) || workflow.Owner != owner) return Results.NotFound();
            var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId && ((s.Agent == "drive" && s.Action is "use_existing_folder" or "list_folder_files") || (s.Agent == "docs" && s.Action == "create_document_in_folder")));
            if (step is null) return Results.BadRequest(new { error = "Langkah ini tidak memakai folder lama." });
            try {
                var client = clients.CreateClient("external");
                var token = await Token(owner, tokens, client, ct);
                var folder = await Folder(selection.FolderId, token, client, ct);
                lock (workflow.Sync) {
                    if (workflow.Status != "planned") return Results.Conflict(new { error = "Folder hanya dapat diganti sebelum pekerjaan dijalankan." });
                    var oldId = step.FolderId; var oldName = step.FolderName;
                    step.FolderId = folder.Id; step.FolderName = folder.Name;
                    try { workflows.Persist(workflow); }
                    catch { step.FolderId = oldId; step.FolderName = oldName; throw; }
                }
                return Results.Ok(workflow.Snapshot());
            } catch (ActionFailure ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return Results.Problem(statusCode: 502, title: "Folder belum dapat diperiksa. Coba lagi."); }
        });
    }
    public static async Task<GoogleTokens> Token(string? owner, GoogleTokenStore store, HttpClient client, CancellationToken ct) {
        if (owner is null) throw new ActionFailure("Masuk dengan Google terlebih dahulu.");
        await store.Refresh(owner, client, ct);
        if (!store.TryGet(owner, out var token) || token.ExpiresAt <= DateTimeOffset.UtcNow || !token.Scopes.Contains(GoogleConnection.DriveMetadataScope))
            throw new ActionFailure("Hubungkan ulang Google dan izinkan pembacaan daftar file untuk memilih folder lama.");
        return token;
    }
    static async Task<JsonDocument> Get(string path, GoogleTokens token, HttpClient client, CancellationToken ct) {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/drive/v3/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new ActionFailure((int)response.StatusCode switch {
            401 => "Sesi Google kedaluwarsa. Hubungkan ulang Google.",
            403 => "Izin membaca daftar file belum tersedia. Hubungkan ulang Google dan setujui izin Drive.",
            404 => "Folder tidak ditemukan atau tidak dapat diakses oleh akun Google ini.",
            _ => "Google belum dapat membaca folder. Coba lagi nanti."
        });
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    static async Task<FolderInfo> Folder(string id, GoogleTokens token, HttpClient client, CancellationToken ct) {
        if (string.IsNullOrEmpty(id) || !Regex.IsMatch(id, "^[a-zA-Z0-9_-]{1,200}$")) throw new ActionFailure("Pilih folder Google Drive yang valid.");
        using var json = await Get("files/" + Uri.EscapeDataString(id) + "?fields=id,name,mimeType,trashed&supportsAllDrives=true", token, client, ct);
        var root = json.RootElement;
        if (root.GetProperty("mimeType").GetString() != FolderType || root.GetProperty("trashed").GetBoolean()) throw new ActionFailure("Pilihan harus berupa folder aktif di Google Drive.");
        return new(root.GetProperty("id").GetString()!, root.GetProperty("name").GetString()!);
    }
    public static async Task<(string Message, string Url, string? MeetUrl)> Execute(Step step, string? owner, GoogleTokenStore store, HttpClient client, CancellationToken ct) {
        var token = await Token(owner, store, client, ct);
        var folder = await Folder(step.FolderId, token, client, ct);
        var url = "https://drive.google.com/drive/folders/" + Uri.EscapeDataString(folder.Id);
        if (step.Action == "use_existing_folder") return ("Menggunakan folder: " + folder.Name, url, null);
        if (step.Action != "list_folder_files") throw new ActionFailure("Jenis tindakan Drive tidak valid. Susun ulang rencana.");
        using var json = await Get("files?q=" + Uri.EscapeDataString("'" + folder.Id + "' in parents and trashed = false") + "&pageSize=20&orderBy=name&fields=nextPageToken,files(id,name,mimeType)&supportsAllDrives=true&includeItemsFromAllDrives=true", token, client, ct);
        var files = json.RootElement.GetProperty("files").EnumerateArray().ToArray();
        var names = files.Select(f => f.GetProperty("name").GetString() ?? "Tanpa nama");
        var message = "Isi folder " + folder.Name + ":\n" + (files.Length == 0 ? "Tidak ada file langsung di folder ini." : string.Join("\n", names.Select(n => "• " + n)));
        if (json.RootElement.TryGetProperty("nextPageToken", out _)) message += "\nDaftar dibatasi 20 file pertama. Buka folder untuk melihat sisanya.";
        return (message, url, null);
    }
    sealed record FolderSelection(string FolderId);
    sealed record FolderInfo(string Id, string Name);
}
