# Can WPF build this design system?

Checked on 1 November 2026 against:
- Slate's Windows app on `main` (`coryj627/slate`, `apps/slate-windows`): `SlateWindows.csproj`, `App.xaml.cs`, `ThemeManager.cs`, `Themes/Slate.*.xaml`, `MainWindow.xaml`.
- The WPF Fluent theme source on `main` (`dotnet/wpf`, `src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Fluent`).
- Microsoft Learn and WPF design notes (listed at the end).

## Short answer

Yes. Nothing in the system needs WinUI. Slate already runs on .NET 10, merges the Fluent theme dictionaries itself and loads its own `Slate.Light / Dark / Contrast` dictionaries after them, so most of the system is a resource layer on top of what ships.

Four areas need real engineering:
1. **Recolouring Fluent controls.** Slate's theme files only define `Slate.*` keys. Fluent controls in the app (check boxes, accent buttons, sliders, focus rings, menus) still use the Windows accent colour and Fluent's default greys, not the gold and neutrals this system specifies.
2. **Menus.** WPF's Fluent `MenuItem` template causes the misalignment seen in the Editor menu and draws shortcut text in the disabled colour.
3. **The 48px title bar.** Slate uses the standard Windows title bar today. The merged title bar needs `WindowChrome` plus a native hook for Snap Layouts.
4. **Text scaling.** WPF doesn't apply the Windows "Make text bigger" setting to fixed font sizes. WinUI does.

Three things should change in the design system rather than in the app: the two-layer shadows, the optional Mica title bar, and the Material acrylic component. All three changed on 1 November 2026 (see Shadows and Mica and acrylic).

## Ratings

- **Native:** the Fluent theme already does it. Override resource keys only.
- **Template:** new `Style` or `ControlTemplate` on an existing WPF control.
- **Build:** behaviour WPF doesn't have. Needs code or a custom control.
- **Change the design:** WPF can't do it well. Adjust the system.

| Area | Rating | What it takes |
| --- | --- | --- |
| Colour tokens, light and dark | Native | Override Fluent's **brush** keys in the Slate dictionaries (see Colour) |
| High Contrast | Native | Keep `Fluent.HC` + `Slate.Contrast`; never apply gold there |
| Type ramp, sizes, line heights | Native | CSS px = WPF device-independent pixels, so values carry over 1:1 |
| Segoe UI Variable | Native, with a check | Use the named families; confirm in a spike |
| Text scaling | Build | Scale font sizes yourself |
| Icons | Native | `SymbolThemeFontFamily`; name every icon-only control |
| Radii, strokes | Native | 4 and 8 match Fluent; 6px tabs need a template |
| Hover and pressed | Template | Map overlays to Fluent's state brushes |
| Shadows | Change the design | One shadow per element; watch ClearType |
| Motion | Native | Storyboards with the same curves and durations |
| Focus ring | Native | One offset differs (1px vs 3px) |
| Text selection | Native, with a switch | Turn off adorner selection for solid gold |
| Menus | Template | One shared template for every row type |
| Tooltips | Native | Keyboard tooltips are built in |
| Title bar | Build | `WindowChrome` + `WM_NCHITTEST` hook |
| Mica, acrylic | Change the design | Conflicts with Slate's solid-surface policy |
| Narrow-width layouts | Build (small) | No `AdaptiveTrigger` in WPF |

## Colour

**Native, with one trap.** The Fluent theme builds its accent brushes from `SystemAccentColor` with `StaticResource`, which is resolved once when the dictionary loads. Redefining `SystemAccentColor` in a later dictionary changes nothing. Control styles read the **brushes** with `DynamicResource` (for example `AccentButtonBackground`), so override the brush keys in `Slate.Light.xaml` and `Slate.Dark.xaml`. Slate already merges these last, and the last merged dictionary wins.

