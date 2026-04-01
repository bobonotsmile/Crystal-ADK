using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Crystal.Adk.Core;

namespace Crystal.Adk.Providers.Ark;

public sealed class ArkChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly ArkChatProviderOptions _options;

    public ArkChatProvider(HttpClient httpClient, ArkChatProviderOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<RuntimeMessage> CreateCompletionAsync(
        IReadOnlyList<RuntimeMessage> messages,
        IReadOnlyList<AgentToolDescriptor> tools,
        CancellationToken cancellationToken)
    {
        // 非流式 completion 对 tool calling 最稳妥，
        // 因为 ARK 会一次返回完整 assistant 消息和完整工具参数。
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
                $"ARK_TIMEOUT: request timed out after {_options.TimeoutMs} ms. url={_options.BaseUrl}, model={_options.Model}",
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"ARK_CONNECTION_ERROR: failed to connect to {_options.BaseUrl}. {ex.Message}",
                ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"ARK_HTTP_ERROR: {(int)response.StatusCode} {responseText}");
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
        // ARK 的流式协议是 SSE，每个有效负载都以 "data: ..." 的形式到达。
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
                $"ARK_TIMEOUT: streaming request timed out after {_options.TimeoutMs} ms. url={_options.BaseUrl}, model={_options.Model}",
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"ARK_CONNECTION_ERROR: failed to connect to {_options.BaseUrl}. {ex.Message}",
                ex);
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
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
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            {
                continue;
            }

            var choice = choices[0];
            var finishReason = choice.TryGetProperty("finish_reason", out var finishNode) && finishNode.ValueKind == JsonValueKind.String
                ? finishNode.GetString()
                : null;

            if (!choice.TryGetProperty("delta", out var delta))
            {
                continue;
            }

            RuntimeMessage? snapshotMessage = null;
            if (delta.TryGetProperty("tool_calls", out var deltaToolCalls) && deltaToolCalls.ValueKind == JsonValueKind.Array)
            {
                // 流式里的 delta tool calls 只是当前分片的快照，
                // 后面是否聚合、是否执行，由 Host 决定。
                snapshotMessage = new RuntimeMessage
                {
                    Role = "assistant",
                    ToolCalls = ParseDeltaToolCalls(deltaToolCalls)
                };
            }

            yield return new StreamingChatChunk
            {
                TextDelta = delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String ? content.GetString() : null,
                ReasoningDelta = delta.TryGetProperty("reasoning_content", out var reasoning) && reasoning.ValueKind == JsonValueKind.String ? reasoning.GetString() : null,
                SnapshotMessage = snapshotMessage,
                FinishReason = finishReason,
                IsCompleted = string.Equals(finishReason, "stop", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(finishReason, "tool_calls", StringComparison.OrdinalIgnoreCase)
            };
        }
    }

    private HttpRequestMessage CreateRequest(Dictionary<string, object?> body)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("ARK_CONFIG_ERROR: ApiKey is required.");
        }

        if (_options.ApiKey.Any(ch => ch > 127))
        {
            throw new InvalidOperationException("ARK_CONFIG_ERROR: ApiKey must contain only ASCII characters. Replace the example placeholder with your real API key.");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        return request;
    }

    private Dictionary<string, object?> BuildRequestBody(
        IReadOnlyList<RuntimeMessage> messages,
        IReadOnlyList<AgentToolDescriptor> tools,
        bool stream)
    {
        // RuntimeMessage / AgentToolDescriptor 是 ADK 的中立模型，
        // 这里负责把中立模型映射成 ARK 的请求体格式。
        var body = new Dictionary<string, object?>
        {
            ["model"] = _options.Model,
            ["messages"] = MapMessages(messages),
            ["tools"] = MapTools(tools),
            ["stream"] = stream,
            ["thinking"] = new Dictionary<string, object?>
            {
                ["type"] = _options.EnableThinking == true ? "enabled" : "disabled"
            }
        };

        if (_options.Temperature.HasValue) body["temperature"] = _options.Temperature.Value;
        if (_options.TopP.HasValue) body["top_p"] = _options.TopP.Value;
        if (_options.MaxOutputTokens.HasValue) body["max_tokens"] = _options.MaxOutputTokens.Value;
        if (_options.Stop is { Count: > 0 }) body["stop"] = _options.Stop.Count == 1 ? _options.Stop[0] : _options.Stop;
        if (!string.IsNullOrWhiteSpace(_options.ToolChoice)) body["tool_choice"] = _options.ToolChoice;
        if (_options.ParallelToolCalls.HasValue) body["parallel_tool_calls"] = _options.ParallelToolCalls.Value;
        if (!string.IsNullOrWhiteSpace(_options.ResponseFormat))
        {
            body["response_format"] = new Dictionary<string, object?> { ["type"] = _options.ResponseFormat };
        }

        foreach (var pair in _options.VendorOptions)
        {
            body[pair.Key] = pair.Value;
        }

        return body;
    }

    private static List<Dictionary<string, object?>> MapMessages(IReadOnlyList<RuntimeMessage> messages)
    {
        var list = new List<Dictionary<string, object?>>();
        foreach (var message in messages)
        {
            if (string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new Dictionary<string, object?>
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = message.ToolCallId,
                    ["content"] = message.Content ?? string.Empty
                });
                continue;
            }

            if (string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) && message.ToolCalls is { Count: > 0 })
            {
                // ARK 要求 function.arguments 是 JSON 字符串，
                // 不是对象本身，所以这里显式序列化。
                list.Add(new Dictionary<string, object?>
                {
                    ["role"] = "assistant",
                    ["content"] = message.Content ?? string.Empty,
                    ["tool_calls"] = message.ToolCalls.Select(call => new Dictionary<string, object?>
                    {
                        ["id"] = call.Id,
                        ["type"] = "function",
                        ["function"] = new Dictionary<string, object?>
                        {
                            ["name"] = call.Name,
                            ["arguments"] = JsonSerializer.Serialize(call.Arguments)
                        }
                    }).ToList()
                });
                continue;
            }

            list.Add(new Dictionary<string, object?>
            {
                ["role"] = message.Role,
                ["content"] = message.Content ?? string.Empty
            });
        }

        return list;
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
        // 把 ARK 的 chat completions 响应归一化成一个 assistant 消息，
        // 这样 Host 就能继续走统一的 tool loop。
        var message = root.GetProperty("choices")[0].GetProperty("message");
        var result = new RuntimeMessage
        {
            Role = "assistant",
            Content = message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String
                ? content.GetString()
                : string.Empty
        };

        if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
        {
            result.ToolCalls = ParseCompletionToolCalls(toolCalls);
        }

        return result;
    }

    private static List<RuntimeToolCall> ParseCompletionToolCalls(JsonElement toolCalls)
    {
        var list = new List<RuntimeToolCall>();
        foreach (var toolCall in toolCalls.EnumerateArray())
        {
            var function = toolCall.GetProperty("function");
            list.Add(new RuntimeToolCall
            {
                Id = toolCall.TryGetProperty("id", out var idNode) && idNode.ValueKind == JsonValueKind.String
                    ? idNode.GetString() ?? Guid.NewGuid().ToString("N")
                    : Guid.NewGuid().ToString("N"),
                Name = function.GetProperty("name").GetString() ?? string.Empty,
                Arguments = ProviderJson.ParseArguments(function.GetProperty("arguments").GetString() ?? "{}")
            });
        }

        return list;
    }

    private static List<RuntimeToolCall> ParseDeltaToolCalls(JsonElement toolCalls)
    {
        // 流式 tool-call arguments 可能只到了一部分字符串，
        // 这里先尽量解析，后续由更上层决定是否使用。
        var list = new List<RuntimeToolCall>();
        foreach (var toolCall in toolCalls.EnumerateArray())
        {
            if (!toolCall.TryGetProperty("function", out var function))
            {
                continue;
            }

            var rawArgs = function.TryGetProperty("arguments", out var argsNode) && argsNode.ValueKind == JsonValueKind.String
                ? argsNode.GetString() ?? "{}"
                : "{}";

            list.Add(new RuntimeToolCall
            {
                Id = toolCall.TryGetProperty("id", out var idNode) && idNode.ValueKind == JsonValueKind.String
                    ? idNode.GetString() ?? Guid.NewGuid().ToString("N")
                    : Guid.NewGuid().ToString("N"),
                Name = function.TryGetProperty("name", out var nameNode) && nameNode.ValueKind == JsonValueKind.String
                    ? nameNode.GetString() ?? string.Empty
                    : string.Empty,
                Arguments = ProviderJson.ParseArguments(rawArgs)
            });
        }

        return list;
    }
}
