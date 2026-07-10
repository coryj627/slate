# Milestone N audit — 2026-07-09

Milestone N (Bases v1) had material gaps despite green focused suites at audit
start. Direct counterexamples and contract tracing found the issues below. The
automatable code, regression-test, and performance-evidence findings are now
remediated on `codex/milestone-n-gap-fixes`; operational closure remains blocked
only by the human VoiceOver checklist.

## Verification baseline

- Rust Bases parser/evaluator/engine/DQL/serializer integration suites: 103 passed.
- Rust session Bases suite: 19 passed.
- Rust CLI query suites: 14 passed.
- Focused Swift Bases suites: 93 passed.
- `SlateCommandsTests`: 38 passed.
- `a11y-check apps/slate-mac/Sources/SlateMac`: 100.0/100, zero findings.
- GitHub milestone 14 remains open with 23/23 issues closed; the written program
  intentionally keeps the milestone open until manual AT is recorded.

Green focused suites were not accepted as proof where the suite asserted the
defective behavior or omitted the required adversarial case.

## Confirmed findings

| ID | Severity | Gap | Reproduced evidence | Governing contract |
|---|---|---|---|---|
| N0-01 | High | Serializer edits can turn valid indented or flow-style YAML into invalid YAML. | Four-space view rename returns `MissingSpan`; adding a formula to `formulas: {}` and a view to `views: []` emits non-parsing YAML. | `specs/n0_spec.md` N0-2/N0-3 |
| N0-02 | High | Scalar edits discard inline comments, quote style, and original final-newline state. | Editing `type`, a single-quoted name, and a no-final-newline fixture changed all three. | `specs/n0_spec.md` N0-3 |
| N0-03 | High | DQL `outgoing([[Hub]])` and `outgoing([[]])` silently return no rows. | Executed vault probe resolved neither extensionless wikilink targets nor dynamic `this`. | `specs/n0_spec.md` N0-5 |
| N0-04 | High | DQL `regextest` is translated to a string receiver and silently evaluates to `Null`. | `regextest("^foo", "foobar")` and a row-membership query returned no match without a warning. | `specs/n0_spec.md` N0-5 fail-loud rule |
| N0-05 | Medium | DQL `trunc(-1.2)` maps to `floor` and returns `-2`. | Executed expression probe; DQL requires truncation toward zero. | `specs/n0_spec.md` N0-5 |
| N0-06 | Medium | Indexed fence source is not verbatim. | A CRLF fence body was stored with LF line endings. | `specs/n0_spec.md` N0-4 |
| N1-01 | High | Time-dependent queries can reuse stale cache entries. | `Recent` crossed its cutoff on a cache hit; a custom `now()` summary reused its earlier value. | `specs/n1_spec.md` N1-2 |
| N1-02 | High | Pinned `if` and `list` semantics are incompatible. | Two-argument `if` errors; `list([1,2])` nests; `list(1,2)` succeeds. | `specs/n1_spec.md` N1-1 |
| N1-03 | Medium | Pinned split/date/duration/render edges are incomplete. | Split limit/regex, space-separated datetime, mixed month/day duration, and unary render arity probes failed. | `specs/n1_spec.md` N1-1 |
| N1-04 | Medium | `file.aliases` and `file.embeds` are never populated. | A note with aliases and an embed rendered both columns empty. | `specs/n1_spec.md` N1-1 |
| N2-01 | High | Leading nulls make the FFI report `value_kind = null`, so later typed edits fall back to text. | Rows `[null, null, number]` produced a null column kind. | `specs/n2_spec.md` N2-1 |
| N2-02 | Medium | Durable export changes `Recent`'s inclusive cutoff to exclusive `>`. | Live engine uses `>=`; emitted `.base` uses `>`. | `00_program.md` N-G |
| N3-01 | High | Quick-filter N/M and export confirmation report the filtered count as both values. | A three-row fixture announces and prompts `1 of 1`, while “all” exports three. | `specs/n3_spec.md` N3-3 |
| N3-02 | High | Native row selection leaves stale cell selection, so Return can edit the previous row. | AppKit probe selected row 1 but dispatched edit for row 0. | `specs/n3_spec.md` N3-1/N3-4 |
| N3-03 | High | Clearing a cell never calls `delete_property`. | Empty commits call `set_property` or fail conversion; `basesDeleteProperty` has no caller. | `specs/n3_spec.md` N3-4 |
| N3-04 | High | Transient sort is local string sorting, absent from list mode and export. | Numeric `10` sorts before `2`; switching renderer/export loses the visible ordering. | `00_program.md` decision 13; `specs/n3_spec.md` N3-1/N3-2 |
| N3-05 | Medium | Page Up/Down moves a fixed ten rows, not one viewport. | Three-row viewport probe landed ten rows away. | `specs/n3_spec.md` N3-1 |
| N3-06 | Medium | Group rows lack heading semantics and headers omit the pinned sort grammar. | AppKit probe reported group role `AXUnknown` and a bare header label. | `specs/n3_spec.md` N3-1 |
| N3-07 | High | A successful note save does not re-execute visible Bases surfaces. | `performSave` refreshes note-derived panels but no open base, dashboard, or dock document. | `specs/n3_spec.md` N3-1 rule 8 |
| N4-01 | High | Builder round-trips drop `limit`, assigned summaries, and custom summaries; Save-as from Edit View Filters omits the effective global filter. | Serialization hard-codes empty/null facets and the edit entry point loads only the local view filter. | `specs/n4_spec.md` N4-1/N4-2 |
| N4-02 | High | Structured builder lacks per-type operators/editors, relative dates, and formula completion. | All property kinds receive one generic operator menu/text field; formulas have validation only. | `specs/n4_spec.md` N4-1/N4-2 |
| N4-03 | Medium | Missing dashboard sections claim actions that do not exist. | Missing state renders text only, with no Remove or Pick replacement action. | `specs/n4_spec.md` N4-4 |
| N4-04 | Medium | Follow-active suppresses empty-to-nonempty membership changes and compares order rather than membership. | Announcement requires a nonempty previous array. | `specs/n4_spec.md` N4-4 |
| N-VER-01 | Medium | Required adversarial censuses and under-load cancellation gate are materially absent. | “Census” tests are small fixed smoke cases; no DQL corpus or generated pushdown equivalence test exists; cancellation is pre-cancel only. | N0/N1/N2 specs and `02_milestone_brief.md` |
| N-VER-02 | Medium | Close-out performance evidence records Criterion means, not p50, and omits the N scan regression comparison. | `BENCHMARKS.md` labels the values as mean estimates and contains no N before/after scan diff. | `00_program.md` decision 16 |

