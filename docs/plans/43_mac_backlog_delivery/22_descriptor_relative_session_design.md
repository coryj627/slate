# PR 22 — Descriptor-relative session interior: design

Status: design, 2026-10-17, with the owner's decisions of 2026-10-17 in
§11. PR 22a (§7, the core interior, macOS and Linux) is delivered. Its
watcher re-arm (§4.3) is built and off until #1513, per the owner's
decision of 2026-10-19 on #1518 (§11, Q3). PR 22b, the mac host, and PR
22W, the Windows body under Milestone W, are not delivered.
Issue #940 (FL-06 round 28, the A→B→A open race). Spec:
`specs/pr22_descriptor_relative_session.md`. Baseline: `main` at
beed82e1; every file:line reference below was read at that commit (the
spec's line numbers were taken at 709ba8a2 and the FL-06 interior lines
have not moved: `pin_root_identity` is still `session.rs:1068`/`:1084`
and `from_filesystem_with_actor` still `session.rs:3012`). This design
depends on PR 20's watcher design (#1420, under owner review); §4 names
every point where it leans on one of PR 20's open answers Q1–Q7. The
spikes behind §3 ran in a scratch directory on this Mac's APFS volume and
were not committed; their code and output are summarised in Appendix A.

## 1. Summary

1. **Mechanism.** One `FsRoot` convention on both hosts: the session
   holds an open descriptor of the vault root for its lifetime and
   resolves every interior path against it. On macOS and Linux the core's
   own IO (the structural lock, `slate.json`, `.slate/prefs.json`, the
   op-log directory, the worker connection) moves to `openat`-family
   calls against the held `OwnedFd`, exactly as `FsVaultProvider`'s
   pinned mutation target (`vault/fs.rs:247-307`) and the mac sidebar
   store (`SidebarVaultPrefsStore.openSlateDirectory`,
   `Sidebar/SidebarVaultPrefsStore.swift:478-527`) already do. SQLite,
   whose path resolution Slate does not own, opens through a synthetic
   prefix that the unix VFS's own system-call hook
   (`sqlite3_vfs.xSetSystemCall`) maps onto `*at` calls against the held
   `.slate` descriptor. **The issue's and the spec's suggested
   `/dev/fd/<rootfd>/…` path does not work for a directory descriptor on
   macOS** (§3.1, measured); the VFS hook is the mechanism that does.
   Windows is the dirfd-relative IO Milestone W already planned
   (`NtCreateFile` with `RootDirectory` set to the held handle, the
   `AnchoredVaultStore` pattern, W1-RT-07); PR 22 ships the macOS/Linux
   half and fixes the shared `fs_root` convention both use.
2. **What stays pathname-based.** Everything the user reads: error
   message paths, the host window title, Reveal in Finder/Explorer,
   recents, Quick Look. Core keeps one display spelling — the root
   descriptor's current path from `fcntl(F_GETPATH)` (macOS) /
   `GetFinalPathNameByHandleW` (Windows), accepted only when an `fstat`
   of it still equals the anchor, else the spelling the session opened —
   and `canonical_path` (`vault/fs.rs:906-945`) is made relative to the
   anchored root so the stored spelling and the descriptor root cannot
   disagree.
