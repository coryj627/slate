# Mermaid architecture diagrams for the accessible Obsidian-compatible Flutter proposal

Generated as standalone Mermaid `.mmd` files plus this combined Markdown reference.

## How to use

- Open the `.mmd` files in any Mermaid-compatible editor or renderer.
- Open this Markdown file in a renderer that supports Mermaid fenced code blocks.
- The plain-language descriptions before each diagram are intentionally included so the document remains useful even when the diagram is not rendered or is being read with a screen reader.

## Diagram inventory

- `01_system_context.mmd` — System context
- `02_core_layered_architecture.mmd` — Core layered architecture
- `03_vault_metadata_pipeline.mmd` — Vault metadata pipeline
- `04_accessibility_ui_architecture.mmd` — Accessibility UI architecture
- `05_plugin_compatibility_runtime.mmd` — Plugin compatibility runtime
- `06_bases_engine.mmd` — Bases engine
- `07_graph_engine.mmd` — Graph engine
- `08_livesync_compatible_sync.mmd` — LiveSync-compatible sync
- `09_phase_0_phase_1_roadmap.mmd` — Phase 0 and Phase 1 roadmap
- `10_core_data_model_er.mmd` — Core data model ER diagram
- `11_plugin_event_lifecycle_sequence.mmd` — Plugin event lifecycle sequence
- `12_canvas_accessibility_architecture.mmd` — Canvas accessibility architecture

## System context

**Description:** Shows the proposed accessible Flutter vault client at the center of the ecosystem. Users interact with the app through screen readers, keyboard, voice control, pointer, or touch. The app opens existing Obsidian-compatible vaults, exposes built-in golden-plugin features, optionally runs a selective plugin compatibility layer, and can connect to open sync adapters including a LiveSync-compatible path.

```mermaid
flowchart LR
  SRU["Screen reader user"]
  KBU["Keyboard-only user"]
  VCU["Voice-control user"]
  SU["Sighted visual user"]

  App["Accessible Flutter vault client"]
  OS["Platform accessibility APIs<br/>Semantics, focus, keyboard, voice"]
  Vault["Existing Obsidian-compatible vault<br/>Markdown, .canvas, .base, .obsidian"]
  BuiltIns["Built-in golden-plugin features<br/>Tasks, Dataview-like queries, Kanban, Templates, Bases"]
  PluginLayer["Selective plugin compatibility layer<br/>Obsidian API shim + native adapters"]
  Sync["Open sync adapters<br/>File-system, Git, LiveSync-compatible"]
  LiveSync["Self-hosted LiveSync infrastructure<br/>CouchDB, object storage, optional P2P"]
  External["External tools<br/>Editors, static site generators, backup tools"]

  SRU --> App
  KBU --> App
  VCU --> App
  SU --> App

  App --> OS
  App <--> Vault
  App --> BuiltIns
  App --> PluginLayer
  App --> Sync
  Sync <--> LiveSync
  Vault <--> External

  classDef primary fill:#eef,stroke:#447,stroke-width:2px
  classDef data fill:#efe,stroke:#484,stroke-width:1px
  classDef external fill:#fff4dd,stroke:#a76,stroke-width:1px

  class App primary
  class Vault,LiveSync data
  class External,Sync,PluginLayer,BuiltIns,OS external
```

## Core layered architecture

**Description:** Shows the proposed app as five layers: accessible Flutter UI, application services, vault intelligence, persistence, and interoperability. The key architectural idea is that visual features, accessible renderers, plugins, sync, and search all depend on the same metadata and vault intelligence layer.

