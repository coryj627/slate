# PR 22 — Descriptor-relative session interior (the A→B→A open race) (design first)

Issue: #940 (FL-06 round 28). Size L; design document first. Lanes:
rust, swift, windows. Depends on PR 20 (the watcher must follow the
pinned root); keep sequential with PR 19 and PR 20 (all touch root
identity).

## Goal

Every interior resolution of a filesystem session (config reads, the
structural lock, the SQLite cache, the provider root, the watcher) binds
to one root descriptor held for the session's lifetime, so a root
swapped between the identity anchor and the post-open re-verification
can no longer yield a session whose SQLite connection belongs to another
vault.

## Current state on main

`crates/slate-core/src/session.rs:3012` `from_filesystem_with_actor`:
pins the root as an open descriptor and anchors its identity by fstat
(`pin_root_identity`, `:1068`/`:1084`), but the interior reads and the
cache open still resolve by pathname (`:3022-3029`), then re-verifies the
pathname against the anchor and fails the open on divergence. The
residual schedule (swap A→B after the anchor, let the interior open B,
restore A before re-verification) survives; only descriptor-relative
interior resolution closes it. The macOS host refuses to run the sidebar
store against a root that does not match the session's anchor.

## Design document (`22_descriptor_relative_session_design.md`)

Must settle:
1. The mechanism per platform: on macOS and Linux, interior paths built
   through `/dev/fd/<rootfd>/…` or `openat`-family calls against the held
   `OwnedFd` (SQLite open through a path under `/dev/fd`, the lock file,
   `slate.json` and `.slate/prefs.json`); on Windows, the dirfd-relative
   IO Milestone W planned (one `fs_root` convention shared by both).
2. What must stay pathname-based (error-message display, user-facing
   paths) and how canonicalization is kept consistent with the
   descriptor root.
3. The watcher's root (PR 20) and FSEvents' path-based API: how the
   watch follows the descriptor (re-resolve the descriptor's current path
   per event batch and refuse events for a swapped root).
4. The deterministic A→B→A regression: a seam that swaps the path
   mid-open and proves the session still binds the anchored root.
5. The host half: the mac sidebar store's admission check and any host
   path display that reads the session root.

## Acceptance (for the design document)

- [ ] Mechanism chosen per platform with the SQLite and lock-file
      answers verified by a spike.
- [ ] The regression's seam designed; implementation PRs sized.

## Brief

Write the descriptor-relative interior design. Read `session.rs:
1060-1100` and `:3012-3080`, the FL-06 identity notes under
`docs/plans/15_files_sidebar/`, locked 05 §4.1, and PR 20's design.
Produce `docs/plans/43_mac_backlog_delivery/22_descriptor_relative_
session_design.md` settling the five points, with a spike result for
SQLite-over-`/dev/fd` and the lock file, and a sized implementation
plan. Do not implement in the same PR.
