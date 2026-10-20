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
  `canonical_path_is_computed_under_the_root_descriptor`. The re-arm
  switch `session/watch.rs:108` `WATCH_REARM_ENABLED = false` (22c).
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
   violation while the session lives; nothing is recreated at the old
   path after close, the `:618` fact's Windows twin) and
   **interior-resolution** (the interior reaches its files through the
   handle, independent of rename refusal): `sqlite3_db_filename` of the
   session's connection is the synthetic `\\.slate-fd\<token>\cache.sqlite`
   path; a `CreateFileW` of that literal string fails with
   `ERROR_PATH_NOT_FOUND` while the connection reads and writes; with the
   hook's Windows install skipped through a test seam
   (`anchored::skip_install_for_tests()`, new), `open_database_anchored`
   fails rather than opening by path; and a write through the hook lands
   in the held `.slate` even when the pathname `<opened_as>\.slate` has
   been replaced by a junction to elsewhere before the open (the seam's
   `RootAnchored` point can swap `.slate`'s *pathname* on Windows only
   before `open_or_make_slate` holds it — a new `OpenSwapPoint::
   BeforeSlateHeld` (new) fires between the root hold and the `.slate`
   open, which is exactly the window the design's §3.6(b) names). The
   mac/Linux A→B→A facts stay `cfg(unix)`.
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
`cfg(unix)` facts and gains a `cfg(windows)` sibling module
`descriptor_interior_windows.rs` (new) with the two families above. Not
changed: `root_identity()`'s and `root_display_path()`'s FFI signatures
(record fields only); macOS/Linux bodies.

## Tests

| Fact | Asserts | Mutation |
|---|---|---|
| `descriptor_interior.rs`'s portable facts (`:229`, `:293`, `:357`, `:424`, `:482`, `:670`) compile and pass on Windows | as 22a | revert the hook |
| `fs_root.rs` tests (`:1020-1140`) on Windows: `both_forms_open_create_rename_list_and_remove_the_same_files`, `a_held_cache_directory_follows_its_folder_and_never_recreates_the_old_path`, `a_held_cache_directory_never_follows_a_link`, `racing_creates_beneath_a_held_directory_all_open_the_one_file` | | |
| `descriptor_interior_windows.rs`: `a_held_root_refuses_a_rename_of_itself_and_of_an_ancestor_while_the_session_lives` | `MoveFileExW` on the root and on its parent fails with `ERROR_SHARING_VIOLATION`/`ERROR_ACCESS_DENIED`; after `drop(session)` the rename succeeds and nothing exists at the old path | open with `FILE_SHARE_DELETE` |
| `the_connection_names_the_synthetic_path_and_the_literal_path_does_not_exist` | `sqlite3_db_filename` starts with `\\.slate-fd\`; `CreateFileW` of it fails `ERROR_PATH_NOT_FOUND`; a row round-trips | open by path |
| `an_uninstalled_hook_fails_the_anchored_open_instead_of_opening_by_path` | with `skip_install_for_tests()`, `open_in` returns `Err`, no `cache.sqlite` written by path | fall back silently |
| `a_slate_junction_planted_before_the_hold_never_receives_the_interior` | at `BeforeSlateHeld`, `<root>\.slate` is replaced by a junction to an outside folder: the open refuses (`slate_refusal`) and the outside folder stays empty | follow the junction |
| `root_identity_on_windows_is_the_volume_serial_and_the_whole_128_bit_file_id` | the record equals `FILE_ID_INFO` of the held root; a synthetic id with high bits set round-trips through the record and the text form | truncate to 64 bits |
| `the_windows_structural_lock_registry_is_keyed_by_identity` | two sessions on two roots with one textual spelling (via a junction swapped between opens) get distinct slots | key by path |
| uniffi: `root_identity_crosses_the_ffi_losslessly` | three fields | drop a field |
| Windows `W1AnchoredVaultStoreTests.OpenRefusesADifferentFolderAtTheVaultsPath` | the folder swapped between session open and store open; `Open(…, expectedIdentity)` refuses with the reason | compare text only |
| `W1AnchoredVaultStoreTests.EveryStoreRefusesAfterTheRootWasReplaced` | `WorkspacePersistence`, `SearchRecentsStore`, `SidebarSettingsStore`, `GraphConfigStore` all refuse | one store bypasses |
| `FileRecentsStoreTests` (new class): `APathKeyedRecentsFileMigratesToTheIdentityKeyOnce`, `TheIdentityKeyIsTheLosslessIdentity` | old file read, new written, old deleted, second load no-op; key text carries the high bits | skip the delete; format `Device-Inode` |
| `VaultOpenIdentityTests` (new class): `OpenRefusesWhenCoreReportsNoIdentity` | fail-closed | admit null |
| `RescanTests.AReadAfterALiveRenameStillLists` (new; if NTFS refuses the rename with handles held, asserts the refusal and no recreation instead) | | revert reads |
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

PowerShell, repo root. Each test line must select ≥ 1 test (`cargo …
-- --list`; `dotnet test … --list-tests`).

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb28'
cargo test -p slate-core --locked descriptor_interior
cargo test -p slate-core --locked fs_root
cargo test -p slate-core --locked anchored
cargo test -p slate-core --locked --no-fail-fast
cargo test -p slate-uniffi --locked root_identity
cargo fmt --all -- --check
cargo clippy --all-targets --workspace -- -D warnings
cargo test --workspace --locked
cargo bench --workspace --no-run
bash scripts/check-license-headers.sh
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --filter "FullyQualifiedName~SlateWindows.Tests.W1AnchoredVaultStoreTests|FullyQualifiedName~SlateWindows.Tests.FileRecentsStoreTests|FullyQualifiedName~SlateWindows.Tests.VaultOpenIdentityTests|FullyQualifiedName~SlateWindows.Tests.RescanTests"
$cs = @('apps/slate-windows/src/SlateWindows/AnchoredVaultStore.cs','apps/slate-windows/src/SlateWindows/FileRecentsStore.cs','apps/slate-windows/src/SlateWindows/FilesSidebarViewModel.cs','apps/slate-windows/src/SlateWindows/QuickSwitcherViewModel.cs','apps/slate-windows/src/SlateWindows/VaultLifecycleViewModel.cs','apps/slate-windows/src/SlateWindows/WorkspacePersistence.cs','apps/slate-windows/src/SlateWindows/Search/SearchRecentsStore.cs','apps/slate-windows/src/SlateWindows/SidebarSettingsStore.cs','apps/slate-windows/src/SlateWindows/Graph/GraphConfigStore.cs','apps/slate-windows/tests/SlateWindows.Tests/W1AnchoredVaultStoreTests.cs','apps/slate-windows/tests/SlateWindows.Tests/FileRecentsStoreTests.cs','apps/slate-windows/tests/SlateWindows.Tests/VaultOpenIdentityTests.cs','apps/slate-windows/tests/SlateWindows.Tests/RescanTests.cs')
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs
python -I scripts/generate-parity-matrix.py --check
cargo bench -p slate-core --bench scan_bench -- 'first_open_and_scan|reopen_with_cache'   # one regex filter; both groups within noise of BENCHMARKS.md
```

Mutation run per row, locally. CI: `windows.yml` (all lanes), `rust.yml`,
`swift-tests.yml`. AT: none — no sentence changes (a new open-refusal
sentence, if one is needed, gets a spot check recorded in the PR body).

## Acceptance

- [ ] WD7 and WD22 recorded; the design section's ten answers recorded;
      22c merged and its names adopted.
- [ ] `FsRoot`, `sys`, the anchored SQLite hook (or the owner-approved
      fallback) and the provider's reads on Windows; the portable 22a
      facts and the two Windows families green on the Windows lane.
- [ ] `VaultRootIdentity` lossless on both bindings; the high-bits fact.
- [ ] `AnchoredVaultStore` compares core's identity; the five stores and
      `OpenVaultAsync` fail closed; `FileRecentsStore` migrated.
- [ ] Benches within noise; registers appended; `Closes #1475`,
      `Refs #940`.
- [ ] Not delivered: anything the SQLite spike forced onto the fallback.

## Brief

Deliver 22W. Pending decisions: WD7 (recommended: identity here; else
only the `FsRoot` body ships) and WD22 (recommended: widen the record to
`{ volume, id_high, id_low }` on both bindings and migrate the recents
key; else hash into two fields or reset recents). Read the design
`:306-385`, `:639-711`, `:737-845`; `fs_root.rs:49-60`, `:142-170`,
`:285-420`, `:536-700`, `:749-1018`; `db/anchored.rs:1-60`, `:100-170`,
`:260-280`, `:400-420`; `session.rs:1085-1120`, `:1440-1452`, `:1600`,
`:2624-2645`, `:3138-3160`, `:3196-3260`; `watch.rs:104-108`;
`windows_entry.rs:185-240`, `:378-410`, `:555-635`; `uniffi lib.rs:
618-630`, `:770-795`; `descriptor_interior.rs:1-80`;
`AnchoredVaultStore.cs:42-70`, `:245-260`, `:333-345`, `:434-456`;
`FileRecentsStore.cs:15-110`; `FilesSidebarViewModel.cs:557`;
`QuickSwitcherViewModel.cs:68`; `AppState.swift:10665-10675`. Settle the
ten design answers first. Build: the Windows `fs_root::sys` (same names
as Unix: `open_at`, `stat_at`, `read_dir_names`, `rename_at`,
`unlink_at`, `ensure_dir_at`, `identity`, `open_or_make_slate`), the
Windows `FsRoot` body holding root and `.slate` without
`FILE_SHARE_DELETE`, `db/anchored.rs`'s win-VFS body plus
`skip_install_for_tests`, `OpenSwapPoint::BeforeSlateHeld`, the lossless
`VaultRootIdentity`, provider reads through handles, the identity-keyed
lock registry; then `AnchoredVaultStore.Open(…, expectedIdentity)`, the
five stores passing `session.RootIdentity()`, `OpenVaultAsync`
fail-closed, and `FileRecentsStore`'s identity key v2 with the one-time
path-key migration. Facts, seen failing first: the portable 22a facts on
Windows; the four `fs_root.rs` tests on Windows;
`a_held_root_refuses_a_rename_of_itself_and_of_an_ancestor_while_the_session_lives`;
`the_connection_names_the_synthetic_path_and_the_literal_path_does_not_exist`;
`an_uninstalled_hook_fails_the_anchored_open_instead_of_opening_by_path`;
`a_slate_junction_planted_before_the_hold_never_receives_the_interior`;
`root_identity_on_windows_is_the_volume_serial_and_the_whole_128_bit_file_id`;
`the_windows_structural_lock_registry_is_keyed_by_identity`;
`root_identity_crosses_the_ffi_losslessly`;
`W1AnchoredVaultStoreTests.OpenRefusesADifferentFolderAtTheVaultsPath`;
`W1AnchoredVaultStoreTests.EveryStoreRefusesAfterTheRootWasReplaced`;
`FileRecentsStoreTests.APathKeyedRecentsFileMigratesToTheIdentityKeyOnce`;
`FileRecentsStoreTests.TheIdentityKeyIsTheLosslessIdentity`;
`VaultOpenIdentityTests.OpenRefusesWhenCoreReportsNoIdentity`;
`RescanTests.AReadAfterALiveRenameStillLists`. Registers: design §7/§11,
contract 31 FR-10, the parity matrix `--check`. Not delivered: whatever
the SQLite spike forces onto the recorded fallback. Done when the Windows
lane runs the facts green, both hosts compile (the `AppState.swift:
10670-10671` edit), the PowerShell gates pass, the two scan benches are
within noise, each mutation was run locally, and the PR bodies carry
`Closes #1475` and `Refs #940`. Commit:
`feat(core,windows): the windows session holds its vault root, resolves its interior through handles and reports a lossless root identity`.
