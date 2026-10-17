# 24 — Trash handoff feasibility (#1125 Track A)

Status: evidence for owner review under D4. No code change. Written
2026-10-16 and 2026-10-17 against `main` at 13624900, revised after an
independent red-team review. The citation key is at the top of
`24_structural_identity_capability_matrix.md`, and the probe environment
and volumes (V0-V5) are in its Appendix A; probes P12-P16 are described
here. Every Trash probe ran on a scratch disk image (V1: APFS, V3: HFS+),
whose Trash is `<volume>/.Trashes/501`; nothing was sent to the owner's
Trash.

The question (from #1125): can the final Trash handoff be bound to the
confirmed selection, and if so at what level (root identity, descendant
identities and names, content versions), by which providers, through a
handle-bound handoff or a private staging step, while keeping system
Trash behaviour?

## 1. The handoff today

Both hosts trash through core (contract 31 FR-8,
`docs/plans/31_file_management_contracts.md:420-442`):

1. `Session::batch_trash_checked` (`crates/slate-core/src/session.rs:14634`)
   claims the single-use token, takes the vault structural lock and the
   structural operation mutex, validates the whole request, plants
   recovery markers, then for each item begins the SQLite writer fence,
   re-takes the inventory and compares it with the token's
   (`validate_item_cancellable`, `trash_confirmation.rs:121-142`), and
   calls `provider.delete(path)` inside the fence (`session.rs:14737-14753`).
2. The inventory descends from the vault root one directory at a time with
   `O_NOFOLLOW` (`crates/slate-core/src/vault/fs.rs:247-307`), then walks
   the selection the same way (`trash_snapshot.rs:119-245`), hashing per
   entry `st_dev, st_ino, st_mode, st_size`, mtime and ctime with
   nanoseconds, and every child name (`:100-113`).
3. `FsVaultProvider::delete` (`fs.rs:703-725`) rebuilds the absolute path
   lexically and calls `trash::delete(&path)`.
4. trash-rs 5.2.6 resolves the parent with `Path::canonicalize`, which
   follows symlinks (`src/lib.rs:228-258`), then:
   - **macOS**: the default `DeleteMethod::Finder` (`src/macos/mod.rs:46-47`,
     `:60`) runs `osascript -e 'tell application "Finder" to delete
     { POSIX file "<path>" }'` (`:112-150`) and discards the script's
     result. Spawning `osascript` alone took 30 ms on this Mac, before
     any Apple event reached Finder [P14].
   - **Windows**: `SHCreateItemFromParsingName(path)`, then
     `IFileOperation::DeleteItem` and `PerformOperations` with
     `FOF_ALLOWUNDO` (`src/windows.rs:44-59`).
   - **Linux**: the freedesktop Trash: create the `.trashinfo` with
     `create_new`, create a placeholder, `std::fs::rename(src, dst)`; on
     `EXDEV` fall back to `copy_dir_all(src, dst)` then
     `remove_dir_all(src)` (`src/freedesktop.rs:460-591`).

So the last no-follow observation is followed by a path that is resolved
again, with symlinks followed, by code Slate does not control, after a
process spawn on macOS. FR-8's residual names this boundary; the probes
below measure what crosses it. Today's structural renames have the same
ancestor weakness (matrix §2, P1b).

## 2. What confirmation could bind

| Level | Captured today | Enforceable at the handoff? | Detectable after the handoff? |
| --- | --- | --- | --- |
| **Root identity**: the selected entry is the object confirmed | Yes: the root's `(dev, ino)` is in the fingerprint | **No platform.** macOS Trash APIs take URLs; a file reference URL is resolved to a path first (3.4-4.0% wrong under a race) [P13]. Windows parses a path into a shell item. trash-rs's freedesktop path renames by path; a Slate-owned implementation would still name the source inside a pinned parent (T4). Windows alone can bind a *rename* to the confirmed handle, so it could capture into staging (T3), not hand the handle to the Recycle Bin. | **Where the result location is known and the volume's IDs are object identities.** `trashItemAtURL:resultingItemURL:` returns the location and the inode is preserved [P12.3]; so does a Finder script that returns the result of `delete` [P16] (trash-rs discards it). `IFileOperationProgressSink::PostDeleteItem` passes `psiNewlyCreated`, "the deleted item, now in the Recycle Bin", or NULL "if the item was fully deleted" [doc] (not probed). A Slate-owned freedesktop move knows its `files/` entry (T4). On smbfs without file IDs a same-name replacement carries the same number, and on FAT32 a file created after the original was deleted can reuse its number (matrix §5). |
| **Tree membership**: descendant identities and names | Yes: every name and per-entry stamps, recursively | **No platform.** A directory move takes whatever subtree is attached at that instant; no OS freezes a tree. A writer holding a directory descriptor created a child inside the tree after it was trashed [P12.4] and after it was staged [P10.2]. | **Partly**: re-run the inventory on the trashed tree at its result location and compare with the token, exempting the root's ctime, which the Trash move itself changes [P12.7]. Changes made after the move through held descriptors are indistinguishable from changes made just before it. |
| **Content versions**: exact bytes | No: metadata stamps only | **Not on macOS or Linux.** A writer's existing handle kept writing into a trashed file [P12.4] and a staged file [P10.2]; nothing revokes open descriptors. On Windows, opening every file without `FILE_SHARE_WRITE` would refuse when a writer already holds one and block new writers while Slate's handles stay open; that was not explored, nor whether directory handles can stop new children. | **Weakly.** On HFS+ a same-size in-place edit inside one second left size, mtime and ctime unchanged, so today's fingerprint misses it [P7]. `ATTR_CMN_GEN_COUNT` caught every edit on APFS and HFS+ [P7] but reads 0 while a file is mapped [P7, man getattrlist(2)]; smbfs does not advertise `VOL_CAP_FMT_WRITE_GENERATION_COUNT` (from source), FAT32 and exFAT do not report it [P17], and Linux `statx` has no generation field. Hashing contents costs a full read and still races. |

## 3. What the macOS Trash APIs actually do (P12-P16)

On V1 (APFS) and V3 (HFS+), with the same results on both unless noted:

| Probe | Setup | Result |
| --- | --- | --- |
| P12.1 | `NSFileManager` trash by path URL after another object took the name | The **replacement** went to the Trash. |
| P12.2 | Trash by file reference URL (`file:///.file/id=<fsid>.<ino>`) after the object moved to `elsewhere/` and another object took its name | The **referenced object** was trashed from its new location; the newcomer was untouched. |
| P12.3 | Compare inode before and after; trash a second item of the same name | Same inode in the Trash (a same-volume rename). The second item was renamed with a time suffix (`keep.md 23-02-03-209.md`). |
| P12.4 | Trash a folder while another descriptor has a child open for append and a directory descriptor is held | Trash succeeded. The late write and a new child created through the directory descriptor both landed **inside the trashed folder**. |
| P12.5 | Trash by the case-alias spelling `case.md` of `Case.md` | Trashed the stored entry under its stored name. |
| P12.6 | V1 only. After validation, replace the ancestor directory `vault/sub` with a symlink to `../outside`, then trash `vault/sub/note.md` by path URL | Trashed the **decoy outside the vault**. |
| P12.7 | V1 only. Compare ctime before and after trashing a file and a folder | The trashed root's inode was unchanged and its **ctime changed** (file and folder); a descendant's ctime did not. |
| P13 | V1 only. A thread keeps swapping directories `A` and `B` (each holding an object `x`) while the main thread trashes `A/x`, restores it by descriptor, and repeats | By file reference URL: 80 of 2,000 (4.0%) in a first run, 169 of 5,000 (3.4%) in a second, trashed the other object. By path URL: 974 of 2,000 (48.7%). (On HFS+ the racer could not run: `RENAME_SWAP` is unsupported.) |
| P14 | `time osascript -e 'return 1'`, five runs | 0.03 s each, without contacting Finder. |
| P15 | V1 only. Decode the put-back records in `<volume>/.Trashes/501/.DS_Store` | After the short-lived probe processes of P12 and P13, records (`ptbL`, the original parent, volume-relative, e.g. `/t/`; `ptbN`, the original name) existed only for the first item each process trashed. A process that trashed five items a second apart and stayed alive 20 s got records for every item it trashed, so the gap was an artifact of processes exiting at once. The format is private. |
| P16 | V1 only. `osascript -e 'tell application "Finder" to set r to delete (POSIX file "…")' -e 'return POSIX path of (r as alias)'` | Returned the item's location in `.Trashes/501`, same inode, with put-back records. Repeating P12.6 through Finder with the lexical path: Finder trashed the **decoy outside the vault**, and its put-back record names the real parent (`/t4/outside/`). trash-rs canonicalizes the parent before calling Finder, which resolves to the same decoy. |

## 4. Options

### T0 — today: validate under the fence, then a path-based system Trash call

Guarantees: Slate's own writers are excluded (structural lock and writer
fence); the confirmed inventory matched at the final revalidation,
shortly before the handoff (which then canonicalizes the path and, on
macOS, spawns `osascript`); FR-11 cancellation; truthful partial
outcomes. Does not guarantee: that the object trashed is the one
confirmed (P12.1); that it is inside the vault when an ancestor was
replaced by a link after validation (P12.6, P16); that the tree or bytes
trashed are the ones confirmed (§2). On Linux, a cross-device Trash
copies and then `remove_dir_all`s the source path, so anything added
under it during the copy is deleted permanently rather than trashed (from
source, not probed).

