Fluent 2 "SecondaryAction" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { SecondaryAction } = window.SlateDesignSystem_326dc9;
<SecondaryAction appearance="primary" state="rest" />
```

Props (from SecondaryAction.d.ts):
- `focus?: boolean`
- `style2?: "primary" | "secondary (default)" | "outline" | "subtle" | "transparent"`
- `state?: "rest" | "hover" | "pressed" | "selected" | "disabled"`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
