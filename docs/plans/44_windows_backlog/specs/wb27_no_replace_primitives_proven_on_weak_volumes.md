# WB-27 — 38W: the Windows no-replace primitives are proven on FAT32, exFAT and an SMB share

Issues: Refs #1434, Refs #1470 (the Windows arm; both close in plan 43
PR 38 after this merges, §5 closure matrix). Size S–M. Lanes: rust
(Windows cfg). AT: none. Wave: W9, slot 3. Depends on: WB-24 (shares
`vault/fs.rs` and `windows_entry.rs`; §6 sequencing "WB-24, then WB-27,
then WB-28, then WB-30"). Parallel with: WB-25 (switcher), WB-26.
Decision: none pending here (plan 43's RD9 is recorded on #1434 and
#1470: "probe, then refuse truthfully, never a replacing fallback"; this
chunk supplies the Windows probe).

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
  (`:637`) → `rename_opened` (`:676`); identity by `FILE_ID_INFO`
  (`:612-631`: volume serial + 128-bit id); `rename_if_identity`
  (`fs.rs:1918`).
- **Existing NTFS facts.** `windows_entry_tests.rs:8`
  `conditional_rename_rejects_replacements_and_collisions`, `:23`
  `conditional_rename_handles_case_only_inverse_and_directory_moves`,
  `:62`, `:94`, `:125`, `:202` — all on the temp directory's NTFS. No
  fact runs on another file system.
- **The matrix.** `24_structural_identity_capability_matrix.md` §3.3
  (`:147-151`) is Windows "(reference)": one row for
  `NtSetInformationFile(FileRenameInformation)` from source; §5 (`:173-187`)
  lists APFS, HFS+, FAT32 (`:180`), exFAT (`:181`), SMB (`:183`, not
  probed, by source). §6 (`:189-220`) case-only renames. All macOS.
- **The gate pattern to reuse.** `trash_handoff.rs:86`
  `SLATE_TRASH_PROBE_VOLUME` and the `#[ignore = "needs a scratch volume
  …"]` facts (`trash_handoff_tests.rs:783-787`); WB-24 adds the Windows
  gated facts and the `home-only` lane job this chunk extends.
- **The issues' Windows content is nil:** both are macOS findings
  (`renameatx_np`, `renamex_np`, `hdiutil`, FSKit, `VOL_CAP_INT_RENAME_EXCL`);
  their comments assign the mac probe to PR 38. The Windows behavioural
  spec is this file.

## Reference

The matrix (§3.3, §5, §6) defines the rows; PR 38's mac probe is the twin
(RD9 on #1434: "probe a real exFAT image first, and SMB if one is
available"). Windows documentation points of record (cite in the matrix
rows, verify by the probe): `FILE_RENAME_INFORMATION.ReplaceIfExists` is
honoured by the file system driver — FastFAT and exFAT implement
`FileRenameInformation` and return `STATUS_OBJECT_NAME_COLLISION` for an
occupied name when `ReplaceIfExists` is false; over SMB the request maps
to `SMB2 SET_INFO FileRenameInformation` with the same flag, and the
server decides. `FILE_ID_INFO` on FAT is synthesised by the driver and is
not persistent across mounts (the matrix's "no persistent IDs" row).

## Design

No production code changes unless a probe fails. Facts first:

1. **A volume gate.** New `vault/weak_volume_tests.rs`, registered in
   `vault/mod.rs` as `#[cfg(all(test, windows))] mod weak_volume_tests;`
   beside `test_support` and `test_trash_census` (`:100-104`;
   `windows_entry_tests.rs` is not registered there but through a
   `#[path]` `mod tests` in `windows_entry.rs:683-685`), so every fact's
   path is `vault::weak_volume_tests::…` and the `weak_volume` filter
   selects it. It reads
   `SLATE_WEAK_VOLUME_FAT32`, `SLATE_WEAK_VOLUME_EXFAT`,
   `SLATE_WEAK_VOLUME_SMB` (new env names; each an existing scratch
   directory on that volume). Each fact is `#[ignore = "needs
   SLATE_WEAK_VOLUME_* set to a scratch directory on a mounted volume"]`
   and iterates the volumes that are set; `weak_volumes() ->
   Vec<(VolumeKind, PathBuf)>` (new) records `GetVolumeInformationW`'s
   file-system name and asserts it is `FAT32`, `exFAT`, or (for SMB) that
   the root is a UNC path and `GetDriveTypeW` reports `DRIVE_REMOTE`.
2. **Provider-level facts** (each builds an `FsVaultProvider` rooted in a
   fresh subfolder of the volume):
   - create via `write_file_if_absent` to a free name lands with the bytes;
     to an occupied name returns `DestinationExists`, the occupant's bytes
     unchanged, `.slate/tmp` empty afterwards;
   - `rename` to a free name lands; to an occupied name refuses with the
     `AlreadyExists`-derived `VaultError`, both entries unchanged; a move
     into a subfolder likewise;
   - case-only rename (`Note.md` → `note.md`) keeps `FILE_ID_INFO`
     (NTFS, SMB with server IDs) or, on FAT, keeps the entry (listing
     shows the new spelling only) — identity asserted where the volume
     reports persistent ids, the listing otherwise, recording which;
   - `rename_if_identity` with a stale expected identity refuses on
     NTFS/SMB; on FAT32/exFAT the fact records whether the synthesised id
     survives the rename and does not assert it — the row says what was
     observed.
3. **If a primitive fails on a volume** (`STATUS_NOT_SUPPORTED` or a
   replacing rename — the §11 risk): the provider gains a truthful
   refusal, never a fallback: `VaultError::InvalidPath { reason }` with
   core-owned text naming the volume's file system ("this volume (exFAT)
   cannot rename without replacing, so nothing was moved"), surfaced
   through `vault_error_detail` as every refusal is. The fact for that
   volume then asserts the refusal. A replacing rename observed anywhere
   is a blocking finding for PR 38 and this chunk.
4. **The lane.** WB-24's `home-only` job gains the two VHDs and the share
   from the procedure below and runs the gated facts by name; if the pool
   cannot (no `Mount-VHD`, or SMB disabled on the image), the facts stay
   owner-run and the results go in the PR body and the matrix rows.

Deliberately not changed: `rename_into`'s signature; the save path's
`replace = true`; nothing on macOS or Linux (PR 38).

### The volume procedure (elevated PowerShell; objects captured, never a guessed letter)

```
New-Item -ItemType Directory C:\probe -Force | Out-Null
$fat   = New-VHD -Path C:\probe\fat32.vhdx -SizeBytes 1GB -Dynamic | Mount-VHD -Passthru | Initialize-Disk -PassThru | New-Partition -AssignDriveLetter -UseMaximumSize | Format-Volume -FileSystem FAT32 -NewFileSystemLabel SlateFat
$exfat = New-VHD -Path C:\probe\exfat.vhdx -SizeBytes 1GB -Dynamic | Mount-VHD -Passthru | Initialize-Disk -PassThru | New-Partition -AssignDriveLetter -UseMaximumSize | Format-Volume -FileSystem exFAT -NewFileSystemLabel SlateExfat
$fatRoot   = "$($fat.DriveLetter):\probe";   New-Item -ItemType Directory $fatRoot   | Out-Null
$exfatRoot = "$($exfat.DriveLetter):\probe"; New-Item -ItemType Directory $exfatRoot | Out-Null
New-Item -ItemType Directory C:\probe\share -Force | Out-Null
New-SmbShare -Name slate-probe -Path C:\probe\share -FullAccess $env:USERNAME | Out-Null
$smbRoot = '\\localhost\slate-probe'
(Get-Volume -DriveLetter $fat.DriveLetter).FileSystemType     # FAT32
(Get-Volume -DriveLetter $exfat.DriveLetter).FileSystemType   # exFAT
Test-Path $smbRoot                                            # True
$env:SLATE_WEAK_VOLUME_FAT32 = $fatRoot; $env:SLATE_WEAK_VOLUME_EXFAT = $exfatRoot; $env:SLATE_WEAK_VOLUME_SMB = $smbRoot
# afterwards:
Remove-SmbShare -Name slate-probe -Force; Dismount-VHD -Path C:\probe\fat32.vhdx; Dismount-VHD -Path C:\probe\exfat.vhdx
```

## Tests

| Fact (`#[cfg(windows)] #[ignore]`) | Asserts | Mutation |
|---|---|---|
| `weak_volume_create_lands_on_a_free_name_and_preserves_an_occupied_one` | per volume: bytes land; `DestinationExists`; occupant unchanged; stage cleaned | pass `replace = true` at `fs.rs:1210` |
| `weak_volume_rename_and_move_land_on_a_free_name_and_refuse_an_occupied_one` | per volume: landed; refusal; both entries unchanged | set `ReplaceIfExists` true in `rename_opened` |
| `weak_volume_case_only_rename_keeps_the_entry` | per volume: one entry with the new spelling; identity equal where ids persist | rename through a temporary name that drops the id |
| `weak_volume_stale_identity_refuses_where_ids_are_identities` | NTFS/SMB-with-ids: refused; FAT: observation recorded | skip the identity compare |
| `weak_volume_gate_names_the_file_system_it_ran_on` | the file-system name matches the variable's kind | accept any file-system name in `weak_volumes()` |
| (conditional) `weak_volume_that_cannot_rename_without_replacing_refuses_truthfully` | the exact reason sentence | fall back to a replacing rename |
| not ignored: `weak_volume_gate_is_quiet_when_no_volume_is_set` | with none set, every gated fact returns early (so `--ignored` runs stay green on a bare runner) | panic on unset |

Rust snake_case; every name carries the `weak_volume` substring for the
filter. The counts the Verification enforces: **five** unconditional
`#[ignore]` facts (the first five rows); the refusal fact is a sixth
`#[ignore]` fact **only if that branch ships** (Design step 3); the
quiet-gate fact is not ignored, so `-- --ignored --list` never lists it
and it is discovered by the plain `-- --list`.

## Docs and registers

- `24_structural_identity_capability_matrix.md`: append (never rewrite)
  a dated §3.3 Windows table with one row per volume × primitive from the
  probe, a §5 Windows block (NTFS, FAT32, exFAT, SMB over `\\localhost`:
  no-replace, case-only, id persistence, id reuse), and a §6 Windows
  paragraph, naming the fact names.
- Contract 31 FR-10: a sentence naming the volume condition, only if a
  truthful refusal ships.
- Issues: `Refs #1434`, `Refs #1470` in the PR body with the per-volume
  table; PR 38 closes both after this merges.

## Verification

PowerShell, repo root. Discovery is checked by name and throws on a
missing fact; every command's `$LASTEXITCODE` is checked (no output
grepping for "Failed"). Libtest's `--ignored --list` lists ignored facts
only, so the quiet gate is discovered by the plain listing and the gated
facts by the ignored one.

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb27'
function Assert-Ok([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed ($LASTEXITCODE)" } }
function Assert-Listed([string[]]$listed, [string[]]$facts, [string]$what) {
    foreach ($fact in $facts) {
        if (-not ($listed -match "::$([regex]::Escape($fact)): test$")) { throw "$what does not list $fact" }
    }
}
# the non-ignored quiet gate, on every machine:
$plain = cargo test -p slate-core --locked weak_volume -- --list; Assert-Ok 'listing weak_volume'
Assert-Listed $plain @('weak_volume_gate_is_quiet_when_no_volume_is_set') 'the plain listing'
cargo test -p slate-core --locked weak_volume; Assert-Ok 'the quiet gate'
cargo test -p slate-core --locked windows_entry; Assert-Ok 'windows_entry'
cargo test -p slate-core --locked --no-fail-fast; Assert-Ok 'the whole core suite'
cargo fmt --all -- --check; Assert-Ok 'fmt'
cargo clippy --all-targets --workspace -- -D warnings; Assert-Ok 'clippy'
cargo test --workspace --locked; Assert-Ok 'workspace tests'
cargo bench --workspace --no-run; Assert-Ok 'bench compile'
bash scripts/check-license-headers.sh; Assert-Ok 'licence headers'

# on the probe machine, in the session where the volume procedure above set the three variables:
$probed = @()
foreach ($name in 'SLATE_WEAK_VOLUME_FAT32', 'SLATE_WEAK_VOLUME_EXFAT', 'SLATE_WEAK_VOLUME_SMB') {
    $value = [Environment]::GetEnvironmentVariable($name)
    if ($value -and -not (Test-Path $value)) { throw "$name names $value, which does not exist" }
    if ($value) { $probed += $name } else { "$name unset: that volume is 'not probed' in the PR body and the matrix" }
}
if ($probed.Count -eq 0) { throw 'no SLATE_WEAK_VOLUME_* is set; run the volume procedure first' }
$gated = @(
    'weak_volume_create_lands_on_a_free_name_and_preserves_an_occupied_one',
    'weak_volume_rename_and_move_land_on_a_free_name_and_refuse_an_occupied_one',
    'weak_volume_case_only_rename_keeps_the_entry',
    'weak_volume_stale_identity_refuses_where_ids_are_identities',
    'weak_volume_gate_names_the_file_system_it_ran_on'
)
$refusal = 'weak_volume_that_cannot_rename_without_replacing_refuses_truthfully'
if (Select-String -Path crates/slate-core/src/vault/weak_volume_tests.rs -Pattern "fn $refusal\(" -Quiet) { $gated += $refusal }   # only when the refusal branch shipped
$ignored = cargo test -p slate-core --locked weak_volume -- --ignored --list; Assert-Ok 'listing the gated facts'
Assert-Listed $ignored $gated 'the ignored listing'
$count = @($ignored | Select-String ': test$').Count
if ($count -ne $gated.Count) { throw "the ignored listing has $count facts; expected $($gated.Count)" }
cargo test -p slate-core --locked weak_volume -- --ignored --test-threads=1; Assert-Ok 'the gated facts'
```

No C# changes, so no `dotnet` steps. Mutation run: each row's mutation
applied locally against the mounted volumes, the fact seen failing,
reverted. CI: `windows.yml` rust-tests (the quiet gate) and the
`home-only` job if the pool can mount; `rust.yml` unaffected. AT: none.

## Acceptance

- [ ] The five unconditional gated facts and the quiet-gate fact exist,
      each discovered by its own listing (the quiet gate by the plain
      `--list`, the five by `--ignored --list`); the five have run green on
      FAT32, exFAT and an SMB share (lane or recorded owner run) and their
      observations are in the PR body.
- [ ] Either no replacing rename was observed and the refusal fact does
      not exist (the ignored listing holds exactly five), or the truthful
      refusal ships with its sixth gated fact, which runs green, and the
      finding is on #1434/#1470 for PR 38.
- [ ] The matrix's Windows rows appended.
- [ ] PR body `Refs #1434`, `Refs #1470`; "not delivered": any volume the
      hardware could not provide (its facts stay unrun; the row says
      "not probed").

## Brief

Prove the Windows no-replace create and rename on FAT32, exFAT and SMB;
no owner decision is pending (RD9 is recorded on both issues). Read
`fs.rs:697-770` and `:1170-1215`, `windows_entry.rs:488-553`, `:612-700`,
`windows_entry_tests.rs:1-60`, `trash_handoff.rs:80-100`,
`trash_handoff_tests.rs:783-790` (the gate pattern), `vault/mod.rs:94-104`
(where test modules register),
`24_structural_identity_capability_matrix.md:147-151`, `:173-187`,
`:189-220`. Build `vault/weak_volume_tests.rs`, registered in
`vault/mod.rs` as `#[cfg(all(test, windows))] mod weak_volume_tests;`,
with `weak_volumes() -> Vec<(VolumeKind, PathBuf)>` (new) reading
`SLATE_WEAK_VOLUME_FAT32` / `_EXFAT` / `_SMB` and checking the file-system
name (`GetVolumeInformationW`; UNC and `DRIVE_REMOTE` for SMB), and the
facts, each seen failing first under its mutation:
- five unconditional `#[ignore]` facts —
  `weak_volume_create_lands_on_a_free_name_and_preserves_an_occupied_one`
  (bytes land; `DestinationExists`; occupant unchanged; stage cleaned —
  pass `replace = true` at `fs.rs:1210`);
  `weak_volume_rename_and_move_land_on_a_free_name_and_refuse_an_occupied_one`
  (landed; refusal; both entries unchanged — `ReplaceIfExists` true in
  `rename_opened`); `weak_volume_case_only_rename_keeps_the_entry` (one
  entry with the new spelling; identity equal where ids persist, listing
  otherwise — rename through a temporary name);
  `weak_volume_stale_identity_refuses_where_ids_are_identities` (refused
  on NTFS/SMB with ids; FAT observation recorded — skip the identity
  compare); `weak_volume_gate_names_the_file_system_it_ran_on` (the
  file-system name matches the variable's kind — accept any name);
- one non-ignored fact, `weak_volume_gate_is_quiet_when_no_volume_is_set`
  (with none set every gated body returns early — panic on unset);
- only if a primitive fails, a sixth `#[ignore]` fact,
  `weak_volume_that_cannot_rename_without_replacing_refuses_truthfully`
  (the exact core-owned `InvalidPath` reason — fall back to a replacing
  rename).

Mount the volumes with the captured-object PowerShell procedure (two VHDs
via `New-VHD | Mount-VHD -Passthru | Initialize-Disk | New-Partition
-AssignDriveLetter | Format-Volume`, directories created under the
returned drive letters, `New-SmbShare slate-probe` over
`C:\probe\share`) and export the three variables. Discovery: the plain
`cargo test -p slate-core --locked weak_volume -- --list` must list the
quiet gate (`--ignored --list` never lists a non-ignored fact); the
`-- --ignored --list` listing must hold exactly the five gated names, or
six when the refusal fact exists in the source; then
`cargo test -p slate-core --locked weak_volume -- --ignored --test-threads=1`.
Each command's `$LASTEXITCODE` is checked and a missing fact throws.
Change production code only if a primitive fails, and then only to
refuse. Registers: the matrix's §3.3 / §5 / §6 Windows rows (appended);
contract 31 FR-10 only with a refusal. Not delivered: any volume the
hardware cannot provide (row "not probed"). Done when the five gated
facts (six with a refusal) have run green on all three volumes somewhere
recorded, the quiet gate and the PowerShell gates (whole core suite,
fmt, clippy, workspace tests, bench compile, licence headers) pass, the
Windows lane is green, and the PR body carries `Refs #1434`,
`Refs #1470` with the per-volume table. Commit:
`test(core): the windows no-replace create and rename are proven on fat32, exfat and an smb share`.
