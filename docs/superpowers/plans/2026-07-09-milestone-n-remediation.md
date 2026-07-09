# Milestone N Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close every automatable Milestone N code, regression-test, and performance-evidence gap identified by the 2026-07-09 adversarial audit while leaving the human VoiceOver checklist honestly open.

**Architecture:** Repair the Rust format/evaluation/session contracts first so every caller receives lossless, fresh, typed results. Then make transient Swift state a projection of that authoritative session state, complete the builder/dashboard accessibility contracts, and finish with generated censuses plus benchmark evidence. Each task owns a narrow subsystem and an atomic finding-ID commit.

**Tech Stack:** Rust 2024, serde/serde_yaml, rusqlite, proptest, UniFFI, Swift 6, SwiftUI/AppKit, XCTest, Criterion.

## Global Constraints

- Unknown `.base` keys and untouched source regions remain byte-identical; no successful edit may emit YAML that fails `parse_base`.
- Supported DQL conversions preserve row membership; unsupported conversions fail loudly instead of evaluating to `Null` silently.
- Quick filter stays engine-side, case/diacritic insensitive, 150 ms debounced, uncached, unpersisted, and summary-honest.
- In-app writes re-execute visible Bases surfaces while preserving selection by `(path, task_ordinal)` and only announcing membership changes.
- Property writes remain atomic through `set_property`/`delete_property`; clearing means deletion, never an empty surrogate value.
- Transient sort is session-only until Save Sort to View; table, list, and export observe the same typed ordering.
- Swift UI changes keep `a11y-check` at 100/100 and add no global keyboard chord.
- Performance gates are p50: query < 50 ms at 10k and < 200 ms at 50k, cache hit < 2 ms, parse/serialize < 5 ms, cancellation < 100 ms at 10k, and scan regression no worse than 5%.
- Do not mark human AT PASS; `at_smoke_checklist.md` remains open until a human executes it.
- Do not stage or modify pre-existing Graphify output, `.agents`, `.github/skills`, `.graphify_*`, or demo-vault pizza-toppings changes.

---

### Task 1: Lossless and structurally valid `.base` edits (N0-01, N0-02)

**Files:**
- Modify: `crates/slate-core/src/bases/mod.rs`
- Modify: `crates/slate-core/tests/bases_serialize.rs`
- Create: `crates/slate-core/tests/fixtures/bases/edit_adversarial.base`

**Interfaces:**
- Consumes: `parse_base(&str) -> (BaseFile, Vec<ParseWarning>)` and `apply_edit(&str, &BaseFile, BaseEdit) -> Result<String, BaseEditError>`.
- Produces: structural-region discovery independent of two-space indentation; `replace_scalar_preserving_style`; block replacement for flow collections.

- [ ] **Step 1: Add failing edit regressions**

```rust
#[test]
fn edits_indented_and_flow_collections_without_invalid_yaml() {
    let four_space = "views:\n    - type: table\n      name: 'Old' # keep\n";
    let renamed = edit(four_space, BaseEdit::RenameView { view: 0, name: "New".into() });
    assert!(parse_base(&renamed).1.iter().all(|w| w.kind != ParseWarningKind::ParseFailed));
    assert!(renamed.contains("name: 'New' # keep"));

    let formulas = edit("formulas: {}\nviews: []\n", BaseEdit::SetFormula {
        name: "score".into(), expression: "1 + 1".into(),
    });
    assert_eq!(parse_base(&formulas).0.formulas.len(), 1);
    let with_view = edit(&formulas, BaseEdit::AddView { name: "Main".into(), view_type: "table".into() });
    assert_eq!(parse_base(&with_view).0.views.len(), 1);
}

#[test]
fn scalar_splice_preserves_comment_quote_and_final_newline() {
    let source = "views:\n  - type: table # keep-inline\n    name: 'Old'";
    let changed = edit(source, BaseEdit::RenameView { view: 0, name: "New".into() });
    assert!(changed.contains("name: 'New'"));
    assert!(changed.contains("# keep-inline"));
    assert!(!changed.ends_with('\n'));
}
```

- [ ] **Step 2: Run the regressions and verify failure**

Run: `cargo test -p slate-core --test bases_serialize -- --nocapture`

Expected: failures showing `MissingSpan`, `ParseFailed`, lost comment/quote, or added newline.