```mermaid
flowchart TB
  subgraph UI["Accessible Flutter application shell"]
    Shell["App shell<br/>navigation, command palette, settings"]
    Editor["Accessible Markdown editor"]
    BasesUI["Bases views<br/>table, list, cards, future map"]
    GraphUI["Graph views<br/>visual graph + future relationship explorer"]
    CanvasUI["Canvas views<br/>visual canvas + structured outline"]
  end

  subgraph AppServices["Application services"]
    Commands["Command registry"]
    Workspace["Workspace manager<br/>tabs, panes, saved layouts"]
    Search["Search service"]
    Properties["Property editor and schema service"]
    Automation["Templates and capture workflows"]
  end

  subgraph Intelligence["Vault intelligence layer"]
    Parser["Markdown parser"]
    Metadata["Metadata cache<br/>links, embeds, headings, blocks, tags, tasks"]
    LinkResolver["Link resolver<br/>resolved and unresolved links"]
    Query["Query engine<br/>Bases, tasks, dashboards"]
    GraphEngine["Graph engine<br/>nodes, edges, groups, layout inputs"]
  end

  subgraph Persistence["Persistence layer"]
    VaultFS["Vault filesystem<br/>.md, .canvas, .base, attachments"]
    Config[".obsidian compatibility<br/>settings, plugins, workspaces"]
    LocalDB["Local app database<br/>cache, file recovery, graph layout, sync state"]
  end

  subgraph Interop["Interoperability layer"]
    PluginShim["Obsidian plugin API shim"]
    NativeAdapters["Native adapters for golden plugins"]
    SyncAdapters["Sync adapters<br/>filesystem, Git, LiveSync-compatible"]
  end

  UI --> AppServices
  AppServices --> Intelligence
  Intelligence --> Persistence
  Interop --> AppServices
  Interop --> Intelligence
  Interop --> Persistence
  PluginShim --> NativeAdapters
  SyncAdapters --> VaultFS
  SyncAdapters --> LocalDB
```

## Vault metadata pipeline

**Description:** Shows how a file event becomes file-state records, parsing work, metadata cache updates, link resolution, and downstream features such as Search, Backlinks, Outline, Bases, Graph, and plugin APIs.

```mermaid
flowchart LR
  VaultChange["Vault change<br/>create, modify, delete, rename"] --> Watcher["File watcher and event normalizer"]
  Watcher --> FileState["File-state record<br/>path, mtime, size, hash"]
  FileState --> DirtyQueue["Dirty-file queue"]
  DirtyQueue --> Parser["Markdown and frontmatter parser"]

  Parser --> Headings["Headings"]
  Parser --> Links["Links and embeds"]
  Parser --> Tags["Tags and aliases"]
  Parser --> Blocks["Blocks, sections, footnotes"]
  Parser --> Tasks["Tasks and list items"]
  Parser --> Props["Properties / YAML frontmatter"]

  Headings --> MetadataCache["Metadata cache"]
  Links --> MetadataCache
  Tags --> MetadataCache
  Blocks --> MetadataCache
  Tasks --> MetadataCache
  Props --> MetadataCache

  MetadataCache --> LinkResolver["Link resolver"]
  LinkResolver --> Resolved["Resolved links"]
  LinkResolver --> Unresolved["Unresolved links"]

  MetadataCache --> Search["Search"]
  MetadataCache --> Backlinks["Backlinks / outgoing links"]
  MetadataCache --> Outline["Outline"]
  MetadataCache --> Bases["Bases / query views"]
  MetadataCache --> Graph["Graph engine"]
  MetadataCache --> Plugins["Plugin API MetadataCache shim"]

  MetadataCache --> LocalDB["Persisted local cache"]
  LocalDB --> Startup["Fast startup / cache warm load"]
  Startup --> MetadataCache
```

## Accessibility UI architecture

**Description:** Shows the accessibility-first UI approach: all input modes flow through focus, semantics, shortcuts, native widgets, and status announcements before reaching feature renderers. The validation layer includes screen-reader, keyboard-only, voice-control, and automated semantics testing.

