Fluent 2 "BreadcrumbItem" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { BreadcrumbItem } = window.SlateDesignSystem_326dc9;
<BreadcrumbItem size="large" type="button" />
```

Props (from BreadcrumbItem.d.ts):
- `size?: "lg" | "md" | "sm"`
- `type?: "button" | "overflow"`
- `states?: "rest" | "hover" | "pressed" | "focused" | "disabled" | "current"`
- `icon?: "on" | "off"`
- `dividerIcon?: React.ReactNode`
- `divider?: boolean`
- `icon2?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Button` with a link-like template.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- The current (last) item isn't a button.

Ratings and theme setup: `wpf-feasibility.md`.
