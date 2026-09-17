# W7-1 editor Text-pattern contracts (#747)

Scope: [W7 executable spec](18_windows_port/specs/w7_spec.md) §2,
the editing surface, one PR. G29 is Windows-first editor semantic reading;
the mac counterpart is [#1224](https://github.com/coryj627/slate/issues/1224).
This document precedes the implementation review as its own commit.
Code citations below identify implementation seats; seats marked planned
do not exist at the contract-only commit.

## Contracts

**E-1 — One canonical source.** `EditorSemanticTextProvider` and
`EditorSemanticTextRange` (planned `EditorSemanticText.cs`) read the editor's
`AvalonDocumentBufferSession.InspectInRange`. That method calls the same
`DocumentBuffer.HighlightInRange` as the colorizer's query and does not
replace `LatestHighlightWindow`. There is no second FFI entry point, host
Markdown classifier, speech builder or retained semantic window. Offscreen
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
Link identity is the canonical link span, not a newly allocated wrapper's
object identity. Queries do not invent font families: the editor palette
changes foreground brushes, not font families.

| Canonical kind | UIA attributes |
|---|---|
| Heading | StyleId = 70000 + level (1–6); StyleName = Heading N |
| Wikilink | Link = own canonical span range; StyleName = Wikilink |
| Link | Link = own canonical span range; StyleName = Link |
| Embed | Link = own canonical span range; StyleName = Embed |
| Image | Link = own canonical span range; StyleName = Image |
| Tag | Link = own canonical span range; StyleName = Tag |
| Citation | Link = own canonical span range; StyleName = Citation |
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

**E-5 — Attribute search.** FindAttribute supports StyleId and Link in
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

**E-9 — Single owner.** The editor peer still has no children; range
GetChildren returns no embedded objects; GetEnclosingElement returns the
public editor's provider. TextArea.EventsSource remains the editor peer.
The #1088 forward and backward UIA3 subtree walks must terminate and agree.

**E-10 — Evidence is executable.** Planned tests:
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
StyleId at 100 KiB, 1 MiB and 8 MiB, plus document-range FindAttribute(Link)
at 8 MiB. First measurements, runner details and budgets are appended here
and to BENCHMARKS.md before acceptance; no unmeasured result is a pass.
The benchmark validates its complete case inventory and a line-query
flatness bound. A full-document query may pay for all its spans; a line
query must not copy or scan the whole document to discover its offsets.

**E-12 — Human evidence is separate.** Update the Editor document matrix
row and W2 checklist with the executable twins and explicit NVDA/JAWS
checks for line/word/character reading, say-all, semantic boundaries,
selection, braille and IME. Named runs include reader/version, OS, commit,
corpus, tester, date and transcript. The owner agreed to both AT checklists;
unexecuted results stay Pending. JAWS StyleId/Link consumption is an open
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

Implementation and measurements pending at the contract-only commit.
Append each invariant-targeted review's findings and resolutions here,
following [the red-team protocol](24_red_team_protocol.md). Three successive
blocking rounds in one subsystem trigger a design pass; a blocker created
by the previous fix counts twice.
