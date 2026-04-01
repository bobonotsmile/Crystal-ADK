namespace Crystal.Adk.Core;

public static class AgentEventKinds
{
    public const string RunStarted = "run_started";
    public const string ToolCallStarted = "tool_call_started";
    public const string ToolCallCompleted = "tool_call_completed";
    public const string TextDelta = "text_delta";
    public const string FinalAnswer = "final_answer";
    public const string RunCompleted = "run_completed";
    public const string RunFailed = "run_failed";
}

public sealed class AgentEvent
{
    public string Kind { get; init; } = string.Empty;
    public string? RunId { get; init; }
    public string? ToolCallId { get; init; }
    public string? ToolName { get; init; }
    public string? Text { get; init; }
    public object? Result { get; init; }
    public Dictionary<string, object?>? Arguments { get; init; }
    public long? ElapsedMs { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
