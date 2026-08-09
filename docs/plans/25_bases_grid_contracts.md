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
`summary`; `Groups` → `ComposeGroupHeading`. AMENDED (red-team round 1):
export does NOT ride the substrate's `exportProducer` — a synchronous
producer can neither run off-dispatcher (INV-6) nor carry the C14 scope
prompt, so the menu export/copy commands own `BaseExport` end-to-end and
the substrate's export commands stay disabled on Bases grids (G29 closes
through the menu route).

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
visible Bases surfaces (tabs, dock, dashboards, embeds), preserves selection
by `(FilePath, TaskOrdinal)` (the surface reconciles the selected row to the
same note-row after each publish; grid position rides the substrate's
re-`Bind` restore), and posts deduped `BasesRefreshUpdated` ONLY for
surfaces whose row-membership multiset changed. Failure leaves previous rows
+ handle in place and publishes a degraded banner — never a blank pane.
Delivery notes (red-team round 1): the in-app CELL-WRITE funnel reaches the
tab registry, the dock, and dashboards directly; everything else (property
panel, task toggles, editor saves, external edits) reaches Bases through the
VAULT-EVENT arm — a 500 ms-debounced silent re-execute of every visible
surface on any `.md`/`.base` change (`.base` definition changes reload their
own document). Reading embed cards re-project on any `.md` change while a
base card is published (a base's membership cannot be enumerated as a
dependency list); the artifact digest makes a no-change re-project a memo
hit. Announcements belong ONLY to the in-app write funnel (INV-4).

**C10 — Embeds are read-only and layered (G28).** A `.base` embed in the
reading view renders NO live grid inside the FlowDocument (an embedded grid
is blank in say-all — the W3-1/G23 finding). The embed card carries, in the
text range: the embed header, core's `AudioSummary` line, and warnings; plus
a focusable "Open base" affordance that opens the real surface (tab for
file-backed, or the grid window shape proven by
`ReadingTableWindowFocusesFirstCellAndEscapeReturns`). Read-only hints use
mac's wording per embed kind verbatim. Recorded divergence D-15 (mac shows a
live embedded grid; Windows layers, per G28). Inline fences (```base,
```slate-query, ```dataview) are recorded divergence D-20: v1 renders them
as readable SOURCE code blocks (the W3-4 pipeline), not executed cards.
When the fence surface ships it must classify through core
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
reopening / failed / missing states expose one canonical disabled reason.
RESOLVED (was "to verify"): the reasons are LABELS, not announcements —
mac posts no availability announcement for them post-#969, so Windows
delivers them as static control hints (`HelpText`, the TaskStatusPhrase
label category) while commands gray out through the shared CanExecute.
While unavailable: no sort affordance, no edit entry, no filter focus; the
header shows the recovery action (Retry) in place of Refresh. Every
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
**INV-6** The UI thread never blocks on FFI whose cost scales with vault or
result size, or that can wait on the per-document FFI lock behind an
execute: `base_execute`, `base_export`, `base_apply_edit(s)`,
`base_view_edit_query_json` all run off-dispatcher with cancel tokens where
the FFI accepts one. SCOPE REFINEMENT (red-team round 1): bounded registry
metadata CRUD (saved-query/dashboard list/get/save/rename/delete, the
parse-only DQL seed) runs synchronously on the dispatcher — it takes no
document lock and its cost is a small registry file, so moving it would buy
latency only at the cost of announcement-ordering complexity. If any of
these ever grows data-proportional, it moves behind the scheduler.

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
- **D-17** `![[x.base#ViewName]]` anchored embeds: mac selects the named
  view; Windows renders the existing unresolved card. Core's
  `parse_embed_target` treats `#` as a heading anchor and a `.base` file
  has no sections, so honoring the view name host-side would mean
  re-implementing target parsing (decision 4). Fix belongs in core (an
  embed-resolution variant for bases); until then the unanchored form
  carries the full experience.
- **D-18** The Windows v1 builder is EXPRESSION-FIRST: condition rows
  are raw expressions validated through `ValidateBaseExpression` (the
  representation mac itself fails closed into), with one-level groups;
  structured property/operator/value pickers are follow-up presentation.
  Column-selection and group-by editing are not in the builder (they are
  not in the command matrix); they remain authorable in the `.base`
  file. Existing view filters enter as ONE preserved opaque row
  (core's Expr JSON has no text-rendering API): it can be kept
  verbatim or deleted, and save-to-view refuses to combine it with new
  rows (semantics are never silently rewritten — the C11 rule).
  Query-JSON provenance: the new-query seed is CORE-produced
  (`OpenDql` → `BaseViewQueryJson`), edits mutate that document at the
  JSON-node level, and every filter node is assembled from core-encoded
  `ExprJson` — the host never synthesizes schema.
- **D-19** Missing dashboard sections repair through the dashboard EDITOR
  (mac also offers inline per-section repair actions); the section banner
  points there.
- **D-20** Inline query fences (```base, ```slate-query, ```dataview) render
  as readable source code blocks in v1 — the tab, dock, dashboard, and
  `![[x.base]]` embed surfaces carry the executed experience. Executing
  fences in-range is follow-up work and must go through core's
  `ClassifySlateQueryFence` family (C10). Rationale: nothing is silently
  absent (the fence text stays fully readable in say-all), and G28 already
  rules out a live in-range grid, so the marginal v1 value is a summary
  card; deferred over destabilizing the W3-4 code-block pipeline late in
  the milestone.

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
- **R6** Dashboard sections execute SERIALLY inside one load body and the
  surface rebuilds wholesale per publish — accepted at dashboard scale
  (sections are few and bounded by the editor); revisit only if a real
  dashboard exceeds it.

## Verified during implementation (both flags resolved)

- Mac's four availability reasons post-#969 are LABEL/HINT copy, not
  announcements — resolution recorded in C13.
- `SetProperty` did NOT trigger any Bases refresh by itself: no vault-event
  arm existed until red-team round 1. The funnel is now explicit for cell
  writes and the vault-event arm (C9 delivery notes) covers every other
  write path, including tabless `SetProperty` from other surfaces.
