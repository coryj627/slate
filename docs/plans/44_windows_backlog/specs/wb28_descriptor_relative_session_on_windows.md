# WB-28 — 22W: the Windows session holds its vault root and resolves its interior through handles, and its identity is lossless

Issues: #1475 (the identity half; closes here per §5's matrix — WB-12
delivered the `GraphConfigStore` write with `Refs`. This chunk ships as
two PRs: PR 1's body writes `Refs #1475`, and only PR 2's body writes
`Closes #1475`, once PR 1 and PR 2 are both merged and verified), Refs
#940 in both PR bodies (closes by hand after 22b and 22W, RD15). Size L (design first:
`22_descriptor_relative_session_design.md` §7 row 22W, `:796`). Lanes:
rust (Windows cfg), windows, swift (the FFI record change — one
production read and one test read of its fields — and one additive
export). AT: NVDA spot check on PR 1 (the open refusal WD24 adds;
nothing else a reader hears changes — a FAT32 or exFAT vault opens as on
main). Wave: W10, alone.
Depends on: WB-27 (shares `fs.rs` and
`windows_entry.rs`), plan 43 22c merged (`FsRoot` helper names agreed,
§6 gate). Parallel with: nothing; WB-29 and WB-30 follow it. Decision:
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
with path-keyed stores. Design items 0, 1, 4, 6 and 7 apply it).

## Goal

On Windows, as on macOS and Linux since 22a, a filesystem session binds
the root it opened: the vault root and `.slate` are held open for the
session's lifetime, every interior open (`cache.sqlite` and its WAL and
SHM, `structural.lock`, `slate.json`, `.slate/prefs.json`, the op-log,
`.slate/tmp`) is `NtCreateFile` with `RootDirectory` set to a held handle,
the provider's reads resolve beneath the held root, and `root_identity()`
is a real anchor read from the held handle in a form that loses no bits
of NTFS's 128-bit file id. #1475's "different folder now at the path"
column gets its answer: the anchored stores compare with core's identity,
and, where the identity is persistent, the device-local recents key
follows the vault, not its path. A folder on FAT32 or exFAT, which
generally answers no `FileIdInfo`, still opens: its identity is the
volume serial and the 64-bit file index, flagged weak, which the stores
compare within the session but which keys nothing durable. Only a folder
for which neither read answers does not open, and the reader hears
core's reason (WD24, amended 2026-10-21).

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
  `nFileIndex` is not unique on ReFS — so the file index is a fallback
  only, never preferred to `FileIdInfo`. The fallback read exists in
  core: the watcher's Windows `observe_identity`
  (`vault/watch.rs:279-307`, the call at `:298`; design §4.4) reads
  `GetFileInformationByHandle` into `RootIdentity { volume:
  dwVolumeSerialNumber, file: nFileIndexHigh << 32 | nFileIndexLow }`
  (`:67-74`). Win32's `BY_HANDLE_FILE_INFORMATION` remarks: a FAT file
  id comes from the containing directory's first cluster and the entry's
  byte offset in it, so renaming (to a longer name) or moving the entry
  can change it — and, by the same derivation, a freed slot can give a
  later entry the same id.
  Measured on this machine's NTFS (the step-0 probe's control row):
  `FILE_ID_INFO.VolumeSerialNumber` is the 64-bit serial
  (`8e885d96885d7da1`), `dwVolumeSerialNumber` its low 32 bits
  (`885d7da1`), and the 128-bit id's low half equals the file index.
- **Hosts' consumers of the record.** Windows: `FileRecentsStore.cs:22-37`
  (the parameter `:24`, key `:27-30` reading `identity.Device` and
  `identity.Inode` at `:30`, hash `:31`, file `:32-36`); `MigrateLegacy`
  (`:65-103`); constructed at `FilesSidebarViewModel.cs:557` and
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
  operation without `FILE_SHARE_DELETE` (`Open` `:58`,
  `OpenRequiredDirectory` `:245`), compares identity as text
  (`VerifyDirectoryIdentity` `:333-342`, `FinalPath` `:434`).
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
   `FILE_FLAG_BACKUP_SEMANTICS` and records, per volume: whether
   `GetFileInformationByHandleEx(FileIdInfo)` answers (its value, or its
   Win32 error number); whether `GetFileInformationByHandle` answers (the
   volume serial and file index); the index read again (stable within a
   mount); a sibling folder's index (distinct); and both reads after
   renaming the folder to a longer name (recorded, not asserted). It
   throws when the NTFS control answers no `FileIdInfo` (the probe is
   broken), when a volume answers neither read, or when a volume that
   needs the fallback gives an index that is not stable and distinct
   within the mount. Either of the last two stops the chunk for an owner
   decision: WD24's refusal would then stop that volume's vaults from
   opening, or the weak identity could not serve even within a session.
   The table goes into PR 1's body and, dated, into the §5 Windows block
   WB-27 appended to `24_structural_identity_capability_matrix.md`. What
   the result changes: where FAT32 or exFAT answers no `FileIdInfo`
   (expected), the fallback is the read their vaults open by, and the
   gated fact proves it on the VHD. Where one does answer `FileIdInfo`,
   the fallback stays as a defensive path for any file system that does
   not, proven by the seam fact
   `a_root_whose_file_id_info_is_unsupported_opens_with_a_weak_file_index_identity`
   alone, and the adoption note in this header says so. The gated fact
   computes its expectation from the volume and passes either way. If
   the probe records an error other than `ERROR_INVALID_PARAMETER` (87)
   for `FileIdInfo`, the two seams of item 6 fail with the recorded code.
