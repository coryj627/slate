# Milestone N research brief — Obsidian Bases: format, semantics, and demand evidence

Primary-source verification for the Bases v1 program. Sources: the official help corpus (`obsidianmd/obsidian-help` @ master, `en/Bases/`, 10 files, fetched 2026-07-06), two forum feature-request threads (fetched 2026-07-06 via the Discourse JSON API), six third-party field reports (2025-08 → 2026-06), and the official Dataview documentation (`blacksmithgu/obsidian-dataview` @ master `docs/docs/`, fetched 2026-07-06 — §8). **This brief is normative for every Obsidian-syntax fact in the N specs**; where the help docs are silent (§6), the spec says so explicitly rather than guessing.

---

## §1 The `.base` file format

A `.base` file is valid YAML with five top-level keys: `filters`, `formulas`, `properties`, `summaries`, `views`. Complete example, **verbatim from `Bases syntax.md`**:

```yaml
filters:
  or:
    - file.hasTag("tag")
    - and:
        - file.hasTag("book")
        - file.hasLink("Textbook")
    - not:
        - file.hasTag("book")
        - file.inFolder("Required Reading")
formulas:
  formatted_price: 'if(price, price.toFixed(2) + " dollars")'
  ppu: "(price / age).toFixed(2)"
properties:
  status:
    displayName: Status
  formula.formatted_price:
    displayName: "Price"
  file.ext:
    displayName: Extension
summaries:
  customAverage: 'values.mean().round(3)'
views:
  - type: table
    name: "My table"
    limit: 10
    groupBy:
      property: note.age
      direction: DESC
    filters:
      and:
        - 'status != "done"'
        - or:
            - "formula.ppu > 5"
            - "price > 2.1"
    order:
      - file.name
      - file.ext
      - note.age
      - formula.ppu
      - formula.formatted_price
    summaries:
      formula.ppu: Average
```

Key-by-key (all from `Bases syntax.md` unless noted):

- **`filters`** (top level) applies to **all views**. There is no `from`/source clause — the default dataset is **every file in the vault**; filters only narrow it. Base-wide and per-view filters are **"concatenated with an `AND`"**.
- **`formulas`**: map of `name: "expression string"`. Formula values are always YAML strings; the output type is determined by the data + function return types. Formulas may reference other formulas (`formula.x`) as long as there is no circular reference.
- **`properties`**: per-property display configuration keyed by property id (`status`, `formula.formatted_price`, `file.ext`). Documented sub-key: `displayName`. Display names are **not** usable in filters or formulas.
- **`summaries`** (top level) **defines** named summary formulas; per-view `summaries` **assigns** built-in or custom summaries to properties. Inside a summary formula the keyword `values` is the list of that property's values across the result set; the formula must return a single value. (`values.mean()` appears in the docs' example but `mean()` is absent from `Functions.md` — summary context has a superset; see §6.)
- **`views`**: ordered list; **first view is the default** and the embed default. Documented per-view keys: `type` (`table`/`cards`/`list`/`map`/plugin), `name` (display name + embed anchor), `limit`, `filters`, `groupBy` (`{property, direction: ASC|DESC}`, one property only), `order` (list of property ids = displayed columns in order), `summaries` (map property id → summary name). **Open extension point (load-bearing for round-trip):** views "can add additional data to store any information needed" — row height, column widths, sort state, card image property, map settings are all *undocumented per-view state*, not schema. Real `.base` files contain such keys; a compatible parser must preserve them verbatim (program decision 3).

Row sort is exposed in the UI ("one or more properties, ascending or descending, including formulas" — `Views.md`) but has **no documented YAML key** — it lives in that per-view free-form state. Sort semantics by type: Text A→Z; Number smallest→largest; Date old→new (each reversible).

## §2 Filter and expression syntax

