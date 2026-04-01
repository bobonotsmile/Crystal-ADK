# Ollama ChatAPI 说明与示例

本文档严格依据提供的 OpenAPI（`POST /api/chat`）片段整理，仅做结构重排与中文释义，不引入未出现字段，不改变字段语义。

---

## 1. Ollama 配置说明

### 1.1 环境变量

* `OLLAMA_URL`

  * 含义：Ollama Chat API 请求地址
  * 本地默认值：`http://localhost:11434/api/chat`
  * 远程部署示例：`http://<host>:11434/api/chat`
  * 备注：如果对接的是本地或自建 Ollama 服务，通常配置到 `/api/chat`
* `OLLAMA_API_KEY`

  * 含义：可选鉴权 Key
  * 使用位置：HTTP Header `Authorization: Bearer <OLLAMA_API_KEY>`
  * 本地默认行为：通常不需要配置
  * 适用场景：接入启用了鉴权的代理层、网关，或直接访问需要 API Key 的云端服务时可配置

### 1.2 固定请求信息

* Method：POST
* Content-Type：application/json
* Authorization：可选，仅在配置了 `OLLAMA_API_KEY` 时传入

---

## 2. 接口概览

* **路径**：`POST /api/chat`
* **服务地址（示例）**：`http://localhost:11434`
* **用途**：在会话中生成下一条助手消息。
* **鉴权**：本地或自建 Ollama 服务通常无需鉴权；如服务端启用了鉴权，则可通过 `Authorization: Bearer <OLLAMA_API_KEY>` 传入。

---

## 3. 请求与响应的内容类型

### 3.1 请求内容类型

* `application/json`

### 3.2 响应内容类型（两种）

* `application/json`：非流式完整响应（对应 `ChatResponse`）
* `application/x-ndjson`：流式事件（对应 `ChatStreamEvent`，逐条事件输出）

---

## 4. 请求体：ChatRequest（完整字段说明）

ChatRequest 是一个 JSON 对象，必填字段为：`model`、`messages`。

为便于阅读，字段按职责分组：必填核心 → 输出控制 → 模型驻留 → 概率信息 → 工具与格式 → 生成参数。

---

### 4.1 必填核心字段

```json
{
  // 【必填】模型名称
  "model": "gpt-oss:20b",

  // 【必填】对话历史数组（按时间顺序）
  "messages": [
    {
      // 【必填】消息角色：system | user | assistant | tool
      "role": "system",
      // 【必填】消息文本
      "content": "输出尽量简洁、条目化。",

      // 【可选】多模态内联图片列表：每项为 base64 字符串
      "images": [
        "BASE64_IMAGE_1"
      ],

      // 【可选】工具调用请求（通常由 assistant 产生；此处仅展示结构）
      "tool_calls": [
        {
          "function": {
            // 【必填】要调用的函数名
            "name": "get_gpu_status",
            // 【可选】函数用途说明
            "description": "获取 GPU 状态",
            // 【可选】函数入参对象（JSON 对象）
            "arguments": {
              "detail": true
            }
          }
        }
      ]
    },
    {
      "role": "user",
      "content": "列出查看GPU、驱动、显存占用的命令，并说明每条命令看什么"
    }

    // 还可以继续追加 assistant/tool 消息作为上下文
  ],

  // 【可选】函数工具定义列表：模型在对话中“可能调用”的工具
  "tools": [
    {
      // 【必填】工具类型：固定为 function
      "type": "function",
      "function": {
        // 【必填】暴露给模型的函数名
        "name": "get_gpu_status",
        // 【可选】人类可读描述
        "description": "获取 GPU / 驱动 / 显存信息",
        // 【必填】函数参数的 JSON 结构约束描述（以 JSON 方式表达）
        "parameters": {
          "type": "object",
          // properties 表示允许出现哪些入参字段（键），以及每个字段的类型约束。
          "properties": {
            "detail": { "type": "boolean" },
            "filter": { "type": "string" }
          },
          // required 表示：哪些字段是必填
          "required": [],
          // 表示：除 properties 列出来的字段外，是否允许再出现其它字段
          "additionalProperties": false
        }
      }
    }
  ],

  // 【可选】返回格式约束：
  // - "json"：要求输出为 JSON
  // - object：用 JSON 结构描述的约束（规范描述为“JSON schema 对象”）
  "format": "json",

  // 【可选】运行参数（控制生成行为）
  "options": {
    // 随机种子（用于可复现输出）
    "seed": 42,

    // 随机性控制（越高越随机）
    "temperature": 0.2,

    // top-k 采样：下一 token 仅从概率最高前 K 个候选中选
    "top_k": 40,

    // top-p 采样：按累积概率阈值截断候选集合
    "top_p": 0.9,

    // 最小概率阈值：低于阈值的 token 候选可被过滤
    "min_p": 0.05,

    // 停止序列：命中任一停止序列即终止生成
    "stop": ["\n\n"],

    // 上下文长度大小（token 数）
    "num_ctx": 4096,

    // 最大生成 token 数
    "num_predict": 200

    // 规范允许出现额外字段（是否生效取决于实现）
  },

  // 【可选】是否流式返回；默认 true
  // - true：返回 application/x-ndjson 逐条事件
  // - false：返回一次性完整 JSON
  "stream": false,

  // 【可选】思考输出开关：
  // - boolean：true/false
  // - string：high | medium | low（仅支持的模型有效）
  "think": "medium",

  // 【可选】模型驻留时长：
  // - 例如 "5m"
  // - 或 0 表示立即卸载
  "keep_alive": "5m",

  // 【可选】是否返回输出 token 的对数概率信息
  "logprobs": false,

  // 【可选】启用 logprobs 时，每个 token 位置返回最可能的前 N 个候选 token
  "top_logprobs": 3
}
```

