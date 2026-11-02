Fluent 2 "SpinnerBase" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { SpinnerBase } = window.SlateDesignSystem_326dc9;
<SpinnerBase size="x tiny" />
```

Props (from SpinnerBase.d.ts):
- `size?: "x-tiny" | "tiny" | "xs" | "sm" | "md" | "lg" | "xl" | "huge" | "tiny 01" | "tiny 02" | "tiny 03" | "x-small 01" | "x-small 02" | "x-small 03" | "small 01" | "small 02" | "small 03" | "medium 01" | "medium 02" | "medium 03" | "large 01" | "large 02" | "large 03" | "x-large 01" | "x-large 02" | "x-large 03" | "huge 01" | "huge 02" | "huge 03" | "x-tiny 01" | "x-tiny 02" | "x-tiny 03"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
