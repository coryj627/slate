# WB-28 — 22W: the Windows session holds its vault root and resolves its interior through handles, and its identity is lossless

Issues: #1475 (the identity half; closes here per §5's matrix — WB-12
delivered the `GraphConfigStore` write with `Refs`), Refs #940 (closes by
hand after 22b and 22W, RD15). Size L (design first:
`22_descriptor_relative_session_design.md` §7 row 22W, `:796`). Lanes:
rust (Windows cfg), windows, swift (compile: the FFI record changes).
AT: none. Wave: W10, alone. Depends on: WB-27 (shares `fs.rs` and
`windows_entry.rs`), plan 43 22c merged (`FsRoot` helper names agreed,
§6 gate "22c's `FsRoot` names agreed"). Parallel with: nothing; WB-29 and
WB-30 follow it. Decision: WD7 (answered: identity is 22W, here), WD22
(recommended: widen `VaultRootIdentity` losslessly; migrate
`FileRecentsStore`'s key).

## Goal

On Windows, as on macOS and Linux since 22a, a filesystem session binds
the root it opened: the vault root and `.slate` are held open for the
session's lifetime, every interior open (`cache.sqlite` and its WAL and
SHM, `structural.lock`, `slate.json`, `.slate/prefs.json`, the op-log,
`.slate/tmp`) is `NtCreateFile` with `RootDirectory` set to a held handle,
the provider's reads resolve beneath the held root, and `root_identity()`
is a real anchor read from the held handle in a form that loses no bits
of NTFS's 128-bit file id. #940's A→B→A race is closed on Windows by the
same seam and facts as 22a; #1475's "different folder now at the path"
column gets its answer: the anchored stores compare with core's identity,
and the device-local recents key follows the vault, not its path.

## Current state on main

Verified against the worktree. Two of the plan's lines drifted by one;
the §11 helper names are not what the tree holds (said below).

- **`FsRoot` is Unix-only in body.** `vault/fs_root.rs:536-549` `pub struct
  FsRoot { opened_as, identity: Option<(u64, u64)>, #[cfg(all(any(macos,
  linux), target_pointer_width = "64"))] root: fs::File, cache: CacheDir }`
  (the plan's `:536-540` is the struct's head; the cfg is `:544-547`).
  Unix body `:579-651` (`open` `:588` holds the root, `sys::identity`,
  `sys::open_or_make_slate`, `CacheDir::held`; `descriptor` `:611`;
  `verified_current_path` `:619`; `read_root_file` `:625`; `is_dir`
  `:642`). The other-platform body `:653-684`: `open` records the
  pathname with `observed_identity(root)` and `CacheDir::by_path`;
  `verified_current_path` is `None`. `ANCHORED` (`:49`) is the cfg as a
  const. The interior abstraction is `trait InteriorDir` (`:142-169`:
  `display_dir`, `ensure_self`, `ensure_child_dir`, `open_file(relative,
  FileOptions)`, `rename_file`, `remove_file`, `file_len`,
  `read_child_dir`, `sync_child_dir`) with impls for `Path` (`:191`),
  `PathBuf` (`:242`) and `CacheDir` (`:422`, Unix). The Unix syscall
  helpers are `mod sys` (`:749-1018`): `open_at` `:809`, `ensure_dir_at`
  `:878`, `open_or_make_slate` `:887`, `rename_at` `:898`, `unlink_at`
  `:920`, `stat_at` `:930`, `sync_dir` `:949`, `read_dir_names` `:960`,
  `identity` `:803`. **Discrepancy with plan §11:** there is no
  `list_dir_at` anywhere; the listing helper is `read_dir_names`, and the
  portable surface is `InteriorDir`. The names to agree with 22c are
  `sys::{open_at, stat_at, read_dir_names}` plus whatever 22c adds for the
  provider's reads (#1513 names `openat`, `fstatat`/`statx`, `fdopendir`).
- **The SQLite hook is Unix-only.** `db/anchored.rs:1-30` (doc), `:55-57`
  (macOS vs other VFS names), `register` `:112`, `database_path` `:127`,
  `install` `:400`. It overrides the unix VFS's `open`, `stat`, `lstat`,
  `access`, `unlink`, `readlink` through `xSetSystemCall`, resolving
  `/.slate-fd/<token>/<leaf>` with `raw_open_at` (`:269-275`). The session
  opens through `interior.sqlite_path("cache.sqlite")`
  (`session.rs:2639-2641`, `CacheDir::sqlite_path` `:374` Unix, `:411`
  by-path elsewhere) and `open_worker_connection(&cache_dir)` (`:1600`).
