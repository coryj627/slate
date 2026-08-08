# W4-6 Bases grid + builder — contracts

Issue [#738](https://github.com/coryj627/slate/issues/738) · spec `18_windows_port/specs/w4_spec.md` §W4-6 · precedents: `22_property_panel_contracts.md` (W4-4), `23_citation_surfaces_contracts.md` (W4-5), `24_red_team_protocol.md`.

Written BEFORE implementation, per the red-team protocol. Contract numbers are
stable identifiers for review prompts; do not renumber.

## Scope (N parity, exactly)

`.base` open-as-tab; table + list views + the fallback banner; transient
quick filter (Ctrl+F grid-scoped); in-grid property editing; `.base` embeds
(owning the transferred W3-5 rows); builder + advanced-expression authoring;
saved queries / dashboards / dock (`queries` + `basesDock` leaves,
`savedQuery`/`dashboard` tab kinds); CSV/Markdown export and copy.
`BasesResultSet`/`audio_*` artifacts consumed as-is.

Out of scope (recorded, not silently dropped): `.base` file-type registration
(#753 W8-3), `docs/help/bases.md` (W8-6), tab-kind matrix table (all #722).

## Contracts

**C1 — Core executes, the host renders.** Every query execution, filter,
sort, group, export, and announcement string is produced by core
(`base_execute`, `base_export`, `a11y_render`, `audio_summary`,
`audio_description`). The C# layer contains view models, UIA plumbing, and
marshalling only (program decision 4). No C# reimplementation of value
formatting: cells render `BasesValue.Display`, sort uses core's transient
sort, never an ICollectionView sort.

**C2 — One grid substrate.** Every Bases grid (tab, embed window, builder
preview, dock, dashboard section) is a configuration of `AccessibleDataGrid`
(program decision 13). `BasesColumn.Role == Primary` → `IsRowHeader`;
`BasesRow.AudioDescription` → `rowAudioDescription`; `AudioSummary` →
`summary`; `Groups` → `ComposeGroupHeading`; `BaseExport` → `exportProducer`
(the first grids with export enabled — G29 closes for Bases).

**C3 — Tab identity and lifecycle.** `.base` paths route to the Bases arm at
open (never the markdown loader). One `BaseDocumentViewModel` per source key,
shared by every tab on that source, registered in the workspace and disposed
before the session (`CloseBase` exactly once per open handle; `CloseBase` is
tolerant but the VM must not double-close or leak). Same-path `.base` tabs
dedup by byte-exact path (mac `BaseExactIdentity` twin — ordinal string
comparison, never culture-aware).

**C4 — View resolution.** Renderer mode = per-tab transient override else
`view_type == "list" ? list : table`. Content shape: no rows → empty
placeholder "No base results."; no columns → row-only list regardless of
renderer; else tabular. `BaseViewStatus.Fallback`/`Error` and non-empty
`ViewError`/`Warnings` render as informational banners ABOVE content that
still renders (mac wording verbatim: "Using fallback view for {name}.",
"View {name} has errors.", "No executable base views were found."). Banners
are captions with accessibility labels, never dialogs, never focus steals.

**C5 — Quick filter is transient, four ways.** Filter text lives only on the
document VM and is passed as `base_execute`'s `quickFilter` argument — never
written to the file; cleared on load, on view switch, on tab open/replace,
and on activating a different tab. Debounce 150 ms. Escape clears only when
the field is focused or a filter is active, and returns focus to the grid.
Announce `BaseQuickFilterResult` after apply; the field's accessible name
carries the transiency ("Quick filter — temporary, does not change the
base"). Ctrl+F reaches it ONLY when a Bases grid is the focused surface, via
`AccessibleDataGrid.FilterRequested` (with no subscriber the gesture
continues routing to app find).

**C6 — Sort is transactional and single-slot.** Transient sort =
`BaseSetTransientSort` + re-execute. If the re-execute fails, the engine
sort is rolled back and the published sort state does not change; if the
rollback itself fails, the document DETACHES (handle closed, terminal failed
state) rather than ever rendering rows that contradict the announced sort.
While detached, column headers expose no sort affordance (no clickable/UIA
sort pattern). `BaseSortedByColumn` (no trailing period) / `BaseSortSavedToView`
(trailing period) verbatim. Save-sort-to-view = `SetSlateSort` edit + clear
transient + reload views + re-execute.

**C7 — Cell editability is core-derived policy, one predicate.**
`BaseCellEditPolicy` twin: `note.`-prefixed id → suffix key; any other
dotted id → read-only; role must be Metadata/Primary; else the bare id.
Read-only reason = `BasesCellReadOnly(fileMetadata:)` rendered once — the
same event is the column's static hint (label) and the refusal announcement
(notification); they cannot drift. Draft validation produces the five
`BasesCellMustBe*` events; failure re-arms the editor with the draft intact.
Empty/whitespace draft = delete the property.

**C8 — Cell writes follow the W4-3 route split.** A Bases row targets an
arbitrary vault file. If that file is open in a markdown tab, the write goes
through the tab write seam (W4-4 shape: lease, refusal gates for dirty/stale
tabs, CAS hash, rebaseline); if not, it writes the session directly with no
expected hash (the W4-3 `NoOpenTab` precedent, matching mac's
`expectedContentHash: nil`). Either way exactly one terminal announcement:
`BasesCellSaved`/`BasesCellCleared`/`BasesCellRowNoLongerMatches` on success
(row-membership re-checked after refresh), `BasesCellEditFailed` on failure.
DIVERGENCE D-14 records the tab-gated half (mac does not gate on dirty tabs).

**C9 — One post-write refresh funnel.** Every in-app write that can change
Bases results (cell write, sort-to-view, builder save, property/task edits
from other surfaces) routes through one refresh entry point that re-executes
visible Bases surfaces (tabs, dock, embeds), preserves selection by
`(FilePath, TaskOrdinal)` + exact column id, restores the reader's grid
position via the substrate's re-`Bind` restore, and posts deduped
`BasesRefreshUpdated` ONLY for surfaces whose row-membership multiset
changed. Failure leaves previous rows + handle in place and publishes a
degraded banner — never a blank pane.

**C10 — Embeds are read-only and layered (G28).** A `.base` embed in the
reading view renders NO live grid inside the FlowDocument (an embedded grid
is blank in say-all — the W3-1/G23 finding). The embed card carries, in the
text range: the embed header, core's `AudioSummary` line, and warnings; plus
a focusable "Open base" affordance that opens the real surface (tab for
file-backed, or the grid window shape proven by
`ReadingTableWindowFocusesFirstCellAndEscapeReturns`). Read-only hints use
mac's wording per embed kind verbatim. Recorded divergence D-15 (mac shows a
live embedded grid; Windows layers, per G28). Inline fences (```base,
```slate-query, ```dataview) classify through core
(`ClassifySlateQueryFence`, `OpenBaseInline`, `OpenDql`) — never string
sniffing; the paragraph/embed-key detection is core's reading blocks.

**C11 — Builder edits are minimal diffs.** The builder (in-window overlay,
never a WPF Popup — W4-5 D-1) round-trips the view's query JSON and emits
only changed keys as `BaseEdit`s on save-to-view. Structured conditions that
cannot round-trip a property's kind downgrade to advanced rows preserving
the original filter JSON verbatim — semantics are never silently rewritten.
Live preview: 300 ms debounce, generation + session + cancel guarded,
`OpenQuery` → `BaseExecute(view 0)` → ALWAYS `CloseBase`; non-empty
`ViewError` publishes failed, not ready. Preview announcements are the
`BaseQueryPreview*` events. Advanced expressions validate through
`ValidateBaseExpression` (never throws); empty input shows no error.

**C12 — Saved queries / dashboards / dock parity.** `queries` leaf (saved
queries + base files + dashboards, pinning, rename/delete/export with the
canonical events), `savedQuery`/`dashboard` tab kinds over the existing
`WorkspaceItemKind` plumbing, dashboard editor overlay (sections with
saved-query picker, heading/view overrides, missing-section repair with
stale-section guard → `BasesDashboardSectionStale`), dock leaf following the
active note (500 ms debounce, identity-guarded against rename-as-retarget,
membership-baseline publish rules). Ephemeral query handles are never
edited. Export of saved queries is exclusive-create; out-of-vault targets
refuse with `BasesPathOutsideVault`.

**C13 — Availability gates fail closed and announce once.** Loading /
reopening / failed / missing states expose one canonical disabled reason
used for BOTH the control hint and the command refusal announcement. While
unavailable: no sort affordance, no edit entry, no filter focus; the header
shows the recovery action (Retry / Refresh) in place of Refresh. Every
menu/keyboard path that can reach a disabled action re-checks admission at
dispatch (the backstop), not only at UI-enable time.

**C14 — Export and copy consume core bytes.** CSV/Markdown export and
copy-as-Markdown call `BaseExport` and deliver core's string unmodified
(CRLF and quoting are core's). Delivery goes through the save-panel write
seam with the structural gates; success/failure announce the canonical
`BasesView*` events. The quick-filter scope prompt (filtered vs all rows)
is a modal choice whose cancel announces `BasesQuickFilterChoiceCanceled`.

**C15 — Commands, chords, matrix.** All 24 static `slate.bases.*` commands +
the dynamic `savedQuery.run.*` family are registered palette commands with
mac's labels/hints verbatim, section `bases`, disabled-reason wiring per
C13. Mac binds no global chords; any Windows chord is a recorded G18
divergence and must pass the collision test. Grid-scoped gestures live on
the substrate's RoutedCommands (Ctrl+F, Ctrl+Alt+S). chords.json gains the
`bases` delivery group; `generate-parity-matrix.py` gains `#738`,
`W4_DELIVERED_COMMANDS`, and both `LEAF_DELIVERED` entries; `w_c_matrix.md`
gains the Bases row.

## Invariants

**INV-1** No `BasesResultSet` field is transformed before display — `Display`
strings, `AudioSummary`, `AudioDescription`, group labels, warnings, and
`ViewError` reach UIA byte-identical to the FFI payload.
**INV-2** Every opened handle is closed exactly once, including every
failure path and preview generation (balanced open/close is testable by
counting through a session probe).
**INV-3** A republished result never leaves a dangling selection, edit
request, or sort affordance pointing at a row/column id that is no longer
present.
**INV-4** No Bases surface ever announces on segment/tab switching alone
(§2.6 posture: reveal announces the leaf/tab, content does not speak
unsolicited).
**INV-5** Background work never publishes into a different vault session
than the one that started it (session identity re-checked at every
publication point; the W4-5 lesson).
**INV-6** The UI thread never blocks on FFI: `base_execute`, `base_export`,
`base_apply_edit(s)`, saved-query/dashboard CRUD all run off-dispatcher with
cancel tokens where the FFI accepts one.

## Recorded divergences (Windows vs mac)

- **D-14** Cell writes to files open in a DIRTY tab refuse with the W4-4
  wording (mac writes regardless). Rationale: the Windows tab model already
  promises "your unsaved buffer will not be clobbered" (contract 8, W4-4);
  breaking it for Bases would be surprising in the shell where tabs are
  primary.
- **D-15** `.base` embeds layer (card + open affordance) instead of live
  embedded grids — G28; say-all integrity outranks visual parity.
- **D-16** Mac's ⌘F router picks the Bases filter only when the editor
  region owns focus; Windows scopes Ctrl+F by WPF focus within the grid
  (the substrate's existing gesture), the closest native analog.

## Accepted risks (off-limits for review re-litigation)

- **R1** No pagination on the FFI: a pathological base marshals every row.
  Mitigated by Standard-mode virtualization + the 10k-row conformance probe;
  matching mac, no host-side row cap (core's quick filter re-executes, it
  does not host-filter).
- **R2** `base_export` is not cancellable (fresh token inside core) — a huge
  export blocks its background thread, not the UI.
- **R3** The dashboards/dock refresh fan-out re-executes each visible
  surface once per in-app write funnel pass (no cross-write coalescing
  beyond the funnel's own scheduling), as on mac.
- **R4** `bases_generation` is not exposed; external-change staleness is
  handled by the existing vault-event refresh triggers, not a cheap probe.
- **R5** Warnings cross the FFI as flat strings (no structured kinds) and
  render verbatim.

## To verify during implementation (flagged, not assumed)

- How mac posts the four availability reasons post-#969 (they are not in
  the canonical Bases vocabulary list — likely label/hint only, or the
  mutation-announcement funnel). Match whatever mac actually does.
- Whether `SetProperty` on a tabless path triggers the vault-event refresh
  that makes C9's funnel fire without an explicit call.
