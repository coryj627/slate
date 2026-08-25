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

**A5 — `CanvasAnnouncer` is a relay and a clock, and nothing else.**
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
`⟨n⟩ of ⟨m⟩ in ⟨container‖canvas⟩[, ⟨color⟩][, marked]` — mac's
`nodeValue` (`CanvasOutlineView.swift:309–318`) minus the `, filtered`
clause, which has nothing to describe until PR C ships the filter.
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
   { Owner, NodeId, Generation }` lives on the document as
   `FocusRequest`. `RequestActiveEditorFocus` — the one funnel every
   user-initiated open calls and no background path does — raises it,
   addressed to the tab that asked. It stays pending until a surface
   delivers it and says so (`CompleteFocusLanding`), and a newer request
   supersedes an older one by generation, so a late delivery of a
   superseded request cannot clear a live one.
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
`ShowVisualRegistersAndStaysDisabledUntilItsProjectionShips` (renamed in
PR B, which enabled `showTable` — see B10);
`showTable` enabled in PR B and `showVisual` enables in PR D. None of the three
carries a chord, so `Scope` resolves to `None` through `Reg`'s own rule
and `ChordScope.Canvas` has no delivery site until PR C — which is why
the scope's doc comment names PR C as the first surface that uses it.

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
`ShowVisualRegistersAndStaysDisabledUntilItsProjectionShips` (PR B's
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
PR G) and **Delete** (disabled until PR E), each disabled one carrying
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
disabled with the registrar's canonical sentence until PR D; §A's fact
was renamed accordingly (`ShowVisualRegistersAndStaysDisabledUntilItsProjectionShips`)
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
projection until PR D, and PR A already round-trips a persisted
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
  until PR D, and each PR flips its own row rather than leaving a
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
`ShowVisualRegistersAndStaysDisabledUntilItsProjectionShips` and
`TheSurfaceSwitcherIsNamedAndTheUnshippedArmIsDisabled` carry §A's rows
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
convenience, never the only path — and rule R2 is why the chord half is
gated on a projection owning the keys.

**Movement rows stay ENABLED on a canvas in any load state.** The
navigator's whole job when the document cannot answer is to say so
(C4); a disabled palette row would replace an accurate sentence with the
registrar's generic unavailable one. The two MODE rows are the
exception and C9 records why.

**C2 — `ICanvasSurfacePresenter` is the only thing the navigator knows
about views.** Three questions (which projection, does it own the keys,
can it move one row) and three focus moves (a row, the projection,
dismiss a transient region). `CanvasSurfaceView` implements it and
routes to whichever projection is showing. Nothing sits on it that the
navigator does not call — "focus the filter field" was drafted onto it
and taken off, because Ctrl+F raises the document's focus TOKEN instead:
the field belongs to every pane showing the canvas and only the one the
reader is in should take it, which a presenter call would have got wrong
by picking whichever pane the navigator was holding.

Why a seam at all: the navigator is per document and focus is per view.
Why it is this narrow: everything else a verb does is model state and
announcements, which is what lets `CanvasNavigatorTests` drive the verbs
with no window at all and keeps the windowed facts to the things that
genuinely need one.

The presenter is ATTACHED when the surface gains keyboard focus and on
every key press, and it is kept afterwards — so a palette-invoked verb
still moves the reader in the pane they are actually in.

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
unreadable state rather than reading the mapping they go through — a
guard may not exercise the mechanism it is guarding, and the mapping's
own fact is separate.

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
| 2 `Filter` | the field holds a needle | clear it, announce `CanvasFilterCleared`, focus the projection |
| 3 `Surface` | a transient region is open or holds focus | close the Where-am-I panel, or the interim card detail, or leave the filter field — focus back to the projection |
| 4 `WorkspaceTab` | nothing above consumed it | NOT consumed; the press bubbles |

Rung 3's effect is a focus move and is deliberately unannounced: the
screen reader reads what focus lands on, and a line on top of that is
the t0 §1.5 doubling rule broken on a dismissal (the same reasoning as
A12's silent seat). Rungs 2 and 3 are registered ONCE by the navigator
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
`CanvasMode`, the `CanvasModeObject`, a commit effect returning the
confirmation EVENT and a cancel effect returning the
`CanvasModeRestoration`. Every sentence is core's (PR 0a). Nothing here
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
out, because a `ContextMenu` lives in its own popup — which covers the
menu bar, submenus and context menus with one question.
`OpeningAMenuKeepsTheModeAliveAndLeavingTheCanvasCancelsIt` drives a
real menu and a real non-menu focus loss in one fact, so it cannot pass
on a surface that stopped classifying departures at all;
mutation-verified by collapsing the arm back into `PaneFocus`.

**Menu-then-elsewhere is not re-classified, and that is the pre-existing
shape this widens rather than a hole it opens.** `IsKeyboardFocusWithin`
is already false once focus is in the menu, so a subsequent move from
the menu to some other part of the shell raises nothing and the mode
survives a departure that would otherwise have cancelled it. Exactly the
same has been true of `ModalOverlay` since this arm existed — dismissing
the palette onto another pane never re-fires either — so `MenuOpen`
inherits the property rather than introducing it. It is bounded, not
unbounded: the tab going invisible (`TabSwitch`) and the window losing
activation (`WindowDeactivated`) are separate triggers that still fire,
and document retirement cancels outright, so no mode outlives the thing
it belongs to. Recorded here so a future reviewer reads it as known;
closing it properly wants a focus-restored trigger, which belongs with
PR F's real modes rather than a test one.

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

**C10 — The filter is ONE view, and every consumer reads it.**
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

**`FilterActive` is mac's predicate, spelled out.** Foundation's
`.whitespaces` does not include newlines, so a needle of nothing but a
newline reads as ACTIVE on mac and (core trimming it) matches
everything. .NET's `IsNullOrWhiteSpace` would call the same needle
inactive, so `IsFilterActive` is written to mac's rule rather than
borrowed. It belongs to the trimming differences CD-22 already records.

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

**The MATCH runs off the dispatcher, and the `Filter` view is pure.**
It used to run inside the getter, on the UI thread, taking the `_ffiLock`
a LOAD body holds across `open_canvas` plus three whole-model
projections — so a keystroke arriving during a load blocked the
dispatcher for the length of that load, which on a large canvas over a
slow filesystem is a stall the user types into. It is a
`PanelWorkScheduler` body now, which is the convention A17 already set
for every other whole-model read in that class, and the getter reads
published state only.

Two paths still answer on the calling frame because they need no query:
an inactive needle IS the full outline, and an unchanged needle already
has its answer memoized — so CLEARING the filter (the Escape rung, the
Clear button, the palette verb) widens the rows immediately rather than
after a scheduler hop. Two generation guards, not one: a stale NEEDLE's
answer must not overwrite a newer one, and a stale DOCUMENT's must not
land at all, because a reload republishes rows the ids no longer
describe. A reload with an active needle re-asks, or the surface would
show every card while the field still claimed to be filtering.

**`Current` now has two causes and they get different answers.** "No
handle could answer" is a state the user needs told about — the mapping's
sentence, mac's behaviour. "The answer has not landed yet" is not: the
previous rows are still on screen and their count still describes them,
so the label lags by a frame the way every async surface does. Before
the FIRST answer there is no summary at all rather than a "9 of 9 cards
match" that would claim a match nobody made — which is why
`FilterSummaryText` returns null and the region hides. The COUNT is
announced from the publish rather than the keystroke, so it can never
describe rows that are not on screen; the announcer's filter class still
collapses a burst into one line.

**The cost of typing, stated rather than assumed.** One memoized
`canvas_filter` call off the dispatcher, plus ONE projection rebuild when
its answer lands. A rebuild at 2,000 rows is the same work PR A already
budgets and measures on the open path (A17's §K fact, in both scheduling
modes), so the per-keystroke cost is bounded by a number that is already
asserted rather than by a hope. The needle's own property change renders
the field's chrome only — Clear has to appear on the frame the reader
typed, not when the query returns.

**The body catches what the scheduler's contract says it must.** A
`bad_node` refusal is an answer; a PANIC-CLASS uniffi exception is not,
and letting one escape faults the tracked task silently — the single
route to a permanently stranded filter, where nothing publishes, the
rows never move, the summary never resolves and the needle sits in the
field describing nothing. `FilterBody` takes LoadBody's own filter
(everything but `OutOfMemoryException`, `StackOverflowException` and
`AccessViolationException`) and
posts the refusal path instead, so the failure is SPOKEN and the label
says the same thing. Pinned by
`APanicClassFilterFailureAnswersInsteadOfStrandingTheFilter`, which
injects at the query seam (`StructuralQueryFaultForTests`, the
`FailIdentityQueryForTests` idiom one subsystem over) because nothing a
fixture hands the real library makes it panic — with the premise
asserted first, and recovery asserted after: a failure caches nothing,
so the next ask answers normally.

**Guarded in the SOURCE** (`TheFilterQueryIsScheduledOffTheDispatcher`),
because a behavioural fact cannot see it: unit facts run in synchronous
scheduling mode, where a scheduled body and an inline one are
indistinguishable by construction, so the thing that regressed would be
invisible to exactly the tests written for it. The guard walks UP from
each call site to its enclosing MEMBER rather than down from the
methods — the bug lived in a property GETTER, which a
`MethodDeclarationSyntax` scan cannot see, so that scan would have
reported the one scheduler body and passed while a "fast path" in the
getter put the query back on the dispatcher. It asserts `canvas_filter`
has exactly one caller, names every caller it found when it does not,
and asserts the body is invoked only inside a `StartWork(...)` argument.

**A reload with an active needle announces its new count, deliberately.**
The re-ask is not silent bookkeeping: the rows changed under the user
while they were filtering, and the accurate number is information they
need — the alternative is a surface that quietly holds a different set
than the one last spoken. It is safe to say because it rides the FILTER
coalescing class, which is separate from the `navigation` class the
degraded-load line and every movement use (0a-8), so a reload's count
cannot collapse the load's own sentence or be collapsed by it.

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
the same sentence twice. Escape (ladder rung 3) closes it and returns
focus to the element the reader came from, with the projection as the
fallback when that element is gone.

Nothing selected falls back to the first row in reading order, and an
empty canvas answers `Canvas is empty.` — the pull surface always
answers, which is the failure t0 §1.4 exists to prevent.

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
The three menu items are checkable radio items, so a screen reader
speaks the selected level from the element itself (t0 §3's
inspectability, and the shape mac's Settings toggle has), and the honest
confirmation of "you are now at Verbose" is the next card you move to.
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

### Tests that pin PR C

`apps/slate-windows/tests/SlateWindows.Tests/CanvasNavigatorTests.cs`:
C1–C6 and C10–C14 against a REAL `VaultSession` and real `.canvas`
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
`TheSnapshotVisibilityPredicateMatchesTheSurfaceRender`,
`TheShellInstallsTheModalOverlayAnswerForModeCancellation` and
`TheFilterQueryIsScheduledOffTheDispatcher` — the three source guards
for properties no in-process fact can reach.
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

**The gate's set is core's, copied because core does not export it.**
`canvas::model::media_class` (`model.rs:661`) is the same private
function whose answer becomes the `image` kind label and the
`Image:`/`Audio:`/`Video:` title prefixes, and it carries no
`#[uniffi::export]`. `CanvasMediaPolicy` transliterates it in ONE place,
including both of its edge rules (the BASENAME's real extension; a
dotfile like `.mov` is a hidden file, not a video), and
`TheMediaGateIsCoresClassification` pins the set and both edges.
The lowercasing is core's `to_ascii_lowercase`, hand-written rather than
.NET's `ToLowerInvariant`: the two differ outside ASCII (the Kelvin sign
lowers to `k`, `İ` to `i̇`) and every difference ADMITS something core
calls not-media, which is the wrong direction for a gate deciding what
reaches `ShellExecute`.

