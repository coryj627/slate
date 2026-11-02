Fluent 2 "AvatarAvatarPie" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { AvatarAvatarPie } = window.SlateDesignSystem_326dc9;
<AvatarAvatarPie layout="2 avatars" size="16" />
```

Props (from AvatarAvatarPie.d.ts):
- `layout?: "2 avatars" | "3 avatars"`
- `size?: "16" | "20" | "24" | "28" | "32 (default)" | "36" | "40" | "48" | "56" | "64" | "72" | "96" | "120"`
- `icon1?: React.ReactNode`
- `icon2?: React.ReactNode`
- `icon3?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Grid` of clipped avatars.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- `Clip` geometries for the pie segments. Name the group, not each segment.

Ratings and theme setup: `wpf-feasibility.md`.
