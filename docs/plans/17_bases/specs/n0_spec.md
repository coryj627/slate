# N0 executable spec — Format layer: expression language, `.base` parser, serializer, scanner discovery

Issues: N0-1 ([#690](https://github.com/coryj627/slate/issues/690)) · N0-2 ([#691](https://github.com/coryj627/slate/issues/691)) · N0-3 ([#692](https://github.com/coryj627/slate/issues/692)) · N0-4 ([#693](https://github.com/coryj627/slate/issues/693)) · N0-5 ([#713](https://github.com/coryj627/slate/issues/713)). Milestone: [GH 14](https://github.com/coryj627/slate/milestone/14). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 2–6; DoD §N-A/§N-B/§N-D/§N-E). Syntax facts: [01_research_brief.md](../01_research_brief.md) — **normative for every Obsidian-syntax fact in this spec** (§1–§7 Bases, §8 DQL); when in doubt, the brief's primary-source citations win.
Backend norms: fmt/clippy pre-push, censuses for correctness invariants, host-independent slate-core (no macOS deps, no I/O in parsers).

**Execution order: N0-1 → N0-2 → { N0-3 ∥ N0-4 ∥ N0-5 }.** (N0-5 needs N0-1's `Expr` and N0-2's `SlateQuery`/`ViewSource` types; independent of N0-3/N0-4 otherwise.)

Baseline facts (verified 2026-07-06, this worktree):

- Tolerant-parser contract to copy (contract, not code): `crates/slate-core/src/canvas/mod.rs` — entry-level problems ⇒ warning + skip, never hard-fail; only an unusable root degrades the load.
- Frontmatter/YAML precedent: `crates/slate-core/src/frontmatter.rs` — `extract_frontmatter`, `frontmatter_range`, yaml-rust2, warning-not-panic discipline. The `.base` parser is a **new sibling module**, not a frontmatter extension — but reuses the same YAML dependency and warning idioms.
- Properties value model: `crates/slate-core/src/properties_db.rs` — `value_kind` strings, `value_text_norm` normalization (:63, :86), `properties_list_values` per-element rows (:58, :121). `PropertyValue` variants per migration 005/007.
- Tag matching semantics already shipped: `SearchScope::Tag` nested-child matching (search_db.rs:60) — `hasTag("a")` matches `#a/b`. Reuse the same normalization; never write a second tag matcher.
- Wikilink parsing: `links.rs` scanner — reuse for Link literals/values; never a second wikilink parser (XD0-2 precedent).
- Fence discovery has **no existing table to extend**: the 05 §4.5 sketch's `specialized_blocks` was never implemented (verified — migrations 001–020 contain no such table; migration 011's `blocks` indexes `^block-id` anchors only, and reading-pipeline block kinds are derived at render time, reading.rs module docs). N0-4 therefore creates its own `bases_blocks` table.
- Fixture convention: crate-relative under `crates/slate-core/tests/fixtures/` (existing precedent: `tests/fixtures/canvas/`). All `tests/fixtures/...` paths in this program mean that root.
- Migrations: `crates/slate-core/migrations/` — next free slot **021**; migration files carry the documentation-header convention (see 008_tasks.sql).
- Census/bench conventions: `census_*` test fns, `SLATE_CENSUS_FULL=1` via `census_scale()`; criterion benches in `crates/slate-core/benches/`; baselines in `BENCHMARKS.md`.
- Workspace YAML: yaml-rust2 does **not** preserve comments through emit — this is why decision 3 mandates the preservation model below (raw-text retention + targeted splicing), not naive parse→emit.

---

## N0-1 · Expression language: lexer, parser, AST (#690) — PR 1

New module `crates/slate-core/src/bases/expr.rs`: `pub fn parse_expr(source: &str) -> Result<Expr, ExprParseError>`.

### Types (pinned)

```rust
pub struct Span { pub start: u32, pub end: u32 }     // byte offsets into the source string

pub struct Expr { pub kind: ExprKind, pub span: Span } // every node carries its verbatim span (rule 7)

pub enum ExprKind {
    Lit(Lit),                                    // string, number, bool, list, object, regex
    Prop(PropertyRef),                           // note.x / file.x / formula.x / this… / task.x / bare→note
    Index { base: Box<Expr>, index: Box<Expr> }, // p[0], p["sub"]
    Field { base: Box<Expr>, name: String },     // dynamic member access on a COMPUTED value: date(x).year, obj.subprop, this.someProp chains
    Unary { op: UnaryOp, rhs: Box<Expr> },       // !x, -x
    Binary { op: BinaryOp, lhs: Box<Expr>, rhs: Box<Expr> }, // + - * / % == != > < >= <= && ||
    Call { callee: Callee, args: Vec<Expr> },    // global fn or method (receiver in Callee::Method)
    ListExpr { base: Box<Expr>, kind: ListExprKind, body: Box<Expr>, init: Option<Box<Expr>> },
                                                 // filter/map(expr: implicit value,index) / reduce(expr, init: implicit acc)
    Unsupported { raw: String, reason: String }, // decision 6: parses, round-trips, fails loud at eval
}

pub enum Callee { Global(GlobalFn), Method { receiver: Box<Expr>, name: MethodName } }

pub enum PropertyRef {
    Note(String), File(FileField), Formula(String),
    This,                                        // bare `this`
    ThisNote(String),                            // this.<frontmatter prop>
    ThisFile(FileField),                         // this.file.<field>
    TaskField(TaskField),                        // task.* — only legal under source: tasks (N1-4); parse always
}
```

**`Prop` vs `Field` boundary (pinned):** the parser produces `Prop` whenever a dotted path starts from a *namespace head* (`note`, `file`, `formula`, `this`, `task`, or a bare identifier ⇒ `Note`) and stays within the closed field sets — so `file.name` is `Prop(File(Name))`, never `Field`. `Field` is only for member access on a **computed** value (`date(x).year`, `link(p).asFile().folder`, object subprops). `FileField` is a closed enum of the brief-§4 table **plus the two Slate degree extensions `InDegree`/`OutDegree`** (decision 5); an unknown `file.<name>` ⇒ `Unsupported`. All AST types derive `Serialize`/`Deserialize` now (N2-2's versioned `query_json` envelope depends on it — pin the shape early, not at Wave 3).

`GlobalFn` and `MethodName` are **closed enums enumerating exactly the brief §3 inventory** (including `random`, `html`, `image`, `icon` — parse-known; their v1 *evaluation* status is N1-1's table). A call to a name outside the inventory parses as `Expr::Unsupported` with the verbatim source text — not an error.

### Normative rules

1. **One grammar for filters and formulas** (brief §2). Operator precedence: JS parity — `! -(unary)` > `* / %` > `+ -` > comparisons > `&&` > `||`; parentheses group.
2. Literals per brief §2: `"…"`/`'…'` strings — **escape set pinned** (the Obsidian docs are silent; recorded stance): `\"`, `\'`, `\\`, `\n`, `\t` interpret; any other `\x` preserves the backslash+char verbatim with a parse warning (round-trip-safe either way). Numbers (int/float); bare `true`/`false`; `[…]` lists; `{"k": v}` objects; `/…/flags` regex (flags: `g` recognized, others preserved-unsupported). **Regex-vs-division disambiguation (pinned, JS lexer rule):** `/` starts a regex literal only in *expression-start* position (start of input, or after an operator, `(`, `[`, `,`, or a combinator keyword); after a value it is always the division operator — so `a / b / c` is division twice and `matches(/b/, x)` is a regex. Duration strings are **plain strings** at parse time; duration semantics live in the evaluator (`date + "1M"` is `Binary(Add, date, Lit(Str))` — brief §2).
3. Property references: bare identifier ⇒ `Note` (brief §4); `note.x`/`note["x"]`, `file.<field>` for the closed field set (brief §4 table + `inDegree`/`outDegree`; unknown `file.` field ⇒ `Unsupported`), `formula.x`, `this` / `this.<prop>` ⇒ `ThisNote` / `this.file.<field>` ⇒ `ThisFile`, `task.<field>` (closed set: `text`, `status`, `completed`, `due`, `scheduled`, `priority`, `file` — N1-4 owns semantics).
4. Method-style calls type-check **by name only** at parse time (receiver types are dynamic; the evaluator owns type errors — decision 6 routes those to fail-loud).
5. `filter`/`map`/`reduce` bodies are bare expressions with implicit `value`/`index`/`acc` identifiers (brief §3 List). Inside a list-expr body, those identifiers resolve to the implicit bindings *before* falling back to `Note(...)`.
6. Determinism: `parse_expr` is a pure function; equal input ⇒ equal AST (derive `PartialEq`; no interning that leaks order).
7. Every AST node retains its **verbatim source span** so N0-3 can re-emit untouched expressions byte-identically.

### Tests (PR 1)

- Unit per rule; golden AST snapshots for every example expression in the brief (§1 example file's formulas/filters, §2 idioms, §3 signatures, §7 field-report idioms).
- Property (proptest): parse never panics on arbitrary strings; parse determinism; parse→emit(verbatim span)→parse fixpoint.
- Exhaustive inventory test: every brief-§3 function name parses to its enum arm; a name one edit-distance off parses to `Unsupported`.

- [ ] Types + `parse_expr` per rules 1–7
- [ ] Golden + property + inventory tests
- [ ] fmt/clippy clean; host-independent; no I/O

## N0-2 · `.base` parser → `SlateQuery` (#691) — PR 2

New module `crates/slate-core/src/bases/mod.rs`: `pub fn parse_base(source: &str) -> (BaseFile, Vec<BaseWarning>)`.

### Types (pinned; 05 §8.2 refined)

```rust
pub struct BaseFile {
    pub raw: String,                       // verbatim source — the round-trip substrate (decision 3)
    pub filters: Option<FilterNode>,       // base-wide
    pub formulas: Vec<(String, Expr)>,     // declaration order preserved
    pub properties: Vec<(String, PropertyConfig)>, // displayName + preserved unknown sub-keys
    pub summaries: Vec<(String, Expr)>,    // custom summary formulas (values keyword ⇒ implicit binding)
    pub views: Vec<ViewDef>,
    pub preserved: PreservedYaml,          // unknown top-level keys, key order, comments — opaque
}

pub enum FilterNode {
    Stmt(Expr),                            // single statement string
    And(Vec<FilterNode>), Or(Vec<FilterNode>),
    Not(Vec<FilterNode>),                  // semantics NOT(OR(list)) — brief §2
}

pub struct ViewDef {
    pub view_type: ViewType,               // Table | List | Cards | Map | Other(String)
    pub name: String,
    pub limit: Option<u64>,
    pub filters: Option<FilterNode>,       // ANDed with base-wide (brief §1)
    pub group_by: Option<GroupBy>,         // { property: PropertyRef, direction: Asc|Desc }
    pub order: Vec<String>,                // property ids, verbatim
    pub summaries: Vec<(String, SummaryRef)>, // Builtin(name) | Custom(name)
    pub source: ViewSource,                // Files | Tasks (Slate extension key `source: tasks`, N1-4)
    pub slate_state: Option<serde_json::Value>, // Slate-authored view state under the `slate` sub-key (decision 3)
    pub preserved: PreservedYaml,          // Obsidian's undocumented per-view state (brief §6.1) — opaque, verbatim
}
```

`PreservedYaml` (pinned): `Vec<PreservedRegion>` where `PreservedRegion { span: Span, text: String }` — byte spans into `raw` plus the verbatim text. **The parser records a span for every structural node** (each top-level key, each view entry, each formula/property/summary entry, each filter node) via yaml-rust2 markers — N0-3's splicing depends on these spans existing, so they are part of this PR's contract, not N0-3's discovery.

**`SlateQuery` (pinned here — this supersedes the 05 §8.2 sketch, recorded as gap G7):** the executable form N1's engine, N0-5's DQL converter, N2's FFI/envelope, and N4's builder all consume.

```rust
pub struct SlateQuery {
    pub source: QuerySource,                   // All | Folder(String) | Tag(String) | Recent{days} | Linked{from_path, depth} — 05 §8.2 kept; Custom parses to Unsupported (gap G4)
    pub row_source: RowSource,                 // Files | Tasks (from ViewDef.source, decision 8)
    pub filters: Option<FilterNode>,           // recursive; NOT the sketch's flat Vec<FilterCondition> (gap G7)
    pub formulas: Vec<(String, Expr)>,         // declaration order (determinism §N-B; not the sketch's HashMap)
    pub group_by: Option<GroupBy>,             // GroupBy { property: PropertyRef, ascending: bool }
    pub sort: Vec<SortKey>,                    // SortKey { expr: Expr, ascending: bool }
    pub columns: Vec<ColumnSelection>,         // ColumnSelection { id: String, display_name: Option<String> }
    pub summaries: Vec<(String, SummaryRef)>,
    pub limit: Option<u64>,
    pub view: ViewSpec,                        // Table | List { … } — Cards/Map/Other(ViewType) carry a table_fallback flag (decision 4)
}
```

Derived per view: `pub fn view_query(base: &BaseFile, view: usize) -> SlateQuery` — base filters ∧ view filters; `ViewDef.view_type` maps to `ViewSpec` (`Table`/`List` directly; `Cards`/`Map`/`Other` ⇒ `ViewSpec::Table` with `fallback_from: Some(name)`). The builder (N4) constructs `SlateQuery` directly; `save_as_base_file` (N2-1) goes through `BaseFile`. All types serde-derived; the persisted form is N2-2's versioned envelope `{"v":1, "query": …}`.

### Normative rules

1. **Load gate:** root must be a YAML mapping. Anything else ⇒ degraded `BaseFile` (raw retained, everything else empty) + `BaseWarning::ParseFailed` — the file still opens (banner) and still round-trips (raw). Empty file ⇒ empty base, no warning (Obsidian's "New base" starts minimal).
2. Top-level keys `filters`/`formulas`/`properties`/`summaries`/`views` parse per brief §1; **any other top-level key** ⇒ `preserved`, verbatim, warning-free.
3. Filter grammar per brief §2: a mapping with exactly one of `and`/`or`/`not` (list values, heterogeneous nesting); a string is a statement parsed via N0-1. A filter mapping with zero or multiple combinator keys, or a non-string non-mapping list entry ⇒ that node becomes `Stmt(Unsupported)` + warning (tolerant; fail-loud at eval).
4. Views: `type` unknown ⇒ `ViewType::Other(name)` (decision 4 fallback); missing `name` ⇒ synthesized `"View N"` + warning (embeds address by name); duplicate names ⇒ warning, first wins for `#View` addressing. `groupBy.direction` accepts `ASC`/`DESC` case-insensitively, default `ASC` (brief §6.3). Unknown per-view keys ⇒ `preserved`. The `slate` sub-key parses as Slate state; Slate never writes any other novel key into a view (decision 3).
5. Formula/summary expression strings parse via N0-1; a formula that fails expression-parse becomes `Unsupported` + warning (round-trips verbatim; referencing views fail loud at eval). Circular formula references are **detected at parse** (graph over `formula.x` refs) ⇒ each cycle member becomes `Unsupported { reason: "circular reference" }` (brief §1: Obsidian forbids cycles).
6. `source:` view key (Slate extension): absent or `files` ⇒ `Files`; `tasks` ⇒ `Tasks`; anything else ⇒ `Files` + warning. Namespaced so Obsidian ignores it structurally (it's just an unknown key there).
7. Determinism: pure function, no I/O, equal input ⇒ equal output.

### Tests (PR 2)

- Fixture: the brief-§1 verbatim example + hand-authored fixtures covering every rule (unknown keys at all levels, plugin view type, `not` semantics, duplicate view names, circular formulas, `source: tasks`, `slate` sub-key).
- Unit per rule; property: never panics on arbitrary YAML/text; determinism.
- Cross-check: `view_query` output for the brief example matches a hand-written expected `SlateQuery` (golden).

- [ ] Types + `parse_base` + `view_query` per rules 1–7; **`SlateQuery` pinned as above (serde-derived)** — downstream waves consume it as-is
- [ ] Structural node spans recorded (N0-3's splice substrate)
- [ ] Fixtures + golden + property tests
- [ ] fmt/clippy; host-independent; no I/O

## N0-3 · `.base` serializer + round-trip corpus (#692) — PR 3

Same module: `pub fn serialize_base(base: &BaseFile, edits: &[BaseEdit]) -> Result<String, SerializeError>`.

### Normative rules

1. **Untouched ⇒ byte-equal:** `serialize_base(parse_base(s).0, &[])` returns `s` exactly (the `raw` field is authoritative when no edits apply). This is the 05 §8.1 hard requirement and it is trivially true by construction — test it anyway, forever (§N-A).
2. **Edits are targeted splices**, not re-emission: `BaseEdit` is a closed enum — the **complete v1 set** (additions are spec amendments, not judgment calls): `SetViewKey { view, key, value }` (type/name/limit/groupBy/order), `AddView`, `RemoveView`, `RenameView`, `SetViewFilters`, `SetTopLevelFilters`, `SetFormula`, `RemoveFormula`, `SetDisplayName`, `SetSummaryAssignment`, `SetSlateState`. Each edit rewrites only the minimal YAML span it owns (the structural node spans recorded by N0-2), preserving surrounding bytes, key order, quoting style, comments, and final-newline state. Line-oriented splicing over the retained `raw` + node spans; **never** parse→mutate→emit the whole document through yaml-rust2 (baseline fact: it drops comments).
3. New content Slate writes (added views, builder-authored files) uses a **pinned canonical style**: two-space indent, keys in the brief-§1 example order (`type`, `name`, `limit`, `groupBy`, `filters`, `order`, `summaries`, then `source`/`slate`), double-quoted strings only where YAML requires, LF endings, trailing newline. New files created by the builder are entirely canonical-style.
4. Filter nodes edited structurally re-emit in the mapping form (brief §1 example shape); a `Stmt` whose `Expr` is untouched re-emits its verbatim span (N0-1 rule 7).
5. `Unsupported` nodes and `preserved` YAML re-emit byte-verbatim always — an edit that would have to rewrite inside preserved content is a `SerializeError::WouldClobber` (callers surface it; never silent data loss).
6. Determinism: equal `(base, edits)` ⇒ equal output.

### Tests (PR 3)

- **Golden corpus** `tests/fixtures/bases/`: the brief-§1 example verbatim; ≥ 6 hand-authored Obsidian-style files exercising comments, key-order variance, single/double quoting, plugin view types, undocumented view-state keys (row height, column widths — realistic names), a base with only a filter string, a base with every documented key. Corpus rule (XD precedent): format-faithful now; genuine Obsidian-app-written captures are added as they become available and **must never be normalized**.
- Byte-equal round-trip over the whole corpus in CI (§N-A).
- Per-edit minimal-diff tests: apply each `BaseEdit` kind to each fixture; assert untouched lines byte-identical (diff-shape golden).
- Adversarial census `census_bases_roundtrip` (§N-A): random YAML mutations of corpus files — parse → serialize untouched ⇒ byte-equal; parse → random valid edit → serialize ⇒ minimal diff or explicit `WouldClobber`; never panic, never drop content.

- [ ] `serialize_base` per rules 1–6; `BaseEdit` closed enum
- [ ] Golden corpus + byte-equal CI + minimal-diff tests + census
- [ ] fmt/clippy; host-independent

## N0-4 · Scanner discovery: `bases_files` index + fence discovery (#693) — PR 4

### Schema (migration 021, per 05 §8.3 with the shipped-schema idioms)

```sql
CREATE TABLE bases_files (
  file_id           INTEGER PRIMARY KEY REFERENCES files(id) ON DELETE CASCADE,
  name              TEXT NOT NULL,          -- filename stem, the palette/embed display name
  parsed_query_json TEXT NOT NULL,          -- BaseFile summary: view names/types/sources (NOT raw yaml — that's the filesystem's copy, 05 §9.2)
  warning_count     INTEGER NOT NULL,
  parser_version    INTEGER NOT NULL,
  indexed_at_ms     INTEGER NOT NULL
);
CREATE INDEX idx_bases_files_name ON bases_files(name);

-- Fence discovery. NEW table (the 05 §4.5 sketch's specialized_blocks was
-- never implemented; reading-pipeline block kinds derive at render time).
CREATE TABLE bases_blocks (
  file_id     INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
  fence_kind  INTEGER NOT NULL,             -- 0=base, 1=slate-query, 2=dataview
  source_text TEXT NOT NULL,                -- fence body, verbatim
  line        INTEGER NOT NULL,
  byte_offset INTEGER NOT NULL
);
CREATE INDEX idx_bases_blocks_file ON bases_blocks(file_id);
```

Deviations from the 05 sketches, recorded: no `raw_yaml` column — §9.2 (SQLite is index, not source of truth) wins; the parser re-reads the file on open. `path TEXT PRIMARY KEY` becomes `file_id` FK per the shipped `files`-table idiom. `bases_blocks` replaces the never-implemented `specialized_blocks` (gap G1).

### Normative rules

1. Scanner: files with extension `base` parse via N0-2 during scan; row upserted with view summary + warning count; parse failure still rows (degraded flag in JSON) so the UI can list-and-explain, never hide. `.base` files are **not** markdown (no FTS body row, no links/tags/properties extraction from them).
2. Fence discovery: during markdown scan, ` ```base `, ` ```slate-query `, and ` ```dataview ` fenced blocks index into the new `bases_blocks` table above. ` ```dataviewjs ` fences are **not** discovered — they stay ordinary code blocks forever (05 §8.1; program decision 2). Content is **not** parsed at scan time (embeds parse on render — parse-on-open, XD decision-4 precedent); discovery exists so reading view/editor know where grids go without rescanning bodies, and so N4-5's E2E can enumerate embeds.
3. Incremental ≡ full: editing/creating/deleting a `.base` file or a fence updates exactly the affected rows. **New census `census_bases_scan_incremental`** (verified: no general incremental-scan census exists to extend): random sequences of create/edit/rename/delete over `.base` files and fenced notes ⇒ `bases_files` + `bases_blocks` rows identical to a from-scratch full rescan.
4. Vault generation: any `bases_files` or source-file change bumps the generation the N1-2 cache keys on (one counter, session-owned — pinned here so cache invalidation has a single authority).

### Tests (PR 4)

Unit per rule; `census_bases_scan_incremental` at 10k-file scale; scan-bench diff proving decision-16's "no first-open regression" (§N-E).

- [ ] Migration 021 (both tables) + scanner + fence discovery per rules 1–4
- [ ] `census_bases_scan_incremental` + scan-bench diff
- [ ] fmt/clippy clean

## N0-5 · Dataview DQL parser → `SlateQuery` (#713) — PR 5

In N scope by owner decision 2026-07-06 (program decision 2, amended — reverses the draft deferral). **Parsed, not authored** (05 §8.1): Slate reads and converts DQL; it never writes it. New module `crates/slate-core/src/bases/dql.rs`: `pub fn parse_dql(source: &str) -> (SlateQuery, Vec<DqlWarning>)`. Grammar facts: brief §8, normative.

### Normative rules

1. **Total function:** every input yields a renderable-or-fail-loud `SlateQuery` — parse problems become `Expr::Unsupported` nodes (decision 6 discipline), never a hard error. `DqlWarning` names each conversion loss with its source span.
2. **Query types** (brief §8.1): `TABLE` ⇒ `ViewSpec::Table` with column exprs as formulas + `order` (an `AS "alias"` becomes the column `displayName`; `WITHOUT ID` omits the synthesized file-link primary column, otherwise one is prepended); `LIST` ⇒ `ViewSpec::List` (the optional single expr = secondary property; `WITHOUT ID` drops the primary link); `TASK` ⇒ `ViewSource::Tasks` + `ViewSpec::List` (N1-4 — task fields map per rule 6); `CALENDAR` ⇒ `Unsupported` (no calendar view in v1; fail-loud names it).
3. **FROM sources** (brief §8.2): `#tag` ⇒ `file.hasTag("tag")` (subtag semantics already match Slate's shipped tag matching — search_db.rs:60); `"folder"` ⇒ `file.inFolder("folder")` (recursive matches); `"path/to/file"` ⇒ path-equality filter (folder-wins tie rule honored: emit the folder form unless the source ends in `.md`); `[[note]]` ⇒ `file.hasLink("note")`; `outgoing([[note]])` ⇒ `QuerySource::Linked { from_path, depth: 1 }` internally, **serializing to the filter form `link("note").linksTo(file.file)`** on save-as-`.base` (brief §3's documented `linksTo`; this is also the builder's Linked-source serialization — N4-1 rule 1, gap G6); `[[]]`/`[[#]]` ⇒ the same forms over `this`. Combinators `and`/`or` (case-insensitive) + parentheses ⇒ `FilterNode` nesting; **both** negation spellings `-`/`!` ⇒ `Not` (brief §8.2's dual documentation).
4. **Data commands** (brief §8.2): repeated `WHERE` ⇒ ANDed filters; `SORT` (all four direction spellings) ⇒ sort keys; `GROUP BY` ⇒ **always `Unsupported { reason: "rows aggregation" }`** — DQL grouping yields *one row per key* with a `rows` array, Slate `group_by` keeps every row in labeled sections; even the "simple" case silently changes row membership, so none of it maps (decision 6; the user re-groups in the grid or builder); `FLATTEN` ⇒ `Unsupported` (v1); `LIMIT` ⇒ `limit`. **Pipeline-order guard (precise predicate):** the written command sequence, ignoring `FROM`, must match `WHERE* SORT? LIMIT?` (each present at most once except `WHERE`, in exactly that relative order — repeated `WHERE`s are order-free among themselves). Anything else — `LIMIT` before `SORT`, repeated `SORT`, any `GROUP BY`/`FLATTEN` — is order-dependent in DQL's written-order execution model and ⇒ `Unsupported { reason: "order-dependent commands" }` (decision-6 over convenience).
5. **Expressions** (brief §8.3): `=` ⇒ `==`; `!=`, comparisons verbatim; infix `AND`/`OR` ⇒ `&&`/`||`; prefix `!` verbatim; string `a * n` ⇒ `repeat(n)`; date shorthands `date(today|now|…)` ⇒ `today()`/`now()`/date arithmetic equivalents (sow/eow/som/eom/soy/eoy compile to arithmetic over `today()`); `dur(…)` unit aliases normalize onto the Bases duration-string tokens; lambdas `(x) => e` ⇒ the implicit-`value` list-expr form where the target is `map`/`filter` (single-arg lambda over the element ⇒ body rewritten with `value`); multi-arg lambdas and `minby`/`maxby`/predicate-form `all`/`any`/`none` ⇒ `Unsupported`. **Null-comparison delta pinned:** DQL's `null <= date(today)` is documented-true; the Bases evaluator type-errors it (fail-loud). Converted queries that relied on that quirk fail loud rather than silently changing membership — recorded in the help migration page (N4-5) with the documented DQL-side guard (`typeof`) as the fix.
6. **Implicit fields** (brief §8.4), three-column mapping table pinned in the module (DQL → target → status): `file.name/path/folder/ext/size/ctime/mtime/tags/aliases` ⇒ same-named Bases fields; `file.cday/mday` ⇒ `file.ctime.date()` / `file.mtime.date()`; `file.link` ⇒ `link(file.path)`; `file.inlinks` ⇒ `file.backlinks`; `file.outlinks` ⇒ `file.links`; `file.etags/lists/frontmatter/day/starred` ⇒ `Unsupported`. Task fields: `text` ⇒ `task.text`; `status` ⇒ `task.status`; `completed` ⇒ `task.status == "x"` (**exact lowercase**, Dataview parity — brief §8.4: DQL `completed` is `"x"` only, while Slate's own `task.completed` derives from `{'x','X'}` per migration 008; mapping to `task.completed` would silently flip `- [X]` rows — gap O12); `checked` ⇒ `task.status != " "`; `due` ⇒ `task.due`; `scheduled` ⇒ `task.scheduled`; `created/completion/start/fullyCompleted/children/section/…` ⇒ `Unsupported` (the tasks table has no such columns — deliberate, gap analysis). `this.` ⇒ `this` (decision 20 contexts apply).
7. **Functions** (brief §8.5), same three-column table: direct maps (`contains`, `lower`, `replace` literal-all, `join`, `length` ⇒ `.length`, `sort/reverse/unique/flat/slice/filter/map`, `sum/average/min/max` where list-shaped, `startswith/endswith`, `round/trunc(⇒floor toward zero)/floor/ceil`, `regextest/regexmatch` ⇒ regex `.matches` forms, `regexreplace`, `split`, `substring` ⇒ `slice`, `striptime` ⇒ `.date()`, `choice` ⇒ `if`, `default` ⇒ `if(x.isEmpty(), v, x)`, `typeof` ⇒ `isType` rewrites in boolean position, `number/string/date/dur/link/object/list/embed⇒link`); everything else (`upper`, `truncate`, `padleft/right`, `containsword/econtains/icontains`, `dateformat/durationformat/currencyformat/localtime`, `hash`, `meta`, `minby/maxby`, `product`, `reduce`, `extract`, `firstvalue`, `nonnull`, `display`, `elink`) ⇒ `Unsupported`. Additions to the evaluated set are individually-tracked issues (decision 5's stance), and the table is the traceable backlog.
8. Inline `= expr` queries: out of scope (reserved N-E1 remainder; brief §8.6). Only ` ```dataview ` **block** queries convert.
9. Determinism: pure function; equal input ⇒ equal output.

### Tests (PR 5)

- Golden corpus `tests/fixtures/dql/`: every brief-§8 documented example verbatim + the field-report migration idioms (Felker's status/schedule tables, reading-duration math) as DQL, each with an expected-`SlateQuery` golden or expected-`Unsupported` reason.
- Unit per rule incl. the pipeline-order guard both ways, both negation spellings, `WITHOUT ID` variants, TASK ⇒ tasks-source mapping.
- Property: never panics on arbitrary text; determinism; converted queries execute under the N1 engine without panicking (integration once N1 lands — wired into the N2-1 census run).

- [ ] `parse_dql` per rules 1–9; mapping tables in module docs
- [ ] Golden corpus + unit + property tests
- [ ] fmt/clippy; host-independent; no I/O

**Wave-1 exit:** round-trip census clean (§N-A), corpus in CI (Bases + DQL), scanner benches inside budget, no save-path deltas.
