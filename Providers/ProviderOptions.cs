namespace Crystal.Adk.Providers;

// Provider 内部使用的配置基类。
// 外部先传 ChatProviderSettings，再由工厂转换成这些厂商专用 options。
public abstract class ChatProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 30000;
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public int? MaxOutputTokens { get; set; }
    public IReadOnlyList<string>? Stop { get; set; }
    public string? ToolChoice { get; set; }
    public bool? ParallelToolCalls { get; set; }
    public string? ResponseFormat { get; set; }
    public bool? EnableThinking { get; set; }
    public Dictionary<string, object?> VendorOptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ArkChatProviderOptions : ChatProviderOptions
{
    public ArkChatProviderOptions()
    {
        // 给 ARK 一个可直接运行的默认地址，调用方不填时也能有合理默认值。
        BaseUrl = "https://ark.cn-beijing.volces.com/api/v3/chat/completions";
    }
}

public sealed class OllamaChatProviderOptions : ChatProviderOptions
{
    public OllamaChatProviderOptions()
    {
        // Ollama 默认走本地 /api/chat 接口。
        BaseUrl = "http://127.0.0.1:11434/api/chat";
    }

    // 这两个字段是 Ollama 特有的运行参数，因此放在专用 options 上。
    public string? KeepAlive { get; set; }
    public string? ThinkLevel { get; set; }
}
