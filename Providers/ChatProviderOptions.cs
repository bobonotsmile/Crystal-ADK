namespace Crystal.Adk.Providers;

public sealed class ChatProviderOptions
{
    public string Vendor { get; init; } = "ollama";
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public int TimeoutMs { get; init; } = 30000;
    public double? Temperature { get; init; }
    public double? TopP { get; init; }
    public int? MaxOutputTokens { get; init; }
    public bool? EnableThinking { get; init; }
}
