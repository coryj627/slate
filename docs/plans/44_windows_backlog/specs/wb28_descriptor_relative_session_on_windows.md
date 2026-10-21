# WB-28 — 22W: the Windows session holds its vault root and resolves its interior through handles, and its identity is lossless

Issues: #1475 (the identity half; closes here per §5's matrix — WB-12
delivered the `GraphConfigStore` write with `Refs`), Refs #940 (closes by
hand after 22b and 22W, RD15). Size L (design first:
`22_descriptor_relative_session_design.md` §7 row 22W, `:796`). Lanes:
rust (Windows cfg), windows, swift (compile: the FFI record changes).
AT: none. Wave: W10, alone. Depends on: WB-27 (shares `fs.rs` and
`windows_entry.rs`), plan 43 22c merged (`FsRoot` helper names agreed,
§6 gate). Parallel with: nothing; WB-29 and WB-30 follow it. Decision:
WD7 (pending; recommended "the identity half is 22W, here"; under the
other answer — identity parked with Milestone W — this chunk ships only
the `FsRoot` body and the host admission stays text-based). WD22
(pending; recommended "widen `VaultRootIdentity` losslessly, change the
FFI record on both bindings, migrate `FileRecentsStore`'s key on first
open"; under the other answer — hash the 128-bit id into `inode` — the
record keeps two fields, the fact with high bits set asserts the hash
instead, and the recents key is unchanged; under "the owner records a
one-time reset" the path-keyed migration step is dropped).

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
and the device-local recents key follows the vault, not its path.

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
  `trash_snapshot/windows.rs:235-262` `stamp` reads the same.
- **Hosts' consumers of the record.** Windows: `FileRecentsStore.cs:22-37`
  (key `:27-30`, hash `:31`, file `:32-36`); `MigrateLegacy` (`:65-103`);
  constructed at `FilesSidebarViewModel.cs:557` and
  `QuickSwitcherViewModel.cs:68`. Mac: `AppState.swift:10670-10671` maps
  `session.rootIdentity()` into the host's `SidebarVaultRootIdentity`;
  no Swift test constructs the FFI record (`git grep "VaultRootIdentity("
  apps/slate-mac` → only the host type), so the record change is one
  production edit at `:10670-10671`.
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

## Reference

- Design `22_descriptor_relative_session_design.md` §4.1 (`:306-385`;
  Windows at `:350-360`), §5.2 (`:639-677`), §5.3 (`:678-711`), §6
  (`:737-789`: `#[cfg(windows)] root: OwnedHandle, slate: OwnedHandle`),
  §7 row 22W (`:796`, facts `:815`), Q6 (`:1069`).
- #1475's table and "Fix" section.
- Plan 44 WD22's text (§3). Mac twin of the admission:
  `AppState.swift:10073-10090` (`VaultOpenIdentityRaceError`, design §5.3).

## Design section to review before code

Record each answer in this file's header on adoption.

1. **The record's shape (WD22).** `VaultRootIdentity { volume: u64,
   id_high: u64, id_low: u64 }` (Unix: `volume = device`, `id_low =
   inode`, `id_high = 0`); `u64`s because both bindings carry them without
   a byte-array convention, and `windows_entry::identity`'s text form
   derives losslessly. Both hosts' field reads change
   (`FileRecentsStore.cs:30`, `AppState.swift:10670-10671`).
2. **Helper names with 22c.** Adopt the tree's: a `#[cfg(windows)] mod
   sys` with the same names and signatures as the Unix one (`open_at`,
   `stat_at`, `read_dir_names`, `rename_at`, `unlink_at`, `ensure_dir_at`,
   `identity`, `open_or_make_slate`) over handles (`NtCreateFile` with
   `RootDirectory`; `GetFileInformationByHandleEx`; `NtQueryDirectoryFile`);
   `InteriorDir for CacheDir` compiles on Windows; 22c's provider-read
   helpers get Windows bodies under their names. No `list_dir_at`.
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
4. **The structural-lock registry key:** the held `.slate`'s `(volume,
   id)`.
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
6. **The host admission.** `AnchoredVaultStore.Open` gains
   `expectedIdentity: VaultRootIdentity?` (new) and compares `FileIdInfo`
   of the root handle it opens with it, refusing on mismatch; the five
   stores pass `session.RootIdentity()`; `OpenVaultAsync` refuses an open
   whose identity is `null` on Windows after this chunk.
7. **`FileRecentsStore` migration.** Key = SHA-256 of
   `$"{identity.Volume:x16}-{identity.IdHigh:x16}{identity.IdLow:x16}"`;
   on first open, if the identity-keyed file is absent and the path-keyed
   file exists, `Load` reads it, saves under the new key and deletes the
   old file; `MigrateLegacy` keeps reading only the old in-vault file.
8. **Provider reads** beneath the held root (`open_relative` `:185` with
   `FILE_READ_DATA`; listings by `NtQueryDirectoryFile` on a child
   handle), following name surrogates where the pathname read follows
   them today.
9. **What stays by path:** display (`opened_as`, `root_display_path` from
   `GetFinalPathNameByHandleW` gated by identity through
   `trash_handoff::final_path` `:328` + `names` `:233`), the host watcher,
   Reveal and ShellExecute (design §4.2).
10. **Split.** PR 1: core (Windows `FsRoot`, `sys`, hook, identity,
    facts) with the FFI record change and both hosts' field edits. PR 2:
    the host admission and the recents migration. Both before WB-29 and
    WB-30.

## Design

Per the section above once agreed. New names: `fs_root::sys` (Windows),
`db::anchored` Windows body and `skip_install_for_tests`,
`OpenSwapPoint::BeforeSlateHeld`, `VaultRootIdentity { volume, id_high,
id_low }` (core and uniffi), `AnchoredVaultStore.Open(…, expectedIdentity)`,
`FileRecentsStore` identity key v2. `descriptor_interior.rs` keeps its
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
bodies; the Unix facts.

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
| `root_identity_on_windows_is_the_volume_serial_and_the_whole_128_bit_file_id` | the record equals `FILE_ID_INFO` of the held root; a synthetic id with high bits set round-trips through the record and the text form | truncate to 64 bits |
| `the_windows_structural_lock_registry_is_keyed_by_identity` (twin of `:357`) | two sessions on two roots with one textual spelling (`outer\vault` → `A`, then `retarget(outer\vault, B)` and a second open) get distinct slots — `A`'s lock is held, `B`'s free; and a third session opened through a second spelling of `A` (`outer\other` → `A`) shares the first session's slot | key by path |
| uniffi: `root_identity_crosses_the_ffi_losslessly` | three fields | drop a field |
| Windows `W1AnchoredVaultStoreTests.OpenRefusesADifferentFolderAtTheVaultsPath` | the vault opened through a junction spelling (made with `mklink /J`, which needs no privilege, as `CanvasDocumentTests.cs:4131-4134` does); the junction retargeted to a decoy folder between session open and store open — the held root cannot be renamed — and `Open(…, expectedIdentity)` refuses with the reason | compare text only |
| `W1AnchoredVaultStoreTests.EveryStoreRefusesAfterTheRootWasReplaced` | after the same junction retarget, `WorkspacePersistence`, `SearchRecentsStore`, `SidebarSettingsStore`, `GraphConfigStore` all refuse | one store bypasses |
| `FileRecentsStoreTests` (new class): `APathKeyedRecentsFileMigratesToTheIdentityKeyOnce`, `TheIdentityKeyIsTheLosslessIdentity` | old file read, new written, old deleted, second load no-op; key text carries the high bits | skip the delete; format `Device-Inode` |
| `VaultOpenIdentityTests` (new class): `OpenRefusesWhenCoreReportsNoIdentity` | fail-closed | admit null |
| `RescanTests.ARefreshAfterTheVaultsSpellingIsRetargetedListsTheHeldVault` (new; replaces the live-rename read the Windows hold makes impossible) | the vault opened through a junction, the junction retargeted to a decoy vault; Refresh's tree lists the held vault's notes and none of the decoy's | revert the provider reads to pathname-based |
| mac: `swift-tests.yml` compiles `AppState.swift:10670-10671` | | |

## Docs and registers

- Design `22_descriptor_relative_session_design.md` §7 row 22W: append a
  "Delivered …" text as 22a's did; §11 a dated note on WD22's answer.
- Contract 31 FR-10: append a sentence that the session's root identity
  is core-owned on Windows and shares structural identity's form.
- `18_windows_port/parity_matrix.md`: `python -I scripts/generate-parity-matrix.py
  --check` stays green (no command rows change).
- Plan §11's helper names: the plan author replaces `list_dir_at`,
  `stat_at`, `open_at` with `sys::{open_at, stat_at, read_dir_names}` and
  `InteriorDir`.
- Issues: `Closes #1475`; `Refs #940` with the 22W delivery comment.

