# CoAP.NET 路线图

本文档只记录 CoAP.NET fork 自身的兼容、MVC Routing、性能和维护路线。CoAP 标准中的 Resource 概念继续保留在协议栈内部；新增 route / endpoint / resource 风格 API 的目的，是给应用代码一个更接近现代 .NET 服务器编程的入口，避免业务代码必须直接继承协议内部 Resource 类型。

宿主应用只声明业务端点和调用业务服务。路由注册、路由匹配、请求分发、结果执行、资源发现和框架约定应沉淀在 CoAP.NET 内部，最终达到类似 REST API controller 的开发体验。

## 目标边界

- 保留原 CoAP.NET client、server、resource tree、blockwise、observe 和 DTLS PSK 能力。
- 新增 CoAP route adapter：`CoapRoute`、`CoapGet/Post/Put/Delete`、`CoapRouteContext`、`CoapResourceBase` 和显式 route builder。
- 新增 CoAP MVC Routing：endpoint 数据源、route pattern、matcher、dispatcher、resource/action 描述、attribute route 和 convention route。
- 新增 CoAP resource discovery 集成：`.well-known/core` 由 endpoint metadata 生成资源描述，支持 `rt`、`if`、`ct`、`obs` 等 CoRE Link Format 属性。
- CoAP.NET 内部仍可继承 `CoAP.Server.Resources.Resource`；应用集成层只暴露 route / endpoint。
- 低分配优先：payload 对外提供 `ReadOnlyMemory<byte>`，JSON 解析优先走 source-generated `System.Text.Json`。
- 提供宿主可实现的扩展点：认证/授权 hook、filter、model binder、result executor、metadata provider、service scope factory。
- 不把任何宿主应用的业务模型放进 CoAP.NET 协议包。
- 不把任何宿主应用的领域模型、权限、租户、审计、数据库访问或消息总线逻辑放进 CoAP.NET。

## MVC Routing 目标形态

CoAP.NET 的主入口应与 ASP.NET Core MVC 的使用方式接近。宿主注册 CoAP server 和 MVC 服务，框架按宿主应用程序集自动发现 resource；业务项目只声明 resource/action，不再手写 `CoapRouteEndpoint.Create(...)`，也不直接 `new CoapServer()` 或传递 `IServiceProvider`。

```csharp
builder.Services.AddCoapServer(options =>
{
    options.ListenAnyIP(5683);
});
builder.Services.AddCoapResources();

var app = builder.Build();
app.MapCoapResources();
app.Run();
```

`AddApplicationPart(...)` 只作为插件程序集、外部模块或测试场景的扩展入口，普通宿主业务 resource 不需要显式调用。

业务项目只声明业务路径和业务动作：

```csharp
[CoapResource]
[CoapRoute("sensors/{sensor}")]
public sealed class SensorCoapResource : CoapResourceBase
{
    [CoapPost("readings")]
    [CoapResourceTitle("Sensor reading upload")]
    public ValueTask<ICoapResult> UploadReadingAsync(
        string sensor,
        CancellationToken cancellationToken)
    {
        var payload = Payload;
        // 宿主业务逻辑：鉴权、资源校验、数据写入、事件发布。
    }
}
```

目标不是复制完整 ASP.NET Core MVC，而是提供 CoAP 适配的最小 MVC 子集：

- server hosting：`AddCoapServer()` 注册监听端口、传输、DTLS 和后台生命周期。
- resource registration：`AddCoapResources()` / `AddCoapMvc()` 注册 endpoint data source、resource discovery、matcher、dispatcher、binder、result executor 和 discovery provider。
- endpoint mapping：`app.MapCoapResources()` 像 `app.MapControllers()` 一样完成 CoAP resource endpoint 映射，不暴露 `CoapServer` 手工组装。
- attribute routing：`[CoapResource]` 是推荐资源标记；`[CoapController]` 仅作为兼容标记；action 使用 `[CoapRoute]`、`[CoapGet]`、`[CoapPost]`、`[CoapPut]`、`[CoapDelete]`、`[CoapObserve]`。
- convention routing：按 resource/action 命名生成默认 route，允许宿主覆盖。
- endpoint metadata：method、path、content-format、accept、observe、discovery 属性、授权策略名。
- dispatcher pipeline：匹配 endpoint，创建调用上下文，执行 filter，绑定参数，调用 action，执行 result。
- resource discovery：根据 endpoint metadata 生成 `.well-known/core` 输出。
- DI 友好：resource 可由宿主服务容器创建，CoAP.NET 不持有业务单例。

