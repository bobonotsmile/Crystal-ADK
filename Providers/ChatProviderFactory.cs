using Crystal.Adk.Providers.Models.Ark;
using Crystal.Adk.Providers.Models.Ollama;

namespace Crystal.Adk.Providers;

public static class ChatProviderFactory
{
    public static IChatProvider Create(HttpClient httpClient, ChatProviderOptions options)
    {
        return options.Vendor switch
        {
            "ark" => new ArkChatProvider(httpClient, options),
            "ollama" => new OllamaChatProvider(httpClient, options),
            _ => throw new InvalidOperationException($"Unsupported vendor: {options.Vendor}")
        };
    }
}
