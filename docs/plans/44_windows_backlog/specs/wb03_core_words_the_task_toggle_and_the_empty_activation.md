# WB-3 — Core words the task toggle and the empty activation: Windows posts `TaskToggled`, `TaskToggleFailed` and `NothingToActivateAtCursor`

Issues: Closes #1467, Closes #1465. Size S–M. Lanes: windows (docs census; ledger regenerated). AT: NVDA spot (the three sentences are already core's words; only the new Ctrl+Enter refusal is new speech). Wave: W1. Depends on: nothing. Parallel with: WB-1, WB-2. Decision: none.

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
- `W1WorkspaceTests.ATabToggleSpeaksCoresEvents` (or in `TasksPanelTests`
  beside the tab-published cases) — `PublishTaskToggleThroughTab` success
  and failure; `Status` equals the rendered text.
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

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb03'; ./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --filter "FullyQualifiedName~TasksPanelTests|FullyQualifiedName~TasksReviewTests|FullyQualifiedName~W2EditorInteractionTests|FullyQualifiedName~A11yTriggerParityCensus|FullyQualifiedName~A11yCorpusCensus"
python -I scripts/a11y_trigger_ledger.py --write ; python -I scripts/a11y_trigger_ledger.py --check
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/src/SlateWindows/WorkspaceViewModel.cs apps/slate-windows/src/SlateWindows/EditorInteractions.cs apps/slate-windows/src/SlateWindows/Panels/TasksReviewViewModel.cs <test files>
```

`WorkspaceViewModel.cs` has 9 CRs today (mixed endings): check the count
before and after the edit (§4 rule 2). CI: the Windows lane only (no core
change; the ledger `--check` is not in CI until WB-17). AT: NVDA spot, per
§4 rule 4: Ctrl+Enter on prose → "Nothing to activate at cursor."; a toggle
in the editor → "Task completed." / "Task reopened."; a forced failure
(read-only vault) → "Task could not be toggled: <detail>." with a period.

## Acceptance

- [ ] Six facts above pass, each seen failing first; mutation list run.
- [ ] Three designations and six residue entries removed; ledger regenerated; `--check` clean; both parity-census facts green.
- [ ] Windows lane green; format gate clean; CR count of `WorkspaceViewModel.cs` unchanged.
- [ ] NVDA spot recorded in the PR body (three phrases).
- [ ] PR body: `Closes #1467`, `Closes #1465`. Not delivered: the two "Task toggled on disk, but …" sentences stay `HostComposed` (no core variant; not in either issue).

## Brief

Read `WorkspaceViewModel.cs:1483-1547` and `:3395-3435`,
`Panels/TasksReviewViewModel.cs:700-815`, `EditorInteractions.cs:802-880`
and `:1954-1980`, core `a11y.rs:791, 880-887, 2086, 2134-2139`, and
`a11y_trigger_designations.json:189-203, 478-486, 623-671`. Replace the
three host-composed task sentences with `A11yEvent.TaskToggled` /
`TaskToggleFailed` (detail through `SlateUniffiMethods.VaultErrorDetail`,
status text through `A11yRender`), and post `NothingToActivateAtCursor` from
`ActivateAt`'s three `false` arms for the `Keyboard` origin only (thread
`origin` into `ActivateCanonicalSpan`). Write the facts named in Tests
first and watch them fail; update the text pins in `TasksPanelTests` and
`TasksReviewTests` to typed matches. Remove the `proposed` designations and
the residue entries, regenerate the ledger, run the two parity-census facts.
Done when the Windows lane is green and the NVDA spot (three phrases) is in
the PR body. Commit: `fix(windows): task toggles and an empty ctrl+enter
speak core's events instead of host-composed text`.
