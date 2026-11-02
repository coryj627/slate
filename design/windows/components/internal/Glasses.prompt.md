Fluent System Icon "Glasses" from the Fluent 2 Web kit — use for inline glyphs at a fixed size/theme; prefer the shared <Icon name> wrapper (assets/icons) for new work.

```jsx
const { Glasses } = window.SlateDesignSystem_326dc9;
<Glasses size="16" theme="regular" />
```

Props (from Glasses.d.ts):
- `size?: "16" | "20" | "24" | "28" | "32" | "48"`
- `theme?: "regular" | "filled"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
