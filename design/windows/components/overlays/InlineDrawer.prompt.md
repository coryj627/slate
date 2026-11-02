Fluent 2 "InlineDrawer" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { InlineDrawer } = window.SlateDesignSystem_326dc9;
<InlineDrawer size="small" verticalDivider="none" />
```

Props (from InlineDrawer.d.ts):
- `footer?: boolean`
- `contentSwap?: React.ReactNode`
- `size?: "small (default)" | "md" | "lg"`
- `verticalDivider?: "none" | "left" | "right"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Grid` column + `GridSplitter`.
- **Rating:** Build: needs code or a custom control.
- Resizable by keyboard as well as mouse, like Slate's Widen and Narrow commands.

Ratings and theme setup: `wpf-feasibility.md`.
