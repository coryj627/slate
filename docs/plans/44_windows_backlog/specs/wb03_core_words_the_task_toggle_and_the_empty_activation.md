# WB-3 — Core words the task toggle and the empty activation: Windows posts `TaskToggled`, `TaskToggleFailed` and `NothingToActivateAtCursor`

Issues: Closes #1467, Closes #1465. Size S–M. Lanes: windows (docs census; ledger regenerated). AT: NVDA spot (the three sentences are already core's words; only the new Ctrl+Enter refusal is new speech). Wave: W1. Depends on: nothing. Parallel with: WB-1, WB-2. Decision: none.

## Amended at implementation (2026-10-26)

This section outranks the text below it.

- **No fact can reach the bare-`#` arm of `ActivateCanonicalSpan`.** Core never makes a tag span of `#` alone: a tag's first character must be an ASCII letter (`scan_tags`, `crates/slate-core/src/editor_spans.rs`). So a bare `#` in a note is plain prose to the activation, and Ctrl+Enter there reaches the no-task arm. The arm still posts the event, so every refusal speaks alike if core ever changes. Its one-arm mutation survives by construction. `CtrlEnterWithNothingUnderTheCaretSaysSo`'s third case is a real bare `#`, through the no-task arm.
- **The two panel facts pin the completion's own arm.** In `AToggledTaskSpeaksCoresTaskToggledEvent` and `AFailedToggleSpeaksCoresDetailWithAClosingPeriod`, the originating tab closes before the publish, so `TaskToggleCompletion` speaks. `ATabToggleSpeaksCoresEvents` pins the tab's own arms.
- **`AReviewToggleSpeaksCoresEvents` also covers a `VaultException`.** The core's fault seam triggers it, and the rendered sentence carries core's detail, not the "@message=" dump.
- **The residue keys:** `PublishTaskToggleThroughTab`'s four identical entries became two (`@1`, `@2`, the two mid-path sentences), the same result as removing `@1` and `@4` and renumbering.
- **WB-2 overlap:** WB-2 (#1659) touches the same designations file and contract 38's ledger. Whichever lands second merges `main` and regenerates the ledger.
- **Unsaved edits ask for the save first (red team).**
  - The task cache describes the saved text. In a note with unsaved edits it cannot vouch for the caret's line: a task typed since the save is not in it.
  - So when no task is found and the tab is dirty, Ctrl+Enter (keyboard origin only) speaks the save-first sentence an unsaved link already gets ("Save <note> before using this interaction.") and returns true, instead of "Nothing to activate at cursor." A Ctrl+click stays silent.
  - Mac still says nothing is there; filed as #1662. The fact is `CtrlEnterOnALineTheUnsavedEditsChangedAsksForTheSaveFirst`.
- **The pointer fact covers every arm** (prose, display math, a bare `#`). The panel failure fact pins the whole sentence.
- **The core-fault facts pin core's wording.** The binding's `Message` for the seam's error (`SavedButUnindexed`) is "@newContentHash=…, @detail=…", which has no "@message=" in it. So `ATabToggleSpeaksCoresEvents` and `AReviewToggleSpeaksCoresEvents` also require the rendering to begin "Task could not be toggled: Saved, but not indexed yet: ". Without that, the "pass `error.Message`" mutation survived both. The run-on tail is #1660's.
- **Mutations:** 12 run, 11 killed. The survivor is the bare-`#` arm, which no input can reach (above).
- **Filed, not delivered:**
  - #1660: core renders a toggle that landed but was not indexed (`SavedButUnindexed`) as a failure, in a run-on sentence.
  - #1661: Activate at Cursor acts on the hidden caret in reading mode, and a Ctrl+click speaks while the cache loads.

## Goal

Three Windows sites still compose "Task completed." / "Task reopened." /
"Task could not be toggled: …" as `HostComposed` residue, although core has
carried the same words since #1431. And Ctrl+Enter with nothing under the
caret returns `false` and says nothing, while mac speaks core's "Nothing to
activate at cursor." After this chunk every one of those sentences is a core
event posted from the host, the `proposed` designations and the residue rows
are gone, the ledger regenerates clean, and a keyboard user who presses
Ctrl+Enter on plain prose hears that the key was received.

## Current state on main

Verified in the worktree; the plan's and `report_a11y.md`'s lines hold. The
only correction: the Tasks Review failure is a plain `string? failure`
caught from `Exception` (`:723`), not a `VaultException`, so core's
`vault_error_detail` applies only when the caught exception is one.

- **#1467 site 1:** `WorkspaceViewModel.cs:1483-1547`
  `WorkspaceTabViewModel.PublishTaskToggleThroughTab`: `:1500-1501` sets
  `Status = $"Task could not be toggled: {error.Message}"` and posts
  `HostComposed(Status, High)`; `:1545-1547` sets `Status` to the success
  pair and posts `HostComposed(Status, Medium)`. The two mid-path sentences
  ("Task toggled on disk, but the editor changed/no longer matches …",
  `:1512`, `:1534`) stay `HostComposed` — the issue does not name them and
  core has no variant for them.
- **#1467 site 2:** `WorkspaceViewModel.cs:3414-3418` and `:3423-3428`
  `WorkspaceViewModel.TaskToggleCompletion`: the same pair when the toggle
  did not publish through a tab (`publishedThroughTab` false).
- **#1467 site 3:** `Panels/TasksReviewViewModel.cs:776-778` (failure,
  `failure` is `exception.Message` from the `catch (Exception …)` at `:723`)
  and `:812-814` (success pair).
