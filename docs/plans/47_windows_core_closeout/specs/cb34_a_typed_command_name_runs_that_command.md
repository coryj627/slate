# CB-34: A typed command name runs that command

Issues: Closes #1834. Size M. Lanes: rust, windows (the app job's unit facts and one shell gate partial, Call 7), swift (the Mac lane compiles core's change and runs its palette tests; no Swift file changes, Call 5). AT: the wave's full NVDA pass, base and head. Wave: C12, slot 2. Decision: CD23, adopted as recommended on 2026-11-16: "Sections in best-match order; an untouched selection follows the first row; no new sentence. Ledger: the Mac's selection rule." Review: an Opus red team on this spec and on the diff, then Codex adversarial-review rounds until it approves (plan §4 rule 9), then the lead's merge judge.

Plan row, verbatim: "CB-34 | A typed command name runs that command | #1834 | M | rust, windows, swift | NVDA full | CD23".

Every `path:line` below is as of `main` at f363a597, read on 2026-11-23. `S/` is `apps/slate-windows/src/SlateWindows/`, `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`, `G/` is `apps/slate-windows/tests/SlateWindows.AccessibilityTests/`, `VM.cs` is `S/CommandPaletteViewModel.cs`, `C28` is `docs/plans/28_palette_contracts.md`, `C40` is `docs/plans/40_nvda_matrix_remediation_contracts.md`. The step 0 probe and its run logs are kept with the chunk's working notes, outside the repository; they stay local. The spec's red team (one Opus agent, 2026-11-23) rebuilt the probe and reproduced every step 0 number; its findings are folded in below and listed under "Red team record".

## The lead's rulings (2026-11-24), binding on the build

The calls below are kept as they were put. Where a ruling differs from the text under "Design" or "Tests", the ruling stands. Build from `main` at ad04a470 (CB-22b merged).

1. D2, as recommended. "An exact name, and the exact name after its section prefix, is the best match CD23 asks for." The "refresh" and "back" moves are accepted and named in the PR body.
2. The three origins, as designed; a seed holds only at its own request's publication.
3. **The owner ruled, 2026-11-23, verbatim: "Silent follows (Recommended)".** A selection the palette moves is never announced at a publication; only a reader's row that a query removes is replaced and announced once, before the count. The two quiet cases go in the PR body with base and head NVDA phrases. The C28 and C40 addenda record the answer as "owner, 2026-11-23".
4. As CD23 words it; the residual is named under not delivered.
5. No Swift edit; a #1954 line and `not delivered: VoiceOver pass`.
6. Both `lib.rs` doc comments are fixed here; no FFI change, and the PR says so. CB-16 and CB-45b do not branch their code until this PR merges.
7. The gate is extended; the palette journeys run locally under the lock.
8. The census, with the three planted cases.
9. Measured first in the NVDA pass, at 60 and 100 ms a key, on base and head. If head runs another command at a typist's speed, the build stops and tells the lead with the numbers. Either way it is named under not delivered.
10. A row that takes the keys becomes the reader's selection, silently. Where the keys land after a publication is measured on `main` first.

## Calls for the lead

Ten calls. Call 9 touches an owner decision (T7, 2026-10-05) and may need the owner; the others are the lead's. **Call 6 edits `crates/slate-uniffi/src/lib.rs` (two doc comments, no FFI change).**

