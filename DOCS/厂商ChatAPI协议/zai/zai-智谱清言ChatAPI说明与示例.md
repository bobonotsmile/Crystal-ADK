
---

# 智谱清言（Z.AI / BigModel）ChatAPI 说明文档（对话补全）

## 1. 配置说明

### 1.1 环境变量

* ZAI_API_KEY（或 BIGMODEL_API_KEY，变量名自定）

  * 含义：鉴权 Key
  * 使用位置：HTTP Header `Authorization: Bearer <ZAI_API_KEY>`
* ZAI_URL

  * 含义：Chat Completions 目标服务 URL
  * 推荐值：`https://open.bigmodel.cn/api/paas/v4/chat/completions` 

### 1.2 固定请求信息

* Method：POST
* Content-Type：application/json
* Authorization：Bearer Token
* Base URL：`https://open.bigmodel.cn/api/` 

---

## 2. 请求参数

### 2.1 请求地址

* POST `https://open.bigmodel.cn/api/paas/v4/chat/completions` 

### 2.2 Header

```http
Content-Type: application/json
Authorization: Bearer <ZAI_API_KEY>
```

### 2.3 Body（请求体）字段说明（文本模型为主）

> 说明：该接口按请求类型分为 **文本模型 / 视觉模型 / 音频模型 / 角色模型** 四类请求体结构（oneOf）。常用的是“文本模型”（ChatCompletionTextRequest）。([智谱AI开放文档][1])

#### 2.3.1 model（必选）

* 类型：string
* 含义：调用的模型代码（模型名称）
* 示例：`glm-4.7`
* 备注：不同请求体支持的 model 枚举不同（文本/视觉/音频/角色各自枚举）

#### 2.3.2 messages（必选）

* 类型：array
* 含义：消息列表，提供完整上下文（多轮对话历史）
* 支持角色：

  * 文本模型：`system / user / assistant / tool`
  * 视觉/音频：`system / user / assistant`（工具字段依模型能力而定）
* 约束：

  * 至少 1 条消息
  * 不能只包含 system 或只包含 assistant（需有 user）

#### 2.3.3 stream（可选，默认 false）

* 类型：boolean
* 含义：是否启用 SSE 流式输出
* 取值：

  * false：一次性返回完整结果（application/json）
  * true：按 SSE 分块返回（text/event-stream），结束为 `data: [DONE]` ([智谱AI开放文档][1])

#### 2.3.4 thinking（可选）

* 类型：object
* 含义：控制模型是否开启“思考/推理模式”
* 字段：

  * thinking.type：`enabled / disabled`
  * thinking.clear_thinking：boolean（默认 true）

    * true：历史 turns 中的 reasoning_content 会被忽略/移除，只保留可见文本与工具信息作为上下文
    * false：保留历史 turns 的 reasoning_content（需要在 messages 中原样透传）

#### 2.3.5 do_sample（可选，默认 true）

* 类型：boolean
* 含义：是否启用采样生成

  * true：受 temperature / top_p 等影响，输出更发散
  * false：更确定（temperature/top_p 会被忽略）

#### 2.3.6 temperature（可选）

* 类型：number
* 含义：采样温度（随机性/创造性）
* 范围：`[0.0, 1.0]`（文档口径：限两位小数）
* 建议：通常 **temperature 与 top_p 二选一**做主要调参，不建议同时大幅调整

#### 2.3.7 top_p（可选）

* 类型：number
* 含义：核采样阈值（nucleus sampling）
* 范围：`[0.01, 1.0]`

#### 2.3.8 max_tokens（可选）

* 类型：integer
* 含义：限制模型输出最大 token 数
* 备注：不同模型最大值不同（文档给出 GLM-4.7/4.6 可到 128K 级别的上限配置）([智谱AI开放文档][1])

#### 2.3.9 tool_stream（可选，默认 false，仅部分模型支持）

