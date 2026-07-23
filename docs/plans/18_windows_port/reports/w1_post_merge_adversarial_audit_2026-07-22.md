# W1 post-merge adversarial audit — 2026-07-22 (updated 2026-07-23)

## Scope and method

This audit covers the 71 files merged for W1 in squash commit
`a8c7b96078f7988d64b1f07882493a03311dacee`. It reviews completeness,
correctness, maintainability, documentation, reliability, performance,
security, and accessibility. Evidence included the merged diff, Graphify's W1
architecture map, the W1 specification and execution report, the existing W1
red-team suites, hostile-input and concurrency source probes, the WPF control
and automation contracts, and a clean Release build/test baseline.

The pre-remediation baseline was:

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
| W1-RT-03 | High / P1 | Correctness, concurrency | The initial close barrier omitted filtering. A final re-audit also found that superseded filter work, direct disposal, import/source-picker and bulk-expansion work, synchronous mutations/providers, task publication, and cancellation-source disposal did not share one complete lifetime boundary. | One sidebar-owned admission/drain barrier closes before producer cancellation, rejects every later native-session entry, joins every admitted native operation before disposal, makes cancellation failure non-fatal, and has deterministic publication, supersession, retained-sidebar, import, expansion, and cancellation-race coverage. |
| W1-RT-04 | Medium / P2 | Reliability | Single-instance activation applies a timeout to pipe connection and server reads, but client writes/flush are synchronous and have no deadline. A connected primary that stops reading can stall secondary startup. | Frame write and flush share the caller's remaining deadline; a stalled-server test completes within a bounded tolerance. |
| W1-RT-05 | Medium / P2 | Performance | Quick Open displays 50 rows, but every keystroke ranks, sorts, allocates, and crosses FFI with every matching row. Work is O(N log N) and output allocation is O(N), even when only top 50 plus the total count are consumed. | Core returns exact total plus the deterministic top K; results match full-ranking goldens and a large-corpus benchmark demonstrates bounded output and O(N log K) selection. |
| W1-RT-06 | Medium / P2 | Performance, accessibility | Initial/root directory refresh synchronously creates and sorts up to 5,000 view models on the UI thread, and the large file collections do not explicitly enable recycling virtualization. The execution report already records the latency residual. | Provider work runs off the UI thread, stale generations cannot publish, recycling virtualization is asserted, and a 5,000-item responsiveness census meets the agreed UI-thread budget. |
| W1-RT-07 | Medium / P2 | Security, reliability | Per-vault workspace/sidebar stores reject visible reparse points but still resolve absolute paths for each operation. A hostile concurrently mutated vault can race those checks. The W1 report acknowledges that these stores lack descriptor-relative traversal. | Reads and atomic replacements are anchored to an opened vault/directory identity, or fail closed when identity changes; adversarial reparse-swap tests cannot read or replace an external sentinel. |
| W1-RT-08 | Medium / release blocker | Accessibility, completeness | Interactive FlaUI/axe is CI-only, and Narrator, NVDA, JAWS, four built-in Contrast themes, and a customized Contrast theme remain unrecorded. Code contracts are strong, but the milestone cannot claim human AT acceptance without this evidence. | Automated evidence retention is implemented and verified by Actions run 29926688975: its retained TRX and three dated, revision-bound JSON summaries passed with zero axe errors. Main-window discovery retries transient UIA COM timeouts only within its existing bounded window, with regression coverage. Human-only AT and Contrast-theme rows remain explicitly pending until performed by a named human operator; this release blocker cannot be closed by code alone. |
| W1-RT-09 | Low / P3 | Reliability | Several temporary-file cleanup blocks catch `IOException` but not `UnauthorizedAccessException`, allowing cleanup failure to mask an earlier primary exception. | Shared cleanup is best-effort for both exception classes and preserves the original failure; unit coverage pins this behavior. |
| W1-RT-10 | Low / P3 | Maintainability, CI | The test project executes `HostLogProbe.dll` without a build dependency. `dotnet test` from a clean tree fails unless the full solution build happened first. | Remediated in the release-evidence PR: the test project declares and contract-tests a build-only `HostLogProbe` project reference. A clean standalone Release invocation rebuilt the probe and passed 127/127 tests on 2026-07-22. |
| W1-RT-11 | Low / P3 | Maintainability | After security/performance remediation, `FilesSidebarViewModel.cs` had grown to 2,653 lines while `WorkspaceViewModel.cs` remained 1,612 lines, with persistence, asynchronous work, command policy, and presentation projection colocated. This raises review and race-analysis cost, although no defect follows from size alone. | Complete. Sidebar filter/tree/child-expansion/import/session-work ownership is isolated in 432/653/470/472/135-line partials, leaving the primary sidebar file at 1,809 lines after later synchronous admission wiring. Workspace persistence and layout policy occupy 212/773-line owners, leaving the primary workspace file at 659 lines. Structure censuses guard representative declarations against accidental boundary collapse. Each extraction/remediation remained within the authored-file cap. |
| W1-RT-12 | Medium / P2 | Performance, reliability | After the top-K core contract landed, macOS `QuickSwitcherModel` still called ranking synchronously from `@MainActor`; candidate construction also made one display-name FFI call per file. Opening or typing in a large vault could therefore block keyboard and assistive-technology interaction, and superseded work had no publication owner. | Candidate capture is value-only; a 60 ms debounced, process-scoped serial background actor constructs FFI inputs and ranks with at most one native call active across sheet lifetimes; query mutation synchronously advances a monotonic publication generation; a surviving selection is retained, revealed in the rebuilt lazy list, and kept inert while stale rows are absent; synthesized stationary-pointer hover cannot steal it; and every explicit dismissal synchronously cancels publication before removing the sheet. The sheet exposes an accessible loading state. Deterministic blocked-worker, default-worker-identity, cross-model queue-admission/serialization, queued-supersession, selection-retention, viewport-target and hover-admission decision, dismissal-cancellation, announcement-cancellation, and max-concurrency tests pin their respective contracts; mac CI remains the view-integration compile gate. |
| W1-RT-13 | Medium / P2 | Performance, reliability, accessibility | Ordinary Windows file-tree folder expansion called the native child provider, restored descendant state, sorted and projected as many as 5,000 rows, and appended them to the live `ObservableCollection` synchronously on the UI thread. A large folder could stall keyboard and UI Automation interaction even though root refresh and bulk expansion were already asynchronous. | Native provider work, projection, sorting, and restored-descendant construction run off the UI thread through the shared serial tree-provider lane. Collapse, refresh, bulk expansion, close, and shutdown cancel or supersede work into an honest accessible retry state. Attachment identity rejects detached nodes before provider admission; operation identity, tree generation, and root identity reject stale publication; and the UI receives one prebuilt child collection swap. Ordinary and bulk requests begun during refresh await its UI publication before snapshot/provider work, while canceling that refresh cascades to both deferred dependents. Deterministic 5,001-item blocked-provider return, supersession, refresh precedence/cancellation cascade, post-refresh detached admission, sibling serialization/canceled queue, failure/retry, canceled-close retry, restoration, close, and direct-disposal tests pin the contract; interactive UIA covers ExpandCollapse and native Right/Left behavior with focus retention. |
| W1-RT-14 | Medium / P2 | Performance, reliability | Core `DirListing.dirs` is still returned as one complete directory array, and directory summaries can recompute descendant state before any host-level page limit is applied. The Windows caller can bound file lookahead to 5,001 in one request, but it cannot place a true bound on a directory-heavy level or guarantee a stable multi-page snapshot. | Core exposes a bounded, deterministic, snapshot-consistent directory-page contract with explicit continuation/truncation state and no repeated descendant-summary traversal. Rust/UniFFI/host goldens cover directory-only overflow, mixed ordering, cancellation and mutation between pages; a large-directory benchmark demonstrates bounded output and work per page. |
| W1-RT-15 | High / P1 | Correctness, reliability, security, CI portability | Windows CI ran only 7 atomic-rename and 5 path-adapter core tests. Separating the intentionally long census tier exposed five Windows-only fixture/assertion failures. Independent review then found a Windows LiveSync reparse/parent-swap escape and a real save/compaction/rebuild event-index race that could duplicate, orphan, or lose repair obligations. | Windows CI runs the complete bounded non-census `slate-core` package plus doctests. Fixtures and platform assertions are honest; Windows LiveSync walks pinned no-follow handles and reopens only the validated object; save and compaction hold their per-log guard from before durable marker publication through event-index commit; atomic rebuilds use one deadlock-safe try-locked log at a time. Escape, contention rollback/retry, and 100-run race stress regressions pass. |
| W1-RT-16 | Medium / P2 | Reliability, correctness | A global event-index rebuild deleted all rows and markers, then treated any fatal `read_oplog` header/I/O error as a successful empty contribution. Missing logs and torn entry tails already degrade to `Ok(empty/prefix)`; an actual `Err` could therefore commit a partial index and erase its retry obligation. | Fatal reads propagate out of the rebuild transaction. A deterministic two-log regression corrupts the later log header after another log was processed, proves every prior row and marker rolls back exactly, repairs the log, and proves retry convergence. |
| W1-RT-17 | Medium / P2 | Accessibility, correctness | The UI progress listener used one overwrite slot, so a fast scan could coalesce `Started`, `FileIndexed`, and `Finished` into only `Finished`; the terminal handler did not restore the report total, allowing a two-file scan to expose a false 1/1 `RangeValue`. The live gate also lacked scan `RangeValue` and sidebar organization/action pattern assertions. | Preserve semantic start/latest-progress/terminal boundaries in one bounded ordered drain; terminal state derives its total independently. A deterministic queued-drain test pins 2/2 and announcements. Live UIA asserts the settled progress range plus exact Selection, Toggle, ExpandCollapse, and Invoke coverage for sidebar controls/actions. |
| W1-RT-18 | Medium / P2 | Performance, reliability | Windows Quick Open cancellation prevents stale publication but cannot stop a native rank already executing; each debounced query starts an independent `Task.Run`, so ranks can overlap across queries and view-model lifetimes despite the process-wide serialization contract. | One process-scoped serial worker admits at most one native rank across all view models, discards canceled queued work before native entry, and retains generation guards. Blocked-ranker tests prove caller return, newest-only publication, cross-lifetime serialization, and maximum native concurrency one. |
| W1-RT-19 | Medium / P2 | Performance, accessibility, reliability | macOS file-tree root, first child page, projection, and restored-descendant recursion execute synchronously on `@MainActor` for up to 5,000 rows; only continuation pages are detached. A blocked provider can prevent loading state, keyboard, and VoiceOver updates. | Page-one provider/projection/restoration work runs off-main with session, generation, attachment, expanded-state, collapse, and rebind guards. Deterministic blocked-provider, stale/collapse/rebind/failure/restoration tests pin prompt actor return and newest-state-only publication. |

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
9. **macOS Quick Open responsiveness** — W1-RT-12. Move candidate conversion
   and top-K ranking off `MainActor`, debounce and generation-guard query work,
   serialize native calls, make the transient loading state accessible, and
   pin blocked-worker plus queued-supersession behavior. Expected: 4–7
   authored files.
