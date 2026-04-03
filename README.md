# Crystal.Adk V0.2.0

`Crystal.Adk` 是一个面向 C# 的轻量类库项目，用来承载最小可用的对话运行时抽象。

当前这版库聚焦三件事：

- 统一消息模型，屏蔽不同厂商聊天接口的消息格式差异
- 提供可替换的 Provider 抽象，分别负责非流式与流式发送/接收
- 提供基于 Session 的会话操作入口，以及独立的消息历史操作入口

这不是脚手架项目，也不负责替开发者读取配置文件、环境变量或自动补系统提示词。初始化参数、起始消息、上下文裁剪策略都由接入方自己决定。

## 当前库内包含的部分

- `Abstractions/`
  统一消息结构与流式文本结构
- `Providers/`
  Provider 抽象、Provider 工厂、厂商实现与发送解析逻辑
- `Session/`
  会话操作入口与消息历史操作入口
- `DOCS/`
  架构说明、版本设计稿、厂商协议资料

## 当前能力边界

当前已提供：

- 非流式对话调用
- 文本流式对话调用
- 会话历史追加、编辑、导出
- ARK Provider
- Ollama Provider

当前暂未提供：

- 自动工具循环
- 统一事件流运行时
- 内置配置加载器
- 内置系统提示词策略

## 目前支持的模型厂商

- `ARK / 火山引擎`
  - Vendor 值：`ark`
  - Provider 实现：`Providers/Models/Ark/ArkChatProvider.cs`
  - 当前支持：非流式对话、文本流式对话

- `Ollama`
  - Vendor 值：`ollama`
  - Provider 实现：`Providers/Models/Ollama/OllamaChatProvider.cs`
  - 当前支持：非流式对话、文本流式对话


## 项目配套

库项目：

- 当前目录：`crystal-adk/`

测试/实验项目：

- `adk-lab/`
- 测试项目链接：`https://github.com/bobonotsmile/adk-test.git`

## 引用方式

项目内引用时，同目录根同级下可直接引用：

```xml
<ProjectReference Include="..\\crystal-adk\\Crystal.Adk.csproj" />
```

最小使用示例如下：

```csharp
using Crystal.Adk.Abstractions;
using Crystal.Adk.Providers;
using Crystal.Adk.Session;

var options = new ChatProviderOptions
{
    Vendor = "ark",
    ApiKey = "...",
    Model = "...",
    BaseUrl = "https://ark.cn-beijing.volces.com/api/v3/chat/completions"
};

using var httpClient = new HttpClient();
var provider = ChatProviderFactory.Create(httpClient, options);

var history = new SessionMessageManager(new[]
{
    new RuntimeMessage { Role = "system", Content = "你是一个简洁的助手。" }
});

var session = new AgentSession(provider, history);
var assistant = await session.RunAsync("你好");
```

## 文档

- 架构说明（包含架构设计、函数职责、数据结构、代码使用体验）：`DOCS/crystal-adk 架构说明/`
- 厂商协议资料：`DOCS/厂商ChatAPI协议/`