#### 3.1.1 `model`（字符串，必填）

* 含义：模型名称。

#### 3.1.2 `messages`（数组，必填）

* 含义：对话历史消息数组。
* 元素类型：`ChatMessage`（见 3.2）。

---

### 3.2 `messages[]` 元素：ChatMessage（完整字段说明）

ChatMessage 是一个对象，必填字段为：`role`、`content`。

#### 3.2.1 `role`（字符串，必填）

* 可选值：`system`、`user`、`assistant`、`tool`
* 含义：消息作者角色。

#### 3.2.2 `content`（字符串，必填）

* 含义：消息文本内容。

#### 3.2.3 `images`（字符串数组，可选）

* 含义：内联图片列表。
* 每项为：图片内容的 base64 编码。

#### 3.2.4 `tool_calls`（数组，可选）

* 含义：模型生成的工具调用请求列表。
* 元素类型：`ToolCall`（见 6.1）。

---

### 3.3 输出控制字段

#### 3.3.1 `stream`（布尔值，可选，默认 `true`）

* 含义：是否流式输出响应。

  * `true`：响应为逐条事件输出（内容类型为 `application/x-ndjson`）
  * `false`：响应为一次性完整 JSON（内容类型为 `application/json`）

#### 3.3.2 `format`（可选）

* 形态：字符串或对象（二选一）
* 含义：约束响应的返回格式：

  * 字符串：仅允许取值 `"json"`（要求输出为 JSON）
  * 对象：作为 JSON 结构约束（以 JSON 结构描述）

#### 3.3.3 `think`（可选）

* 形态：布尔值或字符串（二选一）
* 字符串可选值：`high`、`medium`、`low`
* 含义：启用思考输出；启用后响应消息中可能包含 `thinking` 字段（仅对支持的模型有效）。

---

### 3.4 模型驻留字段

#### 3.4.1 `keep_alive`（可选）

* 形态：字符串或数值（二选一）
* 含义：模型驻留时长控制。

  * 示例：`"5m"` 表示驻留一段时间
  * 示例：`0` 表示立即卸载

> 该字段仅用于控制模型加载驻留与卸载，不表示“记忆功能”。对话上下文仍由 `messages` 提供。

---

### 3.5 概率信息字段

#### 3.5.1 `logprobs`（布尔值，可选）

* 含义：是否返回输出 token 的对数概率信息。
* 影响：启用后响应顶层可能包含 `logprobs` 字段。

#### 3.5.2 `top_logprobs`（整数，可选）

* 含义：当启用 `logprobs` 时，在每个 token 位置返回概率最高的前 N 个候选 token。

---

### 3.6 工具字段

#### 3.6.1 `tools`（数组，可选）

* 含义：模型在对话中可能调用的函数工具列表。
* 元素类型：`ToolDefinition`（见 3.6.2）。

