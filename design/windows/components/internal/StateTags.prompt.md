Fluent 2 "StateTags" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { StateTags } = window.SlateDesignSystem_326dc9;
<StateTags state="visionary feature" />
```

Props (from StateTags.d.ts):
- `state?: "visionary feature" | "experimental feature" | "establishing pattern" | "coherent pattern" | "local feature" | "incoherent feature"`
- `text1?: string`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
