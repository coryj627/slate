# W5-2 — The vault search overlay (#742): contracts

Scope (spec §W5-2): the full-text-search overlay over `full_text_search`,
its result rendering, its announcements, and the §W-A golden extension.
Written BEFORE implementation per `24_red_team_protocol.md` §0. The
divergence (SD) and accepted-risk (SR) registers are deliberate
owner-recorded decisions and are **off-limits for review re-litigation** —
a reviewer may report that an entry is factually wrong, not that the
trade-off should be re-made.

Contract numbering is per-wave. "S3" here is unrelated to any other
document's contract 3.

## The finding that shapes this issue

**The issue's delivery spec asks for three things mac does not have.** The
spec text says the overlay needs "a scope selector covering exactly the
shipped scopes including tag scope", that "arrows move" through results,
and that "Esc closes and restores prior focus". Measured against
`apps/slate-mac/Sources/SlateMac/SearchOverlay.swift`:

| Spec asks for | mac ships |
|---|---|
| A scope selector | **No selector exists.** Tag scope is armed only by activating a tag in reading view (`ReadingLinkRouter.swift:243-258`); Folder scope is never constructed anywhere in the app. The overlay shows a read-only `Tag: {name}` chip with a clear button. |
| Arrow-key result navigation | **No arrow handling at all** (`SearchOverlay.swift` has no `onMoveCommand`, no arrow `onKeyPress`). Tab traversal only. The file's own header comment at `:28-29` claims arrows cycle; it is aspirational. |
| Esc restores prior focus | **Not implemented.** `SearchOverlay.swift:7-8` and `AppState.swift:9463-9464` both claim it; no first responder is captured and none is restored. Whatever AppKit does on teardown is what happens. |

A literal parity port would therefore ship **less** than the spec asks
for, and less than W5-1's palette already does on Windows. Each of the
three is resolved in the divergence register below rather than silently
inherited or silently improved.

**Find-in-note is not in this issue.** The parity matrix assigns
`slate.editor.findInNote` to #742, but it shares no surface with vault
search: mac implements it as AppKit's `NSTextView` find bar
(`NoteEditorView.swift:1128-1132`), and core's `SearchScope::File` is
reserved and unreachable (`search_db.rs:132-136`, `:194-196`). The Windows
equivalent was measured unusable as shipped — see the separate plan — so
it moves to its own issue and the matrix row is reassigned.

## Contracts

**S1 — Core searches, the host renders.** `full_text_search(query, scope,
cancel)` owns matching, ranking, snippet extraction and the result
summary. The host re-implements none of it. Specifically:

- Rows arrive **already sorted** by `bm25()` ascending, where lower is
  more relevant and values are negative (`search_db.rs:180`, `:191`,
  `:314`). The host never re-sorts and never presents `Score`.
- `QueryHit` carries exactly `path`, `snippet`, `score`
  (`search_db.rs:81-94`). There is deliberately **no line number** — it was
  removed in #92 item 1 because producing it means pulling `body_text`
  through SQLite per hit. The line shown after activation is derived
  host-side from the loaded note.
