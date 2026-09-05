# Upgrade fence (#1078): contracts

Scope: the same-protocol-version precondition of the write-intent protocol
(`docs/plans/21`, scope precondition 2) — no Slate-coordinated actor may
write the cache under a schema version other than the one it was built
for. Written BEFORE implementation per `24_red_team_protocol.md` §0. The
divergence (UD) and accepted-risk (UR) registers are owner-recorded and
**off-limits for review re-litigation**.

Contract numbering is per-document.

## The findings that shape this issue

**1. The only fence is at open, and it fences the wrong direction for a
live session.** `VaultSession::open` → `db::migrate` refuses a cache
NEWER than the build (`UnsupportedVersion`) and UPGRADES an older one.
Nothing checks the version afterwards. An older session stays live while
a newer app or CLI migrates the shared cache, then keeps writing with
pre-protocol code — no intent marker, no epoch stamp — and a post-write
failure leaves divergence nobody can see. INV1 is broken by construction.

**2. The fence can only work if it lives in the OLDER binary.** A check
the new build adds does nothing for a session already running old code,
and a post-write SQL trigger installed by the migration fires AFTER the
old binary wrote the file (the file write sits inside the save's writer
transaction, before the index rows a trigger would guard). So the fence
must ship BEFORE the first release. There are no releases yet: landing
it now makes it universal for every later schema bump; landing it later
makes it useless for the builds already out. "Moot until W4-3 ships" has
flipped — W4-3 shipped, and this is a first-release prerequisite.

**3. Saves already hold the right lock at the right moment.**
`save_text_locked` takes its SQLite `IMMEDIATE` transaction BEFORE the
disk write, and `db::migrate` runs inside its own `IMMEDIATE`
transaction. The two serialize on SQLite's one-writer lock (file-based,
so cross-process): a version check at the top of the writer transaction
is atomic with the write — the old writer either commits entirely under
the old schema, which the migration then sees, or observes the new
version and refuses before touching the file.

**4. Structural operations are already excluded from migrations.**
Renames, moves, deletes and batches mutate the filesystem OUTSIDE any
SQLite transaction, under the cross-process `VaultStructuralLock`
(`structural.lock` in the cache dir) — and `VaultSession::open` acquires
that same lock before `db::migrate`. A migration therefore never
overlaps an in-flight structural operation in another process. The
issue's "terminate/fence already-open sessions before any new actor uses
the cache" is already half-satisfied for this class; the other half is
finding 3's check at the start of each operation.

**5. The cache has ~40 writer-transaction sites in ~30 funnels.** Saves,
structural singles/batches/recovery, scan and reindex, op-log
compaction and event regeneration, dashboards and saved queries, canvas
tables, bibliography replacement (`citations_db`). Every one is a place
the old binary can write new-schema rows. One helper at every site is
mechanical; a per-site check that can be forgotten is not a fence.

