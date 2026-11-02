Fluent System Icon "SubtractCircle" from the Fluent 2 Web kit — use for inline glyphs at a fixed size/theme; prefer the shared <Icon name> wrapper (assets/icons) for new work.

```jsx
const { SubtractCircle } = window.SlateDesignSystem_326dc9;
<SubtractCircle size="28" theme="regular" />
```

Props (from SubtractCircle.d.ts):
- `size?: "28" | "24" | "20" | "32" | "16" | "12" | "48"`
- `theme?: "regular" | "filled"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
