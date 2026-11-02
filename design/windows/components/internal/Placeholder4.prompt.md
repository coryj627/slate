Fluent 2 kit subcomponent "Placeholder4" — internal building block used by the larger components; rarely placed directly.

```jsx
const { Placeholder4 } = window.SlateDesignSystem_326dc9;
<Placeholder4 size="12" theme="regular" />
```

Props (from Placeholder4.d.ts):
- `size?: "12" | "16" | "20" | "24" | "28" | "32" | "48"`
- `theme?: "regular" | "filled"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
