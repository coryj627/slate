# W6-1 — Canvas on Windows (#745): contracts

Scope: the stacked PR series that ports the mac canvas (Milestone T) to
Windows —
[`18_windows_port/specs/w6_1_canvas_spec.md`](18_windows_port/specs/w6_1_canvas_spec.md),
behaviourally governed by
[`09_canvas/specs/t0_interaction_contract.md`](09_canvas/specs/t0_interaction_contract.md).
Written BEFORE each PR's round 1 per
[`24_red_team_protocol.md`](24_red_team_protocol.md) §0. The divergence
(CD), accepted-risk (CR) and owner-decision registers are owner-recorded
and **off-limits for review re-litigation**.

Contract ids are prefixed by PR letter (`0a-1…`, `A1…`) so a round can
cite them unambiguously; numbering is per-document.

> **Filename note.** `w6_1_canvas_spec.md` cites this document eleven
> times as `docs/plans/33_canvas_contracts.md`. **33 was taken** by
> `33_upgrade_fence_contracts.md` (shipped in `c559810`, #1078/#1150,
> after that section of the spec was written), so this document is
> **34**. Every `33_canvas_contracts.md` citation in the spec, in the
> SDD ledger, and in the issue means this file.

---

## PR 0a — the canvas announcer vocabulary moves to core

**Goal (spec §PR 0a).** Every canvas announcement becomes a typed
`A11yEvent` in `crates/slate-core/src/a11y.rs` with its template,
priority and verbosity policy core-side; the mac `CanvasAnnouncer`
shrinks to a relay + coalescer (Task 0a-2); `corpus.json` gains the
canvas family; §W-D becomes provable for canvas.

### Contracts

**0a-1 — Full coverage, no free text.** Every shipped canvas
announcement string is a typed variant or a typed ARM of one. Mac's
twelve `CanvasEvent` cases were eight free-text passthroughs
(`CanvasAnnouncer.swift:60–76`) carrying host-composed prose from ~140
call sites; all eight dissolve. `String` payload is permitted only for
dynamic data — titles, labels, group paths, file paths, URL hosts, OS
error detail, host-supplied display chords, counts and dimensions —
**never for a whole sentence**. The vocabulary is
`crates/slate-core/src/a11y.rs` (51 canvas variants over 18 closed
nested enums); enumerated in full below.

**0a-1b — One engine, one top-level variant.** The family is reached
through `A11yEvent::Canvas { event: CanvasA11yEvent }`;
`CanvasA11yEvent` owns the 51 variants and its own `priority()` /
`render()`, which `A11yEvent` delegates to. uniffi caps an enum at 256
variants and this vocabulary was at 197 before canvas, so a flat family
would have spent a fifth of the remaining budget on one surface and left
none for the graph announcer — the other named residue engine, W6-2's
subject. **Nested-family-per-engine is the pattern every later family
copies** (`Graph { event: GraphA11yEvent }`). Variant NAMES keep their
`Canvas` prefix so every spec, contracts-table and downstream-PR
citation (`CanvasMovedTo`, `CanvasSaveConflict`, …) stays literally
true. Pinned by `the_canvas_family_occupies_one_top_level_variant`.
(CD-12.)

**0a-2 — The five-place rule.** A canvas string is pinned in five
hand-maintained places, and all five must move together: the
`A11yEvent` variant + `render()` arm (`a11y.rs`), the in-file golden
table (`a11y.rs`, `corpus_renders_the_shipped_strings`), the
regenerated artifact `tests/fixtures/a11y/corpus.json`, the FFI mirror
(`crates/slate-uniffi/src/lib.rs`), and **both** census mirrors
(`apps/slate-mac/Tests/SlateMacTests/A11yCorpusCensusTests.swift`,
`apps/slate-windows/tests/SlateWindows.Tests/Censuses/A11yCorpusCensus.cs`).
Corpus order is positional, so the canvas family is **appended** — every
pre-existing index (0…258) is untouched.

**0a-3 — Two tripwires, symmetric.** `slate-uniffi`'s
`the_mac_corpus_mirror_lists_every_event_in_order` already parsed the
Swift census so a forgotten mac mirror fails in `cargo test`. Its
Windows twin, `the_windows_corpus_mirror_lists_every_event_in_order`,
now parses `A11yCorpusCensus.cs` the same way, because the C# census
otherwise fails only after `generate-bindings.ps1` + `dotnet test` — a
full local round-trip later. Both are name-and-order only; full
parameter identity stays the censuses' job.

**0a-4 — Completeness is asserted, not assumed.** `a11y.rs`'s
`every_canvas_variant_and_arm_is_represented_in_the_corpus` parses this
file's own `pub enum` declarations and fails when a canvas variant — or
a closed-set ARM of `CanvasStatusNote`, `CanvasBlockedReason`,
`CanvasFailedAction`, `CanvasMutationRefusal`, `CanvasDeleteTarget` —
never reaches `corpus()`. A variant that never reaches the corpus is
pinned by nothing: not the golden, not the artifact, not either census.

**0a-5 — Verbosity is a parameter on exactly two families.**
`CanvasVerbosity { Terse, Standard, Verbose }` is core; it is carried by
`CanvasMovedTo` (the t0 §1.2 navigation matrix) and `CanvasDeleted` (the
t0 §1.3 undo hint at standard+), and by nothing else. Core stays pure —
there is no module state and no "current verbosity"; each host owns the
persisted, live-switchable preference and passes it per event.
`canvas_verbosity_matrix_pins_every_level` pins all fifteen
(family × level × target) renderings and asserts structurally that no
other variant carries the parameter.

**0a-6 — Where-am-I is always verbose-grade.** `CanvasWhereAmI` takes
**no** verbosity parameter — that is the mechanism, not a convention
(t0 §1.4; `CanvasAnnouncer.swift:131–152` never reads `verbosity`).

