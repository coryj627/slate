Fluent 2 "AnimatedProgressBar" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { AnimatedProgressBar } = window.SlateDesignSystem_326dc9;
<AnimatedProgressBar appearance="indeterminate" size="medium" />
```

Props (from AnimatedProgressBar.d.ts):
- `style2?: "indeterminate" | "determinate"`
- `size?: "medium (default)" | "lg"`
- `state?: "default" | "success" | "error" | "warning"`
- `status?: "not started" | "in progress" | "complete" | "after" | "before" | "gradient after" | "gradient before" | "99% complete"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `ProgressBar` with `IsIndeterminate="True"`.
- **Rating:** Native: the Fluent style ships, so override resource keys only.
- The Fluent `ProgressBar` style ships. The fill uses the accent brushes. Accent brushes are gold by default and switch to the user's Windows accent when they have set a custom one (as Windows gives it, whatever its contrast: plan 46, DSD16).
- Show a static bar when `SystemParameters.ClientAreaAnimation` is false.

Ratings and theme setup: `wpf-feasibility.md`.
