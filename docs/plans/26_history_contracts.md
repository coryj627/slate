# W4-7 — Local history on Windows (#739): contracts

Scope (spec §W4-7, O parity — O shipped 2026-07-11, conditional in name
only): `Leaf.history` with two segments ("This note" + "Deleted"),
day-grouped version list, structured accessible diff (`StructuredDiff`
consumed, never re-derived), restore + Restore As…, changes-since-last-open
(opt-in), markers toggle, `.canvas`/`.base` history coverage (#797). One
command: `slate.history.showPanel` (no chord on mac; row actions are
deliberately not commands). Written BEFORE implementation; the divergence
and risk registers are deliberate and off-limits for review re-litigation.

## Contracts

**H1 — Core executes, the host consumes.** Every sentence, fragment,
count, and classification is core's, byte-identical: `VersionSummary`
(`AudioFragment`, annotation `Display` strings, `OpKind`, `ByteDelta`),
`StructuredDiff` (`SemanticDescription`, `Detail`, `AudioSummary`,
operation order), `ChangesSinceOpen` verdicts, and the four canonical
history announcements (`HistoryPanelShown`, `RestoredVersionFrom` — date
HOST-formatted before crossing the FFI, the corpus shape
"July 19, 2026 at 9:41 AM" — `RestoredFile`, `RestoredFileAs`). The host
adds only recorded static labels (the mac label inventory, §W-C class).
The diff is NEVER re-derived and NEVER rendered as a side-by-side text
dump (the mac §7.3 sequential-walkthrough rule).

**H2 — The leaf and its two segments.** The registered `history` leaf
(id already in the 16-leaf rail) gains a body: a segment group ("This
note" / "Deleted", accessible group name "History scope") on the W4-5
bibliography two-segment pattern. Segment switching never announces
(§2.6) and never eagerly re-queries; the Deleted segment lazy-loads on
first visit and reloads on later visits. Segment state is per-session,
not persisted (mac: per-mount). The generic placeholder body's exclusion
list gains `history`. Empty states verbatim: no note selected →
"Select a note to see its history."; no versions → "No versions yet.
Versions are recorded as you save."; deleted empty → "No recently
deleted files."; the Deleted segment always shows the footer "Files
deleted before Slate saved them go to the system Trash."

**H3 — The version list.** Source `ListVersions(path, Paging)`; newest
first; page 1 limit 50; a "Show older versions" button appends the next
cursor page; a cursor-generation bump (`InvalidArgument` "history
changed, restart paging") silently reloads page one. Row IDENTITY is
`PositionFromTail` (hashes repeat across A→B→A); content operations key
on `ContentHashAfter`. Rows render: absolute date (primary), relative
date (secondary), core `AudioFragment` (caption), annotation chips; one
accessible row name "{absolute date}, {AudioFragment}[, {annotations
joined ', '}]". MARKERS (`IsMarker` — anchors, rename markers, no-op
canvas records) are hidden by default behind an inline "Show markers"
toggle in the section header (not buried in a menu — the mac HIG
amendment); the toggle re-filters the already-loaded rows, no re-query.
The section header count is core's `TotalFiltered` (which counts ALL
ledger rows including markers — the mac comment claiming otherwise is
stale; core is authoritative).

**H4 — Day grouping.** Groups are CONSECUTIVE RUNS of visible rows
sharing a local-calendar start-of-day (never a re-sort: a backwards
clock legitimately yields two same-day sections). Group id =
"{dayStartUnixSeconds}#{firstRowPositionFromTail}" — stable across
pagination appends. Headers: "Today" / "Yesterday" / full local date;
collapsible per group with accessible expanded/collapsed state and the
name "{title}, {n} version|versions". Collapse state is per-session,
reset on note switch.

**H5 — The diff surface.** One rendering component (the mac
`DiffOperationList` twin): a header line carrying `AudioSummary`
verbatim; empty operations → "No differences."; else ONE row per
`DiffOperation` in core order, each row a single accessible element
named "{SemanticDescription}. {Detail}" (or the description alone when
`Detail` is null), children ignored. Two consumers: the since-open
section (H8) and Compare (H6). Diff computation runs off the dispatcher;
failures render the error message as an inline caption, never a dialog.

**H6 — Compare.** Per-row "Select for comparison" checkbox: at most two
selected; selecting a third drops the OLDER of the current two (higher
`PositionFromTail`); with exactly two, a "Compare selected versions"
button appears in the section header and renders the diff under it —
endpoints oriented older = from, newer = to. A per-row "Compare" button
diffs that version against the CURRENT content (the active tab's saved
content hash — the log tail after any save) and renders inline under
that row. Inline diffs reset on note switch, list reload, and restore.

