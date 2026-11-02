# Validate the design system against WPF (six spikes)

All six ran on 2 November 2026 and none failed. The results are in `wpf-feasibility.md` under "Spikes to run first". Of the steps under "When all six are recorded", only syncing the design project is still to do (#1756).

Paste this as a GitHub issue on coryj627/slate, or hand it to Claude Code in the repo.

Context: `design/windows/wpf-feasibility.md` rates every part of the Windows design system as Native, Template, Build or Change the design. Six ratings rest on assumptions that only a running WPF app can confirm. Each spike is a throwaway check in `apps/slate-windows` (a scratch window or a test page is fine; nothing ships). Record the result in `wpf-feasibility.md` under "Spikes to run first" (Pass / Fail, build, Windows version, one screenshot) and apply the listed follow-up on failure.

## Checklist

- [x] **1. Segoe UI Variable families.** Log `Fonts.SystemFontFamilies` and confirm `Segoe UI Variable Small`, `Segoe UI Variable Text` and `Segoe UI Variable Display` are listed on Windows 11. Render one `TextBlock` per family at 12, 14 and 28 px, weights 400 and 600.
  - Expected: three families listed; weights render without synthetic bolding.
  - On failure: set `FontFamily="Segoe UI"` everywhere, note it under Type in `wpf-feasibility.md` and `readme.md`, and keep the shipped TTFs for designs only.

- [x] **2. Cascadia Code ligatures in AvalonEdit.** Open a Markdown file with `->`, `!=`, `===`, `<=` and a fenced code block, with syntax colouring on.
  - Expected: ligatures join inside one colour run and break only at colour boundaries.
  - On failure: default the editor to Cascadia Mono, keep Cascadia Code as an opt-in setting, update `--font-editor` in `tokens/typography.css` and the Editor type card.

- [x] **3. Snap Layouts on a custom maximize button.** A window with `WindowChrome` (`CaptionHeight="48"`, `UseAeroCaptionButtons="False"`) and an `HwndSource` hook returning `HTMAXBUTTON` over the maximize button. Hover for one second; also check hover and pressed visuals and Win+Z.
  - Expected: the Snap Layouts flyout opens; the button still shows hover and pressed.
  - On failure: fall back to `UseAeroCaptionButtons="True"` with native caption buttons and drop `CaptionButton` from the title bar spec (`readme.md` Window chrome, `components/shell`).

- [x] **4. ClearType inside popups.** A `Popup` with `AllowsTransparency="True"`, an opaque `Border` with `RenderOptions.ClearTypeHint="Enabled"`, and a `DropShadowEffect` on a sibling background-only `Border`. Compare the popup text with a normal `TextBlock` at 400% magnifier zoom.
  - Expected: colour-fringed (ClearType) text in both.
  - On failure: accept greyscale text in menus and tooltips, and note it under Shadows in `wpf-feasibility.md`.

- [x] **5. Shell at 225% text size.** Windows Settings › Accessibility › Text size at 225%, with the shell heights bound to `MinHeight`. Screenshot the title bar, tab strip, status bar and a menu.
  - Expected: rows grow, nothing clips, caption buttons stay 46 × 48.
  - On failure: record which rows clip; adjust `--titlebar-min-height`, `--tabstrip-min-height`, `--statusbar-min-height` in `tokens/spacing.css` and the matching XAML.

- [x] **6. Non-adorner selection with RTL text.** Set `Switch.System.Windows.Controls.Text.UseAdornerForTextboxSelectionRendering=false`, `SelectionBrush`, `SelectionTextBrush`, `SelectionOpacity="1"`. Select across a Hebrew or Arabic run mixed with Latin, in a TextBox and in AvalonEdit.
  - Expected: solid pale-gold selection with dark text, no exception (dotnet/wpf#2314 was a .NET Framework 4.8 crash).
  - On failure: keep adorner selection, set `SelectionOpacity` to 0.4 over gold, and change `--selection-bg` in `tokens/colors.css` to the composited colour.

## When all six are recorded

- Update the Ratings table and "Short answer" in `wpf-feasibility.md`.
- Copy `xaml/Slate.Light.xaml` and `Slate.Dark.xaml` into `apps/slate-windows/src/SlateWindows/Themes/` and add the Body Large text styles from `xaml/README.md`.
- Commit, then sync the design project from `github.md`.