#### 3.6.2 ToolDefinition（工具定义对象）

ToolDefinition 必填字段：`type`、`function`

* `type`（字符串，必填）

  * 仅允许取值：`function`
  * 含义：工具类型标识（固定为函数工具）。

* `function`（对象，必填）

  * 必填字段：`name`、`parameters`

  * `name`（字符串，必填）

    * 含义：暴露给模型的函数名。

  * `description`（字符串，可选）

    * 含义：函数用途描述。

  * `parameters`（对象，必填）

    * 含义：函数参数的 JSON 结构约束描述。

---

### 3.7 生成参数字段

#### 3.7.1 `options`（对象，可选）

* 含义：控制文本生成过程的运行参数对象。
* 特性：允许除已列出字段之外的其他字段出现（是否生效取决于实现）。

已列出字段如下：

* `seed`（整数）

  * 含义：随机种子，用于输出可复现。

* `temperature`（数值）

  * 含义：控制随机性（越高越随机）。

* `top_k`（整数）

  * 含义：下一 token 仅从概率最高的前 K 个候选中选择。

* `top_p`（数值）

  * 含义：按累积概率阈值选择候选范围。

* `min_p`（数值）

  * 含义：token 选择的最小概率阈值。

* `stop`（字符串或字符串数组）

  * 含义：停止序列；匹配到停止序列时终止生成。

* `num_ctx`（整数）

  * 含义：上下文长度大小（token 数）。

* `num_predict`（整数）

  * 含义：最大生成 token 数。

---

## 4. 响应体：ChatResponse（非流式）

ChatResponse 是一个对象，字段说明如下。

### 4.1 顶层字段

* `model`（字符串）

  * 含义：生成该消息所使用的模型名。

* `created_at`（字符串，时间格式）

  * 含义：响应创建时间戳。

* `message`（对象）

  * 含义：生成的助手消息对象（见 4.2）。

* `done`（布尔值）

  * 含义：响应是否完成。

* `done_reason`（字符串）

  * 含义：结束原因。

* `total_duration`（整数）

  * 含义：总耗时（纳秒）。

* `load_duration`（整数）

  * 含义：模型加载耗时（纳秒）。

* `prompt_eval_count`（整数）

  * 含义：输入部分的 token 数量。

* `prompt_eval_duration`（整数）

  * 含义：输入评估耗时（纳秒）。

* `eval_count`（整数）

  * 含义：输出生成的 token 数量。

* `eval_duration`（整数）

  * 含义：输出生成耗时（纳秒）。

* `logprobs`（数组，可选）

  * 含义：启用概率信息时返回的 token 概率数据（见 6.2）。

---

### 4.2 `message`（响应消息对象）

* `role`（字符串）

  * 仅允许取值：`assistant`
  * 含义：响应消息角色固定为助手。

* `content`（字符串）

  * 含义：助手输出文本。

* `thinking`（字符串，可选）

  * 含义：启用思考输出后可能返回的思考文本。

* `tool_calls`（数组，可选）

  * 含义：助手请求的工具调用列表（见 6.1）。

* `images`（字符串数组，可选）

  * 含义：响应中可能包含的 base64 图片列表。

---

## 5. 流式响应：ChatStreamEvent（逐条事件）

当 `stream=true` 时，响应为逐条事件对象，字段说明如下。

### 5.1 顶层字段

* `model`（字符串）

  * 含义：本事件对应的模型名。

* `created_at`（字符串，时间格式）

  * 含义：该事件创建时间戳。

* `message`（对象）

  * 含义：该事件携带的消息片段（见 5.2）。

* `done`（布尔值）

  * 含义：是否为最后一个事件；为 `true` 时表示流结束。

### 5.2 `message`（流式片段消息对象）

* `role`（字符串）

  * 含义：该片段的消息角色。

* `content`（字符串）

  * 含义：助手文本的部分片段。

* `thinking`（字符串，可选）

  * 含义：思考文本片段（启用思考输出时可能出现）。

* `tool_calls`（数组，可选）

  * 含义：工具调用片段（若存在）。

* `images`（字符串数组，可选）

  * 含义：图片片段（若存在）。

---

## 6. 组件对象：ToolCall 与概率对象

### 6.1 ToolCall

ToolCall 是一个对象，包含字段：