## Verification

PowerShell, repo root, on Windows. Each filter is listed first and the
block throws unless every required fact is discovered under it; every
command's `$LASTEXITCODE` is checked (no output grepping for "Failed").
The Unix facts this chunk leaves in place run on the rust and swift
lanes, not here.

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb28'
function Assert-Ok([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed ($LASTEXITCODE)" } }
function Assert-Facts([string]$crate, [string]$filter, [string[]]$facts) {
    $listed = cargo test -p $crate --locked $filter -- --list
    Assert-Ok "listing $crate $filter"
    foreach ($fact in $facts) {
        if (-not ($listed -match "(^|::)$([regex]::Escape($fact)): test$")) { throw "filter '$filter' does not select $fact" }
    }
}
$required = [ordered]@{
    'descriptor_interior_windows' = @('a_held_root_refuses_a_rename_of_itself_and_of_an_ancestor_while_the_session_lives', 'no_interior_writer_follows_a_retargeted_spelling', 'wal_connections_share_the_held_cache_across_a_retarget', 'a_second_process_on_the_textual_path_shares_the_anchored_cache', 'child_of_a_second_process_on_the_textual_path', 'root_display_path_follows_the_held_root_not_its_retargeted_spelling', 'canonical_path_is_computed_under_the_held_root_handle', 'a_junctioned_interior_folder_is_refused', 'the_connection_names_the_synthetic_path_and_the_literal_path_does_not_exist', 'an_uninstalled_hook_fails_the_anchored_open_instead_of_opening_by_path', 'a_slate_junction_planted_before_the_hold_never_receives_the_interior', 'root_identity_on_windows_is_the_volume_serial_and_the_whole_128_bit_file_id', 'the_windows_structural_lock_registry_is_keyed_by_identity')
    'fs_root' = @('both_forms_open_create_rename_list_and_remove_the_same_files', 'windows_a_held_cache_directory_refuses_a_rename_of_itself_and_of_its_parent', 'windows_a_held_cache_directory_resolves_beneath_its_handle_when_its_spelling_is_retargeted', 'windows_a_held_cache_directory_never_follows_a_junction')
}
foreach ($filter in $required.Keys) {
    Assert-Facts 'slate-core' $filter $required[$filter]
    cargo test -p slate-core --locked $filter; Assert-Ok "cargo test $filter"
}
cargo test -p slate-core --locked anchored; Assert-Ok 'anchored'
cargo test -p slate-core --locked --no-fail-fast; Assert-Ok 'the whole core suite'
Assert-Facts 'slate-uniffi' 'root_identity' @('root_identity_crosses_the_ffi_losslessly')
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
foreach ($fact in 'OpenRefusesADifferentFolderAtTheVaultsPath', 'EveryStoreRefusesAfterTheRootWasReplaced', 'APathKeyedRecentsFileMigratesToTheIdentityKeyOnce', 'TheIdentityKeyIsTheLosslessIdentity', 'OpenRefusesWhenCoreReportsNoIdentity', 'ARefreshAfterTheVaultsSpellingIsRetargetedListsTheHeldVault') {
    if (-not ($listed -match "\.$fact$")) { throw "the host filter does not select $fact" }
}
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'host facts'
$cs = @('apps/slate-windows/src/SlateWindows/AnchoredVaultStore.cs','apps/slate-windows/src/SlateWindows/FileRecentsStore.cs','apps/slate-windows/src/SlateWindows/FilesSidebarViewModel.cs','apps/slate-windows/src/SlateWindows/QuickSwitcherViewModel.cs','apps/slate-windows/src/SlateWindows/VaultLifecycleViewModel.cs','apps/slate-windows/src/SlateWindows/WorkspacePersistence.cs','apps/slate-windows/src/SlateWindows/Search/SearchRecentsStore.cs','apps/slate-windows/src/SlateWindows/SidebarSettingsStore.cs','apps/slate-windows/src/SlateWindows/Graph/GraphConfigStore.cs','apps/slate-windows/tests/SlateWindows.Tests/W1AnchoredVaultStoreTests.cs','apps/slate-windows/tests/SlateWindows.Tests/FileRecentsStoreTests.cs','apps/slate-windows/tests/SlateWindows.Tests/VaultOpenIdentityTests.cs','apps/slate-windows/tests/SlateWindows.Tests/RescanTests.cs')
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs; Assert-Ok 'dotnet format'
python -I scripts/generate-parity-matrix.py --check; Assert-Ok 'parity matrix'
cargo bench -p slate-core --bench scan_bench -- 'first_open_and_scan|reopen_with_cache'; Assert-Ok 'scan benches'   # one regex filter; both groups within noise of BENCHMARKS.md
```

Mutation run per row, locally. CI: `windows.yml` (all lanes), `rust.yml`
(the Unix facts, unchanged, on Linux), `swift-tests.yml` (the record
change; the mac twins of the 22a facts). AT: none — no sentence changes
(a new open-refusal sentence, if one is needed, gets a spot check
recorded in the PR body).

## Acceptance

- [ ] WD7 and WD22 recorded; the design section's ten answers recorded;
      22c merged and its names adopted.
- [ ] `FsRoot`, `sys`, the anchored SQLite hook (or the owner-approved
      fallback) and the provider's reads on Windows.
- [ ] No Windows obligation rests on renaming a held object: the 22a
      facts that do (`descriptor_interior.rs`, `fs_root.rs`'s three
      Unix facts) stay `cfg(unix)` and green on the rust and mac lanes,
      and their Windows twins — the pinning facts and the
      retargeted-spelling facts — are discovered by name and green on the
      Windows lane.
- [ ] `VaultRootIdentity` lossless on both bindings; the high-bits fact.
- [ ] `AnchoredVaultStore` compares core's identity; the five stores and
      `OpenVaultAsync` fail closed; `FileRecentsStore` migrated.
- [ ] Benches within noise; registers appended; `Closes #1475`,
      `Refs #940`.
- [ ] Not delivered: anything the SQLite spike forced onto the fallback.

## Brief

Deliver 22W. Pending decisions: WD7 (recommended: the identity half is
22W, here; under the other answer — identity parked with Milestone W —
only the `FsRoot` body ships and the host admission stays text-based) and
WD22 (recommended: widen the record to `{ volume, id_high, id_low }` on
both bindings and migrate the recents key on first open; under the other
answer the 128-bit id is hashed into `inode`, the record keeps two
fields, the high-bits fact asserts the hash and the recents key is
unchanged; under "the owner records a one-time reset" the path-keyed
migration step is dropped). Read the design `:306-385`, `:639-711`,
`:737-845`; `fs_root.rs:49-60`, `:142-170`, `:285-420`, `:536-700`,
`:749-1018`, `:1020-1140` (its tests); `db/anchored.rs:1-60`,
`:100-170`, `:260-280`, `:400-420`; `session.rs:1085-1120`,
`:1440-1452`, `:1600`, `:2624-2645`, `:3138-3160`, `:3196-3260`, and
the test module list `:22300-22305`; `watch.rs:104-108`;
`windows_entry.rs:185-240`, `:378-410`, `:555-635`;
`vault/test_support.rs:58-86` (`junction`); `trash_handoff_tests.rs:557-572`
(a junction vault root, the junction removed while it works);
`uniffi lib.rs:618-630`, `:770-795`; `descriptor_interior.rs` whole
(every fact but `:293` renames a held object; several use
`std::os::unix`); `AnchoredVaultStore.cs:42-70`, `:245-260`,
`:333-345`, `:434-456`; `FileRecentsStore.cs:15-110`;
`FilesSidebarViewModel.cs:557`; `QuickSwitcherViewModel.cs:68`;
`CanvasDocumentTests.cs:4131-4134` (`mklink /J` in a C# fixture);
`AppState.swift:10665-10675`. Settle the ten design answers first.

Build: the Windows `fs_root::sys` (same names as Unix: `open_at`,
`stat_at`, `read_dir_names`, `rename_at`, `unlink_at`, `ensure_dir_at`,
`identity`, `open_or_make_slate`), the Windows `FsRoot` body holding root
and `.slate` without `FILE_SHARE_DELETE`, `db/anchored.rs`'s win-VFS body
plus `skip_install_for_tests()` (new), `OpenSwapPoint::BeforeSlateHeld`
(new), the lossless `VaultRootIdentity`, provider reads through handles,
the identity-keyed lock registry; then `AnchoredVaultStore.Open(…,
expectedIdentity)` (new parameter), the five stores passing
`session.RootIdentity()`, `OpenVaultAsync` fail-closed, and
`FileRecentsStore`'s identity key v2 with the one-time path-key
migration. Tests: `session/tests/descriptor_interior_windows.rs` (new,
`#[cfg(windows)] mod descriptor_interior_windows;` in the session test
list) with helpers `retarget(link, to)` (remove the junction, recreate it
pointing at `to`) and `tree_bytes(root)` (new).

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
  the record equals the held root's `FILE_ID_INFO`; a high-bits synthetic
  id round-trips [truncate to 64 bits].
- `the_windows_structural_lock_registry_is_keyed_by_identity` (twin of
  `:357`): one spelling retargeted between two opens gives two slots; a
  second spelling of one vault shares its slot [key by path].
- uniffi `root_identity_crosses_the_ffi_losslessly`: three fields [drop a
  field].
- `W1AnchoredVaultStoreTests.OpenRefusesADifferentFolderAtTheVaultsPath`
  and `.EveryStoreRefusesAfterTheRootWasReplaced`: the vault opened
  through an `mklink /J` spelling that is retargeted between session open
  and store open; `Open(…, expectedIdentity)` and all four stores refuse
  [compare text; one store bypasses].
- `FileRecentsStoreTests.APathKeyedRecentsFileMigratesToTheIdentityKeyOnce`
  (old read, new written, old deleted, second load no-op) and
  `.TheIdentityKeyIsTheLosslessIdentity` (key text carries the high bits)
  [skip the delete; format `Device-Inode`].
- `VaultOpenIdentityTests.OpenRefusesWhenCoreReportsNoIdentity`
  [admit null].
- `RescanTests.ARefreshAfterTheVaultsSpellingIsRetargetedListsTheHeldVault`:
  after a junction retarget Refresh lists the held vault's notes, none of
  the decoy's [pathname reads].
- mac: `swift-tests.yml` compiles the `AppState.swift:10670-10671` edit
  and keeps the Unix 22a facts green.

Registers: design §7 row 22W ("Delivered …") and §11 (WD22's answer),
contract 31 FR-10, the parity matrix `--check`, the plan §11 helper names.
Not delivered: whatever the SQLite spike forces onto the recorded
fallback. Done when the Verification block runs clean on Windows — every
required fact discovered by name under its filter, every command's
`$LASTEXITCODE` 0 (the two core filters, `anchored`, the whole core
suite, uniffi, fmt, clippy, workspace tests, bench compile, licence
headers, bindings, build, the host facts, `dotnet format`, the parity
matrix, the two scan benches within noise) — the Unix facts are green on
the rust and swift lanes, each mutation was run locally, and the PR
bodies carry `Closes #1475` and `Refs #940`. Commit:
`feat(core,windows): the windows session holds its vault root, resolves its interior through handles and reports a lossless root identity`.
