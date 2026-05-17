# Accessible Obsidian-compatible Flutter proposal — Mermaid diagrams

This folder contains MermaidJS architecture diagrams for the accessible Obsidian-compatible Flutter app proposal.

## Files

- `ALL_MERMAID_ARCHITECTURE_DIAGRAMS.md` — all diagrams in one Markdown file, with plain-language descriptions.
- `mmd/` — standalone Mermaid files.

## Standalone diagrams

- `01_system_context.mmd` — **System context**. Shows the proposed accessible Flutter vault client at the center of the ecosystem. Users interact with the app through screen readers, keyboard, voice control, pointer, or touch. The app opens existing Obsidian-compatible vaults, exposes built-in golden-plugin features, optionally runs a selective plugin compatibility layer, and can connect to open sync adapters including a LiveSync-compatible path.
- `02_core_layered_architecture.mmd` — **Core layered architecture**. Shows the proposed app as five layers: accessible Flutter UI, application services, vault intelligence, persistence, and interoperability. The key architectural idea is that visual features, accessible renderers, plugins, sync, and search all depend on the same metadata and vault intelligence layer.
- `03_vault_metadata_pipeline.mmd` — **Vault metadata pipeline**. Shows how a file event becomes file-state records, parsing work, metadata cache updates, link resolution, and downstream features such as Search, Backlinks, Outline, Bases, Graph, and plugin APIs.
- `04_accessibility_ui_architecture.mmd` — **Accessibility UI architecture**. Shows the accessibility-first UI approach: all input modes flow through focus, semantics, shortcuts, native widgets, and status announcements before reaching feature renderers. The validation layer includes screen-reader, keyboard-only, voice-control, and automated semantics testing.
- `05_plugin_compatibility_runtime.mmd` — **Plugin compatibility runtime**. Shows how plugin packages are classified into compatibility tiers, routed through a sandboxed JavaScript runtime and Obsidian API shim when feasible, or handled through native adapters and explicit permission gateways when not feasible.
- `06_bases_engine.mmd` — **Bases engine**. Shows Bases as a structured query platform over vault metadata and .base files. The output is a result model that can render as accessible tables, lists, cards, maps, exports, and plugin/API surfaces.
- `07_graph_engine.mmd` — **Graph engine**. Shows Graph as a renderer-agnostic data pipeline: metadata becomes graph nodes, edges, and metrics; filters and presets produce result sets; a layout engine feeds both a visual Flutter graph and future accessible graph modes such as graph table, relationship explorer, path finder, and cluster navigator.
- `08_livesync_compatible_sync.mmd` — **LiveSync-compatible sync**. Shows the proposed native sync adapter path: vault watcher, local sync queue, local sync database, encryption layer, LiveSync-compatible replicator, and backends such as CouchDB, object storage, and optional P2P. It also includes safe coexistence, settings import, status UI, and conflict handling.
- `09_phase_0_phase_1_roadmap.mmd` — **Phase 0 and Phase 1 roadmap**. Shows Phase 0 as validation, evidence consolidation, compatibility contracts, accessibility requirements, and technical spikes. Phase 1 then builds the usable alpha foundation: vault support, metadata engine, accessible editor/navigation, Bases MVP, Graph MVP, golden built-ins, and sync coexistence.
- `10_core_data_model_er.mmd` — **Core data model ER diagram**. Shows the proposed local data model: files, metadata records, property values, links, tags, headings, blocks, tasks, Bases, graph nodes, and graph edges.
- `11_plugin_event_lifecycle_sequence.mmd` — **Plugin event lifecycle sequence**. Shows the proposed lifecycle for loading a compatible plugin, giving it API objects through the shim, registering commands/events/views, responding to vault and metadata events, routing output to native accessible renderers, and unloading cleanly.
- `12_canvas_accessibility_architecture.mmd` — **Canvas accessibility architecture**. Shows Canvas support as a parser and model over .canvas JSON, with separate visual, outline, table, and keyboard-navigator renderers that share actions and serialize back to the canvas file.

## Notes

These diagrams describe a clean-room product architecture and compatibility strategy. They are intended to guide implementation planning, not to reproduce proprietary Obsidian internals.

Recommended first-pass review order:

1. `01_system_context.mmd`
2. `02_core_layered_architecture.mmd`
3. `03_vault_metadata_pipeline.mmd`
4. `06_bases_engine.mmd`
5. `07_graph_engine.mmd`
6. `08_livesync_compatible_sync.mmd`
7. `09_phase_0_phase_1_roadmap.mmd`