- [ ] **Step 3: Replace indentation heuristics with parsed structural ranges**

Use the YAML event/marker offsets already collected by `parse_base` to populate the
top-level and per-view `PreservedRegion` values. A collection whose source value is
`{}` or `[]` must be replaced as a whole:

```rust
fn expand_empty_collection(key_indent: &str, key: &str, child: &str) -> String {
    format!("{key_indent}{key}:\n{key_indent}  {child}")
}
```

Do not append a block child after a flow scalar.

- [ ] **Step 4: Preserve scalar lexical style**

```rust
fn replacement_scalar(existing: &str, value: &str) -> String {
    let (body, comment) = split_inline_comment(existing);
    let rendered = match body.trim().chars().next() {
        Some('\'') => quote_single_yaml(value),
        Some('"') => quote_double_yaml(value),
        _ => yaml_scalar(value),
    };
    format!("{rendered}{comment}")
}
```

Splice exactly the scalar token range; retain the source's `\n`/`\r\n` and final
newline state.

- [ ] **Step 5: Run format-layer coverage**

Run: `cargo test -p slate-core --test bases_parse --test bases_serialize`

Expected: all tests pass, including the new adversarial fixture.

- [ ] **Step 6: Commit**

```bash
git add crates/slate-core/src/bases/mod.rs crates/slate-core/tests/bases_serialize.rs crates/slate-core/tests/fixtures/bases/edit_adversarial.base
git commit -m "fix(bases): preserve valid YAML edits [N0-01 N0-02]"
```

### Task 2: DQL membership fidelity and verbatim fence indexing (N0-03..N0-06)

**Files:**
- Modify: `crates/slate-core/src/bases/dql.rs`
- Modify: `crates/slate-core/src/bases/eval.rs`
- Modify: `crates/slate-core/src/bases/engine.rs`
- Modify: `crates/slate-core/src/code.rs`
- Modify: `crates/slate-core/src/bases_db.rs`
- Modify: `crates/slate-core/tests/bases_dql.rs`
- Modify: `crates/slate-core/src/session/tests/bases.rs`
- Modify: `crates/slate-core/src/session/tests/scan.rs`
- Create: `crates/slate-core/tests/fixtures/dql/outgoing.dql`
- Create: `crates/slate-core/tests/fixtures/dql/functions.dql`

**Interfaces:**
- Consumes: `parse_dql`, `dql_as_base`, `execute`, scanner code-block offsets, vault link resolution.
- Produces: dynamic `this` source representation, regex literals for `regextest`, true truncation, byte-sliced fence bodies.

- [ ] **Step 1: Add failing DQL and CRLF tests**

```rust
#[test]
fn dql_outgoing_regex_and_trunc_preserve_semantics() {
    assert_rows("LIST\nFROM outgoing([[Hub]])", &["Target.md"]);
    assert_rows_with_this("LIST\nFROM outgoing([[]])", "Hub.md", &["Target.md"]);
    assert_eq!(eval_dql_expr("regextest(\"^foo\", \"foobar\")"), Value::Bool(true));
    assert_eq!(eval_dql_expr("trunc(-1.2)"), Value::Number(-1.0));
}

#[test]
fn indexed_base_fence_body_is_original_bytes() {
    let markdown = "```base\r\nviews: []\r\n```\r\n";
    assert_eq!(indexed_fence(markdown).source_text, "views: []\r\n");
}
```

- [ ] **Step 2: Verify the tests fail**

Run: `cargo test -p slate-core --test bases_dql dql_outgoing_regex_and_trunc_preserve_semantics -- --nocapture && cargo test -p slate-core --lib indexed_base_fence_body_is_original_bytes -- --nocapture`

Expected: outgoing rows absent, regex `Null`, trunc `-2`, and LF-normalized source.

- [ ] **Step 3: Preserve DQL source meaning**

Represent `[[]]` as the existing `Expr::This`/link expression rather than the
literal string `"this"`. Resolve explicit extensionless wikilinks with the same
vault resolver used by `link()` before building the SQL source. Translate a
literal regex pattern to `Expr::Literal(Literal::Regex { pattern, flags: "" })`.
Add `MethodName::Trunc` and evaluate numbers with `value.trunc()`; do not reuse
`Floor`.

- [ ] **Step 4: Slice original fence bytes**

Keep Markdown parsing for the opening/closing fence offsets, then derive the body
from the original source:

