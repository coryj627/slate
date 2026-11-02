Fluent 2 "DataGridCellMedium" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { DataGridCellMedium } = window.SlateDesignSystem_326dc9;
<DataGridCellMedium layout="cell actions" appearance="none" />
```

Props (from DataGridCellMedium.d.ts):
- `layout?: "cell actions" | "link" | "multi-select" | "single select" | "text" | "swappable"`
- `style2?: "none" | "primary" | "secondary"`
- `focus?: boolean`
- `avatar32px?: boolean`
- `content?: string`
- `productIcon?: boolean`
- `productIcon32px?: React.ReactNode`
- `systemIcon?: boolean`
- `icon20pxReg?: React.ReactNode`
- `icon1?: React.ReactNode`
- `icon2?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `DataGridCell`.
- **Rating:** Native: the Fluent style ships, so override resource keys only.
- The Fluent `DataGrid` style ships. Set the size with `DataGrid.RowHeight`; the cell style adds Slate padding.

Ratings and theme setup: `wpf-feasibility.md`.
