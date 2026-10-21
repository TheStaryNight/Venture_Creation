using System.Net;
using System.Text;
using System.Text.Json;

static class WorkspaceChecks {
    static void Check(bool value) { if (!value) throw new Exception("Gmail/Docs check failed"); }
    public static async Task Run() {
        var tokens = new GoogleTokenStore();
        tokens.Items["owner"] = new("fake", null, DateTimeOffset.UtcNow.AddHours(1), [GoogleConnection.GmailScope, GoogleConnection.DriveScope]);
        Step Email(string action) => new("gmail", "Email", "Halo, ini résumé tim.\nTerima kasih.") { Name = "Diskusi é", Action = action, Participants = ["friend@example.com"], Attendees = ["friend@example.com"] };
        foreach (var action in new[] { "create_draft", "send_email" }) {
            var handler = new Capture();
            await GoogleActions.Execute(Email(action), "owner", tokens, new HttpClient(handler), default);
            Check(handler.Calls.Count == 1);
            Check(handler.Calls[0].Url.EndsWith(action == "create_draft" ? "/drafts" : "/messages/send"));
            using var payload = JsonDocument.Parse(handler.Calls[0].Body);
            var raw = (action == "create_draft" ? payload.RootElement.GetProperty("message") : payload.RootElement).GetProperty("raw").GetString()!;
            raw = raw.Replace('-', '+').Replace('_', '/'); raw = raw.PadRight((raw.Length + 3) / 4 * 4, '=');
            var mime = Encoding.UTF8.GetString(Convert.FromBase64String(raw));
            Check(mime.Contains("To: friend@example.com\r\nSubject: =?UTF-8?B?"));
            Check(Encoding.UTF8.GetString(Convert.FromBase64String(mime.Split("\r\n\r\n")[1])) == Email(action).Detail);
        }
        var invented = Email("send_email"); AccountFeatures.Resolve(invented, new AccountData(), "Kirim ke teman");
        Check(invented.Attendees.Length == 0 && invented.RecipientIssues.Length > 0);
        Check(PlanContract.Validate([new("gmail", "Email", "Body") { Action = "send_email", Name = "Subject\r\nBcc: evil@example.com", Participants = ["a@example.com"] }]) is not null);
        var denied = new Capture();
        try { await GoogleActions.Execute(Email("send_email"), "other-owner", tokens, new HttpClient(denied), default); throw new Exception("Expected rejection"); }
        catch (ActionFailure) { Check(denied.Calls.Count == 0); }
        var document = new Step("docs", "Dokumen", "Isi dokumen\nBaris kedua") { Name = "Proposal", Action = "create_document_in_folder", FolderId = "folder123" };
        var docs = new Capture();
        var result = await GoogleActions.Execute(document, "owner", tokens, new HttpClient(docs), default);
        Check(result.Url == "https://docs.google.com/document/d/doc123/edit" && docs.Calls.Count == 2);
        using (var create = JsonDocument.Parse(docs.Calls[0].Body)) Check(create.RootElement.GetProperty("parents")[0].GetString() == "folder123");
        using (var insert = JsonDocument.Parse(docs.Calls[1].Body)) Check(insert.RootElement.GetProperty("requests")[0].GetProperty("insertText").GetProperty("text").GetString() == document.Detail);
        var failed = new Capture { FailAt = 2 };
        try { await GoogleActions.Execute(document, "owner", tokens, new HttpClient(failed), default); throw new Exception("Expected partial failure"); }
        catch (ActionFailure ex) { Check(ex.ResultUrl == result.Url && failed.Calls.Count == 2); }
        var uncertain = new Capture { FailAt = 1 };
        try { await GoogleActions.Execute(Email("send_email"), "owner", tokens, new HttpClient(uncertain), default); throw new Exception("Expected failure"); }
        catch (ActionFailure) { Check(uncertain.Calls.Count == 1); }
        Console.WriteLine("Gmail/Docs checks passed (mock HTTP; no email sent).");
    }
    sealed class Capture : HttpMessageHandler {
        public List<(string Url, string Body)> Calls { get; } = [];
        public int FailAt { get; init; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) {
            Calls.Add((r.RequestUri!.AbsoluteUri, await r.Content!.ReadAsStringAsync(ct)));
            return new(Calls.Count == FailAt ? HttpStatusCode.InternalServerError : HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"doc123\"}") };
        }
    }
}