1. **The record's shape (WD22; the flag is WD24 amended).**
   `VaultRootIdentity { volume: u64, id_high: u64, id_low: u64,
   persistent: bool }` (new fields, core and uniffi). `persistent` is
   `true` when the id is the file system's own file id: Windows
   `FileIdInfo` (`volume` = `FILE_ID_INFO.VolumeSerialNumber`, the
   128-bit id split into `id_high`/`id_low`) or Unix `fstat` (`volume =
   st_dev`, `id_high = 0`, `id_low = st_ino`, as today; mac weak volumes
   are plan 43 PR 38/41's). It is `false` when Windows answered only the
   fallback: `volume` = `dwVolumeSerialNumber` zero-extended, `id_high =
   0`, `id_low = nFileIndexHigh << 32 | nFileIndexLow` (the watcher's
   pair). Equality and hashing cover all four fields, so a weak identity
   never equals a persistent one — not even on NTFS, where the two reads
   share the low 64 bits but not the serial. `u64`s because both bindings
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
   item 6's two-read identity, over two new private helpers `file_id_info`
   and `file_index`; `InteriorDir for CacheDir` compiles on Windows; 22c's
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
   `VaultRootIdentity` (volume, id and `persistent`). A weak `.slate` key
   is sound for the registry's life: the registry lives in one process,
   each entry is held by a session that holds `.slate` without
   `FILE_SHARE_DELETE` (so its directory entry is neither renamed nor
   freed while the key exists), and two directories that exist at once on
   one volume never share a file index. Two spellings of one vault read
   the same entry and share a slot.
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
   opening; a vault does not open only when neither read answers).**
   - *The read.* The Windows `sys::identity(file: &fs::File) ->
     io::Result<VaultRootIdentity>` calls `sys::file_id_info(file)` (new:
     `GetFileInformationByHandleEx(FileIdInfo)` → `persistent: true`).
     When that fails — FAT32 and exFAT are expected to fail with
     `ERROR_INVALID_PARAMETER`; step 0 records the real code — it calls
     `sys::file_index(file)` (new: `GetFileInformationByHandle` →
     `persistent: false`, the watcher's pair, `watch.rs:298-306`), and
     logs the first error at debug level. Only when both fail does
     `identity` fail, with the fallback's error. The fallback follows any
     `FileIdInfo` failure, as the amendment words it ("only when neither
     read works"). ReFS always answers `FileIdInfo`, so its non-unique
     64-bit index (`SidebarWriteJournal.cs:58`) is never the identity of a
     healthy ReFS root; were it ever, it is flagged weak and used only as
     the weak rules below allow. Not changed: structural identity
     (`windows_entry::identity` for undo's `capture` and `rename`) and the
     Trash stamp stay `FileIdInfo`-only and fail closed on such volumes,
     as on main — an index that changes on rename cannot check a rename.
   - *Core refuses first, and only when neither read answers.* The
     Windows `FsRoot::open` calls `sys::identity` on the held root right
     after the hold and before `open_or_make_slate`, as the Unix body does
     with `fstat` (`fs_root.rs:588-605`). If it fails, the open fails with
     `VaultError::Io(io::Error::new(error.kind(),
     a11y::VAULT_ROOT_IDENTITY_UNAVAILABLE))`. The OS error is logged at
     debug level and never spoken. The held root closes with the error,
     and no `.slate` is made in a folder Slate cannot anchor. The held
     `.slate`'s identity (item 4's lock key) is read the same way
     (persistent, else weak) and refuses with the same sentence only when
     neither read answers. Core's copy, in `a11y.rs` (new): `pub const
     VAULT_ROOT_IDENTITY_UNAVAILABLE: &str = "This folder's file system
     gives Slate no file identity for it, so Slate cannot keep the vault
     anchored to it.";`. An open Windows session therefore always has a
     root identity, persistent or weak — the invariant WB-30's
     identity-mode rule rests on (its `Strong` mode needs NTFS or ReFS
     **and** `persistent`).
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
   - *Two seams any build can trip.* Both follow the `SLATE_TEST_*`
     precedent (`session.rs:4371`) and are consulted by the Windows
     `FsRoot::open` — the one caller that knows the opened path — for the
     root's and `.slate`'s reads, when the opened root's path contains the
     variable's value. `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` (new)
     makes the `FileIdInfo` read fail with `ERROR_INVALID_PARAMETER`, as
     FAT32 and exFAT do, so the fallback answers and the session opens
     with `persistent: false`. `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE` (new)
     makes both reads fail with `ERROR_INVALID_PARAMETER`, as a file
     system that answers neither would, so the open refuses. They serve
     the core facts, the host facts and the NVDA spot check.
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
   - *The stores, persistent or weak.* `AnchoredVaultStore.Open` gains
     `expectedIdentity: VaultRootIdentity?` (new). It reads the root
     handle it opens with the read the flag names — `FileIdInfo` when
     `expectedIdentity.Persistent`, `GetFileInformationByHandle` (the
     volume serial and file index) when not — and refuses when that read
     fails or any of the four fields differs; the five stores pass
     `session.RootIdentity()`. Comparing a weak identity is safe within a
     session: the session holds the root without `FILE_SHARE_DELETE`, so
     Windows refuses its rename and delete for the session's lifetime and
     its index can neither move nor be reused; and two directories that
     exist at once on one volume never share a FAT file index (each is its
     own directory entry). So a different folder reached at the session's
     spelling — a retargeted junction, or another volume at the drive
     letter — has another index or another serial and is refused, as with
     a persistent id. What a weak identity cannot do is outlive the
     session: with Slate closed, a rename or move of the folder can change
     its index, and a later folder can take a freed slot's index — item 7.
7. **`FileRecentsStore` key and migration.** A persistent identity: key =
   SHA-256 of
   `$"{identity.Volume:x16}-{identity.IdHigh:x16}{identity.IdLow:x16}"`;
   on first open, if the identity-keyed file is absent and the path-keyed
   file exists, `Load` reads it, saves under the new key and deletes the
   old file. A weak identity (`Persistent` false) keys nothing durable:
   the store keeps today's path key (SHA-256 of
   `_vaultRoot.ToUpperInvariant()`, `FileRecentsStore.cs:28-31`), migrates
   nothing and deletes nothing. Why: a FAT index changes when the folder
   is renamed or moved and can be reused by a later folder in a freed
   slot, so as a key it would neither follow the vault nor keep two
   vaults apart over time; the path key is what every Windows vault uses
   on main (`RootIdentity()` is null there), so these volumes lose
   nothing — a stick reopened at another drive letter starts with an
   empty list, exactly as on main. `MigrateLegacy` keeps reading only the
   old in-vault file. PR 1 (before this item lands in PR 2) edits
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
   identity's `persistent` flag names, so a weak root's display path is
   verified by its file index, sound within the session as item 6 says),
   the host watcher, Reveal and ShellExecute (design §4.2).