3. **The watcher (PR 20).** PR 20 §4.4 arms FSEvents by path and verifies
   the root's identity at arm time and before every callback, ending the
   watch on a mismatch. PR 22 replaces its `RootAnchor` (the root path +
   its arm-time identity) with the held descriptor: verification becomes
   `fstat(descriptor)` against `stat(path)`. After a *legitimate* root
   rename (the descriptor's identity still matches, only its path moved)
   PR 22 re-arms once at the descriptor's verified current path; a true
   identity mismatch still ends the watch. The re-arm is measured to work
   (§3.5).
4. **The regression.** A `#[cfg(test)]` seam in
   `from_filesystem_with_actor` runs a caller-supplied swap between the
   anchor capture and the interior open, and a restore between the
   interior open and the re-verification — the exact residual schedule
   #940 names. The fact asserts the session's SQLite connection, config
   and lock all bound to A even though the pathname read B. The spike
   (§3.2, probe (a2)) already demonstrates the two outcomes.
5. **The host half.** The mac sidebar store and the Windows
   `AnchoredVaultStore` are already descriptor-anchored; PR 22 does not
   change their admission comparison, only strengthens what the core
   anchor it compares against means (the identity the interior actually
   bound, not a pathname re-sample). Host path *display* stays pathname
   and goes stale on a live rename, which the open-time identity guard
   (`AppState.swift:10082-10090`) already refuses to pair with another
   vault.

Plan (§7): PR 22a (core `FsRoot` + descriptor-relative interior +
SQLite VFS hook + the regression, macOS/Linux, L), then PR 22b (the mac
host display/canonicalisation audit, S) in parallel with the Windows
`fs_root` unification, which rides Milestone W (tracked, not built here).

## 2. What stands on main

- The open pins and re-verifies, then **drops** the descriptor:
  `from_filesystem_with_actor` (`session.rs:3012-3091`) calls
  `pin_root_identity` (`:1068`, Unix: `File::open(root)` + `fstat`), reads
  `slate.json` (`vault_config::read_root_vault_config`,
  `vault_config.rs:66`) and `.slate/prefs.json`
  (`citations::prefs::read_citations_prefs`, `prefs.rs:45`;
  `history_prefs::read_history_prefs`, `history_prefs.rs:73`) **by
  pathname** (`session.rs:3066`, `:3072`), builds
  `FsVaultProvider::new(root)` (`:3076`), calls `VaultSession::open`
  (`:3077`), then re-verifies `observe_root_identity(&root_for_reverify)
  == root_identity` (`:3082`) and `drop(pinned_root)` (`:3088`). The
  descriptor exists only to keep the anchored inode alive across the
  constructor; nothing resolves through it.
- `VaultSession::open` (`session.rs:2485`) takes the structural lock by
  pathname — `VaultStructuralLock::acquire(&config.cache_dir)`
  (`:2493` → `:86`, which `canonicalize`s the cache dir and `flock`s
  `<canonical>/structural.lock`) — and opens SQLite by pathname:
  `db_path = config.cache_dir.join("cache.sqlite")` (`:2494`),
  `db::open_database(&db_path, …)` (`:2496` → `db.rs:230`, a plain
  `Connection::open(sqlite_open_path(path))`). The cache dir is
  `config.cache_dir`, derived once as `root.join(".slate")`
  (`session.rs:3032`) and never re-derived from a descriptor.
- The op-log worker opens its own connection by pathname:
  `open_worker_connection(&cache_dir)` (`session.rs:1484` →
  `Connection::open(.../cache.sqlite)`), and every op-log read/write
  joins `cache_dir` (`oplog::oplog_path_for_name`, `oplog.rs:762`;
  `try_create_log`, `:802`; `append_entry`, `:988`;
  `read_oplog`, `:1187`; compaction's `fs::rename` of a temp,
  `oplog_compaction.rs:325-328`). Fourteen `VaultStructuralLock::acquire`
  / `acquire_cancellable` call sites take the lock by the cache-dir
  pathname (`session.rs:2493`, `:12910`, `:13008`, `:13900`, `:14647`,
  `:15256`, `:15297`, `:15320`, `:15378`, `:15624`, `:15664`, `:15704`,
  `:15750`, `:15902`, `:16036`, `:16219`, `:16256`, `:16304`).
- The provider resolves every entry by pathname from `self.root`
  (`vault/fs.rs:24-27`, `resolve`/`resolve_relative` `:67`/`:556`). It is
  already partly descriptor-relative for mutations: `pin_mutation_target`
  (`:90`) opens the root and walks each component below it with `openat`
  + `O_NOFOLLOW` (`PinnedMutationTarget::open`, `:247-307`;
  `open_directory_at`, `:374`), and publishes with `renameatx_np` against
  the pinned parent fd (`:313-335`). Reads (`read_file` `:648`, `stat`
  `:947`, `list_dir` `:620`) and `canonical_path` (`:906`) are still pure
  pathname.
- The identity anchor crosses the FFI: `VaultSession::root_identity()`
  (`session.rs:2980`), FFI `root_identity()`
  (`crates/slate-uniffi/src/lib.rs:740`), `VaultRootIdentity { device,
  inode }` (`lib.rs:591`). The host opens with
  `VaultSession.openFilesystem(rootPath: url.path)`
  (`AppState.swift:10003`).
- The mac host already anchors its sidebar store and compares against the
  session anchor. `SidebarVaultPrefsStore.openSlateDirectory`
  (`Sidebar/SidebarVaultPrefsStore.swift:478`) opens the root once, `fstat`s
  the descriptor, then `openat(.slate, O_NOFOLLOW|O_DIRECTORY)` and does
  every lock/read/temp/rename against that fd. `readForAdmission()`
  (`:162`) returns content and identity from that one resolution.
  `openVault(at:)` (`AppState.swift:9962`) compares
  `session.rootIdentity()` against the store's admitted identity and
  throws `VaultOpenIdentityRaceError` on a mismatch or a nil
  (`:10082-10090`, `:10355`).
- The Windows host already holds vault/`.slate` handles for its per-vault
  stores (W1-RT-07, closed by #1016/#1024): `AnchoredVaultStore.cs`,
  `SidebarSettingsStore.cs`, `FileRecentsStore.cs` hold and revalidate
  the opened vault and `.slate` identities, open children without
  following reparse points, and rename the written temp through
  `NtSetInformationFile(FileRenameInformation)` with a null root
  (`w1_execution_2026-07-20.md:136-148`,
  `w1_post_merge_adversarial_audit_2026-07-22.md:187-200`). Core's
  Windows provider already does handle-relative no-follow IO through
  `NtCreateFile` with `RootDirectory` set (`vault/windows_entry.rs:56`,
  `:231`).
- Locked 05 §4 principle 1: "Opaque handles, not raw paths"
  (`05:166`); §7.6 fixes the storage layout, `.slate/cache.sqlite` and
  the op-log and prefs under it (`05:1151-1175`).
- The capability matrix PR 24 (#1430, under review) overlaps this
  mechanism: it probed `renameatx_np`, `fgetattrlist`, `F_GETPATH`,
  `O_NOFOLLOW_ANY`, `O_RESOLVE_BENEATH` and `/.vol` on macOS and read the
  Linux semantics (`24_structural_identity_capability_matrix.md` §3–§4).
  §3 and §4 below build on its findings and add the SQLite and lock-file
  answers it did not cover.

## 3. Evidence (the spike)

A scratch crate depending only on `rusqlite = "=0.40.1"` (the workspace
pin, bundled SQLite 3.53.2) and `libc`, built with the pinned toolchain
(`cargo +1.97.1`, Appendix A), run in temp directories under the
scratchpad on this Mac's boot APFS volume (Darwin 27.0.0, macOS 27.0.1).
No probe touched any vault, iCloud Drive or synced folder. Full output in
Appendix A.

### 3.1 `/dev/fd/<rootfd>/…` does not work for a directory descriptor (macOS)

The issue and the spec both propose building interior paths as
`/dev/fd/<rootfd>/.slate/cache.sqlite`. Measured, that does not resolve:

- `lstat("/dev/fd/3")` on a directory fd reports the directory itself
  (same inode as the root), but `/dev/fd/N` on macOS is not a traversable
  directory. `open("/dev/fd/3", O_DIRECTORY)` and `read_dir` both fail
  `ENOTDIR` (os error 20); `read("/dev/fd/3/.slate/x")` fails `ENOENT`.
  `canonicalize("/dev/fd/3")` returns `/dev/fd/A` (the fdesc layer
  substitutes the leaf name), not the real path.
- Therefore `Connection::open("/dev/fd/3/.slate/cache.sqlite")` fails
  SQLite error 14, `SQLITE_CANTOPEN`.
- A `/dev/fd/<n>` that is itself a *file* descriptor opens as a dup
  (`Connection::open("/dev/fd/4")` returns a handle whose
  `sqlite3_db_filename` is `/dev/fd/4`), but SQLite then cannot derive or
  open the `-wal`/`-shm` siblings (it appends `-wal` to that string) and
  WAL setup fails error 14. So handing SQLite a pre-opened db fd is not a
  usable route for a WAL database either.

macOS has no `/dev/fd/<dirfd>`-as-base facility (unlike Linux's
`/proc/self/fd/<n>`, §3.4). The mechanism must be `openat`, not a `/dev/fd`
pathname.

### 3.2 SQLite through the held `.slate` descriptor (macOS)

The unix VFS exposes `xGetSystemCall`/`xSetSystemCall`
(`sqlite3_vfs.iVersion` 3). Overriding `open`, `stat`, `lstat`, `access`
and `unlink` so that a synthetic prefix `"/.slate-anchored/<token>/<rel>"`
dispatches to `openat`/`fstatat`/`faccessat`/`unlinkat` against a
registered `.slate` descriptor (and everything else passes through
unchanged) works:

- `Connection::open("/.slate-anchored/s1/cache.sqlite")` opened, WAL set,
  `sqlite3_db_filename` reports the synthetic path. A second connection
  on the same synthetic path shared the database and read the first
  connection's row. The `-wal` and `-shm` side files were created and
  resolved through the same held descriptor (their inodes, read via the
  `.slate` fd, were stable across the test). Counters for one open +
  WAL + a few inserts: 11 anchored `open`, 25 `fstatat`, 6 `lstat`, 3
  `unlinkat`, 16 passthrough opens (SQLite's `/dev/urandom`, the temp
  dir, etc.).
- **The A→B→A proof.** With the `.slate` fd anchored to A, then A renamed
  to `A-moved` and B renamed onto A's pathname, the anchored connections
  kept writing to A's cache (rows `A1,A2,A3,A4` landed in `A-moved`'s
  database) while a connection opened by the *textual* path wrote to B's
  cache (rows `B0,P1`). The anchored interior binds the original vault;
  the pathname interior binds the swapped-in one.
- **Cross-process WAL still works (§3.3 below is the same point).** The
  override is per-process on one connection family; WAL sharing is by
  inode, and both the synthetic path and the textual path reach the same
  inode, so a second process on the real path shares the database.

### 3.3 Two connections, two sessions, another process

- A child process opening `A/.slate/cache.sqlite` by its real path while
  the parent held an anchored connection: the child saw the parent's
  committed row, inserted its own, and the parent's open read transaction
  kept WAL snapshot isolation (saw the pre-child count until it
  committed, then `P0,C1`). Anchoring one process does not break the
  `slate`-CLI / second-window sharing the vault is designed for
  (`session.rs:3049-3063`).
- Two anchored sessions in one process over one vault, session 1 closing
  and retiring its token first, then eight threads doing 25 inserts each
  on session 2's token: 203 rows, no corruption. A token derived from the
  `.slate` identity (`<dev>-<ino>`) and reference-counted across the two
  sessions behaves the same.

### 3.4 Linux semantics (documentation and the capability matrix; not run here)

No Linux host exists and no container was used, to keep the spike off any
shared or personal data. From the man pages and PR 24's matrix:

- `/proc/self/fd/<n>` on Linux **is** a usable base: a path
  `/proc/self/fd/<rootfd>/.slate/cache.sqlite` resolves through the
  descriptor (the proc symlink is followed to the directory, then the
  rest is an ordinary walk). This is the Linux analogue the macOS
  `/dev/fd` route lacks, and the same SQLite-VFS hook can simply build
  that path on Linux instead of intercepting `open`. Either route is
  available; the openat hook is preferred for one code path on both
  Unixes.
- `openat2(RESOLVE_BENEATH)` (Linux 5.6+) refuses any escape from the
  dirfd during resolution, and `RESOLVE_NO_SYMLINKS` refuses symlinked
  components; `RESOLVE_BENEATH` is `0x08` in libc. These harden the
  interior walk the way `O_NOFOLLOW` per component does today, in one
  call. glibc has no `openat2` wrapper on every target, so core would
  call `SYS_openat2` directly (the `renameat2` precedent,
  `vault/fs.rs:1166-1190`).
- `renameat2` flags are refused on NFS (`EINVAL`; PR 24 matrix §3.2), so
  the Linux no-replace publish already fails closed there; nothing new
  for PR 22.

macOS has `O_RESOLVE_BENEATH` (open(2), value `0x1000` in the macOS 27
SDK) and it is measured to work on this host (§3.6), but it is not usable
while Slate supports macOS 15 (the deployment target is 15.0,
`apps/slate-mac/Package.swift:28`; PR 24 found the rename variant absent
from the macOS 15 XNU). So the macOS interior walk stays per-component
`openat`+`O_NOFOLLOW`, which works on 15.

### 3.5 FSEvents follows the pathname and the descriptor can re-aim it

A watcher on notify 9.0.0-rc.5 (PR 20's pinned version, Q6) over a temp
directory, with a held root descriptor:

- A write under the root was delivered. Renaming the root away delivered
  `Remove(Any)` with info `"root changed"`, after which writes into the
  moved directory were **not** delivered on that stream (FSEvents follows
  the path, as PR 20 §4.4 records).
- `fcntl(F_GETPATH)` on the held root descriptor, verified by
  `fstat`==anchor, returned the directory's new path. A new watcher armed
  at that verified path delivered writes there. So after a legitimate
  rename the watch can be re-aimed at the descriptor's current path.
- A new directory created at the old path was picked up by the old
  stream (another `"root changed"` then its events) — which is exactly
  the swap PR 20 §4.4 refuses by identity.

### 3.6 `F_GETPATH`, containment, and SQLite's own symlink handling

- `fcntl(F_GETPATH)` on a held directory descriptor tracks renames of the
  object and its ancestors and case-only renames, and reports the current
  name. But it is **not** safe to trust unverified: after a rename-over
  (a held empty directory Y that X was renamed onto) `F_GETPATH` returned
  `.../Y`, which then named *another* object; after `rmdir` it returned
  the stale path (an `fstat` of it fails `ENOENT`). So the display path
  must be `F_GETPATH` **gated by** `fstat(fd) == stat(path)` — the same
  rule PR 24's matrix §4 records for `F_GETPATH`.
- `O_NOFOLLOW_ANY` on an `openat` refuses a symlinked component anywhere
  in the relative path (`ELOOP`); `O_RESOLVE_BENEATH` refuses a path that
  escapes the dirfd (`ENOTCAPABLE`) and admits an in-vault one — both
  measured on macOS 27.
- SQLite's bundled VFS resolves a symlinked component in the db pathname
  (it opened a db under a symlinked `.slate` parent). `SQLITE_OPEN_NOFOLLOW`
  refuses only a symlink on the **final** component (error 1550,
  `SQLITE_CANTOPEN_SYMLINK`); it does not harden ancestors. The anchored
  `openat` hook is what refuses a swapped ancestor, which is why the hook
  is needed rather than `SQLITE_OPEN_NOFOLLOW` alone.

## 4. Decisions

### 4.1 The mechanism per platform

**Chosen: one `FsRoot`, resolved by descriptor on every platform.** A new
core type `FsRoot` owns the vault root as an open descriptor (an
`OwnedFd` on Unix, a `HANDLE`/`OwnedHandle` on Windows) plus the opened
`.slate` descriptor, and the identity read from the root descriptor by
`fstat`/`GetFileInformationByHandle`. `FsVaultProvider`, the structural
lock, the op-log, the config reads and the SQLite open all take their
directory from `FsRoot`, not from a `PathBuf`.

- **macOS and Linux — core's own IO: `openat`-family against the held
  descriptors.** The structural lock opens `structural.lock` with
  `openat(slate_fd, "structural.lock", O_RDWR|O_CREAT|O_NOFOLLOW)` and
  `flock`s that fd; `slate.json` is `openat(root_fd, "slate.json",
  O_RDONLY|O_NOFOLLOW)`; `.slate/prefs.json` is `openat(slate_fd,
  "prefs.json", …)`; the op-log directory and its files resolve under
  `slate_fd`; the worker connection opens through the SQLite hook below.
  This is the pattern `FsVaultProvider::pin_mutation_target`
  (`vault/fs.rs:247-307`) and the mac sidebar store
  (`SidebarVaultPrefsStore.swift:478-527`) already use, lifted into a
  shared helper. The spike (§3.2, §3.6(b)) shows the lock and the config
  reads bind the anchored vault through a swap and refuse a symlinked
  component.
- **macOS and Linux — SQLite: the unix VFS `xSetSystemCall` hook.** At
  process start core registers, once, an override of the unix VFS's
  `open`/`stat`/`lstat`/`access`/`unlink` that recognises a synthetic
  prefix (proposed `"/.slate-fd/<token>/"`, `<token>` a per-session
  registry key) and dispatches it to `openat`/`fstatat`/… against the
  registered `.slate` descriptor, passing every other path straight to
  the saved default. The session registers its `.slate` fd under a token,
  opens `Connection::open("/.slate-fd/<token>/cache.sqlite")`, and
  unregisters on close. The `-wal` and `-shm` siblings and a second
  connection all flow through the same hook (§3.2); SQLite's
  directory-fsync (`openDirectory`, `sqlite3.c:44061`) derives the parent
  and opens it through the same overridable `open` slot, so the five
  overrides cover the WAL commit's directory sync too (measured: the
  commit succeeded with no `openDirectory` override). The hook is
  process-global and installed behind a `OnceLock`; per-session state is
  the small token→fd table. This is measured to work in WAL with two
  connections and to leave cross-process sharing by the real path intact
  (§3.2, §3.3).
- **Windows — the dirfd-relative IO Milestone W already plans.** `FsRoot`
  holds the vault and `.slate` handles; interior opens are `NtCreateFile`
  with `RootDirectory` set to the held handle (the `AnchoredVaultStore`
  /`windows_entry.rs:56` pattern), opened without `FILE_SHARE_DELETE` so
  the root cannot be renamed or deleted out from under the session.
  SQLite on Windows uses `winOpen`→`CreateFileW` with absolute paths; the
  **same** VFS `xSetSystemCall` hook maps the synthetic prefix to a
  handle-relative `NtCreateFile`. Windows is heavier (the win VFS
  overrides are `CreateFileW`/`GetFileAttributesW`/… and the shm uses a
  second handle) and is Milestone W work; PR 22 ships the Unix half and
  defines `FsRoot` so Milestone W fills in the Windows body of the same
  trait. One `fs_root` convention, two bodies.

**Rejected.**

- **`/dev/fd/<rootfd>/…` (the issue's and spec's first suggestion).**
  Disproven on macOS for directory descriptors (§3.1): `/dev/fd/<dirfd>`
  is not traversable and SQLite cannot open a child under it. It would
  work on Linux via `/proc/self/fd`, but a route that works on only one
  Unix is not the shared convention.
- **Hand SQLite a pre-opened db-file descriptor as `/dev/fd/<n>`.** The
  main database opens as a dup, but SQLite derives `-wal`/`-shm` by string
  concatenation and cannot open them, and a second connection cannot
  reopen the fd (§3.1). WAL is non-negotiable (`db.rs:346-370`).
- **A full custom `sqlite3_vfs`.** Would have to reimplement WAL locking,
  shared-memory and the sector/truncate methods; the syscall hook reuses
  all of the unix VFS and overrides only name resolution.
- **`SQLITE_OPEN_NOFOLLOW` instead of the hook.** Hardens only the final
  component, not a swapped ancestor (§3.6(c)); does not make the open
  descriptor-relative at all.
- **Leave SQLite pathname-based and rely on the post-open
  re-verification.** This is today's shape and is exactly what #940
  defeats: the re-verification passes because A is restored, while the
  connection is B's (§3.2, probe (a2)).
- **Keep the descriptor only for identity and leave the interior by
  path** (today). The residual survives (§2, #940).

### 4.2 What must stay pathname-based, and canonicalisation

**Chosen.** Pathnames remain the currency of everything the user sees and
of anything that must survive the process:

- **Display.** `VaultError::InvalidPath { path, reason }`
  (`lib.rs:204`) and the host error surfaces
  (`vault_error_detail`, `lib.rs:10932`; mac `humanReadable`,
  `AppState.swift:24571`) quote a path for the user. These keep quoting a
  pathname. The one hazard is that `sqlite3_db_filename` on an anchored
  connection returns the synthetic `"/.slate-fd/<token>/cache.sqlite"`
  (§3.2); any diagnostic that logs it must map it back to the display
  path, so the hook also exposes `display_path(token) -> PathBuf`.
- **The single display spelling.** Core gains
  `VaultSession::root_display_path() -> PathBuf`, computed as
  `fcntl(F_GETPATH)` of the root descriptor (macOS) /
  `GetFinalPathNameByHandleW` (Windows) **accepted only when** an
  `fstat`/`GetFileInformationByHandle` of it still equals the anchor,
  else the spelling the session was opened with. §3.6(a) is why the gate
  is mandatory: an ungated `F_GETPATH` can name another object after a
  rename-over or a stale path after an unlink. Hosts that want to show
  "the vault moved to X" read this; hosts that just want the opened name
  keep `currentVaultURL` (`AppState.swift:10066`).
- **Canonicalisation.** `FsVaultProvider::canonical_path`
  (`vault/fs.rs:906-945`) today canonicalises both the entry and
  `self.root` by pathname. It is made relative to the anchored root: the
  entry is resolved under the held root descriptor (the stored spelling
  comes from `getattrlistat(ATTR_CMN_NAME)` per component, which the
  spike shows returns the on-disk case — `notes`→`Notes` — §3.6/Appendix
  A probe (e)), and "beneath the vault" is proven by the walk staying
  under the descriptor rather than by `strip_prefix` of two canonical
  pathnames. This keeps the I1/I7 contract (`canonical_path` returns the
  stored spelling or fails closed outside the root) consistent with the
  descriptor root instead of a re-canonicalised pathname that a swap
  could move. #1077's `canonical_path` facts must stay green.

**Rejected.** Making error messages quote the synthetic path (meaningless
to the user); dropping `canonical_path`'s pathname return in favour of a
descriptor (the FFI and the index store spellings, not fds); trusting
`F_GETPATH` ungated for display (§3.6(a)).

### 4.3 The watcher's root (PR 20) and FSEvents' path-based API

**Chosen.** PR 20 §4.4 already settled that the watcher's events are
hints that only request a rescan, that it arms by path and verifies the
root's identity at arm time and before every callback, and that it ends
the watch on a mismatch. PR 22 makes three changes:

1. **The anchor becomes the descriptor.** PR 20's `RootAnchor` (root path
   + arm-time identity) is replaced by the session's `FsRoot`.
   Per-callback verification is `fstat(root_fd)` against `stat(path)` on
   Unix and `GetFileInformationByHandle(root handle)` against a fresh
   open on Windows — a descriptor comparison, not a re-sampled pathname
   pair.
2. **Re-arm after a legitimate rename.** When the per-batch check finds
   the descriptor's identity intact but its path moved (a user renamed
   the vault folder), PR 22 re-arms one new FSEvents stream at
   `root_display_path()` (§4.2) and drops the old one, at most once per
   burst and giving up after a small bound (proposed 4) of re-arms in one
   session to avoid a rename-storm loop. §3.5 measures that a re-armed
   stream at the verified path delivers events. When the identity no
   longer matches (a swap), the watch still ends without a callback, as
   PR 20 decided.
3. **No path canonicalisation at event time** stays PR 20's rule; the
   relative path is stripped from the watched spelling.

**Dependencies on PR 20's open questions.**

- **Q6 (pin `=9.0.0-rc.5`).** §3.5 and the re-arm decision are measured
  on rc.5. rc.4+ keeps the watched path's spelling and rc.5 surfaces a
  renamed/replaced root as `"root changed"`
  (`20_vault_watcher_design.md` §3.1); if the owner waits for 9.0.0 the
  behaviour is the same line, but if 8.2.0 were chosen the re-arm signal
  would be unreliable and §4.3(2) could not stand.
- **Q7 (FSEvents exercised only by the mac host integration fact).** PR
  22's re-arm fact is a core fact on the real FSEvents backend; if the
  owner keeps Q7's "mac host integration fact only" posture, the re-arm
  is witnessed through the mac host, not a mac-lane `cargo test`.
- **PR 20 §4.4's explicit hand-off.** PR 20 wrote "PR 22 replaces the
  anchor with the held descriptor" and "decides whether to re-arm … or
  keep 'end the watch'". §4.3 is that decision: re-arm on a proven
  legitimate rename, end on a mismatch. PR 22 must keep PR 20 §4.7's root
  facts (`a_swapped_root_ends_the_watch_without_a_callback`,
  `a_root_change_event_ends_the_watch`) green and adds
  `a_legitimate_root_rename_rearms_at_the_new_path`.

**Rejected.** Never re-arming (PR 20's conservative default — a user who
renames the open vault folder would silently lose live updates until the
next open, where a rescan already runs; re-arming is cheap and measured);
re-resolving the root path per event (a cost per event and still
pathname-based, PR 20 §4.4 rejected it); watching the descriptor directly
(FSEvents has no descriptor API; kqueue on the root fd watches one
directory, not the tree — PR 20 §4.4).

### 4.4 The deterministic A→B→A regression

**Chosen: a `#[cfg(test)]` swap seam in `from_filesystem_with_actor`.**
The constructor gains two no-op-in-release hook points, driven by a
test-only installed callback (the `SLATE_TEST_*` env-seam precedent,
e.g. `SLATE_TEST_COMMIT_AFTER_INTENT`, `session.rs:4061`): one fires
**after** `pin_root_identity` and before the interior reads/open, one
fires **after** `VaultSession::open` and before the re-verification. The
fact:

1. Builds vault A with `.slate/cache.sqlite` carrying a sentinel row
   `A0`, `slate.json` naming `TemplatesA`, `.slate/prefs.json` with
   `retentionDays: 11`; builds vault B with `B0`, `TemplatesB`,
   `retentionDays: 22`.
2. Installs a swap hook that, on the first fire, renames A→`A-hold` and
   B→A's pathname; on the second fire, renames A(=B)→B and `A-hold`→A.
3. Opens A through `from_filesystem_with_actor`, then asserts: the
   session's own connection sees row `A0` (not `B0`) and a row it writes
   lands in A's cache; the templates dir resolved is `TemplatesA`; the
   retention came from A's prefs; and the structural lock is A's. Today
   (pathname interior) every one of these is B's while the open still
   *succeeds* (the re-verification passes because A was restored) — the
   spike's probe (a2) shows exactly this split (`today` vs `anchored`
   rows). With the descriptor-relative interior the fact passes.