- **Core:** `a11y.rs:880-887` `TaskToggled { completed }`,
  `TaskToggleFailed { detail }`; rendering `:2134-2139` — the failure closes
  with a period through `lead_then_sentence`, and an empty detail renders
  "Task could not be toggled."; priority `:1880` (High). Corpus rows
  `:4319-4328`; Windows mirror `Censuses/A11yCorpusCensus.cs:155-158`.
- **#1465 site:** `EditorInteractions.cs:802` `ActivateAt(utf16Offset,
  origin)`: returns `false` at `:836-839` (inside a math region), at
  `:847` → `TryToggleTaskAt` (`:1954`) whose `task is null` arm returns
  `false` at `:1974-1977`, and `ActivateCanonicalSpan` (`:870`) returns
  `false` at `:875-878` for a tag whose authored text is `#` alone.
  `ActivateAtCaretCommand` (`:616-618`) discards the bool. The
  coordinator's announcer is `_announce` (`:531`); the origin enum already
  distinguishes `Keyboard` (used at `:830`, `:843`).
- **Core:** `a11y.rs:791` `NothingToActivateAtCursor`, rendered `:2086`
  "Nothing to activate at cursor." (Medium); Windows mirror
  `A11yCorpusCensus.cs:125`.
- **Registers:** `a11y_trigger_designations.json:189-203` the three
  `proposed` designations; `:623-642` the four `PublishTaskToggleThroughTab@1-4`
  residue entries, `:663-671` `TaskToggleCompletion@1-2`, `:478-486`
  `TasksReviewViewModel.ToggleTask@1-2`. Contract 38's generated ledger
  lies between `38_notification_dispatcher_contracts.md:711` and `:1060`
  (the residue register §D-8 at `:947` is inside it): rows `:860`
  (`NothingToActivateAtCursor`), `:927`, `:929`. Generator
  `scripts/a11y_trigger_ledger.py` (`--write`), which runs
  `A11yTriggerParityCensus.SourceInventoryCanBeExportedForTheLedgerGenerator`
  (`A11yTriggerParityCensus.cs:324`).
- **Pins to churn:** `TasksPanelTests.cs:429, 470, 668, 730, 807, 872, 1034`
  and `TasksReviewTests.cs:197, 280, 625, 678, 868` match
  `composed.Text == "Task completed."` on `HostComposed`;
  `TasksReviewTests.cs:622` pins `"Task could not be toggled: index commit failed"`
  (no period — core adds one); `TasksPanelTests.cs:795, 849, 919, 1015` and
  `TasksReviewTests.cs:674` match the failure prefix.
  `W2EditorInteractionTests.cs:79-80` asserts `ActivateAt` returns `false`
  on `#not-a-tag` / `#not-math` and only that `tags.Count` is unchanged.

