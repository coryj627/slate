The body of the Settings window: categories on the left, the open category's title, description and setting rows on the right. Pair it with SettingRow.

```jsx
const { SettingsLayout, SettingRow, Switch } = window.SlateDesignSystem_326dc9;
<SettingsLayout value={cat} onChange={setCat} query={q} onQueryChange={setQ}
  categories={[
    { id: 'editor', label: 'Editor', icon: 'E70F' },
    { id: 'reading', label: 'Reading', icon: 'E736' },
    { id: 'math', label: 'Math', icon: 'E8EF' },
  ]}
  description="How Slate reads math aloud and in braille.">
  <SettingRow label="Speech style" description="ClearSpeak or SimpleSpeak">{select}</SettingRow>
</SettingsLayout>
```

- Categories are Fluent VerticalTabs (subtle, medium) with a 20 px icon. Up/Down move and open, Home/End jump, and only the open category is a tab stop.
- The search field appears when you pass `onQueryChange`. Filtering is up to you.
- The page title is 20/28 semibold. The description is 14/20 secondary, up to 560 px wide. Rows are stacked 4 px apart.
- Background is `--surface-window` and rows are `--surface-card`, the same layering as Windows 11 Settings.
- Group headings between rows: `<h3 style={{ margin: '16px 0 4px', font: '600 14px/20px var(--font-ui)' }}>`.
- **Appearance** is the first category (icon `E790`). It holds **Theme** (`Light` · `Dark` · `Use Windows setting`, default `Use Windows setting`) and **Accent color** (`Automatic` · `Slate gold` · `Windows accent`, default `Automatic`, description "Automatic uses Slate gold unless you’ve chosen an accent color in Windows."). Both are dropdowns. The accent rules are in `xaml/README.md`.

## WPF

- **Build as:** `Grid`: `ListBox` + `ScrollViewer`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Category list on the left, search box, scrolling page. WPF has no NavigationView.

Ratings and theme setup: `wpf-feasibility.md`.
