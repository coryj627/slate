# DS-2 — Every text size in the shell is a named step, and default text is 14/20 in Segoe UI Variable

Issues: filed by DS-0. Size M–L. Lanes: windows. AT: — (no name, role or order changes; the shell gate runs). Wave 1, slot 3. Depends on: DS-1. Decision: DSD4. Interlock: `EmbedPreviewTitleCensus` pins a literal this chunk changes. DS-5 replaces this chunk's startup read with a live one.

## Goal

The shell has one type scale, the WinUI ramp the design adopted, reachable as dynamic resources so DS-5 can scale it. A census stops new literal sizes. Text with no size of its own is body text, 14 in Segoe UI Variable Text, instead of the 12 in plain Segoe UI it is today.

## Current state on main

- **The default:** `MainWindow.xaml:11-26` sets no `FontFamily` or `FontSize`, and Fluent's styles set neither (plan 46 §2.2). So a `Button`, a `TreeViewItem` and a `TextBlock` are 12 in Segoe UI, `SystemFonts.MessageFontSize`, not the ramp's body at 14. That same route is why they follow Windows Text size today, up to 27 at 225% (`wpf-feasibility.md:170`).
- **Two that don't inherit:** a default style's setter beats inheritance. `ShellStatusBar` measures `BaseValueSource=DefaultStyle`. `MainMenu`'s local style (`MainWindow.xaml:131`) drops Fluent's and leaves the classic `Menu` style in force, which sets the size from `SystemFonts`.
- **Ramp styles:** only Body Large exists, `Themes/Slate.Text.xaml:8-18`, with fixed values. Fluent's `TextBlock` styles use `StaticResource` sizes, so they can't scale. Nothing in `src` uses a ramp style by key (searched).
- **Literal sizes:** 59 in all. Listed under Design.
- **Popups outside the tree:** a `ContextMenu` and a `ToolTip` don't inherit from the window. Fluent's `ToolTip.xaml:18-21` takes `SystemFonts.Status*`. So setting the window isn't enough for them.
- **Drawn text:** `Canvas/CanvasRendererView.cs:417` and `:433` use `new Typeface("Segoe UI")`. `Graph/GraphDiagramView.cs:930` uses `SystemFonts.MessageFontFamily`.
- **Test pins:** `TheBodyLargeStylesAreTheRampsEighteenOnTwentyFour` (`ThemeDesignSystemTests.cs`); `EmbedPreviewTitleCensus.cs:304-305` quotes the `FontSize` line of `WorkspaceTemplates.xaml:728`.

## Reference

`design/windows/tokens/typography.css:24-32` (the ramp), `design/windows/xaml/README.md:116-141`, `design/windows/winui-review.md:73`. `PanelToolbar.jsx:25` sets a panel title at `600 14px/20px`. `SettingsLayout.prompt.md`: a page title at 20/28 semibold. `MenuShortcuts.StyleFor` (`MenuShortcuts.cs:18-23`) shows how to build on a Fluent style from code, since a dictionary can't reach a sibling's style.

## Design

1. **`TypeRamp.cs`,** one table: step name, size, line height, family, weight. `TypeRamp.DictionaryFor(double factor = 1)` returns a `ResourceDictionary` of `x:Double` and `FontFamily` values, and DS-5 passes the factor. `ThemeManager.DictionariesFor` merges it first, so its keys exist for every layer after it. Only the steps a surface uses:

   | Step | Size / line height | Family | Weight |
   |---|---|---|---|
   | Caption | 12/16 | Segoe UI Variable Small, Segoe UI | 400 |
   | Body | 14/20 | Segoe UI Variable Text, Segoe UI | 400 |
   | BodyStrong | 14/20 | Text | 600 |
   | BodyLarge | 18/24 | Text | 400 |
   | BodyLargeStrong | 18/24 | Text | 600 |
   | Subtitle | 20/28 | Segoe UI Variable Display, Segoe UI | 600 |
   | Title | 28/36 | Display | 600 |

   Keys: `Slate.FontSize<Step>`, `Slate.LineHeight<Step>`, `Slate.FontFamilyText`, `Slate.FontFamilySmall`, `Slate.FontFamilyDisplay`.