### T0+ — narrowing without changing Trash semantics

Two cheap changes narrow T0 without binding anything:

- **Hand the Trash API a path derived from the pinned parent.** Take the
  validated parent descriptor's current path (`F_GETPATH`), append the
  leaf, and refuse if the result is not under the vault root. A
  substituted ancestor then no longer redirects the Trash to an outside
  object deterministically (P12.6, P16); what remains is a race between
  deriving the path and the Trash call resolving it.
- **On macOS, pass a file reference URL** to `trashItemAtURL:` (P12.2):
  under the race it took the wrong object 3.4-4.0% of the time, against
  48.7% for a path URL (P13). This needs `NSFileManager` rather than
  Finder.

### T1 — handle-bound handoff to the system Trash

**Not available.** No system Trash API accepts a handle or descriptor:
`trashItemAtURL:` takes a URL and a file reference URL is resolved to a
path before it acts (P13); Finder's `delete` takes an item specifier,
here a path (P16); `IFileOperation::DeleteItem` takes an `IShellItem`
(trash-rs builds it from a path); the freedesktop Trash is a rename by
name. This is a negative result, recorded as such; it is not a guarantee
Slate can implement.

### T2 — verified handoff: keep the system Trash, check what arrived

After the Trash call returns the trashed item's location, compare its
identity with the confirmed root and re-run the inventory on the trashed
tree against the token, exempting the root's ctime. A mismatch becomes a
distinct outcome ("moved to the Trash, but it was not the confirmed item
/ it had changed"), reported with its Trash location; it is never counted
as a confirmed success.

- **Status: expected to be feasible; demonstrated only on disk-image
  volumes**, for `NSFileManager` (P12.3) and for a Finder script that
  returns the result of `delete` (P16).
- **Preconditions found by the review:**
  - The comparison must exempt the root's ctime, which the Trash move
    changes (P12.7); today's fingerprint includes it.
  - On the boot volume the item lands in `~/.Trash`, which macOS is
    reported to guard with per-app privacy permissions (not verified
    here). Whether Slate may `stat` and walk an item it just trashed there
    is untested, and only a Slate build can test it, because such grants
    are per app.
  - Detection is only as good as the volume's identities: on smbfs
    without file IDs a same-name replacement carries the same number, and
    on FAT32 a file created after the original was deleted can reuse its
    number (matrix §5), so T2 must say "verified by name and metadata
    only" there rather than "confirmed".
- **macOS.** Either Slate scripts Finder itself (keeps Finder's Trash
  behaviour; needs the Automation grant, which unattended runners may
  lack), or Slate switches to `NSFileManager trashItemAtURL:
  resultingItemURL:`, which needs no grant, writes put-back records from
  a long-lived process (P15), and accepts a file reference URL (T0+).
  Both mean replacing trash-rs's macOS back end.
- **Windows.** An `IFileOperationProgressSink` receives `psiNewlyCreated`
  (not probed); NULL means the item was deleted outright, which T2 must
  report as such. Same-volume recycling is a rename, so the file ID
  should survive (expected, not probed). This means replacing trash-rs's
  Windows back end as well.
- **Linux.** T4 below knows the location by construction.
- It turns "a wrong or changed object can be trashed silently" into "can
  be trashed, and is reported wherever identities can tell". It does not
  prevent the wrong move, and it cannot see edits made after the move
  through held handles.

### T3 — private staging (quarantine), then the system Trash

Capture the confirmed root by an atomic no-replace rename into a
Slate-owned directory on the same volume, verify the staged tree against
the token, then trash it from staging, or move it back on a mismatch.

- Capture binding: Windows can capture through the confirmed handle
  (`FileRenameInformation`, the primitive `windows_entry.rs` already uses),
  so the object captured is the object confirmed. macOS and Linux can only
  capture by name inside a pinned parent; the captured object is checked
  afterwards (P10.3, P10.4).
- What it buys: once staged, writers that address files by path can no
  longer reach the tree by its old path, so verification of the staged
  tree is stable against them. (Assumed, not checked: that third-party
  sync clients address files by path; Apple's daemons may address items
  by ID.)
- What it does not buy: writers with open handles and directory
  descriptors still modify the staged tree (P10.2).
- Costs:
  - **Restore breaks.** The system records the real parent at trash time
    (P15, P16: `ptbL` is the parent path), and the Windows Recycle Bin
    keeps an original location per item (trash-rs reads
    `PID_DISPLACED_FROM`, `src/windows.rs:17`, `:104`). Trashing from
    staging makes Put Back and Restore return the item to the staging
    directory. Slate would have to record original locations and own
    restore (FR-5 already lists restore-from-trash as core's follow-up).
  - **Sync side effects.** A staging directory inside the vault is synced
    like any other folder; outside the vault it must still be on the same
    volume, or the capture fails `EXDEV`.
  - **Compensation can fail.** Moving a mismatched capture back fails
    `EEXIST` once the name is retaken, stranding the object (P10.4).
  - **Crash recovery.** Items can be left in staging between capture and
    Trash; a durable journal and a recovery pass on open are required.
  - **Swap capture** (exchange the root with a placeholder) leaves a
    placeholder visible under the name, does not exist on HFS+ or exFAT,
    and on FAT32 destroys the target (matrix P2b).
- Viability: mechanically feasible; it trades a handoff race for a
  restore regression and a new recovery surface, and still cannot bind
  content.

### T4 — Slate-owned freedesktop Trash on Linux

The freedesktop specification is a documented directory format, so on
Linux Slate could implement it instead of calling trash-rs: write the
`.trashinfo` first, opened with `O_EXCL` as the specification requires,
then `renameat2(pinned parent fd, name, files fd, unique name,
RENAME_NOREPLACE)`, then verify the trashed item through the `files`
descriptor and move it back with `RENAME_NOREPLACE` on a mismatch. Refuse
a cross-device Trash instead of copying and deleting (the specification
permits implementations to decline). The result is still the system
Trash (desktop file managers read the same `info`/`files` pair), the
ancestor-link hazard disappears (no path is re-resolved), and the window
shrinks to one system call. The source is still named, so a wrong object
can be trashed for an instant before the verification moves it back.
macOS and Windows have no equivalent: the put-back records and the
Recycle Bin store are private formats. Slate has no Linux host today
(the README lists Linux as deferred), so T4 only matters once one exists.

## 5. Semantics each option would need

| | T0 (today) | T2 verified handoff | T3 staging | T4 Linux Slate-owned Trash |
| --- | --- | --- | --- | --- |
| Refusal | Stale token refuses the request (`TrashConfirmationChanged`); a later stale item is untrashed with markers kept | As T0, plus a post-handoff mismatch outcome that is never a success; an item deleted outright instead of trashed (Windows NULL `psiNewlyCreated`) is reported as deleted | Capture failure (`EXDEV`, `EEXIST`, `ENOTSUP`) and staged mismatch refuse that item | As T2; cross-device refuses |
| Cancellation | Before the next physical Trash attempt (FR-11) | As T0; a started attempt finishes its verification before cancellation takes effect | Between capture and Trash, cancellation must move the item back (which can fail) | As T2 |
| Partial batch | Confirmed, untrashed and unknown buckets | Adds "trashed but not confirmed" and "verified by name and metadata only"; recommended to stop the batch at the first mismatch, since an external writer is active | Each item has three phases (captured, verified, trashed), each a possible stopping point | As T2 |
| Rollback | None (Trash is the end state) | None by default on macOS and Windows: the user restores with Put Back or Restore, from the reported location | Move back from staging; fails if the name was retaken, leaving a stranded item | Automatic move back from `files/`, removing the `.trashinfo`; if the name was retaken, leave the item in the Trash and report |
| Crash recovery | Unknown outcomes keep markers; the orphan sweep converges | As T0; verification cannot resume after a crash (the token is session-local), so the item stays "unknown" | A durable staging journal; on open, move back or trash each staged item, handling a retaken name | `.trashinfo` written before the move, as the specification requires; a crash leaves an orphan `.trashinfo`, which recovery removes |
| Restore | System Put Back / Restore to the real parent at trash time | Unchanged | **Broken** unless Slate records original locations and owns restore | Correct, unless an ancestor was replaced: the `.trashinfo` `Path` is the lexical path, which would then route through the replacement |

## 6. Acceptance evidence for whichever option is chosen

The issue lists root replacement, descendant additions and removals,
link and reparse substitution, and, if content is in scope, in-place edits
through open handles. Each can be made deterministic with one new fault
point between `validate_item_cancellable` and `provider.delete`
(alongside the existing `BatchFaultPoint::TrashWriteFence`), at which a
test mutates the tree:

| Injected change | T0 today | T2 expected | T4 expected |
| --- | --- | --- | --- |
| Root replaced by another object | Replacement trashed, reported as success | Replacement trashed, reported as not confirmed, with its Trash location (by name and metadata only where IDs are not identities) | Replacement moved back; item reported untrashed |
| Descendant added or removed | Trashed, reported as success | Trashed, reported as changed | Moved back; untrashed |
| Ancestor replaced by a link | Object outside the vault trashed (P12.6, P16) | Outside object trashed, reported as not confirmed; with T0+, refused before the call | Impossible: no path is re-resolved |
| In-place edit through an open handle after the handoff | Edit lands in the Trash | Same; out of scope by construction | Same |

Real-Trash tests on macOS need a volume of their own: P12-P16 show that a
disk image attached with `hdiutil attach -nobrowse -mountpoint` by an
unprivileged user has its own `.Trashes`, for both `NSFileManager` and a
Finder script, so such tests need not touch a developer's or runner's
Trash. A Finder-based test still needs the Automation grant.

## 7. Negative results, stated as limitations

1. No system Trash API on macOS, Windows or Linux accepts a handle; a
   handle-bound handoff cannot be built (T1).
2. A macOS file reference URL is not an identity-bound handoff: resolved
   to a path first, it trashed the wrong object 3.4-4.0% of the time under
   an adversarial race (P13).
3. The current path handoff follows ancestor symlinks after the no-follow
   validation and can trash an object outside the vault, through
   `NSFileManager` (P12.6) and through Finder (P16).
4. No mechanism on macOS or Linux stops a writer that already holds a
   file or directory descriptor; content versions cannot be bound there
   (P10.2, P12.4). Windows writer exclusion was not explored.
5. The current fingerprint cannot see a same-size in-place edit within
   one second on HFS+ (P7), and the Trash move changes the root's ctime,
   so the fingerprint cannot be compared unchanged after the move (P12.7).
6. Staging cannot keep system restore behaviour on macOS or Windows, and
   its compensation can strand an item (P10.4, P15, P16).
7. trash-rs's Linux cross-device fallback is a path-addressed copy then
   recursive delete (from source, not probed).

## 8. Not tested here

Windows (no Windows machine in this session: all Windows statements are
from Slate's and trash-rs's source and Microsoft's documentation); Linux;
SMB; iCloud Drive and File Provider folders; the Trash on the boot volume
(`~/.Trash`), including whether Slate may inspect an item it just
trashed there; behaviour of the Recycle Bin and of File Provider
extensions under concurrent remote changes.

## 9. Sources

- Slate at 13624900: `crates/slate-core/src/session.rs`,
  `crates/slate-core/src/trash_confirmation.rs`,
  `crates/slate-core/src/vault/{fs.rs,trash_snapshot.rs,windows_entry.rs}`,
  `docs/plans/31_file_management_contracts.md` (FR-5, FR-8, FR-11),
  `README.md` (platform status).
- trash-rs 5.2.6 (the version in `Cargo.lock`): `src/lib.rs`,
  `src/macos/mod.rs`, `src/windows.rs`, `src/freedesktop.rs`.
- macOS 27 SDK: `Foundation/NSFileManager.h` (`trashItemAtURL:
  resultingItemURL:error:`), `getattrlist(2)`; Finder's scripting
  dictionary (`sdef /System/Library/CoreServices/Finder.app`, the `delete`
  command: "to the item that was just deleted").
- Microsoft Learn: `IFileOperation::DeleteItem`,
  `IFileOperationProgressSink::PostDeleteItem`, `CreateFileW`.
- freedesktop.org Trash specification
  (specifications.freedesktop.org/trash/latest).
- The matrix's Appendix B for everything else.
