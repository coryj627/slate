# Decisions from the WPF review form (2026-11-01, rounds 1–10)

Design-system changes to make (all four picked):
1. Shadows: one WPF-ready shadow per level (2, 8, 16, 28, 64) with XAML values (Direction 270; elevation card + tokens).
2. Remove "optional Mica" from Visual foundations; mark Material acrylic web-only.
3. Shell heights become minimums that grow with text size — up to 225% (Windows max).
4. Add a WPF note (control + template) to each component's prompt.md — ALL public components (not internal/).

Focus ring offset: keep 1px; change the WPF focus style (Margin -1) to match.
XAML theme dictionaries: YES, generated from tokens, stored IN THIS DESIGN SYSTEM (not repo drop-in).

Accent rules:
- Gold everywhere by default: selected-row pill + tab underline, text selection, links, primary buttons, AND check boxes, radios, switches, sliders, progress bars.
- If the user has a custom Windows accent (not the default), Slate follows it — EVERYTHING that's gold switches to their accent.
- Detection: Both — automatic, with a setting to override.
- If the custom accent fails contrast: fall back to gold, silently (no note).
- Mockup stand-in for Windows accent: Windows default blue (#005FB8 light, #60CDFF dark) — now only relevant for showing the "Windows accent" option; default is gold.
- No accent preview tweak in mockups.

Settings / Appearance:
- New Appearance page in Settings: theme + accent together.
- Theme choices: Light, Dark, Use Windows setting. Default for new installs: Use Windows setting.
- Accent colour choices worded: Automatic · Slate gold · Windows accent.
- Remove the title-bar light/dark button; leave its space empty (pane toggles stay in status bar).
- Theme also reachable from the command palette: "Switch theme" opens a list of the 3 choices.

Order of work: 1) WPF notes on every component (FIRST), then XAML theme files + accent rules, Workspace kit (Appearance page, title bar, palette), shadows/Mica/text-scaling.
