# PR 7 — Search overlay: arrow navigation, Esc focus restore, ⌘Return, truthful comments

Issue: #1113 (SD-1, SD-2, and the five stale comments; D2 decides
⌘Return). Size M. Lanes: swift, a11y, windows, plus a VoiceOver check on
the overlay. Depends on nothing. Parallel with PRs 1 to 6.

## Goal

The vault-search overlay drives its results with the arrow keys, gives
focus back to where the user came from on Escape, honours ⌘Return the way
its own comments claim, and stops lying in its header comments. Each of
these is what the Windows overlay already does (contract 29 SD-1, SD-2).

## Current state on main

- `SearchOverlay.swift:7-8`: "Esc closes and returns focus to whatever
  element the user came from" — not implemented; `makeFirstResponder`
  appears nowhere in the search path (the only mention is a comment at
  `AppState.swift:8915`). The palette restores focus because it is a
  `.sheet` (`MainSplitView.swift:800`) and AppKit restores on dismissal;
  the overlay is laid inline (`MainSplitView.swift:201`), so nothing
  restores it.
- `:11`: "Field announces `Search vault.` on focus" — the label at `:126`
  is "Search vault", no period.
- `:28-29`: "arrow keys cycle within results" — there is no
  `onMoveCommand`, no arrow `onKeyPress`, no key code 125/126 handling.
  Traversal is Tab only; with a query present Clear and Close sit between
  the field and the list, so it is three Tabs to the first row
  (`docs/runbooks/voiceover-feature-test.md:141`).
