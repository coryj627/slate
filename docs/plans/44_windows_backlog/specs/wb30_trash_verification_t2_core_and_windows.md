# WB-30 — After a Move to Trash, Slate checks that what arrived is the confirmed item, and says so

Issues: Refs #1468 (its mac host half, plan 43 PR 41, closes it —
whichever of WB-30 and PR 41 merges last closes; §5 closure matrix).
Size L. Lanes: rust, windows, swift (compile: the FFI records change).
AT: NVDA full pass. Wave: W11, slot 1, after WB-28, WB-24's recorded
real-Recycle-Bin results, and plan 43 PR 34's shared identity primitive
merged with green combined-head CI. Parallel with: WB-29. Decision: WD16
(pending; recommended "decouple the Windows T2 from the mac `~/.Trash`
read check; COM in Rust (`windows-rs`) beside the trash-rs path so core
keeps owning the outcome"; under the other answer — COM in the host —
Part 2 moves to C# (`IFileOperation` through CsWin32 in `apps/slate-windows`)
and the provider takes a host-supplied `SystemTrash` through a new FFI
callback interface; and if the mac check stays a programme precondition,
WB-30 waits for it).

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
confirmed inventory that the session hands the provider. The macOS Rust
half (`resultingItemURL`) and the mac host stay with PR 41.

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
  (`:268-275`); `known_link` `:214-231`.
- **Where the outcome attaches.** Single: `session.rs:16073-16110`
  `delete_file_checked` (folder twin `:16225-16240`) validates under the
  structural lock and the fence, calls `provider.delete`, then the index
  transaction. Batch: `:15060-15078` — `faults.check(BatchFaultPoint::
  TrashWriteFence)` at `:15067` fires **before** `validate_item_cancellable`
  at `:15069-15073`, so that point models a pre-validation failure, not a
  replacement after validation; `self.provider.delete(&plan.item.path)`
  at `:15075`; buckets `trashed` / `untrashed` / `unknown` (`:15117`,
  `:15136`, `:15396-15421`). `BatchFaultPoint` (`session.rs:12920-12934`)
  has no point between validation and the handoff. FFI records
  (`slate-uniffi/src/lib.rs:3158-3183`): `BatchTrashState`,
  `BatchTrashRemainder`, `BatchTrashReport { envelope, state, op_id,
  trashed: Vec<StructuralBatchItem>, untrashed, unknown,
  bookkeeping_failures, requires_rescan }`; `BatchItemFailure` /
  `BatchFailureStage` (`:3108-3125`). FFI endpoints `:1083-1138`.
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

Two parts, one PR each (core contract first; PR 41 consumes it, or the
reverse with WB-30 consuming, §2.3).

