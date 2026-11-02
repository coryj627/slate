# 45 — Windows and core follow-ups after plan 44: delivery plan

Status: draft for the owner's review, 2026-10-23; per-chunk specs added
the same day (§10, `45_windows_followups/specs/`, sixteen, one per PR,
every file-and-line claim read from the tree). Not codex-reviewed. Not
adopted; nothing in this plan has started. Baseline: `main` at 2463ee7a (#1599). Companion to
`44_windows_backlog_plan.md`, which plans the Windows and host-neutral
backlog as it stood on 2026-10-21. This plan takes the open issues in
#1475–#1596 that plan 44 does not cover and that a Windows machine can
deliver and verify, and leaves every issue plan 44 already names to plan
44 (the owner's instruction, 2026-10-23: "where plan 44 already covers the
item, leave that item to plan 44"). §9 lists those pointers so the
Windows queue sees them in one place.

Every issue below was read in full, body and comments, and its cited
code checked against the tree at 2463ee7a.

## 1. Scope

**In:** 22 open issues, none of them in plan 44, in five kinds:

| Kind | Issues |
|---|---|
| Windows host (`apps/slate-windows`) | #1573 #1590 (one defect) #1581 #1595 #1591 (its Windows half) #1586 #1594 #1574 #1572 |
| Core, host-neutral or Windows-cfg (`crates/`) | #1570 #1571 #1576 #1580 #1583 #1585 |
| CI workflows and runner images (`.github/`, `ci/`) | #1568 #1575 #1596 #1565 (what remains) #1564 |
| Repository rules (`AGENTS.md`) | #1569 |
| Field-pass close-out | #1562 |

**Left to plan 44 (not re-planned here; §9 has the chunk for each):**
#1475 #1480 #1481 #1484 #1485 #1486 #1492 #1493 #1499 #1505 #1508 #1510
#1515 #1516 #1519 #1522 #1525 #1526.

**Out, and why:**

- **Mac-only, or needs a Mac to verify:** #1479 (the mac lane's APFS
  image), #1490 (Swift test seams), #1497 (VoiceOver phrase), #1503
  (TextKit undo), #1513 (the held root is `cfg(any(macos, linux))`; plan
  44 §1 already says so), #1521 (`openat` in `SidebarVaultPrefsStore`),
  #1527 (`humanReadableVaultError`), and #1591's mac half (the
  `AXCustomContent` order; plan 43's queue). #1591's Windows half is in
  (XB-9).
- **A person's work, not an agent's:** #1560 asks a braille transcriber
  for a verdict. Its ask 2 (the upstream reports) is done; ask 3 (a
  correction with its golden diff) becomes a core PR here once a verdict
  arrives (§8).

**Fixed already, close after a verification run (§2.1):** #1572.

**Merged since this plan was written:** #1569 with #1565's step 3,
as #1605 (1aeb0115, `AGENTS.md` only, 2026-10-23).

## 2. What the reading found

### 2.1 Already fixed on `main`

- **#1572** (censuses fail to compile their fixtures in a filtered run).
  #1601 (f267867b, "the census compilation binds against the build's
  recorded references, so no test order can leave wpf unbound") rewrote
  `Support/ShellCompilation.cs` to take its references from the build's
  recorded reference list instead of `AppDomain.CurrentDomain.GetAssemblies()`.
  That is the fix #1572 asks for ("reference the shell's referenced
  assemblies explicitly (the evidence-compilation manifest already
  records them)"). #1601 closed #1600 and never mentions #1572. Left:
  run the issue's own reproduction on `main`,
  `dotnet test … --filter "FullyQualifiedName~AtNavigationMapCensus"`
  alone in a fresh process, and close #1572 with a comment pointing at
  f267867b and the run's result.

### 2.2 Corrections to the issues