10. **Windows ordinary child expansion** — W1-RT-13. Move native child
    loading, projection, and restored-descendant construction off the UI
    thread; share the serialized provider lane; cancel and generation-guard
    publication; publish one prebuilt collection; and exercise native UIA
    expansion/collapse. Expected: 9–13 authored files.
11. **Bounded directory-listing contract** — W1-RT-14. Add a bounded,
   snapshot-consistent core/UniFFI directory page, migrate both hosts without
   ordering drift, and pin directory-heavy performance and mutation behavior.
   Expected: 8–14 authored files.
12. **Windows core-suite portability and synchronization** — W1-RT-15. Separate the intentionally
   long census tier from the ordinary Windows run, repair platform-invalid
   fixtures and assertions without relaxing production validation, harden
   LiveSync handle containment, close the event-index mutation/rebuild races,
   and gate the full non-census core package in `windows.yml`. Actual: 16
   authored files; the independent security and synchronization findings
   expanded the original 7–10 estimate while remaining below the 22-file cap.
13. **Fatal event-index rebuild rollback** — W1-RT-16. Propagate fatal log
   reads out of the global rebuild transaction and pin exact two-log
   row/marker rollback plus repaired retry. Actual: 5 authored files, including
   the three authoritative W1 status records reopened by the finding.