```rust
let source_text = source[body_start..body_end].to_string();
```

Do not reconstruct the body from normalized parser events.

- [ ] **Step 5: Run DQL/scanner suites**

Run: `cargo test -p slate-core --test bases_dql && cargo test -p slate-core --lib census_bases_scan_incremental`

Expected: all tests pass and unsupported conversions still fail loudly.

- [ ] **Step 6: Commit**

```bash
git add crates/slate-core/src/bases/dql.rs crates/slate-core/src/bases/eval.rs crates/slate-core/src/bases/engine.rs crates/slate-core/src/code.rs crates/slate-core/src/bases_db.rs crates/slate-core/tests/bases_dql.rs crates/slate-core/src/session/tests/bases.rs crates/slate-core/src/session/tests/scan.rs crates/slate-core/tests/fixtures/dql
git commit -m "fix(bases): preserve DQL and fence semantics [N0-03 N0-04 N0-05 N0-06]"
```

### Task 3: Complete pinned evaluator semantics (N1-02, N1-03)

**Files:**
- Modify: `crates/slate-core/src/bases/eval.rs`
- Modify: `crates/slate-core/src/bases/expr.rs`
- Modify: `crates/slate-core/tests/bases_eval.rs`

**Interfaces:**
- Consumes: parsed `Expr`, `Value`, and evaluator arity helpers.
- Produces: official v1 semantics for optional `if`, unary list normalization,
  variadic contains methods, split separator/limit, datetime parsing, mixed duration
  application, and strict render/file/list arities.

- [ ] **Step 1: Add a failing compatibility table**

```rust
#[test]
fn pinned_function_edges_match_the_v1_table() {
    assert_eq!(value("if(true, 7)"), Value::Number(7.0));
    assert_eq!(value("if(false, 7)"), Value::Null);
    assert_eq!(value("list([1, 2])"), list![1.0, 2.0]);
    assert_error("list(1, 2)", "list expected 1, got 2");
    assert_eq!(value("\"abc\".containsAll(\"a\", \"c\")"), Value::Bool(true));
    assert_eq!(value("\"a,b,c\".split(\",\", 2)"), list!["a", "b,c"]);
    assert_eq!(value("\"a, b,c\".split(/,\\s*/)"), list!["a", "b", "c"]);
    assert_date("date(\"2026-07-08 15:04:05\")", "2026-07-08T15:04:05");
    assert_date("date(\"2026-01-31\") + \"1M 1d\"", "2026-03-01");
    assert_error("html(\"a\", \"b\")", "html expected 1, got 2");
}
```

- [ ] **Step 2: Verify the table fails**

Run: `cargo test -p slate-core --test bases_eval pinned_function_edges_match_the_v1_table -- --nocapture`

Expected: failures for each audited edge.

- [ ] **Step 3: Implement arity and normalization fixes**

```rust
fn eval_if(args: &[Expr], ctx: &EvalCtx<'_>) -> Result<Value, EvalError> {
    expect_arity("if", args.len(), 2, 3)?;
    if truthy(&eval(&args[0], ctx)?) {
        eval(&args[1], ctx)
    } else if let Some(otherwise) = args.get(2) {
        eval(otherwise, ctx)
    } else {
        Ok(Value::Null)
    }
}

fn normalize_list(mut values: Vec<Value>) -> Result<Value, EvalError> {
    expect_arity("list", values.len(), 1, 1)?;
    Ok(match values.pop().unwrap() {
        value @ Value::List(_) => value,
        value => Value::List(vec![value]),
    })
}
```

Use `1..=usize::MAX` for variadic contains methods, exact unary arity for
`file/html/image/icon/join`, zero arity for `flat`, and return a string from
`date.time()`.

- [ ] **Step 4: Implement split/date/duration behavior**

Split accepts one or two arguments. Use `Regex::splitn` for `Value::Regex` and
`str::splitn` for text separators. Parse `%Y-%m-%d %H:%M:%S` in addition to the
existing ISO forms. Preserve parsed calendar months separately from fixed
milliseconds and apply months (with end-of-month clamp) before days/time.

- [ ] **Step 5: Run evaluator and engine suites**

Run: `cargo test -p slate-core --test bases_eval --test bases_engine`

Expected: all tests pass; no previous pinned function regresses.

