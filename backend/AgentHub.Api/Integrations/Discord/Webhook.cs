static class Webhook
{
    public static bool IsValid(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host == "discord.com" && uri.AbsolutePath.StartsWith("/api/webhooks/");
}
