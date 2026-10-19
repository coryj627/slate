# 43 — Mac backlog delivery: the program

Status: adopted 2026-10-16. The owner approved the plan, its assumptions
and decisions D6, D8 and D9 as recommended (recorded in §3).
Reassessed 2026-10-19 (§11). The owner paused the queue; §11 records what
was delivered and plans the remaining mac issues with no milestone as
PR 26 onward, plus 21b, 22b and 22c.
Baseline: `main` at 709ba8a2. Every file-and-line reference in this
directory was re-verified against that commit. No file under `apps/` or
`crates/` has changed since plan 41's baseline (c247b865), so plan 41's
§1.3 corrections still hold; the few places where this pass found plan
41 or an issue to be wrong about `main` are listed in §2.

This directory turns `docs/plans/41_mac_backlog_plan.md` §3 (the chunk
list) into one delivery specification per pull request, written so that
each spec is a self-contained brief for an implementing agent. Plan 41
stays authoritative for the triage (§1), the owner decisions D1 to D5
(§5) and the housekeeping record (§6). Where this program and plan 41 §3
differ, this program wins; on adoption, plan 41's status line gets a
pointer here.

## 1. Scope

40 open issues with no milestone that are mac-specific or carry a mac
half: plan 41's 38 plus the two it filed, #1332 and #1333. The other 38
open unmilestoned issues (33 Windows-host, 5 no-action) are excluded for
the reasons in plan 41 §1.2 and were not re-triaged.

| Group | Issues |
|---|---|
| a11y-inspect findings and the sync panel | #445 #446 #447 #1103 |
| Canvas register close-out | #961 #1165 #1166 #1167 #1168 #1169 #1170 |
| Graph register close-out | #1216 #1217 #1218 #1219 #1220 |
| Palette and search overlay convergence | #1105 #1113 |
| Core-owned copy and outcomes on mac | #1053 #1286 #1122 #1140 #1147 |
| Cancellation discipline | #1287 #1324 |
| Graph authority model | #1189 #1190 |
| Legacy bugs and the quit residual | #784 #785 #786 |
| AT structure for VoiceOver | #1048 #1224 |
| Sidebar shortcut slots | #1332 |
| Core infrastructure with a mac half | #940 #944 #1125 #1267 #1293 #1295 #1333 |

## 2. Corrections found in this pass

- **No delta ledger exists.** #1333 and plan 41 PR 19 cite
  `crates/slate-core/src/scan_delta.rs` and a "retained delta ledger".
  Neither exists on `main`. Core's rescan is `rescan(cancel)` /
  `rescan_with_progress(cancel, listener)` returning a `ScanReport`
  (`crates/slate-uniffi/src/lib.rs:1313-1329`), and the Windows host
  re-synchronizes every surface from the index afterwards, comparing open
  tabs through `indexed_content_hashes(paths, cancel)` (`lib.rs:1335`).
  That is contract R-9's actual shape ("it retains nothing"), and PR 19's
  spec is written to it.
- **#1332 had no PR in plan 41.** It is PR 25 here and needs owner
  decision D6 (§3).
- **The rename-preview grid is built in `BulkRenameSheet.swift:107`**
  (`AccessibleDataGrid<RenameGridRow>(columns:rows:summary:)`), not where
  plan 41 PR 1 implied; it is the only grid that relies on the
  rename-specific default label. The other seven construction sites pass
  their own label, so making the parameter required touches two files.
- **`canvasMediaClass` and `fileTypeNotOpenable` already reach mac.**
  The core export (`lib.rs:12993`), the Swift binding
  (`slate_uniffi.swift:36007`, `:23058`) and the two corpus rows
  (`tests/fixtures/a11y/corpus.json:2258`, `:2263`) exist; the mac corpus
  census already lists both events (`A11yCorpusCensusTests.swift:515-516`).
  PR 2 consumes them; no vocabulary work.
- **The template "created" half of #1122 is already converted**
  (`AppState.swift:23688` posts `.templateNoteCreated`). Only the
  picker-open site remains host-composed.
- **No Swift test calls `resolveEmbed(`**, so #1287's "update the Swift
  test call sites" is empty; PR 11 adds facts instead.
- **Ten never-cancelled token sites**, not the issue's list:
  `AppState.swift:9936`, `:11506`, `:16148`;
  `Graph/GraphDiagramView.swift:1352` (a test-only helper, not in the
  issue); `Bases/BaseDocument.swift:248`, `:512`;
  `Bases/BaseEmbedDocument.swift:502`, `:958`;
  `Bases/DashboardDocument.swift:385`;
  `Canvas/AppState+CanvasCreate.swift:155`. `FileTreeSidebar.swift` has
  none.
- **Every PR in this program runs the Windows lane.** `windows.yml`
  triggers on `apps/slate-mac/**` and `docs/plans/**` (its censuses read
  the mac sources and the plan documents), so a mac-only PR runs
  `swift-tests`, `a11y-check` and `windows`; a core PR adds `rust`. Batch
  pushes accordingly (see §7).

## 3. Decisions

