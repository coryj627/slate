The 40 px bar between the tab strip and a note: back/forward, the folder breadcrumb, the Source/Reading switch and the ⋯ menu.

```jsx
const { NoteHeader } = window.SlateDesignSystem_326dc9;
<NoteHeader path={['learning', 'Linear algebra lecture 3']} canGoBack onBack={back}
  mode={mode} onModeChange={setMode} shortcuts={{ mode: 'Ctrl+Shift+E' }}
  moreItems={[
    { label: 'Split Right', shortcut: 'Ctrl+\\' },
    { separator: true },
    { label: 'Add Property…' },
    { label: 'Bulk Rename Properties…', shortcut: 'Ctrl+Shift+R' },
  ]} />
```

- Back and forward are 32 px subtle icon-only Buttons (E72B, E72A); they stay visible but disabled when there's no history. The app leaves both out for now (see WPF).
- Folder segments are text buttons in secondary text, separated by a 12 px chevron (E76C). The note name is semibold primary text and carries `aria-current="page"`. When space runs out, folder names truncate first, then the note name.
- The view switch is a two-option radio group (Source E943, Reading E736): arrow keys switch, and only the checked option is a tab stop. Below 520 px the switch drops its labels and shows tooltips instead; set `compact` to force either way.
- The ⋯ menu follows the menu rules: one leading column, shortcuts in the end column, disabled rows skipped by the arrow keys.
- The bar uses the note surface (`--surface-card`), so it reads as part of the note.

- Compact (below 520 px, or `compact`): the folders collapse into a "…" button that opens a Folders menu, as in the WinUI breadcrumb. The note name keeps the room.

## WPF

- **Build as:** `Grid`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Back and forward wait for a note history, which the app doesn't have: its only history is the sidebar's, which Quick Open and links don't feed. Bound to that, Back would sit disabled after Quick Open or walk sidebar selections, so both buttons stay out until a per-tab history exists (plan 46, DSD15).
- Source/Reading is two `RadioButton`s with a segmented template. ⋯ is a `Button` + `ContextMenu`. Icon buttons here are 32 px. Only the title bar's are 40 × 40 (`winui-review.md`).
- Icon-only below 520px: call `VisualStateManager.GoToState` from `SizeChanged` (WPF has no `AdaptiveTrigger`).
- Icon-only versions need `AutomationProperties.Name`; otherwise Narrator reads the icon glyph.

Ratings and theme setup: `wpf-feasibility.md`.
