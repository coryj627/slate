# W5 executable spec — Commands, search, templates, file management

Issues: W5-1 ([#741](https://github.com/coryj627/slate/issues/741)) · W5-2 ([#742](https://github.com/coryj627/slate/issues/742)) · W5-3 ([#743](https://github.com/coryj627/slate/issues/743)) · W5-4 ([#744](https://github.com/coryj627/slate/issues/744)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue.
Program: [00_program.md](../00_program.md) (decisions 5, 12; DoD §W-C/§W-D). Depends on W0.5-1 (ranking) and W0.5-3 (announcements).

**Execution order: { W5-1 ∥ W5-2 ∥ W5-3 ∥ W5-4 } after W1.**

**W0/W1 execution baseline (2026-07-19 refresh — facts the original spec predates):**

- **The FFI for this wave is bound and largely census-proven** (`SlateUniffi`, `public`): the W0.5-1 palette surface (`palette_sections`, the recents codec/transition fns), the `CommandRegistry` + foreign `CommandAction` round-trip (success/error/re-entry proven by the W0 §W-E censuses), `full_text_search` (§W-A-serialized with goldens since W0-3), `render_template`/`list_templates`/`extract_template_metadata`, and the W5-4 mutation set (`create_exclusive(_bytes)`, rename/move/delete, batch operations). No new FFI is needed to start.
- **The parity matrix is the burn-down list** (W0-4): 181 command rows with capability labels, 52 chords, and a **spoken-hotkey column derived by the generator's reviewed `HotkeySpoken` mirror** — the chord table this wave finalizes and the W7-3 spoken strings both anchor to it; the generator fails on unattributed chords, so matrix drift is loud. Re-run it at wave start.
- **§W-D reality — the palette and search count strings are still host-composed (#969):** the palette model strings carry `#717 core-rendered follow-up` residue markers and the search-result summary builder is a residue family; their conversion (or recorded designation) is pre-unpark-executable and is the §W-D prerequisite for W5-1/W5-2. The three command-drift-test twins have concrete anchors now: registration-forward against the live registry, menu-scrape-reverse against the WPF menu bar, help-table against `docs/help/` per-platform chord tables (decision 20).
- **W5-4 shares W1-0's gate:** rename/move mutations may not ship before **#911** lands the atomic no-clobber primitive — the same reason W1-2 waits; `create_exclusive` is already the only create path.
- **Fluent theme (program decision 2 addendum):** palette/search overlays and pickers are Fluent-styled; §W-C list/keyboard assertions run against the Fluent templates; overlay text sits on W1-1 Slate tokens with the two-layer Contrast behavior; the Mica policy applies to overlay backgrounds.

## W5-1 · Command palette + the chord table — PR 1

1. Palette over the core registry + W0.5-1 ranking/recents; match-range bolding from core data; section grouping; invocation via `CommandAction` round-trip; filter-count announcements via canonical events — **prerequisite: the palette residue family of #969** (the mac strings still carry `#717 core-rendered follow-up` markers; convert before consuming, never re-compose in C#).
2. **The chord table, finalized**: the declarative mac-chord → Windows-chord mapping seeded in W1-1 (⌘→Ctrl, ⌥→Alt, documented exceptions where Windows conventions win, e.g. F2 rename) is completed here and becomes load-bearing: it feeds menu accelerators, palette display, spoken hotkeys (W7-3), and help docs (decision 20). No ad-hoc per-view bindings (§W-G-adjacent review gate). *(Stale as of #850: mac now ships F2-rename too — no longer a divergence example; the Windows mapping stands on its own.)* Live collisions the table must adjudicate as of 2026-07-12 (#863 map): Ctrl+T duplicate-tab vs the Windows new-tab convention; Ctrl+R tasks-review vs refresh; Ctrl+Shift+T reopen-closed-tab happily *matches* convention; Ctrl+O quick-open vs open-file-dialog (defensible — mirrors mac's own ⌘O repurposing).
3. Windows twins of the three command-drift tests: registration-forward (every registry command reachable), menu-scrape-reverse (every menu item backed by a registry command), help-table (docs match registry + chord table).

- [ ] Palette parity over core ranking; chord table + three drift tests green

## W5-2 · Search overlay — PR 2

1. FTS overlay parity: scopes as shipped (incl. tag scope semantics from #567), result navigation, snippet display, open targets — all over the core search API; result-count announcements canonical — **prerequisite: the search-summary residue family of #969**. The core search results are already §W-A-serialized with goldens (W0-3) — the overlay's result rows extend that coverage, not a new mechanism.

## W5-3 · Templates — PR 3

1. Template picker + prompt flow (variables, cursor placement request semantics) over core `render_template`; parity with mac prompt/cursor behaviors.

## W5-4 · File management & bulk operations — PR 4

1. Command-driven file management parity (new note, new folder, rename with link-rewrite, move-to-folder, delete flows incl. confirmation semantics, Duplicate) — all mutations via core (creates via O's never-clobber `create_exclusive`; renames/moves via the same rewrite engine — single-entry operations; the bound batch FFI is move/trash only, and **no file batch-rename primitive exists or is claimed**). **Depends on W1-0 (#911)** — rename/move UI may not ship before the atomic no-clobber primitive lands, the same gate W1-2 carries. *(Ownership correction 2026-07-19: the mac "bulk rename sheet" is the **property** bulk-rename over `rename_property_across_vault` — it belongs to **W4-4 (#736)**, where the matrix already assigns `slate.editor.bulkRenameProperties`; the previous wording here wrongly implied a W5-4 file-batch surface.)*
2. **Mutation-parity harness (new, 2026-07-19):** the shipped §W-A harness is **read-side only** — it cannot validate rewrite safety. W5-4 ships a **differential mutation harness**: drive identical mutation sequences on both platforms over a fixture vault and diff the **final artifacts** — full file tree with byte-exact contents, link-rewrite results, op-log entries, and undo state — under the failure scenarios that matter: occupied destination (typed conflict, nothing clobbered), interruption mid-sequence, cancellation, retry-after-conflict, and undo round-trips. (#911's stress test covers the provider-level rename primitive only; it does not prove rename-with-link-rewrite or delete/batch coherence.) The harness skeleton lands here scoped to W5-4's scenarios and also serves W1-2's tree-CRUD and W4-4's property-rewrite rows; W8-4 hardens and completes it. These are **not** ordinary serializer additions to the read-side harness.

- [ ] (each) matrix rows green; keyboard-complete; §W-C/§W-D rows green
