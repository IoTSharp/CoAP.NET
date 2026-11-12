# CoAP.NET 标准、上游与实现差距

核查日期：2026-11-13（Asia/Shanghai）。源码基线：`1abe69a0edaf55147ad6bccaa4e012aea87a8252`（v3.0.1）。目标是独立协议框架的 UDP/DTLS 设备接入与互操作；宿主业务仍使用 `AddCoapServer`、`AddCoapResources`、`MapCoapResources`。

## 结论与范围

当前库具备 UDP、DTLS 1.2 PSK、四种基础方法、Resource/发现、Observe、Block1/Block2、现代 .NET 宿主与 AOT 分析入口。但已有入口不等于完整规范合规。第一轮发现并复现了报文截断补零、错误版本被接受、响应来源/标识匹配不完整以及 Block1 错误续传。它们应先于增加协议数量收口。

本轮不是“全 GitHub 已穷尽”的声明。GitHub fork API 返回 75 个可见条目；GraphQL 能读取其中 73 个仓库的 301 个分支引用，去重后 47 个 head；两个历史仓库无法解析，保留 UNKNOWN。18 个 head 已是基线祖先。对 23 个有价值的非祖先候选做了比较；旧草案、站点和两条 2021 年 Dependabot 分支不作为现代功能候选。大型 compare 的文件列表最多返回 300 项，提交摘要最多读取 30 项，不能把这些视作大分支逐文件审计完成。

补充检索以 `topic:coap archived:false`、star 降序读取最多 100 个候选，覆盖 21 种明确标注的语言（另有未标注项）。[候选元数据](audits/coap-20261113/topic-survey.json)包含协议栈、codec、LwM2M、平台、测试工具和应用；它们不是 100 套独立协议实现，topic 检索也会漏掉没有标注 topic 的 libcoap 等项目。采纳判断以规范、源码和测试合同为依据，不以 star 数排名。

上游全部 82 个 issue API 条目中有 65 个普通 issue、17 个 PR，已逐条按原始标题/正文摘要分流。完整来源及判断见 [issue 清单](audits/coap-20261113/issue-decisions.json)、[PR 清单](audits/coap-20261113/upstream-prs.json)、[fork 引用](audits/coap-20261113/fork-branches-final.json)与 [fork 候选比较](audits/coap-20261113/fork-review.json)。

## 最新正式标准与常见能力

