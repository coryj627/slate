# Slate Windows Design System

Slate is a desktop knowledge-management and note-taking app for Windows, built accessibility-first. It reads plain Markdown vaults (Obsidian-compatible at the data level) with YAML front matter, and pairs a source-code style **editor** with an alternative **reading view** that renders front matter as collapsible properties, Markdown, and embedded cards (images, embedded notes, raw code blocks). Around that sit a metadata-rich Files sidebar (tags, properties, dates, note purpose), backlinks / outgoing links, an outline, citations and bibliography tooling, a command palette, and full keyboard reach for every surface — while still offering a clean icon-button UI for mouse and touch users.

This design system targets the **Windows app (WPF)**. WPF is themed to match Microsoft's Fluent 2 / WinUI guidance; where WPF can't reach WinUI, Slate falls back to the Fluent 2 **Web** kit, which is the component and token source here.

## Quickstart

```html
<link rel="stylesheet" href="styles.css">            <!-- every token + font -->
<script src="_ds_bundle.js"></script>                 <!-- every component -->
<script type="text/babel">
  const { Button, Input, MenuItem, Icon } = window.SlateDesignSystem_326dc9;
  <Button appearance="primary" size="medium" icon={false} label="Save" />
</script>
```

- Put `data-theme="dark"` on `<html>` for dark mode. Every token and component follows it.
- **Write UI with the semantic tokens** (table below). Fluent's own tokens (`--neutral-*`, `--brand-*`, ~3,400 names) exist for the components; you rarely need them directly.
- **Components are exact Figma frames.** They render every documented variant pixel-for-pixel but have fixed geometry and no built-in interaction. Drive hover/selected with the `state` prop, and wrap them in a native `<button>`/`<input>` for real behaviour (see `ui_kits/slate-windows/`). For app-specific surfaces (editor, reader, sidebar rows) build from tokens.

### Which token do I use?

| Need | Token | Light / dark |
| --- | --- | --- |
| Window background | `--surface-window` | #F7F7F7 / #121212 |
| Panel, card, editor | `--surface-card` · `--surface-editor` | #FFFFFF / #1E1E1E |
| Raised strip, header | `--surface-raised` | #EFEFEF / #292929 |
| Hover · pressed overlay | `--surface-hover` · `--surface-pressed` | 4% · 8% black / 6% · 10% white |
| Selected row | `--surface-selected` + 3px `--accent-indicator` pill | #FDF5E1 / #33291A |
| Text | `--text-primary` · `--text-secondary` · `--text-tertiary` | #171717 #3A3A3A #555555 / #F5F5F5 #D4D4D4 #D4D4D4 |
| Primary fill · text on it | `--accent` · `--text-on-accent` | #7D5A00 + white / #EECF7E + #171717 |
| Links, wikilinks, tags | `--accent-text` | #6B4D00 / #EECF7E |
| Control border · divider | `--border-strong` · `--border-subtle` | #767676 #E0E0E0 / #AAAAAA #3D3D3D |
| Text selection | `--selection-bg` · `--selection-text` | #F9E7B8 + #171717 / #6B4D00 + #F5F5F5 |
| Disabled text · stroke · fill | `--text-disabled` · `--border-disabled` · `--surface-disabled` | #6B6B6B #8A8A8A #F0F0F0 / #919191 #757575 #141414 |
| Focus ring | `--focus-ring` (2px, always visible) | text colour |
| Status | `--status-error` · `--status-warning` · `--status-success` | — |
| Type | `--font-ui` · `--font-ui-display` · `--font-reader` · `--font-editor` · `--font-icons` | — |
| Spacing · radius · shadow | `--space-xxs…xxxl` · `--radius-small…xlarge` · `--shadow-2…64` | px values |

### Prop conventions

