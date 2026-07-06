# W5 executable spec — Commands, search, templates, file management

Issues: W5-1 ([#741](https://github.com/coryj627/slate/issues/741)) · W5-2 ([#742](https://github.com/coryj627/slate/issues/742)) · W5-3 ([#743](https://github.com/coryj627/slate/issues/743)) · W5-4 ([#744](https://github.com/coryj627/slate/issues/744)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue.
Program: [00_program.md](../00_program.md) (decisions 5, 12; DoD §W-C/§W-D). Depends on W0.5-1 (ranking) and W0.5-3 (announcements).

**Execution order: { W5-1 ∥ W5-2 ∥ W5-3 ∥ W5-4 } after W1.**

## W5-1 · Command palette + the chord table — PR 1

1. Palette over the core registry + W0.5-1 ranking/recents; match-range bolding from core data; section grouping; invocation via `CommandAction` round-trip; filter-count announcements via canonical events.
2. **The chord table, finalized**: the declarative mac-chord → Windows-chord mapping seeded in W1-1 (⌘→Ctrl, ⌥→Alt, documented exceptions where Windows conventions win, e.g. F2 rename) is completed here and becomes load-bearing: it feeds menu accelerators, palette display, spoken hotkeys (W7-3), and help docs (decision 20). No ad-hoc per-view bindings (§W-G-adjacent review gate).
3. Windows twins of the three command-drift tests: registration-forward (every registry command reachable), menu-scrape-reverse (every menu item backed by a registry command), help-table (docs match registry + chord table).

- [ ] Palette parity over core ranking; chord table + three drift tests green

## W5-2 · Search overlay — PR 2

1. FTS overlay parity: scopes as shipped (incl. tag scope semantics from #567), result navigation, snippet display, open targets — all over the core search API; result-count announcements canonical.

## W5-3 · Templates — PR 3

1. Template picker + prompt flow (variables, cursor placement request semantics) over core `render_template`; parity with mac prompt/cursor behaviors.

## W5-4 · File management & bulk operations — PR 4

1. Command-driven file management parity (new note, new folder, rename with link-rewrite, move-to-folder, delete flows incl. confirmation semantics) + bulk rename sheet parity — all mutations via core (same rewrite engine; §W-A rewrite rows).

- [ ] (each) matrix rows green; keyboard-complete; §W-C/§W-D rows green
