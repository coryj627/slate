Fluent System Icon "Persona" from the Fluent 2 Web kit — use for inline glyphs at a fixed size/theme; prefer the shared <Icon name> wrapper (assets/icons) for new work.

```jsx
const { Persona } = window.SlateDesignSystem_326dc9;
<Persona layout="text after (default)" appearance="avatar" />
```

Props (from Persona.d.ts):
- `primaryString?: string`
- `subtext?: boolean`
- `tertiaryString?: string`
- `secondaryString?: string`
- `quaternaryText?: boolean`
- `tertiaryText?: boolean`
- `quaternaryString?: string`
- `layout?: "text after (default)" | "text before (avatar only)" | "text below (avatar only)"`
- `style2?: "avatar (default)" | "presence badge"`
- `size?: "small (badge only)" | "medium (default)" | "lg"`
- `alignment?: "center (default)" | "top"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** Avatar + `StackPanel` of `TextBlock`s.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Expose name and secondary text as one `AutomationProperties.Name` on the container.

Ratings and theme setup: `wpf-feasibility.md`.
