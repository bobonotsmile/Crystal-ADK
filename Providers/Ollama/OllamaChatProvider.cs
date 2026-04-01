using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Crystal.Adk.Core;

namespace Crystal.Adk.Providers.Ollama;

public sealed class OllamaChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly OllamaChatProviderOptions _options;

    public OllamaChatProvider(HttpClient httpClient, OllamaChatProviderOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<RuntimeMessage> CreateCompletionAsync(
        IReadOnlyList<RuntimeMessage> messages,
        IReadOnlyList<AgentToolDescriptor> tools,
        CancellationToken cancellationToken)
    {
        // 非流式 completion 会返回一条完整的 Ollama message，
        // 如果模型决定调工具，tool calls 也会一并包含在里面。
        using var request = CreateRequest(BuildRequestBody(messages, tools, stream: false));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.TimeoutMs);

        HttpResponseMessage response;
        string responseText;
        try
        {
            response = await _httpClient.SendAsync(request, cts.Token);
            responseText = await response.Content.ReadAsStringAsync(cts.Token);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"OLLAMA_TIMEOUT: request timed out after {_options.TimeoutMs} ms. url={_options.BaseUrl}, model={_options.Model}",
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"OLLAMA_CONNECTION_ERROR: failed to connect to {_options.BaseUrl}. {ex.Message}",
                ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"OLLAMA_HTTP_ERROR: {(int)response.StatusCode} {responseText}");
            }

            using var doc = JsonDocument.Parse(responseText);
            return ParseCompletionMessage(doc.RootElement);
        }
    }

    public async IAsyncEnumerable<StreamingChatChunk> StreamCompletionAsync(
        IReadOnlyList<RuntimeMessage> messages,
        IReadOnlyList<AgentToolDescriptor> tools,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Ollama 的流式协议是逐行 NDJSON，不是 SSE。
        using var request = CreateRequest(BuildRequestBody(messages, tools, stream: true));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.TimeoutMs);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"OLLAMA_TIMEOUT: streaming request timed out after {_options.TimeoutMs} ms. url={_options.BaseUrl}, model={_options.Model}",
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"OLLAMA_CONNECTION_ERROR: failed to connect to {_options.BaseUrl}. {ex.Message}",
                ex);
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var message = root.TryGetProperty("message", out var messageNode) ? messageNode : default;

            RuntimeMessage? snapshot = null;
            if (message.ValueKind == JsonValueKind.Object &&
                message.TryGetProperty("tool_calls", out var toolCalls) &&
                toolCalls.ValueKind == JsonValueKind.Array)
            {
                // 每个分片里都可能带着当前工具调用快照，只是格式遵循 Ollama 自己的协议。
                snapshot = new RuntimeMessage
                {
                    Role = "assistant",
                    ToolCalls = ParseToolCalls(toolCalls)
                };
            }

            yield return new StreamingChatChunk
            {
                TextDelta = message.ValueKind == JsonValueKind.Object &&
                            message.TryGetProperty("content", out var contentNode) &&
                            contentNode.ValueKind == JsonValueKind.String
                    ? contentNode.GetString()
                    : null,
                ReasoningDelta = message.ValueKind == JsonValueKind.Object &&
                                 message.TryGetProperty("thinking", out var thinkingNode) &&
                                 thinkingNode.ValueKind == JsonValueKind.String
                    ? thinkingNode.GetString()
                    : null,
                SnapshotMessage = snapshot,
                FinishReason = root.TryGetProperty("done_reason", out var doneReason) && doneReason.ValueKind == JsonValueKind.String
                    ? doneReason.GetString()
                    : null,
                IsCompleted = root.TryGetProperty("done", out var doneNode) && doneNode.ValueKind == JsonValueKind.True
            };
        }
    }

    private HttpRequestMessage CreateRequest(Dictionary<string, object?> body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        return request;
    }

    private Dictionary<string, object?> BuildRequestBody(
        IReadOnlyList<RuntimeMessage> messages,
        IReadOnlyList<AgentToolDescriptor> tools,
        bool stream)
    {
        // Ollama 的参数一部分放在 options 里，
        // 另一部分是 think / format 这类顶层字段。
        var body = new Dictionary<string, object?>
        {
            ["model"] = _options.Model,
            ["messages"] = MapMessages(messages),
            ["tools"] = MapTools(tools),
            ["stream"] = stream
        };

        var options = new Dictionary<string, object?>();
        if (_options.Temperature.HasValue) options["temperature"] = _options.Temperature.Value;
        if (_options.TopP.HasValue) options["top_p"] = _options.TopP.Value;
        if (_options.MaxOutputTokens.HasValue) options["num_predict"] = _options.MaxOutputTokens.Value;
        if (_options.Stop is { Count: > 0 }) options["stop"] = _options.Stop.Count == 1 ? _options.Stop[0] : _options.Stop;

        foreach (var pair in _options.VendorOptions)
        {
            options[pair.Key] = pair.Value;
        }

        if (options.Count > 0) body["options"] = options;
        if (!string.IsNullOrWhiteSpace(_options.ResponseFormat)) body["format"] = _options.ResponseFormat;
        if (_options.EnableThinking.HasValue) body["think"] = _options.EnableThinking.Value;
        if (!string.IsNullOrWhiteSpace(_options.ThinkLevel)) body["think"] = _options.ThinkLevel;
        if (!string.IsNullOrWhiteSpace(_options.KeepAlive)) body["keep_alive"] = _options.KeepAlive;

        return body;
    }

    private static List<Dictionary<string, object?>> MapMessages(IReadOnlyList<RuntimeMessage> messages)
    {
        return messages.Select(message =>
        {
            if (string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) && message.ToolCalls is { Count: > 0 })
            {
                // Ollama 接受 function.arguments 直接传对象，
                // 这点和要求字符串化 arguments 的 ARK 不同。
                return new Dictionary<string, object?>
                {
                    ["role"] = "assistant",
                    ["content"] = message.Content ?? string.Empty,
                    ["tool_calls"] = message.ToolCalls.Select(call => new Dictionary<string, object?>
                    {
                        ["function"] = new Dictionary<string, object?>
                        {
                            ["name"] = call.Name,
                            ["arguments"] = call.Arguments
                        }
                    }).ToList()
                };
            }

            return new Dictionary<string, object?>
            {
                ["role"] = message.Role,
                ["content"] = message.Content ?? string.Empty
            };
        }).ToList();
    }

    private static List<Dictionary<string, object?>> MapTools(IReadOnlyList<AgentToolDescriptor> tools)
    {
        return tools.Select(tool => new Dictionary<string, object?>
        {
            ["type"] = "function",
            ["function"] = new Dictionary<string, object?>
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parameters"] = tool.ParametersSchema
            }
        }).ToList();
    }

    private static RuntimeMessage ParseCompletionMessage(JsonElement root)
    {
        // 把 Ollama 的响应归一化成 ADK 统一的 RuntimeMessage。
        var message = root.GetProperty("message");
        var result = new RuntimeMessage
        {
            Role = "assistant",
            Content = message.TryGetProperty("content", out var contentNode) && contentNode.ValueKind == JsonValueKind.String
                ? contentNode.GetString()
                : string.Empty
        };

        if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
        {
            result.ToolCalls = ParseToolCalls(toolCalls);
        }

        return result;
    }

    private static List<RuntimeToolCall> ParseToolCalls(JsonElement toolCalls)
    {
        var list = new List<RuntimeToolCall>();
        var index = 0;
        foreach (var toolCall in toolCalls.EnumerateArray())
        {
            index++;
            if (!toolCall.TryGetProperty("function", out var function))
            {
                continue;
            }

            var arguments = function.TryGetProperty("arguments", out var argsNode)
                ? NormalizeArguments(argsNode)
                : new Dictionary<string, object?>();

            list.Add(new RuntimeToolCall
            {
                Id = $"ollama_call_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{index}",
                Name = function.TryGetProperty("name", out var nameNode) && nameNode.ValueKind == JsonValueKind.String
                    ? nameNode.GetString() ?? string.Empty
                    : string.Empty,
                Arguments = arguments
            });
        }

        return list;
    }

    private static Dictionary<string, object?> NormalizeArguments(JsonElement node)
    {
        // Ollama 的 arguments 可能直接是对象，也可能是 JSON 字符串；
        // 这里统一整理成 Dictionary<string, object?>。
        if (node.ValueKind == JsonValueKind.Object)
        {
            return ProviderJson.ConvertObject(node);
        }

        if (node.ValueKind == JsonValueKind.String)
        {
            return ProviderJson.ParseArguments(node.GetString() ?? "{}");
        }

        return new Dictionary<string, object?>();
    }
}
