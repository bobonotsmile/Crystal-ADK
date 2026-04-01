namespace Crystal.Adk.Providers;

// 业务侧调用方 看到的统一 provider 配置模型。
// 调用方只需要认识这一层，不需要直接接触各厂商自己的 options 类型。
public sealed class ChatProviderSettings
{
    public string Vendor { get; set; } = "ollama";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 30000;
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public int? MaxOutputTokens { get; set; }
    public IReadOnlyList<string>? Stop { get; set; }
    public string? ToolChoice { get; set; }
    public bool? ParallelToolCalls { get; set; }
    public string? ResponseFormat { get; set; }
    public bool? EnableThinking { get; set; }
    public string? KeepAlive { get; set; }
    public string? ThinkLevel { get; set; }
    // 统一模型暂时没覆盖到的厂商字段可以先透传到这里，
    // 避免每次都为了单个厂商参数改公共配置结构。
    public Dictionary<string, object?> VendorOptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
