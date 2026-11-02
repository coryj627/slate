Fluent 2 "InfoButton" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { InfoButton } = window.SlateDesignSystem_326dc9;
<InfoButton size="small" state="rest" />
```

Props (from InfoButton.d.ts):
- `size?: "sm" | "md" | "lg"`
- `state?: "rest" | "hover" | "pressed" | "selected (popover open)" | "focus"`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Button` + `Popup`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Opens on click and Enter, closes on Esc and returns focus to the button.
- Icon-only versions need `AutomationProperties.Name`; otherwise Narrator reads the icon glyph.

Ratings and theme setup: `wpf-feasibility.md`.
