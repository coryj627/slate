# W1 post-merge adversarial audit — 2026-07-22

## Scope and method

This audit covers the 71 files merged for W1 in squash commit
`a8c7b96078f7988d64b1f07882493a03311dacee`. It reviews completeness,
correctness, maintainability, documentation, reliability, performance,
security, and accessibility. Evidence included the merged diff, Graphify's W1
architecture map, the W1 specification and execution report, the existing W1
red-team suites, hostile-input and concurrency source probes, the WPF control
and automation contracts, and a clean Release build/test baseline.

The baseline is:

- `dotnet build apps/slate-windows/SlateWindows.slnx --no-restore --configuration Release`:
  pass, zero warnings.
- `dotnet test apps/slate-windows/SlateWindows.slnx --no-build --no-restore --configuration Release`:
  102/102 unit and integration tests plus 1/1 non-interactive accessibility
  startup test pass.
- A standalone `dotnet test ... --no-restore` run fails because the
  host-logging census assumes `HostLogProbe.dll` was built separately. This is
  tracked below instead of being misreported as a product failure.

No P0/critical defect was confirmed. The prior audit did many things well:
native WPF controls and landmarks are used consistently; the workspace restore
shape is strongly bounded; core path validation prevents persisted tab paths
from escaping the vault; tree and filter results have explicit materialization
limits; cancellation generations prevent stale UI results; Contrast tokens
resolve through dynamic system colors; and the intended Windows baseline is
green.

## Ranked gap register

| ID | Severity | Area | Evidence and impact | Measurable closure |
|---|---|---|---|---|
| W1-RT-01 | High / P1 | Security, privacy | `HostLog` redirects managed stderr into `%LOCALAPPDATA%\Slate\logs\slate-windows.log`, while W1 writes vault-relative file-change paths and raw exception messages to `Console.Error`. Note names and filesystem paths can therefore persist in a default durable log, bypassing core's release privacy floor. | Default durable diagnostics contain stable event names and exception types only. Sentinel vault paths/messages never appear in unit or startup-census logs. |
| W1-RT-02 | High / P1 | Reliability, security | Import, workspace, sidebar settings, and file-recents readers check `FileInfo.Length` and then use an unbounded second read/parse. A replacement between check and open can bypass the cap; import can allocate beyond 256 MiB before its after-read validation runs. | Every affected read enforces its limit on the opened stream, reads at most limit + 1, and has boundary/replacement regression coverage. |
| W1-RT-03 | High / P1 | Correctness, concurrency | Vault close barriers cover import and bulk expansion, but not the sidebar filter task. `CloseSession` can dispose the shared `VaultSession` while a background filter is inside FFI. | Teardown first cancels all session-backed work and cannot dispose the session until those tasks have reached a terminal state; a deterministic race test proves ordering. |
| W1-RT-04 | Medium / P2 | Reliability | Single-instance activation applies a timeout to pipe connection and server reads, but client writes/flush are synchronous and have no deadline. A connected primary that stops reading can stall secondary startup. | Frame write and flush share the caller's remaining deadline; a stalled-server test completes within a bounded tolerance. |
| W1-RT-05 | Medium / P2 | Performance | Quick Open displays 50 rows, but every keystroke ranks, sorts, allocates, and crosses FFI with every matching row. Work is O(N log N) and output allocation is O(N), even when only top 50 plus the total count are consumed. | Core returns exact total plus the deterministic top K; results match full-ranking goldens and a large-corpus benchmark demonstrates bounded output and O(N log K) selection. |
| W1-RT-06 | Medium / P2 | Performance, accessibility | Initial/root directory refresh synchronously creates and sorts up to 5,000 view models on the UI thread, and the large file collections do not explicitly enable recycling virtualization. The execution report already records the latency residual. | Provider work runs off the UI thread, stale generations cannot publish, recycling virtualization is asserted, and a 5,000-item responsiveness census meets the agreed UI-thread budget. |
| W1-RT-07 | Medium / P2 | Security, reliability | Per-vault workspace/sidebar stores reject visible reparse points but still resolve absolute paths for each operation. A hostile concurrently mutated vault can race those checks. The W1 report acknowledges that these stores lack descriptor-relative traversal. | Reads and atomic replacements are anchored to an opened vault/directory identity, or fail closed when identity changes; adversarial reparse-swap tests cannot read or replace an external sentinel. |
| W1-RT-08 | Medium / release blocker | Accessibility, completeness | Interactive FlaUI/axe is CI-only, and Narrator, NVDA, JAWS, four built-in Contrast themes, and a customized Contrast theme remain unrecorded. Code contracts are strong, but the milestone cannot claim human AT acceptance without this evidence. | CI artifact is attached and every required row in `w_c_matrix.md` has dated pass/fail evidence. Human-only rows remain explicitly blocked until performed by a human operator. |
| W1-RT-09 | Low / P3 | Reliability | Several temporary-file cleanup blocks catch `IOException` but not `UnauthorizedAccessException`, allowing cleanup failure to mask an earlier primary exception. | Shared cleanup is best-effort for both exception classes and preserves the original failure; unit coverage pins this behavior. |
| W1-RT-10 | Low / P3 | Maintainability, CI | The test project executes `HostLogProbe.dll` without a build dependency. `dotnet test` from a clean tree fails unless the full solution build happened first. | The project graph builds the probe before the census; clean standalone test invocation passes. |
| W1-RT-11 | Low / P3 | Maintainability | `FilesSidebarViewModel.cs` is 2,286 lines and `WorkspaceViewModel.cs` is 1,612 lines, with persistence, asynchronous work, command policy, and presentation projection colocated. This raises review and race-analysis cost, although no defect follows from size alone. | Extract responsibility-focused collaborators/partial units with unchanged public behavior, explicit ownership of cancellation, and all existing tests green; each refactor PR stays within the authored-file cap. |