### Part 1 — the shared outcome contract (core, both bindings)

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
  TrashDestination { Known(PathBuf), Unknown, DeletedOutright }` (new).
  trash-rs returns `Unknown`; `ScratchTrash` returns `Known(bin.join(name))`;
  macOS returns `Unknown` until PR 41.
- **The confirmed observation travels with the delete.**
  `VaultProvider::delete_confirmed(&self, relative: &str, confirmed:
  &ConfirmedItem) -> Result<TrashOutcome, VaultError>` (new; the default
  body calls `delete` and returns its outcome) where `pub struct
  ConfirmedItem { identity: StructuralIdentity /* PR 34's shared form */,
  tree: TrashSnapshot, tree_without_ids: TrashSnapshot }` (new).
  `TrashConfirmation` gains `pub(crate) fn confirmed_item(&self, path)
  -> Result<ConfirmedItem, VaultError>` (new; reads its private
  `snapshots[index]` and the identity captured at staging — `stage_trash`
  captures both snapshot forms and the identity per item, so nothing is
  recaptured at delete time and an external replacement cannot be
  confirmed against itself). `delete_file_checked`, `delete_folder_checked`
  and the batch loop (`:15075`) call `delete_confirmed` with it when a
  confirmation exists, `delete` otherwise.
- **Two fingerprints.** `trash_snapshot::snapshot_at` takes
  `IdentityMode { Strong, NameAndMetadataOnly }` (new): `Strong` is
  today's stamp; `NameAndMetadataOnly` omits `FILE_ID_INFO` entirely and
  hashes names, kinds, sizes and times only (Unix twin: omits `st_dev`/
  `st_ino`). Both omit the **root's** `ChangeTime`/ctime (a new
  `root_ctime_exempt` flag in `visit`'s depth-0 stamp). `TrashSnapshot`
  gains `pub mode: IdentityMode`. The staging inventory computes both
  forms per item (two walks under the same handles).
- **The mapping, total.** The provider's Windows arm, after the Trash
  call:

  | destination | ids are identities | outcome |
  |---|---|---|
  | `Known(dest)`, re-opened `FILE_ID_INFO` == `confirmed.identity`, `snapshot_at(dest, Strong)` == `confirmed.tree` | yes | `Confirmed` |
  | `Known(dest)`, identity equal, tree differs | yes | `Mismatch { detail: "the tree changed", location }` |
  | `Known(dest)`, identity differs | yes | `Mismatch { detail: "not the confirmed item", location }` |
  | `Known(dest)`, `snapshot_at(dest, NameAndMetadataOnly)` == `confirmed.tree_without_ids` | no (FAT32/exFAT/SMB without ids, by `GetVolumeInformationW` name and WB-27's rows) | `VerifiedByNameAndMetadataOnly` |
  | `Known(dest)`, names-and-metadata differ | no | `Mismatch { … }` |
  | `Known(dest)` cannot be opened | any | `Unverifiable { detail }` |
  | `Unknown` | any | `Unverifiable { detail: "the system Trash reports no destination" }` |
  | `DeletedOutright` | any | `DeletedOutright` |
  | the unlink branch WB-24 kept | — | `UnlinkedForGood` |

- **Session and batch.** A `Mismatch`, `Unverifiable`, `DeletedOutright`
  or `UnlinkedForGood` item is still in `trashed` (it is gone from the
  vault) with its outcome in `outcomes`; `BatchTrashState::Partial` when
  any outcome is not `Confirmed`/`VerifiedByNameAndMetadataOnly` and
  everything else succeeded. A new fault point
  `BatchFaultPoint::AfterTrashValidation` (new) fires strictly between a
  successful `validate_item_cancellable` and the handoff, so a fact can
  replace the item in that window.
- **Core copy** (`A11yEvent`, census rows, both corpus mirrors):
  `TrashMismatch { name, location }` — "Moved {name} to the Recycle Bin,
  but it was not the confirmed item; it is at {location}."; 
  `TrashVerifiedByNameOnly { name }` — "Moved {name} to the Recycle Bin;
  verified by name and metadata only."; `TrashDeletedOutright { name }` —
  "{name} was deleted, not moved to the Recycle Bin: this volume has no
  Recycle Bin or the item was too large for it."; `TrashUnverifiable
  { name }` — "Moved {name} to the Recycle Bin; Slate could not verify
  what arrived." (`LinkDeletedForGood` is WB-24's.) The Windows host
  speaks these for a single item instead of "Moved …" when the outcome is
  not `Confirmed`; the batch sentences at `:2285-2305` append one clause
  per non-confirmed class with counts ("Moved 3 of 3 to the Recycle Bin;
  1 was not the confirmed item; 1 verified by name only."). The mac host
  ignores `outcomes` until PR 41.

### Part 2 — the Windows back end (core, `cfg(windows)`)

- **Spike first (one commit):** add `windows` on the 0.62 line (the
  `windows-core` 0.62.2 already in the lock) with the feature set the
  registry's `Cargo.toml` for that release lists for `IFileOperation`,
  `IShellItem`, `SHCreateItemFromParsingName`, `CoInitializeEx` and
  `IFileOperationProgressSink` — expected `Win32_UI_Shell`,
  `Win32_System_Com`, `Win32_Foundation`, `Win32_UI_Shell_Common` —
  verified by `cargo metadata` after `cargo update -p windows`, and
  compile a minimal `#[windows::core::implement(IFileOperationProgressSink)]`
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
  Windows dispatches here; trash-rs stays for Linux. WB-24's guard and
  gated facts cover it unchanged.

Deliberately not changed: the inventory token protocol; the macOS back
end (PR 41); Linux (T4 deferred); `trashed`'s shape.

## Tests

