# Phase 0 Plan — Discovery, Validation, and Clean-Room Specification

**Phase goal:** Convert the research into a buildable, legally safer, testable product plan before committing to implementation scale.  
**Recommended duration:** 6–10 weeks, depending on team size.  
**Primary output:** A validated technical and accessibility specification for Phase 1.

---

## 1. Phase 0 outcomes

Phase 0 is successful when the team has:

1. A clean-room compatibility boundary.
2. A vault compatibility specification.
3. A core accessibility acceptance matrix.
4. A metadata parser/index proof of concept.
5. A Bases parser/query proof of concept.
6. A Graph data model and visual renderer proof of concept.
7. A plugin-corpus API usage report.
8. A LiveSync compatibility spike plan.
9. A risk register and phase-gate decision for Phase 1.

---

## 2. Workstream A — Product definition and accessibility requirements

### Objectives

- Define the first user population precisely: blind users, keyboard-only users, voice-control users, sighted Obsidian users who need compatibility, and mixed teams.
- Define what “accessible Obsidian-compatible vault client” means and does not mean.
- Convert accessibility goals into testable acceptance criteria.

### Activities

- Write personas and workflows:
  - blind knowledge worker using JAWS/NVDA/VoiceOver;
  - keyboard-only power user;
  - voice-control user;
  - sighted Obsidian user evaluating migration;
  - mixed sighted/blind team sharing a vault.
- Define top 25 workflows:
  - open vault;
  - create note;
  - edit Markdown;
  - navigate headings;
  - create link;
  - search;
  - inspect backlinks;
  - edit properties;
  - create tasks;
  - run query/dashboard;
  - use Bases;
  - use Graph;
  - use Kanban-style board;
  - use templates/capture;
  - resolve sync conflict.
- Draft accessibility acceptance checklist based on Flutter's accessibility guidance: intelligible screen-reader descriptions, no unexpected context switches, 48x48 tappable targets, contrast, error recovery, color-vision support, and large scale factors. See FL3.

### Deliverables

- `accessibility_acceptance_matrix.md`
- `primary_workflows.md`
- `definition_of_accessible_done.md`
- `phase_1_mvp_scope.md`

---

## 3. Workstream B — Clean-room interoperability plan

### Objectives

- Avoid dependence on proprietary Obsidian implementation details.
- Use public docs, public APIs, open-source plugins, and user-owned vault files as the compatibility basis.
- Define what reverse-engineering artifacts can inform architecture without copying code.

### Activities

- Create source-use policy:
  - OK: official docs/API, user-owned vault data, open-source plugin code under license, behavior tests.
  - Use cautiously: minified bundle-derived observations as architecture evidence only.
  - Avoid: copying proprietary code, relying on official Sync internals, using Obsidian trademarks in product naming.
- Create license review checklist for dependencies and plugin-derived ideas.
- Document compatibility targets in terms of behavior and formats, not copied implementation.

### Deliverables

- `clean_room_policy.md`
- `license_review_matrix.md`
- `compatibility_claims_policy.md`

### Evidence basis

- Obsidian core bundle appears private/unlicensed in the extracted package; the bundle trace confirms no useful source maps. See F1 and F2.
- Obsidian public API and docs expose enough to define public interoperability boundaries. See O1 and O5.

---

## 4. Workstream C — Vault compatibility specification

### Objectives

- Define which vault files and settings Phase 1 will read, write, preserve, or ignore.
- Prevent destructive migration.

### Activities

- Build a schema inventory from demo vault, official docs, and observed `.obsidian` files.
- Classify files:
  - **read/write:** Markdown, frontmatter, `.base`, app-owned settings.
  - **read/preserve:** `.canvas`, unknown `.obsidian` config, plugin folders.
  - **read-only initially:** plugin `data.json` for golden plugins.
  - **do not touch:** unrecognized plugin data unless user authorizes migration.
- Define vault-open safety checks:
  - detect external sync providers;
  - detect LiveSync;
  - detect official Sync config if present;
  - warn about simultaneous sync writers.

