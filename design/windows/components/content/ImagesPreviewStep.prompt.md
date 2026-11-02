Fluent System Icon "ImagesPreviewStep" from the Fluent 2 Web kit — use for inline glyphs at a fixed size/theme; prefer the shared <Icon name> wrapper (assets/icons) for new work.

```jsx
const { ImagesPreviewStep } = window.SlateDesignSystem_326dc9;
<ImagesPreviewStep state="selected" />
```

Props (from ImagesPreviewStep.d.ts):
- `state?: "selected" | "rest"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** Custom control.
- **Rating:** Build: needs code or a custom control.
- No Slate screen uses it.

Ratings and theme setup: `wpf-feasibility.md`.
