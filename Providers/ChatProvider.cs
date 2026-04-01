using Crystal.Adk.Core;
using Crystal.Adk.Providers.Ark;
using Crystal.Adk.Providers.Ollama;

namespace Crystal.Adk.Providers;

public static class ChatProvider
{
    // 对外统一的 provider 创建入口。
    // 调用方只传一个扁平 settings，对内再转成各厂商自己的 options。
    public static IChatProvider Create(HttpClient httpClient, ChatProviderSettings settings)
    {
        if (httpClient is null) throw new ArgumentNullException(nameof(httpClient));
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        var vendor = (settings.Vendor ?? "ollama").Trim().ToLowerInvariant();
        return vendor switch
        {
            "ark" => new ArkChatProvider(httpClient, ToArkOptions(settings)),
            "ollama" => new OllamaChatProvider(httpClient, ToOllamaOptions(settings)),
            _ => throw new InvalidOperationException($"Unsupported provider vendor: {settings.Vendor}")
        };
    }

    private static ArkChatProviderOptions ToArkOptions(ChatProviderSettings settings)
    {
        // 把外部统一 settings 转成 ARK 专用 options，
        // 对外 API 才能保持一个入口、一套配置形状。
        return new ArkChatProviderOptions
        {
            ApiKey = settings.ApiKey,
            BaseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
                ? "https://ark.cn-beijing.volces.com/api/v3/chat/completions"
                : settings.BaseUrl,
            Model = settings.Model,
            TimeoutMs = settings.TimeoutMs,
            Temperature = settings.Temperature,
            TopP = settings.TopP,
            MaxOutputTokens = settings.MaxOutputTokens,
            Stop = settings.Stop,
            ToolChoice = settings.ToolChoice,
            ParallelToolCalls = settings.ParallelToolCalls,
            ResponseFormat = settings.ResponseFormat,
            EnableThinking = settings.EnableThinking,
            VendorOptions = new Dictionary<string, object?>(settings.VendorOptions, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static OllamaChatProviderOptions ToOllamaOptions(ChatProviderSettings settings)
    {
        // 和 ARK 一样先做统一 settings -> 厂商 options 的转换，
        // 这里只是额外补上 Ollama 自己的 keep_alive / think 等字段。
        return new OllamaChatProviderOptions
        {
            ApiKey = settings.ApiKey,
            BaseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
                ? "http://127.0.0.1:11434/api/chat"
                : settings.BaseUrl,
            Model = settings.Model,
            TimeoutMs = settings.TimeoutMs,
            Temperature = settings.Temperature,
            TopP = settings.TopP,
            MaxOutputTokens = settings.MaxOutputTokens,
            Stop = settings.Stop,
            ToolChoice = settings.ToolChoice,
            ParallelToolCalls = settings.ParallelToolCalls,
            ResponseFormat = settings.ResponseFormat,
            EnableThinking = settings.EnableThinking,
            KeepAlive = settings.KeepAlive,
            ThinkLevel = settings.ThinkLevel,
            VendorOptions = new Dictionary<string, object?>(settings.VendorOptions, StringComparer.OrdinalIgnoreCase)
        };
    }
}
