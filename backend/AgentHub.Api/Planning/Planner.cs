using System.Text.Json;

static class Planner
{
    public static async Task<List<Step>> Create(string prompt, IConfiguration config, HttpClient client, CancellationToken ct, string context = "")
    {
        var settings = AiSettings.Read(config);
        if (settings.Provider == "Gemini")
            return await GeminiPlanner.Create(prompt, settings, client, ct, context);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new("Bearer", config["OpenAI:ApiKey"]);
        request.Content = JsonContent.Create(new
        {
            model = config["OpenAI:Model"],
            store = false,
            instructions = PlanContract.Instructions + context,
            input = prompt,
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "workflow_plan",
                    strict = true,
                    schema = PlanContract.Schema
                }
            }
        });
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var text = document.RootElement.GetProperty("output").EnumerateArray()
            .Where(o => o.TryGetProperty("type", out var type) && type.GetString() == "message")
            .SelectMany(o => o.GetProperty("content").EnumerateArray())
            .First(c => c.GetProperty("type").GetString() == "output_text").GetProperty("text").GetString();
        return PlanContract.Parse(text!);
    }
}
