# N1 executable spec — Engine: evaluator, planner/execution/cache, sort/group/summaries, tasks, full-text

Issues: N1-1 ([#694](https://github.com/coryj627/slate/issues/694)) · N1-2 ([#695](https://github.com/coryj627/slate/issues/695)) · N1-3 ([#696](https://github.com/coryj627/slate/issues/696)) · N1-4 ([#697](https://github.com/coryj627/slate/issues/697)) · N1-5 ([#698](https://github.com/coryj627/slate/issues/698)). Milestone: [GH 14](https://github.com/coryj627/slate/milestone/14). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 5–9, 16; DoD §N-B/§N-C/§N-D/§N-F). Function semantics: [01_research_brief.md](../01_research_brief.md) §2–§3 normative.
Backend norms: fmt/clippy pre-push, censuses, host-independent slate-core.

**Execution order: N1-1 → N1-2 → { N1-3 ∥ N1-4 ∥ N1-5 }.**

Baseline facts (verified 2026-07-06, this worktree):

- Property storage: `properties` (`value_kind`, `value_text` JSON, `value_text_norm` lowercased-atomic) + `properties_list_values` (per-element `value_norm`) with composite indexes (properties_db.rs:46–70; migrations 005/007). Case-insensitive equality pushdown = `value_text_norm` probe; list membership = `properties_list_values` probe.
- Read APIs already shipped: `files_with_property` (session.rs:1575), `files_with_property_key` (:1599), `list_property_keys` (:1590).
- Tags: `tags` table + `file_tags` semantics from #564–#567 (frontmatter + inline, nested-child matching per search_db.rs:60) — **do not re-litigate**.
- Tasks: `tasks` table, migration 008 (+ 009/010 indexes): `ordinal` (stable per parser version), `text` (metadata-stripped), `status_char` (verbatim 1-char), `completed` (derived, indexed), `due_ms`, `scheduled_ms`, `priority` (Tasks-plugin emoji set), `recurrence`, `line`, `byte_offset`.
- FTS: `files_fts` FTS5 + `full_text_search` with `CancelToken` per-row checks and `SearchScope` (search_db.rs:117); its `QueryResultSet {rows, summary}` (:100) is the FTS shape and is **not** retrofitted (decision 7).
- Links: `links` table (`resolved_path` indexed) — feeds `file.links`, `file.backlinks`, `hasLink`, `linksTo`.
- No load-everything: paging is mandatory on list-like operations (05 §9.3.1); the engine streams candidates in batches.
- Budgets (05 §9.5): indexed structured query < 50 ms @ 10k, < 200 ms @ 50k; memory budgets per SessionConfig (05 §9.3.5).

---

## N1-1 · Formula evaluator (#694) — PR 1

New module `crates/slate-core/src/bases/eval.rs`: `pub fn eval(expr: &Expr, ctx: &EvalCtx) -> Result<Value, EvalError>`.

### Types (pinned)

```rust
pub enum Value {
    Null, Bool(bool), Number(f64), Text(String),
    Date(DateValue),                 // ms epoch + has_time flag (date-only vs datetime)
    Duration(i64),                   // ms
    List(Vec<Value>), Object(BTreeMap<String, Value>),
    Link(LinkValue),                 // target, display, resolved_path: Option<String>
    File(FileHandleValue),           // row-backed: path + lazily-loadable fields
    Regex(String, String),           // pattern, flags
}

pub struct EvalCtx<'a> {
    pub file: &'a RowContext,        // the current row's file fields + properties (or task row, N1-4)
    pub this: Option<&'a RowContext>,// decision 20: embed host / base file / active note — session-supplied
    pub formulas: &'a ResolvedFormulas, // memoized per-row formula results (cycle-free by N0-2 rule 5)
    pub now_ms: i64,                 // captured once per execution run (§N-B)
    pub vault: &'a dyn VaultLookup,  // link resolution, backlinks, file() — trait so eval stays testable
    pub warnings: &'a WarningSink,   // rule-2/3 data warnings accumulate here (deduped per view, non-fatal)
}

// RowContext (pinned — shared by N1-1/N1-2/N1-4): one shape for both row sources.
pub struct RowContext {
    pub file_path: String,
    pub file_fields: FileFields,             // name/path/folder/ext/size/ctime/mtime + tags/links/backlinks + in_degree/out_degree (lazy via VaultLookup)
    pub properties: Vec<(String, Value)>,    // decoded frontmatter, declaration order
    pub task: Option<TaskRow>,               // Some(⋯) only under RowSource::Tasks (N1-4): ordinal, text, status_char, completed, due_ms, scheduled_ms, priority
}
```

(`ResolvedFormulas`, `VaultLookup`, `DateValue`, `LinkValue`, `FileHandleValue` are implementation-latitude types inside `bases::eval`; `RowContext` is pinned because three issues consume it.)

### Normative rules

1. **v1 function table** — every brief-§3 entry has exactly one status, pinned here:
   - **Evaluate:** `if`, `date`, `duration`, `now`, `today`, `number`, `min`, `max`, `link`, `list`, `file`, `escapeHTML`; all Date fields/methods (`format` via a Moment-token subset: `YYYY MM DD HH mm ss ddd MMM` + passthrough literals — unsupported tokens ⇒ `EvalError`, fail-loud); all String methods; all Number methods; all List methods incl. expression-based `filter`/`map`/`reduce`; `any.isTruthy/isType/toString`; Object `isEmpty/keys/values`; Regex `matches`; String/Regex `replace` incl. `$n` captures and `g`-flag semantics; Link `asFile`/`linksTo`; File fields + `asLink`/`hasLink`/`hasProperty`/`hasTag` (nested, tags-table semantics)/`inFolder` (recursive); **`file.inDegree`/`file.outDegree`** (Slate extension fields — links-table degree counts, decision 5; the milestone's "ship the basics" commitment).
   - **Parse-only, render-as-text:** `html`, `image`, `icon` (value = tagged Text; grid shows the string; no HTML/webview — 05 §1.3). Documented in help (N4-5).
   - **Excluded:** `random` (§N-B) — evaluation ⇒ `EvalError::Unsupported`, fail-loud.
2. **Totality over data; errors only for structure** (program decisions 5–6 — the load-bearing line). Documented coercions evaluate (JS parity: `number()` on date/bool/string; string `+` concat when either side is Text; numeric-looking Text coerces in arithmetic and ordering comparisons). Beyond that, **data-shaped mismatches never error**: an ordering comparison (`> < >= <=`) whose sides aren't comparable — either side Null, or cross-family types with no documented coercion — evaluates to `Bool(false)` + a deduped view warning; arithmetic on incompatible operands evaluates to `Null` + warning; IEEE totality for numbers (`x/0` ⇒ ±Infinity per JS parity; `0/0`/NaN results normalize to `Null` + warning). `EvalError` (fatal per rule/engine 4) is reserved for **structural** problems: unknown function (`Unsupported` node), wrong function arity or non-coercible argument *kind* (e.g. `hasTag(5)`), `this` without context, filter-only function misuse, unsupported `format` token. Rationale pinned: the default dataset is the whole vault (brief §1), so `price > 2.1` must run on files without `price` — Null-comparison-as-error would kill nearly every real view, and JS parity itself makes `undefined > 5` false, not an error. The residual delta from *full* JS coercion is recorded in gap O13 and in help.
3. Missing property ⇒ `Null`. `isEmpty()` on Null ⇒ true (brief §3: "true if empty **or absent**"). Equality with Null (pinned): `null == null` ⇒ true; `null == <non-null>` ⇒ false; `!=` complements. Ordering with Null ⇒ false + warning (rule 2). Filters: a statement evaluating to Null is **falsey without error** (`if` truthiness per `isTruthy`); only a *structural* `EvalError` fails the view (decision 6).
4. Date semantics: `Date ± "dur"` parses duration tokens per brief §2 (calendar-aware for `y`/`M` — add months, clamp day; fixed-width for `w/d/h/m/s`); `Date − Date` ⇒ `Duration` (ms; `number()` of it = ms, brief §2). `now()`/`today()` read `ctx.now_ms` (§N-B).
5. Link equality per brief §2: resolved ⇒ same-file; unresolved ⇒ text equality. `x == this` compares against `ctx.this` (file identity). `this` absent (no context) ⇒ `EvalError::NoThisContext` — fail-loud, surfaced as a view error naming `this`.
6. `formula.x` reads memoized per-row results; formulas evaluate in dependency order (cycle-free by parse). A formula whose eval errors poisons **only** columns/filters referencing it (per-view fail-loud).
7. Determinism: no wall clock (rule 4), no randomness (rule 1), no locale-dependent casing (`lower()`/`title()`/normalization use the same Unicode tables as `value_text_norm`, properties_db.rs:192 idiom).
8. Budget: evaluator is allocation-conscious (`Value` cloning bounded; strings Cow where practical) — it runs per row × per column on 50k-row sets.

### Tests (PR 1)

Unit: every Evaluate-status function with edge cases (empty input, Null receiver, type mismatch, division by zero ⇒ `Number(inf)` per JS parity, missing property) — the vendored milestone test list (../02_milestone_brief.md). Golden: brief-§7 field-report idioms evaluate to expected values over fixture rows. Property: eval never panics; determinism (equal ctx ⇒ equal result); `parse ∘ emit ∘ parse ∘ eval` ≡ `parse ∘ eval`.

- [ ] `Value`/`EvalCtx` + `eval` per rules 1–8; function-status table in module docs
- [ ] Unit + golden + property tests
- [ ] fmt/clippy; host-independent; no I/O (VaultLookup is a trait)

## N1-2 · Planner + SQLite execution + cancellation + cache (#695) — PR 2

New module `crates/slate-core/src/bases/engine.rs`: `pub fn execute(query: &SlateQuery, conn: &Connection, ctx: &EngineCtx, cancel: &CancelToken) -> Result<BasesResultSet, VaultError>`.

### Normative rules

1. **Two-stage execution** (05 §8.3): SQL narrows candidates; Rust finishes. Pushdown-eligible predicates (top-level AND conjuncts only, conservative): `file.ext == lit`, `file.inFolder(lit)` (path-prefix), `file.hasTag(lit…)` (tags table, nested semantics), `file.name/path` equality and `startsWith`, `file.mtime/ctime/size` comparisons, `note.prop == lit` (`value_text_norm` probe), `note.prop.contains(lit)` on list properties (`properties_list_values` probe), `task.*` columns (N1-4), `file.matches(lit)` (N1-5). Everything else — OR-trees, NOT, formulas, method chains — evaluates in Rust over the candidate stream. **Semantics identical either way** (census 2).
2. Candidate streaming: batches of `page_size` (SessionConfig-derived; default 512) with `cancel.is_cancelled()` checked per batch (full_text_search precedent, search_db.rs:123). No full-vault materialization (05 §9.3.1).
3. Row assembly: file fields from `files`; properties decoded from `value_text` JSON to `Value` by `value_kind`; formulas per N1-1 rule 6; columns = `order` list resolved against note/file/formula namespaces (unknown column id ⇒ column of Nulls + view warning, not an error — display concern, not semantics).
4. **Fail-loud propagation** (decision 6): the first `EvalError` in a *filter* aborts that view's execution with `ViewError { construct, row_path }`; an `EvalError` in a *column/summary* poisons that column (error marker cell values) but keeps rows. Rationale pinned: filter errors change membership (unsafe to show), column errors don't.
5. **Cache:** key = (SlateQuery stable hash, vault generation from N0-4 rule 4, `this` identity). **Queries mentioning `now()` never cache** (a cached `now()` diverges from a cold run within the same key — §N-C would be violated); queries mentioning only `today()` additionally key on the current day. A cache hit returns the cached `executed_at_ms` unchanged (honest staleness readout). Value = `BasesResultSet`. Invalidation = generation bump (session-global — coarser than 05 §8.3's per-source-set sketch, recorded as gap G9); no partial invalidation in v1 (correct-by-construction beats clever). Cache ≡ fresh census (§N-C).
6. Determinism (§N-B): before sort (N1-3) rows order by `(file path, task ordinal)`; all iteration over hash containers goes through sorted views.
7. Budgets (decision 16): indexed query < 50 ms @ 10k, < 200 ms @ 50k; unindexed worst case (pure formula filter) documented in BENCHMARKS.md, not gated.

### Tests (PR 2)

Unit: each pushdown rule vs. Rust-eval equivalence (same fixture, force both paths); cancellation under load @ 10k (vendored test list, ../02_milestone_brief.md). Censuses: `census_bases_pushdown_equiv` (random queries × random vaults: pushdown-on ≡ pushdown-off), `census_bases_cache_fresh` (§N-C: random edit/rename/delete interleavings), `census_bases_read_only` (§N-F: vault byte-hash before/after every corpus query). Criterion bench `bases_bench.rs` @ 1k/10k/50k synthetic vaults.

- [ ] `execute` per rules 1–7
- [ ] Censuses + benches recorded in BENCHMARKS.md
- [ ] fmt/clippy clean

## N1-3 · Sort, groupBy, summaries, audio strings (#696) — PR 3

### Normative rules

1. **Sort:** multi-key (view state: Slate's own sort lives in the `slate` view sub-key, decision 3; Obsidian's undocumented sort state is preserved-opaque and **ignored for execution** — recorded interop caveat, brief §6.1 — the notice banner names it when present). Type-aware comparisons (Text: Unicode-aware, same tables as `value_text_norm`; Number; Date; Bool false<true; Null always last); final tiebreak `(path, ordinal)` (§N-B).
2. **groupBy:** one property (brief §1), direction ASC/DESC; groups ordered by key with Null-key group last ("No <property>"); rows within groups keep rule-1 order. `ResultGroup { key: Value, label: String, rows: Range, summaries }`.
3. **Summaries:** the ten milestone-14 defaults — count, filled, empty, unique, min, max, sum, average, earliest, latest — computed per column, per view and per group. Type applicability per brief §4 (numeric set on Numbers, earliest/latest on Dates, count/filled/empty/unique on anything); inapplicable assignment ⇒ view warning + em-dash cell. Obsidian built-in summary *names* map onto these (Average→average, Checked/Unchecked→ documented mapping: Checked = count of true, ships as `checked`/`unchecked` aliases). Median/Stddev/Range parse-preserve as `Unsupported` in v1 (N-E6) — fail-loud only if assigned in an executed view.
4. **Custom summary formulas** (top-level `summaries`): evaluate with implicit `values: List` binding when inside the v1 function set; else fail-loud per decision 6. (`values.mean()` from the docs example: `mean` is **not** in the v1 set — brief §6.2 — so it fails loud with a named error; recorded in help.)
5. **Audio strings** (05 §8.4, decision 19): `audio_summary` grammar pinned — "N notes[, grouped by <prop>: <k₁> <c₁>, <k₂> <c₂>…][, limited to L]. Sorted by <keys>." Task-source variant says "N tasks". `audio_description` per row = primary-column value + non-empty secondary values in column order ("<primary>. <col>: <val>. …"). Non-empty even for empty results ("No results."). Computed in Rust, localized later (decision 22).
6. `limit` applies after sort, before summaries? **No — pinned:** summaries compute over the **post-filter, pre-limit** set with the result reporting both counts (`total_count` = post-filter, `shown_count` = post-limit rows actually shown — named for what it is, deliberately diverging from 05 §8.4's `filtered_count`) so "Read This Year: 42 books, showing 10" stays honest. (Obsidian help is silent; this is the accessible-honest choice; recorded as a deliberate stance in help + gap analysis.)

### Tests (PR 3)

Unit per rule incl. groupBy ordering stability + summary correctness per default (vendored test list, ../02_milestone_brief.md); mixed-type column sort golden; audio-grammar snapshots; property: group partition is total and stable under insertion-order permutation (§N-B).

- [ ] Rules 1–6
- [ ] Unit + golden + property tests
- [ ] fmt/clippy clean

## N1-4 · Tasks as rows + `file.tasks` aggregates (#697) — PR 4

The differentiator (brief §5.2). Two independent surfaces:

### Normative rules

1. **`file.tasks` aggregates** (available everywhere, no extension key needed): `file.tasks.total: Number`, `file.tasks.completed: Number` — one indexed SQL aggregate over the `tasks` table per candidate batch. Usable in filters, formulas, columns ("progress: `file.tasks.completed + '/' + file.tasks.total`" — thread 103074's minimal ask).
2. **`source: tasks` views** (view-level Slate extension, N0-2 rule 6): rows are task items, not files. `task.*` fields: `text` (Text), `status` (Text, verbatim `status_char` — custom status sets survive, migration-008 comment honored), `completed` (Bool), `due`/`scheduled` (Date or Null from `due_ms`/`scheduled_ms`), `priority` (Number or Null), `file` (File value of the owning note — so `task.file.name`, `task.file.hasTag(…)` compose). `note.*`/`formula.*` resolve against the owning file (formulas run per task row).
3. File-scoped predicates in a tasks view (e.g. `file.hasTag("project")`) filter the **owning file** — the natural funnel ("tasks in project notes"). Pushdown: `completed`, `due_ms`/`scheduled_ms` ranges, `priority` hit the migration-009/010 indexes.
4. Row identity for §N-B ordering and grid selection: `(file path, ordinal)` (stable per parser version — migration-008 comment).
5. Interop caveat pinned (decision 8): a `.base` with `source: tasks` opened in Obsidian shows their unknown-key behavior (ignored key ⇒ they'd render *files*); the help doc (N4-5) says exactly this and recommends keeping task views in Slate-authored bases. Round-trip is unaffected (key preserved).
6. Default columns when `order` is absent in a tasks view: `task.text`, `task.status`, `task.due`, `task.file`.

### Tests (PR 4)

Unit per rule; fixture vault with custom status chars, emoji metadata, recurrences; pushdown-equiv census extension covering task predicates; golden: thread-103074's two motivating dashboards (progress column per project; open `#next-action` tasks across notes) as executable fixtures.

- [ ] Rules 1–6
- [ ] Fixtures + census extension + goldens
- [ ] fmt/clippy clean

## N1-5 · Full-text filter function (#698) — PR 5

### Normative rules

1. `file.matches("query")` in filter position: FTS5 match against `files_fts` (05 §8.8), candidate-set intersection (pushdown as an `IN (SELECT …)` conjunct when top-level AND; Rust-side membership probe otherwise — one FTS execution per query run either way, memoized).
2. Query string passes through the same sanitization `full_text_search` uses (search_db.rs) — no new FTS dialect. Empty/whitespace query ⇒ matches nothing + view warning (parity with FTS empty-query behavior under Vault scope).
3. Not a formula function in v1: `matches` outside filter position ⇒ `EvalError::FilterOnly` (fail-loud). No snippet/score columns (decision 9).
4. `source: tasks` views: `file.matches` applies to the owning file's body (task text itself is already addressable via `task.text.contains`).
5. Interop caveat pinned (decision 9): unknown function in Obsidian ⇒ their filter errors; file untouched; documented.

### Tests (PR 5)

Unit per rule; composition golden ("in `Projects/` AND matching 'roadmap'" — 05 §8.8's example, executable); pushdown-equiv census extension; determinism (FTS row order never leaks — membership only).

- [ ] Rules 1–5
- [ ] Unit + golden + census extension
- [ ] fmt/clippy clean

**Wave-2 exit:** pushdown-equiv, cache≡fresh, and read-only censuses clean; benches recorded; evaluator function-status table complete.
