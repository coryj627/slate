# DS-1 — Cascadia Code, Cascadia Mono and Open Sans ship with the app, and the editor is 14/22

Issues: filed by DS-0; `Refs #1756` (its "Spike 2 with Slate's own highlighter" item). Size M. Lanes: windows. AT: NVDA spot. Wave 1, slot 2. Depends on: DS-0. Decision: DSD2. Interlock: `WorkspaceTemplates.xaml` is edited by DS-2 (one census line) and DS-8.

## Goal

The editor draws Cascadia Code at 14/22 and the reading view draws Open Sans on every machine, not only one with Windows Terminal installed. Their licences ship with them. The check #1756 was waiting on, ligatures through Slate's own highlighter, has run.

## Current state on main

- **The editor is Consolas:** `WorkspaceTemplates.xaml:638` `FontFamily="Consolas"`, size from `EditorPreferences.FontSize` at `:639`, default 14 at `EditorInteractions.cs:48`.
- **The reading view has no font.** `ReadingDocumentBuilder.cs:216` and `Reading/ReadingSurface.cs:87-88` set a resource reference to `Slate.ReadingFontFamily`. No dictionary in the repo defines that key (searched), so a `FlowDocument` falls back to the system message font.
- **Code in the reading view** names installed fonts: `new FontFamily("Cascadia Mono, Consolas, monospace")` at `ReadingDocumentBuilder.cs:714`, `:885`, `:1216`, `:1283` and `:1915`. Cascadia Mono is on this desktop, but Windows doesn't ship it.
- **Consolas again:** `Panels/SyncDiagnosticsSurfaceView.cs:420`.
- **No fonts in the project:** `SlateWindows.csproj` has no `Resource` item. The files are in `design/windows/fonts/` (12 Cascadia files, 2 Open Sans), and `readme.md:143` says which job each has.
- **Licences:** `design/windows/fonts/OpenSans-OFL.txt` exists. There is no Cascadia licence in the repo.
- **Layers:** `Themes/Slate.Text.xaml` merges in every appearance (`ThemeManager.cs:224-237`), and resources resolve from `pack://application:,,,/SlateWindows;component/Themes/` (`ThemeManager.cs:28-29`).
- **Lane paths:** `design/` isn't on `windows.yml`'s path list (`.github/workflows/windows.yml:122-164`), so a font change alone wouldn't run the lane. Only the route job uses a sparse checkout (`:221-224`), so app jobs have the files.
- **Glyph typefaces:** nothing in `apps/slate-windows/src` calls `TryGetGlyphTypeface` (searched), which is the one call a composite family answers false to.

## Reference

Measured in plan 46 §2.2, in a throwaway app with `Resource` items linked to `design/windows/fonts/` under `Fonts\`. This composite file gave 22 at 14, with Cascadia's width, ligatures, real bold and italic, and fallback for other scripts:

```xml
<FontFamily xmlns="http://schemas.microsoft.com/winfx/2006/xaml/composite-font"
            xmlns:System="clr-namespace:System;assembly=mscorlib"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            Baseline="1.1324"
            LineSpacing="1.5714">
    <FontFamily.FamilyNames>
        <System:String x:Key="en-US">Slate Editor</System:String>
    </FontFamily.FamilyNames>
    <FontFamily.FamilyMaps>
        <FontFamilyMap Target="./#Cascadia Code" />
    </FontFamily.FamilyMaps>
</FontFamily>
```

`LineSpacing` is 22/14. `Baseline` is Cascadia's own 0.9277 plus half of the added spacing, (1.5714 − 1.1621) / 2, which centres the glyphs in the taller line. `design/windows/tokens/typography.css:7,9,42` are the tokens: `--font-reader`, `--font-editor`, `--editor-size` and `--editor-lh`.

## Design

1. **The files.** Six `Resource` items in `SlateWindows.csproj`, linked per DSD2: `..\..\..\..\design\windows\fonts\CascadiaCode.ttf` with `Link="Fonts\CascadiaCode.ttf"`, and the same for `CascadiaCodeItalic.ttf`, `CascadiaMono.ttf`, `CascadiaMonoItalic.ttf`, `OpenSans-Variable.ttf` and `OpenSans-Italic-Variable.ttf`. That's 3,626,128 bytes. Not the NF or PL files.
2. **The lane.** `- 'design/windows/fonts/**'` in `windows.yml`'s path list, with a comment, and a fact in `ci/windows-runner/tests/Workflows.Tests.ps1`.
3. **The composite.** `SlateWindows/Fonts/SlateEditor.CompositeFont`, as above, as a `Resource` beside the links, so `./#Cascadia Code` resolves against the same folder.
4. **The keys,** in `Themes/Slate.Text.xaml`, so every appearance has them:
   - `Slate.EditorFontFamily`: `pack://application:,,,/SlateWindows;component/Fonts/SlateEditor.CompositeFont#Slate Editor`
   - `Slate.ReadingFontFamily`: `pack://application:,,,/SlateWindows;component/Fonts/#Open Sans, Segoe UI`
   - `Slate.CodeFontFamily`: `pack://application:,,,/SlateWindows;component/Fonts/#Cascadia Mono, Consolas`

   The probe built these in code from a base URI. A first fact proves the same strings resolve from XAML; if the absolute form doesn't, use the `FontFamily(Uri, string)` form in `ThemeManager.DictionariesFor`, as that method already adds code-built values.
