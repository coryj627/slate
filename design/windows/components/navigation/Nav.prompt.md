Fluent 2 "Nav" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Nav } = window.SlateDesignSystem_326dc9;
<Nav />
```

Props (from Nav.d.ts):
- `navBody?: React.ReactNode`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `TreeView` (nested) or `ListBox` (flat).
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- WPF has no NavigationView. Selected items get the pale gold fill and the 3px gold pill. Collapse to icons with a width-based visual state.

Ratings and theme setup: `wpf-feasibility.md`.
