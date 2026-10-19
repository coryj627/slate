# 43 — Reassessment evidence: the open mac issues with no milestone (2026-10-19)

Status: evidence for `00_program.md` §11, written when the owner paused the queue on
2026-10-19. Every candidate issue was read in full (its body and every comment) and its cited
code was checked against `main` at b63f6dbb. PR #1518 (22a) was still open then, so its branch
was read where an issue depends on it; it has since merged (3f0b45de).

**This document and §11 use different ids.**

| Here | In §11 |
|---|---|
| Decisions D1–D19 | RD1–RD19 (§11.5), so they are not confused with plan 41's and plan 43's D-numbers |
| Working chunk ids | PR numbers (§11.4) |

| Chunk | PR | Chunk | PR | Chunk | PR |
|---|---|---|---|---|---|
| C1 | 26 | C9 | 33 | C22 | 43 |
| C2 | 27 | C11 | 34 | C26 | 44 |
| C24 | 28 | C16 | 35 | C27 | 45 |
| C3 | 29 | C17 | 36 | C20 | 46 |
| C4 | 22c | C10 | 37 | C31 | 47 |
| C5 | 30 | C13 | 38 | C16b | 48 |
| C6 | 21b | C19 | 39 | C23 | 49 |
| C7 | 22b | C28 | 40 | C25 | 50 |
| C15 | 31 | C12 | 41 | C29 | 51 |
| C8 | 32 | C14 | 42 | C21 | 52 |
| | | | | C30 | 53 |

Where this document and §11 differ, §11 wins.

