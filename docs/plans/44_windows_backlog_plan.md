# 44 — Windows and core backlog: delivery plan

Status: draft for the owner's review, 2026-10-19. Not adopted; nothing in
this plan has started. Baseline: `main` at a9f480dd (every file-and-line
reference below was re-verified against 9fb25176, which differs from
a9f480dd only in `AGENTS.md` and `.claude/`). Companion to
`43_mac_backlog_delivery/00_program.md` §11, which plans the mac issues;
this plan takes every open issue that is Windows-specific, or core work
that needs no mac to deliver and verify, and leaves the rest where it is.

The per-issue evidence (every issue read in full, body and comments, and
its cited code checked against the tree) is summarised in §2; the line
numbers are the current ones where the issue's had drifted.

## 1. Scope

**In:** 68 open issues with no feature milestone, in three kinds:

| Kind | Count | Issues |
|---|---|---|
| Windows host (`apps/slate-windows`) | 45 | #1522 #1515 #1508 #1505 #1312 #1475 #1467 #1465 #1422 #1415 #1397 #1389 #1330 #1329 #1266 #1281 #1277 #1261 #1260 #1259 #1314 #1288 #1051 #1331 #1321 #1316 #1315 #1313 #1308 #1306 #1285 #1323 #1301 #1307 #1384 #1381 #1325 #1322 #1320 #1319 #1317 #1311 #1305 #1296 #1112 |
| Core, host-neutral or Windows-cfg (`crates/`) | 17 | #1516 #1519 #1499 #1492 #1480 #1294 #1486 #1485 #1427 #1426 #1484 #1481 #1395 #1510 #1290 #1370 #1526 |
| Two-host, whose Windows or core half stands alone | 6 | #1293 (21b) #1525 #1468 #1376 #1386 #1390 |

**Out, and why:**

- **Mac-only issues**, all in plan 43 §11.4.
- **Core work that needs a Mac to verify:** #1513 (22c; the held root is
  `#[cfg(any(macos, linux))]`, `fs_root.rs:536-540`, and its facts are
  `cfg(unix)`), the macOS probe halves of #1434 and #1470 (`hdiutil`,
  FSKit exFAT, `VOL_CAP_INT_RENAME_EXCL`), and #1469's mac adoption. Their
  Windows twins are in this plan as WB-27 and WB-28.
- **Milestone W's own open issues:** #746–#750 are human-AT residuals of
  delivered waves; #751–#756 are Wave 8 (settings, theming, MSIX, the
  differential harness, perf gates, docs close-out). They are features
  with specs in `18_windows_port/specs/w8_spec.md`, not fixes, and they
  stay the milestone's own wave. §9 says what this plan hands them.
- **Feature milestones** E, PD, XD, X, V, PS, EX and CV: not fixes.
- **Blocked upstream** (watch list, §8): #1493, #480, #998; #1457 is dated.
- **No action:** #642 (l10n umbrella).

**Fixed already, close after a verification fact (§2.1):** #1320, #1261.

## 2. What the reading found

### 2.1 Already fixed on `main`

- **#1320** (attribute queries cost ~320 µs and 157 KB per paragraph).
  #1328 (c247b865) rewrote `Reading/HeadingStyleText.cs`: the walk is
  `ParagraphFrom(TextPointer)` over the FlowDocument's blocks
  (`:288-290`, `:378`); the adaptor survives only in `ClampToQuery` and
  `ParagraphAt` (`:341-368`), and
  `ReadingViewTests.HugeDocumentSyntheticQueriesHaveBoundedCost` (which
  names #1320) pins `allocated < 16 MiB` for the three whole-document
  queries. Left: confirm the attribute-corpus facts, then close with a
  comment pointing at c247b865.
- **#1261** (the board's UIA child list goes stale). #1328's
  `OnStateInstalled` calls `RefreshAutomationChildren()`
  (`Canvas/CanvasRendererView.cs:146-162`, `:181-196`), and
  `CanvasDocumentTests.TheBoardAnnouncesAChangedChildListOnceAndAnUnchangedOneNever`
  (`:5826-5858`) pins it. Left: the issue's missing evidence (a hosted
  fact that `FindAllChildren` before and after layout lists every card
  with no reset), and the journey workaround it mentions, if still there.

### 2.2 Corrections to the issues