Every component accepts short, consistent values. The original Figma values still work.
- `appearance` instead of `style2` (`"primary" | "secondary" | "outline" | "subtle" | "transparent"`, or that component's own set).
- `size="small" | "medium" | "large"` (also `"extra small"` / `"extra large"` where they exist) instead of `"sm"`, `"medium (default)"`, `"lg"`.
- Booleans for on/off props (`checked={true}` on Radio, `icon={false}` on BreadcrumbItem).
- Placeholder content is on by default, because it is in Figma. Turn it off explicitly: `icon={false}`, `avatar={false}`, `image={false}`, `leftIcon={false}`.
- Not every combination exists. A component renders only the frames in its Figma component set, listed in `__impls` in its `.jsx`. A combination with no frame renders the component's first frame, with no warning, so set enough props to name a real one.

## Sources

- **GitHub:** [coryj627/slate](https://github.com/coryj627/slate) — Rust core + native front ends. Read for this system: `apps/slate-windows/src/SlateWindows/Themes/Slate.{Light,Dark,Contrast}.xaml` (colour tokens), `ThemeManager.cs` (Fluent dictionaries first, Slate tokens layered last; High Contrast maps to SystemColors), `MainWindow.xaml` (menu bar and commands), `docs/help/sidebar.md`, and `demo-vault/` (sample content). Explore the repo further — especially `docs/plans/` and `docs/help/` — to design new surfaces faithfully.
- **GitHub:** [dotnet/wpf](https://github.com/dotnet/wpf/tree/main/src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Fluent): the WPF Fluent theme's `Resources/Theme/Light.xaml` and `Dark.xaml`, read for the brush keys that `xaml/` overrides.
- **Figma:** "Microsoft Fluent 2 Web (Community).fig" — components, 3,414 variables (tokens), icons. Mounted and imported in full.
- **Figma:** "Windows UI kit (Community).fig" (WinUI): reviewed in `winui-review.md`. Ten pieces are built from it (see Components); the rest is skipped on purpose. Its variables are saved for reference in `scraps/winui/fig-tokens.css`, which `styles.css` doesn't load.
- **Uploads:** `slate-gold.png` (logo), Open Sans (variable TTFs). Cascadia Code / Mono (TTFs).

## Index

- `styles.css`: the only entry point. It `@import`s everything below.
- `tokens/`
  - `fonts.css`: every `@font-face`.
  - `brand.css`: the gold ramp, plus the Fluent brand slots re-keyed to gold.
  - `colors.css`: Slate app colours and the semantic aliases.
  - `fluent-slate.css`: the bridge that makes Fluent components use Slate's neutrals, dark-mode primary and theme-correct duplicates.
  - `typography.css`, `spacing.css`, `base.css`.
  - `fluent/`: the generated Fluent tokens (`fig-tokens.css`, all 29 collections and every mode), `fig-assets.css` with `assets/`, and `fig-typography.css` (empty, because the kit has no text styles).
- `components/<group>/`: Fluent 2 components (`.jsx` + `.d.ts` + `.prompt.md`) and `*.card.html` specimens. The groups are `actions/`, `forms/`, `navigation/`, `feedback/`, `overlays/`, `content/`, and `internal/` (building blocks, not for direct use).
- `components/patterns/`: Slate patterns built from the Fluent components (pane switcher, panel tool row, note header, empty-state launcher, count list row, settings layout and row).
- `assets/logo/slate-gold.png`: the logo. `assets/icons/`: the `Icon` component, `icon-data.js` (70 Fluent icon families), and the icon-font cards.
- `guidelines/`: foundation cards (Colors, Type, Spacing, Brand).
- `spikes-issue.md`: the six WPF validation spikes as a paste-ready GitHub issue, with the follow-up for each failure.
- `wpf-feasibility.md`: what WPF's Fluent theme already covers, what needs templates or code, suggested design-system changes, and the spike results. `spikes/` holds their screenshots, and the `theme-` images there show the app with these themes (#1755).
- `winui-review.md`: the WinUI kit compared with this system and the WPF Fluent theme, with proposed changes.
- `xaml/`: `Slate.Light.xaml` and `Slate.Dark.xaml`, generated drop-in replacements for the app's theme files (accent roles, Slate keys, shadows, Fluent brush overrides), and `README.md` with the accent rules, theme setting, shadow values and text-size rules.
- `ui_kits/slate-windows/`: the interactive Windows workspace.
- `fonts/`: Open Sans, Cascadia Code and Cascadia Mono. The Segoe faces aren't in this repo; see Fonts.
- `assessment.md`: the first consistency review and the status of each fix. `review-2026-11.md`: the end-to-end review of 2 November 2026 (card fixes, WinUI/Web alignment per family, open items). `SKILL.md`, `github.md` and `thumbnail.html` sit at the root too.

## Content fundamentals

Copy comes from the shipped menus, help docs and announcements in the repo.

- **Plain, literal, precise.** Labels say exactly what happens: "Open in New Tab", "Reopen Closed Tab", "Jump to Bibliography", "Use Vault Default Sort". No marketing words.
- **Title Case for commands and menu items**; an ellipsis (…, the single character) when a command opens a dialog or needs more input: "Open Vault…", "Add Property…", "Move To…". Sentence case for body copy, help text and announcements.
- **Announcements are full sentences with a period**, and report outcomes honestly, including partial failure: "Tagged 4 files with #draft. 2 skipped.", "Files refreshed. No changes.", "42 results.", "No results.".
- **Disabled verbs explain themselves.** "Select exactly one folder to manage its folder note." / "Open is available only for files." Never silently grey out in menus or the palette.
- **Second person, implied.** Help text addresses "you" ("your selection", "you can't ask to remove something that isn't there"); the app never says "I" or "we". The founder's blog in the demo vault is first person, but that is user content, not UI.
- **Keyboard chords are always shown** next to commands (`Ctrl+Shift+E`, `Ctrl+Alt+I`, `F6`), written with `+` on Windows.
- **No emoji** in UI. Unicode is limited to typographic marks (…, —, ▸ in docs for menu paths like "Settings ▸ Sidebar").
- **Vocabulary:** vault, note, folder note, properties (not "metadata" in UI), backlinks / outgoing links, shortcuts, recents, pins, Bases, Canvas, reading mode / source.

## Visual foundations

- **Overall vibe:** quiet, utilitarian Windows 11 Fluent — neutral greys, high text contrast, content first. Slate gold is a *signal* colour (selection indicators, primary actions, links), never a large background inside the app. The logo tile is the only gold field.
- **Colour.** Neutrals and text pairs come verbatim from `Slate.Light/Dark.xaml` (#F7F7F7 window / #FFFFFF surface / #EFEFEF raised; dark #121212 / #1E1E1E / #292929). The code gates text pairs at **APCA Lc ≥ 75**, so secondary text is darker than Fluent's default (#3A3A3A, #555555). The accent moves from the provisional blue (#005A9E) to the **gold brand ramp** derived from the logo: light mode fills with gold-80 #7D5A00 (white text 6.3:1) and indicators in gold-90; dark mode uses a paler gold, #EECF7E, with dark text, because logo gold is below the Lc 75 floor as text on dark surfaces. Slate grey #47515B is reserved for the brand mark and app icon. High Contrast: every token maps to SystemColors (WindowText, Highlight…), never a fixed brush — preserve this in WPF.
- **Type.** Three voices: Segoe UI (Variable) for all chrome per Fluent; **Open Sans** for the reading view (16/26, 72ch measure); **Cascadia Code** for the source editor (14/22, ligatures). Fluent ramp: Caption 12/16, Body 14/20, Subtitle 20/28, Title 28–32. Weights 400 and 600; 700 only for Reader H1 and editor headings. The UI ramp is the **WinUI type ramp** (`--type-caption` 12/16, `--type-body` 14/20, `--type-body-strong`, `--type-body-large` 18/24, `--type-body-large-strong`, `--type-subtitle` 20/28, `--type-title` 28/36, `--type-title-large` 40/52, `--type-display` 68/92), each a `font` shorthand with the right optical size. Use these for Slate surfaces. The Fluent Web ramp tokens stay because the Web components use them.
- **Spacing & layout.** Fluent 2 4-px base: 2, 4, 6, 8, 10, 12, 16, 20, 24, 32. Shell: 48 title bar (logo app menu, title, quick commands and caption buttons in one row; see Window chrome), 36 tab strip and 24 status bar. These heights, and every row that holds text, are minimums that grow with the Windows text size (up to 225%). Panes: 260 Files sidebar and 300 right pane, both resizable by keyboard (Widen/Narrow commands). Content never sits under fixed overlays; the palette and dialogs are centered overlays.
- **Backgrounds.** Flat solid surfaces only. No imagery, gradients, textures or illustrations inside the app. No Mica or acrylic: Slate keeps every text surface solid, and WPF popups can't show acrylic. *Material acrylic* from the Fluent kit is web-only.
- **Corner radii.** 4px controls (buttons, inputs, list rows), 8px tabs (top corners), cards, flyouts, menus and dialogs, circular for avatars and the selection pill. Menu and navigation highlights are radius 3 inside a 5 px inset (WinUI).
- **Borders.** 1px subtle strokes (#E0E0E0 / #3D3D3D) between regions; input underline uses the strong border. Region separation is by surface tone + 1px line, not shadow. A control's 1px border sits inside its size, so outline, secondary and primary buttons are the same height.
- **Elevation.** One shadow per level, because WPF draws one `DropShadowEffect` per element: 2 card media, 4 and 8 cards, 8 tooltips and floating toolbars, 16 menus, popovers, dropdowns and toasts, 28 the command palette, 64 dialogs. Black at 18%, 36% in dark mode. XAML values are in `xaml/README.md`.
- **Cards.** Embedded notes, code blocks, backlink snippets: surface fill, 1px subtle border, 8px radius, a raised-surface header strip with an icon + title + icon button. No coloured left borders.
- **One neutral system.** Fluent components read Slate's neutrals: text, control borders, dividers and the window and panel surfaces. A Fluent control and a Slate-built surface never disagree on grey. Fluent's hover and pressed steps are kept.
- **Primary in dark mode** is pale gold #EECF7E with #171717 text, for Slate surfaces and Fluent components alike.
- **Theme and accent settings.** Settings › Appearance has **Theme** (Light, Dark, Use Windows setting; default Use Windows setting) and **Accent color** (Automatic, Slate gold, Windows accent; default Automatic). Automatic uses gold unless the user has picked their own accent in Windows. Everything gold follows the accent setting, and a Windows accent is used as Windows gives it, whatever its contrast (plan 46, DSD16). Designs always show gold; where a Windows accent must be shown, use the Windows default blue (#005FB8 light, #60CDFF dark). Rules: `xaml/README.md`.
- **Text selection** is pale gold (#F9E7B8) in light mode and dark gold (#6B4D00) with light text in dark mode. Find-in-note matches use a similar yellow, so they always draw their ring (`--find-match-ring`) to stay distinct.
- **Disabled states stay legible.** Disabled text is at least 4.5:1 and disabled strokes at least 3:1 on every Slate surface. Fluent's defaults (#BDBDBD, #E0E0E0) were raised. Disabled controls still read as dimmer than tertiary text.
- **Selection & focus.** Selected rows get a pale gold fill (gold-160 / #33291A) plus a **3px rounded gold indicator pill** on the leading edge (Fluent NavigationView / TreeView pattern); selected tabs get a 2–3px gold underline/overline. Keyboard focus is always a visible 2px text-colour ring — never removed.
- **Hover / press.** Subtle overlays: hover = 4% black (6% white in dark), pressed = 8% (10%). Primary buttons darken one brand step on hover (80→70) and two on press (→40/50). No scale/shrink on press.
- **Motion.** Minimal, Fluent curves (decelerate-mid in, accelerate-mid out), 100–250ms for flyouts and panes; respect Windows "Animation effects" off — Slate's graph already has a motion policy that disables motion.
- **Transparency & blur.** Only modal scrims: `--scrim`, 30% black in both themes (WinUI smoke; WPF `SmokeFillColorDefault`). No blurred panels, Mica or acrylic.
- **Imagery.** None in chrome. User images render inside embeds as-is; the only brand image is the flat two-colour logo.

## Window chrome (WPF)

A single 48px row replaces the separate title bar and menu bar. It holds the logo button (which opens the app menu), the window title, the quick commands (command palette, then settings) and the caption buttons. Theme lives in Settings › Appearance and in the palette (**Switch Theme…**), not in the title bar. The pane toggles live in the 24px status bar: Files sidebar in the far-left corner, right pane in the far-right corner. Both are 24 × 24 flat buttons, pressed while their pane is visible, using Segoe Fluent Icons E90C and E90D, and are also reachable as **Toggle Files Sidebar** and **Toggle Right Pane** (Ctrl+Alt+I) in the menus and palette. 48px is the Windows "tall" title bar height, used when the title bar holds interactive controls. The window title reads note, vault, Slate (`Why Slate — demo-vault — Slate`), as in the UI kit.

- **App menu.** Click the logo or press **Alt+F**. A cascading popup lists the menus (File, Workspace, Base, Editor, Canvas, Graph, Help), and each menu's submenu flies out to the right. Alt+W, B, E, N, G and H open the popup at that menu, and the same letters switch menus while it's open. Arrows, Enter and Esc behave as in any Windows menu. Build it from the existing `MenuItem` tree in MainWindow.xaml, hosted in a `ContextMenu` or popup, so UIA menu semantics, `InputGestureText` and `AcceleratorKey` carry over.
- **Use `WindowChrome` rather than `WindowStyle="None"`.** WindowChrome keeps the native resize borders, window shadow, Snap, Win+arrow and the Alt+Space system menu. With `WindowStyle="None"` you rebuild resize, shadow and snap by hand. Adding `AllowsTransparency` also makes it a layered window, which renders more slowly.
  - Set `CaptionHeight` to the row's height (48, and more if text size ever grows the row), `ResizeBorderThickness` to Windows' whole frame, and `UseAeroCaptionButtons="False"`. `SystemParameters.WindowResizeBorderThickness` is only the frame's sizing border: add the padded border (`SM_CXPADDEDBORDER`), or the edge takes the pointer over a third of a native window's width.
  - Mark the logo button, the quick commands and the caption buttons `WindowChrome.IsHitTestVisibleInChrome="True"`. The rest of the row stays a drag region, and double-clicking it maximizes.
  - Caption buttons are 46 × 48, using Segoe Fluent Icons E921 (minimize), E922 (maximize), E923 (restore) and E8BB (close). They call `SystemCommands.MinimizeWindow`, `MaximizeWindow`, `RestoreWindow` and `CloseWindow`. Close hover is #C42B1C with a white glyph.
  - For Windows 11 Snap Layouts on the maximize button, hook `WM_NCHITTEST` and return `HTMAXBUTTON` over it. Add the hook after WindowChrome's own, because WPF asks the newest hook first and WindowChrome answers every hit test it's shown. Windows then owns the mouse over the button, so drive its hover and press from `WM_NCMOUSEMOVE`, `WM_NCMOUSELEAVE`, `WM_NCLBUTTONDOWN` and `WM_NCLBUTTONUP`, and run its command on the button, not on whatever has the keys.
  - Windows places the Snap Layouts flyout for a standard caption, about 26 below the window's top, so once it's open it covers the lower half of a 48-tall maximize button: a click there lands on the flyout. A click before it opens, or in the button's upper half, maximizes.
  - When maximized, pad the root element by the same whole frame, because WindowChrome windows overhang the screen edge by it.
- **Accessibility.** Give every chrome button an `AutomationProperties.Name`, and set `AcceleratorKey="Alt+F"` on the logo button. Keep the title bar in the F6 region cycle.

## Iconography

Use icons in this order:
1. **Segoe Fluent Icons** (`--font-icons`, installed with Windows 11). This is the icon font the WPF Fluent theme uses, so designs match the app glyph for glyph. In HTML, render a `<span style="font-family:var(--font-icons)">` holding the character for the code point. In WPF, use `FontFamily="Segoe Fluent Icons"` with the same code point. **Segoe MDL2 Assets** is the automatic fallback.
   Common code points: Search E721 · Settings E713 · Add E710 · Edit E70F · Document E8A5 · Folder E8B7 · Tag E8EC · Pin E718 · Copy E8C8 · History E81C · More E712 · Link E71B · Reading E736 · Code E943 · Filter E71C · Info E946 · Warning E7BA · ChevronRight E76C · ChevronDown E70D.
2. **`Icon`** (`assets/icons`): the Fluent System Icons extracted from the Figma kit as SVG (`name="SearchSize20ThemeRegular"`, using `currentColor`). Use it where an SVG is needed, for example inside exported artwork.
3. The 88 standalone icon components (Search, Settings…) now sit in `components/internal/`. Other components are built from them. Don't use them directly.

Use Regular at rest and Filled for selected or active states, at 16px in dense rows and 20px in toolbars. No emoji. Icon-only buttons always have an accessible name and a tooltip showing the chord.

## Fonts

- **Segoe UI Variable** — installed with Windows 11, not in this repo: Text (14–18px), Display (20px and up), Small (12px captions), as `tokens/typography.css` uses them, each Light 300 / Semilight 350 / Regular 400 / Semibold 600 / Bold 700; plus "Segoe UI". The fonts' licence text limits them to the Microsoft product they came with, so the tokens name the installed families, `tokens/fonts.css` reaches Segoe UI and the icon fonts with `local()`, and these pages render them only on Windows 11. Elsewhere the tokens' fallback stacks apply, and `--font-icons` has none.
- **Open Sans** — shipped (`fonts/`, variable weight 300–800, normal + italic), under the SIL Open Font License 1.1 (`fonts/OpenSans-OFL.txt`).
- **Cascadia Code / Mono** — shipped (`fonts/`, variable 200–700, normal + italic, plus NF and PL variants), under the SIL Open Font License 1.1 with Reserved Font Name Cascadia Code (`fonts/Cascadia-OFL.txt`). Editor uses Cascadia Code (ligatures); Cascadia Mono is the no-ligature fallback, and the reading view's code.
- The Windows app embeds the normal and italic Cascadia Code, Cascadia Mono and Open Sans files, not the NF or PL ones, and copies both licences to `Licenses\` beside its build.
- The Fluent kit's tokens also name **Inter** (one docs label; not loaded, the token is re-pointed to Segoe UI) and **Segoe Sans**, a web-only Microsoft face Slate can't ship. Its `@font-face` points at the installed Segoe UI, so every token that names it renders in Segoe UI. The system loads no fonts from the network.

## Components

All components are on `window.SlateDesignSystem_326dc9`. Everything except the Slate patterns comes from the Fluent 2 Web kit. Props follow the Figma variant axes and accept the short aliases above. Each component's `.prompt.md` has a usage example.

- **Actions (5):** Button, CompoundButton, Link, SplitButton, Toolbar.
- **Forms (20):** Checkbox, Dropdown, Field, InfoButton, InfoLabel, Input, Label, Radio, RadioGroup, Rating, SearchBox, Slider, SpinButton, Swatch, SwatchPicker, Switch, TagPickerDropdown, TagPickerItem, TagPickerTagPicker, Textarea.
- **Navigation (22):** AppItemMedium, AppItemSmall, Breadcrumb, BreadcrumbItem, HorizontalTab, HorizontalTabList, ListContent, ListItem, MenuItem, MenuMenu, MenuSection, MenuSplitItem, Nav, NavBodyMedium, NavBodySmall, NavNodeMedium, NavNodeSmall, NavSubItem, SectionHeader, TreeItem, VerticalTab, VerticalTabList.
- **Feedback (9):** AnimatedProgressBar, Badge, MessageBar, PresenceBadge, Skeleton, Spinner, StaticProgressBar, StatusIndicator, Toast.
- **Overlays (10):** Dialog, Drawer, DrawerFooter, DrawerHeader, InlineDrawer, MaterialAcrylic, OverlayDrawer, Popover, TeachingPopover, Tooltip. Dialog is 448 px wide (320 to 520), the WinUI width.
- **Content (26):** Accordion, Avatar, AvatarAvatarGroup, AvatarAvatarPie, Card, CardBody, CardFooter, CardHeader, CardMedia, Carousel, CarouselNav, CarouselPlayControl, CarouselStep, CarouselSteps, CellActionHeader, ContentHeader, DataGridCellLarge, DataGridCellMedium, DataGridCellSmall, DataGridCellSmaller, Divider, ImagePreviewNav, ImagesPreviewStep, InteractionTag, Persona, Tag.
- **Shell (2):** CaptionButton, TabView.
- **Scrolling (1):** ScrollBar.
- **Date and time (1):** DatePicker.

**Built from the Windows UI kit (WinUI)** because they look different from the Web versions or were missing. The names stay the same, so existing designs pick up the change:
- `Checkbox` and `Radio`: 20 px controls in a 32 px row (Web: 16 px). They are live controls now (`checked`, `onChange`); `status` and `state` still force a look.
- `MenuItem` and `MenuMenu`: 40 px pitch, 34 px highlight inset 5 px, radius 3, label 16 px from the edge.
- `NavNodeMedium`: the same row geometry, 16 px icon, gold pill when selected. The Settings categories use it.
- `Button` with `appearance="subtle" layout="icon only"` at medium size: a 40 × 40 target with a 30 × 30 fill and a 16 px glyph, for the title bar. Panel toolbars and the note header use 32 × 32. Other Button variants are unchanged.
- New: `CaptionButton`, `TabView`, `ScrollBar`, `DatePicker`. `NavNodeSmall` and the other navigation pieces stay Web.
- **Internal (146), not for direct use:** Alert, AnatomyBadge, ArrowCircleDownRight, ArrowClockwise, ArrowClockwiseDashes, ArrowDown, ArrowDownload, ArrowLeft, ArrowProgress, ArrowSwap, ArrowSwap2, ArrowSync, ArrowUp, ArrowUpload, AvatarAvatar, AvatarAvatar2, AvatarAvatar3, Beaker, Bell, BinderTriangle, BlocksTextHeadline, BlocksTextVariation, Bot, BranchFork, Button3, ButtonSlot, Calendar, Calendar2, Calendar3, Call, Caret, Chat, Checkmark, Checkmark2, Checkmark3, CheckmarkCircle, CheckmarkProgress, Chevron, Chevron2, Chevron3, Chevron5, Circle, Circle2, Circle3, CircleHalfFill, CircleHint, ClipboardArrowRight, ClipboardTask, Clock, Code, ContentLeftMedium, ContentLeftSmall, ContentPlaceholder, ContentPlaceholder2, ContentPlaceholder3, Copilot, CustomMenuExample, DEPRECATEDAvatar, DEPRECATEDCarouselStep, DEPRECATEDCarouselStepOnBrand, DEPRECATEDCarouselSteps, DEPRECATEDCarouselStepsOnBrand, DEPRECATEDDivider, Delete, DiamondDismiss, Dismiss, Dismiss2, Dismiss3, Divider4, ErrorCircle, Figma, Filter, Flag, FlexSpace, Fluent, FocusRing, Folder, Folder2, Glasses, Hamburger, Icon24Close, Icon24PlaceholderSmall, Icon24StarSmall, Image, ImageCircle, ImageCircle2, ImageCircle3, ImageSwapPlaceholder, Info, Info2, ItemSplitButton, Lightbulb, LinkTags, Mail, Microsoft, MoreHorizontal, Navigation, Open, PauseCircle, PeopleTeam, Person, Person2, Person3, PersonaTextRampDefault, Pin, Placeholder, Placeholder2, Placeholder3, Placeholder4, Placeholder5, Placeholder6, Placeholderimage, Play, PresenceBadge2, PresenceBadge3, Prohibited, QuestionCircle, RadioBase, ResizeHandler, Search, SecondaryAction, SecondaryActionInteractionTagBrand, SecondaryActionInteractionTagFilled, SecondaryActionInteractionTagOutline, Selection, SelectionSmall, Settings, SpinButtonStepper, SpinnerBase, Square, Star, Star2, StateTags, SubtractCircle, SwitchBase, TagPickerSecondaryAction, TeachingPopoverContent, TeachingPopoverContentOnBrand, TeachingPopoverFooter, TeachingPopoverFooterOnBrand, Text, Text2, TitleBodyCopy, TreeItemChevron, Warning, WrenchScrewdriver.

**Slate patterns (7), in `components/patterns/`:** these are not in the Fluent kit. Each one is composed from the Fluent components above, takes icons as Segoe Fluent Icons code points, and handles its own hover, keyboard focus and keyboard navigation.
- `PaneSwitcher`: icon strip that switches a pane between views, with tooltips, a gold selected indicator and a **More panels** overflow menu.
- `PanelToolbar`: the 40 px row at the top of a panel, with title, count, search, sort menu and collapse/expand all.
- `NoteHeader`: back/forward, folder breadcrumb, Source/Reading switch (icon-only below 520 px) and the ⋯ menu. The app leaves back/forward out until it has a note history.
- `EmptyStateLauncher`: empty tab with logo, title and primary actions with their shortcuts.
- `CountListRow`: icon, label and count, for tags, vault properties and backlinks. 32 px or 24 px, like TreeItem.
- `SettingsLayout` and `SettingRow`: the Settings window body (category tabs and search) and one setting (label, description, control).

**Menu rules** (card: Menu · States). A menu has one leading column. Once any row can be checked, every row keeps the 16 px check column. In a menu with icons, the check replaces the icon. The shortcut and submenu chevron share the end column and dim with their row. Keyboard focus is the 2 px ring on the inset highlight, as WinUI does (decided 3 November 2026), and the arrow keys skip disabled rows. `MenuItem` now keeps the check column on unchecked rows. Its card and component still draw the ring across the full row, so judge the ring in the app.

**Intentional additions:**
- `Icon` (assets/icons) wraps the extracted icon data.
- `components/internal/slateProps.jsx` holds the prop-alias helper. `components/internal/slateUi.jsx` holds the glyph, icon-button, tooltip, search-field and popup-menu helpers that the patterns share.
- The seven Slate patterns in `components/patterns/`, requested to match the parity research (`scraps/parity-notes.md`). The Fluent kit has no counterparts:
  - `PaneSwitcher`: intentional addition (right-pane view switcher).
  - `PanelToolbar`: intentional addition (panel title row).
  - `NoteHeader`: intentional addition (note navigation bar).
  - `EmptyStateLauncher`: intentional addition (no-tab state).
  - `CountListRow`: intentional addition (tag, property and backlink rows).
  - `SettingsLayout`: intentional addition (Settings window body).
  - `SettingRow`: intentional addition (one setting).
- Text props added where the kit hard-coded copy: `SearchBox`, `Input`, `Textarea` and `Dropdown` `placeholder`; `Field` `labelText`; `BreadcrumbItem` `label`; `Switch` `labelText`; `Dialog` `primaryLabel` / `secondaryLabel` / `tertiaryLabel`; `Drawer` `title` / `primaryLabel` / `secondaryLabel`; `DrawerFooter` and `CardFooter` `primaryLabel` / `secondaryLabel`; `MessageBar` `actionLabel` / `secondaryActionLabel`; `TeachingPopoverContent` `media` / `mediaSwap`.
- `TreeItem` draws the 3 px gold pill in its selected state (the kit shows only the fill), so Fluent tree rows match Slate-built rows. `Spinner` is a rotating arc (the kit export flattened it to a disc) and stops under `prefers-reduced-motion`.

**Kit coverage:** the checker counts 295 family entries and 239 are built. The other 56 are duplicate set names in the Figma file (for example 5× `Avatar/Avatar`, 4× `.RadioBase`, 3× `Button`, 4× `Chevron`), and each name is built once. The deprecated sets are built and sit in `internal/`. 45 boolean variables (Arrow position, Toast status/type, Card media layout…) have no CSS form.

**WinUI kit coverage:** the checker counts the WinUI kit's 3,205 family entries against 250 built. From the WinUI kit, this system builds Checkbox, Radio, MenuItem, MenuMenu, NavNodeMedium and the subtle icon-only Button (rebuilt under their existing names), plus CaptionButton, TabView, ScrollBar and DatePicker. The rest is skipped on purpose, as decided in `winui-review.md`:
- Windows shell surfaces: quick-settings toggles, cursors, touch gestures, UAC and shell dialogs, Sticky Keys, input keys.
- 104 Windows app icons.
- Documentation frames and repeated sets.
- Controls the Web components already cover closely enough (Button, text box, combo box, toggle switch, InfoBar, Expander and others).

## UI kits

- `ui_kits/slate-windows/` — Windows workspace: one-row title bar (logo app menu on Alt+F with File, Workspace, Base, Editor, Canvas, Graph, Help and their submenus; palette and settings buttons; caption buttons), status bar with the Files sidebar toggle far left and the right-pane toggle far right, Files sidebar (PanelToolbar with sort and group plus collapse/expand all, filter, tag list of CountListRows), tab strip, NoteHeader (back/forward, breadcrumb, Source/Reading, ⋯ menu), reader and editor views of a demo note, right pane (PaneSwitcher over Slate's 17 views, with Backlinks, Outgoing links, Outline and Citations recreated), EmptyStateLauncher when no tab is open, Settings window (SettingsLayout over Slate's real preferences, Appearance first with Theme and Accent color), command palette (Ctrl+Shift+P) with Switch Theme….