## Reference

Mac: `AppState.swift:14116` (`performToggleCurrentTask` posts
`TaskToggled` / `TaskToggleFailed`); `AppState+EditorActivation.swift:18-55`
posts `NothingToActivateAtCursor` from the mac verb's four refusal arms.
Contract 38 doctrine: text is core-rendered, never host-composed; the
ledger's `proposed` status is a promise to adopt (the rows at `:860, :927,
:929` quote both issues).

## Design

- `PublishTaskToggleThroughTab` (`:1500-1501`): `var failed = new
  A11yEvent.TaskToggleFailed(SlateUniffiMethods.VaultErrorDetail(error));
  Status = SlateUniffiMethods.A11yRender(failed).Text; announce(failed);`
  — the status shows the sentence that is spoken, as `StartSave` does
  (`:1133-1135`). `:1545-1547`: `var toggled = new
  A11yEvent.TaskToggled(!task.Completed); Status = A11yRender(toggled).Text;
  announce(toggled);` (`task.Completed` is the state BEFORE the toggle, so
  `Completed: !task.Completed` is the new state — the existing ternary
  already reads it that way).
- `TaskToggleCompletion` (`:3414-3418`, `:3423-3428`): the same two
  events; the failure detail is `VaultErrorDetail(error!)` (the local is a
  `VaultException`).
- `TasksReviewViewModel` (`:776-778`, `:812-814`): `TaskToggled` on
  success; on failure `TaskToggleFailed(detail)` where `detail` is
  `VaultErrorDetail(vault)` when the caught exception is a `VaultException`,
  else the exception's own message (mac's `localizedDescription` arm; the
  embed resolver's precedent, `EditorInteractions.cs:1529-1540`). Keep the
  `failure` string for `_loadError`; add a parallel `VaultException?` local
  captured in the catch at `:723`.
- `ActivateAt`: the three `return false` arms post
  `_announce(new A11yEvent.NothingToActivateAtCursor())` when `origin is
  EditorInteractionOrigin.Keyboard`, then return `false` (callers that read
  the bool keep their meaning). `ActivateCanonicalSpan` has no origin
  parameter: add one (`origin`), threaded from `ActivateAt`'s call at `:860`.
  `ActivateSpan` (`:852-869`) has no origin of its own and the issue names
  only `ActivateAt`: pass a non-keyboard value there so its bare-`#` arm
  stays silent as today (say so in the PR body). `TryToggleTaskAt` already
  receives `origin`. A Ctrl+click (`Pointer` origin) on plain prose stays
  silent.
- Not changed: the two mid-path `HostComposed` sentences in
  `PublishTaskToggleThroughTab`; `TaskToggleConflict` sites; the
  `W0.5-3 residue` comments are deleted with the residue.
- Registers: delete the three `proposed` designations and the six residue
  entries named above. The surviving `HostComposed` sites in
  `PublishTaskToggleThroughTab` renumber from `@2`/`@3` to `@1`/`@2`: the
  designations keys move with them (the census tells you which key is
  stale). Regenerate the ledger.
- No decision consumed.

## Tests

Facts fail first (§4 rule 1).

- `TasksPanelTests.AToggledTaskSpeaksCoresTaskToggledEvent` — the panel
  toggle's announcement is `A11yEvent.TaskToggled(true)` (typed, not
  `HostComposed` text); the existing text pins at `:429, 470, …` change to
  the typed match. Mutation: post `HostComposed("Task completed.")` → fails.
- `TasksPanelTests.AFailedToggleSpeaksCoresDetailWithAClosingPeriod` — a
  faulted write announces `TaskToggleFailed` whose rendered text is
  "Task could not be toggled: <detail>." Mutation: pass `error.Message` →
  the text carries the binding's `@message=` dump → fails.
