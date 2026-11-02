repo: coryj627/slate
branch: main
path: apps/slate-windows
design_copy: design/windows

## Last sync
date: 2026-11-01T23:36:17Z

### Updated in this project
- `xaml/Slate.Light.xaml` and `Slate.Dark.xaml` generated from the tokens, keeping every key in the app's theme files
- Accent rules and the Appearance settings (Theme, Accent color) built around `ThemeManager.cs`
- Fluent brush keys read from dotnet/wpf (`PresentationFramework.Fluent/Resources/Theme/Light.xaml`, `Dark.xaml`)

## Screen map
| Screen | Repo files |
| --- | --- |
| tokens/colors.css | apps/slate-windows/src/SlateWindows/Themes/Slate.Light.xaml, Slate.Dark.xaml, Slate.Contrast.xaml, ThemeManager.cs |
| xaml/Slate.Light.xaml, xaml/Slate.Dark.xaml, xaml/README.md | apps/slate-windows/src/SlateWindows/Themes/Slate.Light.xaml, Slate.Dark.xaml, ThemeManager.cs |
| ui_kits/slate-windows (menu bar) | apps/slate-windows/src/SlateWindows/MainWindow.xaml |
| ui_kits/slate-windows (Files sidebar) | docs/help/sidebar.md, apps/slate-windows/src/SlateWindows/SidebarSettingsStore.cs |
| ui_kits/slate-windows (reader/editor note) | demo-vault/learning/Linear algebra lecture 3.md |
| ui_kits/slate-windows (Settings window) | apps/slate-windows/src/SlateWindows/AppPreferencesStore.cs, EditorInteractions.cs, SidebarSettingsStore.cs, ThemeManager.cs |
| components/patterns (settings card) | apps/slate-windows/src/SlateWindows/AppPreferencesStore.cs, EditorInteractions.cs, ThemeManager.cs |
| wpf-feasibility.md | apps/slate-windows/src/SlateWindows/App.xaml.cs, ThemeManager.cs, Themes/Slate.*.xaml, MainWindow.xaml, SlateWindows.csproj, design/windows/wpf-feasibility.md |
| spikes-issue.md | design/windows/spikes-issue.md |
| xaml/ | design/windows/xaml/ |
| tokens/ | design/windows/tokens/ |
| readme.md, review-2026-11.md, winui-review.md | design/windows/readme.md, design/windows/review-2026-11.md, design/windows/winui-review.md |

## Sync history
- 2026-11-01T14:43:16Z: WPF feasibility report from the app's theme setup, Slate theme keys and MainWindow.xaml
- 2026-11-01T05:46:27Z: settings mock, Files sidebar sort modes, right-pane view list, colour tokens, menus and chords
- 2026-11-01T01:55:20Z: colour tokens, menu bar, Files sidebar, demo note