| Fact | Asserts | Mutation |
|---|---|---|
| `trash_handoff_tests.rs`: `an_ordinary_trash_is_confirmed` (ScratchTrash, every platform) | `Confirmed`; the bin holds the bytes | return `Unverifiable` |
| `a_replacement_after_validation_is_reported_not_confirmed` | at `AfterTrashValidation`, `sub/note.md` is replaced by a same-name file; outcome `Mismatch { location }`, the replacement in the bin, reported in `outcomes` | recapture at delete time |
| `an_item_moved_out_of_the_vault_and_back_is_reported` | identity differs → `Mismatch` | — |
| `a_trashed_tree_that_changed_is_reported` | a child added to a folder at `AfterTrashValidation` → `Mismatch` | drop the tree re-walk |
| `the_root_ctime_is_exempt_from_the_re_observation` | an unchanged folder whose root ctime the move changed → `Confirmed` | include ctime |
| `a_weak_id_volume_is_verified_by_name_and_metadata_without_reading_ids` (gated: `SLATE_WEAK_VOLUME_FAT32`, and `SLATE_TRASH_PROBE_VOLUME` set to the **same** FAT32 volume) | `VerifiedByNameAndMetadataOnly`; and with the id seam returning a different synthetic id for the destination, still that variant | hash ids in `NameAndMetadataOnly` |
| `a_weak_id_volume_mismatch_is_still_detected_by_name_and_metadata` (gated, same volume) | a renamed child at `AfterTrashValidation` → `Mismatch` | — |
| `an_unknown_destination_is_unverifiable_never_confirmed` | the Linux/trash-rs path | — |
| `a_deleted_outright_destination_is_reported_as_deleted` | `TrashDestination::DeletedOutright` → `DeletedOutright` (ScratchTrash seam) | map to `Unverifiable` |
| `every_outcome_variant_reaches_the_batch_report_in_order` | a mixed batch (one per variant through seams) → `outcomes` in `trashed` order, state `Partial` | drop a variant |
| `windows_the_shell_sink_returns_the_recycled_item` (`#[cfg(windows)] #[ignore]`, scratch volume) | `Known(path)` under `$Recycle.Bin\<SID>`, id equal | ignore the sink |
| `windows_a_bin_disabled_volume_reports_deleted_outright` (`#[ignore]`; the volume's bin disabled by the probe through `NukeOnDelete` under `HKCU\…\Explorer\BitBucket\Volume\<guid>`, restored after) | `DeletedOutright` | treat as confirmed |
| uniffi: `trash_outcome_and_trashed_outcome_cross_the_ffi` | the enum and record | — |
| Windows `FileManagementTests.AMismatchedTrashSpeaksCoresSentenceNotMoved`, `…ADeletedOutrightTrashSpeaksCoresSentence`, `…AnUnverifiableTrashSpeaksCoresSentence`, `…AWeakIdTrashSaysVerifiedByNameOnly` | the four events, not "Moved …" | — |
| `FileManagementTests.ABatchWithMixedOutcomesSaysEachClassInItsSummary` | the batch sentence's clauses and counts | — |
| `A11yCorpusCensus` + mac mirror: the four new rows | | |
| FlaUI `ShellAccessibilityTests.Trash_AVerifiedMoveStillSpeaksMoved` (`ShellAccessibilityTests.Trash.cs`, new partial) | the ordinary sentence unchanged after the back-end swap; NVDA log | — |

## Docs and registers

- Contract 31 FR-8: append the T2 paragraph (outcome variants and their
  mapping, the Windows sink, the weak-ID fingerprint, the ctime
  exemption); FR-10: a sentence that the Trash re-observation uses the
  shared identity.
- `24_trash_handoff_feasibility.md` §8: a dated note that the Windows
  sink was probed, with the result (append).
- `corpus.json`, `A11yCorpusCensus.cs:57`, `A11yCorpusCensusTests.swift:50`
  (+ count `:666`); `docs/a11y_trigger_designations.json` rows for the four
  events; `python -I scripts/a11y_trigger_ledger.py --write`.
- The 14 Swift `BatchTrashReport` literals, if WB-24 did not already add
  `outcomes`.
- `w_c_matrix.md` Files tree row: the four sentences.
- `Cargo.toml` / `Cargo.lock`: the `windows` crate (licence headers are
  per source file; `bash scripts/check-license-headers.sh`).
- PR bodies: `Refs #1468` on both parts; WB-24's real-Recycle-Bin run
  result pasted per WD14's other answer if the lane cannot run it.

## Verification

PowerShell, repo root. Each gated line is preceded by its `--list` form,
which must print 1.

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb30'
cargo update -p windows; cargo metadata --format-version 1 | Select-String '"name":"windows","version":"0.62'   # the spike's pin check
cargo test -p slate-core --locked trash_handoff
cargo test -p slate-core --locked trash_snapshot
cargo test -p slate-core --locked structural_batch
cargo test -p slate-core --locked --no-fail-fast
cargo test -p slate-uniffi --locked trash_outcome
cargo fmt --all -- --check
cargo clippy --all-targets --workspace -- -D warnings
cargo test --workspace --locked
cargo bench --workspace --no-run
bash scripts/check-license-headers.sh
# gated, on the WB-24 VM; $probe is the NTFS scratch volume, $fatRoot the FAT32 one (WB-27's procedure):
$env:SLATE_TRASH_PROBE_VOLUME = $probe
cargo test -p slate-core --locked -- --ignored --list windows_the_shell_sink_returns_the_recycled_item | Select-String ': test$' | Measure-Object | Select-Object -ExpandProperty Count   # 1
cargo test -p slate-core --locked -- --ignored --test-threads=1 windows_the_shell_sink_returns_the_recycled_item
cargo test -p slate-core --locked -- --ignored --test-threads=1 windows_a_bin_disabled_volume_reports_deleted_outright
cargo test -p slate-core --locked -- --ignored --test-threads=1 real_trash_on_windows_lands_in_the_volumes_recycle_bin_with_its_records   # WB-24's fact against the new back end
$env:SLATE_TRASH_PROBE_VOLUME = "$($fat.DriveLetter):\"; $env:SLATE_WEAK_VOLUME_FAT32 = $fatRoot
cargo test -p slate-core --locked -- --ignored --test-threads=1 a_weak_id_volume_is_verified_by_name_and_metadata_without_reading_ids
cargo test -p slate-core --locked -- --ignored --test-threads=1 a_weak_id_volume_mismatch_is_still_detected_by_name_and_metadata
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --filter "FullyQualifiedName~SlateWindows.Tests.FileManagementTests|FullyQualifiedName~SlateWindows.Tests.Censuses.A11yCorpusCensus"
dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj --configuration Release --filter "FullyQualifiedName~ShellAccessibilityTests.Trash_AVerifiedMoveStillSpeaksMoved"
$cs = @('apps/slate-windows/src/SlateWindows/FilesSidebarViewModel.cs','apps/slate-windows/src/SlateWindows/FilesSidebarViewModel.Trash.cs','apps/slate-windows/tests/SlateWindows.Tests/FileManagementTests.cs','apps/slate-windows/tests/SlateWindows.Tests/Censuses/A11yCorpusCensus.cs','apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.Trash.cs')
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs
python -I scripts/a11y_trigger_ledger.py --write
```

Mutation run per row, locally. CI: `windows.yml` (all), `rust.yml`,
`swift-tests.yml` (the record change and the literals; green before
merge). AT: NVDA full pass per §4 rule 4: (1) ordinary single delete —
"Moved note to the Recycle Bin." unchanged; (2) a batch of three — the
summary unchanged; (3) the mismatch, provoked with `AfterTrashValidation`
in a test build launched for the pass — the exact `TrashMismatch` phrase;
(4) on the FAT32 VHD vault — the `TrashVerifiedByNameOnly` phrase; (5)
on a bin-disabled volume — the `TrashDeletedOutright` phrase. Recorded in
the PR body; draft until then.

## Acceptance

- [ ] WD16 recorded; prerequisites merged: WB-28, WB-24's results
      recorded, PR 34's identity primitive with green combined-head CI.
- [ ] `TrashOutcome` (WB-24's enum, unchanged), `TrashedOutcome`,
      `outcomes` (legacy `trashed` untouched), `ConfirmedItem`,
      `delete_confirmed`, `IdentityMode`, `AfterTrashValidation`, the four
      events on both bindings; the Swift literals edited if not already;
      mac lane green.
- [ ] The Windows `IFileOperation` sink back end, spiked and probed on
      the scratch volume (gated facts run and recorded).
- [ ] The eighteen facts pass, seen failing first; NVDA pass recorded.
- [ ] Registers appended; `Refs #1468`; "not delivered": the macOS Rust
      half and the mac host (PR 41); Linux (T4 deferred).

## Brief

Make the Trash outcome truthful on Windows and shared by both hosts.
Pending decision: WD16 (recommended: COM in Rust beside trash-rs,
decoupled from the mac `~/.Trash` check; else C# `IFileOperation` through
a host `SystemTrash` callback FFI). Read `24_trash_handoff_feasibility.md:
125-166`, `:236-257`, `24_decision_memo.md:48-69`, `:122-137`,
`:182-200`; `trash_handoff.rs:55-200`, `:290-345`, `:370-395`;
`trash_confirmation.rs:10-30`, `:95-141`; `trash_snapshot.rs:15-45`;
`trash_snapshot/windows.rs:50-120`, `:210-275`; `provider.rs:95-105`;
`fs.rs:2268-2320`; `session.rs:12920-12934`, `:15040-15140`,
`:15396-15425`, `:16073-16110`; `uniffi lib.rs:1083-1140`, `:3100-3185`;
`windows_entry.rs:612-635`; `FilesSidebarViewModel.cs:1848-1872`,
`:2280-2306`; `Cargo.toml:112-120`; `AppState.swift:1209-1286`, `:1357`,
`:1386`, `:1412`, `:20717-20747`, `:21476`; WB-24's spec for the enum
and the 14 Swift literal sites. Build, Part 1: `TrashDestination`,
`ConfirmedItem` (identity + strong and id-free snapshots captured at
staging), `TrashConfirmation::confirmed_item`,
`VaultProvider::delete_confirmed`, `IdentityMode` and the root-ctime
exemption in `snapshot_at`, the total destination→outcome mapping in the
Windows arm, `BatchFaultPoint::AfterTrashValidation`, `outcomes:
Vec<TrashedOutcome>` beside the unchanged `trashed`, the four
`A11yEvent`s, and the Windows host's single and batch sentences. Part 2:
the spike pinning `windows` 0.62 with the registry-verified feature set
and a compiling `#[windows::core::implement(IFileOperationProgressSink)]`
sink, then `vault/trash_handoff/windows_shell.rs` behind `PlatformTrash`.
Facts, seen failing first: `an_ordinary_trash_is_confirmed`;
`a_replacement_after_validation_is_reported_not_confirmed` (at
`AfterTrashValidation`); `an_item_moved_out_of_the_vault_and_back_is_reported`;
`a_trashed_tree_that_changed_is_reported`;
`the_root_ctime_is_exempt_from_the_re_observation`;
`a_weak_id_volume_is_verified_by_name_and_metadata_without_reading_ids`
(gated; synthetic id differs, still verified);
`a_weak_id_volume_mismatch_is_still_detected_by_name_and_metadata`
(gated); `an_unknown_destination_is_unverifiable_never_confirmed`;
`a_deleted_outright_destination_is_reported_as_deleted`;
`every_outcome_variant_reaches_the_batch_report_in_order`;
`windows_the_shell_sink_returns_the_recycled_item` (gated);
`windows_a_bin_disabled_volume_reports_deleted_outright` (gated);
`trash_outcome_and_trashed_outcome_cross_the_ffi`;
`FileManagementTests.AMismatchedTrashSpeaksCoresSentenceNotMoved`,
`…ADeletedOutrightTrashSpeaksCoresSentence`, `…AnUnverifiableTrashSpeaksCoresSentence`,
`…AWeakIdTrashSaysVerifiedByNameOnly`, `…ABatchWithMixedOutcomesSaysEachClassInItsSummary`;
the `A11yCorpusCensus` rows; `ShellAccessibilityTests.Trash_AVerifiedMoveStillSpeaksMoved`.
Registers: contract 31 FR-8 and FR-10 (appended), the Trash doc §8,
`corpus.json` and both census mirrors, the designations JSON and ledger,
`w_c_matrix.md`, the Swift literals. Not delivered: the macOS Rust half
and mac host (PR 41), Linux (T4). Done when the core suite, the
PowerShell gates, the gated probes (run by name with
`SLATE_TRASH_PROBE_VOLUME` set to each fact's own volume), the Windows,
rust and swift lanes are green, each mutation was run locally, and the
five-scenario NVDA pass is in the PR body with `Refs #1468`. Commit:
`feat(core,windows): a move to the recycle bin is verified at its destination and reported truthfully`.
