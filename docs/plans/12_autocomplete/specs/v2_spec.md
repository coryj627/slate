# V2 executable spec — Settings, management, commands

Issues: V2-1 ([#578](https://github.com/coryj627/slate/issues/578)) · V2-2 ([#579](https://github.com/coryj627/slate/issues/579)) · V2-3 ([#580](https://github.com/coryj627/slate/issues/580)).
Milestone: [GH 29](https://github.com/coryj627/slate/milestone/29). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decision 10). Inherits U §A–§G (accessible settings surfaces, a11y-check 100).

**Execution order:** V2-1 → V2-2 → V2-3 (independent enough to interleave; V2-1 first so there's a settings home). Wave gate: V1-2 landed (a popup to configure).

## Baseline facts (verified 2026-07-04, this worktree)

- **Prefs storage:** `apps/slate-mac/Sources/SlateMac/PrefsJsonStore.swift` reads/writes `<vault>/.slate/prefs.json` (`readBibliographyPrefs`/`writeBibliographyPrefs`), atomic temp-file + rename (~:84–99), preserves unknown top-level keys (forward-compat). Symmetric Rust parser precedent: `crates/slate-core/src/citations/prefs.rs`.
- **Settings UI:** `apps/slate-mac/Sources/SlateMac/SettingsView.swift` — tabbed (`Math`, `Code`, `Bibliography`) via `.tabItem`; each tab is a `Form`/`Picker` wrapped in `.accessibilityElement(children: .contain)` for VoiceOver grouping (~:73–79 Math example).
- **Command registry:** `crates/slate-core/src/commands.rs` — `CommandSection` (~:36–47: File, Navigation, View, Vault, **Editor**, Tasks, Settings, Plugins), `Command { id, label, accessibility_hint, hotkey_hint, section }` (~:50–73), `CommandRegistry` = `Arc<RwLock<HashMap<String, Arc<dyn CommandAction>>>>`, `list()` (sorted section→id) / `invoke_by_id()`. Swift: `SlateCommands.swift` `SlateCommandID` constants + `registerCoreCommands(into:appState:)` (~:191–214); `CommandPaletteView` renders by `CommandPaletteModel.sectionOrder`.
- **Blacklist convention:** completr stores `blacklisted_suggestions.txt` under its plugin dir; Slate mirrors this as a file under `.slate/`.
- **In-editor list-editing precedent:** `AccessibleDataGrid` + the settings `Form` patterns; recents-store write discipline `FileRecentsStore.swift`.

---

## V2-1 · Autocomplete prefs model + settings tab (#578) — PR 1

**Model.** Add `crates/slate-core/src/completion/prefs.rs` `AutocompletePrefs` (mirrors `CompletionConfig`, serde) and a Swift store extension in `PrefsJsonStore` (`readAutocompletePrefs`/`writeAutocompletePrefs`) writing the `"autocomplete"` key of `.slate/prefs.json`, atomic temp+rename, unknown-key preservation. Defaults are the research-brief §1 values verbatim so a fresh vault behaves like completr's defaults:

| Key | Default |
|-----|---------|
| `enabled` | `true` |
| `autoTrigger` | `true` |
| `autoFocus` (auto-select first) | `true` |
| `minWordLength` | `2` |
| `minWordTriggerLength` | `3` |
| `maxLookBackDistance` | `50` |
| `characterRegex` (word-char class) | letters + digits (Unicode-aware) |
| `wordInsertionMode` | `IgnoreCaseReplace` |
| `ignoreDiacriticsWhenFiltering` | `false` |
| `insertSpaceAfterComplete` | `false` |
| `insertPeriodAfterSpaces` | `false` |
| `latexProviderEnabled` / `latexTriggerInCodeBlocks` / `latexMinWordTriggerLength` / `latexIgnoreCase` | `true` / `true` / `2` / `false` |
| `fileScannerProviderEnabled` / `fileScannerScanCurrent` | `true` / `true` |
| `wordListProviderEnabled` | `true` |
| `frontMatterProviderEnabled` / `frontMatterTagAppendSuffix` / `frontMatterIgnoreCase` | `true` / `true` / `true` |
| `calloutProviderEnabled` | `true` |
| `wikilinkProviderEnabled` / `tagProviderEnabled` (Slate-native) | `true` / `true` |

**UI.** A new `Autocomplete` tab in `SettingsView`: master enable, trigger group (autoTrigger, autoFocus, min-lengths, max-look-back), completion group (insertion mode, ignore-diacritics, insert-space/period), and a per-provider enable list (each with its sub-options: LaTeX-in-code, front-matter ignore-case/tag-suffix). `Form` sections wrapped `.accessibilityElement(children: .contain)`; every control labeled + hinted.

**Live application.** Changes write the prefs and bump a generation the editor observes (no restart); the next `completions` query reads the new `CompletionConfig`.

**Tests:** round-trip (write→read equality), forward-compat (unknown keys preserved), defaults match the table, Rust/Swift schema agree. a11y-check 100 on the tab.

## V2-2 · Word-list, custom completions, blacklist management (#579) — PR 2

UI + wiring for the file-backed sources:

- **Word-list files:** add/remove external word-list paths (validated on add: exists, readable, one-word-per-line); persisted in prefs; feeds V0-3's loader. Show per-file word count.
- **Custom completions:** an editor for user-defined `label → replacement` entries (the completr custom-list analog), stored under `.slate/`.
- **Blacklist:** view/remove entries; the **in-editor "blacklist current suggestion" hotkey** (V2-3 command) appends the highlighted item's label here and immediately drops it from the live popup. File under `.slate/…blacklist` (completr `blacklisted_suggestions.txt` convention).

All list editing is keyboard- and VoiceOver-operable: add/remove actions announced, focus managed after removal (don't strand focus on a deleted row — the U file-tree pattern), empty-state text present.

**Tests:** add/remove round-trips to disk atomically; blacklist hotkey removes from the live set; VoiceOver announces add/remove; invalid word-list path rejected with an accessible error.

## V2-3 · Completion commands in CommandRegistry (#580) — PR 3

Register in `CommandSection::Editor` (`commands.rs` + Swift `registerCoreCommands`, `SlateCommands.swift:191`):

| id | label | default hotkey | action |
|----|-------|----------------|--------|
| `slate.editor.triggerCompletion` | Trigger autocomplete | ⌃Space | force a completion query at the caret regardless of token length |
| `slate.editor.toggleAutocomplete` | Toggle autocomplete | — | flip `autocomplete.enabled`, write prefs, announce new state |
| `slate.editor.blacklistSuggestion` | Blacklist current suggestion | ⇧⌘D (avoid the plain ⇧D shadowing completr hit) | append the highlighted popup item to the blacklist (V2-2) |

Each carries `accessibility_hint` + `hotkey_hint`, is palette- and menu-discoverable, and (for the toggle) reflects/writes the pref. `blacklistSuggestion` is a no-op with a spoken "no suggestion selected" when the popup is closed.

**Tests:** commands appear in `list()` under Editor; invoking `toggle` flips the pref and announces; `trigger` opens the popup even below `minWordTriggerLength`; hints present for VoiceOver.
