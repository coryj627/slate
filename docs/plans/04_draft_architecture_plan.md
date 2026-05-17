# Draft Architecture Plan — Accessible Obsidian-Compatible Flutter Application

**Purpose:** Define a clean architecture for an accessible-first Flutter app that can open Obsidian-style vaults and support native accessible equivalents for core and plugin workflows.

---

## 1. Architectural summary

The system should be built around a renderer-agnostic vault intelligence layer:

```text
Local vault files
  ↓
Vault service and file watcher
  ↓
Markdown / properties / Canvas / Base parsers
  ↓
Metadata, property, task, link, and graph indexes
  ↓
Query engine and command engine
  ↓
Native accessible Flutter UI renderers
  ↓
Optional plugin compatibility adapters and sync adapters
```

This deliberately mirrors the useful parts of Obsidian's public architecture while avoiding copying the proprietary implementation. Obsidian public docs establish local Markdown vaults, `.obsidian` config, IndexedDB-backed metadata cache, and plugin APIs around `Vault`, `Workspace`, and `MetadataCache`; our implementation should expose similar concepts internally and through a future compatibility shim. See O1 and O5.

---

## 2. Major subsystems

| Subsystem | Responsibility | Phase 1 status |
|---|---|---|
| App shell | Windows, navigation, command palette, quick switcher, settings, focus routing. | Required |
| Vault service | Open/read/write/watch local vault files. | Required |
| Config service | Read/preserve selected `.obsidian` files and app settings. | Required |
| Markdown parser | Extract headings, sections, links, tags, embeds, frontmatter, tasks, blocks. | Required |
| Metadata index | Store derived metadata and link maps. | Required |
| Property index | Normalize frontmatter/properties and file properties. | Required |
| Query engine | Drive Bases, search, dashboards, Dataview-like workflows. | Required |
| Bases engine | Parse `.base`, evaluate filters/formulas, render table/list/cards. | Required |
| Graph engine | Build graph nodes/edges and filter/group/layout them. | Required |
| Editor | Native accessible Markdown editing. | Required |
| Built-in plugin equivalents | Tasks, templates, Kanban-like boards, capture, query dashboards. | Required/partial |
| Sync adapters | Detect sync providers, local history, later LiveSync-compatible sync. | Detection required, full sync later |
| Plugin compatibility runtime | JS runtime and Obsidian API shim. | Later |
| Accessibility services | Semantics, focus, keyboard, screen-reader announcements, voice-control labels. | Required |

---

## 3. Layered architecture

### 3.1 UI and accessibility layer

```text
Flutter widgets
  - Semantics annotations
  - Focus traversal
  - Actions and Shortcuts
  - Command palette integration
  - Screen-reader announcements
  - Voice-control labels
```

Flutter's `Semantics` widget annotates the widget tree with descriptions used by assistive technologies, and Flutter's accessibility guidance recommends screen-reader testing, contrast, target sizes, undoable errors, and large-scale-factor support. See FL1 and FL3.

### 3.2 Command layer

Every user-visible operation should be a command:

```text
Command
  id
  title
  description
  category
  defaultHotkeys
  canExecute(context)
  execute(context)
  accessibilityLabel
  telemetrySafeName
```

This supports keyboard-only users, command palette discoverability, voice control, automation, and plugin compatibility.

### 3.3 Vault layer

The vault layer abstracts local file access.

```text
VaultService
  openVault(path)
  scan()
  readText(path)
  writeText(path, content)
  readBinary(path)
  writeBinary(path, bytes)
  create(path)
  rename(oldPath, newPath)
  move(oldPath, newPath)
  delete(path, trash=true)
  watch(events)
```

It should preserve unknown files and not assume `.obsidian` is owned exclusively by this app.

### 3.4 Metadata layer

```text
MetadataService
  parseFile(file)
  getFileCache(path)
  getResolvedLinks(path)
  getUnresolvedLinks(path)
  getBacklinks(path)
  getOutgoingLinks(path)
  getTags()
  rebuildCache()
```

Suggested database tables:

```sql
files(id, path, name, ext, folder, size, ctime, mtime, hash, type)
markdown_metadata(file_id, frontmatter_json, aliases_json, cssclasses_json, parse_hash)
headings(file_id, level, text, line, column, offset)
sections(file_id, type, start_line, end_line, start_offset, end_offset)
links(file_id, raw, target, display, subpath, resolved_path, line, offset, link_type)
embeds(file_id, raw, target, resolved_path, line, offset)
tags(file_id, tag, source, line, offset)
blocks(file_id, block_id, line, offset, context)
tasks(file_id, text, status, checked, due, scheduled, priority, recurrence, line, offset)
properties(file_id, key, value_json, type, source)
```

