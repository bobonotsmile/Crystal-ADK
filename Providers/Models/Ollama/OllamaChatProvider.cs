using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Crystal.Adk.Abstractions;

namespace Crystal.Adk.Providers.Models.Ollama;

internal sealed class OllamaChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly ChatProviderOptions _options;

    public OllamaChatProvider(HttpClient httpClient, ChatProviderOptions options)
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

        var options = new Dictionary<string, object?>();
        if (_options.Temperature.HasValue) options["temperature"] = _options.Temperature.Value;
        if (_options.TopP.HasValue) options["top_p"] = _options.TopP.Value;
        if (_options.MaxOutputTokens.HasValue) options["num_predict"] = _options.MaxOutputTokens.Value;
        if (options.Count > 0) body["options"] = options;
        if (_options.EnableThinking.HasValue) body["think"] = _options.EnableThinking.Value;

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
                mapped["thinking"] = message.ThinkingContent;
            }

            return mapped;
        }).ToList();
    }

    private static RuntimeMessage ParseAssistantMessage(JsonElement root)
    {
        var message = root.GetProperty("message");
        return new RuntimeMessage
        {
            Role = "assistant",
            Content = message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String
                ? content.GetString() ?? string.Empty
                : string.Empty,
            ThinkingContent = message.TryGetProperty("thinking", out var thinking) &&
                              thinking.ValueKind == JsonValueKind.String
                ? thinking.GetString()
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

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var message = root.TryGetProperty("message", out var messageNode) ? messageNode : default;

            var text = message.ValueKind == JsonValueKind.Object &&
                       message.TryGetProperty("content", out var contentNode) &&
                       contentNode.ValueKind == JsonValueKind.String
                ? contentNode.GetString()
                : null;

            var thinking = message.ValueKind == JsonValueKind.Object &&
                           message.TryGetProperty("thinking", out var thinkingNode) &&
                           thinkingNode.ValueKind == JsonValueKind.String
                ? thinkingNode.GetString()
                : null;

            var isCompleted = root.TryGetProperty("done", out var doneNode) &&
                              doneNode.ValueKind == JsonValueKind.True;

            yield return new StreamingChatChunk
            {
                TextDelta = text,
                ThinkingDelta = thinking,
                IsCompleted = isCompleted
            };
        }
    }
}
