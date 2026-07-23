# W1 executable spec — Shell, vault lifecycle & workspace

Issues: W1-0 ([#911](https://github.com/coryj627/slate/issues/911)) · W1-1 ([#720](https://github.com/coryj627/slate/issues/720)) · W1-2 ([#721](https://github.com/coryj627/slate/issues/721)) · W1-3 ([#722](https://github.com/coryj627/slate/issues/722)) · W1-4 ([#723](https://github.com/coryj627/slate/issues/723)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue.
Program: [00_program.md](../00_program.md) (decisions 1, 2, 9, 15; DoD §W-C). Behavioral reference: the shipped mac shell (`SlateMacApp`, `MainSplitView`, `VaultPicker`, `WelcomeView`, `RecentVault(s)Store`, `FileTreeSidebar` → superseded by the FL milestone's sidebar if shipped, `Workspace/*` — tabs, splits, leaves, `WorkspaceStore` persistence) plus its parity-matrix rows from W0-4.

**Execution order: { W1-0 ∥ W1-1 }; W1-2 waits for both; W1-3/W1-4 wait for W1-1.** W1-0 requires the Windows runner from W0-2 and closes the no-clobber portability hole before file-mutation UI ships.

**W0 execution baseline (2026-07-19 — facts the original spec predates; anchors current at the W0-4 merge):**

- **Binding:** `uniffi-bindgen-cs` (w0_spec §Decision). The `SlateUniffi` classlib compiles the generated binding with `access_modifier = "public"` (`apps/slate-windows/uniffi.toml`); regenerate with `apps/slate-windows/generate-bindings.ps1` / `make regenerate-bindings-windows`.
- **Scaffold in place:** `apps/slate-windows/` solution (app + tests + `tools/{HostLogProbe,ParityHarness}`), `windows.yml` (x64 build via bindings generation, ARM64 `cargo check`, dotnet build/test, `dotnet format` gate, `cargo test -p slate-cli`). **`windows.yml` does not run slate-core's test suite** — W1-0 adds its Windows-runner tests as an explicit step (see below).
- **Host obligations partially met by W0-3:** `App.OnStartup` already installs the `host_logging` sink and routes native stderr into the durable app log (`HostLog.RedirectNativeStderrToAppLog` → `%LOCALAPPDATA%\Slate\logs\slate-windows.log`, `SLATE_LOG_DIR` override). The `--census-log-probe` startup mode is census-load-bearing (`HostLoggingCensus` launches the real WinExe) — **the W1-1 shell rework must preserve it**. The `VaultEventListener` install is the remaining W1-1 obligation (needs the real vault lifecycle).
- **Dispatch-affinity facts (W0 census evidence, binding contract for all W1 UI marshalling):** `ScanProgressListener` callbacks arrive **inline on the thread that called the scan** — never block them on UI work (`Dispatcher.InvokeAsync`, not `Invoke`; the synchronous form was a shipped-then-fixed Codoki High in #956). `VaultEventListener` dispatches from save-caller and background worker threads and must be marshalled the same way. `CommandAction` re-enters on its invoking thread.
- **Test conventions established:** xUnit censuses use `[Trait("census", …)]`, `CensusTier` (moderate per-PR / `SLATE_CENSUS_FULL=1` full tier), and a serialized test assembly; `Support/` has the recorders, `WorkPump` (single-threaded dispatcher stand-in), and fixture vault. FlaUI + axe-windows are **not yet in the solution** — W1-1 introduces the FlaUI/axe-windows §W-C gate project and wires it into `windows.yml` (first wave that needs it).
- **Storage precedent:** `%LOCALAPPDATA%\Slate\` is established as the device-local app-data root (logs since W0-3); `recent-vaults.json` and `command-palette-recents.json` land beside it.
- **Parity matrix live:** `parity_matrix.md` (generated at W0-4) carries the W1 rows — shell/vault (#720), 40+ sidebar command rows incl. the triaged `slate.file.*` set (#721/#744 split per its override table), the 16-leaf inventory (#722), and `slate.workspace.quickOpen` ⌘O→Ctrl+O (#723).
- **Visual base (owner call 2026-07-19): the WPF Fluent theme** — program decision 2 addendum; executable consequences in W1-1 item 8 below.

Porting doctrine for every W1–W6 spec: the mac view layer is the **behavioral spec** (states, transitions, announcements, keyboard model — including any Reduce Motion equivalents); the WPF implementation is idiomatic WPF (MVVM view models over the FFI), **not** a SwiftUI transliteration. Where mac behavior encodes an AppKit workaround (documented in memory/PRs), port the *intent*, and record the divergence in gap_analysis.

**Execution status (updated 2026-07-23): W1-0 through W1-4 and remediation
through W1-RT-18 are merged through PR #1031 / squash commit
`a443264f4be57e186d106001c3ab500e4622ec97`. W1-RT-19 is implemented as a
15-authored-file change on the current remediation branch; its first independent
review findings are remediated, and closure is gated by three exact-tree
re-reviews plus macOS CI.
Final code and automated closure remains open for W1-RT-19, and human
acceptance evidence remains pending.**

Three independent reviewers audited every W1 workstream
across completeness, correctness, maintainability, documentation, reliability,
performance, security, and accessibility, then independently reviewed the
completed remediations. Their earlier blockers—tab target/dirty/buffer semantics, exact
path identity, workspace restore invariants, real pane focus, modal Quick Open,
F2 rename, paged sidebar completeness, filtering/expansion responsiveness,
persistence bounds, import walking, IPC deadlines, UIA state, evidence
overclaim, host-side synchronous ranking and expansion, unbounded directory
levels, Windows core-suite portability, LiveSync handle containment, and op-log
event-index synchronization—were fixed and regression-tested. The final audit
then identified fatal event-rebuild read rollback, progress-event/UIA evidence,
Windows process-wide Quick Open serialization, and macOS page-one file-tree
responsiveness gaps. RT16–RT18 are closed; RT19 now moves whole-level native
loading, cancellable organization, and live key reorganization to one serial
non-main queue, publishes worker-built indexes without large MainActor scans,
retires obsolete and transient level/presentation/targeted-owner buffers through
a main-turn cleanup barrier, batches partial-child retry teardown, bounds
restored and Expand Loaded admission to one child level with deterministic
ordering, final recency persistence, and truthful completion copy, and
regression-locks async mutation/metadata races with
deadline-bounded tests; its final gates remain.
Ranked closure
evidence is tracked in
[`../reports/w1_post_merge_adversarial_audit_2026-07-22.md`](../reports/w1_post_merge_adversarial_audit_2026-07-22.md).
The generated parity matrix requires inventory-complete implementation/test
evidence for every W1 command row rather than inferring status from issue
ownership; the anchors remain surface-level, not a substitute for
command-specific behavior tests. RT14 merged as
`985061e9198d00db319701aed8f1d2b63ac86f0d` after its bounded-page, host,
migration, and performance gates passed. RT15 then merged as
`726157a858b06fcde13b6ed0936e753848675433`: the separated non-census package
command passed 1,615 unit, 364 integration, and 2 doctests and is required by
`windows.yml`; Windows LiveSync uses pinned no-follow handles; and per-log save,
compaction, and event-index rebuild synchronization has deterministic
contention plus 100-run race-stress coverage. Final PR #1028 revision
`9fac9b2007d0cc7c12ef17fe12c36938796af826` passed all seven checks, including
the complete Windows package and live FlaUI + axe gate. RT16 then merged in PR
#1029 as
`ce19b0ebe9def05c2d55febf807e5c75f2e77a83`; fatal op-log reads now roll back
the complete event-index rebuild transaction, with 1,616 unit, 364 integration,
and 2 doctests green in its bounded package evidence.
RT17 then merged in PR #1030 as
`268953adcaede360429dc41483604a175cff4333`; final Windows Actions run
29988127038 passed the live 2/2 scan RangeValue and sidebar UIA pattern census,
the expanded-state axe scan, the full core package, 174 Windows tests, retained
evidence upload, and formatting.
RT18 then merged in PR #1031 as
`a443264f4be57e186d106001c3ab500e4622ec97`; final-head Windows Actions run
29990822473 passed the complete core/CLI, 179-test Windows, live FlaUI/axe,
retained-evidence, and formatting gates.
Interactive FlaUI + axe-windows remains a required CI gate
(`SLATE_REQUIRE_UI_AUTOMATION=1`). Human
Narrator/NVDA/JAWS and the four built-in plus customized Contrast-theme pass
remain release-blocking §W-C evidence; see
[`../w_c_matrix.md`](../w_c_matrix.md) and
[`../reports/w1_execution_2026-07-20.md`](../reports/w1_execution_2026-07-20.md).
Checkboxes that combine implemented code with that external evidence
intentionally remain open.

## W1-0 · Atomic no-clobber rename on Windows/portable fallbacks — prerequisite PR

1. Replace the Windows `rename_no_replace` fallback in `crates/slate-core/src/vault/fs.rs` with a native atomic no-clobber primitive (`MoveFileExW` without `MOVEFILE_REPLACE_EXISTING`, or an equally strong documented API). Destination-exists maps through the existing typed conflict/error contract; no UI-side pre-check is accepted.
2. Close the remainder of #911 rather than hiding it behind the port: Linux musl/other targets use an available atomic no-replace primitive, or fail closed with an explicit unsupported error. A check-then-`fs::rename` path that can replace a destination is not an acceptable fallback.
3. Windows-runner tests cover success, destination-already-exists with both source and destination contents intact, file and directory cases supported by the provider, and a repeated destination-creation race stress. Existing macOS/Linux-glibc tests stay green. **CI wiring (W0 finding):** `windows.yml` currently runs only the bindings build, the .NET suite, and `cargo test -p slate-cli` — this PR adds a Windows-runner step for the affected slate-core tests (at minimum the vault/fs rename suite; a full `cargo test -p slate-core` step is preferred if runtime allows, recorded either way).
4. W1-2's rename/move UI depends on this issue. W1-1 may proceed in parallel because shell/vault-open work does not expose file rename.
5. **Windows rename-semantics evidence from W0:** the oplog compaction worker's tmp+rename-over rewrite genuinely fails against a read-only destination on Windows (the `EventKindsCensus` on_error trigger exploits exactly this). Treat it as a reminder that `MoveFileExW` failure modes (read-only destinations, sharing violations from AV/indexer holds) need typed mapping and a retry-free contract — surface them as the existing conflict/error types, never a silent fallback to replace semantics.

- [x] Atomic no-clobber semantics proven on Windows; portable fallback cannot silently clobber
- [x] Typed error mapping and race regression tests green

## W1-1 · App shell, window chrome, vault lifecycle — PR 1

1. WPF app: main window, split-view chrome (sidebar / content / right pane), menu bar with access keys, single-instance activation (second launch focuses + optionally opens the passed path).
2. Vault lifecycle parity: welcome view (no vault), vault picker (folder chooser + validation via core), recent vaults (jump list + welcome list), close-vault flow with unsaved-changes parity (`CloseVaultSheetParity` semantics). Recent vaults are **global, device-local host state**, mirroring mac's `~/Library/Application Support/Slate/recent-vaults.json`; Windows persists `%LOCALAPPDATA%\Slate\recent-vaults.json`, and the jump list mirrors that store. It never enters per-vault `.slate/prefs.json` and does not sync with a vault.
3. Windows path adapters in `VaultProvider` land here (program decision 9): long paths, reserved names, and case-insensitivity probes. Line-ending parity fixtures cover LF, CRLF, and mixed endings through read, edit, save, and reopen. Core writes caller-supplied contents without normalization; Windows must match mac output for the same fixture + edit sequence — **core-side PRs** if genuine gaps are found, with censuses.
4. Scan progress: `ScanProgressListener` → progress UI + UIA `RaiseNotificationEvent` progress etiquette (throttled, final summary announced), routed through the W7-2 dispatcher core, which lands with this wave (program wave table). The shell also installs the **`VaultEventListener` + `host_logging` sink** at startup (host obligations, w0 baseline).
5. Per-monitor-v2 DPI; window state persisted (size/position/monitor, degraded gracefully on monitor loss).
6. **Seeds the chord table** (program decision 12): the declarative mac→Windows chord file is created here with the shell/vault chords; every later spec's named chord (Ctrl+O, Ctrl+F, …) is a normative entry added by the issue that ships it; W5-1 finalizes the table and wires the drift tests. **The table derives from the mac registry at port start — where a spec sentence and the table disagree, the table wins.**
7. **Seeds the provisional theme token set**: Slate-owned dark/light token *structure* lands here with values that already meet **APCA Lc ≥ 75** for text-bearing pairs (checked ad hoc via the existing mac APCA test approach — everything after this consumes tokens, never literal brushes). W8-2 finalizes those values and moves their check into CI; Contrast-theme resources are a separate dynamic `SystemColors` mapping finalized and tested in W8-2, not hard-coded APCA token values.
8. **Fluent theme adoption (program decision 2 addendum, owner call 2026-07-19).** The shell ships on the first-party WPF Fluent theme:
   - Load `Fluent.Light.xaml` / `Fluent.Dark.xaml` explicitly from Slate's theme state (item 7's tokens layer **on top of** Fluent's control styles; Slate tokens own every text-bearing surface Slate draws). The experimental `ThemeMode`/WPF0001 API is not taken as a dependency until it exits experimental — record the re-evaluation at each wave-close Fluent currency check.
   - **Contrast themes — two layers, both shipped here.** The automatic `Fluent.HC.xaml` switch covers only Fluent's **stock control styles**; it cannot retarget Slate-owned token keys. W1-1 therefore also ships a **Slate Contrast resource dictionary** — the same token keys resolving through dynamic `SystemColors` pairs — selected reactively on `SystemParameters.HighContrast` (both directions, while running). Assert resource precedence and runtime switching on every Slate-owned shell surface; a Contrast transition must never leave Slate-drawn text on fixed dark/light brushes. The decision-11 acceptance (all four built-in Contrast themes + one customized, selected/disabled states, visible boundaries, no color-only meaning) runs against both layers; W8-2 hardens values and expands coverage — it does not introduce the mechanism.
   - **Mica backdrop policy decided here:** text-bearing surfaces must sit on solid token-backed backgrounds so APCA stays measurable; if the backdrop bleeds into any text surface, disable it (`Switch.System.Windows.Appearance.DisableFluentThemeWindowBackdrop`) and record the call.
   - **§W-C under Fluent:** the axe-windows/FlaUI gate runs against the Fluent-styled controls from this first PR — Fluent restyles stock templates, so keyboard focus visuals, name/pattern exposure, and DPI behavior are asserted on what actually ships, not on Aero defaults.
   - Fluent is upstream-experimental: record behavior observations in the PR (the theme dictionaries version with the .NET 10 SDK pinned in `global.json`) and re-check at each wave close.

- [x] Shell + vault open/close/welcome/recents + scan progress, keyboard-complete
- [x] Path adapter gaps filed/fixed core-side with censuses
- [ ] §W-C rows for shell surfaces green (axe-windows + FlaUI smoke, introduced this PR) under the Fluent theme
- [ ] Fluent adoption per item 8: light/dark dictionaries wired to theme state, HC acceptance run, Mica policy recorded, `--census-log-probe` startup mode preserved

## W1-2 · Files sidebar — PR 2

**Depends on W1-0 and W1-1.**

1. Parity target = the shipped sidebar at port start — the matrix rows decide, and they now exist (W0-4): the FL program's shipped majority including derived row metadata, multi-select + batch ops (incl. FL5-3b batch tags), sort/group/pin/shortcuts/recents, in-sidebar filter, tag tree/navigation (FL-11), folder notes (FL-12, #957), and the gated dual-pane container (FL-13, #959). The matrix's `#721 (W1-2)` command rows — including the FL04-A-projected `slate.file.*` import-engine pair — are the burn-down list; re-run the generator at execution start to absorb any FL close-out rows shipped since.
2. All mutations route through existing core session APIs — creates through O's never-clobber **`create_exclusive`** protocol (the only create path mac uses since #796/#838), renames/moves through the same rewrite engine as mac (§W-A covers the rewrites). API *choice* is part of parity: a plain write path would silently break O's marker/op-log correctness.
3. UIA: TreeView peer with level/position/expandability; type-ahead; the mac announcement grammar via canonical events.
4. Selection model: single-click opens (label and row — the mac onDrag/selection lesson is a *behavioral* requirement: clicking a row's label must open it), keyboard parity (arrows, expand/collapse, F2 rename).
5. Ordinary directory expansion performs native loading, sorting, projection, and restored-descendant construction away from the UI thread through the serialized tree-provider lane. Collapse, refresh, bulk work, and close cancel or supersede it; operation, tree-generation, root-identity, and expanded-state guards precede one prebuilt collection publication. Native UIA ExpandCollapse and Right/Left behavior retain focus. Both hosts consume hard-capped core/UniFFI range pages: deterministic dirs-first combined order plus a direct files-only scope, opaque session/scope/parent/snapshot-bound continuation, exact truncation, SQLite progress cancellation, and post-query stale-snapshot failure. Directory summaries and immediate counts are page-local over indexed order rather than a complete directory-level array.

- [x] Full tree CRUD + link-rewrite parity (§W-A rows)
- [ ] UIA tree semantics + announcements; §W-C green

## W1-3 · Workspace: tabs, splits, leaves, persistence — PR 3

1. Parity with the U-milestone workspace model: tabs, horizontal/vertical splits, leaf kinds (note, canvas, and every leaf kind shipped by port start — matrix rows), focus routing, "open in new tab/split" targets, tab context menus.
2. The workspace *model* semantics (what may dock where, leaf context matrix) mirror `WorkspaceModel`/`WorkspaceState` behavior; persistence format is the same schema `WorkspaceStore` persists (shared store read/written via core if canonicalized by then; else same-shape JSON with a recorded schema pin) so a synced vault restores equivalent layouts cross-platform where feasible; divergences recorded. **Status at W0-4: the store is not canonicalized in core** — plan for the same-shape-JSON path with the schema pin unless a core canonicalization lands first. **Two distinct kind inventories, both in scope:** (a) the **persisted tab-content kinds** — `enum EditorItem`: `markdown`, `canvas`, `base`, `savedQuery`, `dashboard`, `graph` (a path-less singleton) — which is what `WorkspaceStore` round-trips, including the U1-6 forward-compatibility contract (an unknown discriminator drops that tab, never fails the workspace — mirror it); and (b) the 16-row right-pane `enum Leaf` inventory (outline, backlinks, outgoingLinks, connections, embeds, math, code, diagrams, tasks, tasksReview, history, citations, bibliography, queries, basesDock, syncDiagnostics). Acceptance adds **cross-platform persistence round-trip fixtures** (mac-written JSON restores equivalently on Windows and vice versa where kinds are shipped) plus the unknown-kind graceful-drop test.
3. UIA: tabs as TabControl peers with SelectionPattern; splits navigable; focus events fire on route changes (mac focus-routing tests' scenarios re-expressed in FlaUI).

- [x] Tabs/splits/leaves + persistence + focus routing parity
- [ ] §W-C green; workspace FlaUI scenario suite

## W1-4 · Quick switcher — PR 4

1. Ctrl+O (chord table, decision 12) fuzzy file open over the **W0.5-2 core ranking** — zero C# scoring. *(Refreshed 2026-07-12: #863/PR #885 moved mac Quick Open to ⌘O and reassigned ⌘T to Duplicate Tab; per W1-1's rule, the chord table — not this sentence — is normative if they ever disagree.)* The `switcher_rank` / `switcher_display_name` FFI shipped with W0.5-2 and is already bound and `public` in the `SlateUniffi` assembly — the view model consumes it directly.
2. Parity behaviors: recents-first empty state, result count announcements, Enter/modifier-Enter open targets, Esc restore. **Announcement prerequisite (completed by #963):** typed switcher-count events and their rendered strings are core-owned, pinned by goldens, and consumed by both hosts; neither host composes parallel count copy.
3. Ranking responsiveness is a cross-host contract: candidate capture does not call FFI per file on the UI actor; top-K ranking runs after a 60 ms debounce away from the UI thread; native calls are serialized process-wide across sheet lifetimes; query mutation synchronously advances a monotonic generation that rejects superseded publication while retaining and revealing a still-ranked selection when the lazy list returns; synthesized hover beneath a stationary pointer cannot replace it; every explicit dismissal invalidates publication before removing the sheet; and an accessible loading state replaces stale rows while the current query is in flight.

- [ ] Switcher over core ranking; behavior + announcement parity; §W-C green
