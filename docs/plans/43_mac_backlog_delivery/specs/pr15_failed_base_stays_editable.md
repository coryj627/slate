# PR 15 — A failed Base stays editable and says what failed

Issue: #786. Size M. Lanes: swift, a11y, windows. Depends on nothing.
Parallel with PRs 13, 14, 15b, 25.

## Goal

A saved `.base` whose definition is empty, incomplete, or whose selected
view cannot load shows an actionable error and keeps its editing
affordances enabled, so the user can repair it. Today the surface is a
bare placeholder and Edit filters is disabled: a trap.

## Current state on main

- `Bases/BaseContainerView.swift:214-219`: `content` renders `.failed(let
  message)` as `placeholder(message)`; `.loading` as "Loading base…".
- `:118-126`: the Edit filters button is `.disabled
  (baseDefinitionEditingDisabledReason != nil)` with the reason as its
  hint and help; `:644-646` reads `appState.baseDefinitionEditingDisabled
  Reason(for: document)`.
- `Bases/AppState+Bases.swift:144-162` `baseDocumentAvailabilityDisabled
  Reason`: retarget-failed and reopening arms first, then `.loading` →
  opening reason, `.failed` → `baseDocumentUnavailableDisabledReason`
  (`:157-158`), `.ready`/`.degraded` → unavailable reason in this branch
  (read the surrounding guard to see when); `:195` `baseDefinitionEditing
  DisabledReason` delegates to it (`:202`), so a failed load disables
  definition editing (#786, confirmed by code reading in plan 41 §1.3).

## Windows reference

Check `apps/slate-windows/src/SlateWindows/Bases/` for the failed-state
rendering and wording (grep "could not be loaded" and the base editing
command's availability), and keep the mac wording consistent with it; if
Windows has the same trap, file a Windows issue rather than fixing it
here (rule 8).

## Design

1. **Availability.** `.failed` with the file present and writable does
   not disable definition editing; only a missing, unwritable or
   retarget-failed file does (the existing arms). Split the reason
   function so the surface's data interactions (sorting, cell editing)
   can still be unavailable while the definition editing is available.
2. **Error state.** `content` for `.failed` renders an error view: a
   header "This base could not be loaded", the message (core's, not
   parser internals: pass it through `humanReadableVaultError` if it is
   a `VaultError`), one sentence "Edit the base definition to repair it.",
   and an Edit button bound to the same action as the toolbar's Edit
   filters (or the raw-definition editor if the mac has one; name which
   in the PR). The view is a traversable group with the header as a
   heading; never a blank surface.
3. **Empty definition.** A saved empty `.base` loads as `.failed` (or an
   empty-definition state if core distinguishes it); either way the error
   view and the enabled editing apply.
4. The announcement on open uses the existing base-load-failed event if
   one exists in the corpus; add no copy.

## Changes

- `Bases/BaseContainerView.swift:110-130`, `:214-230`, a new
  `BaseLoadFailedView` in the same file or `Bases/`.
- `Bases/AppState+Bases.swift:144-220`.

## Tests

(`BasesTabRoutingTests`, `BaseQueriesPanelTests`, or a new
`BaseContainerRecoveryTests`.)
- `testASavedEmptyBaseOpensInARecoverableStateWithEditingEnabled`.
- `testAnIncompleteDefinitionShowsTheErrorAndKeepsEditingEnabled`.
- `testAnUnloadableSelectedViewExplainsItselfAndKeepsEditingEnabled`.
- `testEditFromTheErrorStateOpensTheBuilder`.
- `testAMissingBaseFileStillDisablesEditingWithTheUnavailableReason`
  (regression for the arms that must stay disabled).
- `testNoBlankSurfaceForAFailedBase` (the error view is present).

## Accessibility

The error view's header carries `.isHeader`; the Edit button's hint says
what it opens; the message is core's. No field pass.

## Docs and registers

None (no register row). Comment #786.

## Acceptance

- [ ] The six facts pass; the trap is gone.
- [ ] Missing/unwritable files still disable editing.
- [ ] Lanes green.

## Brief

Make a failed Base recoverable. Read `Bases/BaseContainerView.swift:
110-130` and `:214-230`, `Bases/AppState+Bases.swift:144-220`, and the
Windows Bases folder for the failed-state wording. Keep definition
editing enabled when the file is present and writable even though the
load failed; render an actionable error view (heading, core's message,
"Edit the base definition to repair it.", an Edit button) instead of a
bare placeholder; keep the missing/unwritable/retarget-failed arms
disabled. Write the six facts first. Done when they pass and the lanes
are green.
