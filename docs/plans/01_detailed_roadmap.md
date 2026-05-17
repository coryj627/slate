# Accessible Obsidian-Compatible Flutter App — Detailed Product Roadmap

**Working title:** Accessible local-first knowledge workspace  
**Generated:** 2026-05-17  
**Primary goal:** Build an accessible-first Flutter application that can open and preserve existing Obsidian vaults, provide native accessible replacements for the highest-value Obsidian workflows, and incrementally support Obsidian plugin-compatible behavior where it is technically and legally safe.

---

## 1. Strategic thesis

The product should not attempt to clone Obsidian's proprietary implementation. It should reimplement an interoperable, accessible, local-first vault client around public and user-owned artifacts:

- Markdown notes and attachments in a local vault.
- `.obsidian` settings and plugin folders where compatibility is useful.
- `.canvas` and `.base` files as durable local formats.
- A native metadata, property, link, task, graph, and query engine.
- Accessible native UI instead of inaccessible DOM/plugin UI.
- Built-in replacements for the most important plugin workflows.
- Optional compatibility adapters for selected Obsidian plugins and open sync systems.

Obsidian's public docs establish the local Markdown vault, `.obsidian` config folder, global app storage, IndexedDB backend storage, and metadata cache. The bundle artifacts establish that the core app is closed-source/unlicensed from an OSS standpoint, so the safest approach is clean-room interoperability based on public docs, open-source plugins, and observed user-owned vault files. See O1, F1, and F2.

---

## 2. Product principles

1. **Accessibility is a core feature, not a mode.** Every primary workflow must work with screen readers, keyboard-only navigation, and voice control.
2. **Data compatibility comes before UI compatibility.** Opening, editing, preserving, and exporting an Obsidian vault is more important than copying every UI detail.
3. **Built-in native workflows beat plugin dependence.** The top workflows should be first-class accessible functionality, with compatibility to equivalent Obsidian plugin data where practical.
4. **Visual features must have structured equivalents.** Graph and Canvas can ship visually first, but their data models must support later accessible renderers.
5. **Safe interoperability over reverse-engineered dependency.** Avoid official Obsidian Sync protocol dependence unless permission or public API access exists.
6. **Incremental plugin compatibility.** Build a measured API shim and native adapters for selected plugins instead of promising universal plugin support.
7. **Open sync first.** Support filesystem sync, Git-style sync, and LiveSync-compatible self-hosted workflows before any proprietary service integration.

---

## 3. Roadmap overview

| Phase | Name | Goal | Exit criteria |
|---:|---|---|---|
| **0** | Discovery, validation, and clean-room specification | Convert research into product requirements, compatibility specs, architecture decisions, risk register, and proof-of-concept spikes. | Validated architecture, vault compatibility matrix, accessibility acceptance matrix, plugin-corpus analysis, Bases/Graph spikes, LiveSync read-only spike plan. |
| **1** | Accessible vault MVP | Build the first usable app: open/edit vaults, metadata index, search, links/backlinks, properties, basic Bases, visual Graph MVP, built-in Tasks/Templates/Kanban-like workflows, and accessible app shell. | A user can open a demo Obsidian vault, navigate and edit notes accessibly, run key workflows, and preserve Obsidian-compatible files. |
| **2** | Built-in golden plugin parity | Implement native accessible equivalents for the top plugin workflows: Dataview-like queries, Tasks, Kanban, Templater/QuickAdd, Calendar/Periodic Notes, advanced search, property editing. | Golden plugin test vaults pass behavior parity tests for selected workflows. |
| **3** | Sync and collaboration adapters | Add filesystem sync detection, Git adapter, LiveSync-compatible CouchDB adapter, conflict resolver, and File Recovery/history equivalent. | App can safely sync a disposable vault through open mechanisms and resolve conflicts accessibly. |
| **4** | Plugin compatibility runtime | Implement a JavaScript runtime and Obsidian API shim for selected plugin classes. | Selected data/command plugins run under the shim without DOM dependence; unsupported plugin types fail safely. |
| **5** | Advanced accessibility and ecosystem maturity | Add accessible Graph navigator, Canvas outline, richer Bases formulas/views, plugin marketplace compatibility, audit tooling, and beta/VPAT readiness. | Accessibility test matrix passes across JAWS, NVDA, VoiceOver, TalkBack, keyboard, and voice-control scenarios. |

---

## 4. MVP definition