14. **Progress semantics and live UIA census** — W1-RT-17. Replace the
   overwrite-only progress mailbox with bounded ordered semantic slots, derive
   the terminal total from the report, and exercise live scan/sidebar patterns.
   Actual: 8 authored files, including the normative spec, execution report,
   this audit, and the wave-close matrix.
15. **Windows Quick Open process worker** — W1-RT-18. Serialize native ranks
   process-wide, reject canceled queued work before FFI, and pin cross-lifetime
   maximum concurrency one. Actual: 7 authored files, including the four
   authoritative W1 status/evidence records.
16. **macOS file-tree page-one responsiveness** — W1-RT-19. Move root/child
   page-one provider and projection work off `MainActor`, preserve restoration
   and invalidation semantics, and add blocked-provider regressions. Expected:
   3–6 authored files.

Every group follows the same gate: focused tests, full applicable local gates,
an adversarial diff review across all eight audit dimensions, correction of any
new finding, PR publication, 120-second CI/review polling, all CI green, then
squash merge before the next group. Codoki is quota-unavailable through
2026-08-01. PRs handled during that outage use a documented exception requiring
three independent read-only adversarial reviews and fully green CI; they do not
claim a Codoki score or “safe to merge” verdict that was not produced.

## Remediation progress

- W1-RT-01 closed by PR #1011, with safe numeric size context added in PR
  #1012: default durable diagnostics now accept only closed event identifiers,
  exception type names, and explicitly safe numeric size fields. All production
  stderr writes route through that boundary; per-file vault change logging was
  removed. Both PRs had green CI and Codoki reported 5/5 confidence and safe to
  merge.
