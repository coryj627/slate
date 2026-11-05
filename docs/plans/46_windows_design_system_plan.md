# 46 — Windows design system: the features not yet built

Status: adopted 2026-11-03, with the owner's decisions in §3. The owner started
Wave 1 on 2026-11-04, and each of its chunks runs `codex:adversarial-review` until
Codex approves before its PR opens. Later waves wait for the owner. Per-chunk specs are in
`46_windows_design_system/specs/`, one per PR. Red-teamed once by a fresh Claude
agent and reviewed four times by Codex, with every finding checked against the tree
and applied. Baseline: `main` at 9f4194c0 (#1787). #1792 landed just after it and
moved `MainWindow.xaml` and the right pane's widths, so a spec's line numbers are as
of the baseline (§9).

The owner asked for "the plan and spec for the features not yet built" while
the design cycle's last PR sat in CI. What is wanted comes from `design/windows`:
`readme.md`, `wpf-feasibility.md`, `winui-review.md` ("Decisions"),
`xaml/README.md`, `tokens/` and the components' `.prompt.md` files. Where those
agree, this plan does not reopen them. Where they disagree, or a choice has a
real cost, §3 records the owner's call.

## 1. Scope

**In:** every design feature the app does not have, with a surface that uses it.

| Area | What the design says | What the app has | Chunk |
|---|---|---|---|
| Fonts | Cascadia Code for the editor at 14/22, Open Sans for reading, both shipped | Consolas in the editor; no reading font at all (the key is undefined) | DS-1 |
| Type ramp | The WinUI ramp in Segoe UI Variable; body text 14/20 | Unset text is 12 in plain Segoe UI; 59 literal sizes | DS-2 |
| Reading view | 16/26, headings 32/24/20/18, a 72ch measure | 15 with no line height, headings 26/21/18/16, full width | DS-3 |
| Menus | One template, aligned columns, 40 pitch, bare check | Fluent's four templates plus a shortcut patch | DS-4 |
| Text size | Follow Windows Text size, heights grow | Canvas, Graph and unset text follow it; sized text doesn't, and nothing is live | DS-5 |
| Elevation | One straight-down shadow beside the content, 30% scrim, radius 8 | 11 shadows falling sideways, on the content; no scrim; square sheets | DS-6 |
| Shell heights | Status bar 24, pane toggles in its corners | About 48, and no way to hide Files | DS-7 |
| Tab strip | TabView: 36 strip, 32 tabs, close glyph, flared corners | A plain underlined tab with a "×" text button | DS-8 |
| Small controls | Subtle icon button, SearchBox | Text glyphs on buttons, plain fields | DS-9 |
| Settings | A Settings surface; Appearance › Theme; Switch Theme… | None; the theme always follows Windows | DS-10 |
| Accent | Automatic, Slate gold, Windows accent, with a contrast gate | Gold only | DS-11 |
| Title bar | One 48 row: title, palette, settings, caption buttons, Snap Layouts | The Windows title bar plus a separate menu bar, about 74 together | DS-12 |
| App menu | The menus cascade from the logo; Alt+letter opens them | A menu bar | DS-13 |
| Narrow windows | 500 wide or less, panes collapse | `MinWidth` 780 | DS-14 |
| Note header | Back, forward, breadcrumb, Source/Reading, ⋯ | None | DS-15, without Back and Forward (DSD15) |
| Pane switcher | Icon strip with a gold pill and a More menu | A 17-row text list | DS-16 |
| Files toolbar | PanelToolbar with Sort and group, New note, collapse/expand | A stack of fields, toggles and expanders | DS-17 |
| Leaf toolbars, count rows | PanelToolbar on each leaf, CountListRow | A heading and plain rows | DS-18 |
| Empty pane | EmptyStateLauncher with actions and chords | One line of text | DS-19 |

**Out, and why:**

- **High Contrast.** The owner's instruction of 2026-11-03: "Creating a high
  contrast color palette is a separate exercise." A chunk adds a new key to
  `Slate.Contrast.xaml` only as `xaml/README.md:17` requires, so
  `ThemeManager.ValidateResourceDictionaries` and missing-key fallbacks hold.
  Nobody evaluates how it looks, and a PR body does not list it as undelivered.
- **The rest of #751.** Its General, Sidebar, Math, Code, Bibliography, Canvas
  and History tabs stay with W8-1 (`18_windows_port/specs/w8_spec.md`). DS-10
  builds the surface and Appearance, so those tabs have somewhere to go.
- **Switch and SpinButton.** Their first consumers are #751's on/off and number
  rows. Appearance uses dropdowns only (`SettingsLayout.prompt.md`), and a
  template with no consumer can't be seen working. They go with W8-1.
- **The Help menu.** `readme.md:120` lists it, but the help surface is #756
  (W8-6). DS-13 leaves Alt+H unbound until it exists.
- **Components no Slate screen uses:** Spinner, Skeleton, Toast, Popover,
  TeachingPopover, the Drawer family, TagPicker, Carousel, ImagePreview, Rating,
  Swatch, CompoundButton, SplitButton. Build one when a feature needs it.
- **The kit's status-bar fields.** `ui_kits/slate-windows/Chrome.jsx:139` draws
  mode, word count, caret position and file kind. No document specifies them and
  no core API provides them, so they aren't planned here.
- **ClearType in ComboBox and DatePicker popups.** Their shadow sits on the
  text's parent in Fluent's template, so fixing it means copying a template of
  several hundred lines (`ComboBox.xaml`, `DatePicker.xaml` in `dotnet/wpf`).
  Those two popups stay greyscale.
- **Reading-view cards.** `readme.md:104` and `ui_kits/slate-windows/Document.jsx:42,60,68`
  show embeds and code blocks as cards with a header strip and radius 8. A flow
  `Paragraph` or `Section` has no corner radius, and a `BlockUIContainer` would take
  the text out of the reading flow that W7 and XB-9 tuned for line reading. The
  shape isn't worth that. A header strip without the radius is possible, if the
  owner wants it as its own chunk.
- **Sort, expand-all and search on right-pane leaves.** `PanelToolbar.prompt.md`
  describes them, but no leaf has those verbs (searched). DS-18's toolbars show a
  title and a count until a feature adds them.
- **Backlink rows that count mentions per note or expand into tree nodes**
  (`wpf-feasibility.md:148`, `ui_kits/slate-windows/Panes.jsx:39`). Today's rows are
  occurrences with a snippet, and a page holds at most 200, so a per-note count needs
  a grouped query in core, the FFI and both hosts. Expanding also changes the leaf's
  role from a list to a tree, so it needs its own design and pass.
- **The Files pane's Shortcuts and Recents sections.** The kit's arrangement
  (`ui_kits/slate-windows/Sidebar.jsx:1`) differs from today's expanders, which hold
  real actions. DS-17 keeps those.
- **The non-design findings** #1765, #1766, #1768, #1775, #1782, #1783 and #1788
  stay filed, by the owner's "design only".
- **The Mac.** Nothing here is Mac work, apart from the Swift corpus mirror DS-7's
  two new announcements need, and compiling against DS-12's core function.

## 2. What the reading found

### 2.1 Already built, not to be rebuilt

The theme files, unchanged from the design's copies (compared byte for byte,
`design/windows/xaml/Slate.{Light,Dark}.xaml` against `Themes/`). The system
colours layer. `Slate.PanelRowTemplate`, `Slate.ComboRowTemplate` and
`ClassicRows`. `Slate.RowFocusVisual`, drawn inside a row. `TextBoxSelection`.
`Slate.ReadingSelectionBrush`. `FieldPrompt`. The Body Large styles. Fluent's
own scroll bar, date picker and expander, which the design rates Native.

