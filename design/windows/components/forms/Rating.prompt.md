Fluent 2 "Rating" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Rating } = window.SlateDesignSystem_326dc9;
<Rating type="interactive" size="small" />
```

Props (from Rating.d.ts):
- `ratingQuantity?: string`
- `type?: "interactive" | "display"`
- `size?: "sm" | "md" | "lg" | "xl"`
- `compact?: boolean`
- `shape?: "stars" | "circles" | "square"`
- `text1?: string`
- `text2?: string`
- `icon1?: React.ReactNode`
- `icon2?: React.ReactNode`
- `icon3?: React.ReactNode`
- `icon4?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `RadioButton` group with star glyphs.
- **Rating:** Build: needs code or a custom control.
- Each star is named ("3 stars"). The filled stars use the accent brushes.

Ratings and theme setup: `wpf-feasibility.md`.
