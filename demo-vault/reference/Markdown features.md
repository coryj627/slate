# Markdown features

A consolidated reference of the less-common markdown surfaces Slate's parser needs to handle. Each section below targets a specific construct from the extended-markdown surface area in the spec.

## Callouts

Obsidian-style callouts are blockquotes with a typed first line. Three flavors below, plus one nested example.

> [!note]
> This is a note callout. The most common variant — used for inline asides that aren't quite important enough to break out into their own section, but matter enough that the reader should slow down and notice them.

> [!warning]
> This is a warning callout. Use it for content that, if ignored, will cause the reader real trouble. Don't overuse the warning style or it loses its weight.

> [!tip]
> This is a tip callout. Lighter than a warning, more directive than a note — the place to put "if you do X, also remember Y" advice that doesn't fit cleanly in the surrounding prose.

> [!info]
> An outer info callout for nesting.
>
> > [!warning]
> > A nested warning inside the info callout. The renderer should preserve both levels of styling and the assistive-tech announcement should make the nesting audible.

## Footnotes

Pandoc-style footnotes use a reference in the prose[^a] and a definition at the bottom of the document. They're useful for source attributions and parenthetical asides that would interrupt the reading flow if inlined[^b].

A future Slate feature will give footnotes their own VoiceOver rotor so screen reader users can jump between them without losing place in the main text.

## Table

A 4×4 table with a header row, one cell containing inline emphasis, and one cell containing an inline link.

| Feature           | Status        | Owner                              | Notes                                |
|-------------------|---------------|------------------------------------|--------------------------------------|
| Heading rotor     | shipped       | core                               | depth 1–6, *baseline* coverage       |
| Math pipeline     | in progress   | [accessibility](https://example.com/a11y) | LaTeX → MathML → speech + braille |
| Mermaid           | in progress   | core                               | structured descriptions per type     |
| Citations         | current       | research                           | hayagriva render, CSL switching      |

## Definition list

A definition list pairs a term with one or more definitions. The syntax is `term` on one line, `: definition` on the next.

Vault
: The top-level folder Slate opens. Contains all markdown notes and any non-markdown files it preserves without modification.

Frontmatter
: The YAML block at the top of a note, delimited by `---` lines. Provides structured metadata: tags, aliases, dates, custom properties.

Rotor
: A VoiceOver navigation mode that lets the user jump between elements of a given type — headings, links, footnotes, form controls. Slate populates the heading rotor from the parsed outline.

## Blockquotes

Three blockquote variants, each exercising a different construct.

A multi-paragraph blockquote:

> The first paragraph of the quoted material. Long enough to span multiple lines, with no special formatting, just continuous prose to verify that paragraph breaks are preserved inside the blockquote rather than collapsed into a single run.
>
> The second paragraph of the same quote. Some renderers lose the paragraph boundary here and run the two together; the correct behavior is to preserve them as distinct paragraphs within a single blockquote node.

A nested blockquote:

> The outer quote. Sets up the context for the nested quote that follows.
>
> > The inner quote, attributed to a different speaker or source. The renderer should style this differently from the outer level so the nesting is visually clear, and assistive tech should announce the nesting depth.

A blockquote containing a list:

> The author makes three points:
>
> 1. The first point, which is foundational and uncontroversial.
> 2. The second point, which builds on the first and starts to introduce the argument's distinctive claim.
> 3. The third point, which is where the real disagreement with the prior literature lives.
>
> Each point is developed further in the chapters that follow.

[^a]: This is the first footnote. Footnote definitions live at the bottom of the document; the markdown parser links them to their references by label. The definition can contain its own formatting — *emphasis*, `code`, even nested lists — though restraint is usually the right call.

[^b]: The second footnote. Shorter than the first, to demonstrate that footnote length is unconstrained and that a one-sentence note is perfectly valid.
