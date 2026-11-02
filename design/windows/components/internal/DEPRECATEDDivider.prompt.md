Fluent 2 "DEPRECATEDDivider" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { DEPRECATEDDivider } = window.SlateDesignSystem_326dc9;
<DEPRECATEDDivider layout="default" appearance="default" />
```

Props (from DEPRECATEDDivider.d.ts):
- `layout?: "default" | "with content" | "with icon"`
- `style2?: "default" | "subtle" | "strong" | "brand"`
- `inset?: "block (default)" | "inset"`
- `contentAlign?: "center (default)" | "start" | "end"`
- `orientation?: "horizontal"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
