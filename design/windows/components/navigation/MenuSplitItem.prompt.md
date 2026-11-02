Fluent 2 "MenuSplitItem" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { MenuSplitItem } = window.SlateDesignSystem_326dc9;
<MenuSplitItem sectionHeader="false" state="rest" />
```

Props (from MenuSplitItem.d.ts):
- `checkmark?: boolean`
- `selected?: boolean`
- `sectionHeader?: "false"`
- `state?: "rest" | "rest (checked only)" | "hover" | "pressed" | "selected" | "disabled" | "disabled (checked only)"`
- `endContent?: boolean`
- `endContentString?: string`
- `focus?: boolean`
- `icon?: boolean`
- `pxRegular20?: React.ReactNode`
- `pxFilled20?: React.ReactNode`
- `secondaryText?: boolean`
- `primaryString?: string`
- `secondaryString?: string`
- `icon1?: React.ReactNode`
- `icon2?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `MenuItem` with two hit areas.
- **Rating:** Build: needs code or a custom control.
- WPF has no split menu item. The label runs the command; the chevron (and the Right arrow key) opens the submenu.

Ratings and theme setup: `wpf-feasibility.md`.
