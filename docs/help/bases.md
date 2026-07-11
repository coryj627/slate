# Bases

> Shortcuts shown in a Bases grid are local to that grid. The Command Palette is always the authoritative list of named Bases commands.

Bases are structured queries over your vault. A `.base` file is portable YAML that Obsidian can keep round-tripping, and Slate renders the same query as an accessible table or list. You can also create saved queries, dock a query in the sidebar so `this` follows the active note, and compose saved queries into dashboards.

Slate treats query execution as read-only. The only writes are explicit `.base` saves, saved-query/dashboard records, and property edits you make in an editable grid cell.

## Opening And Reading

Open a `.base` file from the file tree or Quick Open. The first view opens by default; use **Bases: Open View Switcher**, **Bases: Next View**, or **Bases: Previous View** to change views.

Table views use cell navigation. Column headers announce sort state, cells announce their column and value, group headers are navigable headings, and summary rows are separate from data rows. List views use row navigation and are useful when you want a compact reading order instead of cell-by-cell movement.

Use **Bases: Where Am I?** to hear the active base, view, result count, and temporary quick filter. Use **Bases: Results** for the result summary.

## Query Syntax

A `.base` file is YAML. Slate recognizes these top-level keys:

| Key | Meaning |
|---|---|
| `filters` | A string expression or nested `and` / `or` / `not` object applied to every view. |
| `formulas` | Named expression strings, addressed as `formula.name`. |
| `properties` | Display settings such as `displayName` for note, file, and formula properties. |
| `summaries` | Named summary formulas for per-view summary assignments. |
| `views` | Ordered view list. The first view is the default. |

View keys include `type`, `name`, `limit`, `groupBy`, `filters`, `order`, and `summaries`. Unknown keys are preserved. Slate stores its own view state under a `slate` sub-key so it does not collide with Obsidian's undocumented state.

Filters and formulas share the expression language:

```yaml
filters:
  and:
    - "file.inFolder(\"Projects\")"
    - or:
        - "status == \"active\""
        - "priority >= 2"
formulas:
  ageDays: "((today() - date(created)) / duration(\"1d\")).floor()"
views:
  - type: table
    name: Active
    order:
      - file.name
      - status
      - formula.ageDays
```

Bare identifiers read note properties. Use `note.status`, `file.name`, `formula.ageDays`, `task.text`, or `this.file.name` when you want the namespace to be explicit.

## Native Bases Function Status

This table describes expressions authored directly in `.base` files or the Slate builder. Slate parses the documented Obsidian Bases function inventory, but v1 only evaluates the deterministic subset. Unsupported constructs fail loud in the view instead of silently changing membership. Dataview conversion has a separate, isolated compatibility layer described below; its coercion and vectorization rules do not change native Bases behavior.

| Function or family | Slate v1 status |
|---|---|
| `if`, `date`, `duration`, `now`, `today`, `number`, `string`, `link`, `list`, `object`, `file`, `escapeHTML` | Evaluated. |
| `min`, `max`, `sum`, `average` | Evaluated for numeric aggregate inputs. |
| `html`, `image`, `icon` | Parsed and rendered as text, not rich HTML/media. |
| `random` | Excluded; deterministic result sets are a release gate. |
| Any methods: `isTruthy`, `isType`, `toString` | Evaluated. |
| Date methods: `date`, `format`, `time`, `relative` | Evaluated. `format` supports the v1 Moment-style token subset. |
| String methods: `contains`, `containsAll`, `containsAny`, `startsWith`, `endsWith`, `lower`, `title`, `trim`, `reverse`, `repeat`, `slice`, `split`, `replace`, `isEmpty` | Evaluated. |
| Number methods: `abs`, `ceil`, `floor`, `round`, `toFixed`, `isEmpty` | Evaluated. |
| List methods: `contains`, `containsAll`, `containsAny`, `join`, `flat`, `reverse`, `sort`, `unique`, `slice`, `filter`, `map`, `reduce`, `isEmpty` | Evaluated. |
| Link methods: `asFile`, `linksTo` | Evaluated. |
| File methods: `asLink`, `hasLink`, `hasProperty`, `hasTag`, `inFolder`, `matches` | Evaluated. `file.matches` is a Slate extension and only runs in filters. |
| Object methods: `isEmpty`, `keys`, `values` | Evaluated. |
| Regex `matches` | Evaluated. |
| Obsidian summary names outside the v1 set, custom summaries that call unsupported functions | Preserved and fail loud if executed. |

