# DS-12 — The title bar is one 48 row with Snap Layouts

Issues: filed by DS-0. Size L. Lanes: windows, rust, swift (compile). AT: NVDA and Narrator pass. Wave 4. Depends on: DS-4 (the menu fits), DS-5 (the row grows), DS-9 (the buttons), DS-10 (what the settings button opens). Decision: DSD9, DSD13. Interlock: DS-13 moves the menus; until it lands, the menu headers sit in this row.

## Goal

The Windows title bar and the menu bar become one row: the menus, the window title, the palette and settings buttons, and Slate's caption buttons, with Snap Layouts on maximize. Resize, Snap, Win+arrow and Alt+Space keep working because `WindowChrome` keeps them native.

## Current state on main

- **Two rows:** the Windows caption (about 29) and `MainWindow.xaml:110-742` (about 45).
- **No chrome:** nothing in `src` uses `WindowChrome` or `HTMAXBUTTON`. `ShellModalLoopMonitor.cs:82-91` shows how the app adds an `HwndSource` hook. `Window_SourceInitialized` (`MainWindow.xaml.cs:2133-2136`) restores placement.
- **Title:** static `Title="Slate"` at `MainWindow.xaml:11`, and pinned at `ShellAccessibilityTests.cs:197`.
- **What the menu disables under:** `MainWindow.xaml:131-193`, every `ModalSurface` (`ModalSurfaceTests.TheMenuDisablesUnderEveryModalSurface`).
- **F6:** `ShellRegionKind.MenuBar` lands on the first menu item (`MainWindow.ShellRegions.cs:144-152`). `NeitherTabNorAnArrowEntersTheMenuBar` keeps Tab and arrows out.
- **Backdrop off:** `App.xaml.cs:23-25`.

## Reference

`design/windows/readme.md:116-127`: the row's contents, `WindowChrome` settings, caption buttons 46 × 48 with `E921`, `E922`, `E923`, `E8BB`, `SystemCommands`, close hover `#C42B1C` with a white glyph, padding when maximized, accessibility. `wpf-feasibility.md:109-115`. `spikes-issue.md:19-21` and `wpf-feasibility.md:168`: spike 3 passed with `CaptionHeight="48"`, `UseAeroCaptionButtons="False"` and the hook, and its failure follow-up is native caption buttons. `components/shell/CaptionButton.prompt.md`: 10 px glyphs, `inactive` dims. `ui_kits/slate-windows/Chrome.jsx:106-123`, `index.html:61`.

## Design

