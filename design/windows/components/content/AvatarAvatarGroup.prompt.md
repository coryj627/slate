Fluent 2 "AvatarAvatarGroup" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { AvatarAvatarGroup } = window.SlateDesignSystem_326dc9;
<AvatarAvatarGroup layout="spread" />
```

Props (from AvatarAvatarGroup.d.ts):
- `layout?: "spread" | "stack"`
- `icon1?: React.ReactNode`
- `icon2?: React.ReactNode`
- `icon3?: React.ReactNode`
- `icon4?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `ItemsControl` of avatars.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Negative margins for the overlap; the overflow count is a `Button` that opens a list.

Ratings and theme setup: `wpf-feasibility.md`.