## Remediation result

Every confirmed automated finding above is closed on the remediation branch.
The fixes were implemented with focused RED→GREEN regressions, independently
reviewed, and re-reviewed after each actionable counterexample wave.

| Finding IDs | Result | Evidence commits |
|---|---|---|
| N0-01, N0-02 | Lossless block/flow YAML edits now preserve quoting, comments, CRLF/LF, final-newline policy, unknown bytes, dependent edit batches, and valid empty parents. | `6a35e90`, `9d1310f`, `aa9d512`, `526b615` |
| N0-03..N0-06 | DQL outgoing/regex/trunc semantics and verbatim fence indexing repaired. | `16316f5`, `1c90089`, `72d311d` |
| N1-02, N1-03 | Pinned evaluator compatibility completed. | `c53256c`, `dde821e` |
| N1-01, N1-04, N2-01, N2-02 | Temporal cache safety, aliases/embeds, non-null kind inference, and inclusive Recent export repaired. | `49eba67`, `b86fedd` |
| N3-01, N3-04 | Quick-filter counts and typed transient sort unified across render/export paths. | `bc47b47` |
| N3-02, N3-03, N3-05, N3-06 | Grid selection/editing/navigation/group/header accessibility made safe. | `ec45a21`, `846289b`, `0d61645` |
| N3-07 | Successful note writes now refresh visible Bases surfaces without duplicate sort refreshes. | `501b55b`, `e3a7f0b` |
| N4-01..N4-04 | Typed lossless builder, complete expression regeneration, dashboard actions, and membership announcements completed. | `3a3004f`, `1b6620c` |
| N-VER-01 | Generated serializer/DQL/engine/session/scanner censuses, real under-load cancellation, public-delete cache coverage, and CLI-overridable Criterion runners added. | `526b615` |
| N-VER-02 | Criterion p50s and matched pre/post scan evidence recorded. 10k scan is +3.403%; 50k is +2.390%; both pass the 5% gate. | `BENCHMARKS.md` close-out entry in this evidence update |

Key closure gates include 39 serializer tests, 2,048 generated serializer
cases, 4,096 generated DQL statements, 3,072 pushdown/interpreter query
comparisons, 10,000-row parked-under-load cancellation, generated warm/cold
session interleavings, and the 10,000-file + 1,000-mutation scanner census. The
Task 8 Swift matrix passed 130 tests and `a11y-check` remained 100.0/100.

## Remaining manual-only blocker

`at_smoke_checklist.md` is entirely unchecked. This audit will not manufacture a
VoiceOver PASS from static checks. Code, automated tests, and benchmark evidence
are remediated; the GitHub milestone must remain open until a human runs the
checklist and records PASS or follow-up issues for failures.

## Checked and found correct

- Expression precedence, regex/division lexing, recursive filters, circular
  formula diagnostics, unknown-key preservation, and untouched byte equality on
  the checked-in block-style corpus.
- Scanner classification, migration 021, ordinary degraded rows, supported
  fence discovery, and DataviewJS exclusion.
- Pushdown/interpreter equivalence on existing fixtures, deterministic path
  tiebreaks, grouping, pre-limit summaries, task row identity, task aggregates,
  full-text matching, and owner-file predicates.
- Session handle lifecycle, `this` precedence, saved-query/dashboard envelopes,
  future-version warnings, dangling-reference preservation, CLI formats, and
  exit semantics.
- `.base` routing, view switching, list projection variants, embeds, read-only
  embed enforcement, pin persistence, dynamic commands, and help drift checks.

The implementation plan is
[`docs/superpowers/plans/2026-07-09-milestone-n-remediation.md`](../../superpowers/plans/2026-07-09-milestone-n-remediation.md).