The MVP should be an accessible, local-first Flutter app that:

- Opens an existing Obsidian-style vault.
- Preserves Markdown files, attachments, `.canvas`, `.base`, and `.obsidian` files it does not understand.
- Provides accessible native navigation: file explorer, command palette, quick switcher, tabs/workspaces, search, and settings.
- Edits Markdown and YAML frontmatter/properties.
- Builds a metadata cache from headings, links, embeds, tags, aliases, blocks, tasks, lists, and frontmatter.
- Provides backlinks, outgoing links, outline, tags, properties, and search.
- Provides a basic Bases implementation: `.base` parse/save, table/list views, filters, sort, grouping, summaries, and property editing.
- Provides a visually competitive Graph MVP: global graph, local graph, filters, groups, pan/zoom, open-note actions, node sizing, and saved graph settings.
- Lays the groundwork for accessible Graph equivalents: node table, relationship explorer, path finder, and cluster explorer.
- Includes built-in native versions of high-value plugin workflows: tasks, templates, capture macros, Kanban-like boards, and Dataview-like queries.
- Detects LiveSync configuration and can safely coexist with it; full LiveSync-compatible sync comes later.

---

## 5. Golden built-in plugin/workflow list

The top plugin workflows should be implemented as native accessible features first. Compatibility with corresponding Obsidian plugins should be maintained at the data/config level where practical.

| Priority | Workflow / plugin equivalent | Native feature | Compatibility target |
|---:|---|---|---|
| 1 | Dataview | Query views over files, properties, links, tasks, and graph metrics. | Parse common Dataview query blocks or provide migration helpers. |
| 2 | Tasks | Vault-wide task index, due dates, recurrence, filters, review views. | Read Markdown task syntax and common Tasks plugin metadata. |
| 3 | Kanban | Accessible board/list view with columns, cards, move commands, and Markdown serialization. | Preserve Kanban plugin frontmatter and board Markdown where possible. |
| 4 | Templater | Safe template variables, note creation, insertion, and macros. | Support common template placeholders and safe script subset. |
| 5 | QuickAdd | Capture workflows, macros, prompts, and note creation recipes. | Import or translate simple QuickAdd choices from plugin data. |
| 6 | Calendar / Periodic Notes | Daily, weekly, monthly note workflows. | Preserve date-format conventions and folder settings. |
| 7 | Metadata Menu / MetaEdit | Property editor, bulk property edits, validation, rename. | Preserve frontmatter/properties and simple plugin data where feasible. |
| 8 | Omnisearch / advanced search | Full-text plus metadata search, saved queries, filters. | Native search engine first; plugin compatibility later. |
| 9 | LiveSync / Obsidian Git | Open sync adapters. | Detect configs first; implement LiveSync-compatible CouchDB and/or Git later. |
| 10 | Canvas/Excalidraw-like workflows | Accessible visual/structured diagram and board tooling. | Preserve `.canvas`; external Excalidraw compatibility is later/non-MVP. |

---

## 6. Bases roadmap

Bases should be treated as a core data platform, not a secondary feature.

### Phase 1 Bases MVP

- Parse and save `.base` YAML files.
- Support embedded bases where technically straightforward.
- Support global and view-level filters.
- Support table and list views.
- Support basic cards view if time allows.
- Support note properties, file properties, and formula properties.
- Support sort, simple groupBy, and result limit.
- Support default summaries such as count, filled, empty, unique, min, max, sum, average, earliest, and latest.
- Provide accessible filter builder and keyboard data-grid navigation.

### Phase 2 Bases expansion

- Formula engine parity with common Obsidian functions.
- Cards and map views.
- Custom saved views.
- Export CSV/Markdown.
- Graph visualization for Base result sets.
- Query templates for projects, goals, tasks, people, reading lists, and orphan notes.

### Phase 3 Bases ecosystem

- Plugin-added/custom views through a native extension API.
- Dataview query migration layer.
- Graph-derived fields in Bases: `file.inDegree`, `file.outDegree`, `file.isOrphan`, `file.cluster`, `file.shortestPathTo(...)`.

Official Bases docs establish database-like views over Markdown and properties, `.base` YAML syntax, filters, formulas, summaries, file/note/formula properties, and table/list/cards/map layouts. See O2 and O3.

---

## 7. Graph roadmap

Graph is central for sighted adoption and must be visually credible early. Accessibility can mature later, but the data model must be accessible from day one.