* `function`（对象）

  * 必填字段：`name`

  * `name`（字符串，必填）

    * 含义：要调用的函数名。

  * `description`（字符串，可选）

    * 含义：函数用途说明。

  * `arguments`（对象，可选）

    * 含义：要传入函数的参数对象。

---

### 6.2 概率对象 Logprob

Logprob 是一个对象，字段：

* `token`（字符串）

  * 含义：token 的文本表示。

* `logprob`（数值）

  * 含义：该 token 的对数概率。

* `bytes`（整数数组）

  * 含义：该 token 的原始字节表示。

* `top_logprobs`（数组）

  * 含义：当前位置最可能候选 token 的概率信息列表。
  * 元素类型：`TokenLogprob`（见 6.3）。

### 6.3 TokenLogprob

* `token`（字符串）
* `logprob`（数值）
* `bytes`（整数数组）

---

## 7. 示例

示例假设服务在 `127.0.0.1:11434`，模型为 `gpt-oss:20b`。

### 7.1 非流式

请求示例：

```bash
curl -sS http://127.0.0.1:11434/api/chat \
  -H 'Content-Type: application/json' \
  -d '{
    "model": "gpt-oss:20b",
    "messages": [
      { "role": "user", "content": "你好" }
    ],
    "stream": false
  }'
```

响应示例：

```json
{
  "model": "gpt-oss:20b",
  "created_at": "2025-10-17T23:14:07.414671Z",
  "message": {
    "role": "assistant",
    "content": "你好，有什么可以帮你？"
  },
  "done": true,
  "done_reason": "stop",
  "total_duration": 174560334,
  "load_duration": 101397084,
  "prompt_eval_count": 11,
  "prompt_eval_duration": 13074791,
  "eval_count": 18,
  "eval_duration": 52479709
}
```

### 7.2 流式

请求示例：

```bash
curl -N http://127.0.0.1:11434/api/chat \
  -H 'Content-Type: application/json' \
  -d '{
    "model": "gpt-oss:20b",
    "messages": [
      { "role": "user", "content": "1+1等于几" }
    ],
    "stream": true
  }'
```

响应示例（application/x-ndjson）：

