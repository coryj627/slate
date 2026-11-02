Fluent 2 "Avatar" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Avatar } = window.SlateDesignSystem_326dc9;
<Avatar layout="image" size="16" />
```

Props (from Avatar.d.ts):
- `presence?: boolean`
- `layout?: "image" | "icon" | "initials"`
- `size?: "16" | "20" | "24" | "28" | "32" | "36" | "40" | "48" | "56" | "64" | "72" | "96" | "120"`
- `color?: "neutral" | "brand" | "colored"`
- `activityRing?: boolean`
- `text?: string`
- `icon1?: React.ReactNode`
- `icon2?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Ellipse` + initials `TextBlock`, or `ImageBrush`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Decorative next to a name; otherwise set `AutomationProperties.Name` to the person.

Ratings and theme setup: `wpf-feasibility.md`.
