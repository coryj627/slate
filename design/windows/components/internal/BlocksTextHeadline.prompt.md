Fluent 2 kit subcomponent "BlocksTextHeadline" — internal building block used by the larger components; rarely placed directly.

```jsx
const { BlocksTextHeadline } = window.SlateDesignSystem_326dc9;
<BlocksTextHeadline appearance="xxl headline" />
```

Props (from BlocksTextHeadline.d.ts):
- `preview?: boolean`
- `style2?: "xxl headline" | "xl headline" | "l headline" | "m headline"`
- `text1?: string`
- `text2?: string`
- `text3?: string`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
