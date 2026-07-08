# AGENTS.md

本文件定义 AI 协作和自动化在 CoAP.NET fork 中工作的约束。除非用户明确要求修改这些规则，否则所有代码、文档、示例和路线图变更都必须遵守本文件。

## 项目边界

CoAP.NET 是协议栈和 CoAP resource / routing / discovery 框架，不是任何宿主应用的业务层。

- 保留 CoAP/CoRE 标准语义：`Resource`、`IResource`、resource tree、resource discovery、`.well-known/core`、Observe、Blockwise、Content-Format、Accept、DTLS。
- CoAP.NET 可以提供 MVC 风格的 controller/action 编程模型，但对宿主暴露的主入口必须使用 CoAP resource 命名。
- 宿主应用只声明业务 resource/controller，并调用自己的业务服务。
- CoAP.NET 不得引用宿主应用的业务模型、数据库、租户、审计、事件总线或领域服务。

## 强制 API 约束

宿主集成 API 必须固定为：

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

必须遵守：

- `AddCoapResources()` 是宿主注册 CoAP resource/controller 能力的推荐入口。
- `app.MapCoapResources()` 是宿主映射 CoAP resource endpoint 的推荐入口。
- `AddCoapServer()` 负责 CoAP server 配置、监听端点、传输、DTLS 和生命周期。
- 普通宿主应用不需要显式调用 `AddApplicationPart(...)`；它只允许用于插件程序集、外部模块或测试场景。
- controller/action descriptor、model binding、filter、result executor 可以作为内部 MVC 编程模型存在，但文档和示例的宿主 API 必须以 Resource 命名为准。

禁止事项：

- 不得把推荐入口命名为 `AddCoapControllers()`。
- 不得把推荐入口命名为 `app.MapCoapControllers()`。
- 不得在推荐示例中让宿主业务代码 `new CoapServer()`、`server.Start()` 或直接传递裸 `IServiceProvider`。
- 不得把 `CoapRouteEndpoint.Create(...)`、`CoapRoute.Post(...)` 等低层 route handler API 作为宿主应用的推荐开发模式。
- 不得使用某个宿主应用的业务路径展示低层 route handler 示例；低层示例只能使用通用诊断或协议示例路径。

## MVC Routing 约束

- CoAP.NET 内部可以使用 controller、action、endpoint data source、route pattern、matcher、dispatcher、binder 和 result executor 等概念。
- Attribute routing 可以使用 `[CoapController]`、`[CoapRoute]`、`[CoapGet]`、`[CoapPost]`、`[CoapPut]`、`[CoapDelete]`、`[CoapObserve]`。
- 业务 endpoint 的注册、匹配、分发、结果执行和 resource discovery 必须在 CoAP.NET 内实现。
- `.well-known/core` 必须由 CoAP.NET 根据 endpoint metadata 生成，不得要求宿主应用手写发现输出。
- 认证、授权、租户、身份、审计、领域对象等策略只通过扩展点交给宿主实现，不能写死在 CoAP.NET。

## 宿主集成约束

宿主应用集成目标是：

- 宿主应用使用 `AddCoapServer()` 配置 CoAP 监听和传输。
- 宿主应用使用 `AddCoapResources()` 注册 CoAP resource/controller 能力。
- 宿主应用使用 `app.MapCoapResources()` 映射业务 resource/controller。
- 宿主应用保留自己的业务 controller/action、DTO、领域服务、权限和审计。
- 宿主应用不应拥有 CoAP routing framework、resource discovery、route matcher 或手写 resource endpoint 注册表。

## 文档约束

- README、ROADMAP、示例和测试说明中的推荐宿主 API 必须使用 `AddCoapResources()` / `app.MapCoapResources()`。
- 只有在说明底层兼容或内部实现时，才可以提到 `CoapRouteEndpoint`、`CoapRoute` 或 resource tree adapter。
- 文档必须清楚区分 CoAP 标准 Resource 与宿主业务对象，不能把 CoAP Resource 当作任何特定宿主应用的领域模型。