显式 route handler 只作为底层兼容和轻量测试入口继续保留，不作为宿主应用的推荐开发模式，也不再用某个宿主应用的业务路径作为示例。

## 当前基线

- `CoAP.Server.Routing.CoapRouteEndpoint` 已存在，能够把 method + Uri-Path 映射到 `CoapRouteHandler`。
- `CoapRouteContext` 已暴露 path、route values、queries、payload、Content-Format、Accept；`CoapResourceBase` 已能像 MVC 的 `ControllerBase` 一样在 action 内暴露当前上下文。
- route values 使用数组化只读集合承载，避免小参数集走 `Dictionary` 分配；重复参数名保持后值覆盖前值的兼容语义。
- `CoapRouteResult` 已支持 status、text/binary payload、Content-Format、ETag、Max-Age、Location-Path 和 Location-Query。
- `[CoapResource]`、`[CoapRoute]`、method attributes、resource/action descriptor 和 application part 扫描已能生成 endpoint 数据源；`[CoapController]` 保留为兼容标记。
- 既有宿主集成可能仍通过手写 route endpoint 注册表和业务上下文拼装接入 CoAP.NET；这只是过渡状态。
- 下一阶段要把通用的注册、匹配、分发和发现能力迁入 CoAP.NET；宿主应用只保留业务路径声明和业务服务调用。

## 阶段

| 阶段 | 状态 | 主题 | 交付 |
| --- | --- | --- | --- |
| C0 | `✅` | 命名与兼容基线 | 已记录现有 Resource API、server/client 示例、DTLS PSK、blockwise、observe 测试入口；README 明确 Resource 是协议内部概念。 |
| C1 | `✅` | CoAP route adapter | 已新增 `CoAP.Server.Routing.CoapRouteEndpoint`，把 Uri-Path 和 method 转成 route 匹配输入；公开 API 使用 route / endpoint 命名。 |
| C2 | `✅` | async handler 与结果模型 | handler 返回 `ValueTask`；`CoapRouteResult` 已映射 CoAP status code、payload、Content-Format、ETag、Max-Age、Location-Path 和 Location-Query。 |
| C3 | `✅` | 低分配 payload 与 option 访问 | `CoapRouteContext.Payload` 已暴露 `ReadOnlyMemory<byte>`；route values 已改为数组化只读集合，无参数路由复用空集合。 |
| C4 | `✅` | routing core 抽象 | 已提取 `CoapRoutePattern`、`CoapEndpoint`、`CoapEndpointMetadataCollection`、`ICoapEndpointDataSource`、`ICoapEndpointMatcher`；`CoapRouteEndpoint` 已改为消费 endpoint 数据源。 |
| C5 | `✅` | CoAP hosting 与 Resource 注册 | 已新增 `AddCoapServer()`、`AddCoapResources()` / `AddCoapMvc()`、`CoapMvcOptions`、`app.MapCoapResources()`；server 生命周期由宿主管理，显式 route handler 降为低层扩展。 |
| C6 | `✅` | dispatcher pipeline | 已新增 `CoapRequestDispatcher`、`CoapActionInvoker`、`ICoapResult`、`ICoapResultExecutor`、统一异常和错误响应映射；支持宿主 service scope。 |
| C7 | `✅` | resource attribute routing | 已新增 `[CoapResource]` 推荐标记、`[CoapController]` 兼容标记、`[CoapRoute]`、method attributes、resource/action descriptor、application part 扫描；生成 endpoint 数据源。 |
| C8 | `✅` | model binding 与 media negotiation | 已支持 route value、query、request option、payload、`CancellationToken`、`CoapRouteContext`、远端 endpoint 参数绑定；继承 `CoapResourceBase` 时可通过 `Context` / `Payload` / `RouteValues` / `Options` 访问上下文；已补齐 Content-Format / Accept 匹配和 JSON binder 扩展点。 |
| C9 | `✅` | resource discovery | `.well-known/core` 已从 endpoint metadata 生成 CoRE Link Format；支持标题、资源类型、接口描述、content-format、observe 可见性和隐藏端点。 |
| C10 | `⬜` | filters 与安全扩展点 | 支持 endpoint filter、authorization hook、tenant/context hook；CoAP.NET 只定义接口和调用时机，不内置宿主业务策略。 |
| C11 | `⬜` | Resource/MVC 示例与迁移文档 | 提供 `AddCoapServer()`、`AddCoapResources()`、`app.MapCoapResources()`、resource class、JSON、binary payload、query option、Content-Format、Accept、Observe、发现输出和错误响应示例；说明 Resource 风格与 MVC 风格如何共存。 |
| C12 | `⬜` | 性能、AOT 与文档收口 | CoAP route benchmark、blockwise 大 payload 测试、DTLS PSK smoke、Observe smoke、trim/AOT warning 清单和发布检查清单。 |
| C13 | `⬜` | 宿主应用迁移落地 | 宿主应用移除手写 route endpoint 注册，改用 `AddCoapServer()`、`AddCoapResources()` 和 `app.MapCoapResources()`；保留自己的业务 resource、业务 DTO、授权、审计和领域服务调用。 |

