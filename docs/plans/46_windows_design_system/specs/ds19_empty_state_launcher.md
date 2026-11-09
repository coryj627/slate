# DS-19 — An empty pane offers its next actions

Issues: filed by DS-0. Size S. Lanes: windows. AT: NVDA spot. Wave 5. Depends on: DS-2, DS-9. Decision: none. Interlock: the empty pane's F6 stop.

## Goal

With no tab open, the pane shows the logo, "No note open" and up to five actions, each with its chord, instead of one line of text.

## Current state on main

- `WorkspaceTemplates.xaml:927-947`: one line, "Open a note from the Files list, or press Ctrl+O to find one.", an `AutomationPresentationTextBlock` kept out of the tree, because "A screen reader hears the empty pane's line when it lands here" (#1774).
- **The empty pane's stop:** `ContentPaneBorder` when no tab is open (`MainWindow.ShellRegions.cs:88-93`), `ShellRegionKind.EmptyEditor`. Only the border itself classifies that way: anything else inside `ContentPaneBorder` is `Editor` (`MainWindow.ShellRegions.cs:95-97`), and with no tabs `Editor` isn't in the ring.
- **An empty tab control isn't a stop:** `WorkspaceTemplates.xaml:835-840`, `:846-847`.
- **Verbs:** Quick Open (`MainWindow.xaml:46`), New Note from Template, Search Vault.
- **Chord text:** `cmd:ChordText` (for example `MainWindow.xaml:342`).

## Reference

`design/windows/components/patterns/EmptyStateLauncher.prompt.md`: title 20/28 semibold, description 14/20 secondary, each action a 32 row 320 wide with label and chord, centred; show shortcuts with `{cmd:ChordText …}`. `ui_kits/slate-windows/index.html:68`. `assets/logo/slate-gold.png` (DS-13 links it; if this lands first, it links it).

## Design

1. **The content** replaces `WorkspaceTemplates.xaml:929-947`, shown under the same `Tabs.Count` trigger: logo at 48, `Slate.SubtitleTextBlockStyle` "No note open", three `Button`s in a `Slate.LauncherRowStyle` (whole template, as in DS-9; icons, if any, as `AutomationPresentationTextBlock`s): Quick Open…, New Note from Template…, Search Vault…, label left, chord right through `cmd:ChordText`, bound to the existing commands.
2. **It's now in the tree.** The buttons are Tab stops. The line the region announces (`ShellRegionKind.EmptyEditor`'s landing) stays core's. The decorative parts are out of the tree, as the line was.
3. **Don't take the landing.** F6 still lands on `ContentPaneBorder`, as today, and Tab from there goes to the first action. `FocusedRegion` changes: with no tabs, focus anywhere inside `ContentPaneBorder` is `EmptyEditor`. Otherwise F6 from an action reads as `Editor`, is placed by its slot, and re-lands on the empty pane instead of moving on. `FocusManager` paths for an empty workspace (#1239's fallback seats the Files tree) don't change.
4. **Under a modal surface** these buttons are disabled with the shell, which the existing style triggers already do.
5. **The Welcome view** (`MainWindow.xaml:841-843`) is a different screen with its own Recent Vaults, and stays.

## Tests

Failing first:

- `AnEmptyPaneOffersItsActions`: three buttons, each bound to a registered command, chord text from the table.
- `TheActionsGoAwayWithATab`.
- `F6StillLandsOnTheEmptyPane`, and `F6FromALauncherActionMovesOn`: `ShellRegionCycleTests`.
- `WithNoVaultNoOverlayShowsAndThePointerReachesTheWelcomeView` holds.
- `FocusIntegrityManifestCensus` gains the sites.

Launched app: an empty workspace in a vault, dark; hosted light.

## Docs and registers

`wpf-feasibility.md:147`.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~EmptyPane|FullyQualifiedName~ShellRegion|FullyQualifiedName~FocusIntegrity|FullyQualifiedName~LayoutItemsControlTests"`.

AT: NVDA, F6 to the empty pane: its line as before; Tab to each action: label and chord; Enter opens Quick Open and Esc returns.

## Acceptance

- [ ] Each action's chord matches the menus.
- [ ] The empty pane's spoken line is unchanged.

## Brief

Read `WorkspaceTemplates.xaml:835-950`, `MainWindow.ShellRegions.cs:82-98`, `EmptyStateLauncher.prompt.md`. Replace the line with the launcher, keep the trigger, bind existing commands. Commit: `feat(windows): an empty pane offers its next actions with their chords`.
