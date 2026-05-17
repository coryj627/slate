# Public Conversation and Research Transcript

**Generated:** 2026-05-17  
**Scope:** This is a public-facing transcript and research log for the Obsidian accessibility/replacement-app planning conversation. It captures the substantive user prompts, assistant responses, research findings, artifacts, and decisions. It does not include private chain-of-thought.

---

## 1. Conversation summary

The conversation began as an investigation into how Obsidian works internally and evolved into a product strategy for an accessible Flutter-based replacement. The target is an accessible local-first app that can open Obsidian vaults, provide native equivalents for top Obsidian/plugin workflows, support visual and future-accessible Graph features, implement Bases closely, and avoid legally gray dependence on proprietary Obsidian Sync.

Key conclusions:

- Obsidian core is closed-source, but the vault data model, plugin API, `.canvas`, `.base`, and many ecosystem components are open or observable.
- A clean-room interoperable app should use public docs, user-owned vault files, open-source plugins, and observed behavior rather than copying proprietary code.
- Full plugin compatibility is not an MVP requirement; the top plugin workflows should be native accessible built-ins.
- Official Obsidian Sync is not a safe MVP target; Self-hosted LiveSync compatibility is a feasible open middle ground.
- Bases should be a first-class MVP data platform.
- Graph should be visually competitive early, but architected with a structured graph model so accessible graph navigation can follow.

---

## 2. Timeline of major turns

### Turn 1 — User requested Obsidian feature/backend breakdown

**User request:** Produce a feature breakdown of the Obsidian MD app with clear title, description, function, backend estimates, citations, and research into open-source elements.

**Assistant response summary:**

- Produced a broad feature inventory covering vaults, Markdown, links, embeds, properties, Bases, Canvas, Search, Quick Switcher, Command Palette, Backlinks, Graph, Outline, Tags, Bookmarks, Page Preview, Daily Notes, Templates, File Recovery, Sync, Publish, Community Plugins, Web Clipper, CLI, Importer, and open-source ecosystem elements.
- Introduced confidence labels: High, Medium, Inferred.
- Identified core architecture: local-first vault layer, metadata/index layer, workspace/view layer, editor/rendering layer, extension layer, Sync/Publish services.
- Noted that exact private implementation of the core app was not public.

**Research basis:** Official Obsidian docs and public API.

---

### Turn 2 — User asked how to go one level deeper

**User request:** What can be done to make the analysis more detailed and raise medium/low-confidence assertions?

**Assistant response summary:**

- Proposed separating user-visible behavior, documented storage/API behavior, observed config behavior, and backend inference.
- Proposed evidence matrices per feature.
- Proposed tracing features to public API objects: `Vault`, `MetadataCache`, `Workspace`, `MarkdownView`, `Editor`, `Plugin`, and Bases API types.
- Proposed inspecting official open-source repos and controlled vault experiments.
- Proposed distinguishing behavior confidence from implementation confidence.

---

### Turn 3 — User provided Electron app package files and LiveSync link

**User provided:**

- LiveSync repo: <https://github.com/vrtmrz/obsidian-livesync>
- Extracted Electron package files: `worker.js`, `starter.js`, `sim.js`, `main.js`, `package.json`, `help.js`, `enhance.js`, `app.js`.

**Assistant response summary:**

- Raised confidence for several areas:
  - core app closed-source/private bundle boundary;
  - Electron desktop main process;
  - Capacitor/mobile bridge;
  - vault events;
  - metadata cache persistence;
  - Graph/Outline metadata dependence;
  - File Recovery IndexedDB behavior;
  - Web Viewer sessions/adblocking;
  - official Sync local file tracking and event loop;
  - LiveSync license/dependencies.
- Noted package metadata: `obsidian-dev`, version `1.12.7`, `license: UNLICENSED`, `private: true`.
- Identified desktop main process imports Electron/Node modules.
- Identified mobile bridge through Capacitor-style plugin registration.
- Identified metadata cache and File Recovery IndexedDB patterns.

