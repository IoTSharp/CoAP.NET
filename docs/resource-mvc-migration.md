# Resource/MVC 示例与迁移指南

本文是 C11 的交付文档，说明 CoAP.NET 中标准 Resource 风格和新的 host-managed Resource/MVC 风格如何共存，以及宿主应用如何迁移到 `AddCoapServer()`、`AddCoapResources()` 和 `app.MapCoapResources()`。

## 推荐启动方式

新宿主代码只负责注册 CoAP server、注册 resource 能力、映射 resource endpoint：

```csharp
builder.Services.AddCoapServer(options =>
{
    options.ListenAnyIP(5683);
});
builder.Services.AddCoapResources(options =>
{
    options.AddEndpointFactory(MyGeneratedCoapEndpoints.Create);
});

var app = builder.Build();
app.MapCoapResources();
app.Run();
```

Native AOT 宿主应由 source generator 生成 endpoint factory，并通过 `AddEndpointFactory(...)` 注册。非 AOT 宿主若暂时使用运行时 attribute scanning，可显式启用 `AddReflectionResourceDiscovery()`；`AddApplicationPart(...)` 仍只用于插件程序集、外部模块或测试场景。

宿主项目引用 `IoTSharp.CoAP.NET.SourceGeneration` analyzer 包后，编译期会根据 `[CoapResource]`、`[CoapGet]`、`[CoapPost]`、`[CoapPut]`、`[CoapDelete]` 和 `[CoapObserve]` 生成 `MyGeneratedCoapEndpoints.Create(...)`，运行时不需要扫描程序集发现业务 resource。

## Resource class 示例

完整可编译示例位于 `CoAP.Example/CoAP.ResourceMvc`：

```powershell
dotnet run --project CoAP.Example/CoAP.ResourceMvc/CoAP.ResourceMvc.csproj
```

核心 resource 形态如下：

```csharp
[CoapResource]
[CoapRoute("sensors/{sensor}")]
[CoapResourceTitle("Sample sensor")]
[CoapResourceType("sample.sensor")]
[CoapInterfaceDescription("sensor")]
public sealed class SensorCoapResource : CoapResourceBase
{
    [CoapPost("readings")]
    [CoapConsumes(MediaType.ApplicationJson)]
    [CoapProduces(MediaType.ApplicationJson)]
    public CoapRouteResult UploadReading(
        string sensor,
        [CoapFromQuery] int point,
        [CoapFromOption(OptionType.ContentFormat)] int contentFormat,
        [CoapFromOption(OptionType.Accept)] int accept,
        ReadingPayload payload,
        CoapRouteContext context)
    {
        return CoapRouteResult.Json("{\"ok\":true}");
    }
}
```

这类 resource 可通过构造函数接收宿主 DI 服务。CoAP.NET 只负责 endpoint 发现、匹配、绑定、filter、授权 hook、结果写回和发现输出；业务服务、权限、租户、审计和领域模型仍由宿主应用拥有。

## JSON payload

声明 `CoapConsumes(MediaType.ApplicationJson)` 后，DTO 参数会从 UTF-8 JSON payload 绑定：

```csharp
[CoapPost("readings")]
[CoapConsumes(MediaType.ApplicationJson)]
[CoapProduces(MediaType.ApplicationJson)]
public CoapRouteResult UploadReading(string sensor, ReadingPayload payload)
{
    return CoapRouteResult.Json("{\"ok\":true}");
}
```

Native AOT 宿主需要注册 source-generated JSON metadata：

```csharp
[JsonSerializable(typeof(ReadingPayload))]
internal sealed partial class MyCoapJsonContext : JsonSerializerContext
{
}

builder.Services.AddCoapJsonPayloadBinder(MyCoapJsonContext.Default);
```

客户端需要设置 `Content-Format: application/json`。如果请求使用其他 Content-Format，CoAP.NET 会在 action 前返回 `4.15 Unsupported Content-Format`。

## Binary payload

二进制入口使用 `ReadOnlyMemory<byte>`、`byte[]` 或 `Stream`：

```csharp
[CoapPost("snapshot")]
[CoapConsumes(MediaType.ApplicationOctetStream)]
[CoapProduces(MediaType.ApplicationJson)]
public CoapRouteResult UploadSnapshot(
    string sensor,
    [CoapFromPayload] ReadOnlyMemory<byte> payload)
{
    return CoapRouteResult.Json("{\"bytes\":" + payload.Length + "}");
}
```

## Query option

