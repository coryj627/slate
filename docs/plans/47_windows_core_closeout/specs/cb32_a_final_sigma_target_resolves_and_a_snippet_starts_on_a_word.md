# CB-32: A target with a final sigma resolves, in links and in Quick Open's name match, and a link snippet starts and ends on a word

Issues: #1705, #1782. Size M to L. Lanes: rust (the change and its facts), windows (a change under `crates/` runs the lane in full; two twin facts and the parity golden, which the Windows harness writes), swift (compile and the lane's suite, which compares the same parity golden; no Swift edit). AT: an NVDA spot check (plan row). Wave: C9. Parallel with: CB-15c (Principal V) and CB-17a (this principal, after this chunk). Decision: CD22 as the owner answered it on 2026-11-16 (§3.1): **a new fold stored in the index, with an index rebuild, not selector keys**; snippets trimmed at read to a word boundary and marked with "…"; stripping raw Markdown stays out. Interlock: `links_db.rs` goes CB-31 (merged), CB-32, then CB-33; `db.rs` goes CB-32 (the fold's migration), then CB-45b (plan §6). Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9).

Written 2026-11-21 from `reading/07_core.md:81-123` and `:271-292`, the two issues, CB-31's spec, and step 0 below. Every `path:line` was re-read at 47bcf77e (`main` on 2026-11-21, after #2030). Step 0 ran at 47bcf77e in this chunk's worktree with its own Cargo target directory. Statements still by reading say so.

`D` is `crates/slate-core/src/db.rs`; `R` is `crates/slate-core/src/link_resolver.rs`; `L` is `crates/slate-core/src/links_db.rs`; `S` is `crates/slate-core/src/session.rs`; `W` is `apps/slate-windows/`; `M` is `apps/slate-mac/Sources/SlateMac/`.

## Goal

1. A link whose target and file differ only in how a capital sigma lowercases (`[[ΚΟΣΜΟΣ]]`, `[[κοσμος]]`, `[[Κοσμος]]`, `![[ΚΟΣΜΟΣ]]` against `ΚΟΣΜΟΣ.md`; `[[Οδος]]` against `ΟΔΟΣ.md`) resolves on every route: save, scan, create, rename, move, and an embed's link row and its rendered preview. A rename rewrites it.
2. Quick Open and the Files sidebar filter find a name whichever sigma the reader typed or the name carries (`κοσμος` finds `ΚΟΣΜΟΣ.md`; `ΚΟΣΜΟΣ2` finds `κοσμος2.md`).
3. The fold that does it is the one core already stores in its indexes, `slate_tree_sort_key`, now folding final sigma (U+03C2) to sigma (U+03C3). Each vault's indexes over that fold are rebuilt once, by migration 042, at the first open by this build.
4. A link row's context (Backlinks, Outgoing links, Connections, the CLI's JSON) starts and ends on a whole word, with "…" on each side that was cut. Nothing is re-indexed for it: the trim happens when the row is read.

## Drift check (the plan and the reading against 47bcf77e)

| Claim | At 47bcf77e |
|---|---|
| Brief: "CD22, the owner's call as recommended: selector keys (no migration)" | **Wrong.** §3.1's CD22 row (2026-11-16) and the §10 row both record the owner's departure: a fold stored in the index with a rebuild. The lead confirmed on 2026-11-21 that the brief copied the recommendation column. This spec follows §3.1 |
| Plan and reading: "the six expression indexes in migrations 033, 037, 039 and 041" | **Seven.** 033 builds two (`idx_dirs_parent_tree` at `033:14`, `idx_files_parent_tree` at `033:21-30`); 037 two; 039 one; 041 two. Step 0 read them from `sqlite_master` (`sql LIKE '%slate_tree_sort_key%'`) |
| `tree_sort_key` `db.rs:440-443` (NFC then `to_lowercase`) | Holds |
| Selector keys `R:520-531`, `probe` `R:544-559`, `offer` `R:562-578`, `basename_matches` `R:690-699` | Hold within two lines; the lowercasing sites are `R:84`, `:88`, `:138-139`, `:282`, `:285`, `:302`, `:306`, `:310`, `:314`, `:523`, `:531`, `:567`, `:578`, `:673`, `:679` (and the test double `:1398`, `:1402`) |
| SQL pre-filter `links_db.rs:244-247` | Moved: `L:237-270` (keys folded at `L:255`), statements `L:303-316` |
| Heal keys `links_db.rs:612-624` | Moved: `L:613-631` and `L:654-670` (CB-31's `heal_rows_naming_first_indexed` at `L:568`) |
| `MoveMapping` `link_rewrite.rs:70-76` | Holds |
| "Quick Open: `switcher.rs:79`" | **Wrong site.** `switcher.rs:79` only strips the extension in `display_name`. Quick Open matches through `fuzzy_score` (`palette.rs:143`) and its `grapheme_key` (`palette.rs:211-218`), which lowercases per character with no context, so `Σ` always becomes `σ` and a typed or authored `ς` never meets it |
| Sidebar filter "(`tree_sort_key` over names)" | Holds: `sidebar_filter.rs:389-393` folds the word with `tree_sort_key`; the row side is `slate_effective_name_key` (`D:412-438`), which ends in `tree_sort_key` |
| `session.rs:18522-18533` `stem_of` (rewrite candidates): "no" | Moved to `S:19100-19110` (inside `collect_rewrite_candidates`, `S:19089`). **Affected**: it lowers a stem alone, so `stem_of("ΟΔΟΣ.md")` is `οδος` and `stem_of("οδοσ")` is `οδοσ` |
| `graph.rs:37-49` `ghost_key`: "no" | Holds as a site; its doc says its fold is "EXACTLY the resolver's comparison convention", but frozen graph contract 0b-3 pins it as `to_lowercase` (`docs/plans/35_graph_contracts.md:1376-1386`), so it is not changed here (Call 3b) |
| The scan's tail heal "`session.rs:11136`, on every scan" | Moved to `S:11658` (`re_resolve_unresolved_links_under`); not gated on a partial scan (`S:11609`), skipped only on cancel. Every open scans first on both hosts (`W/src/SlateWindows/VaultLifecycleViewModel.cs:725`; `M/AppState.swift:10950-10955`, `:12661`) |
| `snippet_around` `links_db.rs:942-953` | Moved to `L:992-1003`: ±60 bytes, rounded outward to char boundaries, line breaks replaced by spaces (byte length kept) |
| Written at `links_db.rs:383` | Moved to `L:392`; spans stored at `L:403-404`, the same offsets `snippet_around` read |
| `bound_snippet` `links_db.rs:692-705` | Moved to `L:744-755`; it also bounds `display_text` (`L:785`) and task text (`tasks_db.rs:424`, `:432`), so the word trim must not go inside it |
| Row mappers `:707`, `backlinks_for` `:805`, `unresolved_links` `:877` | `outgoing_links_for` `L:714` and `outgoing_links_bounded` `L:794` (both select the spans); `backlinks_for` `L:855` (its SELECT `L:868` has no spans); `unresolved_links` `L:927` (SELECT `L:938`, no spans, and no `bound_snippet`; no host calls it) |
| Goldens pinning mid-word snippets | `parity_golden/links.json` pins 45 backlink snippets (outgoing snippets are not serialized, `W/tools/ParityHarness/SurfaceSerializer.cs:996-1013`); the two mutation goldens hold whole short notes ("See [[a]]. ", "Link: [[renamed2]]. "), never cut, so they do not change |
| The comment in `041` (and `R:631-633`): "A stale stored key makes SQLite report the index corrupt on the UPDATE or DELETE that cannot find it" | **Wrong on the SQLite we bundle** (3.54.1, libsqlite3-sys 0.39.1): step 0 E1. Lookups miss silently; nothing errors |

## Step 0, as run on main (47bcf77e), 2026-11-21

**P1, the defect, through the session** (debug; a temporary fact in `session/tests/`, removed after; source kept with the working notes). A vault holds `ΚΟΣΜΟΣ.md`, `κοσμος2.md`, `ΟΔΟΣ.md`, `Λογος.md`, and `A.md` with `[[ΚΟΣΜΟΣ]] [[κοσμος]] [[Κοσμος]] [[ΚΟΣΜΟΣ.md]] [[οδοσ]] [[Οδος]] [[ΛΟΓΟΣ]] [[ΚΟΣΜΟΣ2]] ![[ΚΟΣΜΟΣ]]`; `B.md` has a link in the middle of a 90-character run of Greek letter names on each side; `C.md` has `Lead. [[ΟΔΟΣ]] and [[οδοσ]] tail.`

| Probe | Result on `main` |
|---|---|
| `A.md`'s links after a cold scan | `ΚΟΣΜΟΣ`, `κοσμος`, `Κοσμος`, the embed `ΚΟΣΜΟΣ` and `Οδος` **dangle**; `ΚΟΣΜΟΣ.md`, `οδοσ`, `ΛΟΓΟΣ` and `ΚΟΣΜΟΣ2` resolve |
| Quick Open (`switcher_rank_top_indexed`) | `κοσμος` finds only `κοσμος2.md` (**misses `ΚΟΣΜΟΣ.md`**); `οδος` finds nothing (**misses `ΟΔΟΣ.md`**); `ΚΟΣΜΟΣ2` finds nothing (**misses `κοσμος2.md`**); `ΚΟΣΜΟΣ`, `κοσμοσ`, `ΚΟΣ`, `ΟΔΟΣ` find their file |
| Sidebar filter (`filter_files`) | `κοσμος` and `ΚΟΣΜΟΣ` find both; `κοσμοσ`, `ΚΟΣ` and `λογοσ` find **nothing**; `ΛΟΓ` finds `Λογος.md` |
| Backlinks of `Λογος.md` | from `A.md`: `"Κοσμος]] [[ΚΟΣΜΟΣ.md]] … ![[ΚΟΣΜΟΣ]] "` (starts inside a link); from `B.md`: `"on zeta eta … pi rho [[Λογος]] alpha beta … iota kappa la"` (**starts inside "epsilon", ends inside "lambda"**) |
| `rename_file("ΟΔΟΣ.md", "Δρομος.md")` | `C.md` becomes `Lead. [[ΟΔΟΣ]] and [[Δρομος]] tail.`: `[[οδοσ]]` is rewritten, `[[ΟΔΟΣ]]` is left (it never resolved) and now dangles for good |

**P2, the rebuild's cost** (release; a standalone probe crate on the worktree's `slate-core`, `db::open_database` so the pragmas match production; AMD Ryzen 7 9800X3D, 16 logical processors, NVMe; median of 3, a fresh copy of a checkpointed version-41 database each run; the feed filled to its 100,000-row cap). Migration 042's work, in one IMMEDIATE transaction: DROP and CREATE of the seven indexes, the `schema_version` row, the change-feed clear every migration runs (`D:618-627`), commit.

