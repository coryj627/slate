# V — Research brief: what obsidian-completr does, why its popup is inaccessible, and how Slate inverts it

Evidence base for [`00_program.md`](00_program.md). Capability reference: [tth05/obsidian-completr](https://github.com/tth05/obsidian-completr) (read 2026-07-04, `master`). This brief is descriptive; the normative decisions live in the program doc's locked-decision table.

## §1 — The completr feature surface (what "parity" means)

completr is a provider-based autocomplete. Source layout: `src/provider/{dictionary,scanner,word_list,front_matter,latex,callout,blacklist}_provider.ts`, a shared `provider.ts` contract, `popup.ts`, `snippet_manager.ts`, `settings.ts`.

**Provider contract** (`provider.ts`): each provider implements `getSuggestions(context, settings) -> Suggestion[]`; an optional `blocksAllOtherProviders` lets one provider monopolize results. A `Suggestion` carries `{ displayName, replacement, overrideStart?, overrideEnd?, icon?, color? }` — display text and inserted text are distinct, and a provider may override the replaced range. This maps cleanly onto Slate's `CompletionItem` (V0-1) and `blocks_all_other_providers` (locked decision 3).

**Providers:**

| Provider | Fires on | Notes |
|----------|----------|-------|
| LaTeX (`latex_provider`) | math context; optionally code blocks | "All MathJax commands"; `\begin{env}` environment completion; snippet placeholders. Backslash not required. |
| Vault scanner (`scanner_provider`) | any word | Learns words from the current file and/or whole vault. "Performant even with big lists." |
| Word list (`word_list_provider`) | any word | Loads external files, one word per line. |
| Front matter (`front_matter_provider`) | inside YAML frontmatter | "Learns any key with any value"; tag-suffix append; ignore-case. |
| Callout (`callout_provider`) | `> [!` line | Callout-type list; source = built-in or the Callout Manager plugin. |
| Blacklist (`blacklist`) | — | Excludes suggestions via `blacklisted_suggestions.txt`; a hotkey (default `Shift+D`) blacklists the current one. |

**Settings** (`settings.ts`, defaults verbatim): `characterRegex="a-zA-ZöäüÖÄÜß"`, `maxLookBackDistance=50`, `autoFocus=true`, `autoTrigger=true`, `minWordLength=2`, `minWordTriggerLength=3`, `wordInsertionMode=IGNORE_CASE_REPLACE` (enum: `MATCH_CASE_REPLACE` | `IGNORE_CASE_REPLACE` | `IGNORE_CASE_APPEND`), `ignoreDiacriticsWhenFiltering=false`, `insertSpaceAfterComplete=false`, `insertPeriodAfterSpaces=false`, `latexProviderEnabled=true`, `latexTriggerInCodeBlocks=true`, `latexMinWordTriggerLength=2`, `latexIgnoreCase=false`, `fileScannerProviderEnabled=true`, `fileScannerScanCurrent=true`, `wordListProviderEnabled=true`, `frontMatterProviderEnabled=true`, `frontMatterTagAppendSuffix=true`, `frontMatterIgnoreCase=true`, `calloutProviderEnabled=true`, `calloutProviderSource=COMPLETR`. These become Slate's `CompletionConfig` / `AutocompletePrefs` (V0-1, V2-1) with the same names and defaults.

**Snippets** (`snippet_manager.ts`): placeholder syntax `#` = tab-stop, `~` = final cursor, `\n` = newline. Slate models these as a structured `Snippet` returned over FFI so the *accessible* surface can traverse fields (V0-4, V1-3) rather than relying on an editor extension.

**Keys** (`popup.ts`): Enter inserts (default), arrows/Tab navigate, Esc closes; "bypass" hotkeys act while the popup is open; unmodified function keys aren't supported. Slate's key ownership is locked decision 7.

## §2 — Why the completr popup is inaccessible (the thing Slate fixes)

completr's popup is a CodeMirror `EditorSuggest`-style floating `div`. It is drawn, positioned at the caret, and styled — but it is **not** exposed as an accessibility element. There is no combobox relationship between the editor and the list, no per-row selectable elements, no live-region announcement when the selection moves. A VoiceOver/NVDA user typing in the note hears their keystrokes echo and nothing else; the suggestions, their count, and the current selection are all silent. Arrow keys appear to "do nothing" because the visual selection change is never announced. This is not a completr-specific bug — it is the default state of nearly every web/Electron autocomplete, and it is exactly the failure Slate exists to not repeat (cf. the Graph brief's "opaque canvas a screen reader cannot enter").

