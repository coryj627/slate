Fluent 2 "Dropdown" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Dropdown } = window.SlateDesignSystem_326dc9;
<Dropdown appearance="outline" size="small" />
```

Props (from Dropdown.d.ts):
- `appearance?: "outline" | "transparent" | "fill lighter" | "fill darker"`
- `size?: "sm" | "md" | "lg"`
- `expanded?: boolean`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `ComboBox`.
- **Rating:** Native: the Fluent style ships, so override resource keys only.
- The Fluent `ComboBox` style ships. Its popup uses `AllowsTransparency`, so set `RenderOptions.ClearTypeHint="Enabled"` on the opaque list surface.
- Selection text inside uses the Slate selection brushes.

Ratings and theme setup: `wpf-feasibility.md`.
