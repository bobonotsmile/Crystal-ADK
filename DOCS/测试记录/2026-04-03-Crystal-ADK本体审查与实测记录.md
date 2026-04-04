# Crystal-ADK 本体审查与实测记录

日期：2026-04-03

测试入口：`adk-test/adk-lab.csproj`

说明：

- 本文重点是 `Crystal-ADK` 本体
- 结论来自两部分：
  - 真实 `ARK` 云端调用验证
  - `Crystal-ADK` 核心源码审查

本次修复范围记录：

- [x] 修正流式超时未覆盖完整读取阶段的问题
- [x] 修正 `AgentSession` 失败时污染历史的问题
- [x] 修正 `ARK EnableThinking = null` 语义与文档不一致的问题
- [x] 修正 `ChatProviderFactory` 对 `Vendor` 大小写和空白过于脆弱的问题


## 一、实测结论

基于真实 `ARK` 配置，当前版本已经验证通过：

- 非流式调用可用
- 流式调用可用
- `thinking` 关闭场景可正常返回正文
- `Session -> Provider -> ARK` 主链路基本可工作

因此，这一版 `Crystal-ADK` 不是“不能用”，而是“最小链路能跑通，但本体内部有几处明显的语义和设计问题”，其中有些已经会影响异常场景和长连接场景。

## 二、核心问题

以下问题按严重度排序。

### P1. 流式超时并没有真正覆盖整个流读取阶段

代码位置：

