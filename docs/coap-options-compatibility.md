# 现代 option 合同与 legacy 迁移

本页记录 2026-11-13 用户批准的选择 A。该增量属于 Unreleased；正式包版本与发布另行验证。

## Header Token 与 wire option 19

- `Message.Token` 始终表示消息头 Token。
- `AddOption` 保存原始 option，不再把 number 19 当作设置消息头 Token 的快捷方式。
- public `OptionType.Token = 19` 保留名称与数值以保持二进制身份，同时标为弃用。已有源码应迁移到 `Message.Token`；使用 warnings-as-errors 的旧调用会看到明确的迁移提示。
- `QBlock1 = 19`、`QBlock2 = 31` 是当前 IANA 的名称和 uint 格式元数据。它们不代表本库已实现 Q-Block 传输。
- 默认 endpoint 没有注册 Q-Block 处理器，所以带 19/31 的 CON 请求返回 4.02，NON 请求被拒绝而不派发业务。原始 codec 仍保留其编号和值，供工具和真正理解该扩展的宿主使用。

例如，header Token 为 `aa55`、option 19 值为 `08` 的报文保持为 `42 02 00 01 aa 55 d1 06 08`，不会再变成 header Token `08`。

```csharp
Request request = Request.NewPost();
request.Token = new byte[] { 0xaa, 0x55 };
// 只有宿主真正实现该扩展时才应向 endpoint 注册并使用它。
request.AddOption(Option.Create(OptionType.QBlock1, new byte[] { 0x08 }));
byte[] bytes = Spec.Encode(request);
```

## 显式 legacy codec

默认 codec 与 endpoint 始终采用 RFC 7252，包括启用旧符号的构建。默认发行包不额外引入全部历史 codec。

需要旧 draft 的工具可在适用的 COAP03/08/12/13/COAPALL 构建中，显式使用 `Spec.Draft03`、`Draft08`、`Draft12`、`Draft13`。03/08/12 的旧 Token option 转换只在各自 codec 内发生。旧 codec 遇到会混淆 header Token 的 raw 19 时明确拒绝，不静默删除。现代与 Draft18 端点缓存分离，避免共享 Executor、Config 或 MessageDeliverer。

```csharp
// 此示例需要 COAPALL 等包含 Draft12 的构建。
Request request = Request.NewGet();
request.ID = 1;
request.Token = new byte[] { 1, 2 };
byte[] bytes = Spec.Draft12.Encode(request);
Message result = Spec.Draft12.Decode(bytes);
```

这只保留明确受测的历史兼容入口，不代表所有旧草案的完整协议或互操作验收。

## 入站 option 校验

原始 codec 与 endpoint 的能力策略分别处理。每个 endpoint 的 `OptionRegistry` 只声明实际理解的 option 元数据，包括格式、长度、方向、重复规则与可选校验委托。知道一个 IANA 编号、定义了枚举名称、实现了协议处理器是不同事实。

- 未知或非法 critical CON 请求返回 4.02；critical NON 请求静默拒绝。
- critical CON 响应返回 RST；ACK/NON 响应静默丢弃，并保留原请求的等待/重传状态。
- 未知 elective option 保留 raw 值；已知但非法或多余的 elective occurrence 按 RFC 7252 忽略。
- 字符串检查严格 UTF-8，整数先检查已知长度，避免过长 Content-Format 被截断为另一个值。
- If-None-Match 不限定 PUT；Location-Path 的点段被过滤。注册表不会代替资源实现 ETag 条件判断或宿主业务策略。
- 代理选项是已知语法。默认 `SupportsProxying=false` 返回 5.05；只有宿主已经提供实际代理分发器时才显式启用。开启这个开关不等于完整代理协议实现。

自定义处理器通过显式 `CoapOptionDefinition` 与 `Register` 登记，不依赖反射/Activator；同一 endpoint 内不能覆盖已有标准定义。应用应把自定义语义校验器写成有界、可重入的同步校验逻辑。

## 分块生命周期与调用方终态

