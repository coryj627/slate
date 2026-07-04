# V3 executable spec — Close-out: help doc, benchmarks, milestone audit

Issue: V3-1 ([#581](https://github.com/coryj627/slate/issues/581)).
Milestone: [GH 29](https://github.com/coryj627/slate/milestone/29). One PR.
Program: [00_program.md](../00_program.md) (DoD §V-A…§V-E). Wave gate: V2 complete.

## V3-1 · Help doc + benchmarks close-out + milestone audit (#581) — PR 1

**Help doc** — `docs/help/autocomplete.md` (sibling to the other `docs/help/*` topic docs):
- Providers table: what each completes and what triggers it (word, LaTeX/MathJax + `\begin{}` + snippets, callouts, front-matter keys/values, `[[` targets, `#`/`^` anchors, `#tags`).
- Keys: auto-trigger, ⌃Space manual trigger, arrows/Tab/Enter/Esc, PageUp/Down, snippet Tab-fields, ⇧⌘D blacklist.
- Settings: every `autocomplete.*` pref and its default (link to the V2-1 table).
- Accessibility notes: combobox behavior, announcement verbosity interaction, how to make it silent-until-asked (autoTrigger off).

**Benchmarks close-out** — record final `BENCHMARKS.md` baselines for `completion_query/{1k,10k,50k}`, `word_index_incremental/{10k}`, `word_index_build/{10k,50k}`; re-confirm no `scan_initial`/save-path regression (DoD §V-E). Note the `SLATE_CENSUS_FULL=1` release-run result for the V0-6 censuses.

**Milestone audit** — a close-out checklist against the program DoD, recorded in the PR:
- §V-A: every provider path completable keyboard-only + VoiceOver (link the V1-4 recorded pass).
- §V-B: announcement discipline verified (coalesced `.medium`, verbosity honored).
- §V-C/§V-D: determinism + index-vs-rebuild + classifier-total censuses green (incl. one full-scale run).
- §V-E: perf budgets met, no regression.
- U §A–§G: a11y-check 100/100, APCA Lc ≥ 75 both appearances, atomic writes, one-PR-per-issue, fmt/clippy clean across the milestone.

**Exit:** milestone V closable — every issue #568–#581 merged, DoD satisfied, help doc + benchmarks landed. File V-next issues (research brief §6 / program tail) if evidence for them accrued during the build.