```mermaid
flowchart TB
  subgraph Inputs["Input modes"]
    Keyboard["Keyboard navigation"]
    ScreenReader["Screen reader navigation"]
    Voice["Voice control"]
    Pointer["Pointer / touch"]
  end

  subgraph FlutterUI["Flutter UI layer"]
    Focus["Focus model<br/>predictable order, roving focus where needed"]
    Semantics["Semantics tree<br/>roles, labels, states, hints, actions"]
    Shortcuts["Shortcut and command routing"]
    NativeWidgets["Native accessible widgets<br/>lists, trees, data grids, forms"]
    LiveRegions["Status announcements<br/>sync, search results, errors, task moves"]
  end

  subgraph Features["Feature renderers"]
    Editor["Markdown editor"]
    FileExplorer["File explorer tree"]
    Bases["Bases data grid/list/cards"]
    Graph["Graph visual renderer<br/>plus future relationship explorer"]
    Canvas["Canvas visual renderer<br/>plus structured outline"]
    Settings["Settings and plugin configuration"]
  end

  subgraph QA["Accessibility validation"]
    ManualAT["Manual AT testing<br/>JAWS, NVDA, VoiceOver, TalkBack"]
    KeyboardQA["Keyboard-only task tests"]
    VoiceQA["Voice-control command tests"]
    SemanticsTests["Automated semantics checks"]
  end

  Inputs --> FlutterUI
  FlutterUI --> Features
  Features --> QA
  QA --> FlutterUI
```

## Plugin compatibility runtime

**Description:** Shows how plugin packages are classified into compatibility tiers, routed through a sandboxed JavaScript runtime and Obsidian API shim when feasible, or handled through native adapters and explicit permission gateways when not feasible.

```mermaid
flowchart TB
  PluginPkg["Plugin package<br/>manifest.json, main.js, optional styles.css, data.json"] --> Classifier["Compatibility classifier"]

  Classifier --> DataPlugin["Tier 1-2<br/>data, commands, metadata, settings"]
  Classifier --> ViewPlugin["Tier 3<br/>custom views"]
  Classifier --> EditorPlugin["Tier 4<br/>editor / CodeMirror extensions"]
  Classifier --> DesktopPlugin["Tier 5<br/>Node/Electron/desktop-only APIs"]

  DataPlugin --> JSRuntime["Sandboxed JavaScript runtime"]
  ViewPlugin --> AdapterDecision["Native adapter or restricted HTML fallback?"]
  EditorPlugin --> UnsupportedOrAdapter["Native adapter or unsupported initially"]
  DesktopPlugin --> PermissionGateway["Explicit desktop permission gateway<br/>likely unsupported in MVP"]

  JSRuntime --> ApiShim["Obsidian API shim"]
  ApiShim --> App["App"]
  ApiShim --> Vault["Vault"]
  ApiShim --> MetadataCache["MetadataCache"]
  ApiShim --> Workspace["Workspace"]
  ApiShim --> Commands["Commands"]
  ApiShim --> Settings["Settings"]
  ApiShim --> MarkdownRenderer["MarkdownRenderer"]

  AdapterDecision --> NativeAdapters["Native accessible adapters<br/>Dataview, Tasks, Kanban, Templater, LiveSync-aware sync"]
  NativeAdapters --> AppServices["Flutter app services"]
  App --> AppServices
  Vault --> AppServices
  MetadataCache --> AppServices
  Workspace --> AppServices
  Commands --> AppServices
  Settings --> AppServices
  MarkdownRenderer --> AppServices

  AppServices --> Audit["Plugin capability audit log<br/>permissions, events, file access, network access"]
```

## Bases engine

**Description:** Shows Bases as a structured query platform over vault metadata and .base files. The output is a result model that can render as accessible tables, lists, cards, maps, exports, and plugin/API surfaces.