| Vault (files / dirs / links, 10% dangling) | DB file | Today's no-op open + migrate | Migration 042 total | of which the seven indexes / the feed clear / commit |
|---|---|---|---|---|
| 10,000 / 1,000 / 100,000 | 81 MiB | 2.5 ms | **162 ms** | 141 / 13 / 8 ms |
| 50,000 / 5,000 / 500,000 | 324 MiB | 2.8 ms | **1,055 ms** | 880 / 16 / 161 ms |
| 100,000 / 10,000 / 1,000,000 | 630 MiB | 3.0 ms | **2,206 ms** | 1,782 / 17 / 408 ms |

REINDEX by name costs the same within noise. The cost is linear in rows; each index costs 0.4 s or less at 100,000 files. Warm OS cache; cold-cache and Mac timings were not measured. After the rebuild `PRAGMA integrity_check` is `ok` at every size.

**E1, no rebuild.** A version-41 database with Greek rows, opened with the new fold registered and no rebuild: every UPDATE and DELETE succeeds; a lookup by the new key through `idx_files_path_fold` returns 0 rows where the old key returns 1; `integrity_check` lists the rows missing from all seven indexes. Deleting a row whose key carries `ς` took 13.2 ms against 0.66 ms (100,000 files): SQLite's `sqlite3VdbeFindIndexKey` fallback scans for the entry. So skipping the rebuild gives wrong answers, not errors, and that is what the migration's fact pins.

**E2, a downgrade.** An older build opening a version-42 cache: `db::migrate` returns `UnsupportedVersion { db_version: 42, runner_max: 41 }` and leaves the database alone; the open fails with core's sentence, after `VaultError`'s "database error: " prefix: "database is at schema version 42, newer than this build's max version 41; upgrade Slate or restore the vault from a backup". Windows says "Could not open vault: " and that sentence, at high priority, and returns to the welcome view (`W/src/SlateWindows/VaultLifecycleViewModel.cs:777-800`, `:2622-2627`); the Mac shows it in its "Could Not Open Vault" alert (`M/AppState.swift:11006-11013`, `M/WelcomeView.swift:54-62`). This is what every migration since #7 does; nothing new.

## Design

### 1. One fold, in a pure module

- A new module `crates/slate-core/src/name_fold.rs` (no database or I/O, so the pure modules that use it do not import `crate::db`): `pub(crate) fn fold_case(text: &str) -> String`, `text.to_lowercase()` then U+03C2 → U+03C3 (only when the string holds one, so other names allocate as today), and `pub(crate) fn fold_sigma(c: char) -> char` for the per-character matcher. Its doc says why: Unicode's Final_Sigma is the one context rule in `to_lowercase`, so a stem lowered alone and the same stem lowered with its extension (`ΚΟΣΜΟΣ` against `ΚΟΣΜΟΣ.md`) fold apart. It does not claim the volumes agree: **NTFS keeps `κοσμος.txt` and `κοσμοσ.txt` apart** (the red team created both in one folder on this machine's C:, and neither opens under the other's spelling), while APFS, by reading only (no Mac here), folds with Unicode CaseFolding (`03C2; C; 03C3`) and treats them as one name.
- `tree_sort_key(name)` becomes `fold_case(&nfc(name))`. The registered `slate_tree_sort_key` (`D:365-373`) follows it with no change of its own, and so does `slate_effective_name_key` (`D:412-438`).
- `register_connection_functions`'s doc (`D:330-337`) names migration 042 beside 033 as the rebuild that follows a fold change. The other docs that describe the fold as "NFC + full-Unicode lowercase" follow: `S:13118-13125`, `S:19201`, `S:7482-7488`, `L:646-652`, `sidebar_filter.rs:16`.
- **What the fold costs in a vault that already holds a `ς`/`σ` pair** (legal on NTFS and on case-sensitive volumes; by reading, not on APFS). Such a pair now behaves as any two case variants do today (`Note.md` beside `note.md` on a case-sensitive volume): a link naming either resolves by the resolver's existing tiebreak, the byte-smaller path, where today `[[Οδος]]` beside `ΟΔΟΣ.md` and `Οδος.md` finds the exact `Οδος.md`; the collision gate refuses a new twin as the portability guard it is documented to be (it refuses names that would alias on a case-insensitive volume, `migrations/037_path_fold_indexes.sql:6-14`; APFS would alias them); and the gate's `LIMIT 1` lookup (`S:19214-19221`) can return the twin instead of the source, refusing a case-only rename such as `ΟΔΟΣ.md` to `οδοσ.md` while `οδος.md` also exists. Accepted in Call 1, as the cost of one fold.

