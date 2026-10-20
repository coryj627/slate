# WB-30 — After a Move to Trash, Slate checks that what arrived is the confirmed item, and says so

Issues: Refs #1468 (its mac host half, plan 43 PR 41, closes it —
whichever of WB-30 and PR 41 merges last closes; §5 closure matrix).
Size L. Lanes: rust, windows, swift (compile: the FFI records change).
AT: NVDA full pass. Wave: W11, slot 1, after WB-28, WB-24's recorded
real-Recycle-Bin results, and plan 43 PR 34's shared identity primitive
merged with green combined-head CI (§5 row, §6). Parallel with: WB-29.
Decision: WD16 (recommended and consumed: decouple from the mac
`~/.Trash` check; COM in Rust beside trash-rs).

## Goal

Decision 1's contract (`24_decision_memo.md:63-69`) comes true on
Windows: *Slate never reports a Trash as confirmed unless the object that
moved is the confirmed object and its tree matched the confirmed inventory
when re-observed at its destination.* Core gains a `TrashOutcome` that
every host consumes: confirmed; verified by name and metadata only (a
weak-ID volume); or a mismatch, reported with where the item went and
never as success. On Windows the Recycle Bin call is `IFileOperation`
with a progress sink that returns the recycled item, re-observed by volume
serial and 128-bit file id against the confirmed inventory. The macOS
Rust half (`resultingItemURL`) and the mac host stay with PR 41.

## Current state on main

Verified against the worktree; the plan's lines hold.

- **The handoff discards what the Trash returns.** `trash_handoff.rs:57-59`
  `trait SystemTrash { fn trash(&self, path: &Path) -> Result<(),
  VaultError>; }`; `PlatformTrash` (`:64-80`) uses `trash::delete(path)`
  off macOS (`:75-78`; trash-rs 5.2.6, `Cargo.lock:2425`, which pulls
  `windows` 0.56, `Cargo.lock:3023-3024`, for its own COM) and
  `macos::trash_item` (`:378-390`), which calls
  `trashItemAtURL_resultingItemURL_error(&url, None)` (`:389`) — the
  result discarded; that call stays PR 41's.
- **The provider contract.** `vault/provider.rs:101` `fn delete(&self,
  relative: &str) -> Result<(), VaultError>;`. `FsVaultProvider`'s Windows
  arm (`fs.rs:2296-2318`) derives the handed path with
  `trash_handoff::pinned_path` (`:298-320`), then `self.trash.trash(&handed)`.
- **The confirmed inventory.** `trash_confirmation.rs:10-26`
  (`StagedTrashItem`, `StagedTrash { token, items }`, `TrashConfirmation {
  token, request, snapshots: Vec<TrashSnapshot> }`), `validate` `:97`
  (test), `validate_item_cancellable(provider, path, cancel)` `:121`.
  `TrashSnapshot` (`trash_snapshot.rs:17-23`): `fingerprint: String`,
  `is_directory`, `item_count`. The Windows stamp
  (`trash_snapshot/windows.rs:235-262`) hashes `FILE_ID_INFO`,
  `FILE_BASIC_INFO` (which carries `ChangeTime`: the root's ctime T2 must
  exempt) and `FILE_STANDARD_INFO` per entry; `known_link` `:214-231`.
- **Where the outcome attaches.** Single: `session.rs:16073-16110`
  `delete_file_checked` (and the folder twin `:16225-16240`) validates
  under the structural lock and the fence, calls `provider.delete`, then
  the index transaction. Batch: `:15060-15078` validates each item and
  calls `self.provider.delete(&plan.item.path)` under the fence, then
  buckets into `trashed` / `untrashed` (`BatchTrashRemainder`) / `unknown`
  (`:15117`, `:15136`, `:15396-15421`). FFI records
  (`slate-uniffi/src/lib.rs:3158-3183`): `BatchTrashState { Rejected,
  NoOp, Succeeded, Partial, Failed }`, `BatchTrashRemainder { item,
  failure }`, `BatchTrashReport { envelope, state, op_id, trashed,
  untrashed, unknown, bookkeeping_failures, requires_rescan }`;
  `BatchItemFailure { item, stage, message }` with `BatchFailureStage`
  (`:3108-3125`, includes `Trash`). FFI endpoints `:1083-1138`
  (`stage_trash*`, `delete_file_staged*`). Mac tests construct
  `BatchTrashReport(` literals (`BaseBatchTrashQuarantineTests.swift:139`,
  `BatchHostQuarantineTests.swift:280`, `:427`): a new record field is a
  blind edit in those three (§4 rule 6).