- W1-RT-02 and W1-RT-09 closed by PR #1012: affected readers enforce their
  limits against the opened stream, import checks cancellation between bounded
  chunks, stable reads allocate one final buffer, and temporary cleanup cannot
  mask the primary failure. Exact-limit, limit + 1, under-reported growth,
  cancellation, cleanup, and production-store diagnostics are covered.
- W1-RT-04 closed and W1-RT-03 was initially addressed by PR #1013. The final
  post-merge audit reopened W1-RT-03 after finding superseded filter, direct
  import/expansion, synchronous provider/mutation, task-publication, and
  cancellation-source races. The follow-up remediation gives every sidebar
  native-session entry one admission/drain owner, closes admission before
  canceling all producers, joins already-admitted work before session disposal,
  scopes UI publication outside leases, and gives unexpected asynchronous
  failures privacy-safe terminal handling. Production disposal is serialized
  through the owning dispatcher and joins any active session-loading task.
  Deterministic regressions cover supersession, publication, retained
  post-disposal calls, cancellation/disposal races, picker admission, and
  cancellation callbacks that throw without preventing later cancellation.
- W1-RT-05 closed by PR #1014: core retains only the deterministic top K while
  returning an exact total, and Windows/macOS request their 50-row display
  page through the same UniFFI result. The compatibility corpus covers zero,
  partial, exact, and oversized limits; empty and fuzzy queries; missing and
  duplicate recents; and canonical Unicode path ties. On this Windows host,
  the committed 50,000-file Criterion pair measured full ranking at
  362.87–365.60 ms versus top-50 at 335.30–338.32 ms, before accounting for
  the much larger avoided FFI marshalling and host allocation. The final
  Codoki review reported 5/5 confidence and safe to merge; every CI lane was
  green.
- W1-RT-06 closed by PR #1015: WPF-dispatched root refreshes build, page, sort,
  restore expanded descendants, and project tags on a serialized background
  worker. A generation guard publishes one prebuilt collection, close/disposal
  joins the session-backed provider, and the files tree, filtered list, and
  dual-pane list explicitly use recycling virtualization. Deterministic tests
  pin a <100 ms UI-dispatch budget with a blocked 5,001-item provider, reject
  queued stale publication, preserve later restored branches after a truncated
  branch, and cover both close barriers. Unexpected worker faults are
  privacy-safe, user-visible, and terminal without faulting teardown joins.
  CI was green and Codoki reported 5/5 confidence and safe to merge.