- `filters` holds either a **single filter-statement string** or a **recursive filter object** containing exactly one of `and` / `or` / `not`, each a heterogeneous list of statement strings and/or nested objects. `not` takes a *list* and means **NOT(OR(...))** ("results will not be shown if *any* of the conditions in the group are met" — `Views.md`).
- A filter statement is any expression evaluating truthy/falsey against a note. **"The syntax and available functions for filters and formulas are the same"** — one expression language.
- Operators: arithmetic `+ - * / %` and `( )`; comparison `== != > < >= <=` (`>`-family for numbers and Dates; `==`/`!=` for any kind); boolean `! && ||`.
- **Date arithmetic:** `Date ± "durationString"` (`date + "1M"`, chainable `date("2024-12-01") + "1M" + "4h" + "3m"`); `Date − Date` → **milliseconds** (`(now() + "1d") - now()` → `86400000`). Duration unit tokens: `y|year|years`, `M|month|months`, `d|day|days`, `w|week|weeks`, `h|hour|hours`, `m|minute|minutes`, `s|second|seconds` (note `M` = month vs `m` = minute).
- Literals: strings `"…"`/`'…'`; numbers (literal receivers may need parens: `(2.5).round()`); bare `true`/`false`; lists `[1,2,3]`; objects `{"a": 1}`; regex `/abc/` (`g` flag honored in replace). List indexing `property[0]` (0-based); object access `property.subprop` or `property["subprop"]`.
- **Links:** frontmatter wikilinks are Link objects. `==` on links: equal if they resolve to the same file; if unresolved, link **text** must match. Links compare against file objects (`author == this`) and work in `list.contains(this)`.
- UI model (`Views.md`): a filter row = Property + Operator + Value; conjunction groups "All / Any / None of the following are true" = `and`/`or`/`not`; groups nest; an **advanced editor** (code button) exposes raw syntax for what the structured UI can't express. This is the shape Slate's accessible builder mirrors (05 §8.6).

## §3 Function inventory (complete, from `Functions.md`)

"Bases functions follow JavaScript behavior." Global functions:

| Function | Signature | Notes |
|---|---|---|
| `date` | `date(s: string): date` | parses `YYYY-MM-DD[ HH:mm:ss]` |
| `duration` | `duration(s: string): duration` | for duration arithmetic; scalar on the right (`duration('5h') * 2`) |
| `escapeHTML` | `escapeHTML(s: string): string` | |
| `file` | `file(path: string \| file \| url): file` | `file(link("[[name]]"))` |
| `html` | `html(s: string): html` | render as HTML |
| `icon` | `icon(name: string): icon` | Lucide name |
| `if` | `if(cond, then, else?): any` | `else` defaults to null |
| `image` | `image(path \| file \| url): image` | renders in view |
| `link` | `link(path \| file, display?): Link` | display may be `icon(...)` |
| `list` | `list(x: any): List` | normalizes scalar-or-list properties |
| `max`/`min` | variadic numbers | |
| `now` | `now(): date` | date+time |
| `today` | `today(): date` | midnight |
| `number` | `number(x: any): number` | date→ms epoch; bool→1/0; string parsed |
| `random` | `random(): number` | 0–1, regenerates per view load — **excluded from Slate v1** (determinism, DoD §N-B) |

Typed methods:

