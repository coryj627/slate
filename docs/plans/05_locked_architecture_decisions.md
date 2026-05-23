# 05 — Locked Architecture Decisions and Backend API Sketch

**Status:** Locked decisions as of 2026-05-16 (initial) and 2026-05-17 (editor data model, sync direction, syntax highlighting, citations/bibliography, extensibility model and Obsidian migration path, search and Bases queries, performance and scaling added). Captures architectural choices made during a red-team review and stack reconsideration that followed `04_draft_architecture_plan.md`. Where this document and `04_draft_architecture_plan.md` disagree, this document supersedes — most notably, **Flutter is no longer the chosen UI framework**.

**Audience:** Future contributors, AT-user collaborators, grant reviewers, and anyone who needs to understand the technical constraints under which subsequent design and implementation work proceeds.

**How to read this document:**

1. **Architectural principles** that govern all subsequent feature decisions.
2. **Locked technology stack** — backend, FFI, libraries, per-platform UI.
3. **Platform priority** — order of shipping and realistic timeline.
4. **Rust backend API surface** — types, traits, methods, SQLite schema.
5. **Per-platform UI patterns** — what's native, what wraps native, what falls back.
6. **Content-type pipelines** — Markdown, Math, Mermaid, with multi-representation outputs.
7. **Editor data model and sync direction** — rope + persistent operation log; V1 sync detection-only, V2 LiveSync-compatible CouchDB target; accessible conflict resolution as a deliberate V2 differentiator.
8. **Search, queries, and Bases** — `.base` YAML primary, Dataview DQL parseable, Slate AST as engine target; SQLite-backed with formula layer; accessible query builder; accessible data grid per platform.
9. **Performance and scaling** — SQLite as index not source-of-truth; six baked-in V1 design constraints; concrete benchmark targets at release gate.
10. **Extensibility model and Obsidian migration path** — three tiers (config V1, CLI/API V1.x, WASM V2); plugins never draw UI; decentralized distribution; documented Obsidian migration path as a deliverable.
11. **WebView exception rule** — the one narrow case and its three constraints.
12. **Deferred questions** — what is explicitly not decided yet.
13. **References and potential collaborators**.

---

## 1. Architectural principles

These are project-wide constraints that bind all subsequent feature decisions.

### 1.1 Accessibility is a structural property, not a layer

Accessibility is owned by the data model and the Rust backend, not by the UI layer. The UI consumes accessibility artifacts that the backend produces; it does not generate them. This prevents the most common a11y failure mode: UI-layer drift, regressions on UI rewrites, and silent omissions in new feature work.

### 1.2 One canonical structure, many accessible representations

For every content type, the Rust backend produces one canonical structure and multiple accessible representations of it. The UI layer consumes whichever representation it needs for the current surface.

Examples already in scope:

- **Math:** `{ source: LaTeX, mathml, speech, braille }`
- **Mermaid:** `{ source, svg, structured_description }`

Examples anticipated:

- **Code blocks:** `{ source, syntax_tokens, semantic_spans }`
- **Citations:** `{ key, formatted_display, structured_metadata }`
- **Tables:** `{ source, structured_rows, summary_text }`
- **Inline images:** `{ source_path, rendered_pixels, alt_text (required) }`

This pattern is borrowed from the Jupyter / MathJax / Quarto / MyST ecosystem, which has refined it over a decade of accessible scientific publishing work. It is the project's primary content-modeling rule.

### 1.3 Native UI per platform; no webview-based shell