Built-in summary assignments supported in v1 are count-style, empty/filled/unique, numeric min/max/sum/average, date earliest/latest, and task checkbox checked/unchecked mappings.

Summaries are computed after filtering and grouping, but before `limit` is applied. This keeps an average, count, or total representative of the complete matching set even when the view displays only its first few rows. In result metadata, `total_count` is the post-filter, pre-limit row count and `shown_count` is the number of rows actually displayed after the limit. A temporary quick filter narrows the displayed result without changing the saved summary contract.

## Slate Extensions And Interop

Slate adds a few query capabilities that Obsidian does not currently execute:

| Extension | What it does | Interop caveat |
|---|---|---|
| `source: tasks` | Runs a base over task rows instead of file rows. | Obsidian treats this as unknown view state; keep task views in Slate-authored bases. |
| `task.*` | Reads indexed task fields: text, status, completed, due, scheduled, priority, and file. | Dataview task fields that Slate does not expose in Bases fail loud during migration. |
| `file.tasks.*` | Exposes note-level task aggregates. | Obsidian does not have these fields in Bases. |
| `file.matches("fts query")` | Uses Slate's full-text index inside a filter. | Filter-only and Slate-only. Export or duplicate the query before relying on it in Obsidian. |
| `file.inDegree` / `file.outDegree` | Link graph degree counts from Slate's index. | Basic graph fields; full graph-derived query features are future work. |
| `slate:` view sub-key | Stores Slate sort, density, or other UI state without touching Obsidian keys. | Preserved by Slate; Obsidian ignores unknown state. |

Cards, map, and plugin-authored view types are preserved. Slate v1 renders them with a table fallback and a notice naming the requested layout.

## Quick Filter

Use **Bases: Quick Filter** or the grid search field for a temporary filter across displayed values. It never changes the `.base` file, never marks the tab dirty, and never changes saved filters. Use it for one-off narrowing; use the builder when you want a saved filter.

Exports and **Bases: Copy View as Markdown** include the quick filter by default so copied data matches what you are reading. Save-panel exports let you choose whether to include it.

## Builder Walkthrough

Use **Bases: New Query** to open the structured builder, or **Bases: Edit View Filters** from an open view.

1. Pick the source: all notes, a folder, a tag, recent files, linked-from note, or tasks.
2. Add conditions with **Bases: Add Condition**. Each condition is a structured row: property, operator, value.
3. Add one-level groups with **Bases: Add Group** for All / Any / None composition.
4. Use formulas for calculated columns; validation reports the first syntax problem before save.
5. Choose columns, table/list view, sort keys, group key, and summaries.
6. Preview the result, then save to the current view, save as a new `.base`, or save as a saved query.

Use **Bases: Edit Condition** and **Bases: Remove Condition** on the selected builder row. Advanced expressions stay visible as read-only chips until you edit them as raw expressions.

## Saved Queries And Dashboards

A saved query is stored in Slate's vault database and can be pinned in the Queries sidebar. VoiceOver reads a pin as "`<query name>, saved query`." Saved-query records are convenient, but they are not portable vault files. Use export-as-`.base` when you want a durable file that syncs and opens outside Slate.

Dashboards are ordered saved-query sections. Each dashboard tab has a dashboard heading and section headings, followed by each section's grid. Missing saved-query references render as labeled missing sections so you can remove or replace them.

The Base dock sidebar can dock a `.base`, saved query, or dashboard. In the dock, `this` resolves to the active note and re-runs on note switch. This powers patterns like better backlinks:

```yaml
views:
  - type: table
    name: Links to this note
    filters: "file.hasLink(this.file)"
    order:
      - file.name
```

## Dataview Migration

Slate reads `dataview` block queries and can convert supported DQL to `.base` YAML. Slate never writes DQL and never executes DataviewJS.

### DQL sources and commands

