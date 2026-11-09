# DS-4 — Every menu row lines up, shortcuts are readable, and a top-level item fits a 48 row

Issues: filed by DS-0; `Refs #1756` (its menu template item). Size M. Lanes: windows. AT: NVDA and Narrator spot. Wave 1, slot 5. Depends on: DS-2 (sizes), and uses the tokens from DS-1's layer. Decision: DSD5. Interlock: DS-13 reuses the top-level template for the logo. Decision also: DSD10.

## Goal

One Slate template serves all four menu roles, in the menu bar and in every context menu. Labels start in one column whatever a row carries. A checked row draws a bare check. Shortcut text is secondary text at the row's size, and text in a menu keeps ClearType. A top-level item measures under 48 at 14, which #1756 asked to have measured rather than worked out.

## Current state on main

- **Fluent's templates** (`dotnet/wpf`, `release/10.0`, `Styles/MenuItem.xaml`), four of them:
  - `SubmenuItem` (`:221-310`) has a check column (`:231`), an icon column (`:232`) and a shortcut column (`:234`).
  - `SubmenuHeader` (`:313-402`) has an icon column (`:330`) and no check column, so its label starts further left. That's the Math and Code Preamble Verbosity misalignment.
  - A checkable row draws a 20 × 20 box (`:237-257`).
  - The shortcut is `FontSize="11"` in `TextFillColorDisabledBrush` (`:281-282`).
  - Top-level: `Margin="4"` on the border and `Margin="10"` on the grid (`:48`, `:59`), so 28 of padding around the text, which gives 45.96 at 12 and 48.62 at 14 (plan 46 §2.2).
  - The popup's `DropShadowEffect` is on the border holding the items (`:116-122`, `:392-398`), which turns ClearType off (spike 4).
  - The slide-in runs whatever the Windows animation setting says (`:146-163`, `:427-444`; `ContextMenu.xaml:53-70`).
- **The app's patch:** `MenuShortcuts.cs`, reached from `ThemeManager.cs:219-222`, finds `InputGestureText` on load and fixes its size and colour (#1779).
- **The bar:** `MainWindow.xaml:110-198`; items panel at `:194-198`; six menus at `:199`, `:302`, `:385`, `:455`, `:600` (Canvas) and `:701`.
- **What pins it:** `MenuBarCensus`, `MenuItemForegroundTests`, `AMenuShortcutMatchesItsRow` and `TheMenuBarDrawsTheThemesTextOnTheThemesWindow` (`ThemeDesignSystemTests.cs`), `ThemedShell_TheMenuBarAndTheOpenTabAreReadable`, `EveryMenuItemIsBackedByARegisteredCommand`, `SpokenChords_MenusPaletteAndOverlays_MatchTheTable`, `AContextMenuPopupStillReadsAsAMenuToTheClassifier`.
- **Checkable rows are a subclass.** Every checkable row is a `CheckMenuItem` (`CheckMenuItem.cs:22`; 17 in XAML, plus `Graph/GraphVerbosityMenu.cs:38`), and an implicit style matches its exact type. Those rows get the classic style, whose `DynamicResource` template keys reach Fluent's. So an implicit `MenuItem` style alone doesn't touch the rows this chunk most needs to change. None of them has a shortcut today.
- **A fact that wouldn't see the change:** `MenuItemForegroundTests.cs:84-88` merges Fluent and Slate by hand, so it would keep measuring Fluent's template.
- **Session 0.** A popup on a hosted runner opens and doesn't lay out its children, so a fact calls `ApplyTemplate`, `Measure` and `Arrange` on items after opening (`wpf-local-style-drops-fluent` note, #1241).

## Reference

`design/windows/wpf-feasibility.md:92-103`. `design/windows/components/navigation/MenuItem.prompt.md:1`: 40 px pitch, 34 px highlight inset 5 px, radius 3, label 16 px from the edge. `MenuMenu.prompt.md:20`: radius 8, the Slate shadow, `Grid.IsSharedSizeScope` on the item panel, `RenderOptions.ClearTypeHint="Enabled"` on the opaque surface. `components/navigation/menu-states.card.html`. `winui-review.md:76`. `readme.md:176`: one leading column, the check replacing an icon, and shortcut and chevron sharing the end column. `review-2026-11.md:54`. Spike 4's popup: an opaque border with the hint, the shadow on a background-only sibling.

