Fluent 2 "InteractionTag" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { InteractionTag } = window.SlateDesignSystem_326dc9;
<InteractionTag appearance="filled" size="extra small" />
```

Props (from InteractionTag.d.ts):
- `style2?: "filled (default)" | "outline" | "brand"`
- `size?: "extra small" | "sm" | "medium (default)"`
- `layout?: "1 line (default)" | "2 line (medium only)"`
- `state?: "rest" | "hover" | "pressed" | "disabled"`
- `selected?: boolean`
- `dismiss?: boolean`
- `avatar?: boolean`
- `focus?: boolean`
- `icon?: boolean`
- `regular12px?: React.ReactNode`
- `regular20px?: React.ReactNode`
- `regular16px?: React.ReactNode`
- `filled20px?: React.ReactNode`
- `secondaryString?: string`
- `filled16px?: React.ReactNode`
- `filled12px?: React.ReactNode`
- `primaryString?: string`
- `icon2?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Button`, plus a dismiss `Button`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- The dismiss button is named "Remove <tag>".

Ratings and theme setup: `wpf-feasibility.md`.
