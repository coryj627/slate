Fluent 2 "Textarea" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Textarea } = window.SlateDesignSystem_326dc9;
<Textarea appearance="outline" size="small" />
```

Props (from Textarea.d.ts):
- `style2?: "outline" | "filled darker" | "filled lighter"`
- `size?: "sm" | "medium (default)" | "lg"`
- `state?: "rest" | "hover" | "pressed" | "focus" | "error" | "disabled" | "read only"`
- `showResizeHandler?: boolean`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `TextBox` with `AcceptsReturn` and `TextWrapping="Wrap"`.
- **Rating:** Native: the Fluent style ships, so override resource keys only.
- Same placeholder and selection notes as Input.

Ratings and theme setup: `wpf-feasibility.md`.
