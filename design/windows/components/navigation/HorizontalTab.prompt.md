Fluent 2 "HorizontalTab" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { HorizontalTab } = window.SlateDesignSystem_326dc9;
<HorizontalTab appearance="transparent" size="medium" />
```

Props (from HorizontalTab.d.ts):
- `iconUnselected?: boolean`
- `selected?: boolean`
- `style2?: "transparent (default)" | "subtle" | "filled circular" | "subtle circular"`
- `size?: "medium (default)" | "sm"`
- `state?: "rest" | "hover" | "pressed" | "focus" | "disabled"`
- `iconSelected?: boolean`
- `text1?: string`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `TabItem`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Slate template with the 2–3px gold underline (accent brushes; switches to the user's custom Windows accent).

Ratings and theme setup: `wpf-feasibility.md`.
