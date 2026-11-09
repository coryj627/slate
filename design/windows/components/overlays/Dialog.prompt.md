Fluent 2 "Dialog" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Dialog } = window.SlateDesignSystem_326dc9;
<Dialog size="600px" layout="text" />
```

Props (from Dialog.d.ts):
- `footer?: boolean`
- `headerText?: string`
- `secondaryButton?: boolean`
- `tertiaryButton?: boolean`
- `checkbox?: boolean`
- `dismiss?: boolean`
- `size?: "600px" | "320px"`
- `layout?: "text" | "placeholder"`
- `contentSwap?: React.ReactNode`
- `body?: boolean`
- `bodyText?: string`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** Owned `Window` (`ShowDialog`) or an in-window overlay.
- **Rating:** Build: needs code or a custom control.
- In-window: 30% scrim, `KeyboardNavigation.TabNavigation="Cycle"`, `AutomationProperties.IsDialog`, Esc closes and focus returns to the trigger.
- Radius 8, shadow 64 (one WPF shadow, `Direction="270"`).

Ratings and theme setup: `wpf-feasibility.md`.
