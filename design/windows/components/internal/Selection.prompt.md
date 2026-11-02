Fluent 2 "Selection" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Selection } = window.SlateDesignSystem_326dc9;
<Selection type="checkbox" />
```

Props (from Selection.d.ts):
- `type?: "checkbox" | "checkmark" | "radio"`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
