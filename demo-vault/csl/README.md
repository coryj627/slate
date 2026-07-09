# CSL styles

This directory holds the Citation Style Language XML files that drive the bibliography render and the cite-style switching tested across the two citation notes (`learning/AI for accessibility — short reflection.md` and `learning/The future of personal knowledge management.md`).

The three styles referenced by `slate.json` are:

- `ieee.csl`
- `chicago-author-date.csl`
- `apa.csl`

These are not generated as part of the demo vault — they should be downloaded from the official Citation Style Language repository at:

  https://github.com/citation-style-language/styles

Direct file paths in that repo (as of the last check):

- `ieee.csl` — https://github.com/citation-style-language/styles/blob/master/ieee.csl
- `chicago-author-date.csl` — https://github.com/citation-style-language/styles/blob/master/chicago-author-date.csl
- `apa.csl` — https://github.com/citation-style-language/styles/blob/master/apa.csl

Drop the three `.csl` files into this directory. Once they're in place, the citation pipeline in both notes should render correctly, and switching `cite_style` in `slate.json` between the three values should change both the in-text format (numeric for IEEE, author-date for the other two) and the bibliography format.

Until the CSL files are present, the citation render will fail or fall back, and that's the expected behavior — the resolution of the cite keys against `library.bib` is independent of the CSL files, but the formatted output isn't.