1. **What "best match" ranks on.** CD23 says "sections in best-match order". Measured over the 189 registered commands (step 0): ordering sections by their best row alone still leaves 6 typed names whose first row is another command (Duplicate, Move To…, New Note, Rename…, Open, Unpin), each a score tie that the id breaks the wrong way, because a query that is a prefix of two labels earns both the same +50. (D1) Rows by exact label first, then score, then a label match over a hint-only match of equal score, then the declared section, then id; sections in the order of their best row. 189 of 189 typed names lead. (D2) D1 with one more tier after the exact label: the query is exactly the label's text after its first ": " (`Canvas: Show Outline` for "show outline"). 95 of the 189 labels have such a lead. 88 are section names; 7 are not (Connections ×3, Files ×1, Graph Verbosity ×3), and on the Mac every saved query registers as "Run query: {name}" (C28 PINV-6), which the tier also reaches, so typing a saved query's name runs it. Of the 81 tails that no other command's tail shares, 65 lead on `main`, 74 under D1 and 80 under D2; the one miss, "Duplicate", goes to the command whose whole label is "Duplicate", which is the exact name. Against D1, D2 changes the first row for 7 tails, 5 for the better and 2 to a context command a reader may find unavailable where they are: "refresh" leads with Bases: Refresh (D1: Refresh Sync Diagnostics) and "back" with Connections: Back (D1: the Sidebar's history Back); partial names led by their command go from 1,849 to 1,847 of 2,932. Recommended: D2. It is what a reader types after hearing "Canvas: Show Outline" in the palette or "Show Outline" in the Canvas menu. Cost: two tiers beyond the decision's words and the two changed tails above; the order inside a section changes on ties; contract P1's text changes; no FFI change, no sentence.
2. **Who placed the selection.** The design gives the selection one of three origins. Reader: the reader moved it (arrows, Home, End, Page Up and Page Down, a pointer click, a key inside the list, and a UIA select through the list, all of which reach `UserSelect`). Palette: the palette chose it (the open's first row, a follow, a snap after a removed row, a recovery from no rows). Seed: DS-10's two placements, the current choice when a list of choices opens and the command's row when Esc comes back from its choices. A Reader selection is kept while its row survives. A seed is kept only at the publication of the very request that seeded it, and is the palette's at any other (red team finding 1: a letter typed before that request publishes supersedes it, so "the first publication after the seed" would be the letter's, and the seed would survive it). Recommended as designed. Cost: one field and its writes in `VM.cs`, and the seed's generation. The alternative, a seed that sticks like a reader's choice, keeps #1834's cause in the one list of choices that ships: on a fresh root the current theme is "Use Windows setting", and typing "d" leaves it selected because its "Windows" matches, so Enter keeps it where the reader typed Dark's first letter and Dark is the first row ("Dark" scores 65 on "d", "Use Windows setting" 10, run by the red team). `main` does this today.
3. **What the reader hears.** Recommended: a selection the palette moves at a publication is never announced; a reader's row that a query removes is replaced by the first row and the replacement is said once, before the count, as R-11 has it today. The selection lines a reader asks for are untouched: arrows and the other reader moves, Esc's "Selected: <command>" back from a list of choices (`VM.cs:799`) and `PaletteChoicesOpened`. This is the decision's premise: the reading's part (c) recommended no count sentence naming the row "since an automatic selection is otherwise silent before Enter", and the owner adopted it. Cost: what `main` says goes quiet in two cases. (a) Open, type a query that removes the open's row: `main` says "Selected: <first row>" then the count; head says the count. (b) Recover from no matches: the same. The same holds in a list of choices: with Light current, "d" removes Light, and "Selected: Dark" before `2 choices matching "d"` goes quiet. Eleven existing hosted fact methods, some of them theories with several rows, assert one of those lines and change (listed under "Tests", by the red team's reading; the runs confirm the list); two of them, which witness T3's order (the selection at publication, the count after the window), restart from a reader's move so they keep witnessing it. C28 P7, P10, T1, T1′, T3, T6, I5 and the DS-10 record, and C40 R-11, gain the rule. Alternatives: (S2) announce each follow, which queues one "Selected:" per publication at typing speed (contract 38 D-1: Medium queues), the trail P10's window exists to stop, and which breaks the gate journey's leg 1; (S3) keep `main`'s line only when the palette's old row was removed, which speaks a follow on some keystrokes and not others for reasons the reader cannot see.
4. **How long a reader's selection lasts.** Recommended: as CD23 words it, while its row survives. Residual, by design: a reader who arrows to a row and then types a different name that the row still matches, often through its hint, keeps that row, silently, and Enter runs it. Step 0 shows how often a row survives an unrelated name: an untouched row survived the typed name in 282 of 35,721 (open row, typed name) pairs under D2. Alternative ("the clear rule"): emptying the field also hands the selection back to the palette. Cost of the clear rule: one condition and one fact, and a rule a reader has to learn. The PR names the residual under "not delivered".
5. **The Mac.** Recommended: no Swift edit; the Mac's selection rule is a #1954 line, as CD23 says. The Mac gets core's order now, so after a snap VoiceOver names a different row than today; the line says so and the PR writes `not delivered: VoiceOver pass`. Until plan 43 adopts the rule the Mac keeps cause 2: a typed name runs the open's row when that row still matches. Alternative: edit `CommandPaletteModel.handleQueryChange` by reading here. Cost of that: a behaviour and speech change no lane can hear, and two Mac questions this chunk cannot answer, whether hover (`CommandPaletteView.swift:312-320`, which writes `selectedID` on pointer entry) counts as a reader's move, and how the view's `onChange(of: selectedID)` (`:106-144`), which speaks every change after the first, tells a follow from a move.
6. **`crates/slate-uniffi/src/lib.rs`: two doc comments.** CB-34 needs no FFI change, but D1 makes `PaletteRow`'s doc false: "hosts derive the global "strongest match overall" order from it without re-scoring" (`:9681-9684`); after this chunk the first row is the strongest match and the score alone no longer orders exact-name ties. AGENTS.md: "A defect in the PR's own feature is fixed on its branch, whatever its size." `CommandSection`'s "declared in palette render order" (`:8821-8823`) is wrong on `main` already (P1 says the enum is not the render order), and once this PR edits the file it is a small find in a file the PR changes. Recommended: fix both doc comments here. Cost: CB-16 and CB-45b, which edit `lib.rs` after this chunk, take `main` in around two doc comments; the bindings are generated and not committed, so no binding diff. Alternative: leave `lib.rs` alone and file both (the red team reads the second as crossing subsystems, so a filed issue, not a #1955 line); that ships a doc this PR made false.
7. **The gate journey.** Recommended: extend `G/ShellAccessibilityTests.Palette.cs`. Leg 1 ("split", typed fast) asserts that the selected row is the first listed row and that no `PaletteCommandSelected` was heard; a new last leg types "settings" with real keystrokes, waits until the selected row is "Settings…", presses Enter, and the Settings sheet opens. It pins the settled case; the race in Call 9 is the NVDA pass's to hear. Both legs are red on `main` (leg 1 hears "Selected: <first row>" when the open's row is removed; leg 3's selected row is never "Settings…"). Cost: one gate partial file, which no other C12 chunk edits and CB-22b (#2055) does not touch; one local run of the palette journeys under the lock. Alternative: hosted facts and the NVDA pass only.
8. **The typed-name census.** Recommended: one Windows fact over the registered catalog (`ChordTable.RegisteredRows`, through the binding): every command's typed name, with and without its trailing "…", and every tail that is no other command's tail or whole name, ranks that command first. Two commands that share a whole label fail it unless an allow-list row with a reason names them (none today), and a stale allow-list row fails. Planted cases (plan §7): a shared whole label, a tail that loses, and a stale row, each seen to fail. Cost: a future command author meets it; that is its purpose, since a new label that ties an old one brings #1834 back. Alternative: no census, and the Rust facts on synthetic commands only.
9. **An Enter that arrives before the latest query's rank returns.** T7 (C28 `:877`, the owner's decision of 2026-10-05) publishes a rank that has finished but runs the rows on screen when the rank is still running; "Enter never waits for a rank still running" (I3, `:901`). A rank is requested only when its keystroke is dispatched (`VM.cs:513-533`), and each publication lays its rows out synchronously (`S/CommandPaletteResultsPresenter.cs:88-116`, `:143-167`); C28 P10's amendment measured the 178-row "s" publication at about 200 ms under NVDA. Keys typed during such a publication queue, so a name and its Enter typed inside that window are dispatched back to back, and Enter meets a rank still running (the probe's copy ranks in 1.1 to 1.6 ms, before the lane hop). Enter then runs an earlier prefix's first row, which under D2 is the right command for 1,847 of 2,932 partial names (63%). By reading, that needs a whole name and its Enter inside one slow publication, so it is a short name typed quickly right after the open. Recommended: measure first, in the NVDA pass (step 9 below: a short name and Enter at about 60 and 100 ms a key, right after the open, base and head), and take it to the owner only if a head run at a typist's speed runs another command; the PR names it under "not delivered" either way. Alternative, which amends the owner's decision: Enter waits, bounded at 150 ms (the owner's held-file bound of 2026-11-11), for the latest non-empty query's rank, then publishes it and runs its selection, and acts as today on a timeout. Cost of the alternative: an owner question, a change to T7 and I3, `EnterRunsTheSelectionOnScreenWhileANewerRankIsPending` inverted, and a bounded UI-thread wait.
10. **Keys inside the list.** A click or a UIA select on the row already selected raises no `SelectionChanged` (`S/CommandPaletteResultsPresenter.cs:169-180`), so it never becomes the reader's, while Home on that row does. And Tab moves the keys onto the palette's row; a publication that lands then moves the selection to the first row while WPF lands the keys on a row of the new view (`T/CommandPaletteTests.View.cs:200-222` asserts only "a row"), so the row the reader hears and the row Enter runs can differ. Recommended: a row that takes the keys becomes the selection and the reader's, silently, since NVDA already speaks the focused row (the presenter tells the view model on a row's `GotKeyboardFocus`). The build measures on `main` first where the keys land after such a publication. Cost: one more product file (the presenter, which CB-35 reads as its model and does not edit) and two facts. Alternative: name both under "not delivered" as known limits.