```mermaid
flowchart TB
  Vault["Vault files"] --> Metadata["Metadata and property index"]
  BaseFiles[".base files and embedded base blocks"] --> BaseParser["Base syntax parser"]

  Metadata --> QueryPlanner["Base query planner"]
  BaseParser --> QueryPlanner

  QueryPlanner --> Filters["Filters<br/>global + view-specific"]
  QueryPlanner --> Formulas["Formula evaluator"]
  QueryPlanner --> Sorts["Sort engine"]
  QueryPlanner --> Groups["Group engine"]
  QueryPlanner --> Summaries["Summary engine"]

  Filters --> ResultModel["Base result model<br/>rows, columns, computed values, provenance"]
  Formulas --> ResultModel
  Sorts --> ResultModel
  Groups --> ResultModel
  Summaries --> ResultModel

  ResultModel --> Table["Accessible table / data grid"]
  ResultModel --> List["Accessible list view"]
  ResultModel --> Cards["Accessible cards view"]
  ResultModel --> Map["Future map view"]
  ResultModel --> Export["Export<br/>Markdown, CSV, JSON"]
  ResultModel --> PluginAPI["Plugin/API compatibility surface"]

  Table --> Edit["Inline property editing"]
  List --> Edit
  Cards --> Edit
  Edit --> Vault
```

## Graph engine

**Description:** Shows Graph as a renderer-agnostic data pipeline: metadata becomes graph nodes, edges, and metrics; filters and presets produce result sets; a layout engine feeds both a visual Flutter graph and future accessible graph modes such as graph table, relationship explorer, path finder, and cluster navigator.

```mermaid
flowchart TB
  Metadata["Metadata cache<br/>links, embeds, tags, folders, properties"] --> GraphData["Graph data service"]
  GraphData --> Nodes["Nodes<br/>files, attachments, unresolved targets"]
  GraphData --> Edges["Edges<br/>resolved links, embeds, inferred relationships"]
  GraphData --> Metrics["Metrics<br/>in-degree, out-degree, centrality, orphan state"]

  Nodes --> Filter["Graph query and filter service"]
  Edges --> Filter
  Metrics --> Filter

  Filter --> GlobalGraph["Global graph result set"]
  Filter --> LocalGraph["Local graph result set<br/>active note + depth"]
  Filter --> Presets["Saved graph presets<br/>orphans, tag clusters, projects, unresolved links"]

  GlobalGraph --> Layout["Layout engine<br/>force, radial, hierarchical, cached coordinates"]
  LocalGraph --> Layout
  Presets --> Layout

  Layout --> Visual["Flutter visual graph renderer<br/>pan, zoom, select, group colors, node size"]
  Layout --> Details["Selected-node details panel"]

  GraphData --> AccessibleFuture["Future accessible graph modes"]
  AccessibleFuture --> Table["Graph table<br/>sortable nodes and metrics"]
  AccessibleFuture --> Relationship["Relationship explorer<br/>incoming, outgoing, mutual, shared tags"]
  AccessibleFuture --> PathFinder["Path finder<br/>shortest path and explanation"]
  AccessibleFuture --> Cluster["Cluster navigator<br/>groups and neighborhoods"]

  Visual --> Actions["Node actions<br/>open, local graph, backlinks, reveal, copy link"]
  Details --> Actions
  Table --> Actions
  Relationship --> Actions
  PathFinder --> Actions
  Cluster --> Actions
```

## LiveSync-compatible sync

**Description:** Shows the proposed native sync adapter path: vault watcher, local sync queue, local sync database, encryption layer, LiveSync-compatible replicator, and backends such as CouchDB, object storage, and optional P2P. It also includes safe coexistence, settings import, status UI, and conflict handling.

