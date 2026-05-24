# slate-core

The core engine for [Slate](https://github.com/coryj627/slate), an accessibility-first knowledge workspace.

## Status

V1 alpha. Shipped through Milestone H (Templates); covers the four read-and-edit workflows the AT-user tester cohort exercises against existing Obsidian vaults. The full architectural surface — vault provider abstraction, metadata index, operation log, query engine, content-specific pipelines, FFI exposure — is documented in [`docs/plans/05_locked_architecture_decisions.md`](../../docs/plans/05_locked_architecture_decisions.md) and continues to land incrementally.

## What's shipped

- **Vault provider abstraction** — `FsVaultProvider` (desktop) implements the trait; mobile / host-supplied providers slot in without core changes.
- **Metadata index** in SQLite (10 migrations as of Milestone G):
  - `files`: per-file rows with content hash, mtime/ctime/size for fast-path rescan detection.
  - `headings`: per-file Markdown headings + slugified anchors.
  - `links` + `properties` + `properties_list_values`: outgoing/inbound link graph and frontmatter properties (atomic + list-element search).
  - `tasks` (+ `idx_tasks_completed`, `idx_tasks_due`, `idx_tasks_priority`, `idx_tasks_sort`): per-file Markdown task lines with Tasks-plugin emoji metadata, indexed for the panel and vault-wide review query.
  - `files_fts` (FTS5): external-content full-text index over `files.body_text`.
- **Markdown parsing** via `pulldown-cmark` with Obsidian extensions: wikilinks (`[[target]]` / `[[target|display]]` / `[[target#anchor]]`), embeds (`![[target]]`), CommonMark/GFM links, fenced code blocks, HTML blocks, YAML frontmatter.
- **Scanner** with mtime+size+ctime fast-path skip, cooperative cancellation via `CancelToken`, and incremental progress callbacks (`ScanProgressListener`).
- **Editing** via `save_text` — atomic temp+rename write, content-hash conflict detection (`VaultError::WriteConflict`), index refresh inside the same SQLite transaction, op-log append (`OpLogEntry` / `OpKind::WholeFileReplace`).
- **Tasks** — `extract_tasks` parser (bullets `-` / `*` / `+`, status chars including `[ ]` / `[x]` / `[/]` / `[-]`, 📅 due / ⏳ scheduled / 🔼 ⏫ 🔽 ⏬ priority / 🔁 recurrence emoji metadata), `tasks_for_file`, `tasks_in_vault(filter, paging)`, atomic `toggle_task_status`.
- **Search** — FTS5-backed `full_text_search(query, scope, cancel)` with vault / folder scopes, snippet markers for the host to wrap in attributed-string emphasis.
- **Templates** — `list_templates`, `extract_template_metadata` (prompts + cursor presence), `render_template` with `{{date}}` / `{{time}}` / `{{title}}` / `{{vault}}` / `{{cursor}}` / `{{prompt:Label}}` substitution.
- **Op log v0** — coarse per-save `WholeFileReplace` entries, length-prefixed format with version header. Fine-grained per-edit operations come in V1.x.

## Not yet shipped (per the planning doc)

- Content-type pipelines: Math (LaTeX → MathML → speech/braille via MathCAT), Mermaid (SVG + structured description), code (tokens + AT-facing semantic spans), citations (Pandoc syntax + hayagriva).
- Query engine: `.base` YAML / Dataview DQL / native AST → SQLite execution → accessible results.
- Tree-sitter-backed semantic spans for incremental reparse.

## FFI

This crate is pure Rust and doesn't depend on `uniffi`. The FFI mirror lives in [`crates/slate-uniffi`](../slate-uniffi/README.md) — see that README's "FFI surface" section for the full Swift / Kotlin-facing type list.

## Quick check

```sh
cargo check
cargo test
```