## Design

1. **`Themes/Slate.Menus.xaml`,** merged after `Slate.Text.xaml` in `ThemeManager.DictionariesFor`, and added to `ValidateResourceDictionaries` (`ThemeManager.cs:123-124`). It holds a `Slate.MenuItemStyle` with `OverridesDefaultStyle=True` and the four role triggers Fluent's has (`MenuItem.xaml:458-472`), and an implicit `MenuItem` style based on it. An application-level implicit style reaches context menus too. `CheckMenuItem`'s constructor calls `SetResourceReference(StyleProperty, typeof(MenuItem))`, as `AutomationLandmark.cs:139` does.
2. **Row anatomy,** for `SubmenuItem` and `SubmenuHeader` alike, in one shared grid:

   | Column | Shared group | Holds |
   |---|---|---|
   | 0 | `SlateMenuLeading` | The check (`E73E` at 12, fixed size) or the `Icon`, 16 wide. Zero-width when no row in the menu has either; once any row can be checked, every row keeps it (`readme.md:176`) |
   | 1 | star | Header, `RecognizesAccessKey` |
   | 2 | `SlateMenuEnd` | `TextBlock` named `InputGestureText`, left margin 24, `FontSize` from the row; or on a header row, `E76C` at 12 |

   The outer element's height sets the 40 pitch. The highlight is a border with margin `5,3`, radius 3, `MinHeight` 34; the label sits 11 inside it, so 16 from the edge when column 0 is empty. `Grid.IsSharedSizeScope` is on each popup's `ItemsPresenter`, and on `ContextMenu` as Fluent already does (`ContextMenu.xaml:24`). `MinHeight` rather than `Height` throughout: DS-5 grows these rows.
3. **States.** Highlighted: `Slate.HoverOverlayBrush`. Checked: the glyph in `Slate.TextBrush`. Disabled: `Slate.DisabledTextBrush` on label, shortcut and chevron. Shortcut at rest: `Slate.SecondaryTextBrush`. Focus visual: a new `Slate.MenuRowFocusVisual`, the same 2px ring as rows elsewhere, on the inset highlight as WinUI does (DSD10): margin `2,0` and radius 6, so its inner edge sits 1 out from the highlight, as `tokens/base.css:7` says, and its outer edge stays inside the row.
4. **Top level.** `TopLevelHeader` and `TopLevelItem`: `MinHeight` 32, margin `0,0,2,0`, padding `10,0`, radius 4. At 14 that's 32, under 48 with 16 to spare. Foreground stays `MenuBarForeground` from the bar (`MainWindow.xaml:115`).
5. **The popup.** A `Grid` with margin room for the shadow. In it: a background-only `Border` carrying `Slate.Shadow16`, radius 8, then the content `Border` with `FlyoutBackground`, `FlyoutBorderBrush`, radius 8, padding `0,3`, `RenderOptions.ClearTypeHint="Enabled"`. `Popup.AllowsTransparency="True"` as Fluent has it. The `ContextMenu` template gets the same pair, in a `Slate.ContextMenuStyle` replacing the one DS-2 built. `MaxHeight` comes from the popup's own screen fit, and its `ScrollViewer` keeps vertical `Auto`.
6. **Motion.** The 167 ms slide stays only under a `MultiDataTrigger` that also reads a small `ShellMotion` instance, shaped like `Graph/GraphMotionPolicy.cs:25` so a fact can inject the setting, with the real value from `SystemParameters.ClientAreaAnimation`. With animation off, the menu is simply there.
7. **The separator.** `MenuItem.SeparatorStyleKey`: `Slate.SubtleBorderBrush`, 1px, margin `0,2`.
8. **Remove** `MenuShortcuts.cs` and `ThemeManager.cs:219-222`. `AMenuShortcutMatchesItsRow` stays and must pass unchanged, which is why the part keeps its name.
9. **High Contrast keys.** `Slate.HoverOverlayBrush`, `Slate.DisabledTextBrush` and `Slate.SubtleBorderBrush` aren't in `Slate.Contrast.xaml`. Add them mapped to `SystemColors` as `xaml/README.md:17` requires. That keeps a missing key from drawing nothing, and it's the whole of the High Contrast work here.

