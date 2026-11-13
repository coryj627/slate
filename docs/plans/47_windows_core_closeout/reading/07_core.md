# Reading 07 — slate-core cluster and two Windows consumers

Baseline: worktree `<worktree>` at `1ec9bf31` (#1926).
Read-only. Nothing was built or run, so every statement about what a formula
"renders as" or what a statement "costs" is from reading code, a golden, or the
issue's own measurements. Each such statement is marked **unverified** where I
could not check it in the tree.

`git log --grep` for each of the ten numbers finds no merge. None is fixed on `main`.

Paths below are relative to the worktree unless they start with a drive letter.
Dependency sources read: `~/.cargo/registry/src/*/pulldown-latex-0.8.0`,
`mathcat-0.7.7` (the versions `Cargo.lock` pins).

---

## #1628 — three or more primes after a function are spoken as a product

- **Verdict:** stands. One correction: the issue says "none of the shapes MathCAT reads gives correct Nemeth". The cause of the wrong cell is a two-line typo in MathCAT's data, not a rule gap (below).
- **Current state:**
  - `crates/slate-core/src/math.rs:765` `fold_primes_into_superscripts`. `:851-859`: a run of 1 folds to `′`, 2 to `″`, anything else is pushed back loose (`out.extend(run); continue`). Doc comment `:751-754` names #1628.
  - Pinned by `math.rs:1149` `consecutive_primes_fold_into_one_superscript` (`:1156` asserts `f'''(x)`, `f''''(x)`, `f\dprime'(x)`, `f'\dprime(x)` contain no `<msup>`).
  - Golden: `crates/slate-core/tests/fixtures/math_golden/extra-expressions.txt:69` (`f'''(x)`), `expected.txt:2234-2244`: speech "f prime prime, prime, times x" under all six styles, Nemeth `⠋⠄⠄⠄⠷⠭⠾`.
  - MathCAT 0.7.7 `Rules/Braille/Nemeth/unicode-full.yaml:686` is `"‴": [t: "⠄⠄."]` and `:687` is `"⁗": [t: "⠄⠄.."]`. The `.` is a literal ASCII full stop where a third `⠄` belongs. Upstream `NSoiffer/MathCAT` `main` still has both lines today (read with `gh api`, 2026-11-13). The newest tagged release is v0.7.7 (2026-10-21).
- **Cause:** XB-8 kept the loose form to keep Nemeth right, because the folded `‴` form brailles that stray `.`. The loose form has no function before `(`, so MathCAT says "times".
- **Fix shape:** an owner decision between three (see below). Whatever is chosen lives in `math.rs` (`fold_primes_into_superscripts`, and `render_math_on_worker` `:543` if speech and braille are rendered from different trees), plus `extra-expressions.txt` and a regenerated `expected.txt`.
- **Failing test first:** `math.rs` tests, `three_primes_after_a_function_are_spoken_as_a_function`: `render_math("f'''(x)")` under ClearSpeak Medium contains "of x" and not "times"; braille under Nemeth equals `⠋⠄⠄⠄⠷⠭⠾` (the cells the loose form gives today). Then the golden diff shows `extra-expressions.txt:69` and any new entries (`f''''(x)`, `\sin'''(x)`).
- **Size:** S if the answer is "report upstream and leave"; M for the split render.
- **Lanes:** rust; swift (compile only). Windows lane starts because `crates/` changed.
- **Screen-reader work:** NVDA spot (one formula, speech and braille display).
- **Mac work owed:** if speech changes: `f'''(x)` and `f''''(x)` go from "f prime prime, prime, times x" to "f triple prime, of x" (wording **unverified**, taken from the issue's table). Consumer: `apps/slate-mac/Sources/SlateMac/MathView.swift:121` (label) and `:151` (braille); `Reading/ReadingView.swift:922`. No Swift literal pins it; `ParityHarnessTests.swift:729-732` compares against `parity_golden/math.md.json`, which has no primed formula. If the answer is "leave": none.
- **Owner decision needed:** yes. Question: how is `f'''(x)` rendered until MathCAT fixes its `‴` cell? Options: (1) report the typo upstream and leave Slate as it is (closes as "blocked upstream", contradicts the plan's goal); (2) fold to `‴` and accept `⠋⠄⠄.⠷⠭⠾` (wrong braille, needs #1560's reviewer); (3) render speech from the folded tree and braille, plus the MathML Windows exposes over UIA, from the loose tree. **Recommended: (3), and file the upstream report in the same PR's body.** It gives correct speech and correct Nemeth now and touches only formulas with three or more primes. Its cost is a second `set_mathml` for those formulas and a recorded divergence between `MathBlock.speech` and `MathBlock.mathml`. Whether NVDA's own MathCAT add-on, reading the exposed MathML, then says "times" during interactive navigation is **unverified** and belongs in the NVDA spot.
- **Shares files with:** #1629 (`math.rs`, `extra-expressions.txt`, `expected.txt`).

## #1629 — a scripted function after a letter, number or minus sign falls back to its source

- **Verdict:** stands, and it is wider than the reported strings (family in the cluster section).
- **Current state:**
  - pulldown-latex 0.8.0 `src/mathml.rs:641-648`: `Content::Function` writes `<mspace width="0.1667em" />` whenever `previous_atom` is `Inner`, `Close` or `Ord`. It does not look at `env_stack`.
  - `mathml.rs:431-446`: `Event::Script` opens `<msup>`/`<msub>`/`<msubsup>`/`<munder>`/`<mover>`/`<munderover>` before its base is written, so the space lands inside.
  - `mathml.rs:837-857` `set_previous_atom` is skipped only while the *last* slot of a script is being written; the base does set it. So a function used as the script *argument* also gets the space (after the base).
  - MathCAT 0.7.7 `src/canonicalize.rs:638-645`: `msubsup`/`munderover` must have 3 children, the other four exactly 2, else `bail!`.
  - `math.rs:550-557`: any MathCAT error other than too-large becomes `unrendered_speech` ("Math expression: …") with empty braille.
  - XB-8 already works around the same writer behaviour for primed functions by grouping the function (`Carrier.grouped`, `math.rs:867-870`; doc `:744-749`). Pinned by `math.rs:1227` `a_primed_function_after_an_ordinary_atom_still_renders`.
  - Not in any corpus today: no demo-vault note, field-pass block or extra expression has a scripted function after an atom (grep), so the golden has never shown it.
- **Cause:** as the issue says. One addition: a unary minus is written as `<mi>−</mi>` and sets `Ord` (`mathml.rs:719-743`), which is why `-\sin^2 x` fails and `a-\sin^2 x` does not.
- **Fix shape:** in `math.rs` only. Two workable shapes; pick after a step-0 probe.
  - (a) Repair the MathML string after `push_mathml` (the issue's option 2, extended): walk the tags; a literal `<mspace width="0.1667em" />` that is the first child of a script element is moved to just before that element's open tag, and one that is a later child is dropped. `escape_stray_markup` (`math.rs:996`) is the precedent for post-processing writer output. Property worth having: it only changes output MathCAT rejects today, so no working formula's MathML, speech or braille moves, and the golden diff is new entries only. It keeps the `<mo>⁡</mo>` the writer adds after the script.
  - (b) Group the function in the event stream (the issue's option 1, XB-8's mechanism): `Script, Begin, Function, End, …`. `Begin` resets `previous_atom` (`mathml.rs:147`). The function then sits in a group, so the writer adds no application mark (`mathml.rs:660-669`, `next_atom` sees `End`). Whether MathCAT still says "sine squared of x" for `2{\sin}^2 x` with no parentheses is **unverified**; MathCAT infers functions from names (`canonicalize.rs:3804 is_function_name`), and the golden's `2\sin'(x)` (`expected.txt`, extra line 73) says "of x" in that shape, but with parentheses. Applied only where needed it must mirror the writer's `previous_atom`; applied everywhere it changes the MathML of every working `\sin^2 x`.
  - **Recommended: (a)**, for the no-churn property. (b) for the script-argument position (`x_\max`) is also acceptable there, because the writer's application mark after `x_\max` is wrong anyway.
  - Add to `extra-expressions.txt`: `2\sin^2 x`, `x\sin^2 x`, `-\sin^2 x`, `\sin^2 x\cos^2 x`, `n\log_2 n`, `(a+b)\sin^2 x`, `\frac{1}{2}\sin^2 x`, `2\max_i a_i`, `x_\max`.
- **Failing test first:** `math.rs` tests, `a_scripted_function_after_an_ordinary_atom_still_renders`, modelled on `:1227`: for each source above, `render_math` speech does not start with "Math expression" and braille is not empty; for the `^2` cases speech contains "squared". A second fact, `no_script_element_has_the_wrong_number_of_children`, runs `latex_to_mathml` over the list and counts children of each script element.
- **Size:** M (the probe matrix, the golden review, the NVDA spot).
- **Lanes:** rust; swift (compile only).
- **Screen-reader work:** NVDA spot (two formulas in the reading view, speech and braille).
- **Mac work owed:** every formula in the family stops being spoken as "Math expression: <source>" with no braille and gets MathCAT speech and braille, e.g. `2\sin^2 x` becomes "2 sine squared of x" (**unverified** wording). Consumers as #1628: `MathView.swift:121`, `:151`. No Swift or C# literal pins an affected source (grep of `apps/slate-mac/Tests`, `apps/slate-windows/tests`); `ReadingMathTests.cs:560` pins a fallback but uses `{n \choose k}`, which stays a fallback.
- **Owner decision needed:** none.
- **Shares files with:** #1628.

## #1704 — a file first indexed by a save, a repair or an open never heals the dangling links that name it

- **Verdict:** stands. The issue's list is complete (enumeration in the cluster section).
- **Current state** (`crates/slate-core/src/session.rs`):
  - Create: `create_exclusive_binding` `:6467`; indexes at `:6547`, then `links_db::heal_after_create(&tx, path)` at `:6570` and a `LinksetChanged` per healed source `:6571-6582`.
  - Save: `save_text_locked_checked` calls `index_saved_file` at `:5006`. `index_saved_file` `:5384`, when `!existed`, stages `FileAdded` with `graph_inbound_rows` (`:5401-5408`), which reads rows whose `target_path` already equals the path (`links_db.rs:450-470`), not the NULL ones. No heal.
  - Repair: `reindex_path_locked` `:8681` → `index_file` `:8740`. No heal. Callers: `reindex_path` `:7777`, `repair_or_contain_path` `:7806`, `repair_abandoned_intents_locked` `:8488`.
  - Open base: `ensure_open_base_indexed` `:19351` → `index_file` `:19375`. No heal.
  - Open canvas: `open_canvas` `:21842`, its own `INSERT INTO files` at `:21866`. No heal.
  - The heal to reuse: `links_db.rs:604` `re_resolve_unresolved_links_naming`.
  - The comment the issue cites is at `session.rs:6559-6569`.
- **Cause:** as the issue says. WB-19 narrowed the create's heal; the other four paths never had one.
- **Fix shape:** `session.rs`, and a small helper in `links_db.rs` or `session.rs`: "heal the rows naming this new path and stage each healed source's linkset", the create's `:6570-6582` lifted out. Call it from `index_saved_file` when `!existed` (then drop the create's own call, keeping its `CREATE_HEAL_VAULT_WIDE` test seam), from `reindex_path_locked` and `ensure_open_base_indexed` when `index_file` inserted a new row, and from `open_canvas`'s `None` arm. `index_file` (`:11381`) needs to say whether the row was new (`replacing_existing_file`, `:11442`), because the scan also calls it (`:11019`) and must not run a per-file heal: the scan heals once at its tail (`:11136`).
- **Failing test first:** `crates/slate-core/src/session/tests/save_link_lookup.rs`, beside `a_create_heals_only_the_dangling_rows_that_could_name_it` (`:581`). Four facts, one per path: `a_first_save_heals_the_dangling_rows_that_name_it`, `a_repair_that_first_indexes_a_file_heals_…`, `an_open_base_that_first_indexes_a_file_heals_…`, `an_open_canvas_that_first_indexes_a_file_heals_…`. Each: `[[Draft]]` dangles in `A.md` after a scan; the file arrives by that path; `outgoing_targets(&session, "A.md")` is `[Some("Draft.md")]`; `HEAL_ROWS_VISITED` is 1. A fifth: a no-change rescan runs no per-file heal.
- **Size:** M.
- **Lanes:** rust; swift (compile only).
- **Screen-reader work:** none. It changes which rows resolve, not a sentence.
- **Mac work owed:** none to verify by ear. Behaviour note for the ledger: the Mac runs a scan after every structural change, so it already heals on that scan; with this fix a first save heals at once.
- **Owner decision needed:** none.
- **Shares files with:** #1917 and #1718 (`session.rs`); #1782 and #1917 (`links_db.rs`, lightly).

## #1705 — a target with a final sigma never resolves

- **Verdict:** stands. Its "needs a rebuild migration" is true only for one of two fix shapes.
- **Current state** (`crates/slate-core/src/link_resolver.rs`):
  - `LinkCandidateSelector::new` `:520`: exact mode lowercases the target, then appends each extension to the lowered string (`:523-527`); basename mode stores `normalized.to_lowercase()` (`:531`).
  - `probe` `:544-559` builds `format!("{basename}.{ext}")` from the already-lowered basename.
  - `offer` `:562`: lowercases the whole path (`:567`) or the whole final component (`:578`). `basename_matches` `:690-699` tests `file_lower.starts_with(basename_lower)`.
  - `[[ΚΟΣΜΟΣ]]` → `κοσμος`; `ΚΟΣΜΟΣ.md` → `κοσμοσ.md` (`.` is case-ignorable, `m` is cased, so the sigma is not final). No key matches.
  - The SQL pre-filter has the same mismatch: `links_db.rs:244-247` folds the probe keys with `db::tree_sort_key` (`db.rs:440-443`, NFC then `to_lowercase`), and the stored index key of the file name is `κοσμοσ.md`.
  - The heal already compensates on its side: `links_db.rs:612-624` tries the stem folded alone and the folded form with its extension stripped. `save_link_lookup.rs:667` `a_create_heals_a_target_whose_stem_folds_apart_from_its_path` pins that `[[οδοσ]]` (typed with a medial sigma) resolves to `ΟΔΟΣ.md`. The fix must keep that.
  - Rust's `to_lowercase` has one context rule, Final_Sigma. So "any context-dependent lowercase mapping" is the sigma only.
- **Cause:** a target's stem is lowercased alone and its extension glued on afterwards, while a path is lowercased whole.
- **Fix shape:** `link_resolver.rs` only. Build the selector's keys so both sigma forms at the end of the stem are tried: for each implied extension, `lower + "." + ext` (today's key) and `format!("{normalized}.{ext}").to_lowercase()` (the path's context); the same two forms when the target carries a Markdown extension. Basename mode compares the file's lowered name against that key list instead of `starts_with`. `probe()` returns the same list, so `FoldedNameLookup`, `InMemoryVaultIndex`'s `CandidateTable` (`:124-146`) and `MoveMapping`'s keys (`link_rewrite.rs:70-76`) all line up without change. **No stored key changes, so no migration.** `candidate_rows_sql` takes a variable number of placeholders (`links_db.rs:294`), so more keys cost nothing structural.
  - Rows written unresolved before the fix heal at the next scan's tail (`session.rs:11136`), which runs on every scan. **No rescan to force.**
  - The alternative, a real fold (`ς` → `σ` inside `tree_sort_key`), is the one that needs the rebuild migration the issue describes, for the six indexes in migrations 033 (`:16`, `:28`), 037 (`:20`, `:22`), 039 (`:18`) and 041 (`:25`, `:28`). It also moves the tree's sort order for Greek names on both hosts. Not recommended here.
- **Failing test first:** `link_resolver.rs` tests, `a_target_and_a_file_that_differ_only_by_final_sigma_resolve`: the matrix target ∈ {`ΚΟΣΜΟΣ`, `κοσμος`, `Κοσμος`, `ΚΟΣΜΟΣ.md`, `dir/ΚΟΣΜΟΣ`} × file ∈ {`ΚΟΣΜΟΣ.md`, `κοσμος.md`}, each resolving through `resolve_link` on an `InMemoryVaultIndex`. A session fact in `save_link_lookup.rs`, `a_save_resolves_a_final_sigma_target_through_the_fold_indexes`, proves the SQL pre-filter finds the row.
- **Size:** S to M.
- **Lanes:** rust; swift (compile only).
- **Screen-reader work:** none.
- **Mac work owed:** none by ear. Ledger note: such a link shows as resolved on the Mac too once it runs this core.
- **Owner decision needed:** small. Question: selector keys (no migration, covers the stem-end sigma in both directions) or a sigma-insensitive fold with a rebuild migration? **Recommended: selector keys.** Residual, stated in the PR: a name whose *inner* word ends in an upper-case sigma followed by `.`, `'` or `:` is not covered; I could not construct a realistic one.
- **Shares files with:** none of this cluster (`link_resolver.rs` alone).

## #1782 — a link snippet can start mid-word

- **Verdict:** stands. Confirmed in core, so it does affect the Mac.
- **Current state:**
  - `crates/slate-core/src/links_db.rs:942-953` `snippet_around`: ±60 **bytes** around the link span (its doc says chars), rounded to char boundaries, newlines replaced by spaces. No word boundary, no mark. A char boundary is not a grapheme boundary, so it can also split a combining sequence or an emoji sequence.
  - Stored at index time: `links_db.rs:383`, column `links.snippet`.
  - Read-time ceiling: `links_db.rs:692-705` `bound_snippet` (4096 bytes, appends `…`), whose comment says it caps on read "without a migration".
  - Goldens that pin today's mid-word snippets: `crates/slate-core/tests/fixtures/parity_golden/links.json` (for example "ck-embed rows target. ^blk …", "e the corpus. …"), `mutation_golden/s3_rewrite_interruption.json`, `s4_undo_round_trips.json`. Rust unit tests `links_db.rs:1027-1060`.
- **Cause:** a fixed byte window.
- **Fix shape:** two places it can go; an owner decision picks.
  - (a) At read (`links_db.rs`, the row mappers at `:707` and in `backlinks_for` `:805` and `unresolved_links` `:877`): a `present_snippet` that knows from `span_start` whether the left edge was cut (`span_start > 60`) and from the right context's length whether the right was, drops the partial edge token and adds `…`. No stored format change, no migration, every existing row is fixed at once. Cost: when the cut happened to fall on a boundary it drops one whole word. `backlinks_for`'s SELECT needs the span columns added.
  - (b) At index (`snippet_around`): snap using the neighbouring text (exact), using `unicode_segmentation` word bounds (already a dependency, `palette.rs:35`). Existing rows change only when their file is re-indexed, and the scan's fast path does not look at `parser_version` (`session.rs:11474-11519`), so reaching old rows needs a migration that resets `mtime` (precedents: 015, 018, 034). That migration forces one full re-index per vault and clears the change feed (`db.rs:616-624`).
  - Both regenerate the three goldens.
- **Failing test first:** `links_db.rs` tests: `a_snippet_never_starts_or_ends_inside_a_word` (a 300-byte paragraph with the link in the middle: the snippet starts with `…` then a whole word and ends with a whole word then `…`); `a_snippet_at_the_start_of_a_note_has_no_leading_mark`; `a_snippet_never_splits_a_grapheme`. Then the parity golden diff.
- **Size:** M.
- **Lanes:** rust; windows (the parity and mutation censuses: `apps/slate-windows/tests/SlateWindows.Tests/Censuses/ParityHarnessCensus.cs`, `MutationHarnessCensus.cs`, `tools/ParityHarness`); swift (compile, and `ParityHarnessTests.swift:1134-1184`, `MutationHarnessTests.swift:710` compare the same fixtures).
- **Screen-reader work:** NVDA spot on Backlinks, Outgoing links and Connections rows (whether NVDA voices `…`).
- **Mac work owed:** a Mac user hears the row's context start and end on whole words, and may hear "ellipsis" depending on VoiceOver's punctuation setting. `apps/slate-mac/Sources/SlateMac/BacklinksPanel.swift:91` builds the spoken label "Backlink from X, context: <snippet>" in Swift (Windows mirrors it at `Panels/RightPanePanelsViewModel.cs:1163`); `:80`, `OutgoingLinksPanel.swift:95`, `Graph/ConnectionsPanel.swift:277`, `:447-451` show it. Tests: the two harness files above (fixtures regenerate); `ConnectionsPanelTests.swift:167-175` uses its own literals and is unaffected.
- **Owner decision needed:** yes. (1) Where: read or index? **Recommended: read (a)**, for no migration and the `bound_snippet` precedent. (2) The mark: **recommended `…`**, which `bound_snippet` and the Mac test literals already use. (3) Raw Markdown in the snippet (`| |`, `[[…]]`): **recommended out of scope**; stripping it is a feature.
- **Shares files with:** #1917 (`links_db.rs`); #1704 if its helper lands there.

## #1834 — a typed command name can run a different command from the palette

- **Verdict:** stands, with a second cause the issue does not name.
- **Current state:**
  - `crates/slate-core/src/palette.rs:237` `palette_sections`: scores label and hint, keeps the max (`:256-261`), sorts globally (`:273-276`), then groups in `SECTION_ORDER` (`:86-99`, `:278-295`). So the first row is the best row of the first non-empty section.
  - A Rust test already states the problem as a host's job: `palette.rs:746` `scores_expose_the_global_ranked_order_across_sections` ("a weak hint-only match in an early section must not outrank a strong label match in a later one once rows are re-sorted by the exposed score"). No host re-sorts.
  - Contract `docs/plans/28_palette_contracts.md` P1 (`:34-41`) gives core the section order; P7 (`:112-123`) says the selection is **preserved when its id survives a query change** and snaps to the first row only when it vanished or was null.
  - Windows applies P7 at `apps/slate-windows/src/SlateWindows/CommandPaletteViewModel.cs:1610-1618`. The Mac applies the same rule at `apps/slate-mac/Sources/SlateMac/CommandPaletteModel.swift:65-69`.
  - `CommandPaletteRowViewModel.Score` (`CommandPaletteViewModel.cs:140-145`) is exposed and unused, as the issue says.
- **Cause:** two. (1) Section order comes before score. (2) The selection made silently at open, or at the first keystroke, is kept for as long as that row keeps matching, and hints are whole sentences, so it often does. Fixing the ranking alone does not guarantee Enter runs the best match: if the row selected at open (the first Recent row) still subsequence-matches the finished query, it stays selected. **By reading; not reproduced.**
- **Fix shape:**
  - Core (`palette.rs`): for a non-empty query, order the sections by their best row (declared order breaks ties), and rank a label match above a hint-only match of equal score, an exact label match first. No FFI change, no new string. P1's "section order" sentence changes.
  - Hosts: P7 amended so that a selection the reader has not moved follows the first row at each publication, and only a selection they moved (arrows, pointer) is preserved. Windows: `CommandPaletteViewModel.cs` (`:1610` and the user-select path `:1639`). Mac: `CommandPaletteModel.handleQueryChange` — owed.
  - Contract text: `docs/plans/28_palette_contracts.md` P1, P7, and P10 if the count sentence changes.
- **Failing test first:** `palette.rs` tests, `an_exact_label_match_is_the_first_row_whatever_its_section` (a View command whose hint subsequence-matches "settings" and a Settings command labelled "Settings": `sections[0].rows[0].command.id` is the Settings one). Windows, `CommandPaletteTests`: `TypingACommandsNameAndPressingEnterRunsThatCommand` (open with a Recent row whose hint matches the query, type the name, Enter, assert the invoked id).
- **Size:** M.
- **Lanes:** rust; windows; swift (compile only, plus the Swift palette tests that run real core ranking: `CommandPaletteViewTests.swift:693`, `:934`, `MilestoneQIntegrationTests.swift:161` — small fixtures, probably unaffected, **unverified**).
- **Screen-reader work:** NVDA full (typing journeys: the count, the selection sentences, Enter).
- **Mac work owed:** (1) heard and seen now, from core: for a typed query the sections appear in best-match order, so the first row and the row Enter runs can differ from today. (2) Adoption owed: the P7 change in `CommandPaletteModel.swift:65-69`, with `CommandPaletteViewTests.swift:693` `testSelectionResetsToFirstWhenQueryFiltersOutCurrent` and its neighbours. Until the Mac adopts it, the Mac keeps cause (2).
- **Owner decision needed:** yes, three parts. (a) Ranking: sections by best match (recommended), or a synthetic "Best match" section (a new spoken header on both hosts), or "hints count only when no label matches" (partial). (b) P7: an untouched selection follows the first row (recommended). (c) Whether the count sentence should name the selected row, since an automatic selection is otherwise silent before Enter: **recommended no change** in this plan; it is a new sentence on both hosts.
- **Shares files with:** nothing in this cluster. Outside it: `CommandPaletteViewModel.cs` with #1694, #1695 and #1665.

## #1837 — Windows: Quick Open and search may have the palette's stall under NVDA

- **Verdict:** stands as written ("may"). The cause is present by reading in both surfaces; how often it trips needs measurement under NVDA.
- **Current state:**
  - The palette's fix landed in `e1ff2272` (#1840): `apps/slate-windows/src/SlateWindows/CommandPaletteResultsPresenter.cs:111` (`_list.UpdateLayout()` at the end of `Refresh`) and `:166` (end of `SyncSelection`). Fact: `apps/slate-windows/tests/SlateWindows.Tests/CommandPaletteTests.View.cs:42` `TheRowsAreLaidOutInTheOperationThatPublishesThem`, with the `PendingLayout` walker at `:76`. Record: `docs/plans/28_palette_contracts.md:855`.
  - Quick Open: `QuickSwitcherViewModel.ApplyRanked` (`QuickSwitcherViewModel.cs:523-551`) clears and refills the `ObservableCollection` `Results` (`:96`), up to `DisplayCap` = 50 (`:26`), then raises `SelectedRow`. The list binds it (`MainWindow.xaml:2984-2994`, `SelectedItem` two-way) while `QuickSwitcherSearchTextBox` (`:2963`) holds focus. No layout call follows.
  - Search: `SearchOverlayViewModel.PublishResults` (`Search/SearchOverlayViewModel.cs:847-853`) clears and refills `Rows` (`:123`), bound at `MainWindow.xaml:3152-3161`, focus in `SearchOverlaySearchTextBox` (`:3089`). `MainWindow.Search.cs:286-288` scrolls the selection into view. No layout call follows.
  - `grep UpdateLayout` over `apps/slate-windows/src/SlateWindows` finds none in either surface.
- **Cause:** DS-10's: rows changed under a focused WPF `TextBox` leave layout and automation events pending for the render pass, and TSF's `GetTextExt` can run them inside its lock.
- **Fix shape:** Windows only. Each surface flushes the list's layout in the dispatcher operation that publishes its rows and again after the selection's scroll: `QuickSwitcherViewModel.cs` (a "published" signal after `ApplyRanked`), `Search/SearchOverlayViewModel.cs`, and the code-behind that owns the lists (`MainWindow.Search.cs`, the Quick Open partial of `MainWindow`). A small presenter like the palette's would give one place for it.
- **Failing test first:** hosted facts with the shipped XAML lists, same shape as the palette's: `QuickOpenRowsAreLaidOutInTheOperationThatPublishesThem` and `SearchRowsAreLaidOutInTheOperationThatPublishesThem`: publish 50 rows with nothing pumped, assert `PendingLayout(list)` is empty. By reading both fail today.
- **Size:** S to M for the two surfaces.
- **Lanes:** windows.
- **Screen-reader work:** NVDA spot, as DS-10's probe: open the surface under NVDA after a Settings visit, read a property of the field through UIA, time it, N runs before and after. The deterministic fact carries the fix if the probe never trips.
- **Mac work owed:** none.
- **Owner decision needed:** none. One note for the plan: the same shape exists in other search-field surfaces (list in the cluster section); the plan can make this a sweep.
- **Shares files with:** nothing in this cluster.

## #1917 — cached joins on files.path re-prepare on every new binding under SQLite 3.54

- **Verdict:** stands, with one misdescription: three of the four bases temporal statements are not cached at all.
- **Current state:**
  - #1918 (`dbf8480a`) rewrote the scan's probe as `WHERE file_id = (SELECT id FROM files WHERE path = ?1)` (`session.rs:11475-11480`), cached the fast path's `UPDATE` (`:11504-11516`), and added `session/tests/scan.rs:1551` `a_rescan_prepares_its_per_file_statements_once` with the helper `statements_reprepared_more_than` (`:1525`), which reads `SQLITE_STMTSTATUS_REPREPARE` for every statement on the connection.
  - `tags_db.rs:117-130` `load_dql_tags_for_path`: `prepare_cached`, join on `f.path = ?1`. As the issue says.
  - `bases/engine.rs` `SqlVaultLookup`: `oplog_has_change_since` `:3804`, `oplog_has_property_change` `:3813`, `oplog_deleted_content_matches` `:3831` all go through `exists_query` (`:3693-3708`), which calls `Connection::query_row` — **a fresh prepare on every call, under any SQLite version**. Only `oplog_deleted_content_matches_regex` (`:3874-3880`) uses `prepare_cached`. The issue's "9,999 / 0" for this row was measured "through `prepare_cached`", which is not how three of them run.
  - `session.rs:21674-21683` `DbTitleSource::title_for`: uncached `query_row`, as the issue says.
  - The full list is in the cluster section.
- **Cause:** SQLite 3.54's re-prepare on a new binding for this join shape (the issue's measurement; I did not re-measure).
- **Fix shape:** the #1918 remedy applies to every statement on the list: name the file by the nested id lookup, and move the uncached ones to `prepare_cached`. Files: `tags_db.rs`, `bases/engine.rs`, `links_db.rs`, `session.rs`. Then a guard in two parts: a runtime fact per hot path using `statements_reprepared_more_than` (move the helper to `session/tests/common.rs`), and a source census that fails on any SQL literal that joins `files` and filters it by a bound path.
- **Failing test first:** `session/tests/bases.rs` (or `oplog_temporal.rs`): `a_bases_view_prepares_its_per_row_probes_once` — a 40-note vault, a view filtered by `oplog.has_change_since`, then `statements_reprepared_more_than(&conn, 2)` is empty. Today the three uncached probes leave no statement to count, so the fact must also assert the probe's SQL is *in* the statement cache; say so in the spec. `a_dql_query_prepares_its_tag_lookup_once` for `load_dql_tags_for_path`. The census: `crates/slate-core/tests/` beside the other source censuses, `no_cached_statement_joins_files_on_a_bound_path`.
- **Size:** M.
- **Lanes:** rust; swift (compile only).
- **Screen-reader work:** none.
- **Mac work owed:** none.
- **Owner decision needed:** none. QPSG for every connection is not recommended: it turns off plan choices that read bound values, for a problem a rewrite removes.
- **Shares files with:** #1704 and #1718 (`session.rs`), #1782 (`links_db.rs`).

## #1718 — interactive reads wait behind a running scan's connection lock

- **Verdict:** stands; a design question. Two details differ from the issue's text.
- **Current state:**
  - `session.rs:1293` `conn: Mutex<Connection>`; `scan_session` `:3612` takes it at `:3623` and holds it through `scan_vault` and the maintenance after the commit (reconcile, events rebuild, age-out, compaction sweep).
  - `scan_vault` `:10860`: `count_files` at `:10918` is a full directory walk with no database work, done **under the mutex**; one `begin_fenced` IMMEDIATE transaction from `:10928` to the commit at `:11200`; a cancel anywhere rolls the whole scan back.
  - Quick Open: `switcher_rank_top_indexed` `:6702`; `self.conn.lock()` at `:6716` cannot be cancelled, `cancel.check()` follows at `:6717`. It copies every `(path, name)` out under the lock and scores after it.
  - A second connection per session already exists: `worker_conn` (`:1298`), opened by `open_worker_connection` (`:1612-1629`) through the anchored path, with its own `busy_timeout` and registered functions.
  - "`db.rs` falls back when WAL is unavailable": there is no fallback code. `apply_pragmas` (`db.rs:445-478`) sets `journal_mode=WAL` and never reads back what SQLite chose. The anchored Windows VFS is pinned as WAL by `db/anchored_windows.rs:712-715`.
  - The doc sentence the issue quotes is at `session.rs:3396-3397`.
- **Cause:** one connection, one mutex, and a scan that is one transaction.
- **Fix shape / options:** in the cluster section, with sizes and a recommendation.
- **Failing test first** (for the option recommended): `session/tests/rescan.rs`, `a_quick_open_rank_answers_while_a_scan_holds_the_writer`: park the scan at the `"file"` scan point (`scan_point_test_hook`, `session.rs:10793`), run `switcher_rank_top_indexed` on another thread with a deadline, assert it returns the pre-scan rows. Today it blocks until the hook releases.
- **Size:** L.
- **Lanes:** rust; windows (Quick Open's "Searching files…" path); swift (compile only).
- **Screen-reader work:** NVDA spot (Quick Open during a rescan no longer sits on "Searching files…").
- **Mac work owed:** none by ear. The Mac gets the same behaviour from core; an entry on the ledger that a VoiceOver check of Quick Open during a rescan is owed if this lands.
- **Owner decision needed:** yes: which option, and where it is delivered. Recommendation in the cluster section.
- **Shares files with:** #1704, #1917 (`session.rs`); `db.rs` alone otherwise.

## #1727 — Windows: a case-only rename on FAT32 or exFAT reports success but keeps the old spelling

- **Verdict:** stands. Two line numbers drifted.
- **Current state:**
  - The fact: `crates/slate-core/src/vault/weak_volume_tests.rs:611` `weak_volume_case_only_rename_keeps_the_entry` (ignored unless `SLATE_WEAK_VOLUME_*` is set), body `case_only_rename_keeps_the_entry` `:482`, the failing assert `:491`. It loops over the volumes and stops at the first failure, as the issue says.
  - The rename: `vault/fs.rs:553` `rename_beneath_root` (Windows). `:618-630` admits an occupied destination only when it is the source itself (`is_same_entry`, `fs.rs:248`; `fs_root.rs:1019-1031`). `:637` calls `windows_entry::rename_into(…, false)`; `:644-647` retries with replacement when the volume reports the alias occupied. Either way the kernel's `Ok` is returned as is. `rename_into` is now `vault/windows_entry.rs:774` (the issue says `:497`).
  - Nothing reads the entry's stored spelling afterwards. A reader for it exists: `fs_root.rs:1038` `stored_spelling_beneath` (used by `canonical_path`, `fs.rs:2335`).
  - The index then takes the requested spelling verbatim: `session.rs:17506` (`rename_with_identity`), `:17551-17554` (`UPDATE files SET path = ?1 …`), and the link rewrites follow.
  - The identity-checked rename is not reached on FAT32 or exFAT: no `FILE_ID_INFO`, so it refuses first (`docs/plans/43_mac_backlog_delivery/24_structural_identity_capability_matrix.md:172`). The same document, `:222`, records that the fallback file index **changes when the entry is renamed** on FAT32.
- **Cause:** the volume ignores a case-only rename and reports success; Slate trusts the report.
- **Fix shape:** `vault/fs.rs` (`rename_beneath_root`, Windows cfg) and `weak_volume_tests.rs`. After the kernel call, when the rename was a case-only alias of the source (`destination_is_source`), read the stored leaf through the pinned destination folder and compare it with the requested leaf. If it differs, return a refusal and change nothing else. Use an existing variant, `VaultError::InvalidArgument { message }`, so there is no FFI change: `a11y.rs:3591` `vault_error_detail` passes its message through verbatim. The sentence is core-owned and lives in Windows-cfg code.
  - The honest outcomes:
    - **Refuse and say so** (the issue's option 1). Safe, small, needs no probe to be correct.
    - **Two steps through a temporary name, then verify, then refuse if it still did not take** (option 2). By reading it has three problems: on FAT32 the tunnel cache can bring the old long name back within 15 s for a name that fits 8.3 (**unverified**; the issue raises it); a crash between the two renames leaves the note under a hidden name with no journal entry for it; and on FAT32 each rename changes the file index WB-28 uses as identity (doc 24 `:222`). It needs the VHD probe before it can be planned.
  - The fact records each volume before asserting.
- **Failing test first:** not gated, runs on NTFS: `vault/fs.rs` tests (Windows), `a_case_only_rename_the_volume_ignored_is_refused`. Add an `after_kernel_rename` seam beside `before_kernel_rename` (`rename_with_hook_for_tests`, `fs.rs:2771`); in it, rename the entry back to its old spelling to stand in for a volume that ignored the request; assert the provider returns the refusal and the listing still has the old spelling. A session fact in `session/tests/structural.rs`: the index row keeps its old path after the refusal. The gated fact then passes on the VHDs by expecting the refusal there.
- **Size:** S, plus the owner's elevated VHD session for the evidence.
- **Lanes:** rust (Windows cfg, so the Windows runner's Rust job is the one that proves it); windows if a host test covers the sentence.
- **Screen-reader work:** NVDA spot (the refusal is spoken once, through the existing rename-failure route).
- **Mac work owed:** none. The code is `cfg(windows)`, and macOS FAT32 performs the rename (doc 24 §6). The sentence never reaches a Mac.
- **Owner decision needed:** yes. (1) Refuse, or two steps? **Recommended: refuse.** (2) The sentence. Proposed: "This drive keeps the capitalization a file was created with, so Note.md was not renamed to note.md." (3) Whether a folder's case-only rename gets the same check: **recommended yes**; it takes the same path. That FAT32 ignores a folder's case-only rename too is **unverified**.
- **Shares files with:** nothing in this cluster.

---

## Cluster section

### Math: one fix or two, and the family

Two fixes in one function's neighbourhood. #1629 is a writer defect with a mechanical repair and no decision. #1628 is a choice between speech and braille caused by an upstream data typo. They share `math.rs`, `extra-expressions.txt` and `expected.txt`, so they should regenerate the golden once.

**#1629's family: a script element with the wrong number of children, so the whole formula falls back.** By reading the writer; none of it run.

| # | Shape | Examples | Why |
|---|---|---|---|
| F1 | A function as the **base** of any script, after an atom that leaves `Ord`, `Inner` or `Close` | after a number `2\sin^2 x`; a letter `x\sin^2 x`, `\sin^2 x\cos^2 x`, `\sin^2\theta\cos^2\theta`, `\pi\cos^2 t`; a unary minus `-\sin^2 x`, `(-\sin^2 x)`; a closing bracket `(a+b)\sin^2 x`, `\left(a\right)\sin^2 x`; a group, fraction or root `\frac{1}{2}\sin^2 x`, `\sqrt{2}\cos^2 x`, `{a}\sin^2 x`; an earlier script `x^2\sin^2 x`; text `\text{if }\sin^2 x`; a bar `\|x\|\sin^2 x` | leading `<mspace>` inside the element |
| F1 by script kind | `^` (`msup`), `_` (`msub`: `n\log_2 n`, `2\log_{10} x`), both (`msubsup`: `x\log_2^2 n`), and limits in display style (`munder`, `mover`, `munderover`: `2\max_i a_i`, `c\lim_{n\to\infty} a_n`, `a\liminf_n x_n`) | | all six tags share the code path |
| F1 by function | the 24 names at pulldown-latex `parser/primitives.rs:137-139`, the movable-limit names `:140-154` (`lim`, `Pr`, `sup`, `max`, `inf`, `gcd`, `min`, `liminf`, `limsup`), `\operatorname{…}` `:156-168`, `\bmod` `:169` | | all are `Content::Function` |
| F2 | A function as an **ungrouped script argument** | `x_\max`, `T_\min`, `x^\ln` — even at the start of a formula | the base sets `Ord`, the space is written before the argument. `x_{\max}` is fine (`Begin` resets). |
| F3 | Any other event the writer counts as a slot but that writes no element or a stray one | `x^\rm a` (a state change fills the slot, `msup` has one child), `x^\, 2` (a space is the slot) | `push_mathml` takes any event as a slot (XB-8's `a_state_change_counts_as_the_argument_it_fills`, `math.rs:1275`). Rare; recommend out, and named in the PR. |

Not in the family (they render): `a-\sin^2 x`, `a+\cos^2 x`, `=\sin^2 x` (binary operators and relations do not set `Ord`); `\sin\cos^2 x`; `2\sin x` (no script); `2\sin'''(x)` (loose, no script).

**#1628's family: primes that are not folded.**

| Shape | Today | Note |
|---|---|---|
| three or more `'` after a carrier: `f'''(x)`, `f''''(x)`, `\sin'''(x)` | loose, "times" | the issue |
| mixed glyphs that count to three or more: `f\dprime'(x)`, `f'\dprime(x)`, `f\trprime(x)`, `f\qprime(x)` | loose | `primes_in` counts `‴` as 3 and `⁗` as 4 (`math.rs:787-798`) |
| primes then a superscript: `y''^2`, `f'^2` | `y''^2` is "y prime, prime squared" (`expected.txt`, extra line 77) | the parser attaches `^2` to the last prime; TeX reads `(y'')^2`. Pinned on purpose by XB-8 as an "edge carrier". A sibling, not in either issue; **recommend an owner row**: fold it as `{y''}^2` or leave it. |
| a prime after `\left…\right`, a delimiter or an environment: `\left(fg\right)'(x)` | loose, "f g prime, times x" (extra line 75) | deliberate in XB-8; listed so the fix does not widen by accident |

### Links: every path that inserts a `files` row

Production `INSERT INTO files` sites (the other 40-odd matches are tests):

| Site | Reached from | Heals dangling rows naming the path? |
|---|---|---|
| `session.rs:5443` `index_saved_file` | `save_text_locked_checked` `:5006` | **No** (#1704) |
| same | `create_exclusive_binding` `:6547` | Yes, `:6570` |
| `session.rs:11579`, `:11653` `index_file` | `scan_vault` `:11019` | Yes, at the scan's tail `:11136` (vault-wide) |
| same | `reindex_path_locked` `:8740` | **No** (#1704) |
| same | `ensure_open_base_indexed` `:19375` | **No** (#1704) |
| `session.rs:21866` `open_canvas` | host | **No** (#1704) |

A path can also arrive by `UPDATE files SET path`: `:13742`, `:14087`, `:15048`, `:17522`/`:17552`, and the folder-move helpers `:18674`, `:18749`. Each sits in a transaction that then runs the vault-wide `re_resolve_unresolved_links` (`:13761`, `:14107`, `:15068`, `:17635`). So #1704's list of four is complete.

### Links: every place the resolver case-folds

| Where | Fold | Affected by the sigma? |
|---|---|---|
| `link_resolver.rs:523-527`, `:531`, `:552-555` selector keys and probe | lower the target, then append the extension | **Yes — the defect** |
| `link_resolver.rs:567`, `:578`, `:690-699` `offer`, `basename_matches` | lower the whole path or name | the other side of it |
| `link_resolver.rs:84`, `:88`, `:138-139` default `candidate_paths`, `CandidateTable` | whole path, whole name | consistent with `offer`; fixed by fixing the keys |
| `link_resolver.rs:673-679` `collect_basename_matches` | as `basename_matches` | yes; used by the formatter |
| `link_resolver.rs:282-314` `format_wikilink_for_path` | stems lowered alone on both sides; then `collect_basename_matches` | degraded: by reading it falls back to the qualified form with the extension, e.g. `[[/ΚΟΣΜΟΣ.md]]`, which does round-trip |
| `links_db.rs:244-247` SQL pre-filter | `tree_sort_key` over the selector's keys | yes, same keys |
| `links_db.rs:612-624` heal keys | both forms already | no |
| `link_rewrite.rs:70-76` `MoveMapping` | whole path, whole name | lines up once the keys do |
| `session.rs:18522-18533` `stem_of` (rewrite candidates) | stem lowered alone, both sides | no |
| `graph.rs:37-49` `ghost_key` | whole target | no (a key for unresolved targets only) |
| Embeds | the same selector (`FoldedNameLookup`) | **yes**, fixed with the links |
| Heading anchors: `session.rs:7170-7185`, `lib.rs:465-470`, `embeds.rs:416`, `:451` | both sides whole, trimmed | no |
| Tags: `tags_db.rs:269`, `bases/engine.rs:1620` | whole tag; `/` is not case-ignorable | no |
| Aliases | there is no alias resolution of links in core | not applicable |
| Collision checks: `session.rs:4479-4480`, `:13606`, `:14440`, `:15494` | whole path both sides | no |
| Quick Open: `switcher.rs:79`; the sidebar filter (`tree_sort_key` over names) | lower the name with its extension | **a sibling**: a query typed `κοσμος` meets the name key `κοσμοσ.md`, so the last character does not match. Not a resolver; recommend a line in the plan, not this PR. |

**Migration or rescan for #1705:** none with the recommended selector-key fix. A change to `tree_sort_key` itself would need a rebuild of the six expression indexes in migrations 033, 037, 039 and 041.

### SQLite: every cached statement with an equality on `files.path` in a join

Found by scanning every SQL literal under `crates/` that mentions `files` and a bound path (the script is in the scratchpad, `work07/sqlscan.py`).

| Statement | Where | Prepared | Hot? | Remedy |
|---|---|---|---|---|
| DQL tags | `tags_db.rs:121-127` | cached | per row of a DQL evaluation that reads tags (`bases/engine.rs:4007`, `bases/eval.rs:1766`) | nested id lookup |
| `oplog.has_change_since` | `bases/engine.rs:3806` | **uncached** | per row of a view that uses it (`eval.rs:1858`) | cache + nested id |
| `oplog.has_property_change` | `engine.rs:3818` | **uncached** | per row (`eval.rs:1879`) | cache + nested id |
| `oplog.deleted_content_matches` | `engine.rs:3833` | **uncached** | per row (`eval.rs:1900`) | cache + nested id |
| `…_matches_regex` | `engine.rs:3876` | cached | per row | nested id |
| Canvas title | `session.rs:21677` | **uncached** | per file card, each scan's canvas pass and each canvas open | cache + nested id |
| Graph linkset | `links_db.rs:427` `graph_linkset_for` | cached | per healed or moved source while the graph is live (`session.rs:6577`, `:11145`, `:14113`, `:15074`, `:17641`); a batch move can call it thousands of times | nested id |
| Outgoing links | `links_db.rs:669`, `:751`, `:760` | cached | once per note panel load (`session.rs:6916`, `:7611`, `:7635-7638`) — not hot | nested id (cheap, same edit) |
| Outgoing counts | `links_db.rs:785`, `:790` | cached | as above | nested id |
| Open-base probe | `session.rs:19355` | uncached | once per base open | leave, say why |
| Linked-from source clause | `bases/engine.rs:983` | part of one compiled query | once per query | leave, say why |

Same shape, **not measured by the issue** — the fix PR's first step should run them through `statements_reprepared_more_than`:

- joins with an equality on `links.target_path`: `links_db.rs:455` `graph_inbound_rows` (once per new file in a scan while the graph is live, `session.rs:11455`), `links_db.rs:818` `backlinks_for`, `bases/engine.rs:2086` `load_backlinks` (per row, `eval.rs:1787`, `:2700`);
- `session.rs:12555` `get_file_summary_impl`: cached, a CTE on `files WHERE path = ?1` joined in the projection; a host calls it per path;
- `bases/engine.rs:3926` `oplog_untouched_for`: a single table with a correlated `NOT EXISTS`, uncached.

### #1718: the options

| | A. A read-only second connection | B. The scan yields the lock between batches | C. An in-memory snapshot for Quick Open | D. Shrink the window (not a fix) |
|---|---|---|---|---|
| What | Interactive reads lock a reader connection. In WAL it sees the last commit while the scan writes. | The scan commits every N files and releases the mutex. | The session keeps the `(path, name)` list in memory, refreshed at commits; the rank reads it without the connection. | Run `count_files` before taking the mutex; make the rank's wait for the mutex cancellable. |
| Touches | `session.rs` (a `read_conn`, opened like `worker_conn`; each moved read's lock), `db.rs` (read back `journal_mode`; a smaller cache for the reader), `db_cache_status` and the two-sessions guards | `scan_vault`'s single transaction, cancel-rolls-back-everything, the prune, the re-resolve tail, the graph sink, the change feed scope, `WatchScan` | `session.rs`, `switcher.rs` | `scan_session` and `switcher_rank_top_indexed` |
| Write-intent protocol (doc 21) | Safe if the moved reads are not marker-gated. Task reads must stay on the writer: "new task-read entry points must sweep first … and read from one snapshot", and the sweep writes. The design has to list which reads INV4 governs. | High risk. The fences rely on the writer lock being held across the scan; a move or save between batches can make the prune delete a live row; two processes' cold scans interleave. | None. | None. |
| #1909 page caches | A third connection per session (writer, worker, reader), each with its own cache up to `cache_size`. The guards (`two_sessions.rs`, #1926) and `db_cache_status` must count it; give it a small cache. | None. | None in SQLite; about 100 bytes per file of heap. | None. |
| Non-WAL | No fallback code exists today. Add one check: if `PRAGMA journal_mode` is not `wal`, open no reader and keep today's path. | n/a | n/a | n/a |
| Busy and cancel | A WAL reader is not blocked by the writer; set `busy_timeout` as the worker does. Cancellation already works per connection (`directory_page::with_sqlite_cancellation`). One reader still serializes reads behind each other. | Reads still wait up to one batch. | n/a | The wait becomes cancellable, not shorter. |
| Also unblocks saves during a scan? | No | Yes | No | No |
| Size | L in phases: Quick Open first (M), then tree and tag pages, then search | L, and the riskiest | M, Quick Open only; it re-creates the list WB-25 removed from the host, inside core | S |

**Recommendation.** Option A, as its own plan, phased with Quick Open first. It is a change to the session's connection model with the write-intent protocol and the page-cache guards in its blast radius, and it wants its own red team and Codex rounds; inside plan 47 it would be the largest chunk and would share `session.rs` with three others. Inside plan 47: write the design spec and the decision row, and take D only if the owner wants an interim (it is small, safe by reading, and does not close the issue).

### #1837: the same shape elsewhere (for a sweep)

WPF `TextBox` fields with a list that republishes while they hold focus (`SearchGlyph` users): `SidebarFilterTextBox` (`MainWindow.xaml:1259`), `BibliographySearchBox` (`:2247`), `QuickSwitcherSearchTextBox` (`:2963`), `SearchOverlaySearchTextBox` (`:3089`), the Bases quick filter (`Bases/BaseSurfaceView.cs:130`), the canvas filter (`Canvas/CanvasSurfaceView.cs:170`), the graph filter (`Graph/GraphSurfaceView.cs:108`). The palette's own field (`MainWindow.xaml:3382`) is the only one with the flush. None of the seven others is measured.

### Recommended cut into PRs

| PR | Issues | Why together / alone | Lanes | Review |
|---|---|---|---|---|
| 1 | #1629, and #1628 once its decision is answered | one function, one golden regeneration, one NVDA spot, two ledger lines | rust, swift | red team |
| 2 | #1704 | alone: it is on the brief's Codex list and edits `session.rs` | rust, swift | red team + Codex |
| 3 | #1705 + #1782 | different files (`link_resolver.rs`, `links_db.rs`), one NVDA spot over the link panels, both add ledger lines | rust, windows, swift | red team |
| 4 | #1917 | alone: performance, no speech; touches `session.rs` and `links_db.rs`, so **after** PRs 2 and 3 | rust, swift | red team |
| 5 | #1834 | alone: an owner decision, a contract change, NVDA full, and it shares `CommandPaletteViewModel.cs` with the seal chunk | rust, windows, swift | red team + Codex suggested |
| 6 | #1837 | alone, split from #1834: no shared file and different evidence. Could grow into the search-field sweep. | windows | red team |
| 7 | #1727 | alone: Windows cfg in `vault/`, needs the owner's VHD session | rust | red team |
| — | #1718 | a design spec and a decision row; delivery in its own plan | docs | Codex on the design |

Order: PRs 1, 6 and 7 share no file with anything and can go in any wave. PR 2, then PR 3, then PR 4 (`session.rs`, `links_db.rs`). PR 5 when its decision is answered and not in the seal chunk's wave.

Shared files: `session.rs` — #1704, #1917, #1718. `links_db.rs` — #1782, #1917, (#1704). `math.rs` and the math golden — #1628, #1629. `link_resolver.rs` — #1705. `palette.rs` — #1834. `vault/fs.rs` — #1727.

### Mac ledger lines this cluster adds

| Issue | What a Mac user hears or sees differently |
|---|---|
| #1629 | scripted-function formulas (the family above) get MathCAT speech and braille in place of "Math expression: <source>" |
| #1628 | `f'''(x)` is spoken as a function, if the decision changes speech |
| #1782 | link-row context starts and ends on whole words, with `…` where cut; `BacklinksPanel.swift:91` speaks it |
| #1834 | typed-query section order follows the best match; and adoption of the P7 change in `CommandPaletteModel.swift` |
| #1705, #1704 | no sentence changes; links that used to stay unresolved now resolve |
| #1718 | none unless delivered |
| #1837, #1917, #1727 | none |

### Risks

- **The math goldens are the only evidence for speech I could not run.** Every "becomes …" above is unverified until `SLATE_MATH_GOLDEN_UPDATE=1` is run and the diff read. If option (a) for #1629 turns out to leave MathCAT unhappy with a leading space *before* a script (it accepts `2\sin x`, which has the same space outside a script, so I expect not), fall back to (b).
- **#1834 is not fixed by a core reorder alone.** If the plan takes only the ranking change, typing a name can still run another command on both hosts when the row selected at open keeps matching. The decision row must cover P7.
- **#1782 changes three golden fixtures that three harnesses read** (Rust, C#, Swift). Regenerating them in the PR keeps all three lanes green; a hand edit will not.
- **#1917's hot bases probes are uncached today**, so a runtime re-prepare counter sees nothing for them. The guard has to assert presence in the statement cache as well as the count, or it passes vacuously.
- **#1704's heal inside `index_file` must not run during a scan.** A per-file heal on a cold scan is 2 indexed lookups per new file on top of the tail's vault-wide pass; the fifth fact guards it.
- **#1727's evidence needs the owner's elevated VHD session.** The NTFS fact proves the check; only the VHDs prove the volumes.
- **#1705's selector change widens the probe's key list** (up to 9 keys). The production statement takes any number (`links_db.rs:294`); the fixed `IN (?1 … ?5)` at `db.rs:827` is a migration test's `EXPLAIN` and only needs to keep proving the index is used.
- **#1718 inside plan 47 would break the brief's second anti-goal** (a PR too large for its review) unless it is only the design.
