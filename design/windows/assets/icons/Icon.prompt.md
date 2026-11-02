Render any of the Fluent System Icons extracted from the Fluent 2 kit by name — use for every glyph in Slate UI.

```jsx
const { Icon } = window.SlateDesignSystem_326dc9;
<Icon name="SearchSize20ThemeRegular" size={20} style={{ color: 'var(--text-secondary)' }} />
```

Names follow `<Family>Size<px>Theme<Regular|Filled>` — see Icon.d.ts for the full list. Paints with currentColor. Use Regular at rest, Filled for selected/active states (Fluent convention). Glyphs not in the kit (document, add, edit, tag, folder…) come from the same family on CDN: https://unpkg.com/@fluentui/svg-icons/icons/<name>_<size>_regular.svg

## WPF

- **Build as:** `TextBlock` with `FontFamily="{DynamicResource SymbolThemeFontFamily}"` for Segoe Fluent Icons; `Path` for the SVG icons.
- **Rating:** Native: the Fluent style ships, so override resource keys only.
- `SymbolThemeFontFamily` is "Segoe Fluent Icons, Segoe MDL2 Assets", so Windows 10 falls back to MDL2 at the same code points. Reference the resource, not the font name, and don't ship the font file.
- SVG icons from `icon-data.js` are single paths: convert each to `Path.Data` with `Fill` bound to `Foreground`.
- Don't scale icon glyphs with text size.
- Icon-only versions need `AutomationProperties.Name`; otherwise Narrator reads the icon glyph.

Ratings and theme setup: `wpf-feasibility.md`.