## Tests

Failing first, in `ThemeDesignSystemTests.cs` or `MenuTemplateTests.cs`, hosted, with the session-0 recipe:

- `EveryRowKindStartsItsLabelInOneColumn`: a menu with a plain `MenuItem`, a `CheckMenuItem`, a checked one and a submenu row; all four headers share an x. Fluent fails this.
- `ARowsPitchIsFortyAndItsHighlightIsInsetFive`.
- `AMenuRowsRingSitsOnItsHighlight`: the row's `FocusVisualStyle` is `Slate.MenuRowFocusVisual`, and its ring fits inside the 40 pitch without clipping.
- `ACheckMenuItemTakesTheSlateTemplate`: its `Template` is `Slate.MenuItemStyle`'s, not Fluent's.
- `ACheckedRowDrawsABareCheck`: on a `CheckMenuItem`: no `CheckBoxIconBorder`; the glyph is there only when checked.
- `ATopLevelItemFitsATitleBar`: at `Slate.FontSizeBody`, `ActualHeight` plus margins is at most 48. This is the measurement #1756 asked for.
- `AShortcutFollowsItsRow`: at `FontSize` 31.5, the shortcut is 31.5, its column widens, and labels still align.
- `ADisabledRowsTextClearsTheFloor`: the shortcut's brush against `FlyoutBackground`, 4.5:1 in light and dark.
- `AMenusShadowSitsBesideItsContent`: the content border has no `Effect` and has the hint; the sibling carries `Slate.Shadow16`.
- `AMenuHonoursAnimationsOff`: no storyboard runs with `ShellMotion`'s read injected off.
- `MenuItemForegroundTests` changes to `ThemeManager.DictionariesFor`, so it measures what ships.
- Gate: `Menus_ALongMenuScrollsToItsLastRow`: Workspace's 25 rows by keyboard, in the launched app, since a hosted popup can't take focus (`CanvasNavigatorTests.cs:6011-6016`).

Launched app: the Editor menu with Math and Code Preamble Verbosity open, a tab's context menu, and the top-level bar, at 100% and at 200% if the display is there. Spike 4's fringe count on a real menu's text, at 400% magnifier zoom or from a physical-pixel capture.

## Docs and registers

`wpf-feasibility.md:92-103`: Menus goes from a to-do to what shipped, with the measured top-level height. #1756's menu box. `design/windows/CLAUDE.md` if a value differs from the card. No parity change.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~MenuTemplate|FullyQualifiedName~MenuItemForegroundTests|FullyQualifiedName~MenuBarCensus|FullyQualifiedName~ThemeDesignSystemTests|FullyQualifiedName~CommandDriftTests"`, and on the accessibility project `FullyQualifiedName~SpokenChords|FullyQualifiedName~ThemedShell_TheMenuBar`.

AT: NVDA and Narrator, File, then Workspace, then a checkable row in Editor: name, shortcut, checked state, and the access keys. Peers come from the control rather than the template, so the expectation is no change. Record the phrases.

## Acceptance

- [ ] The Math and Code Preamble Verbosity rows align in the launched app.
- [ ] `MenuShortcuts.cs` is gone and its fact still passes.
- [ ] The top-level measurement is recorded, and #1756's box is ticked.
- [ ] DSD5 was looked at in the running app and the PR says what it saw.

## Brief

Read `design/windows/wpf-feasibility.md:92-103`, `MenuItem.prompt.md`, `MenuMenu.prompt.md`, `ThemeManager.cs:204-238`, `MenuShortcuts.cs`, and Fluent's `MenuItem.xaml` and `ContextMenu.xaml` at `dotnet/wpf` `release/10.0`. Write `Themes/Slate.Menus.xaml`, merge it, delete the patch, add the three Contrast keys, give `CheckMenuItem` its style, and move `MenuItemForegroundTests` to `DictionariesFor`. Facts first, using `ApplyTemplate`, `Measure`, `Arrange` on opened items. Launched captures, then the AT spot. Commit: `feat(windows): one menu template lines up every row, and a top-level item fits a 48 row`.