**0a-7 — Priorities are mac's `.error` case, listed explicitly.**
`CanvasModeRejected`, `CanvasBlocked`, `CanvasActionFailed`,
`CanvasSaveConflict`, `CanvasFileNotFound` are `High`; every other
canvas event is `Medium`. `priority()` ends in a catch-all
`_ => Medium`, so the High members are named in the explicit arm and
pinned both ways by `canvas_priorities_pin_the_error_tier`.

**0a-8 — Coalescing stays host-side; the class keys do not.** Timing is
the hosts' (a pure render has no clock), but the two class keys are
pinned in one Rust doc-comment on the canvas family so both hosts copy
one list: **`navigation`** = `CanvasMovedTo`, `CanvasGroupEntered`,
`CanvasGroupLeft`, `CanvasConnectionTraversed`, `CanvasMoveRelative`,
`CanvasResizeGeometry`; **`filter`** = `CanvasFilterCount`,
`CanvasFilterCleared`; everything else posts immediately; a `High`
canvas event flushes **and drops** both pending classes. 200 ms
latest-wins, each class independent (mac `CanvasAnnouncer.swift:90–129,
217–236`).

**0a-9 — The first chord-bearing templates.** `a11y.rs:30–36` said "no
current template carries a chord"; that sentence is replaced. Three
templates now take the host's **display chord string** as a parameter
(decision 12: the chord table is host-owned): `CanvasDeleted.undo_chord`
(mac `"⌘Z"`, Windows `"Ctrl+Z"`) and `CanvasEmptyOnboarding`'s
`new_card_chord` / `palette_chord`. Key NAMES spelled identically on
both platforms — `Return`, `Escape` — stay literal in the template and
are **not** chords. The chord parameter is the one recorded platform
difference in the §W-D corpus; entry `CanvasDeleted { Cards { count: 3 },
Standard, "Ctrl+Z" }` exercises it deliberately.

**0a-10 — One card reference, one relative phrase.** t0 §1.1's
`⟨Kind⟩ card "title"` / `Group "label"` composition is a single core
helper (`a11y.rs::card_ref`) used by every template that names a card;
the placement phrase is `a11y.rs::relative_phrase` over core's OWN
`canvas::placement::RelativeDesc`, which the host passes straight
through (R-D: no host re-derivation). Mac had three spellings of the
card reference (`CanvasAnnouncer.swift:37–39`,
`CanvasRendererView.swift:403,447` unquoted,
`CanvasOutlineView.swift:335–347` with a hardcoded `kind: "text"`);
they collapse to this one.

**0a-11 — Colour names come from core.** No template embeds a preset
dictionary. `color_name: Option<String>` is core's own
`canvas::color_name()` output (`canvas/mod.rs:80–88`); `None` speaks the
literal `no color` (the clear-colour arm). The three Swift copies
(`AppState+CanvasActions.swift:340`, `:766`,
`CanvasPromptSheet.swift:227`) die with the migration.

**0a-12 — The admission ladder joins the vocabulary.** The six
mutation-refusal constants that bypassed the canvas funnel entirely via
`postMutationAnnouncement` (`AppState+Canvas.swift:348–357`, `:411–412`,
posted through `AppState.swift:16271–16276`) are
`CanvasMutationRefused { reason }`. This closes the DoD §H hole the
research pass found: `CanvasAnnouncerTests.testNoDirectAnnouncementsUnderCanvas`
only greps `postAccessibilityAnnouncement`, so those five canvas call
sites were invisible to the funnel guard.