- [ ] **Step 6: Commit**

```bash
git add crates/slate-core/src/bases/eval.rs crates/slate-core/src/bases/expr.rs crates/slate-core/tests/bases_eval.rs
git commit -m "fix(bases): complete evaluator compatibility [N1-02 N1-03]"
```

### Task 4: Fresh, complete, and typed engine/session results (N1-01, N1-04, N2-01, N2-02)

**Files:**
- Modify: `crates/slate-core/src/bases/engine.rs`
- Modify: `crates/slate-core/src/session.rs`
- Modify: `crates/slate-core/tests/bases_engine.rs`
- Modify: `crates/slate-core/src/session/tests/bases.rs`

**Interfaces:**
- Consumes: `query_mentions_global`, row assembly, `BasesResultSet` conversion, query-as-base serialization.
- Produces: conservative temporal cache predicate, aliases/embeds file fields,
  first-non-null column-kind inference, inclusive Recent export.

- [ ] **Step 1: Add failing engine/session regressions**

```rust
#[test]
fn temporal_sources_and_custom_summaries_never_cache() {
    assert_cutoff_changes(QuerySource::Recent { days: 1 });
    assert_summary_changes("now()", 100, 200);
}

#[test]
fn aliases_embeds_and_nullable_column_kind_are_complete() {
    let result = execute_fixture("aliases-embeds");
    assert_display(&result, "file.aliases", "A, Alpha alias");
    assert_display(&result, "file.embeds", "Notes/Target.md");
    assert_eq!(ffi_kind(&[Value::Null, Value::Null, Value::Number(3.0)]), "number");
}

#[test]
fn recent_export_keeps_inclusive_cutoff() {
    assert!(query_as_base(recent_query()).contains("file.mtime >="));
}
```

- [ ] **Step 2: Verify the regressions fail**

Run: `cargo test -p slate-core --test bases_engine temporal_sources_and_custom_summaries_never_cache -- --nocapture && cargo test -p slate-core --lib session::tests::bases -- --nocapture`

Expected: stale cache hit, empty aliases/embeds, `null` kind, and exclusive cutoff.

- [ ] **Step 3: Make the cache predicate conservative**

Return `true` from `query_mentions_global` for `QuerySource::Recent`; visit every
custom-summary expression in addition to filters/formulas/sort/columns. Retain
the existing no-cache behavior for quick filter.

- [ ] **Step 4: Populate file fields and infer kind safely**

Load `aliases` from the indexed aliases property. Partition outgoing link rows by
`is_embed`, keeping links and embeds separately. Change `column_value_kind` to
skip `Null` and error cells and return the first stable concrete kind (or `null`
only when every row is null/error).

- [ ] **Step 5: Align durable Recent export**

Emit `file.mtime >= now() - duration("Nd")` and add an exact-cutoff execute →
export → reopen membership assertion.

- [ ] **Step 6: Run engine/session coverage**

Run: `cargo test -p slate-core --test bases_engine && cargo test -p slate-core --lib session::tests::bases`

Expected: all tests pass.

- [ ] **Step 7: Commit**

```bash
git add crates/slate-core/src/bases/engine.rs crates/slate-core/src/session.rs crates/slate-core/tests/bases_engine.rs crates/slate-core/src/session/tests/bases.rs
git commit -m "fix(bases): keep session results fresh and typed [N1-01 N1-04 N2-01 N2-02]"
```

### Task 5: Authoritative quick-filter counts and transient typed sort (N3-01, N3-04)

**Files:**
- Modify: `crates/slate-core/src/bases/engine.rs`
- Modify: `crates/slate-core/src/session.rs`
- Modify: `crates/slate-core/tests/bases_engine.rs`
- Modify: `crates/slate-core/src/session/tests/bases.rs`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/BaseDocument.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/BaseEmbedDocument.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/BaseContainerView.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/BaseEmbedView.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/DashboardViews.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/AppState+Bases.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/BasesTabRoutingTests.swift`
- Modify generated UniFFI bindings with: `make regenerate-bindings`

**Interfaces:**
- Consumes: `base_execute`, `base_export`, open-handle state, `DataGridSortState`.
- Produces: `base_set_transient_sort(handle:view:column_id:ascending:)`; one engine-sorted result used by table/list/export; unfiltered quick-filter count preserved in the result contract.

- [ ] **Step 1: Add failing count and ordering tests**

```swift
func testQuickFilterReportsOneOfThreeAndExportChoicesMatch() throws {
    let doc = threeRowDocument()
    XCTAssertEqual(doc.applyQuickFilter("cafe", session: session), "1 of 3 results")
    XCTAssertEqual(try doc.export(format: .csv, session: session, includeQuickFilter: true).dataRows, 1)
    XCTAssertEqual(try doc.export(format: .csv, session: session, includeQuickFilter: false).dataRows, 3)
}