- `path` is **vault-relative with forward slashes on every platform**
  (`session.rs:297`). A Windows host that builds a `Folder` scope must
  convert `\` → `/` itself; core does not.

**S2 — The summary string is core's, consumed verbatim.**
`QueryResultSet.Summary` is composed by `search_db::summary_for`, which
renders **through the a11y vocabulary** (`search_db.rs:250-261` →
`a11y.rs:867-871`), so the displayed and the spoken string are one
template rather than two copies. This is the #969 residue conversion
completed by PR #1084 and it is the §W-D prerequisite this issue was
gated on.

The host therefore:
- **displays** `resultSet.Summary` verbatim, and
- **announces** by posting the typed `A11yEvent.SearchResultsSummary(count)`,
  which core renders from the same template.

Composing either string in C# is forbidden. There is no host-side
"{n} results" anywhere in this feature.

**S3 — Snippet markers are the host's to split, and may be malformed.**
Core wraps matched tokens in STX (``) and ETX (``)
(`search_db.rs:110-111`). The constants **do not cross the FFI** — no
`uniffi::export`, nothing in the generated binding — so the host hardcodes
both and cites that line.

Nothing in the repository guarantees the markers are balanced or
non-nested; core inserts them via SQLite's `snippet()` and never validates
the output. Both existing consumers tolerate imbalance deliberately
(`crates/slate-cli/src/commands/search.rs:115-118`,
`SearchOverlay.swift:453-480`). The Windows splitter is a **toggle state
machine that cannot throw**: a stray ETX clears emphasis, a dangling STX
leaves the tail emphasised, a nested pair is treated as a no-op. An empty
snippet is legal and expected — every empty-query tag-scope row has one
(`search_db.rs:377`).

**S4 — One accessible name per result row (the P6 rule, restated).** Each
row publishes exactly one UIA stop whose name is:

```
{basename}: {snippet with both markers removed}
```

matching mac's `rowAccessibilityLabel` (`SearchOverlay.swift:519-524`).
The emphasised runs are **presentation only** and contribute no stops —
they use `AutomationPresentationTextBlock`, whose peer returns false from
both `IsControlElementCore` and `IsContentElementCore`
(`AutomationLandmark.cs:98-108`). The full path is a tooltip, not part of
the name.

Note mac's own comment at `SearchOverlay.swift:511` documents this label
as `"{filename}, line {N}: {snippet}"`. That is **stale** — the code three
lines below emits no line number. The Windows twin follows the code.

**S5 — The FFI call is synchronous; the host owns the thread hop.**
`FullTextSearch` blocks (`lib.rs:1349-1358`). It must never run on the
dispatcher. The host follows the Quick Open shape
(`QuickSwitcherViewModel.cs:295-346`): capture the UI `SynchronizationContext`,
debounce, run off-thread, marshal back, and **discard a stale result**.

Staleness is checked on **four** independent things, because three of them
have each shipped as a bug on mac: the cancellation token is still the one
this call minted, the session is still the same session, the query is
unchanged, and the overlay is still open. The session check is not
theoretical — mac added it after a direct vault switch published vault A's
rows into vault B's overlay (`AppState.swift:8934-8949`).

**S6 — `Cancelled` is not an error.** A cancelled search returns
`VaultError::Cancelled`, and the host **leaves the panel exactly as it
was**: no state change, no summary, no announcement
(`AppState.swift:8963-8968`). Cancellation is a user action, not a
failure. The other reachable variants are `Unsupported` (File scope only,
unreachable from this UI), `InvalidQuery`, and `Db`.

`InvalidQuery`'s message embeds a raw SQLite string and its classification
is deliberately string-sniffed (`search_db.rs:396-414`). The host **never
parses it** and never shows it verbatim as guidance; it announces through
`A11yEvent.SearchFailed(message)`, whose `"Search error: {message}"`
template lives at `a11y.rs:872`.

**S7 — Debounce is 150 ms, trailing, and deliberately not deduplicated.**
Mac debounces at 150 ms on the main queue (`AppState.swift:8492-8497`)
with an explicit red-team decision **against** `removeDuplicates()`: its
memory is pipeline-lifetime, so re-arming the retained query after a
reopen emitted the same string and was silently swallowed, making the
re-arm dead code (`AppState.swift:8485-8491`). Announcement dedup lives at
the view instead, on the rendered string
(`SearchOverlay.swift:111-115`).

The Windows twin keeps both halves: no dedup on the query pipeline, dedup
on the announcement.

**S8 — Nothing is announced per keystroke beyond the count family.** The
only announcements this surface makes are `SearchNeedsVault`,
`SearchResultsSummary`, `SearchFailed`, `SearchResultOpened`, and the
focus-change `RowSelected`. The transition into the searching state is
silent. Verify against mac before treating any silence as a defect — the
recorded W4-5 lesson.

**S9 — Activation closes the overlay and is the only thing that records a
recent.** Enter on a row (or on the field, which activates the top result)
opens the target and closes the overlay. Recents are recorded **only**
here, never per keystroke (`AppState.swift:8614-8622`, `:9461`).

The query used for both the recent and the line lookup is the query that
**produced the visible rows**, not the live field text — the debounce
window means they differ (`AppState.swift:9445-9453`, mac's `lastResultsQuery`).

**S10 — Same tab by default.** `OpenTarget.CurrentTab` unless a modifier
selects otherwise (`AppState.swift:4240-4245`). Opening a file that is
already the selected file does not re-open it.

**S11 — Joining the modal-surface registry is mandatory, not optional.**
W5-1 made modal precedence a pure function over an exhaustive enum. A new
overlay adds a `ModalSurface` member at its correct paint position, a
field on `ModalSurfaceState`, an arm on `IsOpen` (omitting it is a **build
break** — CS8509 is promoted to an error, `SlateWindows.csproj:20`), an
arm on `DecidePaletteOpen`, a field read in `CurrentModalSurfaceState`,
and a scrim plus overlay declared in XAML paint order with no
`Panel.ZIndex`. `ModalSurfaceTests` gates every one of those.

**S12 — The overlay owns a text field, so it needs the text-editing
allow-list.** A blanket shell-chord swallow kills Ctrl+A/C/V/Z and
Shift-selection inside the overlay's own box, because WPF runs
`InputBindings` only for unhandled keys. The overlay routes through
`TextEditingChords.Allows` exactly as the palette does
(`ModalSurfaces.cs:194-258`), including its AltGr arm.

**S13 — §W-A goldens are extended, never replaced.** `search.json` today
pins three queries under `Vault` scope and serialises **rows only** —
there is no `summary`, no error case, no empty-query case
(`SurfaceSerializer.cs:934-965`). Any addition is made to the C# and Swift
serializers **in the same commit**, in the same key order; the canonical
JSON rules are in `CanonicalJson.cs:11-20`. Goldens are regenerated by
pointing the Windows harness's `--out` at the golden directory — there is
no bless flag.

**S14 — Recents match mac, file format included.** Owner decision
2026-08-16, reversing the draft's accepted risk: recents ship in this
issue. The store is a **vault artifact** — `<vault>/.slate/search-recents.json`
— and vaults move between platforms, so the Windows store must
interoperate with mac's `SearchRecentsStore`
(`SearchRecentsStore.swift:33-124`), not merely imitate it:

- The format is a JSON array of raw query strings, most-recent-first.
- `maxEntries = 20`, enforced on load (dedupe, first occurrence wins,
  short-circuit at the cap) and on add.
- `maxFileBytes = 64 KiB`; load reads one byte past the cap and treats a
  larger file as malformed.
- A missing, malformed, oversized or unreadable file degrades to an
  **empty list, never an error** — the overlay must open regardless of
  recents state.
- `add` is LRU: remove any equal entry, insert at front, cap, write
  atomically (temp-file-then-rename inside `.slate/`).
- `clear` persists an empty list rather than deleting the file, so the
  subsequent load path is identical.
- A recent is recorded ONLY on activation (S9), trimmed the way mac trims
  it (`.whitespacesAndNewlines`, `AppState.swift:8624`).

On Windows, reads and writes go through the W1 anchored-vault discipline:
`.slate/` files open fail-closed against external reparse points like
every other vault store. The recents store joins `sidebar.json` and
`workspace.json` under that hardening; it does not open the file naively.

The empty-query state therefore has two shapes, labels mac-verbatim:

- No recents: `Type to search.`
- With recents: a `Recent Searches` header (header trait), rows named
  `Recent search: {query}` whose activation re-runs that search and
  returns focus to the field, and a `Clear recent searches` button
  (hint: `Forgets every remembered search in this vault.`). Focus on a
  recent row announces through `A11yEvent.RecentSearchFocused`, which is
  already in the generated bindings.

## Divergence register (owner-recorded; off-limits for re-litigation)

- **SD-1 — Windows ships arrow-key result navigation; mac has none.**
  Up/Down move the selection, matching the W5-1 palette and Quick Open,
  both of which already do this on Windows. Matching mac's Tab-only
  traversal would make search the only list surface in the Windows app
  that arrows do not drive. Same shape as PD-1 in W5-1, and resolved the
  same way: ship it here, and file mac convergence so the divergence has a
  closing path rather than sitting open indefinitely.
- **SD-2 — Windows restores focus on Esc; mac does not.** The W5-1 palette
  restores to the pre-open element and skips the restore when the invoked
  command claimed focus elsewhere (`MainWindow.Palette.cs:302-355`). The
  search overlay reuses that logic verbatim. Mac's two comments claiming
  it does this are aspirational; Windows makes them true rather than
  copying the gap. Recorded as a divergence rather than a bug fix because
  it is a behavioural difference a reviewer will otherwise flag.
- **SD-3 — No scope selector, matching mac.** The spec's "scope selector"
  is NOT built. Mac has none, Folder scope is constructed nowhere in
  either app, and inventing a picker would ship a Windows-only affordance
  with no parity anchor and no core-side notion of which scopes are
  user-selectable. Tag scope is honoured when armed, and the overlay
  renders the read-only `Tag: {name}` chip and its clear button.

- **SD-4 — Reading-view tag activation is rerouted to this overlay.**
  This corrects an already-shipped divergence rather than creating one.
  Mac's reading-view tag click opens the search overlay in tag scope
  (`ReadingLinkRouter.swift:243-258`, ordered: clear query, open overlay,
  set scope). Windows routes the same gesture to the sidebar filter:
  `ReadingActivation.cs:69-70` → `WorkspaceViewModel.ActivateTagFromReading`
  (`:190-191`) → the shared `_activateTag` seam → `EditorTagActivated` →
  `FileSidebar.ActivateTag`.

  Without this reroute, tag scope would ship implemented, unit-tested and
  **unreachable** — code nothing can run. With it, SD-3's "no selector" is
  a complete design rather than a hole.

  Two things this must NOT disturb:

  - **The editor tag path stays on the sidebar.** Mac's editor is plain
    text with unclickable tags, so there is no mac twin for it; it is a
    Windows-only affordance either way. The shared `_activateTag` seam is
    therefore split — reading-view to search, editor to sidebar — and the
    comment at `WorkspaceViewModel.cs:185-186` claiming "one navigation
    path, one tag path" is updated to say why there are now two.
  - **The host-composed announcement.** `ActivateEditorTag` posts
    `A11yEvent.HostComposed($"Filtered files by tag {tag}.")`
    (`WorkspaceViewModel.cs:1768-1771`) — a §W-D residue string. The
    rerouted path must not keep speaking "Filtered files by tag" while
    opening a search overlay. It announces through the search vocabulary
    instead, and the residue marker count moves accordingly.

  Because this changes shipped W3 reading-view behaviour, it needs its own
  fact and a note in the W3 close-out record, not just a W5-2 fact.

## Accepted-risk register (owner-recorded; off-limits for re-litigation)

- **SR-1 — The sidebar loses its reading-view tag entry point.** SD-4
  moves that gesture to the search overlay. The sidebar's tag filter
  remains reachable from the editor tag path and from the sidebar's own
  filter box, so no capability is lost — but a user who learned the
  reading-view route gets a different surface than before. This is a
  deliberate correction toward mac, not an accident, and it is the reason
  SD-4 carries its own fact rather than riding W5-2's.
- **SR-2 — The snippet is not truncated, matching mac's WCAG 1.4.4
  decision.** Mac deliberately sets no `lineLimit` because truncation
  broke at large Dynamic Type (`SearchOverlay.swift:490-496`). Windows
  wraps likewise, so a long snippet makes a tall row.

## Mac defects found while reading it (not this issue's to fix)

Recorded so the Windows twin does not inherit them. Filed as
[#1113](https://github.com/coryj627/slate/issues/1113), which also carries
the SD-1 and SD-2 convergence items.

1. `SearchOverlay.swift:28-29` — header claims arrow keys cycle results.
   No arrow handling exists.
2. `SearchOverlay.swift:511` — documents the row label as
   `"{filename}, line {N}: {snippet}"`. Line 523 emits
   `"{filename}: {snippet}"`.
3. `SearchOverlay.swift:11` — claims the field announces `"Search vault."`.
   The label at `:126` has no trailing period.
4. `SearchOverlay.swift:7-8` and `AppState.swift:9463-9464` — both claim
   Esc restores prior focus. No such code exists.
5. `AppState.swift:9467-9469` — claims ⌘Return opens in a new tab "through
   the overlay's key monitor". The monitor rejects every modified Return
   (`SearchOverlay.swift:571`), so only ⌘-click works.

Three core-side defects, filed as [#1114](https://github.com/coryj627/slate/issues/1114):

6. `crates/slate-uniffi/src/lib.rs:1346-1348` — says `File` and `Tag`
   scopes return `Cancelled` until they land. Both halves are wrong: `Tag`
   shipped in #567, and `File` returns `Unsupported`. The enum doc 1650
   lines below is correct.
7. `crates/slate-core/src/a11y.rs:2480` — claims a Windows test consumes
   `corpus.json`. None does.
8. `search_db.rs:148-153` — the Vault/Folder empty-query branch writes the
   literal `"Search returned no results."` instead of calling
   `summary_for(0)`. Byte-identical today, structurally outside the
   vocabulary.
