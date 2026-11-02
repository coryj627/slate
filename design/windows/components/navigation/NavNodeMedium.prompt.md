Navigation item rebuilt from the Windows UI kit navigation view: 40 px pitch, 34 px inset highlight, 16 px icon, gold pill when selected; use for side navigation such as Settings categories.

```jsx
const { NavNodeMedium } = window.SlateDesignSystem_326dc9;
<NavNodeMedium state="hover" />
```

Props (from NavNodeMedium.d.ts):
- `subItem?: React.ReactNode`
- `content?: string`
- `group?: boolean`
- `selected?: boolean`
- `state?: "hover" | "pressed" | "rest"`
- `open?: boolean`
- `secondaryActions?: boolean`
- `iconBeforeRegular?: React.ReactNode`
- `iconBeforeFilled?: React.ReactNode`
- `focus?: boolean`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `TreeViewItem`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- See Nav. Expanders expose ExpandCollapse.

Ratings and theme setup: `wpf-feasibility.md`.
