Fluent 2 kit subcomponent "Placeholder6" — internal building block used by the larger components; rarely placed directly.

```jsx
const { Placeholder6 } = window.SlateDesignSystem_326dc9;
<Placeholder6 size="12" theme="regular" />
```

Props (from Placeholder6.d.ts):
- `size?: "12" | "16" | "20" | "24" | "28" | "32" | "48"`
- `theme?: "regular" | "filled" | "z"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
