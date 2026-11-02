Fluent 2 "TagPickerSecondaryAction" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { TagPickerSecondaryAction } = window.SlateDesignSystem_326dc9;
<TagPickerSecondaryAction state="rest" />
```

Props (from TagPickerSecondaryAction.d.ts):
- `state?: "rest" | "hover" | "pressed"`
- `secondaryString?: string`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