func testTransientNumericSortSurvivesListAndExport() throws {
    doc.setTransientSort(columnIndex: numberColumn, ascending: true, session: session)
    XCTAssertEqual(doc.result!.rows.map(number), [2, 10])
    XCTAssertEqual(try doc.export(format: .csv, session: session).numbers, [2, 10])
}
```

- [ ] **Step 2: Verify failures in Rust and Swift**

Run: `cargo test -p slate-core --lib session::tests::bases::quick_filter -- --nocapture`

Run: `cd apps/slate-mac && swift test --filter BasesTabRoutingTests`

Expected: `1 of 1`, string order `[10,2]`, and unsorted export.

- [ ] **Step 3: Preserve the unfiltered count during execution**

Capture the unfiltered cardinality before `apply_quick_filter`; keep filtered rows
for summaries/audio/groups. Expose the unfiltered view cardinality used by the N/M
announcement without changing filtered summary semantics. Tests must cover a view
limit so the dialog's M equals what the unfiltered export actually emits.

- [ ] **Step 4: Add handle-scoped transient sort**

Store an optional per-view sort override in `OpenBaseState`. The new session method
resolves a selected column ID to the same expression/value ordering used by
`sort_rows`, replaces the query sort only for execution/export, and clears the
override on view/handle close. `base_export` must execute through that override.

- [ ] **Step 5: Bind Swift table headers to session state**

`BaseDocument.setTransientSort` calls the new session method and re-executes.
`BaseContainerView` uses `BasesValue` kind-aware comparison only for the immediate
AppKit projection; list and export consume the re-executed engine ordering. Saving
the sort persists it and clears the transient override.

- [ ] **Step 6: Regenerate bindings and run coverage**

Run: `make regenerate-bindings`

Run: `cargo test -p slate-core --test bases_engine && cargo test -p slate-core --lib session::tests::bases`

Run: `cd apps/slate-mac && swift test --filter BasesTabRoutingTests --filter BaseEmbedTests`

Expected: all tests pass and generated bindings are current.

- [ ] **Step 7: Commit**

```bash
git add crates/slate-core apps/slate-mac/Sources/SlateMac/Bases apps/slate-mac/Tests/SlateMacTests/BasesTabRoutingTests.swift apps/slate-mac/Tests/SlateMacTests/BaseEmbedTests.swift
git commit -m "fix(bases): unify filtered counts and transient sort [N3-01 N3-04]"
```

### Task 6: Safe cell editing, viewport paging, and AX grid semantics (N3-02, N3-03, N3-05, N3-06)

**Files:**
- Modify: `apps/slate-mac/Sources/SlateMac/AccessibleDataGrid.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/BaseContainerView.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/BaseListRenderer.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/AccessibleDataGridTests.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/BasesTabRoutingTests.swift`

**Interfaces:**
- Consumes: AppKit table selection callbacks and `basesDeleteProperty`.
- Produces: synchronized row/cell identity; viewport-derived paging; heading/header
  AX grammar; clear-cell deletion.

- [ ] **Step 1: Add failing AppKit and property-write tests**

```swift
func testNativeRowChangeRetargetsCellBeforeReturn() {
    coordinator.selectCell(row: 0, column: 1)
    coordinator.tableView.selectRowIndexes(IndexSet(integer: 1), byExtendingSelection: false)
    coordinator.handleReturn()
    XCTAssertEqual(editedRowID, rows[1].id)
}

func testPageDownMovesOneVisibleViewport() {
    coordinator.setVisibleRows(4...6)
    coordinator.selectCell(row: 4, column: 0)
    coordinator.moveCell(.pageDown)
    XCTAssertEqual(selectedRow, 7)
}

