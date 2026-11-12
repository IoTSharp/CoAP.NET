# CoAP.NET engineering handoff

## 2026-11-13 (Asia/Shanghai): standards and UDP interoperability

- Baseline: master@1abe69a0edaf55147ad6bccaa4e012aea87a8252 (v3.0.1). The original SonnetDB submodule remains unchanged, including its existing parent gitlink difference.
- User authorization covers research, GitHub issues, parallel implementation, commits, push and merge. It does not cover tags, package publication, deployment or production configuration.
- User decision: prioritize UDP/DTLS device integration and interoperability, following current formal and widely used CoAP standards.
- GitHub issues #20–#31 were created once and independently read back. #20/#21/#22 are the first implementation slices; the remaining option policy, lifecycle, transport and security contracts stay open.
- Three implementation clones: C:/Users/mysti/.codex/coap-repos/parser, matcher, blockwise. Integration clone: C:/Users/mysti/.codex/coap-repos/integration. Each clone starts from the same canonical commit and owns disjoint source/test paths.
- In four managed worktrees, git rev-parse returned the shared submodule metadata directory as the worktree root and status showed all tracked files deleted. The canonical submodule stayed clean; the cause was not fully diagnosed. Archive returned an ownership error, so the four directories were preserved and are not used for source changes or builds. Do not delete them without a verified recovery path.
- Audit entry: docs/coap-standards-roadmap.md. It separates current code evidence, README claims, optional RFCs, and unrun interoperability/NativeAOT/package/field gates. The registry option19 compatibility choice was sent to the user and remains pending; no default behavior change is inferred from elapsed time.
- First integrated candidate: 207/207 Release tests PASS, owned-source format PASS, ResourceMvc Release build 0 warnings/0 errors, diff check PASS. See docs/audits/coap-20261113/validation-first-wave.json. Original baseline failures and the Block1 review correction remain recorded. CI, peer interoperability, NativeAOT execution, package publication and field tests have their own gates.
- No persistent service was started. Task-owned process groups were bounded and cleaned. Local clones, tests and necessary evidence are retained for review; shared processes and the original SonnetDB worktree were preserved.