### 2.2 Measured for this plan

Run on this desktop on 2026-11-03: Windows 11 build 26300, .NET 10.0.14,
AvalonEdit 6.3.1.120, throwaway WPF apps outside the repo.

| Question | Result |
|---|---|
| Does WPF get real weights from the embedded variable TTFs? | Yes. Through a pack URI, `CascadiaCode.ttf` gives six named weights and `OpenSans-Variable.ttf` five (plus condensed), all `StyleSimulations=None`. Open Sans's measured width grows 417, 426, 435 across 400, 600, 700. `wpf-feasibility.md:60`'s "can't drive a variable font's axes" doesn't stop named instances |
| Real italics? | Yes, through a folder lookup. `./#Cascadia Code` and `./#Open Sans` resolve Italic to their italic files with `StyleSimulations=None`. A single-file reference gives a simulated oblique |
| Which versions does the repo hold? | Both Cascadia files read `Version 2407.024`, the newest upstream release (v2407.24, 2024-11-27). Open Sans reads `Version 3.003` |
| The licence? | SIL OFL 1.1 with Reserved Font Name "Cascadia Code" (`microsoft/cascadia-code`, `LICENSE`). Unmodified files may ship; the licence must go with them. The repo has `OpenSans-OFL.txt` and no Cascadia licence |
| Can AvalonEdit reach 14/22? | Not by its own API: `VisualLineTextParagraphProperties` is internal and sealed, and `TextView.DefaultLineHeight` has no setter. A composite font file can: Cascadia Code's natural 16.27 becomes exactly 22 at size 14, and 12.57, 31.43, 62.86 at 8, 20, 40. Every wrapped line measures the same |
| What does the composite family keep? | Ligatures (seen), width (131.25 in both), real bold (ink 3632 against 2938), real italic, and fallback for Japanese, Arabic, Hebrew, Greek, Cyrillic and emoji with an even line height |
| How big is unset text under Fluent? | 12, in plain Segoe UI: `SystemFonts.MessageFontSize`. Fluent's control styles set no size of their own and none uses `ControlContentThemeFontSize`, so most controls inherit from the window. `StatusBar` doesn't: its value source measured `DefaultStyle`, the classic theme's, which beats inheritance. From reading WPF's source and not measured, `MainMenu`'s local style leaves the classic `Menu` style in force the same way. Button 28.96 tall, top-level menu item 45.96, status bar 49.96 |
| Does a top-level menu item fit a 48 row? | At 12, yes (45.96). At 14 the template gives 30 + 18.62 = 48.62, the 49 in `wpf-feasibility.md:101`, so it doesn't |
| What does `UISettings` cost? | The project moves to `net10.0-windows10.0.19041.0` and ships `Microsoft.Windows.SDK.NET.dll` (24,877,600 bytes) and `WinRT.Runtime.dll` (528,944 bytes). It read 1 with the registry at 100. Its change event was not exercised: that needs a system setting changed |
| How tall are the shell's rows now? | From a 200% capture of the launched app: title bar about 29 and menu bar about 45 (design: one 48), status bar about 48 (24), tab strip about 30 (36) |
| How tall do menus get at a 40 pitch? | Workspace 1015 (25 rows), Base 821, Canvas 664, Editor 575, Graph 415, File 301 |
| How does a `ToggleButton` reach state? | A click flips `IsChecked` and runs `Command`. UIA's `IToggleProvider.Toggle` flips `IsChecked` and doesn't run `Command`, so `Command` with a one-way binding leaves the button and the state apart. A two-way binding, `IsChecked`'s default, writes back on both. DS-7 is built on this |
| What does the default blue read through `SystemColors`? | `AccentColor` `#0078D4`; Light1, 2 and 3 `#0091F8`, `#4CC2FF`, `#99EBFF`; Dark1, 2 and 3 `#0067C0`, `#003E92`, `#001A68`. `xaml/README.md:56`'s `#005FB8` and `#60CDFF` are WinUI's brush constants |
| Does the README's accent table clear the project's floor on that blue? | No. A selected row in light draws white on `#E0EFFA` (Lc 9.8, 1.17:1), because the templates use `SelectionText` there and the README's gate doesn't check that pair. In dark, black measures Lc 65.4 on the rest fill and 44.7 on the pressed fill, though both pass the README's WCAG list. The owner took the README's bar (DSD14), so DS-11 keeps the table and gives selected rows their own text brush, and everything then passes it on that blue |

