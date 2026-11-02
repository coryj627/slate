Fluent 2 "MessageBar" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { MessageBar } = window.SlateDesignSystem_326dc9;
<MessageBar layout="single line (default)" />
```

Props (from MessageBar.d.ts):
- `textString?: string`
- `actions?: boolean`
- `layout?: "single line (default)" | "multi line"`
- `dismiss?: boolean`
- `additionalAction?: boolean`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `Border` + icon + text + buttons.
- **Rating:** Build: needs code or a custom control.
- WPF has no InfoBar. Announce it with `AutomationProperties.LiveSetting` and raise `LiveRegionChanged` when it appears.

Ratings and theme setup: `wpf-feasibility.md`.
