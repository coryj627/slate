# PR 20 — A real vault watcher in core (design first)

Issue: #1267 (OD-1's follow-up; AR-3, AR-7). Size L; the first
deliverable is a design document in this directory, reviewed before any
implementation PR. Lanes: rust, swift, windows. Depends on PR 19 (the
mac baseline it falls back to). PRs 21 and 22 follow it.

## Goal

Changes made while Slate is in the foreground no longer wait for a
Refresh or a window activation: a `notify`-backed watcher behind
`VaultProvider::watch` triggers the same reconciliation the rescan uses,
on both hosts, with Refresh and the foreground rescan as the fallback
when the platform watcher is unavailable or overflows.

## Current state on main

- `crates/slate-core/src/vault/provider.rs:224` `fn watch(&self, sink:
  Arc<dyn FileEventSink>) -> Result<Option<WatchHandle>, VaultError>`;
  `vault/fs.rs:975` returns `Ok(None)` (pinned by
  `watch_returns_none_in_milestone_a`, `:1861`); `provider.rs` promises
  the refresh-on-foreground fallback.
- Locked 05 `:338-342`: file-change events stay Slate-owned writes only;
  the watcher triggers reconciliation, it does not broaden the event
  channel.
- The rescan path (PR 19 on mac; W7-7 PR 7 on Windows) is the consumer.

## Windows reference

`VaultLifecycleViewModel.Rescan.cs` is the trigger target (a watcher
request joins the same lattice as Explicit and Foreground, with its own
reason); `ForegroundRescanRoute` stays as the fallback.

## Design document (the first deliverable: `20_vault_watcher_design.md`
in this directory)

Must settle, with evidence:
1. The `notify` backend per platform (FSEvents on macOS,
   ReadDirectoryChangesW on Windows), recursive watch rooted at the
   vault, `.slate/` and hidden paths filtered, debounce window, and the
   overflow signal.
2. The reconciliation granularity: a bounded rescan of the touched
   paths versus a full incremental rescan; how a burst from a sync client
   coalesces into one run and one announcement.
3. Threading: the watcher thread hands events to a core-owned
   coalescer; the host is called back at most once per burst through the
   existing listener trait shape (locked 05 §4 principle 5: host-registered
   callbacks, no independent Rust threads calling into the host outside
   that path).
4. Interaction with PR 22 (descriptor-relative root): the watcher must
   follow the pinned root, not a pathname that can be swapped.
5. Announcement policy: a watcher-triggered reconciliation speaks like a
   foreground rescan (only when something changed or incomplete), never
   floods a screen reader during a burst.
6. The fallback contract: when `watch` returns `None` or overflows, the
   hosts' Refresh and foreground rescan remain the baseline and the user
   is told nothing extra.
7. Tests: a core fact that an external create/modify/delete reaches the
   sink; a coalescing fact; an overflow-falls-back fact; host facts that
   a watcher-driven change reaches the tree without a Refresh (both
   hosts).

## Acceptance (for the design document)

- [ ] Each of the seven points answered with the chosen option and the
      rejected alternatives.
- [ ] A sizing of the implementation PRs (core, then each host) with
      their facts named.

## Brief

Write the watcher design. Read `vault/provider.rs:200-240`, `vault/fs.
rs:960-990`, locked 05 §4 and `:338-342`, contract 40 OD-1/AR-3/AR-7,
PR 19's spec, and the Windows rescan lattice. Produce
`docs/plans/43_mac_backlog_delivery/20_vault_watcher_design.md`
answering the seven points above with evidence and a sized
implementation plan. Do not implement in the same PR. Done when the
document is reviewed and its sizing is accepted.