```mermaid
flowchart TB
  Vault["Local vault"] --> Watcher["Vault watcher<br/>create, modify, delete, rename"]
  Watcher --> SyncQueue["Sync queue and local state<br/>path, hash, mtime, deleted, conflict flags"]
  SyncQueue --> LocalSyncDB["Local sync database"]

  LocalSyncDB --> Encrypt["Encryption layer<br/>passphrase, key derivation, encrypted chunks"]
  Encrypt --> Replicator["LiveSync-compatible replicator"]

  Replicator --> CouchDB["CouchDB-compatible backend"]
  Replicator --> ObjectStorage["Object storage backend<br/>MinIO, S3, R2"]
  Replicator --> P2P["Future / optional P2P path"]

  CouchDB --> Pull["Pull remote changes"]
  ObjectStorage --> Pull
  P2P --> Pull
  Pull --> Decrypt["Decrypt and validate"]
  Decrypt --> Conflict["Conflict detector and resolver"]
  Conflict --> Apply["Apply safe changes to vault"]
  Apply --> Vault

  SyncQueue --> Push["Push local changes"]
  Push --> Encrypt

  Config["LiveSync settings importer<br/>detect config, safe coexistence, diagnostics"] --> Replicator
  Status["Accessible sync status UI<br/>pending, last sync, errors, conflicts, logs"] --> SyncQueue
  Status --> Replicator
  Conflict --> Status
```

## Phase 0 and Phase 1 roadmap

**Description:** Shows Phase 0 as validation, evidence consolidation, compatibility contracts, accessibility requirements, and technical spikes. Phase 1 then builds the usable alpha foundation: vault support, metadata engine, accessible editor/navigation, Bases MVP, Graph MVP, golden built-ins, and sync coexistence.

```mermaid
flowchart LR
  P0Start["Phase 0 start<br/>definition and validation"] --> P0A["Evidence consolidation<br/>docs, bundle trace, demo vault, plugins, IndexedDB"]
  P0A --> P0B["Compatibility contract<br/>vault files, .obsidian, plugin tiers, sync boundaries"]
  P0B --> P0C["Accessibility requirements<br/>screen reader, keyboard, voice-control acceptance tests"]
  P0C --> P0D["Technical spikes<br/>Flutter editor, metadata parser, Bases parser, graph prototype, LiveSync settings parser"]
  P0D --> Gate0{"Phase 0 gate<br/>buildable MVP scope?"}

  Gate0 --> P1A["Phase 1 foundation<br/>vault open/save, file explorer, settings import"]
  P1A --> P1B["Metadata engine<br/>parser, index, links, tags, properties, tasks"]
  P1B --> P1C["Accessible editor and navigation<br/>command palette, quick switcher, outline, backlinks"]
  P1C --> P1D["Bases MVP<br/>.base parser, table/list views, filters, sort, property editing"]
  P1D --> P1E["Graph data engine + visual MVP<br/>global/local graph, filters, groups, node actions"]
  P1E --> P1F["Built-in golden features<br/>Tasks, templates, Kanban-compatible boards, Dataview-like query views"]
  P1F --> P1G["Sync MVP<br/>safe coexistence, filesystem sync awareness, LiveSync config import"]
  P1G --> Gate1{"Phase 1 gate<br/>usable accessible alpha?"}
```

## Core data model ER diagram

**Description:** Shows the proposed local data model: files, metadata records, property values, links, tags, headings, blocks, tasks, Bases, graph nodes, and graph edges.

