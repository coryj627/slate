Fluent 2 "Card" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Card } = window.SlateDesignSystem_326dc9;
<Card layout="default" state="rest" />
```

Props (from Card.d.ts):
- `layout?: "default" | "custom"`
- `state?: "rest" | "hover" | "pressed" | "draggable" | "selected" | "disabled"`
- `style2?: "filled" | "filled alt" | "outline" | "subtle"`
- `body?: boolean`
- `footer?: boolean`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Border`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Radius 8, 1px subtle stroke, surface fill. If the card has a shadow, put the effect on a background-only `Border` behind the content so text keeps ClearType.

Ratings and theme setup: `wpf-feasibility.md`.
