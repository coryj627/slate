# DS-14 — The window can be 500 wide, and panes make room

Issues: filed by DS-0. Size M. Lanes: windows. AT: NVDA spot. Wave 4. Depends on: DS-6 (sheets' `MaxWidth`), DS-7 (hidden panes), DS-12. Decision: none. Interlock: DS-15's and DS-17's compact states are measured against this.

## Goal

Snap Layouts can put Slate in a half or a quarter of a small screen. Below the width the shell needs, the right pane steps aside, then Files, without losing what the reader was doing.

## Current state on main

- `MainWindow.xaml:13`: `MinHeight="480" MinWidth="760"`.
- `MainWindow.xaml:937-941`: sidebars `MinWidth="200"`, the editor `MinWidth="300"`, two 5 splitters. So with both panes the shell needs 710 at the minimum.
- `WorkspaceViewModel.SidebarWidths.cs:60-72`: a narrower row brings widths back in, and `:119-129` computes the room. Nothing ever hides a pane.
- `IsRightPaneVisible` isn't persisted (`WorkspaceViewModel.cs:2819`), and its setter announces (`:2826`).

## Reference

`design/windows/wpf-feasibility.md:114`: 500 or less, 330 if possible, so Snap Layouts work; "panes must collapse below that width". `NoteHeader.prompt.md`: compact below 520.

## Design

1. **`MinWidth="500"`.** `MinHeight` stays. 330 isn't taken here: the menu row, tab strip and sheets aren't designed for it. Record that in `wpf-feasibility.md:114`.
2. **What the properties mean.** `IsRightPaneVisible` and `IsFilesSidebarVisible` stay "is it showing", because their readers mean that: reveal routes (`WorkspaceViewModel.History.cs:78`, `WorkspaceViewModel.Connections.cs:187`) and DS-7's two-way toggles. New private fields `_rightPaneWanted`, `_filesSidebarWanted` and `_lastAskedPane` hold what the reader asked for. Every existing write through the setter is an ask: it sets wanted and `_lastAskedPane`, and it announces as today.
3. **The rule, pure.** `PaneRoom.Decide(rowWidth, filesWidth, rightWidth, filesWanted, rightWanted, lastAsked)` returns which panes show, so a fact can run it over numbers. A wanted pane shows if it fits beside the editor's 300 and the other wanted pane. When only one fits, `lastAsked` wins; with no ask, Files wins, then the right pane. If the asked-for pane doesn't fit beside 300 even alone, the editor's floor gives way to the row less the pane, so a command still does what it says. `WorkspaceRowWidth`'s setter (`WorkspaceViewModel.SidebarWidths.cs:66-75`) runs it and writes the two "showing" values on a private path that leaves wanted alone. Because `lastAsked` persists across size changes, the next `SizeChanged` can't undo an ask.
4. **Coming back.** Widening shows wanted panes again, silently.
5. **Focus.** A collapse with focus inside the pane moves it to the editor, as DS-7's hide does. It speaks core's existing `RightPaneHidden` or `FilesSidebarHidden`, only when focus was inside: a reader whose pane just vanished needs to hear it, and a resize doesn't otherwise speak. Each new post site gets its row in `38_notification_dispatcher_contracts.md` (`A11yTriggerParityCensus.cs:56-85`).
6. **The ring.** `ShellRegionLayout` reads the visible state.
7. **Sheets and rows.** Sheets already have `MaxWidth` and margins (DS-6). The menu row wraps through its `WrapPanel` until DS-13 makes it one item. Check the Welcome view (`MainWindow.xaml:836-840`: a fixed 36 and 520 column), which becomes a `MaxWidth`.

## Tests

Failing first:

- `PaneRoomDecides`: a pure theory over row widths around the thresholds: the right pane steps aside first, then Files; an ask wins; the next width change doesn't undo it.
- `WideningRestoresWhatTheUserHadOpen`, `AUserHiddenPaneStaysHiddenWhenWidened`.
- `AskingForACollapsedPaneShowsIt`, and `AHistoryRouteRevealsThePaneInANarrowWindow`: the reveal routes still work.
- `TheToggleReadsWhatIsShowing`: a collapsed pane's toggle isn't pressed.
- `ACollapseWithFocusInsideMovesItAndSpeaks`, `ACollapseWithFocusElsewhereIsSilent`.
- `TheWelcomeViewFitsAtFiveHundred`.
- `ShellRegionRingTests` with collapsed shapes.

Launched app: 500, 640, 760, 960 wide, a note open and each sheet, dark; Snap into a half and a quarter by hand.

## Docs and registers

`wpf-feasibility.md:114`.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~SidebarWidth|FullyQualifiedName~ShellRegion|FullyQualifiedName~ModalSurfaceTests"`.

AT: NVDA, focus in the right pane, shrink the window past the threshold: focus and sentence; F6 round the ring at 500.

## Acceptance

- [ ] Snap to half on a 1280-wide screen works.
- [ ] Nothing clips at 500 in the discovery pass.
- [ ] The user's pane choices survive a narrow spell.

## Brief

Read `WorkspaceViewModel.SidebarWidths.cs`, `MainWindow.xaml:935-945`, DS-7's column styles, `wpf-feasibility.md:114`. Add the wanted fields, the pure rule, the private write path and the focus move, plus the plan 38 rows. Facts first, over numbers. Commit: `feat(windows): the window can be 500 wide, and panes step aside to make room`.
