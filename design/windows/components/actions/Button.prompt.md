Fluent 2 "Button" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Button } = window.SlateDesignSystem_326dc9;
<Button appearance="primary" state="rest" />
```

Props (from Button.d.ts):
- `style2?: "primary" | "secondary (default)" | "outline" | "subtle" | "transparent"`
- `state?: "rest" | "hover" | "pressed" | "selected" | "disabled"`
- `size?: "lg" | "medium (default)" | "sm"`
- `layout?: "icon and label (default)" | "icon only"`
- `xOutline24?: React.ReactNode`
- `menuButton?: boolean`
- `xOutline20?: React.ReactNode`
- `xFilled20?: React.ReactNode`
- `label?: string`
- `focus?: boolean`
- `icon?: boolean`
- `xFilled24?: React.ReactNode`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

- `appearance="subtle" layout="icon only"` at medium size follows the Windows UI kit: a 40 × 40 target with a 30 × 30 fill and a 16 px glyph. Other variants are the Fluent 2 Web design.
- Heights are small 24, medium 32 and large 40, borders included, for every appearance. Icon and label buttons are the same height as text-only ones.

## WPF

- **Build as:** `Button`.
- **Rating:** Native: the Fluent style ships, so override resource keys only.
- Primary is `AccentButtonStyle`. Subtle and transparent are Slate styles `BasedOn` the Fluent button.
- Accent brushes are gold by default and switch to the user's Windows accent when they have set a custom one (falling back to gold if it fails contrast).
- Icon-only versions need `AutomationProperties.Name`; otherwise Narrator reads the icon glyph.

Ratings and theme setup: `wpf-feasibility.md`.
