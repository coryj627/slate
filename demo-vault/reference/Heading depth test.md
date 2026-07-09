# Heading depth test

This note exists to exercise the heading rotor across every level the markdown spec supports. The body paragraphs are deliberately short — just enough to give VoiceOver something to read between rotor jumps — and every heading level appears at least twice so the rotor has sibling navigation to test.

# Second H1 with siblings

A document with two top-level headings is unusual but legal in CommonMark, and Slate should handle it without flattening the second one. The reading flow here is intentionally trivial so the rotor announcement carries the weight.

## H2 with siblings

A paragraph under the first H2 sibling. Nothing fancy. The next heading is another H2 with no intervening body text, which is the back-to-back case called out in the planning note.

## Second H2 immediately following

This is the tricky one. Some renderers collapse adjacent same-level headings or insert phantom content between them; Slate should keep them distinct in the outline and let the rotor land on each independently.

A short paragraph here just to give the section a body.

## Third H2 with deeper children

Under this H2 we descend through the rest of the heading levels. The point is to verify that a single subtree can contain H3 through H6 without losing structure.

### H3 with siblings

The H3 level is where most real documents do their actual sectioning. Two siblings here, both with body paragraphs, so the rotor has somewhere to go and something to read on arrival.

### Second H3 with siblings

A second H3 under the same H2 parent. This tests that the rotor distinguishes "next sibling" from "next heading at any level."

#### H4 with siblings

Below H3, depth starts to feel academic, but the spec supports it and so should Slate. Two H4 siblings, each with a sentence of body text.

#### Second H4 with siblings

The second H4. If you're navigating by rotor, you should land here cleanly after the first.

##### H5 with siblings

H5 is rare in practice but legal. A paragraph here for reading flow.

##### Second H5 with siblings

The matching sibling. The rotor should treat these as peers.

###### H6 with siblings

The deepest level the spec supports. Browsers render it as smaller than body text, which is awkward, but the semantic level is what matters for assistive tech.

###### Second H6 with siblings

The final sibling pair. After this, the document ends at depth six and the rotor should bubble back up to whatever parent the user navigates from.

## H2 ending without siblings

The last child at its level — there are no further H2s after this in the document, which the planning note flagged as worth exercising. The rotor should still announce this heading correctly and not require a sibling to "anchor" against.

A closing paragraph so the document doesn't end on a heading.
