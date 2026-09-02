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
a closed-set ARM of **any of the 18 nested parameter enums** — never
reaches `corpus()`. A variant that never reaches the corpus is pinned by
nothing: not the golden, not the artifact, not either census.

Task 0a-2 widened this from 5 of 18 to **18 of 18**: it covered only
`CanvasStatusNote`, `CanvasBlockedReason`, `CanvasFailedAction`,
`CanvasMutationRefusal` and `CanvasDeleteTarget`, so a new arm on any of
the other thirteen (a fourth `CanvasMode`, a third
`CanvasZoomContext`, …) shipped a string nothing pinned. The coverage
sites are scoped by (variant, field) rather than by field alone —
three different enums ride a field called `verb`, two ride `reason`,
two ride `target` — and `Option`-carried parameters unwrap through
`Some(`. A companion test,
`every_canvas_parameter_enum_is_listed_for_coverage`, asserts the table
lists exactly the `Canvas*` parameter enums this module declares, so
adding a nineteenth without a coverage site fails there rather than
going unpinned. Mutation-verified: deleting the
`CanvasZoom { context: Some(ZoomedToSelection) }` corpus entry fails
with *"CanvasZoomContext arms with no corpus entry … [\"ZoomedToSelection\"]"*.

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
through (R-D: no host re-derivation).

**Scope of the collapse (amended after round 1, M1 — the original
wording over-claimed).** The normative invariant is *every TEMPLATE that
names a card uses the one core helper*, and that holds across all 51
variants. Mac had three spellings; PR 0a deletes the two on the
ANNOUNCEMENT path and leaves the two label-class ones standing:

| Mac spelling | Class | State after 0a |
|---|---|---|
| `CanvasAnnouncer.swift:37–39` (`CanvasCardRef.phrase`, quoted) | speech | **deleted** — every announcement renders `card_ref` |
| `CanvasOutlineView.swift:335–347` (connection rows, hardcoded `kind: "text"`, title-only) | label | **deleted** — the row renders the same `CanvasConnectionTraversed` the navigator speaks, which changes the row's text (**CD-14**) |
| `CanvasOutlineView.swift:220` (the row's `accessibilityLabel`) | label | **survives** — `CanvasCardRef` moved into that file; §W-C label class (0a-13), and no vocabulary event renders a bare card reference |
| `CanvasRendererView.swift:403,447` (`node.kind == "group" ? "Group \(speakable)" : speakable`, UNQUOTED) | label | **survives, untouched by this PR** — it is the peer-name/uniqueness surface of **§W-G row L** (`CardSummary.speakable_name`, Tier 2, open until PR 0b), so deleting it here would mean inventing half of 0b's algorithm |

So: **one spelling on every announcement path, two label-class spellings
left, both with a named owner.** A PR-A implementer should expect a core
accessor for spoken card references and NOT for label-class peer names —
that arrives with §W-G row L in 0b, which is also where the outline's
surviving `CanvasCardRef` collapses.

**0a-11 — Colour names come from core.** No template embeds a preset
dictionary. `None` speaks the literal `no color` (the clear-colour
arm). The three Swift copies (`AppState+CanvasActions.swift:340`,
`:766`, `CanvasPromptSheet.swift:227`) die with the migration.

**Typed, not named (Task 0a-2).** The two events a host reaches when it
has just WRITTEN a colour — `CanvasColorSet` and `CanvasBulkColorSet` —
carry `color: Option<CanvasColor>`, core's own colour type, and
`canvas::color_name()` phrases it inside `render()`. A host cannot
spell `"red"` at that seam even by accident, which is what actually
deletes the two announcement dictionaries. The two events that REPORT a
colour core already named — `CanvasMovedTo` and `CanvasWhereAmI` — keep
`color_name: Option<String>`, because their value arrives from
`CanvasOutlineRow.color_name` / `CanvasWhereAmI.color_name` and the host
only relays it; typing those would force a host to parse a spoken name
back into a colour, which is the re-derivation the typed payload exists
to prevent. The picker's BUTTON LABELS (the third copy) are label class
and so are not in this vocabulary (0a-13); they come from a new
exported accessor, `canvas_color_name(color) -> String`, over the same
`canvas::color_name` — one table, one answer, speech and labels alike.

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

**0a-14 — Every render arm is TOTAL over its payload domain.** An arm
may not assume a cardinality its payload admits. If a `u32` count or a
`Vec` length reaches a template, the template renders grammatically at
every value that payload admits — singular/plural through
`plural` / `plural_len` / `counted` (the one definition of the rule),
and an optional clause omitted rather than emitted empty. There is **no
caller-side cardinality invariant**: a host may construct
`CanvasBulkMoved { count: 1 }` or `CanvasTracePathEnd { titles: [] }`
and get a sentence, because nothing in the type says it may not, and
PR A's Windows host will not inherit mac's incidental guards.

**Exception, by design: the two templates CR-3 pins.** `1 card match.`
and `1 unsupported item are preserved …` are shipped mac defects
migrated verbatim under the copy rule; they are preserved at every count
and are NOT fixed by this contract. They are allow-listed in the test
below as (arm, string) PAIRS, each proved to render from that arm and
nowhere else in the corpus — so a second defective template cannot hide
behind an existing excuse, and the carve-out cannot rot into a blanket
one.

**The invariant is a test, not a paragraph.**
`canvas_count_speaking_arms_have_boundary_witnesses_and_agreement`
(`a11y.rs`) holds it in four parts:

1. an **exhaustive** classifier, `spoken_cardinality`, says for every
   `CanvasA11yEvent` whether THIS VALUE speaks a count or a collection
   length. Exhaustive at every level: `..` elides only fields whose
   types cannot gain variants (`String`, `u32`, `bool`, `Vec<String>`,
   and `Option` of those — `Option`'s two arms are fixed by the
   language), so **every parameter type that CAN gain variants is
   explicitly matched, with no exception** — this module's eighteen
   closed sets plus the three reused `core::canvas` ones — and the
   compiler refuses the function when a variant OR a nested arm is
   added. "This value", not "this variant": `CanvasMovedTo` renders its
   connection count only at `Verbose`, so a terse or standard moved-to
   speaks none and cannot serve as its witness;
2. every arm it classifies must have a `corpus()` witness at exactly
   one; an arm speaking a collection length must have an
   empty-collection witness; and an arm whose zero the HOST can reach
   must have a zero witness. Host reachability is a property of the mac
   call sites, not of this crate, so it is declared per arm with its
   reason in `ZERO_REACHABLE` rather than derived;
3. those boundary renderings carry no plural noun and no plural
   agreement, except the CR-3 pairs above;
4. a source scan of this module's canvas render section: no template may
   interpolate a count immediately before a hardcoded plural noun, and
   no bare plural-noun literal may sit outside a
   `plural` / `plural_len` / `counted` call.

**What is checked, and how far it reaches.** Agreement is CHECKED at
the boundaries — one, the empty collection, and zero where a host
reaches it. Values in between are not witnessed one at a time. What
stands in for witnessing them is part 4, which routes count
interpolations through the shared helpers **to the limit of what a
lexer over this module's string literals can see** — it is now
noun-bound (a plural literal is excused only when THAT literal is an
argument of a helper call), so it is a stronger check than the
line-scoped scan 0a-1 shipped, but it is still a check over source
text, not a proof about runtime values. Parts 2–3 are retroactive: they
catch a regression in an arm that HAS a witness, while part 4 catches
the new arm that has none.

**The list of count-speaking arms lives in that test**, not here. This
paragraph deliberately states no counts: three consecutive adversarial
rounds each found a different miscount in the hand-written enumeration
that used to stand in this space (round record, rule 4), which is what a
quantified claim over a machine-enumerable corpus is worth.

Hosts mirror the rule where they compose the same clause for a LABEL:
mac's M3 inspectable value and its undo-action names route through
`CountCopy`, which is the host's one pluralization source and, like the
templates here, leaves the count ungrouped (CD-6). (Adopted after the
codex adversarial round found arms hardcoding the plural; CD-15.)

**What part 4 is, and is not (amended by PR 0b — contract 0b-16).** It
is a LEXER over this module's string literals, not a line scan and not
a Rust parser. It walks the source character by character tracking
string state and the identifier that opened each enclosing call, so
every literal is known WITH its call. Two of the three artefacts the
0a-1 version declared are now derived from the source:

- the **countable-noun list** is the set of `one`/`many` arguments the
  module's own `plural` / `plural_len` / `counted` call sites pass. A
  noun enters the list by being pluralized somewhere, not by being
  typed into the test — so a template that starts speaking a NEW noun
  correctly also starts guarding it;
- **helper provenance is bound to the noun literal**: a plural literal
  is excused only when it is an argument of one of those calls. A line
  carrying a real helper call no longer vouches for a second,
  hardcoded plural beside it — which the 0a-1 version named as its
  largest residual;
- format strings are reconstructed by the lexer's own handling of
  Rust's `\`-continuation (backslash-newline swallows the newline and
  the following indentation, exactly as rustc does), rather than by
  joining source lines, so a template split across lines is read as
  the one logical string it is, and the "put the helper call back on
  one line" FALSE POSITIVE the 0a-1 scan produced is gone.

Residual classes it still cannot see, named honestly:

- a logical format string assembled at RUNTIME — `push_str`,
  `concat!`, a `match` returning noun fragments, or a plural noun
  living in a `&str` binding that the template interpolates;
- a plural noun no helper call in this module passes, hardcoded in a
  template — the derived list is derived from what IS routed, so a
  vocabulary-wide first use of a noun is guarded only from its second
  use onward;
- non-English or irregular agreement (verbs, articles), which is what
  CR-3's two shipped defects are and why they are allow-listed by
  (arm, string) in part 3 rather than by anything part 4 can see.

**Independence of parts 3 and 4 is still not claimed.** Part 4 is
source-shaped and part 3 is render-shaped, and binding provenance to
the noun makes part 4 strictly stronger than the scan it replaced —
but "stronger" is not "independent", and this document does not say it
is. `ZERO_REACHABLE` remains the one DECLARED artefact in the whole
invariant: host reachability is a property of the mac call sites, and
no parser over this crate can derive it.

### The event enumeration (the PR's contract)

51 variants of `CanvasA11yEvent`, all reached through the single
`A11yEvent::Canvas { event }` wrapper (0a-1b); **179** corpus entries,
artifact 259 → **438**. 0a-1 landed 165; the rest are 0a-2's cardinality
boundary witnesses (contract 0a-14) — the count of those lives in the
test that requires them, not here. `V` = the `CanvasVerbosity`
parameter.
Coalescing class: `nav` / `filter` / `—` (immediate). All priorities
`Medium` unless marked **High**.

| Event | Payload | Template(s) | Pri | Class | mac site(s) replaced |
|---|---|---|---|---|---|
| `CanvasMovedTo` | `V, kind_label, title, ordinal_n, total_m, container?, connection_count, color_name?, marked` | terse `⟨title⟩` · standard `⟨card⟩, ⟨n⟩ of ⟨m⟩ in ⟨container‖canvas⟩` · verbose + `, ⟨k⟩ connection[s]` + `, ⟨color⟩` + `, marked` | | nav | `CanvasAnnouncer.swift:47–50,158–170`; `AppState+CanvasNavigation.swift:186–193`; `CanvasOutlineView.swift:399–406`; `CanvasTableView.swift:114–121` |
| `CanvasGroupEntered` | `label, count` | `Entering group "⟨label⟩", ⟨n⟩ card[s]` | | nav | `CanvasAnnouncer.swift:51,172`; `AppState+CanvasNavigation.swift:181`; `CanvasOutlineView.swift:393–394` |
| `CanvasGroupLeft` | `label` | `Leaving group "⟨label⟩"` | | nav | `CanvasAnnouncer.swift:52,173`; `AppState+CanvasNavigation.swift:183`; `CanvasOutlineView.swift:396` |
| `CanvasConnectionTraversed` | `direction, kind_label, title, label?` | `⟨Connects to‖Connected from‖Linked with⟩ ⟨card⟩[, labelled "⟨label⟩"]` | | nav | `CanvasAnnouncer.swift:55–57,175–184`; `AppState+CanvasNavigation.swift:119–123`; deletes `CanvasOutlineView.swift:335–347` |
| `CanvasTracePathEnd` | `titles[]` | `Path: A, then B. End of path — ⟨n⟩ card[s] visited.` · empty `titles` omits the `Path: …` clause: `End of path — 0 cards visited.` | | — | `AppState+CanvasNavigation.swift:152–155` |
| `CanvasMoveRelative` | `descs[], overlap?` | `Alone on the canvas` ‖ `Below "X", right of "Y"` [+ `. Overlapping another card` ‖ `. Clear of overlaps`] | | nav | `AppState+CanvasModes.swift:192–194,299–339,286–290` |
| `CanvasResizeGeometry` | `preset?, width, height, overlap?` | `⟨w⟩ by ⟨h⟩` ‖ `Resized to default size: ⟨w⟩ by ⟨h⟩` ‖ `Resized to fit to content: …` [+ overlap clause] | | nav | `AppState+CanvasModes.swift:179–181,229–233,286–290` |
| `CanvasResizeClamped` | — | `Minimum size.` | | — | `AppState+CanvasModes.swift:172` |
| `CanvasModeEntered` | `mode, object` | `⟨Mode⟩ mode — ⟨object⟩. ⟨exits⟩`, where `⟨object⟩` = `"⟨title⟩"` ‖ `⟨n⟩ card[s]` | | — | `CanvasModeController.swift:78`; `AppState+CanvasModes.swift:57–65,100–103`; `AppState+CanvasConnect.swift:96–98` |
| `CanvasModeRejected` | `active_mode` | `⟨Mode⟩ mode is active. Return to commit or Escape to cancel first.` | **High** | — | `CanvasModeController.swift:73–74` |
| `CanvasModeCommitted` | `verb, object` | `Placed ⟨object⟩.` ‖ `Resized ⟨object⟩.` — same `⟨object⟩` clause as `CanvasModeEntered`, `⟨n⟩ card[s]` included | | — | `AppState+CanvasModes.swift:261` |
| `CanvasModeEndedWithoutEffect` | `mode` | `⟨Move‖Resize⟩ ended — nothing changed.` ‖ `Connect ended — no target chosen.` | | — | `AppState+CanvasModes.swift:256`; `AppState+CanvasConnect.swift:103` |
| `CanvasModeCancelled` | `mode, restoration` | `⟨Verb⟩ cancelled.` [+ ` — card[s] returned.` ‖ ` — size restored.` ‖ ` — back at "X".`] | | — | `AppState+CanvasModes.swift:71–75,110–112`; `AppState+CanvasConnect.swift:110–112` |
| `CanvasCreated` | `kind_label, title, relative` | `Created ⟨lowercased card⟩ ⟨relative⟩` (no period) | | — | `CanvasAnnouncer.swift:198–208`; `AppState+CanvasActions.swift:101–105,141–144`; `AppState+CanvasCreate.swift:220–224` |
| `CanvasFileCreated` | `name` | `Created canvas "⟨name⟩".` | | — | `AppState+CanvasActions.swift:301–303` |
| `CanvasConnectedCardCreated` | `relative, origin_title` | `Created connected card ⟨relative⟩ — connected from "⟨origin⟩".` | | — | `AppState+CanvasExtras.swift:84–88` |
| `CanvasConnected` | `from_title, to_title, label?` | `Connected "⟨a⟩" to "⟨b⟩"[, labelled "⟨l⟩"].` | | — | `AppState+CanvasConnect.swift:62–64` |
| `CanvasConnectionUpdated` | `label?` | `Connection updated[, labelled "⟨l⟩"].` | | — | `AppState+CanvasConnect.swift:181–183` |
| `CanvasMovedIntoGroup` | `label` | `Moved into group "⟨label⟩".` | | — | `AppState+CanvasActions.swift:472` |
| `CanvasRemovedFromGroup` | `label` | `Removed from group "⟨label⟩".` | | — | `AppState+CanvasCreate.swift:324–325` |
| `CanvasColorSet` | `title, color?` | `Set "⟨title⟩" to ⟨color‖no color⟩.` | | — | `AppState+CanvasActions.swift:340,348–349` |
| `CanvasRenamedGroup` | `label` | `Renamed group to "⟨label⟩".` | | — | `AppState+CanvasActions.swift:370–371` |
| `CanvasCardUpdated` | `title` | `Updated "⟨title⟩".` | | — | `AppState+CanvasCreate.swift:97` |
| `CanvasCardRetargeted` | `title, path` | `"⟨title⟩" now points at ⟨path⟩.` | | — | `AppState+CanvasCreate.swift:270–271` |
| `CanvasCardPlaced` | `verb, title, relative` | `⟨Moved‖Duplicated⟩ "⟨title⟩" ⟨relative⟩.` | | — | `AppState+CanvasActions.swift:538–541`; `AppState+CanvasExtras.swift:197–200` |
| `CanvasCardAligned` | `title, target_title` | `Aligned "⟨a⟩" with "⟨b⟩".` | | — | `AppState+CanvasActions.swift:608–610` |
| `CanvasConvertedToNote` | `path` | `Converted to note ⟨path⟩. The card now points at it.` | | — | `AppState+CanvasExtras.swift:353–355` |
| `CanvasDeleted` | `target, V, undo_chord` | `Deleted ⟨card⟩` ‖ `Ungrouped Group "⟨l⟩" — cards kept` ‖ `Deleted ⟨n⟩ card[s]` ‖ `Deleted connection ⟨to‖from‖with⟩ "⟨t⟩"[, labelled "⟨l⟩"]`; **+ ` — ⟨undo_chord⟩ to undo` at standard+** | | — | `CanvasAnnouncer.swift:63,189–192`; `AppState+CanvasActions.swift:325–329,751–753`; `AppState+CanvasConnect.swift:127–131,148–149` |
| `CanvasBulkMoved` | `count, relative` | `Moved ⟨n⟩ card[s] ⟨relative⟩.` | | — | `AppState+CanvasActions.swift:563–566` |
| `CanvasBulkColorSet` | `count, color?` | `Set ⟨n⟩ card[s] to ⟨color‖no color⟩.` | | — | `AppState+CanvasActions.swift:766,776–777` |
| `CanvasGrouped` | `count, label` | `Grouped ⟨n⟩ card[s] into "⟨label⟩".` | | — | `AppState+CanvasActions.swift:817–821` |
| `CanvasBulkDuplicated` | `count` | `Duplicated ⟨n⟩ card[s] — one undo restores.` | | — | `AppState+CanvasExtras.swift:202–203` |
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
(the golden table — 179 new rows),
`committed_corpus_artifact_matches_the_vocabulary` (artifact round-trip,
438 entries), `every_canvas_variant_and_arm_is_represented_in_the_corpus`,
`every_canvas_parameter_enum_is_listed_for_coverage` (0a-2),
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
`apps/slate-mac/Tests/SlateMacTests/`: `A11yCorpusCensusTests` (all 179
canvas entries through the real FFI), `CanvasAnnouncerTests`
(coalescing, the flush/supersede rule, the priority relay, and the
WIDENED funnel guard), `A11yResidueCensusTests` (`pinnedResidueSites`
30 → **29**).

---

## PR 0b — the canvas structural rules move to core

**Goal (spec §PR 0b).** §W-G rows B–M become pure core queries over the
existing `CanvasModel`: containment, trace path, reading-order
projection, relative description, auto-sides, bounds, group geometry,
the filter predicate, the speakable name and id minting. Task 0b-1 is
the Rust + FFI + Windows-harness half; Task 0b-2 is the mac
consumption half (every Swift copy named in §2 deleted there).

### Contracts

**0b-1 — One derivation per structural rule.** Every rule §2 rows B–M
names lives once, in `crates/slate-core/src/canvas/queries.rs`, as a
pure function of the derived model (plus `placement.rs` for the
constants and `model.rs` for `CardSummary.speakable_name` /
`CardSummary.target`, which are part of the derivation itself). The
session layer adds handle-based methods that resolve the handle and
delegate; `slate-uniffi` adds 1:1 mirrors with no logic. Nothing in a
host recomputes any of them (R-D). **Task 0b-2 deleted the Swift
copies** — §W-G rows B–M all read "closed", with the per-row before/after
counts in "Verified during implementation"; the Windows host had none to
delete because it has not been written yet, which is the point of
ordering 0b before PR C.

**0b-2 — The query surface.** Handle-based unless the row says
otherwise; every one of them reads `OpenCanvasState.model` (the one
exception, `speakable_name` on the two SQLite-served row types, is
0b-6).

| Query | Shape | Reads |
|---|---|---|
| `canvas_parent_of` | `(h, node) -> Option<String>` | `tree.parent` |
| `canvas_children_of` | `(h, group) -> Vec<String>` | `tree.children` (sibling order) |
| `canvas_order_nodes` | `(h, ids) -> Vec<String>` | `reading_order` |
| `canvas_trace_path` | `(h, node) -> Vec<CanvasTraceHop>` | `adjacency` + `summaries` |
| `canvas_describe_relative` | `(h, rect, exclude) -> Vec<CanvasRelativeDesc>` | `spatial` + `summaries` |
| `canvas_bounds` | `(h) -> Option<CanvasRect>` | `SpatialIndex::bounds` |
| `canvas_group_rect_around` | `(h, members) -> Option<CanvasRect>` | `SpatialIndex::rect_of` |
| `canvas_place_inside_group` | `(h, group, w, h, exclude) -> CanvasInsideGroupPlacement` | `spatial` + `tree` |
| `canvas_filter` | `(h, query) -> Vec<String>` | `reading_order` + `summaries` |
| `canvas_auto_sides` | `(from: CanvasRect, to: CanvasRect) -> CanvasSidePair` | **free function** — geometry only |
| `canvas_constants` | `() -> CanvasConstants` | **free function** — `placement.rs` |
| `canvas_new_id` | `() -> String` | **free function** — OS randomness |

**0b-3 — `canvas_auto_sides` is keyed by RECTS, not node ids**
(controller ruling R-0b-1; CD-16). `dx = to.centre.x − from.centre.x`,
`dy = to.centre.y − from.centre.y`; `|dx| > |dy|` **strictly** ⇒
horizontal (`dx > 0` ⇒ `(Right, Left)`, else `(Left, Right)`);
otherwise vertical (`dy > 0` ⇒ `(Bottom, Top)`, else `(Top, Bottom)`).
The tie (`|dx| == |dy|`, including the diagonal and the self-loop
`dx == dy == 0`) therefore resolves **vertical**, and `dy > 0` is false
at zero, so a self-loop answers `(Top, Bottom)`. Mac's two copies
(`AppState+CanvasConnect.swift:24–33`,
`CanvasRendererView.swift:582–600`) were verified to agree by
substitution — this is a duplication deleted, not a defect fixed. The
renderer's per-endpoint `case nil` arm needs no second entry point: the
side for one endpoint is `auto_sides(that_rect, other_rect).from`.

**0b-4 — `canvas_constants()` and `canvas_new_id()` take no handle**
(R-0b-2; CD-17). `CanvasConstants` carries `grid_step`,
`grid_step_large`, `default_card_w/h`, `default_group_w/h`,
`default_gap`, `min_card_size`, and its every field is the
`placement.rs` constant of that name — never a re-typed literal, which
a test asserts field by field. `MIN_CARD_SIZE = 40.0` and
`DEFAULT_GROUP_WIDTH/HEIGHT = 400.0/300.0` are new there; the rest
already existed. `canvas_new_id()` is the first 16 hex characters of a
lowercased v4 UUID — 16 lowercase hex, `'4'` at index 12, 60 bits of
entropy — with **no** collision check against the canvas (mac parity),
pinned by a shape test over many draws rather than by a golden value.

**0b-5 — `speakable_name` is one algorithm, on `CardSummary`.** It is
`display_title` when that spelling is free, and
`⟨display_title⟩ ⟨k⟩` for the k-th node (k ≥ 2) in **document order**
that wants an already-assigned spelling — skipping any ordinal whose
spelling is a real `display_title` of some node. That skip is core's
existing `taken` guard from the untitled-ordinal loop.

**What it buys, stated exactly** (an earlier draft of this row, and the
research report it came from, said mac's loop yields a DUPLICATE on
`A`, `A`, `A 2`; that is wrong, and the correction is recorded rather
than quietly edited). Mac's loop increments until the candidate is
unused, so its names ARE unique. What it does not check is the set of
real titles, so a generated ordinal can spell some OTHER card's actual
title: `A`, `A`, `A 2` becomes `A`, `A 2`, `A 2 2` on mac, and a Voice
Control user who reads `A 2` on the third card and says it selects the
second. Core's guard makes that unreachable — `A`, `A 3`, `A 2` — at
the cost of an ordinal that "skips a number", which is exactly the
trade core's `Untitled N` allocator already made for the same reason.
Untitled cards are unchanged: `display_title` = `Untitled N` from that
same allocator.

Both properties are census assertions rather than claims here —
uniqueness, and separately that no generated name spells another card's
display title. Uniqueness alone would not have caught a missing guard,
because mac's names satisfy it too. The `A`, `A`, `A 2` document order
is a committed fixture (`cycle.canvas`), with its reverse pinned beside
it so the answer cannot pass for the wrong reason.

**0b-6 — `speakable_name` reaches the outline and table by an
in-memory join, not a schema bump.** `canvas_outline` /
`canvas_table_rows` keep their single indexed `SELECT` over
`canvas_nodes` unchanged; `speakable_name` is added to their row
records from the handle's model after the query returns.

**What the join actually does**, rather than what "join" suggests: one
`node_id → speakable_name` `HashMap` is BUILT PER CALL from
`OpenCanvasState.model.summaries` — a linear pass and two `String`
clones per node — and then hit once per row. It is a copy, not a
borrow, because the alternative is holding the registry lock across the
SQLite read, which would invert this module's canvases → conn order.
That is the same order of work as the row materialization it feeds, so
the §K "one indexed query" budget and the query's plan are untouched;
it is not free, and this row says so rather than implying a bare
lookup.

**Rationale for the decision the spec does not make:** (1) a derived
column would put a second copy of the algorithm's OUTPUT in the index,
where it can rot against the model the same rows are derived from —
0b-1 exists to stop exactly that; (2) a column costs a migration plus
the upgrade fence (`c559810`) plus a full canvas reindex on upgrade,
for data that is already in memory whenever a handle is open; (3) no
existing row field changes, so no committed golden or host binding
moves.

**The limit of (2), stated exactly.** Holding a handle guarantees a
MODEL. It does not guarantee that the model and the rows agree about
which nodes exist: `canvas_nodes` is scanner-managed, and a rescan
rewrites it while the handle stays open on its open-time snapshot. An
external edit that changes a node id, followed by a rescan, leaves rows
whose ids the snapshot never had — the join misses. Those rows fall
back to **the row's own title**, never to the empty string: an empty
speakable name is a card Voice Control cannot address at all, and
addressability is what this field exists for. The fallback gives up
UNIQUENESS for the skewed rows, not addressability, and the host
recovers both by reopening the handle on the change event.
`speakable_name_falls_back_to_the_title_when_the_rows_outrun_the_handle`
materializes exactly that skew — external write plus rescan with the
handle open — and asserts the title, not `""`.

The cost is that the two row types are no longer pure projections of
one `SELECT`; that cost is named here rather than hidden.

**The model-backed surfaces do not skew the same way — they refuse.**
`CanvasSceneNode` and `CanvasWhereAmI` read the new field off the
model they are already derived from, so their `speakable_name` can
never disagree with their own `title`; there is no fallback arm on
those two. What the same skew does to them instead is make them
UNREACHABLE for the affected ids: under an external write plus rescan,
`canvas_where_am_i` answers `bad_node` for a node the outline rows name
and `canvas_scene` simply does not list it. That is pre-existing
handle-snapshot behaviour, not something `speakable_name` introduced —
it is stated here because "cannot skew" reads as "is fine", and a host
walking outline rows into `canvas_where_am_i` will see the refusal.
The recovery is the same one 0b-6's fallback assumes: reopen the handle
on the change event.

**0b-7 — Relative description reproduces mac, with its latent
nondeterminism pinned** (CD-19). Candidates are every node that is not
in `exclude` and not a group. For each, `dx`/`dy` are the centre delta
**from the subject rect to the candidate**, and the phrase describes
where the SUBJECT sits, so the sense is inverted: vertical when
`|dy| >= |dx|` (ties vertical), then `dy < 0` ⇒ `Below`, else `Above`;
horizontal, `dx < 0` ⇒ `RightOf`, else `LeftOf`. The nearest candidate
by squared distance yields the first phrase; the first later candidate
whose axis classification differs yields an optional second. Mac sorts
with `sort(by:)`, which is **not stable**, so equidistant candidates
ordered arbitrarily run to run; core's order is `(squared distance,
document index)` with non-finite distances sorting last (the
`canvasSafeInt` precedent). An empty candidate set returns an EMPTY
list — `Alone on the canvas` is 0a's `CanvasMoveRelative` rendering of
that empty list, not a string this query invents.

**0b-8 — Containment queries expose the tree core already derives**
(CD-18). `canvas_parent_of` is `tree.parent`; `canvas_children_of` is
`tree.children`, already sibling-sorted `(y, x, document index)`, and
is empty for a childless group or a non-group node. Both reject an
unknown node id with `bad_node`, which is how a caller tells "no
parent" from "no such node". Core's containment rule — strict centre
containment, smallest area wins, **equal areas resolve to the LATER
document order**, group-in-group made acyclic by requiring the parent
to be strictly greater in the `(area, doc)` total order — is
authoritative where mac's three copies disagreed; mac's `min(by:)`
kept the FIRST equal-area group.

**0b-9 — `canvas_trace_path` is the greedy first-unseen walk.**
Eligible neighbours are `Outgoing` and `Bidirectional` only —
`Undirected` (no arrowheads at either end) is not traversable and is
excluded, exactly as mac has it. Neighbours are visited in edge
document order, the seen set is keyed by NODE, and the walk stops at
the first node whose eligible neighbours are all seen, so it terminates
on any canvas and a self-loop ends the walk at once. The returned hops
EXCLUDE the start node (a dead end returns an empty list; mac's
`visited.count` is `hops.len() + 1`), and each carries
`edge_id, node_id, title, label` with `title` = `display_title`.
Unknown node ⇒ `bad_node`. Cycle fixtures are committed
(`tests/fixtures/canvas/cycle.canvas`).

**0b-10 — `canvas_order_nodes` is a projection, not a filter with
opinions.** One pass over `reading_order` keeping ids present in the
input set: unknown ids are dropped **silently** (mac drops stale marks
the same way, and an error would make a stale mark fatal), duplicates
collapse to the single reading-order position, and an empty input gives
an empty output. O(N + M), never O(N·M).

**0b-11 — Bounds and group geometry.** `canvas_bounds` is
`SpatialIndex::bounds` verbatim — every node including group frames,
`None` on an empty canvas. `canvas_group_rect_around` is the union of
the resolvable members' rects inflated by `DEFAULT_GAP` on all four
sides; **mac's literal `pad = 40.0` IS `placement::DEFAULT_GAP`**, and
a test asserts that identity rather than restating the number. Members
that are not in the canvas are skipped, and a set with no resolvable
member returns `None` — mac's `guard minX.isFinite else { return }`
typed rather than left as a silent no-op (CD-24).

**0b-12 — `canvas_place_inside_group` is clipped to the group and has
a typed no-room outcome** (CD-21). Candidate slots form a lattice
anchored at `(g.x0 + GRID_STEP, g.y0 + 2·GRID_STEP)` — mac's
`(x + 20, y + 40)` inset — stepping by `ceil_to_grid(w + DEFAULT_GAP)`
in x and `ceil_to_grid(h + DEFAULT_GAP)` in y, and only slots lying
fully inside the group rect are candidates. They are visited COLUMN by
column, each column top to bottom — `place_new`'s `Below` before
`RightOf` preference applied to a lattice instead of a ring, which is
a recorded divergence from the ruling's literal wording and is
CD-25; `Above`/`LeftOf` are unreachable because the lattice starts at
the group's inset top-left, which is what clipping to the group means.
The outcome is one of three, so a host never receives a point that is
outside the group it asked about:

| Outcome | When |
|---|---|
| `Placed { x, y }` | a candidate slot is free of card overlap (`exclude` honoured; group frames never block, as in `place_new`) |
| `TooSmall { x, y }` | **no** candidate slot fits inside the group — the fallback point is the inset itself, unchecked for overlap |
| `Full` | slots fit but every one examined is occupied |

The scan examines at most `placement::RING_LIMIT` candidates, the same
budget `place_new`'s ring search spends, so a pathological group cannot
make the query unbounded.

Two id errors are refused before any geometry runs, and they are
DIFFERENT errors because they send a caller to different fixes: an id
that is not in the canvas is `bad_node`, and an id that IS in the
canvas but is not a group is `not_a_group`. A card's rect is not a
container, and answering geometry for one would hand the caller a
position "inside" something that cannot hold it — PR E is the first
consumer, so the refusal is cheaper now than the confusion later.

**API note for PR E — the two refusals are two MESSAGES, not two
variants.** Both are `VaultError::InvalidArgument { message }`, like
every other canvas id error, so across the FFI a host receives one
error case whose text differs. **A host cannot discriminate them
without matching on the string, and no host should.** PR E's guidance
is to treat both as one refusal — the id the user picked cannot take a
card — and say so once. If a future surface genuinely needs to tell
them apart (offering "create a group here" only for the second, say),
that is an escalation: a typed variant or a machine-readable code on
the error, decided then, not a `contains("not a group")` in a host.
`place_inside_group_refuses_a_node_that_is_not_a_group` asserts the
two messages stay distinct so the escalation remains possible; it is
not a licence to parse them. The mac consumer added in Task 0b-2
relies on neither: `canvasMoveIntoGroup` only ever receives ids from a
picker built out of `kind == "group"` rows, and its `catch` announces
one `CanvasActionFailed` either way.

**0b-13 — `canvas_filter` matches mac's field set, in reading order.**
The needle is the query trimmed of whitespace; an **empty needle
matches EVERYTHING** (mac's `filteredOutline` short-circuits to the
unfiltered list, and a `canvas_filter("")` returning `[]` would invert
every consuming UI) — that is the single easiest thing to get
backwards, so it is pinned by its own test. A non-empty needle matches
if it is contained, case-folded, in the node's `display_title`, its
`kind` type word, ANY ONE element of its `group_path`, or its `target`.
Consequences that are mac behaviour and are preserved deliberately, not
oversights: typing `group` selects every group and `link` matches every
link card *and* any title containing "link"; and because the group path
is matched per element, a needle spanning the `›` separator never
matches. `target` comes from `CardSummary.target`, the same derivation
`canvas_table_rows` serves and `canvas_db` writes, so the filter and
the table cannot disagree about what a card points at.

**0b-14 — Case handling diverges from Foundation AND from case
folding, by a recorded amount** (CD-22). Core lowercases both sides
with `str::to_lowercase` and tests containment. That is Unicode's
`Lowercase_Mapping`: locale-independent, and **full** in the sense that
one scalar may become several — but it is NOT a case fold, simple or
otherwise, and the earlier phrase "full Unicode simple lowercase" was
self-contradictory. Three rules, three answers:

- vs mac's `localizedCaseInsensitiveContains` (locale-sensitive, no
  full mapping): they differ on the Turkish dotless `ı` and on `İ`
  (U+0130), whose Rust lowering is `i` + combining dot above, so a
  plain `istanbul` needle does not match it;
- vs a case FOLD: they differ on `ß`, which folds to `ss` but
  lowercases to itself, so `strasse` does not match `Straße` here.

All three agree across ASCII and Latin-1, which is the entire fixture
corpus, so the §W-A census cannot see any of it. Core trims with Rust's
`trim` (all Unicode whitespace, newlines included) where Swift trimmed
`.whitespaces` (no newlines). Both divergences are pinned by tests
(`filter_folds_case_the_unicode_way_not_the_turkish_way`,
`filter_lowercases_rather_than_case_folds`), so they are witnessed
rather than asserted.

**0b-15 — The §W-A `canvas_queries` section.** The harness gains a
canvas pass: a SECOND temp vault holding only the `.canvas` fixtures
(so no existing golden's vault contents change), one artifact
`canvas_queries.json`, one object per fixture in filename order,
carrying `bounds`, then per node in reading order
`speakable_name` / `parent` / `children` / `trace`, then the filter
results for a fixed query list, then `describe_relative` for a fixed
rect list. The query and rect lists are `public static readonly` data
on `SurfaceSerializer` so the Swift twin copies one list; the twin is
Task 0b-2's and is named in a comment beside them.
`large_2000.canvas` is deliberately NOT in the artifact: it is the §K
performance fixture, and at 2,000 nodes its rows would commit a
golden no reviewer reads. Its coverage is the in-crate census, which
runs the same queries over it. The mac parity census was RED between
Task 0b-1 and Task 0b-2 — the golden landed with the Windows twin and
the Swift twin that reproduces it is 0b-2's, the same sequencing 0a
used for the C# corpus mirror. **Task 0b-2 landed that twin**
(`ParityHarnessTests.canvasQueriesArtifact`, with its own temp vault
and its own copies of the three pinned lists), so the deliberate gap is
closed and the artifact is produced from both sides. The mac lane runs
only in CI, so this artifact is where the twins' equality is actually
decided — as with every other §W-A section, the committed golden
arbitrates and the Swift half is unproven until that lane runs.

**0b-16 — The totality parser guard replaces 0a-14's interim scan.**
Contract 0a-14 part (d) was a line-scoped lexical scan with two
DECLARED artefacts — a hand-written list of countable nouns and
line-wide helper provenance. Both are now derived. The guard lexes the
canvas render section into string literals with their enclosing call,
so: the countable-noun list is **the set of `one`/`many` arguments
every `plural` / `plural_len` / `counted` call site in the module
actually passes** (a noun enters the list by being pluralized
somewhere, not by being typed into the test); and a plural literal is
excused only when THAT literal is an argument of one of those calls —
a line carrying a real helper call no longer vouches for a second,
hardcoded plural sitting beside it, which 0a-14 named as its largest
residual. Format strings are reconstructed by the lexer's own
`\`-continuation rule rather than by joining source lines, so a
template split across lines is read as the one logical string it is.
`ZERO_REACHABLE` stays declared — host reachability is a property of
the mac call sites, not of this crate, and no parser over this crate
can derive it. 0a-14's residual list is narrowed accordingly, in
place.

**0b-17 — Rust parses mac's coalescing switch.** 0a-8 pins the
`navigation` / `filter` class membership in one Rust doc comment and
mac's `CanvasAnnouncer.coalescingClass(of:)` copies it; nothing checked
that the copy was still faithful. A `slate-uniffi` test now parses both
— the doc comment's `[`CanvasA11yEvent::…`]` links and the Swift
switch's case lists — and fails `cargo test` when they disagree, in
either direction. It is the same shape as the two corpus-mirror
tripwires (0a-3): a Rust test that reads a Swift file so a forgotten
host edit fails in the fast lane rather than after a full
`generate-bindings` + `dotnet test` round trip.

> **Completed by PR C (contract C17), and this is where the 0b m1 ledger
> line closes.** 0b-17 shipped the MAC half here, in PR 0b — so the "m1
> coalescing-class tripwire" the C-1 brief carried as an open item was
> already done, and PR C VERIFIED it rather than re-implementing it.
> What re-reading it found is that this row's own failure sentence
> ("spoken uncoalesced on mac and coalesced on Windows") assumes the
> Windows copy is faithful, and nothing checked that. The true gap was
> the SYMMETRIC twin, which C17 lands
> (`the_windows_coalescing_switch_matches_the_pinned_class_list`), with
> the pinned-list parser factored into one function both tripwires call.
> **0b m1 reads resolved as of PR C**: mac half 0b-17, Windows half C17,
> and the pair is now the same shape 0a-3 set for the corpus mirrors.

### Tests that pin PR 0b (Task 0b-1)

`crates/slate-core/src/canvas/queries_tests.rs`: per-query behaviour
tables over the committed fixtures (ties, empties, cycles, Unicode),
the `redteam_*` hostile-geometry cases, and censuses over
`model_tests`' random-canvas generator (trace path terminates and never
repeats a node; `order_nodes` is the reading-order projection of the
input set; `filter` output is a reading-order subsequence and the empty
query returns everything; `speakable_name` is injective).
`crates/slate-core/src/canvas/placement_tests.rs`: the constants-source
test and the inside-group placement table.
`crates/slate-core/src/canvas/model_tests.rs`: `speakable_name` /
`target` derivation, including the `A, A, A 2` fixture.
`crates/slate-core/src/a11y.rs`:
`canvas_count_speaking_arms_have_boundary_witnesses_and_agreement`
part (d), now the parser (0b-16).
`crates/slate-uniffi/src/lib.rs`: the FFI-mirror coverage test and
`the_mac_coalescing_switch_matches_the_pinned_class_list` (0b-17).
`apps/slate-windows/tests/SlateWindows.Tests/Censuses/ParityHarnessCensus.cs`:
the `canvas_queries.json` golden, byte-for-byte.

### Tests that pin PR 0b (Task 0b-2, the mac half)

`apps/slate-mac/Tests/SlateMacTests/CanvasFFITests.swift`: one test per
query family over the `t.canvas` fixture — the containment pair with
both its refusals, `order_nodes`' silent drop and collapse,
`trace_path`'s start-excluding hops, `filter`'s empty-matches-everything
rule and its four fields, `describe_relative`'s inverted sense, bounds,
the padded group rect, `place_inside_group`'s lattice slot and BOTH id
errors, and `speakable_name` on all four record types — plus one for
the three handle-free exports (`canvas_constants` field by field,
`canvas_new_id`'s shape over many draws, `canvas_auto_sides` including
the self-loop tie).
`apps/slate-mac/Tests/SlateMacTests/ParityHarnessTests.swift`: the
`canvas_queries` section's Swift twin, asserted against the same
committed golden the Windows census uses.
`apps/slate-mac/Tests/SlateMacTests/CountCopyTests.swift`: the
boundary between the host's ungrouped `counted` and core's exported
`count_noun` — identical below 1000, different at and above it
(CD-6's carried half, CD-26).
`apps/slate-mac/Tests/SlateMacTests/CanvasRendererTests.swift`:
speakable-name uniqueness, and CD-20's renumbering as the one moved
expectation.
`apps/slate-mac/Tests/SlateMacTests/CanvasNavigatorTests.swift`:
`testOnlyTheBatchRetargetWindowPairsReadyWithNoHandle` (the one
`.ready`-with-no-handle window: movement still narrates, mutations
still refuse audibly, and every VA-1 member — the four structural verbs,
Where-am-I and follow-connection — speaks rather than going quiet),
`testWhereAmIAndFollowConnectionAreUnchangedWithALiveHandle` (the same
two OUTSIDE the window, including a WARM adjacency cache inside it,
which must still give the real dead-end phrase — VA-1 must not
over-fire),
`testFilterInTheReopeningWindowNeverAnnouncesAWrongCount` (the filter
family's three rules and the displayed-equals-announced invariant) and
`testEveryNonReadyLoadStateAnswersWhatTheMappingSays` (every non-ready
state — `.loading`, `.degraded`, `.failed`, `.retargetFailed` —
materialized through its real lifecycle method, each driving every VA
member, and every expectation taken FROM `canvasReadRefusal(for:)`
rather than written beside it, so the test cannot drift from the
mapping) and
`testFollowConnectionWithoutASelectionAnswersLikeItsSiblings` (the
selection question, answered the same on a ready canvas as its
siblings answer it).

---

## PR A — the canvas document, the tab, and the outline

**Goal (spec §PR A).** Opening a `.canvas` shows a real surface: a
per-path document view model over the FFI, the outline projection as a
UIA tree, the load states of t0 §5, canvas-open announcements through
the PR 0a vocabulary, and focus landing. Default landing is the outline
(t2 #369 decision 3). The table (PR B) and visual (PR D) projections
are commands here and projections later.

### Contracts

**A1 — One document per open path; the registry owns its lifetime.**
`WorkspaceViewModel` keys canvas documents by the byte-exact
vault-relative path (`Ordinal`, `"canvas:" + path`) exactly as it keys
Bases documents (`WorkspaceViewModel.cs`, `_baseDocuments` /
`BaseDocumentFor`), so every tab and every pane showing that path holds
the SAME `CanvasDocumentViewModel` and therefore the same
`CanvasSelection` (R-B). Release is the Bases sweep, not a hand-kept
integer: `ReleaseUnreferencedCanvasDocuments()` runs at every tab-close
funnel, computes the live key set from the open tabs, and shuts down
every document no tab references — which closes `close_canvas` and
drops the selection and the marks with the object (spec behavior 1's
"marks cleared when the last tab closes"). `Dispose` shuts the whole
registry down and drains its closes into the same bounded teardown
drain the Bases documents use (INV-2's Windows twin).

**A registry HIT must not serve stale content.** The registry keys by
path and a hit returns the document as it stands, which is right for
sharing and wrong the moment the bytes change underneath it. The
attach funnel's fifth site — `ReloadOpenTabFromDisk`, the history
restore's — is the reachable one today: it re-attached the same
document and the outline went on rendering the PRE-restore rows,
directly after this shell announced the restore. So the canvas arm of
that site reloads explicitly (`ReloadCanvasDocumentAt`). Selection
survives wherever the node id still exists, because `PublishReady`
re-seats only when the selected node is gone. Pinned by
`RestoringAVersionReloadsAnOpenCanvasTab` and
`TheReloadKeepsTheSharedDocumentObject`, and mutation-verified: dropping
the reload call brings the stale rows back.

**A rename lands new bytes at the DESTINATION, too.** Two shapes reach
it: the atomic save (write `x.tmp`, rename it onto the open
`board.canvas` — the source was never open, so the registry's re-key
loop does nothing at all), and both-open, where the re-key points the
source's tabs at the destination's existing document without re-reading
it. `RetargetCanvasDocuments` therefore reloads the destination after
the re-key, so the surviving document is the one that re-reads. Pinned
by `ARenameOntoAnOpenCanvasReloadsTheDestination` and
`ARenameWithBothPathsOpenReloadsTheDestinationAndRetargetsTheSource`.

**Scope, stated honestly.** The fact drives the reload SITE, not the
whole History restore: W4-7's restore carries its own preconditions for
a non-markdown tab (the CAS basis is the history head hash, not a tab
buffer hash), and a `.canvas` tab does not satisfy them end to end in
this harness. Whether History should restore a canvas at all is W4-7's
question and is recorded in "verified during implementation" rather than
answered here. The Bases and dashboard arms of the same site have the
same registry-hit hole at BASE; that is pre-existing and not this PR's
to fix, but PR A adopted the shape into new code at the exact site whose
enumeration was a controller ruling, which is why the canvas arm is
closed now.

**"Refcount" is the sweep's live-key set, stated exactly.** The spec
says refcount; this registry has no counter, because the Bases
precedent proved a counter and a tab list can disagree and the tab list
is the one a user can see. Everything the spec asks a refcount for is a
property of the sweep: 0→1 is the `CanvasDocumentFor` miss that
constructs and loads (A4 hangs the once-per-open announcement there),
and N→0 is the sweep finding no live tab. Pinned by
`OneDocumentIsSharedByEveryTabOnThePath`,
`TheLastTabClosingReleasesTheDocumentAndItsMarks`.

**A2 — The attach funnel has FIVE call sites, and the list is derived.**
Controller ruling. The funnel's doc comment — it was named
AttachBaseDocumentIfNeeded before this PR renamed it, so that spelling
is history rather than a citation
(`WorkspaceViewModel.Bases.cs:1042–1046`) — named four — "AddTab, restore,
duplicate, and the in-place REPLACE arm" — and there were five: the
active-tab replace arm in `TryOpenItem` (`Layout.cs:161`), `AddTab`
(`Layout.cs:210`), `DuplicateActiveTab` (`Layout.cs:441`), `RestoreNode`
(`Persistence.cs:83`) and `ReloadOpenTabFromDisk` (`History.cs:237`).
The W4-6 round-1 lesson was that two sweeps missed sites; a comment one
site behind is the same failure in slower motion. This PR renames the
funnel `AttachTabDocumentsIfNeeded` (it attaches Bases, dashboards and
now canvases), fixes the enumeration, and replaces "trust the comment"
with a Roslyn twin: `TheAttachFunnelDocCommentNamesEveryCallSite` parses
the three partial files, collects the enclosing method of every
invocation, and asserts the comment's named set EQUALS the derived set
in both directions. A sixth site that forgets the comment fails; a
comment naming a site that no longer exists fails too.

**A3 — Five load states, and `degraded` is the PARSE-ERROR one.**
`CanvasLoadState { Loading, Ready, ParseError, Failed, RetargetAbsent }`
is the mac `CanvasDocument.LoadState` twin
(`apps/slate-mac/Sources/SlateMac/Canvas/CanvasDocument.swift:174–186`)
with mac's `.degraded` renamed, because on Windows two different things
were both called degraded and the reviewer has to be able to tell them
apart:

| Source fact | State | What the user gets |
|---|---|---|
| `open_canvas` threw | `Failed` | the failure message, retry-able |
| `CanvasOpenInfo.degraded` | `ParseError` | the `ParseFailed` warning's detail; handle closed at once; read-only by construction |
| open succeeded | `Ready` | rows; the A4 banner rides on top when any entry was skipped |
| a retarget's reopen threw | `RetargetAbsent` | the message names both spellings |
| before the first publish | `Loading` | the spinner label |

The three FAILURE states put the banner in the tab order with
`LiveSetting=Assertive` — t0 §3's error-region discipline is "focusable,
never announcement-only", and a plain `TextBlock` satisfies neither
half. `Loading` does not: it is transient, and a tab stop that vanishes
under the cursor is its own defect. Pinned by
`TheFailureBannerIsAFocusableRegion`.

`canvas::is_load_degraded` (`crates/slate-core/src/canvas/mod.rs:356`)
is `any(ParseFailed)`, and every `ParseFailed` arm of `canvas::parse`
returns `Canvas::default()` — so a degraded open carries **no** skipped
entries, and rendering `CanvasLoadedDegraded { skipped }` for it would
speak a count of zero for a file that was not loaded at all. t0 §5
agrees: its sentence is introduced as "Parse warnings (#359 tolerant
contract)", i.e. the warnings, not the flag. Mac reads it the same way —
`preservedItemCount` counts `.skippedEntry`
(`CanvasDocument.swift:486–490`) and the banner renders only in
`.ready` (`CanvasContainerView.swift:354`). The spec's PR A behavior
row 2 folds the two facts into one sentence; this row unfolds them and
takes mac's shipped shape, which is the reference implementation for
both. (CD-28.)

**A4 — `CanvasLoadedDegraded` is announced once per document OPEN.**
Controller ruling on CD-3's Windows reading: the announcement belongs to
the registry's 0→1 transition (A1), not to a mounted view. It is
therefore posted by `CanvasDocumentViewModel` at the end of the publish
that reaches `Ready`, behind a one-shot the `Load()` entry point resets
— so a reload re-announces (a reload IS an open) and a second pane
mounting the same document does not. Mac's one-shot is a view-level
`@State` (`CanvasContainerView.swift:470–487`), which is per-container;
the mac per-container behaviour stays a recorded platform note (CD-29).
`skipped` is the SkippedEntry warning count, and the banner renders the
SAME event through `A11yRender` rather than composing a second copy —
the mac CD-3 precedent, so banner and speech cannot drift. Pinned by
`TwoPanesOnOneDocumentAnnounceTheDegradedLoadOnce` and
`TheDegradedBannerIsTheSameRenderTheAnnouncementSpeaks`.

**The footer rows are wider than the banner's count, deliberately.**
Spec behavior 2 asks for "a focusable detail row in the outline footer
listing `warnings`" — every warning, which on `malformed.canvas` is
eight where the banner's `skipped` is five. The footer lists all of
them, because a dangling connection or an ignored side is a fact about
the user's file that nothing else in this PR reports, and the banner
keeps the vocabulary's number, because that is the parameter the event
takes. The footer renders only under `Ready`: a parse error's state
message IS its single `ParseFailed` detail (A3), so listing it below
would say the same sentence twice.

**A5 — `CanvasAnnouncer` is a relay and a clock, and nothing else —
and it publishes its RETIREMENT so speakers can ask.** `IsRetired` is
the fact a composer checks before building a sentence: PR C-lite gave
the canvas one announce boundary (`CanvasDocumentViewModel.Speak`, which
the navigator and the mode stack both use) and its condition is this
predicate rather than the document's own shutdown flag, because C7's
teardown marks the document first and then drains the mode stack, whose
restoration is the last sentence a retirement owes. The `Debug.Fail`
below stays: the boundary stops the SPEAKER, and anything that still
reaches the funnel afterwards is a defect worth being loud about —
`RefusedAfterShutdownForTests` counts them so the claim is assertable in
Release too, where `Debug.Fail` is silent.
R-C. It takes a typed `CanvasA11yEvent`, wraps it in
`A11yEvent.Canvas`, renders it through `SlateUniffiMethods.A11yRender`,
and posts the rendered `(text, priority)` pair. The class keys are the
one list pinned core-side (0a-8) copied verbatim: `navigation` =
`CanvasMovedTo`, `CanvasGroupEntered`, `CanvasGroupLeft`,
`CanvasConnectionTraversed`, `CanvasMoveRelative`,
`CanvasResizeGeometry`; `filter` = `CanvasFilterCount`,
`CanvasFilterCleared`; everything else posts immediately; a `High`
canvas event cancels and DROPS both pending classes rather than
flushing them (navigation context is re-derivable by moving again).
The window is 200 ms latest-wins, each class independent. `Relay` takes
a non-canvas `A11yEvent` (the shared grid's own events, PR B) and
carries its core priority through unwrapped — the mac
`testRelayCarriesTheCorePriorityOfANonCanvasEvent` fact.

**The timers are bound to the dispatcher captured at CONSTRUCTION, and
an empty render is loud.** A `DispatcherTimer` binds to
`Dispatcher.CurrentDispatcher` of whatever thread creates it, and these
are created lazily on the first debounce of each class — so a first
navigation announcement arriving from a scheduler body would have
created a timer on a POOL thread whose dispatcher nothing ever pumps,
and the queued line would simply never fire. The announcer captures the
dispatcher in its constructor and passes it to every timer, which makes
that unreachable, and asserts in Debug that it is being driven from that
thread. Separately, the empty-render arm (mac's
`guard !rendered.text.isEmpty` twin) now carries a `Debug.Fail`: no
canvas template renders empty, and a silent drop is the worst way to
learn that one started to, because the symptom is an announcement that
does not happen.

**The announcer is retired with its document.** `Shutdown()` cancels the
timers, DROPS the pending lines rather than flushing them (the reason to
say a navigation line — the user is reading that canvas — stopped being
true), and refuses anything posted afterwards with a `Debug.Fail`,
because a post after retirement means some path outlived the document
that owns it. `CanvasDocumentViewModel.Shutdown` calls it, so every
retirement route reaches it: the release sweep, the retarget, the
vault-close drain. Without it, closing the last tab on a canvas the user
had just moved around in left a coalesced line queued on a dead document
that fired ~200 ms later, and the shell spoke about a surface that was
gone. Pinned by `NothingSpeaksAfterTheLastTabClosed`, which covers both
halves (the dropped queue and the refused late post) and is
mutation-verified against each.

**The post seam is `(text, priority)`, so the dispatcher gained an
overload.** `AccessibilityNotificationDispatcher.Post(A11yEvent)`
rendered internally, which a coalescer cannot use: the window's winner
is decided AFTER the render and the loser is dropped without ever being
spoken, so the queue holds rendered lines. `Post(RenderedAnnouncement)`
is now the primitive and `Post(A11yEvent)` delegates to it — the same
seam shape mac's announcer takes (`CanvasAnnouncer.swift`, the
`(String, NSAccessibilityPriorityLevel)` closure), for the same reason.

**A6 — The census is the funnel guard, and it reads syntax, not text.**
The Windows twin of mac's `testNoDirectAnnouncementsUnderCanvas`
(`CanvasAnnouncerTests.swift:138`) is `CanvasAnnouncerCensus`, a Roslyn
pass over every `.cs` under `src/SlateWindows/Canvas/` **recursively**
(the mac round-1 m-F lesson: `Canvas/CanvasPickers/` arrives in PR E)
with `CanvasAnnouncer.cs` as the single exempt file. It fails on any
`AccessibilityNotificationDispatcher` reference, any
`RaiseNotificationEvent`, any `A11yRender` call, and any
`A11yEvent.HostComposed` construction. Syntax, not `Contains`: a
comment naming a bypass is trivia and cannot trip the guard, and a
string literal spelling one is a literal node, not a call — the #1108
rationale that `CSharpSource` exists for. Windows has no residue census
of its own (`A11yResidueCensusTests` has no twin); this is the first,
and it is scoped to `Canvas/` deliberately rather than pretending to be
the general one.

**A7 — Verbosity is a parameter at every announce site.**
`CanvasDocumentViewModel.Verbosity` defaults to
`CanvasVerbosity.Standard` and is passed into every event that takes it
(today: `CanvasMovedTo`). PR C replaces the field with the persisted,
live-switchable preference and changes no call site. The m7 lesson is
recorded rather than papered over: **no unit guard asserts that a new
announce site threads verbosity** — a `CanvasA11yEvent` variant that
takes the parameter cannot be constructed without one, and which VALUE
it gets is not a property any source scan can see. The coverage is the
end-to-end-through-render facts (`MovingSelectionAnnouncesTheCoreRenderedMoveAtTheVerbosity`),
exactly as mac has it.

**A8 — The outline is a tree of core's rows, nested by `depth`.**
One `TreeViewItem` per `CanvasOutlineRow` in `canvas_outline` order,
re-parented by the `depth` column with a stack — the host computes no
containment (R-D; 0b-8 owns the tree). `ControlType.TreeItem` under a
`ControlType.Tree` named "Canvas outline"; `SelectionItem` and
`ExpandCollapse` come from WPF, and `Invoke` is added by the host because
WPF's tree peers implement ExpandCollapse/SelectionItem/ScrollItem and
NOT `IInvokeProvider` — putting an invokable child element inside the row
instead would violate the journeys' recorded peered-elements-only trap.

**Invoke must live on the DATA-ITEM peer, not the container peer (round
7).** WPF does not project a `TreeViewItem`'s own peer into the UIA tree;
it projects each row through WPF's tree *data-item* peer, which
implements SelectionItem/ExpandCollapse/ScrollItem ITSELF and does not
forward a custom pattern to the container. So `Invoke` added only to
`CanvasOutlineItemAutomationPeer` was real but invisible — a client saw
`invoke=NULL` on every row. `CanvasOutlineRowDataPeer` supplies it, and
BOTH item-peer factories are overridden
(`CanvasOutlineTreeAutomationPeer.CreateItemAutomationPeer` for top-level
rows and `CanvasOutlineItemAutomationPeer.CreateItemAutomationPeer` for
nested group children and connection rows). Activation routes to
`CanvasOutlineRowViewModel.RaiseActivate`, the same path Enter and
double-click take, so UIA activation cannot drift from them. Evidence:
`TheTreeItemsCarryTreeSelectionItemExpandCollapseAndInvoke`, which walks
`treePeer.GetChildren()` — the production topology a client reads —
asserting the row peers ARE `CanvasOutlineRowDataPeer` and carry all four
patterns at both levels; mutation-verified against reverting either
override. The `CanvasSurfaces_OutlineTreeSelectionAndActivation_AreClean`
journey is the CI-arbitrated half.

Virtualization is
`VirtualizingStackPanel.IsVirtualizing=true` with
`VirtualizationMode=Standard`, the W4-1 UIA-safe setting (the files
tree uses `Recycling`; a recycled container re-uses one peer for
different data, which is what the canvas outline must not do).

**A9 — The row's Name is the t0 §1.1 card reference, composed from
core's parts, and it is LABEL class.** `⟨Kind⟩ card "⟨speakable_name⟩"`,
groups as `Group "⟨speakable_name⟩"` — `kind` is core's type word
(`CanvasOutlineRow.kind`, the t0 §1.1 word) and `speakable_name` is
core's one uniqueness algorithm (0b-5). The composition is
`CanvasPhrase.CardReference`, one function, the `BasePhrase` precedent
for the label class.

**No core accessor for this exists, and 0a-10 says so.** Its scope
table records mac's outline `accessibilityLabel` as *"survives —
`CanvasCardRef` moved into that file; §W-C label class (0a-13), and no
vocabulary event renders a bare card reference"*, and tells a PR-A
implementer to *"expect a core accessor for spoken card references and
NOT for label-class peer names"*. So this is host label composition
**by designation** (0a-13's list: the outline node value, the renderer's
peer names, the degraded message and the panel headings are all §W-C
label class and deliberately outside the vocabulary), not a missed core
query. §W-G row L is its named owner. The controller ruling's phrase
"core-rendered composition" is satisfied in the sense the ruling's own
parenthesis gives it — every PART is core's — and this row states the
residue plainly rather than letting "core-rendered" imply an
`a11y_render` call that does not exist. (CD-30 records the one place
Windows and mac differ here: mac's outline label spells `title`, this
one spells `speakable_name`.)

**A10 — The row's ItemStatus is the t0 §3 inspectability slot.**
`⟨n⟩ of ⟨m⟩ in ⟨container‖canvas⟩[, ⟨color⟩][, marked][, filtered]` —
mac's `nodeValue` (`CanvasOutlineView.swift:309–318`). The `, filtered`
clause was staged: PR A had no filter to describe and this row said so,
PR C shipped one (`CanvasOutlineRowViewModel.ForNode` passes
`model.FilterActive` into `CanvasPhrase.RowStatus`), and the row was
corrected in codex round 4 rather than at the time — which is why
staged claims now have a guard of their own.
`AutomationProperties.ItemStatus` is the Windows slot for mac's
`accessibilityValue`; `TreeViewItem` supports no Value pattern, and the
spec's "ItemStatus/Value" names the pair for exactly that reason.
`HelpText` is the per-kind activation hint (mac `activationHint`,
verbatim). Every ingredient is core's except the joining commas and the
two English words `in` and `marked` — label class, A9's designation.

**A11 — Connection rows are core-rendered, under the selected card
only.** A selected row's children are its connection rows FIRST, then
its structural children — which reproduces mac's flat reading order
(`[row][its connection lines][its members]`) as a depth-first tree
walk. Their Name is `A11yRender(CanvasConnectionTraversed { direction,
kind_label, title, label })` — the same event the navigator speaks when
it traverses that connection (CD-14, and the reason mac's second copy
died). ItemStatus is `connection ⟨i⟩ of ⟨n⟩`.

**Invoke follows; ARROWING does not.** A connection row is a reading
position, not canvas selection state, so the tree's selection changing
onto one leaves the model alone — `Invoke`, Enter and double-click all
route through the one activation path, which is the same split mac
draws with `returnOpensRow`. Following on selection made the rows
unreadable: the arrow key that landed on a row immediately moved the
model to the other card, which rebuilt the selected card's children out
from under the cursor, so the direction phrase a screen reader was about
to speak was gone before it spoke it.

**And the view's re-seat must not read back as a user action.** Removing
those rows can remove the tree's CURRENT selection, and WPF answers by
re-selecting the parent container — which arrived at the selection
handler as a fresh user selection and dragged the model back to the card
the user had just left. The whole model→view apply therefore runs under
the sync guard, not just its final assignment.
`ArrowingOntoAConnectionRowLeavesItReadable` pins both halves: the row
survives with its name and status intact and nothing is announced, and
then Invoke does move.
Neighbours are `canvas_neighbors`, fetched lazily per node and cached,
cache dropped on every load — the mac `neighborsCache` shape.

**A12 — One selection, both directions, no echo.** The tree's
`SelectedItemChanged` writes `CanvasSelection.Selected`; the selection's
change event re-seats the tree. The echo is broken by a
re-entrancy flag on the view, not by comparing values, because the
value-compare version cannot tell "the model already agrees" from "the
model was set to the same node by another pane". A selection change
announces `CanvasMovedTo` at A7's verbosity, preceded by
`CanvasGroupEntered` / `CanvasGroupLeft` when the container changed —
`CanvasGroupEntered.count` is the arrived-at row's `total_m`, which is
the entered group's own card count (CD-4's fix, not the sibling count).

**The landing selection is silent.** Opening seats selection on the
first row so the tree has a selected item, and announces nothing: the
focus lands there and the screen reader reads the row it lands on, so a
`CanvasMovedTo` on top of it is the t0 §1.5 doubling rule broken at the
first keystroke of the surface's life. Pinned by
`ReadyPublishesCoreRowsAndSeatsTheFirstRowSilently`.

**A13 — Activation is per kind, and every arm speaks canvas
vocabulary.** Text ⇒ the interim read-only detail region seeded from
`canvas_node_text` (`TextBox IsReadOnly`, focusable, named for the card;
PR E swaps in the editor). File/image ⇒ routed on the TARGET, not the
kind, exactly as mac routes it
(`CanvasContainerView.swift:169–187`): a `.md`/`.markdown` target goes
to the workspace's ONE navigation seam (`OpenEditorNavigation`), with
the scene node's `subpath` mapped to a `LinkAnchor` — `#^id` ⇒
`("block", id)`, `#…` ⇒ `("heading", …)` — so the W3-5 anchor
resolution lands the caret at the heading rather than at the note top;
anything else goes to the shell as `CanvasOpened { DefaultApp }` **if
and only if it is media** (CD-38's extension gate), and a non-media
target is refused audibly and never launched. Link ⇒
`ExternalLinkPolicy.IsLaunchable` — the ONE scheme allowlist
(`http`/`https`/`mailto`) — and the injected opener, announcing
`CanvasOpened { Browser }`, `CanvasBlocked { LinkOpenFailed }` or
`CanvasBlocked { NotAUrl }`.

**Two hand-offs, two gates, and they are not the same gate.**
`ExternalLinkPolicy` decides which URLs may be handed to a browser;
`CanvasMediaPolicy` (CD-38) decides which vault FILES may be handed to
the shell. A file card does not go through the scheme allowlist and a
link card does not go through the media gate — different inputs,
different failure modes, and folding them together would let either
rule's reasoning excuse the other's.

**Routing on the KIND was a defect, and the vocabulary said so.**
`image` fell into the `file` arm, and `ItemForPath` calls every
extension that is not `.canvas`/`.base` Markdown — so activating an
image card replaced the canvas tab with an editor over the PNG's bytes,
while the row's own HelpText said media arrives in a later slice. The
tell was `CanvasOpenTarget.DefaultApp`: an arm of the canvas vocabulary
that no Windows code path could reach. The media hand-off resolves the
vault-relative target against the vault root in the WORKSPACE (the only
holder of the root) and refuses anything that escapes the vault — a
`.canvas` file is untrusted input and `../../` in a `file` node would
otherwise open anything on the disk.

**The predicate is shared; the announcements deliberately are not.**
The first cut of this row said "the shared external-link policy" while
copying the scheme literal a THIRD time (the right-pane panels and the
citation popover held the other two), which is the claim-exceeds-
enforcement class this document exists to stop. `ExternalLinkPolicy` is
now the one definition and all three call sites read it, so a fourth
scheme is a one-line change rather than a search. What stays per-surface
is the sentence: the panels and the popover speak the `ExternalLink*`
family, the canvas speaks its own, because a canvas surface emitting
another family's strings would be §W-D drift.
Group ⇒ expand. A file card whose target is gone announces
`CanvasFileNotFound`; a text card whose `canvas_node_text` refuses
announces `CanvasBlocked { CardTextUnreadable }` — the 0b never-silent
table, and the reason A16 exists.

**A14 — Focus delivery is durable STATE, delivered by whichever surface
can actually deliver it** (rewritten as the design pass's contract,
stopping rule 4).

**Why a rewrite.** Four consecutive review rounds found a defect in this
one behaviour — the connection-row follow, the retarget focus theft, two
tests that could not fail, and then codex's three. Every one was a
variation on the same thing: focus was delivered on an EDGE, at the
instant something fired, to whoever happened to be subscribed and ready,
and the tests supplied the trigger themselves. Rule 4 says stop patching
the sites and write the design.

**The design.**

1. **The request is state, not an edge.** `CanvasFocusRequest
   { Owner, NodeId }` lives on the document as `FocusRequest`.
   `RequestActiveEditorFocus` — the one funnel every user-initiated open
   calls and no background path does — raises it, addressed to the tab
   that asked. It stays pending until a surface delivers it and says so
   (`CompleteFocusLanding`), and a newer request supersedes an older one
   because raising OVERWRITES: completion compares the pending RECORD by
   reference, so a late delivery of a superseded request cannot clear a
   live one. (It carried a generation counter until PR C-lite's codex
   round 2, which pointed out that an int comparison has an ABA window a
   reference comparison does not — and the contract was already that a
   surface hands back the instance it was given.)

   **Its LIFECYCLE has two boundaries the original design did not
   record**, both added by PR C-lite and both found by review rather
   than by the change that needed them. TERMINALITY is answered on the
   read AND on the write: a retired document reports no request and
   REFUSES to store one, because the read alone hid a late write that
   repopulated the closed tab's owner — the very reference retirement
   drops. And a request outlives its OWNER only until the tab set
   changes: two panes share a document, so closing the pane that asked
   leaves a request no peer may take and nothing supersedes, holding
   that tab's graph. The workspace drops it where the tab set changes,
   not in `Unloaded`, which also fires when a pane is merely hidden and
   whose request must survive.
2. **Every surface retries on every condition that can change the
   answer**: the model changing, a publish landing, the view loading,
   the view becoming visible, the request being raised, the tree
   realizing containers, and — B2 — the presenter REBINDING its
   `DataContext`. That last one is not covered by the model changing: two
   panes on a path share one document, so a presenter swapped from tab A
   to tab B keeps an identical `Model` reference and `OnModelChanged`
   never fires, yet a request addressed to B must now deliver.
   `ARequestDeliversWhenThePresenterRebindsToItsOwner` pins it. None of these is "the" moment — assuming one
   was is what the edge design got wrong — so each simply asks again.
   A surface whose `DataContext` is not the request's owner ignores it,
   which is what stops one document's request landing in every pane
   showing that path.
3. **Realization is part of delivery.** A virtualized row has no
   container until the panel makes one, and an ancestor group's children
   are not items until it is expanded. `DeliverFocus` walks the ancestor
   chain root-first, expanding and laying out, and asks the virtualizing
   panel to bring the index into view when the generator has not made
   the container yet. **Only a realized container counts as delivered**:
   the previous code fell back to focusing the tree and returned success,
   so the request was consumed, the row was never read, and nothing
   retried. A failed delivery leaves the request pending.
4. **One authority per tab kind.** `MainWindow.FocusEditorPane` hands a
   canvas tab to the canvas delivery — it RAISES a focus request and
   returns, rather than returning bare. Returning bare stranded the
   seven dismissal routes that reach it as a last resort (the palette,
   search, properties and template sheets among them), whose own
   comments say the fallback exists "rather than stranding focus on the
   window root"; for those the canvas delivery had not been asked for at
   all. It still must not fall THROUGH: it is queued at `Input`
   priority — strictly after the canvas delivery — and its fallbacks
   (the `TabItem`, then the `TabControl`) would take focus straight back
   off the row, every time. The ordering is what makes this provable
   rather than a race: the canvas delivers synchronously on the funnel
   call, the pane handler runs later and re-asks instead of competing.
   Handing over is safe because the
   canvas surface has a landing place for **every** state — a realized
   row when Ready with rows, the onboarding region when Ready and empty,
   the (focusable) failure banner otherwise, and nothing at all while
   Loading, where the request simply stays pending.

**What the row lands on.** The request's `NodeId` when it names a row
this document has; else `LastActivatedNode`, which is how returning from
an opened card lands on the row that opened it (WCAG 2.4.3); else the
first row.

**The facts drive the production trigger.** `OpeningACanvasFromTheWorkspaceLandsFocusOnARealizedRow`
opens through `OpenPath` with MainWindow's own subscriber attached, so
the two authorities compete as they do in the app, and asserts the
subscriber RAN (standing aside, not never firing).
`AFocusRequestSurvivesUntilASurfaceCanActuallyDeliverIt` mounts the
surface after the request. `FocusRealizesADeeplyVirtualizedRowRatherThanFakingIt`
lands on the last row of the 2,000-node fixture.
`AnUnrealizableRowIsNotReportedAsDelivered` and
`AFailedDeliveryLeavesTheRequestPendingForTheNextTry` pin the honest
failure. `OpeningACanvasInOnePaneNeverLandsFocusInAnother` pins the
addressing, `AnEmptyCanvasLandsFocusOnTheOnboardingRegion` and
`TheFailureBannerIsAFocusableRegion` the non-Ready landings, and
`TheEditorPaneFocusFallbackStandsAsideForACanvasTab` pins point 4 in the
source, because no in-process fact can reach `MainWindow` — and it is
TWO-SIDED (fix round 7, Major-2): it asserts both the early return AND
that the canvas arm RAISES `RequestFocusLanding`, because a one-sided
"it returns" went green while the raise was missing and the seven
dismissal routes stranded. The delivery half a raised request relies on
is a separate fact, `ABareFocusRequestDeliversWithoutAnyOtherTrigger`,
so neither supplies the other's mechanism.
**Mutation-verified**: removing the funnel's canvas request fails three
of them; restoring the tree-focus fallback fails the realization fact;
deleting MainWindow's early return fails the source guard.

**A15 — `ActiveCanvasSurface` round-trips, and outline is ABSENT.**
The persisted token stays `"table" | "visual"` with outline written as
nothing at all (`WorkspacePersistence.cs:385–395, 549–551`) — the mac
sparse-map shape. `WorkspaceTabViewModel.ActiveCanvasSurface` gains a
setter the surface switcher drives; `Snapshot()` already carries it. The
forward-compat drop test (an unrecognised token collapsing to `null`)
is asserted to still pass, because the switcher now WRITES the field
that test reads.

**A16 — The document survives the 0b-6 skew.** Outline and table rows
are SQLite-served and their `speakable_name` falls back to the row title
when the handle's model does not know the id (0b-6); the model-backed
surfaces refuse instead — `canvas_where_am_i` answers `bad_node` for an
id the outline names. The document therefore treats every per-node
detail query as fallible: `NeighborsOf` and `NodeText` catch
`VaultException`, cache nothing on failure, and the surface renders the
accurate absent phrase from the vocabulary. The row stays selectable
either way. `not_a_group` and `bad_node` are ONE refusal discriminated
by message (0b-12's API note) and are treated as one: no host string
matching, anywhere.

**A17 — The scheduler conventions are the panel conventions, and the
threading rule is stated as the code has it.**
`CanvasDocumentViewModel : PanelWorkScheduler`. **Every FFI section
holds `_ffiLock`** — that is the invariant, and it is what makes a
handle replacement unable to race a read. What is SCHEDULED is narrower
than that, and the first wording of this row claimed otherwise ("every
FFI touch runs inside a `StartWork` body"), which the shipped code never
did:

| Call | Thread | Lock |
|---|---|---|
| `Load` / `LoadBody` (open, outline, table, scene) | `StartWork` body | yes |
| `Shutdown`'s close | `Task.Run` (inline in sync test mode) | yes |
| `NeighborsOf`, `NodeTextOf` | UI thread, synchronous | yes |
| `TargetExistsInVault` (`canonical_path`) | UI thread, synchronous | yes |

The two per-node detail reads are synchronous on purpose: they answer a
selection or an activation the user just made, they are single indexed
lookups, and the mac twin caches them the same way (`neighborsCache`).
`canonical_path` touches no handle at all and needed no lock for
correctness — it takes one anyway, so the rule above has no exception to
remember. The cost of the whole exception class is bounded UI-thread
blocking behind a large load; moving these into scheduled bodies is a
design change PR A does not need and PR C can make if the navigator's
volume warrants it.

Publishes marshal through `Post`, every publish re-reads a
`_generation` bumped with `Interlocked.Increment` at dispatch,
`Shutdown()` bumps the generation and closes the handle off the
dispatcher (exposed as `WhenHandleClosed()` for the bounded teardown
drain), and synchronous test mode runs bodies inline. The handle is
closed exactly once — on replacement inside the load body, or on
shutdown.

**The interleaving facts assert safety, not liveness.**
`AShutdownDuringAnInFlightLoadNeverPublishesAndClosesTheHandle` asserts
that no publish lands and the handle closes — it does NOT assert which
side won the race, because either order is correct and the scheduler
may legitimately refuse the body before it starts; a fact that demanded
the load reach the FFI first would be a timing bet, not a contract.

**The 2,000-node budget fact runs BOTH modes.** The W4-5 lesson is
"test the mode users run": the synchronous mode orders the load body
deterministically and makes every generation guard dead code, so
`LargeCanvasOutlineBuildsUnderBudget` is a `[Theory]` over
`synchronousForTests: true` and `startInteractionBackgroundWork: true`,
and the asynchronous arm drains before asserting.

**A18 — `ChordScope.Canvas` and three surface commands.**
`ChordScope.Canvas` joins the delivery enum (the file did not contain
the string "Canvas" at BASE). `slate.canvas.showOutline`,
`slate.canvas.showTable` and `slate.canvas.showVisual` register now in
`CommandSection.Canvas` (which core already exports,
`commands.rs:38`) with resolvers on the active tab's canvas document,
so all three are palette-reachable from this PR (drift test 1). Table
and visual are `CanExecute == false` until their projections ship, so
`SlateCommandRegistrar.DisabledReason` answers the canonical
`UnavailableReason` — the palette's own vocabulary, not a per-PR
sentence, because a registered row may not carry a `Reason` (that field
belongs to `Unreg`) and "ships in PR B" is not copy any user should
hear. The reason those two are disabled is recorded HERE and pinned by
`ShowVisualIsEnabledAndDrivesTheSurfaceSwitch` (renamed in
PR B, which enabled `showTable` — see B10);
`showTable` enabled in PR B and `showVisual` enables in PR D. None of the three
carries a chord, so `Scope` resolves to `None` through `Reg`'s own rule
and `ChordScope.Canvas` had no delivery site before PR C. PR C ships the
navigator and delivers its rows from the surface's tunnelling handler,
which is why the scope's doc comment names PR C. (The comment itself
was corrected in codex round 4; this sentence, its twin one file over,
in the scoped review after it. Two copies of one staged claim, found one
wave apart, which is why the guard now reads this document too.)

**The switcher is ONE Tab stop, and arrows move within it (added in PR
B).** Recorded as an ADDITION, not as a description of what PR A
shipped: A18 said nothing about the keyboard shape, and the §W-C row PR
B backfilled for the outline claimed "the switcher is one focus stop" —
which the code did not do. `KeyboardNavigation.TabNavigation=Once` on
the group makes it true and `DirectionalNavigation=Cycle` makes the
other half true (with the default, an arrow walked straight out of the
switcher into the projection). This is the WPF radio-group convention,
and it is what gives PR D's planned "the renderer is one focus stop
AFTER the surface switcher" a premise: without it the switcher was
three stops. `Once` also degrades correctly when the CHECKED choice is
disabled — a persisted `"visual"` token before PR D ships the renderer —
landing on the first FOCUSABLE choice rather than stranding focus on an
unreachable one. Pinned by
`TheSurfaceSwitcherIsOneTabStopAndArrowsMoveWithinIt`, which issues WPF
`TraversalRequest`s (the same focus engine a keystroke reaches, one
layer below the key handler) rather than reading back the properties it
also asserts; mutation-verified against removing either setting.

**The switcher is ONE named group in the UIA tree, and that required a
peer (round 8).** The three choices sit in a container carrying
AutomationId "CanvasSurfaceSwitcher" and the name "Canvas view". That
container was a bare `StackPanel`, which WPF gives no automation peer, so
BOTH properties were inert: a client saw no switcher element at all and
the three radio buttons appeared flattened directly under
`CanvasSurface`. It is now an `AutomationNamedGroupPanel` (the shared
peered-container idiom in `AutomationLandmark.cs`, whose own comment
records the W4-5 lesson that produced it), exposing
`ControlType.Group` with the name, and the three choices are its UIA
children. Evidence: the
`CanvasSurfaces_OutlineTreeSelectionAndActivation_AreClean` journey
asserts the element resolves and is named "Canvas view", and
`CanvasAutomationPropertyCensus` pins the general rule structurally —
mutation-verified both ways (reverting the switcher to a `StackPanel`
fails it on both property lines; adding a name to any other bare panel
fails it naming that line).

**A19 — Canvas tabs are never dirty, and the close gate is bypassed by
construction.** `WorkspaceTabViewModel.IsDirty` is only ever set by the
editor session, which a non-markdown tab never creates, so `CanCloseTab`
returns true before it consults `_dirtyCloseDecision`. That is a
property of the existing code, not new code, and it is exactly the kind
of claim that rots silently — so it is pinned by a fact that installs a
throwing close decision and closes a canvas tab
(`ClosingACanvasTabNeverConsultsTheDirtyCloseGate`).

**A20 — §W-A gains `canvas_read`; §K gains `CanvasOpenBenchmarks`.**
The serializer gains `CanvasReadArtifact`: per fixture in filename
order, the `open_canvas` info (node/edge counts, degraded, warnings),
then outline rows, table rows, the scene, and per node in reading order
`canvas_where_am_i` **and** `canvas_neighbors`. It shares
`CanvasArtifactExclusions` with `canvas_queries`, so `large_2000.canvas`
stays out of the golden for the reason 0b-15 gives. A node whose
`canvas_where_am_i` refuses serializes as `null` rather than aborting
the artifact — A16's skew is a shape the artifact must be able to
express. **The Swift twin lands in this PR** (`ParityHarnessTests
.canvasReadArtifact`), written blind against the mac binding and
arbitrated by the committed golden.

**The twin has to be same-PR, and the earlier plan to defer it was a
misread of the precedent it cited.** 0a and 0b both say "the twin lands
later and the mac lane arbitrates", and in both the twin landed in the
SAME pull request, before CI ever ran the pair — 0b's own ledger says
the mac census "was RED between Task 0b-1 and Task 0b-2", two tasks
inside one PR. Across a PR boundary the sentence means something else
entirely: `testHarnessArtifactsMatchCommittedGoldensByteForByte`
asserts golden↔produced name-set equality in BOTH directions, so a
golden with no Swift producer fails the mac lane deterministically on
the first run, with no coincidences. The coverage test was built to
refuse a golden nothing produces, and it would have. **Recorded as the
rule for every remaining PR in this series: a §W-A artifact and both of
its producers land together.** §K is `CanvasOpenBenchmarks` over `large_2000.canvas`
— open, open+outline, open+table, open+scene — recorded in
`BENCHMARKS.md` against the mac core-path figure of 5.62 ms, the C#
delta being marshalling.

### Tests that pin PR A

`apps/slate-windows/tests/SlateWindows.Tests/CanvasDocumentTests.cs`:
the registry facts (sharing, close-and-clear, retarget, vault-close
teardown), the five load states through a real `VaultSession` over the
committed fixtures, the once-per-open degraded announcement across two
panes, selection sync both ways, activation per kind, focus land and
restore, `ActiveCanvasSurface` round-trip and its forward-compat drop,
the close-gate bypass, and the 2,000-node outline budget in both
scheduling modes. Two of its facts are the ones a CI-arbitrated journey
would otherwise be the only witness for:
`TheTreeItemsCarryTreeSelectionItemExpandCollapseAndInvoke` builds the
real peers and asserts the four patterns, the control types and the
three label properties (mutation-verified — dropping the
`GetPattern` override makes Invoke null, which is why the custom peer
exists at all), and
`ShowVisualIsEnabledAndDrivesTheSurfaceSwitch` (PR B's
name for it, once `showTable` shipped)
drives the registrar over a live workspace rather than a null-workspace
stub.
`apps/slate-windows/tests/SlateWindows.Tests/CanvasAnnouncerTests.cs`:
latest-wins per class, the two classes independent, the High
flush-and-drop, the relay's priority pass-through, the label render used
by the banner, the phrase-drift pins (rows P/Q), and — the one fact with
no `FlushForTests` in it —
`APendingNavigationLineFiresOnItsOwnWithoutAFlush`, which pumps a real
dispatcher so the production `DispatcherTimer` actually ticks. Every
other fact in that file stays green with `_timer.Start()` deleted; this
one does not, which is the W4-5 lesson applied to the coalescer.
`CanvasDocumentTests` carries the production-mode interleavings for the
same reason:
`AShutdownDuringAnInFlightLoadNeverPublishesAndClosesTheHandle`,
`ASecondLoadSupersedesTheFirstPublish` and
`ARetargetDuringAnInFlightLoadPublishesOnlyTheNewDocument` are the only
facts in which A17's generation guards are live code.
`apps/slate-windows/tests/SlateWindows.Tests/Censuses/CanvasAnnouncerCensus.cs`:
A6's funnel guard, plus A2's attach-funnel doc-comment twin.
`apps/slate-windows/tests/SlateWindows.Tests/Censuses/CanvasAutomationPropertyCensus.cs`:
round 8's structural end to the inert-a11y-property class — no
`AutomationProperties.Set*` under `Canvas/` targets a type WPF gives no
peer, fail-closed on an unresolvable target, with a floor on the sites
scanned. (Listed here from PR B: it was written in §A's round 8 and
described in A18, but the pin list this section keeps was never
extended to name it.)
`apps/slate-windows/tests/SlateWindows.Tests/Censuses/CanvasMediaGateCensus.cs`:
CD-38's structural half — the gate has exactly one identity method and
reads identity only off held handles, never a re-open by path. (Same
omission, same fix: cited throughout §A's round record, absent from the
list.)
`apps/slate-windows/tests/SlateWindows.Tests/Censuses/AnnouncementSeamCensus.cs`:
the production wiring as a CHAIN — the three shipping call expressions
from `MainWindow` to the announcer, and a canvas load driven through a
real dispatcher. The one guard class here that no injected sink can
replace.
`apps/slate-windows/tests/SlateWindows.Tests/Censuses/ContractsCitationCensus.cs`:
every long identifier §A cites resolves to a real declaration. This
section shipped five citations of tests that did not exist; a contract
row citing a renamed test reads as evidenced and is not, which is the
failure class PR H's reconciliation depends on catching.
`apps/slate-windows/tests/SlateWindows.Tests/ChordTableTests.cs` /
`CommandDriftTests.cs`: green with the three new rows.
`apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs`:
`CanvasSurfaces_OutlineTreeSelectionAndActivation_AreClean` — tree and
tree-item control types, the row
names and ItemStatus, expand/collapse, selection, Enter activation,
focus landing, the degraded banner reachable, axe 0 failures. CI
arbitrates; it is never run locally beside the unit suite.
`apps/slate-windows/tests/SlateWindows.Tests/Censuses/ParityHarnessCensus.cs`:
`canvas_read.json` byte-for-byte against the committed golden.

---

## PR B — the canvas table projection

**Goal (spec §PR B).** The table view over `canvas_table_rows` on the
W4-1 `AccessibleDataGrid`, *exactly as Bases consumed it*: a
configuration of a battle-tested substrate, not new machinery. The
smallest slice of the series — the mac analogue is 125 lines of view on
the same substrate.

### Contracts

**B1 — The projection IS the substrate; nothing here re-implements it.**
`Canvas/CanvasTableView.cs` builds one `AccessibleDataGrid`, calls
`Bind` per publish, and configures columns, comparators, row actions,
activation and the announce seam. Sorting, the reader-position restore,
the "Header: value" cell labels, native row headers, type-ahead, the
row-actions menu, the activation plumbing and the AT-safe virtualization
are the substrate's — the W4-5 D-12 rule ("a second implementation of
cell focus is what the grid-conformance contract forbids"), which is why
the §8.7 matrix in `GridConformanceTests` applies to this projection by
construction and is **cited, not re-run** (re-running its 10,000-row
probe here would prove the substrate again and this projection not at
all). Pinned by
`TheProjectionIsTheSubstrateSoTheConformanceMatrixApplies`.

**The substrate gained exactly one method, and it is the model→view
direction it did not have.** `AccessibleDataGrid.SelectRow(predicate,
moveFocus)` seats currency on a row WITHOUT announcing, and without
taking keyboard focus unless asked. Every earlier consumer owns its
selection inside the grid (Bases reads `CurrentRowChanged`; W4-5's
Ctrl+J jump is a move the user asked for, which `FocusRow` makes and
announces). The canvas is the first surface whose selection lives
OUTSIDE the grid and is shared across panes, so a move made elsewhere
has to re-seat this grid — and re-seating it must not speak a move the
reader did not make, nor pull focus off whatever they were using. The
mac substrate draws the same line: its own key handling announces and
its `syncSelectionFromBinding` is silent. The new method reuses the
substrate's existing `WithoutAnnouncing` and `FocusCellElement` rather
than copying either.

**B2 — Columns are the mac inventory, in mac's order, over core's
fields.** **Type · Title · Group · Target · Connections · Color**, with
`IsRowHeader` on Title. `Type` is core's `kind` word with its leading
character capitalised (mac's `.capitalized`, and this host's existing
`CanvasPhrase` capitaliser, which is core's `capitalize_first`
transliterated); `Group` is core's `group_path`, last element, or empty;
`Target` is core's `target` verbatim; `Connections` is core's
`connection_count`; `Color` is core's `color_name` or empty. Header
labels and the grid's accessible name ("Canvas table") are the mac label
inventory verbatim, in `CanvasPhrase` with the rest of the §W-C label
class (A9's designation). §W-G row J is the owner: the table's
projection config is host-by-designation, and this row is what "host
projection config, mac label inventory verbatim" means concretely.

**`Target` is a file path or a whole URL — the HOST appears in the
title, not here.** The spec's PR B line says "`Target` per kind = file
path / URL host / empty (core-supplied)". Core's `node_target`
(`model.rs`) returns the `file` for a file card and the **whole `url`**
for a link card; the URL *host* is what core's `link_title` puts in the
TITLE. The binding rule the sentence exists to protect — core-supplied,
never host-derived — is met exactly: this projection passes `target`
through untouched. Recorded rather than "fixed", because deriving a host
here would be the R-D violation the same sentence forbids.

**B3 — The comparators are mac's, and the spec's description of the
Color one is corrected here.** Type, Target and Color take mac's plain
`<` over the same values, transliterated as `CompareOrdinal`; Title and
Group take mac's `localizedCaseInsensitiveCompare`,
transliterated as .NET's current-culture, case-insensitive compare,
because those are user-authored prose; Connections compares the COUNT, not the rendered
digits, or 10 would sort before 2.

**What "transliterated as `CompareOrdinal`" claims, and what it does
not — written from the reference implementation's actual comparison
semantics, third attempt.** Swift's `String` ordering is **not** a walk
over code points. `String: Comparable` is defined over Unicode
**canonical equivalence**: the standard library normalizes before
comparing, so canonically equivalent strings compare EQUAL and ordering
is computed on the normalized form. `string.CompareOrdinal` normalizes
nothing and compares raw UTF-16 code units.

**The strongest evidence for that is IN THIS REPO, and it predates this
PR.** Where the two parity harnesses must agree byte-for-byte, the Swift
twin does not use `<`: `ParityHarnessTests.swift` sorts with
`Array($0.utf16).lexicographicallyPrecedes(Array($1.utf16))` — an
explicit UTF-16 lexicographic order, spelled out at every sort site
(golden names, the fixture enumeration, the search rows at `:702`) —
against the C# twin's `StringComparer.Ordinal` in
`SurfaceSerializer.cs:951–952`. That explicit spelling is only necessary
because Swift's native `String <` does NOT order by UTF-16 code units;
the harness had to opt out of it to stay in parity, which is the same
fact this row is about, already load-bearing in the §W-A gate. External
corroboration: the stdlib's `StringComparison.swift` carries the
ORDERING implementation (a byte fast path, a normalizing general path),
and *The Swift Programming Language* ("Strings and Characters →
Comparing Strings") documents the EQUALITY half — values are equal when
their extended grapheme clusters are canonically equivalent, "even if
they're composed from different Unicode scalars behind the scenes".
(The attribution matters: TSPL states equality; the ordering claim comes
from the stdlib source and from the harness pair above.)

There are therefore **two** divergence classes, not one:

1. **Canonical equivalence / normalization form.** Any two `target`s
   that differ in normalization sort differently — and a pair that is
   canonically equivalent COMPARES EQUAL on mac while Windows orders it
   strictly. What mac then renders for that tie is unspecified rather
   than document order: `sorted(by:)` is documented as not guaranteed
   stable (.NET's `OrderBy` is documented stable, which is why the
   Windows side is at least deterministic). The worked case: an
   NFD `Café.md` beside `Caff.md`. Swift compares the NFC form, so `é`
   (U+00E9) beats `f` and `Café.md` sorts AFTER; ordinal compares the
   stored `e` + U+0301, so `e` (U+0065) loses to `f` and `Café.md`
   sorts BEFORE. Opposite order, every character inside the BMP.
2. **Code unit vs scalar for the supplementary planes.** A
   supplementary-plane character compared against a BMP character above
   U+DFFF sorts oppositely, because UTF-16 puts the surrogate lead unit
   (U+D800–U+DBFF) below U+E000 while the scalar is above every BMP
   value.

**Class 1 is ORDINARY, not exotic**, and this row does not get to call
it a corner: macOS's filesystem hands back decomposed filenames, so a
`.canvas` authored on a Mac routinely carries NFD `file` targets, and a
synced vault brings them to Windows verbatim (core stores `target`
byte-exact — 0b's "bytes are bytes" rule and this repo's own
`.gitattributes` doctrine). Any vault with an accented filename can show
it. `kind` and `color_name` remain out of the class for the reason the
earlier draft gave: closed ASCII sets core owns.

**The ordinal choice STANDS, and here is why the divergence is
acceptable rather than a defect to fix.** (a) It is deterministic and
locale-independent: the same canvas sorts identically on every Windows
machine, which a culture-sensitive compare would not guarantee.
(b) Normalizing host-side to chase Swift would be the host deriving an
ordering core does not define — the R-D violation B4 refuses one column
over — and it still would not reproduce Swift exactly. (c) **This
column's order never reaches a §W-A artifact.** Stated precisely,
because the harness does sort: it applies host sorts to FILE
ENUMERATION and to the search rows, and those are kept in cross-twin
parity by the explicit UTF-16 rule cited above — which is exactly how
this divergence would bite if a canvas sort ever were serialized. The
canvas ROW artifacts do not go through a host sort at all:
`CanvasReadArtifact` (A20) passes core's rows through in CORE's order,
so no golden and no cross-host gate compares the two hosts' sorted
tables. What is left is a user-visible ordering difference on
mixed-normalization vaults, and it is registered as **CD-39** rather
than left implied.

**This paragraph's own history is the reason it is this long.** It
first claimed "the same ASCII values" (false of `target`), then "both
walk code points in order … they can disagree ONLY [in the
supplementary planes]" (false of Swift, and it named the wrong single
class). Two corrections to one bound is the shape rule 4 exists for, so
this version is written from the reference implementation's documented
semantics with the source cited in line, and the residual divergence is
registered instead of being bounded away.

**The Color column does not sort "by preset index, hex after presets".**
The spec's parenthetical describes a comparator neither host has, over
data core does not produce. Core's `color_name` (`canvas/mod.rs`) never
yields a hex: a preset renders as its word and a hex renders as
"⟨nearest preset⟩ (custom)", with "custom color" for an unparseable
one. Mac's comparator is `$0.color < $1.color` over that NAME. The
property the spec's phrase is reaching for is core's own, stated in
`color_name`'s doc comment — *"the table's Color column sorts customs
beside their family"* — and it is a consequence of sorting the names:
`red` is immediately followed by `red (custom)`. **This is a
ruling-vs-source conflict resolved in favour of the source** (the mac
comparator, which both the spec sentence and the controller ruling name
as the thing to match); the wording is what is wrong, and it is recorded
here rather than implemented. Pinned by
`TheColorColumnSortsCustomsBesideTheirFamily` and
`TheConnectionsColumnSortsNumericallyNotAsText`; the whole six-column
matrix by `EveryColumnSortsTheWayMacSortsIt`, whose expectations are
CELL VALUES so a tie is spelled identically and the assertion pins the
rendered column rather than an order that depends on how ties fell.

**B4 — Rows are core's, published by the same load the outline is.**
`CanvasDocumentViewModel.TableRows` is `canvas_table_rows` untransformed
and in core's order (R-D), published by the publish that already fetched
it for the activation targets — two reads of one open, never a second
FFI round trip. The projection selects nothing and drops nothing —
asserted as sequence equality of node ids against `TableRows`, which is
the shape a filter or a paging bug breaks. The Title column reads core's
`speakable_name`, the same
field the outline row's name reads (CD-30's Windows reading), so one
card answers to one name on both projections **and** the row-header
identity the substrate restores the reader by is unique by construction
— a bare title is not (the substrate's own comment: "two notes can both
be Untitled"). Mac's table spells `title` there; the divergence is
CD-30's, extended to this surface, and it is visible only on a canvas
with repeated titles.

**Skew-graceful for free (contract 0b-6).** Table rows are SQLite-served
with `speakable_for`'s title fallback, so a handle whose open-time model
no longer knows a row's id yields a row with a non-unique name rather
than a refusal. That is 0b-6's own contract and 0b-6's own tests; this
projection adds no per-row model query, so there is nothing new to make
fallible (A16's list is unchanged by this PR).

**B5 — Selection is `CanvasSelection`, both ways, with no echo.**
View→model: the substrate's `CurrentRowChanged` calls the document's
`SelectNode` — the SAME narrating mutation the outline calls, which is
why both projections speak one grammar and why `CanvasMovedTo` and its
group-boundary event are composed in exactly one place. Model→view:
`SelectRow` re-seats currency silently, keeping the reader's COLUMN. The
echo is broken by a re-entrancy flag on the view, not by comparing
values — the A12 rule, one surface over, and it is needed here because
the substrate raises `CurrentRowChanged` for a programmatic seat exactly
as it does for an arrow key. A `null` from that event is currency
LEAVING the bound set during a rebind, not the user deselecting, so the
table never clears the shared selection (the W4-6 round-2 lesson).
Pinned by `SelectionFlowsBothWaysWithoutAnEcho` (which asserts the
model→view direction posts EXACTLY the canvas line, so a spurious grid
announcement fails it) and
`AReSeatKeepsTheReaderInTheColumnTheyWereReading`.

**The REBUILD runs under the same guard, and that is not redundant with
the substrate's own silence.** `Bind` restores the reader's position by
ROW-HEADER TEXT — every publish builds fresh row objects, so identity is
gone by definition — and this projection's row header is core's
`speakable_name`, which a republish can RENUMBER. Two cards titled
"Shared" are `Shared` and `Shared 2`; rename the first on disk and the
second one becomes `Shared`, so the header-text restore lands the reader
on a DIFFERENT card that now spells what they were reading. The
substrate suppresses its OWN announcement there (its restore runs under
`WithoutAnnouncing`), but `CurrentRowChanged` still fires — so without
the guard the view would call `SelectNode` and the DOCUMENT would speak
a canvas move nobody made, off a reload the user did not ask for. With
it, the re-seat that follows puts the reader back on the selected NODE,
which is the authority the header text is only a proxy for.
`ARepublishThatRenumbersASpeakableNameNeverSpeaks` drives exactly that
rename through a real reload; mutation-verified against dropping the
guard, which fails it on both halves (a line is spoken, and the shared
selection moves to the namesake).

**The repair moves CURRENCY, not focus, and that is a measured decision
rather than an omission** (codex B round 1, B2). The finding was that
the repair leaves the reader on the namesake row while the selection
moves elsewhere — a split in which Enter would open a card the reader is
not on. It does not reproduce: while the DataGrid holds focus, WPF moves
focus WITH currency, so after the full sorted-rename-republish path the
reader, currency and `CanvasSelection` are all on the same row. That was
measured with the proposed fix's own precondition (the reader was in the
grid) holding, so the fix would have fired and changed nothing.

Adding it would have COST something, which is why it is recorded here
rather than taken for safety: `IsKeyboardFocusWithin` on this control
includes the separately-focusable SUMMARY region (§8.7's own contract),
so re-seating the row with focus on every rebind yanks a reader who is
sitting on the summary onto a row they never asked for — the W4-6
background-publish focus-steal defect, reintroduced. Measured too: with
the re-seat applied, focus went from the summary `TextBlock` to a
`DataGridCell`. Both halves are now pinned —
`AfterANamesakeRepublishTheReaderCurrencyAndSelectionAllAgree` as an
end-state characterization (labelled as one: it passes with or without a
re-seat, and says so), and
`ARepublishNeverYanksTheReaderOffTheSummaryRegion` as the guard, which
is mutation-verified in the OTHER direction — adding the re-seat fails
it.

**B6 — Activation and row actions run the document's one seam.** Enter,
double-click, the substrate's `Invoke` path and the "Open" row action
all reach `CanvasDocumentViewModel.Activate`, looked up from the table
row's node id — which is exactly how mac's table does it (its `onActivate`
resolves the outline row and calls the container's shared `activate`).
So the media gate, the link allowlist, the subpath anchor and every
announcement are PR A's, unchanged and unduplicated. A group row has
nothing to expand on a flat projection and falls through silently, as
mac's does (its `activate` has no group arm at all). Row actions are
mac's three, in mac's order: **Open**, **Toggle Mark** (disabled until
PR G) and **Delete** (disabled until §E's verbs land — the section
now owns the row), each disabled one carrying
its reason, which the substrate exposes as HelpText and a tooltip — the
mac RowAction contract ("context menus retain a temporarily unavailable
relevant action WITH its reason"), and the reason a screen-reader user
can tell "not yet" from "not here". The reasons are label-class copy in
`CanvasPhrase`; they are not the registrar's `UnavailableReason`,
because that sentence belongs to command rows and a row action is not
one. Pinned by
`ActivationRunsTheSameSeamTheOutlineDoesIncludingTheMediaGate` and
`TheUnshippedRowActionsAreListedDisabledWithTheirReason`.

**B7 — The announce seam is swapped onto the canvas relay (DoD §H).**
The substrate raises CANONICAL grid events — `GridSorted` on a sort,
`GridRowMoved` on a vertical row move, `GridCellMoved` otherwise — and
under this projection they are posted through
`CanvasAnnouncer.Relay`, which carries core's rendered text AND core's
priority through unwrapped rather than re-classifying them as canvas
status (A5's `Relay` exists for exactly this consumer). The grid is
constructed MUTED and takes the relay when its document arrives, so a
table with no document cannot post through the canonical dispatcher.

**Two lines on a row move, deliberately, because mac has two.** A
vertical move posts the substrate's `GridRowMoved` immediately —
rendered as the focused cell alone, since a canvas row carries no
engine-authored audio description and mac passes none either — and the
document's `CanvasMovedTo` on the navigation class's 200 ms window. They
say different things (the cell under the reader; the card's position in
the canvas), and this is the mac shape rather than a Windows choice.

**The guard is structural AND behavioural, because neither alone can
see this.** `CanvasAnnouncerCensus.NoCanvasSourceAnnouncesOutsideTheRelay`
cannot: the substrate's default seam posts through
`AccessibilityNotificationDispatcher` from `Grids/AccessibleDataGrid.cs`,
which is not a canvas source — so a surface that simply forgot to swap
would bypass the funnel with no canvas file naming the dispatcher at
all. `EveryGridUnderCanvasRidesTheRelay` reads the syntax (every canvas
file that builds an `AccessibleDataGrid` assigns its seam to the relay,
with a floor so it cannot pass over nothing), and
`TheGridsOwnAnnouncementsComeOutOfTheCanvasFunnel` EXECUTES
`ToggleSortCommand` — the command the Ctrl+Alt+S gesture is bound to,
which is the seam a real chord arrives at — on the production surface,
and reads the funnel's post seam. Said exactly, because the difference
is the kind this document keeps catching: that fact does not press a
key; the JOURNEY is the half that does, cross-process, through real
input. A guard may not exercise the mechanism it is guarding — the class
recorded five times in §A's round record — so neither of the two
supplies the other's.

**B8 — No export producer, and Ctrl+F keeps routing.** No core canvas
export exists, and a host-composed one is what the residue census
forbids — the `ReadingTableGrid` precedent, whose own comment records
the same decision. `exportProducer` is therefore null and the
substrate's export commands answer `CanExecute` false. **Owner may
designate a canvas export later**; when core grows one, this row is
where the producer arrives. The substrate's `FilterCommand` is likewise
left unsubscribed, so Ctrl+F CONTINUES ROUTING to the app-level find
(the substrate's own rule: "with no subscriber the gesture continues
routing, so the app-level find is never shadowed by a grid that cannot
filter"). PR C subscribes it to the canvas filter. Pinned by
`NoExportProducerAndTheFilterChordFocusesTheCanvasFilter`.

> **Amended by PR C (contract C10).** The Ctrl+F half of this row is
> spent: the canvas now subscribes `FilterRequested`, so the substrate's
> gesture is the TABLE's delivery site for `slate.canvas.filterCards`
> (spec §7's "table: grid `FilterCommand`") and stops continuing-routing.
> The export half stands unchanged. The fact was RENAMED rather than
> narrowed, because its old name asserted the behaviour that changed —
> and the sentence above kept its "PR C subscribes it" clause precisely
> so this amendment is a completion rather than a contradiction.

**B9 — The summary is mac's sentence, and it is a LABEL.**
`Canvas table: ⟨n⟩ card⟨s⟩, ⟨m⟩ group⟨s⟩.` — mac's string verbatim,
including its pluralisation, over counts taken from `canvas_table_rows`
(a card is any row whose kind is not `group`). It is a static label, not
an announcement: the 0a decision (0a-13, §W-G row J) was that a
"CanvasTableSummary" event would exist only if the sentence were ever
ANNOUNCED, and mac never announces it — so the vocabulary has no such
event and inventing one would put a string in the canonical corpus that
no host speaks. The substrate makes it a separately-focusable named
region, which is how a screen-reader user reads it on demand. Pinned by
`TheSummaryIsMacsSentenceInTheFocusableRegion`, which includes the
pluralisation boundary on both sides of one.

**B10 — `slate.canvas.showTable` is enabled, and there is still one
surface switch.** The command's resolver drives
`CanvasDocumentViewModel.ShowSurface`, the same one the header radio
drives (A15/A18), so the shared state, the persisted `"table"` token and
the spoken `CanvasSurfaceShown` cannot disagree. `showVisual` stays
disabled with the registrar’s canonical sentence until §D lands; §A’s fact
was renamed accordingly (now `ShowVisualIsEnabledAndDrivesTheSurfaceSwitch` — §D TD-6 flipped
the row and renamed the fact)
and `ShowTableIsEnabledAndDrivesTheOneSurfaceSwitch` owns the other
half. No chord: the row stays `ChordScope.None` for A18's reason.

**B11 — Exactly one projection is in the UIA tree.** The surface body
holds both arms in one slot and COLLAPSES the one that is not showing —
collapsed, not hidden, so it leaves the tree a client walks rather than
sitting in it marked off-screen. Neither arm shows outside `Ready`, so a
parse-error pane stays a message rather than becoming an empty grid.

**Where each half of that is proved, stated exactly.** The visibility
gate and the POSITIVE half of the topology (the showing projection
really resolves as a peer under the id AT looks it up by) are
`ExactlyOneProjectionIsEverInTheTree`. The ABSENCE of the other arm is
the journey's, because WPF's in-process peer walk keeps an already-built
peer for a collapsed element — observed while writing that fact — so an
in-process "absent" assertion would be testing the walker rather than
the tree a client reads. The journey asserts the outline's element is
gone from the LIVE tree after the switch, through the real UIA bridge,
and that is the level at which the claim is true. `Visual` is not a
projection until §D lands, and PR A already round-trips a persisted
`"visual"` token, so that token falls back to the OUTLINE rather than to
an empty pane — recorded here because it is a real state a restored
workspace can be in today. Focus delivery routes to whichever arm is
showing (A14's table arm), and the table reports a delivery only when
keyboard focus is actually inside the grid: the substrate's own bool
answers "was the row in the bound set", which is a different question by
design, and A14's rule is that a surface may not report a landing it did
not make. Pinned by `ExactlyOneProjectionIsEverInTheTree` and
`AFocusRequestLandsOnATableRowAndAnUnknownRowDoesNot`.

**B12 — PR B carries the series' evidence debt rather than accruing it.**
Two rows PR A owed were backfilled here on a controller ruling, because
the pattern set now is the one PRs C–G repeat:

- **`w_c_matrix.md` gained the Canvas OUTLINE row**, composed from §A's
  own evidence list (the projection and its state regions, the
  data-item peer topology, the A9/A10/A11 name and status sources, the
  patterns, the A14 focus route, the announcement contract, and the
  named facts and censuses). PR A's spec line asked for it and it was
  not written; it is PR A's content, recorded under PR A's heading.
- **`parity_matrix.md` flips BOTH `slate.canvas.showOutline` and
  `slate.canvas.showTable`** out of `pending`. That file is generated,
  so the change is to its inputs: a `W6_1_STATUS` /
  `W6_1_DELIVERED_COMMANDS` pair in `scripts/generate-parity-matrix.py`
  (the W5-x shape) and a `canvasSurfaces` group in `chords.json`'s
  `deliveryEvidence`, whose implementation and test references the
  generator checks marker-by-marker. `deliveryEvidence` is the one
  object the chord-table PROJECTION leaves untouched — but it is still
  INSIDE the byte-for-byte comparison `ChordTableTests` makes against
  the re-serialized file, so a hand edit is safe only when it matches
  the writer's formatting exactly. This one did, and the round-trip
  test is what proved it. **The durable rule: edit `chords.json`
  through its writer (`SLATE_CHORDS_UPDATE=1`) or match the writer's
  formatting exactly, and let the round-trip test arbitrate — never
  assume a "preserved" object is outside the comparison.**
  **The rule this sets for the rest of the series:** a
  surface command joins the delivered set in the PR that makes it
  EXECUTABLE, not the PR that registers it — so `showVisual` stays out
  until §D makes it executable, and each PR flips its own row rather than leaving a
  batch for PR H.

### Tests that pin PR B

`apps/slate-windows/tests/SlateWindows.Tests/CanvasTableTests.cs`: the
whole of B1–B11 against a REAL `VaultSession` and real `.canvas` bytes,
every fact driving the production composition (`CanvasSurfaceView` →
`CanvasTableView` → the substrate) and reading what the consumer reads —
the rendered row order, the cell labels a screen reader speaks (read off
the generated cell element, not off the column configuration that fed
it), the summary region's name, the row-action menu, and the
announcements that come out of the canvas funnel's post seam. Its
fixture is built for the comparators: titles that only sort correctly
under a case-insensitive compare, connection counts of 2 and 10, and a
hex colour whose nearest preset is one of the two presets present.
`TheLargeCanvasBindsEveryRowCoreServed` pins the thing a projection can
get wrong on its own at 2,000 rows — truncating, paging, or dropping
rows core served — while the responsiveness itself stays
`GridConformanceTests`' claim, cited.
`apps/slate-windows/tests/SlateWindows.Tests/Censuses/CanvasAnnouncerCensus.cs`:
`EveryGridUnderCanvasRidesTheRelay`, B7's structural half.
`apps/slate-windows/tests/SlateWindows.Tests/Censuses/ContractsCitationCensus.cs`:
extended from §A alone to every listed PR section — §B is inside its
jurisdiction, and inserting §B between §A and its old terminator would
otherwise have folded the new section into the old one's extent.
`apps/slate-windows/tests/SlateWindows.Tests/CanvasDocumentTests.cs`:
`ShowVisualIsEnabledAndDrivesTheSurfaceSwitch` and
`TheSurfaceSwitcherIsNamedAndAllThreeArmsAreLive` (renamed at the flip) carry §A's rows
forward with one arm shipped.
`apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs`:
`CanvasSurfaces_TableGridSortSelectionAndActivation_AreClean` — the
spec's "Canvas_TableJourney" under this project's journey naming
convention. Enumerated against the shipped assertions, not the plan: the
switcher's table arm is enabled and selecting it swaps the projection
(the outline's element LEAVES the live tree); Grid and Table patterns
and the grid's name; the six column headers; the summary region's
rendered sentence; Ctrl+Alt+S through a real chord, asserted as the
whole Type column ordered ascending; the row-actions menu opened with
the MENU KEY, its three items named, the two unshipped ones disabled
with their reasons readable as HelpText; Enter activation opening the
card detail; axe 0 over the table. CI arbitrates.

**The row-actions leg was ADDED rather than the claim narrowed** (red
team B-2). This sentence previously named "the disabled row actions"
while the journey never opened the menu — an evidence claim written from
the plan instead of the code, which is the shape PR H's reconciliation
exists to catch, and which the `w_c_matrix` row (written in the same PR)
got right. `GridConformanceTests` had recorded that popup menu items are
"not reliably enumerable through desktop UIA on a starved session", so
the leg was attempted rather than assumed: the menu is found from the
DESKTOP (a WPF `ContextMenu` is its own HWND) and identified by its first
item rather than by an id invented for the test. Four consecutive
published-dll foreign-CWD runs came up green, so the honest fix was the
leg, not the sentence. `ToolTipService.ShowOnDisabled` went into the
substrate in the same pass (red team m-6): the reasons reached AT
through HelpText, but WPF suppresses tooltips on disabled elements, so a
sighted mouse user got nothing on exactly the items that carry one.

---

## PR C — the navigator, the mode stack, Where-am-I, the filter, verbosity

**Goal (spec §PR C).** The canvas-wide command layer every projection
hosts — deliberately **not a fourth view** (the t2/t3 shared-architecture
decision) — plus the t0 §2 M1–M7 mode machine against a test mode, the
M5 Escape ladder, Ctrl+Alt+Shift+I Where-am-I with its focusable panel,
the in-canvas filter, and the live-switchable verbosity preference.

### Contracts

**C1 — The navigator is a per-DOCUMENT command layer, not a view.**
`CanvasNavigator` lives on `CanvasDocumentViewModel` beside
`CanvasModeController`, so two panes on one canvas share one command
layer exactly as they share one `CanvasSelection` (R-B). Every verb has
two shapes and they are the same code: the palette row
(`CanvasNextCardCommand` and its twelve siblings, resolved in
`SlateCommandRegistrar`) and the `ChordScope.Canvas` chord
(`CanvasNavigator.HandleKey`). Rule R1 is why both exist — a chord is a
convenience, never the only path.

**Rule R2 gates the chord half, and which arms it gates is not
uniform**, so this row names them rather than gesturing at "the chords":

| Arm | Gate |
|---|---|
| Down / Up | a projection owns the keys, then defer-or-answer (C3) |
| Right / Left | a projection owns the keys AND it is the OUTLINE (the table's arrows are the grid's cell navigation) |
| Enter | a projection owns the keys — so a focused button, field or any other control keeps its own Enter |
| Escape | UNGATED within the surface: the ladder answers from anywhere, and an open Where-am-I panel pre-empts it (CD-47) |
| Ctrl+F | the GRID does not own the keys — the substrate's own gesture delivers it when it does (C10); §C's m9 rerouted this from "the table is showing", repaired in §D with the header arrangement's fact |
| Ctrl+Alt+Shift+I | ungated within the surface — a pull surface must answer wherever the reader is |

The two ungated rows are the deliberate ones. Escape is M4-adjacent (no
mode survives without focus, so cancelling from anywhere is the point),
and Where-am-I is t0 §1.4's pull, which is worth nothing if it depends
on standing in the right place.

**Movement rows stay ENABLED on a canvas in any load state.** The
navigator's whole job when the document cannot answer is to say so
(C4); a disabled palette row would replace an accurate sentence with the
registrar's generic unavailable one. The two MODE rows are the
exception and C9 records why.

**C2 — `ICanvasSurfacePresenter` is the only thing the navigator knows
about views.** Three questions (which projection, does it own the keys,
can it move one row), three focus moves (a row, the projection, dismiss a
transient region) and one identity — `Owner`, the tab this pane is
showing, which is what `FilterCards` addresses its request to and
therefore the member that makes the addressed request in the next
paragraph work at all. `CanvasSurfaceView` implements it and
routes to whichever projection is showing. Nothing sits on it that the
navigator does not call — "focus the filter field" was drafted onto it
and taken off, because Ctrl+F raises the document's addressed filter
focus REQUEST instead (C10; a durable record carrying its owner, not a
token and not a counter):
the field belongs to every pane showing the canvas and only the one the
reader is in should take it, which a presenter call would have got wrong
by picking whichever pane the navigator was holding.

Why a seam at all: the navigator is per document and focus is per view.
Why it is this narrow: everything else a verb does is model state and
announcements, which is what lets `CanvasNavigatorTests` drive the verbs
with no window at all and keeps the windowed facts to the things that
genuinely need one.

**Attachment is a FOUR-case rule**, and it has been miscounted in this
row once per case added, which is why the cases are listed rather than
totalled. The presenter is attached when the surface gains keyboard
focus; on every key press; when the DOCUMENT under the surface is
replaced and this pane is the one the reader is in (codex round 4); and
when a mode is ENTERED from this pane and admitted (codex rounds 8–9),
because the invocation names the pane the reader is in and it would be
incoherent for the verbs to serve a different one. It is kept afterwards in every case, so a
palette-invoked verb still moves the reader in the pane they are actually
in.

The third case is the one that had to be added rather than deduced. An
external rename retargets the tab from document X to Y (CD-32) and the
surface detaches from X's navigator; nothing then introduces it to Y's,
because a reader whose keys never leave the filter field produces no
focus edge and presses no chord. Every movement verb afterwards moved the
selection and spoke it while the focus call reached nobody — CD-40's
agreement broken by a rename plus one palette command. "The pane the
reader is in" has two readings and the rebind takes both: they own the
keys NOW (`IsKeyboardFocusWithin`), or they owned them LAST and something
transient — a palette, a menu — is holding them while the replacement
lands (`DetachPresenter`'s answer, which is why it now returns one).
Nothing detaches on focus loss, which is what makes the second reading
available at all.

Two panes on one document cannot fight over this: the attaching edge
tracks the last pane to own the keys, so a pane owning them now is the
pane that was attached, and both clauses name the same surface.

**C3 — Down/Up are delivered by the PROJECTION; the navigator answers
the boundary.** The outline tree's own Up/Down and the grid's own Up/Down
already move the reader AND land on `CanvasDocumentViewModel.SelectNode`,
which is the one narrating selection mutation. A navigator that moved as
well would move twice and speak twice. So the arrow arm asks
`CanMoveWithinProjection` and returns UNCONSUMED when the answer is yes.

**What it answers is the thing the projection cannot.** A tree with
nowhere to go does nothing and says nothing, and t0's never-silent rule
says a keypress that does nothing must say so. At the boundary the
navigator consumes the key and announces `End of canvas.` /
`Start of canvas.`; with an active filter that matched nothing it
announces `No cards match the filter.`, and on an empty canvas
`Canvas is empty.` — three different facts, three sentences.

**The boundary is the PROJECTION's rows, not core's reading order**, and
that is a real difference rather than an approximation: a connection row
under the selected card is a reading stop the tree visits (contract
A11), so the last CARD is not the end of the canvas while one sits below
it. `CanvasOutlineView.CanMoveFocus` walks the tree's own visible rows
and `AccessibleDataGrid.CanMoveRow` the grid's own order — which the
reader's SORT may have changed, and which is the honest answer there.
`TheBoundaryIsTheProjectionsRowsNotCoresReadingOrder` pins both.

**The palette rows are card-to-card**, over core's reading order across
the filtered set. The two are not in tension: one is the reader's arrow
through the rows on screen, the other is the verb named "Next Card".
CD-44 records the pair.

**Right/Left FOLLOW unconditionally on the outline, and always answer**
(CD-48). The spec asked for "connection-follow when the card has
connections, else tree semantics, as mac does"; mac does not do that —
it follows unconditionally, so a connectionless card hears
`No outgoing connection.` there. The blend left one keypress on a leaf
silent, which is the never-silent rule broken by a precedence nobody
had. Expand/collapse keeps Enter-on-group, WPF's own numpad `+`/`-`, and
the `ExpandCollapse` pattern a screen reader drives — all three verified
by `ExpandCollapseSurvivesTheArrowsBeingClaimed` rather than asserted.
The TABLE keeps Left/Right for the grid's cell navigation, which the UIA
Table pattern depends on; follow is the palette row there and answers
identically.

**Enter asks R2's own question, and Escape deliberately does not.** A
tunnelling handler runs BEFORE the element the reader is standing on, so
the mode's Enter would otherwise out-rank every control on the surface —
pressing Enter on the visible CANCEL MODE button would COMMIT the mode,
inverting the user's intent on the exact control M6 exists for, and
Enter in the filter field would commit it too. Mac has no such hazard
because its container's Return handler BUBBLES, so a focused button or
field consumes it first.

The gate is **"does a PROJECTION own the keys"** — the same question
every other bare-key arm asks — and NOT a list of control types that own
Enter. The list was tried and rejected in review: `ComboBox`,
`Hyperlink`, a templated item part and every control PR E and PR F add
would each have had to be remembered, and the one that was not would
re-open this silently. The question is closed by construction; a list is
open by construction, and this contract has spent two rounds on
curated-list defects already.

Escape stays broad because cancelling from anywhere in the surface is
M4-adjacent — no mode may survive without focus — and the ladder is the
canvas's answer for it everywhere; the panel case is settled by the
region being open (CD-47), not by what has focus. Pinned by
`EnterOnAFocusedModeButtonActivatesTheButtonNotTheChord`, which asserts
the R2 premise and covers the filter field too.

Latent in PR C — nothing enters a mode outside tests — and fixed here
anyway, because the M-conformance machine is this PR's deliverable for
PR F and F arms it on its first day.

**C4 — The never-silent read gate: one mapping, membership by
construction.** `ReadRefusalFor(state, handleLive)` is the single
state → response authority, STATIC and total over `CanvasLoadState` ×
handle. It is the mac `canvasReadRefusal(for:)` twin (VA-1/VA-2), and it
exists in that shape for the mac reason: three consecutive review rounds
there found a state missing from a hand-written list, and red-team rule 4
says stop patching sentences and implement the invariant.

| State | Answer |
|---|---|
| `Ready`, handle live | proceed |
| `Ready`, handle detached | `CanvasStatusNote::Reopening` (VA-1) |
| `Loading` | `CanvasStatusNote::Loading` (VA-2) |
| `ParseError`, `Failed`, `RetargetAbsent` | `CanvasStatusNote::NotReadable` |

`TheReadMappingAnswersEveryLoadState` ENUMERATES the enum rather than
restating it, so a sixth state fails by name; the arm that cannot be
enumerated is a thrown `UnreachableException`, because C# cannot make a
switch expression over an enum exhaustive.

**`Ready`-with-no-handle is reachable only through retirement on
Windows, and the arm stays anyway.** A rename RE-KEYS the registry and
builds a fresh document rather than detaching a handle (CD-32), so the
mac batch-retarget window has no Windows twin today; a shut-down
document is the one way to be `Ready` with a closed handle, and its
announcer is already silenced (contract A5). The arm is a row in a table
over the STATE SPACE, not over the states somebody remembered were
reachable, and PR E's funnel and the file watcher are the first things
that could make it live.

**Precedence: the verb's own question first, but only where the reader
can see rows.** `AnsweredMissingSelection` announces `Nothing selected.`
and returns true, and it asks `RendersRetainedSnapshot` first — the mac
`rendersRetainedSnapshot` twin. Without that ordering a no-selection
press in a non-ready state answers with the state where the caret is the
honest subject. The predicate is DERIVED from what the surface's
`Render` actually shows, and
`TheSnapshotVisibilityPredicateMatchesTheSurfaceRender` parses that
method and fails when the two disagree — the view is the authority.

**Throw arms.** A structural query fails two ways and they get different
sentences, because they are different facts:

| Failure | Sentence | Why |
|---|---|---|
| No handle | the mapping's note | the query never ran |
| The query THREW with a live handle | `Nothing selected.` | the selection does not name a card this canvas can answer for (0b-6's row/model skew) |
| The query SUCCEEDED and came back empty | the verb's own phrase (`Group "X" is empty.`, `At canvas level.`, `No outgoing path from "X".`) | that fact was actually learned |

Silence is not on the list.
`AnUnresolvableSelectionIsNeverReportedAsAnEmptyAnswer` pins the middle
row, which is the one that is wrong by default: a throw falling into a
verb's empty-answer branch reports a card core could not resolve as an
empty group, or as being at canvas level, or as nothing at all.

`EveryReadVerbAnswersInEveryLoadState` DRIVES every verb in every
unreadable state, and asserts the EXACT sentence each state owes rather
than that something spoke. The weaker assertion is what let `ClearFilter`
walk past admission entirely and stay green for eight rounds: it
announced a count over an empty outline, which is neither silence nor
the state's answer.

It derives that expected sentence from `ReadRefusal` — the mapping the
verbs route through — so it no longer catches a WRONG mapping; both
sides would move together. That is deliberate and it is bounded: this
fact's job is that every verb GOES THROUGH the mapping, and
`TheReadMappingAnswersEveryLoadState` pins what the mapping SAYS,
per state, independently. The guard-may-not-exercise-the-mechanism rule
is satisfied across the pair rather than inside one fact, which is
worth stating because the earlier wording claimed it of this one alone.

**C5 — Structural queries are core's, and their FAILURE is a separate
return.** `TryChildrenOf`, `TryParentOf`, `TryTracePath`, `TryWhereAmI`
and `NeighborsIfKnown` each hold the document's FFI lock, answer
`false`/`null` for "no answer", and never collapse an answer into a
failure. `TryParentOf` is the one that would have been easy to get
wrong: its ANSWER is a nullable id and its FAILURE is the bool, because
folding them would erase exactly the distinction between "no parent" (at
canvas level) and "the query threw" — the same flattening trap the mac
side hit under SE-0230.

`NeighborsOf` (contract A11's outline rows) is now
`NeighborsIfKnown(id) ?? []`: one query, two honest answers, because the
outline wants "no rows" for an unanswerable lookup and follow-connection
must not say `No outgoing connection.` — a claim about an adjacency list
— when there is no list.

**Follow-connection asks the DATA before the STATE** (VA-1's recorded
order): the selection precondition, then the adjacency answer — a
non-null list answers normally, traversal or accurate dead end, whatever
the load state — and only then the mapping's refusal. Asking in the
other order makes a warm cache lose to a refusal, which is the rule
backwards.

**C6 — The Escape ladder lives in the SURFACE's `PreviewKeyDown`, and it
names the rung it consumed.** `CanvasModeController.HandleEscape`
returns a `CanvasEscapeRung`, so "exactly one rung per press" (t0 §2 M5)
is a table test rather than a claim:

| Rung | Consumed when | Effect |
|---|---|---|
| 1 `Mode` | a mode is active | cancel + restore, announced |
| 2 `Filter` | the field holds a needle | clear it, announce `CanvasFilterCleared`, re-seat (the seat rule below) |
| 3 `Surface` | a transient region is open or holds focus | close the interim card detail, or leave the filter field or its result summary — re-seat (the seat rule below). NOT the Where-am-I panel: an open panel pre-empts the whole ladder (CD-47), so no reachable press arrives here with it up |
| 4 `WorkspaceTab` | nothing above consumed it | NOT consumed; the press bubbles |

**An OPEN Where-am-I panel takes the press ahead of every rung**
(CD-47): Escape dismisses it, leaving an active filter and even an
active mode untouched, and returns focus to the element the reader came
from IF they were inside it. That is mac's order — the panel's Close
button carries `.keyboardShortcut(.cancelAction)`, which is
window-scoped and resolves before the container's ladder — and it is
what the spec's own build text asks for. Not a rung: it is the transient
region's own dismissal, keyed on the region being OPEN so no focus
arrangement can route around it. Without it, an Escape meant to close
the panel destroyed a typed filter needle, which is a first-class t0
§1.4 scenario (the readback carries the filter clause, so asking
Where-am-I while filtering is the designed use). Pinned by
`EscapeInsideThePanelWhileFilteringActsOnThePanel` and
`EscapeDismissesAnOpenPanelEvenWhenTheReaderIsElsewhere` — the second is
the arrangement a focus-keyed version got wrong.

**Rung 3's card-detail arm is the READ-ONLY interim, and the editor is
NOT on this ladder.** See the M8 paragraph below; the same locus rule
is why the editor sheet will own its own Escape rather than becoming a
rung.

Rung 3's effect is a focus move and is deliberately unannounced: the
screen reader reads what focus lands on, and a line on top of that is
the t0 §1.5 doubling rule broken on a dismissal (the same reasoning as
A12's silent seat).

**THE SEAT RULE.** Escape's two rungs, the CD-47 pre-ladder dismissal,
AND the Where-am-I panel's own Close button re-seat through one helper
(`FocusProjection`), and it is
STATE-AWARE, which PR C-lite's codex round 2 forced: `Render` collapses
both projections under `Loading` and under every failure state, so "back
to the projection" was a move to something that is not there — the keys
stayed on the window root with the press already consumed. In order:

1. the PROJECTION, when the state renders rows **and there are rows to
   render**. The row condition is codex round 3's: `Ready` keeps the
   projection visible while empty, and both implementations take focus
   holding nothing (`TreeView.Focus`, and the grid's own), so asking it
   first put the reader on a silent empty control with the sentence that
   would have told them what to do unread beside it;
2. the ONBOARDING region, which is visible exactly when the canvas has
   no cards;
3. the failure BANNER, a tab stop only in the error states (a transient
   "Opening canvas…" is not somewhere to put a reader);
4. the FILTER FIELD, when `Ready` has rows but the needle matched none
   of them — the one control on this surface that can change the answer;
5. otherwise nothing on this surface can hold them: it leaves the reader
   in place and defers an addressed A14 landing that the publish
   delivers.

**Arm 4's caller is the reason the caller list above says "and the CD-47
pre-ladder dismissal".** This wave first removed arm 4 as unreachable,
on the reasoning "every caller is an Escape rung" and no rung can
present a needle — rung 2 clears it before asking for a seat, and rung 3
cannot have the press while one exists. That enumeration was false, and
the
scoped review caught it: `CloseWhereAmI` is also reached from
`OnPreviewKeyDown`'s CD-47 path, which by this contract's own text
dismisses the panel "leaving an active filter and even an active mode
untouched" — so it runs with a live needle by design, and is a caller
that is not a rung. **So is the panel's Close BUTTON**, whose `Click`
reaches the same dismissal by pointer or Invoke with the needle equally
live. The paragraph that exists to correct a caller enumeration listed
two of the three; the third was found by reading the call graph again
rather than the paragraph. On a canvas that
HAS cards the onboarding region is hidden (`EmptyOnboardingText` keys on
the UNFILTERED outline) and `Ready` has no focusable banner, so without
arm 4 the seat falls through to a deferred landing with the panel
already collapsed, and whether the reader is rescued depends on the
delivery path's hold conditions still being false a moment later. Arm 4
seats them with nothing left to go right.
`DismissingThePanelSeatsTheReaderEvenWithNoRowsToSitOn` drives the real
`OnPreviewKeyDown` and asserts no landing was raised at all, which is
what distinguishes the arm from the rescue.

The equivalent arm on the DELIVERY path stays and is a different case:
`TryDeliverFocus`'s ready-empty arm falls to the filter field because a
shell-raised landing can arrive while a needle is excluding every row.

That deferred landing is a RESTORATION rather than an INSTRUCTION, and
the distinction governs BOTH ends of its life. The surface WITHDRAWS it
when the reader leaves of their own accord (a pane change, a tab
switch), and HOLDS it — retained, undelivered — while the reader is
behind something layered over this tab: an open overlay, an open menu,
or a deactivated window.

**The RETENTION set is not the mode stack's KEEP-ALIVE set**, and this
row said it was for two waves. The mode stack's closed M4 table
(`CanvasModeController.CancelsFor`) keeps a mode alive across exactly
two departures — `ModalOverlay` and `MenuOpen` — and CANCELS on
`WindowDeactivated` along with the two the restoration withdraws on. So
retention is a strict superset, by one: a deactivated window is a reader
who is coming back to this TAB, which is the restoration's question, and
not a reader who is still in the middle of a mode, which is the stack's.
Two questions, two tables, and the row that conflated them made the
smaller one look like the bigger one's definition.

**The hold ENDS on re-evaluation, not on delivery.** Focus returning to
this surface, and the window activating, are the two moments that clear
the departure edge and ask again — and asking again is all they do. The
levels are consulted afterwards, so a landing whose window came forward
while an overlay is still up, or while the keys are in another pane, is
still held. Nothing here promises a seat; it promises that the question
is re-asked whenever the answer can have changed.

**And the cause can end somewhere the surface cannot see.** A departure
is written when THIS surface loses focus, so the move that ends it — the
menu closing and the reader clicking into another pane — raises nothing
here. That landing used to be held for the pane's lifetime: neither
delivered, nor withdrawn, nor completed. The surface therefore also
watches its host WINDOW's keyboard focus. It runs whenever this surface
has something to lose — a deferred restoration, or an active mode it
OWNS — and when the cause has ended and the keys have gone somewhere
else, that destination is classified as the `PaneFocus` it is, routed
through `Depart` itself so the mode stack hears the same thing. Keys
that came back HERE need no arm: that is a focus-within transition,
which clears the hold and re-asks on its own, and an arm that did it
here too was written and removed as provably redundant.

Codex round 3's B1 and codex round 4's M2 are ONE ROW, and they are the
two failure directions of one lifecycle: the withdrawal existed alone
(theft — delivery on top of a reader who was elsewhere), and then the
hold existed alone (starvation — a landing nothing could ever resolve).
A rule that governs one end of a lifecycle is half a rule; this branch
has now built that shape three times, counting read-side and write-side
terminality on the request properties.

Rungs 2 and 3 are registered ONCE by the navigator
rather than by each surface, because two panes on one canvas would
otherwise claim one rung twice and the order would depend on which pane
mounted first; `RegisterRung` refuses a duplicate and refuses rungs 1
and 4, which are the stack's own and the un-consumed answer.

**The site is the point.** Delivering the ladder from
`Window_PreviewKeyDown` would make canvas Escape global. In the surface's
tunnelling handler it is live exactly while the canvas has focus, so an
unconsumed press keeps its ordinary meaning and the shell's own Escape
behaves with a canvas open exactly as it does without one. The one shell
arm that shares the string — `slate.file.cancelImport` — is delivered
from the WINDOW, which tunnels first, so an import in flight keeps
Escape and the ladder never sees it (C16 records the pair).

**Tunnelling, not bubbling**, for the same reason the reading navigator
records: the projections' own controls (a `TreeView`, a `DataGrid`, a
`TextBox`) consume arrows and Escape on the way up.

**Rung 3's card-detail arm is the READ-ONLY interim, and the editor is
NOT on this ladder.** The region rung 3 dismisses is PR A's t2 #362
detail pane — a `TextBox IsReadOnly` seeded from `canvas_node_text`,
where Escape closes a view and returns focus to the row it opened from
(WCAG 2.1.2), because there is nothing to keep. PR E replaces that
region with the real card editor, and t0 §2 **M8 carves the editor out
of the mode stack entirely**: the inline text editor is not a spatial
mode, **Escape COMMITS** the text and returns focus to the card, and
discarding is the editor's own undo before Escape. So when E lands, the
editor sheet does not join this ladder as a rung and rung 3 stops having
a card-detail arm at all — the sheet handles its own Escape, with
committing semantics.

Written here because the inheritance is the hazard: "Escape dismisses
the detail region" is true of the interim and false of what replaces it,
and a reader porting rung 3 forward would ship an editor that throws
away a user's typing. **M8 is PR E's to satisfy; every string and every
comment about editor Escape says "commits" and never cites M2.**

**C7 — The mode machine is the stack and nothing else.**
`CanvasModeController` owns M1–M7; a `CanvasModeSpec` carries the typed
`CanvasMode`, the `CanvasModeObject`, a commit effect returning a
`CanvasModeCommitResult` and a cancel effect returning the
`CanvasModeRestoration`.

**The transition is CLOSED while a commit effect runs.** The effect is
arbitrary host code — it opens a sheet, it moves focus, it lets the
shell switch tabs — and any of that reaches this controller
synchronously, where M4 turns it into a cancel. Re-entering mid-commit
cleared the stack, announced a cancellation, and let the commit carry on
and announce its confirmation too: two outcomes for one press and a
final state that was neither.

`Commit` therefore enters a committing state before invoking the effect.
A focus departure raised inside that window is DEFERRED — one slot,
latest-wins, because an effect that provokes two departures has still
only left the canvas once — and a direct `Cancel` is REFUSED, because
that is a caller error rather than a race and the commit already owns
this press's outcome. The window CLOSES on the way out — in the
`finally` below, so no failure can strand the controller in a state that
refuses every later transition — and it stays closed across the outcome
and its announcements, which is why a departure raised by the
CONFIRMATION is deferred and applied to the result like any other.

**A direct `Cancel` inside a commit effect is SILENT, and that is
correct.** It returns false and says nothing: `CanvasModeCancelled` is
the sentence for a mode that ENDED with its state restored, and neither
happened — the mode is still up and the commit still owns the press. An
announcement here would be an inaccurate one, which is worse than the
silence, and the caller that made the call is host code rather than the
user. Adjudicated in codex round 2 and recorded rather than left to be
re-derived, because "a verb that answers nothing" normally reads as the
never-silent defect and this one is not.

**The deferred departure then applies to the RESULT**, which is the
M4-correct order: a commit that APPLIED leaves no mode to cancel and the
departure is moot; a REFUSED one keeps the mode, and no mode may survive
a focus departure, so it cancels — after the single commit outcome has
been spoken. `ADepartureDuringTheCommitEffectYieldsOneOutcome` pins both
variants, and PR F inherits it.

**The slot drains on every exit, and "every exit" is a `finally` rather
than a list.** The effect, the outcome application and the
ANNOUNCEMENTS are one guarded region; the drain is its `finally`. That
is what makes applied, refused and thrown the same code path instead of
three arms someone has to keep complete — and the confirmation announce
is INSIDE it, because it renders through core and can fault. It sat
outside for one round, between the outcome and the drain, and a fault
there skipped the drain and left the slot loaded for the next commit.

`finally` is right here for the reason it was wrong before: it runs
BEFORE the exception propagates, so the departure still applies to THIS
commit. What the previous round rejected was a `finally` that only
REOPENED the transition and left the drain on the success path, so a
throw skipped it — the objection was to the missing drain, never to
`finally` itself.

The drain is TOTAL, and that follows from where it is called. An
exception raised inside a `finally` REPLACES the one already unwinding,
so a restoration or an announcement that faults in the drain would erase
the failure worth reporting and blame a restoration effect for it. The
departure's own outcome is not worth that: it is logged
(`CanvasModeDepartureFailed`) and the unwind continues. The slot is
emptied BEFORE the departure runs, so a fault cannot leave it loaded
either.

Teardown is the exit that never returns to `Commit` at all: the shell
can retire the tab from inside an effect, so `Shutdown` forces the
transition closed, drains the held departure (its restoration owes a
sentence), ends whatever mode is left with the tab's own departure, and
CLEARS the slot in a `finally` so nothing stale outlives the object
holding it.

**Teardown SPEAKS, then RELEASES, and the mode stack ends TERMINAL.**

1. **SPEAK.** `Modes.Shutdown` runs while the funnel is still open: a
   departure held across a failed commit owes its restoration, and
   closing the tab a mode is running in is exactly the departure M4
   names. This is the only phase that may announce. It is fallible — a
   restoration effect is host code — so it is logged
   (`CanvasModeTeardownFailed`) and the teardown carries on rather than
   depending on it. Without that guard the announcer was never silenced
   and a coalesced line spoke about a document that no longer existed
   ~200 ms later — the A5 defect reached from the mode side — and the
   handle was never closed either.
2. **RELEASE.** The announcer is silenced (contract A5) and the handle
   closed, after the sentences above have been spoken.

**The stack ends TERMINAL, not merely idle.** A controller with no
active mode ACCEPTS an entry, so an `Enter` arriving from a surface the
shell has not finished tearing down — a menu item on a closed tab, a
palette row still registered — would have run an effect against a
document whose handle is gone. After `Shutdown` every verb refuses:
`Enter`, `Commit`, `Cancel`, `HandleFocusDeparture`. The Escape LADDER
is terminal by construction rather than by a fourth gate: `Shutdown`
empties it and `RegisterRung` refuses afterwards, so there is nothing
left to run, and rung 1 refuses because `Cancel` does. A check inside
`HandleEscape` would answer the same `WorkspaceTab` the empty ladder
already answers, and a guard with no power is a claim this task has
learned the cost of.

**The boundary has one recorded consequence, and it is the pairing.**
Where-am-I renders the panel string and then announces the same event
(t0 §1.4/§3: one render, no second composition). On a RETIRED document
whose handle close has not landed, admission can still admit — so the
panel string is composed while the boundary refuses the line, and the
pairing is broken for one caller nobody can see, since a retired
document has no surface. The alternative is composing a sentence for a
closed funnel, which is the trade C7 already took for the mode stack.
Recorded rather than papered over.

**The DOCUMENT has an announce boundary too, and the stack is handed
it.** Sixteen announce sites live on the document, the navigator adds
its own, and `AdmitStructuralRead` — the never-silent mapping itself —
speaks: so a verb invoked on a retired canvas composed a refusal through
a closed funnel. `CanvasDocumentViewModel.Speak` is the one place the
canvas reaches the announcer, and its condition is the FUNNEL's
retirement rather than the document's shutdown flag, because C7's SPEAK
phase runs between them and owes the drained departure's restoration.

**And the boundary is STRUCTURAL, not conventional.** The announcer is a
private field; production reaches it through exactly two named members,
and there is no allow-list of blessed call sites any more. `Speak` is
the boundary. `GridRelaySeam` is the other one and is not a canvas
sentence at all — B7's canonical grid events (sort, row move, cell move)
ride the funnel uncoalesced, carrying core's own priority through, so
they get a named member rather than a hole in the boundary. What
survives privatisation is one test handle whose NAME reads wrong in
shipping code, and `AnnouncementSeamCensus` fails on ACQUIRING it
anywhere under `Canvas/` — the point where an alias, a captured lambda,
a conditional access or a transitive helper would otherwise get the
funnel out.

**And the stack SPEAKS through one boundary, which is a different
question from whether a verb may run.** The entry gates above answer
"may this verb run", and they necessarily run BEFORE the verb's effect —
the only code that can retire the document mid-verb. So a stack can be
live at entry, retired by its own effect, and still composing:
`Commit`'s confirmation is built after the effect, and `Cancel` builds
its sentence from `spec.OnCancel()` as the ARGUMENT is evaluated.
`Speak` refuses when retired and is the only place `_announce` is
reached, so it reads retirement at EMIT time — which the argument
evaluation order makes both possible and necessary.
`ACancelRestorationThatRetiresTheDocumentComposesNothing` pins the verb
the first fix did not gate, and
`CanvasAnnouncer.RefusedAfterShutdownForTests` makes the claim
assertable in Release as well as Debug, where `Debug.Fail` is silent.

The refusal is SILENT, and that is the never-silent table's
PRECONDITION being absent rather than the table being broken (C4): a
retired document has no surface the user is reading and its announcer is
already shut, so a sentence composed there is A5's `Debug.Fail` rather
than something anybody hears. The return value is the answer, for the
caller that asked. `ARetiredStackRefusesEveryVerbAndHoldsNoRung` drives
every entry point rather than a sample of them, because "which ones did
we remember to gate" is exactly the question a list gets wrong — this
PR's own review history is four rounds of that shape.

**A commit can be REFUSED, and a refused commit KEEPS the mode.** M2 was
modelled as infallible and it is not: the canvas goes degraded or loses
its handle mid-mode and the funnel's admission says no. Mac keeps the
mode up in that case and consumes the key with the refusal, so the user
can fix the problem or cancel out with the restoration still available;
clearing the stack first loses the move with neither a commit nor a
restoration to show for it. `Commit()` therefore runs the effect FIRST
and clears `Active` only when it APPLIED — and the CONTROLLER's
confirmation is announced after the clear, so a sentence that asks
whether a mode is running still sees the stack as it will be.

**That guarantee is bounded, and the bound is worth stating.** An effect
that announces for ITSELF (the `Committed(null)` shape) speaks while the
stack is still up, because the effect runs before the clear. Today the
only reader of "is a mode running" is Where-am-I, which is a pull the
USER invokes — nothing calls it from inside a commit — so the two
orderings are indistinguishable in practice. If PR F gives an effect a
self-announcement that reports mode state, it wants
`Committed(confirmation)` instead, so the controller speaks it after the
clear.

Modelled as an OUTCOME rather than as mac's call-site pre-gate on
purpose: a pre-gate has to be re-implemented at every entry point — the
key, the header button, the palette row, the menu item — and the one
that forgets loses the user's work silently. The refusal's SENTENCE is
the effect's, because which refusal it is is the effect's knowledge; the
controller has none and inventing one would be host prose.
`ARefusedCommitKeepsTheModeAlive` is a conformance arm, so PR F inherits
the check rather than discovering the gap. Every sentence is core's (PR 0a). Nothing here
knows what a mode DOES, which is what lets PR F re-run the same suite
against the real move and resize modes: `CanvasModeConformance` is the
conformance body and `CanvasModeProbe` is its seam, and PR C supplies a
test mode whose commit and cancel record that they ran.

**M3's value is composed here and pinned against core.**
`ContainerValue` is `"⟨Mode⟩: ⟨object⟩"` — §W-C LABEL class, never
spoken, and the one host-side spelling of the mode names that survived
PR 0a on either platform (mac's `containerAXValue` is its twin).
`TheModeValueAgreesWithCoresOwnModeSentence` renders
`CanvasModeEntered` and takes the clauses out of core's own sentence, at
counts 1, 2 and 1,000 — the two places a host-side pluralisation drifts.
The count is interpolated UNGROUPED to match core's `mode_object`; core
exports `count_noun`, which GROUPS, and not the bare agreement rule, so
the noun is taken off `count_noun`'s answer and the number formatted
host-side. That is core's own documented split, reached through the one
export that exists.

**C8 — M4 is a closed table, and the two surfaces layered OVER the tab
are its recorded exceptions.** `HandleFocusDeparture(CanvasFocusDeparture)`
is total over five departures: `TabSwitch`, `PaneFocus` and
`WindowDeactivated` cancel with restoration and an announcement;
`ModalOverlay` and `MenuOpen` keep the mode alive.
`EveryFocusDepartureHasARecordedAnswer` enumerates the enum and names
the keep-alive set, so a sixth departure joins one side or the other by
decision.

**`MenuOpen` is CD-41's failure one surface over, and it was
self-inflicted.** Opening a top-level menu moves keyboard focus onto its
`MenuItem`, which drops `IsKeyboardFocusWithin` on the surface — so with
only the overlay arm, the shell's own Canvas menu cancelled the mode the
instant it opened and its Commit Mode and Cancel Mode items were dead
before the pointer reached them. PR E's and PR F's per-row context menus
are the M6 visible controls for every mode verb and would have inherited
it exactly. Classified by walking the focused element's ancestors for a
`MenuBase` — logical parents first, visual parents when the chain runs
out, because a `ContextMenu` lives in its own popup. One question covers
the menu bar, submenus and context menus; what is TESTED is the menu-bar
chain, because no canvas surface has a context menu until the §E/§F
row menus ship, and the popup arm is named as the untested half so the PR
that ships the first one owns the fact (m11).
`OpeningAMenuKeepsTheModeAliveAndLeavingTheCanvasCancelsIt` drives a
real menu and a real non-menu focus loss in one fact, so it cannot pass
on a surface that stopped classifying departures at all;
mutation-verified by collapsing the arm back into `PaneFocus`.

**Menu-then-elsewhere IS re-classified, since codex round 4.** This row
recorded the opposite for two waves and deferred the repair to PR F, and
both halves of that are now wrong. `IsKeyboardFocusWithin` is already
false once focus is in the menu, so a subsequent move from the menu to
another pane raises nothing on the surface — which is why the surface
watches its host WINDOW's keyboard focus instead. When the cause has
ended (no overlay, the keys not in a menu) and they have landed
somewhere that is not this surface, the destination is classified as the
`PaneFocus` it is, through `Depart`, so the mode stack and a deferred
landing hear the same thing from the same place.
`AModeHeldAcrossAMenuIsCancelledWhenTheReaderTurnsOutToHaveLeft` is the
driving fact and asserts that nothing is pending, so it is about the
mode rather than about a landing.

**Two boundaries, ratified rather than left implicit.** Only SAME-WINDOW
destinations are observable: a reader who leaves the menu for a different
top-level window deactivates this one instead, and the answer is DEFERRED
to the next activation, when the keys land somewhere and the same
classification runs. And only the mode's OWNING surface reclassifies —
see the affinity paragraph below — because the stack is document-shared
and a sibling pane acting on `IsActive` cancels a mode it is not running.
The older bounds still hold underneath both: `TabSwitch` and
`WindowDeactivated` fire on their own, and document retirement cancels
outright, so no mode outlives the thing it belongs to.

**A MODE HAS AN OWNER (codex round 5).** `Enter` REQUIRES one, and
`CanvasNavigator.EnterMode` is the production route: the invoking pane
names ITSELF, so a mode entered from the palette or from an open menu
belongs to the pane whose row the reader pressed. `Modes.Owner` is a
plain read of a field cleared where `Active` goes null, so no mode means
no owner. Only that surface may reclassify a departure on the mode's
behalf.

The owner is not looked up, and the two designs that looked it up both
failed in the same direction. Reading the navigator's attached presenter
gave a mode to whichever pane had the keys LAST — right for the palette
case by luck, and wrong the moment that pane detached, because the
presenter slot is a document-wide CACHE that any pane's departure
clears: a surviving pane was then refused a mode on a canvas that had
plainly been focused. **Identity belongs to the invocation**, which is
the only source that is true at the moment of the call. Without it, two panes on one
canvas meant every focus movement inside the owning pane fired the
sibling's watcher, which saw a mode active and the keys outside itself
and cancelled the mode its reader was in the middle of driving — before
they could reach the M6 controls that exist for that moment.
`AModeBelongsToThePaneItWasEnteredFrom` drives three NAVIGATOR-SEAM
ARRANGEMENTS — the keys on the projection, in a palette, in a menu — and
counts the restorations, because three routes to one cancellation is also
three routes to running the reader's undo twice. It covers no routed
entry, because there is none to cover yet: see the PR F obligation in the
travel table, which is the row that owns that work.

**THE THIRD AFFINITY, and the last one that should have to be
discovered.** A14's focus landing carries its OWNER; the navigator's
presenter carries the pane the reader is in; a mode carries the pane it
was entered from. One rule underneath: **anything DOCUMENT-SHARED that a
PER-SURFACE mechanism acts upon must say which surface, because "the
document has one" and "this pane owns it" are different facts and only
the second licenses acting.**

**AN AFFINITY IS A FIELD PLUS THREE LIFECYCLE GUARANTEES**, and each one
was learned separately on the request before the mode inherited all
three at once:

1. **It cannot be absent while the thing it names is live.** A mode
   REQUIRES an owner at entry (`Enter(spec, owner)`, guarded at runtime),
   and `CanvasNavigator.EnterMode` is the production route: the INVOKING
   pane names itself, and the navigator's cached presenter is not
   consulted at all. An ownerless active mode is excluded rather than
   handled — it was
   representable for one wave, and because `Owner` reads null both for
   "no mode" and for "a mode nobody owns", every consumer had to guess
   which it was seeing and the one that guessed wrong forwarded a peer's
   departure into a mode unrelated to it.
2. **It ends when the thing it names stops being an address.** A pane
   whose document is replaced under it reports the departure BEFORE it
   detaches (`HandleOwnerDeparture`, routed through
   `HandleFocusDeparture` so the commit-time deferral applies), and a
   pane that stops showing the canvas departs through the visibility
   edge. Otherwise a mode goes on naming a surface that no longer shows
   the document, and nothing is entitled to end it.
3. **It is RELEASED, not merely hidden.** The backing field is cleared
   at the one place `Active` goes null, so a completed mode holds no
   pane — and `Owner` is a plain read of that field rather than a
   read-through, because once the clear existed the read-through's own
   mutation could not be made to fail. That ordering is the lesson: a
   read boundary returning null is exactly what hid the same retention on
   the request properties (round 2's B3). Reading null is not holding
   nothing, which is why the retention half has its own observable
   (`HoldsOwnerForTests`) and why the clear — not the read — is what the
   facts assert.

Only the OWNING surface acts on the mode's behalf — in the host-window
watch and in `Depart` itself, which is the classifier the watch routes
through and which was taught this one wave later than the watch was. PR F
inherits the whole shape rather than three examples: its real modes
arrive into a two-pane world, and every new document-shared thing they
add gets asked the question at design time.

The `ModalOverlay` arm is a **divergence from t0 §2 M4's literal list**
and is recorded as CD-41 rather than implemented silently. Reasoning, in
full: t0 names the palette among the departures; the mac controller
excludes it deliberately after red-team #521, because Commit Mode,
Cancel Mode and the resize presets ARE palette commands. Cancelling on
palette open makes three registered verbs unreachable, contradicts M6's
own "Switch Control and Voice Control never depend on the keyboard-only
path", and would diverge behaviourally from the reference
implementation. The exclusion is ONE named arm of ONE total switch so
the decision is reversible in one place, and it is reported to the
controller as the one adjudication this task made against a normative
source.

The shell's answer is a static seam (`ShellOverlayIsOpen`) because the
surface is built by a XAML template with no injection point;
`TheShellInstallsTheModalOverlayAnswerForModeCancellation` pins that the
shell installs it, because a safe default left in place would silently
turn every palette open into a cancellation and no in-process fact can
see it.

**Retirement is M4's last case.** `Shutdown` departs with `TabSwitch`
BEFORE the announcer is silenced, so a mode running in a closing tab is
restored and says so.

**C9 — Commit Mode and Cancel Mode are the two DISABLED rows, and that
is the answer.** There is no vocabulary for "no mode is running" —
`CanvasBlockedReason::ModeBusy` is the opposite fact — so inventing a
sentence would put a string in the canonical corpus that mac never
speaks (0a-1's rule). Instead the two palette rows gate on
`CanCommitOrCancel` and the registrar's own unavailable sentence IS what
the user hears, which is the same shape `showVisual` has carried since
PR A. The header's Commit/Cancel buttons (M6) are hidden rather than
disabled for the same reason: a control for a mode that is not running
is not a temporarily unavailable action, it is not applicable.

`slate.canvas.toggleFollowSelection` registers and stays disabled until
PR D ships the viewport. "State only until D" was the alternative and it
is worse: the toggle's announcement says the viewport follows the
selection, and with no viewport that is a sentence about nothing.

**And on THIS branch the two mode rows are in the same position, which
the parity matrix now says.** They gate on `CanCommitOrCancel`, so they
execute the moment a mode is running — and nothing here ENTERS a mode.
The entrants are PR F's (move, resize, connect); C-lite ships the
machine and the M1–M7 conformance suite drives a TEST mode, which is
precisely the thing §B12's rule distinguishes from executable. So both
are `pending` in the matrix and out of the generator's delivered set,
and both return with F. The earlier reading — the contract recording
mode entry as latent while the matrix called the rows delivered — was a
split consequence nobody had swept for: the parent branch had the same
latency, but the matrix row was written when F was expected to follow
immediately.

**Clearing the filter is a READ VERB, on both of its paths.**
`Filter cleared — n cards.` is a claim about a canvas, and on a canvas
that cannot answer it is a false one: the count comes from an empty
outline, and "0 cards" reads as an empty canvas rather than an
unreadable one. Both the palette verb and the Escape rung go through the
admission mapping. The RUNG still consumes its press — there was a
needle, so Escape belongs to it and must not fall through — and still
clears the needle, because clearing is what the user asked for; only the
SENTENCE is the mapping's. `EveryReadVerbAnswersInEveryLoadState` now
asserts the exact expected sentence per state rather than that something
spoke, which is the assertion that let this walk past.

**C10 — The filter is ONE view, and every consumer reads it. THE
IMPLEMENTATION IS THE SYNCHRONOUS INTERIM.**

`CanvasFilterView` carries the rows, whether they are narrowed, whether
they answer the needle NOW, and the matched ids — and the outline rows,
the table rows, the header's result summary, the announced count and
Where-am-I's filter clause all read that one value. The invariant is
*displayed rows == announced count*, by construction rather than by
agreement.

The MATCH is core's `canvas_filter` (0b-13/0b-14): title, the kind type
word, any one element of the group path, and the activation target. The
needle goes over UNTRIMMED — core trims it, and an empty needle matches
everything. The answer is memoized per needle and invalidated by every
publish, because the ids can move under an unchanged needle; a read
cache never outlives the rows it describes.

**A stale answer stays on screen rather than widening.** When the needle
changed and nothing could answer it, the previous rows remain and
`Current` is false: widening silently back to the full outline would
show every card while the field still claims to be filtering, and then
speak that number as a match count. Both the announcement
(`AnnounceFilterCount`) and the summary LABEL (`FilterSummaryText`) ask
the state mapping for the sentence in that case, so neither has a second
opinion about which sentence the state owes.

**THE INTERIM, and its cost, stated rather than discovered.** The match
runs SYNCHRONOUSLY, inside the `Filter` getter, on the dispatcher,
taking `_ffiLock` — PR A's recorded precedent for a whole-model read,
and the shape this PR ships. Every keystroke pays one `canvas_filter`
call on the UI thread; that is the interim's cost, and it is why the
redesign exists.

**Two costs this row used to claim are NOT reachable, and saying so is
the point.** It said a keystroke arriving DURING a load waits for the
lock the load body holds, and that typing then could mix the new
handle's answer with the old outline's rows. Neither survives the fix
for the stale count: `Load` moves the state to `Loading` before it
starts any work, and the getter returns before it queries whenever the
document is not rendering rows — so during a load there is no query to
block and no answer to mix. The window closed as a side effect of a
different fix, which is exactly the kind of obsolete cost a row keeps
claiming until someone re-reads it.

**A count is only CURRENT while rows are on screen.** C10's one
invariant is *displayed rows == announced count*, and a reload broke it
from the STATE side rather than the filter side: the projections
collapse while the canvas is `Loading`, and the memoized answer stayed
current, so the region read "2 of 5 cards match" over a pane showing
nothing. The view reports `Current: false` whenever the document is not
rendering its rows, which routes both the label and the announcement
through the state mapping — the honest sentence for exactly that window.
The ROWS are unchanged while it lasts: widening them would make the
reload flash every card the moment it finished.
`TheFilterSummaryNeverCountsRowsTheSurfaceIsNotShowing` samples the
summary and the materialized rows together across the whole window.

That is the whole reason the redesign PR exists. Moving the query off
the dispatcher makes the rows and the answer arrive on different frames,
and correlating them is what four review rounds and two design passes
were spent on — the publication transaction, the projection unit, the
one-channel ordering and their censuses. None of that ships here,
because none of it is needed by a match that returns before the getter
does. What the synchronous form buys is that nothing lands BETWEEN the
two numbers; what it does not buy is that the handle they were taken
from is the handle the rows came from.

**WHAT TRAVELS TO THE REDESIGN PR**, so nobody re-derives it. The names
below are in PLAIN TEXT rather than backticks, deliberately: a backtick
in §C is a citation, the citation census checks that every one of them
names something this branch actually has, and none of these do. The
census made that rule visible while this table was being written, which
is the census working.

| Travelling | What it was |
|---|---|
| **The originating surface, through the routed-command boundary (PR F)** | A mode belongs to the pane that invoked it, and `CanvasNavigator.EnterMode` takes that pane as an argument. Nothing in the shell carries it yet: the command helper resolves `ActiveCanvasDocument` and drops the parameter, so a palette or menu entrant arriving in PR F would have no way to say WHICH pane asked. F owns closing that — the routed command must carry the originating surface end to end — and owes two-window facts where the pane that BOUND the command and the pane the reader is in deliberately differ, because that is the only arrangement in which "the active document" and "the pane that asked" give different answers. |
| The off-dispatcher match | the filter body on PanelWorkScheduler, the doubled generation guard, the outline-identity third guard, the panic-class catch and its injected-fault fact |
| The failed-answer bit and the four-branch summary | "ran and failed" vs "has not run yet" vs "no handle" — distinctions only an async match creates |
| The projection unit | rows, table rows, id index, targets, subpaths, the adjacency memo, the answered needle, the matched ids and both filtered halves as ONE immutable value |
| The publication transaction | staged writes, queued notifications, the rows→selection→properties order, nesting by joining, and the retirement mute |
| Silence-first teardown | the speak/silence/release phases, the five-channel detachment and its handler-list observable |
| Their censuses | the derived notification census and the teardown-order census |
| Their facts | the reload / state-render / state-observer / selection-observer / mid-publication Where-am-I family, and the retired-document family |

The full review history for all of it is in this document's round record,
which stays: the redesign PR inherits seven rounds of findings, two
design passes and a rule-4 trip, and starting from that is the point of
splitting rather than reverting.

**`FilterActive` carves out what .NET would have got wrong, and the
claim is bounded to that.** Foundation's `.whitespaces` does not include
newlines, so a needle of nothing but a newline reads as ACTIVE on mac
and (core trimming it) matches everything, while .NET's
`IsNullOrWhiteSpace` would call it inactive — `IsFilterActive` carves
out them so that needle behaves as mac's does. It is NOT a full
transcription of `.whitespaces`, which is Zs plus tab: U+000B, U+000C,
U+0085, U+2028 and U+2029 read active on mac and inactive here.
Recorded rather than chased — they are unreachable from a keyboard and
belong to the trimming differences CD-22 already covers.

**The table's OTHER summary is outside this invariant, and that is
recorded rather than assumed.** `Canvas table: N cards, M groups.`
counts the whole canvas, filtered or not: it is a never-announced label
describing the document, not a match count. The grid binds the FILTERED
rows and composes that summary from the unfiltered ones.

**The table's Ctrl+F is the substrate's** (spec §7's "table: grid
`FilterCommand`"): the table subscribes `FilterRequested` to the same
`FilterCards` verb, and the navigator stands aside on the table rather
than shadowing the substrate's route. This spends B8's Ctrl+F clause,
which said so.

**Filtered-out cards are a VIEW.** Selection is untouched by filtering
and by clearing, and no `canvas_apply` is anywhere near it.

**A reload with an active needle re-asks**, or the surface would show
every card while the field still claimed to be filtering. It does not
ANNOUNCE the new count: the count is announced from the filter FIELD,
by the keystroke that asked for it, and a reload is not a keystroke.
Whether a reload should speak its new number — the rows changed under a
reader who was filtering — is a live question, and it is the async
form's to answer, because only there does the re-ask land on a frame of
its own. It travels with the rest.

**The cost of typing, stated rather than assumed.** Each keystroke is
one memoized `canvas_filter` call plus ONE projection rebuild — the
needle setter raises the republish and the surface deliberately does not
also render on the property change, or every keystroke would rebuild
twice. A rebuild at 2,000 rows is the same work PR A already budgets and
measures on the open path (`A17`'s §K fact, in both scheduling modes),
so the per-keystroke cost is bounded by a number that is already
asserted rather than by a hope. mac has the same shape (its list
re-renders from `filteredOutline`); what neither host does is re-run the
match, which is what the memo is for.

The rebuild reads the UNFILTERED outline as well as the survivors now
(CD-45's correction), so its walk is up to twice what it was — two
linear passes over 2,000 rows plus a dictionary, still inside the same
measured budget, and the honest number rather than the flattering one.
Containment that a reader can trust is worth a second pass; a rebuild
that scales worse than linearly would not be.

**The filter-focus request is A14's TWIN — durable, ADDRESSED, and
completed on the document.** Ctrl+F and the palette row reach the same
verb, and only one of them worked: the palette owns the keys while it
closes, so every surface read as ineligible — and the one-shot
flag the verb used THEN was acknowledged before eligibility was even
asked, so nothing retried. A
verb with two routes must not work on one of them.

Durability alone was not enough, and the shape of what was missing is
worth keeping. A request that survives an ineligible surface is a
request that ineligible surface KEEPS: two panes share one document, so
the pane that could not satisfy it pulled the reader into ITS filter
field the next time it saw the keys — the same defect one arrangement
over. So `CanvasFilterFocusRequest` carries an OWNER, like
`CanvasFocusRequest`: a surface delivers only what is addressed to it,
delivery calls `CompleteFilterFocus` so peers stop holding it, and a
newer request supersedes an older one rather than being consumed by its
late delivery. Supersession is by REFERENCE IDENTITY of the record —
there is no generation counter and never was one after codex round 1's
ABA finding; completion compares the record the surface is holding with
the one the document has, so a late delivery of a superseded request
clears nothing even when the two are value-equal. The one unaddressed
case is a document no surface has ever held the keys on, where the first
eligible surface takes it rather than letting the verb evaporate.

**A live address is a tab PAIRED WITH THIS DOCUMENT.** The tab-set
sweep drops requests addressed outside it, and the predicate is asked
per document, not per window: a pane that is still open but now shows a
different canvas (`TryOpenItem`'s replace arm) is not an address for the
canvas it left. Codex round 3, M3.

Retirement is the twin's other half, and it is answered at the BOUNDARY
rather than by a list of clear sites: both requests READ as absent once
the document is retired, because a surface consults the request and
never the document's liveness, and the workspace can raise a landing
after a close. `Shutdown` also drops both fields, which is a different
job — a retired document must not hold a closed tab's `DataContext` —
and `HoldsPendingRequestsForTests` is how that half is asserted, since
the boundary makes the properties read null either way.

The re-ask list is the SURFACE's, deliberately narrower than A14's:
`Loaded`, `IsVisibleChanged`, `DataContextChanged`, keyboard focus
arriving, the host WINDOW activating, the model changing and the request
itself changing. (The activation arrived with the deferred-restoration
hold two waves after this list was written, and the list said it was
exact — which is why it is spelled out again here rather than left as
"and so on".) Container
realization is not on it, because `Render` never hides the filter field
— only the summary and Clear follow the needle, and the projections
follow `ready` — so no publish, state change or realization can turn an
unsatisfiable request into a satisfiable one.

**C11 — Where-am-I is one render, spoken and shown.** The navigator
builds one `CanvasWhereAmI` event from core's `canvas_where_am_i` plus
the host state core does not hold (marked, the active mode, the filter
state), renders it ONCE into `WhereAmIText`, and announces the same
event — so the panel and the speech cannot drift. Always verbose-grade
by construction: the event has no verbosity parameter.

The panel is a **transient focusable region, not a `ModalSurface`**
(recorded per spec): it takes no keys away from anything, the canvas
behind it stays live, and registering it would put it through the #1118
chord admission it has no business being in. `LiveSetting = Off` — pull,
not push: the announcement is what speaks, and a live region would say
the same sentence twice. Escape dismisses it ahead of every ladder rung
while it is OPEN (CD-47), and returns focus to the element the reader
came from when they were inside it — with C6's SEAT RULE as the fallback
when that element is gone, and no focus move at all when they were never
in it.

Nothing selected falls back to the first row in reading order, and an
empty canvas answers `Canvas is empty.` — the pull surface always
answers, which is the failure t0 §1.4 exists to prevent.

**The filter clause keys on NARROWED, where mac keys on the needle**
(m3). On this branch the divergence has ONE reachable cause, and it is
worth naming precisely rather than describing states the synchronous
form does not have: an active needle with no memo and no handle to build
one — the document cannot answer, so nothing is narrowed, and Where-am-I
omits the clause while the field still reads as filtering. (The async
form adds an in-flight first answer and a ran-and-failed query; both
travel with it.) Deliberate either way: keying on the needle would make
the clause read "9 of 9 shown" for rows that nothing narrowed, and
C10's whole invariant is that the count describes the rows on screen. A
micro-divergence recorded rather than matched.

**Containment comes from the WHOLE canvas, never from the survivors**
(CD-45, and the correction that made CD-45 true). Depth is a position in
core's reading order, so a depth stack run over the FILTERED rows
attached a survivor whose own group was filtered out to whatever
survivor happened to be shallower and earlier — a card from an unrelated
branch, presented to a screen reader as inside a group it is not in.
CD-45's promotion-to-root was the right rule described by the wrong
mechanism. The parent chain is computed once from the unfiltered
outline, and each survivor attaches to its nearest surviving TRUE
ancestor or becomes a root.
`AFilteredOutlineNeverNestsACardUnderAGroupItIsNotIn` pins it on two
sibling branches, one matching by its own label and one by its child's
text — the shape that fabricates.

**C12 — Focus delivery lands the reader and says NOTHING (the carried
A14 defect).** §B filed it: a delivery to `LastActivatedNode` when the
selection has since moved elsewhere reached `SelectNode` and narrated a
`CanvasMovedTo` on top of the row the screen reader was already reading.
Both projections now run the whole delivery inside their sync guard and
seat the shared selection through `SeatSelectionSilently`.
`AFocusDeliveryToANodeOtherThanTheSelectionDoesNotDouble` drives BOTH
projections with the premise established (the landing node really does
differ from the selection) and asserts the funnel posted nothing.

**Why the selection still follows, and CD-40 records it.** The task
brief asked for "lands focus only, never mutates selection". That is not
reachable on either projection: WPF's `TreeViewItem` selects itself on
`GotFocus`, and a `DataGrid`'s currency IS its focused row. The
reachable choice is not "seat or don't", it is "audibly or silently" —
and R-B says there is exactly ONE selection, so the reader and the
selection agreeing is the contract rather than a side effect. Silent it
is; the doubling, which is the half that reached the user, is gone.

**C13 — Verbosity is read at every announce.** The document's
`Verbosity` is a delegate over `CanvasPreferencesViewModel`, not a
cached value, so a change takes effect on the next announcement of every
open canvas with nothing to push.
`VerbosityIsReadLiveAtEveryAnnouncement` moves the same way at all three
levels and gets three different lines.

The preference persists as `canvasVerbosity` in `AppPreferencesState`.
**What is shared is the VALUE spelling, not the store, and the row says
which**: `terse` / `standard` / `verbose` are mac's own
`CanvasVerbosity` case names, so a future shared schema (W8-1 owns the
settings surface) needs no value migration. The STORES are peers rather
than one file — mac keeps `CanvasPrefs` under the `slate.prefs.canvas`
UserDefaults key, which is device-local, and Windows keeps this field in
its device-local preferences JSON; the container formats differ and
neither is the vault's `.slate/prefs.json`. The task brief's "a synced
vault reads identically" is therefore not what shipped and is not
reachable from `AppPreferencesStore` at all; recorded rather than
implied away. Default `standard`; an unknown or version-skewed key
degrades to the default like every other field.

**The setting announces nothing of its own**, and that is a decision.
The three menu items are CHECK items bound OneWay to the level (WPF has
no radio menu item), so a screen reader speaks the selected level from
the element itself (t0 §3's inspectability, and the shape mac's Settings
toggle has), and the honest confirmation of "you are now at Verbose" is
the next card you move to.
Inventing a canvas event would put a string in the corpus that mac never
speaks; composing one host-side is what R-C forbids.

**Three unregistered rows, one parameterized command.**
`windows.canvas.setVerbosityTerse` and its two siblings are catalog
dispositions recording the closed set of levels; the delivery is one
`SetVerbosityCommand` the menu binds with the level as its
`CommandParameter` — the math-verbosity precedent, including its
consequence that a parameterized command is not palette-reachable.

**C14 — Labels are mac's inventory, and the additions are named.**
`Filter cards`, its hint, `Clear` / `Clear filter`, `Where am I?` and
`Close` are mac's strings verbatim. Two are Windows-authored and
recorded as such: the result-summary region's NAME (`Filter results` —
mac's summary is an unlabelled caption, and an unlabelled region is not
readable on demand, which is the whole point of t0 §3's "result summary
element") and the M6 buttons' accessible names (`Commit Mode` /
`Cancel Mode`, the mac catalog's verbs, with short visible text beside
the mode's own value).

**C15 — The chord rows, and the one recorded divergence.** Eight rows
carry a `ChordScope.Canvas` chord — Ctrl+Alt+Shift+I, Down, Up, Right,
Left, Ctrl+F, Enter, Escape — and five more register with no chord
(enter/exit group, trace path, clear filter, toggle follow selection),
because mac gives them none either and rule R1 makes the palette the
path. Mac's chord is recorded on every row that has one, so the mac
column shows the real binding rather than reading as an absence; Return
and Escape carry none, because mac's palette rows declare no hotkey and
the mac column's per-character glyph walk has no spelling for a named
key.

`slate.canvas.whereAmI` is the divergence (owner decision D-2): the rule
predicts Ctrl+Alt+I, which `slate.view.toggleRightPane` holds at GLOBAL
scope — live at the same moment as a focused canvas, so a real collision
and not a disjoint pair. Disambiguated with Shift per the G18 precedent
and recorded on the row.

**The delivery site is SCRAPED.** `ChordTableTests`' canvas scrape reads
the navigator's `AddChord` calls and compares both directions against
the table's Canvas-scoped rows, so a chord handled with no row — or a
row claiming this scope that nothing delivers — fails naming it. The
`ChordScope.Canvas` exemption PR A left in
`ScopesWithoutAProductionScrape` is gone, which is what that entry said
would happen.

**C16 — Two command rows share a chord, by scope, and it is recorded per
PAIR.** The W1 invariant was "no two COMMAND rows share a chord",
globally. Surface rows always had the focus-scope carve-out; W6-1 is
where COMMAND rows first need it, and D-2 anticipated exactly that ("the
same-string/different-scope precedent Ctrl+F already sets"). The check
is now scope-keyed with a named disposition list
(`SharedCommandChords`), checked for staleness in both directions, and
`W1QuickSwitcherAndChordTests` restates the same rule over the projected
file.

**Recorded per pair rather than inferred from "the scopes differ",
because differing scopes are not automatically disjoint.** The canvas
TABLE *is* a grid, so `ChordScope.Grid` and `ChordScope.Canvas` are live
at the same instant there — and the reason Ctrl+F is still correct is
not that they never coexist, it is that both rows end in the SAME
action. A rule that only compared scopes would have accepted that pair
for a reason that is false.

**C17 — The coalescing tripwire is symmetric now.** The task brief
carried "the coalescing-class Swift tripwire (0b m1)" as an open item;
it is not — PR 0b shipped it as contract 0b-17
(`the_mac_coalescing_switch_matches_the_pinned_class_list`), and this
task VERIFIED it rather than re-implementing it. What was genuinely
missing is the other half, and 0b-17's own failure message is where it
shows: it says a class the switch misses "is spoken uncoalesced on mac
and coalesced on Windows", which assumes the Windows copy is faithful,
and nothing checked that.

`the_windows_coalescing_switch_matches_the_pinned_class_list` closes it,
parsing `CanvasAnnouncer.cs`'s switch expression against the same doc
comment in both directions. The pinned-list parser is now ONE function
both tripwires call: two copies of it would be the same curated-list
defect one level up, which is the class 0a's round 4 and 0b's rounds 1–3
were both invoked over. This is the "two tripwires, symmetric" doctrine
0a-3 already set for the corpus mirrors, applied to the class list —
and it matters here because PR C is the first slice whose own behaviour
depends on the FILTER class existing.

Mutation-verified: dropping `CanvasResizeGeometry` from the C# switch
fails it naming the variant and the class.

**The ledger line, closed explicitly.** 0b m1 = mac half in 0b-17
(PR 0b) + Windows half in C17 (PR C). 0b-17 carries the same
cross-reference, so the trail reads the same from either end and nobody
re-opens it looking for a missing Swift parser.


### Red-team round 1 minors — dispositions

Every minor from `redteam-c-round1.md`, with what happened to it. The
ones marked DEFERRED name an owner rather than a date, per the ledger
convention.

| # | Disposition |
|---|---|
| m1 | **DEFERRED — close-out, and it is a CLASS not a canvas row.** Re-clicking a checked verbosity item unchecks it visually because `IsCheckable` toggles `IsChecked` while the OneWay binding only re-pushes on `PropertyChanged` and the setter early-returns on the same value. Shipped by the math-verbosity precedent this row copied, and live on all five groups (math verbosity, speech style, braille, code preamble, canvas). One fix — re-raise the three `PropertyChanged` unconditionally, or handle `Click` — covers every group, and doing it here would fix a canvas symptom of a shell defect. Recorded as the inspectability class's 6th appearance. |
| m2 | **TAKEN (record).** C10 said "mac's predicate, spelled out"; it is not exactly — Foundation's `.whitespaces` is Zs plus tab, so U+000B, U+000C, U+0085, U+2028 and U+2029 read ACTIVE on mac and inactive here. The claim is narrowed to what the code does and the five code points are named. |
| m3 | **TAKEN (record).** Where-am-I's filter clause keys on `Narrowed` where mac keys on `filterActive`, so there are states in which a needle sits in the field and Windows omits the clause. On THIS branch the cause is the synchronous one and only one: an active needle with no memo and no handle to build one — the document cannot answer, so nothing is narrowed and the clause is absent while the field still reads as filtering. (The async form adds two more causes, the in-flight first answer and the ran-and-failed query; they travel with it.) Kept on `Narrowed` deliberately either way: keying on the needle would make the clause say "9 of 9 shown" for rows nothing narrowed, which is a false number, and C10's whole invariant is that the count describes the rows on screen. Recorded as a micro-divergence rather than matched. |
| m4 | **TAKEN (upstream note).** Where-am-I's no-selection fallback is the first UNFILTERED row on BOTH hosts, so with a filter active it can describe a card that is not on screen. Shared-reference quirk; filed in the mac-details register rather than fixed one-sided. |
| m5 | **TAKEN (upstream note).** Enter-group, follow-connection and trace-path can seat a filtered-out node, and enter-group narrates it. Verified mac-parity (`canvasSelect` has no filtered-set check either), so not a Windows defect — but it grinds against CD-40's ratified "the reader and the selection agreeing IS the contract", so the verb family is recorded together. |
| m6 | **TAKEN (fixed + fact).** `DismissTransientRegion`'s detail arm ignored `FocusRow`'s answer, so a row that vanished under an external edit left focus on the window root. `FocusRow` now RETURNS whether the row took focus — which is a seam improvement in its own right — and the arm falls back through C6's SEAT RULE exactly as `CloseWhereAmI` does. |
| m7 | **DEFERRED — PR E, with the expansion-state decision.** A durable focus request naming a filtered-out node stays pending and delivers a surprise jump when the filter later clears. `FocusLandingNodeFor` reads the unfiltered map by design (A14's landing rules predate the filter). Superseding the request when the filter excludes its target is a change to A14's own state machine; it belongs with the request-lifecycle work PR E already owns, not bolted on here. |
| m8 | **TRAVELS.** It was taken and fixed — the scheduler guard scanned one file, so a `canvas_filter` call added anywhere else under `Canvas/` would have evaded the one-caller claim, and it scans the whole directory now. But the guard it strengthens is the off-dispatcher scheduling guard, which is not on this branch: a synchronous match has one caller by construction, in the getter, with no scheduled body for a second one to hide in. Both go to the redesign PR together. |
| m9 | **DEFERRED — PR D, with the surface's focus map.** Ctrl+F reaches nobody when the table shows and focus is in the HEADER: the navigator stands aside for the grid's own gesture, and the grid's binding needs the grid focused. Routing the stand-aside on "the GRID owns focus" rather than "the table is showing" is the fix; it wants the header/projection focus map PR D is already building for the renderer's tab order, and the palette row covers the gap meanwhile. |
| m10 | **RECORDED (no action).** The `Key.System`/`SystemKey` translation and the `Keyboard.Modifiers` read have no unit exercise — `PressKey` documents that it bypasses the routed event for modified chords, and the CI journey's real Ctrl+Alt+Shift+I is the executable coverage. Named so nobody reads the unit fact as covering it. The AltGr note (Ctrl+Alt chords are typeable glyphs on some layouts) is recorded with it; no current canvas chord collides with a common AltGr glyph. |
| m11 | **DEFERRED — PR E/F, with the first context menu.** The `MenuOpen` classification's visual-parent fallback (the `ContextMenu` popup chain) is exercised nowhere, because no canvas surface has a context menu until E/F ship the row menus. C8's "one question covers the menu bar, submenus and context menus" is narrowed to what is tested, and the popup arm is named as the untested half so the PR that ships the first context menu owns the fact. |
| m12 | **DEFERRED — PR F hand-off (recorded below).** A mode survives a document RELOAD: the publish republishes rows under an active mode and no M4 departure fires. Inert for C's test mode; F's transient geometry commits against moved rows, where the funnel's CAS is the backstop. |
| n1 | **TAKEN.** `ArrowFollow`'s comment repeated the spec's false "the precedence mac pins"; rewritten with the mac file it actually comes from (CD-48). |
| n2 | **TAKEN.** C13's "checkable radio items" is wrong — WPF has no radio menu item; they are check items with a OneWay `IsChecked`. Reworded. |
| n3–n5 | No action needed (checked, recorded, or H's). |

### Hand-off to PR F (the mode stack's consumer)

1. `CanvasModeConformance` is the suite: supply a probe with a real spec
   factory AND a real REFUSING spec factory, and every M1–M7 arm plus
   `ARefusedCommitKeepsTheModeAlive` runs against the real modes.
2. A commit effect that cannot apply must return
   `CanvasModeCommitResult.Refused()`, announce its own reason, **and
   have applied NOTHING** — a refusal is atomic by contract, because the
   mode stays up and the user may commit again, so a half-applied
   refusal would apply that half twice. `Refused()` is the answer for
   "admission said no", not for "it partly worked": a partial apply is a
   failure the funnel must undo before it refuses. Returning `Committed`
   on a refused funnel apply drops the mode and the user's transient
   state with it.
3. A mode survives a document RELOAD (m12): the transient geometry F
   holds was computed against rows that may have moved. The funnel's CAS
   is the backstop, and whether a reload should cancel the mode is F's
   call — it needs F's transient holder to answer.
4. Menu and context-menu opens KEEP the mode alive (C8/CD-41); the
   context-menu classification arm is untested until F ships one (m11).
5. A commit effect may not assume the stack is transitionable while it
   runs: `Cancel` refuses and a focus departure DEFERS until the outcome
   is known. An effect that wants to abandon the mode returns
   `Refused()` and lets the deferred departure (or the user) end it —
   calling `Cancel` from inside its own commit gets nothing.

### Tests that pin PR C

`apps/slate-windows/tests/SlateWindows.Tests/CanvasNavigatorTests.cs`:
C1–C8 and C10–C14 against a REAL `VaultSession` and real `.canvas`
bytes, with every announcement read as the RENDERED text the production
funnel posted — a machine that built the right variant and rendered
nothing would pass an object-level check, and what the user hears is the
subject. Fixtures are written inline and shaped for the claims: a
grouped board with a multi-edge card and an incoming edge, nested groups
including an EMPTY one, a three-node CYCLE plus a dead end, an empty
canvas, a non-JSON one, and a 2,000-node grid the movement fact crosses
end to end. The windowed facts drive the real `CanvasSurfaceView`: the
ladder, the M3 value read off the surface's own peer, the M6 buttons,
the filter field's `Value` pattern and its summary region, the
Where-am-I chord landing focus in the panel, and the arrow's
defer-or-answer split — the last with its R2 premise asserted first, so
a focus failure cannot pass one half for a reason that is not the
behaviour.
`apps/slate-windows/tests/SlateWindows.Tests/CanvasModeControllerTests.cs`:
M1–M7 over every `CanvasMode` the vocabulary knows (enumerated, so a new
one joins without anyone remembering), the M5 ladder table, and
`CanvasModeConformance` — the body PR F re-runs against the real modes.
`apps/slate-windows/tests/SlateWindows.Tests/Censuses/CanvasAnnouncerCensus.cs`:
`TheSnapshotVisibilityPredicateMatchesTheSurfaceRender` and
`TheShellInstallsTheModalOverlayAnswerForModeCancellation` — the two
source guards for properties no in-process fact can reach. The third,
the off-dispatcher scheduling guard, travels with the redesign PR: it
guards a scheduled body this branch does not have.
`apps/slate-windows/tests/SlateWindows.Tests/ChordTableTests.cs`: the
canvas scrape, `SharedCommandChords` (C16) and the D-2 divergence row.
`apps/slate-windows/tests/SlateWindows.Tests/CanvasTableTests.cs`:
`NoExportProducerAndTheFilterChordFocusesTheCanvasFilter` — B8's Ctrl+F
clause, completed.
`apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs`:
`CanvasSurfaces_NavigatorFilterAndWhereAmI_AreClean` — the spec's
"Canvas_NavigatorJourney" under this project's naming convention. CI
arbitrates.
`crates/slate-uniffi/src/lib.rs`:
`the_windows_coalescing_switch_matches_the_pinned_class_list` (C17), the
symmetric twin of 0b-17's mac tripwire, mutation-verified by removing
one variant from the C# switch.

---

## PR C-unit — the coherent projection unit (design, FROZEN as the ratified baseline; by the user's split ruling the model is designed here and the presentation periphery is a bounded ledger of unresolved obligations, and by the user's design-by-implementation ruling the claims prose could not close are a second ledger discharged by code)

### THE FREEZE — read this first

**This section is CLOSED to further prose revision.** It is the ratified
baseline for implementation, frozen at revision 7 after seven
adversarial rounds. Nothing below is to be re-argued on paper; the next
thing that changes any of it is code.

**Why, in one paragraph.** Seven rounds ran 10, 11, 12, 8, 5, 4, 8
blockers. The first four rounds each corrected something structural and
the count fell. Round 7 broke the trajectory, and the shape of the break
is the finding: six of its eight blockers were class (i) against the
periphery of ONE mechanism — the compare-and-swap publication — while
the mechanism's core was ratified in the reviewer's own first sentence,
*"the CAS correctly prevents two writes against the same predecessor"*.
That is the signature of a design whose remaining questions are not
answerable in prose: every prose answer to an ownership, progress,
purity or census question generated another prose question about the
answer. The user ruled: **design by implementation.** Prose stops
arbitrating what code arbitrates better — which is the same lesson the
split recorded about the periphery, now reaching the model.

**What is RATIFIED, and is therefore the architecture to build.** These
survived every round and are not open:

* the lease → population → unit chain, with DOCUMENT as the coarsest
  currency and the nesting rule of U3;
* ONE publication slot as the single mutable currency authority, with
  currency DERIVED by comparison and never carried;
* the five dependency classes and the membership principle;
* provenance by TYPE rather than by caller declaration — sealed results,
  minted tokens, and a payload's class read from its own type;
* the CAS publication DIRECTION: publication is a compare-and-swap
  against a decision snapshot, which round 7 confirms prevents two
  writes against the same predecessor;
* the pure-query counter-position, and the state product built on it;
* the split itself, and the T-row ledger the periphery lives in.

**What is RECLASSIFIED.** The claims surrounding the swap — its
ownership transfer, its effect linearization and thread story, its
progress, its purity, and the censuses that would police all four —
stop being CONTRACTS and become **IMPLEMENTATION OBLIGATIONS**, carried
in their own ledger below as I1 through I8. Each one carries round 7's
finding VERBATIM as its acceptance criteria, exactly as the T-rows carry
round 3's. They are discharged by code, facts and mutation batteries and
arbitrated by the implementation gauntlet — not by another revision of
this document.

**How to read the rest of this section.** Everything below was written
as a contract and is now a SPECIFICATION with two grades. Where a
paragraph is in the ratified list above, it binds. Where it makes one of
the eight reclassified claims, it records the DIRECTION the
implementation should take and is marked with its obligation number; the
round-7 text in the I-ledger, not the paragraph, is what the code must
satisfy. Nothing has been rewritten to look closed, because a paragraph
edited to survive a review it already failed is the exact failure this
document has spent seven rounds learning to gate.

### The record of how it got here

**This section was a DESIGN ratified before any code exists**, frozen at
revision 7. Rounds 1–6 returned NOT SOUND with ten, eleven, twelve,
eight, five and four blockers, and not one of them cost an
implementation round — which is the DESIGN-FIRST ruling working. Round 3
tripped the convergence tripwire and the user ruled: **split the
design.** The count then dropped three times, 12 to 8 to 5 to 4, with
the tripwire quiet each time. Round 6 attacked the design's one declared
load-bearing premise — that publication is dispatcher-only — and was
right that it was FALSE against inherited code; revision 7's answer was
a removal rather than a mechanism, and the publication became a
compare-and-swap that needs no thread at all. Round 7 then found eight,
and the ruling above followed.

**Goal.** Move the filter match off the dispatcher without re-opening the
coherence class, and land the projection unit, the publication and the
teardown that §C's travel table sends here. Everything C-lite shipped is
INHERITED, and the authority census below says of every mutable authority
the derivation finds whether this design absorbs it, seams it, or removes
it.

### Scope, and the line the split draws

**IN — the model.** The publication slot and everything published through
it: the lease, the population, the unit, the load and filter request
schedules, the resolved selection and the durable intents. Query
admission and revalidation. **Model DATA provenance** — who mints an
operation token, how a result carries it, and how the funnel checks it.
Effect classification and the validation of model-class effects.
Teardown's model sequence. The censuses that guard all of it.

**OUT — the periphery**, six obligations carried in the ledger below with
round 3's findings as their acceptance criteria: presentation atomicity;
the apply's reentrancy and its exception and nesting semantics; the
composition of data provenance with C-lite's SURFACE provenance and the
forgeability of the surface half; teardown's speech window; and focus
DELIVERY as distinct from the focus request.

**Round 4 moved one line.** Revision 4 left the whole of provenance in
T2 and then had U7 and U8 rest on tokens — blocker 1, and it was right:
a model contract cannot be self-contained while the rule for who may
mint the thing it validates lives in an unresolved row. The MODEL half
of provenance — minting, carriage, the funnel's check, and now the
PROVABILITY of a payload's declared class — is pulled in below as U14.
What remains in T2 is the SURFACE half: which pane initiated an
operation, whether that component can be forged or mismatched, and the
validation of presentation-addressed effects.

**Rounds 5 and 6 moved no line at all.** All nine of their blockers were
inside the model, and both rounds' walks confirm no new dependency on
T1–T6. Round 6 in fact pushed one thing further OUT: the swap makes the
model thread-agnostic, so the only place affinity is still a problem is
WPF's own apply, which is T1 and T3 and always was.

**The rule that makes the split honest:** no model-side contract may
depend on an unresolved row. Where the model must reference the
periphery, it states its guarantee as ENDING at a named seam rather than
assuming the other side of it. The four contract-level seams are
unchanged from revision 4 — U7 ends at presentation-addressed effects,
U8 ends at the focus request, U12 ends at the terminal publication, and
state-product row 5 refuses rather than describes — and U13 now FAILS if
a model contract consumes a member marked unresolved, which is the rule
made executable rather than promised.

### THE MODEL INVARIANT — one mutable field, and where it stops

**There is exactly one mutable CURRENCY authority in the model: the
publication slot.** Every model currency question — admission,
revalidation, effect legality, delivery acceptance — is a comparison
against what that one field holds. No model object carries a currency
flag, and a model state that would need a second mutable currency
authority is a finding.

**The word CURRENCY is doing work, and round 5's blocker 3 is why.**
Revision 5 said "one mutable field" and then gave every load result an
atomic claim, which was a second mutable authority deciding whether a
delivery may act — two executions with identical publications could
behave differently. U4 below removes the claim outright by making the
spend a publication transition, which is the design's own move. What
remains is ONE piece of non-currency mutable model state, enumerated
here rather than glossed: **the lease's close-once record.** It is never
read to decide admission, validation, effect legality or delivery — only
to make physical close idempotent — and it carries that disposition in
the reconciliation table. A second non-currency authority would be a
finding too; the point of naming this one is that the invariant is now
checkable instead of approximately true.

Revision 3 claimed the invariant absolutely, and round 3 was right that
the absolute claim could not stand. Round 4's derivation then found two
more outside authorities that revision 4's prose had not accounted for,
which is the census doing its job before the code exists. Four
authorities sit outside the model, each with a stated relationship to
it:

**SEAM 1 — C-lite's inherited lifecycle authorities.** The mode
controller's active spec, committing flag, terminality, deferred
departure and owner, and the navigator's attached presenter, are
mutable, are C-lite's, and are inherited whole. The model does not
absorb them and does not derive them from the slot. **No model contract
reads them**; the document's own gate is additional to theirs rather
than a replacement. Whether the two can diverge observably is T5.

**SEAM 2 — the presentation epoch.** Each surface's installed unit is
mutable and per-surface. The model's guarantee is that the PUBLICATION
is atomic; presentation atomicity is T1 and is not claimed here.

**SEAM 3 — the work scheduler's terminality, with a stated ORDERING.**
The document is a `PanelWorkScheduler`, so it inherits a mutable
shutdown flag that its own start-work path reads. Round 4's blocker 2
gave both failure directions: shut the scheduler first and loads refuse
while the model says live; publish first and work is admitted after
model retirement. **The reconciliation is an ordering invariant:** the
preterminal retired publication is published BEFORE the base shutdown,
so *scheduler shut implies retired marker already set* and never the
converse. The scheduler's refusal set is then a strict subset of the
model's, no model contract reads the flag, and a silently-dropped load
that the model believed live is unrepresentable. D4 carries the pair
that reverses the two writes.

**SEAM 4 — the announcer's retirement, which is deliberately NOT the
document's.** `CanvasAnnouncer` retires on its own instant, and C-lite's
`CanvasDocumentViewModel` consults that instant rather than its own
shutdown flag at the speak boundary. That gap is not an oversight: it
exists so the mode stack's last restoration sentence can still be heard,
which is precisely T4's subject. **The model's guarantee ends at the
funnel:** a class-INVALID sentence never reaches the announcer. Whether
a class-VALID sentence actually emits inside the retirement window is
T4's, and no model contract depends on the answer.

Physical close remains outside currency: it happens after the terminal
publication, under the lease's own lock, and participates in no
validation.

### THE FIVE CLASSES — U2's normative core

Codex's round-2 classification audit, adopted and verified against its
named sites, with two members added by auditing the auditor.
Classification is **U2**; nesting is U3.

| Class | Readable facts and effects |
|---|---|
| **DOCUMENT** | path and display name; retired state; the typed needle as intent; durable selected and marked IDs; the active surface token; the published load state and its message; presenter and mode ownership *(SEAM 1 — C-lite's, not absorbed)*; the durable focus and filter-focus requests *(SEAM 1)* |
| **LEASE** | the handle capability; the FFI lock; logical liveness; physical close; **and the FFI-failure sentence, which is the class's one effect member** |
| **POPULATION** | the full outline and table rows; warnings and preserved count; the ID, target, subpath and adjacency indexes; node text; the population sibling ordinal; anchor; neighbours; parent and children; trace path; last-activated identity |
| **UNIT** | request, needle and answer state; matched IDs; the filtered rows and their counts; the displayed-unit ordinal; resolved selection; filtered row status; unit-addressed readback and detail |
| **PRESENTATION** *(SEAM 2 — classified here, atomicity unresolved)* | the installed unit; tree roots and expansion; connection rows; grid sort, current row and current cell; realized containers; focus and return-focus; transient-panel visibility; the deferred restoration and its departure edge; the materialized needle in the filter field |

**MEMBERSHIP PRINCIPLE.** Every readable fact and every effect declares
which class it depends on, and that declaration IS its membership.
Storage location decides nothing. The question is *what does this
value's truth depend on*, and the answer is one of five named things.

**LEASE has effect members, so it has a rule** — round 4's blocker 3.
Revision 4 admitted lease-sourced effects in the state product and then
enumerated four classes at the boundary, which left the fifth
unspecified in both directions. A LEASE-sourced effect — the sentence
reporting that a call through the handle failed — carries a lease
component in its token and validates against the publication's lease. It
is therefore ADMITTED while the lease is current even after a population
successor, and refused the instant the lease is replaced, which is the
row-3 answer revision 4 omitted.

### U3 — the nesting chain

DOCUMENT ⊃ LEASE ⊃ POPULATION ⊃ UNIT, coarsest first: each is current
only while its coarser class is. PRESENTATION is not in the chain — it
is per-surface, which is why two panes can be installed at different
units without either being wrong, and why its atomicity is a separate
problem rather than a corollary of this one.

**DOCUMENT is a currency.** C-lite's C7 retires the document before the
terminal publication so the mode stack's restoration can speak, so there
is an interval where the finer classes are current and the document is
not. U12 makes that interval a PUBLISHED model state rather than a gap
between two writes. Every model boundary validates the document first.
What may legally happen inside the interval is T4; **the model claims
nothing about it and no model contract depends on the answer.**

### THE STATE PRODUCT — model currencies only

One row per coarsest failure. There is **no presentation column**:
presentation states live in the ledger where they can be enumerated by
whoever resolves T1. Round 4's major 9 was right that the old retention
column conflated two different events, so retention is now two columns —
managed collection, which is conditional on the LAST owner dropping the
reference, and native close, which is an explicit call.

| # | Coarsest failure | Reached by | Queries | Unit-scoped reads | Model effects (mutation, shell handoff, announcement, focus request) | Rows readable | Managed collection | Native close |
|---|---|---|---|---|---|---|---|---|
| 1 | none — all current | steady state | admitted | admitted | admitted, each against its own class's currency | yes | not applicable | not applicable |
| 2 | unit superseded | a filter request publishing a successor unit | ADMITTED — a filter change cannot invalidate a graph fact | refused | unit-sourced refused; population-, lease- and document-sourced admitted | yes, as a coherent past | when the LAST owner drops it, and the owner set is six: publication, operation token, **sealed result, pending effect**, in-flight operation, installed surface. The surface owner's release is T1/T3's assertion, not this cycle's | not applicable |
| 3 | population stale, lease live | a SAME-LEASE population successor — U10's sanctioned memo-enriched successor is the one such publication this design permits | refused | refused | population- and unit-sourced refused; lease- and document-sourced admitted | yes, as a coherent past | as row 2 | not applicable |
| 4 | lease dead | a reload publishing a new lease and population together | refused | refused | refused except document-sourced | yes | as row 2 | the handle closes once, under the lease's lock, after the last in-flight call returns |
| 5 | document retired | U12's preterminal retired publication | refused | refused | refused — **SEAM, T4**: the retirement's own restoration and its single sentence are the unresolved case, and the model neither admits nor describes them | yes | the terminal publication drops the SLOT's owner ONLY — round 5's major 6. Collection follows when the other five have released too; a delivered sealed result or a constructed-but-undispatched effect legitimately keeps the old graph alive past terminalization, and periphery owners are T1/T3's | after the terminal publication |

**On row 3's reachability, said plainly rather than left for a
reviewer to test.** Every reload replaces the lease, so population
staleness normally arrives together with lease death — row 4. Row 3 is
reachable only through a same-lease population successor, and the design
sanctions exactly one: U10's memo-enriched successor. Its facts must
construct the state through that sanctioned path. If an implementation
never publishes one, row 3 is unreachable by construction and the census
that says so is the evidence, rather than a battery row that quietly
cannot be built.

**Row 2 is the counter-position, confirmed sound and stable across four
rounds.** A query on a superseded unit is ADMITTED because the
non-filter queries are functions of the loaded model: children, parent,
trace, neighbours, node text, target, anchor and core's Where-am-I are
population facts, and core's sibling ordinal is a position in the model
rather than in the filtered display. The filter is the one
needle-dependent query and U9 gives it request currency.

### THE MEMORY MODEL

**Publication is a COMPARE-AND-SWAP on the one field, from any thread;
currency reads are free-threaded volatile reads of it.** There is no
thread affinity anywhere in the model.

**This is revision 7's central change, and it is a REMOVAL.** Revision 6
said publication was dispatcher-only and leaned on that to serialize
deliveries. Round 6 audited the premise and it was not merely unproven —
it was FALSE against inherited code. `PanelWorkScheduler`'s post runs
the callback INLINE on the worker when the captured context is null and
inline on the caller in synchronous mode, and the canvas async test
harness deliberately installs a null context so publishes run on the
worker. Two deliveries could therefore read one snapshot on two pool
threads, both accept, and each install a lease: one publication lost,
one handle leaked, and the one-shot property false.

The remedy on offer was a model-owned serial executor with a closed
delivery entrypoint, its own enqueue lifecycle and its own death story.
That is a large new mechanism, and this branch has twice done better by
removing one instead. **Serialization is a property the ONE FIELD can
have on its own.** Every publication is:

1. read the slot once — the **DECISION SNAPSHOT**;
2. decide, and compute an immutable successor from that snapshot alone;
3. install it with a compare-and-swap whose expected value IS the
   snapshot;
4. if the swap fails, the publication did not happen: re-read and
   re-decide from the new snapshot.

Two concurrent publishers cannot both win. The loser's swap fails
against a slot it no longer recognizes, and it re-decides — where a
delivery will now find its request consumed or superseded and refuse.
Codex's breaking arrangement runs to completion with one publication and
one closed lease, and it becomes a verification fact rather than a
premise to defend.

**Four rules keep the swap honest** — and **all four are RECLASSIFIED**,
each to the obligation named on it. Revision 7 asserted them as
contracts; round 7 showed that each was a claim about code that only
code can make good, which is the ruling in miniature. They are recorded
here as the DIRECTION, not as settled:

* **FRESH ALLOCATION — no publication object is ever installed twice**,
  so the swap cannot compare equal to a value that has come round
  again. The intent is that the publish helper is the only thing that
  allocates a successor, from a pure transform of the snapshot.
  **OBLIGATION I5:** round 7 showed that one swap site and a
  helper-owned allocation do not prohibit an interned record, a
  memoized successor, an identity return, or a shared terminal
  sentinel — the shared-Empty class this branch has met before. The code
  owes the proof.
* **THE TRANSFORM IS PURE.** It may compute an immutable successor and
  allocate identities; it may not close a handle, start a job, announce,
  or touch the shell. **OBLIGATION I4:** round 7 confirmed every
  required decision IS mathematically computable from the snapshot, so
  the direction is sound — and showed that nothing here enforces it. A
  closed command algebra or an executable purity predicate over captures
  and the call graph is what the code owes.
* **EFFECTS FOLLOW A WON SWAP**, keyed to the predecessor-successor pair
  the helper returns — starting the promoted filter job, closing the
  replaced handle, emitting the sentence. **OBLIGATION I3, the largest
  of the eight:** a swap linearizes the slot and nothing else, so
  effects are neither linearized with it nor free of the inherited UI
  affinities, and D2's dispatch check has a TOCTOU window before the
  external call. The thread story for effects is a code question.
* **PROGRESS IS LOCK-FREE.** A failed swap means another publisher
  succeeded, so the system always advances. **OBLIGATION I2:** system
  progress is not OPERATION progress. Round 7 was right that a losing
  delivery is not thereby consumed, superseded or retired, so nothing
  bounds its retries before a terminal state wins — and that teardown's
  own retirement swap can starve the same way.

**What this buys, beyond the blocker.** The rebase becomes atomic with
the publication it feeds: revision 6 read the current publication and
then assigned, so a concurrent writer could stale the rebase between the
two, and the swap closes that without a separate rule. The retirement
check becomes atomic with acceptance for the same reason — a delivery
that decided before a terminal publication cannot install over it,
because its swap fails and its re-decision sees retired.

**And it dissolves the second blocker.** With no marshal there is no
posted callback to be rejected or aborted, so a delivery's finally block
always runs on the thread that produced it, and teardown publishes on
whichever thread calls it. Round 6's dispatcher-shutdown arrangement —
a lease owned by a delivery whose callback never runs, and a U12 that
never enters — has nowhere to occur. The design needs no executor
lifecycle because it has no executor.

**Where the dispatcher still lives, and it is not here.** WPF's own
apply — installing rows and text on controls — is dispatcher-bound and
always was. That is PRESENTATION, it is T1 and T3, and the split
already owns it. The model is thread-agnostic; the periphery is where
affinity is a problem, which is one more reason the split's line is in
the right place.

| Class | Currency authority | Two-write risk |
|---|---|---|
| Document | the publication's retired marker | none — published, not flagged |
| Lease | the publication's lease reference | none — physical close is not currency |
| Population | the publication's population reference | none |
| Unit | the publication's unit reference | none |
| Presentation | *SEAM 2 — the surface's installed unit; unresolved (T1)* | not claimed |

**Selection has no window because it is not two values**: the durable
intent and the resolved selection are published together. Durability
across a reload is achieved by REBASING at acceptance rather than by
carrying a captured value — U4, and round 4's blocker 6.

**Admission is not revocation, and the check happens three times** —
before executing, after acquiring the FFI lock and immediately before
invoking FFI, and after the call returns before the result is exposed.
Killing a lease retracts no running computation; the design claims only
that a result cannot be exposed and an answer cannot be delivered.
Rounds 3 and 4 both confirmed this execution path sound: queued and
in-flight consumers cannot invoke or expose through a closed lease.

### The shape, in objects

Plain text, not backticks, for the reason §C's travel table records: a
backtick here is a CITATION and the census checks it names something the
branch has. **As each task builds a type, its name binds** — the entry
moves from plain text to a citation, and the census that used to reject
the name now has to find it. That is the only edit the freeze admits,
and it is a record operation rather than a revision: what the paragraph
SAYS does not change, only whether the name in it is real yet.

**The publication SLOT** — `CanvasPublicationSlot` — holds the one
mutable model field. Its publish returns a `CanvasPublicationOutcome`,
so a publisher never re-reads the slot to learn what its own attempt
did, and it accepts an optional `CanvasPublicationInstallObserver`,
which is obligation I5's runtime instrument. *(Bound by T1.)*

**The PUBLICATION** — `CanvasPublication` — is an immutable record: the
retired marker; the load state and its message; the lease; the
population; the unit; the active surface token; the durable intents
(selected node, marked IDs, needle); the resolved selection; the LOAD
SCHEDULE — `CanvasLoadSchedule` — and the FILTER SCHEDULE —
`CanvasFilterSchedule`. Both schedules key on `CanvasRequestIdentity`,
which is a reference and never a counter. *(Bound by T1 for the
document-class members and the schedules; the three finer classes bind
with their types in T2.)* Collections enter it only through
`CanvasModelCopy`, which is obligation I7's construction half.

**The LEASE** — `CanvasHandleLease` — owns the handle capability, the
FFI lock and the close-once record, with no mutable liveness flag.
**The POPULATION** — `CanvasPopulation` — owns the full rows, the
indexes, the eager adjacency memo and the query methods. **The UNIT** —
`CanvasProjectionUnit` — owns the request, needle, answer state,
matched IDs, filtered projections, resolved selection and
unit-addressed transients. The ownership transfer between the first of
those and the publication is `CanvasLeaseTransfer`. *(Bound by T2.)*

**The OPERATION TOKEN** is opaque and names the lease, the population
and the initiating unit of one operation. **The SEALED RESULT** is what
every query returns: a payload together with the token that minted it,
with no accessor yielding the payload without the token. **PAYLOAD
TYPES** declare their own required source class as a property of the
type. Together these are U14. *(The token's SURFACE component — which
pane initiated the operation — and the forgeability of that component
are T2.)*

**The DOCUMENT** — `CanvasDocumentViewModel` — keeps the mutation
funnel, the loader, the publisher, and the C-lite collaborators it does
not own.

### Contracts

**U1 — One mutable model field**, with the four outside authorities
named above. A model state needing a second mutable field is a finding.

**U2 — Five classes, one membership principle**, enforced by U13's
classification arm, whose population comes from the types rather than
from the table.

**U3 — The chain nests; presentation is per-surface and outside it.**

**U4 — Reload replaces the LEASE; the load is an OPERATION with
currency, one-shot ownership, and a REBASE at acceptance.** C-lite's
reload closes and reopens the handle, so a reload necessarily creates a
new lease.

*Currency.* The publication carries a LOAD SCHEDULE holding the identity
of the latest load request and its delivery state, which is pending or
consumed. A load result is accepted only if its request is still the
latest, its delivery state is still pending, and the document is live.

*One-shot ownership, WITHOUT a second mutable field* — round 4's blocker
5 as corrected by round 5's blocker 3 and round 6's blocker 1. Revision
5 latched delivery with an atomic claim on the result, and round 5 was
right that this was a second mutable authority. Revision 6 replaced it
with the publication transition and rested serialization on a dispatcher
premise that round 6 showed to be false. **The latch is the publication
itself, and the SWAP is what serializes it** — no thread, no executor,
no claim. Delivery runs on whatever thread produced it. It reads the
slot ONCE as its decision snapshot and decides:

* request not latest, or delivery state not pending, or document retired
  ⇒ REFUSE;
* otherwise ⇒ ACCEPT, and publish the pending-to-consumed transition in
  the same swap that installs the lease and population.

Spending and installing are therefore the same event, and a second
delivery of an already-accepted request reads *consumed* and refuses.
**If the swap fails, nothing happened** — the delivery re-reads and
re-decides, and by then its request is consumed, superseded, or the
document is retired, so it refuses. Two concurrent deliveries on two
pool threads, which is round 6's breaking arrangement and a shipped test
configuration, therefore produce exactly one publication and exactly one
closed lease. That is the whole latch, and it is a derived read of the
one field in exactly U5's style.

*Which makes the refusal branch's close a DERIVED question too*, and
this is what stops the double-close revision 4 worried about: **a
refusing delivery closes the lease it opened unless the live publication
names that lease.** Reference comparison against the slot, not a flag.
Physical close is IDEMPOTENT under the lease's own lock — recorded once
on the lease, never consulted as currency — so even a pathological
second close is a no-op rather than a double free.

*The release obligation, at every point of the window* — round 5's
blocker 4, with round 6's minor 6 correcting what the table claimed. A
delivery performs **one decision read PER ATTEMPT plus one final
ownership read** — round 7's minor, correcting revision 6's vocabulary,
since every failed swap costs another decision read by construction —
and nothing else reads the slot; the obligation is discharged in a
finally block wrapping the whole delivery.

| Point in the window | Who owns the lease | What the finally does |
|---|---|---|
| entered, FRESH delivery | the result alone | closes — the slot does not name it |
| entered, REDELIVERY of an already-accepted result | the PUBLICATION already owns and names it — round 6's minor 6, which revision 6's table wrongly described as exclusive result ownership | does nothing; the guard's live read finds the lease in the slot |
| refusal decided | the result, unless this is the redelivery row | closes in the first case, does nothing in the second — one guard, two truths |
| accepted, during rebase, reseed and successor construction | the result | closes if anything throws — the slot still does not name it |
| the swap itself | the result until the swap WINS | a compare-and-swap either took or did not; the guard's live read answers correctly either way, and a LOST swap leaves the result owning its lease so the re-decision or the finally closes it |
| after the swap wins | the PUBLICATION | does nothing — the slot names it |

One guard covers all six, because the guard is not a state machine: it
is "close unless the live publication names this lease", evaluated once
in the finally, against the slot as it is at that moment.

**OBLIGATION I1 — the sharpest of the eight, and the guard's own
defect.** Round 7 showed that this guard is an OBSERVATION, not an
atomic ownership transfer: it was sound while delivery was serialized,
and the swap deliberately removed that serialization. A faulting
delivery can read "no publication names L", a concurrent delivery can
then win the acceptance swap and publish L, and the first can close a
handle the live publication now names — which the close-once record does
not prevent, because it is the FIRST close that is wrong. The row above
that says "the slot still does not name it" is therefore the claim the
code must make true, by making acceptance impossible before cleanup or
by synchronizing the two. It is not true of the guard as written.

*The REBASE at acceptance* — round 4's blocker 6, which is round 2's
blocker 1 resurfacing through the absorbs-nothing decision. A load
result owns ONLY lease and population material: rows, indexes, memo. It
carries NO document-class state at all. At acceptance the publisher
reads its DECISION SNAPSHOT once (U11) and carries forward
**every DOCUMENT-class member of that snapshot**, which is the rule
rather than a list, because round 5's blocker 5 found the list short.
Two kinds:

* **REBASED** — resolved against the new population: the selected ID
  into a resolved selection, the marked IDs into the subset that exists,
  the needle into a seeded filter machine.
* **CARRIED UNCHANGED** — document-class and population-independent, so
  there is nothing to resolve: the path and display name, the retired
  marker, **the ACTIVE SURFACE token**, and the load state the
  acceptance itself writes.

The active surface is the one round 5 named. It is DOCUMENT-class in
U2's table and absorbed wholesale by the reconciliation, so a load that
began while the surface was Outline must not restore Outline over a user
or retarget switch to Table that happened while it ran. It carries; it
never resets. The census that keeps this honest is U13's classification
manifest: every DOCUMENT-class member must appear in the carry-forward
contract as rebased or carried, and a member in neither fails the arm —
which is what makes "every document-class member" a derivation rather
than another list that can go short.

A load that started while the selection was A therefore cannot roll a
later selection of B backwards, and the same barrier covers marks, the
needle and the surface.

*Transition.* Publish the terminal state for the old lease and
population; close the old handle under its lock; construct the new lease
and population off-thread; rebase, carry forward, and install in ONE
SWAP. No live publication ever names a closed handle.

**U5 — Currency is DERIVED, never carried.** A reference comparison
against what the slot holds, per class. Physical close is a separate,
later, non-currency event.

**U6 — Queries validate document, lease and population — three times.**
Not unit currency: row 2's ruling.

**U7 — Effects are validated against the source class their PAYLOAD
declares.** Revision 3 said an effect's source is the initiating unit
"full stop", which contradicted its own row 2. Revision 4 fixed the rule
and then let the caller state the class, which round 4's blocker 1
correctly called a caller-controlled downgrade. The class is now a
property of the payload TYPE, not a caller's declaration, and U14 makes
it underivable by any other route. The boundary:

* a UNIT-sourced payload validates the token's unit component;
* a POPULATION-sourced payload validates the population component;
* a LEASE-sourced payload validates the lease component;
* a DOCUMENT-sourced payload validates the retired marker only;
* a PRESENTATION-addressed effect is **SEAM, T2** — the model does not
  validate it and does not claim to.

**Count-bearing sentences are classified by the facts in their PAYLOAD,
and different payloads are different TYPES.** The filter-cleared
sentence reads its count from the current population, so its type is
population-sourced; load-state sentences carry no row facts and their
type is document-sourced. This is why Clear Filter can begin on unit A,
publish successor B in the same population, and still be allowed to
speak its own confirmation — and it is also round 4's major 14, because
one class per MEMBER cannot express it and one class per PAYLOAD TYPE
can.

**U8 — The model effect universe.** Document mutation; selection; detail
and panel state; shell navigation and launch; persistence; mode commit
and cancel closures; announcement; focus REQUEST. Every row-derived
route consumes a sealed result, and the boundary validates the class
that result's payload type declares.

**Focus DELIVERY — the direct row-focus call after a query is consumed —
is SEAM, T6.** A request gate does not govern a direct call, and the
model does not pretend otherwise: it is listed in the ledger as an
unresolved effect class rather than covered by implication, and U13
fails if a model contract consumes it.

**U9 — The filter schedule is a TOTAL state machine, published.** Round
4's blocker 4 was right that revision 4 named three transitions and left
four states unreachable or undecided. The publication carries a FILTER
SCHEDULE — the RUNNING request and the QUEUED-LATEST request, together,
in the same immutable value — and the machine is keyed by the current
population as well, because a reload is one of its events.

| State | Keystroke K | Completion of the RUNNING job | Completion of a NON-running job | Load acceptance (new population) | Terminal publication |
|---|---|---|---|---|---|
| *(none, none)* | publish *(K, none)*; start K | unreachable | refuse the delivery; publish nothing | publish *(none, none)*, or *(K', none)* and start K' when the rebased needle is non-empty | publish *(none, none)* |
| *(R, none)* | publish *(R, K)*; do not start K | publish R's ANSWER and *(none, none)* | refuse the delivery; publish nothing | as above; R's later completion becomes a non-running completion and is refused | as above; R's completion is refused at delivery and teardown does not wait |
| *(R, Q)* | publish *(R, K)*; Q is dropped before it ever started | **DISCARD R's answer**, publish *(Q, none)*, then start Q | refuse the delivery; publish nothing | as above; both R and Q are discarded and the rebased needle seeds the new machine | as above; Q is dropped |

**Every cell is a compare-and-swap**, decided from one snapshot and
retried on failure, which is what makes the machine total across threads
as well as across events. A filter completion is a writer like any
other — round 6's audit noted U9 named completion events without a
serialization boundary, and the answer is the same one the whole model
now uses rather than a boundary of its own. Starting the promoted job is
an EFFECT and therefore follows a won swap, never an attempt.

Three consequences worth stating, because revision 4 got each of them
wrong or left it silent:

*Finishing with a queue is not the same as finishing empty.* Publishing
R1's answer while R3 is queued would rest R1's rows under R3's needle,
which is exactly the arrangement U9 exists to prevent. The queued case
discards and promotes; the empty case publishes.

*A population replacement retires BOTH schedule entries and reseeds.*
Revision 4 said nothing, which left a stale running request occupying
the slot forever with no callback able to clear it. The new population's
publication is the reseed, and it comes from the rebased needle rather
than from the dead machine.

*A burst of ten keystrokes pays for at most two matches*: R1, which was
already in flight, and the last one standing. The running job also
revalidates after acquiring the FFI lock, so even the first is abandoned
before the FFI call once it has been superseded.

**U10 — The eager adjacency memo lives in the POPULATION**, built once
from the rows. Confirmed correctly placed by rounds 3 and 4. The only
sanctioned alternative, if the eager cost proves unacceptable, is an
immutable memo-enriched population successor published with completion
validation — never an in-place fill, never a shared sentinel. That
successor is also row 3's one reachable transition.

**U11 — One SWAP, one decision snapshot, and compound consumers hold
that snapshot.** Round 6's blocker 1. Every writer goes through the one
publish helper: read once, decide from that snapshot alone, swap with
the snapshot as the expected value, re-decide on failure. Any consumer
reading more than one fact for one purpose acquires the publication
once; convenience getters that silently re-read the slot are forbidden.
U4's rebase is the load path's instance of this rule, and the swap is
what makes the rebase atomic with the publication it produces.

**The writer set is CLOSED by construction, not by audit.** The slot is
private, the publish helper contains the only compare-and-swap in the
model, and the helper allocates the successor itself from a pure
transform — so there is no API through which a caller can install a
record it built, or a record that was ever installed before. U13's
publication-writer arm asserts exactly that: one swap site, no other
write to the field, and every publishing path reaching the helper.
Revision 6 would have needed a census proving every writer was on some
executor; this needs a census proving there is one writer, which is a
question a compiler can answer.

**Four writer families**, enumerated so the arm has something to be
complete against: load completion, filter completion, model-effect
application, and UI-originated intent publication. All four call the
helper; none of them marshals, and none of them may assume a thread.

**U12 — Teardown's model SEQUENCE, implementable with T4 unresolved.**
Round 4's blocker 7 was right that revision 4 named only the terminal
publication, which left the preterminal state unpublished and let an
exception in the unresolved region prevent terminalization entirely. The
model owns four steps and the seam is one of them:

1. **Publish the PRETERMINAL RETIRED publication** — the retired marker
   set, the old lease, population, unit and intents RETAINED. This is
   state-product row 5, and publishing it is what makes the retired
   interval a model state rather than a gap between two writes. It also
   preserves the old unit and lease that T4's acceptance criterion
   assumes are still there.
2. **Publish before the base shutdown** — SEAM 3's ordering invariant,
   so the scheduler's refusal can never precede the model's.
3. **Cross the T4 SEAM.** Whatever the periphery does inside that window
   is T4's; the model authorizes nothing and refuses everything by row
   5.
4. **Publish the TERMINAL publication unconditionally in a finally
   block**, clearing every model currency at once — guaranteed even if
   the seam throws or reenters. Physical close follows under the lease's
   lock after the last in-flight call returns.

**The finally block encloses steps 1 through 3.** Round 5's walk found
the sequence coherent on exactly that reading, so it is stated rather
than left to be inferred: a fault in step 1 or 2 must still reach step
4, not only a fault inside the seam. Readers arriving after step 1 see
one retired snapshot and every model boundary refuses; the scheduler's
shutdown cannot precede the model's retirement; and a T4 fault or
reentrancy still terminalizes.

**Teardown runs on the calling thread, and needs no dispatcher** —
round 6's blocker 2, which the swap dissolves rather than answers. Both
publications are compare-and-swaps, so they retry until they win.
**OBLIGATIONS I2 and I3 land here together:** revision 7 argued the
retirement retry terminates because retirement has no refusal branch,
and round 7 was right that an unconditional swap can still starve under
sustained contention; and crossing the mode stack's shutdown, retiring
the announcer and clearing bound requests are not established as
pool-thread-safe, whatever the slot write's affinity.

Because nothing is posted anywhere, there is no queued teardown that a
stopping dispatcher can drop, and no delivery callback that can be
rejected while still owning a lease. Round 6's
arrangement — a worker opens lease L, posts its delivery, and shutdown
begins before the callback runs, so nobody's finally ever executes — has
no posting step to build on.

**A delivery racing teardown is safe in both orders, by the same
mechanism.** If the delivery's swap wins first it installs its lease,
and the terminal publication then replaces it, so D1's lease row closes
the handle after the last in-flight call. If teardown's swap wins first,
the delivery's swap fails, its re-decision reads retired, it refuses,
and its finally closes the lease it opened because the live publication
does not name it. There is no third order, because there is one field
and one swap.

This defines the model's refusal and release without authorizing T4's
restoration or its sentence, which is the whole of what the split asks
for.

**U13 — NINE census arms, each from an authority independent of what it
checks, each EXECUTABLE, and PARTITIONED by the split.** Round 4's
blocker 8 was right that a single gate over every type in the canvas
tree could pass only by prematurely resolving the periphery or by
falsely marking it source-free. (Round 5's minor 8: revision 5's heading
still said four while its body named six. The count is stated once, in
the contract's own first line, and the verification plan and D3 quote it
rather than recounting.)

*The owned-symbol closure.* A Roslyn-derived set: every type declared in
the canvas source tree, plus the named collaborators it reaches —
`CanvasAnnouncer`, `PanelWorkScheduler`, and the live preferences seam.
Inherited members are IN only when the base is owned or the base is a
typed framework seam this branch declares; the rest of the framework
surface is out by construction rather than by exception.

*The PARTITION.* The closure splits by the class its member carries.
Model members and model-addressed effect sites are enforced now.
Presentation members are classification-only, or are machine-marked
unresolved against a NAMED obligation row. Two assertions make the
marker a wall rather than an escape hatch: **the census FAILS if a model
contract consumes a member marked unresolved**, which is the split's own
rule executed rather than promised; and every marker must name an
obligation row that is still open, so retiring a T row without replacing
its markers fails closed. The later periphery census replaces them.

*The classification manifest, in TWO tables* — round 4's major 14.
FACTS map member to class, or to source-free with a reason. EFFECT SITES
map each boundary to the sealed payload type it accepts, because a
member like the announcement funnel legitimately carries document-,
population-, lease- and unit-sourced sentences at different call sites,
and one class per member would weaken some and falsely reject others.
The census asserts both directions on both tables: every discovered
member or site appears, and no entry names something that is gone. It
also asserts U4's carry-forward completeness: every DOCUMENT-class entry
in the FACTS table appears in the acceptance contract as rebased or
carried unchanged.

*The PAYLOAD-CLASS arm* — round 5's blocker 1, and the converse U14 was
missing. Restricting construction proves who made a payload; it does not
prove that the class the payload declares is at least as fine as every
fact inside it. This arm derives each payload type's MINIMUM class from
its own fields and fails when the declared class is coarser. See U14,
which states the field-level rule the arm executes.

*The SOURCE-TAG arm* — round 6's blocker 3, which is the arm above's own
anchor. It enumerates every construction site of every class-tagged
source wrapper and fails if a site sits outside the class it tags, so a
population value cannot be born document-tagged. Its population is
deliberately WIDER than the owned-symbol closure, because the FFI-return
adaptation sites are where a raw value first acquires a class and
generated FFI internals are outside that closure — an arm that inherited
the closure would miss the one boundary that matters. It also asserts
that no transformation coarsens a tag, and that no unwrap-to-raw
accessor exists.

*The PUBLICATION-WRITER arm* — round 6's blocker 1. The slot field is
private; exactly one compare-and-swap site exists in the model; no other
statement writes the field; every publishing path reaches the helper;
and the helper allocates its successor from a pure transform rather than
accepting a caller-built record. Revision 6 would have owed a census
proving every writer ran on some executor, which is a runtime property a
gate can only sample; this is a compile-time question about one field,
which is why the removal was worth making.

**OBLIGATIONS I5 and I8 both live in this arm**, and together they are
why it is a code deliverable rather than a paragraph. I8: "no other
statement writes the field" describes ordinary source assignment and
says nothing about reflective field setting, serializer hydration,
unsafe accessors, generated accessors or init-only bypasses — any of
which installs a publication without passing the swap while this arm
stays green, so the arm must either close those mechanisms across the
generated surface or narrow its own claim and name a second boundary.
I5: nothing above prohibits the helper from RETURNING a cached,
interned, or sentinel record, which is the other half of freshness. The
arm as described is a real Roslyn analyzer's worth of work and is
scoped as such.

*The AUTHORITY census* — round 4's blocker 2, with round 5's blocker 2
correcting the derivation itself. Revision 5 derived authorities from
WRITE SITES — "every field written outside its constructor" — and round
5's probe was right that this observes reference assignment rather than
mutable state. It misses a readonly field holding a mutable object, and
the shipped code has three shapes of exactly that: the marked-ID set and
the scheduler's pending-work set are both readonly and both mutated all
day, and auto-properties like the last-activated node and the three
shell-handoff delegates mutate with no source-level field write at all.
A predicate that passes over those is not a closure.

**The derivation is now from TYPES, and it separates identity from
contents.** Round 6's blocker 4 then corrected what "immutable type"
may mean: revision 6's predicate was STRUCTURAL, so a field typed as a
read-only interface counted as immutable while the object behind it was
a caller's list the caller kept mutating. A read-only surface is not
evidence about the runtime object or its aliases.

**Deep immutability is therefore NOMINAL and CLOSED-WORLD.** Unknown
interfaces, abstract types, arrays, delegates and externally implemented
collections default to MUTABLE. Only two things are immutable: an
explicitly trusted framework set — primitives, string, enums, and the
immutable collection types — and owned SEALED types recursively proven,
with immutable generic arguments. Owned immutable types carry a
construction constraint as well: one that accepts a collection must COPY
it into an immutable collection rather than alias the caller's, so the
population's rows and the publication's marked IDs are immutable
collections and not read-only views over somebody's array.

**OBLIGATION I7.** Round 7 was right that this is asserted and not
established: a nominally trusted immutable collection can be built over
a caller-retained array through the marshalling API, giving a trusted
type with a live external alias — after which a published snapshot can
mutate in place and take rebase repeatability and swap decision
stability with it. The code owes a closed whitelist of copying
operations, transitive constructor and factory analysis, and the planted
retained-array alias as the battery's sixth shape. Then:

* a field or auto-property whose type is NOT deeply immutable
  contributes a **CONTENTS** authority, readonly or not;
* a field or auto-property that can be REASSIGNED contributes an
  **IDENTITY** authority, whatever its type;
* events contribute a contents authority — a subscriber list is mutable
  state a consumer can observe;
* mutable statics contribute one regardless of the owning instance;
* ref and interlocked writes, property setters, and mutating calls on a
  reachable collection are all mutation for this purpose.

Every authority the derivation finds — identity and contents counted
separately — must appear in the reconciliation table below marked
absorbed, seam, source-free, or removed. **The one exemption is now
narrower and applies to IDENTITY only:** a reference written once from
null and never changed again is not an identity authority, and must be
declared as write-once rather than merely look it. Its CONTENTS still
count, which is why the memoized command objects now carry a row of
their own rather than disappearing through the exemption they used to
take whole.

*Capability.* Every call site reaching the session's canvas FFI surface,
derived from the session type, must be inside the population or the
loader.

*Effects.* Derived from independent call sites — every shell handoff,
state-writing delegate, event handler, announcer call, focus call and
mutation method — each of which must consume a sealed result, or appear
in the manifest as source-free with its reason, or carry an unresolved
marker naming its obligation.

Each arm carries a discovery floor and fails closed.

**U14 — Model provenance is MINTED, SEALED, PROVABLE, and underivable by
a caller.** Round 4's blocker 1 and round 5's blocker 1. Four rules, and
each is unrepresentable rather than forbidden:

*Minting.* Only the operation funnel constructs an operation token, from
the publication it read at the start of the operation. The token type
has no accessible constructor outside that funnel, and U13's effects arm
asserts exactly one construction site.

*Carriage.* Every query returns a SEALED RESULT — the payload with its
token — and there is no accessor that yields the payload alone. A
consumer cannot hold a raw stale value at all, so it cannot pair one
with a newer token.

*Derivation.* The required source class is a property of the PAYLOAD
TYPE and is read from the type at the boundary. A caller cannot declare
a weaker class for population- or unit-derived content, because a caller
declares nothing. Payload types are constructed only by the same funnel
that mints the token, which is the second construction-site assertion.

*PROVABILITY — the converse round 5's blocker 1 found missing.* The
three rules above prove WHO made a payload; none of them proves the
class it declares is honest about what is INSIDE it. The funnel's own
legitimate site could build a document-classed payload holding a count
read from the population, and it would be sealed, correctly tokened, and
validated against document currency alone — emitting a stale count after
a population replacement. The existing mutation cannot catch it, because
it moves a boundary parameter and this defect is in the type.

**The rule is at the FIELD level: a payload type's declared class must
be at least as FINE as every fact it holds.** Every value a payload can
carry arrives through a class-tagged source type — a document fact, a
lease fact, a population fact, a unit fact — or is a literal that
depends on nothing. The payload's MINIMUM class is the join of its
fields' classes down the U3 chain, so a record holding one population
fact has minimum POPULATION however few other fields it has, and a
record of literals has no minimum at all. **Declaring a class coarser
than the minimum is a census failure.**

*ANCHORING the join — round 6's blocker 3, and the same move made one
level deeper.* A join over tags proves nothing if a tag can be chosen.
Revision 6 left source wrappers mintable, so the funnel could read a row
count, compute an int, and place it in a document-tagged wrapper: the
payload's join would then be DOCUMENT, the arm would pass, and the stale
count would emit on document currency. The defect was not the join. It
was that revision 6 applied its own rule — *the class comes from what
produced the value, not from what the caller says* — at the payload
level and stopped there.

**So a source tag is not minted; it is what the class-owning object
RETURNS.** There is no constructor anywhere that takes a value and a
class. The population's members return population-tagged values, the
unit's return unit-tagged, the lease's return lease-tagged, the
document's return document-tagged, and the loader's adaptation of an FFI
return produces lease- or population-tagged values because that is what
it read. Provenance is structural: a wrapper's class is decided by which
object could have constructed it.

**Transformations PRESERVE or REFINE, never coarsen.** Mapping a
population-tagged value yields a population-tagged value; combining two
yields the finer of the two; refining from population to unit is
allowed, because a unit-classed effect is validated more strictly, and
going the other way is not expressible. There is no unwrap-to-raw
operator: a tagged value's only exit is a boundary that validates it,
which is the same sealed-carriage rule U14 already applies to results,
applied to the values inside them.

U13 executes both halves. The payload-class arm derives each payload
record's minimum from its field types and fails on a weaker declaration.
The SOURCE-TAG arm enumerates every construction site of every wrapper —
including the FFI-return adaptation sites the owned-symbol closure would
otherwise miss, since generated FFI internals are outside it — and fails
if any site sits outside the class it tags.

**OBLIGATION I6.** Round 7 found the remaining gap and it is a real one:
lexical ownership of construction does not prove SEMANTIC provenance of
the value inside. A preserving transformation over a document-tagged
seed can run inside a population-aware closure, compute a row count, and
emit a still-document-tagged value with no constructor taking
value-plus-class, no unwrap, and no syntactic coarsening — and the join
above it then honestly validates a dishonest tag. Closing this means
transformations that take every dependency as a TAGGED input and derive
the join, with ambient reads and arbitrary captures forbidden and
mutation-tested. That is a call-graph property, which is the same thing
I4 needs, and the two should be built once.

What this deliberately does NOT cover, and hands to T2: the token's
SURFACE component, its forgeability, and the validation of
presentation-addressed effects. The model's guarantee is about DATA
provenance and it ends there.

### The design's own contracts

**D1 — Every MODEL lifecycle states logical terminality, cancellation
and physical release SEPARATELY, and release is stated in terms of ALL
owners.** Nineteen rows. Round 4 confirmed fifteen exact; round 5 added
four by finding two things fifteen could not say — a load result has
three OUTCOMES and one unconditional row cannot describe them (major 7),
and sealed results and pending effects are owners the ownership graph
never named (major 6).

| Lifecycle | Logical terminality | Cancellation / refusal | Physical release — by ALL owners |
|---|---|---|---|
| Document | the retired marker | every model boundary refuses | when the registry drops it |
| Lease | replaced by a terminal or successor publication | admission and revalidation refuse | handle closed ONCE under its lock after the last in-flight call, recorded on the lease |
| Population | replaced by a new population publication | queries and population-sourced effects refuse | when ALL SEVEN owners drop it: publication, operation token, sealed result, pending effect, in-flight operation, installed surface, and — round 6's major 5 — an **UNDELIVERED LOAD RESULT**, which owns a population no publication has yet named and is neither in-flight nor a sealed result |
| Unit | replaced by a successor publication | unit-scoped reads and unit-sourced effects refuse | the same seven, less the load result, which never holds a unit |
| Publication | replaced by the next winning swap | none — a value; a LOST attempt's successor is terminal on construction and is simply dropped | the slot holds one; predecessors live until every operation, result, effect and surface holding them drops, so "the slot retains exactly one" is NOT release |
| Load request | superseded in the load schedule, or marked consumed by an accepting publication | a non-latest or non-pending delivery refuses | with the publication carrying the schedule |
| Load result — ACCEPTED | the swap that installs its lease also publishes consumed | not applicable; this is the accepted branch | LEASE ownership transfers to the publication and the result releases nothing; its close observation stays at zero, and a redelivery reads consumed, refuses, and does NOT close because the live publication names that lease. Its POPULATION transfers in the same swap |
| Load result — REFUSED | the acceptance check rejects it, on first decision or after a lost swap | stale, superseded, retired or non-pending | **closes its own lease exactly once**, guarded by "unless the live publication names it". Its POPULATION was never published and is dropped with the result — managed memory, collected when the result is |
| Load result — FAULT before transfer | the delivery throws between entry and a won swap | no publication ever names the lease | the delivery's finally closes it under the same guard — round 5's blocker 4. The population is dropped with the result, which is why a faulted delivery leaks nothing managed either |
| Filter request | superseded in the filter schedule, or discarded on promotion | a non-running delivery refuses | an ANSWERED successor deliberately RETAINS its request, so release is with that unit's publication — not on delivery |
| Filter job | none — not cancellable | revalidated before FFI, refused at delivery | on completion; teardown does not wait |
| Query operation | none — not cancellable | refused at completion validation | when the call returns |
| Operation token | superseded with its data components | effect construction refuses | NOT "with the operation" — round 5's major 6. A token outlives the call that minted it, so release is when the last sealed result and pending effect holding it drops |
| Sealed result | superseded with the token it carries | every boundary that consumes it refuses | when its consumer drops it; until then it retains its token, and through the token the unit and population it was read from |
| Pending effect — constructed, not yet dispatched | superseded with its result | refused again at DISPATCH, EMIT, REQUEST or HANDOFF | when it dispatches or is dropped; a queued effect legitimately keeps the old graph alive past terminalization, which is why row 5 says the terminal publication drops the SLOT's owner only |
| Adjacency memo | none — eager | not applicable | with the population |
| Pending announcement | ANY of four: emitted; superseded or coalesced away; refused at the announce boundary; or the announcer's own retirement *(SEAM 4 — C-lite's instant, T4-ordered)* | refused at the boundary | when the pending owner drops it |
| Durable focus requests | *SEAM 1 — C-lite's, inherited; terminality and release are C-lite's contracts* | — | — |
| Mode ownership and controller terminality | *SEAM 1 — C-lite's, inherited* | — | — |

Per-surface restoration holds and the installed unit are presentation
and appear in the ledger, not here.

**D2 — Every boundary is at EMIT or READ time with its evaluation-order
argument.** C-lite's announce boundary is the precedent: a check written
before a call cannot see a retirement the call's own arguments cause.
The moments: query validation at CALL, at LOCK and at COMPLETION; effect
validation at CONSTRUCTION from the sealed result and again at DISPATCH,
EMIT, REQUEST and HANDOFF; filter and load delivery at DELIVERY, **on
the PRODUCING THREAD** — round 7's minor, sweeping the last of revision
6's dispatcher vocabulary — against that attempt's decision read; the
mode stack at its own transitions. **OBLIGATION I3** owns the hole round
7 found in this contract: a check at DISPATCH does not cover a
publication landing between the check and the external call.

**D3 — Every population is DERIVED from an authority independent of what
it checks.** U13's arms are its own count, quoted from that contract
rather than recounted here. "Derived from the thing being checked" is
forbidden, having been tried twice — and round 5's blocker 2 adds the
third failure mode: a derivation can be independent and still be
UNSOUND, so an arm owes an argument that its predicate observes the
thing it claims to.

**D4 — Every unreachability claim ships a pair whose mutation ACTUALLY
ENABLES the state.** Round 4 found three whose consequences their
mutations could not return; each was rewritten to claim what its
mutation really enables, and round 5 confirmed the resulting
twenty-seven. Round 5's five blockers took it to thirty-two, which round
6 confirmed. Thirty-eight now, six of them for the swap and the two
anchored converses.

| Claim | Enable it by | The consequence that returns |
|---|---|---|
| A stale-population query cannot answer | making reload keep its lease AND validating lease only — a same-lease population successor, which is the arrangement lease-only validation actually admits | a reload's rows read against the old graph |
| A retired document cannot be queried | removing ALL THREE document validations — admission, lock-time and completion — not the first alone | an off-dispatcher completion inside the retirement window |
| No answer or model effect is exposed after teardown | removing the document, request AND population delivery guards together, so a queued answer has no remaining authority to refuse it | an answer delivered after release |
| A dead lease cannot have a current unit | publishing a successor naming the OLD lease with the new population | a query answered through a closed handle |
| A lease-sourced sentence cannot outlive its lease | validating it against the population instead of the lease | an FFI-failure sentence spoken for a handle that has been replaced |
| A query result cannot outlive its currency | removing the completion validation | an answer from A exposed after B is current |
| A result cannot become another class's effect | letting an effect constructor read the slot instead of its sealed result | an effect authorized by a currency it did not come from |
| A caller cannot weaken an effect's class | reading the class from a boundary parameter instead of from the payload type | a population-derived count admitted on document currency |
| A payload type cannot under-describe its contents | declaring a payload that holds a population count as document-classed, which the payload-class arm must reject | a stale count emitted after a population replacement, on document currency alone |
| An authority cannot hide in a readonly field | deriving authorities from write sites instead of from types | the marked-ID set and the scheduler's pending-work set escape the reconciliation while the census passes |
| A consumer cannot hold a raw result | adding an accessor that yields the payload without its token | a stale row paired with a current token |
| A filter answer cannot land on a stale needle | removing the request comparison at delivery | rows for one needle resting under another |
| A filter answer cannot strand a sibling job | requiring base-unit currency instead of population | the newer job dropped, the field never catching up |
| A completing job cannot publish under a newer needle | publishing the finishing job's answer even when the schedule holds a queued request | R1's rows resting under R3's needle |
| A burst cannot pay for obsolete work | removing the revalidation after the lock | the superseded running request performs one obsolete match under the FFI lock |
| A burst cannot pay one match per keystroke | removing the queued-latest coalescing, so every keystroke starts | ten matches for ten keystrokes |
| A third keystroke cannot start a concurrent match | dropping the RUNNING component from the filter schedule, so the machine forgets the work it already has | R3 starts while R1 is still in flight and the coalescing is lost |
| A reload cannot strand the filter machine | preserving the schedule across load acceptance instead of retiring both entries and reseeding | a dead running request occupying the slot with no callback able to clear it |
| A stale load cannot overwrite a newer one | removing the load schedule comparison | L1 replacing L2's publication |
| An unaccepted lease cannot leak | making a refused load delivery return without closing | a handle open with no publication naming it |
| Two publishers cannot both win | replacing the compare-and-swap with a plain assignment, which is exactly revision 6's design | round 6's own arrangement: two deliveries on pool threads under a null context both accept, one publication is lost and its lease leaks |
| A publication cannot be installed twice | letting the publish helper accept a caller-built successor instead of allocating it from a pure transform | a record re-installed, so a swap compares equal to a value that came round again |
| A losing attempt cannot have consequences | letting the transform start the promoted filter job instead of doing it after the swap wins | a job started by an attempt that published nothing |
| A transformation cannot coarsen a class | allowing a population-tagged value to be re-wrapped as document-tagged downstream | the laundering blocker 3 named, one step further along |
| A source tag cannot be chosen | giving the source wrapper a constructor that takes a value and a class | a row count born document-tagged, and every join above it honest about a lie |
| A read-only interface is not evidence | making deep immutability structural again, so a read-only surface counts as immutable | a caller-retained list behind a read-only field, mutating a contents authority the census never found |
| A delivery cannot run twice | removing the pending-to-consumed transition from the accepting publication, so a repeat delivery still reads pending | the same lease installed twice, and the first one leaked |
| An accepted lease cannot be closed by a repeat delivery | dropping the "unless the live publication names it" guard from the refusal branch | a live, published handle closed underneath the model |
| A fault before transfer cannot leak the lease | narrowing the delivery's finally to the refusal branch only | a rebase or publication-preparation throw leaving a handle nobody owns |
| A load cannot roll a durable intent backwards | letting the load result carry the intents it captured at start instead of rebasing at acceptance | a selection of B replaced by the A the load began with |
| A load cannot reset the active surface | dropping the active surface from acceptance's carry-forward set, leaving the successor to default it | a mid-load switch to Table rolled back to Outline while the persisted token says Table |
| The scheduler cannot refuse before the model does | reversing SEAM 3's two writes, shutting the scheduler before publishing retirement | a load the model believes live, silently dropped |
| A stale row cannot mutate, announce, request focus or hand off to the shell | dropping the sealed result from each boundary in turn | one per boundary, four mutations |
| A mode closure cannot run unsourced | dropping the token from commit and cancel | a retained closure acting on a live document |
| A compound read cannot straddle publications | letting a getter re-read the slot | a mixed rows-of-B, selection-of-C composition |
| The population cannot mutate | filling the adjacency memo lazily in place | two consumers racing one dictionary |
| Currency cannot be observed inconsistently | replacing a derived read with a second field | either interleaving of the pair |
| Selection intent and resolution cannot disagree | writing the intent outside the publication | a reentrant observer seeing one with the other |

**Release pairs, and what each KIND of release can actually be proven
with** — round 4's major 10. Managed collection and native release are
different events and cannot share an instrument.

* **Managed owners** are proven with weak references: a mutation retains
  the reference — a static list, a captured closure, an un-detached
  handler — and the weak-reference fact shows the leak.
* **Native handle release** is proven by the CLOSE OBSERVATION on the
  lease's own close-once record, asserted exactly once. A weak reference
  proves nothing here in either direction: dropping a wrapper without
  closing clears the reference while the handle leaks, and a correctly
  closed lease retained in a static list reports retention although the
  physical resource is gone.
* The filter-request row's pair reflects that an answered successor
  retains it, so the fact asserts collection with the PUBLICATION rather
  than on delivery.

**D5 — Every list either derives from an authority or says "among
others".** U2's table is guarded by U13's classification manifest; the
state product by U13 plus D4; D1 by its own release pairs; D4 by the
verification plan, which owes one fact per row; the reconciliation table
by U13's authority census, which is the arm that makes it closed rather
than remembered. The C10 and travel mappings derive from §C's own closed
rows. Every other list is illustrative and says so.

**D6 — Records are swept by the CLAIM's consumers, not the diff's
neighbours.**

### C-lite reconciliation — the CLOSED authority census

Round 4's blocker 2 was right that revision 4's two paragraphs were a
decision, not a census. Round 5's blocker 2 was then right that
revision 5's census had a derivation that could not find what it
claimed to — write sites instead of mutable state — so the table below
is re-derived under U13's corrected predicate, IDENTITY and CONTENTS
counted separately, and it grew by six rows in the doing. U13's
authority arm fails if the derivation finds one that is not here. That
is what makes it closed; the prose is the policy, the gate is the
exhaustiveness, and the derivation is now sound enough for the gate to
mean something.

| Authority | Disposition | Ordering and reconciliation |
|---|---|---|
| `CanvasDocumentViewModel`'s handle, load state and message, outline, table rows, warnings, detail text and title, degraded-load marker, where-am-I text, filter text | **ABSORBED** into the publication as the lease, the published load state, the population, the unit and the durable needle | they cease to exist as fields; every read is a publication read |
| The document's load generation counter | **REMOVED** | the LOAD SCHEDULE's request identity replaces it, which is also why the retired generation-matching vocabulary cannot come back |
| The document's async-close task | **ABSORBED** as the lease's close-once record | physical close, not currency |
| `CanvasSelection`'s selected node and **active surface** (identity) | **ABSORBED**; the object becomes a published projection | the active surface is DOCUMENT-class in U2's table and was independently mutable in revision 4 — the escape round 4 found. The persisted write follows the publication, never precedes it, and U4's carry-forward keeps it across a reload |
| `CanvasSelection`'s marked-ID set (CONTENTS — a readonly field holding a mutable set) | **ABSORBED** as the publication's durable marked IDs, which become an immutable set | round 5's blocker 2 found this one: readonly reference, mutated all day, invisible to a write-site derivation. Absorption makes the mutation unrepresentable rather than merely disallowed |
| The document's last-activated node, and its three shell-handoff delegates (auto-property CONTENTS and identity) | last-activated node **ABSORBED** into the population, where U2 already classifies it; the delegates are **SOURCE-FREE** — the shell wiring seam | also round 5's blocker 2: auto-properties mutate with no source-level field write. The delegates are installed once at composition and depend on no canvas class; installing one after construction is a wiring bug the arm can see |
| The document's and the surfaces' EVENTS — outline published, surface switched, and their siblings (subscriber lists) | **SOURCE-FREE**, with subscription itself an ordering obligation of the periphery | a subscriber list is observable mutable state; it carries no canvas class, and who may subscribe when is T1/T3's |
| `PanelWorkScheduler`'s shutdown flag | **SEAM 3** — external, ordered | the preterminal retired publication is published BEFORE the base shutdown, so scheduler-shut implies model-retired and never the converse; no model contract reads the flag; its refusal set is a strict subset of the model's |
| `PanelWorkScheduler`'s pending-work set and its work lock (CONTENTS — readonly fields holding mutable state) | **SEAM 3** — external | the third shape round 5's blocker 2 named. No model contract reads either; teardown does not wait on the set, and U12 step 4's physical close is what actually waits, under the LEASE's lock rather than this one |
| `PanelWorkScheduler`'s POST — the inherited marshal | **NEITHER WRAPPED, REPLACED, NOR CONSTRAINED — simply not used for slot writes** | round 6's blocker 1 asked which of the three this design does to the scheduler, and the answer the swap makes available is a fourth: none. The model never routes a publication through post, so post's null-context and synchronous-mode inline paths — the ones that falsified revision 6's premise — are irrelevant to model correctness rather than hazards to be fenced. The scheduler keeps its one job, STARTING background work, and the canvas async harness's deliberate null context becomes a supported arrangement instead of a contradiction. Post remains C-lite's route for PRESENTATION application, which is dispatcher-bound and is T1/T3's |
| `CanvasAnnouncer`'s retirement, and the document's speak-boundary read of it | **SEAM 4** — external, T4-ordered | the two instants differ deliberately so the last restoration sentence can be heard; the model guarantees only that a class-invalid sentence never reaches the announcer, and T4 owns whether a class-valid one emits |
| `CanvasModeController`'s active spec, committing flag, terminality, deferred departure and owner; `CanvasNavigator`'s attached presenter; the document's durable focus and filter-focus requests | **SEAM 1** — inherited whole | hardened across twelve codex rounds with their own terminality, addressing, completion and sweep contracts; absorbing them would REPLACE a shipped lifecycle, which the inheritance rules forbid. Divergence under reentrancy is T5 |
| `CanvasOutlineView`'s expansion, selection, selected row, connection host and count, and both surfaces' selection-sync flags; `CanvasSurfaceView`'s switcher and filter sync flags, where-am-I return focus, host window, deferred restoration and departure edge | **PERIPHERY** — PRESENTATION class, classification-only | marked unresolved against T1, T3 and T6; U13 fails if a model contract consumes one |
| `CanvasPreferencesViewModel`'s verbosity key | **SOURCE-FREE** — the declared live-preferences seam | app-wide, depends on no canvas class |
| `CanvasSurfaceView`'s shell-overlay predicate (a mutable STATIC) | **SOURCE-FREE** — the declared shell seam | a static escapes an instance-field derivation entirely, which is why the corrected predicate names statics; it depends on no canvas class |
| `CanvasAnnouncer`'s refused-after-shutdown counter | **SEAM 4** — the announcer's own test observation | a counter on the far side of the announce boundary; no model contract reads it |
| The workspace's memoized canvas command fields — IDENTITY | **NOT AN AUTHORITY** | write-once-from-null, declared as such; the census's one remaining exemption, now IDENTITY-only, and it is checked rather than assumed |
| The same command objects' CONTENTS — their can-execute subscriber lists | **SOURCE-FREE** — the WPF command-binding seam | round 5's blocker 2 in miniature: revision 5 exempted these whole because their references are written once, which said nothing about what the objects hold. They get a row instead of an exemption |
| The LEASE's close-once record | **NON-CURRENCY OPERATIONAL STATE** — the one piece U1 enumerates rather than forbids | never read to decide admission, validation, effect legality or delivery; read only to make physical close idempotent. A second such authority would be a finding |

**The consequence, stated rather than left implicit:** the model's
one-field invariant is a claim about the MODEL, not about the process.
No model contract reads a SEAM 1 authority, the document's own gate is
additional to the controller's rather than a replacement for it, and the
two orderings that DO matter — SEAM 3's and SEAM 4's — are stated above
and carry D4 pairs.

### UNRESOLVED OBLIGATIONS — the periphery ledger

Six obligations, split out by the user's ruling. Each carries the
originating finding as its acceptance criteria, words unchanged, with
link markup and backticks flattened so the citation census is not asked
to resolve names that describe another cycle's code. **Nothing here is
claimed resolved, and no model contract depends on any of it** — U13's
partition is the gate that enforces the second half.

*A reading note:* the criteria quote round 3, which reviewed revision
3's numbering. Two rewrites have renumbered what remains, so old U7 is
now U6–U7, old U10 is the apply and is gone to T3, old U11 is now U9,
old U14 is now U12, and old U15 is now U13. The quotes keep the numbers
they were written with, because a corrected quote is not a quote.

**T1 — Presentation atomicity.** Acceptance criteria, from round 3
blocker 1: *"Presentation is explicitly a second mutable currency
authority. U1 says the publication slot is the only mutable field and
nothing else is written after construction; U3 and the memory table
instead make each surface's installed unit its own mutable currency
authority. 'Written once per apply' still means repeatedly mutable, and
the materialized filter text plus deferred-restoration state are
additional presentation copies. Breaking arrangement: slot B publishes;
phase 3 writes B's needle while rows remain partly A and installed-unit
remains A until phase 6. A reentrant UIA reader observes B's text with
A's rows. User editing creates the inverse interval: the WPF text is B
before its handler publishes B. Remedy: either narrow the invariant to
one model authority and define a second, explicit presentation epoch
with reconciliation, or stage a complete immutable presentation snapshot
off-tree and atomically swap it."*

Revision 4 took the first half of that remedy — the invariant is
narrowed to the model — and leaves the presentation epoch to this cycle.

**T2 — The SURFACE half of provenance: composition, and forgeability.**
From round 3 blocker 5: *"Operation provenance does not compose with
C-lite's surface provenance and is forgeable as specified. Two panes
share the same unit, so unit identity cannot identify the initiating
pane. 'Immutable value' also does not say who may mint it; a consumer
could manufacture a token naming the current unit and relabel a raw
stale result. Breaking arrangement: a follow/trace operation starts in
pane P1; while it waits, P2 becomes the navigator's attached presenter
without changing population or unit. The token validates and the
consumer focuses or expands P2. Remedy: use an opaque, operation-minted
composite capability containing data provenance and initiating surface
for presentation-addressed effects. Encapsulate the result with that
capability so consumers cannot accept raw values or construct
replacement tokens."*

**Where the line now falls, per round 4's blocker 1.** U14 resolves the
DATA half of that remedy inside the model: opaque operation-only
minting, encapsulation of the result with its token, and a source class
sealed by the payload type so no caller can downgrade it. T2 keeps the
SURFACE half — the initiating-pane component, whether it can be forged
or mismatched across a presenter change, and the validation of
presentation-addressed effects — which is the part the P1/P2 breaking
arrangement actually turns on and which no model contract consumes.

**T3 — The apply as a transaction: reentrancy, exceptions, nesting.**
From round 3 blockers 6 and 7: *"Suppression prevents selected
callbacks; it does not make sequential WPF property writes atomic or
stop UIA/property readers. New B callbacks are enabled in phase 4 while
installed-unit remains A until phase 6. Breaking arrangement: a B
realization/focus callback fires during phases 4–5. If it validates
against slot B it acts on a half-B view; if it validates against
installed A it refuses a callback phase 5 may require. A raw UIA read
bypasses either gate."* And: *"There is no finally, rollback, fault
publication, latest-apply queue, or rule for Apply(C) reentering while
Apply(B) is in phases 3–5. Breaking arrangement: a control setter throws
in phase 3. Suppression stays entered, callbacks are detached, controls
are mixed, and installed-unit remains A. Remedy: specify transactional
staging with commit/rollback, guaranteed suppression exit, deterministic
fault posture, and latest-wins serialization of nested applies. Add
fault injection at every phase and a B-to-C nested-apply barrier test."*

**T4 — Teardown's speech window.** From round 3 blocker 8:
*"Retire-before-restoration makes the restoration effect illegal, and
its speech is not source-free. C-lite's cancellation invokes arbitrary
OnCancel host code before announcing. The result can contain a
row-derived count or title, contradicting 'depends on no row
population'. Breaking arrangement: retirement publishes; OnCancel then
attempts to restore selection or geometry. The document gate must refuse
it. If the controller still announces BackAt(title), it reports a
restoration that did not occur and launders row payload through the
typed exception. Remedy: run and authorize restoration under the
pre-retirement publication, capture its immutable outcome, then publish
retired and allow only that captured sentence; or define a capability
bound to the exact retiring unit that explicitly authorizes both
restoration and its one announcement."*

**Also owned by T4, from round 4's blocker 2:** the announcer's
retirement instant is not the document's, and the document's speak
boundary consults the announcer's. SEAM 4 states the model's side of
that — a class-invalid sentence never reaches the announcer — and T4
owns the ordering that decides whether a class-valid sentence emits
inside the window U12 step 3 opens.

**T5 — Divergence between the model and C-lite's inherited authorities.**
Round 4's major 13 was right that revision 4 carried this row's
diagnosis without its required outcome, which would let a later cycle
close it by merely recording that divergence exists. The full round 3
blocker 2 text, verbatim, including its remedy:

*"Current C-lite has mutable _active, _retired, _deferredDeparture, and
_owner in the controller, mutable _presenter in the navigator, and
mutable scheduler terminality. Revision 3 neither derives these from the
slot nor specifies reconciliation. Breaking arrangement: retirement
publishes while _retired is still false; a reentrant retained controller
admits mode entry and writes a new owner before Modes.Shutdown performs
the second write. The document funnel may refuse its sentence, but the
controller state has already diverged. Remedy: Put all document-class
ownership, request, mode, and terminal state into the publication and
make controllers transition shells, or explicitly define their separate
authorities and atomic reconciliation—abandoning U1."*

**The required outcome, so this row cannot be closed by observation.**
The model takes the remedy's second branch: separate authorities,
explicitly defined. T5 is closed only by ONE of two deliverables — a
proof that the two authorities cannot diverge OBSERVABLY under every
ordered transition, or a specified reconciliation and ordering
mechanism, in the shape SEAM 3 already demonstrates, together with a
barrier fact that returns round 3's reentrant arrangement when the
mechanism is removed. Recording that divergence exists does not close
it. The scheduler's half is already closed this way, so the row's
remaining scope is the controller and the navigator.

**T6 — Focus delivery as a distinct effect class.** From round 3 blocker
12: *"The widened effect universe still omits actual focus delivery.
C-lite has direct FocusRow / WPF Focus() effects after query
consumption. A request gate does not govern those calls. Breaking
arrangement: an A query completes after B; the consumer directly focuses
a row through the current presenter. No focus request is created, so the
only specified focus boundary is bypassed. Remedy: add focus
delivery/calls as a distinct effect class, validating data token,
initiating surface, and installed presentation immediately before the
WPF focus call. Add a retained-row direct-focus containment fact."*

### IMPLEMENTATION OBLIGATIONS — the second ledger

**Eight obligations, reclassified from contracts by the user's
design-by-implementation ruling.** Each carries round 7's finding
VERBATIM as its acceptance criteria, with link markup and backticks
flattened for the reason the T-ledger states — a backtick here is a
citation the census must resolve, and these name code that does not
exist yet. The discipline is the T-rows' discipline: **nothing is
claimed resolved, nothing is silently dropped, and no paragraph above
has been edited to look closed.**

The difference from the T-rows is the arbiter. A T-row waits for a
periphery cycle. An I-row is discharged **in this PR's implementation**,
by code plus facts plus a mutation battery, and it is closed when the
gauntlet says so — not when a revision of this document says so. An
obligation whose code lands without its battery is not discharged; an
obligation whose battery passes but whose mutation does not return the
named breaking arrangement is not discharged either.

**I1 — Refusal and fault cleanup can close a concurrently accepted
lease.** *"Root cause: 'close unless a final slot read names my lease'
is an observation, not an atomic ownership transfer. It was safe under
serialized delivery, but not with free-threaded concurrent deliveries.
Breaking arrangement: Deliveries A and B share result/lease L while the
request is pending. A faults during rebase and enters finally. A's final
read sees no publication naming L. B wins the acceptance CAS and
publishes L. A closes L based on its earlier read. The live publication
now names a closed handle. The close-once record prevents double-close,
not this first erroneous close. This falsifies D1's fault assertion that
'no publication ever names the lease' and is absent from the
concurrent-delivery/fault batteries. Refusal or fault must first make
acceptance impossible through an atomic publication transition, or
cleanup must otherwise synchronize with acceptance."*

**STATUS after T2: DISCHARGED.** Cleanup is a TRANSFORM. A release that
still owns its lease publishes a terminal state for its own request
before it closes anything, and an acceptance reads that state and
declines — so codex's own first remedy branch, "make acceptance
impossible through an atomic publication transition", is what the code
does. The two orders are the only two: release-first publishes the
terminal state and the acceptance declines; acceptance-first publishes
the lease and the release reads a publication that names it and closes
nothing. There is no third interleaving because there is one field and
one gate, and both decisions read it inside that gate. *Mutation:* the
observation the frozen design described — read the slot, then close —
with a barrier landing the acceptance between the read and the close,
returns the finding's arrangement exactly: a live publication naming a
closed handle.

**And after T3:** driven under fault injection at all five points of
the ownership window — the four inside leave the close observation at
exactly one with acceptance impossible, the one after leaves the lease
live and published — which is the rewrite of expected values the
frozen plan said this obligation would demand.

**I2 — The retry loop is lock-free but not terminating.** *"Root cause:
a failed CAS proves only that some writer progressed. It does not make
this delivery consumed, superseded, or retired. A load delivery can
repeatedly read a live, latest, pending request while keystrokes, filter
completions, selections, or other intents win successive swaps. Each
loss requires another full rebase; none produces terminal refusal.
Teardown's unconditional retirement CAS can likewise starve before it
ever installs the absorbing retired state. Terminal absorption therefore
bounds retries only after a terminal state wins. Nothing bounds retries
before then. U4's claim that a loser will 'by then' refuse and U12's
termination claim are false; the verification plan has no
sustained-contention/starvation fact."*

**STATUS after T1: discharged for TERMINATION; LIVENESS conditional on
I4.** The retry loop is gone rather than bounded — publication
serializes, so every attempt succeeds on its first swap and no
publisher re-decides, which the battery asserts structurally rather
than by wall clock. What that trades is recorded here rather than in a
report, because it is a dependency this row now has and did not before.
Under the optimistic loop a slow transform cost only its own
publisher's progress; under the gate it costs EVERY publisher's,
including the UI thread's. So "the bound is one transform" is a bound
exactly insofar as transforms are closed and small — which is
obligation I4, and until T4 lands the transitive purity predicate it is
enforced by nothing. **I4 is therefore a LIVENESS obligation for I2 and
not only a purity obligation**, and I2 is not fully closed until it
lands.

**And the cost T1 created, named so it is not discovered later.** The
gate introduces a DEADLOCK CLASS the optimistic loop could not have
had: a monitor with no timeout and no cancellation, so a transform that
marshals to the UI thread and waits while the UI thread is blocked in
the same gate deadlocks. The reentrancy refusal T1 landed covers a
strict subset — same slot, same thread — and does not touch this. Its
guard is I4's no-callout rule, which is the same conditionality said
once more: the wait is finite because the transform is closed. Named in
`CanvasPublicationSlot`'s own doc comment as well, so the hazard sits
where the mechanism is.

**I3 — Post-swap effects are neither linearized nor given a valid
thread/lifecycle story.** *"Root cause: CAS linearizes the slot only. It
cannot make a subsequent external action atomic with validation or
publication. The effect walk is: Lease close: intended free-threaded,
but finding 1 breaks refusal cleanup. Starting background work:
free-threaded, but a won schedule can become stranded if starting throws
or a required marshal is rejected. Announcement: UI-affine; the
inherited announcer asserts access to its captured dispatcher and owns
dispatcher timers. Property notifications, events, focus requests,
presenter calls, mode closures, and shell/tab handoffs: inherited WPF/UI
authorities, not safe merely because the slot write was free-threaded.
Teardown on a pool thread: slot reads remain coherent, but crossing
Modes.Shutdown, shutting down the announcer, and clearing bound requests
are not established as pool-thread-safe. Direct execution violates
affinity. Posting restores the marshal—and its rejection, ordering,
teardown, and drain story—through the back door, but revision 7
specifies none of that. D2 also has a TOCTOU hole: an effect can
validate A, a pool writer can publish B, and the effect can act on A
afterward. 'Caught at DISPATCH' does not cover a publication between the
dispatch check and the external call. The model effect universe
explicitly includes announcement, shell handoff, mode closures, and
focus requests, so T1/T3 cannot absorb this entire defect."*

**STATUS after T3: OPEN, with the load path's shape landed.** The
delivery's projection is an effect validated at DISPATCH — it reads the
slot when it runs and applies the newest publication or nothing — and a
won schedule cannot be stranded by its own start, because the request
transform and the start share the caller's thread with the scheduler's
refusal ordered BEHIND the model's (SEAM 3's fact). What remains is the
rest of the effect universe: announcement, focus requests, presenter
calls and shell handoffs are still inherited WPF authorities with no
validated story, and this row stays open until they have one.

**I4 — Transform purity is a discipline, not unrepresentable.** *"The
required decisions are mathematically computable as follows: Admission:
snapshot plus immutable request input — pure. Provenance validation:
snapshot plus sealed token/payload type — pure, subject to finding 6.
Rebase: snapshot intents plus immutable incoming population — pure,
subject to finding 7. Refusal-close determination: not snapshot-only; it
deliberately performs a later ownership read and close, producing
finding 1. Schedule promotion: decision is pure; starting the promoted
job is an external effect. None inherently needs C-lite authority, a new
FFI call, or wall-clock time. The design's problem is enforcement: U13
describes no transitive call/capture census preventing a transform
delegate or helper from reading C-lite state, invoking FFI, consulting
time, starting work, or closing a lease. One CAS site and a helper-owned
allocation do not prove purity. A closed command algebra or an
executable purity predicate with capture and call-graph restrictions is
required."*

**STATUS after T4: the executable predicate exists, one level deep,
and this note says which level.** Every transform handed to the gate —
twelve sites, floored at ten so the arm cannot silently scan nothing —
is walked for the finding's own list: session, FFI, clock, scheduler,
announcer, lease close, live UI state, locks and interlocked
operations, with the probes stripped by name as the dispositioned
instruments they are. What the arm does NOT walk is the transitive call
graph: a transform's callees are the model's pure surface, and the
compiler-grade walk over every callee's closure is the not-taken
alternative — recorded here so the claim is exactly as big as the
census that enforces it. I2's liveness conditionality stands, resting
on this arm's discipline.

**I5 — Fresh allocation and ABA are not enforced by the stated writer
census.** *"Root cause: 'one swap site,' 'no other field write,' and
'the helper builds the successor' do not prohibit the helper from
returning an interned record, memoized successor, predecessor identity,
or shared Empty/terminal sentinel. Interleaving: T reads shared E; other
writers perform E to B to E; T's stale CAS expecting E succeeds. This is
precisely the shared-Empty-sentinel class already present in section C
history. D4 mutates the helper to accept a caller-built record, but does
not plant helper-internal caching or sentinel reuse. The census must
prove a fresh publication allocation on every installing attempt and
prohibit publication-valued caches/statics/interning and identity-return
paths."*

**I6 — Source tags can still launder finer data through transformations
or ambient reads.** *"Root cause: lexical ownership of wrapper
construction does not prove semantic provenance of the value placed
inside it. A preserving Map over a document-tagged seed can run inside a
population-aware closure and compute Rows.Count; the output remains
document-tagged, no constructor takes value-plus-class, no unwrap
occurs, and the transformation did not syntactically coarsen its input.
The payload join then honestly validates a dishonest document tag. The
same escape exists when a class-owning object can reach finer nested
state and constructs its own nominal tag from it. The source-tag arm
checks construction location and tag direction, not all data
dependencies. Transformations must accept every dependency as a tagged
input and derive the join, with arbitrary captures/ambient reads
forbidden and mutation-tested."*

**I7 — Copy-not-alias is asserted but not mechanically established.**
*"Root cause: nominally trusting ImmutableArray or another immutable
collection does not establish that its backing storage was copied. The
immutable-collections marshal's as-immutable-array over a
caller-retained array produces a trusted nominal immutable type while
retaining a mutable external alias. A published snapshot can then mutate
in place, invalidating rebase repeatability and CAS decision stability.
The five-shape premise battery catches a field typed as a read-only
interface; it does not exercise an owned sealed type whose
constructor/factory stores an unsafe immutable alias. The construction
predicate needs a closed whitelist of copying operations, transitive
constructor/factory analysis, and a planted retained-array alias fact."*

**STATUS after T4: the structural half is a WALL, not an owned row
type, and the narrowing is recorded.** Three arms: nothing in the shell
writes into a row's group path — the one mutable field a core row
carries; rows are re-materialised at exactly one site; and immutable
collections are built only at the two sanctioned files. The owned row
type — model-owned immutable copies of core's records — was not taken:
it would double every row allocation to close a write no consumer
performs, and the wall fails by name on the day one does.

**I8 — "No other write" is not yet a compile-time closed predicate.**
*"Root cause: the publication-writer arm describes ordinary source
assignments only. It does not forbid or discover reflective field-info
set-value, serializer hydration/hooks, unsafe accessors, generated
accessors, or init-only/private-field bypasses. Any owned path using one
of these can install a non-fresh publication without reaching the CAS
site while the direct-write census remains green. The design must either
prohibit and census these mechanisms across the closed world and
generated surfaces or explicitly narrow the claim and provide another
enforcement boundary."*

**STATUS after T4: DISCHARGED AS NARROWED, and the narrowing is the
remedy's own second branch.** The census proves the source claim: one
writable publication-typed field in the model, one seed assignment, one
compare-and-swap, the token absent from every other model file, and no
model type opting into serialization. Reflection, unsafe accessors and
generated hydration remain beyond a source census — the finding's own
list — and their enforcement boundary is the runtime one T1 shipped:
the bypass detector, which the census now makes structural by proving
there is no ordinary write for it to miss.

**What round 7 ratified while finding these**, and what the
implementation therefore starts from rather than re-litigates: the CAS
prevents two writes against the same predecessor; the two claimed
bonuses — atomic rebase-with-publication and retirement-versus-acceptance
ordering — are valid safety properties once a swap succeeds; the record
pass is clean at D1's nineteen rows, D4's thirty-eight and U13's nine
arms; and the undelivered-population owner with its three load-result
dispositions reconciles correctly.

### PR F obligations this PR must not silently absorb

§C's travel table carries one row that is F's: the originating surface
through the routed-command boundary. This PR touches the document F's
command helper resolves, which makes it the PR that could absorb the
obligation by accident. It does not, and this design adds no path by
which a palette entrant can name a pane.

### C10's interim costs, and where each is resolved

| The interim's cost | Resolution |
|---|---|
| Every keystroke pays one match on the UI thread, under the FFI lock | U9: a pending unit publishes immediately, the match runs off the dispatcher, and the published state machine bounds a burst to two matches |
| The rows and the answer can come from different handles | U6's three validations plus U7's payload-class validation, minted and sealed by U14 |
| The count and the state are two fields, so a reload can make the count describe a pane showing nothing | U1 and U11 at the model. The DISPLAY half is T1 and is not claimed resolved |
| A stale answer must not widen the rows | U9's request identity, with the publication as its authority |

### Where every travelling row lands

| Travelling (from §C) | Lands |
|---|---|
| The off-dispatcher match | U9 |
| The failed-answer bit and the four-branch summary | U9's answer state, on the unit |
| The projection unit | U2's population and unit classes |
| The publication transaction | model half in U11; presentation half is **T1/T3, UNRESOLVED** |
| Silence-first teardown | model sequence in U12; the speech window is **T4, UNRESOLVED** |
| Their censuses | U13's arms |
| Their facts | the verification plan below |
| The originating surface (PR F) | not absorbed |

### Verification plan — model side

**Censuses.** U13's arms, in its own order: owned-symbol closure,
partition, classification manifest (both tables), payload class, source
tags, publication writers, authority census, capability, effects — each
with a discovery floor and fail-closed resolution. The partition arm
additionally asserts that no model contract consumes an
unresolved-marked member, and that every marker names an open obligation
row. The manifest arm additionally asserts U4's carry-forward
completeness over every DOCUMENT-class entry. The publication-writer arm
is the one that replaces revision 6's unproven premise, and it is worth
saying why it can: "every writer is on the executor" is a runtime
property a gate can only sample, while "there is one swap site and no
other write to this private field" is a question the compiler answers.

**The authority arm owes a PREMISE fact, because its predicate has been
wrong twice.** Plant each of the FIVE shapes the probes have found — a
readonly field holding a mutable set, an auto-property, an event, a
mutable static, and round 6's read-only interface over a caller-retained
list — and assert the derivation finds all five. The fifth is the one
that matters most: it passed the four-shape battery revision 6 shipped,
which is the argument for growing the battery with every correction
rather than trusting the predicate that survived the last one.

**A derived theory over the state product**, enumerated from the type,
asserting every column of every row — including row 3, constructed
through U10's sanctioned same-lease successor rather than by mutation.

**A derived theory over U9's transition table**, enumerated from the
machine: every state crossed with every event, asserting the published
schedule and whether a job starts. The three arrangements that must be
asserted by name are the queued completion discarding its answer, the
non-running completion publishing nothing, and load acceptance retiring
both entries and reseeding from the rebased needle.

**Barrier-driven concurrency facts**, deterministic by injected barrier:
a query completing after a same-population successor (exposed); after a
new population (refused); after lease death (refused, nothing
cancelled); a waiter acquiring the FFI lock after death (refused before
FFI); ten requests scheduled (at most two matches, with the running and
queued pair asserted at each transition); a completion racing a
keystroke and a completion racing a reload, in both dispatcher orders —
which replaces revision 4's both-completion-orders fact, since one
running job at a time makes a reversed job order unreachable and round 4
was right to say so; an answer arriving after teardown (dropped); both
load completion orders; a retire-before-load-delivery barrier (refused,
and the lease it opened is closed by the delivery).

**ROUND 6'S BREAKING ARRANGEMENT, run as a fact.** Construct the
document through the shipped null-context production-mode harness — the
one that made revision 6's premise false — and complete two load workers
concurrently on pool threads, barriered so both read the same snapshot
before either swaps. Assert exactly one publication survives, exactly
one lease is closed, and the close observation totals one. The same
shape for two filter completions, and for a load racing a filter. A
design whose premise a review falsified owes its verification plan that
review's own arrangement, not a paraphrase of it.

**Swap facts.** A losing attempt publishes nothing and starts nothing
(the promoted job's start count stays at zero for the loser); a
re-decision after a lost swap refuses when its request has been
consumed; a publish helper handed a caller-built record does not compile,
which is the census's job rather than a fact's; and a retirement
swapping against a concurrent acceptance leaves the document retired in
both orders.

**One-shot delivery facts, against the publication rather than a latch.**
A double delivery of an ACCEPTED load result (the second call reads
consumed, refuses, and does NOT close: the lease stays live, the
publication is unchanged, and the close observation stays at zero); a
double delivery of a REFUSED result (the close observation reaches
exactly one); a delivery arriving during retirement; and two concurrent
deliveries in both orders.

**Teardown-without-a-dispatcher facts** — round 6's blocker 2. Tear the
document down on a pool thread with no synchronization context at all
and assert the preterminal and terminal publications both land and the
handle closes; tear down while a delivery is mid-window in both swap
orders; and tear down with a filter job in flight, asserting the
terminal publication does not wait and the completion refuses at
delivery.

**FAULT-INJECTION facts across the ownership window** — one per row of
U4's five-point table. Throw immediately after the slot is read, during
the rebase, during filter reseeding, **immediately before the CAS** —
round 7's minor, since there is no assignment to be before — and
immediately after it. The first four must each leave the close
observation at exactly one; the fifth must leave it at zero with the
lease live and published. **OBLIGATION I1 rewrites the expected value of
the first four under concurrency**, which is exactly why it is an
obligation rather than a paragraph. This is round 5's blocker 4 as a
battery
rather than a paragraph.

**Rebase and carry-forward barriers.** A selection of B while a load
carrying A is in flight (B survives); the same for a mark added
mid-load; the same for a needle typed mid-load, which must also seed the
new machine; and a SURFACE SWITCH to Table mid-load, which must survive
acceptance rather than being defaulted back to Outline.

**Provenance facts.** No accessor yields a payload without its token
(asserted by the census, and by a fact that a retained payload cannot be
obtained); a class read from the payload type rather than from the
caller, so a document-class boundary refuses a population-derived count;
a payload type whose declared class is coarser than its fields' join is
rejected by the payload-class arm, with a planted under-describing type
as that arm's premise; a planted population wrapper constructed outside
the population, and a planted coarsening transformation, each rejected
by the source-tag arm BEFORE any payload exists, which is that arm's
premise; exactly one construction site each for tokens and payload
types.

**Effect-containment facts.** One per model boundary in U8: a retained
filtered-out row cannot mutate, announce, request focus, or hand off to
the shell; a retained mode closure cannot run unsourced; a lease-sourced
sentence refuses after the lease is replaced. Focus DELIVERY is T6's
fact, not this cycle's.

**Retention facts, by the right instrument.** Weak references for
MANAGED owners: publication and unit collection under a same-lease
successor; population collection after a reload; one per D1 release row.
CLOSE OBSERVATIONS for native release: the lease closes exactly once
after terminalization, the refused load result closes exactly once, and
a dropped-without-closing mutation is caught by the observation rather
than by a weak reference.

**Retained-owner facts** — round 5's major 6 and round 6's major 5. A
sealed result held across a terminal publication keeps its unit and
population alive and the weak reference must NOT clear; it clears once
the result is dropped. The same for a constructed-but-undispatched
effect, and the same for an UNDELIVERED LOAD RESULT, whose population
is unpublished and whose retention is therefore invisible to any
publication-based accounting. All three are the positive form of row 5's
corrected claim: terminalization drops the slot's owner, not the graph.

**Ordering facts.** SEAM 3: a load issued between the retired
publication and the base shutdown is refused by the model, not silently
dropped by the scheduler; reversing the writes returns the silent drop.

**Mutation pairs.** One per D4 row, run and reported as a pair, each
with its first half demonstrated to actually enable the state.

**What this plan cannot catch:** a race the barriers do not model; a WPF
binding path outside the owned-symbol closure; and everything in the
ledger, which is the point of the ledger.

**Gates.** Unit suite in both configurations, the FlaUI journeys, and
the format gate across all three projects — run per project, since the
two test hosts race when the solution is invoked as one.

### Deliberate departures from the architecture review's Option A

**The token is a reference, not a stamp** — a counter carries the ABA
shape this branch retired, and the handle it would stamp is a reused
integer. **Currency is four nested model classes plus presentation**,
not one. **Currency is DERIVED from one field, never carried.** **A
valid result is not a licence to act** — Option A bound the query to its
handle; U7 binds the effect to the currency class its payload declares,
and U14 makes that declaration something a caller cannot reach.

### Design round record

**Round 1 — NOT SOUND, 10 blockers.** Projection was classified by
STORAGE TYPE rather than coherence dependency.

**Round 2 — NOT SOUND, 11 blockers.** Three classes were right as far as
they went; the defects were one mistake repeated — a mechanism per
question instead of one authority.

**Round 3 — NOT SOUND, 12 blockers, and the TRIPWIRE FIRED.** The tally
the coordinator had set the tripwire against: **nine of fifteen findings
classified "(i) a new mechanism failing"** rather than "the one-field
invariant not yet reaching a member". That is the treadmill signature —
each revision's new mechanisms generating the next round's findings —
and it escalated.

**THE USER'S RULING: split the design.** Round 4 verifies the MODEL side
only. The presentation and effect periphery becomes six explicit
obligations with round 3's findings as acceptance criteria, to be
resolved in their own cycle — possibly by design-by-implementation,
since prose has now failed three times in exactly the region where
code-level arbitration succeeded repeatedly on C-lite.

**Round 4 — NOT SOUND, 8 blockers, and the FIRST COUNT DROP.** Twelve to
eight, with the tripwire not re-firing. Three blockers were the
hidden-dependency class the split told the reviewer to hunt — model
provenance resting on T2, teardown's sequence unimplementable without
T4, and the model census swallowing the periphery it was supposed to
exclude — which is the split working as a review instrument. The rest
were completions rather than new mechanisms: LEASE had no rule, the
reconciliation was a decision rather than a census, the filter machine
was partial, delivery was one-shot only by discipline, and load
acceptance did not rebase. Revision 5 pulls the model half of provenance
in from T2 as U14, adds the authority census as U13's sixth arm, and
makes the schedule total.

**Round 5 — NOT SOUND, 5 blockers, and the SECOND consecutive drop.**
Twelve to eight to five, tripwire quiet both times, and for the first
time in the series **no finding moved the split's line**: all five
blockers were inside the model, and round 5's own walk states that
revision 5 introduced no new dependency on T1–T6. Its blockers were two
converses and three completions. The converses were the sharp ones —
U14 proved WHO built a payload but not that the class it declares is
honest about what is inside it, and the authority census had a
derivation that observed reference assignment rather than mutable state,
which a probe confirmed by naming a readonly-but-mutated set the census
would have passed over. The completions: the atomic claim was a second
mutable authority contradicting U1, its exception window could leak a
handle, and acceptance's carry-forward was a list that had gone short by
one member. Revision 6 answers the first converse with a field-level
minimum-class rule and its own census arm, the second with a
type-derived predicate that separates identity from contents, and
removes the claim entirely by making the spend a publication transition
— which keeps one mutable currency authority instead of narrowing the
invariant to accommodate a latch.

**Round 6 — NOT SOUND, 4 blockers, and the PREMISE ATTACK LANDED.**
Revision 6's own report flagged one load-bearing assumption for the
reviewer's attention — that delivery is dispatcher-only — and round 6
audited it and found it not merely unproven but FALSE against inherited
code: the scheduler's post runs inline on the worker under a null
context and inline on the caller in synchronous mode, and the canvas
async harness installs exactly that null context on purpose. Two
deliveries could both accept and one lease could leak, in a shipped test
configuration.

The offered remedy was a model-owned serial executor with its own
enqueue lifecycle, closed entrypoint and death story — two blockers'
worth of new mechanism. **Revision 7 removes instead.** The publication
becomes a compare-and-swap against the decision snapshot, which makes
serialization a property of the one field rather than of a thread, and
that single change discharges blocker 1, dissolves blocker 2 by leaving
nothing to post or abort, makes the rebase atomic with its publication,
and lets the inherited scheduler stay exactly as it is because the model
never marshals through it. The third removal of the series, and by some
distance the largest.

The other two blockers were converses of the two newest rules, each
right and each the same shape: revision 6 applied "the class comes from
what produced the value, not from what the caller says" to payload types
and stopped one level short, so a source wrapper could still be tagged
by hand — and it defined deep immutability structurally, so a read-only
interface over a caller's list read as immutable. Both are tightened by
pushing the existing rule further down rather than adding another.

**Round 7 — NOT SOUND, 8 blockers, and the TRAJECTORY BROKE.** 8 to 5 to
4 to 8. Six of the eight were class (i), and all six were against the
PERIPHERY of one mechanism — the swap's ownership transfer, its
progress, its effects, its purity, its freshness, its census — while the
mechanism's core was ratified in the reviewer's own opening sentence:
the compare-and-swap correctly prevents two writes against the same
predecessor. The record pass was clean, the two claimed bonuses were
confirmed valid, and the three vocabulary residues were the only
non-blocker.

Revision 7's report had pre-committed to this reading: it named the
swap's four honesty rules as the obvious next attack surface and said
that if round 7 found them failing, the pattern would be that this
model's remaining defects are exactly the ones prose cannot settle. It
did, and they are.

**THE USER'S RULING: DESIGN BY IMPLEMENTATION.** Prose stops arbitrating
what code arbitrates better. This section is FROZEN as the ratified
baseline; the architecture listed at the top binds; and the eight claims
prose could not close are reclassified from contracts to IMPLEMENTATION
OBLIGATIONS I1–I8, each carrying round 7's finding verbatim, discharged
by code plus facts plus mutations and arbitrated by the implementation
gauntlet. This is the same ruling the split made about the periphery,
arriving at the model — and the two rulings now say the same thing about
the same document, which is the strongest evidence either of them was
right.

**What the seven rounds bought, since the count alone reads as
failure.** Nothing here cost an implementation round: every one of these
findings would otherwise have been found against code, or not at all.
The chain, the one slot, the five classes, provenance-by-type, the
pure-query counter-position and the swap's direction are all ratified,
and three mechanisms were REMOVED rather than patched — the generation
counter, the delivery claim, and the serial executor the dispatcher
premise would have required. A design that ends smaller than it started
and hands its unsettled claims to code with the reviewer's own words
attached is not a failed design.

**THE COUNTER-POSITION RULING, load-bearing for implementation.**
Revision 2 argued against round 1 that a query on a superseded unit
should be ADMITTED, and it survived rounds 2, 3 and 4: the non-filter
queries are functions of the loaded model, core's sibling ordinal is a
model position, the filter is the one needle-dependent query and has its
own currency. **Its boundary is the other half:** it holds at the
PURE-QUERY level. A population-valid result is not by itself a licence
to act, which is U7 and U14 — and the composition of data provenance
with SURFACE provenance is T2.

**What rounds 3, 4 and 5 confirmed sound and stable**, and what this
revision therefore does not touch: the lease-population-unit chain; the
slot's execution path, where terminal publication plus
admission/under-lock/completion validation stops queued and in-flight
consumers from invoking or exposing through a closed lease;
physical-close-outside-currency; the eager memo's placement in the
population; the pure-query counter-position; the five-class
classification; and T1, T3, T4 and T6's retention of their round 3
arrangements. Round 5 added five: U9's transition table is complete at
all fifteen cells with the queued-completion cell promoting the latest
request; U12's refusal sequence is coherent with its finally enclosing
steps 1–3, which revision 6 says outright; U14's construction-site count
is mechanically enforceable and sealed carriage does prevent
payload/token recombination; delivery-versus-retirement is correct in
both orders; and D4's twenty-seven rows stood, with the impossible
reverse-job-order fact confirmed gone. Round 6 added three more: the
refusal-close walk is correct with a live publisher and only the
shutdown case failed, which the swap removes; B5's carry-forward cannot
be defeated by misclassifying the active surface as rebased, since the
normative rule and the mid-load barrier both hold it; and D1's nineteen
and D4's thirty-two rows were confirmed, with the census count stated
once and referenced rather than recounted.

**The lesson the split records, now confirmed twice over.** Three rounds
of prose could not converge on the presentation and effect periphery,
while the same reviewer's model-side findings narrowed each round —
round 4's drop was the first evidence the split was the right instrument
and not only the right scope. Round 7 then showed the model reaching its
own version of the same wall, three rounds later and one layer down.

The difference was never the reviewer and never the effort. It is that
concurrency semantics — like WPF's presentation semantics before them —
are discovered by RUNNING them, and prose about them is a claim nobody
can check until code exists. Ownership under a race, retry termination
under contention, effect affinity, transform purity, allocation
freshness: every one of those is a sentence a reviewer can always
falsify on paper and a battery can settle in an afternoon.

That was the argument for design-by-implementation on the periphery, and
it is now the argument for it on the model. Both rulings are about
METHOD rather than scope, and the seven rounds are what makes the
architecture they hand over worth building — the boundary between what
prose settled and what code must settle is drawn from evidence, not from
where anyone got tired.

### Implementation record

**T1 — the slot and its algebra.** Landed
`CanvasPublication` with its transform algebra, `CanvasPublicationSlot`,
the two schedules, `CanvasRequestIdentity` and `CanvasModelCopy`.
Claimed discharged: **I2, the runtime half of I5, and the construction
half of I7** — said that way rather than as "three obligations", because
two of the three are halves and the other carries a conditionality of
its own. Each has its mutation returning the arrangement its finding
NAMES:

* **I5, runtime half.** Every transform allocates, including when it
  sets the value already there; the slot refuses a transform that hands
  back its own snapshot; and across an 800-publication run that
  deliberately cycles content, the install observer reports zero
  repeated references. *Mutation:* a memoising algebra reinstalls a
  record, and a stale swap expecting the earlier value then succeeds —
  I5's B-to-E-to-B interleaving, returned.
* **I2.** THE RETRY LOOP IS GONE, not bounded. Decision and install are
  one critical section, so every attempt succeeds on its first swap and
  no publisher re-decides. The facts assert a publisher and an
  unconditional retirement both complete under four contending threads
  in both start orders — with the overlap established by the victim's
  OWN observation of a foreign publication rather than by a counter
  sampled after the join — and, the part with teeth, that the victim's
  transform ran exactly once per call and that every call installed.
  *Mutation:* the optimistic loop with a deterministic interleave never
  terminates, spending a full transform per loss; and its victim is a
  real load delivery, so after sixty-four losses the battery can assert
  the finding's own semantic claim — the request is still latest, still
  pending, and the document is not retired, so losing produced none of
  the three terminal answers the finding names. **This row's liveness
  half is conditional on I4; see its status note in the ledger.**
* **I7, construction half.** Collections are copied at the one
  construction site; a caller who retains what it handed in cannot move
  a published snapshot. *Mutation:* the same value built over a
  caller-retained array through the immutable-collections marshal
  mutates in place.

Two things the code decided that the prose had not. The publication is a
sealed CLASS rather than a record, because currency here is reference
identity and a generated value equality would give every currency
question two answers depending on which operator a caller reached for.
And a reentrant publication is REFUSED — a transform that publishes is
not a pure function of its snapshot, and a monitor is reentrant, so
without the refusal an inner publication would install against a
snapshot the outer attempt still holds. That refusal is also the first
runtime piece of obligation I4.

One branch is knowingly uncovered and its owner is named: the swap's
failure path is a bypass detector that no T1 fact can reach, because the
field is private and one method writes it. Task T4's
publication-writer census (obligation I8) is what makes that structural
rather than defensive.

**T2 — the model types and the lease's ownership primitive.** Landed
`CanvasHandleLease`, `CanvasPopulation`, `CanvasProjectionUnit` and
`CanvasLeaseTransfer`; the publication grew to hold the three finer
classes and the load schedule gained the terminal state I1's mechanism
publishes.

* **I1 — discharged**, per the status note in its ledger row. The
  mutation restores the observation and returns the named arrangement.
* **The round-6 probe set, now facts rather than prose:** two racing
  loads leave one publication and one closed lease; a load delivering
  during a reload is refused and the reload's lease survives; a refused
  delivery whose lease the live publication names closes nothing. Plus
  the acceptance-versus-release race run two hundred times, asserting
  the only two reachable end states.
* **The close-once record**, whose disposition U1 already carried,
  now has its facts: close happens once however many callers ask, and
  concurrent closes collapse to one with every caller waiting for the
  handle to actually be gone rather than for somebody to have started.

**The seam between the close-once record and I5's freshness, resolved
as the dispatch asked.** A closed lease is not resurrectable through a
retained unit, and the mechanism is a SLOT-DERIVED read rather than a
second field: admission is "does the live publication name this lease",
a lease is closed only when it does not, so closed implies unadmitted
and the closed handle is unreachable through the admission path instead
of guarded behind a flag. The lease's invoke carries a detector for the
contradiction — admitted while closed — which unlike task T1's bypass
branch IS reachable, since a caller supplies the predicate.

**Obligation I7 reached one level further down.** A core row is a
uniffi record carrying a mutable group-path array, so copying the row
SEQUENCE leaves an alias one field down. The population re-materialises
every row with a group path of its own, and the fact plants both
aliases — the caller's list and the caller's array — and shows neither
moves it. What stays open is a consumer mutating an array through a row
it obtained FROM the population; that needs an owned row type or an
analyzer and is I7's structural half, T4's.

**The two mutable authorities T2 introduced, dispositioned.** The
lease's close-once record is the NON-CURRENCY OPERATIONAL STATE the
restated U1 already enumerates; T2 is where it becomes real, and it is
read for idempotence and never to decide admission, validation, effect
legality or delivery. The lease's closing capability — a readonly field
holding a delegate, and therefore a CONTENTS authority under the
corrected predicate — is SOURCE-FREE: it is the loader's call into the
session, installed once at construction, and it depends on no canvas
class.

**Three things the code decided that the prose had not.** A release
whose request has been SUPERSEDED, or whose document has retired,
closes and publishes nothing: the terminal state is published only for
a request that is still latest and pending on a live document, because
writing RELEASED under an older request's name would overwrite
the newer request's schedule entry — I1's defect family arriving from
the other side — and acceptance of a superseded request was already
impossible before the release read the slot, so its close needs no
publication to be safe. The unit holds no reference to its population:
the chain nests, so a unit is current only while the population
published beside it is, and naming that population from inside
`CanvasProjectionUnit` would put one fact in two places for a currency
comparison to disagree about. And T1's two freshness facts were
extended to the three transforms T2 added — the finer classes
installed, the unit replaced, the terminal publication — with the
defect injected FIRST, as the lesson below says to: a memo keyed on the
lease-population-unit triple made the per-transform arm fail on its own
and the cycling run report 198 repeated references in 200 rounds. Then
reverted, byte-for-byte.

**Two lessons from T1's review worth keeping in the record**, because
both were about the gates rather than the code. A remedy that looked
correct was not: extending the freshness run to all eight transforms
caught nothing until the run also CYCLED the schedule values, because
rebuilding them handed every transform a key it had never seen and a
content memo could never hit — found only by injecting the defect and
watching the remedy fail to catch it. And a floor is decorative unless
it is above the population it guards: the citation arm counts
occurrences rather than distinct names, so raising it by counting the
newly bound names would have left it satisfiable with every one of them
removed.

**T2's review, and what it moved.** Ten findings, none against the
ownership transfer's mechanism and all against its periphery and the
batteries' teeth — T1's shape again. All addressed.

* **The displaced lease had no owner.** A reload's acceptance swapped a
  new lease in over the one the publication named, and after the swap
  nothing could reach the old one — no publication named it, no release
  owned it — so every reload leaked a native handle. The swap's effect
  step now closes what its decision snapshot named, from the outcome
  rather than a second read. The frozen transition text's other order,
  un-naming the old lease before the new load is built, stays T3's to
  choose, and under it nothing is displaced.
* **Acceptance did not reseed.** The frozen "a reload cannot strand the
  filter machine" row was being carried by a comment: a running filter
  request survived the reload with no callback able to clear it, and
  every later keystroke queued behind it. Acceptance now retires both
  schedule entries and reseeds from the carried needle in the same
  publication, and the request it mints is read back from the successor
  and handed out in `CanvasLoadAcceptance`, because it is the job T6 has
  to start.
* **The chain was a comment.** Three nullable fields could spell a unit
  beside no population; `CanvasLoaded` holds the three finer classes as
  one value, so the only way to have a unit is to have the population it
  projects and the lease that loaded it. A unit projected from a
  DIFFERENT population than the one beside it stays open — the unit
  carries no population reference, by the decision above — and is named
  as T6's discipline and T4's census.
* **The release reported a decision, not a deed.** A superseded
  request's release said "published and closed" having published
  nothing, and "already released" depended on whether an unrelated newer
  request had moved the schedule. The close-once record now answers its
  one question to the caller — was this the call that closed — and the
  release reports that; its `Closed` says plainly that the terminal
  state is published only when the request was still latest. A release
  after the TERMINAL publication declines rather than installing a later
  record over the one every holder treats as final.
* **A throwing close** is issued once, never retried, and the exception
  reaches the caller: re-arming would hand the next caller a second
  close of a handle whose first may have half-completed. Sequencing that
  fault against the one a finally is cleaning up after is T3's.
* **The ancestry walk was wrong on a depth gap.** Depth-indexed parents
  attached two rows at depth 2 under a root to each other. The
  population now uses the outline view's stack of open ancestors, and
  the old walk, restored, fails the new fact with the reviewer's exact
  arrangement — one parent empty, the other a sibling.
* **Three facts had no teeth, and have them now.** Concurrent closes
  waited on a yield, so the exchange-outside-the-lock mutant passed on
  any run where the second thread had not been scheduled; the fact now
  observes the second caller BLOCKED before it asks. "Publish before
  close" was pinned by end state alone, which cannot tell the two orders
  apart; the close delegate now reads the slot at the moment it runs.
  And assertions ran on raw threads whose faults abort the host; they
  run on a worker that captures and rethrows. Each was proven the T1
  way — defect injected, fact failing, defect reverted — and each failed
  on the sentence written for it.
* **Smaller.** T1's I2 mutation replica admitted a RELEASED request,
  its predicate predating the third state; it reads production's
  admission now, and the schedule's arithmetic fact carries the released
  row. The projection is built outside the gate, the displayed ordinal
  is an index, and the row re-materialisation moved into
  `CanvasModelCopy` so I7's construction half keeps one site.

**T3 — the load pipeline, the projection, and teardown's sequence.**
Landed `CanvasLoadPipeline` and `ICanvasLoadSource`, the transforms the
operation needed — `WithUnloaded`, the acceptance writing Ready,
`CanvasLoadFailure` published with a release, the lease-less `Refuse`,
`Terminalize` — the population's subpath index, and the document's
rewiring: the handle, the generation counter and the FFI lock are GONE
from the view model. The lease owns the handle, the schedule is the
generation, and the lock is the lease's. The full unit suite —
1,706 tests, T3's battery included — passes on top of the rewiring,
which is the parity claim for a change of this size.

* **The reload order, decided — then corrected by the review.**
  UN-NAME FIRST, by the WORKER: the request publishes Loading and keeps
  the chain; the worker's first publication un-names the old lease and
  closes it, under its own lock, BEFORE the new open. One native handle
  at a time — the inherited load's rule and the frozen transition's own
  order — and a dispatcher never waits under a lease's lock. As first
  shipped, the REQUEST un-named, and the T3 review found the leak that
  buys: a worker the scheduler refuses to run — a shutdown landing
  between the request and the pool — left an un-named lease nothing
  could reach. Un-named by the worker instead, a dropped worker leaves
  the lease NAMED and teardown's terminal publication closes it; one
  fact pins that window, the order fact pins close-before-open on the
  source log, and the close-after-open mutation fails on its sentence.
* **U4's operation, fact by fact.** A delivery superseded after its
  build refuses and closes only its own handle; two concurrent
  deliveries, barriered so BOTH hold open handles before either reaches
  the gate, leave one publication and one closed lease — round 6's
  breaking arrangement, run as the plan demanded. Fault injection at
  the five points of the ownership window: a throw at the snapshot
  read, the rebase, the reseed or before the swap leaves the close
  observation at exactly one, with the failure and the terminal state
  published together; a throw after the swap leaves it at zero with the
  lease live and published — the rewrite of expected values the frozen
  plan said obligation I1 would demand.
* **The failure states are the release's.** A refusing or faulting
  delivery publishes its ParseError, Failed or RetargetAbsent state IN
  the swap that releases its request, so a document is never Failed
  under a request that could still be accepted; a request whose open
  itself threw is refused lease-less by the same terminal transform.
  The words stay the host's: the pipeline takes them through
  `ICanvasLoadSource`, which is also what lets the battery drive an
  in-memory source that faults and blocks on demand.
* **Teardown is U12's sequence.** The preterminal retired publication
  first; the base shutdown second — SEAM 3's ordering, pinned by a
  source-order fact because the window between two adjacent writes
  admits no barrier; the T4 seam guarded as inherited; the terminal
  publication in a finally, with `Terminalize` handing back the lease
  it un-named for its caller to close AFTER the publication. A delivery
  racing teardown is a fact in both orders. The workspace drain got
  strictly stronger: the close-drain now also awaits the tracked
  delivery whose finally closes a mid-flight handle — INV-2's claim
  made whole under the new ownership.
* **The projection is I3's load-path shape.** The view model's bindable
  surface is a PROJECTION of the publication, applied on the dispatcher
  by a posted effect that reads the slot ONCE at dispatch — a
  projection queued behind a newer load applies the newer load, and one
  arriving after retirement applies nothing. The rows are the view
  model's coherent past during a reload, which is presentation outside
  the chain, exactly where the design put it. I3 is NOT discharged by
  this; its ledger row says what remains.
* **The intents write the slot.** U11's fourth writer family arrives:
  selection, marks, surface and needle publish as they change, captured
  BEFORE the gate, because a transform reading the live selection would
  be obligation I4's forbidden ambient read. The rebase barriers are
  facts — a selection, a mark, a surface switch and a needle each
  arriving mid-load survive acceptance, the needle reseeding the new
  machine — and the mutation that rebases from a read that is not the
  decision snapshot fails on the selection sentence.
* **Mutations, the T1 way.** Close-after-open, injected: the order fact
  fails on the trace. A stale rebase, injected: the barrier fact fails
  with the rolled-back selection. A lease dropped without release: the
  close observation catches what a weak reference cannot. Each defect
  reverted byte-for-byte, each fact green after.

**The instruments and authorities T3 introduced, dispositioned.**
`CanvasLoadProbeForTests` is an INSTRUMENT carrying the codebase's own
test-seam suffix — the divergence T1's observer recorded does not
recur — and four of its points are open callouts under the gate, which
is exactly why production never constructs one and task T4's purity
census will treat it as it treats the observer. The view model's
`_applied` reference is PRESENTATION state: the last publication
projected onto the bindable surface, read and written on the dispatcher
only and consulted by no model boundary — the "installed unit" shape
the periphery ledger's T1 owns, named here so T4's authority census
meets it already dispositioned. The pipeline itself holds no mutable
state at all. The request a worker receives is a `CanvasLoadRequest`,
what its delivery did is a `CanvasLoadOutcome`, and the window's named
points are `CanvasLoadPoint` — bound here so the floor below has teeth
over T3's names.

**T3's review, and what it moved.** The correctness verifiers returned
four findings — two confirmed, two plausible — and refuted seven with
code-constructible evidence; the cleanup-side pass NEVER REPORTED and
is recorded here as not run, so T3's reuse and simplification angles
are unreviewed. All four addressed:

* **The displaced lease could leak.** The request un-named the old
  lease and handed the only reference to a worker the scheduler may
  silently drop after shutdown — teardown then found nothing to close,
  the handle leaked for the session's life, and the drain reported
  success. The un-name is the WORKER's first publication now: a dropped
  worker leaves the lease NAMED, and teardown closes it. The window has
  its fact, and the request-side un-name, re-injected, fails it.
* **A panic-class close fault escaped.** A panic out of the FFI is not
  a VaultException, and the reload's displaced close ran outside the
  delivery's catch: it could fault the tracked body, pin the tab on
  "Opening canvas…" forever and fault the teardown drain — the defect
  the inherited load's broad catch existed for, reintroduced by a
  narrow filter. The displaced close now sits inside the fault-mapped
  try, so a panic publishes Failed with the request's terminal state;
  the release's and the terminal close's guards catch everything
  survivable; and the body's finally posts the projection whatever the
  delivery did. The swallow-instead-of-map mutant fails the new fact.
* **The apply could clobber a newer selection.** The projection seated
  the acceptance-time resolution unconditionally, undoing a selection
  made between the swap and the posted apply. It re-seats only when the
  selection AS IT IS AT APPLY TIME did not survive the new rows — the
  inherited contract, which the workspace's own comment still states —
  with the rebase's resolution as the fallback.
* **A failure erased the durable selection intent.** The failure path's
  empty seat flowed back through the intent hook as a null
  `WithSelectedIntent`, although the model's own terminal transform
  deliberately preserves every intent. Apply-time seats no longer
  publish intents, and the end-to-end fact breaks a file, watches the
  failure clear the seat, restores the file, and finds the selection
  back.

**T6 — the filter machine, and the match off the dispatcher.** Landed
`CanvasFilterMachine` and `ICanvasFilterSource`: U9's total state
machine, driven, with the schedule and the unit AS the state — the
machine holds none of its own, because a private copy of the schedule
would be a second authority for the one question the publication
answers. The §C travelling rows land: the off-dispatcher match, the
failed-answer bit, and the four-branch summary.

* **Every event is one transform.** The keystroke publishes the needle
  intent, the `CanvasFilterSchedule` transition and — when the request starts at
  once — the pending unit, in one swap; the completion publishes the
  answer with the running entry retired, or DISCARDS it and promotes
  the queued request, because R's rows under Q's needle is the
  arrangement U9 exists to prevent; the non-running completion
  publishes nothing; the inactive needle retires both entries and
  widens, with the selection re-resolved from the durable intent. Every
  cell of U9's table is a fact, and starting a job is an EFFECT after a
  won swap, read from the outcome — predecessor against successor —
  never from a captured local.
* **The burst rule, priced.** Ten keystrokes pay for two matches — the
  one in flight and the last one standing — with the queue observable
  because the battery's runner releases jobs by hand. The lock-time
  revalidation is its other half: a job parked before its lock is
  abandoned BEFORE its own FFI call once superseded, and the fact
  parks one with a barrier and clears the needle over it. Both
  mutations are replicas in the battery: the completion that publishes
  over a queue returns R's needle on screen under Q's intent, and the
  job that answers admission without reading reaches the FFI for a
  dead request. Both were also INJECTED into production the T1 way —
  the queue-removed machine paid its burst with two matches of the
  same needle, the revalidation-removed job reached the FFI once where
  `CanvasFilterMachineTests` prices zero — and the restores are
  byte-for-byte.
* **The reseed's job.** Acceptance mints the request (task T3);
  the pipeline now hands it to the machine — an effect after that won
  swap, carried on `CanvasLoadAcceptance` with its needle rather than
  through a second read — and the machine's job answers against the
  new population. The needle typed before the load is answered after
  it, end to end.
* **The failed answer keeps its rows.** `Failed()` retained nothing,
  which would have widened the surface silently on a fault — the exact
  arrangement the inherited filter's comments forbid. It keeps the
  matched set and the filtered order now; only the `CanvasAnswerState`
  changes, which is the bit the surface needs for the honest sentence.
  A panic-class fault out of the match becomes that state rather than
  a faulted task.
* **The surface reads the applied unit.** The view model's filter view
  derives from the APPLIED publication — the same value its rows came
  from — so the count and the rows cannot disagree, which is C10's one
  invariant; the memoized match cache is gone with the synchronous
  query it memoized. The four-branch summary follows: a current answer
  counts, a previous answer still on screen counts, an in-flight first
  match renders nothing rather than a claim, and everything else is
  the state mapping's sentence. The count boundary says nothing while
  an answer is in flight — the completion's projection is the
  debounced count's moment — and in synchronous tests the machine runs
  inline, so every keystroke is answered before its setter returns and
  the inherited C10 facts pass unchanged.

**T6's instruments and authorities, dispositioned.** The machine holds
no mutable state; its runner is the document's tracked worker,
source-free, with the projection posted after every job.
`CanvasFilterProbeForTests` is an instrument with the test-seam suffix,
and none of its points is inside the gate. The applied-unit derivation
adds no authority: it reads the same `_applied` reference the load's
projection owns, on the dispatcher only.

**T6's review, and what it moved.** Six correctness findings and four
efficiency notes, every one verified against the source; the
cleanup-side pass NEVER REPORTED — the second review in a row — and is
recorded as not run, so T6's reuse and simplification angles are
unreviewed. All ten addressed:

* **The keystroke's publication was never applied.** The clear's widen
  and the pending unit had no job to post the projection, so the
  APPLIED reference went stale and a cleared filter's rows — and their
  count — resurfaced under the next needle's in-flight window: a
  retired needle's answer under the new one, U9's arrangement arriving
  through the view model's own bookkeeping. The keystroke applies its
  publication itself now; the seam fact pins the applied answer state
  across a clear, and the reverted apply, injected, fails it.
* **The failed answer had no sentence.** A match fault on a healthy
  Ready document announced and rendered "Reopening" — a lie about the
  canvas. The failed-answer bit speaks through the generic
  failed-action arm with the needle as its dynamic detail — one render
  for the count boundary and the summary both. CD-38's STOP recurs
  here and is recorded: a typed filter-failed reason is a core
  vocabulary change this task may not make.
* **The reload flash, again.** The reseed's pending unit was built over
  the unfiltered new population, so a reload under an active filter
  widened to every card until the reseeded answer landed — the flash
  the retired match cache's comment always warned about. The un-name
  outcome still holds the old unit, so the worker carries its matched
  set to the acceptance, which resolves it against the new graph as
  the pending unit's lingering rows. The fact pins the linger, and the
  carry, reverted, fails it.
* **The whitespace phantom.** Acceptance reseeded on a non-empty
  intent while the keystroke read the ACTIVE predicate, so a space in
  the field minted a request and a full-canvas FFI match on every
  reload. The predicate has ONE owner now — the machine — and the
  keystroke, the reseed and the surface all read it; the table below
  records the frozen cell this supersedes.
* **The strand.** A failed reload left the schedule as the request
  found it: a running entry whose job can never complete, every later
  keystroke queueing behind it — U9's dead-running-request shape
  arriving through a FAILURE rather than a missing reseed. The
  terminal transition retires both entries now, the needle INTENT
  survives for the next load, and the fact fails with the retirement
  reverted.
* **The quieted tripwire.** The lease's admitted-while-closed detector
  threw a plain invalid-operation exception, and the filter job's
  survivable catch dressed an invariant breach as a failed match.
  `CanvasLeaseViolationException` exists so every survivable filter in
  the model can exclude it BY NAME, and the fact asserts the breach
  propagates instead of publishing.
* **Four efficiency notes, taken.** The filter view walks the unit's
  own ordered answer through the row index — O(matched) per read; the
  count boundary checks the in-flight bit before building a view it
  would discard; the completion skips building an answer a queued
  request guarantees it will throw away, with a detector arm where the
  impossible would hide; and PENDING successors no longer rebuild the
  surfaces over identical rows.

**Two dispositions with it.** `AppliedFilterAnswerForTests` is a test
seam by name, over presentation state the clear-then-retype fact can
reach no other way. And `CanvasLeaseViolationException` is no authority
at all — it is the tripwire's TYPE, which is what makes "no catch may
absorb it" a clause an exception filter can spell.

**T4 — the censuses.** Landed `CanvasModelCensus`: U13's arms at this
PR's scale — the task every "structural rather than defensive" note in
this record has pointed at since T1.

* **The writer arm (I8).** One writable `CanvasPublication`-typed
  field in the model — `CanvasPublicationSlot`'s own — proven by
  REFLECTION over the closed world's types; one seed
  assignment and one compare-and-swap, proven at SOURCE; the field's
  token absent from every other model file; no model type opting into
  serialization. T1's bypass detector is structural now — the census
  proves there is no ordinary write for it to miss — and the ledger
  records the reflection-shaped remainder as the narrowed claim.
* **The purity arm (I4).** Every `.Publish(` transform region in the
  model and the document — twelve, floored at ten — walked for the
  finding's own impurity list, the probes stripped by name as
  dispositioned instruments. One level deep, and the ledger's status
  note says exactly that.
* **The aliasing wall (I7).** No group-path write anywhere in the
  shell; one re-materialisation site; collections built only at the
  two sanctioned files. The owned-row alternative is recorded as not
  taken.
* **The authority census, TWO-WAY.** The derivation walks every field
  of every model type — auto-property backings, event delegates and
  statics included — and its findings must match the record's
  dispositions exactly in both directions: an undispositioned
  authority fails, and so does a disposition whose authority is gone,
  so this ledger cannot drift from the code either way. The premise
  plants round 5's five shapes — the mutable static, the event, the
  settable auto-property, the readonly-but-mutable contents, and the
  read-only interface over a caller-retained list that beat the
  four-shape battery — and the derivation finds all five. Its doctrine
  is U5's: an authority is where mutable state LIVES, and a published
  value reaches mutability only through the lease it NAMES, which is
  the currency fact itself.
* **The carry-forward manifest (U4), and the minting walls.** Every
  publication member classified in a table keyed by reflection, so a
  new member fails BY NAME until the rebase accounts for it — round
  5's short list, made a gate. And the call-site half of "the writer
  set is closed by construction": a `CanvasHandleLease` is minted only
  by `CanvasLoadPipeline`, a population only by the pipeline and its
  own Empty, and the chain and terminal transforms are reached only by
  their owners.

The derivation's first run earned its keep twice before it went green:
it found the chain's lease reference before any disposition named it —
now the U5 disposition above — and it found the lease's FFI lock,
which taught the lock predicate that the model names its locks two
ways. A two-way census that catches its own author on day one is the
census working.

**T4's review, and what it moved.** The review's verification pass
itself did NOT complete — of sixteen verifiers only two returned, the
third stall in three rounds — so its output was two CONFIRMED findings,
one refutation, and eight recall-biased candidates it marked
unverified. Every one of the eight was then verified HERE, against the
census at the commit, and held. All ten addressed, mostly by one move:

* **The census was rebuilt on the house syntax-tree helper** — the
  confirmed lead finding, and the sharpest kind of irony: the helper's
  own doc comment records that #741 spent four review rounds proving a
  regex cannot tell live code from dead text, and this census had
  re-derived string matching anyway. Every source arm now reads
  SYNTAX: the writer arm counts assignment and invocation NODES, the
  walls count creation and invocation nodes, and a comment or a string
  literal can neither trip an arm nor hide from one — proven both ways
  by injection, with a planted comment naming the slot's field leaving
  the walls green.
* **The aliasing wall closes what the regex missed.** Assignment nodes
  cover every compound kind at once, increments and ref-captures are
  their own node shapes, and the alias-through-a-local bypass is chased
  with the helper's own Resolve — both planted writes fail the wall.
* **The purity arm walks the WHOLE shell** rather than a named file
  list, so a publish site added anywhere is scanned the day it
  appears; the probes stay sanctioned structurally, because Reached is
  simply not a forbidden name, and the line-granular carve-out that
  could hide an impurity beside a probe call is gone.
* **The closed world is closed both ways.** Every type the nine files
  declare is reflected over or censused as stateless, and every census
  entry must still be declared — the cross-check found
  `CanvasLeaseRelease` missing on its first run.
* **The manifest gained its static and field walls** — no static
  properties on the publication (a cached instance is I5's shape; Seed
  is a METHOD that allocates per call), no bare fields — and the
  serialization arm walks MEMBER attributes too, skipping the
  compiler's own [Serializable] closure classes, which its first run
  caught stamping the transfer's lambdas.
* **One parse per run, obj/ and bin/ excluded** — the second confirmed
  finding: three private copies of the shell walk, one of them re-run
  per theory row over a tree that was 88% build artifacts, replaced by
  one memoized parse the way the sibling censuses do it.

**What the walls still narrow, recorded:** the creation walls count
explicit `new T(…)` nodes — a target-typed `new(…)` behind a declared
variable is invisible to a syntax-only census and waits on the
compiler-grade walk I4's note already records as not taken; and the
authority derivation's lock exemption is named fields of type object,
which the five-shape premise cannot probe.

**Frozen paragraphs the gate SUPERSEDES.** The freeze forbids revising
them and that is right; this record is the sanctioned place to say which
of them the implementation has overtaken, so a T2, T3 or T6 owner does
not read a reclassified clause as current.

| Frozen text | Status after T1 |
|---|---|
| U11's "re-decide on failure" clause | SUPERSEDED. U11's other clauses — one publish helper, read once, decide from that snapshot, swap with it as the expected value — are satisfied and still bind. Only the re-decision is gone, and U11 carries no obligation marker, so nothing else in the section says so |
| The verification plan's "a losing attempt publishes nothing and starts nothing" and "a re-decision after a lost swap refuses when its request has been consumed" | UNREPRESENTABLE, both. There are no losing attempts and no re-decisions to write a fact about. The claims they were protecting are carried instead by the I2 mutation, where losing attempts still exist |
| U12's "both publications are compare-and-swaps, so they retry until they win" | SUPERSEDED. They are swaps and they win on the first attempt |
| U12's delivery-versus-teardown walk — "the delivery's swap fails, its re-decision reads retired, it refuses" | SAME OUTCOME, DIFFERENT MECHANISM. The delivery refuses at DECISION time, having read the retired snapshot inside the gate. Recorded here rather than only in a task report, because T2's owner will look here |

**Frozen paragraphs T2's ownership transfer supersedes**, in the same
table's terms, because the finally table and D1's load-result rows are
where a T3 owner will go to build the delivery pipeline:

| Frozen text | Status after T2 |
|---|---|
| The finally table's one guard — "close unless the live publication names this lease, evaluated once in the finally, against the slot as it is at that moment" | SUPERSEDED. The guard is a TRANSFORM, `CanvasLeaseTransfer`'s release, not an observation: it decides inside the gate and publishes a terminal state for its request BEFORE it closes. That table's "the slot still does not name it" cells are true by construction now, rather than the claim I1 said the code had to make true |
| The same table's "a LOST swap leaves the result owning its lease so the re-decision or the finally closes it" | UNREPRESENTABLE, for T1's reason: there are no lost swaps and no re-decisions. The finally still closes, and it is the only thing that does |
| D1's "Load result — REFUSED: the acceptance check rejects it, on first decision or after a lost swap … closes its own lease exactly once, guarded by 'unless the live publication names it'" | SAME OUTCOME, DIFFERENT MECHANISM. The lease closes exactly once and a lease the publication names is never closed; but the guard is the terminal publication rather than a read, and "after a lost swap" is the unrepresentable branch above |
| D1's "Load request: superseded in the load schedule, or marked consumed by an accepting publication" | EXTENDED. A third terminal state, RELEASED, is published by a refusing or faulting delivery, and a redelivery reads it and refuses exactly as it reads consumed. `CanvasLoadDelivery` is the enumeration; the bool T1 shipped had nowhere to put it |
| The acceptance transition's "publish the terminal state for the old lease and population; close the old handle under its lock; construct the new lease and population off-thread; … install in ONE SWAP" | EITHER ORDER, after the review. The acceptance closes whatever lease its swap displaced, from the decision snapshot, so the one-swap order leaks nothing; the un-name-first order stays T3's to choose, and under it nothing is displaced. "No live publication ever names a closed handle" holds in both |


**Frozen paragraphs T3's pipeline supersedes**, in the same terms:

| Frozen text | Status after T3 |
|---|---|
| U4's "one decision read PER ATTEMPT plus one final ownership read … discharged in a finally block wrapping the whole delivery" | SAME OBLIGATION, FEWER READS. There are no attempts to count and no final ownership read: the decision is one snapshot inside the gate, and the release's transform reads the slot inside the same gate. The finally remains, and it is where the release lives |
| The finally table's "a LOST swap … the re-decision or the finally closes it" as it reaches the delivery — "the delivery re-reads and re-decides, and by then its request is consumed, superseded, or the document is retired, so it refuses" | UNREPRESENTABLE — T1's reason reaching the delivery. A delivery decides once; the three refusal arms are unchanged, read from the one decision snapshot |
| D1's "Load result — FAULT before transfer … the delivery's finally closes it under the same guard" | SAME OUTCOME, DIFFERENT MECHANISM. The finally closes through the release, which publishes the failure state and the terminal state FIRST — T2's publication-not-observation row, now driven by a real pipeline |
| The verification plan's "two load workers … barriered so both read the same snapshot before either swaps" | UNREPRESENTABLE under the gate: two transforms cannot hold one snapshot concurrently. The arrangement's teeth survive as the BUILT barrier — both workers hold open handles before either reaches the gate — and the assertion is unchanged: one publication, one closed lease, a close observation totalling one |

**Frozen paragraphs T6's machine supersedes**, in the same terms:

| Frozen text | Status after T6 |
|---|---|
| U9's "Every cell is a compare-and-swap, decided from one snapshot and retried on failure, which is what makes the machine total across threads" | SAME TOTALITY, NO RETRIES — T1's reason reaching the machine. Every cell is a transform under the gate; a cell that loses does not exist, so totality across threads is the gate's property rather than a retry loop's |
| §C's "contract C10 interim" — the synchronous match, "no in-flight frame to describe", the two-branch summary | RETIRED. The match runs off the dispatcher, the in-flight frame exists and renders the previous answer as a coherent past, and the summary has the four branches the travelling row said only an async match creates |
| U9's load-acceptance cell — "publish *(K', none)* and start K' when the rebased needle is NON-EMPTY" | SUPERSEDED BY ITS OWN MACHINE'S PREDICATE (the T6 review): non-empty let a whitespace needle mint a phantom job the keystroke column calls inactive. The reseed reads the one ACTIVE predicate the keystroke reads |
**The four mutable authorities T1 introduced, with their
dispositions.** The frozen rule counts identity and contents
separately and says every authority the derivation finds must appear in
the reconciliation table marked absorbed, seam, source-free or removed —
so four undispositioned authorities would be four findings waiting for
T4. None is CURRENCY, so the ratified invariant is intact; what was
missing was writing them down.

| Authority | Kind | Disposition |
|---|---|---|
| `CanvasPublicationSlot`'s publishing flag | IDENTITY — a reassigned bool | **NON-CURRENCY OPERATIONAL STATE**, alongside the lease's close-once record. Read and written only under the gate, and only to refuse a reentrant transform; no boundary consults it for admission, validation or delivery |
| `CanvasPublicationInstallObserver`'s seen-set | CONTENTS — a readonly field holding a mutable set | **INSTRUMENT.** Not attached in production, and the slot behaves identically with and without it. It is round 5's blocker-2 shape by construction, which is why it is dispositioned rather than exempted |
| The same observer's install and repeat counters | IDENTITY | **INSTRUMENT**, as above |

**A doctrine divergence, recorded rather than left for T4 to find.**
This codebase's test-seam guard is naming — the two dozen members whose
suffix tells a reviewer at a glance that production must not call them.
The observer is a production-visible instrument with no such suffix and
no census arm enforcing one. The divergence is deliberate: the
verification plan sanctions observation as an instrument CLASS, and a
named type documents itself better than a suffix does. But T1's test
file says the shipped slot carries no test seam at all, and that is true
of the mutants and not of the observer parameter, so the claim is
narrowed here.

**A wording collision the binding created.** The bound objects paragraph
reads "**The PUBLICATION** — `CanvasPublication` — is an immutable
record". "Record" was English when the name beside it was plain text;
next to a citation a reader may take it for the C# keyword, and the
decision T1 made was specifically NOT a record. The freeze forbids
editing the sentence; this is the note that stops a later reader
resolving it the wrong way.

**Teardown's starvation has one mutation, deliberately.** The finding
gives retirement its own sentence and the positive fact covers it; the
mutation does not, because retirement and delivery run through the same
loop and a second mutant would exercise the same mechanism twice. The
decision is recorded rather than left to look like an omission.

**Reentrancy and U12's "or reenters" do not collide.** U12 step 4
guarantees terminal publication even if the T4 seam throws or reenters;
that reentrancy is between publish calls, not inside a transform.
Effects follow a WON swap and therefore run with the gate released, so
an effect that publishes makes a sequential second call rather than a
reentrant one. The refusal's scope and U12's anticipation are disjoint.



**The cleanup pass, run at last.** Four review angles — reuse,
simplification, efficiency, altitude — over the whole branch diff, after
the last task and before the push, applied where taking the finding did
not move a ratified behaviour. What changed, by owner:

- **The survivable-fault predicate has one home.** Four catch filters
  said "not OOM, not stack overflow, not access violation, not the
  lease's own tripwire" in four places; `CanvasFaults.Survivable` says
  it once, beside `CanvasHandleLease` whose
  `CanvasLeaseViolationException` it must never swallow. The censuses
  admit the type; its wall is the existing statics discipline.
- **The population derives what it was being told.**
  `CanvasPopulation` counted its preserved rows in a constructor
  parameter AND carried the warnings the count comes from;
  `PreservedCount` is now derived from the skipped-entry warnings and
  the parameter is gone, so the two can no longer disagree. The unused
  `Subpath` accessor went with it — `Subpaths` the index stays, and
  `CanvasDocumentViewModel` now ALIASES that immutable index instead of
  copying it row by row on every publish, capturing
  `PreservedItemCount` once per load rather than recounting per
  binding read.
- **The unit carries the fact the surfaces were reconstructing.**
  `CanvasProjectionUnit` gains `Narrowed` — set by a landed answer,
  carried through pending and failed successors, cleared by the
  unfiltered projection — so the lingering-answer question is read,
  not re-derived from set-size arithmetic that misread a
  match-everything answer. `WithResolvedSelection` and the transitions
  carry it; the transform-allocates rule (I5) is untouched.
- **One verdict, one sentence.** `CanvasDocumentViewModel` computes a
  single `CanvasFilterVerdict`; the view, the count boundary and the
  summary consume it, and `FilterAnswerInFlight` is now a reading of it
  rather than a fourth ladder. `CanvasNavigator` composes the filter's
  status sentence once, in `FilterStatusSentence`, where the count
  boundary announces what the summary renders. The derived
  `CanvasFilterView` is memoized per applied publication and needle —
  a VM-side cache of a pure function, not a publication cache.
  `ReadRefusalFor` reads BOTH halves from the applied snapshot, so the
  sentence cannot be a torn pair mid-reload.
- **Work moved off the gate.** `CanvasFilterMachine.Typed` derives
  activity from the needle instead of being told twice; the machine and
  `CanvasLeaseTransfer` pre-build the widened and lingering projections
  before entering the gate and reuse them when the decision snapshot
  still matches. `CanvasPublication.WithMarkedIntent` gains the
  already-copied overload so the marked-intent publisher copies outside
  the gate too.
- **The outline view asks the population.** `CanvasOutlineView`
  carried a line-for-line copy of the TRUE-ancestry walk — with the
  depth-gap fix only in the copy that documented it. It now reads
  `AppliedPopulation` and asks `Parent`, the one derivation the
  population owns.
- **The batteries share their arrangements.** `CanvasModelFixtures`
  owns the flat and depth-shaped populations three batteries built by
  hand; `CanvasFakeLoadSource` is the one in-memory load source (the
  pipeline battery's rich fake, promoted; the machine battery's
  `ReloadSource` deleted); `CanvasWorker` lives under `Support/`; the
  lease battery's hand-rolled yield loop is `SpinWait.SpinUntil`. The
  model census folds its two wall theories into one `AssertWall` loop.

**Findings taken as notes, not edits — each with its reason:**

- The load and filter schedules' running/queued pairs look mergeable
  into request objects; the pair layout is the frozen design's and the
  swap-width argument lives there. Not a cleanup-pass call.
- `CanvasHandleLease.Invoke` re-reads the slot for its admission
  predicate; the re-read IS the contract (admission against the slot as
  it is), not a redundancy.
- The view model's `_rows` and `_targets` shadow indexes duplicate
  population lookups; they are T5's surface and move with it.
- The per-keystroke outline rebuild behind `OutlinePublished` is
  periphery cost the obligations ledger already carries.
- `DisplayedOrdinal` and the population members only the future
  presentation reads are the ratified API surface, kept.
- The slot battery's raw contender threads never assert, so
  `CanvasWorker`'s fault capture buys nothing there; left as committed.

**The reader-coherence premise, established rather than hoped (CI
round 1).** `ReadersUnderContentionNeverSeeAMixedPublication` fixed its
writer at five thousand publishes and HOPED the reader overlapped; the
runner's scheduler declined, and the premise failed with its own
sentence. The fix is the victim fact's dual-leg rule applied to its
sibling: the writer runs until the churn floor and the reader's overlap
proof both hold, inside the same liveness budget. The whitespace round
before it was the format gate refusing the cleanup pass's scripted line
endings — `dotnet format` is part of the push, not an afterthought.
## PR D — the visual renderer, the viewport, text scaling, and the color contract (FROZEN as the ratified baseline by the owner’s ruling; the presentation commit’s remaining questions are the ID-ledger, discharged by code)

**Goal (spec §PR D).** The visual projection: per-card UIA elements over
a windowed drawing surface, the viewport command set, Windows
text-scaling parity, and the canvas color contract. **Read-only** — no
mutation reaches this PR; every verb it answers is a movement, a
selection, or a view command. It is the third projection arm beside the
outline and the table, and the first surface BORN under the C-unit
model rather than rewired onto it.


### THE FREEZE — read this first

**This section is CLOSED to further prose revision**, frozen at
revision 4 after four adversarial rounds, by the owner's ruling of
2026-08-31. The trajectory — 6, 5, 5, 3 blockers, with the fourth
round's blockers concentrated where the first round's were — is the
C-unit signature: the remaining questions are ordering, lifetime and
inventory questions that every prose answer re-opened, and the ruling
is the same one that closed C-unit: **design by implementation.**
Nothing below is to be re-argued on paper; the next thing that
changes any of it is code, through the task loop, with facts and
mutations arbitrating.

**What is RATIFIED, and is therefore the architecture to build.**
Everything this section states that round 4 did not name in a
finding — including, ratified WHOLE by round 4 itself: D2's scene as
a field of the population behind the deep-copy wall, with the
failure pane and the load budgets; and D5's global namespace, core's
rendered traversal phrase for edge names, the end-style table over
the confirmed independent arrow booleans, and the skip-occupied
ordinal loop. The read-only scope, the two-authority direction of
D1, the three-cell peer table's shape, the origin-sensitive pan, the
dimming filter, the doors of D6, the pattern matrix, the transient
order, the literal color table, the derived inventories, the
budgets, and every DD decision stand as the baseline.

**What is RECLASSIFIED.** Round 4's nine findings stop being prose
questions and become the §D IMPLEMENTATION OBLIGATIONS — ID-1
through ID-9 below, each carrying the finding VERBATIM (backticks
and links flattened, the periphery ledger's convention) as its
acceptance criteria, discharged by code, facts and mutation
batteries through the implementation gauntlet, not by another
revision of this text. Where an obligation's remedy sets a
DIRECTION, the direction binds unless the code proves it wrong, and
the round record — not this section's contracts — is where such a
proof lands.
### Contracts

**D1 — TWO authorities, ONE dispatcher turn, and events follow the
commit.** Round 3 dissolved revision 3's race by naming what the
renderer actually reads: two dispatcher-committed authorities — the
document's APPLIED publication (committed by the apply, on the
dispatcher, never by the slot's worker-thread swap) and the
renderer's VIEWPORT value (zoom, pan, view size — a small immutable
value each viewport command commits on the dispatcher BEFORE any
build is queued, so no build ever owns a viewport delta and a
discarded build cannot swallow a zoom; round 3, blocker 2). A
presentation BUILD derives pure over a snapshot of the pair — the
expensive derivation may run off-thread — and its COMMIT is one
dispatcher turn: re-read both current authorities, install only if
the snapshot is still current, else yield to the freshest queued
build; and because applies, viewport commits and presentation
commits all mutate their authorities ON the dispatcher, the
check-and-swap is same-thread-linearized and the TOCTOU window of
round 3's blocker 1 does not exist — there is no thread on which an
authority can move between the final check and the swap. The
installed state carries the render source (D2), the peer topology
(D3's descriptor index, materialized set and tombstones), and the
DPI, text-scale and theme revisions; selection and filter state are
DERIVED accessors over the render source's unit (round 3, blocker
3 — not carried twice). UIA property and structure events raise
after the install, from the old/new pair; peers read the installed
state and nothing else. The barrier facts cover BOTH orders:
publication C landing during B's build, and a viewport commit
landing during a publication build. The viewport constants: zoom
clamped 0.1–4.0 in steps of 1.25, centre-preserving, fit padding 40
logical pixels (120 for zoom-to-selection), follow-selection default
ON. The zoom announcement and the container Value ride the commit
that first carries the new viewport revision, so the spoken and
reported percentages cannot disagree. This is the periphery T1
remedy taken for this surface at birth; the row stays open for the
surfaces that predate the model.

**D2 — The scene rides the population, and the pair is
unrepresentable apart.** Round 3's blocker 3 refused revision 3's
envelope for duplicating the chain the model already binds, and the
refusal is adopted: there is no third carrier. The load pipeline —
while it holds the open handle — DEEP-COPIES the FFI scene into a
sealed, host-owned, immutable scene value (I7's copy-not-alias wall
extended; no FFI collection is stored or exposed; the construction
census gains the scene's wall) and the scene is a FIELD OF
`CanvasPopulation`, constructed with it, immutable ever after — so
population and scene cannot disagree because they are one object,
and population, unit and their binding stay exactly where the
ratified chain put them: `CanvasLoaded`. The renderer's render
source IS the applied publication's chain; no §D type re-carries
it. The scene's algebra: every publication transform allocates a
fresh publication (I5 unchanged) while the population — scene
included — is load-class state, allocated once per load and
deliberately retained by identity across same-population
successors. A malformed successor is a RENDERER FAILURE STATE: the
visual arm shows its failure pane — message, not stale rows; a
reader is never left interacting with a canvas the document no
longer reports (round 3, blocker 3's tail). This extends C-unit
CODE through the implementation loop with its own facts and census
rows — the sanctioned direction.

**D3 — Peers: a THREE-cell state table, discriminated identity, and
retirement said out loud.** The container peer is a Group/Pane named
"Canvas visual view", exposing Selection, the item-container pattern,
and a READ-ONLY value provider whose Value is "Zoom N percent"
(DD-5), its property-change event raised from D1's old/new pair.
Peer IDENTITY is scoped and DISCRIMINATED: (renderer instance,
document, CARD node id) for cards, (renderer instance, document,
EDGE id) for labelled edges — round 3, major 8: a node-only key
cannot tell twin parallel edges apart. Two panes hold two peers for
one card; closing a pane invalidates only its own. The state table
has THREE cells (round 3, major 4), and the installed state backs
every one: UNREALIZED — no peer object exists yet; the installed
state carries a population-wide immutable DESCRIPTOR INDEX (id,
name, kind, geometry from the population's scene), so a first-touch
find-by-property answers from the index and mints the placeholder
without mutating outside the state; PLACEHOLDER — a peer whose card
or edge is outside the window, or was retained across
dematerialization: virtualized-item pattern ALWAYS, identifying
properties from its descriptor, action patterns unavailable until
realization; MATERIALIZED — the full peer: Button control type for
cards, Name per D5, Invoke, SelectionItem, screen-coordinate
rectangle recomputed on every pan, zoom and resize. REALIZATION
atomically promotes the same peer object; a peer whose card left
the document or whose pane closed refuses with the platform's
element-not-available answer. RETIREMENT is a rule, not a leak
(round 3, major 7): peer identities commit only with a WINNING
install — a discarded build's topology work is discarded whole; the
registry holds weak identity only; a tombstone is carried forward
only while its peer is materialized-adjacent or externally live,
and is pruned otherwise, so state size tracks the window and the
live client references, not every card ever seen. EDGES window like
cards (round 3, major 8's tail — the spec's "visible" qualifier
restored): a labelled edge materializes when either endpoint is in
the window, is a placeholder when retained off-window, and follows
the same three cells. Structure changes raise the container's
children-invalidated event once per installed state.

**D4 — Windowing, the never-dead-end rule, whose pan it is, and the
filter DIMS.** Cards are materialized for the viewport plus one
viewport's margin. Keyboard navigation, selection calls and
virtualized realization past the materialized edge are answered,
not refused: the target's peer realizes, the viewport pans to
contain it, the pan materializes the next window. The pan rule is
ORIGIN-SENSITIVE (mac's rule, pinned): a selection made ON this
surface — keyboard, Invoke, SelectionItem, realization — always
scrolls into view, toggle or no toggle (2.4.11); a selection
arriving from ANOTHER surface pans only while follow-selection is
ON, so the toggle's sentence is true exactly when it speaks. And
the filter NARROWS NOTHING HERE: the visual arm renders the FULL
scene and keeps every card's peer; the applied unit supplies match
state, and a filtered-out card draws DIMMED, hit-tests, and reports
its unmatched state through the peer — the row inventory never
shrinks. The pinning facts run the 2,000-node fixture, walk
selection off the materialized edge, assert both toggle states
against both origins, and drive the filter fixture through drawing,
hit-testing and UIA.

**D5 — One name namespace, core's phrase for edges, and the end-style
table.** Name uniqueness is GLOBAL across the container, every card
and every labelled edge. Cards take core's speakable name; the
container's name is reserved. An EDGE'S name is core's rendered
traversal phrase — the same sentence the connection-walk announces,
INCLUDING the other endpoint (round 3, major 9: a host-composed
"source, connects-to, label" both dropped the destination and
re-derived core grammar; the rendered phrase is core's own, so
§W-G stays clean and two same-label edges from one card to two
destinations differ by name before any ordinal). Residual
collisions in any class — including a card whose given title is a
pre-suffixed spelling of another name (round 3, major 10) — are
resolved by the SKIP-OCCUPIED loop core's own naming uses: try the
next ordinal until the spelling is free, so an occupied "…2" never
double-allocates. The edge peer's INVOKE target is total over the
shipped edge algebra (round 3, blocker 5), as a literal table over
the end-style pair, selection-independent, every cell pinned:

| fromArrow | toArrow | Invoke selects |
|---|---|---|
| false | true | the to-node (the arrow's head) |
| true | false | the from-node (the arrow's head) |
| false | false | the to-node (undirected: document order) |
| true | true | the to-node (bidirectional: document order) |

The uniqueness fact enumerates every peer on a fixture built to
collide four ways: twin card titles, twin parallel same-label
edges, a card named after the container, and a card pre-named with
another card's ordinal suffix.

**D6 — Selection: one committed value, its doors, and the machine
owns the transition.** The surface display authority is
`CanvasSelection`; the publication carries selection INTENT; the
applied unit carries the REBASED resolution. Round 3's blocker 6
caught revision 3's freestanding selection publication racing the
filter machine's completion, so the transition moves WHERE THE
MODEL PUTS TRANSITIONS: selection publication is a total event of
`CanvasFilterMachine`'s state machine — one transform carrying the
selected intent and the current resolution together — and the
running match's completion, which already revalidates inside the
gate, CARRIES FORWARD the current unit's resolved selection instead
of the one it captured at start, the same carry-forward shape
`CanvasLeaseTransfer` already ratified for reseeds. A completion
therefore neither strands the schedule nor overwrites a selection
that landed mid-match; the fact publishes a selection between a
match's start and its landing and asserts both survivals. The
doors are unchanged from revision 3: ANNOUNCED selection — Invoke,
SelectionItem.Select, a keyboard move, an edge follow — through the
document's one announced entry point (write the surface authority,
publish the machine's selection event, narrate); SILENT seating —
automation focus without a user act — through the silent twin,
narrating nothing; both commit before the next presentation state
installs. The PATTERN MATRIX stands as ratified in round 2's answer:
single selection — CanSelectMultiple false, IsSelectionRequired
false; Select replaces; AddToSelection on an unselected card while
another is selected THROWS the platform's invalid-operation answer
(marks are §G's vocabulary, not this pattern's); AddToSelection on
the selected card and RemoveFromSelection on an unselected one are
no-ops; RemoveFromSelection on the selected card clears it,
announced; realization does not select; automation focus without
selection is the silent door. Every cell is a fact.

**D7 — Every operation is addressed to ITS surface, and the no-pane
answer is spoken.** A peer belongs to a concrete renderer view in a
concrete pane; Invoke, Select, realization and edge-follow carry
their view's presenter identity from construction, and the focus or
pan they cause is delivered to THAT pane. Viewport COMMANDS resolve
their pane in two clauses — the INITIATING renderer when the canvas
surface has the keys, else the LAST-OWNING renderer for the
addressed document — and round 3's major 11 named the third case
this rule owed: C2 permits the last owner to be ABSENT (a restored,
never-focused tab), and a palette invocation initiates from no
renderer. The verb then answers with the canonical refusal through
C4's mapping — the no-pane sentence — rather than acting nowhere or
silently. And the honesty that finding demanded: the last-owner
clause CONSUMES the inherited presenter authority, so §D's
read-only slice DOES depend on the periphery's T5 row — recorded in
D17, not waved away. A retained peer whose pane closed refuses with
element-not-available (D3).

**D8 — Focus visibility is screen-space and measured.** The
selection ring draws in a screen-space overlay at minimum 2
device-independent pixels at ANY zoom. Keyboard focus on the view
and card selection are visually distinct. Ring contrast is
APCA-measured against every preset fill, the group fill and the
canvas background, in both appearances, as
`ThemeTokenContrastTests` rows.

**D9 — Hit-testing and z-order are core's order, consumed.** Topmost
by DOCUMENT order (t1's tiebreak); groups behind their members
(#960's insertion rule). The renderer derives z from core's order
and never re-sorts.

**D10 — Transforms are INSTANT in every mode, and the zoom still
speaks.** Round 3's major 13 found the animated case unowned, and
the answer is the simple ruling (DD-7): viewport transforms are
state-jumps in ALL modes — the next installed presentation state
carries the final geometry, peers, hit-testing and the ring agree
with it in the same commit, and no intermediate frame is ever an
installed state. Reduce Motion therefore changes nothing
structural — there is no tween to suppress — and any visual easing
is forbidden rather than specified. The announcement contract:
the percentage speaks and the container Value updates in the
commit, because a reader who cannot see the motion is the reader
the sentence is for.

**D11 — Text scaling: one factor, one owner, every run censused, and
the tooltip's full contract.** Card titles (base 12), edge chips
(base 10) and group labels multiply the text-scale factor by the
zoom — mac's scaled-label parity. WPF binds none of this, so §D
names the owner: ONE app-level text-scale service reads the
accessibility registry value, subscribes to the system
preference-change event, marshals to the dispatcher, and is
disposable — subscribers detach when a pane closes, the W1-1
reactive shape including its unsubscribe half. The renderer
consumes the service through the presentation state's text-scale
revision. A census walks every canvas text run and refuses one that
ignores the factor. Chrome outside the canvas has NO scaling source
today: the canvas surface's own chrome consumes the same service in
this PR, the checklist verifies no clipping at 225 % across every
element the canvas tab shows, and shell-wide chrome beyond the
canvas tab is recorded as out of §D's scope rather than claimed.
Labels truncate visually with the FULL text in the peer Name and a
tooltip whose contract is complete: keyboard FOCUS on the truncated
card SUMMONS it, and it meets all three 1.4.13 conditions —
DISMISSIBLE (Esc, through D12's rung), HOVERABLE (the pointer
travels from label to tooltip without it vanishing), PERSISTENT (it
stays until dismissed, hover leaves, or the content stops being
valid). Focus-open, Esc-dismiss, pointer traversal and persistence
are four separate facts.

**D12 — The tooltip is the surface's, CD-47 stays the one special
case, and the intra-surface order is executable.** C6 and t0 M5
order Escape as mode, then filter, then the surface's own
transients; CD-47's open-panel pre-emption is the ONE recorded
exception. The tooltip takes no pre-ladder slot: it lives in C6's
surface rung beside the interim card detail. Round 3's minor 16
asked for the order INSIDE the rung, and it is literal (DD-3): the
tooltip is dismissed first, then the interim card detail, then the
filter seating — most-transient first, one dismissal per press —
and the arrangement where the tooltip is open on the Where-am-I
panel's own trigger card is pinned: the OPEN PANEL pre-empts
(CD-47, open-state not focus-state), the tooltip goes with the
panel's dismissal turn only if Esc reaches the surface rung with
both open, tooltip first.

**D13 — The color contract, as a literal table, measured everywhere
it draws.** Slate-owned token keys in ALL THREE dictionaries; dark
and light fills are tint 0.18 composited over the opaque surface
token (mac's palette method), keeping text at Lc ≥ 75 on every
preset AND on a hostile raw-hex sample in both appearances.
`ThemeTokenContrastTests` rows: fill×text for the six fills, the
GROUP fill and the hex sample; ring×fill for the six fills AND the
group fill (round 3, major 12 — a selected group is an ordinary
arrangement, not an exotic one); ring×background; both appearances;
and the Contrast census derives from the literal table:

| Token | Contrast maps to |
|---|---|
| Fill1..Fill6 | the window brush |
| GroupFill | the window brush |
| Border1..Border6 | the window-text brush |
| Edge | the window-text brush |
| Text | the window-text brush |
| SelectionRing | the HIGHLIGHT brush (DD-6) |

Meaning never lives in color alone — the color NAME travels in the
outline, table and Where-am-I sentences. Runtime switches re-render
through `ThemeManager`'s resources-changed notification, via the
same disposable-subscriber shape as D11's service. Evidence: the
automated rows plus the recorded manual Contrast check across the
four built-in schemes and one customized scheme; user-customized
colors are not APCA-gated.

**D14 — The command rows, the read gate DERIVED FROM THE REGISTRAR,
and the enablement census EXECUTABLE.** Three chord rows — zoom in
Ctrl+=, zoom out Ctrl+-, actual size Ctrl+0 — scope Canvas,
focus-routed as mac routes them; fit canvas Shift+1 and zoom to
selection Shift+2 are VISUAL SURFACE ONLY (R2); all five resolve
their PANE per D7, including D7's spoken no-pane refusal. Round 3's
major 14 caught the inventory gap: the shipped C4 fact reads public
`CanvasNavigator` members, and pane-owned viewport verbs could live
elsewhere and never appear. So the C4 mapping's membership derives
from the REGISTERED canvas command surface — the registrar's canvas
rows — with each row bound to its production delivery member, so a
verb cannot register without a row and cannot deliver except
through the member its row names. The data-dependent arms are
enumerated: zoom to selection with NO selection answers the
canonical no-selection sentence; fit on an empty canvas answers; a
retained-peer operation on a closed pane refuses per D3; the
no-pane palette invocation refuses per D7. The enablement sweep is
an EXECUTABLE CENSUS, not a grep this section performs once (round
3, major 15): a census class derives the visual-disabled consumer
set on the house syntax-tree helper — every consumer of
`showVisual`, VisualShipsLater (the const is retired) and the visual-disabled
assertions — requires one disposition row per consumer, and fails
on both a consumer without a disposition and a disposition without
a consumer; the flip is complete when the census says so, not when
this list does. Enumerated today for the reader: the surface
radio's disabled wiring and help text, both workspace commands, the
registrar comments, the §A registration fact
(`ShowVisualIsEnabledAndDrivesTheSurfaceSwitch`, renamed at the flip), the §B switcher facts and
journeys that assert Visual is disabled, `toggleFollowSelection`
parity and `chords.json` evidence per B12, and the Canvas VISUAL
row in `w_c_matrix.md`. One §C debt lands here by name: §C's m9
assigned PR D the silent Ctrl+F-from-the-table-header repair, and
it ships in this PR with its fact. C9's headline survives: Commit
Mode and Cancel Mode remain the two disabled rows until §F.

**D15 — Focus order: one stop, and the arrows C1 grants.** The
renderer is a single focus stop after the surface switcher; cards
are reached by arrows and AT navigation, never Tab. The visual
projection owns Down and Up — reading-order moves through the
announced door — and Right/Left stay where frozen C1's R2 table put
them: the OUTLINE's. No spatial-move authority exists in core, and
§D invents none; a 2D arrow scheme is a C1 amendment plus a core
query, requested as an owner decision if wanted. Escape and
Where-am-I remain the two ungated rows.

**D16 — §K budgets are asserted, not aspirational.** The renderer
benchmarks run the 2,000-node fixture and ASSERT the mac budgets:
first windowed rebuild under 500 ms, a pan's window hop under 100
ms, a navigator step under 50 ms, measured values recorded in
`BENCHMARKS.md` beside mac's 3.9 / 2.8 / 0.18.

**D17 — What §D does NOT do, and what it DEPENDS ON, said now.** No
mutation (§E's funnel); no modes (§F draws over D1's installed
state and commits through §E); no mark SEMANTICS (the "marked"
ItemStatus renders; §G gives it meaning); no shell-wide text
scaling beyond the canvas tab (D11's recorded scope). The ledger,
honestly: D1 takes T1's remedy for this surface; D2 and D6 extend
the model by CODE (the population's scene field, the machine's
selection event) through the implementation loop; D7 applies
T2/T6's validation shape to the read-only slice without closing
either row — and its last-owner clause CONSUMES the inherited
presenter authority, so §D DEPENDS on the periphery's T5 row
rather than staying clear of it (round 3, major 11's correction:
the dependency is recorded, not denied); T3 stays §E's. B11's
exactly-one-projection rule now counts three arms —
`ExactlyOneProjectionIsEverInTheTree` widens rather than gaining a
sibling — and outside `Ready` the visual arm shows the state
message pane (or D2's renderer failure pane), never an empty
canvas.

### Decisions

**DD-1 — DrawingVisual, not WebView and not SVG.** A FrameworkElement
drawing through DrawingContext with a DrawingVisual per card. A
WebView puts the accessibility tree behind a browser boundary and
every peer contract above out of reach; SVG buys nothing WPF
geometry does not already do.

**DD-2 — Unlabelled edges are not peers.** Mac parity: an edge with
no label has no accessible handle, and its existence is readable
from either endpoint's connection phrases. A peer per unlabelled
edge on a dense board would bury the cards it connects.

**DD-3 — The tooltip is a SURFACE transient with a literal order.**
Tooltip, then interim card detail, then filter seating —
most-transient first, one dismissal per press, in C6's surface rung
after mode and filter. CD-47 stays the one recorded pre-emption
(open-state, not focus-state), and no new Escape arbiter exists.

**DD-4 — The text-scale mechanism is an owned service.** Registry
read plus preference-change subscription, dispatcher-marshalled,
disposable — the W1-1 reactive shape including its unsubscribe half.
No WPF binding exists, so the census keeps the next text run honest.

**DD-5 — The container's zoom is a declared VALUE PATTERN.** A
read-only value provider on the container peer, its Value "Zoom N
percent", its property-change event raised from D1's old/new pair,
and a journey asserts the pattern is retrievable. Value, not Name:
the name is the surface's stable identity; the zoom is state a
reader polls, and mac reports it the same way.

**DD-6 — The selection ring maps to the highlight brush in
Contrast.** The ring is a stroke on the WINDOW surface; the
highlight-text brush is calibrated for text ON the highlight
surface, which the ring never sits on. The literal table (D13) is
where an implementer reads this.

**DD-7 — Viewport transforms are instant in EVERY mode.** No tween
is ever an installed state, so peers, hit-testing, the ring and the
pixels agree in one commit; Reduce Motion changes nothing because
there is no motion to reduce. Chosen over per-frame installed
states, which would buy animation at the cost of making every
frame a UIA event storm.


### IMPLEMENTATION OBLIGATIONS — the §D ledger

Nine obligations, round 4's findings verbatim as acceptance
criteria. None is claimed resolved here.

**ID-1 — The dispatcher proof made real.** *"The dispatcher-only
proof is false against shipped scheduling: PanelWorkScheduler.Post
runs inline when its captured context is null and may use a non-WPF
context, while ApplyPublication has no dispatcher assertion. …
Remedy: marshal every applied-presentation commit through an
explicitly captured WPF Dispatcher, expose one post-apply
notification, and assert dispatcher access in production and
barrier facts."* Direction: the renderer's commit owns its
dispatcher; the assertion is production code, not a test's hope.

**ID-2 — The build has a progress bound.** *"The stale-build path
has no progress bound: 'yield to the freshest queued build'
specifies neither single-flight coalescing nor a maximum number of
discarded derivations. … Remedy: specify one running build plus one
replaceable latest pending build, deduplicate publications with the
same effective render source, and assert a
final-publication-to-install latency bound."*

**ID-3 — The descriptor index is load-class.** *"The descriptor
index is population-wide but its lifetime is not: D3 places it in
every installed state, while only D2's scene is declared load-class
and identity-retained. … Remedy: declare the descriptor index
load-class, build it once with the population/scene, and require
exact reference reuse across same-population presentation states."*

**ID-4 — Edges window by their rendered bounds.** *"'Either endpoint
is in the window' silently narrows the executable spec's 'visible
labelled connection.' … Remedy: determine edge materialization from
rendered path/label bounds intersecting the window, not endpoint
membership."*

**ID-5 — Visual selection derives from the publication, not the
unit.** *"Full-scene selection cannot be derived from the frozen
unit as claimed: CanvasProjectionUnit.Answered retains
ResolvedSelection only when the node is matched, while D4
deliberately keeps unmatched cards visible and selectable. …
Remedy: derive visual selection from the applied publication's
document-level SelectedIntent resolved against its population,
leaving the unit's filtered-selection semantics unchanged."*
Direction: this retires revision 4's machine-owned selection event —
the pair the renderer needs already travels in the publication, and
D6's doors publish intent exactly as every surface does today.

**ID-6 — Selection's relationship to U9, settled by ID-5.** *"Calling
selection a 'total event' adds an event class to U9 without adding
its cells or defining the relaxed completion validation. … Remedy:
either keep selection outside the filter machine or add a literal
selection-event column with schedule preservation, terminal
behavior, lineage validation, promotion, and matching/nonmatching
carry-forward facts."* Direction: the FIRST branch — selection stays
outside the machine (ID-5's derivation makes the event unnecessary);
if implementation finds the machine event necessary after all, the
literal column with every listed cell is the price of admission.

**ID-7 — The no-pane refusal is a typed vocabulary arm.** *"C4
cannot supply the promised no-pane refusal: it maps document load
state × handle liveness, and Ready with a live handle returns
'proceed'; the shipped canvas vocabulary also has no no-pane arm. …
Remedy: add a separate presentation-address refusal and typed core
vocabulary arm, routed through the document announcer and pinned by
the five-place vocabulary rule."* Direction: a small preparatory
CORE task inside this PR, the 0a pattern — the arm lands in the
vocabulary with its four mirrors before the shell consumes it.

**ID-8 — The binding record is the one authority.** *"Registrar rows
are enumerable, but delivery members are not bound as D14 claims:
ChordTableEntry names no delivery member, Resolvers is a private
ID-to-lambda map, and ResolvableIds exposes only IDs. … Remedy:
make an enumerable binding record containing row ID and named
delivery member/resolver the authority from which registration,
resolution, and the C4 disposition census all derive."*

**ID-9 — The tooltip's trigger matrix covers every truncated
label.** *"D11's keyboard fact covers only a truncated card, not an
edge chip, and its persistence wording permits hover departure to
close a tooltip while keyboard focus remains on the trigger. …
Remedy: pin the card/group/edge × focus/hover trigger matrix and
close only when all active triggers have departed, Esc dismisses,
or the content becomes invalid."*

### Implementation plan — the task loop

Seven tasks, each closing with facts, defect-injection mutations and
a record entry before its review round, the C-unit loop verbatim:

| Task | Builds | Discharges |
|---|---|---|
| TD-1 | the presentation commit: two authorities, the dispatcher-owned install, single-flight coalescing, both barrier facts | ID-1, ID-2 |
| TD-2 | the population's scene field behind the deep-copy wall; the load-class descriptor index | ID-3, D2 |
| TD-3 | peers: three cells, discriminated identity, retirement, path-bounds edge windowing | ID-4, D3 |
| TD-4 | selection: publication-derived visual selection, the doors, the pattern matrix | ID-5, ID-6, D6 |
| TD-5 | the viewport verbs: pane addressing, the no-pane vocabulary arm (core first), the binding record, the enablement census, m9 | ID-7, ID-8, D7, D14 |
| TD-6 | the drawing: ring, z, instant transforms, text scaling, the tooltip matrix, the color rows | ID-9, D8–D13 |
| TD-7 | the §D censuses, the benchmarks, the FlaUI journey, the flip | D14–D16, the evidence |
### The sweep this section performed on arrival

Writing this heading made every live "until PR D" claim stale by the
census's own rule — the shipped set is derived from section headings,
and the safe direction is to fire while the document is being edited.
Swept in the same change, all reworded to name §D without the staged
form: B10's disabled-row sentence and B11's projection note in this
document, B12's delivered-set example, the `CanvasSurfaceView`
projection doc, both `WorkspaceViewModel` command notes, and the two
`SlateCommandRegistrar` comments. Their claims are unchanged — the
rows stay disabled until the code in this section EXISTS. The FULL
flip inventory is D14's executable census, which arbitrates
completeness instead of any list.

### Verification plan

A renderer battery against a REAL `VaultSession` and real `.canvas`
bytes, §C's pattern: D1's barrier facts in BOTH orders (a
publication landing during a build; a viewport commit landing
during a publication build — neither install is lost, neither
installs stale) and the reentrancy arrangement (a UIA-shaped read
raced against an apply observes one installed state throughout);
the two-pane cycle (one document in two panes, independent peers,
one pane closed, the other's peer surviving a
dematerialize/rematerialize round trip); the THREE-cell state table
including first-touch find-by-property on a never-materialized card
answering from the descriptor index; the retirement facts (a
discarded build commits no identities; tombstones prune to the
window plus live references across a long pan sequence); the D6
pattern matrix cell by cell including the AddToSelection throw; the
mid-match selection fact (a selection published between a match's
start and landing: the schedule is not stranded and the selection
survives the completion); the end-style table cell by cell; the
origin-sensitive pan rule in both toggle states from both origins;
the filter fixture through drawing, hit-testing and UIA (dimmed,
never hidden); peer rectangle invalidation after zoom, pan and
resize; the four-way name-collision fixture (twin titles, twin
parallel edges, a card named after the container, a pre-suffixed
spelling); every node kind on the sample canvas; instant transforms
in both animation settings (DD-7); ring metrics at 0.1× and 4×;
hit-test z; the text-scale factor reaching every run; the tooltip's
four facts (focus-open, Esc-dismiss in DD-3's order including the
trigger-card arrangement, pointer traversal, persistence); each
viewport command's transform, pane-addressed, with the no-pane
refusal fact; the m9 header-cell Ctrl+F fact; the registrar-derived
C4 mapping fact with the new verbs' no-selection and empty-canvas
arms; the LIFECYCLE facts (a pane closed and reopened returns the
preference and theme handler counts and the retained renderer
references to baseline); and the ENABLEMENT CENSUS (D14) failing on
both a consumer without a disposition and a disposition without a
consumer. `ThemeTokenContrastTests` gains the fill, group-fill,
ring, background and HEX rows in both appearances, and the Contrast
census derives from D13's table. The text-scale census walks the
canvas runs. The FlaUI journey lands in `ShellAccessibilityTests`:
container and child peers, the container's value pattern retrieved,
a rectangle that CHANGES after Ctrl+=, a select that moves
selection and announces, virtualized realization from a property
search, Shift+1 fitting, axe clean over peered elements only. The
benchmarks assert D16's budgets. The chord scrape and
`SharedCommandChords` pick up the new rows; the parity flip rides
the round-trip test per B12.

### Hand-off rows

- **To §E:** mutation renders by publishing — the funnel writes, the
  slot swaps, the presentation state rebuilds; the renderer exposes
  no second door. The card editor overlays D1's installed state.
  T2/T3 close there; D7's addressing is the shape they inherit.
- **To §F:** a mode's transient geometry draws OVER the installed
  state and commits through §E's funnel. A spatial arrow authority,
  if the owner wants one, is a C1 amendment plus a core query —
  requested there, not here (D15).
- **To §G:** ItemStatus "marked" exists from D3 on; §G gives it
  semantics and bulk verbs.
- **To W8-2:** D13's literal table, the APCA rows and the recorded
  manual check are the canvas half of the shared CI lock.

### Round record

**Revision 1** — drafted after PR C-unit merged, from the executable
spec's §PR D and the C-unit model as shipped.

**Round 1 — NOT SOUND, 6 blockers, 9 majors, 1 minor; all sixteen
accepted, answered by revision 2** (codex at the protocol tier,
xhigh, session 01a0599e-8670-73f0-a1b5-9bfdb6a59417).

**Round 2 — NOT SOUND, 5 blockers, 13 majors, 1 minor; all nineteen
accepted, answered by revision 3.** All five blockers attacked
mechanisms revision 2 introduced — the rule-5 signal, recorded with
rule 4 armed.

**Round 3 — NOT SOUND, 5 blockers, 10 majors, 1 minor — the rule-4
stop TRIPPED, and the owner ruled.** Three consecutive blocker
rounds against the presentation commit, round 2 counting double.
The findings were held for the owner's ruling per the protocol;
**the owner ruled: another prose round — revision 4 answers round 3
in full, with the stop's trip standing in the record.**
Dispositions: (1) the TOCTOU dissolves — both authorities commit on
the dispatcher and the check-and-swap shares that thread (D1); (2)
viewport commands commit their value BEFORE any build, so no build
owns a delta (D1); (3) the envelope is withdrawn — the scene
becomes a field of the population, the chain stays the one carrier,
selection and filter are derived accessors, and rejection shows a
failure pane (D2); (4) the three-cell state table with the
descriptor index (D3); (5) the end-style table makes edge Invoke
total (D5); (6) selection publication becomes a filter-machine
event with completion carry-forward (D6); (7) retirement: identities
commit only with a winning install, weak registry, pruned
tombstones (D3); (8) discriminated edge identity and edge windowing
(D3); (9) edge names are core's rendered traversal phrase (D5);
(10) the skip-occupied ordinal loop and the pre-suffixed fixture
(D5); (11) the no-pane refusal, and the T5 dependency recorded
honestly (D7/D17); (12) the GroupFill APCA pairs (D8/D13); (13)
transforms are instant in every mode (D10/DD-7); (14) the C4
inventory derives from the registrar with bound delivery members
(D14); (15) the enablement sweep is an executable census (D14);
(16) the intra-surface transient order is literal (D12/DD-3).

**Revision 4** — answered round 3 under the owner's ruling; round 4
ran against it.

**Round 4 — NOT SOUND, 3 blockers, 6 majors, 0 minors — and the
recorded condition FIRED.** Codex at the protocol tier. The
trajectory bent — 6, 5, 5, 3 blockers across the rounds, no minors
left — and two rows were ratified WHOLE: D2 (the population's scene
field, its wall, its budgets, the failure pane's reachability) and
D5 (independent end-style booleans confirmed against the shipped
algebra; the rendered traversal phrase host-reachable through the
announcer's label rendering). But the reviewer's own classification
puts blockers 1 and 5 in the PRESENTATION COMMIT — the fourth
consecutive blocker round against that subsystem: the dispatcher
proof is false against `PanelWorkScheduler`'s inline-post arm, and
the unit's `ResolvedSelection` cannot carry a dimmed card's
selection the filter never matched. Blocker 7 is the separate
command/refusal subsystem: C4 maps load state and cannot speak a
no-pane refusal, and the vocabulary has no arm for it — a typed
core addition under the five-place rule. The majors are
implementation-shaped: a single-flight build bound, the descriptor
index's load-class lifetime, path-intersection edge windowing, the
selection event's literal U9 column, an enumerable
row-to-delivery-member binding record, and the tooltip's
trigger-matrix over cards AND edge chips. Findings are HELD, not
dispositioned: the condition recorded beside the owner's ruling —
a blocker round against the same subsystem returns the ruling to
the owner — has fired, and the ruling was made the same day.

**The ruling, 2026-08-31: FREEZE + IMPLEMENT.** The section freezes
at revision 4 as the ratified baseline; round 4's nine findings
become the §D ID-ledger, verbatim, above; and the task loop begins
at TD-1. Prose stops arbitrating what code arbitrates better — the
C-unit sentence, arriving one PR later, at one third the round
count.

### Implementation record

**TD-1 — the presentation commit, landed.** Three types:
`CanvasViewportState` (the viewport authority — the pinned constants,
the one zoom arithmetic every verb routes through, centre
preservation as a fact, `SameGeometry` as the deduplication
predicate), `CanvasPresentationState` (the installed value: source,
viewport, the two service revisions — TD-3 extends it with the peer
topology), and `CanvasPresentationEngine` (both authorities, the
dispatcher captured at construction, the production assertion, the
marshalling intake `OnPublicationApplied`, single-flight builds with
install-time revalidation, `DiscardedBuildsForTests` as the
dispositioned instrument). The document gained its post-apply
notification — `PublicationApplied`, raised after every apply from
whatever thread ran it, with the thread promise deliberately NOT
made: the engine marshals itself, which is ID-1's answer to the
scheduler's recorded inline arm.

**One simplification the facts forced.** The first cut carried a
pending-request flag beside the running-build flag; the analysis
that tightened the mid-build fact showed the flag was dead — the
AUTHORITIES are the pending request, because any arrival that
matters moves an authority, which makes the running build stale at
install, and the stale path re-requests from the current pair. ID-2's
"one replaceable pending build" is structural, not tracked.

**The model census refused the engine, and the refusal was
correct.** Admitting the three types to the model roster tripped
three arms at once — the closed world, the authority dispositions,
and the writer arm, which named the engine's publication field
beside the slot's as a second U1 violation. The engine is PERIPHERY:
it consumes the applied publication exactly as
`CanvasDocumentViewModel` does, and its walls belong to TD-7's
presentation census, not to the model's. Recorded because the
failure is the design speaking: a §D type that wants into the model
roster is claiming currency authority, and nothing presentation-side
may.

**Mutations, each injected into production, the fact failing on its
own sentence, the restore byte-for-byte:** the neutered thread
assertion (`ACommitFromAWorkerThreadThrowsTheThreadRule` red); the
install without revalidation
(`APublicationLandingMidBuildIsNeverOvertaken` red — the tightened
assertion, after the first cut's OR would have let the mutant pass);
the discard without re-request
(`AViewportCommitDuringAPublicationBuildIsNotLost` red). Eight facts
green pristine; ID-1 and ID-2 are discharged pending TD-1's review
round.

**TD-2 — the scene rides the population, landed.** `CanvasPopulation`
gains the deep-copied scene: `CanvasSceneNode` cards and
`CanvasSceneEdge` connections through the one construction site's
collection copy — a full copy because the scene records' fields are
scalars and strings — plus the load-class descriptor index of
obligation ID-3, built once in the constructor and identity-retained
with the population it serves. The pipeline hands the WHOLE scene to
the acceptance now, and the pre-TD-2 sentence on the subpath index —
geometry never enters the model — is superseded in place, D2's
ruling performed. Facts:
`ACallerRetainedSceneCannotMoveTheBuiltPopulation` (I7 reaching the
scene, nodes and edges both) and
`TheDescriptorIndexIsLoadClassAndKeyedByNode` (ID-3's structural
half). Mutations, injected and restored byte-for-byte: the copy
optimized away with the runtime's alias-preserving wrap (the fact
red on the moved node), and the index rebuilt per read (the fact red
on its reference premise). One mutation covers both scene
collections, deliberately — the edges share the nodes' mechanism,
and a second mutant would exercise the same wall twice. ID-3 is
discharged pending TD-2's review round; D2's failure-pane and
renderer-consumption halves land with TD-6.

**TD-3 — the peer topology and the third authority, landed.**
`CanvasPeerTopology`: the three-cell table as data — materialized
placements with document-space rectangles, tombstones ONLY for
externally retained keys, and the UNREALIZED cell deliberately
ABSENT: the population's descriptor index answers a first touch, so
storing an entry per unrealized card would be the index copied per
state, which is what obligation ID-3 just forbade. Identity is
discriminated (card or edge plus core id; the renderer and document
halves structural). Edges window by their RENDERED bounds — the
endpoints' bounding box, containing the straight path and its label
chip — pinned by
`ALongEdgeCrossingTheWindowMaterializesWithoutItsEndpoints`, the
exact arrangement obligation ID-4 forbade narrowing. The engine
gains its third dispatcher-committed authority — the retained set,
deduplicated by set equality
(`ARetainedCommitRebuildsAndAnEqualSetDoesNot`) — and the installed
state carries topology and retained snapshot for the install-time
revalidation; a discarded build's topology vanishes with the build,
which is half the retirement rule, the registry's weak-identity
half arriving with TD-6's peer objects. Also pinned:
`CardsMaterializeByTheWindowPlusOneMargin`,
`AnUnlabelledEdgeIsNeverAPeer` (DD-2), and
`TombstonesAreRetainedOnlyAndIdentityIsDiscriminated`. Mutations,
injected and restored byte-for-byte: endpoint-membership windowing
(the crossing fact red) and tombstones-for-everyone (the
materialization fact red on the far card's entry). ID-4 is
discharged pending TD-3's review round; the Ready-publication
integration fact rides TD-6's renderer battery, where the full
transfer path exists.

**TD-4 — the rendered selection derives from the publication,
landed.** `CanvasPresentationState` gains its selection accessor:
the durable selected intent resolved against the population the
state draws — obligation ID-5's remedy verbatim, and obligation
ID-6's settled direction performed by ABSENCE: the filter machine
gained no event, the doors stay the shipped pair (`SelectNode`
announced, `SeatSelectionSilently` silent), and the unit's filtered
semantics are untouched. Facts:
`TheStateSelectionFollowsThePublicationNotTheUnit` — the dimmed-card
arrangement, a filter matching only one card while the reader
selects the other, the state answering with the publication's
resolution while the unit's says nothing — and
`TwoStatesAnswerWithTheirOwnSelections`, which is the T5-divergence
door held shut by purity. Mutation, injected and restored
byte-for-byte: the selection read from the unit's filtered
resolution, the dimmed-card fact red on the lost selection. ID-5
and ID-6 are discharged pending TD-4's review round; the pattern
MATRIX's cells are peer behavior and land with TD-6's objects.

**TD-5 — the viewport verbs, the typed refusal, the binding record
and the enablement census, landed.** The vocabulary gained
`CanvasViewportNoPane` through all five places — the core enum with
its render ("No canvas view to act on."), the golden table, the
regenerated corpus artifact, the uniffi mirror, and both hosts'
corpus mirrors — obligation ID-7's arm, a Medium-priority
uncoalesced status. The navigator gained the six public verbs
routing through one gate (`ViewportFromKey` for the Ctrl chords,
`VisualOnlyFromKey` for the two bare Shift chords R2 confines to
the visual board), the presenter seam gained its one viewport
member with `CanvasViewportVerb` as the closed set, and the C4
derivation swept all six into the mapping fact automatically — the
reflection-derived surface working exactly as codex round 11 built
it to. Obligation ID-8's `CanvasViewportBindings` is the one
authority: the resolver map consumes it in a loop, and
`TheViewportBindingRecordIsTheOneAuthority` pins id ↔ registered
row ↔ existing navigator member ↔ resolvable id; the record's known
limit — it cannot see a resolver returning the WRONG command — is
stated here rather than implied away, and the full delivery fact
rides TD-6's windowed battery. The enablement sweep is
`CanvasEnablementCensus`, executable: consumers derived from the
three markers, one disposition per consumer, failing both ways —
and its first run caught two consumers the hand list missed, which
is the census earning its keep on day one. §C's m9 is repaired by
name: the stand-aside asks who OWNS the keys
(`CtrlFFromTheTableHeaderReachesTheFilter`), and C1's gate-table
row is swept to match. Five chord rows landed with mac's glyphs;
the scrape learned the digit row; `chords.json` regenerated through
its writer per B12. Mutations, injected and restored byte-for-byte:
the stand-aside reverted to what-is-showing (the m9 fact red), and
the refusal silenced (the no-pane fact red). ID-7 and ID-8 are
discharged pending TD-5's review round.

**TD-6 — the drawing, the peers, the mount and the FLIP, landed
across four slices.** The color layer first: sixteen token keys in
all three dictionaries, the six preset fills precomputed by
`CanvasPalette`'s one composite (mac's 0.18 over the opaque
surface), the Contrast dictionary per D13's literal table, and
`ThemeTokenContrastTests` gaining the canvas matrix plus the
hostile-hex fact that drives the PRODUCTION composite. Then the
text-scale owner (`CanvasTextScaleService` — the registry read, the
marshalled preference subscription, the disposable W1-1 shape). Then
the renderer: `CanvasRendererView` drawing the installed state
(cards, edges, the screen-space ring, dimming from the unit's
matched set), document-order hit-testing, and the peers —
`CanvasRendererAutomationPeer` with the value, selection and
item-container patterns, `CanvasCardAutomationPeer` over
`CanvasPeerKey` with the placeholder's always-exposed
virtualized-item pattern and D6's cell-by-cell selection matrix —
plus the viewport handler (fit and zoom-to-selection from the
installed state's bounds). Last the MOUNT and the FLIP: the third
arm in the same body slot under the one-projection rule, the
presenter's three-way switches, `ViewportCommand` routing to THIS
pane's engine, and every enablement-census disposition performed —
the radio enabled with its ships-later hint gone (the VisualShipsLater
const deleted), both workspace commands live, the registrar comments
swept, the §A fact renamed to
`ShowVisualIsEnabledAndDrivesTheSurfaceSwitch`, the §B facts
flipped, `chords.json` and the parity generator's delivered set
extended, and the doc's citations swept with them.

**Two fixes the facts forced, recorded.** The engine's install
continuation rode the AMBIENT synchronization context; under a test
host that context is the pool's, the install landed off-dispatcher,
and the thread assertion died unobserved — the continuation now
posts to the CAPTURED dispatcher by name, which is what ID-1 meant
all along. And the renderer's resource reads are TryFindResource
with a transparent fallback: an unthemed host draws nothing rather
than crashing the dispatcher, and key integrity belongs to TD-7's
token-drift census, not to a draw-time throw.

**An encoding scar, owned.** A scripted edit wrote a Latin-1 § into
the parity generator; the blanket repair then corrupted the SHIFT
glyph's third byte in two files. Both were repaired
byte-precisely and every changed file decode-verified — the lesson
(scripted edits carry their encoding with them) is this record's to
keep.

**Mutations, injected and restored byte-for-byte:** the third arm
never showing (the render fact red); the viewport verb answering
without acting (the zoom fact red); plus the per-slice mutations
already recorded (the palette's dedupe, the service's stability).
ID-9's tooltip matrix and the windowed lifecycle facts ride the
FlaUI battery with TD-7, where a real pane closes; the w_c_matrix
visual row and the token-drift census are TD-7's, and this entry is
the note that keeps them owed.

**The implementation review round — TD-1 through TD-6, one pass, ten
findings, all accepted.** The house reviewer over the whole
implementation span: eight CONFIRMED, two PLAUSIBLE, every one a
real defect or a real gap. Fixed: (1) a faulted off-thread build
held the single-flight flag forever — the continuation now runs on
every completion, resets the flag and rethrows LOUDLY on the
captured dispatcher (a pure derivation that throws is a defect to
surface, not a frame to skip); (2) the marshalled publication
intake could commit a stale publication over a newer one — each
notification takes an interlocked ticket and a stale messenger
drops itself; (3) a throwing apply suppressed the post-apply
notification — it moves into the finally, honoring "after EVERY
apply"; (4) the bare Shift chords checked only what was SHOWING —
both halves of R2 now gate them, pinned by
`AShiftChordWithoutProjectionFocusFallsThrough` (Shift+1 with the
caret in the filter field is the reader's '!'); (5) the engine
gained its teardown contract — `Shutdown` on the engine, called by
the renderer's, guarded in intake and install, pinned by
`AShutDownEngineInstallsNothing`; (6) the hex parser admitted
whitespace-padded bodies through the framework's composite flags —
AllowHexSpecifier alone now, pinned by
`WhitespacePaddedHexIsRefused` in `CanvasPaletteTests`; (7) the
text-scale read clamps BOTH ways to the documented range; (8) the
topology's window test is edge-inclusive, matching platform
rectangle semantics. One finding was fixed in flight before the
report landed — the ambient-context scheduler, replaced by the
captured-dispatcher post — and the reviewer's PLAUSIBLE sibling
confirms why. The remaining PLAUSIBLE (a reentrant apply raising
newer-then-older) is MITIGATED by the same ticket that fixes (2),
and recorded rather than claimed closed: no in-tree subscriber
re-enters today, and the ordering guard is the ticket, not a hope.
Mutations, injected and restored byte-for-byte: the focus half of
the Shift gate reverted (its fact red) and the whitespace tolerance
reverted (its fact red). Two fixes carry no direct fact and say so:
the faulted-derive reset has no seam to a throwing derivation (the
derive is pure and private), and the stale-ticket race has no
deterministic arrangement in-process — both are covered by the
review's own verification and stated here instead of implied.

**TD-7 — the censuses, the benchmarks, the journey and the evidence,
landed; and the section's remaining debt said out loud.** The
token-drift census closes TD-6's recorded gap:
`ThePrecomputedFillTokensMatchThePaletteArithmetic` proves the six
Fill tokens in each appearance ARE the palette's one composite, so a
hand-edited token cannot drift while the contrast floor happens to
pass. The `w_c_matrix.md` gains the Canvas VISUAL row, composed from
this section's own contract inventory as B12's pattern requires.
`CanvasRendererBenchmarks` lands in the canvas suite with its own
budget arm (the W2-2 shape): the derivation medians on the
2,000-node fixture are 0.018 / 0.023 / <0.001 ms against the
500 / 100 / 50 budgets — recorded in `BENCHMARKS.md` with the
honest scope note that these measure the pure derivation the engine
pays per pan, while the draw and the UIA re-frame ride the app and
the journey. The FlaUI journey
(`CanvasSurfaces_VisualBoardPeersAndZoom_AreClean`) drives the flip
end to end through the real bridge: the enabled radio, the board's
declared Value pattern, unique card names, the rectangle that
CHANGES after Ctrl+= — the stale-frame classic asserted at the level
it is true — a pattern select reporting selected, and axe over
peered elements only; the renderer gained its findable automation
id for it. CI arbitrates the journey per the recorded desktop rules.

**The debt, named rather than implied away:** obligation ID-9's
tooltip is UNBUILT — D11's truncated-label tooltip with its
focus/hover trigger matrix never landed in TD-6's drawing, so
nothing here can evidence it; and the close/reopen lifecycle facts
(the subscription-count half of the review round's minor) remain
unwritten for the same reason — the pane-close path they need is the
workspace's, not this battery's. Both stay OPEN on the ledger for
the review round to weigh: a §D that ships owes them either a
follow-up slice or an owner's recorded deferral, and this entry is
where that decision starts from the truth.

**The tooltip slice — obligation ID-9 discharged, and the ledger
closed by the owner's ruling.** D11's tooltip landed whole: the
truncation set derives per draw pass from the same constrained text
the cards render; keyboard FOCUS summons it — cards are peers, so
the arrows' selection IS the keyboard's presence on a card — and the
three 1.4.13 conditions are one rule
(`TheTooltipClosesOnlyWhenEveryTriggerHasDeparted`): open while ANY
trigger is active (the selection, the pointer on the card, the
pointer on the tooltip itself), closed only when every trigger has
departed, Esc dismisses through the surface rung — DD-3's most
transient, inserted after CD-47's panel pre-emption
(`SelectingATruncatedCardSummonsTheTooltipAndEscDismissesIt` pins
both legs) — or the content stops being valid, revalidated on every
installed state. The review round's lifecycle minor closes with it:
`ARendererDetachReturnsTheSubscriberCountToBaseline` pins the
attach/detach pair against the document's subscriber count.

**And the slice EARNED its keep on the way in: a T5-class divergence
in the shipped intent path, found and fixed.** The failing tooltip
fact would not open, and the diagnosis ran to the model: the
outline's tree auto-selects its first row DURING an apply, and
`PublishIntent`'s applying-guard — built so a failure-CLEAR cannot
erase the durable intent (the T3 review's fourth finding) —
swallowed that positive seat too. The surface authority then ran
AHEAD of the published intent for as long as the reader stayed put:
the exact divergence class the periphery's T-rows describe, live in
shipped code, invisible until a consumer finally read the applied
intent. The fix reconciles at the apply's close: a NON-NULL seat
that differs from the applied intent publishes once the window
ends; a cleared selection still never erases the intent, so T3's
rule survives verbatim. An intent publication also APPLIES
immediately now — the applied snapshot used to lag every selection
until the next load or filter answer, which no consumer noticed
before the renderer became one.

**Mutations, injected and restored byte-for-byte:** the keyboard
trigger removed from the subject rule (the summon fact red), and the
rung arm removed from the surface (the dismiss leg red). The
hover-departure persistence rides its own fact's arrangement. With
ID-9 discharged, every obligation on the §D ledger — ID-1 through
ID-9 — is code, facts and mutations; the section's evidence is
whole, and the PR is the next act.

---

## PR E — the mutation funnel, the undo domain, the authoring verbs, the pickers, and the card editor (FROZEN as the ratified baseline by the owner's ruling; round 2's findings are the IE ledger, discharged by code)

**Goal (spec §PR E).** Everything that is a single committed
`canvas_apply` from a command, plus the undo domain and the editor.
After this PR a keyboard user can create a canvas and author cards,
groups and connections' basics without the visual surface. This is the
first WRITING PR of the series, and five ledgered inheritances come
due as contracts below: M8, m5 (with §C's m-5), m7, m11, and the
media-class export.

This section is revision 2, rewritten against round 1's twenty-one
blockers. Round 1's central finding was that revision 1 described a
SEQUENCE where the platform needs an OPERATION — a value with
identity, currency and a lifecycle — and this revision is organized
around that value. New types remain plain text until tasks bind them
(§C-unit's convention); every core name cited is verified against the
FFI surface as it exists today.

### THE FREEZE — read this first

**This section is CLOSED to further prose revision**, frozen at
revision 2 after two adversarial rounds, by the owner's ruling of
2026-08-31. The trajectory — 21 blockers, then 32 with a cluster
CREATED by revision 2's own fixes — is the protocol's rule-5 signal
one round earlier than §C-unit and §D produced it: the fix was a
patch over a missing model, and the missing model is not a prose
question. The remaining questions are identity, boundary and
FFI-surface questions that every prose answer re-opened; the ruling
is the one that closed §C-unit and §D: **design by implementation.**
Nothing below is re-argued on paper; the next thing that changes any
of it is code, through the task loop, with facts and mutations
arbitrating.

**What is RATIFIED, and is therefore the architecture to build.**
Everything this section states that round 2 did not name in a
finding: the operation value with initiating surface, currency and
typed effects; the per-document gate over the whole transaction with
refusal-not-queue (ED-5); preparation inside the gate; currency
validated at the three boundaries; commit recorded before
presentation; basis-carrying history with restore-on-every-non-
commit and the reload quarantine; the conflict record with three
typed resolutions rendered in-sheet; snapshot-routed undo across the
four domains; the corrected verb algebra (RenameGroup; Ungroup/
Cancel; the three-outcome placement with mac's overlap check; the
reporting create); the total refusal principle; core-owned proximity
order; the basis-bound editor draft; M8's commit semantics; the
E12–E19 inheritance discharges; and the ED decisions. **One
precedence ruling is made here rather than left open (round 2's
last blocker):** E3's order — apply, then the history record, then
refresh, then effects — is NORMATIVE and supersedes the spec §1
sketch "apply → refresh → undo push → announce" wherever the two
disagree; the spec line is a sketch of the same funnel, and the
recorded order is the one the code builds. **ED-6's three-addition
ceiling is LIFTED by the same ruling:** the FFI-gap obligations
below add the core surface they name, each with mac consumption,
and the ledger — not a ceiling count — bounds the additions.

**What is RECLASSIFIED.** Round 2's thirty-nine findings stop being
prose questions and become the §E IMPLEMENTATION OBLIGATIONS —
IE-1 through IE-39 below, each carrying the finding VERBATIM
(backticks and links flattened, the periphery ledger's convention)
as its acceptance criteria, discharged by code, facts and mutation
batteries through the implementation gauntlet, not by another
revision of this text. Where an obligation's remedy sets a
DIRECTION, the direction binds unless the code proves it wrong, and
proving it wrong is a recorded event, not a silent choice.

### Contracts

**E1 — A mutation is an OPERATION VALUE, and one gate serializes the
whole transaction.** Every verb constructs one typed mutation
operation carrying: the document and the lease generation it was
minted under (the §D ticket discipline, one commit thread over); the
INITIATING SURFACE — the pane and row that invoked it, an opaque
owner every later presentation or focus effect validates (round 1
#3: two panes share a document, and a sheet's focus return must land
where the verb began, not where focus wandered); the PREPARATION
BASIS — the content hash the placement or lookup ran against; and
the typed POST-COMMIT EFFECTS (E4). A per-document gate admits ONE
operation at a time through the ENTIRE transaction — prepare →
apply → record → refresh → effects; a verb arriving while the gate
is held is REFUSED with the typed busy status, not queued (mac is
synchronous on the main actor, so "one at a time" is the twin's real
semantic; a queue would invent interleavings mac cannot produce).
Preparation runs INSIDE the held gate (round 1 #5): `canvas_place_new`
/ `canvas_place_inside_group` / the structural lookups execute under
the same admission as the apply they feed, so two rapid New Cards
cannot receive one slot and a lookup cannot go stale between query
and write. The `canvas_apply` itself runs off the dispatcher (the §C
scheduler rule) with the gate still held.

**E2 — Currency is validated at every boundary, and admission is a
table.** The operation's lease is checked BEFORE the FFI call, AFTER
its return, and immediately before every dispatcher-side effect
(round 1 #2's direction verbatim): a document that reloaded,
retargeted or shut down while an apply was in flight swallows the
result — no publish through a successor handle, no announcement for
a closed document, exactly as §D's engine discards a stale install.
Admission refuses, each with its typed event: degraded (the one
reason string, t0 §5); no attached handle — absent, retarget-
pending, shut down (the mac recovery contract); gate held (the busy
status above); a pending conflict record (E6 — no write passes while
recovery is unresolved); a mode transient held (the mac #521 guard).
The transient guard's OWNERSHIP is corrected from revision 1 (round
1 #4): the funnel refuses while ANY transient it does not own is
live, and a mode's commit passes a mode-owned token through the
operation instead of clearing its transient first — a refused commit
therefore leaves the mode and its exact transient state standing for
retry or cancel, which is C7's frozen rule. PR F consumes this token
seam; E ships it with the test mode only.

**E3 — Commit is recorded before presentation, and the two failures
are different states.** The success path in order: `canvas_apply`
returns; the history entry — the action name, the returned inverse,
and the BASIS HASH it is valid against (`CanvasApplyResult`'s
post-write hash) — is recorded IMMEDIATELY, before anything that can
fail; redo clears; then the refresh republishes outline, table and
scene from core; then the post-commit effects run. A refresh or
publish failure after a recorded commit is a typed COMMITTED-BUT-
UNPRESENTED state (round 1 #6): the file changed, the entry exists,
the surface shows a recoverable "reload to see your change" region —
and a retry re-runs the REFRESH, never the committed action. Nothing
in this path can lose the inverse.

**E4 — Post-commit effects are typed in the operation and published
atomically.** Selection reconciliation is not a heuristic over the
old selection (round 1 #7 caught revision 1's rule keeping the ANCHOR
selected after New Card): the operation declares its effect — select-
the-created-node (New Card, New Group, the add verbs), keep-selection
(color, rename, edit), select-the-survivor (delete: the mac next-or-
previous rule) — and the effect resolves against the REFRESHED
population and publishes in the same change the refresh publishes,
so no observer sees the new rows with the old seat. A declared
selection the refresh cannot resolve falls back to the drop rule
(the mac Duplicate lesson stands: never announce over an empty
resolution). The editor-open effect (New Card lands in the editor)
and every focus effect validate the initiating surface first — a
completion whose invoking pane is gone or whose document moved does
NOT focus whatever took its place.

**E5 — The undo domain: entries carry their basis, and transfer only
on success.** Per-document session stacks of (name, inverse, basis).
Undo: admission (E2) → gate → apply the inverse flagged not-to-push →
ON SUCCESS the popped entry's replacement (the returned inverse, the
new basis) pushes to redo; ON ANY non-commit outcome the popped entry
is RESTORED to its stack (round 1 #9 — WriteConflict, InvalidArgument
and a closed handle all leave history exactly as it was). Redo is
symmetric. A conflict against a stale basis raises the conflict
surface AND quarantines: after a Reload attaches a different disk
version, every entry whose basis is not the attached basis is kept
but DISABLED — the menu title says "Undo unavailable — the file
changed on disk" — until an operation restores that exact basis or
the stacks clear with the document (round 1 #10: the old inverse
MUST NOT pass the new CAS by coincidence and overwrite an external
writer's work; basis equality is the only admission). Stacks are
session state, cleared on release, never persisted. Announcements:
"Undid: ⟨name⟩" / "Redid: ⟨name⟩" (`CanvasUndoMenuTitle` renders the
menu), "Nothing to undo.", and the typed blocked arms.

**E6 — Conflict recovery is a state machine with a retained action.**
A `WriteConflict` from any operation creates ONE typed conflict
record: the attempted action, the basis it failed against, and the
initiating surface. While it stands, admission refuses every write
(E2) and the t0 §5 surface shows — assertive `CanvasSaveConflict`
plus a focusable region with the three typed resolutions: RELOAD
discards the record, re-attaches the disk version, republishes, and
applies E5's quarantine to the stacks; OVERWRITE re-applies the
RETAINED action against the reloaded document — a fresh
`canvas_apply` on current basis, which either commits (recording its
inverse normally) or raises a fresh conflict; SAVE A COPY applies
the retained action to a DETACHED copy of the pre-conflict document
and writes the result through `create_exclusive_reporting` under a
"(conflict copy)" name — the never-clobber path with the #1123
typed outcome. The detached-apply query is a core addition this PR
owns (it is the same algebra `canvas_apply` runs, minus the
handle's write), recorded in the core-additions register with the
proximity export (E10) and the media-class export (E12). The region
renders INSIDE the editor sheet when the sheet is open (round 1
#19): recovery is reachable wherever the failure surfaced, the
draft survives, and the focus-return owner is preserved.

**E7 — Undo/redo route by a SNAPSHOT of the focus fact, not by live
focus.** Opening the Edit menu moves focus into the menu (the §C
MenuOpen lesson one domain over — round 1 #11): the menu-open moment
captures one logical undo target — card-editor sheet ⇒ the editor's
own stack; a canvas projection ⇒ the canvas stacks; the files tree ⇒
the structural journal; else the note editor — and titles, enablement
AND execution all read that snapshot, so what the title names is what
the click undoes. The chord path (Ctrl+Z / Ctrl+Y, D-6, imperative
canvas-focus-scoped delivery as the structural rows, allow-listed
with the reason) captures the same target at the keystroke. The
precedence is a table with a fact per row. Titles while a canvas
target is captured render the menu-title event with the top entry's
name — or the quarantine sentence (E5).

**E8 — The verb inventory, corrected against the algebra as it
exists.** Each row: the op, the typed confirmation, the mac twin.
Ops and events below are verified against `CanvasOp` and the
vocabulary — nothing is aspirational.

| Verb | Core write | Confirmation | mac twin |
|---|---|---|---|
| New Card | CreateNode at `canvas_place_new` (anchor = selection, size from `canvas_constants`, no hint), prepared under the gate | `CanvasCreated`; effect: select created + open the editor | `canvasNewCard` (`AppState+CanvasActions.swift:69`) |
| New Group… | CreateGroup at gate-prepared geometry; inserts beneath contained nodes (core rule #960) | `CanvasCreated` | `canvasNewGroup` (`:112`) |
| Delete Selection — card / connection | DeleteNode / DeleteEdge (inverse: RestoreNode / RestoreEdge with exact positions) | `CanvasDeleted` with the undo hint | `canvasDeleteSelection` (`:317`) |
| Delete Selection — group arm | THE ALGEBRA HAS ONE GROUP REMOVAL: DeleteNode and Ungroup on a group are effect-identical (frame + incident edges removed, contained cards kept — `apply.rs:410`). The confirmation offers **Ungroup** and **Cancel** (ED-3): no button promises a descendant delete the algebra does not define | the group arm of `CanvasDeleted` ("cards kept") | mac's delete-on-group, same algebra |
| Rename Group… | RenameGroup { id, label } (round 1 #12) | `CanvasRenamedGroup` | `canvasRenameGroup` (`:361`) |
| Move into Group… | preparation table (E9) → UpdateNodeGeometry | `CanvasMovedIntoGroup` | `canvasMoveIntoGroup` (`:432`) |
| Remove from Group | `canvas_place_new` (anchor = the group, hint Below) → UpdateNodeGeometry | `CanvasRemovedFromGroup` | `canvasRemoveFromGroup` (`AppState+CanvasCreate.swift:280`) |
| Set Color… (preset or validated hex — E10) / Clear Color | SetNodeColor | `CanvasColorSet` — the NAME; hex confirms as core's "custom color" | `canvasSetColor` (`:340`) |
| Edit Card Text… | SetNodeContent on commit (E11) | `CanvasCardUpdated`; "No changes." on an untouched buffer | `canvasCommitCardEdit` (`AppState+CanvasCreate.swift:71`) |
| Add Note… / Add Media… | CreateNode file card via the vault picker | `CanvasCreated`, kind label from core | `canvasAddFileCard` (`:169`) |
| Add Link Card… | CreateNode link card; label = host, from core | `CanvasCreated` | `canvasAddLinkCard` (`:178`) |
| Locate File… | SetNodeContent repointing the missing target | `CanvasCardRetargeted` | `canvasLocate` (`:261`) |
| Edit Connection… | UpdateEdge — label, direction onto from-end/to-end, sides from `canvas_auto_sides` | `CanvasConnectionUpdated` | `canvasEditConnection` (`AppState+CanvasConnect.swift:170`) |
| Delete Connection… | DeleteEdge; the lookup runs under the gate and a miss REFUSES AUDIBLY (E8a) | `CanvasDeleted` | `canvasDeleteConnection` (`:131`) |
| New Canvas | `create_exclusive_reporting` with the canonical serialization as UTF-8 text (round 1 #16 — the canvas format IS text, so the reporting string API carries it; the #1123 typed outcome is real on this path) | `CanvasFileCreated`; tab opens, onboarding focused | `canvasNewCanvasFile` (`AppState+CanvasActions.swift:158`) |

**E8a — The refusal table is TOTAL, and it starts at the verb, not at
the funnel.** Every pre-funnel exit — no selection, wrong kind, no
groups to move into, no connections on the card, an unresolvable
lookup, invalid prompt input — maps to an exact existing event arm:
NothingSelected, NotAGroup, NotATextCard, NotAFileCard, NoGroups,
NoConnections, NoFreeSpaceInGroup, NotAUrl, NoChanges, ModeBusy,
UndoBlocked, RedoBlocked and the admission arms of E2 (round 1 #14 —
the never-silent table covers the verb's whole reachable surface,
and "returns without deleting" ALWAYS has a sentence). The table is
enumerated per verb in the section's task loop and each cell is a
fact.

**E9 — Placement preparation is a typed three-outcome table, mac's
overlap check included.** `canvas_place_inside_group` answers Placed
{x,y} / TooSmall {x,y} / Full. Placed commits; TooSmall runs
`canvas_check_overlap` on core's inset point and commits only when
clear, else refuses NoFreeSpaceInGroup with the label; Full refuses
NoFreeSpaceInGroup (`canvasMoveIntoGroup:432` — the shipped twin,
preserved exactly). `bad_node` / `not_a_group` arise AT the
preparation query, inside the gate, and surface through E8a's table
as one refusal (the 0b API note binds: two messages, one host
behavior, no string matching). The picker stays open across a
refusal — the user re-picks, nothing half-happened.

**E10 — Pickers: core answers order and search; the shell only
renders and filters text.** The card picker's proximity order is a
CORE EXPORT this PR adds (round 1 #21 — R-D forbids a second
distance algorithm in a host; the mac picker computes one today, so
the export lands in core, the mac picker migrates to it, and both
hosts consume one answer), with reading-order ties from the existing
projection. The vault-file picker binds to the paged FFI honestly
(round 1 #24): `list_files` pages by cursor to completion for the
filtered set, media classification (E12's export) applies BEFORE the
200-row display cap, "type to narrow" filters the complete
classified set, and a filter keystroke SUPERSEDES any page fetch
still in flight — results bind to the live picker request or are
discarded (the §C filter's supersession rule, one surface over).
The color picker's hex entry is a TYPED arm (round 1 #23): validated
as 3/6-digit hex, normalized to core's stored form, confirmed as
core's "custom color" rendering; invalid input writes NOTHING, keeps
the sheet, and shows the field-level error — no funnel entry occurs.
Every picker and prompt is a `ModalSurface` member — INCLUDING the
card editor sheet and the two nested confirmations (group-delete,
discard-draft) (round 1 #17) — and the modal-surface × chord
admission is a literal table with one tested outcome per cell: the
editor admits its own Ctrl+Z/Ctrl+Y to AvalonEdit, reinterprets Esc
as commit, and blocks canvas chords; pickers admit type-to-filter
and block canvas chords; confirmations admit only their buttons'
keys. In-window sheets, never a Popup (W4-5 D-1); focus return to
the operation's initiating surface, asserted per sheet.

**E11 — The card editor: the draft is bound to the basis that seeded
it, and Escape COMMITS.** The sheet hosts the real editor
(`AvalonDocumentBufferSession` over a scratch buffer from
`canvas_node_text`), and the editor request carries the operation
currency: document, lease, node id, and the content hash the seed
was read at (round 1 #18). Commit — Escape, per t0 M8, carved out
of the mode stack, no ladder rung, every string says "commits" —
validates that basis first: if the document reloaded or the file
changed externally since the seed, the commit does NOT apply against
the newer text; the conflict surface opens IN THE SHEET (E6, round 1
#19) with the draft intact, and RELOAD-then-recommit resolves it
deliberately. An unchanged buffer announces "No changes." and writes
nothing. Apply failure keeps sheet and draft; closing with an
unsaved draft asks (the mac Discard flow); degraded ⇒ read-only with
the reason, selectable and copyable. New Card's editor-open is an E4
effect, so it validates the initiating surface like every other.

**E12 — Core exports the media classification; the CD-38 copy
retires.** As CD-38's drift note staged: the export lands, the
shell's transliterated copy and its pin retire, the two edge rules
move into the export's tests, and the audio/video thirds get their
first cross-checked pin. Behavior unchanged: media opens externally,
everything else refuses audibly.

**E13 — The refused-open sentence gets its typed reason.** The
`CanvasBlockedReason` arm §A's flag asked for lands in the
vocabulary with its rendered sentence and corpus witness; the
shell's refusal site switches from the generic action-failed arm to
the typed one. Safety shipped in §A; the sentence ships here.

**E14 — The two staged swaps land, and the anchor caveat has an
acceptance RULE.** The empty canvas swaps to `CanvasEmptyOnboarding`
with the real New Card chord (CD-37's tie-breaker inverts). The
activation text arm swaps the interim detail for the editor sheet
(A13's staged replacement; the interim's Esc-dismisses arm retires
with it). The file arm's anchor landing is verified EXACTLY — the
heading or block anchor lands the caret per the W3-5 resolution —
and a failure STOPS for an owner decision; "record and continue" is
not an outcome (round 1 #25).

**E15 — The republish preserves user intent because the SOURCE of
expansion changes is fixed.** m5 comes due: expansion state is
preserved across every funnel republish, keyed by node id, dropping
unknown ids, session-scoped (ED-4). The §C m-5 sibling is discharged
at its CAUSE, not papered at its symptom (round 1 #26): the hidden
outline's selection synchronization is forbidden from CHANGING
expansion state — a seat on a non-showing surface seats without
expanding — so there is no unwanted expansion bit to faithfully
preserve. Both rules are facts.

**E16 — The focus-request lifecycle closes m7 atomically.** The
supersession happens IN the publication that accepts the filter
answer — the request and the filter result cannot cross (round 1
#20) — and every focus delivery validates the installed presentation
immediately before focusing (the §D install-revalidation discipline,
one surface over). A14's unfiltered landing rules stand for every
other case.

**E17 — Context menus: the rows are DERIVED, and the popup arm's
fact ships here.** Every verb reaches a context menu on the outline
row, the table row and the renderer card; the expected row set per
surface is DERIVED from the verb inventory and asserted by a drift
census (round 1 #28 — the derived-consumer discipline of §D's
enablement census), replacing §B's disabled placeholders (Delete
goes live; Toggle Mark stays disabled until G, with its reason). m11
lands: the MenuOpen popup-chain fact ships with the first real
ContextMenu. The ENVIRONMENT FACT binds — the CI desktop refuses
menu focus; the facts host a focusable element in the menu row, keep
the real MenuBase in the chain, name the leg in the premise message,
and budget a CI round trip for the first popup.

**E18 — The §W-A canvas scenarios claim what the serializer
guarantees.** Scripted `CanvasAction` sequences per fixture, executed
verbatim by both twins: for CANONICAL fixtures, final bytes and undo
round-trip bytes are byte-identical cross-platform and against
committed goldens; for FOREIGN-formatted fixtures the assertion is
semantic equality plus canonical-form byte equality after first
write (round 1 #27 — apply-plus-invert restores canonical
serialization, not foreign formatting, and the tests say what is
true).

**E19 — Menus and chords are table rows with drift tests.** Verb
rows in `ChordTable` (Canvas section; New Canvas in File), resolvers
in `SlateCommandRegistrar`, menu items mirroring mac's ownership,
drift tests 2/3 binding menu to table to palette. New chords: New
Card Ctrl+Alt+N; undo/redo per D-6. Everything else is
palette/menu/context-menu only (R1).

### Decisions

- **ED-1 (D-6 adopted).** Ctrl+Z / Ctrl+Y, imperative canvas-focus-
  scoped delivery; no Ctrl+Shift+Z alias — the editor host does not
  register one today. (This record also repairs round 1 #29's
  dangling cross-reference.)
- **ED-2.** The vault picker's display cap is 200 with "type to
  narrow", applied AFTER classification over the complete paged set.
- **ED-3.** The group-delete confirmation offers Ungroup and Cancel
  only. The algebra defines one group removal; no button promises
  more than the op does.
- **ED-4.** Expansion preservation is keyed by node id, drops
  unknown ids, and never persists across close/reopen.
- **ED-5.** The gate REFUSES concurrent verbs rather than queuing —
  mac's main-actor serialization is the semantic twin, and a queue
  would invent interleavings mac cannot produce.
- **ED-6.** Three core additions are this PR's, and no more: the
  proximity-order query (E10), the media-class export (E12), and
  the detached apply behind Save a Copy (E6). Each lands with mac
  consuming it (the 0a/0b discipline).

### IMPLEMENTATION OBLIGATIONS — the §E ledger

Round 2's findings, verbatim, backticks flattened. Each is an
acceptance criterion for the task that claims it; the task record
names the obligations it discharges and the facts that arbitrate.

- **IE-1.** [BLOCKER] [E1/E3/E6] — The operation has no explicit operation identity. Its listed fields identify document currency and intent, but not a unique invocation, so a committed-but-unpresented retry or conflict reattempt cannot distinguish the original operation from an equal later attempt or deduplicate its effects. Add an opaque OperationId, plus an attempt identity where needed, and carry it through conflict records, refresh receipts, and effects.
- **IE-2.** [BLOCKER] [E1/E2] — “Lease generation” contradicts §C-unit’s frozen rule that currency is derived by reference comparison and never carried as a scalar stamp; it reintroduces the retired handle-reuse/ABA shape. Carry the exact lease/publication reference and validate it against CanvasPublicationSlot, never a generation number.
- **IE-3.** [BLOCKER] [E1/E2/E5/E11] — The preparation/editor/history basis cannot be obtained from the current FFI. CanvasOpenInfo has no content hash, queries return no basis, and CanvasApplyResult.new_content_hash exists only after a successful write; therefore the first operation after open, editor seeding, reload quarantine, and attached-basis comparisons are unimplementable. Return the handle’s hash with the atomic open/population snapshot; that necessarily reconciles ED-6’s three-addition ceiling.
- **IE-4.** [BLOCKER] [E11] — Even after exposing a hash, canvas_node_text returns only text, so reading the basis and text in separate calls can seed old text under a new hash or vice versa. Add one locked core read returning {text, content_hash} or an equivalent immutable editor-seed token.
- **IE-5.** [BLOCKER] [E1/E2] — Reload, retarget, release, and shutdown are not stated to participate in the mutation gate. If one displaces the lease while canvas_apply is running, the call can durably commit and return success, after which E2 “swallows” the stale result, losing its inverse and announcement. Serialize lifecycle transitions with mutation commit, or publish a late-commit receipt that updates/reloads the successor while retaining history.
- **IE-6.** [BLOCKER] [E2/E3/E5] — The current canvas_apply error boundary is not a no-commit boundary. save_text_locked can write disk and then return an error before index commit, and canvas_apply also performs a fallible begin_fenced after that save; E3 records only after Ok, while E5 restores a popped entry on every error. Give canvas apply a reporting outcome equivalent to PublishedUnindexed, or make every post-write step non-failing and return a commit receipt once bytes land.
- **IE-7.** [BLOCKER] [E1/E2] — Transient exclusion is one-way. The funnel refuses a write when a foreign mode transient already exists, but a mode may start while an admitted operation is off-dispatcher; the operation then refreshes through a transient it never owned. Mode entry must atomically refuse while the mutation gate is held, using the same document transition authority.
- **IE-8.** [BLOCKER] [E1/E4] — The initiating owner is defined as “the pane and row,” but several verbs have no row—New Card from an empty canvas, menus, palette—and Delete destroys its own row before focus validation. Separate a stable surface owner from an optional source anchor and carry explicit fallback focus candidates.
- **IE-9.** [BLOCKER] [E1/E2/E10/E11] — Busy refusal preserves state only for modes, the move-into-group picker, and arguably the editor. Rename, connection, color, file, link, and New Group surfaces may close before learning that the gate refused them, losing entered data. Make every committing surface close only after recorded success; every refusal keeps its exact state, focus, and retry affordance.
- **IE-10.** [BLOCKER] [E3] — COMMITTED-BUT-UNPRESENTED is absent from E2 admission. A second write can therefore prepare against invisible core state while the user still sees the predecessor, and Undo can reverse a change that was never presented. Make it a document publication/FSM state that blocks all writes except its refresh-only recovery; the region must be the focusable last-error region and say “Refresh,” not “Reload.”
- **IE-11.** [BLOCKER] [E3/E4] — Selection can be included in an atomic publication, but editor opening, WPF focus, and announcements cannot: §D’s publication transform forbids callouts. The contract also retains no pending-effect receipt, so a failed refresh drops all effects, while a failure after publication has no state and risks duplicate announcements on retry. Split model/selection effects from addressed idempotent post-publication effects and persist their completion state under the operation identity.
- **IE-12.** [BLOCKER] [E5/E6] — Conflict retention keeps only the CanvasAction, basis, and surface, not the full operation or history policy. If Undo conflicts, E5 restores the popped undo entry; Overwrite then “records normally,” pushing another undo and clearing redo instead of performing the undo-to-redo transfer. Mode ownership and editor effects are likewise lost. Retain the complete operation, including PushAndClear, UndoTransfer, RedoTransfer, or NoHistory, mode token, and effects.
- **IE-13.** [BLOCKER] [E5/E7] — Quarantine cannot be rendered through the stated vocabulary. CanvasUndoMenuTitle only renders ordinary Undo/Redo titles, while the current UndoBlocked sentence says “Reload it and try again,” although E5 says Reload leaves the entry disabled. Add canonical quarantine title/reason arms and copy that truthfully explains when the entry can become usable.
- **IE-14.** [BLOCKER] [E2/E6] — The pending conflict record refuses every write, so Overwrite’s fresh canvas_apply is refused by its own admission rule. Clearing the record first opens an ordinary-write window. Introduce a resolution-owned admission token and keep the record in a Resolving state until the attempt atomically commits, fails, or replaces it with a fresh conflict.
- **IE-15.** [BLOCKER] [E6] — The conflict FSM is not total. Reload discards the record before reattachment, so reload failure destroys recovery; Overwrite defines commit and fresh conflict but not InvalidArgument, closed handle, I/O failure, or stale completion; resolution success/failure announcements are also unspecified. Retain the record until a terminal transition and provide a literal state × resolution-outcome × event table.
- **IE-16.** [BLOCKER] [E6] — Save a Copy has no transition for the original document. Clearing the record leaves the original stale and causes the next write to conflict again; retaining it blocks writes forever. An existing “(conflict copy)” name and PublishedUnindexed are also unresolved. Treat PublishedUnindexed as a landed copy, handle collision with a deterministic no-clobber naming/prompt loop, then reload/quarantine the original and clear the record exactly once.
- **IE-17.** [BLOCKER] [E6/E11] — Save a Copy’s “pre-conflict document” is unavailable when editor conflict follows a document reload. The editor request retains only node text and a hash; the old handle and the rest of the canvas are gone, so detached apply cannot construct the promised copy. Retain an immutable whole-canvas snapshot/capability at seed time, or remove Save a Copy from this conflict shape.
- **IE-18.** [BLOCKER] [E6] — The t0 conflict state is not added to the tab’s accessible value. An assertive event and focusable region do not satisfy t0 §3’s separate requirement that conflict remain discoverable from the tab after the transient announcement. Publish and clear the tab conflict value with the FSM transition.
- **IE-19.** [BLOCKER] [E7/E10] — The card-editor Edit-menu target is unreachable. E10 makes the editor a ModalSurface, while the ratified Windows rule and ModalSurfaceTests.TheMenuDisablesUnderEveryModalSurface disable the menu under every such surface. Remove that menu row and rely on editor controls/chords, or obtain an explicit baseline carve-out.
- **IE-20.** [BLOCKER] [E7] — Capturing only the undo domain does not guarantee “what the title names is what the click undoes.” An already-running operation or reload can change that stack while the menu is open, making the title name A while execution pops B. Snapshot the top entry identity, basis, and stack epoch, then validate that exact entry on execution.
- **IE-21.** [BLOCKER] [E1/E8] — New Canvas cannot satisfy the universal operation shape: before creation there is no document, lease, basis, or per-document gate, and it does not call canvas_apply. Define a separate vault-scoped create operation and lifecycle, explicitly outside the handle-mutation funnel.
- **IE-22.** [BLOCKER] [E8] — New Canvas requires canonical serialization, but no FFI exports canonical empty-canvas text and R-I forbids a C# serializer or host-owned serialization literal. Supply canonical text/create semantics from core or explicitly ratify a host literal; either direction conflicts with ED-6 as currently bounded.
- **IE-23.** [BLOCKER] [E8] — New Canvas merely notes that the #1123 reporting outcome exists; it does not define PublishedUnindexed. That outcome means the file is real and must not be recreated or renamed, yet opening it may require recovery because it is unindexed. Add a terminal outcome table: finalize creation once, recover indexing/opening by path, and never retry the create.
- **IE-24.** [BLOCKER] [E8] — Edit Connection contradicts the mac twin and the replacement-shaped UpdateEdge. The revision recomputes sides with canvas_auto_sides and omits color, whereas mac preserves fromSide, toSide, and color while changing only label and the four exact end-style combinations. Preserve sides/color and specify the four direction mappings verbatim.
- **IE-25.** [BLOCKER] [E8] — Remove from Group specifies directionHint: Below, but the cited mac implementation passes nil. This changes placement in ordinary canvases. Use nil or record an approved behavioral divergence.
- **IE-26.** [BLOCKER] [E4/E8] — Delete’s “mac next-or-previous rule” does not exist in the cited twin; mac clears selection. Moreover, a refreshed population cannot derive the deleted row’s predecessor/successor without pre-delete ordering carried in the operation. Match mac’s clear-selection behavior or explicitly carry an ordered survivor candidate list under the gate.
- **IE-27.** [BLOCKER] [E8a/E2] — The promised total refusal table is not present; it is deferred to a future “task loop.” The listed arms omit reachable NotInAGroup, NoNotesInVault, NoMediaInVault, NoFilesToPointAt, create collisions/reporting outcomes, paging failures, and several recovery outcomes. Gate-busy, pending-conflict-write, and committed-unpresented also lack exact current vocabulary arms. Land the literal per-verb × stage × outcome table and canonical event mapping before freeze.
- **IE-28.** [BLOCKER] [E9] — canvas_check_overlap accepts a CanvasRect, not the “inset point” E9 names. A point or zero-size rectangle can report clear while the moved card overlaps another card. Check {x, y, original width, original height} and exclude the moved node.
- **IE-29.** [BLOCKER] [E6/E10/E11] — Nested modal and conflict chord ownership is undefined. With a discard confirmation over the editor, underlying Esc still means commit unless the top surface consumes it; with a conflict region inside the editor, Esc attempts another write that the conflict record blocks. Define an explicit topmost modal stack and state × chord table, including conflict-pending and confirmation Esc behavior.
- **IE-30.** [BLOCKER] [E15] — Expansion preservation is not scoped per installed surface, although §C classifies expansion as presentation and R-B allows two panes to share one document. A document-level ID set makes a mutation in pane A overwrite pane B’s independent expansion intent. Key expansion by presentation/surface owner, then by node ID.
- **IE-31.** [BLOCKER] [E17/E19] — “Every verb” on every outline row, table row, and renderer card makes the derived context-menu census unsatisfiable: New Canvas has no canvas row, New Card must work on an empty canvas, and group/connection/file-only verbs do not apply to every row kind. Derive applicable subsets from (surface, row kind, selection capability) with explicit exclusions.
- **IE-32.** [BLOCKER] [E3/spec §1] — The frozen architecture still defines the funnel as apply → refresh → undo push → announce, while E3 requires apply → history record → refresh → effects. Every successful mutation reaches both mutually exclusive orders. Add an explicit normative precedence/supersession ruling before implementation.
- **IE-33.** [MAJOR] [E1/E2/E5] — Gate acquisition is described inconsistently as an admission check followed by “gate” acquisition. If implemented as check-then-enter, two callers can both observe free and the loser queues, violating ED-5. Specify one nonblocking atomic TryAcquire as the gate-held admission cell, followed by revalidation inside ownership.
- **IE-34.** [MAJOR] [E1/E2] — Busy refusal has no coalescing or deduplication policy. Key repeat during one slow apply can flood the announcer with identical busy events immediately before the first operation’s success announcement. Coalesce by document and gate epoch, or disable known-busy commit controls while preserving an audible first refusal.
- **IE-35.** [MAJOR] [E4] — An unresolved required effect such as “select the created node” falls through the ordinary drop rule. If refresh succeeds without the just-created ID, silently clearing selection masks an incoherent committed presentation. Treat missing required targets as committed-but-unpresented; reserve the drop rule for optional/deleted candidates.
- **IE-36.** [MAJOR] [E6] — Conflict ownership after initiating-surface destruction is unspecified. With two panes sharing the document, closing the pane that owns a conflict can leave the surviving pane write-blocked with no defined owner transfer or focus return. Make recovery document-visible in every live pane and define owner loss/transfer explicitly.
- **IE-37.** [MAJOR] [E10] — list_files paging has no snapshot identity. A rename or index change between cursor pages can make the supposedly “complete classified set” omit or duplicate files before the 200-row cap. Use snapshot-aware paging or weaken the contract to a live, revalidated set.
- **IE-38.** [MAJOR] [E10] — A filter keystroke superseding the page fetch is mismatched to the FFI: list_files has no cancellation token and its FileFilter is not the text query. Repeated typing can discard and restart full-vault enumeration, with no specified loading, disposal, or failure state. Keep one picker-generation page cache independent of the query; locally refilter it and discard only stale presentation answers.
- **IE-39.** [MINOR] [E6/ED-6] — E6 calls the media-class export “E11”; it is E12. Correct the cross-reference.

### Implementation plan — the task loop

The C-unit gauntlet, §D's third run: per task — code, facts, a
byte-restored production mutation per named fact, a record entry
appended to this section, and the citation floor raised above the
pre-task population. Core tasks land Rust + uniffi + mac consumption
+ Windows consumption together (the 0a/0b discipline).

| Task | Builds | Discharges |
|---|---|---|
| TE-0 | The core surface the model closes over: basis hash on the open/population snapshot; the locked text-plus-hash editor seed read; the canvas apply commit receipt / no-commit boundary; canonical empty-canvas text from core; the proximity-order query; the media-class export; the detached apply | IE-3, IE-4, IE-6, IE-22, E10/E12/E6's additions |
| TE-1 | The operation value (identity, owner plus optional anchor, currency by reference, effects), the per-document gate with atomic TryAcquire admission, lifecycle transitions serialized with the gate, mode-entry exclusion both ways | IE-1, IE-2, IE-5, IE-7, IE-8, IE-33 |
| TE-2 | The history domain: basis-carrying entries, restore-on-every-non-commit, the reload quarantine with its vocabulary arms, the committed-but-unpresented publication state in admission, snapshot-plus-entry-identity menu routing | IE-10, IE-13, IE-20 |
| TE-3 | The conflict machine: the retained OPERATION (history policy, mode token, effects), the Resolving admission token, the total state-by-resolution-by-event table, Save a Copy's snapshot capability and terminal outcomes, the tab's conflict value | IE-12, IE-14, IE-15, IE-16, IE-17, IE-18, IE-36 |
| TE-4 | Effects split model-side vs post-publication with completion state under the operation identity; required-target failure as committed-but-unpresented; busy-refusal coalescing and every committing surface's state preservation | IE-9, IE-11, IE-34, IE-35 |
| TE-5 | The verbs against the real algebra: Edit Connection preserving sides and color with the four end-style mappings; Remove from Group with mac's nil hint; Delete matching mac's clear-selection; the literal per-verb refusal tables; the corrected overlap check | IE-24, IE-25, IE-26, IE-27, IE-28 |
| TE-6 | The pickers: generation-cached paging with local refiltering, snapshot honesty, the hex arm, core proximity order consumed | IE-37, IE-38 |
| TE-7 | The editor and the modal stack: seed-token binding, the topmost-modal chord table including conflict-pending and confirmation Esc, the Edit-menu route resolved against the ratified modal rule | IE-19, IE-29 |
| TE-8 | Context menus from the derived (surface, row kind, capability) subsets with the census; the m11 popup fact | IE-31 |
| TE-9 | Expansion preservation per installed surface; the atomic focus-request supersession | IE-30, E15, E16 |
| TE-10 | New Canvas as the vault-scoped create operation with the terminal outcome table | IE-21, IE-22, IE-23 |
| TE-11 | The scenario goldens, the FlaUI authoring journey, and the two staged swaps with the anchor acceptance rule | E14, E18, the journeys |

IE-32 (the funnel-order precedence) is discharged by the freeze
block's ruling above; IE-39's cross-reference correction is carried
by the freeze commit itself. Order: TE-0 first (everything closes
over it); then TE-1..TE-4 (the spine); then TE-5..TE-11 in
dependency order, adjusted by the loop as learned.

### Verification plan

Batteries: gate facts (refusal while held; preparation under the
gate; no interleaving); currency facts (a stale lease swallowed at
each of the three boundaries); commit-before-presentation facts (a
refresh failure keeps the inverse; the typed state; refresh-only
retry); effect facts (declared selection atomic with the publish;
initiating-surface validation on every focus effect); undo facts
(basis-carrying entries; restore on every non-commit outcome;
quarantine after reload; snapshot routing across the four domains;
menu titles); conflict facts (the record; blocked admission; all
three resolutions; in-sheet rendering); per-verb mutation facts
(bytes, typed event, inverse restores canonical bytes, non-visual
reachability); the E8a refusal tables, one fact per cell; picker
facts (core order; paging to completion; supersession; the hex arm;
the modal-by-chord table); editor facts (basis binding; Esc commits;
no-op; failure keeps the sheet; Discard; focus return; degraded);
swap facts (the onboarding chord; activation-to-editor; exact anchor
landing); E15's two facts; E16's atomic supersede; the derived
context-menu census and m11's popup fact; New Canvas end-to-end
through the reporting outcome; the canvas scenario set green on both
twins; the FlaUI authoring journey; axe on the sheet and pickers.

### Hand-off rows

1. **To PR F:** the mode-owned commit token through the operation is
   the seam; the picker takes F's purposes as rows.
2. **To PR G:** marks verbs reuse the gate and the bulk-is-one-
   action rule; the marks list is the picker's sibling.
3. **To PR H:** E13 closes the refused-open vocabulary flag; E14's
   anchor verification outcome is recorded either way.


### Round record

**Round 1 — codex, xhigh — NOT SAFE: 21 blockers, 6 majors, 2
minors.** Revision 1 described a sequence where the platform needs
an operation with identity, currency and a lifecycle. Every FFI
claim checked — RenameGroup and Ungroup in the algebra, the
three-outcome placement, the reporting create, the paged list, the
missing proximity export, the four existing event arms — was TRUE
against the surface. Revision 2 rewrote the section around the
operation value.

**Round 2 — codex, xhigh — NOT SAFE: 32 blockers, 6 majors, 1
minor.** The count rose, and a cluster of the new blockers was
created by revision 2's own fixes: the conflict record refusing its
own Overwrite, the currency swallow discarding a durable commit's
inverse, the unrenderable quarantine, the modal editor disabling the
menu it routes through, New Canvas outside the operation shape — the
protocol's rule-5 signal. A second cluster showed the model cannot
close over the FFI as it exists: no basis at open, no locked editor
seed, no no-commit boundary on the canvas apply, no canonical create
text. Neither cluster is a prose question.

**The ruling (owner, 2026-08-31).** Freeze at revision 2; reclassify
round 2's thirty-nine findings as IE-1..IE-39; lift ED-6's ceiling
so the FFI-gap obligations add the core surface they name; run the
task loop. The §C-unit ruling, applied one round earlier because
rule 5 fired.

### Implementation record

**TE-0 — the core surface the model closes over.** Seven slices, each
Rust + FFI + consumer + facts; the ledger obligations it discharges
are IE-3, IE-4, IE-6 and IE-22, plus the three ED-6 additions.

1. **The basis at open (IE-3).** `CanvasOpenInfo` gained
   `content_hash` — one hash computation feeds the handle's CAS basis
   and the exposed field, so they cannot drift. Facts:
   open_canvas_exposes_the_cas_basis (rust — the exposed basis is the
   opened bytes' hash, and an apply's successor basis is exactly the
   next open's); the mac FFI test asserts exposure and stability; the
   Windows population carries it (`CanvasPopulation` gained
   ContentHash, fed by the load pipeline from the open info —
   `ThePopulationCarriesItsLoadBasis`). Mac's document tracks the basis
   across load, retarget, degraded and failed arms, and the funnel
   notes each apply's successor (noteApplySucceeded).
2. **The locked editor seed (IE-4).** `canvas_editor_seed` returns
   text and basis as one value taken under the registry lock —
   canvas_editor_seed_pairs_text_with_its_basis pins the pairing, the
   successor-basis reseed and the None-for-a-group arm. Mac's
   canvasEditCard migrated off the bare text read, and its editor
   request now carries the basis; the two create-and-edit verbs pass
   the post-apply basis the funnel noted.
3. **The commit receipt (IE-6).** From write success to index commit,
   every save failure is now typed `SavedButUnindexed` carrying the
   landed hash — the durable write-intent marker repairs the index;
   the caller must treat the state as a COMMIT. `canvas_apply`
   consumes the arm: it reports success with `indexed: false`, the
   handle advances (model falls back to a DB-free derive when the
   index cannot be read), and
   canvas_apply_reports_a_landed_write_whose_index_failed drives the
   fault seam end-to-end — the second apply proceeds on the landed
   basis with no false conflict, which is the double-apply hazard the
   obligation names. The seam's own error became the typed arm; its
   existing tests assert is_err and were unaffected.
4. **The canonical empty document (IE-22).** Core exports the
   New-Canvas bytes (canonical_empty_canvas_text — exactly the
   serialization of the default canvas, "{}\n"); mac's Swift literal
   migrated to the export, and the FFI test pins the bytes.
5. **The proximity order (ED-6/E10).** Core owns the one distance
   algorithm: squared distance from the anchor's centre, ties by
   READING order — which is geometry-derived, so the tie-pair fixture
   differing only in document order answers identically — groups
   included, reading order itself when no anchor resolves (mac's
   shipped fallback). The Windows picker consumes it in TE-6; the mac
   picker's migration is recorded as owed to keep this task's diff
   reviewable, and the export's behavior is pinned in rust either way.
6. **The media classification (ED-6/E12, CD-38's staged note).**
   `media_class` and its class enum went public and crossed the FFI;
   the Windows gate's transliterated set, its ASCII-lowering helper
   and the kind-label detour pin
   (TheImageThirdOfTheGateAgreesWithCoresOwnKindLabel) all retired.
   The behavior table (`TheMediaGateIsCoresClassification`) now pins
   the set THROUGH the export, audio and video thirds included — the
   pin the detour could never give them. Mac's gate remains CD-38's
   recorded divergence; the mac FFI test consumes the export's edge
   rules.
7. **The detached apply (ED-6/E6).** `canvas_apply_detached` — parse
   refusing a degraded document, apply, serialize; no session, no
   handle, no write. Facts pin the transform, purity and the refusal.
   Its consumer is TE-3's Save a Copy; landed here because the
   conflict machine's design closes over it.

The windows suite (with the error-mapping census's pinned arm set
grown to 18 for the new save arm), the rust canvas and query
batteries and the format gates ran green. Five dir-tree censuses
fail on THIS BOX only — harness-side os error 123 writing the
deliberately hostile fixture names, unchanged since #1028; CI's
rust lanes are the oracle and run them green (recorded in the
session memory). The mac edits compile on CI's Swift lane (this box
builds no Swift).
Mutations: one per named fact class, injected into production and
byte-restored — the table below names them.

| Mutation | Injected | Named fact that failed |
|---|---|---|
| open hash decoupled | info.content_hash emptied | open_canvas_exposes_the_cas_basis |
| seed pairing torn | seed basis emptied | canvas_editor_seed_pairs_text_with_its_basis |
| proximity tie untied | tie key constant | proximity_order_sorts_by_distance_with_reading_order_ties |
| canonical bytes bent | trailing newline dropped | canonical_empty_canvas_text_is_the_default_serialization |
| dotfile rule dropped | hidden-file guard removed | media_class_answers_by_the_basenames_real_extension |
| degraded refusal skipped | parse warnings ignored | apply_detached_refuses_a_degraded_parse |
| receipt untyped | seam reverted to the generic error | canvas_apply_reports_a_landed_write_whose_index_failed |

**TE-1 — the operation value and the gate.** The spine's types, bound
and pinned; discharges IE-1, IE-2 and IE-33, and types IE-7's one-way
half and IE-8's owner/anchor split.

- **The operation** (`CanvasMutationOperation`): identity is an opaque
  reference (`CanvasOperationId`, the request-identity discipline —
  equal inputs mint DISTINCT invocations, pinned); the initiating
  surface is a required owner with a NULLABLE source anchor (IE-8 —
  New Card on an empty canvas, menus and the palette have no row);
  currency is the captured `CanvasLoaded` REFERENCE and the one
  boundary question is reference equality against the live
  publication (IE-2 — no stamp, no counter, no ABA window; the §C-unit
  frozen rule, obeyed rather than reinvented); the typed effect enum
  (`CanvasMutationEffect`) declares E4's table with mac's real delete
  behavior as the arm name (ClearSelection — IE-26 recorded in the
  type); the mode token types C7's retry rule (IE-7's one-way half —
  the funnel's transient guard admits exactly the operation carrying
  the live mode's token; PR F consumes).
- **The gate** (`CanvasMutationGate`): one per-document cell, one
  ATOMIC compare-and-swap acquisition (IE-33 — check-then-enter is
  unspellable), refusal-not-queue (ED-5), the holder as the whole
  state (identity and epoch in one reference), and a holder-only
  release whose violation throws the model's unsurvivable tripwire
  (`CanvasLeaseViolationException`, excluded from every survivable
  catch by `CanvasFaults`).
- **IE-5's ruling applied:** of the obligation's two remedy arms,
  this design takes the SECOND — lifecycle transitions stay on the
  slot's own CAS, and a displacement during an off-dispatcher apply
  is answered by the operation's completion RECEIPT retaining the
  committed hash and inverse for the history domain (TE-0's
  `SavedButUnindexed` sibling, host-side); TE-2 records the entry,
  TE-4 owns the surfaced state. The gate therefore never blocks a
  reload; it makes the stale result harmless-but-retained.
- **Consumed by:** TE-5 wires the verbs and both directions of the
  mode exclusion at the one seam that owns both objects; TE-2/TE-4
  consume the receipt and effects.

Facts: the four in `CanvasMutationGateTests`. Mutations: the
acquisition rewritten check-then-enter (the admit-exactly-one fact
failed); currency reduced to liveness alone (the basis-reference fact
failed); both restored byte-for-byte.

**TE-2 — the history domain.** Discharges IE-9's structural rule,
IE-10 (both halves), IE-13 and IE-20.

- **The stack** (`CanvasUndoStack`, entries as `CanvasHistoryEntry` —
  name, inverse, BASIS): the two-phase checkout makes "transfer only
  on success, restore on every non-commit outcome" structural — a
  checkout must end in exactly one commit or restore, phase-two
  without phase-one is the unsurvivable tripwire, and structural
  motion while one is open is too. The QUARANTINE is one comparison:
  an entry is offered only while its basis equals the attached one;
  a reload that attaches a foreign revision disables rather than
  offers (the old inverse must never pass the new CAS by
  coincidence), and the exact basis returning re-offers. The EPOCH
  plus the entry reference make IE-20's menu snapshot
  (`CanvasHistorySnapshot`) one integer and one reference compare at
  execution — a moved stack refuses the stale title instead of
  undoing whatever is on top now.
- **The publication state (IE-10):** `CanvasPublication` gained the
  committed-but-unpresented operation id — a spellable document
  state set when a commit's refresh fails, carried untouched across
  unrelated publications, cleared only by the refresh-only recovery;
  the funnel's admission (TE-5) reads it, the model census admitted
  the field.
- **The vocabulary (IE-13):** two blocked-reason arms with TRUTHFUL
  copy — "Undo unavailable: the canvas changed on disk, and this
  entry applies to an earlier revision." (nothing re-enables it but
  that revision returning; the shipped Blocked pair's "try again"
  stays for the pre-quarantine refusal it correctly describes) — and
  the label arm CanvasHistoryQuarantinedTitle for the menu while
  nothing is offered. Five places: the core enum and renderings, the
  cardinality and roster censuses, the regenerated corpus artifact
  (four entries), the FFI mirrors, and BOTH host corpus censuses in
  corpus order. The Windows SPEAKER of these arms arrives with the
  funnel's menu-title wiring (TE-5/TE-7); the arms, renderings and
  censuses are pinned now.

Facts: five in `CanvasUndoStackTests`, the publication-state fact in
the gate battery, and the corpus lockstep across three languages.
Mutations: the quarantine comparison forced true (the foreign-basis
fact failed); the publication state dropped by the carrying copy (the
spell-and-clear fact failed); and the snapshot epoch check dropped —
which the stale-menu fact SURVIVED on the first run, because the
reference compare already covers a pushed-over top. The fact was
strengthened with the scenario only the epoch can refuse — a
checkout/restore cycle putting the SAME entry back on top, where a
pre-cycle snapshot would execute it twice — and the re-run mutation
failed exactly that arm. Each restored byte-for-byte; the epoch is
load-bearing and now provably so.

**TE-3 — the conflict machine's record and its one door.** Types and
pins IE-12, IE-14 and IE-17, and the state-machine halves of IE-15;
the FFI-driven resolution arms, the tab's conflict value publish and
the owner-transfer rule (IE-16, IE-18, IE-36) are consumed by the
funnel task, which owns the seams they publish through.

- **The record** (`CanvasConflictRecord`): one WriteConflict's WHOLE
  context, retained until a terminal transition — the full operation
  (owner, effects, mode token: IE-12's "retain the complete
  operation", satisfied by carrying TE-1's value), the attempted
  action and name, the failed basis, the typed HISTORY POLICY
  (push-and-clear, undo-transfer, redo-transfer, no-history — the
  round-2 finding that an Overwrite after a conflicted undo must
  transfer, not re-push), and the PRE-CONFLICT SNAPSHOT.
- **The snapshot (IE-17)** closes over a new core read:
  canvas_current_text — the handle's in-memory document serialized
  under the registry lock, paired with its basis. At conflict time
  that IS the pre-conflict revision (the refused apply moved
  nothing), so Save a Copy's detached apply has its text even after
  a document reload destroyed every other route to it. The rust fact
  pins capture-at-open, canonical round-trip, and the successor
  after an apply; the FFI mirrors it.
- **The door (IE-14):** TryBeginResolving admits exactly one
  resolution; a second while one runs refuses, a terminal record
  admits none, and every terminal transition — resolved, failed-back
  -to-pending (the record whole, recovery intact: IE-15's no-outcome
  -discards-recovery), replaced-by-a-fresh-conflict — requires the
  OWNING resolution, with the unsurvivable tripwire for anything
  else.

Facts: three in `CanvasConflictRecordTests`, one in the rust canvas
battery. Mutations: the door's guard removed (the opens-once fact
failed); a failure made terminal (the record-whole fact failed);
both restored byte-for-byte.

**TE-4 — the effects split.** Discharges IE-35 and IE-34, and types
IE-11's two halves and IE-9's refusal outcome; the funnel task wires
the surfaces that keep their state.

- **The model-side half** (`CanvasEffectPlan`): selection resolution
  as a pure function over the REFRESHED population, callable inside
  the publish transform so rows and seat install in one swap (§D's
  no-callouts rule respected by construction). The four arms: keep
  resolves the current intent — and a deleted seat resolving to null
  is the TRUTH, the drop rule's proper home; clear seats null by
  declaration; select-created resolves the created id, and a
  REQUIRED target the refresh cannot resolve is the typed
  required-target-missing resolution (IE-35) — the
  committed-but-unpresented signal, never a silent clear; an
  undeclared arm is the tripwire.
- **The post-publication half** (`CanvasOperationCompletion`): the
  addressed effects' completion state under the operation identity —
  announce, editor-open, focus-return each at-most-once through an
  interlocked mark, so a retry after a failed refresh re-runs only
  what never ran and a duplicate announcement is unspellable
  (IE-11's persistence clause).
- **The busy gate** (`CanvasBusyGate`, IE-34): one audible refusal
  per HOLD, keyed by the held operation REFERENCE — no timer, no
  announcer class: key repeat under one slow apply speaks once, and
  two distinct holds 50 ms apart are two true refusals that both
  speak. The surviving surface state is IE-9's rule; the funnel
  wires it and this type answers only "is this refusal the audible
  one".

Facts: four in `CanvasOperationEffectsTests`. Mutations: the
required-target arm rewritten as the silent drop (the typed-failure
fact failed); the completion mark stripped of at-most-once (the
marks fact failed); the busy gate's epoch comparison removed (the
once-per-hold fact failed); each restored byte-for-byte.

**TE-5a — the funnel's spine.** The one `canvas_apply` call site
(R-A) as a class, integrating every prior task's type; discharges the
admission and transaction halves of the E1–E3 contracts and IE-5's
receipt arm end-to-end; the verbs, their surfaces and the real-vault
batteries are the loop's next slices.

- **Admission is E2's table in order** — not ready, recovery pending
  (the committed-unpresented state), conflict pending (with the
  resolving door's pass-through token), mode held (the TE-1 token
  seam), stale (currency by reference at mint), then ONE atomic gate
  acquisition whose refusal consults the TE-4 busy gate: audible
  once per hold, and never a queue. Two FFI seams enter the ctor —
  `ICanvasLoadSource`'s refresh trio REUSED, plus apply and the
  snapshot read on the two-method `ICanvasMutationSource`.
- **The transaction is E3's order with the gate held throughout**,
  released in a finally: apply through the lease (currency re-checked
  inside the FFI lock), the history entry recorded before anything
  fallible, the refresh read under the lease again, and ONE publish
  installing rows and the effect-plan seat together on the SAME
  loaded reference. A required-missing seat marks
  committed-unpresented instead of publishing (IE-35 wired).
- **The three failure arms, each typed:** WriteConflict builds the
  retained record AT refusal — snapshot taken with the gate held —
  and announces the conflict; SavedButUnindexed records the landed
  commit and marks recovery (TE-0's boundary honored host-side); a
  mid-apply displacement retains its receipt through a NEW
  non-rebasing push. The battery caught the first cut rebasing the
  stack onto the displaced entry's own basis — self-defeating the
  quarantine — and PushRetained is the repair: the receipt lands
  under the displacing reload's basis and quarantines by the same
  comparison that admits current entries.

Facts: six in `CanvasMutationFunnelTests` over scripted seams (the
run seam inline, the write seam scripted per call — the filter
machine's source-free discipline). Mutations: the E3 order inverted
(the record-before-fallible fact failed); the conflict arm dropped
from admission (the conflict fact's refusal tail failed); the
displaced arm made rebasing again (the receipt fact failed); each
restored byte-for-byte.

**The wall censuses' catch, recorded as the round it was.** The
first cut minted its own population and published `WithLoaded`
directly, and the full suite's two wall censuses refused both —
correctly: publishing around the transfer's wall had silently LOST
the filter-reseed rule, so a mutation under an active needle would
have unfiltered the projection. The reroute is the deep fix: the
transfer gained `Republish` (a `CanvasRepublishOutcome` naming the
reseed the caller must start), the pipeline gained
`RefreshAfterMutation` (the mint inside its wall, the reseed
callback fired exactly as the acceptance path's), and the funnel's
refresh became one call through both. The refresh outcome enum
lives beside the admission enum in the funnel's own file — the
model's closed world stays closed, and its authority walk showed
why an enum does not belong in it. One suite run was pushed red
before this landed; the fix commit follows it directly.

**TE-5b — the funnel enters the document, and the first three verbs
run on a real vault.** The prepare seam, the confirmation seam and
the intent-provenance seat rule; E4's declared effects observable
end-to-end.

- **Prepare under the gate:** the funnel's Apply now takes the
  preparation as a function of the handle, run inside the SAME lease
  hold as the apply — round 1's #5 closed structurally (a placement
  answered and a write fed in one hold; nothing moves between). The
  confirmation seam speaks the verb's typed event only for a commit
  whose presentation INSTALLED, with the operation carrying the
  created id the SelectCreated effect resolves (IE-35's required
  target, threaded).
- **The document integration:** the history domain, the gate, the
  busy gate and the funnel live on `CanvasDocumentViewModel`, built
  over a two-method mutation source beside the load source, riding
  the SAME tracked worker the filter rides (the projection posted
  after every job, inline in synchronous tests). Every ready publish
  rebases the history domain to the population's basis; retirement
  clears it.
- **The intent-provenance seat rule** — the slice's design catch:
  A12's survivor rule kept the OLD seat over a created card, because
  the created id survived nothing (it was born) while the anchor
  survived everything. The distinguisher is provenance: the slot's
  selected INTENT changes only through deliberate publishes, and a
  user's own move publishes an intent equal to the seat it came
  from — so "the publication carries a new intent" fires exactly
  for the funnel's declared effects and never for the T3-protected
  mid-flight move. PublishReady seats the unit's resolution on that
  arm and keeps the survivor rule on every other.
- **The verbs:** New Card (core placement at the selection anchor,
  defaults from the constants, landing selected in one publish),
  the editor's commit (SetNodeContent; the no-change arm is the
  editor task's), and Delete's card arm (mac's clear-selection
  behavior, typed; the group's Ungroup/Cancel confirmation and the
  connection arm ride the next slice with their tables). Each fact
  drives disk bytes, the rendered confirmation, the seat, and the
  inverse restoring the EXACT prior bytes through the two-phase
  checkout.

Facts: three in `CanvasMutationTests` over a real `VaultSession`.
Mutations: the provenance arm dropped (the New Card fact failed on
the unseated card); the created id untreaded into the effect plan
(the same fact failed through the required-target path); and a
finding worth its line — Delete's effect flipped to KeepSelection
did NOT bite, because a verb that deletes its own seat makes the
two arms indistinguishable: the drop rule truthfully resolves the
dead intent to null either way. The ClearSelection arm stays as the
type-level record of mac's behavior, the non-biting flip is
recorded rather than laundered, and the third mutation became the
undo-hint wire instead (the hint constant cut at the verb site —
the delete fact failed on the sentence). Each restored
byte-for-byte.

**TE-5c — the remaining single-shot verb commit paths.** Eleven
verbs now run the funnel end-to-end on a real vault; labels, targets
and picks arrive as parameters, exactly as the editor's commit takes
its text — the sheets and pickers that gather them are the next two
tasks' (IE-8's owner/anchor split carries the surface when it
exists). Discharges IE-23, IE-24, IE-25's direction and the
placement half of IE-15; E8a's tables remain the wiring slice's.

- **Group verbs:** New Group at core's group defaults; Rename
  through the REAL op (IE-23 — never SetNodeContent on a group);
  Ungroup as the delete verb's group arm (the algebra's one group
  removal: frame and incident edges go, cards stay — pinned by the
  keeps-cards fact; ED-3's two-button sheet is TE-7's, this is its
  commit path). Move into Group is the typed three-outcome
  preparation, mac verb for verb: Placed commits; TooSmall commits
  only when core's overlap check clears the inset; Full — and an
  occupied inset, driven by the fixture's Cramped group and its
  blocker card — refuses audibly with the label, from PREPARATION,
  inside the gate, writing nothing and recording nothing.
- **Color:** preset or validated hex through one grammar
  (IsCanvasColor); the confirmation speaks core's NAME, pinned
  against the digit never appearing in the sentence; an invalid hex
  never reaches the funnel.
- **Connections:** Connect defaults sides by core over the two rects
  (0b-3) with mac's end styles; Edit Connection changes label and
  the four end-style mappings ONLY, the author's sides and color
  PRESERVED from the `CanvasSceneEdge` (IE-24, pinned byte-level); Delete
  Connection looks up BEFORE the apply and a miss refuses AUDIBLY
  (0a-2 — the mac trailing-space sentence unreachable), with the
  `CanvasConnectionDirection` mapping and the edge-direction table
  made total over the arrow pair.
- **Add and locate:** file cards with subpaths, link cards behind
  the NotAUrl gate, and Locate repointing a missing target with the
  typed retarget confirmation. The slice's wart worth its line: the
  first cut's confirmations read the VM's row mirror, which
  refreshes only when the posted projection applies — AFTER the
  transaction — so a created card's sentence spoke its raw id. The
  confirmations now read the PUBLISHED population (an Outline scan;
  the model's surface stays closed), and the add-file fact pins the
  humanized title in the sentence.

Facts: eleven more in `CanvasMutationTests` (fourteen total), the
fixture grown to two groups, a blocker card and a sided, colored,
labelled edge — written as a canonical-form raw literal after the
foreign-format attempt failed exactly as E18 predicts (undo restores
canonical bytes, so a non-canonical fixture cannot round-trip).
Mutations: the preserved sides and color recomputed to null (the
preservation fact failed); the missing-edge refusal silenced (the
audible-refusal fact failed); the URL gate dropped (the link fact
failed on bytes that should not exist); each restored byte-for-byte.

**TE-6 — the picker models.** Discharges IE-37 and IE-38 and
consumes TE-0's proximity export; the sheets, their XAML, the
`ModalSurface` membership and the modal-by-chord table are TE-7's,
which owns the modal stack whole.

- **The card model** (`CanvasCardPickerModel` over
  `CanvasCardPickerRow`): CORE owns the order — the factory reads
  `canvas_proximity_order` through the lease (anchor = the
  selection, reading-order ties, groups included) and the model only
  renders and text-filters, order preserved, no comparator anywhere
  (R-D's second-algorithm ban, pinned by the real-vault fact that
  compares the factory's rows against the FFI answer VERBATIM).
- **The file model** (`CanvasVaultFilePickerModel`): ONE GENERATION
  per open — the paged listing walks to completion into the
  generation cache; classification applies BEFORE the display cap
  (ED-2 — the fact drives a media file arriving beyond two hundred
  markdown rows and finds it admitted); a filter keystroke refilters
  the CACHE locally and the page seam's call count stays flat
  (IE-38's never-restart rule); and a pick admits only against the
  generation it was shown from — the model reference IS the
  generation, so supersession is one reference compare (IE-38's
  discard rule). Snapshot honesty stands in the type's own record:
  the set is the walk's, revalidated by the verb it feeds.
- **The factories** on the document build both: proximity through
  the lease with the palette-shaped labels, and the vault walk with
  notes-vs-media admission (markdown for Add Note; core's
  classification over the FFI for Add Media).

Facts: four in `CanvasPickerTests` over scripted page seams plus the
real-vault factory fact. Mutations: the model given a host
comparator (the no-reordering fact failed); the generation wire cut
(the stale-pick fact failed); and the cap-order mutation earned a
correction — capping the CLASSIFIED count did not bite, because one
admitted media file never reaches any cap; the hazard ED-2 names is
capping the RAW walk, and injecting exactly that stopped the paging
two pages early and failed the beyond-cap media fact. Each restored
byte-for-byte.

**TE-7 — the card editor and the modal stack.** Discharges IE-19's
in-sheet rule and IE-29's menu-route resolution, and consumes the
seed token (IE-4/IE-18) end-to-end; M8 lands as code.

- **The model** (`CanvasCardEditorViewModel`): the SEED TOKEN whole —
  node, title, text and basis from one locked read via the document
  factory — and Escape's three arms: an untouched buffer speaks "No
  changes." and writes nothing; a draft whose seed basis the live
  population no longer holds does NOT apply — the conflict surfaces
  IN the sheet, the draft survives, the sheet refuses to close
  (IE-19; the fact drives another verb's commit between seed and
  Escape and finds the stale text absent from disk); a current draft
  commits through the funnel verb. Every string says commits; no arm
  cites M2.
- **The membership, whole:** the sheet is a WORKSPACE property — the
  modal machinery's own censuses refused the first document-held cut
  and enumerated the real integration: the enum member, the state
  record field, the IsOpen arm, the name-matched flat read, the
  sheet-presentation observer arm, the Menu.Style disable trigger,
  the decision rows in every admission table, and the REAL sheet
  element in `MainWindow.xaml`, declared after MoveTo's so the
  topmost-surface order stays the declaration order. The palette
  refuses beneath it like every sheet.
- **IE-29 resolved without a carve-out:** the ratified rule disables
  the menu under every modal surface, the editor included — its
  undo/redo ride the editor's own machinery inside the sheet, and
  the canvas stacks' menu titles apply only when no sheet is open
  (TE-2's snapshot rule already reads the captured domain).

Facts: four in `CanvasCardEditorTests` over a real vault, plus the
modal batteries' derived censuses now counting the editor among the
sheets (one hundred forty-four across the three). Mutations: the
basis re-validation dropped (the moved-basis fact failed); the
no-change arm cut (the writes-nothing fact failed on the history
entry); the conflict made to close over the draft (the keeps-draft
fact failed); each restored byte-for-byte.

**TE-8 — the context menus from the one plan.** Discharges IE-31 and
lands m11's popup fact beside the classification it exercises.

- **The plan** (`CanvasContextMenuPlan` over `CanvasContextMenuRow`):
  rows derived from the row's KIND with explicit exclusions — mac's
  leading pair, then the kind's own verbs; a group's removal is
  Ungroup and Delete is ABSENT there (ED-3's algebra, in the table
  rather than a button's fine print); Delete is LIVE on card kinds;
  Toggle Mark stays staged for PR G with its why. Two honesty calls
  recorded as decisions: Rename Group and Set Color ship their
  COMMIT paths but not yet their prompt sheets, so their rows stand
  visible-and-staged with "Its prompt arrives with a later slice." —
  the mac contract's temporarily-unavailable shape, never a dead
  click — and the grid's group-row Delete stays disabled with "A
  group is removed by Ungroup — its cards stay." replacing the
  retired staging reason.
- **Two consumers, one derivation:** the grid's row actions flip
  Delete live (seat the row, then the funnel verb — the
  acts-on-its-row rule) and the outline's context menu builds
  lazily during ContextMenuOpening from `BuildMenuFromPlan` — the
  SAME mapping the census fact drives, so the built rows cannot
  drift from the plan. Edit Card routes through a document-raised
  request the workspace answers (the sheet lives on the workspace;
  the modal censuses' convention from TE-7, reused rather than
  bypassed). The §B-era fact that guarded the STAGED Delete flipped
  to guard the live truth from the other side.

Facts: three in `CanvasContextMenuTests` (the plan's tables, the
no-silent-staging rule, the derived equality), the flipped table
fact, and m11's popup-chain fact in the navigator battery.
Mutations: the plan's Delete arm dropped (the tables fact failed);
a staged reason nulled (the no-silent-staging fact failed); the
builder hand-listed a kind (the equality fact failed); each
restored byte-for-byte.

**TE-9 — expansion preservation per installed surface; the atomic
focus-request supersession (IE-30, E15, E16).** The outline view
gained an expansion memory — a per-VIEW dictionary, which is per
installed surface by construction, so IE-30's two-pane overwrite
cannot be written at all: pane A's collapse and pane B's open state
live in different objects. `Rebuild` captures group rows' expansion
before teardown, prunes the memory against the POPULATION rather
than the displayed rows (a group hidden by a filter keeps its
remembered collapse for the filter's clearing; an id the canvas no
longer contains is forgotten — ED-4's drop rule), and the
default-open rule consults the memory before it opens anything. The
cause side of E15 landed where the contract put it: the
connection-host expansion in the selection sync is gated on the
outline being the ACTIVE surface, so a seat synchronized into a
hidden outline seats without expanding and there is no unwanted bit
for the preservation to faithfully keep. E16's supersession sits IN
the publication that accepts the filter answer: a pending
`CanvasFocusRequest` naming a node the ANSWERED unit's
`FilteredOrder` excludes is cleared before `OutlinePublished` fires —
the request and the result meet atomically and cannot cross (m7's
surprise jump), while a FAILED answer keeps its rows and supersedes
nothing, and a surviving node's request outlives the answer for a
surface to deliver. Five facts: the collapse that survives a
republish; the two-pane independence; the hidden-outline seat; the
superseded excluded-node request; the surviving request. Three
mutations, each byte-restored: the default-open rule stopped
consulting the memory and the republish re-opened the reader's
collapse; the ActiveSurface gate dropped and the hidden seat
expanded; the supersession block deleted and the excluded request
out-slept the answer. Not taken: a live expansion tracker mirroring
every toggle — the capture-at-rebuild reads the row VMs' final
state, which the TwoWay binding already holds, so a tracker would be
a second copy of a truth the view owns.

**TE-10 — New Canvas as the vault-scoped create operation (IE-21,
IE-22, IE-23).** The operation lives where IE-21 put it: on the
sidebar, beside the note and folder creates, deliberately OUTSIDE
the handle-mutation funnel — before creation there is no document,
lease, basis or gate to run it through. The flow is mac's
canvasNewCanvasFile verb for verb: `UntitledCandidates` walks
"Untitled Canvas.canvas" then "Untitled Canvas 2.canvas" onward,
advanced ONLY by the typed `DestinationExists` (never a pre-check);
the bytes are core's `CanvasCanonicalEmptyText` (IE-22 — no host
literal); a create is a `StructuralHistoryBarrier`; the new document
opens in its OWN tab (mac's rule: replacing the current tab could
destroy an unsaved buffer's only owner). IE-23's terminal outcome
table reuses the #1123 helper the note create already trusts,
`CreateOutcomes.CreateReporting` over `CreateExclusiveReporting`: a
refusal stops with the failure spoken; a LANDED write — committed or
published-but-unindexed — finalizes the creation exactly once, opens
the real file by path, speaks the caveat AFTER the created sentence,
and never retries under another name; the NEXT invoke advances past
the landed file by the disk gate's own refusal. The sentence is
core's `CanvasFileCreated` render — announced once as the canvas
EVENT through the sidebar's own sink, with ReportResult's status
discipline inlined so the corpus row and the status line are one
sentence, not two compositions. The command is a registered,
chordless FILE-section row on both hosts (`slate.file.newCanvas`;
mac registers section .file with no chord), a File-menu item below
the template item, a palette row by derivation, and NOT in
`SidebarPinnedOrder`, which is Sidebar-section only; chords.json
reprojected by its own gate. Four facts: the canonical document in a
new tab with the spoken sentence; the typed advance past an occupied
name; the landed-but-unindexed create that opens and is never
recreated, with the SLATE_TEST_FAULT_AFTER_WRITE seam driven
end-to-end; the chord-row registration shape. Three mutations, each
byte-restored: the content argument swapped for an empty host
literal and the byte fact failed; the landed arm taught to advance
the sequence and the no-duplicate fact failed; the announce dropped
and the spoken-once fact failed.

**TE-11a — E19's wiring and E14's first swap.** The deferred surface
slice lands: `slate.canvas.newCard` is a registered Canvas-section
row with the series' ONE new verb chord — mac's ⌥⌘N as Ctrl+Alt+N
(mac's own #368 allocation keeps Ctrl+N for notes) — a File-menu
sibling in the Canvas menu, a palette row by derivation, and a
resolver over a workspace command gated only on a canvas being
active, because the funnel's admission table owns every other
refusal and speaks it (C9). The history domain went live as ED-1
ruled it: Ctrl+Z and Ctrl+Y are chord-only `ChordScope.Canvas` rows
(the structural pair's shape one scope over; mac routes ⌘Z through
the responder chain, so neither host registers a command), delivered
through the navigator's ladder into `CanvasUndo`/`CanvasRedo` — the
OFFERED entry snapshotted, checked out under the gate, its inverse
applied through the one funnel via `ApplyHistory` (the admission
ladder extracted and shared, not duplicated), and the receipt
crossing to the OPPOSITE stack so the redo pile survives, which is
exactly what the verb path's clear-redo recording must never do
here. A raced mutation displaces the snapshot and the entry returns
byte-exactly where it was (IE-9); the blocked and empty-stack arms
speak core's sentences. The redo divergence is recorded in the
census's pinned set (⇧⌘Z predicts Ctrl+Shift+Z; Windows keeps
Ctrl+Y, and ED-1 registers no alias). E14's first staged swap rode
the chord it was waiting for: `EmptyOnboardingText` now renders
`CanvasEmptyOnboarding` with the SPELLED-OUT chords a screen reader
actually receives ("Control Alt N", "Control Shift P" — 0a-13), and
CD-37's tie-breaker inverts exactly as its ruling promised; the
guarding fact flipped from NOT-the-event to the event's own render.
The test suite's manual undo plumbing retired for the real verb, so
every per-verb undo fact now runs gate, checkout, apply and refresh
end to end. Five facts (the verb round-trip with both spoken
sentences; the empty-stack arms; the moved-disk block that retains
the entry; the ladder delivery of all three chords; the flipped
onboarding render). Three mutations, each byte-restored: the
conflict arm's `RestoreCheckout` dropped and the retained-entry fact
failed; the empty-stack sentence dropped and the status fact failed;
the onboarding chord swapped for its glyph form and the render fact
failed. chords.json reprojected by its own gate. Owed onward within
TE-11: the activation-to-editor swap, the anchor acceptance rule,
the E8a tables, the scenario goldens, the journeys.

**TE-11b — E14's second staged swap: activation opens the real
editor, and the interim detail retires whole.** Activating a text
card now asks for the editor through the TE-8 seam — the document
seats `LastActivatedNode`, raises the request, and the workspace
opens the sheet whose modal machinery owns focus; `OpenCardEditor`
owns every refusal with its spoken arms (not-a-text-card,
unreadable), so the activation path carries no second refusal
vocabulary. The A13 interim retired WHOLE, not half: the
`DetailShown` arm became `EditorRequested` (the view does nothing —
the workspace answers); the document's DetailText/DetailTitle pair,
their reload clear and the close verb are gone; the surface's detail
region — fields, construction, docking, property-change arm, apply
site, focus hop and test accessor — deleted; both row surfaces'
DetailRequested events deleted; and the Escape rung retired exactly
as its own comment promised, because t0 §2 M8 carves the editor OUT
of the mode stack: Escape COMMITS there (C6 — porting the arm
forward would throw away a user's typing), and the sheet's
focus-return owns m6's trap, pinned since TE-7. The facts flipped to
the new truth: activation asserts the raised request, the seat and
the seed's text through `CanvasCardEditorViewModel.Draft`; the
table's text arm, its group fall-through and its Open row action
assert the request; the interim's Escape keyboard-trap fact retired
WITH its subject. Three mutations, each byte-restored: the
activation stopped raising and the request fact failed; the seat
dropped and the landing fact failed; the event raise deleted and the
table's seam fact failed.

**TE-11c — E8a's never-silent table: the funnel half and the verb
half.** The admission ladder SPEAKS now. Every refused admission
announces its typed event at the refusal site, shared by both
entrypoints: NotReady renders mac's exact reason table over the
publication's load state through one derivation
(the `CanvasMutationFunnel` ladder's `AnnounceAdmission` and its
NotReadyReason — Loading is Opening, the
parse error is ReadOnly, a lost retarget is RetargetFailed, the rest
Unavailable); a pending conflict re-speaks the conflict's own event;
a foreign mode transient and a held gate speak the mode sentence,
the gate once per hold through the existing dedup; Stale stays
silent BY CONTRACT (E2 — a displaced operation swallows). The
committed-but-unpresented arm had no true sentence anywhere, so the
freeze-lifted ceiling paid for one CORE addition:
`CanvasMutationRefusal`'s RefreshPending arm, rendered "Your last change
is saved but not shown yet. Refresh to see it before making more
changes." — IE-10's say-Refresh-not-Reload ruling verbatim — carried
through the TE-4 playbook's places (enum, render, witness, golden
expected row, the cardinality group, the regenerated corpus, both
hosts' corpus censuses, the uniffi mirror, regenerated bindings; mac
renders refusals through core, so no Swift switch widened). The verb
half: fourteen silent guard exits split and spoken — the no-basis
short-circuits speak the SAME derivation the ladder uses
(`SpeakNotReady`, so guard and admission can never disagree);
no-selection and vanished-endpoint exits speak `CanvasStatusNote`'s
NothingSelected; an unknown group speaks NotAGroup; the two guards no surface can reach
(delete-on-group behind ED-3's routing and the fixed-palette color
check) are recorded as guards, not cells. Facts: the not-ready,
refresh-pending and foreign-mode admissions each pinned to their
sentence; the busy hold's once-audible refusal; the pending
conflict's audible second refusal; the verb cells counted per
sentence through the coalescing announcer's flush; the unready-verb
refusal end to end. Three mutations, each byte-restored: the ladder
announce dropped and the not-ready fact failed; one guard's sentence
dropped and its cell failed; one no-basis speak dropped and the
unready fact failed.

**TE-11d — E18's scenario goldens: the canvas mode of the §W-A
differential harness.** The scripts are DATA — a shared scenarios
file beside the mutation harness's, executed verbatim so identical
`CanvasAction` sequences on both platforms are enforced by
construction — and the driver (`CanvasScenarioDriver`, the harness's
canvas mode behind a new flag) seeds each fixture into a fresh temp
vault, applies every step through the REAL vault apply for the
inverse-carrying receipts, then walks the inverses backward. The
E18 rules are DRIVER-ENFORCED, not aspirational: every scenario's
post-inverse bytes must equal core's OWN canonical serialization of
the original content — the empty detached apply is the canonicalizer,
so semantic equality is core's judgment, never a host
reimplementation; a canonical fixture must round-trip BYTE-IDENTICAL
to its original; and a foreign-formatted fixture must NOT — the
foreign formatting dies on the first write, which is E18's
apply-plus-invert rule said from the other side. Three scenarios:
the sample corpus fixture through every op kind the algebra exports
(create card and group, content, both color arms, geometry, add,
update and delete edge, delete card); the nested-groups fixture
through geometry and a deep-group delete; a committed
foreign-formatted fixture (four-space indentation, reordered keys)
through content and color. Three committed artifacts pin step-level
content hashes, terminal bytes and the round-trip hash;
`CanvasScenarioCensus` asserts them byte-for-byte with the mutation
census's regen instruction, its determinism twin reruns the driver
in-process, and the tamper fact proves the foreign gate has teeth —
a canonical fixture mislabeled foreign makes the driver throw. The
mac Swift twin lane is recorded as OWED to keep this diff
reviewable (the mutation harness's own precedent): the scenarios
file is already the shared contract, and the Swift driver lands as
its own slice. Three mutations, each byte-restored: the inverse walk
dropped and the census failed; the round-trip hash dropped from the
artifact and the byte-compare failed; the foreign-survived gate
deleted and the tamper fact failed.

**TE-11e — the authoring journey, and the three production bugs it
flushed.** The FlaUI journey drives the whole authoring loop through
real chrome: File menu New Canvas into its own tab; the onboarding
region carrying core's spelled-out chord sentence; Ctrl+Alt+N
creating the first card; activation opening the card editor sheet
(axe-scanned as `AssertAxeClean` "canvas-card-editor"); typing
through the real keyboard; Escape COMMITTING; the committed title on
the outline row; Ctrl+Z undoing it — every leg a keystroke or UIA
invoke, no test seams. The first real keyboard on this path found
three shipped defects the unit facts could not see. FIRST: the
funnel's transactions run on the work seam, so every confirmation,
admission refusal and history sentence reached `CanvasAnnouncer` on
a pool thread — the thread-affinity Debug.Assert killed the Debug
build, and Release would have raced the coalescing timers silently.
The announcer now marshals ITSELF (the publish side's own
discipline; order preserved per dispatcher queue), pinned by a fact
whose foreign announce runs on a DEDICATED thread because
Task.Run().Wait() inlines the lambda onto the waiting thread and
un-crosses the very boundary under test. SECOND: `CommitOnEscape`
had NO CALLER — TE-7 built the seam and nothing wired the key. The
window's key gate gained the sheet's ONE arm: Escape commits (the
seam decides commit, no-change, or in-sheet conflict), everything
else passes to the draft box, M8's carve-out made real. THIRD: the
sheet never bound at all — `Title` and `Draft` were internal, and a
WPF binding against a non-public property fails SILENTLY: the box
held the typing, the draft held the seed, and Escape's no-changes
arm closed over the difference. Both went public with the reason in
their doc comments, pinned by a reflection fact so a refactor back
to internal fails in the suite, not in a screen reader. Three
mutations, each byte-restored: the self-marshal deleted and the
cross-thread fact failed; `Draft` back to internal and the
visibility fact failed; the Escape arm deleted and the JOURNEY
failed — the wiring's only honest fact is the journey itself. The
journey's probes re-find elements fresh per poll (a republish
rebuilds the tree, so cached UIA handles go stale mid-walk), and its
failure messages carry the sheet state, the visible rows, the canvas
bytes on disk and the app log tail, because each of the three bugs
was found by exactly that forensics.

### What carries out of the task loop

The loop closed with TE-11e. Four pieces are CARRIED, not silently
dropped, each with its owner: the staged Rename Group and Set Color
prompt sheets (the rows stand visible-with-reason per the TE-8 plan;
the prompt machinery is PR F's mode-owned commit seam, and landing a
bare prompt here would fork that design); the mac Swift lane of the
canvas scenario driver (the scenarios file is already the shared
contract — the mutation harness's own precedent — and mac compiles
CI-only from this checkout); axe over the two pickers (the sheet is
scanned in the journey; the pickers' modal-by-chord surfaces are the
same TE-6 substrate the citation gate already scans, and the arm
rides the prompt-sheet slice); and E14's file-arm anchor EXACT
verification, which by its own contract STOPS for an owner decision
if the caret misses — a fact that can end in a STOP belongs where
the owner is watching, so it is recorded here as the PR's one open
acceptance rather than buried green. Everything else E promised is
in: the ledger's thirty-nine findings dispositioned through eighteen
recorded tasks, the citation floor raised eighteen times, every
mutation byte-restored, and the three shipped bugs the first real
keyboard found.

### Codoki round 1

One finding, and it was right: the history path took the landed-but-
unindexed apply straight into publish and speech, missing the verb
path's guard — an undo against a stale index would have confirmed a
presentation that never installed. The guard is mirrored
checkout-shaped (the receipt crosses with its REAL inverse, the
publication marks committed-unpresented, recovery is refresh-only),
and the review's own prescribed fact pins all four claims, bitten by
deleting the guard. The formal review approved with the no-issues
sentence; the comment's finding was addressed rather than waved at.

### CI round 1 (mac)

The Mac XCTest lane refused the PR at compile: TE-0's
`SavedButUnindexed` arm made two exhaustive `VaultError` switches in
AppState.swift non-exhaustive — a debt invisible from this checkout
because mac compiles CI-only. Both switches gained the arm with the
landed-write sentence (bytes real, index catches up, never recreate
— the #1123 shape), landed blind with CI as the oracle, which is
this repo's recorded convention for the mac lane. The lane's second
pass surfaced the same debt's test-side tail — the mutation-harness
twin's refusal-kind switch and a prepared-load fixture missing the
CAS basis — both extended the same way — and a third pass found the
FFI smoke example's own exhaustive switch, whose entire purpose is
to fail compile when the Rust side grows an arm; it did its job.
The Windows FlaUI gate then failed HONESTLY on the two journeys this
PR's own ratified flips outdated — the outline and table activation
legs still expected the retired interim detail, and the row-actions
leg still pinned the staged Delete — both flipped to the TE-8/
TE-11b truth, with the editor sheet closed inside each leg so the
modal never swallows what follows.

## PR F — move and resize modes, structural placement, and the connect flow

**Goal (spec §PR F).** The spatial and connection authoring that runs
on the C mode controller and the E funnel: move/resize modes (t4
#521), placement commands via the card picker (#522), and the
Connect To… picker, connect mode and connection editing (#523).
After this PR a keyboard user can rearrange a canvas — grab, nudge,
place, align, resize, connect — without the visual surface, and
every spatial sentence is core's.

This section is revision 2, rewritten against round 1's thirty-four
findings. Round 1's central finding was revision 1's central
omission: a mode's COMMIT is not a call, it is an OPERATION IN
FLIGHT — the E funnel completes asynchronously, and a contract that
lets `OnCommit` answer synchronously either ends the mode before the
truth exists or leaves it standing on success. This revision is
organized around the MODE COMPLETION value that closes that gap.
New types remain plain text until tasks bind them (the series
convention); every core name cited is verified against the generated
surface, with required parameters spelled.

Contract ids are `F1…`; decisions are `FD-…`.

**F1 — The transient is UI-held hypothetical geometry with an
IDENTITY, and R-A's one carved exception is honored exactly.** Move
and resize hold a transient: ids in reading order via
`CanvasOrderNodes` (a rigid unit for sets — a TOTAL bijection
id ↔ original rect ↔ hypothetical rect, entry refuses if any member
lacks scene geometry), original rects (restored on cancel),
hypothetical rects (what commit writes), the is-resize flag, the
entry overlap state, AND the publication the mode entered against —
the same reference identity every §E operation carries, compared by
reference, never a stamp. Per step the UI queries
`CanvasCheckOverlap(rect, exclude: the moving ids)` for EVERY moved
rect (overlap is the logical OR) under the read lease — steps are
reads; only Return writes (FD-1). The outline and table do not
change until an INSTALLED commit; the renderer alone draws the
hypothetical state (F10).

**F1a — Displacement cancels the mode, audibly.** A publication
displacement while a mode holds — reload, retarget, conflict record
installed by another path, committed-unpresented, shutdown, tab
close — CANCELS the mode through the C machine's own cancel: the
transient is discarded (no write, no rebase — hypothetical geometry
over rows that moved is a guess, and the §E Stale arm would
otherwise eat Return forever), the restoration is the cancel
announcement's (cards-returned / size-restored where the original
rows still exist; UNSTATED where they do not — the C vocabulary
already carries it), and the mode token clears in the same
transition. Rebase-on-reload is REJECTED as a design: round 1 #3
showed both rebase shapes write entry-era geometry over reloaded
truth. mac diverges here (its transient silently survives what its
single-threaded reload path never interleaves); the divergence is
admitted and the Windows rule is the contract.

**F2 — Move mode enters on the moving set, and the set rule is
mac's.** Entry on the selection, or the marked set as a rigid unit
once G lands — the holder takes a set from day one; an empty set
refuses NothingSelected; a set member without scene geometry refuses
NothingSelected (gone is gone). Arrows nudge by `GridStep`, Shift by
`GridStepLarge` (`CanvasConstants`; never a host constant); each
step announces the coalesced `CanvasMoveRelative` built from
`CanvasDescribeRelative` over the PRIMARY rect (the first id in
reading order — the one-rect API's designated feeder), plus the
overlap two-state machine: `CanvasOverlapTransition` onset when any
member's overlap begins, cleared when the last ends, silence while
the state holds.

**F2a — A step that cannot answer does not move.** Entry and step
reads are never-silent and atomic: a refused lease or a thrown
`CanvasCheckOverlap`/`CanvasDescribeRelative` leaves the transient
and overlay EXACTLY as they were and speaks the generic failed-action
arm (`CanvasActionFailed{CanvasAction}` — FD-6). mac's `try?` swallow
is an admitted divergence assigned to the mac lane, not imported.

**F3 — Resize mode is a single NODE, clamped, preset-equipped.**
Eligibility is mac's: any scene node, groups included, announced
through `CanvasModeObject.Card` with the node's title — the
card-grammar-on-a-group is mac's shipped shape and is adopted, not
fixed silently (recorded divergence at the tail). ←/→ change width,
↑/↓ height, Shift the large step; any step that would cross
`MinCardSize` refuses `CanvasResizeClamped` and changes nothing.
Presets — Default Size (`DefaultCardW/H`) and Fit to Content (the
D-5 placeholder formula; the content read is `CanvasNodeText` under
the mode's currency, and an unreadable or non-text node refuses
`CardTextUnreadable` keeping the transient) — route through the SAME
overlap machine as steps: a preset that creates or clears overlap
speaks the transition in its `CanvasResizeGeometry`, whose
`Width/Height` are minted through the mac `canvasSafeInt` twin
(clamp to non-negative finite, round, saturate at the integer type)
— a material rule, not a cast. The resize chord while resize mode is
ACTIVE commits it (mac's quick loop).

**F4 — Mode entry is ADMITTED, and commit is a completion, not a
return.** Entry routes through `CanvasNavigator.EnterMode(spec,
pane)` — the frozen C seam that attaches the invoking presenter —
and F adds the admission PREFLIGHT in front of the C machine: entry
acquires the E gate exactly as an operation does (the same ladder —
NotReady, RecoveryPending, ConflictPending, Busy — each refusing
with its §E sentence and entering nothing), and the mode token is
installed WHILE THE GATE IS HELD, then the gate released with the
token standing — so no previously admitted operation can be in
flight when the token installs, and none can admit after (round 1
#4's atomicity, IE-7's other half closed with the same lock).

**F4a — The commit bridge.** Return runs `OnCommit`, which submits
ONE `CanvasAction` of `UpdateNodeGeometry` per changed node (the
start lives in the holder and in the returned inverse — the op
carries only the end; round 1 #23) through the funnel WITH the mode
token, and answers the C machine PENDING — a third
`CanvasModeCommitResult` arm the tasks bind. The mode and its
transient STAND while the operation is in flight; the funnel's
completion — a per-outcome callback carried on the operation, the
mode-completion seam — resolves it per the terminal table (F4b).
The C machine's conformance suite gains the pending arm and re-runs
against move, resize AND connect (round 1 #34); refused-commit,
focus-departure, retirement and transition-closure run against the
real specs.

**F4b — The terminal outcome table.** Per outcome, in the funnel's
own vocabulary — mode / transient+overlay / token / history /
announcement:
- **Installed success:** mode ends committed; transient and overlay
  clear in the same completion; token clears; one history entry; the
  mode's confirmation (`CanvasModeCommitted`) speaks AFTER the clear
  (the §C Committed(confirmation) shape).
- **No-effect Return** (no rect changed): mode ends; nothing
  applies; token clears; no history; `CanvasModeEndedWithoutEffect`.
- **Preparation refusal / InvalidArgument:** the mode and transient
  STAND, token retained; no history; the preparation's own typed
  sentence spoke already; the user adjusts or cancels.
- **WriteConflict:** the conflict record installs (E6); the mode
  SUSPENDS — the transient and overlay stand frozen, the mode token
  YIELDS to `ConflictResolutionToken` for the recovery's writes and
  reinstalls atomically when the record resolves to this document's
  continuation, or the mode cancels per F1a when resolution reloads
  (round 1 #6's deadlock closed by the yield).
- **SavedButUnindexed / committed-unpresented:** the write landed —
  the mode ends committed, transient clears, token clears, the entry
  records with the landed hash (§E's arm), NO confirirmation beyond
  the refresh-only region: nothing speaks success against a stale
  index.
- **Mid-apply displacement:** §E retains the receipt quarantined;
  the mode is already cancelling per F1a; the token cleared with
  that cancel.
- **Esc:** restores exact prior geometry with NO backend call; token
  clears; `CanvasModeCancelled` with the restoration.

**F4c — The token lifecycle is a closed table.** Installed
atomically at admission (F4); retained across every non-commit
outcome that keeps the mode; yielded-then-reinstalled across a
conflict suspension; cleared exactly once at: installed success,
no-effect Return, Esc, every F1a cancellation (departure, reload,
tab close, shutdown), and restoration failure (the cancel that
cannot restore still clears — a leaked token is a bricked document).
`ARefusedCommitKeepsTheModeAlive` and the departure table are
inherited conformance arms over this table.

**F4d — History during a held mode is refused, audibly.** Ctrl+Z /
Ctrl+Y while a mode holds route through `ApplyHistory` as ever and
the token refuses them `ModeHeld` → `CanvasBlocked(ModeBusy)` — the
shipped arm IS the contract (round 1 #15); connect mode installs the
token too (F8), so no mode's remembered state can be undone out from
under it. A commit-pending window answers the same way: the token
stands until completion.

**F5 — Placement commands are picker-anchored engine placement, one
action end to end.** Place Below/Above/Left Of/Right Of… open the
card picker purpose-labelled (the §E hand-off), proximity-sorted via
`CanvasProximityOrder(model, anchor: the primary mover, exclude: the
moving ids)`. Refusals, each exact and state-keeping (the picker
retains its filter and highlight): empty moving set — NothingSelected;
anchor inside the moving set — PickOutsideMovingSet; anchor vanished
at confirm — PickDifferentTarget; a mover vanished — NothingSelected;
the placement query throwing — the FD-6 arm, nothing written. The
placement itself runs INSIDE the operation's prepare-under-the-gate
(§E's `prepare(handle)` — round 1 #10): a single mover routes
`CanvasPlaceNew(handle, anchor, width, height, directionHint,
exclude: moving)` with the mover's OWN width/height; a set routes
`CanvasPlaceSet(handle, anchor, boxes, directionHint, exclude)`
where `boxes` is the movers' rects IN THE SET'S READING ORDER and
the result's positional `Origins` map back BY THAT SAME ORDER
(cardinality must match or the preparation refuses whole). One
`CanvasAction`, one undo, `CanvasCardPlaced` / `CanvasBulkMoved`.

**F6 — Align With… is the same-axis slot with a total refusal
table.** Align sets the mover's Y to the anchor's Y (top edges,
FD-2). Refusals: no selection — NothingSelected; target vanished or
self — PickDifferentTarget; already aligned — NoChanges, nothing
written, no history (round 1 #27); occupied slot — AlignWouldOverlap;
the overlap check runs inside prepare-under-the-gate with the write
it guards (round 1 #10). Success is one action announced
`CanvasCardAligned` with both titles.

**F7 — Connect To… stages, then applies ONCE.** The picker is
proximity-sorted from the selected card. Confirm STAGES an immutable
request — origin id, owner pane, the entry publication, the target —
then runs the label step (the prompt machinery this PR lands; Enter
skips); only after the label answer does ONE apply run, inside
prepare-under-the-gate: re-resolve both endpoints against the held
handle (either vanished → PickDifferentTarget, staged state kept),
sides from `CanvasAutoSides(from, to)`, then `AddEdge(id:
CanvasNewId(), fromNode, fromSide, toNode, toSide, FromEnd: None,
ToEnd: Arrow, label: the answer or null, color: null)` — every
generated parameter spelled (round 1 #25). Announced
`CanvasConnected`. The staged request never re-reads live selection
(round 1 #12 — the mac prompt's re-read is an admitted divergence
assigned to the mac lane). Connection editing and deletion stay E's
verbs; F adds no second edit surface. Refusal table: no origin —
NothingSelected; no candidates — NoConnections is WRONG and not
used — the picker simply shows none and Esc returns; apply failure —
the F4b table (this is a funnel operation like any other).

**F8 — Connect MODE is navigator movement verbatim, label-less, and
token-holding.** Entering connect mode remembers the origin
(id + publication identity), installs the mode token through F4's
admission, and the navigator's own movements step candidates — the
same chords, handlers and announcements (R1). Return with the
reader ON the origin — or with no movement — ends
`CanvasModeEndedWithoutEffect(Connect)` (round 1 #14); Return
elsewhere applies F7's exact staged apply WITH `label: null` — the
mode is the no-label fast path BY CONTRACT (round 1 #13's fork
closed: the label flow is the picker's, the mode's is immediacy);
Esc returns the selection to the origin and connects nothing. F1a
binds: displacement cancels the mode; a vanished origin at Return
refuses PickDifferentTarget and cancels with the selection left
where the reader stands.

**F9 — Every mode and command has visible controls (M6), and the
matrix is total.** Header buttons for commit/cancel while a mode is
active; context-menu rows from the ONE TE-8 plan; palette rows by
registration; chord rows: `moveMode` (Ctrl+Alt+G), `resizeMode`
(Ctrl+Alt+R), `resizeDefaultSize`, `resizeFitContent`, the four
`place…` rows, `alignWith…`, `connectTo…` (Ctrl+Alt+C), and
`connectMode` — labels byte-identical to mac (P3), divergences
recorded. The applicability matrix is pinned per row: resize presets
OUTSIDE resize mode — disabled with the mode's reason; commit/cancel
with no mode — disabled (the shipped C gate); entry while a mode is
active — invoked-and-refused through F4's admission (Busy speaks);
picker rows without the selection they need — invoked-and-refused
with their F5–F7 arms; ordinary funnel verbs during a held mode —
invoked-and-refused ModeHeld (F4d). Disabled-with-reason is for
structural impossibility; typed refusal is for state the user can
change; each row's side is named in the tasks' decision tables.
The staged Rename Group and Set Color prompt sheets carried from §E
land HERE, on the prompt machinery F7's label step builds — the
hand-off honored, not narrowed (round 1 #16).

**F9a — Action names are contract.** The stored action names core
speaks through `CanvasHistoryApplied` and the undo menu title are
mac's verbatim: `move "T"` / `move N cards` (the count-noun rule),
`resize "T"`, `align "T"`, `connect "A" to "B"`, with quoting
exactly as §E's verbs quote. A name is part of Ctrl+Z's observable
sentence, not an implementation string.

**F10 — The renderer draws the transient; the pairing is
identity-checked; the visible state is one observable.** PR D's
renderer gains the transient input (the mac `transientRects` twin)
carrying the F1 publication identity: a scene render pairs rects
with cards ONLY when identities match — a sibling pane on another
publication, or a reloaded scene, renders committed truth. The
transient applies to card visuals, their connected edge paths, the
selection ring, hit-testing and the accessibility frames (round 1
#33 — mac's full list); virtualization treats a transient card as
materialized. Mode-visible state — active spec, container value,
overlay rects — is ONE aggregate observable derived in a single
change per transition (round 1 #8): observers never see
idle-with-overlay or active-without-overlay; the C machine's
clear-then-effect order is bridged by that derivation, not by
ordering promises at every site.

**F11 — Mode transitions retire the pending navigation line.** The
announcer's Medium coalescing class holds a 200 ms pending line;
every mode TRANSITION — entered, committed, cancelled, no-effect,
clamped — retires any pending line in the same class before
speaking (round 1 #31): a cancel is never followed by a now-false
position sentence. The rule is the transition's, enforced at the
announcer seam the tasks bind, and it is grammar (0a), not polish.

### Decisions

- **FD-1.** Steps are reads under the read lease; ONLY Return
  writes. The E gate is not held during stepping — the mode token is
  what excludes writers (F4c), not a held gate.
- **FD-2.** The align axis is the TOP edge (mac's shipped shape).
  A same-X variant is a new command, not an option.
- **FD-3.** Chords: Ctrl+Alt+G / Ctrl+Alt+R / Ctrl+Alt+C; mode
  arrows are surface keys consumed by the ACTIVE mode ahead of
  selection movement (the `canvasModeConsumesArrows` twin — the
  navigator's arrow handlers gain the mode branch); no Ctrl+Shift
  variants.
- **FD-4.** The prompt machinery (F7's label step) is this PR's
  build, and the §E-carried Rename Group and Set Color sheets land
  on it in this PR (F9). Enter-skips is the label step's contract.
- **FD-5.** A mode conflict SUSPENDS rather than cancels (F4b's
  WriteConflict arm): the recovery owns the writes through
  `ConflictResolutionToken`, and the mode resumes or cancels with
  the record's resolution. Suspension is a C-machine state the tasks
  bind.
- **FD-6.** F's non-admission failures speak the generic
  `CanvasFailedAction.CanvasAction` arm with the dynamic detail —
  the generated vocabulary has no Move/Resize/Connect arms, host
  prose is forbidden (0a), and minting new core arms is deferred to
  the tasks' judgment under the lifted ceiling with the §E
  RefreshPending precedent.

### Recorded divergences (mac, admitted — assigned, not imported)

- mac clears `active` before `onCommit` and its transient before
  `canvasApply`, so a conflict there loses mode and preview; the
  Windows contract preserves both (F4b). The Swift repair is the
  mac lane's, recorded as owed.
- mac's step reads `try?`-swallow overlap/describe failures (F2a);
  same assignment.
- mac's connect prompt re-reads live selection as origin (F7); same
  assignment.
- mac announces a group resize through the Card grammar (F3);
  adopted as shipped grammar for parity, both hosts.

### Accepted risks

- The marked-set rigid move is exercised with test-built sets until
  PR G lands marks; the holder's set-shape is contract now.
- The FlaUI modes journey budgets the recorded CI round trip for
  first-popup/menu legs.
- mac compiles CI-only from this checkout; Swift touches land blind
  with CI as the oracle.

### THE FREEZE — read this first

**This section is CLOSED to further prose revision**, frozen at
revision 2 after two adversarial rounds, by APPLICATION OF THE
STANDING PRECEDENT — the owner's ruling that closed §C-unit, §D and
§E, applied here by the session rather than freshly ruled, and
recorded as such; the owner may overrule. The trajectory is rule 5's
signal on the same schedule as §E: 34 findings, then 32 with the
blocker half CREATED by revision 2's own fixes — the completion
bridge, the displacement rule and the token atomicity are defects OF
the new text. The missing model is not a prose question: it is the
identity of a completion, the transactionality of a token install
against a funnel that reads then acquires, and the semantics of a
suspension — questions every prose answer re-opened, exactly as
§E's operation identity did.

**The ruling: design by implementation.** Round 2's thirty-two
findings are reclassified as the IF ledger below — obligations
discharged by the task loop, each task binding names, running the
per-task gauntlet (code + facts + byte-restored mutations + a record
entry + a citation-floor raise + a suite-gated commit), with the
following explicitly permitted where a finding demands it, ceiling
lifted as §E's was: amendments to the FROZEN C machine and §E funnel
(IF-6's supersession — the Pending arm, the completion seam, the
Suspended state, the admission re-check under the gate), and core
vocabulary additions through the full playbook. A frozen section's
AMENDMENT BY TASK is recorded in that task's §F record entry naming
the frozen contract it supersedes — the freeze bars prose
re-litigation, not the code the rulings exist to produce.

The revision-2 contracts stand as the ratified baseline the ledger
refines; where a ledger entry contradicts the prose (IF-1's identity
type, IF-2's own-commit exemption, IF-16's connect confirmation,
IF-18's quick-loop precedence, IF-28's vanished-origin arm), the
LEDGER's direction governs and the discharging task's record is the
normative text.

### Round record

**Round 1 (xhigh, 2026-09-01): 34 findings — 17 blockers.** The
central finding: revision 1 treated a mode's commit as a call where
the platform needs a COMPLETION. Revision 2 was organized around
the mode-completion value.

**Round 2 (xhigh, 2026-09-01): 32 findings — 16 blockers, and rule
5 fired.** The blockers are defects of revision 2's own new text:
the transient's identity names the wrong reference type (IF-1); the
displacement rule eats the mode's own commit (IF-2); the token
install's claimed atomicity is false against the shipped
read-then-acquire ladder (IF-7); entry is not a closed transaction
(IF-8); the completion handshake has no delivery-order or affinity
rules (IF-9, IF-10); the terminal table misses the synchronous
admissions, the thrown preparations and the whole conflict
second-stage matrix (IF-12..IF-14); and the suspension contradicts
the displacement rule it shares a boundary with (IF-15). The freeze
above is the disposition.

### The IF ledger (round 2's findings, discharged by the task loop)
- **IF-1.** [BLOCKER] [F1/F1a/F7/F8/F10] Revision 2 names a `CanvasPublication` as the transient’s identity and claims it is “the same reference identity every §E operation carries.” §E operations actually carry a `CanvasLoaded` reference. Implementing the text literally makes connect mode cancel on its first navigation step because selection changes publish a fresh `CanvasPublication`; implementing `CanvasLoaded` contradicts the contract’s stated type. The identity must be corrected consistently throughout F1, F7, F8, and F10.
- **IF-2.** [BLOCKER] [F1a/F4b] F1a does not exempt publications caused by the mode’s own commit. An installed mutation refresh creates a new `CanvasLoaded`, and committed-unpresented explicitly creates a new publication—yet F1a says displacement cancels while F4b says those outcomes end committed. Consequently every successful refresh, and certainly SavedButUnindexed, can take both the cancel and commit arms. Mid-apply displacement also can announce “cancelled/returned” before §E later reports that the write durably landed.
- **IF-3.** [MAJOR] [F1/F2a/F5] `CanvasOrderNodes` is a required entry query, but F2a’s supposedly exhaustive never-silent entry-read rule covers only lease refusal and throws from `CanvasCheckOverlap`/`CanvasDescribeRelative`. A thrown or refused ordering query has no announcement or mode/token rollback row. Placement’s moving-set construction inherits the same hole.
- **IF-4.** [MAJOR] [F3] The Fit-to-Content refusal collapses two different generated outcomes. `CanvasNodeText` returns `null` for a known non-text node and throws for an unreadable/unknown node; the vocabulary has `CanvasStatus(NotATextCard)` for the former and `CanvasBlocked(CardTextUnreadable)` for the latter. F3 assigns `CardTextUnreadable` to both, producing a false sentence and an unrecorded behavioral divergence from mac, which treats non-text content as empty.
- **IF-5.** [MAJOR] [F3] The stated `canvasSafeInt` twin is not the shipped function. Swift rejects non-finite values, clamps to ±9e15, and `Int(Double)` truncates toward zero; the later `UInt32(clamping:)` supplies non-negative saturation. It does not round. Fractional imported geometry therefore produces different announced dimensions under F3’s rule.
- **IF-6.** [BLOCKER] [F4a/FD-5] The completion design directly changes both frozen machines without a normative supersession. Frozen C has a two-arm synchronous `CanvasModeCommitResult` and closes/drains its transition in `Commit()`’s `finally`; frozen §E’s operation has no outcome callback. F4a adds `Pending`, while FD-5 adds `Suspended`, and F4a adds a funnel callback, but never reconciles those changes with the frozen contracts.
- **IF-7.** [BLOCKER] [F4] Holding the mutation gate while installing the token does not establish the claimed admission atomicity. Frozen `AdmitAndAcquire` reads `_modeToken` before `gate.TryAcquire`. An operation can read `null`, lose the gate to mode entry, then acquire it after entry releases and proceed without rechecking the newly installed token. Revision 2’s “none can admit after” claim is therefore false against the shipped funnel.
- **IF-8.** [BLOCKER] [F4/F4c] Entry is not a closed transaction. The real admission enum also has `ModeHeld`, `BusyAlreadyAnnounced`, and `Stale`, and §E deliberately does not serialize reload/shutdown with the mutation gate. A lifecycle displacement or C rejection can therefore occur after token installation but before `EnterMode` succeeds. F4c has no rollback row for “token installed, mode never entered,” leaving a leaked token that bricks mutations.
- **IF-9.** [BLOCKER] [F4a] The completion handshake has no early- or late-delivery rule. §E’s `run` seam is inline in its conformance tests, so a callback may arrive before `OnCommit` has returned `Pending`; it may also arrive after F1a cancellation or retirement. No operation identity/state check says whether to buffer, accept, or discard such a callback, allowing resolution of a not-yet-pending transition or resurrection of an ended mode.
- **IF-10.** [BLOCKER] [F4a/F4b] Completion affinity and confirmation ownership are undefined. Funnel transactions run on the work seam, while mode, overlay, and WPF property state must change on the UI dispatcher. The existing §E confirmation seam also announces inside `Transact`; unless mode operations pass it as null and make the marshalled completion solely responsible, success can speak before the clear or speak twice. Neither requirement is stated.
- **IF-11.** [BLOCKER] [F4a/F4b/F4c] Focus and direct commands arriving during `Pending` have no total rule. Frozen C drains a deferred departure as soon as `OnCommit` returns, while F4a requires the mode to stand until asynchronous completion. F4b does not say how an already-arrived departure combines with Installed, refusal, conflict, or committed-unpresented. Esc, Cancel, a second Return, and the Escape ladder are also undecided: C refuses Cancel while closed, whereas F4b’s Esc row says it cancels.
- **IF-12.** [BLOCKER] [F4a/F4b] The terminal table omits synchronous funnel admission outcomes. `Apply` can return NotReady, RecoveryPending, ConflictPending, ModeHeld, Busy, BusyAlreadyAnnounced, or Stale without scheduling work and therefore without ever invoking a completion callback. If `OnCommit` nevertheless returns `Pending`, the mode is stranded permanently. These are not “preparation refusal / InvalidArgument.”
- **IF-13.** [BLOCKER] [F4b] The post-admission outcome table is also incomplete. It has no row for a thrown preparation, non-InvalidArgument `VaultException`, refresh/read exception, or `CanvasRefreshOutcome.Refused`. Some occur after the action and history entry have landed, so they cannot safely inherit either “refusal—mode stands” or “mid-apply displacement—receipt quarantined.”
- **IF-14.** [BLOCKER] [F4b/FD-5] WriteConflict is listed in a “terminal” table but is explicitly non-terminal, and its second-stage matrix is absent. There are no mode/token/history/announcement answers for Overwrite success, repeated conflict, InvalidArgument or I/O failure; Reload failure; or Save a Copy success, failure, collision, and landed-but-unindexed outcomes. In particular, a successful Overwrite committed the mode action and should not generically “reinstall” the transient for another commit.
- **IF-15.** [BLOCKER] [F1a/F4b/F4c/FD-5] Reload during suspension has contradictory token outcomes. F1a cancellation clears the token, while F4b/F4c promise later atomic reinstallation when resolution continues. A late resolution completion can therefore reinstall the mode token after reload already cancelled the mode. No comparison against the same suspended mode, conflict record, and publication prevents that resurrection.
- **IF-16.** [BLOCKER] [F4b/F7/F8] The universal Installed-success announcement is unconstructible for Connect. Generated `CanvasModeCommitted` requires `CanvasTransientVerb`, whose only arms are Move and Resize. F7 correctly requires `CanvasConnected`; F4b incorrectly requires `CanvasModeCommitted` for the same connect-mode success.
- **IF-17.** [MAJOR] [F1a/F4b/F8] The cancellation rows are not total over Connect. F4b says Esc restores prior geometry, and F1a lists only CardsReturned, SizeRestored, and Unstated. Generated vocabulary has the required `CanvasModeRestoration.BackAt`, and F8 separately says selection returns to the origin. The declared terminal table therefore contradicts its Connect specialization.
- **IF-18.** [BLOCKER] [F3/F4/F9] Active-mode entry precedence contradicts frozen C and contradicts the resize quick loop. C’s M7 contract requires `CanvasModeRejected(activeMode)` when another entry is attempted. F4/F9 instead preflight through §E and say Busy/ModeBusy, bypassing C’s conformance arm. Separately, F3 says invoking `resizeMode` during Resize commits it, while F9’s blanket active-mode cell says the entry is refused.
- **IF-19.** [MAJOR] [F5/F6] Placement and alignment picker requests do not carry the immutable operation context §C/§E require: originating pane, captured moving IDs/rects, and currency basis. F7 explicitly stages these for Connect, but F5/F6 do not. A confirm after shared selection or presenter affinity changes can therefore move the wrong set or address effects to the wrong pane.
- **IF-20.** [MAJOR] [F5] The FFI-exactness claim is false. Generated C# exposes `VaultSession.CanvasProximityOrder(ulong handle, string? anchor, string[] exclude)`, not `CanvasProximityOrder(model, …)`. F5 also has no refusal row for this picker-building query throwing or losing its lease; presenting an empty picker would conflate “no candidates” with “the query never answered.”
- **IF-21.** [MAJOR] [F5/F6] The refusal tables still have unnamed cells. F5 says an `Origins` cardinality mismatch refuses whole but names no event. F6 omits a selected mover that vanished after picker open, despite separately covering a vanished target. Neither table states precedence when these conditions combine with query failure.
- **IF-22.** [MAJOR] [F5/F6/FD-6] FD-6 selects the wrong existing grammar and then reopens its own decision. Generated `CanvasFailedAction` already has `Placement` and `Align`, and mac uses those exact arms; forcing F5/F6 through generic `CanvasAction` loses verb identity. The final sentence then permits tasks to mint different arms, so the supposedly contractual announcement remains undecided.
- **IF-23.** [MAJOR] [F6/FD-2] F6 contradicts executable spec §PR F. The spec requires the same-X-or-Y, first-non-overlapping-slot behavior and refusal only when overlap is unavoidable. F6 checks only the exact same-Y/top-edge slot and refuses immediately when that slot is occupied, even if another qualifying slot is free. This divergence is not in the recorded list.
- **IF-24.** [MAJOR] [F7/F4b] “Apply failure — the F4b table” is invalid for Connect To. The picker flow has no active mode, transient, overlay, or mode token, so it cannot stand, suspend, yield, resume, or cancel as F4b prescribes. It must use §E’s committing-surface lifecycle and state-preservation rules instead.
- **IF-25.** [MAJOR] [F7] The staged request retains entry currency but has no displacement rule. A reload while the label sheet is open makes the eventual operation Stale; frozen §E deliberately announces nothing for Stale and schedules no completion. F7 promises a kept staged state and routes failures to F4b, leaving this ordinary prompt/reload path silent and without a defined retry basis.
- **IF-26.** [MAJOR] [F7] Self-connection is not excluded or refused. `CanvasProximityOrder` requires an explicit `exclude` argument, but F7 never requires the origin in it, and its prepare validation checks only endpoint existence—not `origin != target`. The shipped mac apply has an explicit inequality gate and `PickDifferentTarget`.
- **IF-27.** [MAJOR] [F7] Empty-label normalization is missing. Mac converts an explicitly submitted empty string to null; F7 says only “answer or null,” which distinguishes Enter-to-skip but not clicking Connect with an empty field. Persisting `""` instead of null changes serialized data and can produce an empty “labelled” clause.
- **IF-28.** [BLOCKER] [F8/F4a/F4b] The vanished-origin Return arm contradicts both the completion table and C conformance. F4b says preparation refusal keeps the mode and token; C’s `ARefusedCommitKeepsTheModeAlive` requires the same. F8 instead says `PickDifferentTarget` and then cancels. Calling Cancel inside `OnCommit` is expressly refused by frozen C, and returning Refused cannot produce the demanded cancellation.
- **IF-29.** [MAJOR] [F8] Esc restores only selection, not reader focus. After connect navigation, focus remains on the target row while F8 silently seats selection at the origin, violating C12/CD-40’s frozen reader/selection agreement. The restoration must address the owning presenter and focus the origin, with the existing seat fallback if it vanished.
- **IF-30.** [BLOCKER] [F4b/FD-5/F9] F9 has no suspended-state applicability matrix. Immediately after WriteConflict, arrows and presets are still routed by FD-3’s “ACTIVE mode” rule although F4b says the transient is frozen; commit/cancel controls remain visible with no enablement; entry and ordinary verbs can resolve as ConflictPending rather than F9’s Busy/ModeHeld; and owner-pane departure is unspecified. Every conflict reaches this undecided state.
- **IF-31.** [MAJOR] [F9] The claimed normal-state matrix is not actually published; it defers each row’s side to future task decision tables. Missing explicit cells include no-selection behavior for Move/Resize/Connect mode entry, each entry command under each active mode, per-row context-menu applicability, the two carried prompt sheets, and the distinction C9 requires between hidden header controls and disabled palette rows. “Total” cannot be delegated past contract freeze.
- **IF-32.** [MAJOR] [F11] The new coalescing rule names a class that does not exist. The frozen announcer has `Navigation` and `Filter` coalescing classes; Medium is a priority, and mode events plus `CanvasResizeClamped` are immediate/unclassified. Therefore “retire any pending line in the same class” has no determinate meaning and could either fail to drop stale navigation or incorrectly drop pending filter feedback. The contract must name `Navigation` explicitly and define whether Filter survives.

### The task loop

| Task | Slice | Discharges |
| --- | --- | --- |
| TF-0 | The mode-completion seam: the Pending arm and the completion callback with identity, delivery-order and dispatcher-affinity rules; the frozen-C/§E supersessions recorded | IF-6, IF-9, IF-10, IF-11, IF-16 |
| TF-1 | Admission-preflight entry as a closed transaction; the token install re-checked under the gate; the closed clear table with the rollback row; the conflict suspension with its second-stage matrix and the reload boundary | IF-7, IF-8, IF-12, IF-14, IF-15, IF-18, IF-30 |
| TF-2 | The transient holder with the corrected identity; the own-commit exemption; displacement-cancels; the arrow mode-branch; the total never-silent entry/step read rule | IF-1, IF-2, IF-3, IF-4, IF-5 |
| TF-3 | Move mode end to end through the bridge; the set bijection; the describe feeder; action names | IF-17, IF-19, IF-20 |
| TF-4 | Resize mode: clamp, presets through the overlap machine, the minting rule, the quick loop's precedence reconciled | IF-18's resize half, IF-21, IF-22 |
| TF-5 | The renderer transient input with identity pairing and the one aggregate observable | IF-23, IF-24 |
| TF-6 | The transition-retirement rule at the announcer seam | IF-25 |
| TF-7 | Placement and align: FFI-exact shapes, total refusal tables, prepare-under-the-gate | IF-26, IF-27 |
| TF-8 | The prompt machinery, the staged Connect To… flow, and the carried Rename/Set Color sheets | IF-28's staging half, IF-29 |
| TF-9 | Connect mode: token-holding, navigator reuse, the no-effect and vanished-origin arms reconciled with conformance | IF-16's connect half, IF-28 |
| TF-10 | The F9 matrix: chords, menus, palette, context rows, decision tables, the suspended-state column | IF-30's matrix half, IF-31 |
| TF-11 | The journeys: the FlaUI modes journey, axe, and the remaining ledger sweep | IF-32, the sweep |

Task order: TF-0 → TF-1 → TF-2 (the spine), then TF-3..TF-11 in
order, each under the §E gauntlet. Ledger entries not named above
ride the task whose surface they touch; the closing record sweeps
the ledger for stragglers, as §E's did.

### Implementation record

**TF-0 — the mode-completion seam (IF-6, IF-9, IF-10, IF-11,
IF-16).** The two recorded supersessions of frozen machines land
together, each named here as the freeze requires. The C machine's
two-arm commit result becomes three: `CanvasModeCommitArm` adds
Pending, legal ONLY when the submission was Admitted (IF-12's rule
stated at the type), carrying the submitted operation's identity.
`Commit` on Pending keeps the mode standing and refuses re-entrant
Commit and Cancel while the truth is in flight (IF-11);
`CanCommitOrCancel` reads pending-aware, so the header buttons and
the Esc ladder see the window honestly. The completion side is
`ResolveCommit(operationId, outcome)` — identity-checked by
reference so a late or foreign completion drops itself without
touching the mode (IF-9), and dispatcher-marshaled with the
presentation engine's own capture idiom so a work-seam completion
re-invokes itself home before any mode state changes (IF-10); an
Installed resolution clears FIRST and speaks after, the §C
Committed-confirmation order preserved across the bridge, and the
confirmation type is the spec's business — move and resize carry
`CanvasModeCommitted`, connect carries `CanvasConnected` (IF-16).
A departure arriving mid-pending follows the LIVE table: the
in-flight mark is abandoned so the cancel runs now and the late
completion finds nothing — a rule the fact found when the first cut
let the pending guard refuse the departure's own cancel. The §E
side: `CanvasOperationOutcome` types the terminal outcomes
(Installed, RefusedPrepare, Conflict, Unindexed, Displaced,
RefreshRefused) and the operation carries an optional completion the
transaction invokes in its finally, gate released first — one
delivery per transaction, from the transaction's own thread, the
consumer marshaling. Six facts: the pending window's standing mode
with live departures; the foreign completion dropping itself; the
foreign-thread marshal home (a dedicated thread, the inlining
lesson); clear-before-speak pinned by reading the mode from inside
the announce sink; the second-Return-and-Cancel refusal; the funnel
delivering Installed and Conflict to the callback exactly once.
Four mutations, each byte-restored: the identity check widened and
the foreign fact failed; the marshal block deleted and the
cross-thread fact failed; the clear/speak order flipped and the
order fact failed; the funnel's delivery dropped and the wiring
fact failed.

**TF-1 — admission-preflight entry and the token lifecycle (IF-7,
IF-8, IF-12, IF-14, IF-15, IF-18, IF-30's funnel half).** Mode entry
is ADMITTED like a write. `AdmitModeEntry` runs the shared ladder,
speaks every refusal through the TE-11c seam, and on Admitted
installs the mode token UNDER THE HELD GATE before releasing — so no
admitted operation can be in flight when the token lands (IF-8's
observable, pinned by the entry refusing Busy while an operation
holds). The ladder itself gains the IF-7 recheck: after TryAcquire,
the token and the conflict record are re-asked under the held gate,
a mismatch releasing and refusing with the arm the pre-read would
have used; the recheck's own window is not reachable from a public
seam deterministically, so its bite rides the install-under-gate
fact and the recheck is recorded as belt-and-braces rather than
claimed proven. The navigator's `EnterMode` grows the preflight with
M7's precedence honored FIRST: an active mode answers with the C
machine's own rejection sentence — "is active. Return to commit or
Escape to cancel first." — never the funnel's Busy (IF-18), and the
standing mode is untouched. The clear table (F4c) is wired by
WRAPPING the spec's closures: commit-Applied and cancel both clear
the token, which covers departures and retirement because they all
run those closures; a machine refusal after install rolls back
immediately. The first cut shipped exactly the leak this table
exists to prevent — the entry installed and cancel never cleared,
so the frozen §C fact's second entry found a bricked document — and
the fix is the wrap, found by that fact. The conflict half lands as
the token trio: `SuspendModeToken` yields to the resolution's
writes, `ReinstallSuspendedModeToken` matches ONLY the suspended
identity, and `ForgetSuspendedModeToken` (the F1a cancel's row)
makes a late reinstall a no-op — IF-15's resurrection closed by
identity, with the full second-stage outcome matrix riding the
flows that can reach it (TF-8/TF-9), recorded honestly. `ClearModeToken`
is identity-checked so a stale clear cannot strip a successor. Four
facts: the both-ways exclusion with the spoken ModeHeld; the
identity-checked clear; the suspend/reinstall/forget trio; the
rejection-then-freed end-to-end through the real navigator. Four
mutations, each byte-restored: the install moved off the gate and
the Busy arm failed; the clear's identity check dropped and its fact
failed; the reinstall's identity check dropped and the trio failed;
the wrap's cancel-clear dropped and the end-to-end fact failed.

**TF-2 — the transient holder, its identity, and displacement
(IF-1, IF-2, IF-3, IF-5; the arrow branch rides TF-3 where the step
exists, recorded here so the rescope is deliberate).**
`CanvasTransientHolder` is the F1 value: ids reading-ordered by
`CanvasOrderNodes`, a TOTAL bijection over originals and
hypotheticals, the resize flag, the entry overlap state — and the
IDENTITY, corrected per IF-1 to the `CanvasLoaded` reference every
§E operation carries: a selection intent publishes a fresh
publication but keeps the loaded triple, so navigation during a
held mode survives, while a reload installs a different reference
and F1a answers. The capture is ONE never-silent read under the
lease (IF-3): ordering, geometry and entry overlap inside a single
try — a vanished member, a short ordering answer or a thrown query
builds NOTHING and the caller speaks; the ordering-length gate is
the ghost's real door (its mutation bites), and the per-node null
arm behind it is recorded as belt-and-braces. The displacement
watcher rides the publication-applied seam: a publish whose Loaded
differs from the holder's identity discards the transient and
cancels through the machine — restoration is the cancel's own
sentence — EXCEPT while this mode's commit is pending, where the
completion is the one arbiter (IF-2's own-commit exemption: the
commit's refresh publishes a new Loaded and must not cancel the
mode it is completing; the pending mark stands the watcher down).
Three facts: the reading-order bijection with the ghost refusal;
the selection-publish-stands / reload-cancels pair through real
publishes; the pending-commit exemption driven by a real funnel
verb republishing mid-pending. Three mutations, each byte-restored:
the identity comparison dropped and the selection publish cancelled
a living mode; the exemption dropped and the mode's own refresh
killed it; the ordering-length gate dropped and the ghost built a
holder.

### TF-3 — move mode end to end

Move mode is the first REAL mode over the whole §F machine: entry
through TF-1's admitted preflight, TF-2's holder as the hypothetical,
steps that never touch the engine, and a commit that is a completion.
`CanvasNavigator` gained `EnterMoveMode` (the moving set is the marked
set reading-ordered, else the selection; every refusal speaks —
NothingSelected, the not-ready sentence, or FD-6's failed-action arm
when the capture refuses), `ModeStep` (one grid step over the whole
rigid set — core's `CanvasConstants` supplies the step and its large
variant; overlap is an OR over every moved rect via
`CanvasCheckOverlap` excluding the set itself; the spoken position
comes from `CanvasDescribeRelative` over the primary; the two-state
machine speaks Onset and Cleared only; a throwing read leaves the
transient untouched and speaks FD-6), the arrow router `ModeStepOr`
(a held transient owns the arrows AHEAD of ArrowMove/ArrowFollow;
Shift rows are mode-only with a null fallback — four
`windows.canvas.modeStepLarge*` chord rows land under FD-3), and
`SubmitTransientCommit` (geometry ops for CHANGED rects only; none →
Applied with the ended-without-effect sentence; otherwise one
`UpdateNodeGeometry`-batch action named per F9a, submitted with the
mode token and a completion mapping every F4b row — Installed resolves
Committed with core's sentence, Conflict suspends the token and
abandons the mark leaving the transient frozen, RefusedPrepare
resolves Refused, Displaced defers to the F1a watcher, and the
indexing failures resolve Committed with no spoken success).

The task's discovery is an ORDERING HOLE the contracts never named:
under a synchronous funnel the completion runs while `OnCommit` is
still on the stack, so `ResolveCommit` and `AbandonPendingCommit`
arrived BEFORE the pending mark existed — the resolution died at the
identity check and the mode wedged pending forever, Escape and Return
refusing honestly at a door nobody could ever open again. The fix is
the controller's EARLY-RESOLUTION MEMORY: a resolution or abandon
arriving during `_committing` with no mark is remembered rather than
dropped, and consumed the moment the Pending arm sets the mark — a
foreign identity still dies at the same check, one commit later. Two
controller facts pin both arms; the end-to-end fact walked the wedge
first. Core's committed sentence for a move renders as `Placed "…"` —
the fact pins the render, not a guess.

Facts: the six mutation-suite facts (enter/nudge/commit-one-action,
Esc restores exact bytes, transitions-only overlap, arrows route and
Shift is large, no-effect Return says so, a conflicted Return suspends
without a wedged mark) plus the two controller facts. Mutations, each
byte-restored: M1 the x-axis dropped from the step math (the commit
fact bit), M2 the transition machine inverted to per-step speech (the
overlap fact bit), M3 the arrow branch dropped (the routing fact bit),
M4 the no-effect arm dropped (the empty-commit fact bit), M5 the early
memory dropped (the controller fact bit). Process note, honestly held:
the first gauntlet round restored mutations with `git checkout`, which
also discarded the task's own uncommitted production code — the round
was rebuilt from the payloads and rerun with scratchpad-copy restores;
the discipline is copies, never checkout, while work is uncommitted.

### TF-4 — resize mode, the quick loop, and the minting rule

Resize is the single-node mode: entry on the selected scene node
(groups included through the Card grammar — the recorded mac
divergence, adopted), TF-2's holder with the resize flag, and the
same commit bridge move built — `SubmitTransientCommit` needed no
change, which is the §F architecture doing its job. The step is
REJECT-THE-STEP, mac's rule copied by contract: when either
dimension would cross the minimum, nothing moves and the clamped
sentence speaks. Presets — Default Size from core's constants, Fit
to Content by D-5's placeholder formula at default width — land
through `ApplyResizeRect`, the ONE gate where steps and presets
alike read overlap under the lease and speak their geometry sentence
with the transition; the spoken width and height are MINTED by
`CanvasSafeUint`, mac's `canvasSafeInt` twin (non-finite mints 0,
clamp, round, saturate) — a material rule with its own edge facts.
The content read rides the VM's never-silent node-text table: a
refused read keeps the transient, and the table's own sentence has
already spoken. FIT-FORMULA ARITHMETIC, recorded: MinCardSize is 40
and one line wants 64, so the formula's floor can never fire today —
it stands as belt-and-braces (mac carries the same dead guard); the
600 CAP is the live guard and the mutation aims there, proven by an
800-character fixture essay.

IF-18's resize half is RECONCILED as the same-mode exception: the
resize chord routes through `CommitOrEnterResize` — during resize it
COMMITS (mac's grab-adjust-done loop), otherwise it enters; a
DIFFERENT active mode still gets frozen C's M7 rejection inside the
entry, exactly as TF-1 built it. The chord table gains the four F9
rows — `moveMode` (Ctrl+Alt+G) and `resizeMode` (Ctrl+Alt+R) with
mac's glyphs, the two presets palette-only as mac allocates them —
and the moveMode row is TF-3's owed front door, delivered here and
recorded as owed. IF-21 and IF-22 name cells on the PLACEMENT and
ALIGN refusal tables (`Origins` cardinality, the vanished mover, the
`CanvasFailedAction` verb arms); TF-4 touches neither surface, so
both ride to TF-7 where those tables are built — recorded as a
ledger reassignment, not a silent drop.

Facts: six — the end-to-end enter/step/commit (disk width, one
history entry, core's Resized sentence), the whole-step refusal, the
preset overlap onset (one geometry sentence, `Assert.Single`), the
fit formula with its cap half and the group-refusal half, the quick
loop with the cross-mode rejection, and the minting edges. Mutations,
each byte-restored: M1 the reject gate dropped, M2 the preset
transition suppressed, M3 the cap dropped (re-aimed there after the
floor proved structurally dead — the first aim not-bitten is the
arithmetic above), M4 the quick-loop branch dropped, M5 the finite
guard dropped. All bitten.

### TF-5 — the renderer transient input and the one observable

F10 lands as a fourth commit authority. The engine gains
`CommitTransient` — the mac `transientRects` twin, dispatcher-owned
like every authority and folded into the derivation's capture, the
install-time revalidation and the stale-build recheck. The IDENTITY
CHECK runs once, in the pure derivation:
`CanvasPresentationState.EffectiveRects` admits the holder's rects
only when its identity IS the publication's own loaded reference, so
a sibling pane on another publication or a reloaded scene derives
null and renders committed truth. The admitted map flows two ways
from that one site: into `CanvasPeerTopology.Derive` as per-node
overrides — where a transient card MATERIALIZES regardless of the
window (virtualization must not hide the card being moved) and an
edge follows its moved endpoint — and onto the state as
`TransientRects` with the `NodeRect` helper the edge pass, the
selection ring and hit-testing now answer from. Placements carry the
moved rects, so card pixels and the a11y peers' bounding rectangles
moved without touching either consumer: mac's full list — cards,
edges, ring, hit-test, frames — in ONE derived install per change.

The ONE AGGREGATE OBSERVABLE is the document's `ModeVisibleChanged`:
fired exactly once per transition — entry after the machine entered
AND the holder installed, teardown after both cleared, each step
after the rects moved — and consumed by the renderer's model wiring
into `CommitTransient`. An observer can never see
active-without-overlay or idle-with-overlay; the fact SAMPLES the
pair at every firing to pin exactly that. The surface's commit and
cancel buttons keep riding the machine's own atomic Active-setter
notification — frozen C wiring, deliberately untouched and recorded.
The task table names IF-23/IF-24 here; both rows are F6/F7 surfaces
(align's slot search, Connect To's lifecycle) and ride to TF-7/TF-8
under the standing surface rule — recorded, not silently dropped.

Facts: five — the one-pass step (state, placement and effective rect
agree), the foreign identity rendering committed truth, the
off-window materialization, the one-event-per-transition sampler,
and the committed teardown deriving from the refreshed publication.
Mutations, each byte-restored: M1 the identity check dropped, M2 the
step notify dropped, M3 the force-materialize arm dropped, M4 an
early double-fire installed. All bitten.

### TF-6 — transition retirement at the announcer seam

F11 lands with IF-32 reconciled by NAMING: "same class" meant
nothing — Medium is a priority — so the rule now says NAVIGATION
explicitly. A mode transition — entered, committed, cancelled,
no-effect, clamped — retires the pending Navigation line before it
speaks: a cancel is never followed by a now-false position sentence,
a clamp is never followed by a size the step never took. FILTER
SURVIVES: pending filter feedback is not made false by a mode
ending. The mechanics ride the seam that already owns ordering:
`Announce` classifies the event twice (its coalescing class, its
transition-ness) and `Emit` takes the stale Navigation line on the
dispatcher side, after the self-marshal, before the render — order
preserved by the dispatcher queue exactly as emission is.

The task's discovery: `CanvasModeRejected` renders HIGH, and t0's
frozen assertive rule already drops ALL pending lines for High
events — so a rejection empties the queue through a different law
than the transition rule, and the first cut of the
rejection-retires-nothing fact was wrong about frozen behavior. The
fact was rewritten to pin the actual distinction: an ordinary MEDIUM
immediate event (a status note) leaves the queued position line
alone; the rejection's drop is the assertive rule's own and is
recorded here rather than re-tested. A REJECTION is still not a
transition — the mode did not change — and the transition list
excludes it.

Facts: four — the cancel retiring the pending navigation line, the
filter line surviving a transition, the clamp retiring the geometry
line, and the ordinary-immediate-event control. Mutations, each
byte-restored: M1 the retirement arm dropped, M2 transitions
dropping EVERYTHING (the wrong reconciliation — the first aim sat in
dead code behind the Navigation guard and was re-aimed as the
unguarded total drop), M3 the clamped arm dropped from the list. All
bitten.

### TF-7 — placement and align: the verb layer

F5 and F6 land as verbs with their tables total; the SHEET — XAML
overlay, `ModalSurface` membership, the workspace property, the
admission rows — rides TF-8's prompt machinery, one modal-clone pass
for picker and prompts together, exactly as §E's TE-6 record divided
the same ground ("the sheets ... are TE-7's"). The boundary is
recorded here so nobody reads the unpresented event as an accident.

The request is IF-19 discharged: `CanvasCardPickerRequest` captures
the reading-ordered movers, their rects and the LOADED IDENTITY in
one TF-2 lease read at open; a confirm re-validates the identity
FIRST and a stale request refuses PickDifferentTarget writing
nothing. The pick routes mac's switch; a target inside the moving
set refuses PickOutsideMovingSet before any operation exists.
`CanvasPlaceRelative` runs EVERYTHING against the handle inside
prepare-under-the-gate: existence checks with their typed refusals,
`CanvasPlaceNew` with the mover's own box, `CanvasPlaceSet` with
boxes in the set's reading order and the positional Origins mapped
back by that same order — a cardinality mismatch refuses WHOLE
(IF-21; the arm is belt-and-braces against an engine that never
misbehaves in the vault, recorded as such), and the FD-6 arms carry
their VERB IDENTITY (IF-22): Placement and Align, never the generic
action. `CanvasAlignWith` is the same-axis top-edge slot (FD-2) with
the arm mac lacks — already-aligned answers NoChanges and writes
nothing — and the overlap check runs inside prepare with the write
it guards. IF-20 is discharged at the model: `BuildCardPickerModel`
gained the anchor parameter and F5 anchors proximity at the PRIMARY
MOVER; IF-23's disposition is that frozen F6 WINS — the exact slot,
refuse on occupied — and the executable-spec divergence stands
recorded rather than silently resolved. Five palette rows land,
labels byte-identical to mac, no chords as mac allocates none.

Facts: six — the single place end to end, the rigid set by reading
order, the total refusal table, align's three arms, the request's
context with a foreign-identity confirm, and the proximity anchor
compared VERBATIM against core. Mutations, each byte-restored: M1
the identity re-validation dropped, M2 the self-pick guard dropped,
M3 the already-aligned arm dropped, M4 the overlap refusal inverted,
M5 the anchor reverted to the selection — M5's first aim was
INVISIBLE on the fixture's near-collinear geometry (both anchors
ordered the field identically), so the fact moved to a divergent
pair where the nearest-from-one is farthest-from-the-other. All
bitten.

### TF-8 — the prompt machinery, the picker sheet, and staged Connect To…

The one modal-clone pass TF-7 promised: TWO surfaces —
`CanvasCardPicker`, then `CanvasPrompt` declared after it (the
prompt FOLLOWS the picker in the connect flow, so between the two it
wins the tie, the TemplatePicker/Flow precedent) — each with the
FULL membership the TE-7 censuses enumerate: enum member,
state-record field, topmost arm, palette flat read, menu-disable
trigger, XAML overlay, lifecycle observer arm, and the six
ModalSurfaceTests row sites. The window key gate gains both sheets'
arms ahead of the chord ladder: Escape dismisses committing nothing;
Enter submits — on the picker a ROUTED pick closes it while a
refusal keeps it, filter and highlight intact (F5's state-keeping);
on the prompt the answer rides the shipped verb and an empty connect
label SKIPS (FD-4's Enter-skips).

Connect To… is F7 whole: the pick STAGES `CanvasConnectStage` —
origin, target, titles and the entry publication's loaded reference,
immutable, never re-reading live selection (mac's re-read is the
recorded divergence, assigned there). The label step answers into
`CanvasConnect`: IF-27's empty-to-null normalization; the apply runs
ONCE inside prepare-under-the-gate with both endpoints re-resolved,
the self gate belt-and-braces under the pick's own (IF-26 — the
pick's self-refusal speaks PickDifferentTarget for connect, a TARGET
problem, where placement's speaks the moving-set sentence), sides
from `CanvasAutoSides` over the rects, and every `AddEdge` parameter
spelled — None/Arrow ends, the cleaned label, null color. IF-24 and
IF-25 land as DISPOSITIONS: the apply is a funnel operation on the
STAGE's identity, so a reload makes it Stale and frozen §E's
silent-Stale lifecycle governs — the fact deletes the staged target
and finds nothing written and no success spoken. The carried sheets
(FD-4): Rename Group seeds its draft with the CURRENT title and
submits through §E's shipped verb; Set Color's choices are core's
`CanvasColorName` verbatim — never a host copy — plus the No-color
row; both context-menu rows flip LIVE. The connectTo chord lands
(Ctrl+Alt+C, mac's ⌃⌘C). IF-29 (Esc's focus restoration) is the
connect MODE's surface and rides TF-9, recorded.

Facts: seven — the spelled staged apply (its side fragments UNIQUE
to the new edge, because the fixture's own e1 masked the first cut),
empty-label normalization, the self-pick, the silently-Stale
vanished target (first aimed at a GROUP, which §E's delete guard
refuses to delete — re-aimed at a text card), the seeded rename, the
core-named color choices, and the reorder-free refilter. Mutations,
each byte-restored: M1 the normalization dropped, M2 the self-arm
collapsed, M3 the sides swapped (bitten only after the fact's
fragments went unique — the mask was real bytes, not a weak fact),
M4 the draft seed dropped, M5 the names copied. All bitten. Process
note: a heredoc-python patch failed its anchor assert and the
runner's set -e did not trip — caught by the mutation gauntlet
running against an unchanged fact; the patch moved to a file payload
and the gauntlet reran.

### TF-9 — connect mode

F8 lands WITHOUT a transient — that is the design's teeth: the
arrows must stay navigator movement verbatim, and a held transient
would route them to the step machine, so connect mode remembers its
origin in its own small memory (`CanvasConnectOrigin`: id, title,
and the publication identity it was remembered against). The F1a
watcher gains the connect arm — a publish whose loaded reference is
not the origin's clears the memory and cancels through the machine.
Entry rides TF-1's preflight whole; Return with no movement — or on
the origin — ends `CanvasModeEndedWithoutEffect(Connect)` with the
token freed (the fact proves a later verb admits); Return elsewhere
is F7's exact staged apply with label NULL by contract, through
`ConnectForMode` — the same shared preparation the picker flow uses
(one builder, extracted), the operation carrying the mode token and
a completion that maps F4b's rows, with the funnel's confirm
SUPPRESSED so the connected sentence rides Committed(confirmation)
and speaks after the clear. The completion takes its OPERATION as a
parameter — the first cut closed over a variable assigned after
`Apply` returned, and the synchronous funnel's inline completion
read it null; the restructure is the same lesson TF-3's early
memory taught, one seam over.

Esc discharges IF-29: the restoration seats the origin silently AND
addresses the OWNING presenter — `FocusRow(origin)` with the seat as
the fallback — so reader focus returns with selection, honoring
C12/CD-40. IF-28's mode half closes BY CONSTRUCTION: while the token
holds, no funnel verb can remove the origin (the fact that tried
found the delete refused ModeHeld), and every real vanish arrives as
a displacement F1a already cancels — so F8's vanished-origin-cancels
demand is satisfied vacuously; the OnCommit arm still refuses
PickDifferentTarget as belt-and-braces, and the planned
outside-the-stack cancel hook was REMOVED as machinery guarding an
unreachable state. Recorded, not silently dropped: the fact that
would have driven it was cut for an impossible premise. The
connectMode palette row lands, chord-less as mac allocates it.

Facts: four — the end-to-end connect (label absent on disk, sides
unique to the new edge), no-movement ending with the freed token,
Esc's selection-and-focus return, and the displacement cancel via
the TF-2 reload idiom. Mutations, each byte-restored: M1 the
no-effect arm dropped, M2 the re-seat dropped, M3 the focus address
dropped, M4 the watcher arm dropped, M5 the label forced non-null.
All bitten.

### TF-10 — the F9 matrix published, and the suspended column closed

IF-31 demanded the matrix be PUBLISHED, not delegated. Here it is,
the decision table this task ships and its facts pin:

| Row | Normal state | Suspended (conflict pending) |
| --- | --- | --- |
| Mode entry, no selection | `NothingSelected` | ConflictPending (the ladder, ahead of everything) |
| Mode entry, a mode active | frozen C's M7 rejection; the resize chord's quick loop commits its OWN mode | ConflictPending |
| Arrows / steps | the active mode's step; navigator movement otherwise | REFUSED — the ladder's conflict sentence, nothing moves |
| Resize presets | outside resize: `CanvasBlocked(ModeBusy)`; inside: the preset | REFUSED — the conflict sentence |
| Commit / Return | the mode's completion table (F4b) | ConflictPending through the ladder; the mode STANDS |
| Cancel / Esc | the mode's restoration | ALLOWED — local restore, the suspended identity FORGOTTEN |
| Ordinary funnel verbs | `ModeHeld` while a token holds | ConflictPending (the ladder's precedence) |
| Picker rows without their selection | their F5–F7 arms | ConflictPending |
| Header commit/cancel buttons | HIDDEN with no mode (the shipped C gate) | visible; presses refuse through the machine |
| Palette rows | visible; refusals are typed (C9: hidden is for controls that mean nothing; refusal is for state the user can change) | same, the conflict sentence |
| Owner-pane departure | the C machine's cancel | the same cancel; the wrap forgets the identity |

IF-30's matrix half closes with two code changes and one probe. The
probe: `ModeSuspended` on the funnel, true while the yielded identity
is remembered. The first change: the step gate sits INSIDE
`ModeStep` — the one door every step takes, chord-routed or direct —
and the presets gate the same way; both speak `AnnounceConflictPending`,
the LADDER'S own sentence (`CanvasSaveConflict` renders "The canvas
changed on disk. Reload it to continue…"), never a second phrasing.
The second change is a LEAK found while building: the entry wrap
cleared only the live token, so a cancel during suspension left the
suspended identity remembered forever — the wrap now forgets it
beside every clear. Suspension's real exits today are cancel,
departure and reload (nothing writes through
`ConflictResolutionToken` yet and the record's Terminal flag has no
writer); the reinstall arm stands contractual with its TF-1 fact,
recorded. F9a lands as a fact pinning all four stored names
byte-exact — and it caught the connect flows passing the SHORT
operation name where mac's full sentence belonged; both connect
paths now name `connect "A" to "B"`.

Facts: four — the frozen steps and presets under suspension, the
forgotten identity after a suspended cancel, the ladder-refused
second Return, and the verbatim action names. Mutations, each
byte-restored: M1 the step gate dropped, M2 the forget dropped, M3
the preset gate dropped, M4 a name reworded. All bitten.

### TF-11 — the modes journey, and the sweep

The FlaUI modes journey lands beside §E's five: one launch, every
leg — a canvas authored by chord, move mode entered by Ctrl+Alt+G
with the M6 commit control APPEARING (and axe scanning the
mode-active state), an arrow step, Return collapsing the controls;
resize mode entered and Escape collapsing it; Ctrl+Alt+C raising the
card-picker sheet (axe again) and Escape closing it. Budgeted per
the recorded accepted risk: real keystrokes, fresh element probes,
the journey traps honored.

THE JOURNEY'S CATCH — the series' fifth shipped bug found by a real
keyboard: under the production funnel the mode COMPLETION runs on
the worker thread, its teardown fires the aggregate observable from
that thread, and the renderer's `CommitTransient` intake ASSERTED
the commit thread — the thrown assert killed the completion before
`ResolveCommit`, leaving a committed mode's controls up forever
while every unit suite stayed green (their funnel is synchronous).
The intake now marshals ITSELF exactly as the publication intake
does — ID-1's discipline, not an exemption from it — and a unit
regression fact drives the worker-thread commit and finds no throw.

THE SWEEP: a mechanical survey of the ledger against the eleven
records finds every row IF-1 through IF-32 named by the task that
discharged, reconciled, reassigned, or recorded it — none silently
dropped. The dispositions of note: IF-18 reconciled (TF-1/TF-4),
IF-21's cardinality arm belt-and-braces (TF-7), IF-23's divergence
recorded with frozen F6 winning (TF-7), IF-24/IF-25 as lifecycle
dispositions under silent-Stale (TF-8), IF-28's mode half closed by
construction with its hook removed (TF-9), IF-29 discharged at Esc
(TF-9), IF-30's matrix half closed with the published table
(TF-10), IF-31 discharged by that same table, IF-32 reconciled by
naming (TF-6). The task loop is complete; the close-out record
follows.

### §F close-out — the modes shipped, the record closed

Twelve tasks, twelve commits, the ledger swept. What §F leaves
behind: the mode-completion architecture whole — commit as a
COMPLETION with the Pending arm, the early-resolution memory for
synchronous completions, suspension as a first-class column with its
published matrix — move, resize and connect modes end to end on the
real vault; the placement and align verbs under
prepare-under-the-gate; the picker and prompt sheets with their full
modal membership; the renderer drawing the transient through one
identity-checked derivation and one aggregate observable; the
announcer retiring stale navigation on every transition; and the
journeys driving it all with a real keyboard.

The section's discoveries, honestly counted: the synchronous
completion racing the pending mark (TF-3, the controller's early
memory); the CanvasModeRejected High-priority interplay with t0's
assertive rule (TF-6); the connect completion closure reading its
operation before assignment (TF-9, the same lesson one seam over);
the suspended-identity leak on cancel (TF-10); the worker-thread
completion killed by the renderer's commit assert (TF-11, the
journey's catch — invisible to every synchronous unit suite). Owed
to the mac lane, restated from the freeze: the conflict-loses-mode
repair, the try?-swallowed step reads, the prompt's live-selection
re-read. Owed here, recorded: the resolution-continuation reinstall
arm awaits a recovery flow that writes through
`ConflictResolutionToken`; the F1a watcher and the completion remain
the two arbiters until then.

### PR review round 1 — the Displaced completion, confirmed and closed

Codoki's automated round approved the PR and still carried one
finding worth its name, and the finding was REAL: the Displaced
completion arms did nothing on the theory that "the F1a watcher
already cancelled" — but IF-2's own-commit exemption stands the
watcher down exactly while a commit pends, so F4b's "already
cancelling per F1a" never happens in that window and the mode
wedged, pending forever, Escape and Return refusing at a door
nobody could reopen. The two frozen texts reconcile the way IF-2
itself says: THE COMPLETION IS THE ONE ARBITER WHILE PENDING. The
controller gains `ResolveCommitDisplaced` — dispatcher-marshalled
like every resolution, clearing the mark and running the machine's
own cancel so restoration, token clear and the cancelled sentence
all ride the wrapped closures; inside a synchronous commit stack the
arrival is REMEMBERED like the other early arrivals and the cancel
posts outside it, because frozen C refuses a cancel from within a
commit effect. Both completion arms wire to it in one line each.
Four facts (the resolution's two windows at the controller, the
transient and connect halves at the document, each proving the
document FREE afterward); two byte-restored mutations, both bitten.

## PR G — marks: mark-then-act, the marks list, and the bulk verbs

Milestone T's multi-select on Windows (#524, interview decision 4: no
shift-range selection). The STORE shipped in PR A and the §C-unit
split: `CanvasSelection.Marked` behind its two mutators (`ToggleMark`
and `ClearMarks` — "present now so the marked set is never mutated
from two places"), the publication's durable `MarkedIntent`, the
outline rows and renderer peers rendering `marked`,
`CanvasMovedTo`/`CanvasWhereAmI` carrying the flag, and CD-32's
retarget seeding. §E handed off "marks verbs reuse the gate and the
bulk-is-one-action rule; the marks list is the picker's sibling"; §F
built the rigid moving set over test-built marks and recorded the
risk. PR G lands what is missing: the VERBS, the marks list on TF-8's
prompt machinery, the three bulk verbs through the funnel, the chord
and rows, the context row going live, and the journey. Contract
numbering is per-wave: G1–G9, GD-1 onward. This is revision 2, after
round 1's thirty findings (the record follows the section).

**G1 — The store is the one mark authority; the document's two verbs
over it speak the live store count.** `CanvasSelection` owns the
set; the document exposes exactly two store verbs: `ToggleMark` (the
Ctrl+Alt+M chord — mac's ⌃⌘M in FD-3's family — the palette row, and
the context row) and an IDEMPOTENT `Unmark(nodeId)` for the marks
list's row action, both answering the resulting store count. Both
require the target to RESOLVE in the current population (mac's
`doc.outline.first`, `AppState+CanvasActions.swift:753–758`): a
selection that is absent OR holds an id the population no longer
resolves refuses `CanvasStatus(NothingSelected)` and mutates nothing
— the `CanvasMarkToggled` event needs a non-null title
(`slate_uniffi.cs:24956–24960`). Groups are markable (mac marks any
outline row). Toggle announces `CanvasMarkToggled(marked, title,
count)` and Unmark announces `CanvasMarkToggled(false, title, count)`
with the STORE's count after the write, published through the
document's `Marked` republish arm (`WithMarkedIntent`, applied
immediately) so every pane's rows and the peers' `marked` state move
together. Arrows never mutate marks (PR A's rule). Every
context-menu consumer SILENTLY SEATS its source row before invoking
the verb — the outline dispatcher's existing Delete/Set Color shape
(`CanvasOutlineView.cs:923–945`; mac's `CanvasOutlineView.swift:303–305`,
`CanvasTableView.swift:87–92`) — and a vanished source takes G1's
unresolvable-row refusal.

**G2 — Durable per document, honest about the scene, and read by
two rules.** The set lives on the document, shared across panes,
seeded across a retarget (CD-32), cleared when the last tab closes
(PR A's teardown row). Two READ RULES: (a) MEMBERSHIP, LIVE COUNT,
EMPTINESS, and F2's marks-versus-selection choice read the STORE
directly (G1, G3, the list's admission, `CanvasNavigator.cs:625–634`);
(b) every MULTI-NODE TARGET for geometry or mutation reads the
store through core's reading-order projection `CanvasOrderNodes`
(§W-G row F, contract 0b-10), which drops ids the scene no longer
holds SILENTLY — so a ghost mark stays in the store, counts in
Toggle's and Clear's sentences, and vanishes from every target and
from the list's rows and heading. Store count is spoken by Toggle,
Unmark and Clear ONLY; the list heading and every bulk sentence
speak the PROJECTED count.

**G3 — Clear All Marks never refuses on a live document and speaks
the store count.** From the palette row and the marks list's
control: `ClearMarks`, then `CanvasMarksCleared(count)` — the
render's `No marks.` arm at zero, `Cleared ⟨n⟩ mark[s].` otherwise
(vocabulary row). A retired document is the one refusal: the
announcer's retirement boundary drops the sentence and nothing
mutates.

**G4 — The marks list is a KIND of the TF-8 prompt sheet with a live
projection and an addressed landing.** The prompt model becomes an
EXHAUSTIVE variant set (GD-1): the prompt kind (once an enum, now the hierarchy) = ConnectLabel,
RenameGroup, SetColor(target: Selection | Marked), GroupMarked,
MarksList — each kind with its OWN submit arm and NO default arm
(`CanvasPromptViewModel.cs:117–130`'s fall-through retires). The
marks-list request captures the INVOKING SURFACE OWNER (the pane
the verb ran from, TF-7's request shape). Admission reads STORE
emptiness — an empty store refuses `CanvasStatus(NoMarks)` and
presents nothing (mac's `canvasShowMarksList`, `:782–792`); the rows
and heading read the PROJECTION over the full population's reading
order — so a store holding only ghosts opens a zero-row list whose
Clear control removes the ghosts (mac's shape, `CanvasPromptSheet.swift:333–338`).
Rows are keyed by node id and REPROJECT LIVE on every marked-intent
and population change; the accessible name is
`SpeakableName + ", marked"` (`CanvasOutlineRow.SpeakableName`,
`slate_uniffi.cs:15539–15555`, the unique name every Windows
projection already uses) with `Title` for visual display; the
active row survives reprojection by id, and a removed active row's
successor is the next row at the same ordinal, else the previous.
ENTER JUMPS: the selection seats the row SILENTLY (frozen A12,
`SeatSelectionSilently`), the document leaves an A14 focus request
addressed to the captured owner (`RequestFocusLanding(owner,
nodeId)`) that SURVIVES the sheet's closure and that the SURFACE
delivers, and NO `CanvasMovedTo` speaks — the landing is the line.
A row that is filtered out of the owner's projection CLEARS THE
FILTER first (the shipped `CanvasFilterCleared` sentence, never a
silent hidden seat) and then lands (GD-6). A vanished row lands by
A14's own order — the named row, the last activated row, the first
row (`FocusLandingNodeFor`) — and speaks
`CanvasActionFailed(CanvasFailedAction.CanvasAction, "jump")` when
even that fails; the sheet closes on every Jump. DELETE on a row
runs G1's `Unmark` — `CanvasMarkToggled(false, title, storeCount)`
speaks — the sheet stays with the successor active, and CLOSES when
the STORE empties. The Clear control runs G3 and closes. Escape
closes choosing nothing.

**G5 — Bulk verbs are one funnel action each over an IMMUTABLE
snapshot, with a total outcome table.** Each bulk verb captures the
raw marked-intent SNAPSHOT at invocation — the direct row's press, or
the prompt's SUBMIT for Group and Color (live-at-submit, immutable
once submitted) — and mints one §E operation carrying it (E1's
invocation value). Preparation under the gate projects the snapshot
through `CanvasOrderNodes`; an EMPTY projection refuses
`CanvasStatus(NoMarks)` — AFTER admission, under the gate; a THROWN
query (`CanvasOrderNodes` and `CanvasGroupRectAround` both throw
`VaultException`, `slate_uniffi.cs:11165–11166,11179–11182`) refuses
`CanvasActionFailed(CanvasFailedAction.CanvasAction, detail)` writing
nothing, the prompt and the marks preserved. Every bulk target and
count INCLUDES marked groups — mac passes the whole projected set to
all three verbs — and the canonical `card[s]` in the names and
sentences denotes those NODES (GD-2). Names are core's `CountNoun`
over the FFI (`delete ⟨n⟩ card[s]`, `color ⟨n⟩ card[s]`, `group ⟨n⟩
card[s]`). All three carry E4's `KeepSelection` (a deleted selected
node drops by resolution; a later seat survives) and a NEW typed
model-side MARK EFFECT on the operation, `CanvasMarkEffect { Keep,
RemoveCaptured }`, applied in the SAME publication as the refreshed
rows and retained under the operation identity for refresh-only
recovery; later local mark writes win over it (GD-7):

- **Delete Marked** — `DeleteNode(Id)` per projected id in reading
  order; mark effect RemoveCaptured; announces
  `CanvasDeleted(CanvasDeleteTarget.Cards(count), verbosity,
  CanvasPhrase.UndoChord)`. A marked group deletes by the algebra's
  one group removal — frame and incident edges gone, contained cards
  kept (ED-3, `apply.rs:410`).
- **Color Marked** — `SetNodeColor(Id, Color)` per id with the
  storage string (`"1"`–`"6"` or null); mark effect Keep; announces
  `CanvasBulkColorSet(count, color)` with the TYPED
  `CanvasColor.Preset(byte)` or null — two representations, one
  mapping (mac keeps the marks, `:838–865`).
- **Group Marked Cards…** — the GroupMarked prompt kind (Enter with
  an empty field means a null label, IF-27's normalization); the
  frame is `CanvasGroupRectAround(handle, members)` under the gate;
  ONE `CreateGroup(Id: CanvasNewId(), Label: label or null, X, Y,
  Width, Height from the returned CanvasRect, Color: null)` — all
  seven arguments spelled (`slate_uniffi.cs:27113–27121`); mark
  effect RemoveCaptured; announces `CanvasGrouped(count, label ‖
  "Untitled")`. A NULL frame — no member resolves — speaks
  `CanvasActionFailed(CanvasFailedAction.CanvasAction, "group")`
  and writes nothing (GD-3).

The TERMINAL OUTCOME TABLE, per funnel arm — write / marks / prompt
sheet and draft / speech: **Installed**: landed; the mark effect
applied with the refreshed rows; the sheet CLOSES; the verb's
sentence. **RefusedPrepare** (empty projection, thrown query, null
frame): nothing written; marks untouched; sheet and draft KEPT; the
preparation's own typed sentence spoke. **Conflict**: E6's record
installs; nothing written; marks untouched; sheet KEPT (mac keeps
the sheet unless an undo step appended, `AppState+Canvas.swift:509–522`);
the conflict sentence. **Unindexed / RefreshRefused**: the write
LANDED — the mark effect is applied under the operation identity
when the recovery presents; the sheet CLOSES; no spoken success
beyond the refresh-only region (§E's arm). **Displaced**: the
receipt quarantined, nothing published; marks untouched; sheet KEPT.
Admission refusals (the ladder, G7) keep sheet and draft and speak
the ladder's sentence. No arm that wrote nothing closes the sheet.

**G6 — Marks and modes: store actions are free, funnel actions
answer the ladder, and a holder never re-reads the store.** The
marked set IS the moving set at ENTRY (F2, unchanged); a mode's
captured holder is IMMUTABLE — Toggle, Unmark, Clear, Show List,
Jump, Escape and prompt OPENING are store or focus actions allowed
during a held mode, during a pending commit and during suspension,
and never alter the current holder; only a later mode entry reads
the new set (GD-4). Jump during connect mode moves the reader as
the mode's own arrows do; the origin memory stands. Prompt
SUBMISSION (Group, Color) and Delete Marked are funnel verbs: under
a held mode `ModeHeld` → `CanvasBlocked(ModeBusy)` (F4d); under
suspension ConflictPending; under a busy gate Busy — the ladder's
sentences, sheet and draft kept. A mode commit KEEPS the marks; a
mode's own Installed completion does not touch them.

**G7 — Controls, the literal ladder, and the matrix (M6, F9's
pattern).** Palette rows, labels byte-identical to mac (P3):
`toggleMark` "Canvas: Toggle Mark" (Ctrl+Alt+M), `showMarks` "Canvas:
Show Marked Cards", `clearMarks` "Canvas: Clear All Marks",
`groupMarked` "Canvas: Group Marked Cards…", `deleteMarked` "Canvas:
Delete Marked Cards"; and `colorMarked` "Canvas: Color Marked Cards…"
— mac's `canvasColorMarked` has no caller (recorded divergence), so
this row is Windows' front door onto the SetColor kind with the
Marked target, its title carrying the projected count (GD-5). The
context-menu Toggle Mark row goes LIVE with the seat rule of G1; its
"arrives later" reason retires. THE LITERAL ORDERED LADDER every
bulk verb answers, the shipped one (`CanvasMutationFunnel.cs:95–144`,
frozen TE-5a): NotReady → RecoveryPending → ConflictPending →
ModeHeld → Stale → Busy ‖ BusyAlreadyAnnounced → (post-acquire
rechecks: ModeHeld, ConflictPending) → Admitted; ONLY THEN the
projection and `NoMarks`. So an empty store under suspension says
ConflictPending here where mac's pre-admission emptiness check says
`No marks.` (recorded divergence). The remaining cells: Toggle with
no or unresolvable selection → `NothingSelected`; list opening with
an empty STORE → `NoMarks`; Jump's filtered and vanished rows per G4.

**G8 — History: one checkout of the single returned inverse, and
marks are not history.** §E's seam applies the ONE inverse action
the apply returned, and the effect is the verb's own: Delete's
inverse restores the removed structure, Color's restores the prior
colors, Group's removes the created group. `CanvasHistoryApplied(verb,
name)` renders core's `Undid: ⟨name⟩` ‖ `Redid: ⟨name⟩` (vocabulary
row) — so `Deleted ⟨n⟩ cards — Ctrl+Z to undo` (standard verbosity)
is followed by `Undid: delete ⟨n⟩ cards`, the pair agreeing at ≥ 1000
because the NAME was built by `CountNoun` over the FFI; host-composed
history or delete prose is forbidden (0a). Marks and selection are
NOT history: undo and redo never snapshot or replay them — after an
undone Delete or Group the marks stay cleared, after an undone Color
they stay live, and the selection is whatever live intent resolves
after the checkout (mac's shape; GD-7).

**G9 — Verification: the journey and the named facts.** The FlaUI
marks journey — two cards authored by chord, both marked by
Ctrl+Alt+M with the rows' `marked` state visible to UIA, the marks
list opened (sheet + axe), Jump landing reader focus on the row,
Delete Marked removing both in one action, Ctrl+Z restoring both —
is the end-to-end leg; the tasks' fact tables carry a named fact for
every G1–G8 row, every refusal and outcome cell, all three inverses,
the mark-effect atomicity, the context seat, the store-versus-
projection counts, the filtered and vanished Jump cells, and each
recorded divergence's Windows side.

### Decisions

- **GD-1.** The marks list is a KIND of the TF-8 prompt sheet — one
  modal membership, an exhaustive variant model with per-kind submit
  arms and no default; keyed row actions (Enter jumps, Delete
  unmarks) rather than per-row buttons.
- **GD-2.** All three bulk targets and counts include marked groups;
  `card[s]` denotes nodes; Delete removes a group by the algebra's
  one removal (cards kept).
- **GD-3.** A null group frame speaks the FD-6 arm; mac's silence is
  a recorded divergence assigned to the mac lane.
- **GD-4.** Store and focus actions are free under every mode state;
  a captured holder is immutable; only entry reads the store.
- **GD-5.** Color Marked lands with a Windows front door; mac's
  missing row is a recorded divergence assigned there.
- **GD-6.** Jump onto a filtered-out row clears the filter (spoken)
  and lands; mac's silent hidden-node select is a recorded
  divergence.
- **GD-7.** Marks carry a typed model-side effect applied with the
  refreshed rows; history never replays marks or selection.

### Recorded divergences (mac, admitted — assigned, not imported)

- mac's `canvasGroupMarked` is a silent no-op when no member
  resolves (GD-3); the Swift repair is the mac lane's.
- mac's `try?` also silences a thrown `canvasGroupRectAround`
  (`:885–891`), and mac maps a thrown `canvasOrderNodes` to `[]` and
  then `NoMarks` (`:545–546`); Windows speaks the FD-6 arm for both.
- mac's `canvasColorMarked` has no palette row or chord (GD-5).
- mac's marks list Jump calls `canvasSelect` with its default
  ANNOUNCING behavior and addresses no reader focus
  (`CanvasPromptSheet.swift:343–348`); Windows seats silently and
  lands through A14.
- mac's marks list row Unmark is silent (`:350–355`); Windows speaks
  `CanvasMarkToggled(false, …)`.
- mac's marks list names rows by `title`, not `SpeakableName`
  (`:333–338`); the Windows list uses the unique name.
- mac's marks list Jump selects a filtered-out node silently; Windows
  clears the filter first (GD-6).
- mac's `canvasPromptGroupMarked` checks store emptiness BEFORE
  admission (`:916–922`); Windows admits first, so empty-plus-suspended
  says ConflictPending here and `No marks.` there.

### Accepted risks

- The marks list rides the prompt sheet's choices shape with keyed
  actions; a per-row button layout is later polish if UIA clients
  prove to need it.
- The journey budgets one launch for every leg, as §F's did.

### Round 1 (revision 1 → revision 2) — thirty findings, each closed

| Finding | Closed by |
| --- | --- |
| IG-1 store authority / idempotent Unmark | G1 (two document verbs over the store; `Unmark` idempotent) |
| IG-2 unresolvable selection | G1 (absent OR unresolvable → NothingSelected, no mutation) |
| IG-3 the read rule over-claimed | G2 (two read rules; membership/count/emptiness read the store) |
| IG-4 store vs projected count | G2 (Toggle/Unmark/Clear speak the store; list and bulk speak the projection) |
| IG-5 row naming | G4 (`SpeakableName + ", marked"`; divergence recorded) |
| IG-6 Jump's focus seam | G4 (owner captured; A14 `RequestFocusLanding` survives closure; the surface delivers) |
| IG-7 Jump's seat announces | G4 (silent seat, no `CanvasMovedTo`; divergence recorded) |
| IG-8 silent row Unmark | G4 (`Unmark` → `CanvasMarkToggled(false, …)`; divergence recorded) |
| IG-9 live projection / successor | G4 (rows keyed by id, live reprojection, next-else-previous) |
| IG-10 store vs projection emptiness | G4 (admission on the store, rows on the projection) |
| IG-11 vanished-row landing | G4 (A14's order; the FD-6 "jump" arm when even that fails) |
| IG-12 filtered-row Jump | G4/GD-6 (clear the filter, spoken; divergence recorded) |
| IG-13 prompt variants | G4/GD-1 (exhaustive kinds with targets; no default submit) |
| IG-14 prompt lifecycle | G5's outcome table (no no-write arm closes the sheet; live-at-submit, immutable after) |
| IG-15 snapshot timing | G5 (immutable snapshot at invocation or submit, projected in prepare) |
| IG-16 atomic mark effect | G5/GD-7 (CanvasMarkEffect, applied with the refreshed rows, retained for recovery) |
| IG-17 selection effect | G5 (`KeepSelection` for all three) |
| IG-18 the literal ladder | G7 (published in order; NoMarks after admission; divergence recorded) |
| IG-19 terminal outcomes | G5's table (every funnel arm) |
| IG-20 thrown queries | G5 (FD-6 rows; two mac silences recorded) |
| IG-21 CreateGroup shape | G5 (seven arguments spelled) |
| IG-22 color representations | G5 (storage string for the op, typed `CanvasColor` for the event) |
| IG-23 `CanvasFailedAction.CanvasAction` | G4/G5 (spelled everywhere) |
| IG-24 mode/suspension cells | G6 (store and focus actions free; funnel actions answer the ladder; holder immutable) |
| IG-25 context seat | G1 (every context consumer seats silently first) |
| IG-26 groups in counts | G5/GD-2 (all three include groups; `card[s]` = nodes) |
| IG-27 "restores every node" | G8 (one inverse; per-verb effect) |
| IG-28 history render | G8 (`Undid: ⟨name⟩`; host prose forbidden) |
| IG-29 marks and history | G8/GD-7 (never snapshotted or replayed) |
| IG-30 verification | G9 (named facts per row and cell; the journey kept) |


### THE FREEZE — read this first

**This section is CLOSED to further prose revision**, frozen at
revision 2 after two adversarial rounds, by APPLICATION OF THE
STANDING PRECEDENT — the owner's ruling that closed §C-unit, §D, §E
and §F, applied here by the session rather than freshly ruled, and
recorded as such; the owner may overrule. The trajectory is rule 5's
signal on the same schedule as §E and §F: thirty findings, then
twenty-six with NINE of the twenty-four blockers CREATED by revision
2's own closures — the prompt variant model, the A14 landing from a
closing sheet, the filter-clearing Jump, the typed mark effect — which
is the protocol's own definition of a patch over a missing model
rather than a repair. Prose cannot close what the round found; the
findings become the ledger below, each discharged BY CODE in the task
whose surface it names, each closure recorded with its facts and its
byte-restored mutations, exactly as the IF ledger was.

The frozen text stands with its known holes NAMED: the publication as
the one mark authority (IG-31), the three store verbs (IG-32–35), the
list as a currency-bound read machine (IG-36), the prompt variants as
a sealed hierarchy with sheet identity and submit results (IG-37–38),
A14's real outcomes and post-close posting (IG-39–41), the filter
announcement as the one arbiter before landing (IG-42), the zero-row
and externally-emptied list states (IG-43–44), per-id mark epochs and
the retained receipt (IG-45–46), the split post-commit and
continuation outcomes (IG-47–50), the ladder's silent arms (IG-51),
core vocabulary arms in place of static detail strings (IG-52, under
FD-6's lifted ceiling and the RefreshPending precedent), the
resolution-versus-action rows (IG-53), the name composition (IG-54),
the count-title (IG-55), and the inverse guarantee's true scope
(IG-56). Where a task finds the frozen text and the shipped code in
conflict, the shipped frozen sections win and the record says so.

### Round record

- **Round 1** (xhigh, conforming): 30 findings — 28 blockers, 2
  majors — closed by revision 2's text (the table above).
- **Round 2** (xhigh, conforming): 26 findings — 24 blockers, 1
  major, 1 minor — nine created by revision 2. Rule 5's signal; the
  precedent applied; the findings are the ledger.

### The ledger — IG-31 … IG-56, discharged by code

| Row | Severity | Contracts | Origin | Claim | Rides |
| --- | --- | --- | --- | --- | --- |
| IG-31 | BLOCKER | G1/G2/GD-7 | survived round 1 | “`CanvasSelection` owns the set” and the publication merely republishes it. | TG-0 |
| IG-32 | BLOCKER | G1/G3 | created by revision 2 | G1 says the document exposes “exactly two store verbs,” `ToggleMark` and `Unmark`. | TG-0 |
| IG-33 | BLOCKER | G2/G3 | survived round 1 | “`ClearMarks`, then `CanvasMarksCleared(count)`,” with Clear speaking the store count. | TG-0 |
| IG-34 | BLOCKER | G1/G4 | created by revision 2 | “Both” `ToggleMark` and `Unmark(nodeId)` refuse when “a selection is absent.” | TG-0 |
| IG-35 | BLOCKER | G1/G7 | survived round 1 | An absent or unresolvable selection always maps directly to `NothingSelected`. | TG-0 |
| IG-36 | BLOCKER | G4/G6 | survived round 1 | The list opens and reprojects live via `CanvasOrderNodes`, including under pending commit and suspension. | TG-2 |
| IG-37 | BLOCKER | G4/GD-1 | survived round 1 | `CanvasPromptKind = … SetColor(target: Selection \| Marked) …` is an exhaustive variant model. | TG-1 |
| IG-38 | BLOCKER | G4/G5 | survived round 1 | No no-write outcome closes the prompt; installed outcomes close it. | TG-1 |
| IG-39 | BLOCKER | G4 | created by revision 2 | The sheet raises `RequestFocusLanding` and then closes; the request “survives” and the surface delivers it. | TG-2 |
| IG-40 | BLOCKER | G4 | survived round 1 | The captured owner’s surface delivers the Jump landing. | TG-2 |
| IG-41 | BLOCKER | G4 | created by revision 2 | A vanished row uses A14’s order and speaks `CanvasActionFailed(…, "jump")` “when even that fails.” | TG-2 |
| IG-42 | BLOCKER | G4/GD-6 | created by revision 2 | For a filtered row, `CanvasFilterCleared` is “spoken first” and focus then lands. | TG-2 |
| IG-43 | BLOCKER | G4 | created by revision 2 | A ghost-only store opens a zero-row modal list with a Clear control. | TG-2 |
| IG-44 | BLOCKER | G4/G5/GD-7 | created by revision 2 | The live list closes only when its own row Unmark or Clear empties the store. | TG-2 |
| IG-45 | BLOCKER | G5/GD-7 | created by revision 2 | “Later local mark writes win” over a delayed `RemoveCaptured`. | TG-3 |
| IG-46 | BLOCKER | G5/GD-7 | survived round 1 | The mark effect is retained “under the operation identity” for refresh-only recovery. | TG-3 |
| IG-47 | BLOCKER | G5 | survived round 1 | `Unindexed` and `RefreshRefused` are one landed-write/recovery outcome. | TG-3 |
| IG-48 | BLOCKER | G5/G6 | survived round 1 | `Conflict` is a terminal outcome with the prompt kept. | TG-3 |
| IG-49 | BLOCKER | G5 | survived round 1 | Every funnel arm is in the terminal table. | TG-3 |
| IG-50 | BLOCKER | G5 | survived round 1 | `RefusedPrepare` always has “the preparation’s own typed sentence.” | TG-3 |
| IG-51 | BLOCKER | G5/G7 | survived round 1 | “Admission refusals … speak the ladder’s sentence.” | TG-3 |
| IG-52 | BLOCKER | G4/G5/GD-3 | survived round 1 | Failures use `CanvasActionFailed(CanvasAction, "jump")` and `CanvasActionFailed(CanvasAction, "group")`. | TG-2/TG-5 |
| IG-53 | MAJOR | G6/GD-4 | survived round 1 | Jump/list/prompt-opening are freely allowed during conflict suspension and pending commit. | TG-6 |
| IG-54 | MINOR | G4 | survived round 1 | `SpeakableName` is “the unique name every Windows projection already uses.” | TG-2 |
| IG-55 | BLOCKER | G7/G5/GD-5 | created by revision 2 | The Color Marked title carries the projected count, while the operation captures a fresh snapshot at Submit. | TG-1 |
| IG-56 | BLOCKER | G8 | survived round 1 | Every bulk action has one real returned inverse: Delete restores structure, Color restores colors, and Group removes the group. | TG-6 |

### The task loop

| Task | Slice | Discharges |
| --- | --- | --- |
| TG-0 | The store verbs: document ToggleMark/Unmark/ClearMarks over the store with resolve-or-refuse, the live-count sentences, the Ctrl+Alt+M chord, the palette rows, the context row live with the silent seat | G1, G2(a), G3, G7's store cells |
| TG-1 | The prompt variant model: exhaustive kinds with targets and per-kind submit arms, no default; the Group Marked and Color Marked (Marked target) kinds; the sheet/draft outcome table's keep-on-refusal seam | G4's model, G5's sheet column, GD-1, GD-5 |
| TG-2 | The marks list kind: live projection keyed by id with the successor rule, store-vs-projection admission, SpeakableName rows, Enter's A14 landing surviving closure with the filter-clearing and vanished-row cells, Delete's spoken Unmark, Clear, Escape | G4, GD-6 |
| TG-3 | The bulk substrate: the immutable snapshot operation, the typed mark effect applied with the refreshed rows and retained for recovery, the ladder-then-NoMarks order, the thrown-query rows, the per-arm outcome table | G5's frame, G6's funnel cells, G7's ladder, GD-7 |
| TG-4 | Delete Marked and Color Marked end to end: reading-ordered ops, CountNoun names, the two color representations, KeepSelection, the sentences | G5's first two verbs, GD-2 |
| TG-5 | Group Marked end to end: the seven-argument CreateGroup from core's frame, the null-frame FD-6 arm, the label normalization, the sentence | G5's third verb, GD-3 |
| TG-6 | Marks and modes, history: the holder-immutability and mode/suspension cells, the one-inverse facts with the exact Undid render, marks-are-not-history, the action-name drift | G6, G8, GD-4 |
| TG-7 | The journey, axe, and the ledger sweep | G9, the sweep |

Ledger rows not named in a task's row ride the task whose surface
they touch; the closing record sweeps stragglers. Each task lands
with its facts, its byte-restored mutations, its record here, and a
raised citation floor — the §F gauntlet, unchanged.

### Implementation records

### TG-0 — the mark verbs, and the publication as the one authority

IG-31 is discharged BY CONSTRUCTION. The publication's marked intent
is the one mark authority: the document's three verbs — `ToggleMark`
over the selection, `Unmark` by explicit id, `ClearMarks` — compute
their successor from the current publication's marked intent,
publish `WithMarkedIntent`, and apply at once; the selection's set is
the applied PROJECTION, seeded by `SeedMarks` at the top of the apply
(before the rows rebuild, so every projection reads the applied
marks) and raised only when its contents change. The mirror's public
`ToggleMark` RETIRED, so the second mutation path IG-31 named is
gone at the API; the mirror keeps its two one-way seeds — a
retarget's `SeedFrom` (CD-32, its fact migrated and green) and a
teardown's `ClearMarks` — which the document's guarded republish arm
carries into the publication, and an apply-side seed never echoes
back (the apply flag guards the arm). The order is the frozen one
(IG-35): a plainly absent selection refuses NothingSelected; then
C4's `AdmitStructuralRead` speaks its own state; then the id must
resolve in the admitted population — the sentence needs a title —
else NothingSelected. `Unmark` never consults the selection and is
idempotent: an unmarked id answers the count and speaks nothing
(IG-34); `ClearMarks` captures the PRE-CLEAR count (IG-33); three
verbs are named (IG-32). Ctrl+Alt+M lands as mac's ⌃⌘M in FD-3's
family; the Toggle Mark and Clear All rows carry mac's labels; the
context-menu Toggle Mark row goes LIVE, its "arrives later" reason
retired, and the outline dispatcher seats the source row silently
before the verb (IG-25's rule, cited wiring).

Facts: five — the authority transform both ways with the live
count, the selection-then-resolution order, Unmark's idempotence
without a selection, the pre-clear count and the zero arm, the
chord. The eight existing facts that wrote the mirror directly now
go through the verb. Mutations, each byte-restored: M1 the
apply-side seed dropped (publication marked, mirror stale — the
first aim, a mirror-only write, was a NON-BUG the one-way seed heals
by design, and is recorded as such), M2 the resolution refusal
dropped (a compilable fallback-title mutant; the first attempt did
not compile and counted for nothing), M3 the count read after the
clear, M4 idempotence broken. All bitten.

### TG-1 — the prompt variant model: payloads, results, one identity

IG-37 is discharged BY CONSTRUCTION. The prompt machinery is a
sealed hierarchy: an abstract `CanvasPromptViewModel` owns the
bindable surface the one XAML sheet reads (title, draft, the
text-versus-choices shape, the choices and the active choice), and
one class per variant carries its own payload — the connect stage,
the group id, the color choices built from core's `CanvasColorName`
— with an abstract `Submit` every variant MUST implement. There is
no default arm and no nullable payload assertion: a variant that
cannot submit cannot exist. IG-38 is discharged the same way: a
submit answers `Refused`, `Pending` or `Completed`, and the
workspace closes the sheet on the RESULT, never on the keypress —
Completed closes now; Pending closes when the variant's exact
operation LANDS (the three verbs `CanvasRenameGroup`, `CanvasSetColor`
and `CanvasConnect` now answer their operation and carry a
completion; the landing arms are Installed and the two
committed-unpresented arms, G5's table); Refused keeps the sheet and
its draft. The landing marshals HOME — the completion runs on the
funnel's worker (TF-11's lesson), so the submit captures the
submitting thread's dispatcher and posts the closure to it — and the
closure runs only if THAT sheet is still the current one, so a stale
landing can never close a successor. Re-entrant opening is REFUSED
rather than overwritten: unreachable from the surfaces (the sheet
owns the keys; the palette refuses beneath every sheet), the guard
makes the programmatic path honest. The GroupMarked and MarksList
variants and the Marked color target join with their verbs (TG-2,
TG-4, TG-5), each forced to its own arm by the base.

Facts: four, at the workspace where the sheet lives — a Pending
submit's sheet standing until the pumped landing closes it with the
write on disk; a refused submit (the connect's target deleted before
Enter) keeping sheet and draft through the pump; re-entrant opening
refused with the first sheet standing; a stale landing leaving a
successor sheet standing. Mutations, each byte-restored: M1 Pending
closing immediately, M2 Refused closing, M3 the re-entrancy guard
dropped, M4 the identity check dropped. All bitten. Citation repair,
recorded: frozen G4 cited the prompt-kind enum this task retired; the
citation census refused the dead name, and the reference now reads
as prose naming its successor — the prose is not re-litigated, a dead
citation is.

### TG-2 — the marks list: live, currency-bound, close-then-land

The picker's sibling lands as `CanvasMarksListPrompt`, a variant of
TG-1's hierarchy whose bindable surface is the base's — the prompt
base gained change notification for exactly this task. It is a LIVE
projection (IG-36): the document's `ProjectMarkedRows` reads the
store through core's reading order under the lease in ONE try, and
the variant reprojects on every applied publication — marks and
population alike — with rows keyed by node id, named as every
Windows projection names a card, `CardReference` plus ", marked"
(IG-54), the heading carrying the projected count, the active row
surviving by id and a removed one's successor the next at the same
ordinal, else the previous. Admission reads STORE emptiness through
`OpenMarksList` — an empty store refuses `NoMarks` and presents
nothing — while the rows read the projection, so a store holding
only ghosts opens a zero-row list whose rows CANNOT take focus (the
list's `Focusable` binds to `HasRows`), whose Clear control shows
and, having emptied the store, closes it (IG-43); the store emptying
closes the sheet however it emptied — an external unmark, another
pane's clear, a later mark effect — through the workspace's
close-if-current (IG-44), and the sheet's closure hook unsubscribes
the projection in the one place a sheet stops being current.

Enter JUMPS close-first-then-land (IG-39): the seat is silent (no
moved-to line, frozen A12), a Visual owner switches to the outline
through the shipped `ShowSurface` (IG-40), a filtered-out row clears
the filter and the announcer's new `FireFilterLineNow` emits the
filter line as the one arbiter before the landing posts (IG-42), and
the A14 request — addressed to the owner captured at open, the tab
the pane renders — posts at background priority so it runs only
after the workspace has cleared the sheet; A14's own outcomes
(delivered, pending, dropped) are the landing's, and the invented
synchronous failure arm is gone (IG-41). Delete unmarks the active
row through the document's idempotent, spoken `Unmark`. A refused
lease or a thrown query speaks the C4 table's `NotReadable` — an
existing core arm, no host prose — and the sheet keeps its last
current rows (IG-52's list half; the group half rides TG-5). The
Show Marked Cards palette row lands with mac's label; the Delete key
joins the prompt sheet's gate.

Facts: six, at the workspace — the empty-store refusal; the live
projection with the external unmark's successor and the store-empty
close; the Jump's close-first-then-land (the pin is that the
document's standing request is unchanged until the pump, since the
tab's own nodeless request stands headless); Delete's spoken unmark
keeping the sheet; the ghost-only zero-row list and its Clear; the
filtered-row Jump clearing first. Mutations, each byte-restored: M1
the store-empty close dropped, M2 the landing posted before the
close, M3 the filter clearance dropped, M4 the successor rule
dropped, M5 admission on the projection instead of the store. All
bitten.

### TG-3 — the bulk substrate: mark epochs, the effect as receipt, split outcomes

The publication gains per-id MARK EPOCHS (IG-45) behind the ONE
transform every mark write already goes through: `WithMarkedIntent`
stamps a fresh epoch from a monotonic clock on every id newly
present, drops the epoch of every id no longer present, and leaves an
id present before and after with the epoch it had — callers never
stamp anything. The operation carries a typed `CanvasMarkEffect`
(Keep, RemoveCaptured) and `CapturedMarks`, the immutable id-to-epoch
snapshot of its invocation, plus a once-only applied flag: THE
OPERATION IS ITS OWN RECEIPT (IG-46/GD-7). The effect applies in the
SAME republish as the refreshed rows — `CanvasLeaseTransfer.Republish`
took a marks transform beside the seat, the pipeline threads it, and
`RefreshAndPublish` supplies `CanvasMarkEffectPlan.ResolveMarks`,
which removes a captured id only while its CURRENT epoch is still the
captured one, so an id unmarked and re-marked after the capture — a
later local write — survives. History never touches marks (G8).

The outcomes split as the ledger demanded. `DisplacedBeforeApply`
(IG-49): the lease refused before the write; nothing landed, nothing
spoke, frozen Stale's own silence. `ApplyRefused` (IG-50): a
non-conflict, non-unindexed engine refusal is its own arm, and the
funnel speaks `CanvasActionFailed(CanvasFailedAction.CanvasAction,
detail)` with the engine's DYNAMIC message — dynamic data, never host
prose. `RefreshRefused` (IG-47) — and a post-commit read that throws,
caught at the refresh — now retains the recovery receipt exactly as
Unindexed does: the write LANDED, the publication names the
operation, and the funnel holds the operation itself as the receipt
(`UnpresentedReceiptForTests` reads it back). The ladder's silent arms
— Stale and the repeated Busy — stay silent as frozen TE-5a says
(IG-51, the contract's enumeration, no code). Both mode completions
map the new arms: ApplyRefused resolves Refused, DisplacedBeforeApply
resolves as Displaced does.

Recorded, not silently dropped: IG-48's continuation matrix and the
recovery's re-present have NO consumers today — no resolution verb
writes through `ConflictResolutionToken`, the record's Terminal flag
has no writer, and `WithPresented` has no caller — so the receipt is
held on the funnel until that operation's own refresh installs (the
first attempt or any continuation re-run through the same
`RefreshAndPublish`), which applies the effect once by the flag. The
sheet column of those rows is TG-1's landing rule, already
identity-bound.

Facts: four — the effect landing in the very publication that carries
the write's new rows (the mark effect assertion is on the carrier
publication itself); the re-marked id surviving RemoveCaptured with
the untouched one leaving; the apply refusal as its own spoken
outcome on the funnel harness; the throwing refresh retaining the
receipt. DisplacedBeforeApply is reachable only between admission and
transaction under the asynchronous runner and stands typed but
unpinned, recorded. Mutations, each byte-restored: M1 the effect
ignored, M2 the epoch comparison dropped, M3 the refusal silent
again, M4 the receipt dropped on RefreshRefused. All bitten.

### TG-4 — Delete Marked and Color Marked: one action each on the substrate

Both bulk verbs ride ONE frame, `SubmitBulkMarked`: the mark
snapshot — id to epoch — captured at invocation (IG-15), the
operation minted with `KeepSelection` (IG-17) and its typed mark
effect, the ladder answered first, and the projection through core's
reading order UNDER THE GATE — an empty projection refusing `NoMarks`
after admission (G7's order), a thrown query speaking
`CanvasActionFailed(CanvasFailedAction.CanvasAction, detail)` with
the engine's dynamic message — then one op per projected node and
the name core's `CountNoun` over the FFI, so the verb's sentence and
the undo sentence group identically at every magnitude (F9a). Delete
Marked removes the captured marks with the refreshed rows
(RemoveCaptured, TG-3's plan) and a marked group goes by the
algebra's one group removal, cards kept (GD-2); Color Marked keeps
the marks (Keep) and feeds the storage string to `SetNodeColor` while
the sentence carries the typed color (IG-22). Undo restores the
structure through the one inverse and the marks stay cleared —
marks are not history (G8/GD-7).

Color Marked lands with a Windows front door (GD-5): `RequestColorMarked`
refuses `NoMarks` on an empty store (a prompt opening is a store
action, G6) and otherwise presents the Set Color prompt over the
MARKED target — `CanvasSetColorPrompt` gained the target, its submit
routing to the same bulk frame, its title "Set Color for Marked
Cards" carrying NO count (IG-55: the submit's snapshot is the truth;
a title count would be a second, stale reading). The Delete Marked
and Color Marked palette rows land with mac's labels where mac has
them and the recorded divergence where it does not.

Facts: four — Delete Marked as one action with the group removed,
the name, the cleared marks, the resolved selection, the spoken
count and the undo restoring structure with marks still cleared;
Color Marked keeping the marks with typed per-node color asserts
(the fixture's own edge carries the same color, so a byte count
would have lied); the empty projection refusing after admission
with the ghost mark standing; the marked color prompt's countless
title routing to the bulk verb. Two first cuts were fact errors
against this fixture — the edge's color and the other harness's
node — corrected, not the verbs. Mutations, each byte-restored: M1
delete keeping the marks, M2 a host-composed name, M3 color clearing
the marks, M4 the empty projection landing an empty action. All
bitten.

### TG-5 — Group Marked: core's frame, the seven-argument group, the closed arm

The third bulk verb lands through `SubmitGroupMarked` and its prompt
variant `CanvasGroupMarkedPrompt` — a text kind of TG-1's hierarchy
whose Enter with an empty field means an unlabeled group (the verb
normalizes empty to null, IF-27's rule once more). The submit runs
ONE `CreateGroup` under the gate: the mark snapshot captured at the
submit, the ladder first, then `CanvasOrderNodes` and
`CanvasGroupRectAround` inside one try — a thrown query speaks
`CanvasActionFailed(CanvasFailedAction.NewGroup, detail)` with the
engine's dynamic message (IG-20's second silence, typed here), an
empty projection refuses `NoMarks` after admission — and every one of
the seven CreateGroup arguments is spelled from the returned rect
(IG-21): the minted id, the normalized label, X, Y, Width, Height,
null color. The captured marks leave with the refreshed rows
(RemoveCaptured), the name is `group ⟨n⟩ card[s]` by CountNoun, and
the sentence is `CanvasGrouped(count, label ‖ "Untitled")`.

The NULL-FRAME arm (GD-3) is decided under IG-52: the frozen text's
`"group"` detail is static host vocabulary, which 0a forbids, and a
null frame from core after a projection that RESOLVED its members
means exactly "no member resolves" — so the arm speaks the closed
core sentence for that, `CanvasStatus(NoMarks)`, and writes nothing.
It is belt-and-braces by construction (core frames every resolved
scene node) and stands recorded as such; mac's silence there is the
registered divergence. The front door `RequestGroupMarked` refuses
`NoMarks` on an empty store — a prompt opening is a store action (G6)
— and the Group Marked Cards… palette row lands with mac's label.

Facts: three — the set wrapped at a frame that contains every
member (checked against the members' own rects), the name, the
cleared marks and the labeled sentence; the empty label as a null
label with "Untitled" spoken; the door refusal. Mutations, each
byte-restored: M1 the frame's extents swapped (the first aim, X and
Y, was INVISIBLE on this fixture — both are −40 — and was re-aimed at
width and height, recorded), M2 the label normalization dropped, M3
the marks kept after grouping, M4 the door refusal dropped. All
bitten.

### TG-6 — marks and modes, marks and history: the cells pinned

This task ships facts more than code, because the substrate the
earlier tasks built already IMPLIED every cell — and the point of
pinning is that the implication cannot drift. G6 (GD-4): a mark write
during a held mode is FREE and never alters the holder — the
captured set stands at its entry size while the store grows, cancel
leaves the marks alone, and only the next entry reads the new set; a
bulk verb under a held mode is a funnel verb and is refused ModeHeld
with the ModeBusy sentence, the marks and the mode standing; under
suspension the same verb answers the ladder's ConflictPending — the
"changed on disk" sentence, TF-10's column — writing nothing; a mode
commit KEEPS the marks, the moving set surviving its own move. G8
(GD-7): undo of a bulk verb speaks core's `CanvasHistoryApplied`
render VERBATIM — `Undid: color 2 cards`, `Undid: group 2 cards` —
and applies the one returned inverse, Color's restoring the prior
colors and Group's removing the created group, while the marks are
NOT history: the color's marks stay live across the undo, the
group's stay cleared.

IG-53's REACHABLE row is pinned: a reload while the marks list is
open — the one displacement the shipped machinery can produce — the
marks carried, the list reprojected, the sheet standing. The
resolution rows (overwrite, save-copy, reconflict) await resolution
verbs that do not exist (TG-3's finding), recorded as future
machinery. IG-56 closes by SCOPE: the inverse guarantee holds for
the arms that carry a `CanvasApplyResult`; the unindexed save's
recorded empty inverse is §E's shipped receipt shape and stands,
recorded rather than superseded. The action names were pinned by
their tasks (TG-4, TG-5).

Facts: six — the holder untouched by a mark write with the next
entry reading the new set; ModeHeld under a held mode; ConflictPending
under suspension; the commit keeping the marks; the undo renders and
inverses with the marks untouched; the reload with the list open.
Mutations, each byte-restored: M1 a mode guard added to Toggle (GD-4
regressed), M2 the move commit clearing the marks, M3 history
clearing the marks. All bitten.

### TG-7 — the journey, axe, and the ledger sweep

G9 as one FlaUI journey, `CanvasMarks_ToggleListJumpDeleteAndUndo_AreReachable`
(gate W-C): a real keyboard authors two cards by Ctrl+Alt+N, marks
the second by Ctrl+Alt+M, steps Up and marks the first, and the
tree's two rows carry `marked` in their UIA ItemStatus; the palette
opens the marks list ("Canvas: Show Marked Cards…"), the sheet is
found by its id and axe scans it clean (`canvas-marks-list`); Enter
Jumps — the sheet closes and reader focus lands on a card row; the
palette's Delete Marked removes both in one action and the onboarding
returns; Ctrl+Z restores both. One launch, every leg in sequence.

The journey found what no fact had: a mark toggle is a marks-only
publication — the loaded population is the same reference, the filter
is unchanged, so `OutlinePublished` never fires — and the outline's
row view-models were snapshots whose `Status` was get-only, refreshed
only by a rebuild. The surface's peers read `IsMarked` live and were
right; the tree's ItemStatus stayed "1 of 2 in canvas" after the
chord. The fix is the smallest true one: `CanvasOutlineRowViewModel.Status`
notifies, `RefreshStatus(marked, filtered)` recomposes it from the same
core row through `CanvasPhrase.RowStatus`, and the view's
`OnSelectionChanged` answers `Marked` with `RefreshMarks()` — every
node row re-read from the mirror the apply just seeded, in place, no
rebuild, so the row a reader is standing on keeps its identity and
its focus while its status gains or loses ", marked". A rebuild would
have been the easy shape and the wrong one: it tears every row down
under the reader's focus for a change that moved no row.

Fact: one — the same row instance's status gains ", marked" on the
toggle and loses it on the next, identity asserted. Mutation M1, the
`Marked` branch removed: bitten, byte-restored. The seven canvas
journeys run green together with the screen reader stopped.

The ledger sweep: every IG-31..IG-56 row is named in a TG record
(the sweep script printed an empty "never named" set). Owed and
recorded rather than shipped: the conflict-resolution verbs and their
continuation matrix (IG-53's resolution rows), `WithPresented()`'s
re-present (no caller), `DisplacedBeforeApply`'s sentence (unpinned
by design — silent), and the mac-lane divergences GD-1..GD-7.

### §G close-out — the marks shipped, the record closed

Eight tasks, eight commits, the ledger swept. What §G leaves behind:
one mark authority — the publication's `MarkedIntent`, with
`CanvasSelection.Marked` as its applied projection seeded at the top
of every apply and echoed back only for the two one-way seeds; the
three document verbs (`ToggleMark`, `Unmark`, `ClearMarks`) publishing
through one `PublishMarks`; per-id mark EPOCHS stamped by
`CanvasModelCopy.StampMarks` so a bulk verb's `RemoveCaptured` drops
exactly the marks it captured and never a mark re-placed since; the
mark effect resolved inside the operation's own republish, once; the
funnel's outcomes split honestly (`DisplacedBeforeApply` silent,
`ApplyRefused` spoken, the unpresented receipt kept); the prompt
hierarchy replacing the prompt kind, each variant owning its submit;
the marks list as a sheet whose Jump seats silently, clears the
filter, and lands reader focus through the owner; Delete, Color and
Group Marked through one `SubmitBulkMarked` frame with core's count
noun; the chords and palette rows registered on both tables; and the
journey driving every leg with a real keyboard.

The section's discoveries, honestly counted: the mirror-only write
healed by the seed arm (TG-0, which is why the mutation had to aim at
the apply side); the prompt kind that could not carry the variants'
submits (TG-1, the hierarchy); the Jump that had to clear the filter
before it could land (TG-2); the resolution verbs that do not exist —
Terminal has no writer, `WithPresented()` has no caller — found while
splitting the outcomes (TG-3); the group frame that is null for an
empty set and the verb that must say NoMarks rather than throw
(TG-5); the marks that are NOT history, standing across an undo
(TG-6); the outline's ItemStatus that never gained ", marked" because
a mark toggle republishes no row (TG-7, the journey's catch —
invisible to every fact that read the mirror instead of the row).
Owed to the mac lane, restated from the freeze: GD-1 through GD-7,
the Windows-only color verb among them. Owed here, recorded: the
conflict-resolution verbs and their continuation matrix (IG-53's
resolution rows) await a recovery flow with a writer;
`WithPresented()`'s re-present awaits its caller; the
`DisplacedBeforeApply` sentence stays unpinned because the arm is
silent by design.

### CI round 1 — the first canvas open on a loaded runner

The marks journey passed locally and failed on the runner at its first
canvas wait — "CanvasOutlineTree did not become available" twenty
seconds after New Canvas, before a single mark was placed — while the
modes journey, which opens a canvas by the same lines, passed in the
same run. The TRX said why the two differed: the runner's order put
the marks journey five milliseconds after the visual-board journey,
whose teardown kills its app without waiting for exit, so the marks
journey's first canvas open — the slowest step of a fresh process —
ran under that teardown; the modes journey ran before it. The
persisted-surface theory was checked and closed: the surface token
lives in the vault's own `workspace.json`, and every journey opens a
fresh vault.

Two changes, both in the journey: the first canvas wait is sixty
seconds and carries the app log tail on a miss, so the next miss is
diagnosable rather than a bare timeout; and the journey's Up and
Enter go through `PressKey` (type and settle) instead of
`Keyboard.Press`, which holds the key down. Validated locally alone
and in the runner's order (the visual-board journey, then the marks
journey), both green.

The re-run of the same head got past every marks leg and lost the
last: "Ctrl+Z never restored both cards". Ctrl+Z is two chords split
by a focus gate — structural undo while the files tree holds keyboard
focus, canvas undo under the canvas — and Delete Marked destroys the
row the palette's close would restore focus to, so on a slower runner
the chord could land in the files tree and undo nothing the canvas
could see. The journey now seats focus on the (empty) outline tree
before the chord, as the modes journey seats a row before its own,
and says the row count and the app log tail on a miss. Validated
locally alone and in the runner's order (table, visual, then marks).

## §W-G canonical-consumption audit (seeded from the spec §2 table; closed in PR H)

Tier 1 and 2 move to core with the mac consuming the new API in the same
PR and the Swift derivation deleted (decision 5). Tier 3 is
host-by-designation — recorded so the audit is explicit, not silent. PR
H re-greps `apps/slate-windows/src/SlateWindows/Canvas/` against this
table.

| # | Swift-derived pocket (file:line) | Core today | Tier | Target (PR) | State |
|---|---|---|---|---|---|
| A | The entire announcer grammar: verbosity matrix, group entered/left, connection traversed (direction phrases, duplicated again in `CanvasOutlineView.swift:335–347`), confirmations, destructive "— ⌘Z to undo", error, filter count, mode entered/cancelled/committed, undid/redid, Where-am-I readback (`CanvasAnnouncer.swift:104–213`, `AppState+CanvasActions.swift:328–819` prose sites, `AppState+CanvasConnect.swift:62,127–181`); preset-name dictionary duplicated (`AppState+CanvasActions.swift:340,766`) | `HostComposed` residue; core supplies every payload (`CardSummary`, `Neighbor.direction`, `RelativeDesc`, `color_name`) | **1** | `A11yEvent::Canvas*` family + `CanvasVerbosity` (0a) | **closed** — core half + mac consumption both landed (0a-1, 0a-2) |
| B | Relative-position description in move mode — nearest neighbours by squared centre distance, `Below "X", right of "Y"` (`AppState+CanvasModes.swift:299–339`) | `RelativeDesc` exists for placement only | 2 | `canvas_describe_relative(h, rect, exclude) -> Vec<CanvasRelativeDesc>` (0b) | **closed** — 0b-7; mac's nearest-neighbour walk deleted (0b-2), CD-19 |
| C | Auto-side selection for new connections, **two copies** (`AppState+CanvasConnect.swift:24–33`, `CanvasRendererView.swift:582–600`) | none | 2 | `canvas_auto_sides` (0b) | **closed** — 0b-3, rect-keyed (CD-16); BOTH Swift copies deleted (0b-2) |
| D | Containment / parent-group resolution, **three copies** (`AppState+CanvasCreate.swift:296–305`, `AppState+CanvasExtras.swift:131–147`, `AppState+CanvasActions.swift:439–441`) | `GroupTree` exists, not exposed | 2 | `canvas_parent_of`, `canvas_children_of` (0b) | **closed** — 0b-8; all THREE Swift copies deleted (0b-2 — the title-keyed one with `place_inside_group`), CD-18, CD-27. CD-4 no longer needs `children_of` (0a-2 deleted the walk) |
| E | Enter/exit group + group card count from outline `depth` walks; trace-path walk (`AppState+CanvasNavigation.swift:55–90,129–156,170–181`) | `reading_order`/`adjacency` exist | 2 | D's queries + `canvas_trace_path` (0b) | **closed** — 0b-8, 0b-9; the depth walks and the trace walk deleted (0b-2) |
| F | Selection model + reading-order re-projection of the marked set | none (correct) | 3 / 2 | host state; `canvas_order_nodes` (0b) | **closed** — 0b-10; both re-projections deleted (0b-2). Selection model stays host state |
| G | Undo/redo stacks, depth/session policy, menu-title composition (`AppState.swift:3987`) | `apply()` returns inverse + names | 3 | host stack; **menu title** = `CanvasUndoMenuTitle{verb,name}` | **menu title landed (0a)** |
| H | Placement math leaks; `MIN_CARD_SIZE` only in Swift | constants in `placement.rs` | 2 | `canvas_constants()`, `canvas_group_rect_around`, `canvas_place_inside_group`, `canvas_bounds` (0b) | **closed** — 0b-4, 0b-11, 0b-12; mirrored constants, the fit re-union, the bbox fold and the move-into-group math deleted (0b-2), CD-21/CD-24. `MIN_CARD_SIZE`'s reject-not-clamp ENFORCEMENT stays host-side |
| I | Viewport math — clamp 0.1–4.0, step 1.25, fit padding 40/120 | none | 3 | host rendering; constants pinned here; zoom % announced via `CanvasZoom` | **event landed (0a)** |
| J | Table column order/sort comparators/summary sentence; outline interleave | rows from core | 3 | host projection config; summary sentence stays a **static label** (never announced on mac) | resolved as label class (0a-13); **the Windows half landed in PR B** — B2 (columns), B3 (comparators, incl. the correction to the spec's Color description) and B9 (the summary label) are the projection config this row designates |
| K | Filter predicate — title/kind/groupPath/target, case-insensitive contains | none | 2 | `canvas_filter` (0b) | **closed** — 0b-13, 0b-14; `matchesFilter` deleted (0b-2), CD-22. `filterActive` stays host UI state |
| L | Speakable-name dedup vs core's untitled-only allocation — two uniqueness algorithms | partial, conflicting | 2 | one algorithm in core: `CardSummary.speakable_name` (0b, D-3) | **closed** — 0b-5, 0b-6, CD-20, CD-23; the renderer's used-set walk and its per-view sticky map deleted (0b-2). **PR A note:** CD-23's "which surface reads which field" answer now differs by platform — the Windows OUTLINE reads `speakable_name` where mac's reads `title` (CD-30), so row P's two copies are not byte-identical on a canvas with repeated titles |
| M | Node/edge id minting | none | 2 | `canvas_new_id()` (0b) | **closed** — 0b-4; `newCanvasEntityID` deleted, nine call sites (0b-2) |
| N | Overlap onset/offset transition tracking | query exposed | 3 | host state machine (two-state, pinned); the CLAUSE is core's (`CanvasOverlapTransition`) | **clause landed (0a)** |
| O | Resize → Fit to Content text-metrics approximation | none | 3 (D-5) | host, identical placeholder formula both hosts; the LABEL is core's (`CanvasResizePreset::FitToContent`) | **label landed (0a)** |
| P | **The outline row's card reference** — `⟨Kind⟩ card "⟨name⟩"` / `Group "⟨label⟩"`, composed host-side by `CanvasPhrase.CardReference` (Windows) and `CanvasCardRef.phrase` (mac) | **`a11y.rs::card_ref` composes the identical clause**, but only INSIDE templates — no exported accessor renders a bare card reference (0a-10) | 3 by designation (§W-C label class, 0a-13) | host, on both platforms, until an owner designates a label accessor | **open by designation** — the two copies are pinned against core's own render by `TheCardReferenceMatchesCoresOwnComposition`, so a core wording change fails Windows CI rather than drifting. CD-30 records the `speakable_name` vs `title` difference; CD-34 the capitalisation residue |
| Q | **The outline row's positional status** — `⟨n⟩ of ⟨m⟩ in ⟨container‖canvas⟩[, ⟨colour⟩][, marked]`, composed by `CanvasPhrase.RowStatus` (Windows) and `nodeValue` (mac) | **the same clause is the tail of `CanvasMovedTo`** at Standard and Verbose; again template-internal, with no accessor | 3 by designation (§W-C label class, 0a-13 names the outline node value explicitly) | host, both platforms | **open by designation** — same pin: the Standard render of `CanvasMovedTo` is asserted to equal `CardReference + ", " + RowStatus`, word for word |

---

## Vocabulary additions (new copy, not migrated strings)

The `CD-n` register below records where core DIVERGES from a shipped mac
string. This section is the other thing: sentences this programme
**adds**, which no mac string corresponds to. Kept separate on purpose —
calling an addition a divergence would imply a mac behaviour to compare
it against, and a reviewer checking parity would go looking for one.

**VA-1 — `CanvasStatusNote::Reopening`** (W6-1 0b-2 fix round 2).
*"This canvas is reopening. Try again in a moment."* — Medium, status
family, uncoalesced like every other `CanvasStatus`.

Spoken when a structural READ verb is used during
`beginBatchRetarget`'s window: the canvas is `.ready` and its snapshot
is visible, but the path-bound handle is detached until the background
reopen lands, so the queries those verbs delegate to have no handle to
answer from.

**VA-1's members** — written here for a reader; the list a REVIEWER
should trust is the one `every_mac_canvas_read_is_gated_or_named`
derives from the Swift source (see VA-2's section). Handwritten
membership is what rule 4 was invoked over, and this table is now a
description of the derivation rather than the authority:

| Member | Site | What it would otherwise do |
|---|---|---|
| Enter group | `canvasEnterGroup` | silence |
| Exit group | `canvasExitGroup` | silence |
| Trace path | `canvasTracePath` | silence |
| Fit canvas | `canvasFitCanvas` | silence |
| The filter family | `filterView` / `canvasAnnounceFilterCount` / the summary label | announce a count for rows that answer a different needle |
| **Where am I** | `canvasWhereAmI` | silence — and it is the PULL surface, so silence is the one failure t0 §1.4 exists to prevent |
| **Follow connection** | `canvasFollowConnection` | speak `No connection…`, a fact the adjacency list never returned, when the cache is cold and no handle can fill it — **and, in the other direction, refuse with VA-1 when the cache CAN answer.** Its order is fixed: (1) the selection precondition, via the precedence gate; (2) `neighborsIfKnown` — a non-nil list answers normally, traversal or accurate dead end, whatever the state; (3) the mapping's refusal, and only when (2) came back nil |

The last two were present at BASE and PR 0b did not change either site;
they are in VA-1 because the sentence is the right answer for the state,
not because 0b broke them. Where-am-I needed its `.ready` check moved
ahead of its handle check — reversed, it also made the `.notReadable`
arm below it unreachable, so a canvas that never opened cleanly said
nothing at all. Follow-connection needed adjacency to distinguish "no
connections" from "no answer": `neighborsIfKnown` returns `nil` for the
second, and only a real list may produce the dead-end phrase.

**The order took a CI run to become executable** (PR #1155). The
context-as-proof rewiring put the mapping first, so a WARM cache lost
to `.reopening` — the accurate phrase refused in favour of a refusal,
which is the rule backwards. Asking the data first is not a second
state reader: the consult reads no state, and it cannot escalate a
refusal into a live read, because `neighborsIfKnown`'s query arm needs
a handle and every non-`.ready` state has already dropped it. It is the
same shape the filter family has had since VA-1 — `FilterView.current`
first, mapping only when the needle went unanswered — and those two are
the only members with a cache-answerable path.

**The invariant that makes it safe: a read cache never outlives the
rows it describes.** `neighborsCache` and `filterMatchCache` were
already emptied by every reload and by the successful retarget; PR 0b
adds the two transitions that BLANK the surface without reloading —
`beginPreparedReplacement` (the container shows a spinner) and
`markMovedToTrash` (it shows a message). Both cost nothing, since every
path out of those states clears the caches again, and both close the
gap where a stale answer could speak for rows the user cannot see. What
remains warm is exactly `.ready` and `.retargetFailed` — the two states
whose retained rows the container renders, per the matrix in VA-2's
section.

**Why an addition rather than silence.** The verbs answered from
Swift-side outline walks before PR 0b, so the window used to produce an
answer; consuming core's queries made it produce nothing. t0's
never-silent principle governs: a keypress that does nothing must say
so. The window is transient but genuinely user-reachable — the canvas
commands carry no enablement predicate (rule R1: commands are always
reachable), and a batch rename's background reopens are not
instantaneous.

**Why not the existing sentence.** `CanvasMutationRefusal::Reopening`
covers the same window for WRITES and keeps its
*"Wait for it to finish before making changes."* tail. Reusing it here
would tell a user who pressed a navigation key and changed nothing to
wait before making changes — the wrong sentence for the trigger. The two
now differ exactly where the user's intent differs: the write refusal
names the changes, the read refusal names the retry.

**It also covers the FILTER family.** `canvas_filter` is a structural
query like the other four, and the filter has a second failure mode the
navigation verbs do not: a stale answer that still looks like an answer.
The rule is *never a wrong number*:

- an ACTIVE filter whose needle is UNCHANGED keeps serving the memoized
  match set — those ids are still correct, so nothing is announced
  differently;
- a needle CHANGE that no handle can answer does **not** apply. The
  previous rows stay on screen and VA-1 is announced instead of a count.
  Widening silently back to the full outline — which is what a naive
  "no answer ⇒ no filter" fallback does — would show every card while
  the field still claims to be filtering, and then speak that number as
  a match count;
- the count the host announces is read from the view the surfaces are
  DISPLAYING, never recomputed, so *displayed rows == announced count*
  holds by construction. `CanvasDocument.FilterView` is that one value,
  and the outline rows, the table rows, the FILTER summary label, the
  count announcement and Where-am-I's matched count all read it. (The
  table's OTHER summary — the grid's `Canvas table: N cards, M groups.`
  composition label — counts unfiltered rows and always has. It is a
  never-announced label describing the canvas, not a match count, so it
  is outside this invariant; named here because "the table reads it"
  was too broad to be checkable.);
- `NoCardsMatchFilter` is unaffected outside the window.

**And it sets the rule for THROW arms.** A structural query can fail two
ways, and they get different sentences because they are different facts:

| Failure | Sentence | Why |
|---|---|---|
| No handle (the reopening window) | VA-1 | the query never ran |
| The query THREW with a live handle | `Nothing selected.` | `bad_node` — the selection does not name a card this canvas can answer for (0b-6's row/model skew) |
| The query SUCCEEDED and came back empty | the verb's own phrase — `Group "X" is empty.`, `At canvas level.`, `No outgoing path from "X".` | that fact was actually learned |

Silence is not on the list. The middle row is the one that was wrong
before: a throw fell into the verb's empty-answer branch, so a card core
could not resolve was reported as an empty group, or as being at canvas
level, or (trace path) as nothing at all. Announcing a verb-specific
phrase for a query that never answered asserts something no query
returned.

**Two sites are outside the table by decision, both recorded rather than
left to be rediscovered.**

- `canvasFitCanvas` swallows a `bad_handle` throw silently
  (`(try? canvas_bounds) ?? nil`, then return). Its `nil` is dominated
  by the EMPTY canvas, which was silent before PR 0b too
  (`guard !doc.scene.nodes.isEmpty`), and `canvas_bounds` has no
  `bad_node` path — its only error is `bad_handle`, which needs a
  stale-handle race to reach. Distinguishing the two would mean a
  `do`/`catch` for a state the public surface cannot construct; the
  no-handle case is already VA-1 via the guard above it.
- `canvasMoveOutOfGroup` collapses a `canvas_parent_of` throw into
  `NotInAGroup` through `try?` — the shape the middle row outlaws. It is
  unreachable today: the node must be in `doc.scene`, which derives from
  the same model the query reads, so `bad_node` cannot fire, and the
  verb is a write behind `admitCanvasMutation`. **Flagged for PR E/F:
  do not copy the collapse.** A Windows re-implementation whose scene
  and query can come from different snapshots makes it live.

**A behaviour change this rule carries, recorded so it is not read as
incidental:** enter-group, exit-group and trace-path now announce
`Nothing selected.` where BASE returned SILENTLY — for a missing
selection as well as an unresolvable one. At BASE all three were silent
and no test covered it; ~8 sibling verbs (`canvasEnterMoveMode`,
`canvasEditCard`, `canvasRemoveFromGroup`, …) already announce exactly
that string in exactly that situation, and the never-silent principle
above decides the tie. It needs no vocabulary work — the arm was already
in the corpus.

**Lockstep.** Five places moved together: the `CanvasStatusNote` arm and
its render, the corpus entry (**appended**, so no pre-existing corpus
index moves — contract 0a-2), the in-file golden table, the regenerated
`corpus.json`, and both host censuses. The §W-D artifact diff is **five
lines, purely additive**: one new entry, no existing entry's identity,
priority or text touched. The uniffi mirror gained the arm; the
coalescing class list did not change, because `CanvasStatus` is
"everything else, posts immediately".

**VA-2 — `CanvasStatusNote::Loading`** (W6-1 0b-2, codex round 1).
*"This canvas is loading. Try again in a moment."* — Medium, status
family, uncoalesced. VA-1's sibling, same membership, different state.

`LoadState` is `.loading`, `.ready`, `.degraded`, `.failed` and
`.retargetFailed`; only `.ready` can answer a query, and VA-1 answered
for that one alone.
`.loading` is reachable two ways: a first open, and a prepared
replacement installed over an already-open tab
(`beginPreparedReplacement`), where the tab and its document object
survive while the state flips. (An earlier draft of this sentence said
the previous SNAPSHOT stays on screen; the container renders a
`ProgressView` for `.loading`, so it does not — the round-5 matrix in
this section is the authority, and this was the same kind of
hand-written claim about the view that round 5 was called on.)
**VA-1's copy would be false there** — a
first open is not a REopen — so the state gets its own sentence rather
than a stretched one. That is the same decision VA-1 made against
`CanvasMutationRefusal::Reopening`, applied one level down.

`CanvasMutationRefusal::Opening` covers `.loading` for WRITES and keeps
its *"Wait for it to finish before making changes."* tail, untouched.

**One mapping, and membership is DERIVED — red-team rule 4.**

Three consecutive review rounds found the same class of defect in this
subsystem: `.loading` bypassing VA-1, then the state census drifting,
then the merged test's heterogeneous expectations plus a filter path
that announced `.reopening` in every non-ready state. The class was
never the individual sentence — it was **VA membership and per-state
responses written by hand in four places**. Rule 4 says stop patching
and implement the invariant.

**`AppState.canvasReadRefusal(for:)` is now the single state → response
authority**, total over `LoadState` with no `default`, so a new case
fails to compile there rather than falling into somebody's silent arm:

| State | Answer |
|---|---|
| `.ready`, handle live | proceed |
| `.ready`, handle detached | `.reopening` (VA-1) |
| `.loading` | `.loading` (VA-2) |
| `.degraded`, `.failed`, `.retargetFailed` | `.notReadable` |

Every member routes through it — `canvasReadContext(for:)` announces
and returns `nil`, or hands back a `CanvasReadContext` whose existence
IS the proof the mapping said yes, so no verb re-derives session and
handle.

**Discovery is separated from admission**, because admission announces.
A verb resolves its document with `activeCanvasDocumentAnyState`,
answers its OWN preconditions, and only then asks the mapping —
otherwise the eager announcement inverts the recorded precedence and a
no-selection press in the reopening window says "reopening" where m6
says "Nothing selected." That precedence is itself one function,
`canvasAnsweredMissingSelection`, and **what it asks is a question about
the VIEW**: `LoadState.rendersRetainedSnapshot` — does
`CanvasContainerView` put this state's retained rows on screen? A
selection question is meaningful exactly where the user can see rows to
have a caret in; everywhere else the mapping's sentence is the only
honest answer. The predicate was first written as `state == .ready`,
which was wrong: `.retargetFailed` renders its snapshot read-only, so a
no-selection press there said "cannot be read" where the selection-first
rule requires "Nothing selected." (codex 0b round 5). The per-state
truth is derived from the container's own switch, not from the state's
name:

| State | Container arm | Renders retained rows | No-selection press says |
|---|---|---|---|
| `.ready` | `readyBody` → `canvasBody` | yes | "Nothing selected." |
| `.retargetFailed` | `retargetFailureSnapshot` → banner + `canvasBody(readOnly: true)` | yes | "Nothing selected." |
| `.loading` | `ProgressView` | no | `.loading` |
| `.degraded` | `degradedState` → `stateMessage` | no | `.notReadable` |
| `.failed` | `stateMessage` | no | `.notReadable` |

`.failed` is the case worth stating plainly, because the earlier
analysis stopped one step short: a trashed canvas KEEPS its outline
(`markMovedToTrash` does not blank it), yet the view renders only the
message, so there is nothing on screen for a selection question to be
about and `.notReadable` is right. Data survival is not visibility. The
mapping's `.notReadable` arm therefore stands unchanged for every
predicate-false state; only the precedence moved.

Two guards hold this down, both mutation-verified in both directions.
`the_snapshot_visibility_predicate_matches_the_container_switch` parses
the container's `switch document.state` arms, computes transitive
reachability to `canvasBody` within the file, and fails if the predicate
and the view disagree — the VIEW is the authority, so changing what a
state renders forces the predicate to follow. And
`every_mac_canvas_read_is_gated_or_named` gained a second assertion: a
verb whose body can announce `.nothingSelected` must reach it through
`canvasAnsweredMissingSelection`, so "the selection-bearing verbs" is a
derived set rather than a list in a test. Where-am-I and fit canvas take admission
first: neither has a precondition that outranks the state (Where-am-I's
"nothing selected" falls back to the first row by design). The last row is Where-am-I's old per-state answer becoming
everyone's; it also closes the announcement half of the t0 §5 gap filed
in "Mac details recorded while reading" (those canvases now SAY they
cannot be read — they are still not navigable, which is what remains
filed).

**The filter family routes through it too.** `canvasAnnounceFilterCount`
and the summary label used to hardcode `.reopening` when the needle went
unanswered, which was simply false in `.loading` and in the three
unreadable states. They ask the mapping now. The one arm the mapping
cannot speak to — a state that CAN answer, meaning the handle went
stale inside the call — keeps `.reopening` as the honest "not now", and
says so at the site.

**Membership is enforced, not curated.**
`every_mac_canvas_read_is_gated_or_named` (`slate-uniffi`, sibling to
the coalescing tripwire) scans the mac tree for calls to the
handle-based canvas read queries and asserts each calling function
either mentions `canvasReadTarget` / `canvasReadContext` /
`canvasReadRefusal`, or appears on a NAMED exclusion list with a stated
reason. Adding an ungated read verb fails the test and names the
function — mutation-verified. Two properties make it non-vacuous: it
asserts it found call sites at all, and it asserts every exclusion still
calls a scanned query, so a stale excuse cannot sit there hiding the
next one.

It binds SOURCE and TEST membership, not just source (codex 0b rounds
6–7): the same test parses the two-column test's `selectionBearing` /
`selectionFree` arrays and requires `selectionBearing` to equal the
source-derived set of selection-first verbs in both directions, so the
matrix cannot lose a row or miss a newly gated verb. The anchor symbol
is `canvasAnsweredMissingSelection` — a verb is selection-first iff it
CALLS THE GATE, because the gate is what announces; deriving from
`canvasAnnounceSelectionUnresolvable` mentions instead described only
the verbs with their own throw arms and let a verb adopting the gate
alone change no compared set. No gate caller may sit outside both
arrays. The parse anchors on the two binding declarations and fails
loudly if they move.

**Its reach, stated rather than implied** (the doctrine 0a's source scan
settled on). It matches the query as a MEMBER TOKEN on a session-ish
receiver, with no call paren required — `let read = session.canvasBounds`
then `try? read(handle)` is a use, and demanding the paren let exactly
that shape past until codex round 4 found it. The receiver is the
discriminator because these are `VaultSession` methods, which is also
what keeps the navigator's own same-named verb out of the results. What
it CANNOT follow is indirect flow: a session passed into a helper that
calls the query, a method reference stored in a property and invoked
elsewhere, or a closure capturing either. Those need symbol resolution
across functions — a real front end, which this scan is not and should
not become. The bound is the usual one: it can miss a NEW evasion, it
cannot go quietly green on the code it does see.

The exclusions, each with its reason: `filterView` and
`neighborsIfKnown` (the document's data layer RETURNS un-answerability
instead of announcing it, so their callers can route through the
mapping); and `canvasRemoveFromGroup`, `canvasGroupMarked`,
`canvasDuplicate`, `canvasInReadingOrder`, `canvasRelativeDescription`
(write paths behind `admitCanvasMutation`, a different ladder with its
own spoken refusals — routing them here would double-announce).

`canvasSelectAdjacent` is deliberately NOT an exclusion. It reaches
`canvas_filter` only through `filteredOutline`, so the scan never sees
it, and `FilterView.current` already separates a live answer from a
retained one — which is exactly what keeps arrow movement working on
the displayed rows in the reopening window, as VA-1's tests require.
Decided by reading the path, recorded here rather than left implicit.

`canvasAutoSides` is outside the scanned set: it takes no handle and
cannot fail, so there is no state for a mapping to answer about.

Same five-place lockstep as VA-1, and the §W-D artifact diff is again
**five lines, purely additive**.

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

**This closes t0 §5's ANNOUNCEMENT for a degraded load, not its
navigability.** A degraded canvas is still unreachable by every
navigator verb on mac — `activeCanvasDocument` gates on `.ready`, and
the degraded branch clears `outline` outright. That is a separate,
pre-existing gap no PR has owned; it is filed in "Mac details recorded
while reading" (Task 0b-2's entry) for close-out, so nobody reads CD-3
as evidence that §5 is satisfied end to end.

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

**CD-13 — `CanvasTracePathEnd` speaks the count of the titles it just
listed.** The trace sentence is ONE utterance —
`Path: A, then B. End of path — ⟨n⟩ cards visited.` — and mac took its
two halves from two different collections
(`AppState+CanvasNavigation.swift:144–155`): the list from `titles`
(`visited.compactMap { outline row → title }`) and the count from
`visited.count`. They differ whenever a visited node id has no outline
row, and the result is a sentence that contradicts itself. The event
therefore carries `titles` and NOTHING else, and the template speaks
`titles.len()`: the list and the number it claims cannot disagree,
because there is only one collection. This is the t0-correct reading —
t0 §1.1's rule is that the utterance describes what it names — and the
divergence is unreachable in practice anyway (the start id is the
selected row, and every other id comes from core's own adjacency over
the same model the outline is flattened from). Recorded rather than
"fixed to match mac" because the mac number, not the template, was the
defect. (Decided per the authority chain in Task 0a-2; the alternative —
adding a `visited` count parameter — would have preserved the ability to
disagree.)

**CD-14 — The outline's connection ROW now reads the traversal
sentence.** `CanvasOutlineView.swift:335–347` composed its own direction
phrase and printed the bare title, with `kind` hardcoded to `"text"`:
`Connects to "Ideas"`. It now renders the very
`CanvasConnectionTraversed` event the navigator speaks when it follows
that connection, so the row reads `Connects to Text card "Ideas"` — and
`Linked with Group "Q3"` where it used to mislabel a group as a text
card. The row's `Text` and its `accessibilityLabel` are the same string,
as before.

This is a **label-class text change**, the only one PR 0a makes, and it
is deliberate on two counts. (1) It is 0a-10's second deletion: a second
copy of a phrase table is the failure mode this contract exists to
prevent, and this copy had already drifted from the announcer's (no
card reference, no real kind, no `towardOther` flip). (2) t0 §3's
inspectability rule wants the pull-readable row and the spoken line to
agree; they did not. The cost is that the row is longer, and that a
sighted user reading the outline sees the kind word repeated from the
parent row — accepted, because a braille user reading only the
connection row previously could not tell what kind of card it pointed
at. No test pinned the old string. Cross-referenced from 0a-10's scope
table. (Promoted from a round-1 minor at the controller's direction —
it was recorded in prose but not in this register.)

**CD-15 — Four templates stop hardcoding the plural.** Migration turned
mac's cardinality assumptions into TYPE-level lies: the payloads admit
`count: 1` and an empty `Vec`, the arms tabulated below rendered
`cards` unconditionally, and all five lockstep places agreed with them because
the corpus only ever sampled a plural value. The arms are
`CanvasTracePathEnd`, `CanvasBulkMoved`, `CanvasBulkDuplicated`, and the
`CanvasModeObject::Cards` clause shared by `CanvasModeEntered` and
`CanvasModeCommitted`. They are total now (0a-14), which changes what is
spoken at exactly one shipped mac site and adds correct renderings for
states mac could not reach:

| Payload | Was | Now | Reachable on mac? |
|---|---|---|---|
| `CanvasBulkMoved { count: 1 }` | `Moved 1 cards below "X".` | `Moved 1 card below "X".` | **yes** — `canvasPlaceRelative`'s bulk branch takes one moving card when the scene-node lookup misses (`AppState+CanvasActions.swift:563–566`) |
| `CanvasBulkDuplicated { count: 1 }` | `Duplicated 1 cards — one undo restores.` | `Duplicated 1 card — …` | no — the `single` branch guards it |
| `CanvasModeEntered`/`Committed` + `Cards { count: 1 }` | `Move mode — 1 cards. …` / `Placed 1 cards.` | `… 1 card. …` / `Placed 1 card.` | no — mac's ternary picks `Card { title }` at one |
| `CanvasTracePathEnd { titles: [t] }` | `Path: t. End of path — 1 cards visited.` | `… 1 card visited.` | only through CD-13's title/visited skew |
| `CanvasTracePathEnd { titles: [] }` | `Path: . End of path — 0 cards visited.` | `End of path — 0 cards visited.` | no — the walk seeds with the selected card |

The grammar fix is t0 §1.3-conformant and the same class as CD-4, CD-5
and CD-7: the shipped mac string was the defect, not the template. The
empty trace path omits the `Path: …` clause rather than emitting it
empty, which is how every other optional clause in this vocabulary
behaves; it is pinned because the arm must be total, not because a host
produces it.

Mac's host-side mirrors of the same clause moved with it — the M3
inspectable value (`CanvasModeController`) and three undo-action names
(`AppState+CanvasModes`, `+CanvasActions`, `+CanvasExtras`) now use
`CountCopy`, so `move 1 cards` becomes `move 1 card` in the undo stack
and the Edit-menu title. No test pinned any `1 cards` string (checked
across the mac, Windows and Rust suites).

### PR 0b

**CD-16 — `canvas_auto_sides` takes rects, not node ids** (controller
ruling R-0b-1). §2 row C proposes `canvas_auto_sides(h, from, to)` keyed
by node ids, and that signature cannot serve one of its own three call
sites: `AppState+CanvasExtras.swift:61–65` (create-connected-card)
builds a **synthetic `CanvasSceneNode` that is not in the model yet** —
it computes the placement first, then asks for the sides. A rect-keyed
pure function serves all three, needs no handle, and lets the renderer
resolve one endpoint at a time (0b-3). The id-keyed convenience is not
added: a caller that has ids has the scene, and adding a second spelling
of the same rule is what §W-G exists to prevent.

**CD-17 — `canvas_constants()` and `canvas_new_id()` are free
functions** (R-0b-2). §PR 0b says "all `#[uniffi::export]` on
`VaultSession`". Both are pure, and making them methods would force a
caller to have an open canvas to read a constant — Windows' document VM
needs `min_card_size` before any canvas is open, to construct the mode
controller. They join `a11y_render` / `canvas_color_name` as free
exports (the `SlateUniffiMethods.*` shape the C# harness already uses).

**CD-18 — Equal-area containment ties resolve to the LATER document
order.** Core's `min_by(area_cmp).then_with(|| b.doc.cmp(&a.doc))`
(`model.rs`) keeps the later group; Swift's `min(by:)`
(`AppState+CanvasCreate.swift:296–305`) keeps the first. Observable only
for exactly-coincident group rects — the shape
`model_tests.rs:coincident_groups_are_acyclic_and_deterministic`
deliberately constructs — so migrating mac onto `canvas_parent_of`
silently changes its answer there. Core's rule wins: it is the one the
reading order and the acyclicity refinement already use, and mac's is
an accident of which overload was reached for.

**CD-19 — `describe_relative`'s tie-break is pinned where mac's was
undefined.** Swift's `sort(by:)` is introsort with no stability
guarantee, so two equidistant neighbours could order either way run to
run and mac could speak a different fix for the same geometry twice.
Core orders by `(squared distance, document index)`, with non-finite
distances last (`canvasSafeInt`'s precedent for hostile geometry). This
is a latent nondeterminism fixed, not a phrasing change: every fixture
mac's tests exercise has a unique nearest neighbour.

**CD-20 — `speakable_name` ordinals renumber on delete** (owner
decision D-3, settled here). Core's derivation is recomputed from
scratch on every `open_canvas` / `canvas_apply`, so deleting the first
of two same-titled cards renumbers the second. T R21's "ordinals hold
for the session" survives today only in `CanvasRendererView`'s
per-VIEW `assignedSpeakable` map — two panes on one canvas already hold
independent ordinals, so the stickiness was never a document- or
session-level property to preserve. D-3's first branch is therefore
taken: **deterministic by document order, identical on both platforms**,
and the renumbering is this divergence from R21. The alternative — a
session-scoped sticky map in `OpenCanvasState` — would make the answer
depend on the handle's history, which is exactly the property that makes
a §W-A golden impossible to write.

**CD-21 — `place_inside_group`'s fallback fires on SIZE, not on
childlessness.** Mac's fallback (`AppState+CanvasActions.swift:439–461`)
triggers when the group has **no children at all**, regardless of size,
and when the group *does* have a child it delegates to plain
`place_new`, which is not clipped to the group — a full group pushes the
card outside and containment silently un-parents it. The spec's rule
("falls back to `(x + 20, y + 40)` only when the group is smaller than
one slot") is better defined and wins, so **geometry outcomes for
empty-but-large groups differ from legacy mac**: mac drops the card at
the inset, core walks the group's interior lattice from that inset and
takes the first free slot (0b-12). Coordinates are never announced, so
no §W-D string moves; §W-A pins cross-host equality of the NEW rule.
The spec is also silent on the group that fits slots but is full, which
mac leaves undefined — 0b-12's third outcome (`Full`) closes it rather
than returning a point outside the group.

**CD-22 — Case handling and whitespace trimming are Rust's, not
Foundation's — and not case folding either.** `canvas_filter`
lowercases both sides with `str::to_lowercase` (Unicode
`Lowercase_Mapping`, locale-independent) where mac used
`localizedCaseInsensitiveContains` (current locale, no full mapping),
and trims with `str::trim` (all Unicode whitespace) where Swift trimmed
`.whitespaces` (newlines NOT trimmed).

The spec's §2 row K and §PR 0b both ask for "Unicode simple
case-folding". **What shipped is lowercasing, which is a third rule**,
and the difference is recorded rather than glossed: a case fold
collapses `ß` to `ss`, lowercasing leaves it alone, so `strasse` does
not match `Straße` here. Lowercasing was kept because it is what the
rest of this crate's case-insensitive matching already uses, and
switching one predicate to `to_lowercase`-plus-folding for one letter
would put a second case rule in core. All three rules agree across
ASCII and Latin-1, which is the whole fixture corpus; the recorded
differences are Turkish dotless `ı`, `İ` (U+0130, whose Rust lowering
is `i` + combining dot), `ß`, and a query carrying a newline. Each is
pinned by test rather than merely described (0b-14).

**CD-23 — `speakable_name` is exposed on four records; which surface
SPEAKS it stays the host's.** Mac's speakable names reach only the
renderer's AX peer names (`CanvasRendererView.swift:403,447`) — the
outline, table and Where-am-I all use raw `title`. Surfacing the field
on all four records is plumbing; switching the outline's or table's item
name from `title` to `speakable_name` would be a behaviour expansion
that moves `CanvasOutlineTests.testOutlineRowsCarryDerivedLabelsAndValues`.
Task 0b-2 therefore consumes the field at the renderer's peer names
only — the second surviving card-reference spelling 0a-10's scope table
left standing with §W-G row L as its owner — and leaves the other three
surfaces reading `title`. The field being available on all four is what
stops a later host from re-deriving it.

**Amended by PR A: the hosts no longer do the same thing here.** This
row originally closed "both hosts do the same thing, which is what
parity requires". CD-30 then took the Windows OUTLINE to
`speakable_name` on a controller ruling (t0 §4's Voice Control
uniqueness row, on Windows' only canvas surface in PR A), so the two
outlines differ on any canvas with repeated titles — mac's renderer
already spelled `speakable_name`, which makes mac's outline the odd one
out rather than Windows'. The sentence is corrected rather than deleted:
what it asserted was true when written and is the reason CD-30 had to be
a recorded divergence instead of a quiet change.

**CD-24 — `canvas_group_rect_around` returns `Option`.** Mac's bbox
fold aborts on `guard minX.isFinite` when no member resolves — a silent
no-op with no announcement at all. `None` is that outcome typed, so a
host can decide what to say instead of inheriting silence by accident.
The non-empty case is byte-identical arithmetic.

**CD-25 — the inside-group search is a column-major LATTICE, not a
ring.** Controller ruling R-0b (§PR 0b) said "ring search inside the
group rect with `place_new`'s preference order". A ring search is what
`place_new` does around an anchor: for ring *r*, try the four
directions at distance *r*. Clipped to a group and anchored at the
group's inset top-left, three of those four directions leave the frame
at every ring, so the ring degenerates to two arms and never reaches
the interior's diagonal slots — a two-column, two-row group would leave
its far corner unreachable. The implemented rule is a lattice over the
interior, visited column by column and each column top to bottom, which
preserves the part of the ruling that carries meaning — `Below` is
preferred to `RightOf`, `place_new`'s first two preferences — and drops
the part that does not survive clipping. Adopted by controller ruling
in fix round 1: one deterministic order, pinned by fixtures
(`inside_group_prefers_below_then_right`) and by the §W-A goldens, and
both hosts consume core rather than re-deriving it, so there is no
second implementation for it to disagree with. `Above` and `LeftOf`
remain unreachable by construction, which is what clipping to the group
means.

**CD-26 — `count_noun` is an FFI export, because CD-6's other half is a
host string.** CD-6 routes the canvas ANNOUNCEMENTS through core's
grouped `count_noun`. Three of the undo-stack action names pair with
announcements that group — bulk delete, bulk colour, group-marked — and
those names are SPOKEN, because `CanvasHistoryApplied.name` is a
payload rendered verbatim. So `Undid delete 1000 cards.` followed
`Deleted 1,000 cards.` from the same action.

Task 0b-2 first closed that with a four-line `CountCopy.countedGrouped`
mirroring `group_thousands` in Swift. **That was wrong and is
retracted**: a host re-implementation where a pure core function can be
called is precisely the failure §W-G exists to prevent, and the fact
that it was small and test-pinned did not make it a second definition
any less. `slate_core::sidebar_filter::count_noun` is now `pub` and
`slate-uniffi` exports it as a free function (`count_noun(count,
singular, plural)`), joining `canvas_constants` / `canvas_new_id` /
`canvas_color_name` in the handle-free family (CD-17). The Swift
mirror is deleted; the three names call the export.

`CountCopy` keeps its ungrouped `counted` — that divergence is
deliberate and documented for host copy with no core counterpart — and
its doc comment now names the export and says not to add a grouped
helper beside it. The mac host still owns the two-branch agreement
ternary (`noun`/`verb`), which carries no formatting to disagree about.

Nothing corpus-visible moved: the export renders no event, so no
corpus entry, golden, census or §W-A artifact changed — verified by
regenerating all 29 parity artifacts on the rebuilt library and
diffing them byte-for-byte against the committed goldens.

**CD-27 — Duplicate's group expansion answers from the tree, not from
"centre inside a picked group".** Mac's copy 2
(`AppState+CanvasExtras.swift:131–147`) asked, for every node, whether
its centre fell strictly inside ANY picked group, and included it if so.
That is the same set as "is a descendant of a picked group" only while
groups NEST. Concretely, with groups `A` (large) and `B` (small)
overlapping without either containing the other's centre, and card `c`
whose centre lies inside BOTH:

- core's `GroupTree` gives `c` exactly ONE parent — the smaller area,
  `B` — so `canvas_children_of(A)` does not contain `c`, and duplicating
  `A` alone copies the frame without `c`;
- mac's test included `c` when `A` was picked, so duplicating `A` alone
  copied `c` too — while the outline, derived from the same tree,
  showed `c` under `B`.

Core's answer wins per decision 14 (one derivation) and is the one the
user can already see: the outline, the group path, `depth`, and every
other containment surface are that tree. Mac's answer additionally
contradicted its own outline, which is the stronger argument.

**No mac test pinned the old answer** — `testDuplicateGroupExpandsToMembersAsOneAction`
uses one group with two plainly-nested children, where both rules agree
— so the migration moved no expectation. Cross-reference CD-18, which
records the OTHER disagreement between mac's containment copies and
core's tree (the equal-area tie-break); CD-18 is about which group
wins a tie, CD-27 about a node whose membership two rules answer
differently without any tie.

### PR A

**CD-28 — `CanvasOpenInfo.degraded` is the PARSE-ERROR state, not the
"unsupported items" banner.** The spec's PR A behavior row 2 reads
*"`degraded=true` ⇒ … banner 'N unsupported items are preserved in the
file but not shown'"*, which joins two facts core keeps apart.
`is_load_degraded` is `any(ParseFailed)` (`canvas/mod.rs:356`) and every
`ParseFailed` arm of `parse` returns `Canvas::default()`, so a degraded
open has zero skipped entries and zero nodes: its banner would say
"0 unsupported items are preserved" about a file that produced no rows
at all. t0 §5 introduces the sentence as *"Parse warnings (#359 tolerant
contract)"* — the warnings — and mac implements exactly that
(`preservedItemCount` counts `.skippedEntry`; the banner renders only in
`.ready`; `info.degraded` closes the handle and enters a read-only error
state). Windows takes mac's shape and names the two states apart:
`ParseError` for the flag, the A4 banner for the skipped count. The
spec sentence is the thing that is wrong here; t0 and the shipped mac
behaviour agree with each other and with core.

**CD-29 — The degraded announcement is once per DOCUMENT on Windows and
once per CONTAINER on mac.** Controller ruling, resolving the ledgered
m2 wording question in favour of the contract as written: CD-3 says
"once per open", and on Windows an open is the registry's 0→1
transition (A1), so the post lives on the document and two panes on one
canvas hear it once. Mac's one-shot is `@State` on
`CanvasContainerView` (`:470–487`), so a second container on the same
document announces again. The mac per-container behaviour is recorded
here as the platform note rather than changed: this issue does not
re-open a mac view's state ownership, and the difference is audible only
when a user splits a pane onto a canvas that has skipped entries.

**CD-30 — The outline row's Name spells `speakable_name`; mac's spells
`title`.** Mac's outline `accessibilityLabel` builds
`CanvasCardRef(kind:title:)` from the raw `title`
(`CanvasOutlineView.swift:220, 12–21`) and reserves `speakable_name` for
the renderer's peer names (CD-23). Windows spells `speakable_name` in
the outline too, on the controller ruling, because the outline is
Windows' primary and (in PR A) only canvas surface, and t0 §4's Voice
Control row asks for *"no duplicate speakable names per surface"* — two
cards both titled `Research` are two rows a dictated "click Research"
cannot disambiguate. 0b-5's algorithm returns `display_title` verbatim
whenever that spelling is free, so the two hosts differ only on the
canvases where mac's outline is ambiguous. Recorded as an upstream note
rather than a mac change: mac's renderer already spells it this way, so
the mac outline is the odd one out, and aligning it is a mac-side edit
this issue's Windows lane must not make.

**CD-31 — The surface view is a code-built `UserControl`, not a
`.xaml(.cs)` pair.** Spec §1's file layout names
`CanvasSurfaceView.xaml(.cs)`. The Windows shell has no XAML surface
view: `BaseSurfaceView.cs`, `DashboardSurfaceView.cs`,
`HistorySurfaceView.cs`, `SyncDiagnosticsSurfaceView.cs` and
`ReadingSurface.cs` are all code-built controls, and the project's only
XAML is the window, the app, the shared templates and the themes. A
XAML pair here would be the single one of its kind, and the brief's
"match the Workspace/Bases idioms" is the stronger instruction: the
sibling surfaces are the idiom. Every other name in the layout is
literal.

**CD-32 — A retarget re-keys the registry; it does not mutate the
document's path.** Mac's `CanvasDocument` retargets in place, keeps its
last published snapshot visible while the reopen runs, and can land in
`.retargetFailed` still showing rows. The Windows registry keys
documents by path and a document's `Path` is immutable — the Bases
precedent, whose round-2 blocker was exactly the alternative (a renamed
tab keeping a document that reopens the OLD path forever). So
`RetargetCanvasDocuments` shuts the old document down and attaches a
fresh one at the new path, carrying the previous `CanvasSelection`
(selected node and marks) across so a rename does not silently drop a
user's marks. What does NOT carry is the retained snapshot: a failed
reopen shows `RetargetAbsent` with the message, not the old rows. The
snapshot-retention machinery is 0b-2's `beginBatchRetarget` family and
the surfaces that consume it are PR C/E's; adopting it here would be
inventing PR C.

**CD-33 — The Windows outline NESTS; mac's is flat with indentation.**
Mac renders a `List` of lines and indents by `depth * spacing`
(`CanvasOutlineView.swift:207`), so a VoiceOver user hears reading order
with no structural nesting and no collapse. The spec's PR A behavior
row 3 asks Windows for a `TreeView` with `ExpandCollapse` on groups and
items "nested by `depth`", which is a real capability difference, not a
divergence in what is spoken: a depth-first walk of the Windows tree
visits exactly mac's line order, including A11's connection-rows-first
rule inside a selected group. Two consequences are recorded rather than
discovered: a selected LEAF card also becomes expandable, because its
connection rows are its children (the surface auto-expands it, so the
rows are never hidden behind a collapse the user did not ask for), and
a collapsed group hides its members from the tree walk, which is what a
tree is for and what mac's flat list cannot offer.

**CD-34 — `CanvasPhrase.CardReference` capitalises with .NET's SIMPLE
mapping where core uses Rust's FULL one.** Core's `capitalize_first`
(`a11y.rs:2841`) upper-cases the leading character through
`char::to_uppercase`, which is the full Unicode mapping — one scalar may
become several, so `ß` → `SS` and `ﬁ` → `FI`. .NET's
`ToUpperInvariant` is deliberately the simple 1:1 mapping and leaves
both alone. The two therefore disagree on any leading character whose
full uppercase is longer than itself.

**Unreachable, and checked rather than asserted.** The only argument is
core's own `kind_label`, a closed set of five ASCII words
(`text` · `file` · `image` · `link` · `group`,
`model.rs::kind_label` returning `&'static str`), so no input in the
system reaches the divergence.
`TheCardReferenceMatchesCoresOwnComposition` renders all five through
core and compares, which turns "unreachable" from a claim into a
per-kind check that fails the day a sixth kind arrives with a
non-ASCII initial.

**What the host actually does, since the first wording of this row said
otherwise.** It splits on the first UTF-16 unit, taking two when that
unit is a high surrogate — which keeps a surrogate PAIR intact but is
not the same thing as a text element: a base character followed by a
combining mark is one grapheme and this splits it, and so does an
emoji ZWJ sequence. Saying "first text element" was simply false, and
the same false sentence was in the code remark. Both are corrected.
Nothing depends on it — the argument is `kind_label`, five ASCII words
— and the per-kind test is what keeps that a check. Recorded rather
than worked around: emulating Rust's full mapping in C# means
hand-coding the special-casing table, which is a real second copy of a
Unicode rule for a case no caller can produce, and grapheme-correct
splitting would be a third.

**CD-35 — The canvas link card has no confirmation step, and neither
does the policy it reuses.** Spec §PR A behavior 5 says *"link ⇒
`Process.Start` URL with confirmation per the existing external-link
policy"*. The existing policy has no confirmation: the right-pane
panels and the citation popover both check the scheme allowlist and
launch, announcing `ExternalLinkOpened` or `ExternalLinkFailed`
afterwards — there is no prompt anywhere in it. Mac's canvas is the
same shape (`CanvasContainerView.swift:177–178,188` announces
`CanvasOpened` after the fact).

So "with confirmation per the existing policy" reads as *the
announcement IS the confirmation*, and that is what ships: the
allowlist refuses `file:`/`javascript:`/custom schemes with
`CanvasBlocked { NotAUrl }`, a successful launch says
`CanvasOpened { Browser }`, and a failed one says
`CanvasBlocked { LinkOpenFailed }`. Adding a modal prompt on the canvas
path alone would make the canvas the only surface in the shell that
asks before opening a link — a divergence from both the other Windows
surfaces and from mac, introduced by PR A, on the strength of one
ambiguous word. If an owner wants a confirmation, it belongs on
`ExternalLinkPolicy` for every surface at once, which is a decision, not
a canvas detail.

**CD-36 — The media activation hint is corrected on Windows; mac's is
stale.** The mac label inventory gives every kind an activation hint and
A10 takes them verbatim. Mac's image hint is *"Media cards open with
canvas actions, arriving in a later milestone slice."* — but mac's own
`activate` opens a non-Markdown target in its default app TODAY
(`CanvasContainerView.swift:181–187`), so the hint has been describing a
deferral that is not there. Windows does what the mac CODE does (M1),
and a HelpText contradicting its row's behaviour fails the one job the
§W-C label inventory has, so Windows spells it *"Opens the media file in
its default app."* Filed as a mac note rather than fixed there: this
issue's Windows lane does not edit mac copy, and the hint is the
divergence, not the behaviour.

**CD-37 — The empty canvas renders `CanvasStatus{Empty}`, not
`CanvasEmptyOnboarding`.** Spec behavior 2 asks for an onboarding region
"whose text leads with the New Card chord … until then the copy is the
palette sentence". The event cannot deliver that in PR A: its template
renders *"Press ⟨chord⟩ to create your first card."* unconditionally, so
whatever goes in that slot — the palette chord included — tells a
screen-reader user to press a key that creates nothing, and PR A ships
no create command. The t2 rule the spec cites in the same sentence
("don't advertise a command that doesn't exist yet") is the tie-breaker,
so the region renders the true sentence the vocabulary already has, and
PR E swaps `CanvasEmptyOnboarding` in with the real New Card chord.
No host prose either way: both are core renders.
**Ratified by controller ruling** (fix round 3): the core-rendered empty
copy beats the spec's interim palette sentence, and the deviation from
spec behavior 2's literal wording stands as recorded.

**CD-38 — Windows will not shell-execute a non-media file card; mac
will** (controller security ruling, fix round 3). PR A's media arm
(M1, CD-36) handed any non-Markdown in-vault target to
`Process.Start(UseShellExecute: true)`. On Windows that is
`ShellExecute`, which EXECUTES what it is given, and a canvas is
untrusted input — it arrives over sync, from a shared vault, from
Obsidian — so a `{"type":"file","file":"setup.exe"}` node ran on one
Enter. The default-app open is therefore gated to MEDIA by extension;
everything else is refused, audibly, and never launched.

**The gate's set is core's — copied at first because core did not
export it, ASKED FOR since §E TE-0.** `canvas::model::media_class`
(`model.rs:661`) is the same function whose answer becomes the `image`
kind label and the `Image:`/`Audio:`/`Video:` title prefixes; it was
private, so `CanvasMediaPolicy` transliterated it in ONE place,
including both of its edge rules (the BASENAME's real extension; a
dotfile like `.mov` is a hidden file, not a video). The staged export
landed with §E's first task: the transliterated set and its lowering
helper retired, the gate became one FFI call, and
`TheMediaGateIsCoresClassification` pins the set and both edges
THROUGH the export — audio and video thirds included.
The lowercasing is core's `to_ascii_lowercase`, hand-written rather than
.NET's `ToLowerInvariant`: the two differ outside ASCII (the Kelvin sign
lowers to `k`, `İ` to `i̇`) and every difference ADMITS something core
calls not-media, which is the wrong direction for a gate deciding what
reaches `ShellExecute`.

**Half of it was pinned against core even before the export.** Core
exported one of the classification's ANSWERS: `kind_label` returns
`"image"` exactly when `media_class` says Image (`model.rs:646`),
reaching the host as `CanvasOutlineRow.kind` — and the kind-label
detour fact (TheImageThirdOfTheGateAgreesWithCoresOwnKindLabel, while
it lived) asserted agreement in both directions for the image third.
The audio and video thirds had no exported answer to check against.

**Drift note (discharged in §E TE-0):** PR E was the first PR that
needed the classification for its own reasons (the spec's Add Media
row — "media kinds by extension set — core's `media_class` decides the
label"), so §E's TE-0 exported it, deleted the copy, and retired the
detour pin with it; every third is now pinned through the export
itself.

**Containment is by OS FILE IDENTITY, not path text (codex round 3 — the
class ended, not patched).** Three consecutive codex rounds found
containment defects, and codex named the class: *filesystem identity
reduced to path text*, where two normalization or case rules on the same
string disagree. Path text is retired as the decision substrate. The gate
resolves the target through an OPENED HANDLE and answers every
containment and every "unchanged since check" question with OS file
identity. Immune to case, trailing dots, per-directory case sensitivity,
SUBST, and which spelling reached it.
`FileIdentityIsStableAcrossSpellingsAndDistinctAcrossObjects` pins the
primitive (same object ⇒ equal, different objects ⇒ unequal).

**The identity is the 128-bit `FILE_ID_INFO`, and it is the ONLY identity
method (round 4 #2, completed by round 5).** `nFileIndex` from
`GetFileInformationByHandle` is documented as NOT unique on ReFS and its
ids are reused, so a 64-bit compare can call two different files the same
— a fail-OPEN in the very gate it anchors. The identity is
`GetFileInformationByHandleEx(FileIdInfo)` → `FILE_ID_INFO`: a 64-bit
`VolumeSerialNumber` plus a 128-bit `FileId`, stable and unique on NTFS
and ReFS alike.

**There is NO legacy fallback, and the round-4 record's claim that there
was a safe one is corrected.** Round 4 shipped a 64-bit
`BY_HANDLE_FILE_INFORMATION` arm and this document described it as a
capability selection confined to pre-Windows-8 hosts. That description was
WRONG, and the scoped re-review that endorsed it was wrong to: the arm was
per-CALL, not per-host. It triggered on ANY failure of the primary query —
a transient error, a handle race, an unusual filesystem — and silently
downgraded that individual read to the non-unique index. On ReFS that is a
fail-open that arrives precisely when something is already wrong, which is
strictly worse than no fallback at all. The arm, its P/Invoke and its
struct are DELETED (codex round 5, controller ruling: take the strongest
form). `IdentityOfHandle` is now `FileIdInfo` or `null`, and a failed
identity query REFUSES the media like every other failure in this gate.

**The real constraint is the FILESYSTEM, not the OS version — and it has
an availability cost, stated plainly.** An earlier draft of this row
justified the deletion by minimum OS (Windows 10 1607 postdates
`FileIdInfo`'s Windows 8). That argument is the wrong KIND: `FileIdInfo`
is not a function of OS version but of what the volume's filesystem
answers, and FAT32 and exFAT commonly fail it, as do some redirectors and
virtual filesystems. The correct statement:

- **Supported volumes for opening vault media are NTFS and ReFS.**
- **On a vault whose volume does not answer `FileIdInfo` — a FAT32/exFAT
  stick, some network or virtual mounts — every media open REFUSES,
  audibly.**
- This is a **known, recorded fail-CLOSED limitation**, and a real
  availability regression against the round-4 code, which would have
  opened those files through the legacy index.

It is accepted deliberately. The only alternative is deciding containment
on a weaker identity, which is precisely the fail-OPEN codex round 5
killed: a per-call downgrade fires exactly when something is already
wrong. Refusing to open a photo is recoverable; launching a file that
escaped the vault is not. The fallback's existence WAS the mixed-method
class, so the choice is refusal or a hole, and this gate refuses.

`IdentityIsThe128BitFileIdInfoNotThe64BitIndex` pins that the 128-bit
class succeeds on a live handle (mutation-verified against a wrong class
value). `IdentityQueryFailureRefusesRatherThanDowngrading` injects a
primary-query failure into the containment flow and pins that the identity
primitive, `ResolveInsideVault` and `OpenMediaInVault` ALL refuse, with
nothing handed to the shell — mutation-verified by reintroducing a
fallback arm. `CanvasMediaGateCensus.TheGateHasExactlyOneIdentityMethod`
pins the legacy symbols absent rather than dormant, two-sided against the
surviving `TryGetFileIdInfo`.

**Containment reaches the root by identity, with NO depth cap on the
lexical walk (round 4, fail-closed #3).** The resolved terminal path names
canonical ancestors; each is opened, its handle HELD (see the coherent
snapshot below), and its identity compared to the vault root's. The walk
is purely lexical — `ParentOf` strictly shortens and terminates at the
volume root — so a fixed-point/shortening guard suffices and it carries no
arbitrary iteration bound. An earlier `ResolveRounds=64` reparse-cycle
bound had been mis-applied to this walk, refusing valid in-vault media
more than 64 directories deep (a fail-CLOSED availability bug); it is
removed. `MediaSeventyDirectoriesDeepStillOpens` opens a media file 70
directories down (mutation-verified against reinstating a 64-iteration
cap), and `TheAncestorWalkCarriesNoDepthCap` pins the `ResolveRounds`
symbol gone from the gate. A case-sensitive-directory sibling —
`C:\work\VAULT` vs `C:\work\vault`, which an `OrdinalIgnoreCase` text
prefix falsely accepts when per-directory case sensitivity is on (codex
defect 3) — is a DIFFERENT object with a different identity and does not
match. That feature is non-default and needs admin to enable, so the
exploit itself is recorded as a manual residual; the SAME rule is pinned
reproducibly by
`IdentityContainmentAcceptsAJunctionRootedVaultAtextPrefixWouldReject`,
where a vault rooted at a junction is contained by identity and would be
REJECTED by a text prefix — mutation-verified against a text-prefix
containment.

**The extended (`\\?\`) form is kept end to end (launch-integrity).**
The handle-resolved path is verified and launched unchanged. The
extended prefix is exactly what stops `ShellExecute` renormalizing
`vault.\file` to `vault\file` — verifying one string and launching
another was a real bug. `ATrailingDotVaultComponentLaunchesTheVerifiedIdentity`
pins it and is mutation-verified against stripping the prefix.

**One coherent snapshot: capture is fused with containment (round 4,
fail-open #1).** The leaf is opened ONCE; its resolved terminal path AND
its identity come from THAT handle; and every ancestor up to the vault
root is opened and HELD simultaneously while its identity is compared —
one coherent view, not three independent opens. Revalidation re-checks the
identity captured from the containment handle. An earlier shape captured
the check identity by RE-OPENING the resolved path after containment,
which opened a second window: a swap between the containment open and that
re-open made the captured identity the OUTSIDE object, and revalidating
outside-against-outside passed — a fail-OPEN. Fusing capture into
containment closes that sub-window by construction. The property is pinned
STRUCTURALLY — `CanvasMediaGateCensus.TheSnapshotCapturesIdentityFromThe
HeldHandleNotAReopen` proves `ResolveContained` reads identity only off
held handles (`IdentityOfHandle`) and never re-opens by path
(`IdentityOf`), mutation-verified against reinstating the re-open — because
an unprivileged in-process race cannot drive a swap inside a single
method's handle-held region; the swap-during-capture E2E is a manual
residual.

**The TOCTOU window, narrowed by IDENTITY (B1).** Immediately before
launch the resolved path is re-opened and its identity compared to the
snapshot's, and the launch happens only if the identity is unchanged — an
OS "same file" guarantee, not a string compare, so it is immune to the
case/normalization tricks a text revalidation was not. A swap in the
window redirects the re-open to a different object whose identity differs,
and the launch refuses. `ASwapInTheTocTouWindowIsCaughtByRevalidation`
drives the swap through a test seam, mutation-verified. **The single
remaining residual** is now exactly the launch-time re-resolution: the
snapshot is coherent and the revalidation reads the containment handle's
own identity, so the ONLY window left is that `ShellExecute` re-opens the
verified path BY NAME and resolves it itself — a path-taking launcher
cannot be handed the verified handle, and closing this needs a
handle-based launcher (a verb invoked against the open handle), which
`ShellExecute` is not. Precondition: hostile in-vault write access — the
peer the gate defends against — against which the exploit is a
sub-millisecond race on an already-identity-checked path.

**Driveless folder-mounted volumes open their media (Major-4).**
`GetFinalPathNameByHandle` with the default DOS-name flag returns
`ERROR_PATH_NOT_FOUND` for a volume with no drive letter, so every target
under such a vault resolved null. A `VOLUME_NAME_GUID` fallback resolves
it. A driveless mount cannot be created unprivileged, so the end-to-end
is a manual residual; the fallback PRIMITIVE is pinned by
`TheVolumeGuidResolutionReturnsAWellFormedPath` so it is not dead code.

**Junctions, pinned.** `AJunctionInsideTheVaultPointingOutsideIsRefused`
builds the reviewer's construction (`mklink /J`, no elevation, a plain
`.png` leaf) and `ANestedJunctionChainStillResolvesOutsideTheVault` the
nested one; both resolve through the OS handle now.

**Hardlinks are NOT covered, and the earlier claim that they were is
withdrawn.** A hardlink is a second directory entry for the same file
data, not a reparse point: it has no other path to resolve to — the
in-vault name IS a real name for that file, and its identity is the
file's own. Bounded by what a hardlink can be: same volume only, never a
directory, and it must be created by something that already has write
access inside the vault. It cannot reach a file the vault's filesystem
cannot reach, and the extension gate still applies to the name opened.
Accepted residual.

**Two more residuals codex verified, recorded.** An alternate-data-stream
syntax leaf can satisfy the extension gate, but only in a narrower shape
than this row used to claim — and the correction matters because the old
wording described a parser that does not exist.

*What the parser actually does:* `IsOpenableMedia` takes everything after
the LAST `.` in the basename, colon included, and compares that whole
string to the media sets. It does not split on `:` and it does not read
"the part before the colon". Verified against the shipped code:

| leaf | gate | why |
|---|---|---|
| `photo.png:stream` | **refused** | extension parses as `png:stream` |
| `photo:stream` | refused | extension parses as `stream` |
| `photo:cover.png` | **accepted** | extension parses as `png` |
| `photo.png:stream.png` | **accepted** | extension parses as `png` |

So the example this row carried for three rounds (`photo.png:stream`) was
REJECTED all along — the claim erred in the safe direction, but it
described the wrong mechanism. The real shape is an ADS whose STREAM NAME
ends in a media extension (`photo:cover.png`). Codex found no
boundary-escape or execution path through it either way: the resolved
terminal identity is still the in-vault base file, and containment is
decided on that identity, not on the leaf's spelling. A policy residual,
not a hole.

UNC and the `\\?\` / `\\.\` device-namespace forms fail closed: the
volume-GUID/UNC resolution does not reach a file whose identity chain
lands under a local vault root. Noted so a later change does not
regress it.

**The manual/bounded residuals, gathered** (each pinned at the primitive
or structurally, E2E deferred because the feature needs privilege or a
race that cannot be driven unprivileged): per-directory case sensitivity
(defect 3, `fsutil`); driveless folder-mount (Major-4, admin); the ReFS
128-bit id (round 4 #2, needs an ReFS volume — the 128-bit primitive is
pinned, the ReFS collision is the deferred E2E); the swap-during-capture
race (round 4 #1, closed by construction and pinned structurally, its E2E
undrivable unprivileged); and the sub-millisecond launch-time re-resolution
gap (needs a handle-based launcher). None is a path-text defect; the class
codex named is closed. Round 4 found three fail modes in the identity/
snapshot logic (a check→capture window, a ReFS-unsafe 64-bit id, a
mis-applied depth cap) and round 5 found that round 4's own fix for the
second one still carried a per-call downgrade; all four are fixed.

**One recorded fail-CLOSED limitation, not a residual risk.** On a vault
volume whose filesystem does not answer `FileIdInfo` (FAT32, exFAT, some
redirectors and virtual filesystems), media open refuses audibly rather
than downgrading to a weaker identity — supported volumes are NTFS/ReFS.
Listed here because it is a real availability regression introduced by
round 5 and belongs where the other bounded statements live; it is the
deliberate price of closing the fail-open.

**The capability-fallback sweep (round 5 #4).** No primitive in this gate
weakens itself on failure. Identity is `FileIdInfo` or refusal — no second
method exists. The two surviving retry/alternate shapes are NOT downgrades:
the final-path buffer growth re-invokes the SAME method with a larger
buffer, and the `VOLUME_NAME_DOS` → `VOLUME_NAME_GUID` step is the same
`GetFinalPathNameByHandle` asked for a different SPELLING of the same
resolved object (identity is still read from the held handle, and
`TheVolumeGuidResolutionReturnsAWellFormedPath` pins the two spellings name
one identity). Everything else fails CLOSED: a mixed-class comparison would
differ in the high half; UNC and `\\.\`/`\\?\` device forms never reach an
identity chain under a local vault root; an unopenable ancestor refuses.

**Every failure mode is a refusal** — a NUL character, a reserved device
name, a path too long, a link cycle, a permission error — because an
exception escaping into the activation would abort it silently rather
than refuse it audibly; the whole closure is wrapped and any failure
answers "no". Pinned by
`AnInVaultSymlinkPointingOutsideTheVaultIsRefused` (which FAILS rather
than skips when the box cannot make symlinks, so the arm is never
silently unchecked) and `AMalformedMediaTargetIsRefusedRatherThanThrown`
over six hostile shapes, with `AMalformedMediaTargetRefusesAudibly` for
the never-silent half.

**Deliberately stricter than mac, because the threat models differ.**
Mac opens any non-Markdown target through `NSWorkspace`
(`CanvasContainerView.swift:181–187`), where Gatekeeper, quarantine and
notarization adjudicate an execution; `ShellExecute` adjudicates
nothing. Matching mac here would import a decision that only holds under
mac's protections. Mac's laxer arm goes on the upstream-notes list, not
fixed here.

**STOP point recorded: the vocabulary has no reason for this refusal.**
Nothing in `CanvasBlockedReason` or `CanvasStatusNote` says "this file
type is not openable from a canvas" — `CanvasFileNotFound` is false (the
file is present), `LinkOpenFailed` is false (it is not a link), and a
host-authored clause in `CanvasActionFailed`'s `detail` would be exactly
the prose 0a deleted. So the refusal rides
`CanvasActionFailed { CanvasAction, detail: target }` — High priority,
never silent, dynamic data only — rendering *"Canvas action failed:
setup.exe"*. That is accurate and uninformative, and it is the best the
shipped vocabulary can do. Adding the typed reason is a core change this
task may not make (the brief's hard rule), so it is flagged rather than
smuggled: **the vocabulary needs a `CanvasBlockedReason` arm for a
refused file-type open, and PR E or a 0a follow-up should add it.** The
SAFETY behaviour does not wait on that; only the sentence does.

**CD-39 — The canvas table's ordinal columns sort differently from
mac's on a mixed-normalization vault** (W6-1 PR B, red-team round 1
B-1). Mac's Type/Target/Color comparator is Swift's `<`, which orders by
Unicode **canonical equivalence** (the stdlib normalizes before
comparing); Windows transliterates it as `string.CompareOrdinal`, which
compares raw UTF-16 code units and normalizes nothing. The two therefore
disagree on any pair of `target`s differing in normalization form — an
NFD `Café.md` sorts BEFORE `Caff.md` on Windows and AFTER it on mac —
and on the supplementary-plane pairs where code-unit order and scalar
order differ. The first class is reachable with ordinary data: macOS
hands back decomposed filenames, so a Mac-authored canvas carries NFD
`file` targets and a synced vault brings them across byte-exact.

**The divergence is real, and in-repo evidence already depends on it:**
the Swift parity harness sorts with an explicit
`Array($0.utf16).lexicographicallyPrecedes(…)` at every site rather than
with `<`, precisely because Swift's native ordering would not match the
C# twin's `StringComparer.Ordinal`. That opt-out is this row's claim,
shipped and gating since W3.

**Recorded, not fixed, on three grounds.** Ordinal is deterministic and
locale-independent, which is what a Windows user gets to rely on;
normalizing host-side would be the host deriving an ordering core does
not define (the R-D line B4 holds for the `Target` column's VALUE, held
here for its ORDER); and this column's order never reaches a §W-A
artifact — the harness sorts FILE ENUMERATION and search rows (kept in
parity by that explicit UTF-16 rule), while `CanvasReadArtifact` passes
core's rows through in core's order — so no parity gate compares the two
hosts' sorted tables and no golden moves. The user-visible residue is the ordering of a Target/Color column
on a vault that mixes normalization forms. If an owner ever wants
byte-parity here, the honest shape is a CORE-supplied sort key rather
than a second host normalizer, and it belongs with the §W-G audit.

### PR C

**CD-40 — Focus delivery seats the shared selection SILENTLY; "lands
focus only" is not reachable.** The task brief asked for a delivery that
"must never mutate selection or narrate". The narration half is fixed
(contract C12) and is the half that reached the user. The mutation half
is not implementable on either projection: WPF's `TreeViewItem` selects
itself in `OnGotFocus`, and a `DataGrid`'s CURRENCY is its focused row —
there is no "focus this row without selecting it" on either control.

Recorded rather than worked around, on three grounds. R-B says there is
exactly ONE selection shared by every pane, so the reader and the
selection agreeing is the contract and not a side effect. Landing focus
on a row while the selection points elsewhere would leave every
selection-scoped verb acting on a card the reader is not on — a worse
failure than the one being fixed. And the alternative that DOES avoid
the seat — preferring the selection over `LastActivatedNode` — breaks
WCAG 2.4.3's return-to-where-you-were, which is what A14 exists for.

**Ratified by controller ruling, with the design argument on the
record**: the residue is not merely unavoidable, it is CORRECT. Spec
§PR A behavior 6 mandates the ACTIVATED row as the return-focus target
(WCAG 2.4.3) — the row the user demonstrably left from — so a delivery
is a statement about where the user IS, not an arbitrary jump. WPF's
focus-selection coupling then unifies the shared selection with focus at
exactly that point. That is CONVERGENCE on the return target, not a yank
away from the user; the platform constraint and the contract happen to
want the same thing. What was actually broken was the announcement, and
`AFocusDeliveryToANodeOtherThanTheSelectionDoesNotDouble` is the
defect's real closure.

The residue, stated plainly: a second pane's selection follows a first
pane's return from an activated card. Silently, to the row the user is
on, and by R-B's design.

**CD-41 — M4 does not cancel on a shell overlay; t0 §2 M4's palette
clause is superseded.** t0 lists the palette among the focus departures
that auto-cancel a mode. Windows does not, and neither does mac
(`CanvasModeController.swift`, after red-team #521). The reason is a
contradiction inside t0 itself: M6 requires every mode to be committable
and cancellable from visible controls "so Switch Control and Voice
Control never depend on the keyboard-only path", and Commit Mode, Cancel
Mode and the resize presets are PALETTE commands — so a palette that
cancels makes three registered verbs permanently unreachable.

Implemented as one named arm (`CanvasFocusDeparture.ModalOverlay`) of
one total switch, so reversing the decision is a one-line change with a
test that already enumerates it. Reported to the controller as the one
adjudication this task made against a normative source; the alternative
(implementing t0 literally) was rejected rather than deferred because it
would have shipped three dead commands.

**Ratified by controller ruling** (the shipped mac arm stands, and the
M4-vs-M6 contradiction is real), **with an upstream note attached**:
t0 §2 M4's palette clause is a DOC-FIX CANDIDATE at close-out. The
contract contradicts its own M6 for the mode-lifecycle palette verbs, so
the fix belongs in t0 rather than in either host — and t0's own preamble
("when a wave spec and this contract disagree, this contract wins — fix
the spec") gives no rule for a contract that disagrees with ITSELF,
which is why both implementations quietly diverged from the letter
instead of one of them being wrong. It joins the upstream-file list;
PR H's §9 reconciliation carries it.

**CD-42 — The filter's visible summary is mac's sentence, not t0's
spoken one.** Spec §PR C's Builds line writes the slot as "n of m
shown"; that is t0 §1.4's spelling for the SPOKEN Where-am-I filter
clause, which core renders and CD-5 already settled there is exactly one
of. The visible LABEL is mac's `filterSummary` — "3 of 40 cards match" /
"1 of 40 cards matches" — and §1 R-C says a static label is the mac
inventory verbatim. Both spellings ship, for two different surfaces, and
this row exists so the pair is not read as drift.

**CD-43 — Clear Filter always answers; mac stays silent when nothing is
filtered.** Mac's `canvasClearFilter` guards on `filterActive ||
!filterText.isEmpty` and returns silently otherwise, so the palette row
can do nothing and say nothing. Windows ANSWERS where mac is silent:
`Filter cleared — ⟨n⟩ cards.` is true of the resulting state whether or
not a needle was in the field, and t0's never-silent rule is what
decides the tie. The Escape RUNG keeps the guard — a rung that consumed
a press without an effect would break "exactly one rung per press" by
swallowing the rung below it.

The DECISION is unchanged; the mechanism sentence is not. It used to say
Windows announces "unconditionally", and both paths now go through the
admission mapping (C4): on a canvas that cannot answer, the count would
come from an empty outline, so "0 cards" would read as an empty canvas
rather than an unreadable one — a false sentence, which is the one thing
never-silent does not buy. The rung still consumes its press and still
clears the needle in every state; only the sentence is the mapping's.

**CD-44 — `nextCard` the CHORD and `nextCard` the COMMAND visit
different rows, deliberately.** The chord defers to the projection, so
Down on the outline also steps through the selected card's connection
rows — which contract A11 put there precisely as reading stops. The
palette row moves card to card over core's reading order across the
filtered set. Both narrate through the one selection mutation, so the
user hears one grammar; what differs is what "next" means for a reader's
arrow versus for a verb named "Next Card". Mac has the same split for
the same reason (its outline list interleaves connection lines and
`canvasSelectAdjacent` does not).

The consequence worth stating: `End of canvas.` fires when the
PROJECTION has nowhere to go, so on a canvas with connections the last
CARD is not the end while its connection row is still below the cursor.
`TheBoundaryIsTheProjectionsRowsNotCoresReadingOrder` pins it.

**CD-45 — A survivor whose containing group was filtered out is
promoted to a ROOT; the intermediate "nests under a surviving
GRANDparent" case cannot occur.** The implementation walks to the
nearest surviving ancestor and falls to the root, which is the safe
general form and costs nothing — but the intermediate case cannot be
reached, and the reason has to be stated exactly, because TWO obvious
versions of it are false and this row carried one of them for two
rounds.

Core matches a row on four routes (0b-13/0b-14): its own title, its kind
type word, ANY ELEMENT OF ITS GROUP PATH, and its activation target. The
group path is ANCESTOR-ONLY — root down to the immediate parent, the row
itself excluded (`canvas/model.rs`).

**The lemma that holds runs ANCESTOR → DESCENDANT GROUP.** If a group A
survives, every descendant GROUP of A survives:

* A matched by its own TITLE ⇒ that title is an element of every
  descendant's group path;
* A matched by the KIND word ⇒ every descendant group has the same kind
  word;
* A matched by an element of its OWN group path ⇒ A's group path is a
  prefix of every descendant's, so that element is in theirs too;
* A matched by TARGET ⇒ impossible, a group's target is empty.

Therefore no survivor can sit under a surviving ancestor A with a
filtered-out group between them: the intermediate group would have
survived. The nearest-ancestor walk finds the TRUE parent whenever a
surviving ancestor exists at all, and the only other shape is promotion
to the root. `AMatchingGroupCarriesItsDescendantsSoNoAncestorGapExists`
pins it.

**Two directions this does NOT license, both of which this row once
asserted.** It does not run descendant → ancestor: "every route that
matches a group G also matches its parent P, so P survives whenever G
does" is FALSE, because the group path is ancestor-only and a child
never carries a parent. A group whose own title matches inside a group
whose title does not is promoted to the root exactly like a card, and
`AGroupThatMatchesInsideANonMatchingGroupIsPromotedToTheRoot` is that
case on `promoted.canvas`. And it does not run ancestor → every
descendant ROW: the needle `group` matches a group by its KIND word
while a text card inside it matches nothing, which is why the lemma
above says descendant GROUP and must keep saying it.

The conclusion is unchanged by both corrections — promotion to the root
is still the only alternative shape — but it now rests on the direction
that is true. Recorded in full because the wrong proof of a right
conclusion is the kind of row that survives every review that only
checks the conclusion. The Windows
outline NESTS (CD-33) and the filter is a row subset, so a card whose
containing group did not match cannot sit under it. The alternative —
indenting it under a group that is not on screen — would claim a
containment the reader cannot verify. Mac's outline is flat with
indentation, so it has no such case; recorded because a reviewer
comparing the two projections will see the difference.

Both halves of that sentence were wrong until codex round 1 on C-lite.
The row said "the depth-stack pass makes it a root", and the mechanism
was the defect: depth is a position in core's READING ORDER, so a stack
run over the FILTERED rows attached such a survivor to whatever survivor
happened to be shallower and earlier — a card from an unrelated branch,
spoken as inside a group it is not in. And the rule was only half
stated: containment must be computed from the UNFILTERED hierarchy, so a
survivor attaches to its nearest surviving true ancestor and otherwise
becomes a root. `AFilteredOutlineNeverNestsACardUnderAGroupItIsNotIn`
pins it.

That is the implementation's general form, and this row used to justify
it with a case — "a survivor whose own group was filtered out but whose
GRANDparent matched belongs under the grandparent" — that the headline
above says cannot occur. Both cannot be true, and the headline is the
true one: by the ancestor → descendant-GROUP lemma, an intermediate
group cannot be missing while a higher ancestor survives, so the walk
never actually finds a grandparent to stop at. The walk stays because it
is the safe general form and costs nothing, NOT because that case is
reachable. The reachable shape is the other one: a survivor with no
surviving ancestor at all, promoted to the root
(`AGroupThatMatchesInsideANonMatchingGroupIsPromotedToTheRoot`).

**CD-46 — Next/previous card route through the read mapping; mac
returns silently outside `.ready`.** `canvasSelectAdjacent` guards on
`activeCanvasDocument` (which is `.ready`-only) and returns with no
announcement, so on mac an arrow or a palette row on a loading or
unreadable canvas says nothing. Windows routes both through
`AdmitStructuralRead`, so they answer with the state's own sentence.
No new copy: the arms were already in the corpus. Filed under "mac
details recorded while reading" as an upstream note rather than fixed
here — it is the same never-silent gap VA-1/VA-2 closed for the other
verbs, and mac's own membership guard does not see these two because
they reach `canvas_filter` only through `filteredOutline`.

**And the same two are silent on an EMPTY canvas, which is a second
divergence in the same verb.** Mac's `canvasSelectAdjacent` guards
`!rows.isEmpty` and announces `NoCardsMatchFilter` only when a filter is
active — so an arrow on a canvas with no cards at all says nothing at
all, in the `.ready` state, with no state sentence to fall back on.
Windows answers `Canvas is empty.` there
(`AnEmptyCanvasAnswersRatherThanMovingNowhere`), which is the arm the
onboarding region already renders, so again no vocabulary work. Both
halves go upstream together: the state gap and the empty-canvas gap are
the same never-silent hole seen from two sides.

**CD-47 — Escape inside the Where-am-I panel is the PANEL's, not the
ladder's; t0 §2 M5 has no clause for a focused transient region.**
Ruled panel-first, on mac parity and on the spec's own build text.

t0's M5 ladder is `mode → filter → surface → workspace`, and it says
nothing about where FOCUS is when the press arrives — it was written for
a reader standing in the canvas, and the transient regions §1.4 and §3
add came later in the same document without M5 being revisited. Read
literally, a reader standing IN the Where-am-I panel who presses Escape
to close it gets their typed filter needle destroyed instead, and the
panel stays open. Mac never had that behaviour: the panel's Close button
carries `.keyboardShortcut(.cancelAction)`, which resolves at the
key-equivalent phase BEFORE the container's ladder, so on mac the panel
closes first and the filter (and even an active mode) is untouched. The
spec §PR C Builds line says the same thing in its own words — "Esc
returns focus to the prior element".

**So an OPEN panel takes the press ahead of the ladder** — the key is
the panel being open, not where focus is. Mac's `.cancelAction` is
WINDOW-scoped, so it resolves whatever the focus arrangement, and
keying on focus instead left the same defect standing one arrangement
over: an open-but-unfocused panel plus an Escape from the projection
destroyed the needle AND left the panel sitting there. With the
panel-open key there is no divergence from mac left to record — the
behaviours match, which is the outcome an adjudication should reach when
the reference is right.

Focus RESTORE is the part that stays locus-dependent, and
`CloseWhereAmI` owns it: the reader is put back only if they were INSIDE
the panel. Dismissing a panel someone was not in must not relocate them,
which is the mirror of the defect the restore exists to prevent. The
ladder is otherwise unchanged and still owns Escape from the projections.

**Rung 3 no longer closes the panel, and this row said it did.** An OPEN
panel pre-empts the whole ladder — that is what CD-47 IS — so the press
never reaches rung 1, let alone rung 3, while the panel is up. Rung 3's
remaining arms are the interim card detail and leaving the filter field
(`DismissTransientRegion`). `CloseWhereAmI` is still reached from rung 3
in code, as the arm that runs when the panel is somehow visible without
the pre-empt having fired, but no Escape a reader can press takes that
route. The clause is corrected rather than deleted because "rung 3
closes the panel" was true for one wave and is the kind of sentence a
reader checks the ladder table against.

**Why it went silent, and the class it belongs to.** This is CD-41's
shape a second time: a t0 clause that does not mention the case, so both
hosts diverge from the letter without either being wrong, and nothing
records it because there is no contradiction to trip over — only an
absence. CD-41's sweep stopped at M4 and never re-read mac's M5
consumers. **The rule this sets: a t0-vs-reference adjudication is swept
per CONTRACT, not per site** — when one clause of M1–M8 is found to
disagree with the reference, every clause with the same consumers gets
re-read in the same pass.

The M5 blind spot joins the upstream-file list beside CD-41's M4-vs-M6
one: t0 §2 M5 should name the transient regions §1.4 and §3 introduce
and say where they sit relative to the rungs.

**CD-48 — Right/Left FOLLOW unconditionally; the spec's "as mac does"
premise was false.** Spec §PR C Builds asked for "connection-follow when
the selected card has connections, else tree semantics — pin the
precedence as mac does; record". Mac pins no such precedence:
`CanvasOutlineView.swift` delivers `canvasFollowConnection`
unconditionally and returns handled, so a connectionless card ANSWERS
there ("No outgoing connection."). Mac's list has no expand/collapse for
an arrow to defer to, so the blend the spec describes was never shipped
anywhere.

Implementing the spec's sentence left one keypress on a leaf doing
nothing and saying nothing — the never-silent rule broken by a
precedence nobody had. Mac is the authority: the arrows follow always,
and the leaf gets the `NoConnection` sentence the vocabulary already
carries.

**Expand/collapse keeps three keyboard routes, VERIFIED rather than
assumed** (`ExpandCollapseSurvivesTheArrowsBeingClaimed`): Enter on a
group toggles it through the one activation seam; WPF's own
`TreeViewItem` numpad `+`/`-` still arrive, because the canvas claims
neither; and the `ExpandCollapse` pattern — the route a screen reader
actually drives, and the one that matters most here — is untouched. The
numpad pair is recorded as a route rather than THE route: a keyboard
without a numpad has the other two.

**The TABLE keeps Left/Right** for the grid's cell navigation, which the
UIA Table pattern depends on and the W4-1 conformance matrix asserts;
follow there is the palette row, and it answers identically. Recorded so
the asymmetry is a decision.

**Root class, and what it costs.** The spec sentence was written from
memory of mac rather than from the source, and then transcribed into a
code comment that repeated the attribution — the same false-attribution
shape B3/CD-39 closed by binding claims to primary sources in line. Both
the contract and the comment now name the file the behaviour comes from.

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

**CR-5 — The residue count is unchanged by 0a-1; 0a-2 lowers it.**
`A11yResidueCensusTests.pinnedResidueSites` stayed 30 through 0a-1 and
drops to **29** in Task 0a-2, where `CanvasAnnouncer` stopped posting
`.hostComposed` — **done**, together with the `a11y.rs` module-doc
paragraph that named the canvas announcer as a residue engine.
`AppState.swift`'s `postMutationAnnouncement` is a SHARED residue site
used by five canvas admission paths and by non-canvas structural
builders, so it cannot be deleted by the canvas migration even though
`CanvasMutationRefused` now exists (0a-12); the canvas call sites moved
off it and the marker stays. Its `.hostComposed(` line moved into a new
`mutationAnnouncementEvent(_:)` seam so the canvas funnel can carry the
one sentence it still relays (BatchTrash's quarantine reason) without
adding a residue site of its own — the count is 29, not 30.

---

## Mac details recorded while reading (not this issue's to fix)

- **Backspacing a needle to EMPTY exits filtering silently, on both
  hosts.** `AppState+CanvasExtras.swift:415` guards
  `canvasAnnounceFilterCount` on `filterActive` and returns, and mac's
  `.onChange(of: filterText)` calls exactly that — so deleting the last
  character widens the canvas back to every card and says nothing. The
  explicit CLEAR verb announces `canvasFilterCleared`; the keystroke
  that does the same thing does not. Windows matches, deliberately:
  `AnnounceFilterCount` has the same guard, and diverging here would
  make the two hosts disagree about a keystroke rather than fix
  anything. **It is a real gap in both** — a screen-reader user who
  backspaces to empty gets no confirmation the canvas widened, which is
  the never-silent rule's own subject — so it belongs upstream as one
  decision for both hosts: either the last backspace speaks
  `canvasFilterCleared` like the button, or the rule records why a
  keystroke-driven widening is exempt.

- **`canvasColorMarked` is unregistered.** `AppState+CanvasActions.swift:757`
  is implemented and tested but has no command id, palette row, or menu
  item. Windows matches the registry, not the hidden verb (D-4); file
  the mac issue.
- **The renderer's auto-side rule is duplicated** —
  `AppState+CanvasConnect.swift:24–33` vs `CanvasRendererView.swift:582–600`
  (`anchorPoint`'s `case nil` arm). PR 0b deletes it.
- **Preset colour names are spelled three times in Swift** —
  `AppState+CanvasActions.swift:340`, `:766`,
  `CanvasPromptSheet.swift:227` — shadowing core's `color_name()`.
  **Done (0a-2):** the two announcement dictionaries die with the typed
  `Option<CanvasColor>` payload; the picker's labels come from the new
  `canvas_color_name` export (0a-11).
- **`CanvasTableView.swift:92` discards core's priority**: it unwraps an
  already-core-rendered event to text and re-wraps it as `.status`
  (Medium). **Done (0a-2):** `CanvasAnnouncer.relay(_ event: A11yEvent)`
  carries the render's text AND priority; pinned by
  `CanvasAnnouncerTests.testRelayCarriesTheCorePriorityOfANonCanvasEvent`.
- **`CanvasOutlineView.swift:391` still uses the title-keyed group
  lookup** that `AppState+CanvasNavigation.swift:172–180` deliberately
  replaced for Codoki #613 (repeated group labels miscount).
  **Done (0a-2), earlier than expected:** CD-4's correct number is the
  arrived-at row's `total_m`, so BOTH lookups are deleted rather than
  replaced — 0b's `canvas_children_of` is no longer needed for this.
- **The funnel guard has a hole**:
  `CanvasAnnouncerTests.testNoDirectAnnouncementsUnderCanvas:168–191`
  greps only `postAccessibilityAnnouncement`, so the five canvas
  `postMutationAnnouncement` sites bypass it undetected.
  **Done (0a-2):** the guard scans both names (comment-only lines
  dropped, so prose naming them cannot trip it) and no canvas file
  calls either.
- **A stale selection survives undo.** `reloadAfterMutation`
  (`CanvasDocument.swift`) refreshes the outline, table, scene and
  targets but never reconciles `CanvasSelection.selected`, so undoing a
  create leaves the selection pointing at a node that no longer exists.
  Verbs whose guard tests the raw selection then pass, while the
  collection they announce — resolved against the outline — is empty.
  The symptom codex round 6 traced is Duplicate announcing
  `Duplicated 0 cards` (and writing an empty undo entry named
  `duplicate 0 cards`). PR 0a pins the RENDERING (the zero witness is in
  the corpus and 0a-14 requires it); reconciling selection after a
  mutation is a host fix — file the mac issue. Windows must not copy it:
  PR E's mutation funnel should drop a selection its reload cannot
  resolve.
- **`Deleted connection ` with a trailing space** is reachable on mac
  when the edge lookup misses (`AppState+CanvasConnect.swift:149`'s
  `?? ""`). The typed event cannot express it. **0a-2's resolution:**
  the structural lookup runs BEFORE the apply and a miss returns
  without deleting — see CD-7's behaviour note.

Read during PR 0b (Task 0b-1), none of it this task's to fix:

- **The move-into-group first-child anchor is title-keyed**
  (`AppState+CanvasActions.swift:439–441`:
  `$0.groupPath.last == group.title`) — the same repeated-label
  miscount Codoki #613 flagged, still live at that one site after 0a-2
  retired the other two. `canvas_children_of` is id-keyed and retires
  it in Task 0b-2; the fix is free with the migration.
- **Group-around-marked is a silent no-op when no member resolves**
  (`AppState+CanvasActions.swift:791–812`: `guard minX.isFinite else
  { return }` — no announcement of any kind). `canvas_group_rect_around`
  types the outcome as `None` (CD-24); deciding what the host SAYS
  there is PR G's, not core's.
- **`MIN_CARD_SIZE` is enforced as REJECT-the-step, not clamp-to-min.**
  `canvasModeStep` (`AppState+CanvasModes.swift:171–174`) refuses the
  whole step — neither dimension changes — and announces
  `Minimum size.` when EITHER new dimension would fall below 40. §PR F
  of the spec says "clamp at `min_card_size`", which is a different
  behaviour; the constant is core's (0b-4), the reject rule is PR F's
  to copy. Recorded here so PR F does not read the spec's word
  literally. Fit-to-content separately floors height at the same 40 and
  caps it at 600 (`:213`) — part of D-5's shared formula.
- **The trace-path walk leaves the selection on the last hop**
  (`AppState+CanvasNavigation.swift:160`, announcement-suppressed).
  `canvas_trace_path` returns the hop list and nothing about selection;
  where the caret lands is host state (§2 row F, Tier 3).

Read during PR 0b (Task 0b-2), not this issue's to fix — **file at
close-out**:

- **A degraded or unavailable canvas is ANNOUNCED but still not
  NAVIGABLE, and t0 §5 wants both.** The state story itself lives in
  VA-2's section — `canvasReadRefusal(for:)`'s table — and this entry
  cites it rather than restating it; what is filed here is only the
  half that remains open.

  **Closed by PR 0b:** the silence. `.degraded`, `.failed` and
  `.retargetFailed` answer `.notReadable` at every read verb now, so a
  keypress on such a canvas says why it did nothing instead of doing
  nothing quietly.

  **Still open:** those canvases cannot be READ. `activeCanvasDocument`
  gates the snapshot-only verbs on `.ready`
  (`AppState+CanvasNavigation.swift:24–33`), and the deeper obstacle is
  not the gate — `CanvasDocument.load` (`:431–444`) *releases the handle
  and sets `outline = []`* on a degraded load (Codoki #608's resource
  fix), so there is nothing left to navigate even if the gate opened.
  (The published snapshot DOES survive on the trashed and
  retarget-failed paths — `markMovedToTrash` `:590–600` clears the
  handle but not `outline` — which is what made the code look navigable
  on a first read.)

  t0 §5 wants a degraded canvas readable, so a user can inspect what
  survived a bad file rather than facing a dead tab. Closing it means
  deciding whether a degraded load keeps a read-only handle: a
  core-adjacent design question with a §W-D announcement surface, a
  piece of work in its own right, and no PR has owned it. **Filed
  rather than absorbed into W6-1:** PR 0b did not cause it, and the
  window PR 0b DID change (`beginBatchRetarget`'s) is a different state
  with its own answer (VA-1).
- **t0 §2 M5's ladder does not mention the transient regions** §1.4 and
  §3 introduce (W6-1 PR C, CD-47 — ruled panel-first). The rungs are
  `mode → filter → surface → workspace` with no clause for where FOCUS
  is, so read literally a reader standing in the Where-am-I panel who
  presses Escape loses their filter needle instead of closing the panel.
  Both hosts do the sensible thing and neither records it, because an
  absence trips nothing. The fix belongs in
  `09_canvas/specs/t0_interaction_contract.md`: name the transient
  regions and say where they sit relative to the rungs. Same blind-spot
  class as the M4 note below, found by the sweep that note's rule
  demanded.
- **t0 §2 M4's palette clause is a DOC-FIX candidate** (W6-1 PR C,
  CD-41 — ratified by controller ruling). Not a mac-code note like the
  rest of this list, and listed here anyway because this is where the
  close-out reads the things to file: the fix belongs in
  `09_canvas/specs/t0_interaction_contract.md`, not in either host. M4
  names the palette among the departures that auto-cancel a mode while
  M6 requires every mode to be committable and cancellable from visible
  controls — and Commit Mode, Cancel Mode and the resize presets ARE
  palette commands, so the two clauses cannot both hold. Both hosts
  implement M6's side. t0's precedence rule covers a spec disagreeing
  with the contract and says nothing about the contract disagreeing with
  itself, which is why the divergence went unrecorded on mac until now.
- **Where-am-I's no-selection fallback names the first UNFILTERED row**
  on both hosts (`doc.outline.first` / `Outline[0]`), so with a filter
  active it can describe a card that is not on screen. Shared-reference
  quirk found reading for W6-1 PR C (m4); filed rather than fixed
  one-sided, because a Windows-only change would make the pull surface
  answer differently on the two platforms for the same canvas.
- **The movement verbs can seat a filtered-out node** — enter-group,
  follow-connection and trace-path all move to a card core named without
  asking whether the surfaces are showing it, and enter-group narrates
  the arrival. Verified mac-parity (`canvasSelect` has no filtered-set
  check either, and mac's list equally cannot show the row), so not a
  Windows defect (m5). Recorded because it grinds against CD-40's
  ratified "the reader and the selection agreeing IS the contract": the
  focus delivery that follows finds no container and no-ops, so the
  reader's cursor and the selection every selection-scoped verb acts on
  come apart. The honest fix is a shared decision about whether
  structural movement escapes the filter, which is a t0/spec question
  rather than a host bug.
- **`canvasSelectAdjacent` is silent outside `.ready`** (found reading
  for W6-1 PR C). It resolves through `activeCanvasDocument`, which
  admits only `.ready`, and returns with no announcement otherwise — so
  an arrow or the Next/Previous Card palette row on a loading, degraded,
  failed or retarget-failed canvas says nothing. It is the same
  never-silent gap VA-1/VA-2 closed for the other read verbs, and mac's
  own membership guard cannot see it: the verb reaches `canvas_filter`
  only through `filteredOutline`, so the source scan never matches it
  (which VA-2's section records as a deliberate non-exclusion, for the
  filter-view reason — the SILENCE was not the thing being decided
  there). Windows routes both verbs through its state mapping (CD-46)
  using arms already in the corpus, so closing it upstream needs no
  vocabulary work.

---

## Owner decisions (adopted by controller ruling 2026-08-22, autonomous run)

| # | Decision | Adopted |
|---|---|---|
| D-1 | §2 tiering (Tier 1+2 move to core with mac consumption; Tier 3 host-by-designation) | **Accepted as listed.** Any demotion of a Tier-2 row is a recorded divergence naming the duplicated rule. |
| D-2 | Chord collisions | **`whereAmI` = Ctrl+Alt+Shift+I** (G18 precedent, `Divergence` recorded on the row); **`connectTo` keeps Ctrl+Alt+C** in `ChordScope.Canvas` (disjoint delivery site from Reading — the Ctrl+F precedent). |
| D-3 | `speakable_name` session stickiness (T R21) | **Core deterministic by document order**; R21 stickiness preserved by the same mechanism core uses for untitled ordinals if it is session-held, else "ordinals may renumber on delete" is a two-platform divergence. **Settled in 0b-1: the second branch.** Mac's stickiness lives in a per-VIEW map, so it was never session-held; ordinals renumber on delete, identically on both platforms — CD-20. |
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

### Task 0a-2 (the mac consumption half)

- **The migration is 140 → 0 free-text announcements.** All 136
  `canvasAnnouncer.announce(…)` call sites across ten canvas files plus
  the four inside `CanvasModeController` construct a typed
  `CanvasA11yEvent`; `CanvasEvent` and its twelve cases are deleted,
  along with `phrase(_:)`, `whereAmIText(…)`, `createdText`,
  `relativePhrase`, `undidText` and `redidText`. `CanvasAnnouncer` is
  158 lines of verbosity storage, class-keyed coalescing, error flush
  and `flushForTests` — plus one `a11yRender` call.
- **The host `CanvasVerbosity` enum could not have survived.** uniffi
  generates `public enum CanvasVerbosity` into
  `Sources/SlateMac/slate_uniffi.swift`, the SAME Swift module as the
  hand-written canvas code, so 0a-1's core enum and the host's
  three-case copy were a redeclaration: **the mac lane could not have
  compiled on 0a-1 alone**. The host copy is gone and the FFI enum is
  extended in place (`Codable` over a stable persistence tag,
  `CaseIterable`, `title`) exactly as `MathPrefs.swift` does for
  `MathVerbosity` — the stored preference strings are unchanged, so
  saved prefs decode as before.
- **CD-4 fell out of the row data, not a new query.** The entered
  group's own card count is exactly the ARRIVED-AT row's `total_m` (its
  container size). mac walked back to the group's own outline row and
  spoke ITS `total_m` — the group's SIBLING count. The fix deletes the
  walk instead of adding one, which also retires the Codoki #613
  repeated-label hazard at both call sites without waiting for 0b's
  `canvas_children_of`.
- **The residue census forbids the string primitive**, so the
  announcer's default post goes through `AppKitAnnouncementPoster`
  rather than `postAccessibilityAnnouncement(_:priority:)`: a
  `priority:` label at the top level of that call is exactly what
  `testNoInteractionSiteCallsTheStringPrimitiveDirectly` fails on.
- **`postMutationAnnouncement`'s `.hostComposed(` line moved into a new
  `mutationAnnouncementEvent(_:)`** so the canvas funnel can carry the
  one admission sentence that is NOT canvas vocabulary (BatchTrash's
  quarantine reason) without adding a second residue site. The canvas
  six announce as `CanvasMutationRefused` and record into the same
  structural-mutation ledger, so §U2-6's verbatim-string assertions and
  the focus token are byte-identical either way.
- **The mode controller's `ModeSpec` is typed** (`CanvasMode`,
  `CanvasModeObject`, `onCommit -> CanvasA11yEvent?`,
  `onCancel -> CanvasModeRestoration`), which rewrote the M1–M7 test
  FIXTURES: they used to invent strings ("Move mode — 'Research'. …")
  and now assert the real shipped ones. No shipped expectation changed
  there; only the fixture shape and the M3 label's quoting
  (`Move mode: 'Research'` → `Move mode: "Research"`), which follows
  core's `mode_object`.
- **The M3 inspectable value is the one host-side spelling of the mode
  names that survives.** It is §W-C label class (0a-13), composed from
  the same typed fields the spoken entry carries; a label-grade
  accessor on 0b's query surface is where it collapses.
- **The outline keeps a `CanvasCardRef`** — as a LABEL helper for the
  row's `accessibilityLabel` only, moved out of `CanvasAnnouncer.swift`
  into `CanvasOutlineView.swift`. The outline's DUPLICATED direction
  phrases are gone: the connection row renders the same
  `CanvasConnectionTraversed` event the navigator announces, so the row
  now reads `Connects to Text card "Ideas"` rather than
  `Connects to "Ideas"` (it also stops hardcoding `kind: "text"`).
  Both facts are contract-level, not just notes: the surviving helper
  is in 0a-10's scope table and the row's text change is **CD-14**.
- **`canvas_color_name` is a new FFI export.** Without it the third
  preset-name copy (`CanvasPromptSheet.swift:227`, the picker's button
  labels) could not die: it is label class, so no announcement event
  renders it, and 0a's surface had no colour-name accessor. Ten lines
  over the `CanvasColor` type that the typed payload already carries
  across the boundary.
- **CD-7 costs a behaviour note.** `canvasDeleteConnection` now looks
  the connection up structurally BEFORE applying and returns without
  deleting when the edge is not among the selected card's neighbours —
  where mac deleted it and spoke `Deleted connection ` with a trailing
  space (`?? ""`). Connection rows only ever materialise under the
  selected card, so the path is unreachable in the shipped UI, and the
  typed event cannot express that string.
- **CD-3 is scoped by the view's lifetime.** The degraded-load
  announcement fires from a `@State` flag on `CanvasContainerView`, so
  "once per open" is "once per mounted container"; the banner renders
  the SAME event, so the two spellings cannot drift.

### Task 0b-1 (the Rust + FFI + Windows-harness half)

- **Mac's speakable names are NOT duplicated; the defect is narrower.**
  The research report claimed mac's loop yields `A`, `A 2`, `A 2` on
  document order `A`, `A`, `A 2`. Re-deriving both algorithms side by
  side shows it does not: its `while used.contains(candidate)` keeps
  incrementing, so mac produces `A`, `A 2`, `A 2 2` — unique. What it
  lacks is the check against REAL titles, so a generated ordinal can
  spell a different card's actual title and a Voice Control user who
  says what they read lands on the wrong card. 0b-5 records the
  correction rather than editing the claim away, because the wrong
  version reached a commit message on this branch first.
- **A census can assert the wrong property and look green.** The
  speakable-name census asserted uniqueness, which mac satisfies too;
  deleting the taken-guard left it passing and failed only the three
  fixture tests. It now also asserts that no GENERATED name spells
  another card's display title, which is the property the guard exists
  for, and deleting the guard fails it.
- **`(y, x)` order is not `Below` → `RightOf`.** Contract 0b-12's first
  draft said the inside-group lattice is visited in `(y, x)` order and
  called that `place_new`'s preference; `(y, x)` exhausts a ROW first,
  which is `RightOf`. Caught by having to write
  `inside_group_prefers_below_then_right`, which had to pick one. The
  code walks a column top to bottom before moving right.
- **`speakable_name` had to reach `CardSummary`, not just the session.**
  Putting it in the session layer would have meant computing it per
  query and twice for a row type that also feeds the scene; on the
  summary it is derived once per model, which is also what makes the
  0b-6 join a hash lookup rather than a second derivation.
- **`target` moved into `CardSummary` too.** The filter matches it, and
  `canvas_db` had its own `match` on node kind for the column. Two
  spellings of "what does this card point at" is exactly the §W-G
  failure mode, inside core this time; the column now writes the
  summary's field.
- **`getrandom` was already in the lock** (via `tempfile`, a direct
  slate-core dependency), so `canvas_new_id` cost no new crate. It is
  the whole dependency: the id is 16 hex characters with `'4'` at index
  12, which needs randomness and no UUID type at all.
- **The parity harness needed a second vault, not a second loop.**
  Copying the canvas fixtures into the markdown vault would have
  changed what `search`, `links`, `tasks` and `properties` see. With
  its own vault, regenerating produced exactly one new artifact and
  left the other twenty-eight byte-identical.
- **Not built, deliberately:** `canvas_containing_group_of_rect` (the
  transient-rect form of `parent_of`) and `canvas_roots`. The research
  report suggests both; neither is in PR 0b's deliverable list, and
  PR F is the first caller that would need the former. Recorded here so
  the omission is a decision rather than an oversight.

### Task 0b-2 (the mac consumption half)

**Per-row site counts — what was deleted, and what consumes core now.**
Every "before" number is a grep over the mac tree at BASE `8564946`;
every deleted symbol was re-grepped to zero afterwards.

| Row | Swift derivations deleted | Core call sites now |
|---|---|---|
| B | 1 — `canvasRelativeDescription`'s candidate filter, squared-distance sort and axis phrasing (`AppState+CanvasModes`) | 1 `canvas_describe_relative` |
| C | 2 — `AppState.canvasAutoSides` and `CanvasRendererView.anchorPoint`'s `case nil` arm | 3 `canvas_auto_sides` (connect, create-connected-card, renderer) |
| D | 3 — the enclosing-group filter (`+CanvasCreate`), the inside-a-picked-group test (`+CanvasExtras`), the title-keyed first-child lookup (`+CanvasActions`, deleted with row H's placement rewrite) | 1 `canvas_parent_of` + 1 transitive `canvas_children_of` walk |
| E | 3 — the `depth + 1` enter walk, the `depth − 1` exit scan, the greedy trace loop (`+CanvasNavigation`) | 1 `canvas_children_of`, 1 `canvas_parent_of`, 1 `canvas_trace_path` |
| F | 2 — `canvasMovingSet`'s and `canvasMarkedInOrder`'s outline projections | 1 `canvasInReadingOrder` helper over `canvas_order_nodes`, called by both |
| H | 3 mirrored `static let`s + 21 retyped size literals + 1 bounds re-union + 1 bbox fold (with its literal `pad = 40`) + 1 move-into-group placement block (with its `(x + 20, y + 40)`) | 26 `canvas_constants()` field reads (5 replacing the statics, 21 the literals), 1 `canvas_bounds`, 1 `canvas_group_rect_around`, 1 `canvas_place_inside_group` |
| K | 1 — `CanvasDocument.matchesFilter` | 1 `canvas_filter`, memoized per needle |
| L | 1 two-pass walk + 2 per-view maps (`speakableNames`, `assignedSpeakable`) | 2 reads of `CanvasSceneNode.speakableName` |
| M | 1 — `AppState.newCanvasEntityID` | 9 `canvas_new_id()` |

- **One mac expectation moved, and CD-20 is why.**
  `CanvasRendererTests.testRenameRefreshesLabelAndSurvivorsKeepOrdinals`
  asserted that renaming the first of two `Ideas` cards left the second
  as `Ideas 2`. Core recomputes speakable names from document order on
  every derivation, so the survivor is now plain `Ideas` — the ordinal
  existed only to disambiguate against the card that was renamed. Two
  assertions flip and the test is renamed to
  `…AndOrdinalsFollowDocumentOrder`. **No other expectation in any mac
  suite changed**; the only other test-file edits are the four
  `filteredOutline` call sites gaining the `session:` argument (the
  asserted values are untouched) and new tests.
- **The three containment copies were not three copies of one
  question.** Copy 1 asked "which group contains this card" (parent),
  copy 3 asked "what is in this group" (children) — but copy 2 asked
  "is this node inside any PICKED group", which is only the same as
  "is it a descendant" while groups nest. Two groups that overlap
  without nesting answer differently: mac included a node whose centre
  fell in the picked group even when the outline showed it inside the
  other one. Core's tree answers the question the outline shows, and
  Duplicate now walks `canvas_children_of` transitively. **CD-27**
  records the scenario and both answers; no mac test pinned the old
  one.
- **`canvas_place_inside_group`'s `TooSmall` still needs a host
  overlap check.** The contract says its point is the inset, unchecked
  — so `canvasMoveIntoGroup` runs `canvas_check_overlap` on that rect
  and keeps mac's shipped `No free space inside "X".` refusal. `Full`
  takes the same refusal directly. Without the check the host would
  have silently stacked a card in a group too small to hold it.
- **One degenerate navigation path was held byte-identical; the other
  was SUPERSEDED and this bullet used to assert the old one.** The
  surviving half: trace-path still names its start card from the outline
  row, so the `No outgoing path from "X".` string is composed where it
  always was.

  The retracted half said exit-group "returns silently for a selection
  the canvas no longer holds", on the reasoning that announcing anything
  there would be a new sentence rather than a migration. **VA-1's
  throw-arm table replaced that**: a `bad_node` throw now answers
  `Nothing selected.` at exit-group, enter-group and trace-path alike,
  because a card core cannot resolve has no level, no children and no
  path — and silence is the outcome the never-silent principle rules
  out. The behaviour was already changed in code when this bullet still
  described the old one, which is exactly the drift a PR C/E author
  copying "silent by design" into the Windows navigator would have
  shipped as a t0 violation. Retracted in place rather than deleted,
  because the reasoning that produced it — "a new sentence is out of
  scope for a migration" — is the tempting one to repeat.
- **Structural navigation's state answers moved twice, and this entry
  is the history — VA-2's section owns the CURRENT story.** For the
  mapping, the membership guard and the per-state sentences, read there
  (`canvasReadRefusal(for:)`'s table); what is recorded here is how the
  claims got wrong, because the failure mode outlived each individual
  correction.

  The first draft of this note said the silence affected "a canvas
  moved to Trash, or one whose retarget failed". Both were wrong:
  `activeCanvasDocument` gated on `case .ready`, so those states were
  unreachable by every navigator verb — the claim was written from the
  shape of the code rather than from its reachability, the same class
  of error the round record already carries twice.

  The second version scoped the answer to `beginBatchRetarget`'s window
  alone — the one state pairing `.ready` with a detached handle, where
  the snapshot stays visible while nothing may save through the
  moved-away path. True, and still incomplete: `.loading` had no answer
  at all, and the three unreadable states kept their silence.

  **What is true now** (codex 0b rounds 1–4, rule 4): one total mapping
  answers every state, `.loading` included, and the three unreadable
  states say `.notReadable` instead of nothing — which closes the
  ANNOUNCEMENT half of the t0 §5 gap filed in "Mac details recorded
  while reading", leaving only navigability open there. Arrow movement
  still narrates in the reopening window (the filtered outline serves
  the displayed rows), and mutations are still refused audibly
  (`CanvasMutationRefusal::Reopening`). A verb's own selection question
  still outranks the state's on a canvas whose snapshot is on screen —
  which is `.ready` including that window, AND `.retargetFailed`, whose
  retained rows the container renders read-only. Round 5 corrected that
  last clause: it had been written as "`.ready`", a hand-curated
  restatement of a fact that belongs to the view, and it was wrong by
  one state. "On screen" is now `LoadState.rendersRetainedSnapshot`,
  derived from `CanvasContainerView`'s switch and pinned to it by
  `the_snapshot_visibility_predicate_matches_the_container_switch`. Per
  the same lesson, which verbs ask that question is no longer a list
  either — `every_mac_canvas_read_is_gated_or_named` asserts that any
  verb able to announce `.nothingSelected` reaches it through the gate.

  Silence is a test failure now:
  `testOnlyTheBatchRetargetWindowPairsReadyWithNoHandle` asserts the
  sentence at each VA-1 member, and
  `testEveryNonReadyLoadStateAnswersWhatTheMappingSays` drives every
  non-ready state with its expectations taken FROM the mapping, in BOTH
  selection columns — the round-5 miss was a test that forced a
  selection before exercising each state and so never asked the
  absent-selection half.

  **Retaining the detached handle for READS is refused permanently, and
  this is the reasoning so it is not re-proposed.** It is the cheapest
  way to restore full navigability in that window and it was
  considered. Today, "no edit can save through the moved-away path" is
  a STRUCTURAL invariant: there is no handle, so `canvas_apply` cannot
  be reached at all. Handing the verbs a read-only handle would
  downgrade that to a host-enforced one — `admitCanvasMutation`'s
  refusal would become the only thing standing between a mis-sequenced
  call and a canvas re-created at the path the user just moved away
  from. Trading a structural write-safety invariant for an
  announcement is disqualified: the announcement was obtainable
  another way, and safety invariants that depend on every caller
  remembering a guard are the ones that fail quietly. (Controller
  ruling, W6-1 0b-2 fix round 2.)
- **`try?` on an optional-returning FFI call FLATTENS (SE-0230), and a
  scoped review blessed the shape anyway.** `canvas_parent_of` returns
  `String?`, so `try? session.canvasParentOf(…)` is `String?`, NOT
  `String??`. Two consequences, and 0b-2 hit both: the two-step
  `guard let` shape it was written with does not COMPILE, and — the
  reason it matters after the compile error is fixed — a throw and a
  `nil` result arrive as the same `nil`, erasing exactly the
  distinction VA-1's throw-arm table exists to make. The scoped
  re-review looked at this hunk and recorded that "the trap was
  avoided"; it was not. Every such site now uses `do`/`catch` where the
  distinction is load-bearing, and the sites where it is NOT (CD-24's
  group-rect silence, VA-1's recorded `canvas_bounds` exclusion) carry
  a comment saying so instead of a `?? nil` that implies a second level
  of optionality that never existed.

  **For future Swift reviews:** check every `try?` against its
  function's RETURN type, not against the call's shape. The optional
  the reviewer expects to see is not always there, and the failure is
  silent in prose review because the code reads as if it distinguishes
  what it cannot.

  **The mechanical check reported zero once before it was true.** Its
  first closure matched only `receiver.method(…) ?? nil` — a bare
  receiver with the unwrap inline — and so missed both shapes the one
  remaining site used: an optional-CHAINED receiver
  (`currentSession?.canvasNodeText`, where chaining flattens too), and
  an unwrap applied to a bound variable on a later line. The zero was a
  property of the closure, not of the tree. Widened to cover any
  receiver shape and unwrap-via-binding, it finds that site in the
  pre-fix tree and reports zero after — which is the only order in
  which a zero means anything. Recorded because "the automated check
  says zero" is exactly the reassurance that stops a reviewer looking.
- **One row-F caller is not behind admission, and the cost is
  cosmetic.** `canvasInReadingOrder` answers `[]` without a handle, and
  its first doc comment claimed every caller was behind
  `admitCanvasMutation`. Not true: the card picker's `excluded:`
  argument is a sheet BODY, re-evaluated on every SwiftUI pass, so a
  reopening window opening while the sheet is up leaves it empty and the
  picker stops hiding the moving set. Picking one of those rows is still
  refused downstream — `canvasPlaceRelative` guards
  `!moving.contains(target)` and announces
  `Pick a card outside the moving set.` — so no wrong placement is
  reachable through it; the user is offered rows that would otherwise be
  omitted. Left as-is deliberately (the alternative is threading a
  handle into a sheet body for a transient cosmetic), with the comment
  corrected to say what is true.
- **`filterActive` did not move, and that is a decision.** It is UI
  state — the Clear button, the summary, the Esc rung — not the match
  rule, so it keeps Foundation's `.whitespaces` trimming. The one input
  where host and core disagree is a needle of nothing but newlines:
  active by the host's test, empty by core's, therefore matching
  everything. CD-22 already lists newline trimming among the recorded
  differences; this is where a user could see it.
- **The filter needed a memo, not just a call.** `filteredOutline` is
  read several times per SwiftUI body pass, and at the §K 2,000-node
  budget each read would be another `canvas_filter` round trip. It is
  memoized per needle and invalidated exactly where `neighborsCache`
  is — six sites, the same lifetime rule.
- **CD-6's carried half was three sites, not six.** The undo-stack
  action names are SPOKEN (`CanvasHistoryApplied.name` is a payload),
  so each must count the way the sentence it undoes counts. Delete,
  colour and group render through core's grouped `counted`, so their
  names call core's `count_noun` over the FFI (CD-26); move, duplicate
  and the mode object render through core's ungrouped `plural`, so
  leaving them on the host's `counted` is what makes them agree. A
  blanket "group everything" would have broken the pairing in the other
  direction. Below 1000 the two are byte-identical, which a test
  asserts rather than assumes.
- **The first fix for that was a host mirror, and it was wrong.** The
  grouped counting initially landed as a four-line
  `CountCopy.countedGrouped` re-deriving `group_thousands` in Swift,
  on the reading that core was out of scope. Corrected by controller
  ruling: `count_noun` is now an FFI export and the Swift mirror is
  deleted (CD-26). Recorded as a correction rather than edited away —
  "small, pinned by test, and the alternative was out of scope" is
  exactly the reasoning that puts a second definition of a core rule in
  a host.
- **Two Swift-side helpers survive by design.** The renderer's
  `Group ⟨name⟩` prefix is §W-C label class, not a spoken card
  reference; and `MIN_CARD_SIZE`'s reject-the-whole-step enforcement
  stays host-side, with the CONSTANT coming from core. Both are
  commented at the site so PR F copies the rule and not the spec's
  looser "clamp".
- **The §W-A Swift twin was verified mechanically, not by eye.** The
  ordered sequence of `Raw(...)` literals in the two serializers is
  identical (40 tokens); the full emission sequence aligns 1:1 apart
  from the two places C# spells an if/else where Swift calls
  `appendOptionalString`; and the three pinned lists — filter queries,
  relative rects, the `large_2000.canvas` exclusion — compare equal
  value for value. The equality that matters is still the committed
  golden, which only the mac CI lane can decide.

### Task A-1 (the Windows document, tab and outline)

- **CD-28 is not a reading, it is measurable.** The committed
  `malformed.canvas` — the fixture whose whole purpose is entries core
  preserves but cannot show — comes back `degraded: false` with eight
  warnings, five of them `SkippedEntry`, and two live rows. The banner
  it drives says *five*. Meanwhile every `ParseFailed` arm of
  `canvas::parse` returns `Canvas::default()`, so the state the flag
  names can never carry a skipped entry at all. Both facts are visible
  in the committed `canvas_read.json` golden, which is why that artifact
  serializes the warnings rather than just their count.
- **The group boundary is superseded by the move, on BOTH hosts.**
  `CanvasGroupEntered` and `CanvasMovedTo` are both class `navigation`
  (0a-8), and every selection change announces the boundary and then the
  move in the same synchronous run — so inside the 200 ms window the
  move wins and the boundary line is dropped, never spoken. Mac's
  `announceMove` (`CanvasOutlineView.swift:404–432`) has exactly the
  same shape, so this is inherited parity, not a Windows defect. The
  class membership is pinned core-side and is not a host's to change.
  Recorded rather than worked around: CD-4's count rule is pinned on the
  PURE `GroupBoundaryEvent` (the mac `returnOpensRow` precedent) and the
  audible outcome is pinned separately by
  `TheMoveSupersedesTheBoundaryInsideTheCoalescingWindow`. **Worth an
  upstream look at close-out** — t0 §1.2 specifies group entry/exit
  narration, and neither host currently delivers it on the arrow path.
- **The §K row's mac reference number is the wrong benchmark.** The spec
  asks PR A to record its numbers "against the mac `5.62 ms` core path
  (the C# delta is marshalling only)". `5.62 ms` is
  `canvas_parse_derive_2000` — pure parse + derive, no persistence.
  `open_canvas` additionally runs an indexed `canvas_nodes` write inside
  `begin_fenced` and inserts into the session registry, and it measures
  **36.27 ms** here. The C# delta this suite actually isolates is
  **~4.6 ms** for all three projections behind one open. A like-for-like
  ratio needs a Rust bench of `open_canvas`, which does not exist;
  BENCHMARKS.md says so rather than printing a ratio that would mean
  nothing. (The measuring box is also a QEMU VM, not the bare metal the
  W2-x rows used — recorded in the same entry.)
- **The warning footer is wider than the banner, and gated on Ready.**
  Spec behavior 2 asks for "a focusable detail row in the outline footer
  listing `warnings`", and the banner's count is core's `skipped`
  parameter. Those are two different sets on the same fixture (eight
  versus five on `malformed.canvas`), so the footer lists every warning
  — a dangling connection is a fact about the user's file that nothing
  else in this PR reports — while the banner keeps the vocabulary's
  number. The footer renders only under `Ready`: a parse error's state
  message IS its single `ParseFailed` detail, so listing it below would
  say the same sentence twice.
- **`TreeViewItemAutomationPeer` has no `IInvokeProvider`.** It
  implements ExpandCollapse, SelectionItem and ScrollItem, and the
  spec's outline needs Invoke on both the node rows and the connection
  rows. The alternative — an invokable child element inside each row —
  is what the journeys' peered-elements-only trap forbids, so the item
  is a `CanvasOutlineItem` whose peer adds the pattern.
- **A `RenderedAnnouncement` overload was unavoidable, and the seam it
  created was not wired.** `AccessibilityNotificationDispatcher.Post`
  rendered internally, and a coalescer cannot use that: the window's
  winner is decided AFTER the render and the loser is dropped without
  ever being spoken, so the queue holds rendered lines.
  `Post(A11yEvent)` now delegates to `Post(RenderedAnnouncement)`, and
  the new seam takes the same `?? (_ => { })` default the event seam
  already had at each hop.

  **This bullet used to claim the seam was "threaded from `MainWindow`
  through the vault lifecycle to the workspace". It was not.** The
  production construction passed no `announceRendered:` argument at all,
  so the default no-op threaded the whole way and every canvas
  announcement died silently in the shipping app — while the entire
  suite stayed green, because every fact injects its own sink. That is
  the exact shape a default-to-harmless seam fails in, and no
  test-injected sink can ever catch it. Fixed, and guarded by
  `AnnouncementSeamCensus`, which reads the three SHIPPING call
  expressions — `MainWindow` → lifecycle → workspace → announcer — and
  a fourth fact that drives a canvas load through a real
  `AccessibilityNotificationDispatcher`. Mutation-verified: deleting the
  argument again fails the first of them by name.
- **The attach funnel's doc comment was one site behind, and now cannot
  be.** The controller ruling said four listed versus five real; that
  was correct (`Layout.cs:161`, the active-tab replace arm in
  `TryOpenItem`, was missing). Both the fix and the guard landed
  together, and the guard was mutation-verified: dropping `TryOpenItem`
  from the comment fails
  `TheAttachFunnelDocCommentNamesEveryCallSite` with that name in the
  message, and adding an `AccessibilityNotificationDispatcher` field to
  a `Canvas/` file fails `NoCanvasSourceAnnouncesOutsideTheRelay`.
- **`ChordScope.Canvas` has no scrape yet, and `ChordTableTests` says
  so.** `EveryScopeIsEitherScrapedFromProductionOrDispositioned` demands
  that a new scope be either scraped from a production key handler or
  dispositioned with a reason. PR A's three canvas commands carry no
  chord (rule R1: the switcher is a visible control and the palette is
  always a path), so `Reg` gives them `ChordScope.None` and the scope
  itself is dispositioned — PR C's navigator ships the first
  Canvas-scoped row and the scrape that checks it.
- **`dotnet format --verify-no-changes` is clean solution-wide** at
  every commit; no unrelated file was rewritten. The ten xUnit2031
  analyzer warnings the first cut of `CanvasDocumentTests` produced were
  cleared rather than left in the log.

**Fix round 1 (task review).** Three of the findings changed shipped
behaviour rather than only prose, and one of those was found by a test
written for a different finding:

- **The rendered seam was never wired** (above, in the amended overload
  bullet). Class: a seam whose every hop defaults to harmless is
  invisible to any suite that injects its own sink.
- **Removing a selected connection row dragged the model backwards.**
  Writing the arrow-onto-a-connection-row fact exposed it: WPF
  re-selects the parent container when the selected item is removed, and
  that arrived at the selection handler as a user action. The apply now
  runs wholly under the sync guard (A11). The reviewer's finding was
  that arrowing must not follow; the guard gap was underneath it and
  would have survived the narrow fix.
- **Three copies of the scheme allowlist** — panels, citation popover,
  canvas — while the contracts row said "shared". Now one
  `ExternalLinkPolicy`; the row says what is true (I4/A13).
- **Five §A citations named tests that did not exist.** Corrected, and
  `ContractsCitationCensus` now derives the check instead of trusting a
  re-read. The mechanical sweep also found the inverse hazard: §A cites
  plenty of real identifiers that live outside the C# tree (WPF, the mac
  twins), so the census lists those explicitly rather than loosening the
  rule until it passes.

**Fix round 2 (red team round 1).** Three blockers, and two of them are
the same class: a record that outran the code.

- **The `canvas_read` golden had no Swift producer**, and mac's
  `ParityHarnessTests` asserts golden↔produced name-set equality BOTH
  directions — so the mac lane would have failed deterministically on
  this PR's first CI run. A20's "the twin lands later" was copied from
  0a/0b, where "later" meant a later TASK inside the same PR. The twin
  is written (blind, against symbols verified one by one in the mac
  sources), and the same-PR rule is now stated in A20 for the rest of
  the series.
- **A17 claimed every FFI touch was scheduled; three were not**, and one
  of those held no lock. The lock is now universal and the contract
  carries the actual table. This is the 0a record-drift class, caught
  before codex rather than by it.
- **The history reload site did not reload a canvas** — a registry hit
  returned the document untouched, so the outline kept the pre-restore
  rows right after the shell announced the restore.
- **Image cards opened as Markdown.** `ItemForPath` calls every
  non-`.canvas`/`.base` extension Markdown, so activating an image
  replaced the canvas tab with an editor over the PNG's bytes. Mac
  routes on the TARGET, not the kind; the unreachable
  `CanvasOpenTarget.DefaultApp` arm was the tell that a whole branch was
  missing. (CD-36 records the stale mac hint that hid it.)
- **Focus was a side effect of publishing**, which stole it on retarget
  and never landed it on a registry hit — both halves of A14 wrong at
  once, one of them contradicting the guard comment beside the code.
- **The real coalescer had no test.** Every announcer fact flushed
  manually, so deleting `_timer.Start()` left the whole suite green.
  One fact now pumps a real dispatcher; mutation-verified.

**What generalises.** Four of these six are the same failure: a
mechanism whose absence is silent (a no-op default, a registry hit, a
manual flush, a deferred twin) tested only through a path that supplies
the mechanism. The guards added here — the seam census, the citation
census, the real-timer fact, the production-mode interleavings — all
exist to make absence loud.

**Also this round.** The History restore of a `.canvas` tab does not
complete end to end in the harness: `RequestRestoreVersion` stages the
history head hash as the CAS basis for a non-markdown tab and the
restore does not reach disk. Unresolved here and NOT PR A's to answer —
W4-7 owns that path, and PR A's obligation is that its own reload site
reloads, which is where the fact is pinned. Flagged for close-out.

**Fix round 3 (scoped re-review).** The round-2 fixes introduced three
of their own, which is the stopping rule's own warning shape — a fix
that creates the next round's finding counts double. None of these
created a NEW blocker class, but they are recorded plainly:

- **M1's media arm was a shell-execution hole.** Opening "anything
  non-Markdown" in its default app is right on mac and wrong on Windows,
  because `ShellExecute` executes. The gate (CD-38) is the fix; the
  lesson is that "match the reference implementation" is not a safety
  argument when the platforms' adjudicators differ.
- **The gate's own tests were stubbed.** The activation facts replace
  `OpenMediaCardFromSurface`, so they could never have seen a gate in
  the production closure — the same silent-absence shape as round 2's
  unwired seam, one layer down.
  `TheProductionMediaSeamOpensMediaAndRefusesEverythingElse` drives the
  real closure on both arms.
- **M2's addressing was still a broadcast.** One document serves every
  pane, so `RequestFocusLanding` reached every mounted surface and each
  landed. The request carries the asking tab now.
**Fix round 4 (scoped re-check).** Two test-integrity defects in round
3's own focus facts, both the same shape: a test that could not have
failed.

- **The two-pane fact was addressed to the wrong pane.** It asked for
  focus in B and asserted focus landed in B — but with the guard deleted
  BOTH surfaces land and B, subscribed second, wins the last word, so
  the fact passed either way. It addresses pane A now, the surface
  mounted first, which is the one a broadcast loses to. Mutation-verified.
- **`AnEmptyCanvasLandsFocusOnTheOnboardingRegion` had no
  `DataContext`,** so it was asserting on a shape production never
  builds; A14 cited it as a pin for behaviour it did not exercise. It
  runs through the workspace's open funnel now. The enabler is gone
  with it: `RequestFocusLanding`'s `owner` parameter lost its default,
  so an unaddressed call is a compile error rather than a convention —
  which is what turned the defect up in the first place, since removing
  the default broke exactly that one call site.

**Fix round 6 (scoped re-review).** B4 was half-fixed and the round-5
focus fix carried a regression.

- **Leaf-only resolution was bypassed empirically**: a directory
  junction inside the vault with an ordinary `.png` leaf. Junctions need
  no elevation, so the privilege argument never covered them. The walk
  resolves the whole chain now, bounded to the vault subtree — and the
  first attempt at that bound refused everything, because interrogating
  `C:\` throws and the fail-closed handler caught it. Both the bypass
  and the over-refusal are pinned.
- **The hardlink claim was false** and is withdrawn with the residual
  stated rather than implied.
- **The canvas early return stranded focus.** Seven dismissal routes
  reach `FocusEditorPane` as a last resort — their own comments say the
  fallback exists "rather than stranding focus on the window root" — and
  a bare return left them with nowhere. The canvas arm RAISES a request
  instead, so every route lands on the outline row.
- **The rename double-loaded**: the re-key's `CanvasDocumentFor` loads
  on a miss and the unconditional reload after it read the file again,
  speaking the degraded-load sentence twice. The reload is now skipped
  when the re-key just created the document.

- Two contract amendments from round 2 (A13's routing, A14's trigger)
  were **lost when their edit script aborted on a later assertion** and
  the file was never written — the code shipped, the record did not.
  Caught by re-reading §A against the diff in this round and reapplied.
  A script that edits a document in several places must write what it
  has before it can fail; recorded because the same shape would have
  silently dropped any of the other rows.

**Deferred to PR E (m5, ledgered, no action here).** `Rebuild()`
force-expands every group with members so a first read is the whole
structure, which means a republish discards a user's collapse. Nothing
in PR A republishes except a reload, so the cost is currently one lost
collapse per explicit reload; PR E's mutation funnel republishes on
every write, at which point expansion state has to be preserved across a
rebuild rather than reset. Recorded here so PR E inherits the decision
rather than rediscovering it.


### Task C-1 (the Windows navigator, mode stack, filter and Where-am-I)

- **Carried item 2 was already done, and saying so is the point.** The
  brief listed "the coalescing-class Swift tripwire (0b m1)" as an open
  carried item. PR 0b shipped it as contract 0b-17 and it passes; this
  task read it before writing anything. What re-reading it FOUND is that
  its own failure message assumes the Windows copy of the class list is
  faithful, which nothing checked — so the work that was actually
  outstanding is the symmetric twin, and that is what landed (C17). A
  carried item taken on faith would have produced a duplicate of the mac
  half and left the real gap open.
- **"Delivery must never mutate selection" is unreachable on WPF, and
  the brief's other half is the one that mattered.** `TreeViewItem`
  selects itself in `OnGotFocus` and a `DataGrid`'s currency IS its
  focused row; there is no focus-without-select on either control. The
  narration — the half a user can hear, and the half §B actually filed —
  is gone in both projections. CD-40 records the constraint, the two
  alternatives that were tried on paper and rejected (prefer the
  selection, which breaks WCAG 2.4.3; leave them divergent, which points
  every selection-scoped verb at a card the reader is not on), and the
  residue.
- **t0 §2 M4's palette clause contradicts t0 §2 M6.** M4 lists the
  palette among the departures that auto-cancel a mode; M6 requires
  every mode to be committable and cancellable from visible controls so
  Switch Control and Voice Control never depend on the keyboard. Commit
  Mode, Cancel Mode and the resize presets ARE palette commands, so
  implementing M4 literally ships three permanently dead verbs. Mac
  resolved this in red-team #521 and the Windows machine follows mac.
  Recorded as CD-41 and reported to the controller; the exclusion is one
  named arm of one total switch, so reversing it is a one-line change
  with a test that already enumerates every arm.
- **The arrow and the verb are not the same movement, and the fixture
  choice is what made that visible.** The first version of the arrow
  fact used the grouped board and failed at the boundary — because the
  last CARD is not the last ROW there: the selected card's connection
  rows sit under it, which is exactly what contract A11 put them there
  for. The fact now runs on the edge-free fixture and a SECOND fact
  pins the connection-row case deliberately, so the behaviour is
  recorded rather than being an accident of which canvas the test
  happened to open (CD-44).
- **A journey that passes in six seconds is not self-evidently running.**
  Round 7's lesson is this suite's, so the new journey was
  mutation-verified rather than trusted: breaking one assertion makes it
  fail in 4 s, and restoring it makes it pass in 6 s — the same envelope
  as the two canvas journeys already known to execute their bodies.
- **`NothingSpeaksAfterTheLastTabClosed` used to fail in DEBUG and pass
  in Release — and THAT EXCUSE COST A DEFECT.** It deliberately posts
  through a retired announcer, which contract A5 makes a `Debug.Fail`,
  and the test host turns that into an exception. The original
  disposition was "CI runs `--configuration Release`, where `Debug.Fail`
  compiles out, so the gate is green either way", verified at BASE so it
  would not read as a PR C regression.

  **That reasoning was wrong in the way excuses are wrong: it was true.**
  Release was green, CI was green, and a configuration nobody runs is
  where the next real failure lands. Two more facts joined the red set —
  both written by this PR, in the same shape, months of rounds apart —
  and neither was noticed. The third failure in that set was not a test
  artefact at all: `Commit` was announcing its confirmation into a
  retired announcer, which the funnel dropped, so every Release run and
  every behavioural assertion looked right.

  Repaired rather than re-excused. `DebugAsserts.Suppressed()` scopes the
  ONE deliberate call in each of the three facts — never a test body,
  never a production teardown — so a `Debug.Fail` raised by SHIPPED code
  still fails the run, and `CanvasAnnouncer.RefusedAfterShutdownForTests`
  makes "retirement composes nothing" assertable in Release too. **The
  standing gate is now Debug AND Release**, both on a
  porcelain-clean committed tree, with the tail line of each run quoted
  in the report rather than summarised.
- **Three build warnings are pre-existing on this branch**
  (`ModalSurfaces.cs:228` CS8524, `FilesSidebarViewModel.FileManagement.cs:1117`
  CS8604, `MutationHarnessCensus.cs:59` CS8620), all in files this task
  did not touch. `dotnet format --verify-no-changes` over the WHOLE
  solution also reports pre-existing whitespace in `WorkspaceViewModel.cs`
  and `ShellAccessibilityTests.cs` at lines this task did not edit — the
  recorded mixed-EOL trap, and the reason the DoD scopes the format gate
  to the changed files, which pass clean.
- **RULE 4 TRIPPED, as pre-declared, and this is the series' THIRD
  design pass** (after focus delivery and identity containment, both in
  PR A). Round 3 recorded the tripwire in advance: two subsystems at
  three rounds each, and another continuation in either stops the
  patching. Round 4 found one in each — the selection re-seat outside
  the publication, and teardown mutating while observers could still run
  — so the patching stopped and both were replaced by a primitive.

  The class, stated once: **any correlated observer-visible mutation
  outside the publication transaction is a second channel.** The
  enumeration (rows → state → controls → selection → next) can never end
  by enumeration, because its population is everything the document
  exposes. So `Publication` (travels) stages the writes and queues the
  notifications, and the outermost scope raises them in one defined
  order; every notifying member is read-only outside it; and
  `TheCanvasModelNotifiesOnlyFromInsideAPublication` (travels) is the census that
  keeps the population closed instead of a reviewer doing it one pair at
  a time.

  Teardown got the same treatment, because it had the same shape: each
  round found the NEXT fallible callback reachable while the document
  mutated on its way out. **Silence-first total finalization** — SPEAK
  (the sentences a retirement owes, while the funnel is open), then
  SILENCE as the first act of everything after, then RELEASE from a
  `finally`. The clears cannot run a callback because there is nothing
  left that can raise one, and `TeardownSpeaksThenSilencesThenReleases` (travels)
  reads that order out of the source.

  What made this a design pass rather than a fifth patch is the test
  shape: the two censuses assert over the POPULATION (every notifying
  write, the whole teardown order), and the behavioural facts drive the
  primitive rather than the pair that happened to be reported.
- **Round 5 adjudicated as MECHANICAL COMPLETION, not a design
  continuation — and that adjudication is on the record because it is
  the kind that gets made too easily.** The design held: codex verified
  the ordering and the throwing-Shutdown construction. What it found was
  the design not fully APPLIED — the marked set and the `SurfaceChanged`
  event still notifying outside the transaction — and a census whose
  population was a hard-coded field list in one file, which could not
  have seen either. Both are failures of application and of the check,
  not of the primitive, so the controller ruled completion rather than a
  fifth round of the class. **A round-6 breach of the DESIGN itself
  escalates to the user without further adjudication**, which is what
  keeps "mechanical" from being a word that can be reused.

  The lesson generalizes past this task: a census that closes a class
  must derive its own population. Ours now scans the directory, classes
  every notifying type as model, view or recorded exclusion, and fails on
  anything unclassified — so the next notifying member joins a side by
  decision instead of by being absent from a list nobody updated.
- **Round 6 ESCALATED to the user, as pre-declared, and the USER RULED:
  one boundary-complete wave, then codex round 7 as final arbiter — and
  if round 7 fails, PR C pauses for an architecture review.** The
  escalation was the promise round 5 made being kept: a breach of the
  design itself, not another application gap, so the decision left the
  loop. Codex's four blockers were taken as the finite spec for the wave,
  which is what "boundary-complete" means here — the boundary is what an
  observer can reach, and the wave extends the primitive to all of it
  rather than to the next reported instance.

  What the wave found, stated as the boundary rather than as four items.
  The transaction ordered everything that RAISES and said nothing about
  what a woken observer then READS: the activation targets, the subpath
  anchors and the adjacency memo were document fields installed by a
  reload before its rows were published, so a Where-am-I composed from a
  mid-publication wake described the new canvas's connections against the
  old canvas's outline. Those are fields of the unit now, and the sweep
  that found them also brought in `LastActivatedNode`, the last mutable
  field a publication-time reader could reach.

  Retirement had the same boundary problem twice over. It retired two
  channels of five, and asserted it with an observable that inspected the
  same two — a green light over the gap, which is worse than the gap. And
  the mode stack was left IDLE rather than terminal, so an `Enter` from a
  surface the shell had not finished tearing down would have started a
  mode on a document whose handle was gone. Both are closed: five
  channels retired, the observable over all five, and every mode verb
  refusing after `Shutdown` — silently, which is the never-silent table's
  precondition being absent rather than the table being broken, since a
  retired document has no surface and its announcer is already shut.

  And the census that closed the population was itself failing OPEN. It
  matched bases by direct name (a type one derivation down read as not
  notifying), knew only field-like events, saw one partial per type, and
  named two generic types for the collection scan and eleven fields for
  the assignment scan. All four are derived now — transitive and
  simple-name base resolution, both event forms, all partials merged, and
  the state scan resolved from the members that HAND IT OUT — and the
  discovery assertions count what the scan FOUND rather than what the
  method listed, so a scan that silently matched nothing fails instead of
  passing everything above it.

- **Round 7 failed, and the user ruled a SPLIT. This branch is C-lite.**
  The second user decision, and the one that ends the loop. Round 7
  found four blockers inside the wave — query-source identity, the Empty
  sentinel's shared memo, `HandleEscape`/`RegisterRung` un-gated, and the
  census blind to state handed out by METHODS rather than properties —
  so PR C paused as pre-declared and the path was decided outside the
  review loop rather than by another round.

  **What ships here** is the layer seven rounds verified clean: the
  navigator's verbs (the unconditional follow, the dead ends, the CD-48
  routes), the M1–M7 machine with ALL of its hardening (the closed
  transition, the drained departures on every exit, the M4 table with
  MenuOpen, the terminal shutdown — including round 7's ladder holes,
  which are cheap and in scope because the stack ships), the chord rows
  and their resolvers, Where-am-I with its panel and the panel-open
  Escape, the verbosity preference, the coalescing tripwire, the
  never-silent read gates, and every fact that pins them.

  **What travels** is the asynchronous filter and everything four rounds
  and two design passes built to correlate it: the projection unit, the
  publication transaction, silence-first teardown and their censuses.
  C10 carries the travel list and the interim's cost. The filter here is
  the SYNCHRONOUS pre-review form — the memoized view with its `Current`
  flag, querying under the lock on the dispatcher, PR A's own recorded
  precedent — and C10 states the lock-wait plainly rather than leaving it
  to be discovered.

  **The round record above STAYS, in full, and that is the point of
  splitting rather than reverting.** The redesign PR does not start from
  a blank page: it inherits seven rounds of findings, two design passes,
  a rule-4 trip and the two user decisions — and it inherits the reason
  the design was right, which is that a match returning on a later frame
  makes rows and answers separate facts. Nothing above this line was
  wasted; it was early.

  The generalizable lesson, recorded because it is the expensive kind:
  the async filter was one requirement inside a PR whose other twelve
  contracts had nothing to do with it, and it took the whole PR hostage
  for seven rounds. Sizing a PR by SUBSYSTEM rather than by requirement
  count would have caught that at the brief.

- **The spec's PR A evidence line for the "Accessible canvas (T parity)"
  surface row is still unexecuted.** It says the row moves to "in
  progress — PR A"; the generated matrix still reads `pending`. Left
  alone rather than fixed here: it is PR A's content and PR H's
  reconciliation, and §B12's backfill named the two rows it took and not
  this one. Flagged so it lands in one of those rather than being
  rediscovered.

---

## Round record

Per `24_red_team_protocol.md` §Per-round record.

**Red team round 1** (2026-08-22) — SAFE TO MOVE FORWARD; the finding
tally is in the report's own verdict line (`redteam-0a-round1.md`).
M1: 0a-10 over-claimed the card-reference collapse — the outline's
AX-label helper and the renderer's peer names survive, and 0a-10's scope
table now names each and cites §W-G row L. M2: the poster layer was an
unguarded canvas-funnel bypass
(`AppKitAnnouncementPoster` added to the funnel guard's scanned
symbols). m6 promoted to CD-14; m-F closed by making the guard's walk
recursive.

**Codex adversarial round 1** — NOT SAFE, blocker: the render arms
CD-15 tabulates hardcoded a plural while their payloads admitted
`count: 1` and an empty
`Vec`, and every lockstep place agreed with them because the corpus only
sampled plural values. Fixed as a class (0a-14, CD-15); boundary
witnesses added.

**Codex adversarial round 2** — NOT SAFE, blocker: the implementation
verified clean, but the enumeration table still carried the pre-fix
plural-only formulas, and 0a-14's witness claim was false for
`CanvasGrouped` and `CanvasMarksCleared`. Table rows corrected after a
re-scan of every row against the artifact; the missing witnesses added.

**Codex adversarial round 3** — NOT SAFE, blocker: 0a-14 conflated the
appended boundary ENTRIES with the ARMS lacking a witness — the trace
path contributes both a singular and an empty-collection entry, so the
two populations were never equal. Enumeration rewritten, then removed
entirely by round 4.

**Codex adversarial round 4 — PROTOCOL RULE 4.** Three consecutive
blockers in one subsystem: 0a-14's hand-written quantified prose. The
class was never the individual sentence — it was **prose making
hand-counted claims over a machine-enumerable corpus**, where each fix
corrected the named sentence and left the next miscount in place. Per
rule 4 the patching stopped and the invariant was implemented:
`canvas_count_speaking_arms_have_boundary_witnesses_and_agreement`
carries it (exhaustive classifier, boundary witnesses, agreement at the
boundary with the CR-3 carve-out allow-listed, source scan), and
0a-14's prose de-quantified to name the invariant and point at the test.
Round 4's other finding — that 0a-14's blanket "renders correctly at
0/1/n" contradicted CR-3 — is settled by the explicit exception
paragraph and the allow-list; CR-3 itself is not re-litigated.

Mutation-verified: deleting a count-one witness fails part 2;
re-hardcoding a plural fails part 3; the same mutation with the string
excused in the allow-list still fails part 4; a stale allow-list entry
fails the anti-rot assertion; and adding a variant to `CanvasA11yEvent`
fails to COMPILE at `spoken_cardinality`, which is the property the hand
list never had.

**Codex adversarial round 5** — NOT SAFE, blockers all inside round 4's
own design pass (the interim guard overclaimed; rule-5 double-count
noted in the ledger). The theme was **claims matching powers**: the
classifier's nested matches were not exhaustive and its witness rule was
blind to `CanvasMovedTo`'s Verbose-only clause; the "0/1/n" claim
asserted more than the boundary witnesses check; the CR-3 carve-out
matched on string alone, so a defect could move arms unseen; the source
scan missed `\`-continued literals and bare plural arguments; and the
round-record's own mutation tally was a hand-counted number that
disagreed with its list. Each is fixed at the level of the power rather
than the sentence — nested exhaustiveness, value-level classification,
zero witnesses where a host reaches zero, (arm, string) provenance,
continuation-joining plus bare-literal detection, and the guard's
residual classes named in 0a-14 rather than implied away.

**Codex 0b rounds 1–3 — PROTOCOL RULE 4 (second invocation this
programme).** Three consecutive rounds of blockers in one subsystem: the
VA/reopening family. Round 1 found `.loading` bypassing VA-1 entirely
(plus a `try?`-flattening compile break and an auto-side pair read
twice); round 2 found the state census drifting — "four cases" where
`LoadState` has five — after round 1 had just fixed a membership list;
round 3 found the merged test asserting handwritten per-state
expectations while the filter family announced `.reopening` in every
non-ready state.

**The class was never the sentence.** It was *VA membership and
per-state responses maintained as handwritten lists in four places* —
the same shape as 0a's round-4 finding (hand-quantified prose over a
machine-enumerable corpus), and each round's fix corrected the named
list and left the next copy of it standing.

Per rule 4 the patching stopped and the invariant was implemented:
`canvasReadRefusal(for:)` is one total mapping over `LoadState`; every
member reaches it through `canvasReadContext(for:)`, whose return value
is the proof it said yes; the filter family routes through it; the test
takes its expectations FROM it rather than restating them; and
`every_mac_canvas_read_is_gated_or_named` derives membership from the
Swift source, failing when a canvas read query is called from a
function that neither routes through the mapping nor appears on a named
exclusion list with a reason.

Mutation-verified: an ungated read verb fails the guard by name; an
exclusion that stops calling a query fails the anti-rot assertion. Both
were observed failing and reverted.

Round 3's non-membership findings landed with it: rustc's
string-continuation escape skips blank lines and CRs, not just
indentation (the decoder now matches, with codex's multi-newline probe
as a witness, mutation-verified both ways); the surviving "four"
counts became names; and the SE-0230 sweep's zero became true rather
than a property of its closure.

**Codex adversarial round 6 — the final strengthening wave.** NOT SAFE.
`ZERO_REACHABLE` recorded that no bulk verb can reach zero, and codex
traced one that can: create selects the new card, undo removes it
without reconciling `selection.selected`, and Duplicate's seed passes
its non-empty guard while the collection it ANNOUNCES resolves to
nothing — `Duplicated 0 cards`. The root cause is general and was
re-audited per verb: reachability had been argued from the guarded seed
while the payload speaks a later, filtered collection. The witness was
added, its reason records the chain, and the mac behaviour behind it —
a stale selection surviving undo — is filed in "Mac details recorded
while reading" as an upstream bug, not this PR's to fix. Two further
findings were fixed by raising the power: the classifier now
destructures every parameter type that can gain variants — `..` elides
only variant-fixed types (`String`, `u32`, `bool`, `Vec<String>`, and
`Option` of those) — and comments are stripped before the source scan so
a trailing `// plural(` cannot vouch for a hardcoded plural. The third was fixed by RETRACTION —
the parts-3-and-4 independence claim is withdrawn rather than scoped,
because provenance in a lexical scan is line-wide and cannot support it.
**By decision, scan residue past this point is 0b's parser**; no further
lexical strengthening is in scope for 0a.

### PR A — Codex adversarial round 1 — NOT SAFE, 5 blockers + 1 major

Blockers B1/B2/B6 all landed on focus and selection delivery, and that
made **four consecutive rounds** with a finding in the same behaviour
(red-team I2's connection-row follow, M2's retarget theft and
registry-hit gap, round 4's two facts that could not fail, and now
codex's three). **Stopping rule 4 applied**: one design pass instead of
three more point fixes — written up as the rewritten **A14**, which is
now the design's contract rather than a description of the code.

The class, named: **edge-triggered delivery with no durable state,
tested by supplying the trigger.** Focus was delivered at the instant
something fired, to whoever happened to be subscribed and in the right
state, and every fact called the delivery method itself. Three
independent things can make that instant wrong — the surface is not
mounted, is not visible, or its virtualized container does not exist —
and none of them is observable from the document. Each round fixed the
instance and left the shape. A14's rewrite replaces it: the request is
state, every surface retries on every condition that can change the
answer, realization is part of delivery, only a realized container
counts as delivered, and one authority owns focus per tab kind.

Point fixes, each with its own contract amendment: **B3** the rename
DESTINATION reloads (A1); **B4** containment becomes physical and the
closure fails closed (CD-38); **B5** the announcer is retired with its
document (A5).

**Every guard in this round is mutation-verified**: removing the open
funnel's canvas request fails three integration facts; restoring the
tree-focus fallback fails the realization fact; deleting MainWindow's
early return fails the source guard; dropping the announcer's
shut-down flag fails the late-post half of the lifecycle fact; deleting
the media gate opens an `.exe`.

### PR A — Codex adversarial round 2 — NOT SAFE, 2 blockers + 2 majors

All four valid; none re-opened a design, so point fixes (the focus
design pass held — codex found no new instance of the delivery class,
which was round 1's rule-4 subject).

- **B1 (TOCTOU in the media gate).** The gate resolved a path string and
  `ShellExecute` re-resolved the namespace later, so an in-vault-write
  attacker — exactly this gate's threat model — could swap a checked
  directory for an outward junction in between. Narrowed both ways the
  ruling named: resolution is through an OPENED HANDLE
  (`GetFinalPathNameByHandle`) and the launcher is handed the
  fully-resolved terminal path, and containment is revalidated
  immediately before `Process.Start` with nothing in between. The
  irreducible residual (a path-taking launcher re-resolves; closing it
  needs a handle-based launcher) is recorded in CD-38 with its
  precondition and future-work shape — the same claims-match-powers
  doctrine as the hardlink residual. The handle resolution also replaced
  the hand-rolled ancestor walk, which was an approximation of exactly
  that call.
- **B2 (focus retry incomplete under DataContext churn).**
  `DataContextChanged` joins the retry triggers: a presenter rebinding
  A→B with an identical shared `Model` never fired `OnModelChanged`, so a
  request for B stranded. Pinned.
- **Major-1 (drive-root vault rejected all media).** The `root +
  separator` containment prefix produced `C:\` for a drive-root vault.
  Replaced with `GetRelativePath`, which is root- and case-correct;
  pinned by a nine-case predicate theory, mutation-verified. (The
  end-to-end SUBST route could not test it — `GetFinalPathNameByHandle`
  collapses a SUBST drive to its real path — so the predicate is tested
  directly, which is the honest discriminating shape.)
- **Major-2 (round-6 guard false-green, the supplies-its-own-mechanism
  class a THIRD time).** The dismissal fact called `RequestFocusLanding`
  itself and never touched MainWindow's arm; the census proved only the
  early return. The census is two-sided now — early return AND the raise
  — and mutation-fails when the raise is removed with the bare return
  left; the delivery half is a separate fact. This class has now appeared
  in rounds 4, 5 and 6/7; the standing rule for the rest of the series:
  a guard may not exercise the mechanism it is guarding, and where the
  production seam is unreachable in-process it is pinned two-sided in the
  source.
- **Residuals recorded** (codex-verified, not holes): an ADS-syntax leaf
  satisfies the extension gate with no escape/execution path found; UNC
  and device-namespace forms fail closed. Both in CD-38.
  > **Example corrected (round 6 hygiene):** the leaf named here and in
  > CD-38 was `photo.png:stream`, which the last-dot parser actually
  > REFUSES (it reads `png:stream` as the extension). The accepted shape is
  > an ADS whose stream name ends in a media extension, e.g.
  > `photo:cover.png`. See CD-38 for the verified table.

### PR A — Codex adversarial round 3 — containment class ended (1 blocker + 1 major)

Containment findings in codex rounds 1, 2 AND 3 — the same subsystem
three times running, which is the stopping-rule-4 shape a SECOND time in
this PR (the first was focus delivery). Codex named the class precisely:
**filesystem identity reduced to path text**, where two normalization or
case rules on the same string disagree. Every prior fix was another path
predicate; this round replaces the substrate.

- **B1 (two sub-defects).** Verifying `C:\vault.` and launching a string
  ShellExecute renormalizes to `C:\vault` was a launch-integrity bug —
  fixed by keeping the handle-resolved EXTENDED (`\?\`) form end to end.
  TOCTOU-by-path-text was fixed by revalidating FILE IDENTITY
  (`BY_HANDLE_FILE_INFORMATION`: volume serial + file index) captured at
  check and re-compared immediately before launch. The residual shrinks
  to the re-open→ShellExecute gap and is recorded.
  > **Superseded:** the identity primitive named here is no longer the one
  > shipped. Round 4 replaced it with the 128-bit `FILE_ID_INFO` (ReFS
  > safety) and round 5 deleted the `BY_HANDLE_FILE_INFORMATION` path
  > entirely — see the round-5 entry and CD-38.
- **Defect 3 (case-sensitive-directory sibling).** The `GetRelativePath`
  OrdinalIgnoreCase prefix falsely accepted an adjacent case-different
  directory under per-directory case sensitivity. Ended by identity: the
  ancestor chain is compared by `(volumeSerial, fileIndex)`, so an
  adjacent directory is a different object. The feature needs admin, so
  the E2E is a bounded/manual residual and the rule is pinned
  reproducibly by a junction-rooted vault (identity accepts, text prefix
  rejects), mutation-verified.
- **Major (driveless folder-mounted volume).** `GetFinalPathNameByHandle`
  default DOS name returns `ERROR_PATH_NOT_FOUND` for a driveless volume,
  refusing all its media. `VOLUME_NAME_GUID` fallback added; the
  primitive is pinned, the E2E (needs admin) is manual.

**The identity-based resolution ends the class:** containment,
revalidation and launch are all OS file identity now, not path text.
Where a filesystem feature needs privilege to create (per-dir case
sensitivity, driveless mount, the TOCTOU race), the identity PRIMITIVE
is pinned directly and the E2E recorded as manual — the standing
false-green rule, applied honestly rather than by manufacturing a
passing E2E that does not exercise the feature.

**Every fix mutation-verified:** identity revalidation (swap → refused),
`\?\` retention (strip → trailing-dot launches the sibling), identity
containment (text prefix → junction-rooted vault refused), and the
identity primitive itself (distinct objects → distinct identities).

### PR A — Codex adversarial round 4 — containment's 4th round, converged (3 blockers)

The FOURTH consecutive codex round with a containment finding — but not a
new instance of a closed class. Round 3 moved the substrate from path text
to OS file identity; round 4 found that the identity implementation itself
still had three fail modes. All three are fixed and, per the pre-declared
stopping rule for this subsystem, the identity/snapshot logic was then
swept end to end for any further fail-open and none remains.

- **B1 (snapshot coherence — fail-OPEN).** Containment and the identity
  CAPTURE were two separate opens: containment resolved through one handle,
  then the check identity was captured by RE-OPENING the resolved path. A
  swap between those two opens made the captured identity the OUTSIDE
  object, and revalidating outside-against-outside passed. Fixed by fusing
  capture into containment: the leaf is opened ONCE, its identity and
  resolved path come from THAT handle, and every ancestor handle up to the
  vault root is opened and HELD simultaneously — one coherent snapshot, and
  revalidation re-checks the containment handle's own identity. The
  sub-window is closed by construction. It cannot be driven by an
  unprivileged in-process race (the swap would have to land inside a single
  method's handle-held region), so the property is pinned STRUCTURALLY by
  `CanvasMediaGateCensus.TheSnapshotCapturesIdentityFromTheHeldHandleNotAReopen`
  (`ResolveContained` reads identity only off held handles, never re-opens
  by path), mutation-verified against reinstating the re-open; the
  swap-during-capture E2E is a manual residual. Per the coordinator's
  instruction, no passing race test was manufactured.
- **B2 (128-bit identity for ReFS — fail-OPEN).** `nFileIndex` is not
  unique on ReFS, so the 64-bit identity could call two different files the
  same. Switched to `GetFileInformationByHandleEx(FileIdInfo)` →
  `FILE_ID_INFO` (64-bit volume serial + 128-bit file id); the 64-bit
  `BY_HANDLE_FILE_INFORMATION` index remains only as a pre-Windows-8
  fallback, which never meets ReFS. Pinned by
  `IdentityIsThe128BitFileIdInfoNotThe64BitIndex` (the 128-bit class is the
  one taken on a live handle, mutation-verified against a wrong class value)
  and `TheLegacy64BitFallbackAlsoDistinguishesFiles`.
  > **CORRECTED BY ROUND 5.** The claim above — that the retained 64-bit
  > arm was a pre-Windows-8 capability selection — is FALSE. The arm was
  > per-CALL: it triggered on any failure of the primary query and
  > downgraded that individual read to the non-unique index, a fail-open on
  > ReFS. The scoped re-review that endorsed this wording did not catch it
  > either. The fallback is deleted in round 5 and
  > `TheLegacy64BitFallbackAlsoDistinguishesFiles` is replaced by
  > `IdentityQueryFailureRefusesRatherThanDowngrading`. This entry is left
  > standing, with its error marked, because the record of what was claimed
  > is part of the record.
- **B3 (depth cap — fail-CLOSED availability).** The `ResolveRounds=64`
  reparse-cycle bound had been mis-applied to the lexical ParentOf walk,
  refusing valid media more than 64 directories deep. Removed: the walk
  strictly shortens and terminates at the volume root, so a
  fixed-point/shortening guard suffices. Pinned by
  `MediaSeventyDirectoriesDeepStillOpens` (70 levels deep, mutation-verified
  against reinstating a cap) and `TheAncestorWalkCarriesNoDepthCap` (the
  symbol is gone).

**The sweep for further fail-opens found none.** A mixed 128-vs-64-bit
comparison of different files differs in the high half and fails CLOSED;
UNC and `\\.\`/`\\?\` device forms resolve to a volume whose identity chain
never lands under a local vault root, and fail CLOSED; an unopenable
ancestor fails CLOSED; and a final-path buffer too small is grown and
re-read rather than refusing a legitimate long path (the availability
sibling of B3). **The single remaining residual** is exactly the
launch-time re-resolution: `ShellExecute` re-opens the verified path BY
NAME, which a path-taking launcher cannot avoid; CD-38 now states that as
the one residual and no longer implies the check→capture gap is covered.

**Stopping rule honoured.** This was the pre-declared final codex round for
containment; it converged (three concrete fixes, a clean sweep) rather than
surfacing another instance of a closed class, so the loop ends here rather
than escalating the design decision to the user.

### PR A — Codex adversarial round 5 — 1 blocker, introduced by round 4's own fix

Genuine, and mine: round 4 replaced the 64-bit identity with the 128-bit
`FILE_ID_INFO` but RETAINED the old one as a fallback, and this document
described that fallback as a per-host capability selection confined to
pre-Windows-8. It was per-CALL. Any transient failure of the primary query
— not an old OS, just an error — downgraded that single read to the
ReFS-non-unique `nFileIndex`, i.e. a fail-open that fires exactly when
something is already wrong. Strictly worse than having no fallback.

**Controller ruling: strongest form — delete it.** `IdentityOfHandle` is
now `FileIdInfo` or `null`; the `GetFileInformationByHandle` P/Invoke, its
`BY_HANDLE_FILE_INFORMATION` struct and the legacy test seam are gone.

**The rationale, corrected in the micro-round that followed.** My first
write-up justified the deletion by minimum OS — that .NET 10 WPF's Windows
10 1607 floor postdates `FileIdInfo`'s Windows 8 — and concluded the
deletion cost nothing. Wrong KIND of argument: `FileIdInfo` depends on the
FILESYSTEM, not the OS version, and FAT32/exFAT and some redirectors and
virtual filesystems do not answer it. So the deletion DOES have a cost:
supported media-open volumes are NTFS/ReFS, and on any other volume every
media open now refuses audibly — a fail-CLOSED availability regression
against round 4, recorded in CD-38 rather than glossed. Accepted
deliberately, because the alternative is the fail-open just killed.

- **The failure-injection fact.**
  `IdentityQueryFailureRefusesRatherThanDowngrading` injects a
  primary-identity-query failure into the containment flow and pins that
  the primitive, `ResolveInsideVault` and `OpenMediaInVault` all refuse and
  nothing reaches the shell — with a before/after premise so a broken
  fixture cannot pass it. Mutation-verified: reintroducing ANY fallback arm
  makes the injected failure resolve successfully again, failing both this
  fact and `TheGateHasExactlyOneIdentityMethod`.
- **The structural pin.** `CanvasMediaGateCensus.TheGateHasExactlyOneIdentityMethod`
  requires the legacy symbols ABSENT rather than merely unreached
  (dormant dead code is how this returned), two-sided against the surviving
  `TryGetFileIdInfo` so deleting identity altogether cannot satisfy it.
- **Sweep — no other per-call downgrade.** Recorded in CD-38: the
  final-path buffer growth retries the SAME method, and the DOS→GUID step
  is the same call asking for a different spelling of the same object.
  Neither weakens a primitive; nothing else in the gate does either.

**On the record.** Round 4's entry above is left in place with its error
marked rather than rewritten, and the fact that the scoped re-review
endorsed the wrong claim is recorded with it. The claims-match-powers
doctrine applies to this document too: a fallback described as narrower
than it was is the same defect class as a guard described as stronger than
it is.

**Stop condition.** The round-4 stop was pre-declared for a FIFTH
containment fail-open. The controller adjudicated it unmet in spirit —
deleting dead code that serves no supported platform is not a product
decision, and codex specified the closure shape rather than disputing it —
so the loop continues to a scoped re-review and codex round 6. Recorded in
the ledger and reversible until the user says otherwise.

### PR A — Round 7 — the outline's Invoke never reached assistive technology

Found while fixing a CI failure, not by review. The
`CanvasSurfaces_OutlineTreeSelectionAndActivation_AreClean` journey had
never once executed its assertions: `DemoVaultCanvasDirectory()` walked up
to `Cargo.toml`, and the shell a11y gate runs on downloaded binaries with
no checkout, so it threw "repository root not found" in 4 ms on every run.
With the fixture lookup repaired (linked `Content` items read from
`AppContext.BaseDirectory`, the mechanism `CitationStyleFixture` already
proved — and whose comment already warned about exactly this mistake), the
journey ran its assertions for the first time and immediately failed on
`Invoke`.

**The defect.** WPF projects a TreeView row into the UIA tree as a
`TreeViewDataItemAutomationPeer`, not as the `TreeViewItem`'s own peer.
That data peer implements SelectionItem, ExpandCollapse and ScrollItem
itself and does NOT forward a custom pattern to the container peer. The
`Invoke` this PR added to `CanvasOutlineItemAutomationPeer` was therefore
invisible: an in-process peer walk showed `invoke=NULL` on every row while
`sel=OK exp=OK`. Contract A8's activation pattern was absent from the only
surface assistive technology reads. Fixed by overriding both item-peer
factories to build `CanvasOutlineRowDataPeer` (see A8).

**The false-green class, FOURTH instance — and the one that matters
most.** `TheTreeItemsCarryTreeSelectionItemExpandCollapseAndInvoke` passed
throughout by calling `CreatePeerForElement(container)` and interrogating
the CONTAINER peer — an object no UIA client ever sees. It constructed the
mechanism it was meant to be checking, exactly like the three earlier
instances (the unwired seam behind injected sinks, the gate behind a
stubbed closure, the focus guard calling `RequestFocusLanding` itself).
The fact now walks `treePeer.GetChildren()` and asserts on the projected
row peers at both nesting levels; both halves are mutation-verified.

**Codex round 6's SAFE TO MERGE predates this journey's first execution.**
That verdict was reached while the only test capable of catching this
defect was dying in setup — so it was never evidence about the outline's
UIA surface. Recorded plainly because the review ledger should not read as
though the gauntlet cleared something it never examined.

**A third defect, found and NOT fixed in this round (reported, awaiting
scope).** With Invoke working the journey advances to its surface-switcher
assertion and fails deterministically there. Verified in-process: the UIA
tree exposes `CanvasShowOutline/Table/Visual` flattened directly under
`CanvasSurface`, with **no `CanvasSurfaceSwitcher` node at all** — the
switcher is a bare `StackPanel`, which WPF gives no automation peer, so
its AutomationId and its "Canvas view" name never reach a client. Contract
A18's "one named group" is not met. Same root class as the Invoke defect:
`AutomationProperties` set on an element WPF never peers. Not fixed here
because it is production a11y behaviour needing its own contract evidence
and mutation-verified test. *(Fixed in round 8, below, along with the
class-wide sweep and the census that ends it.)*

### PR A — Round 8 — the class ended: inert a11y properties

The switcher defect reported above is fixed, and — the point of this round
— the CLASS behind it is closed rather than the instance.

**The fix.** The switcher is an `AutomationNamedGroupPanel`, the shared
peered-container idiom from `AutomationLandmark.cs`. It exposes
`ControlType.Group` carrying the "Canvas view" name and the
`CanvasSurfaceSwitcher` AutomationId, with the three radio buttons as its
UIA children. Verified in-process before and after: the tree went from
`CanvasSurface | .CanvasShowOutline | .CanvasShowTable | .CanvasShowVisual`
(flattened, no switcher) to
`CanvasSurface | .CanvasSurfaceSwitcher[Group]name='Canvas view' |
..CanvasShowOutline | ..CanvasShowTable | ..CanvasShowVisual`.

**The sweep.** Every `AutomationProperties` usage across all canvas
surfaces was enumerated and its target's element type resolved. There is
no XAML under `Canvas/` (CD-31 builds these views in code), so the sweep is
the five `.cs` files. Result: **one** peerless target — the switcher — and
every other site already sat on a peered type (`TreeView`, `UserControl`,
`TextBlock`, `TextBox`, `ListBox`, `RadioButton`, and a `Style` targeting
`TreeViewItem`). Nothing was deleted as decorative; the full hit list and
disposition are in the task report. The in-process peer walk corroborates
the static sweep — every AutomationId in the canvas surfaces now resolves
to a real node.

**The census that ends the class.** `CanvasAutomationPropertyCensus`
asserts structurally that no `AutomationProperties.Set*` in the canvas
sources targets an element type without an automation peer, with peered
types allow-listed BY NAME so a new element type is a conscious decision
rather than a silent pass. It is deliberately fail-closed — a target whose
type it cannot resolve is a failure, not a skip, because a blind spot here
is indistinguishable from the bug. It also carries a floor on the number
of sites scanned, so a refactor that moved these calls elsewhere cannot
leave it passing over nothing. Mutation-verified twice: reverting the
switcher to a `StackPanel` fails it on both property lines, and adding a
name to any other bare panel fails it naming that line.

*(One false positive was caught and fixed while writing it: a file-wide
identifier map let `SetBanner`'s `string text` parameter shadow
`BannerText`'s local `TextBlock text`. Resolution is scope-aware now —
locals, then parameters, then fields. A census that cries wolf gets
suppressed, which would cost more than the bug it guards.)*

**Three defects, one journey, none catchable before it ran.** The
`CanvasSurfaces_OutlineTreeSelectionAndActivation_AreClean` journey had
never executed a single assertion: its fixture lookup walked up to
`Cargo.toml`, and the gate runs on downloaded binaries with no checkout,
so it threw in 4 ms on every run since it was written. Behind that setup
failure sat (1) the fixture lookup itself, (2) the outline's Invoke
sitting on a peer no client reads, and (3) the switcher's inert
properties. Each was found only by fixing the one in front of it. The
lesson recorded for the rest of the series: **a journey that has never
reached its assertions is not evidence of anything**, and a green gate
containing one is green about the setup, not the behaviour. Codex round
6's SAFE TO MERGE was reached in exactly that state.

**The false-green tally, closed out.** Instances 1–3 were tests that
supplied their own mechanism (an unwired seam behind injected sinks, the
media gate behind a stubbed closure, a focus guard calling
`RequestFocusLanding` itself). Instance 4 was the Invoke fact
interrogating a container peer no client sees. Instance 5 is this one —
properties that reach no client at all. Instances 4 and 5 share a root
that the earlier three did not: **the assertion targeted a real object,
but not the object the consumer reads.** That is what the two new censuses
guard, and it is the form to watch for in PRs B–E.

### PR B — red team round 1 — NOT SAFE, 2 blockers + 1 major + 6 minors

Both blockers were **record-accuracy** defects with no behavioral change
forced; the implementation came through all nine attack surfaces clean.
That is the shape this document was built to catch, and it caught it.

- **B-1 — the comparator bound was wrong for the SECOND time.** B3's
  replacement claim ("both walk code points in order… they can disagree
  ONLY in the supplementary planes") was false of the reference
  implementation: Swift's `String` ordering is defined over Unicode
  canonical equivalence and normalizes before comparing, so the two
  comparators diverge on any `target` differing in NORMALIZATION FORM —
  an NFD `Café.md` sorts opposite to `Caff.md` across the hosts, every
  character inside the BMP. Reachable with ordinary data (macOS hands
  back decomposed filenames). Two corrections to one paragraph is the
  rule-4 shape, so the third version is written from the reference
  implementation's documented semantics **with the source cited in
  line**, states BOTH divergence classes, and registers the residue as
  **CD-39** instead of bounding it away. The ratified ordinal choice
  stands; only the recording was wrong.
- **B-2 — §B claimed a journey leg that did not exist** ("the disabled
  row actions"). Fixed by adding the leg, not by narrowing the sentence
  — see the tests paragraph above for why that was the honest direction
  and what it cost.
- **M-1 — `DeliverFocus`'s "the seat is SILENT" comment was false for a
  reachable arm.** Seating currency raises `CurrentRowChanged` outside
  the sync guard, so a request landing on a node other than the current
  selection reaches `SelectNode` and the document narrates. Corrected in
  the comment. **The BEHAVIOR is not PR B's to change**: it is A14's,
  the outline drives the identical path through its own selection
  binding, and a table-only fix would make the two projections behave
  differently on the same request. **Filed for PR C**, which adds the
  first production caller that passes a `NodeId` and therefore widens
  the reachable surface: decide whether a delivery to a node other than
  the selection should narrate (t0 §1.5 doubling against A14's landing
  rules), then pin it in BOTH projections — the reviewer's named missing
  fact is `AFocusDeliveryToANodeOtherThanTheSelectionDoesNotDouble`, or
  a recorded decision that it SHOULD narrate.
- **Minors folded:** m-1 (the hand-counted "18 facts" in the §W-C row —
  deleted, per 0a's rule-4 lesson that counts live in tests, not prose);
  m-2 (the collation-dependent sort expectations now pin the culture the
  production comparator reads, so an exotic host cannot report a defect
  that is not one); m-3 (the muted-until-attach seam had no witness —
  `AGridWithoutADocumentNeverPostsThroughTheSubstratesDefaultSeam` now
  covers both the unattached and the DETACHED arm, the second being the
  one with teeth: a grid still holding a retired document's relay would
  post to a shut-down announcer, which A5 makes a `Debug.Fail`);
  m-6 (`ToolTipService.ShowOnDisabled`, one substrate line).
- **Minors recorded, not fixed:** m-4 — the generator's evidence
  validation allows a command to reference ANY existing group and checks
  markers by substring; pre-existing (W5-x), `canvasSurfaces` added under
  the same strength, closed by PR H's matrix pass or the next PR that
  edits that file for its own reasons. m-5 — while the table shows, the
  COLLAPSED outline still runs its `ApplySelection` on every row move,
  so switching back can show group expansions the user never made there;
  it belongs to the ledgered expansion-state-preservation decision PR E
  owns, and is recorded here so it lands in that decision rather than
  being rediscovered.

### PR B — codex adversarial round 1 — NOT SAFE, 2 blockers

Both were A14 properties the outline had and the table did not. One was
real and is fixed; the other did not reproduce and its proposed fix
would have introduced a defect, so it is recorded with the measurements
instead.

- **B1 — the focus request was consumed by a grid-level fallback.**
  Real, and the same class A14.3 was rewritten over on the outline:
  `SelectRow` reported bound-set membership, `FocusCellElement` fell back
  to focusing the GRID, and `DeliverFocus` asked only
  `IsKeyboardFocusWithin` — true for that fallback, and true again
  whenever the reader was anywhere in the grid already. So a request for
  a row that could not be realized was marked delivered while the reader
  never reached it, and nothing retried. **Fixed at the seam**, where the
  outline's equivalent already is: `FocusCellElement` returns whether the
  REALIZED CELL took focus (the grid fallback returns false, and the
  callers that only want the reader's position still ignore it);
  `SelectRow(moveFocus: true)` returns that instead of set membership,
  with the split documented on the method; `DeliverFocus` is exactly that
  bool. Realization joins delivery: the substrate raises
  `ContainersRealized` off its generator (posted at Background priority —
  the outline's recorded re-entrancy trap), the table re-raises it and
  the surface retries, so a request for a virtualized-away row survives
  until the panel makes the container.
  `SeatingTheReaderOnAnUnrealizedRowReportsFailureNotSuccess` pins the
  seam and `AnUnrealizedRowLeavesTheRequestPendingUntilItCanBeDelivered`
  the end to end, on the last row of the 2,000-node fixture.
  Mutation-verified: restoring the fallback-accepting form fails both.
- **B2 — the currency/focus split did not reproduce, and the fix was
  harmful.** See B5's own paragraph for the measurements. Summary: WPF
  moves focus with currency while the DataGrid holds focus, so the
  reader, currency and the shared selection end the namesake path on the
  same row — measured with the proposed fix's precondition holding, so
  it would have fired and changed nothing. Meanwhile
  `IsKeyboardFocusWithin` on this control includes the
  separately-focusable summary region, so the proposed re-seat pulls a
  reader off the summary onto a row (measured). The finding is now
  guarded from the other side:
  `ARepublishNeverYanksTheReaderOffTheSummaryRegion` fails if the
  re-seat is ever added. **Recorded as a stop point rather than
  implemented**, per the standing rule that a fix must not be
  manufactured to satisfy a mutation that does not fail.

### PR C — task review — Approved, 3 Importants + 6 minors

Written into this document because it had only ever lived in the SDD
ledger, which is git-ignored and one `git clean -fdx` from gone. Every
round below is reconstructed from that ledger and the task report; the
sequence is the redesign PR's inheritance, and the reason the split was
a split rather than a revert.

- **I1 — opening a MENU cancelled the mode**, which is CD-41's failure
  one surface over: this PR's own Canvas menu carries Commit Mode and
  Cancel Mode, so opening it killed the two items the reader came for.
  Fixed as a named `MenuOpen` arm of the M4 table (a `MenuBase` ancestor
  walk), not a condition at one site — PR E's and PR F's context menus
  inherit it.
- **I2 — the M8 boundary was unrecorded.** Rung 3's card-detail Escape
  is a read-only interim; PR E owns M8, where Escape COMMITS. One
  paragraph, so the two are not read as the same rung.
- **I3 — the filter's FFI ran under `_ffiLock` on the dispatcher.** The
  review preferred a scheduler publish; the accepted-risk fallback was
  evidence-backed. Taken as the scheduler publish — **and this is the
  decision the next six rounds were about.**
- Minors 4–8 taken; minor 9 deferred to PR H. Parity, M1–M8 letter
  conformance, the never-silent table and the preference schema were
  verified independently against mac.

### PR C — red team round 1 — NOT SAFE, 1 blocker + 3 majors + 12 minors

- **B1 — Escape with the reader INSIDE the Where-am-I panel** destroyed
  a typed needle and left the panel open. Asking Where-am-I while
  filtering is the designed use — the readback carries the filter
  clause. RULED panel-first. The first fix keyed on FOCUS; the scoped
  re-review found mac keys on the panel being OPEN, so the same defect
  survived one arrangement over. Re-ruled to the open-panel key, which
  turned CD-47 from a divergence into an AGREEMENT. **CD-47's own
  lesson is the durable one: a t0-vs-reference adjudication is swept per
  CONTRACT, not per site** — M5 lists rungs and says nothing about
  transient regions, because §1.4 and §3 added them later without M5
  being revisited, and an absence trips nothing.
- **M1 — the spec's "connection-follow when the card has connections,
  else tree semantics, as mac does" describes a blend mac never
  shipped.** Mac follows unconditionally and always answers. The blend
  left a keypress on a connectionless leaf silent — the never-silent
  rule broken by a precedence nobody had. CD-48; all three expand routes
  (Enter-on-group, numpad `+`/`-`, the `ExpandCollapse` pattern) DRIVEN
  rather than claimed.
- **M2 — a tunnelling Enter ran ahead of every focused control**, so
  Enter on the visible Cancel Mode button would have committed the mode.
  First fixed with a list of control types; re-ruled to R2's own
  question — does a PROJECTION own the keys — because the list was the
  brittle half and the one control nobody remembered would re-open it
  silently.
- **M3 — a commit that cannot apply now keeps the mode**, modelled as an
  OUTCOME rather than mac's call-site pre-gate so no entry point can
  forget it.
- Verified clean under hostility: the filter guards, the panic path, the
  CD-40/41 records, tripwire symmetry, scope.

### PR C — codex adversarial round 1 — NOT SAFE, 2 blockers

- **B1 — a reload published the unfiltered canvas Ready before the
  re-ask landed**: a flash of everything under a populated filter field.
  Correlated state without a snapshot boundary. Fixed with a coherent
  outline+matches pair publish, prior-view retention, and a third
  (outline-identity) guard beside the two generations — the handle can
  be swapped between a query taking the lock and its rows publishing,
  which is a question the generations cannot answer.
- **B2 — `OnCommit` ran while `Active` was still cancellable**, so a
  departure raised from inside the effect re-entered `Cancel` and
  produced two outcomes for one press. Fixed with a committing-state
  transition guard and a deferred latest-wins departure applied to the
  RESULT, which keeps M2's one outcome and M4's ordering both true.
- Self-caught during the wave: the reload fact reloaded identical
  content, so it passed against its own mutation. Rewritten to delete a
  matching card first.

### PR C — codex adversarial round 2 — NOT SAFE, 2 blockers (continuations)

- **B1-continued — the snapshot was the OUTLINE only.** The table rows,
  the totals and the summary still read live, and `State` flipped before
  the deferred publish. Ruled to a full-unit snapshot: everything a
  projection reads, one immutable value, with the state flip and the
  publish atomic from the consumers' view. `CanvasProjectionUnit`.
- **B2-continued — the deferred slot drained only on the happy path.**
  Teardown-aware drain: an effect that threw is the refused case by
  another name, so the departure cancels after it; shutdown reordered to
  drain-before-silence; the slot cleared.
- Adjudicated by codex and recorded: a direct `Cancel` inside a commit
  effect is SILENT and that is correct — `CanvasModeCancelled` would be
  an inaccurate sentence, and an inaccurate one is worse than none.

### PR C — codex adversarial round 3 — NOT SAFE, 2 blockers (classes named)

- **B1 — TWO NOTIFICATION CHANNELS.** The model was atomic and the
  presentation caches were not: they rebuilt on `OutlinePublished` while
  the state moved on `PropertyChanged`, so a binding woken by the first
  read "Ready", with the new canvas's summary, over the PREVIOUS
  canvas's controls. Fixed to one channel in a defined order, with the
  fact sampling the MATERIALIZED controls rather than the model.
- **B2 — PIECEMEAL CATCH.** The confirmation announce sat between the
  outcome and the drain, unguarded, and a teardown throw blocked
  `Announcer.Shutdown`. Fixed structurally: one guarded region with the
  drain in its `finally`, and the distinction from round 2's rejected
  shape stated in the code (that one reopened WITHOUT draining).
- **The rule-4 tripwire was set here**: a fourth continuation in either
  subsystem would be a formal stop.
- A behaviour improvement fell out of the fix: a reload no longer
  collapses its rows, so the reader keeps the canvas and their keyboard
  focus for the length of the load.

### PR C — codex adversarial round 4 — RULE 4 TRIPS (2 blockers, both continuations)

- **B1 — the selection re-seat was the next channel member. B2 —
  `WhereAmIText` and `FocusRequest` were teardown callbacks.** Both
  continuations, so the tripwire fired as pre-declared.
- **Design pass (the series' third**, after focus delivery and identity
  containment in PR A**).** A publication-SCOPE primitive: every
  correlated observer-visible write staged, notifications deferred to
  scope close, a mid-transaction wake UNREPRESENTABLE rather than
  avoided, and a census forbidding out-of-scope writes. Plus
  silence-first total finalization: the drain speaks, then every channel
  mutes, then the state clears, and the announcer and handle release
  from a `finally`.
- The design commit found a FIFTH channel member no round had reported —
  the workspace's direct `ActiveSurface` write.
- **The gate mystery was solved in the same pass**: a solution-level test
  run executes the unit and journey projects CONCURRENTLY, and the
  journey app eats the STA facts' synthetic keystrokes. That is every
  intermittent key failure this task saw. The serial gate is two
  commands.

### PR C — codex adversarial round 5 — NOT SAFE, 1 blocker (the design HELD)

- Codex verified the ordering and the throwing-`Shutdown` construction.
  The blocker was incomplete APPLICATION — `CanvasSelection.Marked` and
  `SeedFrom` still raised outside the primitive — and a census that
  scanned one file with a hard-coded field list, which could not have
  seen either.
- **Adjudicated as mechanical completion, not a design continuation**,
  with a round-6 design breach pre-declared to escalate to the user
  without further adjudication.
- The completion made the census DERIVE its population; it caught its
  first unclassified offender immediately, and `ContractsCitationCensus`
  fired unprompted on a rename.

### PR C — codex adversarial round 6 — ESCALATED TO THE USER (4 blockers)

- Side-state outside the unit; the census not fail-closed semantically;
  **the detachment observable FALSE — it inspected two of five channels
  while a passing test said otherwise**; shutdown a drain rather than a
  terminal retirement; and the crates-untouched claim inaccurate about
  the tripwire commit.
- **USER RULING #1: one boundary-complete wave, then codex round 7 as
  final arbiter; a failing round 7 pauses PR C for an architecture
  review.**
- The wave stated the boundary once — everything REACHABLE during a wake,
  not just what raises — and its sweep found `LastActivatedNode`, the
  last mutable field a publication-time reader could reach.

### PR C — codex adversarial round 7 — PR C PAUSED (4 blockers)

- **Query-source identity outside the unit**: handle B queried against
  view A. **The `Empty` sentinel's shared mutable memo.**
  **`HandleEscape`/`RegisterRung` un-gated** — terminality reached by
  enumeration rather than at the command boundary. **The census scanned
  property roots**, so state handed out by a METHOD was invisible.
- Paused per the mandate. The architecture review named the fixed point:
  **the document exposes THREE populations — what RAISES, what is
  READABLE, and what is QUERYABLE — and only two were ever in the
  design.** The coherent shape is the unit OWNING its queries, with
  generation-stamped handle tokens.
- **USER RULING #2: SPLIT.** C-lite now — the verified-clean layer with
  the filter reverted to the synchronous pre-review interim — and the
  coherent-unit redesign as its own contracts-first PR before PR D, with
  its design doc codex-verified BEFORE implementation.

### PR C-lite — surgery review and red team — the extraction, checked

- **Surgery review: one leaked async hunk severed the headline
  feature.** `OnFilterTextChanged` kept its async-era body, so typing in
  the filter field announced nothing — and every filter fact stayed
  green, because the covering fact called `AnnounceFilterCount` itself
  rather than driving the field. Restored, and the mutation re-homed to
  the FIELD. **The lesson is in the fact's own doc comment: a fact that
  calls the verb tests the verb, and what broke was the wire.**
- An era-mixed render list had silently reversed a recorded
  single-render decision, and its §C paragraph had been deleted with it.
  Both restored — and recorded rather than pinned, because the mutation
  PASSES: a double render is invisible to a functional fact, and
  manufacturing one to satisfy a mutation is the unearned-mechanism
  class this PR paid for twice.
- **Red team: the blocker was that this record did not exist.** The §C
  claims said the seven-round history was "in this document's round
  record"; it was not, on either branch — it lived only in the
  git-ignored ledger. That is W4-5's founding lesson repeated, and by
  the controller rather than by an implementer. This section is the fix.
- Recorded with it: the interim filter's SECOND cost (C10), and five
  minors including extraction residue in the mode controller's comments.

### PR C-lite — codex adversarial round 1 — NOT SAFE, 5 blockers + 2 minors

Every one a SYNC-ERA or SPLIT-CONSEQUENCE defect: the parent's seven
rounds hunted the asynchronous publish, and none of them looked at the
code the split kept. That is the argument for reviewing an extracted
branch on its own terms rather than trusting the layer's history.

- **B1 — the palette could not focus the filter field.** The token was
  acknowledged before eligibility was asked; a closing palette holds the
  keys, so every surface read as ineligible, the token was consumed and
  nothing retried. **Ctrl+F worked and Filter Cards did not** — one verb,
  two routes, one dead. Fixed to A14's durable shape.
- **B2 — the filtered outline fabricated containment.** The depth stack
  ran over the FILTERED rows, so a survivor whose group was filtered out
  attached to whatever survivor happened to be shallower and earlier — a
  card from an unrelated branch, spoken as inside a group it is not in.
  CD-45 swept: containment comes from the unfiltered hierarchy, and the
  rule is nearest surviving ancestor, else root.
- **B3 — the delivered set lied.** `commitMode`/`cancelMode` were
  `implemented` in the parity matrix while nothing on this branch ENTERS
  a mode; the entrants are PR F's and the conformance suite drives a test
  mode, which is what §B12's executable rule distinguishes. Both rows
  returned to `pending` and come back with F.
- **B4 — Clear Filter bypassed the admission mapping**, announcing a
  count over an empty outline on a canvas that cannot answer — "0 cards"
  reading as an empty canvas. Both paths admitted; CD-43's mechanism
  sentence swept.
- **B5 — a stale count over zero visible rows.** The memoized answer
  stayed `Current` while the projections were collapsed under `Loading`,
  so the summary counted rows nobody could see. The view is current only
  while the rows are renderable.
- Minors: the verbosity comment's stale "checkable radio items" (the
  third site of a correction recorded in this document), and the
  mixed-EOL trap's FOURTH round-trip, closed as a class with a
  `.gitattributes` `whitespace=cr-at-eol` entry.

### PR C-lite — scoped re-review — REVISE

The five fixes held; what forced a revision was records that no longer
described the code, and one defect the first remedy introduced.

- **B1's remedy opened the other half of its own class.** Acknowledging
  only on success stopped the palette route from evaporating and let the
  OTHER pane on the same document keep the request pending — so it
  pulled the reader into ITS filter field the next time it saw the keys.
  The request is ADDRESSED to a tab and COMPLETED on the document now,
  which is A14's full shape rather than half of it, and both halves are
  separately mutation-verified: dropping the address lets a peer answer a
  request raised for a hidden pane, dropping the completion lets a peer
  inherit a satisfied one.
- **Three records contradicted the code** — CD-45's mechanism and rule,
  CD-43's "unconditionally", and C4's claim that the strengthened
  never-silent fact does not read the mapping it guards. All swept, and
  C4 now says what the pair of facts proves between them rather than
  claiming it of one.
- **A fact that could go vacuously green.** The B5 fact skips every
  sample without a count, which is the fixed behaviour — so its whole
  content lived in samples it might never take. It asserts the hidden
  window was SAMPLED, and the exact sentence in it.
- **An unremarked reorder reverted.** The Escape rung's focus move had
  drifted ahead of its announcement; not required by the remedy, and it
  can reorder two lines when clearing the needle seats a first selection.
  Announcement first, as before.
- **A doc-comment splice** left the method the fix documents with no
  documentation at all, and `FocusFilterField` with two `<summary>`
  elements — which Roslyn does not diagnose, so a 0-warning build said
  nothing. Both repaired.
- **The "same list" claim, and the asymmetry it produced.** The old
  block said the filter request re-asks on "the same list
  `TryDeliverFocus` is on", which was never true. Closed by wiring the
  outline's container realization back to the A14 landing alone — it can
  turn a LANDING deliverable and never a filter-field request — so both
  projections ask the same question again.
- **Minors:** `Rebuild`'s "one stack pass" summary (two passes now, and
  always); C10's cost paragraph, which records the second pass rather
  than leaving the flattering number; `ClearFilter`'s `<summary>`, which
  presented an unconditional answer three lines above the comment
  saying otherwise.
- **The process finding, named because round 1's entry names its own:**
  this record exists because the C-lite red team's blocker was that the
  per-round history lived only in the git-ignored ledger. A wave that
  skipped its own entry would be that finding recurring.

### PR C-lite — scoped re-review, round 2 — REVISE

"The engineering is right; the revision is for records again." The
ownership defect was closed in the twin's own idiom and the fact failed
both mutations — and the record describing the mechanism was falsified
BY THE COMMIT THAT FIXED IT.

- **C10's filter-focus paragraph still called it a token**, still said
  the re-ask happens "from one place", and still said the outline's
  realization asks both requests. All three were true one commit
  earlier; the fix made them false, in the same section the same commit
  was editing two paragraphs above. Swept to the addressed, completed,
  boundary-terminated shape it actually has.
- **The replacement doc block got its own list wrong**, listing
  container realization as a filter re-ask trigger two sentences before
  arguing why realization cannot matter. The list is exact now.
- **`Shutdown` cleared the A14 landing and not its twin** — the one line
  of A14's shape the commit claiming "A14's full shape" left behind. Now
  answered at the BOUNDARY (both requests read absent once retired, so
  no consumer needs to ask whether the document is alive and no list of
  clear sites has to stay complete) plus the field clear for reference
  lifetime. Each half has its own observable and its own mutation,
  because the first pair passed both — two mechanisms covering one
  assertion is a claim without a power.
- **The repo's only duplicate `<remarks>`**, introduced by the hunk that
  fixed a duplicate `<summary>`. Merged.
- **`FilterFocusToken` retired.** No production code read it after the
  addressed request landed, its summary described the superseded design,
  and a table fact watched it — a counter no consumer reads, which would
  stay green for a request nothing could deliver. The fact watches
  `FilterFocusRequest` now.
- The two-pane fact gained its address premise (`Owner` is the tab it
  was raised for) and a behavioural catch for the completion half.

**The C7 asymmetry this exposed, recorded because it outlives the
incident.** The mode stack gates its ENTRY points on retirement
(`Enter`, `Commit`, `Cancel`, `HandleFocusDeparture`) and did not gate
its ANNOUNCE. Those are different boundaries: an entry check answers
"may this verb run", and it necessarily runs BEFORE the verb's effect —
which is the only code that can retire the document mid-verb. A stack
can therefore be live at entry, retired by its own effect, and still
composing. Terminality needs both: the command boundary for the verbs,
and one announce boundary that reads retirement when the sentence is
actually emitted.

**THE PATTERN, recorded for the redesign PR to inherit.** Four times in
this wave — CD-45, CD-43, C4, and now C10 — the record that went stale
was the one describing the mechanism being changed, and three of those
were found by review rather than by the change. Sweeping the rows a fix
CITES is not the rule; the rule is sweeping the row that DESCRIBES what
the fix changed, which is the one most likely to be read as still true.
A fix that edits a mechanism should open its contract's own paragraph
before it opens anything else.

### PR C-lite — codex adversarial round 2 — NOT SAFE, 3 blockers + 2 majors + 2 minors

Every finding sat in a SEAM BETWEEN round 1's fixes, or in an unfinished
half of one. That is the signature of a wave that fixed each item
correctly and did not ask what the items did to each other.

- **B1 — the two clear routes disagreed in the window they were both
  fixed for.** The verb returned from admission BEFORE clearing; the
  Escape rung cleared first and let admission choose only the sentence.
  So during a reload the visible command announced "Opening canvas…" and
  left the needle in the field while Escape cleared it. The rung's order
  was the correct one: clearing is host state and always succeeds;
  admission decides what is TRUE to say about it, never whether the
  user's request runs.
- **B2 — Escape dead-ended in the filter field.** `Render` collapses
  both projections under `Loading` and every failure state, and the
  restoration focused the projection unconditionally and discarded the
  result — so the keys stayed on the window root with the press already
  consumed. It is result-bearing and state-aware now: the projection
  when it renders rows, else the region this state actually shows, else
  a durable addressed A14 landing. `Loading` deliberately reaches the
  last arm — a transient banner is not somewhere to put a reader — and
  the reader keeps the field until the publish seats them.
- **B3 — late writes repopulated retired fields.** Both request-raising
  methods still wrote after retirement. The READ boundary hid it: every
  public read said null while the field held the closed tab's owner and
  its graph — the exact reference C7 claims to drop. Terminality now
  answers on the WRITE as well, and the fact asserts retention AFTER the
  late calls, which is the assertion the read boundary cannot fake.
- **M1 — closing an addressed pane stranded its request.** The document
  survives when a second pane still shows it, so no peer may take the
  request (the address gate working), nothing supersedes it, and the
  closed tab stays reachable. Taken at the TAB-SET boundary, not in
  `Unloaded`, which also fires on mere hiding — a hidden pane's request
  must survive, since becoming visible is one of the conditions that
  delivers it.
- **M2 — and the missing half of CD-45 turned out to be unreachable.**
  The review asked for a surviving-grandparent fixture. It cannot be
  built: core's matcher includes ANY ELEMENT OF THE GROUP PATH, so a
  group that matches carries every descendant, and an ancestor can never
  survive a needle its own children fail. The walk stays as the safe
  general form; CD-45 no longer claims a fact pins a case that cannot
  occur, and a new fact pins the REACHABILITY that makes it so.
- **Min1 — generation ABA.** Completion compared int counters. It
  compares the pending RECORD by reference now — the contract already
  is that a surface hands back the instance it was given — and both
  counters are retired, because nothing else read them.
- **AND THE WAVE'S OWN FACT FOUND THE NEXT ONE, in Debug, immediately.**
  B3's fact calls a verb on a retired document — and `AdmitStructuralRead`
  announced its refusal through the retired announcer, because the
  never-silent mapping speaks and the DOCUMENT had no announce boundary.
  The mode stack had been given one a wave earlier; the document, which
  owns the funnel and has sixteen announce sites, had not. It has one
  now, the navigator speaks through it, and the mode stack is handed it
  instead of the raw funnel — one boundary for the whole canvas.

  Its condition is the FUNNEL's retirement, not the document's own
  shutdown flag, and the difference is C7's SPEAK phase: `Shutdown`
  marks the document first and then drains the mode stack, whose
  restoration is the last sentence a retirement owes. Gating on the
  document's flag silenced it — caught by the mid-commit-shutdown fact
  within one run.

- **Min2 — C10 recorded two costs the previous wave's own fix had made
  impossible.** The reload lock-wait and the mixed-handle count both
  required a query during a load, and the getter now returns before it
  queries whenever the document is not rendering rows. Swept, with the
  cause named: a window closed as a side effect of a different fix, and
  a cost row keeps claiming it until someone re-reads it. **That is the
  describes-what-it-changed rule pointing at a row the change did not
  touch** — the sweep has to follow the MECHANISM, not the diff.

### PR C-lite — codex round 2's closing pass

Extends the entry above; no shipped-behaviour blocker, and every item is
a consequence of that wave rather than a new subsystem.

- **The B2 fix introduced a focus-steal vector, in the class A14 exists
  to prevent.** The durable landing it defers had no reader-location
  guard, so a load finishing after the reader had moved elsewhere pulled
  them back into the canvas. The landing is a RESTORATION, not an
  instruction — it exists only because the reader was already there — so
  the surface WITHDRAWS it when they leave of their own accord
  (`PaneFocus`/`TabSwitch`, never a window deactivation or an overlay
  they are coming back from). A shell-raised landing is untouched.
- **The describes-what-it-changed rule, applied to the wave's own
  mechanisms.** A14 still documented a `Generation` field that had been
  deleted and recorded neither the write-side terminality nor the
  tab-set cancellation the wave added; C6's rung-3 row still promised
  "focus back to the projection"; A5 was cited by the code for a
  predicate it did not record; two navigator comments still deferred to
  a C10 cost C10 now calls impossible. All swept. The rule caught Min2's
  row last wave and missed four of its own — which is the same failure
  one level up, and worth saying so.
- **CD-45's lemma was broader than the code supports.** "A matching
  group carries every descendant" is refuted by the needle `group`,
  which matches a parent by its KIND word while a text card inside it
  matches nothing. The true lemma is "carries every descendant GROUP",
  and it is enough: every route that matches a group also matches its
  parent group, so a CARD's own parent is the only ancestor it can lose,
  and losing it makes the card a root. Stated in the row and in the
  fact.
- **Two consequences of replacing the generation counter, both
  untested until now.** Supersession had no fact at all — and the one
  written first used different owners, so value equality would have
  passed it; it uses a VALUE-EQUAL pair now, which is the ABA case the
  counter used to hide. And an identical re-raise had become a silent
  no-op, because records have value equality and `SetField` saw "no
  change": a reader pressing Ctrl+F twice got no second notification.
  Change detection for the request properties is by REFERENCE now, so
  the notification side agrees with the completion side.
- **The boundary is a GUARD now, not a convention.**
  `NoCanvasCodeReachesTheAnnouncerExceptThroughTheBoundary` derives the
  forbidden surface from the announcer's own members (whatever reaches
  `Emit`) and scans every production source under `Canvas/`, exempting
  the boundary and the one B7 relay seat — each of which must be FOUND
  or the census fails as vacuous. Both historical instances were re-run
  against it rather than argued: the mode stack's wiring and
  `AdmitStructuralRead`'s direct call each fail it by name.
- Recorded rather than fixed: the announce boundary breaks t0 §1.4's
  panel/announcement pairing for one caller — Where-am-I on a retired
  document composes the panel string while the boundary refuses the
  line. Nobody can see that panel, and the alternative is composing for
  a closed funnel.

### PR C-lite — the gate-integrity incident, and the rule that came out of it

A wave whose only finding was in the CONTROLLER's own process, so it is
recorded where the process is.

**What happened.** A report claimed "unit suite 1562/0". The run was
real, it was Release, and Release is what CI runs — and the Debug suite
was failing three canvas facts deterministically. One was PR A's,
recorded early in this PR as acceptable ("CI runs Release, so the gate
is green either way"). The other two were written BY this PR, in the
same shape as the one already excused, and nobody re-ran Debug after
adding them. The third was not a test artefact: `Commit` composed its
confirmation into a retired announcer, the funnel dropped it, and the
only witness was a `Debug.Fail` in a configuration that had been
formally excused.

**The root cause was named correctly and then fixed at the wrong
altitude**, which is its own lesson. Terminality is the SPEAKER's
question, not the funnel's — right — but the first fix gated the one
announce site the failing test walked. `Cancel` had the identical defect
in a worse form: its restoration runs while the announcement's ARGUMENT
is being built, so a restoration that retires the document composes a
cancellation on a retired stack, and no entry check can see it because
the entry check ran first. One `Speak` boundary now carries all four
sites and any fifth, and it reads retirement at EMIT time, which is what
the argument-evaluation order makes both possible and necessary.

**A guard nobody runs is a guard nobody has.** `Debug.Fail` says nothing
in Release. The announcer counts refusals now, so the claim is
assertable in both configurations and its mutation fails in both — where
before it failed only in the configuration nobody ran.

**THE GATE-INTEGRITY RULE, which every later PR in this series
inherits:**

1. **Both configurations.** Debug is where the `Debug.Fail` guards
   speak; Release is what CI runs. A green Release over a red Debug is
   half a gate.
2. **The committed tree.** `git status --porcelain` must be empty before
   the run — stash anything uncommitted — so the numbers describe what
   was committed and not what was in the editor.
3. **Quote the tail.** The report carries the runner's own final line,
   not a summary of it. A summarised number cannot be checked; a quoted
   one can.
4. **A known failure is a finding with an owner, not a footnote.** If a
   configuration is red, either it is repaired or the report says which
   facts, why, and what would make them green — and every wave re-reads
   that list before adding to it.

### PR C-lite — codex adversarial round 3 — NOT SAFE, 2 blockers + 2 majors + 2 minors

- **B1 — the restoration/instruction distinction was WITHDRAWAL-ONLY.**
  A deferred A14 landing was withdrawn when the reader left of their own
  accord, and delivered unconditionally otherwise — so the three
  departures the design deliberately RETAINS across (an overlay, an open
  menu, a deactivated window) were exactly the states in which a
  finishing load seated the reader in a canvas they were not looking at,
  taking the keys off the dialog they were typing in. Delivery now asks
  too: one edge (`_awayBecause`, set from the same classification that
  decides the withdrawal, so the two halves cannot disagree) and three
  levels (an overlay already open, a menu already down, the keys already
  in another pane — the cases with no departure event to withdraw). The
  hold ends on focus returning to the surface or the window activating.
  **A distinction that governs one end of a lifecycle is half a
  distinction, and this branch had built the shape once already**: the
  read side of request terminality was correct for a wave while the
  write side let retired fields be repopulated (round 2's B3). The same
  question — "and what about the other end?" — was the fix both times.
- **B2 — ready-empty Escape landed on an EMPTY PROJECTION.** `Ready`
  keeps the projection visible with nothing in it, and both
  implementations take focus while holding nothing (`TreeView.Focus`,
  and the grid's own), so C6's seat rule asked the projection first and
  got a yes: the reader sat on a silent empty control with the one
  sentence that would have told them what to do unread beside it. The
  seat rule now requires ROWS before it asks the projection. Pinned in
  both projections and through both of the helper's callers (rung 2's
  clear-and-reseat and rung 3's dismissal), with the rung the press took
  asserted — only rung 2 speaks — so the theory is not one case run four
  times.
- **M3 — owner liveness was per WINDOW, not per DOCUMENT.** The sweep
  asked "is this tab still SOME canvas owner", and a pane pointed at a
  different canvas (`TryOpenItem`'s replace arm) answered yes — so the
  canvas it LEFT went on holding a request addressed to a surface that
  renders something else, with the tab's object graph attached. The
  predicate is the pairing now. The fact's two re-raise arms are what
  make it a predicate rather than a one-shot: the same wrong address is
  dropped again by the next sweep, the right one survives it, and the
  swept record arriving late clears nothing.
- **M4 — the announcer census was false-green against ALIASING.**
  `var sink = document.Announcer; sink.Announce(e);` passed a scan that
  matched on the receiver's spelling. Taken by UNREPRESENTABILITY first,
  per the series' doctrine: the announcer is a private field, and the
  two things production legitimately needs are named members —
  `Speak` (the boundary) and `GridRelaySeam` (B7's canonical grid
  relay). `internal` cannot separate this assembly's tests from its
  production code, so `AnnouncerForTests` remains as the named residue,
  and the census's first rule is now that ACQUIRING it outside a seam is
  the offence — which closes alias, conditional access, captured lambda
  and transitive helper at the point they get the funnel rather than at
  a call site a receiver-shaped scan has to recognise. The allow-list is
  gone; the two seams carry their reasons as members, and both must be
  FOUND or the census reports that it is exempting nothing. Ten
  mutations, zero escapes: one compile wall (the private field), one
  compile wall for the renamed boundary, seven census walls, and a stale
  seam.
- **Min5 — "all swept" was false AGAIN, for the third round running.**
  Five more live rows described retired mechanisms. **So the rule stops
  being discipline.** `ContractsCitationCensus` gained a retired-
  vocabulary table — each row a phrase that can only assert the retired
  thing is current, with the replacement that must still be named — over
  §C's live prose and every `.cs` under `Canvas/`, with the round record
  deliberately out of range because history has to keep the old names. A
  quoted phrase is a MENTION, not a use, and is skipped. The check found
  two rows I had not, on its first run. It does not detect staleness in
  general — no textual guard can — and the report says so.
- **Min6 — CD-45's proof step was false; its conclusion was not.**
  "Every route that matches a group also matches its parent" is refuted
  by core's group path being ANCESTOR-ONLY: a child never carries a
  parent, so a group whose own title matches inside one whose title does
  not is promoted to the root like any card. `Pocket zeta` inside
  `Container` is the counterexample and `Rebuild` already handled it.
  CD-45 now carries the direction that actually proves it —
  ancestor → descendant GROUP, route by route — and the conclusion
  (promotion to root is the only alternative shape) is unchanged.
  **The GROUP qualification is the part that has now been lost twice.**
  The first repair of this row wrote "a surviving ancestor carries every
  descendant", dropping exactly the word round 2 had established:
  the needle `group` matches a group by its KIND word while a text card
  inside it matches nothing. The scoped review caught the regression.
  **And the epistemics matter more than either correction**: the
  original conclusion came from one needle route against one fixture and
  was written up as verified. Enumerating core's four match routes
  against the shape is what verification meant, and it takes a minute —
  which is also the whole content of the removed-arm bullet below, at a
  different altitude. Two "every X" claims in one wave, both proved from
  memory rather than from the tool.
- **AND ONE ARM WAS BUILT, REMOVED ON A FALSE ENUMERATION, AND
  RESTORED.** The seat rule was given a fourth arm — a needle that
  matched nothing seats the reader in the filter field — and removed
  again on the reasoning that every caller is an Escape rung and no rung
  can present a needle. The scoped review of this wave refuted it:
  `CloseWhereAmI` is also called from the CD-47 PRE-LADDER path, which
  by C6's own text dismisses the panel with an active filter untouched.
  Restored, with the real caller named in all three places the false
  claim had reached (the code, §C, the report), and pinned by a fact
  that drives `OnPreviewKeyDown` with two panes on one document — the
  arrangement that makes the remembered element stale while a needle is
  live.
  **The lesson is the enumeration, not the arm.** "Unreachable" was
  asserted from a caller list read off the ladder rather than off the
  call graph, and one caller of a caller was missed. It is the same
  failure Min6 records at a different altitude: a claim of the form
  "every X" proved by listing the X's somebody remembered. When the
  conclusion is UNREACHABILITY, the enumeration is the whole proof and
  it has to come from the tool, not the memory.

### PR C-lite — codex round 3's scoped review — REVISE (5 items)

The code was right in substance at every one of round 3's six items. What
this review returned was **the recorded reasoning**, for the third wave
running — two justifications false as written, one correction that
landed everywhere except the canonical row it corrected, and a new gate
that did not cover the mechanisms its own wave had retired.

- **The removed seat arm came back.** "Every caller is an Escape rung"
  was false: `CloseWhereAmI` also runs from the CD-47 pre-ladder path,
  which by C6's own text keeps an active filter — so `Ready` with no rows
  IS reachable at the seat, on a canvas that has cards. Arm 4 restored
  and pinned through `OnPreviewKeyDown` with two panes on one document,
  asserting that NO landing was raised: seating them there and seating
  them by deferring-and-being-rescued are indistinguishable afterwards,
  and only the first needs nothing to go right.
- **CD-45's canonical row still carried the refuted proof step**, while
  the round record two thousand lines away carried the correction. A
  correction that does not land on the row a reader will actually consult
  has not landed. The row now carries the ancestor → descendant-GROUP
  direction, route by route.
- **And the repaired lemma had dropped "GROUP"** — the exact
  qualification round 2 established with the needle `group`. Twice lost
  now, which is why the fact's own comment says the word is load-bearing
  and why the fixture's blind spot (`zeta` appears only in a title) is
  written down beside it.
- **The announcer census had an implicit-`this` hole.** Both rules were
  receiver-shaped one spelling over, so `AnnouncerForTests.Announce(e)`
  inside the declaring file — the file where a new announce site is most
  likely to be written — escaped. Closed with a bare-identifier rule, and
  the residue member is DERIVED from the document now rather than spelled
  as a literal.
- **B1's three LEVELS had no fact.** All three arms of the first theory
  set the EDGE, so deleting the levels changed nothing observable — the
  same standard this branch had just applied to a seat-rule arm, applied
  asymmetrically. `ARestorationRecordedWhileAlreadyAwayIsHeldByTheLevels`
  is the arrangement they exist for: the reader leaves BEFORE any
  restoration exists, so nothing is retained and no edge is recorded.
  Each arm is built so its own level answers — the overlay arm keeps the
  keys in the surface, the menu arm puts the menu in another window,
  which is what a WPF popup is.
- **The retired-vocabulary gate did not cover this wave's own
  retirements.** Three rows for the three mechanisms that had already
  bitten, and none for unconditional delivery, per-window liveness or the
  allow-listed relay seat — and the review found two live stale rows in
  precisely those blind spots. Five rows added, the prose range widened
  to the recorded divergences, the source range widened to the censuses
  themselves, and each row now carries a SAMPLE its pattern must match,
  so a mistyped pattern fails instead of guarding nothing.

**THE INCIDENT: one unexplained Debug red.** The first full Debug run on
`10d943b` reported `Failed: 1, Passed: 1584`, and the identity was never
recovered because the command tailed only the summary line. Four further
full Debug runs and six targeted canvas-class runs were green, Release
and the journeys were green, and assembly-wide parallelisation is
disabled so the wave's new static swap cannot be the cause. It is
recorded here, not only in the transient report, because the
gate-integrity rule's own text says every wave re-reads this list:
**a red that was not identified is an open finding, not a rounding
error.** The process correction is a rule, sitting beside the other
four: **every gate run is captured to a file and its fail block grepped
out of it, never tailed.** A summary line cannot tell you what failed.

**And the shape both false claims shared.** "Every caller is an Escape
rung" and "every route that matches G also matches P" are the same
mistake at two altitudes: a universal claim proved by enumerating the
cases the author remembered. Whenever the conclusion is UNREACHABILITY,
the enumeration IS the proof — so it comes from the tool (the call graph,
core's match routes) or it does not come at all.

### PR C-lite — codex adversarial round 4 — NOT SAFE, 1 blocker + 1 major + 2 minors

Codex confirmed what the previous wave claimed where it could be checked:
no `nameof`/reflection/XAML bypass of the announcer census, no conflict
between the restored seat arm and the pre-ladder path, no mismatch in the
delivered set. What it found was one live defect, one lifecycle half
again, and two classes of record drift.

- **B1 — a rename severed presenter affinity while the reader never
  moved.** An external rename retargets the tab from document X to
  document Y (CD-32); `OnModelChanged` detached the surface from X's
  navigator and never introduced it to Y's. Attachment otherwise waits
  for a false→true keyboard-focus edge or a canvas chord, and a reader
  whose keys sit in the filter field produces neither — so every palette
  movement verb afterwards moved the selection and SPOKE the motion while
  `FocusRow` reached nobody. **The reader and the selection disagreeing
  IS the contract (CD-40), and the recipe was an ordinary rename plus one
  palette command.** `DetachPresenter` reports whether it WAS attached
  now, and a replacement rebinds on that answer or on currently owning
  the keys — the two senses in which the reader can be "in this pane"
  while a document is swapped underneath them.
- **M2 — and the restoration hold starved when its cause ended out of
  sight.** A departure is written when THIS surface loses focus, so the
  move that ENDS it is invisible: open a menu, then click into another
  pane, and this surface hears nothing the second time. The landing was
  then held for the pane's lifetime — never delivered, never withdrawn,
  never completed, and holding the tab it was addressed to. The surface
  watches its host WINDOW's keyboard focus now; while a menu/overlay hold
  is in force and the cause has ended, the DESTINATION decides, routed
  back through `Depart` so the mode stack hears the same thing and stops
  keeping a mode alive across a menu the reader has left.

  **Recorded as ONE ROW with round 3's B1, because they are one
  lifecycle.** B1 was theft (delivery on top of a reader who was
  elsewhere); M2 is starvation (a landing nothing could ever resolve).
  Fixing one direction and stopping is what produced the other. That is
  the third instance of the shape on this branch, after read-side vs
  write-side terminality and withdrawal vs delivery — **a rule that
  governs one end of a lifecycle is half a rule, and the missing half is
  always the one nobody has a fact for.**
- **Min3 — three records claimed more than the code does.** C6 said the
  restoration's RETENTION set is "exactly" the mode stack's KEEP-ALIVE
  set; it is a strict superset by one, because `WindowDeactivated`
  CANCELS a mode (`CancelsFor`) while a deactivated window is still a
  reader coming back to this TAB. C6 also promised DELIVERY on window
  activation, when activation only clears the deactivation edge and the
  levels are consulted afterwards — re-evaluation, not a seat. And the
  levels' theory narrated a "palette-driven rung" it does not have:
  `ClearFilter`, the palette's own clear, deliberately does not re-seat,
  and with the keys outside the surface NO production route reaches
  `FocusProjection`. One arm is a real route end to end (the overlay,
  where the keys stay here); the other two drive the navigator's seam and
  now say so. **A fact may not present a synthetic call as production
  reachability** — the watch-form rule's cousin, and worth its own line
  because a seam exercise is legitimate and a mislabelled one is not.
- **Min4 — staged claims outlived their PRs, four at once.** A10's
  "nothing to describe until PR C ships the filter" (the `, filtered`
  clause has shipped), the §W-C outline row's ItemStatus string (same
  clause), `ChordScope.Canvas`'s "no row is delivered at this scope until
  PR C", and the registrar's "false until PR B and PR D ship their
  projections" (Table shipped in PR B). All four sit OUTSIDE the
  retired-vocabulary census's recorded boundaries, which is the honest
  reading: those boundaries were chosen and written down, and the answer
  to a claim outside them is another guard rather than a quiet widening.

  `NoStagedClaimOutlivesThePrItNames` is that guard. The SHIPPED set is
  derived from the contracts document's own PR section headings, so a
  claim naming PR D stays legal until §D exists and fails on the day it
  does — the day somebody is already editing this document. Sampled
  scope, stated as sampled: the command surface and the §W-C matrix,
  where all four misses were.

**The pattern across rounds 3, 4 and the scoped review between them.**
Every finding has been a claim that was true when written: "every caller
is an Escape rung", "every route that matches G also matches P",
"exactly the departures the mode stack survives", "nothing to describe
until PR C". None was a coding error. The guards that now exist —
retired vocabulary, staged claims, the announcer's acquisition rule —
are all the same instrument pointed at the same failure: **prose ages
against code, and the only prose that stays true is prose something
executes.**

### PR C-lite — codex round 4's scoped review — REVISE (6 items)

- **The mode half of M2 was claimed and not delivered.** The window watch
  was gated on a pending restoration, and `_awayBecause` is only ever
  written while one exists — so the arrangement codex named (mode active,
  menu open, reader clicks another pane, nothing pending) ran none of it,
  and the mode survived in a pane nobody was in with its Commit and
  Cancel controls showing. Both records asserted the closure. The GATE
  serves both constituencies after this wave, because one departure
  classification holds a restoration and a mode alive on the same
  evidence, and a rule that releases only one of them leaves the other in
  the shape it was written to end.
  `AModeHeldAcrossAMenuIsCancelledWhenTheReaderTurnsOutToHaveLeft` asserts
  the premise that no landing is pending, which is what makes it a fact
  about the mode.

  **This wave closed the GATE and not OWNERSHIP, and the round-5 section
  below is where that half lands** — corrected here rather than left
  reading as a full closure, because a record that claims more than the
  wave that wrote it is the exact failure these sections keep finding one
  altitude up. The gate asked "is a mode active", which is a fact about
  the DOCUMENT; asking "is this pane running it" needed the mode to carry
  an owner, and it did not yet.
- **The affinity clause had neither a fact nor a mutation.** Every
  arrangement kept the keys inside the surface, where
  `IsKeyboardFocusWithin` alone satisfies the rebind — and the battery
  row aimed at the clause was written `x && !x`, which is a constant
  false and therefore a second copy of "the rebind dropped". The theory
  has an arm that hands the keys to something else BEFORE the
  replacement now, which is the only arrangement in which
  `wasTheAttachedPane` is the clause doing the work.
- **The clear-and-retry arm was provably redundant and is gone.** The
  keys returning to this surface is a false→true focus-within transition,
  which already clears the hold and re-asks; whichever ran first made the
  other a no-op. Deleting it turned nothing red, which by this branch's
  own standard is an unearned guard rather than a belt — the same test
  that removed a seat-rule arm and then earned the levels theory.
- **Two describing rows the wave changed and did not sweep.** C2 still
  stated a two-case attachment rule after the wave made it three;
  `_awayBecause`'s remark still said "nothing clears this on withdrawal"
  after the wave added the line that does — and warned the next reader
  against the very change the wave had made. **Both were inside the
  retired-vocabulary census's range and invisible to it**, because
  neither used a retired NAME. A row can go stale while every word in it
  stays a word nobody retired, and that limit is now written into the
  census's own remark with these two as the example.
- **The staged-claim guard exempted the document it reads.** "Where all
  four misses were" was false: A10's miss lived in this document, which
  was out of scope, and a live twin of the `ChordScope.Canvas` claim was
  still sitting in §A. The document is in scope now (with the
  mention-strip and a refusal to read `PR C-lite` as a claim about PR C),
  headings match on a word boundary so a reworded one fails loudly rather
  than widening what is legal, and the spec tree stays OUT with its
  reason recorded — a spec records the plan AS PLANNED.
- **Precision, recorded where it will be read.** The retention set is a
  strict superset of the mode keep-alive set; window activation
  re-evaluates rather than delivers; `FocusProjection`'s callers cannot
  run with the keys outside this surface (rather than "all three need the
  press", which misses the panel's Close button); and the window watch
  sees SAME-WINDOW destinations only, with a cross-window departure
  deferred to the next activation rather than held forever.

**TWO TEST-FIDELITY FINDINGS, both from arrangements that passed.**

1. **The false-green menu arm.** The first starvation fact disposed the
   menu's window before the reader clicked away, which bounced focus back
   through the surface — so the ORDINARY departure did the withdrawal and
   the arm stayed green with the fix removed. The mutation battery caught
   it, not a reviewer, and that is the part worth keeping: a green arm is
   not evidence; a green arm whose mutation goes red is.
2. **A second window is not a menu.** Hosting the menu in a second window
   makes the OS deactivate the first, so `WindowDeactivated` is
   classified BEFORE `MenuOpen` — and it CANCELS a mode. The mode fact
   failed on that and the cause took four hypotheses and a temporary
   instrumentation seam to find, because every guess about focus ordering
   was wrong. A WPF menu is a popup owned by its window; the fact that
   needs a menu now uses one in the same window, and the level theory
   keeps the second window deliberately, for the different property it
   needs. **The device that makes a thing observable can also make it a
   different thing** — and the way to tell is to record the actual
   departures rather than reason about which handler runs first.

**THE UNEXPLAINED DEBUG RED IS IDENTIFIED, and it is not ours.** The
watch opened two waves ago — one red in a Debug run whose identity was
lost because the command tailed only the summary — is closed. The FIRST
Debug run on this wave's commit went red, the rule that came out of that
incident caught it (captured to a file, fail block grepped), and the
answer is:

```
SlateWindows.Tests.ReadingViewTests.CodeCopyButtonCopiesTheSourceAndAnnounces
System.Runtime.InteropServices.COMException : OpenClipboard Failed
    (0x800401D0 (CLIPBRD_E_CANT_OPEN))
```

Another process held the Windows clipboard while the test read it. The
reflection frames match the earlier red's stack shape exactly and the
test is a plain `[Fact]` with no async, so the two are almost certainly
one flake; nine green Debug runs between them is what a clipboard race
across a 1,600-fact suite looks like.

**It is a finding with an owner, not a footnote** (gate-integrity rule
4). The owner is the reading view's copy button, W3's; the remedy is the
standard bounded retry around `Clipboard.GetText`, which the machine-wide
clipboard lock has always required and which
`ReadingViewTests.cs:2789` does not have. It was NOT fixed here: the file
is untouched by this branch (`git log main..HEAD` over it is empty) and
editing an unrelated suite without a ruling is the drive-by these reviews
exist to catch. Flagged for a ruling instead.

**The lesson is about the rule, not the flake.** The identity was
recoverable this time only because the previous wave's process failure
had already been turned into a rule. A process rule pays for itself the
first time the thing it guards against recurs — which was two waves.

**PROCESS RULE 6, REWRITTEN AFTER IT FAILED ITS FIRST TEST.** `git add -A` swept a
scratch mutation script into `02a4e93`; it was removed in `803371a`. This
is the SECOND time on this branch — the coordinator removed
`.tmp-boundary.py` in `8b240f3` — which is what makes it a pattern rather
than a slip. The rule as first written — "the scratch sweep runs BEFORE
`git add`, and the staged list is read back before committing" — was
followed to the letter on the very next wave and a third scratch file was
committed anyway. The sweep RAN. It PRINTED the file. The staged list was
read back and showed it added. All three steps happened, chained in one
command with the `git add` and the `git commit`, and the output was read
after the commit had already been made.

**So the rule is not a checklist item, it is a GATE: the scratch check
must ABORT the command rather than report into it.** A check whose
failure mode is "prints a warning next to the action it should have
prevented" is a check that has already failed — the same shape as a
guard whose mutation passes, one altitude up, and the third time this
branch has met it (the gate-integrity incident printed a red Debug run
into a report that claimed green; the false-green menu arm printed a pass
next to a mutation that should have gone red).

Recorded here beside the other five because a commit message is not a
durable place for a rule, and the gate-integrity list is where this
branch keeps them.

### PR C-lite — codex adversarial round 5 — NOT SAFE, 1 blocker + 1 minor

No majors, and codex verified clean what the previous wave claimed: both
rebind-clause pins with independent premises, the clear-and-retry
removal, the staged-claim guard end to end, the delivered set against the
matrix.

- **BLOCKER — a sibling pane could cancel the mode the reader was
  using.** The mode stack is DOCUMENT-shared; every loaded surface
  subscribes to its host window's focus stream; and the widened gate read
  `Modes.IsActive` — a fact about the document — as though it were about
  itself. Two panes on one canvas, mode entered from A: every focus
  movement INSIDE A (returning from the palette, clicking A's own Commit
  or Cancel button) fired B's watcher, which saw a mode active and the
  keys outside B and cancelled the mode, before the reader could reach
  the M6 controls that exist for that moment. The previous wave's fact
  used one surface, so it could not see the sibling.

  A mode carries its OWNER now, captured at `Enter` from the navigator's
  attached presenter, and cleared where `Active` goes null, so no mode
  means no owner. `AModeBelongsToThePaneItWasEnteredFrom`
  drives three ARRANGEMENTS at the navigator seam — the reader on the
  projection, the keys in a palette, the keys in a menu — moves twice
  inside the owner, and COUNTS the restorations when the reader finally
  lands in the peer, because three routes to one cancellation is also
  three routes to running the reader's undo twice.

  **It does NOT cover "entry from the palette" as a route**, and the
  record said it did until codex round 9. There are no production
  `EnterMode` callers at this tip — the entrants are PR F's — and the
  shell helper that a palette row would reach resolves only
  `ActiveCanvasDocument`, discarding the command parameter. So the fact
  exercises the seam with the pane supplied directly; what it cannot
  exercise is the routed-command boundary that would have to carry the
  originating surface to it. That obligation is written into the travel
  table rather than left as a green light.

- **THE THIRD AFFINITY, named once so it stops being discovered.** A14's
  landing carries its owner; the navigator's presenter carries the pane
  the reader is in; a mode now carries the pane it was entered from.
  Three waves, three separate discoveries of one rule: **anything
  DOCUMENT-SHARED that a PER-SURFACE mechanism acts upon must say WHICH
  SURFACE, because "the document has one" and "this pane owns it" are
  different facts and only the second licenses acting.** It is written
  into C8 as a rule for PR F to inherit rather than as three examples for
  PR F to re-derive, which is the difference between a record and a
  lesson.

- **MINOR — C8 was two waves stale, in the row describing the very
  mechanism.** It still said menu-then-elsewhere is not reclassified and
  deferred the repair to PR F, both falsified by the window watcher. C8
  now carries the same-window reclassification rule, the ratified
  cross-window boundary (deferred to the next activation, not held), the
  ownership rule, and the affinity lesson, citing the driving facts.

  **And it was invisible to BOTH guards, in a third way.** The
  retired-vocabulary rows could not see it — no retired name, the same
  class as C2 and `_awayBecause` one wave earlier. The staged-claim rule
  could not either, and this is the new shape: its question is "does this
  claim name a PR that has SHIPPED", and the deferral named PR F, which
  has not. **A deferral to a future PR that the present already carried
  out is invisible to a shipped-set test by construction.** No textual
  rule proposed so far catches it; it is recorded in the census's honesty
  paragraph so the next reviewer looks for it by hand instead of trusting
  the green.

- **A record correcting itself.** Round 4's section claimed the gate
  change closed both constituencies. It closed the GATE; ownership was
  the other half and landed here. That bullet now says which wave did
  which — and it is worth noticing that the correction was needed on a
  section written specifically to record claims outrunning code.

### PR C-lite — codex adversarial round 6 — NOT SAFE, 1 blocker + 1 minor

- **BLOCKER — ownership did not follow the lifecycle it names.** A mode
  captured its owning pane and nothing told it when that pane stopped
  being one. Panes A and B on canvas X, mode entered from A, A's tab
  retargeted to another canvas while B keeps X open: A detaches from X's
  navigator, the mode goes on naming a surface that no longer shows X,
  and NOBODY is entitled to end it — A watches its new document, B is not
  the owner. M4 says no mode survives without focus; this was a mode
  surviving without a PANE, while B rendered the shared Commit and Cancel
  controls for transient state it could apply and did not own. A second
  half: a completed mode READ `Owner` as null while still holding the
  pane's whole control tree, which is round 2's B3 exactly — a read
  boundary hiding a retention — one object over.

  `HandleOwnerDeparture` routes through `HandleFocusDeparture`, so the
  commit-time deferral applies to a vanishing owner as it does to every
  other departure. The retention half clears at the ONE place `Active`
  goes null, which is also why a REFUSED commit keeps its owner without
  needing an exception: it never sets `Active` null.
  `ACompletedModeHoldsNoPane` asserts the field through a dedicated
  observable, because the read boundary is what hid it the first time.

- **AND THE SAME DEFECT WAS LIVE ONE ALTITUDE BELOW, found by a mutation
  that would not fail.** The window watch had been taught ownership in
  round 5; `Depart` — the classifier it routes THROUGH — had not. So any
  pane's departure ended any pane's mode: collapsing a second pane in a
  split, which never touches the keys, cancelled the mode the reader was
  running in the first. Reached by asking why "owner identity ignored"
  escaped, building the arm that should have caught it, and finding it
  caught something else. **A mutation that will not fail is a question
  about the arrangement, not a defect in the mutation** — the third time
  on this branch that following an escape led to a live defect rather
  than to a test edit.

- **THE THIRD AFFINITY NOW HAS THE FIRST TWO'S LIFECYCLE.** C8 named the
  pattern last round; this round gives the mode's owner the three
  guarantees the request already had — write-side terminality (the owner
  cannot outlive its mode), cancellation when the addressed surface stops
  being an address (the detach transition), and the retention half
  (nothing held after completion). That is the request-lifecycle doctrine
  arriving at the mode, and it completes the pattern rather than adding
  to it: **an affinity is not one field, it is a field plus those three
  guarantees**, and PR F inherits the whole shape.

- **THREE BELTS WRITTEN AND REMOVED, all for one reason.** An
  owner-departure call on unload; a `_owner = null` in `Shutdown`; and
  before them, round 5's clear-and-retry arm. Each was redundant with a
  path that already ran — the visibility edge, the drain's own cancel —
  and none could be made to fail. Removed, with the argument recorded at
  each site, and the FACTS left asserting the invariant rather than the
  line, so a future change that breaks the covering path is still caught.
  The lesson is not "write fewer belts": it is that **the redundancy is
  only visible from the mutation**, and writing the belt first and the
  mutation second is what surfaces it.

- **MINOR — a member was spliced inside a neighbour's doc block.** The
  new owner fact's `<summary>`/`<remarks>` opened before
  `AnAnnouncementThatFaultsAfterTheOutcomeStillDrainsTheSlot`'s remarks
  closed, leaving that fact's closing tags orphaned after the new method.
  Roslyn says nothing, the build is clean, the suite is green, and two
  members' prose is silently welded together for the next reader. FIFTH
  appearance on this branch — so `EveryDocCommentClosesWhatItOpens` joins
  the guarded set: a tag stack over each contiguous run of `///` lines,
  which is exactly one member's block, over canvas production, the canvas
  tests and the censuses. Seeded both ways so a mistyped checker fails
  instead of guarding nothing.

### PR C-lite — codex adversarial round 7 — NOT SAFE, 1 blocker + 2 minors

- **BLOCKER — the "or when nobody owns it" arm restored the very defect
  it was written beside.** Round 6 taught `Depart` ownership and left a
  safety net for the ownerless case. But `Owner` reads null for TWO
  states — "no mode active" and "a mode nobody owns" — so the net could
  not tell them apart, and forwarded a peer's departure into a mode
  unrelated to it. Latent at this tip and reachable in PR F: a palette
  entry before any pane has focus, then a split change.

  Taken STRUCTURALLY rather than by a smarter predicate, which is the
  branch's own doctrine (M4's unrepresentability): **an owner is a
  required argument to `Enter`.** `CanvasNavigator.EnterMode` is the one
  production route in, supplying the attached pane and REFUSING when no
  pane has ever held the keys; the null arm is deleted because the state
  it read no longer exists. The ambiguity is not handled, it is gone.

  **And the evidence had to be paired to be honest.** The review's own
  mutation — restore the null arm — no longer fails anything, and NOT
  because the arm is harmless: because the state it reads is
  unreachable. So the battery runs the pair: restore the arm alone and
  nothing changes; permit ownerless entry AND restore the arm, and the
  fact fails again. **When a remedy makes a state unrepresentable, the
  single mutation stops being evidence and the pair becomes the
  evidence** — worth naming, because "the mutation passed" would
  otherwise read as a weaker guard rather than a stronger one.

  The refusal's cost was weighed rather than assumed: it applies only to
  a canvas nobody has focused, and opening one lands focus on a row
  (A14), so it is the background tab — where no mode verb is reachable
  anyway. Recorded in C8, and PR E/F enter through the same seam.

- **MINOR 1 — the doc census was false-green against its own motivating
  defect.** Two separately-balanced `<summary>`/`<remarks>` pairs in one
  `///` run pass a LIFO check, and that is exactly what a splice leaves
  behind: one member carrying both blocks and its neighbour carrying
  none. There was a live instance in the file the last wave edited —
  `HandleFocusDeparture` had lost its documentation to
  `HandleOwnerDeparture`. **A guard written for a defect that does not
  catch that defect is worse than no guard**, because it retires the
  reader's suspicion. The rule is now one top-level `<summary>` per run,
  seeded with the balanced-duplicate case; the scope is enumerated and
  de-duplicated (the first version read every `Canvas…Census` twice);
  `ChordTableTests` is in; the FlaUI project is out with its reason
  recorded; and the floor counts FILES, because a blocks-per-file floor
  drifts with every paragraph anyone writes and a floor that drifts is a
  floor nobody re-reads.

- **MINOR 2 — the canonical rows, reconciled against the tip.** The
  corrections had been landing in ROUND RECORDS while the normative rows
  they correct went on saying the old thing. That is the whole failure
  class of this PR at one remove: the derivation is honest and the
  artefact PR F actually reads is not. Four contradictions closed:

  * **C8** carried the affinity FIELD and not its doctrine. It now
    carries the doctrine that took three waves to assemble — **an
    affinity is a field plus three lifecycle guarantees**: it cannot be
    absent while the thing it names is live; it ends when the thing it
    names stops being an address; and it is RELEASED rather than merely
    hidden, because a read boundary that returns null is not a field
    holding nothing.
  * **CD-45** said the grandparent case cannot occur and then justified
    the implementation with that case. Both cannot be true; the headline
    is the true one, the walk stays because it is the safe general form,
    and the reachable shape is promotion to the root.
  * **CD-47** said rung 3 closes the panel — but an open panel PRE-EMPTS
    the ladder, which is what CD-47 is, so no Escape a reader can press
    reaches rung 3 while the panel is up.
  * **§W-C's navigator row** described canvas chords as uniformly gated
    on projection focus with a panel exception that only applied from
    inside the panel. Delivery is from the SURFACE and the gate is per
    ARM; the panel pre-empt keys on the panel being OPEN, not on focus
    being in it.

  The pass was run over C1–C13, CD-40..48 and the three §W-C rows against
  the code at the tip rather than against the round history — which is
  the only way to catch this class, because a row that was true when
  written reads as true to anyone who remembers writing it. **It found
  seven more beyond the four the review named**, and the shape of them is
  the argument for doing it as a pass rather than per finding:

  * C8's own new paragraph said `Owner` "reads through the active mode"
    — false within the same wave that wrote it, because the read-through
    was removed hours later on mutation evidence. A row can go stale
    against a change nobody else has seen yet.
  * C10's re-ask list called itself EXACT and had missed the window
    activation for two waves; the code comment beside it repeated the
    same list, so both said "three places" while the code had five.
  * §W-C described Enter with mac's bubbling precedence, which C3
    explicitly REJECTED for Windows — two canonical rows disagreeing
    about the same key.
  * C6's seat-rule paragraph — the one written to correct a false caller
    enumeration — enumerated two of three callers. The Where-am-I
    panel's Close BUTTON reaches the same dismissal by pointer, with a
    live needle, which is arm 4's whole justification.
  * C2's presenter inventory was one member short: `Owner`, the member
    that makes the addressed filter request the next paragraph describes
    work at all.
  * C6's rung table still listed the Where-am-I panel as rung 3's arm
    (CD-47's correction had not swept it) and omitted the result summary
    from the leave-the-field arm.
  * §W-C's panel inventory omitted the Close button — a focusable Invoke
    target missing from the accessibility instrument's own list.

  **Three of the seven are enumerations, and two of those are inside
  paragraphs written to correct enumerations.** That is the finding worth
  carrying: a list in prose is a claim of completeness that nothing
  checks, and this branch has now got one wrong at four different
  altitudes. Where a list is load-bearing, derive it or guard it — and
  where it cannot be, say "among others" rather than "exact".

- **A FOURTH belt removed, and the reason this wave kept making them.**
  `Owner` was a read-through (`_active is null ? null : _owner`) written
  one wave before the field gained its clear. Once both existed the
  read-through's mutation could not be made to fail — `Enter` is the only
  writer and the clear sits at the one place `Active` goes null — so it
  went, with `ACompletedModeHoldsNoPane` and `AModesOwnerEndsWithTheMode`
  both left asserting the invariant (the clear's own mutation now fails
  two facts, not one). That is four across three waves: the
  clear-and-retry arm, `Shutdown`'s owner clear, the unload transition,
  and now the read-through. **They are not carelessness; they are what
  happens when a lifecycle is built one guarantee per wave.** Each was
  written defensively before the guarantee that would subsume it existed,
  and only the mutation could tell. The rule for PR F: when you add a
  guarantee, re-run the mutations for the guarantees already there.

**THE LAST-MILE RULE, recorded because it is the shape of this whole
PR.** The round records are the DERIVATION; the canonical rows are the
ARTEFACT. Every review from round 3 onward found the same thing at some
altitude — a claim that outran its code — and the reason the class kept
recurring is that fixing it in the derivation feels like fixing it.
**A correction has not landed until the row a future reader will consult
says the new thing.** For PR F: read C1–C13 and CD-40..48 against the
code before starting, not against these sections.

### PR C-lite — codex adversarial round 8 — NOT SAFE, 1 blocker + 1 major + 3 minors

- **BLOCKER — the refusal predicate was reading a CACHE and calling it
  history.** Round 7 made `EnterMode` supply the owner from
  `AttachedPresenter` and refuse when it was null, on the reasoning that
  null meant "no pane has ever held the keys". It does not.
  `_presenter` is a document-wide slot that ANY pane's detachment
  clears — so pane A holds focus, A unloads or is retargeted, and the
  surviving pane B's next mode invocation is refused: on a canvas that
  has plainly been focused, from a pane that is plainly live. The
  predicate was answering "has any pane held the keys lately", and no
  predicate over that cache could have answered the question that was
  asked.

  **Identity comes from the INVOCATION now.** `EnterMode(spec, pane)` —
  a chord already carries its presenter, and a palette or menu row knows
  which pane it serves, because the shell resolved a canvas tab to put
  the row in front of the reader at all. That source is true at the
  moment of the call, which is the only moment that matters. The
  non-null requirement stays where it belongs, at
  `CanvasModeController.Enter`, so ownerless-active remains
  unrepresentable.

  **The shape to carry: a cache is not a log.** Twice now on this branch
  a predicate has been written over `_presenter` as though it recorded
  history — first for presenter affinity, where the answer happened to be
  right, and here, where the same read was wrong for the same reason.
  When a question is about what HAS happened, a field that anything can
  clear cannot answer it.

- **MAJOR — two surface-hosted facts supplied a fictitious owner**, and
  the previous report claimed all surface-hosted facts had moved to the
  production route. They had not: `EnterOnAFocusedModeButtonActivatesTheButtonNotTheChord`
  and `TheModeIsInspectableAndHasVisibleControls` entered with the opaque
  stand-in while hosting a real surface — so the ownership-sensitive
  departure paths were disabled in exactly the facts that build surface
  state, and the state was created through a route production could
  refuse. Both now enter through `Navigator.EnterMode` naming their own
  surface. The stand-in survives ONLY in genuinely surface-free M1–M7
  facts, where it says something true: this fact is not about panes.
  **A blanket rewrite reported as complete is a claim like any other**,
  and this one was checked by the reviewer rather than by me.

- **MINOR 1 — prose describing the removed design.** `Enter` "captures
  the presenter"; `Owner` "reads through the active mode"; "computed
  rather than cleared"; the presenter inventory; rung 3's description.
  All swept against the final code, with the history left in these round
  records AS history. Two of them were written in the wave that removed
  the thing they describe.

- **MINOR 2 — the paired evidence short-circuited.** The prerequisite
  (a mode cannot be entered without a pane) and its downstream
  consequence (a peer cannot end somebody else's mode) shared one test,
  in an order where the first assertion made the second unreachable — so
  the pair proved one thing twice. Split:
  `AModeCannotBeEnteredWithoutAPane` asserts the refusal alone, and the
  consequence lives in `AModeDoesNotOutliveThePaneThatOwnsIt`'s
  `PeerHidden` arm, with each half's failing assertion recorded in the
  report.

- **MINOR 3 — a battery row claimed an impossible catch.** "Remove the
  balanced-duplicate seed → caught" cannot happen: the seed IS an
  assertion inside the test, so removing it removes the check rather than
  failing it. The row is corrected to the mutation actually run —
  removing duplicate-summary DETECTION, which the seed then catches.
  **A mutation table is evidence, and a row nobody could have run is
  worse than a missing row**, because it reads as coverage.

- **AND A MUTATION ESCAPED, which produced a fact rather than a
  deletion.** `EnterMode` attaches the invoking pane, and nothing
  observed it: the mode would belong to one pane while the movement verbs
  seated the reader in another — the reader/selection disagreement CD-40
  is about, arriving by a third route. Kept and pinned
  (`EnteringAModeFromAPaneMakesItThePaneTheVerbsActOn`) rather than
  deleted, because the invocation naming the pane and the navigator
  serving a different one is incoherent on its face. Four belts were
  deleted on this branch for failing their mutations; this one earned a
  fact instead, and the difference is whether the code says something
  true that nothing had yet asked about.

### PR C-lite — codex adversarial round 9 — NOT SAFE, 1 blocker + 1 major + 2 minors

**The user's ruling: continue until SAFE.** Recorded here because the
alternative — stopping at a round count — would have shipped the blocker
below, and because the rounds have stopped converging on the same defect
and started finding successive ones in the same NEIGHBOURHOOD. That is a
different signal from round 6 of the async filter, which found the same
design failing repeatedly.

- **BLOCKER — a REFUSED entry still moved the reader's pane.** The attach
  that makes the invoking pane the reader's pane ran before an admission
  that can say no. So a second pane asking for a mode while the first
  pane's was still running left the mode owned by A and every movement
  verb acting through B, and an entry into a retired controller left the
  navigator holding a presenter the terminal object had just rejected. A
  missing spec threw after the attach had already landed.

  **Asking for something and being told no must cost nothing.** Neither
  half was wrong — the attach is right, the refusal is right — only their
  ORDER was, which is why the fix is the window the attach sits in rather
  than its place: it still runs BEFORE publication, so anything reacting
  to the mode becoming active already sees the pane it belongs to.
  `AdmitsEntry` is the controller's own condition, used by `Enter`
  itself, because two spellings of "would this be admitted" is how the
  answer and the effect drift apart.
  `ARefusedEntryLeavesAffinityWhereItWas` covers all three refusal
  shapes — speaks-and-returns-false, silently-returns-false, throws —
  each asserting the answer AND that affinity is where it was.

- **MAJOR — a fact claimed routes it does not have.** The ownership
  theory drives three ARRANGEMENTS at the navigator seam; the record
  called them "entry from the projection, the palette and a menu", which
  reads as three ROUTES. There are no production `EnterMode` callers at
  this tip, and the shell helper a palette row would reach resolves only
  `ActiveCanvasDocument`, discarding the command parameter — so nothing
  in the shell can yet say WHICH pane asked.

  Corrected to the seam, and the residue is written into the travel table
  as an OBLIGATION rather than left as a green light: PR F must carry the
  originating surface through the routed-command boundary, and owes
  two-window facts where the pane that BOUND the command and the pane the
  reader is in deliberately differ — the only arrangement in which "the
  active document" and "the pane that asked" give different answers.
  **A fact's arrangements are not its routes**, and a record that lists
  arrangements in the vocabulary of routes will be read as coverage.

- **MINOR 1 — a ratified rationale that was wrong, and the ratification
  did not make it right.** The previous report explained an escaped
  mutation by saying ownerless entry had become "inexpressible in the
  signature". It had not: C# nullable annotations are not type-level
  non-nullability, this branch writes `null!` twice in the very facts
  concerned, and the actual exclusion is a RUNTIME `ThrowIfNull` in two
  places. The pair is reclassified as **not applicable after the API
  shape changed**, and the true statement recorded: ownerless-active is
  excluded by runtime guards, independently caught by
  `AModeCannotBeEnteredWithoutAPane`. Kept as a record entry rather than
  quietly fixed, because a wrong rationale that survived a ratification
  is worth more as a caution than as a deletion.

- **MINOR 2 — the prose sweep, one mechanism behind AGAIN.** Five live
  formulations: the three-case attachment count (four now), "EnterMode
  supplies the attached pane and refuses", "the affinity a MODE captures"
  on `AttachedPresenter`, the cache-capture wording in the ownership
  fact, and C7–C8 missing from both test inventories. Swept together —
  and, because this is the fourth consecutive round to sweep prose about
  the SAME mechanism, the surviving formulations are now rows in the
  retired-vocabulary census ("captures the presenter", "supplies the
  attached pane", "attachment is a THREE-case rule"). **A class that
  needs sweeping four times is a class that needs a guard**, which is the
  rule this branch already wrote and did not apply to itself here.

- **AND THE DOC-TAG GUARD FIRED ON ITS AUTHOR.** Adding `AdmitsEntry`
  spliced its doc block between `Enter`'s summary and `Enter` — the sixth
  instance of the class, and **the first caught by the guard rather than
  by a reviewer**. It named the file, the line and the reason, and the
  repair took one move. That is the whole argument for converting a
  recurring review finding into a check: the seventh instance costs
  thirty seconds instead of a round.

### PR C-lite — codex adversarial round 10 — NOT SAFE, 1 major + 1 minor, ZERO code defects

The first round on this branch to find nothing wrong with the code. Codex
probed the admission ordering directly: every refusal shape preserves
affinity, the active refusal speaks before returning, the attach precedes
`Active`'s publication, and a publication exception lands after `_active`
is assigned so there is no attachment-without-mode window. Both findings
are RECORD-only.

- **MAJOR — the ownership theory still presented arrangements as routed
  coverage.** Round 9 corrected the §C sentence and left the fact's own
  dimension named `ModeEntry` with values `Palette` and `Menu` — vocabulary
  that says "how the reader got in" for arms that all drive
  `Navigator.EnterMode` at the seam. So the correction and the artefact
  disagreed, and the canonical row still read as covering two routes the
  branch has never touched. The dimension is `ModeEntryLocus` now —
  `KeysOnTheProjection`, `KeysInThePalette`, `KeysInAMenu` — which says
  what the arms actually vary: where the reader's keys are sitting when
  the owner is recorded. C8 says three navigator-seam ARRANGEMENTS and
  points at the PR F obligation for the routed work.

  **THE ROOT CAUSE, which is the sweep rule in its sharper form.** Round
  9's correction followed the EDITED SITES — the sentence that was wrong,
  and its neighbours in the same file. It did not follow the CONSUMERS of
  the claim, and the enum was one. **Sweep the claim's consumers, not the
  diff's neighbours.** This branch has now written three versions of the
  same rule: sweep the row that describes the mechanism (round 3), sweep
  the canonical rows rather than the round records (round 7), and now
  sweep everything that repeats the claim in any vocabulary. They are one
  rule, and the reason it keeps needing restating is that each time it was
  applied to the artefact in front of the author rather than to the claim.

- **MINOR — `Owner`'s doc described the retired cache-derived
  admission**, still saying `EnterMode` supplies "the pane that owns the
  keys or owned them last" and "refuses when no pane has ever held them".
  Both were true two rounds ago. Rewritten to invocation-supplied
  ownership with runtime-guarded refusals, and the escaped formulations
  are now their own retired-vocabulary row with their own seed — the
  fifth consecutive round in which a formulation of this mechanism
  survived a sweep, and the second in which the answer was a census row
  rather than another sweep.

**What this round says about the process.** Nine rounds found code
defects; the tenth found only records. The records are the thing that has
been hardest to get right, and the reason is legible in the last three
findings: prose has no test, so every claim in it is load-bearing until
someone re-reads it, and the author is the worst person to do that
because they remember what they meant. Every durable fix on this branch
has been the same move — take the class the reviewer found by reading,
and give it something that executes.

### PR C-lite — codex adversarial round 11 — NOT SAFE, 1 major + 1 minor, production sound

Production sound again, every round-10 repair verified accurate, scope
and delivery exact. Both findings are the SAME class as round 10's, found
the same way — by enumerating what a claim covers rather than by
following a diff — and both are the consumers those sweeps missed.

- **MAJOR — the "every read verb" evidence omitted a shipping read
  path.** `EveryReadVerbAnswersInEveryLoadState` says in its own remarks
  that a verb added without a state answer "fails here by name". The
  list was hand-curated and missing `AnnounceFilterCount`, which the
  filter field's `TextChanged` reaches on every keystroke — including
  while the canvas is reloading or has failed. So a regression confined
  to that verb's unreadable branch passed the fact whose entire claim is
  that such a thing cannot pass. The field-wiring fact drives only a
  READY canvas and the reload fact checks the summary LABEL rather than
  the announcement, so nothing else was looking either.

  Both halves fixed. The verb is in the battery, and the battery's
  inventory is **DERIVED**: reflection over the navigator's public
  methods, each of which must be a row or a named exclusion carrying its
  reason, with stale exclusions failing too. The claim "fails here by
  name" is true by construction now rather than by maintenance. And the
  PRODUCTION trigger has its own fact — `TypingInTheFieldAnswersInEveryLoadState`
  drives the field's `TextChanged` in every unreadable state, because
  calling a verb on the navigator and a reader typing are not the same
  evidence.

  **This was the last hand-maintained list on the branch**, and it is the
  fifth instance of the rule it breaks: a list is a claim of completeness
  that nothing checks. The earlier four were in prose. This one was in a
  test, which is worse — prose that is wrong reads as wrong to a careful
  reader, and a green test reads as proof.

- **MINOR — a fact claimed exact mac equivalence the record already
  rejects.** `TheFilterActivePredicateIsMacs` and the predicate's own
  comment said the rule is mac's. §C's micro-divergence m2 says
  otherwise and has since it was written: Foundation's `.whitespaces` is
  Zs plus tab, `char.IsWhiteSpace` is wider, and five code points
  (U+000B, U+000C, U+0085, U+2028, U+2029) read INACTIVE here and ACTIVE
  on mac. The fact is `WhitespaceIsNotAFilterButANewlineIs` now — the
  bounded claim, which is the newline carve-out — and **the five
  divergences are arms rather than a paragraph**, because a ratified
  boundary that nothing executes is a boundary that moves. The comment
  says what the predicate is spelled out FOR.

  The formulations join the retired-vocabulary census — and its LIMIT is
  recorded with them rather than implied. That census scans canvas
  PRODUCTION and the censuses, deliberately not the test tree, because
  test prose narrates retired mechanisms on purpose. So the row catches
  the wording in the comment (verified) and would NOT have caught it in
  the fact's NAME, which is where this instance actually lived. **A test
  method's name is a claim and nothing guards it**; that is a known gap,
  named here, and not one a fifth census row would close without a
  name-shaped rule that has its own false-green risk.

**THE PATTERN ACROSS ROUNDS 10 AND 11, recorded because it is the exit
condition for this kind of review.** Neither round found a code defect.
Both found a claim wider than its evidence, in an artefact nobody had
edited recently — an enum's value names, a test's curated list, a fact's
title. Diff-following cannot reach those; only enumerating a claim's
consumers can. **The sweep rule's final form: when a claim is corrected,
find everything that repeats it in any vocabulary, including the things
that state it by being NAMED that way.** A test method's name is a
claim. An enum value is a claim. A curated list is a claim of
completeness. All three were wrong on this branch while the prose beside
them was right.

### PR C-lite — the CI divergence: six menu arms, green locally, red on the runner

Every canvas fact passed locally at 1638/0 and six failed on the CI
windows job — all of them MENU arms, every one a bare
`Assert.True() Failure` with no message. The failure cost a round trip
because the log could not say which premise died.

**ROOT CAUSE, reasoned from the signature and then confirmed by probe.**
The six share exactly one thing: a `MenuItem.Focus()` call reached only
in the menu arm. Each theory's other arms passed on the same runner, in
the same test class, executing the same surrounding asserts — so
elimination alone put it on that call. It is NOT window activation: two
of the six use an IN-WINDOW menu and failed identically, which retires
the hypothesis that a second window fails to activate on a runner
desktop.

`Menu` is a WPF FOCUS SCOPE. A local probe of both shapes:

```
scope=True   itemFocus=True  keyboard=MenuItem  logicalInMenu=MenuItem
scope=False  itemFocus=True  keyboard=MenuItem  logicalInMenu=null
```

Both work here, and they work by DIFFERENT mechanisms. With the scope,
`Focus()` sets LOGICAL focus inside the menu's scope and keyboard focus
follows by a handover; without it, keyboard focus is set directly in the
window's own scope — the same path `TextBox.Focus()` takes, which the
runner demonstrably supports, because every non-menu premise in the same
facts passes there.

So the arrangement drops the scope. **Nothing the code under test reads
changes**: `ClassifyFocusLoss` walks the focused element's ancestors for a
`MenuBase`, a TYPE test that a focus scope neither helps nor hinders. The
fact keeps the production predicate exactly and stops depending on a WPF
focus-management step this branch owns no behaviour in.

**What the evidence does and does not show, stated plainly.** It shows
the two paths are mechanically different and that the surviving one is
the path CI is known to support. It does NOT show the runner fails
specifically at the handover — that is an inference, and the premise
messages added with the fix are what will confirm or refute it on the
next run.

**NOT taken: skip-on-CI, and not a synthetic seam either.** An excused
configuration is where the next failure lands, which is this branch's own
gate-integrity record. And a seam-driven substitute — legitimate when
labelled, per round 4's Min3 — was not reached for, because the evidence
does not say the OS route is impossible there; it says one OS route is
more portable than another. Reach for the synthetic only with evidence,
or the fact quietly stops testing the thing.

**THE RULE THIS ADDS, and it is a gate-integrity rule.** **Every premise
assertion carries a message naming its leg.** Forty-six messageless
premise asserts across the canvas facts are now written — every
`.Focus()`, every `PressKey`, every `FocusRow` — because a premise that
fails on a machine nobody can attach a debugger to must say what it was
trying to establish. `Assert.True() Failure / Expected: True / Actual:
False` from a remote runner is not a finding, it is a request for another
round trip. This joins the list beside "capture the gate run to a file"
and "the scratch sweep aborts": all three are the same rule, which is
that a check must be able to explain itself to somebody who was not
there.

**The local-vs-CI class itself, for PR F.** Every WPF arrangement that
depends on the desktop's focus policy — menus, activation, foreground —
is a portability question before it is a test question. The ones that
survive are the arrangements that use the plainest mechanism which still
satisfies the production predicate, and the way to find out which that is
is to probe both and compare, not to assume the one that works here is
the one that works.

### ENVIRONMENT FACT — the CI desktop refuses keyboard focus into a menu (PR E/F must read this)

**Durable, and it is not about this branch's code.** On the GitHub
windows runner, `MenuItem.Focus()` returns false. Every canvas menu arm
failed there while passing locally, and the premise message added for the
purpose named the leg exactly: *the menu item refused keyboard focus*.
The same runner focuses a `TextBox` in the same window without complaint
— every non-menu premise in the same facts passes — so this is specific
to menu elements, not to focus.

Two things were ruled out with evidence rather than argued away. It is
NOT window activation: two of the six arms use an IN-WINDOW menu and
failed identically. And it is not only the focus SCOPE: switching
`FocusManager.IsFocusScope` off on the `Menu` (a local probe shows the
two shapes reach keyboard focus by different mechanisms) did not save it.

**What the facts do now.** A menu row HOSTS a focusable `TextBox` and the
keys go there. The chain `TextBox → MenuItem → Menu` satisfies the
production predicate exactly — `ClassifyFocusLoss` walks ancestors for a
`MenuBase`, a TYPE test — using the one focus mechanism that desktop is
proven to support. No synthetic call, and **no production seam invented
for a test**: the classifier reads `Keyboard.FocusedElement`, so the only
way to reach it is to put the keys somewhere real, and a hook would have
been a production change made for CI's convenience.

**And if that is refused too, the arms are still not green for nothing.**
`TryPutTheKeysInTheMenu` answers whether the desktop allowed it. On
refusal it writes `DESKTOP REFUSED THE MENU` to the test output naming
the arrangement, and the arm asserts the REFUSAL INVARIANT — that
nothing moved, so the arrangement did not half-happen — and stops. That
is deliberately not a skip attribute: **a skip says "we did not look";
this says "we looked, the desktop said no, and here is what was true
anyway".** Proven both ways by mutation: with focus forced to refuse, the
thirteen arms pass and log six refusals; with the invariant flipped under
the same conditions, exactly those six go red.

**What is NOT proven on that desktop**, stated so nobody reads the green
as more than it is: that a reader can reach a menu there at all. Nothing
can prove it — the desktop refuses. What the arms prove is the thing
under test, which is that focus inside a `MenuBase` classifies as
`MenuOpen`.

**SECOND CI ROUND: 6 → 2, and the TextBox route is confirmed on that
desktop.** Four menu arms went green there, which settles the question the
previous round could only reason about: the runner refuses focus to a
`MenuItem` and allows it to a `TextBox` hosted in one. The production
predicate is exercised on CI by a real menu.

**The two survivors were both MY defects, not the desktop's.**

- **The diagnostic contradicted itself**, and that is the more useful
  finding. It reported *"the keys would not go into a MenuBase: focus()
  returned True and the thread's focused element is TextBox"* — a
  sentence that refutes itself in its own clause. Cause: the decision was
  taken on one sample and the MESSAGE was built by RE-READING the state
  afterwards, so it described a later world than the one that was
  judged. **A diagnostic that re-reads is not a diagnostic**; every value
  in that message is now the value the decision used. The self-refuting
  sentence is what made the defect findable, which is the argument for
  messages stated twice over.
- **Cross-window focus had not settled.** One arm hosts its menu in a
  second window on purpose (it is the only way to isolate the menu level
  from the keys-outside level), and on that desktop the transfer is not
  delivered by the time `Focus()` returns. The helper pumps the
  dispatcher before it decides. That was hypothesis (2) from the first
  round, unfalsified then and confirmed now.
- **The refusal invariant was the wrong invariant.** It asserted "nothing
  moved", which is false precisely when focus DID move — asynchronously,
  into the menu — so the arm went red on the path designed to be
  green-with-log. It now asserts the arrangement's INTEGRITY: if the keys
  landed on the row this fact built, production's own walk must agree
  that row is inside a menu. That can fail, and does — blinding the walk
  turns seven facts red — so the refusal path is still never
  green-for-nothing.

**ONE PREDICATE, NOT TWO.** The premise check no longer re-implements the
question: `CanvasSurfaceView.FocusIsInAMenu` is widened from private to
internal and asked by name. A test that re-implements the walk can
disagree with production about the very thing it is testing, and it did —
the arrangement said "not in a menu" about an element production would
have called in-menu. No behaviour is added and no state exposed; it is
the existing pure predicate, asked rather than copied. That is a
different act from inventing a hook, and the difference is that nothing
in production changed.

**And a seventh fact was found by the rule this wave wrote.**
`OpeningAMenuKeepsTheModeAliveAndLeavingTheCanvasCancelsIt` was not in
the original six, so it never got the shared arrangement — and it carried
a premise with NO message, the one rule this wave had just instituted at
46 other sites. It is on the shared helper now. **A rule applied to the
sites a failure named is a rule applied to a diff**, which is this
branch's oldest lesson arriving one more time, in the wave that wrote it
down.

**The open decision, which is not mine to take.** If the runner refuses
this arrangement as well, the menu classification becomes OS-unverifiable
on CI, and the choice is between accepting that with the refusal
invariant as the standing record, or adding a production-visible hook for
the predicate. I decline the second unilaterally: this branch has one
such static (`ShellOverlayIsOpen`) and it exists because the SHELL sets
it, not because a test needed it. Adding one for CI would be the
excused-configuration trap wearing a different coat.

**For PR E/F.** Row menus and context menus are E/F's work, and their
facts will meet this the moment they exist. The pattern that survives:
drive the classification through the plainest element the desktop will
focus, keep the real `MenuBase` in the ancestor chain, and assert the
premise with a message that names the leg. `ContextMenu` popups are a
separate HWND and are likely WORSE on that desktop than an in-window
`Menu`; budget a CI round trip for the first one, and write the premise
message before the first push rather than after.

**A correction to my own reasoning, recorded because it cost the round
trip's framing.** After the first CI failure I wrote that only leg (b)
would justify a labelled seam. That was too narrow: leg (a) says *that
element* refused focus, and says nothing about whether ANY element inside
a `MenuBase` would. Collapsing "the MenuItem refused" into "the OS route
is impossible" would have conceded the real route while a cheaper one
was untried. The distinction is the whole experiment, and it is the same
error this PR kept making one altitude up — a claim wider than its
evidence.

### PR C — the strategic lesson

One asynchronous requirement, inside a PR whose other twelve contracts
had nothing to do with it, took the whole PR hostage for seven codex
rounds, two rule-4 trips, three design passes and two user rulings.
Nothing above was wasted — the redesign PR starts from all of it — but
the sizing was wrong from the brief. **Size a PR by SUBSYSTEM, not by
requirement count**: the filter's async publish was a subsystem, and it
was scoped as one line item among thirteen.
