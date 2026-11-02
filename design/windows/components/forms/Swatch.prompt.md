Fluent 2 "Swatch" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Swatch } = window.SlateDesignSystem_326dc9;
<Swatch state="rest" size="extra small" />
```

Props (from Swatch.d.ts):
- `state?: "rest" | "hover" | "pressed" | "disabled" | "empty" | "transparent"`
- `size?: "extrasmall" | "sm" | "medium (default)" | "lg"`
- `style2?: "color" | "icon" | "image"`
- `focus?: boolean`
- `selected?: boolean`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `ListBoxItem` or `RadioButton`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Name each swatch by colour name, not hex.

Ratings and theme setup: `wpf-feasibility.md`.
