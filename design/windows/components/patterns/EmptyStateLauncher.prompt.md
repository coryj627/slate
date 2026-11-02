What an empty tab or pane shows: an optional logo, a title, one line of help and up to five primary actions, each with its shortcut.

```jsx
const { EmptyStateLauncher } = window.SlateDesignSystem_326dc9;
<EmptyStateLauncher logo="../../assets/logo/slate-gold.png" title="No note open"
  actions={[
    { label: 'Quick Open…', shortcut: 'Ctrl+O', icon: 'E8E5', onClick: quickOpen },
    { label: 'New Note from Template…', shortcut: 'Ctrl+Shift+N', icon: 'E710' },
    { label: 'Search Vault…', shortcut: 'Ctrl+Shift+F', icon: 'E721' },
  ]} />
```

- Each action is a MenuItem row (32 px, 320 px wide) in a native button: label on the left, chord in the end column, keyboard focus ring, hover and pressed fills.
- Use the same command names and chords as the menus and the command palette, so people learn them once.
- Title is 20/28 semibold (Segoe UI Variable Display); the description is 14/20 secondary text.
- Fills its container and centres the content on `--surface-card`.

## WPF

- **Build as:** `StackPanel` of `Button`s.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Show shortcuts with the same `{cmd:ChordText …}` extension the menus use, so they stay in sync.

Ratings and theme setup: `wpf-feasibility.md`.
