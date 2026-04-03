namespace Crystal.Adk.Abstractions;

public sealed class RuntimeMessage
{
    public string Role { get; set; } = "user";
    public string? Content { get; set; }
    public string? ThinkingContent { get; set; }
}
