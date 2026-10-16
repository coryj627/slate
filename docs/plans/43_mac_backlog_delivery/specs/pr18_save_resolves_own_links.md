# PR 18 — A Markdown save resolves only its own links (core)

Issue: #1295 (#1280 follow-up B; AR-32). Size S to M. Lanes: rust (plus
swift and windows, which run on `crates/**`). Depends on nothing. Any
time.

## Goal

Every Markdown save (`save_text`, the create path, and `open_base`'s
repair) resolves the saved note's distinct link targets through the
folded-name lookup instead of reading every `files` row into memory, so
a save's cost tracks the note, not the vault (locked 05 §4 and §9.3.1).

## Current state on main

- `crates/slate-core/src/session.rs:4697` `index_saved_file`: `SELECT
  path FROM files` into an `InMemoryVaultIndex` to resolve links; shared
  by `ensure_open_base_indexed` (`:18350`). Since #1280 it runs on the
  save worker, so the UI no longer freezes, but it is unbounded.
- `crates/slate-core/src/links_db.rs:100` `replace_links_for_file`; the
  folded-name lookup from #1279: `idx_files_name_fold` /
  `idx_files_path_fold` and `LinkCandidateSelector`
  (`link_resolver.rs:311`), the rule `resolve_link` uses.

## Windows reference

Not host work. Contract 40 AR-32 (`:69`) records the gap and the fix.

## Design

- `replace_links_for_file` takes a resolver; the save and the `open_base`
  repair pass a lookup-backed resolver that resolves each distinct target
  through the folded-name probe, memoized per save; scan and rename keep
  their snapshot resolver.
- No FFI, bindings or host change.

## Tests (Rust)

- A vault of N unrelated notes plus a note with k links: the save's
  resolution visits only the candidate rows for its k targets (a
  test-only row counter), for `save_text` and the create path.
- The lookup resolves exactly as `resolve_link` over the whole index
  across the resolver fixtures: bare name, folder path, vault-root path,
  relative `../`, a target without an extension, attachments, the same
  name in many folders.
- The links a save writes equal the links a scan writes for the same
  note (the equivalence census).

## Docs and registers

`docs/plans/40_nvda_matrix_remediation_contracts.md` AR-32 (`:69`)
closed.

## Acceptance

- [ ] `index_saved_file` no longer reads the whole `files` table.
- [ ] The three facts pass; `make ci` green; the native lanes green.

## Brief

Bound the save-path link resolution in core. Read `session.rs:4697` and
`:18350`, `links_db.rs:100`, `link_resolver.rs:311` and contract 40
AR-32. Give `replace_links_for_file` a resolver parameter; make the save
and the `open_base` repair resolve only the note's distinct targets
through the folded-name lookup, memoized per save; keep scan and rename
on their snapshot. Write the row-counter fact, the equivalence fixtures
and the save-equals-scan census first. Close AR-32. Done when `make ci`
and the native lanes are green.