- **Any:** `isTruthy()`, `isType(name: string)`, `toString()`.
- **Date fields:** `year`, `month` (1–12), `day`, `hour` (0–23), `minute`, `second`, `millisecond`. Methods: `date()` (strip time), `format(fmt)` (**Moment.js format strings**), `time()`, `relative()` ("3 days ago"), `isEmpty()` (always false).
- **String:** field `length`; `contains`, `containsAll`, `containsAny`, `startsWith`, `endsWith`, `isEmpty()` (true if empty **or absent**), `lower()`, `title()`, `trim()`, `reverse()`, `repeat(n)`, `slice(start, end?)`, `split(sep: string|Regexp, n?)`, `replace(pattern: string|Regexp, replacement)` (string pattern replaces **all**; capture refs `$1`, `$2`).
- **Number:** `abs()`, `ceil()`, `floor()`, `round(digits?)`, `toFixed(precision): string`, `isEmpty()`.
- **List:** field `length`; `contains`, `containsAll`, `containsAny`, `isEmpty()`, `join(sep)`, `flat()`, `reverse()`, `sort()`, `unique()`, `slice(start, end?)`, and **expression-based** `filter(expr)` / `map(expr)` (implicit `value`, `index`) / `reduce(expr, initial)` (implicit `value`, `index`, `acc`; sum idiom `[1,2,3].reduce(acc + value, 0)`). Not lambdas — bare expressions with implicit variables.
- **Link:** `asFile(): file`, `linksTo(file): boolean`.
- **File fields:** `name`, `basename`, `path`, `folder`, `ext`, `size`, `properties: object`, `tags: list` (content **and** frontmatter), `links: list`, `ctime`, `mtime`. Methods: `asLink(display?)`, `hasLink(other)`, `hasProperty(name)`, `hasTag(...tags)` (**any**-of; **includes nested**: `hasTag("a")` matches `#a/b` — matches Slate's shipped `SearchScope::Tag` semantics, search_db.rs:60), `inFolder(folder)` (folder **or any sub-folder**).
- **Object:** `isEmpty()`, `keys()`, `values()`.
- **Regexp:** `matches(s: string): boolean`.

## §4 Property namespaces and `this`

Three namespaces: **`note.*`** (frontmatter; **bare identifiers default to `note`**; bracket form `note["price"]`), **`file.*`** (all file types), **`formula.*`**. Enumerated `file.*` table (`Bases syntax.md`): `backlinks` (List — flagged "performance heavy", does **not** auto-refresh), `ctime`, `embeds`, `ext`, `file` (file object), `folder`, `links`, `mtime`, `name`, `path`, `properties` (Object — does not auto-refresh), `size`, `tags`. (Slate adds two extension fields Obsidian lacks: `file.inDegree` / `file.outDegree` — degree counts from the links index, honoring the milestone-14 "ship the basics" commitment; interop caveat as with all extensions.)

**`this`** is context-dependent: (1) base open in the main content area → the base file itself; (2) base **embedded** in another file → the **embedding** file; (3) base in a **sidebar** → the **active file** in the main content area (the "better backlinks" pattern: `file.hasLink(this.file)`).

**Embedding:** `![[File.base]]` renders the first view; `![[File.base#View]]` selects a view by name; a ` ```base ` code block holds full Bases YAML inline in a note (`Create a base.md`). Views/toolbar: view menu, results (limit/copy/**export CSV**), sort, filter, properties, **search**, new-file. View types with min versions: Table (1.9), Cards (1.9), List (1.10), Map (1.10 + official Maps plugin); community plugins can add layouts.

**List view settings** (`List view.md`): **Markers** — bullets/numbers/none; **Indent properties** — when on, selected properties render as indented sub-items under the primary item (the property at the **top** of the Properties menu; reorder by drag or `Alt+Up/Down`); **Separators** — when indenting is off, a separator character joins properties on one line, **default comma**.

**Formula editor** (`Formulas.md`): autocompletes function and property names and shows a **green checkmark on valid syntax** (error feedback otherwise) — the validation-affordance parity target for Slate's builder formula editor.

**Summaries:** per-view, per-column; shown at column foot, and per group when grouped. Built-ins by input type — Number: Average, Min, Max, Sum, Range, Median, Stddev; Date: Earliest, Latest, Range; Checkbox: Checked, Unchecked; Any: Empty, Filled, Unique.

**Editing:** the table view is read-write — cell edit, shift-click selection, copy/paste cells, `Backspace` clears, undo/redo of property changes; edits write back to frontmatter. Formula columns and immutable file properties are read-only.

## §5 Feature-request evidence (the two threads the owner pinned)

### §5.1 Transient quick search (forum 100964, ~45 posts, closed-resolved)

The ask: a **temporary, non-persistent** search/filter inside a view — Ctrl/Cmd-F or a toolbar search box — explicitly *not* a saved-filter edit. Pain points: saved filters are overkill for one-off lookups; long tables unnavigable; Cmd-F silently does nothing in a base; **git-controlled vaults get spurious `.base` diffs when users abuse saved filters for ad-hoc search**. Obsidian's resolution: on the public roadmap 2025-11; team confirmed "will be implemented in 1.12.x" (2026-02-10); shipped in 1.12.1 as a toolbar search icon filtering by displayed properties. Variants requested but not shipped: per-column header search, Excel-style distinct-value checklists, saved quick-filter presets, field-scoped query syntax (`Title:Meeting`, wildcards, boolean). **Slate v1 ships the transient toolbar+⌘F filter (N3-3); the variants are recorded as reserved enhancements, not scope.**

### §5.2 Tasks in Bases (forum 103074, 30 posts, open, no team commitment)

The ask, two tiers: (a) **tasks as rows** — a base whose entries are checkbox items from note bodies, with `task.*` addressing, due/scheduled/priority filters, calculated fields; (b) minimal **note-level task counts** (`file.tasks` total/completed) for progress columns. Obsidian's blocker is architectural and explicit: "Bases don't scan file contents; only cached information" (moderator), and "the cache only stores the task location and that a task exists, but not the task details/text" (Licat, team). Community workaround: one-note-per-task modeling (manual YAML or the TaskNotes plugin). **Slate's position is structurally different: the `tasks` table (migration 008) already indexes text, status_char (verbatim, custom-status-safe), completed, due_ms, scheduled_ms, priority, recurrence, line — powering the Tasks panel since Milestone G. N1-4 exposes both tiers natively. This is a headline differentiator, not a parity item.**

## §6 Where the help docs are silent (spec-relevant gaps)

1. **Per-view UI state keys are undocumented** (sort, column widths, row height, card image/size/ratio, map settings). N treats *all* unrecognized view keys as opaque preserved state (decision 3); Slate's own grid never invents Obsidian's names — Slate-authored view state uses a `slate` sub-key namespace to avoid collision (N0-2 rule).
2. `values.mean()` in the summaries example has no `Functions.md` entry — summary context is a superset. N1-3 ships the milestone-14 default summary list and maps Obsidian's built-in summary *names*; custom summary formulas evaluate when they stay inside the v1 function set, else fail loud (decision 6).
3. `groupBy.direction` shows `DESC` only; `ASC` is accepted as the counterpart.
4. No kanban/calendar/chart view types exist in the official docs — table, cards, list, map only. Anything else in the wild is plugin-authored and hits the same preserve-and-fallback path as cards/map (decision 4).
5. `file.backlinks` is documented as performance-heavy and non-refreshing in Obsidian. Slate's links table indexes both directions; N supports `file.backlinks` natively but the v1 engine may evaluate it Rust-side without pushdown (n1 spec).

## §7 Field reports (what real users build — shapes defaults, not scope)

Six write-ups reviewed (Felker/Medium; Dubois/dsebastien.net 20k-note dashboards; Obsidian Rocks; Effortless Academic; XDA Notion-switcher; Chugh architect's guide). Recurring patterns worth honoring in defaults and docs:

- **"One base, many views"** is the canonical mental model (base-wide filter = funnel mouth; per-view filters refine). The BasesView view switcher and the builder must make view-adding cheap (N3-1, N4-2).
- **Properties discipline is the whole game** — bases expose messy metadata. Slate already has the property-key index (`list_property_keys`, session.rs:1590) and the FL sidebar; the builder's property picker lists real vault keys, not free text (N4-1).
- **`this`-driven context panels** (better-backlinks, parent/child navigation) are the most-praised advanced pattern → embed-`this` in N3-5, sidebar follow-active in N4-4.
- **CLI querying** (`obsidian base:query … format=json`) is called "the bridge between pretty dashboard and automation building block" → N2-3 `slate query`.
- **Editing in place** (change `status`, every view updates) is the headline Dataview-killer → N3-4 routes through the existing property write path.
- Formula idioms observed in the wild that the v1 function set must cover: `if(...)` chains, `date(x) - date(y)` day math, `.relative()`, `file.ctime`, string `contains/lower/replace`, `link(file, "…" + file.name)`, time-recency bucketing via comparison chains, numeric `round/toFixed`. All are in the §3 v1 set.

## §8 Dataview DQL (migration input — in N scope per owner decision 2026-07-06)

Source: `blacksmithgu/obsidian-dataview` @ master, `docs/docs/` (queries/structure, query-types, data-commands, dql-js-inline; reference/expressions, literals, sources, functions; annotation/metadata-pages, metadata-tasks), fetched 2026-07-06. DQL only — DataviewJS and `$=` inline JS are permanently out (05 §8.1).

### §8.1 Query structure

` ```dataview ` fenced block: **exactly one query type** (the only mandatory element), then **zero-or-one `FROM`** (must come immediately after the type, never repeats), then **zero-to-many other data commands, repeatable, executed in written order** (order is observable: `LIMIT 5` then `SORT …` sorts the already-limited rows).

| Type | Header | Semantics |
|---|---|---|
| `TABLE [WITHOUT ID]` | comma-separated column exprs, optional `AS "Header"` (quotes required when the alias has spaces) | rows are pages; first column = file link + count unless `WITHOUT ID` |
| `LIST [WITHOUT ID]` | at most **one** extra expr | bullet list of file links (`link: value`); `WITHOUT ID` prints only the expr |
| `TASK` | no fields | **rows are tasks**, task fields usable unprefixed; the only file-mutating type (checkbox toggling) |
| `CALENDAR expr` | required date expr | month calendar; SORT/GROUP BY accepted but no-ops |

### §8.2 Data commands

- **FROM sources:** `#tag` (**includes subtags** — matches Slate's shipped nested-tag semantics), `"folder"` (recursive; no trailing slash), `"path/to/file"` (folder wins ties; disambiguate with `.md`), `[[note]]` (pages linking **to** note), `outgoing([[note]])` (pages linked **from** note), `[[]]`/`[[#]]` (current file). Combinators `and`/`or` (case-insensitive in the wild) + parentheses; negation documented in **both** spellings `-#tag` and `!#tag`. **No CSV source exists in documented DQL** (`dv.io.csv` is JS-only).
- `WHERE expr` (repeatable ⇒ AND), `SORT e1 [ASC|ASCENDING|DESC|DESCENDING], e2 …` (ties cascade), `GROUP BY expr [AS name]` (one row per key with exactly two props: the key + the **`rows` array**; swizzling `rows.field` → per-group arrays; grouped-LIST key readable as `key`), `FLATTEN expr [AS name]` (one row per array element; also the let-binding idiom `FLATTEN (…) AS x`), `LIMIT n`.

### §8.3 Expressions

Literals: numbers, `true`/`false`, `"strings"`, `[[links]]`, `[1,2,3]`, `{a: 1}`, `null` (implied), **date shorthands** `date(today|now|tomorrow|yesterday|sow|eow|som|eom|soy|eoy)` and `date(2021-11-11)`, **durations** `dur(1 s 2 m 3 h)` with rich unit aliases (`s/sec/…`, `m/min/…`, `h`, `d`, `w`, `mo/month`, `yr/year`), **lambdas** `(x) => expr` (map/filter/all/any/none/minby/maxby). Operators: arithmetic `+ - * / %`; comparison `> < = != <= >=` — **equality is single `=`**; string `+` concat and `a * n` repeat; boolean infix `AND`/`OR` + prefix `!`. **Null gotcha documented**: `null <= date(today)` is *true*; guards are truthiness or `typeof(x) = "date"`. Field access: bare name (sanitized: lowercase, spaces→`-`), `a.b`, `a[expr]` (0-indexed), `row["where"]` for keyword-named fields, **link indexing** `[[Page]].field`, and `this.` = the page containing the query.

### §8.4 Implicit fields

Pages (all under `file.`): `name, folder, path, ext, link, size, ctime, cday, mtime, mday, tags` (subtags broken down per level), `etags` (explicit only), `inlinks, outlinks, aliases, tasks, lists, frontmatter, day` (from `yyyy-mm-dd` filenames), `starred`. Tasks (top-level in TASK queries; inherit all page fields): `status` (raw char), `checked` (non-space), `completed` (`x` on this task), `fullyCompleted` (incl. subtasks), `text, visual, line, lineCount, path, section, tags, outlinks, link, children, task, annotated, parent, blockId`, plus shorthand-derived dates `due` 🗓, `completion` ✅, `created` ➕, `start` 🛫, `scheduled` ⏳. **No `priority` field** — Tasks-plugin priority/recurrence shorthands are explicitly unsupported by Dataview (Slate's index *does* store priority; the delta runs the other way).

### §8.5 Function inventory (complete)

Constructors: `object, list/array, date(any), date(text, luxonFmt), dur, number, string, link(path,[display]), embed, elink, typeof`. Numeric: `round(n,[digits]), trunc, floor, ceil, min, max, sum, product, reduce(array, "+|-|*|/|&||"), average, minby, maxby` (empty-array aggregates ⇒ null). Containers: `contains, icontains, econtains, containsword, extract, sort, reverse, length, nonnull, firstvalue, all, any, none, join(array,[delim=", "]), filter, unique, map, flat(array,[depth=1]), slice`. Strings: `regextest, regexmatch` (whole-string), `regexreplace, replace` (literal, all), `lower, upper, split` (regex delimiter; capture groups spliced), `startswith, endswith, padleft, padright, substring, truncate`. Utility: `default/ldefault, display, choice(bool,l,r), hash(seed,[text],[variant]), striptime, dateformat` (Luxon; returns a *string*), `durationformat, currencyformat, localtime, meta(link)` (`.display/.embed/.path/.subpath/.type`). Most functions vectorize over lists element-wise. Functions are legal everywhere **except FROM**.

### §8.6 Inline queries

Inline code spans with a settings-configurable prefix (default `=`): `` `= this.file.name` `` — one value, no query types or data commands. Out of N's v1 scope (reserved N-E1 remainder); block queries are the migration surface.