* 类型：boolean
* 含义：是否开启“工具调用的流式响应”（Function Calls 流式返回）

#### 2.3.10 tools（可选）

* 类型：array
* 含义：工具列表（函数调用 / 知识库检索 / 网络搜索 / MCP）
* 结构分支：

  * function：`{ type:"function", function:{ name, description, parameters } }`
  * retrieval：`{ type:"retrieval", retrieval:{ knowledge_id, prompt_template? } }`
  * web_search：`{ type:"web_search", web_search:{ ... } }`
  * mcp：`{ type:"mcp", mcp:{ server_label, server_url?, ... } }`

#### 2.3.11 tool_choice（可选）

* 类型：string（当前文档示例以 function 为主）
* 取值：`auto`
* 含义：控制模型如何选择工具（当 tools 中包含 function 时常用）

#### 2.3.12 stop（可选）

* 类型：string[]
* 含义：命中停止词则停止生成
* 约束：当前文档口径为最多 1 个停止词（maxItems=1）

#### 2.3.13 response_format（可选，仅文本模型）

* 类型：object
* 含义：指定模型输出格式
* 分支：

  * `{ "type": "text" }`：默认，自然语言文本
  * `{ "type": "json_object" }`：要求模型输出合法 JSON（建议提示词里明确要求）

#### 2.3.14 request_id（可选）

* 类型：string
* 含义：请求唯一标识（建议 UUID）

#### 2.3.15 user_id（可选）

* 类型：string
* 含义：终端用户标识（长度 6~128）

---

### 2.4 messages 结构（对齐 ARK 风格）

#### 2.4.1 system（系统消息）

* role：`"system"`
* content：string

#### 2.4.2 user（用户消息）

* role：`"user"`
* content：

  * 文本模型：string
  * 视觉/音频：string 或 object[]（多模态）

#### 2.4.3 assistant（模型消息）

* role：`"assistant"`
* 可选字段：

  * content：string（文本回答；当返回 tool_calls 时可能为空或为 null）
  * reasoning_content：string（仅部分模型在非流式或某些模式下返回）
  * tool_calls：array（当模型触发工具调用）

#### 2.4.4 tool（工具消息，仅文本模型支持该 role 分支）

* role：`"tool"`
* content：string（工具执行结果，通常放 JSON 字符串）
* tool_call_id：string（与 assistant.tool_calls[].id 对齐）

---

### 2.5 content 多模态结构（messages[].content 为 object[] 时）

#### 2.5.1 文本（type="text"）

```json
{ "type": "text", "text": "xxxxx" }
```

#### 2.5.2 图片（type="image_url"）

```json
{ "type": "image_url", "image_url": { "url": "https://..." } }
```

* url：图片 URL 或 Base64（具体限制以官方说明为准：单张 <=5MB、像素不超过 6000*6000，jpg/png/jpeg 等）([智谱AI开放文档][1])

#### 2.5.3 视频（type="video_url"）

```json
{ "type": "video_url", "video_url": { "url": "https://..." } }
```

* url：视频 URL（大小/时长限制按模型不同）([智谱AI开放文档][1])

#### 2.5.4 文件（type="file_url"）

```json
{ "type": "file_url", "file_url": { "url": "https://..." } }
```

* 说明：部分视觉模型支持文件理解；且与 image_url / video_url 同时传入可能受限（以模型说明为准）([智谱AI开放文档][1])

#### 2.5.5 音频输入（type="input_audio"，音频模型）

```json
{ "type": "input_audio", "input_audio": { "data": "base64_xxx", "format": "wav" } }
```

---

## 3. 响应参数

### 3.1 非流式调用返回（application/json）

#### 3.1.1 顶层字段

* id：任务 ID
* request_id：请求 ID（若请求体提供则回显）
* created：Unix 时间戳（秒）
* model：模型名称
* choices：结果列表
* usage：token 用量统计
* video_result：视频生成结果（如该类能力生效）
* web_search：网页搜索结果信息（当启用 web_search 工具时可能返回）
* content_filter：内容安全信息（若触发）