**H7 — Restore.** Per-row "Restore…" captures AT STAGE TIME: path,
`ContentHashAfter`, the host-formatted date, the expected current
content hash, and whether the target's open tab is dirty
(capture-at-staging is load-bearing — the mac adversarial rounds).
Confirmation (injectable dialog seam; production modal): title "Restore
version?", message "Restore the version from {date}? This replaces the
current content of {filename}. The replaced state remains available in
version history.", Cancel / Restore. A DIRTY open tab refuses with the
D-14 sentence family BEFORE any disk touch (divergence HD-1). A clean
target calls `RestoreVersion(path, hash, expectedHash)` off-dispatcher:
success announces `RestoredVersionFrom` (High), reloads the open tab's
buffer from disk, reloads the version list, and moves focus to the NEW
HEAD row (position 0 — WCAG 2.4.3); `WriteConflict` refuses with the
staleness dialog seam and reloads the list (the CAS is core's half of
the same contract); `HistoryUnavailable` refuses with "This version
can't be restored: its history failed an integrity check." History is
never rewritten — undo of a restore is another restore.

**H8 — Changes since last open (opt-in).** A HOST preference
(AppPreferencesStore field, default OFF) toggled from a checkable menu
item. With the pref on, ONE serialized funnel per note activation:
`ChangesSinceLastOpen(path)` FIRST, publish under the standard guards,
then `MarkOpened(path)` only after a successful publish (marking first
would always report Unchanged — the pinned core order). With the pref
off: neither call, and turning it off clears the section. The section
(top of "This note"): header "Since you last opened" + the H5 list for
`Diff`; one caption "Earlier changes have been compacted." for
`BaselineCompacted`; NOTHING for `NoBaseline`/`Unchanged`. No badge, no
announcement, no shortcut — the section is the entire surface.

