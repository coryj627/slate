# PR 9 — Finish a landed-but-unindexed create instead of reporting it as a failure

Issue: #1140 (#1123 follow-up). Size M. Lanes: swift, a11y, windows,
rust. D9 decided 2026-10-16: core owns the caveat.
PR 10 depends on this. Parallel with PRs 8 and 11.

## Goal

Every mac create funnel calls `createExclusiveReporting` and finishes as
a create on the `PublishedUnindexed` arm: the ordinary created sentence,
then the caveat, open the note, never a retry under another name. Today
a post-publish index failure is caught as a failure and re-presents the
name sheet while the file exists (contract 30 TR-5).

## Current state on main

- Core: `create_exclusive_reporting(path, content) -> CreateExclusiveOutcome`
  (`crates/slate-uniffi/src/lib.rs:1454`); `CreateExclusiveOutcome` is
  `Committed { report }` or `PublishedUnindexed { path, content_hash,
  error_message }` (`:5406-5415`). `create_exclusive` keeps its one-error
  shape. The `SLATE_TEST_FAULT_AFTER_WRITE` seam (path substring) trips
  the post-publish arm.
- Mac call sites of `createExclusive` (all single do/catch):
  - `AppState.swift:16519` the untitled note (⌘N, `uniqueUntitledName`);
  - `AppState.swift:16578` `createNote(in:)` (`:16554`);
  - `AppState.swift:16702` the candidate-path loop (folder note or
    duplicate; read the enclosing function and name it in the PR);
  - `AppState.swift:23642` `performCreateNoteFromTemplate` (`:23618`);
  - `AppState+History.swift:586` Restore As… from a version
    (`recoverDeletedFileAs` at `:579` is a different core call and stays).
  - `AppState.swift:18005` is the file-drop import funnel (text or
    `createExclusiveBytes`); it has its own batch report and is out of
    scope (say so in the PR).
- Core already renders the save-path twin: "Saved, but not indexed yet:
  {detail} It will appear after the next scan." (`a11y.rs:3338`).

## Windows reference

- `apps/slate-windows/src/SlateWindows/CreateOutcomes.cs:1-50`:
  `CreateReporting(session, path, content, displayName)` returns the
  caveat (null when committed) and lets a refusal throw as before;
  `PublishedUnindexedCaveat(name, reason)` = "{name} was written but not
  indexed: {reason} It will appear in listings after the next scan; do
  not recreate it." (host-composed, a W0.5-3 residue on Windows).
- Consumers: the sidebar untitled note and folder note, duplicate,
  history Restore As…, the template flow, and the hashless save
  (`WorkspaceViewModel.cs:1039`, PR 10's I8).
- Contract 30 TR-5 (`:461`) records the retirement.

## Design (D9: core owns the caveat)

- **Core owns the caveat.** Add an `A11yEvent` variant
  beside the save twin (name it to match that variant's family, e.g.
  `CreatePublishedUnindexed { name, reason }`), rendered byte-identically
  to Windows' current string, priority medium (spoken after the created
  sentence, never instead of it), with a corpus row. Both hosts consume
  it in this PR: mac posts it; Windows' `CreateOutcomes.PublishedUnindexed
  Caveat` becomes a render of the event and loses its residue marker
  (the C# facts that pin the string keep passing because the bytes are
  unchanged). This is an additive enum variant: regenerate both
  bindings; no Windows switch needs a new arm because uniffi's C# enums
  are class hierarchies.
- Rejected alternative: a mac host-composed twin with a `W0.5-3 residue`
  marker and a lock-step golden.
- One Swift helper mirrors `CreateOutcomes.CreateReporting`:
  runs the create, returns the caveat (or event) on the landed arm, and
  rethrows a refusal so `DestinationExists` keeps driving the
  auto-suffix and the name sheet.
- Each funnel: on `Committed` or `PublishedUnindexed`, announce the
  ordinary created sentence (the existing event), then the caveat on the
  landed arm, open the note, record structural history as today; never
  loop back to the name sheet on the landed arm.

## Changes

- Core: `a11y.rs` variant, template, priority, corpus
  row; `lib.rs` if the enum mirror needs an arm; regenerate bindings.
- `apps/slate-mac/Sources/SlateMac/`: the helper (new file beside the
  create funnels or in `AppState+Create…`), the five call sites.
- Windows: `CreateOutcomes.cs` renders the event.

## Tests

(`CreateNoteRaceTests`, `TemplateCursorRequestTests` or
`HistoryPanelTests`, driven by `SLATE_TEST_FAULT_AFTER_WRITE` the way
`save_text`'s facts are.)
- `testAnUnindexedUntitledCreateFinishesAsACreate`: the note exists, the
  created sentence then the caveat are posted in that order, the note is
  open, no second create happens, the name sheet is not re-presented.
- The same shape for `createNote(in:)`, the template flow and Restore As….
- `testARefusedCreateStillPresentsDestinationExists` (regression: the
  auto-suffix path).
- Corpus census: the new row is consumed by mac and by Windows.

## Accessibility

The caveat is spoken after the created sentence at medium priority so
the created sentence (high) wins the tab-switch race as today.

## Docs and registers

`docs/plans/30_templates_contracts.md` TR-5 (`:461`): the mac half
retired. The Windows residue retirement is noted
in contract 40's PR 7 record (or wherever `CreateOutcomes`' residue is
registered).

## Acceptance

- [ ] All five mac funnels use the reporting create and finish on the
      landed arm.
- [ ] The facts above pass with the fault seam.
- [ ] TR-5 mac half retired; lanes green (rust and windows too).

## Risks

A funnel that opens the note before the announcement reorders the two
sentences; keep the Windows order (created, caveat, open). The PR
touches core and Windows; keep the string bytes identical so no Windows
fact changes.

## Brief

Adopt core's typed create outcome on mac. Read `lib.rs:1440-1475` and
`:5406-5415`, the five mac `createExclusive` sites listed above, the
Windows `CreateOutcomes.cs` and `WorkspaceViewModel.cs:1020-1052`, and
contract 30 TR-5. Per D9, add the core event for the
caveat with Windows' exact wording and consume it on both hosts; add a
Swift helper mirroring `CreateOutcomes.CreateReporting`; convert the five
funnels so a `PublishedUnindexed` outcome announces created-then-caveat,
opens the note and never retries under another name, while a refusal
still throws `DestinationExists`. Write the fault-seam facts first. Leave
the import funnel (`:18005`) alone and say so. Retire TR-5's mac half.
Done when the facts pass and every lane the change touches is green.