#### 3.1.2 choices[] 字段

* index：结果索引
* finish_reason：终止原因

  * stop：自然结束或命中 stop
  * tool_calls：触发工具调用
  * length：达到 token 限制
  * sensitive：被安全审核拦截
  * network_error：推理异常
* message：模型消息

  * role：assistant
  * content：回答文本（或 tool_calls 模式下为 null/空）
  * reasoning_content：思维链（仅部分模型）
  * tool_calls：工具调用结构（如触发）

---

### 3.2 流式调用返回（text/event-stream）

* 以 SSE 分块输出，每个 chunk 结构为 ChatCompletionChunk
* 结束标记：`data: [DONE]` ([智谱AI开放文档][1])

chunk 常见字段：

* id / created / model
* choices[0].delta：

  * role：assistant（通常仅首包出现）
  * content：增量文本（逐步拼接成最终回答）
  * reasoning_content：增量思维链（仅部分模型）
  * tool_calls：增量工具调用（工具流式时逐步生成）
* choices[0].finish_reason：最后一个 chunk 可能出现（stop/length/tool_calls/…）
* usage：部分策略下可能返回（多数情况下不返回或为 null）

---

## 4. 调用示例（已补全）

### 4.1 示例一：基础请求（stream=true/false 通用，文本模型）

Header：

```http
Content-Type: application/json
Authorization: Bearer <ZAI_API_KEY>
```

Body（stream=false）：

```json
{
  "model": "glm-4.7",
  "messages": [
    { "role": "system", "content": "一个有用的AI助手。" },
    { "role": "user", "content": "请用三句话解释递归。" }
  ],
  "temperature": 0.7,
  "stream": false
}
```

Body（stream=true）：

```json
{
  "model": "glm-4.7",
  "messages": [
    { "role": "user", "content": "写一首关于春天的短诗。" }
  ],
  "temperature": 1,
  "stream": true
}
```

参考：官方“对话补全/快速开始”示例即为该结构。([智谱AI开放文档][1])

---

### 4.2 示例二：多轮对话（messages 携带历史）

```json
{
  "model": "glm-4.7",
  "messages": [
    { "role": "system", "content": "专业编程助手。" },
    { "role": "user", "content": "什么是递归？" },
    { "role": "assistant", "content": "递归是一种让函数调用自身来解决问题的技术……" },
    { "role": "user", "content": "给一个 Python 递归例子。" }
  ],
  "stream": false
}
```

---

### 4.3 示例三：流式输出响应示例（节选）

SSE chunk（示例格式，逐行读取并拼接 delta.content）：

```json
{"id":"task_xxx","created":1760000000,"model":"glm-4.7","choices":[{"index":0,"delta":{"role":"assistant","content":"我"} }]}
{"id":"task_xxx","created":1760000000,"model":"glm-4.7","choices":[{"index":0,"delta":{"content":"是"} }]}
{"id":"task_xxx","created":1760000000,"model":"glm-4.7","choices":[{"index":0,"delta":{"content":"一"} }]}
{"id":"task_xxx","created":1760000000,"model":"glm-4.7","choices":[{"index":0,"delta":{"content":"个"} }]}
{"id":"task_xxx","created":1760000000,"model":"glm-4.7","choices":[{"index":0,"delta":{"content":"A"} }]}
{"id":"task_xxx","created":1760000000,"model":"glm-4.7","choices":[{"index":0,"delta":{"content":"I"} }]}
{"id":"task_xxx","created":1760000000,"model":"glm-4.7","choices":[{"index":0,"delta":{"content":"助"} }]}
{"id":"task_xxx","created":1760000000,"model":"glm-4.7","choices":[{"index":0,"delta":{"content":"手"} }]}
{"id":"task_xxx","created":1760000000,"model":"glm-4.7","choices":[{"index":0,"delta":{"content":"。"},"finish_reason":"stop"}]}
```

