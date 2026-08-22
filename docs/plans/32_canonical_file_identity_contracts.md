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

The fix for that is already in the codebase and the gate simply does
not use it: `db.rs` registers `slate_tree_sort_key(name)` =
`name.nfc().to_lowercase()` as a deterministic SQL function, persisted
in expression indexes by migration 033, and its doc comment records
that changing its semantics needs a schema migration that rebuilds
those indexes. It is the rule the file tree already sorts and filters
by. Its behavior on the scripts that matter:

- CJK ideographs, hiragana, and katakana have no case, so
  `to_lowercase` is the identity on them — `日本.md` collides only with
  a byte-identical `日本.md`. Pure-CJK names are untouched.
- NFC (not NFKC) keeps full-width and half-width forms distinct:
  `ＡＢＣ.md` ≠ `ABC.md`, `ｱ` ≠ `ア` — which is also how NTFS and APFS
  treat them. Width is a display property, not identity.
- The only CJK code points NFC changes are the compatibility
  ideographs (U+F900 block), which normalize to their unified forms —
  and APFS already unifies those on disk.
- Full-width Latin folds within its own block (`Ａ` ↔ `ａ`), matching
  NTFS's `$UpCase` table.

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

**Core-boundary canonicalization alone does NOT close it** (the
reassessment finding). The tab's save is
`_session.SaveText(Path, text, _contentHash)` with a null hash — an
unconditional write. Canonicalizing `Ghost.md` → `ghost.md` in core
routes that write squarely onto the new note: same data loss, one index
row instead of two. A tab whose spelling was captured BEFORE the file
was recreated cannot be retargeted by anything that happens at the
point its stale spelling enters core. Closing the scenario needs the
host half (I6) and the hashless-save rule (I8).

**7. The gate is a PORTABILITY guard, and it refuses case-only renames
today.** Its doc comment: *"APFS default is case-insensitive; a
differing-case collision would shadow on disk."* It runs on every
volume — including case-sensitive ones — so a vault cannot grow names
that collide the moment it lands on a Mac. Retiring it in favour of a
filesystem-truthful probe would silently drop that protection on Linux
and case-sensitive APFS. Separately, `rename_file`/`rename_folder`
probe `index_entry_case_insensitive(to)`, which for `A.md → a.md` finds
the SOURCE's own row under `lower(to)` and refuses with
`DestinationExists { A.md }` — so a case-only rename is not merely
fragile today, it is impossible (for ASCII; `Ä.md → ä.md` slips past
the ASCII gate and works).

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

**I2 — Only EXISTING targets are canonicalized; a new target's PARENT
is an existing target.** A create keeps the caller's LEAF verbatim: that
is how a user legitimately creates `Ä.md`, and inventing a normalization
there would corrupt the name the user chose. But the parent components
of a new path already exist and have a stored spelling of their own, and
a fresh row under `notes/…` beside the scanned `Notes/…` is exactly the
drift this document exists to stop — so a new target binds its parent's
stored spelling and keeps only its leaf (found in Phase 1; `rename_file`
derives the destination from the requested source spelling, which made
the hole concrete). Resolution of the whole path applies when the target
already exists — the save, delete, rename-source, and move-source paths;
parent-only binding applies to saves of new files, creates, and every
rename/move destination.

**I3 — One physical file, one index row, one marker key.** Once
resolved, the canonical spelling is what binds: the `files`/`dirs` row,
`text_write_intents`, the epoch rows, and the op-log binding. A save
through an alias updates the canonical row rather than inserting a
second one.

**I4 — A case-only rename BECOMES expressible (a behavior change).**
`A.md → a.md` is the sharp edge: source and destination alias each
other, so canonicalizing the DESTINATION would collapse it onto the
source and turn a legal rename into a no-op or a false
`DestinationExists`. The rename path resolves the SOURCE only; the
destination binds its parent (I2) and keeps its leaf verbatim — a
destination leaf is NEVER canonicalized, because canonicalizing it
would collapse the case-only rename onto its own source and make it a
no-op. The collision gate (I5) exempts a destination whose fold key
equals the resolved source's fold key — that is the legal case-only
rename, not a collision. Today the gate refuses it (finding 7); after
this document it succeeds, and the index row moves to the new spelling.

**I5 — The collision gate is EXTENDED to Unicode, not retired.** The
gate keeps its purpose (finding 7: portability across volumes) and
gains the fold rule the tree already uses: `index_entry_case_
insensitive` compares `slate_tree_sort_key(path)` instead of
`lower(path)`, backed by expression indexes on `files(path)` and
`dirs(path)` added by a schema migration (the discipline migration 033
already records for that function). The rule is NFC + Unicode
lowercase — deliberately NOT NFKC, so width and compatibility forms
stay distinct as they do on disk (finding 1). The gate remains a
REFUSAL on every volume, including case-sensitive ones: that is the
portability intent, and being slightly more conservative than a
particular filesystem is the correct side for a refusal to err on. It
is not an identity — identity still comes from the filesystem (I1);
the gate only says "do not create a second name that would alias this
one somewhere."

