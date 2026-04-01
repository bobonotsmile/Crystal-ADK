# crystal-adk

[English](./README.en.md) | [简体中文](./README.zh-CN.md)

面向 C# / .NET 的轻量 Agent Development Kit。

`crystal-adk` 只处理运行时这一层：Session 消息状态、Tool Loop、Provider 协议映射、文本流输出。业务逻辑继续留在业务项目里。

## 它解决什么问题

- 维护对话消息数组
- 维护 `assistant -> tool -> assistant` 主循环
- 适配不同厂商的 Chat API 协议
- 提供统一的 Provider 创建入口
- 支持非流式运行
- 支持文本流输出
- 支持运行事件的异步枚举

## 它不打算做什么

- 工作流编排引擎
- planner 框架
- 插件平台
- Web 宿主框架

## 如何引用

当前仓库使用项目引用方式。

在你的应用项目里添加：

```xml
<ItemGroup>
  <ProjectReference Include="..\\crystal-adk\\Crystal.Adk.csproj" />
</ItemGroup>
```

当前目标框架是 `net6.0`。

## 如何使用

### 1. 创建 Provider

```csharp
using Crystal.Adk.Core;
using Crystal.Adk.Providers;

using var httpClient = new HttpClient();

var provider = ChatProvider.Create(httpClient, new ChatProviderSettings
{
    Vendor = "ollama",
    BaseUrl = "http://127.0.0.1:11434/api/chat",
    Model = "gpt-oss:20b",
    Temperature = 0.2,
    TopP = 0.9,
    MaxOutputTokens = 512,
    EnableThinking = false
});
```

当前代码里支持的 `Vendor`：

- `ollama`
- `ark`

### 2. 创建 Host 和 Session

```csharp
var host = new AgentHost(provider, new AgentHostOptions
{
    SystemPrompt = "你是一个简洁的助手。",
    MaxRounds = 4
});

var session = host.CreateSession();
```

### 3. 非流式运行

```csharp
var result = await session.RunAsync("查询设备 EQ-001");

Console.WriteLine(result.FinalMessage);
```

### 4. 文本流输出

```csharp
await foreach (var chunk in session.StreamTextAsync("请解释什么是设备履历分析"))
{
    Console.Write(chunk.Text);
}
```

### 5. 读取运行事件

```csharp
await foreach (var evt in session.StreamEventsAsync("查询设备 EQ-001 并总结状态"))
{
    Console.WriteLine(evt.Kind);
}
```

当前事件类型：

- `run_started`
- `tool_call_started`
- `tool_call_completed`
- `final_answer`
- `run_completed`
- `run_failed`

说明：`v0.1.0` 里的 `StreamEventsAsync(...)` 不是 provider 原始实时分片事件流，而是运行过程中的统一事件异步序列。

## 工具示例

```csharp
using Crystal.Adk.Core;

public sealed class QueryDeviceInfoTool : IAgentTool
{
    public AgentToolDescriptor Descriptor { get; } = new()
    {
        Name = "query_device_info",
        Description = "查询设备基础信息和运行状态。",
        ParametersSchema = new
        {
            type = "object",
            properties = new
            {
                deviceCode = new { type = "string", description = "设备编号，例如 EQ-001" }
            },
            required = new[] { "deviceCode" }
        }
    };

    public Task<object?> InvokeAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var deviceCode = arguments["deviceCode"]?.ToString();
        return Task.FromResult<object?>(new
        {
            deviceCode,
            status = "running"
        });
    }
}
```

注册工具后再创建 Host：

```csharp
var toolHub = new ToolHub();
toolHub.Register(new QueryDeviceInfoTool());

var host = new AgentHost(toolHub, provider, new AgentHostOptions
{
    SystemPrompt = "当需要结构化数据时优先调用工具。",
    MaxRounds = 6
});
```

## 如何运行 adk-lab 示例

当前工作区包含 `adk-lab/`，用于最小示例和终端实验。

运行方式：

```powershell
dotnet run --project .\adk-lab\adk-lab.csproj -- 01
dotnet run --project .\adk-lab\adk-lab.csproj -- 02
dotnet run --project .\adk-lab\adk-lab.csproj -- 03
dotnet run --project .\adk-lab\adk-lab.csproj -- 04
dotnet run --project .\adk-lab\adk-lab.csproj -- 05
dotnet run --project .\adk-lab\adk-lab.csproj -- 06
```

当前示例覆盖：

- `01`：Ollama 非流式
- `02`：Ollama 文本流
- `03`：Ollama 工具运行事件
- `04`：ARK 非流式
- `05`：ARK 文本流
- `06`：ARK 工具运行事件

## 仓库结构

```text
crystal-adk/
├─ Core/
├─ Providers/
├─ Session/
├─ DOCS/
└─ Crystal.Adk.csproj
```

## 文档

- [DOCS/版本设计/v0.1.0/代码设计.md](./DOCS/%E7%89%88%E6%9C%AC%E8%AE%BE%E8%AE%A1/v0.1.0/%E4%BB%A3%E7%A0%81%E8%AE%BE%E8%AE%A1.md)
- [DOCS/版本设计/v0.1.0/特性清单.md](./DOCS/%E7%89%88%E6%9C%AC%E8%AE%BE%E8%AE%A1/v0.1.0/%E7%89%B9%E6%80%A7%E6%B8%85%E5%8D%95.md)

## 当前版本范围

`v0.1.0` 当前方向：

- `RunAsync(...)`
- `StreamTextAsync(...)`
- `StreamEventsAsync(...)`
- 进程内 Session 状态
- Function Calling
- 优先兼容 ARK 和 Ollama

`v0.1.0` 暂不做：

- workflow / planner 抽象
- 插件系统
- Session 持久化存储
- 自动上下文压缩 / 摘要