## 推荐执行顺序

```text
C0 兼容基线
  -> C1 CoAP route adapter
    -> C2 async handler / result
      -> C3 低分配 payload / option
        -> C4 routing core 抽象
          -> C5 AddCoapServer / AddCoapResources / app.MapCoapResources
            -> C6 dispatcher pipeline
              -> C7 resource attribute routing
                -> C8 model binding / media negotiation
                  -> C9 resource discovery
                    -> C10 filters / security hooks
                      -> C11 示例与迁移文档
                        -> C12 性能与 AOT 收口
                          -> C13 宿主应用迁移落地
```

## C4-C10 设计细节

### C4 routing core 抽象

目标是把当前 `CoapRoute` 内部的模板解析和匹配逻辑提升为可复用模型。

- `CoapRoutePattern`：解析 `{name}`、literal segment、可选约束，负责 path 匹配和 route value 提取。
- `CoapEndpoint`：描述一个可调用 CoAP endpoint，包含 method、route pattern、handler/action、metadata。
- `CoapEndpointMetadataCollection`：保存 discovery、media type、observe、authorization、filter 等元数据。
- `ICoapEndpointDataSource`：暴露 endpoint 集合，供 route handler 和 resource 扫描共同使用。
- `ICoapEndpointMatcher`：按 method、Uri-Path、Content-Format、Accept、Observe 等条件选择 endpoint。
- `CoapRouteEndpoint`：继续作为 resource tree adapter，但只负责把 CoAP.NET resource tree 请求转给 matcher/dispatcher。

验收：

- 现有低层 route handler 测试继续通过，但示例使用通用诊断路径，不使用宿主应用业务路径。
- matcher 可单测，不需要启动 UDP server。
- 宿主应用不需要自己实现 route segment 匹配。

### C5 CoAP hosting 与 Resource 注册

目标是把宿主入口固定为常规 MVC 风格：服务注册发生在 `builder.Services`，resource 映射发生在 `app`，CoAP server 生命周期由 host 管理，让宿主应用不再显式创建 `CoapServer` 或 `CoapRouteEndpoint`。

- `AddCoapServer()`：已注册 CoAP server 配置、监听端点、可扩展 endpoint factory、后台服务和 host 生命周期。
- `AddCoapResources()`：已注册 `CoapMvcOptions`、endpoint data source、matcher 和 resource mapper；resource/controller discovery、dispatcher、binder、result executor 和 discovery provider 分别在 C6-C9 继续补齐。
- `AddCoapMvc()`：已作为 `AddCoapResources()` 的兼容别名，为后续完整 MVC 能力保留入口。
- `CoapMvcOptions`：当前承载 `ApplicationPart`、显式 endpoint 和低层 route 兼容注册；route conventions、filter、默认 discovery metadata 和 media type 策略在后续阶段扩展。
- `app.MapCoapResources()`：已像 `app.MapControllers()` 一样映射 CoAP resource endpoint，不接受裸 `IServiceProvider` 参数。
- 显式 route handler 当前通过 `CoapMvcOptions.AddRoute(...)` 或 `CoapRouteEndpoint.Create(...)` 保留给低层 handler、单文件样例和轻量测试，不作为宿主应用推荐入口。

验收：

- README 和示例中的推荐启动代码使用 `AddCoapServer()`、`AddCoapResources()` 和 `app.MapCoapResources()`。
- 普通业务 resource 的推荐路径保持为不需要 `AddApplicationPart(...)`；该扩展点仅面向插件程序集、外部模块或测试场景。
- 宿主应用可删除 `new CoapServer()`、手写 resource endpoint 注册表这类 server/resource 注册入口。
- 显式 route handler 的示例被标注为低层兼容入口。
- `CoapHostingTest` 覆盖 server DI 注册、endpoint data source/matcher 注册，以及 `MapCoapResources()` 到 resource tree 的映射。

