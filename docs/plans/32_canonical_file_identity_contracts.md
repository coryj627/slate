# Canonical file identity (#1077): contracts

Scope: one identity for one physical file across the index, the
durable write-intent markers, the epoch rows, and every host
comparator — on volumes that alias distinct spellings (NTFS and APFS
default to case-insensitive; APFS also normalizes Unicode). Written
BEFORE implementation per `24_red_team_protocol.md` §0. The divergence
(ID) and accepted-risk (IR) registers are owner-recorded and
**off-limits for review re-litigation** — a reviewer may report that an
entry is factually wrong, not that the trade-off should be re-made.

Contract numbering is per-document. "I3" here is unrelated to any other
document's contract 3.

## The findings that shape this issue

**1. The only existing case gate is ASCII-only — measured, not
assumed.** `index_entry_case_insensitive` (`session.rs`) resolves
aliases with SQLite's `lower()`. Stock SQLite `lower()` does not
lowercase non-ASCII, verified by probe against this build:

```
lower("A.md")      = "a.md"        <- ASCII folds
lower("Ä.md")      = "Ä.md"        <- NOT folded
lower("İstanbul")  = "İstanbul"    <- NOT folded
lower(NFC "café.md") = lower(NFD "café.md")  -> false
```

So `Ä.md` and `ä.md` do not collide under the gate, while NTFS and APFS
treat them as one physical file. The gate is narrower than #1077
describes: the hole is not merely "exact string keys" but "the
mitigation itself only covers ASCII."

**2. That gate is a refusal, not an identity.** It answers "is some
row already here under another spelling?" and is used to REJECT a
create or rename. Nothing resolves a requested path to the row that
already represents it, which is what the save and delete paths need.

**3. Creates already fail closed; saves do not.**
`create_exclusive_binding` follows the ASCII gate with
`provider.stat(path)` — a real filesystem probe, which on a
case-insensitive volume DOES observe the alias. So a create cannot
clobber. `save_text` has no such probe: it calls
`index_saved_file(path)` with the exact requested spelling, so a save
through an alias inserts a SECOND index row for one physical file. The
same asymmetry applies to `text_write_intents`, keyed by exact path
string.

**4. The scan is already canonical.** The scanner walks
`provider.list_dir`, so every scanned row carries the filesystem's own
spelling. Index drift therefore originates ONLY from host-supplied
mutation paths — not from scanning. This scopes the work: no migration
or repair pass over existing rows is required, and a rescan heals a
drifted vault today.

