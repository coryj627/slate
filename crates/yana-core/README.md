# yana-core

The core engine for [YANA](https://github.com/coryj627/YANA), an accessibility-first knowledge workspace.

## Status

Bootstrap stage. This crate currently exposes only a minimal Markdown heading extractor as a toolchain-validation step. The full API surface — vault provider abstraction, metadata index, operation log, query engine, content-specific pipelines, FFI exposure — is documented in [`docs/plans/05_locked_architecture_decisions.md`](../../docs/plans/05_locked_architecture_decisions.md) and will land incrementally.

## Planned scope (see Section 4 of the planning doc)

- Vault provider abstraction with desktop (`FsVaultProvider`) and mobile (host-supplied) implementations.
- Metadata index (headings, links, embeds, tags, frontmatter properties, tasks, blocks) backed by SQLite.
- Operation log infrastructure for accessible conflict resolution and change tracking.
- Markdown parsing via `pulldown-cmark` with Obsidian extensions (wikilinks, embeds, callouts).
- Content-type pipelines: Math (LaTeX → MathML → speech/braille via MathCAT), Mermaid (SVG + structured description), code (tokens + AT-facing semantic spans), citations (Pandoc syntax + hayagriva).
- Query engine: `.base` YAML / Dataview DQL / native AST → SQLite execution → accessible results.

## Quick check

```sh
cargo check
cargo test
```
