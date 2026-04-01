namespace Crystal.Adk.Core;


// 单条聊天上下文的消息记录，role 可能是 system、user、assistant、tool
public sealed class RuntimeMessage
{
    public string Role { get; set; } = "user";
    public string? Content { get; set; }
    public string? ToolCallId { get; set; }     // 模型发起一次工具调用会带上 ToolCallId （多数厂商协议会携带，Ollama没有），工具执行完再回一条 role=tool 的消息，就需要带上对应的 ToolCallId。这样模型才知道这个结果是回给哪次调用的
    public string? Name { get; set; }
    public List<RuntimeToolCall>? ToolCalls { get; set; }       //assistant 请求工具调用的消息里附加此字段，要求调用工具以及附加对应参数
}


// 单次模型方的 ToolCalls 的具体内容数据结构
public sealed class RuntimeToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object?> Arguments { get; set; } = new(StringComparer.OrdinalIgnoreCase);     // StringComparer.OrdinalIgnoreCase 表示键名比较时不区分大小写
}

// 业务侧工具函数的描述器（ 描述 + 参数 schema ）
public sealed class AgentToolDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object ParametersSchema { get; set; } = new { type = "object", properties = new { } };
}

// 业务侧预设 Agent 运行配置（ 消息列表 + 单轮 Function calling 意图最大次数限制 ）
public sealed class AgentRunRequest
{
    public List<RuntimeMessage> Messages { get; set; } = new();
    public int? MaxRounds { get; set; }
}

// 业务侧工具实际执行后的结果记录
public sealed class ToolRunRecord
{
    public string ToolCallId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object?> Arguments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public object? Result { get; set; }
    public long ElapsedMs { get; set; }     // 耗时
}


// Agent 执行结束后的总结果（最终消息 + 消息列表 + 工具执行结果列表）
public sealed class AgentRunResult
{
    public string FinalMessage { get; set; } = string.Empty;
    public List<RuntimeMessage> Messages { get; set; } = new();
    public List<ToolRunRecord> ToolRuns { get; set; } = new();
}