**5. `VaultProvider` has no canonicalization surface.** The trait
offers `list_dir`, `read_file`, `write_file`, `delete`, `rename`,
`stat`, `mutation_path_exists`, `mutation_path_kind`, `create_dir`,
`watch` — nothing that answers "what spelling does the filesystem
actually store for this path?". Equivalence must come from the
provider (#1077's explicit direction); the trait cannot express it yet.

**6. The concrete loss scenario is already written.** Codex round 2 of
W5-3 constructed it: a parked tab of a deleted `Ghost.md`; the user
creates `ghost.md`; the create succeeds (the deleted row is gone);
Windows is case-insensitive/case-preserving so both spellings address
one physical file; but every workspace identity comparison is
`StringComparison.Ordinal`, so the parked tab is neither reloaded nor
retargeted — and its failed load left no content hash, so saving takes
the hashless unconditional-save path and **overwrites the newly created
note**. That scenario is this issue's acceptance test.

## Contracts

**I1 — Canonical identity comes from the filesystem, never from a case
rule.** A new provider method answers it:

```rust
/// The spelling the filesystem actually stores for an EXISTING path,
/// or None if nothing exists there. Never invents a normalization.
fn canonical_path(&self, relative: &str) -> Result<Option<String>, VaultError>;
```

A global lowercase rule is forbidden (#1077): it merges genuinely
distinct files on case-sensitive volumes and makes case-only renames
unexpressible. The default trait implementation returns the requested
spelling when the path exists (`mutation_path_exists`) and `None`
otherwise — so a case-sensitive volume and every in-memory test
provider behave exactly as today, with no new cost (ID-1).

**I2 — Only EXISTING targets are canonicalized.** A create keeps the
caller's spelling verbatim: that is how a user legitimately creates
`Ä.md`, and inventing a normalization there would corrupt the name the
user chose. Resolution applies when the target already exists — the
save, delete, rename-source, and move-source paths.

**I3 — One physical file, one index row, one marker key.** Once
resolved, the canonical spelling is what binds: the `files`/`dirs` row,
`text_write_intents`, the epoch rows, and the op-log binding. A save
through an alias updates the canonical row rather than inserting a
second one.

**I4 — A case-only rename stays expressible.** `A.md → a.md` is the
sharp edge: source and destination alias each other, so canonicalizing
the DESTINATION would collapse it onto the source and turn a legal
rename into a no-op or a false `DestinationExists`. The rename path
resolves the SOURCE only, and treats a destination that canonicalizes
to the resolved source as the legal case-only rename it is — not a
collision.

**I5 — The ASCII `lower()` gate is superseded, not extended.** Once
resolution exists, `index_entry_case_insensitive`'s job (refuse a
colliding create) is answered by the canonical resolver plus the
existing `provider.stat` probe, both of which are Unicode-complete
because the filesystem answers them. Leaving an ASCII-only SQL gate
beside a filesystem-truthful resolver would be two identity rules
disagreeing — the exact shape this issue exists to remove.

**I6 — Host comparators keep their ordinal comparisons.** Windows and
mac compare paths with `StringComparison.Ordinal` / `==` in dozens of
places (tab reuse, same-path mirroring, save targeting, invalidation).
None of those change. They become correct because BOTH sides of every
comparison are canonical spellings — identity is fixed at the boundary
where a path enters core, not re-litigated at each comparison site.
This is deliberate: a half-aliased host (some sites case-insensitive,
some ordinal) is worse than a consistently ordinal one, which is
exactly why W5-3 declined to fork one sweep and routed the fix here.

**I7 — Failure is honest and closed.** A provider that cannot answer
canonicalization returns the requested spelling (I1's default), never a
guess. A canonicalization that fails with an IO error fails the
mutation rather than silently proceeding on the unresolved spelling —
proceeding is precisely how the second index row appears.

## Divergence register (owner-recorded; off-limits for re-litigation)

**ID-1 — The default provider implementation is identity.** Rather than
requiring every provider to implement real canonicalization, the trait
default returns the requested spelling for an existing path. Effect:
case-sensitive volumes (Linux/ext4), the in-memory test providers, and
any future provider get today's exact behavior with no new filesystem
round-trip. Only the filesystem-backed providers on aliasing volumes
override it. The alternative — a required method — would force every
test double to model filesystem case semantics it does not have.

**ID-2 — Canonicalization is scoped to mutation entry points, not
reads.** `read_text` and the query surfaces keep accepting whatever
spelling the caller supplies. Reads through an alias already work (the
filesystem resolves them) and cannot corrupt identity; paying a
round-trip on every read to normalize a string nobody stores would be
cost without a defect behind it.

## Accepted-risk register (owner-recorded; off-limits for re-litigation)

**IR-1 — Canonicalization is a probe, so a TOCTOU window remains.** The
filesystem can change between resolving a path and mutating it. This
narrows the window to the same residue #1125 records for staged
confirmation and identity-conditional inverses; closing it entirely
needs the mutation itself to be identity-conditional (open once, act on
the descriptor), which is #1125's work, not this document's.

**IR-2 — Cross-volume normalization drift is out of scope.** A vault
cloned between APFS (which may store NFD) and NTFS (which preserves
what it was given) can legitimately hold two different byte spellings
of the same user-visible name in two different clones. Canonical
identity is per-volume — it makes each clone internally consistent, and
makes no claim across them.

**IR-3 — Batch cost.** A batch move of N entries adds up to N
canonicalization probes. Bounded by the existing
`MAX_STRUCTURAL_BATCH_ITEMS` cap and cheap relative to the mutations
themselves, but it is real, and it is why ID-2 keeps reads out.

## Phase plan

**Phase 0 — the provider surface.** `canonical_path` on
`VaultProvider` with the identity default (I1); real implementations on
the filesystem providers (Windows: the final-path/handle query;
macOS: `F_GETPATH` on an opened descriptor, which returns the stored
form); unit facts per platform including the ASCII, non-ASCII, and
NFC/NFD cases from finding 1.

**Phase 1 — bind the mutation entry points.** Resolve the source on
`save_text`, `delete_file`, `delete_folder`, `rename_*`, `move_*`, and
bind index/op-log writes to the resolved spelling (I2, I3). Keep
creates on the requested spelling.

**Phase 2 — markers, intents, epochs.** `text_write_intents` and the
epoch rows key on the canonical spelling (I3), so a marker planted
through one spelling is cleared through any alias.

**Phase 3 — the collision gate.** Retire the ASCII `lower()` gate in
favour of the resolver + `stat` probe (I5), with facts covering the
non-ASCII and NFC/NFD collisions the old gate missed.

**Phase 4 — the acceptance scenario and close-out.** The finding-6
stale-overwrite scenario as an end-to-end regression; the Windows host
comparators verified unchanged (I6); mac's half recorded as a
follow-up, since Swift is not verifiable from the Windows side.

## Explicitly not in this document

- **Identity-conditional mutation** (open-once, act-on-descriptor) —
  that is #1125.
- **Descriptor-relative session interior** (the A→B→A root swap) —
  that is #940, which shares the "resolve by handle, not by name"
  direction but addresses the vault ROOT rather than entries within it.
- **Link resolution semantics.** Wikilink matching has its own
  case rules; this document changes which spelling the INDEX stores,
  not how a link resolves to it.