- W1-RT-07 was initially remediated by PR #1016: workspace, sidebar settings,
  and legacy per-vault recents operations hold and revalidate opened vault and
  `.slate` identities, reject final reparse handles, verify child final paths,
  and use temporary-file handles for replacement. The final post-merge audit
  reopened the destination rename itself because it still named the target
  through an absolute path. The follow-up remediation closes that window with
  `NtSetInformationFile(FileRenameInformation)`: a null root plus a simple
  destination leaf tells Windows to rename within the already-open temporary
  source handle's directory, so no ancestor path is reopened and the request
  remains valid for SMB. Deterministic x86/x64 buffer tests pin the simple-name
  and null-root protocol, while an adversarial test attempts an ancestor
  redirect immediately before rename and proves the external sentinel is
  unchanged whether Windows blocks the namespace swap or permits it. A
  separate post-commit-failure regression proves cleanup cannot delete an
  already-replaced store file. PR #1024 passed 156 Windows and two
  non-interactive accessibility tests, three independent final adversarial
  reviews, and every CI lane under the documented Codoki-outage exception;
  RT-07 is closed.
- W1-RT-12 closed by PR #1025: macOS candidate capture no
  longer calls FFI on `MainActor`; top-K ranking runs on a debounced,
  process-scoped serial background actor with at most one native rank active
  across sheet lifetimes; query assignment and generation invalidation share
  one main-actor turn; a surviving selection remains stable, is revealed when
  the lazy results list returns, stays inert while stale rows are absent, and
  cannot be replaced by synthesized hover under a stationary pointer;
  explicit dismissal cancels before sheet removal; cancellation
  makes stale completion inert and clears pending announcements; and an
  accessible loading state prevents stale rows from being opened under newer
  query text. Focused tests block the worker to prove the main actor returns,
  queue a replacement
  to prove newest-query-only publication and max concurrency one, and retain
  the existing ordering, selection, cap, and announcement contracts. Three
  independent final adversarial reviews found the diff code-ready, and every
  CI lane, including mac XCTest and SwiftUI accessibility, passed in Actions
  run 29950708145 under the documented Codoki-outage exception. The PR was
  squash-merged as `866d3c92c43a630edda7aa1943dbbf1519fb49e7`.
- W1-RT-13 closed by PR #1026: an ordinary expansion owns a
  cancellable per-node operation, shares the sidebar's serial tree-provider
  lane and session-work admission barrier, builds the complete visible child
  collection away from the UI thread, and publishes it with operation,
  generation, root-identity, attachment, and expanded-state checks. Collapse,
  refresh, bulk work, close, and shutdown cancel or supersede publication into
  a truthful accessible retry state. The caller
  uses one native request with bounded file lookahead rather than repeated
  file-page calls, and deterministic tests cover 5,001 children,
  supersession, detached refresh nodes before and after replacement, shared
  provider serialization with canceled queued work, generic private failures
  and retry, canceled-close retry, restored descendants, interactive close,
  direct disposal, and native UIA expansion/collapse. Three independent final
  read-only adversarial reviews report no remaining actionable finding and
  `CODE-READY: YES`. Every CI lane passed for revision
  `7a860ca962b33bfe50d348ae1efb46c12679e3ce`; Windows Actions run 29956533198
  includes the live ExpandCollapse/Right/Left accessibility gate and evidence
  upload. The PR was squash-merged as
  `81a108220a7202dc414d53286b644bd4ca290323` under the documented
  Codoki-outage exception.
