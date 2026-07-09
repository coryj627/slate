# Slate demo vault

A demo vault for testing Slate's parsing, rendering, and indexing pipeline end-to-end. Every note in this vault is here for a reason — most exercise a specific feature or regression case from the development plan.

## Layout

```
demo-vault/
├── README.md                  this file
├── library.bib                BibTeX entries for all resolved citation keys
├── slate.json                 vault preferences (cite_style, templates, rendering)
│
├── attachments/               preserve-unknown attachments + the SVG embed target
├── bases/                     Bases v1 `.base` files
├── bases-demo/                small data set for the runnable Bases examples
├── blog/                      conversational markdown surfaces (Hello world, Why Slate)
├── csl/                       Citation Style Language XML — see csl/README.md
├── daily/                     daily-note instances (three days)
├── learning/                  citation notes, math notes, linear algebra cluster
├── people/                    person notes with aliases (Cory Joseph)
├── personal/                  Weekly ToDos, Grocery list, personal Index
├── projects/                  project notes with rich YAML frontmatter (Slate)
├── recipes/                   embed-heavy notes (Apple pie, Whipped cream)
├── reference/                 code/Mermaid/math/markdown samplers + Search bait
├── templates/                 daily-note and meeting-note templates
└── work/                      work Index (the other half of the disambiguation pair)
```

## What each section covers

**Top-level files.** `library.bib` and `slate.json` together drive the citation pipeline; the bib has 11 entries matching the resolved cite keys across the two citation notes, and the JSON sets `cite_style: "ieee"` with three available styles for the switching test.

**`attachments/`.** Contains the SVG used by image embeds in three notes (`pie.svg`), two text-format preserve-unknown attachments (`index.html`, `script.js`), and three binary preserve-unknown placeholders (`photo.png`, `audio.mp3`, `document.pdf`) generated with valid file signatures.

**`bases/` and `bases-demo/`.** `bases/brief_example.base` is a runnable copy of the Obsidian syntax example, scoped to the small `bases-demo/` data set so it opens with real rows instead of evaluating against the whole vault. The other `.base` files mirror parser and round-trip edge cases from the Milestone N corpus.

**`blog/`.** Two long-form posts exercising the common markdown surfaces (`Hello world.md`) and the long tail (`Why Slate.md` — autolinks, hard line breaks, escaped punctuation, nested emphasis, double-backtick code spans, `<details>` HTML passthrough).

**`csl/`.** Holds the three CSL style files you'll need to grab from the official `citation-style-language/styles` repo. See `csl/README.md` for direct links.

**`daily/`.** Three daily-note instances (2026-05-26, -05-27, -05-28) with varying levels of fill-in, mixing completed and in-progress tasks and linking back to active projects.

**`learning/`.** The intellectually dense section. Contains the linear algebra cluster (lecture 2/3/4 plus glossary, with embeds, heading-targeted links, and one deliberately broken link), the calculus notes (LaTeX math source), the raw MathML passthrough note, and the two citation notes — a short three-citation reflection and a 1,361-word essay exercising every citation variant (page locator, Chapter locator, author-suppressed, multi-cite, same-surname pair, ibid-bait, and one unresolved key).

**`people/`.** Person notes with `aliases:` frontmatter, reachable through any alias form from elsewhere in the vault.

**`personal/`.** `Weekly ToDos.md` exercises the full task surface (four standard status chars + custom `[?]`, all four priority emojis, 📅 ⏳ 🔁 metadata, three-deep nested indentation) plus two exclusion regressions (task-shaped lines inside a fence and inside YAML frontmatter, neither of which should appear in the task panel). `Grocery list.md` is a simpler nested-checklist note with cross-links. `Index.md` is one half of a same-basename disambiguation pair.

**`projects/`.** `Slate.md` has frontmatter exercising every YAML construct: string, hyphenated enum-style string, integer, date, boolean, array of strings (one of which contains a slash, `rust/ffi`), array of objects, and array of URLs.

**`recipes/`.** `Apple pie.md` and `Whipped cream.md` are a tightly-coupled pair: the pie has an image embed of `pie.svg`, a whole-note embed of the cream recipe, a block-ref embed of one step (`![[Whipped cream#^method-step-2]]`), and the same block-ref as a prose link in the surrounding text. The cream recipe has the matching `^method-step-2` block anchor.

**`reference/`.** Samplers and edge-case notes. `Code cookbook.md` has 18 V1 languages plus three edge cases (no-tag, unknown-tag `esoteric` for syntect fallback, very-long-lines). `Mermaid sampler.md` covers seven diagram types. `Math sampler.md` covers inline/display/aligned/matrix/integral and one deliberately malformed expression. `Markdown features.md` covers callouts (three types plus one nested), footnotes, a 4×4 table, a definition list, and three blockquote variants. `Heading depth test.md` exercises H1 through H6 with siblings. `Search bait.md` is for FTS5 testing — the pseudoword `xyzzyplover` appears three times at start/middle/end, three non-Latin paragraphs cover Cyrillic/Greek/CJK tokenization, and a 2,590-word body contains the phrase "the kestrel's nest" buried in the middle for snippet-window testing.

**`templates/`.** `daily-note.md` and `meeting-note.md` exercise the template variable system (`{{date}}`, `{{time}}`, `{{vault}}`, `{{cursor}}`, `{{prompt:...}}`).

**`work/`.** `Index.md` is the other half of the disambiguation pair — the link `[[Index]]` from anywhere outside `personal/` and `work/` should surface both candidates rather than silently resolving to one.

## What's intentionally broken

Three things in this vault are *supposed* to fail:

- `[[Linear algebra supplementary]]` in `learning/Linear algebra lecture 3.md` — broken link to a note that doesn't exist.
- `[@notinbib2099]` in `learning/The future of personal knowledge management.md` — unresolved citation key not present in `library.bib`.
- `$\frac{a$` in `reference/Math sampler.md` — deliberately malformed LaTeX to exercise the graceful-failure path.

If any of these starts succeeding without an explicit fix, something has changed in the parser.

## What this vault doesn't test

A few things are out of scope and would need to be added later: synced collaboration, version history, mobile-specific surfaces, and any feature that requires more than one vault at a time. For everything else covered by milestones A through L, there should be at least one note that exercises it.
