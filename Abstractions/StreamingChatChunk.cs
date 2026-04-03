namespace Crystal.Adk.Abstractions;

public sealed class StreamingChatChunk
{
    public string? TextDelta { get; init; }
    public string? ThinkingDelta { get; init; }
    public bool IsCompleted { get; init; }
}
