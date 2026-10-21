# WB-24 — Trash on Windows is tested again, trashes a dangling link as the link where the Recycle Bin takes it, and tells the truth in a OneDrive vault

Issues: #1480 #1499 #1492. Size M. Lanes: rust (Windows cfg), windows,
swift (compile, only if the outcome field ships). AT: NVDA spot (any
changed Trash or refusal sentence). Wave: W8, slot 2 after WB-23.
Depends on: WB-1 (the field-pass launch script). Parallel with: WB-22,
WB-21b. Decision: WD14 (pending; recommended "developer-run first on a
throwaway Hyper-V VM with a VHD, then a `home-only` lane job; the
OneDrive probe on a Files On-Demand vault the owner provides"; under the
other answer — no lane may attach a VHD — the gated facts stay owner-run
and every run's result is pasted into the PR body, and WB-30 records each
later run). WD15 (pending; recommended "relax the identity and undo
walks to #1491's `NotLinks`; keep the Trash handoff fail-closed unless the
probe shows `IFileOperation` is not redirected through a placeholder;
word the refusal truthfully either way"; under the other answer — keep
every walk fail-closed — only the refusal wording ships, undo stays a
barrier in such vaults, and the PR body says so). Neither decision is
answered on the plan; nothing below that depends on one starts before the
owner records it.

## Goal