**H9 — Restore As… (live versions).** Per-row "Restore As…" composes
`VersionContent` + `CreateExclusive(destination)` (no live-file
restore-as FFI exists — the mac composition). The destination prompt is
an INLINE row in the leaf (divergence HD-2): seeded with
"{stem} (restored).{ext}", carrying the mac prompt copy ("Save a copy
of the version from {date} to a new file."), Enter commits, Escape
cancels, focus returns to the anchor row. `DestinationExists` refuses
with "A file already exists at {path}. Choose a different name." and
keeps the row open. Success announces `RestoredFileAs` ("version from
{date}", filename), refreshes, and opens the new file. AMENDED (red
team round 1, verified against mac source): the original "counter-
suffixed on collision" clause was contract-text drift — mac's
`restoredCopyPath(existing:)` counter runs only when an `existing` set
is passed, and BOTH production call sites pass none, so neither
platform suffixes in production; collisions surface through the
exclusive-create refusal on both. The Windows seed matches mac's
actual behavior.

**H10 — Deleted-file recovery.** `ListDeletedFiles` (single page, limit
200, no pagination UI — mac parity). Rows: path, "Deleted {relative}"
or "Deletion time unknown", byte size when recoverable; accessible name
"{path}, {deleted text lowercased}, restorable|not restorable". The
Restore button exists only for `Recoverable` rows and calls
`RecoverDeletedFile(path)` — recovery to the ORIGINAL path is primary;
`DestinationExists` routes into the H9 inline row with the
deleted-file message ("A file already exists at {path}. Restore the
deleted file to a different location.") and `RecoverDeletedFileAs`.
Success announces `RestoredFile` and refreshes both the tree and the
deleted list. Files never saved through Slate are honestly absent (the
footer sentence covers them). DELIVERY NOTE (verified against core):
the remnant set is built at RECONCILE (scan time) — a file deleted in
the CURRENT session enters the Deleted list at the next vault open,
identically on mac; the segment is a recovery surface, not a live
deletion feed (core-owned semantics, off-limits).

**H11 — Command + reveal.** `slate.history.showPanel` ("Show History
Panel", View menu, no chord): un-hide the right pane, activate the
leaf, announce the canonical `HistoryPanelShown` ONLY on an actual
switch (the mac rule; the setter's generic `LeafPanelShown` pairs with
it — the W4-3 OpenTasksReview precedent), move focus to the right-pane
boundary. The leaf reveal hook refreshes idempotently (never a
whole-vault query).

**H12 — `.canvas`/`.base` coverage (#797).** No special-casing: both
route through the same save seam core-side, so listing, verified
content, diff, and restore behave identically; canvas transitions fold
into one row with the "Canvas: {action}" annotation, and a standalone
semantic record renders core's "canvas action" fragment. The host must
not filter by extension anywhere in the history surface.

**H13 — Compaction failure.** The `CompactionFailed` vault error event
relays core's composed message ("Slate couldn't compact the edit
history for {path}: {cause}. History for this file may grow
unbounded.") as a Medium announcement, once per path per session
(divergence HD-4: no modal, no suppress pref).

**H14 — Close-out.** chords.json gains the `history` delivery group
(implementation + test anchors, command→group, "#739"→group);
`generate-parity-matrix.py` gains `W4_7_STATUS`/`W4_7_COMMANDS`
(`{"slate.history.showPanel"}`), the folds, `expected_issues` "#739",
and a `history` LEAF_DELIVERED sentence naming checkable evidence;
`w_c_matrix.md` gains the history row; a FlaUI journey covers the leaf
(reveal via the command's menu item, version rows after real saves, the
markers toggle, a compare diff, the Deleted segment, axe scans),
honoring the recorded journey traps (foreground re-assert, Value
pattern, async Invoke settle, panel-no-peer).

## Invariants

**HINV-1** No history sentence is composed host-side beyond the recorded
static-label inventory; core strings reach UIA byte-identical.
**HINV-2** Row identity is `PositionFromTail`; content operations use
`ContentHashAfter`; nothing indexes versions by hash.
**HINV-3** Fail closed: `HistoryUnavailable` renders the integrity
refusal; wrong or partial content is never shown or written.
**HINV-4** No unsolicited speech: segment switches, list loads, diff
publishes, and since-open publishes are silent; only the four canonical
events, the generic leaf reveal, and the H13 relay speak.
**HINV-5** Every publish re-checks session identity, generation, and
byte-exact (Ordinal) path; loads per note are serialized.
**HINV-6** The UI thread never blocks on history FFI — every call's
cost is proportional to the op-log length and none is cancellable, so
all of them run on the scheduler.
**HINV-7** All writes go through core (`RestoreVersion`,
`RecoverDeletedFile[As]`, `CreateExclusive`); the host never writes
files.
**HINV-8** `MarkOpened` never precedes the since-open publish for the
same activation, and neither call happens with the pref off.

## Recorded divergences (Windows vs mac)

- **HD-1** Restoring over a DIRTY open tab refuses with the shared D-14
  sentence family instead of staging mac's save-conflict alert. The
  Windows tab model already promises "your unsaved buffer is never
  clobbered" (W4-4/W4-6 precedent); the mac flow abandons the restore
  into a Keep-Mine/Reload resolution anyway — the refusal reaches the
  same protective outcome through the established Windows surface.
- **HD-2** The Restore As… destination prompt is an INLINE leaf row
  (the queries-leaf rename-row pattern) instead of mac's text-field
  alert: same seeded suggestion, same exclusive-create semantics, same
  refusal wording; the inline row keeps focus management local and the
  flow testable headless. REFINED (red team round 1): the row's whole
  state (identity hash/source path, notice, draft, refusal) is STAGED
  in the VM at open — a republish re-renders it rather than orphaning
  view elements, a reload can never re-target the open row to a
  different version, a refusal is made perceivable by focusing the
  named refusal element (no live region exists on this surface by
  design), and Escape refocuses the anchor row.
- **HD-3** The retention setting (mac Settings ▸ History picker,
  30/90/180/365 days) ships NO Windows UI in W4-7: retention is not in
  the spec's W4-7 sentence, core's default (90 days) governs, and the
  `HistoryPrefs` FFI remains bound for a follow-up settings surface.
- **HD-4** Compaction failure is an announcement relay (Medium, once
  per path per session), not mac's modal + "Don't Show Again" pref: the
  message is fully core-composed, and a modal for a background
  maintenance failure is the interruption mac's suppress pref exists to
  avoid.
- **HD-5** The diff rows omit mac's decorative class-family icons
  (accessibility-ignored there); the accessible surface is identical.

## Accepted risks (off-limits for review re-litigation)

- **HR-1** Every history read reconstructs from the whole op-log (cost
  ∝ log length, no cancellation) — mitigated by off-dispatcher
  execution, per-note serialization, and page-1-limit-50.
- **HR-2** There is no "version created" event; the visible list
  refreshes on note saves and vault file events for the active path,
  not on op-log appends.
- **HR-3** The deleted list is a single 200-item page (mac parity).
- **HR-4** `Detail` truncation (200 chars) and description truncation
  (60 chars) are core's; the host renders them as-is.
- **HR-5** The since-open baseline degrades honestly (`NoBaseline` /
  `BaselineCompacted`) after cache rebuilds or compaction; the host
  never guesses.
- **HR-6** `DeleteFile` (outside this surface) routes through the
  system trash, whose COM init requires an STA thread — the app's UI
  thread; no HISTORY flow calls it (restore/recover use save-machinery
  and exclusive-create only), so history work stays pool-schedulable.

## To verify during implementation (flagged, not assumed)

- Whether the restore-success tab reload can ride the existing
  full-reload path without disturbing unrelated tab state (mode,
  reading view) beyond what mac's `loadCurrentNote` also resets.
- The exact `WriteConflict` refusal presentation for restores (dialog
  seam wording) — mac re-raises its conflict alert; Windows has no
  history-specific conflict event.
