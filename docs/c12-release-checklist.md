# C12 性能、AOT 与发布检查清单

本文档是 C12 的收口入口，用于在发布 `IoTSharp.CoAP.NET` 前复核 route 性能、块传输、DTLS、Observe、trim/AOT 风险和打包结果。

## 必跑验证

从 `SonnetDB/extensions/IoTSharp.CoAP.NET` 目录执行：

```powershell
dotnet build CoAP.NET/CoAP.NET.csproj -c Release
dotnet test CoAP.Test/CoAP.Test.csproj -c Release
dotnet pack CoAP.NET/CoAP.NET.csproj -c Release
```

C12 smoke 可单独执行：

```powershell
dotnet test CoAP.Test/CoAP.Test.csproj -c Release --filter FullyQualifiedName~CoapC12SmokeTest
```

覆盖项：

- `RouteEndpointHandlesBlockwiseLargePayload`：route endpoint 接收 32 KiB payload，强制走 Block1 重组后交给 `CoapRouteContext.Payload`。
- `RouteEndpointObserveSmokeEstablishesRelation`：route endpoint 的 Observe metadata、Observe 请求参数、响应 `Observe` option 和非最终响应状态。
- `RouteEndpointDtlsPskSmokeReturnsResponse`：`DtlsPskChannel` + `DtlsPskClientChannel` 通过 PSK 完成 coaps 请求/响应。

## Route Benchmark

route benchmark 位于 `CoAP.Benchmarks`：

```powershell
dotnet run -c Release --project CoAP.Benchmarks/CoAP.Benchmarks.csproj -- --filter *CoapRouteMatcherBenchmark*
```

当前基准包含：

- `MatchLastRoute`：129 个 endpoint 中匹配最后一个 route，记录 matcher 分配和耗时。
- `BuildResourceTree`：从 endpoint data source 构建 CoAP resource tree，记录启动/映射阶段成本。

发布前保留 BenchmarkDotNet 输出，用于和上一版 artifact 对比；若 matcher 或 resource tree 构建出现明显回退，应先确认是否来自 endpoint metadata、media negotiation 或 discovery 变更。

## Trim / AOT Warning 清单

`CoAP.NET/CoAP.NET.csproj` 设置 `IsAotCompatible=true`，Release build 会启用 trim/AOT analyzer。当前已知边界如下：

- `AddCoapResources()` 默认只构建显式 endpoint、route 和 endpoint factory；Native AOT 宿主应通过 source generator 或手写 factory 注册 endpoint。
- `CoapResourceEndpointBuilder` 使用 application-part reflection 扫描 resource/action；该路径只能通过 `AddReflectionResourceDiscovery()` / `AddApplicationPart(...)` 显式启用，并已标记 `RequiresUnreferencedCode` / `RequiresDynamicCode`。
- JSON DTO payload 绑定应调用 `AddCoapJsonPayloadBinder(JsonSerializerContext)` 或 `AddCoapJsonPayloadBinder(IJsonTypeInfoResolver, JsonSerializerOptions)`，由 source-generated `JsonTypeInfo` 驱动。
- `CoapSystemTextJsonPayloadBinder` 只作为非 AOT 兼容入口保留，并已标记 `RequiresUnreferencedCode` / `RequiresDynamicCode`。
- DTLS PSK 依赖 `BouncyCastle.Cryptography`，只位于 CoAP.NET 传输层；发布 coap-only 宿主时可通过宿主配置关闭 coaps 监听。

发布前 `dotnet build CoAP.NET/CoAP.NET.csproj -c Release` 不应产生新的 IL2026、IL2070、IL3050 或 IL3053 warning。不得用 `UnconditionalSuppressMessage` 或 `#pragma` 强行屏蔽 IL warning；无法静态证明安全的路径必须标记 `RequiresUnreferencedCode` / `RequiresDynamicCode`，并提供 generated endpoint / source-generated JSON 替代路径。

## 发布检查

- README 的推荐宿主入口仍是 `AddCoapServer()`、`AddCoapResources()`、`app.MapCoapResources()`。
- 示例和文档没有把 `AddCoapControllers()` / `MapCoapControllers()` 作为推荐入口。
- 低层 `CoapRouteEndpoint.Create(...)` 只作为兼容、测试或 benchmark 入口出现。
- `dotnet pack` 输出位于 `CoAP.NET/artifacts`，包内包含 `README.md`、`LICENSE` 和 NuGet 图标。
- C12 smoke、完整 `CoAP.Test`、Resource/MVC 示例 build 均通过后，再进入 C13 宿主应用迁移。
