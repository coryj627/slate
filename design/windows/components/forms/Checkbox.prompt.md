Check box rebuilt from the Windows UI kit (20 px box in a 32 px row); use it for on/off options in lists and dialogs.

Use `checked` and `onChange` for a live control, or `status` and `state` to show a fixed state. Glyphs: E73E checked, E9AE indeterminate (the WPF Fluent theme’s).

```jsx
const { Checkbox } = window.SlateDesignSystem_326dc9;
<Checkbox appearance="standard" status="unchecked" />
```

Props (from Checkbox.d.ts):
- `style2?: "standard" | "circular"`
- `status?: "unchecked" | "checked" | "indeterminate"`
- `state?: "rest" | "hover" | "pressed" | "disabled" | "focus"`
- `label?: boolean`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `CheckBox`.
- **Rating:** Native: the Fluent style ships, so override resource keys only.
- The Fluent `CheckBox` style ships. The checked fill uses the accent brushes. Accent brushes are gold by default and switch to the user's Windows accent when they have set a custom one (falling back to gold if it fails contrast).
- Mixed state: `IsThreeState="True"`.

Ratings and theme setup: `wpf-feasibility.md`.
