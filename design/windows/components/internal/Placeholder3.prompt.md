Fluent 2 kit subcomponent "Placeholder3" — internal building block used by the larger components; rarely placed directly.

```jsx
const { Placeholder3 } = window.SlateDesignSystem_326dc9;
<Placeholder3 slot="1" />
```

Props (from Placeholder3.d.ts):
- `image?: React.ReactNode`
- `slot?: "1" | "2"`
- `instance?: React.ReactNode`
- `slotBefore?: boolean`
- `text1?: string`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
