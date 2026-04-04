using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Crystal.Adk.Abstractions;

namespace Crystal.Adk.Providers.Models.Ark;

internal sealed class ArkChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly ChatProviderOptions _options;

    public ArkChatProvider(HttpClient httpClient, ChatProviderOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<RuntimeMessage> CompleteAsync(
        IReadOnlyList<RuntimeMessage> messages,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateHttpRequest(BuildRequestBody(messages, stream: false));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.TimeoutMs);

        using var response = await _httpClient.SendAsync(request, cts.Token);
        var responseText = await response.Content.ReadAsStringAsync(cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}{Environment.NewLine}{responseText}");
        }

        using var doc = JsonDocument.Parse(responseText);
        return ParseAssistantMessage(doc.RootElement);
    }

    public async IAsyncEnumerable<StreamingChatChunk> StreamAsync(
        IReadOnlyList<RuntimeMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = CreateHttpRequest(BuildRequestBody(messages, stream: true));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.TimeoutMs);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync(cts.Token);
            throw new InvalidOperationException(
                $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}{Environment.NewLine}{errorText}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);

        await foreach (var chunk in ReadStreamAsync(reader, cts.Token))
        {
            yield return chunk;
        }
    }

    private HttpRequestMessage CreateHttpRequest(Dictionary<string, object?> body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, new JsonSerializerOptions
                {
                    WriteIndented = true
                }),
                Encoding.UTF8,
                "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        return request;
    }

    private Dictionary<string, object?> BuildRequestBody(
        IReadOnlyList<RuntimeMessage> messages,
        bool stream)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = _options.Model,
            ["messages"] = MapMessages(messages),
            ["stream"] = stream
        };

        if (_options.Temperature.HasValue) body["temperature"] = _options.Temperature.Value;
        if (_options.TopP.HasValue) body["top_p"] = _options.TopP.Value;
        if (_options.MaxOutputTokens.HasValue) body["max_tokens"] = _options.MaxOutputTokens.Value;
        if (_options.EnableThinking.HasValue)
        {
            body["thinking"] = new Dictionary<string, object?>
            {
                ["type"] = _options.EnableThinking.Value ? "enabled" : "disabled"
            };
        }

        return body;
    }

    private static List<Dictionary<string, object?>> MapMessages(IReadOnlyList<RuntimeMessage> messages)
    {
        return messages.Select(message =>
        {
            var mapped = new Dictionary<string, object?>
            {
                ["role"] = message.Role,
                ["content"] = message.Content ?? string.Empty
            };

            if (!string.IsNullOrWhiteSpace(message.ThinkingContent))
            {
                mapped["reasoning_content"] = message.ThinkingContent;
            }

            return mapped;
        }).ToList();
    }

    private static RuntimeMessage ParseAssistantMessage(JsonElement root)
    {
        var message = root.GetProperty("choices")[0].GetProperty("message");
        return new RuntimeMessage
        {
            Role = "assistant",
            Content = message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String
                ? content.GetString() ?? string.Empty
                : string.Empty,
            ThinkingContent = message.TryGetProperty("reasoning_content", out var reasoningContent) &&
                              reasoningContent.ValueKind == JsonValueKind.String
                ? reasoningContent.GetString()
                : null
        };
    }

    private static async IAsyncEnumerable<StreamingChatChunk> ReadStreamAsync(
        StreamReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().WaitAsync(cancellationToken);
            if (line is null)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(line) ||
                !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = line[5..].Trim();
            if (string.Equals(payload, "[DONE]", StringComparison.Ordinal))
            {
                yield return new StreamingChatChunk { IsCompleted = true };
                yield break;
            }

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (!root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                continue;
            }

            var choice = choices[0];
            if (!choice.TryGetProperty("delta", out var delta))
            {
                continue;
            }

            var text = delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String
                ? content.GetString()
                : null;

            var thinking = delta.TryGetProperty("reasoning_content", out var reasoningNode) &&
                           reasoningNode.ValueKind == JsonValueKind.String
                ? reasoningNode.GetString()
                : null;

            var finishReason = choice.TryGetProperty("finish_reason", out var finishNode) &&
                               finishNode.ValueKind == JsonValueKind.String
                ? finishNode.GetString()
                : null;

            yield return new StreamingChatChunk
            {
                TextDelta = text,
                ThinkingDelta = thinking,
                IsCompleted = string.Equals(finishReason, "stop", StringComparison.OrdinalIgnoreCase)
            };
        }
    }
}