流式结束：

```text
data: [DONE]
```

（结束标记口径见官方说明）([智谱AI开放文档][1])

---

### 4.4 示例四：非流式输出响应示例（stream=false）

```json
{
  "id": "task_xxx",
  "request_id": "req_xxx",
  "created": 1760000000,
  "model": "glm-4.7",
  "choices": [
    {
      "index": 0,
      "finish_reason": "stop",
      "message": {
        "role": "assistant",
        "content": "xxxxx这里是回答xxxxx"
      }
    }
  ],
  "usage": {
    "prompt_tokens": 120,
    "completion_tokens": 40,
    "total_tokens": 160,
    "prompt_tokens_details": { "cached_tokens": 0 }
  }
}
```

---

### 4.5 示例五：流式思考（thinking.enabled + stream=true）

请求：

```json
{
  "model": "glm-4.7",
  "messages": [
    { "role": "user", "content": "比较快速排序与归并排序的时间复杂度与适用场景。" }
  ],
  "thinking": { "type": "enabled" },
  "stream": true
}
```

SSE chunk（节选，思维链在 delta.reasoning_content，答案在 delta.content；字段名以文档为准）：

```json
{"choices":[{"index":0,"delta":{"role":"assistant","reasoning_content":"xxxx"} }],"id":"task_xxx","created":1760000000,"model":"glm-4.7"}
{"choices":[{"index":0,"delta":{"reasoning_content":"xxxx"} }],"id":"task_xxx","created":1760000000,"model":"glm-4.7"}
{"choices":[{"index":0,"delta":{"content":"结论：两者平均复杂度均为 O(n log n) ……"} }],"id":"task_xxx","created":1760000000,"model":"glm-4.7"}
{"choices":[{"index":0,"delta":{"content":"……"},"finish_reason":"stop"}],"id":"task_xxx","created":1760000000,"model":"glm-4.7"}
```

结束：

```text
data: [DONE]
```

---

### 4.6 示例六：Function Calling（tools 定义 + messages 回填链路，单工具）

#### 4.6.1 tools 定义（一个工具）

```json
{
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "get_weather",
        "description": "获取指定城市的天气信息",
        "parameters": {
          "type": "object",
          "properties": {
            "city": { "type": "string", "description": "城市名称" }
          },
          "required": ["city"]
        }
      }
    }
  ],
  "tool_choice": "auto"
}
```

#### 4.6.2 第一次请求（带 tools）

```json
{
  "model": "glm-4.7",
  "messages": [
    { "role": "user", "content": "今天北京天气怎么样？" }
  ],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "get_weather",
        "description": "获取指定城市的天气信息",
        "parameters": {
          "type": "object",
          "properties": {
            "city": { "type": "string", "description": "城市名称" }
          },
          "required": ["city"]
        }
      }
    }
  ],
  "tool_choice": "auto",
  "stream": false
}
```

#### 4.6.3 模型返回 tool_calls（业务侧需要执行工具）

```json
{
  "choices": [
    {
      "finish_reason": "tool_calls",
      "index": 0,
      "message": {
        "role": "assistant",
        "content": "",
        "tool_calls": [
          {
            "id": "call_xxx",
            "type": "function",
            "function": {
              "name": "get_weather",
              "arguments": "{\"city\":\"北京\"}"
            }
          }
        ]
      }
    }
  ]
}
```

#### 4.6.4 回填 tool 结果，再次请求继续对话

messages（把 tool 结果塞回去）：

```json
[
  { "role": "user", "content": "今天北京天气怎么样？" },
  {
    "role": "assistant",
    "tool_calls": [
      {
        "id": "call_xxx",
        "type": "function",
        "function": { "name": "get_weather", "arguments": "{\"city\":\"北京\"}" }
      }
    ]
  },
  {
    "role": "tool",
    "tool_call_id": "call_xxx",
    "content": "{\"city\":\"北京\",\"temp\":-2,\"text\":\"晴\",\"wind\":\"西北风\"}"
  }
]
```

