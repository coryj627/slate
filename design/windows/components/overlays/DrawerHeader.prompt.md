Fluent 2 "DrawerHeader" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { DrawerHeader } = window.SlateDesignSystem_326dc9;
<DrawerHeader />
```

Props (from DrawerHeader.d.ts):
- `navigation?: boolean`
- `leftActions?: boolean`
- `title?: string`
- `dismiss?: boolean`
- `divider?: boolean`
- `rightActions?: boolean`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Grid`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Title and close button. Name the close button.

Ratings and theme setup: `wpf-feasibility.md`.
