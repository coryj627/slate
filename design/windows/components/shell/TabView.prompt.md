Tab strip for open notes; use it above the editor or reader, one tab per open note.

```jsx
const { TabView } = window.SlateDesignSystem_326dc9;
<TabView value={active} onChange={setActive} onClose={closeTab} onAdd={newNote}
  tabs={[{ id: "a", label: "Linear algebra lecture 3", icon: "E8A5" }, { id: "b", label: "Why Slate", icon: "E8A5" }]} />
```

- From the Windows UI kit (Navigation › Tab view, in content area): 32 px tabs 100–240 px wide, 8 px top radius, 16 px icon at 8 px, label at 32 px, 40 × 32 close button with a 12 px E711 glyph.
- The open tab uses `--surface-card` and joins the content below with flared corners. Other tabs are text-secondary, separated by 1 px dividers.
- Keyboard: Left/Right/Home/End move and open; Delete or Ctrl+F4 closes. Only the open tab is a tab stop.

## WPF

- **Build as:** `TabControl` with a custom `TabItem` template (WPF has no TabView).
- **Rating:** Template: a new `ControlTemplate` on an existing control.
- Flared corners are two small `Path`s at the bottom of the selected tab; the close button is a `Button` in the header template.

Ratings and theme setup: `wpf-feasibility.md`.
