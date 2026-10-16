# PR 6 — Command palette: Home, End, Page Up, Page Down

Issue: #1105 (PD-1 convergence, owner call 2026-08-13). Size S to M.
Lanes: swift, a11y, windows. Depends on nothing. Parallel with PRs 1 to
5 and 7.

## Goal

The mac palette moves its selection with Home, End, Page Up and Page
Down exactly as the Windows palette does (first, last, clamped page of
ten, the same no-selection start rule), bare-modifier only, while every
modified arrow (VoiceOver Quick Nav, Shift selection) and every key typed
during IME composition still passes through.

## Current state on main

- `CommandPaletteModel.swift:164-180`: `selectNext()` / `selectPrevious()`
  cycle over `displayOrder` (sectioned, deduped) with wrap; no first,
  last or page moves.
- `CommandPaletteView.swift:492-493`: `arrowModifierMask = [.shift,
  .control, .option, .command, .function]`; `shouldPassThroughArrow
  (keyCode:modifierFlags:)` (`:513-519`) passes through any non-navigable
  key and any arrow with a mask modifier held, so fn+↑/↓ (Page Up/Down on
  Apple keyboards) reaches the text field. The IME guard
  (`:571-574`, `editor.hasMarkedText()`) passes everything through during
  composition. Escape and bare arrows are consumed.
- `Tests/SlateMacTests/CommandPaletteViewTests.swift:236-252`
  `testFnArrowPassesThroughForPageNav` pins the current passthrough;
  `:257` `testShiftArrowPassesThroughForSelectionExtend` and `:274-285`
  `testCtrlOptArrowPassesThroughForVoiceOverQuickNav` must keep passing.

## Windows reference

- `apps/slate-windows/src/SlateWindows/CommandPaletteViewModel.cs:448`
  `PageSize` (default 10); `:722` `SelectFirst` (rows non-empty → first);
  `:731` `SelectLast`; `:745-760` `MovePage(delta)`: no rows or delta 0 →
  return; no selection → page down selects the first row, page up the
  last; otherwise `Clamp(index + delta * PageSize, 0, count - 1)`,
  clamped, never wrapping.
- `MainWindow.Palette.cs:225-275` `HandleCommandPaletteKey`: only when
  `modifiers == ModifierKeys.None`; Home → SelectFirst, End → SelectLast,
  PageDown → MovePage(1), PageUp → MovePage(-1); every modified key
  passes through (contract P7). Contract 28 PD-1 (`:293`): "This entry
  retires when #1105 lands".

## Design

1. **Model.** `selectFirst()`, `selectLast()`, `movePage(_ delta: Int)`
   with `pageSize = 10` (a `var` so a test can shrink it), over
   `displayOrder`, with Windows' exact rules: empty → no-op; no selection
   → first (page down) or last (page up); otherwise clamp. `selectNext`
   and `selectPrevious` keep their wrap (that is the existing mac
   behaviour and Windows' `MoveSelection` wraps too).
2. **Keys.** The keyDown monitor handles Home (115), End (119), Page Up
   (116), Page Down (121) and the fn+arrow form bare-modifier only.
   Verify with a real `NSEvent` what fn+↑ delivers on an Apple keyboard
   (key code 116 with `.function`, or 126 with `.function`) and handle
   both forms through one pure function, `pageNavigation(keyCode:
   modifierFlags:) -> PageMove?`, that returns nil whenever any modifier
   other than `.function` is held. Remove only `.function` from the
   passthrough mask for the arrow codes; `.shift`, `.control`, `.option`
   and `.command` arrows still pass through (Quick Nav is
   Control+Option+arrow). Keep the IME guard first.
3. **Order of checks in the monitor:** IME guard, Escape, page
   navigation, bare arrows, else pass through.

## Changes

- `CommandPaletteModel.swift`: the three methods and `pageSize`.
- `CommandPaletteView.swift:486-525`, the monitor around `:566-600`.
- `CommandPaletteViewTests.swift:236-252`: flip to "fn+arrow is a page
  move"; add the facts below.

## Tests

- Model (`CommandPaletteModel` tests, new or in `CommandPaletteViewTests`):
  `testHomeSelectsTheFirstVisibleRow`, `testEndSelectsTheLastVisibleRow`,
  `testPageDownMovesTenAndClampsAtTheEnd`,
  `testPageUpMovesTenAndClampsAtTheStart`,
  `testPageDownWithNoSelectionStartsAtTheFirstRow`,
  `testPageUpWithNoSelectionStartsAtTheLastRow`,
  `testPageMovesCrossSectionsInDisplayOrder` (the flat cross-section
  cycle).
- Key routing (pure function facts): bare Home/End/PageUp/PageDown map;
  fn+↑/↓ map; Shift+Home and ⌘+End pass through;
  `testCtrlOptArrowPassesThroughForVoiceOverQuickNav` unchanged; marked
  text passes everything through.

## Accessibility

VoiceOver Quick Nav (Control+Option+arrows) and the Shift-selection
chords are untouched by construction (the pure function's modifier
rule). The focused row announcement path is unchanged. No field pass
required; mention in the PR that the Quick Nav regression fact still
passes.

## Docs and registers

`docs/plans/28_palette_contracts.md` PD-1 (`:293-302`): retired, with
the landing PR named; `:117` cross-reference updated.
`docs/runbooks/voiceover-feature-test.md` §Q (palette) gains one line:
Home/End/Page keys move the selection.

## Acceptance

- [ ] The model facts and the routing facts pass; the two passthrough
      regressions still pass.
- [ ] PD-1 retired.
- [ ] Lanes green.

## Risks

The fn+arrow delivery form is the one empirical question; the pure
function handles both, and the PR records which one the machine
produced.

## Brief

Converge the mac palette to Windows' Home/End/Page Up/Page Down
behaviour. Read `CommandPaletteModel.swift:158-185`,
`CommandPaletteView.swift:486-525` and `:566-600`,
`CommandPaletteViewTests.swift:230-290`, and the Windows
`CommandPaletteViewModel.cs:722-760` plus `MainWindow.Palette.cs:225-275`.
Add `selectFirst`, `selectLast`, `movePage(_:)` with `pageSize = 10`
using Windows' exact clamp and no-selection rules. Route the four keys
(and the fn+arrow form) through one pure function that refuses any
modifier other than `.function`; keep the IME guard and the
Control+Option Quick Nav passthrough. Flip
`testFnArrowPassesThroughForPageNav` and add the facts under "Tests"
first. Retire PD-1 in contract 28 and add the runbook line. Done when
the facts pass, the Quick Nav fact still passes, and the lanes are green.