func testBlankCommitDeletesProperty() async {
    await commitEditableCell("   ")
    XCTAssertNil(try session.property(path: "Alpha.md", key: "score"))
}
```

- [ ] **Step 2: Verify failures**

Run: `cd apps/slate-mac && swift test --filter AccessibleDataGridTests --filter BasesTabRoutingTests`

Expected: stale row edit, ten-row page, and empty surrogate value/failure.

- [ ] **Step 3: Synchronize selection and delete blanks**

In `tableViewSelectionDidChange`, map the native row entry back to the selected
row and retain the current column index (clamped), then publish both bindings.
In `commitEdit`, trim only to detect clearing; route a blank draft to
`basesDeleteProperty`, while nonblank text continues through the typed converter.

- [ ] **Step 4: Use the visible viewport for paging**

Compute the visible data-row count from `tableView.rows(in: tableView.visibleRect)`;
move by `max(visibleDataRows, 1)` while skipping inserted group rows.

- [ ] **Step 5: Expose pinned accessibility grammar**

Group cells and list section headers expose heading role/trait. Sortable headers
publish `Column: <label>, sortable, current sort: <asc/desc/none>` and update their
sort direction after every reload. Grid entry uses the engine `audio_summary`, not
the generic “Base table” label.

- [ ] **Step 6: Run Swift and static accessibility coverage**

Run: `cd apps/slate-mac && swift test --filter AccessibleDataGridTests --filter BasesTabRoutingTests`

Run: `a11y-check apps/slate-mac/Sources/SlateMac`

Expected: tests pass; a11y score remains 100.0/100.

- [ ] **Step 7: Commit**

```bash
git add apps/slate-mac/Sources/SlateMac/AccessibleDataGrid.swift apps/slate-mac/Sources/SlateMac/Bases/BaseContainerView.swift apps/slate-mac/Sources/SlateMac/Bases/BaseListRenderer.swift apps/slate-mac/Tests/SlateMacTests/AccessibleDataGridTests.swift apps/slate-mac/Tests/SlateMacTests/BasesTabRoutingTests.swift
git commit -m "fix(bases): make grid editing and navigation safe [N3-02 N3-03 N3-05 N3-06]"
```

### Task 7: Refresh every visible Bases surface after in-app note writes (N3-07)

**Files:**
- Modify: `apps/slate-mac/Sources/SlateMac/AppState.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/AppState+Bases.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/DashboardDocument.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/BasesTabRoutingTests.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/RightPaneViewTests.swift`

**Interfaces:**
- Consumes: successful `performSave`, open document registries, dashboard/dock refresh methods.
- Produces: `refreshVisibleBasesAfterInAppWrite(session:changedPath:)` with membership-aware announcements.

- [ ] **Step 1: Add a failing save-to-live-view test**

```swift
func testSavingNoteRefreshesOpenBaseDashboardAndDock() async throws {
    let state = try makeStateWithOpenBasesSurfaces()
    state.currentNoteText = bodyThatLeavesOneQueryAndEntersAnother
    await state.saveCurrentNote()!.value
    XCTAssertEqual(state.openBaseRows, expectedRows)
    XCTAssertEqual(state.dashboardRows, expectedRows)
    XCTAssertEqual(state.dockRows, expectedRows)
    XCTAssertEqual(state.membershipAnnouncements.count, 1)
}
```

- [ ] **Step 2: Verify failure**

Run: `cd apps/slate-mac && swift test --filter BasesTabRoutingTests/testSavingNoteRefreshesOpenBaseDashboardAndDock`

Expected: all three surfaces retain pre-save rows.

- [ ] **Step 3: Add the shared post-write refresh**

Capture each document's row-identity multiset, re-execute open base documents,
dashboard sections, and the dock against the same live session, restore selections,
and announce `Updated: <audio_summary>` only where membership changed. Guard
publication with `currentSession === session` and current document identity.

- [ ] **Step 4: Call it only after successful in-app writes**

Invoke the helper in `performSave` after the session save succeeds, and reuse it
from property writes/base edits where those paths do not already refresh the same
surface. Do not add a filesystem watcher.

- [ ] **Step 5: Run focused Swift coverage**

Run: `cd apps/slate-mac && swift test --filter BasesTabRoutingTests --filter RightPaneViewTests`

Expected: all tests pass with no duplicate announcement.

- [ ] **Step 6: Commit**

```bash
git add apps/slate-mac/Sources/SlateMac/AppState.swift apps/slate-mac/Sources/SlateMac/Bases/AppState+Bases.swift apps/slate-mac/Sources/SlateMac/Bases/DashboardDocument.swift apps/slate-mac/Tests/SlateMacTests/BasesTabRoutingTests.swift apps/slate-mac/Tests/SlateMacTests/RightPaneViewTests.swift
git commit -m "fix(bases): refresh live views after note saves [N3-07]"
```

### Task 8: Lossless typed builder and actionable dashboards (N4-01..N4-04)

**Files:**
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/BaseQueryBuilderModel.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/BaseQueryBuilderSheet.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/AppState+Bases.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/DashboardDocument.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/DashboardViews.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/BaseQueryBuilderTests.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/RightPaneViewTests.swift`

