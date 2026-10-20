using System.Text.Json;

sealed record AiSettings(string Provider, string Key, string Model, string? Error) {
    public bool Ready => Error is null && Key.Length > 0 && Model.Length > 0;
    public static AiSettings Read(IConfiguration config) {
        // Explicit provider wins. Otherwise a Gemini key takes precedence over old OpenAI variables.
        var provider = config["AI:Provider"]?.Trim();
        if (string.IsNullOrEmpty(provider)) provider = string.IsNullOrWhiteSpace(config["Gemini:ApiKey"]) ? "OpenAI" : "Gemini";
        provider = provider.ToLowerInvariant() switch { "gemini" => "Gemini", "openai" => "OpenAI", _ => provider };
        if (provider is not ("Gemini" or "OpenAI")) return new(provider, "", "", "AI__Provider harus Gemini atau OpenAI.");
        var key = config[$"{provider}:ApiKey"]?.Trim() ?? "";
        var model = config[$"{provider}:Model"]?.Trim() ?? "";
        if (provider == "Gemini" && model.Length == 0) model = "gemini-3.8-flash";
        string? error = null;
        if (key.Length > 0 && (model.Length == 0 || model.Equals("Default", StringComparison.OrdinalIgnoreCase)))
            error = $"Isi {provider}__Model dengan ID model, bukan nama project atau Default.";
        return new(provider, key, model, error);
    }
}

sealed class PlannerException(string message) : Exception(message);

static class GeminiPlanner {
    public static async Task<List<Step>> Create(string prompt, AiSettings settings, HttpClient client, CancellationToken ct, string context = "") {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions");
        request.Headers.Authorization = new("Bearer", settings.Key);
        request.Content = JsonContent.Create(new {
            model = settings.Model,
            messages = new[] {
                new { role = "system", content = PlanContract.Instructions + context },
                new { role = "user", content = prompt }
            },
            response_format = new { type = "json_schema", json_schema = new { name = "workflow_plan", strict = true, schema = PlanContract.Schema } }
        });
        using var response = await client.SendAsync(request, ct);
        // Never return provider response bodies: they may contain credential or prompt data.
        if (!response.IsSuccessStatusCode) throw new PlannerException((int)response.StatusCode switch {
            401 or 403 => "Gemini menolak akses. Periksa API key Google AI Studio, izin key, dan akses API project.",
            429 => "Kuota atau batas permintaan Gemini tercapai. Periksa kuota project di Google AI Studio lalu coba lagi.",
            404 => "Model Gemini tidak ditemukan atau tidak tersedia untuk akun ini. Periksa Gemini__Model.",
            400 => "Gemini menolak konfigurasi permintaan. Periksa key, ID model, dan dukungan Structured Outputs.",
            _ => $"Layanan Gemini mengembalikan HTTP {(int)response.StatusCode}. Coba lagi setelah beberapa saat; jika berulang, periksa model dan status layanan Google."
        });
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!body.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            throw new PlannerException("Gemini tidak menghasilkan rencana. Coba instruksi yang lebih spesifik.");
        var choice = choices[0];
        if (choice.GetProperty("finish_reason").GetString() != "stop")
            throw new PlannerException("Rencana Gemini tidak lengkap atau dibatasi. Coba instruksi yang lebih singkat.");
        var content = choice.GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(content)) throw new PlannerException("Gemini tidak menghasilkan rencana yang dapat dijalankan.");
        return PlanContract.Parse(content);
    }
}
