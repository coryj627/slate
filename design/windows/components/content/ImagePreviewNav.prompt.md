Fluent System Icon "ImagePreviewNav" from the Fluent 2 Web kit — use for inline glyphs at a fixed size/theme; prefer the shared <Icon name> wrapper (assets/icons) for new work.

```jsx
const { ImagePreviewNav } = window.SlateDesignSystem_326dc9;
<ImagePreviewNav />
```

Props (from ImagePreviewNav.d.ts):
- `icon1?: React.ReactNode`
- `icon2?: React.ReactNode`
- `icon3?: React.ReactNode`
- `icon4?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** Custom control.
- **Rating:** Build: needs code or a custom control.
- No Slate screen uses it.

Ratings and theme setup: `wpf-feasibility.md`.
