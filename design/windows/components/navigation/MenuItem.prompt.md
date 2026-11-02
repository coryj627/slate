Menu row rebuilt from the Windows UI kit: 40 px pitch, 34 px highlight inset 5 px (radius 3), label 16 px from the edge; use inside MenuMenu or a popup menu.

```jsx
const { MenuItem } = window.SlateDesignSystem_326dc9;
<MenuItem state="rest" />
```

Props (from MenuItem.d.ts):
- `checkmark?: boolean`
- `selected?: boolean`
- `endContent?: boolean`
- `endContentString?: string`
- `chevron?: boolean`
- `switch?: boolean`
- `focus?: boolean`
- `icon?: boolean`
- `pxRegular20?: React.ReactNode`
- `state?: "rest" | "rest (checked only)" | "hover" | "pressed" | "selected" | "disabled" | "disabled (checked only)"`
- `pxFilled20?: React.ReactNode`
- `secondaryText?: boolean`
- `primaryString?: string`
- `secondaryString?: string`
- `icon1?: React.ReactNode`
- `icon2?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `MenuItem`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- One Slate template for all four roles (top-level header and item, submenu header and item) with the same columns (check, icon, label, shortcut, chevron) via `SharedSizeGroup`. This fixes the Fluent template, where submenu rows have no check column.
- Shortcut text uses secondary text, not `TextFillColorDisabledBrush`. Keep `InputGestureText` and the `AcceleratorKey` binding.

Ratings and theme setup: `wpf-feasibility.md`.
