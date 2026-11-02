Fluent 2 "SpinButton" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { SpinButton } = window.SlateDesignSystem_326dc9;
<SpinButton appearance="outline" state="rest" />
```

Props (from SpinButton.d.ts):
- `style2?: "outline (default)" | "filled darker" | "filled lighter" | "underline"`
- `state?: "rest" | "hover" | "pressed" | "selected" | "error" | "disabled" | "read only"`
- `size?: "medium (default)" | "sm"`
- `icon2?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `TextBox` + two `RepeatButton`s.
- **Rating:** Build: needs code or a custom control.
- Up and Down arrow keys change the value. Expose the RangeValue pattern with a custom `AutomationPeer`.

Ratings and theme setup: `wpf-feasibility.md`.