**Artifacts:** F2 and uploaded JS files.

---

### Turn 4 — User asked what else could help

**User request:** Would more open-source plugin examples help, or anything else?

**Assistant response summary:**

- Recommended more unpacked Electron package files, especially source maps if present.
- Recommended `.obsidian` folder from a disposable test vault.
- Recommended IndexedDB snapshots from a disposable vault.
- Recommended controlled synthetic vault content.
- Recommended targeted plugins: sync/database replication, metadata/query, file management, editor extensions, Canvas, Bases, graph/visualization, Publish/export, mobile-safe, desktop-only.
- Recommended LiveSync-specific source files: license, package, src, CouchDB/PouchDB, S3/object storage, encryption, conflict resolution, WebRTC.

---

### Turn 5 — User asked how to get source maps

**User request:** How to get a source map from the unpacked Electron package?

**Assistant response summary:**

- Explained that source maps generally must be emitted during build and shipped with the app; they cannot be recreated exactly after the fact.
- Gave commands to find `.map` files, search for `sourceMappingURL`, check ASAR archives, and extract inline Base64 maps.
- Suggested a Node audit script to find maps and map references.
- Recommended falling back to pretty-printing, string mining, command mining, and bundle analysis if no maps exist.

---

### Turn 6 — User provided four plugin repos

**User provided:**

- Dataview: <https://github.com/blacksmithgu/obsidian-dataview>
- Tasks: <https://github.com/obsidian-tasks-group/obsidian-tasks>
- Kanban: <https://github.com/obsidian-community/obsidian-kanban>
- Templater: <https://github.com/SilentVoid13/Templater>

**Assistant response summary:**

- Classified the plugins as high-value architecture examples:
  - Dataview: metadata indexing and queries.
  - Tasks: task parsing, cache invalidation, metadata events.
  - Kanban: custom file-backed views using Markdown/frontmatter.
  - Templater: automation, templates, user scripts, system commands.
- Proposed adding a plugin architecture appendix.
- Noted these plugins demonstrate sophisticated use of Obsidian APIs: `Vault`, `MetadataCache`, `Workspace`, events, commands, custom views, Markdown post-processing, editor extensions, and file operations.

---

### Turn 7 — User confirmed LiveSync source repo

**User provided:** LiveSync source repo again: <https://github.com/vrtmrz/obsidian-livesync>

**Assistant response summary:**

- Positioned LiveSync as an open-source sync architecture comparator, not proof of official Obsidian Sync internals.
- Noted LiveSync's MIT license, non-official compatibility boundary, and support for CouchDB, object storage, and WebRTC.
- Identified important source modules: main plugin wiring, `LiveSyncBaseCore`, storage event adapter, file adapter abstraction, abstract replicator, CouchDB replicator, chunk transfer, encryption helpers.
- Recommended cloning with submodules because important code lives in `livesync-commonlib`.

---

### Turn 8 — User provided `OBSIDIAN_BUNDLE_TRACE.md`

**User provided:** A locally generated bundle trace from Codex session.

**Assistant response summary:**

- Identified the trace as valuable despite no source maps.
- Confirmed two extracted ASAR payloads:
  - `Resources/app.asar` to `Resources/app`;
  - `Resources/obsidian.asar` to `Resources/obsidian`.
- Confirmed version `1.12.7` and no usable source maps.
- Identified high-value files: bootstrap `main.js`, Obsidian `main.js`, renderer `app.js`, metadata worker `worker.js`.
- Highlighted IPC capability surface and worker metadata parser evidence.
- Recommended generating command indexes, IPC maps, worker metadata maps, CSS class indexes, and vendored-library inventories if possible.

**Artifact:** F1.

---

### Turn 9 — User said additional indexes could not be provided

**User message:** Unfortunately cannot provide those additional bundle indexes.