### Phase 1 visual Graph MVP

- Global graph and local graph.
- Nodes for files/notes and edges for internal links.
- Node size by degree or inbound references.
- Filters by search, tag, folder, attachment, orphan, unresolved, existing-only.
- Groups by tag, folder, property, saved query.
- Pan/zoom/select/open note.
- Context actions: open, copy link, local graph, backlinks, reveal in file explorer.
- Saved graph settings in vault-local config.
- Accessible side panel for selected node details.

### Phase 2 accessible Graph equivalents

- Relationship Explorer for the active note.
- Graph Table sorted by inbound/outbound/degree/tag/folder/modified date.
- Path Finder between two notes.
- Cluster Explorer by tag/folder/property/query.
- Keyboard graph navigator.
- Screen-reader announcements for selected node, visible neighborhood, and graph stats.

### Phase 3 visual and accessible convergence

- CustomPainter semantics nodes where useful.
- Graph presets: orphans, highly connected notes, unresolved links, tag clusters, recent high-degree notes.
- Large-vault clustering and layout caching.
- Graph result sets from Bases and saved queries.

Obsidian Graph docs establish graph relationships as nodes and links, node sizing from references, hover/click interaction, filters, groups, and local graph controls. Flutter supports semantic annotations and custom paint semantic builders, which should be used to make the visual graph progressively accessible. See O4, FL1, and FL2.

---

## 8. Sync roadmap

Official Obsidian Sync should not be an MVP dependency. It is documented at a behavioral level but not as a public client protocol. Open sync is the safer path.

### Phase 1

- Detect vaults managed by external sync providers.
- Detect LiveSync plugin configuration and warn about duplicate sync risk.
- Preserve sync-related plugin data without altering it.
- Provide local File Recovery/history equivalent.

### Phase 2

- Git or filesystem sync adapter.
- LiveSync configuration importer and read-only diagnostics.
- Local conflict model and accessible conflict viewer.

### Phase 3

- LiveSync-compatible CouchDB adapter.
- Encryption compatibility spike and test vectors.
- Pull-only mode, then write/push mode.
- Conflict behavior matching or safely interoperating with LiveSync.

### Phase 4

- Object storage adapter for MinIO/S3/R2 if required.
- WebRTC/P2P only after core sync is stable.

LiveSync is a feasible middle ground because it is open-source, self-hosted, and supports CouchDB, object storage, and experimental WebRTC. It is not official Obsidian Sync. See P5 and P6.

---

## 9. Major risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Full plugin compatibility overwhelms accessibility work. | High | Build native golden workflows first; API shim later. |
| CodeMirror/editor plugins conflict with Flutter-native editor. | High | Support data/command plugins first; editor extensions later or unsupported. |
| Visual Graph eats engineering time. | Medium-high | Build graph engine separately; use existing Flutter graph packages for spikes only. |
| Bases formula compatibility becomes open-ended. | Medium-high | Start with core operators/functions and provide compatibility test fixtures. |
| LiveSync compatibility is more complex than expected. | Medium-high | Start with detection/import/read-only diagnostics; CouchDB first. |
| Official Obsidian Sync temptation creates legal/operational risk. | High | Exclude from MVP; pursue only with public API or permission. |
| Flutter accessibility varies by platform. | Medium-high | Test early with JAWS, NVDA, VoiceOver macOS/iOS, TalkBack, keyboard-only, and voice control. |
| Obsidian data formats evolve. | Medium | Version compatibility matrix, schema tests, migration layer. |

---

## 10. Success metrics

### Accessibility

- 100% of MVP workflows operable by keyboard without hidden mouse-only requirements.
- Screen-reader users can open, edit, search, link, query, and organize notes without sighted assistance.
- Voice-control users can activate all visible controls by stable accessible names.
- No critical action relies solely on color, spatial position, or hover.

### Compatibility

- Opens and preserves a representative Obsidian demo vault.
- Preserves unknown `.obsidian` settings and plugin data unless explicitly migrated.
- Correctly parses common frontmatter, properties, links, embeds, tags, tasks, headings, and blocks.
- Passes golden test vaults for Dataview-like, Tasks-like, Kanban-like, and template workflows.

### Adoption

- Sighted users recognize the Graph and Bases workflows as credible alternatives to Obsidian.
- Blind and keyboard-only users can complete workflows that are blocked or unreliable in Obsidian.
- Existing vault owners can trial the app without destructive migration.


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
