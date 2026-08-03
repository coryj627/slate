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

## Round-2 review resolutions (2026-08-03)

Round 2 (same invariant prompt) returned 6 findings + 3 below-bar;
all fixed in one batch. Contracts 1, 4, 5, 6 and core INV1–4 were
not falsifiable. Additional resolutions and accepted risks:

- **Bulk-rename runs are strictly serialized**: WorkInFlight gates
  and a runtime guard prevent any run from starting while another is
  in flight; independently, an APPLY report's tab reconciliation and
  summary announcement are UNCONDITIONAL in the publish path — a
  landed apply can never be stale-discarded by shutdown or a later
  request.
- **Drafts are parked across authoritative refreshes** (the mac
  parked-draft posture): every accepted publish re-applies surviving
  dirty drafts by key onto the rebuilt rows (fresh baseline and
  hash); completions commit the DISPATCHED draft, so mid-flight
  edits stay honestly dirty against what disk actually received.
  Accepted risk: a parked draft whose stored KIND changed externally
  rides on a row whose editor mode reflects the new stored value.
- **Typed list elements and the shipping transport**: core emits
  Text, Date, Datetime, and Wikilink as the SAME YAML string node
  and re-classifies by shape on read (property_value_to_yaml,
  frontmatter.rs) — element kind is a derived property of the text,
  so preserving the text preserves the kind at the byte level. The
  parse-path's plain-string elements decode as Text sources and
  re-encode byte-identically; the tagged DB representation keeps
  richer sources when present. Pinned end-to-end by
  EditingOneListItemLeavesTypedSiblingsByteIdenticalOnDisk.
- **The property-write lease is NOTE-scoped** (workspace-owned,
  path-keyed), not per visual tab: duplicate same-path tabs share
  one lease and closing the dispatching tab cannot free the note
  mid-flight. Released by the wrapped terminal completion.
- **Vault teardown cancels the rename worker**: workspace disposal
  cancels in-flight bulk-rename work via the CancelToken and shuts
  the sheet's scheduler down before tabs and session go; per-file
  CAS makes the already-landed writes individually safe, and the
  publish path's disposed-tab guards contain the rest. Accepted
  risk: the cancellation partition has no UI to land in after a
  vault close — the disk state is reported by core's Cancelled
  entries on the next open.
- **One reload action, one announcement**: the reload outcome is
  spoken by the FIRST same-path header only; peer duplicates refresh
  silently. Accepted risk: two reload requests racing before a
  publish coalesce into one completion announcement — one final
  state, one announcement.
- Below-bar fixes: list equality ignores the transient Edited flag;
  bulk-rename arming compares TRIMMED keys; an edited typed list
  element keeps its kind only when the new text still fits it, else
  degrades to Text (byte-identical emission either way).

## Round-3 review resolutions (2026-08-03)

Round 3 returned 2 mediums (contracts 1–8 and core INV1–4 not
falsifiable) plus a below-bar list of vacuous regressions; all
addressed:

