# DS-5 — Windows Text size scales the whole shell, live, and nothing clips at 225%

Issues: filed by DS-0; `Refs #1756` (its "Spike 5's source for the text scale" item); notes #752's "system font-size respect". Size L. Lanes: windows. AT: NVDA spot. Wave 1, slot 6. Depends on: DS-2, DS-3, DS-4. Decision: DSD3. Interlock: none. Owner step: the live check in plan 46 §8.

## Goal

Windows Settings › Accessibility › Text size changes every piece of Slate's text at once, with no restart, up to 225%. Icons stay their size. Rows grow rather than clip. There's one owner of the setting, where today there's one per canvas and graph view.

## Current state on main

- **The one reader:** `Canvas/CanvasTextScaleService.cs`. It reads `HKCU\Software\Microsoft\Accessibility\TextScaleFactor` (`:23-24`, `:95-114`), clamps to 1 to 2.25 (`:103-105`), subscribes to `SystemEvents.UserPreferenceChanged` (`:32`) and marshals to its dispatcher (`:71-76`). Its facts call `RefreshForTests` (`:53`), so nothing has shown a real change arrive.
- **Consumers:** one instance per view: `Canvas/CanvasRendererView.cs:37-38`, and lazily `Graph/GraphDiagramView.cs:230-236`, used for drawn labels.
- **The rest of the shell** doesn't scale explicit sizes. Spike 5 (`wpf-feasibility.md:170`) found that text with no size of its own grows through `SystemFonts.MessageFontSize` (27 at 225%). DS-2 gives that text an explicit size from the factor read once at startup, so after DS-2 the ramp follows the setting only across a restart.
- **`ThemeManager` can't be built in a hosted test:** `_application` (`ThemeManager.cs:31`), against `ShownShell.cs:31`.
- **Hosted tests and the setting.** At 225% on the desktop, about 29 shell tests fail because unset sizes become 27. `ThemeManager.DictionariesFor` is what hosted tests merge.
- **Fixed heights on text:** none left in XAML. The `Height=` values are a progress bar (`MainWindow.xaml:805`, `:4078`), a pill (`Themes/Slate.Text.xaml:98`, `:130`), a chevron (`WorkspaceTemplates.xaml:163`) and a thumb (`:995`).
- **Fixed widths that hold text:** `MainWindow.xaml:786`, `:1012`, the sheets (`:2591` and on), `Canvas/CanvasSurfaceView.cs:151`.
- **The design says** `UISettings.TextScaleFactor` (`xaml/README.md:164`, `wpf-feasibility.md:62`). The project's target is plain `net10.0-windows` (`SlateWindows.csproj:5`), which has no WinRT projection; adding one costs 25 MB (plan 46 §2.2).

## Reference

`design/windows/xaml/README.md:162-166`: multiply every UI font size, update on change, don't scale icon glyphs, `MinHeight` not `Height`. `wpf-feasibility.md:62`, `:170`. Spike 5's numbers at 2.25: title bar 75.9, tab strip 45.9, status bar 39.9. `LayoutInformation.GetLayoutClip` says whether an element was clipped to its layout slot.

## Design

1. **One service.** `CanvasTextScaleService` moves to `TextScaleService.cs`, unchanged in substance: `Factor`, `Revision`, `Changed`, `Dispose`, the clamp, the marshal. `App.OnStartup` creates it before `ThemeManager`, and it's handed to the canvas and graph views, so they stop creating their own. Rename the facts with it. Source per DSD3: the registry, behind a `Func<double>` seam.
2. **A test seam for launched runs.** `SLATE_TEXT_SCALE_FOR_TESTS`, honoured only when `SLATE_CENSUS_INSTANCE_ID` is set, as the capture and census modes already key off. `ShellLaunch.ApplyDeviceState` defaults it to `1`, so the shell gate no longer depends on the machine's setting. One gate fact sets `2.25`.
3. **The ramp follows.** `TypeRamp.Apply(ResourceDictionary, double factor)` is the pure part: it writes new values into a ramp dictionary. `ThemeManager` holds the dictionary and calls it on `Changed`, replacing DS-2's startup read. Every `DynamicResource` updates in place, with no re-merge and no `ResourcesChanged`, so theme listeners don't rebuild. `DictionariesFor` gets `double textScale = 1`, and only `ApplyResources` passes the live factor, so hosted tests are independent of the desktop's setting. That also ends the 29 failures at 225%.
4. **Straight multiplication,** as `xaml/README.md:164` says and spike 5 ran, for chrome, reading keys and menus. `wpf-feasibility.md:62` mentions Microsoft's guidance to scale small text more than large; a curve would need its own design, and isn't taken here.
5. **The editor.** `EditorPreferencesViewModel.FontSize` stays the user's logical size: its bounds and its sentence "Editor text size already 14 points" (`EditorInteractions.cs:241-250`) don't change. A new `EffectiveFontSize` is `FontSize * Factor`, and `WorkspaceTemplates.xaml:639` binds to it. The composite's line spacing scales with it (measured at 8 to 40).
6. **What doesn't scale.** Glyphs drawn in `SymbolThemeFontFamily` keep literal sizes. A census lists them. Layout pixels don't scale: pane widths and icons stay.
7. **Rows that grow.** Everything in DS-2's and DS-4's templates is already `MinHeight`. Go through the fixed widths under "Current state" at 2.25. Each either wraps, becomes a `MinWidth`, or moves to a `MaxWidth` with a margin. Sheets scroll past `MaxHeight`, which `MainWindow.xaml:2592` already does for Quick Open.
8. **The reading measure** (DS-3) recomputes when the body key changes.
9. **The canvas and graph** already take `Factor`. Keep their revision path (`CanvasPresentationEngine.cs:137`).
10. **The decision recorded.** `xaml/README.md:164` and `wpf-feasibility.md:62` state the source DSD3 settled and why, with the live check's result.