- **The Windows identity primitive exists already** —
  `windows_entry::identity` (`:612-631`: volume serial + 128-bit id as
  text), `capture` (`:633`), `FsVaultProvider::mutation_identity`
  (`fs.rs:1907-1915`), `VaultSession::capture_structural_identity`
  (`session.rs:16564`), FFI `capture_structural_identity` (`lib.rs:974`).
  The plan's prerequisite (PR 34's "shared core identity primitive") is
  the *cross-platform* form #1469 adds (decision 4: persistent volume id
  + file id; the volume UUID on macOS); the `TrashOutcome` record must
  carry that shared form, which is why the order stands even though the
  Windows capture is in hand.
- **Host sentences.** `FilesSidebarViewModel.cs:1869` "Moved {name} to the
  Recycle Bin."; batch `:2285-2305` ("Couldn't verify whether N moved…",
  "Stopped. Moved N of M…", "Nothing was moved…"). Windows speaks its
  own composition over the report; core owns the per-item outcome text
  only through `vault_error_detail` (`a11y.rs:3401-3412`).
- **WB-24's possible `Deleted` enum** (its spec's refused-kind branch): if
  it shipped, `TrashOutcome` subsumes it here (`UnlinkedForGood` becomes a
  `TrashOutcome` variant) and the `unlinked` report field is renamed in
  the same FFI change.
- **Core's Windows deps.** `Cargo.toml:112-113`: `windows-sys` 0.61 with
  `Wdk_Foundation`, `Wdk_Storage_FileSystem`, `Win32_Security`,
  `Win32_System_IO` (+ `Win32_Storage_FileSystem` from the workspace). No
  `windows` (windows-rs) COM crate in slate-core; `IFileOperation` needs
  COM interfaces (`Win32_UI_Shell`, `Win32_System_Com`), which `windows-sys`
  exposes as raw vtables but `windows` wraps.

## Reference

