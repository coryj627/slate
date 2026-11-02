Fluent 2 "Drawer" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Drawer } = window.SlateDesignSystem_326dc9;
<Drawer type="overlay" />
```

Props (from Drawer.d.ts):
- `type?: "overlay" | "inline"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Grid` column or overlay.
- **Rating:** Build: needs code or a custom control.
- Inline: a `Grid` column with a `GridSplitter` (Fluent style ships in .NET 10). Overlay: see OverlayDrawer.

Ratings and theme setup: `wpf-feasibility.md`.
