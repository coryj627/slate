Fluent 2 "TagPickerTagPicker" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { TagPickerTagPicker } = window.SlateDesignSystem_326dc9;
<TagPickerTagPicker appearance="outline" size="medium" />
```

Props (from TagPickerTagPicker.d.ts):
- `tags?: boolean`
- `style2?: "outline" | "transparent" | "filled darker" | "filled lighter"`
- `size?: "medium (default)" | "lg" | "extra large"`
- `state?: "rest" | "hover" | "pressed" | "focused" | "disabled"`
- `expanded?: boolean`
- `secondaryAction?: boolean`
- `icon1?: React.ReactNode`
- `icon2?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** Custom control: `TextBox` + tag list + `Popup`.
- **Rating:** Build: needs code or a custom control.
- The heaviest UI Automation work in the set: the selected tags, the text field and the suggestion list each need correct roles, and Backspace removes the last tag.

Ratings and theme setup: `wpf-feasibility.md`.
