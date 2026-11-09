Radio button rebuilt from the Windows UI kit (20 px circle in a 32 px row); use it for one choice from a short list, grouped by `name`.

Checked shows an 8 px dot (10 px on hover, 6 px pressed).

```jsx
const { Radio } = window.SlateDesignSystem_326dc9;
<Radio layout="icon only" checked="on" />
```

Props (from Radio.d.ts):
- `layout?: "icon only" | "icon+label after" | "icon+label below"`
- `checked?: "on" | "off"`
- `state?: "rest" | "hover" | "pressed" | "disabled" | "focus"`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `RadioButton`.
- **Rating:** Native: the Fluent style ships, so override resource keys only.
- The Fluent `RadioButton` style ships. The selected dot uses the accent brushes. Accent brushes are gold by default and switch to the user's Windows accent when they have set a custom one (as Windows gives it, whatever its contrast: plan 46, DSD16).

Ratings and theme setup: `wpf-feasibility.md`.
