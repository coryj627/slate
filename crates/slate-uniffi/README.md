# slate-uniffi

[uniffi-rs](https://github.com/mozilla/uniffi-rs) FFI bindings for [`slate-core`](../slate-core), targeting Swift (Mac, iOS) and Kotlin (Android).

This crate is the FFI boundary between Slate's pure-Rust core and platform-native UI code on Apple and Android. Windows uses a separate `csbindgen`-based binding crate (not yet present).

**Scope (as of Milestone H — Templates).** Vault open + scan with cooperative cancellation and progress callbacks; paged file listing with markdown-only filtering; per-file metadata hydration including headings, parsed frontmatter properties, outgoing links, paged backlinks, and parsed tasks; FTS5-backed full-text search across the vault; atomic `save_text` with content-hash conflict detection and op-log append; vault-wide task review with filtering and sort-tuple-cursor paging; markdown task toggle (`toggle_task_status`); template enumeration and `{{date}}` / `{{title}}` / `{{prompt:Label}}` substitution. Architectural rationale lives in [`docs/plans/05_locked_architecture_decisions.md`](../../docs/plans/05_locked_architecture_decisions.md).

## Building

```sh
cargo build -p slate-uniffi
```

Produces `target/<profile>/libslate_uniffi.dylib` (Mac), `.so` (Linux), or `.dll` (Windows), plus the `uniffi-bindgen` binary used to generate foreign-language bindings.

## Generating Swift bindings

After building, run:

```sh
cargo run -p slate-uniffi --bin uniffi-bindgen -- \
    generate \
    --library target/debug/libslate_uniffi.dylib \
    --language swift \
    --out-dir target/generated/swift
```

This emits three files into `target/generated/swift/`:

- `slate_uniffi.swift` — Swift API surface.
- `slate_uniffiFFI.h` — C header for the low-level FFI symbols.
- `slate_uniffiFFI.modulemap` — Swift module map exposing the header.

The Swift smoke-test client in [`examples/swift-cli/`](../../examples/swift-cli/) shows how to compile a Swift program against these bindings.

## Generating Kotlin bindings

```sh
cargo run -p slate-uniffi --bin uniffi-bindgen -- \
    generate \
    --library target/debug/libslate_uniffi.dylib \
    --language kotlin \
    --out-dir target/generated/kotlin
```

Kotlin client examples are not yet wired up (Android is the fourth shipping platform — see `docs/plans/05` §3).

## FFI surface

Everything below is defined in [`src/lib.rs`](src/lib.rs) and exposed to Swift / Kotlin. The Rust types here are FFI mirrors of types in [`slate-core`](../slate-core) — the duplication is intentional so the core stays free of uniffi annotations.

### Session

| Type | Kind | Description |
|------|------|-------------|
| `VaultSession` | struct | Top-level handle. Opened against a filesystem path; owns the SQLite cache and provides the scan, query, and save APIs. |
| `SessionConfig` (constructed inside) | — | Tunables: parser version, max cache pages, large-file refuse threshold. |

### Errors

| Type | Kind | Description |
|------|------|-------------|
| `VaultError` | enum | 11 variants surfaced across the API: `Io`, `Db`, `InvalidPath`, `Trash`, `Cancelled`, `InvalidUtf8`, `FileTooLarge`, `InvalidQuery`, `Unsupported`, `InvalidArgument`, `WriteConflict`. `WriteConflict` carries `current_content_hash` + `expected_content_hash` + `current_mtime_ms` so the host can drive a "Keep mine / Reload from disk" resolution UI. `InvalidArgument` is returned for caller-side validation failures (out-of-range ordinal, status-char outside the allowlist, etc.). |

### Cancellation

| Type | Kind | Description |
|------|------|-------------|
| `CancelToken` | struct | Cooperative cancellation flag. Pass to long-running calls (scans, full-text search); flip via `.cancel()` from any thread. Clones share state. |

### Listing & paging

| Type | Kind | Description |
|------|------|-------------|
| `FileFilter` | enum | Scope a `list_files` call: `All` or `MarkdownOnly`. |
| `Paging` | struct | Cursor + page-size pair for paged endpoints. Built via `Paging::first(size)` or carried forward from the previous page. |
| `FileSummary` | struct | Per-file row (path, name, mtime, size, is_markdown, content_hash). |
| `FileSummaryPage` | struct | Page of `FileSummary` + next-cursor + total count. |
| `FileMetadata` | struct | Single-file hydration: summary fields plus headings + parsed frontmatter properties. |
| `Property` | struct | One frontmatter property (key, kind, value JSON). |
| `Heading` | struct | One indexed heading (level, text, ordinal, anchor_id). |

### Links

| Type | Kind | Description |
|------|------|-------------|
| `LinkAnchor` | struct | Wikilink anchor — `("heading", text)` or `("block", text)`. |
| `OutgoingLink` | struct | One link from the current file out. Resolved or unresolved; internal or external. |
| `Backlink` | struct | One inbound link — the source file path + snippet around the link site. |
| `BacklinkPage` | struct | Paged backlinks (cursor + total). |
| `UnresolvedLink` | struct | Internal link whose target file does not exist in the index. |
| `UnresolvedLinkPage` | struct | Paged unresolved links. |
| `NoteLoadBundle` | struct | One-shot composition: file metadata + outgoing links + paged backlinks, for the "open note" UI flow. |

### Query (full-text search)

| Type | Kind | Description |
|------|------|-------------|
| `SearchScope` | enum | `Vault` (everything), `Folder{path}` (prefix-bounded), `File{path}` (reserved — returns `Cancelled` today), `Tag{name}` (reserved — returns `Cancelled` today). |
| `QueryHit` | struct | One FTS5 match — path, name, snippet wrapping matched tokens in `STX` / `ETX` (U+0002 / U+0003) for the host to replace with attributed-string emphasis. |
| `QueryResultSet` | struct | Hits + paging cursor + (eventually) facets. |

### Scan progress

| Type | Kind | Description |
|------|------|-------------|
| `ScanProgress` | enum | Progress events: `Started{total_files}`, `FileIndexed{path, indexed, total}`, `Finished{report}`, `Cancelled`, `Failed{message}`. |
| `ScanProgressListener` | callback trait | Implemented on the consumer side; receives `on_progress(event)` callbacks. Used by the Mac app for the scan strip. |
| `ScanReport` | struct | End-of-scan summary: files indexed, skipped, errors collected. |

### Save & op log

| Type | Kind | Description |
|------|------|-------------|
| `SaveReport` | struct | Result of a `save_text` call: new content_hash, mtime, op-log entry id. |
| `OpKind` | enum | Op-log entry types. Currently `WholeFileReplace`. |
| `OpLogEntry` | struct | One op-log row: timestamp, kind, actor, before/after hashes, payload. |

### Tasks (Milestone G)

Markdown task lines (`- [ ]` / `- [x]` / `- [/]` / etc.) parsed out of note bodies and indexed in the `tasks` SQLite table. Surfaces the Tasks panel (per-file) and the vault-wide Tasks Review view.

| Type | Kind | Description |
|------|------|-------------|
| `TaskItem` | struct | One parsed task: ordinal (0-based document order, stable across saves), text, status_char (`String` — uniffi has no `char`), completed (derived from status_char `x`/`X`), due_ms / scheduled_ms / priority / recurrence (Tasks-plugin emoji metadata), line (1-based), byte_offset. |
| `TaskWithLocation` | struct | `TaskItem` + owning file's `path` + `file_name`, so the vault-wide review view doesn't pay a second roundtrip per row. |
| `TaskFilter` | struct | `tasks_in_vault` query shape: `completed: Option<bool>`, `due_from_ms` / `due_to_ms` (inclusive lower / exclusive upper), `priority_at_least`. |
| `TaskWithLocationPage` | struct | Paged `TaskWithLocation` — items + next_cursor + total_filtered. |

`VaultSession` adds `tasks_for_file(path)`, `tasks_in_vault(filter, paging)`, and `toggle_task_status(path, ordinal, new_status_char, expected_content_hash)`. Toggle routes through `save_text` under the connection mutex so it shares the editor's conflict-detection, atomic-write, index-refresh, and op-log discipline. `new_status_char` is allowlisted to printable ASCII minus `[` / `]` / `\t` / `\n` / `\r` (would corrupt the file).

### Templates (Milestone H)

`{{date}}` / `{{title}}` / `{{prompt:Label}}` substitution over a Markdown template file. Drives the Mac UI's create-from-template flow.

| Type | Kind | Description |
|------|------|-------------|
| `TemplateSummary` | struct | Row in the template picker: vault-relative path, name (file stem), optional description (frontmatter `description:` or first non-blank non-frontmatter line, truncated to 120 chars). |
| `TemplatePrompt` | struct | One `{{prompt:Label}}` marker — `key` (dedup-friendly slug) + `label` (raw human-facing text). |
| `TemplateMetadata` | struct | What `extract_template_metadata(source)` returns — just `prompts: Vec<TemplatePrompt>` today, additive shape for future fields. |
| `TemplateContext` | struct | Render-time inputs: `now_ms` (UTC reference for `{{date}}` / `{{time}}`), `title`, `vault_name`, `prompt_values: Map<key, response>`. Missing prompt keys leave the marker literal. |
| `RenderedTemplate` | struct | Result of `render_template`: `body` (substituted Markdown, with `{{cursor}}` stripped) + optional `cursor_byte_offset` for the host editor to park the caret. |

`VaultSession` adds `list_templates()` and `render_template(template_path, context)`. The free function `extract_template_metadata(source)` runs without a session so the UI can pre-flight a template's prompt set before the user confirms.

## Consumer notes

- All entry points run on the calling thread; concurrency is the caller's problem. The Mac app drives long-running calls (scans, search) off the main actor.
- Strings cross the FFI as UTF-8 owned by the Rust side; Swift / Kotlin see them as native strings.
- `VaultError` variants map to `throws` in Swift and `Throws` in Kotlin.
- Generated code is rewritten on every build — never edit `slate_uniffi.swift` / `slate_uniffiFFI.h` by hand.