### C6 dispatcher pipeline

目标是让“匹配后怎么调用业务代码”成为 CoAP.NET 通用能力。

- `CoapRequestDispatcher`：从 `Exchange` 或 `CoapRouteContext` 创建 endpoint invocation。
- `CoapActionInvoker`：调用 handler 或 resource action。
- `ICoapResult`：统一表达 status、payload、Content-Format、ETag、Max-Age、Location-Path、Observe 等 response 信息。
- `ICoapResultExecutor`：把 `ICoapResult` 写回 `Response`。
- 统一异常处理：未匹配、method 不允许、media type 不支持、业务异常分别映射到 CoAP response code。
- 支持 `IServiceProvider` / scope factory，但不要求所有宿主都引用 ASP.NET Core。

当前完成：

- `CoapRouteEndpoint` 已改为调用 `CoapRequestDispatcher`，不再内联 action 调用和 response 写回。
- `CoapRouteResult` 已实现 `ICoapResult`，并补齐 JSON payload 与 Observe 结果表达。
- `AddCoapResources()` 已注册 dispatcher、action invoker 和 result executor；host-managed 请求会创建并释放 request scope。
- 低层 `CoapRouteEndpoint.Create(...)` 仍可使用默认 dispatcher，不要求宿主接入 DI。

验收：

- route handler 抛异常时返回稳定的 5.xx 响应并记录日志。
- `ICoapResult` 可以无 payload、text payload、JSON payload、binary payload。
- handler 和 resource 共用同一套结果执行器。

### C7 resource attribute routing

目标是把业务项目从“手写 route 注册表”迁到“声明 resource/action”。

- resource 扫描：按 `[CoapResource]`、`*CoapResource` 命名约定识别业务 resource；`[CoapController]` 和 `*CoapController` 仅作为兼容识别，不作为新示例命名。
- route attributes：`[CoapRoute]` 定义前缀，`[CoapGet]` / `[CoapPost]` / `[CoapPut]` / `[CoapDelete]` / `[CoapObserve]` 定义 action route。
- action descriptor：已记录 method、route template、参数、返回类型、metadata，并挂到 endpoint metadata。
- application model convention：允许宿主修改 route 前缀、隐藏发现、添加默认 metadata。
- endpoint 生成：resource action 已转成 `CoapEndpoint`，并可与低层 handler route 共存。

验收：

- 示例 resource 可注册 `sensors/{sensor}/readings`、`diagnostics/{target}/status` 等通用路径。
- 业务项目不需要直接引用 `IResource`；需要协议上下文时优先继承 `CoapResourceBase`。
- resource route 与 handler route 可以共存。

### C8 model binding 与 media negotiation

目标是让 resource action 参数由 CoAP.NET 绑定，不让业务层手动拆协议细节。

- route value 参数：`string device`、`Guid id`、`int point`。
- query 参数：从 Uri-Query 绑定简单类型和集合。
- option 参数：Content-Format、Accept、Observe、ETag、Block option。
- payload 参数：`ReadOnlyMemory<byte>`、`Stream`、`JsonDocument`、DTO。
- 特殊参数：`CoapRouteContext`、`CancellationToken`、远端 endpoint 信息；继承 `CoapResourceBase` 的业务 resource 可直接访问 `Context`。
- media negotiation：按 action 声明的 consumes/produces metadata 和请求 option 匹配。

当前完成：

- 已新增 `[CoapFromRoute]`、`[CoapFromQuery]`、`[CoapFromOption]`、`[CoapFromPayload]`、`[CoapConsumes]` 和 `[CoapProduces]`。
- resource action 已支持简单类型 route/query 绑定、query 集合绑定、常用 option 绑定、raw payload、`Stream`、`JsonDocument` 和 JSON DTO payload 绑定。
- `CoapRouteContext` 已暴露 request option 快照、Observe、ETag、Block1/Block2、token 和远端 endpoint；`CoapResourceBase` 暴露常用上下文属性。
- endpoint matcher 已按 `Content-Format` / `Accept` 与 consumes/produces metadata 做协商，不匹配时返回稳定的 4.15 或 4.06。
- 已提供 `ICoapJsonPayloadBinder` 扩展点；默认实现使用 `System.Text.Json`，Native AOT/source-generated 场景可替换 binder。

验收：

- JSON DTO 可直接从 UTF-8 payload 反序列化。
- 不支持的 Content-Format 返回 `4.15 Unsupported Content-Format` 或约定的 `4.06 Not Acceptable`。
- 宿主应用的 token、payload、route value 读取逻辑可以逐步从手写上下文迁到 action 参数。

