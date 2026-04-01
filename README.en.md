# crystal-adk

[English](./README.en.md) | [简体中文](./README.zh-CN.md)

Lightweight Agent Development Kit for C# / .NET.

`crystal-adk` keeps the runtime layer small: session messages, tool loop, provider protocol mapping, and text streaming. Business logic stays outside the ADK.

## What It Does

- manages conversation message state
- runs the `assistant -> tool -> assistant` loop
- exposes one provider factory for different vendors
- supports non-streaming runs
- supports text streaming
- supports run events as an async sequence

## What It Does Not Try To Be

- a workflow engine
- a planner framework
- a plugin platform
- a web host abstraction

## Install / Reference

This repo currently uses project reference style.

Add a reference from your app:

```xml
<ItemGroup>
  <ProjectReference Include="..\\crystal-adk\\Crystal.Adk.csproj" />
</ItemGroup>
```

Target framework is currently `net6.0`.

## Quick Start

### 1. Create a provider

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

Supported vendor values in the current codebase:

- `ollama`
- `ark`

### 2. Create a host and session

```csharp
var host = new AgentHost(provider, new AgentHostOptions
{
    SystemPrompt = "You are a concise assistant.",
    MaxRounds = 4
});

var session = host.CreateSession();
```

### 3. Run in non-streaming mode

```csharp
var result = await session.RunAsync("Query device EQ-001");

Console.WriteLine(result.FinalMessage);
```

### 4. Stream text

```csharp
await foreach (var chunk in session.StreamTextAsync("Explain device history analysis"))
{
    Console.Write(chunk.Text);
}
```

### 5. Consume run events

```csharp
await foreach (var evt in session.StreamEventsAsync("Query device EQ-001 and summarize status"))
{
    Console.WriteLine(evt.Kind);
}
```

Current event kinds:

- `run_started`
- `tool_call_started`
- `tool_call_completed`
- `final_answer`
- `run_completed`
- `run_failed`

Note: in `v0.1.0`, `StreamEventsAsync(...)` is not provider realtime chunk streaming. It is a normalized async event sequence produced during a run.

## Tool Example

```csharp
using Crystal.Adk.Core;

public sealed class QueryDeviceInfoTool : IAgentTool
{
    public AgentToolDescriptor Descriptor { get; } = new()
    {
        Name = "query_device_info",
        Description = "Query basic device info and status.",
        ParametersSchema = new
        {
            type = "object",
            properties = new
            {
                deviceCode = new { type = "string", description = "Device code, e.g. EQ-001" }
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

Register it when creating the host:

```csharp
var toolHub = new ToolHub();
toolHub.Register(new QueryDeviceInfoTool());

var host = new AgentHost(toolHub, provider, new AgentHostOptions
{
    SystemPrompt = "Use tools when structured data is needed.",
    MaxRounds = 6
});
```

## How To Run The Lab Project

The workspace includes `adk-lab/` as a playground for examples.

Run examples:

```powershell
dotnet run --project .\adk-lab\adk-lab.csproj -- 01
dotnet run --project .\adk-lab\adk-lab.csproj -- 02
dotnet run --project .\adk-lab\adk-lab.csproj -- 03
dotnet run --project .\adk-lab\adk-lab.csproj -- 04
dotnet run --project .\adk-lab\adk-lab.csproj -- 05
dotnet run --project .\adk-lab\adk-lab.csproj -- 06
```

Examples currently cover:

- `01`: Ollama non-streaming
- `02`: Ollama text streaming
- `03`: Ollama tool run events
- `04`: ARK non-streaming
- `05`: ARK text streaming
- `06`: ARK tool run events

## Repository Layout

```text
crystal-adk/
├─ Core/
├─ Providers/
├─ Session/
├─ DOCS/
└─ Crystal.Adk.csproj
```

## Docs

- [DOCS/版本设计/v0.1.0/代码设计.md](./DOCS/%E7%89%88%E6%9C%AC%E8%AE%BE%E8%AE%A1/v0.1.0/%E4%BB%A3%E7%A0%81%E8%AE%BE%E8%AE%A1.md)
- [DOCS/版本设计/v0.1.0/特性清单.md](./DOCS/%E7%89%88%E6%9C%AC%E8%AE%BE%E8%AE%A1/v0.1.0/%E7%89%B9%E6%80%A7%E6%B8%85%E5%8D%95.md)

## Current Scope

Current `v0.1.0` direction:

- `RunAsync(...)`
- `StreamTextAsync(...)`
- `StreamEventsAsync(...)`
- in-memory session state
- function calling
- provider compatibility focused on ARK and Ollama

Not planned for `v0.1.0`:

- workflow / planner abstractions
- plugin system
- persistent session store
- context compression / auto-summary