```jsonl
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:37.518616017Z","message":{"role":"assistant","content":"","thinking":"The"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:37.576188908Z","message":{"role":"assistant","content":"","thinking":" user"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:37.634754811Z","message":{"role":"assistant","content":"","thinking":" writes"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:37.692413259Z","message":{"role":"assistant","content":"","thinking":" in"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:37.751714388Z","message":{"role":"assistant","content":"","thinking":" Chinese"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:37.810884563Z","message":{"role":"assistant","content":"","thinking":":"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:37.872010101Z","message":{"role":"assistant","content":"","thinking":" \""},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:37.930888267Z","message":{"role":"assistant","content":"","thinking":"1"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:37.988265671Z","message":{"role":"assistant","content":"","thinking":"+"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.045658301Z","message":{"role":"assistant","content":"","thinking":"1"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.10325382Z","message":{"role":"assistant","content":"","thinking":"等"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.16142379Z","message":{"role":"assistant","content":"","thinking":"于"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.224945602Z","message":{"role":"assistant","content":"","thinking":"几"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.282101607Z","message":{"role":"assistant","content":"","thinking":"\""},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.338879139Z","message":{"role":"assistant","content":"","thinking":" which"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.396645492Z","message":{"role":"assistant","content":"","thinking":" means"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.453674804Z","message":{"role":"assistant","content":"","thinking":" \""},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.510297078Z","message":{"role":"assistant","content":"","thinking":"What"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.566679095Z","message":{"role":"assistant","content":"","thinking":" is"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.623119221Z","message":{"role":"assistant","content":"","thinking":" "},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.679832428Z","message":{"role":"assistant","content":"","thinking":"1"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.736388143Z","message":{"role":"assistant","content":"","thinking":"+"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.793532205Z","message":{"role":"assistant","content":"","thinking":"1"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.849861282Z","message":{"role":"assistant","content":"","thinking":"?\""},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.910948407Z","message":{"role":"assistant","content":"","thinking":" The"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:38.972251413Z","message":{"role":"assistant","content":"","thinking":" answer"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.030436956Z","message":{"role":"assistant","content":"","thinking":" is"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.088085416Z","message":{"role":"assistant","content":"","thinking":" "},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.152737619Z","message":{"role":"assistant","content":"","thinking":"2"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.212256863Z","message":{"role":"assistant","content":"","thinking":"."},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.269809849Z","message":{"role":"assistant","content":"","thinking":" But"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.326868494Z","message":{"role":"assistant","content":"","thinking":" we"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.383944111Z","message":{"role":"assistant","content":"","thinking":" need"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.440902324Z","message":{"role":"assistant","content":"","thinking":" to"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.497901743Z","message":{"role":"assistant","content":"","thinking":" respond"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.554748767Z","message":{"role":"assistant","content":"","thinking":" appropriately"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.611448705Z","message":{"role":"assistant","content":"","thinking":"."},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.66799059Z","message":{"role":"assistant","content":"","thinking":" It's"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.725673202Z","message":{"role":"assistant","content":"","thinking":" a"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.783417764Z","message":{"role":"assistant","content":"","thinking":" simple"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.840320732Z","message":{"role":"assistant","content":"","thinking":" question"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.897485257Z","message":{"role":"assistant","content":"","thinking":"."},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:39.955414342Z","message":{"role":"assistant","content":"","thinking":" The"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.015340974Z","message":{"role":"assistant","content":"","thinking":" answer"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.073436212Z","message":{"role":"assistant","content":"","thinking":" is"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.13048899Z","message":{"role":"assistant","content":"","thinking":" \""},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.193880756Z","message":{"role":"assistant","content":"","thinking":"2"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.251065186Z","message":{"role":"assistant","content":"","thinking":"\"."},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.309959904Z","message":{"role":"assistant","content":"","thinking":" Possibly"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.366653695Z","message":{"role":"assistant","content":"","thinking":" respond"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.423610091Z","message":{"role":"assistant","content":"","thinking":" in"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.480886294Z","message":{"role":"assistant","content":"","thinking":" Chinese"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.53735673Z","message":{"role":"assistant","content":"","thinking":":"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.594248313Z","message":{"role":"assistant","content":"","thinking":" \""},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.650847399Z","message":{"role":"assistant","content":"","thinking":"1"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.70729807Z","message":{"role":"assistant","content":"","thinking":"+"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.764210885Z","message":{"role":"assistant","content":"","thinking":"1"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.82120318Z","message":{"role":"assistant","content":"","thinking":"等"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.878380486Z","message":{"role":"assistant","content":"","thinking":"于"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.935253631Z","message":{"role":"assistant","content":"","thinking":"2"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:40.992200459Z","message":{"role":"assistant","content":"","thinking":"\"."},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:41.051682407Z","message":{"role":"assistant","content":"","thinking":" There's"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:41.112175317Z","message":{"role":"assistant","content":"","thinking":" no"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:41.175274724Z","message":{"role":"assistant","content":"","thinking":" trick"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:41.23189567Z","message":{"role":"assistant","content":"","thinking":"."},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:41.288917298Z","message":{"role":"assistant","content":"","thinking":" So"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:41.345936552Z","message":{"role":"assistant","content":"","thinking":" output"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:41.403080753Z","message":{"role":"assistant","content":"","thinking":" \""},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:41.460011098Z","message":{"role":"assistant","content":"","thinking":"2"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:41.516729054Z","message":{"role":"assistant","content":"","thinking":"\"."},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:41.917482137Z","message":{"role":"assistant","content":"1"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:41.974282366Z","message":{"role":"assistant","content":"+"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:42.031511704Z","message":{"role":"assistant","content":"1"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:42.089227421Z","message":{"role":"assistant","content":" 等"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:42.151191339Z","message":{"role":"assistant","content":"于"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:42.210310319Z","message":{"role":"assistant","content":" "},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:42.267525968Z","message":{"role":"assistant","content":"2"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:42.324466719Z","message":{"role":"assistant","content":"。"},"done":false}
{"model":"gpt-oss:20b","created_at":"2026-02-03T01:28:42.381986599Z","message":{"role":"assistant","content":""},"done_reason":"stop","done":true,"total_duration":10359329462,"load_duration":4509748597,"prompt_eval_count":68,"prompt_eval_duration":794467938,"eval_count":88,"eval_duration":5053469687}
```

### 7.3 Function calling（工具函数调用）

请求示例：

