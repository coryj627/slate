# WB-27 — 38W: the Windows no-replace primitives are proven on FAT32, exFAT and an SMB share

Issues: Refs #1434, Refs #1470 (the Windows arm; both close in plan 43
PR 38 after this merges, §5 closure matrix). Size S–M. Lanes: rust
(Windows cfg). AT: none. Wave: W9, slot 3. Depends on: WB-24 (shares
`vault/fs.rs` and `windows_entry.rs`; §6 sequencing "WB-24, then WB-27,
then WB-28, then WB-30"). Parallel with: WB-25 (switcher), WB-26.
Decision: none pending here (plan 43's RD9 is recorded on #1434 and
#1470: "probe, then refuse truthfully, never a replacing fallback"; this
chunk supplies the Windows probe).

**Delivery record, 2026-10-31.** Where it differs from the sections
below, it wins.

- **WB-24 is skipped for now (owner, 2026-10-31).** This chunk adds no
  lane job: the `home-only` job it would extend does not exist. Design
  step 4's fallback applies: the gated facts are run by hand, and their
  results are in the PR body and the matrix.
- **The SMB share is `\\localhost\C$`.** It is the default
  administrative share, so no `New-SmbShare` and no elevation are needed.
  The gate accepts it as a UNC path on a `DRIVE_REMOTE` volume, and
  reports the root `\\localhost\C$\`. Run the facts from PowerShell:
  Git Bash rewrites `\\localhost` to `\localhost` before Rust sees it,
  and the gate then refuses the path, as it should.
- **FAT32 and exFAT are not probed.** The run had no elevated session
  for the VHD procedure, and this machine has no FAT volume. Their rows
  in the matrix say "Not probed". The facts are ready: the volume
  procedure below, then the gated command in Verification, fills them in
  one elevated session.
- **FAT32 and exFAT probed, 2026-11-01** (the owner's elevated session;
  doc 24's P-W2). Five of the six gated facts pass on both. The
  case-only fact fails on both: the rename reports success and the
  entry keeps its old spelling, and `MoveFileEx` behaves the same. Filed
  as #1727, outside this chunk; the matrix rows now say so.
- **A third non-ignored fact.**
  `weak_volume_gate_decides_each_kind_from_what_the_volume_reports`. The
  gate is split in two:
  - `read_volume` reads what the volume reports: UNC or not, the drive
    type, the file-system name, and the network protocol through
    `FileRemoteProtocolInfo`;
  - a pure `decide` judges the report.

  The table fact runs `decide` against built-up reports for the volumes
  this probe cannot mount. The plain listing now holds three facts; the
  ignored listing still holds exactly six.
- **SMB observations** (all six gated facts green):
  - the create, the raced rename and move, and the identity rename onto
    an occupied name are each refused by the kernel;
  - a case-only rename keeps one entry and its `FILE_ID_INFO`;
  - the id survives a rename, a new file at the old name gets a
    different id, and a stale identity is refused.
  No replacing rename was observed, so no refusal branch and no seventh
  fact.
- **Red team, 2026-10-31: changes made before the push.**
  - *The SMB gate checks the protocol.* A UNC path on a `DRIVE_REMOTE`
    volume is not enough: WSL's `\\wsl.localhost` is both and is served
    by 9P. The gate now requires `FileRemoteProtocolInfo` to report
    `WNNC_NET_SMB`. The query needs an 8-aligned buffer; with the
    structure's own alignment it answers `ERROR_NOACCESS`.
  - *The FAT kinds must be local.* A share reports its SERVER's file
    system, so `\\host\F$` over a FAT32 volume would otherwise pass as
    local FAT32.
  - *SMB must answer `FILE_ID_INFO`.* The identity facts no longer fall
    back to observation there. A FAT volume may still not answer, and
    that is recorded.
  - *A stale identity accepted is a failure on every volume whose
    identity answers,* not a printed observation. This departs from
    "asserting neither". An undo that renames a different file under
    the old name is the hazard the inverse exists to stop. So, by Design
    step 3's logic, it is a blocking finding for PR 38.
  - *The case-only fact also runs the identity inverse.* The inverse
    makes only the no-replace call, so its success proves the no-replace
    call alone renames the alias. The provider's own rename could reach
    its replacing retry arm and look the same.
  - *The mislabelled-volume precondition reads the file system through
    a handle* (`GetVolumeInformationByHandleW`), independently of the
    gate's root-path read.
  - *The create fact requires the stage folder to exist* before
    asserting it empty.
- **Mutations: 15 distinct, all killed.**
  - The Tests table's case-only mutation, "rename through a temporary
    name that drops the id", is replaced by "recreate the file". On NTFS
    and SMB a rename keeps `FILE_ID_INFO`, so the planned mutation could
    not fail the fact.
  - The full list is P-W1 in the matrix.
  - "Accept any name" is killed by the mislabelled-folder fact and the
    decision table. With the remote guard, the gated gate fact on SMB no
    longer reaches it.

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
  (`AlreadyExists`)". Two callers reach it with `replace = false`:
  - **the ordinary rename and move.** `provider.rename` →
    `rename_unrecorded` (`fs.rs:2349-2355`) → the Windows
    `rename_beneath_root` (`:456-560`), which first **pre-checks the
    destination** — `windows_entry::entry_exists` on the pinned
    destination folder returns `VaultError::DestinationExists` for an
    occupied name before any kernel call (`:512-533`, "the fast
    early-out") — then calls its `before_kernel_rename` seam (`:534`, a
    closure `rename_unrecorded` passes as `|| Ok(())`; the existing
    Windows facts drive it from `fs.rs`'s own `mod tests`, `:4127-4200`,
    because `rename_beneath_root` is private to `vault::fs`), then the
    no-replace kernel call `windows_entry::rename_into(&source,
    destination_parent.handle(), to_leaf, false)` (`:540`), whose
    `AlreadyExists` becomes `DestinationExists` (`:553-557`). So a
    destination occupied **before** the call never reaches the kernel:
    only a destination created after the pre-check tests the primitive.
  - **the identity inverse** (undo and redo). `rename_if_identity`
    (`fs.rs:1918`) → `windows_entry::rename` (`:637-673`): open the
    source, compare its identity (`FILE_ID_INFO`, `:612-631`: volume
    serial + 128-bit id), open the destination folder, then
    `rename_opened` (`:676-681`) → `rename_into(…, false)` (`:680`). It
    has **no** destination pre-check, so an occupied destination reaches
    the kernel. `provider.rename` never calls `rename_opened`.
  The staged create has no destination pre-check either: an occupied
  destination reaches the kernel publish (`fs.rs:739` →
  `publish_staged_no_replace` `:1130-1132` → `publish_staged(…, false)` →
  `rename_into` `:1210`).
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
server decides. FAT32 and exFAT generally answer no `FILE_ID_INFO` at
all (microsoft/STL PR 6187 restores a `GetFileInformationByHandle`
fallback for `std::filesystem` for this reason; `SidebarWriteJournal.cs:73-75`
says the same of FAT32); the 64-bit file index `GetFileInformationByHandle`
gives there is derived from where the directory entry sits, so a rename
or move can change it, and it is not a persistent id (the matrix's "no
persistent IDs" row). WB-28's step 0 records which of the two reads this
chunk's VHDs answer.

## Design

No production code changes unless a probe fails (the one `fs.rs`
addition, `rename_with_hook_for_tests`, is `#[cfg(all(test, windows))]`).
Facts first:

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
   Vec<(VolumeKind, PathBuf)>` (new) panics, naming the variable, unless
   `check_weak_volume(kind, path)` accepts each. `fn
   check_weak_volume(kind: VolumeKind, path: &Path) -> Result<String,
   String>` (new, the gate's one decision, callable on any path) reads
   `GetVolumeInformationW`'s file-system name for `path`'s volume and
   returns it when it matches `kind` — exactly `FAT32` for
   `VolumeKind::Fat32`, `exFAT` for `VolumeKind::Exfat` — or, for
   `VolumeKind::Smb`, when `path` is a UNC path and `GetDriveTypeW`
   reports `DRIVE_REMOTE`; otherwise it returns `Err` with a sentence
   naming the expected kind and what the volume reported. A gate that
   accepted any name would let a mislabelled variable (an NTFS folder
   exported as `SLATE_WEAK_VOLUME_FAT32`) run every gated fact on NTFS
   and record NTFS's answers as FAT32's, so the gate is proven against a
   deliberately mislabelled volume: on every machine, the lane's NTFS
   temp folder offered as each of the three kinds; on the probe machine,
   each mounted volume offered as the two kinds it is not.
2. **Provider-level facts** (each builds an `FsVaultProvider` rooted in a
   fresh subfolder of the volume):
   - create via `write_file_if_absent` to a free name lands with the bytes;
     to an occupied name returns `DestinationExists`, the occupant's bytes
     unchanged, `.slate/tmp` empty afterwards — the create has no
     destination pre-check, so this occupant is refused by the kernel
     publish itself;
   - `rename` and a move into a subfolder to a free name land; **to a
     name occupied after the provider's pre-check** they refuse with
     `DestinationExists` and both entries are unchanged. The ordinary
     rename's pre-check (`fs.rs:512-533`) refuses a destination that is
     occupied before the call without reaching the kernel, so the
     occupant is created inside the pre-check-to-kernel window instead:
     a test wrapper (new, `fs.rs`, `#[cfg(all(test, windows))]`)
     `pub(crate) fn rename_with_hook_for_tests(&self, from: &str, to:
     &str, before_kernel_rename: impl FnOnce() -> io::Result<()>) ->
     Result<(), VaultError>` resolves both paths with
     `resolve_for_mutation`, as `rename_unrecorded` does, and calls the
     private `rename_beneath_root` with the hook, which this module cannot
     reach directly. The fact's hook writes the occupant's bytes at the
     destination through `std::fs::write` (the pin holds the folders, not
     their free names) and records that it ran; the call must then return
     `DestinationExists` from the kernel's `AlreadyExists` arm
     (`:553-557`), the source and the occupant keeping their own bytes.
     That is the primitive's no-replace under a racing creation. An
     occupant present before the call is refused too (the pre-check;
     asserted, but not the proof);
   - `rename_if_identity` with the source's current identity onto an
     occupied name, where `windows_entry::identity` answers: it reaches
     the kernel (`windows_entry::rename` has no destination pre-check,
     `:637-673`) and `rename_opened`'s `rename_into(…, false)` (`:680`)
     refuses, both entries unchanged; where the identity read does not
     answer (FAT32 and exFAT are expected not to), the call refuses on
     the identity before the kernel and the fact records that, asserting
     only the refusal and the unchanged entries;
   - case-only rename (`Note.md` → `note.md`) keeps `FILE_ID_INFO`
     (NTFS, SMB with server IDs) or, on FAT, keeps the entry (listing
     shows the new spelling only) — identity asserted where the volume
     reports persistent ids, the listing otherwise, recording which;
   - `rename_if_identity` with a stale expected identity refuses on
     NTFS/SMB; on FAT32/exFAT the fact records whether
     `windows_entry::identity` (`FileIdInfo`-only, no fallback) answers
     at all — where it does not, every identity-checked rename there
     refuses, fail-closed — and, where it does, whether the id survives
     the rename, asserting neither; the row says what was observed.
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
| `weak_volume_create_lands_on_a_free_name_and_preserves_an_occupied_one` | per volume: bytes land; an occupant present before the call (the create has no pre-check, so the kernel publish meets it) → `DestinationExists`; occupant unchanged; stage cleaned | pass `replace = true` at `fs.rs:1210` (→ the occupant is replaced) |
| `weak_volume_rename_and_move_land_on_a_free_name_and_refuse_an_occupied_one` | per volume: a rename and a move to a free name land with their bytes; then, through `rename_with_hook_for_tests`, the hook creates the destination (`b.md` for the rename, `sub/a.md` for the move) with the occupant's bytes after the pre-check: the hook ran (asserted), the call is `DestinationExists`, the source and the occupant keep their bytes; an occupant present before the call is refused as well (the pre-check, `fs.rs:512-533`) | pass `true` for `replace` in the ordinary rename's kernel call, `rename_into(&source, destination_parent.handle(), to_leaf, false)` at `fs.rs:540` (→ the raced occupant is replaced by the source's bytes and the source is gone; the pre-checked occupant still refuses, which is why it is not the proof) |
| `weak_volume_identity_rename_refuses_an_occupied_name_at_the_kernel` (new) | per volume: where `mutation_identity` (`fs.rs:1907`) answers for the source, `rename_if_identity(from, to, current)` onto an occupied name is `Err` and both entries keep their bytes (no pre-check: the kernel refused); where it does not answer, the observation is recorded and the call's refusal with both entries unchanged is asserted | set `ReplaceIfExists` true in `rename_opened` (`windows_entry.rs:680`) (→ on a volume whose identity answers, the occupant is replaced) |
| `weak_volume_case_only_rename_keeps_the_entry` | per volume: one entry with the new spelling; identity equal where ids persist | rename through a temporary name that drops the id |
| `weak_volume_stale_identity_refuses_where_ids_are_identities` | NTFS/SMB-with-ids: refused; FAT: observation recorded | skip the identity compare |
| `weak_volume_gate_names_the_file_system_it_ran_on` | per volume set: `check_weak_volume` accepts it as its own kind and returns the name the volume reports (`FAT32`, `exFAT`, or a UNC root on `DRIVE_REMOTE`), and refuses it as each of the two other kinds (the FAT32 volume offered as exFAT and as SMB, and so on) | accept any file-system name in `check_weak_volume` (→ the cross-kind offers are accepted); match either FAT kind by the name containing `FAT` (→ the exFAT volume is accepted as FAT32 and the FAT32 volume as exFAT) |
| (conditional) `weak_volume_that_cannot_rename_without_replacing_refuses_truthfully` | the exact reason sentence | fall back to a replacing rename |
| not ignored: `weak_volume_gate_is_quiet_when_no_volume_is_set` | with none set, every gated fact returns early (so `--ignored` runs stay green on a bare runner) | panic on unset |
| not ignored (new): `weak_volume_gate_refuses_a_volume_whose_file_system_does_not_match_its_kind` | the deliberately mislabelled volume, on every machine: a fresh folder under the system temp directory, whose volume the fact first reads independently with `GetVolumeInformationW` and asserts is neither `FAT32` nor `exFAT` (NTFS on the lane; a precondition, so the label is wrong by construction), offered to `check_weak_volume` as `Fat32`, as `Exfat` and as `Smb` is `Err` each time, the first two naming the name the fact read as what the volume reported and the third saying the path is not a UNC path on a remote drive | accept any file-system name (→ `Ok` for the FAT kinds); skip the UNC and `DRIVE_REMOTE` check (→ `Ok` for `Smb`) |

Rust snake_case; every name carries the `weak_volume` substring for the
filter. The counts the Verification enforces: **six** unconditional
`#[ignore]` facts (the first six rows); the refusal fact is a seventh
`#[ignore]` fact **only if that branch ships** (Design step 3); the two
gate facts marked "not ignored" are not ignored, so `-- --ignored
--list` never lists them and they are discovered by the plain
`-- --list`.

## Docs and registers

- `24_structural_identity_capability_matrix.md`: append (never rewrite)
  a dated §3.3 Windows table with one row per volume × primitive from the
  probe (the rename and move rows saying the occupant was created after
  the provider's destination pre-check, so the kernel's no-replace
  answered, and the identity rename's row saying it has no pre-check), a
  §5 Windows block (NTFS, FAT32, exFAT, SMB over `\\localhost`:
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
only, so the two non-ignored gate facts are discovered by the plain
listing and the gated facts by the ignored one.

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb27'
function Assert-Ok([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed ($LASTEXITCODE)" } }
function Assert-Listed([string[]]$listed, [string[]]$facts, [string]$what) {
    foreach ($fact in $facts) {
        if (-not ($listed -match "::$([regex]::Escape($fact)): test$")) { throw "$what does not list $fact" }
    }
}
# the non-ignored gate facts (the quiet gate and the mislabelled volume), on every machine:
$plain = cargo test -p slate-core --locked weak_volume -- --list; Assert-Ok 'listing weak_volume'
Assert-Listed $plain @('weak_volume_gate_is_quiet_when_no_volume_is_set', 'weak_volume_gate_refuses_a_volume_whose_file_system_does_not_match_its_kind') 'the plain listing'
cargo test -p slate-core --locked weak_volume; Assert-Ok 'the non-ignored gate facts'
cargo test -p slate-core --locked windows_entry; Assert-Ok 'windows_entry'
cargo test -p slate-core --locked --no-fail-fast; Assert-Ok 'the whole core suite'
cargo fmt --all -- --check; Assert-Ok 'fmt'
cargo clippy --all-targets --workspace -- -D warnings; Assert-Ok 'clippy'
cargo test --workspace --locked; Assert-Ok 'workspace tests'
cargo bench --workspace --no-run; Assert-Ok 'bench compile'
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh; Assert-Ok 'licence headers'

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
    'weak_volume_identity_rename_refuses_an_occupied_name_at_the_kernel',
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
applied locally against the mounted volumes (the mislabelled-volume
fact's on any machine), the fact seen failing, reverted — the rename
row's is the ordinary rename's no-replace flag at `fs.rs:540`, which only
the occupant raced in through `rename_with_hook_for_tests` can expose
(the pre-checked occupant refuses under it too), and the identity row's
is `rename_opened`'s flag at `windows_entry.rs:680`. CI: `windows.yml` rust-tests (the quiet gate) and the
`home-only` job if the pool can mount; `rust.yml` unaffected. AT: none.

## Acceptance

- [ ] The six unconditional gated facts and the two non-ignored gate
      facts (the quiet gate and the mislabelled volume) exist, each
      discovered by its own listing (the two by the plain `--list`, the
      six by `--ignored --list`); the six have run green on FAT32, exFAT
      and an SMB share (lane or recorded owner run) and their observations
      are in the PR body.
- [ ] The rename's no-replace is proven at the kernel, not at the
      provider's pre-check: the occupant is created after the pre-check
      through `rename_with_hook_for_tests` (`#[cfg(all(test, windows))]`)
      and the mutation run flips the flag at `fs.rs:540`; the identity
      rename's occupied-name refusal is proven where an identity answers,
      with the mutation at `windows_entry.rs:680`.
- [ ] The volume gate refuses a mislabelled volume: the lane's NTFS temp
      folder offered as each kind, and on the probe machine each volume
      offered as the kinds it is not.
- [ ] Either no replacing rename was observed and the refusal fact does
      not exist (the ignored listing holds exactly six), or the truthful
      refusal ships with its seventh gated fact, which runs green, and the
      finding is on #1434/#1470 for PR 38.
- [ ] The matrix's Windows rows appended.
- [ ] PR body `Refs #1434`, `Refs #1470`; "not delivered": any volume the
      hardware could not provide (its facts stay unrun; the row says
      "not probed").

## Brief

Prove the Windows no-replace create and rename on FAT32, exFAT and SMB;
no owner decision is pending (RD9 is recorded on both issues). Read
`fs.rs:456-560` (the Windows `rename_beneath_root`: the destination
pre-check `:512-533` that returns `DestinationExists` for an occupant
present before the call without reaching the kernel, the
`before_kernel_rename` seam `:534`, the no-replace kernel call
`rename_into(&source, destination_parent.handle(), to_leaf, false)`
`:540` and its `AlreadyExists` arm `:553-557`), `:697-770` (the staged
create, which has no destination pre-check, so an occupant reaches the
publish at `:739`), `:1130-1132` and `:1170-1215` (`publish_staged`,
`rename_into` at `:1210`), `:1907-1922` (`mutation_identity`,
`rename_if_identity`), `:2349-2355` (`rename_unrecorded`, which passes
`|| Ok(())` as the seam), `:4127-4200` (the existing Windows facts that
drive the seam from `fs.rs`'s own tests, since `rename_beneath_root` is
private to `vault::fs`); `windows_entry.rs:488-553` (`rename_into`),
`:612-700` (`identity`, and `rename` `:637-673` → `rename_opened`
`:676-681`, the identity inverse's no-replace call at `:680`, with no
destination pre-check; `provider.rename` never calls it);
`windows_entry_tests.rs:1-60`, `trash_handoff.rs:80-100`,
`trash_handoff_tests.rs:783-790` (the gate pattern), `vault/mod.rs:94-104`
(where test modules register),
`24_structural_identity_capability_matrix.md:147-151`, `:173-187`,
`:189-220`.

Build `vault/weak_volume_tests.rs`, registered in `vault/mod.rs` as
`#[cfg(all(test, windows))] mod weak_volume_tests;`, with:
- `fn check_weak_volume(kind: VolumeKind, path: &Path) -> Result<String,
  String>` (new, the gate's one decision): `GetVolumeInformationW`'s
  file-system name for `path`'s volume, returned when it is exactly
  `FAT32` for `Fat32` or `exFAT` for `Exfat`, or, for `Smb`, when `path`
  is UNC and `GetDriveTypeW` reports `DRIVE_REMOTE`; otherwise `Err`
  naming the expected kind and what the volume reported;
- `weak_volumes() -> Vec<(VolumeKind, PathBuf)>` (new) reading
  `SLATE_WEAK_VOLUME_FAT32` / `_EXFAT` / `_SMB` and panicking, naming the
  variable, unless `check_weak_volume` accepts each;
- in `fs.rs`, the test wrapper (new, `#[cfg(all(test, windows))]`)
  `pub(crate) fn rename_with_hook_for_tests(&self, from: &str, to: &str,
  before_kernel_rename: impl FnOnce() -> io::Result<()>) -> Result<(),
  VaultError>`, resolving both paths with `resolve_for_mutation` as
  `rename_unrecorded` does and calling the private `rename_beneath_root`
  with the hook (no production code changes unless a primitive fails).

The facts, each seen failing first under its mutation:
- six unconditional `#[ignore]` facts —
  `weak_volume_create_lands_on_a_free_name_and_preserves_an_occupied_one`
  (bytes land; an occupant present before the call meets the kernel
  publish → `DestinationExists`; occupant unchanged; stage cleaned —
  mutation: pass `replace = true` at `fs.rs:1210`);
  `weak_volume_rename_and_move_land_on_a_free_name_and_refuse_an_occupied_one`
  (a rename and a move to a free name land; then, through
  `rename_with_hook_for_tests`, the hook writes the occupant at the
  destination — `b.md`, `sub/a.md` — after the pre-check and records that
  it ran: the call is `DestinationExists` and the source and the occupant
  keep their bytes; an occupant present before the call is refused too,
  by the pre-check, which is not the proof — mutation: `true` for
  `replace` in `rename_into` at `fs.rs:540`, under which the raced
  occupant is replaced while the pre-checked one still refuses);
  `weak_volume_identity_rename_refuses_an_occupied_name_at_the_kernel`
  (where `mutation_identity` answers, `rename_if_identity` with the
  current identity onto an occupied name is `Err` with both entries
  unchanged — the kernel refused, there being no pre-check; where it does
  not answer, as FAT32 and exFAT are expected not to, the observation is
  recorded and the refusal and unchanged entries asserted — mutation:
  `ReplaceIfExists` true in `rename_opened`, `windows_entry.rs:680`);
  `weak_volume_case_only_rename_keeps_the_entry` (one entry with the new
  spelling; identity equal where ids persist, listing otherwise — rename
  through a temporary name);
  `weak_volume_stale_identity_refuses_where_ids_are_identities` (refused
  on NTFS/SMB with ids; FAT observation recorded — skip the identity
  compare); `weak_volume_gate_names_the_file_system_it_ran_on` (each set
  volume accepted by `check_weak_volume` as its own kind with the name it
  reports, and refused as each of the two other kinds — mutations: accept
  any name; match either FAT kind by the name containing `FAT`);
- two non-ignored facts — `weak_volume_gate_is_quiet_when_no_volume_is_set`
  (with none set every gated body returns early — panic on unset) and
  `weak_volume_gate_refuses_a_volume_whose_file_system_does_not_match_its_kind`
  (the deliberately mislabelled volume on every machine: a fresh folder
  under the system temp directory, its volume first read independently
  and asserted to be neither `FAT32` nor `exFAT`, offered as `Fat32`,
  `Exfat` and `Smb` is `Err` each time, naming the file system read or
  the missing UNC/`DRIVE_REMOTE` — mutations: accept any name; skip the
  UNC check);
- only if a primitive fails, a seventh `#[ignore]` fact,
  `weak_volume_that_cannot_rename_without_replacing_refuses_truthfully`
  (the exact core-owned `InvalidPath` reason — fall back to a replacing
  rename).

Mount the volumes with the captured-object PowerShell procedure (two VHDs
via `New-VHD | Mount-VHD -Passthru | Initialize-Disk | New-Partition
-AssignDriveLetter | Format-Volume`, directories created under the
returned drive letters, `New-SmbShare slate-probe` over
`C:\probe\share`) and export the three variables. Discovery: the plain
`cargo test -p slate-core --locked weak_volume -- --list` must list both
non-ignored gate facts (`--ignored --list` never lists a non-ignored
fact), then `cargo test -p slate-core --locked weak_volume` runs them;
the `-- --ignored --list` listing must hold exactly the six gated names,
or seven when the refusal fact exists in the source; then
`cargo test -p slate-core --locked weak_volume -- --ignored --test-threads=1`.
Each command's `$LASTEXITCODE` is checked and a missing fact throws.
Change production code only if a primitive fails, and then only to
refuse. Registers: the matrix's §3.3 / §5 / §6 Windows rows (appended;
the rename rows say the occupant was raced in after the provider's
pre-check); contract 31 FR-10 only with a refusal. Not delivered: any
volume the hardware cannot provide (row "not probed"). Done when the six
gated facts (seven with a refusal) have run green on all three volumes
somewhere recorded, each mutation was seen failing locally (the rename
row's at `fs.rs:540`, the identity row's at `windows_entry.rs:680`, the
mislabelled-volume fact's on any machine), the two non-ignored gate
facts and the PowerShell gates (whole core suite, fmt, clippy, workspace
tests, bench compile, licence headers) pass, the Windows lane is green,
and the PR body carries `Refs #1434`, `Refs #1470` with the per-volume
table. Commit:
`test(core): the windows no-replace create and rename are proven on fat32, exfat and an smb share`.