### Deliverables

- `vault_file_matrix.md`
- `obsidian_config_schema_notes.md`
- `vault_open_safety_checks.md`

### Evidence basis

- Official Obsidian data storage docs establish Markdown vaults and `.obsidian` settings. See O1.
- Demo vault evidence confirms practical `.obsidian` files, workspace state, plugin folders, command palette pins, hotkeys, and graph settings. See F4.

---

## 5. Workstream D — Metadata parser and index spike

### Objectives

- Prove that a Flutter/Dart service can parse enough Markdown metadata to support Phase 1.
- Decide whether to implement parser in Dart, use a Rust/native parser, or use a hybrid worker model.

### Minimum metadata fields

```text
files
headings
sections
frontmatter
frontmatterLinks
links
embeds
tags
aliases
blocks
lists
listItems
tasks
footnotes
resolvedLinks
unresolvedLinks
file state: path, mtime, size, hash
```

### Activities

- Parse the demo vault and compare output against expected links/properties/tasks.
- Build link resolver for wikilinks, Markdown links, aliases, folders, headings, and block IDs.
- Store results in a local database, likely SQLite or a Dart embedded DB.
- Build invalidation model based on file watcher events and mtime/size/hash.

### Deliverables

- `metadata_parser_spike.md`
- `metadata_schema_v0.sql` or equivalent
- `demo_vault_parse_results.json`
- `link_resolution_test_cases.md`

### Evidence basis

- Obsidian docs state metadata cache powers Graph and Outline and is preserved in IndexedDB. See O1.
- Bundle trace identifies `worker.js` as Markdown parsing and metadata extraction worker. See F1.
- IndexedDB files show observed `*-cache` and `*-backup` DB names and cache/backup structures. See F3.

---

## 6. Workstream E — Bases spike

### Objectives

- Prove `.base` parsing, query evaluation, and accessible table rendering.
- Decide Phase 1 Bases scope.

### Activities

- Implement YAML parser for `.base` files.
- Implement filter expression evaluator for common comparisons, boolean operators, and file/note properties.
- Implement formula evaluator for safe core functions.
- Render result set as accessible table/list.
- Build query test fixtures from Obsidian Bases syntax examples.

### Phase 0 prototype target

- One `.base` file with:
  - global filter;
  - one table view;
  - one list view;
  - note properties;
  - file properties;
  - sort;
  - simple groupBy;
  - summary count.

### Deliverables

- `bases_compatibility_v0.md`
- `bases_parser_spike.md`
- `bases_accessible_table_prototype.md`

### Evidence basis

- Bases docs define `.base` YAML syntax, filters, formulas, summaries, and views. See O2 and O3.

---

## 7. Workstream F — Graph spike

### Objectives

- Prove visual Graph MVP feasibility in Flutter.
- Keep graph data model independent from visual renderer.
- Lay accessibility groundwork.

### Activities

- Build graph data model from metadata index.
- Prototype visual graph using:
  - Flutter `CustomPainter`; or
  - `graphview`; or
  - `force_directed_graphview`.
- Prototype graph controls:
  - global/local graph;
  - depth;
  - filters;
  - groups;
  - node sizing;
  - open note action.
- Prototype accessible side panel:
  - selected node title;
  - inbound count;
  - outbound count;
  - tags;
  - folder;
  - connected notes.

### Deliverables

- `graph_data_model_v0.md`
- `graph_visual_renderer_spike.md`
- `graph_accessibility_followup_plan.md`

### Evidence basis

- Obsidian Graph docs define nodes, links, node sizing, filters, groups, and local graph behavior. See O4.
- Flutter `CustomPainter.semanticsBuilder` can supply semantic information for custom-painted visuals. See FL2.
- Existing Flutter graph packages can be used for prototypes. See G1 and G2.

---

## 8. Workstream G — Golden plugin corpus analysis

### Objectives

- Identify the Obsidian API surface required by the top plugins and decide which should be native built-ins.

### Initial corpus