```bash
curl -sS http://127.0.0.1:11434/api/chat \
  -H 'Content-Type: application/json' \
  -d '{
    "model": "gpt-oss:20b",
    "messages": [
      { "role": "user", "content": "查询北京天气" }
    ],
    "tools": [
      {
        "type": "function",
        "function": {
          "name": "get_weather",
          "description": "查询城市天气",
          "parameters": {
            "type": "object",
            "properties": {
              "city": { "type": "string" }
            },
            "required": ["city"]
          }
        }
      }
    ],
    "stream": false
  }'
```

响应示例：

```json
{
  "model": "gpt-oss:20b",
  "created_at": "2025-10-17T23:14:07.714671Z",
  "message": {
    "role": "assistant",
    "content": "",
    "tool_calls": [
      {
        "function": {
          "name": "get_weather",
          "arguments": {
            "city": "北京"
          }
        }
      }
    ]
  },
  "done": true,
  "done_reason": "stop",
  "total_duration": 182560334,
  "load_duration": 101397084,
  "prompt_eval_count": 12,
  "prompt_eval_duration": 13074791,
  "eval_count": 6,
  "eval_duration": 32479709
}
```

### 7.4 多轮 Function calling（完整回填链路示例）

##### 1. 构建初始发送信息与工具

向 OLLAMA_URL 发送请求：
Header：

```http
Content-Type: application/json
Authorization: Bearer <OLLAMA_API_KEY> //一般 ollama 默认不需要 apikey
```

Body：

```json
{
    "model": "gpt-oss:20b",

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

    "options": {
      "temperature": 0.2
    },
    
    "think": false,
    "stream": false,
    "keep_alive": "5m"
}
```

##### 2. AI 识别到 FC 意图，返回 Function Calling 结构

```json
{
    "model": "gpt-oss:20b",
    "created_at": "2026-02-03T00:50:01.744988265Z",
    "message": {
        "role": "assistant",
        "content": "",
        "thinking": "The user wants to send a message to Zhang San via WeCom (企业微信) with the completion rate for line A on 2026-01-28. We need to query the completion rate first using GetLineCompletionRate. Then send a message using WeComSendMessage. We need to call the function GetLineCompletionRate with date \"2026-01-28\" and line \"A产线\". Then use the result to send a message. The message text should include the completion rate. The user didn't specify the exact format, but we can send a simple text: \"A产线2026年1月28号的完成率为 X%\". We need to call the function.",
        "tool_calls": [
            {
                "function": {
                    "name": "GetLineCompletionRate",
                    "arguments": {
                        "date": "2026-01-28",
                        "line": "A产线"
                    }
                }
            }
        ]
    },
    "done_reason": "stop",
    "done": true,
    "total_duration": 16717590775,
    "load_duration": 4491304448,
    "prompt_eval_count": 255,
    "prompt_eval_duration": 1621987101,
    "eval_count": 180,
    "eval_duration": 10478111812
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
      "content": "",
      "thinking": "The user wants to send a message to Zhang San via WeCom (企业微信) with the completion rate for line A on 2026-01-28. We need to query the completion rate first using GetLineCompletionRate. Then send a message using WeComSendMessage. We need to call the function GetLineCompletionRate with date \"2026-01-28\" and line \"A产线\". Then use the result to send a message. The message text should include the completion rate. The user didn't specify the exact format, but we can send a simple text: \"A产线2026年1月28号的完成率为 X%\". We need to call the function.",
      "tool_calls": [
          {
              "function": {
                  "name": "GetLineCompletionRate",
                  "arguments": {
                      "date": "2026-01-28",
                      "line": "A产线"
                  }
              }
          }
      ]
  },

  {
    "role": "tool",
    "content": "{\\\"line\\\":\\\"A产线\\\",\\\"date\\\":\\\"2026-01-28\\\",\\\"planQty\\\":1200,\\\"actualQty\\\":1068,\\\"completionRate\\\":0.89,\\\"completionRateText\\\":\\\"89%\\\",\\\"updatedAt\\\":\\\"2026-01-28T09:15:30+08:00\\\"}"
  }
]
```

##### 4. 再次发送继续 FC 流程直到本次意图任务完成

向 OLLAMA_URL 发送请求：
Header：

```http
Content-Type: application/json
Authorization: Bearer <OLLAMA_API_KEY> //一般 ollama 默认不需要 apikey
```

Body：

