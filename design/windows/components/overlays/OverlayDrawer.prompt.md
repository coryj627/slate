Fluent 2 "OverlayDrawer" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { OverlayDrawer } = window.SlateDesignSystem_326dc9;
<OverlayDrawer size="small" />
```

Props (from OverlayDrawer.d.ts):
- `footer?: boolean`
- `size?: "small (default)" | "md" | "lg"`
- `contentSwap?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** Overlay `Border` + scrim.
- **Rating:** Build: needs code or a custom control.
- Focus moves in, Esc closes, focus returns to the trigger.

Ratings and theme setup: `wpf-feasibility.md`.
