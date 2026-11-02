Fluent 2 "Divider" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Divider } = window.SlateDesignSystem_326dc9;
<Divider layout="center (default)" />
```

Props (from Divider.d.ts):
- `contentSlot?: boolean`
- `layout?: "center (default)" | "start" | "end"`
- `vertical?: boolean`
- `text?: boolean`
- `textString?: string`
- `icon?: boolean`
- `pxIcon20?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Separator`.
- **Rating:** Native: the Fluent style ships, so override resource keys only.
- Vertical dividers: rotate with `LayoutTransform`, or use a 1px `Border`.

Ratings and theme setup: `wpf-feasibility.md`.
