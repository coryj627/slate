# 09 — Milestone M plan: Sync detection + diagnostics + CLI v1

**Status:** 📝 Planned (2026-07-03). Not started. GitHub [milestone 13](https://github.com/coryj627/slate/milestone/13).
**Implements:** `05_locked_architecture_decisions.md` §7.2 phase 1 (sync **detection only** — no sync writer), §7.4 sync-detection types, and the §10 Tier-2 decision ("CLI tool `slate` ships in V1"; the local HTTP API ships separately in V1.x and is **not** in this milestone).
**Executable spec:** [m_spec.md](m_spec.md) — the per-issue implementation contract.

**Goal.** A tester learns whether their vault is being managed by an external sync system (and what risk that carries) the moment they open it, and can drive a useful read-only subset of Slate from a shell.

---

## Scope decisions (locked for this milestone)

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | Diagnostics surface is a **right-pane leaf**, not a sidebar section | The GH milestone text ("collapsible sidebar section, below Properties") predates the U program. U3-3 removed Properties from the sidebar; U4-2 retired the sidebar panel stack entirely. Post-U, panels live in the `Leaf` registry (`RightPaneView.swift:19`). The diagnostics panel becomes `Leaf.syncDiagnostics`, vault-scoped like `.bibliography`. |
| 2 | Detector output uses the `05` §7.4 shape, **extended** | `DetectedSyncProvider { kind, evidence_paths, risk_level, recommendation }`. `evidence_paths: Vec<String>` replaces §7.4's single `indicator: String` (several detectors produce multiple markers); `SyncProviderKind` gains `Syncthing` (in the GH milestone's detector list, absent from the §7.4 sketch). §7.4's `Unknown` variant is dropped — every detector is explicit, and an empty report means "nothing detected", so `Unknown` has no producer. These are extensions of the locked sketch, not reversals. |
| 3 | CLI v1 is **read-only on vault content** | `open`, `read`, `list`, `search`, `tasks`, `render-template`, `properties`, `links`, `sync-check` read the vault and write only the `.slate/` cache. Of the locked §10 verb list ("open, read, write, list, search, query, render"), only `write` (concurrent-writer story unproven) and `query` (needs N) are deferred — both with filed follow-ups. `render-template` prints to stdout; it does not create notes. |
| 4 | `slate sync-check` is added to the GH milestone's command list | Detection callable from a shell is nearly free once M-1 lands and makes the CLI the scriptable diagnostics surface ("run sync-check in your dotfiles before pointing a new sync tool at the vault"). |
| 5 | Detection is filesystem-probe based, not index based | `.icloud` placeholders, `.stfolder`, `.dropbox` etc. are dot-prefixed and the scanner deliberately skips hidden files — the SQLite index can never see the markers. Detectors probe the filesystem directly (bounded, exact paths — no unbounded walks). Consequence: detection is only supported for filesystem-rooted sessions (`from_filesystem`); provider-abstracted sessions return an empty report with `supported = false`. |
| 6 | One shared binary name: `slate` | Crate `crates/slate-cli`, `[[bin]] name = "slate"`. Matches `05` §10 ("CLI tool (`slate`)"). |

## Issue map

| ID | Issue | Track | Depends on | Labels |
|----|-------|-------|-----------|--------|
| M-1 | Sync detector engine (`sync_detect.rs`) + session/uniffi API | Rust | — | `backend` |
| M-2 | LiveSync config reader (read-only, credential-free) | Rust | M-1 | `backend` |
| M-3 | Sync diagnostics leaf (Mac UI) | Swift | M-1, M-2 | `swift-ui`, `a11y` |
| M-4 | `slate-cli` crate: scaffold, output formats, exit codes, Ctrl-C, `open` + `sync-check` | Rust | M-1 | `backend` |
| M-5 | CLI query commands: `read`, `list`, `search`, `links`, `properties` (+ two new core property queries) | Rust | M-4 | `backend` |
| M-6 | CLI `tasks` + `render-template` | Rust | M-4 | `backend` |

```
M-1 ──┬──▶ M-2 ──▶ M-3            (diagnostics track)
      └──▶ M-4 ──▶ M-5 ∥ M-6      (CLI track)
```

Two tracks are independent after M-1; they can run in parallel worktrees. One PR per issue. Execution order within tracks: M-1 → M-2 → M-3 and M-4 → (M-5 ∥ M-6).

**Issue-body contract** (the U-program convention, `08_ui_parity/00_program.md` §DoD): each filed issue body carries (a) a link to its spec section in [m_spec.md](m_spec.md), (b) the issue's condensed DoD checklist — the spec's test list for that issue verbatim, as checkboxes, (c) the labels from the map above, and (d) its dependency edge ("blocked by M-1" etc.).

## Relationship to other milestones

- **U program:** every U piece M consumes is shipped — the U4-2 leaf registry (`RightPaneView.swift`), U0 design tokens/`SlateSymbol`, and the PresentationReady test harness. (The U program as a whole is still in flight — U5 and parts of U3/U4-4 remain open; M assumes nothing from those.) M-3 follows the U-program Presentation-Ready DoD (`08_ui_parity/00_program.md` §A–G) as its standing bar.
- **N (Bases):** no dependency either way. The CLI's `search` returns the FTS `QueryResultSet`; when N ships structured queries, a `slate query` command is a natural V1.x follow-up (explicitly out of scope here).
- **O (Local history):** no dependency either way. M-4's CLI scaffold is where an eventual `slate history` command would land (out of scope).
- **Local HTTP API:** V1.x, separate milestone, per `05` §10. Nothing in M may assume its existence.

## Definition of done (milestone)

- Every detector fires on a fixture vault seeded with that system's markers, and the negative fixtures produce zero detections (no false positives).
- The LiveSync config reader never reads or surfaces credentials — enforced by test (output asserted to not contain planted credential strings), not by convention.
- Every CLI command round-trips against the fixture vault in all three formats — except `render-template` and `read`, whose output is a document body and which reject `--format tsv` with a usage error by contract; `--format json` output parses and matches the documented envelope; exit codes match the contract; Ctrl-C during a slow scan **and** during a search exits 130 with no cache corruption (vault reopens clean).
- Diagnostics leaf renders every detection variant; High-risk / multi-sync states announce assertively; `a11y-check` 100 at the PR tip; APCA Lc ≥ 75 measured on the risk-badge text in both appearances.
- `cargo fmt --check` + clippy clean; Swift tests green. Benchmarks: no per-issue runs required (no editor-path code is touched); the **milestone-close benchmark run** (standing norm, `06` cross-cutting concerns) is the owned check that confirms no regression.

## Out of scope (deferred)

- Sync **writers** of any kind (V2 — `05` §7.2 phase 2/3).
- Local HTTP API (V1.x, after CLI — `05` §10).
- CLI write/edit commands, `slate query` (structured), `slate history` (needs O).
- Watching for sync-marker changes after open (detection runs once per vault open + on manual refresh; live re-detection is a follow-up if testers ask).
- Windows/iOS surfaces (Mac-only UI, per platform order in `05` §3).

## Tester feedback questions (carried from the GH milestone)

1. Are the detection recommendations actually actionable, or just noise?
2. CLI: which command do you actually reach for? Anything missing?
3. LiveSync config viewer: useful, or do you just check the plugin's own UI?