- Dataview
- Tasks
- Kanban
- Templater
- QuickAdd
- LiveSync
- Calendar / Periodic Notes equivalent
- Metadata Menu / MetaEdit equivalent
- Advanced search equivalent
- Git sync equivalent

### Activities

- For each plugin:
  - inspect `manifest.json`, `package.json`, license, source modules;
  - classify API usage: Vault, MetadataCache, Workspace, Editor, Markdown rendering, DOM, CodeMirror, Node/Electron, requestUrl, settings, views, commands;
  - classify compatibility strategy: native built-in, API shim candidate, unsupported.
- Create a compatibility tier matrix:
  - Tier 1: data/command plugins;
  - Tier 2: metadata/query plugins;
  - Tier 3: custom views;
  - Tier 4: editor extensions;
  - Tier 5: Node/Electron desktop-only plugins.

### Deliverables

- `plugin_api_usage_corpus.md`
- `golden_plugins_test_plan.md`
- `plugin_compatibility_tiers.md`

### Evidence basis

- Obsidian API docs define plugin modules and capabilities. See O5.
- Plugin security docs warn about broad plugin access; this supports native built-ins and explicit permissions over wholesale plugin loading. See O6.
- Open-source plugin case studies: P1–P5.

---

## 9. Workstream H — LiveSync compatibility spike plan

### Objectives

- Decide whether LiveSync-compatible sync is feasible as a native adapter.
- Avoid official Obsidian Sync dependence.

### Phase 0 scope

- Parse LiveSync plugin settings from a demo vault if present.
- Set up disposable local CouchDB.
- Connect read-only.
- Enumerate remote docs/chunks.
- Identify encryption mode and schema version.
- Do not write remote data in Phase 0.

### Deliverables

- `livesync_compatibility_spike.md`
- `livesync_settings_parser.md`
- `sync_provider_detection.md`
- `sync_safety_policy.md`

### Evidence basis

- LiveSync is open-source and self-hosted, with CouchDB, object storage, and experimental WebRTC support. See P5 and P6.
- Official Obsidian Sync behavior is documented, but the service protocol is not public enough for MVP dependence. See O7 and O8.

---

## 10. Workstream I — Flutter platform and accessibility spike

### Objectives

- Verify Flutter can deliver the desired desktop/mobile accessibility behaviors.
- Build reusable components for the MVP.

### Components to spike

- Accessible file tree.
- Command palette.
- Quick switcher.
- Markdown editor surface.
- Property editor form.
- Data grid for Bases.
- Visual graph plus accessible side panel.
- Keyboard shortcut manager.
- Voice-control-friendly command names.

### Testing targets

- JAWS on Windows.
- NVDA on Windows.
- VoiceOver on macOS.
- VoiceOver on iOS.
- TalkBack on Android.
- Keyboard-only on all platforms.
- Voice Access / Dragon / macOS Voice Control where possible.

### Deliverables

- `flutter_accessibility_component_spikes.md`
- `screen_reader_test_script_v0.md`
- `keyboard_navigation_spec.md`
- `voice_control_labeling_spec.md`

### Evidence basis

- Flutter's `Semantics` API annotates widgets with meaning for assistive technologies. See FL1.
- Flutter accessibility docs recommend screen-reader testing, contrast, context stability, large targets, undoable errors, color-vision testing, and scale-factor testing. See FL3.

---

## 11. Phase 0 gate checklist

Before Phase 1 starts, answer these questions:

- Can the app open and parse the demo vault without destructive writes?
- Can the metadata engine support backlinks, outline, tags, tasks, and graph edges?
- Can `.base` parsing and basic query evaluation work from the shared metadata index?
- Can a Flutter visual graph be performant enough for small and medium vaults?
- Are accessibility components viable with real screen readers?
- Is LiveSync-compatible sync plausible enough to keep on the roadmap?
- Which golden plugins become native built-ins in Phase 1?
- What compatibility claims are safe to make publicly?
- What must be explicitly out of scope?


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
