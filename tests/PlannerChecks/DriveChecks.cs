using System.Net;

static class DriveChecks {
    public static async Task Run() {
        var tokens = new GoogleTokenStore();
        tokens.Items["owner"] = new("fake", null, DateTimeOffset.UtcNow.AddMinutes(30), [GoogleConnection.DriveMetadataScope]);
        var handler = new ReadHandler();
        var client = new HttpClient(handler);
        var step = new Step("drive", "Isi folder", "Lihat isi") { Action = "list_folder_files", FolderId = "original-folder", Name = "user testing" };
        var result = await GoogleActions.Execute(step, "owner", tokens, client, default);
        if (!result.Message.Contains("apple.png") || !result.Url.EndsWith("original-folder") || handler.Calls != 2) throw new Exception("Existing folder result mismatch");
        step.Result = result.Message; step.ResultUrl = result.Url; step.Status = "completed";
        if (!GoogleActions.DiscordContent(new("discord", "Kirim", "Hasil") { IncludeLinks = true }, [step]).Contains("apple.png")) throw new Exception("File listing not forwarded");
        var use = new Step("drive", "Link", "Gunakan") { Action = "use_existing_folder", FolderId = "original-folder" };
        await GoogleActions.Execute(use, "owner", tokens, client, default);
        if (handler.Calls != 3) throw new Exception("Using folder should only fetch its metadata");
        var previous = handler.Calls;
        foreach (var invalid in new[] { new Step("drive", "Legacy", "Old"), new Step("drive", "No selection", "Read") { Action = "list_folder_files" } }) {
            try { await GoogleActions.Execute(invalid, "owner", tokens, client, default); throw new Exception("Expected invalid action/selection rejection"); }
            catch (ActionFailure) { }
        }
        try { await GoogleActions.Execute(use, "another-owner", tokens, client, default); throw new Exception("Expected owner rejection"); }
        catch (ActionFailure) { }
        if (handler.Calls != previous) throw new Exception("Invalid selection sent a request");
        tokens.Items["owner"] = tokens.Items["owner"] with { Scopes = [GoogleConnection.DriveScope] };
        try { await GoogleActions.Execute(use, "owner", tokens, client, default); throw new Exception("Expected metadata permission rejection"); }
        catch (ActionFailure) { }
        if (handler.Calls != previous) throw new Exception("Missing metadata permission sent a request");
        Console.WriteLine("PASS: existing Drive folder uses GET only, original ID and file names preserved, missing selection/scope/owner rejected.");
    }
    sealed class ReadHandler : HttpMessageHandler {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            if (request.Method != HttpMethod.Get) throw new Exception("Existing-folder operation attempted a write");
            Calls++;
            var body = request.RequestUri!.AbsolutePath.EndsWith("/original-folder")
                ? """{"id":"original-folder","name":"user testing","mimeType":"application/vnd.google-apps.folder","trashed":false}"""
                : """{"files":[{"id":"apple-id","name":"apple.png","mimeType":"image/png"}]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
