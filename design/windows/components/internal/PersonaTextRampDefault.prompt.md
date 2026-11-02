Fluent System Icon "PersonaTextRampDefault" from the Fluent 2 Web kit — use for inline glyphs at a fixed size/theme; prefer the shared <Icon name> wrapper (assets/icons) for new work.

```jsx
const { PersonaTextRampDefault } = window.SlateDesignSystem_326dc9;
<PersonaTextRampDefault />
```

Props (from PersonaTextRampDefault.d.ts):
- `tertiaryText?: string`
- `tertiaryTextSlot?: boolean`
- `primaryText?: string`
- `primaryBadgeSlot?: boolean`
- `secondaryBadgeSlot?: boolean`
- `secondaryTextSlot?: boolean`
- `secondaryText?: string`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