**I6 — Equivalence reaches the host; comparators stay ordinal, and the
host RE-SEATS spellings instead.** `canonical_path` is exposed over FFI
(`VaultSession::canonical_path`). Hosts keep `StringComparison.Ordinal`
/ `==` at every comparison site — a half-aliased host (some sites
case-insensitive, some ordinal) is worse than a consistently ordinal
one, which is exactly why W5-3 declined to fork one sweep. What
changes is a single re-resolution step: after every create, rename, or
external-create publication, a path-backed tab that is
missing-from-disk (or whose path failed to load) asks core for
`canonical_path(tab.Path)`; a non-null answer that differs ordinally
means the file came back under another spelling, and the tab is
retargeted to that spelling and reloaded through the existing
same-path reload machinery. Identity is fixed at the boundary where a
path enters the host's state, re-asserted when the filesystem changes
under it, and never re-litigated per comparison.

**I7 — Failure is honest and closed.** A provider that cannot answer
canonicalization returns the requested spelling (I1's default), never a
guess. A canonicalization that fails with an IO error fails the
mutation rather than silently proceeding on the unresolved spelling —
proceeding is precisely how the second index row appears.

**I8 — A hashless save never lands on an existing file (host-side,
owner-decided).** A tab with no content hash has never loaded bytes
from the path it names; it has no basis to overwrite whatever is there
now. Its save goes through create-exclusive semantics
(`write_file_if_absent`): success when the path is empty, otherwise
`DestinationExists` → the existing conflict flow, never a silent
overwrite. This is a HOST rule: core's documented `expected_hash =
None` → unconditional save is unchanged. It closes the finding-6
scenario even if the I6 re-resolution has not yet run — an external
recreate between the sweep and the save (the TOCTOU I6 cannot cover)
meets the same refusal.

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

**IR-4 — The gate is more conservative than some filesystems.** Under
I5 a case-sensitive volume refuses `Ä.md` beside `ä.md` (and `A.md`
beside `a.md`, which it already refuses today). That is a real loss of
expressiveness on Linux for the portability guarantee it buys, and it
is the gate's stated intent. Pure-CJK names are unaffected (finding 1).

## Owner calls

**Call 1 — gate semantics (I5, IR-4): decided 2026-08-22.** Extend to
Unicode via the existing `slate_tree_sort_key` rule (NFC + Unicode
lowercase, not NFKC). Behavior change is confined to names containing
caseable letters; kanji/kana/width are untouched (finding 1).

**Call 2 — the hashless-save rule (I8): decided 2026-08-22.** Host-side
create-exclusive semantics; core's `None = unconditional` contract is
unchanged.

## Phase plan

**Phase 0 — the provider surface.** `canonical_path` on
`VaultProvider` with the identity default (I1); the real implementation
on `FsVaultProvider` (one struct, `cfg`-split internals). Known risks,
to be settled by facts before Phase 1:

- **Symlinks/junctions.** `GetFinalPathNameByHandle` and `F_GETPATH`
  both resolve them, but `FsVaultProvider::new` only extended-path-
  prefixes the root on Windows and never canonicalizes it — so a vault
  rooted through a symlink would resolve outside its own prefix and,
  under I7, become un-mutable. Either canonicalize the root once at
  construction by the same call, or resolve per-component (readdir the
  parent, match the leaf under the filesystem's own rule), which is
  symlink-safe at O(depth) readdirs.
- **Unix already pins a descriptor** for mutations
  (`PinnedMutationTarget` = parent fd + leaf), so `F_GETPATH` on that
  fd plus a readdir match for the leaf is nearly free. The Windows pin
  is path-only.
- **The watcher** is a second ingestion source (finding 4 covers the
  scan only); verify per platform that event paths carry on-disk
  spellings.
- Unit facts per platform: the ASCII, non-ASCII, NFC/NFD, and
  pure-CJK cases from finding 1.

**Phase 1 — bind the mutation entry points.** Resolve the source on
`save_text`, `delete_file`, `delete_folder`, `rename_*`, `move_*`, and
bind index/op-log writes to the resolved spelling (I2, I3). Keep
creates on the requested spelling.

**Phase 2 — markers, intents, epochs.** `text_write_intents` and the
epoch rows key on the canonical spelling (I3), so a marker planted
through one spelling is cleared through any alias.

**Phase 3 — the collision gate (gated on call 1).** Migration adding
expression indexes on `slate_tree_sort_key(path)` for `files` and
`dirs`; `index_entry_case_insensitive` compares through it (I5); the
case-only-rename exemption (I4); facts covering the non-ASCII, NFC/NFD,
full-width, and pure-CJK cases, and `A.md → a.md` succeeding.

**Phase 4 — the host half and the acceptance scenario.**
`canonical_path` over FFI; the Windows re-resolution step after
create/rename/external-create publications (I6); the hashless save
through create-exclusive semantics (I8); the finding-6 stale-overwrite
scenario as an end-to-end regression that FAILS without Phase 4 and
passes with it; mac's half (re-resolution + I8) recorded as a
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
