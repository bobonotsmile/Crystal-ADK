namespace Crystal.Adk.Core;

// 抽象接口类：工具（工具说明 + 执行工具的异步函数）
public interface IAgentTool
{
    AgentToolDescriptor Descriptor { get; }
    Task<object?> InvokeAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken);
}

public sealed class ToolHub
{
    // readonly 表示这个字段在初始化后，不能再被重新指向别的对象，并非不能写了
    private readonly Dictionary<string, IAgentTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    // 工具注册进 _tools 字典
    public void Register(IAgentTool tool)
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        if (string.IsNullOrWhiteSpace(tool.Descriptor.Name))
        {
            throw new InvalidOperationException("Tool name is required.");
        }

        if (_tools.ContainsKey(tool.Descriptor.Name))
        {
            throw new InvalidOperationException($"Duplicated tool: {tool.Descriptor.Name}");
        }

        _tools[tool.Descriptor.Name] = tool;
    }

    // 获取工具说明的 List
    public IReadOnlyList<AgentToolDescriptor> GetDescriptors() => _tools.Values.Select(x => x.Descriptor).ToList();

    public async Task<object?> InvokeAsync(string name, Dictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        if (!_tools.TryGetValue(name, out var tool))
        {
            throw new InvalidOperationException($"TOOL_NOT_FOUND: {name}");
        }

        return await tool.InvokeAsync(arguments, cancellationToken);
    }
}
