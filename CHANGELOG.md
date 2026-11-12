# Changelog

## [Unreleased]

### Fixed — UDP interoperability audit (2026-11-13)

- 按 RFC 7252 严格拒绝截断头、Token、option、无 payload 的 marker 和非法 Empty/type/code 组合；修复版本自比较，格式失败后的 decoder 保持失败状态。可辨识的畸形 CON 使用匹配 MID 的 RST，ACK/RST、未知版本和短头不反射回复。
- 单播响应同时匹配完整 Token 和远端地址/端口，piggybacked ACK 另外校验 MID；空 ACK/RST 也绑定远端。保留组播的显式例外，不宣称完整组播实现。
- 修复 Option.IsCritical() 永远返回 false 的位掩码比较，奇数 option 为 critical、偶数为 elective；已知 option 长度和未知 critical 的 endpoint 策略仍由 #28 单独验收。
- 修复 Block1 无状态空引用、错误后继续上传、重复/乱序确认及中途降低块大小的偏移计算。保留 RFC 7959 的合法逐块 2.xx/M=0 模式；矛盾的原子响应、非法 Block2 组合和最后块 Continue 取消传输，不报告部分上传成功。
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