10. **Split.** PR 1: step 0's probe record, then core (Windows `FsRoot`,
    `sys`, hook, the two-read identity with its `persistent` flag, the
    WD24 refusal and its two seams, facts) with the FFI record change, the
    additive `vault_root_identity_unavailable_detail` export, both hosts'
    field edits (`FileRecentsStore.cs:30` keeping the path key,
    `AppState.swift:10670-10671`, `SidebarOrganizationDispatchTests.swift:2478-2479`),
    and the host's open-failure catch speaking core's detail. The refusal
    is first heard in PR 1, so PR 1 carries its host fact and its NVDA
    spot check, and PR 1 runs
    `FileRecentsStoreTests.AWeakIdentityKeepsThePathKeyAndMigratesNothing`
    (its interim path key satisfies it). PR 1's body writes `Refs #1475` and `Refs #940` — never a
    closing keyword, because the store admission and the recents key that
    answer #1475's columns are PR 2's. PR 2: the stores' admission
    (persistent and weak), `OpenVaultAsync`'s fail-closed null check, and
    the recents key (persistent: identity key with the migration; weak:
    the path key). PR 2's body writes `Closes #1475` and `Refs #940` with
    the 22W delivery comment; PR 2 merges only after PR 1 is merged and
    both PRs' Verification runs and PR 1's NVDA spot check are recorded,
    so the close lands when both deliveries are verified. Both before
    WB-29 and WB-30.

## Design

