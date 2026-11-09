Icon strip at the top (or edge) of a pane that switches between its views. Use it wherever a pane has more views than fit as text tabs, such as Slate's right pane.

```jsx
const { PaneSwitcher } = window.SlateDesignSystem_326dc9;
<PaneSwitcher label="Right pane views" value={view} onChange={setView} maxVisible={6} items={[
  { id: 'properties', label: 'Properties', icon: 'E946' },
  { id: 'backlinks', label: 'Backlinks', icon: 'E71B' },
  { id: 'outline', label: 'Outline', icon: 'E8FD' },
  { id: 'history', label: 'History', icon: 'E81C' },
]} />
```

- Each view is an icon-only subtle Button (32 px) with a tooltip showing its name and chord. The open view gets the selected state and a 3 px gold indicator.
- Past `maxVisible`, the last slot becomes **More panels** (⋯). Its menu lists the rest with their icons, and the open one shows a check in place of its icon. The ⋯ button takes the selected look while one of those views is open.
- Keyboard: Tab enters the strip, arrow keys move, Home/End jump, Enter or Space opens. The More button is the next tab stop. In the app, an arrow opens the view it reaches (see WPF).
- `orientation="vertical"` stacks the strip, puts the indicator on the left and opens tooltips and the menu to the left.
- Render the view itself with `role="tabpanel"`.

## WPF

- **Build as:** `ListBox` with a Slate item template.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- The app's rail is already a `ListBox`, and tests pin its automation contract and its settled selection, so the strip keeps it. An arrow opens the panel it reaches at once, so the focused row is always the panel on show. The panel's reload, the queries refresh and the workspace write wait until the arrows settle: the last held arrow is released, or focus leaves the rail. Gold indicator on the selected row.
- The overflow is the fixed `maxVisible` of six: five views, then "More panels", a `Button` + `ContextMenu` below the list. Nothing is computed on `SizeChanged`, so a reader hears the same count at any window size.
- Buttons here are 32 px, as in panel toolbars. Only the title bar's are 40 × 40 (`winui-review.md`).
- If tooltips get in the way while arrowing, set `ToolTipService.ShowsToolTipOnKeyboardFocus="False"`.
- Icon-only versions need `AutomationProperties.Name`; otherwise Narrator reads the icon glyph.
- Slate's icons, as the owner reviewed them (plan 46 DSD19): `Kit.jsx`'s, except Backlinks, two interlocking links with an arrow coming in, drawn as a path since Segoe Fluent Icons has no chain; Outgoing links, `E71B` turned 45° to rise to the right; Graph inspector, `F22C`; and Diagrams, a flowchart drawn as a path. In the strip the slots are 40, the More panels button with them, and the icons 20.

Ratings and theme setup: `wpf-feasibility.md`.
