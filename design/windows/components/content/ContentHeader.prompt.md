Fluent 2 "ContentHeader" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { ContentHeader } = window.SlateDesignSystem_326dc9;
<ContentHeader layout="selection" appearance="regular" />
```

Props (from ContentHeader.d.ts):
- `layout?: "selection" | "content"`
- `style2?: "regular" | "semibold"`
- `sorting?: boolean`
- `focus?: boolean`
- `headerContent?: string`
- `icon2?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Grid`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Title `TextBlock` plus actions.

Ratings and theme setup: `wpf-feasibility.md`.
