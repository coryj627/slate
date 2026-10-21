# WB-30 — After a Move to Trash, Slate checks that what arrived is the confirmed item, and says so

Issues: Refs #1468 (its mac host half, plan 43 PR 41, closes it —
whichever of WB-30 and PR 41 merges last closes; §5 closure matrix).
Size L. Lanes: rust, windows, swift (the FFI records change, and a Swift
pin that mac's successful batch still speaks as today). AT: NVDA full
pass on Part 2. Wave: W11, slot 1, after WB-28, WB-24's recorded
real-Recycle-Bin results, and plan 43 PR 34's shared identity primitive
merged with green combined-head CI. Parallel with: WB-29. Decision: WD16
(pending; recommended "decouple the Windows T2 from the mac `~/.Trash`
read check; COM in Rust (`windows-rs`) beside the trash-rs path so core
keeps owning the outcome"; under the other answer — COM in the host —
Part 2 moves to C# (`IFileOperation` through CsWin32 in `apps/slate-windows`)
and the provider takes a host-supplied `SystemTrash` through a new FFI
callback interface; and if the mac check stays a programme precondition,
WB-30 waits for it). WD24 (answered 2026-10-21; amended 2026-10-21:
FAT32 and exFAT vaults keep opening — WB-28 reads the root's identity
with `FileIdInfo`, else with `GetFileInformationByHandle`'s volume serial
and file index, flagged weak (`persistent: false`), and refuses only when
neither read answers. So the mode is `Strong` only when the vault's file
system is NTFS or ReFS **and** its root identity is the persistent
`FileIdInfo` kind; `NameAndMetadataOnly` for FAT, FAT32, exFAT, a share
whose server reports another file system, or a weak root identity).
WD22 (pending, decided in WB-28; recommended "widen `VaultRootIdentity`
losslessly"; under the other answer — the 128-bit id hashed into
`inode` — this chunk is unchanged, because either record carries
`persistent`, the only part of the identity it reads).

## Goal

Decision 1's contract (`24_decision_memo.md:63-69`) comes true on
Windows: *Slate never reports a Trash as confirmed unless the object that
moved is the confirmed object and its tree matched the confirmed inventory
when re-observed at its destination.* Core's `TrashOutcome` — the one
model WB-24 defined — is produced in full: confirmed; verified by name
and metadata only (a weak-ID volume); a mismatch, reported with where the
item went and never as success; unverifiable; deleted outright; unlinked
for good. On Windows the Recycle Bin call is `IFileOperation` with a
progress sink that returns the recycled item, re-observed against the
confirmed inventory that the session hands the provider. A vault on a
volume whose file system keeps no persistent file ids (FAT, FAT32,
exFAT, or a share whose server reports another file system), or whose
root identity WB-28 could read only as a weak file index, is staged,
revalidated and verified without reading one. `Strong` needs both: an
NTFS or ReFS volume and the persistent `FileIdInfo` root identity. FAT32
and exFAT vaults open (WB-28's file-index fallback); only a Windows
folder for which neither identity read answers does not open (WD24 as
amended, WB-28). One
verifier serves every platform, so the same check that confirms a
Windows recycle confirms the scratch bin every test uses. A back end that
reports no destination — macOS until PR 41, Linux — keeps today's batch
state and sentences exactly. The macOS Rust half (`resultingItemURL`)
and the mac host stay with PR 41.

## Current state on main

Verified against the worktree; the plan's lines hold.

- **The handoff discards what the Trash returns.** `trash_handoff.rs:57-59`
  `trait SystemTrash { fn trash(&self, path: &Path) -> Result<(),
  VaultError>; }`; `PlatformTrash` (`:64-80`) uses `trash::delete(path)`
  off macOS (`:75-78`; trash-rs 5.2.6, `Cargo.lock:2425`, which pulls
  `windows` 0.56 for its own COM, `Cargo.lock:3023-3024`) and
  `macos::trash_item` (`:378-390`), whose `trashItemAtURL_resultingItemURL_error(&url, None)`
  (`:389`) discards the result; that call stays PR 41's.
- **The provider contract.** `vault/provider.rs:101` `fn delete(&self,
  relative: &str) -> Result<(), VaultError>;` (or, after WB-24's
  refused-kind branch, `-> Result<TrashOutcome, VaultError>`). The
  Windows arm (`fs.rs:2296-2318`) derives the handed path with
  `trash_handoff::pinned_path` (`:298-320`), then `self.trash.trash(&handed)`.
- **The confirmed inventory is private to the session.**
  `trash_confirmation.rs:22-26` `TrashConfirmation { token, request,
  snapshots: Vec<TrashSnapshot> }` — `snapshots` is a private field;
  `validate_item_cancellable(provider, path, cancel)` (`:121-141`) finds
  the item's index in `request.items`, re-snapshots through
  `provider.trash_snapshot_cancellable` and compares with
  `self.snapshots[index]`, returning only `()`. `TrashSnapshot`
  (`trash_snapshot.rs:17-23`): `fingerprint: String`, `is_directory`,
  `item_count`. The Windows stamp (`trash_snapshot/windows.rs:235-275`)
  **requires `FILE_ID_INFO` and hashes the volume serial and the whole
  128-bit id** (`:266-267`) before `CreationTime`, `LastWriteTime`,
  `ChangeTime` (the root's ctime T2 must exempt) and `EndOfFile`
  (`:268-275`); `known_link` `:214-231`. A failed `FileIdInfo` query
  fails the whole stamp (`:262`), and staging
  (`TrashConfirmation::capture`, `trash_confirmation.rs:43-80`, one
  `provider.trash_snapshot_cancellable` per item at `:60`) and
  revalidation (`:121-141`) both go through it — so on a volume that
  answers no file id, staging refuses before any id-free branch could
  run.
- **FAT32 and exFAT generally answer no `FileIdInfo`; after WB-28 they
  open with a weak root identity (WD24 amended).** Codex round 8, citing
  microsoft/STL PR 6187 (which restores a `GetFileInformationByHandle`
  fallback for `std::filesystem` for this reason), and the host's own
  `SidebarWriteJournal.cs:73-75` ("a filesystem that does not answer
  `FileIdInfo`, such as FAT32"): FAT does not synthesize a `FILE_ID_INFO`.
  So `windows_entry::identity` (`windows_entry.rs:612-631`) and
  `trash_snapshot/windows.rs`'s `stamp` (`:262`), both `FileIdInfo`-only,
  fail there. WB-28's Windows `FsRoot::open`
  reads the held root's and `.slate`'s identity with `FileIdInfo`, else
  with `GetFileInformationByHandle` — `dwVolumeSerialNumber` and the
  64-bit file index, the read the watcher already makes
  (`vault/watch.rs:279-307`) — flags that fallback weak (`persistent:
  false`; a FAT index changes when its entry is renamed or moved), and
  refuses only when neither read answers, before `.slate` is made. Its
  step 0 records, on WB-27's FAT32 and exFAT VHDs, which read each
  answers. So an open Windows vault always has a root identity, persistent
  or weak, and FAT32/exFAT vaults open; whether its ids can serve as
  evidence is told by the volume's file-system name **and** that
  identity's kind.
- **Every test sees a known destination.** `ScratchTrash::trash`
  (`trash_handoff.rs:561-586`) renames the item into its bin
  (`std::fs::rename`, `:580`); core's test build gives every
  `FsVaultProvider` one (`default_trash`, `:610`), and a host test
  process routes to one (`route_trash_to_scratch_bin_for_tests`, `:642`).
  The verifier that turns that destination into `Confirmed` therefore
  has to exist on every platform the core suite runs on, not only in the
  Windows arm.
- **The mac host speaks by batch state.** `AppState.swift:1237-1265`:
  `.succeeded` → "Moved {n} to Trash."; `.partial` → "Moved {k} of {n}
  to Trash." (plus not-moved and unknown clauses). A core change that
  turns a successful mac batch into `Partial` changes mac speech without
  a mac migration or VoiceOver pass — which PR 41 owns.
- **Where the outcome attaches.** Single: `session.rs:16073-16110`
  `delete_file_checked` (folder twin `:16225-16240`) validates under the
  structural lock and the fence, calls `provider.delete`, then the index
  transaction. Batch: `:15060-15078` — `faults.check(BatchFaultPoint::
  TrashWriteFence)` at `:15067` fires **before** `validate_item_cancellable`
  at `:15069-15073`, so that point models a pre-validation failure, not a
  replacement after validation; `self.provider.delete(&plan.item.path)`
  at `:15076`; buckets `trashed` / `untrashed` / `unknown` (`:15117`,
  `:15136`, `:15396-15421`). `BatchFaultPoint` (`session.rs:12920-12934`)
  has no point between validation and the handoff. FFI records
  (`slate-uniffi/src/lib.rs:3158-3183`): `BatchTrashState`,
  `BatchTrashRemainder`, `BatchTrashReport { envelope, state, op_id,
  trashed: Vec<StructuralBatchItem>, untrashed, unknown,
  bookkeeping_failures, requires_rescan }`; `BatchItemFailure` /
  `BatchFailureStage` (`:3108-3125`). FFI endpoints `:1083-1138`.
- **The single-delete signatures — every one `Result<(), VaultError>` on
  main.** Session (`session.rs`): `pub fn delete_file(&self, path: &str)`
  (`:16073`), `pub fn delete_file_staged(&self, path: &str, token: u64)`
  (`:16077`), `pub fn delete_file_staged_cancellable(&self, path: &str,
  token: u64, cancel: &CancelToken)` (`:16081-16088`) and `fn
  delete_file_checked(&self, path: &str, token: Option<u64>, cancel:
  &CancelToken)` (`:16090-16221`; its revalidation `:16156` and
  `provider.delete` `:16159-16161` sit inside the
  `with_structural_tx_cancellable` closure opened at `:16154`, which
  returns `journal_append`'s result, so anything the delete reports
  leaves the closure through a captured local, as `recreated` does);
  the folder twins `pub fn delete_folder(&self, path: &str)` (`:16225`),
  `pub fn delete_folder_staged(&self, path: &str, token: u64)` (`:16229`),
  `pub fn delete_folder_staged_cancellable(&self, path: &str, token: u64,
  cancel: &CancelToken)` (`:16233-16240`) and `fn
  delete_folder_checked(&self, path: &str, token: Option<u64>, cancel:
  &CancelToken)` (`:16242-16374`; revalidation `:16284`, delete
  `:16287-16289`). FFI (`slate-uniffi/src/lib.rs`): `pub fn
  delete_folder(&self, path: String)` (`:1078-1080`), `pub fn
  delete_file_staged_cancellable(&self, path: String, token: u64, cancel:
  Arc<CancelToken>)` (`:1099-1108`), `pub fn
  delete_folder_staged_cancellable(&self, path: String, token: u64,
  cancel: Arc<CancelToken>)` (`:1110-1119`), `pub fn
  delete_file_staged(&self, path: String, token: u64)` (`:1138-1140`),
  `pub fn delete_folder_staged(&self, path: String, token: u64)`
  (`:1142-1144`), `pub fn delete_file(&self, path: String)`
  (`:1170-1172`). WB-24's refused-kind branch returns `TrashOutcome` from
  `delete_file_checked` / `delete_folder_checked` and the staged FFI
  wrappers only; its all-kinds-accepted branch changes none of the
  fourteen and leaves the model to this chunk.
- **Who compiles against them.** Swift production:
  `AppState.swift:1697-1704`, the `cancellableSingleTrashRunner`, whose
  `Task.detached` body calls `deleteFolderStagedCancellable` (`:1700`)
  and `deleteFileStagedCancellable` (`:1702`) as statements; its type
  `CancellableSingleTrashRunner` (`:1667-1668`) returns `Void`, and its
  eight test overrides (`FileTreeMultiSelectTests.swift:4577`, `:4616`,
  `:4650`, `:4685`, `:4717`, `:4730`, `VaultRescanTests+Watcher.swift:331`,
  `VaultRescanTests.swift:904`) never call the binding. Swift tests that
  call a wrapper as a statement: `CanvasNavigatorTests.swift:1381`,
  `HistoryPanelTests.swift:504`, `:524`, `:956`, `:1050`, `:1140`,
  `:1161`, `:1375`, `MutationHarnessTests.swift:322`,
  `ScratchTrashCensusTests.swift:111`,
  `StructuralCreationGateTests.swift:167`,
  `StructuralUndoTests.swift:1080`,
  `VaultRescanTests+ChangeFeed.swift:47`, `:335`; inside
  `XCTAssertThrowsError`'s generic autoclosure:
  `FileTreeMultiSelectTests.swift:4439`, `:4605`, `:4608`. UniFFI
  0.31.2's Swift template (`macros.swift`'s `func_decl`) emits no
  `@discardableResult`, so a wrapper that returns a value, called as a
  statement, is an unused-result warning (not an error:
  `apps/slate-mac/Package.swift` sets no `-warnings-as-errors`). C#:
  production `FilesSidebarViewModel.cs:1846-1847` (statements in the
  `RunTrashWorkAsync` lambda, which returns `true`) and `:1940`
  (`DeleteFolderNote`: the unstaged `DeleteFile` through
  `TryRunSessionWork(Action)`, then "Deleted the {name} folder note.");
  tests `Censuses/ScratchTrashCensus.cs:79`, `FileManagementTests.cs:641`,
  `HistoryPanelTests.cs:85`, `PumpedSaveReentrancyTests.cs:1485`,
  `RescanTests.cs:1736`, `:2305`, and
  `tools/ParityHarness/MutationHarness.cs:335` (`ExecuteVoid(Action)`) —
  each a statement or an expression lambda C# accepts as an `Action`
  whatever the call returns. Core's callers (`vault/test_trash_census.rs:145`
  and the session tests) take `.unwrap()` or `?` and compile unchanged.
- **No replacement window a launched app can open.**
  `batch_trash_staged_cancellable` (`session.rs:14951-14958`), the batch
  entry both hosts call, always passes `&NoStructuralBatchFaults` to the
  private `batch_trash_checked`, so no `BatchFaultPoint` — a new
  `AfterTrashValidation` included — fires outside core's own tests, and
  the single deletes have no fault hook at all. The precedent for a seam
  any build can trip is `session.rs:4371` and `:4386`
  (`SLATE_TEST_SWEEP_AFTER_INTENT`, `SLATE_TEST_COMMIT_AFTER_INTENT`: an
  environment variable read with `var_os` in every build, firing for a
  path that contains its value), which WB-28 follows for
  `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` and
  `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE`, whose NVDA spot check sets the
  variable in a PowerShell window and starts the Release
  `SlateWindows.exe` from that window — the inheritance is the
  precedent; a launch with no `SLATE_LOCAL_APP_DATA` is not (plan §4
  rule 4), so this chunk's pass starts the app through WB-1's
  `scripts/windows/launch-for-field-pass.ps1`, whose child process
  inherits the variable too. WB-18 (W6, on `main` before this
  chunk) puts every `SLATE_TEST_*` a `session/tests` fact sets behind its
  `FaultGuard` (`fault_guard()`, then `_env.set`), and its census
  `no_test_sets_a_fault_variable_outside_the_guard` fails on a bare
  `set_var("SLATE_TEST_…")` there.
- **Mac consumers of the report (why `trashed` keeps its shape).**
  Production: `AppState.swift:1209-1286` (the batch sentences over
  `report.trashed.count`), `:1357`, `:1386` (`preview.append(contentsOf:
  report.trashed)`), `:1412`, `:20717`, `:20730`, `:20747` (batch-trash
  receipts), `:21476` (`invalidateTrashedBatchItems(report.trashed)`,
  typed `[StructuralBatchItem]`). Construction sites (14, every one a
  blind edit when a field is added): `BaseBatchTrashQuarantineTests.swift:139`,
  `BatchHostQuarantineTests.swift:280`, `:427`, `:522`, `:1235`, `:1969`,
  `:2041`, `BatchResultPresentationTests.swift:438`,
  `BatchTrashDirtyCloseTests.swift:78`, `FileTreeMultiSelectTests.swift:257`,
  `MarkdownPropertyRecoveryContractTests.swift:190`,
  `StructuralUndoTests.swift:145`, `:302`, `WorkspaceTabsTests.swift:83`.
- **The Windows identity primitive exists already** —
  `windows_entry::identity` (`:612-631`), `capture` (`:633`),
  `FsVaultProvider::mutation_identity` (`fs.rs:1907-1915`),
  `VaultSession::capture_structural_identity` (`session.rs:16564`), FFI
  `:974`. The plan's prerequisite (PR 34's primitive) is the
  cross-platform form #1469 adds (decision 4); the outcome's identity must
  carry that form, so the order stands.
- **Host sentences.** `FilesSidebarViewModel.cs:1869` "Moved {name} to the
  Recycle Bin."; batch `:2285-2305`. Core owns per-item refusal text only
  through `vault_error_detail` (`a11y.rs:3401-3412`).
- **Core's Windows deps.** `Cargo.toml:112-113`: `windows-sys` 0.61
  (`Wdk_Foundation`, `Wdk_Storage_FileSystem`, `Win32_Security`,
  `Win32_System_IO`; `Win32_Storage_FileSystem` from the workspace). No
  `windows` (windows-rs) crate in slate-core. In the registry cache:
  `windows-core` 0.62.2 (`Cargo.lock:3045`) whose `lib.rs:58` is `pub use
  windows_implement::implement;` — the `#[implement]` macro is an
  unconditional re-export, **not a cargo feature**; no `windows` 0.6x
  source is cached locally, so the exact feature names are confirmed
  against the registry's `Cargo.toml` in the spike below.

## Reference

- `24_trash_handoff_feasibility.md` T2 (`:125-166`): the definition, the
  ctime exemption (`:133-135`), the weak-ID wording (`:141-146`), the
  Windows paragraph (`:150-154`: "An `IFileOperationProgressSink`
  receives `psiNewlyCreated` (not probed); NULL means the item was
  deleted outright, which T2 must report as such … This means replacing
  trash-rs's Windows back end as well."); §6 acceptance evidence
  (`:236-257`); §8 (`:279-287`).
- `24_decision_memo.md`: decision 1 (`:48-69`), decision 4 (`:122-137`),
  owner decisions (`:182-200`).
- Contract 31 FR-8 (`:420-470`) and FR-10 (`:540-556`).
- WB-24's spec: the shared `TrashOutcome` enum (repeated verbatim below)
  and the `outcomes` report field; WB-24's probe log.

## Design

Two parts, one PR each, in this order: Part 1 is the core contract and
the shared verifier (no host speaks anything new, and no batch state
changes on a back end that reports no destination); Part 2 is the Windows
back end that reports destinations **and** the Windows host's sentences,
so no merged state ever says "could not verify" for an ordinary delete.
PR 41 consumes Part 1 on macOS (or, if PR 41 merges first, Part 1
consumes its contract, §2.3).

### Part 1 — the shared outcome contract and verifier (core, both bindings)

- **The one model** (identical in WB-24; if WB-24 shipped it, this part
  changes no variant and adds no field):
  ```rust
  pub enum TrashOutcome {
      Confirmed,
      VerifiedByNameAndMetadataOnly,
      Mismatch { detail: String, location: Option<String> },
      Unverifiable { detail: String },
      DeletedOutright,
      UnlinkedForGood,
  }
  ```
  `VaultProvider::delete -> Result<TrashOutcome, VaultError>`;
  `BatchTrashReport.trashed: Vec<StructuralBatchItem>` **unchanged**
  (every mac production consumer keeps compiling) plus the parallel
  `outcomes: Vec<TrashedOutcome { item, outcome }>`, one per `trashed`
  entry in order. If WB-24 did not add the field, this PR adds it and
  edits the 14 Swift literals (`outcomes: []`); the combined-head
  `swift-tests.yml` gates the merge.
- **The destination the Trash reports.** `SystemTrash::trash` becomes
  `-> Result<TrashDestination, VaultError>` with `pub(crate) enum
  TrashDestination { Known(PathBuf), Unknown, DeletedOutright }` (new),
  and `SystemTrash` gains `fn reports_destination(&self) -> bool` (new).
  `ScratchTrash` (which renames into its bin, `trash_handoff.rs:580`)
  returns `Known(bin.join(name))` and reports `true`;
  `ScratchTrash::reporting_no_destination()` (new, `#[cfg(test)]`) moves
  the same way but returns `Unknown` and reports `false`. `PlatformTrash`
  returns `Unknown` and reports `false` on Linux (trash-rs) and on macOS
  until PR 41 (PR 41 flips it, with the mac migration and its VoiceOver
  pass); on Windows it reports `false` until Part 2's shell back end
  flips it. `VaultProvider::trash_verifies_destination(&self) -> bool`
  (new; default `false`; `FsVaultProvider` forwards
  `self.trash.reports_destination()`).
- **Strong evidence only where the volume has file ids.**
  `pub enum IdentityMode { Strong, NameAndMetadataOnly }` (new).
  `windows_entry::query_file_id(file: &fs::File) -> io::Result<FILE_ID_INFO>`
  (new) becomes the one place the Trash and structural-identity code
  query `FileIdInfo`: `windows_entry::identity` (`:612-631`) and
  `trash_snapshot/windows.rs`'s `stamp` (`:235-275`, whose `FileIdInfo`
  failure at `:262` fails the whole snapshot today) both call it, and
  only the `Strong` branch reaches it. (WB-28's `FsRoot` reads the held
  root's own identity at open through its own `sys::identity` —
  `FileIdInfo`, else the file index — not through this function.)
  `IdentityMode` and `ConfirmedItem` are `pub`
  and re-exported beside `TrashSnapshot` (`vault/mod.rs:82`), since the
  public `VaultProvider` trait names them.
- **The mode is chosen by the file system and the root identity's kind
  (WD24, amended 2026-10-21).** Two new functions:
  - `trash_snapshot::identity_mode_for_file_system(name: &str,
    root_id_persistent: bool) -> IdentityMode` (new, `#[cfg(windows)]`,
    pure) is `Strong` iff `name` is `NTFS` or `ReFS` (ASCII
    case-insensitive) **and** `root_id_persistent` is `true`. Everything
    else is `NameAndMetadataOnly`: FAT, FAT32, exFAT, a share whose
    server reports another file system (WB-27's rows), an empty name, or
    any name with a weak root identity — an NTFS or ReFS volume whose
    `FileIdInfo` read failed at open, so WB-28 fell back to the file
    index, has shown it cannot be relied on for an id read now.
  - `trash_snapshot::identity_mode_for(root: &fs::File,
    root_id_persistent: bool) -> IdentityMode` (new). On Windows it
    reads the held root's volume name with
    `GetVolumeInformationByHandleW` and returns
    `identity_mode_for_file_system(name, root_id_persistent)`. A failed
    volume query is `NameAndMetadataOnly`, because the weak mode claims
    no identity. On Unix it is `Strong` (mac weak volumes are PR 38/41's).

  `identity_mode_for` reads no file id itself: the identity's kind is the
  one WB-28's open recorded — `persistent` when `FileIdInfo` answered,
  weak when only the file index did. A root for which neither read
  answers never opens (WD24, WB-28's `FsRoot::open`), so an open Windows
  vault always has one. The rule is exposed as
  `VaultProvider::trash_identity_mode(&self, relative: &str) ->
  Result<IdentityMode, VaultError>` (new; default `Ok(Strong)`).
  `FsVaultProvider` asks `identity_mode_for` on the held root's handle
  with the held identity's `persistent` (`FsRoot::identity()`, WB-28). A
  provider built by `FsVaultProvider::new`, which holds no root, opens
  the root as its Windows arm does (`windows_entry::PinnedRoot::open`,
  `windows_entry.rs:378-398`) and reads its identity once with
  WB-28's `fs_root::sys::identity` (the same two reads a session's open
  makes); if that read fails, the mode is `NameAndMetadataOnly`.
- **The no-id-read witness.** Test seam `FILE_ID_UNAVAILABLE` (new,
  `#[cfg(test)]` thread-local `Cell<bool>` in `windows_entry.rs`). While
  it is set, `query_file_id` fails with `ERROR_INVALID_PARAMETER`. It no
  longer selects a mode; it proves that the weak branch reads no id.
  Facts set it only after the vault is open (WB-28's open reads the
  root's identity through its own `sys::identity`, with its own seams),
  so any id read during staging, revalidation, the move or verification
  fails the fact. `windows_entry` is private to `vault`, so
  the seam is re-exported for the session tests as
  `crate::vault::FILE_ID_UNAVAILABLE` (`#[cfg(all(test, windows))]
  pub(crate) use`, beside the watcher's test seams at
  `vault/mod.rs:93-95`).
- **One walk per item, in the staged mode.** `stamp` takes the mode:
  `Strong` is today's bytes (volume serial and 128-bit id first);
  `NameAndMetadataOnly` never calls `query_file_id` and hashes
  `FILE_BASIC_INFO` times and attributes and `EndOfFile` only (Unix twin:
  omits `st_dev`/`st_ino`). Both omit the **root's**
  `ChangeTime`/ctime (a `root_ctime_exempt` flag in `visit`'s depth-0
  stamp). `TrashSnapshot` gains `pub mode: IdentityMode` (part of its
  equality). `VaultProvider::trash_snapshot_in_mode_cancellable(&self,
  relative: &str, mode: IdentityMode, cancel: &CancelToken) ->
  Result<TrashSnapshot, VaultError>` (new; default: `Strong` forwards to
  `trash_snapshot_cancellable`, `NameAndMetadataOnly` returns
  `InvalidArgument` — a provider whose `trash_identity_mode` is the
  default is never asked for it).
- **The confirmed observation travels with the delete.**
  `pub enum ConfirmedItem { Strong { identity: StructuralIdentity /* PR
  34's shared form */, tree: TrashSnapshot }, NameAndMetadataOnly { tree:
  TrashSnapshot } }` (new) — the weak variant has no strong field, so
  nothing forces an id read on a volume without ids.
  `TrashConfirmation::capture` (`trash_confirmation.rs:43-80`) asks
  `provider.trash_identity_mode(path)` per item, takes **one** snapshot
  in that mode through `trash_snapshot_in_mode_cancellable`, and only in
  `Strong` captures the identity (PR 34's primitive). `validate_item_cancellable`
  (`:121-141`) re-snapshots in `self.snapshots[index].mode`, so an
  ID-free item is revalidated without an id read, and a mode change
  between staging and revalidation compares unequal and reads as stale.
  `TrashConfirmation::confirmed_item(&self, path: &str) ->
  Result<ConfirmedItem, VaultError>` (new) reads what staging captured,
  so nothing is recaptured at delete time and an external replacement
  cannot be confirmed against itself.
  `VaultProvider::delete_confirmed(&self, relative: &str, confirmed:
  &ConfirmedItem) -> Result<TrashOutcome, VaultError>` (new; the default
  body calls `delete` and returns its outcome). `delete_file_checked`,
  `delete_folder_checked` and the batch loop (`:15076`) call it when a
  confirmation exists, `delete` otherwise.
- **One shared verifier, every platform.**
  `trash_snapshot::verify_destination(dest: &Path, confirmed:
  &ConfirmedItem, cancel: &CancelToken) -> TrashOutcome` (new,
  `pub(in crate::vault)`, Unix and Windows bodies): opens `dest` without
  following its leaf (Unix: the parent with `O_DIRECTORY | O_NOFOLLOW`
  and `snapshot_at(parent, leaf, …)`, `trash_snapshot.rs:53`; Windows:
  `open_relative` beneath the opened parent), then applies the mapping
  below. `FsVaultProvider::delete_confirmed` — the Unix and Windows arms
  alike — hands the item to `self.trash`, then: `Known(dest)` →
  `verify_destination`; `Unknown` → `Unverifiable { detail: "the system
  Trash reports no destination" }`; `DeletedOutright` → `DeletedOutright`;
  WB-24's unlink branch → `UnlinkedForGood`. The unconfirmed
  `FsVaultProvider::delete` maps the same three non-`Known` rows and
  answers a `Known(dest)` with `Unverifiable { detail: "nothing was
  confirmed before the move" }` — it has nothing to compare against. So
  `ScratchTrash`, which
  every core test provider and every routed host test process uses
  (`default_trash`, `trash_handoff.rs:610`;
  `route_trash_to_scratch_bin_for_tests`, `:642`), is verified by the
  same code on macOS, Linux and Windows.
- **The mapping, total** (`verify_destination`, plus the provider's
  three non-`Known` rows):

  | destination | confirmed | observation at `dest` | outcome |
  |---|---|---|---|
  | `Known(dest)` | `Strong` | identity equal, `snapshot_at(dest, Strong)` == `tree` | `Confirmed` |
  | `Known(dest)` | `Strong` | identity equal, tree differs | `Mismatch { detail: "the tree changed", location }` |
  | `Known(dest)` | `Strong` | identity differs | `Mismatch { detail: "not the confirmed item", location }` |
  | `Known(dest)` | `Strong` | the identity cannot be read | `Unverifiable { detail }` |
  | `Known(dest)` | `NameAndMetadataOnly` | `snapshot_at(dest, NameAndMetadataOnly)` == `tree`; **no id is read at any step** | `VerifiedByNameAndMetadataOnly` |
  | `Known(dest)` | `NameAndMetadataOnly` | names-and-metadata differ | `Mismatch { … }` |
  | `Known(dest)` | any | `dest` cannot be opened | `Unverifiable { detail }` |
  | `Unknown` | any | — | `Unverifiable { detail: "the system Trash reports no destination" }` |
  | `DeletedOutright` | any | — | `DeletedOutright` |
  | the unlink branch WB-24 kept | — | — | `UnlinkedForGood` |

- **Session and batch — legacy state where nothing is verified.** Every
  item that left the vault is in `trashed` with its outcome in
  `outcomes`. The batch state is computed **exactly as on main**
  (`session.rs:15396-15421`: a trashed item counts as success) unless
  the batch ran under a staged confirmation **and**
  `self.provider.trash_verifies_destination()` is `true`. So on macOS
  until PR 41, on Linux, on Windows until Part 2, for every test provider
  that keeps the default, and for any unconfirmed batch, a successful
  batch stays `Succeeded` with its outcomes all `Unverifiable`, and
  `AppState.swift:1237-1265` keeps speaking `.succeeded`'s "Moved {n} to
  Trash." rather than `.partial`'s "Moved {k} of {n} to Trash." Only a
  confirmed batch on a verifying provider turns an otherwise successful
  batch into `BatchTrashState::Partial` when an outcome is other than
  `Confirmed` / `VerifiedByNameAndMetadataOnly`. A new
  fault point `BatchFaultPoint::AfterTrashValidation` (new) fires strictly
  between a successful `validate_item_cancellable` and the handoff, so a
  fact can replace the item in that window. If WB-24 shipped
  `every_provider_but_windows_reports_unverifiable`, this PR narrows it
  to `a_trash_that_reports_no_destination_is_unverifiable` (a `ScratchTrash`
  with destinations now confirms on every platform).
- **The single delete returns its outcome through both bindings,
  whatever WB-24 shipped.** The end state is fixed, and this PR changes
  whichever of the fourteen still return `()` — all of them after WB-24's
  all-kinds-accepted branch; the four public session wrappers and the
  unstaged FFI `delete_file` / `delete_folder` after its refused-kind
  branch. Every one returns `Result<TrashOutcome, VaultError>` with its
  parameters unchanged: session `delete_file`, `delete_file_staged`,
  `delete_file_staged_cancellable`, `delete_file_checked`,
  `delete_folder`, `delete_folder_staged`,
  `delete_folder_staged_cancellable`, `delete_folder_checked`; FFI
  `delete_file`, `delete_folder`, `delete_file_staged`,
  `delete_folder_staged`, `delete_file_staged_cancellable`,
  `delete_folder_staged_cancellable`, each `Ok(self.inner.…(…)?.into())`
  through the `uniffi::Enum` mirror of `TrashOutcome` (added here with
  `TrashedOutcome` if WB-24 did not). In `*_checked`, the outcome of
  `delete_confirmed` (staged) or `delete` (unstaged) is captured in a
  local that the transaction closure sets, as it sets `recreated`, and is
  returned after the commit; every error path is unchanged. Consumers:
  the mac production runner's two calls (`AppState.swift:1700`, `:1702`)
  become `_ = try …` — `CancellableSingleTrashRunner` stays `-> Void`
  (mac ignores the outcome until PR 41) and its eight test overrides are
  untouched — and the fourteen Swift test statements listed under
  Current state become `_ = try …`, so the mac build gains no
  unused-result warning; the three `XCTAssertThrowsError` sites compile
  unchanged. These are blind edits, compiled and run by the mac lane's
  `swift-tests.yml` on the combined head. Every C# call site compiles
  unchanged, and Part 1 changes no Windows sentence; Part 2 consumes the
  outcome at `FilesSidebarViewModel.cs:1846-1847`. The folder-note
  delete (`:1940`) keeps "Deleted the {name} folder note.": it stages
  nothing, so its outcome is always the unconfirmed `Unverifiable {
  detail: "nothing was confirmed before the move" }`, the legacy rule's
  case.
- **A replacement window any build can open (the field pass's
  trigger).** `SLATE_TEST_TRASH_REPLACE_AFTER_VALIDATION` (new), read with
  `std::env::var_os` in every build — the `session.rs:4371` precedent,
  not `#[cfg(test)]` — so a Release `SlateWindows.exe` started from a
  PowerShell window that set it sees it. One new private helper,
  `VaultSession::replace_after_trash_validation_for_diagnostics(&self,
  path: &str, is_directory: bool)`, is called at exactly three places,
  each strictly after the item's successful `validate_item_cancellable`
  and before its handoff, inside the branch that holds a confirmation:
  `delete_file_checked` (after `:16156`, before the delete at
  `:16159-16161`), `delete_folder_checked` (after `:16284`, before
  `:16287-16289`) and the batch loop (after `:15069-15073`, before
  `:15076`, beside `faults.check(BatchFaultPoint::AfterTrashValidation)`).
  When the variable is set, non-empty, and the item's vault-relative path
  contains its value, the helper replaces the item: a file is rewritten
  through `self.provider.write_file(path, …)` with its own bytes
  (`read_file`) followed by the line `<!-- replaced after validation -->`,
  so its length and modification time change (and its file id too, where
  the write is an atomic replace); a folder gains the child
  `replaced-after-validation.md`. What reaches the Trash is then not the
  confirmed item, so a verifying provider reports `Mismatch` — the
  single-item `TrashMismatch` sentence, or the batch's mismatch clause.
  It is best-effort: a failed read or write is ignored, the item verifies
  `Confirmed`, and the facts and the field step read that as a failure.
  An unstaged delete never reaches it (nothing was validated). Slate never
  sets the variable; unset, the helper is one `var_os` read. The
  variable joins WB-18's `FAULT_VARIABLES` in `session/tests/common.rs`
  and the literal list of WB-18's
  `the_fault_guard_clears_every_fault_variable_on_acquisition`, and the
  core fact sets it through `fault_guard()`'s `_env.set`, as WB-18's
  census requires.

### Part 2 — the Windows back end and the Windows host's sentences

- **Spike first (one commit):** add `windows` on the 0.62 line (the
  `windows-core` 0.62.2 already in the lock) with the feature set the
  registry's `Cargo.toml` for that release lists for `IFileOperation`,
  `IShellItem`, `SHCreateItemFromParsingName`, `CoInitializeEx` and
  `IFileOperationProgressSink` — expected `Win32_UI_Shell`,
  `Win32_System_Com`, `Win32_Foundation`, `Win32_UI_Shell_Common`. The
  spike locks it with `cargo update -p windows@0.62` and records the
  resolved patch version (for example `0.62.2`) in its commit message.
  The selector carries the version because the lock already holds
  trash-rs's `windows` 0.56.0 (`Cargo.lock:3023-3024`), so a bare
  `-p windows` is ambiguous beside the new 0.62 line: cargo exits 101
  with "specification `windows` is ambiguous". The lock's two
  `windows-core` versions (0.56.0 and 0.62.2) fail the same way today
  for `cargo update -p windows-core`, while `-p windows-core@0.62`
  resolves. The pin is checked without rewriting the lock:
  `cargo metadata --format-version 1 --locked` must list `windows`
  0.62.x. Then compile a minimal `#[windows::core::implement(IFileOperationProgressSink)]`
  sink. `implement` is a macro (`windows::core::implement`, re-exported
  by `windows-core`), not a feature. If the 0.62 line cannot coexist with
  trash-rs's `windows` 0.56 (two major versions compile side by side in
  Cargo; only link-time COM GUID duplication would matter, and it does
  not), the spike records it.
- `vault/trash_handoff/windows_shell.rs` (new): `SHCreateItemFromParsingName`
  on the handed path, `IFileOperation` with `FOF_ALLOWUNDO |
  FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI | FOFX_RECYCLEONDELETE`,
  `Advise` the sink, `PerformOperations`, `GetAnyOperationsAborted`.
  `PostDeleteItem(flags, item, hrDelete, psiNewlyCreated)`: a non-NULL
  item → `GetDisplayName(SIGDN_FILESYSPATH)` → `Known(path)`; NULL with
  `S_OK` → `DeletedOutright`; failed `hrDelete` → `VaultError::Trash
  { message }` with the HRESULT text. `CoInitializeEx(COINIT_APARTMENTTHREADED)`
  on the calling thread, which the session serialises. `PlatformTrash` on
  Windows dispatches here and its `reports_destination()` becomes `true`;
  trash-rs stays for Linux. WB-24's guard and gated facts cover it
  unchanged.
- **Core copy** (`A11yEvent`, census rows, both corpus mirrors), shipped
  here with the back end that makes destinations known:
  `TrashMismatch { name, location }` — "Moved {name} to the Recycle Bin,
  but it was not the confirmed item; it is at {location}.";
  `TrashVerifiedByNameOnly { name }` — "Moved {name} to the Recycle Bin;
  verified by name and metadata only."; `TrashDeletedOutright { name }` —
  "{name} was deleted, not moved to the Recycle Bin: this volume has no
  Recycle Bin or the item was too large for it."; `TrashUnverifiable
  { name }` — "Moved {name} to the Recycle Bin; Slate could not verify
  what arrived." (`LinkDeletedForGood` is WB-24's.) The Windows host
  speaks these for a single item instead of "Moved …" when the outcome is
  not `Confirmed`; the batch sentences at `FilesSidebarViewModel.cs:2285-2305`
  append one clause per non-confirmed class with counts to
  `BatchTrashSummary`'s `Partial` sentence, whose prefix is main's
  "Moved {k} of {n} items to the Recycle Bin" (`:2312-2313`): "Moved 3 of
  3 items to the Recycle Bin; 1 was not the confirmed item; 1 verified by
  name only." The single-item sentence replaces "Moved {name} to the
  Recycle Bin." at `:1869`, chosen from the outcome that
  `DeleteFileStagedCancellable` / `DeleteFolderStagedCancellable` return
  at `:1846-1847` (the `RunTrashWorkAsync` lambda returns it instead of
  `true`). The mac host ignores `outcomes` until PR 41.

Deliberately not changed: the inventory token protocol; the macOS back
end and the mac host (PR 41, which flips macOS `reports_destination` and
owns the mac sentences and VoiceOver pass); Linux (T4 deferred; trash-rs
reports no destination, so its batch state stays main's); `trashed`'s
shape.

## Tests

Where they live: the session-flow facts in
`session/tests/trash_confirmation.rs` (filter `trash_confirmation`); the
identity-mode unit fact in `vault/trash_snapshot.rs` (filter
`trash_snapshot`); the platform-trash and Windows shell facts in
`vault/trash_handoff_tests.rs` (filter `trash_handoff`); the uniffi facts
in `slate-uniffi`'s `mod tests` (filter `trash_outcome`). Part 1 ships
every row down to the uniffi rows plus the Swift pin, the FAT32/exFAT
fact and the seam fact among them. Part 2 ships the Windows
platform and shell facts and the host rows. The FAT32/exFAT fact needs
two `#[cfg(test)]` helpers:
- `ScratchTrash::in_folder` (`trash_handoff.rs:518`) becomes
  `pub(crate)`.
- `VaultSession::from_filesystem_with_trash_for_tests(root: PathBuf,
  trash: Arc<dyn SystemTrash>) -> Result<Self, VaultError>` (new) runs
  the filesystem open, with WB-28's `FsRoot` and its root identity read
  (`FileIdInfo`, else the file index flagged weak), but
  builds the provider through `FsVaultProvider::held_with_trash(root,
  held_root, trash)` (new, beside `held`, `session.rs:3256`).

A scratch bin must sit on the vault's own volume, because `ScratchTrash`
moves by `rename`.

| Fact | Asserts | Mutation |
|---|---|---|
| `an_ordinary_trash_is_confirmed` (every platform: `ScratchTrash` with destinations, through the shared `verify_destination`) | a staged single delete's outcome is `Confirmed`; the bin holds the bytes | return `Unverifiable` from `verify_destination`; or keep the comparison in the Windows arm only (the fact then fails on the rust lane's Linux run) |
| `a_replacement_after_validation_is_reported_not_confirmed` | at `AfterTrashValidation`, `sub/note.md` is replaced by a same-name file; outcome `Mismatch { location }`, the replacement in the bin, reported in `outcomes` | recapture at delete time |
| `an_item_moved_out_of_the_vault_and_back_is_reported` | identity differs → `Mismatch { detail: "not the confirmed item" }` | skip the identity comparison |
| `a_trashed_tree_that_changed_is_reported` | a child added to a folder at `AfterTrashValidation` → `Mismatch` | drop the tree re-walk |
| `the_root_ctime_is_exempt_from_the_re_observation` | an unchanged folder whose root ctime the move changed → `Confirmed` | include the root's ctime |
| `strong_needs_ntfs_or_refs_and_a_persistent_root_id` (`#[cfg(windows)]`, unit, `vault/trash_snapshot.rs`'s `mod tests`; WD24 amended) | `identity_mode_for_file_system(name, persistent)` is `Strong` for (`NTFS`, `true`), (`ReFS`, `true`) and (`ntfs`, `true`), and `NameAndMetadataOnly` for (`NTFS`, `false`), (`ReFS`, `false`), (`exFAT`, `true`), (`exFAT`, `false`), (`FAT32`, `true`), (`FAT`, `true`), (`ext4`, `true`) (a name a share's server may report) and (`""`, `true`); and `identity_mode_for` on a held temp directory of the lane's NTFS volume is `Strong` with `true` and `NameAndMetadataOnly` with `false` | `Strong` for every name (→ `exFAT` is `Strong`); ignore the flag (→ (`NTFS`, `false`) is `Strong`); ignore the name (→ (`exFAT`, `true`) is `Strong`); `Strong` unless the name starts with `FAT` (→ `exFAT` is `Strong`); a case-sensitive compare (→ `ntfs` is `NameAndMetadataOnly`) |
| `a_vault_whose_root_identity_is_weak_is_verified_by_name_and_metadata_even_on_ntfs` (`#[cfg(windows)]`, not gated, `session/tests/trash_confirmation.rs`; WD24 amended) | with WB-28's `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` set to the vault's unique folder name through WB-18's `fault_guard()` (`_env.set`; its `Drop` removes it — a bare `set_var("SLATE_TEST_…")` fails WB-18's census; the name joins `FAULT_VARIABLES` unless WB-28 added it), the vault opens through `from_filesystem` with `root_identity()` `persistent == false`; `trash_identity_mode("a.md")` is `NameAndMetadataOnly`, while a sibling vault the value does not match is `Strong`; then, with `FILE_ID_UNAVAILABLE` set on the test thread after the open (cleared in a drop guard), a staged single delete of `a.md` is `VerifiedByNameAndMetadataOnly` with its bytes in the bin | choose the mode by the file system alone (→ `Strong`, and staging fails under the seam); pass `persistent = true` from `FsVaultProvider` whatever the held identity says (→ the same) |
| `a_vault_on_a_fat32_or_exfat_volume_opens_stages_and_verifies_by_name_and_metadata_without_reading_an_id` (`#[cfg(windows)] #[ignore = "needs SLATE_WEAK_VOLUME_FAT32 or SLATE_WEAK_VOLUME_EXFAT (WB-27's volume procedure)"]`, `session/tests/trash_confirmation.rs`; WD24 amended; replaces round 6's "ids unavailable end to end" fact) | see the four-step note below the table | refuse the open when `FileIdInfo` fails, as WD24 read before its amendment (→ the open fails on a volume without it); stage the strong snapshot whatever the mode (staging fails with the seam's `ERROR_INVALID_PARAMETER`); read the destination's id on the weak branch (→ `Unverifiable`) |
| `a_weak_id_volume_is_verified_by_name_and_metadata_without_reading_ids` (gated: `SLATE_WEAK_VOLUME_FAT32`, and `SLATE_TRASH_PROBE_VOLUME` set to the **same** FAT32 volume) | `VerifiedByNameAndMetadataOnly`; and run again with `FILE_ID_UNAVAILABLE` set on the test thread from the moment the vault is open (WB-28's open reads the root's identity, by `FileIdInfo` or the file index), still that variant (no later step reads an id) | hash ids in `NameAndMetadataOnly` |
| `a_weak_id_volume_mismatch_is_still_detected_by_name_and_metadata` (gated, same volume) | a renamed child at `AfterTrashValidation` → `Mismatch` | compare `item_count` only |
| `an_unknown_destination_is_unverifiable_never_confirmed` | through `ScratchTrash::reporting_no_destination()`, a staged single delete's outcome is `Unverifiable { detail: "the system Trash reports no destination" }` | map `Unknown` to `Confirmed` |
| `a_back_end_without_destinations_keeps_todays_batch_state` | a staged three-item batch through `ScratchTrash::reporting_no_destination()` (so `trash_verifies_destination()` is `false`, as macOS's and Linux's back ends are): state `Succeeded` exactly as on main, `trashed` three, `outcomes` three `Unverifiable` | drop the legacy guard (every `Unverifiable` turns the batch `Partial`) |
| `an_unconfirmed_batch_keeps_todays_batch_state` | an unstaged three-item batch through a verifying `ScratchTrash`: state `Succeeded`, outcomes `Unverifiable { detail: "nothing was confirmed before the move" }` | apply the `Partial` rule without a confirmation |
| `the_replace_after_validation_seam_trips_single_and_batch_deletes` (Part 1) | on the default test provider (a verifying `ScratchTrash`), holding `let mut _env = fault_guard();` with `_env.set("SLATE_TEST_TRASH_REPLACE_AFTER_VALIDATION", "wb30-seam-")`: a staged `delete_file_staged("wb30-seam-file.md", …)` returns `Mismatch { location: Some(dest), .. }` and the bytes at `dest` end with `<!-- replaced after validation -->`; a staged `delete_folder_staged("wb30-seam-folder", …)` returns `Mismatch` and the bin's folder holds `replaced-after-validation.md`; a staged batch of `plain-a.md` and `wb30-seam-batch.md` has `outcomes` `[Confirmed, Mismatch]` in `trashed` order and state `Partial`; and a staged `delete_file_staged("plain-b.md", …)` (a path the value is not in) returns `Confirmed` | drop the helper's call from `delete_file_checked` (→ the file `Confirmed`), from `delete_folder_checked` (→ the folder `Confirmed`) or from the batch loop (→ `[Confirmed, Confirmed]`); fire whatever the path (→ `plain-b.md` `Mismatch`); call it before `validate_item_cancellable` (→ the delete returns `Err`, a stale confirmation, not `Mismatch`) |
| `a_deleted_outright_destination_is_reported_as_deleted` | through `ScratchTrash::reporting_deleted_outright()` (new, `#[cfg(test)]`: removes the item and returns `DeletedOutright`) → `DeletedOutright` | map to `Unverifiable` |
| `every_outcome_variant_reaches_the_batch_report_in_order` | a staged mixed batch on a verifying provider, one item per variant a single vault can produce — `Confirmed`, `Mismatch`, `Unverifiable`, `DeletedOutright`, and on Windows `UnlinkedForGood` when WB-24 kept its unlink branch (a vault has one identity mode, chosen by its file system and its root identity's kind, so `VerifiedByNameAndMetadataOnly`'s batch path is the FAT32/exFAT fact's) — the `Mismatch` item replaced at `AfterTrashValidation`, the `Unverifiable` and `DeletedOutright` items through a `ScratchTrash::scripted(by_path)` (new, `#[cfg(test)]`: per-path destination kinds; it reports destinations) → `outcomes` in `trashed` order, state `Partial` | drop a variant |
| `the_platform_trash_off_windows_reports_no_destination` (`#[cfg(not(windows))]`, `trash_handoff_tests.rs`) | `PlatformTrash.reports_destination()` is `false` on macOS and Linux, so their batch state stays main's until PR 41 / T4 | return `true` on macOS |
| `windows_the_platform_trash_reports_destinations` (`#[cfg(windows)]`, Part 2) | `PlatformTrash.reports_destination()` is `true` once the shell back end is in | leave it `false` |
| `windows_the_shell_sink_returns_the_recycled_item` (`#[cfg(windows)] #[ignore]`, scratch volume) | `Known(path)` under `$Recycle.Bin\<SID>`, id equal | ignore the sink |
| `windows_a_bin_disabled_volume_reports_deleted_outright` (`#[ignore]`; the volume's bin disabled by the probe through `NukeOnDelete` under `HKCU\…\Explorer\BitBucket\Volume\<guid>`, restored after) | `DeletedOutright` | treat as confirmed |
| uniffi: `trash_outcome_and_trashed_outcome_cross_the_ffi` | the enum and record | drop a variant from the binding |
| uniffi (Part 1, end to end): `a_single_delete_returns_its_trash_outcome_through_the_ffi` (parent) with `single_delete_trash_outcome_child` (new; a no-op unless its variables are set), in the form of `routed_trash_child` / `routing_the_trash_sends_every_later_vaults_deletions_to_the_scratch_bin` (`lib.rs:17455-17532`) | the parent makes `bin` and `vault` (holding `plain-file.md`, `plain-folder/x.md`, `unstaged-file.md`, `unstaged-folder/y.md`, `wb30-replace-file.md`, `wb30-replace-folder/z.md`, `batch-a.md`, `wb30-replace-batch.md`) in a temp dir and runs `current_exe()` with `["tests::single_delete_trash_outcome_child", "--exact", "--nocapture"]` and `SLATE_UNIFFI_OUTCOME_BIN`, `SLATE_UNIFFI_OUTCOME_VAULT` and `SLATE_TEST_TRASH_REPLACE_AFTER_VALIDATION=wb30-replace` set on the child only, asserting its exit is success. The child routes its Trash into `bin` (`route_trash_to_scratch_bin_for_tests`, a verifying `ScratchTrash`), opens `VaultSession::open_filesystem(vault)`, scans, and through the binding, each staged call with its own `stage_trash` token: `delete_file_staged("plain-file.md")` is `TrashOutcome::Confirmed`; `delete_folder_staged("plain-folder")` is `Confirmed`; `delete_file_staged_cancellable("wb30-replace-file.md")` and `delete_folder_staged_cancellable("wb30-replace-folder")` are `Mismatch { .. }`; `delete_file("unstaged-file.md")` and `delete_folder("unstaged-folder")` are `Unverifiable { detail: "nothing was confirmed before the move" }`; `batch_trash_staged` of `batch-a.md` and `wb30-replace-batch.md` has `outcomes` `[Confirmed, Mismatch]` and state `Partial`. slate-core is built here without `cfg(test)`, as in the app, so this also proves the seam reachable outside core's tests | any one wrapper returns a constant (`Unverifiable { detail: "the system Trash reports no destination" }`) instead of core's outcome (→ its assertion fails); `delete_file_checked` returns that constant instead of the captured outcome (→ the file assertions fail); read the variable only under `#[cfg(test)]` (→ the two `Mismatch` assertions and the batch's are `Confirmed`) |
| Swift (Part 1, `BatchResultPresentationTests.swift`): `testASucceededBatchWhoseOutcomesAreUnverifiableStillSaysMovedToTrash` | a `.succeeded` report with three `trashed` and three `Unverifiable` `outcomes` announces "Moved 3 items to Trash." — main's `.succeeded` sentence (`AppState.swift:1237-1250`); the `trashReport` helper gains an `outcomes:` parameter defaulting to `[]` | announce from `outcomes`, or build the report as `.partial` |
| Windows (Part 2) `FileManagementTests.AMismatchedTrashSpeaksCoresSentenceNotMoved`, `…ADeletedOutrightTrashSpeaksCoresSentence`, `…AnUnverifiableTrashSpeaksCoresSentence`, `…AWeakIdTrashSaysVerifiedByNameOnly` | the four events, not "Moved …"; the mismatch end to end through the real core — the test process's routed scratch bin (a verifying `ScratchTrash`) and `SLATE_TEST_TRASH_REPLACE_AFTER_VALIDATION` set in-process to the fixture note's unique name (cleared in `finally`), so the staged single delete's binding returns `Mismatch` and the host speaks `TrashMismatch` from it | speak "Moved …" for every outcome; ignore the returned outcome (→ the mismatch fact hears "Moved …") |
| `FileManagementTests.ABatchWithMixedOutcomesSaysEachClassInItsSummary` (Part 2) | the batch sentence's clauses and counts | drop a clause |
| `A11yCorpusCensus` + mac mirror: the four new rows (Part 2) | each event's corpus row matches its sentence | drop a row |
| FlaUI `ShellAccessibilityTests.Trash_AVerifiedMoveStillSpeaksMoved` (`ShellAccessibilityTests.Trash.cs`, new partial; Part 2) | the ordinary sentence unchanged after the back-end swap. The speech witness is the shell gate's own desktop-scoped UIA listener, never NVDA (the gate runs with NVDA closed): Slate launched through `StartShellProcess` (`ShellAccessibilityTests.cs:117-138`, which WB-1 points at the run's `ShellLaunch.Root`) on a temp vault holding `note.md` and `other.md`, nothing open; once the window exists, a `DesktopNotificationListener` (`ShellAccessibilityTests.Announcements.cs:520-581`, NVDA's registration shape) registered through `ListenOnTheDesktop` (`:221-228`), as `ShellAccessibilityTests.Rescan.cs:90-109` does; the mark taken with `LaunchSettledMark` (`Announcements.cs:350-376`); `note` selected in the Files tree and Delete pressed; then `AwaitHeardSince(heard, process.Id, mark, [moved], TimeSpan.FromSeconds(15), logFile)` (`:384-402`), with `moved` = `SlateUniffiMethods.A11yRender(new A11yEvent.HostComposed("Moved note to the Recycle Bin.", A11yPriority.Medium))` — the line `ReportMutationResult` raises (`FilesSidebarViewModel.FileManagement.cs:160-167`): exactly that line since the mark, once, nothing else, raised as rendered | speak the unverifiable sentence for `Confirmed` (the listener hears it instead of "Moved note to the Recycle Bin.") |

The FAT32/exFAT fact asserts, for each of `SLATE_WEAK_VOLUME_FAT32` and
`SLATE_WEAK_VOLUME_EXFAT` that is set (it panics when neither is, so a
named run never passes vacuously):
1. The volume. Its file-system name is `FAT32` / `exFAT` respectively.
   A fresh vault folder and a scratch bin are made on it, and the vault
   holds `a.md`, `f/b.md` and `f/c.md`.
2. The open. `VaultSession::from_filesystem_with_trash_for_tests(root,
   ScratchTrash::in_folder(bin))` succeeds, and `root_identity()` is
   `Some`: WB-28's identity read answered — by the file-index fallback
   (`persistent == false`) where the volume answers no `FileIdInfo`, as
   WB-28's step 0 records FAT32 and exFAT generally do, else by
   `FileIdInfo` — so the WD24 refusal (neither read answers) did not
   fire. `trash_identity_mode("a.md")` is `NameAndMetadataOnly`, because
   the file-system name is not NTFS or ReFS, whatever the identity's
   kind.
3. Stage, delete and verify, with no id read. `FILE_ID_UNAVAILABLE` is
   set on the test thread now, after the open, and cleared in a drop
   guard; staging, revalidation, the move and verification all run on
   that thread. A staged batch of `a.md` and `f` runs, with `f/c.md`
   renamed to `f/d.md` at `AfterTrashValidation`. Staging and
   revalidation succeed (any id read would fail under the seam).
4. The outcomes. `a.md` is `VerifiedByNameAndMetadataOnly` with its
   bytes in the bin; `f` is `Mismatch`; the state is `Partial`.

## Docs and registers

- Contract 31 FR-8: append the T2 paragraph (outcome variants and their
  mapping, the Windows sink, the weak-ID fingerprint chosen unless the
  volume's file system is NTFS or ReFS and the root identity WB-28 read
  is the persistent `FileIdInfo` kind (WD24 as amended 2026-10-21), the
  ctime exemption); FR-10: a sentence that the Trash re-observation uses
  the shared identity.
- `24_trash_handoff_feasibility.md` §8: a dated note that the Windows
  sink was probed, with the result (append).
- Part 2: `corpus.json`, `A11yCorpusCensus.cs:57`,
  `A11yCorpusCensusTests.swift:50` (+ count `:666`);
  `docs/a11y_trigger_designations.json` rows for the four events;
  `python -I scripts/a11y_trigger_ledger.py --write`.
- Part 1: the 14 Swift `BatchTrashReport` literals, if WB-24 did not
  already add `outcomes`; `BatchResultPresentationTests.swift`'s new pin
  and its helper's `outcomes:` parameter; the sixteen Swift single-delete
  statements (`AppState.swift:1700`, `:1702` and the fourteen test
  calls) as `_ = try …`; `SLATE_TEST_TRASH_REPLACE_AFTER_VALIDATION` in
  WB-18's `FAULT_VARIABLES` (`session/tests/common.rs`) and in the
  literal list of `the_fault_guard_clears_every_fault_variable_on_acquisition`,
  and a line for it beside the other `SLATE_TEST_*` diagnostic seams in
  the PR body (never set by Slate; the field pass's trigger).
- Part 1: contract 31 FR-8 records that a back end reporting no
  destination keeps main's batch state (the legacy rule) until its host
  migrates — PR 41 for macOS.
- `w_c_matrix.md` Files tree row: the four sentences.
- `Cargo.toml` / `Cargo.lock`: the `windows` crate (licence headers are
  per source file; `& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh`).
- PR bodies: `Refs #1468` on both parts; WB-24's real-Recycle-Bin run
  result pasted per WD14's other answer if the lane cannot run it.

## Verification

PowerShell, repo root, on Windows. Each filter is listed first and the
block throws unless every required fact is discovered under it (the gated
ones by `--ignored --list`, which lists ignored facts only); every
command's `$LASTEXITCODE` is checked (no output grepping for "Failed").
Part 1's PR runs blocks 1 and 1b; Part 2's runs all four.

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb30'
function Assert-Ok([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed ($LASTEXITCODE)" } }
function Assert-Facts([string]$crate, [string]$filter, [string[]]$facts, [switch]$Ignored) {
    $listed = if ($Ignored) { cargo test -p $crate --locked $filter -- --ignored --list } else { cargo test -p $crate --locked $filter -- --list }
    Assert-Ok "listing $crate $filter"
    foreach ($fact in $facts) {
        if (-not ($listed -match "(^|::)$([regex]::Escape($fact)): test$")) { throw "filter '$filter' does not select $fact" }
    }
}
# 1. core (Part 1; Part 2 adds windows_the_platform_trash_reports_destinations)
$required = [ordered]@{
    'trash_confirmation' = @('an_ordinary_trash_is_confirmed', 'a_replacement_after_validation_is_reported_not_confirmed', 'an_item_moved_out_of_the_vault_and_back_is_reported', 'a_trashed_tree_that_changed_is_reported', 'the_root_ctime_is_exempt_from_the_re_observation', 'a_vault_whose_root_identity_is_weak_is_verified_by_name_and_metadata_even_on_ntfs', 'an_unknown_destination_is_unverifiable_never_confirmed', 'a_back_end_without_destinations_keeps_todays_batch_state', 'an_unconfirmed_batch_keeps_todays_batch_state', 'a_deleted_outright_destination_is_reported_as_deleted', 'every_outcome_variant_reaches_the_batch_report_in_order', 'the_replace_after_validation_seam_trips_single_and_batch_deletes')
    'fault_guard'        = @('the_fault_guard_clears_every_fault_variable_on_acquisition', 'no_test_sets_a_fault_variable_outside_the_guard')   # WB-18's guard facts: the new variable is in FAULT_VARIABLES and no fact here sets a SLATE_TEST_* outside the guard
    'trash_handoff'      = @()   # Part 2: 'windows_the_platform_trash_reports_destinations'
}
foreach ($filter in $required.Keys) {
    Assert-Facts 'slate-core' $filter $required[$filter]
    cargo test -p slate-core --locked $filter; Assert-Ok "cargo test $filter"
}
Assert-Facts 'slate-core' 'trash_snapshot' @('strong_needs_ntfs_or_refs_and_a_persistent_root_id')
cargo test -p slate-core --locked trash_snapshot; Assert-Ok 'trash_snapshot'
cargo test -p slate-core --locked structural_batch; Assert-Ok 'structural_batch'
cargo test -p slate-core --locked --no-fail-fast; Assert-Ok 'the whole core suite'
Assert-Facts 'slate-uniffi' 'trash_outcome' @('trash_outcome_and_trashed_outcome_cross_the_ffi', 'a_single_delete_returns_its_trash_outcome_through_the_ffi', 'single_delete_trash_outcome_child')
cargo test -p slate-uniffi --locked trash_outcome; Assert-Ok 'uniffi trash_outcome'   # the parent runs the child in its own process, where the seam variable is set
cargo fmt --all -- --check; Assert-Ok 'fmt'
cargo clippy --all-targets --workspace -- -D warnings; Assert-Ok 'clippy'
cargo test --workspace --locked; Assert-Ok 'workspace tests'
cargo bench --workspace --no-run; Assert-Ok 'bench compile'
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh; Assert-Ok 'licence headers'
./apps/slate-windows/generate-bindings.ps1 -Locked; Assert-Ok 'bindings'
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'build'
$fm = 'FullyQualifiedName~SlateWindows.Tests.FileManagementTests'
$n = (dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-build --configuration Release --list-tests --filter $fm | Select-String '^\s{4}\S').Count
if ($n -lt 1) { throw "filter selects nothing: $fm" }
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-build --configuration Release --filter $fm; Assert-Ok 'FileManagementTests (Part 1: unchanged sentences)'

# 1b. gated (Part 1), in the elevated session where WB-27's volume procedure set $fatRoot and $exfatRoot (the FAT32 and exFAT VHDs):
if (-not $fatRoot -or -not $exfatRoot) { throw 'run WB-27''s volume procedure in this session first' }
$env:SLATE_WEAK_VOLUME_FAT32 = $fatRoot; $env:SLATE_WEAK_VOLUME_EXFAT = $exfatRoot
$weak = 'a_vault_on_a_fat32_or_exfat_volume_opens_stages_and_verifies_by_name_and_metadata_without_reading_an_id'
Assert-Facts 'slate-core' $weak @($weak) -Ignored
cargo test -p slate-core --locked $weak -- --ignored --test-threads=1; Assert-Ok $weak   # runs both volumes; record the pass in Part 1's PR body

# 2. the spike's pin check and the shell back end (Part 2)
# the pin is read, never rewritten: --locked fails if Cargo.lock does not already resolve Cargo.toml (the spike's `cargo update -p windows@0.62` wrote it; a bare `-p windows` is ambiguous beside trash-rs's windows 0.56.0)
$meta = cargo metadata --format-version 1 --locked; Assert-Ok 'cargo metadata --locked'
if (-not ($meta | Select-String '"name":"windows","version":"0\.62\.')) { throw 'windows 0.62 is not in the resolved graph' }
Assert-Facts 'slate-core' 'trash_handoff' @('windows_the_platform_trash_reports_destinations')
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$filter = 'FullyQualifiedName~SlateWindows.Tests.FileManagementTests|FullyQualifiedName~SlateWindows.Tests.Censuses.A11yCorpusCensus'
$listed = dotnet test $tests --no-build --configuration Release --list-tests --filter $filter; Assert-Ok 'listing the host facts'
foreach ($fact in 'AMismatchedTrashSpeaksCoresSentenceNotMoved', 'ADeletedOutrightTrashSpeaksCoresSentence', 'AnUnverifiableTrashSpeaksCoresSentence', 'AWeakIdTrashSaysVerifiedByNameOnly', 'ABatchWithMixedOutcomesSaysEachClassInItsSummary') {
    if (-not ($listed -match "\.$fact$")) { throw "the host filter does not select $fact" }
}
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'host facts'
$gate = 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj'
$journey = 'FullyQualifiedName~SlateWindows.AccessibilityTests.ShellAccessibilityTests.Trash_AVerifiedMoveStillSpeaksMoved'
$n = (dotnet test $gate --no-build --configuration Release --list-tests --filter $journey | Select-String 'Trash_AVerifiedMoveStillSpeaksMoved').Count
if ($n -ne 1) { throw "the journey filter selects $n tests" }
dotnet test $gate --no-build --configuration Release --filter $journey; Assert-Ok 'FlaUI journey'   # desktop free, NVDA closed: the journey's witness is the desktop-scoped UIA listener, not NVDA's log
$cs = @('apps/slate-windows/src/SlateWindows/FilesSidebarViewModel.cs','apps/slate-windows/src/SlateWindows/FilesSidebarViewModel.Trash.cs','apps/slate-windows/tests/SlateWindows.Tests/FileManagementTests.cs','apps/slate-windows/tests/SlateWindows.Tests/Censuses/A11yCorpusCensus.cs','apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.Trash.cs')
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs; Assert-Ok 'dotnet format'
python -I scripts/a11y_trigger_ledger.py --write; Assert-Ok 'ledger write'
python -I scripts/a11y_trigger_ledger.py --check; Assert-Ok 'ledger check'

# 3. gated (Part 2), on the WB-24 VM, in the session where WB-24's procedure set $probe (the NTFS scratch volume) and WB-27's set $fat and $fatRoot (the FAT32 VHD):
if (-not $probe -or -not $fat -or -not $fatRoot) { throw 'run WB-24''s and WB-27''s volume procedures in this session first' }
$env:SLATE_TRASH_PROBE_VOLUME = $probe
$shell = @('windows_the_shell_sink_returns_the_recycled_item', 'windows_a_bin_disabled_volume_reports_deleted_outright', 'real_trash_on_windows_lands_in_the_volumes_recycle_bin_with_its_records')   # the last is WB-24's fact, run against the new back end
foreach ($fact in $shell) {
    Assert-Facts 'slate-core' $fact @($fact) -Ignored
    cargo test -p slate-core --locked $fact -- --ignored --test-threads=1; Assert-Ok $fact
}
$env:SLATE_TRASH_PROBE_VOLUME = "$($fat.DriveLetter):\"; $env:SLATE_WEAK_VOLUME_FAT32 = $fatRoot
foreach ($fact in 'a_weak_id_volume_is_verified_by_name_and_metadata_without_reading_ids', 'a_weak_id_volume_mismatch_is_still_detected_by_name_and_metadata') {
    Assert-Facts 'slate-core' $fact @($fact) -Ignored
    cargo test -p slate-core --locked $fact -- --ignored --test-threads=1; Assert-Ok $fact
}
```

The mac half runs on CI: `swift-tests.yml` compiles the record change
and runs `BatchResultPresentationTests` (the new pin among them) on the
combined head, and compiles Part 1's `_ = try …` edits at the Swift
single-delete call sites. Mutation run per row, locally (the Swift pin's
mutation on the mac lane's PR run). CI: `windows.yml` (all), `rust.yml`
(the portable facts and `the_platform_trash_off_windows_reports_no_destination`
on Linux), `swift-tests.yml` (green before merge). AT: NVDA full pass per
§4 rule 4, on Part 2's PR, with the Release build and NVDA running.
Launch, once, from one PowerShell window at the repo root, through
WB-1's field-pass launcher, so the app inherits the seam variable
(`AfterTrashValidation` is a test-only fault point that no launched app
reaches; the variable is the app-reachable seam, read in every build)
and writes its device state under a temporary root, never the owner's
`%LOCALAPPDATA%\Slate`. Started directly from a shell without
`SLATE_LOCAL_APP_DATA`, the app would write the owner's real recents and
device state, because WB-1 keeps the production profile paths when the
variable is absent. The launcher
(`scripts/windows/launch-for-field-pass.ps1`, `param([string]$Vault,
[switch]$Seed, [switch]$Keep, [string]$Exe)`) creates
`$env:TEMP\slate-field-pass-<yyyyMMdd-HHmmss>`, sets `SLATE_LOCAL_APP_DATA`
and `SLATE_LOG_DIR` beneath it, with `-Seed` writes a `recent-vaults.json`
naming `$Vault`, starts `$Exe` on `$Vault`, prints the root, and with
`-Keep` keeps it (the app log for the PR body is under it). It runs as a
child `pwsh`, which — like the Slate it starts — inherits this window's
environment, the seam variable included, and leaves this window's own
environment untouched:

```powershell
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; if ($LASTEXITCODE -ne 0) { throw 'build failed' }
if (-not (Test-Path -LiteralPath scripts/windows/launch-for-field-pass.ps1)) { throw 'WB-1''s field-pass launcher is not on this branch' }
$vault = Join-Path $env:TEMP ("wb30-field-pass-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $vault | Out-Null
foreach ($name in 'note', 'plain-a', 'plain-b', 'plain-c', 'plain-d', 'plain-e', 'wb30-replace-me', 'wb30-replace-me-in-a-batch') {
    Set-Content -LiteralPath (Join-Path $vault "$name.md") -Value "# $name"
}
$exe = (Resolve-Path apps/slate-windows/src/SlateWindows/bin/Release/net10.0-windows/SlateWindows.exe).Path
$env:SLATE_TEST_TRASH_REPLACE_AFTER_VALIDATION = 'wb30-replace-me'
pwsh -NoProfile -File scripts/windows/launch-for-field-pass.ps1 -Vault $vault -Seed -Keep -Exe $exe
if ($LASTEXITCODE -ne 0) { throw "field-pass launcher failed ($LASTEXITCODE)" }   # it prints the kept root; record it
# after the pass: close Slate, then
Remove-Item Env:SLATE_TEST_TRASH_REPLACE_AFTER_VALIDATION
```

Scenarios (4) and (5) open their vaults from this same running app
(Open Vault), so they share its temporary root.

In the tree, each selection deleted with Delete (a file takes no prompt): (1)
`note` alone — "Moved note to the Recycle Bin." unchanged; (2) `plain-a`,
`plain-b`, `plain-c` together — main's summary unchanged, "Moved 3 items
to the Recycle Bin."; (3a) the single-item mismatch: `wb30-replace-me`
alone — "Moved wb30-replace-me to the Recycle Bin, but it was not the
confirmed item; it is at {location}.", where {location} is the
`$Recycle.Bin` path the sink reported (recorded verbatim); (3b) the batch
mismatch: `wb30-replace-me-in-a-batch`, `plain-d` and `plain-e` together
— "Moved 3 of 3 items to the Recycle Bin; 1 was not the confirmed
item."; (4) on the FAT32 VHD vault — the `TrashVerifiedByNameOnly`
phrase; (5) on a bin-disabled volume — the `TrashDeletedOutright`
phrase. Recorded in the PR body (the NVDA log excerpt per scenario);
draft until then. Part 1 changes no Windows or mac sentence, so it needs
no AT pass; its mac state is pinned by the Swift fact and
`a_back_end_without_destinations_keeps_todays_batch_state`.

## Acceptance

- [ ] WD16 recorded; WD24 (answered 2026-10-21, amended 2026-10-21)
      applied; prerequisites merged: WB-28 (whose open reads the root's
      identity by `FileIdInfo`, else by the file index flagged weak, and
      refuses only when neither answers; its step-0 probe recorded),
      WB-24's results recorded, PR 34's identity primitive with green
      combined-head CI.
- [ ] Part 1: `TrashOutcome` (WB-24's enum, unchanged), `TrashedOutcome`,
      `outcomes` (legacy `trashed` untouched), `TrashDestination`,
      `SystemTrash::reports_destination`, `trash_verifies_destination`,
      `IdentityMode`, `identity_mode_for` and
      `identity_mode_for_file_system` (`Strong` only for NTFS or ReFS
      with a persistent root identity; the function reads no id),
      `query_file_id` and its
      `FILE_ID_UNAVAILABLE` no-id-read witness, `trash_identity_mode`,
      `trash_snapshot_in_mode_cancellable`, `ConfirmedItem` (no strong
      field on the weak variant), `delete_confirmed`, the shared
      `verify_destination` on Unix and Windows, `AfterTrashValidation`,
      and the legacy batch-state rule; the fourteen single-delete
      signatures (eight session methods, six FFI wrappers) returning
      `Result<TrashOutcome, VaultError>` whatever WB-24 shipped, proven
      end to end through the binding by the uniffi parent and child; the
      diagnostic seam `SLATE_TEST_TRASH_REPLACE_AFTER_VALIDATION` at its
      three call sites, read in every build, in WB-18's
      `FAULT_VARIABLES`; the Swift literals edited if not already, the
      Swift single-delete statements `_ = try …`, the Swift pin added;
      mac lane green with mac's successful batch still `Succeeded` and
      still "Moved {n} to Trash."
- [ ] A vault on a FAT32 and on an exFAT volume opens (WB-28's identity
      read, by the file-index fallback where the volume answers no
      `FileIdInfo`) and is staged, revalidated, moved and verified by
      name and metadata with no id read after the open (the FAT32/exFAT
      fact, run in Part 1 and recorded in its PR body). An NTFS vault
      whose root identity is weak is verified the same way (the
      non-gated NTFS fact). `Strong` needs NTFS or ReFS and a persistent
      root identity (the unit fact). The real FAT32 Recycle Bin agrees
      (Part 2's gated facts, run and recorded).
- [ ] Part 2: the Windows `IFileOperation` sink back end, spiked
      (`windows` 0.62 locked by `cargo update -p windows@0.62`, never
      the ambiguous bare `-p windows`, and listed by `cargo metadata
      --format-version 1 --locked`) and probed on the scratch volume
      (gated facts run and recorded);
      `PlatformTrash.reports_destination()` true on Windows; the four
      events on both bindings and the Windows host's single and batch
      sentences.
- [ ] The facts in the Tests table pass, seen failing first, each
      discovered by name under its filter; NVDA pass recorded on Part 2,
      its mismatch scenarios run from one PowerShell window with the seam
      variable set and the Release app started from it through WB-1's
      `scripts/windows/launch-for-field-pass.ps1 -Vault $vault -Seed -Keep
      -Exe $exe` (device state under the launcher's temporary root, never
      the owner's profile), single and batch
      phrases recorded.
- [ ] Registers appended; `Refs #1468`; "not delivered": the macOS Rust
      half, the mac host and flipping macOS's `reports_destination` (PR
      41, with its VoiceOver pass); Linux (T4 deferred).

## Brief

Make the Trash outcome truthful on Windows and shared by both hosts,
without changing any mac sentence before PR 41. Pending decision: WD16
(recommended: decouple the Windows T2 from the mac `~/.Trash` read check
and put COM in Rust (`windows-rs`) beside the trash-rs path so core keeps
owning the outcome; under the other answer — COM in the host — Part 2
moves to C# (`IFileOperation` through CsWin32 in `apps/slate-windows`)
and the provider takes a host-supplied `SystemTrash` through a new FFI
callback interface; and if the mac check stays a programme precondition,
WB-30 waits for it). Answered: WD24 (2026-10-21; amended 2026-10-21) —
FAT32 and exFAT generally answer no `FileIdInfo` (microsoft/STL PR 6187;
`SidebarWriteJournal.cs:73-75`), so WB-28 reads the root's identity with
`FileIdInfo`, else with `GetFileInformationByHandle`'s volume serial and
64-bit file index flagged weak (`persistent: false`), and refuses only
when neither answers; such vaults open. The mode is therefore `Strong`
only when the file system is NTFS or ReFS **and** the root identity is
the persistent `FileIdInfo` kind, and `NameAndMetadataOnly` for FAT,
FAT32, exFAT, a share whose server reports another file system, or a
weak root identity. Pending in WB-28: WD22 (recommended: widen
`VaultRootIdentity` losslessly; under the other answer — the id hashed
into `inode` — nothing here changes, because either record carries
`persistent`, the only part of the identity this chunk reads).
Read `24_trash_handoff_feasibility.md:125-166`,
`:236-257`, `24_decision_memo.md:48-69`, `:122-137`, `:182-200`;
`trash_handoff.rs:55-200`, `:290-345`, `:370-395`, `:490-650` (ScratchTrash
renames into its bin at `:580`; `default_trash` `:610`; the host routing
`:642`); `trash_confirmation.rs:10-141` (capture `:43-80`, revalidation
`:121-141`); `trash_snapshot.rs:1-80` (the Unix `snapshot_at` `:53`);
`trash_snapshot/windows.rs:20-70`, `:95-133`, `:210-275` (`stamp` fails
the whole snapshot when `FileIdInfo` fails, `:262`); `provider.rs:95-135`;
`fs.rs:1745-1760`, `:1905-1915`, `:2268-2320`; `session.rs:4365-4400`
(the `SLATE_TEST_*` seams any build can trip: `var_os`, path contains the
value), `:12920-12934`, `:14951-14958` (the hosts' batch entry always
passes `NoStructuralBatchFaults`, so no fault point reaches a launched
app), `:15040-15140` (validation `:15069-15073`, handoff `:15076`),
`:15396-15425`, `:16073-16374` (the eight single-delete methods, every one
`Result<(), VaultError>` on main: `delete_file(path)` `:16073`,
`delete_file_staged(path, token)` `:16077`,
`delete_file_staged_cancellable(path, token, cancel)` `:16081`,
`delete_file_checked(path, token: Option<u64>, cancel)` `:16090-16221`
with its revalidation `:16156` and handoff `:16159-16161` inside the
transaction closure opened at `:16154`, and the folder twins `:16225`,
`:16229`, `:16233`, `:16242-16374`, revalidation `:16284`, handoff
`:16287-16289`); `windows_entry.rs:612-635`; `uniffi lib.rs:1078-1172`
(the six FFI wrappers, every one `Result<(), VaultError>`: `delete_folder`
`:1078`, `delete_file_staged_cancellable` `:1099`,
`delete_folder_staged_cancellable` `:1110`, `delete_file_staged`
`:1138`, `delete_folder_staged` `:1142`, `delete_file` `:1170`),
`:3100-3185`, `:17455-17532` (the routed-trash child-process form);
`FilesSidebarViewModel.cs:1818-1872` (the single delete: the staged calls
at `:1846-1847`, "Moved …" at `:1869`), `:1926-1955` (the unstaged
folder-note delete, `:1940`), `:2278-2330` (`BatchTrashSummary`; the
`Partial` prefix "Moved {k} of {n} items to the Recycle Bin" at
`:2312-2313`); `Cargo.toml:112-120`;
`AppState.swift:1667-1668`, `:1697-1704` (the mac single-delete runner
calling `deleteFolderStagedCancellable` / `deleteFileStagedCancellable`
as statements); WB-18's spec (`FaultGuard`, `FAULT_VARIABLES`, its census
of `SLATE_TEST_*` set under `session/tests`);
`AppState.swift:1198-1270` (`.succeeded` says "Moved {n} to Trash.",
`.partial` "Moved {k} of {n} to Trash."), `:1357`, `:1386`, `:1412`,
`:20717-20747`, `:21476`; `BatchResultPresentationTests.swift:299-331`,
`:425-450`; WB-24's spec for the enum and the 14 Swift literal sites;
WB-27's spec for its volume procedure and the `SLATE_WEAK_VOLUME_*`
names; WB-28's design items 0, 1 and 6 (the step-0 probe of both reads
on the VHDs; `VaultRootIdentity`'s `persistent` flag and
`FsRoot::identity()`; the two-read identity, its seam
`SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED`, and the refusal only when
neither read answers, WD24); `vault/watch.rs:279-307` (the file-index
read WB-28 falls back to); `session.rs:3160-3260` (the filesystem open;
`FsVaultProvider::held` at `:3256`); `fs.rs:125-158` (`new` holds no
root, `held` does); for Part 2's journey and field pass,
`FilesSidebarViewModel.FileManagement.cs:160-167` (`ReportMutationResult`
raises a `HostComposed` line at `Medium`), the shell gate's speech
witness in `tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.Announcements.cs`
(`:221-228` `ListenOnTheDesktop`, `:264-402` `AwaitHeard`, `HeardFrom`,
`QuietMark`, `LaunchSettledMark`, `AwaitHeardSince`, `:411-430`
`AssertRaisedAsRendered`, `:520-581` `DesktopNotificationListener`),
`ShellAccessibilityTests.Rescan.cs:80-130` (the listener registered once
the window exists, then a mark per step), `ShellAccessibilityTests.cs:117-138`
(`StartShellProcess`), and WB-1's spec for
`scripts/windows/launch-for-field-pass.ps1` (`-Vault`, `-Seed`, `-Keep`,
`-Exe`).

Build, Part 1 (core, both bindings; no host speaks anything new):
`TrashDestination { Known, Unknown, DeletedOutright }` and
`SystemTrash::reports_destination()` (ScratchTrash `true`; `PlatformTrash`
`false` on Linux, on macOS until PR 41, on Windows until Part 2), test
constructors `ScratchTrash::reporting_no_destination()`,
`::reporting_deleted_outright()` and `::scripted(by_path)`;
`VaultProvider::trash_verifies_destination()` (default `false`);
`IdentityMode { Strong, NameAndMetadataOnly }`.
`windows_entry::query_file_id` is the one `FileIdInfo` query of the
Trash and structural-identity code, reached only on the `Strong` branch.
Its `#[cfg(test)]` thread-local seam `FILE_ID_UNAVAILABLE` no longer
chooses a mode; it is the witness that the weak branch reads no id, set
by facts only after the vault is open (WB-28's open reads the root's
identity through its own `sys::identity`). The mode, by file system and
root identity kind (WD24 amended):
- the new pure `#[cfg(windows)] trash_snapshot::identity_mode_for_file_system(name,
  root_id_persistent)` is `Strong` iff the name is `NTFS` or `ReFS`
  (ASCII case-insensitive) and `root_id_persistent` is `true`, else
  `NameAndMetadataOnly` (FAT, FAT32, exFAT, a share whose server reports
  another file system, an empty name, or any name with a weak root
  identity);
- `trash_snapshot::identity_mode_for(root, root_id_persistent)` reads
  the held root's volume name with `GetVolumeInformationByHandleW` and
  applies that function. A failed volume query is `NameAndMetadataOnly`,
  and the function never reads an id. On Unix it is `Strong`.

The rule sits behind `VaultProvider::trash_identity_mode`, which
`FsVaultProvider` answers with its held root's handle and
`FsRoot::identity()`'s `persistent` (WB-28); a provider built by
`FsVaultProvider::new` opens the root through
`windows_entry::PinnedRoot::open` and reads its identity once with
WB-28's `fs_root::sys::identity`, a failed read giving
`NameAndMetadataOnly`;
`stamp` by mode (the weak mode never queries an id), the root-ctime
exemption, `TrashSnapshot.mode`, `trash_snapshot_in_mode_cancellable`;
`ConfirmedItem::{Strong { identity, tree }, NameAndMetadataOnly { tree }}`
captured at staging with one walk in the item's mode;
revalidation in the staged mode; `TrashConfirmation::confirmed_item`;
`VaultProvider::delete_confirmed`; the shared
`trash_snapshot::verify_destination(dest, confirmed, cancel)` on Unix and
Windows, called by `FsVaultProvider::delete_confirmed` for a `Known`
destination (the unconfirmed `delete` answers `Known` with `Unverifiable
{ "nothing was confirmed before the move" }`); the total mapping (Strong:
identity and tree → `Confirmed`, tree differs or identity differs →
`Mismatch`, identity unreadable → `Unverifiable`; weak: ID-free tree →
`VerifiedByNameAndMetadataOnly` or `Mismatch`; unopenable or `Unknown` →
`Unverifiable`; `DeletedOutright`; WB-24's `UnlinkedForGood`);
`BatchFaultPoint::AfterTrashValidation`; `outcomes:
Vec<TrashedOutcome>` beside the unchanged `trashed`; and the batch-state
rule: main's state unless the batch ran under a staged confirmation and
the provider verifies destinations, only then `Partial` for an outcome
other than `Confirmed`/`VerifiedByNameAndMetadataOnly`. If WB-24 shipped
`every_provider_but_windows_reports_unverifiable`, narrow it to
`a_trash_that_reports_no_destination_is_unverifiable`. The single delete
returns its outcome whatever WB-24 shipped (its all-kinds-accepted branch
changes none of these; its refused-kind branch only `*_checked` and the
staged FFI wrappers): the eight session methods and six FFI wrappers
listed above all return `Result<TrashOutcome, VaultError>`, parameters
unchanged, the FFI through the `uniffi::Enum` mirror (added here with
`TrashedOutcome` if WB-24 did not); `*_checked` captures the outcome of
`delete_confirmed` (staged) or `delete` (unstaged) in a local the
transaction closure sets, as it sets `recreated`. Blind mac edits (the
mac lane compiles them): `AppState.swift:1700` and `:1702` become `_ =
try …` (`CancellableSingleTrashRunner` stays `-> Void`, its eight test
overrides untouched), and so do the fourteen statement calls in
`CanvasNavigatorTests.swift:1381`, `HistoryPanelTests.swift:504`, `:524`,
`:956`, `:1050`, `:1140`, `:1161`, `:1375`,
`MutationHarnessTests.swift:322`, `ScratchTrashCensusTests.swift:111`,
`StructuralCreationGateTests.swift:167`, `StructuralUndoTests.swift:1080`,
`VaultRescanTests+ChangeFeed.swift:47`, `:335` (UniFFI's Swift emits no
`@discardableResult`); `FileTreeMultiSelectTests.swift:4439`, `:4605`,
`:4608` (inside `XCTAssertThrowsError`) compile unchanged. Every C# call
site (`FilesSidebarViewModel.cs:1846-1847`, `:1940`;
`ScratchTrashCensus.cs:79`, `FileManagementTests.cs:641`,
`HistoryPanelTests.cs:85`, `PumpedSaveReentrancyTests.cs:1485`,
`RescanTests.cs:1736`, `:2305`; `ParityHarness/MutationHarness.cs:335`)
compiles unchanged; Part 1 speaks nothing new, and the folder-note delete
keeps "Deleted the {name} folder note." (unstaged: always the unconfirmed
`Unverifiable`). The field pass's trigger: the new diagnostic seam
`SLATE_TEST_TRASH_REPLACE_AFTER_VALIDATION`, read with `var_os` in every
build (the `:4371` precedent, never `#[cfg(test)]`), through the new
private `replace_after_trash_validation_for_diagnostics(&self, path:
&str, is_directory: bool)`, called only after a successful
`validate_item_cancellable` and before the handoff in
`delete_file_checked`, `delete_folder_checked` and the batch loop: for a
set, non-empty value that the item's path contains, a file is rewritten
through `provider.write_file` with its own bytes plus `<!-- replaced
after validation -->`, a folder gains `replaced-after-validation.md`, so a
verifying provider reports `Mismatch` (best-effort; a failed write leaves
`Confirmed`, which the facts read as failure). It joins WB-18's
`FAULT_VARIABLES` and the literal list of
`the_fault_guard_clears_every_fault_variable_on_acquisition`. Part 2: the spike
pinning `windows` 0.62 with the registry-verified feature set (locked by
`cargo update -p windows@0.62`, its resolved patch version recorded in
the commit — never the bare `-p windows`, which cargo refuses as
ambiguous because trash-rs keeps `windows` 0.56.0 in `Cargo.lock` — and
checked by `cargo metadata --format-version 1 --locked` listing `windows`
0.62.x) and a
compiling `#[windows::core::implement(IFileOperationProgressSink)]` sink,
`vault/trash_handoff/windows_shell.rs` behind `PlatformTrash` (now
reporting destinations), the four `A11yEvent`s (`TrashMismatch`,
`TrashVerifiedByNameOnly`, `TrashDeletedOutright`, `TrashUnverifiable`)
with corpus and census rows, and the Windows host's single and batch
sentences — the single sentence chosen from the outcome the staged
wrappers return at `FilesSidebarViewModel.cs:1846-1847` (the
`RunTrashWorkAsync` lambda returns it instead of `true`), the batch
clauses appended to `BatchTrashSummary`'s `Partial` sentence ("Moved 3 of
3 items to the Recycle Bin; 1 was not the confirmed item; 1 verified by
name only.").

Facts (mutation in brackets), seen failing first:
- `an_ordinary_trash_is_confirmed` (every platform, ScratchTrash through
  `verify_destination`): `Confirmed`, bytes in the bin [return
  `Unverifiable`; or verify in the Windows arm only].
- `a_replacement_after_validation_is_reported_not_confirmed`: a same-name
  replacement at `AfterTrashValidation` → `Mismatch { location }`
  [recapture at delete time].
- `an_item_moved_out_of_the_vault_and_back_is_reported`: identity differs
  → `Mismatch` [skip the identity comparison].
- `a_trashed_tree_that_changed_is_reported`: a child added at
  `AfterTrashValidation` → `Mismatch` [drop the re-walk].
- `the_root_ctime_is_exempt_from_the_re_observation`: an unchanged folder
  whose root ctime the move changed → `Confirmed` [include it].
- `strong_needs_ntfs_or_refs_and_a_persistent_root_id` (Windows unit,
  `vault/trash_snapshot.rs`): `identity_mode_for_file_system(name,
  persistent)` is `Strong` for (`NTFS`, `true`), (`ReFS`, `true`),
  (`ntfs`, `true`) and `NameAndMetadataOnly` for (`NTFS`, `false`),
  (`ReFS`, `false`), (`exFAT`, `true`), (`exFAT`, `false`), (`FAT32`,
  `true`), (`FAT`, `true`), (`ext4`, `true`), (`""`, `true`);
  `identity_mode_for` on a held NTFS temp directory is `Strong` with
  `true` and `NameAndMetadataOnly` with `false` [`Strong` for every
  name; ignore the flag; ignore the name; `Strong` unless the name
  starts with `FAT`; a case-sensitive compare].
- `a_vault_whose_root_identity_is_weak_is_verified_by_name_and_metadata_even_on_ntfs`
  (Windows, not gated, `session/tests/trash_confirmation.rs`, Part 1):
  with WB-28's `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` set to the
  vault's unique folder name through WB-18's `fault_guard()` (`_env.set`,
  removed by its `Drop`; a bare `set_var` fails WB-18's census; the name
  joins `FAULT_VARIABLES` unless WB-28 added it), the vault opens with
  `root_identity()` `persistent == false`; `trash_identity_mode("a.md")`
  is `NameAndMetadataOnly` while a sibling vault not matching is
  `Strong`; with `FILE_ID_UNAVAILABLE` set after the open (drop guard), a
  staged single delete of `a.md` is `VerifiedByNameAndMetadataOnly`, its
  bytes in the bin [choose by file system alone; pass `persistent =
  true` from `FsVaultProvider` regardless].
- `a_vault_on_a_fat32_or_exfat_volume_opens_stages_and_verifies_by_name_and_metadata_without_reading_an_id`
  (Windows, `#[ignore]`, gated on `SLATE_WEAK_VOLUME_FAT32` /
  `SLATE_WEAK_VOLUME_EXFAT` from WB-27's VHD procedure, run in Part 1).
  Per volume set (panicking when neither is set):
  1. The volume reports `FAT32` / `exFAT`. A vault and a scratch bin are
     made on it, with `a.md`, `f/b.md` and `f/c.md`.
  2. Open through the new `#[cfg(test)]`
     `VaultSession::from_filesystem_with_trash_for_tests(root,
     ScratchTrash::in_folder(bin))`. It opens: `root_identity()` is
     `Some` — WB-28's file-index fallback (`persistent == false`) where
     the volume answers no `FileIdInfo`, as step 0 records FAT32 and
     exFAT generally do, else `FileIdInfo`. `trash_identity_mode` is
     `NameAndMetadataOnly`: the name is not NTFS or ReFS.
  3. Then, with `FILE_ID_UNAVAILABLE` set (cleared in a drop guard), a
     staged batch of `a.md` and `f` runs, with `f/c.md` renamed at
     `AfterTrashValidation`. Staging and revalidation succeed;
     `a.md` → `VerifiedByNameAndMetadataOnly`, its bytes in the bin;
     `f` → `Mismatch`; state `Partial`.

  [refuse the open when `FileIdInfo` fails, as WD24 read before its
  amendment (the open fails); stage the strong snapshot regardless of
  mode; read the destination's id on the weak branch].
- `a_weak_id_volume_is_verified_by_name_and_metadata_without_reading_ids`
  (gated, FAT32, probe volume the same): `VerifiedByNameAndMetadataOnly`,
  also with the seam set from the moment the vault is open (WB-28's open
  has read the root's identity by then) [hash ids in the weak mode].
- `a_weak_id_volume_mismatch_is_still_detected_by_name_and_metadata`
  (gated): a renamed child → `Mismatch` [compare `item_count` only].
- `an_unknown_destination_is_unverifiable_never_confirmed`: `Unknown` →
  `Unverifiable { "the system Trash reports no destination" }` [map to
  `Confirmed`].
- `a_back_end_without_destinations_keeps_todays_batch_state`: a staged
  three-item batch through `reporting_no_destination()` stays
  `Succeeded`, three `Unverifiable` outcomes [drop the legacy guard].
- `an_unconfirmed_batch_keeps_todays_batch_state`: an unstaged batch on a
  verifying ScratchTrash stays `Succeeded` [apply the rule without a
  confirmation].
- `a_deleted_outright_destination_is_reported_as_deleted` [map to
  `Unverifiable`].
- `the_replace_after_validation_seam_trips_single_and_batch_deletes`
  (Part 1, `session/tests/trash_confirmation.rs`): holding `let mut _env
  = fault_guard();` with `_env.set("SLATE_TEST_TRASH_REPLACE_AFTER_VALIDATION",
  "wb30-seam-")`, on the default verifying `ScratchTrash`: staged
  `delete_file_staged("wb30-seam-file.md")` → `Mismatch { location:
  Some(dest) }`, the bytes at `dest` ending with the marker line; staged
  `delete_folder_staged("wb30-seam-folder")` → `Mismatch`, the bin's
  folder holding `replaced-after-validation.md`; a staged batch of
  `plain-a.md` and `wb30-seam-batch.md` → `outcomes` `[Confirmed,
  Mismatch]`, `Partial`; staged `delete_file_staged("plain-b.md")` →
  `Confirmed` [drop the call from `delete_file_checked`, from
  `delete_folder_checked` or from the batch loop; fire for every path;
  call it before revalidation (→ `Err`, stale)].
- `every_outcome_variant_reaches_the_batch_report_in_order`: `Confirmed`,
  `Mismatch`, `Unverifiable`, `DeletedOutright` (and `UnlinkedForGood` on
  Windows) in `trashed` order, state `Partial` [drop a variant].
- `the_platform_trash_off_windows_reports_no_destination` (not Windows):
  `false` on macOS and Linux [return `true` on macOS].
- `windows_the_platform_trash_reports_destinations` (Part 2): `true`
  [leave it `false`].
- `windows_the_shell_sink_returns_the_recycled_item` (gated): `Known` under
  `$Recycle.Bin\<SID>`, id equal [ignore the sink];
  `windows_a_bin_disabled_volume_reports_deleted_outright` (gated):
  `DeletedOutright` [treat as confirmed].
- uniffi `trash_outcome_and_trashed_outcome_cross_the_ffi` [drop a
  variant].
- uniffi, end to end (Part 1):
  `a_single_delete_returns_its_trash_outcome_through_the_ffi` (parent)
  with `single_delete_trash_outcome_child` (new; a no-op unless its
  variables are set), in `routed_trash_child`'s form
  (`lib.rs:17455-17532`). The parent makes `bin` and `vault` (holding
  `plain-file.md`, `plain-folder/x.md`, `unstaged-file.md`,
  `unstaged-folder/y.md`, `wb30-replace-file.md`,
  `wb30-replace-folder/z.md`, `batch-a.md`, `wb30-replace-batch.md`),
  runs `current_exe()` with `["tests::single_delete_trash_outcome_child",
  "--exact", "--nocapture"]` and, on the child only,
  `SLATE_UNIFFI_OUTCOME_BIN`, `SLATE_UNIFFI_OUTCOME_VAULT` and
  `SLATE_TEST_TRASH_REPLACE_AFTER_VALIDATION=wb30-replace`, and asserts
  success. The child routes its Trash into `bin`, opens
  `VaultSession::open_filesystem(vault)`, scans, and through the binding
  (each staged call with its own `stage_trash` token):
  `delete_file_staged("plain-file.md")` and
  `delete_folder_staged("plain-folder")` → `TrashOutcome::Confirmed`;
  `delete_file_staged_cancellable("wb30-replace-file.md")` and
  `delete_folder_staged_cancellable("wb30-replace-folder")` → `Mismatch`;
  `delete_file("unstaged-file.md")` and `delete_folder("unstaged-folder")`
  → `Unverifiable { detail: "nothing was confirmed before the move" }`;
  `batch_trash_staged` of `batch-a.md`, `wb30-replace-batch.md` →
  `outcomes` `[Confirmed, Mismatch]`, `Partial`. slate-core is built
  here without `cfg(test)`, as in the app, so the seam is proven
  reachable outside core's tests [a wrapper returns a constant
  `Unverifiable { detail: "the system Trash reports no destination" }`
  instead of core's outcome; `delete_file_checked` returns that constant
  instead of the captured one; the seam read only under `#[cfg(test)]`
  (→ the `Mismatch` assertions read `Confirmed`)].
- Swift `BatchResultPresentationTests.testASucceededBatchWhoseOutcomesAreUnverifiableStillSaysMovedToTrash`:
  a `.succeeded` report with three `Unverifiable` outcomes says "Moved 3
  items to Trash." [announce from `outcomes`].
- Part 2 host: `FileManagementTests.AMismatchedTrashSpeaksCoresSentenceNotMoved`
  (end to end through the real core: the routed scratch bin and
  `SLATE_TEST_TRASH_REPLACE_AFTER_VALIDATION` set in-process to the
  fixture note's unique name, cleared in `finally`, so the staged single
  delete's binding returns `Mismatch` and the host speaks `TrashMismatch`
  [ignore the returned outcome → "Moved …"]),
  `.ADeletedOutrightTrashSpeaksCoresSentence`,
  `.AnUnverifiableTrashSpeaksCoresSentence`,
  `.AWeakIdTrashSaysVerifiedByNameOnly` (the events, not "Moved …"),
  `.ABatchWithMixedOutcomesSaysEachClassInItsSummary` (clauses and
  counts); the four `A11yCorpusCensus` rows and the mac mirror;
  FlaUI `ShellAccessibilityTests.Trash_AVerifiedMoveStillSpeaksMoved`
  (new partial `ShellAccessibilityTests.Trash.cs`; run with NVDA closed,
  its speech witness the shell gate's desktop-scoped UIA listener, never
  an NVDA log: Slate launched through `StartShellProcess`
  (`ShellAccessibilityTests.cs:117-138`) on a temp vault holding
  `note.md` and `other.md`; a `DesktopNotificationListener`
  (`ShellAccessibilityTests.Announcements.cs:520-581`) registered through
  `ListenOnTheDesktop` (`:221-228`) once the window exists; the mark from
  `LaunchSettledMark` (`:350-376`); `note` deleted with Delete in the
  tree; `AwaitHeardSince` (`:384-402`) hears exactly
  `SlateUniffiMethods.A11yRender(new A11yEvent.HostComposed("Moved note to the Recycle Bin.", A11yPriority.Medium))`,
  once, raised as rendered [speak the unverifiable sentence for
  `Confirmed` → the listener hears that instead]).

Registers: contract 31 FR-8 (T2 paragraph, the legacy batch-state rule)
and FR-10 (shared identity), appended; the Trash doc §8 note; Part 2's
`corpus.json`, both census mirrors, the designations JSON and ledger,
`w_c_matrix.md`; Part 1's Swift literals, the sixteen Swift
single-delete statements as `_ = try …`, the Swift pin, and the seam's
entry in WB-18's `FAULT_VARIABLES` and its guard fact's literal list. Not
delivered: the macOS Rust half, the mac host and flipping macOS's
`reports_destination` (PR 41, with its VoiceOver pass); Linux (T4). Done
when, per part, the Verification block runs clean in PowerShell — every
required fact discovered by name, every command's `$LASTEXITCODE` 0, the
gated facts run by name with `SLATE_TRASH_PROBE_VOLUME` set to each fact's
own volume, and Part 1's FAT32/exFAT fact run by name with both of
WB-27's `SLATE_WEAK_VOLUME_*` variables set and recorded in Part 1's PR
body — the windows, rust and swift lanes are green (mac's
successful batch still `Succeeded`; the Swift `_ = try …` edits
compiled), each mutation was run locally, and Part 2's NVDA pass is in
its PR body with `Refs #1468`. That pass runs with NVDA running from
one PowerShell window at the repo root: `dotnet build
apps/slate-windows/SlateWindows.slnx --configuration Release`; a check
that WB-1's `scripts/windows/launch-for-field-pass.ps1` exists; a fresh
vault folder under `$env:TEMP`, named `wb30-field-pass-` and a
timestamp, holding `note.md`, `plain-a.md` … `plain-e.md`,
`wb30-replace-me.md` and `wb30-replace-me-in-a-batch.md`; `$exe =
(Resolve-Path apps/slate-windows/src/SlateWindows/bin/Release/net10.0-windows/SlateWindows.exe).Path`;
`$env:SLATE_TEST_TRASH_REPLACE_AFTER_VALIDATION = 'wb30-replace-me'`;
then `pwsh -NoProfile -File scripts/windows/launch-for-field-pass.ps1
-Vault $vault -Seed -Keep -Exe $exe`, checking `$LASTEXITCODE` — never
`SlateWindows.exe` started directly, which without `SLATE_LOCAL_APP_DATA`
writes the owner's real recents and device state (§4 rule 4). The
launcher (WB-1: `param([string]$Vault, [switch]$Seed, [switch]$Keep,
[string]$Exe)`) sets `SLATE_LOCAL_APP_DATA` and `SLATE_LOG_DIR` under a
new `$env:TEMP\slate-field-pass-<yyyyMMdd-HHmmss>` root, seeds
`recent-vaults.json` with `$Vault`, starts `$Exe` on it, prints the root
and keeps it; it and the app inherit the seam variable from the window,
which is removed with `Remove-Item
Env:SLATE_TEST_TRASH_REPLACE_AFTER_VALIDATION` after Slate closes.
Scenarios (4) and (5) open their vaults from the same running app. The
phrases, each from Delete in the tree: (1) `note` — "Moved note to the
Recycle Bin."; (2) `plain-a`, `plain-b`, `plain-c` — "Moved 3 items to the
Recycle Bin."; (3a) `wb30-replace-me` alone — "Moved wb30-replace-me to
the Recycle Bin, but it was not the confirmed item; it is at
{location}." ({location} the sink's `$Recycle.Bin` path, recorded
verbatim); (3b) `wb30-replace-me-in-a-batch`, `plain-d`, `plain-e` —
"Moved 3 of 3 items to the Recycle Bin; 1 was not the confirmed item.";
(4) the FAT32 VHD vault — `TrashVerifiedByNameOnly`; (5) a bin-disabled
volume — `TrashDeletedOutright`.
Commit:
`feat(core,windows): a move to the recycle bin is verified at its destination and reported truthfully`.