**Assistant response summary:**

- Said the existing bundle trace was enough to proceed.
- Proposed three evidence tiers:
  - official docs/API;
  - bundle-derived evidence;
  - open-source plugin case studies.
- Proposed producing a revised Obsidian feature and architecture breakdown with stronger labels.

---

### Turn 10 — User asked about uploading demo vault and `.obsidian` zips

**User request:** Can zip files be used, or are separate files needed?

**Assistant response summary:**

- Confirmed zip files would work.
- Recommended uploading both the full demo vault and a separate `.obsidian` zip.
- Gave macOS and zip commands to include hidden files.
- Recommended removing private data and keeping useful demo notes, properties, Canvas/Base files, workspace, plugins, hotkeys, and appearance settings.

---

### Turn 11 — User uploaded demo vault and `.obsidian` zips

**User provided:** `demo-vault.zip` and `dot-obsidian.zip`, explicitly demo-only with nothing personal.

**Assistant response summary:**

- Unpacked and inspected both zips.
- Generated `obsidian_demo_vault_evidence.md`.
- Found 26 Markdown files, `.obsidian` settings, core/community plugin state, workspace layout, plugin folders, Dataview/Kanban/QuickAdd patterns.
- Raised confidence for vault-local config, plugin install layout, workspace serialization, Markdown-backed custom views, query/automation plugin workflows.

**Artifact:** F4.

---

### Turn 12 — User asked what else could be collected

**User request:** Knowing what we know now, is there anything else we could collect?

**Assistant response summary:**

- Said the project had reached diminishing returns.
- Recommended the highest-value remaining artifacts:
  - before/after vault diffs;
  - IndexedDB snapshots;
  - runtime console logs;
  - CLI output;
  - official Sync dummy-vault experiments;
  - screen-reader and keyboard transcripts;
  - targeted plugin examples.

---

### Turn 13 — User asked where to find IndexedDB

**User request:** Where would I find the IndexedDB?

**Assistant response summary:**

- Explained IndexedDB is in global Obsidian app storage, not inside the vault.
- Gave locations:
  - macOS: `~/Library/Application Support/obsidian/IndexedDB`
  - Windows: `%APPDATA%\Obsidian\IndexedDB`
  - Linux: `$XDG_CONFIG_HOME/obsidian/IndexedDB` or `~/.config/obsidian/IndexedDB`
- Explained that Chromium/Electron stores IndexedDB as LevelDB-style directories.
- Recommended quitting Obsidian before copying.

---

### Turn 14 — User uploaded IndexedDB contents

**User provided:** `000003.log`, `LOG`, `CURRENT`, `MANIFEST-000001`.

**Assistant response summary:**

- Identified this as LevelDB-backed storage for Obsidian's IndexedDB origin:
  - `app_obsidian.md_0.indexeddb.leveldb`.
- Confirmed global path under `Application Support/obsidian/IndexedDB`.
- Found observed database names:
  - `01c44a8cc1efb296-cache`;
  - `01c44a8cc1efb296-backup`.
- Interpreted these as metadata cache and File Recovery backup DBs.
- Found fields such as `mtime`, `size`, `hash`, `path`, `headings`, `sections`, `frontmatter`, `frontmatterLinks`, `links`, `lists`, `listItems`, `position`, `tags`, `aliases`.
- Found Dataview cache namespace: `dataview/cache/01c44a8cc1efb296`.
- Warned that real vault IndexedDB can expose note-derived content and should not be shared.

**Artifacts:** F3.

---

### Turn 15 — User asked for online research pass

**User request:** Research published official and unofficial blogs and relevant sources to understand Obsidian better.

**Assistant action summary:**

- A deep research session was initiated in the available research tool.
- Subsequent planning continued using gathered official docs, open-source repos, user-provided artifacts, and web checks.
- Official sources used across the final planning include Obsidian Help for data storage, Bases, Bases syntax, Graph, Sync, plugin security, and the public Obsidian API repo.

