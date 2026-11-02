Fluent System Icon "Bell" from the Fluent 2 Web kit — use for inline glyphs at a fixed size/theme; prefer the shared <Icon name> wrapper (assets/icons) for new work.

```jsx
const { Bell } = window.SlateDesignSystem_326dc9;
<Bell variant="default" state="default" />
```

Props (from Bell.d.ts):
- `variant?: "default" | "message" | "message w dismiss"`
- `icon?: boolean`
- `close?: boolean`
- `cTA2?: boolean`
- `state?: "default" | "danger"`
- `cTA1?: boolean`
- `icon2?: React.ReactNode`
- `count?: boolean`
- `cTARail?: boolean`
- `text1?: string`
- `text2?: string`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
