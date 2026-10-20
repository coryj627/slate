# WB-27 — 38W: the Windows no-replace primitives are proven on FAT32, exFAT and an SMB share

Issues: Refs #1434, Refs #1470 (the Windows arm; both close in plan 43
PR 38 after this merges, §5 closure matrix). Size S–M. Lanes: rust
(Windows cfg). AT: none. Wave: W9, slot 3. Depends on: WB-24 (shares
`vault/fs.rs` and `windows_entry.rs`; §6 sequencing "WB-24, then WB-27,
then WB-28, then WB-30"). Parallel with: WB-25 (switcher), WB-26.
Decision: none here (RD9 in plan 43 decided "probe, then refuse
truthfully, never a replacing fallback"; this chunk supplies the Windows
probe).

## Goal

Plan 43 PR 24's capability matrix answered for macOS what the no-replace
primitives do on weak volumes and left Windows as a reference row from
source. This chunk answers it by running: on a FAT32 and an exFAT VHD and
on an SMB share, a create through the staged publish lands when the name
is free and preserves an occupied destination; a rename or move lands on a
free name and refuses an occupied one; a case-only rename keeps the entry's
identity. Where a volume cannot honour no-replace, the fact records it and
the provider refuses with a truthful reason rather than replacing. The
matrix gains Windows rows PR 38 can cite.

## Current state on main

Verified against the worktree; the plan's lines hold.

- **The staged create.** `vault/fs.rs:697-770`
  `write_file_if_absent_with_staging`: pins the destination first
  (`:712`, `pin_mutation_target(relative, true, Mutation::Create)`),
  stages under `.slate/tmp` (`:713-720`), writes and syncs, then
  `publish(&temp, &destination)` (`:738`); on `AlreadyExists` it cleans
  the stage and returns `VaultError::DestinationExists` (`:744-754`); any
  other publish error becomes `destination.failure(publish_error)`
  (`:758`, the typed junction refusal, #1451). Comment at `:733-737`:
  "The shipping no-replace rename is the sole publish point."
- **The Windows publish.** `PinnedMutationTarget::publish_staged`
  (`fs.rs:1178-1211`): opens the staged file `DELETE |
  FILE_WRITE_ATTRIBUTES` with `FILE_FLAG_OPEN_REPARSE_POINT`, resets
  attributes to `FILE_ATTRIBUTE_NORMAL` (`:1186-1206`), then
  `windows_entry::rename_into(&source, self.parent.handle(), &self.leaf,
  replace)` (`:1210`).
- **The primitive.** `windows_entry.rs:497-553` `rename_into(source,
  parent, name, replace)`: `FileRenameInformation` via
  `NtSetInformationFile` with `RootDirectory = parent` and
  `ReplaceIfExists = replace` (`:521-522`); the doc at `:493-496`: "a save
  replaces its target, every other rename refuses an occupied name
  (`AlreadyExists`)". The structural rename is `windows_entry::rename`
  (`:637`) → `rename_opened` (`:676`); identity by
  `FILE_ID_INFO` (`:612-631`: volume serial + 128-bit id).
- **Existing NTFS facts.** `windows_entry_tests.rs:8`
  `conditional_rename_rejects_replacements_and_collisions`, `:23`
  `conditional_rename_handles_case_only_inverse_and_directory_moves`,
  `:62`, `:94`, `:125`, `:202` — all on the temp directory's NTFS. No
  fact runs on another file system.
- **The matrix.** `24_structural_identity_capability_matrix.md` §3.3
  (`:147-151`) is Windows "(reference)": one row for
  `NtSetInformationFile(FileRenameInformation)` from source; §5 (`:173-187`)
  lists APFS, HFS+, FAT32 (`:180`: EXCL honoured via `EEXIST`/success
  though the capability bit is unreported; IDs reused at once), exFAT
  (`:181`: `ENOTSUP` for a free target), SMB (`:183`: not probed, by
  source). §6 (`:189-220`) case-only renames: FAT32 succeeded, exFAT
  `ENOTSUP`. All macOS.
- **The gate pattern to reuse.** `trash_handoff.rs:86`
  `SLATE_TRASH_PROBE_VOLUME` and the `#[ignore = "needs a scratch volume
  …"]` facts (`trash_handoff_tests.rs:783-787`); WB-24 adds the Windows
  gated facts and the `home-only` lane job this chunk extends.
- **The issues' Windows content is nil:** both are macOS findings
  (`renameatx_np`, `renamex_np`, `hdiutil`, FSKit, `VOL_CAP_INT_RENAME_EXCL`);
  their comments assign the mac probe to PR 38. Nothing in them is wrong
  about Windows; nothing in them is about Windows. The Windows behavioural
  spec is this file.

## Reference

The matrix (§3.3, §5, §6) defines the rows; PR 38's mac probe is the twin
("probe a real exFAT image first, and SMB if one is available", RD9 on
#1434). Windows documentation points of record (cite in the matrix rows,
verify by the probe): `FILE_RENAME_INFORMATION.ReplaceIfExists` is
honoured by the file system driver — FastFAT and exFAT implement
`FileRenameInformation` and return `STATUS_OBJECT_NAME_COLLISION` for an
occupied name when `ReplaceIfExists` is false; over SMB the request maps
to `SMB2 SET_INFO FileRenameInformation` with the same flag, and the
server decides. `FILE_ID_INFO` on FAT is synthesised by the driver and is
not persistent across mounts (the matrix's "no persistent IDs" row).

## Design

No production code changes unless a probe fails. Facts first:

1. **A volume gate.** `vault/windows_entry_tests.rs` (or a new
   `vault/weak_volume_tests.rs`, `#![cfg(windows)]`) reads
   `SLATE_WEAK_VOLUME_FAT32`, `SLATE_WEAK_VOLUME_EXFAT`,
   `SLATE_WEAK_VOLUME_SMB` (new env names; each a directory on that
   volume, e.g. `F:\probe`, `E:\probe`, `\\localhost\slate-probe`). Each
   fact is `#[ignore = "needs SLATE_WEAK_VOLUME_* set to a mounted scratch
   volume"]` and iterates the volumes that are set; a helper
   `weak_volumes() -> Vec<(VolumeKind, PathBuf)>` (new) also records
   `GetVolumeInformationW`'s file-system name so the fact asserts it
   really is FAT32 / exFAT / an SMB redirector (`FileSystemName` `FAT32`,
   `exFAT`, `NTFS` with a UNC root).
2. **Provider-level facts** (each builds an `FsVaultProvider` rooted in a
   fresh subfolder of the volume):
   - create via `write_file_if_absent` to a free name lands with the bytes;
     to an occupied name returns `DestinationExists`, the occupant's bytes
     unchanged, `.slate/tmp` empty afterwards;
   - `rename` to a free name lands; to an occupied name refuses with
     `AlreadyExists`-derived `VaultError`, both entries unchanged; a move
     into a subfolder likewise;
   - case-only rename (`Note.md` → `note.md`) keeps `FILE_ID_INFO`
     (NTFS, SMB with server IDs) or, on FAT, keeps the entry (listing shows
     the new spelling only) — the fact asserts identity where the volume
     reports persistent ids and the listing otherwise, recording which;
   - `rename_if_identity` (`fs.rs:1918`) with a stale expected identity
     refuses on NTFS/SMB; on FAT32/exFAT the fact records whether the
     synthesised id survives the rename (it is expected to: the driver
     keys it on the directory entry) and *does not* assert it — the row
     says what was observed.
3. **If a primitive fails on a volume** (an `STATUS_NOT_SUPPORTED` or a
   replacing rename — the §11 risk): the provider gains a truthful
   refusal, never a fallback: `VaultError::InvalidPath { reason }` with
   core-owned text naming the volume's file system ("this volume (exFAT)
   cannot rename without replacing, so nothing was moved"), surfaced
   through `vault_error_detail` as every refusal is. The fact for that
   volume then asserts the refusal. A replacing rename observed anywhere
   is a blocking finding for PR 38 and this chunk (the §11 rule).
4. **The lane.** WB-24's `home-only` job gains two more VHDs
   (`Format-Volume -FileSystem FAT32` needs a ≤ 32 GB partition;
   `-FileSystem exFAT`) and `New-SmbShare -Name slate-probe -Path
   <ntfs dir> -FullAccess $env:USERNAME` (needs elevation, which the pool
   VMs' runner user has), exports the three variables, and runs
   `cargo test -p slate-core --locked -- --ignored weak_volume
   --test-threads=1`. If the pool cannot (no `Mount-VHD`, or SMB disabled
   on the image), the facts stay owner-run and the results go in the PR
   body and the matrix rows, as WD14's fallback does for WB-24.

Deliberately not changed: `rename_into`'s signature; the save path's
`replace = true` (a save replaces its own file by design); nothing on
macOS or Linux (PR 38).

## Tests

| Fact (`#[cfg(windows)] #[ignore]`) | Asserts | Mutation |
|---|---|---|
| `weak_volume_create_lands_on_a_free_name_and_preserves_an_occupied_one` | per volume: bytes land; `DestinationExists`; occupant unchanged; stage cleaned | pass `replace = true` at `fs.rs:1210` |
| `weak_volume_rename_and_move_land_on_a_free_name_and_refuse_an_occupied_one` | per volume: landed; refusal; both entries unchanged | set `ReplaceIfExists` true in `rename_opened` |
| `weak_volume_case_only_rename_keeps_the_entry` | per volume: one entry with the new spelling; identity equal where ids persist | rename through a temporary name that drops the id |
| `weak_volume_stale_identity_refuses_where_ids_are_identities` | NTFS/SMB-with-ids: refused; FAT: observation recorded | skip the identity compare |
| `weak_volume_gate_names_the_file_system_it_ran_on` | `GetVolumeInformationW` name matches the variable's kind | — |
| (conditional) `a_volume_that_cannot_rename_without_replacing_refuses_truthfully` | the exact reason sentence | fall back to a replacing rename |
| not ignored: `the_weak_volume_gate_is_quiet_when_no_volume_is_set` | with none set, every gated fact returns early (so `--ignored` runs stay green on a bare runner) | panic on unset |

Rust snake_case; the facts' names share the `weak_volume` substring for
the lane's filter.

## Docs and registers

- `24_structural_identity_capability_matrix.md`: append (never rewrite)
  a dated §3.3 Windows table with one row per volume × primitive from the
  probe, a §5 Windows block (NTFS, FAT32, exFAT, SMB over `\\localhost`:
  no-replace, case-only, id persistence, id reuse), and a §6 Windows
  paragraph. Reference the fact names.
- Contract 31: no row (the refusal, if any, is a reason text under
  existing FR-10/FR-8 semantics); if a truthful refusal ships, append a
  sentence to FR-10 naming the volume condition.
- `BENCHMARKS.md`: none.
- Issues: `Refs #1434`, `Refs #1470` in the PR body with the per-volume
  table; PR 38 closes both after this merges (closure matrix).

## Verification

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb27'
cargo test -p slate-core --locked weak_volume                    # the quiet gate
cargo test -p slate-core --locked windows_entry
cargo test -p slate-core --locked --no-fail-fast
# on the probe machine (a throwaway VM, or this desktop with VHDs mounted):
New-VHD -Path C:\probe\fat32.vhdx -SizeBytes 1GB -Dynamic | Mount-VHD -Passthru | Initialize-Disk -PassThru | New-Partition -AssignDriveLetter -UseMaximumSize | Format-Volume -FileSystem FAT32
New-VHD -Path C:\probe\exfat.vhdx -SizeBytes 1GB -Dynamic | Mount-VHD -Passthru | Initialize-Disk -PassThru | New-Partition -AssignDriveLetter -UseMaximumSize | Format-Volume -FileSystem exFAT
New-SmbShare -Name slate-probe -Path C:\probe\share -FullAccess $env:USERNAME   # elevated
$env:SLATE_WEAK_VOLUME_FAT32='F:\probe'; $env:SLATE_WEAK_VOLUME_EXFAT='E:\probe'; $env:SLATE_WEAK_VOLUME_SMB='\\localhost\slate-probe'
cargo test -p slate-core --locked -- --ignored weak_volume --test-threads=1
make ci
```

CI: `windows.yml` rust-tests (the quiet gate) and the `home-only` job if
the pool can mount; `rust.yml` unaffected (Windows cfg). AT: none.

## Acceptance

- [ ] The six gated facts and the quiet-gate fact exist; the gated ones
      have run green on FAT32, exFAT and an SMB share (lane or recorded
      owner run) and their observations are in the PR body.
- [ ] No replacing rename was observed; or, if one was, the truthful
      refusal ships and the finding is on #1434/#1470 for PR 38.
- [ ] The matrix's Windows rows appended.
- [ ] PR body `Refs #1434`, `Refs #1470`; "not delivered": any volume the
      hardware could not provide (then its facts stay unrun and the row
      says "not probed").

## Brief

Prove the Windows no-replace create and rename on FAT32, exFAT and SMB.
Read `fs.rs:697-770` and `:1170-1215`, `windows_entry.rs:488-553`,
`:612-700`, `windows_entry_tests.rs:1-60`, `trash_handoff.rs:80-100` and
`trash_handoff_tests.rs:783-790` (the gate pattern), the matrix §3.3,
§5, §6. Add the `SLATE_WEAK_VOLUME_*` gate and the gated provider facts
named in Tests (plus the quiet-gate fact), mount the volumes per
Verification and run them; change production code only if a primitive
fails, and then only to refuse truthfully. Append the matrix rows. Done
when the gated facts have run green on all three volumes somewhere
recorded and the lane is green. Commit:
`test(core): the windows no-replace create and rename are proven on fat32, exfat and an smb share`.
