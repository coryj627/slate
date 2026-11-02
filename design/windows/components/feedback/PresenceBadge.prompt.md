Fluent 2 "PresenceBadge" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { PresenceBadge } = window.SlateDesignSystem_326dc9;
<PresenceBadge status="offline" size="extra large huge" />
```

Props (from PresenceBadge.d.ts):
- `inOffice?: boolean`
- `status?: "offline" | "available" | "busy" | "away" | "blocked" | "do not disturb" | "unknown" | "oof"`
- `size?: "extra-large huge" | "lg" | "md" | "sm" | "extra-small" | "tiny"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Ellipse` + glyph.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Never rely on colour alone; include the status in the accessible name.

Ratings and theme setup: `wpf-feasibility.md`.