### C9 resource discovery

目标是让注册发现属于 CoAP.NET，而不是宿主应用手写 `.well-known/core`。

- 从 endpoint metadata 生成 CoRE Link Format。
- 支持 `rt`、`if`、`ct`、`obs`、title 等常用属性。
- 支持隐藏 endpoint，避免管理或内部入口暴露给设备。
- 支持 resource/action attribute 声明 discovery metadata。
- 与原 `DiscoveryResource` 共存，必要时由 discovery resource 查询 endpoint 数据源。

验收：

- `.well-known/core` 能列出 route/resource endpoint。
- 隐藏 endpoint 不出现在 discovery 输出。
- Observe endpoint 标记 `obs`。

### C10 filters 与安全扩展点

目标是给宿主业务提供治理入口，同时保持 CoAP.NET 不理解业务域。

- endpoint filter：请求前后处理、短路响应、日志、审计桥接。
- authorization hook：CoAP.NET 识别 metadata 和调用 hook，宿主实现策略。
- context hook：宿主可注入 tenant、device、gateway、edge runtime 等上下文。
- 高风险业务动作仍由宿主做人类确认、租户隔离和审计。

验收：

- 宿主应用可以把 access token、身份、领域对象校验挂到 filter 或 resource service，而不是写在 CoAP.NET。
- CoAP.NET 不引用任何宿主应用的 contracts、data 或数据库包。

## 宿主应用迁移计划

宿主应用侧要迁出的是“通用框架工作”，不是业务路径本身。

| 当前宿主集成内容 | 迁移方向 | 最终归属 |
| --- | --- | --- |
| 手动把业务模板注册成 `IResource` | 替换为 `AddCoapServer()` + `AddCoapResources()` + `app.MapCoapResources()` | CoAP.NET |
| route segment 匹配和 route values 提取 | 由 `CoapRoutePattern` 和 matcher 处理 | CoAP.NET |
| method/content-format/accept/observe 匹配 | 由 endpoint matcher 和 media negotiation 处理 | CoAP.NET |
| `.well-known/core` 发现输出 | 由 endpoint metadata 生成 | CoAP.NET |
| 业务路径 | 放到业务 resource attribute | 宿主应用 |
| access token、权限、租户、领域对象、消息发布、审计 | 业务 service、filter 或 resource action | 宿主应用 |
| 宿主领域语义 | 保持在宿主应用控制面 | 宿主应用 |

宿主应用第一轮目标：

1. 新增业务 CoAP resource，声明宿主自己的 resource 路径。
2. resource 内调用现有业务服务，先不重写业务逻辑。
3. 旧的 `CoAPService` 或手写 server 启动逻辑逐步退化为 CoAP.NET hosting 内部实现，宿主只通过 `AddCoapServer()` 配置端口和传输，通过 `app.MapCoapResources()` 映射业务 resource。
4. 测试覆盖 resource 注册、匹配、业务调用和错误响应后，删除旧手写 route 注册。
5. 后续新增业务入口时继续增加 resource/action，而不是增加手写路由分发器。

## 验收标准

- 原 Resource 风格 server 示例继续可用。
- 新 route 风格不要求应用代码继承 `Resource`。
- handler 内读取 payload 不需要先转成 string；JSON DTO 绑定可直接从 UTF-8 payload 反序列化。
- `async void DoPost` 不出现在新 route 入口；异常能被 adapter 捕获并转成 CoAP response。
- Blockwise、Observe、DTLS PSK 与 route adapter 的组合有 smoke 测试。
- CoAP.NET 能独立测试 endpoint 注册、匹配、分发和发现，不依赖任何宿主应用。
- 宿主应用最终只保留业务 resource/action、业务 DTO、领域服务、权限和审计，不再拥有 CoAP routing framework。
- `.well-known/core` 的 route/resource 发现输出来自 CoAP.NET metadata。

## 不做

- 不删除 CoAP.NET 标准 Resource 树，也不把 RFC 语义改名。
- 不把 CoAP 强行建模成其他消息协议；CoAP method、Content-Format、Accept、Observe、Blockwise 和 DTLS 都保持协议原貌。
- 不在 CoAP.NET 包内直接访问宿主应用数据库或业务服务。
- 不把任何宿主应用的领域对象或注册语义写死进 CoAP.NET。
- 不把实时规则链、OTA、配置 rollout 或长任务流程塞进 CoAP.NET dispatcher。
