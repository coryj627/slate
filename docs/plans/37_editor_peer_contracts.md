# W7-1 editor Text-pattern contracts (#747)

Scope: [W7 executable spec](18_windows_port/specs/w7_spec.md) §2,
the editing surface, one PR. G29 is Windows-first editor semantic reading;
the mac counterpart is [#1224](https://github.com/coryj627/slate/issues/1224).
This document precedes the implementation review as its own commit.
Code citations identify implementation seats. The contract-only commit was
`f328dec7`; the following implementation adds those seats and their witnesses.

## Contracts

**E-1 — One canonical source.** `EditorSemanticTextProvider` and
`EditorSemanticTextRange` (`EditorSemanticText.cs`) read the editor's
`AvalonDocumentBufferSession.InspectInRange`. That method calls the same
`DocumentBuffer.HighlightInRange` as the colorizer's query and does not
replace `LatestHighlightWindow`. There is no second FFI entry point, host
Markdown classifier or speech builder. Hyperlink inventory validation is cached only at the current stable revision; the paint window is never read by the peer. Offscreen
range queries are supported independently of the viewport.

**E-2 — Preserve the text engine.** `SlateTextEditorAutomationPeer.GetPattern`
decorates the base Text provider. Text, selection, movement, comparison,
search, rectangles and selection mutation remain AvalonEdit operations.
Every returned range is decorated, including Clone and FindText; operands
of Compare, CompareEndpoints and MoveEndpointByRange are unwrapped.
SupportedTextSelection remains Single. Unsupported attributes return
`AutomationElement.NotSupported`, never null. Unsupported FindAttribute
requests return null. The pinned library's three missing provider methods
are handled as recorded in divergence A-1 below.

**E-3 — Coordinates and lifetime.** Attribute ranges use the raw range's
UTF-16 endpoints, never a whole-document GetText copy or character movement
to infer offsets. The adapter accesses AvalonEdit's private segment through
one cached reflection boundary and pins the package shape in tests (A-2).
FFI offsets remain core byte offsets converted by `EditorSpanMapper`.
Queries verify dispatcher access before touching the session. A provider
retained across an editor document/session replacement must not read the
replacement document with the old range's offsets.

**E-4 — Attribute mapping.** The table below is exhaustive over the sixteen
canonical variants; the doctrine census compares it with the mapping
switch. Attribute identity is evaluated over the queried interval, including
plain gaps. Uniform values are returned as values; differing values return
`TextPattern.MixedAttributeValue`. A degenerate caret range uses the
character at the caret (and the preceding character at document end).
Nested canonical spans contribute independently to different attributes;
for one attribute, the most specific applicable span wins, so a heading
inside a block quote retains its heading level. Plain text is unsupported.
Hyperlink identity follows a surviving canonical span through document edits; stale membership is never inferred from surviving anchors alone. Queries do not invent font families: the editor palette
changes foreground brushes, not font families.

| Canonical kind | UIA attributes |
|---|---|
| Heading | StyleId = 70000 + level (1–6); StyleName = Heading N |
| Wikilink | StyleName = Wikilink |
| Link | StyleName = Link |
| Embed | StyleName = Embed |
| Image | StyleName = Image |
| Tag | StyleName = Tag |
| Citation | StyleName = Citation |
| InlineCode | StyleName = Code |
| CodeFence | StyleName = Code |
| Code | StyleName = Code |
| BlockQuote | StyleId = 70014; StyleName = Quote |
| Emphasis | IsItalic = true |
| Strong | FontWeight = 700 |
| Strikethrough | StrikethroughStyle = Single |
| Comment | StyleName = Comment |
| Frontmatter | StyleName = Frontmatter |

Tags and citations are links under the owner's accepted recommended
defaults (2026-09-17); both already activate with Ctrl+Enter. No editor
navigation chords are added. StyleName exposure does not imply a reader
speaks styles at stock settings.

**E-5 — Attribute search.** FindAttribute supports StyleId in
both directions within the calling range, returning a decorated matching
range and null when absent. Search respects range boundaries and canonical
span identity. It cannot return a span that does not overlap the query.
The rest of FindAttribute remains unsupported. No caret or selection
mutation is needed to answer a search or attribute read.

**E-6 — Canonical semantics include structural overlays.**
`crates/slate-core/src/editor_spans.rs::highlight_spans` currently filters
Link, Image and BlockQuote from the shared query. This PR preserves them
as semantic overlays after the existing visual overlap resolution; it does
not introduce a separate accessibility parse. Frontmatter, comments and
code retain their suppression rules. Container spans may overlap their
contents without suppressing those contents' paint spans. Both native
`EditorSyntaxPalette` implementations already leave these three kinds
uncoloured. Full/window differential tests and shared parity goldens must
agree on the new span set. A shared fixture includes all sixteen kinds.

**E-7 — Stable reads during edits.** Attribute queries return NotSupported
and attribute searches return null while a peer update or IME composition
is open, without calling the FFI. The availability check covers the whole
editor conservatively rather than guessing an affected subrange. A stable
query sees one current canonical window. The existing integrity/drift guard
remains authoritative; the decorator does not silently repair the buffer.

**E-8 — One deferred change notification.**
`AvalonHighlightingCoordinator`'s existing 40 ms debounce owns publication.
Any number of edits before a tick yields one TextPatternOnTextChanged for
the latest stable revision, after EndPeerUpdate and composition completion.
Scroll, resize, theme changes and no-op ticks do not publish text changes.
An unavailable tick retains the pending change. Disposal ends publication.
AvalonEdit's native selection events remain redirected to the owner peer.
The deferred TextChanged is followed by a selection notification so clients
can refresh selection against the committed text. No ValueProperty change
storm is added; the pinned base peer does not raise such a text-change event.

**E-9 — One text owner, ordinary Hyperlink descendants.** The editor remains
one Document/Text owner; TextArea.EventsSource remains the editor peer.
Wikilink, Link, Embed, Image, Tag and Citation spans project to ordinary UIA
Hyperlink peers. GetEnclosingElement returns the smallest enclosing link or
the editor. Range GetChildren returns intersecting immediate descendants of
that enclosing element, excluding the enclosing element itself. RangeFromChild
accepts only current descendants of this provider, returning ordinary decorated
ranges. Nested spans form a containment tree; equal intervals choose canonical
kind order, and crossing intervals retain the earlier outer interval, suppressing
the crossing peer (text/style attributes remain canonical). Forward and backward
UIA3 walks terminate and agree, including offscreen and nested links.

Every edit invalidates semantic validation. Candidate identities use AvalonEdit's
edit-tracking segment index; a local query revalidates its canonical AppliedRange
at the current revision. AppliedRange is NOT an edit invalidation delta. Full
root enumeration may inspect the whole document once per stable revision;
range queries and peer connection must not trigger full root enumeration.
Reconciliation uses indexed overlap lookup and dictionary matching, never a
per-span scan of all peers. WPF child caches are reset at the stable E-8 tick,
with one ChildrenInvalidated structure event when the tree has been exposed.

All observable peer properties validate current session and canonical membership.
Disabled editors reject Invoke; read-only editors still permit navigation.
Invoke queues the existing interaction action and raises Invoked when dispatched;
a child, session or interaction owner removed before dispatch cancels safely.
Saved, current canonical OutgoingLink records provide meaningful read-only Value
(destinations), using existing hash/generation/source-current guards. Dirty or
unavailable records expose no guessed destination; save/loading feedback remains
the existing interaction policy. Tags/citations expose their action without
invented URIs. All six kinds must exercise the real production activation path,
including ordinary Markdown links/images. No new editor navigation chords.

**E-10 — Evidence is executable.** Tests:
`EditorSemanticTextRangeTests` exercises all kinds through a real session,
mixed/plain/degenerate ranges, overlapping attributes, both-direction
search, Unicode offsets, read-only paint-cache behavior, session lifetime,
dispatcher access, IME and peer updates, and a twenty-edit batch.
`EditorPeerDoctrineCensus` pins the single query boundary, exhaustive table,
and absence of host classification and HostComposed.
`EditorTextPattern_SemanticAttributesUnitsAndEvents_AreClean` is the real
cross-process FlaUI/axe witness for the fixture, attributes and searches,
Line/Word/Character units, RangeFromPoint, selection and batched changes,
and the bidirectional tree walk. The fixture ships with gate binaries.

**E-11 — Measure bounded reads.** BenchmarkDotNet measures line-range
StyleId at 100 KiB, 1 MiB and 8 MiB, plus full document Hyperlink inventory at 8 MiB, local post-edit lookup,
and dense-link inventory/traversal. Local reads must remain bounded after edits,
including edits that reclassify distant syntax. Explicit full enumeration may
pay for all spans; returning a local peer must not secretly enumerate the root. Measurements, runner details and budgets are recorded below
and in BENCHMARKS.md; no unmeasured result is a pass.
The benchmark validates its complete case inventory and a line-query
flatness bound. A full-document query may pay for all its spans; a line
query must not copy or scan the whole document to discover its offsets.

**E-12 — Human evidence is separate.** Update the Editor document matrix
row and W2 checklist with the executable twins and explicit NVDA/JAWS
checks for line/word/character reading, say-all, semantic boundaries,
selection, braille and IME. Named runs include reader/version, OS, commit,
corpus, tester, date and transcript. The owner agreed to both AT checklists;
unexecuted results stay Pending. JAWS StyleId/Hyperlink consumption is an open
measurement, not an assumed compatibility claim. Narrator remains the W8
release smoke pass. No custom AT layer is load-bearing.

## Recorded dependency differences and accepted scope

**A-1 — The pinned provider has missing methods.** Package
AvalonEdit 6.3.1.120 identifies repository commit
`862415d51eddc9eac93f462dbc522ffbf929cd52`. Its
[TextAreaAutomationPeer](https://github.com/icsharpcode/AvalonEdit/blob/862415d51eddc9eac93f462dbc522ffbf929cd52/ICSharpCode.AvalonEdit/Editing/TextAreaAutomationPeer.cs)
throws NotImplementedException from GetVisibleRanges, RangeFromPoint and
RangeFromChild. This corrects the spec's assumption that forwarding them
is sufficient. The adapter uses AvalonEdit's visible lines and point
geometry, creating AvalonEdit ranges at the resulting offsets. Source
editor ranges contain no children, so RangeFromChild rejects an invalid
child with ArgumentException. Other members continue to delegate.

**A-2 — Endpoint comparison is ordering, not distance.** The pinned
[TextRangeProvider](https://github.com/icsharpcode/AvalonEdit/blob/v6.3.1/ICSharpCode.AvalonEdit/Editing/TextRangeProvider.cs)
stores an ISegment privately and exposes no offset accessors.
CompareEndpoints returns the sign of integer comparison. Character movement
uses caret boundaries, not UTF-16 distances, and GetText allocates text.
One cached reflection adapter over the segment and native range constructor
is therefore deliberate, as in Reading/HeadingStyleText.cs's dependency
adapter. Tests fail loudly on a package-shape change. This is not a second
text implementation or a reason to scan from document start.

**A-3 — Selection events can precede the deferred text event.** The pinned
TextArea peer raises selection events immediately on caret and selection
changes. Those events are preserved. E-8 guarantees TextChanged precedes
the following post-batch selection event; it does not claim to suppress or
reorder native events that already happened during typing.

**A-4 — Speech settings and platform scope.** NVDA's
[UIA format-field consumer](https://github.com/nvaccess/nvda/blob/5ba9521/source/NVDAObjects/UIA/__init__.py)
derives heading-level from StyleId and link from the Link attribute. Style
names and font attributes depend on reporting settings. Actual stock-reader
speech remains a human gate, independently of attribute conformance.
Mac editor semantic reading is absent and tracked by #1224, not emulated as
a Windows target. G29 records this Windows-first delivery. The shipped
W3 reading decorator is the range-wrapping precedent, not a new editor
navigation surface.

## Measurements and review record

First measurement, 2026-09-17: BenchmarkDotNet 0.15.8, three warmups,
ten measured iterations; Windows 11 build 26200.9457, .NET 10.0.12,
SDK 10.0.401, QEMU virtual CPU 3.19 GHz (12 cores). A real native peer and
session live on an STA dispatcher; the measured operation includes dispatcher
marshaling. The fixture repeats heading/prose blocks and ends with one link.

| Operation | Document | p50 | Budget |
|---|---|---|---|
| Line StyleId | 100 KiB | 0.0363 ms | 0.5 ms |
| Line StyleId | 1 MiB | 0.0328 ms | 0.5 ms |
| Line StyleId | 8 MiB | 0.0331 ms | 0.5 ms |
| Document FindAttribute(Link) | 8 MiB | 321.2658 ms | 1000 ms |

The 8 MiB / 1 MiB line ratio is 1.01x, below the frozen 4.00x ceiling.
Line queries allocate 3.49 KiB; the full-document search allocates 70.1 MiB,
including the canonical full span query. The runner rejects missing or duplicate
benchmark cases. Reference-definition fallback is deliberately not claimed to
be a bounded line read: A-5 states its whole-document cost.

**A-5 — Reference definitions have document-wide scope.** Retaining canonical
Link/Image spans makes an isolated reference-link window insufficient when
its definition lies elsewhere. `StructureSnapshot` maintains a conservative
index of `]:` marker positions over the edit halo, sharing the adjacent-byte
index maintenance with the lone-CR guard. A nonempty marker index makes the
canonical query use its existing full-document fallback. Even a marker in
code can cause a fallback; the parser, not the marker, determines semantics.
This trades bounded reads in such notes for exact full/window agreement;
ordinary heading/prose reads retain their measured flatness. The tests cover
references before/after the window and insertion/removal at marker boundaries.
No host classification or second query path is added.
Append each invariant-targeted review's findings and resolutions here,
following [the red-team protocol](24_red_team_protocol.md). Three successive
blocking rounds in one subsystem trigger a design pass; a blocker created
by the previous fix counts twice.


**A-6 — Superseded generic Link attribute experiment.** The original Link
VARIANT exporter could not provide symmetric ordinary-range operands across
WPF's COM boundary. The isolated Hyperlink prototype and actual NVDA evidence
are recorded in the [architecture research](18_windows_port/reports/w7_1_editor_accessibility_architecture_research.md)
and [prototype validation](https://github.com/coryj627/slate/blob/5d3b2a7f3bf0f73fba23c9d2028aa3a2210236ac/apps/slate-windows/tools/EditorHyperlinkPrototype/NVDA-VALIDATION.md).
On 2026-09-18 the owner authorized production integration in the same PR.
The generic Link attribute, its FindAttribute overload and unsafe COM exporter
are removed. Native Hyperlink children are the link semantics path. Prior
measurements/review records below describe the superseded implementation.
Fresh production evidence is required; prototype tests are not production acceptance.

**A-7 — Bounded peer connection.** WPF's internal TrySetParentInfo is used
through one pinned reflection adapter to connect an already canonical child to
its actual parent without enumerating the whole document. It copies only the
parent/HWND information WPF's own GetChildren would supply. The same adapter
invalidates WPF's ChildrenValid bit: public ResetChildrenCache eagerly rebuilds
the tree and therefore cannot serve the bounded publication path. Package-shape and
cross-process tree tests pin this boundary; no invented parent is permitted.

**A-8 — Retained selection diagnosis.** The prototype's selected-link deletion
changed the document correctly but NVDA announced later text as unselected.
A native AvalonEdit versus decorated retained-range regression must establish
the fault before changing endpoint tracking. Text units and geometry stay native;
any endpoint tracking correction must survive delete, undo and range movement.
The deterministic regression reproduced exactly `after.\nEmbe` on the pinned
native GetSelection range: that path supplies a SimpleSegment while the offset
constructor uses AnchorSegment. Wrapping now normalizes only nontracking native
segments to native anchors. Delete, undo and subsequent movement pass; the
control witness still reproduces the original text. Live-synth NVDA confirmation
remains pending until the owner's sound setup works.

### Review round 1 (head 21fe5962)

Standards: three findings (E-2/E-3 WPF Link marshaling, E-8 startup painting
canceling the first edit notification, E-3 retained-provider point geometry).
Spec: four findings (the shared E-8/E-3 findings, E-6 comments surrounding
fences losing opaque coverage, E-2/E-7 visible ranges disappearing during IME).
Resolutions: A-6 supplies WPF's wrapper; painting no longer stops the batch
timer; all document-based range creation checks the retained provider's
lifetime separately from semantic-read availability. Native geometry remains
available during IME/peer updates, including a character clipped at the right
edge. Opaque coverage is preserved before visual conflict resolution, with
literal percent markers beginning in code excluded from comment coverage.
Targeted regressions exercise each case, including twenty native edits before
the initial dispatcher callback and comments containing links/images/quotes
before a nested fence. Full/window differential coverage remains required.

Codoki round 1: accepted deterministic benchmark-startup diagnostics. The
claimed C# compile errors were disproved by the .NET 10 CI build (Order and
collection expressions are supported). CI instead found that the doctrine
census parsed a later benchmark-table header as a canonical kind; parsing now
selects the exact attribute table. The proposed removal of VerifyAccess was
rejected: TextDocument and the byte-offset index are dispatcher-owned, and
WPF marshals external UIA calls. A-6 documents the exceptional Link attribute
route and its remaining operand limitation; exported reads are exercised off-thread.
The raw internal session method deliberately retains its thread guard.


### Review round 2 and opaque-region design pass (head 29dbeabe)

Standards and spec independently found one E-6 blocker in the same subsystem:
raw InlineCode was allowed to veto a higher-priority Comment. Two comments
containing one backtick each could be joined by raw Markdown into one code
span; the second comment's Link then escaped its mask. The simpler retained
Comment plus escaped Link case also contradicted visual precedence. This
was created by the round-one fix, so rounds 1 + 2 reach the protocol's
weighted three-round stop. The following design is recorded before code.

1. Preserve the established paint precedence: Frontmatter > CodeFence >
   Comment > InlineCode. In particular, percent markers inside inline
   backticks retain their existing comment meaning; W7 does not introduce
   a new code-first comment dialect. Raw InlineCode cannot veto a comment.
2. Classify comments once, before deriving semantic masks. A possible opener
   inside higher-priority frontmatter or fenced/indented code is literal and
   skipped BEFORE pairing. A real opener consumes the next lexical percent
   pair as its close, even when a raw Markdown fence occurs in between.
   Unterminated openers emit no Comment. This preserves a real comment's
   complete opaque coverage when a nested fence wins visual resolution.
3. Feed these same canonical Comment spans into both paint resolution and
   the opaque semantic mask. The mask retains complete comment coverage;
   it never reclassifies an already-paired comment using raw inline code.
4. The incremental CommentIndex is a conservative window guard, not a
   classifier. Cache every adjacent non-overlapping lexical percent-token
   pair, including the pair that bridges the old close/open parity. Every
   canonical comment is one such pair after literal openers are skipped.
   A window between otherwise independent comments may therefore fall back
   unnecessarily, but a skipped code marker cannot hide a genuine comment
   from the guard. The existing percent-edit rescan / ordinary-edit shift
   model remains sound for these overlapping candidate intervals.
5. Regression witnesses cover comments joined by backticks, a retained
   Comment with an escaped Link, literal fence/frontmatter markers before a
   genuine comment, a real comment surrounding fences, Unicode/CRLF, and
   full/window equivalence inside comments across blank paragraphs. The
   cached guard must still match a fresh scan after arbitrary edit sequences.

This is one core classification and the existing full-document fallback,
not an accessibility-only parser. The cost is conservative fallback between
percent markers; ordinary heading/prose benchmark cases are unchanged.

The regression first failed on 29dbeabe with `[hidden](x)` exposed from the
second comment. With the shared classification repair, all 83 editor-span
and edit-sequence tests pass. The expanded focused witness also passes
UTF-8/UTF-16, CRLF, and live DocumentBuffer window equivalence. No further
native ABI, ownership, dispatcher, geometry or batching blocker was found
by either review axis. A-6 remains pending owner acceptance.


### Review round 3 and complete comment coverage (head 4ed119b8)

Spec reported no new findings. Standards identified the remaining whole-span
paint interaction: a fence dropped an entire surrounding Comment, leaving a
Heading (and other lower-priority prose kinds) outside the fence available to
the new semantic reader. The round-two repair fixed added overlays but did
not yet make canonical comment coverage complete for all sixteen kinds.

Design extension, recorded before code: when higher-priority paint occupies
part of a Comment, retain its uncovered fragments instead of discarding the
whole Comment. CodeFence keeps its existing higher priority and token paint;
Comment fragments keep every other part of the body opaque to Heading, Tag,
Wikilink, Citation, InlineCode and formatting. The existing complete comment
mask still suppresses structural overlays across that boundary. Both native
hosts consume the same corrected spans. Fragment boundaries inherit existing
UTF-8-safe canonical boundaries; ordinary span conflict resolution is unchanged.
A regression must assert every retained kind across a comment containing all
prose styles and a fence, while preserving visible heading/link text afterward.

The new all-prose regression reproduced the leaked Heading before the repair;
all 84 editor-span and edit-sequence tests now pass. The earlier Windows
provider/doctrine/parity run passed all 44 facts with the round-two core.
The independent native probe also verified the A-6 operand limitation for
MoveEndpointByRange under both CUIAutomation and CUIAutomation8; cloning
first works. The owner-pending scope now names all three operand methods.


### Review round 4 and raw-candidate precedence (head a8259299)

Standards found no new issue. Spec found two E-6 frontmatter interactions:
a raw fence starting inside YAML could cross into the body and steal its
comment opener, even though Frontmatter later discarded that fence; raw
code tokens were also appended after their containing fence was discarded.
The crossing-fence regression reproduced both an escaped Link and an orphan
Code token. These cases require the following design extension before code.

Raw Markdown output is a set of candidates, not the authority for retained
code coverage. Frontmatter is a prefix and wins over every intersecting
CodeFence. Comment classification therefore excludes such discarded fences
from its literal-opener ranges. After visual resolution, semantic code masks
use retained InlineCode/CodeFence spans, while full canonical comments remain
available separately (their paint may be fragmented). Finally, code tokens
are overlays only when contained by a retained CodeFence. This orders each
classification after its higher-priority inputs and prevents discarded raw
structure from affecting lower-priority or overlay semantics.

Codoki proposed skipping literal comment closers as well as openers. That
would change the documented existing lexical close rule. A dedicated witness
will pin a real comment closing inside a raw fence and preserve visible prose
after the fence. This is a deliberate grammar choice, not an ignored error.

Codoki also questioned UTF-8 safety of comment fragments. Their endpoints
already come from coverage transitions at canonical boundaries. The sweep
now explicitly rejects any candidate whose endpoints are not UTF-8 character
boundaries before it can alter coverage, making that invariant local and
executable. A malformed-candidate witness and multibyte comment/fence witness
pin the guarantee; no endpoint rounding can change the accepted coverage.

Both frontmatter regressions failed before the repair. All 88 editor-span
and edit-sequence tests now pass, including the lexical-close, Unicode and
malformed-candidate boundary witnesses; workspace Clippy is clean.


### Review round 5 and final local verification (head 7d7f4f13)

Both independent review axes reported no new actionable finding. The final
constructor parameter names distinguish retained paint from full comments;
this makes the reviewed data flow explicit without changing its behavior.
All 88 Rust span/edit-sequence tests and Clippy pass. With the rebuilt native
library, 44 Windows provider/doctrine/parity facts pass and the real desktop
UIA/axe editor journey passes, including actual exported Link methods.
A restricted sandbox launch could not enumerate FilesTree; the normal Windows
desktop run passed in nine seconds. This was not counted as a product pass
until the actual UIA journey completed.

The benchmark rerun after the complete core repair passed all frozen budgets:
line p50 0.0332 / 0.0338 / 0.0337 ms at 100 KiB / 1 MiB / 8 MiB;
8 MiB FindAttribute(Link) p50 321.3668 ms; line flatness 1.00x.
The full Windows suite with the final export adapter had already passed
2,976 tests; final-head CI remains the merge gate. A-6 and named human AT
runs remain pending. No review or automated check supplies that acceptance.

### Hyperlink integration design record (2026-09-18)

Preparatory independent reviews of prototype 5d3b2a7f found unbounded per-edit
rebuilds, quadratic matching/membership, missing WPF cache reset, stale deferred
Invoke, missing destinations/actions, and overlapping peers. E-9 now specifies
revision-local validation, indexed identity candidates, ordinary range operands,
consistent containment, guarded actions and metadata. No old/new AppliedRange
union is trusted: list-container edits can reclassify distant paragraphs.
Production implementation and review evidence follow in later commits.

Production integration first verification: 52 provider/doctrine/interaction facts
passed, including canonical Markdown/reference/autolink/image destinations and
activation. The actual cross-process UIA3/axe editor journey passed with seven
Hyperlink descendants, symmetric ordinary range comparisons, destination Value,
RangeFromChild roundtrips and agreeing forward/backward walks. Full unit suite,
new nine-case benchmarks, CI and renewed reviews are still in progress.

### Hyperlink integration review round 1 (head 7c23147d)

Standards confirmed replacement leaving cached old children, first enumeration
during a peer update retaining an empty tree, native movement discarding anchor
tracking, and a growing tag losing its peer identity. Spec independently confirmed
line/paragraph expansion losing tracking and a 512-unit Name cutting a surrogate.
Red witnesses reproduced stale replacement children in a headless editor and
expanded deleted lines reading the next line; the visible case was masked by
incidental layout. Fixes invalidate owner/availability transitions independently
of TextPattern lookup, publish replacement structure after binding settles, mark
even unavailable enumeration as exposure, normalize native mutating operations,
and bound names at a complete Unicode scalar. Exact interval identity is tried
first; only an unambiguous canonical surviving prefix can retain a growing span.
New tests cover headless/visible replacement, EOF deletion, Line/Paragraph deletion,
empty IME completion, peer-update first enumeration and tag growth versus adjacency.

The full-suite census also found the new EditorLink automation-ID construction
missing from the surface register; it is now assigned to Editor document. The
post-edit benchmark uses a bounded end edit; a prefix edit invokes the core's
existing whole-structure fallback and does not isolate bounded semantic lookup.

Round-one fix verification: all 91 focused provider/interaction/surface-census
facts pass; the renewed cross-process UIA3/axe journey also passes, including
new-link discovery after replacing editor text. The first integration head's CI
app lane passed 2,984 tests with only the now-corrected automation-ID register
failure. Its Codoki summary was Safe to merge, 5/5. Revised-head CI, benchmark
measurements and the next independent review remain required.
