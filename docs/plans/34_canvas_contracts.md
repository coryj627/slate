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
host recomputes any of them (R-D). The Swift copies die in Task 0b-2;
the Windows host has none to delete because it has not been written
yet, which is the point of ordering 0b before PR C.

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
existing `taken` guard from the untitled-ordinal loop, and it is what
makes the result injective: mac's loop checks only the names it has
already assigned, so document order `A`, `A`, `A 2` yields
`A`, `A 2`, `A 2` — a duplicate on a surface whose whole purpose is
Voice-Control uniqueness. Untitled cards are unchanged: they get
`display_title` = `Untitled N` from the same allocator, which already
skips taken spellings. Injectivity is a census assertion, not a claim
here, and the `A, A, A 2` case is a committed fixture.

**0b-6 — `speakable_name` reaches the outline and table by an
in-memory join, not a schema bump.** `canvas_outline` /
`canvas_table_rows` are single indexed `SELECT`s over `canvas_nodes`;
`speakable_name` is added to their row records by looking the node up
in `OpenCanvasState.model.summaries` after the query returns —
a `HashMap` hit per already-materialised row, so the §K "one indexed
query" budget and the query's shape are both untouched. **Rationale for
the decision the spec does not make:** (1) a derived column would put a
second copy of the algorithm's OUTPUT in the index, where it can rot
against the model the same rows are derived from — 0b-1 exists to stop
exactly that; (2) a column costs a migration plus the upgrade fence
(`c559810`) plus a full canvas reindex on upgrade, for data that is
already in memory whenever a handle is open — and outline/table rows
are only reachable *through* an open handle, so the model is never
absent at the join; (3) no existing row field changes, so no committed
golden or host binding moves. The cost is that the two row types are no
longer pure projections of one `SELECT`; that cost is named here rather
than hidden. `CanvasSceneNode` and `CanvasWhereAmI` are model-backed
already and simply read the new field.

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
fully inside the group rect are candidates. They are visited in `(y, x)`
order, which is `place_new`'s `Below` → `RightOf` preference and the
model's own sibling order; `Above`/`LeftOf` are unreachable because the
lattice starts at the group's inset top-left. The outcome is one of
three, so a host never receives a point that is outside the group it
asked about:

| Outcome | When |
|---|---|
| `Placed { x, y }` | a candidate slot is free of card overlap (`exclude` honoured; group frames never block, as in `place_new`) |
| `TooSmall { x, y }` | **no** candidate slot fits inside the group — the fallback point is the inset itself, unchecked for overlap |
| `Full` | slots fit but every one examined is occupied |

The scan examines at most `placement::RING_LIMIT` candidates, the same
budget `place_new`'s ring search spends, so a pathological group cannot
make the query unbounded.

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

**0b-14 — Case folding and trimming diverge from Foundation, by a
recorded amount** (CD-22). Core folds with Rust's `to_lowercase`
(full Unicode simple lowercase, locale-INdependent) where mac uses
`localizedCaseInsensitiveContains` (locale-sensitive, no full folding).
The two agree on ASCII and Latin-1 — which is the entire fixture
corpus, so the §W-A census cannot see the difference — and diverge on
the Turkish dotless ı and on `İ` (U+0130), whose Rust lowering is two
scalars. Core trims with Rust's `trim` (all Unicode whitespace,
newlines included) where Swift trimmed `.whitespaces` (no newlines). A
test pins which side wins on each of those, so the divergence is
witnessed rather than asserted.

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
runs the same queries over it.

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

---

## §W-G canonical-consumption audit (seeded from the spec §2 table; closed in PR H)

Tier 1 and 2 move to core with the mac consuming the new API in the same
PR and the Swift derivation deleted (decision 5). Tier 3 is
host-by-designation — recorded so the audit is explicit, not silent. PR
H re-greps `apps/slate-windows/src/SlateWindows/Canvas/` against this
table.

