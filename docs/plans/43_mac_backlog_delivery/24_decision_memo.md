# 24 — Decision memo: #1125's five open decisions

Status: for the owner. D4 asked for the evidence first; it is in
`24_structural_identity_capability_matrix.md` (Track B, "the matrix") and
`24_trash_handoff_feasibility.md` (Track A, "the Trash doc"), both revised
after an independent red-team review. This memo lists the five decisions
from #1125's status of 2026-09-14, the options, the evidence for and
against each, and a recommendation. Nothing is implemented until the
owner decides.

## The evidence in six lines

1. No macOS or Linux rename primitive binds the source to a checked
   identity, and nothing on those systems stops another process from
   renaming the source meanwhile; Windows has both (matrix §2). Slate's own
   renames fall further short: they use absolute paths, so a vault
   directory replaced by a symlink redirects them into or out of the vault
   (P1b).
2. No system Trash API on any platform accepts a handle; a macOS file
   reference URL still trashed the wrong object 3.4-4.0% of the time under
   an adversarial race (Trash doc §3, P13).
3. Today's path handoff follows a substituted ancestor and trashes an
   object outside the vault, through `NSFileManager` (P12.6) and through
   Finder, which is what trash-rs calls (P16).
4. Nothing on macOS or Linux binds content: writers holding descriptors
   keep writing into a trashed or staged tree; on HFS+ the current
   fingerprint misses a same-size edit within one second; the Trash move
   itself changes the root's ctime (P7, P10, P12.4, P12.7).
5. Detection after the fact works where the result location is known and
   the volume's IDs are object identities; they are not on smbfs without
   server file IDs, and FAT32 and ext4 reuse numbers (matrix §5).
6. Side findings, outside #1125's scope: exFAT fails no-replace renames
   to a free name with `ENOTSUP`, so by code reading Slate's renames and
   no-replace creates should fail on exFAT vaults (Slate itself was not
   run); FAT32 performs `RENAME_SWAP` as a replacing rename; by source,
   smbfs and Linux NFS fail no-replace renames too (matrix §2, §5).

## Recommendations at a glance

| # | Decision | Recommendation |
| --- | --- | --- |
| 1 | Guarantee scope | Objects and tree membership, reported truthfully; "by name and metadata only" where IDs are not identities; never exact bytes; writers holding open descriptors out of scope |
| 2 | Unsupported-platform policy | Harden today's renames against substituted ancestors first; then an identity-*checked* (not bound) inverse under its own capability name where IDs are meaningful; a history barrier, as on Windows, where they are not |
| 3 | Trash semantics | Keep the ordinary system Trash; narrow the handoff now (T0+); then verify what arrived (T2); no staging; Linux work deferred with Linux |
| 4 | Identity lifetime | Filesystem-object identity, invalidated by replacement, as Windows does, keyed on a persistent volume ID |
| 5 | Closure | Record these decisions and the residuals in FR-8 and FR-10, split the work and the side findings into their own issues, and close the umbrella |

## Decision 1 — Guarantee scope

**Question.** Does confirmation bind the selected objects, their tree
membership, or exact bytes, and under which external-writer and provider
conditions must it hold?

| Option | For | Against |
| --- | --- | --- |
| A. Exact bytes against any writer | The original atomicity goal | Not achievable on macOS or Linux: no OS revokes open descriptors (P10.2, P12.4); metadata misses edits (P7). On Windows, opening every file without `FILE_SHARE_WRITE` could exclude file writers for the handoff; not explored, nor whether it can stop new children in a directory |
| B. Objects and tree membership; prevented where the OS can (Windows renames), detected and reported everywhere else; open-descriptor writers excluded | Expected achievable on all hosts (Trash doc T2; matrix M3, M4), though only shown on disk-image volumes; covers external writers that replace or add files | A wrong object can still move before Slate reports it; not "atomic"; weaker on volumes whose IDs are not identities |
| C. Cooperating Slate writers only | Already delivered (FR-8) | An explicit scope change the issue warns against; leaves P1b, P12.1, P12.6 and P16 silent |

**Recommendation: B**, worded as the contract: *Slate never reports a
Trash or an inverse as confirmed unless the object that moved is the
confirmed object and, for Trash, its tree matched the confirmed inventory
when re-observed at its destination. Where the volume's file IDs are not
object identities, a Trash is reported as verified by name and metadata
only, and an inverse is not offered. When an external writer defeats the
check, Slate reports what happened and where the item went, and does not
claim to have prevented it.* Record "exact bytes" and "writers holding
open descriptors" as out of scope, with the probes as the reason.

## Decision 2 — Unsupported-platform policy (macOS and Linux inverses)

