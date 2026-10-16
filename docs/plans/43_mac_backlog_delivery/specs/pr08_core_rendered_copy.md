# PR 8 — Core-rendered copy on mac: code preamble, embed titles, template events

Issues: #1053, #1286, #1122. Size M. Lanes: swift, a11y, windows. Depends
on nothing. Parallel with PRs 9 to 12 (all touch `AppState.swift`;
rebase).

## Goal

Three places where mac still composes accessibility text that core
already renders. Every string is byte-identical today, so the user hears
no change; what changes is ownership (locked architecture §1.1, contract
38: core owns copy), the bound core applies (4 096-character headings and
alt text), and the drift risk `07_portability_review.md:114` claims does
not exist.

## Current state on main

- `CodeBlockView.swift:97-137`: `preambleLabel` composes "Code block,
  {language}, {N} line|lines." from `displayLanguage` ("plain text"
  fallback) and `lineCount` (trailing-newline rule). Core exports
  `code_block_preamble(language: Option<String>, source: String)`
  (`crates/slate-uniffi/src/lib.rs:7312`; Swift
  `codeBlockPreamble(language:source:)`), pinned against the same four
  `CodeBlockViewTests` goldens and byte-checked by the §W-A `code_blocks`
  rows (#1053).
- `EmbedView.swift:73` "Embedded note: \(targetPath)", `:94` "Embedded
  section: \(heading) from \(targetPath)", `:114` "Embedded block from
  \(targetPath)", `:169` `imageEmbedTitle(targetPath:alt:)` ("Embedded
  image: {alt or filename}", audit #198's empty-alt rule). Core owns the
  words: `ResolvedEmbed` (`lib.rs:5813`: `.note(targetPath)`, `.section
  (targetPath, heading)`, `.block(targetPath)`, `.image(targetPath,
  alt)`, `.base(targetPath)`) and `resolved_embed_title(resolved)`
  (`lib.rs:10955`). `BaseEmbedDocument` composes the base card's title
  the same way (AR-23) (#1286).
- `AppState.swift:23929-23937` `announceTemplate(_ message:priority:)`
  wraps `.hostComposed` under a `// W0.5-3 residue:` marker; the created
  site is already converted (`:23688` posts `.templateNoteCreated(name:
  template:)`). The picker-open sentence ("Template picker opened. N
  templates available." and its 0 and 1 arms) is still host-composed;
  find its site by grepping the Sources for "templates available" (the
  helper is referenced from `TemplatePicker.swift:15`). Core renders
  `TemplatePickerOpened { count }` with all three arms
  (`crates/slate-core/src/a11y.rs:1419`, `:2554-2560`). The availability
  and busy reasons (`:23405-23419`) stay host-composed by the a11y.rs
  double-duty carve-out (#1122).

## Windows reference

Windows consumes all three: the code preamble over FFI (W3-4), the embed
titles through `resolved_embed_title` in its header, popover UIA name
and reading cards (#1278), the template events (W5-3). Contract 40
AR-23 (`:81`) records the mac embed divergence; contract 30 records the
template event family's mac column.

## Design

1. **Code preamble.** `preambleLabel` returns `codeBlockPreamble
   (language: block.language, source: block.source)`. Delete `lineCount`
   and the composition; keep `displayLanguage` only if a visible chip
   uses it (check `:138+`); `CodeBlockViewTests` stay as consumers of the
   canonical string (the goldens must not change).
2. **Embed titles.** Each variant builds a `ResolvedEmbed` and labels its
   `EmbedDisclosure` with `resolvedEmbedTitle(resolved:)`; delete
   `imageEmbedTitle` (its alt-trimming rule lives in core; prove it with
   the existing `EmbedViewTests` image facts, which must keep passing
   unchanged). `BaseEmbedDocument`'s base-card title → `.base(targetPath:)`.
   Add a provenance census: no Swift source under `Sources/SlateMac`
   contains the literal "Embedded note", "Embedded section", "Embedded
   block", "Embedded image" or "Embedded base" (allowlist: none), and an
   overlong witness: a heading and an alt of 5 000 characters render
   through core's bound (assert the label length equals core's rendered
   length and the label is what the disclosure exposes).
3. **Template events.** The picker-open site posts
   `.templatePickerOpened(count:)`; remove its residue marker; the
   availability/busy reasons keep theirs. `A11yResidueCensusTests`'
   `pinnedResidueSites` shrinks by the converted sites with the delta
   justified in the commit.

## Changes

- `CodeBlockView.swift:92-137`.
- `EmbedView.swift:66-175`; `Bases/BaseEmbedDocument.swift` (the title).
- `AppState.swift` template picker-open site; `:23929-23937` if the
  message overload becomes unused.
- `Tests/SlateMacTests/EmbedViewTests.swift`, `A11yResidueCensusTests.
  swift`, a new provenance census (in `A11yCorpusCensusTests` or its own
  file).

## Tests

- `CodeBlockViewTests`: the four goldens unchanged; a new fact that the
  label equals `codeBlockPreamble` for a block with no language and a
  trailing newline (the two rules core owns).
- `EmbedViewTests`: existing image facts unchanged;
  `testEveryResolvedEmbedLabelIsCoreRendered` (note, section, block,
  image with and without alt, base); `testAnOverlongHeadingIsBoundedByCore`.
- Provenance census as above.
- Template: the existing exact-wording facts for the three picker-open
  arms keep passing (they are the faithfulness proof); the residue census
  delta.

## Accessibility

No audible change. The overlong witness proves VoiceOver now reads the
bounded text.

## Docs and registers

`docs/plans/40_nvda_matrix_remediation_contracts.md` AR-23 (`:81`)
closed. `docs/plans/30_templates_contracts.md`: the template event
family's mac column marked consumed (TR-5 is PR 9's; leave it).
`docs/plans/07_portability_review.md:114`: the claim is now true; no
edit needed unless it carries a caveat.

## Acceptance

- [ ] No host-composed resolved-embed or code-preamble string remains.
- [ ] The picker-open event is core's; residue census updated.
- [ ] Goldens unchanged; new facts pass; lanes green.

## Brief

Move three families of mac accessibility text onto core's renderers
without changing a single spoken word. Read `CodeBlockView.swift:92-140`,
`EmbedView.swift:60-180`, `Bases/BaseEmbedDocument.swift` (the card
title), `AppState.swift:23929-23945` and the picker-open site, plus
`lib.rs:5813`, `:7312`, `:10955` and `a11y.rs:1419`, `:2554`. Call
`codeBlockPreamble` from `preambleLabel`; build `ResolvedEmbed` values
and call `resolvedEmbedTitle` for every embed label, deleting
`imageEmbedTitle`; post `.templatePickerOpened(count:)` at the picker-open
site and drop its residue marker (availability and busy reasons stay).
Add the provenance census and the overlong witness. Keep every existing
golden green. Close AR-23 in contract 40 and mark the template events
consumed on mac in contract 30. Done when the censuses and facts pass and
the lanes are green.