1. **Chrome.** On `MainWindow`: `WindowChrome` with `CaptionHeight` from the row, `ResizeBorderThickness="{x:Static SystemParameters.WindowResizeBorderThickness}"`, `UseAeroCaptionButtons="False"`, and `GlassFrameThickness` left as spike 3 had it until the by-hand list below has checked the window's shadow and rounded corners, which `readme.md:121` promises. If they're lost, `0,0,0,1` is the usual way to keep them; measure it. `CaptionHeight` follows the row's `ActualHeight` on `SizeChanged`, because the row grows with text size (`MinHeight` 48, 75.9 at 2.25 in spike 5).
2. **The row.** A `Grid` docked at the top, `MinHeight` 48, `Slate.WindowBackgroundBrush`, in this order: `MainMenu`, title (Caption, trimming, the drag region), a palette button and a settings button in `Slate.SubtleIconButton40Style` (`E721`, `E713`), three caption buttons. Hit-test visible in chrome: the menu, both buttons, all three captions. A subtle border below.
3. **Caption buttons.** `Slate.CaptionButtonStyle`, 46 × 48, whole template. `SystemCommands` through `CommandBinding`s. The middle one swaps glyph and name on `WindowState`. All three dim when `IsActive` is false.
4. **Maximize.** Padding on the root from `SystemParameters.WindowResizeBorderThickness` while maximized, so nothing overhangs the screen.
5. **Snap Layouts.** An `HwndSource` hook, added at `SourceInitialized`, returns `HTMAXBUTTON` from `WM_NCHITTEST` over the maximize button, converting physical coordinates through the current DPI. Windows then owns the mouse over that button, so the hook drives its hover and pressed visuals from `WM_NCMOUSEMOVE`, `WM_NCMOUSELEAVE`, `WM_NCLBUTTONDOWN` and `WM_NCLBUTTONUP`, and a release on the button maximizes. Spike 3's list is the checklist.
6. **Keyboard.** The row's buttons are `Focusable="False"` and out of the Tab order, for the reason `MainWindow.xaml:90-109` records about the bar. Each action has a route already: Ctrl+Shift+P, Ctrl+, and Alt+Space. F6 still lands on the first menu header, and the row is still that region (`readme.md:127`). `AutomationProperties.Name` on all five.
7. **Modal surfaces.** The palette and settings buttons disable under every modal surface, by the same list as the menu. Captions never do. DS-6's thin scrim layer over the menu's row becomes one over this row, `IsHitTestVisible="False"`, so captions still take the pointer.
8. **The title,** per DSD9: core gains `window_title(note: Option<String>, vault: Option<String>) -> String`, giving `Note — Vault — Slate`, `Vault — Slate` or `Slate`. It's what a screen reader hears on the window, so it's core's (plan 46 §4 rule 3). Windows binds `Window.Title`, and the row's `TextBlock` binds to that. Update `ShellAccessibilityTests.cs:197`.
9. **Duplicate caption buttons (DSD13).** With the non-client frame gone, dump the UIA tree: a reader must not meet two Close buttons. WPF has no `AccessibilityView` (plan 46 §4 rule 10), and a native title bar's elements aren't Slate's to change, so the only control here is Slate's own peers. If the native ones remain, Slate's take a peer that isn't a control element, the way `AutomationPresentationTextBlockPeer` does (`AutomationLandmark.cs:174-184`). The pass decides, and the PR records which.
10. **Fallback.** If the hook can't be made reliable, `spikes-issue.md:21`'s follow-up applies: `UseAeroCaptionButtons="True"` with native captions. That goes to the owner, since it drops `CaptionButton` from the design.

## Tests

Failing first:

- `TheTitleRowIsFortyEightAtBodyText`, and `TheRowGrowsWithTextSize` at 2.25 with `CaptionHeight` following.
- `TheChromeButtonsAreHitTestVisibleAndTheTitleIsNot`.
- `HitTestOverMaximizeSaysMaxButton`: a pure function from point, DPI and button bounds, at 100%, 150% and 200%.
- `ThePaletteAndSettingsButtonsDisableUnderEveryModalSurface`: driven by `ModalSurface`.
- `TheMaximizedWindowPadsItsRoot`.
- Rust: `window_title` for the three shapes. C#: `TheTitleFollowsTheActiveNote`.
- Gate: `TitleBar_ButtonsAreNamedAndWindowPatternsWork`: minimize, maximize, restore and close through `Window` and `Invoke`; no private-use names; `FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean` holds.

By hand on the PR's build, recorded in the PR, at 100% and at 200% if the display is there: a real mouse hover on maximize opens the flyout; Win+Z; hover and pressed draw; the window keeps its shadow and rounded corners; a drag moves; a double-click maximizes; a drag to the top edge snaps; resize from all edges; Alt+Space opens the system menu. A multi-monitor move across DPIs, if available.

## Docs and registers

`wpf-feasibility.md:109-115`, `:168`. `readme.md:116-127` if anything differs. `w_c_matrix.md` rows for the row. `#1756`'s optional 200% check for spike 3 is met here.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~TitleBar|FullyQualifiedName~ModalSurfaceTests|FullyQualifiedName~MenuBarCensus|FullyQualifiedName~ShellRegion"`, the gate fact locally, and for core: `cargo fmt --check`, `cargo clippy --workspace --all-targets --locked -- -D warnings`, `cargo test -p slate-core`.

AT: NVDA and Narrator pass: window title on Alt+Tab and focus; F6 to the menu; object navigation across the row (names, one set of captions); the title's change on switching tabs doesn't speak by itself. Record the phrases.

## Acceptance