## Step 0, as run

On 2026-11-23, against `main` f363a597. A standalone Rust probe (`step0/src/main.rs`) holds `fuzzy_score`, `grapheme_key`, `WORD_BOUNDARY`, `SECTION_ORDER` and the non-empty branch of `palette_sections` copied verbatim from `crates/slate-core/src/palette.rs`, and `fold_sigma` from `name_fold.rs`, over the registered rows of `apps/slate-windows/chords.json` (189 commands, all with hints). The host's P7 rule is simulated from `VM.cs:1610-1618` ("survives": keep the selection while its id survives, else the first row) against the proposed rule ("follows": the first row). "Final" publishes only the finished query (fast typing); "every" publishes each prefix (slow typing). A pair is (the row selected at open, the typed name); any command can be the open's row, since the first Recent row is the last one run. The labels are typed with a trailing ellipsis dropped. No two registered commands share a typed name. The red team rebuilt the probe and reproduced every number below.

| Ranking | Typed names whose first row is another command | Enter runs another command, rule survives, final / every | rule follows | Partial names (3+ letters) led by their command | Tails led (of 81) |
|---|---|---|---|---|---|
| `main` | 16 of 189 | 3,046 / 3,353 of 35,721 pairs (36 names) | 3,024 (16 names) | 1,547 of 2,932 | 65 |
| sections by best row only | 6 | 1,252 / 2,199 | 1,134 (6 names) | 1,824 | 73 |
| + label over hint | 6 | 1,252 / 1,916 | 1,134 | 1,854 | 74 |
| + exact label first (D1) | 0 | 282 / 1,727 (36 names) | 0 | 1,849 | 74 |
| + exact tail (D2) | 0 | 282 / 1,727 (36 names) | 0 | 1,847 | 80 |

Rank cost in the probe's copy (release, median of 201 runs): "s" 1.12 ms on `main` and 1.52 ms under D2 with the tiers computed once per row; "settings" 1.30 and 1.33 ms; "canvas: show outline" 1.64 and 1.64 ms. A comparator that recomputes the tiers took "s" to 6.7 ms.

What it shows:

- **Cause 1 is real and wider than the issue's two examples.** On `main` 16 typed names put another command first: "Settings" leads with Close Pane (View; the issue said Close Tab, which is second today), "Save" with Refresh Sync Diagnostics, "Find…" with New Canvas, "Copy Path" with Graph's Where Am I?, "Move to Trash" with Focus Next Pane, "New Note" with Split Down, "Rename…" with Focus Previous Pane, "Add Tag…" with Resize Mode, and eight more. "Help" has no command of that name; its first row moves from New Canvas to Show History Panel.
- **Cause 2 is real, as the reading said by reading.** With the ranking fixed (D1 or D2) and the P7 rule kept, a typed name still runs the open's row in 282 pairs when only the final query publishes and 1,727 when each prefix does; 36 names are affected. With the follow rule, 0. Reordering in core alone does not fix #1834.
- **Exact-name ties are what the section order alone misses.** Without the exact tier 6 names still lose to a longer label with the same prefix score.
- The step does not run the Windows view model; the host half is a simulation of its rule. `TypingACommandsNameAndPressingEnterRunsThatCommand` is that half's red on the real host, seen at step 4.
- The probe does not see the Mac's catalog, which differs; by reading the same causes hold there.

## Drift check (step 1)

