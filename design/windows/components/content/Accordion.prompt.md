Fluent 2 "Accordion" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Accordion } = window.SlateDesignSystem_326dc9;
<Accordion size="medium" state="rest" />
```

Props (from Accordion.d.ts):
- `icon?: boolean`
- `focused?: boolean`
- `iconBefore20pxRegular?: React.ReactNode`
- `text?: string`
- `contentSwap?: React.ReactNode`
- `size?: "md" | "lg" | "sm" | "extra large"`
- `state?: "rest" | "disabled"`
- `expanded?: boolean`
- `chevron?: "before" | "after"`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Expander`.
- **Rating:** Native: the Fluent style ships, so override resource keys only.
- The Fluent `Expander` style ships, and its animation was fixed in .NET 10. Exposes the ExpandCollapse pattern.
- Skip the animation when `SystemParameters.ClientAreaAnimation` is false.

Ratings and theme setup: `wpf-feasibility.md`.
