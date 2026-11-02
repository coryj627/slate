A row with an icon, a label and a count on the right. Use it for the tag list, the vault's property list and the notes in Backlinks.

```jsx
const { CountListRow } = window.SlateDesignSystem_326dc9;
<div role="tree" aria-label="Tags">
  <CountListRow icon="E8EC" label="projects" count={2} countLabel="notes" expanded={open} onToggle={setOpen} />
  {open && <CountListRow icon="E8EC" label="reading" count={1} countLabel="notes" depth={1} nested />}
  <CountListRow icon="E8EC" label="math" count={6} countLabel="notes" nested selected />
</div>
```

- 32 px (medium) or 24 px (small), like the Fluent TreeItem. Label 14/20, count 12/16 tertiary with tabular figures.
- Selected uses the pale gold fill, semibold label and the 3 px gold indicator. Hover uses `--surface-hover`, and keyboard focus draws a 2 px ring.
- Pass `expanded` to show the chevron. Set `nested` on the other rows of the same tree so labels line up, and `depth` to indent (20 px a level).
- The accessible name reads "label, count countLabel", for example "math, 6 notes".
- Typical icons: tag E8EC, note E8A5, text property E8D2, date property E787, list property E8FD, number property E8EF, link E71B.

## WPF

- **Build as:** `TreeViewItem` (or `ListBoxItem`) template.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Icon, label and right-aligned count. Fold the count into the name ("Linear algebra, 12"). Backlink rows expand as tree nodes.

Ratings and theme setup: `wpf-feasibility.md`.
