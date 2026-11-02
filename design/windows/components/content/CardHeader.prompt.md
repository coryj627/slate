Fluent 2 "CardHeader" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { CardHeader } = window.SlateDesignSystem_326dc9;
<CardHeader />
```

Props (from CardHeader.d.ts):
- `customHeader?: boolean`
- `checkbox?: boolean`
- `avatar?: boolean`
- `headerQuickAction?: boolean`
- `subtextString?: string`
- `productIcon?: boolean`
- `titleString?: string`
- `subtext?: boolean`
- `pxIcon32?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Grid`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Icon, title and an icon button; the raised-surface strip is a `Border` with only the top corners rounded.
- Icon-only versions need `AutomationProperties.Name`; otherwise Narrator reads the icon glyph.

Ratings and theme setup: `wpf-feasibility.md`.
