Fluent 2 "ListItem" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { ListItem } = window.SlateDesignSystem_326dc9;
<ListItem state="rest" type="single select" />
```

Props (from ListItem.d.ts):
- `checkmark?: boolean`
- `state?: "rest" | "hover" | "pressed" | "disabled" | "focus"`
- `type?: "single select" | "multi select" | "header"`
- `text?: string`
- `icon?: boolean`
- `icon1?: React.ReactNode`
- `icon2?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `ListBoxItem` / `ListViewItem`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Hover overlay and, when selected, the pale gold fill plus the 3px gold pill.

Ratings and theme setup: `wpf-feasibility.md`.
