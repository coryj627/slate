# W1 execution report — 2026-07-20

## Outcome

W1-0 through W1-4 are implemented in the repository. Three independent agents red-teamed the complete wave, found no P0 issue but several P1/P2 correctness, performance, reliability, documentation, and accessibility defects, and then performed repeated read-only audits after remediation. The Windows shell now opens and closes vaults, persists recents/window state, exposes a native files sidebar, restores recursive tab/split workspaces, hosts the full right-pane leaf registry, and provides a core-ranked Quick Open surface. Interactive CI and human assistive-technology evidence remain explicit release gates, not implied passes.

The post-merge remediation sequence through W1-RT-18 is merged through PR
#1031 and squash commit `a443264f4be57e186d106001c3ab500e4622ec97`.
Final-head Windows Actions run 29990822473 passed the complete core/CLI,
Windows, live accessibility, evidence-upload, and format gates.
W1-RT-19—macOS file-tree page-one responsiveness—is implemented as a
14-authored-file change on the current remediation branch; its first independent
review round is remediated and three exact-tree re-reviews are code-ready, while
macOS CI remains pending. These
follow-ups are tracked in the adversarial audit and must merge
before W1 code or automated acceptance is called complete. Named-human
Narrator/NVDA/JAWS and Contrast-theme evidence remains a separate release
blocker.

## Delivered scope

### W1-0 — atomic no-clobber rename

- Windows uses a native atomic move without replacement; destination conflicts preserve both source and destination.
- Linux glibc/musl and unsupported portable paths are atomic or fail closed; no check-then-replace fallback remains.
- File, directory, occupied-destination, and repeated destination-creation race tests are wired into Windows CI.

### W1-1 — shell and vault lifecycle

- WPF Fluent shell with welcome/workspace states, native menu/access keys, declarative chords, single-instance activation, folder picker, recent vault welcome list and Jump List, close-vault safety, scan progress, and vault-event/UI-thread marshalling.
- `%LOCALAPPDATA%\Slate` owns device-local recents and logs. Window placement degrades safely after monitor changes. Per-monitor-v2 is asserted against the production executable.
- Explicit Fluent Light/Dark/HC dictionaries are layered below Slate Light/Dark/Contrast tokens. Contrast changes are reactive; text-bearing surfaces are solid and Fluent’s Mica backdrop is disabled. Provisional dark/light pairs have the W1 APCA test. The experimental `ThemeMode` API is deliberately not used on the pinned .NET 10 SDK.
- The W0 `--census-log-probe` startup mode and host logging remain intact.

### W1-2 — files sidebar

- Native hierarchical file and tag trees; filter/date grouping; sort, pin, shortcut, recent and sidebar-history state; optional dual-pane container; folder notes; wikilink copy; type-ahead and keyboard routing.
- Create/rename/move/trash/tag mutations route through core session APIs. Creates use exclusive reservation. Batch destructive operations require confirmation and preserve core report semantics.
- File/folder import is cancellable and backgrounded. It rejects in-vault sources and reparse points, routes every destination create through core exclusive APIs, applies collision names, and bounds roots (256), total entries (10,000), and bytes per file (256 MiB). Omitted roots are counted in the result instead of silently dropped.
- Pins, shortcuts, organization and shared file recents persist with bounded, forward-compatible stores. Structural changes update stale paths.

### W1-3 — workspace

- Recursive horizontal/vertical split model with minimum sizes, geometry-based directional focus, terminal-boundary events, focusable pointer/arrow resize handles, tab duplicate/close/reopen/move/cycle, close-pane, open-current/new-tab/split targets, and three-region focus routing.
- Native WPF `TabControl` peers expose Selection patterns. The right pane registers all 16 shipped leaf kinds; later waves own their feature bodies.
- `.slate/workspace.json` is pinned to the mac schema-v1 shape. All six persisted `EditorItem` discriminators round-trip; an unknown kind drops only its tab. Cross-platform fixtures cover mac, Windows and unknown-kind inputs.

### W1-4 — Quick Open