**6. `schema_version` is a table, not `PRAGMA user_version`.** The build's
version is `MIGRATIONS.len()`; the cache's is `MAX(version) FROM
schema_version`. The check is one indexed read.

**7. A fence that READS before it writes must take the lock first (found
by the full suite).** The first cut began `DEFERRED` writers with the
version read; the compaction worker's event regeneration — racing the
test's saves — then failed its first write with `SQLITE_BUSY_SNAPSHOT`
and left its marker standing. The busy handler does not retry that
code: it is not "busy", it is "your snapshot is stale". `IMMEDIATE`
everywhere is the fix, and it is stronger, not merely safer.

## Contracts

**U1 — Every cache-writer transaction is fenced.** A transaction that may
write the cache is begun through ONE helper, `db::begin_fenced(conn)`,
which begins an IMMEDIATE transaction and, inside it, refuses unless
the cache's schema version equals this build's. No other transaction
constructor appears in a production cache writer (a source pin holds
this). `db::migrate` is the deliberate exception — it IS the
version-change path.

**U2 — The refusal is before any mutation, and every fenced transaction
is `IMMEDIATE`.** The writer lock is taken first, then the version is
read under it, then the writes follow — atomic by construction (finding
3). No fenced transaction is `DEFERRED` (finding 7): a version read at
the top of a deferred transaction pins a snapshot before the first
write, and ANY concurrent commit in between fails that write with
`SQLITE_BUSY_SNAPSHOT`, which the busy handler does not retry. Every one
of the ~40 sites turned out to be a writer (each commits), so taking the
lock first costs nothing a writer was not already going to pay, and it
removes the read-then-write upgrade deadlock between two connections as
a side effect.

**U3 — The error is `VaultError::Db`, typed inside core.** Owner call:
reuse the existing `Db` channel rather than adding an FFI variant. Core
adds `DbError::SchemaVersionSkew { db_version, build_version }` whose
message is host-ready ("…another Slate process upgraded or replaced the
vault cache — restart Slate to continue"); it flows through
`VaultError::Db { message }` unchanged on the FFI, so hosts relay it
exactly as they relay every save refusal today ("Save blocked: …"). No
bindings change; no host change is required for correctness.

**U4 — Either direction is skew.** A cache NEWER than the build is the
upgrade case; a cache OLDER than the build (a backup restored under a
live session) is also a schema this session did not open against. Both
refuse. The open-time `UnsupportedVersion` stays as it is.

**U5 — Reads are not fenced.** Queries keep running against whatever
schema is there: additive migrations read fine; a breaking one surfaces
as an ordinary `Db` error from old SQL. INV1 is about unmarked
DIVERGENCE, which only writers cause (divergence UD-1).

**U6 — No session state.** The fence compares the cache's version to a
process constant (`MIGRATIONS.len()`); there is no latch. A fenced write
is refused every time it is tried, identically, and a cache that returns
to the expected version (the skew was a transient replacement) writes
again. Stateless means nothing to reset and nothing to get wrong.

**U7 — Migration excludes structural work, by pin.** `VaultSession::open`
holds the structural lock across `db::migrate` today (finding 4); a fact
pins it so a refactor cannot drop it silently.

## Divergence register (owner-recorded; off-limits for re-litigation)

**UD-1 — Reads are unfenced (U5).** Fencing reads needs the check on
every query path and buys nothing for INV1. Hosts see the refusal at the
first write, which is where the user learns to restart.

**UD-2 — No live-session registry.** A heartbeat table so a new actor
can refuse to migrate while older sessions are live was considered and
rejected: it cannot terminate other processes, stale heartbeats after
crashes are their own failure mode, and with U1 correctness no longer
depends on the new actor knowing who else is live.

**UD-3 — Per-connection TEMP tables are not cache writes.**
`bases/engine.rs` materializes query matches into `temp.*` inside a
transaction; TEMP is per-connection and vanishes with it, so it is
outside U1's "cache writer" and outside the pin.

## Accepted-risk register (owner-recorded; off-limits for re-litigation)

**UR-1 — The fence ships in the first release or not at all.** Its whole
value is that the OLDER binary carries it (finding 2). Recorded as a
release precondition; every later schema bump inherits it for free.

**UR-2 — A long migration holding the writer lock makes concurrent old
writers time out.** They wait rusqlite's busy timeout, then fail with a
`Db` error — a refusal, not corruption, and the same outcome U1 would
have given them a moment later.

**UR-3 — Old sessions reading a breaking schema see raw `Db` errors.**
Accepted with UD-1; the first write tells them why.

## Phase plan

**Phase 0 — the helper and its error.** `db::begin_fenced`,
`DbError::SchemaVersionSkew`, db facts (fenced on a current cache: Ok;
newer: refused; older: refused).

**Phase 1 — every writer site.** The ~40 transaction constructors in
`session.rs` and the `citations_db` writer route through the helper
(mechanical); the source pin that no raw constructor remains outside
`db::migrate`.

**Phase 2 — session facts.** The cache moves ahead under a live session
(a raw connection bumps `schema_version`): a save refuses BEFORE the
file write and leaves no marker; a structural op and a create refuse;
reads still serve (U5); restoring the version writes again (U6). The
open-time refusal still holds. `open` waits on the structural lock (U7).

**Phase 3 — docs.** `docs/plans/21` precondition 2 → this document;
precondition 1 → `docs/plans/32` (#1077 satisfied it). This document's
UR-1 is the release precondition.
