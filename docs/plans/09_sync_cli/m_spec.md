# M executable spec — Sync detection + diagnostics + CLI v1

Issues: [#532](https://github.com/coryj627/slate/issues/532) (M-1) · [#533](https://github.com/coryj627/slate/issues/533) (M-2) · [#534](https://github.com/coryj627/slate/issues/534) (M-3) · [#535](https://github.com/coryj627/slate/issues/535) (M-4) · [#536](https://github.com/coryj627/slate/issues/536) (M-5) · [#537](https://github.com/coryj627/slate/issues/537) (M-6).
Milestone: [GH 13](https://github.com/coryj627/slate/milestone/13). One PR per issue.
Plan: [00_plan.md](00_plan.md). U-program Presentation-Ready DoD applies to M-3; backend norms
(fmt/clippy pre-push, censuses for correctness invariants, one PR per issue) apply throughout.

**Execution order: M-1 → (M-2 → M-3) ∥ (M-4 → M-5 ∥ M-6).**

Baseline facts (verified 2026-07-03, this worktree):

- No sync-detection code exists anywhere in `crates/` (grepped: only incidental uses of the word).
- Workspace members are `crates/slate-core`, `crates/slate-uniffi` (root `Cargo.toml:3-6`); no
  binary target, no `clap` anywhere. `serde_json`, `tempfile`, `thiserror` are already workspace deps.
- `VaultSession::from_filesystem(root: PathBuf)` (session.rs:591) is the desktop entry; sessions
  don't store the root as a field, but `vault_root_for_bibliography` already derives it as
  `cache_dir.parent()` (session.rs:2418-2430) — M-1 refactors that derivation into a shared
  helper rather than adding a second rooted-ness convention.
- `CancelToken` = `Arc<AtomicBool>` with `cancel()` / `is_cancelled()` (session.rs:379-396); taken by
  `scan_initial` (652) and `full_text_search` (2031).
- `Paging { cursor: Option<String>, limit: u32 }` / `Page<T> { items, next_cursor, total_filtered }`
  (session.rs:337-373).
- Relevant read APIs the CLI wraps (session.rs): `scan_initial` :652,
  `list_files(FileFilter, Paging)` :1078, `read_text` :706, `full_text_search(&str, &SearchScope,
  &CancelToken) -> QueryResultSet` :2031, `tasks_in_vault(TaskFilter, Paging)` :2017,
  `list_templates()` :2060, `render_template(&str, TemplateContext)` :2145,
  `files_with_property(key, value, Paging)` :1461, `backlinks(path, Paging)` :1407,
  `outgoing_links(path)` :1119.
- `QueryResultSet { rows: Vec<QueryHit>, summary: String }`; `QueryHit { path, snippet, score }` with
  STX/ETX (`\u{0002}`/`\u{0003}`) hit markers in `snippet` (search_db.rs:59-88).
- `TaskFilter { completed: Option<bool>, due_from_ms, due_to_ms, priority_at_least }` (tasks_db.rs:30);
  `TaskWithLocation { task: TaskItem, path, file_name }`; `TaskItem` fields incl. `due_ms`,
  `status_char`, `line` (tasks.rs:51-64).
- `TemplateContext { now_ms, title, vault_name, prompt_values: HashMap<String,String> }`
  (templates.rs:86-98); unknown/unfilled `{{prompt:…}}` markers render as literal text (templates.rs:94-96);
  `RenderedTemplate { body, cursor_byte_offset }`.
- `VaultError` variants (lib.rs:102-193) include `Cancelled`, `InvalidPath`, `InvalidQuery`,
  `Unsupported { feature }`, `PrefsUnreadable`.
- uniffi mirroring = record + `From` impl + `#[uniffi::export]` on the `VaultSession` object wrapper
  (slate-uniffi/src/lib.rs:240-259); Swift bindings regenerate via `scripts/build-mac-app.sh:61-75`.
- Leaf registry: `Leaf` enum + `Leaf.registered` + `leafContent(_:)` switch in
  `apps/slate-mac/Sources/SlateMac/Workspace/RightPaneView.swift`; panels follow the
  `LeafEmptyState` / `LeafSection` states discipline (ContentBlockPanels.swift); AppState FFI calls
  dispatch via `Task.detached` + `@MainActor` publish (AppState.swift:2867 pattern); announcements
  via `postAccessibilityAnnouncement(_:priority:)`.
- Scanner skips dot-prefixed entries, so sync markers never reach the SQLite index — detectors must
  probe the filesystem (plan decision #5).
- Census convention: `census_*` test fns in `crates/slate-core/src/session/tests/*.rs`, scale
  driven by the `SLATE_CENSUS_FULL=1` env gate via `census_scale()` (see
  session/tests/link_integrity.rs:10-19); censuses run in release as part of the standing
  red-team protocol.

---

## M-1 · Sync detector engine (#532) — PR 1

### Rust: new module `crates/slate-core/src/sync_detect.rs`

```rust
pub enum SyncProviderKind { LiveSync, ICloudDrive, Dropbox, OneDrive, GoogleDrive, Git, Syncthing }
pub enum RiskLevel { Low, Medium, High }

pub struct DetectedSyncProvider {
    pub kind: SyncProviderKind,
    /// Markers that produced the detection. Vault-relative when the marker is
    /// inside the vault; absolute when it is an ancestor/location signal.
    pub evidence_paths: Vec<String>,
    pub risk_level: RiskLevel,
    /// Full recommendation sentence(s) — the exact user-facing copy (table below).
    pub recommendation: String,
}

pub struct SyncDetectionReport {
    pub providers: Vec<DetectedSyncProvider>,   // detector-table order, deterministic
    /// Some(copy) when ≥ 2 providers with risk >= Medium are detected.
    pub multi_sync_warning: Option<String>,
    /// Pre-rendered VoiceOver summary, same pattern as QueryResultSet.summary:
    /// "No sync systems detected." / "1 sync system detected: iCloud Drive." /
    /// "3 sync systems detected: LiveSync, iCloud Drive, Git. Warning: multiple
    ///  sync systems on one vault."
    pub audio_summary: String,
    /// False when the session has no filesystem root (provider-abstracted
    /// session): detection unsupported, providers empty.
    pub supported: bool,
}

/// Pure function; no SQLite, no session state. Probes are exact paths +
/// xattr/prefix checks — never an unbounded directory walk.
pub fn detect_sync_providers(vault_root: &Path) -> SyncDetectionReport
```

`VaultSession` gains:

```rust
pub fn detect_sync(&self) -> Result<SyncDetectionReport, VaultError>
// Root resolution: the SAME convention vault_root_for_bibliography already uses
// (session.rs:2418-2430, cache_dir.parent() for the .slate layout) — refactor that
// derivation into one shared private fs_root() helper consumed by both features, so a
// session is never "rooted" for bibliography but "unsupported" for sync detection.
// fs_root() None → Ok(report with supported=false, providers=[],
// audio_summary = "Sync detection isn't available for this vault type.") — NOT an
// error: the Swift caller renders from the flag.
```

Synchronous and cheap (< 20 fixed probes); no `CancelToken`. Host calls it off-main like any FFI call.

### Detector table (normative — evidence rules and copy are the contract)

| Kind | Fires when (ALL probes are exact paths) | Risk | Recommendation copy |
|---|---|---|---|
| `LiveSync` | dir `{root}/.obsidian/plugins/obsidian-livesync/` exists AND contains `manifest.json` or `data.json` | High | "Self-hosted LiveSync replicates your saves at the file level. Avoid editing the same note simultaneously in Slate and Obsidian." |
| `ICloudDrive` | (a) canonicalized root has prefix `$HOME/Library/Mobile Documents/`, OR (b) root or any ancestor up to `$HOME` carries the `com.apple.fileprovider.fpfs#P` or legacy `com.apple.clouddocs.*` xattr, OR (c) ≥ 1 `*.icloud` placeholder among the direct children of the vault root (one `read_dir` of the root only — placeholders elsewhere are caught by (a)/(b)) | Medium | "This vault is inside iCloud Drive. Files may be evicted to the cloud; Slate reads them transparently, but first-touch latency can spike and mid-write eviction is outside Slate's control." |
| `Dropbox` | (a) file `{root}/.dropbox` or dir `{root}/.dropbox.cache` exists, OR (b) any ancestor directory of root (walking to `/`) contains `.dropbox.cache`, OR (c) canonicalized root has prefix `$HOME/Library/CloudStorage/Dropbox` | Medium | "This vault is inside a Dropbox-synced folder. Dropbox replicates whole files on save; concurrent edits from another device can produce conflicted copies." |
| `OneDrive` | (a) canonicalized root path contains a component exactly `OneDrive` or starting `OneDrive-`, OR (b) marker file `{root}/.849C9593-D756-4E56-8D6E-42412F2A707B` exists (OneDrive sync-root marker) | Medium | "This vault is inside a OneDrive-synced folder. OneDrive replicates whole files on save; concurrent edits from another device can produce conflicted copies." |
| `GoogleDrive` | (a) canonicalized root has prefix `$HOME/Library/CloudStorage/GoogleDrive-`, OR (b) dir `{root}/.tmp.driveupload` or `{root}/.tmp.drivedownload` exists | Medium | "This vault is inside a Google Drive–synced folder. Drive replicates whole files on save; concurrent edits from another device can produce conflicted copies." |
| `Git` | `{root}/.git` exists (dir **or** file — worktrees use a `.git` file) | Low | "This vault is a Git working tree. Slate's writes go through the working tree like any editor; commit on your own cadence." |
| `Syncthing` | dir `{root}/.stfolder/` or file `{root}/.stignore` exists | Medium | "This vault is a Syncthing folder. Syncthing replicates whole files on save; concurrent edits from another device can produce conflicts." |

Rules:
- Display names (normative — used in `audio_summary`, human output, and the M-3 row labels):
  `LiveSync` → "LiveSync", `ICloudDrive` → "iCloud Drive", `Dropbox` → "Dropbox",
  `OneDrive` → "OneDrive", `GoogleDrive` → "Google Drive", `Git` → "Git",
  `Syncthing` → "Syncthing". Exposed as `SyncProviderKind::display_name()`.
- `$HOME` = `std::env::var_os("HOME")`; when unset, prefix probes are skipped (marker probes still run).
- Canonicalization: `std::fs::canonicalize(root)`; on failure fall back to the raw path (detection
  degrades, never errors).
- Ancestor walks are bounded by path depth (walk `root.ancestors()`), never `read_dir` on ancestors
  except the single named-marker existence checks listed.
- xattr probe: `rustix`/manual `listxattr` — **no new heavyweight dep**; if a light crate is needed,
  `xattr = "1"` is the approved pick (fixture tests stub the probe seam; see below).
- `multi_sync_warning` copy: "Multiple sync systems are managing this vault (<display names of the
  risk ≥ Medium providers only, comma-joined>). Consider disabling all but one — overlapping sync
  tools can corrupt each other's state." (Git never appears in the warning; it still appears in
  `providers`.)
- Ordering: `providers` sorted by the table order above (LiveSync, iCloud, Dropbox, OneDrive,
  GoogleDrive, Git, Syncthing) so output is deterministic for tests and TSV consumers.

### Probe seam (testability)

`detect_sync_providers` delegates probes through a private `trait FsProbe { fn exists(&Path) -> bool;
fn is_dir(&Path) -> bool; fn read_dir_names(&Path) -> Vec<String>; fn xattr_names(&Path) ->
Vec<String>; fn canonicalize(&Path) -> Option<PathBuf>; fn home() -> Option<PathBuf> }` with a real
impl and a test impl. Fixture tests exercise the full detector table without touching real `$HOME`
or xattrs; an additional integration test runs the real impl against a `tempfile` vault seeded with
the marker files (xattr/`$HOME`-prefix arms are covered only by the seam tests — CI runners can't
plant xattrs reliably).

### uniffi (slate-uniffi/src/lib.rs)

Mirror `SyncProviderKind`, `RiskLevel` (uniffi::Enum), `DetectedSyncProvider`,
`SyncDetectionReport` (uniffi::Record) with `From` impls; export `detect_sync()` on the
`VaultSession` object. Regenerate Swift bindings via `scripts/build-mac-app.sh`.

### Tests (Rust)

- One positive fixture per detector row (each probe arm hit at least once across the suite:
  LiveSync-with-manifest, LiveSync-with-data-json, `.git`-dir, `.git`-file, `.stfolder`, `.stignore`,
  Dropbox marker file / cache dir / ancestor cache dir / CloudStorage prefix, OneDrive path
  component / marker file, GoogleDrive prefix / tmp dirs, iCloud prefix / xattr / placeholder-child).
- Negative census: `census_sync_detect_no_false_positives` — 10k randomized vaults (random nested
  dirs/files with names drawn from lookalikes: `dropbox-notes.md`, `git/`, `stfolder.md`,
  `OneDriveBackup.md` as a *file*, `.gitignore`, `.obsidian/plugins/other-plugin/`) → zero
  detections on every one. Lookalike list is part of the fixture, extended whenever a false
  positive is ever found in the wild.
- Multi-sync: LiveSync+iCloud fixture → both providers + `multi_sync_warning` populated; Git+iCloud
  → two providers, **no** multi-sync warning (only one is ≥ Medium)… **correction:** iCloud is
  Medium and Git is Low — the rule is ≥ 2 providers of risk ≥ Medium, so Git+iCloud yields no
  warning; Dropbox+iCloud yields one. Both cases pinned.
- Determinism: detection of a fixture with all seven systems returns providers in table order.
- `supported=false` path: session opened with a mock provider (no fs root) → empty report, no error.

## M-2 · LiveSync config reader (#533) — PR 2

### Rust: `sync_detect.rs` additions

```rust
pub enum LiveSyncConfigStatus {
    NotPresent,                       // no data.json
    Parsed(LiveSyncConfig),
    Malformed { reason: String },     // unreadable/unparseable — never a hard error
}

pub struct LiveSyncConfig {
    /// Host (+ optional port) extracted from couchDB_URI. NEVER contains
    /// userinfo, path, query, or fragment. None when the URI is absent/unparseable.
    pub server_host: Option<String>,
    /// couchDB_DBNAME verbatim (a database name, not a credential).
    pub database: Option<String>,
    pub live_sync_enabled: Option<bool>,   // "liveSync"
    pub sync_on_save: Option<bool>,        // "syncOnSave"
    pub sync_on_start: Option<bool>,       // "syncOnStart"
    pub end_to_end_encryption: Option<bool>, // "encrypt"
}

pub fn read_livesync_config(vault_root: &Path) -> LiveSyncConfigStatus
```

`VaultSession::livesync_config(&self) -> Result<LiveSyncConfigStatus, VaultError>` (same
`fs_root` rule as `detect_sync`: no root → `Ok(NotPresent)`).

Rules (normative):
- Source file: `{root}/.obsidian/plugins/obsidian-livesync/data.json`, parsed with
  `serde_json::Value` (field-tolerant: absent fields → `None`; the plugin's schema drifts).
- Host extraction from `couchDB_URI` string, manual (no `url` crate): strip `scheme://`, cut at
  first `/`, then drop everything up to and including a `@` if present (userinfo), keep
  `host[:port]`. Extraction failure → `server_host: None`, still `Parsed`.
- **Credential blacklist is structural:** only the six allow-listed fields above are ever read out
  of the JSON. `couchDB_USER`, `couchDB_PASSWORD`, `passphrase`, and every other key are never
  copied into any output type. No "redaction" — fields that aren't read can't leak.
- File > 1 MiB → `Malformed { reason: "config file too large" }` (defensive bound).

### uniffi

Mirror `LiveSyncConfigStatus` (uniffi::Enum with associated record) + `LiveSyncConfig`
(uniffi::Record); export `livesync_config()`.

### Tests

- Round-trip: realistic `data.json` fixture (with planted credentials `"couchDB_USER": "alice"`,
  `"couchDB_PASSWORD": "hunter2"`, `"passphrase": "secret123"`) → `Parsed`; serialize the entire
  output struct with `format!("{:?}")` and assert it contains **none** of the planted credential
  substrings. This is the credential-safety gate from the DoD.
- URI variants: `https://user:pass@couch.example.com:5984/db` → host `couch.example.com:5984`;
  `http://10.0.0.5:5984` → `10.0.0.5:5984`; garbage URI → `server_host: None`.
- Missing file → `NotPresent`; invalid JSON → `Malformed` with nonempty reason; partial JSON
  (only `couchDB_DBNAME`) → `Parsed` with the rest `None`. None of these panic (fuzz 1k random
  byte-strings through the parser).

## M-3 · Sync diagnostics leaf (#534) — PR 3

### Leaf registration

- `Leaf` gains `case syncDiagnostics` — title "Sync", symbol `.syncDiagnostics` (SlateSymbol table
  below), appended to `Leaf.registered` **after** `.bibliography` (last position: vault-level
  diagnostics, least-frequently visited; the registry order comment in `RightPaneView.swift`
  updated). `leafContent` switch gains `case .syncDiagnostics: SyncDiagnosticsPanel()`.
- Persistence: `rawValue = "syncDiagnostics"` round-trips through the existing
  `Leaf.init(persisted:)`; older builds decode it to `.outline` by the existing unknown-token
  fallback — no migration needed.

### AppState

```swift
@Published private(set) var syncReport: SyncDetectionReport?
@Published private(set) var liveSyncConfig: LiveSyncConfigStatus?
@Published private(set) var syncDiagnosticsError: String?
```

- Loaded once per vault open, from the post-scan continuation (the same funnel that seeds file
  lists), via `Task.detached` → `session.detectSync()` + `session.livesyncConfig()` → publish on
  `@MainActor`. Errors land in `syncDiagnosticsError` (specific message, panel renders it).
- A `refreshSyncDiagnostics()` method re-runs both calls (wired to a "Refresh" button in the panel
  header; also the command registry entry `slate.diagnostics.refreshSync`, palette name
  "Refresh sync diagnostics"). **Registry + menu, not palette-only:** the registry's invariant is
  menu↔palette unification and the drift test scrapes menu source (SlateCommands.swift:170-182,
  SlateCommandsTests.swift:84) — so the command also gets a menu item: **View menu,
  `CommandSection.view`, following the workspace-tabs precedent (SlateCommands.swift:36)**. This
  is the normative home for panel-scoped commands; O-5's `slate.history.showPanel` uses the same
  one (the two specs converge here by explicit cross-reference, not convention). Drift test
  updated.
- **Announcement (assertive):** after first load per vault, iff `multi_sync_warning != nil` OR any
  provider has `riskLevel == .high`, post the report's `audioSummary` at `.high` priority.
  Exactly once per vault open (gate on vault identity, the `announcedFilePath`-style pattern from
  CitationsPanel.swift:27-148). No announcement for low/medium-only or empty reports — the leaf is
  discoverable, not shouty. **Testability seam:** announcements route through an init-injected
  `AnnouncementPosting` protocol on AppState — normative shape:
  `protocol AnnouncementPosting { func post(_ message: String, priority: AnnouncementPriority) }`,
  default impl wraps the global `postAccessibilityAnnouncement` (which early-returns when
  `NSApp == nil` and is therefore un-spyable in tests). This definition is shared with O-5:
  whichever PR lands first creates exactly this seam, the other reuses it. The gate tests assert
  against a recording fake.

### `SyncDiagnosticsPanel.swift`

States (LeafSection/LeafEmptyState discipline):
- Unsupported (`supported == false`): `LeafEmptyState("Sync detection isn't available for this vault type.")`
- Loading: `ProgressView()` row.
- Error: specific message + Retry button (calls `refreshSyncDiagnostics`).
- Empty (`providers.isEmpty`): `LeafEmptyState("No sync systems detected.")`
- Populated: `LeafSection` header "Sync, N systems detected" (`.isHeader` trait) containing:
  1. Multi-sync warning row first when present: the **existing** `SlateSymbol.warning` role
     (SlateSymbol.swift:42, `exclamationmark.triangle.fill` — reuse, don't add a non-fill twin) +
     copy; `.accessibilityLabel("Warning: \(copy)")`.
  2. One row per provider: risk badge (icon + text — shape never color alone: High =
     `SlateSymbol.warning`, Medium = `exclamationmark.circle`, Low = `info.circle`; text
     "High risk"/"Medium risk"/"Low risk") + `display_name` + recommendation as wrapping body
     text (no `lineLimit`). Row AX label: "\(displayName): \(riskText). \(recommendation)".
     A `DisclosureGroup` "Evidence" inside each row lists `evidence_paths` in monospace
     (`Tokens.Typography.code`), collapsed by default.
  3. LiveSync config section (only when provider LiveSync detected): `Parsed` → labeled rows
     (Server host / Database / Live sync / Sync on save / Sync on start / End-to-end encryption;
     booleans render "On"/"Off", absent render "Unknown"); `Malformed` → "LiveSync config could
     not be read: \(reason)"; `NotPresent` while LiveSync detected → "LiveSync plugin present;
     no config found."
- Badge colors through tokens: High reuses the existing `destructive*` text role family
  (DesignTokens.swift:59-75); Medium gets a **new** `warningText` token (no warning role exists in
  `Tokens.ColorRole` today — add it plus its `contrastPairings` entry so the automated APCA gate
  covers it); Low uses `textSecondary`. APCA Lc ≥ 75 measured for badge text on its background in
  both appearances, numbers in the PR.

### Tests (XCTest)

- Panel state matrix: unsupported / loading / error / empty / single-Low / single-High /
  multi-sync / LiveSync-with-config / LiveSync-malformed-config — each renders with expected
  labels (view-inspection).
- Announcement gate: High-risk report announces once per vault open (spy on the announcement
  helper); Medium-only report does not announce; reopening the same vault doesn't re-announce;
  opening a different vault re-arms.
- Leaf registry: `Leaf.registered` contains `.syncDiagnostics` last; persistence round-trip;
  unknown-token fallback still `.outline`.
- Command drift test includes `slate.diagnostics.refreshSync`.
- Appearance snapshots (both modes) for the populated multi-provider state; `a11y-check` 100 at
  the PR tip.

## M-4 · `slate-cli` scaffold: formats, exit codes, Ctrl-C, `open`, `sync-check` (#535) — PR 4

### Crate layout

- New workspace member `crates/slate-cli`, `[[bin]] name = "slate"`, depends on `slate-core` (NOT
  `slate-uniffi` — the CLI is a native Rust consumer; no FFI layer in the loop).
- New deps (workspace-pinned): `clap = { version = "4", features = ["derive"] }`, `ctrlc = "3"`.
  Dev-deps: `assert_cmd = "2"`, `predicates = "3"` (+ existing `tempfile`, `serde_json`).
- `src/main.rs` (arg parsing + dispatch only), `src/commands/<cmd>.rs` one module per command,
  `src/output.rs` (format layer), `src/session.rs` (open-vault helper). **No business logic in the
  CLI layer** — every command is a thin wrapper over `VaultSession` calls; anything smarter moves
  into slate-core first (locked principle from the GH milestone).

### Global contract (applies to every *vault-reading* command; encoded once in `output.rs`/`main.rs`)

- Usage: `slate <command> <vault-path> [args…] [--format json|tsv|human]`. `<vault-path>` is always
  the first positional (GH milestone contract) — **for the vault-reading commands**. See the
  meta-command exception below.
- **Meta-command exception**: a command that reads no vault takes no `<vault-path>` (it would have
  nothing to read), the same way `--help`/`--version` take none. `completions <shell>` (#639, the
  clap_complete follow-up filed below) is the one such command in v1: it generates a shell-completion
  script from clap's own grammar. It emits the raw script to stdout (no json envelope, no `--format`),
  keeps stderr empty, and exits 0; an unknown/missing `<shell>` is a clap usage error (exit 2), and a
  broken stdout pipe follows the standard exit-1 `slate: ` discipline (never a panic). Everything else
  in this contract — the exit codes, the stdout=data/stderr=diagnostics split — still applies.
- `--format` default `human`.
  - **json**: single object to stdout:
    `{"schema":"slate.cli.v1","command":"<cmd>","vault":"<abs path>","data":{…}}`. `data` shapes are
    per-command (below) and are a **stability contract** — additive evolution only; breaking changes
    bump `slate.cli.v2`. Pretty-printed (`to_string_pretty`); trailing newline.
  - **tsv**: header row then data rows, `\t`-separated, `\n` row terminator. Any literal tab or
    newline inside a value is replaced by a single space (documented lossy flattening — tsv is the
    cut/awk format; use json for fidelity).
  - **human**: plain lines, screen-reader-friendly: no box-drawing, no column art, no color
    (colorless output unconditionally in v1 — stronger than `NO_COLOR`, which is therefore trivially
    honored; revisit only if testers ask for color).
- stderr carries diagnostics/progress only; stdout carries data only (pipeable).
- Progress: when stderr is a TTY and a scan takes > 1s, print `Indexing… <n> files` lines (coarse,
  1/s max, via `scan_initial_with_progress` listener); never on non-TTY stderr.
- Exit codes: `0` success; `1` runtime error (message on stderr, prefixed `slate: `);
  `2` usage error (clap's default); `130` cancelled by Ctrl-C. `VaultError` → exit 1 with the
  error's `Display` text — every command must produce an *informative* message (the GH milestone
  DoD), asserted per-command in tests.
- Ctrl-C: `ctrlc::set_handler` set **before** session open; handler calls `CancelToken::cancel()` on
  the shared token passed to every cancellable call, and a second Ctrl-C hard-exits (handler
  re-entry → `std::process::exit(130)`). After a cancelled call returns `VaultError::Cancelled`,
  main exits 130. The session drops normally on unwind — SQLite/WAL and the atomic write discipline
  mean no cleanup pass is needed (asserted by the reopen-after-SIGINT test).
- Session opening (`src/session.rs` helper): validate the path is a directory (else exit 1,
  "not a vault directory: <path>"), `VaultSession::from_filesystem`, then `scan_initial` with the
  shared token. SQLite contention with a concurrently-open app: **no new plumbing needed** —
  rusqlite 0.39 applies a 5s `busy_timeout` on every connection open
  (rusqlite `inner_connection.rs:118`); the CLI's only obligation is mapping a post-timeout
  `SQLITE_BUSY`/locked `DbError` to exit 1 with "vault cache is busy (is Slate open?) — retry in
  a moment".

### `slate open <vault-path>`

Vault summary from `ScanReport` (session.rs:402-414) + `list_files` totals.

`data`: `{ "files_seen": u64, "files_indexed": u64, "files_skipped": u64, "bytes_processed": u64,
"markdown_files": u64, "scan_errors": [String], "cache": "warm"|"cold" }`
(`markdown_files` = `list_files(MarkdownOnly, first(1)).total_filtered`; **normative rule:**
`cache` = "cold" iff `.slate/cache.sqlite` did not exist before this run (the session helper
checks before opening), else "warm" — the empty-vault and no-changes cases both read truthfully).
Human: `Vault: <path>` / `Files: N (M markdown)` / `Indexed: fresh|reused cache` / one line per
scan error. tsv: two columns, `field<TAB>value`, one row per scalar field; `scan_errors` joined
with `"; "` into one row.

### `slate sync-check <vault-path>`

Runs M-1 detection + M-2 config read (no scan needed — probes only; skip session open entirely and
call `detect_sync_providers(root)` + `read_livesync_config(root)` directly — the one command that
doesn't build the index).

`data`: `{ "supported": bool, "providers": [{ "kind": "livesync|icloud-drive|dropbox|onedrive|google-drive|git|syncthing", "risk": "low|medium|high", "evidence": [String], "recommendation": String }], "multi_sync_warning": String|null, "livesync_config": { "status": "not-present|parsed|malformed", … } }`.
Human: the audio_summary line, then one indented block per provider (display name, risk,
recommendation, evidence lines), then the LiveSync config block when parsed. tsv: header
`kind risk evidence recommendation`, one row per provider, evidence joined with `";"` —
`multi_sync_warning` and the LiveSync config are json/human-only (documented: "use --format json
for the full report"). Exit code stays 0 even when systems are detected (detection is
information, not failure); scripts branch on json.

### Tests (in `crates/slate-cli/tests/cli.rs`, `assert_cmd` against fixture vaults built with `tempfile`)

- `open` on a seeded vault: human contains counts; json parses, `schema == "slate.cli.v1"`, counts
  match the fixture; second run reports `"cache":"warm"`.
- `sync-check` against a LiveSync+iCloud-marker fixture: json lists both, multi-sync warning
  non-null; against a clean fixture: empty providers, exit 0.
- Nonexistent path → exit 1, stderr contains "not a vault directory". Unknown flag → exit 2.
- stdout/stderr separation: with `--format json`, stdout parses as JSON even when progress/warnings
  were emitted (they went to stderr).
- SIGINT (cfg(unix)): spawn `slate open` on a large generated fixture (5k files), send SIGINT
  ~50ms in → exit code 130; reopen the same vault normally → exit 0 (no cache corruption).

## M-5 · CLI query commands: `read`, `list`, `search`, `links`, `properties` (#536) — PR 5

### slate-core additions (properties need two small queries; SQL over the existing `properties` table)

```rust
pub struct PropertyKeySummary { pub key: String, pub file_count: u64 }
pub fn list_property_keys(&self) -> Result<Vec<PropertyKeySummary>, VaultError>       // DISTINCT key + COUNT(DISTINCT file), key-sorted
pub fn files_with_property_key(&self, key: &str, paging: Paging) -> Result<Page<FileSummary>, VaultError>  // any value
```

Both uniffi-mirrored (the app's future property browser wants them too). Unit tests beside the
existing `files_with_property` tests (session/tests/properties.rs).

### `slate search <vault-path> <query> [--limit N]`

Wraps `full_text_search(query, SearchScope::Vault, cancel)`. `--limit` (default 50) truncates rows
client-side (the API returns ranked rows; record `truncated: bool`).

`data`: `{ "summary": String, "truncated": bool, "hits": [{ "path": String, "snippet": String, "score": f64 }] }`
— snippet with STX/ETX markers **replaced**: json/tsv get `[` `]` around matches… **no — normative:**
markers are replaced by nothing in tsv/human plain output and by `"match_ranges"` in json:
`hits[i].snippet` is the plain snippet (markers stripped) and `hits[i].match_ranges` is
`[{start,end}]` byte ranges into that plain snippet derived from the marker positions. Human:
`path: snippet` per line (grep convention), markers stripped. `InvalidQuery` (FTS5 syntax) → exit 1
with the query error message verbatim.

### `slate read <vault-path> <note-path>` and `slate list <vault-path> [--markdown-only]`

The two remaining read-only verbs from the locked §10 CLI list ("open, read, write, list, search,
query, render" — write and query are the deferred ones, per plan decisions #3 and the N follow-up).
- `read`: existence check via `get_file_metadata` (`None` → exit 1 "no such note: <path>"), then
  `read_text` to stdout verbatim (human); json `data: { "path": String, "content": String }`;
  tsv → exit 2 (document body, same rule as render-template). `FileTooLarge` surfaces as exit 1
  with the session's message.
- `list`: `list_files(All|MarkdownOnly, …)` drained. `data: { "files": [{ "path": String, "name": String, "size_bytes": u64, "mtime_ms": i64 }] }`
  (fields = the `FileSummary` slim shape). tsv columns: `path name size_bytes mtime_ms`. Human:
  one path per line.

### `slate links <vault-path> <note-path>`

Existence check first: `get_file_metadata(path)` → `None` → exit 1 "no such note: <path>"
(`note_load_bundle` itself returns empty results for unknown paths, session.rs:1428-1445 — empty
output must mean "isolated note", never "typo'd path"). Then `note_load_bundle` (backlinks first
page + outgoing); page backlinks to exhaustion via `backlinks(path, …)` `next_cursor` (the CLI is
the one consumer allowed to drain, bounded by vault size).

`data`: `{ "path": String, "backlinks": [{ "source_path": String, "snippet": String }], "outgoing": [{ "target": String, "resolved_path": String|null, "kind": "wikilink"|"markdown", "embed": bool, "external": bool, "unresolved": bool }] }`
— backlink fields pinned to the shipped `Backlink` shape (links_db.rs:44-52): `source_path` +
`snippet` (non-optional String — NOT an invented nullable `context`);
`kind`/`embed`/`external`/`unresolved` mirror the real `OutgoingLink` model (links_db.rs:22-40:
`LinkKind` is only Wikilink|Markdown; embed/external/unresolved are orthogonal flags, links.rs:44-50).
These names are part of the `slate.cli.v1` stability contract. Human: `Backlinks (N):` block then `Outgoing links (M):` block, one per
line, `→ unresolved` suffix for unresolved, `(embed)` suffix for embeds. tsv: header
`direction path kind embed external unresolved`; backlink rows have `direction=in`, `path` = the
linking file, remaining columns empty; outgoing rows have `direction=out`, `path` = target.

### `slate properties <vault-path> [--key <key>]`

- Without `--key`: `list_property_keys` → `data: { "keys": [{ "key": String, "file_count": u64 }] }`;
  human `key<TAB>count` lines (tsv-like even in human — it's a two-column list).
- With `--key`: `files_with_property_key` drained → `data: { "key": String, "files": [String] }`;
  human: one path per line.

### Tests

- Fixture vault with links/properties/content; every command × every supported format round-trips
  (`read` rejects tsv by contract); json parses and field-level asserts hold; search `--limit 1`
  sets `truncated`; `links` on an isolated note → empty blocks, exit 0; `links`/`read` on a
  missing path → exit 1 "no such note"; unresolved link renders the `unresolved` marker; embed
  flag round-trips; `properties --key missing` → empty list, exit 0 (absence isn't an error);
  FTS syntax error → exit 1; SIGINT during `slate search` on a large fixture → exit 130 (the
  search loop checks the token per row — this covers the DoD's "scan/search" cancellation claim
  beyond M-4's scan test).

## M-6 · CLI `tasks` + `render-template` (#537) — PR 6

### `slate tasks <vault-path> [--filter due-today|overdue|this-week|all] [--include-completed]`

Maps to `tasks_in_vault(TaskFilter, …)`, drained. Date math (normative — **UTC calendar days,
matching both the storage convention and the app**): `due_ms` is stored as midnight UTC of the
parsed date (tasks.rs:393-396) and the Mac Tasks panel windows compare against UTC midnight
(AppState.swift:44-115, `startOfTodayUtc`); the CLI MUST return the same sets the app shows.
`TaskFilter.due_to_ms` is **exclusive** (`due_ms < ?`, tasks_db.rs:135-138). With
`today_utc = (now_epoch_ms / 86_400_000) * 86_400_000` (plain integer math — workspace `chrono`
has `default-features = false` and deliberately lacks clock features; `now_epoch_ms` comes from
`std::time::SystemTime`):
- `due-today`: `due_from_ms = today_utc`, `due_to_ms = today_utc + 86_400_000` (exclusive).
- `overdue`: `due_to_ms = today_utc` (exclusive — due *today* is not overdue, same as the app),
  and completed excluded regardless of `--include-completed` (an overdue-done task is nonsense).
- `this-week`: `due_from_ms = today_utc`, `due_to_ms = today_utc + 7 * 86_400_000` (exclusive).
- `all` (default): no due bounds.
- `--include-completed` clears the default `completed: Some(false)` filter (except `overdue`).
- `--help` documents: "Due-date windows use UTC calendar days, matching how Slate stores and
  displays due dates."

`data`: `{ "filter": String, "tasks": [{ "path": String, "file_name": String, "line": u32, "text": String, "status_char": String, "completed": bool, "due": "YYYY-MM-DD"|null, "priority": i32|null }] }`
(`due` = the UTC calendar date of `due_ms` — the date as authored in the note, never
timezone-shifted). tsv columns: `path line status due text`. Human:
`[ ] path:line — text (due YYYY-MM-DD)` with the actual status char inside the brackets.

### `slate render-template <vault-path> <template-path> [--prompt key=value …] [--title T]`

- Builds `TemplateContext { now_ms: now, title: --title or template stem, vault_name: root basename,
  prompt_values: from repeated --prompt }`. `--prompt` value syntax `key=value`, split at the first
  `=` (values may contain `=`); missing `=` → exit 2 (usage).
- Before rendering, scan the template for `{{prompt:…}}` markers via the existing prompt-scan API
  (templates.rs:134); any marker whose key has no supplied value → **warning to stderr**
  (`slate: warning: unfilled prompt 'Label'`) and rendering proceeds with the marker left literal
  (matches the engine's documented behavior, templates.rs:94-96). Exit stays 0 — the output makes
  the gap visible; `--strict` (flag) upgrades unfilled prompts to exit 1 before any output.
- stdout = `RenderedTemplate.body` verbatim (human); json `data: { "body": String, "cursor_byte_offset": u64|null, "unfilled_prompts": [String] }`.
  tsv: not meaningful for a document body → `--format tsv` exits 2 with "tsv not supported for
  render-template".
- Template path resolution: exactly the session's `render_template` semantics (vault-relative);
  missing template → exit 1 with the session's error text.

### Tests

- Tasks fixture with due dates planted relative to a **fixed** reference date; the date-window math
  is factored into a pure fn tested against that reference (the CLI passes `now`; tests inject).
  Filters × formats matrix; `--include-completed` behavior; overdue-excludes-completed pinned.
- Template fixture with two prompts; supplying one → stderr warning names the other, exit 0, marker
  literal in stdout; `--strict` → exit 1, no stdout; both supplied → clean render; `--format tsv`
  → exit 2; json includes `unfilled_prompts`.
- Both commands: SIGINT mid-run inherits the M-4 handler (no per-command work needed — asserted by
  code review, not test).

---

## SlateSymbol additions

| Role | v7 | fallback | PR |
|---|---|---|---|
| `.syncDiagnostics` | `arrow.trianglehead.2.clockwise.rotate.90` | `arrow.triangle.2.circlepath` | M-3 |

(`.warning` already exists — SlateSymbol.swift:42, `exclamationmark.triangle.fill` — M-3 reuses it.)

## Follow-ups to file during M

- `slate query` structured-query command — file when N ships (`enhancement`).
- Live re-detection on sync-marker changes (watcher-driven) — file with M-3 (`enhancement`).
- Localized recommendation copy — all user-facing copy in this milestone is English-only like the
  rest of the app; file the l10n umbrella note if one doesn't exist.
- CLI shell completions (`clap_complete`) — filed as #639 (`enhancement`); **shipped** as the
  `slate completions <shell>` meta-command (see the meta-command exception in the §M-4 global
  contract above).