- **The add sheet gained an "Initial value" field** (Windows
  addition, recorded divergence): core classifies stored values by
  SHAPE, so shape-derived kinds require a shape-valid seed or the
  authoritative re-read would reclassify them — date/datetime
  validate their shape, wikilink requires a non-empty bracketless
  target, tag_list requires a seed tag (an empty tag list has no
  #-shape and would re-read as list), number/boolean parse or
  default. Every advertised kind is pinned add→disk→authoritative
  re-read by a parameterized fact.
- **Reload announcement ownership is per-REQUEST**: the announce
  mode (None / FailureOnly / SuccessAndFailure) is captured with the
  refresh request and consumed at its own publish — duplicate
  headers refresh silently on failure too, and a discarded stale
  publish can never migrate an announcement to a later path.
  Post-write funnels are FailureOnly; the explicit Reload is
  SuccessAndFailure; initial loads are silent (the LoadError region
  is the surface).
- Vacuous regressions strengthened: duplicate-tab lease exclusion
  with the dispatching tab closed mid-flight; duplicate-header
  failed-reload single announcement; whitespace arming; the
  kind-fitting encode guards proven to bite (including the tagged
  wikilink decode branch); disposal shutdown pinned behaviorally.
- Accepted risk (recorded): a dead application dispatcher skips the
  wrapped completion and retains the note's write lease — terminal
  app teardown only, when no further writes can be issued.

## Round-4 DESIGN PASS (2026-08-03)

Contracts 9 and 10 produced findings in rounds 2, 3, AND 4 — the
trajectory rule fired, so round 4 was resolved as a design pass
addressing the ROOT causes rather than the instances:

- **Contract 10's root cause was classifier mirroring**: the host
  kept hand-porting core's key/shape classification and every gap
  was a finding. Structural resolution: (a) the integral-float flip
  (`1.0` → `1`) was a CORE emitter bug — core's own round-trip test
  now pins `Float(1.0)`/`Float(1e3)` and the emitter appends a
  decimal marker; (b) the add sheet's validation is key-aware
  (`tags` is always a tag list) and shape-aware (datetime requires a
  time component; `#`-seeds refuse under list; date/datetime/
  wikilink-SHAPED text refuses — while YAML-scalar shapes like
  true/123/1.5 stay text because core quotes them); (c) the
  contract itself is pinned by an either-refuse-or-match matrix
  fact run against real core for every (key, kind, seed) — future
  classifier drift in either direction fails the matrix instead of
  waiting for a review round.
- **Contract 9's root cause was multi-entrant refresh scheduling**:
  conflict completions scheduled a refresh AND handed the resolution
  dialog callbacks that scheduled their own, so the second request
  superseded the first's announcement. Structural resolution: the
  RESOLUTION owns the follow-up exclusively — Keep Mine's completion
  refreshes, Reload refreshes with the announced mode, Cancel
  refreshes nothing ("the panel stays as it was", which is also the
  mac hint's literal contract). No other path schedules a refresh
  after presenting the dialog.
- Below-bar: the disposal regression now bites (a real CancelToken
  is asserted cancelled); wikilink newline targets refuse pre-core
  in both the sheet and the row editor with host copy.

## Round-5 resolutions (2026-08-03)

Round 5 confirmed contracts 1–9 unfalsifiable and found no
under-refresh path, but contract 10 fell again on two more
classifier-mirror gaps (case-insensitive `tags`, structural-vs-
calendar date validation). Four rounds on one contract means the
mirror itself was the defect:

- **CLASSIFICATION AUTHORITY MOVED TO CORE.** New pure export
  `round_trip_property_kind(key, value) -> Option<String>` runs
  core's own emit-then-classify round trip on a scratch document and
  returns the kind the value would ACTUALLY have — or None when core
  won't store it at all. The add sheet builds its candidate, asks,
  and refuses on mismatch. Every mirrored rule (tags casing, date
  shape, datetime time-component, `#`-prefixed lists, YAML-scalar
  text shapes) is DELETED from the host; the remaining host checks
  exist only to produce a better message than "would be stored as
  text". The matrix grew to 31 cases including all round-5
  counterexamples, and a core-side test pins the export directly.
- **Reload-from-disk now actually discards** (self-caught while
  writing the requested regression): round 2's universal draft
  parking silently preserved the very edit the Reload hint promises
  to discard. Parking is now skipped for the explicit reload flow
  (the SuccessAndFailure mode) — the deliberate discard round 2
  called for and did not implement. Cancel and no-op saves still
  park.
- Below-bar: edited wikilink list elements degrade to Text on CR/LF
  (matching the other kind-fit guards) rather than failing the whole
  list write.

**Ship posture:** contracts 1–9 unfalsifiable across rounds 3–5;
contract 10 is now enforced by core's own round trip instead of a
host copy, so the defect class behind four rounds of findings cannot
recur without failing core's test and the 31-case matrix at once.

## Round-6 resolutions (2026-08-03)

Round 6 falsified contracts 2, 6, and 10 (1, 3, 4, 5, 7, 8, 9 held).
Two were genuine data-loss paths that earlier rounds never reached:

- **Contract 6 — add could erase a nested container.** The duplicate
  gate read FLATTENED property rows, where `person: {name: …}`
  appears only as `person.name`; a flat `person` add therefore looked
  collision-free and the write replaced the whole mapping. Core gains
  a second pure export, `frontmatter_top_level_keys`, and the add
  intent captures THAT — the authoritative top-level YAML keys,
  including containers and shapes properties can't type. Pinned by a
  core test (properties genuinely omit the container) and a
  real-vault regression asserting byte-identical disk on refusal.
- **Contract 2 — reload discarded the wrong drafts.** Round 5's
  discard was mode-scoped, so an explicit Reload dropped EVERY dirty
  row on the announcing header (and never the add sheet's draft it
  was actually about). Discard policy is now separated from
  announcement ownership: the refresh request carries the specific
  key to drop, add conflicts drop the sheet instead, and peer headers
  keep their drafts. "Discard this property edit" is singular.
- **Contract 10 — the last mirror was a FALSE refusal.** Host
  calendar parsing rejected structurally-valid dates core stores
  happily (`2026-99-99`). Date/datetime candidates are now built
  unconditionally and core decides; host parsing only selects nicer
  WORDING once core reports a mismatch. The matrix now fails on false
  refusals too — a refusal is legitimate only when core would not
  have stored the chosen kind.