Per the section above once agreed. New names: `fs_root::sys` (Windows;
its `identity(file) -> io::Result<VaultRootIdentity>` over the private
`file_id_info(file)` and `file_index(file)`), `db::anchored` Windows body
and `skip_install_for_tests`, `OpenSwapPoint::BeforeSlateHeld`,
`VaultRootIdentity { volume: u64, id_high: u64, id_low: u64, persistent:
bool }` (core and uniffi; `FsRoot::identity() ->
Option<VaultRootIdentity>`), `a11y::VAULT_ROOT_IDENTITY_UNAVAILABLE`,
`SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` and
`SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE`, the uniffi export
`vault_root_identity_unavailable_detail()`,
`VaultLifecycleViewModel.RootIdentityReader`, `AnchoredVaultStore.Open(…,
expectedIdentity)` (comparing by the read the flag names),
`FileRecentsStore` identity key v2 (persistent identities only), and the
step-0 probe script (Verification; not committed code). Two test helpers
in the new module, each reading the folder independently of `sys`, give
the weak facts their expectations: `fn file_index_of(path: &Path) ->
(u64, u64)` (new: `dwVolumeSerialNumber` zero-extended and
`nFileIndexHigh << 32 | nFileIndexLow`) and `fn
file_id_info_answers(path: &Path) -> bool` (new: whether
`GetFileInformationByHandleEx(FileIdInfo)` succeeds on it).
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
| `a_root_whose_file_id_info_is_unsupported_opens_with_a_weak_file_index_identity` (WD24 amended; PR 1) | with `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` set to the name of a unique temp folder `T` (removed in a drop guard) that holds the vault `T\vault` and a junction `T\other` → `T\vault`, so both spellings contain it: `VaultSession::from_filesystem(T\vault)` is `Ok`; `root_identity()` is `Some` with `persistent == false`, `id_high == 0` and `(volume, id_low) == file_index_of(T\vault)` (the watcher's pair), after asserting the precondition that the lane's NTFS index has a nonzero high word (`file_index_of(T\vault).1 >> 32 != 0`); `T\vault\.slate` exists; a second session opened through `T\other` shares the first's structural-lock slot (item 4's weak key); a sibling vault outside `T` opens with `persistent == true` | refuse when `FileIdInfo` fails, as WD24 read before its amendment (→ `Err`); flag the fallback persistent (→ `persistent == true`); keep only `nFileIndexLow` (→ `id_low` differs) |
| `a_root_whose_file_id_cannot_be_read_does_not_open_and_gains_no_slate` (WD24: neither read answers; PR 1) | with `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE` set to the temp root's unique folder name (removed in a drop guard), so both reads fail: `VaultSession::from_filesystem(root)` is `Err(VaultError::Io(_))` and `a11y::vault_error_detail` of it equals `VAULT_ROOT_IDENTITY_UNAVAILABLE` exactly; `root\.slate` does not exist; `std::fs::rename(root, …)` succeeds right after (nothing is held); a sibling vault in the same fact, whose path does not contain the value, opens with `root_identity()` `Some` | open with the identity `None` instead of refusing (→ `Ok`); open with a path-derived identity when both reads fail (→ `Ok`); make `.slate` before the identity read (→ `.slate` exists); pass the OS error through (→ the detail is the OS text) |
| `a_vault_on_a_fat32_or_exfat_volume_opens_with_the_identity_its_file_system_answers` (`#[cfg(windows)] #[ignore = "needs SLATE_WEAK_VOLUME_FAT32 or SLATE_WEAK_VOLUME_EXFAT (WB-27's volume procedure)"]`; WD24 amended; PR 1) | for each of the two variables that is set (it panics when neither is, so a named run never passes vacuously): a fresh vault folder on that volume opens through `from_filesystem`; `root_identity()` is `Some`, its `persistent` equals `file_id_info_answers(root)` (expected `false`, per step 0's record), and when `persistent` is false `(volume, id_low) == file_index_of(root)`; `root\.slate` exists; a note saved through the session reads back | refuse when `FileIdInfo` fails (→ the open fails on a volume without it); flag the fallback persistent (→ `persistent` differs from `file_id_info_answers`) |
| `the_windows_structural_lock_registry_is_keyed_by_identity` (twin of `:357`) | two sessions on two roots with one textual spelling (`outer\vault` → `A`, then `retarget(outer\vault, B)` and a second open) get distinct slots — `A`'s lock is held, `B`'s free; and a third session opened through a second spelling of `A` (`outer\other` → `A`) shares the first session's slot | key by path |
| uniffi: `root_identity_crosses_the_ffi_losslessly` | four fields, a high-bits id and `persistent` both `true` and `false` | drop a field; drop `persistent` |
| uniffi: `the_root_identity_refusal_crosses_the_ffi_as_cores_sentence` (PR 1) | `vault_error_detail(VaultError::from(core::VaultError::Io(io::Error::new(io::ErrorKind::InvalidInput, VAULT_ROOT_IDENTITY_UNAVAILABLE))))` and `vault_root_identity_unavailable_detail()` both equal `core::a11y::VAULT_ROOT_IDENTITY_UNAVAILABLE` | render `Io` by the binding's `Display` (`"io error: …"`); return a second copy of the text from the export |
| Windows `W1AnchoredVaultStoreTests.OpenRefusesADifferentFolderAtTheVaultsPath` | the vault opened through a junction spelling (made with `mklink /J`, which needs no privilege, as `CanvasDocumentTests.cs:4131-4134` does); the junction retargeted to a decoy folder between session open and store open — the held root cannot be renamed — and `Open(…, expectedIdentity)` refuses with the reason | compare text only |
| `W1AnchoredVaultStoreTests.EveryStoreRefusesAfterTheRootWasReplaced` | after the same junction retarget, `WorkspacePersistence`, `SearchRecentsStore`, `SidebarSettingsStore`, `GraphConfigStore` all refuse | one store bypasses |
| `W1AnchoredVaultStoreTests.AWeakIdentityIsComparedByTheFileIndex` (WD24 amended; PR 2) | with `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` set in-process to the fixture's unique folder name, which the junction spelling contains (cleared in `finally`): `session.RootIdentity()` has `Persistent` false; the fact first asserts that the volume's `FILE_ID_INFO` serial differs from its `dwVolumeSerialNumber` (NTFS's 64-bit serial has nonzero high bits); `Open(…, expectedIdentity)` on the same root admits; after the junction is retargeted to a decoy, it refuses | compare `FileIdInfo` whatever the flag (→ the same root refuses: the serials differ); admit a weak identity without comparing (→ the decoy is admitted) |
| `FileRecentsStoreTests` (new class): `APathKeyedRecentsFileMigratesToTheIdentityKeyOnce`, `TheIdentityKeyIsTheLosslessIdentity` (persistent identities) | old file read, new written, old deleted, second load no-op; key text carries the high bits | skip the delete; format `Device-Inode` |
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
  the file system answers `FileIdInfo`; that otherwise it is the volume
  serial and 64-bit file index, flagged weak (`persistent: false`), which
  the stores compare within a session and which keys nothing durable;
  and that a Windows root for which neither read answers does not open
  (WD24 as amended 2026-10-21, core's `VAULT_ROOT_IDENTITY_UNAVAILABLE`
  sentence).
- `24_structural_identity_capability_matrix.md`: append (dated, never
  rewrite) step 0's table — NTFS control, FAT32, exFAT: which identity
  read answers, the `FileIdInfo` error number, the index's stability and
  distinctness within a mount, and what a longer rename did — to the §5
  Windows block WB-27 appended.
- No `corpus.json`, census, designations or trigger-ledger change: the
  refusal is a `VaultError` detail, not an `A11yEvent` (design item 6).
- PR 1's body: step 0's probe table, the gated FAT32/exFAT fact's run,
  and the NVDA spot check (Verification) with its NVDA log excerpt.
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
    // One folder, both reads (FileIdInfo = class 18): each its value or "error <Win32 code>".
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
        return new[] { fileId, index };
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
    $row = [pscustomobject]@{ Volume = $volume.Name; FileIdInfo = $first[0]; FileIndex = $first[1]; IndexAgain = $again[1]; SiblingIndex = $sibling[1]; FileIdInfoAfterRename = $renamed[0]; IndexAfterRename = $renamed[1] }
    if ($volume.Name -eq 'NTFS' -and $row.FileIdInfo -like 'error*') { throw 'the NTFS control answers no FileIdInfo: the probe is broken' }
    if ($row.FileIdInfo -like 'error*' -and $row.FileIndex -like 'error*') { throw "$($volume.Name) answers neither read: stop for an owner decision (WD24 would refuse its vaults)" }
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
    'descriptor_interior_windows' = @('a_held_root_refuses_a_rename_of_itself_and_of_an_ancestor_while_the_session_lives', 'no_interior_writer_follows_a_retargeted_spelling', 'wal_connections_share_the_held_cache_across_a_retarget', 'a_second_process_on_the_textual_path_shares_the_anchored_cache', 'child_of_a_second_process_on_the_textual_path', 'root_display_path_follows_the_held_root_not_its_retargeted_spelling', 'canonical_path_is_computed_under_the_held_root_handle', 'a_junctioned_interior_folder_is_refused', 'the_connection_names_the_synthetic_path_and_the_literal_path_does_not_exist', 'an_uninstalled_hook_fails_the_anchored_open_instead_of_opening_by_path', 'a_slate_junction_planted_before_the_hold_never_receives_the_interior', 'root_identity_on_windows_is_the_volume_serial_and_the_whole_128_bit_file_id', 'a_root_whose_file_id_info_is_unsupported_opens_with_a_weak_file_index_identity', 'a_root_whose_file_id_cannot_be_read_does_not_open_and_gains_no_slate', 'the_windows_structural_lock_registry_is_keyed_by_identity')
    'fs_root' = @('both_forms_open_create_rename_list_and_remove_the_same_files', 'windows_a_held_cache_directory_refuses_a_rename_of_itself_and_of_its_parent', 'windows_a_held_cache_directory_resolves_beneath_its_handle_when_its_spelling_is_retargeted', 'windows_a_held_cache_directory_never_follows_a_junction')
}
foreach ($filter in $required.Keys) {
    Assert-Facts 'slate-core' $filter $required[$filter]
    cargo test -p slate-core --locked $filter; Assert-Ok "cargo test $filter"
}
cargo test -p slate-core --locked anchored; Assert-Ok 'anchored'
cargo test -p slate-core --locked --no-fail-fast; Assert-Ok 'the whole core suite'
Assert-Facts 'slate-uniffi' 'root_identity' @('root_identity_crosses_the_ffi_losslessly', 'the_root_identity_refusal_crosses_the_ffi_as_cores_sentence')
cargo test -p slate-uniffi --locked root_identity; Assert-Ok 'uniffi root_identity'
cargo fmt --all -- --check; Assert-Ok 'fmt'
cargo clippy --all-targets --workspace -- -D warnings; Assert-Ok 'clippy'
cargo test --workspace --locked; Assert-Ok 'workspace tests'
cargo bench --workspace --no-run; Assert-Ok 'bench compile'
bash scripts/check-license-headers.sh; Assert-Ok 'licence headers'
./apps/slate-windows/generate-bindings.ps1 -Locked; Assert-Ok 'bindings'
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'build'
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$filter = 'FullyQualifiedName~SlateWindows.Tests.W1AnchoredVaultStoreTests|FullyQualifiedName~SlateWindows.Tests.FileRecentsStoreTests|FullyQualifiedName~SlateWindows.Tests.VaultOpenIdentityTests|FullyQualifiedName~SlateWindows.Tests.RescanTests'
$listed = dotnet test $tests --no-build --configuration Release --list-tests --filter $filter; Assert-Ok 'listing the host facts'
foreach ($fact in 'OpenRefusesADifferentFolderAtTheVaultsPath', 'EveryStoreRefusesAfterTheRootWasReplaced', 'AWeakIdentityIsComparedByTheFileIndex', 'APathKeyedRecentsFileMigratesToTheIdentityKeyOnce', 'TheIdentityKeyIsTheLosslessIdentity', 'AWeakIdentityKeepsThePathKeyAndMigratesNothing', 'AFolderWhoseFileSystemGivesNoFileIdDoesNotOpenAndSaysWhy', 'OpenRefusesWhenCoreReportsNoIdentity', 'AFolderAnsweringOnlyTheFileIndexOpens', 'ARefreshAfterTheVaultsSpellingIsRetargetedListsTheHeldVault') {
    if (-not ($listed -match "\.$fact$")) { throw "the host filter does not select $fact" }
}
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'host facts'
$cs = @('apps/slate-windows/src/SlateWindows/AnchoredVaultStore.cs','apps/slate-windows/src/SlateWindows/FileRecentsStore.cs','apps/slate-windows/src/SlateWindows/FilesSidebarViewModel.cs','apps/slate-windows/src/SlateWindows/QuickSwitcherViewModel.cs','apps/slate-windows/src/SlateWindows/VaultLifecycleViewModel.cs','apps/slate-windows/src/SlateWindows/WorkspacePersistence.cs','apps/slate-windows/src/SlateWindows/Search/SearchRecentsStore.cs','apps/slate-windows/src/SlateWindows/SidebarSettingsStore.cs','apps/slate-windows/src/SlateWindows/Graph/GraphConfigStore.cs','apps/slate-windows/tests/SlateWindows.Tests/W1AnchoredVaultStoreTests.cs','apps/slate-windows/tests/SlateWindows.Tests/FileRecentsStoreTests.cs','apps/slate-windows/tests/SlateWindows.Tests/VaultOpenIdentityTests.cs','apps/slate-windows/tests/SlateWindows.Tests/RescanTests.cs')
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

PR 1 runs step 0 first, then the block with the host list cut to
`AFolderWhoseFileSystemGivesNoFileIdDoesNotOpenAndSaysWhy` and
`AWeakIdentityKeepsThePathKeyAndMigratesNothing` (its filter cut to
`VaultOpenIdentityTests` and `FileRecentsStoreTests`), and the gated
FAT32/exFAT fact; the other host facts are PR 2's. Mutation run per row,
locally. CI: `windows.yml`
(all lanes), `rust.yml` (the Unix facts, unchanged, on Linux),
`swift-tests.yml` (the record change at `AppState.swift:10670-10671`
and `SidebarOrganizationDispatchTests.swift:2478-2479`, and the additive
export; the mac twins of the 22a facts).

AT, NVDA spot check on PR 1 (§4 rule 4), with the Release build and
NVDA running:
1. Open a scratch vault folder once normally, so it is in the recents,
   and close Slate.
2. From a PowerShell window, set
   `$env:SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE` to that folder's name and
   start `SlateWindows.exe` from the same window, so the app inherits it.
3. Open the vault from the welcome view's recents. NVDA speaks once
   "Could not open vault: This folder's file system gives Slate no file
   identity for it, so Slate cannot keep the vault anchored to it." The
   keys stay in the welcome view (WD4), and nothing else is spoken.
4. In the same run, open a different vault. It opens and is announced
   as usual.

The PR body records the NVDA log excerpt, and the PR stays draft until
then. Nothing else a reader hears changes.

## Acceptance

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
- [ ] `AnchoredVaultStore` compares core's identity by the read its
      `persistent` flag names; the five stores and `OpenVaultAsync` fail
      closed; `FileRecentsStore` migrated for a persistent identity and
      path-keyed, migrating nothing, for a weak one (PR 1 keeps the path
      key for every identity).
- [ ] WD24 as amended: the root's and `.slate`'s identity is
      `FileIdInfo`, else the `GetFileInformationByHandle` file index
      flagged weak, so FAT32 and exFAT vaults open (the seam fact on the
      lane, the gated fact on WB-27's VHDs). A Windows root for which
      neither read answers does not open: core refuses in `FsRoot::open`
      before `.slate` is made, with `VAULT_ROOT_IDENTITY_UNAVAILABLE` as
      the `VaultError::Io` detail. The host speaks core's detail on every
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
stores is rejected. Read the design `:306-385`, `:639-711`,
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
`std::os::unix`); `AnchoredVaultStore.cs:42-70`, `:245-260`,
`:333-345`, `:434-456`; `FileRecentsStore.cs:15-110`;
`FilesSidebarViewModel.cs:557`; `QuickSwitcherViewModel.cs:68`;
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
`session.rs:4365-4390` (the `SLATE_TEST_*` seams any build can trip).
Settle the design answers (items 0 to 10) first.

Step 0, before any code: in the elevated session where WB-27's volume
procedure mounted the FAT32 and exFAT VHDs (`$fatRoot`, `$exfatRoot`),
run the probe script in this spec's Verification (an `Add-Type` of
`GetFileInformationByHandleEx(FileIdInfo)` and
`GetFileInformationByHandle` over a folder opened with
`FILE_FLAG_BACKUP_SEMANTICS`, an NTFS temp folder as control). Per
volume it records which read answers (or its Win32 error), the index
read twice and a sibling's index, and both reads after a longer rename.
It throws if the NTFS control answers no `FileIdInfo`, if a volume
answers neither read, or if a volume that needs the fallback gives an
index that is not stable and distinct within the mount; the last two
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
the held `.slate`'s whole identity. The record, core and uniffi:
`VaultRootIdentity { volume: u64, id_high: u64, id_low: u64,
persistent: bool }` (new fields; equality covers all four), with
`FsRoot::identity() -> Option<VaultRootIdentity>` (the Unix body wraps
`fstat` as `{ volume: dev, id_high: 0, id_low: ino, persistent: true }`).
The Windows identity, in PR 1: `sys::identity(file) ->
io::Result<VaultRootIdentity>` calls the new private
`sys::file_id_info(file)` (`FileIdInfo`: the 64-bit volume serial, the
128-bit id, `persistent: true`); on any failure it logs the error at
debug level and calls the new private `sys::file_index(file)`
(`GetFileInformationByHandle`: `dwVolumeSerialNumber` zero-extended,
`id_high = 0`, `id_low = nFileIndexHigh << 32 | nFileIndexLow`,
`persistent: false`, the watcher's pair); it fails only when both fail.
Structural identity (`windows_entry::identity`) and the Trash stamp stay
`FileIdInfo`-only, as on main. WD24, in PR 1: the Windows `FsRoot::open`
calls `sys::identity` on the held root right after the hold and before
`open_or_make_slate`, and only when it fails refuses the open with
`VaultError::Io(io::Error::new(kind, a11y::VAULT_ROOT_IDENTITY_UNAVAILABLE))`.
The constant is new, core's copy: "This folder's file system gives Slate
no file identity for it, so Slate cannot keep the vault anchored to
it." The OS error is logged, not spoken, and no `.slate` is made. The
held `.slate`'s identity is read the same way and refuses the same way,
only when neither read answers. Also new in PR 1:
- Two seams any build can trip, consulted by the Windows `FsRoot::open`
  for the root's and `.slate`'s reads when the opened root's path
  contains the value: `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` makes
  the `FileIdInfo` read fail with `ERROR_INVALID_PARAMETER` (the
  fallback answers; the session opens weak), and
  `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE` makes both reads fail (the open
  refuses).
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

Then in PR 2: `AnchoredVaultStore.Open(…, expectedIdentity)` (new
parameter) and the five stores passing `session.RootIdentity()`. The
store reads the root handle it opens with the read the flag names
(`FileIdInfo` when `Persistent`, else `GetFileInformationByHandle`) and
refuses when the read fails or any field differs. A weak identity is
safe to compare within a session: the held root cannot be renamed or
deleted, so its index neither moves nor is reused, and two directories
that exist at once on one volume never share a file index — so a folder
reached through a retargeted junction is refused as with a persistent
id. `OpenVaultAsync` refuses fail-closed: it reads the identity through
the new internal seam `RootIdentityReader`
(`Func<VaultSession, VaultRootIdentity?>`) before `AddRecentVault` and
`VaultOpened`. On Windows a null identity disposes the session and
speaks `"Could not open vault: " + SlateUniffiMethods.VaultRootIdentityUnavailableDetail()`
through the same terminal path: no `VaultOpened`, no recents entry,
`ReturnedToWelcome`; a weak identity opens. Last, `FileRecentsStore`:
a persistent identity gets the identity key v2 (SHA-256 of
`{Volume:x16}-{IdHigh:x16}{IdLow:x16}`) with the one-time path-key
migration; a weak identity keeps today's path key and migrates and
deletes nothing, because a FAT index changes on a rename or move made
with Slate closed and a freed slot's index can be reused, so it can
neither follow the vault nor keep two vaults apart over time, and the
path key is what every Windows vault uses on main. Tests: `session/tests/descriptor_interior_windows.rs` (new,
`#[cfg(windows)] mod descriptor_interior_windows;` in the session test
list) with helpers `retarget(link, to)` (remove the junction, recreate it
pointing at `to`), `tree_bytes(root)`, `file_index_of(path)` (the
folder's `dwVolumeSerialNumber` and 64-bit file index) and
`file_id_info_answers(path)` (whether `FileIdInfo` succeeds), all new.

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
- `a_root_whose_file_id_info_is_unsupported_opens_with_a_weak_file_index_identity`
  (WD24 amended, PR 1): with `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED`
  set to the name of a unique temp folder `T` (removed in a drop guard)
  holding the vault `T\vault` and a junction `T\other` → `T\vault`,
  `from_filesystem(T\vault)` is `Ok`; `root_identity()` has `persistent
  == false`, `id_high == 0` and `(volume, id_low) ==
  file_index_of(T\vault)` (after asserting the NTFS index's high word is
  nonzero); `.slate` exists; a second session through `T\other` shares
  its structural-lock slot; a sibling vault outside `T` opens with
  `persistent == true` [refuse when `FileIdInfo` fails; flag the
  fallback persistent; keep only `nFileIndexLow`].
- `a_root_whose_file_id_cannot_be_read_does_not_open_and_gains_no_slate`
  (WD24, neither read answers, PR 1): with
  `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE` set to the temp root's unique name
  (removed in a drop guard), so both reads fail, `from_filesystem` is
  `Err(VaultError::Io)` whose `vault_error_detail` is exactly
  `VAULT_ROOT_IDENTITY_UNAVAILABLE`, no `root\.slate` exists, the root
  renames right after, and a sibling vault not matching the value opens
  with an identity [open with `None`; open with a path-derived identity;
  make `.slate` before the identity read; pass the OS text through].
- `a_vault_on_a_fat32_or_exfat_volume_opens_with_the_identity_its_file_system_answers`
  (`#[cfg(windows)] #[ignore]`, gated on `SLATE_WEAK_VOLUME_FAT32` /
  `SLATE_WEAK_VOLUME_EXFAT` from WB-27's VHD procedure, panicking when
  neither is set; PR 1): per volume set, a fresh vault opens; its
  `root_identity()` has `persistent == file_id_info_answers(root)`
  (expected `false` per step 0), and when weak `(volume, id_low) ==
  file_index_of(root)`; `.slate` exists; a saved note reads back [refuse
  when `FileIdInfo` fails; flag the fallback persistent].
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
- `W1AnchoredVaultStoreTests.OpenRefusesADifferentFolderAtTheVaultsPath`
  and `.EveryStoreRefusesAfterTheRootWasReplaced`: the vault opened
  through an `mklink /J` spelling that is retargeted between session open
  and store open; `Open(…, expectedIdentity)` and all four stores refuse
  [compare text; one store bypasses].
- `W1AnchoredVaultStoreTests.AWeakIdentityIsComparedByTheFileIndex`
  (PR 2): with `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` set in-process
  to the fixture's unique folder name, which the junction spelling
  contains (cleared in `finally`), `RootIdentity().Persistent` is false;
  after asserting the volume's `FILE_ID_INFO` serial differs from its
  `dwVolumeSerialNumber`, `Open(…, expectedIdentity)` admits the same
  root and refuses after the junction is retargeted to a decoy [compare
  `FileIdInfo` whatever the flag; admit a weak identity uncompared].
- `FileRecentsStoreTests.APathKeyedRecentsFileMigratesToTheIdentityKeyOnce`
  (old read, new written, old deleted, second load no-op) and
  `.TheIdentityKeyIsTheLosslessIdentity` (key text carries the high
  bits), both for a persistent identity [skip the delete; format
  `Device-Inode`].
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
contract 31 FR-10 (the root identity's two reads, the weak flag, and
WD24's refusal only when neither read answers), step 0's table appended
(dated) to the §5 Windows block of
`24_structural_identity_capability_matrix.md` and pasted in PR 1's body,
the parity matrix `--check`, the plan §11 helper names; no corpus,
census, designations or ledger change (the refusal is a `VaultError`
detail). AT: PR 1's NVDA
spot check with the Release build:
1. Open a scratch vault once, so it is in the recents.
2. Relaunch `SlateWindows.exe` from a PowerShell window where
   `$env:SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE` is that folder's name.
3. Open the vault from the recents. NVDA speaks once "Could not open
   vault: This folder's file system gives Slate no file identity for
   it, so Slate cannot keep the vault anchored to it." The keys stay in
   the welcome view, and nothing else is spoken.
4. Another vault opens normally in the same run.

The NVDA log excerpt goes in PR 1's body, and PR 1 stays draft until
then.

Not delivered: whatever the SQLite spike forces onto the recorded
fallback.

Done when all of these hold:
- Step 0 ran before any code and its table is in PR 1's body and the
  matrix (and, if FAT32 or exFAT answered `FileIdInfo`, the header says
  the fallback is a defensive path).
- The Verification block runs clean on Windows: every required fact is
  discovered by name under its filter, and every command's
  `$LASTEXITCODE` is 0 (the two core filters, `anchored`, the whole
  core suite, uniffi, fmt, clippy, workspace tests, bench compile,
  licence headers, bindings, build, the host facts, `dotnet format`,
  the parity matrix, the two scan benches within noise, and — in PR 1,
  on WB-27's VHDs — the gated FAT32/exFAT fact, discovered by
  `--ignored --list`). PR 1 cuts the host list to its two host facts,
  `AFolderWhoseFileSystemGivesNoFileIdDoesNotOpenAndSaysWhy` and
  `AWeakIdentityKeepsThePathKeyAndMigratesNothing`.
- The Unix facts are green on the rust and swift lanes.
- Each mutation was run locally.
- The NVDA spot check is recorded in PR 1's body.
- PR 1's body carries `Refs #1475` and `Refs #940` and no closing
  keyword. PR 2's body carries `Closes #1475` and `Refs #940`, and PR 2
  merges only after PR 1 is merged and both PRs' Verification runs and
  PR 1's NVDA spot check are recorded. Commit:
`feat(core,windows): the windows session holds its vault root, resolves its interior through handles and reports a lossless root identity`.
