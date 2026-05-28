# Technology Stack

**Analysis Date:** 2026-05-28

## Languages

**Primary:**
- Rust (edition 2021, min 1.89) — core engine, parsing, database, FFI (`crates/slate-core/`, `crates/slate-uniffi/`)
- Swift 6.3.2 — macOS SwiftUI application (`apps/slate-mac/Sources/SlateMac/`)

**Secondary:**
- Bash — build orchestration and CI scripts (`scripts/`)
- Python — license-header fixer (`scripts/apply-license-header.py`)
- SQL — embedded SQLite migrations (`crates/slate-core/migrations/*.sql`)

## Runtime

**Environment:**
- macOS 13+ (Ventura minimum, targeting arm64; current dev machine: macOS 26 arm64)
- No server-side runtime; entirely local desktop application

**Package Manager:**
- Rust: Cargo (workspace resolver v2), lockfile at `Cargo.lock`
- Swift: Swift Package Manager, lockfile at `apps/slate-mac/Package.resolved`

## Frameworks

**Core (Rust):**
- `pulldown-cmark` 0.10 — Markdown parsing (event-stream model, used in links, code, templates)
- `rusqlite` 0.31 (bundled SQLite) — embedded per-vault SQLite database
- `uniffi` 0.28 — FFI binding generator; produces Swift bindings from Rust annotated API
- `tree-sitter` 0.26 — incremental code-block parsing for syntax analysis
- `mathcat` 0.7.6-beta.4 — LaTeX → MathML → speech + braille (NVDA-derived, `include-zip` feature bundles rules)
- `pulldown-latex` 0.7 — LaTeX → MathML conversion
- `mermaid-rs-renderer` 0.2 — pure-Rust Mermaid diagram → SVG rendering
- `hayagriva` 0.9 — BibTeX/BibLaTeX/CSL citation and bibliography engine (Typst project)
- `blake3` 1 — content hashing for change detection
- `trash` 5 — cross-platform "move to trash" for vault file deletion
- `chrono` 0.4 — date/time formatting for `{{date}}`/`{{time}}` template substitution
- `yaml-rust2` 0.11 — YAML frontmatter parsing (successor to deprecated `serde_yaml`)
- `serde_json` 1 — property value serialization / JSON round-trips
- `thiserror` 1 — error type derivation throughout `slate-core` and `slate-uniffi`
- `libc` 0.2 (Unix only) — `O_NOFOLLOW` symlink-escape defence in templates flow

**Core (Swift):**
- SwiftUI — primary UI framework (`apps/slate-mac/Sources/SlateMac/`)
- AppKit — NSOpenPanel, NSWorkspace, NSTextView, accessibility bridge, contrast detection
- Combine — reactive state in `AppState.swift`
- Foundation — FileManager, UserDefaults, JSONSerialization, URL
- `LaTeXSwiftUI` 1.5.0 — renders LaTeX math expressions to native SwiftUI views (via MathJaxSwift)
- `SwiftDraw` 0.27.0 — renders SVG content to AppKit (used by `MermaidView.swift`)

**Testing:**
- Rust: built-in `cargo test` + `criterion` 0.5 benchmarking framework
- Swift: XCTest (`apps/slate-mac/Tests/SlateMacTests/`)
- Property-based: `proptest` 1 (Rust, dev-dependency, links round-trip integration tests)

**Build/Dev:**
- `rustup` 1.29 / stable channel (pinned via `rust-toolchain.toml`)
- `uniffi-bindgen` binary (`crates/slate-uniffi/src/bin/uniffi-bindgen.rs`) — generates Swift FFI glue
- `scripts/build-mac-app.sh` — full build pipeline: cargo → uniffi-bindgen → swift build → optional .app bundle
- `scripts/build-swift-cli.sh` — Swift command-line smoke test
- `Makefile` — thin wrappers (`make ci`, `make mac-app`, `make bench`, etc.)

## Key Dependencies

**Critical:**
- `rusqlite` 0.31 (bundled) — embeds SQLite 3 directly; no external DB daemon required. WAL mode, NORMAL sync, in-memory temp store, foreign keys enabled per `crates/slate-core/src/db.rs`
- `uniffi` 0.28 — the sole mechanism bridging Rust (slate-core) to Swift (SlateMac); regenerated on every build via `uniffi-bindgen`
- `mathcat` 0.7.6-beta.4 — uses thread-local state internally; all calls are serialized through a dedicated Rust worker thread (see `crates/slate-core/src/math.rs`)
- `tree-sitter` 0.26 + 15 compiled-in grammars — syntax analysis for code blocks; grammars are statically linked, not dynamically loaded

**Tree-sitter grammar set (compiled in):**
- `tree-sitter-rust` 0.24, `tree-sitter-swift` 0.7, `tree-sitter-python` 0.25
- `tree-sitter-javascript` 0.25, `tree-sitter-typescript` 0.23
- `tree-sitter-md` 0.5, `tree-sitter-yaml` 0.7, `tree-sitter-json` 0.24
- `tree-sitter-bash` 0.25, `tree-sitter-sequel` 0.3 (third-party SQL; note: not tree-sitter org)
- `tree-sitter-html` 0.23, `tree-sitter-css` 0.25, `tree-sitter-go` 0.25
- `tree-sitter-c` 0.24, `tree-sitter-cpp` 0.23

**Swift package dependencies:**
- `LaTeXSwiftUI` 1.5.0 (transitively includes `MathJaxSwift` 3.5.0, `swift-html-entities` 4.0.1)
- `SwiftDraw` 0.27.0

**Infrastructure:**
- `blake3` 1 — used for content-change detection in vault scan
- `trash` 5 — OS-native trash instead of unlink for vault file operations
- `tempfile` 3 — safe temp-file creation in tests and atomic writes

## Configuration

**Environment:**
- No `.env` files present; application is fully local with no cloud configuration required
- Per-vault config stored in `<vault>/.slate/prefs.json` (citations bibliography sources, CSL styles)
- App preferences stored in macOS `UserDefaults.standard` (namespaced under `slate.prefs.*`)
- Recent-vault history stored in `~/Library/Application Support/` JSON file

**Build:**
- `rust-toolchain.toml` — pins Rust stable channel, includes `rustfmt` + `clippy` components
- `apps/slate-mac/Package.swift` — SwiftPM manifest (swift-tools-version 5.9, macOS 13 platform)
- `apps/slate-mac/Package.resolved` — pinned Swift dependency versions (version 2 format)
- `Cargo.toml` — workspace root; `Cargo.lock` — pinned Rust dependency versions
- LTO: `thin` in release profile; symbols stripped in release builds

## Platform Requirements

**Development:**
- macOS (arm64 primary; APFS for case-insensitive path handling)
- Rust stable toolchain via `rustup`
- Swift 6+ (swift build, swift test)
- `cargo`, `swift` on PATH (sourced from `~/.cargo/env` and Xcode CLT)
- Optional: `a11y-check` CLI for local accessibility validation

**Production:**
- macOS 13+ desktop application
- Fully offline; all data stored in local vault directories (plain Markdown files + SQLite)
- Distributed as `.app` bundle (ad-hoc signed for local dev; production distribution requires Apple Developer signing identity)

---

*Stack analysis: 2026-05-28*