- `24_trash_handoff_feasibility.md` T2 (`:125-166`): the definition, the
  ctime exemption (`:133-135`), weak-ID wording (`:141-146`), the Windows
  paragraph (`:150-154`: "An `IFileOperationProgressSink` receives
  `psiNewlyCreated` (not probed); NULL means the item was deleted
  outright, which T2 must report as such. Same-volume recycling is a
  rename, so the file ID should survive (expected, not probed). This
  means replacing trash-rs's Windows back end as well."); §6 acceptance
  evidence (`:236-257`); §8 (`:279-287`, Windows untested).
- `24_decision_memo.md`: decision 1 (`:48-69`, option B and the contract
  text), decision 4 (`:122-137`, identity A), owner decisions (`:182-200`:
  "T2 next, verifying what arrived. Before committing to it, run the
  `~/.Trash` access check from a Slate build" — WD16 decouples Windows
  from that check).
- Contract 31 FR-8 (`:420-470`) and FR-10 (`:540-556`).
- #1468's facts list (Trash doc §6): replacement after validation
  reported; moved out and back reported; ordinary confirmed; weak-ID
  "by name and metadata only"; batch with one mismatch partial.
- WB-24's probe log (the gated Windows facts and the per-kind results) is
  the evidence this chunk's Windows facts extend.

## Design

Two parts, one PR each (core contract first; PR 41 consumes it, or the
reverse with WB-30 consuming, §2.3).

### Part 1 — the shared outcome contract (core, both bindings)

- `SystemTrash::trash` becomes `fn trash(&self, path: &Path) ->
  Result<TrashDestination, VaultError>` with `pub(crate) enum
  TrashDestination { Known(PathBuf), Unknown, DeletedOutright }` (new).
  trash-rs's path returns `Unknown` (it reports nothing); `ScratchTrash`
  returns `Known(bin.join(name))`; macOS keeps `Unknown` until PR 41
  passes a `resultingItemURL` out-pointer.
- `VaultProvider::delete` becomes `-> Result<TrashOutcome, VaultError>`
  with `pub enum TrashOutcome { Confirmed, VerifiedByNameAndMetadataOnly,
  Mismatch { detail: String, location: Option<String> }, Unverifiable {
  detail: String } }` (new, core + `uniffi::Enum`); the provider's Windows
  arm computes it: after `Known(dest)`, open `dest` with
  `FILE_FLAG_OPEN_REPARSE_POINT`, compare `FILE_ID_INFO` with the identity
  captured under the fence before the call, then re-run
  `snapshot_cancellable` on the destination and compare with the
  confirmed `TrashSnapshot` with the root's `ChangeTime` exempted (a
  second stamp form, `stamp_without_root_ctime` (new) — or the
  fingerprint is split into root and tree parts); on a volume whose ids
  are not identities (FAT32/exFAT per WB-27's matrix rows: `FileSystemName`
  from `GetVolumeInformationW`), skip the id compare and return
  `VerifiedByNameAndMetadataOnly`; `Unknown` destination →
  `Unverifiable`. The shared identity form comes from PR 34's primitive
  (its name per that PR; on Windows it wraps `windows_entry::identity`).
- Session: `delete_file_checked` / `delete_folder_checked` return
  `TrashOutcome` (FFI `delete_file_staged*` / `delete_folder_staged*`
  return it); the batch report gains `outcomes: Vec<(StructuralBatchItem,
  TrashOutcome)>` — concretely `pub struct TrashedItem { item, outcome }`
  (new record) replacing `trashed: Vec<StructuralBatchItem>` with
  `trashed: Vec<TrashedItem>`, and `BatchTrashState::Partial` covers "one
  mismatch" (a mismatch is not `untrashed`: the item is gone; it is
  trashed-and-mismatched). Both bindings regenerate; the three Swift
  literals are blind-edited (`.trashed` element type).
- Core copy: `A11yEvent::TrashMismatch { name, location }` and
  `A11yEvent::TrashVerifiedByNameOnly { name }` (new; census rows, both
  corpus mirrors), with `a11y.rs` phrasing: "Moved {name} to the Recycle
  Bin, but it was not the confirmed item; it is at {location}." and
  "Moved {name} to the Recycle Bin; verified by name and metadata only."
  The hosts speak these; the ordinary case keeps each host's sentence.

### Part 2 — the Windows back end (core, `cfg(windows)`)