| DQL source or command | Slate conversion | Status / caveat |
|---|---|---|
| `TABLE WITHOUT ID a AS "A"` | Table columns with display names; omitting `WITHOUT ID` prepends the file link. | Converts. |
| `LIST expr` | List view with the expression as its secondary property. | Converts. |
| `TASK` | `source: tasks` with task fields. | Converts. |
| `FROM #tag` | `file.hasTag("tag")`. | Converts, including nested tags. |
| `FROM "Folder"` | `file.inFolder("Folder")`. | Converts recursively. |
| `FROM "path/to/file.md"` | `file.path == "path/to/file.md"`. | A quoted source without `.md` is treated as a folder. |
| `FROM [[note]]` | `file.hasLink("note")`. | Converts. |
| `FROM outgoing([[note]])` | Linked source; durable `.base` export uses `link("note").linksTo(file.file)`. | Converts links and embeds. |
| `FROM [[]]` / `FROM [[#]]` | The corresponding link or outgoing source over `this`. | Requires an embedded/docked context. |
| Repeated `WHERE expr` | Filter expressions ANDed together. | Converts. |
| `SORT a ASC, b DESC` | Ordered sort keys. | Converts all documented direction spellings. |
| `LIMIT n` | View limit. | Converts non-negative integers. |
| `CALENDAR` | No equivalent view. | Fails loud. |
| `GROUP BY` | Dataview creates one row per key with a `rows` array; Slate grouping retains every row. | Fails loud to avoid changing membership. |
| `FLATTEN` | Changes row cardinality. | Fails loud. |
| Pipelines outside `WHERE* SORT? LIMIT?` order | Bases has a fixed filter/sort/limit model. | Fails loud. |

Both source negations (`-source` and `!source`) convert to a `not` filter. `and`, `or`, and parentheses retain their authored grouping.

### DQL file fields

| DQL field | Slate target | Status |
|---|---|---|
| `file.name` | Dataview page title. | Converts; strips `.md` but preserves other extensions. |
| `file.path`, `file.folder`, `file.ext`, `file.size`, `file.ctime`, `file.mtime` | Same-named indexed file value. | Converts. |
| `file.tags` | Ordered Dataview tag projection. | Converts with leading `#`; preserves authored case and first-occurrence order, expands nested parents once, and string-coerces non-null tag values. This does not rewrite Slate's normalized native tag index. |
| `file.aliases` | Frontmatter aliases only. | Converts; body inline fields named `alias` or `aliases` do not contribute. |
| `file.cday`, `file.mday` | `file.ctime.date()`, `file.mtime.date()`. | Converts. |
| `file.link` | `link(file.path)`. | Converts. |
| `file.inlinks` | Resolved incoming page links. | Converts; links and embeds are deduplicated by page identity. |
| `file.outlinks` | Resolved outgoing page links. | Converts; preserves link/embed metadata while deduplicating pages. |
| `file.etags`, `file.lists`, `file.frontmatter`, `file.day`, `file.starred` | No v1 field. | Fails loud. |

Ordinary DQL row properties merge frontmatter with indexed page/list inline fields in source order. Exact authored keys win before canonical-key collision merging, repeated values remain ordered, and inline links resolve relative to the note that owns them. If Slate cannot prove the body projection complete, the query fails loud instead of returning partial fields.

### DQL task fields

| DQL field | Slate target | Status / caveat |
|---|---|---|
| `text`, `status` | `task.text`, `task.status`. | Converts in `TASK` queries. |
| `completed` | `task.status == "x" OR task.status == "X"`. | Matches Dataview's two completed markers. |
| `checked` | Status is nonempty and is not the single unchecked space. | Converts. |
| `due`, `scheduled` | `task.due`, `task.scheduled`. | Converts. |
| `created`, `completion`, `start`, `fullyCompleted`, `children`, `section`, `subtasks`, `line`, `lineCount`, `path`, `blockId`, `link` | No indexed v1 task field. | Fails loud. |

### DQL functions

| DQL function or family | Slate conversion | Status / caveat |
|---|---|---|
| `contains`, `lower`, `replace`, `join`, `length` | Isolated DQL-compatible operation. | Converts. `replace` is literal-all; `join` also stringifies a scalar and ignores its separator. |
| `sort`, `reverse`, `unique`, `flat`, `slice`, `filter`, `map` | Isolated DQL-compatible list operation. | Converts supported list shapes and one-argument lambdas for `filter`/`map`; unsupported dynamic, nested, or multi-argument lambda shapes fail loud. |
| `sum`, `average`, `min`, `max` | DQL aggregate over a list-shaped input. | Converts. A null first element fails; later nulls are skipped, while `average` still divides by the full list length. `min`/`max` preserve supported typed values. |
| `startswith`, `endswith` | `.startsWith`, `.endsWith`. | Converts. |
| `round`, `trunc`, `floor`, `ceil` | Corresponding numeric operation; `trunc` rounds toward zero. | Converts. |
| `regextest`, `regexmatch`, `regexreplace` | JavaScript-compatible regex operation. | Converts representable patterns: `regextest` searches, `regexmatch` requires the whole string, and replacement/ASCII character-class behavior follows DQL. Rust-only or unrepresentable JavaScript syntax fails loud. |
| `split`, `substring` | DQL split/slice operation. | Converts. `split` accepts scalar text only and does not vectorize over list input. |
| `striptime` | `.date()`. | Converts. |
| `choice`, `default` | DQL truth/default operation. | Converts. `default` replaces null only; empty strings, lists, and objects remain values. `ldefault` is unsupported. |
| `typeof` | `isType` rewrite in boolean comparisons. | Converts; use it to guard nullable fields. |
| `number`, `string`, `date`, `dur`, `link`, `embed`, `object`, `list`, `array` | Isolated DQL constructor. | Converts supported shapes. `list`/`array` are variadic and preserve nested lists. One- and two-argument `link` calls vectorize; the three-argument form is scalar-only and requires string, string, boolean. |
| `upper`, `truncate`, `padleft`, `padright`, `containsword`, `econtains`, `icontains` | No v1 DQL conversion. | Fails loud. |
| `dateformat`, `durationformat`, `currencyformat`, `localtime` | Formatting/local-time semantics are not portable to v1. | Fails loud. |
| `hash`, `meta`, `minby`, `maxby`, `product`, `reduce`, `extract`, `firstvalue`, `nonnull`, `display`, `elink`, `ldefault` | No v1 DQL conversion. | Fails loud. |