- `palette.rs`: `palette_sections` is at `:241` (the reading said `:237`), the two-score max at `:260-265` (`:256-261`), the global sort at `:277-280` (`:273-276`), the grouping at `:282-299` (`:278-295`), the test `scores_expose_the_global_ranked_order_across_sections` at `:750` (`:746`). All moved because CB-32 (#2033) added the final-sigma fold and its doc to `grapheme_key` (`:207-222`). `SECTION_ORDER` is still `:86-99`.
- `VM.cs`: the P7 rule is still `:1610-1618`, `UserSelect` `:1639-1648`, `Score` `:140-145`.
- Mac: `CommandPaletteModel.swift:65-69`, `CommandPaletteViewTests.swift:693`, `:934` and `MilestoneQIntegrationTests.swift:161` are where the reading said.
- C28: P1 is `:35-53` (the reading said `:34-41`); P7 `:112-123` unchanged.
- The issue's "first is Close Tab" for "Settings" is now Close Pane (step 0).
- The Mac tests that run real core ranking type only "alpha", "save", "e" and no-match queries over fixtures of two sections or fewer, and none changes under D2 (`CommandPaletteViewTests.swift:693-701`, `:934-944`, `:1015-1039`; `MilestoneQIntegrationTests.swift:161-176`, where "save" leads by score and by exact name). Nor does the uniffi test `palette_sections_ranks_and_blends_through_ffi_shapes` (`lib.rs:18397-18440`). Both by reading, confirmed by the red team.

## Current state on main

**Core.** `palette_sections` (`palette.rs:241-300`): an empty query gives Recent first, then the declared sections (`:302-363`). A non-empty query scores label and hint with `fuzzy_score` (`:143-201`), keeps the max (`:260-265`), sorts by score, then id (`:277-280`), then groups in `SECTION_ORDER` (`:282-299`). The row's `score` is that max (`:58-69`). The module doc (`:13-16`) and the function doc (`:235-240`) say "globally sorted by descending score ... grouped into native sections in declared order". The test at `:749-784` states the defect as a host's job; no host re-sorts. Quick Open shares `fuzzy_score` and nothing else (`crates/slate-uniffi/src/lib.rs:9770-9772`).

**Windows.** `Publish` (`VM.cs:1539-1634`) applies P7 at `:1610-1618`. `SetSelection` (`:1772-1828`) announces any change except the first after an open (`_suppressSelectionAnnouncement`, armed at `:702`, `:760`, `:793`). Reader moves reach `UserSelect` (`:1639-1648`) from `MoveSelection` (`:888-905`), `SelectFirst` (`:908`), `SelectLast` (`:917`), `MovePage` (`:932-948`) and `Select` (`:951`), which the presenter calls for any list selection it did not make itself (`S/CommandPaletteResultsPresenter.cs:169-180`). The seeds: `EnterChoices` sets `_selectedId = choices.CurrentId` (`:758`) with `_query = ""` and `RequestRank` (`:772`); `BackOrDismiss` sets `_selectedId = level.RowId` (`:791`), announces that row (`:799`) and ranks. A worker's result posts below input (`:1478-1503`), so a letter already typed supersedes a seed's request. `Open` on a palette already open (PD-2, `:646-710`) clears the query and the selection but keeps `_rows` and `_sections` until its own publication, with the suppression flag armed (`:695-702`): a Down pressed in that gap moves over the old query's rows, silently. `InvokeSelected` (`:971-982`) publishes a finished rank first (T7) and runs the selection. `CommandPaletteRowViewModel.Score` (`:140-145`) is read only by one fact (`T/CommandPaletteTests.cs:78-94`).

**Mac.** `handleQueryChange` (`CommandPaletteModel.swift:65-69`) is `main`'s P7; `loadCommands` selects the first row (`:56-60`); the view speaks every `selectedID` change after the open's (`CommandPaletteView.swift:106-144`), and hover writes `selectedID` (`:312-320`). `filteredCommands` (`CommandPaletteModel.swift:151-160`) sorts by score for the count and one integration test.

**Contracts.** C28 P1 (`:35-53`): core owns section order; `PaletteRow.Score` "is not a display order". P7 (`:112-123`): "Selection is **preserved** when the selected id survives a query change". P10 (`:178-179`): the first selection change after open is suppressed. The #1275 state machine: T1 and T1′ (`:867-868`), T3 (`:872`, "P7 against the CURRENT selection (announced unless suppressed or unchanged)"), T6 (`:875`), T7 (`:877`), I3 and I5 (`:901`); the DS-10 record (`:855`, "P18, P7 and the count window hold unchanged"). C40 R-11 (`:43`). C40 is a census path: `W77RemediationDocsCensus` treats a line that starts `**R-n` as a contract heading, so an addendum is written `*R-11 addendum (…):*`, as C40 `:23`, `:25` and `:29` do.

## Design

**D1 (core: rows).** In the non-empty branch of `palette_sections`, each matched row also carries two flags, computed once per row before the sort: the query is exactly the label, and the query is exactly the label's text after its first ": ". "Exactly" compares the grapheme keys `grapheme_key` already makes (`palette.rs:214-222`), so case, canonical equivalence and the final sigma fold as in every match, and a label's one trailing `…` may be dropped. The query is not trimmed: the tiers only reach rows `fuzzy_score` matched, and a query's surrounding space already decides that (red team finding 6). Rows sort by: exact label, then exact tail (Call 1), then score, then a label match over a hint-only match, all descending; then the declared section's index and the id, ascending. `fuzzy_score`, `grapheme_key`, `MatchSpan` and `PaletteRow.score` do not change, so the count, the bolding and Quick Open are untouched. The empty-query branch does not change.

**D2 (core: sections).** Sections come in the order their first row appears in that sort: a section's place is its best row's, and two sections whose best rows tie on every tier above the declared section keep the declared order. Recent never appears for a non-empty query (unchanged). The module, function and `PaletteRow` docs say so, and so do the two uniffi doc comments (Call 6).

**D3 (Windows: the selection's origin).** `VM.cs` keeps `_selectionOrigin` ∈ {Palette, Reader, Seed} beside `_selectedId`, and, for a seed, the generation of the request that seeded it.
- `Open`, a re-open (PD-2), `Dismiss`, and a selection that resolves to no row: Palette.
- `UserSelect` when it lands on a row: Reader. Refused while sealed, so the origin does not change then. A row that takes the keys inside the list: Reader (Call 10).
- `EnterChoices`: Seed, at the generation its `RequestRank` makes. `BackOrDismiss`: the origin the row had when the reader pressed Enter on it, stored with the level (`CommandsLevel`), so an arrowed-to row comes back as the reader's and a followed row as the palette's (red team finding 8). If that row was the palette's, Back's own line (`:799`) still names it, and a later query moves it silently like any palette selection.
- At `Publish` (replacing `:1610-1618`): no rows, the selection is null and Palette. A Reader selection whose row survives is kept. A Seed whose row survives, at the publication of its own generation, is kept and becomes Palette; at any other publication a Seed is treated as Palette. Otherwise the first row, and Palette.
- A re-open (PD-2): until the re-open's first publication, the rows on screen are the previous query's, so a reader move then does nothing, as on a fresh open, where there are no rows yet (red team finding 12, fixed here: under D3 that silent move would become a reader's selection the next typed name keeps). The rows stay on screen, so no empty state shows in the gap. `_suppressSelectionAnnouncement` stays.

**D4 (Windows: what is said).** A publication announces only when a Reader selection's row was removed and replaced by the first row: one `PaletteCommandSelected` (or `PaletteChoiceSelected`) for the replacement, before the count, as `main` does today. Every other change at a publication is silent. Reader moves still speak through `SetSelection` exactly as today, and Back and `PaletteChoicesOpened` speak as today. `SetSelection` gains a flag for "spoken"; its construction sites stay in `SetSelection`, so contract 38's trigger ledger does not change (`--check`).

**D5 (Windows: what Enter runs).** No change to `InvokeSelected`. With D3 the selection at Enter is the first row unless the reader placed it. T7's typed-ahead publication runs first, as today. Call 9 is the case it does not reach.

**D6 (contracts and comments).** C28: P1's section-order bullet says "for a non-empty query, sections come in the order of their best row, a tie keeping the declared order; for an empty query, Recent and then the declared order", and its `Score` bullet says the first row is the strongest match and the score is its fuzzy part. P7 names the three origins and the publication rule, and says it is Windows' rule until #1954's Mac line lands. P10 gains D4's rule beside the first-selection rule: "a selection the palette moves is never announced at a publication". T1, T1′ and T6 say whose selection each makes, T3 and I5 take D4, and the DS-10 record's "P7 ... hold unchanged" gains a dated note. A dated CB-34 paragraph closes the record. C40 gains `*R-11 addendum (2026-11-2x, plan 47 CB-34, #1834):*`: the replacement is announced when a query removes a row the reader moved to; a selection the palette made follows the first row and is not announced at a publication. `VM.cs:720-722`'s remark ("seeded on the current choice, which the P7 rule then keeps") and the `Score` doc (`:140-145`) are corrected.

**What does not change.** No FFI change, no `a11y.rs` change, no new sentence or event, no chord or help line, no automation id, no focus site. The count, its window and its priority. Arrow, Home, End and Page behaviour on a published list. The empty query's layout. Quick Open's ranking. The seal.

**Known limits, named in the PR.** Call 4's residual. Call 9, unless it is ruled the other way. The Mac keeps cause 2 until plan 43 (Call 5). If Call 10 is declined, its two cases.

## Tests

Each new fact is seen red on `main`'s code first, with its failing line kept, then green; each mutation is seen to fail at least one fact.

**Rust (`palette.rs` tests).**

| Fact | What it pins | Red on `main` because |
|---|---|---|
| `an_exact_label_match_is_the_first_row_whatever_its_section` | A View command whose hint matches "settings" and a Settings command "Settings…": the first row is Settings' | View comes first by declared order (it pins section order: by score alone Settings… already wins, so M4 sees it, not M1) |
| `sections_come_in_the_order_of_their_best_row` | Three sections whose best rows score in an order unlike the declared one | declared order |
| `sections_whose_best_rows_tie_keep_the_declared_order` | Two sections with equal best rows | a pin: green on `main`; M5 |
| `an_exact_label_outranks_a_longer_label_with_the_same_prefix_score` | "Open" over "Open Vault…" whose id sorts first | the id breaks the tie |
| `the_name_after_the_first_colon_is_an_exact_match` | "show outline" puts "Canvas: Show Outline" over a smaller-id row of equal score; a whole-label match still outranks a tail ("duplicate") | no tier |
| `a_trailing_ellipsis_and_the_matchers_fold_do_not_stop_an_exact_match` | "rename", "RENAME" and a decomposed or final-sigma spelling exact-match "Rename…" or a Greek label, each against a competitor of equal score with a smaller id | the id breaks the tie |
| `a_label_match_outranks_a_hint_only_match_of_equal_score` | equal scores, the hint row with the smaller id | the id breaks the tie |
| `the_display_leads_with_the_strongest_match` | replaces `scores_expose_the_global_ranked_order_across_sections` (`:749-784`): "save" gives Editor's Save, then File's Print Note | display stays section-grouped |
| `ranked_query_sections_follow_their_best_row` | replaces `ranked_query_groups_by_section_in_declared_order` (`:688-715`): "ne" gives File, Sidebar, Vault | File, Vault, Sidebar |

The empty-query tests stand unchanged and pin D2's last sentence.

**Windows, new facts (`T/CommandPaletteTests.TypedName.cs`, new; the real ranker through the binding).**

| Fact | What it pins | Red on `main` because |
|---|---|---|
| `TypingACommandsNameAndPressingEnterRunsThatCommand` | Open with a Recent row whose hint matches "settings"; type "settings" one letter a publication; Enter invokes Settings | the Recent row survives (cause 2), and View leads (cause 1) |
| `AnUntouchedSelectionFollowsTheFirstRowSilently` | Across three queries the open's row survives but is not first: after each, the selection is the first row and no selection line is posted | the survivor is kept |
| `AMovedSelectionThatSurvivesIsKept` (theory: Down, Up, Home, End, Page Down, Page Up, pointer `Select`) | each route makes the selection the reader's; a surviving row is kept, silently | a pin; M7 per route |
| `AfterAMovedRowIsRemovedTheNextQueryFollowsTheFirstRow` | End, a query that removes it (one line, the replacement, then the count), then a query where the replacement survives but is not first: the selection follows, silently | the replacement survives and is kept |
| `AnUntouchedRowAQueryRemovesIsReplacedSilently` | open, a query that removes the open's row: the count alone | `main` says the replacement |
| `RecoveringFromNoMatchesSelectsTheFirstRowSilently` | "zzzz", then "sav": the count alone | `main` says the replacement |
| `TheCurrentChoiceIsKeptAtTheOpenAndTypingFollowsTheFirstRow` | Switch Theme… on "Use Windows setting": the open selects it; "d" keeps it but ranks Dark first, and the selection is Dark; Enter chooses Dark | the seed is kept |
| `ALetterTypedBeforeTheChoicesPublishTakesTheFirstRow` | the same, with the choices' "" rank parked and "d" typed before it publishes (red team finding 1) | the seed is kept |
| `EscBackKeepsTheRowAsTheReaderLeftIt` (theory: arrowed to, followed) | Back's row comes back with its origin: an arrowed-to row survives a query, and its removal is announced; a followed row follows the next query silently | `main` keeps a survivor either way |
| `AReaderMoveBeforeAReOpenPublishesDoesNothing` | re-open (PD-2) with the rank parked, Down: no selection, no line; after the publication the selection is the first row | `main` moves over the old rows |
| `ATypedAheadEnterRunsTheFirstRowOfTheQueryItFollows` | the T7 path with the open's row surviving the finished query | the survivor runs |
| `EveryRegisteredCommandsTypedNameRanksItFirst` and its planted cases (Call 8) | the census and its three planted failures | 16 names and 16 tails fail |
| `TheKeysStayInTheFieldWhileTheSelectionFollowsTheFirstRow` | AGENTS.md's republish rule, in the shell's palette overlay: across three publications the keys stay in the search field, and the list's selected item is the first row, realized and on screen | the selection stays on the survivor |
| `ARowThatTakesTheKeysIsTheReaders` and `TheKeysAndTheSelectionStayOnOneRowAcrossARepublish` (Call 10) | Tab into the list then a query: the row the keys are on is kept; after a republish the keys and the selection are on one row | measured first on `main` |
| `TheDisplayLeadsWithTheStrongestMatch` | replaces `ScoreIdentifiesTheStrongestMatchWithoutReorderingTheDisplay` (`T/CommandPaletteTests.cs:78-94`) | File first |

`AQueryPublishedUnderTheKeysLandsThemOnARowOfTheNewView` (`T/CommandPaletteTests.View.cs:200-222`) gains one assertion, that the row holding the keys after the query is the reader's row (it survives). It is run on `main` first and recorded as a pin or a find.

**Existing facts whose expectations move, each re-seen with its reason.** By D2, over `StandardCommands` (`T/CommandPaletteTests.cs:1032-1039`) the query "o" now ranks Quick Open (Navigation, a word-boundary O) before New Note and Toggle Bold. By D4, a palette selection's change at a publication says nothing.

- D2: `FromNoSelectionDownLandsOnTheFirstRowAndUpOnTheLast` (`T/CommandPaletteTests.cs:373`); `SelectionIsPreservedWhenTheSelectedIdSurvivesAQueryChange` (`:428`; Toggle Bold is still last, so its premise holds); `SelectionSnapsToTheFirstRowWhenTheSelectedIdVanishes` (`:451`); `ASlowRankLeavesTheDispatcherPumpingAndTheRowsUntouched` (`T/CommandPaletteTests.Lane.cs:54-64`); `TheSnapshotLoadsOffTheUiThreadAndAClosedPaletteTakesNothing` (`Lane.cs:586-588`); `TheGuardAloneKeepsASynchronizedSwapSilent` (`T/CommandPaletteTests.View.cs:256-258`); `AQueryThatRemovesTheSelectionAnnouncesItsReplacementThenTheCount` (`View.cs:157`; the replacement is Quick Open, and its "no" leg no longer pins a survivor, so that leg moves to a query Quick Open survives).
- D4: `TheShippedListAnnouncesOnlyTheViewModelsSelection` (`View.cs:103`, its last leg); `TheShippedListTakesAWorkersPublicationOnTheDispatcher` (`View.cs:398-408`); `NothingThePaletteOwnsPublishesOrSpeaksInsideACommandsModalLoop(true)` (`T/CommandPaletteTests.Invoking.cs:113-127`); `InsideACommandsLoopThePaletteTakesNoKeysAndATypedQueryRanksAfterAFailure` (`:340-348`); `ASupersededRanksFailureIsSilentAndUnlogged`, both rows (`:429-436`); `TheUnsavedChangesPromptOnCloseHearsNothingFromThePalette` (`:594-600`); `APromptInsideACommandKeepsThePaletteSealedUntilTheCommandReturns` (`:672-680`); `AssertSealedAndSilentThenResumed` (`T/CommandPaletteTests.ModalLoop.cs:375-380`), and with it `AModalLoopOverTheShellSealsThePalette` (every kind) and `TheShippedClosePromptSealsThePalette`.
- D4, restarted from a reader's move (`SelectLast`) so they still witness T3's order: `TheCountWindowOpensAtTheKeystrokeAndARankSlowerThanItSpeaksAtPublication` (`Lane.cs:518-526`) and `ARankFasterThanTheCountWindowSpeaksTheCountWhenTheWindowRunsOut` (`Lane.cs:547`, `:553-561`).

The build runs every class in `T/CommandPaletteTests*.cs`, `T/CommandDriftTests.cs`, `T/EditorSurfaceCommandTests.cs`, `T/MainWindowHandoffTests.cs` and `T/ReadingFocusTests.cs`'s palette facts, and the PR lists each moved expectation with its reason.

**Gate (Call 7, `G/ShellAccessibilityTests.Palette.cs`).** Leg 1 asserts the selected row is the first listed row and no selection line was heard. Leg 3: "settings" typed with real keystrokes; wait until the selected row is "Settings…"; Enter; the Settings sheet's root appears. Run locally under the lock (`--filter Palette_`); on the hosted runners the journey runs in the shell gate.

**Mutations.**

| # | Mutation | Seen by |
|---|---|---|
| M1 | no exact-label tier | `an_exact_label_outranks_*`, the census |
| M2 | no tail tier | `the_name_after_the_first_colon_*`, the census's tail half |
| M3 | no label-over-hint tier | `a_label_match_outranks_*` |
| M4 | sections back in declared order | `an_exact_label_match_is_the_first_row_*`, `sections_come_*`, `TheDisplayLeads*`, `TypingACommandsName*` |
| M5 | a tie between sections broken by id | `sections_whose_best_rows_tie_*` |
| M6 | the trailing-ellipsis drop removed | `a_trailing_ellipsis_*`, the census |
| M7 | one reader route not marking Reader (each in turn) | `AMovedSelectionThatSurvivesIsKept` |
| M8 | an untouched survivor kept (`main`'s P7) | `AnUntouchedSelection*`, `TypingACommandsName*`, `ATypedAheadEnter*`, the keys fact |
| M9 | a seed that stays sticky | `TheCurrentChoiceIsKept*` |
| M9b | a seed kept at any first publication, not only its own generation's | `ALetterTypedBeforeTheChoicesPublish*` |
| M10 | a seed dropped at its own publication | the existing choices facts (`T/CommandPaletteTests.Choices.cs:57`, `:180`) |
| M11 | a follow announced | `AnUntouchedSelection*`, `AnUntouchedRow*`, `Recovering*` |
| M12 | a reader's removed row replaced silently | `AQueryThatRemovesTheSelection*` |
| M13 | the replacement kept as Reader | `AfterAMovedRowIsRemoved*` |
| M14 | Back's row always the palette's | `EscBackKeepsTheRowAsTheReaderLeftIt(arrowed)` |
| M15 | a reader move allowed before the re-open publishes | `AReaderMoveBeforeAReOpen*` |

## NVDA full pass (base and head)

Launcher: `scripts/windows/launch-for-field-pass.ps1 -Root <scratch root>`, under the desktop lock, NVDA speech read from its log, keys sent with SendInput at about 150 ms a letter unless a step says otherwise. Base is a Release build of `main`; head is the branch. Vault: two notes, `alpha.md` open in a tab. Each step starts from a recents file the step writes before launch (empty unless it says otherwise), so no step's recents come from another's command. A step's text is entered by select-all and one paste where it says "paste", so no prefix publishes. Each phrase is recorded as heard on both; the expected lines below are core's renderings, base lines from the step 0 probe's simulation, and how NVDA reads the quotes is recorded as heard.

1. **The issue's repro.** Empty recents (the open's row is New Canvas). Ctrl+Shift+P; type "settings"; Enter. Both: `11 commands matching "settings"`. Base: Enter runs Close Pane (the note's tab closes). Head: the Settings sheet opens and its title is read.
2. **A second name.** Empty recents. Ctrl+Shift+P; "save"; Enter. Base: Refresh Sync Diagnostics runs. Head: Save runs.
3. **Cause 2.** Recents `["slate.workspace.closeTab"]`. Ctrl+Shift+P (the open's row is Recent's Close Tab, silent); "settings"; Enter. Base: Close Tab runs (it survives through its hint). Head: Settings opens.
4. **A moved row kept.** Ctrl+Shift+P; End (both: "Selected: <last row>"); one letter of its label that keeps it (both: the count, no selection line); Escape.
5. **A moved row removed.** Ctrl+Shift+P; End; a letter that removes it. Both: "Selected: <first row>", then the count.
6. **An untouched row removed.** Recents `["slate.workspace.closeTab"]`. Ctrl+Shift+P; "x". Base: "Selected: " and the label of `slate.navigation.jumpToBibliography` (the first row for "x" on `main`), then the count. Head: the count alone.
7. **Recovery from no matches.** Ctrl+Shift+P; paste "zzz" (both: `No commands match "zzz"`); paste "sav". Base: "Selected: Refresh Sync Diagnostics", then the count. Head: the count alone.
8. **Choices.** On the fresh root the theme is "Use Windows setting". Ctrl+Shift+P; "switch theme"; Enter (both: "Select a theme. Selected: Use Windows setting, current."); "d"; Enter. Base: `2 choices matching "d"` and no selection line, then "Theme: Use Windows setting.". Head: the same count, then "Theme: Dark.". Switch back.
9. **Typed ahead at a typist's speed (Call 9).** Empty recents. Ctrl+Shift+P, then at once "save" and Enter at about 100 ms a key; then again at about 60 ms a key; then "settings" and Enter in one SendInput burst. Each run recorded on both: which command ran. Head is expected to run Save and Settings at the first two speeds; any other result goes to the lead under Call 9.
10. **Back.** Ctrl+Shift+P; "switch theme"; Enter; Escape (both: "Selected: Switch Theme…"); one more letter that removes it. Base: "Selected: <first row>", then the count. Head: the count alone (the row was followed, not arrowed to). Then again with Down and Up onto Switch Theme… before Enter: head, "Selected: <first row>", then the count.
11. **The reader's element and focus across the republish.** During steps 1, 4 and 6, NVDA's focus line is recorded after each letter: on both, the keys stay in "Search commands, edit" and NVDA says nothing of the list as it republishes. With Call 10 ruled as recommended: Tab into the list, then type; head keeps the keys and the selection on one row.

## Files it edits

- `crates/slate-core/src/palette.rs`: the non-empty branch's sort and grouping, the exact helper, the docs, the tests.
- `crates/slate-uniffi/src/lib.rs`: two doc comments (Call 6). No signature, record or export changes.
- `S/CommandPaletteViewModel.cs`: `_selectionOrigin`, the seed's generation and their writes; `CommandsLevel`'s stored origin; the publication rule; the re-open gap; the `SetSelection` flag; two comments.
- `S/CommandPaletteResultsPresenter.cs`: a row taking the keys (Call 10).
- `T/CommandPaletteTests.TypedName.cs` (new: `git add`, then `python3 scripts/apply-license-header.py`), and the moved expectations listed under "Tests".
- `G/ShellAccessibilityTests.Palette.cs` (Call 7).
- `docs/plans/28_palette_contracts.md`, `docs/plans/40_nvda_matrix_remediation_contracts.md` (D6).
- Not edited: `a11y.rs`, any Swift file (Call 5), `MainWindow*`, `chords.json`, the parity matrix, `focus_integrity_manifest.json`. `python scripts/a11y_trigger_ledger.py --check`, `python3 scripts/generate-parity-matrix.py --check` and `python scripts/check_windows_census_trait.py` are run to show it. The census reads `ChordTable.RegisteredRows`, which the app compiles, so it reads no census-only path. The non-test files are five against rule 2's cap.

## Overlap

- CB-22b (#2055, running) edits `MainWindow.xaml`, `NoteHeaderView.cs`, `EditorEmbedPreview.cs` and menu censuses; no file in common.
- CB-16 and CB-45b edit `lib.rs` after this chunk; with Call 6 as recommended they take `main` in around two doc comments.
- CB-38 and then CB-38b follow on `VM.cs`, C28 and C40 (§6); they meet D3's field.
- CB-35 (#1837) reads the presenter as its model and edits the other search fields; Call 10 adds one handler to the presenter.
- CB-32 (merged) made `grapheme_key` fold the final sigma; D1's tiers use it, so "ΚΟΣΜΟΣ" exact-matches a label "κοσμος".

## #1954 and #1955

- #1954, under "Speech or text", replacing the #1834 line: "#1834 (CB-34): core orders a typed query's palette by best match: sections by their best row, and within that a whole-label match, then a match of the name after the label's first ": ", then score, then a label match over a hint-only match. On the Mac the first row changes, and with it the row a removed selection snaps to and the "Selected:" VoiceOver hears; a saved query's name typed alone now leads with its "Run query: {name}" row. Owed: the selection rule in `CommandPaletteModel.handleQueryChange` (a selection the reader did not move follows the first row and is not announced; one they moved is kept while it survives, and its replacement is announced once), with `CommandPaletteView`'s `onChange(of: selectedID)` silent for the palette's own moves; whether hover is a reader's move; Swift twins of `TypingACommandsNameAndPressingEnterRunsThatCommand` and the selection facts; a VoiceOver pass. Until then a typed name on the Mac runs the open's row when that row still matches." The PR writes `not delivered: VoiceOver pass (#1954)`.
- #1955: no line if the calls are ruled as recommended. If Call 6 is declined, the two `lib.rs` doc comments are filed (the red team sizes the second as crossing subsystems). If Call 10 is declined, one line: `S/CommandPaletteResultsPresenter.cs:169-180`, a click or a UIA select on the selected row, and a publication under keys in the list, failing tests `ARowThatTakesTheKeysIsTheReaders` and `TheKeysAndTheSelectionStayOnOneRowAcrossARepublish`.

## Found on the way

| # | Find | Size | What happens to it |
|---|---|---|---|
| 1 | 16 typed names lead with another command on `main`, not two | the chunk's own feature | Fixed here (D1, D2) |
| 2 | Section order alone leaves 6 exact names losing a prefix tie | the chunk's own feature | Fixed here (Call 1) |
| 3 | 16 of 81 unshared tails typed without their lead lead with another command | the chunk's own feature | Fixed here if Call 1 is ruled D2 |
| 4 | `PaletteRow`'s uniffi doc becomes false; `CommandSection`'s is false already | the PR's own feature, and a doc in a file the PR then changes | Fixed here (Call 6) |
| 5 | A Down pressed before a re-open publishes moves over the old rows silently (red team) | the chunk's own feature under D3; small, in `VM.cs` | Fixed here, red first |
| 6 | A seed survives a letter typed before its request publishes (red team) | the chunk's own feature | Fixed here, red first |
| 7 | Keys in the list and the selection can split across a republish; a click on the selected row is not a reader's move (red team) | the chunk's own feature | Call 10 |
| 8 | A short name and Enter typed inside a slow publication can run an earlier prefix's row (red team, by reading) | needs an owner decision to change | Call 9; measured in the NVDA pass |
| 9 | The issue names Close Tab as "Settings"'s first row; it is Close Pane today | a wrong claim | Corrected under "Drift check" |

## Verification

- Red run of every new fact against `main`'s code, failing lines kept; green run; each mutation in the table.
- Rust: `cargo fmt --check`, `cargo clippy --all-targets -- -D warnings`, `cargo test -p slate-core palette`, then the core and uniffi suites, all with `CARGO_TARGET_DIR=C:/dev/slate/target-plan-47-cb34`.
- Windows: `./apps/slate-windows/generate-bindings.ps1 -Locked`, Release build, the palette classes and the classes named under "Tests", `dotnet format ... --verify-no-changes --include` each edited C# file, then the whole unit run under the lock.
- The gate's palette journeys under the lock (Call 7).
- The three generated-table checks above.
- The NVDA full pass, base and head, phrases in the PR body.
- The diff red team (Opus), then Codex rounds to approve.
- The PR body ends with the count: found on the way, fixed here, deferred, filed (plan §4 rule 8).

## Acceptance

- Typing any registered command's name, with or without its trailing ellipsis, or its name after the label's first ": " where no other command has that name (Call 1), and pressing Enter once its rank has returned runs that command, whatever row the palette opened on.
- A selection the reader moved is kept while its row survives a query; a query that removes it moves the selection to the first row and says so once.
- A selection the palette made follows the first row and is not announced at a publication.
- The empty query's layout, the count, the keys on a published list and Quick Open are as on `main`.
- No FFI change and no new sentence; contracts 28 and 40 say what ships.

## Red team record

One Opus agent read the spec against `main` f363a597 on 2026-11-23, rebuilt the step 0 probe and reproduced every number, and ran no Windows test. 15 findings: 4 major, 9 minor, 2 nits. All are taken.

| # | Severity | Finding | Where it is now |
|---|---|---|---|
| 1 | major | A letter typed before a seed's request publishes supersedes it, so the seed survives the letter and #1834 stays in the theme list | D3: a seed holds only at its own generation's publication; fact and M9b |
| 2 | major | The census as worded is red on head: "Duplicate" is both a tail and a whole name; no planted cases | Call 8: an unshared tail is no other command's tail or whole name; three planted cases |
| 3 | major | D4 moves eleven existing fact methods, not two; D2 moves three more | Call 3's cost; the list under "Tests"; two T3 witnesses restarted from a reader's move |
| 4 | major | The typed-ahead race is not only a SendInput burst: keys queue behind a slow publication | Call 9 (owner-adjacent); NVDA step 9 |
| 5 | minor | NVDA base expectations ignored the recents earlier steps leave; step 7's Backspaces published prefixes | Each step writes its recents; paste; base lines from the probe |
| 6 | minor | Trimming the query and dropping "..." do nothing | D1: only the label's trailing "…" |
| 7 | minor | D2 is "the first ': '", not a section lead; two tails get worse | Call 1, D1, the acceptance and the #1954 line |
| 8 | minor | Back's row became the palette's, losing an arrowed-to row | D3: Back restores the stored origin; fact and M14 |
| 9 | minor | Keys in the list and a click on the selected row escape the origin model | Call 10 |
| 10 | minor | Leaving `lib.rs`'s doc false goes against AGENTS.md | Call 6 now recommends the edit |
| 11 | minor | D6 missed T1, T1′, T6, the DS-10 record and a `VM.cs` comment; C40's addendum form | D6 |
| 12 | minor | A reader move before a re-open publishes becomes a silent reader selection; the suppression flag is not inert | D3's re-open gap; fact and M15; the flag stays |
| 13 | minor | Gate leg 3's Enter can beat the rank | Leg 3 waits for the selected row |
| 14 | nit | Test design: the exact fact is an M4 witness; the ellipsis fact needs a competitor; Page Up; a name clash with T3; the "no" leg | Tests and mutation tables |
| 15 | nit | Two line ranges; the PR body's count line | Corrected; "Verification" |

## What delivery changed

Written as the build went; completed at READY.

- **The seed rule.** The spec's "a seed holds only at the publication of its own generation" was replaced, by the lead's acceptance of the worker's follow-up, with "a seed holds until the query changes": a seal between a list of choices opening and its first publication re-ranks the same empty query and must keep the current choice (`ASealBeforeTheChoicesPublishKeepsTheCurrentChoice`, red on the generation rule).
- **A failed rank ends the re-open gap** (T4), with `AFailedReOpenRankLeavesTheKeysOnTheRowsOnScreen`.
- **Call 10's premise was wrong.** Measured on `main`, after a publication the keys always land on the selected row of the new view; the split the spec described does not happen after a publication. It happens inside the list: the arrows move the selection and leave the keys (on `main` too, heard under NVDA; filed as #2057).
- **The keys rule was narrowed** after the diff red team: a row Tab lands on that is already the selection stays the palette's (otherwise Tab into the list and back brought #1834's cause 2 back), and a focus restored with no old focus element moves nothing (otherwise a window re-activation silently snapped the selection back to the keys' row).
- **The census** fails 33 spellings on `main` (18 whole names over 16 commands, and 15 tails), not 16 tails; its "losing tail" planted case runs against a stand-in ranker, since under the tail tier no catalog alone can make an unshared tail lose.
- **NVDA.** Base step 1 did not close a tab: Close Pane is unavailable with one pane, so Enter said "This command is not available right now." A click on another row is not silent under NVDA: NVDA names the clicked row by its focus; only the palette's "Selected:" line is gone. Step 9 ran the typed command 15 of 15 on head, so Call 9's alternative was not needed.
- **The diff red team and Codex.** The red team's major (Tab onto the palette's row made it the reader's) and its minors were taken. The press route replaced the "pointer button down" condition after Codex round 1, since a click on a row that already holds the keys and the selection raises neither the keys' event nor the selection's. Esc back and a failed rank now clear the first-selection flag (Codex rounds 1 and 2). Codex round 3 approved d50cdc21.
- **The letter search.** The red team read the list's letter search as matching every row's type name, and the build switched it to the labels. Measured, the type-name search moved nothing (on `main` and here), and the label search made a letter speak its row twice under NVDA, so the search is off (`ALetterTypedInTheListMovesNothing`). The found-on-the-way claim was withdrawn.
- **A right-button press** makes a row the reader's, as a left press does (the lead left it to the principal).
- **Spec claims that were wrong:** the census count on `main` (33 spellings, not 16 tails); Call 10's "keys and selection split after a publication" (they do not, measured); the type-name letter search as a live defect; the base NVDA line for step 1 (Close Pane is unavailable with one pane, so nothing ran); and M10's witness at `Choices.cs:180`.
- **The merge judge** (MERGE AFTER FIXES, issuecomment-7149589874): m3 and n1 taken in b2e313f2 (the letter fact starts on the fourth row, so it fails with the search switched back on; three comments and contract 28 no longer name a letter search or say `main` moved nothing); n2 left, since `WcMatrixEvidenceCensus` and contract 40's record name the journey; M1, m1, m2, m4, n3 and n4 ruled or noted by the lead.
- **Merged** as fce6a6e1 on 2026-11-24 (#2058, squash, pinned to b2e313f2), with every check green: 29 passed, 8 skipped.
