# X3 executable spec — Settings, snippet manager, docs + close-out

Issues: X3-1 ([#586](https://github.com/coryj627/slate/issues/586)) · X3-2 ([#595](https://github.com/coryj627/slate/issues/595)) · X3-3 ([#596](https://github.com/coryj627/slate/issues/596)).
Milestone: [GH 30](https://github.com/coryj627/slate/milestone/30). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 2, 8; DoD §A–§G + §X-A…§X-E).

**Sequencing:** X3-1 lands **early** (dep X0-3 only) so the suite is switchable during the X1 wave. X3-2 follows X3-1. X3-3 is the milestone close-out — last, after all X2 + X3-2.

Baseline facts (verified 2026-07-04, this worktree):

- Settings tabs: `apps/slate-mac/Sources/SlateMac/SettingsView.swift` — `TabView` with `MathSettingsTab` / `CodeSettingsTab` / `BibliographySettingsTab` (`:28`). The TabView is left unwrapped so its native tab-interface AT shape is preserved (audit #262 M2 note at `:19`). X3-1 adds the LaTeX authoring-aids settings under the **Editor Intelligence** grouping (program locked decision 11), **shared with Milestone V — Editor autocomplete** (`docs/plans/12_autocomplete/`, V2-1 #578). Whichever milestone lands its settings surface first *introduces* the Editor Intelligence container; the second *joins* it — the two coordinate on one grouping, not two competing tabs. Native tab-interface AT shape preserved either way.
- App-global prefs: `PreferencesStore.swift` (`mathKey`, `codeKey`; Codable, schema-drift-tolerant `decode`/`encode` at `:52`/`:68`). Persistence-tag stability is load-bearing — the `MathPrefs.swift` header warns never to rename a `persistenceTag` without a migration. X3-1 follows this verbatim.
- Localization: `SettingsLocalizationTests.swift` exists; all new strings are `String(localized:)`.
- Vault-local file convention: `.slate/prefs.json` / `.slate/graph.json`, atomic temp+rename. X3-2 writes `.slate/latex-snippets` the same way.
- In-app help routing precedent: `docs/help/canvas.md` (#526). X3-3 adds `docs/help/latex.md` through the same route.

---

## X3-1 · LaTeX settings tab — master + per-feature toggles (#586) — PR 1

### Model

`LatexSuitePrefs` (Codable, mirrors `MathPrefs.swift`): master `enabled: Bool = false` + sub-toggles `snippets`, `autoFraction`, `tabout`, `matrix`, `conceal`, `bracketMatch`, `preview` — each with a **stable `persistenceTag`** (documented "DO NOT RENAME" like `MathPrefs`). Persisted via a new `PreferencesStore.latexKey`. Maps to the Rust `LatexSuiteConfig` (X0-3) at session-config time.

### Surface & interaction

`LatexSettingsTab` under the **Editor Intelligence** grouping in the SettingsView `TabView` (native AT shape preserved — do not wrap the TabView; coordinate the grouping with V2-1 #578 per the baseline note above). Master **Enable LaTeX authoring aids** toggle at top (off by default), then a section of sub-toggles; **sub-toggles are disabled (greyed, AX-"dimmed") while the master is off**. Concealment/bracket-match/preview rows note they are visual and opt-in. Changing a toggle updates the live session config so the editor reflects it without restart.

**Scope note (program §2 boundary):** this tab governs the **new authoring aids only**. It contains **no** controls for the existing math *rendering* — reading-mode rendering, MathCAT speech, verbosity, and braille remain on the **Math** tab (`MathSettingsTab`) and are unaffected by everything here. A short caption on the tab states that the core LaTeX rendering is always on and is configured under Math. Turning the master off returns the editor to its exact default behavior; it never dims or disables rendering/speech/braille.

### Tests

`LatexSuitePrefs` Codable round-trip + schema-drift tolerance (unknown/missing keys don't corrupt); persistence tags asserted stable (a test pins the literal strings); master-gates-children (sub-toggles inert + AX-dimmed when master off); default is all-off; config propagates to session; localization keys present (`SettingsLocalizationTests` extended); native TabView AT shape intact (audit #262 M2); a11y-check 100/100.

## X3-2 · Snippet manager + user `.slate/latex-snippets` file (#595) — PR 2

### Surface & interaction

A snippet manager (reachable from the LaTeX settings tab). Lists the **default** library and the **user** library, marking which user entries override a default (by trigger+mode). Per entry: enable/disable, edit (trigger, replacement, options, priority, description), remove. Global actions: **Add**, **Import** (merge a file), **Export**, **Reset to default** (clears user overrides after confirm). Edits validate through the X0-3 parser and **surface `SnippetParseError`s inline** (field + message) — a malformed snippet is diagnosable, never silently dropped. Writes `.slate/latex-snippets` atomically (temp+rename).

### Tests

List renders default vs. user + override marking; CRUD each path; import merge + export round-trip; reset-to-default (with confirm); malformed edit shows the typed error and does not persist; atomic write (no partial file on failure); fully keyboard-operable (add/edit/remove without pointer); a11y-check 100/100.

## X3-3 · Docs + Milestone X close-out sweep (#596) — PR 3

### Docs

`docs/help/latex.md`, routed through in-app help like `docs/help/canvas.md`:
- **What it is + how to enable/disable** (the master + sub-toggles; the "inert when off" guarantee).
- **Snippet syntax + options** (`t/m/M/n/A/r/v/w/c/C`, tabstops, mirrors, regex captures) and the `.slate/latex-snippets` format.
- **The default library** (table).
- **Full keyboard + VoiceOver reference** from the recorded X1-5 walkthrough (expansion, tabstops, tabout, auto-fraction, matrix, visual snippets, Box/Select).
- **Obsidian-parity notes + deliberate divergences with rationale:** concealment-as-projection (AX text stays real LaTeX, §X-D), no-global-chords (T rule R1), accessible-first ordering. Link back to this program.

### Close-out sweep

Full `SLATE_CENSUS_FULL=1` rerun (bypass, determinism, context; X0-4); APCA Lc table for conceal glyphs + bracket-match in both appearances; a11y-check 100/100 across the settings tab, manager, and editor-with-suite-on; announcement audit (verbosity honored, no double-speak); `BENCHMARKS.md` refresh (expansion, context, disabled-path free); confirm §X-A/§X-B/§X-D gates still hold on `main`.

### Tests / exit

Docs render + help route resolves; every keyboard command in the reference is reachable and matches the implementation; close-out checklist complete; **Milestone X reviewed and closed** (GH milestone 30).

---

**Milestone exit:** all 15 issues merged; the suite ships **off by default**; enabling it delivers the full authoring set keyboard-only and with VoiceOver; the optional visual layer is opt-in and AX-preserving; and with everything off the editor is byte-identical to its pre-X behavior (census + E2E). Update the project memory (`project_milestone_x_latex_suite.md`) with the durable invariants and any human-residual VoiceOver pass, mirroring the Milestone U close-out record.