### 2.3 Where the design's documents disagree

DS-0 settles these in the documents, so no later chunk has to.

| Topic | One says | Another says | Taken here |
|---|---|---|---|
| Modal scrim | `Dialog.prompt.md:29`: 20% | `xaml/README.md:145`, `winui-review.md:74`: 30% | 30%, the recorded decision |
| PaneSwitcher's control | `PaneSwitcher.prompt.md`: `TabControl` | `wpf-feasibility.md:144`: `ListBox` | `ListBox`, plus a button for More |
| PaneSwitcher's overflow | `PaneSwitcher.jsx:6`: a fixed `maxVisible`, six by default | `wpf-feasibility.md:144`: computed on `SizeChanged` | Fixed at six slots (DSD8) |
| Subtle icon button | `winui-review.md:77`: 40 × 40 with a 30 × 30 fill | The pattern prompts: 32 px | 32 in toolbars, 40 in the title bar (DSD6) |
| Segoe UI Variable Small | `wpf-feasibility.md:60`: under 12. `readme.md:141`: at 11 and under | `tokens/typography.css:24`, `xaml/README.md:122`: at 12 | The tokens |
| A menu's columns | `wpf-feasibility.md:103`: check, icon, label, shortcut, chevron | `readme.md:176` and `menu-states.card.html`: one leading column, and shortcut and chevron share the end column | `readme.md:176` |
| A menu row's focus ring | `readme.md:176`, `review-2026-11.md:54`: full row width | WinUI rings the inset highlight | The inset highlight (DSD10) |
| A tab's corner radius | `readme.md:101`, `winui-review.md:31`: 6 | `TabView.prompt.md`: 8 | 8, the component's own spec |
| How far the focus ring sits out | `wpf-feasibility.md:89`: Fluent draws 3 out, the design says 1, so set `Margin="-1"` | `tokens/base.css:7`: a 2px outline offset 1. A `Rectangle` strokes inside its bounds, so Fluent's `-3` already has its inner edge 1 out | Fluent is right, and DS-0 corrects `wpf-feasibility.md:89`. `-1` would straddle the control's border |
| Text size source | `xaml/README.md:164`: `UISettings` | `CanvasTextScaleService.cs:23-24`: the registry | The registry (DSD3), confirmed by the owner's check on 2026-11-04 |
| A note's Back and Forward | `NoteHeader.prompt.md:27`: `BrowseBack`, so a note history | `Commands/ChordTable.cs:1509`: the only history is the sidebar's | Left out for now (DSD15) |
| An accent's contrast bar | `xaml/README.md:35-42`: WCAG, 4.5:1 for text | `ThemeTokenContrastTests.cs:18`: \|Lc\| > 75 for every text pair | The README's, for an accent. Gold stays at the floor (DSD14) |
| Window title | Static "Slate" (`MainWindow.xaml:11`) | `ui_kits/slate-windows/index.html:61`: note, vault, Slate | The kit's (DSD9) |

