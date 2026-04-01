using System.Threading.Channels;
using Crystal.Adk.Core;

namespace Crystal.Adk.Session;

public sealed class AgentSession
{
    private readonly AgentHost _host;
    private List<RuntimeMessage> _messages = new();

    internal AgentSession(AgentHost host)
    {
        _host = host;
    }

    public IReadOnlyList<RuntimeMessage> Messages => _messages;

    // Session 自己维护消息历史，调用方只需要提供这次新的用户输入。
    public async Task<AgentRunResult> RunAsync(string userInput, CancellationToken cancellationToken = default)
    {
        _messages.Add(new RuntimeMessage { Role = "user", Content = userInput });
        var result = await _host.RunAsync(new AgentRunRequest { Messages = _messages }, null, cancellationToken);
        _messages = result.Messages;
        return result;
    }

    // 文本流通过 Channel 把 Host 的回调式事件桥接成 async enumerable，
    // 这样外部就可以直接 await foreach 消费文本分片。
    public async IAsyncEnumerable<TextChunk> StreamTextAsync(
        string userInput,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _messages.Add(new RuntimeMessage { Role = "user", Content = userInput });

        var channel = Channel.CreateUnbounded<TextChunk>();
        var request = new AgentRunRequest { Messages = _messages };

        var producer = Task.Run(async () =>
        {
            try
            {
                // 这条路径仍然走 provider 的真实文本流，
                // 因此 text_delta 可以在分片到达时立刻往外转发。
                var result = await _host.RunStreamingAsync(
                    request,
                    (evt, ct) =>
                    {
                        if (string.Equals(evt.Kind, AgentEventKinds.TextDelta, StringComparison.Ordinal))
                        {
                            channel.Writer.TryWrite(new TextChunk { Text = evt.Text ?? string.Empty });
                        }

                        return Task.CompletedTask;
                    },
                    cancellationToken);

                _messages = result.Messages;
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
        }, cancellationToken);

        await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return chunk;
        }

        await producer;
    }

    // v0.1.0 里的事件流是“事件的异步序列”，
    // 不是 provider 原始分片级别的实时事件流；事件来自非流式运行过程。
    public async IAsyncEnumerable<AgentEvent> StreamEventsAsync(
        string userInput,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _messages.Add(new RuntimeMessage { Role = "user", Content = userInput });
        var channel = Channel.CreateUnbounded<AgentEvent>();
        var request = new AgentRunRequest { Messages = _messages };

        var producer = Task.Run(async () =>
        {
            try
            {
                // RunAsync 会通过回调吐出统一的生命周期/工具事件，
                // Session 只负责按顺序把这些事件转发到 Channel。
                var result = await _host.RunAsync(
                    request,
                    (evt, ct) =>
                    {
                        channel.Writer.TryWrite(evt);
                        return Task.CompletedTask;
                    },
                    cancellationToken);

                _messages = result.Messages;
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
        }, cancellationToken);

        await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return evt;
        }

        await producer;
    }

    public void Reset()
    {
        _messages = new List<RuntimeMessage>();
    }
}