**Interfaces:**
- Consumes: full `SlateQuery` JSON, property-key kind inventory, expression validator,
  dashboard CRUD, follow-active refresh.
- Produces: opaque facet preservation plus structured edits; per-kind operator/editor
  descriptors; function completion; missing-section actions; membership-set comparison.

- [ ] **Step 1: Add failing builder/dashboard tests**

```swift
func testBuilderRoundTripPreservesOpaqueQueryFacets() throws {
    let original = queryJSON(limit: 25, summaries: [.count], custom: ["ratio": expression])
    let draft = try BaseQueryBuilderDraft(queryJSON: original)
    XCTAssertJSONEqual(try draft.queryJSON(), original)
}

func testTypedBuilderAndDashboardActions() {
    XCTAssertFalse(BaseQueryOperator.options(for: .bool).contains(.contains))
    XCTAssertEqual(BaseQueryEditorDescriptor.for(.date), .dateAndRelative)
    XCTAssertTrue(BaseFormulaCompletion.names.contains("if"))
    XCTAssertEqual(missingSection.actions, ["Remove section", "Pick replacement"])
}

func testFollowActiveAnnouncesEmptyToNonemptyButNotReorder() {
    XCTAssertTrue(change(old: [], new: ["A"]).shouldAnnounce)
    XCTAssertFalse(change(old: ["A", "B"], new: ["B", "A"]).shouldAnnounce)
}
```

- [ ] **Step 2: Verify failures**

Run: `cd apps/slate-mac && swift test --filter BaseQueryBuilderTests --filter RightPaneViewTests`

Expected: lost facets, universal operators/text editor, absent actions, and suppressed/extra announcements.

- [ ] **Step 3: Preserve the complete query**

Decode and store `limit`, `summaries`, `custom_summaries`, and an opaque root map.
When encoding, start from the opaque root and replace only builder-owned keys.
Keep two explicit products: effective full-query JSON for preview/Save-as/saved-query,
and view-local `BaseEdit` splices for Save to View. The Edit View Filters entry point
must retain the global filter in the effective query without writing it into the
view-local filter block.

- [ ] **Step 4: Add typed builder descriptors**

Carry the indexed property kind beside each property choice. Filter the operator
menu by kind; use checkbox, number, date/list, and text controls from the shipped
property editor family. A relative-date control emits `now() - duration("Nd")`.
Add the pinned function names as completion suggestions while retaining Rust live
validation as authority.

- [ ] **Step 5: Make missing dashboard sections actionable**

Pass Remove/Pick Replacement closures into `DashboardSectionView`; render keyboard
buttons and equivalent AX custom actions. Persist removal/replacement through the
existing dashboard update API.

- [ ] **Step 6: Fix follow-active change detection**

Track `hasPublishedBaseline` independently from membership. Compare multisets of
stable row identities, not ordered arrays, so empty→nonempty announces and reorder
alone does not.

- [ ] **Step 7: Run Swift and accessibility coverage**

Run: `cd apps/slate-mac && swift test --filter BaseQueryBuilderTests --filter RightPaneViewTests --filter BaseQueriesPanelTests`

Run: `a11y-check apps/slate-mac/Sources/SlateMac`

Expected: all tests pass; a11y remains 100.0/100.

- [ ] **Step 8: Commit**

```bash
git add apps/slate-mac/Sources/SlateMac/Bases apps/slate-mac/Tests/SlateMacTests/BaseQueryBuilderTests.swift apps/slate-mac/Tests/SlateMacTests/RightPaneViewTests.swift apps/slate-mac/Tests/SlateMacTests/BaseQueriesPanelTests.swift
git commit -m "fix(bases): complete builder and dashboard contracts [N4-01 N4-02 N4-03 N4-04]"
```

