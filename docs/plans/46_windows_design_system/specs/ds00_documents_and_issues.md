# DS-0 — The design's documents agree, the measured results are recorded, and each chunk has an issue

Issues: none of its own; `Refs #1756`. Size S. Lanes: none. `design/` and the plan folder aren't on any lane's path list (`.github/workflows/windows.yml:122-164`). AT: —. Wave 1, slot 1. Depends on: the owner adopting plan 46. Decision: none. Interlock: none.

## Goal

Later chunks read one answer from the design's documents. The measurements plan 46 made are recorded where the next reader of `wpf-feasibility.md` looks. Every chunk has an issue to close, and #1756 says which chunk carries each of its items.

## Current state on main

- `design/windows/components/overlays/Dialog.prompt.md:29` says a 20% scrim. `design/windows/xaml/README.md:145` and `design/windows/winui-review.md:74` say 30%, and `Themes/Slate.Dark.xaml` has `Slate.ScrimColor` at 30%.
- `design/windows/components/patterns/PaneSwitcher.prompt.md` ("Build as") says `TabControl`, and adds that "Narrator announces tabs". `design/windows/wpf-feasibility.md:144` says `ListBox`. The app's rail is a `ListBox` (`MainWindow.xaml:2560`).
- `design/windows/winui-review.md:77` records subtle icon buttons at 40 × 40 with a 30 × 30 fill. `PaneSwitcher.prompt.md`, `PanelToolbar.prompt.md` and `NoteHeader.prompt.md` say 32 px.
- `design/windows/wpf-feasibility.md:60` puts Segoe UI Variable Small under 12px. `design/windows/tokens/typography.css:24` and `design/windows/xaml/README.md:122` use it at 12.
- `design/windows/wpf-feasibility.md:60` says WPF can't drive a variable font's axes, which is true but reads as if embedded variable fonts give one weight. They give their named instances (plan 46 §2.2).
- `design/windows/wpf-feasibility.md:61` says only "Embed them as resources". It doesn't mention the editor's line height, which AvalonEdit has no API for.
- `design/windows/wpf-feasibility.md:152-160` orders the work with text scaling last. Plan 46 §6 moves it up and says why.
- `design/windows/spikes-issue.md:3` and #1756 name no chunk.
- `design/windows/wpf-feasibility.md:89` says Fluent's ring sits 3px out and the design's 1px, and decides `Margin="-1"`. `design/windows/tokens/base.css:7` is `outline:2px solid; outline-offset:1px`, so the ring's inner edge is 1 out and its outer edge 3. A `Rectangle` strokes inside its bounds, so Fluent's `Margin="-3"` (`DefaultFocusVisualStyle.xaml:17`) already draws exactly that. `-1` would put the stroke across the control's border.
- `design/windows/wpf-feasibility.md:103` lists five menu columns. `design/windows/readme.md:176` and `components/navigation/menu-states.card.html` have one leading column and a shared end column.
- `design/windows/readme.md:101` and `winui-review.md:31` give tabs a radius of 6. `components/shell/TabView.prompt.md` gives 8.
- `design/windows/readme.md:141` says Small at 11 and under.
- `design/windows/xaml/README.md:35-42` gates an accent by WCAG and `design/windows/xaml/README.md:44-54` takes its dark fills from Fluent. The app's floor for text is |Lc| > 75 (`ThemeTokenContrastTests.cs:18`), and that table doesn't clear it (plan 46 §2.2). `design/windows/xaml/README.md:56`'s blue isn't what `SystemColors` returns.

## Reference

Plan 46 §2.2 and §2.3. The design's voice: plain sentences, `design/windows/wpf-feasibility.md` as it stands. `design/windows/CLAUDE.md` lists hand fixes in `.jsx` and `_ds_bundle.js`; this chunk touches neither.

## Design

1. `Dialog.prompt.md:29`: 30% scrim.
2. `PaneSwitcher.prompt.md`, "Build as": `ListBox` with a Slate item template, with the reason (the rail's automation contract and its settled selection). The "Narrator announces tabs" line goes. Past-`maxVisible` behaviour follows the owner's DSD8.
3. `winui-review.md:77` and the three pattern prompts: 32 in panel toolbars and the note header, 40 × 40 with the 30 fill in the title bar (DSD6).
4. `wpf-feasibility.md:60` and `readme.md:141`: Small at 12, Text at 14 to 18, Display from 20, matching the tokens. Add that an embedded variable font gives its named instances with no simulation, and that a folder reference (`./#Open Sans`) reaches the italic file.
5. `wpf-feasibility.md:61`: add the composite font file for the editor's 14/22, with the numbers from plan 46 §2.2.
6. `wpf-feasibility.md`, "Suggested order": a sentence pointing at plan 46 §6.
7. `wpf-feasibility.md:89`: the ring is right as Fluent draws it, with the reason above. `readme.md:101`: tabs are 8, from the TabView card. `wpf-feasibility.md:103`: the columns from `readme.md:176`.
8. `xaml/README.md:35`: a sentence saying DS-11 corrects the gate and the table, pending DSD14, with plan 46 §2.2's measurements. `xaml/README.md:56`: the values `SystemColors` returns.
9. Issues. One `gh issue create` per chunk DS-1 to DS-19, labels `windows` and `design` (the triage labels in `docs/agents/triage-labels.md` don't exist on the repo, and `--label` fails outright on a missing one). Title: `windows: <the spec's "what becomes true">`. Body: one sentence, the spec's path, `Refs #1756` where it applies.
10. Comments. On #1756: which chunk carries each item, and that the 200% item is met by captures rather than a rebuilt throwaway app. On #751: DS-10 builds the surface and Appearance. On #752: DS-2 and DS-5 deliver "system font-size respect". Use `--body-file`, and confirm each comment landed by reading it back.

## Tests

None; this is documents. `grep` the edited files for the old wording.

## Docs and registers

The edits above. No contract register rows. No parity change.

## Verification

```powershell
gh issue list --repo coryj627/slate --label design --state open --json number,title
gh issue view 1756 --repo coryj627/slate --comments
```

## Acceptance

- [ ] Every row of plan 46 §2.3 has one answer in the design's documents. The rows waiting on DSD3, DSD9, DSD10, DSD14 and DSD15 are noted as such.
- [ ] One issue per chunk, each naming its spec.
- [ ] #1756, #751 and #752 carry their comments.
- [ ] `git diff --stat` touches only `design/windows/**/*.md`.

## Brief

Read plan 46 §2.2 and §2.3, then edit `design/windows/components/overlays/Dialog.prompt.md`, `components/patterns/PaneSwitcher.prompt.md`, `PanelToolbar.prompt.md`, `NoteHeader.prompt.md`, `winui-review.md`, `wpf-feasibility.md` and `xaml/README.md` as Design says. No `.jsx`, no `_ds_bundle.js`. File the issues with `--label windows --label design`, then comment on #1756, #751 and #752 with `--body-file`. Commit: `docs(windows): the design's documents agree on the scrim, the rail, icon buttons and font sizes`. Tell the owner `/design-sync` can run once this merges.
