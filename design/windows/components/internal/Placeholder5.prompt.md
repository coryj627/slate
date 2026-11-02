Fluent 2 kit subcomponent "Placeholder5" — internal building block used by the larger components; rarely placed directly.

```jsx
const { Placeholder5 } = window.SlateDesignSystem_326dc9;
<Placeholder5 slot="1" />
```

Props (from Placeholder5.d.ts):
- `slot?: "1" | "2"`
- `image?: React.ReactNode`
- `instance?: React.ReactNode`
- `slotBefore?: boolean`
- `text1?: string`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