Obsidian's own metadata cache powers Graph and Outline, and the user-provided IndexedDB files showed observed `*-cache` and `*-backup` databases in Obsidian's global app storage. See O1 and F3.

---

## 4. Bases architecture

### 4.1 Data flow

```text
.base YAML / embedded base code block
  ↓
Base parser
  ↓
Base definition
  ↓
Query planner
  ↓
Property and file index
  ↓
Formula/filter evaluator
  ↓
Result set
  ↓
Native renderers: table, list, cards, map later
```

### 4.2 Base definition model

```text
BaseDefinition
  id
  path
  globalFilters
  formulas
  propertyConfig
  summaries
  views[]

BaseView
  name
  type
  filters
  order
  sort
  groupBy
  summaries
  limit
  viewSpecificConfig
```

### 4.3 Formula/filter engine

Support:

- arithmetic operators;
- comparison operators;
- boolean operators;
- string/number/boolean/date values;
- file properties;
- note properties;
- formula properties;
- list contains and link equality where feasible.

Bases syntax docs define valid YAML, filters, formulas, summaries, view fields, note/file/formula properties, and file properties including `file.links`, `file.embeds`, `file.tags`, `file.path`, `file.mtime`, `file.ctime`, `file.size`, and `file.backlinks`. See O3.

---

## 5. Graph architecture

### 5.1 Graph data service

```text
GraphService
  buildGlobalGraph(filters)
  buildLocalGraph(centerPath, depth, filters)
  getNode(path)
  getEdges(path)
  getClusters(strategy)
  computeMetrics()
```

### 5.2 Graph query/filter service

```text
GraphFilter
  searchQuery
  includeTags
  excludeTags
  folderFilters
  showAttachments
  showExistingOnly
  showOrphans
  showUnresolved
  groups[]
```

### 5.3 Graph layout service

```text
GraphLayout
  algorithm: force | radial | circular | hierarchical | clustered
  positions
  seed
  cachedAt
  graphHash
```

### 5.4 Renderers

| Renderer | Phase | Purpose |
|---|---:|---|
| Visual graph canvas | 1 | Sighted adoption and Obsidian-like experience. |
| Accessible selected-node panel | 1 | Immediate partial utility for screen-reader users. |
| Graph table | 2 | Sortable list of nodes and metrics. |
| Relationship explorer | 2 | Active-note in/out links, related notes, clusters. |
| Path finder | 2 | Explain graph paths nonvisually. |
| Cluster explorer | 2 | Browse groups as structured lists. |

### 5.5 Flutter implementation notes

- Start with a package spike using `graphview` or `force_directed_graphview`.
- Move toward an owned layout/rendering layer for performance, customization, and accessibility.
- Use `CustomPainter` for visual rendering and `semanticsBuilder` where node-level semantics are useful. See FL2.
- For large vaults, do not layout all nodes on every frame; use clustering, filtering, local graph defaults, cached layouts, and progressive rendering.

---

## 6. Editor architecture

### Goals

- Native accessible editing first.
- Markdown text remains the source of truth.
- Rich features are command-driven, not mouse-dependent.

### Components

```text
EditorDocument
EditorSelection
EditorCommandService
MarkdownAutocomplete
LinkAutocomplete
PropertyEditorBridge
PreviewRenderer
```

### Key tradeoff

Embedding CodeMirror would improve compatibility with Obsidian editor plugins but may undermine accessibility. Phase 1 should prefer a native Flutter editor and defer CodeMirror compatibility.

---

## 7. Built-in workflow modules

### Tasks module

```text
TaskIndex
TaskQuery
TaskReviewView
TaskCommands
```

### Templates/capture module

```text
TemplateEngine
VariableResolver
CapturePrompt
MacroRunner
```

### Kanban module

```text
BoardParser
BoardSerializer
BoardView
ColumnList
CardList
MoveCommands
```

### Query/dashboard module

```text
QueryParser
QueryBuilder
ResultRenderer
SavedQuery
```

These should be native accessible features, not plugin dependencies.

---

## 8. Plugin compatibility architecture

### 8.1 Long-term runtime shape

```text
Plugin JS bundle
  ↓
Sandboxed JavaScript runtime
  ↓
Obsidian API shim
  ↓
Dart bridge
  ↓
Vault / Metadata / Workspace / Commands / Views / Settings
```

### 8.2 Compatibility tiers

| Tier | Plugin type | Strategy |
|---|---|---|
| 1 | Data/command plugins | API shim candidate. |
| 2 | Metadata/query plugins | Native equivalents plus API shim. |
| 3 | Custom views | Native adapters first, generic compatibility later. |
| 4 | Editor extensions | Defer; CodeMirror dependency conflict. |
| 5 | Node/Electron plugins | Desktop-only, explicit permissions, likely unsupported early. |