成功、错误、取消、超时、Clear/Stop/Dispose 都释放对应分块状态、计时器和登记。正常 Observe 通知释放当前组装体，保留继续通知与取消所需的最小上下文。关闭后的旧排队工作有代次校验；公开回调后、下一块发送和交付前重新检查终态。

清理先释放整个快照，再在 exchange 锁外通知。用户回调抛异常时仍处理其余交换并保证子分块取消，最后把同步错误汇总抛出。异步清理通知没有同步调用方，异常通过 ILogger 记录。Dispose 即使通知失败也继续释放传输和 Matcher。

端点在始发请求入队前登记同一 exchange，也跟踪小请求与非分块观察，原始代次不会在旧 outbox 回调中被重新绑定。退休时先释放整个旧快照的 Blockwise/Observe/Reliability 自有 context，再发布首个用户通知，不遍历 Dispose 宿主任意附加属性。Matcher 的 Token、MID、URI 清理原子比较当前 owner，旧交换的完成不能删除替代交换的映射。

Cancelled、TimedOut、Rejected 与 Exchange.Completed 会按原注册顺序通知全部订阅者，最后以 AggregateException 报告回调异常；这改变了终态回调异常的外观，保留了内部全部异常。Respond、Responding、Acknowledged 等非终态事件的原异常合同不变。

普通单播成功和失败共同仲裁终态；失败后不再交付成功。实际 Observe 通知可有多个响应，注销和没有建立观察的普通 GET fallback 会封口。回调门禁保留 multicast 扩展分支，但协议栈的有界多端收集尚未完成，不能把这个分支当作完整多播支持。清理回调可以发起新请求，其 Matcher 登记不会被随后清空。

这些确定性代码回归不代替高并发/现场资格、真实 peer、NativeAOT 实物或长期安全验收。

## 明确保留的后续项

- [#33](https://github.com/IoTSharp/CoAP.NET/issues/33)：自动 Token 已改为八字节平台 CSPRNG，显式 Token 和已有 exchange 身份保持不变。`UseRandomTokenStart` API 保留，但不再使自动 Token 确定化。当前 `IChannel` / `Exchange` 不提供真实 DTLS session、traffic-key 或 re-key identity，因此 secure-context non-reuse 仍 UNKNOWN；不以 URI 推断绑定，也没有增加无界历史集合。
- [#34](https://github.com/IoTSharp/CoAP.NET/issues/34)：新增 `Option.UIntValue`、`Option.Create(OptionType, UInt32)` 与 `Message.Size1Unsigned` / `Size2Unsigned`，保留 `0..0xffffffff`；旧 `Int32` getters 继续返回四字节 signed bit pattern。已知整数 option 的 overlong raw getter 现在抛 `OverflowException`，而不是截断。未知 option 的 `IntValue` / `LongValue` 分别最多四/八字节。Resource/MVC 反射和生成路径支持 `uint` / `uint?`；Blockwise 首块 Size hint 在组装时保留，不用于预分配或替代 M 位。
- [#35](https://github.com/IoTSharp/CoAP.NET/issues/35)：Observe 使用 `TimeProvider` 的 monotonic deadline 与单一有界 timer 分片，后续通知刷新 deadline；取消、终态、Clear、Stop、Dispose 解绑并释放 timer。原构造函数保留，新增 clock/provider 重载。`UInt32.MaxValue` 通过确定性 fake-time 测试，未等待真实墙钟。
- [#37](https://github.com/IoTSharp/CoAP.NET/issues/37)：有界多播收集、两个端点及每端分块状态；当前首个普通响应结束匹配的既有缺口保持开放。
- [#24](https://github.com/IoTSharp/CoAP.NET/issues/24)、[#25](https://github.com/IoTSharp/CoAP.NET/issues/25)、[#26](https://github.com/IoTSharp/CoAP.NET/issues/26)：常用扩展与双实现互操作矩阵。

本批原始失败、修复后 focused/完整/legacy 结果和 #26 peer 条件见 `docs/audits/coap-20261113/next-batch-validation.json`。这些代码回归不等同于真实 DTLS peer、NativeAOT 实物、公开包或现场长稳验收。