- Add `windows = { version = "0.61", features = ["Win32_UI_Shell",
  "Win32_System_Com", "Win32_Foundation", "implement"] }` to slate-core's
  Windows deps (new; `implement` for the sink), matching the
  `windows-sys` 0.61 line (one `windows-core` 0.62 in the lock already,
  `Cargo.lock:3045`; trash-rs's 0.56 stays separate). A new module
  `vault/trash_handoff/windows_shell.rs`: `SHCreateItemFromParsingName`
  on the handed path, `IFileOperation` with `FOF_ALLOWUNDO |
  FOF_NOCONFIRMATION | FOF_SILENT | FOFX_RECYCLEONDELETE`
  (`FOF_NOERRORUI` too), `Advise` a sink implementing
  `IFileOperationProgressSink::PostDeleteItem(flags, item, hrDelete,
  psiNewlyCreated)`, `PerformOperations`, then `GetAnyOperationsAborted`.
  `psiNewlyCreated` → `IShellItem::GetDisplayName(SIGDN_FILESYSPATH)` →
  `Known(path)`; NULL with `S_OK` → `DeletedOutright` (a volume with no
  Recycle Bin, or a size over the bin's cap); a failed `hrDelete` →
  `VaultError::Trash { message }` with the HRESULT text. COM apartment:
  `CoInitializeEx(COINIT_APARTMENTTHREADED)` on the calling thread, which
  the session already serialises; `PlatformTrash` on Windows dispatches
  here instead of `trash::delete`, and trash-rs stays for Linux.
- The guard (`refuse_a_trash_that_is_not_scratch`) and WB-24's gated facts
  cover this back end: the gated fact re-runs against it unchanged.

What is deliberately not changed: the inventory token protocol; the
macOS back end (PR 41); Linux (T4 deferred); the host batch summary
shapes beyond the two new sentences.

If the owner answers WD16 otherwise (COM in the host): Part 2 moves to
C# (`IFileOperation` via CsWin32 in `apps/slate-windows`) and the
provider's `delete` takes a host-supplied `SystemTrash` through a new FFI
callback interface — a larger FFI change, recorded here as the rejected
shape.

## Tests

| Fact | Asserts | Mutation |
|---|---|---|
| `trash_handoff_tests.rs`: `an_ordinary_trash_is_confirmed` (ScratchTrash, every platform) | `TrashOutcome::Confirmed`; the bin holds the bytes | return `Unverifiable` |
| `a_replacement_after_validation_is_reported_not_confirmed` | replace `sub/note.md` between `validate_item_cancellable` and the Trash through a fault point (`BatchFaultPoint::TrashWriteFence` exists, `session.rs:15066`); `Mismatch { location }` | skip the id compare |
| `an_item_moved_out_of_the_vault_and_back_is_reported` | identity differs → `Mismatch` | — |
| `a_trashed_tree_that_changed_is_reported` | a child added to a folder between inventory and Trash → `Mismatch` | drop the tree re-walk |
| `the_root_ctime_is_exempt_from_the_re_observation` | an unchanged folder whose root ctime the move changed → `Confirmed` | include ctime |
| `a_weak_id_volume_reports_verified_by_name_and_metadata_only` (gated, FAT32 VHD via WB-27's `SLATE_WEAK_VOLUME_FAT32`) | the variant | claim `Confirmed` |
| `an_unknown_destination_is_unverifiable_never_confirmed` | trash-rs/Linux path | — |
| `windows_the_shell_sink_returns_the_recycled_item` (`#[cfg(windows)] #[ignore]`, scratch volume) | `Known(path)` under `$Recycle.Bin\<SID>`, id equal | ignore the sink |
| `windows_a_delete_outright_is_reported_as_such` (gated: a volume with the bin disabled via `NukeOnDelete`) | `DeletedOutright` → `Unverifiable` with the detail | treat as confirmed |
| `structural_batch.rs`: `a_batch_with_one_mismatch_reports_partial_with_the_item_trashed_and_mismatched` | state `Partial`, `trashed` carries the outcome | bucket as `untrashed` |
| uniffi: `trash_outcome_crosses_the_ffi` | the enum and `TrashedItem` | — |
| Windows: `FileManagementTests.AMismatchedTrashSpeaksCoresSentenceNotMoved` | the `TrashMismatch` event, not "Moved …" | — |
| `FileManagementTests.ABatchWithAMismatchSaysSoInItsSummary` | the batch sentence counts the mismatch | — |
| `A11yCorpusCensus` + mac mirror: the two new rows | | |
| FlaUI `ShellAccessibilityTests.Trash_AVerifiedMoveStillSpeaksMoved` | the ordinary sentence unchanged after the back-end swap; NVDA log | — |

## Docs and registers

- Contract 31 FR-8: append the T2 paragraph (outcome variants, the Windows
  sink, the weak-ID wording, the ctime exemption); FR-10: a sentence
  that the Trash re-observation uses the shared identity.
- `24_trash_handoff_feasibility.md` §8: a dated note that the Windows
  sink was probed, with the result (append).
- `corpus.json`, `A11yCorpusCensus.cs:57`, `A11yCorpusCensusTests.swift:50`;
  `a11y_trigger_designations.json` rows for the two events;
  `python -I scripts/a11y_trigger_ledger.py --write`.
- `w_c_matrix.md` Files tree row: the two sentences.
- `Cargo.toml` / `Cargo.lock`: the `windows` crate (licence gate:
  `make ci`'s `check-license-headers` and `cargo deny` if configured).
- PR bodies: `Refs #1468` on both parts; the WB-24 real-Recycle-Bin run's
  result pasted per WD14's fallback if the lane cannot run it.

## Verification

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb30'
cargo test -p slate-core --locked trash_handoff
cargo test -p slate-core --locked structural_batch
cargo test -p slate-core --locked --no-fail-fast
cargo test -p slate-uniffi --locked trash
make ci
# gated, on the WB-24 VM with the VHDs mounted:
$env:SLATE_TRASH_PROBE_VOLUME='S:\'; $env:SLATE_WEAK_VOLUME_FAT32='F:\probe'
cargo test -p slate-core --locked -- --ignored real_trash --test-threads=1
cargo test -p slate-core --locked -- --ignored weak_id --test-threads=1
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --filter "FullyQualifiedName~FileManagementTests|FullyQualifiedName~A11yCorpusCensus"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <edited .cs files>
python -I scripts/a11y_trigger_ledger.py --write
```

CI: `windows.yml` (all), `rust.yml`, `swift-tests.yml` (compile of the
record change and the three blind literals; wait for it green before
merge, §11). AT: NVDA full pass per §4 rule 4: (1) ordinary single
delete — "Moved note to the Recycle Bin." unchanged; (2) a batch of three
— the summary sentence unchanged; (3) the mismatch, provoked with the
fault seam in a test build launched for the pass — the exact
`TrashMismatch` phrase; (4) on the FAT32 VHD vault — the
`TrashVerifiedByNameOnly` phrase. Recorded in the PR body; draft until
then.

## Acceptance

- [ ] Prerequisites merged: WB-28, WB-24's results recorded, PR 34's
      identity primitive with green combined-head CI.
- [ ] `TrashOutcome`, `TrashedItem`, the two events on both bindings;
      Swift literals edited and the mac lane green.
- [ ] The Windows `IFileOperation` sink back end, probed on the scratch
      volume (gated facts run and recorded).
- [ ] The fifteen facts pass, seen failing first; NVDA pass recorded.
- [ ] Registers appended; `Refs #1468` (closes with PR 41 or by the last
      merge); "not delivered": the macOS Rust half and the mac host (PR
      41); Linux (T4 deferred).

## Brief

Make the Trash outcome truthful on Windows and shared by both hosts.
Read the Trash doc `:125-166`, `:236-257`, the memo `:48-69`,
`:122-137`, `:182-200`, then `trash_handoff.rs:55-200`, `:290-345`,
`:370-395`, `trash_confirmation.rs:10-30`, `:95-130`,
`trash_snapshot.rs:15-45`, `trash_snapshot/windows.rs:70-85`, `:210-262`,
`provider.rs:95-105`, `fs.rs:2268-2320`, `session.rs:15040-15140`,
`:15396-15425`, `:16073-16110`, `uniffi lib.rs:1083-1140`, `:3100-3185`,
`windows_entry.rs:612-635`, `FilesSidebarViewModel.cs:1848-1872`,
`:2280-2306`, `Cargo.toml:112-120`, the three Swift literals. Part 1:
the contract (`TrashDestination`, `TrashOutcome`, `TrashedItem`, the two
events) through provider, session, FFI and both hosts. Part 2: the
Windows `IFileOperation` sink in Rust with the `windows` crate, behind
`PlatformTrash`. Facts per Tests (ScratchTrash for the portable ones,
gated for the Recycle Bin and FAT32), failing first; registers per Docs.
Done when both lanes and the mac compile are green and the NVDA pass is
recorded. Commit:
`feat(core,windows): a move to the recycle bin is verified at its destination and reported truthfully`.