- W1-RT-14 closed by PR #1027, a 22-authored-file change. Its first three
  independent reviews blocked publication: macOS dropped continuation
  directories; page reads had an external-writer TOCTOU; nullable cursor
  predicates still prefix-scanned; raw-connection migration lacked the new
  UDF; files-only Windows work drained directory prefixes synchronously; and
  mac list/duplicate/folder-discovery caps and error paths were incomplete.
  The corrected core/UniFFI contract now provides hard-capped combined and
  direct files-only range pages. Opaque cursors are size-limited and bind the
  session, scope, normalized parent, and SQLite discriminator; a post-query
  recheck rejects any in-flight external commit. SQLite progress cancellation
  interrupts count/enrichment statements, and only returned rows are
  enriched. Migration registers deterministic functions for raw connections,
  with a populated Unicode v32→v33 upgrade/reopen regression and a documented
  REINDEX obligation if sort-key semantics ever change. Query-plan tests
  require range constraints on both expression indexes.
  Windows files-only loading is one bounded request. macOS continuation drains
  retain directories and files, cap their combined count, cancel revoked
  native work, and adopt restored page-two expansions; the list pane caps an
  unaligned page exactly. Duplicate preparation performs no sibling
  enumeration and uses at most 200 authoritative exclusive-create collision
  attempts. Folder discovery is linear, stops paging at 50,000 folders, and
  surfaces reaching that limit or a failure through the existing alert and assistive
  announcement channels. Safety-capped tree levels retain a visible,
  VoiceOver-labeled incomplete row.
  The focused evidence is 12/12 core page tests, 128/128 database/migration
  tests, 72/72 UniFFI tests, and 171/171 Windows tests. On a 10,000-directory
  fixture, exact 200-row first/middle/98%-late medians are
  153.04/158.20/167.96 µs; the late page is only 9.8% above the first.
  Three fresh independent closure reviews (core, hosts/accessibility, and
  cross-cutting quality) each returned CODE-READY: YES. Local focused gates
  were green, including 20 consecutive parallel runs of the formerly racy
  core page module. CI first exposed a migration-replay collision in an older
  fixture; migration 033 now transactionally rebuilds both owned indexes and
  a regression seeds stale same-name definitions before replay. All ten CI
  checks passed, and the PR was squash-merged as
  `985061e9198d00db319701aed8f1d2b63ac86f0d` under the documented
  Codoki-outage exception.
- W1-RT-15 closed by PR #1028, a 16-authored-file change. The full non-census Windows
  run no longer asks NTFS for POSIX-only `*`, `?`, or `|` names; those cases
  remain covered on supporting hosts while cross-platform SQL wildcard and
  punctuated-parent cases still run on Windows. Local-time DQL regressions use
  the configured Windows zone instead of assuming the Unix-only `TZ` override
  works there, while Unix retains the pinned America/New_York child coverage.
  The ctime backfill regression
  asserts the documented zero sentinel off Unix, Dropbox evidence uses the
  host separator, and Windows LiveSync opens and retains the root and every
  fixed component without delete/write sharing, rejects reparses and special
  targets before reading, and uses `ReOpenFile` for the validated object.
  Save and compaction now acquire the per-log mutation guard before publishing
  their durable marker and retain it through the matching event transaction.
  A rebuild holds an immediate SQLite writer, try-locks one log at a time, and
  rolls back with markers intact on contention; deterministic rollback/retry
  coverage and 100/100 race-stress repetitions pass. `windows.yml` replaces
  its two narrow filters with the complete non-census package command. Repeated
  exact local gates passed in 30.1–31.9 seconds: 1,615 unit, 364 integration,
  and 2 doctests passed; 2 intentionally ignored and 50 census/timing tests
  were excluded from the unit binary. CI first exposed a Unix-only local-time
  test-harness assumption: Windows does not honor `TZ=America/New_York` through
  Chrono. The corrected tests retain deterministic New York/DST child-process
  coverage on Unix and straddle the actual configured local-midnight boundary
  on Windows. Three independent final reviewers returned `CODE-READY: YES`,
  including targeted reviews of both CI repairs. On final revision
  `9fac9b2007d0cc7c12ef17fe12c36938796af826`, all seven PR checks passed:
  Windows Actions run 29980921181 included the complete core package, Windows
  tests, live FlaUI + axe, evidence upload, and format gate; Mac Actions run
  29980921180 passed XCTest; and Actions run 29980921192 passed workspace
  formatting/Clippy, nextest, and bench compilation. The PR was squash-merged
  as `726157a858b06fcde13b6ed0936e753848675433` under the documented
  Codoki-outage exception.
