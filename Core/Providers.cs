namespace Crystal.Adk.Core;

// 抽象接口类：模型提供商至少包含非流式与流式两个异步函数
public interface IChatProvider
{
    Task<RuntimeMessage> CreateCompletionAsync(
        IReadOnlyList<RuntimeMessage> messages,
        IReadOnlyList<AgentToolDescriptor> tools,
        CancellationToken cancellationToken);

    // IAsyncEnumerable<StreamingChatChunk>: 异步流式地产出很多个 StreamingChatChunk
    IAsyncEnumerable<StreamingChatChunk> StreamCompletionAsync(
        IReadOnlyList<RuntimeMessage> messages,
        IReadOnlyList<AgentToolDescriptor> tools,
        CancellationToken cancellationToken);
}


// 配置类： 流式文本块（协议传输层）
public sealed class StreamingChatChunk
{
    public string? TextDelta { get; init; }     // 正文文本块
    public string? ReasoningDelta { get; init; }        // 推理文本块
    public RuntimeMessage? SnapshotMessage { get; init; }       // 消息快照
    public string? FinishReason { get; init; }      // 流式结束原因
    public bool IsCompleted { get; init; }
}
