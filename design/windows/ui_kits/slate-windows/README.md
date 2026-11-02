# Slate for Windows — UI kit

Interactive recreation of the Slate WPF workspace (`index.html`, 1280×800). It is built from the design-system components, including the Slate patterns in `components/patterns/`.

- `Kit.jsx`: icon helper (`SIcon`, Segoe Fluent Icons glyphs), `IconBtn` (DS `Button`, subtle icon-only) and `Kbd`, plus the demo vault, palette commands, the 17 right-pane views (`SLATE_PANELS`) and the sidebar sort modes (`SLATE_SORT`).
- `Chrome.jsx`: `TitleBar` is one 48px row with the logo app menu (`AppMenu`, cascading `MenuItem` lists from MainWindow.xaml, Alt+F), the window title as drag region, the palette and settings buttons, and caption buttons. The row has no theme button: theme is in Settings › Appearance and the palette's Switch Theme…. Shell heights are minimums (`--titlebar-min-height` etc.) so they grow with the Windows text size. `StatusBar` has the Files-sidebar toggle in the far-left corner and the right-pane toggle in the far-right corner.
- `Sidebar.jsx`: the Files sidebar. `PanelToolbar` has New note, Sort and group (six sort modes plus Group by date) and collapse/expand all. Below it are the filter, Shortcuts, Recents, the folder tree with date and task metadata and a pin badge, and Tags as `CountListRow`s.
- `Document.jsx`: `NoteTabs` (tab strip), `EditorView` (Cascadia source with front matter) and `ReaderView` (Open Sans, collapsible Properties, embedded note card, math, code card, broken wikilink).
- `Panes.jsx`: `RightPane` (`PaneSwitcher` over the 17 views with More panels, a `PanelToolbar` per view, and Backlinks as expandable `CountListRow`s) and `CommandPalette` (Switch Theme… opens a list of Light, Dark and Use Windows setting with the current one checked).
- `SettingsWindow.jsx`: `SettingsWindow` (`SettingsLayout` + `SettingRow`) over Slate's preferences: Appearance (Theme, Accent color), Editor, Math, Files sidebar and History. The kit always shows Slate gold; picking Windows accent doesn't preview it.
- `index.html`: `NoteHeader` with back/forward history and the ⋯ menu, and `EmptyStateLauncher` once every tab is closed.

Try: click files in the sidebar, type in the filter, Ctrl+Shift+P (palette), Ctrl+Shift+E (reading/source), Ctrl+Alt+I (right pane), the logo menu (or Alt+F, then the arrow keys), the right-pane icons and ⋯ More panels, Sort and group in the sidebar, the settings button (Appearance › Theme), Switch Theme… in the palette, and close every tab. The theme starts on Use Windows setting.

Right-pane views other than Backlinks, Outgoing links, Outline and Citations say they aren't recreated. The app-specific surfaces (editor, reader, sidebar rows) are hand-built from tokens because the Fluent kit has no equivalents. The real app hasn't shipped a Windows UI with this gold theme yet. Layouts follow the structure of MainWindow.xaml and the macOS help docs.