- **#1290's "locked 05 :1492"** is now `05_locked_architecture_decisions.md:1556`
  ("All vault operations are paged or streamed … `Paging` is required on
  every list-like operation"). Part of the issue is done: `list_files`
  takes a `CancelToken` (`slate-uniffi/src/lib.rs:909-914`) and the host
  passes it (`VaultLifecycleViewModel.cs:1758-1761`). Still true:
  `QuickSwitcherViewModel.cs:32` keeps `_files` and passes it to
  `SwitcherRankTop` on every keystroke (`:67`, `:474`); `tag_tree`
  (`lib.rs:852`) is tokenless and unpaged.
- **#1112's two architectural arguments have drifted.**
  `SlateTextEditor.GetChildrenCore` now returns `Links.RootChildren()`
  (`:538-539`), so "a child under a Document peer is unprecedented" no
  longer holds (sibling placement is still the safer shape). Row 2 of
  `WorkspaceTabContentTemplate` is now the status `TextBlock`
  (`WorkspaceTemplates.xaml:591`), so the find bar needs its own row. And
  the chord-collision mechanism the issue wanted exists:
  `ChordTableTests.cs:319` has a `SharedCommandChords` id-pair allowlist.
- **#1331 has a sixth store** since filing: `SidebarWriteJournal.cs:301`.
  `QuickSwitcherRankCoordinatorTests.cs:356` is already fixed.
- **#1316's branch is not on `main`.** 21cfc8e lives on
  `origin/claude/laughing-meitner-86n5do` (one commit on old main
  023f658); `CitationAsyncInterleavingTests.cs:411` and `:445` are unheld.
- **#1294's three tests are not all timing-bound.**
  `structural_batch.rs` already uses `LIVENESS = 30 s` (`:28`); the
  remaining short budgets are *negative* waits (`recv_timeout(200 ms)`
  at `:1310`, `100 ms` at `:3299`) that under load spuriously pass, not
  fail, and `successful_batch_finalizes_one_undo_row…` (`:1683-1719`) has
  no timing at all. The fix starts with diagnostics.
- **#1475's identity half is 22W,** not a host change: `root_identity()`
  is `None` off Unix (`session.rs:1109-1115`). Its first bullet
  (`GraphConfigStore.Write` recreates the old path, `:95-99`) is a small
  host fix on its own.
- **#1370's core row has no target.** `CanvasOutlineRow`
  (`session.rs:20839-20854`) carries `kind` only, so the core-owned hint
  RD7 asked for is a new record field, not a new `A11yEvent`.
- **#1288 must not add an `EmbedResolution` variant.** Mac's
  `EmbedView.swift:52-63` and `:240-249` switch over it exhaustively with
  no default; a core free function (`embed_target_kind(path)`) keeps mac
  compiling untouched. The same rule applies to #1526: a sibling FFI
  function, not a changed `code_block_preamble` signature.
- **#1484's mechanism is unproven** (libsqlite3-sys 0.38.1 builds with
  `-DSQLITE_ENABLE_MEMORY_MANAGEMENT`; `db.rs:429` sets only
  `cache_size`). Diagnose before deciding.
- **#1457 has an opening:** MathCAT `main` (793fcb77, 2026-10-18) moved
  to zip 9, though no crates.io release carries it yet. The audit
  exception's review-by date is 2026-11-17.
- **#998 has an opening:** NordSecurity/uniffi-bindgen-cs#176 ("Upgrade
  to uniffi-rs 0.32.0") is open and mergeable (updated 2026-10-14); the
  pin is now one file, `apps/slate-windows/uniffi-bindgen-cs.version`.
- **#1301's five accepted risks are all still actionable** (AR-54
  `MainWindow.Citations.cs:483`; AR-55 `WorkspaceViewModel.Layout.cs:869`,
  `:884-888`; AR-56 create/update `BaseQueryBuilderViewModel.cs:741`,
  `:766` and rename `Bases.cs:1369` still speak the bare name; AR-57
  `Bases.cs:889`, `:895`; AR-58 `FilesSidebarViewModel.cs:2063`).

### 2.3 Where this plan meets plan 43

Plan 43 §11.4 placed some host-neutral core work and some Windows halves
on the mac queue because the reassessment was a mac triage. This plan
takes them, and plan 43 §11.4 gets a pointer per row on adoption
(decision WD1):

