# 44 — Windows and core backlog: delivery plan

Status: adopted 2026-10-26 (see the paragraph's end and §3.1); drafted for
the owner's review 2026-10-19; per-chunk specs added
2026-10-20 (§10). Codex adversarial review approved the plan at ce53e22f
(four rounds), then reviewed the plan with its 35 specs through twelve
rounds in all (findings 51, 28, 14, 12, 14, 9, 9, 13 over the last eight).
Every finding through round 11 is fixed. On round 12 the owner chose
(2026-10-21) to fix the findings in the ordinary chunks and to record
those in the three design-first chunks — WB-28, WB-29, WB-30, where each
fix kept opening new surface — as "Open design items" in their specs,
which each chunk's design review closes before code; no further
whole-set rounds. The set is not Codex-approved as a whole, and every
chunk gets its own adversarial review when it is implemented (§4 rule 5). Owner answers so
far are in §3.1. Adopted 2026-10-26 (WD1): every decision still open
was answered as recommended, except that WD20's design and core store
moved here as WB-29a, and WD18 is moot (§3.1); delivery runs wave by
wave from WB-0, each chunk re-checked against `main` when it starts.
Baseline: `main` at a9f480dd (every file-and-line
reference below was re-verified against 9fb25176, which differs from
a9f480dd only in `AGENTS.md` and `.claude/`). Companion to
`43_mac_backlog_delivery/00_program.md` §11, which plans the mac issues;
this plan takes every open issue that is Windows-specific, or core work
that needs no mac to deliver and verify, and leaves the rest where it is.

The per-issue evidence (every issue read in full, body and comments, and
its cited code checked against the tree) is summarised in §2; the line
numbers are the current ones where the issue's had drifted.

## 1. Scope

**In:** 65 open issues with no feature milestone, in three kinds:

| Kind | Count | Issues |
|---|---|---|
| Windows host (`apps/slate-windows`) | 44 | #1522 #1515 #1508 #1505 #1312 #1475 #1467 #1465 #1422 #1415 #1397 #1389 #1330 #1329 #1266 #1281 #1277 #1261 #1260 #1259 #1314 #1288 #1331 #1321 #1316 #1315 #1313 #1308 #1306 #1285 #1323 #1301 #1307 #1384 #1381 #1325 #1322 #1320 #1319 #1317 #1311 #1305 #1296 #1112 |
| Core, host-neutral or Windows-cfg (`crates/`) | 15 | #1516 #1519 #1499 #1492 #1480 #1294 #1486 #1485 #1427 #1426 #1484 #1481 #1510 #1290 #1526 |
| Two-host, whose Windows or core half stands alone | 6 | #1293 (21b) #1525 #1468 #1376 #1386 #1390 |

**Out, and why:**

- **Mac-only issues**, all in plan 43 §11.4.
- **Two-host issues the owner decided land in one PR on both hosts, or
  whose change is heard on mac and so needs a VoiceOver pass:** they stay
  in plan 43, and their Windows edits arrive from there. This plan names
  them so the Windows queue sees them coming and sequences around them:

  | Issue | Plan 43 PR | Decision | Windows files it will touch | Interlock here |
  |---|---|---|---|---|
  | #1370 (file-card hint by target) | 32 | RD7: core-owned hint, both hosts consume in the same PR | `Canvas/CanvasOutlineView.cs:53` (`CanvasPhrase.ActivationHint(row.Kind)`), `CanvasDocumentViewModel.cs:5467-5475`, `CanvasDocumentTests.cs` | WB-13 edits `CanvasOutlineView.cs` for #1260; whichever lands second rebases |
  | #1395 (the unindexed caveats' punctuation) | 39 | changes a sentence both hosts speak; mac VoiceOver pass owed | Windows `CreateOutcomeTests` pin a prefix and keep passing | none |
  | #1382 (the palette field hint) | 30 | RD7: core-owned hint copy | `MainWindow.xaml:2980` (HelpText) | none |
  | #1398 (a `.base` whose YAML does not parse) | 35 | RD5: core-owned failed-state copy on both hosts | the Windows base failed state (`BaseDocumentViewModel.cs`, `BaseSurfaceView.cs`) | WB-14 (#1397) touches the same failed state: WB-14 first, PR 35 rebases, or PR 35 first and WB-14 adopts its copy |
  | #1466 (⌘-click / Ctrl+click setting) | 53 | RD13: a setting honoured by Windows' Ctrl+click too | `EditorInteractions.cs` (pointer origin), settings menu, `ChordTable.cs` | after WB-21 and WB-22 (both edit `ChordTable.cs` and `EditorInteractions.cs`) |
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
- **Blocked upstream** (watch list, §8): #1493, #480, #998. #1457 closed
  on 2026-10-22, when #1537 moved MathCAT to 0.7.7, which is on zip 9.
- **Milestone W's W8-2 palette pass:** #1051 (accent on raised at Lc 74.46;
  no surface uses the pair today), per WD19.
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
  fact whose first read is held before layout — the surface in no
  window, so the engine's view has no size — and whose second read,
  after the surface is hosted and with no reset, lists every card of a
  compact fixture the window holds), and the journey workaround it
  mentions, if still there.

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
  `ChordTableTests.cs:319` has a `SharedCommandChords` allowlist keyed by every command id sharing a chord (Ctrl+F already holds two: the Bases quick filter and the canvas filter).
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
  RD7 asked for is a new record field, not a new `A11yEvent`; it adds a
  field to the Swift record literal in
  `PreparedFailureRecoveryTests.swift:268`. Recorded here for plan 43's
  PR 32, which keeps the issue (§1).
- **#1526's "all tokens" cannot mean semantic spans.** `CodeBlock` has
  separate `tokens: Vec<SyntaxToken>` (byte ranges with a `TokenKind`)
  and `semantic_spans` (`code.rs:84-90`), and `capture_semantic`
  records declaration names only (`:387-412`), so an expression or a
  JSON block can have no semantic span at all. A verbosity mode is
  defined over the syntax-token stream or the whole source (WD13).
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
  exception's review-by date is 2026-11-17. *Since:* MathCAT 0.7.7
  carries zip 9, #1537 (7599b3b7) moved to it, `.cargo/audit.toml` has
  no active exception, and #1457 closed on 2026-10-22; WD18 is moot.
- **#1316's fix is superseded on `main`.** #1328 (c247b865) rewrote both
  deferred-summary facts into `StaThread.RunPumped` with a serialized
  dispatcher context (`CitationAsyncInterleavingTests.cs:413-416`,
  `:447-450`; `PumpUntilDrained` `:431`, `:460`), a different fix for
  the same race. 21cfc8e (one commit on 023f658, which makes the facts
  `async Task`) no longer cherry-picks; WB-4 ports its hold into the
  pumped bodies and still runs the 50× loop.
- **Three #1323 items are already done:** `ReadingFocusTests.ReleaseProjection`
  waits for the specific projection since #1304 (`:4420-4430`,
  `LetTheHeldFetchRunOut(HeldFetchEnd.Published)`); the code-copy fact's
  clipboard seam exists (`ReadingContentViewModel.cs:397`
  `ClipboardForTests`); `RecomputeScalesLinearlyInRowCount` is already a
  ratio (`:162-166`) and needs only the benchmark tag. WB-7 records them.
- **Core already has a load-failure event.** `A11yEvent::FileReopenFailed
  { filename, detail }` (`a11y.rs:677`, "Could not reopen {filename}:
  {detail}") is posted by the Windows rescan reopen
  (`WorkspaceViewModel.Layout.cs:900-903`). Reusing it for #1266 would
  remove WB-8's corpus change and mac mirror edit (WD23); the Swift
  compile lane stays, because WB-8's `embed_target_kind` is a new FFI
  export under either answer.
- **`FsRoot` has no `list_dir_at`.** Its system layer is `fs_root.rs`
  `mod sys`: `open_at :809`, `stat_at :930`, `read_dir_names :960`,
  `rename_at :898`, `unlink_at :920`, `ensure_dir_at :878`, `identity
  :803`, behind `trait InteriorDir` (`:142-169`) and `CacheDir` (`:285`).
  Those, plus whatever 22c adds for #1513's provider reads, are the names
  WB-28 mirrors (§11).
- **Windows has no Edit menu.** The top-level menus are File, Workspace,
  Base, Editor, Canvas and Graph (`MainWindow.xaml:179-677`); WB-22's
  find item lands in the Editor menu.
- **A change-feed `Directory` rename record moves every note under it,**
  so WB-10a's trigger is any foreign record, not only a file record.
- **The sidebar write journal records renames only**
  (`SidebarJournalEntry.Renames`, `SidebarWriteJournal.cs:23-28`), so the
  stale-pin prune (WD6) goes through the settings store's locked `Update`
  on the dispatcher, as `PersistPins` does, not the journal — mac does not
  journal its prune either.
- **The next cache migration is 041** (`db.rs:75`), and any migration at
  or past `FEED_MIGRATION_VERSION` (40) clears every session's change-feed
  cursor in the same transaction (`db.rs:489-491`) and fences older
  sessions with `SchemaVersionSkew` (`:60-64`); WB-19 expects both.
- **The Windows identity primitives exist** (`windows_entry::identity`
  `:612-631`, `FsVaultProvider::mutation_identity` `fs.rs:1907`,
  `capture_structural_identity` `session.rs:16564`, FFI `lib.rs:974`);
  WB-30's prerequisite on PR 34 is for the cross-platform identity *form*
  `TrashOutcome` carries, not for Windows' capture.
- **`slate-core` has no COM crate** (`windows-sys` 0.61 only; trash-rs
  brings its own `windows` 0.56): WB-30's Rust `IFileOperation` sink adds
  the `windows` crate's 0.62 line (features `Win32_UI_Shell` and
  `Win32_System_Com`; `implement` is a macro `windows-core` 0.62.2
  re-exports, not a feature) through the licence and lock gates.
- **No PR 29 design document exists yet** in `43_mac_backlog_delivery/`
  (only the row at `00_program.md:437` and RD1 at `:557`); WB-29's spec
  lists what that design must fix before WB-29 starts. *Since:* the
  design and its core store are WB-29a (§3.1, 2026-10-26), so WB-29's
  list is WB-29a's design brief.
