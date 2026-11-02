Fluent 2 "Button3" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Button3 } = window.SlateDesignSystem_326dc9;
<Button3 variant="primary" iconLead="false" />
```

Props (from Button3.d.ts):
- `icon?: React.ReactNode`
- `variant?: "primary" | "destructive" | "inverse" | "success" | "figjam" | "secondary" | "secondary destruct" | "link" | "link danger" | "ghost"`
- `iconLead?: "false" | "left-aligned" | "center-aligned"`
- `label?: string`
- `size?: "default" | "lg" | "wide"`
- `disabled?: boolean`
- `hotkey?: boolean`
- `state?: "default" | "active" | "focused" | "hover"`
- `text1?: string`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
