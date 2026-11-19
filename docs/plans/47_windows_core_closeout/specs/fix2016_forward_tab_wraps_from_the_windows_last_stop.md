# #2016: Forward Tab wraps from the window's last stop

Issue: #2016. PR: #2017, merged 2026-11-19 as 57b9fbbb. Taken early in wave C7 as a standalone fix, not a CB chunk. Lanes: windows (the app job and the shell gate). AT: an NVDA spot check on base and head. Review: two Opus xhigh red teams, Codex adversarial-review rounds 1 to 5 (approve at rounds 1, 4 and 5), and merge judge 1 (Merge).

`S/` below is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`; `G/` is `apps/slate-windows/tests/SlateWindows.AccessibilityTests/`.

## The defect

With a vault open, Tab on the window's last stop moved nothing: More panels with the right pane open, the canvas outline with it closed. Shift+Tab worked.

`WelcomeRoot` carried `FocusManager.FocusedElement="{Binding ElementName=OpenVaultButton}"`. WelcomeRoot is neither a focus scope nor focusable, so WPF's `KeyboardNavigation.GetFirstChild` delegated to that element without checking its visibility. WelcomeRoot is collapsed under an open vault and is the first child of the grid it shares with WorkspaceRoot. So WPF's search for the window's first stop went into the hidden button, climbed out, and never reached the workspace. W0 had put the binding on the Window; W1 (#1010) moved it to WelcomeRoot.

## Step 0, as run on main 5b890dd9

All runs used the app, driven by UI Automation under the desktop lock.
- Launch with no vault: UIA focus is Open Vault.
- With board.canvas open: Tab from More panels, and from the canvas outline with the right pane closed, stays put.
- The binding removed with nothing in its place: the wrap works, but the app launches with UIA focus on the bare window. So the binding is what gives the first launch its focus, and it has to sit somewhere Tab does not follow.

## Design

1. The binding moves to the Window (`S/MainWindow.xaml`). The Window is a focus scope and focusable, so `KeyboardNavigation` never delegates to its focused element. Launch focus and its timing are unchanged.
2. The dead end had also ended WPF's directional (arrow) search through the window. Three stops judge their own arrows in the window's group, and had kept them only because of it:
   - **The empty editor stop**, the content pane's border itself. The shell gate's `RegionStops_ArrowsStayInRegion` failed on the PR's first CI run.
   - **The menu bar's logo**, the bar's one header. The bar's `None` mode refuses an arrow coming in but lets one go out. A red team found it.
   - **The rail's ends**: Down on More panels and Up on the bare strip. A sweep found them.
3. The border and the logo get a KeyDown keeper (`S/MainWindow.ShellRegions.cs`, `KeepTheLoneStopsArrows` and `KeepTheArrow`, registered in `S/MainWindow.xaml.cs`).
   - It keeps an arrow, with any modifiers, while the stop itself is the `OriginalSource`.
   - A chord a window `KeyBinding` takes is left to that binding (`AWindowKeyBindingTakes`). A binding takes a chord when its command is not null, is not `NotACommand`, and its gesture matches, as WPF's `InputBindingCollection.FindMatch` and `CommandManager.TranslateInput` decide.
   - Two alternatives were rejected. A window-level handler would break ShellSealAdmissionCensus, which requires every window input handler to be the seal's admission. A Contained wrapper around the border would break the reading harness, which lifts the pane from a Panel parent.
4. The rail's strip and More panels sit in a Contained `StackPanel`, the status bar's shape (`S/MainWindow.xaml`). A keeper on More panels was rejected: it would also have kept Up, which enters the strip on main.

No spoken text is added. No `A11yEvent` site moves. The parity matrix does not change.

## The arrow sweep

A temporary hosted fact took every visible, enabled, focusable element, one fresh shell per element, and pressed the four arrows through the input manager. The layouts were:
- no file open and a note open, through the real lifecycle;
- a canvas open, through the shown shell.

| Layout | Elements | Presses | Binding move alone differs from main | Head differs from main |
|---|---|---|---|---|
| No file | 39 | 156 | 6 | 0 |
| A note | 52 | 208 | 8 | 0 |
| A canvas (shown shell) | 52 | 208 | 0 | 8, each one main lets out and the head keeps |

## Intended changes (the lead's rulings)

| Case | Main | Head |
|---|---|---|
| Canvas harness: the logo's Left and Right, the border's four arrows, the rail's ends | leave the stop | stay |
| No vault: Alt onto the logo, then Left | into the welcome view | stays on the logo |
| No vault: Alt onto the logo, then Ctrl+Alt+Left or Ctrl+Alt+Shift+Left | into the welcome view | stays on the logo |
| The welcome view with a recent vault: Alt onto the logo, then Shift+Tab | Open Vault, skipping the recents | the last recent vault, the welcome view's true last stop |

## Facts

New `T/WindowTabWrapTests.cs`:
- The wrap, with the right pane open and closed.
- Launch and return focus.
- The empty pane's stop keeps its arrows, and keeps any chord no binding takes.
- The logo keeps Left and Right, and keeps unbound chords.
- The pane chords and Move Tab still reach the window's bindings.
- The rail's ends stay, and Up on More panels still enters the strip.
- An arrow from inside the pane still moves within it.
- The two welcome rows, and the no-vault chords.

New in `T/ShellContainerLandingTests.cs`: the empty editor stop and the logo, each lifted between buttons on every side.

Each keeper, the rail group, the `OriginalSource` check, the binding exemption and the command check fails its own facts when removed.

The logo facts skip on CI's runners through the #1680 guard, which refuses menu focus there. Their evidence is the local Release run, on a desktop that grants menu focus: 43 passed, 0 skipped.

## Found on the way

- Deferred to #1955: with the command palette open, `ActivateFromExternalRequestAsync` calls `Window.Focus()` and moves the keys to the bare window. A probe confirmed it, and main does the same.
- A one-off local failure of `LayoutItemsControlTests.AChangedSourceLeavesNoStalePeerOnTheHost(refill)` in one full run, which touches nothing this PR changes. Its class passed 3 of 3 alone straight afterwards. It was not counted.

Count: found 5, fixed here 4, deferred 1, filed 0.

## What delivery changed

- Moving the binding in `MainWindow.xaml` alone was not enough. The fix also needs the two keepers, the constructor call that registers them, and the rail's group.
- The return to the welcome view does not rest on `ViewModel_ReturnedToWelcome` alone. The WorkspaceRoot gone landing also lands on Open Vault.
- The fixes found in review:
  - Codex round 2 found the logo's keeper swallowing Move Tab, so bound chords are now exempt.
  - Merge judge 1 found that with no vault every arrow binding's command is null, so the exemption now checks the command.
- One CI attempt on 2a890bcf had a single hosted-runner failure of `G/` `ThemedShell_TheMenuBarAndTheOpenTabAreReadable`. It did not reproduce locally on the head or on main (3 of 3 each), its route never reaches the keepers, and the rerun passed.
- Not delivered:
  - Narrator and VoiceOver passes.
  - CI runs of the logo facts (the #1680 guard).
- Not taken: the red team's suggested census rule, that every focusable region root names what keeps its own arrows.
