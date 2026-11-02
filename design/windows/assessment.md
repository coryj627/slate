# Slate Windows Design System — Assessment

Reviewed on 2026-11-01 against the Fluent 2 Web kit, the `coryj627/slate` repo, and the rendered cards in light and dark mode.

**Summary:** the foundations are good. The tokens come from real sources, the three typefaces have clear jobs, every font is now hosted locally, and the gold accent passes contrast checks. The parts a designer touches most are hard to use, though. The components still carry Figma's naming and placeholder content. There are two sets of neutral colours that don't quite match. And there are four different ways to draw an icon. Most of the fixes are small and low-risk.

---

## What works

- **Foundations come from real sources.** Neutrals and text colours are copied exactly from the app's light and dark theme files. Text contrast meets the APCA Lc ≥ 75 threshold the app's own tests use. The High Contrast behaviour is written up.
- **Each typeface has one job.** Segoe UI Variable for the interface, Open Sans for the reading view, Cascadia Code for the editor. Each has its own type scale.
- **The gold colour scale is easy to adopt.** It is plugged into Fluent's brand colour slots, so every Fluent component that uses the brand colour picks up gold without changes.
- **The copy guidance is specific.** It covers capitalisation, ellipses, announcement sentences and how disabled commands explain themselves, with real examples from the app.

## Issues, by priority

### P1 — Things that look wrong or behave inconsistently

