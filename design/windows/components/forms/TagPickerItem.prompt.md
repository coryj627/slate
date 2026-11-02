Fluent 2 "TagPickerItem" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { TagPickerItem } = window.SlateDesignSystem_326dc9;
<TagPickerItem state="rest" size="small" />
```

Props (from TagPickerItem.d.ts):
- `state?: "rest" | "hover" | "pressed"`
- `size?: "sm" | "medium (default)"`
- `selection?: boolean`
- `media?: boolean`
- `iconBefore?: boolean`
- `primaryString?: string`
- `secondaryContent?: boolean`
- `secondaryString?: string`
- `endContent?: boolean`
- `focus?: boolean`
- `icon3?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Button` (dismissible tag).
- **Rating:** Build: needs code or a custom control.
- Part of TagPicker. Remove buttons are named "Remove <tag>".

Ratings and theme setup: `wpf-feasibility.md`.
