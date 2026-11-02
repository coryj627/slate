Fluent 2 "SearchBox" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { SearchBox } = window.SlateDesignSystem_326dc9;
<SearchBox appearance="filled darker" size="small" />
```

Props (from SearchBox.d.ts):
- `style2?: "filled darker (default)" | "filled lighter" | "outline" | "transparent"`
- `size?: "sm" | "medium (default)" | "lg"`
- `state?: "rest" | "focus" | "hover"`
- `iconAfter24pxRegular?: React.ReactNode`
- `focusActions?: boolean`
- `iconAfter12pxRegular?: React.ReactNode`
- `iconAfter16pxRegular?: React.ReactNode`
- `secondaryAction?: boolean`
- `iconAfter212pxRegular?: React.ReactNode`
- `iconAfter216pxRegular?: React.ReactNode`
- `iconAfter224pxRegular?: React.ReactNode`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `TextBox` with a Slate template.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Search glyph E721 and a clear button. Name the clear button: the Fluent clear button's private-use glyph was being read as its name (Slate #1106).

Ratings and theme setup: `wpf-feasibility.md`.