- Ctrl+O opens a recents-first, 50-result overlay. Only core `SwitcherRank` determines ordering; C# performs no fuzzy scoring or display-name derivation.
- Initial indexing runs after scan; vault Created/Renamed/Deleted events update the cache incrementally, including folder descendants. Windows ranking is debounced off the UI thread through one process-lifetime native lane; canceled queued queries never enter FFI, an admitted rank retains the lane until it returns, and generation and cancellation-token guards reject stale publication across vault/view-model lifetimes.
- The mac consumer uses the same top-50 contract through a 60 ms debounced, process-scoped serial background actor. Candidate capture performs no per-file FFI on `MainActor`; at most one native rank is active across sheet lifetimes; query assignment synchronously invalidates older publication while retaining and re-revealing a still-valid selection in the rebuilt lazy list; synthesized hover under a stationary pointer cannot steal that selection; explicit dismissal cancels before sheet removal; and the sheet shows an accessible loading state until the current query publishes.
- Enter, Ctrl+Enter and Ctrl+Alt+Enter target current tab, new tab and split. Arrow selection wraps; Esc restores prior focus; tab traversal stays in the overlay.
- Result counts use the new typed core `A11yEvent::QuickSwitcherCount`; core goldens, corpus, UniFFI bindings and the mac consumer were updated together.

## Independent red team and remediation

Three read-only reviewers independently inspected all W1 code, tests, CI, fixtures, documentation and accessibility evidence. Their earlier closure pass found no remaining P0/P1 implementation defect; the 2026-07-23 final audit subsequently reopened the four P2 gaps W1-RT-16 through W1-RT-19. RT16–RT18 are closed; RT19 is locally implemented, every review finding is remediated, and all three exact-tree re-reviews report code-ready with macOS CI still pending. The prior remediation closed these confirmed findings:

- Current-tab replacement is dirty-gated and selects an exact existing same-group target; new-tab deduplicates; close-tab/pane uses Save/Discard/Cancel. Duplicate/split Markdown tabs share live dirty buffers. Path identity is exact and case-sensitive.
- Graph is a global singleton, restore prunes graph-created empty panes, and workspaces stop at six panes. Persistence rejects hostile structure, duplicate IDs, invalid group counts and its own size bound without escaping UI commands.
- Closing a same-axis pane preserves all remaining siblings and their weight ratios. Nested directional focus uses normalized geometry instead of tree adjacency.
- Rename/move/delete events retarget or invalidate open tabs, including folder descendants, while preserving dirty buffers.
- Model pane focus now moves real WPF focus for Markdown and placeholder kinds, and real keyboard focus updates the active model group. Recursive split handles expose size state, accept pointer/arrow resizing, persist on completion and announce the result.
- Quick Open is modal for keyboard, pointer and background command routing; cancel validates/restores prior focus with an active-pane fallback, and commit focuses the destination.
- Tree/dual-pane listings consume the bounded core/UniFFI directory-page contract. Combined pages are dirs-first, hard-capped, keyset-continued and snapshot-consistent; cursors are ephemeral and session/scope/parent/snapshot-bound. Windows and the mac list pane use a direct files-only range seek rather than draining directory prefixes, while visible collections remain capped and truthfully truncated. The mac tree retains continuation directories, applies its cap to combined rows, cancels revoked native work, and adopts restored expansions that land after page one. Expansion snapshots survive refresh; Expand Loaded is snapshot-only, cancellable, generation-guarded, uses immutable ordering state off-thread, and participates in the vault-close barrier.
- Files-tree F2 expands and focuses the rename editor, selects the filename stem, commits on Enter and cancels on Escape; placeholder/group rows cannot enter rename.
- Filtering is a cancellable, generation-checked 200 ms background operation. Import enumeration is streaming and bounded, checks ancestor reparse points, rechecks file size after reading, and reports omitted items accurately.
- Sidebar forward-compatibility bounds cannot write a file the next read rejects; grouped oldest-first sort persists correctly. Single-instance frames have a read deadline.
- Clean/dirty tabs expose truthful UIA names; terminal failures and mutation results route through the dispatcher. Runtime Windows light/dark and Contrast changes are observed.
- The parity generator validates an exact 60-command inventory and live implementation/test anchors across four primary surfaces. These anchors are intentionally described as surface-level evidence; command behavior remains covered by the Windows suites. Windows CI includes the five path-adapter tests in the full non-census `slate-core` package gate.