- There are 163 accent references in Fluent's theme files, plus the neutral families (`TextFillColor*`, `ControlFillColor*`, `ControlStrokeColor*`, `CardBackgroundFill*` and the solid backgrounds). **Done:** `xaml/Slate.Light.xaml` and `xaml/Slate.Dark.xaml` are generated from the tokens and override 81 accent brushes and about 267 neutral brushes. The accent brushes read nine accent-role colours with `DynamicResource`, so the Windows accent can replace gold at runtime (`xaml/README.md`).
- Colour formats differ: CSS 8-digit hex is `#RRGGBBAA`, XAML is `#AARRGGBB`. Convert `rgba()` values to ARGB.
- High Contrast: leave Fluent's HC dictionary and `Slate.Contrast.xaml` (which maps to `SystemColors`) untouched. No gold overrides in HC.

## Type

- **Sizes and line heights: Native.** One CSS px is one WPF device-independent pixel, so 12/16, 14/20, 20/28 carry over unchanged. To get exact line heights, set `LineHeight` with `LineStackingStrategy="BlockLineHeight"`. The default strategy uses the tallest glyph instead. The tokens use no letter-spacing, which WPF `TextBlock` lacks.
- **Segoe UI Variable: Native, with a check.** WPF's Fluent theme sets only the icon font (`SymbolThemeFontFamily`), so text stays in plain Segoe UI unless the app sets `FontFamily`. WPF can't drive a variable font's axes (its DirectWrite layer has no font-axis APIs), so automatic optical sizing, which WinUI has, isn't available. Use the named families: `Segoe UI Variable Small` under 12px, `Text` for 12–24px, `Display` above 24px, each falling back to `Segoe UI` (Windows 10 doesn't ship the variable font). The system only uses weights 400, 600 and 700, which WPF names directly. **Spike:** confirm WPF lists these three named families on Windows 11.
- **Open Sans and Cascadia Code: Native.** Embed them as resources. **Spike:** check that Cascadia Code ligatures survive AvalonEdit's syntax colouring, which splits text into separate coloured runs.
- **Text scaling: Build.** WinUI applies the Windows "Make text bigger" setting automatically. WPF doesn't, so fixed `FontSize` values stay as they are. Read `UISettings.TextScaleFactor` (1 to 2.25) and handle `TextScaleFactorChanged`. Microsoft's guidance is to scale small text more than large text and to leave icon glyphs unscaled. **Design impact (done):** the shell heights (48 title bar, 36 tab strip, 24 status bar) and the 32–36px rows are now minimums that grow with text (`--titlebar-min-height`, `--tabstrip-min-height`, `--statusbar-min-height`; `MinHeight` in WPF), up to 225%.
- Expect WPF text to look slightly different from the HTML cards (different rasterizer). Judge type in the running app.

## Icons

**Native.** The Fluent theme's `SymbolThemeFontFamily` is `Segoe Fluent Icons, Segoe MDL2 Assets`. Windows 11 has Segoe Fluent Icons; Windows 10 doesn't ship it and falls back to MDL2, which uses the same code points for shared glyphs. Reference the resource, not the font name, and don't redistribute the font file. For Fluent System Icons (the SVG set), convert the single-path SVGs to `Path` geometry.

Every icon-only button needs `AutomationProperties.Name`. Otherwise Narrator reads the private-use glyph as the name. This is the same problem Slate already fixed for the TextBox clear button (#1106).

## Shape, state and motion

- **Radii: Native.** Fluent's `ControlCornerRadius` is 4 and `OverlayCornerRadius` is 8, matching the system. 6px tabs and the 3px selection pill go in templates.
- **1px strokes: Native.** Set `UseLayoutRounding="True"` on the window so lines stay crisp at 125% and 150% scaling.
- **Hover and pressed: Template.** The system describes states as overlays (4% / 8% black, 6% / 10% white in dark). Fluent controls use one solid brush per state (`ControlFillColorSecondary`, `ControlFillColorTertiary`), so set those keys to the overlay colours as composited on each surface. Slate-built rows can use semi-transparent brushes in their triggers directly.
- **Motion: Native.** Durations carry over unchanged. Each `cubic-bezier` token becomes a `KeySpline` with the same four numbers (decelerate-mid is `KeySpline="0,0,0,1"`). Skip animations when `SystemParameters.ClientAreaAnimation` is false (Windows "Animation effects" off).

## Shadows

**Change the design.** The shadow tokens are two layers each (a soft ambient layer plus a key layer). A WPF element takes one `DropShadowEffect`. Stacking two effects needs two elements and costs rendering time.

- `DropShadowEffect.Direction` defaults to 315°, which puts the shadow down and to the right. Fluent shadows fall straight down: set `Direction="270"`. All 11 shadows in Slate's `MainWindow.xaml` (`BlurRadius 18`, `ShadowDepth 4`, `Opacity 0.28`) use the default direction.
- CSS blur and WPF `BlurRadius` aren't the same unit. Tune by eye against the HTML elevation card.
- Effects turn off ClearType for the text inside them, and so do popups with `AllowsTransparency="True"`, which is every Fluent popup (menus, tooltips, ComboBox, DatePicker). Text there renders in greyscale. Put the effect on a background-only `Border` behind the content rather than on the content's parent, and set `RenderOptions.ClearTypeHint="Enabled"` on the opaque popup surface. The hint has no effect on `TextBox`.
- **Done:** one shadow per level (2, 4, 8, 16, 28, 64), black at 18% (36% in dark). The CSS tokens and the Fluent components use the single layer, and `xaml/` has a `DropShadowEffect` per level (`Slate.Shadow2` … `Slate.Shadow64`, `Direction="270"`, `BlurRadius` = 1.5 × the CSS blur). Values are in `xaml/README.md` and on the Elevation card.

## Focus and selection

- **Focus ring: Native.** WPF shows `FocusVisualStyle` only for keyboard focus, which matches `:focus-visible`. Fluent's default is a 2px ring with radius 4, drawn 3px outside the control. The system says 1px outside. **Decision:** keep 1px and set `Margin="-1"` in Slate's focus style. Set `KeyboardFocusBorderColorBrush` to the focus-ring token. For list and menu rows, Fluent's collection focus style draws inside the row, which matches the menu fix in this system.
- **Text selection: Native, with a switch.** By default WPF draws TextBox selection as a translucent overlay (`SelectionOpacity`) and ignores `SelectionTextBrush`, so pale gold with dark text isn't guaranteed. Set the AppContext switch `Switch.System.Windows.Controls.Text.UseAdornerForTextboxSelectionRendering` to `false`, then set `SelectionBrush`, `SelectionTextBrush` and `SelectionOpacity="1"`. The Fluent styles read `SelectionBrush` from `TextControlSelectionHighlightColor`, which Slate doesn't override yet. In the editor, AvalonEdit's `TextArea` has its own `SelectionBrush` and `SelectionForeground`.

## Menus

**Template.** Three problems in Fluent's `MenuItem.xaml`:
- **Rows don't line up.** Checkable rows have a check column, but rows that open a submenu don't, so their labels start further left. This is the Math / Code Preamble Verbosity misalignment in the Editor menu.
- **Shortcut text uses the disabled colour** (`TextFillColorDisabledBrush`, #5C000000 in light mode, about 2.5:1 on white) whether or not the item is enabled. The system requires secondary text, and at least 4.5:1 even for disabled items.
- **Checked rows draw a 20 × 20 check box** rather than the bare checkmark the system uses.

Fix: one Slate `MenuItem` template for all four roles (top-level header, top-level item, submenu header, submenu item). Each role uses the same columns (check, icon, label, shortcut, chevron) with `SharedSizeGroup`, and the menu panel sets `Grid.IsSharedSizeScope`. Keep `InputGestureText` and the `AcceleratorKey` binding Slate already uses. Popup values for reference: Fluent uses radius 8, 3px vertical padding and a 167ms slide-in.

## Tooltips

**Native.** Since .NET Framework 4.8, WPF opens a tooltip when its control gets keyboard focus. Ctrl+Shift+F10 toggles it, and Esc or Ctrl alone closes it. From .NET 6, `ToolTipService.ShowsToolTipOnKeyboardFocus` turns this off per control (useful in the pane switcher if tooltips appear while arrowing through icons). A tooltip isn't the control's accessible name, so set `AutomationProperties.Name` as well.

## Title bar (48px, merged with the menus)

**Build.** The approach in readme.md (Window chrome) is right. What the code shows:
- Slate has no `WindowChrome` today, so this is new work.
- `WindowChrome` answers `WM_NCHITTEST` but never returns `HTMAXBUTTON`, so Snap Layouts won't open from a custom maximize button. Add an `HwndSource` hook that returns `HTMAXBUTTON` over the button. Windows then handles the mouse over that button itself, so the hook also has to drive the button's hover and pressed visuals.
- Microsoft recommends a minimum window width of 500 effective pixels or less (330 or less if possible) so Snap Layouts work. With a 260 sidebar and a 300 right pane, panes must collapse below that width.
- **App menu keys: Build.** WPF's Alt and F10 menu activation belongs to a visible `Menu` with `IsMainMenu`. With the menus moved into the logo popup, handle Alt+F, W, B, E, N, G, H yourself (`Key.System` in `PreviewKeyDown`). F10 should open the app menu too.

## Mica and acrylic

**Change the design.** Slate turns off the Fluent window backdrop (`DisableFluentThemeWindowBackdrop`, policy W1-1: every text-bearing surface is solid). WPF also applies a backdrop to the whole window, so a Mica title bar would mean making everything else opaque by hand. WPF popups have no acrylic. **Done:** "optional Mica" is gone from Visual foundations, and Material acrylic is marked web-only.

## Components

**Fluent style ships; override resources only:** Button, Link (`Hyperlink`), Checkbox, Radio, RadioGroup, Dropdown (`ComboBox`), Input and Textarea (`TextBox`), Slider, StaticProgressBar and AnimatedProgressBar (`ProgressBar`), Divider (`Separator`), Tooltip, Accordion (`Expander`), TreeItem (`TreeView`), ListItem (`ListView`), DataGridCell sizes (`DataGrid`), Label.

**New template on an existing control:**
- Menus: MenuItem, MenuMenu, MenuSection (see Menus).
- Switch: template a `CheckBox`. WPF's Fluent theme has no toggle switch. A `CheckBox` keeps the Toggle pattern and the Space key.
- Buttons: CompoundButton (`Button`). SplitButton and MenuSplitItem: a `Button` plus a `ContextMenu`, since WPF has no split button.
- Tabs and navigation: HorizontalTab and VerticalTab lists (`TabControl` or `ListBox`). Nav and AppItem (`ListBox` / `TreeView` with the gold pill).
- Inputs: SearchBox (`TextBox` with icon and placeholder; WPF's `TextBox` has no placeholder property). SpinButton (`TextBox` plus `RepeatButton`s). Swatch, SwatchPicker and Rating (`ListBox` or radio buttons).
- Display: Card parts (`Border`). Badge, PresenceBadge, StatusIndicator, Tag and InteractionTag (`Border` + `TextBlock` / `Button`). Avatar (`Ellipse`).
- Composition: Field, InfoLabel, InfoButton, MessageBar, Breadcrumb (`ItemsControl`), SectionHeader, ContentHeader, CellActionHeader, ListContent, Toolbar (a panel of buttons is simpler than WPF's `ToolBar`).

**Build:**
- Feedback: Spinner (WPF's Fluent theme has no progress ring; animate an arc). Skeleton (animated gradient). Both must stop when animations are off.
- Overlays: Popover and TeachingPopover (`Popup` plus focus in, Esc, and focus back to the trigger). Dialog (in-window overlay with the 30% scrim, `KeyboardNavigation.TabNavigation="Cycle"` and `AutomationProperties.IsDialog`, or an owned window). Drawer, InlineDrawer and OverlayDrawer.
- Toast: an in-app overlay announced through `AutomationProperties.LiveSetting`. Windows notifications don't use these styles.
- TagPicker: the heaviest UI Automation work in the set.
- Carousel and ImagePreview: no Slate screen uses them.

**Web-only:** Material acrylic.

**Slate patterns** (all buildable):
- PaneSwitcher: `ListBox` with single selection and a vertical or horizontal panel. Compute the "More panels" overflow on `SizeChanged`.
- PanelToolbar: icon buttons plus a sort `ContextMenu` with checkable items.
- NoteHeader: switch visual states from `SizeChanged` for the icon-only switch below 520px, since WPF has no `AdaptiveTrigger`.
- EmptyStateLauncher: a `StackPanel` of buttons.
- CountListRow: `TreeViewItem` template. Backlink rows expand as tree nodes.
- SettingsLayout: `ListBox` navigation plus a `ScrollViewer`. WPF has no NavigationView.
- SettingRow: a `Grid` with the Switch template.

## Suggested order

1. Generate the XAML theme from the tokens: `Slate.*` keys plus Fluent brush overrides for light and dark. This brings gold and Slate's neutrals to every Fluent control at once. **Done in `xaml/`**: copy the two files into the app, then add the accent setting.
2. Menu template (fixes the shipped misalignment and shortcut contrast).
3. Focus style, selection switch and brushes, `AutomationProperties.Name` on icon buttons.
4. Shadows: one effect per element, direction 270, ClearType hint on popups.
5. Switch and SearchBox templates, then the Slate patterns.
6. Title bar spike: `WindowChrome`, the `HTMAXBUTTON` hook, and app-menu keys.
7. Text scaling across the shell.

## Spikes to run first

- WPF lists `Segoe UI Variable Small / Text / Display` as families on Windows 11.
- Cascadia Code ligatures in AvalonEdit with syntax colouring on.
- Snap Layouts hover and pressed visuals with the `HTMAXBUTTON` hook.
- ClearType in a popup with `ClearTypeHint` set on an opaque surface.
- The shell at 225% text scale.
- Non-adorner text selection with right-to-left text. A crash with Hebrew text was reported on .NET Framework 4.8 (dotnet/wpf#2314).

## Sources

- What's new in WPF for .NET 10: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100
- WPF Fluent theme source: https://github.com/dotnet/wpf/tree/main/src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Fluent
- Fluent theme in .NET 10 plan: https://github.com/dotnet/wpf/discussions/10387
- WPF tooltip behaviour spec: https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/specs/tooltip.md
- RenderOptions.ClearTypeHint: https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.renderoptions.cleartypehint
- ClearTypeHint background: https://learn.microsoft.com/en-us/archive/blogs/text/cleartypehint
- Snap layouts for desktop apps: https://github.com/MicrosoftDocs/windows-dev-docs/blob/docs/hub/apps/desktop/modernize/ui/apply-snap-layout-menu.md
- Segoe Fluent Icons: https://learn.microsoft.com/en-us/windows/apps/design/iconography/segoe-fluent-icons-font
- Windows typography: https://learn.microsoft.com/en-us/windows/apps/design/style/typography
- Text scaling: https://learn.microsoft.com/en-us/windows/apps/develop/input/text-scaling
- Accessible text requirements: https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessible-text-requirements
- Reading the text scale factor outside WinUI: https://devblogs.microsoft.com/oldnewthing/20230830-00/?p=108680
- Non-adorner TextBox selection switch: https://github.com/Microsoft/dotnet/blob/main/Documentation/compatibility/wpf-TextBox-PasswordBox-text-selection-does-not-follow-system-colors.md
- Slate Windows app: https://github.com/coryj627/slate/tree/main/apps/slate-windows
