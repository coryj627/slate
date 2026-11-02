Fluent 2 "MaterialAcrylic" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { MaterialAcrylic } = window.SlateDesignSystem_326dc9;
<MaterialAcrylic material="acrylic" />
```

Props (from MaterialAcrylic.d.ts):
- `material?: "acrylic"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** None.
- **Rating:** Web-only: don't use it in the Windows app.
- WPF popups and panes can't show acrylic, and Slate turns the window backdrop off (policy W1-1). Use the solid surface colours.

Ratings and theme setup: `wpf-feasibility.md`.
