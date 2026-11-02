Fluent 2 "Tag" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Tag } = window.SlateDesignSystem_326dc9;
<Tag appearance="filled" size="medium" />
```

Props (from Tag.d.ts):
- `avatar?: boolean`
- `iconInstance16px?: React.ReactNode`
- `iconInstance12px?: React.ReactNode`
- `primaryString?: string`
- `secondaryString?: string`
- `dismiss?: boolean`
- `iconBefore?: boolean`
- `iconInstance20px?: React.ReactNode`
- `style2?: "filled" | "outline" | "brand"`
- `size?: "md" | "sm" | "extra small"`
- `radius?: "rounded square" | "circular"`
- `state?: "rest" | "hover (dismiss only)" | "pressed (dismiss only)" | "disabled" | "focus"`
- `secondaryText?: boolean`
- `icon1?: React.ReactNode`
- `icon2?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Border` + `TextBlock`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Display only. Interactive tags use InteractionTag.

Ratings and theme setup: `wpf-feasibility.md`.
