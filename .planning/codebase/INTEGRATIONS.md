# External Integrations

**Analysis Date:** 2026-05-28

## APIs & External Services

**DOI Resolution:**
- Service: `doi.org` — used to open citation DOI URLs in the system browser
  - SDK/Client: none; URLs constructed as `https://doi.org/<doi>` and handed to `NSWorkspace.shared.open`
  - Auth: none required
  - Location: `apps/slate-mac/Sources/SlateMac/CitationPopover.swift` (line 234)

**No other external APIs.** The application makes no network requests from the Rust core or Swift UI layers (other than DOI URL hand-off to the OS). There are no REST clients, WebSocket connections, or background sync calls.

## Data Storage

**Databases:**
- SQLite (embedded via `rusqlite` 0.31 bundled feature)
  - One database per vault, stored at `<vault>/.slate/slate.db`
  - Connection config: WAL journal mode, NORMAL synchronous, in-memory temp store, foreign keys ON
  - Client: `rusqlite` (no ORM; hand-written SQL across `*_db.rs` modules)
  - 13 versioned migrations: `crates/slate-core/migrations/001_init.sql` through `013_citations.sql`
  - Schema covers: files, headings, links (outgoing + backlinks), properties, FTS5 full-text search, tasks, blocks, citations

**File Storage:**
- Local filesystem only — vault directories are plain Markdown files (`*.md`) managed directly by `slate-core`
- Per-vault config: `<vault>/.slate/prefs.json` (bibliography sources, CSL styles)
- App support data: `~/Library/Application Support/` — recent vaults list (`RecentVaultsStore.swift`), command palette recents (`CommandPaletteRecentsStore.swift`)
- Preferences: `UserDefaults.standard` — app-level settings (namespaced under `slate.prefs.*`)
- No cloud storage, no iCloud sync, no CloudKit

**Caching:**
- In-process only; no external cache layer
- MathCAT rule files bundled into binary via `include-zip` feature (no runtime file lookup)
- tree-sitter grammars statically linked (no dynamic grammar loading)

## Authentication & Identity

**Auth Provider:** None
- Fully local application; no user accounts, no login, no sessions
- No OAuth, no API keys, no JWT

## Monitoring & Observability

**Error Tracking:** None
- No Sentry, Crashlytics, Bugsnag, or equivalent
- No telemetry collection of any kind

**Logs:**
- Rust: standard `eprintln!` / no structured logging framework
- Swift: no logging framework; print-based debugging only
- No log aggregation, no remote log shipping

## CI/CD & Deployment

**Hosting:**
- Local macOS desktop app; no server hosting required
- Distribution: `.app` bundle (built by `scripts/build-mac-app.sh`); ad-hoc signed for dev

**CI Pipeline:** GitHub Actions (`.github/workflows/`)
- `rust.yml` — runs on `macos-14`: `cargo fmt --check`, `cargo clippy`, `cargo test`, `cargo bench --no-run`
- `swift-tests.yml` — runs on `macos-14`: full `scripts/build-mac-app.sh --skip-a11y-check` + `swift test`
- `a11y-check.yml` — runs on `macos-14`: cvs-health/ios-swiftui-accessibility-techniques `a11y-check` static analyzer, SARIF upload to GitHub Code Scanning, PR score comment, score floor enforcement (MIN_SCORE=100/100)
- `license-headers.yml` — runs on `ubuntu-latest`: SPDX header guard on all `.rs`/`.swift` files
- Cache: `Swatinem/rust-cache` v2 for `target/` + `~/.cargo/registry`; pinned a11y-check binary by commit SHA

**Pinned GitHub Actions SHAs:** All actions pinned to full commit SHAs for supply-chain hardening (no floating version tags).

## Environment Configuration

**Required env vars:** None
- No API keys, database URLs, or secrets required to build or run
- `DYLD_LIBRARY_PATH` set at runtime to point at `target/debug` or `target/release` for the `libslate_uniffi.dylib` dynamic library

**Secrets location:** None — no secrets management system used

## Webhooks & Callbacks

**Incoming:** None
**Outgoing:** None (DOI link hand-off to OS browser is not a programmatic webhook)

## Accessibility Infrastructure

**a11y-check static analyzer** (cvs-health/ios-swiftui-accessibility-techniques):
- Pinned commit: `bcaddd56931ce14d32cebcf42ea9f5b08ed5f7d8`
- Scope: `apps/slate-mac/Sources/SlateMac/`
- Rules: 34 rules across 19 WCAG 2.2 criteria
- Score floor: 100/100 (any drop fails CI)
- SARIF results uploaded to GitHub Code Scanning
- Install locally: `brew tap cvs-health/ios-swiftui-accessibility-techniques ... && brew install --HEAD a11y-check`

**macOS Accessibility APIs (runtime):**
- `NSWorkspace.accessibilityDisplayOptionsDidChangeNotification` — observed by `NoteEditorView.swift` and `CodeBlockView.swift` to respond to contrast preference changes
- `NSWorkspace.shared.accessibilityDisplayShouldIncreaseContrast` — read to select high-contrast syntax palette in `EditorSyntaxPalette.swift`
- SwiftUI `.accessibilityLabel`, `.accessibilityHint`, `.accessibilityHeading` — used extensively throughout the UI for VoiceOver support
- MathCAT (`mathcat` crate) generates speech descriptions and Braille output for math expressions; this output populates `.accessibilityLabel` on `MathView`

---

*Integration audit: 2026-05-28*
