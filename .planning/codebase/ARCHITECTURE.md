<!-- refreshed: 2026-05-28 -->
# Architecture

**Analysis Date:** 2026-05-28

## System Overview

```text
┌─────────────────────────────────────────────────────────────────┐
│                      SwiftUI Mac App                            │
│   `apps/slate-mac/Sources/SlateMac/`                           │
│                                                                  │
│  SlateMacApp → RootView → WelcomeView | MainSplitView          │
│  AppState (@MainActor, ObservableObject) — all UI state         │
└──────────────────────────┬──────────────────────────────────────┘
                           │ FFI via uniffi-generated Swift bindings
                           │ `slate_uniffi.swift` + `slate_uniffiFFI.h`
┌──────────────────────────▼──────────────────────────────────────┐
│                  slate-uniffi (Rust FFI crate)                  │
│   `crates/slate-uniffi/src/lib.rs`                              │
│                                                                  │
│  VaultSession (uniffi::Object) — wraps core::VaultSession       │
│  VaultError / all record+enum types — 1:1 mirrors of core       │
│  ScanProgressListener (with_foreign) — callback to Swift        │
└──────────────────────────┬──────────────────────────────────────┘
                           │ Rust crate dependency
┌──────────────────────────▼──────────────────────────────────────┐
│                  slate-core (Rust library crate)                │
│   `crates/slate-core/src/`                                      │
│                                                                  │
│  VaultSession — Mutex<Connection> + VaultProvider               │
│  Parsers: links / tasks / blocks / frontmatter / citations      │
│  DB modules: *_db.rs — SQLite index via rusqlite                │
│  Session layer: session.rs — orchestrates all domain ops        │
└──────────────────────────┬──────────────────────────────────────┘
                           │ filesystem + SQLite
┌──────────────────────────▼──────────────────────────────────────┐
│  Storage                                                         │
│  Markdown vault files (source of truth)                         │
│  `<vault>/.slate/cache.sqlite` — regenerable metadata index    │
│  `<vault>/.slate/prefs.json` — per-vault bibliography prefs    │
└─────────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| `SlateMacApp` | App entry point, menu commands, Settings scene | `apps/slate-mac/Sources/SlateMac/SlateMacApp.swift` |
| `RootView` | Routes between WelcomeView and MainSplitView based on vault state | `apps/slate-mac/Sources/SlateMac/SlateMacApp.swift` |
| `AppState` | Central @MainActor state: vault session, file selection, note text, all async task handles | `apps/slate-mac/Sources/SlateMac/AppState.swift` |
| `MainSplitView` | 3-column NavigationSplitView: FileListSidebar / NoteContentView+NoteEditorView / right sidebar | `apps/slate-mac/Sources/SlateMac/MainSplitView.swift` |
| `WelcomeView` | Pre-vault landing: open vault, recent vaults | `apps/slate-mac/Sources/SlateMac/WelcomeView.swift` |
| `FileListSidebar` | Lazy List of vault files + per-note panels (BacklinksPanel, TasksPanel, etc.) | `apps/slate-mac/Sources/SlateMac/FileListSidebar.swift` |
| `NoteContentView` | Read-only Markdown display, section-per-heading scroll anchors | `apps/slate-mac/Sources/SlateMac/NoteContentView.swift` |
| `NoteEditorView` | NSTextView-backed editable Markdown pane (NSViewRepresentable) | `apps/slate-mac/Sources/SlateMac/NoteEditorView.swift` |
| `CommandPaletteModel` | Testable view-model: fuzzy filter, selection, recents for command palette | `apps/slate-mac/Sources/SlateMac/CommandPaletteModel.swift` |
| `CommandPaletteView` | Command palette sheet UI (Milestone Q) | `apps/slate-mac/Sources/SlateMac/CommandPaletteView.swift` |
| `SlateCommands` | Command IDs catalogue + registerCoreCommands | `apps/slate-mac/Sources/SlateMac/SlateCommands.swift` |
| `ScanProgressAdapter` | Bridges Rust scan-thread callbacks to @Sendable Swift closure | `apps/slate-mac/Sources/SlateMac/ScanProgressAdapter.swift` |
| `PreferencesStore` | UserDefaults JSON persistence for MathPrefs + CodePrefs | `apps/slate-mac/Sources/SlateMac/PreferencesStore.swift` |
| `RecentVaultsStore` | JSON file persistence at ~/Library/Application Support/Slate/recent-vaults.json | `apps/slate-mac/Sources/SlateMac/RecentVaultsStore.swift` |
| `CommandPaletteRecentsStore` | JSON file persistence for palette recent-commands list | `apps/slate-mac/Sources/SlateMac/CommandPaletteRecentsStore.swift` |
| FFI `VaultSession` (uniffi) | 1:1 wrapper over core::VaultSession with uniffi annotations | `crates/slate-uniffi/src/lib.rs` |
| `core::VaultSession` | Orchestrates all vault operations: scan, save, query, render | `crates/slate-core/src/session.rs` |
| `VaultProvider` / `FsVaultProvider` | Filesystem abstraction; atomic writes via temp+rename | `crates/slate-core/src/vault/` |
| `db` module | SQLite connection lifecycle, PRAGMA defaults, migration runner | `crates/slate-core/src/db.rs` |
| Parser modules | Pure functions: extract_links, extract_tasks, extract_blocks, extract_citations, extract_frontmatter | `crates/slate-core/src/{links,tasks,blocks,citations,frontmatter}.rs` |
| `*_db` modules | SQLite index queries for each domain (links_db, tasks_db, properties_db, etc.) | `crates/slate-core/src/{links_db,tasks_db,blocks_db,citations_db,properties_db,search_db}.rs` |

## Pattern Overview

**Overall:** Layered native-first architecture. Rust backend owns all domain logic and accessibility artifact production; native SwiftUI consumes via a generated FFI boundary. The backend's SQLite index is a regenerable cache — Markdown files on disk are the source of truth.

**Key Characteristics:**
- `AppState` is the single source of truth for all UI state, held on `@MainActor`. No view holds domain state directly.
- All FFI calls are dispatched from `AppState` via `Task.detached(priority: .userInitiated)` to avoid blocking the main actor; results are published back through `@Published` properties.
- The Rust `VaultSession` is guarded by a single `Mutex<Connection>` — one writer, serialized access. Long-running scans are cancellable via `CancelToken`.
- Each domain concern (math, code, diagram, citations, embeds) is a parallel "content pipeline" — independent async tasks fanned out from `handleSelectionChange`.
- The FFI layer (`slate-uniffi`) mirrors every core type 1:1; no logic lives there. Types carry `From<core::X>` impls; the session wrapper delegates every call to `self.inner`.

## Layers

**SwiftUI Presentation Layer:**
- Purpose: Render UI and drive user interactions
- Location: `apps/slate-mac/Sources/SlateMac/`
- Contains: SwiftUI View structs, NSViewRepresentable wrappers, sheet models
- Depends on: AppState (via @EnvironmentObject), generated FFI types
- Used by: End user

**AppState (Application State Layer):**
- Purpose: Central @MainActor state machine; owns vault session, all async task handles, and every @Published property the UI observes
- Location: `apps/slate-mac/Sources/SlateMac/AppState.swift`
- Contains: ~4500 lines; all vault lifecycle, note selection, content pipeline orchestration, command palette, search debouncer
- Depends on: FFI VaultSession, store types (PreferencesStore, RecentVaultsStore, CommandPaletteRecentsStore)
- Used by: All SwiftUI views via @EnvironmentObject

**FFI Bridge Layer:**
- Purpose: Type-safe boundary between Swift and Rust; generated via uniffi-rs
- Location: `crates/slate-uniffi/src/lib.rs` (source); `apps/slate-mac/Sources/SlateMac/slate_uniffi.swift` (generated — do not edit)
- Contains: VaultSession uniffi::Object, all record/enum mirrors, ScanProgressListener callback interface
- Depends on: slate-core
- Used by: AppState (via the generated Swift bindings)

**Core Engine Layer:**
- Purpose: All domain logic — vault scanning, Markdown parsing, SQLite indexing, search, property editing, template rendering, content pipelines
- Location: `crates/slate-core/src/`
- Contains: session.rs (orchestrator), domain parser modules, *_db query modules, vault/ (filesystem), db.rs (SQLite lifecycle)
- Depends on: rusqlite, pulldown-cmark, blake3, yaml-rust2, chrono, tree-sitter (code), MathCAT (math)
- Used by: slate-uniffi

**Storage Layer:**
- Purpose: On-disk persistence
- Contains: Markdown vault files (source of truth), `.slate/cache.sqlite` (regenerable index), `.slate/prefs.json` (per-vault prefs)
- Accessed by: slate-core via FsVaultProvider (files) and rusqlite (SQLite)

## Data Flow

### Note Selection (Primary Read Path)

1. User selects row in `FileListSidebar` → sets `AppState.selectedFilePath` (`AppState.swift:251`)
2. `$selectedFilePath` Combine subscriber fires `handleSelectionChange(to:)` (`AppState.swift:1188`)
3. All pending content-pipeline tasks are cancelled; stale state is cleared
4. Five parallel async tasks are spawned:
   - `noteLoadTask` → `loadCurrentNote` → `session.read_text` → `currentNoteText`
   - `linksLoadTask` → `loadCurrentLinks` → `session.note_load_bundle` → backlinks, outgoing links, properties; then chains `embedsLoadTask`
   - `tasksLoadTask` → `loadCurrentNoteTasks` → `session.tasks_for_file` → `currentNoteTasks`
   - `mathBlocksLoadTask` → `loadCurrentNoteMathBlocks` → `session.get_math_blocks` → `currentNoteMathBlocks`
   - `codeBlocksLoadTask` / `diagramBlocksLoadTask` → equivalent pipelines
5. Each task runs the FFI call in `Task.detached(priority: .userInitiated)` then publishes results back on `@MainActor`

### Note Save

1. User presses Cmd+S → `AppState.saveCurrentNote()` called
2. `isSaving = true`; `saveTask` spawned
3. `Task.detached` calls `session.save_text(path, contents, expectedContentHash)`
4. On success: `currentNoteContentHash` updated, `savedBaselineText` synced, `hasUnsavedChanges = false`
5. On `WriteConflict`: `currentSaveConflict` populated → drives "Keep mine / Reload" alert
6. Post-save: refresh tasks, headings, and content pipeline (math/code/diagram) via separate refresh tasks

### Vault Open

1. `AppState.openVault(at:)` → `VaultSession.openFilesystem(rootPath:)` (FFI constructor)
2. `currentSession` set; `loadFiles()` spawned
3. `loadFiles` → `Task.detached` → `session.scan_initial_with_progress(cancel:listener:)` with `ScanProgressAdapter`
4. `ScanProgressAdapter.onProgress` → closure → `DispatchQueue.main.async { handleScanProgress }` → `scanProgress` published
5. On completion: `session.list_files` → `files` array populated

### Command Palette Invocation

1. `⌘⇧P` → `SlateMacApp` command group → `appState.requestCommandPalette()`
2. Guard: vault must be open; otherwise announces "Open a vault first"
3. `isCommandPaletteOpen = true` → `CommandPaletteView` sheet presented
4. `CommandPaletteModel.loadCommands` called `.onAppear` with `commandRegistry` snapshot + `commandPaletteRecents`
5. User navigates / types → `CommandPaletteModel` filters and manages `selectedID`
6. Enter → command action closure executes; `AppState.recordCommandInvocation(id:)` updates recents

**State Management:**
- All state lives on `AppState` (`@MainActor`). Views observe via `@EnvironmentObject`. Individual view-models (`CommandPaletteModel`) are `@StateObject` within the view that owns them. Combine `PassthroughSubject` is used for one-shot scroll and cursor requests (`scrollAnchorRequest`, `lineScrollRequest`, `cursorByteOffsetRequest`).

## Key Abstractions

**VaultSession (core + FFI):**
- Purpose: Gateway to all vault operations. Holds SQLite connection under Mutex. One instance per open vault.
- Examples: `crates/slate-core/src/session.rs`, `crates/slate-uniffi/src/lib.rs` (VaultSession uniffi::Object)
- Pattern: Constructor (`from_filesystem`) returns owned session; Arc-wrapped at FFI boundary for reference-counted lifetime

**Content Pipeline:**
- Purpose: Per-content-type parallel load chain triggered by file selection. Each pipeline has its own `loadTask` + `refreshTask` handle on AppState.
- Examples: math → `mathBlocksLoadTask`/`mathBlocksRefreshTask`, code → `codeBlocksLoadTask`, diagrams → `diagramBlocksLoadTask`, citations → `citationsLoadTask`
- Pattern: race-guard at start of each loader (`guard selectedFilePath == path else { return }`) prevents stale results from landing

**CancelToken:**
- Purpose: Cooperative cancellation for long-running Rust operations (scan, search, rename)
- Examples: `crates/slate-core/src/session.rs:CancelToken`, FFI mirror in `crates/slate-uniffi/src/lib.rs`
- Pattern: Created per call, stored as `private var searchCancelToken`; `cancel()` called before replacing with a new token

**VaultProvider trait:**
- Purpose: Filesystem abstraction so core logic stays testable and mobile platforms can supply bookmark-based implementations
- Examples: `crates/slate-core/src/vault/provider.rs`, `crates/slate-core/src/vault/fs.rs` (FsVaultProvider)
- Pattern: Trait object passed into VaultSession at construction; `FsVaultProvider` is the production desktop implementation

**ScanProgressAdapter:**
- Purpose: Bridges the Rust scanner's background-thread `ScanProgressListener` callbacks to a `@Sendable` Swift closure that marshals to the main actor
- Examples: `apps/slate-mac/Sources/SlateMac/ScanProgressAdapter.swift`
- Pattern: `@unchecked Sendable` class; closure bounces via `DispatchQueue.main.async` inside the callback

## Entry Points

**Mac App Entry:**
- Location: `apps/slate-mac/Sources/SlateMac/SlateMacApp.swift` (`@main struct SlateMacApp`)
- Triggers: App launch
- Responsibilities: Creates `AppState` as `@StateObject`, mounts `RootView`, registers menu commands + Settings scene

**Vault Session Open:**
- Location: `crates/slate-uniffi/src/lib.rs` (`VaultSession::open_filesystem`)
- Triggers: `AppState.openVault(at:)` → FFI constructor
- Responsibilities: Creates `FsVaultProvider`, opens/creates SQLite cache, runs migrations

**Rust Crate Entry:**
- Location: `crates/slate-core/src/lib.rs`
- Triggers: Compiled as `staticlib` linked by Swift
- Responsibilities: Re-exports all public domain types and VaultSession; defines VaultError

## Architectural Constraints

- **Threading:** Swift side: single-threaded main actor for all state mutations. FFI calls dispatched via `Task.detached(priority: .userInitiated)` to a cooperative thread pool. Rust side: single `Mutex<Connection>` serializes all SQLite access — long scan holds the lock; cancellation is cooperative via `CancelToken`.
- **Global state:** `AppState` is a single `@StateObject` at the root; passed down via `@EnvironmentObject`. `CommandRegistry` and `CommandPaletteRecentsStore` are owned by `AppState`. `PreferencesStore` is injected at `AppState.init`. No module-level Rust singletons.
- **FFI type mirroring:** Every core Rust type has an exact mirror in the uniffi crate. `From<core::X>` impls on each FFI type perform the mapping. The generated Swift file (`slate_uniffi.swift`) is build output — do not edit.
- **SQLite as index only:** Markdown files are the authoritative source of truth. The SQLite cache at `<vault>/.slate/cache.sqlite` is a regenerable index. If corrupt or missing, `scan_initial` rebuilds it.
- **No circular imports:** Parser modules (`links.rs`, `tasks.rs`, etc.) are pure — they depend only on external crates (pulldown-cmark). The `*_db` modules depend on the parser modules and `db.rs`. `session.rs` depends on all of them. No cycles.
- **Migrations append-only:** `crates/slate-core/src/db.rs` runs numbered migrations from `crates/slate-core/migrations/`. Existing migrations are never modified; new ones append. The runner refuses to open a DB from a newer schema version.

## Anti-Patterns

### Calling FFI Synchronously on the Main Actor

**What happens:** Calling `session.*` directly (without `Task.detached`) from `@MainActor` code
**Why it's wrong:** Blocks the main thread; the Rust session mutex can be held for the duration of a full vault scan
**Do this instead:** Wrap every FFI call in `Task.detached(priority: .userInitiated) { ... }.value` inside a `Task { @MainActor in ... }` so results publish on the main actor. See `loadCurrentNote` in `AppState.swift:1954`.

### Editing `slate_uniffi.swift` Directly

**What happens:** Manually modifying `apps/slate-mac/Sources/SlateMac/slate_uniffi.swift`
**Why it's wrong:** This file is generated output from `uniffi-bindgen`; it is overwritten on every build
**Do this instead:** Modify `crates/slate-uniffi/src/lib.rs` and regenerate via `make` or the build script

### Holding Content Pipeline State in View-Local @State

**What happens:** Storing note content or metadata (backlinks, math blocks, etc.) in a View's `@State`
**Why it's wrong:** State is lost on view reconstruction, lost on tab switch, and can't be awaited by tests
**Do this instead:** All domain state lives on `AppState` as `@Published` properties. Views observe via `@EnvironmentObject appState`.

## Error Handling

**Strategy:** `VaultError` enum (defined in `crates/slate-core/src/lib.rs`, mirrored in `crates/slate-uniffi/src/lib.rs`) carries all failure cases. Each `AppState` async pipeline catches errors and writes them to a dedicated `@Published` error property (e.g. `noteLoadError`, `linksLoadError`, `scanError`). Views observe these and present alerts or inline error states.

**Patterns:**
- `WriteConflict` from `save_text` → `currentSaveConflict` populated → "Keep mine / Reload / Cancel" alert
- `WriteConflict` from property edits → `currentPropertyEditConflict` → separate property alert
- All other errors → human-readable string via `humanReadable(_:)` (`AppState.swift:4341`) → error `@Published` property

## Cross-Cutting Concerns

**Logging:** `fputs(... stderr)` for unrecoverable Rust-session errors; `NSLog` for non-critical persistence failures (palette recents). No structured logging framework.

**Validation:** Input validation for `toggle_task_status` status character lives in `crates/slate-uniffi/src/lib.rs` (`is_allowed_status_char`). Frontmatter property edits validated by `slate-core`'s YAML parser. Template note names validated by `AppState.validateTemplateNoteName`.

**Authentication:** Not applicable — vault is a local filesystem directory. No network auth.

**Accessibility:** A first-class concern enforced at every layer. The Rust backend produces structured accessibility artifacts (MathML + speech + braille for math, SVG + structured description for diagrams, AT preamble for code). The Swift layer consumes them. `postAccessibilityAnnouncement` (defined in `AppState`) posts polite/assertive VoiceOver announcements. APCA Lc > 75 is the contrast standard; contrast is measured instrumentally (see `APCAContrast.swift` in the test target).

---

*Architecture analysis: 2026-05-28*
