Fluent 2 "Field" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Field } = window.SlateDesignSystem_326dc9;
<Field size="medium" />
```

Props (from Field.d.ts):
- `size?: "medium (default)" | "sm" | "lg"`
- `instance?: React.ReactNode`
- `helperText?: boolean`
- `statusText?: boolean`
- `label?: boolean`
- `text1?: string`
- `text2?: string`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Grid` with label, control and message.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Link the control to its label with `AutomationProperties.LabeledBy`, and put the hint in `HelpText`. Show errors with `Validation.ErrorTemplate`.

Ratings and theme setup: `wpf-feasibility.md`.
