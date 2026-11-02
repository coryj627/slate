Fluent 2 "Badge" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Badge } = window.SlateDesignSystem_326dc9;
<Badge color="brand" size="extra large" />
```

Props (from Badge.d.ts):
- `color?: "brand" | "danger" | "warning" | "success" | "important" | "informative" | "subtle"`
- `size?: "extra large" | "lg" | "md" | "sm"`
- `appearance?: "filled" | "tint" | "outline" | "subtle"`
- `leftIcon?: boolean`
- `showText?: boolean`
- `rightIcon?: boolean`
- `text?: string`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Border` + `TextBlock`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Fold the count into the parent's `AutomationProperties.Name` ("Backlinks, 12").

Ratings and theme setup: `wpf-feasibility.md`.