### 2.4 Where this plan meets other work

- **Plan 44's WB-29 and WB-30** are still open and don't touch shell chrome. Their
  files can overlap `MainWindow.xaml`, so a chunk rebases as usual.
- **W8-1 (#751)** consumes DS-10. **W8-2 (#752)** keeps the High Contrast
  acceptance; DS-2 and DS-5 deliver its "system font-size respect" line, and the
  issue should say so when they merge.
- **The design cycle** (`claude/design-pass`) has its own discovery passes. A
  defect they find is fixed there, not here.

## 3. The owner's decisions (DSD1 to DSD15)

Settled with the owner on 2026-11-03. DSD8, DSD10 and DSD14 differ from what this
plan first recommended, and their specs follow the owner's call.

| # | Chunk | Question | Decided |
|---|---|---|---|
| DSD1 | all | Everything here, or only what #1756 waits on? | Wave 1 first (DS-0 to DS-5), when the owner says: on 2026-11-03 they asked that it not start yet. It closes #1756's checks and makes every later template scale for free. Later waves one at a time, each when the owner says, because they change how the app works rather than how it looks |
| DSD2 | DS-1 | Ship the fonts by linking `design/windows/fonts` or by copying them into the app? | Link. One copy in history instead of 3.6 MB twice. Costs one line in `windows.yml`'s path list |
| DSD3 | DS-5 | Read Windows Text size from the registry or from `UISettings`? | The registry, with `SystemEvents.UserPreferenceChanged`, as `CanvasTextScaleService` already does. `UISettings` is a WinRT API with no projection under plain `net10.0-windows`, and the standard route, a versioned target framework, ships the projection of the whole Windows SDK (`Microsoft.Windows.SDK.NET.dll`, 24.9 MB, and `WinRT.Runtime.dll`) for one number. The owner's two-minute live check (§8) confirms it. If that shows the registry path misses a change, `UISettings` it is. The check passed on 2026-11-04 |
| DSD4 | DS-2 | Unset text goes from 12 to 14. Accept the whole shell growing? | Yes. It is the design's body size, and the reason to do it first is that everything after it is judged at the real size |
| DSD5 | DS-4 | Menu rows at the recorded 40 pitch leave Workspace 1015 tall, past a 1080p screen's room once the taskbar and title bar are counted, so it scrolls | Keep 40, as decided on 2 November, and look at it in the launched app. If scrolling a menu feels wrong, 36 keeps the inset highlight and brings Workspace to about 915 |
| DSD6 | DS-9 | Subtle icon buttons: 40 × 40 (`winui-review.md`) or 32 (the pattern prompts)? | 32 in panel toolbars and the note header, where the prompts are specific and the rows are 40. 40 × 40 with the 30 fill in the title bar |
| DSD7 | DS-13 | The design hosts the menus in "a `ContextMenu` or popup" with hand-built keys (`readme.md:120`, `wpf-feasibility.md:115`). Keep a real `Menu` instead, with the logo as its one top-level item? | Keep the `Menu`. Alt, F10, the UIA menu bar, the F6 landing, `MenuBarCensus` and the NVDA-tested behaviour of W7-5 and W7-7 all come from that control. Only Alt+letter needs code. Step 0 of DS-13 is an NVDA and Narrator check the owner reads before the code goes further |
| DSD8 | DS-16 | The design hides panels past `maxVisible` behind a More menu. Keep all 17 in one list that scrolls? | The design's More menu, at its default six slots: Outline, Backlinks, Outgoing links, Connections and Graph inspector in the strip, and the other 12 in More panels. A reader hears "2 of 5", the same at any window size, and a panel in the menu takes more keys to reach than today, so DS-16 gets a full NVDA and Narrator pass with the owner's read. The icons in `Kit.jsx:54-56` also need the owner's review, as `assessment.md:113` says |
| DSD9 | DS-12, DS-17 | The kit's title is "note — vault — Slate", and it has no band for the vault's name and path | Take both. The title comes from core (it's read aloud), and DS-17 moves the vault's name, still heading level 1, into the Files toolbar, with the path in its tooltip |
| DSD10 | DS-4 | A menu row's keyboard ring: the full row, as `readme.md:176` and `review-2026-11.md:54` record, or the inset highlight, as WinUI does? | The inset highlight, as WinUI does. It's a different shape from the full-row ring rows have elsewhere, still 2px. DS-0 corrects the two records |
| DSD11 | DS-15 | The note header adds Tab stops before a note's Properties and text: one per folder, the mode switch and ⋯. Keep them all? | Keep them. A folder segment has no other route, and F6 and Ctrl+Tab already skip the bar. The AT pass can change this |
| DSD12 | DS-17 | Sort moves from a combo box to a menu. A combo says its value on focus ("File sort order, Name A to Z"); a menu button says only its name until it's opened | Take the menu and name the button with its value through core ("Sort and group, Name A to Z"), so a reader doesn't lose the value. If that reads badly in the pass, the combo stays beside the toolbar |
| DSD13 | DS-12 | With the native caption gone, UIA may still expose native minimize, maximize and close alongside Slate's. Two Close buttons is a defect, and WPF can't hide a peer from the raw view | Measure first. If they double, keep Slate's visible ones as `AutomationPresentation`-style peers out of the control view and leave the native ones to carry the pattern. If native ones are gone, keep Slate's. The PR shows the tree |
| DSD14 | DS-11 | The README gates an accent by WCAG and takes its dark fills from Fluent. The project's floor for text is \|Lc\| > 75, and black on Fluent's dark fill measures Lc 65.4 on the default blue. What must a Windows accent pass? | The README's gate: text at 4.5:1, fills and marks at 3:1. Gold stays at the floor. A chosen accent may sit below it, so on the default blue in dark, black on the primary fill is 10.47:1 and Lc 65.4. In exchange the dark fills stay Fluent's, and fewer hues fall back to gold. Selected rows get their own text brush regardless, because white on the tint fails any bar |
| DSD15 | DS-15 | The design's Back and Forward assume a note history. The only history here is "Back in Sidebar History", on both platforms, which Quick Open and links don't feed. Bound to it, Back sits disabled after Quick Open or walks sidebar selections | Leave both buttons out of DS-15, by the same rule as DS-18's toolbars: a button that misleads is worse than none. A per-tab note history is new behaviour the Mac doesn't have, with its own chords, and DS-15 lists what it takes. It can be its own chunk later |

## 4. Rules (every chunk)

Plan 44 §4 applies: one PR closes its issues and nothing else; the failing test
first; the Windows test recipe; `dotnet format --include` after every C# edit;
the parity matrix regenerated when a chord or help line changes; `AGENTS.md`'s
merge steps and cooldown. Additions:

1. **The theme files are generated.** `Slate.Light.xaml` and `Slate.Dark.xaml`
   stay byte-equal to `design/windows/xaml/`. New keys that aren't tokens go in
   `Slate.Text.xaml` or a sibling. A token that must change changes in
   `design/windows` first and is regenerated (`xaml/README.md:168-170`).
2. **A local style loses Fluent.** Base a new style on what it replaces, or give
   it a whole template, and check it in the launched app. Hosted tests alone have
   missed this class repeatedly (#1767, #1781).
3. **No spoken text is composed in the host** (`AGENTS.md`, "Architecture rules").
   A new composed label or announcement is a `crates/slate-core` change, reached
   over `crates/slate-uniffi`, with both hosts compiling in the same PR.
4. **Names, roles and tab order are contracts.** A chunk that changes one says so,
   updates the census that pins it (`MenuBarCensus`, `FocusIntegrityManifestCensus`,
   `CanvasAutomationPropertyCensus`) and carries the AT pass in §5.
5. **Every chunk ends with a discovery pass** on its surfaces in the launched app
   (a temporary capture fact, never committed: a census fails on it), in the dark
   theme, plus a hosted render in light.
6. **`wpf-feasibility.md` follows the code.** A rating or spike result that a chunk
   changes is updated in that PR, in the design's plain voice.
7. **`/design-sync` is the owner's.** Only an owner-started sync updates the Claude
   Design project; no chunk runs it.
8. **This desktop hosts the home CI runners.** No broad local run while a job is on
   a `slate-win-*` runner. Narrow filters are fine.
9. **A style in `MainWindow.xaml` or `WorkspaceTemplates.xaml` can't be `BasedOn` a
   Fluent style by `StaticResource`.** `ShownShell` merges the theme after `new
   MainWindow` (`ShownShell.cs:48`, `:63`), so the lookup throws under test. Build on
   Fluent's in code, as `MenuShortcuts` does, or set local values.
10. **A glyph is never a plain `TextBlock`.** Outside a control's own template, a
    `TextBlock` holding a private-use character is a Text peer named by it, which axe
    rejects (`NameExcludesPrivateUnicodeCharacters`). Use
    `AutomationPresentationTextBlock` (`AutomationLandmark.cs:168`). WPF has no
    `AutomationProperties.AccessibilityView`; that's WinUI.
11. **A subclass gets no implicit style** (#1767). `CheckMenuItem` and the rest take
    theirs in the constructor, as `AutomationLandmark.cs:139` does.
12. **`ThemeManager` can't be built under test.** It needs an `Application`, and
    `ShownShell.cs:31` forbids one. Keep each decision in a pure function a fact can
    call, with `ThemeManager` as the thin part that applies it.

## 5. The chunks

Size: S under a day, M a few days, L a week or more. Lanes: `windows` (windows.yml),
`rust` (rust.yml), `swift` (swift-tests.yml, compile only). AT: the NVDA or Narrator
work the chunk carries.

| Chunk | What | Closes | Size | Lanes | AT | Decision |
|---|---|---|---|---|---|---|
| DS-0 | The documents agree (§2.3), the measured results are recorded, one issue per chunk is filed, and #1756 points at them | — | S | none | — | — |
| DS-1 | Cascadia Code, Cascadia Mono and Open Sans ship with the app and their licences; the editor is 14/22 through a composite font; the reading view has its font; spike 2 reruns on Slate's own highlighter | #1756 item | M | windows | NVDA spot | DSD2 |
| DS-2 | The WinUI ramp as dynamic resources; the shell's default text is 14/20 Segoe UI Variable; every literal size becomes a ramp step; a census keeps it so | — | M–L | windows | — | DSD4 |
| DS-3 | The reading view's type: 16/26, the heading scale, a 72ch measure | — | S–M | windows | NVDA spot | — |
| DS-4 | One menu template for all four roles, context menus included; a measured fit in 48 | #1756 item | M | windows | NVDA and Narrator spot | DSD5, DSD10 |
| DS-5 | Windows Text size across the whole shell, one owner, live; nothing clips at 225% | #1756 item | L | windows | NVDA spot | DSD3 |
| DS-6 | Sheets, palette and tooltips: tokens for shadow, sibling shadow for ClearType, 30% scrim, radius 8, dialog widths | — | M | windows | Narrator spot | — |
| DS-7 | The status bar at 24; pane toggles; a Toggle Files Sidebar command with core's announcements and their Mac mirror; a hidden pane gives its room back | — | M | windows, rust, swift | NVDA spot | — |
| DS-8 | The tab strip as the design's TabView | — | M | windows | NVDA and Narrator spot | — |
| DS-9 | Subtle icon button and SearchBox styles; every text glyph on a button becomes an icon with a name | — | S–M | windows | NVDA spot | DSD6 |
| DS-10 | A Settings sheet (SettingsLayout, SettingRow), Appearance › Theme, the theme preference, Settings… and Switch Theme… | Refs #751 | L | windows | NVDA and Narrator pass | — |
| DS-11 | Appearance › Accent color, with the rules and contrast gate in `xaml/README.md`, and selected rows' own text brush | — | M | windows | — | DSD14 |
| DS-12 | `WindowChrome`: one row with title, palette, settings and caption buttons; Snap Layouts | — | L | windows, rust, swift | NVDA and Narrator pass | DSD9, DSD13 |
| DS-13 | The menus cascade from the logo; Alt+letter; F10 | — | L | windows | Full NVDA and Narrator menu pass | DSD7 |
| DS-14 | `MinWidth` 500; panes collapse by width without losing the reader's place | — | M | windows | NVDA spot | — |
| DS-15 | NoteHeader above a note, each control acting on its own note | — | L | windows | NVDA and Narrator pass | DSD11, DSD15 |
| DS-16 | The right-pane rail as an icon strip with a More menu; panes at the design's default widths | — | L | windows | NVDA and Narrator pass | DSD8 |
| DS-17 | The Files pane under a PanelToolbar | — | L | windows, rust, swift | NVDA and Narrator pass | DSD9, DSD12 |
| DS-18 | PanelToolbar on each right-pane leaf; CountListRow for tags | — | L | windows | NVDA spot | — |
| DS-19 | EmptyStateLauncher in an empty pane | — | S | windows | NVDA spot | — |

### Closure matrix

| Issue | Closed by |
|---|---|
| #1756 | Its items: Spike 2 by DS-1; the menu template by DS-4; Spike 5's source and live change by DS-5 plus the owner's check; the optional 200% runs by the captures in §7. The last of those, and the owner's `/design-sync`, close it |
| #751 | Stays open; DS-10 is `Refs` |
| #752 | Stays open for its High Contrast acceptance |
| Each chunk's own issue | Filed by DS-0, closed by that chunk's PR |

## 6. Delivery order

One PR at a time per `AGENTS.md`, the next starting while the previous sits in CI.

- **Wave 1, type and menus:** DS-0, DS-1, DS-2, DS-3, DS-4, DS-5. In this order
  because DS-2 gives DS-3, DS-4 and DS-5 the keys they use, and a census stops new
  literals from then on.
- **Wave 2, surfaces:** DS-9 first, because DS-7 uses its toggle style and DS-8 its
  close button. Then DS-6, DS-7 and DS-8, in any order.
- **Wave 3, settings:** DS-10, then DS-11.
- **Wave 4, chrome:** DS-12, DS-13, DS-14. DS-12 needs DS-4 (the menu fits the row)
  and DS-9 (its buttons), and its settings button needs DS-10.
- **Wave 5, patterns:** DS-15, DS-16, DS-17, DS-18, DS-19. DS-17 and DS-18 need
  DS-9.

This reorders `wpf-feasibility.md:152-160`, which puts text scaling last. Scaling
early means each later template is measured at 225% as it's built, instead of all
of them being reworked at the end.

## 7. Verification lanes and the local loop

Per chunk, in the chunk's worktree:

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-<chunk>'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests -c Release --no-build --filter "<the spec's filter>"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <each edited file>
```

Keep `ConnectionsLeafTests.TheModelOf` out of any filter that sweeps `Connections`:
it's CI's long model lane. A shell-gate fact runs locally only with NVDA closed and
the desktop free. The whole suites run on `windows.yml`, and the shell gate starts
only after the app job, so "not pending" isn't "passed" until it's listed.

**Captures.** This desktop's display moves between 100% and 200%, so a capture uses
`CapturePhysicalPixels` (`ShellAccessibilityTests.ReadingSelection.cs:112`). It's in
dark mode, so the launched app shows dark, and light is a hosted render. When the
display is at 200%, a chunk captures there too, which also covers #1756's optional
200% check for the features that spikes 1 to 4 and 6 stood in for. No throwaway app
needs rebuilding.

**AT.** Plan 44 §4 rule 4's recipe. An agent run doesn't fill a human cell in
`w_c_matrix.md`.

## 8. Owner steps and watch list

| What | When | Why it can't be done for you |
|---|---|---|
| `/design-sync`, with the map in `design/windows/github.md` | After DS-0 merges, and again whenever you like | The project has none of the repo's hand fixes until it runs, and only you start it |
| The live Text size check: run the app, set Settings › Accessibility › Text size to 150%, Apply, then back to 100% | When DS-5's PR is up | It changes a system setting, which an agent doesn't do. Two minutes. Expected: the shell's text changes within about a second, with no restart |
| Review of the right-pane icons (`Kit.jsx:54-56`) | Before DS-16 | `assessment.md:113`: "my picks and need your review" |
| A look at a few Windows accents under `Windows accent` | When DS-11's PR is up | It changes a system setting, which an agent doesn't do. It shows how many hues fall back to gold under DSD14's bar |
| The go-ahead for each later wave | Before its first chunk | DSD1: they change how the app works, one wave at a time |

## 9. How an implementing agent works a chunk

Plan 44 §10 applies: a fresh worktree on `claude/<chunk-slug>` from `main`; read the
spec's "Current state" lines before coding, because `MainWindow.xaml` moves; failing
facts first; commit messages `type(scope): one lower-case sentence saying what is
now true`; PR bodies list `Closes #N`, the facts, the field-pass phrases and
`not delivered: <reason>`. Each spec ends with a Brief that, with this section, is
all an implementing agent needs. File names without a path are under
`apps/slate-windows/src/SlateWindows/`, or under `design/windows/` for a design
document.

| Chunk | Spec | What becomes true |
|---|---|---|
| DS-0 | `specs/ds00_documents_and_issues.md` | The design's documents agree, and each chunk has an issue |
| DS-1 | `specs/ds01_fonts_ship_with_the_app.md` | The editor is Cascadia Code at 14/22, and reading is Open Sans, on any machine |
| DS-2 | `specs/ds02_type_ramp.md` | Every text size in the shell is a named step, and default text is 14/20 |
| DS-3 | `specs/ds03_reading_view_type.md` | The reading view has the design's scale and measure |
| DS-4 | `specs/ds04_menu_template.md` | Every menu row lines up, shortcuts are readable, and a top-level item fits 48 |
| DS-5 | `specs/ds05_text_scaling.md` | Windows Text size scales the whole shell, live, and nothing clips at 225% |
| DS-6 | `specs/ds06_elevation_and_scrim.md` | Overlays keep ClearType and have the design's shadow, scrim and corners |
| DS-7 | `specs/ds07_status_bar_and_pane_toggles.md` | The status bar is 24 high and either pane can be hidden, giving its room back |
| DS-8 | `specs/ds08_tab_strip.md` | The tab strip is the design's |
| DS-9 | `specs/ds09_icon_button_and_search_box.md` | Icon-only buttons draw an icon and have a name, and search fields look like search fields |
| DS-10 | `specs/ds10_settings_and_theme.md` | There is a Settings surface, and the theme can be chosen |
| DS-11 | `specs/ds11_accent_setting.md` | The accent can follow Windows, and never fails contrast |
| DS-12 | `specs/ds12_title_bar.md` | The title bar is one 48 row with Snap Layouts |
| DS-13 | `specs/ds13_app_menu.md` | The menus open from the logo, with every key they had |
| DS-14 | `specs/ds14_narrow_window.md` | The window can be 500 wide, and panes make room |
| DS-15 | `specs/ds15_note_header.md` | A note has a header with its place and its mode |
| DS-16 | `specs/ds16_pane_switcher.md` | The right-pane rail is an icon strip with a More menu |
| DS-17 | `specs/ds17_files_toolbar.md` | The Files pane sits under one toolbar |
| DS-18 | `specs/ds18_leaf_toolbars_and_count_rows.md` | Each leaf has the toolbar, and tags use count rows |
| DS-19 | `specs/ds19_empty_state_launcher.md` | An empty pane offers its next actions |

## 10. Risks

- **DS-2 moves everything.** 12 to 14 changes every row, and some tests measure
  widths (the graph filter's 219 to 221 band in #1784). It lands early for that
  reason, with a discovery pass of every surface. It also makes unset text explicit,
  which would stop it following Text size, so it reads the factor at startup until
  DS-5 makes it live.
- **DS-13 changes the most-tested route in the app.** The menus' behaviour under
  NVDA took W7-5 and W7-7 to get right. DS-13 opens with a spike, keeps the control
  (DSD7) and doesn't merge without a full pass. If the pass goes badly, the fallback
  is DS-12's row with the menu headers in it, which is a design change for the owner
  to take.
- **DS-16 puts 12 panels behind a menu.** That's the owner's call (DSD8), against
  today's single list. Its pass records what reaching them costs, so the owner can
  judge it on the real thing before it merges.
- **An accent's text can sit below gold's floor.** That's the owner's call too
  (DSD14). Their look at a few real accents (§8) shows how it reads, and how many hues
  fall back.
- **A composite font is unusual.** `Typeface.TryGetGlyphTypeface` returns false for
  one, which nothing in the app calls today (searched). DS-1's census keeps it that
  way, and `SlateEditor.CompositeFont` is one small file to drop if a problem appears:
  the editor then takes Cascadia's natural line height.
- **Ligatures may cost keystroke time.** There is no editor benchmark yet (W8-5).
  DS-1 records a stopwatch number in its PR, on a 1 MB note, against Consolas.
- **The live Text size path was unproved.** The canvas had trusted it since W6-1, but
  its facts called `RefreshForTests`. The owner's check passed on 2026-11-04, so DS-5
  keeps the registry and doesn't take `UISettings` and its 25 MB.
- **The design was built against a Web kit.** Some prompts describe behaviour the app
  already does differently and tests for (rail selection, tab close keys). A spec says
  which one stands; where it says nothing, today's tested behaviour does.
- **Twenty PRs is a lot of CI.** Each is a full Windows lane on home runners. Wave 1
  is the part with a clear payoff; §3 DSD1 lets the rest wait.
