Fluent 2 "MenuSection" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { MenuSection } = window.SlateDesignSystem_326dc9;
<MenuSection state="rest" />
```

Props (from MenuSection.d.ts):
- `divider?: boolean`
- `state?: "rest" | "disabled"`
- `primaryString?: string`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Separator` + header row.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- A titled section header is a `MenuItem` styled as a label with `Focusable="False"` and `IsHitTestVisible="False"`.

Ratings and theme setup: `wpf-feasibility.md`.