**0a-13 — Label-grade events are marked as such.** Three events are
LABEL class, never spoken: `CanvasEmptyOnboarding` (the onboarding
region's text), `CanvasUndoMenuTitle` (the Edit menu item title), and —
per its own doc comment — nothing else. They live here because both
hosts must compose them identically from core data, not because they are
announcements. Every other canvas label (the filter summary
`⟨n⟩ of ⟨m⟩ cards match`, the table summary, the outline node value, the
renderer's peer names, the degraded-state message, the "Where am I?"
panel heading) stays §W-C label class and is NOT in this vocabulary.

### The event enumeration (the PR's contract)

51 variants of `CanvasA11yEvent`, all reached through the single
`A11yEvent::Canvas { event }` wrapper (0a-1b); 165 corpus entries,
artifact 259 → **424**. `V` = the `CanvasVerbosity` parameter.
Coalescing class: `nav` / `filter` / `—` (immediate). All priorities
`Medium` unless marked **High**.

| Event | Payload | Template(s) | Pri | Class | mac site(s) replaced |
|---|---|---|---|---|---|
| `CanvasMovedTo` | `V, kind_label, title, ordinal_n, total_m, container?, connection_count, color_name?, marked` | terse `⟨title⟩` · standard `⟨card⟩, ⟨n⟩ of ⟨m⟩ in ⟨container‖canvas⟩` · verbose + `, ⟨k⟩ connection[s]` + `, ⟨color⟩` + `, marked` | | nav | `CanvasAnnouncer.swift:47–50,158–170`; `AppState+CanvasNavigation.swift:186–193`; `CanvasOutlineView.swift:399–406`; `CanvasTableView.swift:114–121` |
| `CanvasGroupEntered` | `label, count` | `Entering group "⟨label⟩", ⟨n⟩ card[s]` | | nav | `CanvasAnnouncer.swift:51,172`; `AppState+CanvasNavigation.swift:181`; `CanvasOutlineView.swift:393–394` |
| `CanvasGroupLeft` | `label` | `Leaving group "⟨label⟩"` | | nav | `CanvasAnnouncer.swift:52,173`; `AppState+CanvasNavigation.swift:183`; `CanvasOutlineView.swift:396` |
| `CanvasConnectionTraversed` | `direction, kind_label, title, label?` | `⟨Connects to‖Connected from‖Linked with⟩ ⟨card⟩[, labelled "⟨label⟩"]` | | nav | `CanvasAnnouncer.swift:55–57,175–184`; `AppState+CanvasNavigation.swift:119–123`; deletes `CanvasOutlineView.swift:335–347` |
| `CanvasTracePathEnd` | `titles[]` | `Path: A, then B. End of path — ⟨n⟩ cards visited.` | | — | `AppState+CanvasNavigation.swift:152–155` |
| `CanvasMoveRelative` | `descs[], overlap?` | `Alone on the canvas` ‖ `Below "X", right of "Y"` [+ `. Overlapping another card` ‖ `. Clear of overlaps`] | | nav | `AppState+CanvasModes.swift:192–194,299–339,286–290` |
| `CanvasResizeGeometry` | `preset?, width, height, overlap?` | `⟨w⟩ by ⟨h⟩` ‖ `Resized to default size: ⟨w⟩ by ⟨h⟩` ‖ `Resized to fit to content: …` [+ overlap clause] | | nav | `AppState+CanvasModes.swift:179–181,229–233,286–290` |
| `CanvasResizeClamped` | — | `Minimum size.` | | — | `AppState+CanvasModes.swift:172` |
| `CanvasModeEntered` | `mode, object` | `⟨Mode⟩ mode — ⟨"title"‖N cards⟩. ⟨exits⟩` | | — | `CanvasModeController.swift:78`; `AppState+CanvasModes.swift:57–65,100–103`; `AppState+CanvasConnect.swift:96–98` |
| `CanvasModeRejected` | `active_mode` | `⟨Mode⟩ mode is active. Return to commit or Escape to cancel first.` | **High** | — | `CanvasModeController.swift:73–74` |
| `CanvasModeCommitted` | `verb, object` | `Placed ⟨object⟩.` ‖ `Resized ⟨object⟩.` | | — | `AppState+CanvasModes.swift:261` |
| `CanvasModeEndedWithoutEffect` | `mode` | `⟨Move‖Resize⟩ ended — nothing changed.` ‖ `Connect ended — no target chosen.` | | — | `AppState+CanvasModes.swift:256`; `AppState+CanvasConnect.swift:103` |
| `CanvasModeCancelled` | `mode, restoration` | `⟨Verb⟩ cancelled.` [+ ` — card[s] returned.` ‖ ` — size restored.` ‖ ` — back at "X".`] | | — | `AppState+CanvasModes.swift:71–75,110–112`; `AppState+CanvasConnect.swift:110–112` |
| `CanvasCreated` | `kind_label, title, relative` | `Created ⟨lowercased card⟩ ⟨relative⟩` (no period) | | — | `CanvasAnnouncer.swift:198–208`; `AppState+CanvasActions.swift:101–105,141–144`; `AppState+CanvasCreate.swift:220–224` |
| `CanvasFileCreated` | `name` | `Created canvas "⟨name⟩".` | | — | `AppState+CanvasActions.swift:301–303` |
| `CanvasConnectedCardCreated` | `relative, origin_title` | `Created connected card ⟨relative⟩ — connected from "⟨origin⟩".` | | — | `AppState+CanvasExtras.swift:84–88` |
| `CanvasConnected` | `from_title, to_title, label?` | `Connected "⟨a⟩" to "⟨b⟩"[, labelled "⟨l⟩"].` | | — | `AppState+CanvasConnect.swift:62–64` |
| `CanvasConnectionUpdated` | `label?` | `Connection updated[, labelled "⟨l⟩"].` | | — | `AppState+CanvasConnect.swift:181–183` |
| `CanvasMovedIntoGroup` | `label` | `Moved into group "⟨label⟩".` | | — | `AppState+CanvasActions.swift:472` |
| `CanvasRemovedFromGroup` | `label` | `Removed from group "⟨label⟩".` | | — | `AppState+CanvasCreate.swift:324–325` |
| `CanvasColorSet` | `title, color_name?` | `Set "⟨title⟩" to ⟨color‖no color⟩.` | | — | `AppState+CanvasActions.swift:340,348–349` |
| `CanvasRenamedGroup` | `label` | `Renamed group to "⟨label⟩".` | | — | `AppState+CanvasActions.swift:370–371` |
| `CanvasCardUpdated` | `title` | `Updated "⟨title⟩".` | | — | `AppState+CanvasCreate.swift:97` |
| `CanvasCardRetargeted` | `title, path` | `"⟨title⟩" now points at ⟨path⟩.` | | — | `AppState+CanvasCreate.swift:270–271` |
| `CanvasCardPlaced` | `verb, title, relative` | `⟨Moved‖Duplicated⟩ "⟨title⟩" ⟨relative⟩.` | | — | `AppState+CanvasActions.swift:538–541`; `AppState+CanvasExtras.swift:197–200` |
| `CanvasCardAligned` | `title, target_title` | `Aligned "⟨a⟩" with "⟨b⟩".` | | — | `AppState+CanvasActions.swift:608–610` |
| `CanvasConvertedToNote` | `path` | `Converted to note ⟨path⟩. The card now points at it.` | | — | `AppState+CanvasExtras.swift:353–355` |
| `CanvasDeleted` | `target, V, undo_chord` | `Deleted ⟨card⟩` ‖ `Ungrouped Group "⟨l⟩" — cards kept` ‖ `Deleted ⟨n⟩ card[s]` ‖ `Deleted connection ⟨to‖from‖with⟩ "⟨t⟩"[, labelled "⟨l⟩"]`; **+ ` — ⟨undo_chord⟩ to undo` at standard+** | | — | `CanvasAnnouncer.swift:63,189–192`; `AppState+CanvasActions.swift:325–329,751–753`; `AppState+CanvasConnect.swift:127–131,148–149` |
| `CanvasBulkMoved` | `count, relative` | `Moved ⟨n⟩ cards ⟨relative⟩.` | | — | `AppState+CanvasActions.swift:563–566` |
| `CanvasBulkColorSet` | `count, color_name?` | `Set ⟨n⟩ card[s] to ⟨color‖no color⟩.` | | — | `AppState+CanvasActions.swift:766,776–777` |
| `CanvasGrouped` | `count, label` | `Grouped ⟨n⟩ card[s] into "⟨label⟩".` | | — | `AppState+CanvasActions.swift:817–821` |
| `CanvasBulkDuplicated` | `count` | `Duplicated ⟨n⟩ cards — one undo restores.` | | — | `AppState+CanvasExtras.swift:202–203` |
| `CanvasMarkToggled` | `marked, title, count` | `⟨Marked‖Unmarked⟩ "⟨title⟩". ⟨n⟩ marked.` | | — | `AppState+CanvasActions.swift:698–703` |
| `CanvasMarksCleared` | `count` | `No marks.` (0) ‖ `Cleared ⟨n⟩ mark[s].` | | — | `AppState+CanvasActions.swift:711–712` |
| `CanvasFilterCount` | `matched` | `⟨n⟩ card[s] match.` | | filter | `AppState+CanvasExtras.swift:398–399` |
| `CanvasFilterCleared` | `total` | `Filter cleared — ⟨n⟩ card[s].` | | filter | `AppState+CanvasExtras.swift:388–390`; `CanvasContainerView.swift:458–461` |
| `CanvasZoom` | `context?, percent` | `Zoom ⟨p⟩ percent.` ‖ `Fit canvas. Zoom …` ‖ `Zoomed to selection. Zoom …` | | — | `AppState+CanvasNavigation.swift:199,228–229,243–244` |
| `CanvasFollowSelectionToggled` | `following` | `Viewport follows selection.` ‖ `Viewport stays put.` | | — | `AppState+CanvasNavigation.swift:252–255` |
| `CanvasSurfaceShown` | `surface` | `Canvas ⟨outline‖table‖visual⟩ view.` | | — | `AppState+Canvas.swift:293`; `CanvasSelection.swift:19–25` |
| `CanvasHistoryApplied` | `verb, name` | `Undid: ⟨name⟩` ‖ `Redid: ⟨name⟩` | | — | `CanvasAnnouncer.swift:212–213`; `AppState+Canvas.swift:606,636` |
| `CanvasUndoMenuTitle` | `verb, name` | `⟨Undo‖Redo⟩ ⟨Name⟩` (leading char only), bare verb when empty — LABEL | | — | `AppState.swift:3987–3990` |
| `CanvasStatus` | `note` (26 arms) | see the status table below | | — | 60+ sites; see below |
| `CanvasBlocked` | `reason` (15 arms) | see the blocked table below | **High** | — | see below |
| `CanvasActionFailed` | `action` (12 arms), `detail` | `⟨Verb⟩ failed: ⟨detail⟩` — New card · New group · New canvas · Move · Placement · Align · Create · Remove · Duplicate · Create connected card · Canvas action · Where am I | **High** | — | `AppState+CanvasActions.swift:110,146,220–221,230–231,474,569,613`; `AppState+CanvasCreate.swift:226,327`; `AppState+CanvasExtras.swift:93–94,206`; `AppState+Canvas.swift:332,576,580` |
| `CanvasSaveConflict` | — | `The canvas changed on disk. Reload it to continue — your action was not applied.` | **High** | — | `AppState+Canvas.swift:571–574` |
| `CanvasFileNotFound` | `target` | `⟨target⟩ is missing from the vault. Use Locate File to repoint this card.` | **High** | — | `CanvasContainerView.swift:180–183` |
| `CanvasOpened` | `title, target` | `Opened ⟨t⟩ in its default app.` ‖ `Opened ⟨t⟩ in your browser.` | | — | `CanvasContainerView.swift:177–178,188` |
| `CanvasMutationRefused` | `reason` (6 arms) | the six admission sentences verbatim | | — | `AppState+Canvas.swift:348–357,411–412` (via `AppState.swift:16271–16276`) |
| `CanvasLoadedDegraded` | `skipped` | `Canvas loaded. ⟨n⟩ unsupported item[s] are preserved in the file but not shown.` | | — | `CanvasContainerView.swift:354–355` (static banner today — CD-3) |
| `CanvasEmptyOnboarding` | `new_card_chord, palette_chord` | `Canvas is empty. Press ⟨chord⟩ to create your first card. Every other canvas action is in the Command Palette, ⟨chord⟩.` — LABEL | | — | `CanvasContainerView.swift:479,481–483,492–494` |
| `CanvasWhereAmI` | `kind_label, title, group_path[], ordinal_n, total_m, connection_count, in_count, out_count, color_name?, marked, mode?, filter` | `⟨card⟩, ⟨at canvas level‖in A › B⟩, ⟨n⟩ of ⟨m⟩, ⟨k⟩ connection[s] (⟨i⟩ in, ⟨o⟩ out)[, ⟨color⟩][, marked][, ⟨Mode⟩ mode][, ⟨x⟩ of ⟨y⟩ shown]` — always verbose-grade | | — | `CanvasAnnouncer.swift:134–152`; `AppState+Canvas.swift:315–330` |

**`CanvasStatusNote` (26 arms, all Medium).** `NothingSelected`
(`Nothing selected.` — 16 mac sites: Actions 400/415/488/693, Connect
73/89, Create 33/240/287, Extras 52/101/123/217, Modes 48/92, Navigation
237) · `NoMarks` (`No marks.` — Actions 720/738/763/788/826) ·
`NotAGroup` (`Not a group.` — Actions 361/389, Navigation 61) ·
`NotATextCard` (Create 46) · `NotAFileCard` (Create 245) · `NoGroups`
(`This canvas has no groups.` — Actions 406) · `NoNotesInVault` (Create
142) · `NoMediaInVault` (Create 154) · `NoFilesToPointAt` (Create 251) ·
`OnlyTextCardsConvert` (Extras 221) · `NoConnections`
(`The selected card has no connections.` — Actions 643/659) ·
`PickOutsideMovingSet` (Actions 516) · `PickDifferentTarget` (Connect
44) · `NoChanges` (Create 84) · `NotReadable` (`Canvas is not
readable.` — Canvas 304) · `Empty` (`Canvas is empty.` — Canvas 310) ·
`EndOfCanvas` / `StartOfCanvas` (Navigation 43–44) · `AtCanvasLevel`
(Navigation 86) · `NoCardsMatchFilter` (Navigation 33) ·
`NothingToUndo` / `NothingToRedo` (Canvas 597/627) ·
`GroupIsEmpty { label }` (Navigation 69) ·
`NoOutgoingPath { title }` (Navigation 148) ·
`NotInAGroup { title }` (Create 291/306) ·
`NoConnection { forward, ordinal? }` (Navigation 109–111).

**`CanvasBlockedReason` (15 arms, all High).** `ModeBusy`
(`A move or resize is in progress. Return to place it or Escape to
cancel first.` — Canvas 553/591/621, Extras 266–269 — four copies) ·
`UndoBlocked` (Canvas 611–612) · `RedoBlocked` (Canvas 639–640) ·
`LinkOpenFailed` (`CanvasContainerView.swift:190`) ·
`AlignWouldOverlap` (Actions 592–593) · `NotAUrl` (Create 181) ·
`CardTextUnreadable` (Create 52) · `NotePathMustEndInMd` (Extras 254) ·
`NoFreeSpaceInGroup { label }` (Actions 457) ·
`NotePathExists { path, on_disk }` (Extras 262 / 357–358) ·
`NoteReadFailed { message }` (Extras 360–361) ·
`NoteCreateFailed { path, message }` (Extras 362–364) ·
`NoteRetargetFailed { path, message }` (Extras 365–368) ·
`HeadingNotFound { heading, filename }` (Extras 421–424) ·
`ReopenFailed { message }` (`CanvasContainerView.swift:449–451`).

### Tests that pin PR 0a

`crates/slate-core/src/a11y.rs`: `corpus_renders_the_shipped_strings`
(the golden table — 165 new rows),
`committed_corpus_artifact_matches_the_vocabulary` (artifact round-trip,
424 entries), `every_canvas_variant_and_arm_is_represented_in_the_corpus`,
`the_canvas_family_occupies_one_top_level_variant`,
`canvas_verbosity_matrix_pins_every_level`,
`canvas_where_am_i_is_always_verbose_grade`,
`canvas_priorities_pin_the_error_tier`,
`multiline_templates_carry_no_stray_whitespace`.
`crates/slate-uniffi/src/lib.rs`: `the_ffi_mirror_covers_every_core_a11y_variant`,
`the_mac_corpus_mirror_lists_every_event_in_order`,
`the_windows_corpus_mirror_lists_every_event_in_order` (new).
`apps/slate-windows/.../A11yCorpusCensus.cs`:
`EveryCorpusEventRendersTheCommittedIdentityTextAndPriority`,
`TheMirrorHasNoDuplicateEntries`.

---

## §W-G canonical-consumption audit (seeded from the spec §2 table; closed in PR H)

Tier 1 and 2 move to core with the mac consuming the new API in the same
PR and the Swift derivation deleted (decision 5). Tier 3 is
host-by-designation — recorded so the audit is explicit, not silent. PR
H re-greps `apps/slate-windows/src/SlateWindows/Canvas/` against this
table.

| # | Swift-derived pocket (file:line) | Core today | Tier | Target (PR) | State |
|---|---|---|---|---|---|
| A | The entire announcer grammar: verbosity matrix, group entered/left, connection traversed (direction phrases, duplicated again in `CanvasOutlineView.swift:335–347`), confirmations, destructive "— ⌘Z to undo", error, filter count, mode entered/cancelled/committed, undid/redid, Where-am-I readback (`CanvasAnnouncer.swift:104–213`, `AppState+CanvasActions.swift:328–819` prose sites, `AppState+CanvasConnect.swift:62,127–181`); preset-name dictionary duplicated (`AppState+CanvasActions.swift:340,766`) | `HostComposed` residue; core supplies every payload (`CardSummary`, `Neighbor.direction`, `RelativeDesc`, `color_name`) | **1** | `A11yEvent::Canvas*` family + `CanvasVerbosity` (0a) | **core half landed (this PR); mac consumption is Task 0a-2** |
| B | Relative-position description in move mode — nearest neighbours by squared centre distance, `Below "X", right of "Y"` (`AppState+CanvasModes.swift:299–339`) | `RelativeDesc` exists for placement only | 2 | `canvas_describe_relative(h, rect, exclude) -> Vec<CanvasRelativeDesc>` (0b) | open — 0a's `CanvasMoveRelative { descs }` already takes that list |
| C | Auto-side selection for new connections, **two copies** (`AppState+CanvasConnect.swift:24–33`, `CanvasRendererView.swift:582–600`) | none | 2 | `canvas_auto_sides` (0b) | open |
| D | Containment / parent-group resolution, **three copies** (`AppState+CanvasCreate.swift:296–305`, `AppState+CanvasExtras.swift:131–147`, `AppState+CanvasActions.swift:439–441`) | `GroupTree` exists, not exposed | 2 | `canvas_parent_of`, `canvas_children_of` (0b) | open — 0a-CD-4 depends on `children_of` for the group card count |
| E | Enter/exit group + group card count from outline `depth` walks; trace-path walk (`AppState+CanvasNavigation.swift:55–90,129–156,170–181`) | `reading_order`/`adjacency` exist | 2 | D's queries + `canvas_trace_path` (0b) | open |
| F | Selection model + reading-order re-projection of the marked set | none (correct) | 3 / 2 | host state; `canvas_order_nodes` (0b) | open |
| G | Undo/redo stacks, depth/session policy, menu-title composition (`AppState.swift:3987`) | `apply()` returns inverse + names | 3 | host stack; **menu title** = `CanvasUndoMenuTitle{verb,name}` | **menu title landed (0a)** |
| H | Placement math leaks; `MIN_CARD_SIZE` only in Swift | constants in `placement.rs` | 2 | `canvas_constants()`, `canvas_group_rect_around`, `canvas_place_inside_group`, `canvas_bounds` (0b) | open |
| I | Viewport math — clamp 0.1–4.0, step 1.25, fit padding 40/120 | none | 3 | host rendering; constants pinned here; zoom % announced via `CanvasZoom` | **event landed (0a)** |
| J | Table column order/sort comparators/summary sentence; outline interleave | rows from core | 3 | host projection config; summary sentence stays a **static label** (never announced on mac) | resolved as label class (0a-13) |
| K | Filter predicate — title/kind/groupPath/target, case-insensitive contains | none | 2 | `canvas_filter` (0b) | open |
| L | Speakable-name dedup vs core's untitled-only allocation — two uniqueness algorithms | partial, conflicting | 2 | one algorithm in core: `CardSummary.speakable_name` (0b, D-3) | open |
| M | Node/edge id minting | none | 2 | `canvas_new_id()` (0b) | open |
| N | Overlap onset/offset transition tracking | query exposed | 3 | host state machine (two-state, pinned); the CLAUSE is core's (`CanvasOverlapTransition`) | **clause landed (0a)** |
| O | Resize → Fit to Content text-metrics approximation | none | 3 (D-5) | host, identical placeholder formula both hosts; the LABEL is core's (`CanvasResizePreset::FitToContent`) | **label landed (0a)** |

---

## Recorded divergences (owner-recorded; off-limits for re-litigation)

**CD-1 — No standalone overlap events.** The spec's minimum set proposed
`CanvasOverlapOnset{titles}` / `CanvasOverlapCleared`. Mac ships them as
string SUFFIXES appended to the transient-geometry line
(`AppState+CanvasModes.swift:286–290`) with **no titles anywhere**, and
the coalescer therefore emits ONE utterance. Modelling them as separate
events would change what a user hears (two utterances) and would invent
a payload. They are `overlap: Option<CanvasOverlapTransition>` on
`CanvasMoveRelative` / `CanvasResizeGeometry`, rendering mac's verbatim
`". Overlapping another card"` / `". Clear of overlaps"`. (Controller
ruling R-0a-2.)

**CD-2 — `CanvasFilterCount` carries `matched` only.** The spec proposed
`{matched, total}`. The announced mac string
(`AppState+CanvasExtras.swift:398–399`) is `"⟨n⟩ card[s] match."`;
`total` appears only in the static summary LABEL
(`CanvasContainerView.swift:338–342`) and in Where-am-I. The m-of-n form
stays label class. (R-0a-3.)

**CD-3 — `CanvasLoadedDegraded` is an announcement Windows and mac both
gain.** t0 §5 requires the polite announcement; mac ships only a static
banner (`CanvasContainerView.swift:350–360`). The static-only banner is
a t0 cheat, so the event exists and mac gains the post in Task 0a-2.
Likewise `CanvasEmptyOnboarding` renders the **AX spelled-out** form
(`CanvasContainerView.swift:492–494`) as the canonical text — the glyph
form (`:481–483`) stays a host label, because the spelled-out one is
what a screen reader actually receives. (R-0a-4.)

**CD-4 — Group entry speaks the group's CHILD count.** t0 §1.2 specifies
`Entering group "X", ⟨m⟩ cards`; mac passes the entered row's SIBLING
count (`AppState+CanvasNavigation.swift:170–181`), which miscounts. The
core parameter is documented as *the group's own card count*; the mac
expectation is updated in Task 0a-2 and the host will source it from
0b's `canvas_children_of`. This is a bug fixed by the migration.
(R-0a-7.)

**CD-5 — Where-am-I has ONE filter spelling.** Mac ships two: the
tested/parameterised `3 of 40 shown` (`CanvasAnnouncerTests:143–144`)
and the production `Filter active: 3 of 40 cards match.` appended by the
call site with a leading space and no comma
(`AppState+Canvas.swift:322–328`). t0 §1.4 specifies the first, and t0
wins over the implementation, so `CanvasFilterState::Active` renders
`⟨matched⟩ of ⟨total⟩ shown` as a normal comma-joined clause. The event
also carries `mode` (always; the host passes `None` when no mode is
active) — mac hardcodes `activeMode: nil` despite the mode stack having
shipped. (R-0a-6.)

**CD-6 — Core's thousands grouping wins over `CountCopy`.**
`CountCopy.swift:19–25` documents a deliberate NON-grouping divergence
from core's `count_noun`. Every canvas string mac built with
`CountCopy.counted` now routes through core's grouped `count_noun`, so
at ≥ 1000 the spoken string changes (`1000 cards` → `1,000 cards`).
Affected: bulk delete, bulk colour, group-marked, filter-cleared,
cleared-marks. The strings mac interpolated PLAINLY (`Moved ⟨n⟩ cards`,
`Duplicated ⟨n⟩ cards`, `Entering group …, ⟨n⟩ cards`,
`⟨n⟩ card[s] match.`, `⟨n⟩ marked.`, `⟨n⟩ cards visited.`, the mode
object) stay ungrouped, exactly as shipped.

**CD-7 — The connection-delete sentence no longer lower-cases the
author's words.** `AppState+CanvasConnect.swift:148–149` lower-cases the
whole picker row, so mac speaks `Deleted connection to "ideas"` for a
card titled *Ideas*. t0 §1.1 quotes titles verbatim, and the spec names
the mac implementation as the reference "never as the parity target
where it cheats". `CanvasDeleteTarget::Connection` is therefore
structural (`direction`, `other_title`, `label?`): the **template**
lower-cases only the fixed direction word (`to` / `from` / `with`) and
the title and label pass through unchanged. The template is
byte-identical to the shipped one; only the runtime casing of user data
changes.

**CD-8 — The chord parameter is the one recorded platform difference in
the corpus.** `undo_chord` / `new_card_chord` / `palette_chord` are
host-supplied display strings, so a mac corpus entry and a Windows one
can differ there and only there. The §W-D census normalizes it; the
corpus deliberately carries one `Ctrl+Z` entry so the parameter is
exercised, not assumed. (Program decision 12; contract 0a-9.)

**CD-9 — `towardOther` is dropped.** `CanvasAnnouncer.swift:53–57,
178–179` inverts the direction phrase on a `towardOther` flag that every
production call site passes `true` (the sole `false` case lives in
`CanvasAnnouncerTests:70–72`). t0 §1.2 derives the phrase from
`fromEnd`/`toEnd` alone. `CanvasConnectionTraversed` therefore takes
`direction` only; all three shipped phrases stay reachable
(`Outgoing → Connects to`, `Incoming → Connected from`,
`Bidirectional|Undirected → Linked with`). The Swift golden at
`CanvasAnnouncerTests:70–72` is updated in Task 0a-2.

**CD-10 — Families are typed nested enums, not one variant per
sentence.** uniffi caps an enum at **256 variants**
(`uniffi_macros`), and `A11yEvent` already carried 197. One variant per
shipped canvas sentence would have been ~95, i.e. 292 — the mirror does
not compile. The vocabulary is therefore 51 variants over closed nested
enums: `CanvasStatus{note}` (26 arms), `CanvasBlocked{reason}` (15),
`CanvasActionFailed{action}` (12), `CanvasMutationRefused{reason}` (6),
`CanvasDeleteTarget` (4), plus the small mode/verb/surface sets. Every
sentence is still core-owned and selected by a closed typed set — 0a-1's
substance is unchanged — but the spec's "one `A11yEvent` variant per
family" reads as "one per family, families grouped by speech act". The
spec-named variants (`CanvasMovedTo`, `CanvasDeleted`, `CanvasZoom`,
`CanvasSaveConflict`, `CanvasFileNotFound`, `CanvasWhereAmI`,
`CanvasResizeClamped`, `CanvasTracePathEnd`, `CanvasEmptyOnboarding`,
`CanvasLoadedDegraded`, `CanvasUndoMenuTitle`, …) all kept their names.
The four bulk variants stayed four per R-0a-5.

**CD-12 — The family nests under one top-level variant** (controller
ruling R-0a-8, 2026-08-22). `A11yEvent::Canvas { event: CanvasA11yEvent }`
rather than 51 top-level variants. Landing it here — before Task 0a-2 and
before any host names a case — is the cheapest moment it can ever
happen: after merge it would churn shipped §W-D identities across the
artifact and three mirrors. **Constraint honoured:** corpus indexes
0–258 are byte-identical (verified entry by entry), and no `text` or
`priority` changed anywhere; only the 165 canvas `event` identity
strings gained the `Canvas { event: … }` wrapper. The in-file golden
table needed no edit at all — it pins (priority, text). Variant names
keep the `Canvas` prefix (0a-1b). Two tests were strengthened rather
than merely updated: `the_ffi_mirror_covers_every_core_a11y_variant`
now checks `CanvasA11yEvent` as well as `A11yEvent` (a nested family is
exactly as invisible to a host when the mirror misses one of its
variants), and both corpus tripwires now compare
`Canvas/CanvasMovedTo`-style paths — without the inner name the wrapper
would have flattened 165 distinct entries into one and the order check
would have stopped meaning anything. Mutation-verified by swapping two
adjacent canvas entries: both tripwires fail at index 265.

**CD-11 — Names that differ from the spec's indicative list.** The spec
wrote names "indicative". Landed: `CanvasBulk{verb,count,undo_chord}` →
four variants (R-0a-5, and none carries the undo hint, matching mac);
`CanvasModeCommitted{mode,detail}` → `CanvasModeCommitted{verb,object}`
+ `CanvasModeEndedWithoutEffect{mode}`;
`CanvasModeCancelled{mode}` → `{mode, restoration}`;
`CanvasUndid`/`CanvasRedid` → `CanvasHistoryApplied{verb,name}`;
`CanvasMoveRelative{descs}` gained `overlap` (CD-1);
`CanvasRemovedFromGroup` gained `label` (the spec listed it
payload-less; the shipped sentence names the group).

---

## Accepted risks (owner-recorded; off-limits for re-litigation)

**CR-1 — uniffi's 256-variant enum cap: pressure resolved, and the
pattern is set.** The flat family took `A11yEvent` to 248 of 256, which
left nothing for W6-2's graph family. R-0a-8 nested it: the top level is
back to **~198 of 256** and the canvas family has its own budget inside
`CanvasA11yEvent` (51 of 256). **Nested-family-per-engine is now the
pattern** — the graph announcer lands as `Graph { event: GraphA11yEvent }`,
and any future engine-scale vocabulary does the same rather than
spending top-level slots. The residual risk is only that a later author
adds a one-off variant at the top level without noticing the ceiling;
`the_canvas_family_occupies_one_top_level_variant` documents the shape
they should copy.

**CR-2 — `a11y.rs` is now 5,291 lines** (was 2,586). The canvas family roughly
doubled the file. Splitting the module was deliberately NOT done in this
PR: the three positional lists (`corpus()`, the golden table, the
artifact) and two parsers (the uniffi tripwires, which locate
`pub enum A11yEvent {` in this file) are coupled to its layout, and a
split is a separate, reviewable change.

**CR-3 — Two shipped strings have English defects and were migrated
verbatim.** `⟨1⟩ card match.` (`AppState+CanvasExtras.swift:398–399` —
the plural rule is applied to the noun but the verb is fixed) and
`1 unsupported item are preserved …`
(`CanvasContainerView.swift:354–355`). The copy rule is verbatim
migration; fixing them is a product decision and a §W-D parity change,
so they are pinned as shipped and listed here rather than silently
corrected.

**CR-4 — `CanvasModeCancelled` and `CanvasModeEndedWithoutEffect` admit
combinations no host produces** (e.g. `Resize` + `BackAt`). The
alternative — one variant per mode — costs three more of the eight
remaining enum slots (CR-1). Hosts construct these at exactly three call
sites each, all in the mode controller.

**CR-5 — The residue count is unchanged by THIS commit.**
`A11yResidueCensusTests.pinnedResidueSites` stays 30; it drops to **29**
in Task 0a-2, when `CanvasAnnouncer.swift:104` stops posting
`.hostComposed`. `AppState.swift:16273` (`postMutationAnnouncement`) is a
SHARED residue site used by five canvas admission paths and by
non-canvas structural builders, so it cannot be deleted by the canvas
migration even though `CanvasMutationRefused` now exists (0a-12); the
canvas call sites move off it, the marker stays.

---

## Mac details recorded while reading (not this issue's to fix)

- **`canvasColorMarked` is unregistered.** `AppState+CanvasActions.swift:757`
  is implemented and tested but has no command id, palette row, or menu
  item. Windows matches the registry, not the hidden verb (D-4); file
  the mac issue.
- **The renderer's auto-side rule is duplicated** —
  `AppState+CanvasConnect.swift:24–33` vs `CanvasRendererView.swift:582–600`
  (`anchorPoint`'s `case nil` arm). PR 0b deletes it.
- **Preset colour names are spelled three times in Swift** —
  `AppState+CanvasActions.swift:340`, `:766`,
  `CanvasPromptSheet.swift:227` — shadowing core's `color_name()`. Task
  0a-2 deletes all three (0a-11).
- **`CanvasTableView.swift:92` discards core's priority**: it unwraps an
  already-core-rendered event to text and re-wraps it as `.status`
  (Medium). Task 0a-2 relays `a11yRender(...).priority` instead.
- **`CanvasOutlineView.swift:391` still uses the title-keyed group
  lookup** that `AppState+CanvasNavigation.swift:172–180` deliberately
  replaced for Codoki #613 (repeated group labels miscount). PR 0b's
  `canvas_children_of` settles it.
- **The funnel guard has a hole**:
  `CanvasAnnouncerTests.testNoDirectAnnouncementsUnderCanvas:168–191`
  greps only `postAccessibilityAnnouncement`, so the five canvas
  `postMutationAnnouncement` sites bypass it undetected. 0a-12 gives
  them a vocabulary; Task 0a-2 should widen the grep.
- **`Deleted connection ` with a trailing space** is reachable on mac
  when the edge lookup misses (`AppState+CanvasConnect.swift:149`'s
  `?? ""`). The typed event cannot express it.

---

## Owner decisions (adopted by controller ruling 2026-08-22, autonomous run)

| # | Decision | Adopted |
|---|---|---|
| D-1 | §2 tiering (Tier 1+2 move to core with mac consumption; Tier 3 host-by-designation) | **Accepted as listed.** Any demotion of a Tier-2 row is a recorded divergence naming the duplicated rule. |
| D-2 | Chord collisions | **`whereAmI` = Ctrl+Alt+Shift+I** (G18 precedent, `Divergence` recorded on the row); **`connectTo` keeps Ctrl+Alt+C** in `ChordScope.Canvas` (disjoint delivery site from Reading — the Ctrl+F precedent). |
| D-3 | `speakable_name` session stickiness (T R21) | **Core deterministic by document order**; R21 stickiness preserved by the same mechanism core uses for untitled ordinals if it is session-held, else "ordinals may renumber on delete" is a two-platform divergence. Settled in 0b. |
| D-4 | `canvasColorMarked` unregistered on mac | **File the mac issue; Windows ships Color Marked only if mac registers it** (parity = the registry). |
| D-5 | Resize → Fit to Content placeholder formula | **Both hosts use the identical formula**, host-designated and recorded; a real text-measure API is a future core query. 0a owns only the spoken label. |
| D-6 | Canvas undo/redo chords | **Ctrl+Z / Ctrl+Y** (decision 12 + the structural-undo precedent); `Ctrl+Shift+Z` only if the editor already aliases it. |
| D-7 | Voice Control twin for the AT checklist | **Windows Voice Access** ("show numbers") as the recorded tool; Narrator smoke only. |

---

## Verified during implementation

- **The four-place rule is a five-place rule.** The hand-written golden
  table inside `a11y.rs`'s tests (`corpus_renders_the_shipped_strings`)
  is NOT touched by `SLATE_REGENERATE_FIXTURES=1`, and its
  length-lockstep assertion fires before the artifact test runs. The
  spec's "four places" folds it into "a11y.rs variants"; it is a
  separate positional list. (Contract 0a-2.)
- **uniffi caps enums at 256 variants**, and `A11yEvent` was already at
  197. Discovered by compile failure at 292 (one variant per shipped
  sentence). Drove CD-10 (typed nested enums for the families) and then
  CD-12 / R-0a-8 (the whole family nests under one top-level variant).
- **The wrapper cost nothing in the golden table.** Because the golden
  pins (priority, text) and the wrapper is a pure relay, only the
  artifact's `event` identities changed — which is the exact property
  that makes doing this before merge cheap and after merge expensive.
- **`CanvasRelativeDesc` needed `Eq`** and a reverse
  `From<CanvasRelativeDesc> for RelativeDesc` (as did
  `CanvasEdgeDirection`): the a11y vocabulary is the first place these
  types cross the FFI **into** core.
- **`Created group "X" ⟨relative⟩` is byte-identical** whether produced
  by mac's hand-rolled string (`AppState+CanvasActions.swift:141–144`)
  or by `createdText` with `kind: "group"` — verified character by
  character, which is why `CanvasCreated` needs no group arm.
- **The C# census proves the whole chain.** With the regenerated
  bindings, `EveryCorpusEventRendersTheCommittedIdentityTextAndPriority`
  passes for all 165 new entries: identity, rendered text, and priority
  all match the artifact through the real FFI.
- **`dotnet format --verify-no-changes` is clean solution-wide** after
  the census edit; no unrelated file was rewritten.
- **The 165 census entries were machine-transcribed from the artifact's
  Debug identities** (throwaway script, not committed) and then verified
  by the censuses themselves — the transcription risk of 165 entries ×
  two hosts is the kind of thing hand-typing gets wrong silently.

---

## Round record

Per `24_red_team_protocol.md` §Per-round record.

*(No rounds yet — PR 0a's implementation landed 2026-08-22; review
follows.)*