### 2. Migration 042, the rebuild

`crates/slate-core/migrations/042_tree_key_folds_final_sigma.sql`: `DROP INDEX IF EXISTS` then the identical `CREATE INDEX` text for all seven indexes, in 033, 037, 039, 041 order, with 033's replay discipline. The text is copied byte for byte, the line break before 041's `WHERE` included, because `D:2039-2045` asserts 041's index text and keeps doing so after 042. Its comment says why, and corrects 041's claim (step 0 E1: a stale key gives wrong answers silently on the bundled SQLite; integrity_check sees it). Migrations are append-only, so 041's comment is not edited. `MIGRATIONS` gains the entry.

- **When it runs:** inside `db::migrate`'s one IMMEDIATE transaction at the first open of each vault by this build (`S:2732`), after the structural lock and before the scan, like every migration. Once per vault; a later open finds version 42 and does nothing.
- **What it costs:** step 0 P2: 0.16 s at 10,000 notes, 1.1 s at 50,000, 2.2 s at 100,000 notes with a million links, on this machine.
- **Another process during the rebuild:** the transaction holds the writer lock for its length, so a second process (the CLI, a second Slate) that opens the vault meanwhile waits on rusqlite's 5-second busy timeout and gets "database is locked" only past it, which by step 0's line is a vault of about 230,000 notes.
- **What the reader sees and hears:** nothing new (Call 2). Windows opens off the UI thread with its existing "Opening <root>…" status, the welcome view's opening status and the indeterminate progress bar, and speaks nothing until the vault opens (`VaultLifecycleViewModel.cs:226`, `:633-645`, `:702`). The Mac opens on the main actor synchronously (`M/AppState.swift:10660`, `:10705`), so the window does not respond for the rebuild's length; that is true of every migration and of the open itself today.
- **What else every migration does:** the change feed's cursors and rows clear (`D:618-627`), so each host's first page after the open is a full one, as after any upgrade.
- **Links left dangling before the upgrade** heal at the first scan's tail (`S:11658`), which every open runs. A row that resolved to a different file before (two files differing only by `ς`/`σ` in different folders) is not revisited until its source is re-indexed; accepted, as CB-31 accepted the same for a create.
- **Downgrade:** step 0 E2. An older Slate refuses the vault with core's existing sentence. An older Slate still running on the vault while a newer one migrates it is fenced from writing (`SchemaVersionSkew`, `D:691-712`); by reading, its reads through the rebuilt indexes can miss Greek-named rows until it restarts, which its next write's refusal tells it to do.

### 3. The resolver and the rewrite use the same fold

