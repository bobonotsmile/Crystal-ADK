namespace Crystal.Adk.Core;

// 配置类：主代理配置选项
public sealed class AgentHostOptions
{
    public string SystemPrompt { get; set; } = string.Empty;
    public int MaxRounds { get; set; } = 8;     // 单轮 Function calling 意图最大次数限制
}