Function names and date shorthand tokens are lowercase and case-sensitive in DQL. Supported scalar functions vectorize over list arguments using the shortest list. DataArray-style property projection drops missing/null entries before flattening list-valued entries. Authored method-call syntax and authored `if(...)` are not Dataview DQL forms; use the mapped functions above, including `choice(...)`, or the conversion fails loud.

DQL dates retain local-calendar versus explicit-offset provenance. Local date arithmetic preserves the authored day across daylight-saving transitions; week shorthands use Monday through Sunday, and end-of-week/month/year values end at `23:59:59.999`. Durations retain their authored year/month/week/day/hour/minute/second units for DQL equality and calendar arithmetic. `date(link)` tries the exact display and path, then the resolved page's ordered `date`/`day` fields and dated Markdown title.

### Failure behavior

The converter preserves the supported DQL truthiness, equality, ordering, coercion, and vectorization rules inside compatibility markers. Those rules are intentionally isolated from native Bases expressions. A bare DQL `null` literal, native-Bases-only fields, unsupported authored methods, and unrepresentable dynamic shapes fail loud rather than falling through to similarly named native properties or functions.

Direct mixed-null ordering is not representable safely. When a migrated filter depended on `null <= date(...)`, make the intent explicit with a `typeof`/presence check or a condition such as `field && date(field) <= today()`.

DataviewJS and inline `$=` / `= expr` have no JavaScript runtime in Slate v1. Rewrite them as Slate queries or wait for a future plugin/WASM extension point. Every unsupported conversion remains visible as a named warning or view error; Slate never silently broadens or narrows the query.

## CLI Querying

Use `slate query` for automation:

```sh
slate query /path/to/vault --base Queries/Reading.base --format json
slate query /path/to/vault --base Queries/Reading.base --view Reading --format csv
slate query /path/to/vault --saved "Active projects" --this Projects/Alpha.md
```

JSON returns an array of row objects keyed by column label plus `path`. CSV and Markdown use the same export renderer as the app.

## Command Reference

Every Bases command lives in the Command Palette under **Bases**. Static Bases commands have no global chords.

| Command |
|---|
| Bases: Open View Switcher |
| Bases: Next View |
| Bases: Previous View |
| Bases: Sort by Column |
| Bases: Save Sort to View |
| Bases: View as Table |
| Bases: View as List |
| Bases: Quick Filter |
| Bases: Where Am I? |
| Bases: Open Row |
| Bases: Copy Link |
| Bases: Show Backlinks |
| Bases: Edit Property |
| Bases: Export View as CSV |
| Bases: Export View as Markdown Table |
| Bases: Copy View as Markdown |
| Bases: Results |
| Bases: Refresh |
| Bases: New Query |
| Bases: Edit View Filters |
| Bases: Add Condition |
| Bases: Add Group |
| Bases: Edit Condition |
| Bases: Remove Condition |

Saved queries also register dynamic commands named `Run query: <query name>`.

## Troubleshooting

- **"this is unavailable"** means the query uses `this` outside an embedded or docked context. Dock the query to the Base dock or open it from a note embed.
- **A view falls back to a table** when the `.base` requests cards, map, or a plugin view. Slate preserves the requested type and unknown state.
- **A conversion fails** when Dataview would change row membership or uses unsupported functions. The error names the construct to rewrite.
- **A query is slow** when it asks for expensive fields such as backlinks over a large result set. Add filters first, then widen.