URI query 通过 `[CoapFromQuery]` 绑定，重复 query 可以绑定到数组或集合：

```csharp
public CoapRouteResult UploadReading(
    string sensor,
    [CoapFromQuery] int point,
    [CoapFromQuery("tag")] string[] tags)
{
    return CoapRouteResult.Changed();
}
```

请求示例：`coap://localhost/sensors/demo/readings?point=1&tag=fast&tag=lab`。

## Content-Format 与 Accept

`[CoapConsumes]` 约束请求 Content-Format，`[CoapProduces]` 约束响应 Content-Format。也可以把常用 option 绑定为参数：

```csharp
public CoapRouteResult UploadReading(
    [CoapFromOption(OptionType.ContentFormat)] int contentFormat,
    [CoapFromOption(OptionType.Accept)] int accept)
{
    return CoapRouteResult.Json("{\"ok\":true}");
}
```

协商失败时的默认响应：

| 场景 | 响应 |
| --- | --- |
| 路径不存在 | `4.04 Not Found` |
| 路径存在但 method 不匹配 | `4.05 Method Not Allowed` |
| Content-Format 不被 endpoint 消费 | `4.15 Unsupported Content-Format` |
| Accept 不被 endpoint 生产 | `4.06 Not Acceptable` |
| 参数或 JSON payload 无法绑定 | `4.00 Bad Request` |
| action 抛出未处理异常 | `5.00 Internal Server Error` |

## Observe

`[CoapObserve]` 声明 observable GET endpoint，并让 `.well-known/core` 输出 `obs` 属性：

```csharp
[CoapObserve("status")]
[CoapResourceTitle("Observable sensor status")]
[CoapResourceType("sample.sensor.status")]
[CoapProduces(MediaType.ApplicationJson)]
public CoapRouteResult ObserveStatus(string sensor)
{
    return CoapRouteResult.Json("{\"status\":\"online\"}")
        .WithObserve(1)
        .WithMaxAge(5);
}
```

后续持续通知仍由 resource/宿主业务状态变化决定；C11 只把 Resource/MVC endpoint 的 Observe 标记、首个响应和发现输出示例补齐。

## Discovery 输出

`.well-known/core` 由 CoAP.NET 根据 endpoint metadata 生成。示例项目会产生类似输出：

```text
</sensors/{sensor}/latest>;title="Latest sensor reading";rt="sample.sensor";if="sensor";ct=50,
</sensors/{sensor}/readings>;title="Upload JSON sensor reading";rt="sample.sensor";if="sensor";ct=50,
</sensors/{sensor}/snapshot>;title="Upload binary sensor snapshot";rt="sample.sensor";if="sensor";ct=50,
</sensors/{sensor}/status>;title="Observable sensor status";rt="sample.sensor sample.sensor.status";if="sensor if.s";ct=50;obs,
</sensors/{sensor}/fault>;title="Sample error response";rt="sample.sensor";if="sensor";ct=0
```

实际输出由 CoRE Link Format 编码，客户端应按 link-format 解析，而不是依赖纯字符串比较。

## Resource 风格与 MVC 风格共存

标准 CoAP `Resource` / `IResource` / resource tree 仍是协议栈内部的一等概念，旧代码可以继续工作：

```csharp
var server = new CoapServer();
server.Add(new HelloResource());
server.Start();
```

新的宿主推荐入口则把通用框架工作交给 CoAP.NET：

```csharp
builder.Services.AddCoapServer(options => options.ListenAnyIP(5683));
builder.Services.AddCoapResources(options =>
{
    options.AddEndpointFactory(MyGeneratedCoapEndpoints.Create);
});
var app = builder.Build();
app.MapCoapResources();
app.Run();
```

迁移时不要把宿主业务模型搬进 CoAP.NET。建议按以下顺序推进：

1. 为现有业务路径新增 `[CoapResource]` class 和 method attributes。
2. 在 resource action 内调用现有业务服务，先保留业务逻辑不变。
3. 用 `[CoapConsumes]`、`[CoapProduces]`、query/option/payload 参数替换手写协议拆包。
4. 用 host-owned filter、`ICoapRequestContextHook`、`ICoapAuthorizationHook` 承接身份、租户、审计和策略。
5. 覆盖注册、匹配、业务调用、发现输出和错误响应测试后，再删除旧手写 route/resource 注册表。

底层 `CoapRouteEndpoint.Create(...)` 和 `CoapRoute.Post(...)` 继续作为兼容、测试或适配器入口保留，但不作为宿主业务代码的推荐开发模式。
