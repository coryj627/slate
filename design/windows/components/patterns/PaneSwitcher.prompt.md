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
- Keyboard: Tab enters the strip, arrow keys move, Home/End jump, Enter or Space opens. The More button is the next tab stop.
- `orientation="vertical"` stacks the strip, puts the indicator on the left and opens tooltips and the menu to the left.
- Render the view itself with `role="tabpanel"`.

## WPF

- **Build as:** `TabControl` with a Slate template.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- The strip is the tab panel and the right pane is the content, so Narrator announces tabs. Gold indicator on the selected tab. "More panels" is a `Button` + `ContextMenu`; compute the overflow on `SizeChanged`.
- If tooltips get in the way while arrowing, set `ToolTipService.ShowsToolTipOnKeyboardFocus="False"`.
- Icon-only versions need `AutomationProperties.Name`; otherwise Narrator reads the icon glyph.

Ratings and theme setup: `wpf-feasibility.md`.