- `TasksReviewTests.AReviewToggleSpeaksCoresEvents` — success and failure
  through `TasksReviewViewModel.ToggleTask`; `:622`'s pin becomes
  "Task could not be toggled: index commit failed." (period).
- `TasksPanelTests.ATabToggleSpeaksCoresEvents` — beside the tab-route
  cases `PanelToggleWritesThroughTheGuardedTabPath` (`TasksPanelTests.cs:244`)
  and `RealPreCommitFailuresRepairTheIndexOnTheTabRoute` (`:819`), so the
  Verification filter's `TasksPanelTests` class selects it (`W1WorkspaceTests.cs`
  has no class `W1WorkspaceTests`; its classes are `W1WorkspacePersistenceTests`,
  `W1WorkspaceModelTests`, …). Success: `todo.md` open in a tab, the panel
  toggles its first open task through the tab route
  (`PublishTaskToggleThroughTab`; pumped with `PumpDispatcherUntil` until
  the announcement lands, as `:819` does): the tab's `Status` equals
  `A11yRender(new A11yEvent.TaskToggled(true)).Text` and exactly one
  `TaskToggled { Completed: true }` is announced, no `HostComposed`.
  Failure: under `EnvFaultLock` with `SLATE_TEST_FAULT_AFTER_WRITE` set to
  the note's stem (the `:819` seam, cleared in `finally`), the tab's
  `Status`, read inside the announcer callback when the single
  `TaskToggleFailed` arrives (the later repair may post its own sentence,
  as `:819` shows), equals `A11yRender` of that event, ends with ".", and
  contains no `@message=`. Mutation: restore either
  `HostComposed` arm in `PublishTaskToggleThroughTab` → fails.
- `W2EditorInteractionTests.CtrlEnterWithNothingUnderTheCaretSaysSo` —
  Ctrl+Enter on plain prose, inside display math, and on a bare `#` each
  post exactly one `NothingToActivateAtCursor`; `ActivateAt` still returns
  `false`. Mutation: drop the post in one arm → that case fails.
- `W2EditorInteractionTests.ACtrlClickOnPlainProseStaysSilent` — `Pointer`
  origin posts nothing. Mutation: post regardless of origin → fails.
- Censuses: `A11yTriggerParityCensus.EveryBindingKeyAndEveryConstructionIsCoveredInBothDirections`
  and `EveryResidueConstructionHasItsOwnNamedEngineAndNoStaleWaivers`
  pass with the designations removed (they fail while a `proposed` row has a
  Windows site, or a residue key points at a construction that is gone).

## Docs and registers

- `a11y_trigger_designations.json`: remove `:189-203` (three `proposed`),
  `:478-486`, `:623-642` (@1 and @4; renumber @2/@3), `:663-671`.
- `38_notification_dispatcher_contracts.md`: regenerate the ledger between
  `:711` and `:1060` (`python -I scripts/a11y_trigger_ledger.py --write`,
  bindings present). Rows `NothingToActivateAtCursor`, `TaskToggled`,
  `TaskToggleFailed` gain Windows sites and lose the "Windows proposed"
  note; the residue register loses six rows.
- No `w_c_matrix.md` or `parity_matrix.md` row (behaviour, not a surface).
- Contract 38's generated ledger is also touched by WB-17 (§6: "kept
  apart") — WB-3 lands in W1, WB-17 in W6.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb03'
