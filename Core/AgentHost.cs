using System.Text;
using System.Text.Json;
using Crystal.Adk.Session;

namespace Crystal.Adk.Core;

public sealed class AgentHost
{
    // 工具中心：保存全部已注册工具，并负责按名称执行工具
    private readonly ToolHub _toolHub;

    // 模型提供者：负责真正调用底层大模型
    private readonly IChatProvider _chatProvider;

    // Host 运行配置：包含 system prompt、最大轮数等默认设置
    private readonly AgentHostOptions _options;

    // 简化构造函数，为了兼容不传 ToolHub 的使用体验：
    // 只传模型提供者时，自动创建一个新的 ToolHub
    public AgentHost(IChatProvider chatProvider, AgentHostOptions? options = null)
        : this(new ToolHub(), chatProvider, options)
    {
    }

    // 完整构造函数：
    // 手动传入 ToolHub、模型提供者和配置
    public AgentHost(ToolHub toolHub, IChatProvider chatProvider, AgentHostOptions? options = null)
    {
        _toolHub = toolHub;
        _chatProvider = chatProvider;
        _options = options ?? new AgentHostOptions();
    }

    // 创建一个新的会话对象，用于保存多轮对话历史
    public AgentSession CreateSession()
    {
        return new AgentSession(this);
    }

    // 非流式运行：模型先返回完整消息；
    // 执行一次完整 Agent 运行，并返回最终结果
    public async Task<AgentRunResult> RunAsync(
        AgentRunRequest request,
        Func<AgentEvent, CancellationToken, Task>? onEvent = null,
        CancellationToken cancellationToken = default)
    {
        if (request.Messages.Count == 0)
        {
            var badRequestEvent = new AgentEvent
            {
                Kind = AgentEventKinds.RunFailed,
                ErrorCode = "BAD_REQUEST",
                ErrorMessage = "messages is required"
            };
            await EmitStreamEventAsync(onEvent, badRequestEvent, cancellationToken);
            throw new InvalidOperationException("messages is required");
        }

        var runId = Guid.NewGuid().ToString("N");
        var messages = BuildInitialMessages(request.Messages);
        var toolDescriptors = _toolHub.GetDescriptors();
        var toolRuns = new List<ToolRunRecord>();
        var seenToolCalls = new HashSet<string>(StringComparer.Ordinal);
        var maxRounds = request.MaxRounds ?? _options.MaxRounds;

        await EmitStreamEventAsync(onEvent, new AgentEvent
        {
            Kind = AgentEventKinds.RunStarted,
            RunId = runId
        }, cancellationToken);

        for (var round = 1; round <= maxRounds; round++)
        {
            RuntimeMessage assistant;
            try
            {
                assistant = await _chatProvider.CreateCompletionAsync(messages, toolDescriptors, cancellationToken);
            }
            catch (Exception ex)
            {
                await EmitStreamEventAsync(onEvent, new AgentEvent
                {
                    Kind = AgentEventKinds.RunFailed,
                    RunId = runId,
                    ErrorCode = "PROVIDER_ERROR",
                    ErrorMessage = ex.Message
                }, cancellationToken);
                throw;
            }

            messages.Add(assistant);

            var toolCalls = assistant.ToolCalls ?? new List<RuntimeToolCall>();
            if (toolCalls.Count == 0)
            {
                var finalText = assistant.Content ?? string.Empty;
                await EmitStreamEventAsync(onEvent, new AgentEvent
                {
                    Kind = AgentEventKinds.FinalAnswer,
                    RunId = runId,
                    Text = finalText
                }, cancellationToken);
                await EmitStreamEventAsync(onEvent, new AgentEvent
                {
                    Kind = AgentEventKinds.RunCompleted,
                    RunId = runId
                }, cancellationToken);

                return new AgentRunResult
                {
                    FinalMessage = finalText,
                    Messages = messages,
                    ToolRuns = toolRuns
                };
            }

            foreach (var toolCall in toolCalls)
            {
                // 同名且同参数的重复工具调用通常意味着模型陷入循环，
                // 这里直接拦截，避免无限调工具。
                var signature = BuildToolCallSignature(toolCall.Name, toolCall.Arguments);
                if (!seenToolCalls.Add(signature))
                {
                    await EmitStreamEventAsync(onEvent, new AgentEvent
                    {
                        Kind = AgentEventKinds.RunFailed,
                        RunId = runId,
                        ErrorCode = "DUPLICATE_TOOL_CALL",
                        ErrorMessage = $"Duplicated tool call blocked: {toolCall.Name}"
                    }, cancellationToken);
                    throw new InvalidOperationException($"DUPLICATE_TOOL_CALL: {toolCall.Name}");
                }

                await EmitStreamEventAsync(onEvent, new AgentEvent
                {
                    Kind = AgentEventKinds.ToolCallStarted,
                    RunId = runId,
                    ToolCallId = toolCall.Id,
                    ToolName = toolCall.Name,
                    Arguments = toolCall.Arguments
                }, cancellationToken);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                object? result;
                try
                {
                    result = await _toolHub.InvokeAsync(toolCall.Name, toolCall.Arguments, cancellationToken);
                }
                catch (Exception ex)
                {
                    await EmitStreamEventAsync(onEvent, new AgentEvent
                    {
                        Kind = AgentEventKinds.RunFailed,
                        RunId = runId,
                        ErrorCode = "TOOL_EXECUTION_ERROR",
                        ErrorMessage = ex.Message,
                        ToolCallId = toolCall.Id,
                        ToolName = toolCall.Name
                    }, cancellationToken);
                    throw;
                }
                finally
                {
                    sw.Stop();
                }

                toolRuns.Add(new ToolRunRecord
                {
                    ToolCallId = toolCall.Id,
                    ToolName = toolCall.Name,
                    Arguments = toolCall.Arguments,
                    Result = result,
                    ElapsedMs = sw.ElapsedMilliseconds
                });

                await EmitStreamEventAsync(onEvent, new AgentEvent
                {
                    Kind = AgentEventKinds.ToolCallCompleted,
                    RunId = runId,
                    ToolCallId = toolCall.Id,
                    ToolName = toolCall.Name,
                    Result = result,
                    ElapsedMs = sw.ElapsedMilliseconds
                }, cancellationToken);

                messages.Add(new RuntimeMessage
                {
                    Role = "tool",
                    Name = toolCall.Name,
                    ToolCallId = toolCall.Id,
                    Content = SerializeToolContent(result)
                });
            }
        }

        await EmitStreamEventAsync(onEvent, new AgentEvent
        {
            Kind = AgentEventKinds.RunFailed,
            RunId = runId,
            ErrorCode = "TOOL_LOOP_MAX_ROUNDS",
            ErrorMessage = "Tool loop exceeded max rounds."
        }, cancellationToken);
        throw new InvalidOperationException("Tool loop exceeded max rounds.");
    }

