Menu flyout surface from the Windows UI kit (radius 8, 2 px vertical padding); pass MenuItems and separators as children.

```jsx
const { MenuMenu } = window.SlateDesignSystem_326dc9;
<MenuMenu />
```

Props (from MenuMenu.d.ts):
- `customMenu?: boolean`
- `instance?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `ContextMenu` / submenu popup.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Radius 8 and the Slate shadow. Set `Grid.IsSharedSizeScope` on the item panel, and `RenderOptions.ClearTypeHint="Enabled"` on the opaque surface (the popup is a layered window).

Ratings and theme setup: `wpf-feasibility.md`.
