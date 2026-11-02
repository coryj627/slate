Fluent 2 "VerticalTab" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { VerticalTab } = window.SlateDesignSystem_326dc9;
<VerticalTab appearance="transparent" size="medium" />
```

Props (from VerticalTab.d.ts):
- `iconSelected?: boolean`
- `iconUnselected?: boolean`
- `selected?: boolean`
- `style2?: "transparent (default)" | "subtle" | "filled circular" | "subtle circular"`
- `size?: "medium (default)" | "sm"`
- `state?: "rest" | "hover" | "pressed" | "focus" | "disabled"`
- `text1?: string`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `TabItem` or `ListBoxItem`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- 3px gold pill on the leading edge when selected.

Ratings and theme setup: `wpf-feasibility.md`.
