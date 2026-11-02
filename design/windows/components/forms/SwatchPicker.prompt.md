Fluent 2 "SwatchPicker" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { SwatchPicker } = window.SlateDesignSystem_326dc9;
<SwatchPicker size="extra small" layout="grid" />
```

Props (from SwatchPicker.d.ts):
- `size?: "extrasmall" | "sm" | "medium (default)" | "lg"`
- `layout?: "grid" | "row"`
- `spacing?: "md" | "sm"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `ListBox` with a `WrapPanel`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Selected ring uses the accent brushes. Arrow keys move between swatches.

Ratings and theme setup: `wpf-feasibility.md`.
