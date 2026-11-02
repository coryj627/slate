Fluent 2 "SplitButton" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { SplitButton } = window.SlateDesignSystem_326dc9;
<SplitButton appearance="primary" state="rest" />
```

Props (from SplitButton.d.ts):
- `label?: string`
- `focus?: boolean`
- `style2?: "primary" | "secondary (default)" | "outline" | "subtle" | "transparent"`
- `state?: "rest" | "hover" | "pressed" | "selected" | "disabled"`
- `size?: "lg" | "medium (default)" | "sm"`
- `layout?: "icon and label (default)" | "icon only"`
- `pxFilled20?: React.ReactNode`
- `pxRegular24?: React.ReactNode`
- `icon?: boolean`
- `pxFilled24?: React.ReactNode`
- `pxRegular20?: React.ReactNode`
- `icon1?: React.ReactNode`
- `icon2?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Button` + `ToggleButton` + `ContextMenu`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- WPF has no split button. Put both parts in one `Grid`. The toggle opens the menu (`PlacementTarget` = the whole control) and exposes the ExpandCollapse pattern.
- Name both parts with `AutomationProperties.Name`.

Ratings and theme setup: `wpf-feasibility.md`.
