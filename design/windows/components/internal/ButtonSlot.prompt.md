Fluent 2 kit subcomponent "ButtonSlot" — internal building block used by the larger components; rarely placed directly.

```jsx
const { ButtonSlot } = window.SlateDesignSystem_326dc9;
<ButtonSlot />
```

Props (from ButtonSlot.d.ts):
- `button1?: boolean`
- `button2?: boolean`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
