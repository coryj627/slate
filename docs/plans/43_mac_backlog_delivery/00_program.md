# 43 — Mac backlog delivery: the program

Status: adopted 2026-10-16. The owner approved the plan, its assumptions
and decisions D6, D8 and D9 as recommended (recorded in §3).
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
| 20 | `specs/pr20_vault_watcher.md`; split by its design (`20_vault_watcher_design.md` §7) into 20.0 (core link pass, #1429, delivered), 20a (core watcher and FFI, delivered), 20b (Windows host) and 20c (mac host, delivered) | #1267 | L (design first) | rust, swift, windows | 19 | 24 |
| 21 | `specs/pr21_change_feed.md`; split by its design (`21_change_feed_design.md` §7) into 21a (core and FFI, delivered), 21b (Windows, after 20b) and 21c (mac, after PR 19 and 20c) | #1293 | L (design first) | rust, swift, windows | 20 | 24 |
| 22 | `specs/pr22_descriptor_relative_session.md` | #940 | L (design first) | rust, swift, windows | 20 | — |
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