## Remediation PR groups

The observed Codoki maximum is 11 files. Per project policy, each PR may
directly create or modify no more than 22 authored files; dependencies and
transitive inputs do not count. These groups intentionally target fewer files
than that ceiling.

1. **Private durable diagnostics** — W1-RT-01. Add a closed diagnostic event
   vocabulary, route W1 exception/error logging through it, remove per-file
   change logging, and add sentinel privacy tests. Expected: 13–16 authored
   files.
2. **Bounded file ingestion and cleanup** — W1-RT-02 and W1-RT-09. Introduce
   one opened-stream bounded reader, migrate import and the affected JSON
   stores, and test exact limit, limit + 1, replacement, and primary-exception
   preservation. Expected: 6–9 authored files.
3. **Session-work and IPC deadlines** — W1-RT-03 and W1-RT-04. Give sidebar
   session work a single cancellation/completion owner, enforce the vault-close
   barrier, and make activation frame writes deadline-aware. Expected: 5–8
   authored files.
4. **Top-K Quick Open contract** — W1-RT-05. Add a core top-K result carrying
   exact total, expose it through UniFFI, migrate Windows and mac consumers, and
   preserve canonical ordering goldens. Expected: 9–15 authored files.
5. **Large-sidebar responsiveness** — W1-RT-06. Background initial/root
   projection with generation checks, enable recycling virtualization, and add
   responsiveness/accessibility contracts. Expected: 5–8 authored files.
6. **Descriptor-anchored per-vault stores** — W1-RT-07. Use an opened
   vault/directory identity for bounded reads and atomic replacement, preserving
   schema-v1 forward compatibility and locking. Expected: 8–14 authored files.
7. **Release evidence and clean test graph** — W1-RT-08 and W1-RT-10. Wire the
   missing build dependency, retain CI accessibility artifacts, and update the
   evidence matrix without converting unperformed human checks into passes.
   Expected: 4–8 authored files plus human execution for the remaining AT rows.
8. **View-model responsibility extraction** — W1-RT-11. Perform behavior-neutral
   extractions in small PRs, starting with asynchronous operation ownership;
   never combine a refactor with a semantic fix. Expected: fewer than 12
   authored files per extraction PR.

Every group follows the same gate: focused tests, full applicable local gates,
an adversarial diff review across all eight audit dimensions, correction of any
new finding, draft PR publication, 120-second CI/Codoki polling, all CI green,
Codoki explicitly safe to merge, then squash merge before the next group.