Three things become true. A gated fact exercises core's real Windows Trash
back end (trash-rs `IFileOperation::DeleteItem`, `FOF_ALLOWUNDO`) against a
scratch volume's own `$Recycle.Bin`, so the back end #1455 took out of
every test run is tested again, by the lane WD14 names. A dangling link is
handed to the Recycle Bin as the link, as macOS and Linux do since #1495,
once the probe shows the Recycle Bin takes it; where it refuses a kind,
Slate keeps unlinking that kind and both hosts stop counting it as moved.
And in a OneDrive Files On-Demand vault, undo and redo work (a placeholder
folder is a folder, #1491's rule) and Move to Trash either works or
refuses with a reason a user can act on.

## Current state on main

Verified against the worktree; the plan's and reports' lines hold.

- **The guard.** `vault/trash_handoff.rs:57-59` `trait SystemTrash`;
  `PlatformTrash::trash` (`:64-80`) calls `refuse_a_trash_that_is_not_scratch`
  in test builds (`:69`) and `trash::delete(path)` off macOS (`:75-78`).
  `PROBE_VOLUME = "SLATE_TRASH_PROBE_VOLUME"` (`:86`). The guard
  (`:97-141`): resolves the volume (`:106-112`), requires the path on it,
  then per platform; the Windows arm (`:139-140`) is an unconditional
  refusal: "Windows has no gated real-Trash fact".
- **The gated facts.** `trash_handoff_tests.rs:763-766`
  `real_system_trash_provider` is `cfg(macos, linux)`; the macOS fact
  `real_trash_on_a_scratch_volume_lands_in_its_trashes_with_put_back_records`
  (`:783-818`, `#[ignore = "needs a scratch volume in
  SLATE_TRASH_PROBE_VOLUME; never a real user's Trash"]`) uses
  `probe_volume_and_its_trashes` (`:822-833`) and trashes a note, a folder
  and a link through the provider. Sibling gated facts: `:954`
  (unresolvable links), `:1039` (many processes), Linux `:1134`, `:1178`.
  `windows.yml:335-341` runs `cargo test … -- --skip census_ …` with no
  `--ignored`, so no gated fact runs in any lane today.
- **The Windows unlink.** `vault/fs.rs:2305-2317` (the Windows `delete`
  arm): after `pinned_path`, a symlink whose `fs::metadata(&handed)` fails
  with `trash_handoff::link_target_is_absent` (`trash_handoff.rs:184-194`:
  `ERROR_FILE_NOT_FOUND | ERROR_PATH_NOT_FOUND | ERROR_DIRECTORY`) is
  removed by `unlink_dangling_symlink` (`:2412-2419`) and `Ok(())`
  returns: no error, nothing reaches the Trash. Pinned by
  `windows_a_link_whose_target_is_missing_is_still_unlinked`
  (`trash_handoff_tests.rs:663-686`). The two kinds already handed to the
  Recycle Bin, unverified: `:689` (`ERROR_ACCESS_DENIED`) and `:724`
  (cycle).
- **The hosts count an unlinked link as moved.** `VaultProvider::delete`
  returns `Result<(), VaultError>` (`provider.rs:101`), so core cannot
  say which happened. Windows: `FilesSidebarViewModel.cs:1869`
  `ReportMutationResult($"Moved {node.DisplayName} to the Recycle Bin.")`;
  batch sentences `:2285-2305` count `report.Trashed.Length`. Mac composes
  its own over `report.trashed` (`AppState.swift:1209-1286`, `:1357`,
  `:1386`, `:1412`, `:20717`, `:20730`, `:20747`, `:21476`). Core puts the
  item in `trashed` (`session.rs:15075` `self.provider.delete(…)` → `Ok`).
  Contract 31 FR-8 (`31_file_management_contracts.md:420-470`) records the
  Windows exception at `:462-468`.
- **The reparse walks (#1492).** `windows_entry.rs:40-50` (placeholders
  are folders for forward moves, saves and creations; the Trash keeps
  #1450's rule). `Folders` (`:310-330`): `NotLinks` admits
  `Found::ReparseFolder`, `PlainOnly` does not. The identity walk
  `windows_entry::open` (`:571-580`) calls `directory()` (`:563-569`),
  which refuses any reparse attribute with `refused()` (`:555-561`:
  "Cannot undo or redo: the files have changed or their identities cannot
  be verified"); `capture` (`:633-635`) backs `FsVaultProvider::
  mutation_identity` (`fs.rs:1907-1915`), `VaultSession::
  capture_structural_identity` (`session.rs:16564`) and the host's
  `TryCaptureStructuralIdentity` (`FileManagement.cs:315-319`, `null` on
  `VaultException` → `StructuralHistoryBarrier`). The Trash inventory
  `trash_snapshot/windows.rs` refuses a placeholder at **three** sites:
  `check_directory` (`:77-83`, every ancestor, through `is_link` `:73-75`),
  the selected item itself (`:58-60`, "Cannot stage Trash for a symbolic
  link"), and the recursive visit (`:111-116`: an entry with a reparse
  attribute whose tag `known_link` (`:214-231`, tags `0xA000_0003 |
  0xA000_000C` only) does not know is refused, and a reparse directory is
  never recursed into). The handoff `trash_handoff::pinned_path`
  (`:298-320`) walks with `Folders::PlainOnly` (`:314`). The host's import
  check `HasReparsePointInPath` (`Import.cs:401`, used `:248`).
  `test_support::opaque_reparse_point` (`test_support.rs:98-115`) makes a
  folder that is a reparse point redirecting nothing — the #1451 facts'
  stand-in for a placeholder (`windows_entry_tests.rs:125`).
- **FFI record consumers.** `BatchTrashReport` (`slate-uniffi/src/lib.rs:
  3174-3183`). Every Swift construction on main (14): 
  `BaseBatchTrashQuarantineTests.swift:139`,
  `BatchHostQuarantineTests.swift:280`, `:427`, `:522`, `:1235`, `:1969`,
  `:2041`, `BatchResultPresentationTests.swift:438`,
  `BatchTrashDirtyCloseTests.swift:78`, `FileTreeMultiSelectTests.swift:257`,
  `MarkdownPropertyRecoveryContractTests.swift:190`,
  `StructuralUndoTests.swift:145`, `:302`, `WorkspaceTabsTests.swift:83`.
  Production reads of `report.trashed`: `AppState.swift:1209-1286`,
  `:1357`, `:1386`, `:1412`, `:20717`, `:20730`, `:20747`, `:21476`.

## Reference

- The macOS twin: `real_trash_on_a_scratch_volume_lands_in_its_trashes_with_put_back_records`
  and `…takes_each_unresolvable_link_as_the_link`
  (`trash_handoff_tests.rs:783-818`, `:953-1010`).
- Contract 31 FR-8's Windows sentences (`:462-468`) are the rows this
  chunk amends (append, never rewrite).
- #1491's rule, `windows_entry.rs:46-48`: "forward moves, saves and
  creations admit it (`Folders::NotLinks`), the line Rust's own
  `FileType::is_symlink` draws".
- The shared outcome model (plan §4 "shared primitives agree"): this
  spec and WB-30 use **one** `TrashOutcome` enum, defined here in full
  and filled in by WB-30; WB-30's spec repeats it verbatim.

## Design

### The probe procedure (owner-run first; WD14)

1. On the home host, from the pool's image, create a throwaway Hyper-V VM
   (not a pool VM). Inside it, in an elevated PowerShell:
   ```
   $disk = New-VHD -Path C:\probe\scratch.vhdx -SizeBytes 2GB -Dynamic | Mount-VHD -Passthru | Initialize-Disk -PassThru
   $vol  = $disk | New-Partition -AssignDriveLetter -UseMaximumSize | Format-Volume -FileSystem NTFS -NewFileSystemLabel SlateTrashProbe
   $probe = "$($vol.DriveLetter):\"
   New-Item -ItemType Directory "$probe\probe" | Out-Null
   (Get-Volume -DriveLetter $vol.DriveLetter).FileSystemType   # must print NTFS
   ```
   Open the Recycle Bin once in Explorer so `<probe>$Recycle.Bin\<SID>`
   exists.
2. `$env:SLATE_TRASH_PROBE_VOLUME = $probe`, then run each gated fact by
   name (Verification lists the exact lines with the nonzero-count check).
3. Record, per kind in #1499's list (file link and folder link to a
   missing target; a link whose target runs through a file; a junction to
   a missing folder; a link to an `ERROR_ACCESS_DENIED` target; a cycle):
   `DeleteItem` result; the `$R…` entry's reparse data equals the link's;
   the `$I…` record's original path; Restore brings it back (a manual
   step read from the probe log — trash-rs exposes no undo); the target
   untouched. The fact asserts the first three.
4. `Dismount-VHD -Path C:\probe\scratch.vhdx`; discard the VM.
5. The OneDrive probe: the owner supplies a Files On-Demand folder; the
   agent runs the three #1492 scenarios in the app launched under
   `SLATE_LOCAL_APP_DATA` (§4 rule 4) and records the sentences.

### The guard rule

`refuse_a_trash_that_is_not_scratch`'s Windows arm (`:139-140`) becomes:
the path's volume (`GetVolumePathNameW` of the canonical path) must equal
the probe volume's, and that volume must not be the system drive
(`GetWindowsDirectoryW`'s root) — refuse otherwise with both volumes
named. Everything before it stays.

### The facts' shape (#1480)

`trash_handoff_tests.rs` gains `cfg(windows)` twins: `real_system_trash_provider`
lifted to `cfg(any(macos, linux, windows))`; `probe_volume_and_its_recycle_bin()
-> (PathBuf, PathBuf)` (new: the volume and `<volume>$Recycle.Bin\<SID>`,
the SID from `GetTokenInformation(TokenUser)` via `windows_sys`
`Win32_Security`, a feature slate-core already enables, `Cargo.toml:113`);
`recycled_entries` (new: pair each `$I<id>` record — version 2: original
path as a length-prefixed UTF-16 string at offset 28 — with its `$R<id>`
entry and compare identity by volume serial + 128-bit id, the
`windows_entry::identity` form `:612-631`, lifted `pub(super)`).

### The dangling link (#1499): the shared outcome model

Branch on the recorded result; both branches implemented behind one
constant so the PR carries the probe's answer.

- **Taken for every kind:** delete `fs.rs:2305-2317`'s unlink branch and
  `unlink_dangling_symlink` (`:2412-2419`) and `link_target_is_absent`;
  flip `windows_a_link_whose_target_is_missing_is_still_unlinked` to
  `windows_a_link_whose_target_is_missing_goes_to_the_recycle_bin_as_the_link`
  (asserts `f.trash.handed()` holds it and the entry is gone). No FFI
  change; the outcome model below is left to WB-30.
- **Refused for a kind:** keep the unlink for that kind only (matched on
  the `symlink_metadata` kind: file link, directory link, junction) and
  make both hosts truthful through core. Core and `uniffi::Enum`, the
  **one** model WB-30 completes (verbatim in both specs):
  ```rust
  pub enum TrashOutcome {
      /// Re-observed at its destination: the confirmed object, its tree matching (WB-30).
      Confirmed,
      /// Re-observed by name and metadata only: the volume's ids are not identities (WB-30).
      VerifiedByNameAndMetadataOnly,
      /// Something moved, but not the confirmed item or not its tree; `location` where it went (WB-30).
      Mismatch { detail: String, location: Option<String> },
      /// The Trash reported no destination (trash-rs today; `Unknown` on every platform until WB-30).
      Unverifiable { detail: String },
      /// The Trash deleted the item outright and said so (`psiNewlyCreated` NULL, WB-30).
      DeletedOutright,
      /// Slate itself unlinked a dangling link the Recycle Bin refuses (this chunk, Windows only).
      UnlinkedForGood,
  }
  ```
  `VaultProvider::delete` becomes `-> Result<TrashOutcome, VaultError>`;
  every provider but the Windows `FsVaultProvider` returns
  `Unverifiable { detail: "the system Trash reports no destination" }`
  (which is what today's `Ok(())` meant), the Windows arm returns
  `UnlinkedForGood` for an unlinked kind. The session threads it:
  `delete_file_checked` / `delete_folder_checked` return it (FFI
  `delete_file_staged*` / `delete_folder_staged*` return it), and the
  batch (`session.rs:15075`) keeps `trashed: Vec<StructuralBatchItem>`
  **unchanged** (every mac production consumer keeps compiling) and gains
  a parallel field `outcomes: Vec<TrashedOutcome>` (new record
  `TrashedOutcome { item: StructuralBatchItem, outcome: TrashOutcome }`),
  one per `trashed` entry in the same order. The new field changes the
  generated Swift initializer, so the PR edits all 14 Swift literals
  listed above (`outcomes: []` or the matching list) in the same branch;
  the mac production reads of `report.trashed` are untouched; the
  combined-head `swift-tests.yml` must be green before merge. Core copy:
  `A11yEvent::LinkDeletedForGood { name }` (new; `corpus.json`,
  `A11yCorpusCensus.cs:57`, `A11yCorpusCensusTests.swift:50`, census
  count `:666`), spoken by the Windows host instead of "Moved …" when the
  single outcome is `UnlinkedForGood`; the batch sentences at `:2285-2305`
  count `outcomes.Count(o => o.Outcome is UnlinkedForGood)` separately:
  "Moved 2 of 3 to the Recycle Bin; 1 link deleted for good." The mac
  host ignores `outcomes` until PR 41.

### OneDrive (#1492, WD15)

- One classification, `windows_entry::reparse_kind(file) -> ReparseKind
  { Plain, Placeholder, NameSurrogate(tag) }` (new; `Placeholder` = reparse
  attribute set and the tag's name-surrogate bit `0x2000_0000` clear;
  `NameSurrogate` = bit set, which is what `Folders::NotLinks` already
  distinguishes through `Found::ReparseFolder`), used at every site below.
- Identity walk: `windows_entry::open` (`:571`) and `directory()` (`:563`)
  take a `Folders` argument; `capture` and `rename` pass `NotLinks`; a
  `NameSurrogate` ancestor keeps today's refusal.
- Trash inventory, **all three sites**: `check_directory` (`:77`) admits a
  `Placeholder` ancestor and refuses a `NameSurrogate`; the selected
  item (`:58-60`) is admitted as a folder when `Placeholder` (a
  `NameSurrogate` item stays "staged as the link"); the visit
  (`:111-116`) recurses into a `Placeholder` directory and hashes it as a
  folder, refuses an unknown `NameSurrogate` tag as today, and keeps
  `known_link`'s two tags for links. The fingerprint of a placeholder
  folder is its children's, like any folder, so counts are not lost.
- Trash handoff: **stays `PlainOnly` unless the OneDrive probe shows
  `IFileOperation` on a path through a placeholder is not redirected**
  (WD15's recommendation). Either way `pinned_path`'s refusal becomes
  actionable for a `Placeholder` with a Cloud Files tag
  (`IO_REPARSE_TAG_CLOUD*`, `0x9000_xxxx` family): `"<folder>" is a cloud
  placeholder folder (OneDrive Files On-Demand), so Slate cannot move
  items beneath it to the Recycle Bin; make the folder "Always keep on
  this device" and try again`; today's sentence stays for a junction or
  link. Core-owned text (`VaultError::InvalidPath { reason }`, rendered by
  `vault_error_detail`, `a11y.rs:3401-3412`).
- Host: `HasReparsePointInPath` (`Import.cs:401`) adopts the name-surrogate
  test, so importing into a OneDrive vault works.

## Tests

| Fact | Asserts | Mutation |
|---|---|---|
| `trash_handoff_tests.rs` `#[cfg(windows)] #[ignore]` `real_trash_on_windows_lands_in_the_volumes_recycle_bin_with_its_records` | note, folder, link trashed through `PlatformTrash` land as `$R` entries with the same identity, `$I` naming the original path | hand a lexical path; or assert on the system drive |
| `#[ignore]` `real_trash_on_windows_takes_each_dangling_link_kind_as_the_link` | per #1499 kind: `DeleteItem` ok, `$R` is a reparse point with equal data, target untouched; prints which kinds refuse | — (this is the probe) |
| `the_windows_guard_refuses_the_system_drive_and_any_other_volume` | with `PROBE_VOLUME` set to a temp dir on `C:`, the guard panics naming the system drive; set to the scratch volume, a path on `C:` panics | drop the system-drive check |
| flipped or kept: `windows_a_link_whose_target_is_missing_goes_to_the_recycle_bin_as_the_link` / `…_is_still_unlinked_and_reported_so` | per the branch; the kept form asserts `TrashOutcome::UnlinkedForGood` | revert the branch |
| if the model ships: `every_provider_but_windows_reports_unverifiable` (core; WB-30 later narrows it to `a_trash_that_reports_no_destination_is_unverifiable`, because its shared verifier makes `ScratchTrash` confirm on every platform), `a_batch_report_carries_one_outcome_per_trashed_item_in_order` (core), `FileManagementTests.ADeletedForGoodLinkIsNotReportedAsMovedToTheRecycleBin` and `FileManagementTests.ABatchCountsDeletedForGoodLinksSeparately` (Windows) | the variant / the sentences | return `Unverifiable` for the unlink |
| `windows_entry_tests.rs`: `undo_identity_is_captured_through_a_placeholder_folder_and_refused_through_a_junction` | `capture` succeeds beneath `opaque_reparse_point`; refuses beneath a junction | pass `PlainOnly` |
| `trash_snapshot` tests: `the_inventory_admits_a_placeholder_root_item_and_nested_folder_and_refuses_a_junction` | three fixtures: placeholder ancestor, placeholder as the selected item, placeholder nested inside the tree — `snapshot_cancellable` ok with the nested item counted; a junction at each position refused | revert any one of the three sites |
| `trash_handoff_tests.rs`: `a_cloud_placeholder_ancestor_refuses_with_an_actionable_reason` (or `…is_admitted` if the probe allows) | the exact sentence | change the text |
| Windows: `FileManagementTests.ImportIntoAPlaceholderFolderIsNotRefusedAsAReparsePoint` | `HasReparsePointInPath` false for an opaque reparse folder, true for a junction | revert |
| census: `A11yCorpusCensus` row for `LinkDeletedForGood` if it ships; `VaultErrorDetailCensus` unchanged | | |

Every wait in a fact is a `Wait(timeout)` whose result is asserted, with
release in `finally` (none of the facts above needs one; the gated facts
poll the bin with a bounded, asserted deadline).

## Docs and registers

- Contract 31 FR-8: append a dated paragraph after `:468` with the probe
  results per kind and the new Windows rule; the batch sentence ("still
  counts an unlinked dangling link") is superseded by the appended text.
- `18_windows_port/w_c_matrix.md`: the Files tree row's notification
  column gains the new sentence(s) if the model ships.
- `corpus.json` + `A11yCorpusCensus.cs:57` + `A11yCorpusCensusTests.swift:50`
  (+ count `:666`) mirror entry, if the event ships (§4 rule 6); the 14
  Swift `BatchTrashReport` literals above, if the field ships.
- `windows.yml`: a `home-only` job `rust-real-trash` (new) gated on
  `vars.WINDOWS_RUNNER_MODE == 'home-only'` that runs the four PowerShell
  lines above, exports `SLATE_TRASH_PROBE_VOLUME`, runs the two gated
  facts by name and dismounts in `if: always()`; it joins `gate`'s `needs`
  only in that mode. If the pool's VMs cannot attach a VHD (the job fails
  at `Mount-VHD`), the job is removed again in the same PR and the
  owner-run result is pasted into the PR body (WD14's other answer).
- `docs/plans/44_windows_backlog_plan.md` §5 WB-24's row and §3 WD15: no
  edit needed; §5 WB-30's row should say the outcome model may already
  exist from WB-24 (plan author).

## Verification

PowerShell, repo root. The gated lines must print a selected count ≥ 1
(the `--list` form) before the run form.

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb24'
cargo test -p slate-core --locked trash_handoff
cargo test -p slate-core --locked windows_entry
cargo test -p slate-core --locked trash_snapshot
cargo test -p slate-core --locked --no-fail-fast
cargo fmt --all -- --check
cargo clippy --all-targets --workspace -- -D warnings
cargo test --workspace --locked
cargo bench --workspace --no-run
bash scripts/check-license-headers.sh
# the gated facts, on the throwaway VM with $probe from the procedure:
$env:SLATE_TRASH_PROBE_VOLUME = $probe
cargo test -p slate-core --locked -- --ignored --list real_trash_on_windows_lands_in_the_volumes_recycle_bin_with_its_records | Select-String ': test$' | Measure-Object | Select-Object -ExpandProperty Count   # must be 1
cargo test -p slate-core --locked -- --ignored --test-threads=1 real_trash_on_windows_lands_in_the_volumes_recycle_bin_with_its_records
cargo test -p slate-core --locked -- --ignored --test-threads=1 real_trash_on_windows_takes_each_dangling_link_kind_as_the_link
# Windows host (only if the outcome model ships; otherwise no C# changes):
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --filter "FullyQualifiedName~SlateWindows.Tests.FileManagementTests|FullyQualifiedName~SlateWindows.Tests.Censuses.A11yCorpusCensus"
$cs = @('apps/slate-windows/src/SlateWindows/FilesSidebarViewModel.cs','apps/slate-windows/src/SlateWindows/FilesSidebarViewModel.Import.cs','apps/slate-windows/tests/SlateWindows.Tests/FileManagementTests.cs','apps/slate-windows/tests/SlateWindows.Tests/Censuses/A11yCorpusCensus.cs')
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs
```

Mutation run: each row's mutation applied locally, the fact seen failing,
reverted. CI: `windows.yml` (rust-tests, app) plus the `home-only`
real-Trash job; `rust.yml` for `cfg(unix)`; `swift-tests.yml` if the FFI
record changes. AT: NVDA spot check of the single-delete sentence for a
dangling link and of the OneDrive refusal, launched per §4 rule 4;
expected phrases are the exact sentences in Design; recorded in the PR
body.

## Acceptance

- [ ] WD14 and WD15 recorded by the owner before the lane job or the
      walk relaxation is built.
- [ ] The Windows gated facts exist and a lane (or the recorded owner run)
      has run them green on a scratch volume; the probe results per kind
      are in the PR body and FR-8's appended paragraph.
- [ ] The guard refuses the system drive and any volume but the probe's.
- [ ] The dangling-link branch matches the probe: unlink removed, or kept
      per kind with `TrashOutcome::UnlinkedForGood` reported on both single
      and batch paths and the 14 Swift literals edited.
- [ ] `reparse_kind` applied at the identity walk and all three inventory
      sites; the handoff per WD15; the Cloud Files refusal sentence
      core-owned and actionable.
- [ ] Facts pass, seen failing first; format gate clean.
- [ ] PR body: `Closes #1480`, `Closes #1499`, `Closes #1492`, the NVDA
      spot check, and "not delivered" for any kind the hardware could not
      probe (that kind keeps today's behaviour, its issue stays open with
      a comment).

## Brief

Prove the Windows Trash back end on a scratch volume, then act on what the
probe says. Two owner decisions are pending: WD14 (recommended: a
throwaway VM with a VHD, then a `home-only` lane job; otherwise owner-run
with results pasted into the PR body) and WD15 (recommended: identity
walk and inventory admit placeholders, the handoff stays fail-closed
unless the OneDrive probe shows no redirection; otherwise only the
refusal wording ships). Read `trash_handoff.rs:55-200`, `:290-345`,
`trash_handoff_tests.rs:20-60`, `:660-830`, `fs.rs:2268-2320`,
`:2405-2420`, `provider.rs:101`, `windows_entry.rs:40-56`, `:305-330`,
`:555-635`, `trash_snapshot/windows.rs:50-120`, `:210-262`,
`test_support.rs:90-115`, `session.rs:15060-15078`, `:16073-16110`,
`uniffi lib.rs:3158-3183`, `FilesSidebarViewModel.cs:1848-1872`,
`:2280-2306`, `Import.cs:395-405`, `FileManagement.cs:315-320`, contract
31 `:420-470`, `windows.yml:228-345`. Build: the Windows guard arm
(volume equality, never the system drive); `probe_volume_and_its_recycle_bin`
and `recycled_entries`; the two gated Windows facts; then, per the
probe, either delete the unlink branch or keep it per kind and ship the
shared `TrashOutcome` enum (`Confirmed`, `VerifiedByNameAndMetadataOnly`,
`Mismatch { detail, location }`, `Unverifiable { detail }`,
`DeletedOutright`, `UnlinkedForGood`) as `VaultProvider::delete`'s result,
the parallel `BatchTrashReport.outcomes: Vec<TrashedOutcome>` field
(legacy `trashed` unchanged; 14 Swift literals edited —
`BaseBatchTrashQuarantineTests.swift:139`, `BatchHostQuarantineTests.swift:280/427/522/1235/1969/2041`,
`BatchResultPresentationTests.swift:438`, `BatchTrashDirtyCloseTests.swift:78`,
`FileTreeMultiSelectTests.swift:257`, `MarkdownPropertyRecoveryContractTests.swift:190`,
`StructuralUndoTests.swift:145/302`, `WorkspaceTabsTests.swift:83`) and
`A11yEvent::LinkDeletedForGood`; `windows_entry::reparse_kind` applied at
`open`/`directory` and the inventory's three sites; the Cloud Files
refusal sentence; `HasReparsePointInPath` on the name-surrogate test; the
`home-only` lane job. Facts, seen failing first:
`real_trash_on_windows_lands_in_the_volumes_recycle_bin_with_its_records`
(`$R`/`$I` identity and path); `real_trash_on_windows_takes_each_dangling_link_kind_as_the_link`
(per kind); `the_windows_guard_refuses_the_system_drive_and_any_other_volume`;
the flipped or kept dangling-link fact; `every_provider_but_windows_reports_unverifiable`;
`a_batch_report_carries_one_outcome_per_trashed_item_in_order`;
`FileManagementTests.ADeletedForGoodLinkIsNotReportedAsMovedToTheRecycleBin`;
`FileManagementTests.ABatchCountsDeletedForGoodLinksSeparately`;
`undo_identity_is_captured_through_a_placeholder_folder_and_refused_through_a_junction`;
`the_inventory_admits_a_placeholder_root_item_and_nested_folder_and_refuses_a_junction`;
`a_cloud_placeholder_ancestor_refuses_with_an_actionable_reason`;
`FileManagementTests.ImportIntoAPlaceholderFolderIsNotRefusedAsAReparsePoint`;
the `A11yCorpusCensus` row. Registers: contract 31 FR-8 (appended
paragraph), `w_c_matrix.md` Files tree row, `corpus.json` and both census
mirrors, `windows.yml`. Not delivered: any link kind the hardware could
not probe (keeps today's behaviour, issue left open with a comment).
Done when the gated facts have run green on a scratch volume somewhere
recorded, the core suite and the PowerShell gates pass, the Windows,
rust and (if the record changed) swift lanes are green, each mutation
was run locally, and the NVDA spot check is in the PR body. Commit:
`fix(core,windows): the recycle bin back end is tested on a scratch volume, dangling links go to it as the link where it takes them, and a onedrive vault refuses truthfully`.
