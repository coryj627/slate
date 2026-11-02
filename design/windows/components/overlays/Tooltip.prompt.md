Fluent 2 "Tooltip" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Tooltip } = window.SlateDesignSystem_326dc9;
<Tooltip appearance="default" />
```

Props (from Tooltip.d.ts):
- `style2?: "default" | "brand" | "inverted"`
- `label?: string`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `ToolTip`.
- **Rating:** Native: the Fluent style ships, so override resource keys only.
- The Fluent `ToolTip` style ships. WPF opens tooltips on keyboard focus; Ctrl+Shift+F10 toggles, Esc or Ctrl closes. Turn that off per control with `ToolTipService.ShowsToolTipOnKeyboardFocus`.
- A tooltip isn't the accessible name; also set `AutomationProperties.Name`. ClearType needs `RenderOptions.ClearTypeHint="Enabled"`.

Ratings and theme setup: `wpf-feasibility.md`.