    // 流式运行：一边消费 provider 的文本分片，一边向外发 text_delta，
    // 其余 tool loop 结构仍然和非流式路径保持一致。
    internal async Task<AgentRunResult> RunStreamingAsync(
        AgentRunRequest request,
        Func<AgentEvent, CancellationToken, Task>? onEvent = null,
        CancellationToken cancellationToken = default)
    {
        if (request.Messages.Count == 0)
        {
            var badRequestEvent = new AgentEvent
            {
                Kind = AgentEventKinds.RunFailed,
                ErrorCode = "BAD_REQUEST",
                ErrorMessage = "messages is required"
            };
            await EmitStreamEventAsync(onEvent, badRequestEvent, cancellationToken);
            throw new InvalidOperationException("messages is required");
        }

        var runId = Guid.NewGuid().ToString("N");
        var messages = BuildInitialMessages(request.Messages);
        var toolDescriptors = _toolHub.GetDescriptors();
        var toolRuns = new List<ToolRunRecord>();
        var seenToolCalls = new HashSet<string>(StringComparer.Ordinal);
        var maxRounds = request.MaxRounds ?? _options.MaxRounds;

        await EmitStreamEventAsync(onEvent, new AgentEvent
        {
            Kind = AgentEventKinds.RunStarted,
            RunId = runId
        }, cancellationToken);

        for (var round = 1; round <= maxRounds; round++)
        {
            RuntimeMessage? assistant = null;
            var streamedText = new StringBuilder();
            var streamedToolCalls = new List<RuntimeToolCall>();

            await foreach (var chunk in _chatProvider.StreamCompletionAsync(messages, toolDescriptors, cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.TextDelta))
                {
                    streamedText.Append(chunk.TextDelta);
                    await EmitStreamEventAsync(onEvent, new AgentEvent
                    {
                        Kind = AgentEventKinds.TextDelta,
                        RunId = runId,
                        Text = chunk.TextDelta
                    }, cancellationToken);
                }

                if (chunk.SnapshotMessage?.ToolCalls is { Count: > 0 })
                {
                    // 同一个工具调用可能会在多个流式分片里重复出现，
                    // 这里按 tool-call id 去重，保证 Host 只执行一次。
                    foreach (var toolCall in chunk.SnapshotMessage.ToolCalls)
                    {
                        if (!string.IsNullOrWhiteSpace(toolCall.Name) &&
                            !streamedToolCalls.Any(x => string.Equals(x.Id, toolCall.Id, StringComparison.Ordinal)))
                        {
                            streamedToolCalls.Add(toolCall);
                        }
                    }
                }

                if (chunk.IsCompleted)
                {
                    assistant = new RuntimeMessage
                    {
                        Role = "assistant",
                        Content = streamedText.ToString(),
                        ToolCalls = streamedToolCalls.Count > 0 ? streamedToolCalls : null
                    };
                }
            }

            // 有些 provider 不一定能稳定给出完整的流式结束快照，
            // 这里回退到一次普通 completion，保证本轮还能正常收尾。
            assistant ??= await _chatProvider.CreateCompletionAsync(messages, toolDescriptors, cancellationToken);
            messages.Add(assistant);

            var toolCalls = assistant.ToolCalls ?? new List<RuntimeToolCall>();
            if (toolCalls.Count == 0)
            {
                var finalText = assistant.Content ?? string.Empty;
                await EmitStreamEventAsync(onEvent, new AgentEvent
                {
                    Kind = AgentEventKinds.FinalAnswer,
                    RunId = runId,
                    Text = finalText
                }, cancellationToken);
                await EmitStreamEventAsync(onEvent, new AgentEvent
                {
                    Kind = AgentEventKinds.RunCompleted,
                    RunId = runId
                }, cancellationToken);

                return new AgentRunResult
                {
                    FinalMessage = finalText,
                    Messages = messages,
                    ToolRuns = toolRuns
                };
            }

            foreach (var toolCall in toolCalls)
            {
                var signature = BuildToolCallSignature(toolCall.Name, toolCall.Arguments);
                if (!seenToolCalls.Add(signature))
                {
                    var duplicateEvent = new AgentEvent
                    {
                        Kind = AgentEventKinds.RunFailed,
                        RunId = runId,
                        ErrorCode = "DUPLICATE_TOOL_CALL",
                        ErrorMessage = $"Duplicated tool call blocked: {toolCall.Name}"
                    };
                    await EmitStreamEventAsync(onEvent, duplicateEvent, cancellationToken);
                    throw new InvalidOperationException($"DUPLICATE_TOOL_CALL: {toolCall.Name}");
                }

                await EmitStreamEventAsync(onEvent, new AgentEvent
                {
                    Kind = AgentEventKinds.ToolCallStarted,
                    RunId = runId,
                    ToolCallId = toolCall.Id,
                    ToolName = toolCall.Name,
                    Arguments = toolCall.Arguments
                }, cancellationToken);

                object? result = null;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Exception? toolError = null;
                try
                {
                    result = await _toolHub.InvokeAsync(toolCall.Name, toolCall.Arguments, cancellationToken);
                }
                catch (Exception ex)
                {
                    toolError = ex;
                }
                finally
                {
                    sw.Stop();
                }

                if (toolError is not null)
                {
                    var toolErrorEvent = new AgentEvent
                    {
                        Kind = AgentEventKinds.RunFailed,
                        RunId = runId,
                        ErrorCode = "TOOL_EXECUTION_ERROR",
                        ErrorMessage = toolError.Message,
                        ToolCallId = toolCall.Id,
                        ToolName = toolCall.Name
                    };
                    await EmitStreamEventAsync(onEvent, toolErrorEvent, cancellationToken);
                    throw new InvalidOperationException(toolError.Message, toolError);
                }

                toolRuns.Add(new ToolRunRecord
                {
                    ToolCallId = toolCall.Id,
                    ToolName = toolCall.Name,
                    Arguments = toolCall.Arguments,
                    Result = result,
                    ElapsedMs = sw.ElapsedMilliseconds
                });

                await EmitStreamEventAsync(onEvent, new AgentEvent
                {
                    Kind = AgentEventKinds.ToolCallCompleted,
                    RunId = runId,
                    ToolCallId = toolCall.Id,
                    ToolName = toolCall.Name,
                    Result = result,
                    ElapsedMs = sw.ElapsedMilliseconds
                }, cancellationToken);

                messages.Add(new RuntimeMessage
                {
                    Role = "tool",
                    Name = toolCall.Name,
                    ToolCallId = toolCall.Id,
                    Content = SerializeToolContent(result)
                });
            }
        }

