# WB-28 — 22W: the Windows session holds its vault root and resolves its interior through handles, and its identity is lossless

Issues: #1475 (the identity half; closes here per §5's matrix — WB-12
delivered the `GraphConfigStore` write with `Refs`. This chunk ships as
two PRs: PR 1's body writes `Refs #1475`, and only PR 2's body writes
`Closes #1475`, once PR 1 and PR 2 are both merged and verified), Refs
#940 in both PR bodies (closes by hand after 22b and 22W, RD15). Size L (design first:
`22_descriptor_relative_session_design.md` §7 row 22W, `:796`). Lanes:
rust (Windows cfg), windows, swift (the FFI record change — one
production read and one test read of its fields — and two additive
exports, neither called from Swift). AT: NVDA spot check on PR 1 (the open refusal WD24 adds;
nothing else a reader hears changes — a FAT32 or exFAT vault opens as on
main). Wave: W10, alone.
Depends on: WB-27 (shares `fs.rs` and
`windows_entry.rs`), plan 43 22c merged (`FsRoot` helper names agreed,
§6 gate), and every Open design item (Codex round 12) closed in the
reviewed design, with its fact, before code starts. Parallel with:
nothing; WB-29 and WB-30 follow it. Decision:
WD7 (pending; recommended "the identity half is 22W, here"; under the
other answer — identity parked with Milestone W — this chunk ships only
the `FsRoot` body and the host admission stays text-based). WD22
(pending; recommended "widen `VaultRootIdentity` losslessly, change the
FFI record on both bindings, migrate `FileRecentsStore`'s key on first
open"; under the other answer — hash the 128-bit id into `inode` — the
record keeps `device` and `inode` and gains only WD24's `persistent`
flag, the fact with high bits set asserts the hash instead, the recents
key is unchanged and no Swift line changes; under "the owner records a
one-time reset" the path-keyed migration step is dropped). WD24
(answered 2026-10-21; amended 2026-10-21: FAT32 and exFAT vaults keep
opening. The root's and `.slate`'s identity is read with
`GetFileInformationByHandleEx(FileIdInfo)`, else with
`GetFileInformationByHandle` — the volume serial number and the 64-bit
file index, the read the vault watcher already makes — and an identity
from that fallback is flagged weak (`persistent: false`), because a FAT
file index changes when its entry is renamed or moved. A Windows vault
does not open only when neither read answers: core refuses with a
sentence saying why, and `OpenVaultAsync` refuses a null root identity
on Windows. Rejected: admitting an id-less root by handle and final path
with path-keyed stores. Codex round 11 narrows how this spec applies
the amendment, keeping its choice that FAT32 and exFAT vaults open on a
weak identity: the fallback is taken only on a volume whose file system
`GetVolumeInformationByHandleW` names `FAT`, `FAT32` or `exFAT` — the
file systems that do not answer `FileIdInfo` — and on NTFS, ReFS or any
other file system a `FileIdInfo` failure refuses the open, because ReFS's
64-bit index is not unique and a failure there is not "unsupported"; and
a weak identity authorizes nothing — the host's vault stores reach the
vault through a duplicate of the session's held root handle, never by
comparing identities, since two folders' weak identities can be equal.
The plan's WD24 rows need a dated note saying so (this spec's report).
Design items 0, 1, 4, 6, 7 and 9 apply it).

**Owner's call of 2026-10-26** (plan §3.1, "Review calls", WD7 and WD22) outranks this spec where they differ: WD7 and WD22 are answered as recommended; WB-28 starts once its design review posts the `FsRoot` helper names on #1513, not after 22c merges; WB-29a's design (docs) may run beside it, and WB-29a's core store follows it.

**Delivery record, PR 1 (2026-11-01).** Where it differs from the sections
below, it wins. The design review on #1513 (issuecomment-6553047390)
named the `FsRoot` helpers; the owner answered D1 (an equal persistent
identity is the same root) and D3 (a weak-root reopen accepts the
overwrite) there.

- **Step 0 ran 2026-11-01 (owner's elevated session; doc 24 P-W2).** No
  stop condition: FAT32 and exFAT answer `FileIdInfo` with error 87,
  name themselves `FAT32` and `exFAT`, and give a file index that is
  stable and distinct within the mount, and that changes on a rename.
  The seams fail `FileIdInfo` with 87. D2 needs no answer: no FAT volume
  answers `FileIdInfo` at all.
- **The SQLite path is `C:\|slate-fd\<token>\<leaf>`, not
  `\\.slate-fd\<token>\<leaf>` (item 3).** SQLite takes a path that
  starts with `\\` for a network share and shares one lock handle among a
  process's connections (`winIsUNCPath`, `bUseSharedLockHandle`); racing
  first writes then failed with `SQLITE_PROTOCOL` in 8 runs of 8. The
  new prefix is absolute, so `GetFullPathNameW` keeps it, and `|` is not
  allowed in a Windows file name, so no real path is ever it: without the
  hook kernel32 answers `ERROR_INVALID_NAME`. The race passes 8 of 8. D4
  (the by-path fallback) is not needed.
- **The watch's arm check compares like with like (C9, found in PR 1).**
  `session/watch.rs` compared the watcher's arm identity (on Windows the
  volume serial and file index) with the session's identity; with the
  identity now `FileIdInfo`, every Windows watch would have been refused.
  The session keeps a `watch_anchor`, the held root in the watcher's own
  terms.
- **The root is listed on a file object of its own.** A listing's position
  belongs to its file object, and a duplicated handle shares the held
  one's. `ReOpenFile` answered `ERROR_ACCESS_DENIED` in this PR's attempt;
  `NtCreateFile` with an empty name relative to the held handle
  (`windows_entry::reopen`) makes the new file object, for root listings
  and for `duplicate_root_handle`, so a host listing never shares a
  position with the session's.