- **#998 has an opening:** NordSecurity/uniffi-bindgen-cs#176 ("Upgrade
  to uniffi-rs 0.32.0") is open and mergeable (updated 2026-10-14); the
  pin is now one file, `apps/slate-windows/uniffi-bindgen-cs.version`,
  read in two places (`windows.yml:489-513`, `:716-740` and
  `.github/actions/windows-native-build/action.yml:45-58`) through a
  regex that rejects a `--rev` fork pin; `swift-tests.yml` no longer uses
  `cache: rust`, so the askama cache-path blocker's mac exposure is
  re-checked by WB-32, not assumed.
- **#1481 has 42 guard sites now**, not 41 (`change_feed.rs:1336` was
  added since), and fifteen `SLATE_TEST_*` names are set under the guard,
  not only `SLATE_TEST_FAULT_*`.
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
| 31 | #1519 (core only; no host change). PR 31's slot is retired on adoption | WB-10a |
| 22c | #1510's doc sentence (the code stays mac/Linux) | WB-17 |
| 37 | #1481 (host-neutral test fix; #1479 stays, it needs the mac lane) | WB-18 |
| 44, 45 | #1427 #1485 #1486; #1426 (pure Rust) | WB-18, WB-19 |
| 46 | #1390's text edit (its code twins #1393 #1388 stay in 46) | WB-17 |
| 47 | #1376 #1386 (needs the C# binding, which only a Windows checkout has) | WB-17 |
| 52 | #1389's Windows half (file-disjoint from the mac half; the issue closes with 52) | WB-21 |
| 29 | the shared hot-exit design and the core store (the owner's call, 2026-10-26, §3.1), then #1525's Windows half | WB-29a, then WB-29 |
| 41 | #1468's shared outcome contract (`TrashOutcome`: confirmed, mismatch, verified by name and metadata only; its core copy) and the Windows back end. PR 41 keeps the macOS Rust half — `trash_handoff.rs:378-390` discards `resultingItemURL` today, so macOS destination capture, wiring it to WB-30's shared verifier (flipping `reports_destination`), and the macOS facts stay with the owner of the `~/.Trash` check — and the mac host. Order: WB-30's contract first, PR 41 consumes it, or the reverse with WB-30 consuming. PR 34 (#1469) stays mac-driven; its shared core identity primitive is WB-30's prerequisite | WB-30 |
| 38 | the Windows proof of #1434/#1470 (38W) | WB-27 |
| 22W | the Windows body of #940, with #1475's identity half | WB-28 |

Not moved, on purpose: #1370 (PR 32), #1395 (PR 39), #1382 (PR 30),
#1398 (PR 35), #1466 (PR 53) — the §1 table says why and what each will
touch on Windows.

## 3. Decisions for the owner (WD1 to WD24)

Each has a recommended answer; only WD1 blocks adoption. The rest are
needed by the wave named.

| Id | Issues | Question | Recommended | Needed by |
|---|---|---|---|---|
| WD1 | plan docs | Does this plan own the rows in §2.3, with plan 43 §11.4 pointing here? | Yes. One queue per host keeps the Windows lanes and the owner's NVDA time in one place. | adoption |
| WD2 | #1322 #1325 | A reload or a property write under an admitted save of the same note: wait, or refuse? And does the "optional" #1325 ship? | Wait (join the save's publication through `WorkspaceSaveCoordinator.Admit`, then act), and yes; an admitted operation's own reload is never gated by `HasAdmittedSaveFor`, which its own ticket trips. | W1 (WB-2) |
| WD3 | #1515 | Are cases 1 (organic publications) and 3 (the selected row removed) wanted now, with #1522? | Yes; the rule since 20b is "a reader's row never gets a new element for a change elsewhere", and these are the last two holes. | W2 (WB-6) |
| WD4 | #1319 | Where do the keys wait during an open's scan? | The welcome view stays, with a live "Opening vault…" status, until `WorkspaceReady`; a failed open returns the keys to the welcome view. `IsOpening` spans the whole open from admission (before `IsBusy` and the `OpenFilesystem` await at `VaultLifecycleViewModel.cs:475-483`), and the welcome's Open and recents buttons stay enabled meanwhile through command-level admission (the two open commands are admitted while executing, and a press during the open restates the opening status before any folder picker, probe or box) — the one-focus-change rule holds only if nothing disables or regenerates under the keys, so the recents list is published only once the welcome has collapsed or the open has failed. Smaller than attaching the shell early, and the four launch lines stay. | W3 (WB-9) |
| WD5 | #1508 | A Bases export saved inside the vault: write it through core, or accept the watcher's second sentence? | Through core (as Import does, `FilesSidebarViewModel.Import.cs:350`), so the write is an echo; the overwrite confirmation moves from the save dialog into the app: the destination is observed on disk first (`CanonicalPath`, then `ReadNoteParts(relative).ContentHash`), the app asks, and the replacement goes through core under that unchanged hash, core re-reading the disk bytes inside its writer lock (`save_text` if it accepts the path — a core fact pins it first — else a new `replace_file_bytes(path, bytes, expected_content_hash)` with `save_text`'s disk-hash check on the staged publish path), refusing on a mismatch and leaving the destination intact on a failed publish; an absent destination is created exclusively and never replaced unconfirmed; never delete-then-create. | W4 (WB-12) |
| WD6 | #1505 | Mirror mac's lazy stale-pin prune, or record "no Windows twin"? | Mirror it: prune on a complete level fetch, once per folder per session, skipped while the store is read-only, proved absent on a worker, then committed on the dispatcher in one turn with its memory publication — the pin generation checked, the settings store's locked `Update` re-checking absence, the removed pins leaving memory — so the file and memory agree after every prune (the write journal records renames only, §2.2; mac does not journal its prune either). | W4 (WB-12) |
| WD7 | #1475 | Fix the `GraphConfigStore` write now and leave identity to 22W? And the optional "Open Recent says what it found when the folder holds only `.slate`"? | Yes; and 22W is WB-28 here, not a parked milestone item. The Open Recent wording states only what the folder observably holds — "This folder holds only Slate's data (`.slate`); the vault's files are not here" — and never claims the folder was renamed or replaced: `RecentVaultsStore.cs:11-14` persists only path, display name and last-opened time, so no earlier identity exists to compare against, and this plan does not add one (persisted recent-vault identities would be a new feature, not a fix). WB-12 ships that wording, with its NVDA spot check. | W4 (WB-12) |
| WD8 | #1397 | Contract 25 C4 records the no-views wording as a banner above content; mac now shows a failed-state group with an Edit button. Which row? | Mac's group (heading, core message, repair sentence, Edit), recorded as C4's amendment; Windows and mac then match. | W5 (WB-14) |
| WD9 | #1307 | Should contract 28 I3 (the input seal) extend to windows the shell owns, such as the Reading table? Does UIA pattern invocation (Invoke, SelectionItem.Select, ExpandCollapse, Value.SetValue) fall under it? | Probe both with hosted facts first. A demonstrated bypass through a shell-owned window extends I3 to it: every route the shell's own window admits (key down and release, text, pointer press and release, wheel, the rebuilt double-click, mnemonic) through one shared admission, for modeless owned windows only — a modal dialog is the prompt the seal waits on. Contract 40's frozen AR-48 records UIA pattern calls as outside the seal, so a UIA guard is an amendment appended to AR-48, never a rewrite of it. A demonstrated UIA-pattern bypass is guarded: the pattern providers of sealed surfaces refuse while the seal holds, with hosted facts, unless the owner records an explicit I3 exclusion in contract 28 — that portion of #1307 does not close on a recorded probe alone. | W5 (WB-15) |
| WD10 | #1314 | Drop a parked Ctrl+Shift+J when its note closes, even on a quick A→B→A? | Yes, drop at B: deterministic, and "a question nobody asked" never pops. | W8 (WB-23) |
| WD11 | #1317 | Does the first partial reading projection (text and math, diagrams pending) count as "applied" for R-10's held landings? What does a late diagram say? | Yes, it is applied (the reader can read); a late diagram arrives silently and its placeholder is named "Diagram, rendering" until then. | W7 (WB-20) |
| WD12 | #1112 | Chord collision disposition for Ctrl+F; regex and replace; the mac extras the issue could not check (⌘G / ⇧⌘G find next and previous, find-in-selection)? | Disposition 1 via one `SharedCommandChords` entry for the three Ctrl+F commands (scope-keyed: Bases quick filter in a Base, the canvas filter on a canvas, find-in-note in a note); replace yes, in the editor only (mac has it; the reading view has none), regex no (mac has none; parity). Ctrl+G / Ctrl+Shift+G as next and previous from the bar and from the editor or reading text, as mac's ⌘G steps from the text (AppKit's defaults, readable from `NoteEditorView.swift`'s `usesFindBar`) — answered 2026-10-21 (§3.1 \"WD12 (in part)\"), delivered by two window `KeyBinding`s on routed commands that pass the key through unless the focused note's bar is open, with an accessible results list if WB-22's step-0 spike finds a screen-reader mode that keeps the keys from the text; defer find-in-selection, recorded in the parity matrix as "unverified on mac; not shipped", so no Mac probe gates the chunk. | W8 (WB-22) |
| WD13 | #1526 | What do "+ first line" and "+ all tokens" speak? | Both over `CodeBlock.source` and `tokens` (the `SyntaxToken` stream, sorted by `start_byte`), never `semantic_spans` (§2.2). First line: the source's first line verbatim (blank or not — the setting's original label says "first line"; a leading blank line is spoken as "blank line"). All tokens: the whole source in source-byte order, each token spoken as its text (punctuation as its characters, comments and strings verbatim), with every source byte no token covers (whitespace, uncovered ranges) spoken as plain text in place, so nothing is omitted or reordered; an unknown language, which yields one `Other` token over the whole source, reads the source verbatim. Core renders both; the spec carries examples with no declarations, with comments, with uncovered ranges and with an unknown language. | W8 (WB-21b) |
| WD14 | #1480 #1499 #1492 | Which lane runs the scratch-volume Recycle Bin fact, and may the Recycle Bin and OneDrive probes run on the owner's machines? | Developer-run first on a throwaway Hyper-V VM from the home pool's image, with a VHD attached and `SLATE_TRASH_PROBE_VOLUME` set, `cargo test -p slate-core -- --ignored real_trash`; the guard refuses the system drive and any volume but the probe's. WB-24 is not complete until a lane runs that invocation: the recommended lane is a `home-only` job on the self-hosted pool (its VMs are throwaway, so the VHD is provisioned in the job and discarded with it); if the pool cannot attach a VHD, the fact stays owner-run and WB-30 records each run's result in its PR body before it merges. The OneDrive probe needs a Files On-Demand vault the owner provides. | W8 (WB-24) |
| WD15 | #1492 | If the probe confirms it: relax the identity and undo walks to #1491's `NotLinks` rule for cloud placeholders, and the Trash walk too? | Relax identity and undo (a placeholder is a file, not a link); keep Trash fail-closed unless the probe shows `IFileOperation` is not redirected through the placeholder, and word the refusal truthfully either way. | W8 (WB-24) |
| WD16 | #1468 | Decouple the Windows T2 (Trash verification) from the Mac `~/.Trash` read check, and do the COM in Rust or in the host? | Decouple: the Mac check gates only macOS. COM in Rust (the `windows` crate's 0.62 line, whose `implement` is a macro `windows-core` 0.62.2 already in the lock re-exports; a sink-compilation spike is the first commit), beside the existing trash-rs path, so core keeps owning the outcome. One outcome model, `TrashOutcome { Confirmed, VerifiedByNameAndMetadataOnly, Mismatch, Unverifiable, DeletedOutright, UnlinkedForGood }`, and a parallel `BatchTrashReport.outcomes` field originate in WB-24 if its refused-kind branch ships first, else in WB-30; `trashed` keeps its shape so mac's production consumers (`AppState.swift:1209-1286`, `:1357`, `:1386`, `:1412`, `:20717-20747`, `:21476`) and the fourteen Swift `BatchTrashReport` test literals compile unchanged until mac migrates. A batch whose back end reports no destination keeps main's batch state, so mac's sentences do not change before PR 41. | W11 (WB-30) |
| WD17 | #1290 | Keep `switcher_rank_top` and `tag_tree` until mac migrates? | Yes; add the indexed and paged siblings, file a mac follow-up, retire the old entry points when both hosts are off them. | W9 (WB-25) |
| WD18 | #1457 | Renew the zip exception's date, or admit a Git patch to MathCAT `main` now? | Renew once (to 2026-12-17) and watch for a release; if none by 2026-11-30, admit the Git patch under the citationberg admission pattern. | before 2026-11-17 |
| WD19 | #1051 | Retune the accent now, or rule "accent never on raised" and leave it to W8-2? | Leave it to W8-2's palette pass as the issue suggests; this plan only records the pair as a known gap. | — |
| WD20 | #1525 | Who writes the hot-exit design, and does it fix the store schema before either host starts? | One design for both hosts, written as plan 43's PR 29 design (it already has the slot). The design fixes the store schema, the restore rule and the open-sequence ordering (recovery state read after the open scan and before the `WorkspaceViewModel` is constructed at `VaultLifecycleViewModel.cs:1184`, then injected into `RestoreNode`'s tab construction, because `Restore` (`WorkspaceViewModel.cs:2309`, `Persistence.cs:53-108`) builds new tab instances; `WorkspaceReady` raised only after restored buffers are installed, one launch landing) and the close rule: closing never asks only once the latest buffer generations are durably flushed — a failed, timed-out or over-cap flush falls back to today's prompt with the reason — and the discard rule: every Discard route (Close Vault's Discard All, the fallback prompt's Discard All on application close and vault switch, and the per-buffer discard on a clean transition or on a dirty tab's own Discard — closing it, closing its pane, or replacing its document) fences pending recovery writes before the durable discard, the store's discard superseding every write issued before it, including one that completes later. And the weak-root rule: a vault whose root identity is weak (WB-28's `persistent: false`, FAT32 and exFAT) keys nothing durable by it — the design names either no hot exit on such a vault (the store refuses the write with a typed refusal and application close and vault switch fall back to today's prompt with core's reason) or a path key with a named restore-admission check, so that no buffer is ever restored into a different folder and a rename made with Slate closed never silently loses one. And the lifetime rule: the store's calls are on a handle opened once per process that never borrows the vault session, is safe to call concurrently and treats a call cut short by process exit as a crash, so a recovery write can outlive the workspace and the session and no teardown joins one. And the peer rule: two tabs on one note can hold separate dirty buffers, so the key within a vault is per tab, or per path with a merge rule under which no tab's discard removes another tab's newer generation. Plan 43's PR 29 series implements the core store, its Windows binding and its facts; WB-29 starts only after that implementation is on `main` with green CI, never against the design alone. | W10 |
| WD21 | #1484 | After WB-31's bench: unset SQLite's `ENABLE_MEMORY_MANAGEMENT`, size `cache_size` for a shared LRU, or release an idle session's cache? | Conditional: if the bench proves the shared LRU (by per-connection `SQLITE_DBSTATUS_CACHE_USED` / `CACHE_MISS` deltas and `PAGECACHE_OVERFLOW`, not `PAGECACHE_USED`, which counts a `SQLITE_CONFIG_PAGECACHE` buffer this repo never configures), release the idle session's cache (`sqlite3_db_release_memory`) from a new idle scheduler — no housekeeping tick exists today (`watch.rs:14-22` is an event-driven coalescer that exists only while watching) — keeping the bundled build and the one-session case untouched. If the bench refutes the LRU mechanism, the issue stays open with the measurements and the diagnosis continues, because the observed two-session slowdown is the defect, not the hypothesis. The reported numbers are macOS (M5 Max, 173 ms against 96 ms): a Windows bench that does not reproduce them closes nothing. WB-31 is complete when its bench and, if the mechanism is proven, its remedy are merged; #1484 closes only after the remedy is measured on a Mac (a plan 43 follow-up row WB-0 files, or the owner's own run), or the owner records that the macOS observation is withdrawn. Implementation waits for this answer. | after WB-31's bench |
| WD22 | #1475 #940 | Windows root identity: `VaultRootIdentity` is `{device: u64, inode: u64}` (`session.rs:1092-1095`) but NTFS `FileIdInfo` is a 128-bit file id that `windows_entry.rs` keeps whole. Widen the shared record, or hash? | Widen: `VaultRootIdentity` gains a lossless form (volume serial plus the 128-bit id as two `u64`s, with the `persistent` flag WD24's amendment adds), the FFI record changes on both bindings, and a fact uses an id with high bits set. `FileRecentsStore` (`FileRecentsStore.cs:27-31`) switches its file name from a path hash to an identity hash the moment `RootIdentity` becomes non-null, so WB-28's PR 1 keeps the path key until PR 2's migration, and `MigrateLegacy` reads only the old in-vault file: WB-28 migrates the path-keyed device-local recents to the identity key on first open for a persistent identity (a weak FAT32/exFAT identity keeps the path key), or the owner records a one-time reset. On Windows a root swap after the hold is impossible by construction (no `FILE_SHARE_DELETE`), so 22W's facts are handle-pinning facts plus interior-resolution facts — a junction spelling retargeted under the live session, which no interior writer may follow, and the VFS hook's own evidence — not the Unix A→B→A or live-rename facts. | W10 (WB-28) |
| WD23 | #1266 | A tab's load failure: reuse core's existing `FileReopenFailed { filename, detail }` ("Could not reopen {filename}: {detail}"), or add `NoteOpenFailed` ("Could not open {name}: {detail}")? | Reuse `FileReopenFailed`: the sentence is truthful for a first open too, and it removes the corpus change and the blind mac mirror edit from WB-8 (the Swift compile lane stays under either answer: `embed_target_kind` is a new FFI export). Add `NoteOpenFailed` only if the owner wants "open" and "reopen" to differ. | W3 (WB-8) |

### 3.1 Owner answers

Recorded as they arrive; an answer here supersedes the recommendation in
the table above. Since 2026-10-26 no row remains pending.

| Id | Date | Answer | What it changes |
|---|---|---|---|
| WD2 | 2026-10-21 | **Wait, and #1325 ships.** An admitted History restore, template reload or property write runs through `WorkspaceSaveCoordinator.Admit` behind the note's save. A property write additionally waits until the owning header has re-read the saved bytes (the header refresh `SaveActive` starts at `WorkspaceViewModel.cs:3233` runs on a worker and publishes after the save's ticket completes), then takes the edited row's CAS token from that fresh header. The header stays the single hash authority (contract 4). | WB-2's property-write design (codex round 7's high) |
| WD24 | 2026-10-21 | **A Windows vault whose root file id cannot be read does not open.** WB-28's refusal stands (`OpenVaultAsync` refuses a null root identity on Windows, with a sentence saying why). WB-30's id-free verification (`NameAndMetadataOnly`) is chosen by file system — FAT, FAT32, exFAT, or a share whose server reports another file system — never by a failed root-id read — amended 2026-10-21: see the next row. Rejected: admitting an id-less root by handle and final path with path-keyed stores. | WB-28's admission sentence; WB-30's identity-mode rule and its "ids unavailable end to end" fact (codex round 7's high) |
| WD24 (amended) | 2026-10-21 | **FAT32 and exFAT vaults keep opening.** Codex round 8 found that FAT32 and exFAT generally do not answer `GetFileInformationByHandleEx(FileIdInfo)` (microsoft/STL#6187), and core's Windows identity read (`windows_entry.rs:612-631`) has no fallback, so the WD24 answer above would have stopped every vault on a USB stick or SD card from opening. When `FileIdInfo` is unsupported, the identity read falls back to `GetFileInformationByHandle` — volume serial plus the 64-bit file index, the read the vault watcher already uses for its root (`vault/watch.rs:279-307`, the call at `:298`) — and the identity is flagged weak (a FAT index changes when its entry is renamed or moved). WB-30 verifies by name and metadata on those volumes. The refusal applies only when neither read works. WB-28 opens with a VHD probe of both reads on FAT32 and exFAT. **Codex round 11 refinement, confirmed by the owner 2026-10-21:** "unsupported" is decided by the volume's file system — the fallback is taken only when `GetVolumeInformationByHandleW` names FAT, FAT32 or exFAT; a `FileIdInfo` failure on NTFS, ReFS or any other file system refuses the open (one attempt, no retry), because ReFS's 64-bit index is not unique; and a weak identity authorizes no store access — WB-28's stores reach the vault through a duplicate of the session's held root handle. Consequence: a share whose server reports NTFS or ReFS but answers no `FileIdInfo` now refuses to open, where the answer as first recorded would have opened it weak. | WB-28's identity read, strength flag and store handling; WB-30's mode rule and its FAT32/exFAT facts |
| WD12 (in part) | 2026-10-21 | **Find in note works in the editor and the reading view, and find next and previous work from the text.** Ctrl+F opens the find bar in either mode and highlights the matches on the visible surface. Ctrl+G and Ctrl+Shift+G step to the next and previous match from the bar or from the editor or reading surface that shows the highlights, leaving focus where the reader is; each step moves the caret or selection to the match and speaks the count. The owner prefers this keyboard approach. If a spike shows the keys cannot reach Slate reliably from those surfaces under NVDA, JAWS or Narrator, the accepted fallback is a results-list panel: an accessible list of the matches with their context, arrows to move between them and Enter to land on one. Reading-view find goes beyond Mac, which has no reading-view find; WB-0 files the Mac follow-up. The chord disposition, regex and replace parts of WD12 stay recommendations. | WB-22's scope, delivery and facts; WB-0's Mac follow-ups; §6's reading-surface order |
| WD1, WD3–WD11, WD12 (the rest), WD13–WD17, WD19, WD21–WD23 | 2026-10-26 | **Adopted as recommended.** The owner approved the delivery run with every pending decision taken as the §3 table recommends it: WD1 (plan 43 §11.4 points here, PR 31's slot retired), WD3, WD4, WD5, WD6, WD7, WD8, WD9, WD10, WD11, the rest of WD12 (disposition 1 under one `SharedCommandChords` entry, replace in the editor only, no regex, find-in-selection deferred), WD13, WD14, WD15, WD16, WD17, WD19 (#1051 stays with W8-2's palette pass), WD21, WD22 and WD23. | Each chunk's spec as written for those rows |
| WD20 | 2026-10-26 | **As recommended, except who writes and builds it.** One design for both hosts fixes the store schema, the restore rule and the open-sequence order, the close and discard rules, the weak-root rule, the lifetime rule and the peer rule as the §3 row lists them; under the weak-root rule a weak root gets no hot exit (the review calls below). The design and the core store are plan 44's WB-29a, not plan 43's PR 29, whose queue is paused; plan 43's PR 29 keeps the mac half and consumes WB-29a's store. WB-29 starts only after WB-29a's core store is on `main` with green CI. | WB-29a, WB-29; plan 43 §11.4 row 29 |
| WD18 | 2026-10-26 | **Moot.** #1457 closed on 2026-10-22: #1537 moved MathCAT to 0.7.7, which is on zip 9, and `.cargo/audit.toml` holds no active exception. | WB-0 renews nothing and leaves the audit gate alone |
| Run process | 2026-10-26 | **How the chunks are delivered.** One chunk is implemented at a time, the next starting while the previous sits in CI. A fresh Claude subagent red-teams every chunk — its spec, issues, contracts and diff, not the implementer's conclusions — before its PR opens, by `24_red_team_protocol.md`'s prompts and stopping rules; Codex reviews as well WB-10, WB-22, WB-25, WB-26, WB-28, WB-29a, WB-29 and WB-30. An agent-driven NVDA, Narrator or JAWS pass (§4 rule 4's recipe) lifts a PR's draft; agent runs never fill a human cell in `w_c_matrix.md`. The agent merges a PR once every check passes and Codoki reports "Safe to merge" at a confidence of 3/5 or more, pinned with `--match-head-commit`, one at a time, `main` green on each merge commit before the next. | §4 rules 4 and 5 |
| Review calls | 2026-10-26 | **What the drift check of 2026-10-26 raised, answered as recommended.** WB-9 parks a second launch or jump-list open while a close is deciding, too, and runs it once the close is decided (#1622 pumps the close with the window enabled). WB-20 never lands a transient refusal — core's "busy" render (#1632) or the host's "image-busy" decode (#1636) — so the next refresh retries it, as `main` does; its PR writes `Refs #1635` for the diagram half. WB-22's reading-view find matches a math block's source and marks the whole math element, never the hidden speech run #1633 puts in the text. WB-26 no longer waits for plan 43 PR 32 (#1370): they share no Windows file. WB-28 starts once its design review posts the `FsRoot` helper names on #1513, not after 22c merges. Plan 43 PR 29's shared hot-exit design and its core store move here as WB-29a (docs and Rust, no Mac needed); plan 43 PR 29 keeps the mac half. WB-29 gives a vault whose root identity is weak (FAT32, exFAT) no hot exit: closing falls back to today's prompt with core's reason. WB-30 never deletes permanently without asking: it keeps the warning trash-rs asks for today (`FOF_WANTNUKEWARNING`). WB-30's start is decided when W10 is reached. #1618's two files carry the repository's AGPL header, and the licence check requires it (WB-0). | WB-9, WB-20, WB-22, WB-26, WB-28, WB-29a, WB-29, WB-30 and WB-0 |

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
   `dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <the exact .cs files the change touched, new test files included>`
   (a literal `<files>` is not a PowerShell argument; build the list
   from `git diff --name-only` filtered to `.cs`); check CR counts before
   editing a file (mixed line endings flatten).
3. **Core changes run the Rust gates here in PowerShell** (there is no
   `make` on this machine; `CONTRIBUTING.md:76-98` gives the loop):
   `cargo fmt --all -- --check`, `cargo clippy --all-targets --workspace -- -D warnings`,
   `cargo test -p slate-core` (the Windows-cfg facts included),
   `cargo bench --workspace --no-run` for the bench compile, and
   `& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh` (Git Bash by full path; a bare `bash` in PowerShell here resolves to WSL) (the Makefile's
   `check-license-headers` target; `apply-license-header.py` only adds
   headers and has no check mode, so run it after `git add` on a new
   file and confirm the diff is only the header). The Windows lane runs
   the same gates; the Linux lane proves `cfg(unix)` compiles.
4. **Field passes are NVDA first, Narrator for the reading view,**
   with the SendInput plus `nvda.log` recipe from
   `18_windows_port/reports/nvda_agent_matrix_pass_2026-09-22.md`. Until
   WB-1 lands, back up `%LOCALAPPDATA%\Slate` first; after it, every
   field pass launches the app under test through WB-1's
   `scripts/windows/launch-for-field-pass.ps1`, never `SlateWindows.exe`
   directly: it points `SLATE_LOCAL_APP_DATA` at a temporary root,
   seeds one or more vaults into the recents (one `-Vault` string, several joined by `|`, since `pwsh -File` cannot bind an array), and with `-Root`
   relaunches on the same root (a pass that reopens, such as WB-29's,
   reads the device state it wrote). A `SLATE_TEST_*` diagnostic seam
   set in the calling window reaches the app. This matters because an
   ordinary launch still writes the owner's real recents and
   preferences by design (#1331). A PR whose feature
   changes what the reader hears — a new sentence, a removed one, or a
   changed name or hint — stays a draft until its pass is recorded in the
   PR body. An agent-driven pass by that recipe lifts the draft (the
   owner's call, 2026-10-26); it never fills a human cell.
5. **Adversarial review before every PR** (the owner's call, 2026-10-26,
   §3.1 "Run process"): a fresh Claude subagent red-teams every chunk
   with `24_red_team_protocol.md`'s prompts and stopping rules before its
   PR opens; Codex reviews as well WB-10, WB-22, WB-25, WB-26, WB-28,
   WB-29a, WB-29 and WB-30; the merge-delta check before merge when
   `main` moved under it.
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
| WB-0 | Close-outs and housekeeping: verify-and-close #1320 and #1261; file the mac follow-ups this plan names (#1526's mac consumer, #1651; #1290's mac migration off `switcher_rank_top` and `tag_tree`, and a node identity for `full` on both hosts, #1652; #1484's macOS measurement of WB-31's remedy, #1653; #1112's \"mac: find in note in the reading view, with ⌘G from the reading text\", #1654); add the §2.3 pointers to plan 43 §11.4 and retire PR 31's slot; record the owner's answers of 2026-10-26 (§3.1); #1618's two files take the repository's AGPL header and the licence check requires it. The zip renewal it once carried is moot (§3.1 WD18) | #1320 #1261 #1618 | S | windows, license-headers | — | WD1 |
| WB-1 | The test suites stop writing the developer's `%LOCALAPPDATA%\Slate`: one `SlateDeviceState.Root` honouring `SLATE_LOCAL_APP_DATA`, six stores and `HostLog` on it, the shell gate on a per-run temp root, the four bare constructions; a whole-run wrapper `scripts/windows/guard-device-state.ps1` that snapshots the real profile around a test command and its child `SlateWindows.exe` processes (CI's two Windows test steps run under it — the binary-only gate runner from the copy the artifact carries; an in-process fact is only a fast partial signal); a field-pass launch script that seeds a temporary root from one `-Vault` string (several vaults joined by `|`, split and validated by the script, because `pwsh -File` cannot bind an array) (§4 rule 4) | #1331 | M | windows | — | — |
| WB-2 | Save-chain admission: a new `WorkspaceSaveCoordinator.Admit(path, operation)` always reserves the path's chain and completes only when the operation's publication (or terminal failure) lands; the History restore (its write and its reload), template reloads and property writes run through it, so neither direction overtakes the other; the operation's own reload runs inside its ticket and carries a queued save across the epoch bump, and the restore re-applies its dirty refusal when it starts; an admitted property write re-applies its dirty refusal too, then waits for its header to publish the saved hash (new `NotePropertiesViewModel.WhenPublished`, which always ends — published, failed or abandoned) and takes the edited row's CAS token from that fresh header, a changed row taking the existing conflict path | #1322 #1325 | S–M | windows | — | WD2 (answered 2026-10-21) |
| WB-3 | Core-owned task and activation speech: post `TaskToggled` / `TaskToggleFailed` and `NothingToActivateAtCursor`; retire the residue rows and `proposed` designations; regenerate the trigger ledger | #1467 #1465 | S–M | windows (docs census) | spot | — |
| WB-4 | Citation async facts: land 21cfc8e's hold, confirm 50× on Windows, replace the three "has not landed yet" bets with holds (one `FilesCitingViewModel` seam) | #1316 #1285 #1315 | S–M | windows | — | — |
| WB-5 | Keys never leak past a modal surface: the editor popover joins `ModalSurfaceState` so F6 is a no-op; the sheet fence refuses Undo, Redo, Cut, Paste, Delete and the editing commands, and guards the palette, search and Quick Open fields too; contract 40 R-6 note | #1281 #1259 | S–M | windows | NVDA spot (F6 on the popover says nothing) | — |
| WB-6 | Tree publication keeps every reader's element: date groups keyed by (parent, label) and reconciled in place; organic publications take the keep path; when the selected row is not kept, the selection is disarmed before the reconcile and resolved after the pending restores (a rename's or move's named successor first): a published row of the same key — the note changed date group, the grouping changed, its folder was rebuilt — is re-selected silently and takes the keys (or the collapsed group now holding it does), and only when none exists do the keys land on the nearest survivor *without selecting it* (R-5's focus-without-select, as `ReconcileSelectionAfterPublication` already does, `FileManagement.cs:644-673`), the survivor resolved from the row's place snapshotted before the reconcile moves anything (the in-place reconcile empties kept folders' and groups' child lists), at any depth and with a surviving date-group header eligible, with WPF's automatic first-row selection suppressed during the reconcile — hosted facts prove no note opens and no selection announcement fires, at the root, in an open folder and under a date group, and that a re-bucketed, re-grouped or renamed selected note stays selected with the keys moving once | #1522 #1515 | M | windows + FlaUI | spot | WD3 |
| WB-7 | Flake sweep: condition waits for the eleven Windows items of #1323 (clipboard seam for the code-copy fact, `WaitForTreeItems` for `OpenCanvasFromTree`, `ReleaseProjection` on the specific projection, graph probes behind a publication gate, and the 22 s ratio fact benchmark-tagged; `ReleaseProjection`, the clipboard seam and the virtualization fact are recorded as already done — the last re-verified under load and by mutation, not rewritten: main's item-unit viewport-plus-cache capacity with separate resting and paging bounds, never from realised rows, and c247b865's cleanup wait), `CanonicalHighlightCensus` pumps until the refresh lands, a shared `CancellationTokenSource` owned by `StaThread.Run` so a timed-out body unwinds and the wedge message names the root fact; `structural_batch.rs`'s negative waits become deterministic rendezvous with diagnostics | #1306 #1321 #1294 #1323 (Windows items; then retitle per RD17) | M–L | windows, rust | — | — |
| WB-8 | Core words the failure: core's `FileReopenFailed` (or a new `NoteOpenFailed`, WD23) replaces `VaultException.Message` in the tab's load failure and in the reopen's own catch (two census rows), and is spoken once by the command that opened the tab (`Load` records it; an explicit open or the reopen speaks it); a core `embed_target_kind` makes Ctrl+E title a `.base` embed as a base on both Windows surfaces (the Windows `A11yEvent` mirror in `lib.rs:9164` and `:11041` changes only under the new-event answer) | #1266 #1288 | S | rust, windows, swift (compile) — all three under either WD23 answer, since `embed_target_kind` is a core export; the blind mirror entry only under the new-event answer | NVDA spot (the open and reopen failure sentences, each once; the base embed title) | WD23 |
| WB-9 | Open, handoff and landing lifecycle: a second launch or jump-list open parks behind a modal loop and runs when it ends; the welcome view holds the keys with an "Opening vault…" status until `WorkspaceReady`; a held reading landing is carried over, under the token the window's slot holds, to the projection a rescan's reload installs, ending exactly once (resumed or refused) instead of being dropped (the pinning fact flips; contract 40 R-9/R-10 records) | #1296 #1319 #1312 | M | windows | NVDA | WD4 |
| WB-10a | Core: a change-feed window drops the link graph so the hosts' generation probes rebuild it — on an `Exact` window carrying any record from another writer (a directory rename moves every note beneath it), and conservatively on every `Gap` and `TooLarge` window (they carry no records, `change_feed.rs:100-129`, and the full re-sync behind them cannot see another session's already-indexed links); two-session facts for the exact, trimmed-cursor, no-cursor and oversized cases, a no-bump fact for a window with only this session's records, and a floor bump for a session whose graph was never built | #1519 | S–M | rust, windows | — | — |
| WB-10 | 21b, the Windows host of the change feed, per `21_change_feed_design.md` §6–§7 and Q1–Q8: register after the open scan, window with the journal held, the record path in two phases (removals and presents per page with removed paths accumulated over the whole window, renames applied after the last page by baseline hash through a new `RetargetPathForFeedRename`, Quick Open `ApplyRecordsAsync`, dependents narrowed by path and by touched directory prefix, the Bases and graph probes on any foreign record as WB-10a, failed dependents retried by the next run and failed surfaces re-checked on an unchanged explicit Refresh independent of the records (design §4.3, Q8)), the explicit-Refresh-only counting sentence, up to three passes, acknowledge; the retired fact rewritten first; FlaUI `AnotherSession_ChangesCountedOnRefresh` with an in-process second `VaultSession` (the gate's artifact carries no `slate-cli.exe`); close #1293 by hand | #1293 | L | windows + FlaUI | NVDA | — |
| WB-11 | Reading-view list semantics and cost: authored list markers rendered and read; item peers cached, names lazy, positions indexed once, `CensusDiag` gated, measured with a timed UIA probe that replays a screen reader's per-arrow queries on a 1,996-item note sized to the 2,000-block cap; Ctrl+A raises `TextSelectionChanged` through one cached text provider | #1422 #1329 #1330 | M | windows + FlaUI | NVDA timing, Narrator | — |
| WB-12 | Watcher noise and the vault-root stores: a folder's own Modify is dropped in `translate`; an in-vault Bases export writes through core; `GraphConfigStore` writes through `AnchoredVaultStore`; stale pins are pruned lazily through an error-aware absence probe and an under-lock re-check, committed on the dispatcher with their memory publication (WD6); Open Recent on a folder holding only `.slate` says so in observable terms (WD7); `ModifyKind::Name` is never suppressed | #1516 #1508 #1505; Refs #1475 (its identity half closes in WB-28) | M | rust, windows, swift (compile, if `replace_file_bytes` is added per WD5) | NVDA spot (the export's single sentence; a pruned pin's silence; the Open Recent sentence) | WD5 WD6 WD7 |
| WB-13 | Canvas outline: deleting the focused card seats the next row in reading order (else the previous, else the surface root) and marks `TryMarkFocusReturned`; the "Deleted … Ctrl+Z to undo" sentence is unchanged — from the card menu or the palette, in either order of publication and the palette's focus restore (the successor is seated while the palette holds the keys and the restore lands there) | #1260 | S–M | windows | NVDA spot | — |
| WB-14 | Bases repair on Windows: a no-view definition loads as failed with core's wording, stays editable, "Save to View" emits `BaseEdit::AddView`, and the failed state renders mac's group with an Edit button addressed to the tab that shows it (a split group's surface activates its own tab first), disabled with the definition's reason, which the palette speaks (a `ReasonedCommand`) — the read-only Bases dock and a failed saved query show the heading and message only; editability derives from a definition-availability state (never from a retained handle) and Refresh probes a `.base` definition before executing (a saved query has no definition file and is never probed; its handle and rows survive Refresh and the rescan) — explicit Refresh is the recovery route for a deleted `.base`, since the rescan reload loop (`WorkspaceViewModel.Rescan.cs:276-290`) reloads only hash-changed bases; six twin facts of `BaseContainerRecoveryTests.swift` | #1397 | M | windows | NVDA | WD8 |
| WB-15 | Shell-seal witnesses: `Transition`/`OnTransition` call-graph census, landing-request census in pinned roles (the admission callers, the documents' two publishers, their lifecycle clearers, the surfaces' relays) with planted unauthorized construction, write and call, `AddHook` inventory, method-group prompt detection, `MainWindow` input-override detector, `PressPointer`/`Wheel` model WPF routing, with key-release, text and split press/release helpers; probes for shell-owned windows (every I3 route) and UIA invocation, and the guards or the recorded exclusion WD9 calls for | #1313 #1308 #1307 | M | windows | — | WD9 |
| WB-16 | Spoken names: unresolved citation rows keyed; the pane line composed after the invalidation; saved-query create, update and rename through `SavedQuerySpokenName`; dashboard names in the census; shortcut slots spoken; the recent-vault distinguisher is the shortest distinguishing ancestor; the rail coalesces its workspace write and defers History and Queries behind an explicit settle (when the last held rail arrow is released or focus leaves the rail, never while an arrow is down — the first key repeat can come after any fixed interval, so a timer only polls the key state as a backstop for a release the seal took — and not dispatcher idleness, which falls between key repeats) | #1301 #1311 #1305 | M | windows | NVDA spot | — |
| WB-17 | Docs and registers back in step, checked in CI: the chord table's SD-1 and PD-1 reasons, `chords.json` regenerated, the two Swift citations, contract 38's AR-23 row and designation, contract 34 and 35 reconciliation tables with `--check` steps in the `windows` app job after its build (`windows.yml:523-527`; the existing `generate-parity-matrix.py --check` job runs on Ubuntu without a binding), the `on_external_change` doc sentence | #1384 #1381 #1390 #1376 #1386 #1510 | S | windows, rust (doc) | — | — |
| WB-18 | Core: a poison-proof fault guard (`FaultGuard` over a private `FAULT_MUTEX`, replacing `ENV_FAULT_GUARD`), then bounded link resolution everywhere (`reindex_path_locked`, `PreMoveIndex::offer_candidates`, Bases and DQL through `FoldedNameLookup`, a failed lookup failing the query as its public failed result `BasesResultSet.error` — never `Err(VaultError)`, a cell error or an unresolved link) | #1481 #1427 #1485 #1486 | M | rust | — | — |
| WB-19 | Core: the create heal reads only the links its path could resolve (two folded `target_raw` indexes — exact (rooted or qualified) targets by path, bare names by name, through one classification shared with the selector; migration 041, which — being at or past `FEED_MIGRATION_VERSION` — clears every session's change-feed cursor, `db.rs:489-491`, and fences older sessions with `SchemaVersionSkew`) | #1426 | M | rust, windows, swift (compile) | — | — |
| WB-20 | Progressive reading projection: text, code, math and embeds publish first with named diagram placeholders; diagrams complete the same placeholder element in place (it is focusable, so it is never swapped) while the pending source and any failure header, reason and source stay paragraph Runs in the text range, with anchors inside the source remapped; caret, selection, focused link and held landings survive (a rescan-carried landing, WB-9, seats on the reloaded partial projection); WB-11's peer caches survive a text-only completion and are invalidated once when an unmatched placeholder's container is removed; unchanged diagrams stay complete across refreshes; the diagram stage belongs to the projection it completes, so a refresh that keeps that projection (a memo hit, a preserved failure — a preference refresh's included, since a preference change retires nothing — a cancelled rescan) leaves it to land, its landing never restores a memo dropped while it ran, and the rebind fast path resumes it; SVG decoded off the dispatcher | #1317 | M–L | windows + FlaUI | NVDA | WD11 |
| WB-21 | Graph verbosity palette rows registered; the dead preamble modes removed on Windows (RD6) | #1415; Refs #1389 (closes after both halves, see the closure matrix) | S | windows | NVDA spot (the three palette rows; no verbosity announcement for a removed mode) | — |
| WB-21b | Core's `code_block_preamble_with_verbosity` (a sibling function; `code_block_preamble` unchanged) over the token stream per WD13, Windows consuming — every Reading model made with the verbosity provider through its constructor (restored, toggled and replaced alike) — with the two modes back in its menu and WB-21's two removal pins replaced | Refs #1526 (closes with its mac follow-up, filed by WB-0 as "mac: consume `code_block_preamble_with_verbosity`, Settings samples through core, VoiceOver pass per mode") | M | rust, windows, swift (compile) | NVDA per mode | WD13 |
| WB-22 | Find in note (Ctrl+F) in the editor and the reading view, two PRs after a step-0 key spike (do Ctrl+G and Ctrl+Shift+G reach Slate from the text under NVDA focus and browse mode, Narrator scan mode off and on, and JAWS if installed?). PR 1, the editor: `FindInNoteViewModel` over AvalonEdit's search strategies, a landmark bar beside the note, "N of M" as a pinned residue, wrap, case, whole word, replace in the editor only (never on stale results: result sets are stamped with the document version and option revision; navigation and Replace All recompute when stale, and Replace acts only on the hit it last revealed — tracked by `TextAnchor`s and validated before any recompute — refusing with "Match changed; nothing replaced." when that hit no longer matches); Ctrl+G / Ctrl+Shift+G step from the bar and from the text through two window `KeyBinding`s on `FindInNoteKeyRoute`'s routed commands, whose window `CommandBinding` passes the key through unless the focused note's bar is open (a plain-`ICommand` `KeyBinding` would swallow it); the bar's own keys (Enter / Shift+Enter in the query, Escape in the bar) in a new `ChordScope.FindInNote`; fall-through to vault search from a command owned by `VaultLifecycleViewModel` so the welcome view reaches it too; chord rows and Editor-menu item; one `SharedCommandChords` entry for the whole Ctrl+F group (Bases, Canvas, editor — replacing today's Bases/Canvas pair). PR 2, the reading view (after WB-11 and WB-20): a `ReadingTextIndex` over the reading document, highlights drawn by `FindMatchAdorner` in the surface's own adorner layer with no automation peer and the reading document never mutated, re-run on a republish and on WB-20's in-place diagram completion. If the spike finds a mode that keeps the keys from the text, an accessible "Matches" results list joins the bar (the owner's accepted fallback). Eleven automation ids (twelve with the list) registered in `w_c_automation_ids.json`, §W-C row, axe and FlaUI journeys; the parity note cites the mac follow-up #1654 | #1112 (PR 2 closes; PR 1 refs) | L (two PRs) | windows + FlaUI | spike: NVDA, Narrator, JAWS; passes: NVDA, Narrator, both modes | WD12 |
| WB-23 | A parked citation summary is dropped when its note changes | #1314 | S | windows | NVDA spot (reopening the note is silent) | WD10 |
| WB-24 | Trash on Windows: a gated real Recycle Bin fact on a scratch volume, run by the lane WD14 names before the chunk is complete; the dangling-link probe, then trash-as-link or truthful host wording; the OneDrive Files On-Demand probe, then one name-surrogate classification across all three inventory checks and a truthful refusal where Trash stays fail-closed (the `TrashOutcome` model and `BatchTrashReport.outcomes` of WD16 originate here if the refused-kind branch ships; the fourteen Swift `BatchTrashReport` literals are updated in the same PR) | #1480 #1499 #1492 | M | rust (Windows cfg), windows, swift (literals) | NVDA spot (any changed Trash or refusal sentence) | WD14 WD15 |
| WB-25 | Bounded FFI lists: a paged, cancellable tag tree in segment-aware pre-order (one injective `cmp_segments` comparator — `build_tag_tree`'s own segment order — over each node's key, its segment path joined with `/`, so tags holding U+0000 or U+0001 never collide with the separator; the cursor and the counts use that key, never `full`, which leading empty segments make ambiguous (`/a`, `a` and `//a` all read `a`), and the pages keep `tag_tree`'s `full` byte for byte; a core page type and a shared `flatten_preorder`; core's `TagTree` is nested, only the FFI record has `entries`; descendant counts by a BINARY byte range, never `LIKE`), then an index-backed `switcher_rank_top_indexed` that runs `switcher_rank_top`'s own heap and comparator (score, recency, NFC path, input order); Windows drops `_files` and the listing seam (the open-cancellation fact moves to the open's scan, the rescan's close-drain fact to its hash read); the old entry points stay for mac | #1290 | L (S–M, then L) | rust, windows, swift (compile) | — | WD17 |
| WB-26 | Canvas board edge peers per contract 34 D3–D5: `CanvasEdgeAutomationPeer` named by a core-rendered edge phrase (its design section chooses between a `speakable_name` on `CanvasSceneEdge` — an FFI record change with blind Swift literal edits — and a core free function; `CanvasConnectionTraversed` is card-relative and cannot name a board edge), materialized with the edge placements, realized at the current zoom on its union when that fits the view and otherwise on its designated endpoint, Invoke total over the end-style table; live UIA-tree facts | #1277 | L (design first) | rust, windows + FlaUI, swift (compile) — all three under either design answer, since the free-function alternative is a core export too | NVDA | — |
| WB-27 | 38W: the Windows no-replace primitives proven on FAT32, exFAT and an SMB share (env-gated provider-level facts over a mounted VHD and a `\\localhost` share): create and publish a new note through the staged path (`fs.rs:698` onward — stage, sync, publish; an occupied destination is preserved and the stage is cleaned up), rename and move (an occupied name refuses, a free one lands), case-only renames keep identity; capability-matrix rows | #1434 #1470 (Windows arm; Refs) | S–M | rust (Windows cfg) | — | — |
| WB-28 | 22W: the descriptor-relative session on Windows — `FsRoot` holds the root and `.slate` as handles, `NtCreateFile` with `RootDirectory` for the interior, the SQLite VFS hook's Windows twin, `root_identity()` from `FileIdInfo`, else — only on a FAT, FAT32 or exFAT volume — from `GetFileInformationByHandle`'s volume serial and file index flagged weak (`persistent: false`), which authorizes nothing (the host's vault stores reach the vault through a duplicate of the held root handle, `duplicate_root_handle`), in the lossless form WD22 fixes (FFI record change with the `persistent` flag, both bindings; Swift reads at `AppState.swift:10670-10671` and `SidebarOrganizationDispatchTests.swift:2478-2479`), `verified_current_path` by handle, the provider's reads relative; `descriptor_interior.rs` stays `cfg(unix)` (its facts rename held objects or use `std::os::unix`) and gains a Windows sibling of handle-pinning twins and retargeted-junction interior-resolution twins; #1475's identity half and the `FileRecentsStore` key migration (persistent identities, deleting the path-keyed file only after its successful read and a confirmed destination commit, and retried until complete; a weak one keeps the path key; the old in-vault recents file, for either identity, migrates the same way and stays retryable after an `Add` created the destination, newer destination entries first, and Quick Open's Clear drops an unmigrated one); a step-0 probe of both reads on WB-27's FAT32 and exFAT VHDs before any code; a root does not open when `FileIdInfo` fails on any other file system or neither read answers on FAT, FAT32 or exFAT — core refuses before `.slate` is made with its own sentence, which the host speaks (WD24 as amended) | #1475 (identity; PR 1 `Refs`, PR 2 `Closes`) #940 (Refs; Milestone W) | L (design first: `22_descriptor_relative_session_design.md` §7 row 22W) | rust, windows, swift (compile) | NVDA (spot check) | WD7 WD22 WD24 |
| WB-29a | Hot exit, the shared design and the core store, moved here from plan 43 PR 29 by the owner's call of 2026-10-26 (§3.1): one design for both hosts that fixes WD20's rules (the store schema, the restore rule and the open-sequence order, the close and discard rules, the weak-root rule — no hot exit on a weak root — the lifetime rule and the peer rule), reviewed before code (beside WB-28); then, after WB-28 is on `main`, the process-lifetime core store, its FFI and its facts; the mac half stays plan 43's PR 29. Its spec is written when its wave starts | Refs #1525 | L (design first) | rust, windows, swift (compile) | — | WD20 |
| WB-29 | Hot exit, Windows half, consuming the core store WB-29a ships: unsaved buffers written as you type to the store outside the vault, each write a WB-2 `Admit` operation on the file's save chain until it is durable (every teardown route abandons, never waits on, one still in flight: recovery writes and discards run on a process-lifetime store host that no teardown or workspace disposal joins, whose store calls borrow no vault session, so save workers alone keep today's join; a teardown waits for its discards at most 5 s, unpumped, then proceeds, logged); a weak-root vault follows the design's weak-root rule (WD20); closing never asks once a durable flush of the latest buffer generations succeeds (a failed, timed-out or over-cap flush falls back to today's prompt, whose Discard All, like Close Vault's and like a dirty tab's own Discard, fences pending recovery writes before the store's discard, so a late write cannot restore a discarded buffer); on open, recovery state is read before the `WorkspaceViewModel` is constructed (`VaultLifecycleViewModel.cs:1184`) and injected into `RestoreNode`'s tab construction (`Persistence.cs:53-108` builds new instances; leftover entries open as new dirty tabs), and `WorkspaceReady` (`:1280`) is raised only once restored buffers are installed, so the launch landing happens once; disk-changed buffers are restored dirty into the save-time conflict machinery | #1525 (Windows half) | L | windows (core store from WB-29a) | NVDA | WD20 |
| WB-30 | Trash verification T2, core and Windows: `IFileOperation` with a progress sink returns the recycled item, re-observed by volume serial and file id where the volume's file system keeps ids (NTFS, ReFS) and WB-28's root identity is the persistent `FileIdInfo` kind, and by an id-free fingerprint where it reports FAT, FAT32, exFAT or another file system or the root identity is WB-28's weak file-index fallback (WD24 as amended) — staged, revalidated and verified without an id read — through one verifier shared by every platform; a back end that reports no destination (macOS until PR 41, Linux) keeps main's batch state; the Windows host's sentences ship with the back end; a `ConfirmedItem` captured at staging is passed through deletion so the verifier compares against the confirmation, not a recapture; the destination-to-outcome mapping is total (`UnlinkedForGood`, `DeletedOutright`, `Unknown` included) under WD16's one model, with `trashed` unchanged. The single delete's eight session methods and six FFI wrappers return the outcome whatever WB-24 shipped, and a diagnostic seam any build can trip (`SLATE_TEST_TRASH_REPLACE_AFTER_VALIDATION`) provokes the mismatch for the NVDA pass. Prerequisites: the shared core identity primitive from plan 43 PR 34 (#1469) merged with green combined-head CI, WB-24's recorded real-Recycle-Bin results, and WB-28 | Refs #1468 (its mac host half, plan 43 PR 41, closes it) | L | rust, windows, swift (compile) | NVDA | WD16 WD24 |
| WB-31 | Core: two sessions in one process — a bench that reproduces or refutes the shared page cache on Windows by per-connection cache counters; the remedy (a `ConnSlot` for the main and the compactor's connection whose guards record activity when taken and when dropped, so an idle interval starts when a use ends, and a new idle scheduler releasing idle, unheld sessions' caches while two or more are registered) waits for WD21; the issue's close waits for a macOS measurement | Refs #1484 (closes per WD21) | M (diagnose first) | rust | — | WD21 |
| WB-32 | uniffi 0.32 spike: generate the 0.31 baseline with the pinned generator first, then build the C# generator from NordSecurity/uniffi-bindgen-cs#176, bump, regenerate, build and test `apps/slate-windows`; report, ship only on a tag | #998 (Refs) | S | windows (local) | — | — |

### Closure matrix

An issue closes in exactly one PR; a chunk that delivers part of an
issue writes `Refs #N`, never a closing keyword, and §10's PR-body rule
names what the chunk left. A split issue closes only when every half
is merged and verified: the earlier PRs, whichever host goes first,
write `Refs`, and the last delivery closes it (or the owner closes it by
hand with a comment naming both merges). 65 in scope: WB-0 closes 2
(#1320, #1261), and one more outside the 65, #1618, the owner's rider of
2026-10-26; 53 close in the one chunk that lists them; ten close
elsewhere or on a gate:

| Issue | Delivered here by | Closes in | Gate |
|---|---|---|---|
| #1475 | WB-12 (the `GraphConfigStore` write) | WB-28's PR 2 (the identity half; PR 1 writes `Refs`) | WB-28's PR 1 and PR 2 both merged and verified |
| #1389 | WB-21 (Windows half) | whichever of WB-21 and plan 43 PR 52 merges last | both halves merged |
| #1526 | WB-21b (core and Windows) | the mac follow-up WB-0 filed, #1651 | VoiceOver pass per mode on mac |
| #1323 | WB-7 (the eleven Windows items) | WB-7's PR, but only after plan 43 PRs 28 and 37 have landed its mac and Rust items (RD17: those land first, then the issue is retitled to the Windows list); if WB-7 merges before them it writes `Refs` and the close is by hand after the retitle | PRs 28 and 37 merged, then the retitle |
| #1525 | WB-29a (the shared design and the core store), WB-29 (Windows half) | whichever of WB-29 and plan 43 PR 29's mac half merges last (WB-29a's design and core-store PRs write `Refs`) | both hosts merged and field-passed (WD20) |
| #1468 | WB-30 (the shared outcome contract and Windows) | whichever of WB-30 and plan 43 PR 41 (macOS Rust half and mac host) merges last | PR 34's identity primitive; both platforms merged |
| #1434 #1470 | WB-27 (the Windows proof) | plan 43 PR 38 (the macOS probe and refusal), after WB-27 has merged too | both merged |
| #940 | WB-28 (22W) | by hand after 22b and 22W, per RD15 | — |
| #998 | WB-32 (the spike) | the dependency bump when a tag exists | upstream |
| #1484 | WB-31 (the Windows bench and, if the mechanism is proven, the remedy) | by hand after a macOS measurement confirms the remedy (the mac follow-up #1653), or the owner withdraws the macOS observation (WD21) | a Mac run |

## 6. Delivery order

At most three agents at once (`subagent-parallelism`). Chunks in one
wave share no files except `WorkspaceViewModel*.cs` (different partials)
and core's `a11y.rs` and corpus (independent variants); a merge of `main`
is expected inside a wave.

| Wave | Slot 1 | Slot 2 | Slot 3 | Gate |
|---|---|---|---|---|
| W0 | WB-0 | — | — | WD1 (answered; WD18 moot) |
| W1 | WB-1 | WB-2 | WB-3 | WD2 |
| W2 | WB-4 | WB-5 | WB-6 | WD3 |
| W3 | WB-7 | WB-8 | WB-9 | WD4 |
| W4 | WB-10a, then WB-10 | WB-11 | WB-12 | WD5 WD6 WD7 |
| W5 | WB-10 continues | WB-13, then WB-14 | WB-15 | WD8 WD9 |
| W6 | WB-16 | WB-17 | WB-18 | — |
| W7 | WB-19 | WB-20 | WB-21 | WD11 |
| W8 | WB-21b (after WB-20), then WB-22 may start (both edit `MainWindow.xaml`); WB-22 is two PRs after a step-0 key spike, and its PR 2 edits `Reading/ReadingSurface.cs` after WB-11 and WB-20 | WB-23, then WB-24 | WB-25 (tag tree) | WD10 WD12 WD13 WD14 WD15 WD17 |
| W9 | WB-25 (switcher) | WB-26 | WB-27 | — |
| W10 | WB-28 | WB-29a's design (docs only: its core store waits for WB-28, below) | — | WD16 WD20 WD22; 22c's `FsRoot` names posted on #1513 by WB-28's design review (§3.1) |
| W11 | WB-30 (after WB-28, WB-24's results and PR 34's primitive; its start decided at W10, §3.1) | WB-29a's core store (after WB-28), then WB-29 (after that store is on `main`) | — | WD20; combined-head CI green on `main` |
| any | WB-31, WB-32 take a free slot | | | |

No wave runs two chunks that the sequencing table below orders; where a
chunk's prerequisite is in another program (PR 34 for WB-30), the wave
does not start until that merge is on `main` and `main`'s own CI is
green on the combined head. (WB-29's hot-exit design and store were
plan 43 PR 29's; since 2026-10-26 they are WB-29a, §3.1.)

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
| `Reading/ReadingSurface.cs`, `ReadingDocumentBuilder.cs` | WB-9 (`ReadingSurface.cs`: the carried landing), then WB-11, then WB-20, then WB-21b, then WB-22's PR 2 (`ReadingSurface.cs`: the content-changed event and the find adorner's render scope) |
| `WorkspaceViewModel.Layout.cs` (`TryOpenItem`, `ReopenClosedTab`/`ReopenFileAnnouncement`) | WB-8, then WB-16 (whose AR-55 reorder keeps WB-8's `ReopenFileAnnouncement` census row) |
| `WorkspaceViewModel.Rescan.cs`, `VaultLifecycleViewModel.Rescan.cs`, `EditorLandingSlot.cs`, `QuickSwitcherViewModel.cs` | WB-9 (which adds `WorkspaceTabViewModel.ReadingRescanReloadBegan`, `ReadingReloadTransaction` and `ReadingContentViewModel.Tab`), then WB-10 and WB-20 (which consume them), then WB-25 |
| `FilesSidebarViewModel*.cs` | WB-6, then WB-12, then WB-16 |
| `Canvas/CanvasOutlineView.cs`, `CanvasRendererView.cs` | WB-13 and plan 43 PR 32 (#1370) in either order, the second rebasing; then WB-26, which does not wait for PR 32 (they share no Windows file; §3.1, 2026-10-26) |
| `EditorInteractions.cs` | WB-3, then WB-8, then WB-21, then plan 43 PR 53 (#1466) |
| `ChordTable.cs`, `chords.json` | WB-17, then WB-21, then WB-22, then plan 43 PR 53 |
| Bases failed state (`BaseDocumentViewModel.cs`, `BaseSurfaceView.cs`) | WB-14, then plan 43 PR 35 (#1398), or the reverse with WB-14 adopting its copy |
| `vault/fs.rs`, `windows_entry.rs`, trash files | WB-24, then WB-27, then WB-28, then WB-30 |
| `links_db.rs`, `session.rs` link paths | WB-18, then WB-19 |
| Contract 38's generated ledger | Every chunk that regenerates it (WB-3, WB-8, WB-9, WB-10, WB-17, WB-22, WB-29, WB-30) runs `python -I scripts/a11y_trigger_ledger.py --write` then `--check`; in a same-wave pair (WB-8 and WB-9 in W3) the second to merge rebases and reruns both, never hand-merging the generated block; WB-3 and WB-17 kept apart |
| Core `a11y.rs` corpus | WB-8 alone in its wave; WB-30 after it |
| `VaultLifecycleViewModel.cs` (the open sequence: `OpenRecentAsync :724`, the workspace construction `:1184`, `WorkspaceReady :1280`) | WB-9 (`IsOpening`, `IsVaultOpen` beside `InitializeWorkspace`, the recents publication, `AdmitOpenCommand` first in `OpenRecentAsync`), then WB-12 (the `.slate`-only check after the admission), then WB-25 (the listing seam's removal from the open load), then WB-28, then WB-29, which rebases and re-verifies the combined open, identity-admission and recovery flow after `main`'s CI is green on WB-28 |
| Core store and FFI | WB-28 (`fs_root.rs`, `session.rs`'s identity and the FFI record), then WB-29a's core store and its FFI (both edit `crates/slate-uniffi`, and the weak-root rule reads WB-28's `persistent` flag; WB-29a's design, docs only, may run beside WB-28), then WB-29, which consumes that store and adds no core module (WD20 as answered, §3.1) |
| `MainWindow.xaml` (menus) in W8 | WB-21b (the verbosity items, `:535-556`), then WB-22 (the Editor-menu item and the Ctrl+F / Ctrl+G / Ctrl+Shift+G `Window.InputBindings`); the second rebases and the combined head runs the Windows lane before merge |

Field passes run one at a time on the owner's desktop with NVDA (and
Narrator for WB-11 and WB-22), launched per §4 rule 4. WB-22 first
runs a step-0 key spike with NVDA, Narrator and JAWS, whose record
decides whether its results-list fallback ships. Counting the AT
column of §5: full passes WB-9, WB-10, WB-11, WB-14, WB-20, WB-21b,
WB-22, WB-26, WB-29, WB-30 (ten), and spot checks WB-3, WB-5, WB-6,
WB-8, WB-12, WB-13, WB-16, WB-21, WB-23, WB-24 (ten). W4 carries two
full passes and one spot check; no wave carries more than two full
passes. A chunk with an empty AT cell changes nothing a reader hears;
if its implementation turns out to, §4 rule 4 applies and the PR body
records the check.

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
- Rust: the four PowerShell gates of §4 rule 3 (`cargo fmt --all -- --check`,
  `cargo clippy --all-targets --workspace -- -D warnings`,
  `cargo test -p slate-core`, `cargo bench --workspace --no-run`) plus the
  license-header script for new files; `make ci` is the CI-side name for
  the same set and does not run here.
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
| #1457 | — closed 2026-10-22: MathCAT 0.7.7 carries zip 9, and #1537 moved to it | — | none; `.cargo/audit.toml` holds no exception |
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
Every chunk has its spec under `44_windows_backlog/specs/`, written
2026-10-20 with every file-and-line claim re-verified against the tree.
Each spec ends with a "Brief" that, with this section, is all an
implementing agent needs; where a Brief asks for an NVDA or Narrator pass without naming a
launch command, the pass launches through WB-1's launcher per §4 rule 4. WB-10's spec reads with the change-feed
design's §6–§7, WB-28's with the descriptor-relative design's §7 row
22W, and WB-26, WB-28 and WB-29 open with a design section reviewed
before code.

| Chunk | Spec | What becomes true |
|---|---|---|
| WB-0 | `specs/wb00_close_outs_and_housekeeping.md` | Close-outs and housekeeping: #1320 and #1261 close on the facts they lacked, the four mac follow-ups exist, plan 43 §11.4 points here, the owner's answers are recorded, and every tracked `.rs`, `.swift` and `.cs` file carries the repository's licence line and no other |
| WB-1 | `specs/wb01_test_suites_leave_the_developers_app_data_alone.md` | The test suites and the field passes stop writing the developer's `%LOCALAPPDATA%\Slate` |
| WB-2 | `specs/wb02_save_chain_admission.md` | A History restore, a template reload and a property write join an admitted save of the same note instead of racing it |
| WB-3 | `specs/wb03_core_words_the_task_toggle_and_the_empty_activation.md` | Core words the task toggle and the empty activation: Windows posts `TaskToggled`, `TaskToggleFailed` and `NothingToActivateAtCursor` |
| WB-4 | `specs/wb04_citation_async_facts_hold_their_work.md` | The citation interleaving facts hold the work they reason about instead of betting on the scheduler |
| WB-5 | `specs/wb05_keys_never_leak_past_a_modal.md` | Keys never leak past a modal surface: F6 is a no-op over the editor popover, and a sheet refuses the editing commands it cannot answer |
| WB-6 | `specs/wb06_tree_publication_keeps_every_readers_element.md` | Tree publication keeps every reader's element: date groups, organic publications, and the vanished selected row |
| WB-7 | `specs/wb07_flake_sweep_condition_waits.md` | The Windows lane waits on conditions, not the clock: the eleven #1323 items, the highlight census, a cooperative STA timeout, and deterministic structural-batch rendezvous |
| WB-8 | `specs/wb08_core_words_the_load_failure_and_the_base_embed.md` | Core words the failure: a tab that cannot load speaks core's sentence, and Ctrl+E titles a `.base` embed as a base |
| WB-9 | `specs/wb09_open_handoff_and_landing_lifecycle.md` | Open, handoff and landing lifecycle: a handoff parks behind a modal, the welcome view holds the keys through the open, and a rescan reload carries a held reading landing |
| WB-10 | `specs/wb10_windows_host_of_the_change_feed.md` | 21b, the Windows host of the change feed: another session's changes are applied by record and counted exactly on Refresh |
| WB-10a | `specs/wb10a_change_feed_window_drops_the_link_graph.md` | Core: a change-feed window that carries another session's change drops the link graph, so the hosts' generation probes rebuild it |
| WB-11 | `specs/wb11_reading_view_list_semantics_and_cost.md` | The reading view renders the authored list marker, its list peers cost nothing per keystroke, and Ctrl+A is heard |
| WB-12 | `specs/wb12_watcher_noise_and_the_vault_root_stores.md` | Watcher noise and the vault-root stores: a folder's own Modify is dropped, an in-vault export is an echo, graph config refuses a missing root, stale pins are pruned, and Open Recent says what the folder holds |
| WB-13 | `specs/wb13_canvas_outline_delete_seats_the_next_row.md` | Deleting the focused canvas card seats the next outline row instead of dropping the keys on the window |
| WB-14 | `specs/wb14_bases_repair_on_windows.md` | A saved empty or incomplete `.base` can be repaired on Windows: it loads as failed with core's wording, stays editable, and its failed state is mac's group with an Edit button |
| WB-15 | `specs/wb15_shell_seal_witnesses.md` | The shell seal's structural witnesses catch the bypasses codex named, the pointer harness models WPF, and two probes decide I3's reach |
| WB-16 | `specs/wb16_spoken_names_and_the_rail.md` | Every announcement names the item as its list does: the five accepted R-4 risks fixed, the recent-vault distinguisher is the shortest differing ancestor, and the rail stops writing and reloading on every arrow |
| WB-17 | `specs/wb17_docs_and_registers_back_in_step.md` | The Windows registers say what the tree does, and CI checks the two generated tables |
| WB-18 | `specs/wb18_bounded_link_resolution_everywhere.md` | Every link resolution reads its own candidates, and a panicking fault fact poisons nothing |
| WB-19 | `specs/wb19_create_heal_reads_only_the_rows_that_could_name_it.md` | A create heals only the dangling links its path could resolve |
| WB-20 | `specs/wb20_progressive_reading_projection.md` | The reading view shows text, code, math and embeds first; diagrams arrive in place, silently, without moving the reader |
| WB-21 | `specs/wb21_graph_verbosity_rows_and_dead_preamble_modes.md` | The palette has the three graph verbosity rows mac has, and the code-preamble menu offers only the mode that does something |
| WB-21b | `specs/wb21b_code_preamble_verbosity_over_the_token_stream.md` | Core renders the code preamble at three verbosities over the token stream, and Windows speaks the one the reader picked |
| WB-22 | `specs/wb22_find_in_note.md` | Ctrl+F finds in the open note, in the editor and in the reading view: a landmark find bar with next, previous, wrap, case, whole word, editor-only replace and a spoken count, and Ctrl+G steps from the text |
| WB-23 | `specs/wb23_parked_citation_summary_dropped_when_its_note_changes.md` | A parked citation summary is dropped when its note changes |
| WB-24 | `specs/wb24_trash_on_windows_proven_on_a_scratch_volume.md` | Trash on Windows is tested again, trashes a dangling link as the link where the Recycle Bin takes it, and tells the truth in a OneDrive vault |
| WB-25 | `specs/wb25_bounded_ffi_lists.md` | No Windows path marshals a whole-vault list across the FFI: the tag tree is paged and cancellable, and Quick Open ranks against the index |
| WB-26 | `specs/wb26_canvas_board_edge_peers.md` | Labelled canvas edges are UIA peers on the visual board: named by core's traversal phrase, windowed like cards, Invoke total over the end-style table |
| WB-27 | `specs/wb27_no_replace_primitives_proven_on_weak_volumes.md` | 38W: the Windows no-replace primitives are proven on FAT32, exFAT and an SMB share |
| WB-28 | `specs/wb28_descriptor_relative_session_on_windows.md` | 22W: the Windows session holds its vault root and resolves its interior through handles, and its identity is lossless |
| WB-29a | written when its wave starts (W10) | One hot-exit design serves both hosts, and the core store it fixes is on `main` with its facts |
| WB-29 | `specs/wb29_hot_exit_windows_half.md` | Unsaved notes survive closing, quitting and a crash on Windows, and come back unsaved when the vault reopens |
| WB-30 | `specs/wb30_trash_verification_t2_core_and_windows.md` | After a Move to Trash, Slate checks that what arrived is the confirmed item, and says so |
| WB-31 | `specs/wb31_two_sessions_one_process_bench.md` | Two sessions in one process: the shared page cache is reproduced or refuted on Windows, and the remedy waits for the answer |
| WB-32 | `specs/wb32_uniffi_032_spike.md` | The uniffi 0.32 bump is a day's work when the C# generator tags: a local spike against NordSecurity/uniffi-bindgen-cs#176 says exactly what breaks |

## 11. Risks

- **Three chunks carry open design items.** WB-28, WB-29 and WB-30 each
  list Codex round 12's findings in an "Open design items" section that
  outranks the text below it. They are design-first already (22c's
  `FsRoot` names, the shared hot-exit design — WB-29a's since
  2026-10-26 — and PR 34's identity primitive);
  their design reviews must close every item, with its fact, before code.
- **Field-pass time is the bottleneck,** not agent time: ten chunks
  need a full NVDA or Narrator pass and ten a spot check (§6). No wave
  carries more than two full passes.
- **Hardware the probes need may be unavailable** (a VHD the pool cannot
  attach, no SMB share, no Files On-Demand vault). Then the probe's
  facts stay `#[ignore]` and unrun, the issue stays open with a comment
  saying so, and the chunk is not complete; nothing is merged on an
  assumption. A demonstrated unsupported rename (WB-27) is answered
  with a truthful fail-closed refusal, never by weakening the no-replace
  assertion.
- **WB-10 is the largest host chunk** (the design estimates 1,200 to
  1,600 lines; 21a and 20a ran 1.5 to 2× their estimates). It takes a
  full slot for two waves.
- **WB-28 touches the same `FsRoot` surface 22c changes on mac.** Agree
  the `fs_root.rs` `sys` helper names (`open_at`, `stat_at`,
  `read_dir_names`, and whatever 22c adds for the provider's reads) with
  22c before either starts, or the second one rebases a core module.
- **Hardware probes can refuse:** the Recycle Bin may reject a dangling
  link (then WB-24 makes the host's wording truthful instead), and
  OneDrive may redirect a placeholder path (then Trash stays fail-closed
  with a better reason).
- **Four chunks regenerate the C# binding from new FFI** (WB-8, WB-21b,
  WB-25, WB-30): never a dylib with mismatched bindings; the Swift lane
  compiles each.
- **One chunk may edit a mac test file blind** (WB-8's corpus mirror
  entry, under WD23's new-event answer only); the mac lane is the only
  check, so that PR waits for it green before merge — and WB-8 waits for
  the mac lane under either answer, since it compiles the new
  `embed_target_kind` binding. No chunk here changes a sentence mac speaks: the two
  that would have (#1370, #1395) stay in plan 43 with their VoiceOver
  passes (§1).
