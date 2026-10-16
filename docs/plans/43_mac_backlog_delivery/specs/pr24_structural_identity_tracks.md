# PR 24 — #1125 Track B capability matrix and Track A feasibility (documents first, D4)

Issue: #1125 (umbrella; FR-8 and FR-10 are the residuals). Size: docs S;
implementation sized after the owner's policy decision. Lanes: windows
(docs trigger it). Depends on nothing. Any time.

## Goal

The two open tracks of #1125 get the evidence the owner asked for before
any policy call (D4): a platform capability matrix for identity-
conditional inverses on macOS and Linux (Track B) and a feasibility
investigation for binding the final Trash handoff to the confirmed
selection (Track A). No implementation before the decision.

## Current state on main

- Contract 31 FR-8 (`:420-442`): staged Trash inventory shipped on both
  hosts; the remaining boundary is the observation-to-Trash handoff an
  external writer can race.
- FR-10 (`:454-478`): Windows inverses compare the expected filesystem
  identity and rename through the same opened handle; macOS and Linux
  rename APIs resolve the source name during the call, so their undo
  behaviour was deliberately preserved and the shared provider interface
  refuses the conditional capability rather than faking it with a check
  then a path-addressed rename.
- The issue's five decisions (guarantee scope, unsupported-platform
  policy, Trash semantics, identity lifetime, closure criteria) are open.

## Deliverables

1. `docs/plans/43_mac_backlog_delivery/24_structural_identity_capability_
   matrix.md`: for macOS (APFS, HFS+, SMB, iCloud Drive and sync-client
   folders as providers) and Linux (ext4, btrfs, NFS): the rename
   primitives available (`renameat`, `renameat2` with `RENAME_NOREPLACE`
   and `RENAME_EXCHANGE`, `renamex_np` with `RENAME_SWAP`/`RENAME_EXCL`),
   whether any binds the source to a previously checked identity, what an
   open-descriptor identity check (`fstat` dev and inode, `F_GETPATH`)
   can and cannot guarantee, the case-only rename behaviour, directories,
   and ID reuse. Each cell cites the man page or XNU source and, where
   cheap, a probe program's result on the Mac Studio.
2. `24_trash_handoff_feasibility.md` (Track A): what confirmation can
   bind (root identity, descendant identities and names, content
   versions), which providers can enforce it, whether a handle-bound
   handoff or a private staging/quarantine step is viable while keeping
   system Trash behaviour, with the refusal, cancellation, partial-batch,
   rollback, crash-recovery and restore semantics each option would need.
   A negative result is recorded as the exact limitation.
3. A short decision memo listing the five decisions with the matrix's
   evidence against each option, for the owner.

## Acceptance

- [ ] Both documents cite sources and probe results; no option is
      claimed viable without evidence.
- [ ] The memo asks the five questions with recommendations.
- [ ] #1125 commented with the documents; no code change.

## Brief

Produce the Track B capability matrix and the Track A feasibility
investigation for #1125 (D4: evidence before policy). Read contract 31
FR-8 and FR-10, the issue's status comment of 2026-09-14, the Windows
identity-conditional inverse implementation (grep `FILE_RENAME_INFO` /
the conditional rename in `apps/slate-windows` and `crates/slate-core/
src/vault/`), and the macOS and Linux rename documentation. Write the
two documents and the decision memo in this directory, with probe
results where a probe is cheap. Do not implement anything. Done when
the owner can decide the five questions from the documents alone.
