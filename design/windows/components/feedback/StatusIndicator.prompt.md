Fluent 2 "StatusIndicator" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { StatusIndicator } = window.SlateDesignSystem_326dc9;
<StatusIndicator message="archived" size="medium" />
```

Props (from StatusIndicator.d.ts):
- `message?: "archived" | "cancelled" | "draft" | "failed" | "inprogress" | "new" | "not started" | "pause" | "pending" | "scheduled" | "success" | "synced" | "syncing" | "unknown" | "warning" | "generic information"`
- `size?: "medium (default)" | "sm" | "lg"`
- `category?: "informational/neutral" | "success" | "error" | "active/ in progress" | "warning"`
- `label?: boolean`
- `genericLabel?: string`
- `iconX20?: React.ReactNode`
- `iconX12?: React.ReactNode`
- `iconX16?: React.ReactNode`
- `text1?: string`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Ellipse` or glyph.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Pair it with text or an accessible name; never colour alone.

Ratings and theme setup: `wpf-feasibility.md`.
