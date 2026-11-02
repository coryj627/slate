Fluent 2 "Carousel" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Carousel } = window.SlateDesignSystem_326dc9;
<Carousel layout="over content" chevronPlacement="flexible to edges" />
```

Props (from Carousel.d.ts):
- `layout?: "over content" | "outside content"`
- `chevronPlacement?: "flexible to edges" | "grouped to steps" | "centered to content"`
- `pauseButton?: "in nav" | "on content click"`
- `navType?: "steps" | "image preview"`
- `title?: boolean`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** Custom control.
- **Rating:** Build: needs code or a custom control.
- No Slate screen uses it. If needed: one visible item, arrow keys and buttons to move, and a pause control when it auto-plays.

Ratings and theme setup: `wpf-feasibility.md`.