5. **The consumers.** `WorkspaceTemplates.xaml:638` becomes `FontFamily="{DynamicResource Slate.EditorFontFamily}"`. `SyncDiagnosticsSurfaceView.cs:420` and the five builder sites take a resource reference to `Slate.CodeFontFamily`, as `ReadingDocumentBuilder.cs:216` already does for the document, with one small helper in the builder. In that file, `:714` is on a `TextBlock`, `:885` and `:1283` on `Paragraph`s, `:1216` on a `Run`, `:1915` on a `TextElement`; all of them have `SetResourceReference`.
6. **Licences.** `design/windows/fonts/Cascadia-OFL.txt`: upstream's `LICENSE` from `microsoft/cascadia-code`, verbatim, including its copyright line and Reserved Font Name. Both OFL files are `Content` items, `CopyToOutputDirectory="PreserveNewest"`, at `Licenses\` in the output, so they travel with the build. W8-3's package picks them up from there. `readme.md`'s Fonts section names the licence.
7. **Spike 2.** Run in the real editor: `SlateTextEditor` with its highlight session on a Markdown document holding `->`, `!=`, `===`, `<=` and a fenced code block. Record the result, with a launched-app capture, in `wpf-feasibility.md:167` and tick #1756's box.

No new package, so the cooldown doesn't apply; the PR names Cascadia v2407.24 (2024-11-27) and Open Sans 3.003 as what's embedded.

## Tests

New `FontPackagingTests.cs` (run `apply-license-header.py` after `git add`), each failing first:

- `TheFontsShipInsideTheApp`: each resource stream opens through `Application.GetResourceStream`.
- `TheLicencesShipBesideIt`: both OFL files exist in the build output, and `Cascadia-OFL.txt` names the Reserved Font Name.
- `TheFamilyKeysResolveInEveryAppearance`: a theory over light, dark and contrast; each key resolves to a `FontFamily` whose faces are real, through `GetTypefaces`.
- `TheEditorIsFourteenOnTwentyTwo`: a hosted `SlateTextEditor` at 14, `TextView.DefaultLineHeight` is 22 within 0.01, and it scales with `FontSize` at 8 and 40. This is what a Consolas editor fails.
- `TheEditorsBoldAndItalicAreRealFaces`: rendered ink in a Bold colorizer run exceeds Regular's by at least 15%, and an italic run differs from a slanted regular. The composite answers false to `TryGetGlyphTypeface`, so this measures ink.
- `TheEditorJoinsOperatorsThroughItsHighlighter`: on a highlighted line holding `->`, `GetIndexedGlyphRuns()` gives a glyph index for `-` different from `GlyphTypeface.CharacterToGlyphMap['-']`. Cascadia's ligatures are contextual alternates, so the glyph count stays the same and the index changes.
- `TheReadingViewDrawsOpenSans`: a hosted `ReadingSurface` under `ThemeManager.DictionariesFor`, `Document.FontFamily` resolves to the Open Sans faces.
- `FontNamesLiveInTheTypeLayer` (census, in `Censuses/`): no `Consolas`, `Cascadia` or `new FontFamily("` under `src/SlateWindows` outside `Themes/` and `Fonts/`, and no `TryGetGlyphTypeface`.

`Workflows.Tests.ps1`: the lane's path list carries the fonts.

Launched app: a capture of `Markdown features` in the editor and in reading mode, and of a note with a fenced code block, at 100% and at 200% if the display is there.

## Docs and registers

`wpf-feasibility.md:61` and `:167`, `readme.md` Fonts, `design/windows/CLAUDE.md` (one decision line: the fonts are linked, the composite is why the editor is 14/22, don't delete it). #1756's Spike 2 box. No parity change.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~FontPackagingTests|FullyQualifiedName~ThemeDesignSystemTests|FullyQualifiedName~ReadingViewTests|FullyQualifiedName~W2EditorInteractionTests"`, then:

```powershell
Invoke-Pester ci/windows-runner/tests/Workflows.Tests.ps1
```

Record a stopwatch number in the PR body: opening a 1 MB note and typing one character at its end, on Consolas (before) and on the composite (after). There's no editor benchmark until W8-5. A clear slowdown goes to the owner rather than merging.

AT: NVDA, char and line reading across `->` and `!=` in the editor, and a line in the reading view. Text comes from `TextPattern`, not glyphs, so the expectation is no change. Record the phrases.

## Acceptance

- [ ] The facts above were seen failing, then passing.
- [ ] On a machine or VM without Cascadia installed, the editor still draws it. The job VM's capture from the gate is enough if the image has none; otherwise say so under `not delivered`.
- [ ] Spike 2 is recorded and #1756's box is ticked.
- [ ] Both licences are in the build output.

## Brief

Read plan 46 §2.2, `WorkspaceTemplates.xaml:626-645`, `ReadingDocumentBuilder.cs:208-216` and the five `FontFamily` sites, `ThemeManager.cs:204-238`. Add the six `Resource` links and the composite under `Fonts\`, the three keys to `Themes/Slate.Text.xaml`, the consumers, the Cascadia licence and both `Content` items, and the `windows.yml` path with its Pester fact. Write the facts first. Run plan 46 §7's loop, the NVDA spot, and the launched captures. Update `wpf-feasibility.md`, `readme.md`, `design/windows/CLAUDE.md` and #1756. Commit: `feat(windows): the editor and reading view draw the design's fonts, shipped with the app`.
