# FL1 executable spec — Row presentation: display names, dates, previews, badges, settings

Issues: FL1-1 ([#653](https://github.com/coryj627/slate/issues/653)) · FL1-2 ([#654](https://github.com/coryj627/slate/issues/654)). Milestone: [GH 31](https://github.com/coryj627/slate/milestone/31). Grouped delivery: FL-02 closes both issues after FL-01.
Program: [00_program.md](../00_program.md) (locked decisions 4–6; DoD §FL-A). U §A–§G apply (a11y 100/100 on tip, APCA ≥ 75 both appearances).

Baseline facts (verified 2026-07-14 at `origin/main` `6aa9fce`):

- `FileTreeSidebar.swift` remains the assembly surface for file/folder rows and `FileTreeViewModel` level projection. Exact July 5 line references are obsolete; FL-02 extracts one immutable `SidebarRowModel` and shared `SidebarFileRow` rather than adding more row logic to the monolith.
- VoiceOver announcements already flow through AppState. The shared row composes one coherent utterance and preserves the established leaf-level text-selection behavior.
- `SettingsView.swift` already exists and is wired from the app's SwiftUI `Settings` scene. FL-02 adds a Sidebar tab/section to that view; it must not create a second Settings scene or window.
- Device-local settings use typed `UserDefaults` preferences. Vault-authored pins, shortcuts, and overrides use the new `.slate/sidebar.json` store; core does not read that presentation file.
- Tokens: type/spacing/color via `Tokens.*`; SF Symbols v7 via `SlateSymbol`.

---

## FL1-1 · Display names + configurable date line (#653) — closing PR FL-02

1. **Primary label** = `FileSummary.display_name ?? stem(name)`. When a display name is used, the AX label appends the filename once: `"Weekly review — file review-2026-07.md"` (em-dash separator; sighted users get a `help`/tooltip with the filename instead of a second visible line).
2. **Date line** (the existing secondary line) becomes configurable: source `modified | created` (default modified), format `relative | absolute` (default relative; absolute = `DateFormatter` medium date, user locale). `created` with NULL `created_ms` falls back to modified **with the label "Modified"** — never mislabel a date.
3. Sorting/search elsewhere are untouched — display names change *presentation only* in this PR (FL3-1 adds name-sort on display names; note the seam).
4. Rename flow: inline rename edits the **filename** (unchanged). When a display name differs from the stem, the rename field shows the filename — renaming a titled note must not silently suggest the title as the new filename.
5. AX: file AX value becomes `"<date-label> <date>"` matching the visible line; VO announce conventions for mutations unchanged.

Tests (`FileTreeSidebarTests.swift` pattern): titled vs untitled fixture rows; created-NULL fallback labeling; AX label composition; rename-field shows filename for titled note.

- [ ] Display-name fallback + AX composition
- [ ] Date source/format settings (device-local UserDefaults; keys `sidebar.dateSource`, `sidebar.dateFormat`)
- [ ] Rename-field filename rule
- [ ] Unit + a11y tests; APCA re-measured on the two-line row in both appearances

## FL1-2 · Previews, badges, density + Sidebar settings surface (#654) — closing PR FL-02

1. **Preview lines**: setting `sidebar.previewLines ∈ {0,1,2,3}` (default 0 — opt-in; the U2 row density is the shipped baseline). Renders `FileSummary.preview` clamped to N lines with ellipsis; empty/None ⇒ no line (no placeholder). Preview text is `Tokens` secondary style; **AX**: appended to the row's AX value after the date, prefixed "Preview:", so VO reads name → date → preview in one utterance (leaf Text, per the textSelection memory).
2. **Task badge**: when `task_total > 0` and setting `sidebar.showTaskCounts` (default on), a trailing badge `3/5` with symbol `checklist`; AX value appends `"3 of 5 tasks open"`; `task_open == 0` renders the badge dimmed with AX `"all 5 tasks done"`. Badge is informational only (no button).
3. **Word count**: setting `sidebar.showWordCount` (default off) appends `· 1,240 words` to the date line (grouped decimal, localized); AX included verbatim.
4. **Density**: setting `sidebar.density ∈ {standard, compact}` (default standard). Compact = name only (no date/preview/badges) at reduced row height — and AX value still carries the date (visual density must not reduce spoken information).
5. **Settings surface**: add a **Sidebar** tab/section to the existing `SettingsView`. Controls: date source/format (FL1-1), preview lines, task counts, word count, and density. All are typed device-local preferences. FL-02 also creates the AppState-owned `.slate/sidebar.json` store (`SidebarVaultPrefsStore`: versioned, bounded reads, exclusive cross-writer lock, deterministic JSON, atomic temp+rename, unknown-key preservation, corruption fallback plus visible notice) with no authored consumers until FL-06.
6. Perf: rows remain cell-cheap — no per-row formatter allocation (share `RelativeDateTimeFormatter`/`DateFormatter` statics); 10k-vault root paint budget from U2 unchanged.

Tests: preview clamp + AX composition; badge states (open/all-done/hidden); compact keeps AX value; formatter-allocation regression; `SidebarVaultPrefsStore` concurrent-writer/round-trip preserves unknown keys; corrupted or oversized input ⇒ defaults + notice (no crash).

- [ ] Preview/badge/word-count/density rendering + AX
- [ ] Settings pane section + UserDefaults keys
- [ ] `SidebarVaultPrefsStore` (bounded, locked, versioned, atomic, forward-tolerant) + tests
- [ ] a11y-check 100/100 on tip; APCA both appearances
