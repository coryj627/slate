# WB-24 — Trash on Windows is tested again, trashes a dangling link as the link where the Recycle Bin takes it, and tells the truth in a OneDrive vault

Issues: #1480 #1499 #1492. Size M. Lanes: rust (Windows cfg), windows.
AT: NVDA spot (any changed Trash or refusal sentence). Wave: W8, slot 2
after WB-23. Depends on: WB-1 (the field-pass launch script; a shared
`SlateDeviceState.Root` is not needed by this chunk). Parallel with:
WB-22, WB-21b. Decision: WD14 (lane and owner-machine probes), WD15 (the
`NotLinks` relaxation) — both answered with recommendations; this spec
consumes them as written.

## Goal

Three things become true. A gated fact exercises core's real Windows Trash
back end (trash-rs `IFileOperation::DeleteItem`, `FOF_ALLOWUNDO`) against a
scratch volume's own `$Recycle.Bin`, so the back end #1455 took out of
every test run is tested again, by the lane WD14 names. A dangling link is
handed to the Recycle Bin as the link, as macOS and Linux do since #1495,
once the probe shows the Recycle Bin takes it; where it refuses a kind,
Slate keeps unlinking that kind and the host stops saying "Moved … to the
Recycle Bin" about it. And in a OneDrive Files On-Demand vault, undo and
redo work (a placeholder folder is a folder, #1491's rule) and Move to
Trash either works or refuses with a reason a user can act on.

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
  The invocation documented at `:783-787` filters
  `real_trash_on_a_scratch_volume`; the plan's `-- --ignored real_trash`
  matches every gated fact by substring.
- **The Windows unlink.** `vault/fs.rs:2305-2317` (inside the Windows
  `delete` arm): after `pinned_path`, a symlink whose
  `fs::metadata(&handed)` fails with
  `trash_handoff::link_target_is_absent` (`trash_handoff.rs:184-194`:
  `ERROR_FILE_NOT_FOUND | ERROR_PATH_NOT_FOUND | ERROR_DIRECTORY`) is
  removed by `unlink_dangling_symlink` (`:2412-2419`, `remove_dir` for a
  directory symlink else `remove_file`) and `Ok(())` returns: no error,
  nothing reaches the Trash. Pinned by
  `windows_a_link_whose_target_is_missing_is_still_unlinked`
  (`trash_handoff_tests.rs:663-686`; its doc names #1499). The two kinds
  already handed to the Recycle Bin, unverified:
  `…cannot_be_opened_goes_to_the_recycle_bin_as_the_link` (`:689`) and
  `…link_cycle_goes_to_the_recycle_bin_as_the_link` (`:724`).
- **The host says "Moved".** `FilesSidebarViewModel.cs:1869`
  `ReportMutationResult($"Moved {node.DisplayName} to the Recycle Bin.")`
  after any successful single delete; the batch summary sentences at
  `:2285-2305` count `report.Trashed.Length`, and core puts an unlinked
  link in `trashed` (`session.rs:15075` `self.provider.delete(…)` → `Ok`).
  Contract 31 FR-8 (`31_file_management_contracts.md:420-470`) records the
  Windows exception in its last sentences (:462-468: "Windows still unlinks
  a link whose target is truly absent … until the Recycle Bin is checked
  with one on a scratch VM (#1499) … on Windows a batch still counts an
  unlinked dangling link among the trashed items").
- **The reparse walks (#1492).** `windows_entry.rs:40-50` (module doc:
  placeholders are folders for forward moves, saves and creations; the
  Trash keeps #1450's rule). `Folders` (`:310-330`): `NotLinks` admits
  `Found::ReparseFolder`, `PlainOnly` does not. The identity walk
  `windows_entry::open` (`:571-580`) opens the root with
  `FILE_FLAG_OPEN_REPARSE_POINT` and calls `directory()` (`:563-569`),
  which refuses any reparse attribute with `refused()` (`:555-561`:
  "Cannot undo or redo: the files have changed or their identities cannot
  be verified"); `capture` (`:633-635`) is what `FsVaultProvider::
  mutation_identity` calls (`fs.rs:1907-1915`), behind
  `VaultSession::capture_structural_identity` (`session.rs:16564`) and the
  host's `TryCaptureStructuralIdentity` (`FileManagement.cs:315-319`,
  `null` on `VaultException` → `StructuralHistoryBarrier`). The Trash
  inventory `trash_snapshot/windows.rs:77-83` `check_directory` refuses
  `is_link` (`:73-75`, the reparse attribute); `known_link` (`:214-231`)
  admits only tags `0xA000_0003 | 0xA000_000C`. The handoff
  `trash_handoff::pinned_path` (`:298-320`) walks with `Folders::PlainOnly`
  (`:314`). The host's import check `HasReparsePointInPath`
  (`Import.cs:401`, used `:248`). `test_support::opaque_reparse_point`
  (`test_support.rs:98-115`) makes a folder that is a reparse point
  redirecting nothing — the existing stand-in for a placeholder in the
  #1451 facts (`windows_entry_tests.rs:125`).
- **The lane.** `windows.yml:228-232` rust-tests runs on the home pool
  (`slate-win-rust`) or Namespace; `:335-341` runs `cargo test -p slate-core
  --profile ci --locked -- --skip census_ …` with no `--ignored`, so no
  gated fact runs anywhere today. `vars.WINDOWS_RUNNER_MODE` picks the
  pool (`:37-74`, `:191-218`).

## Reference

- The macOS twin: `real_trash_on_a_scratch_volume_lands_in_its_trashes_with_put_back_records`
  and `…takes_each_unresolvable_link_as_the_link`
  (`trash_handoff_tests.rs:783-818`, `:953-1010`): same items, same
  "landed as the same object" assertion, platform Trash layout differs.
- Contract 31 FR-8's Windows sentences (`:462-468`) are the rows this
  chunk amends (append, never rewrite).
- #1491's rule, quoted from `windows_entry.rs:46-48`: "forward moves,
  saves and creations admit it (`Folders::NotLinks`), the line Rust's own
  `FileType::is_symlink` draws".
- Trash doc `24_trash_handoff_feasibility.md:150-154` (T2's Windows
  paragraph: the progress sink, "not probed") is WB-30's, not this
  chunk's; this chunk does not touch the back end's API.

## Design

### The probe procedure (owner-run first; WD14)

1. On the home host, from the pool's image, create a throwaway Hyper-V VM
   (not a pool VM: the shell gate's VMs must stay as imaged). Inside it:
   `New-VHD -Path C:\probe\scratch.vhdx -SizeBytes 2GB -Dynamic`;
   `Mount-VHD -Path C:\probe\scratch.vhdx -Passthru | Initialize-Disk
   -PassThru | New-Partition -AssignDriveLetter -UseMaximumSize |
   Format-Volume -FileSystem NTFS -NewFileSystemLabel SlateTrashProbe`;
   note the letter (say `S:`). Open the Recycle Bin once in Explorer so
   `S:\$Recycle.Bin\<SID>` exists.
2. `$env:SLATE_TRASH_PROBE_VOLUME='S:\'`;
   `cargo test -p slate-core --locked -- --ignored real_trash
   --test-threads=1` (on Windows only the `cfg(windows)` gated facts
   compile; the `--test-threads=1` keeps two facts from racing the bin).
3. Record, per kind in #1499's list (file link and folder link to a
   missing target; a link whose target runs through a file; a junction to
   a missing folder; a link to an `ERROR_ACCESS_DENIED` target; a cycle):
   `DeleteItem` result; the `$R…` entry's reparse data equals the link's;
   the `$I…` record's original path; Restore brings it back; the target
   untouched. The fact itself asserts the first three; Restore is read
   from the probe log (the fact can call `IFileOperation::… Undo` only
   through a shell API trash-rs does not expose, so it is a recorded
   manual step).
4. `Dismount-VHD`; discard the VM.
5. The OneDrive probe (WD14's last sentence): the owner supplies a Files
   On-Demand folder; the agent runs the three #1492 scenarios in the app
   under `SLATE_LOCAL_APP_DATA` (§4 rule 4) and records the sentences.

### The guard rule

`refuse_a_trash_that_is_not_scratch`'s Windows arm (`:139-140`) becomes:
the path's volume (`GetVolumePathNameW` of the canonical path) must equal
the probe volume's, and that volume must not be the system drive
(`GetWindowsDirectoryW`'s root) — refuse otherwise with the volume named.
Everything before it (`PROBE_VOLUME` set, canonicalised, path on it) stays.

### The facts' shape (#1480)

`trash_handoff_tests.rs` gains `cfg(windows)` twins: `real_system_trash_provider`
lifted to `cfg(any(macos, linux, windows))`; `probe_volume_and_its_recycle_bin()
-> (PathBuf, PathBuf)` (new: the volume and `<volume>\$Recycle.Bin\<SID>`,
the SID from `GetTokenInformation(TokenUser)` via `windows_sys`
`Win32_Security`, already a feature in `Cargo.toml:113`); `recycled_entries`
(new: pair each `$I<id>` record — original path is the UTF-16 string at
offset 28 of the version-2 record, length-prefixed — with its `$R<id>`
entry and compare identity by volume serial + 128-bit id, the
`windows_entry::identity` form `:612-631`, lifted `pub(super)`).

### The dangling link (#1499)

Branch on the recorded result, both branches implemented behind one
constant so the PR carries the probe's answer:

- **Taken:** delete `fs.rs:2305-2317`'s unlink branch and
  `unlink_dangling_symlink` (`:2412-2419`); `link_target_is_absent` stays
  only if a refusing kind remains. Flip
  `windows_a_link_whose_target_is_missing_is_still_unlinked` to
  `windows_a_link_whose_target_is_missing_goes_to_the_recycle_bin_as_the_link`
  (asserts `f.trash.handed()` holds it and the entry is gone).
- **Refused for a kind:** keep the unlink for that kind only (matched on
  the `symlink_metadata` kind: file link, directory link, junction) and
  make both hosts' reporting truthful. Today the provider's `Ok(())`
  cannot say which happened, and the host composes "Moved …" itself
  (`FilesSidebarViewModel.cs:1869`), so the truth needs a core answer, not
  an error (the item is gone; `VaultError::Trash` would be false too):
  `VaultProvider::delete` changes to `-> Result<Deleted, VaultError>` with
  `pub enum Deleted { Trashed, UnlinkedForGood }` (new; every other
  provider returns `Trashed`), threaded through `delete_file_checked` /
  `delete_folder_checked` and the batch (`session.rs:15075`); the FFI
  `delete_file_staged*` returns it and `BatchTrashReport` gains
  `unlinked: Vec<StructuralBatchItem>` (new field; both bindings
  regenerate; the Swift lane compiles and the mac host never receives
  `UnlinkedForGood`). Core speaks the single case through a new
  `A11yEvent::LinkDeletedForGood { name }` (census row, both corpus
  mirrors per §4 rule 6), which the Windows host posts instead of
  "Moved …"; the batch sentences at `:2285-2305` count `unlinked`
  separately ("Moved 2 of 3 to the Recycle Bin; 1 link deleted for
  good."). The outcome type WB-30 later adds (`TrashOutcome`) subsumes
  `Deleted`; WB-30's spec names the migration. If every kind is taken,
  none of this ships and the PR body says so.

### OneDrive (#1492, WD15)

- Identity walk: `windows_entry::open` (`:571`) and `directory()` (`:563`)
  take a `Folders` argument; `capture` and `rename` pass `NotLinks`; the
  refusal for a name-surrogate stays. Undo and redo then work in a
  placeholder vault.
- Trash inventory: `check_directory` admits a reparse folder whose tag is
  not a name surrogate (`known_link`'s complement: tag bit `0x2000_0000`
  clear), fail-closed otherwise.
- Trash handoff: **stays `PlainOnly` unless the OneDrive probe shows
  `IFileOperation` on a path through a placeholder is not redirected**
  (WD15). Either way the refusal text at `pinned_path` becomes actionable:
  `"<folder>" is a cloud placeholder folder (OneDrive Files On-Demand), so
  Slate cannot move items beneath it to the Recycle Bin; make the folder
  "Always keep on this device" and try again` for a Cloud Files tag
  (`IO_REPARSE_TAG_CLOUD*`, `0x9000_xxxx` family), keeping today's
  sentence for a junction or link. Core-owned text (`VaultError::InvalidPath
  { reason }`, rendered by `vault_error_detail`, `a11y.rs:3401-3412`).
- Host: `HasReparsePointInPath` (`Import.cs:401`) adopts the same
  name-surrogate test, so importing into a OneDrive vault works.

What happens if the owner answers WD15 otherwise (keep all three
fail-closed): only the refusal wording ships, and the identity-walk
relaxation is dropped; undo stays a barrier in such vaults and the PR body
records it.

## Tests

| Fact | Asserts | Mutation |
|---|---|---|
| `trash_handoff_tests.rs` `#[cfg(windows)] #[ignore]` `real_trash_on_windows_lands_in_the_volumes_recycle_bin_with_its_records` | note, folder, link trashed through `PlatformTrash` land as `$R` entries with the same identity, `$I` naming the original path | hand a lexical path; or assert on the system drive |
| `#[ignore]` `real_trash_on_windows_takes_each_dangling_link_kind_as_the_link` | per #1499 kind: `DeleteItem` ok, `$R` is a reparse point with equal data, target untouched; records which kinds refuse | — (this *is* the probe) |
| `the_windows_guard_refuses_the_system_drive_and_any_other_volume` | with `PROBE_VOLUME` set to a temp dir on `C:`, the guard panics naming the system drive; set to the scratch volume, a path on `C:` panics | drop the system-drive check |
| flipped or kept: `windows_a_link_whose_target_is_missing_…` | per the branch above | revert the branch |
| if `Deleted` ships: `an_unlinked_link_is_reported_unlinked_not_trashed` (core) and `FileManagementTests.ADeletedForGoodLinkIsNotReportedAsMovedToTheRecycleBin` (Windows) | `Deleted::UnlinkedForGood` / the new sentence; batch `unlinked` count | return `Trashed` |
| `windows_entry_tests.rs`: `undo_identity_is_captured_through_a_placeholder_folder` | `capture` succeeds beneath `opaque_reparse_point`; refuses beneath a junction | pass `PlainOnly` |
| `trash_snapshot` tests: `the_inventory_walks_a_placeholder_folder_and_refuses_a_junction` | `snapshot_cancellable` ok through an opaque reparse folder; `refused` for a junction | revert `check_directory` |
| `trash_handoff_tests.rs`: `a_cloud_placeholder_ancestor_refuses_with_an_actionable_reason` (or `…is_admitted` if WD15's probe allows) | the exact sentence | change the text |
| Windows: `FileManagementTests.ImportIntoAPlaceholderFolderIsNotRefusedAsAReparsePoint` | `HasReparsePointInPath` false for an opaque reparse folder, true for a junction | revert |
| census: `A11yCorpusCensus` row for `LinkDeletedForGood` if it ships; `VaultErrorDetailCensus` unchanged (the reason flows through `InvalidPath`) | | |

## Docs and registers

- Contract 31 FR-8: append a dated paragraph after `:468` with the probe
  results per kind and the new Windows rule; FR-8's batch sentence
  ("still counts an unlinked dangling link") is superseded by the
  appended text, not edited.
- `18_windows_port/w_c_matrix.md`: the Files tree row's notification
  column gains the new sentence(s) if `Deleted` ships.
- `corpus.json` + `A11yCorpusCensus.cs:57` + mac
  `A11yCorpusCensusTests.swift:50` mirror entry, if the event ships (§4
  rule 6; the PR body names the blind mac edit).
- `windows.yml`: a `home-only` job `rust-real-trash` (new) that runs only
  when `vars.WINDOWS_RUNNER_MODE == 'home-only'`, provisions a VHD with
  the four PowerShell lines above, exports `SLATE_TRASH_PROBE_VOLUME`,
  runs the `--ignored real_trash` invocation and dismounts in `if:
  always()`. It joins `gate`'s `needs` only in that mode. If the pool's
  VMs cannot attach a VHD (the job fails at `Mount-VHD`), the job is
  removed again in the same PR and the owner-run result is pasted into the
  PR body (WD14's fallback); WB-30 then records each later run.
- `docs/plans/44_windows_backlog_plan.md` §7: no edit (the probe hardware
  line already lists the VHD).

## Verification

```
# core, this machine (the gated facts stay ignored here)
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb24'
cargo test -p slate-core --locked trash_handoff
cargo test -p slate-core --locked windows_entry
cargo test -p slate-core --locked trash_snapshot
cargo test -p slate-core --locked --no-fail-fast
# the gated facts, on the throwaway VM (procedure above)
$env:SLATE_TRASH_PROBE_VOLUME='S:\'; cargo test -p slate-core --locked -- --ignored real_trash --test-threads=1
# Windows host
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --filter "FullyQualifiedName~FileManagementTests"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <edited .cs files>
```

CI: `windows.yml` (rust-tests, app) plus the `home-only` real-Trash job;
`rust.yml` for `cfg(unix)`; `swift-tests.yml` only if the FFI record
changes. AT: NVDA spot check of the single-delete sentence for a dangling
link and of the OneDrive refusal, launched per §4 rule 4; expected phrases
are the exact sentences in Design; recorded in the PR body.

## Acceptance

- [ ] The Windows gated facts exist and a lane (or the recorded owner run)
      has run them green on a scratch volume; the probe results per kind
      are in the PR body and FR-8's appended paragraph.
- [ ] The guard refuses the system drive and any volume but the probe's.
- [ ] The dangling-link branch matches the probe: unlink removed, or kept
      per kind with truthful reporting on both single and batch paths.
- [ ] Identity walk and inventory follow `NotLinks`; the handoff per WD15's
      probe; the Cloud Files refusal sentence is core-owned and actionable.
- [ ] Facts in Tests pass, seen failing first; format gate clean.
- [ ] PR body: `Closes #1480`, `Closes #1499`, `Closes #1492` (all three
      close here per §5), the NVDA spot check, and "not delivered" for any
      kind the hardware could not probe (§11: nothing merges on an
      assumption — a kind not probed keeps today's behaviour and its
      refusal stays, with the issue left open and commented if so).

## Brief

Prove the Windows Trash back end on a scratch volume, then act on what the
probe says. Read `trash_handoff.rs:55-200`, `:290-345`,
`trash_handoff_tests.rs:20-60`, `:660-830`, `fs.rs:2268-2320`, `:2405-2420`,
`windows_entry.rs:40-56`, `:305-330`, `:555-635`,
`trash_snapshot/windows.rs:70-85`, `:210-235`, `test_support.rs:90-115`,
`FilesSidebarViewModel.cs:1848-1872`, `:2280-2306`, `Import.cs:395-405`,
`FileManagement.cs:315-320`, contract 31 `:420-470`, `windows.yml:228-345`.
Write the two gated Windows facts and the guard rule; run them on a
throwaway VM with a VHD per the procedure; branch the dangling-link code
on the result; relax the identity walk and inventory to `NotLinks`, keep
or relax the handoff per the OneDrive probe, and word the Cloud Files
refusal. Add the `home-only` lane job (or record its impossibility).
Registers: FR-8 appended paragraph, corpus row if the event ships. Done
when the gated facts have run green somewhere recorded, the suite and
lanes are green, and the spot check is in the PR body. Commit:
`fix(core,windows): the recycle bin back end is tested on a scratch volume, dangling links go to it as the link where it takes them, and a onedrive vault refuses truthfully`.