---

### Turn 16 — User asked whether anything else could be gathered

**User request:** With everything gathered, is there anything else possible for the reverse-engineering exploration?

**Assistant response summary:**

- Recommended only structured/runtime evidence at this point:
  - structured IndexedDB export;
  - minimal diagnostic plugin logging runtime events;
  - before/after diffs;
  - official Sync dummy-vault experiments;
  - network metadata only if safe;
  - Electron/Chromium storage neighbors;
  - accessibility tree and screen-reader transcripts;
  - performance profiles;
  - CLI transcripts;
  - one Git-based sync plugin.
- Recommended not collecting more random minified bundle snippets.

---

### Turn 17 — User disclosed the actual goal

**User goal statement:** Obsidian is not accessible for blind people, keyboard-only users, and voice-control users; developers have ignored requests for 5+ years; user wants to build a Flutter replacement that is completely accessible and compatible with existing Obsidian plugins, and ideally but not necessarily official Obsidian Sync.

**Assistant response summary:**

- Reframed the project:
  - accessible Obsidian-compatible vault client is feasible;
  - full plugin compatibility is a major runtime-emulation project;
  - official Obsidian Sync compatibility is least advisable without permission/public API.
- Recommended a data-compatible accessible Flutter client first, then selective plugin compatibility.
- Proposed plugin compatibility tiers.
- Proposed native accessible shell, vault engine, metadata engine, plugin runtime, and plugin UI adapters.
- Recommended built-in accessible alternatives for visual features such as Graph and Canvas.
- Advised clean-room approach and legal caution.

---

### Turn 18 — User accepted MVP direction and asked about LiveSync as middle ground

**User message:** User agreed with proposed MVP and golden plugins as built-in functionality. Asked whether maintaining compatibility with Self-hosted LiveSync would be a feasible middle ground.

**Assistant response summary:**

- Strongly endorsed LiveSync compatibility as a middle ground.
- Recommended targeting the same LiveSync setup rather than official Obsidian Sync.
- Proposed native LiveSync-compatible built-in sync, not running the LiveSync plugin unchanged.
- Proposed roadmap levels:
  - safe coexistence;
  - settings import;
  - CouchDB-compatible sync;
  - encryption compatibility;
  - hidden-file/customization sync;
  - object storage;
  - WebRTC/P2P later.
- Recommended a LiveSync compatibility spike: parse settings, connect to CouchDB, list remote docs/chunks, identify encryption, read-only pull prototype.

---

### Turn 19 — User emphasized Bases and Graph

**User message:** Bases is important to come close to. Graph features must be there and just as useful as Obsidian's for sighted adoption; accessibility can come later if groundwork is laid. Flutter-native graph work may be significant.

**Assistant response summary:**

- Agreed that Bases should be a first-class data platform and Graph should be visually competitive from day one.
- Proposed a principle: everything visual must have a structured data equivalent.
- Proposed Bases architecture:
  - Markdown vault;
  - metadata/properties index;
  - Base parser;
  - query planner;
  - formula/filter evaluator;
  - result model;
  - table/list/cards/map renderers.
- Proposed Graph architecture:
  - graph data service;
  - query/filter service;
  - layout engine;
  - renderers: visual Flutter canvas, accessible relationship explorer, graph table, path finder, cluster explorer.
- Recommended visual Graph MVP with pan/zoom, filters, groups, local graph, node sizing, context actions.
- Recommended native Flutter graph prototype with `CustomPainter`, `graphview`, or `force_directed_graphview`, but owning the long-term renderer.
- Recommended accessible graph groundwork: node table, selected node panel, graph stats, path finder, cluster explorer.

---

### Turn 20 — User requested final planning artifacts

**User request:** Lay out a detailed roadmap, detailed Phase 0 plan, Phase 1 plan, draft architecture plan in separate Markdown files, and generate a complete Markdown transcript of the entire conversation and research.