- `R`: every lowercasing site listed in the drift table calls `crate::name_fold::fold_case` in place of `to_lowercase()`: the selector's keys (`:523`, `:531`), `offer` (`:567`, `:578`), `basename_matches`/`collect_basename_matches` (`:673`, `:679`), `candidate_paths` and `CandidateTable` (`:84`, `:88`, `:138-139`), `format_wikilink_for_path` (`:282-314`) and the test double (`:1398`, `:1402`). `probe` builds its keys from the folded basename, so `FoldedNameLookup`, the SQL pre-filter (`L:255`, through `tree_sort_key`) and the heal keys (`L:654-670`) agree with no edit. `R:631-633`'s comment is corrected as 042's is.
- `link_rewrite.rs:70-76` (`MoveMapping`): `fold_case`.
- `S:19100-19110` (`collect_rewrite_candidates`'s `stem_of`): `fold_case`, so a rename finds every source whose link names the moved stem under either sigma.
- **Not changed: `graph.rs:36-50` (`ghost_key`).** Frozen graph contract 0b-3 (`docs/plans/35_graph_contracts.md:1376-1386`, frozen at `:1087`) pins the ghost key as Rust `to_lowercase`, byte-stable across generations; only the owner's recorded call can amend it. Its code doc's "EXACTLY the resolver's comparison convention" stops being true for `ς`/`σ`, so the doc says so and names the #1955 line (Call 3b). The cost until then: two notes linking the missing `[[ΑΓΝΩΣΤΟΣ]]` and `[[αγνωστοσ]]` draw two ghosts.
- Not changed: heading anchors, tags, property values and slugs (their folds compare whole strings on both sides, or are ASCII; the reading's table, `07_core.md:286-287`, and step 0's survey).

### 4. Quick Open folds the same way

`palette.rs:211-218` (`grapheme_key`): after the per-character lowercase, `name_fold::fold_sigma` (one copy of the rule). Both sides of every comparison pass through it, so `κοσμος`, `ΚΟΣΜΟΣ` and `κοσμοσ` all meet `ΚΟΣΜΟΣ.md` and `κοσμος2.md`. Match spans are computed on the target's own bytes (`palette.rs:157-183`), so the highlighted ranges are unchanged in length. The command palette shares the matcher; its labels carry no sigma. The sidebar filter is fixed by Design 1 (both its sides go through `tree_sort_key`).

### 5. The sort order moves only where two names meet at a sigma

A name moves only against a name that matches it up to a `ς`/`σ` position: they used to part there (`ς` U+03C2 sorts before `σ` U+03C3) and now compare on what follows. A common pair flips in core's order: `ΝΟΜΟΣ2.md` sorts before `ΝΟΜΟΣ.md` today (keys `νομος2.md` and `νομοσ.md`) and after it once both read `νομοσ…`. Names equal after the fold tie and are ordered by their binary path. On Windows the visible order does not move: the Files sidebar re-sorts every level with `OrdinalIgnoreCase` (`W/src/SlateWindows/FilesSidebarViewModel.cs:1421-1437`), which already equates the two sigmas. On the Mac, folders keep core's order (`M/FileTreeLevelWorker.swift:21-23`), so folders named like `ΝΟΜΟΣ` and `ΝΟΜΟΣ2` swap; files are re-sorted with `localizedStandardCompare` (`M/Sidebar/SidebarOrganization.swift:449-492`), which already equates them. The #1954 line says so (plan §3.1 asks for it).

### 6. Snippets are trimmed when a row is read

A new `fn present_snippet(snippet: String, span_start: u32, span_end: u32) -> String` in `L`, beside `snippet_around`, which it inverts:

- **Where the link sits in the stored text.** `snippet_around` started at `span_start - 60` rounded down to a char boundary, or at 0. So the link's offset `k` in the stored text lies in `min(60, span_start)..=min(63, span_start)`, and the stored text cannot say which (the rounding's skip is invisible once the source is gone). A candidate `k` is one where `k` and `k + span_len` are char boundaries, the text at `k` starts with `[`, `!` or `<` and the byte before `k + span_len` is `]`, `)` or `>`. Every span the parser stores has that shape: `[[…]]`, `![[…]]`, `[…](…)`, `![…](…)`, and pulldown-cmark's autolinks `<https://…>` and `<a@b.co>` (`links.rs:239-262`; the red team's probe). When more than one candidate qualifies (brackets beside the link), each edge takes the one that trims less: the left cut and left guard use the largest `k`, and the right-cut test and right guard use the smallest. No candidate (a row this rule did not write) returns the text bounded and otherwise untouched.
- **Left cut** (`k < span_start`, so `snippet_around` did not start at 0): the snippet starts at the first word-like segment (a UAX #29 word segment holding an alphanumeric character; `unicode_segmentation`, already a dependency, `palette.rs:35`) that follows a non-word segment and starts at or before `k`; leading whitespace and punctuation before it are dropped; prefix "…". If none exists before `k` (one unbroken word of 60 bytes or more), the edge is the first grapheme boundary instead, and still takes its mark.
- **Right cut** (the right context, `snippet.len() - (k + span_len)` with the smallest candidate, is at least 60 bytes): mirrored: the snippet ends at the end of the last word-like segment that precedes a non-word segment and ends at or after `k + span_len`; trailing whitespace and punctuation after it are dropped; suffix "…". A right context under 60 bytes means the note ended, so nothing is cut or marked.
- Word-like segments start and end on grapheme boundaries (UAX #29 WB4), so a trimmed edge never splits an accent or a ZWJ emoji sequence, and a flag pair that `snippet_around` cut in half is dropped with the partial segment (it is not word-like). Segmentation runs only over the two edges, at most 63 bytes each, so a megabyte alias costs nothing extra.
- **The ceiling.** After the trim, the text between the marks is bounded to `MAX_LINK_SNIPPET_BYTES` minus the leading mark's bytes, rounded down to a grapheme boundary; a truncation by the ceiling takes the right "…" (once, whether or not the right side was also cut). So a row is never longer than today's ceiling plus one "…" (`session/tests/links_embeds.rs:511`) and never carries two marks on one side. `bound_snippet` itself is unchanged and keeps serving `display_text` and the task rows.
- **Callers.** `outgoing_link_from_row` (`L:757-790`, both outgoing reads and `note_load_bundle`, the CLI's path), `backlinks_for` and `unresolved_links` call it; the last two gain `links.span_start, links.span_end` in their SELECTs (`L:868`, `L:938`). The CLI's JSON backlinks (`crates/slate-cli/src/commands/links.rs:102-107`) and both hosts' rows get it through the session.
- **What it costs a reader, accepted in CD22:** when a cut fell exactly on a word boundary, one whole word is dropped (the stored text cannot tell); a note that ends 60 to 63 bytes after a link reads as cut and loses its last word under a "…". The raw Markdown in a snippet (`| |`, `[[…]]`) stays (CD22).
- **Mark shape:** "…" joined to the word with no space, the shape `bound_snippet` already writes.

### 7. What is not changed

No FFI change, no event, no new sentence (the rows' sentences are the hosts' own, "Backlink from X, context: …", `W/src/SlateWindows/Panels/RightPanePanelsViewModel.cs:1168`, `M/BacklinksPanel.swift:91`), no Swift or C# product source, `a11y.rs` and the corpus, the trigger ledger, the parity matrix, any manifest or lockfile, any workflow. `index_file` (CB-31's `Result<bool, VaultError>`) is not called by the rebuild: the rebuild is SQL in the migration.

## Tests

Red first: the branch's first commit adds the facts with the product unchanged; each is seen red on `main`'s code for the reason given, then the change lands. No fact uses a fixed wait.

**Core, the fold and the rebuild (`D` tests).**

1. `the_tree_key_folds_both_sigmas_to_one`: `tree_sort_key` gives one key for `ΚΟΣΜΟΣ`, `κοσμος`, `Κοσμος` and `κοσμοσ`, and for `ΚΟΣΜΟΣ.md` and `κοσμος.md`; ASCII, accented and CJK samples keep today's keys. `every_slate_connection_registers_the_exact_tree_sort_key` (`D:889`) gains the Greek samples, so the registered SQL function is held to the Rust one. Red: the keys differ.
2. `migration_042_rebuilds_every_index_over_the_tree_key`: `migrate_up_to(41)` (which registers this build's fold, `D:651`), then the old fold is registered over it on that connection, then the rows are inserted: `Φιλοσοφία/Λόγος.md`, a folder `Λόγος`, and dangling `[[Κόσμος]]`, `[[ΚΟΣΜΟΣ]]` and `[[notes/Λόγος]]`; then `migrate`, which re-registers this build's fold and applies 042. `integrity_check` is `ok`, and an indexed lookup by the new key (each statement's `EXPLAIN QUERY PLAN` uses its index, as `D:790` and `D:827` do) finds each row through each of the seven indexes. Red: with the fold changed and 042 left out, `integrity_check` lists the missing rows and the lookups find 0 (step 0 E1). Mutation: drop any one index from 042.
3. `every_index_over_a_slate_function_is_rebuilt_by_the_last_fold_migration`: the names of the indexes whose `sql` calls any `slate_` function (`slate_tree_sort_key`, `slate_effective_name_key`, `slate_link_target_*`) after `migrate` equal 042's list, so a later index over the fold that a future fold change forgets fails here. A guard, green once 042 exists; mutation: a scratch index over the fold in a test-only later migration (`migrate_with_later_migration_for_tests`) turns it red.
3b. `D:2026`'s `migrate(...) == 41` becomes `== 42` (it goes red the moment 042 exists); its assertions on 041's index text stay.

**Core, links (`R` and `session/tests/save_link_lookup.rs`).**

4. `a_target_and_a_file_that_differ_only_by_final_sigma_resolve` (`R` tests): targets `ΚΟΣΜΟΣ`, `κοσμος`, `Κοσμος`, `ΚΟΣΜΟΣ.md`, `dir/ΚΟΣΜΟΣ`, `/dir/ΚΟΣΜΟΣ` × files `ΚΟΣΜΟΣ.md`, `κοσμος.md`, `dir/ΚΟΣΜΟΣ.md`, each through `resolve_link` on an `InMemoryVaultIndex`. Red: every bare `ΚΟΣΜΟΣ` form against `ΚΟΣΜΟΣ.md` dangles (step 0 P1). (`[[ΚΟΣΜΟΣ2]]` against `κοσμος2.md` already resolves on `main` and stays as a guard cell.)
5. `a_save_resolves_a_final_sigma_target_through_the_fold_indexes`: P1's `A.md` saved in a scanned vault; every link resolves through the SQL pre-filter (`RESOLUTION_ROWS_VISITED` counts the candidate rows, as `a_save_resolves_its_links_from_its_own_targets_rows_only` does), the embed's row included. Red: five dangle.
5b. `an_embed_of_a_final_sigma_target_renders`: the render path, `resolve_embed_link_target` (`S:7487`), finds `ΚΟΣΜΟΣ.md` for `![[κοσμος]]` and `![[ΚΟΣΜΟΣ]]` (through the session's embed preview call). Red: unresolved.
6. `a_scan_heals_a_final_sigma_link_left_dangling`: a scanned vault whose `[[ΚΟΣΜΟΣ]]` row is set to NULL by one `UPDATE links` (the state an upgrade leaves); a `rescan_with_progress` resolves it. Red: it stays NULL.
7. `a_create_heals_a_final_sigma_target`: `A.md` links `[[Κοσμος]]`; `create_exclusive("ΚΟΣΜΟΣ.md")` heals it (CB-31's helper, through migration 041's indexes). Red.
8. `renaming_a_final_sigma_note_rewrites_every_link_that_names_it`: `C.md` holds `[[ΟΔΟΣ]] [[Οδος]] [[οδοσ]]` and `D.md` holds only `[[οδοσ]]`; `rename_file("ΟΔΟΣ.md", "Δρομος.md")` rewrites every spelling in both to `[[Δρομος]]` and all resolve; a second arm moves `ΟΔΟΣ.md` into a folder with `move_file`. Red: `[[ΟΔΟΣ]]` and `[[Οδος]]` are left (step 0 P1), and `D.md` is no rewrite candidate (its stem `οδοσ` against the moved stem `οδος`). Mutations: `stem_of` back to `to_lowercase` (bites through `D.md`); `MoveMapping` back to `to_lowercase` (bites in the rename arm, because `Δρομος.md` carries an authored `ς`; it does not bite in the move arm, which keeps the stem).
9. `a_name_differing_only_by_final_sigma_is_refused_for_portability`: the collision gate's `index_entry_case_insensitive` (`S:19209-19226`) finds `κοσμος.md` for `κοσμοσ.md`, and a `create_exclusive("κοσμοσ.md")` beside an indexed `κοσμος.md` is refused as a name that would alias on a case-insensitive volume (APFS, by reading). Red: on `main` the gate sees no alias, and on NTFS (step 0's red team) the create succeeds and both files exist.

**Core, Quick Open and the filter.**

10. `quick_open_finds_a_final_sigma_name_whichever_sigma_was_typed` (`switcher.rs` tests, and once through `switcher_rank_top_indexed`): `κοσμος`, `ΚΟΣΜΟΣ` and `κοσμοσ` each find `ΚΟΣΜΟΣ.md` and `κοσμος.md`; `ΚΟΣΜΟΣ2` finds `κοσμος2.md`; `οδος` finds `ΟΔΟΣ.md`; the display-name spans cover the whole stem. Red: step 0 P1.
11. `the_sidebar_filter_finds_a_final_sigma_name_whichever_sigma_was_typed` (`session/tests/` beside the filter's facts): `κοσμοσ`, `ΚΟΣ` and `λογοσ` find their notes; the order is still effective name, then path. Red: step 0 P1.

**Core, snippets (`L` tests and `session/tests/links_embeds.rs`).**

12. `a_snippet_starts_and_ends_on_a_word_with_a_mark_where_cut`: step 0's `B.md` paragraph: the backlink's snippet starts "…" plus a whole word and ends a whole word plus "…", and holds the link. Red: "on zeta …" / "… la".
13. `an_autolink_snippet_is_trimmed_too`: the same paragraph around `<https://example.com/a>` and around `<a@b.co>`; the outgoing row's snippet is trimmed and marked. Red: today's text. Mutation: drop `<`/`>` from the candidate shapes (the row comes back untouched).
14. `a_snippet_at_the_start_or_end_of_a_note_has_no_mark_on_that_side`: a link in a note's first line, and one in its last; and the two mutation goldens' notes unchanged. A guard, green on `main`.
15. `a_snippet_never_splits_a_grapheme`: cuts that fall inside a combining sequence (`é` as `e` + U+0301), a ZWJ emoji family and a flag pair (`🇬🇷🇫🇷`); the result's edges are grapheme boundaries and no half flag remains. Red where today's cut lands inside one.
16. `an_unbroken_word_beside_a_link_keeps_the_link_whole`: 120 bytes of letters and digits with no word break, ending right before `[[Target]]` (a URL would not do: UAX #29 breaks it at `:`, `/`, `?` and `=`); the snippet keeps the whole link and starts with "…" at a grapheme boundary (the guard branch). Red: it starts mid-run with no mark.
17. `a_snippet_is_trimmed_on_every_read`: one row read through `backlinks`, `outgoing_links`, `note_link_panels` and `note_load_bundle` (the CLI's path), and a second, dangling row through `list_unresolved_links`, each with a cut paragraph: every read returns the trimmed text. Red: each returns today's text. Mutation: drop the call from any one read.
18. The ceiling fact (`links_embeds.rs:485-531`) keeps its ceiling (4096 bytes plus one "…") and gains a row whose link is longer than the ceiling and whose left edge is cut. A guard against stacked marks; red under the mutation that bounds after marking.
19. The existing `snippet_around_*` facts (`L:1077-1110`) are unchanged: the stored text is what it was.

**Windows twins** (Call 6), both against a real `VaultSession` through the FFI, no window, no census path, so no census trait (`scripts/check_windows_census_trait.py`):

20. `W/tests/SlateWindows.Tests/QuickSwitcherViewModelTests.cs`, `AFinalSigmaNameIsFoundWhicheverSigmaWasTyped`: the switcher's view model ranks `ΚΟΣΜΟΣ.md` for `κοσμος`. Red on `main`.
21. `W/tests/SlateWindows.Tests/RightPanePanelsTests.cs`, `ABacklinkThroughAFinalSigmaListsWithItsContextOnWholeWords`: `ΚΟΣΜΟΣ.md`'s Backlinks panel lists the note that links `[[κοσμος]]` among cut prose, and the row's automation name reads "Backlink from Linker.md, context: …" followed by a whole word. Red on `main`.

**The parity golden.** `parity_golden/links.json` is regenerated by the Windows harness (`dotnet run --project apps/slate-windows/tools/ParityHarness -- --fixtures crates/slate-core/tests/fixtures/markdown --canvas-fixtures crates/slate-core/tests/fixtures/canvas --graph-fixtures crates/slate-core/tests/fixtures/graph_vault --out crates/slate-core/tests/fixtures/parity_golden`), never by hand; the diff is only the 45 backlink snippets, each cut on at least one side today (the red team rebuilt them from the fixtures). `ParityHarnessCensus` passes on Windows; the Swift `ParityHarnessTests` compare the same file on the Mac lane.

## Files

- `crates/slate-core/src/name_fold.rs` (new; license header): `fold_case`, `fold_sigma`, their unit facts.
- `crates/slate-core/src/lib.rs`: the module line.
- `crates/slate-core/src/db.rs`: `tree_sort_key`, the docs, `MIGRATIONS`, facts 1 to 3b.
- `crates/slate-core/migrations/042_tree_key_folds_final_sigma.sql` (new; license header).
- `crates/slate-core/src/link_resolver.rs`: the fold at every site; the comment; fact 4.
- `crates/slate-core/src/link_rewrite.rs`: `MoveMapping`.
- `crates/slate-core/src/session.rs`: `stem_of` and the three fold docs.
- `crates/slate-core/src/graph.rs`: `ghost_key`'s doc only.
- `crates/slate-core/src/palette.rs`: `grapheme_key`.
- `crates/slate-core/src/sidebar_filter.rs`: its module doc only.
- `crates/slate-core/src/links_db.rs`: `present_snippet`, its callers, two SELECTs, the heal-key doc; facts 12 to 16.
- `crates/slate-core/src/switcher.rs` (tests only), `session/tests/save_link_lookup.rs`, `session/tests/links_embeds.rs`, the filter's and structural test files: facts.
- `crates/slate-core/tests/fixtures/parity_golden/links.json`: regenerated.
- `docs/plans/32_canonical_file_identity_contracts.md` and `docs/plans/34_canvas_contracts.md`: one appended note each (below).
- `W/tests/SlateWindows.Tests/QuickSwitcherViewModelTests.cs`, `RightPanePanelsTests.cs`: the twins.

Eleven non-test source files, two of them new and four touched for a doc or a module line. Not edited: `crates/slate-uniffi`, any Swift or C# product source, `a11y.rs`, the corpus, contract 38's ledger, the parity matrix, migrations 001 to 041, graph contract 35.

## Overlap

- **This wave.** CB-15c edits `diagram.rs`, `EditorInteractions.cs`, `Reading/` and `ShellFailureDetailCensus` (its spec, when the lead shares it, is checked for these two test files). The rebuild shows the user no surface (Design 2), so there is nothing to check against CB-15c's files beyond that. CB-17a edits `MainWindow*`, the focus manifest and its census; no overlap.
- **Next waves.** CB-33 edits hot statements with an equality on `files.path` in `L` and `S`; this chunk adds two span columns to `backlinks_for`'s and `unresolved_links`'s SELECTs, which CB-33 meets. CB-45b's reader connection (`D`) registers the same functions, so it inherits the fold. CB-34 (C12) edits `palette.rs`'s ranking and meets `grapheme_key`'s one-line change.

## NVDA spot (plan row), on `main` and on the head

A vault with `ΚΟΣΜΟΣ.md`, `Linker.md` (`Lead. [[κοσμος]] tail.`), `Long.md` (step 0's `B.md` linking `[[ΚΟΣΜΟΣ]]`), launched by the field-pass script with `-Root` (#1995), NVDA 2026.3.1, speech read from `nvda.log`.

1. Quick Open (Ctrl+P), type `κοσμος`. Base expected: `ΚΟΣΜΟΣ` is not among the rows (no match, or only other names). Head expected: the first row is spoken as "ΚΟΣΜΟΣ" with its folder.
2. Open `ΚΟΣΜΟΣ.md`, move to the Backlinks panel. Base expected: the empty state "No notes link here yet." (`W/src/SlateWindows/Panels/RightPanePanelsViewModel.cs:327-333`). Head expected: "Backlink from Linker.md, context: Lead. [[κοσμος]] tail." and "Backlink from Long.md, context: …<word> … <word>…" (the label carries the file name with its extension, `:1157`, `:1168`).
3. Record whether NVDA voices "…" at its default punctuation level and at "all" (recorded, not assumed).

## Docs and registers

- **#1954 (Mac work owed):** #1705's line becomes: links that used to stay unresolved now resolve; each vault's index is rebuilt once at its first open by this core, during which the Mac window does not respond (0.2 s at 10,000 notes, 2.2 s at 100,000 on the measured machine); folders named like `ΝΟΜΟΣ` and `ΝΟΜΟΣ2` swap in the Mac's tree. #1782's line: the Mac's Backlinks label (`M/BacklinksPanel.swift:91`) speaks the trimmed context, so `not delivered: VoiceOver pass`. Call 3d's line for the Move to sheet. And by reading: whether APFS treats `ς`/`σ` names as one, which the collision gate's portability rule now assumes.
- **#1955 (deferred finds):** Calls 3a, 3b, 3c and 3e, if taken as recommended.
- **Contracts.** No row covers #1705, #1782 or snippet trimming. Two records name the fold this chunk changes and get one appended note each, citing CD22 (the owner's recorded call, which AGENTS.md lets a frozen record take): contract 32's I5 (the collision gate's fold, "NFC + Unicode lowercase", `:221-228`, owner Call 1 at `:439-444`), and contract 34's 0b-14 and CD-22 (`:754-760`, `:14689-14702`), which record `to_lowercase` as the crate's one case rule and argue against a per-letter fold. Graph contract 35's 0b-3 (the ghost key) and 0b-6 (the name filter) are not changed (Calls 3b, 3c).
- **#1705 and #1782:** step 0's tables go in a comment on each before code. Both close on this PR (Call 7).
- **Found on the way, predicted:** found 5 (Call 3a to 3e), fixed here 0, deferred 4 (#1955), Mac ledger 1 (#1954), filed 0, if Call 3 goes as recommended.

## Calls for the lead

1. **Which fold.** (a) **Recommended:** `tree_sort_key` itself folds final sigma (Design 1), and migration 042 rebuilds all seven indexes over it: one fold for links, Quick Open, the filter, the collision gate and the tree. Costs: (i) core's sort order moves for names that meet at a `ς`/`σ` (`ΝΟΜΟΣ2` against `ΝΟΜΟΣ`); Windows shows no change, Mac folders swap (a #1954 line). (ii) In a vault that already holds a `ς`/`σ` pair, which NTFS allows (step 0's red team: both files coexist on this machine), the pair now behaves as case variants do: a link naming either resolves by the byte-smaller tiebreak, the gate refuses a new twin for portability (APFS would alias them, by reading), and a case-only rename onto one spelling can be refused when the other also exists (Design 1). (b) A second fold beside `tree_sort_key`, for links and the two filters only (new or replaced indexes for the five link and name indexes; 033's two and the collision gate keep today's fold). The sort order and the gate do not move, so (i) and (ii)'s gate effects go; the link tiebreak in (ii) stays, since links must fold the two together. Costs: two folds that differ by one letter, each future caller choosing between them; the gate stays as NTFS sees names and blind to APFS's alias; about the same rebuild (five of the seven indexes, 0.42 s less at 100,000 notes).
2. **The rebuild's surface.** (a) **Recommended:** no new surface or sentence. It runs at the first open inside the open's existing wait (Windows: the "Opening <root>…" status and progress bar, off the UI thread; nothing spoken), and costs 0.16 s at 10,000 notes and 2.2 s at 100,000 notes with a million links on this machine. The Mac's main-actor open is not changed here; the #1954 line names the pause. A second process waits on the writer lock and fails only past 5 s (about 230,000 notes). A downgrade refuses the vault with core's existing sentence, "Could not open vault: database error: database is at schema version 42, …", as every migration does (step 0 E2). (b) A spoken "Updating the vault index" while it runs: a new core event, an FFI addition, both hosts, a full NVDA pass and a VoiceOver ledger line, for a wait of a fraction of a second on a typical vault. Not recommended.
3. **Members of the class found on the way** (step 0's survey and the red team):
   - (a) **Batch moves preflight with plain `to_lowercase`** (`S:14179-14189`, `:15013-15036`, `:16067-16092`), no NFC and, after this chunk, a different fold from the single-item gate. On NTFS a batch move of a `ς`/`σ` twin passes preflight and lands, creating a pair the single-item gate would have refused; on APFS (by reading) it fails mid-batch at the no-replace rename. **Recommended: a #1955 line** naming the failing fact (a batch move onto a `ς`/`σ` or NFD twin is refused at preflight). Cost: the batch route stays inconsistent with the gate until then.
   - (b) **The graph's ghost key** (`graph.rs:36-50`) keeps `to_lowercase`, so `[[ΑΓΝΩΣΤΟΣ]]` and `[[αγνωστοσ]]` draw two ghosts. Frozen contract 0b-3 pins it; only the owner can amend it. **Recommended: a #1955 line for the owner.**
   - (c) **The graph's name filter** (`graph_queries.rs:270-285`, also groups at `graph_config.rs:665`) lowercases with context, so `ΚΟΣ` misses `κοσμος`: the same class as Quick Open. Frozen contract 0b-6 with divergences 0b-D3. **Recommended: a #1955 line for the owner.**
   - (d) **The Mac's Move to sheet filter** (`M/MoveToFolderSheet.swift:428-441`, `lowercased().contains`) misses a sigma spelling where the Windows twin (`OrdinalIgnoreCase`) hits. Mac host code, no Mac here. **Recommended: a #1954 line.**
   - (e) **The canvas filter** (`canvas/queries.rs:376-377`) lowercases with context, so `ΚΟΣ` misses `κοσμος`. **Recommended: a #1955 line.** Its fix is one call of `fold_case` in another module of the crate (medium by AGENTS.md); taking it here is the alternative, at the cost of one more file and a fact.
4. **The snippet's read-time limits** (Design 6): one whole word lost when a cut fell on a boundary; a note ending 60 to 63 bytes after a link reads as cut. **Recommended: accept, as CD22's "trim at read" implies, and say so in the PR.** The exact alternative is a trim at index time plus a forced re-index of every note (a second, much larger rebuild), which CD22 declined.
5. **The mark's shape.** **Recommended:** "…" joined to the word with no space on each cut side, `bound_snippet`'s shape. `unresolved_links` (no host caller) gets the same presentation and the ceiling, so all reads agree.
6. **Windows twins.** **Recommended:** the two facts above (Quick Open; a Backlinks row's resolution and context). Cost: a Windows build, the two classes, `dotnet format --include` on both files, the parity golden's regeneration, and the whole unit run under the desktop lock before the push (about 40 minutes).
7. **Closing.** **Recommended:** `Closes #1705` and `Closes #1782`. #1782 mentions raw Markdown in wide windows; CD22 put stripping it out as a feature, so the PR says so. Optionally file that feature for Milestone W (one issue); recommended only if the owner wants it tracked.
8. **Step 0's comments** on #1705 and #1782 go up now, before code, with no close word before any other number. Recommended.

### Lead rulings (2026-11-21)

1. (a): one fold. `tree_sort_key` folds `ς` to `σ` and 042 rebuilds all seven indexes. The PR body records both costs (the Mac's folder order, a #1954 line; a `ς`/`σ` pair behaves like case variants). **Added: fact 9b**, below, pins the pair.
2. (a): nothing new. The PR body records where the rebuild runs, what each host shows, the 5-second line and the downgrade. **Added: fact 2b**, below.
3. (a) the batch-move preflight and (e) the canvas filter are **fixed here, red first**, with the same fold (facts 22 and 23 below); (b) the ghost key and (c) the graph name filter get #1955 lines and go to the owner in the PR's "For the owner" section; (d) a #1954 line.
4. Accepted; both limits go in the PR body.
5. Accepted.
6. Accepted; the whole unit run under the desktop lock.
7. `Closes #1705`, `Closes #1782`; no feature issue for raw Markdown.
8. Step 0's comments go up now.
The appended CD22 notes on contracts 32 and 34 are accepted.

Added by the rulings:

- **2b.** `an_upgraded_index_resolves_a_final_sigma_link_without_a_rescan` (`session/tests/upgrade_fence.rs` or beside fact 2): a vault scanned under the old fold (fold registered over the session's connection is not reachable, so the fact builds the cache at version 41 with the old fold through `db` directly: `files` row `ΚΟΣΜΟΣ.md`, a source row with dangling `[[κοσμος]]`), then a session opens it (042 runs) and, with no scan, a save of a third note linking `[[κοσμος]]` resolves through the rebuilt `idx_files_name_fold`, and the create heal of a new `Κοσμος` sibling path reads the rebuilt 041 index. Red: with 042 left out, the indexed lookup misses (step 0 E1). The exact route is settled at delivery and named in the PR.
- **9b.** `a_final_sigma_pair_behaves_like_case_variants`: a vault holding `ΟΔΟΣ.md` and `Οδος.md` (both exist on NTFS and on a case-sensitive volume): `[[οδος]]` and `[[Οδος]]` resolve to the byte-smaller `ΟΔΟΣ.md`, as `[[note]]` beside `NOTE.md` and `Note.md` resolves today; `create_exclusive("οδοσ.md")` is refused by the gate. Red on the tiebreak arm (today `[[Οδος]]` finds `Οδος.md`) and the gate arm (today the create succeeds on NTFS).
- **22.** `a_batch_move_refuses_a_final_sigma_twin_at_preflight` (`session/tests/structural_batch.rs`): a batch move of `κοσμοσ.md` into a folder holding `κοσμος.md`, and of an NFD `é` name beside its NFC twin, is refused at preflight with the single-item gate's outcome. Red: NTFS lands both. Fix: the three preflight sites (`S:14179-14189`, `:15013-15036`, `:16067-16092`) fold with `tree_sort_key`.
- **23.** `the_canvas_filter_finds_a_final_sigma_name_whichever_sigma_was_typed` (`canvas/queries.rs` tests): `ΚΟΣ` and `κοσμοσ` find a card titled `κοσμος`. Red. Fix: `canvas/queries.rs:376-377` folds with `fold_case`.

The predicted count becomes found 5, fixed here 2 (3a, 3e), deferred 2 (#1955, for the owner: 3b, 3c), Mac ledger 1 (3d), filed 0.

## Verification

```powershell
$env:CARGO_TARGET_DIR = '<this worktree's own target directory>'
cargo test -j 4 -p slate-core --lib -- db:: name_fold:: link_resolver:: links_db:: palette:: switcher::
cargo test -j 4 -p slate-core --lib -- session::tests::save_link_lookup session::tests::links_embeds
cargo fmt --all -- --check
cargo clippy --all-targets --workspace -- -D warnings
cargo nextest run --workspace -j 4   # make test-fast's sweep
cargo bench --workspace --no-run
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh
python -I scripts/a11y_trigger_ledger.py --check
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet run --project apps/slate-windows/tools/ParityHarness -- <as above>
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~QuickSwitcherViewModelTests|FullyQualifiedName~RightPanePanelsTests|FullyQualifiedName~ParityHarnessCensus|FullyQualifiedName~MutationHarnessCensus"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <the two test files>
python3 scripts/check_windows_census_trait.py
```

Then the whole Windows unit run under the desktop lock, and the NVDA spot on `main` and the head.

## Acceptance

- [ ] Step 0's tables are on #1705 and #1782 and in the PR body.
- [ ] Facts 1, 2 (in its fold-without-042 state), 4 to 13, 15 to 17 and both twins were seen red for the stated reason; guards 3, 14 and 18 are named with their mutations; every listed mutation bites.
- [ ] Migration 042 rebuilds exactly the seven indexes; `integrity_check` is `ok` after it.
- [ ] The NVDA spot's phrases on `main` and the head are in the PR body.
- [ ] `Closes #1705`, `Closes #1782`; `gh pr view N --json closingIssuesReferences` shows only those; no close word before #1954, #1955, #1426, #1704 or #1917.
- [ ] Contracts 32 and 34 carry their appended CD22 notes.
- [ ] The body has the red team, the Codex rounds and verdict, "Found and fixed here", `not delivered: VoiceOver pass` and the Mac timing, and the count.

## Red team record

A fresh Opus agent at xhigh read the first draft against 47bcf77e, step 0's outputs and probes of its own: 1 blocker, 4 majors, 9 minors, 9 lows. It confirmed the core mechanism: seven indexes exactly (no trigger, view, generated column or Rust-built index uses the fold), no stored key or cursor goes stale (directory cursors carry a per-session nonce, the feed clears, no host or `graph_config` persists a fold key), DROP and CREATE are safe inside the IMMEDIATE transaction, the spans are byte offsets into the text `snippet_around` read, the candidate window is right, all 45 golden snippets are cut today, the regeneration command is right, the twins need no census trait. Every finding is taken:

- Blocker: **NTFS keeps `ς` and `σ` names apart** (it created both in one folder on C:; neither opens under the other's spelling; .NET `OrdinalIgnoreCase` calls them equal, so the Windows host disagrees with its own volume). The `fold_case` doc now rests on Final_Sigma alone and on APFS by reading; Call 1 is re-costed with the twin-vault effects (resolution tiebreak, the gate's portability refusal, the `LIMIT 1` case-only rename) as accepted costs in Design 1; the first draft's Call 3a (a sigma-only case rename refused on Windows) is dropped, since the destination does not exist on NTFS and the rename lands today; test 9 (was 10) is reworded as a portability refusal; Call 3a (was 3b) describes the batch route as NTFS runs it.
- Major 1: `ghost_key` is frozen contract 0b-3. It is no longer changed; its doc is corrected and Call 3b puts it to the owner. The first draft's ghost fact is dropped.
- Major 2: test 8's `stem_of` mutation could not bite. `D.md`, whose only link is `[[οδοσ]]`, now carries it; the `MoveMapping` mutation's reach is stated.
- Major 3: autolinks (`<https://…>`, `<a@b.co>`) have spans that start `<` and end `>`. The candidate shapes include them; fact 13 pins them.
- Major 4: contracts 32 (I5) and 34 (0b-14, CD-22) name the fold; each gets an appended note citing CD22. The canvas filter is Call 3e.
- Minors: `D:2026`'s version assertion is fact 3b and 042 copies 041's text byte for byte; Design 5's mechanism and the `ΝΟΜΟΣ2` flip; the trim's edges defined by word-like segments, with the flag case and the largest/smallest candidate rule; fact 16 uses an unbroken run, not a URL; the ceiling's mark and the edge-only segmentation; fact 5b for the embed render path; fact 3 matches any `slate_` function; fact 2's registration order; fact 17's two rows and `note_load_bundle`.
- Lows: the acceptance list separates red facts from guards and Goal's examples are ones that fail today; the verification commands use `--lib --`; the NVDA phrases use "Linker.md" and "No notes link here yet."; Call 1(b)'s saving is 0.42 s; E2 carries the "database error: " prefix; a second process during a rebuild past 5 s; the stale fold docs are listed; `fold_case` lives in a pure module; CB-34's overlap on `palette.rs`; the tally is recounted.

Not verified by anyone here: APFS's and HFS+'s sigma behaviour, the Mac's `localizedStandardCompare`, cold-cache timings, and whether every write path goes through `begin_fenced` (an older build writing old-fold keys into rebuilt indexes would make them silently wrong; the fence is what prevents it).

## Brief

Principal W, wave C9, 2026-11-21.

## What delivery changed (2026-11-21)

- **The snippet half was built by a worker from Design 6 and departed from it in three places,** each pinned by a fact and a mutation: a word-like segment that follows another word-like segment also starts a word (Chinese and Japanese rows kept no context under the spec's rule); the boundary after the first left segment, and before the last right one, counts only when that segment is white space (a cut inside a combining sequence left "…toile"); the guard's fallback uses a grapheme cursor that does not trust the cut edge.
- **The diff red team found two majors, fixed red first:** the guards used the inner candidate places, so a wikilink followed by `)`, `]` or `>` with no word in its left context could be trimmed into (the guards now take the outer places); and the upgrade fact used a name whose old and new keys agree, so it passed without migration 042 (it now uses names with an authored final sigma and adds the create heal). It also added two batch-preflight facts and corrected the pair fact and three docs.
- **Codex round 1:** migration 042 also sets every internal link whose target holds a sigma dangling, so the opening scan resolves it again with the new fold (an upgrade left old resolutions cached); the left cut test takes the smallest candidate place, so a false place never hides a cut. **Round 2:** a cluster that joins the link (a combining mark after its last bracket, a prepended mark before its first) is kept whole. **Round 3:** a rename among equal-key twins can leave an exact link unrewritten; put to the owner under the 2026-11-20 rule (see the PR).
- **Design 2's "seven indexes, and nothing else"** gained one statement: the re-resolution `UPDATE` above. Until the opening scan ends, a link whose target holds a sigma reads as unresolved.
- **Spec claims that were wrong:** test 9's premise that NTFS treats `ς`/`σ` names as one (the spec red team measured otherwise, folded in before code); the "MoveMapping to_lowercase" mutation as first written (it reverted the pushed values, which pass through the tree key again on the SQL path, so it could not bite; the entry-key mutation does, through a new fact for a note named with an authored final sigma).
- **Round 3, on the owner's call ("Fix it here"):** the rename planner offers mapped-back candidates in pre-move path order, so renaming one of two equal-key twins (case twins on a case-sensitive volume, and since the fold a sigma pair on NTFS) keeps the exact link on it; a pure fact for both twin kinds and a session fact whose case arm ran on a case-sensitive directory (`fsutil setCaseSensitiveInfo`). **Round 4:** the rename's candidate scan folds the whole name before stripping the extension, so `Note.MD` (pre-existing, no sigma needed) and `ΟΔΟΣ.MD` rewrite their links. **Round 5** and **round 6** approved.
- **The merge judge** (MERGE, 0 blockers, 0 majors): minor 1 fixed red first (a cut on an alphabetic combining mark, an Indic vowel sign, an Arabic haraka or a Hebrew point, no longer starts the row on the rest of its word); nit 3's stale docs fixed; nit 4 settled by a probe on SQLite 3.54.1 (a stale expression-index key makes UPDATE and DELETE succeed, lookups miss, `integrity_check` lists it), so the comments stand; nits 1, 2 and 5 went to #1955.
- **CI's one red:** contract 34's generated reconciliation tables had to be regenerated after its appended note (`scripts/canvas_reconciliation.py --write`).
- **The whole Windows run:** AGENTS.md's `dotnet test SlateWindows.slnx` also runs the shell gate, which competes with the unit project for the foreground; the unit project alone is the whole unit run (8,357 passed), and the gate runs on CI.
