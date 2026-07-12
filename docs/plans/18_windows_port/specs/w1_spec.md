# W1 executable spec — Shell, vault lifecycle & workspace

Issues: W1-1 ([#720](https://github.com/coryj627/slate/issues/720)) · W1-2 ([#721](https://github.com/coryj627/slate/issues/721)) · W1-3 ([#722](https://github.com/coryj627/slate/issues/722)) · W1-4 ([#723](https://github.com/coryj627/slate/issues/723)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue.
Program: [00_program.md](../00_program.md) (decisions 1, 2, 9, 15; DoD §W-C). Behavioral reference: the shipped mac shell (`SlateMacApp`, `MainSplitView`, `VaultPicker`, `WelcomeView`, `RecentVault(s)Store`, `FileTreeSidebar` → superseded by the FL milestone's sidebar if shipped, `Workspace/*` — tabs, splits, leaves, `WorkspaceStore` persistence) plus its parity-matrix rows from W0-4.

**Execution order: W1-1 → { W1-2 ∥ W1-3 ∥ W1-4 }.**

Porting doctrine for every W1–W6 spec: the mac view layer is the **behavioral spec** (states, transitions, announcements, keyboard model — including any Reduce Motion equivalents); the WPF implementation is idiomatic WPF (MVVM view models over the FFI), **not** a SwiftUI transliteration. Where mac behavior encodes an AppKit workaround (documented in memory/PRs), port the *intent*, and record the divergence in gap_analysis.

## W1-1 · App shell, window chrome, vault lifecycle — PR 1

1. WPF app: main window, split-view chrome (sidebar / content / right pane), menu bar with access keys, single-instance activation (second launch focuses + optionally opens the passed path).
2. Vault lifecycle parity: welcome view (no vault), vault picker (folder chooser + validation via core), recent vaults (jump list + welcome list, persisted like `RecentVaultsStore` — storage goes through the same core/prefs path the mac store uses, or its W0.5-era successor), close-vault flow with unsaved-changes parity (`CloseVaultSheetParity` semantics).
3. Windows path adapters in `VaultProvider` land here (program decision 9): long paths, reserved names, case-insensitivity probes, and **CRLF discipline fixtures** (write LF, tolerate CRLF on read) — **core-side PRs** if gaps found, with censuses.
4. Scan progress: `ScanProgressListener` → progress UI + UIA `RaiseNotificationEvent` progress etiquette (throttled, final summary announced), routed through the W7-2 dispatcher core, which lands with this wave (program wave table). The shell also installs the **`VaultEventListener` + `host_logging` sink** at startup (host obligations, w0 baseline).
5. Per-monitor-v2 DPI; window state persisted (size/position/monitor, degraded gracefully on monitor loss).
6. **Seeds the chord table** (program decision 12): the declarative mac→Windows chord file is created here with the shell/vault chords; every later spec's named chord (Ctrl+O, Ctrl+F, …) is a normative entry added by the issue that ships it; W5-1 finalizes the table and wires the drift tests. **The table derives from the mac registry at port start — where a spec sentence and the table disagree, the table wins.**
7. **Seeds the provisional theme token set**: dark/light token *structure* lands here with values that already meet **APCA Lc ≥ 75** for text-bearing pairs (checked ad hoc via the existing mac APCA test approach — everything after this consumes tokens, never literal brushes); W8-2 finalizes values, adds High Contrast, and moves the check into the CI gate — it does not first create the set.

- [ ] Shell + vault open/close/welcome/recents + scan progress, keyboard-complete
- [ ] Path adapter gaps filed/fixed core-side with censuses
- [ ] §W-C rows for shell surfaces green (axe-windows + FlaUI smoke)

## W1-2 · Files sidebar — PR 2

1. Parity target = the shipped sidebar at port start (FL milestone if shipped: tree, create/rename/move/delete with link-rewrite via core, drag-drop, context menus, focus/selection model; else the pre-FL `FileTreeSidebar` capability set). Matrix rows decide.
2. All mutations route through existing core session APIs — creates through O's never-clobber **`create_exclusive`** protocol (the only create path mac uses since #796/#838), renames/moves through the same rewrite engine as mac (§W-A covers the rewrites). API *choice* is part of parity: a plain write path would silently break O's marker/op-log correctness.
3. UIA: TreeView peer with level/position/expandability; type-ahead; the mac announcement grammar via canonical events.
4. Selection model: single-click opens (label and row — the mac onDrag/selection lesson is a *behavioral* requirement: clicking a row's label must open it), keyboard parity (arrows, expand/collapse, F2 rename).

- [ ] Full tree CRUD + link-rewrite parity (§W-A rows)
- [ ] UIA tree semantics + announcements; §W-C green

## W1-3 · Workspace: tabs, splits, leaves, persistence — PR 3

1. Parity with the U-milestone workspace model: tabs, horizontal/vertical splits, leaf kinds (note, canvas, and every leaf kind shipped by port start — matrix rows), focus routing, "open in new tab/split" targets, tab context menus.
2. The workspace *model* semantics (what may dock where, leaf context matrix) mirror `WorkspaceModel`/`WorkspaceState` behavior; persistence format is the same schema `WorkspaceStore` persists (shared store read/written via core if canonicalized by then; else same-shape JSON with a recorded schema pin) so a synced vault restores equivalent layouts cross-platform where feasible; divergences recorded.
3. UIA: tabs as TabControl peers with SelectionPattern; splits navigable; focus events fire on route changes (mac focus-routing tests' scenarios re-expressed in FlaUI).

- [ ] Tabs/splits/leaves + persistence + focus routing parity
- [ ] §W-C green; workspace FlaUI scenario suite

## W1-4 · Quick switcher — PR 4

1. Ctrl+O (chord table, decision 12) fuzzy file open over the **W0.5-2 core ranking** — zero C# scoring. *(Refreshed 2026-07-12: #863/PR #885 moved mac Quick Open to ⌘O and reassigned ⌘T to Duplicate Tab; per W1-1's rule, the chord table — not this sentence — is normative if they ever disagree.)*
2. Parity behaviors: recents-first empty state, result count announcements (canonical events), Enter/modifier-Enter open targets, Esc restore.

- [ ] Switcher over core ranking; behavior + announcement parity; §W-C green