**Question.** With no binding primitive (matrix §2), preserve today's
behaviour with documented limits, refuse the inverses, or restrict them?
Today mac's single move and rename undo checks only that the source
exists and the destination does not (`AppState.swift:18144-18166`), and
batch undo calls core's `undo_batch_move` without identities (Windows
calls `undo_batch_move_if_identities`); both then rename by absolute path
with no-replace.

| Option | For | Against |
| --- | --- | --- |
| H. Harden the rename itself: rename relative to the pinned vault root with `RENAME_NOFOLLOW_ANY` on macOS, from pinned parent descriptors on Linux | Removes the substituted-ancestor redirection (P1b: `ELOOP` instead of a move into or out of the vault); small; independent of every other choice | Does not bind the leaf; Linux form not probed |
| A. Keep today's existence-only inverse, documented | No work | A file replaced while the user was away (sync conflict, atomic save, Finder) is renamed back as if it were the original, and that window is user time, not microseconds |
| B. Refuse structural undo and redo (a history barrier) | No wrong-object risk from undo | A visible regression of shipped undo where applied everywhere (FR-10 deliberately preserved mac undo) |
| C. Identity-checked inverse: record the identity at the forward operation, check it under the structural lock immediately before the hardened no-replace rename, verify what arrived, move back and invalidate history on a mismatch; carried through compound, batch and recovery records as on Windows; a new capability name, never `rename_if_identity` | Closes the user-time window that matters in practice; the residual is the gap between two system calls, after which the wrong move is detected and reported (matrix M3, M4) | New core and host work; a transient wrong move stays possible and visible to sync clients; the move back can fail (`EEXIST`, P10.4); undo is then refused after any external atomic save or sync replacement, as on Windows, which mac undo tolerates today; mac recovery records need the volume UUID, not `st_dev` (matrix §4); gating on `VOL_CAP_FMT_PERSISTENTOBJECTIDS` refuses every non-Apple SMB server, even one with file IDs; must never be described as Windows-equivalent |
| D. `/.vol` ID-addressed rename | Moved the ID's own object in 220,000 of 220,000 race iterations (P9) | Undocumented; XNU translates to a path with no ID re-check, so nothing is guaranteed; the kernel picks the hard link; can act outside the vault (matrix §2, P8) |

**Recommendation: H first, then C where identities are meaningful, and B
(a history barrier) where they are not** (smbfs without server file IDs,
FAT32, exFAT, Linux NFS). B there matches FR-10's Windows rule ("Missing/
unsupported IDs never fall back to an existence-only inverse",
`31:462-464`) and keeps Decision 1's contract. If the owner prefers to
keep mac undo on those volumes, A there is the alternative, recorded as
an accepted residual and named in the contract. Reject D. Keep
`rename_if_identity` the Windows-only bound primitive, so the two
guarantee classes stay distinct in code, in history records and in the
contract text.

## Decision 3 — Trash semantics

**Question.** Must ordinary system Trash behaviour be preserved, or may a
staging/quarantine workflow be used?

| Option | For | Against |
| --- | --- | --- |
| T0. Today | Shipping; system Trash and Put Back | Silent wrong-object and outside-the-vault Trash (P12.1, P12.6, P16); Linux cross-device copy then recursive delete (from source) |
| T0+. Derive the Trash path from the pinned parent and refuse it outside the vault; on macOS pass a file reference URL | Cheap; removes the deterministic outside-the-vault case; the reference URL cuts the race from 48.7% to 3.4-4.0% (P13) | Narrows, does not bind; the reference URL needs `NSFileManager` rather than Finder |
| T2. Keep the system Trash, verify what arrived, report mismatches | Restore unchanged; detects P12.1, P12.6 and P16 where identities are meaningful | Shown only on disk-image volumes; needs the result location (a Slate-owned Finder script or `NSFileManager`, P12.3, P16), so trash-rs's macOS and Windows back ends are replaced; must exempt the root's ctime (P12.7); whether Slate may inspect its own item in `~/.Trash` is untested and needs a Slate build to test; only "by name and metadata" on weak-ID volumes |
| T3. Private staging, then Trash | Windows can capture through the confirmed handle; staged trees are out of reach of path-addressed writers | Put Back and Restore return items to the staging directory (P15, P16); compensation can strand items (P10.4); sync side effects; crash journal; swap capture destroys targets on FAT32; still no content binding |
| T4. Linux only: Slate's own freedesktop move | Spec-compliant system Trash; no ancestor re-resolution; refuses cross-device instead of copy and delete | Slate has no Linux host today (README: "Linux deferred") |

