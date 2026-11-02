Fluent 2 "Toolbar" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Toolbar } = window.SlateDesignSystem_326dc9;
<Toolbar size="medium" type="static" />
```

Props (from Toolbar.d.ts):
- `size?: "md" | "lg" | "sm"`
- `type?: "static" | "contextual (floating)"`
- `endContent?: boolean`
- `endSearch?: boolean`
- `icon1?: React.ReactNode`
- `icon2?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `ToolBar`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Slate template with the gripper and overflow hidden (`ToolBarTray.IsLocked`). Keeps the UIA ToolBar role.
- Arrow keys move between items: `KeyboardNavigation.TabNavigation="Once"`, `DirectionalNavigation="Cycle"`.
- Icon-only versions need `AutomationProperties.Name`; otherwise Narrator reads the icon glyph.

Ratings and theme setup: `wpf-feasibility.md`.