```mermaid
erDiagram
  FILE {
    string path PK
    string type
    int mtime
    int ctime
    int size
    string hash
    bool deleted
  }

  METADATA_RECORD {
    string file_path FK
    string parser_version
    string metadata_json
    datetime indexed_at
  }

  PROPERTY_VALUE {
    string file_path FK
    string property_name
    string value_type
    string value_json
  }

  LINK {
    string source_path FK
    string target_ref
    string resolved_path
    string link_type
    int source_line
    int source_offset
  }

  TAG_ASSIGNMENT {
    string file_path FK
    string tag
    string source
  }

  HEADING {
    string file_path FK
    string text
    int level
    int line
  }

  BLOCK {
    string file_path FK
    string block_id
    string section_id
    int line
  }

  TASK {
    string file_path FK
    string task_id
    string status
    string text
    date due_date
    bool completed
  }

  BASE_FILE {
    string path PK
    string base_config_json
  }

  BASE_VIEW {
    string base_path FK
    string view_id
    string view_type
    string query_json
  }

  GRAPH_NODE {
    string node_id PK
    string file_path FK
    string label
    int in_degree
    int out_degree
    string group_json
  }

  GRAPH_EDGE {
    string edge_id PK
    string source_node_id FK
    string target_node_id FK
    string edge_type
    int weight
  }

  FILE ||--o{ METADATA_RECORD : has
  FILE ||--o{ PROPERTY_VALUE : has
  FILE ||--o{ LINK : emits
  FILE ||--o{ TAG_ASSIGNMENT : has
  FILE ||--o{ HEADING : contains
  FILE ||--o{ BLOCK : contains
  FILE ||--o{ TASK : contains
  BASE_FILE ||--o{ BASE_VIEW : defines
  FILE ||--o{ GRAPH_NODE : represented_by
  GRAPH_NODE ||--o{ GRAPH_EDGE : source_of
  GRAPH_NODE ||--o{ GRAPH_EDGE : target_of
```

## Plugin event lifecycle sequence

**Description:** Shows the proposed lifecycle for loading a compatible plugin, giving it API objects through the shim, registering commands/events/views, responding to vault and metadata events, routing output to native accessible renderers, and unloading cleanly.

```mermaid
sequenceDiagram
  participant User
  participant Shell as Flutter shell
  participant Runtime as JS plugin runtime
  participant Shim as Obsidian API shim
  participant Vault
  participant Metadata as Metadata cache
  participant Workspace
  participant Renderer as Native renderer/adapters

  User->>Shell: Enable compatible plugin
  Shell->>Runtime: Load manifest and main.js
  Runtime->>Shim: Construct Plugin(app)
  Shim-->>Runtime: Provide App, Vault, Workspace, MetadataCache
  Runtime->>Shim: registerCommand, registerEvent, registerView, addSettingTab
  Shim->>Shell: Register commands and settings
  Shim->>Workspace: Register view factories

  User->>Vault: Create or modify file
  Vault->>Metadata: Re-index changed file
  Metadata-->>Shim: changed / resolved events
  Shim-->>Runtime: Invoke plugin event handlers
  Runtime->>Shim: Read/write vault, update view, save plugin data
  Shim->>Renderer: Route output to native accessible renderer or adapter
  Renderer-->>User: Announce result or update UI

  User->>Shell: Disable plugin
  Shell->>Runtime: onunload
  Runtime->>Shim: Cleanup registered events, commands, views
  Shim->>Shell: Remove plugin contributions
```

## Canvas accessibility architecture

**Description:** Shows Canvas support as a parser and model over .canvas JSON, with separate visual, outline, table, and keyboard-navigator renderers that share actions and serialize back to the canvas file.

```mermaid
flowchart TB
  CanvasFile[".canvas JSON file"] --> CanvasParser["Canvas parser"]
  CanvasParser --> Cards["Cards<br/>text, file, link, group, media"]
  CanvasParser --> Edges["Edges<br/>source, target, side, label"]
  CanvasParser --> Spatial["Spatial state<br/>x, y, width, height, z/order"]

  Cards --> CanvasModel["Canvas model"]
  Edges --> CanvasModel
  Spatial --> CanvasModel

  CanvasModel --> Visual["Visual canvas renderer<br/>pan, zoom, drag, connect"]
  CanvasModel --> Outline["Accessible canvas outline<br/>groups, cards, links, reading order"]
  CanvasModel --> Table["Canvas table<br/>card type, title, group, linked note"]
  CanvasModel --> Navigator["Keyboard navigator<br/>next card, group, connection, path"]

  Visual --> Actions["Canvas actions<br/>open card, edit, move, connect, group"]
  Outline --> Actions
  Table --> Actions
  Navigator --> Actions
  Actions --> Serializer["Canvas serializer"]
  Serializer --> CanvasFile
```