- **The mutation probes follow ancestors (`Follow::Never` is
  `symlink_metadata`'s rule).** The mutation's own walk refuses a link
  among the folders in its typed words (#1449, #1452).
- **Table B has Windows bodies only.** Unix declarations with today's
  pathname bodies would have no caller (the provider keeps its own Unix
  code), which `-D warnings` refuses; 22c adds them with its bodies.
- **PR 2 carries open items 1 and 2** (the review's split).
- **The fresh red team's findings, taken in PR 1:**
  - the Trash confirmation's snapshot walks from the held root, as the
    Trash handoff does, so the count the reader confirms is the tree that
    goes, and a vault opened through a junction can Trash (main refused
    it);
  - the interior's replacing rename keeps `std::fs::rename`'s POSIX
    semantics (`FileRenameInformationEx`, with the classic rename on FAT);
  - folders on the way are opened with attribute access only, as a path
    open needs no rights on the folders it passes;
  - a case-only same-entry needs the two spellings to differ only by
    case, so two hard links to one file stay two entries;
  - interior appends map as std's do.
- **Recorded, not changed:**
  - `canonical_path` through a junction ancestor that stays in the vault
    gives the target's spelling (main's Windows rule), not `None` as row
    1014 and mac's I7 have it; a fact pins the kept rule;
  - WD24's refusal reaches any volume that answers no `FileIdInfo` and
    does not name itself FAT: a Samba or NAS share without file ids, a
    WinFsp mount (Cryptomator, rclone) or `\\wsl.localhost` may now refuse
    a vault that opened on main; only loopback Windows SMB was probed.
- **Codex round 1, taken:** the session's path-based readers (bibliography
  and CSL styles, sync detection, the LiveSync config) read through
  `fs_root()`, which on Windows is now the held root's own final path,
  verified against its identity (no reparse point in it, and none of its
  folders can be renamed or removed while the root is held); the spelling
  lookup asks only for attribute access.
- **Codex rounds 2 and 3: the provider's reads open their whole relative
  name in one `NtCreateFile` against the held root**
  (`windows_entry::open_beneath_path`). The system parses the folders on
  the way as for a path, so the traverse-check bypass and the reparse
  points among them behave as for a path open; only the starting point is
  the held handle. A folder-by-folder walk had asked each folder for
  rights a path open never asks (attribute, then `SYNCHRONIZE`, then
  traverse access), and a handle with no access at all cannot parent a
  relative open. Metadata probes ask for `FILE_READ_ATTRIBUTES` alone,
  with no synchronous I/O: less than `std::fs::metadata` asks
  (`CreateFileW` adds `SYNCHRONIZE`). The interior's walks beneath
  `.slate` stay folder by folder, because they refuse links.
- **Codex round 4, taken:** the held root and `.slate` ask what main's
  per-operation root open asked (attribute and list access, no traverse);
  a listing asks list access alone; a configured templates folder parses
  as a Windows path (`\`, repeated and trailing separators); the held
  path, the stored spelling and the in-vault check fall back to the
  volume-GUID final path where the DOS form fails (a volume mounted
  without a drive letter); a followed stat of a reparse point that is no
  link (an AF_UNIX socket) reports the entry itself, as `std` does; the
  stored spelling is strict Unicode again, as main's.
- **Codex round 5, taken:** interior folders on the way and the staging
  folder are opened with attribute access alone (a handle with attribute
  access, or even `SYNCHRONIZE` alone, parents a relative open), and an
  interior listing reopens its folder for list access alone. The session's
  two holds keep a data right, because only a handle with one takes part
  in sharing checks and so blocks another opener's delete: list access,
  then traverse, then the two creation rights (codex round 6). A folder
  that grants none of the four is held with attribute access alone: the
  vault opens, as on main, without the pin. The display spelling keeps a
  volume-GUID path absolute.
- **Codex round 7, taken:** the root hold and the end-of-open spelling
  check open with `NtCreateFile` by the NT name (`\??\…`), asking no
  `SYNCHRONIZE` (`CreateFileW` adds it to every open, so a root that
  denies it refused the vault); the host's duplicate is reopened without
  synchronous I/O; interior deletes use `std`'s disposition (POSIX
  semantics, the read-only attribute ignored, the classic disposition on
  FAT).
- **Codex round 8, taken:** the spelling becomes an NT name only after
  Win32's own normalization (`std::path::absolute`, `GetFullPathNameW`):
  `.` and `..` resolved and a relative spelling made absolute, nothing on
  disk consulted, for the hold and the end-of-open check alike.
- **Followed links keep pathname semantics.** Only the starting point is
  anchored: a junction inside the vault that leads back out through the
  session's own retargetable spelling is followed as a path open follows
  it (codex round 7).
- **Not mirrored:** `std`'s fallback to a directory listing when even an
  attribute open is refused with a sharing violation (system files such
  as a page file), which does not reach a vault.
- **Accepted risks (codex round 1, below the two-coincidence bar):**
  - two mounted FAT clones whose weak identities collide, plus a spelling
    retargeted from one to the other during the open, pass the open's
    re-verification (weak identities compare equal); the session still
    holds and uses the folder it opened;
  - two hard links whose names differ by a Unicode case mapping NTFS does
    not apply (`K` and the Kelvin sign) compare as one entry, so a move of
    one onto the other replaces the second link (no content is lost);
  - the watcher arms its spelling before it reads the identity it compares
    (pre-existing, `vault/watch.rs`): two timed retargets (A→B before the
    backend opens, B→A before it observes) pass the arm check while it
    watches B. A later change binds the watched folder and its identity to
    one handle;
  - a `.slate` that denies listing together with an op-log folder that
    denies attribute reads fails the op-log listing's first open (two
    restricted folders, codex round 6);
  - a root or `.slate` that grants no data right at all is held unpinned;
  - in a case-sensitive folder, hard links named `a.md` and `A.md` compare
    as one entry, so a move of one onto the other replaces the second
    link (codex round 7);
  - a held root whose watch identity cannot be read leaves the watch's arm
    check without an anchor, so that failure plus a retarget could arm a
    watch on another folder (codex round 9).
- **Codex approved at round 9** (head `b1c98a76`).
- **The facts are in `session/tests/held_root_windows.rs`** (and
  `db/anchored_windows.rs`, `session/tests/watch.rs`), named for what they
  prove; the Verification list's names map onto them in PR 1's body.

## Goal

On Windows, as on macOS and Linux since 22a, a filesystem session binds
the root it opened: the vault root and `.slate` are held open for the
session's lifetime, every interior open (`cache.sqlite` and its WAL and
SHM, `structural.lock`, `slate.json`, `.slate/prefs.json`, the op-log,
`.slate/tmp`) is `NtCreateFile` with `RootDirectory` set to a held handle,
the provider's reads resolve beneath the held root, and `root_identity()`
is a real anchor read from the held handle in a form that loses no bits
of NTFS's 128-bit file id. #1475's "different folder now at the path"
column gets its answer: the anchored stores reach the vault through a
duplicate of the root handle the session holds, so a different folder at
the path is never reached, and, where the identity is persistent, the
device-local recents key follows the vault, not its path. A folder on
FAT32 or exFAT, which generally answers no `FileIdInfo`, still opens:
its identity is the volume serial and the 64-bit file index, flagged
weak, which keys nothing durable and authorizes nothing. A folder does
not open when its volume keeps file ids (NTFS, ReFS, or any file system
but FAT, FAT32 and exFAT) and `FileIdInfo` fails, or when neither read
answers on FAT, FAT32 or exFAT, and the reader hears core's reason (WD24,
amended 2026-10-21, as codex round 11 narrows it).

## Open design items (Codex round 12, 2026-10-21)

These findings were recorded, not fixed, by the owner's decision of
2026-10-21: the chunk's design review closes each one before any code,
and where an item contradicts the text below, the item wins until the
design resolves it. (Line numbers into this spec are Codex's, from
before the round-12 notes were added; each passage is also named.)

1. **[high] Pinned graph writes still leak pending settings into another
   vault.** Design item 6 pins `GraphConfigWriter.Enqueue`'s destination
   (its "the graph writer's queued write can outlive the session"
   passage, Codex's `:633-640`), but `GraphConfigWriter.Newest(string
   key)` (`GraphConfigWriter.cs:114-123`) still finds the outstanding
   aggregate by the path key alone, and `GraphPreferencesViewModel`'s
   constructor (`GraphPreferencesViewModel.cs:94-97`) adopts that
   aggregate as its config, writable, before it reads any store. Hold A's
   write, close A, retarget its junction spelling to B and open B: B's
   preferences show A's config, and B's next preference edit writes that
   aggregate through B's anchor; `AQueuedWriteLandsInTheRootItWasEditedIn`
   never opens B's preferences during the overlap, so it cannot see this.
   *Codex recommends:* scope the pending reads and the generation and
   coalescing state to a safely established root, not to path or
   weak-identity equality alone; add overlapping A/B preference instances
   with a held write, a retargeted spelling, aliases and a forced
   weak-identity collision; change `Newest` and the Brief alongside
   `Enqueue`.
   *The design closes it with:* the scope that `Newest`, `Enqueue`'s
   coalescing and the generation counter are keyed by, and their changed
   signatures; how a later session of the same root is recognised as that
   root (Term W6: a reopen during a straggling write reads what the
   straggler will write) for a persistent identity, and what a weak root
   does instead, since a weak identity authorizes nothing; and a
   `GraphConfigWriterTests` fact (or `GraphPreferencesTests`, as the
   design places it) in which A's write is held, A's session closes, the
   junction `T\vault` is retargeted from `T\a` to `T\b`, and B's
   `GraphPreferencesViewModel` is constructed during the hold — B's config
   is the one B's store holds, B's edit lands only in
   `T\b\.slate\graph.json`, and after the release `T\a` holds A's
   aggregate — repeated with an alias spelling of A (the design's answer
   for a same-root reopen, asserted) and with
   `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` and
   `SLATE_TEST_ROOT_FILE_INDEX_COLLIDES` forcing A and B to one weak
   identity (B still never reads A's aggregate). Mutations: key `Newest`
   by the path (→ B shows A's config); key it by the weak identity (→ the
   collision row shows A's config).
2. **[medium] Search recents keep the disposed session after a same-path
   reopen.** Design item 6 has `Search/VaultSearchSource.cs:96` build its
   store with `VaultRootAnchor.Of(_session())` (Codex's `:625-628`), but
   `RecentsStore()` (`Search/VaultSearchSource.cs:86-100`) caches that
   store and rebuilds it only when the root spelling changes (`:93-97`),
   and the source is one app-lifetime instance (`:10`). Closing a vault
   and reopening the same path returns the cached store, whose anchor's
   session is disposed, so its `OpenRoot` refuses (item 6's
   disposed-anchor rule, `AStoreRefusesOnceItsSessionIsDisposed`) and
   search-recents reads and writes stay broken until the path changes;
   WB-9 and WB-10 do not rebuild this source.
   *Codex recommends:* invalidate the cached store when the session's
   identity changes, a same-path reopen included, and clear it when no
   session exists; add same-path reopen and same-spelling replacement
   facts; put the lifecycle change in the Brief.
   *The design closes it with:* what the cache is keyed by (the session
   instance or its anchor, never the spelling) and the clear when
   `_session()` is null; and facts in `SearchRecentsStoreTests` (which
   already builds a `VaultSearchSource`): open V, record a query, close,
   reopen V at the same path — `LoadRecents()` returns the query and a
   second `RecordRecent` lands, with no refusal logged; close V, retarget
   the junction it was opened through to a different folder V′ and open
   it — `LoadRecents()` is V′'s, never V's, and a record lands in V′
   only; with no vault open, `LoadRecents()` is empty and no store is
   cached. Mutation: rebuild the store on a spelling change only (→ the
   same-path reopen row's read and write refuse).

## Current state on main

Verified against the worktree. Two of the plan's lines drifted by one;
the §11 helper names are not what the tree holds (said below).

- **`FsRoot` is Unix-only in body.** `vault/fs_root.rs:536-549` `pub struct
  FsRoot { opened_as, identity: Option<(u64, u64)>, #[cfg(all(any(macos,
  linux), target_pointer_width = "64"))] root: fs::File, cache: CacheDir }`
  (the cfg is `:544-547`). Unix body `:579-651` (`open` `:588`;
  `descriptor` `:611`; `verified_current_path` `:619`; `read_root_file`
  `:625`; `is_dir` `:642`). The other-platform body `:653-684`: `open`
  records the pathname with `observed_identity(root)` and
  `CacheDir::by_path`; `verified_current_path` is `None`. `ANCHORED`
  (`:49`) is the cfg as a const. The interior abstraction is `trait
  InteriorDir` (`:142-169`) with impls for `Path` (`:191`), `PathBuf`
  (`:242`) and `CacheDir` (`:422`, Unix). The Unix syscall helpers are
  `mod sys` (`:749-1018`): `open_at` `:809`, `ensure_dir_at` `:878`,
  `open_or_make_slate` `:887`, `rename_at` `:898`, `unlink_at` `:920`,
  `stat_at` `:930`, `sync_dir` `:949`, `read_dir_names` `:960`,
  `identity` `:803`. **Discrepancy with plan §11:** there is no
  `list_dir_at`; the listing helper is `read_dir_names`, and the portable
  surface is `InteriorDir`.
- **The SQLite hook is Unix-only.** `db/anchored.rs:1-30` (doc), `:55-57`
  (VFS names), `register` `:112`, `database_path` `:127`, `install`
  `:400`, `raw_open_at` use `:269-275`. The session opens through
  `interior.sqlite_path("cache.sqlite")` (`session.rs:2639-2641`;
  `CacheDir::sqlite_path` `:374` Unix, `:411` by-path elsewhere) and
  `open_worker_connection(&cache_dir)` (`:1600`).
- **The open sequence and the seam.** `session.rs:3206` `let held_root =
  Arc::new(FsRoot::open(&root)?);` then `:3207-3209` the identity, then
  `:3210-3211` `fire_open_swap_seam(OpenSwapPoint::RootAnchored)` — the
  seam's first point fires **after** the hold. `open_in` at `:2624`;
  structural lock then `db::open_database` at `:2636-2641`.
- **Identity.** `session.rs:1092-1095` `pub struct VaultRootIdentity {
  device: u64, inode: u64 }`; the Windows `observe_root_identity` returns
  `None` (`:1108-1115`). Fields `:1442`, `:1450`; `root_identity()`
  `:3141`, `root_display_path()` `:3152`. FFI: `slate-uniffi/src/lib.rs:
  623-626` record, `:772-777`, `:786-790`. Windows already reads the
  lossless id: `windows_entry::identity` (`:612-631`), used by `capture`
  (`:633`) for structural undo (contract 31 FR-10, `:540-556`);
  `trash_snapshot/windows.rs:235-262` `stamp` reads the same. Both read
  **only** `GetFileInformationByHandleEx(FileIdInfo)`, with no fallback.
- **FAT32 and exFAT generally answer no `FileIdInfo`** (codex round 8,
  citing microsoft/STL PR 6187, which restores a
  `GetFileInformationByHandle` fallback for `std::filesystem` for this
  reason). The repository already says so: `SidebarWriteJournal.cs:73-75`
  ("a filesystem that does not answer `FileIdInfo`, such as FAT32: then
  nothing is journaled"), and `:58` notes that the 64-bit
  `nFileIndex` is not unique on ReFS — Win32's
  `BY_HANDLE_FILE_INFORMATION` remarks say the same (ReFS has 128-bit
  ids; read them through `FileIdInfo`, because the 64-bit identifier is
  not guaranteed unique there) — so the file index is never an identity
  on ReFS, and a `FileIdInfo` failure there is not evidence of a volume
  that lacks file ids. No code on main reads a volume's file-system name
  (`GetVolumeInformation*` appears nowhere in `crates/` or
  `apps/slate-windows/src`). The fallback read exists in
  core: the watcher's Windows `observe_identity`
  (`vault/watch.rs:279-307`, the call at `:298`; design §4.4) reads
  `GetFileInformationByHandle` into `RootIdentity { volume:
  dwVolumeSerialNumber, file: nFileIndexHigh << 32 | nFileIndexLow }`
  (`:67-74`). Win32's `BY_HANDLE_FILE_INFORMATION` remarks: a FAT file
  id comes from the containing directory's first cluster and the entry's
  byte offset in it, so renaming (to a longer name) or moving the entry
  can change it — and, by the same derivation, a freed slot can give a
  later entry the same id. Two entries that exist at once in one FAT
  volume sit in different places and so have different ids, but the
  identity is the pair (32-bit `dwVolumeSerialNumber`, index), and the
  serial is written in the volume's boot sector: two FAT32 or exFAT
  volumes imaged from one another (a cloned USB stick) share the serial
  and every entry's place, so two folders on them, mounted at once, have
  equal weak identities. Equality of weak identities is therefore not
  sameness, even within a session.
  Measured on this machine's NTFS (the step-0 probe's control row):
  `FILE_ID_INFO.VolumeSerialNumber` is the 64-bit serial
  (`8e885d96885d7da1`), `dwVolumeSerialNumber` its low 32 bits
  (`885d7da1`), and the 128-bit id's low half equals the file index.
- **Hosts' consumers of the record.** Windows: `FileRecentsStore.cs:22-37`
  (the parameter `:24`, key `:27-30` reading `identity.Device` and
  `identity.Inode` at `:30`, hash `:31`, file `:32-36`); `Load` runs
  `MigrateLegacy` only while the destination is absent (`:41-44`), and
  `Add` is `Load` then `Save` (`:49-58`); `MigrateLegacy`
  (`:69-105`), `Read` (`:107-125`, a read error becomes an empty list)
  and `Save` (`:127-148`, `void`; it logs and swallows `IOException` and
  `UnauthorizedAccessException`); constructed at `FilesSidebarViewModel.cs:557` and
  `QuickSwitcherViewModel.cs:68`. (`SidebarWriteJournal.cs:64` keys the
  sidebar journal by its own host-read `SidebarJournalIdentity`, not the
  FFI record, and is not changed.) Mac: no Swift code constructs the FFI
  record (`git grep -n "VaultRootIdentity(" apps/slate-mac` finds only
  the host type `SidebarVaultRootIdentity(`), and two Swift sites read its
  fields: production `AppState.swift:10670-10671` maps
  `session.rootIdentity()` into the host's `SidebarVaultRootIdentity`
  (`$0.device`, `$0.inode`), and the test
  `SidebarOrganizationDispatchTests.swift:2475-2479`
  (`testOpenBindsSessionAndSidebarToOneObservedRoot`) reads
  `anchor.device` and `anchor.inode` of `session.rootIdentity()`. Both
  change with the record (`git grep -n "rootIdentity()" apps/slate-mac`
  lists exactly these two).
- **`AnchoredVaultStore.cs`** opens root and `.slate` by path per
  operation without `FILE_SHARE_DELETE` (`Open(string vaultRoot, bool
  createDirectory, …)` `:58-120`, `OpenRequiredDirectory` `:245`,
  `Directory.CreateDirectory` by path `:72-75`), opens each child by path
  (`TryOpenChild` `:292-320`, `Path.Combine(_directoryPath, fileName)`),
  and compares identity as text (`VerifyDirectoryIdentity` `:333-342`,
  `EnsureRegularChild` `:322-331`, `FinalPath` `:434`). Because the root
  is opened with `FILE_FLAG_OPEN_REPARSE_POINT` and `EnsureAttributes`
  refuses a reparse point (`:249-281`, `:344-363`), a vault opened through
  a junction spelling has no working store on main. Its callers build it
  from a path string: `WorkspacePersistence(string vaultRoot, …)`
  (`WorkspacePersistence.cs:92`, built at `VaultLifecycleViewModel.cs:1182`
  and `WorkspaceViewModel.cs:2119`), `SearchRecentsStore(string)`
  (`Search/SearchRecentsStore.cs:67`, built at `Search/VaultSearchSource.cs:96`,
  which also holds `Func<VaultSession?>` `:36`), `SidebarSettingsStore(string,
  …)` (`SidebarSettingsStore.cs:45`, built at `FilesSidebarViewModel.cs:556`),
  `FileRecentsStore`'s in-vault read (`FileRecentsStore.cs:75`), and — once
  WB-12 has moved it onto `AnchoredVaultStore` — `GraphConfigStore(string)`
  (`Graph/GraphConfigStore.cs:40`, built at
  `Graph/GraphPreferencesViewModel.cs:101` and by the process-wide writer's
  `StoreFor`, `Graph/GraphConfigWriter.cs:65`, whose queued write,
  `:132`, can run after the vault closed: nothing drains it at close).
  Every construction site has the session in scope, and
  `VaultLifecycleViewModel`'s close disposes the workspace before the
  session (`:1095-1124`). The test project builds these stores from
  temp-folder paths at about 70 sites (`new WorkspacePersistence(` 21,
  `new SidebarSettingsStore(` 11, `new SearchRecentsStore(` 12,
  `new GraphConfigStore(` 24, `AnchoredVaultStore.Open(` 2) and drives
  `GraphConfigWriter.Enqueue`/`new GraphPreferencesViewModel(` at 80.
- **The provider on Windows** pins per operation by path:
  `windows_entry::PinnedRoot::open(root: &Path)` (`:378-398`),
  `trash_handoff::pinned_path` (`:298`); reads are pathname-based
  (#1513).
- **The facts to lift.** `session/tests/descriptor_interior.rs`
  `#![cfg(unix)]` (`:16`): `:75`
  `the_interior_binds_the_anchored_root_through_an_a_b_a_swap`, `:142`,
  `:181`, `:229` `sqlite_wal_two_connections_through_the_anchored_path`,
  `:293` `a_second_process_on_the_textual_path_shares_the_anchored_cache`,
  `:357` `the_structural_lock_registry_is_keyed_by_identity_not_the_canonical_path`,
  `:424` `root_display_path_is_gated_by_the_anchor`, `:482`
  `a_symlinked_interior_component_is_refused`, `:618`
  `after_a_live_rename_no_interior_writer_recreates_the_old_path`, `:670`
  `canonical_path_is_computed_under_the_root_descriptor` (itself
  `#[cfg(target_os = "macos")]`, `:668`). **None of them can run on
  Windows as written**, and lifting the module's `cfg` would not make them:
  every one but `:293` renames a held root, `.slate` or a decoy over it
  while the session lives (`:87-92`, `:154-155`, `:198-203`, `:264`,
  `:368-398`, `:440`, `:460`, `:628`, `:687-688`) — which this chunk's
  Windows hold (no `FILE_SHARE_DELETE`) refuses by design — and they use
  `std::os::unix` (`MetadataExt` `:425`, `symlink` `:483`, `:683-684`) or
  the Unix synthetic spelling `/.slate-fd/` (`:239`). The same holds in
  `fs_root.rs`'s tests (`:1020-1140`): only
  `both_forms_open_create_rename_list_and_remove_the_same_files` (`:1028`)
  is uncfg'd (it renames a file *inside* the directory, not the held
  directory); `a_held_cache_directory_follows_its_folder_and_never_recreates_the_old_path`
  renames the held cache directory (`:1071-1081`),
  `a_held_cache_directory_never_follows_a_link` uses Unix `symlink` and
  `libc` errnos, and `racing_creates_beneath_a_held_directory_all_open_the_one_file`
  pins a Darwin `openat` race with `MetadataExt`; all three are
  `cfg(any(macos, linux))`. A Windows vault root reached through a
  junction already works, because the root's own reparse point is followed
  at open (`windows_entry.rs:383-385`, `trash_handoff_tests.rs:557-572`),
  and the junction itself stays deletable while the root is held (that
  fact removes it at `:571`); `vault::test_support::junction(path, target)`
  (`test_support.rs:58-86`, `cfg(all(test, windows))`) makes one. The
  re-arm switch `session/watch.rs:108` `WATCH_REARM_ENABLED = false` (22c).
- **The host's open sequence:** `VaultLifecycleViewModel.cs` `OpenRecentAsync`
  `:724`, the workspace construction `:1184`, `WorkspaceReady` `:1280`.
  `OpenVaultAsync` (`:436`) calls `VaultSession.OpenFilesystem(root)`
  (`:483`), then `AddRecentVault` and `VaultOpened` (`:517-519`). Every
  failed open lands in `catch (VaultException exception)` (`:556-565`),
  which speaks `$"Could not open vault: {exception.Message}"` through
  `ReportTerminalStatus` (`:1870-1875`, a `HostComposed` event at `High`),
  closes the session and raises `ReturnedToWelcome`. A `VaultException`'s
  `Message` is the binding's field dump: the generated `VaultException.Io`
  passes `"@message=" + message` to its base. Core's text for a caught
  error is `SlateUniffiMethods.VaultErrorDetail(exception)` (uniffi
  `lib.rs:11329`, rendered by `a11y::vault_error_detail`, `a11y.rs:3401`,
  which gives `Io`'s message verbatim), as `WorkspaceViewModel.cs:1135`
  already uses.
- **The Unix open already refuses a root it cannot identify.**
  `FsRoot::open` (`fs_root.rs:588-605`) reads `sys::identity(&held)` right
  after the hold and before `open_or_make_slate`, and fails the open
  with `VaultError::Io` (the OS text) when `fstat` fails — so no `.slate`
  is made in such a folder.
- **Release-reachable fault seams exist.** `SLATE_TEST_SWEEP_AFTER_INTENT`
  (`session.rs:4371`) and `SLATE_TEST_COMMIT_AFTER_INTENT` (`:4386`): an
  environment variable whose value, contained in a path, triggers the
  fault in any build, so a host test or a launched app can provoke it.

## Reference

- Design `22_descriptor_relative_session_design.md` §4.1 (`:306-385`;
  Windows at `:350-360`), §5.2 (`:639-677`), §5.3 (`:678-711`), §6
  (`:737-789`: `#[cfg(windows)] root: OwnedHandle, slate: OwnedHandle`),
  §7 row 22W (`:796`, facts `:815`), Q6 (`:1069`).
- #1475's table and "Fix" section.
- microsoft/STL PR 6187 (FAT32/exFAT and `FileIdInfo`; the
  `GetFileInformationByHandle` fallback) and Win32's
  `BY_HANDLE_FILE_INFORMATION` remarks (how FAT derives a file id).
- WB-27's spec: its volume procedure (the FAT32 and exFAT VHDs, `$fatRoot`,
  `$exfatRoot`) and the §5 Windows block it appends to
  `24_structural_identity_capability_matrix.md`.
- Plan 44 WD22's and WD24's text (§3, WD24 with its 2026-10-21
  amendment). Mac twin of the admission:
  `AppState.swift:10073-10090` (`VaultOpenIdentityRaceError`, design §5.3).

## Design section to review before code

Record each answer in this file's header on adoption.

0. **Step 0, before any code: probe both identity reads on WB-27's
   FAT32 and exFAT VHDs (WD24 amended).** In the elevated session where
   WB-27's volume procedure set `$fatRoot` and `$exfatRoot`, the probe in
   Verification (a PowerShell `Add-Type` of the two P/Invoke reads, with
   an NTFS temp folder as the control row) opens a folder with
   `FILE_FLAG_BACKUP_SEMANTICS` and records, per volume: the file-system
   name `GetVolumeInformationByHandleW` reports for the handle (the name
   item 6's fallback gate reads); whether
   `GetFileInformationByHandleEx(FileIdInfo)` answers (its value, or its
   Win32 error number); whether `GetFileInformationByHandle` answers (the
   volume serial and file index); the index read again (stable within a
   mount); a sibling folder's index (distinct); and both reads after
   renaming the folder to a longer name (recorded, not asserted). It
   throws when the NTFS control answers no `FileIdInfo` (the probe is
   broken), when a volume answers neither read, when a volume that needs
   the fallback reports a file-system name other than `FAT`, `FAT32` or
   `exFAT` (item 6's gate would refuse its vaults), or when a volume that
   needs the fallback gives an index that is not stable and distinct
   within the mount. Any of the last three stops the chunk for an owner
   decision: WD24's refusal would then stop that volume's vaults from
   opening, or the weak identity could not serve as a lock key and a
   recents-key decision even within a session.
   The table goes into PR 1's body and, dated, into the §5 Windows block
   WB-27 appended to `24_structural_identity_capability_matrix.md`. What
   the result changes: where FAT32 or exFAT answers no `FileIdInfo`
   (expected), the fallback is the read their vaults open by, and the
   gated fact proves it on the VHD. Where one does answer `FileIdInfo`,
   the fallback stays as a defensive path for a FAT, FAT32 or exFAT volume that does
   not, proven by the seam fact
   `a_root_whose_file_id_info_is_unsupported_opens_with_a_weak_file_index_identity`
   alone, and the adoption note in this header says so. The gated fact
   computes its expectation from the volume and passes either way. If
   the probe records an error other than `ERROR_INVALID_PARAMETER` (87)
   for `FileIdInfo`, the three seams of item 6 that fail `FileIdInfo` fail with the recorded code.
1. **The record's shape (WD22; the flag is WD24 amended).**
   `VaultRootIdentity { volume: u64, id_high: u64, id_low: u64,
   persistent: bool }` (new fields, core and uniffi). `persistent` is
   `true` when the id is the file system's own file id: Windows
   `FileIdInfo` (`volume` = `FILE_ID_INFO.VolumeSerialNumber`, the
   128-bit id split into `id_high`/`id_low`) or Unix `fstat` (`volume =
   st_dev`, `id_high = 0`, `id_low = st_ino`, as today; mac weak volumes
   are plan 43 PR 38/41's). It is `false` when the volume is FAT, FAT32
   or exFAT and Windows answered only the fallback (item 6's gate):
   `volume` = `dwVolumeSerialNumber` zero-extended, `id_high = 0`,
   `id_low = nFileIndexHigh << 32 | nFileIndexLow` (the watcher's pair).
   Equality and hashing cover all four fields, so a weak identity never
   equals a persistent one — not even on NTFS, where the two reads share
   the low 64 bits but not the serial. Two weak identities that are equal
   need not name one folder (two cloned FAT volumes mounted at once), so
   no access decision anywhere in this chunk compares identities: what
   reaches the vault is a handle the session holds (items 6 and 9). `u64`s because both bindings
   carry them without a byte-array convention, and
   `windows_entry::identity`'s text form derives losslessly from a
   persistent one. `FsRoot::identity()` returns
   `Option<VaultRootIdentity>` (new return type; the Unix body wraps
   `sys::identity`'s `(dev, ino)` as `{ volume: dev, id_high: 0, id_low:
   ino, persistent: true }`). The host reads that change:
   `FileRecentsStore.cs:30` (item 7);
   `AppState.swift:10670-10671` → `SidebarVaultRootIdentity(device:
   $0.volume, inode: $0.idLow)`, lossless on Unix, where core always sets
   `id_high = 0` and `persistent = true`; and the test
   `SidebarOrganizationDispatchTests.swift:2478-2479` → `device:
   anchor.volume, inode: anchor.idLow`. No Swift line constructs the
   record. Under WD22's other answer the record is `{ device, inode,
   persistent }` and neither Swift site changes.
2. **Helper names with 22c.** Adopt the tree's: a `#[cfg(windows)] mod
   sys` with the same names as the Unix one (`open_at`, `stat_at`,
   `read_dir_names`, `rename_at`, `unlink_at`, `ensure_dir_at`,
   `identity`, `open_or_make_slate`) over handles (`NtCreateFile` with
   `RootDirectory`; `GetFileInformationByHandleEx`; `NtQueryDirectoryFile`);
   the Windows `identity` returns `io::Result<VaultRootIdentity>` and is
   item 6's gated two-read identity, over three new private helpers
   `file_id_info`, `file_system_name` and `file_index` and the pure
   decision `identity_from_reads` (item 6); `InteriorDir for CacheDir` compiles on Windows; 22c's
   provider-read helpers get Windows bodies under their names. No
   `list_dir_at`.
3. **SQLite.** First commit is a spike: verify in the bundled 3.53
   `os_win.c` that `winSetSystemCall` and `aSyscall[]` carry `CreateFileW`,
   `GetFileAttributesW`, `GetFileAttributesExW`, `DeleteFileW`,
   `CreateFileMappingW`, then give `db/anchored.rs` a `#[cfg(windows)]`
   body overriding those, mapping `\\.slate-fd\<token>\<leaf>` to
   `NtCreateFile` relative to the registered `.slate` handle, with
   `-wal`, `-shm` and `-journal` by name under the same handle. If the
   hook proves unworkable, the fallback (open `cache.sqlite` by the held
   `.slate` handle's final path re-verified by identity immediately before
   `open_database`) is the design's rejected shape (`:378-383`) and ships
   only on a recorded owner approval.
4. **The structural-lock registry key:** the held `.slate`'s whole
   `VaultRootIdentity` (volume, id and `persistent`). Two spellings of one
   vault read the same entry and share a slot. A weak `.slate` key is
   held by a session that holds `.slate` without `FILE_SHARE_DELETE`, so
   its entry is neither renamed nor freed while the key exists — but it
   can still **collide** with another vault's: weak identities of two
   folders can be equal (two cloned FAT volumes mounted at once share the
   serial and every entry's place; Current state), and nothing makes two
   live directories' indexes distinct across volumes. (A ReFS `.slate`
   never has a weak key: item 6 refuses a `FileIdInfo` failure there.)
   A collision is harmless here, because the key only orders turns and
   grants no access: two sessions whose keys collide take turns at the
   in-process gate (`ProcessStructuralLocks`, `session.rs:109-121`,
   acquired first in the lock order, `:124-127`, so a waiter holds nothing
   of its own session), and each still opens and locks `structural.lock`
   beneath its own held `.slate` (`StructuralLockKey::open_file`'s
   identity arm, `:86-91`, `cache.open_file`), so neither session reaches
   the other's vault. A key that collided would cost a wait, never a
   write into the wrong vault; the colliding-index fact pins it.
5. **Why #940's race cannot be driven on Windows, and what the facts prove
   instead.** The seam fires `RootAnchored` after `FsRoot::open`
   (`:3206-3211`); with the root held without `FILE_SHARE_DELETE`, a
   rename of the root or of any ancestor is refused by Windows for the
   session's lifetime, so an A→B→A swap after the anchor is impossible by
   construction, and a swap before it is an ordinary open of B. The
   Windows facts therefore split into two families: **handle-pinning**
   (a rename of the held root or an ancestor fails with a sharing
   violation while the session lives; after close the rename succeeds and
   nothing is recreated at the old path — the twin of `:75`, `:142` and
   `:181`, whose swaps it makes impossible) and
   **interior-resolution** (the interior reaches its files through the
   held handles, proven without renaming anything the session holds and,
   for every interior file but the cache, without the SQLite hook):
   - *the retargeted spelling* — the Windows stand-in for every Unix live
     rename. The fact opens the vault through a junction `outer\vault` →
     `outer\A` (the root's own reparse point is followed at open, as
     `PinnedRoot::open` does), then, with the session live, removes the
     junction and recreates it pointing at a decoy vault `outer\B` that
     holds its own `.slate`, `slate.json` and notes. The junction is not
     held (only `A` is), so this is the one way a different folder can
     appear at the session's spelling on Windows. Every interior writer
     and reader must then still reach `A`: `structural.lock`,
     `.slate\prefs.json`, the op-log, `.slate\tmp` staging, a worker
     connection's `cache.sqlite` with its `-wal` and `-shm`, `root_display_path()`,
     `canonical_path`, and the provider's reads — while `B`'s tree stays
     byte-identical. A by-path implementation writes into `B`, so these
     facts discriminate; the non-SQLite ones go through `InteriorDir for
     CacheDir`'s Windows `sys` body, not the hook;
   - *the hook's own evidence*: `sqlite3_db_filename` of the session's
     connection is the synthetic `\\.slate-fd\<token>\cache.sqlite` path;
     a `CreateFileW` of that literal string fails with
     `ERROR_PATH_NOT_FOUND` while the connection reads and writes; with
     the hook's Windows install skipped through a test seam
     (`anchored::skip_install_for_tests()`, new), `open_database_anchored`
     fails rather than opening by path;
   - *the pre-hold window*: a write through the hook lands in the held
     `.slate` even when the pathname `<opened_as>\.slate` has been
     replaced by a junction to elsewhere before the open (the seam's
     `RootAnchored` point can swap `.slate`'s *pathname* on Windows only
     before `open_or_make_slate` holds it — a new `OpenSwapPoint::
     BeforeSlateHeld` (new) fires between the root hold and the `.slate`
     open, which is exactly the window the design's §3.6(b) names).

   Every 22a fact that renames a held root, a held `.slate` or a held
   cache directory, or uses `std::os::unix`, stays `cfg(unix)` (the
   `descriptor_interior.rs` module keeps its `#![cfg(unix)]`, and the
   three `fs_root.rs` facts keep their `cfg(any(macos, linux))`); none
   is claimed for Windows. Their Windows obligations are carried by
   named twins in `descriptor_interior_windows.rs` and `fs_root.rs`
   (the Tests table maps each one).
6. **The identity read, and a root without a readable identity (WD24,
   answered 2026-10-21, amended 2026-10-21: FAT32 and exFAT vaults keep
   opening; codex round 11 narrows the fallback to the file systems that
   do not answer `FileIdInfo`).**
   - *The read.* The Windows `sys::identity(file: &fs::File) ->
     io::Result<VaultRootIdentity>` calls `sys::file_id_info(file)` (new:
     `GetFileInformationByHandleEx(FileIdInfo)` → `persistent: true`).
     When that fails it reads `sys::file_system_name(file)` (new:
     `GetVolumeInformationByHandleW`'s file-system name for the handle's
     volume) and takes the fallback **only when that name is `FAT`,
     `FAT32` or `exFAT`** (compared ASCII case-insensitively): then it
     calls `sys::file_index(file)` (new: `GetFileInformationByHandle` →
     `persistent: false`, the watcher's pair, `watch.rs:298-306`) and logs
     the `FileIdInfo` error at debug level. On any other name — NTFS,
     ReFS, or a name Slate does not know, such as a share's server may
     report — or when the name cannot be read, `identity` fails with the
     `FileIdInfo` error and the file index is never read: a `FileIdInfo`
     failure on a file system that keeps file ids is a fault, not
     "unsupported", and ReFS's 64-bit index is not unique
     (`SidebarWriteJournal.cs:58`, the `BY_HANDLE_FILE_INFORMATION`
     remarks), so it must never stand in for the id. **This is a refusal,
     not a retry**: core makes one attempt, the open fails with the
     sentence below, and the reader can open the vault again; a retry
     loop inside the open would only hide a fault on the very read the
     session is about to rely on. On FAT, FAT32 or exFAT, `identity` fails
     only when the fallback fails too, with the fallback's error. The
     decision is one pure function so a fact can drive every row of it:
     `fn identity_from_reads(file_id_info: io::Result<VaultRootIdentity>,
     file_system_name: impl FnOnce() -> io::Result<String>, file_index:
     impl FnOnce() -> io::Result<VaultRootIdentity>) ->
     io::Result<VaultRootIdentity>` (new, `pub(super)` in the Windows `sys`, so `fs_root.rs`'s tests reach it;
     `sys::identity` is it applied to the three reads). Not changed:
     structural identity (`windows_entry::identity` for undo's `capture`
     and `rename`) and the Trash stamp stay `FileIdInfo`-only and fail
     closed on such volumes, as on main — an index that changes on rename
     cannot check a rename.
   - *Core refuses first.* The Windows `FsRoot::open` calls
     `sys::identity` on the held root right after the hold and before
     `open_or_make_slate`, as the Unix body does with `fstat`
     (`fs_root.rs:588-605`). If it fails — a `FileIdInfo` failure on a
     file system that keeps ids, an unreadable file-system name after a
     `FileIdInfo` failure, or both reads failing on FAT, FAT32 or exFAT —
     the open fails with `VaultError::Io(io::Error::new(error.kind(),
     a11y::VAULT_ROOT_IDENTITY_UNAVAILABLE))`. The OS error is logged at
     debug level and never spoken. The held root closes with the error,
     and no `.slate` is made in a folder Slate cannot anchor. The held
     `.slate`'s identity (item 4's lock key) is read the same way and
     refuses with the same sentence in the same cases. Core's copy, in
     `a11y.rs` (new): `pub const VAULT_ROOT_IDENTITY_UNAVAILABLE: &str =
     "This folder's file system gives Slate no file identity for it, so
     Slate cannot keep the vault anchored to it.";`. An open Windows
     session therefore always has a root identity, persistent or weak,
     and a weak one only on FAT, FAT32 or exFAT — the invariant WB-30's
     identity-mode rule rests on (its `Strong` mode needs NTFS or ReFS
     **and** `persistent`). A consequence the PR body states: a share
     whose server reports NTFS or ReFS but answers no `FileIdInfo` (an
     older SMB server) now refuses to open rather than opening weak, as
     it would have under the amendment's "any failure" wording; such a
     share cannot be told from a real NTFS volume with a failing read.
   - *Why a `VaultError` detail, not a new `A11yEvent`.* The refusal has
     to happen in core, before `.slate` exists and before the lock
     registry needs `.slate`'s id. So it reaches the host as a failed
     `OpenFilesystem`, through the host's one open-failure path. An event
     of its own would need a typed discriminator — a new `VaultError`
     variant on both bindings — for the host to choose it.
     `vault_error_detail` already carries `Io`'s message verbatim across
     the FFI. So the refusal needs no `corpus.json` row, no census
     mirror, no designations or trigger-ledger row and no mac edit of
     its own (the record's two Swift reads change with item 1): APFS and
     HFS+ always answer `fstat`, and the Unix body already refuses its
     failure. The spoken prefix "Could not open vault:" is the host's
     existing text, unchanged.
   - *Four seams any build can trip.* All follow the `SLATE_TEST_*`
     precedent (`session.rs:4371`) and are consulted by the Windows
     `FsRoot::open` — the one caller that knows the opened path — for the
     root's and `.slate`'s reads, when the opened root's path contains the
     variable's value; each fakes only what `sys::identity` reads, so no
     other code (WB-30's identity mode, which reads the volume's real
     file-system name, included) sees a faked value.
     `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` (new) presents the root as
     a FAT32 volume does: the file-system name reads `FAT32` and the
     `FileIdInfo` read fails with `ERROR_INVALID_PARAMETER`, so the
     fallback answers (the real file index) and the session opens with
     `persistent: false`. `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE` (new) is
     the same FAT32 profile with the file-index read failing too
     (`ERROR_INVALID_PARAMETER`), as a volume that answers neither would,
     so the open refuses. `SLATE_TEST_ROOT_FILE_ID_INFO_FAILS` (new) makes
     only the `FileIdInfo` read fail with `ERROR_INVALID_PARAMETER` and
     leaves the real file-system name (NTFS on the lane): the transient
     failure on a volume that keeps ids, so the open refuses without
     reading the file index — and since the error code is the one FAT32
     gives, a gate that keyed the fallback on the code rather than the
     name would admit it. `SLATE_TEST_ROOT_FILE_INDEX_COLLIDES` (new)
     makes the file-index read return the volume's real serial with the
     fixed index `0x5EED_0000_5EED_0001`, so every root and `.slate`
     beneath a folder whose path contains the value has the same weak
     identity as every other — the synthetic collision (two cloned FAT
     volumes) the colliding-index facts need; with it and
     `…_FILE_ID_INFO_UNSUPPORTED` both set to one folder, two vaults in it
     open with equal weak identities. They serve the core facts, the host
     facts and the NVDA spot check.
   - *The host speaks core's text.* The open-failure catch
     (`VaultLifecycleViewModel.cs:556-565`) reports `$"Could not open
     vault: {SlateUniffiMethods.VaultErrorDetail(exception)}"`, not
     `exception.Message` (the binding's "@message=…" dump). The refusal is
     heard as "Could not open vault: This folder's file system gives Slate
     no file identity for it, so Slate cannot keep the vault anchored to
     it." Every other failed open now speaks core's detail too.
   - *`OpenVaultAsync` refuses a null identity, fail-closed behind core.*
     Right after `OpenFilesystem` returns, and before `AddRecentVault` and
     `VaultOpened`, it reads the identity through a new internal seam,
     `Func<VaultSession, VaultRootIdentity?> RootIdentityReader` (default
     `session => session.RootIdentity()`). On Windows a null identity
     disposes the session and reports `$"Could not open vault:
     {SlateUniffiMethods.VaultRootIdentityUnavailableDetail()}"` through
     the same terminal path. `vault_root_identity_unavailable_detail() ->
     String` is a new additive `#[uniffi::export]` returning core's
     constant, so the host never carries the copy. Either refusal leaves
     no `VaultOpened`, no recents entry and `IsVaultOpen` false, and
     raises `ReturnedToWelcome`, so the keys return to the welcome view
     (WD4). A weak identity is not null: its vault opens as any other.
   - *The stores reach the vault through the session's held root, never
     by comparing an identity.* Round 10 had `AnchoredVaultStore.Open`
     reopen the vault by its spelling and admit it when the identity read
     there (the file index, for a weak root) equalled
     `session.RootIdentity()`. That admits the wrong vault whenever two
     folders' identities are equal — two cloned FAT volumes, or a ReFS
     index — and a retargeted junction can present exactly such a folder
     at the spelling. So no identity, persistent or weak, and no path
     authorizes a store; the capability is the root handle the session
     already holds:
     - **Core** (PR 1): `VaultSession::duplicate_root_handle(&self) ->
       Result<std::os::windows::io::OwnedHandle, VaultError>` (new,
       `#[cfg(windows)]`) returns `FsRoot::duplicate_root(&self) ->
       io::Result<OwnedHandle>` (new: `OwnedHandle::try_clone` of the held
       root — `DuplicateHandle` with `DUPLICATE_SAME_ACCESS`, not
       inheritable — so it names the held directory object itself, with
       the hold's sharing, whatever any spelling names now); a session no
       filesystem root backs returns `VaultError::Unsupported`. The uniffi
       export (new, additive, on `VaultSession`):
       `duplicate_root_handle(&self) -> Result<u64, VaultError>` — on
       Windows the duplicate's raw value, ownership passing to the caller;
       elsewhere `Err(VaultError::Unsupported { feature:
       "duplicate_root_handle" })` (Swift never calls it).
     - **Host** (PR 2): `VaultRootAnchor` (new, `internal sealed class :
       IDisposable`, `VaultRootAnchor.cs`): `internal static
       VaultRootAnchor Of(VaultSession session)` — each `OpenRoot()` asks
       core for a fresh duplicate and wraps it as `new
       SafeFileHandle((nint)value, ownsHandle: true)`; a `VaultException`
       or the binding's `ObjectDisposedException` (a disposed session)
       becomes `IOException`, so a store whose session has gone refuses;
       `internal SafeFileHandle OpenRoot()` (the caller owns the handle);
       `internal VaultRootAnchor Pin()` — a new anchor that owns one
       duplicate taken now (an `IOException` if none can be) and hands out
       duplicates of it (`DuplicateHandle`, kernel32) until disposed, for
       work queued while the vault is open that may run after it closes;
       and `internal static VaultRootAnchor ByPathForTests(string
       vaultRoot)` — main's by-path open of the spelling, which throws
       `IOException("This store was given no vault session to write
       through.")` unless `internal static bool ByPathAllowed` (new,
       `false`) is set, which only the test assembly does, in a
       `[ModuleInitializer]` (new, `tests/SlateWindows.Tests/TestRootAnchors.cs`).
       So the ~70 test sites that build stores from temp folders keep
       working, and a production site that forgot its anchor refuses
       instead of following the spelling.
     - `AnchoredVaultStore.Open(VaultRootAnchor root, bool
       createDirectory, Action? afterDirectoryAnchored = null, Action?
       beforeRename = null, Action? afterRename = null)` (changed: the
       anchor replaces the path) takes `root.OpenRoot()` as its vault
       handle and opens `.slate` relative to it — `NtCreateFile` with
       `RootDirectory` = that handle, the name `.slate`, `FILE_OPEN` (or
       `FILE_OPEN_IF` with `createDirectory`), `FILE_DIRECTORY_FILE |
       FILE_OPEN_REPARSE_POINT`, shared for reading and writing — and every
       child relative to the `.slate` handle the same way (`TryOpenChild`
       loses its `Path.Combine`). `EnsureAttributes` still refuses a
       reparse point at each, so a junction planted as `.slate` or as a
       child is refused; `RenameAnchored` is already handle-relative. The
       final-path text checks (`VerifyDirectoryIdentity`, the comparison
       in `EnsureRegularChild`, `FinalPath`) are removed: nothing is
       resolved by path any more, so they guard nothing. A vault opened
       through a junction spelling now has working stores (main refuses
       them, Current state).
     - The five stores take the anchor as a new optional last parameter
       `VaultRootAnchor? root = null` (`null` → `ByPathForTests(vaultRoot)`):
       `WorkspacePersistence`, `SearchRecentsStore`, `SidebarSettingsStore`,
       `GraphConfigStore` (its `Read(VaultRootAnchor? root = null)` and
       `Write(GraphConfig config, VaultRootAnchor? root = null)`), and
       `FileRecentsStore` for its in-vault step (item 7). Every
       production construction site passes `VaultRootAnchor.Of(session)`:
       `FilesSidebarViewModel.cs:556-557`, `QuickSwitcherViewModel.cs:68`,
       `Search/VaultSearchSource.cs:96` (from `_session()`),
       `VaultLifecycleViewModel.cs:1182`, `WorkspaceViewModel.cs:2119`, and
       `Graph/WorkspaceViewModel.Graph.cs:224`, whose
       `GraphPreferencesViewModel` gains the same optional parameter and
       uses it for its `Read` and its writes. The graph writer's queued
       write can outlive the session, so
       `GraphConfigWriter.Enqueue(string key, GraphConfig aggregate, ulong
       generation, VaultRootAnchor? root = null)` (changed) keeps
       `root?.Pin()` with the admitted aggregate, the queued write passes
       it to `StoreFor(key).Write(aggregate, pinned)`, and the pin is
       disposed after that write, succeeded or failed (a dropped
       aggregate disposes its pin at once): a straggler lands in the root
       its edit was made in even after the session closed, and never in
       whatever a later session or a retargeted spelling names.
     The weak identity therefore decides nothing about where a store
     writes. What it cannot do either is outlive the session as a key:
     with Slate closed, a rename or move of the folder can change its
     index, and a later folder can take a freed slot's index — item 7.
7. **`FileRecentsStore` key and migration.** The constructor becomes
   `FileRecentsStore(string vaultRoot, VaultRootIdentity? identity = null,
   string? localAppDataRoot = null, VaultRootAnchor? root = null)` (the
   anchor new, kept as `_root`; `null` → `VaultRootAnchor.ByPathForTests`,
   item 6): the identity chooses the device-local key, the anchor is how
   the in-vault step reaches the vault. A persistent identity: key =
   SHA-256 of
   `$"{identity.Volume:x16}-{identity.IdHigh:x16}{identity.IdLow:x16}"`.
   The path-keyed file (today's key, kept as a new private field
   `_pathKeyedPath`) migrates into it, and **the source is deleted only
   after a successful read of it and a confirmed atomic commit of the
   destination**. On main neither half can be observed: `Save`
   (`FileRecentsStore.cs:127-148`) returns `void` and swallows
   `IOException` and `UnauthorizedAccessException` after logging
   `FileRecentsPersistFailed`, and `Read` (`:107-125`) turns every read
   error into an empty list — so "read, save, delete" would delete the
   only valid file after a failed write, or migrate an unreadable file as
   empty and then delete it. Two private helpers (new) make both halves
   observable:
   - `private RecentsRead TryRead(string path)` (new; `RecentsRead` is a
     new private record: `Absent`, `Read(IReadOnlyList<string> Entries)`
     or `Unreadable(Exception Error)`) — the bounded read and parse of
     `Read`, with an `IOException`, `UnauthorizedAccessException` or
     `JsonException` reported as `Unreadable`, not as an empty list.
     `Read` keeps its contract (an empty list for `Absent` and
     `Unreadable`) for the live file.
   - `private bool TryCommit(IReadOnlyList<string> entries)` (new) — the
     temporary file in the destination's directory is written through a
     `FileStream` and flushed with `Flush(flushToDisk: true)`, moved over
     the destination with `File.Move(temporary, _path, overwrite: true)`
     (one rename), and the destination is read back through `TryRead`;
     it returns `true` only when that read-back is `Read` with exactly
     `entries`. Any failure logs `FileRecentsPersistFailed` as today,
     deletes the temporary file and returns `false`. `Save` becomes
     `_ = TryCommit(Sanitize(paths))`, so every other caller keeps its
     behaviour.

   The migration runs in `Load` whenever `_pathKeyedPath` exists and
   differs from `_path` — not only when the destination is absent, so an
   interrupted migration is retried on the next load even after an `Add`
   has created the destination meanwhile:
   1. `TryRead(_pathKeyedPath)`: `Unreadable` → stop; nothing is written
      and nothing deleted, and the source is retried on the next load.
   2. `TryRead(_path)`: `Unreadable` → stop the same way (an unreadable
      destination is never overwritten); `Absent` → no entries.
   3. `merged = Sanitize(destination entries, then source entries)` —
      the destination's entries are the newer, so they lead and win the
      cap of 50.
   4. `TryCommit(merged)`: `false` → stop; the source stays, and `Load`
      returns `merged` for this session only.
   5. `true` → `SafeFile.TryDelete(_pathKeyedPath)`. A delete that fails
      leaves a source the next load merges again, which changes nothing
      (`Sanitize` drops the duplicates).

   `MigrateLegacy` (the old in-vault `.slate/file-recents.json`) is a
   migration source like the path-keyed file and is retried the same
   way, **also after the destination exists**. On main it runs only
   while the destination is absent (`Load`, `:41-44`), and `Add` is
   `Load` then `Save` (`:49-58`; `Replace` and `Clear` save too), so
   under that gate an in-vault file unreadable at the first load — held
   by another process, or refused — followed by an `Add` would create
   the destination and skip the in-vault entries for good. It runs in
   `Load` for every identity, persistent or weak, after the path-key
   migration and only once that migration has no source left (no
   `_pathKeyedPath` file other than `_path`; a weak identity's `_path` is
   that file, so it never has one), so entries keep their recency order —
   destination, then path-keyed, then in-vault — and it keeps running on
   every `Load` until this store instance has settled it:
   1. The anchored read: `AnchoredVaultStore.Open(_root,
      createDirectory: false)` — `.slate` opened relative to a duplicate
      of the session's held root (item 6); `null` (no `.slate`
      directory) is `Absent` — then
      `ReadAllBytesBounded(LegacyFileName, MaxPayloadBytes)` — `null` is
      `Absent` — parsed by the same private parse `TryRead` uses, into a
      `RecentsRead`; an
      `IOException` (an anchor whose session has gone included),
      `UnauthorizedAccessException` or `JsonException` is `Unreadable`.
      `Absent` → settled: nothing is written or deleted. `Unreadable` →
      stop: nothing is written or deleted, and the next `Load` retries.
   2. `TryRead(_path)`: `Unreadable` → stop (the destination is never
      overwritten); `Absent` → no entries.
   3. `merged = Sanitize(destination entries, then in-vault entries)` —
      the destination's entries, an `Add` made while the in-vault file
      was unreadable included, are the newer, so they lead and win the
      cap of 50.
   4. `TryCommit(merged)`: `false` → stop; the in-vault file stays, `Load`
      returns `merged` for that call, and the next `Load` retries (the
      store is not settled).
   5. `true` → `DeleteRegularFileIfExists(LegacyFileName)` through the
      same anchored store, then settled. A delete that fails leaves the
      file for the next store instance's first `Load`, which merges it
      again and changes nothing (`Sanitize` drops the duplicates).

   A new private `bool _legacySettled` records step 1's `Absent` and
   step 5, so a settled instance stops opening the vault's store on each
   `Load`; until then every `Load` — `Add`'s included — retries, so an
   in-vault file readable again is migrated by the next load even though
   the destination exists. Each vault open builds new instances
   (`FilesSidebarViewModel.cs:557`, `QuickSwitcherViewModel.cs:68`), which
   probe once more. A new internal counter `LegacyProbesForTests` (each
   anchored open the step makes) lets a fact observe the settling.
   `Replace` (the sidebar's rename reconciliation,
   `FilesSidebarViewModel.cs:2827`, `FilesSidebarViewModel.WriteJournal.cs:343`)
   writes a list that never held the in-vault entries, so they still
   merge behind it on a later load. `Clear` (Quick Open's Clear recents,
   `QuickSwitcherViewModel.cs:236`) means none: on an unsettled instance,
   after its own confirmed commit of the empty list, it deletes the
   in-vault file through the anchored store and settles the step whether
   or not that delete succeeded; a file the delete could not remove is
   merged by a later session's first load — the one way a cleared list
   regains entries, which needs the in-vault file unreadable at the first
   load and undeletable at the Clear. On main (`:69-105`) an unreadable
   in-vault file is read as empty, an empty destination is saved, and the
   in-vault file is deleted.

   A weak identity (`Persistent` false) keys nothing durable:
   the store keeps today's path key (SHA-256 of
   `_vaultRoot.ToUpperInvariant()`, `FileRecentsStore.cs:28-31`), so its
   `_path` is `_pathKeyedPath`, the path-key migration never runs, and no
   path-keyed file is moved or deleted. Why: a FAT index changes when the folder
   is renamed or moved, can be reused by a later folder in a freed slot,
   and is shared by the same folder on a cloned volume, so as a key it
   would neither follow the vault nor keep two vaults apart; the path key is what every Windows vault uses
   on main (`RootIdentity()` is null there), so these volumes lose
   nothing — a stick reopened at another drive letter starts with an
   empty list, exactly as on main. The in-vault step runs for a weak
   identity exactly as above, into its path-keyed `_path`, retried after
   that file exists. PR 1 (before this item lands in PR 2) edits
   `FileRecentsStore.cs:30` only to stop reading the removed
   `Device`/`Inode` fields and keys every identity by the path, so no
   recents file is orphaned between the two PRs.
8. **Provider reads** beneath the held root (`open_relative` `:185` with
   `FILE_READ_DATA`; listings by `NtQueryDirectoryFile` on a child
   handle), following name surrogates where the pathname read follows
   them today.
9. **What stays by path:** display (`opened_as`, `root_display_path` from
   `GetFinalPathNameByHandleW` gated by identity through
   `trash_handoff::final_path` `:328` + `names` `:233`; the Windows gate
   re-reads the folder at the final path with the read the held
   identity's `persistent` flag names), the host watcher, Reveal and
   ShellExecute (design §4.2). The display gate authorizes nothing:
   `root_display_path()` is display only — "nothing resolves through it"
   (`session.rs:3146-3151`) — and the path it checks is the held root
   handle's own final path, which names the held directory while the
   hold lasts, so a weak index that collided with another folder's could
   at worst accept that same path; the gate's answer is never a
   capability. Every write the session or the host's stores make goes
   through a held handle (items 5, 6 and 8).
10. **Split.** PR 1: step 0's probe record, then core (Windows `FsRoot`,
    `sys`, hook, the gated two-read identity with its `persistent` flag,
    the WD24 refusal and its four seams, `duplicate_root_handle`, facts)
    with the FFI record change, the two additive exports
    (`vault_root_identity_unavailable_detail` and
    `VaultSession.duplicate_root_handle`), both hosts'
    field edits (`FileRecentsStore.cs:30` keeping the path key,
    `AppState.swift:10670-10671`, `SidebarOrganizationDispatchTests.swift:2478-2479`),
    and the host's open-failure catch speaking core's detail. The refusal
    is first heard in PR 1, so PR 1 carries its host fact and its NVDA
    spot check, and PR 1 runs
    `FileRecentsStoreTests.AWeakIdentityKeepsThePathKeyAndMigratesNothing`
    (its interim path key satisfies it). PR 1's body writes `Refs #1475` and `Refs #940` — never a
    closing keyword, because the stores' held-root anchor and the recents key that
    answer #1475's columns are PR 2's. PR 2 (host only): the stores'
    held-root anchor (`VaultRootAnchor`, `AnchoredVaultStore.Open(root,
    …)`, the five stores, the construction sites, the graph writer's
    pin), `OpenVaultAsync`'s fail-closed null check, and
    the recents key (persistent: identity key with the migration; weak:
    the path key). PR 2's body writes `Closes #1475` and `Refs #940` with
    the 22W delivery comment; PR 2 merges only after PR 1 is merged and
    both PRs' Verification runs and PR 1's NVDA spot check are recorded,
    so the close lands when both deliveries are verified. Both before
    WB-29 and WB-30.

## Design

Per the section above once agreed. New names: `fs_root::sys` (Windows;
its `identity(file) -> io::Result<VaultRootIdentity>` over the private
`file_id_info(file)`, `file_system_name(file) -> io::Result<String>`
(`GetVolumeInformationByHandleW`) and `file_index(file)`, decided by the
`pub(super)` pure `identity_from_reads(file_id_info, file_system_name,
file_index)`, which takes the file index only after a `FileIdInfo`
failure on a volume named `FAT`, `FAT32` or `exFAT`), `db::anchored`
Windows body and `skip_install_for_tests`, `OpenSwapPoint::BeforeSlateHeld`,
`VaultRootIdentity { volume: u64, id_high: u64, id_low: u64, persistent:
bool }` (core and uniffi; `FsRoot::identity() ->
Option<VaultRootIdentity>`), `a11y::VAULT_ROOT_IDENTITY_UNAVAILABLE`,
`SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED`,
`SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE`, `SLATE_TEST_ROOT_FILE_ID_INFO_FAILS`
and `SLATE_TEST_ROOT_FILE_INDEX_COLLIDES`, the uniffi export
`vault_root_identity_unavailable_detail()`, core's
`VaultSession::duplicate_root_handle() -> Result<OwnedHandle,
VaultError>` (`#[cfg(windows)]`) over `FsRoot::duplicate_root() ->
io::Result<OwnedHandle>` and its uniffi export
`VaultSession.duplicate_root_handle() -> Result<u64, VaultError>`
(`Unsupported` off Windows), `VaultLifecycleViewModel.RootIdentityReader`,
the host's `VaultRootAnchor` (`Of(session)`, `OpenRoot()`, `Pin()`,
`ByPathForTests(vaultRoot)`, `ByPathAllowed`) with the test assembly's
`TestRootAnchors.cs` module initializer, `AnchoredVaultStore.Open(VaultRootAnchor
root, bool createDirectory, …)` (`.slate` and its children opened
relative to a duplicate of the session's held root; the final-path text
checks removed), the five stores' optional `VaultRootAnchor? root`
parameter, `GraphPreferencesViewModel`'s, and
`GraphConfigWriter.Enqueue(…, VaultRootAnchor? root = null)` holding
`root.Pin()` until its write ran (item 6),
`FileRecentsStore` identity key v2 (persistent identities only) with its
private `_pathKeyedPath`, `TryRead(path) -> RecentsRead` (`Absent`,
`Read(entries)`, `Unreadable(error)`) and `TryCommit(entries) -> bool`
(a flushed temporary file, one `File.Move` over the destination, a
read-back equal to `entries`), so a migration source is deleted only
after its successful read and a confirmed destination commit (item 7);
the in-vault step retried on every `Load` after the destination exists
until the instance settles it (`_legacySettled`, new private `bool`;
`LegacyProbesForTests`, new internal counter of its anchored opens),
with `Clear` deleting an unsettled in-vault file after its own confirmed
commit; and the step-0 probe script (Verification; not committed code). Two test helpers
in the new module, each reading the folder independently of `sys`, give
the weak facts their expectations: `fn file_index_of(path: &Path) ->
(u64, u64)` (new: `dwVolumeSerialNumber` zero-extended and
`nFileIndexHigh << 32 | nFileIndexLow`), `fn
file_id_info_answers(path: &Path) -> bool` (new: whether
`GetFileInformationByHandleEx(FileIdInfo)` succeeds on it), `fn
file_system_name_of(path: &Path) -> String` (new:
`GetVolumeInformationW` of the path's volume root) and `fn
file_id_info_of_handle(handle: BorrowedHandle<'_>) -> (u64, u128)` (new:
`FILE_ID_INFO` of an open handle, to say which folder a duplicated root
handle names).
`descriptor_interior.rs` keeps its
`#![cfg(unix)]` and every fact in it; a `cfg(windows)` sibling module
`session/tests/descriptor_interior_windows.rs` (new, registered as
`#[cfg(windows)] mod descriptor_interior_windows;` beside
`mod descriptor_interior;` in `session.rs`'s test module list, `:22305`)
holds the Windows families above, and `fs_root.rs`'s test module gains
three `#[cfg(windows)]` facts. A shared helper in the new module,
`fn retarget(link: &Path, to: &Path)` (new: `std::fs::remove_dir(link)`,
`std::fs::create_dir(link)`, `vault::test_support::junction(link, to)`),
and `fn tree_bytes(root: &Path) -> BTreeMap<PathBuf, Vec<u8>>` (new) for
the decoy's byte-identity check. Not changed: `root_identity()`'s and
`root_display_path()`'s FFI signatures (record fields only); macOS/Linux
bodies, beyond `FsRoot::identity()` wrapping `fstat`'s `(dev, ino)` in
the new record with `persistent: true`; the Unix facts.

## Tests

| Fact | Asserts | Mutation |
|---|---|---|
| `descriptor_interior.rs` (all of it, `#![cfg(unix)]` unchanged) and `fs_root.rs`'s three `cfg(any(macos, linux))` facts | still green on the rust lane (Linux) and the mac lane; **not** claimed for Windows — each renames a held object or uses `std::os::unix` | — |
| `fs_root.rs` `both_forms_open_create_rename_list_and_remove_the_same_files` (existing, uncfg'd, `:1028`) | now exercises the Windows `CacheDir` handle body: both forms create, rename (a file inside), list and remove the same files | route `InteriorDir for CacheDir` through the by-path impl |
| `fs_root.rs` `windows_a_held_cache_directory_refuses_a_rename_of_itself_and_of_its_parent` (new, `#[cfg(windows)]`; the pinning twin of `a_held_cache_directory_follows_its_folder_and_never_recreates_the_old_path`) | while `CacheDir::configured(&path)` is held, `std::fs::rename` of `path` and of its parent fails (`ERROR_SHARING_VIOLATION` or `ERROR_ACCESS_DENIED`); after `drop(held)` both succeed and nothing exists at the old path | open the held directory with `FILE_SHARE_DELETE` |
| `fs_root.rs` `windows_a_held_cache_directory_resolves_beneath_its_handle_when_its_spelling_is_retargeted` (new, `#[cfg(windows)]`; the interior-resolution twin of the same fact) | `CacheDir::configured` through a junction `link` → `real`; `retarget(link, decoy)`; `held.ensure_child_dir("oplog")` and `held.open_file("oplog/x.oplog", create_new)` land in `real`, `decoy` stays empty, `held.display() == link` | resolve the child by joining `display()` |
| `fs_root.rs` `windows_a_held_cache_directory_never_follows_a_junction` (new, `#[cfg(windows)]`; twin of `a_held_cache_directory_never_follows_a_link`, directory junctions only — a file symbolic link needs a privilege the lane lacks) | with `oplog` a junction to `elsewhere`, `open_file("oplog/x.oplog", …create)` and `read_child_dir("oplog")` are `Err`, and `elsewhere` stays empty | follow name surrogates in `sys::open_at` |
| `descriptor_interior_windows.rs`: `a_held_root_refuses_a_rename_of_itself_and_of_an_ancestor_while_the_session_lives` (the pinning twin of `:75`, `:142`, `:181`) | `MoveFileExW` on the root and on its parent fails with `ERROR_SHARING_VIOLATION`/`ERROR_ACCESS_DENIED`; after `drop(session)` the rename succeeds and nothing exists at the old path | open with `FILE_SHARE_DELETE` |
| `no_interior_writer_follows_a_retargeted_spelling` (new; the twin of `:618` and of `:229`'s post-rename half) | vault opened through `outer\vault` → `A`; `retarget(outer\vault, B)` with the session live; then a save returns `Ok` (design item 8: the re-read resolves beneath the held root — the Unix twin's tolerated `SavedButUnindexed` is a failure here), a retention change, an op-log append, a staged create through `.slate\tmp`, a `VaultStructuralLock::acquire`, and an insert through `open_worker_connection(&session.interior)` all land in `A` (bytes, `prefs.json`, `read_oplog`, the new note, the lock file, the row); `tree_bytes(B)` equals its value before the retarget | route one interior writer through `InteriorDir for Path` |
| `wal_connections_share_the_held_cache_across_a_retarget` (new; twin of `:229`) | `journal_mode` is `wal`; a worker connection sees the session's row and writes its own; `A\.slate\cache.sqlite-wal` and `-shm` exist; after `retarget(outer\vault, B)` a new worker connection reads all rows, and `B\.slate` holds no `cache.sqlite*` it did not hold before | open `-wal`/`-shm` by pathname |
| `a_second_process_on_the_textual_path_shares_the_anchored_cache` + its child `child_of_a_second_process_on_the_textual_path` (new Windows twins of `:293`/`:332`; the child is invoked with `--exact session::tests::descriptor_interior_windows::child_of_a_second_process_on_the_textual_path`) | the child opens `cache.sqlite` by its real path while the session holds `.slate`, sees `P0`, writes `C1`; the parent's open read transaction keeps its snapshot until `COMMIT` | open the session's cache exclusively |
| `root_display_path_follows_the_held_root_not_its_retargeted_spelling` (new; twin of `:424`) | before and after `retarget(outer\vault, B)`, `root_display_path()`'s `FILE_ID_INFO` equals the anchor; after it the path is `A`'s final path, never `outer\vault` (which now names `B`) | return `opened_as` without the identity gate |
| `canonical_path_is_computed_under_the_held_root_handle` (new; twin of the mac-only `:670`, as its doc's "Windows its pathname form until 22W" foresees) | after `retarget(outer\vault, B)`: `canonical_path("Notes/Alpha.md")` is `Some("Notes/Alpha.md")` (A's note), `canonical_path("Notes/Beta.md")` (B's) is `None`, `canonical_path("notes/alpha.md")` is `Some("Notes/Alpha.md")` (NTFS aliases case), and `canonical_path("Linked/x.md")` is `None` for a junction `A\Linked` | compute the stored spelling by path |
| `a_junctioned_interior_folder_is_refused` (new; twin of `:482`, directory junctions only) | a `.slate\oplog` junction refuses the op-log append and a `.slate\tmp` junction refuses a save, each with the typed refusal, and nothing is written where either junction points | follow the junction |
| `the_connection_names_the_synthetic_path_and_the_literal_path_does_not_exist` | `sqlite3_db_filename` starts with `\\.slate-fd\`; `CreateFileW` of it fails `ERROR_PATH_NOT_FOUND`; a row round-trips | open by path |
| `an_uninstalled_hook_fails_the_anchored_open_instead_of_opening_by_path` | with `skip_install_for_tests()`, `open_in` returns `Err`, no `cache.sqlite` written by path | fall back silently |
| `a_slate_junction_planted_before_the_hold_never_receives_the_interior` | at `BeforeSlateHeld`, `<root>\.slate` is replaced by a junction to an outside folder: the open refuses (`slate_refusal`) and the outside folder stays empty | follow the junction |
| `root_identity_on_windows_is_the_volume_serial_and_the_whole_128_bit_file_id` | the record equals `FILE_ID_INFO` of the held root with `persistent == true`; a synthetic id with high bits set round-trips through the record and the text form | truncate to 64 bits; read the file index first (→ `persistent == false` on NTFS) |
| `fs_root.rs` `the_file_index_fallback_is_taken_only_on_fat_fat32_and_exfat` (new, `#[cfg(windows)]`, a unit fact over `sys::identity_from_reads`; PR 1; codex round 11) | with the `FileIdInfo` read `Err(ERROR_INVALID_PARAMETER)` and a file index that answers: the names `FAT`, `FAT32`, `exFAT` and `fat32` give `Ok` with `persistent == false`; `NTFS`, `ReFS`, `""`, `ext4` (a name a share's server may report) and an unreadable name give `Err` carrying the `FileIdInfo` error, and the file-index closure was never called (a flag it sets); `FAT32` with the file index failing too gives `Err` carrying the index's error; a `FileIdInfo` read that answers gives that identity with `persistent == true`, neither closure called | fall back after any `FileIdInfo` failure, as round 10 specified (→ the `NTFS` and `ReFS` rows are `Ok`); add `ReFS` to the names (→ its row is `Ok`); key the fallback on the error code `ERROR_INVALID_PARAMETER` rather than the name (→ the `NTFS` row is `Ok`); compare names case-sensitively (→ the `fat32` row is `Err`) |
| `a_file_id_info_failure_on_a_volume_that_keeps_ids_refuses_the_open_and_never_reads_the_index` (new; PR 1; codex round 11: the transient failure on NTFS) | with `SLATE_TEST_ROOT_FILE_ID_INFO_FAILS` set to the name of a unique temp folder `T` through WB-18's `fault_guard()`: the preconditions that `file_system_name_of(T\vault)` is `NTFS` or `ReFS` and that `file_index_of(T\vault)` answers (so a fallback would have succeeded); `VaultSession::from_filesystem(T\vault)` is `Err(VaultError::Io(_))` whose `vault_error_detail` equals `VAULT_ROOT_IDENTITY_UNAVAILABLE`; `T\vault\.slate` does not exist; `std::fs::rename(T\vault, …)` succeeds right after (nothing is held); a sibling vault outside `T` opens with `persistent == true` | fall back after any `FileIdInfo` failure (→ `Ok` with `persistent == false`); key the fallback on the seam's error code, `ERROR_INVALID_PARAMETER`, which FAT32 also gives (→ the same) |
| `two_roots_forced_to_one_weak_index_keep_their_own_interiors` (new; PR 1; codex round 11's synthetic collision, core half) | with `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` and `SLATE_TEST_ROOT_FILE_INDEX_COLLIDES` both set to a unique temp folder `T`'s name through `fault_guard()`: session `a` opened through the junction `T\vault` → `T\a`, session `b` at `T\b`; the precondition `a.root_identity() == b.root_identity()`, both `persistent == false` (the collision is real); `retarget(T\vault, T\b)` with both live; `b` saves a note and `tree_bytes(T\b)` is taken; then `a`'s save, op-log append, staged create through `.slate\tmp`, structural lock (taken and dropped) and a worker connection's insert all land in `T\a`, and `tree_bytes(T\b)` is unchanged; `a.root_display_path()` is `T\a`'s final path, never `T\vault`; `b` then takes and drops its structural lock (the colliding `.slate` keys share one slot, so the two take turns) and `T\b\.slate\structural.lock` exists beside `T\a\.slate\structural.lock`; `file_id_info_of_handle` of `a.duplicate_root_handle()` is `T\a`'s and of `b`'s is `T\b`'s | derive the display path from `opened_as`, gated by the weak identity (→ `T\vault`, now naming `T\b`, passes the colliding gate); open one interior writer by the session's spelling (→ it lands in `T\b`); duplicate the root by reopening `opened_as` (→ `a`'s duplicate names `T\b`) |
| `the_duplicated_root_handle_names_the_held_root_after_its_spelling_is_retargeted` (new; PR 1) | the vault opened through `outer\vault` → `A`; `retarget(outer\vault, B)`; `duplicate_root_handle()` is `Ok`, and `file_id_info_of_handle` of it equals the session's persistent `root_identity()` and differs from `B`'s; after the duplicate is dropped, `MoveFileExW` of `A` still fails (the session's hold is its own); a session opened with `VaultSession::open` over a test provider no filesystem root backs (as `session/tests/structural_batch.rs:976` builds one) returns `Err(VaultError::Unsupported { .. })` | duplicate by reopening `opened_as` (→ `B`'s id); hand out the held handle itself, not a duplicate (→ dropping it closes the hold: the rename of `A` succeeds) |
| `a_root_whose_file_id_info_is_unsupported_opens_with_a_weak_file_index_identity` (WD24 amended; PR 1) | with `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` (the FAT32 profile: the identity read's file-system name is `FAT32` and `FileIdInfo` fails) set to the name of a unique temp folder `T` (set and cleared through WB-18's `fault_guard()`) that holds the vault `T\vault` and a junction `T\other` → `T\vault`, so both spellings contain it: `VaultSession::from_filesystem(T\vault)` is `Ok`; `root_identity()` is `Some` with `persistent == false`, `id_high == 0` and `(volume, id_low) == file_index_of(T\vault)` (the watcher's pair), after asserting the precondition that the lane's NTFS index has a nonzero high word (`file_index_of(T\vault).1 >> 32 != 0`); `T\vault\.slate` exists; a second session opened through `T\other` shares the first's structural-lock slot (item 4's weak key); a sibling vault outside `T` opens with `persistent == true` | refuse when `FileIdInfo` fails, as WD24 read before its amendment (→ `Err`); flag the fallback persistent (→ `persistent == true`); keep only `nFileIndexLow` (→ `id_low` differs); gate the fallback on the volume's real file-system name, ignoring the seam's profile (→ the lane's `NTFS` refuses: `Err`) |
| `a_root_whose_file_id_cannot_be_read_does_not_open_and_gains_no_slate` (WD24: neither read answers; PR 1) | with `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE` set to the temp root's unique folder name (set and cleared through WB-18's `fault_guard()`) — the FAT32 profile with both reads failing, so the fallback is taken and fails too: `VaultSession::from_filesystem(root)` is `Err(VaultError::Io(_))` and `a11y::vault_error_detail` of it equals `VAULT_ROOT_IDENTITY_UNAVAILABLE` exactly; `root\.slate` does not exist; `std::fs::rename(root, …)` succeeds right after (nothing is held); a sibling vault in the same fact, whose path does not contain the value, opens with `root_identity()` `Some` | open with the identity `None` instead of refusing (→ `Ok`); open with a path-derived identity when both reads fail (→ `Ok`); make `.slate` before the identity read (→ `.slate` exists); pass the OS error through (→ the detail is the OS text) |
| `a_vault_on_a_fat32_or_exfat_volume_opens_with_the_identity_its_file_system_answers` (`#[cfg(windows)] #[ignore = "needs SLATE_WEAK_VOLUME_FAT32 or SLATE_WEAK_VOLUME_EXFAT (WB-27's volume procedure)"]`; WD24 amended; PR 1) | for each of the two variables that is set (it panics when neither is, so a named run never passes vacuously): the precondition that `file_system_name_of(root)` is `FAT32` for the FAT32 variable and `exFAT` for the exFAT one (the names the fallback gate admits); a fresh vault folder on that volume opens through `from_filesystem`; `root_identity()` is `Some`, its `persistent` equals `file_id_info_answers(root)` (expected `false`, per step 0's record), and when `persistent` is false `(volume, id_low) == file_index_of(root)`; `root\.slate` exists; a note saved through the session reads back | refuse when `FileIdInfo` fails (→ the open fails on a volume without it); flag the fallback persistent (→ `persistent` differs from `file_id_info_answers`); admit `FAT32` alone (→ the exFAT vault refuses) |
| `the_windows_structural_lock_registry_is_keyed_by_identity` (twin of `:357`) | two sessions on two roots with one textual spelling (`outer\vault` → `A`, then `retarget(outer\vault, B)` and a second open) get distinct slots — `A`'s lock is held, `B`'s free; and a third session opened through a second spelling of `A` (`outer\other` → `A`) shares the first session's slot | key by path |
| uniffi: `root_identity_crosses_the_ffi_losslessly` | four fields, a high-bits id and `persistent` both `true` and `false` | drop a field; drop `persistent` |
| uniffi: `the_root_identity_refusal_crosses_the_ffi_as_cores_sentence` (PR 1) | `vault_error_detail(VaultError::from(core::VaultError::Io(io::Error::new(io::ErrorKind::InvalidInput, VAULT_ROOT_IDENTITY_UNAVAILABLE))))` and `vault_root_identity_unavailable_detail()` both equal `core::a11y::VAULT_ROOT_IDENTITY_UNAVAILABLE` | render `Io` by the binding's `Display` (`"io error: …"`); return a second copy of the text from the export |
| uniffi: `duplicate_root_handle_crosses_the_ffi_as_an_owned_duplicate` (PR 1) | on Windows: the returned `u64`, wrapped by the fact as an `OwnedHandle`, reads the same `FILE_ID_INFO` as `root_identity()`; after the fact drops it, a rename of the vault root still fails (core's hold is untouched); off Windows (the rust lane on Linux): `Err(VaultError::Unsupported { .. })` | return the held handle's raw value rather than a duplicate (→ after the drop the rename succeeds); return `Ok(0)` off Windows (→ the Linux arm fails) |
| Windows `W1AnchoredVaultStoreTests.OpenReachesTheHeldRootWhenTheVaultsSpellingIsRetargeted` (PR 2; replaces round 10's `OpenRefusesADifferentFolderAtTheVaultsPath`) | the vault `T\a` opened through a junction spelling `T\vault` (made with `mklink /J`, which needs no privilege, as `CanvasDocumentTests.cs:4131-4134` does) and a decoy `T\b` with its own `.slate`; the junction retargeted to `T\b` between session open and store open (the held root cannot be renamed); `AnchoredVaultStore.Open(VaultRootAnchor.Of(session), createDirectory: true)` then `WriteAtomically("probe.json", …)` lands in `T\a\.slate\probe.json`, `ReadAllBytesBounded` reads it back, and every file under `T\b` is byte-identical | open the root by the session's spelling as main does (→ the junction is refused as a reparse point, nothing reaches `T\a`); open the spelling following its reparse point (→ `probe.json` lands in `T\b`) |
| `W1AnchoredVaultStoreTests.EveryStoreWritesBeneathTheHeldRootAfterTheSpellingIsRetargeted` (PR 2; replaces `EveryStoreRefusesAfterTheRootWasReplaced`) | after the same retarget, `WorkspacePersistence.Save`, `SearchRecentsStore.Add`, `SidebarSettingsStore.SetOrganization` and `GraphConfigStore.Write`, each built with `VaultRootAnchor.Of(session)`, write their files into `T\a\.slate`, and `T\b` stays byte-identical | one store ignores its anchor and opens its `vaultRoot` spelling (→ that store's file is refused or lands in `T\b`) |
| `W1AnchoredVaultStoreTests.ARootForcedToAnotherVaultsWeakIndexIsNeverReachedThroughIt` (PR 2; codex round 11's synthetic colliding-index / retargeted-root fact, host half; replaces `AWeakIdentityIsComparedByTheFileIndex`) | with `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` and `SLATE_TEST_ROOT_FILE_INDEX_COLLIDES` set in-process to the fixture's unique folder name `T` (cleared in `finally`): session `a` opened through the junction `T\vault` → `T\a`, session `b` at `T\b`; the precondition that `a.RootIdentity()` equals `b.RootIdentity()` with `Persistent` false; the junction retargeted to `T\b`; a store through `VaultRootAnchor.Of(a)` writes `probe.json` into `T\a\.slate` and `T\b\.slate` stays byte-identical; a store through `VaultRootAnchor.Of(b)` writes `probe.json` into `T\b\.slate` and `T\a\.slate`'s other files are unchanged | round 10's admission — reopen the session's spelling and admit when the identity read there under the flag equals `RootIdentity()` (→ `a`'s store reaches `T\vault`, which names `T\b`, and `probe.json` never reaches `T\a`); pick the root through a process-wide table of open sessions keyed by `RootIdentity()` (→ under the collision `a`'s store gets `b`'s root and writes into `T\b`) |
| `W1AnchoredVaultStoreTests.AStoreRefusesOnceItsSessionIsDisposed` (PR 2) | the session disposed; `AnchoredVaultStore.Open(VaultRootAnchor.Of(session), createDirectory: true)` throws `IOException`, and the vault's `.slate` gains no file | fall back to the spelling when core refuses (→ the store opens and writes) |
| `W1AnchoredVaultStoreTests.AStoreGivenNoAnchorRefusesOutsideTheTestAssembly` (PR 2; in a collection with `DisableParallelization = true`) | with `VaultRootAnchor.ByPathAllowed` set `false` (restored in `finally`), `new WorkspacePersistence(root).Save(snapshot)` throws `IOException` and nothing is written under `root\.slate`; with it `true` again (the test assembly's setting) the same call writes | let a null anchor open the spelling whatever `ByPathAllowed` says (→ the first save writes) |
| `GraphConfigWriterTests.AQueuedWriteLandsInTheRootItWasEditedIn` (PR 2; new fact in the existing class) | a `GraphConfigWriter` of the fact's own whose `WriteGateForTests` signals `entered` and parks on `release` (a `ManualResetEventSlim`; the gate records its `Wait(TimeSpan.FromSeconds(10))` result, asserted after the write, and `release` is set in `finally`); the vault `T\a` opened through the junction `T\vault`; `Enqueue(key, aggregate, generation, VaultRootAnchor.Of(session))`; `entered.Wait(TimeSpan.FromSeconds(10))` asserted true; the session disposed and the junction retargeted to `T\b`; `release` set and the write's task awaited: `T\a\.slate\graph.json` holds the aggregate, `FailedForTests` is 0, `T\b` is byte-identical, and `T\a` can then be renamed (the pin was disposed after the write) | resolve the anchor when the write runs instead of pinning at `Enqueue` (→ the disposed session refuses: `FailedForTests` is 1 and no `graph.json`); never dispose the pin (→ the rename of `T\a` fails) |
| `FileRecentsStoreTests` (new class): `APathKeyedRecentsFileMigratesToTheIdentityKeyOnce`, `TheIdentityKeyIsTheLosslessIdentity` (persistent identities) | old file read, new written, old deleted, second load no-op; key text carries the high bits | skip the delete; format `Device-Inode` |
| `FileRecentsStoreTests.AFailedDestinationCommitKeepsThePathKeyedFile` (PR 2; item 7) | a persistent identity, the path-keyed file holding `[a, b]`, and a directory created at the identity-keyed file's path so `File.Move` over it fails: `Load()` returns `[a, b]`, the path-keyed file is byte-identical afterwards and no identity-keyed file exists; after the directory is removed, the next `Load()` migrates — the identity-keyed file reads `[a, b]` and the path-keyed file is gone | delete the source after a `void Save` (main's `Save`, `:127-148`) → the path-keyed file is gone and nothing holds `[a, b]` |
| `FileRecentsStoreTests.AnUnreadablePathKeyedFileIsNeitherMigratedNorDeleted` (PR 2; item 7) | the path-keyed file `[a, b]` held open by the fact with `FileShare.None` (released in `finally`) during `Load()`: `Load()` returns an empty list, no identity-keyed file is created, and once the hold is released the path-keyed file is byte-identical; the next `Load()` migrates it | read the source through main's `Read` (`:107-125`, an error is an empty list) → an empty destination is committed and the source deleted |
| `FileRecentsStoreTests.AnInterruptedMigrationCompletesOnALaterLoad` (PR 2; item 7) | the path-keyed file `[a, b]` held with `FileShare.None` while `Add("c")` runs (the migration stops; `Add` commits `[c]` to the identity-keyed file); the hold released (in `finally`), the next `Load()` returns `[c, a, b]`, the identity-keyed file reads `[c, a, b]` and the path-keyed file is gone | migrate only while the identity-keyed file is absent → the next `Load()` returns `[c]` and the path-keyed file stays forever |
| `FileRecentsStoreTests.AnUnreadableInVaultRecentsFileIsNotDeleted` (PR 2; item 7, `MigrateLegacy`) | the store built with the fixture vault's own persistent `session.RootIdentity()` and `VaultRootAnchor.Of(session)`, through which the in-vault read reaches the vault; neither device file exists and the vault's `file-recents.json` (`[a]`) is held with `FileShare.None` during `Load()` (released in `finally`): `Load()` returns an empty list, no identity-keyed file is created, and the in-vault file is byte-identical after the hold; the next `Load()` migrates `[a]` and only then deletes it | main's `MigrateLegacy` (`:69-105`: a read error saves an empty destination and deletes the in-vault file) → the in-vault file is gone |
| `FileRecentsStoreTests.AnInVaultRecentsFileUnreadableBeforeAnAddMigratesOnALaterLoad` (PR 2; item 7, the in-vault step; Theory: the fixture vault's persistent identity, and its weak one through `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` set in-process to the fixture's unique folder name, cleared in `finally`) | the store built with the fixture vault's own `session.RootIdentity()` (`Persistent` asserted per row) and `VaultRootAnchor.Of(session)`; no device recents file exists and the vault's `file-recents.json` (`[a, b]`) is held with `FileShare.None` while `Load()` and `Add("c")` run: `Load()` returns an empty list, `Add("c")` returns `[c]` and creates the destination (it reads `[c]`), and the in-vault file is byte-identical; the hold released (in `finally`), the next `Load()` returns `[c, a, b]`, the destination reads `[c, a, b]`, the in-vault file is gone, and one more `Load()` leaves `LegacyProbesForTests` unchanged (settled) | run the in-vault step only while no device file exists (main's `Load` gate, `:41-44`) → the next `Load()` returns `[c]` and the in-vault file stays forever; or never settle → the last `Load()` probes the vault again |
| `FileRecentsStoreTests.AFailedInVaultCommitIsRetriedAfterTheDestinationExists` (PR 2; item 7, the in-vault step) | the fixture vault's own persistent identity and `VaultRootAnchor.Of(session)`; the destination holds `[c]` with the read-only attribute set — `File.Move(…, overwrite: true)` onto a read-only file fails with access denied while the file stays readable (measured on this machine) — and the vault's `file-recents.json` holds `[a, b]`: `Load()` returns `[c, a, b]` for that call, `FileRecentsPersistFailed` is logged once (`HostLog.ObserverForTests`), and the destination (`[c]`) and the in-vault file are byte-identical; the attribute cleared (again in `finally`), the next `Load()` on the same instance commits `[c, a, b]` and only then deletes the in-vault file | delete the in-vault file after an unconfirmed commit (main's `void Save`) → the in-vault file is gone and the destination still reads `[c]`; or settle the step on a failed commit → the next `Load()` returns `[c]` and the in-vault file stays |
| `FileRecentsStoreTests.ClearingRecentsDropsAnUnmigratedInVaultFile` (PR 2; item 7, the in-vault step) | the fixture vault's own persistent identity and `VaultRootAnchor.Of(session)`; the vault's `file-recents.json` (`[a]`) held with `FileShare.None` during the first `Load()` (an empty list; the step unsettled), the hold released (in `finally`), then `Clear()`: the destination reads `[]`, the in-vault file is gone, and the next `Load()` returns `[]` | `Clear` without the in-vault delete → the next `Load()` returns `[a]` |
| `FileRecentsStoreTests.AWeakIdentityKeepsThePathKeyAndMigratesNothing` (WD24 amended; PR 1, where every identity keeps the path key, and PR 2) | a store built with `new VaultRootIdentity(volume, 0, index, false)` reads and writes the same file as a store built with `identity: null` for that root; a path-keyed file present beforehand is neither moved nor deleted, and no identity-keyed file appears | key by the weak identity (→ another file); run the migration for a weak identity (→ the path-keyed file is deleted) |
| `VaultOpenIdentityTests` (new class): `AFolderWhoseFileSystemGivesNoFileIdDoesNotOpenAndSaysWhy` (WD24; PR 1) | through the real core, with `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE` set in-process to the fixture vault's unique folder name (cleared in `finally`): `OpenVaultAsync(root)` raises exactly one announcement, a `HostComposed` at `High` whose text is `"Could not open vault: " + SlateUniffiMethods.VaultRootIdentityUnavailableDetail()`; no `VaultOpened`; `IsVaultOpen` false; `ReturnedToWelcome` raised once; the recents list unchanged; `root\.slate` absent | speak `exception.Message` (the text becomes "Could not open vault: @message=…"); open anyway (→ `VaultOpened`) |
| `VaultOpenIdentityTests.OpenRefusesWhenCoreReportsNoIdentity` (PR 2) | a real vault opens in core, and `RootIdentityReader` returns `null`: the session is disposed, the same single `High` sentence is spoken, no `VaultOpened`, no recents entry, `ReturnedToWelcome` raised once | admit null (→ `VaultOpened`) |
| `VaultOpenIdentityTests.AFolderAnsweringOnlyTheFileIndexOpens` (WD24 amended; PR 2) | through the real core, with `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` set in-process to the fixture vault's unique folder name (cleared in `finally`): `OpenVaultAsync(root)` raises `VaultOpened` once, `IsVaultOpen` is true, `RootIdentity()` has `Persistent` false, no `High` refusal is spoken and the recent-vaults list gains the vault | refuse a non-persistent identity in `OpenVaultAsync` (→ no `VaultOpened`); fail the open in core when `FileIdInfo` fails (→ the refusal is spoken) |
| `RescanTests.ARefreshAfterTheVaultsSpellingIsRetargetedListsTheHeldVault` (new; replaces the live-rename read the Windows hold makes impossible) | the vault opened through a junction, the junction retargeted to a decoy vault; Refresh's tree lists the held vault's notes and none of the decoy's | revert the provider reads to pathname-based |
| mac: `swift-tests.yml` compiles `AppState.swift:10670-10671` and runs `SidebarOrganizationDispatchTests.testOpenBindsSessionAndSidebarToOneObservedRoot` with its `:2478-2479` read changed to `anchor.volume`/`anchor.idLow` | the session's anchor and the sidebar admission still match | map `inode` from `idHigh` at `AppState.swift:10671` (→ the open aborts with `VaultOpenIdentityRaceError` and the fact fails) |

## Docs and registers

- Design `22_descriptor_relative_session_design.md` §7 row 22W: append a
  "Delivered …" text as 22a's did; §11 a dated note on WD22's answer.
- Contract 31 FR-10: append a sentence that the session's root identity
  is core-owned on Windows and shares structural identity's form where
  the file system answers `FileIdInfo`; that on a FAT, FAT32 or exFAT
  volume (by `GetVolumeInformationByHandleW`'s name) whose `FileIdInfo`
  read fails it is the volume serial and 64-bit file index, flagged weak
  (`persistent: false`), which keys nothing durable and authorizes
  nothing — equal weak identities need not be one folder (cloned
  volumes), so the host's vault stores reach the vault through a
  duplicate of the session's held root handle and compare no identity;
  that on any other file system a `FileIdInfo` failure refuses the open
  (ReFS's 64-bit index is not unique), and that a FAT, FAT32 or exFAT
  root for which neither read answers does not open either (WD24 as
  amended 2026-10-21 and narrowed by codex round 11, core's
  `VAULT_ROOT_IDENTITY_UNAVAILABLE` sentence).
- `24_structural_identity_capability_matrix.md`: append (dated, never
  rewrite) step 0's table — NTFS control, FAT32, exFAT: the file-system
  name the volume reports, which identity
  read answers, the `FileIdInfo` error number, the index's stability and
  distinctness within a mount, and what a longer rename did — to the §5
  Windows block WB-27 appended.
- No `corpus.json`, census, designations or trigger-ledger change: the
  refusal is a `VaultError` detail, not an `A11yEvent` (design item 6).
- PR 1's body: step 0's probe table, the gated FAT32/exFAT fact's run,
  the NVDA spot check (Verification) with its NVDA log excerpt, and the
  consequence of the narrowed fallback: a share whose server reports
  NTFS or ReFS but answers no `FileIdInfo` now refuses to open.
- `18_windows_port/parity_matrix.md`: `python -I scripts/generate-parity-matrix.py
  --check` stays green (no command rows change).
- Plan §11's helper names: the plan author replaces `list_dir_at`,
  `stat_at`, `open_at` with `sys::{open_at, stat_at, read_dir_names}` and
  `InteriorDir`.
- Issues: PR 1's body writes `Refs #1475` and `Refs #940` (no closing
  keyword). PR 2's body writes `Closes #1475` and `Refs #940` with the
  22W delivery comment, and PR 2 merges only after PR 1 is merged and
  both PRs' Verification runs and PR 1's NVDA spot check are recorded.

## Verification

PowerShell, repo root, on Windows. Each filter is listed first and the
block throws unless every required fact is discovered under it; every
command's `$LASTEXITCODE` is checked (no output grepping for "Failed").
The Unix facts this chunk leaves in place run on the rust and swift
lanes, not here.

Step 0, before any code (PR 1's first commit records its output; design
item 0). In the elevated session where WB-27's volume procedure set
`$fatRoot` and `$exfatRoot` (the FAT32 and exFAT VHDs); the script was
run against NTFS temp folders on 2026-10-21 and gave the control row
quoted under Current state:

```powershell
$ErrorActionPreference = 'Stop'
if (-not $fatRoot -or -not $exfatRoot) { throw 'run WB-27''s volume procedure in this session first' }
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
public static class RootIdProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdInfo { public ulong VolumeSerialNumber; public ulong IdLow; public ulong IdHigh; }
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct ByHandleFileInformation { public uint Attributes; public long Created, Accessed, Written; public uint VolumeSerialNumber, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int infoClass, out FileIdInfo info, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation info);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetVolumeInformationByHandleW(SafeFileHandle file, IntPtr volumeName, uint volumeNameSize, IntPtr serial, IntPtr maximumComponent, IntPtr flags, StringBuilder fileSystemName, uint fileSystemNameSize);
    // One folder, both reads (FileIdInfo = class 18) and its volume's file-system name: each its value or "error <Win32 code>".
    public static string[] Probe(string path)
    {
        using SafeFileHandle file = CreateFileW(path, 0x80, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (file.IsInvalid) { throw new IOException($"cannot open {path}: error {Marshal.GetLastWin32Error()}"); }
        string fileId = GetFileInformationByHandleEx(file, 18, out FileIdInfo id, 24)
            ? $"{id.VolumeSerialNumber:x16}-{id.IdHigh:x16}{id.IdLow:x16}"
            : $"error {Marshal.GetLastWin32Error()}";
        string index = GetFileInformationByHandle(file, out ByHandleFileInformation info)
            ? $"{info.VolumeSerialNumber:x8}-{info.IndexHigh:x8}{info.IndexLow:x8}"
            : $"error {Marshal.GetLastWin32Error()}";
        var name = new StringBuilder(261);
        string fileSystem = GetVolumeInformationByHandleW(file, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, name, (uint)name.Capacity)
            ? name.ToString()
            : $"error {Marshal.GetLastWin32Error()}";
        return new[] { fileId, index, fileSystem };
    }
}
'@
$ntfsRoot = Join-Path ([IO.Path]::GetTempPath()) "wb28-probe-$PID"; New-Item -ItemType Directory $ntfsRoot -Force | Out-Null
$rows = foreach ($volume in @(@{ Name = 'NTFS'; Root = $ntfsRoot }, @{ Name = 'FAT32'; Root = $fatRoot }, @{ Name = 'exFAT'; Root = $exfatRoot })) {
    $a = Join-Path $volume.Root 'wb28-a'; $b = Join-Path $volume.Root 'wb28-b'; $longer = Join-Path $volume.Root 'wb28-a-renamed-to-a-longer-name'
    New-Item -ItemType Directory $a, $b -Force | Out-Null
    $first = [RootIdProbe]::Probe($a); $again = [RootIdProbe]::Probe($a); $sibling = [RootIdProbe]::Probe($b)
    Rename-Item $a (Split-Path $longer -Leaf); $renamed = [RootIdProbe]::Probe($longer)
    Remove-Item $longer, $b -Recurse -Force
    $row = [pscustomobject]@{ Volume = $volume.Name; FileSystem = $first[2]; FileIdInfo = $first[0]; FileIndex = $first[1]; IndexAgain = $again[1]; SiblingIndex = $sibling[1]; FileIdInfoAfterRename = $renamed[0]; IndexAfterRename = $renamed[1] }
    if ($volume.Name -eq 'NTFS' -and $row.FileIdInfo -like 'error*') { throw 'the NTFS control answers no FileIdInfo: the probe is broken' }
    if ($row.FileIdInfo -like 'error*' -and $row.FileIndex -like 'error*') { throw "$($volume.Name) answers neither read: stop for an owner decision (WD24 would refuse its vaults)" }
    if ($row.FileIdInfo -like 'error*' -and @('FAT', 'FAT32', 'exFAT') -notcontains $row.FileSystem) { throw "$($volume.Name) needs the fallback but reports '$($row.FileSystem)', which the fallback gate (design item 6) refuses: stop for an owner decision" }   # -notcontains is case-insensitive, as the gate is
    if ($row.FileIdInfo -like 'error*' -and ($row.IndexAgain -ne $row.FileIndex -or $row.SiblingIndex -eq $row.FileIndex)) { throw "$($volume.Name)'s file index is not stable and distinct within a mount: stop for an owner decision" }
    $row
}
Remove-Item $ntfsRoot -Recurse -Force
$rows | Format-List   # PR 1's body and the matrix's §5 Windows block; a FAT32/exFAT row with a FileIdInfo value keeps the fallback as a defensive path (design item 0)
```

The delivery block, on Windows:

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb28'
function Assert-Ok([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed ($LASTEXITCODE)" } }
function Assert-Facts([string]$crate, [string]$filter, [string[]]$facts, [switch]$Ignored) {
    $listed = if ($Ignored) { cargo test -p $crate --locked $filter -- --ignored --list } else { cargo test -p $crate --locked $filter -- --list }
    Assert-Ok "listing $crate $filter"
    foreach ($fact in $facts) {
        if (-not ($listed -match "(^|::)$([regex]::Escape($fact)): test$")) { throw "filter '$filter' does not select $fact" }
    }
}
$required = [ordered]@{
    'descriptor_interior_windows' = @('a_held_root_refuses_a_rename_of_itself_and_of_an_ancestor_while_the_session_lives', 'no_interior_writer_follows_a_retargeted_spelling', 'wal_connections_share_the_held_cache_across_a_retarget', 'a_second_process_on_the_textual_path_shares_the_anchored_cache', 'child_of_a_second_process_on_the_textual_path', 'root_display_path_follows_the_held_root_not_its_retargeted_spelling', 'canonical_path_is_computed_under_the_held_root_handle', 'a_junctioned_interior_folder_is_refused', 'the_connection_names_the_synthetic_path_and_the_literal_path_does_not_exist', 'an_uninstalled_hook_fails_the_anchored_open_instead_of_opening_by_path', 'a_slate_junction_planted_before_the_hold_never_receives_the_interior', 'root_identity_on_windows_is_the_volume_serial_and_the_whole_128_bit_file_id', 'a_root_whose_file_id_info_is_unsupported_opens_with_a_weak_file_index_identity', 'a_root_whose_file_id_cannot_be_read_does_not_open_and_gains_no_slate', 'a_file_id_info_failure_on_a_volume_that_keeps_ids_refuses_the_open_and_never_reads_the_index', 'two_roots_forced_to_one_weak_index_keep_their_own_interiors', 'the_duplicated_root_handle_names_the_held_root_after_its_spelling_is_retargeted', 'the_windows_structural_lock_registry_is_keyed_by_identity')
    'fs_root' = @('both_forms_open_create_rename_list_and_remove_the_same_files', 'windows_a_held_cache_directory_refuses_a_rename_of_itself_and_of_its_parent', 'windows_a_held_cache_directory_resolves_beneath_its_handle_when_its_spelling_is_retargeted', 'windows_a_held_cache_directory_never_follows_a_junction', 'the_file_index_fallback_is_taken_only_on_fat_fat32_and_exfat')
}
foreach ($filter in $required.Keys) {
    Assert-Facts 'slate-core' $filter $required[$filter]
    cargo test -p slate-core --locked $filter; Assert-Ok "cargo test $filter"
}
cargo test -p slate-core --locked anchored; Assert-Ok 'anchored'
cargo test -p slate-core --locked --no-fail-fast; Assert-Ok 'the whole core suite'
Assert-Facts 'slate-uniffi' 'root_identity' @('root_identity_crosses_the_ffi_losslessly', 'the_root_identity_refusal_crosses_the_ffi_as_cores_sentence')
cargo test -p slate-uniffi --locked root_identity; Assert-Ok 'uniffi root_identity'
Assert-Facts 'slate-uniffi' 'duplicate_root_handle' @('duplicate_root_handle_crosses_the_ffi_as_an_owned_duplicate')
cargo test -p slate-uniffi --locked duplicate_root_handle; Assert-Ok 'uniffi duplicate_root_handle'
cargo fmt --all -- --check; Assert-Ok 'fmt'
cargo clippy --all-targets --workspace -- -D warnings; Assert-Ok 'clippy'
cargo test --workspace --locked; Assert-Ok 'workspace tests'
cargo bench --workspace --no-run; Assert-Ok 'bench compile'
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh; Assert-Ok 'licence headers'   # Git Bash by full path: a bare `bash` in PowerShell here resolves to WSL's C:\WINDOWS\system32\bash.exe
./apps/slate-windows/generate-bindings.ps1 -Locked; Assert-Ok 'bindings'
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'build'
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$filter = 'FullyQualifiedName~SlateWindows.Tests.W1AnchoredVaultStoreTests|FullyQualifiedName~SlateWindows.Tests.FileRecentsStoreTests|FullyQualifiedName~SlateWindows.Tests.VaultOpenIdentityTests|FullyQualifiedName~SlateWindows.Tests.RescanTests|FullyQualifiedName~SlateWindows.Tests.GraphConfigWriterTests'
$listed = dotnet test $tests --no-build --configuration Release --list-tests --filter $filter; Assert-Ok 'listing the host facts'
foreach ($fact in 'OpenReachesTheHeldRootWhenTheVaultsSpellingIsRetargeted', 'EveryStoreWritesBeneathTheHeldRootAfterTheSpellingIsRetargeted', 'ARootForcedToAnotherVaultsWeakIndexIsNeverReachedThroughIt', 'AStoreRefusesOnceItsSessionIsDisposed', 'AStoreGivenNoAnchorRefusesOutsideTheTestAssembly', 'AQueuedWriteLandsInTheRootItWasEditedIn', 'APathKeyedRecentsFileMigratesToTheIdentityKeyOnce', 'TheIdentityKeyIsTheLosslessIdentity', 'AFailedDestinationCommitKeepsThePathKeyedFile', 'AnUnreadablePathKeyedFileIsNeitherMigratedNorDeleted', 'AnInterruptedMigrationCompletesOnALaterLoad', 'AnUnreadableInVaultRecentsFileIsNotDeleted', 'AnInVaultRecentsFileUnreadableBeforeAnAddMigratesOnALaterLoad', 'AFailedInVaultCommitIsRetriedAfterTheDestinationExists', 'ClearingRecentsDropsAnUnmigratedInVaultFile', 'AWeakIdentityKeepsThePathKeyAndMigratesNothing', 'AFolderWhoseFileSystemGivesNoFileIdDoesNotOpenAndSaysWhy', 'OpenRefusesWhenCoreReportsNoIdentity', 'AFolderAnsweringOnlyTheFileIndexOpens', 'ARefreshAfterTheVaultsSpellingIsRetargetedListsTheHeldVault') {
    if (-not ($listed -match "\.$([regex]::Escape($fact))(\(.*\))?$")) { throw "the host filter does not select $fact" }   # a Theory lists as Name(row)
}
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'host facts'
# PR 2: production never enables the by-path anchor, and every store construction site passes the session's anchor (the by-path default refuses in production anyway; this names a forgotten site before a reader meets the refusal)
$enabled = @(git grep -n -e 'ByPathAllowed = true' -e 'ByPathAllowed=true' -- 'apps/slate-windows/src'); if ($LASTEXITCODE -gt 1) { throw 'git grep failed' }
if ($enabled.Count -gt 0) { throw "production code enables the by-path anchor: $($enabled -join '; ')" }
$anchored = @(git grep -n 'VaultRootAnchor.Of(' -- 'apps/slate-windows/src'); Assert-Ok 'git grep VaultRootAnchor.Of'
foreach ($site in 'FilesSidebarViewModel.cs', 'QuickSwitcherViewModel.cs', 'Search/VaultSearchSource.cs', 'VaultLifecycleViewModel.cs', 'SlateWindows/WorkspaceViewModel.cs', 'Graph/WorkspaceViewModel.Graph.cs') {
    if (-not ($anchored -match [regex]::Escape($site))) { throw "$site builds a store without VaultRootAnchor.Of(session)" }
}
$cs = @('apps/slate-windows/src/SlateWindows/AnchoredVaultStore.cs','apps/slate-windows/src/SlateWindows/FileRecentsStore.cs','apps/slate-windows/src/SlateWindows/FilesSidebarViewModel.cs','apps/slate-windows/src/SlateWindows/QuickSwitcherViewModel.cs','apps/slate-windows/src/SlateWindows/VaultLifecycleViewModel.cs','apps/slate-windows/src/SlateWindows/WorkspacePersistence.cs','apps/slate-windows/src/SlateWindows/Search/SearchRecentsStore.cs','apps/slate-windows/src/SlateWindows/SidebarSettingsStore.cs','apps/slate-windows/src/SlateWindows/Graph/GraphConfigStore.cs','apps/slate-windows/src/SlateWindows/Graph/GraphConfigWriter.cs','apps/slate-windows/src/SlateWindows/Graph/GraphPreferencesViewModel.cs','apps/slate-windows/src/SlateWindows/Graph/WorkspaceViewModel.Graph.cs','apps/slate-windows/src/SlateWindows/Search/VaultSearchSource.cs','apps/slate-windows/src/SlateWindows/WorkspaceViewModel.cs','apps/slate-windows/src/SlateWindows/VaultRootAnchor.cs','apps/slate-windows/tests/SlateWindows.Tests/TestRootAnchors.cs','apps/slate-windows/tests/SlateWindows.Tests/GraphConfigWriterTests.cs','apps/slate-windows/tests/SlateWindows.Tests/W1AnchoredVaultStoreTests.cs','apps/slate-windows/tests/SlateWindows.Tests/FileRecentsStoreTests.cs','apps/slate-windows/tests/SlateWindows.Tests/VaultOpenIdentityTests.cs','apps/slate-windows/tests/SlateWindows.Tests/RescanTests.cs')
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs; Assert-Ok 'dotnet format'
python -I scripts/generate-parity-matrix.py --check; Assert-Ok 'parity matrix'
cargo bench -p slate-core --bench scan_bench -- 'first_open_and_scan|reopen_with_cache'; Assert-Ok 'scan benches'   # one regex filter; both groups within noise of BENCHMARKS.md

# gated (PR 1), in the elevated session where WB-27's volume procedure set $fatRoot and $exfatRoot:
if (-not $fatRoot -or -not $exfatRoot) { throw 'run WB-27''s volume procedure in this session first' }
$env:SLATE_WEAK_VOLUME_FAT32 = $fatRoot; $env:SLATE_WEAK_VOLUME_EXFAT = $exfatRoot
$weak = 'a_vault_on_a_fat32_or_exfat_volume_opens_with_the_identity_its_file_system_answers'
Assert-Facts 'slate-core' $weak @($weak) -Ignored
cargo test -p slate-core --locked $weak -- --ignored --test-threads=1; Assert-Ok $weak   # both volumes; record the pass in PR 1's body
```

PR 1 runs step 0 first, then the block with every core and uniffi fact
(the four seams, the fallback gate, the colliding-index fact and
`duplicate_root_handle` are all PR 1's), the host list cut to
`AFolderWhoseFileSystemGivesNoFileIdDoesNotOpenAndSaysWhy` and
`AWeakIdentityKeepsThePathKeyAndMigratesNothing` (its filter cut to
`VaultOpenIdentityTests` and `FileRecentsStoreTests`), the two `git grep`
checks and the files PR 2 adds (`VaultRootAnchor.cs`,
`TestRootAnchors.cs`) left out, and the gated FAT32/exFAT fact; the
other host facts are PR 2's, which changes no Rust. Mutation run per row,
locally. CI: `windows.yml`
(all lanes), `rust.yml` (the Unix facts, unchanged, on Linux),
`swift-tests.yml` (the record change at `AppState.swift:10670-10671`
and `SidebarOrganizationDispatchTests.swift:2478-2479`, and the two
additive exports; the mac twins of the 22a facts).

AT, NVDA spot check on PR 1 (§4 rule 4), with the Release build and
NVDA running. Device state is isolated through WB-1's field-pass
launcher, `scripts/windows/launch-for-field-pass.ps1`
(`[CmdletBinding(PositionalBinding = $false)] param([string]$Vault,
[switch]$Seed, [switch]$Keep, [string]$Exe, [string]$Root)`, several vaults
joined by `|` in one `-Vault` string because `pwsh -File` cannot bind an
array: without `-Root` it makes a fresh
`%TEMP%\slate-field-pass-<stamp>` root, sets `SLATE_LOCAL_APP_DATA` and
`SLATE_LOG_DIR` beneath it, with `-Seed` writes a `recent-vaults.json`
holding the given vaults there, starts `$Exe` with the first `$Vault` as
its argument, prints `Root: <path>` and keeps the root with `-Keep`;
variables set in the calling window reach the app), so neither
the open nor the recents touch the owner's profile, and the seed replaces
opening the vault once by hand. The launcher's child inherits the
window's environment, so the seam set first reaches the app:

```powershell
$exe = (Resolve-Path 'apps/slate-windows/src/SlateWindows/bin/Release/net10.0-windows/SlateWindows.exe').Path
$scratch = Join-Path ([IO.Path]::GetTempPath()) "wb28-no-file-id-$PID"; New-Item -ItemType Directory (Join-Path $scratch 'Notes') -Force | Out-Null
Set-Content (Join-Path $scratch 'Notes\a.md') '# A'
$other = Join-Path ([IO.Path]::GetTempPath()) "wb28-other-vault-$PID"; New-Item -ItemType Directory $other -Force | Out-Null   # its path does not contain the seam's value
$env:SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE = Split-Path $scratch -Leaf
pwsh -NoProfile -File scripts/windows/launch-for-field-pass.ps1 -Vault $scratch -Seed -Keep -Exe $exe
if ($LASTEXITCODE -ne 0) { throw "field-pass launcher failed ($LASTEXITCODE)" }
Remove-Item Env:SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE
```

1. At launch the app opens `$scratch` (the launcher's argument): NVDA
   speaks once "Could not open vault: This folder's file system gives
   Slate no file identity for it, so Slate cannot keep the vault anchored
   to it." The keys stay in the welcome view (WD4), and nothing else is
   spoken.
2. Open the same vault from the welcome view's recents (the seed put it
   there): the same sentence, once, and the keys stay in the welcome
   view.
3. In the same run, open `$other` through Open Folder. It opens and is
   announced as usual. Close Slate; the launcher returns and the command
   above checks its exit code.

The PR body records the NVDA log excerpt, and the PR stays draft until
then. Nothing else a reader hears changes.

## Acceptance

- [ ] Every Open design item (Codex round 12) is closed in the reviewed design, with its fact, before code starts.
- [ ] `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED`,
      `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE`,
      `SLATE_TEST_ROOT_FILE_ID_INFO_FAILS` and
      `SLATE_TEST_ROOT_FILE_INDEX_COLLIDES` join WB-18's `FAULT_VARIABLES`
      (`session/tests/common.rs`, the array grows by four) and its guard
      fact's literal list in PR 1, and every core fact that sets any of
      them does so through `fault_guard()`, so WB-18's
      `no_test_sets_a_fault_variable_outside_the_guard` and
      `the_fault_guard_clears_every_fault_variable_on_acquisition` stay
      green (WB-30 then finds the two names it uses present).
- [ ] WD7 and WD22 recorded; WD24 (answered 2026-10-21, amended
      2026-10-21) applied; step 0's probe run before any code, its table
      in PR 1's body and the matrix, and — if FAT32 or exFAT answered
      `FileIdInfo` — the header's note that the fallback is a defensive
      path; the design section's answers (items 0 to 10) recorded; 22c
      merged and its names adopted.
- [ ] `FsRoot`, `sys`, the anchored SQLite hook (or the owner-approved
      fallback) and the provider's reads on Windows.
- [ ] No Windows obligation rests on renaming a held object: the 22a
      facts that do (`descriptor_interior.rs`, `fs_root.rs`'s three
      Unix facts) stay `cfg(unix)` and green on the rust and mac lanes,
      and their Windows twins — the pinning facts and the
      retargeted-spelling facts — are discovered by name and green on the
      Windows lane.
- [ ] `VaultRootIdentity { volume, id_high, id_low, persistent }`
      lossless on both bindings; the high-bits fact; both Swift reads
      (`AppState.swift:10670-10671`,
      `SidebarOrganizationDispatchTests.swift:2478-2479`) changed.
- [ ] No identity and no path authorizes a store (codex round 11):
      `AnchoredVaultStore.Open(VaultRootAnchor root, …)` opens `.slate`
      and its children relative to a duplicate of the session's held root
      (core's `duplicate_root_handle`, PR 1), the five stores and the
      graph writer take the anchor, every production construction site
      passes `VaultRootAnchor.Of(session)` (the `git grep` checks), the
      graph writer pins it at `Enqueue`, a disposed session's anchor
      refuses, and a store given no anchor refuses outside the test
      assembly — `OpenReachesTheHeldRootWhenTheVaultsSpellingIsRetargeted`,
      `EveryStoreWritesBeneathTheHeldRootAfterTheSpellingIsRetargeted`,
      `ARootForcedToAnotherVaultsWeakIndexIsNeverReachedThroughIt`,
      `AStoreRefusesOnceItsSessionIsDisposed`,
      `AStoreGivenNoAnchorRefusesOutsideTheTestAssembly` and
      `GraphConfigWriterTests.AQueuedWriteLandsInTheRootItWasEditedIn`
      green, with the core collision fact
      `two_roots_forced_to_one_weak_index_keep_their_own_interiors`.
- [ ] `OpenVaultAsync` fails closed on a null identity;
      `FileRecentsStore` migrated for a persistent identity and
      path-keyed, migrating nothing, for a weak one (PR 1 keeps the path
      key for every identity); a migration source — the path-keyed file
      or the in-vault file — is deleted only after its successful read
      and `TryCommit`'s confirmed destination commit, and an interrupted
      migration of either source completes on a later load even after an
      `Add` created the destination meanwhile, merging the destination's
      newer entries first; a failed in-vault commit is retried by the same
      store instance; `Clear` drops an unmigrated in-vault file (the seven
      item-7 facts: `AFailedDestinationCommitKeepsThePathKeyedFile`,
      `AnUnreadablePathKeyedFileIsNeitherMigratedNorDeleted`,
      `AnInterruptedMigrationCompletesOnALaterLoad`,
      `AnUnreadableInVaultRecentsFileIsNotDeleted`,
      `AnInVaultRecentsFileUnreadableBeforeAnAddMigratesOnALaterLoad`,
      `AFailedInVaultCommitIsRetriedAfterTheDestinationExists`,
      `ClearingRecentsDropsAnUnmigratedInVaultFile`).
- [ ] WD24 as amended, narrowed by codex round 11: the root's and
      `.slate`'s identity is `FileIdInfo`, else — only on a volume whose
      file system is `FAT`, `FAT32` or `exFAT` — the
      `GetFileInformationByHandle` file index flagged weak, so FAT32 and
      exFAT vaults open (the seam fact on the lane, the gated fact on
      WB-27's VHDs, the decision table
      `the_file_index_fallback_is_taken_only_on_fat_fat32_and_exfat`). A
      `FileIdInfo` failure on NTFS, ReFS or any other file system refuses
      the open without reading the index, one attempt and no retry
      (`a_file_id_info_failure_on_a_volume_that_keeps_ids_refuses_the_open_and_never_reads_the_index`),
      and a FAT-family root for which neither read answers does not open:
      core refuses in `FsRoot::open` before `.slate` is made, with
      `VAULT_ROOT_IDENTITY_UNAVAILABLE` as the `VaultError::Io` detail. The host speaks core's detail on every
      failed open, never the binding's field dump. `OpenVaultAsync`
      refuses a null identity with the same sentence through
      `vault_root_identity_unavailable_detail()` and admits a weak one.
      The core, uniffi and host facts are green, and PR 1's NVDA spot
      check is recorded.
- [ ] Benches within noise; registers appended. PR 1's body carries
      `Refs #1475` and `Refs #940` and no closing keyword; PR 2's body
      carries `Closes #1475` and `Refs #940`, and PR 2 merges only after
      PR 1 is merged and both deliveries are verified.
- [ ] Not delivered: anything the SQLite spike forced onto the fallback.

## Brief

Every Open design item (Codex round 12) is closed in the reviewed design, with its fact, before code starts.
(They are the spec's section of that name: the graph writer's
`Newest` and its pending state scoped by path, and the search-recents
store cached by spelling across a same-path reopen.)

Deliver 22W. Pending decisions: WD7 (recommended: the identity half is
22W, here; under the other answer — identity parked with Milestone W —
only the `FsRoot` body ships and the host admission stays text-based) and
WD22 (recommended: widen the record to `{ volume, id_high, id_low,
persistent }` on both bindings and migrate the recents key on first open
for a persistent identity; under the other answer the 128-bit id is
hashed into `inode`, the record keeps `device` and `inode` and gains only
`persistent`, the high-bits fact asserts the hash, the recents key is
unchanged and no Swift line changes; under "the owner records a one-time
reset" the path-keyed migration step is dropped). Answered: WD24
(2026-10-21; amended 2026-10-21) — FAT32 and exFAT vaults keep opening:
the root's and `.slate`'s identity is read with
`GetFileInformationByHandleEx(FileIdInfo)`, else with
`GetFileInformationByHandle` (volume serial and 64-bit file index, the
read the watcher already makes), and the fallback identity is flagged
weak because a FAT index changes when its entry is renamed or moved; a
Windows vault does not open only when neither read answers, and
`OpenVaultAsync` refuses a null root identity with a sentence saying
why; admitting an id-less root by handle and final path with path-keyed
stores is rejected. Codex round 11 narrows how this applies, keeping the
owner's choice that FAT32 and exFAT vaults open weak: the fallback is
taken only on a volume whose file system `GetVolumeInformationByHandleW`
names `FAT`, `FAT32` or `exFAT`; on NTFS, ReFS or any other file system a
`FileIdInfo` failure refuses the open (ReFS's 64-bit index is not unique
— `SidebarWriteJournal.cs:58` and Win32's `BY_HANDLE_FILE_INFORMATION`
remarks); and a weak identity authorizes nothing, because two folders'
weak identities can be equal (two FAT volumes imaged from one another
share the 32-bit serial and every entry's place), so the host's vault
stores reach the vault through a duplicate of the session's held root
handle and compare no identity. The plan's WD24 rows get a dated note.
Read the design `:306-385`, `:639-711`,
`:737-845`; `fs_root.rs:49-60`, `:142-170`, `:285-420`, `:536-700`,
`:749-1018`, `:1020-1140` (its tests); `db/anchored.rs:1-60`,
`:100-170`, `:260-280`, `:400-420`; `session.rs:1085-1120`,
`:1440-1452`, `:1600`, `:2624-2645`, `:3138-3160`, `:3196-3260`, and
the test module list `:22300-22305`; `watch.rs:104-108` (session) and
`vault/watch.rs:67-74`, `:279-307` (the watcher's
`GetFileInformationByHandle` root identity, the fallback's model);
`windows_entry.rs:185-240`, `:378-410`, `:555-635` (`identity`, the
`FileIdInfo`-only read at `:612-631`);
`SidebarWriteJournal.cs:54-76` (FAT32 answers no `FileIdInfo`; ReFS's
64-bit index is not unique); WB-27's spec (its volume procedure and
matrix block);
`vault/test_support.rs:58-86` (`junction`); `trash_handoff_tests.rs:557-572`
(a junction vault root, the junction removed while it works);
`uniffi lib.rs:618-630`, `:770-795`; `descriptor_interior.rs` whole
(every fact but `:293` renames a held object; several use
`std::os::unix`); `AnchoredVaultStore.cs:42-120` (`Open` by path, the
`.slate` made by path at `:72-75`), `:245-363` (`TryOpenDirectory`
refusing a reparse-point root, `TryOpenChild` by path, the final-path
text checks), `:434-456`; the stores built from path strings —
`WorkspacePersistence.cs:92` (built at `VaultLifecycleViewModel.cs:1182`,
`WorkspaceViewModel.cs:2119`), `Search/SearchRecentsStore.cs:67` (built at
`Search/VaultSearchSource.cs:96`, which holds `Func<VaultSession?>` at
`:36`), `SidebarSettingsStore.cs:45` (built at
`FilesSidebarViewModel.cs:556`), `Graph/GraphConfigStore.cs:40` (built at
`Graph/GraphPreferencesViewModel.cs:82-101`, itself built at
`Graph/WorkspaceViewModel.Graph.cs:224`) and the process-wide
`Graph/GraphConfigWriter.cs:30-140` (`StoreFor` `:65`, `Enqueue`, the
queued write `:132`, which nothing drains at close); the close order
`VaultLifecycleViewModel.cs:1095-1124` (the workspace disposed before the
session); `session.rs:60-170` (the structural-lock registry:
`StructuralLockKey` `:71`, `open_file` `:86-91`, `ProcessStructuralLocks`
`:109-121`, the lock order `:124-127`) and `:3144-3152` (`root_display_path`,
display only); `session/tests/structural_batch.rs:976` (a session over a
test provider no filesystem root backs); `FileRecentsStore.cs:15-171` (`Load`'s
destination-absent gate `:41-44`, `Add` = `Load` then `Save` `:49-58`,
`Clear` and `Replace` `:60-67`, `MigrateLegacy` `:69-105`, `Read`
`:107-125`, the `void` `Save` that swallows its errors `:127-148`);
`FilesSidebarViewModel.cs:557` and its `Replace` calls `:2827` and
`FilesSidebarViewModel.WriteJournal.cs:343`; `QuickSwitcherViewModel.cs:68`
and its `Clear` call `:236`; `HostLog.cs:212` (`ObserverForTests`);
`CanvasDocumentTests.cs:4131-4134` (`mklink /J` in a C# fixture);
`AppState.swift:10665-10675` and
`SidebarOrganizationDispatchTests.swift:2468-2480` (the only two Swift
reads of the FFI record's fields; no Swift line constructs it); for
WD24: `fs_root.rs:588-605` (the Unix
open refuses an `fstat` failure before `.slate` is made),
`VaultLifecycleViewModel.cs:436-575` (the open, and the failure catch
at `:556-565` that speaks `exception.Message`, the binding's "@message=…"
dump) and `:1870-1875` (`ReportTerminalStatus`),
`WorkspaceViewModel.cs:1130-1137` (`VaultErrorDetail` in use),
`uniffi lib.rs:11320-11372` (`vault_error_detail`), `a11y.rs:3401-3440`,
`session.rs:4365-4390` (the `SLATE_TEST_*` seams any build can trip);
WB-1's spec for `scripts/windows/launch-for-field-pass.ps1` (the NVDA
spot check's launcher). Settle the design answers (items 0 to 10) first.

Step 0, before any code: in the elevated session where WB-27's volume
procedure mounted the FAT32 and exFAT VHDs (`$fatRoot`, `$exfatRoot`),
run the probe script in this spec's Verification (an `Add-Type` of
`GetFileInformationByHandleEx(FileIdInfo)` and
`GetFileInformationByHandle` over a folder opened with
`FILE_FLAG_BACKUP_SEMANTICS`, plus `GetVolumeInformationByHandleW` for
the file-system name; an NTFS temp folder as control). Per volume it
records the file-system name, which read answers (or its Win32 error),
the index read twice and a sibling's index, and both reads after a
longer rename. It throws if the NTFS control answers no `FileIdInfo`, if
a volume answers neither read, if a volume that needs the fallback
reports a name other than `FAT`, `FAT32` or `exFAT` (the gate would
refuse its vaults), or if a volume that needs the fallback gives an
index that is not stable and distinct within the mount; the last three
stop the chunk for an owner decision. The table goes into PR 1's body
and, dated, into the §5 Windows block WB-27 appended to
`24_structural_identity_capability_matrix.md`. If FAT32 or exFAT
answers `FileIdInfo`, the fallback stays as a defensive path, proven by
the seam fact alone, and the header records that. If the `FileIdInfo`
error is not `ERROR_INVALID_PARAMETER`, the seams use the recorded code.

Build: the Windows `fs_root::sys` (same names as Unix: `open_at`,
`stat_at`, `read_dir_names`, `rename_at`, `unlink_at`, `ensure_dir_at`,
`identity`, `open_or_make_slate`), the Windows `FsRoot` body holding root
and `.slate` without `FILE_SHARE_DELETE`, `db/anchored.rs`'s win-VFS body
plus `skip_install_for_tests()` (new), `OpenSwapPoint::BeforeSlateHeld`
(new), provider reads through handles, and the lock registry keyed by
the held `.slate`'s whole identity — a weak key can collide with another
vault's (cloned FAT volumes), which only makes the two sessions take
turns at the in-process gate (`session.rs:109-127`), because each still
opens and locks `structural.lock` beneath its own held `.slate`
(`:86-91`); and `root_display_path()`'s identity gate stays display
only, nothing resolving through it (`session.rs:3150-3151`), so no
identity comparison anywhere grants access. The record, core and uniffi:
`VaultRootIdentity { volume: u64, id_high: u64, id_low: u64,
persistent: bool }` (new fields; equality covers all four), with
`FsRoot::identity() -> Option<VaultRootIdentity>` (the Unix body wraps
`fstat` as `{ volume: dev, id_high: 0, id_low: ino, persistent: true }`).
The Windows identity, in PR 1: `sys::identity(file) ->
io::Result<VaultRootIdentity>` calls the new private
`sys::file_id_info(file)` (`FileIdInfo`: the 64-bit volume serial, the
128-bit id, `persistent: true`); when that fails it reads the new private
`sys::file_system_name(file)` (`GetVolumeInformationByHandleW`) and,
**only when the name is `FAT`, `FAT32` or `exFAT`** (ASCII
case-insensitive), logs the `FileIdInfo` error at debug level and calls
the new private `sys::file_index(file)` (`GetFileInformationByHandle`:
`dwVolumeSerialNumber` zero-extended, `id_high = 0`, `id_low =
nFileIndexHigh << 32 | nFileIndexLow`, `persistent: false`, the
watcher's pair), failing only when that fails too. On any other name
(NTFS, ReFS, a name a share's server reports) or an unreadable name it
fails with the `FileIdInfo` error and never reads the index: a refusal,
not a retry — one attempt, the open fails with the sentence below, and
the reader can open again. The decision is the new `pub(super)` pure
`sys::identity_from_reads(file_id_info: io::Result<VaultRootIdentity>,
file_system_name: impl FnOnce() -> io::Result<String>, file_index: impl
FnOnce() -> io::Result<VaultRootIdentity>) ->
io::Result<VaultRootIdentity>`, which `sys::identity` applies to the
three reads. A consequence the PR body states: a share whose server
reports NTFS or ReFS but answers no `FileIdInfo` now refuses rather than
opening weak.
Structural identity (`windows_entry::identity`) and the Trash stamp stay
`FileIdInfo`-only, as on main. WD24, in PR 1: the Windows `FsRoot::open`
calls `sys::identity` on the held root right after the hold and before
`open_or_make_slate`, and only when it fails refuses the open with
`VaultError::Io(io::Error::new(kind, a11y::VAULT_ROOT_IDENTITY_UNAVAILABLE))`.
The constant is new, core's copy: "This folder's file system gives Slate
no file identity for it, so Slate cannot keep the vault anchored to
it." The OS error is logged, not spoken, and no `.slate` is made. The
held `.slate`'s identity is read the same way and refuses in the same
cases. Also new in PR 1:
- Four seams any build can trip, consulted by the Windows
  `FsRoot::open` for the root's and `.slate`'s reads when the opened
  root's path contains the value, each faking only what `sys::identity`
  reads (no other code, WB-30's identity mode included, sees a faked
  name): `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` presents a FAT32
  volume — the name reads `FAT32` and `FileIdInfo` fails with
  `ERROR_INVALID_PARAMETER` — so the fallback answers and the session
  opens weak; `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE` is that profile with
  the file-index read failing too, so the open refuses;
  `SLATE_TEST_ROOT_FILE_ID_INFO_FAILS` fails only `FileIdInfo` (with
  `ERROR_INVALID_PARAMETER`, FAT32's own code) under the volume's real
  name — the transient NTFS failure — so the open refuses without reading
  the index; `SLATE_TEST_ROOT_FILE_INDEX_COLLIDES` makes the file-index
  read return the real serial with the fixed index
  `0x5EED_0000_5EED_0001`, so every root and `.slate` under the folder
  named collide (with `…_FILE_ID_INFO_UNSUPPORTED` on the same folder,
  two vaults there open with equal weak identities). All four join WB-18's
  `FAULT_VARIABLES`.
- `VaultSession::duplicate_root_handle(&self) -> Result<OwnedHandle,
  VaultError>` (`#[cfg(windows)]`) over the new
  `FsRoot::duplicate_root() -> io::Result<OwnedHandle>`
  (`OwnedHandle::try_clone` of the held root: `DuplicateHandle` with
  `DUPLICATE_SAME_ACCESS`, not inheritable, so it names the held directory
  object whatever any spelling names now); a session no filesystem root
  backs returns `VaultError::Unsupported`. Its additive uniffi export
  `VaultSession.duplicate_root_handle() -> Result<u64, VaultError>` hands
  the duplicate's raw value to the caller, who owns it, and returns
  `Unsupported` off Windows (Swift never calls it).
- The additive export `vault_root_identity_unavailable_detail() ->
  String`, which returns the constant.
- The host's open-failure catch (`VaultLifecycleViewModel.cs:556-565`)
  speaks `"Could not open vault: " + SlateUniffiMethods.VaultErrorDetail(exception)`
  instead of `exception.Message`.
- The record's field reads: `AppState.swift:10670-10671` maps
  `SidebarVaultRootIdentity(device: $0.volume, inode: $0.idLow)`;
  `SidebarOrganizationDispatchTests.swift:2478-2479` reads
  `anchor.volume`/`anchor.idLow`; `FileRecentsStore.cs:30` stops reading
  `Device`/`Inode` and keys every identity by the path until PR 2, so no
  recents file is orphaned between the PRs.

The refusal is a `VaultError` detail, not a new `A11yEvent`. It must
happen in core, before `.slate` and the identity-keyed lock exist, so it
arrives as a failed `OpenFilesystem`. An event would need a new
`VaultError` variant on both bindings. `Io`'s message already crosses
verbatim. So there is no corpus, census, designations or ledger change,
and the refusal needs no mac edit (the record's two Swift reads above
are the only mac lines this chunk changes).

Then in PR 2 (host only): no identity and no path authorizes a store;
the capability is the root handle the session holds. New
`VaultRootAnchor` (`internal sealed class : IDisposable`,
`VaultRootAnchor.cs`): `Of(VaultSession session)` — each `OpenRoot()`
asks core's `DuplicateRootHandle()` for a fresh duplicate and wraps it as
`new SafeFileHandle((nint)value, ownsHandle: true)`, turning a
`VaultException` or the binding's `ObjectDisposedException` into
`IOException`; `OpenRoot() -> SafeFileHandle` (the caller owns it);
`Pin()` — an anchor owning one duplicate taken now, handing out
duplicates of it (kernel32 `DuplicateHandle`) until disposed;
`ByPathForTests(string vaultRoot)` — main's by-path open, which throws
`IOException` unless `internal static bool ByPathAllowed` (default
`false`) is set, which only the test assembly's `[ModuleInitializer]` in
the new `tests/SlateWindows.Tests/TestRootAnchors.cs` does.
`AnchoredVaultStore.Open(VaultRootAnchor root, bool createDirectory,
Action? afterDirectoryAnchored = null, Action? beforeRename = null,
Action? afterRename = null)` replaces the path form: it takes
`root.OpenRoot()` as the vault handle, opens `.slate` relative to it
(`NtCreateFile`, `RootDirectory` = that handle, `FILE_OPEN` or
`FILE_OPEN_IF`, `FILE_DIRECTORY_FILE | FILE_OPEN_REPARSE_POINT`) and every
child relative to the `.slate` handle; `EnsureAttributes` still refuses a
reparse point; the final-path text checks (`VerifyDirectoryIdentity`,
`EnsureRegularChild`'s comparison, `FinalPath`) go, since nothing is
resolved by path. The five stores (`WorkspacePersistence`,
`SearchRecentsStore`, `SidebarSettingsStore`, `GraphConfigStore`'s `Read`
and `Write`, `FileRecentsStore`'s in-vault step) and
`GraphPreferencesViewModel` gain an optional last parameter
`VaultRootAnchor? root = null` (`null` → `ByPathForTests`, which refuses
in production), and every production site passes
`VaultRootAnchor.Of(session)`: `FilesSidebarViewModel.cs:556-557`,
`QuickSwitcherViewModel.cs:68`, `Search/VaultSearchSource.cs:96`,
`VaultLifecycleViewModel.cs:1182`, `WorkspaceViewModel.cs:2119`,
`Graph/WorkspaceViewModel.Graph.cs:224`. The graph writer's queued write
can outlive the session, so `GraphConfigWriter.Enqueue(string key,
GraphConfig aggregate, ulong generation, VaultRootAnchor? root = null)`
keeps `root?.Pin()` with the admitted aggregate, the write goes through
`StoreFor(key).Write(aggregate, pinned)`, and the pin is disposed after
the write (or at once for a dropped aggregate): a straggler lands in the
root it was edited in, never in what a later session or spelling names.
A vault opened through a junction spelling gets working stores (main's
refuse it). `OpenVaultAsync` refuses fail-closed: it reads the identity through
the new internal seam `RootIdentityReader`
(`Func<VaultSession, VaultRootIdentity?>`) before `AddRecentVault` and
`VaultOpened`. On Windows a null identity disposes the session and
speaks `"Could not open vault: " + SlateUniffiMethods.VaultRootIdentityUnavailableDetail()`
through the same terminal path: no `VaultOpened`, no recents entry,
`ReturnedToWelcome`; a weak identity opens. Last, `FileRecentsStore`:
a persistent identity gets the identity key v2 (SHA-256 of
`{Volume:x16}-{IdHigh:x16}{IdLow:x16}`) with the path-key migration; a
weak identity keeps today's path key and moves and deletes no
path-keyed file, because a FAT index changes on a rename or move made
with Slate closed, a freed slot's index can be reused and a cloned
volume repeats it, so it can neither follow the vault nor keep two
vaults apart, and the
path key is what every Windows vault uses on main. The migration deletes
its source only after a successful read of it and a confirmed atomic
commit of the destination, because on main `Save`
(`FileRecentsStore.cs:127-148`) returns `void` and swallows
`IOException`/`UnauthorizedAccessException`, and `Read` (`:107-125`)
turns a read error into an empty list. New private helpers:
`TryRead(path) -> RecentsRead` (`Absent`, `Read(entries)`,
`Unreadable(error)`; `Read` keeps its empty-list contract for the live
file) and `TryCommit(entries) -> bool` (a temporary file flushed with
`Flush(flushToDisk: true)`, one `File.Move(temporary, _path, overwrite:
true)`, then a read-back that must equal `entries`; failures log
`FileRecentsPersistFailed` and return `false`; `Save` becomes `_ =
TryCommit(Sanitize(paths))`). `Load` migrates whenever the path-keyed
file (`_pathKeyedPath`, new field) exists and differs from `_path`: an
`Unreadable` source or destination stops it with nothing written or
deleted; otherwise it commits the destination's entries followed by the
source's (`Sanitize`, cap 50) and deletes the source only when
`TryCommit` returned `true` — a failed commit keeps the source and
returns the merged list for this session, and the next load retries,
even after an `Add` created the destination meanwhile. The in-vault
step (`MigrateLegacy`, the old `.slate/file-recents.json`) is retried the
same way for every identity, persistent or weak, also after the
destination exists — on main it runs only while the destination is
absent (`Load`, `:41-44`), and `Add` is `Load` then `Save` (`:49-58`), so
an in-vault file unreadable at the first load followed by an `Add` would
be skipped for good. It runs after the path-key migration, once that
has no source left: the anchored read (`AnchoredVaultStore.Open(_root,
createDirectory: false)` — `_root` the store's new constructor argument
`VaultRootAnchor? root = null`, so the read reaches the session's held
root — then `ReadAllBytesBounded("file-recents.json", …)`; no `.slate`
directory or no file is `Absent`, an `IOException` — an anchor whose
session has gone included —
`UnauthorizedAccessException` or `JsonException` is `Unreadable`), then
`TryRead(_path)`; an `Unreadable` source or destination stops it with
nothing written or deleted; otherwise it commits the destination's
entries followed by the in-vault entries (`Sanitize`, cap 50) and
deletes the in-vault file through the anchored store only when
`TryCommit` returned `true` (a failed commit keeps it, returns the
merged list for that call, and the next `Load` retries). A new private
`bool _legacySettled` records an `Absent` read or a confirmed commit
with its delete attempted, so a settled instance stops opening the
vault's store; a new internal counter `LegacyProbesForTests` counts the
step's anchored opens. `Replace` (the sidebar's rename reconciliation)
writes a list that never held the in-vault entries, so they still merge
behind it later; `Clear` (Quick Open's Clear recents) on an unsettled
instance deletes the in-vault file through the anchored store after its
own confirmed commit and settles the step even if that delete fails (a
file it could not remove is merged by a later session — the one way a
cleared list regains entries). Main instead reads an unreadable in-vault
file as empty, saves an empty destination and deletes the file. Tests: `session/tests/descriptor_interior_windows.rs` (new,
`#[cfg(windows)] mod descriptor_interior_windows;` in the session test
list) with helpers `retarget(link, to)` (remove the junction, recreate it
pointing at `to`), `tree_bytes(root)`, `file_index_of(path)` (the
folder's `dwVolumeSerialNumber` and 64-bit file index),
`file_id_info_answers(path)` (whether `FileIdInfo` succeeds),
`file_system_name_of(path)` (`GetVolumeInformationW` of the path's
volume) and `file_id_info_of_handle(handle)` (the `FILE_ID_INFO` of an
open handle, to say which folder a duplicated root names), all new.

**The Unix live-rename facts are not Windows obligations.**
`descriptor_interior.rs` keeps `#![cfg(unix)]` and `fs_root.rs`'s
`a_held_cache_directory_follows_its_folder_and_never_recreates_the_old_path`,
`a_held_cache_directory_never_follows_a_link` and
`racing_creates_beneath_a_held_directory_all_open_the_one_file` keep
`cfg(any(macos, linux))`: they rename a held root, `.slate` or cache
directory (which the Windows hold refuses by design) or use
`std::os::unix`. On Windows their obligations are carried by these facts,
each seen failing first under its mutation:
- `fs_root.rs` `both_forms_open_create_rename_list_and_remove_the_same_files`
  (existing, uncfg'd): now through the Windows handle body [route
  `CacheDir` by path].
- `windows_a_held_cache_directory_refuses_a_rename_of_itself_and_of_its_parent`:
  renames fail while held, succeed after drop, old path absent [share
  delete].
- `windows_a_held_cache_directory_resolves_beneath_its_handle_when_its_spelling_is_retargeted`:
  opened through a junction, junction retargeted, creates land in the
  original, decoy empty [resolve by `display()`].
- `windows_a_held_cache_directory_never_follows_a_junction`: an `oplog`
  junction refuses open and listing; target empty [follow surrogates].
- `a_held_root_refuses_a_rename_of_itself_and_of_an_ancestor_while_the_session_lives`
  (pinning twin of `:75`, `:142`, `:181`): `MoveFileExW` of root and
  parent fails with a sharing or access error, succeeds after drop, old
  path empty [share delete].
- `no_interior_writer_follows_a_retargeted_spelling` (twin of `:618`):
  after `retarget(outer\vault, B)` a save returns `Ok`, and the
  retention change, op-log append, staged create, structural lock and a
  worker connection's insert all land in `A`; `tree_bytes(B)` unchanged
  [one writer through `InteriorDir for Path`].
- `wal_connections_share_the_held_cache_across_a_retarget` (twin of
  `:229`): WAL mode, a worker connection shares rows, `-wal`/`-shm` in
  `A\.slate`; after the retarget a new connection reads every row and `B`
  gains no cache file [open `-wal`/`-shm` by path].
- `a_second_process_on_the_textual_path_shares_the_anchored_cache` with
  its child (twins of `:293`/`:332`): the child sees `P0` and writes
  `C1`; the parent's snapshot holds until `COMMIT` [open exclusively].
- `root_display_path_follows_the_held_root_not_its_retargeted_spelling`
  (twin of `:424`): identity equals the anchor before and after; after,
  `A`'s final path, never the junction [drop the identity gate].
- `canonical_path_is_computed_under_the_held_root_handle` (twin of the
  mac-only `:670`): after the retarget `Notes/Alpha.md` resolves,
  `Notes/Beta.md` does not, `notes/alpha.md` aliases, a junctioned
  `Linked/x.md` is `None` [compute by path].
- `a_junctioned_interior_folder_is_refused` (twin of `:482`, directory
  junctions only): `.slate\oplog` and `.slate\tmp` junctions refuse with
  the typed refusal; their targets stay empty [follow the junction].
- `the_connection_names_the_synthetic_path_and_the_literal_path_does_not_exist`:
  `sqlite3_db_filename` starts `\\.slate-fd\`; `CreateFileW` of it is
  `ERROR_PATH_NOT_FOUND`; a row round-trips [open by path].
- `an_uninstalled_hook_fails_the_anchored_open_instead_of_opening_by_path`:
  with `skip_install_for_tests()` `open_in` is `Err` and no
  `cache.sqlite` is written by path [fall back silently].
- `a_slate_junction_planted_before_the_hold_never_receives_the_interior`:
  at `BeforeSlateHeld` a `.slate` junction makes the open refuse and the
  outside folder stays empty [follow the junction].
- `root_identity_on_windows_is_the_volume_serial_and_the_whole_128_bit_file_id`:
  the record equals the held root's `FILE_ID_INFO` with `persistent ==
  true`; a high-bits synthetic id round-trips [truncate to 64 bits; read
  the file index first].
- `fs_root.rs` `the_file_index_fallback_is_taken_only_on_fat_fat32_and_exfat`
  (`#[cfg(windows)]`, a unit fact over `sys::identity_from_reads`, PR 1):
  with `FileIdInfo` failing (`ERROR_INVALID_PARAMETER`) and the file index
  answering, the names `FAT`, `FAT32`, `exFAT` and `fat32` give a weak
  identity; `NTFS`, `ReFS`, `""`, `ext4` and an unreadable name give the
  `FileIdInfo` error with the index closure never called; `FAT32` with
  the index failing gives the index's error; a `FileIdInfo` that answers
  gives a persistent identity, neither closure called [fall back after
  any failure; admit `ReFS`; key on the error code instead of the name;
  compare case-sensitively].
- `a_file_id_info_failure_on_a_volume_that_keeps_ids_refuses_the_open_and_never_reads_the_index`
  (PR 1, the transient NTFS failure): with
  `SLATE_TEST_ROOT_FILE_ID_INFO_FAILS` set to a unique temp folder `T`'s
  name through `fault_guard()`, after asserting `file_system_name_of(T\vault)`
  is `NTFS` or `ReFS` and `file_index_of(T\vault)` answers,
  `from_filesystem(T\vault)` is `Err(VaultError::Io)` with detail
  `VAULT_ROOT_IDENTITY_UNAVAILABLE`, no `.slate` is made, the folder
  renames right after, and a sibling vault outside `T` opens persistent
  [fall back after any failure; key the fallback on the error code].
- `two_roots_forced_to_one_weak_index_keep_their_own_interiors` (PR 1,
  the synthetic collision's core half): with
  `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` and
  `SLATE_TEST_ROOT_FILE_INDEX_COLLIDES` set to `T` through
  `fault_guard()`, session `a` through the junction `T\vault` → `T\a` and
  session `b` at `T\b` have equal weak identities (asserted); after
  `retarget(T\vault, T\b)` and `b`'s own save, `a`'s save, op-log
  append, staged create, structural lock and worker insert land in `T\a`
  and `T\b`'s tree is unchanged; `a.root_display_path()` is `T\a`'s final
  path; each session's `structural.lock` is beneath its own `.slate`
  (they take turns at the shared slot); each `duplicate_root_handle()`
  names its own root [display path from `opened_as` gated by the weak
  identity; one interior writer by the spelling; duplicate by reopening
  `opened_as`].
- `the_duplicated_root_handle_names_the_held_root_after_its_spelling_is_retargeted`
  (PR 1): after `retarget(outer\vault, B)` the duplicate's `FILE_ID_INFO`
  is the session's identity, not `B`'s; dropping it leaves the hold (a
  rename of `A` still fails); a session over a test provider no
  filesystem root backs (`structural_batch.rs:976`) returns
  `Unsupported` [duplicate by reopening `opened_as`; hand out the held
  handle itself].
- `a_root_whose_file_id_info_is_unsupported_opens_with_a_weak_file_index_identity`
  (WD24 amended, PR 1): with `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED`
  (the FAT32 profile) set to the name of a unique temp folder `T` (set and cleared through WB-18's `fault_guard()`)
  holding the vault `T\vault` and a junction `T\other` → `T\vault`,
  `from_filesystem(T\vault)` is `Ok`; `root_identity()` has `persistent
  == false`, `id_high == 0` and `(volume, id_low) ==
  file_index_of(T\vault)` (after asserting the NTFS index's high word is
  nonzero); `.slate` exists; a second session through `T\other` shares
  its structural-lock slot; a sibling vault outside `T` opens with
  `persistent == true` [refuse when `FileIdInfo` fails; flag the
  fallback persistent; keep only `nFileIndexLow`; gate on the volume's
  real name, ignoring the seam's profile].
- `a_root_whose_file_id_cannot_be_read_does_not_open_and_gains_no_slate`
  (WD24, neither read answers, PR 1): with
  `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE` set to the temp root's unique name
  (set and cleared through WB-18's `fault_guard()`) — the FAT32 profile
  with both reads failing — `from_filesystem` is
  `Err(VaultError::Io)` whose `vault_error_detail` is exactly
  `VAULT_ROOT_IDENTITY_UNAVAILABLE`, no `root\.slate` exists, the root
  renames right after, and a sibling vault not matching the value opens
  with an identity [open with `None`; open with a path-derived identity;
  make `.slate` before the identity read; pass the OS text through].
- `a_vault_on_a_fat32_or_exfat_volume_opens_with_the_identity_its_file_system_answers`
  (`#[cfg(windows)] #[ignore]`, gated on `SLATE_WEAK_VOLUME_FAT32` /
  `SLATE_WEAK_VOLUME_EXFAT` from WB-27's VHD procedure, panicking when
  neither is set; PR 1): per volume set, after asserting
  `file_system_name_of(root)` is `FAT32` or `exFAT` to match the
  variable, a fresh vault opens; its
  `root_identity()` has `persistent == file_id_info_answers(root)`
  (expected `false` per step 0), and when weak `(volume, id_low) ==
  file_index_of(root)`; `.slate` exists; a saved note reads back [refuse
  when `FileIdInfo` fails; flag the fallback persistent; admit `FAT32`
  alone].
- `the_windows_structural_lock_registry_is_keyed_by_identity` (twin of
  `:357`): one spelling retargeted between two opens gives two slots; a
  second spelling of one vault shares its slot [key by path].
- uniffi `root_identity_crosses_the_ffi_losslessly`: four fields, a
  high-bits id, `persistent` both `true` and `false` [drop a field; drop
  `persistent`].
- uniffi `the_root_identity_refusal_crosses_the_ffi_as_cores_sentence`
  (PR 1): `vault_error_detail` of the core refusal crossed to the binding,
  and `vault_root_identity_unavailable_detail()`, both equal core's
  constant [render `Io` by its `Display`, "io error: …"; a second copy
  of the text in the export].
- uniffi `duplicate_root_handle_crosses_the_ffi_as_an_owned_duplicate`
  (PR 1): on Windows the returned `u64`, wrapped as an `OwnedHandle`,
  reads the record's `FILE_ID_INFO`, and after it is dropped the root
  still cannot be renamed; off Windows it is `Unsupported` [return the
  held handle's raw value; return `Ok(0)` off Windows].
- `W1AnchoredVaultStoreTests.OpenReachesTheHeldRootWhenTheVaultsSpellingIsRetargeted`
  (PR 2): the vault `T\a` opened through an `mklink /J` spelling `T\vault`
  retargeted to a decoy `T\b` between session open and store open;
  `AnchoredVaultStore.Open(VaultRootAnchor.Of(session), true)` writes
  `probe.json` into `T\a\.slate`, reads it back, and `T\b` is
  byte-identical [open the spelling as main does → refused as a reparse
  point; follow the junction → `T\b`].
- `W1AnchoredVaultStoreTests.EveryStoreWritesBeneathTheHeldRootAfterTheSpellingIsRetargeted`
  (PR 2): after the same retarget `WorkspacePersistence.Save`,
  `SearchRecentsStore.Add`, `SidebarSettingsStore.SetOrganization` and
  `GraphConfigStore.Write`, each with `VaultRootAnchor.Of(session)`, land
  in `T\a\.slate`; `T\b` unchanged [one store opens its spelling].
- `W1AnchoredVaultStoreTests.ARootForcedToAnotherVaultsWeakIndexIsNeverReachedThroughIt`
  (PR 2, the synthetic colliding-index / retargeted-root fact's host
  half): with `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` and
  `SLATE_TEST_ROOT_FILE_INDEX_COLLIDES` set in-process to `T` (cleared in
  `finally`), session `a` through `T\vault` → `T\a` and session `b` at
  `T\b` have equal weak `RootIdentity()` (asserted); after the junction
  is retargeted to `T\b`, a store through `VaultRootAnchor.Of(a)` writes
  into `T\a\.slate` with `T\b\.slate` unchanged, and one through
  `VaultRootAnchor.Of(b)` writes into `T\b\.slate`, never `T\a` [round
  10's admission by the weak identity at the spelling → nothing reaches
  `T\a`; a table of open sessions keyed by `RootIdentity()` → `a`'s store
  writes into `T\b`].
- `W1AnchoredVaultStoreTests.AStoreRefusesOnceItsSessionIsDisposed`
  (PR 2): a disposed session's anchor makes `Open` throw `IOException`
  and nothing is written [fall back to the spelling].
- `W1AnchoredVaultStoreTests.AStoreGivenNoAnchorRefusesOutsideTheTestAssembly`
  (PR 2; a collection with `DisableParallelization = true`): with
  `ByPathAllowed` false (restored in `finally`) a store built without an
  anchor throws `IOException` on `Save` and writes nothing; with it true
  it writes [open the spelling whatever the switch says].
- `GraphConfigWriterTests.AQueuedWriteLandsInTheRootItWasEditedIn`
  (PR 2): a write parked by `WriteGateForTests` on a
  `ManualResetEventSlim` (its `Wait(TimeSpan.FromSeconds(10))` result
  asserted; released in `finally`) after `Enqueue(…,
  VaultRootAnchor.Of(session))`, the session disposed and the junction
  retargeted, then released: `graph.json` lands in `T\a`,
  `FailedForTests` is 0, `T\b` unchanged, and `T\a` renames afterwards
  [resolve the anchor at write time → the write fails; never dispose the
  pin → the rename fails].
- `FileRecentsStoreTests.APathKeyedRecentsFileMigratesToTheIdentityKeyOnce`
  (old read, new written, old deleted, second load no-op) and
  `.TheIdentityKeyIsTheLosslessIdentity` (key text carries the high
  bits), both for a persistent identity [skip the delete; format
  `Device-Inode`].
- `FileRecentsStoreTests.AFailedDestinationCommitKeepsThePathKeyedFile`
  (PR 2): a directory at the identity-keyed file's path makes the
  `File.Move` fail; `Load()` returns the path-keyed `[a, b]`, the
  path-keyed file is byte-identical and no identity-keyed file exists;
  with the directory removed the next `Load()` migrates and deletes the
  source [delete after main's `void Save` → `[a, b]` lost].
- `FileRecentsStoreTests.AnUnreadablePathKeyedFileIsNeitherMigratedNorDeleted`
  (PR 2): the source held with `FileShare.None` (released in `finally`)
  during `Load()` → an empty list, no identity-keyed file, the source
  byte-identical; the next `Load()` migrates [read through main's `Read`,
  an error as an empty list → an empty destination committed and the
  source deleted].
- `FileRecentsStoreTests.AnInterruptedMigrationCompletesOnALaterLoad`
  (PR 2): the source held while `Add("c")` commits `[c]`; after release
  the next `Load()` returns and stores `[c, a, b]` and the source is gone
  [migrate only while the destination is absent → `[c]`, the source
  kept forever].
- `FileRecentsStoreTests.AnUnreadableInVaultRecentsFileIsNotDeleted`
  (PR 2): the store built with the fixture vault's own persistent
  `session.RootIdentity()` and `VaultRootAnchor.Of(session)`; the in-vault `file-recents.json` held with
  `FileShare.None` while neither device file exists → an empty list, no
  device file, the in-vault file byte-identical; the next `Load()`
  migrates `[a]` and only then deletes it [main's `MigrateLegacy` → the
  in-vault file deleted].
- `FileRecentsStoreTests.AnInVaultRecentsFileUnreadableBeforeAnAddMigratesOnALaterLoad`
  (PR 2; a Theory over the fixture vault's persistent identity and its
  weak one through `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` set
  in-process to the fixture's unique folder name, cleared in `finally`):
  the in-vault `[a, b]` held with `FileShare.None` while `Load()` (an
  empty list) and `Add("c")` (returns `[c]`, creating the destination)
  run; after release the next `Load()` returns and stores `[c, a, b]`, the
  in-vault file is gone, and one more `Load()` leaves
  `LegacyProbesForTests` unchanged [the in-vault step only while no
  device file exists → `[c]`, the in-vault file kept forever; never
  settle → the vault probed again].
- `FileRecentsStoreTests.AFailedInVaultCommitIsRetriedAfterTheDestinationExists`
  (PR 2): the destination `[c]` read-only (a `File.Move` over it fails
  with access denied; it stays readable), the in-vault `[a, b]`: `Load()`
  returns `[c, a, b]` for that call, `FileRecentsPersistFailed` is logged
  once, and both files are byte-identical; with the attribute cleared
  (again in `finally`) the next `Load()` on the same instance commits
  `[c, a, b]` and only then deletes the in-vault file [delete after an
  unconfirmed commit → the in-vault file gone, the destination `[c]`;
  settle on a failed commit → `[c]`, the in-vault file kept].
- `FileRecentsStoreTests.ClearingRecentsDropsAnUnmigratedInVaultFile`
  (PR 2): the in-vault `[a]` held during the first `Load()` (an empty
  list, unsettled), released (in `finally`), then `Clear()`: the
  destination reads `[]`, the in-vault file is gone, the next `Load()`
  returns `[]` [`Clear` without the in-vault delete → `[a]`].
- `FileRecentsStoreTests.AWeakIdentityKeepsThePathKeyAndMigratesNothing`
  (PR 1, where every identity keeps the path key, and PR 2): a store
  built with `new VaultRootIdentity(volume, 0, index, false)` uses the
  same file as one built with `identity: null`; a path-keyed file is
  neither moved nor deleted; no identity-keyed file appears [key by the
  weak identity; migrate a weak identity].
- `VaultOpenIdentityTests.AFolderWhoseFileSystemGivesNoFileIdDoesNotOpenAndSaysWhy`
  (WD24, PR 1): through the real core with the seam set in-process (cleared
  in `finally`), `OpenVaultAsync` speaks exactly one `High` `HostComposed`
  "Could not open vault: " + `VaultRootIdentityUnavailableDetail()`, no
  `VaultOpened`, `IsVaultOpen` false, `ReturnedToWelcome` once, recents
  unchanged, no `.slate` [speak `exception.Message`, which reads
  "@message=…"; open anyway].
- `VaultOpenIdentityTests.OpenRefusesWhenCoreReportsNoIdentity` (PR 2):
  a real vault opens in core, `RootIdentityReader` returns `null`; the
  session is disposed and the same single sentence spoken, no
  `VaultOpened`, no recents entry [admit null].
- `VaultOpenIdentityTests.AFolderAnsweringOnlyTheFileIndexOpens` (PR 2):
  through the real core with `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED`
  set in-process (cleared in `finally`), `OpenVaultAsync` raises
  `VaultOpened` once, `IsVaultOpen` is true, `RootIdentity().Persistent`
  is false, no refusal is spoken, the recent-vaults list gains the vault
  [refuse a non-persistent identity in `OpenVaultAsync`; fail the core
  open when `FileIdInfo` fails].
- `RescanTests.ARefreshAfterTheVaultsSpellingIsRetargetedListsTheHeldVault`:
  after a junction retarget Refresh lists the held vault's notes, none of
  the decoy's [pathname reads].
- mac: `swift-tests.yml` compiles the `AppState.swift:10670-10671` edit
  and runs `SidebarOrganizationDispatchTests.testOpenBindsSessionAndSidebarToOneObservedRoot`
  with its `:2478-2479` read changed to `anchor.volume`/`anchor.idLow`
  [map `inode` from `idHigh` at `:10671` → the open aborts], and keeps
  the Unix 22a facts green.

Registers: design §7 row 22W ("Delivered …") and §11 (WD22's answer),
contract 31 FR-10 (the root identity's two reads, the fallback only on
FAT, FAT32 and exFAT, the weak flag that keys nothing and authorizes
nothing, the stores reaching the vault through the held root, and WD24's
refusal: a `FileIdInfo` failure on any other file system, or neither
read on those), step 0's table (with each volume's file-system name) appended
(dated) to the §5 Windows block of
`24_structural_identity_capability_matrix.md` and pasted in PR 1's body,
the parity matrix `--check`, the plan §11 helper names; no corpus,
census, designations or ledger change (the refusal is a `VaultError`
detail). AT: PR 1's NVDA
spot check with the Release build and NVDA running, device state isolated
through WB-1's field-pass launcher
(`scripts/windows/launch-for-field-pass.ps1 -Vault -Seed -Keep -Exe`: a
fresh `%TEMP%` root with `SLATE_LOCAL_APP_DATA` and `SLATE_LOG_DIR`
beneath it, `-Seed` writing a `recent-vaults.json` that holds the vault,
the exe started with the vault as its argument), so nothing touches the
owner's recents or device state (plan §4 rule 4) and the seed replaces
opening the vault once by hand. In one PowerShell window: make a scratch
vault `$scratch` named `wb28-no-file-id-$PID` under the temp folder (a
`Notes\a.md` in it) and a second vault `$other` whose path does not
contain that name; set `$env:SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE = Split-Path
$scratch -Leaf`; run `pwsh -NoProfile -File
scripts/windows/launch-for-field-pass.ps1 -Vault $scratch -Seed -Keep
-Exe $exe` (`$exe` the Release `SlateWindows.exe`), then `if
($LASTEXITCODE -ne 0) { throw }`, and remove the variable. Then:
1. At launch the app opens `$scratch`: NVDA speaks once "Could not open
   vault: This folder's file system gives Slate no file identity for
   it, so Slate cannot keep the vault anchored to it." The keys stay in
   the welcome view, and nothing else is spoken.
2. Open it again from the welcome view's recents (seeded): the same
   sentence, once.
3. Open `$other` through Open Folder: it opens and is announced as
   usual. Close Slate; the launcher returns and its exit code is checked.

The NVDA log excerpt goes in PR 1's body, and PR 1 stays draft until
then.

Not delivered: whatever the SQLite spike forces onto the recorded
fallback.

Done when all of these hold:
- `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED`,
  `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE`,
  `SLATE_TEST_ROOT_FILE_ID_INFO_FAILS` and
  `SLATE_TEST_ROOT_FILE_INDEX_COLLIDES` are in WB-18's `FAULT_VARIABLES`
  (`session/tests/common.rs`, four more) and its guard fact's literal
  list from PR 1 on, every core fact sets them only through
  `fault_guard()`, and WB-18's two guard facts stay green.
- Step 0 ran before any code and its table is in PR 1's body and the
  matrix (and, if FAT32 or exFAT answered `FileIdInfo`, the header says
  the fallback is a defensive path).
- The Verification block runs clean on Windows: every required fact is
  discovered by name under its filter, and every command's
  `$LASTEXITCODE` is 0 (the two core filters with the fallback-gate,
  NTFS-failure, colliding-index and duplicated-handle facts among them,
  `anchored`, the whole core suite, both uniffi filters (`root_identity`
  and `duplicate_root_handle`), fmt, clippy, workspace tests, bench
  compile, licence headers through Git Bash by full path, bindings,
  build, the host facts (the six held-root store facts and the seven
  item-7 migration facts among them, the in-vault Theory discovered as
  rows), the two `git grep` checks (no production `ByPathAllowed = true`;
  every store construction site passes `VaultRootAnchor.Of`), `dotnet format`,
  the parity matrix, the two scan benches within noise, and — in PR 1,
  on WB-27's VHDs — the gated FAT32/exFAT fact, discovered by
  `--ignored --list`). PR 1 cuts the host list to its two host facts,
  `AFolderWhoseFileSystemGivesNoFileIdDoesNotOpenAndSaysWhy` and
  `AWeakIdentityKeepsThePathKeyAndMigratesNothing`, and leaves out the
  `git grep` checks and the files PR 2 adds.
- The Unix facts are green on the rust and swift lanes (the Linux arm of
  `duplicate_root_handle_crosses_the_ffi_as_an_owned_duplicate`
  included).
- Each mutation was run locally.
- The NVDA spot check, launched through WB-1's field-pass launcher, is
  recorded in PR 1's body.
- PR 1's body carries `Refs #1475` and `Refs #940` and no closing
  keyword. PR 2's body carries `Closes #1475` and `Refs #940`, and PR 2
  merges only after PR 1 is merged and both PRs' Verification runs and
  PR 1's NVDA spot check are recorded. Commit:
`feat(core,windows): the windows session holds its vault root, resolves its interior through handles and reports a lossless root identity`.