**Assistant action:** Generated this planning pack.

---

## 3. Research artifact inventory

| Artifact | Contribution |
|---|---|
| `worker.js` | Bundle-derived evidence of Markdown/metadata extraction worker. |
| `starter.js` | Bundle-derived evidence of Capacitor/mobile bridge and platform flags. |
| `main.js` | Bundle-derived evidence of Electron main-process OS bridge. |
| `app.js` | Bundle-derived evidence of renderer bundle, commands, settings, views, plugin concepts. |
| `package.json` | Core bundle package metadata: private, unlicensed, version 1.12.7. |
| `OBSIDIAN_BUNDLE_TRACE.md` | Consolidated bundle archaeology: two ASAR payloads, no source maps, high-value files, IPC and worker evidence. |
| `demo-vault.zip` | Demo vault content for observed Markdown/plugin workflow evidence. |
| `dot-obsidian.zip` | Observed `.obsidian` configuration, plugin folders, workspace, graph, hotkeys, command pins. |
| `obsidian_demo_vault_evidence.md` | Generated evidence report from demo vault. |
| IndexedDB LevelDB files | Observed global IndexedDB origin, metadata cache DB, File Recovery DB, Dataview cache namespace. |
| Dataview repo | Open-source evidence for metadata/query/index plugin architecture. |
| Tasks repo | Open-source evidence for task parsing, cache invalidation, metadata/vault events. |
| Kanban repo | Open-source evidence for custom Markdown-backed views. |
| Templater repo | Open-source evidence for automation, templates, user scripts, system commands. |
| LiveSync repo | Open-source evidence for sync architecture, CouchDB/object storage/WebRTC, encryption/conflict modules. |

---

## 4. Final product direction captured from the conversation

### MVP principle

Build an accessible, local-first Flutter app that opens existing Obsidian vaults, preserves Obsidian-compatible files/configuration, and provides native accessible equivalents for the workflows people rely on most.

### Key inclusions

- Markdown editing.
- Properties/frontmatter.
- Metadata index.
- Search, backlinks, outline, tags.
- Bases table/list and `.base` compatibility.
- Visual Graph MVP.
- Accessible graph groundwork.
- Tasks, templates, Kanban, Dataview-like queries as native built-ins.
- LiveSync detection and later LiveSync-compatible sync adapter.

### Key exclusions from MVP

- Official Obsidian Sync compatibility.
- Full arbitrary plugin compatibility.
- CodeMirror plugin compatibility.
- Desktop-only Node/Electron plugin compatibility.
- Full accessible Graph canvas, unless feasible as a stretch.

---

## 5. Important evidence conclusions

1. Obsidian's source-of-truth vault model is local Markdown files plus vault-local `.obsidian` configuration. See O1.
2. Obsidian keeps derived backend state in global storage, including IndexedDB metadata cache and Sync connection state. See O1 and F3.
3. The extracted app bundle is version 1.12.7 and contains no usable source maps. See F1.
4. The extracted package manifest identifies the bundle as private and unlicensed. See F2.
5. Demo vault evidence confirms `.obsidian` stores app, appearance, core plugin, community plugin, graph, command palette, hotkey, and workspace state. See F4.
6. Open-source plugin examples show real plugins build substantial backend-like systems on Obsidian APIs. See P1–P5.
7. Bases are stored as `.base` YAML or embedded code blocks and operate over Markdown files/properties. See O2 and O3.
8. Graph is a core visual feature representing notes/files as nodes and internal links as edges. See O4.
9. Flutter provides accessibility primitives through `Semantics` and custom painter semantics, but the app must be designed and tested intentionally. See FL1, FL2, FL3.
10. LiveSync is the best sync middle ground because it is open-source and self-hosted, while official Obsidian Sync is proprietary at the protocol/service level. See P5, P6, O7, O8.


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