function Assert-Ok([string] $What) { if ($LASTEXITCODE -ne 0) { throw "$What exited $LASTEXITCODE" } }
./apps/slate-windows/generate-bindings.ps1 -Locked; Assert-Ok 'generate-bindings'
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'dotnet build'   # and Error(s)=0
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
# every required fact lives in a class this filter selects (the tab-route fact is in TasksPanelTests; W1WorkspaceTests.cs has no class of that name)
$filter = 'FullyQualifiedName~SlateWindows.Tests.TasksPanelTests|FullyQualifiedName~SlateWindows.Tests.TasksReviewTests|FullyQualifiedName~SlateWindows.Tests.W2EditorInteractionTests|FullyQualifiedName~SlateWindows.Tests.Censuses.A11yTriggerParityCensus|FullyQualifiedName~SlateWindows.Tests.Censuses.A11yCorpusCensus'
$names = @(dotnet test $tests --no-build --configuration Release --list-tests --filter $filter | Select-String '^\s{4}\S' | ForEach-Object { ($_.Line.Trim() -split '\(', 2)[0] }); Assert-Ok 'test discovery'
$required = @(
  'SlateWindows.Tests.TasksPanelTests.AToggledTaskSpeaksCoresTaskToggledEvent',
  'SlateWindows.Tests.TasksPanelTests.AFailedToggleSpeaksCoresDetailWithAClosingPeriod',
  'SlateWindows.Tests.TasksPanelTests.ATabToggleSpeaksCoresEvents',
  'SlateWindows.Tests.TasksReviewTests.AReviewToggleSpeaksCoresEvents',
  'SlateWindows.Tests.W2EditorInteractionTests.CtrlEnterWithNothingUnderTheCaretSaysSo',
  'SlateWindows.Tests.W2EditorInteractionTests.ACtrlClickOnPlainProseStaysSilent',
  'SlateWindows.Tests.Censuses.A11yTriggerParityCensus.EveryBindingKeyAndEveryConstructionIsCoveredInBothDirections',
  'SlateWindows.Tests.Censuses.A11yTriggerParityCensus.EveryResidueConstructionHasItsOwnNamedEngineAndNoStaleWaivers')
$missing = @($required | Where-Object { $names -notcontains $_ })
if ($missing.Count -gt 0) { throw "the filter does not select: $($missing -join ', ')" }
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'dotnet test'
python -I scripts/a11y_trigger_ledger.py --write; Assert-Ok 'ledger --write'
python -I scripts/a11y_trigger_ledger.py --check; Assert-Ok 'ledger --check'
$cs = @(git diff --name-only origin/main -- '*.cs') + @(git ls-files --others --exclude-standard -- '*.cs')   # WorkspaceViewModel.cs, EditorInteractions.cs, Panels/TasksReviewViewModel.cs, TasksPanelTests.cs, TasksReviewTests.cs, W2EditorInteractionTests.cs
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs; Assert-Ok 'dotnet format'
```

`WorkspaceViewModel.cs` has 9 CRs today (mixed endings): check the count
before and after the edit (§4 rule 2). CI: the Windows lane only (no core
change; the ledger `--check` is not in CI until WB-17). AT: NVDA spot, per
§4 rule 4: Ctrl+Enter on prose → "Nothing to activate at cursor."; a toggle
in the editor → "Task completed." / "Task reopened."; a forced failure
(read-only vault) → "Task could not be toggled: <detail>." with a period.

## Acceptance

- [ ] Six facts above pass, each seen failing first; mutation list run; the Verification discovery check finds all six plus the two parity-census facts by fully qualified name (it throws otherwise), and every Verification command exits 0.
- [ ] Three designations and six residue entries removed; ledger regenerated; `--check` clean; both parity-census facts green.
- [ ] Windows lane green; format gate clean; CR count of `WorkspaceViewModel.cs` unchanged.
- [ ] NVDA spot recorded in the PR body (three phrases).
- [ ] PR body: `Closes #1467`, `Closes #1465`. Not delivered: the two "Task toggled on disk, but …" sentences stay `HostComposed` (no core variant; not in either issue).

## Brief