## Architecture and data contracts

The WPF host is MVVM over the generated UniFFI boundary. Product logic remains in Rust: ranking, file mutation/link rewrites, scans, tag editing, structural reports and canonical accessibility strings. C# owns native-control state, Windows lifecycle and device-local adapters.

The sidebar filter pipeline is isolated in
`FilesSidebarViewModel.Filter.cs`: that partial unit exclusively owns the
filter UI context, cancellation token, completion task, generation guard,
background query, UI publication and local date-window conversion. The
extraction is behavior-neutral and a repository-structure census guards
representative operation-ownership declarations against drifting back into the
primary view-model file.

Tree refresh and snapshot-based “Expand Loaded” are isolated in
`FilesSidebarViewModel.TreeOperations.cs`. They intentionally share the same
tree-generation boundary; the partial owns both cancellation sources,
completion tasks, the worker/UI contexts, stale-generation rejection and
bounded UI publication. A second structure census guards representative
tree-operation declarations against drifting back into the primary view model.

Bounded source import is isolated in `FilesSidebarViewModel.Import.cs`. The
partial owns source selection, cancellation and importing state, the 256-source
and 10,000-entry traversal caps, the 256 MiB per-file read bound, reparse/vault
rejection, collision handling and completion summary. Constructor wiring and
command policy remain in the primary partial; a structure census guards
representative declarations at the operation boundary.

Native-session lifetime admission is isolated in
`FilesSidebarViewModel.SessionWork.cs`. Every filter, tree, tag, dual-pane,
import, mutation, wikilink and batch entry either acquires its sidebar-owned
lease or becomes a no-op after shutdown closes admission. Teardown cancels all
producers only after closing admission and waits for the admitted-work drain
before releasing the shared `VaultSession`; UI collection changes,
announcements and other callbacks run outside synchronous leases. Behavioral
tests force superseded filter chains, tree-task publication, cancellation versus
completion, pending source pickers, import/expansion workers, throwing
cancellation callbacks, and retained-sidebar calls after direct disposal. A
structure census guards representative ownership declarations without treating
source strings as a substitute for those behavioral guarantees. Lifecycle
disposal is marshalled to the owning WPF dispatcher in production and joins an
active scan/file-list loading task before releasing the session, so synchronous
workspace/open events and initialization cannot race background disposal.

Workspace restore and persistence are isolated in
`WorkspaceViewModel.Persistence.cs`. The partial owns the store and expanded
path provider, restore suppression, mutation batching/pending-save state,
duplicate-graph pruning, empty-group normalization and snapshot serialization.
Workspace layout policy is isolated in `WorkspaceViewModel.Layout.cs`. That
partial owns the pane tree, closed-tab stack and reopening policy, tab placement
and movement, split/close policy, normalized directional focus geometry,
focus-boundary and resize announcements, weight normalization and
layout-command refresh. Constructor wiring, document/path identity (including
retargeting or invalidating path-backed closed-tab entries) and right-pane leaf
state remain in the 659-line primary unit; persistence and layout occupy 212-
and 773-line partials. Separate structure censuses guard representative
declarations at both ownership boundaries.

Two per-vault host stores are deliberate same-shape implementations because no canonical core store exists:

- `.slate/workspace.json`: mac schema version 1, bounded recursive decode, unknown-tab forward compatibility.
- `.slate/sidebar.json`: mac schema version 1, unknown sibling and reserved-shortcut preservation.