### 8.3 Plugin permissions

Unlike Obsidian's broad plugin trust model, the replacement should implement explicit permissions:

- vault read;
- vault write;
- network;
- shell/system command;
- external file access;
- local database;
- UI view registration;
- editor modification.

Obsidian's plugin security docs warn that community plugins inherit Obsidian's access and cannot be reliably restricted; the replacement should improve this model. See O6.

---

## 9. Sync architecture

### 9.1 Sync adapter model

```text
SyncProvider
  id
  displayName
  detect(vault)
  configure(settings)
  status()
  pull()
  push()
  resolveConflict(conflict)
```

### 9.2 Initial providers

| Provider | Phase | Scope |
|---|---:|---|
| External filesystem sync | 1 | Detect and avoid conflicts. |
| Local history/recovery | 1 | Snapshot and restore. |
| LiveSync detector | 1 | Detect plugin config and warn. |
| Git adapter | 2/3 | Optional open sync. |
| LiveSync-compatible CouchDB | 3 | Native adapter. |
| LiveSync-compatible object storage | 4 | Optional. |
| Official Obsidian Sync | Not planned | Only with permission/public API. |

### 9.3 LiveSync compatibility path

1. Parse settings.
2. Connect read-only to disposable CouchDB.
3. Enumerate remote docs/chunks.
4. Validate encryption/decryption with test data.
5. Pull-only into isolated vault.
6. Push with conflict detection.
7. Full two-way sync.

LiveSync is open-source and supports CouchDB, object storage, and WebRTC. See P5 and P6.

---

## 10. Persistence architecture

### Local app database

Use a clear local database, likely SQLite, rather than opaque browser IndexedDB.

```text
app_state
vaults
workspace_layouts
metadata_cache
query_cache
graph_layouts
history_snapshots
sync_state
```

### Vault-local config

Read and preserve:

```text
.obsidian/app.json
.obsidian/appearance.json
.obsidian/core-plugins.json
.obsidian/community-plugins.json
.obsidian/workspace.json
.obsidian/workspaces.json
.obsidian/hotkeys.json
.obsidian/graph.json
.obsidian/plugins/*/manifest.json
.obsidian/plugins/*/data.json
```

The demo vault showed these files and plugin folders in practice. See F4.

---

## 11. Accessibility architecture

### Core services

```text
AccessibilityLabelService
FocusService
AnnouncementService
KeyboardNavigationService
VoiceControlNameService
SemanticsAuditService
```

### Design rules

- All actions are commands.
- All controls have stable labels.
- All complex visuals have structured side panels or alternate views.
- Tables are real navigable data grids, not visual-only grids.
- Graph and Canvas expose structured data even before full nonvisual rendering.
- Important actions are undoable.
- No context changes while typing without explicit user action.

### Test harness

- Manual screen-reader scripts.
- Golden focus-order tests.
- Semantics tree inspection.
- Keyboard-only workflow tests.
- Voice-control label checks.

---

## 12. Security and safety architecture

### Data safety

- Open vault in read-only mode first.
- Create backups before destructive bulk operations.
- Preserve unknown files.
- Warn on concurrent sync providers.
- Keep local history.

### Plugin safety

- No arbitrary plugin execution in Phase 1.
- Native built-ins for golden workflows.
- Future plugin runtime uses permissions and sandboxing.

### Sync safety

- Never run built-in sync writer while LiveSync/other sync writer is active unless explicitly configured.
- Default to pull-only/read-only diagnostics for LiveSync compatibility spikes.
- Treat encryption compatibility as a separately tested milestone.

---

## 13. Suggested repository structure

```text
/app
  /lib
    /accessibility
    /app_shell
    /commands
    /vault
    /config
    /markdown
    /metadata
    /properties
    /search
    /bases
    /graph
    /editor
    /tasks
    /templates
    /kanban
    /sync
    /history
    /plugins
    /testing
  /test
  /golden_vaults
  /docs
```

---

## 14. Architecture decision records to write

- ADR-001: Native Flutter editor vs. embedded CodeMirror.
- ADR-002: SQLite/local database choice.
- ADR-003: Metadata parser implementation language.
- ADR-004: Graph renderer strategy.
- ADR-005: Bases formula evaluator strategy.
- ADR-006: Plugin compatibility runtime timing.
- ADR-007: LiveSync-compatible adapter vs. running LiveSync plugin.
- ADR-008: Clean-room source-use policy.
- ADR-009: Accessibility definition of done.


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