Decided in plan 41 §5 and unchanged: D1 (converge the graph authority
model to Windows), D2 (implement ⌘Return in the search overlay), D3
(#784 retitled to the quit residual), D4 (#1125: capability matrix before
any policy call), D5 (#1048: spike, then research, then shape).

Decided at adoption, 2026-10-16, as recommended:

- **D6 (#1332, PR 25): converge to the Windows slot model.** Two shortcut
  slots on one note are two rows with composed spoken names, survive a
  rename in order, and removal names the slot; the adversarial-repetition
  bound is a decode cap, not a collapse (the G20/G22/G29 posture).
  Rejected: recording the mac collapse as an accepted divergence.
- **D8 (PR 4): the graph menu items live in a View ▸ Graph submenu**
  (Open Graph, the three presets, a Verbosity submenu), because mac has
  no custom top-level menus today (every item sits in a standard menu
  through `CommandGroup`) and one submenu gives the Windows Graph menu
  (C-12) a single mac home. Rejected: a top-level Graph `CommandMenu`.
- **D9 (PR 9): core owns the unindexed-create caveat**, as an `A11yEvent`
  beside the save twin that already exists (`a11y.rs:3338`, "Saved, but
  not indexed yet: …"), consumed by both hosts in the same PR with
  byte-identical wording so Windows retires its host-composed
  `CreateOutcomes.PublishedUnindexedCaveat`. Rejected: a mac
  host-composed twin pinned lock-step with the Windows string.

## 4. Cross-cutting rules (every PR)

These are the maintainability, reliability, accessibility and parity
rules the specs assume. A PR that cannot meet one says so in its
description and the register records why.

1. **Core owns copy.** No new host-composed sentence without a
   `// W0.5-3 residue: <owner>` marker and a justified
   `A11yResidueCensusTests` delta; prefer a core event, rendered through
   `a11yRender`. Strings that must stay host-composed are pinned
   lock-step with their Windows twin (the SD10 golden pattern).
2. **Never silent.** A verb that cannot act says why, through the corpus
   arm the other host uses. Speak only what published (a superseded or
   cancelled result says nothing).
3. **One token per operation**, tied to its task or owner, cancelled
   through `withTaskCancellationHandler` or the owner's teardown; no
   `cancel: CancelToken()` outside PR 12's reviewed allowlist. Core calls
   that scan, list or query run off the main actor (locked 05 §4.1).
4. **Parity by registry and register.** Chord and command parity is the
   invariant, not label placement. Every PR closes its register rows in
   the contracts documents in the same change, the way the Windows
   close-outs did, and updates `w_c_matrix.md` /
   `parity_matrix.md` where the surface is listed.
5. **Twin facts.** Swift facts mirror the Windows fact names where a twin
   exists, so the matrix shows twin coverage; a fact that pins a frozen
   design is rewritten with the owner's recorded call before the code
   changes (the TGC-17 process rule: read and simulate every frozen fact
   that touches a changed member before pushing).
6. **Accessibility gates.** `a11y-check` stays at 100. Anything that
   changes what VoiceOver speaks or how it navigates gets a VoiceOver
   field pass (`scripts/vo.sh` with
   `docs/runbooks/voiceover-feature-test.md`) recorded in the PR; XCTest
   proves attributes, not speech.
7. **FFI changes regenerate both bindings** (`make regenerate-bindings`,
   `make regenerate-bindings-windows`) and keep both native lanes green;
   never a dylib with mismatched bindings.
8. **Scope.** One PR closes its issues and nothing else. A defect found
   on the way is filed, not fixed in passing, unless it blocks the PR.

## 5. The chunks

Sizes: S under 300 changed lines, M 300 to 1500, L over 1500 or
design-first. "Lanes" lists what CI runs; "Parallel with" lists PRs whose
files are disjoint enough to develop concurrently (rebases still
expected inside a wave because many touch `AppState.swift`).

| PR | Spec | Issues | Size | Lanes | After | Parallel with |
|---|---|---|---|---|---|---|
| 1 | `specs/pr01_a11y_inspect_closeout.md` | #446 #447 #445 #1103 | S | swift, a11y, windows | — | 2 to 7 |
| 2 | `specs/pr02_canvas_activation_and_names.md` | #1169 #1170 #1168 #1165 | S–M | swift, a11y, windows | — | 1, 3, 5 to 7 (shares `SlateMacApp.swift` / `SlateCommands.swift` with 4) |
| 3 | `specs/pr03_canvas_state_and_paint_order.md` | #961 #1166 #1167 | M | swift, a11y, windows | — | 1, 2, 4 to 7 |
| 4 | `specs/pr04_graph_menu_parity.md` | #1219 #1216 | S | swift, a11y, windows | 2 | 1, 3, 6, 7 (shares menu files with 2, graph files with 5) |
| 5 | `specs/pr05_graph_behaviours.md` | #1217 #1218 #1220 | M | swift, a11y, windows | — | 1 to 3, 6, 7 |
| 6 | `specs/pr06_palette_home_end_page.md` | #1105 | S–M | swift, a11y, windows | — | 1 to 5, 7 |
| 7 | `specs/pr07_search_overlay_convergence.md` | #1113 | M | swift, a11y, windows | — | 1 to 6 |
| 8 | `specs/pr08_core_rendered_copy.md` | #1053 #1286 #1122 | M | swift, a11y, windows | — | 9 to 12 (all touch `AppState.swift`; rebase) |
| 9 | `specs/pr09_create_exclusive_reporting.md` | #1140 | M | swift, a11y, windows, rust | — | 8, 11 |
| 10 | `specs/pr10_canonical_identity_i6_i8.md` | #1147 | M–L | swift, a11y, windows | 9 | 8, 11 |
| 11 | `specs/pr11_resolve_embed_cancel_token.md` | #1287 | M | rust, swift, a11y, windows | — | 8, 9, 10 |
| 12 | `specs/pr12_one_token_per_operation.md` | #1324 | M | swift, a11y, windows | 11 | — (touches every site; last in wave 2) |
| 13 | `specs/pr13_graph_authority_model.md` | #1189 #1190 | M | swift, a11y, windows | 5 | 14, 15, 15b, 25 |
| 14 | `specs/pr14_rename_keeps_its_row.md` | #785 | S | swift, a11y, windows | — | 13, 15, 15b, 25 |
| 15 | `specs/pr15_failed_base_stays_editable.md` | #786 | M | swift, a11y, windows | — | 13, 14, 15b, 25 |
| 15b | `specs/pr15b_quit_with_unsaved_edits.md` | #784 | S–M | swift, a11y, windows | — | 13, 14, 15, 25 |
| 25 | `specs/pr25_sidebar_shortcut_slots.md` | #1332 | M | swift, a11y, windows | — | 13, 14, 15, 15b |
| 16 | `specs/pr16_reading_view_list_semantics.md` | #1048 | L | swift, a11y, windows + VoiceOver pass | D5 steps | 17 |
| 17 | `specs/pr17_editor_span_semantics.md` | #1224 | M–L | swift, a11y, windows + VoiceOver pass | — | 16 |
| 18 | `specs/pr18_save_resolves_own_links.md` | #1295 | S–M | rust, swift, windows | — | any |
| 19 | `specs/pr19_mac_rescan_baseline.md` | #1333 | M–L | swift, a11y, windows | 10, 12 | 18, 23, 24 |
| 20 | `specs/pr20_vault_watcher.md`; split by its design (`20_vault_watcher_design.md` §7) into 20.0 (core link pass, #1429, delivered), 20a (core watcher and FFI, delivered), 20b (Windows host, delivered) and 20c (mac host, delivered) | #1267 | L (design first) | rust, swift, windows | 19 | 24 |
| 21 | `specs/pr21_change_feed.md`; split by its design (`21_change_feed_design.md` §7) into 21a (core and FFI, delivered), 21b (Windows; the next wave, §11.4) and 21c (mac host, delivered) | #1293 | L (design first) | rust, swift, windows | 20 | 24 |
| 22 | `specs/pr22_descriptor_relative_session.md`; split by its design (`22_descriptor_relative_session_design.md` §7) into 22a (core interior, macOS and Linux, delivered; its watcher re-arm is built and switched off until #1513), 22c (the provider's reads beneath the held root, #1513, which switches the re-arm on; added by §11), 22b (mac host, after 22c) and 22W (Windows, Milestone W) | #940 | L (design first) | rust, swift, windows | 20 | — |
| 23 | `specs/pr23_durable_sidebar_journal.md` | #944 | M–L | swift, a11y, windows | 15b | 18, 19, 24 |
| 24 | `specs/pr24_structural_identity_tracks.md` | #1125 | docs S, then sized | windows (docs) | — | any |

Issue counts at each checkpoint: wave 1 (PRs 1 to 7) closes 18; wave 2
(8 to 12) 25; wave 3 (13 to 15b and 25) 31; wave 4 (16, 17) 33; wave 5
(18 to 24) 40.

## 6. Delivery order

1. **Wave 1, in parallel.** PRs 1, 3, 5, 6, 7 are mutually disjoint. PR
   2 and PR 4 both add menu items and registry entries; land 2 first or
   expect a small rebase in 4. Nothing in wave 1 waits on a decision.
2. **Wave 2, mostly sequential merges.** PR 8 and PR 11 can start at
   once. PR 9 at once; PR 10 after PR 9 (its hashless save uses the
   create outcome). PR 12 last, because it sweeps the sites the others
   just touched.
3. **Wave 3, in parallel** after PR 5 (PR 13 shares `AppState+GraphTable.
   swift` with PR 5's persistence fix): PRs 13, 14, 15, 15b, 25.
4. **Wave 4.** PR 16 and PR 17 each carry a spike and a VoiceOver session
   before merge; they share no files.
5. **Wave 5.** PR 18 any time. PR 19 after PR 10 and PR 12. PR 24's
   capability matrix any time. PR 23 after PR 15b (both touch the
   termination fence). PRs 20, 21, 22 are design-first core programs:
   each opens with a design document in this directory, reviewed before
   implementation, and they run in the order 20, then 21 and 22.

## 7. Verification lanes and local loop

- `swift-tests.yml`: XCTest on the Mac Studio runner (Namespace Tahoe when
  the Studio is offline), about two minutes warm. Locally:
  `./scripts/build-mac-app.sh --skip-a11y-check`, then from
  `apps/slate-mac`: `DYLD_LIBRARY_PATH="$PWD/../../target/debug" swift
  test --parallel` (CI's shape) or `swift test --filter <TestClass>` while
  iterating.
- `a11y-check.yml`: the static analyzer over `apps/slate-mac/Sources/
  SlateMac`, floor 100; the README's brew tap installs it locally and
  `scripts/build-mac-app.sh` runs it without `--skip-a11y-check`.
- `rust.yml`: `make ci` locally (fmt, clippy with warnings as errors,
  tests, bench compile, license headers) for any change under `crates/`.
- `windows.yml`: runs on every PR here (§2). For an FFI change, regenerate
  with `make regenerate-bindings-windows` and build with `dotnet build
  apps/slate-windows/SlateWindows.slnx --configuration Release` when a
  Windows machine is available; otherwise let the lane prove it.
- VoiceOver field pass: `scripts/vo.sh` and the runbook; record the
  spoken phrases in the PR description for PRs 7, 16, 17 and any PR that
  changes a label VoiceOver reads.
- Proportionality (owner rule): a tiny change on a green PR runs the lane
  for the files it touched, not a full re-run before merge; batch
  follow-up pushes.

## 8. How an implementing agent works a spec

Each spec ends with a "Brief". The brief is the only context the agent
needs, together with this section.

- Work in a fresh worktree on a `claude/<spec-slug>` branch from `main`.
- Read the spec's "Current state on main" and "Windows reference" files
  before writing code; if a line number has drifted, say so in the PR and
  continue from the function names.
- Write the failing fact first for every behaviour the spec lists under
  "Tests"; keep the names the spec gives (they mirror the Windows twins).
- Close the register rows the spec names in the same PR; never edit a
  frozen contracts record except to append the owner's recorded call the
  spec quotes.
- Commit messages: `type(scope): one lower-case sentence saying what is
  now true` (the repo's convention, e.g. `fix(mac): the media card's
  activation hint says what activation does`), with the harness's
  attribution line. PR titles match; PR bodies list the issues with
  `Closes #N`, the facts added, the registers closed, and the VoiceOver
  phrases where a pass was required.
- Run only the lanes the change touches; push once per review round.
- Report what was not done or not verified in the PR body, in plain
  words. A spec item the agent cannot deliver is left in the PR body as
  "not delivered: <reason>", never silently dropped.

## 9. Risks

- Mac-lane cost: every PR also runs the Windows lane; batch pushes.
- PR 11 changes an FFI signature; PR 9 does too (D9). Both native
  lanes must stay green with regenerated bindings.
- PR 13 rewrites frozen facts. The TGC-17 record shows the exact failure
  mode (a fact that waits on a gate the new design never reaches hung the
  Swift lane for five hours); the spec sequences the fact rewrite first.
- PRs 16 and 17 depend on what VoiceOver honours, which only a field pass
  proves; both specs carry a spike with a decision gate.
- PRs 19, 20, 22 all touch root identity and rescans; keep them
  sequential.
- PR 15b and PR 23 both change the termination fence; keep them
  sequential.

## 10. Adoption record

- 2026-10-16: adopted with D6, D8 and D9 as recommended; plan 41's status
  line points here and its §3 is superseded; every in-scope issue carries
  a comment naming its spec. This program creates no issues.
- Delivery: one implementing session per PR, from the spec's "Brief"
  and §8, in the §6 order.
- 2026-10-19: the owner paused the queue once the in-flight PRs had
  merged, for a reassessment. §11 records the delivery, the lessons
  (now rules), the remaining scope and the next wave, and lists the
  owner decisions that wave needs (RD1 to RD19).

## 11. Delivery record and reassessment (2026-10-19)

The owner paused the queue on 2026-10-19 once the in-flight PRs had
merged, to take stock of what this program delivered and to plan the
remaining mac issues that have no milestone. The per-issue evidence
behind this section (every candidate read in full and checked against
`main`) is in `90_reassessment_2026-10-19.md`.

### 11.1 What was delivered

- **The original scope.** Of the 40 issues in §1, 37 are closed. Three
  remain open, each for a known reason:
  - #445, the a11y-inspect summary. Its findings, #446 and #447, closed
    through #1374, but no scan has run since June to reconcile the
    summary. RD16 recommends closing it by hand.
  - #940, the descriptor-relative session. Its core landed in 22a
    (#1518); the mac host half is 22b, and the Windows body (22W) waits
    for Milestone W.
  - #1293, the change feed. Its core landed in 21a (#1512) and its mac
    host in 21c (#1520); only the Windows host, 21b, remains.
- **About 53 PRs merged** from adoption (2026-10-16) to the pause. That
  covers PRs 1 to 19, 23, 24 and 25, and the design series:
  - 20.0, 20a, 20b and 20c, the vault watcher (#1267 closed);
  - 21a and 21c, the change feed;
  - 22a, the descriptor-relative session.
  The pinned-root boundary fixes (#1449, #1450, #1451, #1452, #1477 and
  #1495) and the test and tooling fixes (#1442, #1455, #1460's
  tripwire, #1500, the Windows docs trigger) also merged.
- **Every merge was pinned.** Each used `--match-head-commit`, and
  `main`'s tree was compared with the tested tree, or with a clean merge
  of it when `main` had only moved in disjoint files.
- **Rule 8 worked, and the backlog grew.** Defects found on the way
  were filed, not fixed in passing: about 105 issues have been opened
  since adoption, 79 of them still open (many Windows-only). So the mac
  backlog with no milestone went from 40 issues to 67.

### 11.2 Lessons, recorded as rules for the next wave

These extend §4, and every brief in the next wave carries them.

1. **Facts that cannot run locally get a mutation probe.** This Mac has
   no Windows toolchain, so a Windows fact is never seen to fail before
   it merges. Before merging such a PR, push the change with its
   behaviour switched off to a throwaway draft PR or a scratch branch,
   show that the new facts fail, then delete the probe. #1509 did this
   for #1506: 13 of 17 facts failed and every other test passed.
2. **No accidental closing keywords.** GitHub closes an issue when a
   closing word (close, fix or resolve, in any form) stands before its
   number, even mid-sentence. #1460 closed by accident this way and was
   reopened. A PR that only references an issue keeps every such word
   away from the number. Before merging, check `gh pr view N --json
   closingIssuesReferences`.
3. **What publishes on its own is scanned right after it publishes.**
   The watcher made the Windows tree's wholesale row replacement visible:
   a screen reader could hear a row named after its class (20b). A
   surface that a watcher, a feed or a timer can republish carries a
   fact that the reader's element and focus survive the publication,
   plus an accessibility scan right after it (#1507, #1523).
4. **A known defect in a PR's own feature** is fixed on its branch first
   (the owner's rule). The owner may choose otherwise case by case, and
   two precedents are recorded:
   - switch the defective part off behind one switch and merge the rest
     (22a's re-arm, off until #1513);
   - merge with a filed follow-up that runs next (20b, then #1515).
5. **Back-to-back merges that touch the same files** were never tested
   together. Wait for `main`'s own CI on the combined head before
   building on it. #1517 and #1507 merged two minutes apart; `main` went
   green at d5128eb5.
6. **Windows lanes and the home runner.** If a Windows job sits queued
   for more than 20 minutes on a `slate-win-*` label, cancel the run and
   use "Re-run failed jobs", which routes to Namespace.
   - A stuck older run on `main` holds the workflow's concurrency group,
     because push runs don't cancel each other. On 2026-10-19 one did,
     for 4 hours 20 minutes; cancelling it freed `main`'s run.
   - The infrastructure side is #1482.
7. **VoiceOver passes need the owner's unlocked screen.** Run the owed
   passes back to back while it stays unlocked, under the shared UI
   lock.
   - Read what VoiceOver actually spoke from its speech-queue log, not
     `last phrase`.
   - A PR whose feature changes what VoiceOver says stays a draft until
     its pass is recorded.
8. **Sizes run large.** 21a was about 4,900 lines against an estimate
   of 2,000 to 2,600, and 20a was about 4,000. An L chunk is planned as
   a full slot for its whole wave.

### 11.3 The remaining scope

There are 75 open issues with no milestone that are not obviously
Windows-only. Each was read in full and checked against `main` at
b63f6dbb, and 67 of them are mac-relevant:

| Scope | Count |
|---|---|
| mac-only | 32 |
| both hosts | 7 |
| core with mac impact | 12 |
| process, docs, deps or test infrastructure | 16 |

The other eight leave the mac plan:
- **Windows-only**, to Milestone W or the Windows backlog: #1051,
  #1480, #1492 and #1516.
- **Core-only, with no mac impact today:** #640, which is already
  delivered (close it), and #1484, which goes to the core performance
  backlog.
- **No action:** #445 (close it) and #642 (an l10n tracking umbrella;
  it stays open and unscheduled).

Of the 67:
- **#1474 can close now that #1518 has merged (RD18).** Its leftovers
  are #1513 on macOS and Linux, and 22W and #1475 on Windows.
- **Four go on a watch list**, because each is blocked by an upstream
  release:
  - #480: a registry release of citationberg;
  - #998: a uniffi-bindgen-cs build for uniffi 0.32;
  - #1457: MathCAT taking zip 9. The audit exception's review-by date
    is **2026-11-17**, so it needs renewing or upgrading before then;
  - #1493: notify 9.0.0.
- **One is a feature:** #1414 (copy a canvas media card). It goes to a
  milestone (RD14).
- **The other 61 fall into the 31 PRs below.** They close 59 issues;
  #940 moves to Milestone W after 22b, and #1323 is narrowed to its
  Windows items.

What matters most:
- **Security:** canvas link and file cards hand untrusted targets to the
  system opener (#1367, #1368).
- **Data integrity:**
  - an undo before the editor's first highlight is lost and clears the
    history (#1503);
  - closing the main window leaves unsaved edits behind no window
    (#1402);
  - the two decided #1125 tracks, identity-checked undo (#1469) and
    Trash verification (#1468).
- **An accessibility blocker:** Quick Open's bare arrow keys don't move
  its selection (#1379).

### 11.4 The next wave

The numbering continues from §5 (PR 26 onward). The three remaining
design-series PRs keep their names. 22c is new: PR 22's follow-up for
#1513. Each PR gets its spec under `specs/` when its wave starts, as the
first waves did. Sizes use the triage's scale: S is under about 150
changed lines with facts, M is 150 to 1,000, and L is over 1,000 or
design-first.

| PR | Title | Issues | Size | Lanes | Wave |
|---|---|---|---|---|---|
| 26 | Canvas: untrusted link and file targets (one link allowlist shared with notes and the reading view; containment that resolves symlinks against the vault root) | #1367 #1368 #1369 | M | swift, a11y | W1 |
| 27 | Editor: an undo or redo before the first highlight reaches the buffer | #1503 | S–M | swift, a11y/VO | W1 |
| 28 | Tests stop writing the developer's files; two flakes; the benchmark command | #1423 #1408 #1432 (Refs #1323) | S–M | swift | W1, slot 3 first |
| 29 | Unsaved buffers survive closing the window, quitting and a crash, and come back unsaved when the vault reopens (hot exit; explicit save stands; design first, both hosts in scope) | #1525 #1402 | L (design first) | rust, swift, a11y/VO, windows | W1, slot 3 after 29a (design), then sized by its design (RD1) |
| 29a | Close Vault with one dirty note speaks its close line | #1403 | S | swift, a11y/VO | W1, slot 3 after 28 |
| 22c | The provider's reads beneath the held root; the re-arm switched on; the FFI doc | #1513 #1510 | L | rust, swift, windows | W2, first |
| 30 | Palette and Quick Open keyboard (bare arrows, the key monitors' window gate, hover on open, the field hint) | #1379 #1380 #1413 #1382 | M | swift, a11y/VO (+ rust, windows with RD7) | W2 |
| 21b | Windows host of the change feed | #1293 | L | windows | W2 (Windows files only) |
| 22b | Mac host half of the descriptor-relative session | #940 (Refs) | S–M | swift, a11y | W3, after 22c |
| 31 | Core: another session's link changes reach the graph | #1519 | S–M | rust, swift, windows | W3, after 22b in its slot; ideally before 21b's graph facts |
| 32 | Canvas outline: VoiceOver's press, and the file-card hint by target | #1411 #1370 | S–M | swift, a11y/VO (+ rust, windows with RD7) | W3 |
| 33 | Canvas: failure speech through core, reload selection, connection paint order | #1364 #1363 #1365 | M | swift, a11y/VO | W3, after 26; needs RD2, RD3, RD8 |
| 34 | Identity-checked structural undo (#1125 decision 2) | #1469 | L | rust, swift, windows | W4, after 22c |
| 35 | Bases: failure speech, pane targeting, unreadable YAML | #1399 #1400 #1398 | M | rust, swift, a11y/VO, windows | W4; needs RD5, RD8 |
| 36 | Sidebar: selection after mutations, shortcut verbs never silent, a race-safe lock | #1407 #1409 #1405 #1521 | M | swift, a11y/VO | W4 |
| 37 | Core test infrastructure: the real-Trash facts in the mac lane, a poison-proof fault guard | #1479 #1481 (Refs #1323) | S–M | rust (mac lane) | W5, slot 1 first |
| 38 | No-replace renames on weak volumes (exFAT, SMB, NFS, FAT32) | #1434 #1470 | M | rust, swift | W5, slot 1 after 37 and 34; needs RD9 |
| 39 | Landed-but-unindexed creates, and the caveat's punctuation | #1394 #1395 | S–M | rust, swift, a11y, windows | W5, after 33 |
| 40 | Open-scan and rescan follow-ups | #1462 #1446 | M | swift, a11y | W5 (diagnose #1462 with one fact first) |
| 41 | Trash: verify what arrived (T2) | #1468 | L | rust, swift, windows | W6, after 37 and 34; needs the `~/.Trash` read check on the owner's unlocked Mac |
| 42 | R-9 re-sync, complete on mac (canvas and base tabs, Bases, dashboards, an open Quick Open, the filter, feed renames of every tab kind) | #1445 #1416 | L | swift, a11y | W6, after 30, 33, 35, 36 and 40 |
| 43 | Search overlay: the selection model, and the stray phrase | #1377 #1497 | M | swift, a11y/VO | W6; needs RD4 |
| 44 | Core: bounded link resolution, the rest | #1427 #1485 #1486 | M | rust, swift | W7, slot 1 |
| 45 | Core: the create heal without whole-table reads | #1426 | M | rust, swift, windows | W7, slot 1 after 44 |
| 46 | Embeds: live-buffer keys survive; base-card titles from core | #1393 #1388 #1390 | M | rust, swift, a11y/VO, windows | W7, after 35, 42 and 39 |
| 47 | Generated register tables back in step, and checked in CI | #1376 #1386 | S | windows | W7 (docs; any free slot) |
| 48 | Bases: queries off the main actor | #1436 | M–L | swift, a11y/VO | W8, after 35 and 42; needs RD10 |
| 49 | Static accessibility: a11y-check 0.7.0, and doubled names | #1371 #1372 | M | swift, a11y/VO (spot) | W8; needs RD11 |
| 50 | Organization facts on the async seam | #1490 | M | swift | W8, after 36 and 42 |
| 51 | Reading-view links VoiceOver can press on any SDK | #1460 | L (spike first) | swift, a11y/VO on both builds | W9, and before any move to an Xcode project or `--build-system native`; needs RD12 |
| 52 | Code preamble verbosity: remove the two modes that do nothing, on both hosts (the real modes are #1526) | #1389 | S | swift, a11y/VO, windows | W9 (RD6) |
| 53 | Editor ⌘-click activates in the current tab by default, with a setting for a new tab, honoured by Windows' Ctrl+click too | #1466 | M | swift, a11y, windows | W9, after 27 (RD13) |

**Waves.** At most three delivery agents run at once
(`subagent-parallelism`). PRs in one wave share no files, except
`AppState.swift` (different functions; a merge of `main` is expected)
and core's `a11y.rs` and corpus (independent variants).

| Wave | Slot 1 | Slot 2 | Slot 3 |
|---|---|---|---|
| W1 | 26 | 27 | 28, then 29a, then 29 (design) |
| W2 | 22c | 30 | 21b |
| W3 | 22b, then 31 | 32 | 33 |
| W4 | 34 | 35 | 36 |
| W5 | 37, then 38 | 39 | 40 |
| W6 | 41 | 42 | 43 |
| W7 | 44, then 45 | 46 | 47 |
| W8 | 48 | 49 | 50 |
| W9 | 51 | 52 | 53 |

**Some PRs are sequenced on purpose** because they share files:

| Shared code | Order |
|---|---|
| Canvas action and document files | 26, then 33, then 39 and 42 |
| Bases files | 35, then 42, then 46, then 48 |
| `AppState+Rescan.swift` | 40, then 42 |
| `FileTreeSidebarTests` | 36, then 50 |
| `vault/fs.rs` | 22c, then 34, then 38 |
| The real-Trash lane, and a shared identity primitive | 37 and 34, then 41 |
| Contract 38's generated ledger | 39 and 46 kept apart |

When an S PR frees its slot early, the next PR in the same row takes
it, or 47 does.

**VoiceOver passes run one at a time,** under the shared UI lock on the
owner's unlocked Mac. W1 has three (26, 27 and 29a); plan for them to
run back to back.

### 11.5 Owner decisions for the next wave

The owner answered all nineteen on 2026-10-19; §11.7 records the
answers, which supersede the recommendations below where they differ.

Each has a recommended answer; the evidence is in
`90_reassessment_2026-10-19.md` (f). Only RD1 blocks W1. The rest are
needed by the wave named.

| Id | Issues | Question | Recommended | Needed by |
|---|---|---|---|---|
| RD1 | #1402 | Should closing the main window with unsaved edits ask? | Yes: run the Close Vault prompt (Save All, Discard All, Cancel); Cancel keeps the window. With nothing dirty, keep today's behaviour. This matches Windows and U1-2. | W1 (29) |
| RD2 | #1363 | When a ready publish loses the selected card, drop the selection or re-seat it? | Re-seat, as Windows' `PublishReady` does (contract A12), with no announcement. | W3 (33) |
| RD3 | #1365 | Should coloured connections paint beneath the cards, like plain ones, with label chips above? | Yes to both. | W3 (33) |
| RD4 | #1377 | Search overlay: rows focusable whatever Full Keyboard Access says, or the palette's selection model? | The selection model. Focus stays in the field; the arrows move a spoken selection; Return opens it; ⌘Return opens it in a new tab. Rewrite #1113's facts first. | W6 (43) |
| RD5 | #1398 | How should a `.base` whose YAML doesn't parse be repaired? | Now: core-owned failed-state copy that says the YAML could not be read, on both hosts. Later, as features: a raw-definition editor. | W4 (35) |
| RD6 | #1389 | The code-preamble verbosity modes do nothing: remove them, or build them? | Remove them on both hosts now. File the real modes as a core enhancement. | W9 (52) |
| RD7 | #1382, #1370 | New hint copy: core-owned, or a host residue? | Core-owned, consumed by both hosts in the same PR. | W2 (30), W3 (32) |
| RD8 | #1364, #1399 | Failure details: core's `vault_error_detail`, or mac's own humanizer? | Core's. Add a mac twin of `VaultErrorDetailCensus`, and file the rest of the host (about 90 `localizedDescription` uses) as a follow-up issue. | W3 (33) |
| RD9 | #1434, #1470 | On a volume without `RENAME_EXCL`, refuse or fall back? | Probe first, then refuse truthfully with core copy. Never fall back to a replacing rename. | W5 (38) |
| RD10 | #1436 | Accept that Base executes become asynchronous? | Yes, as Windows does and locked 05 §4 principle 2 requires. | W8 (48) |
| RD11 | #1371 | a11y-check 0.7.0: fix and bump, or pin local installs to CI's ref? | Fix the real findings, waive the false positives with a record, then bump CI and the runner image to 0.7.0. | W8 (49) |
| RD12 | #1460 | When should the SDK 27 reading-link fix be scheduled? | W9, and before any build-system move. The tripwire keeps shipping builds safe until then. | W9 (51) |
| RD13 | #1466 | Does ⌘-click in the editor act in the current tab or open a new one? | The current tab, matching ⌘↩ and G22. | W9 (53) |
| RD14 | #1414 | Schedule canvas copy now, or move it to a milestone? | Move it to a feature milestone. | housekeeping |
| RD15 | #940 | After 22b, close #940, or keep it for 22W? | Keep it, and move it to Milestone W with a comment. | after 22b |
| RD16 | #445 | Close the stale a11y-inspect summary by hand? | Yes. | housekeeping |
| RD17 | #1323 | Keep #1323 as a mixed-host list? | No. Fix its mac and Rust facts in 28 and 37 (Refs), then retitle it as the Windows flakes list. | W1, W5 |
| RD18 | #1474 | Close it at #1518's merge, or keep it until #1513? | Close it at the merge, pointing to #1513, 22W and #1475. | at #1518's merge |
| RD19 | plan docs | Where does this wave live? | Here (§11), with a spec per PR written when its wave starts. | this PR |

### 11.6 Status

- **All nineteen decisions are answered** (§11.7). The issues each one
  touches carry a comment recording its answer.
- **Done the same day:**
  - #445, #640 and #1474 are closed (RD16 and RD18);
  - #1414 has moved to the new Milestone CV, Canvas follow-up features
    (RD14);
  - the follow-ups the answers call for are filed: #1525 (hot exit,
    RD1), #1526 (core preamble verbosity, RD6) and #1527 (the rest of
    the host's failure text, RD8).
- **The queue stays paused** until the owner says to start W1, which
  is 26, 27, 28, 29a, then 29's design.
- **Dated item:** the zip exception (#1457) must be renewed or upgraded
  before 2026-11-17.

### 11.7 Owner answers (2026-10-19)

The answers came from a decision interview. Two differ from the
recommendation and widen the plan: RD1 and RD13.

| Id | Answer | What it changes |
|---|---|---|
| RD1 | **Unsaved buffers are restored ("hot exit"),** not the recommended close prompt. Closing the window or quitting never asks and never writes the notes. Unsaved buffers are kept outside the vault, written as you type so a crash keeps them too, and come back still marked unsaved when the vault reopens, as in other text editors. Explicit save (#877) stands, with its conflict checks and per-save history. Close Vault still asks. | PR 29 becomes design-first (#1525, closing #1402); the design decides whether core owns the store and whether Windows adopts hot exit too. #1403 moves to PR 29a. #877 carries the amendment. |
| RD2 | Re-seat, as Windows does (contract A12). | PR 33 |
| RD3 | Yes to both: coloured connections paint beneath the cards, and chips stay above. | PR 33 |
| RD4 | The selection model, matching the palette, Quick Open and Windows. | PR 43 rewrites #1113's facts first |
| RD5 | Explain now, with core-owned copy on both hosts; edit later. | PR 35 |
| RD6 | Remove the dead modes on both hosts now. | PR 52; #1526 keeps the real modes |
| RD7 | Core-owned hint copy, consumed by both hosts in one PR. | PRs 30 and 32 |
| RD8 | Core's `vault_error_detail`, with mac censuses for `Canvas/` and `Bases/`. | PRs 33 and 35; #1527 covers the rest of the host |
| RD9 | Probe, then refuse truthfully; never fall back to a replacing rename. | PR 38 |
| RD10 | Yes: Base queries go asynchronous. | PR 48 |
| RD11 | Fix and bump a11y-check to 0.7.0, with recorded waivers. | PR 49 |
| RD12 | W9, and before any move to an Xcode project or `--build-system native`. | PR 51 |
| RD13 | **The current tab by default, plus a setting** to open a new tab instead. | PR 53 adds the setting; for mac/Windows consistency it also covers Windows' Ctrl+click |
| RD14 | Move it to a feature milestone. | #1414 is in Milestone CV |
| RD15 | Keep #940; move it to Milestone W after 22b. | after 22b |
| RD16 | Close #445 and #640. | done 2026-10-19 |
| RD17 | Split #1323. | PRs 28 and 37 fix its mac and Rust facts; then it is retitled for Windows |
| RD18 | Close #1474 now. | done 2026-10-19 |
| RD19 | Keep the plan in §11, with a spec per PR written when its wave starts. | — |