| # | Swift-derived pocket (file:line) | Core today | Tier | Target (PR) | State |
|---|---|---|---|---|---|
| A | The entire announcer grammar: verbosity matrix, group entered/left, connection traversed (direction phrases, duplicated again in `CanvasOutlineView.swift:335–347`), confirmations, destructive "— ⌘Z to undo", error, filter count, mode entered/cancelled/committed, undid/redid, Where-am-I readback (`CanvasAnnouncer.swift:104–213`, `AppState+CanvasActions.swift:328–819` prose sites, `AppState+CanvasConnect.swift:62,127–181`); preset-name dictionary duplicated (`AppState+CanvasActions.swift:340,766`) | `HostComposed` residue; core supplies every payload (`CardSummary`, `Neighbor.direction`, `RelativeDesc`, `color_name`) | **1** | `A11yEvent::Canvas*` family + `CanvasVerbosity` (0a) | **closed** — core half + mac consumption both landed (0a-1, 0a-2) |
| B | Relative-position description in move mode — nearest neighbours by squared centre distance, `Below "X", right of "Y"` (`AppState+CanvasModes.swift:299–339`) | `RelativeDesc` exists for placement only | 2 | `canvas_describe_relative(h, rect, exclude) -> Vec<CanvasRelativeDesc>` (0b) | **core half landed (0b-1)** — 0b-7; mac consumption is 0b-2 |
| C | Auto-side selection for new connections, **two copies** (`AppState+CanvasConnect.swift:24–33`, `CanvasRendererView.swift:582–600`) | none | 2 | `canvas_auto_sides` (0b) | **core half landed (0b-1)** — 0b-3, rect-keyed (CD-16); mac consumption is 0b-2 |
| D | Containment / parent-group resolution, **three copies** (`AppState+CanvasCreate.swift:296–305`, `AppState+CanvasExtras.swift:131–147`, `AppState+CanvasActions.swift:439–441`) | `GroupTree` exists, not exposed | 2 | `canvas_parent_of`, `canvas_children_of` (0b) | **core half landed (0b-1)** — 0b-8; mac consumption is 0b-2. CD-4 no longer needs `children_of` (0a-2 deleted the walk) |
| E | Enter/exit group + group card count from outline `depth` walks; trace-path walk (`AppState+CanvasNavigation.swift:55–90,129–156,170–181`) | `reading_order`/`adjacency` exist | 2 | D's queries + `canvas_trace_path` (0b) | **core half landed (0b-1)** — 0b-8, 0b-9; mac consumption is 0b-2 |
| F | Selection model + reading-order re-projection of the marked set | none (correct) | 3 / 2 | host state; `canvas_order_nodes` (0b) | **core half landed (0b-1)** — 0b-10; mac consumption is 0b-2 |
| G | Undo/redo stacks, depth/session policy, menu-title composition (`AppState.swift:3987`) | `apply()` returns inverse + names | 3 | host stack; **menu title** = `CanvasUndoMenuTitle{verb,name}` | **menu title landed (0a)** |
| H | Placement math leaks; `MIN_CARD_SIZE` only in Swift | constants in `placement.rs` | 2 | `canvas_constants()`, `canvas_group_rect_around`, `canvas_place_inside_group`, `canvas_bounds` (0b) | **core half landed (0b-1)** — 0b-4, 0b-11, 0b-12; mac consumption is 0b-2 |
| I | Viewport math — clamp 0.1–4.0, step 1.25, fit padding 40/120 | none | 3 | host rendering; constants pinned here; zoom % announced via `CanvasZoom` | **event landed (0a)** |
| J | Table column order/sort comparators/summary sentence; outline interleave | rows from core | 3 | host projection config; summary sentence stays a **static label** (never announced on mac) | resolved as label class (0a-13) |
| K | Filter predicate — title/kind/groupPath/target, case-insensitive contains | none | 2 | `canvas_filter` (0b) | **core half landed (0b-1)** — 0b-13, 0b-14; mac consumption is 0b-2 |
| L | Speakable-name dedup vs core's untitled-only allocation — two uniqueness algorithms | partial, conflicting | 2 | one algorithm in core: `CardSummary.speakable_name` (0b, D-3) | **core half landed (0b-1)** — 0b-5, 0b-6, CD-20, CD-23; mac consumption is 0b-2 |
| M | Node/edge id minting | none | 2 | `canvas_new_id()` (0b) | **core half landed (0b-1)** — 0b-4; mac consumption is 0b-2 |
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
the inset, core ring-searches the interior. Coordinates are never
announced, so no §W-D string moves; §W-A pins cross-host equality of the
NEW rule. The spec is also silent on the group that fits slots but is
full, which mac leaves undefined — 0b-12's third outcome (`Full`) closes
it rather than returning a point outside the group.

**CD-22 — Case folding and whitespace trimming are Rust's, not
Foundation's.** `canvas_filter` folds with `str::to_lowercase` (full
Unicode simple lowercase, locale-independent) where mac used
`localizedCaseInsensitiveContains` (current locale, no full folding),
and trims with `str::trim` (all Unicode whitespace) where Swift trimmed
`.whitespaces` (newlines NOT trimmed). Identical on ASCII and Latin-1,
which is the whole fixture corpus; the recorded difference is Turkish
dotless ı, `İ` (U+0130, whose Rust lowering is `i` + combining dot), and
a query carrying a newline. The spec asked for this divergence
explicitly; it is pinned by test rather than merely described.

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
surfaces reading `title`. Both hosts do the same thing, which is what
parity requires; the field being available on all four is what stops a
later host from re-deriving it.

**CD-24 — `canvas_group_rect_around` returns `Option`.** Mac's bbox
fold aborts on `guard minX.isFinite` when no member resolves — a silent
no-op with no announcement at all. `None` is that outcome typed, so a
host can decide what to say instead of inheriting silence by accident.
The non-empty case is byte-identical arithmetic.

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