- W1-RT-16 was closed by PR #1029. Fatal op-log reads
  now abort the immediate rebuild transaction instead of silently contributing
  no events. A two-log corrupt-header regression proves the global delete and
  partial reinserts roll back with the repair marker, then restores the log and
  proves exact successful retry convergence. Both rollback tests pass together;
  the complete bounded package passes 1,616 unit, 364 integration, and 2
  doctests; and workspace all-target Clippy is clean. Three independent final
  reviews returned code-ready. All seven checks passed after rerunning one
  unchanged Windows timing-test failure, and the PR was squash-merged as
  `ce19b0ebe9def05c2d55febf807e5c75f2e77a83` under the documented
  Codoki-outage exception.
- W1-RT-17 was closed by PR #1030. The progress
  listener preserves `Started`, the latest `FileIndexed`, and the terminal
  event in one bounded ordered drain, rejects events after terminal admission,
  and restores the terminal maximum from the report's authoritative
  `FilesSeen` total. A deterministic cached two-file scan pins the 2/2 range
  while its canonical completion truthfully announces zero files re-indexed.
  Live UIA now inspects the progress `RangeValue` and exact
  sidebar Selection, Toggle, ExpandCollapse, and Invoke census. Three
  independent final reviewers returned code-ready. CI exposed and the same
  reviewers cleared an initial-focus ordering defect plus zero-height empty
  shortcuts list; final Windows Actions run 29988127038 passed live UIA/axe,
  full core, 174 Windows tests, retained evidence, and formatting. All three
  path-selected checks were green, and the PR was squash-merged as
  `268953adcaede360429dc41483604a175cff4333` under the documented outage
  exception.
- W1-RT-18 closed in PR #1031. One
  process-lifetime coordinator serializes native ranks across view-model/vault
  lifetimes, drops canceled queued work before FFI, and retains an admitted
  lane until the non-cancellable native call actually returns. Generation and
  token guards suppress stale success and failure publication. Five focused
  tests cover a 20-query burst/newest-only publication, default cross-lifetime
  maximum concurrency one and disposed-model nonpublication, canceled queued
  admission, exception release, and privacy-safe terminal failure publication.
  Local evidence is 179/179 Windows tests plus clean .NET formatting. Three
  independent final reviewers returned code-ready. Semgrep and license checks
  passed, and final-head Windows Actions run 29990822473 passed the complete
  core/CLI, Windows, live accessibility, evidence-upload, and format gates with
  retained artifact `slate-windows-accessibility-d2da2cdb023bf0a988b9ba52d2046f8287c61825`.
  The PR was squash-merged as
  `a443264f4be57e186d106001c3ab500e4622ec97` under the documented outage
  exception.
