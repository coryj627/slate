The 40 px row at the top of a panel (Files, Backlinks, Tags, Outline…): title and count on the left, then search, sort and collapse/expand all.

```jsx
const { PanelToolbar } = window.SlateDesignSystem_326dc9;
<PanelToolbar title="Backlinks" count={3}
  sortOptions={[{ value: 'name', label: 'Note name' }, { value: 'modified', label: 'Modified' }]}
  sortValue={sort} onSortChange={setSort}
  expanded={open} onExpandedChange={setOpen} />
```

- Buttons are 32 px subtle icon-only Buttons with tooltips: search E721, sort E8CB, expand all F164, collapse all F166.
- Search replaces the title with a 32 px search field. Esc clears the text; a second Esc (or the × button) closes it and returns focus to the search button. `searchOpen` and `query` can be controlled.
- The sort menu keeps the check column on every row. `{ label, checked, onSelect }` entries add toggles such as Group by date; `{ separator: true }` divides groups.
- Omit `expanded` to hide the collapse/expand toggle and `sortOptions` to hide sort. Set `search={false}` when the panel already has a filter field.

## WPF

- **Build as:** `Grid` (or `ToolBar`).
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Title, count, icon buttons. Sort is a `Button` + `ContextMenu` with checkable items.
- Icon-only versions need `AutomationProperties.Name`; otherwise Narrator reads the icon glyph.

Ratings and theme setup: `wpf-feasibility.md`.