2. **Styles,** in `Themes/Slate.Text.xaml`: `Slate.<Step>TextBlockStyle`, each with `DynamicResource` size, family and line height, `LineStackingStrategy="BlockLineHeight"`, and its weight. The two Body Large styles change to dynamic values.
3. **The default.** `MainWindow.xaml:11-17` gains `FontFamily="{DynamicResource Slate.FontFamilyText}"` and `FontSize="{DynamicResource Slate.FontSizeBody}"`. No line height at the root: `TextBlock.LineHeight` inherits, so a fixed value would follow into every size. Unstyled body text keeps Segoe UI's natural 18.62 at 14. `HistoryConfirmationDialog.cs:24-27` and `Reading/ReadingTableGrid.cs:38` are their own windows and take the same two. `MainMenu` and `ShellStatusBar` set both locally, since the window's value can't reach them.
4. **Text size until DS-5.** Making unset text explicit stops it following Windows Text size, so `ApplyResources` passes `DictionaryFor` the factor `CanvasTextScaleService` reads, once, at startup. A reader at 225% keeps the larger text. It just takes a restart to change until DS-5, which is no worse than sized text today.
5. **Outside the tree.** Beside `MenuShortcuts.StyleFor`, a small `TypeRampStyles` builds `ContextMenu` and `ToolTip` styles on Fluent's: family and size from Body for the menu, Caption for the tooltip. `ThemeManager.DictionariesFor` adds them as `slate[typeof(...)]`, as it does for `MenuItem` at `ThemeManager.cs:219-222`.
6. **Drawn text.** `CanvasRendererView.cs:417`, `:433` and the graph's label typeface read `Slate.FontFamilyText`'s value from `TypeRamp`.
7. **The literals.** Every one below becomes its step's style, or its size key where a style won't fit, as on a `TextBox`:

   | Today | Sites | Becomes |
   |---|---|---|
   | 11 or 12, secondary lines and captions | `MainWindow.xaml:891, 928, 972, 1080, 1169, 1247, 1362, 1503, 1823, 1831, 1930, 2145, 2214, 2266, 2852, 3487, 3993`; `WorkspaceTemplates.xaml:243`; `Panels/HistorySurfaceView.cs:429, 1091, 1104`; `Panels/SyncDiagnosticsSurfaceView.cs:363` | Caption |
   | 15, one-line help | `MainWindow.xaml:843`; `Bases/BaseSurfaceView.cs:200` | Body |
   | 16, panel and leaf headings | `MainWindow.xaml:954, 1325 (PanelHeading), 2131, 2341, 2465, 2490, 2552`; `WorkspaceTemplates.xaml:728`; `Bases/DashboardSurfaceView.cs:257`; `Graph/ConnectionsLeafView.cs:300` | BodyStrong |
   | 16 or 20, a document surface's title | `Bases/BaseSurfaceView.cs:78`; `Canvas/CanvasSurfaceView.cs:92`; `Graph/GraphSurfaceView.cs:86`; `Bases/DashboardSurfaceView.cs:55` | Subtitle |
   | 16, a sheet's title | `MainWindow.xaml:3228, 3319, 3692, 3809, 4050, 4191, 4257, 4340` | Subtitle |
   | 18, the overlay search fields | `MainWindow.xaml:2626, 2747, 3033` | BodyLarge |
   | 18, semibold | `MainWindow.xaml:865, 927` | BodyLargeStrong |
   | 22 | `WorkspaceTemplates.xaml:793` | Subtitle |
   | 30 | `MainWindow.xaml:842` | Title |

   Stay literal, named in the census with the reason: `EditorInteractions.cs:48-50` (the user's own size preference and its bounds), `Graph/GraphDiagramView.cs:44` (drawn labels, already scaled by text size and zoom), and any size beside `SymbolThemeFontFamily` (a glyph, which DS-4, DS-8 and DS-9 add). DS-3 takes `ReadingDocumentBuilder.cs:210, 347-353, 810` and `Reading/ReadingSurface.cs:83`. `ReadingDocumentBuilder.cs:694` stays: it's a hidden speech run sized to its line's width.
8. **Fallout.** Rows, buttons and panels grow. Fix clipping as the discovery pass finds it, in this PR, within the design's own values: margins and wrapping rather than new sizes. Known soft spots: `MainWindow.xaml:1012` (`ComboBox Width="145"`), `:786` (`StatusBarItem Width="180"`), the graph filter's star column from #1784.

## Tests

Failing first:

- `TypeRampCensus` (in `Censuses/`): no `FontSize="<number>"`, `Property="FontSize" Value="<number>"` or `FontSize = <number>` under `src/SlateWindows`, outside the named exceptions. Red with all 59 sites at the start. A stale exception fails too.
- `EveryRampStepIsItsToken`: a theory over the table, reading `design/windows/tokens/typography.css`. If the tokens and the app differ, this fails.
- `TheShellsDefaultTextIsBody`: a hosted `ShownShell`, an unstyled `TextBlock`, a `Button`, `MainMenu`, a top-level item and `ShellStatusBar` are 14 in a family starting Segoe UI Variable Text.
- `NoControlTakesItsSizeFromTheSystem`: walk the hosted shell's visual tree; no `Control`'s `FontSize` has `BaseValueSource.DefaultStyle`. This finds any control like the status bar that the list above missed.
- `TheSizeFollowsTheStartupFactor`: `DictionaryFor(2.25)` gives 31.5 for body.
- `AContextMenuAndATooltipUseTheRamp`: opened in a hosted window, both report their step's size and family.
- `AHeadingStyleCarriesItsLineHeight`: a wrapped `Slate.SubtitleTextBlockStyle` block's height is a multiple of 28.
- Updated: `TheBodyLargeStylesAreTheRampsEighteenOnTwentyFour`, `EmbedPreviewTitleCensus`.

Launched app: a discovery pass of every surface in `install_pass`'s list, dark; a hosted render in light.

## Docs and registers

`design/windows/xaml/README.md:116-141`: the Slate styles are the ones to use, because Fluent's can't scale. `- 'design/windows/tokens/**'` on `windows.yml`'s path list, with a fact in `Workflows.Tests.ps1`, so `EveryRampStepIsItsToken` runs when a token changes. No parity change.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~TypeRamp|FullyQualifiedName~ThemeDesignSystemTests|FullyQualifiedName~EmbedPreviewTitleCensus|FullyQualifiedName~FocusIntegrity"`, then the whole app suite on the lane. Width-measuring facts are the likely reds: read each one, and change a fact only when its number was a consequence of 12 rather than the behaviour it pins.

## Acceptance

- [ ] The census passes with only the named exceptions.
- [ ] The default text is 14 in the launched app (from the capture and a `TextPattern` read of a row's font).
- [ ] A discovery pass finds nothing clipped at 100% and, if the display is there, at 200%.
- [ ] The shell gate is green on the lane.

## Brief

Read `design/windows/tokens/typography.css:24-32`, `Themes/Slate.Text.xaml:8-18`, `ThemeManager.cs:204-238`, `MenuShortcuts.cs`. Add `TypeRamp.cs` and the styles, set the window's two properties and the menu's and status bar's, pass the startup factor, build the `ContextMenu` and `ToolTip` styles, then convert the table's sites and write the census. Update `EmbedPreviewTitleCensus`. Run the discovery pass and fix what it finds. Commit: `feat(windows): every text size in the shell is a step of the design's ramp, and default text is 14 on 20`.