1. **Component previews show Figma placeholder content.** Buttons and badges show placeholder circles, tabs show calendar icons, tags show a fake "MB" avatar, the compound button shows a "40×40" image, and one search box says "Search for messages, files, and more" (copy from Microsoft Teams). That last one is in the Components → Inputs card; the others are in the Buttons, Navigation and Badges cards. These come from the Figma defaults (for example `icon` defaults to true, `avatar` to true).
2. **Some icons disappear in dark mode.** The Status icons (Synced, Syncing, Warning) and the icons inside tags draw near-black on the dark background. Their fill is fixed rather than following the theme.
3. **Breadcrumb renders broken.** The text overlaps placeholder circles (Navigation card).
4. **Primary buttons change colour between modes.** Slate's own accent in dark mode is bright gold (#E2AD24) with dark text. Fluent components in dark mode use Fluent's own mapping, which is dark brown (#6B4D00) with white text. So a "primary" control looks different depending on which layer drew it.
5. **There are two neutral colour systems.** The two are close but don't match, so a Fluent component sitting on a Slate surface looks slightly off:

   | Role | Slate (app theme files) | Fluent kit |
   | --- | --- | --- |
   | Light text | #171717 | #242424 |
   | Light stroke | #E0E0E0 / #767676 | #D1D1D1 |
   | Dark surface | #1E1E1E | #292929 (component background) |

   This does match how the WPF app works today: Fluent's own colours for standard controls, Slate's colours for Slate's own surfaces. It should still be a deliberate choice.
6. **Text selection is still blue** (#004578 light, #B9D9FF dark, the app's placeholder values). It is the only blue left in an otherwise gold system.

### P2 — Readability and naming

7. **33 components have numbered names.** Examples: `Chevron2/3/5`, `Placeholder2–6`, `AvatarAvatar2/3`, `Button3`, `Text2`, `Info2`. The number says nothing about how they differ. Most are near-copies or internal pieces, but they sit in the public list next to `Button`.
8. **Prop names and values are inconsistent.**
   - `style2` (35 components) is a workaround for the name `style` being reserved.
   - 40 components use values like `"medium (default)"`.
   - Size scales come in at least eight forms: `sm|md|lg`, `sm|"medium (default)"|lg`, `"small (default)"|md|lg`, `extra small|sm|"medium (default)"`, and so on.
   - On/off is `checked: boolean` on Switch but `checked: "on"|"off"` on Radio.
9. **Icon names are long and some are ambiguous.** For example `SearchSize20ThemeRegular` and `ChevronDirectionRightSize12Theme` versus `…Theme2`, where "Theme2" turns out to mean Filled.
10. **There are four ways to draw an icon:** the 88 standalone icon components, the shared `Icon` component, the Segoe Fluent Icons font, and the CDN icons used in the UI kit. Nothing says which one to use first.
11. **There are three spacing and radius vocabularies.** `--space-*` and `--radius-*` (in px), Fluent's unitless `--spacing-horizontal-*` and `--corner-radius-*`, and the `-2…-10` duplicates (1,603 suffixed names out of 3,369).
12. **Some token names are confusing.** `--colors-brand-comm-blue-*` now holds gold. `--slate-slate` repeats itself.

### P3 — Organisation and documentation

13. **`components/fluent/` is one flat folder of about 960 files.** That's 239 components (×3 files each) plus 19 cards and the assets.
14. **The readme doesn't open with a quickstart.** It lists 239 names in three paragraphs. There's no "which token or component do I use?" guidance. Two lines are out of date: icon gaps "use the CDN" (the font now covers them), and "Segoe Serif — none shipped".
15. **The UI kit is mostly hand-built.** It uses the tokens but very few DS components. The generated components are fixed-size copies of the Figma frames, which makes them hard to fit into app layouts. This is the main tension in the system and is worth stating openly.
16. **Fluent's other theme presets add noise.** The token file defines 112 theme scopes besides light/dark, mostly Microsoft presets (Word, Teams, Copilot, …) and avatar colours. They have no meaning for Slate.

---

## Recommended fixes

| # | Fix | Effort | Risk |
| --- | --- | --- | --- |
| A | Clean up the component cards: turn off placeholder icons, avatars and images, use Slate copy, fix the Breadcrumb and row labels | S | none |
| B | Make the fixed icon fills follow the theme so they show in dark mode | S | low |
| C | Accept short prop values (`size="md"`, `appearance=`) alongside the Figma ones, keeping the Figma values working | M | low |
| D | Move internal and numbered pieces to `components/fluent/internal/` and list only the public set | M | medium (imports get rewritten) |
| E | Pick one primary style for dark mode and remap the Fluent dark brand colours to match | S | low |
| F | Map Fluent's neutral roles to Slate's values, or document the split as intentional | S | low |
| G | Set one icon order: Segoe Fluent Icons first, the `Icon` SVGs second, drop the CDN icons from the UI kit | S | none |
| H | Rewrite the readme: quickstart, "which token do I use?", components grouped by job, remove the stale lines | S | none |
| I | Rename the confusing tokens (`--slate-grey`) and add a note on the brand overrides | S | low |
| J | Group the components into folders by job (forms, navigation, feedback…) | L | medium |

---

## Status — applied 2026-11-01

| # | Fix | Status |
| --- | --- | --- |
| A | Clean component cards | Done. Placeholder icons, avatars and images are off, with Slate copy throughout. Breadcrumb is rebuilt from `BreadcrumbItem` with a new `label` prop, and `SearchBox` has a `placeholder` prop. |
| B | Dark-mode icon fills | Done. 102 duplicate role tokens that were only defined for light mode now follow their base role. The fixed greys and blue in Tag, InteractionTag, TagPicker, BreadcrumbItem, Rating and Star now use tokens. |
| C | Short prop aliases | Done. `appearance` works for `style2`, `small|medium|large` for every size scale, and booleans for on/off props. Old values still work, and the `.d.ts` files list both. |
| D | Internals to `/internal` | Done. 146 pieces moved there: icon components, placeholders, numbered duplicates, `*Base` parts and deprecated sets. |
| E | One dark primary | Done. Fluent's dark brand roles now use `--accent` (#EECF7E since 2 November 2026) with #171717 text. |
| F | Unify neutrals | Done. `tokens/fluent-slate.css` maps Fluent text, stroke and background-1/2 roles to the Slate xaml values. |
| — | Gold text selection | Done. Light #F9E7B8 with #171717 text, dark #6B4D00 with #F5F5F5 text. `::selection` is set in base.css. |
| G | One icon order | Done. Segoe Fluent Icons first, then `Icon` SVGs. The UI kit no longer loads the online (CDN) icons. |
| H | Readme rewrite | Done. It now has a quickstart, a "which token" table, prop conventions, components grouped by job, and the icon order. The stale lines are gone. |
| I | Token renames | Done. `--slate-slate*` is now `--slate-grey*`, with the old names kept as aliases. `comm-blue` has a comment explaining it. |
| J | Group component folders | Done. `components/{actions,forms,navigation,feedback,overlays,content,internal}/`. The generated Fluent tokens and assets moved to `tokens/fluent/`. |
| — | UI kit on DS components | Partly done. Icon buttons use `Button`, menu rows `MenuItem`, right-pane tabs `HorizontalTab`, and the toast `Toast`. The editor, reader and sidebar rows stay token-built because they are Slate-specific. |

**Still open:**
- Issue 11: the Fluent duplicate token names (`-2…-10`) remain. Components depend on them.
- Issue 16: the 112 Fluent theme presets (Word, Teams, Copilot…) are still in `fig-tokens.css`.
- If the Figma components are generated again, `fig-tokens.css` is regenerated too. That would undo the weight-token fix applied to it earlier, and the generated components would come back without the hard-coded colour fixes.

## Follow-up fixes — buttons, collapsed controls, contrast (2026-11-01)

- **Button overflow (Dialog, Drawer, InlineDrawer, OverlayDrawer, DrawerFooter, TeachingPopover footers, MessageBar).** The Figma export kept a placeholder icon that was hidden in Figma, and it overflowed fixed-width buttons. I removed 53 of these icons and dropped the fixed widths, so buttons size to their label with Fluent's minimum width (96/64).
- **Invisible controls.** The unchecked Radio and Switch drew nothing, because the indicator box collapsed to 0×0. RadioBase now has an explicit height and SwitchBase an explicit width, and the switch track uses `border-box` so it no longer clips.
- **Disabled variants drew enabled colours** (labelled Switch, Radio). `__slateNorm` now scopes disabled overrides on any `state="disabled"` component, so instance overrides that were lost in the export can no longer leak. Disabled checked switches also get a disabled-stroke outline.
- **Disabled contrast.** There are new `--text-disabled`, `--border-disabled` and `--surface-disabled` tokens. All Fluent disabled roles now map to them, so text is ≥ 4.5:1 and strokes ≥ 3:1.
- **Icons were fixed dark grey (#242424) and vanished in dark mode** (for example the menu checkmarks and the Dialog close button). 94 icon components now inherit their colour from context. Baked text and icons in 16 other components use `--neutral-foreground-1-rest`.
- **List and menu headers** ("Sort by") use secondary text (#3A3A3A) instead of tertiary.
- **Menus card** now sits on the Fluent flyout surface (`--neutral-background-1-rest`), uses real Slate commands and chords, and shows checkmarks only on selected rows.

## Slate patterns and menu states (2026-11-01)

You asked for these after the parity research (`scraps/parity-notes.md`). They are in `components/patterns/`, and the Workspace kit uses all of them.

- **Menu states.** A new card (Menu · States) shows rest, hover, keyboard focus, pressed, checked, unchecked, submenu and disabled rows in one menu, with the alignment rules. `MenuItem` had two bugs. Unchecked rows dropped the check column, so their labels started left of checked rows. This was the same misalignment as Math and Code Preamble Verbosity in the WPF Editor menu. The focus ring was also fixed at 240 px. Both are fixed. The WPF note on the card covers the submenu-header template.
- **PaneSwitcher**, **PanelToolbar**, **NoteHeader**, **EmptyStateLauncher**, **CountListRow**, **SettingsLayout** and **SettingRow** are built from Button, Tooltip, MenuItem, VerticalTab and the Slate tokens. Each one has a card, a `.d.ts` (marked as a starting point) and a `.prompt.md`.
- **Settings content** uses Slate's real preferences from `AppPreferencesStore`, `EditorPreferencesViewModel` and `SidebarSettingsStore`, for example the Code preamble verbosity labels and ClearSpeak/SimpleSpeak.
- **Open:** Slate has no Settings window or icon strip yet, so these are proposals. The right-pane icons (Segoe Fluent Icons) are my picks and need your review. Canvas announcement verbosity is a real preference, but it isn't in the Settings mock because no icon fits Canvas without clashing with Base dock.
