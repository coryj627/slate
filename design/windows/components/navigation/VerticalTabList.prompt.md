Fluent 2 "VerticalTabList" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { VerticalTabList } = window.SlateDesignSystem_326dc9;
<VerticalTabList layout="icon before" size="medium" />
```

Props (from VerticalTabList.d.ts):
- `layout?: "icon before" | "text only" | "icon only"`
- `size?: "medium (default)" | "sm"`
- `circular?: boolean`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `TabControl` with `TabStripPlacement="Left"`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Arrow keys move between tabs.

Ratings and theme setup: `wpf-feasibility.md`.
