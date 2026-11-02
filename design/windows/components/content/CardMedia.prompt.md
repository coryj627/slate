Fluent 2 "CardMedia" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { CardMedia } = window.SlateDesignSystem_326dc9;
<CardMedia aspectRatio="16:9" />
```

Props (from CardMedia.d.ts):
- `checkbox?: boolean`
- `aspectRatio?: "16:9" | "4:3" | "flex (default)"`
- `mediaIcon?: boolean`
- `pxIcon20?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Image`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- `Stretch="UniformToFill"` with a clip that matches the card radius.

Ratings and theme setup: `wpf-feasibility.md`.