- [ ] One row, 48 at 100%.
- [ ] Every item in the by-hand list passes.
- [ ] No duplicate Close for a screen reader.
- [ ] The shell gate is green on the lane, where the window is driven through UIA.

## Notes from the build

- **No glass frame (step 1).** WindowChrome's default `GlassFrameThickness` is the caption's height, and with it, or with `0,0,0,1`, the whole window's text lost ClearType: the menu bar measured 0% colour fringes and the palette 3%, against 53% and 38% with none. The gate's `ThemedShell_APalettesTextKeepsItsClearType` caught it. Windows 11 rounds the corners with no glass. The shadow couldn't be measured on this desktop's dark background, so it's on the owner's eye.
- **The hook goes in after WindowChrome's.** WPF asks the newest `HwndSource` hook first, and WindowChrome adds its own from the window's `SourceInitialized`, so a hook added from the window's own handler is never asked over maximize: the window answered client, and Snap Layouts never opened. `MainWindow.OnSourceInitialized` adds it after `base`. `TheWindowAnswersMaxButtonOverMaximize` failed first.
- **The edge is Windows' whole frame.** `SystemParameters.WindowResizeBorderThickness` is the sizing border alone: 2.5 at 200% against Windows' 6.5, so the edge took the pointer over a third of a native window's width, and a drag 3 pixels in missed. The resize zone and the maximized padding take `TitleBarChrome.FrameThickness`: `SM_CXSIZEFRAME`, `SM_CYSIZEFRAME` and `SM_CXPADDEDBORDER` at the window's own DPI, again when the DPI changes, since WPF's property is at the system's. WindowChrome keeps that zone inside the window, where a native window's is outside it, so the outer 6.5 of the content answers to a resize.
- **The flyout covers the button's lower part.** Windows places the Snap Layouts flyout at a fixed depth below the window's top, wherever the pointer is on the button, which suits a standard caption: about 29 with no glass frame (26 with WindowChrome's default glass). Once it's open, a click on the lower 19 of the 48-tall button lands on it. A click before it opens, or anywhere above, maximizes. `WM_GETTITLEBARINFOEX` answered with the real rectangles didn't move it. The owner kept it as it is, with the design's buttons, over step 10's fallback (DSD17).
- **A click runs maximize's command on the button.** A routed command with no target goes to whatever has the keys, which in a window without focus is nothing.
- **The menus wrap beside the captions.** They share the star column with the title, so at the 780 minimum width and 225% text their headers wrap rather than push Close off the window (`AtTheNarrowestWindowAndLargestTextTheCaptionsStayOn`).
- **The title follows the active tab's own title,** so a saved query's or a dashboard's rename, which goes through the tab alone, reaches it.
- **The row stays 48 at 225% text size** in a window wide enough for the menus on one line. DS-4's top-level menu item is 42 there, so nothing grows past 48. `TheRowHoldsTextSizeAndTheCaptionAreaFollowsIt` checks that everything fits, and that a taller row takes the caption area with it, in place of the spec's `TheRowGrowsWithTextSize`.
- **DSD13 measured: one set of captions.** With the non-client frame gone, UIA has no native caption buttons: the gate finds one Close, one Minimize and one Maximize, NVDA's object navigation and Narrator's item navigation meet each once.
- **The title bar's hook is the shell's second.** `ShellSealAdmissionCensus` admits it, reading non-client mouse messages and nothing else. A native caption's clicks never reached the palette's seal either, but the caption buttons are WPF buttons, so under the seal (a palette command running) they don't act, where native ones would.
- **The quick commands and captions are written `Focusable="False"`** on each element, which the region census reads; their styles say it too.

## Brief

Read `design/windows/readme.md:116-127`, `wpf-feasibility.md:109-115`, `:168`, `ShellModalLoopMonitor.cs`, `MainWindow.xaml:1-26`, `:89-135`, `ui_kits/slate-windows/Chrome.jsx:106-123`. Build the chrome and the row with the menu in it, then the hook, then the title through core. Facts first where a fact can reach; the hover flyout is by hand. Commit: `feat(windows): the title bar is one 48 row with snap layouts`.
