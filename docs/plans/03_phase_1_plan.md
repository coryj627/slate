# Phase 1 Plan — Accessible Vault MVP

**Phase goal:** Build a usable accessible app that opens existing Obsidian-style vaults, preserves data, and implements the core workflows natively.  
**Recommended duration:** 16–24 weeks, depending on team size and parallelization.  
**Primary output:** A private alpha usable with demo and real-but-backed-up vaults.

---

## 1. Phase 1 scope statement

Phase 1 should produce a native Flutter application that supports:

- opening local Obsidian-style vaults;
- reading/writing Markdown notes;
- parsing and editing frontmatter/properties;
- file tree, command palette, quick switcher, search, backlinks, outline, tags;
- metadata index and link resolver;
- task index and task review;
- template/capture workflows;
- Kanban-like Markdown-backed boards;
- basic Dataview-like queries through Bases/query views;
- `.base` files and accessible Bases table/list views;
- visual Graph MVP with accessible controls and selected-node details;
- safe LiveSync detection and coexistence warning;
- local history/File Recovery equivalent;
- accessibility testing across screen readers and keyboard-only interaction.

Phase 1 should **not** promise full Obsidian plugin compatibility, official Obsidian Sync compatibility, or universal CodeMirror plugin support.

---

## 2. Workstream 1 — App shell and navigation

### Features

- Vault picker and recent vaults.
- Main layout: file tree, editor area, side panels, status bar.
- Command palette.
- Quick switcher.
- Settings screen.
- Keyboard shortcut manager.
- Screen-reader-friendly focus management.
- Voice-control-friendly labels.

### Accessibility requirements

- Every command reachable through command palette.
- Every visible interactive control has an accessible name.
- File tree supports expand/collapse, open, rename, move, delete, reveal, and context actions by keyboard.
- No operation requires hover.
- Context changes require explicit action or clear announcement.

### Deliverables

- `AppShell`
- `VaultPicker`
- `CommandService`
- `CommandPaletteView`
- `QuickSwitcherView`
- `SettingsView`
- `KeyboardShortcutService`

---

## 3. Workstream 2 — Vault engine

### Features

- Open local folder as vault.
- Scan files and folders.
- Read/write Markdown.
- Read binary attachments.
- Create, rename, move, delete files.
- Preserve unknown files and config.
- Detect `.obsidian` folder.
- Detect external sync markers and LiveSync plugin config.

### Data model

```text
Vault
  id
  rootPath
  configPath
  files
  folders
  ignoredPatterns

VaultFile
  path
  name
  extension
  size
  ctime
  mtime
  hash
  type
```

### Acceptance criteria

- Opens the demo vault.
- Preserves unknown `.obsidian` plugin folders.
- Handles hidden folders safely.
- Does not corrupt vault on failed open.
- Supports undo or backup for destructive file actions.

---

## 4. Workstream 3 — Markdown editor and renderer

### Features

- Plain-text Markdown editor.
- Optional preview pane.
- Headings, links, lists, code fences, tables, blockquotes, callouts, comments, tags, embeds.
- Keyboard navigation by heading, link, block, task, and property.
- Markdown link insertion and autocomplete.
- YAML frontmatter editor bridge.

### Accessibility requirements

- Screen reader can edit text reliably.
- User can navigate headings and links without visual scanning.
- Cursor/selection behavior is predictable.
- Markdown formatting commands announce results or maintain context.

### Implementation note

A native Flutter text editor may be more accessible than embedding CodeMirror, but it will reduce immediate compatibility with CodeMirror-based plugins. This tradeoff should be accepted for Phase 1.

---

## 5. Workstream 4 — Metadata index

### Features

- Parse headings, sections, links, embeds, tags, frontmatter, aliases, blocks, lists, tasks, and footnotes.
- Resolve wikilinks and Markdown links.
- Maintain resolved/unresolved link maps.
- Maintain backlink/outgoing-link indexes.
- Incremental reindex on file changes.
- Persist cache in local database.

### Storage proposal

Use SQLite or another explicit local database rather than opaque IndexedDB. Suggested tables:

```text
files
file_metadata
headings
sections
properties
links
embeds
tags
blocks
tasks
footnotes
metadata_cache_status
```

### Acceptance criteria

- Backlinks match expected demo vault relationships.
- Outline matches headings.
- Tags include inline and frontmatter tags.
- Properties parse common YAML values.
- Link resolver handles aliases, folders, headings, and block IDs.

### Evidence basis

Obsidian's official docs state metadata cache powers Graph and Outline. The bundle trace and IndexedDB artifacts confirm a worker/cache/IndexedDB model in Obsidian; the replacement should implement an explicit, accessible, inspectable equivalent. See O1, F1, and F3.

---

## 6. Workstream 5 — Properties and frontmatter

### Features

- Structured property editor.
- YAML frontmatter parse/write.
- Property types: text, number, checkbox, date, datetime, list, link, tags.
- Property rename.
- Property suggestions.
- Validation and repair prompts.

### Accessibility requirements

