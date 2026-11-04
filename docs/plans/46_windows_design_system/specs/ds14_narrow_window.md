# DS-14 — The window can be 500 wide, and panes make room

Issues: filed by DS-0. Size M. Lanes: windows. AT: NVDA spot. Wave 4. Depends on: DS-6 (sheets' `MaxWidth`), DS-7 (hidden panes), DS-12. Decision: none. Interlock: DS-15's and DS-17's compact states are measured against this.

## Goal

Snap Layouts can put Slate in a half or a quarter of a small screen. Below the width the shell needs, the right pane steps aside, then Files, without losing what the reader was doing.

## Current state on main

- `MainWindow.xaml:13`: `MinHeight="480" MinWidth="775"` (760 at the baseline; #1795).
- `MainWindow.xaml:937-941`: Files `MinWidth="200"`, the right pane 250 (200 at the baseline; #1795), the editor `MinWidth="300"`, two 5 splitters. So with both panes the shell needs 760 at the minimum.
- `WorkspaceViewModel.SidebarWidths.cs:60-72`: a narrower row brings widths back in, and `:119-129` computes the room. Nothing ever hides a pane. Since #1795 the row's width comes from `WorkspaceRowHost`, because the columns grid never reports less than the sum of its columns, and each width comes back when there's room again.
- `IsRightPaneVisible` isn't persisted (`WorkspaceViewModel.cs:2819`), and its setter announces (`WorkspaceViewModel.cs:2826`).
- **The setter also runs Connections' lifecycle** (`WorkspaceViewModel.cs:2830`, `WorkspaceViewModel.Connections.cs:116-127`). A hide clears the leaf's selection and bumps `ViewStateEpoch` (`Graph/ConnectionsLeafViewModel.cs:766-774`), which clears expansion and pending focus (`Graph/ConnectionsLeafView.cs:520-528`). A show arms a mount whose consume runs an audible load (`WorkspaceViewModel.Connections.cs:134-145`, `Graph/ConnectionsLeafViewModel.cs:429-435`). `35_graph_contracts.md` records these as Term 2 and Term 3(a), and `ConnectionsLeafCensus.cs:514` holds every `IsRightPaneVisible = true` writer to the consume.
- **A silent reconcile exists.** `Connections.Probe` (`Graph/ConnectionsLeafViewModel.cs:776-785`) loads silently when the presentation is stale or errored, and does nothing when it's current. A note change while unmounted leaves it stale (`Graph/ConnectionsLeafViewModel.cs:458-464`).

## Reference

`design/windows/wpf-feasibility.md:114`: 500 or less, 330 if possible, so Snap Layouts work; "panes must collapse below that width". `NoteHeader.prompt.md`: compact below 520.

## Design

1. **`MinWidth="500"`.** `MinHeight` stays. 330 isn't taken here: the menu row, tab strip and sheets aren't designed for it. Record that in `wpf-feasibility.md:114`.
2. **What the properties mean.** `IsRightPaneVisible` and `IsFilesSidebarVisible` stay "is it showing", because their readers mean that: reveal routes (`WorkspaceViewModel.History.cs:78`, `WorkspaceViewModel.Connections.cs:187`) and what DS-7's toggles show. New private fields `_rightPaneWanted`, `_filesSidebarWanted` and `_lastAskedPane` hold what the reader asked for. Every existing write through the setter is an ask: it sets wanted and `_lastAskedPane`, and it announces as today.
3. **The rule, pure.** `PaneRoom.Decide(rowWidth, filesWidth, rightWidth, filesWanted, rightWanted, lastAsked)` returns which panes show, so a fact can run it over numbers. A wanted pane shows if it fits beside the editor's 300 and the other wanted pane. When only one fits, `lastAsked` wins; with no ask, Files wins, then the right pane. If the asked-for pane doesn't fit beside 300 even alone, the editor's floor gives way to the row less the pane, so a command still does what it says. `WorkspaceRowWidth`'s setter (`WorkspaceViewModel.SidebarWidths.cs:66-75`) runs it and writes the two "showing" values on a private path that leaves wanted alone. Because `lastAsked` persists across size changes, the next `SizeChanged` can't undo an ask.
4. **A narrow spell suspends the pane. It doesn't hide it.** The private path writes the field and raises what depends on it: the property, DS-7's toggle property, the resize and column states, and the inspector toggle. It doesn't call `OnRightPaneVisibilityChanged`, so the reader's Connections selection, expansion and pending focus survive, and no mount is armed. While suspended the leaf counts as unmounted (`WorkspaceViewModel.Connections.cs:80`), so a publication or a note change only leaves the presentation stale. **Coming back:** widening shows wanted panes again, silently, and when the Connections leaf is active the path ends in `Connections.Probe`, so the leaf is current and nothing is spoken. A real root change during the spell still clears the selection, as any root change does. A reader's own hide or show still goes through the setter, so Term 2 and Term 3(a) hold for an ask. `35_graph_contracts.md` gets the suspended state beside Term 2, as an owner's recorded call since that record is frozen, and a census names the private path as the one writer of the field outside the setter, so a second one fails.
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
- `ANarrowSpellKeepsTheConnectionsSelectionAndExpansion`: a selected, expanded tree; shrink and widen; the same row is selected and the same nodes are open.
- `ComingBackFromANarrowSpellSpeaksNothing`: no `RightPaneShown` and no graph summary, with Connections active.
- `APublicationDuringANarrowSpellIsReconciledSilently`: a publication and an in-flight load across the spell; after widening the presentation is current, silently.
- `ANoteChangedDuringANarrowSpellLoadsSilently`: the selection clears, as a root change does, and the new note's tree loads with no summary.
- `AnExplicitHideStillClearsAndAnExplicitShowStillSpeaks`: Term 2 and Term 3(a) are unchanged.
- `TheWelcomeViewFitsAtFiveHundred`.
- `ShellRegionRingTests` with collapsed shapes.

Launched app: 500, 640, 760, 960 wide, a note open and each sheet, dark; Snap into a half and a quarter by hand.

## Docs and registers

`wpf-feasibility.md:114`.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~SidebarWidth|FullyQualifiedName~ShellRegion|FullyQualifiedName~ModalSurfaceTests|FullyQualifiedName~ConnectionsLeafCensus|FullyQualifiedName~ConnectionsLeafViewTests"`.

AT: NVDA, focus in the right pane, shrink the window past the threshold: focus and sentence; widen again with a Connections row selected: the same row, and nothing spoken; F6 round the ring at 500.

## Acceptance

- [ ] Snap to half on a 1280-wide screen works.
- [ ] Nothing clips at 500 in the discovery pass.
- [ ] The user's pane choices survive a narrow spell, and so does their place in Connections.

## Brief

Read `WorkspaceViewModel.SidebarWidths.cs`, `WorkspaceViewModel.Connections.cs:111-145`, `Graph/ConnectionsLeafViewModel.cs:425-464`, `MainWindow.xaml:935-945`, DS-7's column styles, `wpf-feasibility.md:114`. Add the wanted fields, the pure rule, the private write path with its silent return, and the focus move, plus the plan 38 rows. Facts first, over numbers. Commit: `feat(windows): the window can be 500 wide, and panes step aside to make room`.