- [ArkChatProvider.cs](e:/Documents/code/ADK/Crystal-ADK/Providers/Models/Ark/ArkChatProvider.cs#L45)
- [OllamaChatProvider.cs](e:/Documents/code/ADK/Crystal-ADK/Providers/Models/Ollama/OllamaChatProvider.cs#L45)

问题描述：

- `StreamAsync` 中先创建了带 `TimeoutMs` 的 `cts`
- `SendAsync` 和 `ReadAsStreamAsync` 用的是 `cts.Token`
- 但真正进入流读取时，传给 `ReadStreamAsync` 的却是外部的 `cancellationToken`，不是 `cts.Token`

结果：

- `TimeoutMs` 只基本覆盖到“发请求和拿到流”
- 一旦进入逐行读取阶段，如果服务端卡住、长时间不结束、或者中间挂起，库内部超时可能不再生效

影响：

- 流式请求可能无限挂住
- `TimeoutMs` 语义不完整
- 在生产场景中，这类问题会直接变成资源占用和线程等待问题

建议：

- `ReadStreamAsync` 应统一接收 `cts.Token`
- 同时需要明确“连接超时”和“流读取超时”是否共用同一个超时模型

处理状态：

- 已修复
- 当前实现改为在逐行读取阶段使用统一取消 token，并对 `ReadLineAsync()` 补上取消等待

### P1. Session 在请求成功前就写入历史，失败时会污染会话状态

代码位置：

- [AgentSession.cs](e:/Documents/code/ADK/Crystal-ADK/Session/AgentSession.cs#L24)
- [AgentSession.cs](e:/Documents/code/ADK/Crystal-ADK/Session/AgentSession.cs#L36)

问题描述：

- `RunAsync` 一开始就 `AddUser(userInput)`
- `StreamTextAsync` 一开始也先 `AddUser(userInput)`
- 只有 provider 成功返回后，才补写 assistant

结果：

- 一旦 provider 调用失败，`user` 消息已经进历史，但这一轮 assistant 不存在
- 如果流式过程中中断，已经输出给调用方的部分文本也不会写回历史

影响：

- 会话历史和真实执行结果不一致
- 重试时会把失败轮次的 `user` 也带进去，污染上下文
- 调用方很难区分“这条 user 是成功轮次的一部分，还是失败残留”

建议：

- 改成“先构建待发送消息，成功后再提交历史”
- 或者提供事务式会话提交机制
- 至少要明确失败时是否自动回滚 `user`

处理状态：

- 已修复
- 当前实现改为先拼装请求消息，只有 provider 成功完成后才写入 `user` 与 `assistant` 历史

### P1. `EnableThinking = null` 的语义与文档不一致

代码位置：

- [ChatProviderOptions.cs](e:/Documents/code/ADK/Crystal-ADK/Providers/ChatProviderOptions.cs#L3)
- [ArkChatProvider.cs](e:/Documents/code/ADK/Crystal-ADK/Providers/Models/Ark/ArkChatProvider.cs#L91)

问题描述：

- 设计文档写的是：`EnableThinking` 为空，表示“不主动表达该配置”
- 但 `ArkChatProvider.BuildRequestBody` 无论是否为空，都会发送：
  - `thinking.type = enabled` 或
  - `thinking.type = disabled`
- 也就是说 `null` 实际被压成了 `disabled`

影响：

- 配置语义失真
- 文档与实现不一致
- 使用者以为“交给模型默认行为”，实际却变成“强制关闭 thinking”

建议：

- `EnableThinking == null` 时不要下发 `thinking` 字段
- 只有 `true/false` 时才显式发送

处理状态：

- 已修复
- 当前 `ARK` provider 仅在 `EnableThinking.HasValue` 时下发 `thinking`

### P2. Provider 工厂对 `Vendor` 大小写和空白非常脆弱

代码位置：

- [ChatProviderFactory.cs](e:/Documents/code/ADK/Crystal-ADK/Providers/ChatProviderFactory.cs#L8)

问题描述：

- 当前直接对 `options.Vendor` 做字面量匹配
- `"ark"` 可以
- `"ARK"`、`"Ark"`、`" ark "` 都不行

影响：

- 很容易因为配置格式问题抛异常
- 这是低价值失败，不应该让调用方承担

建议：

- 在工厂入口先做 `Trim()` 和 `ToLowerInvariant()`
- 把配置容错放到边界层消化掉

处理状态：

- 已修复
- 当前工厂入口会先对 `Vendor` 做空白检查与规范化

## 三、设计上的不足

这些不一定是当前版本的 bug，但属于本体设计上已经能看出的后续瓶颈。

### 1. `RuntimeMessage` 过于弱类型，后续很难自然承接 FC / Tool 调用

代码位置：

- [RuntimeMessage.cs](e:/Documents/code/ADK/Crystal-ADK/Abstractions/RuntimeMessage.cs#L3)

当前只有：

- `Role`
- `Content`
- `ThinkingContent`

问题：

- `Role` 是裸字符串，没有约束
- 没有 `tool_calls`
- 没有 `tool_call_id`
- 没有工具结果承载结构

影响：

- 当前模型只适合“纯文本会话”
- 文档里已经研究到 FC 链路，但本体抽象还没为那一步做准备

建议：

- 如果后续明确要走 agent/tool 方向，应尽早决定统一消息模型是否扩展
- 否则越往后补，兼容成本越高

### 2. `SessionMessageManager` 更像历史容器，不是真正的会话状态策略层

代码位置：

- [SessionMessageManager.cs](e:/Documents/code/ADK/Crystal-ADK/Session/SessionMessageManager.cs#L5)

当前有：

- 添加
- 修改
- 删除
- 导出

但没有：

- 上下文裁剪
- token 预算
- system 合并策略
- 流失败后的状态恢复策略

影响：

- 作为最小库没问题
- 但名字叫 `SessionMessageManager`，容易让人误以为它已经承担“会话治理”

建议：

- 文档里应进一步强调：它只是消息存储与编辑器，不是上下文策略层

### 3. Provider 的错误模型仍偏底层，缺少统一异常语义

代码位置：

- [ArkChatProvider.cs](e:/Documents/code/ADK/Crystal-ADK/Providers/Models/Ark/ArkChatProvider.cs#L31)
- [OllamaChatProvider.cs](e:/Documents/code/ADK/Crystal-ADK/Providers/Models/Ollama/OllamaChatProvider.cs#L31)

当前做法：

- 直接抛 `InvalidOperationException`
- 或保留底层 `HttpRequestException`

问题：

- 调用方知道“失败了”，但不知道失败属于哪一类
- 例如：
  - 配置错误
  - 认证错误
  - 网络错误
  - 协议解析错误
  - 服务端错误

建议：

- 后续可考虑定义统一 provider 异常模型
- 至少做分类包装，方便业务层定向处理

### 4. 流式解析器实现偏“最小可跑”，健壮性一般

代码位置：

- [ArkChatProvider.cs](e:/Documents/code/ADK/Crystal-ADK/Providers/Models/Ark/ArkChatProvider.cs#L148)
- [OllamaChatProvider.cs](e:/Documents/code/ADK/Crystal-ADK/Providers/Models/Ollama/OllamaChatProvider.cs#L147)

问题：

- `ARK` 解析器按单行 `data:` 事件处理，属于偏简化版 SSE 解析
- `ReadLineAsync()` 本身也不是 token-aware 的精细流控方案
- 整体更偏 demo 级实现，而不是高鲁棒性流解析器

影响：

- 面对更复杂的 SSE 事件格式时可扩展性一般
- 异常流、慢流、代理层改写流时更容易暴露问题

建议：

- 如果未来要把 streaming 当核心卖点，建议单独抽离流解析层

## 四、当前本体的整体评价

### 优点

- 结构清晰
- 分层比较克制
- `Session / Provider / Abstractions` 边界明确
- 最小可用链路已经打通
- `thinking` 已经进入统一模型，这一步是对的

### 不足

- 异常与失败场景的状态一致性考虑不够
- streaming 的超时和解析鲁棒性还不够扎实
- 配置与抽象模型还偏“文本聊天 SDK”，距离更完整的 ADK 还有一段距离

## 五、建议的下一步优先级

建议按下面顺序处理：

1. 修正流式超时覆盖范围
2. 修正 `AgentSession` 的失败轮次历史污染问题
3. 修正 `ARK EnableThinking = null` 的语义不一致问题
4. 让 `ChatProviderFactory` 做基础规范化
5. 再决定是否扩展统一消息模型以承接 FC / Tool Calls

## 六、结论

当前 `Crystal-ADK` 本体的状态可以概括为：

- 主链路可用
- 结构方向是对的
- 但还停留在“最小可用内核”

如果只是拿来做文本对话实验，这一版已经够用。

如果目标是继续往真正的 ADK 演进，那么上面列出的 P1 问题应该尽快处理，否则后续会在 streaming、重试和工具调用阶段持续放大。