- `:511`: the row label comment says `"<filename>, line <N>: <snippet>"`;
  the code at `:523` emits `"<filename>: <snippet>"` (the line was dropped
  in #92).
- `:566-580`: the key monitor activates a focused result row on Return
  (36) or keypad Enter (76) only when `modifierFlags.intersection(
  .deviceIndependentFlagsMask).isEmpty` (`:571`) and the palette is not
  open; so ⌘Return is rejected.
- `AppState.swift:9573-9574`: "Close the overlay first so focus moves
  cleanly back to the content area" — nothing moves it;
  `:9577-9578`: "honor a live ⌘ modifier (row ⌘-click or ⌘Return through
  the overlay's key monitor)" — ⌘-click works, ⌘Return does not
  (`openTargetFromCurrentEvent()` at `:9579` reads `NSApp.currentEvent`).
- Focus is a SwiftUI `@FocusState` with `.result(index)` cases (`:510`);
  the focused row is announced by `announceFocusedResult` (#418 F-A1).
- The runbook's step E.3 says "Tab three times" (`:141`) and §4 says
  "Return on a focused result row is a no-op — Space activates rows;
  #414" (`:93`), which the `:566-580` monitor (#422 F-E2) made stale.

## Windows reference

- `MainWindow.Search.cs:168-207` `HandleSearchOverlayKey`: bare modifiers
  only; Escape → Close; Enter → `ActivateSelected` unless a Button is
  focused; Down/Up → `MoveSelection(±1)`.
- `Search/SearchOverlayViewModel.cs:484-500` `MoveSelection`: gated on the
  Results state (never on a hidden list), each move announces through
  `RowSelected` (S8).
- `MainWindow.Search.cs:209-240` `RestoreFocusAfterSearch`: restores the
  element captured at open; skips when a modal surface superseded the
  overlay or when activation claimed focus (the editor owns it after an
  open).
- Contract 29 SD-1 (`:405`), SD-2 (`:415`); D2 in plan 41 §5: "the
  overlay's monitor claims ⌘Return only while a result or recent row is
  focused and passes it through otherwise" (no VoiceOver command is a
  plain ⌘Return; the only in-app ⌘Return binding is the properties-source
  Apply button, window-scoped).

## Design

1. **Arrows (SD-1).** In the results state, Down moves focus from the
   field to the first result row and between rows; Up moves back and,
   from the first row, to the field. Movement sets the `@FocusState`
   (`.result(i)`), so the existing `announceFocusedResult` speaks each
   row (the S8 rule; silent arrows are the defect Windows hit in round
   1). Implement in the same key monitor as Return (key codes 125/126,
   bare modifiers only, IME guard first, palette-open guard kept) so
   Control+Option+arrows (Quick Nav) pass through. In the idle state the
   recent rows get the same treatment if they are focusable rows today;
   otherwise leave idle alone and say so.
2. **Restore (SD-2).** On open, capture `window.firstResponder` (the
   `NSWindow` hosting the overlay, from `NSApp.keyWindow`). On Esc-close,
   after the overlay leaves the view tree, `makeFirstResponder(captured)`
   if the window still has it in its responder chain; on activation
   (open a hit) do not restore (the editor claims focus). If the palette
   or a sheet is open at close time, skip the restore (Windows'
   stand-down).
3. **⌘Return (D2).** The guard at `:571` admits an empty modifier set, or
   exactly `.command`, for key codes 36/76, and only while a result or
   recent row is focused; a ⌘Return anywhere else passes through
   untouched. The comment at `AppState.swift:9577-9578` then states the
   truth.
4. **Comments.** Fix `:7-8`, `:11`, `:28-29`, `:511` and
   `AppState.swift:9573-9574` to describe what the code does after this
   PR.

## Changes

- `SearchOverlay.swift`: the monitor (`:560-600`), the open/close
  capture-and-restore, the header comments, the row-label comment.
- `AppState.swift:9570-9585`: the two comments; `toggleSearchOverlay` /
  `closeSearchOverlay` carry the captured responder if that is where
  open/close live.
- `docs/runbooks/voiceover-feature-test.md:93` and `:141`.

## Tests

(`SearchErgonomicsTests` or a new `SearchOverlayKeyRoutingTests`; the
routing decisions are pure functions like the palette's.)
- `testDownArrowMovesFocusFromTheFieldToTheFirstResult`,
  `testArrowsMoveBetweenResultsAndUpReturnsToTheField`,
  `testArrowsAreIgnoredOutsideTheResultsState`,
  `testControlOptionArrowPassesThroughForQuickNav`.
- `testEscapeRestoresTheFirstResponderCapturedAtOpen`,
  `testActivationDoesNotRestoreFocus`,
  `testRestoreIsSkippedWhenThePaletteSupersededTheOverlay`.
- `testCommandReturnOnAFocusedRowOpensInANewTab` (via
  `openTargetFromCurrentEvent`), `testCommandReturnOnTheFieldPassesThrough`,
  `testPlainReturnOnTheFieldStillActivatesTheTopResult`.
- A source fact that the four comments contain the new wording is not
  needed; review covers prose.

## Accessibility

A VoiceOver field pass on the overlay: open with ⇧⌘F, type the bait
phrase, Down arrow → the first row speaks "<file>.md: …", Esc → the
editor announces again ("Note content for <file>. edit text" per the
runbook's palette round-trip), ⌘Return on a row opens in a new tab and
speaks the tab. Record the phrases in the PR; update the runbook steps.

## Docs and registers

`docs/plans/29_search_overlay_contracts.md` SD-1 (`:405`) and SD-2
(`:415`) retired, `:547` updated; D2 recorded as delivered in plan 41 §5
(a one-line note). Runbook `:93` and `:141` corrected.

## Acceptance

- [ ] Arrows, restore and ⌘Return behave as designed, with the facts.
- [ ] The five comments are true.
- [ ] VoiceOver phrases recorded; runbook updated; SD-1/SD-2 retired.
- [ ] Lanes green.

## Risks

Focus restore in an inline overlay depends on the responder still being
in the chain after SwiftUI removes the overlay; restore on the next run
loop turn (`DispatchQueue.main.async`) after the state change, as
Windows does with `Dispatcher.InvokeAsync`, and fall back to no restore
rather than focusing something else.

## Brief

Bring the mac search overlay to the Windows overlay's keyboard contract.
Read `SearchOverlay.swift` (header, `:505-530`, `:560-600`),
`AppState.swift:9570-9585`, `MainSplitView.swift:201` and `:800`, the
runbook lines `:93` and `:141`, and the Windows `MainWindow.Search.cs:
168-240` and `SearchOverlayViewModel.cs:484-500`. Add Up/Down result
navigation through the existing focus state so each move announces;
capture the first responder at open and restore it on Escape (not on
activation, not when a palette or sheet superseded the overlay); admit
⌘Return only on a focused result or recent row (D2) and pass it through
otherwise; fix the five stale comments. Write the facts under "Tests"
first. Do a VoiceOver pass and record the phrases. Retire SD-1 and SD-2
in contract 29 and correct the runbook. Done when the facts pass, the
VoiceOver phrases are in the PR, and the lanes are green.