### Task 9: Replace smoke gates with generated censuses and close performance evidence (N-VER-01, N-VER-02)

**Files:**
- Modify: `crates/slate-core/tests/bases_serialize.rs`
- Modify: `crates/slate-core/tests/bases_dql.rs`
- Modify: `crates/slate-core/tests/bases_engine.rs`
- Modify: `crates/slate-core/src/session/tests/bases.rs`
- Modify: `crates/slate-core/src/session/tests/scan.rs`
- Modify: `crates/slate-core/benches/bases_bench.rs`
- Modify: `crates/slate-core/benches/scan_bench.rs`
- Modify: `BENCHMARKS.md`
- Modify: `docs/plans/17_bases/00_program.md`
- Modify: `docs/plans/17_bases/specs/gap_analysis.md`
- Modify: `docs/plans/17_bases/milestone_n_audit_2026-07-09.md`

**Interfaces:**
- Consumes: fixed contracts from Tasks 1–8 and `SLATE_CENSUS_FULL` scale switch.
- Produces: deterministic generated gates, real under-load cancellation, Criterion
  p50 records, scan regression evidence, finding-status closeout.

- [ ] **Step 1: Expand the automated gates before changing their labels**

Add proptests/generated loops that assert:

```rust
proptest! {
    #![proptest_config(ProptestConfig::with_cases(128))]
    #[test]
    fn base_edits_always_reparse(case in base_document_strategy(), edit in base_edit_strategy()) {
        let (base, warnings) = parse_base(&case);
        prop_assume!(!has_parse_failure(&warnings));
        if let Ok(changed) = apply_edit(&case, &base, edit) {
            prop_assert!(!has_parse_failure(&parse_base(&changed).1));
        }
    }
}
```

Add a checked-in DQL golden corpus; generated pushdown/interpreter equivalence;
generated save/rename/delete/cache/fail-loud/read-only interleavings; a 10k scan
mutation census under `SLATE_CENSUS_FULL=1`; and deterministic cancellation that
signals after work begins and asserts completion within 100 ms.

- [ ] **Step 2: Run default and full census modes**

Run: `cargo test -p slate-core --test bases_expr --test bases_parse --test bases_serialize --test bases_dql --test bases_eval --test bases_engine`

Run: `SLATE_CENSUS_FULL=1 cargo test -p slate-core census_bases -- --nocapture --test-threads=1`

Expected: all gates pass; logs show full scale and under-load cancellation.

- [ ] **Step 3: Run fresh p50 benchmarks**

Run: `cargo bench -p slate-core --bench bases_bench -- --sample-size 20`

Run the Milestone N-relevant `scan_bench` cases on the same machine/sample size.
Read Criterion's `median/point_estimate` from each `estimates.json`; do not copy the
mean. Compare the scan p50 against the recorded pre-N baseline and calculate the
percentage delta.

- [ ] **Step 4: Update close-out evidence honestly**

Replace the mean table in `BENCHMARKS.md` with p50 values, date, commit, command,
machine context, and scan delta. Mark every audit finding `RESOLVED` only after its
test passes. Keep manual AT open and keep the program/GitHub milestone status
operationally open until a human result exists.

- [ ] **Step 5: Run final repository verification**

Run: `cargo fmt --check`

Run: `cargo clippy -p slate-core -p slate-cli --all-targets -- -D warnings`

Run: `cargo test -p slate-core -p slate-cli`

Run: `cd apps/slate-mac && swift test`

Run: `a11y-check apps/slate-mac/Sources/SlateMac`

Expected: every command passes; a11y is 100.0/100.

- [ ] **Step 6: Commit**

```bash
git add crates/slate-core BENCHMARKS.md docs/plans/17_bases/00_program.md docs/plans/17_bases/specs/gap_analysis.md docs/plans/17_bases/milestone_n_audit_2026-07-09.md
git commit -m "test(bases): enforce Milestone N closure gates [N-VER-01 N-VER-02]"
```

## Completion boundary

Tasks 1–9 close the automatable gaps. Completion does **not** authorize checking
boxes in `at_smoke_checklist.md`; the final handoff must name that human execution
as the sole remaining milestone-close action unless the user separately performs
and supplies the results.