| Plan 43 PR | What moves here | Chunk |
|---|---|---|
| 21b | Windows host of the change feed (#1293) | WB-10 |
| 22c | #1510's doc sentence (the code stays mac/Linux) | WB-17 |
| 32 | #1370's core field and Windows consumer (the mac consumer stays in 32) | WB-13 |
| 37 | #1481 (host-neutral test fix; #1479 stays, it needs the mac lane) | WB-18 |
| 39 | #1395's core punctuation (the mac host part, #1394, stays in 39) | WB-8 |
| 44, 45 | #1427 #1485 #1486; #1426 (pure Rust) | WB-18, WB-19 |
| 46 | #1390's text edit (its code twins #1393 #1388 stay in 46) | WB-17 |
| 47 | #1376 #1386 (needs the C# binding, which only a Windows checkout has) | WB-17 |
| 52 | #1389's Windows half (file-disjoint from the mac half) | WB-21 |
| 29 | #1525's Windows half, after the shared design | WB-29 |
| 34, 41 | #1468's core and Windows half; #1469 stays mac-driven, its core identity primitive is shared | WB-30 |
| 38 | the Windows proof of #1434/#1470 (38W) | WB-27 |
| 22W | the Windows body of #940, with #1475's identity half | WB-28 |

## 3. Decisions for the owner (WD1 to WD20)

Each has a recommended answer; only WD1 blocks adoption. The rest are
needed by the wave named.

| Id | Issues | Question | Recommended | Needed by |
|---|---|---|---|---|
| WD1 | plan docs | Does this plan own the rows in §2.3, with plan 43 §11.4 pointing here? | Yes. One queue per host keeps the Windows lanes and the owner's NVDA time in one place. | adoption |
| WD2 | #1322 #1325 | A reload or a property write under an admitted save of the same note: wait, or refuse? And does the "optional" #1325 ship? | Wait (join the save's publication, then act), and yes: both go through `WorkspaceSaveCoordinator.HasAdmittedSaveFor`. | W1 (WB-2) |
| WD3 | #1515 | Are cases 1 (organic publications) and 3 (the selected row removed) wanted now, with #1522? | Yes; the rule since 20b is "a reader's row never gets a new element for a change elsewhere", and these are the last two holes. | W2 (WB-6) |
| WD4 | #1319 | Where do the keys wait during an open's scan? | The welcome view stays, with a live "Opening vault…" status, until `WorkspaceReady`; a failed open returns the keys to the welcome view. Smaller than attaching the shell early, and the four launch lines stay. | W3 (WB-9) |
| WD5 | #1508 | A Bases export saved inside the vault: write it through core, or accept the watcher's second sentence? | Through core (as Import does, `FilesSidebarViewModel.Import.cs:350`), so the write is an echo; the dialog's overwrite confirmation maps to a core save when the file exists. | W4 (WB-12) |
| WD6 | #1505 | Mirror mac's lazy stale-pin prune, or record "no Windows twin"? | Mirror it: prune on a complete level fetch, once per folder per session, skipped while the store is read-only, through the sidebar write journal. | W4 (WB-12) |
| WD7 | #1475 | Fix the `GraphConfigStore` write now and leave identity to 22W? | Yes; and 22W is WB-28 here, not a parked milestone item. | W4 (WB-12), W10 |
| WD8 | #1397 | Contract 25 C4 records the no-views wording as a banner above content; mac now shows a failed-state group with an Edit button. Which row? | Mac's group (heading, core message, repair sentence, Edit), recorded as C4's amendment; Windows and mac then match. | W5 (WB-14) |
| WD9 | #1307 | Should contract 28 I3 (the input seal) extend to windows the shell owns, such as the Reading table? Does UIA pattern invocation fall under it? | Probe both with hosted facts first; extend I3 to shell-owned windows if the probe shows a bypass; record the UIA outcome either way. | W5 (WB-15) |
| WD10 | #1314 | Drop a parked Ctrl+Shift+J when its note closes, even on a quick A→B→A? | Yes, drop at B: deterministic, and "a question nobody asked" never pops. | W8 (WB-23) |
| WD11 | #1317 | Does the first partial reading projection (text and math, diagrams pending) count as "applied" for R-10's held landings? What does a late diagram say? | Yes, it is applied (the reader can read); a late diagram arrives silently and its placeholder is named "Diagram, rendering" until then. | W7 (WB-20) |
| WD12 | #1112 | Chord collision disposition for Ctrl+F; regex and replace? | Disposition 1 via `SharedCommandChords` (scope-keyed: Bases quick filter in a Base, find-in-note in a note); replace yes (mac has it), regex no (mac has none; parity). | W8 (WB-22) |
| WD13 | #1526 | What do "+ first line" and "+ all tokens" speak? | First line: the first non-blank source line verbatim after the preamble. All tokens: every semantic span's text in order, punctuation spans skipped. Both rendered by core. | W7 (WB-21) |
| WD14 | #1480 #1499 #1492 | Which lane can attach a scratch VHD, and may the Recycle Bin and OneDrive probes run on the owner's machines? | Developer-run first on a throwaway Hyper-V VM (the home pool's images already exist); make the facts `#[ignore]`+env-gated; decide a CI lane after the first green run. The OneDrive probe needs a Files On-Demand vault the owner provides. | W8 (WB-24) |
| WD15 | #1492 | If the probe confirms it: relax the identity and undo walks to #1491's `NotLinks` rule for cloud placeholders, and the Trash walk too? | Relax identity and undo (a placeholder is a file, not a link); keep Trash fail-closed unless the probe shows `IFileOperation` is not redirected through the placeholder, and word the refusal truthfully either way. | W8 (WB-24) |
| WD16 | #1468 | Decouple the Windows T2 (Trash verification) from the Mac `~/.Trash` read check, and do the COM in Rust or in the host? | Decouple: the Mac check gates only macOS. COM in Rust (`windows-rs`), beside the existing trash-rs path, so core keeps owning the outcome. | W10 (WB-30) |
| WD17 | #1290 | Keep `switcher_rank_top` and `tag_tree` until mac migrates? | Yes; add the indexed and paged siblings, file a mac follow-up, retire the old entry points when both hosts are off them. | W9 (WB-25) |
| WD18 | #1457 | Renew the zip exception's date, or admit a Git patch to MathCAT `main` now? | Renew once (to 2026-12-17) and watch for a release; if none by 2026-11-30, admit the Git patch under the citationberg admission pattern. | before 2026-11-17 |
| WD19 | #1051 | Retune the accent now, or rule "accent never on raised" and leave it to W8-2? | Leave it to W8-2's palette pass as the issue suggests; this plan only records the pair as a known gap. | — |
| WD20 | #1525 | Who writes the hot-exit design, and does it fix the store schema before either host starts? | One design for both hosts, written as plan 43's PR 29 design (it already has the slot); the Windows half (WB-29) starts only once the store schema and the restore rule are fixed. | W10 |

## 4. Rules (every chunk)

Plan 43 §4 and §11.2 apply unchanged (core owns copy; never silent; one
token per operation; parity by registry and register; twin facts; the
accessibility gates; FFI changes regenerate both bindings; one PR closes
its issues and nothing else; no accidental closing keywords; a surface
that republishes on its own gets a fact that the reader's element and
focus survive, plus a scan right after; a known defect in a PR's own
feature is fixed on its branch first; wait for `main`'s own CI on a
combined head). Added for this plan:

1. **Windows facts run locally before they merge.** This machine has
   the toolchain: regenerate bindings per worktree
   (`$env:CARGO_TARGET_DIR='C:\dev\slate\target-<branch>'; ./apps/slate-windows/generate-bindings.ps1 -Locked`),
   then `dotnet test <tests csproj> --filter "FullyQualifiedName~X"`.
   Every new fact is seen to fail first; mutation runs happen locally
   (the #1517 recipe), not through a throwaway probe PR.
2. **Format gate after every C# edit:**
   `dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <files>`;
   check CR counts before editing a file (mixed line endings flatten).
3. **Core changes run `cargo test -p slate-core` here and `make ci`'s
   Rust gates;** the Windows lane runs them too, the Linux lane proves
   `cfg(unix)` compiles.
4. **Field passes are NVDA first, Narrator for the reading view,**
   with the SendInput plus `nvda.log` recipe from
   `18_windows_port/reports/nvda_agent_matrix_pass_2026-09-22.md`;
   back up `%LOCALAPPDATA%\Slate` first (WB-1 removes the need). A PR
   whose feature changes what the reader hears stays a draft until its
   pass is recorded in the PR body.
5. **Codex adversarial review per `24_red_team_protocol.md`** for every
   chunk of size M or larger, with the merge-delta check before merge
   when `main` moved under it.
6. **A core change that mac consumes unchanged** still touches mac's
   hand-listed mirrors in two cases: a new `A11yEvent` adds one entry to
   `A11yCorpusCensusTests.swift:50`, and a new record field adds to any
   Swift test literal of that record. These are blind text edits that
   the mac CI lane compiles; the PR body names them.
7. **Blocked lanes:** if a `slate-win-*` job queues more than 20
   minutes, cancel and "Re-run failed jobs" (routes to Namespace).

## 5. The chunks

Sizes: S under about 150 changed lines with facts, M 150 to 1,000, L
over 1,000 or design-first. Lanes name what CI runs beyond the always-on
Windows lane; "AT" marks a required field pass.

| Chunk | Title | Issues | Size | Lanes | AT | Decision |
|---|---|---|---|---|---|---|
| WB-0 | Close-outs and housekeeping: verify-and-close #1320 and #1261; renew the zip date; file the mac follow-ups this plan names | #1320 #1261 (#1457) | S | windows, rust | — | WD18 |
| WB-1 | The test suites stop writing the developer's `%LOCALAPPDATA%\Slate`: one `SlateDeviceState.Root` honouring `SLATE_LOCAL_APP_DATA`, six stores and `HostLog` on it, the shell gate on a per-run temp root, the four bare constructions, snapshot guard facts | #1331 | M | windows | — | — |
| WB-2 | Save-chain admission: History restore, template reloads and property writes join an admitted save of the same note instead of replacing the tab under it | #1322 #1325 | S–M | windows | — | WD2 |
| WB-3 | Core-owned task and activation speech: post `TaskToggled` / `TaskToggleFailed` and `NothingToActivateAtCursor`; retire the residue rows and `proposed` designations; regenerate the trigger ledger | #1467 #1465 | S–M | windows (docs census) | spot | — |
| WB-4 | Citation async facts: land 21cfc8e's hold, confirm 50× on Windows, replace the three "has not landed yet" bets with holds (one `FilesCitingViewModel` seam) | #1316 #1285 #1315 | S–M | windows | — | — |
| WB-5 | Keys never leak past a modal surface: the editor popover joins `ModalSurfaceState` so F6 is a no-op; the sheet fence refuses Undo, Redo, Cut, Paste, Delete and the editing commands, and guards the palette, search and Quick Open fields too; contract 40 R-6 note | #1281 #1259 | S–M | windows | — | — |
| WB-6 | Tree publication keeps every reader's element: date groups keyed by (parent, label) and reconciled in place; organic publications take the keep path; the selected row's removal moves selection to the nearest survivor before the publication | #1522 #1515 | M | windows + FlaUI | spot | WD3 |
| WB-7 | Flake sweep: condition waits for the eleven Windows items of #1323 (clipboard seam for the code-copy fact, `WaitForTreeItems` for `OpenCanvasFromTree`, `ReleaseProjection` on the specific projection, graph probes behind a publication gate, budgets to ratios or benchmark-tagged), `CanonicalHighlightCensus` pumps until the refresh lands, `StaThread.TimedOut` so a timed-out body unwinds and the wedge message names the root fact; `structural_batch.rs`'s negative waits become deterministic rendezvous with diagnostics | #1306 #1321 #1294 #1323 (Windows items; then retitle per RD17) | M–L | windows, rust | — | — |
| WB-8 | Core words the failure: `A11yEvent::NoteOpenFailed` replaces `VaultException.Message` in the tab's load failure (census row added); a core `embed_target_kind` makes Ctrl+E title a `.base` embed as a base on both Windows surfaces; the unindexed create and save caveats close their sentence | #1266 #1288 #1395 | S–M | rust, windows, swift (mirror entry) | — | — |
| WB-9 | Open, handoff and landing lifecycle: a second launch or jump-list open parks behind a modal loop and runs when it ends; the welcome view holds the keys with an "Opening vault…" status until `WorkspaceReady`; a held reading landing is carried over to the projection a rescan's reload installs instead of being dropped (the pinning fact flips; contract 40 R-9/R-10 records) | #1296 #1319 #1312 | M | windows | NVDA | WD4 |
| WB-10a | Core: a change-feed window carrying another writer's file record drops the link graph so the hosts' generation probes rebuild it | #1519 | S–M | rust, windows, swift (compile) | — | — |
| WB-10 | 21b, the Windows host of the change feed, per `21_change_feed_design.md` §6–§7 and Q1–Q8: register after the open scan, window with the journal held, the record path (renames by baseline hash, Quick Open `ApplyRecordsAsync`, narrowed dependents), the explicit-Refresh-only counting sentence, up to three passes, acknowledge; the retired fact rewritten first; FlaUI `AnotherSession_ChangesCountedOnRefresh`; close #1293 by hand | #1293 | L | windows + FlaUI | NVDA | — |
| WB-11 | Reading-view list semantics and cost: authored list markers rendered and read; item peers cached, names lazy, positions indexed once, `CensusDiag` gated, measured with a timed UIA probe; Ctrl+A raises `TextSelectionChanged` through one cached text provider | #1422 #1329 #1330 | M | windows + FlaUI | NVDA timing, Narrator | — |
| WB-12 | Watcher noise and the vault-root stores: a folder's own Modify is dropped in `translate`; an in-vault Bases export writes through core; `GraphConfigStore` writes through `AnchoredVaultStore`; stale pins are pruned lazily | #1516 #1508 #1475 (bullet 1) #1505 | M | rust, windows | — | WD5 WD6 WD7 |
| WB-13 | Canvas outline: deleting the focused card seats the next row; the file-card hint is chosen by core from the target (new `CanvasOutlineRow.activation_hint`), consumed on Windows now and by plan 43 PR 32 on mac | #1260 #1370 | S–M | rust, windows, swift (literal) | spot | — |
| WB-14 | Bases repair on Windows: a no-view definition loads as failed with core's wording, stays editable, "Save to View" emits `BaseEdit::AddView`, and the failed state renders mac's group with an Edit button; six twin facts of `BaseContainerRecoveryTests.swift` | #1397 | M | windows | NVDA | WD8 |
| WB-15 | Shell-seal witnesses: `Transition`/`OnTransition` call-graph census, request-creation census, `AddHook` inventory, method-group prompt detection, `MainWindow` input-override detector, `PressPointer` models WPF routing; probes for shell-owned windows and UIA invocation | #1313 #1308 #1307 #1301 (AR-54 to AR-58) | M | windows | — | WD9 |
| WB-16 | Spoken names: unresolved citation rows keyed; the pane line composed after the invalidation; saved-query create, update and rename through `SavedQuerySpokenName`; dashboard names in the census; shortcut slots spoken; the recent-vault distinguisher is the shortest distinguishing ancestor; the rail coalesces its workspace write and defers History and Queries until the arrows settle | #1301 #1311 #1305 | M | windows | NVDA spot | — |
| WB-17 | Docs and registers back in step, checked in CI: the chord table's SD-1 and PD-1 reasons, `chords.json` regenerated, the two Swift citations, contract 38's AR-23 row and designation, contract 34 and 35 reconciliation tables with `--check` in the Windows lane, the `on_external_change` doc sentence | #1384 #1381 #1390 #1376 #1386 #1510 | S | windows, rust (doc) | — | — |
| WB-18 | Core: a poison-proof `ENV_FAULT_GUARD`, then bounded link resolution everywhere (`reindex_path_locked`, `PreMoveIndex::offer_candidates`, Bases and DQL through `FoldedNameLookup` with an error channel) | #1481 #1427 #1485 #1486 | M | rust | — | — |
| WB-19 | Core: the create heal reads only the links its path could resolve (a folded `target_raw` index; schema bump) | #1426 | M | rust, windows, swift (compile) | — | — |
| WB-20 | Progressive reading projection: text, code, math and embeds publish first with named diagram placeholders; diagrams re-project in place keeping caret, focused link and held landings; SVG decoded off the dispatcher | #1317 | M–L | windows + FlaUI | NVDA | WD11 |
| WB-21 | Graph verbosity palette rows registered; the dead preamble modes removed on Windows (RD6); then core's `code_block_preamble_with_verbosity` and the modes back, Windows consuming | #1415 #1389 (Windows half) → #1526 | S, then M | windows; then rust, windows | NVDA per mode | WD13 |
| WB-22 | Find in note (Ctrl+F): `FindInNoteViewModel` over AvalonEdit's search strategies, a landmark bar beside the editor, "N of M" as a pinned residue, wrap, case, whole word, replace, fall-through to vault search, chord row and Edit menu item, `SharedCommandChords` entry, §W-C row, axe and FlaUI journey | #1112 | L | windows + FlaUI | NVDA, Narrator | WD12 |
| WB-23 | A parked citation summary is dropped when its note changes | #1314 | S | windows | — | WD10 |
| WB-24 | Trash on Windows: a gated real Recycle Bin fact on a scratch volume; the dangling-link probe, then trash-as-link or truthful host wording; the OneDrive Files On-Demand probe, then the walks' line | #1480 #1499 #1492 | M | rust (Windows cfg), windows | — | WD14 WD15 |
| WB-25 | Bounded FFI lists: a paged, cancellable tag tree, then an index-backed `switcher_rank_top_indexed`; Windows drops `_files`; the old entry points stay for mac | #1290 | L (S–M, then L) | rust, windows, swift (compile) | — | WD17 |
| WB-26 | Canvas board edge peers per contract 34 D3–D5: `CanvasEdgeAutomationPeer` named by core's traversal phrase, materialized with the edge placements, Invoke total over the end-style table; live UIA-tree facts | #1277 | L (design first) | windows + FlaUI | NVDA | — |
| WB-27 | 38W: the Windows no-replace rename proven on FAT32, exFAT and an SMB share (env-gated facts over a mounted VHD and `\\localhost` share: an occupied name refuses, a free one lands, case-only renames keep identity); capability-matrix rows | #1434 #1470 (Windows arm; Refs) | S | rust (Windows cfg) | — | — |
| WB-28 | 22W: the descriptor-relative session on Windows — `FsRoot` holds the root and `.slate` as handles, `NtCreateFile` with `RootDirectory` for the interior, the SQLite VFS hook's Windows twin, `root_identity()` from `FileIdInfo`, `verified_current_path` by handle, the provider's reads relative; `descriptor_interior.rs` lifted off `cfg(unix)` behind a Windows seam; #1475's identity half | #1475 (identity) #940 (Refs; Milestone W) | L (design first: `22_descriptor_relative_session_design.md` §7 row 22W) | rust, windows | — | WD7 |
| WB-29 | Hot exit, Windows half: unsaved buffers written as you type to the core store outside the vault, closing never asks, restore after `WorkspaceReady` before the layout seats tabs, disk-changed buffers restored dirty into the save-time conflict machinery | #1525 (Windows half) | L | rust (store, if core-owned), windows | NVDA | WD20 |
| WB-30 | Trash verification T2, core and Windows: `IFileOperation` with a progress sink returns the recycled item, re-observed by volume serial and file id against the confirmed inventory; `TrashOutcome` reports a mismatch or "verified by name and metadata only" | #1468 (core + Windows) | L | rust, windows, swift (compile) | NVDA | WD16 |
| WB-31 | Core: two sessions in one process — a bench that reproduces or refutes the shared page cache, then the chosen remedy | #1484 | M (diagnose first) | rust | — | (its own, after the bench) |
| WB-32 | uniffi 0.32 spike: build the C# generator from NordSecurity/uniffi-bindgen-cs#176, bump, regenerate, build and test `apps/slate-windows`; report, ship only on a tag | #998 (Refs) | S | windows (local) | — | — |

Issue counts: 68 in scope; WB-0 closes 2; WB-1 to WB-32 close 59 and
carry 7 as references (#1434 #1470 #940 #998, and the core-only halves
of #1370 #1525 #1468 whose mac halves close them).

## 6. Delivery order

At most three agents at once (`subagent-parallelism`). Chunks in one
wave share no files except `WorkspaceViewModel*.cs` (different partials)
and core's `a11y.rs` and corpus (independent variants); a merge of `main`
is expected inside a wave.

| Wave | Slot 1 | Slot 2 | Slot 3 | Gate |
|---|---|---|---|---|
| W0 | WB-0 | — | — | WD18 |
| W1 | WB-1 | WB-2 | WB-3 | WD2 |
| W2 | WB-4 | WB-5 | WB-6 | WD3 |
| W3 | WB-7 | WB-8 | WB-9 | WD4 |
| W4 | WB-10a, then WB-10 | WB-11 | WB-12 | WD5 WD6 WD7 |
| W5 | WB-10 continues | WB-13, then WB-14 | WB-15 | WD8 WD9 |
| W6 | WB-16 | WB-17 | WB-18 | — |
| W7 | WB-19 | WB-20 | WB-21 | WD11 WD13 |
| W8 | WB-22 | WB-23, then WB-24 | WB-25 (tag tree) | WD10 WD12 WD14 WD15 WD17 |
| W9 | WB-25 (switcher) | WB-26 | WB-27 | — |
| W10 | WB-28 | WB-29 (after the shared design) | WB-30 | WD16 WD20; 22c's `FsRoot` names |
| any | WB-31, WB-32 take a free slot | | | |

Why this order:

- **W1 to W3 are the safety and hygiene floor.** WB-1 stops every
  later `dotnet test` and every field pass rewriting the owner's recents;
  WB-2 closes the last two reload-under-save holes #1309 left; WB-5
  closes two ways keys reach the note behind a modal; WB-7 makes the
  Windows lane trustworthy before the large chunks land on it.
- **W4 is the biggest reader-facing step:** the change feed on Windows
  (another session's changes counted exactly) and the reading view's
  cost under NVDA, which today is five seconds a line at the block cap.
- **The large design-first chunks go last** (W9 and W10) so that the
  small fixes they would otherwise conflict with have landed, and so
  22c's `FsRoot` API names are settled before 22W mirrors them.

Sequenced on purpose, because they share files:

| Shared code | Order |
|---|---|
| `Reading/ReadingSurface.cs`, `ReadingDocumentBuilder.cs` | WB-11, then WB-20, then WB-21's #1526 |
| `WorkspaceViewModel.Rescan.cs`, `EditorLandingSlot.cs`, `QuickSwitcherViewModel.cs` | WB-9, then WB-10, then WB-25 |
| `FilesSidebarViewModel*.cs` | WB-6, then WB-12, then WB-16 |
| `Canvas/CanvasOutlineView.cs`, `CanvasRendererView.cs` | WB-13, then WB-26 |
| `EditorInteractions.cs` | WB-3, then WB-8, then WB-21 |
| `ChordTable.cs`, `chords.json` | WB-17, then WB-21, then WB-22 |
| `vault/fs.rs`, `windows_entry.rs`, trash files | WB-24, then WB-27, then WB-28, then WB-30 |
| `links_db.rs`, `session.rs` link paths | WB-18, then WB-19 |
| Contract 38's generated ledger | WB-3 and WB-17 kept apart |
| Core `a11y.rs` corpus | WB-8 and WB-13 take independent variants; WB-30 after both |

Field passes run one at a time on the owner's desktop with NVDA (and
Narrator for WB-11 and WB-22). W4 has three; plan them back to back.

## 7. Verification lanes and the local loop

- `windows.yml`: four lanes (rust-tests, windows app build and xUnit,
  the two model shards, the FlaUI shell gate on GitHub-hosted
  `windows-latest`) behind the `gate` aggregate; routed home-first by
  `WINDOWS_RUNNER_MODE`. It runs on every PR here; a core change adds
  `rust.yml`; a corpus or binding change adds `swift-tests.yml` for the
  compile.
- Locally, per §4 rule 1: regenerate bindings per worktree, `dotnet
  build apps/slate-windows/SlateWindows.slnx --configuration Release`
  (check `Error(s)=0` before reading test counts: a stale build runs
  stale binaries), `dotnet test --filter`, `dotnet format --include`.
  Shell-gate journeys run locally only with NVDA closed and the
  desktop free (the FlaUI-on-busy-desktop trap).
- Rust: `cargo test -p slate-core` here (Windows-cfg facts included) and
  `make ci` for the format, clippy and license gates.
- Scripts: `python -I scripts/canvas_reconciliation.py --check`,
  `graph_reconciliation.py --check`, `a11y_trigger_ledger.py --write`
  and `generate-parity-matrix.py --check` all need the generated C#
  binding present (WB-17 adds the first two to the Windows lane).
- Probes needing hardware the agent cannot conjure: a scratch VHD and
  Hyper-V VM (WB-24, WB-27), an SMB share (WB-27), a OneDrive Files
  On-Demand vault (WB-24), NVDA and Narrator time (§5's AT column).

## 8. Watch list and dated items

| Issue | Waits for | Check | Action when it lands |
|---|---|---|---|
| #1457 | a MathCAT release on zip 9 (`main` 793fcb77 already is) | **before 2026-11-17** | bump, `cargo audit`, drop the three ignores (WD18 meanwhile) |
| #998 | a uniffi-bindgen-cs tag for uniffi 0.32 (#176 open) | weekly | WB-32's spike makes the bump a day's work |
| #1493 | notify 9.0.0 (rc.6 is newest, 2026-10-17) | weekly | one-line bump; recheck the Windows overflow arm |
| #480 | a citationberg or hayagriva registry release | monthly | drop the Git patch; the runbook's qualification run |

## 9. What this plan hands Milestone W

- 22W (WB-28) is Milestone W's by the design's own row; it is delivered
  here because it is Windows-deliverable now, and #940 moves to the
  milestone after plan 43's 22b as RD15 says.
- #1051 goes to W8-2's palette pass (WD19).
- The human-AT residuals #746–#750 are the owner's sign-offs; the field
  passes in §5 (WB-10, WB-11, WB-14, WB-20, WB-22) each add rows to
  `w_c_matrix.md` the residuals can cite.
- W8-1, W8-3 to W8-6 stay the milestone's own wave; WB-17's `--check`
  steps and WB-7's flake work are what W8-4 and W8-6 would otherwise
  have to clean up first.

## 10. How an implementing agent works a chunk

Plan 43 §8 applies (fresh worktree on a `claude/<chunk-slug>` branch
from `main`; read the "Current state" lines before coding; failing facts
first, twin names where a mac twin exists; registers closed in the same
PR; commit messages `type(scope): one lower-case sentence saying what is
now true`; PR bodies list `Closes #N`, the facts, the registers, the
field-pass phrases and "not delivered: <reason>" for anything left).
Each chunk gets its spec under `44_windows_backlog/specs/` when its wave
starts, as plan 43 did; WB-10's spec is the change-feed design's §6–§7,
WB-28's is the descriptor-relative design's §7 row 22W, and WB-26 and
WB-29 open with a design section reviewed before code.

## 11. Risks

- **Field-pass time is the bottleneck,** not agent time: eight chunks
  need the owner's NVDA or Narrator session. The waves place at most
  three per wave; W4 has three.
- **WB-10 is the largest host chunk** (the design estimates 1,200 to
  1,600 lines; 21a and 20a ran 1.5 to 2× their estimates). It takes a
  full slot for two waves.
- **WB-28 touches the same `FsRoot` surface 22c changes on mac.** Agree
  the helper names (`list_dir_at`, `stat_at`, `open_at`) with 22c before
  either starts, or the second one rebases a core module.
- **Hardware probes can refuse:** the Recycle Bin may reject a dangling
  link (then WB-24 makes the host's wording truthful instead), and
  OneDrive may redirect a placeholder path (then Trash stays fail-closed
  with a better reason).
- **Five chunks regenerate the C# binding from new FFI** (WB-8, WB-13,
  WB-21, WB-25, WB-30): never a dylib with mismatched bindings; the
  Swift lane compiles each.
- **Two chunks edit mac test files blind** (WB-8's corpus mirror entry,
  WB-13's record literal); the mac lane is the only check, so those PRs
  wait for it green before merge.
