# Changelog

## [Unreleased]

### Token、unsigned option 与 Observe deadline — 2026-11-13

- #33 的自动请求 Token 改为平台 CSPRNG 生成八字节，保留显式 Token、重传、Blockwise 与 Observe 身份。当前 channel/exchange 没有真实 DTLS session/traffic-key/re-key identity，RFC 9175 secure-context non-reuse 仍 UNKNOWN，issue 保持开放。
- #34 新增 `Option.UIntValue`、`Message.Size1Unsigned` / `Size2Unsigned`，覆盖完整 UInt32 范围；旧 signed API 和签名保留，typed raw getter 对过长值抛 `OverflowException`。Resource/MVC 反射与生成绑定支持 `uint` / `uint?`；组装保留只在首块出现的 Size 提示，不以提示预分配或替代 M 位。
- #35 的 Observe 重注册采用 monotonic deadline、单一 `ITimer` 和最长 `Int32.MaxValue` 毫秒分片，支持 `UInt32.MaxValue` Max-Age。通知更新 deadline，取消、终态、Clear、Stop、Dispose 释放 timer。
- [本批审计](docs/audits/coap-20261113/next-batch-validation.json)记录原始 FAIL 与本地回归；真实 peer、NativeAOT 实物、公开包与现场验收保持独立，#26 双实现矩阵为 NOT_RUN。

### Modern option semantics and lifecycle — 2026-11-13

- 按用户批准的兼容选择，默认 AddOption/codec 不再把 wire number 19 转成消息头 Token；旧 Token 名称保留并弃用，迁移到 Message.Token。QBlock1/2 仅登记现行名称/格式，默认未启用其处理器。
- 显式 legacy codec 保留受测 Token 往返并拒绝 raw19 歧义，默认在所有受测旧符号构建下仍为 RFC7252；现代与 Draft18 端点缓存隔离。
- 新增 endpoint 选项定义/注册，派发前按长度、方向、重复、UTF-8 和语义校验；critical 请求/响应按消息类型拒绝，非法 elective 被忽略，unknown elective 保留。默认不支持代理时返回5.05，真实代理宿主需显式启用 SupportsProxying。
- 分块状态、计时器、事件登记在成功/错误/取消/超时及 endpoint Clear/Stop/Dispose 时回收，原调用方收到明确终态。终止通知保持锁外且异常清理继续执行；旧排队代次被拒绝，可重入清理不复活传输或丢失新请求。
- 端点同时退休小请求和非分块观察，释放自有重传/重注册context，旧outbox绑定原始代次。Matcher按expected exchange原子删除Token/MID/URI，避免旧完成删掉replacement映射；远端未发response的清理不再空引用。
- 四种终态事件（Cancelled/TimedOut/Rejected/Completed）即使较早用户回调抛出也通知后续清理，最后以AggregateException报告全部异常；非终态事件保持原合同。完整多播收集是既有缺口，单独跟踪#37，本批不宣称支持。
- CoapClient 普通单播成功/失败共同仲裁一次终态，区分实际 Observe 通知、注销与正常 GET fallback。异步客户端回归改为等待实际响应信号，保留原断言并去除固定100ms协调假设。
- 兼容影响、受测 legacy 范围及未完成的 Token 生成/无符号 getter/大 Max-Age 见[迁移合同](docs/coap-options-compatibility.md)；新包发行、AOT运行、真实peer与现场资格独立验收。

### Fixed — UDP interoperability audit (2026-11-13)

- 按 RFC 7252 严格拒绝截断头、Token、option、无 payload 的 marker 和非法 Empty/type/code 组合；修复版本自比较，格式失败后的 decoder 保持失败状态。可辨识的畸形 CON 使用匹配 MID 的 RST，ACK/RST、未知版本和短头不反射回复。
- 单播响应同时匹配完整 Token 和远端地址/端口，piggybacked ACK 另外校验 MID；空 ACK/RST 也绑定远端。保留组播的显式例外，不宣称完整组播实现。
- 端点键与比较统一规范化 IPv4-mapped IPv6，保留端口、原生 IPv6 ScopeId 与自定义 EndPoint 语义；避免有效响应/空 ACK/RST 因地址表示不同被丢弃。
- 修复 Option.IsCritical() 永远返回 false 的位掩码比较，奇数 option 为 critical、偶数为 elective；已知 option 长度和未知 critical 的 endpoint 策略仍由 #28 单独验收。
- 修复 Block1 无状态空引用、错误后继续上传、重复/乱序确认及中途降低块大小的偏移计算。保留 RFC 7959 的合法逐块 2.xx/M=0 模式；矛盾的原子响应、非法 Block2 组合和最后块 Continue 取消传输，不报告部分上传成功。
- 中间逐块响应附带 Block2 时只推进上传，不并发启动另一次取回或报告最终成功；已匹配的非法 NUM/SZX 明确取消并通知调用方，真正旧 MID/duplicate 保留忽略。无状态的非最终 Block1 成功被拒绝，手工 Block1 与合法 4.13 大小提示保持可用。
- CoapClient.SendAsync 将取消通知给失败回调与 Error 事件，追加 FailReason.Cancelled=2，保留 Rejected=0、TimedOut=1；同一请求的重复取消只通知一次。
- 吸纳上游 PR64 的停止日志修正和 PR80 的测试计数隔离思路，保留现有 ILogger 与测试框架。

### Documentation

- 新增[CoAP 标准与上游调查](docs/coap-standards-roadmap.md)，记录 65 个上游 issue、17 个 PR、可见 fork 分支清单、跨语言实现比较、正式 RFC/IANA 边界和 #20～#31 逐项实现队列。单元测试、真实 peer、AOT、包与现场验收分别记录。

### Changed

- 准备 `3.0.1` 补丁候选，统一核心、ASP.NET Core adapter 和 source generator 的包版本，并将包仓库地址对齐当前正式仓库。
- 候选收录主干已有的配置驱动 UDP/DTLS PSK 监听、宿主日志接入、Uri-Query 读取和关闭传输时的资源映射处理；本轮不重写这些通用实现。
- 补充 [3.0.1 候选说明](docs/release-notes/3.0.1.md)，分别记录兼容入口、验证计划和未发布状态。
- 新增组件配置监听、日志桥接和查询参数回归测试；独立参数关闭上层构建继承后，组件完整测试 124/124 通过。
- 发布流水线先构建并测试同版本程序集，再使用 `--no-build` 打包；分支仅验证和保留产物，tag 发布 job 只下载已通过测试的包，重复版本上传直接失败。

当前正式 NuGet 版本仍为 `3.0.0`；本地构建、完整测试和打包已通过，正式发布与公开消费继续独立验证。包来源使用 GitHub 返回的规范地址 `IoTSharp/CoAP.NET`，旧 origin 地址保留为可用重定向别名。

`v3.0.1` 已固定到 `1abe69a0edaf55147ad6bccaa4e012aea87a8252` 并完成 NuGet.org 发布；三个包的匿名索引、NUSPEC 仓库提交和全新 NuGet-only 消费者均已回读通过。完整回执与 CI/DLL 哈希对比见 `releases/3.0.1-20261112/publication-receipt.json`。
