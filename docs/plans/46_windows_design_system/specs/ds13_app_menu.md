# DS-13 — The menus open from the logo, with every key they had

Issues: filed by DS-0. Size L. Lanes: windows. AT: full NVDA and Narrator menu pass, plus a step-0 check the owner reads. Wave 4. Depends on: DS-4, DS-12. Decision: DSD7. Interlock: `MenuBarCensus`, `MenuItemForegroundTests` and `SpokenChords` all read `MainMenu`'s shape.

## Goal

The row holds one logo button that opens the app menu, and File, Workspace, Base, Editor, Canvas and Graph cascade from it. Alt, F10, Alt+F and its siblings, arrows, Enter, Esc and F6 all work, and a screen reader meets a menu bar and menus as it does today.

## Current state on main

- **The bar:** `MainWindow.xaml:110`, `x:Name="MainMenu"`, six top-level items (`:199`, `:302`, `:385`, `:455`, `:600`, `:701`), with `AutomationId`s `FileMenu` to `GraphMenu`.
- **Mnemonics:** `EveryTopLevelMenuHasAUniqueMnemonicAndAnAutomationId` (`MenuBarCensus.cs:55`). `_File` and the rest are live because the headers are realized in the bar. A header inside a closed popup isn't realized, so its access key isn't live. No Alt+letter chord exists in `chords.json`, but the find bar registers access keys on the same letters: `_Next` and `_Whole word` (`FindInNoteBar.xaml:60`, `:70`), with `_Previous`, `Match _case`, `_Replace` and `Replace _All`. Alt+N and Alt+W there belong to the find bar.
- **Activation:** `IsMainMenu` isn't set, so it's `Menu`'s default of true, which gives Alt and F10.
- **F6:** `MainWindow.ShellRegions.cs:144-152` focuses the first container. `MainMenu_PreviewKeyDown` (`:756`) hands F6 on.
- **Who reads the shape:** `MenuBarCensus.TopLevelItems`, `TheMenuBarsOwnArrowsStillWrap`, `CommandDriftTests`, `ModalSurfaceTests`, `ShellAccessibilityTests.cs:3697`, `ShellAccessibilityTests.SpokenChords.cs:78`, `ShellAccessibilityTests.cs:9139` and `:9943` (menu control types), `ThemeDesignSystemTests.TheMenuBarDrawsTheThemesTextOnTheThemesWindow`, `CommandPaletteTests.ShellKeys.cs:799` (`MainMenu.Items[0]` is `_File`). About twenty gate sites use the top-level ids through `WaitForMenuItem` or `WaitForElement`, among them `ShellAccessibilityTests.cs:341`, `:738`, `:814`, `:7357`, `:11347`, `ShellAccessibilityTests.RegionStops.cs:88`, `ShellAccessibilityTests.Rescan.cs:1030`, `ShellAccessibilityTests.ShellRegions.cs:58`, `:106`.
- **History:** W7-5 (#1239) and W7-7 (#1247) settled Tab and arrows on this bar through NVDA passes. The comment at `MainWindow.xaml:90-109` records why.

## Reference

`design/windows/readme.md:120`: Alt+F opens the popup; Alt+W, B, E, N, G and H open it at that menu; the same letters switch menus while it's open; arrows, Enter and Esc as in any Windows menu; built from the existing `MenuItem` tree so UIA semantics, `InputGestureText` and `AcceleratorKey` carry over. `wpf-feasibility.md:115`. `readme.md:127`: `AcceleratorKey="Alt+F"` on the logo. `ui_kits/slate-windows/Chrome.jsx` `AppMenu`.

## Design

1. **Step 0, read by the owner before more code.** A branch with the tree nested under one logo item and no key handling. NVDA and Narrator: Alt, F10, F6, Down, Right, Esc. Record what each says, against a recording of today's bar. If it reads worse in a way the template can't help, stop and take it to the owner. The fallback is DS-12's row with headers in it.
2. **The tree.** `MainMenu` stays a `Menu`, keeping `Focusable`, `TabNavigation`, `DirectionalNavigation`, its modal style and `PreviewKeyDown`. It holds one `MenuItem`, `x:Name="AppMenuItem"`, `AutomationId="AppMenu"`, `Header` a 20 logo image, name "Slate menu" and `AcceleratorKey="Alt+F"`. File to Graph move inside it, unchanged. Their roles become `SubmenuHeader`, and DS-4's template already serves that.
3. **The logo.** `design/windows/assets/logo/slate-gold.png`, linked as a `Resource` at the size the row needs, `RenderOptions.BitmapScalingMode="HighQuality"`. The file is 99 KB, so a downscaled copy is fine if it's kept beside the original with the size noted.
4. **Alt+letter.** A pure `AppMenuKeys.Decide(systemKey, modalOpen, liveAccessKey)` says what to do, so a fact can reach it. In `Window_PreviewKeyDown` (`MainWindow.xaml.cs:1006`), when `e.Key == Key.System`, no modal surface is open, `e.SystemKey` is F, W, B, E, N or G, and `AccessKeyManager.IsKeyRegistered(null, letter)` is false, so that a live control such as the find bar keeps its own key: open `AppMenuItem`, make sure its containers exist (`UpdateLayout`, `ItemContainerGenerator.ContainerFromItem`), open that submenu, focus its first enabled row, and mark the key handled. Alt+F opens the app menu at File. Alt+H is left unbound until #756 adds Help. Only these letters, as `readme.md:120` lists.
5. **The same letters while open.** Inside the open popup, the headers' own access keys work without Alt. Left and Right are WPF's between a submenu and its parent. Check Alt+letter while open, too: close the open submenu and open the new one.
6. **F10.** Enters menu mode as today, then opens `AppMenuItem` through a handler, as `wpf-feasibility.md:115` asks. Plain Alt keeps WPF's behaviour: focus on the logo, no popup.
7. **Tests' shape.** `MenuBarCensus.TopLevelItems` looks one level down from `AppMenuItem`, and pins that `MainMenu` has exactly one top-level item. `MenuBarCensus.cs:112-114`'s wrap test goes, because a single header has nothing to wrap to; say so in its place. `WaitForMenuItem` opens `AppMenu` first, and each gate site that waits for a top-level id directly does too, or waits for `AppMenu`: F6's landing is now the logo (`ShellAccessibilityTests.ShellRegions.cs:58`, `:106`). `CommandPaletteTests.ShellKeys.cs:799` takes `AppMenuItem.Items[0]`.

## Tests

Failing first:

- `TheBarHoldsOneItemAndTheMenusSitUnderIt`, and the census moved a level.
- `AppMenuKeysDecide`: a pure theory: each letter opens its menu; nothing under any `ModalSurface`; nothing when the key is live elsewhere; `H` isn't claimed yet. A hosted `KeyEventArgs` can't carry `SystemKey`, and a hosted popup can't take focus (`CanvasNavigatorTests.cs:6011-6016`), so what follows needs the gate.
- Gate, `AppMenu_AltLetterOpensThatMenu`: F, W, B, E, N, G, each by real keys; the menu opens with focus on its first enabled row.
- Gate, `AppMenu_AltLetterWhileOpenSwitchesMenus`, `AppMenu_F10OpensItAndPlainAltFocusesTheLogo`.
- Gate, `AppMenu_TheFindBarKeepsItsKeys`: with the find bar open, Alt+N steps to the next match and Alt+W toggles whole word.
- `F6LandsOnTheLogoAndAnnouncesTheMenuBar`: `ShellRegionCycleTests` holds.
- Gate: `SpokenChords_MenusPaletteAndOverlays_MatchTheTable` on the new shape. Its walk gains a level, and every chord must still match. `FluentShell_...` holds.

Launched app: each menu open from the logo, at 100% and at 200% if the display is there.

## Docs and registers

`w_c_matrix.md`'s menu rows. `readme.md:120` and `wpf-feasibility.md:115`: why it stayed a `Menu`. `MainWindow.xaml:90-109`'s comment.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~MenuBarCensus|FullyQualifiedName~AppMenu|FullyQualifiedName~MenuItemForegroundTests|FullyQualifiedName~ShellRegion|FullyQualifiedName~CommandDriftTests|FullyQualifiedName~ModalSurfaceTests|FullyQualifiedName~CommandPaletteTests|FullyQualifiedName~GraphMenuTests|FullyQualifiedName~ReadingShellToggleTests"`, and the accessibility project's whole shell gate with the desktop free, since about twenty of its facts walk through these menus.

AT: the full menu section of the NVDA matrix (W7-5's F-series) and a Narrator run: Alt, each Alt+letter, a checkable row, a disabled row's reason, a submenu, Esc back out, focus returned to where it was. Record the phrases. This PR doesn't merge without them.

## Acceptance

- [ ] Step 0's recording was read by the owner.
- [ ] Every menu command is reachable by the same chord and the same mnemonic path, one level deeper.
- [ ] The field pass is recorded.
- [ ] The menu bar takes no Tab or arrow from outside (`NeitherTabNorAnArrowEntersTheMenuBar`).

## Brief

Read `MainWindow.xaml:90-200`, `MenuBarCensus.cs`, `MainWindow.ShellRegions.cs:144-152`, `:756-775`, `MainWindow.xaml.cs:1006`, `readme.md:120`. Do step 0 first and stop for the owner. Then the logo, the Alt+letter handler and F10, then the census and the tests. Commit: `feat(windows): the menus open from the logo with every key they had`.