        var maxRoundsEvent = new AgentEvent
        {
            Kind = AgentEventKinds.RunFailed,
            RunId = runId,
            ErrorCode = "TOOL_LOOP_MAX_ROUNDS",
            ErrorMessage = "Tool loop exceeded max rounds."
        };
        await EmitStreamEventAsync(onEvent, maxRoundsEvent, cancellationToken);
        throw new InvalidOperationException("Tool loop exceeded max rounds.");
    }

    // 构建本次运行的初始消息列表
    // 若配置中有 SystemPrompt 且消息中尚无 system 消息，则自动补一条
    private List<RuntimeMessage> BuildInitialMessages(IReadOnlyList<RuntimeMessage> incomingMessages)
    {
        // 先克隆一份消息列表，避免 Host 在追加 system / assistant / tool
        // 消息时直接改掉调用方手里的原始集合。
        var messages = incomingMessages.Select(CloneMessage).ToList();
        if (string.IsNullOrWhiteSpace(_options.SystemPrompt))
        {
            return messages;
        }

        var hasSystemMessage = messages.Any(x => string.Equals(x.Role, "system", StringComparison.OrdinalIgnoreCase));
        if (!hasSystemMessage)
        {
            messages.Insert(0, new RuntimeMessage
            {
                Role = "system",
                Content = _options.SystemPrompt.Trim()
            });
        }

        return messages;
    }

    // 深拷贝一条 RuntimeMessage，避免直接改动外部传入消息对象
    private static RuntimeMessage CloneMessage(RuntimeMessage message)
    {
        return new RuntimeMessage
        {
            Role = message.Role,
            Content = message.Content,
            ToolCallId = message.ToolCallId,
            Name = message.Name,
            ToolCalls = message.ToolCalls?.Select(call => new RuntimeToolCall
            {
                Id = call.Id,
                Name = call.Name,
                Arguments = new Dictionary<string, object?>(call.Arguments, StringComparer.OrdinalIgnoreCase)
            }).ToList()
        };
    }

    // 统一发事件：
    // 如果外部传了 onEvent，就把事件发出去；否则直接忽略
    private static async Task EmitStreamEventAsync(
        Func<AgentEvent, CancellationToken, Task>? onEvent,
        AgentEvent evt,
        CancellationToken cancellationToken)
    {
        if (onEvent is null)
        {
            return;
        }

        await onEvent(evt, cancellationToken);
    }

    // 将工具执行结果转成字符串，便于写入 role=tool 的消息内容
    private static string SerializeToolContent(object? value)
    {
        // provider 协议通常要求 tool 结果以消息文本形式回填，
        // 而不是直接传 CLR 对象，所以这里统一序列化成字符串。
        if (value is null) return "null";
        if (value is string text) return text;
        return JsonSerializer.Serialize(value);
    }

    // 根据工具名和参数构造“调用签名”
    // 用于检测重复工具调用
    private static string BuildToolCallSignature(string toolName, IReadOnlyDictionary<string, object?> arguments)
    {
        var normalized = NormalizeForSignature(arguments);
        return $"{toolName}|{JsonSerializer.Serialize(normalized)}";
    }

    // 归一化参数对象，保证签名构造稳定
    // 例如把字典 key 排序、递归处理列表和嵌套对象
    private static object? NormalizeForSignature(object? value)
    {
        if (value is null) return null;

        if (value is IReadOnlyDictionary<string, object?> roDict)
        {
            var sorted = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (var pair in roDict)
            {
                sorted[pair.Key] = NormalizeForSignature(pair.Value);
            }

            return sorted;
        }

        if (value is Dictionary<string, object?> dict)
        {
            var sorted = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (var pair in dict)
            {
                sorted[pair.Key] = NormalizeForSignature(pair.Value);
            }

            return sorted;
        }

        if (value is IEnumerable<object?> array)
        {
            return array.Select(NormalizeForSignature).ToList();
        }

        return value;
    }
}
