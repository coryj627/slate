Fluent 2 "Skeleton" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Skeleton } = window.SlateDesignSystem_326dc9;
<Skeleton shape="rectangle (default)" frame="1" />
```

Props (from Skeleton.d.ts):
- `shape?: "rectangle (default)" | "circle"`
- `animated?: boolean`
- `frame?: "1" | "2" | "3" | "4" | "5"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Rectangle`s with an animated `LinearGradientBrush`.
- **Rating:** Build: needs code or a custom control.
- Static when animations are off. Hide it from screen readers and announce "Loading" on the container.

Ratings and theme setup: `wpf-feasibility.md`.
