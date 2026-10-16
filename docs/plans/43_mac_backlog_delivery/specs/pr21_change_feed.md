# PR 21 — A core change feed for exact cross-session rescan counts (design first)

Issue: #1293 (AR-30, AR-31). Size L; design document first. Lanes: rust,
swift, windows. Depends on PR 20 (the watcher feeds the same path).

## Goal

A Refresh counts and applies the changes another Slate session (another
window, `slate-cli`) already indexed, exactly, and the tree and Quick
Open can no longer sit one index version apart mid-re-sync.

## Current state on main

PR 7's rescan counts only its own scan's changes (AR-30, contract 40
`:59`); a commit by another session during the re-sync can leave the
tree and Quick Open one version apart (AR-31, `:60`). `.slate/cache.
sqlite` records what is current, not who changed it; connection-local
triggers cannot see other connections' commits and `PRAGMA data_version`
also moves for Slate's own compaction connection.

## Design document (`21_change_feed_design.md`, the first deliverable)

Option D from PR 7's design review, to be settled with evidence:
1. A core-owned persistent `change_feed` table: `seq`, `path`,
   `previous_path`, `kind` (created / modified / deleted / renamed),
   `writer_id` (session nonce plus process), hash before and after,
   written in the same transaction as every index mutation (scan upserts
   and prunes, `save_text`, structural operations).
2. Registered cursors with a heartbeat; trimming below the oldest live
   cursor or past a maximum age; a trimmed gap falls back honestly to the
   full re-sync with the "not counted" wording.
3. Host consumption in bounded, cancellable pages across the FFI (locked
   05 §4), giving per-path effects and exact counts including other
   writers' changes; reading and history notified per path only.
4. Crash consistency: the feed row and the index mutation commit together.
5. The watcher (PR 20) feeds the same path; the rescan is the fallback.
6. Facts: two sessions (window plus CLI) where A's Refresh counts and
   applies B's create, modify and delete exactly; a trimmed feed falls
   back; per-path notification only; bounded pages; crash consistency; a
   watcher-driven change reaches the host without a Refresh.

## Acceptance (for the design document)

- [ ] Schema, cursor and trimming rules decided; the FFI page shape named.
- [ ] Both hosts' consumption sized, with facts named.

## Brief

Write the change-feed design. Read contract 40 AR-30/AR-31 and PR 7's
design review notes (the W7-7 spec §8), PR 19 and PR 20's specs, and the
session's index mutation sites (`session.rs` `index_file`, `save_text`,
the structural operations, the prunes). Produce
`docs/plans/43_mac_backlog_delivery/21_change_feed_design.md` settling
the six points with evidence and a sized implementation plan. Do not
implement in the same PR.