Read `WorkspaceViewModel.cs:1483-1547` and `:3395-3435`,
`Panels/TasksReviewViewModel.cs:700-815`, `EditorInteractions.cs:802-880`
and `:1954-1980`, core `a11y.rs:791, 880-887, 2086, 2134-2139`, and
`a11y_trigger_designations.json:189-203, 478-486, 623-671`. Build: in
`WorkspaceTabViewModel.PublishTaskToggleThroughTab`,
`WorkspaceViewModel.TaskToggleCompletion` and
`TasksReviewViewModel.ToggleTask`, replace the host-composed task sentences
with `A11yEvent.TaskToggled(Completed: !task.Completed)` and
`A11yEvent.TaskToggleFailed(detail)` (detail =
`SlateUniffiMethods.VaultErrorDetail(vaultException)`, else the
non-vault exception's own message; `Status = A11yRender(event).Text`); in
`EditorInteractionCoordinator.ActivateAt`, post
`A11yEvent.NothingToActivateAtCursor` before each of the three `return
false` arms (math region, no task at the caret, bare `#`) when `origin is
EditorInteractionOrigin.Keyboard`, threading `origin` into
`ActivateCanonicalSpan`; `ActivateSpan` keeps silent. Facts, written first
and seen failing: `TasksPanelTests.AToggledTaskSpeaksCoresTaskToggledEvent`
(the panel toggle's announcement is a typed `TaskToggled(true)`);
`TasksPanelTests.AFailedToggleSpeaksCoresDetailWithAClosingPeriod` (a
faulted write renders "Task could not be toggled: <detail>." with no
`@message=`); `TasksReviewTests.AReviewToggleSpeaksCoresEvents` (success
and failure typed; `:622`'s pin gains its period);
`TasksPanelTests.ATabToggleSpeaksCoresEvents` (beside the tab-route facts
`PanelToggleWritesThroughTheGuardedTabPath` `TasksPanelTests.cs:244` and
`RealPreCommitFailuresRepairTheIndexOnTheTabRoute` `:819` — not in
`W1WorkspaceTests.cs`, which has no class of that name and which the filter
does not select; `todo.md` open in a tab, a panel toggle publishes through
`PublishTaskToggleThroughTab`: the tab's `Status` equals the rendered
`TaskToggled(true)` and exactly one typed `TaskToggled` is announced; under
`EnvFaultLock` with `SLATE_TEST_FAULT_AFTER_WRITE` cleared in `finally`,
`Status` read in the announcer callback when the single `TaskToggleFailed`
arrives equals its rendering, with its period and no `@message=`);
`W2EditorInteractionTests.CtrlEnterWithNothingUnderTheCaretSaysSo`
(prose, display math, bare `#` each post exactly one
`NothingToActivateAtCursor`, `ActivateAt` still false);
`W2EditorInteractionTests.ACtrlClickOnPlainProseStaysSilent` (`Pointer`
origin posts nothing). Mutations: post `HostComposed` text instead; pass
`error.Message`; drop one arm's post; post regardless of origin — each
fails its fact. Registers: delete the three `proposed` designations
(`a11y_trigger_designations.json:189-203`) and the residue entries for
`PublishTaskToggleThroughTab@1/@4` (renumber @2/@3 → @1/@2),
`TaskToggleCompletion@1/@2`, `TasksReviewViewModel.ToggleTask@1/@2`; run
`python -I scripts/a11y_trigger_ledger.py --write` so
`38_notification_dispatcher_contracts.md:711-1060` regenerates; both
`A11yTriggerParityCensus` coverage facts green. Local loop, each native
command followed by a check that throws on a nonzero `$LASTEXITCODE`:
regenerate bindings (`$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb03'`),
Release build, then `dotnet test --list-tests` over the filter
`FullyQualifiedName~SlateWindows.Tests.TasksPanelTests |
…TasksReviewTests | …W2EditorInteractionTests |
…Censuses.A11yTriggerParityCensus | …Censuses.A11yCorpusCensus`, throwing
unless all six facts above and the two parity-census facts
(`EveryBindingKeyAndEveryConstructionIsCoveredInBothDirections`,
`EveryResidueConstructionHasItsOwnNamedEngineAndNoStaleWaivers`) are
listed by their fully qualified names; the run; the ledger `--write` and
`--check`; `dotnet format --verify-no-changes --include` over the six
touched `.cs` files. No owner decision. Not
delivered: the two "Task toggled on disk, but …" sentences stay
`HostComposed`. Done when the Windows lane is green, the format gate is
clean with `WorkspaceViewModel.cs`'s CR count unchanged, the mutation run
is recorded, and the NVDA spot (three phrases) is in the PR body with
`Closes #1467`, `Closes #1465`. Commit: `fix(windows): task toggles and an
empty ctrl+enter speak core's events instead of host-composed text`.