- Property editor behaves as a clear form.
- Errors are announced.
- Users can move through properties by keyboard and screen reader.
- Bulk edit actions require confirmation and provide undo/backup.

---

## 7. Workstream 6 — Search, backlinks, outline, tags

### Features

- Full-text search.
- Property filters.
- Tag filters.
- Backlinks and unlinked mention candidates.
- Outgoing links.
- Outline navigation.
- Tags view.

### Acceptance criteria

- Search results are exposed as a list/table with file, heading/line, snippet, and actions.
- Backlinks include source note, heading/context, and link target.
- Outline supports keyboard navigation and section movement if implemented.

---

## 8. Workstream 7 — Bases MVP

### Features

- `.base` parser and writer.
- Embedded base support if feasible.
- Table and list views.
- Global and view filters.
- Sort and limit.
- Group by one property.
- Note/file/formula properties.
- Summary row for common summaries.
- Property editor integration.

### Accessible table requirements

- Announce row/column headers.
- Support cell-by-cell navigation.
- Support sort/filter commands.
- Support row actions: open note, edit property, copy link, show backlinks, show local graph.
- Support export to CSV/Markdown.

### Compatibility goal

The `.base` storage should remain compatible enough that a user can reopen the vault in Obsidian without losing Base definitions. Obsidian Bases docs define valid YAML syntax for filters, formulas, views, summaries, and property references. See O2 and O3.

---

## 9. Workstream 8 — Graph MVP

### Features

- Graph data service from metadata index.
- Global graph.
- Local graph with depth.
- Filters: search, tag, folder, attachments, orphans, unresolved, existing-only.
- Groups: tag, folder, property, saved query.
- Node sizing.
- Directed edge arrows.
- Pan/zoom/select/open.
- Persist graph settings.
- Accessible selected-node panel.

### Graph data model

```text
GraphNode
  id
  path
  displayName
  aliases
  tags
  folder
  fileType
  inDegree
  outDegree
  totalDegree
  createdAt
  modifiedAt
  groupIds
  visible
  selected
  x
  y

GraphEdge
  id
  sourcePath
  targetPath
  linkType
  resolved
  count
  sourcePositions
  weight
  directed
```

### Accessibility groundwork

Even if the visual graph is not fully screen-reader-accessible in Phase 1, ship:

- graph filter controls accessible by keyboard/screen reader;
- selected node details as accessible side panel;
- node list/table;
- graph stats summary;
- commands for open selected node, show backlinks, show local graph.

### Implementation options

- Prototype with `graphview` or `force_directed_graphview`.
- Prefer an owned graph data/layout layer and Flutter `CustomPainter` renderer for long-term control.
- Use `CustomPainter.semanticsBuilder` later for semantic nodes where practical. See FL2, G1, and G2.

---

## 10. Workstream 9 — Built-in golden workflows

### Tasks

- Parse Markdown tasks.
- Status, due date, scheduled date, recurrence, priority if supported.
- Task queries by date/project/tag/status.
- Accessible task review and batch actions.

### Templates and capture

- Template variables.
- Create note from template.
- Insert template into note.
- Prompted variables.
- Safe macros.

### Kanban-like boards

- Markdown-backed columns and cards.
- Keyboard move commands.
- Screen-reader announcements for moves.
- Compatibility with Kanban plugin frontmatter/settings where feasible.

### Dataview-like queries

- Start with a native query builder and Bases-style views.
- Read common Dataview blocks and either render supported ones or provide migration guidance.

---

## 11. Workstream 10 — Local history and recovery

### Features

- Local snapshots for Markdown, `.canvas`, `.base`, and selected config files.
- Retention settings.
- Restore previous version.
- Compare versions.
- Deleted file recovery if feasible.

### Accessibility requirements

- Version history as table/list.
- Diff summaries readable by screen reader.
- Restore requires confirmation and provides undo where possible.

### Evidence basis

Obsidian File Recovery is observed through bundle and IndexedDB evidence as a local backup database with path/timestamp indexing. See F3.

---

## 12. Workstream 11 — Sync detection and safety

### Phase 1 sync scope

- Detect LiveSync plugin folder and settings.
- Detect other sync-related plugin folders.
- Detect external filesystem sync markers where possible.
- Warn users before enabling any built-in sync writer.
- Preserve sync plugin data.

### Not in Phase 1

- Official Obsidian Sync client.
- Full LiveSync write compatibility.
- Full P2P sync.

### Optional Phase 1 stretch

- LiveSync settings parser and accessible diagnostics.
- Read-only CouchDB connection tester using disposable setup.

---

## 13. Workstream 12 — Testing and release readiness

### Test suites

- Unit tests for parser and metadata index.
- Golden vault tests.
- Bases syntax tests.
- Graph data tests.
- Accessibility tests.
- Keyboard navigation tests.
- Plugin-data preservation tests.
- File operation safety tests.

### Accessibility matrix

