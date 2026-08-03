# W4-4 Property Surfaces — Feature Contracts & Accepted-Risk Register

Scope: Windows in-note properties header, add-property sheet, and
vault-wide bulk-rename sheet (#736, `apps/slate-windows`). Written
BEFORE the adversarial review per the W4-3 retrospective protocol:
every contract below is a testable property, cited by number in the
review prompts. Core write semantics (CAS, save markers, epoch
fencing) are covered by `21_write_intent_protocol_invariants.md` and
are satisfied inside `save_text_locked` — host code never re-implements
them.

## Feature contracts

1. **CAS snapshot identity.** Every `SetProperty`/`DeleteProperty`
   call carries the content hash pinned at the read that produced the
   surface issuing it — rows pin their publish hash
   (`PropertyRowViewModel.ContentHash`), the add sheet pins the header
   VM's publish hash (`NotePropertiesViewModel.ContentHash`). Never a
   hash re-read at commit time, never null. The single exception is
   the conflict dialog's **Keep Mine**, which re-reads at the user's
   explicit choice. If disk ≠ snapshot, the outcome is
   `PropertyEditConflict` + the resolution dialog; no silent-overwrite
   path exists.
2. **Draft locality.** No keystroke, stepper bump, list item
   add/remove, or focus change writes to disk. Writes occur only at
   the enumerated commit triggers: Enter/Save for text-shaped
   editors and lists; immediate for the boolean switch and the date
   picker. Revert restores `CommittedBaseline` byte-exactly and
   announces once.
3. **Refusal totality.** Dirty tab, `IsExternallyStale`,
   write-in-flight, or failed validation ⇒ zero core calls, draft
   preserved intact, exactly one typed refusal surface (announcement
   or inline validation error). For any refused commit, the note's
   bytes on disk are unchanged.
4. **Post-write publication from authoritative bytes.** After a
   successful write: rows re-derive from
   `ParseFrontmatterProperties(ReadNoteParts(path).FmSource)` — never
   from the local draft; every clean same-path tab re-baselines to
   `SaveReport.NewContentHash` before any later save can be issued;
   the note body is byte-identical across the write.
5. **Delete safety.** The confirmation's default action (Cancel)
   never deletes; delete is disabled while the row's draft is dirty
   (spoken reason); deleting the last key removes the `---` shell;
   a stale missing-key delete surfaces `WriteConflict`, not success.
6. **Add-property validation is total and pre-core.** Empty, dotted,
   and duplicate keys are refused with the verbatim message before
   any FFI call; a successful add appends the key at the end of the
   block and leaves all other keys and the body byte-identical; an
   async add failure keeps the sheet open with the draft and the
   verbatim failure copy.
7. **Bulk-rename arming and report semantics.** Apply can only
   execute after a dry-run preview on the identical (old, new) key
   pair; any key edit disarms it (the grid stays visible).
   `affected ∪ skipped ∪ failed` partitions the candidate set; a
   per-file failure never aborts remaining files; cancellation leaves
   applied files applied and reports unprocessed ones as `Cancelled`;
   `KeyCollision` never overwrites; the sheet footer is the
   `A11yRender(RenameSummary)` output verbatim, not a host
   re-composition.
8. **Rename ↔ tab reconciliation.** After apply, every open tab whose
   path appears in `report.Affected` ends either re-baselined to that
   entry's `NewContentHash` (clean tabs) or flagged
   `IsExternallyStale` (dirty tabs); no open tab retains a stale
   `SavedContentHash` that would let a later save clobber the rename.
   Reconcile failures announce `RenameReloadFailed`, never pass
   silently.
9. **Announcement identity.** Every user-visible property outcome
   produces exactly one announcement; canonical events render via
   `A11yRender` byte-equal to the corpus goldens on the same triggers
   as mac; `HostComposed` appears only at `// W0.5-3 residue:`-marked
   sites counted 1:1 by the census.
10. **Round-trip fidelity.** Committing an unchanged draft is
    value-preserving: date/datetime stored strings survive verbatim
    (ISO-with-`Z` stays ISO, naive stays naive), `number` never flips
    integer↔float, malformed dates remain raw text (a date is never
    invented), and list/tag_list typing survives the tagged-element
    encoding (`\u{f8ff}slate.property-kind`) both directions.

## Accepted risks / recorded divergences (do not re-litigate)

- **Dirty-tab refusal** (vs mac body-only buffers that never
  conflict): Windows tabs hold the whole file; property commits
  refuse under a dirty or stale tab with a spoken reason. Building
  body-only buffers is out of bounds (recorded divergence, w_d).
- **YAML source mode out of scope**: `togglePropertiesSource` stays
  pending in the parity matrix; `set_frontmatter_source` has no call
  site. Deliberate deferral, recorded in the PR body.
- **Retained-update recovery suite out of scope**: a post-write
  refresh failure surfaces a load error + `PropertiesReloadFailed`;
  the write itself landed and was announced. The mac
  `PropertyPublicationUncertainty` machinery has no Windows twin
  (recorded designation).
- **Datetime rows edit as raw text** (the plan's sanctioned "manual
  datetime control" option): serialized-form preservation is trivially
  guaranteed; a date+time picker pair is future polish, not a gap.
- **Conflict/delete dialogs are MessageBox-hosted** (the vault-close
  YesNoCancel precedent) with non-destructive defaults; custom button
  captions are not available, so the message text names the Yes/No
  mapping. FlaUI drives the VM delegates instead of the native modal.
- **Boolean double-click**: the second click lands while
  write-in-flight and is refused with the spoken in-flight reason;
  the flipped draft stays visibly dirty until the publish re-renders
  the row. Honest containment, not lost input.
- **Header attach is activation-driven** (`SyncPanels`), so a
  markdown tab that has never been activated has no header VM until
  first activation — indistinguishable from lazy creation.
- **`RetryPropertyEditWithFreshHash` (Keep Mine)** is the one
  sanctioned non-snapshot hash use; it re-reads inside the write
  worker at the user's explicit instruction (contract 1's stated
  exception). Retries are OPERATION-SPECIFIC: a conflicted delete
  retries as a delete, a conflicted add re-issues its captured
  intent.

## Round-1 review resolutions (2026-08-03)

Adversarial round 1 (invariant prompt, xhigh) returned 13 findings;
all fixed in one batch. Additional accepted risks recorded from that
round:

- **Add sheet with no authoritative header data**: opening the sheet
  before the header's first publish (or on a load error) captures a
  null intent — every Add refuses with "No note is loaded." until
  the sheet is reopened. Inert-but-total beats a mutable intent.
- **Per-tab containment speech**: a property write that lands under
  a dirty same-path duplicate tab speaks the property-stale
  containment once per affected tab (each tab's state changed);
  contract 9's "one per outcome" governs the WRITE outcome
  announcement, which stays single.
- **Well-formed dates edit via the calendar only**: WPF's DatePicker
  parses typed text on focus loss, which would turn a focus change
  into a disk write (contract 2); the text portion is read-only and
  calendar picks commit immediately. Malformed stored dates keep the
  raw TextBox with Enter/Save commits.
- **Unknown future property kinds** decode/re-encode as text — a
  version-skew type-loss risk outside contract 10's enumerated
  kinds; tracked with #1078 (version-skew fencing).
- **Edited list elements convert along their source kind** when the
  new text still parses (a re-typed date stays a date), else become
  text — the sanctioned explicit conversion; untouched elements
  re-encode their decoded source verbatim.
- **RenameReloadFailed vs PropertiesReloadFailed**: after a rename,
  tab-BUFFER reload failures aggregate into one RenameReloadFailed;
  header refresh failures speak their own PropertiesReloadFailed at
  publish. Two events for two distinct failure surfaces.
