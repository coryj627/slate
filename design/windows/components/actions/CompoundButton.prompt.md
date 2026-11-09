Fluent 2 "CompoundButton" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { CompoundButton } = window.SlateDesignSystem_326dc9;
<CompoundButton appearance="primary" state="rest" />
```

Props (from CompoundButton.d.ts):
- `image?: boolean`
- `secondaryText?: boolean`
- `primaryString?: string`
- `secondaryString?: string`
- `style2?: "primary" | "secondary (default)" | "outline" | "subtle" | "transparent"`
- `state?: "rest" | "hover" | "pressed" | "selected" | "disabled"`
- `size?: "medium (default)" | "sm" | "lg"`
- `focus?: boolean`
- `swap40x40Img?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Button`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Content template with the label and a secondary line.
- Primary uses the accent brushes. Accent brushes are gold by default and switch to the user's Windows accent when they have set a custom one (as Windows gives it, whatever its contrast: plan 46, DSD16).

Ratings and theme setup: `wpf-feasibility.md`.
