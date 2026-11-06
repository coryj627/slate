Fluent 2 "Switch" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Switch } = window.SlateDesignSystem_326dc9;
<Switch layout="switch" state="rest" />
```

Props (from Switch.d.ts):
- `layout?: "switch" | "switch+label after" | "switch+label before" | "switch+label above"`
- `checked?: boolean`
- `state?: "rest" | "hover" | "pressed" | "focus" | "disabled"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `CheckBox` with a toggle-switch template.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- WPF's Fluent theme has no toggle switch. `CheckBox` keeps the Toggle pattern and the Space key.
- The on-state fill uses the accent brushes. Accent brushes are gold by default and switch to the user's Windows accent when they have set a custom one (as Windows gives it, whatever its contrast: plan 46, DSD16).

Ratings and theme setup: `wpf-feasibility.md`.
