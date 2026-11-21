# An invalid search says plain words, from core

Owner direction (2026-11-20, in chat): "Plain words. Please change it." It answers #2020's "For the owner" section, where CB-15b (merged as 15cba369, under CD9) made the Windows search panel speak core's detail as it is, so an invalid query now reads `Search error: FTS5 could not parse "AND": fts5: syntax error near "AND"`. The lead's example sentence, which the owner saw: "This search can't be read. Check its quotes and operators."

Issue: none of its own. The PR writes "Refs #2012" and "Refs #1527" and keeps every close, fix or resolve word away from those numbers and every other. Lanes: rust (`crates/`), windows (whole lane, since `crates/` changes), swift, and a11y-check (any Swift change runs it; it stays at 100). AT: an NVDA spot check on Windows, base and head; `not delivered: VoiceOver pass` with a #1954 line. Wave: C8, principal U. Review: this spec's red team (folded in below), then a red team on the diff, Codex rounds until it approves, and the lead's merge judge.

## Lead rulings (2026-11-20)

1. Call 1 accepted: "Check the quotes, symbols and operators in your search." The lead's sentence was an example; this one completes every lead and names symbols, which cause most refusals. The PR body's "For the owner" section gives the before and after for each surface; the lead tells the owner.
2. Call 2 accepted: "Search error:" stays.
3. Call 3 accepted: class C takes the sentence too (the owner takes the fuller fix). The #2012 comment names the item this takes; #2012 stays open (Refs only).
4. Call 4 accepted: Bases follows on both paths; the PR body records the "(a.md)" suffix.
5. Call 5 accepted: one Mac-only throwaway draft PR shows the Swift test red on main's Mac code, deleted after. If the push is refused, stop, tell the lead, and record the Mac red as by reading; no route around it.
6. Call 6 accepted: Find 2 is filed (bug, rust), naming the examples (#project, don't, e.g., email addresses).
7. Call 7 accepted for this PR: a second refused search in an open overlay is not announced, consistent with S7, pinned by a fact and listed under "For the owner" with the per-query sentence as the alternative.
8. Find 1 (the bare "fts5:" marker) is this change's own defect: fixed here, red first.

Written on the day of the owner's direction from the brief, #2012 and its CB-15b comment, #2020's body, CB-15b's spec (`specs/cb15b_canvas_graph_and_search_speak_cores_words.md`) and the code at `main` c4d1cd60, where step 0 ran. `S/` is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`. A statement that rests on reading alone says so. In this spec, **the sentence** means Call 1's recommended wording, `Check the quotes, symbols and operators in your search.`, and every phrase below uses it; a different ruling changes only the words.

## Goal

1. A search core's full-text index refuses as a query says one plain sentence that core owns, on both hosts: `Search error: Check the quotes, symbols and operators in your search.` (wording, Call 1; the lead, Call 2).
2. The sentence reaches every query FTS5 refuses as a query, by SQLite's own list, and only those: a damaged full-text index is a database error, never "check the quotes" (Find 1, fixed here).
3. The Mac's search says core's detail, so it adds no "Search query is invalid: " before the sentence.
4. Every Bases surface that carries the refusal says `file.matches: ` and the sentence, on both of its paths, with no "invalid search query:" between them.

## Current state on main (the drift check)

Every claim below was re-read at c4d1cd60; the red team re-read them too.

- **The source.** `crates/slate-core/src/search_db.rs:396-414`, `map_fts5_error`: a `rusqlite` error whose text contains `fts5:`, `syntax error`, `unterminated` or `unknown special query` becomes `VaultError::InvalidQuery { message: format!("FTS5 could not parse {query:?}: {message}") }`; anything else becomes `Db`. Four callers, all on the MATCH cursor (`:223`, `:229` for Vault and Folder scope; `:325`, `:332` for Tag scope). The outer statement is prepared before them (`:199`, `:315`, with `?`).
- **The detail.** `crates/slate-core/src/a11y.rs:3668-3705`, `vault_error_detail`, passes an `InvalidQuery`'s message through (`:3673`); the FFI export maps the variant across and calls it. The search's template is `SearchFailed { message } => "Search error: {message}"` (`a11y.rs:2155`), core's, shared by every search failure.
- **Windows search.** `S/Search/SearchOverlayViewModel.cs:875-888`, `PublishError`: `new A11yEvent.SearchFailed(FailureDetail.Of(failure))`, displayed through `A11yRender` and announced; no text of its own since CB-15b. Its comment (`:879-881`) says an `InvalidQuery`'s message "embeds a raw SQLite string". `PublishSummary` (`:895-905`) announces only when the rendered summary changes (contract S7), and a new search's Searching state does not clear the summary (`:741-742`).
- **The Mac's search** (by reading). `apps/slate-mac/Sources/SlateMac/AppState.swift:9900`: `let message = self.humanReadable(error)`, then `A11yEvent.searchFailed(message:)`, rendered through `a11yRender` for the summary. The instance `humanReadable` (`:26004-26055`) returns `"Search query is invalid: \(message)"` for `InvalidQuery` (`:26018-26019`). So the Mac says `Search error: Search query is invalid: FTS5 could not parse …`; its error view shows its own "Search error" heading over the message (`SearchOverlay.swift:438-446`), and it announces from `.onChange(of: appState.searchSummary)` (`SearchOverlay.swift:154-157`). The same `humanReadable` arm serves the Mac's other `InvalidQuery` callers (the sidebar filter, the tag tree), which this change does not touch. No Mac code calls `vaultErrorDetail` today; the static `humanReadableVaultError` (`:14019-14023`) passes the message bare. No Swift test pins a search failure's text.
- **The graph.** CB-15b's step 0 showed no input reaches the graph's invalid-query path: core's graph build fails only with `Db`. Its hosted fact (`T/GraphSearchFailureDetailTests.cs:207`, `:221`) throws a constructed `VaultException.InvalidQuery("a query core refused")` and expects that text. This change leaves both alone.
- **Bases, two paths.** A top-level `file.matches(…)` conjunct goes through `engine.rs:1045-1048`, which makes the view error `file.matches: invalid search query: {message}` (`load_file_candidates` `:814-831`, `load_task_candidates` `:893-912`). Every other `file.matches` (under `or:` or `not:`, the query builder's OR group, `BaseQueryBuilderViewModel.cs:29`, or with pushdown off) goes through the interpreter's `SqlVaultLookup::file_matches` (`engine.rs:3937-3945`), which wraps any error as `EvalError::InvalidArgument { function: "file.matches", message: error.to_string() }`, so `VaultError`'s `Display` (`invalid search query: {message}`, `lib.rs:240`) lands inside `{function}: {message}` (`eval.rs:495-496`), and `engine.rs:677-683` adds the evaluated row's path. Session composes `view_error` (`session.rs:21740-21746`, ` (path)` appended).
- **Where a Bases view error reaches a reader.** Spoken: the query builder's preview announces `A11yEvent.BaseQueryPreviewFailed`, "Preview failed: {detail}" (`a11y.rs:2566`), on Windows (`BaseQueryBuilderViewModel.cs:654-683`) and the Mac (`AppState+Bases.swift:2314-2321`, `BaseQueryBuilderModel.swift:1324-1325`); a raw expression row accepts `file.matches("AND")` (validation only parses, `slate-uniffi/src/lib.rs:13135-13159`). Shown, not announced: the base's banner (`BaseDocumentViewModel.cs:1462-1464`, `BaseSurfaceView.cs:786-790`, its accessible name), the dashboard (`DashboardViewModel.cs:215-218`), base embeds (`ReadingDocumentBuilder.cs:1419-1421`); on the Mac `BaseDocument.swift:560-563`, `DashboardDocument.swift:412`, `:479`, `BaseEmbedView.swift:224`, `BaseEmbedDocument.swift:1011`, `:1214`, `BaseContainerView.swift:179`; and `slate query`.
- **The CLI.** `slate search` prints `VaultError`'s `Display`, `invalid search query: {message}`, after `slate: `.
- **What pins the old text.** `T/GraphSearchFailureDetailTests.cs:65` (`StartsWith("Search error: FTS5 could not parse ")`), `crates/slate-core/src/session/tests/search.rs:14-37` (the message contains the query), `crates/slate-core/tests/bases_engine.rs:2571-2597` (the view error contains "unbalanced"), contract 29's SD-6 (`docs/plans/29_search_overlay_contracts.md:558`, quoting "Search error: FTS5 could not parse …") and S6 (`:150-154`: the message "embeds a raw SQLite string", the host "never shows it verbatim as guidance", the classifier at `search_db.rs:396-414`). The `vault_error_detail` witness `"fts5: syntax error near \"AND\""` (`a11y.rs:6813-6818`) only proves the passthrough. `crates/slate-cli/tests/cli.rs:588-599` checks "invalid search query", which still holds. No census, golden or corpus entry pins the FTS5 text (`corpus.json` uses "the index is unavailable"); `ShellFailureDetailCensus` reads exception reads, not words.

## Step 0, as run (main c4d1cd60)

Two uncommitted probes, deleted before the first commit; copies stay local, in the principal's scratch folder, and are not in the repository (`c8u_step0_probe.rs`, `C8uStep0SearchProbe.cs`; outputs `step0-core.log`, `step0-windows.txt`).

- **Core** (`cargo test --release -p slate-core --test c8u_step0_probe`, 3 facts, passed, run three times with additions): a scanned vault; 77 typed queries through `VaultSession::full_text_search` at Vault scope, each printed with its variant, `vault_error_detail` and `Display`; four again at Folder and Tag scope; nesting and NOT chains; six through a Bases view's top-level `file.matches`; and a damaged index.
- **Windows** (`dotnet test … --filter C8uStep0SearchProbe`, Release, 1 fact, passed, no window): 39 of the queries typed into a real `SearchOverlayViewModel` over `VaultSearchSource` and a scanned `FixtureVault`, each printed with the variant a direct call raises, the panel state, the summary and the spoken line.
- **The red team's additions** (a temporary probe change it reverted, file hash checked): an `or:` filter base and NOT chains.

**What each query reaches.** 77 queries: 49 `InvalidQuery`, 11 `Db`, 17 succeed.

| Class | Queries | Arm | What Windows says today (the Mac, by reading) |
|---|---|---|---|
| A. Operators and quotes (21) | `AND`, `OR`, `NOT`, `foo AND`, `AND foo`, `foo OR`, `foo NOT`, `NOT foo`, `foo AND OR bar`, `NEAR(`, `NEAR(alpha beta`, `NEAR(alpha beta, -1)`, `unbalanced"`, `"unbalanced`, `*`, `*foo`, `^`, `+`, `(foo`, `foo)`, `()` | `InvalidQuery` | `Search error: FTS5 could not parse "AND": fts5: syntax error near "AND"`; `…"foo AND": fts5: syntax error near ""`; `…"unbalanced\"": unterminated string`; `…"*": unknown special query: ` (the Mac adds "Search query is invalid: " after "Search error: ") |
| B. Everyday text with a symbol (28) | `C++`, `#project`, `#project/sub`, `e.g.`, `node.js`, `1.5`, `don't`, `it's`, `'single'`, `[[alpha]]`, `- [ ] task`, `> quote`, `` `code` ``, `**bold**`, `a/b`, `~/notes`, `@name`, `email@example.com`, `$100`, `50%`, `foo, bar`, `what?`, `wow!`, `a&b`, `x=y`, `foo\|bar`, `a;b`, `<tag>` | `InvalidQuery` | `Search error: FTS5 could not parse "#project": fts5: syntax error near "#"`; `…"don't": fts5: syntax error near "'"`; `…"**bold**": unknown special query: *bold**` |
| C. A colon, a hyphen or a NEAR distance (11) | `foo:bar`, `title:foo`, `{foo}:bar`, `-x`, `TODO:`, `https://example.com`, `well-known`, `self-care`, `2024-01-01`, `NEAR(alpha, beta)`, `NEAR(alpha beta, x)` | `Db` | `Search error: sqlite error: no such column: known`; `…no such column: 01`; `…no such column: https`; `Search error: sqlite error: expected integer, got "beta"` (the Mac the same) |
| D. Succeed (17) | `NEAR`, `NEAR(alpha beta, 99999999999)`, `foo*`, `^foo`, `foo + bar`, `foo_bar`, `café`, `日本語`, `“curly”`, `alpha`, `"alpha beta"`, `alpha AND beta`, `alpha OR beta`, `alpha NOT beta`, `and`, `near`, `or alpha` | results | `Search returned 2 results.` and the like |
| E. A damaged full-text index (constructed) | every query, `alpha` included | `InvalidQuery` | `Search error: FTS5 could not parse "alpha": fts5: corruption found reading blob 274877906945 from table "files_fts"` |

- Folder and Tag scope reach the same arms (`AND`, `unbalanced"`, `#project`: `InvalidQuery`; `foo:bar`: `Db`).
- Depth: `alpha` inside 50 pairs of parentheses runs; inside 100, 200 or 1,000 it is `InvalidQuery`, `fts5: parser stack overflow`. `alpha` followed by 200 ` NOT beta` runs; by 300 it is `Db`, `sqlite error: fts5 expression tree is too large (maximum depth 256)`.
- Bases, top-level conjunct: `AND`, `unbalanced"`, `C++`, `#project` give the view error `file.matches: invalid search query: FTS5 could not parse "AND": fts5: syntax error near "AND"` (no rows); `foo:bar` fails the whole execute with `Db`, `sqlite error: no such column: foo`. Bases, nested (the red team's `or: ['file.matches("AND")', 'file.hasTag("nope")']`): `file.matches: invalid search query: FTS5 could not parse "AND": fts5: syntax error near "AND" (a.md)`.
- The Windows overlay's summary and spoken line are equal for all 39, and match core's detail with "Search error: " before it.
- Class E's construction: a scanned vault, the session dropped, then a second connection zeroes the index's leaf blocks (`UPDATE files_fts_data SET block = X'0000000000000000' WHERE id > 10`, two rows), and a fresh session searches. An earlier run with a stray index row alone (an `INSERT INTO files_fts` whose content row does not exist) returned 0 rows for its term: the search's join drops that row before `snippet()` reads content.

**SQLite's own list.** The bundled SQLite (libsqlite3-sys 0.39.1, `sqlite3/sqlite3.c`) raises FTS5's query-parse failures through `sqlite3Fts5ParseError` with these texts: `fts5: syntax error near "…"` (`:245736`, `:248627`, `:250381`), `unterminated string` (`:248616`), `expected integer, got "…"` (`:250399`, a NEAR distance), `no such column: %s` (`:250503`, a column filter), `fts5: parser stack overflow` (`:245365`), `fts5 expression tree is too large (maximum depth %d)` (`:250815-250818`, depth 256 at `:248397`), and two `detail=` refusals this index's default `detail=full` cannot reach (`:250615`, `:250800`); `unknown special query: …` comes from the cursor's filter (`:263023`). Its corruption texts also begin `fts5:`: `fts5: corruption found reading blob …` (`:252819`), `fts5: corruption on page …`, `fts5: corruption in table …`, `fts5: corrupt structure record …`, `fts5: checksum mismatch …`, `fts5: missing row … from content table …` (`:263583`). FTS5 also prepares its own content-table statements during the step and passes `sqlite3_errmsg` out (`:265949-265957`), so a schema mismatch in `files` could arrive as `no such column: body_text`; the migrations rule that out.

**Step 0 settles:**

1. **Not all of them reach the same arm.** Classes A and B reach `InvalidQuery`. Class C and the expression-tree limit, every one an FTS5 parse failure by SQLite's own source, reach `Db`, because the markers miss "no such column", "expected integer" and "expression tree is too large". Class E reaches `InvalidQuery` falsely, through the bare `fts5:` marker.
2. **"Check its quotes and operators" fits class A only.** Class B, 28 of the 49 `InvalidQuery` queries, is ordinary text with one symbol in it: a tag (`#project`), a contraction (`don't`), an abbreviation (`e.g.`), a question (`what?`), a list (`foo, bar`), a link (`[[alpha]]`), an address. A reader who typed `#project` has no quote and no operator to check (Call 1).
3. **A plain sentence on class E would mislead.** With the bare `fts5:` marker, a damaged index would tell the reader to check a search that is fine, every time they search. The marker set narrows to SQLite's parse failures before the sentence lands (Find 1).
4. **One fixed sentence makes a second unreadable search silent** on both hosts: the summary does not change, so neither host announces it (Call 7).
5. **The graph needs nothing.** No input reaches its invalid-query path, and its fact constructs its own message.

## Design

### 1. Core's sentence

`crates/slate-core/src/a11y.rs`, beside `VAULT_ROOT_IDENTITY_UNAVAILABLE` (`:3603`), the precedent for core copy that crosses in a `VaultError`'s message:

```rust
/// What a reader hears when core's full-text index refuses a search as a
/// query (the owner, 2026-11-20: "Plain words."): an operator with nothing
/// to join, a quote left open, or a symbol the query syntax refuses. It
/// completes a lead ("Search error: ", "file.matches: ") and never restates
/// the failure. Core's copy, carried in a `VaultError::InvalidQuery` whose
/// message crosses the FFI verbatim, so a host speaks it through
/// `vault_error_detail` with no corpus or event change, as
/// [`VAULT_ROOT_IDENTITY_UNAVAILABLE`]'s is.
pub const SEARCH_QUERY_UNREADABLE: &str =
    "Check the quotes, symbols and operators in your search.";
```

### 2. `map_fts5_error` says it, for parse failures only

```rust
/// SQLite's own texts for a query FTS5 refuses (sqlite3Fts5ParseError and
/// the cursor's special-query check, bundled sqlite3.c). FTS5's corruption
/// texts also begin "fts5:", so the bare prefix is not one of them.
const FTS5_QUERY_REFUSALS: &[&str] = &[
    "syntax error",
    "unterminated string",
    "unknown special query",
    "parser stack overflow",
    "expression tree is too large",
    "no such column",   // a column filter, a colon or a hyphen (Call 3)
    "expected integer", // a NEAR distance that is not a number (Call 3)
];

fn map_fts5_error(err: rusqlite::Error) -> VaultError {
    let message = err.to_string();
    if FTS5_QUERY_REFUSALS.iter().any(|marker| message.contains(marker)) {
        VaultError::InvalidQuery {
            message: crate::a11y::SEARCH_QUERY_UNREADABLE.to_owned(),
        }
    } else {
        VaultError::from(err)
    }
}
```

- The `query` parameter goes (four call sites): the sentence does not echo the query, which the reader has in the field. SQLite's text only classifies; nothing keeps it (core has no logging on this path), so the CLI and Bases lose SQLite's reason too.
- The doc comment above it is rewritten: the markers are SQLite's refusal texts with their sources; the outer statement fails at prepare and never arrives here; FTS5's own content statement could, only with a schema the migrations rule out (accepted risk, recorded); corruption is `Db`. The comment at `:202-207` says the error carries core's sentence. `VaultError::InvalidQuery`'s doc (`lib.rs:236-239`) says a search's message is `a11y::SEARCH_QUERY_UNREADABLE`, and other producers (the sidebar filter, the tag tree) carry their own. The FFI crate's doc for the variant (`slate-uniffi/src/lib.rs:76-79`, "without conflating it with a corrupt cache (which arrives as `Db`)") becomes true and is left alone, so the uniffi crate is not touched.
- Gating on SQLite's error code as well (parse failures are `SQLITE_ERROR`, corruption `SQLITE_CORRUPT_VTAB`) was weighed and left out: no corruption text in SQLite's source holds a marker, so no fact could show the gate bites.
- Without Call 3, its two markers are left out and class C keeps the `Db` arm. "expression tree is too large" is taken either way: it is FTS5's refusal of the query's size, not #2012's column-filter item.

### 3. Bases says `file.matches: ` and the sentence on both paths

- `engine.rs:1047`: `"file.matches: invalid search query: {message}"` becomes `"file.matches: {message}"`.
- `engine.rs:3937-3945`, `file_matches`: an `Err(VaultError::InvalidQuery { message })` becomes `EvalError::InvalidArgument { function: "file.matches", message }`, the message itself, not `VaultError`'s `Display`; other errors keep `to_string()`. The two paths then agree, as `bases_engine.rs:256` and its peers hold pushdown equal to the interpreter.
- The nested path's view error keeps the evaluated row's path suffix (`engine.rs:677-683`), which names a note unrelated to a query refusal: `file.matches: Check the quotes, symbols and operators in your search. (a.md)`. It is every eval error's existing shape; "For the owner" says so, and it is not changed here.

### 4. What each surface says on head

| Surface | Main | Head |
|---|---|---|
| Windows search, classes A and B (spoken and shown) | `Search error: FTS5 could not parse "AND": fts5: syntax error near "AND"` | `Search error: Check the quotes, symbols and operators in your search.` |
| Windows search, class C (Call 3) | `Search error: sqlite error: no such column: known` | the same as A and B |
| Windows search, a second unreadable search in the same panel | announced (its text differed) | not announced (Call 7); the panel still shows the line |
| Windows search, class E | `Search error: FTS5 could not parse "alpha": fts5: corruption found reading blob … from table "files_fts"` | `Search error: sqlite error: fts5: corruption found reading blob … from table "files_fts"` (core's `Db` text, by reading; measured at delivery) |
| The Mac's search (by reading) | `Search error: Search query is invalid: FTS5 could not parse …`, under a "Search error" heading | the same lines as Windows, under the same heading (Design 5) |
| Bases preview, both hosts (spoken) | `Preview failed: file.matches: invalid search query: FTS5 could not parse "AND": …` | `Preview failed: file.matches: Check the quotes, symbols and operators in your search.` |
| Bases banner, dashboard, embeds, both hosts (shown) | `file.matches: invalid search query: FTS5 could not parse "AND": …` (nested: the same with ` (a.md)`) | `file.matches: Check the quotes, symbols and operators in your search.` (nested: with ` (a.md)`); with Call 3, `file.matches("foo:bar")` gives this where it failed the whole view with `Db` |
| `slate search`, `slate query` (stderr and output; read by screen readers too) | `slate: invalid search query: FTS5 could not parse "AND": …` | `slate: invalid search query: Check the quotes, symbols and operators in your search.` (`Display` unchanged; SQLite's reason is gone) |
| The graph | unreachable by input | unchanged |

### 5. The Mac's search speaks core's detail

`AppState.swift:9900` becomes `let message = vaultErrorDetail(error: error)` (core's renderer, the FFI function behind Windows' `FailureDetail.Of`; the Mac's first call of it, a slice of #1527), with a comment: the search says core's detail, so its sentence has no lead of the Mac's own (the owner, 2026-11-20). For the variants a search raises (`InvalidQuery`, `Db`, `Io` from the detached task's fallback, `Unsupported` for File scope, which the Mac never sets; `Cancelled` returns first), `vaultErrorDetail` equals the instance `humanReadable` except for `InvalidQuery`'s lead (by reading both). The red team checked by reading that the call compiles against the generated API (`error` is a `VaultError`, `AppState.swift:9848-9861`; nothing shadows the name). The instance `humanReadable`'s `InvalidQuery` arm stays for the sidebar filter and the other callers. The static `humanReadableVaultError`, which also passes the message bare, would do the same with host-owned text; core's renderer is the direction #1527 and #1954 record. By reading only; the Swift lane proves it (Tests 7, Call 5).

### 6. Windows

No product change beyond one comment: `FailureDetail.Of` already speaks core's detail. `SearchOverlayViewModel.cs:879-881` says an `InvalidQuery`'s detail is core's sentence for a search its index refuses, passed through and never parsed or re-classified host-side (contract S6). What a reader hears does change: the sentence, and the silence of a repeated one (Call 7).

### 7. What this change does not touch

The `SearchFailed` and `BaseQueryPreviewFailed` templates and their leads (Call 2), the corpus and the golden, the trigger ledgers (no `A11yEvent` construction is added, removed or moved; keys are `function@ordinal`, contract 38; `--check` confirms), the parity matrix, the FFI surface (no `crates/slate-uniffi` change), the graph, the sidebar filter's and the tag tree's `InvalidQuery` messages, the Mac's `humanReadable` arms, the CLI's `Display`, the eval error's row path suffix, and #2012's other items.

## Tests (red on main first, then green)

The red run is the tests-only commit on main's code.

1. **Core's sentence** (`a11y.rs` tests, `the_unreadable_search_is_cores_sentence`): the literal; `vault_error_detail` passes it through; `SearchFailed`'s render is `Search error: Check the quotes, symbols and operators in your search.` and `BaseQueryPreviewFailed { detail: "file.matches: " + it }`'s is `Preview failed: file.matches: Check …`. On main it fails to compile (no constant), so its operand is shown by the session facts. The passthrough witness at `:6813-6818` takes the constant as its message.
2. **Every query FTS5 refuses says it** (`session/tests/search.rs`, `every_query_fts5_refuses_says_cores_sentence`): one query of each kind step 0 found (a bare operator, a dangling operator, an open NEAR, a NEAR distance below zero, each quote side, `*`, `^`, `+`, each parenthesis, 100 nested parentheses, 300 ` NOT beta`; from class B `#project`, `don't`, `e.g.`, `what?`, `foo, bar`, `[[alpha]]`, `C++`, `**bold**`), at Vault, Folder and Tag scope, each `InvalidQuery` whose message is `SEARCH_QUERY_UNREADABLE`. Red on main (the developer message; `Db` for the NOT chain). `full_text_search_maps_fts5_syntax_errors_to_invalid_query` asserts the constant where it asserted the query.
3. **Class C** (Call 3; same file, `a_column_filter_hyphen_or_near_distance_is_refused_as_a_query`): `foo:bar`, `-x`, `TODO:`, `well-known`, `2024-01-01`, `https://example.com`, `NEAR(alpha, beta)` give `InvalidQuery` with the constant. Red on main (`Db`).
4. **A damaged index is a database error** (same file, `a_damaged_full_text_index_is_a_database_error_not_an_unreadable_search`): step 0's construction, then `alpha` gives `Db` whose detail contains "corruption". Red on main (`InvalidQuery`). Beside it, a regression fact: every class D query still runs (green on main).
5. **Bases, both paths** (`crates/slate-core/tests/bases_engine.rs`): `file_matches_invalid_query_fails_view_with_search_error`'s construct equals `file.matches: Check the quotes, symbols and operators in your search.`; a new fact runs the same refusal under an `or:` filter and with pushdown off and expects `file.matches: ` and the sentence with the row's path, with no "invalid search query". Red on main. With Call 3, `file.matches("foo:bar")` gives that view error, not a failed execute (red on main: `Db`).
6. **The CLI** (`crates/slate-cli/tests/cli.rs`, `search_fts_syntax_error_exits_one_with_message`): stderr also contains the sentence. Red on main.
7. **The Mac** (`apps/slate-mac/Tests/SlateMacTests/AppStateTests.swift`, `testAnInvalidQuerySaysCoresDetailWithNoLeadOfItsOwn`, the Windows twin's name): a vault, `AND` through `searchQuery` and `bumpSearchQuery`, `awaitSearch`; the oracle is core's detail of the same refusal made directly on `currentSession` (`vaultErrorDetail(error:)`), so the fact is red on main's Mac code whatever core says: `searchState == .error(detail)`, `searchSummary == "Search error: " + detail`, no "Search query is invalid", and `detail` equals the literal sentence. Written by reading; red shown by Call 5.
8. **Windows** (`T/GraphSearchFailureDetailTests.cs`): in `AnInvalidQuerySaysCoresDetailWithNoLeadOfItsOwn` the `StartsWith("Search error: FTS5 could not parse ")` pin becomes `Assert.Equal("Search error: Check the quotes, symbols and operators in your search.", overlay.Summary)`, and the theory's data grows to one query per class (`AND`, `unbalanced"`, `NEAR(alpha beta`, `*`, `#project`, `don't`; with Call 3 `foo:bar`, `well-known`, `NEAR(alpha, beta)`). A new fact, `ASecondUnreadableSearchIsNotAnnouncedAgain` (Call 7's ruling decides its assertion): `AND`, then `#project` in the same open overlay, announces once on head; red on main (twice). Red on main for the first. `EveryOtherVariantSaysCoresDetail` and the graph's facts do not change.

**Mutations** (each turns the named facts red; the PR lists each with its result):

- the message back to `format!("FTS5 could not parse {query:?}: {message}")`: Tests 2, 3, 5, 6, 8;
- each marker removed in turn: `syntax error` (Test 2's operator and symbol cases), `unterminated string` (the quote cases), `unknown special query` (`*`, `**bold**`), `parser stack overflow` (the nesting case), `expression tree is too large` (the NOT chain), `no such column` and `expected integer` (Test 3);
- the bare `fts5:` marker restored: Test 4;
- each Bases path's old wording restored: Test 5's top-level fact, then its nested fact;
- the Mac's search site back on `humanReadable`: Test 7 (Call 5's probe).

## The NVDA spot check

Under the desktop lock, NVDA with debug logging and its punctuation level recorded, base (main) and head Release builds through `scripts/windows/launch-for-field-pass.ps1 -Root`, over a vault with a note `alpha.md` and a base `matches.base` whose filter is `file.matches("AND")`. The routes are by reading and are confirmed before the pass. Each search step starts from a closed overlay (Esc publishes an empty summary, `SearchOverlayViewModel.cs:361-364`), so each is announced.

1. **Search, class A.** Ctrl+Shift+F, paste `AND` (pasting avoids searching "A" and "AN" first). Base: `Search error: FTS5 could not parse "AND": fts5: syntax error near "AND"`. Head: `Search error: Check the quotes, symbols and operators in your search.`
2. **The repeat** (Call 7). Without closing, select all and paste `#project`. Base: `Search error: FTS5 could not parse "#project": fts5: syntax error near "#"`. Head: nothing new; reading the panel gives the head line of step 1.
3. **Search, class B.** Esc, Ctrl+Shift+F, paste `#project`. Base as step 2. Head: the head line of step 1.
4. **Search, class C** (Call 3). Esc, Ctrl+Shift+F, paste `well-known`. Base: `Search error: sqlite error: no such column: known`. Head: the head line of step 1.
5. **The Bases preview.** Open the query builder on `matches.base`, add a raw expression row `file.matches("AND")`, and let the preview run. Base: `Preview failed: file.matches: invalid search query: FTS5 could not parse "AND": fts5: syntax error near "AND"`. Head: `Preview failed: file.matches: Check the quotes, symbols and operators in your search.`
6. **The Bases banner.** Open `matches.base`; read the banner by object navigation (a text element, not announced). Base: `file.matches: invalid search query: FTS5 could not parse "AND": fts5: syntax error near "AND"`. Head: `file.matches: Check the quotes, symbols and operators in your search.`

The PR records each phrase as heard, base and head; a phrase not reached says how far the pass got.

## Files

Product: `crates/slate-core/src/a11y.rs` (the constant), `crates/slate-core/src/search_db.rs` (the markers, the message, the comments), `crates/slate-core/src/lib.rs` (one doc comment), `crates/slate-core/src/bases/engine.rs` (two sites), `apps/slate-mac/Sources/SlateMac/AppState.swift` (one line and its comment), `S/Search/SearchOverlayViewModel.cs` (a comment). Tests: `crates/slate-core/src/a11y.rs` (its tests module), `crates/slate-core/src/session/tests/search.rs`, `crates/slate-core/tests/bases_engine.rs`, `crates/slate-cli/tests/cli.rs`, `T/GraphSearchFailureDetailTests.cs`, `apps/slate-mac/Tests/SlateMacTests/AppStateTests.swift`. Docs: `docs/plans/29_search_overlay_contracts.md`, one amendment line under SD-6. No new file; no generated binding changes.

## Overlap

- **CB-29 (principal T, same wave)** edits `diagram.rs`, `SvgPreviewGuard.cs`, `EditorInteractions.cs`, `HostLog.cs` and `ReadingEmbedArtifact.cs`. No file is shared. Both change `crates/`, so whichever merges second merges `main` and reruns its lanes.
- **CB-15c (C9)** routes the last census rows; it does not touch search or Bases.
- **#2012's fix**, whenever it lands, owns the other developer-text details; this change takes the search's (the #2012 comment says which).

## Docs and registers

- Contract 29: one amendment line under SD-6, the owner's recorded call; no existing text changes: "Amended (owner, 2026-11-20, "Plain words. Please change it."): a search core's index refuses as a query carries core's sentence, `a11y::SEARCH_QUERY_UNREADABLE`, as its `InvalidQuery` message, and the Mac's search says core's detail too, so both hosts say "Search error: Check the quotes, symbols and operators in your search." S6 no longer holds for a search on three points: the message embeds no SQLite text, the host shows it verbatim as guidance, and the classifier moved within `search_db.rs` to SQLite's own refusal texts; a damaged index is `Db`. A second unreadable search in an open overlay is not announced again, as S7 has it for equal summaries. Facts: `GraphSearchFailureDetailTests`, the core session facts, the Mac's `AppStateTests`."
- No contract 35 change (the graph's text does not change). No plan or contract document quotes "invalid search query" (`grep -rl` over `docs/plans` at c4d1cd60 finds none).
- #2012: one comment saying which items this change takes: the search's FTS5 details (`FTS5 could not parse …`, the backslash quoting, `unknown special query: ` with nothing after it), and, if Call 3 is ruled yes, the column filter classified as a database error, widened by step 0 to hyphenated words, dates, links and `TODO:`. Not taken: the canvas handle and node texts, the locked-index text, the directory-page restart and the other details it lists. New data for it: a damaged full-text index says `Search error: sqlite error: fts5: corruption found reading blob …` on head, developer text on the `Db` arm.
- #1527: one comment: the Mac's search is the first Mac surface that says core's `vaultErrorDetail`.

## Ledgers

- #1954 (Mac work owed): one line. The Mac's search says core's detail since this PR (by reading; the Swift lane proves it), so CB-15b's #1954 line keeps only the graph's `humanReadable` arms. A VoiceOver pass is owed for the Mac's new search line, the silence of a repeated one, the Bases preview's line and the Bases views' text.
- #1955 (deferred finds): none.

## Corrections and the feature's own changes (not finds)

- **"Check its quotes and operators" does not fit most refused searches** (step 0, settles 2): Call 1.
- **Class C and the expression-tree limit reach a different arm** (settles 1): Call 3 and Design 2.
- **The nested Bases path wraps `VaultError`'s `Display`**, so changing the message alone leaves "invalid search query:" there: Design 3 (the red team's blocker).
- **A fixed sentence silences a repeated failure**: Call 7.
- **The Windows comment and contract 29's S6 and SD-6 describe the old message**: Design 6 and the amendment line.

## Finds

1. **A damaged full-text index makes every search say it cannot be parsed** (step 0, class E). The bare `fts5:` marker classifies FTS5's corruption texts as a query problem, so today a reader hears `Search error: FTS5 could not parse "alpha": fts5: corruption found reading blob …` for a search that is fine; with the plain sentence it would tell them to check their quotes. It is in the function this change rewrites and it would make the new sentence false, so it is the PR's own feature: fixed here (Design 2, Test 4). A "Found and fixed here" line.
2. **Ordinary text with a symbol is refused as a search** (step 0, classes B and C): a tag (`#project`), a contraction, an abbreviation, a question mark, a comma, a wikilink, an email address, a hyphenated word, a date, a link. Both hosts pass the typed text to FTS5's query syntax raw (`VaultSearchSource.cs:76`; `AppState.swift:9851`, by reading). The fix (quote the barewords that hold a symbol, or retry the query as a phrase, while keeping operators) changes what a search matches on both hosts and needs a design and an owner call: High, filed (Call 6). Its failing test: search `#project` over a note tagged `#project` and expect one result.
3. **A damaged index's `Db` text is developer text** (`sqlite error: fts5: corruption found reading blob …`): #2012's class; a line in the #2012 comment.

## Verification

Each cargo run uses a target directory of its own beside the clone (`CARGO_TARGET_DIR` set to `target-invalid-search-plain-words`). `make` is not on this machine's path, so `make ci`'s parts run one by one.

```
cargo fmt --all --check                               # the Makefile's fmt-check
cargo clippy --all-targets --workspace -- -D warnings  # clippy
cargo test --workspace                                 # test (or make test-fast's nextest sweep)
cargo bench --workspace --no-run                       # bench-check
./scripts/check-license-headers.sh                     # check-license-headers
cargo test -p slate-core --lib -- session::tests::search a11y::tests::the_unreadable_search
cargo test -p slate-core --test bases_engine -- file_matches
cargo test -p slate-cli --test cli -- search_fts_syntax_error
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --filter "FullyQualifiedName~GraphSearchFailureDetailTests|FullyQualifiedName~SearchOverlay|FullyQualifiedName~Census|FullyQualifiedName~FailureDetail|FullyQualifiedName~BaseQueryBuilder"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <the edited .cs files>
python scripts/a11y_trigger_ledger.py --check
# the whole Windows unit run under the desktop lock before the push
```

## Acceptance

- Every fact above red on the tests-only commit and green on the fix; every mutation red; the trigger ledger unchanged; the Swift and a11y-check lanes green.
- The NVDA phrases recorded for base and head.
- `gh pr view N --json closingIssuesReferences` is empty.
- PR title: `fix(core): a search the index refuses says plain words on both hosts`. A "For the owner" heading: the spoken line before and after, the repeat's silence (Call 7), the Bases lines with the nested row suffix, and the wording alternatives Call 1 lists. Predicted count: found 3, fixed here 1 (Find 1), deferred 0, filed 1 (Find 2); Find 3 is a #2012 line.

## Calls for the lead

1. **The sentence.** Step 0: 28 of the 49 searches FTS5 refuses as queries are ordinary text with one symbol (`#project`, `don't`, `e.g.`, `what?`, `foo, bar`), where "Check its quotes and operators" names nothing the reader typed. The red team adds: after the lead, "This search can't be read." says the failure a second time, and in a screen-reader-first app "can't be read" can be heard as the reader or the file failing. Recommended: **`Check the quotes, symbols and operators in your search.`**, which completes every lead it lands after without restating it: `Search error: Check …` (both hosts, and under the Mac's "Search error" heading), `Preview failed: file.matches: Check …`, `file.matches: Check …`, `slate: invalid search query: Check …`. Cost: it departs from the sentence the owner saw; "For the owner" shows it beside the alternatives. Alternatives: (a) the owner-seen form with "symbols" added, `This search can't be read. Check its quotes, symbols and operators.` (restates after "Search error:", doubles after "invalid search query:"); (b) the example unchanged (misleads on class B); (c) an action, `Remove its symbols, or put its words in double quotes.` (fixes every class: `"AND"`, `"#project"` and `"well-known"` all run); (d) name the symbol SQLite names (`near "#"`), which keeps each line distinct but sniffs SQLite's text further and needs fallbacks for `near ""` and an open quote.
2. **"Search error:" stays.** It is core's `SearchFailed` template (`a11y.rs:2155`), shared by every search failure, and a `Db` or `Io` detail needs it. With Call 1's recommended wording the line has one lead and no restatement. Recommended: keep it. Cost: none beyond Call 1. Alternatives, to drop it for this failure only: a new unit event `A11yEvent::SearchQueryUnreadable` the host picks on `VaultException.InvalidQuery` (an FFI change: both hosts' bindings and consumers, a corpus witness and golden line, both trigger ledgers and both corpus censuses), or core's `SearchFailed` render leaving out its lead when the detail is its own constant (no FFI change, but core compares a host-supplied string with its own copy).
3. **Class C goes to the sentence too, against the brief's scope.** The brief excludes "the column filter misclassified as Db". Step 0 shows that item is wider than column filters: hyphenated words, dates, links, `TODO:` and a NEAR distance all reach `Db` and say `Search error: sqlite error: no such column: known`. Each is an FTS5 parse failure by SQLite's own source. Recommended: take it, two markers in the function this change rewrites (Design 2, Test 3). Cost: #2012's column-filter item is taken here, so the PR takes two of #2012's items; Bases' `file.matches("foo:bar")` becomes a view error where it failed the whole view; the accepted risk that a schema mismatch in FTS5's content statement would read as a refused query. Alternative: leave class C on `Db`, as the brief scoped it, with step 0's widened list in the #2012 comment; a reader who searches `well-known` keeps hearing "no such column: known".
4. **Bases follows on both paths.** Core's change reaches Bases on both hosts: a spoken preview and five shown surfaces. Recommended: Design 3, `file.matches: ` and the sentence on the top-level path and in the interpreter's `file_matches`, which keeps the two paths equal. Cost: `engine.rs` in a search PR; Bases text changes on both hosts (in the NVDA pass and the #1954 line); the nested path keeps its unrelated row suffix, recorded for the owner. Alternative: the top-level line only, which leaves `file.matches: invalid search query: Check …` on the nested path.
5. **The Mac's red.** Test 7's oracle is core's detail, so it is red on main's Mac code whatever core says, and a draft PR that touches only `apps/slate-mac/` (the test without the fix) runs the Swift lane, the a11y-check lane and the Windows census tier. Recommended: one such throwaway draft probe, closed and its branch deleted once the test is seen red, as AGENTS.md asks for a test whose machine is not here. Cost: one Swift lane run on Namespace (the home Mac runner is offline), the a11y-check run and the census tier. Alternative: no probe; the red rests on reading (the arm at `AppState.swift:26018-26019` adds the lead), and the PR says so.
6. **Find 2 (ordinary text with a symbol is refused) is filed** (labels bug, rust), with step 0's lists and its failing test. Recommended: file it; how a typed search maps onto FTS5's syntax is a search-semantics design for both hosts and the owner. Cost: one issue. Until it is fixed, a reader who searches a tag hears the sentence and must remove the `#`.
7. **A second unreadable search is silent.** Both hosts announce a search's summary only when it changes (contract S7; the Mac's `.onChange`), and today each refusal's text differed because it held the query. With one fixed sentence, a reader who replaces `AND` with `#project` in an open overlay hears nothing; the panel still shows the line. Recommended: accept it, as S7 accepts it for two searches with the same result count; pin it with Test 8's new fact, record it under SD-6 and "For the owner", and ask for the Mac's half in the VoiceOver line. Cost: a reader who edits a refused search into another refused one gets no new speech. Alternatives: announce every error publish even when unchanged (a host change on both platforms, against S7's dedup, with its own facts), or Call 1's alternative (d), which makes each line distinct.

## Red team record

A fresh Opus xhigh red team read the first draft against the worktree at c4d1cd60, the probes, SQLite's source, #2012 and #2020: 1 blocker (conditional on Call 4), 3 major, 11 minor. All taken.

- **B1, the nested Bases path keeps the doubled lead (taken):** `SqlVaultLookup::file_matches` wraps `VaultError`'s `Display`; Design 3 maps `InvalidQuery` to its message there, Test 5 covers `or:` and pushdown off, and the row suffix is recorded for the owner.
- **M1, one fixed sentence silences a repeated failure (taken):** settles 4, Call 7, Test 8's new fact, NVDA steps 2 to 4 close the overlay between searches and record the silence.
- **M2, a Bases view error is spoken in the builder preview, and five shown surfaces were missed (taken):** current state, Design 4, NVDA step 5, the #1954 line.
- **M3, the recommended sentence restated the failure, "can't be read" can mislead a screen-reader user, and the calls offered no lead-completing form (taken):** Call 1 now recommends a lead-completing sentence, with the owner-seen form and three others as alternatives, and (d) for naming the symbol.
- **Minors (taken):** the expression-tree limit is a missing parse-failure text (a marker, a Test 2 case, a mutation); the step 0 log re-saved with all three facts, and the stray-row claim rests on the earlier isolated run; the "no other error" claim reworded to the content statement's accepted risk, and the error-code gate weighed and left out; the graph fact's real message; S6's "never shows it verbatim as guidance" and its moved citation in the amendment; the FFI crate's variant doc (left, now true) and the `a11y.rs` witness (takes the constant); the CLI is read by screen readers, and it loses SQLite's reason; #1527 named, the Mac's first `vaultErrorDetail` call, `humanReadableVaultError` as the host-owned alternative, and the Mac's own "Search error" heading; the a11y-check lane; the verification commands (no `make` here; filters after `--`; the target directory); one date convention.

## What delivery changed (2026-11-20, PR #2028, head cf7bdbb8)

**What the code does that the spec did not say.**

- The constant's name stayed `SEARCH_QUERY_UNREADABLE`, though the ruled sentence no longer says "can't be read": the name describes the condition, the index cannot read the query.
- The session facts hold core to a literal in the test module (`REFUSED`), so the tests-only commit compiles on main and fails on behaviour; `a11y::tests::the_unreadable_search_is_cores_sentence` pins the constant to the literal and lands with the constant.
- The damaged-index fact zeroes the leaves through the session's own connection (no second session); FTS5 keeps its averages at id 1 and its structure record at id 10.
- The positive control, `every_query_fts5_reads_still_runs`, also runs refused texts in double quotes (`"#project"`, `"don't"`, `"well-known"`, `"https://example.com"`), which #2026's body cites.
- The diff red team added two facts the spec did not list: `slate query` over a refused `file.matches` (a view error, exit 2; main exits 1 for `well-known`), and the Mac's half of the repeat's silence (`SearchOverlayVoiceTests.testASecondRefusedSearchIsNotAnnouncedAgain`, red by reading only). The Bases every-path fact checks each path's row (none when pushed down, a seeded note when interpreted).
- Contract 29 gained a line under S6 pointing at SD-6's amendment, and the amendment credits the owner with the direction and the lead with the rulings.

**What was measured that the spec expected otherwise.**

- Red on the tests-only commit: Rust 7 facts (4 session, 2 Bases, 1 CLI), Windows 10 facts. The Mac probe draft #2025 ran `AppStateTests.testAnInvalidQuerySaysCoresDetailWithNoLeadOfItsOwn` on main's Mac code: 4 assertion failures, on the lead and the sentence. The push was allowed; the PR is closed and its branch deleted.
- Green: the touched Rust targets, `cargo test --workspace` (3,328 passed), fmt, clippy, bench compile, license headers; the related Windows classes (1,672 passed); the trigger ledger, the parity matrix and the census trait checks current. 11 Rust mutations, each red.
- NVDA (2026.3.1, "most"): every expected phrase heard on base and head. Three routes differed from the spec: the overlay reopens holding its last query (each step cleared the field), the builder runs its preview only after an expression changes (step 6 added a condition and typed and deleted a letter), and the banner is the object after the Quick filter.

**Found on the way.** Found 6, fixed here 2, deferred 1, filed 2.

- Fixed here: a damaged full-text index classified as a refused query (Find 1).
- Filed #2026 (Find 2): ordinary text with a symbol is refused as a search.
- Filed #2027 (the NVDA pass): Add condition in the Windows Bases builder moves focus to the Quick filter, on main and head.
- Recorded on #2012: a damaged index's `Db` text is developer text (Find 3).
- Fixed here (the merge judge's first minor, 37d28b85 red, cf7bdbb8): a damaged database schema whose SQLITE_CORRUPT message says "syntax error" was heard as the reader's query; `map_fts5_error` classifies an SQLITE_ERROR only. The spec's Design 2 had weighed a code gate and left it out because no corruption text held a marker; the schema-parse text does.
- Deferred on #1955 (the diff red team's second minor, older than the PR): the two Bases `file.matches` paths take different arms for a `Db` or `Io` error from the index.

**Reviews.** Spec red team: 1 blocker, 3 major, 11 minor, all taken. Diff red team: 0 blockers, 0 major, 9 minor, 7 nits, all taken but the tests-only commit's subject scope (its minor 2 deferred to #1955 by the lead). Codex round 1 on 2f2bf36a: approve. Merge judge on 2f2bf36a: Merge, no blocker or major; its minor 1 and nit 4 taken in 37d28b85 and cf7bdbb8. Codex round 2 on the delta: approve.
