Fluent 2 "Label" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Label } = window.SlateDesignSystem_326dc9;
<Label type="regular (default)" size="small" />
```

Props (from Label.d.ts):
- `required?: boolean`
- `type?: "regular (default)" | "semibold"`
- `size?: "sm" | "md" | "lg"`
- `disabled?: "off" | "on"`
- `text?: string`
- `text1?: string`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Label`.
- **Rating:** Native: the Fluent style ships, so override resource keys only.
- The Fluent `Label` style ships in .NET 10. Set `Target` and use `_` for the access key.

Ratings and theme setup: `wpf-feasibility.md`.