| Platform | Assistive tech | Phase 1 target |
|---|---|---|
| Windows | JAWS | Core workflows pass |
| Windows | NVDA | Core workflows pass |
| macOS | VoiceOver | Core workflows pass |
| iOS | VoiceOver | Core mobile workflows pass |
| Android | TalkBack | Core mobile workflows pass |
| All | Keyboard-only | Full MVP operable |
| Windows/macOS | Voice control | Controls have stable names |

---

## 14. Phase 1 release gate

Phase 1 should not exit until:

- The app can open the demo vault and preserve it.
- Markdown editing is usable with at least one desktop screen reader and one mobile screen reader.
- File explorer, command palette, search, backlinks, properties, Bases table, and selected-node Graph panel are accessible.
- Visual Graph is credible to sighted users for small/medium vaults.
- Bases table/list can represent real project/task/dashboard data.
- Local history works.
- Sync detection prevents double-sync hazards.
- Unknown plugin data is preserved.


## Source register used across these planning documents

### Official Obsidian sources

- **O1 — Obsidian Help: Data storage**: <https://obsidian.md/help/data-storage>
  - Establishes local Markdown vaults, `.obsidian` vault settings, global settings locations, IndexedDB use, and metadata-cache role.
- **O2 — Obsidian Help: Bases introduction**: <https://obsidian.md/help/bases>
  - Establishes Bases as a core plugin for database-like views over Markdown files and properties, with table/list/cards/map views and `.base` or embedded syntax storage.
- **O3 — Obsidian Help: Bases syntax**: <https://obsidian.md/help/bases/syntax>
  - Establishes `.base` YAML syntax, filters, formulas, views, summaries, note/file/formula properties, and file properties such as links, embeds, tags, mtime, ctime, size, path, and backlinks.
- **O4 — Obsidian Help: Graph view**: <https://obsidian.md/help/plugins/graph>
  - Establishes the visual graph model: nodes as notes/files, lines as internal links, node sizing from references, hover/click behavior, filters, groups, local graph, arrows, forces, and display settings.
- **O5 — Obsidian API README**: <https://github.com/obsidianmd/obsidian-api/blob/master/README.md>
  - Establishes the public plugin architecture around `App`, `Vault`, `Workspace`, `MetadataCache`, commands, events, settings, views, and plugin data.
- **O6 — Obsidian Help: Plugin security**: <https://obsidian.md/help/plugin-security>
  - Establishes that community plugins run third-party code, inherit Obsidian's access, and cannot be reliably permission-restricted by Obsidian.
- **O7 — Obsidian Help: Local and remote vaults**: <https://obsidian.md/help/sync/vault-types>
  - Establishes official Sync's local/remote vault model, file-level synchronization, offline behavior, and remote-vault concept.
- **O8 — Obsidian Help: Sync security**: <https://obsidian.md/help/sync/security>
  - Establishes documented official Sync encryption primitives and behavior.

### User-provided reverse-engineering artifacts

- **F1 — `OBSIDIAN_BUNDLE_TRACE.md`**
  - Establishes unpacked macOS Electron package shape, two ASAR payloads, version 1.12.7, absence of usable source maps, and high-value files: `Resources/app/main.js`, `Resources/obsidian/main.js`, `Resources/obsidian/app.js`, and `Resources/obsidian/worker.js`.
- **F2 — extracted `package.json`**
  - Establishes `obsidian-dev`, version 1.12.7, `license: UNLICENSED`, `private: true`.
- **F3 — IndexedDB LevelDB files: `LOG`, `000003.log`, `CURRENT`, `MANIFEST-000001`**
  - Establishes global IndexedDB origin path and observed vault-specific `*-cache` and `*-backup` databases, with metadata cache and File Recovery evidence.
- **F4 — `obsidian_demo_vault_evidence.md`**
  - Establishes observed demo vault state: Markdown files, `.obsidian` config, plugin folders, workspace serialization, graph config, hotkeys, command palette pins, Dataview/Kanban/QuickAdd patterns.

### Open-source plugin case studies

- **P1 — Dataview**: <https://github.com/blacksmithgu/obsidian-dataview>
- **P2 — Tasks**: <https://github.com/obsidian-tasks-group/obsidian-tasks>
- **P3 — Kanban**: <https://github.com/obsidian-community/obsidian-kanban>
- **P4 — Templater**: <https://github.com/SilentVoid13/Templater>
- **P5 — Self-hosted LiveSync**: <https://github.com/vrtmrz/obsidian-livesync>
- **P6 — LiveSync settings docs**: <https://github.com/vrtmrz/obsidian-livesync/blob/main/docs/settings.md>

### Flutter and graph implementation references

- **FL1 — Flutter `Semantics` API**: <https://api.flutter.dev/flutter/widgets/Semantics-class.html>
- **FL2 — Flutter `CustomPainter.semanticsBuilder` API**: <https://api.flutter.dev/flutter/rendering/CustomPainter/semanticsBuilder.html>
- **FL3 — Flutter accessibility checklist**: <https://docs.flutter.dev/ui/accessibility>
- **G1 — Flutter `graphview` package**: <https://pub.dev/packages/graphview>
- **G2 — Flutter `force_directed_graphview` package**: <https://pub.dev/packages/force_directed_graphview>