```json
{
  "model": "gpt-oss:20b",
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
      "content": "",
      "thinking": "The user wants to send a message to Zhang San via WeCom (企业微信) with the completion rate for line A on 2026-01-28. We need to query the completion rate first using GetLineCompletionRate. Then send a message using WeComSendMessage. We need to call the function GetLineCompletionRate with date \"2026-01-28\" and line \"A产线\". Then use the result to send a message. The message text should include the completion rate. The user didn't specify the exact format, but we can send a simple text: \"A产线2026年1月28号的完成率为 X%\". We need to call the function.",
      "tool_calls": [
        {
          "function": {
            "name": "GetLineCompletionRate",
            "arguments": {
              "date": "2026-01-28",
              "line": "A产线"
            }
          }
        }
      ]
    },
    {
      "role": "tool",
      "content": "{\"line\":\"A产线\",\"date\":\"2026-01-28\",\"planQty\":1200,\"actualQty\":1068,\"completionRate\":0.89,\"completionRateText\":\"89%\",\"updatedAt\":\"2026-01-28T09:15:30+08:00\"}"
    }
  ],
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
  "options": {
    "temperature": 0.2
  },
  "think": false,
  "stream": false,
  "keep_alive": "5m"
}
```

##### 5. AI 识别到剩余的 FC 意图，返回 Function Calling 结构

Body：

```json
{
    "model": "gpt-oss:20b",
    "created_at": "2026-02-03T02:18:47.927868678Z",
    "message": {
        "role": "assistant",
        "content": "",
        "thinking": "Now we have completionRate 0.89, completionRateText \"89%\". We need to send a message to Zhang San. Use WeComSendMessage. The msgType is \"text\". The text field likely expects a \"content\" key. We'll send content: \"A产线2026年1月28号的完成率为 89%\". toUser: \"zhangsan\".",
        "tool_calls": [
            {
                "function": {
                    "name": "WeComSendMessage",
                    "arguments": {
                        "msgType": "text",
                        "text": {
                            "content": "A产线2026年1月28号的完成率为 89%"
                        },
                        "toUser": "zhangsan"
                    }
                }
            }
        ]
    },
    "done_reason": "stop",
    "done": true,
    "total_duration": 15577928273,
    "load_duration": 4519744726,
    "prompt_eval_count": 507,
    "prompt_eval_duration": 2801711533,
    "eval_count": 139,
    "eval_duration": 8080781712
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
    "content": "",
    "thinking": "The user wants to send a message to Zhang San via WeCom (企业微信) with the completion rate for line A on 2026-01-28. We need to query the completion rate first using GetLineCompletionRate. Then send a message using WeComSendMessage. We need to call the function GetLineCompletionRate with date \"2026-01-28\" and line \"A产线\". Then use the result to send a message. The message text should include the completion rate. The user didn't specify the exact format, but we can send a simple text: \"A产线2026年1月28号的完成率为 X%\". We need to call the function.",
    "tool_calls": [
        {
            "function": {
                "name": "GetLineCompletionRate",
                "arguments": {
                    "date": "2026-01-28",
                    "line": "A产线"
                }
            }
        }
    ]
  },

  {
    "role": "tool",
    "content": "{\\\"line\\\":\\\"A产线\\\",\\\"date\\\":\\\"2026-01-28\\\",\\\"planQty\\\":1200,\\\"actualQty\\\":1068,\\\"completionRate\\\":0.89,\\\"completionRateText\\\":\\\"89%\\\",\\\"updatedAt\\\":\\\"2026-01-28T09:15:30+08:00\\\"}"
  },

  {
    "role": "assistant",
    "content": "",
    "thinking": "Now we have completionRate 0.89, completionRateText \"89%\". We need to send a message to Zhang San. Use WeComSendMessage. The msgType is \"text\". The text field likely expects a \"content\" key. We'll send content: \"A产线2026年1月28号的完成率为 89%\". toUser: \"zhangsan\".",
    "tool_calls": [
        {
            "function": {
                "name": "WeComSendMessage",
                "arguments": {
                    "msgType": "text",
                    "text": {
                        "content": "A产线2026年1月28号的完成率为 89%"
                    },
                    "toUser": "zhangsan"
                }
            }
        }
    ]
  },

  {
    "role": "tool",
    "content": "{\"ok\":true,\"provider\":\"wecom\",\"requestId\":\"req_01HMZ6D9K7P7M1D6ZVJ4W3N2Q8\",\"sentAt\":\"2026-01-28T09:15:35+08:00\"}"
  }
]
```