## Tests

Failing first:

- `TheRampFollowsTheFactor`: a theory at 1, 1.5, 2.25: every `TypeRamp` key is its base times the factor.
- `ALiveChangeKeepsFocusAndReflows`: a hosted shell with focus in the Files tree; call `TypeRamp.Apply` on its ramp dictionary from 1 to 2 and back; focus stays on the same element and the row's height changes.
- `ALiveChangeDoesNotRebuildTheReadingDocument`: same `FlowDocument` instance, same caret offset.
- `TheEditorCombinesItsPreferenceAndTheFactor`: preference 16 at factor 1.5 gives 24; the announced size stays 16.
- `IconGlyphsKeepTheirSize` (census).
- `NoShellTextIsClippedAtTheLargestSize`: hosted at 2.25: welcome view, a vault with a note open, the Files tree, the rail. No `TextBlock` without trimming has a non-null `LayoutInformation.GetLayoutClip`. Trimmed text is allowed where it already trims.
- `TextScaleServiceTests`: the seam's read drives `Factor`, `Revision` and `Changed`, as the canvas facts do today.
- `AHostedTestIgnoresTheDesktopsSetting`: `DictionariesFor` with no factor gives base sizes, whatever the registry says.
- Gate: `ThemedShell_AtTheLargestTextTheShellGrows`: launched at the seam's 2.25, status bar and tab strip exceed their 100% heights, and `FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean` still passes.

Launched app: the whole discovery list at the seam's 2.25.

## Docs and registers

`xaml/README.md:162-166`, `wpf-feasibility.md:62`, `:170`. #1756's text scale box, once the owner's check is in. A comment on #752. No parity change.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~TextScale|FullyQualifiedName~TypeRamp|FullyQualifiedName~ThemeDesignSystemTests|FullyQualifiedName~CanvasDocumentTests|FullyQualifiedName~GraphDiagramTests"`, then the gate fact locally with the desktop free.

**The owner's check, on the PR's build (plan 46 §8):** set Text size to 150% and Apply, watch the shell, then return to 100%. Expected: the shell's text changes within about a second and focus stays. Reading `SystemEvents` from a broadcast sent by a test wouldn't prove what the Settings app does, and an agent doesn't change this setting. If the check fails, DS-5 switches the seam's source to `UISettings` per DSD3: the target framework moves to `net10.0-windows10.0.19041.0`, and the PR records the size change.

AT: NVDA at the seam's 2.25, a menu and a sheet. Sizes aren't spoken, so the expectation is no change.

## Acceptance

- [ ] One service, created once. `CanvasTextScaleService` is gone.
- [ ] The facts pass, and the gate fact passes at 2.25.
- [ ] A discovery pass at 2.25 finds nothing clipped.
- [ ] The owner's check is recorded in the PR, or the PR stays open for it.
- [ ] #1756's box is ticked, and the documents state the source.

## Brief

Read `Canvas/CanvasTextScaleService.cs`, `Canvas/CanvasRendererView.cs:30-40`, `Graph/GraphDiagramView.cs:225-240`, `ThemeManager.cs:204-259`, `TypeRamp.cs` from DS-2, `EditorInteractions.cs:296-320`, `design/windows/xaml/README.md:162-166`. Move the service, add `TypeRamp.Apply` and wire it, add the editor's effective size and the launched seam. Facts first. Work through the fixed widths at 2.25 in the launched app. Hand the owner the two-minute check before asking to merge. Commit: `feat(windows): windows text size scales the whole shell, live, up to 225 percent`.
