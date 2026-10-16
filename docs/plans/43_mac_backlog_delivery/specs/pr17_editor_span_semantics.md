# PR 17 — Editor: span semantics for VoiceOver (heading level, links) (G29)

Issue: #1224 (twin of W7-1, #747). Size M to L (a short spike first).
Lanes: swift, a11y, windows, plus a VoiceOver field pass. Depends on
nothing. Parallel with PR 16.

## Goal

While reading Markdown source in the editor, VoiceOver hears "heading
level 2" on a heading line and "link" on wikilinks, links and embeds,
and the Links rotor lists them, the way NVDA and JAWS do on Windows
through the editor peer's text-range attributes. Issue #63's rule (no
override of `NSTextView`'s native reading) stands; this adds attributes,
not a replacement.

## Current state on main

- `NoteEditorView.swift:980` `applyHighlight(_ prepared:)` consumes the
  windowed canonical spans and applies colour (`layoutManager.
  addTemporaryAttribute(.foregroundColor…)`, `:1006-1016`) and the embed
  underline (`:1023-1035`) as TEMPORARY layout-manager attributes, which
  the text storage never holds and the AX tree never sees (the comment at
  `:976-979` says so). Heading navigation is delegated to the Outline
  sidebar.
- Core's `EditorSpanKind` (`crates/slate-uniffi/src/lib.rs:6828-6844`):
  `heading(level)`, emphasis, strong, strikethrough, inlineCode,
  codeFence, link, image, blockQuote, wikilink, embed, tag, citation,
  comment, frontmatter, code(token).

## Windows reference

`apps/slate-windows/src/SlateWindows/EditorSemanticText.cs:183-185`
(StyleId 40034, StyleName 40033, Link 40035) and `:259-279` the map:
Heading → StyleId 70000+level and StyleName "Heading N"; Wikilink /
Link / Embed / Image → StyleName (and the Link attribute per #747);
BlockQuote → StyleId 70014 "Quote"; Emphasis → italic; Strong → weight
700; Strikethrough; Tag, Citation, Comment, Frontmatter, Code → their
StyleNames. `gap_analysis.md:103` (G29): Windows-first, mac converges.

## Design

**Spike (two to three hours, result in the PR).** Which mechanism makes
VoiceOver speak links and headings inside an `NSTextView` without
replacing native reading. Candidates:
- a narrow `NSTextView` subclass override of the accessibility
  attributed-string accessor for a range, adding AppKit's AX text
  attributes for links and a custom text attribute carrying "Heading N"
  on heading ranges (an AX override, but exactly the "tester reports a
  missing affordance" exception #63 allows, since G29 is the owner's
  call);
- real `.link` attributes in the text storage (expected to be rejected:
  they change click behaviour and styling and collide with the editor's
  own link handling);
- `NSAccessibilityCustomTextAttribute`-style annotations only.
Record what VoiceOver speaks and whether the Links rotor lists the
ranges for each.

**Implementation.** A pure mapping `EditorSpanKind → [AX attribute]`
mirroring the Windows table (heading level, link for wikilink / link /
embed / image, quote, emphasis, strong, strikethrough, code, tag,
citation, comment, frontmatter), applied through the chosen mechanism
over the same windowed spans the colorizer paints (so cost tracks the
window, not the document). Where AppKit honours no attribute for a kind
(heading level is the likely case), record the platform limit in G29 and
ship the custom-text annotation so the level is at least spoken.

## Changes

- `NoteEditorView.swift` (the mapping beside `applyHighlight`, the AX
  accessor override on the text view subclass), a new
  `EditorAccessibilityAttributes.swift` for the pure map.

## Tests

- `EditorSpanMappingTests` (or new `EditorAccessibilityAttributesTests`):
  one fact per span kind over the mapping; a fact over the AX attributed
  string for a buffer with a heading, a wikilink, a link, an embed and a
  quote (the attribute runs cover exactly the span ranges, windowed).
- `NoteEditorRangedHighlightTests` keep passing (colour unchanged);
  `HighlightBenchmarkTests` within budget.
- `a11y-check` 100.

## Accessibility (the proof)

VoiceOver field pass: read a heading line (level spoken), a wikilink
("link" spoken), open the Links rotor (VO-U) and confirm the three link
kinds are listed; record the phrases in the PR.

## Docs and registers

`docs/plans/18_windows_port/specs/gap_analysis.md:103` G29 closed (or
the platform limit recorded); `w_c_matrix.md` editor row updated;
`docs/runbooks/voiceover-feature-test.md` §F gains the span steps.

## Acceptance

- [ ] Spike result written; mechanism chosen with evidence.
- [ ] Links are spoken and listed; heading level is spoken (or the limit
      recorded).
- [ ] Facts pass; benchmarks within budget; registers updated; lanes
      green.

## Risks

An AX accessor override that is slow on a large note would regress
VoiceOver reading; apply attributes windowed and measure with
`HighlightBenchmarkTests`' harness.

## Brief

Expose the editor's canonical spans to VoiceOver. Read
`NoteEditorView.swift:960-1060`, `lib.rs:6828-6844`, the Windows
`EditorSemanticText.cs:183-185` and `:255-280`, and `gap_analysis.md:103`.
Spike the mechanisms (AX attributed-string accessor override versus
storage attributes versus custom-text annotations) and record what
VoiceOver speaks and lists for each. Implement a pure span-kind-to-AX-
attribute map mirroring the Windows table and apply it over the
windowed spans without replacing native reading. Write the per-kind
facts first; keep colour and benchmarks unchanged. Do the VoiceOver
pass (heading level, link, Links rotor) and record the phrases. Close or
annotate G29. Done when the phrases are in the PR and the lanes are
green.