The seam is the minimum that reproduces #940's residual schedule
deterministically, without a race, because the swap is driven from inside
the open at the two exact points #940 names. It mirrors the spike. A
companion fact drives the same seam through `VaultSession::open` directly
(the path all the host and bench callers share).

**Rejected.** A timing/race-based test (flaky, and #940 explicitly asked
for a deterministic seam); a provider-level fake that returns B's bytes
(would not exercise the real SQLite/lock/config resolution the bug lives
in); asserting only that the open fails (the point is that the open
*succeeds* on the anchored vault, not that it is refused).

### 4.5 The host half

**Chosen.** The hosts already do the right comparison; PR 22 strengthens
what it compares against and audits display.

- **mac.** `SidebarVaultPrefsStore` is already descriptor-anchored
  (`:478-527`) and `openVault(at:)` already refuses an open whose store
  identity and `session.rootIdentity()` disagree
  (`AppState.swift:10082-10090`). After PR 22 `session.rootIdentity()` is
  the identity the interior actually bound (not a re-sampled pathname), so
  the comparison proves the sidebar store and the SQLite cache share one
  physical root, which is the property #940's last paragraph says the
  host wants ("The macOS host refuses to run the sidebar store against a
  root that does not match the session's anchor"). No admission code
  changes. The one audit item is **display**: `currentVaultURL`
  (`:10066`) and the drag-in-vault check
  (`vaultURL.resolvingSymlinksInPath().standardizedFileURL.path`,
  `:18709`, `:18736`) read the opened pathname; after a live rename these
  go stale. That is acceptable (the identity guard already refuses to
  pair a swapped open with another vault, and a rename of the *open*
  vault is rare), and the host may optionally refresh its title from
  `root_display_path()` (§4.2). The mac app is **not** sandboxed (no
  `com.apple.security.app-sandbox` entitlement; it opens vaults by
  pathname from an `NSOpenPanel`, `AppState.swift:10908`), so descriptor
  reopen and `openat` are unrestricted. [host inventory, §5]
- **Windows.** `OpenVaultAsync` does **no** open-time identity check,
  because `root_identity()` is always `None` off Unix
  (`session.rs:1053-1061`, `lib.rs:740`; §5.2). The `AnchoredVaultStore`
  family opens the root/`.slate` by path per operation without
  `FILE_SHARE_DELETE` and compares identity as text via
  `GetFinalPathNameByHandleW` (`W/AnchoredVaultStore.cs:249-258`,
  `:439`), not a volume-serial/file-ID pair. So on Windows the guarantee
  rests on Milestone W's descriptor-relative interior (§4.1), and giving
  the Windows session a real `root_identity()` from the held handle — so
  the host can compare as the mac host does — is part of that work. No
  behaviour change in PR 22a; the Windows body of `FsRoot` is Milestone W.

**Rejected.** Threading the descriptor across the FFI to the host (the
hosts already hold their own anchored handles; the FFI carries the
identity, not the fd); changing the admission comparison (it is correct;
only its meaning strengthens).

## 5. Host inventory of vault-root pathname uses

A full read of both hosts at beed82e1 (paths `M/ =
apps/slate-mac/Sources/SlateMac/`, `W/ =
apps/slate-windows/src/SlateWindows/`). This grounds §4.2 and §4.5 and
scopes what PR 22 does and does not fix. PR 22 changes the **core**
session interior and the core anchor the hosts compare against; the
host-owned per-vault stores and display sites below are a separate
surface, and this inventory marks which of them PR 22 leaves stale or
would want the shared `fs_root` convention to reach next.

### 5.1 mac

- **Display (13 sites).** Window title and first-mount VoiceOver
  announcement are `currentVaultURL.lastPathComponent`
  (`M/MainSplitView.swift:177`, `:941-946`, `:1332-1334`); the
  switch-announcement uses `vaultRoot.lastPathComponent`
  (`M/AppState.swift:10757-10763`); recents store `url.path` and cache
  `lastPathComponent` (`M/RecentVault.swift:26-33`,
  `M/AppState.swift:24555-24567`) and feed Open Recent
  (`M/SlateMacApp.swift:406-412`), the switcher
  (`M/SidebarUtilityBar.swift:118-133`) and Welcome
  (`M/WelcomeView.swift:55-133`); the bibliography error quotes
  `<root>/.slate/prefs.json` (`M/SettingsView.swift:707`,
  `M/PrefsJsonStore.swift:228-243`); `{{vault}}` expands to
  `vaultURL.lastPathComponent` and is written into note content
  (`M/AppState.swift:24180-24186`). Core `InvalidPath` errors surface the
  root string (`M/AppState.swift:24575-24576`).
- **Host-side IO built from the root (22 sites).** Per-vault stores
  **inside the vault**, all plain-path IO except the sidebar store:
  `SidebarVaultPrefsStore` → `.slate/sidebar.json` (descriptor-anchored,
  §5.3); `WorkspaceStore` → `.slate/workspace.json`
  (`M/Workspace/WorkspaceStore.swift:152-175`, `createDirectory` at
  `:168`); `SearchRecentsStore` → `.slate/search-recents.json`
  (`M/SearchRecentsStore.swift:62-102`, `createDirectory` at `:95`);
  `PrefsJsonStore` → `.slate/prefs.json` (the file core also writes,
  `M/PrefsJsonStore.swift:84-170`, `createDirectory` at `:159`);
  `GraphConfigStore` → `.slate/graph.json`
  (`M/Graph/GraphConfigStore.swift:19-107`, `createDirectory` at `:103`).
  Per-vault stores **outside the vault, keyed by identity or path**:
  `FileRecentsStore` in UserDefaults keyed `…v2.<dev>-<ino>` with a path
  fallback (`M/FileRecentsStore.swift:37-65`); `RecentVaultsStore` in
  Application Support keyed by the exact path
  (`M/RecentVaultsStore.swift:42-151`). Watchers/opens: the host
  `SyncMarkerWatcher` holds `open(path, O_EVTONLY)` descriptors on the
  root, `.obsidian` and `.obsidian/plugins` for the session and re-opens
  by path after a delete/rename (`M/SyncMarkerWatcher.swift:186-257`);
  Reveal in Finder `activateFileViewerSelecting([root+rel])`
  (`M/AppState.swift:2131-2146`, `:20726-20731`); the canvas card
  `NSWorkspace.open(root+target)`
  (`M/Canvas/AppState+CanvasExtras.swift:505-523`). Drag-out publishes
  `root+rel` file URLs (`M/FileTreeSidebar.swift:7875-7958`); the Finder
  move-vs-import split is a symlink-resolved path containment check
  (`M/AppState.swift:18650-18758`) and the import walker opens the vault
  by its path components (`M/Sidebar/SidebarImportSourceWalker.swift:
  1261-1281`). Many `root+rel` existence checks (sidebar action
  validation `M/AppState.swift:1854-1894`; Reopen Closed Tab `:3648-3657`;
  undo/redo safety `:18277-18299`; `uniqueName` `:20050-20058`;
  batch-trash probes `:19264-19282`). Pickers seeded with/compared to the
  root: Bases save panels (`M/Bases/AppState+Bases.swift:927-950`),
  the wikilink picker (`M/PropertyEditorRow.swift:758-778`) and the
  bibliography pickers (`M/SettingsView.swift:890-964`).
- **Open funnel.** `VaultSession.openFilesystem(rootPath: url.path)`
  (`M/AppState.swift:10003`, in `openVault(at:)` `:9962-10309`);
  `currentVaultURL = url` stored as-is (`:10066`). `url` is an
  `NSOpenPanel` result (`M/VaultPicker.swift:13-23`), a recents string
  via `URL(fileURLWithPath:)` (`:10837-10856`), or a launch-restore
  recents entry (`M/SlateMacApp.swift:359`). **No canonicalisation** —
  `url.path` passes through with no `standardized`, realpath or symlink
  resolution; only the later containment helpers resolve symlinks
  (`:18709`, `:18736`). No command-line vault arg, no open-document
  handler, no bookmarks.
- **Not sandboxed.** No `*.entitlements` anywhere in the worktree, no
  `com.apple.security.*`; the build signs ad-hoc (`--sign -`, no
  `--entitlements`, `scripts/build-mac-app.sh:220-222`). No
  security-scoped bookmark on the root (`startAccessingSecurityScopedResource`
  is used only on dropped/picked/save URLs). So descriptor reopen and
  `openat` on the root are unrestricted, which is what makes the `FsRoot`
  mechanism (§4.1) and the existing sidebar anchoring possible.

### 5.2 Windows

- **Display (13 sites).** The workspace header shows `VaultDisplayName`
  (basename) and the full `VaultPath`
  (`W/MainWindow.xaml:877-878`, set `W/VaultLifecycleViewModel.cs:511-512`);
  the open announcement uses `RecentVault.SpokenName`
  (`:514`, `:882-886`); recents, the Jump List
  (`W/RecentVaultJumpList.cs:25-35`), "Vault not found: <path>"
  (`:724`) and `{{vault}}` (`:1165-1166`) all quote the path or basename.
  The window title itself is a fixed "Slate" (`W/MainWindow.xaml:11`).
- **Host-side IO (14 sites).** Per-vault stores inside the vault via the
  shared `AnchoredVaultStore` (handle-anchored, §5.3):
  `SidebarSettingsStore` → `.slate\sidebar.json`
  (`W/SidebarSettingsStore.cs:246-294`); plain-path stores:
  `WorkspacePersistence` → `.slate\workspace.json`
  (`W/WorkspacePersistence.cs:92-213`); `GraphConfigStore` →
  `.slate\graph.json` (`W/Graph/GraphConfigStore.cs:53-125`,
  `Directory.CreateDirectory` at `:98`); `SearchRecentsStore`
  (`W/Search/SearchRecentsStore.cs:88-187`). Outside the vault:
  `FileRecentsStore` under `%LOCALAPPDATA%` keyed by the upper-cased root
  path (identity is always null on Windows,
  `W/FileRecentsStore.cs:22-37`); `RecentVaultsStore`
  (`W/RecentVaultsStore.cs:16-160`). Three `FileSystemWatcher`s on the
  root/`.obsidian`/`.obsidian\plugins` fixed at construction
  (`W/SyncMarkerWatcher.cs:115-148`, each event re-checks
  `Directory.Exists`). Reveal via `Process.Start("explorer.exe",
  "/select,…")` (`W/FilesSidebarViewModel.FileManagement.cs:1139-1163`);
  canvas media `Path.Combine(root,target)` then ShellExecute
  (`W/ExternalLinkPolicy.cs:152-196`); import prefix-check against the
  root (`W/FilesSidebarViewModel.Import.cs:246-251`). No drag-and-drop;
  the clipboard only ever gets text.
- **Open funnel.** `VaultSession.OpenFilesystem(root)`
  (`W/VaultLifecycleViewModel.cs:483`); `root = Path.GetFullPath(path)`
  (`:446`) is **lexical only** — no junction/symlink resolution, no
  final-path lookup, no case fixing. `path` comes from an
  `OpenFolderDialog`, a recents entry after `Directory.Exists`, or the
  first command-line argument / a second-instance forward
  (`W/App.xaml.cs:137-164`, Jump List tasks pass the recent path).

### 5.3 Identity checks, and what PR 22 changes for them

- **mac is already descriptor-anchored and compares against the core
  anchor.** `SidebarVaultPrefsStore.openSlateDirectory` opens the root
  by path per operation, `fstat`s it, compares to the expected
  `RootIdentity(device, inode)`, and does `.slate` lock/read/temp/rename
  relative to that descriptor (`M/Sidebar/SidebarVaultPrefsStore.swift:
  478-576`); the descriptor lasts one operation. `openVault(at:)`
  compares `session.rootIdentity()` to the store's admitted identity once
  at open and throws `VaultOpenIdentityRaceError` on a mismatch or nil
  (`M/AppState.swift:10073-10090`). The identity-keyed registries
  (writer chains, file-recents key) survive a rename
  (`M/AppState.swift:5124-5147`, `M/FileRecentsStore.swift:51-59`). **PR
  22 does not change any of this**; it changes what `session.rootIdentity()`
  *means* — the identity the interior (SQLite, lock, config) actually
  bound, not a pathname re-sample — so the comparison now proves the
  sidebar store and the SQLite cache share one physical root (#940's host
  requirement).
- **Windows has no open-time core anchor.** `root_identity()` is always
  `None` off Unix (`session.rs:1053-1061`, `lib.rs:740`), so
  `OpenVaultAsync` does no identity check at open
  (`W/VaultLifecycleViewModel.cs:436-583`) and the Windows
  `FileRecentsStore` key falls back to the path. The `AnchoredVaultStore`
  opens the root/`.slate` by path per operation **without
  `FILE_SHARE_DELETE`** (so a rename/delete is blocked while an op runs),
  rejects reparse points, and compares identity **as text** via
  `GetFinalPathNameByHandleW` — no volume-serial/file-ID check
  (`W/AnchoredVaultStore.cs:249-258`, `:333-363`, `:439`). (The canvas
  policy is the exception: it uses the 128-bit `FILE_ID_INFO`,
  `W/ExternalLinkPolicy.cs:378-388`.) So on Windows the descriptor-relative
  interior (Milestone W, §4.1) is the main guarantee once it lands, and
  giving the Windows session a real `root_identity()` from the held
  handle is part of unifying `FsRoot` there.

### 5.4 What goes stale on a live rename, and PR 22's scope

If the vault folder is renamed while open (APFS allows it; on Windows the
session's watcher and `cache.sqlite` handles may block it — untested):

- **PR 22 fixes:** the core SQLite cache, structural lock, `slate.json`,
  `.slate/prefs.json` and op-log keep operating on the original directory
  through the held descriptor (§3.2, §4.1); the mac admission comparison
  proves the sidebar store and cache share a root.
- **PR 22 does not fix (host-owned, pre-existing):** the display stays on
  the old name; **four mac host stores recreate the old path as an empty
  folder** because they `createDirectory(withIntermediateDirectories:
  true)` on save — `WorkspaceStore` (`:166-175`), `SearchRecentsStore`
  (`:93-102`), `PrefsJsonStore` (`:153-170`), `GraphConfigStore`
  (`:99-107`) — after which mac launch-restore silently opens that empty
  folder as the vault (`M/AppState.swift:10863-10868`); Reveal, drag-out,
  canvas opens and the `root+rel` existence checks give wrong answers;
  the host sync-marker watcher drops itself and cannot re-arm
  (`M/SyncMarkerWatcher.swift:190`, `:248-255`); the Windows host stores
  fail or recreate the old path the same way. These are the same class of
  bug #940 names, one layer out (host per-vault IO rather than the core
  session), and are called out in Q8 below as a candidate for the shared
  `fs_root` convention once core has it — but they are outside PR 22's
  core deliverable and #940's scope.

## 6. Core and FFI sketch

```rust
// vault/fs_root.rs (new)
/// The vault root held open for a session's lifetime. Interior paths
/// resolve against these descriptors, never by re-walking the pathname.
pub struct FsRoot {
    #[cfg(unix)] root: OwnedFd,          // O_DIRECTORY|O_CLOEXEC on the vault root
    #[cfg(unix)] slate: OwnedFd,         // openat(root, ".slate", O_DIRECTORY|O_NOFOLLOW)
    #[cfg(windows)] root: OwnedHandle,   // NtCreateFile, no FILE_SHARE_DELETE
    #[cfg(windows)] slate: OwnedHandle,
    identity: VaultRootIdentity,         // fstat / GetFileInformationByHandle of `root`
    opened_as: PathBuf,                  // the spelling the session opened (display fallback)
}

impl FsRoot {
    pub fn open(root: &Path) -> Result<Self, VaultError>;      // opens, fstats, opens .slate
    pub fn identity(&self) -> VaultRootIdentity;
    pub fn verified_current_path(&self) -> Option<PathBuf>;    // F_GETPATH gated by fstat==identity
    pub fn display_path(&self) -> PathBuf;                     // verified_current_path().unwrap_or(opened_as)
    // interior opens, all O_NOFOLLOW / RootDirectory-relative:
    fn open_config(&self, name: &str) -> io::Result<Option<Vec<u8>>>;   // slate.json (root), prefs.json (.slate)
    fn open_lock(&self) -> io::Result<File>;                             // openat(.slate, "structural.lock", …)
    fn sqlite_token(&self) -> SqliteAnchorToken;                         // registers .slate fd, returns the hook path
}

// db.rs: a process-global unix/win VFS syscall hook, installed once.
pub(crate) fn open_database_anchored(token: &SqliteAnchorToken, cache_pages: u32)
    -> Result<Connection, DbError>;   // Connection::open("/.slate-fd/<token>/cache.sqlite")

// session.rs
impl VaultSession {
    // from_filesystem_with_actor builds an FsRoot once and threads it to
    // open(), the provider, the lock and the config reads.
    pub fn root_display_path(&self) -> PathBuf;   // self.fs_root.display_path()
    #[cfg(test)] fn install_open_swap_seam(hook: OpenSwapSeam);  // §4.4
}
```

The structural-lock registry key changes from the canonicalised cache
pathname (`session.rs:88`, `:7515`, `:7540`) to the `.slate` descriptor's
`(dev, ino)` identity, because the spike (§3.2, probe (b)) shows the
canonicalised pathname is **the same string before and after a swap**, so
a path-keyed registry would treat B's lock as A's slot; an
identity-keyed registry does not. The cross-process `flock` stays, on the
descriptor-opened lock file.

No FFI signature changes: `openFilesystem(rootPath:)` and
`root_identity()` keep their shapes; `root_display_path()` is an additive
FFI method for hosts that want to show a moved vault's new path. If PR 20a
has not landed, PR 22a adds `FsRoot` and the interior rewrite without the
watcher; PR 20's watcher then takes its `RootAnchor` from `FsRoot`.

## 7. Implementation plan

| PR | Scope | Size | Lanes | After |
|---|---|---|---|---|
| 22a | Core `FsRoot`; descriptor-relative structural lock, `slate.json`, `.slate/prefs.json`, op-log dir and worker connection; the SQLite unix-VFS syscall hook with the per-session token; identity-keyed structural-lock registry; `canonical_path` relative to the root descriptor; `root_display_path()`; the A→B→A regression seam and facts (macOS + Linux). **Delivered 2026-10-18:** `vault/fs_root.rs` (`FsRoot`, the root and its `.slate` held open, `.slate` made with `mkdirat` and opened `O_NOFOLLOW`; `CacheDir`, the interior's `openat`-family IO, whose creates open the name first and make it with `O_EXCL` only when it is missing, because Darwin's `openat` with `O_CREAT` through a descriptor can fail with `ENOENT` in a create race, and the hook's creates do the same; the mac host's twin is #1521; the display path gated by `fstat`, sharing the Trash handoff's check), `db/anchored.rs` (the hook overrides `open`, `stat`, `lstat`, `access`, `unlink` and `readlink`; tokens are never reused, and a retired one is `ENOENT`; on macOS anchored connections open through the `unix-posix` VFS, because the default `unix` VFS picks its locking by a `statfs` of the path, which the hook cannot reach), the registry keyed by the held `.slate`'s `(device, inode)`, the op-log and the history prefs written beneath the held `.slate`, the provider adopting the held root for renames, saves, creations and Move to Trash (option H's `pinned_root`) and staging in the held `.slate/tmp` (#940's comment of 2026-10-17), `canonical_path` walked beneath the root on macOS (a folder on the way that is a link answers `None`), the watch anchored to the held root, and `root_display_path()` across the FFI. Re-arm built, off until #1513: the re-arm at the held root's verified current path (at most four times a session, with the root decision waiting for the burst to close) sits behind one switch, `WATCH_REARM_ENABLED` in `session/watch.rs`, which the FFI reads as `vault_watch_rearm_enabled()`. While it is off, a renamed vault folder ends the watch, as PR 20 decided, and the two re-arm facts are gated by it. Provider reads stay pathname-based (#1513). Facts in `session/tests/descriptor_interior.rs`, `session/tests/watch.rs`, `vault/fs_root.rs`, `db/anchored.rs`, slate-uniffi and the mac host's `VaultRescanTests+Watcher.swift`; the open and the rescan of a 10k-note vault (`first_open_and_scan/10000`, `reopen_with_cache/10000`) are unchanged within noise, so `BENCHMARKS.md` is not | L (about 1,500–2,000 with facts) | rust, swift, windows | PR 20a if it lands first; else independent and PR 20a rebases onto it |
| 22b | mac host display/canonicalisation audit: optional title/`currentVaultURL` refresh from `root_display_path()`; confirm the admission comparison and the drag/reveal path uses; a host fact that a swapped open is refused (already green) and that a renamed-open vault keeps working | S | swift, a11y, windows | 22a |
| 22W | Windows `FsRoot` body (`NtCreateFile` RootDirectory-relative interior + the win-VFS SQLite hook), unifying `AnchoredVaultStore` with `FsRoot` | M, Milestone W | windows | 22a, Milestone W |

**Facts by PR.**

- **22a.** `the_interior_binds_the_anchored_root_through_an_a_b_a_swap`
  (the §4.4 seam, over SQLite, the lock, `slate.json` and `prefs.json`);
  `a_swapped_root_during_open_still_fails_the_reverification`
  (today's guard stays, now redundant-but-kept);
  `sqlite_wal_two_connections_through_the_anchored_path`;
  `a_second_process_on_the_textual_path_shares_the_anchored_cache`;
  `the_structural_lock_registry_is_keyed_by_identity_not_the_canonical_path`;
  `canonical_path_is_computed_under_the_root_descriptor` (the #1077 I1/I7
  facts stay green); `a_symlinked_interior_component_is_refused`
  (`O_NOFOLLOW`); `root_display_path_is_gated_by_the_anchor`
  (`F_GETPATH` + `fstat`, the §3.6(a) hazard). Linux facts run on the rust
  lane (`.github/workflows/rust.yml`); the SQLite-hook and lock facts are
  portable.
- **22b.** `a_renamed_open_vault_keeps_working_and_can_show_its_new_path`;
  the existing `VaultOpenIdentityRaceError` fact stays green.
- **22W.** the 22a core facts under the Windows `cargo test -p slate-core`
  lane plus the `AnchoredVaultStore` unification tests.

**Interlocks.**

- **PR 19 (mac rescan baseline).** No dependency; PR 19 reads the index,
  not the root descriptor. PR 22 keeps the program §9 rule "PRs 19, 20,
  22 all touch root identity; keep them sequential" by landing 22a after
  20a (both touch the anchor), but 22a does not need 19.
- **PR 20 (watcher).** 22a replaces PR 20's `RootAnchor` with `FsRoot`
  and adds the re-arm (§4.3). If 20a lands first, 22a edits the watcher's
  anchor; if 22a lands first, 20a reads `FsRoot` from the start. Either
  order keeps PR 20 §4.4/§4.7's root facts green. §4.3 depends on PR 20's
  Q6 (notify pin) and Q7 (where FSEvents runs in CI).
- **PR 21 (change feed).** Independent. The feed records index mutations;
  the connection it records through is the anchored connection after PR
  22, which only strengthens the "this is the right vault" guarantee the
  feed's `writer_id` relies on.
- **PR 24 (#1125 capability matrix).** Shares the macOS/Linux identity
  mechanism. PR 24's matrix §4 (what `F_GETPATH`/`fstat`/`fgetattrlist`
  can and cannot guarantee) is the evidence base for §4.2's gated display
  path and §4.4's identity comparison; PR 24's decision memo option H
  (harden the rename relative to the pinned root with `RENAME_NOFOLLOW_ANY`)
  is the sibling of PR 22's interior hardening and should share the
  `FsRoot` helper if both land. PR 22 does not touch the rename
  *capability* (`rename_if_identity` stays Windows-only, per the memo);
  it hardens the session *interior*.
- **In `00_program.md` §5**, PR 22's row stays design-first; on
  acceptance it becomes 22a, 22b and 22W.

## 8. Documents and registers the implementing PRs change

- Locked 05 §4 principle 1 ("Opaque handles, not raw paths", `05:166`)
  gains a note that the desktop `FsVaultProvider` and the session interior
  are now descriptor-relative, closing the gap between the principle and
  the `FsVaultProvider`'s historical pathname resolution.
- `docs/plans/32_canonical_file_identity_contracts.md:417` ("Descriptor-
  relative session interior … that is #940") is updated to point at this
  design and, on implementation, at the landed PRs.
- Contract 40 (if a row exists for the A→B→A residual) records #940
  closed by the descriptor-relative interior, with the Windows arm-race
  residual PR 20 §4.4 names left to Milestone W.
- The FL-06 round-28 comment in `session.rs:3038-3047` (the
  `from_filesystem_with_actor` note that "the interior reads and the cache
  open below still resolve by pathname … tracked separately") is deleted
  when 22a lands, because it no longer holds.

## 9. Residual risks

1. **The SQLite syscall hook is process-global.** It overrides the unix
   VFS's `open`/`stat`/`lstat`/`access`/`unlink` for every connection in
   the process. The override passes any path without the synthetic prefix
   straight through to the saved default (measured: 16 passthrough opens
   in one session, §3.2), so non-anchored connections (an in-memory test
   db, a future second database) are unaffected; but the hook is a
   process-wide install and must be audited as such. An alternative that
   scopes the override to one `sqlite3_vfs` registered per session is
   possible if the global install proves too broad.
2. **A pre-release dependency** only if §4.3's re-arm rides notify
   rc.5 (PR 20's Q6); core's own `FsRoot` and SQLite hook have no new
   dependency.
3. **`F_GETPATH` staleness** (§3.6(a)) is mitigated by the `fstat` gate,
   but a display path can still lag a rename by one observation; it is
   display only.
4. **Windows is deferred to Milestone W.** Until then the Windows session
   interior stays pathname-based (its per-vault *stores* are already
   anchored, W1-RT-07); the A→B→A residual is closed on macOS/Linux first.
   The Windows arm-race residual PR 20 §4.4 records is unchanged.
5. **The structural-lock registry re-key** (pathname → identity) touches
   a heavily-reviewed concurrency surface (`session.rs:61-154`,
   `:7511-7585`); the change is keyed-by-identity, the `flock` and the
   cross-process semantics are unchanged, and the existing structural-lock
   facts must stay green.
6. **iCloud/File-Provider roots.** PR 24's matrix notes File-Provider
   items can be dataless and a descriptor open can trigger materialisation;
   the session root is a directory, not a dataless file, so this does not
   bite the anchor, but the owner's vaults on iCloud were deliberately not
   probed (the spike ran only on scratch APFS), so a first run on an
   iCloud vault should be watched.

## 10. Open questions for the owner

- **Q1.** The mechanism is the unix-VFS syscall hook, not the
  `/dev/fd/<rootfd>/…` the issue and the spec proposed — because
  `/dev/fd/<dirfd>` is not traversable on macOS (§3.1, measured).
  Approve the deviation (recommended), or ask for the Linux-only
  `/proc/self/fd` route to be kept as a second code path?
- **Q2.** The SQLite hook is a process-global `xSetSystemCall` override
  (§4.1, §9.1), recommended for its simplicity. Accept the process-global
  install, or require a per-session `sqlite3_vfs` instead?
- **Q3.** Re-arm the watcher once at the descriptor's verified current
  path after a legitimate root rename (§4.3, recommended), or keep PR 20's
  conservative "end the watch, rely on the next open"? This depends on PR
  20's Q6 (pin rc.5) and interacts with its Q7 (where FSEvents is tested).
- **Q4.** Add `root_display_path()` to the FFI and let hosts show a moved
  vault's new path (recommended), or keep hosts showing only the opened
  spelling and never surface a live rename?
- **Q5.** Re-key the structural-lock registry from the canonical cache
  pathname to the `.slate` identity (§6, recommended — the spike shows the
  pathname key is swap-blind, §3.2 probe (b)), accepting a change to the
  concurrency surface?
- **Q6.** Ship Windows under Milestone W (recommended — its stores are
  already anchored and core's Windows provider already does handle-relative
  IO), or block PR 22 until the Windows `FsRoot` body lands too?
- **Q7 (depends on PR 20).** PR 20's Q6 (notify `=9.0.0-rc.5`) and Q7
  (FSEvents exercised only by the mac host integration fact) both bear on
  §4.3; PR 22's re-arm fact needs PR 20's version and its decision on
  where the real FSEvents backend runs in CI.
- **Q8 (host per-vault stores, out of #940's core scope).** The
  inventory (§5.4) found that a live rename of the open vault folder
  leaves four mac host stores (`WorkspaceStore`, `SearchRecentsStore`,
  `PrefsJsonStore`, `GraphConfigStore`) recreating the old path as an
  empty folder, which mac launch-restore then opens as the vault, and the
  Windows host stores behaving similarly — the same bug class as #940 one
  layer out. PR 22 fixes the core session interior and the mac sidebar
  admission, not these host stores. File a follow-up to carry the shared
  `fs_root` convention into the host stores once core has it
  (recommended), or leave them pathname-based with the rename hazard
  documented?

## Appendix A. The spike

Environment: Mac Studio, arm64, macOS 27.0.1, Darwin 27.0.0; `cargo
+1.97.1` (the workspace pin, `rust-toolchain.toml:8`); `rusqlite
=0.40.1` with `bundled` (SQLite 3.53.2, the workspace pin,
`Cargo.toml:28`) and `libc`. Two scratch crates (`spike`, SQLite and
descriptor probes; `fsev`, the FSEvents re-arm probe on `notify
=9.0.0-rc.5`), built `--offline`, run only in temp directories under the
scratchpad (`<scratchpad>/pr22/run`, `/fsrun`), which the programs delete
and recreate. The build folders were deleted after the runs and nothing
was committed. Code outline:

- The SQLite anchoring hook installs `xSetSystemCall` overrides on the
  default unix VFS for `open`/`stat`/`lstat`/`access`/`unlink`; a path
  under `"/.slate-anchored/<token>/"` dispatches to
  `openat`/`fstatat`/`faccessat`/`unlinkat` against a registered
  directory fd (with `O_NOFOLLOW` added on `open`), every other path to
  the saved default. `apply_pragmas` is a copy of `db.rs:346-370` (the WAL
  retry loop, `synchronous=NORMAL`, `temp_store=MEMORY`, `foreign_keys=ON`,
  `cache_size=4096`).
- Probe (a0): `/dev/fd/<rootfd>` traversal and SQLite over it. Probe (a1):
  anchored SQLite, WAL, two connections, an A→B→A swap, the `-wal`/`-shm`
  inodes. Probe (a2): the deterministic A→B→A, `today` (pathname) vs
  `anchored`. Probe (a3): a child process on the textual path. Probe (a4):
  two sessions + eight threads, and a shared identity-derived token. Probe
  (b): the structural lock and config reads against held descriptors, the
  registry-key-before==after check, `O_NOFOLLOW_ANY`/`O_RESOLVE_BENEATH`.
  Probe (c): `F_GETPATH` across rename, swap, move, case-only rename,
  rmdir and rename-over. Probe (d): SQLite's own symlink resolution and
  `SQLITE_OPEN_NOFOLLOW`. Probe (e): the stored spelling via
  `getattrlistat(ATTR_CMN_NAME)`. The `fsev` crate arms a notify watcher,
  renames the root, verifies `F_GETPATH`+`fstat`, re-arms, and writes
  again.

Exact output:

```
== (a0) /dev/fd/<rootfd>/... on macOS (no override installed) ==
root fd 3; A inode …
lstat(/dev/fd/3) -> rc 0, type directory, mode 40444, inode … (same as A: true)
read /dev/fd/3/.slate/x -> error: No such file or directory (os error 2)
read_dir(/dev/fd/3) -> error: Not a directory (os error 20)
open(/dev/fd/3, O_DIRECTORY) -> error: Not a directory (os error 20)
canonicalize(/dev/fd/3) -> /dev/fd/A
openat(rootfd, ".slate/x") -> "hello"
SQLite open /dev/fd/3/.slate/cache.sqlite -> error: Error code 14: unable to open database file
SQLite open /dev/fd/4 (a file fd) -> ok, path() = Some("/dev/fd/4")
  journal_mode=WAL -> error: Error code 14: unable to open database file
  create+insert -> error: Error code 14: unable to open database file

== installing the anchored-path hooks on the unix VFS ==
default VFS "unix", iVersion 3, SQLite 3.53.2
xSetSystemCall(open, stat, lstat, access, unlink) -> [0, 0, 0, 0, 0] (0 = SQLITE_OK)

== (a1) SQLite through the anchored path, WAL, two connections, then a swap ==
conn1 open /.slate-anchored/s1/cache.sqlite -> ok; journal_mode wal
conn2 sees 1 row(s) written by conn1, then inserts A2
A/.slate by its real path: cache.sqlite cache.sqlite-shm cache.sqlite-wal prefs.json
SWAP: A renamed to A-moved; B renamed to A's pathname
conn3 opened after the swap -> journal_mode wal
conn1 inserts A3, conn3 inserts A4; conn4 opened by the textual path inserts P1
-wal inode via held .slate fd … (before swap …); -shm … (before …)   [unchanged]
rows in A-moved's cache: A1,A2,A3,A4
rows in B's cache (at A's path): B0,P1
anchored calls: open 11, stat 25, lstat 6, access 0, unlink 3; passthrough opens 16

== (a3) another process on the textual path shares the anchored WAL database ==
parent (anchored) count 1; child (textual path): journal_mode wal, saw 1 row(s), inserted C1;
parent inside its read txn still sees 1; after commit: P0,C1

== (a4) two anchored sessions on one vault in one process; eight threads ==
rows after session 1 retired its token and 8 threads x 25 inserts on session 2's token: 203 (expected 203)
shared identity token <dev>-<ino>: Q/.slate after the last close: cache.sqlite prefs.json

== (a2) the A->B->A schedule: today's shape vs the anchored interior ==
   today: re-verification passes: true; prefs read retentionDays 22; slate.json TemplatesB; rows A: A0; rows B: B0,X
anchored: re-verification passes: true; prefs read retentionDays 11; slate.json TemplatesA; rows A: A0,X; rows B: B0

== (b) structural lock and config reads against the held descriptors ==
anchored lock: openat(.slate fd, structural.lock) + File::lock -> held, inode …
SWAP: A renamed to A-moved; B renamed to A's pathname
today's registry key before == after the swap: true (<run>/b/A/.slate/structural.lock);
  the path-opened lock is inode P (the held one is H)   [different inodes]
re-open through the held .slate fd -> inode H (same as held: true)
child flock on A-moved/.slate/structural.lock -> Resource temporarily unavailable (os error 35)  [held]
child flock on A/.slate/structural.lock -> acquired   [the swapped-in B's lock is free]
anchored reads: slate.json TemplatesA; .slate/prefs.json retentionDays 11; TemplatesA via fstatat: true
pathname reads (today): slate.json TemplatesB; .slate/prefs.json retentionDays 22; TemplatesA is_dir: false
openat(root, "sub/link/f.md") with sub/link -> outside: plain "outside"; O_NOFOLLOW_ANY error: ELOOP (os error 62)
openat(root, "sub/link/f.md", O_RESOLVE_BENEATH) -> error: Capabilities insufficient (os error 107)
openat(root, "sub/inlink/g.md", O_RESOLVE_BENEATH) -> opened
openat(root, "../outside/f.md", O_RESOLVE_BENEATH) -> error: Capabilities insufficient (os error 107)
openat(root, "in/g.md", O_RESOLVE_BENEATH) -> opened

== (c) F_GETPATH on a held directory descriptor ==
open V: F_GETPATH -> <run>/c/V (names the held directory)
open V: F_GETPATH_NOFIRMLINK -> /System/Volumes/Data<run>/c/V
after rename V -> V2: F_GETPATH -> <run>/c/V2 (names the held directory)
after swap (V2 -> V3, new dir at V2): F_GETPATH -> <run>/c/V3 (names the held directory)
  stat(V2) is the held directory: false
after a move into another directory (Trash/V3): F_GETPATH -> <run>/c/Trash/V3 (names the held directory)
after a case-only rename (Trash/v3): F_GETPATH -> <run>/c/Trash/v3 (names the held directory)
after rmdir of the held directory E: F_GETPATH -> <run>/c/E (stat fails: ENOENT)
  openat(E, "new.md", O_CREAT) -> error: ENOENT; mkdirat(E, "d") -> error: ENOENT
held Y after X was renamed over it: F_GETPATH -> <run>/c/Y (names ANOTHER object)

== (d) SQLite's own pathname resolution of a symlinked component ==
open <run>/d/L/.slate/cache.sqlite -> sqlite3_db_filename <run>/d/V/.slate/cache.sqlite
the same with SQLITE_OPEN_NOFOLLOW -> error: Error code 1550 (SQLITE_CANTOPEN_SYMLINK)

== (e) the stored spelling from descriptors, versus F_GETPATH ==
getattrlistat(root, "notes") -> Ok("Notes"); getattrlistat(Notes fd, "idea.md") -> Ok("Idea.md")
F_GETPATH of notes/idea.md (it has a second link Other/Copy.md) -> <run>/e/V/Notes/Idea.md
std::fs::canonicalize(V/notes/idea.md) -> <run>/e/V/Notes/Idea.md

== FSEvents re-arm (fsev) ==
write V/a.md: Create(File), Modify(Metadata), Modify(Data) on V/a.md
rename V -> V2 (root moves): Remove(Any) V info="root changed"; then events stop for the moved tree
write V2/b.md (inside the moved root): (nothing on the old stream)
verified current path of the held root: Some("<run>/V2")
re-armed at the verified path; write V2/c.md: Create(File)/Modify on V2/c.md  [delivered]
a new directory appears at V and V/x.md is written: old stream reports "root changed" then V/x.md
```

## 11. Owner decisions (2026-10-17)

The owner answered §10 in a decision interview on 2026-10-17. Every answer below is the recommendation.

- **Q1.** The unix-VFS syscall hook replaces `/dev/fd/<rootfd>/…`, which was disproven on macOS. There is no second Linux `/proc/self/fd` path.
- **Q2.** The hook is a process-global `xSetSystemCall` override, not a per-session `sqlite3_vfs`.
- **Q3.** After a legitimate root rename, the watcher re-arms once at the held root's verified current path. A true identity mismatch still ends the watch.
  - *Amended 2026-10-19 (#1518):* the re-arm is built and off until #1513 moves the provider's reads beneath the held root. Until then, every rescan a re-armed watch started would read the vault by its old path and fail, and the host would say "Files refreshed with errors" once a minute. So a renamed vault folder ends the watch, as PR 20 decided. One switch, `WATCH_REARM_ENABLED`, turns the re-arm on with #1513. Both re-arm facts follow it, and `a_legitimate_root_rename_ends_the_watch_while_the_rearm_is_off` holds while it is off.
- **Q4.** `root_display_path()` is added to the FFI, and hosts show a moved vault's verified current path.
- **Q5.** The structural-lock registry is re-keyed by the `.slate` identity.
- **Q6.** macOS and Linux ship now (22a, 22b), and the Windows `FsRoot` body (22W) follows under Milestone W.
- **Q7.** Settled by PR 20's Q6 (rc.5 pinned) and Q7 (the mac host's integration fact), so the re-arm fact rides that witness.
- **Q8.** #1442 (four mac stores recreate a renamed vault's old path) is fixed now with that issue's small change. The shared `fs_root` convention is then carried into the host stores once core has it.
  - *Delivered (#1442):* the four stores write through one helper, `VaultStoreDirectory.prepare` (`apps/slate-mac/Sources/SlateMac/VaultStoreDirectory.swift`), which refuses a root whose identity is no longer the open's and makes at most `.slate` inside it; recents entries record the root's identity and launch-restore refuses a folder that is not it. That helper is the one place the `fs_root` convention replaces. Core's own pathname writers that still recreate the old path are #1474; the Windows twin is #1475.
- **Plan.** §8's split is queued after 20a: 22a, then 22b; 22W waits for Milestone W.