标准来源是 [RFC Editor](https://www.rfc-editor.org/rfc/rfc7252.json) 和 [IANA CoRE Parameters](https://www.iana.org/assignments/core-parameters/core-parameters.xml)。本轮读取到 IANA 更新时间为 2026-08-11。RFC 7252 的更新链包含 RFC 7959、8613、8974、9175、9876。RFC 9876 是 Content-Format 注册流程更新，不是新的传输协议门禁。RFC 9668 规定 EDHOC 与 CoAP/OSCORE 的集成；RFC 9528 规定 EDHOC 密钥交换本身。

| 能力 | 依据 | 基线现状 | 推进判断 |
| --- | --- | --- | --- |
| UDP 核心协议 | RFC 7252 | 代码与测试存在，负面边界不足 | P0：#20、#21 |
| Observe | RFC 7641 | 有注册、通知与取消入口 | 继续补序号回绕、重复/失序、取消和双实现互操作 |
| Block1/Block2 | RFC 7959 | 有实现和正常传输测试，错误/乱序边界有缺陷 | P0：#22 |
| 发现与 Link Format | RFC 6690 | Resource 与 MVC metadata 生成发现输出 | 保持标准语义并补 wire vectors |
| DTLS PSK | RFC 7252 §9、DTLS 1.2 | 有客户端/服务端、显式监听与关闭入口 | P0：#26 双实现矩阵 |
| Echo/Request-Tag | RFC 9175 | 无实现 | P1：#24 |
| FETCH/PATCH/iPATCH | RFC 8132 | 只有 GET/POST/PUT/DELETE | P1：#25；必须贯通 client、Resource/MVC 和媒体协商 |
| OSCORE | RFC 8613 | 无 context/replay 实现 | 正式但可选的安全能力；独立实现与评审 |
| EDHOC over CoAP | RFC 9668 | 无实现 | 随 OSCORE 场景单独规划 |
| CoAP over TCP/TLS/WebSockets | RFC 8323 | 无 transport | 后续独立传输能力 |
| Q-Block | RFC 9177 | 无实现，旧枚举19与现代注册冲突 | #23 先修 registry，再决定完整 Q-Block；不能只加常量 |
| Extended Tokens | RFC 8974 | 默认 TKL 0–8 字节 | 可选扩展，须单独容量/无状态客户端合同 |
| No-Response | RFC 7967，Informational | 无专门实现 | 常见可选扩展，不能误称核心强制 |
| Group Communication | RFC 7390，Experimental | 只有局部组播字段/匹配例外 | 需要接口选择、多响应与安全边界；不可称完整支持 |
| Hop-Limit/4.29 等 | RFC 8768/RFC 8516 | 未形成完整合同 | 作为代理、过载语义的独立增量 |

现有 Bouncy Castle C# 2.6.2 虽声明 `DTLSv13` 常量，但 `CLIENT_LATEST_SUPPORTED_DTLS` 与 `SERVER_LATEST_SUPPORTED_DTLS` 的上界都是 DTLS 1.2。[提供者源码](https://raw.githubusercontent.com/bcgit/bc-csharp/release-2.6.2/crypto/src/tls/ProtocolVersion.cs)不支持“一行改版本就完成 DTLS 1.3”的结论。证书/RPK、Connection ID、密钥轮换、配额和 1.3 提供者评估见 #27。

## 哪些上游内容值得吸收

| 来源 | 判断 | 原因 |
| --- | --- | --- |
| [smeshlink PR64](https://github.com/smeshlink/CoAP.NET/pull/64) | 采纳窄修复 | Stop 日志仍错误写成 Starting；适配现有 ILogger 即可 |
| [smeshlink PR80](https://github.com/smeshlink/CoAP.NET/pull/80) | 采纳测试隔离思路 | Observe 通知计数应在每个测试前重置，避免顺序依赖 |
| [smeshlink PR81](https://github.com/smeshlink/CoAP.NET/pull/81) | 不直接合并 | 全局广播/地址重用改变默认 Socket 合同，并吞掉配置异常；有需求时应显式 opt-in |
| [smeshlink PR53](https://github.com/smeshlink/CoAP.NET/pull/53) | 拒绝直接合并 | 作者明确说未完成，使用自选 OSCOAP option、缺 Observe/Blockwise，早于现行 RFC8613 |
| [world-direct develop](https://github.com/world-direct/CoAP.NET/tree/develop) | 逐提交研究 | Blockwise timeout、重复 ACK、竞态修复有参考价值；分支混入改名、API 重写和大量文件 |
| [world-direct hotfix/block1](https://github.com/world-direct/CoAP.NET/tree/hotfix/block1) | 用回归吸收机制 | 重复确认、MID 重用、重传向原请求转发等应逐一重现，不能照搬整个分支 |
| [IoTSharp develop](https://github.com/IoTSharp/CoAP.NET/tree/develop) | 不直接合主干 | 含未完成的客户端/Span/codec 重写；不能把新主干退回另一套半成品 |
| 历史 .NET4/Mono/DNX/SDK 迁移分支 | 不重复采纳 | 当前主干已 .NET10/SDK/ILogger，迁移旧项目不会增加现行协议能力 |

上游 issue 的重要线索包括 Observe/ACK（#79/#78/#32）、取消/长时间睡眠（#48/#54）、组播多响应与网卡（#66/#65/#70）、Reject/RST（#61）、重传配置（#63）、内存/计时器（#57/#67）、Blockwise 范围（#13）和 NAT（#40）。这些是复现候选，不能仅根据标题就认定当前主干仍有相同缺陷。WCF、Compact Framework、Unity、串口和集中式 Observe 状态需要明确消费者和宿主合同。

## 跨语言项目值得学习什么

| 实现 | 语言 | 可学习机制 | 本轮证据边界 |
| --- | --- | --- | --- |
| [libcoap](https://github.com/obgm/libcoap) | C | 明确 RFC 列表、option registry、多 TLS backend、fuzzing、interop fixtures | README/源码调研；未运行该 peer |
| [Californium](https://github.com/eclipse-californium/californium) | Java | Plugtest、DTLS 会话与 NAT/CID、OSCORE context/replay | 文档与源码；未连接其公网测试服务 |
| [aiocoap](https://github.com/chrysn/aiocoap) | Python | asyncio transport/context 分层，准确披露 Observe/DTLS/RD/EDHOC 的部分边界 | 文档调研；部分网络读取失败原样保留 |
| [go-coap](https://github.com/plgd-dev/go-coap) | Go | UDP/DTLS/TCP 分层、Echo/Request-Tag、opt-in Q-Block、libcoap 对拍 | 当前 README 明确标注 Q-Block 为实验性实现 |
| [node-coap](https://github.com/coapjs/node-coap) | Node.js | 以独立 coap-oscore 组件管理安全 context，明确 OSCORE-only policy | 外部包能力不能直接推断为本库能力 |
| coap-rs / CoAPthon3 | Rust / Python | 精简 codec、异步状态与已有缺口表达 | 辅助代表，不以仓库数量或 star 决定采纳 |

源码采纳必须保留许可与署名。CoAP.NET 的 LICENSE 是带三条条件的 BSD 风格文本；GitHub 显示 NOASSERTION 仅说明检测未识别。不能据此说没有许可，也不能从一个 SDK 的许可推断整个项目许可。

### 受限设备、codec 与测试生态补充

| 实现 | 可核对机制 | 采纳与边界 |
| --- | --- | --- |
| [Zephyr CoAP](https://github.com/zephyrproject-rtos/zephyr/blob/main/include/zephyr/net/coap.h) | packet/pending/observer API、相关 RFC option 定义、[OSCORE context 池](https://github.com/zephyrproject-rtos/zephyr/blob/main/subsys/net/lib/coap/coap_oscore.c)、`tests/net/lib/coap` | 学习显式内存上限与上下文数量。Apache-2.0。option/API 存在不能证明所有 transport/安全 profile 均已运行验收；EDHOC 本轮 UNKNOWN。 |
| [Contiki-NG](https://github.com/contiki-ng/contiki-ng/tree/develop/os/net/app-layer/coap) | 固定事务/observer 池、CON 初始随机超时与指数退避、超限释放事务并回调失败 | 学习受限资源与失败回收。相关 CoAP 源码是 BSD 三条款；配置默认值可覆盖。测试/DTLS 运行 NOT_RUN。 |
| [RIOT gcoap/nanocoap](https://github.com/RIOT-OS/RIOT/tree/master/sys/net/application_layer) | nanocoap 是 codec 工具箱；gcoap 才包含线程、UDP 事件、Observe/Blockwise helper；[bounds 测试](https://github.com/RIOT-OS/RIOT/blob/master/tests/unittests/tests-nanocoap/tests-nanocoap.c) | 学习 PDU/option/URI/待响应数量预算与空闲 DTLS 会话淘汰。相关源码 LGPL-2.1-only，不能直接复制并继续只标现有 BSD 许可。 |
| [NZSmartie/CoAP.Net](https://github.com/NZSmartie/CoAP.Net) | 显式 [OptionFactory](https://github.com/NZSmartie/CoAP.Net/blob/master/src/CoAPNet/Options/OptionFactory.cs) 区分 unknown critical 与 opaque elective，允许自定义注册 | Apache-2.0。README 与 DTLS/Observe 项目状态不同步；不能据此宣布完整支持。反射/Activator 注册不能照搬到本库 AOT 路径，可用显式 factory。 |
| [coap-lite](https://github.com/martindisch/coap-lite) | Rust `Unknown(u16)`/原始重复 option 保真、checked-add、明确错误类型与输出尺寸限制 | 是 codec，不是完整协议栈；MIT OR Apache-2.0，manifest 标注 passively-maintained。本轮静态看到尾部 marker 被接受为空 payload，因此也不能作为无需验证的正确性 oracle；运行 NOT_RUN。 |
| [coap-rs](https://github.com/Covertness/coap-rs) | 基于 coap-lite/Tokio 的 client/server，Observe 注册失败清映射、每通知独立续块模板、取消保留 query、有界重试 | MIT。README/Cargo/源码能对应 DTLS feature 与可选 EDHOC，但本轮并未运行。学习状态隔离与恢复测试，不照搬 Rust unsafe 到本库。 |
| [Leshan](https://github.com/eclipse-leshan/leshan) | [LockStep 报文/ACK 故障脚本](https://github.com/eclipse-leshan/leshan/blob/master/leshan-integration-tests/src/test/java/org/eclipse/leshan/integration/tests/lockstep/LockStepTest.java)、不同 transport provider 的 PSK 参数化测试 | EPL-2.0 OR BSD-3-Clause 双许可。LwM2M 是 CoAP 上层，不把 Bootstrap/设备对象模型强加为本库合规门禁。 |
| [Anjay/avs_coap](https://github.com/AVSystem/Anjay) | [可重放的 API 调用序列 fuzz 输入](https://github.com/AVSystem/Anjay/blob/master/deps/avs_coap/doc/sphinx/source/Fuzzing.rst)、重传/离线/分块场景 | 当前 root 与 avs_coap LICENSE 都是 Non-Commercial License，README 说明 v3.10.0 起商用须商业许可。当前版本不作为可直接吸收的 BSD 开源代码；只独立实现测试思想，历史版本许可未核定。 |
| [xk6-coap](https://github.com/golioth/xk6-coap) | 有界 VU/时长、PSK/ECDSA、timeout、Observe 到期取消；源码使用 dtls.Dial | Apache-2.0。复用 go-coap/pion，是负载工具，不是新的独立 peer。分别计量新握手与复用会话，并补 RTT/错误/通知量指标。 |

这些补充为只读文档/源码证据。固定资源默认值、维护时间和 feature 状态都绑定读取的分支快照，不能替代本库实测；也不把“有 OSCORE 源码”扩大为完整 EDHOC 或生产安全证明。

## 逐项实现队列

- [#20](https://github.com/IoTSharp/CoAP.NET/issues/20)：格式错误报文与解码失败隔离。
- [#21](https://github.com/IoTSharp/CoAP.NET/issues/21)：endpoint + MID + 完整 Token 匹配。
- [#22](https://github.com/IoTSharp/CoAP.NET/issues/22)：Block1 状态、错误、重复/乱序、SZX 与组合响应。
- [#23](https://github.com/IoTSharp/CoAP.NET/issues/23)：IANA registry、旧 Token option19、critical 位和 Token 合同。
- [#24](https://github.com/IoTSharp/CoAP.NET/issues/24)：Echo/Request-Tag。
- [#25](https://github.com/IoTSharp/CoAP.NET/issues/25)：FETCH/PATCH/iPATCH。
- [#26](https://github.com/IoTSharp/CoAP.NET/issues/26)：libcoap/Californium 的 UDP/DTLS PSK 双实现门禁。
- [#27](https://github.com/IoTSharp/CoAP.NET/issues/27)：证书/RPK/DTLS1.3 能力与提供者合同。
- [#28](https://github.com/IoTSharp/CoAP.NET/issues/28)：已知 option 长度、重复次数、整数上界与未知 critical 处理。
- [#29](https://github.com/IoTSharp/CoAP.NET/issues/29)：无 Block 选项的错误终态、超时/取消和状态/计时器释放。
- [#30](https://github.com/IoTSharp/CoAP.NET/issues/30)：当前 OSCORE/EDHOC 安全上下文能力。
- [#31](https://github.com/IoTSharp/CoAP.NET/issues/31)：TCP/TLS/WebSocket transport。

每项代码先有原始 FAIL，再有修复后的定向 PASS，主监督复核组合行为后才提交/合并。单元测试、完整 CI、跨语言 peer、NativeAOT 实物、NuGet 公开包与设备现场分别记录。未跑的 gate 保持 NOT_RUN；本轮不发布 tag 或 NuGet，不部署、不修改生产配置。
