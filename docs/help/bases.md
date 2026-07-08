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

## Function Status

Slate parses the documented Obsidian Bases function inventory, but v1 only evaluates the deterministic subset. Unsupported constructs fail loud in the view instead of silently changing membership.

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

Use **Bases: Quick filter** or the grid search field for a temporary filter across displayed values. It never changes the `.base` file, never marks the tab dirty, and never changes saved filters. Use it for one-off narrowing; use the builder when you want a saved filter.

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

Converts:

| DQL | Bases equivalent |
|---|---|
| `TABLE WITHOUT ID a AS "A"` | Table columns with display names. |
| `LIST expr` | List view with the expression as a column. |
| `TASK` | `source: tasks` with task fields. |
| `FROM "Folder"` | `file.inFolder("Folder")`. |
| `FROM #tag` | `file.hasTag("tag")`. |
| `WHERE expr` | Filter expression. Repeated `WHERE` clauses are ANDed. |
| `SORT a ASC, b DESC` | Sort keys. |
| `LIMIT n` | View limit. |
| `file.cday`, `file.mday`, `file.link` | `file.ctime.date()`, `file.mtime.date()`, `link(file.path)`. |

Fails loud:

| DQL construct | Why |
|---|---|
| `CALENDAR` | No calendar view in Bases v1. |
| `GROUP BY` | Dataview creates one row per group with a `rows` array; Slate groupBy keeps rows and labels sections. Silent conversion would change membership. |
| `FLATTEN` | Changes row cardinality; reserved for future work. |
| Order-dependent command pipelines | Bases has a fixed filter/sort/limit model. |
| Unmapped functions such as `upper`, `nonnull`, `hash`, formatting helpers, and many vectorized Dataview helpers | Not in the v1 Bases function set. |
| Dataview task fields Slate does not index, such as `created`, `completion`, `start`, `fullyCompleted`, `children`, `section` | No backing data in Slate's v1 task index. |
| DataviewJS and inline `$=` / `= expr` | No JavaScript runtime in Slate v1. Rewrite as Slate queries or wait for future plugin/WASM extension points. |

Null comparisons differ from Dataview's JavaScript-like behavior. When a migrated filter depended on `null <= date(...)`, make the intent explicit with `typeof`/presence checks or a condition such as `field && date(field) <= today()`.

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
| Bases: Quick filter |
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
