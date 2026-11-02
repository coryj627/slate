Fluent 2 kit subcomponent "FlexSpace" — internal building block used by the larger components; rarely placed directly.

```jsx
const { FlexSpace } = window.SlateDesignSystem_326dc9;
<FlexSpace />
```

Props (from FlexSpace.d.ts):
- `flexSpace?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
