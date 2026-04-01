
# 字节跳动火山引擎（Volcengine ARK）ChatAPI 说明文档

## 1. 火山引擎配置说明

### 1.1 环境变量
- ARK_API_KEY
  - 含义：鉴权 Key
  - 使用位置：HTTP Header `Authorization: Bearer <ARK_API_KEY>`
- ARK_URL
  - 含义：Chat Completions 目标服务 URL
  - 推荐值：`https://ark.cn-beijing.volces.com/api/v3/chat/completions`

### 1.2 固定请求信息
- Method：POST
- Content-Type：application/json
- Authorization：Bearer Token

---

## 2. 请求参数

### 2.1 请求地址
- POST `https://ark.cn-beijing.volces.com/api/v3/chat/completions`

### 2.2 Header
```http
Content-Type: application/json
Authorization: Bearer <ARK_API_KEY>
````

### 2.3 Body（请求体）字段说明

#### 2.3.1 model（必选）

* 类型：string
* 含义：调用的模型 ID（Model ID）或 Endpoint ID
* 备注：

  * 多应用及精细管理场景推荐使用 Endpoint ID 调用
  * Model ID / Endpoint ID 获取方式以官方控制台与文档为准

#### 2.3.2 messages（必选）

* 类型：object[]
* 含义：消息列表，用于对话上下文与多轮记忆
* 说明：

  * 不同模型支持不同模态（文本/图片/视频）
  * role 支持 system/user/assistant/tool
* 详见：2.4 messages 结构

#### 2.3.3 thinking（可选，默认值 {"type":"enabled"}）

* 类型：object
* 含义：控制模型是否开启深度思考模式
* 字段：

  * thinking.type（必选）

    * 类型：string
    * 取值：enabled / disabled / auto
    * enabled：开启思考模式，模型先思考后回答
    * disabled：关闭思考，直接回答
    * auto：自动思考，模型自行判断是否需要思考
* 备注：

  * 部分模型支持输出 reasoning_content（思维链内容），详见响应字段

#### 2.3.4 stream（可选，默认值 false）

* 类型：boolean / null
* 含义：响应是否按 SSE 流式返回
* 取值：

  * false：模型生成完毕后一次性返回（object=chat.completion）
  * true：按 SSE 逐块返回，并以 `data: [DONE]` 结束（object=chat.completion.chunk）
* 备注：

  * stream=true 时可配合 stream_options 获取 token 用量

#### 2.3.5 stream_options（可选，默认值 null，仅 stream=true 时有效）

* 类型：object / null
* 字段：

  * stream_options.include_usage（可选，默认值 false）

    * 类型：boolean / null
    * true：在 `data: [DONE]` 之前额外返回一个 chunk

      * 该 chunk 的 choices 为空数组
      * usage 字段包含整次请求的 token 用量
    * false：结束前不额外返回整次 usage chunk
  * stream_options.chunk_include_usage（可选，默认值 false）

    * 类型：boolean / null
    * true：每个 chunk 的 usage 字段返回截至当前 chunk 的累计 token 用量
    * false：每个 chunk 通常不返回 usage（usage 多为 null）

#### 2.3.6 max_tokens（可选，默认值 4096）

* 类型：integer / null
* 含义：限制模型“回答内容”的最大长度（单位 token）
* 关键说明：

  * 模型回答不包含思维链内容（若模型输出 reasoning_content）
  * 输出 token 总长度还受上下文窗口限制（输入 + 思维链 + 回答）
* 取值范围：不同模型不同，以模型列表为准

#### 2.3.7 max_completion_tokens（可选）

* 类型：integer / null
* 取值范围：[0, 65536]
* 含义：限制模型输出最大长度（包含思维链 + 回答，单位 token）
* 关键约束：

  * 不可与 max_tokens 同时设置
  * 设置该字段后 max_tokens 默认值失效
  * 模型按需输出（思维链与回答），直到达到 max_completion_tokens 或上下文限制

#### 2.3.8 service_tier（可选，默认值 auto）

* 类型：string / null
* 取值：auto / default
* 含义：控制是否使用 TPM 保障包额度
* 解释：

  * auto：优先使用 TPM 保障包额度（若有），提升服务等级（响应速度、可用性）
  * default：不使用 TPM 保障包，维持默认服务等级

#### 2.3.9 stop（可选，默认值 null）

* 类型：string / string[] / null
* 含义：命中 stop 字符串时停止生成，该字符串本身不会输出
* 约束：最多支持 4 个字符串
* 备注：深度思考能力模型可能不支持该字段（以官方模型说明为准）

#### 2.3.10 reasoning_effort（可选，默认值 medium）

* 类型：string / null
* 取值：minimal / low / medium / high
* 含义：限制思考工作量，降低思考深度可提升速度并减少思考 token 花费
* 解释：

  * minimal：接近关闭思考，偏直接回答
  * low：轻量思考，侧重快速响应
  * medium：均衡模式，兼顾速度与深度
  * high：深度分析，适合复杂问题，通常更慢且 token 花费更高
* 备注：与 thinking.type 的关系与支持情况以官方文档为准

#### 2.3.11 response_format（可选，默认值 {"type":"text"}，beta）

* 类型：object
* 含义：指定模型回答格式
* 类型分支：

  1. 文本格式

     * response_format.type = "text"
  2. JSON Object 格式（beta）

     * response_format.type = "json_object"
     * 含义：模型输出以 JSON 对象结构组织
     * 备注：仅部分模型支持；beta 阶段生产谨慎
  3. JSON Schema 格式（beta）

     * response_format.type = "json_schema"
     * response_format.json_schema（必选）

       * name：string（必选）
       * description：string / null（可选）
       * schema：object（必选，JSON Schema 对象）
       * strict：boolean / null（默认 false）

         * true：生成输出时始终严格遵循 schema
         * false：尽可能遵循 schema（不保证完全严格）

#### 2.3.12 frequency_penalty（可选，默认值 0）

* 类型：float / null
* 取值范围：[-2.0, 2.0]
* 含义：频率惩罚系数
* 解释：

  * 值为正：根据新 token 在文本中的出现频率惩罚，降低逐字重复概率
  * 值为负：可能提升重复倾向（一般不建议）

#### 2.3.13 presence_penalty（可选，默认值 0）

* 类型：float / null
* 取值范围：[-2.0, 2.0]
* 含义：存在惩罚系数
* 解释：

  * 值为正：根据新 token 是否已出现进行惩罚，增加谈论新主题可能性
  * 值为负：可能更倾向围绕已有内容展开

#### 2.3.14 temperature（可选，默认值 1）

* 类型：float / null
* 取值范围：[0, 2]
* 含义：采样温度，控制候选词概率分布平滑程度
* 更细说明：

  * 0：近似只选择对数概率最大的 token（输出更确定）
  * 0.1~0.3：偏确定、适合结构化与稳定输出
  * 0.4~0.8：更自然但相对可控
  * 0.9~2.0：随机性更强，发散更明显
* 调参建议：

  * 通常仅调整 temperature 或 top_p 其中之一，不建议两者同时大幅调整

#### 2.3.15 top_p（可选，默认值 0.7）

* 类型：float / null
* 取值范围：[0, 1]
* 含义：核采样概率阈值，模型仅在累计概率质量位于 top_p 的候选 token 集合内采样
* 更细说明：

  * top_p 越小：候选集合越小，输出越确定
  * top_p 越大：候选集合越大，输出越随机
* 调参建议：

  * 通常仅调整 temperature 或 top_p 其中之一

#### 2.3.16 logprobs（可选，默认值 false）

* 类型：boolean / null
* 含义：是否返回输出 tokens 的对数概率信息
* 备注：深度思考能力模型可能不支持该字段

#### 2.3.17 top_logprobs（可选，默认值 0）

* 类型：integer / null
* 取值范围：[0, 20]
* 含义：指定每个输出 token 位置最可能返回的 token 数量
* 约束：仅当 logprobs=true 时可设置；深度思考能力模型可能不支持

#### 2.3.18 logit_bias（可选，默认值 null）

* 类型：map / null
* 含义：调整指定 token 出现概率（更符合特定偏好）
* 结构：

  * key：词表 token ID（需通过 tokenization 接口获取）
  * value：偏差值，范围 [-100, 100]

    * -1：降低选择可能性
    * 1：增加选择可能性
    * -100：完全禁止选择该 token
    * 100：极强偏向，仅可选择该 token
* 备注：深度思考能力模型可能不支持该字段

#### 2.3.19 tools（可选，默认值 null）

* 类型：object[] / null
* 含义：待调用工具列表，用于 Function Calling
* 备注：支持情况以模型列表为准
* 详见：2.6 tools 结构

#### 2.3.20 parallel_tool_calls（可选，默认值 true）

* 类型：boolean
* 含义：本次请求模型返回是否允许包含多个待调用工具
* 解释：

  * true：允许一次返回多个 tool_calls
  * false：允许返回的 tool_calls 数量小于等于 1（仅部分模型/版本生效）

#### 2.3.21 tool_choice（可选）

* 类型：string / object
* 含义：控制模型返回信息中是否包含待调用工具，以及允许的工具范围
* 字符串取值：

  * none：返回中不可包含 tool_calls
  * auto：模型自行判断是否调用工具（有 tools 时通常为默认）
  * required：返回中必须包含 tool_calls（需确保存在合适工具，减少幻觉）
* 对象形式：指定可调用的具体工具

  * tool_choice.type：string（必须为 "function"）
  * tool_choice.function：object

    * name：string（必选，工具名称）

---

### 2.4 messages 结构

#### 2.4.1 system（系统消息）

* messages[].role：string（必选），必须为 "system"
* messages[].content：string / object[]（必选）

  * string：纯文本系统提示词
  * object[]：多模态内容，详见 2.5

#### 2.4.2 user（用户消息）

* messages[].role：string（必选），必须为 "user"
* messages[].content：string / object[]（必选）

  * string：纯文本用户输入
  * object[]：多模态内容，详见 2.5

#### 2.4.3 assistant（模型消息）

提示：messages[].content 与 messages[].tool_calls 两者至少填写其一

* messages[].role：string（必选），必须为 "assistant"
* messages[].content：string / array（可选）
* messages[].reasoning_content：string（可选，仅部分模型支持）
* messages[].tool_calls：object[]（可选）

  * tool_calls[].function：object（必选）

    * tool_calls[].function.name：string（必选）
    * tool_calls[].function.arguments：string（必选，JSON 字符串；可能非严格 JSON）
  * tool_calls[].id：string（必选）
  * tool_calls[].type：string（必选，仅支持 "function"）

#### 2.4.4 tool（工具消息）

* messages[].role：string（必选），必须为 "tool"
* messages[].content：string / array（必选）
* messages[].tool_call_id：string（必选）

  * 含义：与 assistant.tool_calls[].id 对应，用于关联请求工具与工具返回

---

### 2.5 content 多模态结构（messages[].content 为 object[] 时）

#### 2.5.1 文本部分（type="text"）

* messages[].content[i].type：string（必选），必须为 "text"
* messages[].content[i].text：string（必选）

#### 2.5.2 图片部分（type="image_url"）

* messages[].content[i].type：string（必选），必须为 "image_url"
* messages[].content[i].image_url：object（必选）

  * url：string（必选）

    * 支持：图片链接 / 图片 Base64
  * detail：string（默认值 low）

    * high：高细节模式，更细节更耗时
    * low：低细节模式，更快更省
  * image_pixel_limit：object / null（默认值 null）

    * max_pixels：integer
    * min_pixels：integer
    * 规则：

      * image_pixel_limit 优先级高于 detail
      * 像素不在平台允许范围可能直接报错

#### 2.5.3 视频部分（type="video_url"）

* 备注：不支持理解视频中的音频内容
* messages[].content[i].type：string（必选），必须为 "video_url"
* messages[].content[i].video_url：object（必选）

  * url：string（必选）

    * 支持：视频链接 / 视频 Base64
  * fps：float / null（默认值 1，取值范围 [0.2, 5]）

    * fps 越高：对画面变化更敏感，但 token 花费更高
    * fps 越低：更快更省，但对变化不敏感

---

### 2.6 tools 结构（Function Calling 工具定义）

#### 2.6.1 tools[] 顶层

* tools[].type：string（必选），必须为 "function"
* tools[].function：object（必选）

  * name：string（必选）
  * description：string（可选）
  * parameters：object（可选，JSON Schema 对象，建议提供）

    * 说明：

      * 字段名大小写敏感
      * parameters 必须是合规 JSON Schema
      * 建议使用英文字段名，中文写在 description

#### 2.6.2 parameters JSON Schema 结构示例

```json
{
  "type": "object",
  "properties": {
    "paramXXXX": {
      "type": "string",
      "description": "参数说明"
    }
  },
  "required": ["paramXXXX"]
}
```

---

## 3. 响应参数

### 3.1 非流式调用返回（object = "chat.completion"）

#### 3.1.1 顶层字段

* id：string，本次请求唯一标识
* model：string，本次请求实际使用的模型名称和版本
* service_tier：string，本次请求是否使用 TPM 保障包

  * scale：使用 TPM 保障包额度
  * default：未使用 TPM 保障包额度
* created：integer，Unix 时间戳（秒）
* object：string，固定为 "chat.completion"
* choices：object[]，模型输出内容
* usage：object，本次请求 token 用量

#### 3.1.2 choices[] 字段

* index：integer，元素索引
* finish_reason：string，停止生成原因

  * stop：自然结束或命中 stop
  * length：达到输出限制

    * 触发 max_tokens
    * 触发 max_completion_tokens
    * 触发 context_window（输入+输出+思维链的上下文限制）
  * content_filter：内容审核拦截
  * tool_calls：模型调用了工具
* message：object，输出内容（assistant）

  * role：string（assistant）
  * content：string（回答）
  * reasoning_content：string / null（思维链，部分模型支持）
  * tool_calls：object[] / null（工具调用）
* logprobs：object / null（对数概率信息）
* moderation_hit_type：string / null（风险分类标签，特定模型/护栏配置才会返回）

#### 3.1.3 usage 字段

* total_tokens：integer，总 token（输入 + 输出）
* prompt_tokens：integer，输入 token
* prompt_tokens_details：object

  * cached_tokens：integer（应为 0）
* completion_tokens：integer，输出 token
* completion_tokens_details：object

  * reasoning_tokens：integer（思维链 token，支持输出思维链的模型才有意义）

---

### 3.2 流式调用返回（object = "chat.completion.chunk"）

#### 3.2.1 顶层字段

* id：string
* model：string
* service_tier：string
* created：integer
* object：string，固定为 "chat.completion.chunk"
* choices：object[]
* usage：object / null

  * 默认不统计 token 用量，通常为 null
  * stream_options.include_usage=true 时，会在 [DONE] 之前返回额外 chunk 输出整次 usage（choices 为空数组）

#### 3.2.2 choices[] 字段

* index：integer
* finish_reason：string（最后一个 chunk 可能出现）

  * stop / length / content_filter / tool_calls
* delta：object（增量内容）

  * role：string（assistant）
  * content：string（增量回答文本）
  * reasoning_content：string / null（增量思维链）
  * tool_calls：object[] / null（增量工具调用）

#### 3.2.3 流式结束

* SSE 以 `data: [DONE]` 结束（部分示例直接显示为 `[DONE]`）

---

## 4. 调用示例

### 4.1 示例一：基础请求（stream=true/false 通用）

Header：

```http
Content-Type: application/json
Authorization: Bearer <ARK_API_KEY>
```

Body：

```json
{
  "model": "doubao-seed-1-6-flash-250828",
  "messages": [
    { "role": "system", "content": "xxxxx系统提示词xxxxx" },
    { "role": "user", "content": "xxxxx用户问题xxxxx" }
  ],
  "thinking": { "type": "disabled" },
  "stream": true
}
```

说明：

* messages 数组可包含多轮对话历史，用于上下文记忆

---

### 4.2 示例二：多轮对话（messages 携带历史）

```json
{
  "model": "doubao-seed-1-6-flash-250828",
  "messages": [
    { "role": "system", "content": "xxxxx系统提示词xxxxx" },
    { "role": "user", "content": "我刚刚吃了米饭。" },
    { "role": "assistant", "content": "好的，我明白了。" },
    { "role": "user", "content": "我刚刚吃了什么？" }
  ],
  "thinking": { "type": "disabled" },
  "stream": false
}
```

---

### 4.3 示例三：流式输出响应示例（thinking.disabled, stream=true）

SSE chunk（节选）：

```json
{"choices":[{"delta":{"content":"我","role":"assistant"},"index":0}],"created":1767669040,"id":"02176766904009105f4998a4b036137ca52d90e5e3ca4d87ae9ef","model":"doubao-seed-1-6-flash-250828","service_tier":"default","object":"chat.completion.chunk","usage":null}
{"choices":[{"delta":{"content":"是","role":"assistant"},"index":0}],"created":1767669040,"id":"02176766904009105f4998a4b036137ca52d90e5e3ca4d87ae9ef","model":"doubao-seed-1-6-flash-250828","service_tier":"default","object":"chat.completion.chunk","usage":null}
...
{"choices":[{"delta":{"content":"。","role":"assistant"},"index":0}],"created":1767669040,"id":"02176766904009105f4998a4b036137ca52d90e5e3ca4d87ae9ef","model":"doubao-seed-1-6-flash-250828","service_tier":"default","object":"chat.completion.chunk","usage":null}
{"choices":[{"delta":{"content":"","role":"assistant"},"finish_reason":"stop","index":0}],"created":1767669040,"id":"02176766904009105f4998a4b036137ca52d90e5e3ca4d87ae9ef","model":"doubao-seed-1-6-flash-250828","service_tier":"default","object":"chat.completion.chunk","usage":null}
[DONE]
```

---

### 4.4 示例四：非流式输出响应示例（thinking.disabled, stream=false）

```json
{
  "choices": [
    {
      "finish_reason": "stop",
      "index": 0,
      "logprobs": null,
      "message": {
        "content": "xxxxx这里是回答xxxxx",
        "role": "assistant"
      }
    }
  ],
  "created": 1767663862,
  "id": "021767663861632ff2ba97e867cc87407be36d298bd3a559b092c",
  "model": "doubao-seed-1-6-flash-250828",
  "service_tier": "default",
  "object": "chat.completion",
  "usage": {
    "completion_tokens": 32,
    "prompt_tokens": 119,
    "total_tokens": 151,
    "prompt_tokens_details": {
      "cached_tokens": 0
    },
    "completion_tokens_details": {
      "reasoning_tokens": 0
    }
  }
}
```

---

### 4.5 示例五：流式思维链（thinking.enabled, stream=true）

说明：

* 思维链增量在 delta.reasoning_content
* 答案增量在 delta.content

SSE chunk（节选）：

```json
{"choices":[{"delta":{"content":"","reasoning_content":"xxxx","role":"assistant"},"index":0}],"created":1767683614,"id":"02176768361464022e3e8fd95860fe9a0eb447c2a2edafc810a2a","model":"doubao-seed-1-6-flash-250828","service_tier":"default","object":"chat.completion.chunk","usage":null}
...
{"choices":[{"delta":{"content":"xxxx","role":"assistant"},"index":0}],"created":1767683614,"id":"02176768361464022e3e8fd95860fe9a0eb447c2a2edafc810a2a","model":"doubao-seed-1-6-flash-250828","service_tier":"default","object":"chat.completion.chunk","usage":null}
...
{"choices":[{"delta":{"content":"","role":"assistant"},"finish_reason":"stop","index":0}],"created":1767683614,"id":"02176768361464022e3e8fd95860fe9a0eb447c2a2edafc810a2a","model":"doubao-seed-1-6-flash-250828","service_tier":"default","object":"chat.completion.chunk","usage":null}
[DONE]
```

---

### 4.6 示例六：Function Calling（tools 定义 + messages 回填链路）

#### 4.6.1 tools 定义示例（两个工具）

```json
{
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "GetLineCompletionRate",
        "description": "查询指定产线在指定日期的计划产量、完成产量与完成率",
        "parameters": {
          "type": "object",
          "properties": {
            "line": { "type": "string", "description": "产线名称，例如 A产线" },
            "date": { "type": "string", "description": "日期，格式 YYYY-MM-DD" }
          },
          "required": ["line", "date"]
        }
      }
    },
    {
      "type": "function",
      "function": {
        "name": "WeComSendMessage",
        "description": "通过企业微信发送文本消息给指定人员",
        "parameters": {
          "type": "object",
          "properties": {
            "toUser": { "type": "string", "description": "接收人账号，例如 zhangsan" },
            "msgType": { "type": "string", "description": "消息类型，示例为 text" },
            "text": {
              "type": "object",
              "properties": {
                "content": { "type": "string", "description": "文本内容" }
              },
              "required": ["content"]
            }
          },
          "required": ["toUser", "msgType", "text"]
        }
      }
    }
  ],
  "tool_choice": "auto",
  "parallel_tool_calls": true
}
```

#### 4.6.2 FC 调用流程

>一次多轮 FC 工作的终止条件可以是 choices[].message 里不在包含 tool_calls

##### 1. 构建初始发送信息与工具

向 ARK_URL 发送请求：
Header：

```http
Content-Type: application/json
Authorization: Bearer <ARK_API_KEY>
```

Body：

```json
{
    "model": "doubao-seed-1-6-flash-250828",
    "messages": [
        {
        "role": "system",
        "content": "你是产线数据自动化分析助手"
        },
        {
        "role": "user",
        "content": "把A产线2026年1月28号的完成率发给张三（微信）"
        }
    ],
    "thinking": {
        "type": "disabled"
    },
    "temperature": 1,
    "max_tokens": 30000,
    "tools": [
        {
            "type": "function",
            "function": {
                "description": "查询指定产线在指定日期的计划产量、完成产量与完成率",
                "name": "GetLineCompletionRate",
                "parameters": {
                "type": "object",
                "properties": {
                    "line": {
                    "type": "string",
                    "description": "产线名称，例如 A产线"
                    },
                    "date": {
                    "type": "string",
                    "description": "日期，格式 YYYY-MM-DD"
                    }
                },
                "required": [
                    "line",
                    "date"
                ]
                }
            }
        },
        {
            "type": "function",
            "function": {
                "name": "WeComSendMessage",
                "description": "通过企业微信发送文本消息给指定人员",
                "parameters": {
                "type": "object",
                "properties": {
                    "toUser": {
                    "type": "string",
                    "description": "接收人账号，例如 zhangsan"
                    },
                    "msgType": {
                    "type": "string",
                    "description": "消息类型，示例为 text"
                    },
                    "text": {
                    "type": "object",
                    "properties": {
                        "content": {
                        "type": "string",
                        "description": "文本内容"
                        }
                    },
                    "required": [
                        "content"
                    ]
                    }
                },
                "required": [
                    "toUser",
                    "msgType",
                    "text"
                ]
                }
            }
        }
    ]
}
```

##### 2. AI 识别到 FC 意图，返回 Function Calling 结构

```json
{
  "choices": [
    {
      "finish_reason": "tool_calls",
      "index": 0,
      "logprobs": null,
      "message": {
        "content": "",
        "role": "assistant",
        "tool_calls": [
          {
            "function": {
              "arguments": "{\"line\":\"A产线\",\"date\":\"2026-01-28\"}",
              "name": "GetLineCompletionRate"
            },
            "id": "call_je8ascb3viik9kngtdtz7515",
            "type": "function"
          }
        ]
      }
    }
  ],
  "created": 1769584425,
  "id": "021769584424856c9e5891c80703c73b0beb53b0155b0bf4551b1",
  "model": "doubao-seed-1-6-flash-250828",
  "service_tier": "default",
  "object": "chat.completion",
  "usage": {
    "completion_tokens": 31,
    "prompt_tokens": 555,
    "total_tokens": 586,
    "prompt_tokens_details": {
      "cached_tokens": 0
    },
    "completion_tokens_details": {
      "reasoning_tokens": 0
    }
  }
}
```

##### 3. 业务侧根据 AI 返回的 FC 参数调用工具，并回填结果

```json
"messages": [
    {
    "role": "system",
    "content": "你是产线数据自动化分析助手"
    },
    {
    "role": "user",
    "content": "把A产线2026年1月28号的完成率发给张三（微信）"
    },
    {
    "role": "assistant",
    "tool_calls": [
        {
        "id": "call_je8ascb3viik9kngtdtz7515",
        "type": "function",
        "function": {
            "name": "GetLineCompletionRate",
            "arguments": "{\\\"line\\\":\\\"A产线\\\",\\\"date\\\":\\\"2026-01-28\\\"}"
        }
        }
    ]
    },
    {
    "role": "tool",
    "content": "{\\\"line\\\":\\\"A产线\\\",\\\"date\\\":\\\"2026-01-28\\\",\\\"planQty\\\":1200,\\\"actualQty\\\":1068,\\\"completionRate\\\":0.89,\\\"completionRateText\\\":\\\"89%\\\",\\\"updatedAt\\\":\\\"2026-01-28T09:15:30+08:00\\\"}",
    "tool_call_id": "call_je8ascb3viik9kngtdtz7515"
    }
]
```

##### 4. 再次发送继续 FC 流程直到本次意图任务完成

向 ARK_URL 发送请求：
Header：

```http
Content-Type: application/json
Authorization: Bearer <ARK_API_KEY>
```

Body：

```json
{
    "model": "doubao-seed-1-6-flash-250828",
    "messages": [
        {
        "role": "system",
        "content": "你是产线数据自动化分析助手"
        },
        {
        "role": "user",
        "content": "把A产线2026年1月28号的完成率发给张三（微信）"
        },
        {
        "role": "assistant",
        "tool_calls": [
            {
            "id": "call_je8ascb3viik9kngtdtz7515",
            "type": "function",
            "function": {
                "name": "GetLineCompletionRate",
                "arguments": "{\\\"line\\\":\\\"A产线\\\",\\\"date\\\":\\\"2026-01-28\\\"}"
            }
            }
        ]
        },
        {
        "role": "tool",
        "content": "{\\\"line\\\":\\\"A产线\\\",\\\"date\\\":\\\"2026-01-28\\\",\\\"planQty\\\":1200,\\\"actualQty\\\":1068,\\\"completionRate\\\":0.89,\\\"completionRateText\\\":\\\"89%\\\",\\\"updatedAt\\\":\\\"2026-01-28T09:15:30+08:00\\\"}",
        "tool_call_id": "call_je8ascb3viik9kngtdtz7515"
        }
    ],
    "thinking": {
        "type": "disabled"
    },
    "temperature": 1,
    "max_tokens": 30000,
    "tools": [
        {
        "type": "function",
        "function": {
            "description": "查询指定产线在指定日期的计划产量、完成产量与完成率",
            "name": "GetLineCompletionRate",
            "parameters": {
            "type": "object",
            "properties": {
                "line": {
                "type": "string",
                "description": "产线名称，例如 A产线"
                },
                "date": {
                "type": "string",
                "description": "日期，格式 YYYY-MM-DD"
                }
            },
            "required": [
                "line",
                "date"
            ]
            }
        }
        },
        {
        "function": {
            "name": "WeComSendMessage",
            "description": "通过企业微信发送文本消息给指定人员",
            "parameters": {
            "type": "object",
            "properties": {
                "toUser": {
                "type": "string",
                "description": "接收人账号，例如 zhangsan"
                },
                "msgType": {
                "type": "string",
                "description": "消息类型，示例为 text"
                },
                "text": {
                "type": "object",
                "properties": {
                    "content": {
                    "type": "string",
                    "description": "文本内容"
                    }
                },
                "required": [
                    "content"
                ]
                }
            },
            "required": [
                "toUser",
                "msgType",
                "text"
            ]
            }
        },
        "type": "function"
        }
    ]
}
```

##### 5. AI 识别到剩余的 FC 意图，返回 Function Calling 结构

Body：

```json
{
  "choices": [
    {
      "finish_reason": "tool_calls",
      "index": 0,
      "logprobs": null,
      "message": {
        "content": "",
        "role": "assistant",
        "tool_calls": [
          {
            "function": {
              "arguments": "{\"toUser\":\"张三\",\"msgType\":\"text\",\"text\":{\"content\":\"A产线2026年1月28号的完成率为89%（计划产量1200，实际产量1068）\"}}",
              "name": "WeComSendMessage"
            },
            "id": "call_gcpx88kpyyiwbpuc4id9ds8x",
            "type": "function"
          }
        ]
      }
    }
  ],
  "created": 1769582655,
  "id": "021769582655087dff8db84132232d2c44092acc800bee11d5d08",
  "model": "doubao-seed-1-6-flash-250828",
  "service_tier": "default",
  "object": "chat.completion",
  "usage": {
    "completion_tokens": 59,
    "prompt_tokens": 700,
    "total_tokens": 759,
    "prompt_tokens_details": {
      "cached_tokens": 0
    },
    "completion_tokens_details": {
      "reasoning_tokens": 0
    }
  }
}
```

##### 6. 业务侧根据 AI 返回的 FC 参数调用工具，并回填结果

```json
"messages": [
      {
        "role": "system",
        "content": "你是产线数据自动化分析助手"
      },
      {
        "role": "user",
        "content": "把A产线2026年1月28号的完成率发给张三（微信）"
      },
      {
        "role": "assistant",
        "tool_calls": [
          {
            "id": "call_je8ascb3viik9kngtdtz7515",
            "type": "function",
            "function": {
              "name": "GetLineCompletionRate",
              "arguments": "{\\\"line\\\":\\\"A产线\\\",\\\"date\\\":\\\"2026-01-28\\\"}"
            }
          }
        ]
      },
      {
        "role": "tool",
        "content": "{\\\"line\\\":\\\"A产线\\\",\\\"date\\\":\\\"2026-01-28\\\",\\\"planQty\\\":1200,\\\"actualQty\\\":1068,\\\"completionRate\\\":0.89,\\\"completionRateText\\\":\\\"89%\\\",\\\"updatedAt\\\":\\\"2026-01-28T09:15:30+08:00\\\"}",
        "tool_call_id": "call_je8ascb3viik9kngtdtz7515"
      },
      {
        "role": "assistant",
        "tool_calls": [
          {
            "id": "call_gcpx88kpyyiwbpuc4id9ds8x",
            "function": {
              "name": "WeComSendMessage",
              "arguments": "{\\\"toUser\\\":\\\"张三\\\",\\\"msgType\\\":\\\"text\\\",\\\"text\\\":{\\\"content\\\":\\\"A产线2026年1月28号的完成率为89%（计划产量1200，实际产量1068）\\\"}}"
            },
            "type": "function"
          }
        ]
      },
      {
        "role": "tool",
        "tool_call_id": "call_gcpx88kpyyiwbpuc4id9ds8x",
        "content": "{\"ok\":true,\"provider\":\"wecom\",\"requestId\":\"req_01HMZ6D9K7P7M1D6ZVJ4W3N2Q8\",\"sentAt\":\"2026-01-28T09:15:35+08:00\"}"
      },
]
```

##### 7. 再次发送继续 FC 流程直到本次意图任务完成

得到最终响应：

```json
{
  "choices": [
    {
      "finish_reason": "stop",
      "index": 0,
      "logprobs": null,
      "message": {
        "content": "已将A产线2026年1月28号的完成率信息通过企业微信发送给张三（微信），内容如下：  \n**A产线2026年1月28号的完成率为89%**（计划产量1200，实际产量1068）  \n\n消息发送状态：成功 ✅",
        "role": "assistant"
      }
    }
  ],
  "created": 1769585091,
  "id": "02176958509030016b9207fa63b06ee2291b1c6ec38d3d7b9b211",
  "model": "doubao-seed-1-6-flash-250828",
  "service_tier": "default",
  "object": "chat.completion",
  "usage": {
    "completion_tokens": 78,
    "prompt_tokens": 873,
    "total_tokens": 951,
    "prompt_tokens_details": {
      "cached_tokens": 0
    },
    "completion_tokens_details": {
      "reasoning_tokens": 0
    }
  }
}
```

#### 4.6.3 messages 最终回填链路示例（完整列表）

```json
"messages": [
      {
        "role": "system",
        "content": "你是产线数据自动化分析助手"
      },
      {
        "role": "user",
        "content": "把A产线2026年1月28号的完成率发给张三（微信）"
      },
      {
        "role": "assistant",
        "tool_calls": [
          {
            "id": "call_je8ascb3viik9kngtdtz7515",
            "type": "function",
            "function": {
              "name": "GetLineCompletionRate",
              "arguments": "{\\\"line\\\":\\\"A产线\\\",\\\"date\\\":\\\"2026-01-28\\\"}"
            }
          }
        ]
      },
      {
        "role": "tool",
        "content": "{\\\"line\\\":\\\"A产线\\\",\\\"date\\\":\\\"2026-01-28\\\",\\\"planQty\\\":1200,\\\"actualQty\\\":1068,\\\"completionRate\\\":0.89,\\\"completionRateText\\\":\\\"89%\\\",\\\"updatedAt\\\":\\\"2026-01-28T09:15:30+08:00\\\"}",
        "tool_call_id": "call_je8ascb3viik9kngtdtz7515"
      },
      {
        "role": "assistant",
        "tool_calls": [
          {
            "id": "call_gcpx88kpyyiwbpuc4id9ds8x",
            "function": {
              "name": "WeComSendMessage",
              "arguments": "{\\\"toUser\\\":\\\"张三\\\",\\\"msgType\\\":\\\"text\\\",\\\"text\\\":{\\\"content\\\":\\\"A产线2026年1月28号的完成率为89%（计划产量1200，实际产量1068）\\\"}}"
            },
            "type": "function"
          }
        ]
      },
      {
        "role": "tool",
        "tool_call_id": "call_gcpx88kpyyiwbpuc4id9ds8x",
        "content": "{\"ok\":true,\"provider\":\"wecom\",\"requestId\":\"req_01HMZ6D9K7P7M1D6ZVJ4W3N2Q8\",\"sentAt\":\"2026-01-28T09:15:35+08:00\"}"
      },
      { 
        "role": "assistant", 
        "content": "已将A产线2026年1月28号的完成率信息通过企业微信发送给张三（微信），内容如下：  \n**A产线2026年1月28号的完成率为89%**（计划产量1200，实际产量1068）  \n\n消息发送状态：成功 ✅" 
      }
]
```

4.6.3 工具调用链路关键规则

* assistant 返回 tool_calls 后，业务侧执行工具并回填 role="tool" 消息
* tool 消息必须带 tool_call_id，且与对应的 tool_calls[].id 完全一致
* function.arguments 为 JSON 字符串，模型可能生成非严格 JSON 或虚构参数；工具执行前需要参数校验
* parallel_tool_calls=true 时，assistant 可能一次返回多个 tool_calls；每个 tool_call 都需要各自回填一条 tool 消息，避免混淆
