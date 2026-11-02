Fluent 2 "DrawerFooter" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { DrawerFooter } = window.SlateDesignSystem_326dc9;
<DrawerFooter />
```

Props (from DrawerFooter.d.ts):
- `primaryButton?: boolean`
- `moreButton?: boolean`
- `divider?: boolean`
- `secondaryButton?: boolean`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `StackPanel` of buttons.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Part of Drawer.

Ratings and theme setup: `wpf-feasibility.md`.