- **Base:** `origin/main` at **b63f6dbb** (#1523, 2026-10-19 05:46Z). PR 22a (#1518) is still
  open (head aa7fb455, which is 220066e7 plus a merge of b63f6dbb; mergeable). Where an issue
  depends on it, its branch was read too.
- **Method:** each of the 75 candidates was read in full (body and every comment, via
  `gh issue view --json`) and its cited code was checked with `git show` / `git grep` at
  b63f6dbb. Upstream releases were checked on crates.io and GitHub on 2026-10-19. Two tools were
  re-run on an exported copy of the tree: `a11y-check 0.7.0` for #1371, and both reconciliation
  scripts with `--check` for #1376 and #1386.
- **Sizes** use the brief's scale: **S** is under about 150 changed lines with facts, **M** about
  150 to 1,000, **L** over 1,000 or design or spike first.
- **Lanes:** `rust`, `swift`, `a11y/VO` (a11y-check plus a VoiceOver check or pass), `windows`.
  Here `windows` means the change touches Windows code or needs the Windows binding. The Windows
  lane runs on every mac PR anyway (program §2).

## (a) Summary

All 75 candidates were checked against `origin/main` b63f6dbb.

**Already fixed or obsolete (two):**
- #640: `slate query` shipped in #771 in July.
- #445: its findings #446 and #447 were closed by #1374, and the scanner has not run since
  June.

**Fixed when #1518 merges (one):** #1474. 22a moves the op-log, the history-prefs writer and
`.slate/tmp` beneath the held root on macOS and Linux. Its leftovers are already tracked: #1513,
and 22W for Windows.

**Everything else is still true.** A few are true by code or report but not confirmed at
runtime, and are marked as such: #1379, #1400, #1434, #1470, #1462 (cause not diagnosed) and
#1497 (never investigated).

**By scope:**

| Scope | Count |
|---|---|
| mac-only | 32 |
| both hosts | 7 |
| core with mac impact | 12 |
| process, docs, deps or test infrastructure | 16 |
| Windows-only | 4 |
| core-only with no mac impact | 2 |
| no-action | 2 |

That leaves **67 mac-relevant issues**:
- Four are dependency bumps blocked upstream: #480, #998, #1457 and #1493. They go on a watch
  list.
- #1414 is a two-host feature and should go to a milestone.
- #1474 closes with #1518.
- The other 61 fall into **31 PR-sized chunks**, three of which are the remaining design-series
  PRs (21b, 22b and "22c" for #1513).
  - The 31 chunks close 59 issues.
  - #940 moves to Milestone W after 22b.
  - #1323 is narrowed to its Windows items.

**What matters most:**
- **Security:** canvas link and file cards hand untrusted targets to the system opener (#1367,
  #1368).
- **Data integrity:**
  - an undo before the editor's first highlight is lost and clears the undo history (#1503);
  - closing the main window leaves unsaved buffers behind no window (#1402);
  - two decided #1125 tracks, identity-checked undo (#1469) and Trash verification (#1468).
- **An accessibility blocker:** Quick Open's bare arrow keys do not move its selection (#1379).
- **Wrong or missing speech:**
  - canvas and Bases failures speak the Swift debug description of `VaultError` (#1364, #1399);
  - VO+Space does nothing on canvas outline rows (#1411);
  - several verbs give no confirmation (#1403, #1405).

**Proposed order:** nine waves of at most three agents.
1. Security and data integrity first.
2. The #1513 follow-up (22c, with the re-arm switch on) right after #1518 merges, then 22b.
   21b runs in parallel because it touches only Windows files.
3. Accessibility blockers, then speech, reliability, performance and test hygiene.

Nineteen owner decisions are listed in (f), each with a recommended answer. Only D1 (#1402)
blocks the first wave.

## (b) Per-issue table

Scope: `mac` = mac-only; `both` = both hosts; `core→mac` = core with mac impact; `core-only` =
core with no mac impact; `win` = Windows-only; `proc` = process, docs, deps or test
infrastructure; `no-action`. Impact: `sec` security or containment; `data` data loss or
integrity; `a11y-B` accessibility blocker; `a11y-D` accessibility degradation; `rel` reliability
or correctness; `perf`; `maint`; `test`; `docs`; `enh` enhancement. Chunk ids (C…) refer to (c).

| # | Issue (short) | Scope | Still true on main? (evidence at b63f6dbb) | Impact | Size | Lanes | Dependencies, interlocks | Owner decision |
|---|---|---|---|---|---|---|---|---|
| #445 | a11y-inspect summary | no-action | **No longer meaningful.** Its findings #446 and #447 closed 2026-10-17 through #1374 (e0755fd8). The scanner's last scan was 2026-06-24, so "reconciles itself on the next scan" (PR 1 spec) never happened. | a11y (tracking) | — | — | — | D16: close by hand |
| #480 | citationberg Git patch, move to a registry release | proc (deps) | Yes. `Cargo.toml:169-170` still has `[patch.crates-io] citationberg` at rev 06a591e2. On crates.io the newest citationberg is still 0.7.0 and hayagriva 0.10.1 (unchanged since the issue). `.cargo/audit.toml` has no quick-xml ignores. | sec (advisories already resolved by the patch) | S once unblocked | rust | Upstream release. `docs/runbooks/citation-dependency-policy.md` says do not close before the qualification run. | none (watch list) |
| #640 | `slate query` CLI verb | core-only (CLI) | **Fixed.** `slate query <vault>` with `--base` (optional `--view`) or `--saved`, `--limit`, `--this` and `--format` json, csv or markdown shipped in #771 (6be62f4c, 2026-07-08): `crates/slate-cli/src/commands/query.rs`, `main.rs:161`. | enh | — | — | — | Close as delivered (an inline query-string form would be a new issue) |
| #642 | l10n umbrella | no-action | Yes, as a tracking umbrella by its own scope. Its deliverable `docs/plans/14_l10n.md` shipped (#647). | docs | — | — | — | none (stays a tracking issue) |
| #940 | Descriptor-relative session interior | both (+ core) | Yes on main. 22a (#1518) delivers the core on macOS and Linux (`FsRoot`, the SQLite VFS hook, the A→B→A fact). Remaining: 22b (mac display and audit, plus the host fact `a_renamed_open_vault_keeps_working_and_can_show_its_new_path`, which needs #1513) and 22W (Windows). | sec | 22b S–M; 22W M | 22b: swift, a11y; 22W: windows | #1518 → C4 (22c, #1513) → C7 (22b). 22W goes in Milestone W. | D15: after 22b, move to Milestone W |
| #998 | uniffi 0.32 blocked | proc (deps) | Yes. The newest uniffi-bindgen-cs tag is v0.11.1+v0.31.0 (2026-10-09) and there is still no 0.32 build. uniffi 0.32.2 has shipped. The issue's three pin sites are now one file, `apps/slate-windows/uniffi-bindgen-cs.version` (v0.11.0+v0.31.0). | maint | S once unblocked | rust, swift, windows | Upstream. Blocker 2 (Namespace cache path) now matters only on the Namespace fallback, because the mac lanes run on the Studio. | none (watch list) |
| #1051 | Windows accent/raised at Lc 74.46 | win | Yes on Windows (`Slate.Light.xaml`). Mac's tokens have no raised surface (plan 41 §1.2). | a11y-D (latent: no surface uses the pair) | S | windows | W8-2 palette pass | Move to Milestone W |
| #1293 | Change feed | both (only Windows left) | 21a (#1512) and 21c (#1520) merged. 21b (Windows host) has not started. | rel | 21b L | windows | After #1523 (merged). Better after C15 (#1519), so Windows' graph probe also follows other sessions. | Close by hand when 21b lands |
| #1323 | W7-7 timing and clipboard flakes | proc (mostly Windows) | Yes. The mac fact `LeafContextMatrixTests.swift:94` and the Rust fact `reading.rs:4820` (`many_unique_citations_stay_subquadratic`) are unchanged. The other eight are Windows facts. | test | mac + Rust items S | swift, rust | Windows items also tracked in #1285, #1294, #1306, #1315, #1316 and #1321 | D17: fix the mac and Rust items with Refs, then narrow to Windows |
| #1363 | Canvas ready publish keeps a selection the snapshot lost | mac | Yes. The ready arm of `installPreparedSnapshot` (`Canvas/CanvasDocument.swift:580-628`) never reconciles `selection.selected`; only `reloadAfterMutation` (`:698-712`) does. | rel | S | swift | C9 (same file as #1364's `:766`) | D2 |
| #1364 | Canvas failures speak the Swift debug description of `VaultError` | mac | Yes. There are 21 `error.localizedDescription` sites under `Canvas/` (for example Duplicate, `AppState+CanvasExtras.swift:223`). Only `AppState+CanvasCreate.swift:245` uses the humanizer. | a11y-D | S–M | swift, a11y/VO | Twin of #1399. Core already renders this text (`vault_error_detail`, `a11y.rs:3401`, FFI `lib.rs:11306`), and Windows uses it, guarded by `VaultErrorDetailCensus`. | D8 |
| #1365 | Canvas paint order of plain vs coloured connections | mac | Yes. `CanvasRendererView.swift:178` puts plain connections at −1, `:538` puts coloured ones above the cards, `:561` puts chips above those. | rel (visual only) | S | swift | Pinned by `CanvasRendererTests.swift:493` | D3 |
| #1367 | Canvas link card opens any URL scheme | mac | Yes. `CanvasContainerView.swift:169-177` opens whatever `URL(string:)` parses. `canvasAddLinkCard` (`AppState+CanvasCreate.swift:342-345`) checks only that a scheme exists. Note links allow only http, https and mailto (`AppState.swift:10430-10440`). | sec | S | swift, a11y | Contract 34 CD-35. Share one allowlist with `Reading/ReadingLinkRouter.swift:157`. | none |
| #1368 | Canvas media gate has no vault containment | mac | Yes. `AppState+CanvasExtras.swift:505-524` checks `fileExists` on vault + target text and the name's media class, then calls `externalOpener`. There is no symlink resolution and no containment check. | sec | S–M | swift | CD-38. #1414 would reuse the same check. | none |
| #1369 | Add Media and Locate File offer any non-note file | mac | Yes. `listCanvasMediaPaths` (`AppState+CanvasCreate.swift:172-197`) keeps every file that is not .md, .markdown or .canvas. | rel | S | swift | Uses C1's media rule (G2-6, G2D-6) | none |
| #1370 | File-card hint says "Opens the note in this tab." on non-notes | both | Yes on mac (`CanvasOutlineView.swift:486-495`) and on Windows (`CanvasDocumentViewModel.cs:5471`). | a11y-D | S–M | swift, a11y/VO, windows (+ rust if core owns it) | Hint should follow C1's classification. Same view as #1411. | D7 |
| #1371 | a11y-check 0.7.0 finds 49 errors | proc (test) | Yes. Re-run here at b63f6dbb with 0.7.0: score 82.9; 49 errors (41 `duplicate-button-label`, 8 `picker-style-missing-accessibility`) and 9 warnings. The CI pin is still `bcaddd56` (`a11y-check.yml:45`). | test (the 8 picker findings may be real a11y defects) | M | swift, a11y | The runner image pins the same ref (`ci/mac-runner/image/03-toolchain.pkr.hcl`). Files overlap #1372. | D11 |
| #1372 | Two property sheets read a name twice | mac | Yes. `AddPropertySheet.swift:65-72` has `Picker("Property type")` plus `.labelsHidden()` plus the same `.accessibilityLabel`. In `BulkRenameSheet.swift`, the captions at `:45`, `:52-60` and `:64` are not hidden. | a11y-D | S | swift, a11y/VO | Same chunk as #1371 | none |
| #1376 | Contract 34 reconciliation tables out of step | proc (docs) | Yes. `scripts/canvas_reconciliation.py --check` still reports "the reconciliation tables in the doc differ" on a b63f6dbb tree, and no workflow runs it (the generator now has a `CLOSED` map). | docs | S | windows (needs the generated C# binding) | Twin of #1386 | none |
| #1377 | Search overlay rows need Full Keyboard Access | mac | Yes. Rows are `.plain` buttons bound with `.focused` (`SearchOverlay.swift:378`, `:549`), and `rowsTakeFocus` is `NSApp.isFullKeyboardAccessEnabled` (`:581`). The header comment records the limit. | a11y-D (keyboard-only users with FKA off; per the comment, VoiceOver turns FKA on for the app) | M | swift, a11y/VO | Replaces #1113's focus walk: rewrite those facts first (TGC-17 rule). Same surface as #1497. | D4 |
| #1379 | Quick Open's bare ↑/↓ go to the field | mac | Yes by code. The mask at `QuickSwitcherView.swift:279-280` includes `.function`, which macOS sets on every arrow key. `QuickSwitcherViewTests.swift:41` pins the wrong behaviour. The palette was fixed in PR 6 (`CommandPaletteView.swift:513-518`). Not reproduced on hardware. | a11y-B (keyboard: arrows cannot move the selection) | S | swift, a11y/VO | C5 | none |
| #1380 | Palette key monitor acts on keys from other windows | mac | Yes. `CommandPaletteView.swift:669-707` routes without checking `event.window`. Quick Open (`QuickSwitcherView.swift:347`) and Move to Folder (`MoveToFolderSheet.swift:524`) install the same app-wide kind of monitor. | rel | S | swift | C5 | none |
| #1382 | Palette field hint omits Home, End, Page Up/Down and Escape | mac (copy may move to core) | Yes (`CommandPaletteView.swift:171-173`). Windows' HelpText mentions them. | a11y-D | S | swift, a11y/VO (+ rust, windows if core owns it) | The runbook's §Q quotes the mac hint | D7 |
| #1386 | Contract 35 reconciliation tables out of step | proc (docs) | Yes. `scripts/graph_reconciliation.py --check` reports the same difference, and nothing in CI runs it. | docs | S | windows | Twin of #1376 | none |
| #1388 | Query-block base cards are titled in Swift | core→mac | Yes. `Bases/BaseEmbedDocument.swift:156-164` and `viewSuffix` at `:625`, registered in `CoreRenderedCopyCensusTests.swift:42-46`. `ResolvedEmbed` (`a11y.rs:3287`) has no kind for these cards. | maint (core owns copy) | M | rust, swift, windows (bindings) | #1390 rewrites its sentence. FFI change, so both bindings are regenerated. | none |
| #1389 | Code preamble verbosity setting changes nothing | both | Yes. `CodeBlockView.swift:99` always calls `codeBlockPreamble(language:source:)`. `codePrefs` (`AppState.swift:8230-8236`) only persists and announces. Core's `code_block_preamble` (`code.rs:204`) takes no verbosity. Windows mirrors the dead setting (`EditorInteractions.cs:132`). | a11y-D | S to remove the modes, M to build them | swift, windows (+ rust to build) | The census entry `settingsPreambleSamples` | D6 |
| #1390 | Contract 38's EmbedPreviewShown designation is out of date | proc (docs) | Yes. `docs/plans/18_windows_port/a11y_trigger_designations.json:267` and the generated ledger (contract 38, `:825`) still use the present tense. | docs | S | windows (ledger census) | With #1388 | none |
| #1393 | Embed batch publish drops live-buffer keys | mac | Yes. `AppState.swift:13272` replaces the map wholesale (`currentNoteEmbedResolutions = resolutions`), while the single-key merge at `:13346` adds to it. | rel | S | swift | — | none |
| #1394 | New Canvas and ghost-row create report a landed create as failed | mac | Yes. `Canvas/AppState+CanvasActions.swift:216` and `Graph/AppState+Connections.swift:285` still call `createExclusive`. `createReporting` exists (`AppState+CreateReporting.swift:23`). | rel (a retry creates a second file) | S–M | swift, a11y | Same New Canvas lines as #1364 (do it after C9). Trigger-ledger rows. | none |
| #1395 | Unindexed create and save caveats run into the next sentence | core→mac (both hosts' speech) | Yes for a non-empty reason and for the save twin (`a11y.rs:2170`, `:3419`). `lead_then_sentence` (`:3270`) is not applied. #1396 fixed only the empty-reason create case. | a11y-D | S | rust, swift, windows (corpus) | With #1394 | none |
| #1398 | A .base with broken YAML can't be repaired, and the error doesn't say why | both (+ core) | Yes. `bases/mod.rs:303-325` turns the error into a `ParseFailed` warning, `serialize_base` rejects the edit (`:618-625`), and mac shows `noExecutableViewsMessage` (`BaseDocument.swift:1202`). | a11y-D + enh | M | rust, swift, a11y/VO, windows | Windows twin #1397 (open) | D5 |
| #1399 | Bases failures speak the Swift debug description | mac | Yes. 36 sites: 26 in `Bases/AppState+Bases.swift`, 3 in `DashboardDocument.swift`, 5 in `BaseEmbedDocument.swift`, 1 in `BaseEmbedView.swift`, 1 in `BaseQueryBuilderSheet.swift`. `BaseDocument.failureDetail`'s fallback adds one more. | a11y-D | M | swift, a11y/VO | Same fix as #1364 | D8 |
| #1400 | A Base toolbar in an unfocused pane acts on the focused pane's Base | mac | Yes by code (not reproduced). `basesEditViewFilters`, `basesRefresh` and `basesResultsPopover` read `activeBaseDocument` (`AppState+Bases.swift:2097-2098`, `:3050`, `:2852`). They are called from `BaseContainerView.swift:117`, `:146`, `:158` and `:223`, and unfocused panes render live (`WorkspaceView.swift:83`). | rel (an edit can land on the wrong Base) | S | swift | Bases files shared with #1399 and #1436 | none |
| #1402 | Closing the main window with unsaved edits doesn't ask | mac | Yes. There is no `windowShouldClose` or `NSWindowDelegate` anywhere in `Sources/SlateMac`. | data (latent: buffers behind no window are lost on a crash or force quit) | M | swift, a11y/VO | #1403; PR 15b's termination fence | D1 |
| #1403 | Close Vault with one dirty note speaks no close line | mac | Yes. The `.closeVault` arm of `applyPendingNavigation` calls `closeVault()` and posts nothing (`AppState.swift:24783-24795`). The multi-note resolvers do post (`:11844`, `:11873`). | a11y-D | S | swift, a11y/VO | #1402 | none |
| #1405 | Shortcuts' Remove, Move Up and Move Down are silent when the write is refused | mac | Yes. `Sidebar/SidebarSectionsView.swift:220`, `:224` and `:229` (`try?` remove), and `moveSidebarShortcut` uses `try? mutateSidebarOrganization` (`AppState.swift:7604-7612`). | a11y-D (breaks the never-silent rule) | S | swift, a11y/VO | #1521 (same store) | none |
| #1407 | Filter-overlay selection isn't carried through a rename | mac | Yes. `filterListSelection` (`FileTreeSidebar.swift:3527`) is written only at `:5111`, `:5188` and `:5248`. | a11y-D + rel (the command target names a path that no longer exists) | M | swift, a11y/VO | #1409 (same file) | none |
| #1408 | `BasesTabRoutingTests` flake | proc (test) | Yes. `BasesTabRoutingTests.swift:680` (same shape at `:475` and `:566`). The file is unchanged since #1309. | test | S | swift | — | none |
| #1409 | Undoing a rename inside a collapsed folder leaves a pending focus | mac | Yes. A `.rename` installs `PendingPostMutationFocus` with no expanded-ancestor check (`FileTreeSidebar.swift:6654-6660`); a `.move` expands its ancestors first (`:6663`). | a11y-D + rel (selection and command target jump later) | S | swift, a11y/VO | #1407 | none |
| #1411 | VO+Space on a canvas outline row does nothing | mac | Yes. `CanvasOutlineView.swift:224-226` adds `.isButton` and only named actions. The connection rows (`:358-366`) have the same shape. | a11y-D (VoiceOver's main press is silent; Return and the actions menu work) | S | swift, a11y/VO | #1370 (same view) | none |
| #1413 | A resting pointer selects a palette row on open | mac | Yes. Hover is admitted only by time since the last key navigation (`CommandPaletteView.swift:312-319`). Quick Open already guards this with `shouldAdmitHoverSelection` (`QuickSwitcherView.swift:335`). | a11y-D | S | swift, a11y/VO | C5 | none |
| #1414 | Copy a canvas media card | both (+ core), enhancement | Yes by code: neither host has a copy action, a ⌘C route or a canvas copy event. | enh | L | rust, swift, a11y/VO, windows | Reuses #1368's containment check; #1411 | D14 |
| #1416 | A missing Canvas or Base tab never re-seats | mac | Yes. `missingTabReseatCandidates` takes only `.markdown` tabs (`AppState+CanonicalIdentity.swift:89-93`). | rel | M | swift | #1445 | none |
| #1423 | Tests write the developer's real recent-vaults.json | proc (test) | Yes. 16 `recentsStore: nil` sites in 7 test files. `AppState.swift:9091` and `:9110` fall back to the real `RecentVaultsStore()` and `CommandPaletteRecentsStore()`. | test (also disturbs VoiceOver field passes) | S | swift | Windows twin #1331 | none |
| #1426 | Creating a note reads every files row to heal links | core→mac | Yes. `create_exclusive_binding` (`session.rs:5984`) calls `re_resolve_unresolved_links(&tx)` (`:6085`; `links_db.rs:458`). #1488 removed the per-row cost but kept the whole-table reads. | perf | M (schema change) | rust, swift, windows | After C26 (`links_db.rs`) | none |
| #1427 | `reindex_path_locked` reads every files row | core→mac | Yes (`session.rs:8192-8194`: `indexed_paths_in_path_order`, then `InMemoryVaultIndex`). | perf | S | rust | #1295's resolver | none |
| #1432 | Documented release highlight benchmark doesn't compile | proc (docs) | Yes. `BENCHMARKS.md:264` and `:333` still give the plain `swift test -c release` command. Only the PR 17 row (`:1199-1204`) adds `-Xswiftc -DDEBUG`. | docs, test | S | swift | — | none |
| #1434 | exFAT: no-replace create and rename fail | core→mac | Yes by code (Slate was not run on exFAT). RENAME_EXCL is used at `vault/fs.rs:893` and `vault/fs/pinned_root.rs:156`, and nothing under `vault/` handles ENOTSUP. | rel (must never become a replacing rename) | M | rust, swift | #1470; #1469's weak-ID barrier | D9 |
| #1436 | Base queries run on the main actor | mac | Yes. `BaseDocument.swift:525` and `:547`, `BaseEmbedDocument.swift:991` and `DashboardDocument.swift:405` call `baseExecute` synchronously. | perf (window freezes; the owned token can't interrupt) | M–L | swift, a11y/VO | Bases files with #1399 and #1400; #1445's silent re-run | D10 |
| #1445 | A rescan doesn't re-sync canvas and base tabs, Bases, dashboards, an open Quick Open or the filter | mac | Yes. `resyncFromIndex` (`AppState+Rescan.swift:1268-1296`) covers only the inventory, the tree, Markdown tabs and the leaves. A dirty missing tab is never marked (`:1514`). An open Quick Open keeps the file list it read on appear (`QuickSwitcherView.swift:95-106`). 21c's `retargetTabsForFeedRename` (`AppState.swift` ~23390) is Markdown-only too. | rel (stale views; saves are compare-and-swap, so nothing is overwritten) | L | swift, a11y | #1416. Since 20c, a watcher run can happen while Quick Open is open. | none |
| #1446 | Import and Trash don't refuse while a rescan runs | mac | Yes. Only one direction exists: `rescanBlockedReason` (`AppState+Rescan.swift:349-355`). The structural availability reasons (`AppState.swift:16270-16279`) never read `isRescanning`. | rel (counts can be off; no data at risk) | S–M | swift, a11y | `AppState+Rescan.swift` (with #1445) | none |
| #1457 | zip advisories via MathCAT | proc (deps) | Yes. Three IDs are ignored in `.cargo/audit.toml` (review-by 2026-11-17). The newest MathCAT is 0.7.7-alpha.1. | sec (accepted; the vulnerable path is not reachable) | S once unblocked | rust | The audit gate fails after 2026-11-17 unless the date is renewed | none (watch; renew or upgrade by 2026-11-17) |
| #1460 | SDK 27: reading-view links have no press for VO-Space | mac | Yes. The fix has not started. The tripwire (#1504) is on main (`scripts/check_mac_sdk.py`; `build-mac-app.sh:123`, `:239`), so a shipping build cannot regress unnoticed. | a11y-B (latent: only an SDK 27 build) | L (spike first) | swift, a11y/VO (on both builds) | Any move to an Xcode project or to `--build-system native` | D12 |
| #1462 | A note edited while Slate was quit keeps its old links after relaunch | mac | Likely; not diagnosed. The code fits one cause: `restoreWorkspaceLayout()` runs before the open scan (`AppState.swift:10827-10838`), and the open scan's finish refreshes none of the active note's leaves (`:24896-24909`), unlike the rescan's step (d) (`AppState+Rescan.swift:1293`). | rel (wrong links and backlinks; activation says "unresolved") | S–M | swift, a11y | — | none |
| #1466 | ⌘-click in the editor activates | mac (enhancement) | Yes: there is no `mouseDown` override in the editor (only in the canvas and graph views). | enh (pointer parity) | M | swift, a11y | `NoteEditorView.swift` (with #1503) | D13 |
| #1468 | Trash: verify what arrived (T2) | both (+ core) | Open and decided (owner, 2026-10-17); not started. | data | L | rust, swift, windows | Precondition: the `~/.Trash` read check from a Slate build. After #1479 (real-Trash lane), #1469 (shared identity) and #1518. | none (decided) |
| #1469 | Identity-checked structural undo | core→mac | Yes. Mac batch undo calls `undoBatchMove(opId:)` without identities (`AppState.swift:1719`); single undo is `executeStructuralInverse` (`:19340`). The FFI already has `undo_batch_move_if_identities` (`lib.rs:1046`). | data | L | rust, swift, windows | After #1518. Overlaps #1513's path probes and #1434/#1470 (weak-ID volumes). | none (decided) |
| #1470 | SMB, NFS and FAT32 rename findings | core→mac | Yes (unprobed); same primitives as #1434. | rel | S–M | rust | #1434 | D9 |
| #1474 | After a live root rename, writes go to a recreated old folder | core→mac | Yes on main for history prefs and the op-log (`history_prefs.rs:35`, `:147`; `oplog.rs:804`, `:971`, `:1016` call `create_dir_all`). The save itself already fails at the root pin (#1464). **#1518's head removes these creates** and adds `after_a_live_rename_no_interior_writer_recreates_the_old_path`. | data | — | — | Closes with #1518 on mac and Linux. `SavedButUnindexed` is left to #1513; Windows to 22W and #1475. | D18: close when #1518 merges |
| #1479 | Run the gated real-Trash facts in the mac lane | proc (test) | Yes. Both facts are `#[ignore]` (`trash_handoff_tests.rs:785`, `:1038`), and nothing in `.github/` sets `SLATE_TRASH_PROBE_VOLUME`. | test | S | rust (on the mac lane) | Before #1468 | none |
| #1480 | Windows Recycle Bin fact | win | Not re-checked (Windows test infrastructure) | test | S | windows | — | Out of scope |
| #1481 | `ENV_FAULT_GUARD` poison fails later facts | proc (test, core) | Yes. 42 `ENV_FAULT_GUARD.lock().unwrap()` sites: 30 in tasks, 5 in structural_batch, 4 in structural_identity, 1 each in canvas, canonical_identity and change_feed. | test | S | rust | — | none |
| #1484 | Two sessions share SQLite's page cache | core-only | Yes (no change; the mechanism is not proven). | perf (tests and benches; hosts hold one session) | M | rust | — | Out of scope (core backlog) |
| #1485 | Rename planning offers every path through `PreMoveIndex` | core→mac | Yes. `PreMoveIndex` implements only `all_paths()` (`link_rewrite.rs:86-99`), so it gets the default `offer_candidates` (`link_resolver.rs:67`). | perf (renaming a heavily linked note in a large vault) | S–M | rust | #1429 | none |
| #1486 | Bases and DQL link resolution snapshots the files table and hides failures | core→mac | Yes. `bases/engine.rs:3646-3647` calls `indexed_paths_in_path_order(...).ok()?` and builds an `InMemoryVaultIndex`. | rel + perf | M | rust, swift | #1295's `FoldedNameLookup` | none |
| #1490 | Organization facts drive a sync re-sort that production never runs | proc (test) | Yes. `bindForTesting` binds: 19 in `SidebarOrganizationTreeTests`, 42 in `FileTreeSidebarTests`, 12 in `MutationAnnouncementFocusTests`; none of them async. | test | M | swift | After the sidebar chunk (same test files) | none |
| #1492 | OneDrive Files On-Demand refusals | win | Not re-checked (Windows code) | rel | M | windows | #1491 | Out of scope (Milestone W) |
| #1493 | notify =9.0.0-rc.5 to 9.0.0 | proc (deps) | Yes. `Cargo.toml:133`; the newest on crates.io is 9.0.0-rc.6. | maint | S once 9.0.0 ships | rust, swift, windows | Coordinate with C4 (`watch.rs`); AR-72 | none (watch list) |
| #1497 | A stray "T" phrase after a search result opens | mac | Not verified. A field-pass observation (3 of 7 runs), never investigated; no code evidence either way. | a11y-D (can cut short the editor's read) | S–M (investigation) | swift, a11y/VO | #1377 (same surface) | none |
| #1503 | Undo before the first highlight is lost | mac | Yes. The editor's text view isn't built on TextKit 1, and nothing writes the binding on `NSUndoManagerDidUndoChange` / `DidRedoChange`. The only observer (`AppState.swift:9205-9210`) just ticks the Undo menu. | data (an undo is reverted and the history cleared) | S–M | swift, a11y/VO | `NoteEditorView.swift` (with #1466). #1502's test wait can be relaxed afterwards. | none |
| #1510 | `on_external_change` doc still says "until then a no-op" | proc (docs) | Yes (`crates/slate-uniffi/src/lib.rs:5521-5522`). Both hosts now wire it (#1507, #1511). | docs | S | rust | With C4 | none |
| #1513 | Provider reads still resolve by the opened path | core→mac | Yes. On #1518's branch, only the mutations and the session interior follow the held root; reads and probes don't, and `WATCH_REARM_ENABLED` is `false`. | rel + sec (the rest of #940) | L | rust, swift, windows | After #1518. Unblocks 22b and the re-arm. | none |
| #1516 | Windows folder Modify event starts a silent rescan | win | Not re-checked (seen on Windows only; a core `translate` change is optional) | perf (AR-74 class) | S | rust, windows | C4 touches `watch.rs` | Out of scope |
| #1519 | The link graph never learns another session's link changes | core→mac | Yes. The only core change since the measurement (981904d4) is 20b's watcher classification, and `change_feed.rs` has no graph invalidation. | rel (Connections and the graph views stay stale) | S–M | rust, swift, windows | 21b (Windows graph probe); R-9 (d) | none |
| #1521 | `SidebarVaultPrefsStore` lock created with `openat` and `O_CREAT` | mac | Yes (`Sidebar/SidebarVaultPrefsStore.swift:264-270`). #1518 fixed core's twin by opening first and creating with `O_EXCL`. | rel | S | swift | #1405 (same store) | none |

## (c) Proposed chunks

The plan 43 rules carry over:
- One PR closes its issues and nothing else.
- Chunks are grouped by the code they touch, so chunks in the same wave do not edit the same
  files (the exceptions are listed under the waves).
- Core owns copy.
- Twin facts.
- A VoiceOver pass for anything that changes what VoiceOver speaks or how it navigates.

Chunk ids are working names; the plan document can number them PR 26 onward. The three
design-series PRs keep their plan 43 names.

### Chunk list (in priority order)

| Id | Title | Issues | Files and areas (at b63f6dbb) | Size | Lanes | Wave, after |
|---|---|---|---|---|---|---|
| **C1** | Canvas: untrusted link and file targets | #1367 #1368 #1369 | `Canvas/CanvasContainerView.swift` (link arm, `:169`); `Canvas/AppState+CanvasCreate.swift` (`canvasAddLinkCard` `:342`, `listCanvasMediaPaths` `:172`); `Canvas/AppState+CanvasExtras.swift` (`canvasActivateFileCard` `:491`); one Swift link allowlist shared with `AppState.swift:10437` and `Reading/ReadingLinkRouter.swift:157`; a containment check that resolves symlinks against the resolved vault root; contract 34 CD-35, CD-38, G2-6 | M | swift, a11y/VO (light: refusal lines) | **W1**; nothing before it |
| **C2** | Editor: an undo before the first highlight reaches the buffer | #1503 | `NoteEditorView.swift` (text-view construction on TextKit 1, or writing the binding on `NSUndoManagerDidUndo` / `DidRedoChange`); check `PlainTextEditor.swift` and `CodeBlockView.swift`; `EditorReloadUndoTests` | S–M | swift, a11y/VO | **W1** |
| **C24** | Tests stop writing the developer's files; two flakes; the benchmark command | #1423 #1408 #1432 (Refs #1323's mac fact) | 7 test files, plus a census on `recentsStore: nil` and a missing `commandPaletteRecentsStore`; `BasesTabRoutingTests.swift:475`, `:566`, `:680`; `LeafContextMatrixTests.swift:94`; `BENCHMARKS.md:264`, `:333` | S–M | swift | **W1**, slot 3 first. Test-only, and it stops other sessions' `swift test` runs rewriting the recents file during VoiceOver passes. |
| **C3** | Closing with unsaved edits | #1402 #1403 | `SlateMacApp.swift` (the main window's close gate); `AppState.swift` (`applyPendingNavigation` `:24783`, the Close Vault resolvers `:11844` and `:11873`); facts | M | swift, a11y/VO | **W1**, slot 3 after C24, once D1 is answered |
| **C4 = PR 22c** | Provider reads beneath the held root; turn the re-arm on | #1513 #1510 | `crates/slate-core/src/vault/fs.rs` (`list_dir`, `read_file*`, `stat`, `read_in_vault_with_cap`, `mutation_path_kind`, `preflight_rename`, `preflight_delete`, `require_mutation_parent_access`, `same_entry`); `vault/fs_root.rs`; `session/watch.rs` (`WATCH_REARM_ENABLED` to true, retire `a_legitimate_root_rename_ends_the_watch_while_the_rearm_is_off`); the `lib.rs:5521` doc; mac `VaultRescanTests+Watcher.swift` (the witness stops skipping); `BENCHMARKS.md` (10k open and rescan) | L | rust, swift, windows (bindings build) | **W2**, first thing after #1518 merges |
| **C5** | Palette and Quick Open keyboard | #1379 #1380 #1413 #1382 | `QuickSwitcherView.swift` (mask `:279`, monitor `:347`); `CommandPaletteView.swift` (hint `:171`, hover `:312`, monitor `:669`); `MoveToFolderSheet.swift:524` (same window gate); their tests; with D7, core `a11y.rs`, the corpus and Windows `MainWindow.xaml:2980` | M | swift, a11y/VO (+ rust, windows with D7) | **W2** |
| **C6 = PR 21b** | Windows host of the change feed | #1293 | `apps/slate-windows` (`VaultLifecycleViewModel.Rescan.cs`, `WorkspaceViewModel.Rescan.cs`, `QuickSwitcherViewModel`, FlaUI journey), per `21_change_feed_design.md` §6–§7 | L | windows | **W2**, after #1523 (merged). No mac files. |
| **C7 = PR 22b** | Mac host half of #940 | #940 (Refs) | `AppState.swift`: `currentVaultURL` (`:491`) and the window title from `rootDisplayPath()`, and the drag-in-vault checks (`:19688`, `:19715`); host fact `a_renamed_open_vault_keeps_working_and_can_show_its_new_path`; `VaultOpenIdentityRaceError` stays green | S–M | swift, a11y | **W3**, after C4 |
| **C15** | Core: another session's link changes reach the graph | #1519 | `crates/slate-core/src/session.rs` (the graph index: rebuild or invalidate when a feed window carries another writer's file record), `change_feed.rs`; no host change | S–M | rust, swift, windows | **W3**, in C7's slot after it. Any time; ideally before 21b's graph facts. |
| **C8** | Canvas outline: VoiceOver press and the file-card hint | #1411 #1370 | `Canvas/CanvasOutlineView.swift` (row `:224-226`, connection rows `:358-366`, `activationHint` `:486`); with D7, a core hint chosen by target, plus Windows `CanvasDocumentViewModel.cs:5471` | S–M | swift, a11y/VO (+ rust, windows with D7) | **W3** |
| **C9** | Canvas: failure speech, reload selection, connection order | #1364 #1363 #1365 | `Canvas/AppState+Canvas*.swift` (21 sites), `CanvasDocument.swift` (`installPreparedSnapshot` `:580`, `:766`), `CanvasRendererView.swift` (`:538`, `:561`), `CanvasRendererTests.swift:493`; adopt core's `vaultErrorDetail`, plus a canvas census | M | swift, a11y/VO (spot) | **W3**, after C1 (same files); needs D2, D3, D8 |
| **C11** | Identity-checked structural undo (#1125, decision 2) | #1469 | Core structural undo and redo, op-log records, the `vault/fs*` rename primitives; mac `executeStructuralInverse` (`AppState.swift:19340`) and batch undo (`:1719`, move to `undoBatchMoveIfIdentities`) | L | rust, swift, windows (FFI) | **W4**, after C4 |
| **C16** | Bases: failure speech, pane targeting, unreadable YAML | #1399 #1400 #1398 | `Bases/AppState+Bases.swift`, `DashboardDocument.swift`, `BaseEmbedDocument.swift`, `BaseEmbedView.swift`, `BaseQueryBuilderSheet.swift`, `BaseContainerView.swift`, `BaseDocument.swift`; core `bases/mod.rs` and `a11y.rs` (the copy for broken YAML); the Windows base failed state | M | rust, swift, a11y/VO, windows | **W4**; needs D5, D8 |
| **C17** | Sidebar: selection after mutations, shortcut verbs never silent, race-safe lock | #1407 #1409 #1405 #1521 | `FileTreeSidebar.swift` (overlay selection `:3527` and `:5111-5276`; post-mutation focus `:6640-6690`); `Sidebar/SidebarSectionsView.swift:220-229`; `AppState.swift:7604`; `Sidebar/SidebarVaultPrefsStore.swift:264-270` | M | swift, a11y/VO | **W4** |
| **C10** | Core test infrastructure | #1479 #1481 (Refs #1323's Rust fact) | `.github/workflows/swift-tests.yml`, or a small mac job: scratch APFS image, `--ignored real_trash`, detach always; the 42 `ENV_FAULT_GUARD` sites plus an env-var guard that clears on drop; `reading.rs:4820` counts operations instead of timing them | S–M | rust (on the mac lane) | **W5**, slot 1 first |
| **C13** | No-replace renames on weak volumes | #1434 #1470 | `vault/fs.rs` no-replace publish and rename, `vault/fs/pinned_root.rs`, a probe of the volume's capabilities (`VOL_CAP_INT_RENAME_EXCL`), a refusal worded by core; the capability matrix doc; first probe an exFAT image (`hdiutil -fs ExFAT`) and, if one is available, an SMB share | M | rust, swift | **W5**, slot 1 after C10 and after C11; needs D9 |
| **C19** | Landed-but-unindexed creates | #1394 #1395 | `Canvas/AppState+CanvasActions.swift:210-248` (New Canvas) and `Graph/AppState+Connections.swift:278-285` (ghost create) move to `createReporting`; core `a11y.rs:2170` and `:3419` (`lead_then_sentence`); corpus rows and goldens; the contract 38 trigger ledger | S–M | rust, swift, a11y, windows (corpus) | **W5**, after C9 (same New Canvas lines) |
| **C28** | Open-scan and rescan follow-ups | #1462 #1446 | Open flow (`AppState.swift:10827-10838`; the `.finished` arm of `handleScanProgress`, `:24896`): after the open scan, refresh the leaves of a restored note it changed, reusing the rescan's step (d); `isRescanning` in the structural availability reasons (`AppState.swift:16231-16279`), keyboard and drag-and-drop admission; `AppState+Rescan.swift` | M | swift, a11y | **W5**; diagnose #1462 first (one fact) |
| **C12** | Trash: verify what arrived (T2) | #1468 | Core trash handoff (`resultingItemURL` on macOS, the `IFileOperation` sink on Windows), the identity comparison shared with C11, core copy for a mismatch and for "verified by name and metadata only"; mac Trash outcome paths | L | rust, swift, windows | **W6**, after C10 and C11. Precondition: the `~/.Trash` read check from a Slate build (needs the owner's unlocked Mac). |
| **C14** | R-9 re-sync, complete on mac | #1445 #1416 | `AppState+Rescan.swift` (`resyncFromIndex`); `AppState+CanonicalIdentity.swift` (re-seat canvas and base tabs); `Canvas/CanvasDocument.swift` reload; a silent mode for `refreshVisibleBasesAfterInAppWrite`; Quick Open re-ranks in place (`QuickSwitcherModel.swift`); the sidebar filter re-runs; dirty missing tabs get marked; `retargetTabsForFeedRename` covers canvas and base tabs | L | swift, a11y | **W6**, after C5, C9, C16, C17 and C28 |
| **C22** | Search overlay: selection model and the stray phrase | #1377 #1497 | `SearchOverlay.swift` (rows `:378` and `:549`, monitor `:576` and `:581`; rewrite #1113's facts first); find the "T" with the speech-queue log (runbook gotcha 7) | M | swift, a11y/VO | **W6**; needs D4 |
| **C26** | Core: bounded link resolution, the rest | #1427 #1485 #1486 | `session.rs:8144-8194` (`reindex_path_locked` uses the lookup resolver); `link_rewrite.rs:86-99` (`PreMoveIndex::offer_candidates`); `bases/engine.rs:3640-3650`, plus an error channel on `VaultLookup::resolve_link` | M | rust, swift | **W7**, slot 1 |
| **C27** | Core: the create heal without whole-table reads | #1426 | `session.rs:6085`, the `links_db.rs` heal, a migration that adds an index on a folded `target_raw` | M | rust, swift, windows | **W7**, slot 1 after C26 |
| **C20** | Embeds: live-buffer keys survive; base-card titles from core | #1393 #1388 #1390 | `AppState.swift:13272` (merge instead of replace); `Bases/BaseEmbedDocument.swift:150-165` and `:625`; core `ResolvedEmbed` (new kinds, a view field) and FFI; drop the `queryBlockCardTitles` registration; `a11y_trigger_designations.json:267` and the regenerated contract 38 ledger | M | rust, swift, a11y/VO, windows (bindings, ledger census) | **W7**, after C16 and C14 (Bases files) and C19 (ledger) |
| **C31** | Generated register tables back in step | #1376 #1386 | `docs/plans/34_canvas_contracts.md` and `35_graph_contracts.md` (regenerate (b)–(d) with the generated binding); `scripts/canvas_reconciliation.py` and `scripts/graph_reconciliation.py`; `--check` in the Windows lane, or the censuses compare the line pointers | S | windows | **W7** (docs only; any free slot) |
| **C16b** | Bases: queries off the main actor | #1436 | `BaseDocument.swift`, `BaseEmbedDocument.swift`, `DashboardDocument.swift`, `AppState+Bases.swift` (prepared loaders; publish gated on the document's generation) | M–L | swift, a11y/VO | **W8**, after C16 and C14; needs D10 |
| **C23** | Static accessibility: a11y-check 0.7.0 and doubled names | #1371 #1372 | About 20 views (`MainSplitView`, `NotePropertiesHeader`, `HistoryPanel`, `BaseQueriesPanel`, `CanvasCardEditorSheet`, `TemplatePromptSheet`, `SidebarListPane`, `DashboardEditorSheet`, `CanvasContainerView`, `BaseQueryBuilderSheet`, `BibliographyPanel`, `ConnectionsPanel`, `GraphTableView`, `AddPropertySheet`, `BulkRenameSheet`, …); `a11y-check.yml:45` and the runner image pin | M | swift, a11y/VO (spot) | **W8**, late because it touches many views; needs D11 |
| **C25** | Organization facts on the async seam | #1490 | `SidebarOrganizationTreeTests`, `FileTreeSidebarTests`, `MutationAnnouncementFocusTests`; the sync seam `bindForTesting` (`FileTreeSidebar.swift:1717`) is retired or kept on purpose | M | swift | **W8**, after C17 and C14 |
| **C29** | Reading-view links VoiceOver can press on any SDK | #1460 | `Reading/*`: an app-owned link element whose press calls `ReadingLinkRouter` (the `EditorLinkAccessibilityElement` shape); spike first; VoiceOver on both builds (`swift build --build-system native` gives SDK 27) | L | swift, a11y/VO | **W9**, before any build-system move; needs D12 |
| **C21** | Code preamble verbosity | #1389 | `SettingsView.swift:555-640`, `AppState.swift:8230`, `CodeBlockView.swift:99`; Windows `EditorInteractions.cs` and the settings UI; the `settingsPreambleSamples` census entry | S (remove) / M (build) | swift, a11y/VO, windows (+ rust to build) | **W9**; needs D6 |
| **C30** | Editor ⌘-click | #1466 | `NoteEditorView.swift` (`SlateEditorTextView.mouseDown(with:)`, so that ⌘-drag, ⌘-double-click and multi-click still work); a G22 note in `gap_analysis.md` | M | swift, a11y | **W9**, after C2; needs D13 |

Not chunked:
- #1414 is a feature; it goes to a milestone (D14).
- #480, #998, #1457 and #1493 are on the watch list (blocked upstream).
- #1474 closes with #1518.
- 22W is in Milestone W.

### Waves (at most three agents at a time)

| Wave | Slot 1 | Slot 2 | Slot 3 | Gate |
|---|---|---|---|---|
| W1 (now) | C1 Canvas untrusted targets | C2 Editor undo | C24 test hygiene, then C3 Closing with unsaved edits | #1518 finishes alongside. C3 waits for D1. |
| W2 | C4 = 22c (#1513 + re-arm + #1510) | C5 Palette and Quick Open | C6 = 21b (Windows) | #1518 merged (C4); D7 (C5's hint) |
| W3 | C7 = 22b, then C15 (#1519) | C8 Canvas outline a11y | C9 Canvas speech, selection, order | C4 done (C7); D2, D3, D8 |
| W4 | C11 (#1469) | C16 Bases | C17 Sidebar | D5 |
| W5 | C10, then C13 | C19 Unindexed creates | C28 Open scan and rescan follow-ups | C11 done (C13); D9 |
| W6 | C12 (#1468) | C14 R-9 re-sync | C22 Search overlay | `~/.Trash` check (C12); D4 |
| W7 | C26, then C27 | C20 Embeds and base titles | C31 Register tables | — |
| W8 | C16b Bases off-main | C23 a11y-check 0.7.0 | C25 Async organization facts | D10, D11 |
| W9 | C29 Reading-link press | C21 Code preamble | C30 Editor ⌘-click | D6, D12, D13 |

Parallelism notes:
- **Disjoint files in a wave.** The three chunks in a wave share no files. The exceptions are
  `AppState.swift`, where they touch different functions and a rebase is expected (as in plan
  43), and core's `a11y.rs` and corpus, which take independent variants.
- **Sequenced on purpose:**
  - C1, then C9, then C19 and C14 (canvas action and document files);
  - C16, then C14, then C20, then C16b (Bases files);
  - C28, then C14 (`AppState+Rescan.swift`);
  - C17, then C25 (`FileTreeSidebarTests`);
  - C4, then C11, then C13 (`vault/fs.rs`);
  - C10 and C11, then C12 (the real-Trash lane and a shared identity primitive);
  - C19 and C20 apart (both regenerate contract 38's ledger).
- **S chunks free their slot early.** Pull the next chunk from the same row, or C31.
- **VoiceOver passes go one at a time.** Field passes need the owner's unlocked Mac and the shared
  UI lock. W1 has three VoiceOver checks (C1, C2, C3); plan for them to run one after another.

### Where the remaining design-series PRs fit

- **22a (#1518):** in flight, with the re-arm off. When it merges, close #1474 (D18).
- **22c, the #1513 follow-up (C4):** the first chunk after #1518 merges (W2). It flips
  `WATCH_REARM_ENABLED`, which brings back `a_legitimate_root_rename_rearms_at_the_new_path` and
  mac's `testARenamedVaultFolderRearmsTheWatcherAtItsNewPath` unchanged, and it retires the
  off-switch fact. It also carries #1510's doc fix, because the sentence should describe the
  re-arm too.
- **22b (C7):** W3, right after 22c, because its host fact needs the provider's reads to follow
  the held root. Afterwards #940 keeps only 22W (D15).
- **21b (C6):** W2, in parallel with everything mac (Windows files only). Closing it closes #1293
  by hand. Landing C15 (#1519) before 21b finishes lets its graph facts be real. Neither host
  needs a code change for #1519.
- **22W:** Milestone W. It also carries #1474's Windows writers, with #1475.

## (d) Close candidates

| # | Recommendation | Evidence |
|---|---|---|
| #445 | Close by hand now | Its two child findings, #446 (WCAG 1.3) and #447 (WCAG 4.1), closed 2026-10-17 through #1374 (e0755fd8, "the a11y-inspect findings are closed …"). The summary's last scan is dated 2026-06-24. PR 1's spec expected it to "reconcile itself on the next scan", but no scan has run since. If the scanner runs again it will open a fresh summary. |
| #640 | Close as delivered | `slate query` exists: `crates/slate-cli/src/commands/query.rs` ("N2-3's additive `slate.cli.v1` verb for Bases automation") and `crates/slate-cli/src/main.rs:161` (`Command::Query` with `--base`, `--view`, `--saved`, `--limit`, `--this` and `--format`). It shipped as #771 (6be62f4c, 2026-07-08), three days after the issue was filed. The 2026-10-09 triage comment, which marked it unblocked, did not notice this. An inline query-string form (`slate query <vault> "<dql>"`) would be a new enhancement. |
| #1474 | Close when #1518 merges | #1518 (22a, head aa7fb455) moves the op-log, the history-prefs writer and `.slate/tmp` beneath the held `.slate`. On that branch, `history_prefs.rs` and `oplog.rs` no longer call `create_dir_all` outside tests (on main they do: `history_prefs.rs:35`, `:147`; `oplog.rs:804`, `:971`, `:1016`). It also adds #1474's three facts as `after_a_live_rename_no_interior_writer_recreates_the_old_path`. After a live rename, a save lands in the renamed folder and reports the typed `SavedButUnindexed`, never `Ok` while writing elsewhere, which meets #1474's fact. The rest is tracked: the index `stat` by the opened path is #1513; Windows' writers are 22W, with #1475 for the Windows host store. |
| #1293 | Close by hand when 21b lands (not now) | 21a (#1512) and 21c (#1520) have merged; the issue's last comment says it closes by hand once 21b lands. |
| #940 | Move to Milestone W after 22b (do not close) | Core done in 22a (macOS and Linux); mac host in 22b; Windows body 22W under Milestone W (design §7, Q6). |

None of the other 70 candidates is fixed on main. Two are partly done:
- #1460: the tripwire (#1504) is in place, but the fix has not started.
- #1395: the empty-reason create case was fixed by #1396; the rest is not.

## (e) Out-of-scope moves

**Windows-only.** Leave these out of the mac plan.
- #1051: accent on the raised surface in Windows' `Slate.Light.xaml`. Move to Milestone W
  (W8-2 palette pass).
- #1480: a gated Recycle Bin fact on a scratch volume (Windows test infrastructure). Windows
  backlog or Milestone W.
- #1492: OneDrive Files On-Demand reparse-point refusals. Milestone W.
- #1516: a ReadDirectoryChangesW folder `Modify` starts a silent rescan. Windows backlog. Its
  optional core `translate` change would touch `vault/watch.rs`, so not during C4.
- #1323, once narrowed: after C24 and C10 fix its mac fact and its Rust fact (with Refs), the
  remaining eight items are Windows flakes.

**Core-only, no mac impact today.**
- #640: close (see (d)).
- #1484: the shared page cache costs only test and bench processes that hold two sessions, and
  hosts hold one. Core performance backlog.

**No action.**
- #445: close.
- #642: an l10n tracking umbrella by its own scope; leave it open, unscheduled.

**Watch list.** Blocked upstream; no PR until the upstream release ships.

| # | Waiting on | Checked 2026-10-19 |
|---|---|---|
| #480 | A registry citationberg with quick-xml 0.41 | Still 0.7.0 |
| #998 | A uniffi-bindgen-cs tag for 0.32 | Newest is v0.11.1+v0.31.0 |
| #1457 | MathCAT accepting zip 9 | Newest is 0.7.7-alpha.1. The audit gate fails after the review-by date (2026-11-17), so this needs a renewal or an upgrade decision before then. |
| #1493 | notify 9.0.0 | Newest is rc.6 |

**To a milestone.**
- #1414: canvas copy. A two-host feature (core payloads, a pasteboard writer, menu, registry and
  palette entries, both hosts), not a defect. No open canvas milestone exists, so open a canvas
  follow-up milestone or keep it as an unscheduled enhancement.
- 22W: Milestone W.
- #940: Milestone W after 22b.

## (f) Owner decisions

Listed in the order the waves need them.

| Id | Issue(s) | Question | Recommended answer | Needed by |
|---|---|---|---|---|
| D1 | #1402 | Should closing the main window with unsaved edits go through the Close Vault prompt, or is a vault kept open behind a closed window intended? | **Ask.** With unsaved edits, the window's close button runs the Close Vault prompt: Save All, Discard All or Cancel; Cancel keeps the window. With nothing dirty, closing keeps today's behaviour (the vault stays open; a Dock click brings it back). That matches Windows (`Window_Closing` → `PrepareForApplicationClose`) and U1-2, and removes the "edits behind no window" crash risk. | W1 (C3) |
| D2 | #1363 | When a ready publish (Retry, a move's reopen) loses the selected card, drop the selection (mac's mutation path) or re-seat it (Windows' `PublishReady`, contract A12)? | **Re-seat as Windows does.** Use the resolved intent, else the first row, seated with no announcement; focus lands on the row and the screen reader reads it (`CanvasDocumentViewModel.cs:1830-1839`). It is the recorded cross-host contract for ready publishes, and it gives keyboard and VoiceOver focus a row after a repair. The mutation funnel keeps its "no seat" rule on both hosts. Canvas Delete is undoable if a verb then acts on the re-seated row. | W3 (C9) |
| D3 | #1365 | Should coloured connections paint beneath the cards like plain ones, with label chips above? | **Yes to both.** Coloured connections go beneath the cards (Windows draws every connection beneath them). Chips stay above so they stay readable. Change the two `zPosition` stamps in `rebuildEdges` and the pinning fact together. | W3 (C9) |
| D4 | #1377 | Search overlay: make the rows focusable regardless of Full Keyboard Access (option 1), or adopt the Windows and palette selection model (option 2)? | **Option 2.** Focus stays in the field; ↑/↓ move a selection that speaks `RowSelected`; Return opens the selection; ⌘Return opens it in a new tab (D2 from plan 41 kept). That gives one model across the palette, Quick Open and search, works with any system setting, and keeps Tab order unchanged. #1113's focus-walk facts are rewritten first. | W6 (C22) |
| D5 | #1398 | How should a `.base` whose YAML doesn't parse be repaired? | **Now:** core-owned failed-state copy that says the definition could not be read as YAML, with core's parse message, on both hosts. **Later, as features:** a raw-definition editor, and a confirmed "replace with the builder's view". | W4 (C16) |
| D6 | #1389 | Remove the two code-preamble verbosity modes that do nothing, or build them? | **Remove them on both hosts now.** A stored non-default value falls back to "Preamble only" silently. File "first line" and "all tokens" as an enhancement for core (`code_block_preamble` with a verbosity). The defect is a setting that announces a change VoiceOver never makes. | W9 (C21) |
| D7 | #1382, #1370 | New host copy: core-owned (D9 pattern, both hosts byte-identical in one PR) or a W0.5-3 residue pinned lock-step with Windows? | **Core-owned**, consumed by both hosts in the same PR. Two hints: the palette field hint, with Windows' content and each platform's key names (Return on mac, Enter on Windows), so core renders it per platform; and the file-card hint chosen by target (note, media, or "won't open from the canvas"). | W2 (C5), W3 (C8) |
| D8 | #1364, #1399 | Route failure details through core's `vault_error_detail` (FFI `vaultErrorDetail`) or mac's `humanReadableVaultError`? And how wide should the census be? | **Core's `vault_error_detail`.** Its doc says "mac keeps its own copy until it adopts this", and adopting it retires the duplicate. Add a mac twin of Windows' `VaultErrorDetailCensus` scoped to `Canvas/` in C9, extended to `Bases/` in C16. File one more issue for the rest of the host: about 90 `localizedDescription` uses outside `Canvas/` and `Bases/`, 63 of them in `AppState.swift`; not all of them speak a `VaultError`. | W3 (C9) |
| D9 | #1434, #1470 | On a volume where `RENAME_EXCL` is unsupported (exFAT, SMB, maybe NFS), refuse or use a checked fallback? | **Probe first, then refuse truthfully** with core-rendered copy and a capability check on `VOL_CAP_INT_RENAME_EXCL`. Never fall back to a replacing rename. Decide on a checked fallback (an `O_EXCL` placeholder) only if exFAT vaults turn out to matter to users. | W5 (C13) |
| D10 | #1436 | Accept that Base executes become asynchronous? The Base verbs would stop reading synchronous results such as `applyQuickFilter`'s returned announcement. | **Yes.** Windows' contract and locked decision 05 §4 (principle 2) both call for it. Results publish under the document's generation, and the owned token can then cancel a running query. | W8 (C16b) |
| D11 | #1371 | a11y-check 0.7.0: fix and waive, then bump CI to it, or pin local installs to CI's ref? | **Fix and bump.** Fix the real findings (likely the 8 segmented-picker labels) and record waivers for the false positives (most of the 41 duplicate labels — 16 are "Cancel", others "Done", "OK", "Save" and "Discard" — are buttons in separate sheets declared in one file). Then bump `A11Y_CHECK_REF` and the runner image's `a11y_check_ref` to 0.7.0, so local and CI agree. | W8 (C23) |
| D12 | #1460 | When should the SDK 27 reading-link fix be scheduled? | **After the accessibility-degradation chunks (W9), and before any move to an Xcode project or `--build-system native`.** The tripwire keeps shipping builds safe until then. Start with a spike that proves an app-owned link element's press on both builds. | W9 (C29) |
| D13 | #1466 | Does ⌘-click in the editor activate in the current tab (G22, Windows' Ctrl+click) or open a new tab like the reading view? | **Current tab.** In the editor ⌘ means activate, matching ⌘↩ (`activateAtCaret`) and G22. Record it beside G22 in `gap_analysis.md`. | W9 (C30) |
| D14 | #1414 | Schedule canvas copy now, or move it to a milestone? | **Move it to a feature milestone.** When it is scheduled, adopt the issue's D1–D4 recommendations: file URL plus image data; a separate "Copy Embed Link"; one Copy verb per kind with the payload decided in core; ⌘C routing that leaves text fields alone. | housekeeping |
| D15 | #940 | After 22b, close #940 and file a 22W issue, or keep #940 for 22W? | **Keep #940.** Move it to Milestone W with a comment naming 22W as its only remaining part. | after C7 |
| D16 | #445 | Close the stale a11y-inspect summary by hand, or re-run the scanner so it reconciles itself? | **Close by hand.** Both findings are closed (#1374); a future scan opens a fresh summary. | housekeeping |
| D17 | #1323 | Keep #1323 as a mixed-host list? | **No.** Fix its mac fact (`LeafContextMatrixTests`) in C24 and its Rust fact (`many_unique_citations_stay_subquadratic`, by operation count) in C10, both with Refs. Then retitle it as the Windows flakes list and move it out of the mac plan. | W1, W5 |
| D18 | #1474 | Close it when #1518 merges, or keep it until #1513? | **Close at #1518's merge.** Its fact holds on macOS and Linux (no folder recreated at the old path; never `Ok` while writing elsewhere). Point to #1513 for `SavedButUnindexed` after a rename, and to 22W and #1475 for Windows. | at #1518's merge |
| D19 | plan docs | Where does this wave live? | **In plan 43:** a §11 "Reassessment (2026-10-19)" in `00_program.md`, with this chunk table numbered PR 26 onward and 22c added to PR 22's split. Write a spec per chunk when its wave starts, as plan 43 did. | the reassessment PR |

## Found in passing (not filed)

Recommended for the chunks named, or for new issues. Each goes beyond what the candidates' text
says.

- **The search overlay's key monitor.** #1380 names the palette, Quick Open and Move to Folder
  monitors. The search overlay's (`SearchOverlay.swift:576`) is app-wide too, with no
  `event.window` gate. Gate Quick Open and Move to Folder in C5, and the search overlay in C22.
- **Canvas connection rows.** They have the same named-actions-only shape as #1411
  (`CanvasOutlineView.swift:358-366`), so VO+Space probably does nothing there either. Fix them
  in C8.
- **21c's feed rename.** `retargetTabsForFeedRename` (`AppState.swift` ~23390) moves only
  Markdown tabs, so a canvas or base tab renamed by another session is not moved. It is the same
  class as #1445 and #1416; include it in C14.
- **Failure-detail text across the host.** Core's `vault_error_detail` is documented as mac's
  wording "arm for arm" (`a11y.rs:3393-3400`). Mac still keeps its own copy,
  `humanReadableVaultError` (`AppState.swift:13853`), and many non-canvas, non-Bases failure
  paths speak `localizedDescription`. That needs a host-wide follow-up issue (see D8).
- **#1462's likely cause** (not proven): workspace restore loads the active tab before the open
  scan (`AppState.swift:10827` comes before the `loadFiles` task at `:10838`), and nothing
  refreshes the leaves after the open scan. One fact (edit while quit, relaunch, compare the
  outgoing-links leaf) would confirm it before C28 changes anything.
- **uniffi-bindgen-cs patch release.** The repository pins `v0.11.0+v0.31.0`, while a patch
  release for the same uniffi, `v0.11.1+v0.31.0` (2026-10-09), exists. It is not a 0.32 build,
  so #998 is unchanged; it is only worth a look on the Windows side.
