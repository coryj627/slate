Fluent 2 "AppItemSmall" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { AppItemSmall } = window.SlateDesignSystem_326dc9;
<AppItemSmall state="rest" />
```

Props (from AppItemSmall.d.ts):
- `state?: "rest" | "hover" | "pressed"`
- `text?: string`
- `iconBefore?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** Header `Button` at the top of the nav.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- See Nav.

Ratings and theme setup: `wpf-feasibility.md`.