The accessibility ceiling for desktop applications is platform-native UI surfaces (UIA on Windows, NSAccessibility on Apple, TalkBack on Android). Webview-based shells (Electron, Tauri, Wails, Dioxus-desktop) are rejected as the primary stack — the webview's accessibility behavior depends on browser engine versions outside the project's control, and the regression history is well-documented (Obsidian's Electron 30 upgrade, intermittent WebView2 bugs, NVDA virtual-buffer behavior on app-like UIs).

The cost of native-per-platform (2–3× the UI work; per-platform a11y expertise; two CI pipelines; longer V1) is **accepted** as the price for stable accessibility.

### 1.4 Narrow WebView exception: render-only fallback (currently Android)

A constrained WebView is acceptable **only** as a last-resort fallback for rendering preview content, under three rules:

1. **Preview-only**, never the editor or any interactive control flow.
2. A **structured accessibility representation must always be available** alongside, so AT users never depend on the WebView to understand content.
3. **Fully isolated:** no JS-native bridge, no network, no external scripts; only library code vendored into the app bundle.

Currently scoped to Android only, where native math/diagram renderers are thinner. The same rule generalizes to other render-only surfaces (math, niche syntax highlighting) on any platform where native options prove insufficient. Each use is a deliberate, named exception, not a relaxation of the overall rule. The Mac / iOS / Windows webview prohibition is unchanged.

### 1.5 Build for AT users in the room

AT users (the project founder; future co-designers) are first-class participants in design decisions, not testers of finished work. When AT-user input is required beyond casual community participation, it is paid. The project will engage with the math accessibility and scientific publishing communities — Neil Soiffer (MathCAT), Volker Sorge (Speech Rule Engine), Jupyter Accessibility SIG, DAISY Consortium, NFB / AFB / RNIB — when there is something concrete to evaluate.

---

## 2. Locked technology stack

### 2.1 Shared backend: Rust

Reasons:

- **FFI is a first-class story.** `uniffi-rs` generates idiomatic Swift / Kotlin bindings; `csbindgen` generates idiomatic C#. Both sides feel like normal native code.
- **Memory safety matters in the surface area** — Markdown parsing, file I/O, network sync, crypto for future LiveSync compatibility. Rust's compiler eliminates a class of bugs that would be catastrophic in C++.
- **No runtime overhead.** Compiles to a platform-native `.dylib` / `.dll` / `.so` without dragging a garbage collector or interpreter along.
- **Ecosystem is ready** for every dependency Slate needs (parsing, SQLite, file watching, encryption, HTTP).
- **Learned once, applied everywhere.** Whatever you learn for Rust on the backend works the same on Windows, macOS, iOS, and Android.

### 2.2 Per-platform UI

| Platform | UI shell | Editor surface |
|---|---|---|
| **Mac** | SwiftUI | `NSTextView` via `NSViewRepresentable` |
| **iOS** | SwiftUI | `UITextView` via `UIViewRepresentable` |
| **Windows** | WPF | AvalonEdit |
| **Android** | Jetpack Compose | `EditText` via `AndroidView` |

**Pattern:** declarative UI for chrome, embedded native text view for the editor surface. SwiftUI's `TextEditor`, Compose's `BasicTextField`, and WPF's `TextBox` are not mature enough for long-form Markdown editing with full screen-reader semantics, but each platform has a battle-tested native control to wrap.

**Windows decision note:** WPF is picked over WinUI 3 because [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) provides a mature accessible long-document editor with 15+ years of UIA hardening. WinUI 3 has no equivalent and would require building a custom accessible text editor from scratch — an estimated 2–4 additional months of solo work. The aesthetic / modernity advantage of WinUI 3 does not outweigh the accessibility-maturity advantage of WPF for an a11y-first product.

### 2.3 FFI tooling

- **uniffi-rs** (Mozilla, production-tested in Firefox) — generates Swift bindings for Mac/iOS and Kotlin bindings for Android from a single Rust interface. One generator covers three platforms.
- **csbindgen** (Cysharp) — generates idiomatic C# bindings for Rust libraries. Used for Windows.

### 2.4 Rust backend libraries (locked picks)

| Purpose | Library | Notes |
|---|---|---|
| Markdown parsing | [`pulldown-cmark`](https://github.com/raphlinus/pulldown-cmark) | With custom extensions for Obsidian wikilinks, embeds, callouts |
| LaTeX → MathML | [`pulldown-latex`](https://docs.rs/pulldown-latex) | MathML Core compliant; pull-parser style matches `pulldown-cmark` |
| Math → speech + braille | [`MathCAT`](https://github.com/NSoiffer/MathCAT) | **Same library NVDA uses.** ClearSpeak / MathSpeak speech styles; Nemeth + UEB braille. Pure Rust |
| Mermaid → SVG | [`mermaid-rs-renderer`](https://github.com/1jehuang/mermaid-rs-renderer) | Pure Rust; 13 diagram types; early-stage but viable |
| Citations / bibliography | [`hayagriva`](https://github.com/typst/hayagriva) | CSL processor, supports 2,600+ styles, BibTeX/BibLaTeX/CSL-JSON |
| Syntax highlighting (primary) | [`tree-sitter`](https://crates.io/crates/tree-sitter) | Incremental parsing; parse trees enable semantic spans for AT |
| Syntax highlighting (fallback) | [`syntect`](https://github.com/trishume/syntect) | TextMate grammars for the long tail |
| SQLite | `rusqlite` | WAL mode; bounded page cache (mobile budget) |
| File watching | `notify` | Desktop; mobile uses refresh-on-foreground |
| Content hashing | `blake3` | Fast, modern |
| WASM plugin runtime (V2) | [`wasmtime`](https://github.com/bytecodealliance/wasmtime) | Sandboxed plugin execution; capability-based imports |

### 2.5 Per-platform UI libraries

| Need | Mac / iOS | Windows | Android |
|---|---|---|---|
| Visual math rendering | [`LaTeXSwiftUI`](https://github.com/colinc86/LaTeXSwiftUI) — has VoiceOver via Speech Rule Engine | [`WPFMath` / `xaml-math`](https://github.com/ForNeVeR/xaml-math) + custom UIA `AutomationPeer` exposing MathML and MathCAT speech | SVG display via AndroidSVG; WebView fallback (KaTeX, sandboxed) for diagrams AndroidSVG can't render |
| SVG display | `SVGKit` | `SharpVectors` | `AndroidSVG` |

---

## 3. Platform priority and shipping order

Decided 2026-05-16.

| Order | Platform | Notes |
|---|---|---|
| 1 | **Mac** | First; founder is more fluent here, AppKit/NSTextView is the most mature accessible text editor on any platform |
| 2 | **Windows** | Second; WPF + AvalonEdit gives parity for the editor surface |
| 3 | **iOS** | Third; reuses Rust backend + significant Swift code from Mac |
| 4 | **Android** | Fourth; new UI language (Kotlin), new a11y framework (TalkBack), heaviest lift |
| — | Linux | Explicitly deferred; not on V1 roadmap. GTK4 via `gtk-rs` or Qt are candidates if reconsidered later |

### 3.1 Why this order

- **Mac and iOS share most architecture.** The Rust backend builds for both Apple platforms from one cargo source. Swift code (view-models, FFI bridge, most state management) is largely reusable. SwiftUI views compile on both with `#if os(iOS) ... #else ... #endif` for divergence. `NSTextView` (Mac) and `UITextView` (iOS) are parallel structures with matching wrapper patterns. iOS port is 2–4 months after Mac feature-complete.
- **Mac and Windows are the desktop targets**; both required before mobile work begins. Sighted users adopting from Obsidian, Logseq, etc. are mostly on desktop.
- **Android is the biggest jump** — new UI language, new a11y framework, Storage Access Framework for vault access, new lifecycle model. Best handled after the Apple platforms are settled and Windows is shipped.
- **Linux is deferred** because the Flutter Linux-installer Orca regression demonstrates ongoing Linux a11y immaturity across desktop UI frameworks. Revisit when there is a clear native-Linux a11y story (GTK4 + Orca, Qt + Orca).

### 3.2 Realistic timeline (solo, full-time)

| Window | Deliverable |
|---|---|
| Months 0–3 | Rust backbone with mobile-friendly API; FFI smoke tests on Swift and C# |
| Months 3–9 | Mac alpha (vault open, edit, backlinks, search, properties) |
| Months 9–12 | Windows port (WPF + AvalonEdit, UIA peers, feature parity with Mac alpha) |
| Months 12–15 | iOS port (reuses Rust backend + most Swift code) |
| Months 15–21 | Android port (new Kotlin/Compose codebase, TalkBack hardening) |
| Months 21+ | Bases, Graph, sync layered across all four platforms |

Roughly **21 months to cross-platform core editing**; **2.5–3 years to the fuller vision** including Bases / Graph / sync. Available scope reductions if needed: drop Android until V2; drop sync until V2; drop Bases / Graph until V2. With those cuts, **~15 months to Mac + iOS + Windows core editing**.

---

## 4. Rust backend API surface

The API is designed around five principles:

1. **Opaque handles, not raw paths.** Mobile platforms don't grant arbitrary path access; the API accepts resource handles produced by the host.
2. **Sync API with explicit threading expectations.** Callers dispatch off the UI thread. No `async` across the FFI boundary (UniFFI's async support is improving but still rough).
3. **Long operations are cancellable.** Every method that scans, indexes, or queries the vault accepts a `CancelToken`.
4. **Memory-bounded.** No "load entire vault into memory" operations. Streaming and paged queries throughout.
5. **Events via host-registered callbacks.** Hosts register listener traits; Rust dispatches events to them. No long-lived Rust-side threads independently calling into the host.

### 4.1 Layer 1: `VaultProvider` (platform abstraction)

The host implements this trait so the engine works identically on desktop (where Rust supplies the built-in `FsVaultProvider`) and mobile (where the host implements it with security-scoped bookmarks on iOS / SAF URIs on Android).

```rust
pub trait VaultProvider: Send + Sync {
    fn list_dir(&self, relative: &str) -> Result<Vec<DirEntry>, VaultError>;
    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError>;
    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError>;
    fn delete(&self, relative: &str) -> Result<(), VaultError>;
    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError>;
    fn stat(&self, relative: &str) -> Result<FileStat, VaultError>;

    /// Best-effort change subscription. Returns None if the platform doesn't
    /// support filesystem events for this vault — the engine falls back to
    /// refresh-on-foreground.
    fn watch(&self, sink: Arc<dyn FileEventSink>) -> Result<Option<WatchHandle>, VaultError>;
}

pub struct DirEntry { pub name: String, pub kind: EntryKind }
pub struct FileStat { pub size_bytes: u64, pub mtime_ms: i64, pub kind: EntryKind }
pub enum EntryKind { File, Directory, Symlink }

pub struct FsVaultProvider { /* ... */ }
impl FsVaultProvider { pub fn new(root: PathBuf) -> Self { /* ... */ } }
impl VaultProvider for FsVaultProvider { /* uses std::fs and notify */ }
```

### 4.2 Layer 2: `VaultSession` (main API)

```rust
pub struct VaultSession { /* provider, db connection pool, indexer */ }

impl VaultSession {
    pub fn open(provider: Arc<dyn VaultProvider>, config: SessionConfig) -> Result<Self, VaultError>;
    pub fn close(self) -> Result<(), VaultError>;

    // Scan / refresh
    pub fn scan_initial(&self, cancel: &CancelToken) -> Result<ScanReport, VaultError>;
    pub fn refresh(&self, cancel: &CancelToken) -> Result<RefreshReport, VaultError>;

    // File operations
    pub fn read_text(&self, path: &str) -> Result<String, VaultError>;
    pub fn write_text(&self, path: &str, contents: &str) -> Result<(), VaultError>;
    pub fn create_note(&self, path: &str, contents: &str) -> Result<(), VaultError>;
    pub fn rename(&self, from: &str, to: &str) -> Result<RenameReport, VaultError>;
    pub fn delete_file(&self, path: &str) -> Result<(), VaultError>;

    // Index queries (cheap reads from SQLite)
    pub fn get_file_metadata(&self, path: &str) -> Result<Option<FileMetadata>, VaultError>;
    pub fn list_files(&self, filter: FileFilter, paging: Paging) -> Result<Page<FileSummary>, VaultError>;
    pub fn backlinks(&self, path: &str, paging: Paging) -> Result<Page<Backlink>, VaultError>;
    pub fn outgoing_links(&self, path: &str) -> Result<Vec<Link>, VaultError>;
    pub fn all_tags(&self) -> Result<Vec<TagSummary>, VaultError>;
    pub fn files_with_tag(&self, tag: &str, paging: Paging) -> Result<Page<FileSummary>, VaultError>;

    // Search
    pub fn search(&self, query: SearchQuery, cancel: &CancelToken) -> Result<SearchResults, VaultError>;

    // Events
    pub fn register_listener(&self, listener: Arc<dyn VaultEventListener>) -> ListenerHandle;
    pub fn unregister_listener(&self, handle: ListenerHandle);
}

pub struct SessionConfig {
    pub cache_dir: PathBuf,                 // SQLite location; host-provided
    pub max_db_cache_pages: u32,            // SQLite page cache cap (desktop: 4096; mobile: 512)
    pub parse_workers: u32,                 // indexing parallelism (desktop: num_cpus; mobile: 2)
    pub parser_version: u32,                // for cache invalidation
    pub tree_sitter_cache_size: u32,        // LRU size for parse trees (desktop: 32; mobile: 8)
    pub oplog_compaction_threshold_entries: u32,  // op-log compaction trigger; default 10_000
    pub oplog_compaction_threshold_bytes: u32,    // default 5 MB
    pub oplog_retention_days: u32,          // default 90
    pub large_file_warn_bytes: u64,         // default 5 MB
    pub large_file_confirm_bytes: u64,      // default 10 MB
    pub large_file_refuse_bytes: u64,       // default 50 MB
}
```

### 4.3 Layer 3: Data types crossing the FFI boundary

```rust
pub struct FileMetadata {
    pub path: String,
    pub name: String,
    pub size_bytes: u64,
    pub mtime_ms: i64,
    pub content_hash: String,       // blake3
    pub parser_version: u32,

    pub frontmatter: Option<FrontmatterValue>,
    pub aliases: Vec<String>,
    pub tags: Vec<TagOccurrence>,
    pub headings: Vec<Heading>,
    pub links: Vec<Link>,
    pub embeds: Vec<Embed>,
    pub tasks: Vec<TaskItem>,
    pub blocks: Vec<BlockReference>,
    pub properties: Vec<Property>,

    /// Specialized content blocks with multi-representation outputs.
    pub specialized_blocks: Vec<SpecializedBlock>,
}

pub enum SpecializedBlock {
    Math(MathBlock),
    Diagram(DiagramBlock),
    Code(CodeBlock),
    // Future: Citation, Table-with-metadata, etc.
}

pub struct MathBlock {
    pub source: String,             // LaTeX
    pub display_style: MathDisplayStyle,  // Inline | Block
    pub mathml: String,             // pulldown-latex output
    pub speech: String,             // MathCAT-generated (per user preference)
    pub braille: Vec<u8>,           // MathCAT-generated; encoding per braille_code preference
    pub line: u32,
    pub byte_offset: u32,
}

pub struct DiagramBlock {
    pub source: String,
    pub dialect: DiagramDialect,    // Mermaid | (future: PlantUML, D2)
    pub svg: Option<Vec<u8>>,       // None if renderer doesn't support this dialect/feature
    pub png_fallback: Option<Vec<u8>>,  // For platforms where SVG display is painful
    pub structured_description: String,  // AT-friendly summary
    pub render_status: DiagramRenderStatus,  // Ok | UnsupportedDialect | RenderFailed
    pub line: u32,
    pub byte_offset: u32,
}

pub struct CodeBlock {
    pub source: String,
    pub language: Option<String>,
    pub tokens: Vec<SyntaxToken>,       // for visual highlighting
    pub semantic_spans: Vec<SemanticSpan>,  // for AT (function name, type name, etc.)
    pub line: u32,
    pub byte_offset: u32,
}
```

`FileSummary` is the slim version for list views (path, name, mtime, size, primary tag). `FileMetadata` is rehydrated on-demand when a user opens a note.

### 4.4 Layer 4: Events

```rust
pub trait VaultEventListener: Send + Sync {
    fn on_file_changed(&self, event: FileChangeEvent);
    fn on_index_progress(&self, progress: IndexProgress);
    fn on_index_complete(&self);
    fn on_error(&self, error: VaultError);
}

pub enum FileChangeEvent {
    Created { path: String },
    Modified { path: String },
    Deleted { path: String },
    Renamed { from: String, to: String },
}
```

### 4.5 SQLite schema (V0)

PRAGMAs: `journal_mode=WAL`, `synchronous=NORMAL`, `temp_store=MEMORY`, `cache_size` configurable (default 4096 pages desktop, 512 pages mobile).

```sql
CREATE TABLE files (
  id            INTEGER PRIMARY KEY,
  path          TEXT NOT NULL UNIQUE,
  name          TEXT NOT NULL,
  extension     TEXT,
  size_bytes    INTEGER NOT NULL,
  mtime_ms      INTEGER NOT NULL,
  content_hash  TEXT NOT NULL,
  parser_version INTEGER NOT NULL,
  indexed_at_ms INTEGER NOT NULL,
  is_markdown   INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX idx_files_extension ON files(extension);
CREATE INDEX idx_files_mtime ON files(mtime_ms);

CREATE TABLE markdown_metadata (
  file_id           INTEGER PRIMARY KEY REFERENCES files(id) ON DELETE CASCADE,
  frontmatter_json  TEXT,
  aliases_json      TEXT,
  parsed_at_ms      INTEGER NOT NULL
);

CREATE TABLE headings (
  file_id     INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
  level       INTEGER NOT NULL,
  text        TEXT NOT NULL,
  slug        TEXT NOT NULL,
  line        INTEGER NOT NULL,
  byte_offset INTEGER NOT NULL
);
CREATE INDEX idx_headings_file ON headings(file_id);

CREATE TABLE links (
  file_id        INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
  raw            TEXT NOT NULL,
  target         TEXT NOT NULL,
  display        TEXT,
  subpath        TEXT,
  resolved_path  TEXT,               -- NULL if unresolved
  link_kind      INTEGER NOT NULL,   -- Wikilink | Markdown | Embed
  line           INTEGER NOT NULL,
  byte_offset    INTEGER NOT NULL
);
CREATE INDEX idx_links_target ON links(target);
CREATE INDEX idx_links_resolved_path ON links(resolved_path);
CREATE INDEX idx_links_file ON links(file_id);

CREATE TABLE tags (
  file_id     INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
  tag         TEXT NOT NULL,
  source      INTEGER NOT NULL,     -- 0=frontmatter, 1=inline
  line        INTEGER,
  byte_offset INTEGER
);
CREATE INDEX idx_tags_tag ON tags(tag);
CREATE INDEX idx_tags_file ON tags(file_id);

CREATE TABLE properties (
  file_id    INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
  key        TEXT NOT NULL,
  value_type INTEGER NOT NULL,
  value_json TEXT NOT NULL,
  source     INTEGER NOT NULL
);
CREATE INDEX idx_properties_key ON properties(key);
CREATE INDEX idx_properties_file ON properties(file_id);

CREATE TABLE tasks (
  file_id        INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
  text           TEXT NOT NULL,
  status_char    TEXT NOT NULL,
  completed      INTEGER NOT NULL,
  due_ms         INTEGER,
  scheduled_ms   INTEGER,
  priority       INTEGER,
  recurrence     TEXT,
  line           INTEGER NOT NULL,
  byte_offset    INTEGER NOT NULL
);
CREATE INDEX idx_tasks_completed ON tasks(completed);
CREATE INDEX idx_tasks_due ON tasks(due_ms);

CREATE TABLE blocks (
  file_id    INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
  block_id   TEXT NOT NULL,
  line       INTEGER NOT NULL,
  byte_offset INTEGER NOT NULL
);
CREATE UNIQUE INDEX idx_blocks_file_block ON blocks(file_id, block_id);

-- Specialized blocks: math, diagrams, code with semantic spans, etc.
-- Stored as JSON for forward compatibility with new dialects.
CREATE TABLE specialized_blocks (
  file_id      INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
  kind         INTEGER NOT NULL,     -- 0=math, 1=diagram, 2=code, ...
  payload_json TEXT NOT NULL,        -- typed payload per kind
  line         INTEGER NOT NULL,
  byte_offset  INTEGER NOT NULL
);
CREATE INDEX idx_specialized_blocks_file ON specialized_blocks(file_id);
CREATE INDEX idx_specialized_blocks_kind ON specialized_blocks(kind);

-- Full-text search over Markdown body
CREATE VIRTUAL TABLE files_fts USING fts5(
  path UNINDEXED, body, content='', tokenize='unicode61'
);
```

---

## 5. Per-platform UI patterns

### 5.1 Common pattern across all four platforms

```
Declarative UI shell (SwiftUI / WPF / Compose) for chrome:
  ├─ File explorer / vault navigator
  ├─ Command palette
  ├─ Settings / preferences
  ├─ Status bar
  └─ Sidebars

Embedded native text view for editor:
  ├─ Mac:     NSTextView (via NSViewRepresentable)
  ├─ iOS:     UITextView (via UIViewRepresentable)
  ├─ Windows: AvalonEdit (WPF-native)
  └─ Android: EditText (via AndroidView)

Specialized renderers wrapping native libraries:
  ├─ Math:    LaTeXSwiftUI / WPFMath+UIA peer / AndroidSVG | WebView fallback
  ├─ Diagrams: SVG via SVGKit / SharpVectors / AndroidSVG | WebView fallback
  └─ Code:    Syntax highlighting in native text view
```

### 5.2 Mac-specific notes

- For the editor surface, use **AppKit's `NSTextView` + `NSTextStorage`** — the most accessible long-document text editor on any platform. VoiceOver reads it natively; selection, find, navigation, IME, braille display support all work.
- SwiftUI for chrome; drop to AppKit via `NSViewRepresentable` wherever VoiceOver behavior matters most.
- For Graph view (deferred to V1.x): `NSView` + `CALayer` or Metal. SwiftUI Canvas is too constrained for the data scales expected.

### 5.3 iOS-specific notes

- For the editor surface, use **`UITextView`** wrapped via `UIViewRepresentable`. Among the most polished VoiceOver experiences on any platform.
- Vault access via `UIDocumentPicker` + security-scoped bookmarks. Persist permission as bookmark data; restore on subsequent launches.
- iCloud Drive, Working Copy (Git), Dropbox, Files app all work through the same picker mechanism.
- **No Mac Catalyst.** Build two separate UI shells that share Swift code, not one shell that runs on both.
- Switch Control and Voice Control on iOS are best-in-class — label accessibility actions for them, not just VoiceOver.

### 5.4 Windows-specific notes

- WPF + AvalonEdit for the editor; AvalonEdit has 15+ years of UIA hardening with JAWS and NVDA.
- For math rendering with full UIA accessibility, build a custom `AutomationPeer` over `WPFMath` output that exposes the MathML representation and the MathCAT-generated speech as the peer's `Name` / `HelpText`. Budget 2–4 weeks.
- Don't pick MAUI. Its Windows backend is WinUI 3 anyway.
- Don't pick WinForms. Looks like 2008.

### 5.5 Android-specific notes

- Jetpack Compose for chrome; `AndroidView` wrapping `EditText` for the editor.
- TalkBack quality has improved a lot but still trails VoiceOver. Expect workarounds for complex custom views.
- Storage Access Framework for vault access. `MANAGE_EXTERNAL_STORAGE` exists but is policy-gated by Google Play.
- This is the platform where the WebView render-only fallback applies (Section 7).

---

## 6. Content-type pipelines

### 6.1 Markdown

Parser: `pulldown-cmark` with custom extensions for:

- Obsidian wikilinks: `[[target]]`, `[[target|display]]`, `[[target#heading]]`, `[[target^block]]`
- Embeds: `![[target]]`
- Callouts: `> [!note]`, `> [!warning]`, etc.
- Inline tags: `#tag/nested`
- Block references: `^block-id`
- Frontmatter: YAML between `---` fences

Output: structured `FileMetadata` with all the inline structures detected, plus specialized blocks for math/diagrams/code/etc.

### 6.2 Math (LaTeX)

```
LaTeX source (e.g. "$\sum_{i=0}^{n} i$")
    │
    │ pulldown-latex
    ▼
MathML representation
    │
    │ MathCAT (per user preferences)
    ▼
{ speech: "the sum from i equals 0 to n of i",
  braille: <Nemeth or UEB bytes> }
```

Stored as `MathBlock` with all four representations: `source`, `mathml`, `speech`, `braille`.

User preferences exposed in settings:

- **Speech style:** ClearSpeak / MathSpeak
- **Verbosity:** short / medium / long
- **Braille code:** Nemeth / UEB
- **Math explorer keybindings** (for navigating math expressions element-by-element)

Per-platform visual rendering:

- **Mac / iOS:** `LaTeXSwiftUI` (has Speech Rule Engine for VoiceOver). MathML and MathCAT speech additionally exposed in a11y tree.
- **Windows:** `WPFMath` for visual rendering. Custom `AutomationPeer` exposes MathML + MathCAT speech to UIA → JAWS / NVDA.
- **Android:** SVG render (preferred) via converting MathML to SVG in the backend and displaying with AndroidSVG. WebView fallback (KaTeX, sandboxed) when SVG path fails for specific diagrams. MathCAT speech set as `aria-label` regardless.

### 6.3 Mermaid diagrams

```
Mermaid source
    │
    │ mermaid-rs-renderer (or fallback if unsupported dialect)
    ▼
{ svg: <bytes>, structured_description: "Flowchart with 5 nodes ..." }
```

Stored as `DiagramBlock` with `source`, `svg`, `structured_description`, `render_status`.

Per-platform visual rendering:

- **Mac / iOS:** SVG via SVGKit, displayed as image.
- **Windows:** SVG via SharpVectors (XAML conversion) or as image.
- **Android:** AndroidSVG (preferred) or WebView fallback for diagrams AndroidSVG can't display.

**Accessibility-first feature unique to Slate:** the structured-description output is generated alongside the visual. Mermaid's own SVG output is acknowledged as "wholly inaccessible to screen readers" by the maintainers (see [mermaid-js#5632](https://github.com/mermaid-js/mermaid/issues/5632), [#2395](https://github.com/mermaid-js/mermaid/issues/2395)). Slate always exposes the structured description in the a11y tree alongside the SVG.

Future work: contribute the structured-description feature back to `mermaid-rs-renderer` as an upstream accessibility PR.

### 6.4 Code blocks and syntax highlighting

Decided 2026-05-17.

#### Engine

**Primary:** [tree-sitter](https://tree-sitter.github.io/tree-sitter/) via the [`tree-sitter`](https://crates.io/crates/tree-sitter) Rust crate. Incremental parsing, parse trees, 300+ available grammars; used by Helix, Zed, Atom, Neovim, GitHub's code rendering.

**Fallback:** [`syntect`](https://github.com/trishume/syntect) for languages without tree-sitter grammars. Regex-based via TextMate grammars; lower quality but covers the long tail.

Rejected alternatives: LSP-based highlighting (too heavy for short snippets), per-platform native highlighters (inconsistent, every grammar reimplemented N times), WASM grammar loading at runtime (good V2+ idea; locked in compiled grammars for V1).

#### V1 language set (compiled into the binary)

| Category | Languages |
|---|---|
| Programming | JavaScript / TypeScript, Python, **R**, **Julia**, Rust, Go, Java, C / C++, C#, Ruby, Swift |
| Markup | Markdown (always), HTML, XML, YAML, TOML, JSON |
| Querying | SQL, GraphQL |
| Shell | Bash, PowerShell |
| Style | CSS, SCSS |
| Academic | LaTeX (separate from math pipeline) |

R and Julia included for scientific and academic users — both have stable tree-sitter grammars and meaningful adoption in statistics and scientific computing. Total compiled-in grammar size estimated at ~5–10 MB. Anything outside this set falls through to syntect's TextMate grammars.

**V1.x:** WASM-based grammar loading at runtime via [`wasmtime`](https://github.com/bytecodealliance/wasmtime) (in the Rust backend, separate from the WebView exception rule). Would let users add languages without rebuilding Slate.

#### Visual tokens (for highlighting)

```rust
pub struct SyntaxToken {
    pub range: TextRange,
    pub kind: TokenKind,
}

pub enum TokenKind {
    Keyword,
    String,
    Number,
    Boolean,
    Comment { doc: bool },
    Function,
    Type,
    Variable,
    Parameter,
    Property,
    Operator,
    Punctuation,
    Constant,
    Builtin,
    Tag,                  // HTML/XML tag, Markdown tag
    Attribute,
    // ... extensible
}
```

Token kinds are **semantic, not literal colors**. The UI maps kinds to visual properties via a theme (see "Theme model" below).

#### Semantic spans (for AT — the differentiator)

Tree-sitter's parse tree contains structural information (function definitions, imports, type annotations, etc.) that today is used only for visual highlighting in editors. Slate maps tree-sitter node kinds to a stable `SemanticKind` enum and exposes the spans to the platform's accessibility tree. This enables AT navigation commands and structural announcements that no other PKM currently provides.

```rust
pub struct SemanticSpan {
    pub range: TextRange,
    pub kind: SemanticKind,
    pub depth: u8,                       // nesting depth, for navigation
}

pub enum SemanticKind {
    Comment { doc: bool },
    StringLiteral,
    NumberLiteral,
    Keyword { word: String },
    Identifier,
    FunctionDefinition { name: String, params: Vec<String> },
    FunctionCall { name: String },
    TypeDefinition { name: String, kind: TypeKind },
    TypeAnnotation { name: String },
    Import { symbol: String, source: Option<String> },
    Export { symbol: String },
    VariableDeclaration { name: String, mutable: bool },
    Parameter { name: String, type_hint: Option<String> },
    Property { name: String },
    ControlFlow { kind: ControlFlowKind },
    // ... extensible per-language
}

pub enum TypeKind { Class, Struct, Enum, Interface, Trait, Alias }
pub enum ControlFlowKind { If, For, While, Loop, Match, Try, Return }
```

**Per-language work:** for each shipped tree-sitter grammar, define a mapping from grammar node kinds to `SemanticKind`. A few hundred lines per language, mostly straightforward — the parse tree already gives you the structure.

#### Per-platform AT navigation

| Platform | Mechanism |
|---|---|
| Mac | [`NSAccessibilityCustomRotor`](https://developer.apple.com/documentation/appkit/nsaccessibilitycustomrotor) — custom navigation rotors (e.g., "Functions" rotor jumps function-to-function) |
| iOS | [`UIAccessibilityCustomRotor`](https://developer.apple.com/documentation/uikit/uiaccessibilitycustomrotor) — same model as Mac |
| Windows | Custom `AutomationPeer` ranges with semantic descriptions exposed via UIA properties |
| Android | `AccessibilityNodeInfo` virtual children with TalkBack custom actions |

**V1 ships:** basic rotor / navigation types for functions, imports, type definitions, and comments — on all four platforms.

**V1.x:** per-language semantic refinements; an "explain this function" AT command that walks the semantic span tree and produces a structured spoken summary ("Function `parseUrl`, takes 1 parameter `input` of type `String`, returns `Url`. Body is 12 lines.").

#### Theme model

1. **Tokens are semantic** (`TokenKind`), not literal colors.
2. **Themes map kinds to visual properties.** Theme files specify per-kind: color, weight, italic, underline. Default themes ship per platform; users can override with custom themes.
3. **OS preferences override theme details.** High-contrast mode, increase-contrast accessibility settings, system font size, dark / light mode — all respected. macOS uses `NSAppearance.current` plus increase-contrast; Windows reads `SystemParameters.HighContrast`; Android uses `AccessibilityManager.isHighTextContrastEnabled`.

**Accessibility rules baked into the theme model:**

- **Don't rely on color alone** for token differentiation — also use weight, italic, or underline. User-configurable.
- **One-click "no highlighting" mode** for AT users who find highlighting distracting or misleading.
- **Auto-shift to high-contrast theme variant** when the OS reports increase-contrast preference.

#### Performance and threading

- **Parsing on a worker thread**, never the editor main thread.
- **Initial parse:** tens of milliseconds for typical files; cached after first compute, keyed by content hash.
- **Incremental reparse on edit:** microsecond-scale. Each operation in the editor's op log triggers an incremental tree-sitter reparse on the worker thread.
- **Vault-wide token cache:** stored in SQLite keyed by `(file_id, parser_version, grammar_version)`. Invalidated on file change.
- **Memory:** parse trees recomputed lazily per-viewport for large vaults. Don't keep all 10k file parse trees resident.

#### VaultSession additions

```rust
impl VaultSession {
    fn get_syntax_tokens(&self, file_path: &str)
        -> Result<Vec<SyntaxToken>, VaultError>;
    fn get_semantic_spans(&self, file_path: &str)
        -> Result<Vec<SemanticSpan>, VaultError>;
    fn get_semantic_spans_filtered(&self, file_path: &str, kinds: Vec<SemanticKindFilter>)
        -> Result<Vec<SemanticSpan>, VaultError>;
    fn get_enclosing_semantic_span(&self, file_path: &str, pos: TextPos)
        -> Result<Option<SemanticSpan>, VaultError>;
    fn list_semantic_navigation_targets(&self, file_path: &str, rotor: SemanticRotor)
        -> Result<Vec<SemanticSpan>, VaultError>;
}

pub enum SemanticRotor {
    Functions,
    Imports,
    TypeDefinitions,
    Comments,
    // ... extensible
}
```

### 6.5 Citations and bibliography

Decided 2026-05-17. Particularly important for the academic and scientific user audience.

#### Syntax: Pandoc style (canonical)

Pandoc citation syntax is the de facto standard for academic Markdown:

| Form | Renders as (Chicago author-date) | Mode |
|---|---|---|
| `[@smith2020]` | (Smith 2020) | Bracketed |
| `[@smith2020, p. 23]` | (Smith 2020, 23) | Bracketed with locator |
| `[@smith2020; @jones2019]` | (Smith 2020; Jones 2019) | Multiple |
| `@smith2020` | Smith (2020) | In-text |
| `[-@smith2020]` | (2020) | Author suppressed |
| `[see @smith2020, p. 23]` | (see Smith 2020, 23) | With prefix |

This is what Pandoc-based academic workflows use, what Obsidian's Citations plugin recognizes, and what Better BibTeX generates keys for.

#### CSL engine: hayagriva

[hayagriva](https://github.com/typst/hayagriva) (Typst team) is the production-ready Rust CSL processor.

- Supports all 2,600+ styles in the official [CSL repository](https://github.com/citation-style-language/styles).
- BibTeX and BibLaTeX bibliographies supported out of the box.
- Latest release Feb 2025; actively maintained.

Rejected alternative: `citeproc-rs` (Zotero) is explicitly labeled work-in-progress.

#### Bibliography source format

V1 supports:

- BibTeX / BibLaTeX (`.bib`)
- CSL-JSON (`.json` conforming to the CSL JSON schema)

User configures bibliography sources and CSL styles via `.slate/prefs.json`:

```json
{
  "bibliography": {
    "sources": [
      { "path": "../library.bib", "format": "BibTeX", "watch": true }
    ],
    "default_style": "styles/apa-7th.csl",
    "additional_styles": ["styles/chicago-author-date.csl", "styles/ieee.csl"]
  }
}
```

Multiple bibliography sources permitted; entries merged by citation key. CSL style files referenced by path; users drop styles from the official CSL repo into their vault as needed.

#### Zotero integration (phased)

| Phase | Integration |
|---|---|
| **V1** | File-based: user exports from Zotero (via Better BibTeX or built-in) to `.bib` or CSL-JSON in the vault. Slate reads on open and re-reads on file change. |
| **V1.x** | Better BibTeX auto-export file watching: "Bibliography updated" notifications when BBT writes to its configured auto-export path. |
| **V2** | Direct Zotero local API (port 23119, available when Better BibTeX is installed) for live autocomplete, full entry display, PDF attachment lookup. Optional; file-based path always works. |
| **Deferred** | Direct Zotero SQLite read; Zotero Web API for cloud-only libraries. |

V1's file-based path covers ~95% of the existing academic Obsidian workflow, because Better BibTeX's auto-export-to-.bib is how most users already operate.

#### Accessible citation handling — V1 differentiator

The documented gap (from [academic library research](https://crl.acrl.org/index.php/crl/article/view/16947/19428)): citation managers are inaccessible to screen-reader users. Today every PKM tool reads `(Smith, 2020)` as "open paren Smith comma twenty twenty close paren" — no structured navigation, no expand-in-place, no jump-to-bibliography.

Applying the project's content-representation principle:

```
"[@smith2020, p. 23]"
    │
    │ Pandoc parse + hayagriva + CSL style
    ▼
{
  raw: "[@smith2020, p. 23]",
  visual_text: "(Smith 2020, 23)",            // sighted display
  speech_text: "Citation: Smith 2020, page 23",  // AT label
  bib_entry: { ... },                          // full structured entry
  style_id: "apa-7th"
}
```

**V1 AT features:**

- Inline citation a11y label is `speech_text`, not visual rendering.
- "Expand citation" command: popover with structured fields (title, authors, year, journal, DOI, abstract) as separate a11y nodes.
- Citations navigation rotor — jump citation-to-citation, same pattern as functions in code.
- "Jump to bibliography entry" command from any citation.
- Bibliography view with each field separately accessible.
- Document audio summary: "This document has 23 citations referencing 18 unique sources. Walk through?"
- Citation-style switching adapts `speech_text` along with the visual.

**V1.x:**

- "Insert citation" workflow with screen-reader-friendly autocomplete from bibliography.
- Citation hover/inspect for sighted users.

#### Footnotes (related but separate)

Standard Markdown footnotes (`[^foo]`) are not citations, but academic users want them. V1 handles them via the `pulldown-cmark` footnote extension. Footnotes get their own AT navigation rotor.

#### New API surface

```rust
pub struct CitationReference {
    pub raw: String,
    pub citations: Vec<CitedItem>,
    pub byte_offset: u32,
    pub line: u32,
}

pub struct CitedItem {
    pub key: String,
    pub locator: Option<Locator>,
    pub prefix: Option<String>,
    pub suffix: Option<String>,
    pub mode: CitationMode,
}

pub struct Locator { pub label: String, pub locator: String }

pub enum CitationMode { Bracketed, InText, SuppressAuthor }

pub struct BibliographySource {
    pub path: String,
    pub format: BibFormat,
    pub watch: bool,
}

pub enum BibFormat { BibTeX, BibLaTeX, CslJson }

pub struct RenderedCitation {
    pub raw: String,
    pub visual_text: String,
    pub speech_text: String,
    pub bib_entry: Option<BibEntry>,
    pub style_id: String,
}

pub struct BibEntry {
    pub key: String,
    pub item_type: String,
    pub title: String,
    pub authors: Vec<Author>,
    pub year: Option<i32>,
    pub journal: Option<String>,
    pub doi: Option<String>,
    pub url: Option<String>,
    pub publisher: Option<String>,
    pub abstract_text: Option<String>,
    pub raw_csl_json: String,
}

pub struct Author { pub family: String, pub given: Option<String> }

impl VaultSession {
    fn set_bibliography_sources(&self, sources: Vec<BibliographySource>)
        -> Result<(), VaultError>;
    fn get_bibliography_entries(&self) -> Result<Vec<BibEntry>, VaultError>;
    fn get_bibliography_entry(&self, key: &str) -> Result<Option<BibEntry>, VaultError>;
    fn render_citation(&self, reference: &CitationReference, style_id: &str)
        -> Result<RenderedCitation, VaultError>;
    fn list_citations_in_file(&self, path: &str)
        -> Result<Vec<CitationReference>, VaultError>;
    fn search_bibliography(&self, query: &str) -> Result<Vec<BibEntry>, VaultError>;
    fn list_files_citing(&self, key: &str) -> Result<Vec<FileSummary>, VaultError>;
    fn list_unresolved_citations(&self) -> Result<Vec<(String, String)>, VaultError>;
}
```

#### SQLite additions

```sql
CREATE TABLE bibliography_entries (
  key             TEXT PRIMARY KEY,
  item_type       TEXT NOT NULL,
  title           TEXT,
  authors_json    TEXT,
  year            INTEGER,
  journal         TEXT,
  doi             TEXT,
  url             TEXT,
  publisher       TEXT,
  raw_csl_json    TEXT NOT NULL,
  source_path     TEXT NOT NULL,
  last_updated_ms INTEGER NOT NULL
);
CREATE INDEX idx_bib_year ON bibliography_entries(year);
CREATE INDEX idx_bib_title ON bibliography_entries(title);

CREATE TABLE file_citations (
  file_id       INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
  citation_key  TEXT NOT NULL,
  locator_label TEXT,
  locator_text  TEXT,
  mode          INTEGER NOT NULL,
  line          INTEGER NOT NULL,
  byte_offset   INTEGER NOT NULL
);
CREATE INDEX idx_file_citations_file ON file_citations(file_id);
CREATE INDEX idx_file_citations_key ON file_citations(citation_key);
```

---

## 7. Editor data model and sync direction

Decided 2026-05-17.

### 7.1 Editor model: rope + persistent operation log (Option B)

**In-memory representation:** a [rope](https://en.wikipedia.org/wiki/Rope_(data_structure)) data structure for the document text. Crate choices: [`ropey`](https://crates.io/crates/ropey) (mature, widely used) or [`crop`](https://crates.io/crates/crop) (newer, similar API). Edits are insert/delete/replace operations on the rope.

**Persistent operation log:** every edit operation is also written to a per-file operation log at `.slate/oplog/<file>.oplog`. The log captures both low-level text operations and higher-level semantic operations (property changes, heading inserts, list-item moves, etc.). The log is **not user-visible in V1** — it's internal infrastructure enabling:

- Robust undo/redo across editor sessions.
- V1.x change-tracking features ("what did I change since I last opened this note?").
- V2 accessible conflict resolution (Section 7.3).
- Future CRDT migration if collaborative editing ever becomes a priority.

**Why not Option A (rope only):** simpler but forecloses on accessible conflict resolution, change tracking, and any future CRDT migration. The marginal V1 cost of adding the op log is small; the upside is large.

**Why not Option C (CRDT-backed editor):** CRDT semantics don't compose with LiveSync's file-level replication. Real-time multi-user collaboration is not a stated V2/V3 target. The complexity is not earned.

### 7.2 Sync direction

| Phase | Sync scope |
|---|---|
| **V1** | Filesystem-based sync provider **detection only** — iCloud Drive, Dropbox, OneDrive, LiveSync plugin config, Git. Warn users about coexistence risks before writing files. **No sync writer in V1.** |
| **V2** | LiveSync-compatible CouchDB sync. Wire-protocol compatibility with the [`obsidian-livesync`](https://github.com/vrtmrz/obsidian-livesync) plugin — same chunking, same encryption, same replication semantics. ~4–6 months of focused work. |
| **V3** | Git-based sync as a secondary option for technical users. |
| **Deferred indefinitely** | CRDT real-time collaboration, object storage / S3-compatible sync, WebRTC P2P. |

The earlier-stated LiveSync target is reaffirmed under the new stack. CouchDB clients exist in Rust ([`couch_rs`](https://crates.io/crates/couch_rs)); the LiveSync encryption scheme is implementable in Rust via `RustCrypto`/`ring`. The conflict model is CouchDB MVCC (file-level conflicts surfaced to the user), which composes cleanly with Option B's operation log.

### 7.3 Accessible conflict resolution as a deliberate V2 differentiator

The standard conflict UX in PKM tools is "show two versions side-by-side." For sighted users it's bad; for AT users it's unusable. Slate's V2 conflict resolution feature is a deliberate differentiator, made possible by Option B's operation log.

**The pattern:**

1. **Structured diff, not textual.** A conflict between two file versions decomposes into named operations: "Local: added heading 'Goals' at line 10. Remote: changed property `status` from 'draft' to 'final'. Local: inserted paragraph at line 23."
2. **Per-operation resolution.** User resolves each operation independently: accept local / accept remote / accept both / custom edit / skip.
3. **Sequential walkthrough.** Screen reader reads operations one at a time. Keyboard shortcuts progress through the list.
4. **Audio summary first.** "5 conflicts: 2 property changes, 2 inserted paragraphs, 1 heading edit. Walk through?"
5. **Visual rendering on top of the same canonical data.** Sighted users get a structured diff display backed by the same operation list. Same content-representation principle as Math/Mermaid: one canonical structure, multiple representations.

**V1 work:**
- Operation log infrastructure (Rust backend, FFI-exposed).
- Structured diff algorithm producing operation-level diffs from two file versions (~2–4 weeks of Rust).
- Diff API exposed via FFI but not yet wired to any conflict-resolution UI.

**V2 work:**
- Per-platform accessible conflict resolution UI (~2–4 weeks per platform).
- Wired into LiveSync replicator's conflict surface.

This is genuinely a feature no other PKM has. The V1 infrastructure cost is small because we'd build the diff infrastructure anyway for change-tracking features; the V2 UI cost is contained because the canonical structure is already there.

### 7.4 New Rust API types

Added to the API surface in Section 4:

```rust
// Operation log

pub struct OperationId(pub String);   // ULID for ordering + uniqueness

pub enum EditOperation {
    // Low-level text operations
    Insert { pos: TextPos, text: String },
    Delete { range: TextRange },
    Replace { range: TextRange, text: String },

    // Higher-level semantic operations
    SetProperty { key: String, value: PropertyValue },
    RemoveProperty { key: String },
    InsertHeading { pos: TextPos, level: u8, text: String },
    MoveListItem { from: TextPos, to: TextPos },
    // ... extensible
}

pub struct LoggedOperation {
    pub id: OperationId,
    pub file_path: String,
    pub op: EditOperation,
    pub timestamp_ms: i64,
    pub session_id: String,            // groups operations from a single editing session
    pub parent_id: Option<OperationId>,
}

pub struct TextPos {
    pub byte_offset: u32,
    pub line: u32,
    pub column: u32,
}

pub struct TextRange { pub start: TextPos, pub end: TextPos }

// Structured diff

pub struct StructuredDiff {
    pub file_path: String,
    pub local_version: ContentHash,
    pub remote_version: ContentHash,
    pub base_version: Option<ContentHash>,   // common ancestor when known
    pub operations: Vec<DiffOperation>,
}

pub struct DiffOperation {
    pub id: DiffOpId,
    pub kind: DiffOpKind,
    pub local_change: Option<EditOperation>,
    pub remote_change: Option<EditOperation>,
    pub context: DiffContext,
    pub semantic_description: String,   // "Added heading 'Goals' at line 10"
}

pub enum DiffOpKind {
    BothModified,   // both sides modified the same location
    LocalOnly,
    RemoteOnly,
}

// Pending conflicts (populated by V2 sync; surfaced for V1 testing)

pub struct PendingConflict {
    pub id: ConflictId,
    pub file_path: String,
    pub detected_at_ms: i64,
    pub diff: StructuredDiff,
    pub resolutions: Vec<ResolutionDecision>,
}

pub enum ResolutionDecision {
    AcceptLocal { op_id: DiffOpId },
    AcceptRemote { op_id: DiffOpId },
    AcceptBoth { op_id: DiffOpId },
    Custom { op_id: DiffOpId, text: String },
    Skip { op_id: DiffOpId },
}

pub struct ConflictSummary {
    pub conflict_id: ConflictId,
    pub file_path: String,
    pub total_operations: u32,
    pub operations_by_kind: HashMap<String, u32>,
    pub spoken_summary: String,        // "5 conflicts: 2 property changes ..."
}

// Sync provider detection (V1)

pub struct DetectedSyncProvider {
    pub kind: SyncProviderKind,
    pub indicator: String,             // path/file that revealed this provider
    pub risk_level: RiskLevel,
    pub warning_text: String,
}

pub enum SyncProviderKind {
    LiveSync,
    ICloudDrive,
    Dropbox,
    OneDrive,
    GoogleDrive,
    Git,
    Unknown,
}

pub enum RiskLevel { Low, Medium, High }
```

`VaultSession` gains these methods:

```rust
impl VaultSession {
    // Operation log
    fn get_operation_log(&self, file_path: &str, paging: Paging)
        -> Result<Page<LoggedOperation>, VaultError>;
    fn get_session_operations(&self, session_id: &str)
        -> Result<Vec<LoggedOperation>, VaultError>;

    // Structured diff
    fn diff(&self, file_path: &str, local: &str, remote: &str)
        -> Result<StructuredDiff, VaultError>;
    fn diff_from_log(&self, file_path: &str, from: OperationId, to: OperationId)
        -> Result<StructuredDiff, VaultError>;

    // Conflicts (V1 plumbing; V2 UI consumption)
    fn pending_conflicts(&self) -> Result<Vec<PendingConflict>, VaultError>;
    fn conflict_summary(&self, conflict_id: ConflictId)
        -> Result<ConflictSummary, VaultError>;
    fn apply_resolution(&self, conflict_id: ConflictId,
                        resolutions: Vec<ResolutionDecision>)
        -> Result<(), VaultError>;

    // Sync detection (V1)
    fn detect_sync_providers(&self) -> Result<Vec<DetectedSyncProvider>, VaultError>;
}
```

### 7.5 Operation log persistence and compaction

The operation log will grow over time. Strategy:

- **Append-only files** at `.slate/oplog/<file>.oplog` — one log file per content file.
- **Binary format** with length-prefixed records for fast tail reads and resumable appends.
- **Compaction policy:** when a file's op log exceeds N entries (initial: 10,000) or M bytes (initial: 5 MB), the log is compacted into a snapshot + recent ops. Old ops past a retention window (initial: 90 days) are discarded.
- **Compaction is incremental** — happens in background, never blocks the editor.
- **Operations are NOT replicated by sync** in V2. The op log is local-only state. Sync replicates the Markdown files; the op log is rebuilt locally on first sync as "imported" operations.

This keeps op log size bounded while preserving the change-tracking and conflict-resolution affordances that matter most.

### 7.6 Updated storage layout

```
vault-root/
├── note-1.md
├── note-2.md
├── folder/
│   └── note-3.md
├── attachment.png
├── .obsidian/                  # preserved unchanged, Obsidian-compatible
│   └── ... (preserved)
└── .slate/                      # Slate-specific state (local-only by default)
    ├── cache.sqlite            # metadata index cache
    ├── oplog/                  # per-file operation logs
    │   ├── note-1.md.oplog
    │   └── ...
    ├── conflicts/              # pending conflicts awaiting resolution
    └── prefs.json              # Slate vault-specific prefs
```

Default sync-ignore patterns when the V2 sync writer ships: `.slate/cache.sqlite`, `.slate/conflicts/`, `.slate/oplog/`. The `.slate/prefs.json` may or may not sync depending on user preference (default: local-only, with an opt-in to sync prefs).

---

## 8. Search, queries, and Bases

Decided 2026-05-17.

### 8.1 Query syntax model: three syntaxes, one engine

| Syntax | Status | Purpose |
|---|---|---|
| **`.base` YAML** | First-class, round-trippable with Obsidian | Vault compatibility; user-authored Bases files |
| **Dataview DQL** | Parsed but not authored | Migration path for users with existing Dataview queries |
| **Slate query AST** | The actual engine target | What the accessible query builder produces |

All three compile to the same `SlateQuery` AST. Round-trip fidelity for `.base` is a hard requirement (open in Slate, save, open in Obsidian → no loss). **DataviewJS (the JavaScript variant) is not supported and never will be** — it requires a JS runtime Slate doesn't have. Users with DataviewJS code rewrite as Slate queries or V2 WASM plugins. This fits the Obsidian migration commitment in Section 10.

### 8.2 Query AST

```rust
pub struct SlateQuery {
    pub source: QuerySource,
    pub filters: Vec<FilterCondition>,
    pub formulas: HashMap<String, Formula>,
    pub group_by: Option<GroupBy>,
    pub sort: Vec<SortClause>,
    pub columns: Vec<ColumnSelection>,
    pub summaries: Vec<SummaryClause>,
    pub limit: Option<usize>,
    pub view: ViewSpec,
}

pub enum QuerySource {
    All,
    Folder(String),
    Tag(String),
    Recent { days: u32 },
    Linked { from_path: String, depth: u32 },
    Custom(String),                      // V2+ via plugin
}

pub struct FilterCondition {
    pub id: ConditionId,
    pub property: PropertyRef,           // file.tags, file.mtime, note.status, formula.is_overdue, ...
    pub operator: FilterOperator,
    pub value: PropertyValue,
    pub combinator: Combinator,          // And, Or
}

pub enum ViewSpec {
    Table { columns: Vec<String>, show_summary: bool },
    List { primary: String, secondary: Vec<String> },
    Cards { fields: Vec<String> },              // V1.x
    Map { lat_field: String, lon_field: String },  // V2+
}
```

### 8.3 Engine architecture: SQLite-backed with Rust formula layer

```
.base YAML  /  Dataview DQL  /  query builder output
    │
    │ parse
    ▼
SlateQuery AST
    │
    │ plan (which tables, joins, indexes)
    ▼
SQLite execution
    │
    │ formula evaluation (in-Rust, after retrieval)
    ▼
QueryResultSet
    │
    │ render
    ▼
table / list / cards / (map) — native per platform
```

Most query execution work happens in SQLite via the existing schema (files, headings, links, tags, properties, tasks). Formula evaluation happens in Rust on the result set, not as part of the SQL query.

Two schema additions to the V0 SQLite schema:

```sql
CREATE TABLE bases_files (
  path              TEXT PRIMARY KEY,
  name              TEXT NOT NULL,
  raw_yaml          TEXT NOT NULL,
  parsed_query_json TEXT NOT NULL,
  parser_version    INTEGER NOT NULL,
  indexed_at_ms     INTEGER NOT NULL
);

CREATE TABLE saved_queries (
  id              TEXT PRIMARY KEY,
  name            TEXT NOT NULL,
  description     TEXT,
  query_json      TEXT NOT NULL,        -- SlateQuery AST serialized
  source_syntax   INTEGER NOT NULL,     -- 0=builder, 1=.base, 2=DQL
  created_at_ms   INTEGER NOT NULL,
  modified_at_ms  INTEGER NOT NULL
);
```

**Query result caching:** each query has a cache key from `(query AST hash, vault generation)`. Cache invalidated when any file in the query's source set changes.

### 8.4 Results data model

Applying the canonical-structure-many-representations principle.

```rust
pub struct QueryResultSet {
    pub query_id: QueryId,
    pub columns: Vec<ColumnDef>,
    pub rows: Vec<ResultRow>,
    pub groups: Option<Vec<ResultGroup>>,
    pub summaries: HashMap<String, SummaryValue>,
    pub total_count: usize,
    pub filtered_count: usize,
    pub executed_at_ms: i64,
    pub audio_summary: String,           // "23 notes, grouped by status, 8 active, 12 in-progress, 3 done"
}

pub struct ColumnDef {
    pub id: String,
    pub label: String,
    pub value_kind: ValueKind,
    pub semantic_role: ColumnRole,       // Primary, Identifier, Metadata, Metric, Action
}

pub struct ResultRow {
    pub file_path: String,
    pub values: HashMap<String, PropertyValue>,
    pub audio_description: String,       // pre-computed for AT navigation
}
```

Two AT-relevant fields worth noting:

- **`ColumnRole::Primary`** marks the column the screen reader uses to announce each row (usually the note title). AT users navigate rows by primary identifier rather than column-by-column.
- **`audio_description`** is pre-computed by the Rust backend per row. The UI hands this string to the platform a11y label. Same canonical-structure pattern as math `speech_text` and citation `speech_text`.

### 8.5 V1 renderers: table and list

Per the original roadmap §6, V1 ships **table** and **list**. **Cards** in V1.x. **Map view** deferred to V2+.

Map view accessibility caveat: a geographic map of notes does not translate well to AT. If/when Slate ships map view, the AT representation is a structured list ("Notes by location: Paris — 3; London — 5; ..."), not a synthesized map description. Sighted users get the map; AT users get the list. Both exposed; user picks.

### 8.6 Accessible query builder

The genuinely hard design problem. Today most users write `.base` YAML by hand or use Obsidian's visual builder; for AT users both are bad UX.

Slate's approach: **structural, condition-by-condition**, each piece independently editable, with live preview and audio summaries.

UX shape:

1. **Source picker** — small, clearly-labeled list of choices ("All notes / Folder X / Tag Y / Recently edited / Linked from this note").
2. **Conditions list** — each filter condition is an independently navigable row. Add condition / Remove condition / Edit condition are explicit keyboard commands. Conditions read aloud sequentially: "Condition 1: tag contains 'project'. Condition 2: status equals 'active'. Combined with AND."
3. **Sort and group sections** — separate sections, each with a small list of fields to add/remove.
4. **Columns / view picker** — which columns appear (table view) or which fields show (list view).
5. **Live preview pane** — separate accessible region. Updates as builder changes. Audio summary updates: "Query returns 23 notes. First result: 'Project Roadmap', modified yesterday."
6. **Save as `.base` file** — when satisfied, the query AST serializes to `.base` YAML for vault compatibility.

The builder produces the same `SlateQuery` AST that `.base` parsing and DQL parsing produce. All three paths are fully interchangeable.

### 8.7 Accessible data grid (per platform)

| Platform | Control | A11y mechanisms |
|---|---|---|
| Mac | `NSTableView` / `NSOutlineView` | VoiceOver native; row/column headers; cell navigation; custom rotors for "Next row," "Next column" |
| iOS | `UITableView` / `UICollectionView` (grouped layout) | VoiceOver native; custom-rotor pattern same as code semantic spans |
| Windows | WPF `DataGrid` | UIA-native; decade-plus of JAWS/NVDA hardening with WPF DataGrid; row/column header support built in |
| Android | Jetpack Compose `LazyColumn` with `Modifier.semantics` annotations | TalkBack reads semantics; workarounds needed for full data-grid behavior |

Required behaviors across all four platforms:

- **Row/column headers announced** when first entering the grid.
- **Cell-by-cell keyboard navigation** with arrow keys.
- **Sort and filter commands** accessible from keyboard (no header-click-only).
- **Row-level actions** accessible by keyboard: open note, edit property, copy link, show backlinks, show local graph (per the roadmap §6).
- **Summary row** at bottom with the default summaries from the roadmap: count, filled, empty, unique, min, max, sum, average, earliest, latest.
- **Export to CSV / Markdown** as keyboard-accessible commands.

### 8.8 Full-text search composes with structured queries

Separate concerns sharing the same output:

- **Full-text search** uses SQLite FTS5 (`files_fts` virtual table, already in V0 schema).
- **Structured queries** hit the relational metadata tables.
- **Both produce `QueryResultSet`** — same output shape, same renderers.
- Structured queries can include a full-text filter clause: "Files in Projects/ AND containing the phrase 'roadmap'."

### 8.9 Op-log-aware temporal queries (V1.x)

A novel capability the operation log (Section 7) enables. Examples:

- "Files I modified in the last 7 days" — already possible via `file.mtime`, but the op log makes it more precise (excludes touch-only events).
- "Files where I added the property `status` in the last week" — only possible with op log.
- "Files where I deleted content containing 'TODO' recently."
- "Files I haven't touched in 6 months but link from many recent notes."

These compose with regular queries:

```yaml
filters:
  and:
    - file.tags.contains("project")
    - oplog.has_change_since(7d)
    - oplog.has_property_change("status", 30d)
```

V1 ships the op log infrastructure (Section 7). V1.x adds the op-log query operators.

### 8.10 Saved queries and dashboards

```rust
pub struct SavedQuery {
    pub id: QueryId,
    pub name: String,
    pub description: Option<String>,
    pub query: SlateQuery,
    pub source_syntax: QuerySyntax,
    pub created_at_ms: i64,
    pub modified_at_ms: i64,
}

pub enum QuerySyntax { Builder, BaseFile, Dql }
```

Saved queries appear in:

- Command palette ("Run query: Active projects").
- A "Queries" sidebar section.
- **Embeddable in notes** via a `slate-query:` directive (V1.x) — similar to Dataview's `dataview:` block, but Slate's renders to the accessible data grid, not visual-only HTML.

### 8.11 New API surface

```rust
impl VaultSession {
    fn create_query_builder(&self) -> QueryBuilder;
    fn execute_query(&self, query: &SlateQuery, cancel: &CancelToken)
        -> Result<QueryResultSet, VaultError>;
    fn save_as_base_file(&self, query: &SlateQuery, path: &str) -> Result<(), VaultError>;
    fn parse_base_file(&self, path: &str) -> Result<SlateQuery, VaultError>;
    fn parse_dql(&self, dql_source: &str) -> Result<SlateQuery, VaultError>;
    fn save_query(&self, name: &str, query: SlateQuery) -> Result<QueryId, VaultError>;
    fn list_saved_queries(&self) -> Result<Vec<SavedQuerySummary>, VaultError>;
    fn full_text_search(&self, query: &str, scope: Option<QuerySource>, cancel: &CancelToken)
        -> Result<QueryResultSet, VaultError>;
}
```

---

## 9. Performance and scaling

Decided 2026-05-17.

### 9.1 Vault scale targets

| Vault size | Use case | Slate target |
|---|---|---|
| 1k–5k notes | Power user | Trivial |
| 10k–20k notes | Heavy user, researcher | Comfortable on all platforms (V1 sweet spot) |
| 40k–50k notes | Extreme — Evernote refugees, decades of academic notes | Comfortable on desktop, tight on mobile |
| 100k+ notes | Outliers (literature scrapers, archives) | Functional but not optimized |

File size targets:

| Size | Behavior |
|---|---|
| <5 MB | Open normally |
| 5–10 MB | Open, warn user once, suggest splitting |
| 10–50 MB | Refuse to open without explicit user confirmation; warn about performance impact |
| >50 MB | Refuse to open; advise splitting or marking as attachment |

V1 design target is **10k–50k notes on desktop, 10k–20k notes on mobile, files up to a few MB**.

### 9.2 SQLite is the index, not the source of truth

A load-bearing scaling property: SQLite holds **derived metadata, not user content**. The expensive data lives on the filesystem; SQLite is the fast-access index over it.

| Stored in SQLite | Stored on filesystem (source of truth) |
|---|---|
| Metadata index (files, headings, links, tags, properties, tasks) | Markdown source files (`.md`) |
| Bibliography entries (parsed from `.bib` / `.json`) | `.bib` files |
| FTS5 full-text index | Attachments / binary files |
| Saved queries and parsed Bases files | `.base` files |
| Specialized blocks (parsed math, mermaid, code) | CSL style files, themes, prefs |
| | Operation logs (`.slate/oplog/`, append-only binary) |

**If the SQLite database is deleted, Slate rebuilds it from vault content on next open.** The cache is regenerable; nothing user-authored lives only in SQLite.

This separation is what makes vault sync feasible (only `.md` files and explicit Slate state need to sync, not the index) and what bounds memory growth (the index can be dropped and rebuilt without data loss).

### 9.3 Six V1 design constraints (baked into the API)

These are not aspirations; they're enforced by the API surface and tested at the V1 release gate.

#### 9.3.1 No "load entire vault" operations

All vault operations are paged or streamed. The Rust API has no method that returns all files at once, all metadata at once, or all operation log entries at once. `Paging` is required on every list-like operation. Methods that could return unbounded results simply don't exist.

#### 9.3.2 Tree-sitter parse trees are LRU-cached

Parse trees are not kept resident for every file. An LRU cache holds parse trees for:

- The currently-open file.
- Recently-touched files (`tree_sitter_cache_size` in `SessionConfig` — desktop default 32, mobile default 8).

Off-screen files have their parse trees evicted. When a query needs to scan many files for code-block metadata, the cached `tokens` and `semantic_spans` from SQLite are used; the parse tree itself is reconstructed lazily if needed.

#### 9.3.3 Op log compaction is automatic and tested at scale

Per Section 7.5, op log compaction triggers at 10,000 entries or 5 MB per file, with a 90-day retention window for fine-grained operations (snapshot + recent ops after that).

Hard constraints:

- Compaction runs in the background — never blocks the editor.
- Failure to compact is a user-visible hard error.
- Compaction is tested against synthetic workloads simulating 5 years of daily editing at the V1 release gate.

#### 9.3.4 First-open indexing runs in the background

Opening a vault for the first time (or after cache invalidation) doesn't block UI:

- File listing and basic vault operations work immediately on filesystem scan.
- Index builds in the background using a thread pool (`rayon` or equivalent).
- Query-dependent features show "indexing N of M files..." until index ready.
- Index progress is exposed via `VaultEventListener::on_index_progress`.

Initial parse time targets:

- 5k files: <5 seconds to fully indexed.
- 10k files: <15 seconds.
- 50k files: <60 seconds.

#### 9.3.5 Mobile-specific memory budgets in `SessionConfig`

`SessionConfig` (Section 4.2) exposes per-budget knobs:

- `max_db_cache_pages` — SQLite page cache (desktop 4096 = 32 MB; mobile 512 = 4 MB).
- `parse_workers` — indexing parallelism (desktop `num_cpus()`; mobile 2).
- `tree_sitter_cache_size` — LRU size for parse trees (desktop 32; mobile 8).
- `oplog_compaction_threshold_entries` / `oplog_compaction_threshold_bytes` — op-log compaction triggers.
- `large_file_warn_bytes` / `large_file_confirm_bytes` / `large_file_refuse_bytes` — file-size thresholds.

Hosts request larger budgets where appropriate (desktop), or honor stricter budgets when the OS reports memory pressure (mobile).

Target steady-state memory:

- Desktop: ~150 MB for a 10k-note vault.
- Mobile: ~50 MB for a 10k-note vault.

#### 9.3.6 Large-file warnings and refusals

File-size thresholds are configurable in vault prefs (defaults in §9.1). Pathological files block more than they enable; the right user action for a 50 MB Markdown file is usually "split the note," not "make the editor faster."

### 9.4 Honest hard limits

Where this design would actually break, not just slow down:

| Scenario | Result | Mitigation |
|---|---|---|
| >500k files in one vault | Index management overhead dominates | Sharded indexes, per-folder databases (V2+, only if real demand) |
| >50 MB individual Markdown file | Native text views struggle | "Split the file" is the right answer, not "make the editor faster" |
| >10 GB cumulative op log without compaction | `.slate/` directory bloats; potentially exceeds filesystem quotas | Compaction must work; monitoring needed; failure is a user-visible error |
| Real-time multi-user concurrent edit | SQLite single-writer model doesn't fit | Different database entirely (CRDT-aware). Explicitly not a V1 or V2 target |
| Cross-vault queries spanning multiple databases | Each vault has its own SQLite database; cross-vault joins are infeasible | Re-architecture would be needed. Not a V1 or V2 target |

### 9.5 V1 release gate performance benchmarks

Measured against synthetic benchmark vaults at each scale. Regressions are V1 release blockers.

| Operation | 10k-note vault | 50k-note vault |
|---|---|---|
| First-open indexing time | <15 s | <60 s |
| Re-open with cache | <2 s | <5 s |
| Open a note (cached) | <50 ms | <50 ms |
| Save a note + reindex | <100 ms | <200 ms |
| Structured Bases query (indexed columns) | <50 ms | <200 ms |
| Full-text search (FTS5) | <100 ms | <300 ms |
| Tree-sitter incremental reparse on keystroke | <5 ms | <5 ms |
| Memory steady state (no edit) | <100 MB desktop / <40 MB mobile | <200 MB desktop / <60 MB mobile |

---

## 10. Extensibility model and Obsidian migration path

Decided 2026-05-17.

### 8.1 Three-tier extensibility model

Slate's extensibility is structured as three distinct tiers with different security and capability profiles. Each tier has clear boundaries and is permanent — these are not nested or layered, they're separate paths suited to different needs.

| Tier | Mechanism | Ships in | What it can do | What it cannot do |
|---|---|---|---|---|
| 1 | Configuration-based extensions | V1 | Customize visual style, automate text expansion, define templates and saved queries, supply CSL styles, bind keyboard shortcuts | Run code; access vault programmatically |
| 2 | External CLI + local HTTP API | V1 (CLI) / V1.x (API) | Anything from outside Slate's process; integrations, automation, batch operations | Affect in-app accessibility or interactive flows (it's out-of-process) |
| 3 | In-process WASM plugin sandbox | V2 | Read/write vault, transform content, register commands, subscribe to events, make network requests — all under capability grants | Draw UI directly; manipulate the a11y tree; run code on the UI thread |

### 8.2 Tier 1: Configuration-based extensions (V1)

Declarative configuration files in the vault or `.slate/`. No code execution.

- **Themes** — token-kind to visual property (color, weight, italic, underline) mapping. JSON/TOML format. (See also Section 6.4 "Theme model.")
- **Snippets** — text expansion. Pure text replacement (e.g., `;todo` → `- [ ] `).
- **Templates** — note templates with safe variable substitution (`{{date}}`, `{{title}}`, etc.). No scripting; variables resolve from a fixed allowlist.
- **Saved queries and Base views** — declarative search and Bases configurations.
- **Custom CSL styles** — drop CSL files into vault and reference from prefs (see Section 6.5).
- **Custom keyboard shortcuts** — command-to-key bindings.
- **Custom tree-sitter grammars** (V1.x) — drop a compiled grammar into vault to add language support.

**Properties:** no code execution, no security risk, no a11y risk, no marketplace needed. Covers ~80% of common customization. Always available.

### 8.3 Tier 2: External CLI and local HTTP API

Slate exposes itself to the outside world via two mechanisms:

- **CLI tool** (`slate`) **ships in V1.** Thin wrapper around the Rust backend: open, read, write, list, search, query, render. Pipeable. Foundation for shell scripting, automation, batch operations.
- **Local HTTP API** **ships in V1.x.** Bound to localhost, authenticated by per-request token, exposes the same surface as the CLI plus events.

**Properties:** out-of-process, no a11y impact, no in-app security model needed (separate processes), language-agnostic. Covers automation, integrations, and "I want to write a Python script to bulk-edit my vault" workflows.

### 8.4 Tier 3: In-process WASM plugin sandbox (V2)

Plugins compile to WebAssembly. Slate runs them inside [`wasmtime`](https://github.com/bytecodealliance/wasmtime) with capability-based security.

- **Language-agnostic.** Plugins written in Rust, AssemblyScript, C, Go, Zig, or any language with a WASM target.
- **Capability-based security.** Each plugin manifests what it needs: `vault.read`, `vault.write`, `network`, `filesystem`. User explicitly grants each capability at install time. Revocable.
- **No direct UI access.** This is the load-bearing accessibility decision (see Section 8.5). Plugins cannot draw UI elements. They produce structured content (Markdown, JSON, structured data) that Slate's native UI renders accessibly.
- **What plugins register:** commands (integrated into the native command palette), event handlers (file changes, on-save, etc.), content transformers, content generators.

#### Plugin manifest format

```toml
[plugin]
name = "weekly-review-generator"
version = "0.1.0"
author = "..."
description = "Generates a weekly review note from the past week's daily notes."

[capabilities]
vault.read = true
vault.write = true
network = false
filesystem = false

[entry]
wasm = "main.wasm"

[commands]
"slate.weekly-review.generate" = "Generate weekly review note"

[events]
on_save = false
on_open = false
```

### 8.5 The load-bearing accessibility constraint

**Plugins NEVER draw UI directly.** This is permanent, not phased.

What plugins *can* do:
- Read vault content.
- Transform Markdown.
- Produce new content (which Slate's native UI renders).
- Register commands (which integrate with the native command palette, accessible by keyboard / voice / screen reader as first-class commands).
- Subscribe to vault and editor events.
- Make network requests (with explicit user permission).

What plugins *cannot* do:
- Draw custom widgets or position views.
- Override accessibility annotations on any Slate-rendered content.
- Block the main thread.
- Manipulate the a11y tree.
- Cancel built-in accessibility commands.

This is the same shape as "content plugins" in some other ecosystems but made explicit and structural: the UI layer is sacred and accessibility-protected; plugins live below it as content/data processors.

#### Visual extensibility through native rendering only

Some plugin use cases want visual extensibility (custom graph rendering, custom timeline, Kanban variant). Two answers:

1. **Most are covered by Bases / Graph / built-in custom views.** Bases is a query → results model with multiple renderers (table, list, cards, map); these are native. That covers most "I want a different view of this data" needs.
2. **For genuinely novel visualizations,** V3+ extension point: plugins register a **"render kind"** — a structured content type. Slate's native UI maps it to a native rendering primitive (e.g., a Gantt chart built into Slate renders the structured rows the plugin produces). The plugin doesn't draw pixels.

This is restrictive but is the only way to keep accessibility intact across third-party extensions.

### 8.6 Rejected alternatives (and why)

- **Third-party JavaScript plugins (the Obsidian model).** The whole reason Slate isn't on webview is to avoid JS-induced accessibility regressions. JS plugins also assume DOM/CodeMirror/Electron capabilities they wouldn't have in Slate.
- **Lua scripting sandbox** ([`mlua`](https://github.com/mlua-rs/mlua)). Fine technology, but the user audience (academic, AT users) isn't Lua-native; the academic ecosystem is Python-dominant. WASM supports plugins written in any language including Python (via Pyodide). WASM's capability model is also more rigorous than embedded Lua sandboxing.
- **QuickJS / V8 JS embedding.** Same arguments against Lua, plus the JS ecosystem expectations (DOM, Node modules) don't match.
- **Native dylib plugins.** Effectively impossible on iOS App Store distribution; crash-not-isolated from Slate; no real security model; code-signing nightmare.

### 8.7 Distribution model

Decentralized. No central marketplace.

- Plugins are signed bundles installed by URL or file.
- Each install displays the plugin's capability manifest; user grants explicit capabilities.
- Capabilities are revocable from settings.
- No Slate-side marketplace curation, review, or hosting.

This avoids the marketplace-curation burden, the trust/liability of being a plugin distributor, and the slippery slope into App-Store-style review processes. The cost is discoverability, which is solved by:

- A community-maintained registry (separate repo, community-maintained, not gatekept by Slate).
- Convention: plugins live in GitHub repos with a standard manifest; users install by pasting URLs.

### 8.8 Obsidian plugin migration path

Slate will not pretend to be Obsidian-plugin-compatible. It IS Obsidian-vault-compatible at the data level. The migration path is a documented commitment — a real deliverable, not aspiration.

#### Three deliverables, three phases

##### V1: "What Slate covers natively, and what happens to your existing vault data"

The most important and least code-heavy deliverable. For each Obsidian plugin whose function Slate covers natively:

- The Obsidian plugin and what it does.
- The Slate native equivalent.
- What happens to existing vault data when opened in Slate — what syntax is recognized, what metadata is preserved, what's silently ignored, what's lost.
- Migration steps if any are needed (e.g., "Templater scripts using JavaScript need to be rewritten as Slate templates with safe substitution, or as V2 WASM plugins").

Covers the ~10–20 plugins that account for most Obsidian usage: Dataview → Bases; Tasks → native; Kanban → native; Templater → native templates; QuickAdd → native capture; Calendar / Periodic Notes → native; Citations plugins → native (see Section 6.5); LiveSync → V2 native sync; theme plugins → Slate theme system; sync plugins → not needed (detection in V1, native sync in V2).

This is drafted *during* development, not at the end. As each native built-in lands, its migration section is written.

##### V1.x: "What Slate cannot and will not port, and why"

A documented, honest list of architectural incompatibilities. Important for trust — we're straight with users about what won't work, rather than letting them discover it plugin by plugin.

Categories:

- **Editor extensions (CodeMirror-based).** Slate uses native text views per platform; CodeMirror plugins don't apply. Examples: code-block customizers, editor syntax highlighters, vim/emacs-style extensions.
- **DOM-based custom views.** Slate has no DOM. Examples: graph customization plugins, custom HTML rendering plugins.
- **CSS injection / `styles.css` plugins.** Slate themes are structured (token-kind to visual property), not CSS. Most theme plugins.
- **Plugins requiring direct UI access.** A11y constraint (Section 8.5). Examples: floating panels, custom modals, status bar manipulation.
- **Mobile-only plugins.** Plugins relying on Obsidian's specific mobile APIs.
- **Plugins using Node.js / Electron APIs.** Slate isn't Node/Electron.

For each: the reason, and what (if anything) replaces the capability through other means.

##### V2: API mapping reference + per-plugin conversion guides

When the WASM plugin API ships:

- **API mapping reference.** Side-by-side: Obsidian's `app.vault.read(file)` ↔ Slate's `vault_session.read_text(path)`. Obsidian's `metadataCache.getCache(file)` ↔ Slate's `get_file_metadata(path)`. Plus the inverse: "Obsidian APIs without a Slate equivalent" with explanations.
- **Conceptual model differences.** Plugin lifecycle, event model, capability model, UI model — where Slate diverges and why.
- **Migration cookbook.** Pattern-by-pattern. "If your Obsidian plugin listens for `file-open` events and modifies the active editor, in Slate you'd register a `vault.on_open` handler that emits a content transformation." Real code examples.
- **Per-plugin conversion guides.** For the top ~25 Obsidian plugins worth porting (the ones whose function isn't already covered natively): specific guides on what conversion looks like.

#### Community contribution model

Not "Slate team writes everything."

- **Slate team writes:** the API mapping reference, the conceptual differences doc, ~5–10 reference plugin conversions, the contribution process itself.
- **Community contributes:** specific plugin conversions, additional cookbook patterns, edge cases Slate team hadn't seen.
- **Repository structure:** migration docs live in `docs/migration/` in the Slate repo. A separate `slate-plugin-conversions` community repository hosts converted plugins, each with the original Obsidian plugin link, the converted Slate WASM plugin source, conversion notes, and what didn't survive the port.

#### Public framing

The honest message:

> Slate is not Obsidian-compatible at the plugin level and never will be. It IS Obsidian-vault-compatible at the data level, and for the most popular plugins, we provide documented migration paths — either to Slate's native equivalents (Dataview, Tasks, Kanban, Templates, Citations, etc.) or, in V2+, to a sandboxed WASM plugin model with capability-based security. Plugins requiring direct UI manipulation, CodeMirror, DOM access, or Electron APIs are not portable, by design, because Slate's accessibility model depends on the UI layer being native.

This recruits the plugin-author community as collaborators (here's what we'll do, here's what we won't, here's how you contribute) rather than treating them as a problem to be solved.

#### Suggested docs/migration/ directory structure

```
docs/
└── migration/
    ├── README.md                       # overview, navigation, framing
    ├── covered-by-native.md            # V1 deliverable
    ├── vault-data-migration.md         # what's preserved/lost when opening
    ├── not-portable.md                 # V1.x deliverable; honest non-list
    ├── api-mapping.md                  # V2 deliverable; Obsidian ↔ Slate APIs
    ├── conceptual-differences.md       # V2; lifecycle, events, capabilities, UI
    ├── cookbook.md                     # V2; pattern-by-pattern
    └── plugins/                        # V2; per-plugin guides
        ├── dataview.md
        ├── tasks.md
        ├── templater.md
        └── ...
```

---

## 11. WebView exception rule (detailed)

Currently scoped to Android render-only surfaces. Re-stated for precision.

**The rule:** a constrained WebView is acceptable as a last-resort fallback for rendering preview content under all three of the following conditions:

1. **Preview-only.** The WebView never hosts content the user edits, types into, navigates with cursor keys, or selects text in. It renders a finished artifact (SVG produced by Mermaid.js, KaTeX HTML produced from LaTeX, etc.) and that is its entire role.
2. **Structured accessibility always available.** Even when the WebView is rendering, the AT-friendly representation (MathCAT speech for math, structured description for diagrams, etc.) is exposed independently in the platform a11y tree — through a sibling control, an a11y label, or a "describe this" command. Screen-reader users never depend on the WebView to understand the content.
3. **Fully isolated.** No JavaScript-to-native bridge. No network access. No external scripts. Only vendored library code (Mermaid.js bundled in the app, KaTeX bundled in the app). The WebView is a sandbox that renders pixels and nothing else.

**Currently in scope for use:**

- Android Mermaid rendering fallback (when AndroidSVG can't display a specific diagram).
- Android math rendering fallback (if KaTeX-via-WebView is needed for visual parity).

**Not in scope:**

- Any editor surface on any platform.
- Mac, iOS, Windows webview use (still prohibited).
- Any case where the WebView would carry user-typed or user-selected interactive content.

The rule generalizes to future render-only surfaces (e.g., niche syntax highlighting for languages without Rust-side support). Each use is a deliberate, named exception captured in this document or its successors — not a relaxation of the overall rule.

---

## 12. Explicitly deferred questions

No open architectural questions remain at the level of this document. Future decisions belong in per-feature ADRs (`docs/adr/`) once implementation work begins.

**Previously deferred, now resolved:**
- Editor data model, sync direction, accessible conflict resolution — see Section 7.
- Syntax highlighting at scale, semantic spans for AT — see Section 6.4.
- Citations, bibliography, footnotes — see Section 6.5.
- Search and Bases / Dataview-style queries — see Section 8.
- Performance and scaling — see Section 9.
- Plugin / extensibility model, Obsidian migration path — see Section 10.

---

## 13. References

### Locked dependencies

- **Rust:** `pulldown-cmark`, `pulldown-latex`, `MathCAT`, `mermaid-rs-renderer`, `rusqlite`, `notify`, `blake3`
- **FFI:** `uniffi-rs`, `csbindgen`
- **Mac / iOS:** SwiftUI, `LaTeXSwiftUI`, SVGKit
- **Windows:** WPF, AvalonEdit, WPFMath / xaml-math, SharpVectors
- **Android:** Jetpack Compose, AndroidSVG, KaTeX (bundled for WebView fallback only)

### Potential collaborators and expertise sources

(See also memory entry `reference_a11y_collaborators.md`.)

- **Math accessibility:** Neil Soiffer (MathCAT, DAISY); Volker Sorge (SRE, MathJax accessibility); Davide Cervone (MathJax)
- **Scientific publishing accessibility:** Jupyter Accessibility SIG; Quarto / Posit team; MyST community
- **Standards:** W3C MathML refresh working group; WAI APG authors
- **AT user advocacy:** NFB, AFB, RNIB, Hadley
- **Communities:** AccessHigherGround; NVDA users mailing list; r/Blind (when framed respectfully); a11y Slack

**Engagement principle:** when asking AT users or accessibility experts for feedback beyond casual community participation, pay them.

### Superseded by this document

This document supersedes the following parts of `04_draft_architecture_plan.md`:

- The Flutter UI framework decision (replaced by native-per-platform: SwiftUI, WPF, Compose).
- The plugin-runtime-in-V4 assumption (now a deferred open question with no JavaScript runtime assumed).
- The "open vault read-only first" framing (still a safety principle but not the literal V1 mode).
- The graph engine package picks (`graphview`, `force_directed_graphview` — Flutter-specific, no longer relevant).

The principles, accessibility-first orientation, and Bases / Graph / sync ambitions from `01_detailed_roadmap.md` remain in force. The technical stack to deliver them has changed.

---

**Document status: locked at the level of stack and pipelines. Refinement of API details, error model, and per-feature design will follow in subsequent ADRs (`docs/adr/` — to be created when the first ADR is needed).**