**Half of it is pinned against core anyway, without waiting for the
export.** Core does not export the classification, but it exports one of
its ANSWERS: `kind_label` returns `"image"` exactly when `media_class`
says Image (`model.rs:646`), and that reaches the host as
`CanvasOutlineRow.kind`. `TheImageThirdOfTheGateAgreesWithCoresOwnKindLabel`
opens a canvas of file cards over every image extension the host set
claims plus six non-media ones, and asserts core's own row agrees in
both directions. The audio and video thirds have no exported answer —
`kind_label` calls them plain `"file"` — and stay unpinned until PR E.

**Drift note:** PR E is the first PR that needs the classification for
its own reasons (the spec's Add Media row — "media kinds by extension
set — core's `media_class` decides the label"), so PR E exports it,
deletes this copy, and retires the pin above with it.

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
can do nothing and say nothing. Windows announces
`CanvasFilterCleared{total}` unconditionally: the sentence is true of
the resulting state either way, and t0's never-silent rule is what
decides the tie. The Escape RUNG keeps the guard — a rung that consumed
a press without an effect would break "exactly one rung per press" by
swallowing the rung below it.

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

**CD-45 — A filtered-out group's surviving child is promoted to a root
row.** The Windows outline NESTS (CD-33) and the filter is a row
subset, so a card whose containing group did not match has no parent
row to sit under. The depth-stack pass makes it a root. The alternative
— indenting it under a group that is not on screen — would claim a
containment the reader cannot verify. Mac's outline is flat with
indentation, so it has no such case; recorded because a reviewer
comparing the two projections will see the difference.

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
- **`NothingSpeaksAfterTheLastTabClosed` fails in a DEBUG test run and
  passes in Release, at BASE as well as here.** It deliberately posts
  through a retired announcer, which contract A5 makes a `Debug.Fail`,
  and xUnit turns that into an exception. CI runs
  `--configuration Release`, where `Debug.Fail` compiles out, so the
  gate is green either way. Verified against a stashed tree so it is not
  read as a PR C regression; recorded because the next person to run the
  suite locally in Debug will see it.
- **Three build warnings are pre-existing on this branch**
  (`ModalSurfaces.cs:228` CS8524, `FilesSidebarViewModel.FileManagement.cs:1117`
  CS8604, `MutationHarnessCensus.cs:59` CS8620), all in files this task
  did not touch. `dotnet format --verify-no-changes` over the WHOLE
  solution also reports pre-existing whitespace in `WorkspaceViewModel.cs`
  and `ShellAccessibilityTests.cs` at lines this task did not edit — the
  recorded mixed-EOL trap, and the reason the DoD scopes the format gate
  to the changed files, which pass clean.
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
