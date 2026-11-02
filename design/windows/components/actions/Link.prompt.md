Fluent 2 "Link" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Link } = window.SlateDesignSystem_326dc9;
<Link appearance="default" state="rest/visited" />
```

Props (from Link.d.ts):
- `style2?: "default" | "subtle" | "overbrand"`
- `state?: "rest/visited" | "hover" | "pressed" | "focused" | "disabled"`
- `showIcon?: boolean`
- `inlineStyle?: boolean`
- `text?: string`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Hyperlink` inside a `TextBlock`.
- **Rating:** Native: the Fluent style ships, so override resource keys only.
- The Fluent `Hyperlink` style ships in .NET 10. A standalone link is a `Button` with a link template.
- Text uses the accent text colour (gold by default).

Ratings and theme setup: `wpf-feasibility.md`.