Both use the Windows anchored-store boundary: each operation holds and
revalidates vault and `.slate` directory identities, opens final children
without following reparse points, reads through the verified child handle, and
atomically renames the written temporary handle only after confirming the
directory identity is still fixed. The replacement request uses a null root
and a simple destination leaf, which renames within the already-open temporary
source handle's directory without reopening any ancestor path and remains a
valid request over SMB. The legacy per-vault file-recents migration uses the
same read/delete boundary.

## UI Automation release and migration note

W1 introduced recursive workspace split handles and their UI Automation identifiers. During review, the original shared `WorkspaceSplitHandle` identifier was made unique without removing the legacy lookup path:

| Handle | AutomationId | Compatibility guidance |
|---|---|---|
| Horizontal (left/right resize) | `WorkspaceSplitHandle` | Retained as the legacy identifier so existing automation continues to find the historically first handle. |
| Vertical (up/down resize) | `WorkspaceSplitHandleVertical` | Use this identifier for vertical handles. The former shared identifier could not distinguish this orientation reliably. |

Automation clients should select the orientation-specific entry above and may additionally verify `AutomationProperties.Name` (`Resize editor panes horizontally` or `Resize editor panes vertically`). The hosted FlaUI gate creates both split orientations, asserts that each identifier resolves exactly once, and checks the handles' accessible names and keyboard focusability.

Automation written against the transient W1 review build should migrate its selector as follows:

```csharp
// Before: transient review-only identifier.
conditionFactory.ByAutomationId("WorkspaceSplitHandleHorizontal");

// After: stable, backward-compatible horizontal identifier.
conditionFactory.ByAutomationId("WorkspaceSplitHandle");

// Vertical split handles are now addressable without ambiguity.
conditionFactory.ByAutomationId("WorkspaceSplitHandleVertical");
```

The repository contains no remaining automation consumer of `WorkspaceSplitHandleHorizontal`; the local resource-contract test and hosted FlaUI gate enforce the stable selectors above.

## Verification evidence

| Gate | Result |
|---|---|
| `dotnet format ... --verify-no-changes` | Pass |
| Windows unit/integration suite | 179 passed, 0 failed; the latest clean standalone invocation includes bounded/reentrant scan-progress semantics plus blocked-ranker return, 20-query newest-only supersession with stale-failure suppression, default cross-lifetime maximum-concurrency-one and disposed-model nonpublication, canceled-queue rejection, exception release, and generic high-priority rank-failure publication coverage, alongside the existing sidebar/session/workspace behavioral and structure contracts |
| Accessibility project, non-interactive local branch | 2 passed, 0 failed; production executable survived XAML load and initial scan, and transient UIA COM timeout retry behavior is pinned |
| Interactive FlaUI + axe-windows | Pass for final PR #1031 revision `d2da2cdb023bf0a988b9ba52d2046f8287c61825` in Windows Actions run 29990822473. Live RT17 scan RangeValue and exact sidebar pattern assertions remained green together with the RT18 tests, complete core/CLI tiers, and format gate. Retained artifact `slate-windows-accessibility-d2da2cdb023bf0a988b9ba52d2046f8287c61825`, TRX, and dated revision-bound workspace, Quick Open, and welcome axe evidence were green. |
| `cargo test -p slate-core --lib vault::fs::tests::rename --locked -q` | 7 passed, 0 failed |
| `cargo test -p slate-core vault::fs::tests::windows_ --locked -q` | 5 passed, 0 failed |
| `cargo test -p slate-core --locked -- --skip census_ --skip perf_guard_root_listing_under_100ms_on_10k_files` on Windows | Current bounded-package evidence: 1,616 unit, 364 integration, and 2 doctests; zero failures. The long randomized censuses and hardware-timing guard remain in their dedicated Rust lanes. |
| Focused core accessibility tests | 3 passed, 0 failed |
| Focused bounded-directory core tests | 12 passed, 0 failed; includes range plans, in-flight mutation, post-admission cancellation, cursor replay/scope/size, and Unicode ties |
| Focused database/migration tests | 128 passed, 0 failed; includes raw connections and populated Unicode v32→v33 upgrade/reopen |
| Focused core switcher tests | 20 passed, 0 failed |
| `cargo test -p slate-uniffi --lib --locked -q` | 72 passed, 0 failed |
| `cargo fmt --check` | Pass |
| `cargo clippy --workspace --all-targets --locked -- -D warnings` | Pass locally and in final PR #1028 CI |
| `cargo bench -p slate-core --bench directory_page_bench --locked` | Pass; 10,000-directory first/middle/98%-late 200-row p50 pages measured 153.04/158.20/167.96 µs |
| `python scripts/generate-parity-matrix.py --validate-delivery-evidence` | Pass; 60 command rows and four issue surfaces |
| Complete Windows `slate-core` verification | Closed: the apparent nontermination came from mixing intentionally long randomized censuses into the ordinary local run. The separated non-census package tier passes completely and is now required by Windows CI. |
| mac Swift Quick Open tests | Pass in Actions run 29950708145 for PR #1025; the final W1 PR #1028 Mac XCTest suite also passed in Actions run 29980921180 |
| Final PR #1028 rollup | Seven of seven checks passed on `9fac9b2007d0cc7c12ef17fe12c36938796af826`: Mac XCTest, workspace fmt/Clippy, nextest, bench compile, SPDX, Windows x64, and Semgrep |
| RT17 PR #1030 rollup | Three of three path-selected checks passed on `86443682e7e2d41eebf6e660c9b5393ea713696a`: SPDX, Semgrep, and Windows x64 including full core, 174 Windows tests, live UIA/axe, retained evidence, and formatting |