- W1-RT-19 is implemented as a 14-authored-file change on the current
  remediation branch. Production root and child page-one fetch, continuation
  draining, shared pure organization, projection, metadata-overlay
  re-preparation, and restored-chain materialization run on one dedicated
  serial non-main queue rather than Swift's cooperative executor. Cancellation
  is checked before native admission and throughout projection, key building,
  sorting, grouping, and publication preparation; bind/session/
  organization/level/attachment/expanded/root and overlay-revision guards
  reject stale publication. Worker-built file and directory indexes are
  assigned directly; level-owned header/pin lookups avoid a whole-tree
  MainActor merge; obsolete arrays, indexes, and presentation buffers cross a
  main-turn barrier before final destruction on a utility queue; and restored
  expansion uses the indexes instead of scanning a 50,000-row level. Live
  active-key and preference reorganization uses one deterministic, compacting
  pump over the same worker;
  inactive metadata changes remain one keyed lookup. Expand Loaded snapshots
  indexed directories in provider order, incrementally discloses them, and
  admits at most one child operation to the serial worker at a time. Restored
  and invalidated expansion chains share a structurally owned one-at-a-time
  pump. Collapse/invalidation cancels stale bulk intent, and completion copy is
  posted only after a full drain, so queued rows never claim an expanded
  accessibility state without children or a loading row. While an authoritative
  root replacement keeps predecessor rows visible, disclosure is read-only and
  rejected bulk commands replace their start announcement with bounded
  “File tree is updating” copy; Collapse All announces completion only after
  the live tree accepts it. Targeted async
  root rename/move/delete reconciliation follows the already-mutated path
  ledger; immediate state-specific disclosure/cache/fetch/materialization
  cleanup prevents a blocked delete or chained same-ID remap/delete from
  inheriting expanded or collapsed caches while stale-path-only removal
  preserves the authoritative remapped disclosure, and
  failures clear predecessor caches before a reused directory ID can land.
  Backend error payloads no longer enter visible or VoiceOver copy. Thirty-one
  async tests use deadline-bounded waits and cover prompt root return/newest
  rebind, read-only stale root-subtree disclosure and running-child retirement
  during same-ID replacement, chained remap/delete cancellation, child collapse/
  cancel/retry, active failure/retry, sibling serialization and queued
  cancellation, restored recursive expansion, async root rename/delete/same-
  path reuse, targeted failure/reused-ID retry, save-during-load overlay
  ordering/grouping, owner-only overlay routing, overflow-refetch, cancellable
  preparation, active live reorganization, normal and stale-result off-main
  level retirement, owned-directory reconciliation, ordered and cancellable
  Expand Loaded admission/completion, truthful one-at-a-time disclosure for
  1,000 restored siblings, dormant nested restoration across ancestor collapse/
  re-expand, queued-restoration collapse and Collapse All across owner
  replacement, same-ID owning-level supersession, empty-level publication
  without global expansion rescans, and production-path pagination/pin/folder-
  note parity. A separate pure regression proves linear component visits while
  ordering 50,000 unrelated removal roots plus 1,000 descendants and retains
  every same-path predecessor/replacement owner; an integration regression
  proves 1,000 owned levels publish one batched teardown. The first
  independent review round
  found the overlay ordering,
  mutation reconciliation, failure cleanup, cooperative-executor, large-level
  indexing, duplicated organization, raw error-copy, and unbounded-test gaps;
  all are remediated and three exact-tree re-reviews report code-ready. No Swift toolchain is
  available on the Windows development host; macOS compile/test CI remains the
  executable gate.

## Current closure status

The remediation sequence from PR #1011 through PR #1031 is merged on
`main` at `a443264f4be57e186d106001c3ab500e4622ec97`. The current mapping is:

| Gap | Closing PR(s) | Repository status |
|---|---|---|
| W1-RT-01 | #1011, #1012 | Closed |
| W1-RT-02, W1-RT-09 | #1012 | Closed |
| W1-RT-03 | #1013, #1023 | Closed |
| W1-RT-04 | #1013 | Closed |
| W1-RT-05 | #1014 | Closed |
| W1-RT-06 | #1015 | Closed |
| W1-RT-07 | #1016, #1024 | Closed |
| W1-RT-08 | #1017 | Code and automated evidence closed; named-human Narrator/NVDA/JAWS and Contrast-theme evidence remains a release blocker |
| W1-RT-10 | #1017 | Closed |
| W1-RT-11 | #1018–#1022 | Closed |
| W1-RT-12 | #1025 | Closed |
| W1-RT-13 | #1026 | Closed |
| W1-RT-14 | #1027 | Closed |
| W1-RT-15 | #1028 | Closed |
| W1-RT-16 | #1029 | Closed |
| W1-RT-17 | #1030 | Closed |
| W1-RT-18 | #1031 | Closed |
| W1-RT-19 | Current remediation branch (14 authored files) | Implementation/test code and three independent exact-tree re-reviews complete; macOS CI pending |

W1 repository remediation and automated acceptance are not yet complete:
W1-RT-19 must merge first. Independently, milestone release
acceptance remains blocked under WGA-9 until a named human records the Narrator
smoke, NVDA and JAWS passes, and all four built-in plus one customized Windows
Contrast-theme passes in `w_c_matrix.md`.