- **#1573 and #1590 are the same defect**, filed four days apart from
  the same CI failure (`PumpedSaveReentrancyTests` on #1555): the
  150 ms debounced refresh after `HandleFileChange`
  (`VaultLifecycleViewModel.cs:859`) and the refusal in
  `TryCloseWorkspaceCore` (`:1485-1488`). One PR closes both (XB-11).
  #1590 carries the a11y label and the three options; the plan reads
  them together.
- **#1493's text is stale.** It says the pin is `=9.0.0-rc.5`; #1553
  (ed01221e) moved it to rc.6 (`Cargo.toml:125-132`). crates.io's newest
  is still 9.0.0-rc.6 (checked 2026-10-23), so plan 44 §8's watch row
  stands; XB-0 corrects the issue's note so the eventual bump reads the
  right starting point.
- **#1575 is provable from here.** The job that downloads the results
  and computes the score runs on `ubuntu-latest`
  (`a11y-check.yml:187`), and the workflow's `paths` filter lists its
  own file, so a PR touching it runs the whole workflow, the mac
  analysis job included. No Mac is needed to deliver or prove the fix;
  the PR's own run is the proof.
- **#1562's recorded cells pass.** The owner's comment of 2026-10-23
  records 96/96 speech, 32/32 braille and 16/16 UI Automation cells
  equal to the golden, and files the two failures of the raw-LaTeX
  criterion as #1595 and #1591. Whether #1562 closes on that record,
  with #1595 and #1591 carrying the criterion, is XD8.
- **#1594's cause is not known.** The fact's drain did time out and no
  direct wake was recorded; which path woke the loop was not
  investigated. XB-2 is a diagnosis chunk, kept apart from the
  known-cause test fix (#1586, XB-1) so a wrong guess cannot hold a
  one-line fix hostage.
- **#1591's Windows comment settles the channel.** NVDA reads
  `ReadingMathElement`'s HelpText (`ReadingMathElement.cs:29-33`) as the
  element's description on every Tab landing. The decision (what the
  first on-request channel for the source is) is shared with the mac
  half; the Windows implementation stands alone, and XB-9 carries it
  with #1595 because both change the same two files.
- **#1574's cooldown is already satisfied for SkiaSharp.** 4.153.1 is
  dated 2026-09-30, more than 7 days before any PR here opens.
  HarfBuzzSharp 14.2.0's date is read from NuGet when XB-4b opens.
  `SlateWindows.csproj:31` references only `Svg.Skia 5.2.3`; the two
  native packages are transitive today, which is what the issue asks to
  change.

### 2.3 Where this plan meets plan 44

Plan 44 has not started (its W0 is not begun). The chunks here are small
and most touch files plan 44's early waves also touch, so the cheapest
order is this plan's X0–X3 before plan 44's W1, and the three
reader-facing chunks (XB-9, XB-10, XB-11) before the plan 44 chunk that
shares their files. §6 names each interlock.

## 3. Decisions for the owner (XD1 to XD8)

**Decided 2026-10-24.** The owner took every recommendation below as written. Each call is recorded on its issue: #1590 (XD1), #1595 and #1591 (XD2), #1580 and #1581 (XD3), #1585 (XD4), #1576 (XD5), #1564 (XD6) and #1574 (XD7). XD8 is recorded by XB-0's close-out comment on #1562.

| Id | Issue | Question | Decision (as recommended) | Blocks |
|---|---|---|---|---|
| XD1 | #1573 #1590 | After a save, a close that lands during the debounced tree refresh is refused ("File tree refresh cancellation requested…"). Keep the refusal, have the close cancel and await the read, or skip the refresh once a close has begun? | A close that has begun cancels the pending debounced refresh (it never starts) and awaits any in-flight tree read before tearing down, whoever started it (a user who pressed Refresh then Close wants the close), bounded (5 s, then today's refusal with its sentence, so the fallback path keeps its announcement contract). The user never repeats a close for Slate's own save. The three refusals for user-started work (folder expansion, import, filter) are unchanged. A close refused for another reason re-issues the refresh it skipped. | XB-11 |
| XD2 | #1595 #1591 | Line reading (Down) speaks a drawn formula as "grouping" and a coverage-gap formula as its `$$…$$` source; Tab speaks the HelpText source after the speech. Which shape: (a) a text-range representation that reads as the MathCAT speech, (b) a caret-entry announcement like Ctrl+Alt+M, or (c) document that math is read with Ctrl+Alt+M? And where does the source go when it leaves HelpText? | Decided by a one-day spike (XB-9 step 0, recorded on #1595 before any product code) that records what NVDA and Narrator speak, per block, for Down, Up, caret re-entry and Tab under three candidate shapes: the speech as the paragraph's range text in a visually suppressed `Run` beside the drawing; every math paragraph element-only (empty range, the speech as the element's Name) with the peer's control type tried as Group, Image and Text; and a caret-entry announcement. The shape that speaks the speech once with no LaTeX and no bare "grouping" under both readers wins; (c) is rejected because it leaves the commonest path broken. The source leaves HelpText (ToolTip keeps it) and is spoken on request by a Reading-scope chord (`Ctrl+Shift+Enter` unless taken), named in the help line and the chord table; its sentence is core-owned (`A11yEvent::MathSourceSpoken`, an FFI addition that keeps the Swift switch compiling) unless the owner declines the FFI change, then `HostComposed` like the braille sentence. The mac half of #1591 takes the same "on request, never on focus" rule. | XB-9 |
| XD3 | #1580 #1581 | The budgets, the reader's sentence, and how many abandoned renders may run. | Per diagram 5 s, per note 20 s (the diagram cap of 2,000 stays); SVG preview decode 5 s; at most two abandoned renders (and, separately, two abandoned decodes) per process at once, further renders refused at once (amended 2026-10-24, the owner's call recorded on #1580: a refused render has its own sentence, "Diagram not drawn: earlier diagrams are still rendering past their 5-second budget. The source follows.", since the timeout sentence is untrue of a render that never ran, so core's sentence has four forms, `"diagram"`, `"note"`, `"busy"` and `"image"`). Core owns the sentence (`diagram.rs` `render_time_exceeded_message`: "Diagram not drawn: rendering took longer than 5 seconds. The source follows.", the per-note form, and "Image not shown: decoding took longer than 5 seconds."), exposed over the FFI as a free function so the Windows SVG path reads it; rendered as `RenderFailed` for Mermaid (both hosts' failed states already show the message) and as the preview's failed state on Windows. The guard weights (rules × elements, marker vertices × subtree) are a follow-up issue XB-0 files, not this chunk. | XB-10 |
| XD4 | #1585 | Fix the prime in core's `latex_to_mathml` event rewrite, or wait for upstream? And since the fix changes what VoiceOver speaks, does the PR wait for the owner's VoiceOver pass or merge with the pass owed? | Fix in core (the `push_mathml` rewrite path #1557 added), report upstream to pulldown-latex in the same PR body, regenerate the golden. The PR records the NVDA spot check here; the owner runs the VoiceOver pass on the PR's branch before merge (both lines of the calculus note). If the pass cannot run in time, the PR merges with `not delivered: VoiceOver pass`, writes `Refs #1585`, and a mac follow-up closes it. | XB-8 |
| XD5 | #1576 | Accept the tree-sitter 0.27 malformed-code differences as cosmetic, or pin snapshots? | Pin the eight changed results, and one malformed snippet per language entry, as snapshots, so the next bump shows its effect in a diff. Cosmetic today, but silent drift is what the dependency arc is trying to end. | XB-7 |
| XD6 | #1564 | The redesign before Dependabot is re-enabled: a policy actor list separate from the Studio hook's, Dependabot runs on vendor runners only, required checks on `main` (done, #1565). Write it as a plan 42 amendment first? | Yes: XB-12 is design-first and produces the amendment (`42_self_hosted_mac_runner_plan.md` §x) plus the Windows home-pool half (`route.py`, `SlateCiHost.psm1`: a Dependabot head is never routed home) and the `allowlist.json` split. The Studio hook change (`admission.py`) is written here but its live effect is proved on the Studio, which is the owner's step; the amendment says so. Re-enabling stays off until the owner has run that step. | XB-12 |
| XD7 | #1574 | Trim the build output by a `RuntimeIdentifier` (`win-x64`) or by excluding the non-Windows native assets from the copy? | `RuntimeIdentifier` `win-x64` on the app project (the shipping shape; the FlaUI gate already runs an x64 exe) plus `<ExcludeAssets>native</ExcludeAssets>` is not enough on its own because the `.pdb` symbols ride the native asset; the PR measures `runtimes/` before and after and records the `flaui-binaries` artifact size. If a RID breaks the test projects' `dotnet test` (they reference the app), the fallback is a `Content` exclusion of `runtimes/<non-win>/**` and `*.pdb` under `runtimes/`. | XB-4b |
| XD8 | #1562 | Close #1562 on the recorded pass, with #1595 and #1591 carrying the raw-LaTeX criterion? | Yes; the field pass's deliverable (the recorded table) is on the issue, and the two findings have issues of their own that this plan delivers. | XB-0 |

## 4. Rules (every chunk)

Plan 44 §4 applies unchanged: one PR closes its issues and nothing else;
the failing test first; the Windows test recipe (bindings regenerated
per worktree into a per-worktree `CARGO_TARGET_DIR`, `dotnet test
--filter`, `dotnet format --include` after every C# edit); the four Rust
gates in PowerShell for a core change; field passes launched per plan 44
§4 rule 4; the adversarial review per chunk; the parity matrix regenerated
when a chord or help line changes. Two additions:

1. **The cooldown and the one-at-a-time merge** (`AGENTS.md`,
   "Dependencies" and "Git and PRs", #1605) are in force for every chunk: every version a PR brings in is at least 7 days old when the PR
   opens, and no PR merges until `main`'s runs on the previous merge
   commit are green.
2. **A Windows-lane change proves itself on the lane.** A chunk that edits
   `windows.yml`, `a11y-check.yml` or `pr-gate.yml` pins the property it
   changes in `Workflows.Tests.ps1` (as #1577 and #1578 did), and its PR
   body quotes the run that exercised the changed step.

## 5. The chunks

Size: S under a day, M a few days, L a week or more. Lanes: `windows`
(windows.yml), `rust` (rust.yml), `a11y` (a11y-check.yml), `swift`
(swift-tests.yml compile only). AT: the NVDA or Narrator work the chunk
carries.

| Chunk | What | Closes | Size | Lanes | AT | Decision |
|---|---|---|---|---|---|---|
| XB-0 | Close-outs: verify #1572 on `main` by its own filtered-run reproduction and close it with a comment at f267867b; the cooldown PR for #1569 is done (#1605, merged as 1aeb0115; `Refs #1565`, which stays open for XB-13's lane-level checks); correct #1493's starting pin to rc.6 in a comment; close #1562 per XD8; file the two follow-up issues this plan names (the SVG guard weights, XD3; the mac "source on request" twin of XD2 if #1591's mac half does not already carry it) | #1572 #1562 (Refs #1493) | S | none (issue comments and one local run) | — | XD8 (decided: close) |
| XB-1 | The shell gate's history test waits for the alpha row (`WaitForTreeItemStartingWith(filesTree, automation, "alpha")`, the file's own helper at `:1571`), after confirming the row's name in a run; the other files-tree failure (run 39728855123, `FilesTree_ArrowsKeepFocusEnterOpens`) is read and, if it is the same one-shot lookup, fixed in the same PR, else filed | #1586 | S | windows (shell gate) | — | — |
| XB-2 | `PumpedDispatcherTests.ADrainOnADispatcherWithoutItsWindowEndsThroughTheDirectWake`: a diagnosis first (which path ended the drain on run 39897042671; the test records every wake source, run on a busy local desktop and on the lane); then the fact either insists on the direct wake deterministically (the window-less dispatcher's other wake paths closed in the test) or accepts the other path with a comment naming it as legitimate | #1594 | S–M (diagnose first) | windows (app tests) | — | — |
| XB-3 | Rust on the Windows lane: the four clippy findings in Windows-only code fixed by tightening their `cfg` gates (`session/tests/watch.rs`'s `WATCH_REARM_ENABLED` import, `vault/watch.rs`'s `Relocation::Moved`, `session.rs`'s `OpenSwapSeamGuard` and `install_open_swap_seam`, `vault/fs.rs:331`'s borrow), never a blanket allow and never touching the #1513 switch itself; `cargo clippy --all-targets --workspace --locked -- -D warnings` added to windows.yml's rust-tests job after `cargo check`; the `dir_tree` census generator produces only host-valid names (a Windows name filter at generation, with the seed sequence unchanged on Linux and macOS so their coverage is identical), and the five censuses pass on Windows | #1570 #1571 | S | windows (rust-tests), rust | — | — |
| XB-4a | CI uses the pinned .NET SDK. As delivered (owner call 2026-10-24): `global.json` at the repository root with `rollForward: patch`; `global-json-file: global.json` on all six `actions/setup-dotnet` steps; a step after each that fails unless `dotnet --version` is the pin; and `Workflows.Tests.ps1` facts over every workflow and action | #1568 | S | windows | — | — |
| XB-4b | SkiaSharp and HarfBuzzSharp pinned directly at their current releases past the cooldown; native `.pdb` symbols and non-Windows native libraries out of the build output per XD7 (as delivered: XD7's fallback, done as a `Directory.Build.targets` target every project imports, since a RID on the app trims only the app's own output. It keeps the apphost's RID, `win` and `any`, without `.pdb`); `runtimes/` and the `flaui-binaries` artifact measured before and after in the PR body | #1574 | S–M | windows | — | XD7 |
| XB-5 | a11y-check's score never reads a mismatched download: the download step drops `continue-on-error`, a following step handles "no results" explicitly (the floor step's plain-words report, unchanged in wording), and a digest failure deletes the extracted files before the floor step runs; a `Workflows.Tests.ps1` fact pins the step shape | #1575 | S | a11y (the PR's own run) | — | — |
| XB-6 | Dated, on or after 2026-10-29: (a) the nine `actions/setup-python` pins move to v7.1.0's commit SHA (re-read at open; a newer release past the cooldown wins), `# v7.1.0` comments; (b) `ci/windows-runner/golden/versions.json` moves the runner to 2.338.1 with its win-x64 SHA256, in the PR that prepares the next golden rebuild (the rebuild is the owner's) | #1596 | S | windows, and the pilots the pins touch | — | — |
| XB-7 | Dependency-upgrade residue in core: after MathCAT initializes on its worker, `slate-core` installs a panic hook that calls MathCAT's hook (its bookkeeping kept) and then the default hook, with a fact that a panic after the first math render prints its message; eight malformed-code highlighting snapshots and one per language entry pinned (XD5) | #1583 #1576 | S | rust | — | XD5 |
| XB-8 | A primed function's argument: `latex_to_mathml` rewrites a prime after a base into an `<msup>` superscript on it before `push_mathml`, MathCAT's speech for `<msup><mi>v</mi><mo>′</mo></msup><mo>(</mo>…` checked under all twelve settings, the golden regenerated with the diff in the PR, an upstream pulldown-latex report linked; NVDA spot check on the two calculus lines here; the VoiceOver pass per XD4 | #1585 (or `Refs`, per XD4) | S–M | rust, swift (golden fixture only; no FFI change) | NVDA spot; VoiceOver pass (owner) | XD4 |
| XB-9 | The reading view's math blocks under line reading and Tab: step 0 is the XD2 spike (one day, NVDA and Narrator, both candidate shapes, recorded on the issue); then `ReadingDocumentBuilder` builds every math paragraph (drawn, coverage-gap, fallback) in the shape the spike chose, so Down and caret re-entry speak the MathCAT speech; the source leaves `ReadingMathElement`'s HelpText and is spoken on request through the chosen landing (core-owned copy; parity matrix regenerated; the reading view's help line names the math chords); hosted facts pin the paragraph's text range and the element's HelpText; a full NVDA pass records line reading for a drawn, a coverage-gap and a fallback formula, Tab on each, and the on-request source, plus a Narrator spot | #1595; `Refs #1591` (its mac half closes it) | M | windows + FlaUI (the shell gate's reading rows) | NVDA full, Narrator spot | XD2 |
| XB-10a | Core: a diagram render has a per-diagram and a per-note budget (XD3), the caller stops waiting at the budget, the block degrades to `RenderFailed` with core's "took too long" sentence and its source, abandoned renders are counted and bounded; the sentence's three forms are one core function exposed over the FFI (a free function; no record change); facts through a slow-renderer seam (no real 30 s render in the suite); one Windows display fact for a core `RenderFailed`; `BENCHMARKS.md` notes the budgets | #1580 | M | rust, windows, swift (compile) | — | XD3 |
| XB-10b | Windows: the SVG preview decode on its own thread gets the same budget and the same core sentence, the caller stops waiting, the preview shows the failed state, abandoned decodes are bounded per process; a hosted fact with a slow-decoder seam; files the guard-weights follow-up | #1581 | M | windows | NVDA spot (the failed-state sentence) | XD3 |
| XB-11 | Close after save: per XD1, a close that has begun cancels the pending debounced refresh and awaits an in-flight directory read, bounded, before teardown; the refusal and its sentence remain only for the bounded fallback and a user-started refresh; hosted facts: a close issued inside the 150 ms window, during the read, and past a 5 s read (the seam holds the read) each end as XD1 says; `PumpedSaveReentrancyTests`' wait from #1555 is then either relaxed or kept with a comment | #1573 #1590 | S–M | windows | NVDA spot (if the sentence or its timing changes) | XD1 |
| XB-12 | Dependabot policy redesign, design-first (XD6): the plan 42 amendment (a separate policy actor list; Dependabot heads never routed to the Studio or the home pool; vendor runners only; the required checks of #1565 as the merge gate); then the Windows home-pool half (`ci/windows-runner`'s `route.py` and `SlateCiHost.psm1` refuse a Dependabot actor with a fact each), the `allowlist.json` split, and `dependabot.yml`'s comments; `admission.py` is edited here and proved on the Studio by the owner before `open-pull-requests-limit` leaves zero | #1564 (closes only after the owner's Studio step; until then `Refs`) | M | windows (runner tests), the mac runner's own tests | — | XD6 |
| XB-13 | Every lane reports on every PR, so `main`'s ruleset can require them: every lane triggers on every PR, and its first job decides from the lane's one path list whether the PR touches it, skipping the other jobs (a skip reports as passed) when it does not (the owner's option 1 on #1565, 2026-10-25, in place of twin workflows); every lane groups `main` pushes by commit, so none drops a queued run (#1638); the ruleset adds the lane checks once a week of PRs shows no false reds; `Workflows.Tests.ps1` pins the decision and every job's gate | #1565 (what remains) | M | every lane (the PR's own runs are the proof) | — | — |

### Closure matrix

| Issue | Chunk | Closes or Refs | What else must happen before close |
|---|---|---|---|
| #1562 | XB-0 | closes (XD8) | the owner's yes |
| #1564 | XB-12 | `Refs`, then closes | the owner's Studio step |
| #1565 | XB-0 writes step 3; XB-13 closes | `Refs`, then closes | a week of PRs under the lane checks |
| #1568 | XB-4a | closes | — |
| #1569 | XB-0 | closes | — |
| #1570 #1571 | XB-3 | closes | — |
| #1572 | XB-0 | closes by comment | the filtered run on `main` |
| #1573 #1590 | XB-11 | closes both | XD1 |
| #1574 | XB-4b | closes | XD7; HarfBuzzSharp's date past the cooldown |
| #1575 | XB-5 | closes | — |
| #1576 #1583 | XB-7 | closes | XD5 |
| #1580 | XB-10a | closes | XD3 |
| #1581 | XB-10b | closes | XD3; XB-10a merged |
| #1585 | XB-8 | closes, or `Refs` with a mac follow-up | XD4 |
| #1586 | XB-1 | closes | — |
| #1591 | XB-9 | `Refs` | the mac half (plan 43's queue) |
| #1594 | XB-2 | closes | the diagnosis |
| #1595 | XB-9 | closes | XD2; the NVDA pass |
| #1596 | XB-6 | closes | 2026-10-29 |

## 6. Delivery order

At most three agents at once, as plan 44 §6. Chunks in one wave share
no files.

| Wave | Slot 1 | Slot 2 | Slot 3 | Gate |
|---|---|---|---|---|
| X0 | XB-0 | — | — | XD8 |
| X1 | XB-1 | XB-3 | XB-4a | — |
| X2 | XB-2 | XB-4b | XB-5 | XD7 |
| X3 | XB-7 | XB-11 | XB-9 step 0 (the spike) | XD1 XD2 XD5 |
| X4 | XB-8 | XB-9 | XB-10a | XD3 XD4 |
| X5 | XB-10b | XB-12 | XB-13 | XD6 |
| dated | XB-6 on or after 2026-10-29, in any free slot | | | |

Why this order:

- **X0 is a morning.** It pushes a written branch, closes two issues by
  evidence that already exists, and settles XD8.
- **X1 and X2 are hygiene the later chunks and plan 44 both lean on:**
  a deterministic shell gate (XB-1), a Windows Rust lane that lints
  what it builds (XB-3, before plan 44's WB-12 and WB-28 edit the same
  `cfg`-gated files), and a toolchain CI actually pins (XB-4a, before
  XB-4b changes the package graph on it). XB-4a and XB-4b are two PRs
  on purpose: a red lane after a combined change would not say which
  of the SDK and the package graph broke it.
- **X3 and X4 are the reader-facing work,** each behind its owner
  decision, with one full NVDA pass (XB-9) and two spot checks (XB-8,
  XB-11) across the two waves; never two full passes in a wave.
- **X5 is CI design** that no other chunk waits on.

Sequenced on purpose, because they share files with plan 44:

| Shared code | Order |
|---|---|
| `Reading/ReadingDocumentBuilder.cs`, `ReadingMathElement.cs` | XB-9, then plan 44's WB-9 → WB-11 → WB-20 → WB-21b → WB-22 (plan 44 §6's Reading row); XB-9 is small and lands first |
| `VaultLifecycleViewModel.cs` | XB-11 (the close path, `:859` and `:1485`), then plan 44's WB-9 → WB-12 → WB-25 → WB-28 → WB-29 (the open sequence); if WB-9 lands first, XB-11 rebases, the two paths do not overlap |
| `crates/slate-core/src/vault/watch.rs`, `session.rs`, `vault/fs.rs` | XB-3 (cfg gates only), then plan 44's WB-12, WB-24, WB-28 |
| `windows.yml` | XB-4a (setup-dotnet lines), XB-3 (the rust-tests job), then plan 44's WB-17 (the app job's `--check` steps, `:523-527`), then XB-13 (the `changes` job, the gated jobs and the concurrency block); each later one rebases |
| `crates/slate-core/src/math.rs` and the math golden | XB-7 (the panic hook, after init), then XB-8 (the event rewrite and the golden); one golden diff at a time |
| `crates/slate-core/src/diagram.rs` and the Windows diagram/SVG preview | XB-10a, then XB-10b; plan 44's WB-11 and WB-20 (reading cost) touch the reading view's diagram handling, so whichever lands second rebases |
| `AGENTS.md` | XB-0 (the cooldown and merge procedure), then plan 44's own `AGENTS.md` edits if any |
| `Workflows.Tests.ps1` | XB-4a, XB-5, XB-12, XB-13 each add a fact; same-wave pairs rebase, never hand-merging |

Field passes run one at a time on the owner's desktop with NVDA
(Narrator for XB-9's spot), launched per plan 44 §4 rule 4. The
VoiceOver pass XD4 names is the owner's, on XB-8's branch.

## 7. Verification lanes and the local loop

Plan 44 §7 applies. Per chunk, the command its PR body quotes:

- XB-0: the filtered census run on `main`
  (`dotnet test apps/slate-windows/tests/SlateWindows.Tests --filter "FullyQualifiedName~AtNavigationMapCensus"`, a fresh process, Release, bindings regenerated).
- XB-1, XB-2, XB-9, XB-11: `dotnet test … --filter` on the touched
  facts, seen failing first; XB-1 and XB-9 also the shell gate on the
  lane (local shell-gate runs only with NVDA closed and the desktop
  free).
- XB-3, XB-7, XB-8, XB-10a: the four Rust gates in PowerShell; XB-3 also
  `cargo test -p slate-core --profile ci --lib -- session::tests::dir_tree::`
  on this machine (the issue's own reproduction).
- XB-4a, XB-4b, XB-5, XB-6, XB-12, XB-13: `Invoke-Pester` on
  `Workflows.Tests.ps1`, then the PR's own lane runs, quoted by run id.
- XB-8, XB-9: the NVDA recipe of plan 44 §4 rule 4; the recorded phrases
  in the PR body.

## 8. Watch list and dated items

| Issue | Waits for | Check | Action when it lands |
|---|---|---|---|
| #1596 | 2026-10-29 02:56Z (setup-python), 14:14Z (runner) | that day | XB-6 |
| #1560 | a braille transcriber's verdict | when the owner has one | a core PR correcting the golden per item, with its diff; deliverable here |
| #1493 | notify 9.0.0 (rc.6 is newest, 2026-10-17) | weekly, plan 44 §8 | plan 44's one-line bump |
| #1591 (mac half) | plan 43's queue | — | XB-9's "on request" rule is the shared decision |

## 9. Left to plan 44

Per the owner's instruction of 2026-10-23, these issues are not
re-planned here. The chunk is plan 44's; its spec is in
`44_windows_backlog/specs/`.

| Issue | Plan 44 chunk | Note |
|---|---|---|
| #1475 | WB-12 (the `GraphConfigStore` write, WD7's Open Recent sentence); WB-28 (the identity half, WD22) | |
| #1480 #1499 #1492 | WB-24 | needs a scratch VHD or VM and a OneDrive Files On-Demand vault from the owner (WD14, WD15) |
| #1481 #1485 #1486 | WB-18 | |
| #1484 | WB-31 | closes only after a macOS measurement (WD21) |
| #1493 | §8 watch list | starting pin is now rc.6 (§2.2) |
| #1505 #1508 #1516 | WB-12 | WD5, WD6 |
| #1510 | WB-17 | |
| #1515 #1522 | WB-6 | WD3 |
| #1519 | WB-10a | |
| #1525 (Windows half) | WB-29 | after plan 44 WB-29a's core store is on `main` (WD20 as answered 2026-10-26) |
| #1526 | WB-21b | the mac follow-up WB-0 files closes it (WD13) |

## 10. How an implementing agent works a chunk

Plan 44 §10 applies (fresh worktree on a `claude/<chunk-slug>` branch
from `main`; read the spec's "Current state" lines before coding; failing
facts first; registers closed in the same PR; commit messages
`type(scope): one lower-case sentence saying what is now true`; PR bodies
list `Closes #N`, the facts, the registers, the field-pass phrases and
"not delivered: <reason>"). Every chunk has its spec under
`45_windows_followups/specs/`, written 2026-10-23 with every
file-and-line claim verified against `main` at 2463ee7a. Each spec ends
with a "Brief" that, with this section, is all an implementing agent
needs. XB-9 and XB-12 open with a step the owner reads before code (the
XD2 spike record; the plan 42 amendment).

| Chunk | Spec | What becomes true |
|---|---|---|
| XB-0 | `specs/xb00_close_outs.md` | #1572 closes on its own reproduction, the cooldown rules are already on `main` (#1605), #1562 closes on its recorded pass, and the stale notes are corrected |
| XB-1 | `specs/xb01_shell_gate_waits_for_the_alpha_row.md` | The shell gate's files-tree tests wait for their rows instead of looking once |
| XB-2 | `specs/xb02_pumped_dispatcher_direct_wake_diagnosis.md` | The direct-wake fact says which path ended its drain, and insists on it |
| XB-3 | `specs/xb03_rust_on_the_windows_lane_lints_and_censuses.md` | The Windows Rust lane runs clippy on the code it builds, and the dir_tree censuses pass on Windows |
| XB-4a | `specs/xb04a_ci_uses_the_pinned_dotnet_sdk.md` | Every CI `dotnet` runs the SDK `global.json` pins |
| XB-4b | `specs/xb04b_skia_pins_and_trimmed_output.md` | SkiaSharp and HarfBuzzSharp are pinned directly, and the build output carries no native symbols or non-Windows libraries |
| XB-5 | `specs/xb05_a11y_check_digest_gate.md` | A digest mismatch stops the a11y score from being computed |
| XB-6 | `specs/xb06_dated_pins_setup_python_and_runner.md` | setup-python and the golden image's runner move to their cooled releases |
| XB-7 | `specs/xb07_dependency_upgrade_residue_in_core.md` | A panic after the first math render prints its message, and malformed-code highlighting is pinned |
| XB-8 | `specs/xb08_primed_function_argument.md` | A primed function's argument is spoken as its argument |
| XB-9 | `specs/xb09_reading_view_math_under_line_reading_and_tab.md` | Reading a note line by line speaks a math block's speech, and the source is heard on request only |
| XB-10a | `specs/xb10a_diagram_render_budget.md` | A diagram render has a time budget, and a diagram past it degrades with core's sentence |
| XB-10b | `specs/xb10b_svg_preview_decode_budget.md` | An SVG preview decode has the same budget and the same sentence |
| XB-11 | `specs/xb11_close_after_save.md` | A close after a save never has to be repeated for Slate's own refresh |
| XB-12 | `specs/xb12_dependabot_policy_redesign.md` | Dependabot can run CI without reaching the owner's hardware |
| XB-13 | `specs/xb13_every_lane_reports_on_every_pr.md` | Every lane reports on every PR, so `main`'s ruleset can require them |

## 11. Risks

- **XD2 chooses a shape no spike has heard.** The spike is step 0 for
  that reason; if neither candidate reads cleanly under NVDA and
  Narrator, XB-9 returns to the owner with the recordings before any
  builder code.
- **A render budget hides a regression.** A diagram that used to draw
  in 3 s and now takes 6 s degrades silently to the failed state. XB-10a
  logs every budget hit with the diagram's size and time, and
  `BENCHMARKS.md` keeps a sample that must stay under half the budget.
- **XB-4b's RID changes what the test projects load.** XD7's fallback
  exists for that; the PR body records which shape shipped and why.
- **XB-12 is half-provable here.** The Studio hook's live behaviour is
  the owner's step; the plan keeps `open-pull-requests-limit: 0` until
  it has run, so an untested Dependabot PR cannot reach the home pool
  in the meantime.
- **The one-at-a-time merge slows a three-slot wave.** It is the rule
  XB-0 writes into `AGENTS.md`; a wave's three PRs merge in sequence,
  each after `main` is green on the previous merge, and the second and
  third rebase. Plan 44's waves inherit the same cost.