- **Identity.** `session.rs:1092-1095` `pub struct VaultRootIdentity {
  device: u64, inode: u64 }` (the plan's `:1093-1096`); the Windows
  `observe_root_identity` returns `None` (`:1108-1115`). The session sets
  `root_identity` from `held_root.identity()` (`:3206-3209`), fields at
  `:1442`, `:1450`; `root_identity()` `:3141`, `root_display_path()`
  `:3152`. FFI: `slate-uniffi/src/lib.rs:623-626` record, `:772-777`
  `root_identity`, `:786-790` `root_display_path` ("on Windows until
  Milestone W, the path the session opened"). Windows already reads the
  lossless id: `windows_entry::identity` (`:612-631`) formats
  `VolumeSerialNumber` and the 128-bit `FileId.Identifier` as text, used by
  `capture` (`:633`) for structural undo (contract 31 FR-10, `:540-556`);
  `trash_snapshot/windows.rs:235-262` `stamp` reads the same
  `FILE_ID_INFO`.
- **Hosts' consumers of the record.** Windows:
  `FileRecentsStore.cs:22-37`: key is `identity is null ?
  _vaultRoot.ToUpperInvariant() : $"{identity.Device}-{identity.Inode}"`
  (`:27-30`), SHA-256 hex (`:31`), file `%LOCALAPPDATA%\Slate\file-recents\
  <key>.json` (`:32-36`); `MigrateLegacy` (`:65-103`) reads the in-vault
  `file-recents.json` through `AnchoredVaultStore.Open(_vaultRoot,
  createDirectory: false)` and deletes it after a successful save.
  Constructed at `FilesSidebarViewModel.cs:557` and
  `QuickSwitcherViewModel.cs:68` with `session.RootIdentity()`. Mac:
  `AppState.swift:10670-10671` maps `session.rootIdentity()` into
  `SidebarVaultRootIdentity(device:inode:)`; tests construct that host type
  (`SidebarOrganizationDispatchTests.swift:1982`, `:2478`,
  `SidebarWriteJournalTests.swift:463`), not the FFI record, so a record
  change is a blind edit only at `:10670-10671`.
- **`AnchoredVaultStore.cs`** opens root and `.slate` by path per
  operation without `FILE_SHARE_DELETE` (`Open` `:58`,
  `OpenRequiredDirectory` `:245`), compares identity as text through
  `GetFinalPathNameByHandleW` (`VerifyDirectoryIdentity` `:333-342`,
  `FinalPath` `:434`), so it cannot tell a replaced folder (#1475's last
  column). `GraphConfigStore.Write` (`Graph/GraphConfigStore.cs:92-99`)
  still `Directory.CreateDirectory`s — WB-12 fixes that before this chunk.
- **The provider on Windows** pins per operation by path:
  `windows_entry::PinnedRoot::open(root: &Path)` (`:378-398`) for every
  mutation (`fs.rs` `pin_mutation_target`), `trash_handoff::pinned_path`
  (`:298`) likewise; reads are pathname-based (`#1513` lists them:
  `list_dir`, `read_file*`, `stat`, the preflights).
- **The facts to lift.** `session/tests/descriptor_interior.rs` is
  `#![cfg(unix)]` (`:16`); its facts: `:75`
  `the_interior_binds_the_anchored_root_through_an_a_b_a_swap`, `:142`,
  `:181`, `:229`, `:293`, `:357`, `:424`, `:482`, `:618`
  `after_a_live_rename_no_interior_writer_recreates_the_old_path`, `:670`.
  The re-arm switch `session/watch.rs:108` `WATCH_REARM_ENABLED = false`
  (22c turns it on).
- **The open sequence on the host:** `VaultLifecycleViewModel.cs`
  `OpenRecentAsync` `:724`, the workspace construction `:1184`,
  `WorkspaceReady` `:1280`. No identity admission on Windows today
  (design §5.3).

## Reference

- Design `22_descriptor_relative_session_design.md`: §4.1 (`:306-385`,
  the Windows paragraph at `:350-360`: `NtCreateFile` with `RootDirectory`,
  no `FILE_SHARE_DELETE`, the same VFS `xSetSystemCall` hook mapping the
  synthetic prefix, "Windows is heavier (the win VFS overrides are
  `CreateFileW`/`GetFileAttributesW`/… and the shm uses a second
  handle)"), §5.2 Windows inventory (`:639-677`), §5.3 (`:678-711`:
  "giving the Windows session a real `root_identity()` from the held
  handle is part of unifying `FsRoot` there"), §6 sketch (`:737-789`:
  `#[cfg(windows)] root: OwnedHandle, slate: OwnedHandle`), §7 row 22W
  (`:796`) and its facts line (`:815`: "the 22a core facts under the
  Windows `cargo test -p slate-core` lane plus the `AnchoredVaultStore`
  unification tests"), owner decisions Q6 (`:1069`).
- #1475's table and "Fix" section (the identity half and the "after 22W, a
  different folder at the vault's path refuses every anchored store's
  write" fact).
- Plan 44 WD22's text (§3): widen, both bindings, a fact with high bits
  set, `FileRecentsStore` switches key and migrates on first open.
- Mac twin of the admission: `AppState.swift:10073-10090`
  (`VaultOpenIdentityRaceError`, design §5.3) — Windows gains the same
  comparison in `AnchoredVaultStore`.

## Design section to review before code

Decide these with the owner and 22c's author before the branch starts;
each is a one-line answer recorded in this file's header on adoption.

1. **The shared record's shape.** `VaultRootIdentity` becomes
   `{ volume: u64, id_high: u64, id_low: u64 }` (new fields; Unix fills
   `volume = device`, `id_low = inode`, `id_high = 0`). Chosen over
   `[u8; 16]` because uniffi records carry `u64` on both bindings without
   a byte-array convention, and `windows_entry::identity`'s text form
   `"{volume:016x}-{id:032x}"` (`:620-630`) derives from it losslessly
   (the structural-identity strings stay strings). Both hosts' field
   names change: `FileRecentsStore.cs:30` and `AppState.swift:10670-10671`.
2. **Helper names with 22c.** Adopt the tree's: `fs_root::sys::{open_at,
   stat_at, read_dir_names, rename_at, unlink_at, ensure_dir_at,
   identity}` get a `#[cfg(windows)] mod sys` twin with the same names and
   signatures over `HANDLE`s (`NtCreateFile` with `RootDirectory`;
   `GetFileInformationByHandleEx(FileIdInfo | FileBasicInfo |
   FileStandardInfo)`; `NtQueryDirectoryFile`); `InteriorDir for CacheDir`
   compiles on Windows. 22c's provider-read helpers (whatever #1513 names
   them) get Windows bodies here under the same names. No `list_dir_at`.