**Recommendation: T0+ now, T2 next, reject T3, defer T4 until a Linux
host exists.** For T2 on macOS, choose between a Slate-owned Finder
script (keeps Finder's Trash behaviour; needs the Automation grant, which
unattended runners may lack) and `NSFileManager` (no grant; returns
`resultingItemURL`; takes the reference URL of T0+; wrote put-back records
for every item from a long-lived process, P15). Both write put-back
records (P15, P16). Before committing to T2, run the `~/.Trash` access
check from a Slate build. The restore and crash-recovery semantics each
option needs are in the Trash doc §5.

## Decision 4 — Identity lifetime

**Question.** Keep filesystem-object identity, or require a permanent
incarnation identity across deletion and ID reuse?

| Option | For | Against |
| --- | --- | --- |
| A. Filesystem-object identity (persistent volume ID + file ID), invalidated by replacement including atomic save; today's Windows rule | Matches FR-10; available wherever IDs are meaningful; no APFS ID reuse was seen (P6) | IDs can recur: ext4 and FAT32 reuse numbers, HFS+ after a 32-bit wrap (TN1150), NTFS eventually (FR-10 residual); APFS non-reuse is documented only through a capability bit that HFS+ also sets. On macOS the volume part must be the file system UUID (`ATTR_VOL_UUID`), because `st_dev` can change between mounts |
| A+. A, plus Linux `name_to_handle_at` handles where supported | Could distinguish a reused inode number | Untested: the man page shows detection by opening the handle with privilege, and handles are opaque, so comparing their bytes is an inference; Linux has no host |
| B. Document identity (`ATTR_CMN_DOCUMENT_ID`) | Survives atomic saves (P7) | mac only; "sticky to the path", so a replacement counts as the same document, the opposite of today's conservative rule; requires setting `UF_TRACKED` on users' files |
| C. Slate-assigned incarnation IDs (extended attributes or a sidecar) | Survives deletion and reuse | Copied or stripped by atomic saves and sync clients; a new persistence layer; no external writer honours it |

**Recommendation: A**, with the per-filesystem reuse residual recorded
(matrix §5) rather than a claim of permanence. Defer A+ with Linux. Do not
adopt B or C.

## Decision 5 — Closure criteria

**Question.** Implement and verify the chosen guarantees, or accept and
split out the residual limits with an owner-recorded rationale?

| Option | For | Against |
| --- | --- | --- |
| A. Close now on these documents alone | Cheapest | Without recorded decisions and updated contract text, closing would read as if the documents delivered the guarantee, which the issue forbids |
| B. Implement the decisions, then close with the residuals recorded in FR-8 and FR-10 | Each track ends implemented and verified | Keeps one umbrella open across several unrelated PRs and months of work |
| C. Keep #1125 open indefinitely as an umbrella | No decision forced | Contradicts the "explicit disposition" the issue requires |
| D. Record the decisions, rewrite FR-8 and FR-10 to state the accepted limits in the exact terms of the Trash doc §7 and the matrix §2, open one issue per piece of follow-up work, then close the umbrella | The issue allows closing by "explicitly accept/split out the residual limits with owner-recorded rationale"; each follow-up stays independently reviewable | Relies on the follow-up issues being tracked; the umbrella no longer shows the open work |

**Recommendation: D.** The follow-up issues: H and T0+ (existing defects,
independent of the policy, first); T2; the checked inverse (Decision 2 C
and its barrier); and the side findings (exFAT and, after one probe on a
real share, SMB no-replace renames; FAT32 `RENAME_SWAP`; Linux NFS). The
tests each one needs are in the Trash doc §6 and in #1125's own list:
replacement after validation, destination collision, case-only and
normalization-only renames, directories, unsupported providers, and
failure during compensation and recovery, with Windows' regressions
untouched.

## If the recommendations are accepted

First estimates, to be sized properly in each follow-up's spec:

- H and T0+: one core PR (root-relative or pinned-parent renames, the
  Trash path derived from the pinned parent, the substituted-ancestor
  tests; S to M).
- T2: one core PR replacing trash-rs's macOS and Windows back ends with
  calls that return the result location, plus the re-inventory with the
  ctime exemption, the new outcomes, the fault point and tests (M to L);
  one host PR per platform for the new outcomes' accessible wording (S
  each).
- Track B: one core PR for the checked capability (capture with the
  volume UUID, check, verify, compensate, recovery-record version; M), one
  mac host PR to capture identities at the forward operation and carry
  them through the undo stacks with refusal and recovery messaging (M).
- Side findings: one issue each, starting with a probe on a real SMB
  share.

Each would get its own delivery spec under `specs/`, as every PR in this
program has, before any code.
