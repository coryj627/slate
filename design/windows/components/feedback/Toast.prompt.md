Fluent 2 "Toast" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { Toast } = window.SlateDesignSystem_326dc9;
<Toast />
```

Props (from Toast.d.ts):
- `pxIcon20?: React.ReactNode`
- `titleString?: string`
- `endContent?: boolean`
- `timestampString?: string`
- `additionalContent?: boolean`
- `quickActions?: boolean`
- `additionalAction?: boolean`
- `secondaryString?: string`
- `tertiaryInformation?: boolean`
- `progressString?: string`
- `tertiaryString?: string`
- `progressBar?: boolean`
- `icon1?: React.ReactNode`
- `icon2?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** In-app overlay `Border`.
- **Rating:** Build: needs code or a custom control.
- Announce with `AutomationProperties.LiveSetting`. Windows notifications don't use these styles.
- Leave it on screen long enough to read, and give it a close button.

Ratings and theme setup: `wpf-feasibility.md`.