3. **SQLite.** Verify in the bundled 3.53 `os_win.c` that the win VFS's
   `aSyscall[]` carries `CreateFileW`, `GetFileAttributesW`,
   `GetFileAttributesExW`, `DeleteFileW`, `CreateFileMappingW` and that
   `winSetSystemCall` is compiled (it is behind no option in upstream);
   `db/anchored.rs` gains a `#[cfg(windows)]` body overriding those,
   mapping `\\.slate-fd\<token>\<leaf>` to `NtCreateFile` relative to the
   registered `.slate` handle, with `-wal`, `-shm` and `-journal` by name
   under the same handle. The SHM "second handle" is `CreateFileW` of
   `<db>-shm`, which the same override serves. If the hook proves
   unworkable in the spike (first commit), the fallback is recorded, not
   improvised: open `cache.sqlite` by the held `.slate` handle's
   `GetFinalPathNameByHandleW` path re-verified by identity immediately
   before `open_database`, with the A→B→A seam proving the connection's
   identity — weaker (the design's rejected "leave SQLite pathname-based",
   `:378-383`) and therefore an owner call before it ships.
4. **The structural lock registry key** (design `:779-787`): the Windows
   key is the held `.slate`'s `(volume, id)`.
5. **The host admission.** `AnchoredVaultStore.Open` gains an
   `expectedIdentity: VaultRootIdentity?` parameter (new) and compares
   `FileIdInfo` of the root handle it opens with it, refusing on mismatch
   with a typed reason, replacing the text compare in
   `VerifyDirectoryIdentity` (kept as the per-operation re-check). The
   five stores pass `session.RootIdentity()`. `OpenVaultAsync` refuses an
   open whose identity is `null` on Windows after this chunk (fail-closed,
   as the record's doc says), which no longer happens on a real volume.
6. **`FileRecentsStore` migration.** Key = SHA-256 of
   `$"{identity.Volume:x16}-{identity.IdHigh:x16}{identity.IdLow:x16}"`;
   on first open after the change, if the identity-keyed file is absent
   and the path-keyed file (`_vaultRoot.ToUpperInvariant()` key) exists,
   `Load` reads it, saves under the new key and deletes the old file
   (one-time, mirroring `MigrateLegacy`'s in-vault step); `MigrateLegacy`
   itself keeps reading only the old in-vault file. WD22's alternative
   ("the owner records a one-time reset") drops the path-keyed step.
7. **Provider reads** go beneath the held root (`#1513`'s Windows half:
   `open_relative` `:185` already exists for mutations; reads use it with
   `FILE_READ_DATA`, listings `NtQueryDirectoryFile` on a child handle),
   following name surrogates where the pathname read follows them today.
8. **What stays by path:** display (`opened_as` and `root_display_path`
   from `GetFinalPathNameByHandleW` gated by identity, the
   `trash_handoff::final_path` `:328` + `names` `:233` pair), the host
   watcher (`SyncMarkerWatcher`), Reveal and ShellExecute (design §4.2).
9. **Size and split.** One PR for core (the Windows `FsRoot`, `sys`, hook,
   identity, lifted facts) with the FFI record change and both hosts'
   field renames; a second for the host admission and recents migration.
   Both before WB-29 and WB-30.

## Design

Per the section above, once agreed. New names: `fs_root::sys` (Windows),
`db::anchored` Windows body, `VaultRootIdentity { volume, id_high,
id_low }` (core and uniffi), `AnchoredVaultStore.Open(…, expectedIdentity)`,
`FileRecentsStore` identity key v2. `descriptor_interior.rs` drops
`#![cfg(unix)]` and gains a Windows `rename` helper (`MoveFileExW` is
blocked while handles are held without `FILE_SHARE_DELETE` — the facts
that rename a *held* root expect the Windows refusal instead and assert
nothing is recreated; the A→B→A facts swap before the hold, which the
seam's `RootAnchored` point (`session.rs:3210-3211`) allows). What is
deliberately not changed: `root_identity()`'s and `root_display_path()`'s
FFI signatures (additive record fields only); macOS/Linux bodies.

## Tests

| Fact | Asserts | Mutation |
|---|---|---|
| `descriptor_interior.rs` lifted to Windows: the ten facts listed above, run under `cargo test -p slate-core` on Windows | as 22a | revert the hook |
| `fs_root.rs` tests (`:1020-1140`) compile and pass on Windows: `both_forms_open_create_rename_list_and_remove_the_same_files`, `a_held_cache_directory_follows_its_folder_and_never_recreates_the_old_path`, `a_held_cache_directory_never_follows_a_link`, `racing_creates_beneath_a_held_directory_all_open_the_one_file` | | |
| `root_identity_on_windows_is_the_volume_serial_and_the_whole_128_bit_file_id` | the record equals `FILE_ID_INFO` of the held root; a fact over a synthetic id with high bits set round-trips through the record and the text form | truncate to 64 bits |
| `a_windows_session_refuses_a_root_whose_identity_changed_during_open` (the seam) | `VaultError` from `open`, no cache written in B | drop the compare |
| `the_windows_structural_lock_registry_is_keyed_by_identity` | two sessions on one textual path with swapped folders get distinct slots | key by path |
| `vault_tests`/`uniffi`: `root_identity_crosses_the_ffi_losslessly` | the uniffi record carries all three fields | drop a field |
| Windows: `W1AnchoredVaultStoreTests.OpenRefusesADifferentFolderAtTheVaultsPath` | swap the folder; `Open(…, expectedIdentity)` returns null/throws with the reason | compare text only |
| `W1AnchoredVaultStoreTests.EveryStoreRefusesAfterTheRootWasReplaced` (#1475's third fact) | `WorkspacePersistence`, `SearchRecentsStore`, `SidebarSettingsStore`, `GraphConfigStore` all refuse | one store bypasses |
| `FileRecentsStoreTests.APathKeyedRecentsFileMigratesToTheIdentityKeyOnce` | old file read, new written, old deleted, second load does not migrate | skip the delete |
| `FileRecentsStoreTests.TheIdentityKeyIsTheLosslessIdentity` | key text includes the high bits | format `Device-Inode` |
| `VaultLifecycleTests.OpenRefusesWhenCoreReportsNoIdentity` | fail-closed | admit null |
| `RescanTests` (existing) under a renamed open vault: `AReadAfterALiveRenameStillLists` (new) | `list_files` works after the folder is renamed with the session open (if NTFS permits the rename with the handles held; else asserts the refusal and no recreation) | revert reads |
| mac: `swift-tests.yml` compiles the renamed fields (`AppState.swift:10670-10671`) | | |

## Docs and registers

- Design `22_descriptor_relative_session_design.md` §7 row 22W: append
  "Delivered …" text as 22a's row did; §11 a dated note that the record
  widened (WD22).
- Contract 31 FR-10: append a sentence that the session's root identity is
  now core-owned on Windows and shared with structural identity's form.
- `18_windows_port/parity_matrix.md`: `generate-parity-matrix.py --check`
  must stay green (no command rows change).
- `docs/plans/44_windows_backlog_plan.md` §11's helper names: corrected by
  this spec's finding (plan author's edit, not this chunk's).
- Issues: `Closes #1475`; `Refs #940` with the 22W delivery comment, then
  the owner closes #940 by hand (RD15).

## Verification

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb28'
cargo test -p slate-core --locked descriptor_interior
cargo test -p slate-core --locked fs_root
cargo test -p slate-core --locked anchored
cargo test -p slate-core --locked --no-fail-fast
cargo test -p slate-uniffi --locked root_identity
make ci
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --filter "FullyQualifiedName~AnchoredVaultStore|FullyQualifiedName~FileRecentsStore|FullyQualifiedName~VaultLifecycle|FullyQualifiedName~RescanTests"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <edited .cs files>
python -I scripts/generate-parity-matrix.py --check
cargo bench -p slate-core --bench scan_bench -- first_open_and_scan reopen_with_cache   # within noise of BENCHMARKS.md
```

CI: `windows.yml` (rust-tests, app, model shards, FlaUI gate), `rust.yml`
(the Unix bodies unchanged), `swift-tests.yml` (compile of the record
change). AT: none — no sentence changes (an identity refusal at open
reuses the existing open-failure route; if a new sentence is needed, §4
rule 4 applies and the PR body records a spot check).

## Acceptance

- [ ] The design section's nine answers recorded; 22c merged and its
      names adopted.
- [ ] `FsRoot`, `sys`, the anchored SQLite hook (or the recorded fallback,
      owner-approved) and the provider's reads on Windows; the 22a facts
      green on the Windows lane.
- [ ] `VaultRootIdentity` lossless on both bindings; the high-bits fact.
- [ ] `AnchoredVaultStore` compares core's identity; the five stores and
      `OpenVaultAsync` fail closed; `FileRecentsStore` migrated.
- [ ] Open and rescan benches within noise; registers appended; PR bodies
      `Closes #1475`, `Refs #940`.
- [ ] Not delivered: anything the SQLite spike forced onto the fallback
      (named, with the owner's recorded approval).

## Brief

Deliver 22W. Read the design `:306-385`, `:639-711`, `:737-845`, then
`fs_root.rs:49-60`, `:142-170`, `:285-420`, `:536-700`, `:749-1018`,
`db/anchored.rs:1-60`, `:100-170`, `:260-280`, `:400-420`,
`session.rs:1085-1120`, `:1440-1452`, `:1600`, `:2624-2645`, `:3138-3160`,
`:3196-3260`, `watch.rs:104-108`, `windows_entry.rs:185-240`, `:378-410`,
`:555-635`, `uniffi lib.rs:618-630`, `:770-795`,
`descriptor_interior.rs:1-80`, `AnchoredVaultStore.cs:42-70`,
`:245-260`, `:333-345`, `:434-456`, `FileRecentsStore.cs:15-110`,
`FilesSidebarViewModel.cs:557`, `QuickSwitcherViewModel.cs:68`,
`AppState.swift:10665-10675`. Settle the design section first (record
the answers in this file). Build the Windows `sys` and `FsRoot` body, the
anchored hook, the lossless identity record across both bindings, lift the
22a facts; then the host admission and the recents migration with their
facts (names in Tests), each seen failing first. Registers: design §7/§11,
FR-10. Done when the 22a facts run green on the Windows lane, both hosts
compile, the host facts pass, and the benches are within noise. Commit:
`feat(core,windows): the windows session holds its vault root, resolves its interior through handles and reports a lossless root identity`.