##### 7. 再次发送继续 FC 流程直到本次意图任务完成

得到最终响应：

```json
{
    "model": "gpt-oss:20b",
    "created_at": "2026-02-03T02:49:00.071470795Z",
    "message": {
        "role": "assistant",
        "content": "已将 A产线 2026 年 1 月 28 日的完成率（89%）发送给张三。"
    },
    "done_reason": "stop",
    "done": true,
    "total_duration": 16675295829,
    "load_duration": 4540754322,
    "prompt_eval_count": 717,
    "prompt_eval_duration": 10073121634,
    "eval_count": 31,
    "eval_duration": 1807620277
}
```

#### messages 最终回填链路示例（完整列表）

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
    "content": "",
    "thinking": "The user wants to send a message to Zhang San via WeCom (企业微信) with the completion rate for line A on 2026-01-28. We need to query the completion rate first using GetLineCompletionRate. Then send a message using WeComSendMessage. We need to call the function GetLineCompletionRate with date \"2026-01-28\" and line \"A产线\". Then use the result to send a message. The message text should include the completion rate. The user didn't specify the exact format, but we can send a simple text: \"A产线2026年1月28号的完成率为 X%\". We need to call the function.",
    "tool_calls": [
        {
            "function": {
                "name": "GetLineCompletionRate",
                "arguments": {
                    "date": "2026-01-28",
                    "line": "A产线"
                }
            }
        }
    ]
  },

  {
    "role": "tool",
    "content": "{\\\"line\\\":\\\"A产线\\\",\\\"date\\\":\\\"2026-01-28\\\",\\\"planQty\\\":1200,\\\"actualQty\\\":1068,\\\"completionRate\\\":0.89,\\\"completionRateText\\\":\\\"89%\\\",\\\"updatedAt\\\":\\\"2026-01-28T09:15:30+08:00\\\"}"
  },

  {
    "role": "assistant",
    "content": "",
    "thinking": "Now we have completionRate 0.89, completionRateText \"89%\". We need to send a message to Zhang San. Use WeComSendMessage. The msgType is \"text\". The text field likely expects a \"content\" key. We'll send content: \"A产线2026年1月28号的完成率为 89%\". toUser: \"zhangsan\".",
    "tool_calls": [
        {
            "function": {
                "name": "WeComSendMessage",
                "arguments": {
                    "msgType": "text",
                    "text": {
                        "content": "A产线2026年1月28号的完成率为 89%"
                    },
                    "toUser": "zhangsan"
                }
            }
        }
    ]
  },

  {
    "role": "tool",
    "content": "{\"ok\":true,\"provider\":\"wecom\",\"requestId\":\"req_01HMZ6D9K7P7M1D6ZVJ4W3N2Q8\",\"sentAt\":\"2026-01-28T09:15:35+08:00\"}"
  },

  {
    "role": "assistant",
    "content": "已将 A产线 2026 年 1 月 28 日的完成率（89%）发送给张三。"
  }
]
```

### 7.5 补充说明（工具回填与参数差异）

1. **Ollama 的工具回填约束**
   * 协议只要求 `messages[]` 中的 `role: "tool"` 与 `content` 字段。
   * 可以开发业务侧额外增加顶层字段（如 `tool_call_id`、`callId`、`traceId` 等），Ollama 服务端不会强制校验，但模型会看到这些字段（wesunast包不建议加额外字段，但可以暴露回填的权利给包使用者）。
   * `content` 一般是 JSON 字符串，建议保持结构稳定便于模型继续推理。

2. **Ollama 与 Ark/OpenAI 的 `arguments` 形态差异**
   * Ollama 的 `tool_calls.function.arguments` 是 **对象**（JSON object）。
   * Ark/OpenAI 的 `tool_calls.function.arguments` 是 **字符串**（JSON string）。
   * 兼容做法：若 `arguments` 是字符串则 `JSON.parse`，若是对象则直接使用。

3. **关于 `tool_call_id`**
   * OpenAI / Ark 会返回 `tool_call_id`（或 `id`）用于结果绑定。
   * Ollama 通常不返回该字段，因此只能依赖“顺序一致”或自行在 `content` 内附带 `callId` 以做追踪。
