Fluent 2 "Spinner" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Spinner } = window.SlateDesignSystem_326dc9;
<Spinner layout="label after" appearance="primary" />
```

Props (from Spinner.d.ts):
- `layout?: "label after" | "label before" | "label above" | "label below"`
- `style2?: "primary (default)" | "subtle"`
- `size?: "extra-tiny" | "tiny" | "extra-small" | "sm" | "md" | "lg" | "extra-large" | "huge"`
- `text1?: string`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `ProgressBar` (`IsIndeterminate`) with an arc template.
- **Rating:** Build: needs code or a custom control.
- WPF has no ProgressRing. Templating `ProgressBar` keeps the UIA ProgressBar role. Animate a rotating `Path` arc.
- The arc uses the accent brushes. Accent brushes are gold by default and switch to the user's Windows accent when they have set a custom one (as Windows gives it, whatever its contrast: plan 46, DSD16).
- Stop the rotation when `SystemParameters.ClientAreaAnimation` is false.

Ratings and theme setup: `wpf-feasibility.md`.
