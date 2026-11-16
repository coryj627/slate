# 24 — Structural identity capability matrix (#1125 Track B)

Status: evidence for owner review under D4 (test more and build the
matrix before any policy call). No code change. Written 2026-10-16 and
2026-10-17 against `main` at 13624900, revised after an independent
red-team review whose findings were checked and, where right, probed.
The Trash handoff (Track A) is in `24_trash_handoff_feasibility.md`; the
five decisions are in `24_decision_memo.md`.

Every cell says where its claim comes from: **[P*n*]** is a probe run on
this Mac (Appendix A lists the probes and the volumes V0-V5), **[man]** a
man page, **[src]** source code, **[doc]** vendor documentation. A cell
marked *not probed* rests on documentation or source only.

## 1. The question and the bar

FR-10 (`docs/plans/31_file_management_contracts.md:454-478`) records that
Windows inverses compare the expected identity and rename the same opened
object, and that macOS and Linux "still resolve source names". The
provider interface refuses the capability elsewhere and forbids faking it
(`crates/slate-core/src/vault/provider.rs:120-139`: "Never implement this
as stat/check followed by a path-addressed rename").

The Windows primitive gives three properties at once
(`crates/slate-core/src/vault/windows_entry.rs:36-88`, `:152-261`):

1. **Compare on the handle.** The volume serial and 128-bit file ID are
   read from the opened source handle.
2. **Mutate the same handle.** `NtSetInformationFile(FileRenameInformation)`
   renames that handle's object, relative to a pinned destination-parent
   handle, with `ReplaceIfExists` false.
3. **Exclude other renamers during the window.** Every handle is opened
   with `FILE_SHARE_READ | FILE_SHARE_WRITE` and without
   `FILE_SHARE_DELETE`. Microsoft documents that without that flag "no
   process can open the file or device if it requests delete access", and
   that "Delete access allows both delete and rename operations" [doc
   CreateFileW]. Nobody else can rename, delete or replace the source or
   its pinned ancestors between the comparison and the rename. (FR-8 adds
   the caveat that sharing flags cannot stop an attribute writer from
   converting a pinned directory into a junction, `31:434-436`.)

The matrix asks whether any macOS or Linux mechanism provides all three.

## 2. Answers in brief

- **No macOS or Linux rename primitive binds the source to a previously
  checked identity.** Every one is addressed by a name, resolved during
  the call. Holding an open no-follow descriptor on the source and pinning
  the parent directory do not change that: in 15 deterministic cases per
  volume (seven primitives on a file and on a directory, plus an
  atomic-save replacement), on all six macOS volumes probed, every call
  the filesystem accepted moved the replacement object, never the checked
  one [P1]. XNU looks the source up from the `from` string relative to
  `fromfd` (`NDINIT(fromnd, DELETE, OP_UNLINK, …)` then
  `nameiat(fromnd, fromfd)` in `renameat_internal()`), and no path
  accepts a descriptor or vnode identity for the source [src XNU].
- **Slate's own rename is not even descriptor-relative.**
  `FsVaultProvider::rename` joins absolute paths lexically and calls
  `renamex_np(abs, abs, RENAME_EXCL)` on macOS and
  `renameat2(AT_FDCWD, abs, AT_FDCWD, abs, RENAME_NOREPLACE)` on Linux
  (`crates/slate-core/src/vault/fs.rs:771-843`, `:1123-1186`); the pinned
  helper (`fs.rs:247-307`) serves creation and the Trash inventory, not
  rename. Every ancestor is resolved again at the call and symlinks are
  followed, so
  replacing a vault directory with a symlink after the check redirects
  the rename: the probe moved a file from outside the vault into it, and
  a vault file out of it. The same calls made relative to the vault root
  descriptor with `RENAME_NOFOLLOW_ANY` refused both (`ELOOP`), and a
  parent descriptor pinned before the swap moved the validated parent's
  child [P1b]. This hardens the ancestors, not the leaf.
- **Nothing on macOS or Linux provides property 3.** Unix has no share
  modes. Advisory locks, Linux leases (broken only by `open` and
  `truncate`), immutable flags (`CAP_LINUX_IMMUTABLE`, or a `UF_IMMUTABLE`
  flag the file's owner can clear) and fanotify permission events
  (unprivileged callers "cannot request permission events") do not fence
  a rename by another process [man].
- **The one ID-addressed rename on macOS is undocumented and not a
  guarantee.** A `/.vol/<fsid>/<inode>` source path moved the ID's own
  object in all 220,000 iterations of a race on V0 [P9]. But XNU
  translates the ID to the object's current path and then looks that path
  up by name, with no check that the vnode found has the requested ID,
  and restarts only on `ENOENT` [src XNU `vfs_lookup.c`]; the race did not
  show that the window between translation and lookup was ever hit. The
  kernel also chooses which hard link to move, and the object can be
  anywhere on the volume, including outside the vault [P8]. `/.vol` has
  no man page.
- **The best available macOS/Linux design detects, it does not prevent.**
  Check the identity under the structural lock, rename by name inside a
  pinned parent with no-replace, then verify what arrived and move it back
  on a mismatch. That shrinks the window from user time (seconds to
  hours between the forward operation and undo) to the gap between two
  system calls, but a wrong object can still move for an instant, and the
  move back can fail with `EEXIST` [P10.4]. It must be a separately named
  capability with its own contract, never `rename_if_identity`.
- **Identity quality differs by provider** (§5). No APFS file ID was
  reused in these probes, and Apple documents the capability bit APFS
  sets as implying IDs are "not recycled", but HFS+ sets the same bit and
  does recycle after a 32-bit wrap, so the bit is not proof. ext4 and
  FAT32 reuse numbers. On an SMB server without file IDs, macOS derives
  the inode number from the parent's inode number plus a hash of the
  name, so a replacement under the same name has the same "identity". On
  macOS `st_dev` is a device number that can change between mounts; the
  persistent volume key is the file system UUID.
- **Side findings outside #1125, from FAT32 and exFAT drives** (both are
  mounted through FSKit on macOS 27) [P17]: exFAT fails every
  `RENAME_EXCL` to a free name with `ENOTSUP`, so Slate's no-replace
  rename, and its no-replace publish of new files (`fs.rs:320-328`),
  should fail on exFAT vaults (by code reading; Slate itself was not run).
  FAT32 accepted `RENAME_SWAP` and performed it as a replacing rename,
  destroying the target's contents. Slate does not use `RENAME_SWAP`
  today; any design that does must not run on FAT32.

## 3. Rename primitives

"Binds" means the object renamed is guaranteed to be the one whose
identity was checked earlier. "Case-only" is a same-directory rename that
changes only letter case on a case-insensitive volume.

### 3.1 macOS

| Primitive | Source addressed by | Binds a checked identity? | No-replace | Directories | Case-only | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| `rename`, `renameat` | name (path, or name under `fromfd`); ancestors followed | No. Moved the replacement in every case. | No; replaces the target | Yes | Works: XNU hands a same-vnode case change to the filesystem [src XNU]; probed for normalization-only changes (§6) | [P1] [man rename(2)] |
| `renamex_np`/`renameatx_np` + `RENAME_EXCL` | name; ancestors followed | No. Moved the replacement in every case. | `EEXIST` for an existing target on every volume probed. For a free target, exFAT fails `ENOTSUP` [P17], and by source so does smbfs (§5): XNU does not emulate the flag (`vn_rename()`) | Yes | Works on case-insensitive APFS, HFS+ and FAT32; `ENOTSUP` on exFAT; see §6 | [P1] [P2b] [P3] [man] [src XNU `kpi_vfs.c`] |
| + `RENAME_SWAP` | two names | No. Swapped the replacement. | n/a (both must exist, else `ENOENT`) | Yes, file with directory too | n/a | [P1] [P2]; HFS+ and exFAT `ENOTSUP`; **FAT32 returned success and replaced the target** [P2b] |
| + `RENAME_NOFOLLOW_ANY` | name; refuses symlinks anywhere in either path | No (leaf still named) | with `EXCL` | Yes | not probed | [P1] [man]; refused a substituted ancestor with root-relative paths (`ELOOP`) [P1b]; accepted by the macOS 15 flag mask [src XNU] |
| + `RENAME_RESOLVE_BENEATH` | name; refuses escape from `fromfd`/`tofd` | No | with `EXCL` | Yes | not probed | [P1] [man]; accepted by XNU main (xnu-12377.1.9, macOS 26.0) but not by xnu-11417.140.69 (macOS 15), so not usable while Slate supports macOS 15 |
| + `RENAME_SECLUDE` (in `sys/stdio.h`, absent from `rename(2)`) | name | No | not tried | APFS `EISDIR`; HFS+ allowed | — | [P11]: did not refuse files held open by this or another process, or hard-linked files, through this interface |
| `/.vol/<fsid>/<ino>` as the source path | ID, translated in-kernel to the current path, then looked up by name | Not guaranteed (see §2) | with `EXCL` | Yes | — | [P8] [P9]: no wrong move in 220,000 race iterations on V0; [src XNU `vfs_lookup.c` `namei()`, `vfs_getrealpath()`]; undocumented |
| `fsgetpath(fsid, id)` then rename | ID → path in user space, then name | No. Two calls; the window is user-space wide. | as the rename used | Yes | — | [man fsgetpath(2)]: unprivileged on APFS and HFS+, `ENOTSUP` on FAT32 and exFAT [P4] |
| `openbyid_np` | ID | Opens only; cannot rename | — | — | — | [P4] `EPERM` unprivileged; gated on platform binaries or a private entitlement, and itself builds a path then opens it with an ID check [src XNU] |
| `exchangedata` | two paths | Swaps data, not names; "deprecated in macOS 10.13"; APFS does not support it (`VOL_CAP_INT_EXCHANGEDATA` set on HFS+ only) | — | Files only | — | [man exchangedata(2)] [P0] |
| NSURL file reference URL + `NSFileManager` | ID, resolved to a path, then the path | No. `trashItemAtURL:` with a reference URL acted on the other object 3.4-4.0% of the time under a race; `moveItemAtURL:` was not probed. | per call | Yes | — | [P13] (Track A doc §3) |

### 3.2 Linux (from documentation and source, not probed)

| Primitive | Source addressed by | Binds a checked identity? | No-replace | Directories | Evidence |
| --- | --- | --- | --- | --- | --- |
| `rename`, `renameat` | name | No | No ("atomically replaced") | Yes | [man rename(2)] |
| `renameat2` + `RENAME_NOREPLACE` | name | No | Where the filesystem supports it: ext4 3.15; btrfs, tmpfs, cifs 3.17; xfs 4.0; in 4.9 "many other filesystems … including ext2, minix, reiserfs, jfs, vfat, and bpf". Otherwise `EINVAL`. | Yes | [man rename(2)]; glibc wrapper 2.28 |
| `renameat2` + `RENAME_EXCHANGE` | two names | No | n/a | Yes, different types allowed | [man rename(2)]: the page lists no per-filesystem table |
| `renameat2` + `RENAME_WHITEOUT` | name | No; needs `CAP_MKNOD`; for overlay filesystems | — | — | [man rename(2)] |
| Any `renameat2` flag on an NFS mount | — | — | The Linux NFS client returns `EINVAL` for every flag (`nfs_rename()`: `if (flags) return -EINVAL;`), so Slate's Linux no-replace rename (`fs.rs:1140-1186`) fails closed on NFS | — | [src `fs/nfs/dir.c`] |
| `linkat(AT_FDCWD, "/proc/self/fd/<fd>", dirfd, name, AT_SYMLINK_FOLLOW)` | the open descriptor | Creates a new name for the descriptor's object; does not remove the old name | `EEXIST` if taken | **No**: "any type of file except a directory" | [man link(2)]; `AT_EMPTY_PATH` itself needs `CAP_DAC_READ_SEARCH` |
| `open_by_handle_at` | file handle | Opens only; needs `CAP_DAC_READ_SEARCH` | — | — | [man open_by_handle_at(2)] |

### 3.3 Windows (reference)

| Primitive | Source addressed by | Binds a checked identity? | No-replace | Directories | Evidence |
| --- | --- | --- | --- | --- | --- |
| `NtSetInformationFile(FileRenameInformation)` on a handle opened without `FILE_SHARE_DELETE` | the handle | Yes, with other renamers and deleters excluded while the handle is open | `ReplaceIfExists` false | Yes | [src `windows_entry.rs:177-261`] [doc FILE_RENAME_INFO, CreateFileW] |

**Windows, probed 2026-10-31 (plan 44 WB-27).** Each fact in
`crates/slate-core/src/vault/weak_volume_tests.rs` runs only on a
scratch directory named by `SLATE_WEAK_VOLUME_FAT32`, `…_EXFAT` or
`…_SMB`, after a gate confirms that the volume is that kind
(`check_weak_volume`). The gate is proven against a mislabelled NTFS
folder, and against a table of the reports it judges, on every machine
[P-W1].

- **Rename and move rows.** The occupant is created AFTER the provider's
  own destination pre-check, through the test seam
  `rename_with_hook_for_tests`. So it is the kernel's no-replace that
  refuses, not the pre-check.
- **Identity-rename row.** The identity rename has no pre-check, so its
  occupant always meets the kernel.

| Volume | Create (staged publish) onto an occupied name | Rename and move onto a name occupied after the pre-check | Identity rename onto an occupied name | Case-only rename `Note.md` → `note.md` | Evidence |
| --- | --- | --- | --- | --- | --- |
| NTFS (the temp directory) | Refused | Refused: the primitive with an occupant made before its call, and one made concurrently | Refused | One entry; identity kept | Not through the WB-27 seam. The NTFS evidence is:<br>• create: `create_exclusive_collision_preserves_winner_bytes_and_cleans_staged_temp`;<br>• rename: `rename_no_replace_atomic_refuses_occupied_destination` and `rename_no_replace_destination_creation_race_never_clobbers`, which call the primitive directly (`fs.rs`);<br>• identity and case-only: `conditional_rename_rejects_replacements_and_collisions` and `conditional_rename_handles_case_only_inverse_and_directory_moves` (`windows_entry_tests.rs`) |
| SMB 3.1 over `\\localhost\C$` (loopback to an NTFS volume; the gate reads the protocol) | Refused by the kernel publish: `DestinationExists`, occupant unchanged, stage cleaned | Refused by the kernel: `DestinationExists`; source and occupant unchanged | Refused by the kernel: `Io(AlreadyExists)`; both entries unchanged | One entry, new spelling, `FILE_ID_INFO` unchanged. The identity inverse, which makes only the no-replace call, performs the same case-only rename | [P-W1] |
| FAT32 (1 GB VHD, probed 2026-11-01) | Refused by the kernel publish: `DestinationExists`, occupant unchanged | Refused by the kernel; source and occupant unchanged | Not reached: no `FILE_ID_INFO` (error 87), so the identity rename refuses before the kernel call; both entries unchanged | **The volume reports success and keeps the old spelling** (#1727), on Windows 11 10.0.26300 [P-W2] and on Windows Server 10.0.20348.5761 [P-W3]. `MoveFileEx` does the same, in every case shape tried. From plan 47 CB-13 the provider reads the stored name back and refuses the rename in core's sentence (§6) | [P-W2] [P-W3] |
| exFAT (1 GB VHD, probed 2026-11-01) | As FAT32 | As FAT32 | As FAT32 | As FAT32 (#1727), on both machines | [P-W2] [P-W3] |

No replacing rename was observed, so no refusal branch shipped. The one
refusal that did ship is not for a replacement: it is the case-only
rename a volume reports as done and does not make (§6, plan 47 CB-13).

## 4. What an open-descriptor identity check can and cannot guarantee

A provider can open the source with `O_NOFOLLOW`, or descend from the
vault root one `openat` at a time with `O_NOFOLLOW` (the pinned descent in
`fs.rs:247-307` and the inventory walk in `trash_snapshot.rs:119-245`),
and read an identity from the descriptor. What each source of identity
offers:

| Check | Can guarantee | Cannot guarantee | Evidence |
| --- | --- | --- | --- |
| `fstat` `st_dev` + `st_ino` | The descriptor's object is the expected one at the moment of the check, and stays that object for the descriptor's life (the descriptor follows renames). | That the *name* still refers to it a moment later; the rename then acts on the name. On smbfs without server file IDs the number is derived from name and parent; on ext4 and FAT32 a freed number is taken by a later file. | [P1] [P6] [P17] [src SMBClient `smbfs_getino()`] [doc ext4 inodes] |
| `st_dev` across mounts | Distinguishes volumes while mounted | Persistence: it is the block device's number, and "disk device names may change over time when disks are added, disks are removed or when the system is rebooted" [man diskutil(8)]. One detach and re-attach of a scratch image kept the same value [P18]. The persistent key is `ATTR_VOL_UUID`, "the file system UUID" [man getattrlist(2)]. | [P18] |
| `fcntl(F_GETPATH)` | One current path of the descriptor's object, after renames of the object or its directory. | Stability: the path is stale as soon as it is returned. For a hard-linked file it returned one of the names. After the file was replaced by a rename-over (link count 0) it returned the old path, which at that moment named the *replacement*: a stale result can name a different object, not only a missing one. After an explicit unlink it returned `ENOENT` on APFS and HFS+, and the old path on FAT32 and exFAT. | [P1] [P4] [P17] [man fcntl(2)] |
| `fgetattrlist` `ATTR_CMN_FILEID` | Equals `st_ino` [man]; same limits. | — | [P5] |
| `ATTR_CMN_OBJID` / `ATTR_CMNEXT_LINKID` | The man page says a link ID is distinct per hard link on HFS+ and APFS. | This probe never saw two distinct values: both descriptors of a hard-linked pair returned the same link ID, so the link a descriptor came from was not recoverable. `OBJID` is deprecated since macOS 10.13 and its generation field read 0. | [P5] [man getattrlist(2)] |
| `ATTR_CMN_GEN_COUNT` | Detects data modification; caught every in-place edit, including one whose mtime was restored and one on HFS+ inside the same second. | Identity. It reads 0 (invalid) while any mapping of the file exists, including across a write through the mapping. FAT32 and exFAT do not report `VOL_CAP_FMT_WRITE_GENERATION_COUNT`. | [P7] [P17] [man getattrlist(2)] |
| `ATTR_CMN_DOCUMENT_ID` (after `chflags UF_TRACKED`) | A document identity that survived an atomic save (new inode, same document ID) on APFS and HFS+. | It is "sticky to the path", so it would treat a replacement as the same document; setting it modifies the user's file flags. | [P7] [man getattrlist(2)] |
| `fsgetpath(fsid, id)` | A current path for an ID, unprivileged on APFS and HFS+. | Not a binding: the path is looked up again by name by whatever call follows. `ENOENT` for an unlinked-but-open object; `ENOTSUP` on FAT32 and exFAT. | [P4] [P17] [man fsgetpath(2)] |
| Linux `name_to_handle_at` | An opaque, filesystem-specific handle, unprivileged. The man page's example deletes and quickly recreates a file that gets "(by chance) the same inode", and the old handle then fails as stale. | That example detects staleness by *opening* the handle under `sudo` (`open_by_handle_at` needs `CAP_DAC_READ_SEARCH`). Detecting reuse without privilege would mean comparing handle bytes, which the page does not promise (handles are opaque): an untested inference. Some network filesystems do not support handles (`EOPNOTSUPP`); `statx` exposes no generation field. | [man open_by_handle_at(2), statx(2)], not probed |

## 5. Filesystems and providers

| Provider | Probed? | `EXCL` / `NOREPLACE` | `SWAP` / `EXCHANGE` | Is `(dev, ino)` an object identity? | ID reuse | Change detection without content hashing |
| --- | --- | --- | --- | --- | --- | --- |
| APFS, case-insensitive (V0 boot Data volume, scratch directories only; V1 scratch image) | Yes | Yes | Yes | Yes, while mounted (`st_dev` is per mount, see §4) | None observed: no duplicate among 10,000 creates, and 5,000 create-and-delete cycles got strictly increasing numbers [P6]. `VOL_CAP_FMT_PATH_FROM_ID` is set, which the man page says "implies that object IDs on this file system are persistent and not recycled", but HFS+ sets the same bit (next row), so the bit is not proof | ns ctime and mtime, and `ATTR_CMN_GEN_COUNT` (`VOL_CAP_FMT_WRITE_GENERATION_COUNT` set) |
| APFS, case-sensitive (V2 scratch image) | Yes | Yes | Yes | As above | As above | As above |
| HFS+ journaled, case-insensitive (V3 scratch image) | Yes | Yes | **No** (`ENOTSUP`; `VOL_CAP_INT_RENAME_SWAP` not reported) | Yes (32-bit catalog node IDs) | Sequential in the probe; after the 32-bit counter wraps, "smaller catalog node IDs [are] reused" (`kHFSCatalogNodeIDsReusedBit`) [doc TN1150], although HFS+ also reports `VOL_CAP_FMT_PATH_FROM_ID` [P0] | **1-second timestamps**: a same-size in-place edit within the second left size, mtime and ctime unchanged [P7]; `ATTR_CMN_GEN_COUNT` caught it |
| FAT32 (V4 scratch image; FSKit `msdos`) | Yes | `EEXIST` for an existing target; succeeds for a free one, although the volume reports no `VOL_CAP_INT_RENAME_EXCL` | **Unsafe**: reported unsupported, returned success, and replaced the target [P2b] | Stable across rename and move in this probe | **Reused at once**: a new file took the number of the file just deleted | No generation count; no persistent IDs; no hard links |
| exFAT (V5 scratch image; FSKit) | Yes | `EEXIST` for an existing target; **`ENOTSUP` for a free one** | `ENOTSUP` | Stable across rename and move in this probe | Not observed in one short probe | No generation count; no persistent IDs |
| HFS+ case-sensitive | Not probed | — | — | — | — | — |
| SMB (macOS smbfs) | Not probed; from Apple's SMBClient source | **No, by source**: smbfs advertises neither `VOL_CAP_INT_RENAME_EXCL` nor `…_SWAP`, its vnode table has `vnop_rename` and no `vnop_renamex`, and XNU's `vn_rename()` returns a filesystem's `ENOTSUP` for `RENAME_EXCL` unchanged. exFAT shows exactly that pattern (`EEXIST` if the target exists, `ENOTSUP` if not), so Slate's no-replace rename probably fails closed on SMB vaults; one probe on a real share would settle it | No | Only with server file IDs (SMB 2/3 with FileId support). Otherwise `ino = parent ino + hash(name)`: a different file under the same name has the same number. smbfs reports `VOL_CAP_FMT_PERSISTENTOBJECTIDS` only for an Apple server with resolve-ID and file-ID support, so a gate on that bit refuses every other server, even one with file IDs | Server-dependent | Server timestamps; no `WRITE_GENERATION_COUNT` in smbfs's capability list |
| iCloud Drive and File Provider folders (`~/Library/Mobile Documents`, `~/Library/CloudStorage`), and other sync clients' plain folders | Not probed (never written to) | As the local APFS volume they live on | As APFS | As APFS, with a permanent external writer. Assumed, not checked: that third-party clients address files by path. Apple's own daemons are platform binaries, which may open by ID (§3.1 `openbyid_np`), and document IDs exist to follow items across moves; whether a remote edit is applied in place or by replacing the inode is not documented and varies by client | As APFS | As APFS. File Provider items can be dataless: `SF_DATALESS` is "a dataless placeholder", and `rename` fails `EDEADLK` when a dataless source must be materialized and the I/O policy forbids it [man chflags(2), rename(2)] |
| ext4 | Not probed | Yes (3.15) | Exchange support is per filesystem; the man page lists none | Yes | **Reused**: inode numbers index fixed per-group tables ("Each block group contains sb->s_inodes_per_group inodes"), so a freed number goes to a later file; the on-disk `i_generation` ("File version (for NFS)") is what tells them apart [doc ext4 inodes] | ns timestamps; `statx` exposes no generation field [man statx(2)] |
| btrfs | Not probed | Yes (3.17) | Per filesystem | The inode number alone is per subvolume: each subvolume has "its own independent … inode number namespace", "Inode number is not a filesystem-wide unique identifier", and snapshots "preserve their original inode numbers" [doc btrfs Subvolumes]. That page does not say whether `st_dev` differs per subvolume; `statx` reports the subvolume (`stx_subvol`) since Linux 6.10 [man statx(2)] | Not documented on that page | ns timestamps |
| NFS (Linux client) | Not probed | **No**: every `renameat2` flag is `EINVAL` | No | The server's file ID; handles may be unsupported | Server-dependent | Attribute caching; and "you can not assume that if the operation failed, the file was not renamed" [man rename(2) BUGS] |

**Windows (plan 44 WB-27, 2026-10-31).** These rows record what the
Windows provider meets. Here no-replace is `FileRenameInformation` with
`ReplaceIfExists` false, and the identity is `FILE_ID_INFO`: the volume
serial number plus the 128-bit file id.

| Volume | Probed? | No-replace | Case-only rename | `FILE_ID_INFO` persists across rename | ID reuse |
| --- | --- | --- | --- | --- | --- |
| NTFS | Yes (existing facts) | Yes | Yes, identity kept | Yes | Not observed: a new file at an old name gets a different id |
| SMB over `\\localhost\C$` (Windows 11 client and server, NTFS underneath) | Yes [P-W1] | Yes, at the kernel | Yes, identity kept | Yes: the id survives a rename, and a stale identity is refused | Not observed: a new file at the old name got a different id |
| FAT32 | Yes [P-W2] [P-W3] | Yes, at the kernel | **No**: the volume reports success and the spelling is unchanged, for a file and for a folder (#1727). The provider refuses it (§6) | No `FILE_ID_INFO` (error 87). The fallback file index (`GetFileInformationByHandle`) is stable within a mount and distinct per entry, and **changes when the entry is renamed** | Not probed |
| exFAT | Yes [P-W2] [P-W3] | Yes, at the kernel | **No**, as FAT32 (#1727) | As FAT32 | Not probed |

## 6. Case-only and normalization-only renames

Before renaming, Slate's provider checks whether the two spellings are
one entry (`same_entry`), then tries `renamex_np(RENAME_EXCL)`, and falls
back to a plain `rename` only when that fails with `EEXIST` for a proven
alias (`fs.rs:801-841`; residual IR-5 in
`docs/plans/32_canonical_file_identity_contracts.md:262-270`). XNU's
`RENAME_EXCL` block returns `EEXIST` for an existing target unless source
and target are the same vnode in the same directory on a volume that
reports `_PC_CASE_SENSITIVE` = 0; its comment adds that the name must
"only differ in case (determined by underlying filesystem)", a condition
the code leaves to the filesystem. The exception is in
`renameat_internal()` on XNU main and in xnu-11215.1.10 (macOS 15.0)
[src XNU].

| Rename | Case-insensitive APFS | Case-sensitive APFS | HFS+ (case-insensitive) |
| --- | --- | --- | --- |
| `Note.md` → `note.md` (file) with `RENAME_EXCL` | ok, same inode | ok (two distinct names; the target was absent) | ok, same inode |
| `Folder` → `folder` (directory) with `RENAME_EXCL` | ok, same inode | ok | ok |
| `café` NFC → NFD with `RENAME_EXCL` | ok, stored NFD | **`EEXIST`** (the volume aliases normalization but reports case-sensitive) | **`EEXIST`**: the filesystem refused a difference that is not one of case |
| the same with plain `rename` (the IR-5 fallback) | ok, stored NFD | ok, but **a no-op**: the stored name stayed NFC | ok, stored NFD |
| case-only rename after an external writer replaced `Note.md` | renamed the **replacement** | renamed the **replacement** | renamed the **replacement** |

So on macOS 27 the IR-5 fallback is not reached for case-only renames on
case-insensitive APFS or HFS+; it is reached for normalization-only
renames on HFS+ and case-sensitive APFS; and a normalization-only rename
cannot be expressed on case-sensitive APFS without a temporary name. On
FAT32, `RENAME_EXCL` case-only and normalization-only renames succeeded;
on exFAT they failed `ENOTSUP`, which the provider does not treat as a
fallback case, so they would fail outright [P3] [P17]. None of these paths
binds identity [P3].

**Windows (plan 44 WB-27).** The provider first asks whether the
destination name is the source under another spelling (`same_entry`),
then renames with `ReplaceIfExists` false. Only when the volume reports
the source's own alias as occupied does it retry that one rename with
replacement (the `destination_is_source` arm of `rename_beneath_root`).

On SMB over `\\localhost\C$`, the no-replace call alone performs the
case-only rename. That holds in `weak_volume_case_only_rename_keeps_the_entry`
[P-W1]:
- the provider's rename leaves one entry, with the new spelling and the
  same `FILE_ID_INFO`;
- the identity inverse (`rename_if_identity`) renames another file's case
  alias the same way. It makes ONLY the no-replace call and has no retry
  arm.

The same holds on NTFS
(`conditional_rename_handles_case_only_inverse_and_directory_moves`).
So on both, the provider's first call does the work, and the retry has
no case to answer.

On FAT32 and exFAT (2026-11-01) neither call performs it: the
no-replace call returns success and the entry keeps its old spelling,
so the retry arm is never reached either [P-W2]. `MoveFileEx`
(PowerShell's `Rename-Item`) leaves the spelling unchanged too, for a
long name to a lowercase 8.3 name, an 8.3 name to another case, an 8.3
name to a long name, and a long name to a long name. A retry 16 s later
(past the file-name tunnel cache) does not change it either. The
provider reports the rename as done, so the index and the disk
disagree: #1727.

**From plan 47 CB-13 (2026-11-16, owner decision CD7) the provider no
longer reports that rename as done.** When the destination is the source
under another spelling, `rename_beneath_root` reads the entry's stored
name back after the kernel call (`windows_entry::stored_name`: an
attribute open of the name in the pinned folder, then the last component
of the handle's final path). Where the stored name is not the requested
one, the rename fails with core's sentence
(`a11y::case_only_rename_refused`): "This drive keeps the capitalization
a file was created with, so Note.md was not renamed to note.md." The
index row, the links and the report do not move, and both spellings are
recorded as the provider's own echo. The rule names no file system: it
refuses wherever the volume kept the old spelling, and NTFS and SMB over
NTFS, which apply the rename, read back the requested name and are
unchanged. A folder gets the same check and the same sentence with its
own names. The rename is not made in two steps through a temporary name,
because FAT32's file index changes on a rename (§5).

What each machine showed, on FAT32 and on exFAT alike:

| Case-only rename | Windows 11 10.0.26300, the owner's desktop, 2026-11-01 [P-W2] | Windows Server 10.0.20348.5761, the CI runner, 2026-11-16 [P-W3] | With the read-back in [P-W3] |
| --- | --- | --- | --- |
| `Note.md` → `note.md` | success reported, old spelling kept | success reported, old spelling kept | refused in core's sentence; the listing unchanged |
| `UPPER.MD` → `upper.md` (the owner's run: `NOTE.MD` → `note.md`) | as above | as above | as above |
| `lower.md` → `Lower.md` (the owner's run: `note.md` → `Note.md`) | as above | as above | as above |
| `Long Note.md` → `long note.md` | as above | as above | as above |
| `MIXED.MD` → `MIXED.md` (the owner's run: `NOTE.MD` → `NOTE.md`) | as above | as above | as above |
| the folder `Docs` → `docs`, holding one file | not tried | success reported, old spelling kept | refused, with the folder's names: "…so Docs was not renamed to docs." |

The read-back was measured where production makes it. With the source
handle, opened under the old spelling, still open after the kernel call,
the final-path read gave the folder's own listing in all twelve cases on
the runner's two volumes, and so did a read of the listing itself. The
final-path read was made again after the handle closed and gave the same;
the listing read was not repeated [P-W3]. So the final-path read stays
in production. The listing read is compiled for tests only
(`windows_entry::stored_name_by_listing`), and the gated fact prints
both beside its own listing on every run. Not measured anywhere: either
read after a case-only rename that a FAT volume applied, because neither
volume on either machine applied one.

The vault watcher heard nothing for an ignored rename: on both volumes
no event named `Watch.md` or `watch.md` between a file made before the
rename and one made after it, whose events both arrived [P-W3]. A
refusal still records both spellings as the provider's own echo, so a
volume that did raise events for a rename it did not keep would start no
catch-up scan.

One residual is left, by reading; none of it was run. The read-back
fails, or answers a spelling other than the old or the requested one:
an outside rename or removal between the kernel call and the read-back,
or a share whose server applies the rename in its own case.

- Where the read fails, the rename returns the read's own error. The
  rename is never reported as done on a spelling core could not read,
  but the volume may hold the new spelling while the index holds the old
  one, and the host says the rename failed. No echo is recorded, so by
  reading the watcher's burst for those paths calls back and asks the
  host to rescan.
- Where the read answers a third spelling, the rename is refused in
  core's sentence with that spelling: for `Note.md` to `note.md` on a
  server that stores `NOTE.md`, the reader hears "…so NOTE.md was not
  renamed to note.md." while the entry was renamed. Before this change
  the same volume gave a reported success with the index on a name the
  volume did not hold.

One outcome is possible by reading and was seen on no volume: a volume
that applies a folder's case-only rename and ignores a file's. The
compound rename of a folder with its note then renames the folder, is
refused at the note, and rolls the folder back. Everything holds the old
spelling and the error reads "renaming the folder note failed and the
folder rename was rolled back: invalid argument: This drive keeps the
capitalization a file was created with, so Docs.md was not renamed to
docs.md."

## 7. Directories

Directories behave like files for every primitive above: the rename acts
on whatever directory carries the name at the call [P1]. Two
directory-specific points:

- A directory's identity says nothing about its contents. Descendants can
  be added, removed or replaced without changing the directory's
  `st_ino`; holding a directory descriptor lets a writer create children
  even after the directory has been moved [P10.2]. A conditional inverse
  of a folder rename binds the folder, not the tree (as on Windows today).
- APFS refuses `RENAME_SECLUDE` on directories (`EISDIR`) [P11]. HFS+
  reports `VOL_CAP_FMT_DIR_HARDLINKS` [P0], but a user process cannot
  create one: `link(2)` fails with `EPERM` when "the file named by path1
  is a directory" [man link(2)].

## 8. What this means for Track B

| Mechanism | Same guarantee as Windows? | What it does guarantee | Cost and limits | Verdict |
| --- | --- | --- | --- | --- |
| M1. Absolute-path no-replace rename (today's `rename`, used by undo) | No | The destination is not clobbered on volumes that honour `RENAME_EXCL` | Ancestors re-resolved and followed: a substituted ancestor moves objects into or out of the vault [P1b]; fails `ENOTSUP` on exFAT [P17] and, by source, smbfs | Shipping behaviour; mac's single undo preflight checks only existence (`AppState.swift:18144-18166`) |
| M2. Ancestor hardening: rename relative to the pinned vault root with `RENAME_NOFOLLOW_ANY`, or from pinned parent descriptors (Linux: pin with `openat2` `RESOLVE_NO_SYMLINKS`, not probed) | No | No symlinked or substituted ancestor redirects the rename [P1b] | Small; the leaf is still named. A symlinked vault *root* keeps working only with the root-descriptor form | Worth doing regardless of the policy decision |
| M3. M2 plus an identity check under the structural lock just before the rename | No | The object at the name matched the recorded identity one system call before the rename | One `fstatat` (plus a handle on Linux), and identities recorded at the forward operation | Viable as a narrower, honestly named capability; forbidden as `rename_if_identity` (`provider.rs:128-129`) |
| M4. M3, then verify the arrived object, then move it back on a mismatch | No | A wrong object is never left at the destination silently | A second check and a compensation path; `EEXIST` if the source name was retaken [P10.4]; NFS may report failure after renaming [man] | Viable; the residual is a transient wrong move visible to sync clients |
| M5. Swap capture: `RENAME_SWAP` the source with a placeholder, verify, swap back | No | As M4 | APFS only (HFS+ and exFAT `ENOTSUP`; FAT32 destroys the target [P2b]); observers see the placeholder under the name [P10.3] | Rejected |
| M6. `/.vol` ID-addressed rename | Not by contract | — | Undocumented; kernel picks the hard link; may act outside the vault; mac only; no wrong move seen in 220,000 race iterations [P9] | Rejected as a supported mechanism |
| M7. Linux `linkat` through `/proc/self/fd`, then exchange-verify-unlink of the old name | No | Files only | Intermediate states with two links; directories impossible | Rejected |
| M8. Locks, leases, immutable flags, fanotify | — | Nothing against an uncooperative renamer | — | Not applicable [man] |

The decision memo turns this into options and a recommendation.

## Appendix A. Probes

Environment: Mac Studio, arm64, macOS 27.0.1 (26A434), Darwin 27.0.0
(xnu-13432.1.9), Apple clang 21.0.0. The running kernel's source is not
yet published; XNU citations are to apple-oss-distributions/xnu `main`
(xnu-12377.1.9, macOS 26.0) and to the macOS 15 tags named in the text
(xnu-11215.1.10 is macOS 15.0; xnu-11417.140.69 is a later macOS 15
release).

Volumes: **V0** the boot Data volume (APFS, case-insensitive), used only
inside directories created under the session scratchpad. **V1** APFS
case-insensitive, **V2** APFS case-sensitive, **V3** journaled HFS+
case-insensitive, **V4** FAT32, **V5** exFAT: 40 MB disk images created in
the scratchpad, attached with `hdiutil attach -nobrowse -mountpoint
<scratchpad>/…`, detached and deleted afterwards (V1 was re-created once
for the follow-up Trash probes). V4 and V5 mounted through FSKit (`mount`
lists `fskit`). No probe touched the owner's Trash, documents, iCloud
Drive or any synced folder; every Trash probe ran on V1 or V3, whose
Trash is `<volume>/.Trashes/501`.

The programs (C and Objective-C plus a small Python decoder, about 1,000
lines, kept in the scratchpad and not committed) simulate the external
writer in-process at a fixed point between the check and the call, so the
deterministic probes do not depend on winning a race. The race probes use
a second thread.

| Probe | What it does | Result |
| --- | --- | --- |
| P0 | `getattrlist(ATTR_VOL_CAPABILITIES)`, `pathconf` per volume | APFS (V0-V2): `RENAME_SWAP`, `RENAME_EXCL`, `PERSISTENTOBJECTIDS`, `PATH_FROM_ID`, `64BIT_OBJECT_IDS`, `DOCUMENT_ID`, `WRITE_GENERATION_COUNT` yes; `RENAME_OPENFAIL`, `EXCHANGEDATA` no. HFS+ (V3): `RENAME_EXCL`, `EXCHANGEDATA`, `PATH_FROM_ID`, `DIR_HARDLINKS`, `DOCUMENT_ID`, `WRITE_GENERATION_COUNT` yes; `RENAME_SWAP` not reported. FAT32 and exFAT (V4, V5): `RENAME_EXCL`, `RENAME_SWAP`, `PERSISTENTOBJECTIDS`, `PATH_FROM_ID`, `WRITE_GENERATION_COUNT` all no. |
| P1 | Open the source no-follow, record `st_ino`, check it; the writer moves the source away and creates a new object under the name (or replaces it by an atomic rename-over); call each primitive | Every accepted call renamed the replacement: 15 of 15 on V0, V1, V2 and V4; 13 of 13 on V3, where the two `RENAME_SWAP` cases failed `ENOTSUP`; 4 of 4 on V5, where the eleven `RENAME_EXCL` and `RENAME_SWAP` cases failed `ENOTSUP`. The held descriptor kept the original object; `F_GETPATH` followed it to its new name, or after the rename-over returned the old path with link count 0. |
| P1b | Replace the vault's `sub` directory with a symlink to `../outside`, then rename as Slate does today and in hardened forms (V0) | `renamex_np(abs vault/sub/a.md → abs vault/b.md, EXCL)` moved the **outside** file into the vault; `renamex_np(abs vault/c.md → abs vault/sub/c.md, EXCL)` moved the vault file **out**. Root-relative `renameatx_np(…, RENAME_EXCL \| RENAME_NOFOLLOW_ANY)` refused both (`ELOOP`). With `sub` pinned before the swap, `renameatx_np(subfd, "a.md", …)` moved the vault's own file. |
| P2 | `RENAME_SWAP` semantics (V0-V3) | file↔file and file↔directory swap; missing target `ENOENT`; `SWAP\|EXCL` `EINVAL`; a name with itself ok. HFS+ `ENOTSUP`. |
| P2b | `RENAME_EXCL` onto an existing file and a non-empty directory; `RENAME_SWAP` of two files (V1, V4, V5) | `EXCL`: `EEXIST`, both intact, everywhere. `SWAP`: a true swap on APFS; `ENOTSUP` on exFAT; on **FAT32 success, with the target's contents gone** (`c` absent, `d` holds `c`'s data). |
| P3 | Case-only and normalization-only renames, and a raced case-only rename | §6 table and text. |
| P4 | `F_GETPATH`, `fsgetpath`, `openbyid_np` | §4 table; `openbyid_np` `EPERM`. |
| P5 | `fgetattrlist` identity attributes on a file, its hard link and a directory | `FILEID` = `st_ino`; both descriptors of a hard-linked pair returned the same `OBJID` and `LINKID`; `OBJID` generation 0; `st_gen` 0 (root-only). |
| P6 | 5,000 create/unlink cycles plus 5,000 creates in one directory (V0-V3); rename, move and recreate one file (V0, V4, V5) | No duplicate inode number among the 10,000 on any of V0-V3; the 5,000 single cycles were strictly increasing. A file kept its number across rename and move everywhere; on FAT32 a new file took the number of the file deleted just before it. |
| P7 | In-place write; same-size write with mtime restored; atomic save; a write through `mmap`; a rename into another directory | Inode unchanged in place, changed by the atomic save. `GEN_COUNT` changed on every write. mtime and ctime changed on APFS; on HFS+ neither changed for writes 20 ms apart. Document ID survived the atomic save on APFS and HFS+. While the file was mapped, `GEN_COUNT` read 0 before and after a write through the mapping; after `munmap` it had advanced (2 → 4), on APFS and HFS+. A rename into another directory changed the moved file's and directory's ctime, not their mtime (V0). |
| P8 | Rename and link through `/.vol/<fsid>/<ino>` after the object moved and another took its name; a directory; a hard-linked file (V0-V3) | Moved the ID's object (file and directory); `link` worked; for a pair `h1`/`h2` the kernel moved `h2`, the name created and opened last. |
| P9 | Race on V0: a thread keeps swapping two names (or two directories 300 levels deep) holding objects I and Y while the main thread renames I by `/.vol` path | 100,000 + 100,000 + 20,000 iterations against 136,943 + 198,767 + 39,046 concurrent swaps: 0 wrong moves. A by-path baseline in the deep harness moved the wrong object 9,397 times in 20,000. The baseline shows that the racer interleaves with the main thread's renames, not that it ever landed inside the window between ID translation and lookup. |
| P10 | Staging (V0-V3): (P10.1) move a tree into a private directory; (P10.2) write through a pre-existing handle and create a child through a held directory descriptor; (P10.3) swap capture after the name was given to another object; (P10.4) exclusive capture, then the name is retaken, then move back | P10.1 moved the confirmed root. P10.2: the late write and the late child both landed in the staged tree. P10.3 captured the replacement and exposed the placeholder under the name (HFS+ `ENOTSUP`). P10.4: the move back failed `EEXIST`, leaving the captured object stranded. |
| P11 | `RENAME_SECLUDE` on a closed, an open (same and other process) and a hard-linked file, and a directory; `unlinkat(AT_NODELETEBUSY)` (V0, V3) | `SECLUDE` renamed all files; directory `EISDIR` on APFS, allowed on HFS+. `AT_NODELETEBUSY` `EBUSY` on an open file. |
| P12-P16 | Trash probes, including Finder | Reported in `24_trash_handoff_feasibility.md` §3. |
| P17 | P0, P1, P2b, P3, P4 and P6 on FAT32 and exFAT (V4, V5) | Reported in the rows above and in `§5`. |
| P18 | `stat -f %d` on a file, detach and re-attach the same image, `stat` again (V1) | `st_dev` 16777243 before and after; `diskutil` reports the volume UUID. |

**P-W1 (plan 44 WB-27, 2026-10-31).** Environment: Windows 11 Pro
10.0.26300, Slate at the WB-27 branch. Gated facts:

```
$env:SLATE_WEAK_VOLUME_SMB = '\\localhost\C$\Users\<user>\AppData\Local\Temp\wb27-smb'
cargo test -p slate-core --locked --lib weak_volume -- --ignored --test-threads=1 --nocapture
```

- **The share.** It is the default administrative share. It reaches an
  NTFS volume through the SMB client and server on one machine, and
  needs no elevated setup.
- **What runs.** Six gated facts passed there, printing their
  `OBSERVED` lines. The three non-ignored gate facts pass on every
  machine.
- **The mutations: 15 applied, each seen failing.** The fact that
  failed ran on this share, on the mislabelled folder, or in the decision
  table. Product sites:
  - `ReplaceIfExists` true in the staged publish;
  - `ReplaceIfExists` true in the ordinary rename's kernel call;
  - `ReplaceIfExists` true in the identity inverse's kernel call;
  - a case-only rename that recreates the file;
  - the identity inverse refusing a case-only alias;
  - the identity compare skipped.

  Gate sites:
  - any file-system name accepted;
  - either FAT kind accepted by "FAT";
  - the two FAT names swapped;
  - a remote volume taken for local FAT;
  - the SMB check without its UNC half, without its remote half, and
    without both;
  - any redirector taken for SMB;
  - a panic on an unset variable.
- **Not run here.** FAT32 and exFAT, which need the elevated VHD
  procedure: P-W2.

**P-W2 (plan 44 WB-27 and WB-28 step 0, 2026-11-01).** Environment:
Windows 11 Pro 10.0.26300, the owner's elevated PowerShell 7, `main`
at 500d4382 or later. Two 1 GB dynamic VHDs, made and formatted by WB-27's
volume procedure (`New-VHD | Mount-VHD | Initialize-Disk |
New-Partition | Format-Volume -FileSystem FAT32` and `exFAT`), with
`SLATE_WEAK_VOLUME_FAT32` and `…_EXFAT` set to a folder on each.

- **The six gated facts.** Five pass on both volumes:
  - create, rename and move each land on a free name;
  - an occupied name is refused at the kernel;
  - the gate names the file system it ran on;
  - with no `FILE_ID_INFO` (error 87, `ERROR_INVALID_PARAMETER`), the
    identity rename and the stale-identity rename refuse.

  `weak_volume_case_only_rename_keeps_the_entry` fails on both: the
  provider's rename returns `Ok`, and the folder still lists `Note.md`.
  The fact stops at the first volume that fails, so exFAT's result
  comes from a second run with only `…_EXFAT` set.
- **Case-only renames through `MoveFileEx`.** On both volumes,
  `Rename-Item` left the spelling unchanged for every case tried:
  `Note.md` → `note.md`, `NOTE.MD` → `note.md`, `note.md` → `Note.md`,
  `Long Note.md` → `long note.md` and `NOTE.MD` → `NOTE.md`. The first
  two were renamed again 16 s later, with no change.
- **WB-28's step 0: the identity reads on a folder.** Opened with
  `FILE_FLAG_BACKUP_SEMANTICS`, with an NTFS temp folder as the
  control. No stop condition.

| Volume | File-system name | `FileIdInfo` | File index (serial-index) | Index read again | Sibling's index | `FileIdInfo` after a rename | Index after a rename |
| --- | --- | --- | --- | --- | --- | --- | --- |
| NTFS (control) | `NTFS` | answers | `885d7da1-007e00000025022f` | same | `885d7da1-020700000025025e` | unchanged | unchanged |
| FAT32 | `FAT32` | error 87 | `0cda959e-00000000004042c0` | same | `0cda959e-00000000004042e0` | error 87 | `0cda959e-0000000000404360` |
| exFAT | `exFAT` | error 87 | `c0dce383-0000000000060280` | same | `c0dce383-00000000000602e0` | error 87 | `c0dce383-0000000000060340` |

**P-W3 (plan 47 CB-13, 2026-11-16).** Environment: the Windows lane's
`rust-weak-volumes` job on a Namespace runner, Windows Server
(`cmd /c ver`: `Microsoft Windows [Version 10.0.20348.5761]`), which
makes a FAT32 and an exFAT VHD with `diskpart` and sets
`SLATE_WEAK_VOLUME_FAT32` and `…_EXFAT` to a folder on each. Three
`workflow_dispatch` runs on scratch branches, with no pull request open.

- **Run 43935566159 (job 133539197985, commit 85649b43): `main`'s
  product code with the rewritten provider fact and an earlier session
  fact.** The provider fact's file is byte for byte CB-13's tests-only
  version, before its by-hand and watcher additions. The session fact was an
  earlier version: it renamed the folder with `rename_folder`, and its
  folder had no note.
  `weak_volume_case_only_rename_keeps_the_entry` makes six case-only
  renames per volume through the provider and records each before it
  asserts. On FAT32 and on exFAT all six, the folder included, printed
  the same shape:

  ```
  OBSERVED Fat32 case-only Note.md -> note.md: REPORTED SUCCESS AND KEPT Note.md (listing ["Note.md"], canonical_path Ok(Some("Note.md")))
  OBSERVED Fat32 case-only Docs -> docs: REPORTED SUCCESS AND KEPT Docs (listing ["Docs"], canonical_path Ok(Some("Docs")))
  OBSERVED Exfat case-only MIXED.MD -> MIXED.md: REPORTED SUCCESS AND KEPT MIXED.MD (listing ["MIXED.MD"], canonical_path Ok(Some("MIXED.MD")))
  ```

  `FILE_ID_INFO` did not answer on either volume (error 87). The other
  five provider facts passed. That earlier session fact showed the index
  moving while the volume kept its names:

  ```
  OBSERVED FAT32 session case-only Note.md -> note.md: REPORTED SUCCESS AND KEPT Note.md (listing ["Docs", "Note.md"], files ["Docs/Inner.md", "note.md"], dirs ["Docs"])
  OBSERVED FAT32 session case-only Docs -> docs: REPORTED SUCCESS AND KEPT Docs (listing ["Docs", "Note.md"], files ["docs/Inner.md", "note.md"], dirs ["docs"])
  ```

  So the runner's volumes ignore the rename as the owner's desktop did
  [P-W2], on a different Windows build.
  That run is the provider fact's red phase on a real volume, and the
  earlier session fact's: one of six provider facts and one of three
  session facts failed, on both volumes.
- **Run 43956121701 (job 133611086339, commit bc2dd72d): `main`'s
  product code with CB-13's own session fact.** The session fact renames
  the folder through `rename_folder_with_note`, and the folder holds its
  note `Docs/Docs.md`. This is that fact's red phase on a real volume.
  The provider fact failed as in the first run, all twelve cases
  `REPORTED SUCCESS AND KEPT`. The session fact failed on both volumes
  with the same two lines:

  ```
  OBSERVED FAT32 session case-only Note.md -> note.md: REPORTED SUCCESS AND KEPT Note.md (listing ["Docs", "Note.md"], in the folder ["Docs.md", "Inner.md"], files ["Docs/Docs.md", "Docs/Inner.md", "note.md"], dirs ["Docs"])
  OBSERVED FAT32 session case-only Docs -> docs: failed: InvalidArgument { message: "renaming the folder note failed (invalid vault-relative path \"Docs/Docs.md\": no such file in the index) AND rolling the folder rename back failed (invalid argument: destination equals source): the folder is now at \"docs\" with its note still named \"Docs.md\" — a rescan reconciles the index" } (listing ["Docs", "Note.md"], in the folder ["Docs.md", "Inner.md"], files ["docs/Docs.md", "docs/Inner.md", "note.md"], dirs ["docs"])
  ```

  So before the read-back, a folder with its note fared worse than a
  plain folder. The folder hop was reported as done and moved the index
  to `docs/…`; the note hop then failed, the rollback failed, and the
  reader was told the folder is now at `docs` while the volume held
  `Docs`.
- **Run 43943403456 (job 133566627722, commit 6061441e), made between
  the two above: the same facts with the read-back in.** Six provider facts and three session facts
  passed. All twelve provider cases were refused, with the listing and
  `canonical_path` on the old spelling:

  ```
  OBSERVED Fat32 case-only Note.md -> note.md: refused: This drive keeps the capitalization a file was created with, so Note.md was not renamed to note.md. (listing ["Note.md"], canonical_path Ok(Some("Note.md")))
  OBSERVED Exfat case-only Docs -> docs: refused: This drive keeps the capitalization a file was created with, so Docs was not renamed to docs. (listing ["Docs"], canonical_path Ok(Some("Docs")))
  ```

  The same six renames made by hand, on a source handle opened under the
  old spelling and kept open across the read, twelve lines of this
  shape:

  ```
  OBSERVED Fat32 read-back by hand Note.md -> note.md: kernel Ok("done"); with the source handle open, the final-path read Ok("Note.md") and the listing read Ok("Note.md"); the test's listing ["Note.md"]; the final-path read after the handle closed Ok("Note.md")
  OBSERVED Exfat read-back by hand Docs -> docs: kernel Ok("done"); with the source handle open, the final-path read Ok("Docs") and the listing read Ok("Docs"); the test's listing ["Docs"]; the final-path read after the handle closed Ok("Docs")
  ```

  The vault watcher, on both volumes:

  ```
  OBSERVED Fat32 watcher around a case-only rename (refused: This drive keeps the capitalization a file was created with, so Watch.md was not renamed to watch.md.): events for the renamed name []; the provider's own echo: Watch.md true, watch.md true; the later file's event arrived: true
  ```

  The session fact, through `rename_file` and `rename_folder_with_note`,
  on both volumes:

  ```
  OBSERVED FAT32 session case-only Note.md -> note.md: refused: This drive keeps the capitalization a file was created with, so Note.md was not renamed to note.md. (listing ["Docs", "Note.md"], in the folder ["Docs.md", "Inner.md"], files ["Docs/Docs.md", "Docs/Inner.md", "Note.md"], dirs ["Docs"])
  OBSERVED FAT32 session case-only Docs -> docs: refused: This drive keeps the capitalization a file was created with, so Docs was not renamed to docs. (listing ["Docs", "Note.md"], in the folder ["Docs.md", "Inner.md"], files ["Docs/Docs.md", "Docs/Inner.md", "Note.md"], dirs ["Docs"])
  ```
- **SMB, local only.** The job has no share. On the loopback share of
  P-W1 (Windows 11 10.0.26300, 2026-11-16) the fact passed with the
  read-back in: all six cases `renamed`, each read equal to the listing
  with the source handle open and after it closed, the watcher hearing
  the rename's events with both spellings the provider's own echo, and
  the identity inverse renaming its alias.

## Appendix B. Sources

- macOS 27 SDK man pages: `rename(2)`, `getattrlist(2)`, `fcntl(2)`,
  `fsgetpath(2)`, `open(2)`, `link(2)`, `unlink(2)`, `stat(2)`,
  `chflags(2)`, `clonefile(2)`, `fs_snapshot_create(2)`,
  `exchangedata(2)`, `getiopolicy_np(3)`, `diskutil(8)`; headers
  `sys/stdio.h`, `sys/attr.h`, `Foundation/NSFileManager.h`.
- XNU (github.com/apple-oss-distributions/xnu): `bsd/vfs/vfs_syscalls.c`
  (`renameatx_np()` flag mask, `renameat_internal()`, `openbyid_np()`),
  `bsd/vfs/kpi_vfs.c` (`vn_rename()`), `bsd/vfs/vfs_lookup.c` (`namei()`
  volfs block, `vfs_getrealpath()`), `bsd/sys/namei.h`
  (`CN_SECLUDE_RENAME`); tags xnu-11215.1.10 and xnu-11417.140.69.
- Apple SMBClient (github.com/apple-oss-distributions/SMBClient):
  `kernel/smbfs/smbfs_vfsops.c` (`smbfs_vfs_getattr()` capabilities),
  `kernel/smbfs/smbfs_vnops.c` (vnode operation table),
  `kernel/smbfs/smbfs_smb.c` (`smbfs_getino()`).
- Apple Technical Note TN1150 (HFS Plus volume format).
- Linux man pages (man7.org): `rename(2)`, `link(2)`,
  `open_by_handle_at(2)`, `statx(2)`, `F_SETLEASE(2const)`,
  `ioctl_iflags(2)`, `fanotify_init(2)`; kernel `fs/nfs/dir.c`
  (`nfs_rename()`); kernel documentation, ext4 "Index Nodes"
  (kernel.org/doc/html/latest/filesystems/ext4/inodes.html); btrfs
  documentation, Subvolumes.
- Microsoft Learn: `CreateFileW` (`FILE_SHARE_DELETE`), `FILE_RENAME_INFO`.
- Slate at 13624900: `crates/slate-core/src/vault/{provider.rs,fs.rs,
  windows_entry.rs,trash_snapshot.rs}`, `crates/slate-core/src/session.rs`,
  `apps/slate-mac/Sources/SlateMac/AppState.swift`,
  `docs/plans/31_file_management_contracts.md`,
  `docs/plans/32_canonical_file_identity_contracts.md`.