## Reliability, performance and security notes

- Callback paths marshal asynchronously to the UI dispatcher. File-change refreshes coalesce for 150 ms; Quick Open ranks after a 60 ms debounce through one process-wide native lane, rejects canceled queued work before FFI, and suppresses stale publication; sidebar filtering debounces for 200 ms and rejects stale generations.
- Structural mutation commands are disabled while import owns the mutation lane; vault close/switch requests cancel import, bulk expansion, or ordinary child expansion and wait for their barriers.
- Store decoders are bounded and fail closed for malformed or forward schema versions. Per-vault store reads, locks, migration cleanup, and atomic replacement are anchored to opened Windows directory/file identities; reparse sentinels and deterministic directory-swap attempts are covered. File imports bound breadth, count and file size and reject reparse traversal.
- The LiveSync reader applies the same containment discipline on Windows: it pins the resolved vault root and each fixed child without following reparses, rejects non-disk/reparse targets before reading, and reopens the validated object rather than its pathname. Op-log saves and compaction hold their per-log mutation guard from before durable staleness-marker publication through event-index commit. Full event-index rebuilds remain atomic while try-locking only one log at a time; contention rolls back rows and markers for a later retry. The confirmed duplicate-row race now passes 100/100 stress repetitions and a deterministic contended-rebuild rollback/retry regression.
- No W1 surface adds an unbounded keystroke-size algorithm; W2 owns the formal §W-B editor benchmark. Quick Open, initial/root sidebar refresh, sidebar filtering, bulk Expand Loaded provider work, and ordinary child-folder provider/projection/restored-descendant work run off the UI thread. Root refresh and child expansion publish prebuilt, generation- and identity-guarded collections; all large file controls use recycling virtualization. A deterministic 5,001-child test proves the expansion caller returns before a blocked provider is released. Core combined and files-only pages are hard-capped range seeks over indexed order, enrich only returned rows, and install a SQLite progress handler for mid-statement cancellation. A post-query discriminator check rejects an external commit anywhere inside the multi-statement page. The 10,000-sibling late page remains within 9.8% of the first-page median. Ordinary and bulk work requested during refresh await its UI publication before taking a provider turn.

## Remaining release evidence

1. Record Narrator smoke plus NVDA and JAWS passes row-by-row in `w_c_matrix.md`.
2. Exercise all four built-in Windows Contrast themes and one customized theme, including runtime switching, selection/disabled states, visible boundaries and non-color cues.
These are explicit human verification residuals. They do not conceal missing W1 implementation, but they remain milestone release blockers under WGA-9.
