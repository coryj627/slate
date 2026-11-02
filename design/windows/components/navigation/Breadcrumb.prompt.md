Fluent 2 "Breadcrumb" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Breadcrumb } = window.SlateDesignSystem_326dc9;
<Breadcrumb size="small" />
```

Props (from Breadcrumb.d.ts):
- `size?: "sm" | "md" | "lg"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `ItemsControl` of BreadcrumbItems.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- WPF has no BreadcrumbBar. Chevron separators (E76C) are decorative. Collapse the middle items into a `…` menu when space runs out.

Ratings and theme setup: `wpf-feasibility.md`.