第二次请求：

```json
{
  "model": "glm-4.7",
  "messages": [
    { "role": "user", "content": "今天北京天气怎么样？" },
    {
      "role": "assistant",
      "tool_calls": [
        {
          "id": "call_xxx",
          "type": "function",
          "function": { "name": "get_weather", "arguments": "{\"city\":\"北京\"}" }
        }
      ]
    },
    {
      "role": "tool",
      "tool_call_id": "call_xxx",
      "content": "{\"city\":\"北京\",\"temp\":-2,\"text\":\"晴\",\"wind\":\"西北风\"}"
    }
  ],
  "stream": false
}
```

最终模型回复（finish_reason=stop）：

```json
{
  "choices": [
    {
      "finish_reason": "stop",
      "index": 0,
      "message": {
        "role": "assistant",
        "content": "北京今天晴，气温约 -2℃，西北风。"
      }
    }
  ]
}
```

---

### 4.7 示例七：多模态（图片理解 / 视频理解 / 文件理解 / 音频对话）

#### 4.7.1 图片理解（视觉模型）

```json
{
  "model": "glm-4.6v",
  "messages": [
    {
      "role": "user",
      "content": [
        { "type": "image_url", "image_url": { "url": "https://cdn.bigmodel.cn/static/logo/register.png" } },
        { "type": "image_url", "image_url": { "url": "https://cdn.bigmodel.cn/static/logo/api-key.png" } },
        { "type": "text", "text": "图片在讲什么？" }
      ]
    }
  ],
  "stream": false
}
```

（该类示例在官方对话补全文档中有对应演示）([智谱AI开放文档][1])

#### 4.7.2 视频理解（视觉模型）

```json
{
  "model": "glm-4.6v",
  "messages": [
    {
      "role": "user",
      "content": [
        { "type": "video_url", "video_url": { "url": "https://cdn.bigmodel.cn/agent-demos/lark/113123.mov" } },
        { "type": "text", "text": "视频在展示什么？" }
      ]
    }
  ],
  "stream": false
}
```

([智谱AI开放文档][1])

#### 4.7.3 文件理解（视觉模型）

```json
{
  "model": "glm-4.6v",
  "messages": [
    {
      "role": "user",
      "content": [
        { "type": "file_url", "file_url": { "url": "https://cdn.bigmodel.cn/static/demo/demo2.txt" } },
        { "type": "file_url", "file_url": { "url": "https://cdn.bigmodel.cn/static/demo/demo1.pdf" } },
        { "type": "text", "text": "文件在讲什么？提炼3条要点。" }
      ]
    }
  ],
  "stream": false
}
```

([智谱AI开放文档][1])

#### 4.7.4 音频对话（音频模型）

```json
{
  "model": "glm-4-voice",
  "messages": [
    {
      "role": "user",
      "content": [
        { "type": "text", "text": "你好，这是语音输入测试，请慢速复述一遍。" },
        { "type": "input_audio", "input_audio": { "data": "base64_voice_xxx", "format": "wav" } }
      ]
    }
  ],
  "stream": true
}
```

([智谱AI开放文档][1])

---

## 5. 工具调用链路关键规则（对齐 ARK 文档口径）

* assistant 返回 tool_calls 后：业务侧执行工具，并回填一条 `role="tool"` 的消息
* tool 消息必须带 `tool_call_id`，且与对应的 `assistant.tool_calls[].id` 完全一致
* `function.arguments` 为 JSON 字符串；可能出现：

  * 非严格 JSON（需要业务侧做解析与校验）
  * 虚构字段/缺字段（需要业务侧做输入验证与兜底）
* 当模型再次返回的 choices[].message 不再包含 tool_calls，可视为本次工具链路完成

---
