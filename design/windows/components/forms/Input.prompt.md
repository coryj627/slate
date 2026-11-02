Fluent 2 "Input" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Input } = window.SlateDesignSystem_326dc9;
<Input appearance="outline" size="small" />
```

Props (from Input.d.ts):
- `iconBefore16pxRegular?: React.ReactNode`
- `style2?: "outline" | "filled darker" | "filled lighter" | "underline"`
- `size?: "sm" | "medium (default)" | "lg"`
- `state?: "rest" | "hover" | "pressed" | "focus" | "error" | "disabled" | "read only"`
- `iconBefore20pxRegular?: React.ReactNode`
- `iconBefore24pxRegular?: React.ReactNode`
- `iconAfter1?: boolean`
- `iconAfter116pxRegular?: React.ReactNode`
- `iconAfter120pxRegular?: React.ReactNode`
- `iconAfter124pxRegular?: React.ReactNode`
- `iconAfter2?: boolean`
- `iconBefore?: boolean`
- `iconAfter216pxRegular?: React.ReactNode`
- `iconAfter224pxRegular?: React.ReactNode`
- `iconAfter220pxRegular?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `TextBox`.
- **Rating:** Native: the Fluent style ships, so override resource keys only.
- The Fluent `TextBox` style ships (reworked in .NET 10). WPF has no placeholder property, so add the placeholder in a Slate template.
- Selection: set the AppContext switch `Switch.System.Windows.Controls.Text.UseAdornerForTextboxSelectionRendering` to false, then set `SelectionBrush`, `SelectionTextBrush` and `SelectionOpacity="1"` (gold by default).

Ratings and theme setup: `wpf-feasibility.md`.