The fix is not cosmetic. An accessible autocomplete is an ARIA-combobox-equivalent: the input owns a popup, the popup is a listbox, options are individually addressable, `aria-activedescendant`/selection moves are announced, and every action has a keyboard path. On macOS/AppKit that means combobox semantics on the text view plus per-row `NSAccessibilityElement`s and coalesced `NSAccessibilityPriorityAnnouncement`s (locked decisions 5–7; V1-2, V1-4).

## §3 — Auto-trigger vs. screen readers (why "VoiceOver-aware" is a real constraint)

Auto-triggering (completr's default, and what "IntelliSense-style" implies) is in tension with screen-reader UX: every keystroke both echoes the typed character *and* would announce a changed suggestion set. Naïvely posting an announcement per keystroke produces garbled overlap at typing speed ("1—5—12 suggestions"). Slate keeps auto-trigger as the default (owner decision, program locked decision 6) but requires **coalesced `.medium`-priority announcements** — the `CommandPaletteView`/`QuickSwitcherView` precedent, where `.medium` is the politeness floor that survives typing echoes and rapid updates supersede rather than queue. A manual trigger and a global toggle are always available for users who prefer silence-until-asked.

## §4 — What Slate reuses (nothing here is greenfield)

Verified in this worktree (see the V0/V1 specs for exact `file:line`):

- **Fuzzy ranking** — `CommandPaletteModel.fuzzyScore(query:target:)` is already a pure `nonisolated static` scorer (subsequence + word-boundary/consecutive/prefix bonuses); `QuickSwitcherModel` adds a filename bonus and recency tie-break. Reused for the wikilink/word providers.
- **Link resolution** — `link_resolver::VaultIndex::all_paths` + `ResolvedLink`, and `links_db::OutgoingLink`, already resolve `[[targets]]`. The wikilink provider is a thin query over these.
- **Incremental buffer** — `DocBufferState` already maintains block structure, `fm_end`, and a comment index per keystroke off the main thread; the classifier and word-index hook ride this existing edit path.
- **Command registry** — `commands.rs` `CommandRegistry` + `CommandSection::Editor`, registered Swift-side via `registerCoreCommands`.
- **Accessible list + announcements** — `AccessibleDataGrid`, `CommandPaletteView`'s `.keyDown` monitor / `ArrowKey` / `isDismissKey` / IME guards, and the `.medium` announcement discipline.
- **Prefs** — `.slate/prefs.json` via `PrefsJsonStore` with atomic writes and forward-compat, mirrored by a Rust prefs struct (`citations` precedent).

## §5 — Explicit gaps Slate must build new

- No trie / prefix / word-frequency structure exists in slate-core today — V0-3 builds it (incremental, laziness-first, census-gated).
- `EditorSpanKind` has no `Math` or `Callout` variant — V0-2 adds them so the classifier can gate LaTeX/callout completion.
- No completion FFI surface, no popup UI, no completion settings — V1/V2.

## §6 — Scope Slate deliberately adds beyond completr

Obsidian core (not completr) supplies `[[` link, heading/block, and `#tag` completion — so completr never shipped them. For Slate these are the highest-value completions in a wiki-style vault and the cheapest to build (the resolver + fuzzy matcher already exist). They ship in V0-5 as first-class, context-gated providers. Time-lapse/animation and any cloud/LLM completion are out of scope for V (V-next candidates).
