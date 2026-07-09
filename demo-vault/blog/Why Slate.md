# Why Slate

I've been asked, more than once, why I'm building yet another note-taking app when the market already has a dozen good ones. It's a fair question and I want to answer it carefully — not with marketing copy, but with the actual reasoning that led me here. The short version is that none of the tools I tried treated accessibility as a first-class concern, and after enough years of working around that, I decided to stop working around it.

The longer version takes some unpacking.

## What "accessible" actually means in a notes app

When most apps claim accessibility, they mean their UI passes an automated audit. Buttons have labels, contrast ratios clear the thresholds, focus order is mostly correct. That's table stakes, and a lot of apps don't even reach that bar, so I don't want to dismiss it. But it's not the same as being *usable* with a screen reader for hours a day.

Real usability shows up in the small things:

- Whether a heading rotor jumps to the right place
- Whether math is announced as math, not as a soup of operators
- Whether code blocks expose their language to assistive tech instead of being read character by character
- Whether a diagram has *anything* behind it besides "image"

Each of those is a separate engineering investment. You don't get them by accident, and you don't get them from a CSS audit. You get them by building the pipeline with the assumption that the screen reader path is the primary path — and then making the visual path consistent with it.

That's the design constraint Slate starts from. Everything else follows.

## The long tail of markdown

One of the unsung qualities of markdown — the reason it's outlasted a generation of supposed replacements — is that the surface is small enough to memorize and large enough to do real work. Most of what you need fits in a paragraph of documentation. But the long tail is where things get interesting: hard line breaks (two trailing spaces, or a backslash at end of line), soft line breaks that some renderers preserve and others collapse,
escaped punctuation like \*literal asterisks\* and \[literal brackets\], nested emphasis that you can write as ***both bold and italic*** or as **_bold containing italic_** depending on your taste, and code spans that need to contain backticks themselves — which is the whole reason ``double-backtick fences like `this` `` exist.

Autolinked URLs are another one. You can write <https://example.com/path?q=1> and have it linkified without a markdown link wrapper, which matters when you're pasting URLs into notes at speed. The convention is older than markdown itself — it goes back to plain-text email — and it still works.

Then there's the HTML-passthrough escape hatch, which I have complicated feelings about but use anyway. The canonical example is a collapsible section:

<details>
<summary>Click to expand: what I actually use HTML passthrough for</summary>

Almost exclusively for `<details>` and `<summary>`, because no markdown extension has standardized a clean syntax for them and the HTML is short enough to type without resentment. Occasionally for a `<kbd>` tag when I want to render a keyboard shortcut visually. Almost never for anything else, because once you start mixing HTML into your markdown, the round-trip through any parser gets fragile fast.

</summary>
</details>

The trick with passthrough is to use it for things the markdown spec genuinely doesn't cover, and to resist the temptation to start styling. The moment you reach for a `<div class="...">`, you've left markdown behind and you're writing HTML in a `.md` file, which defeats the purpose.

## The right level of opinionatedness

I want Slate to have strong opinions about the things screen reader users care about, and weak opinions about everything else.

Strong opinions:

1. Math source is LaTeX. The render target is MathML with MathCAT-generated speech. That pipeline is fixed.
2. Diagram source is Mermaid. The render target is SVG with a structured description alongside.
3. Code blocks expose semantic spans, not just syntax highlighting.

Weak opinions: file layout, naming conventions, what you put in frontmatter, whether you write in long paragraphs or terse bullets. None of that is the app's business.

The reason I separate these is that the strong opinions are *cost centers* — every one of them is an engineering investment that most teams won't make. If I'm doing the work, I want the result to be uniform, predictable, and consistent across notes. The weak opinions are *user choices* — I have no business overriding them just because I happen to write a certain way.

## One more thing about formatting

Here's a piece of trivia that took me embarrassingly long to learn: in CommonMark, a backslash at the end of a line is a hard line break. \
That's the one I'm using right now. \
The two-trailing-spaces variant works too, but it's invisible in source, which means you can't tell at a glance whether a paragraph is one logical unit or several. The backslash form is uglier and better.

I'll write more about Slate's internals in future posts. For now this is the manifesto, such as it is. The image below is, again, a placeholder — but it has explicit alt text this time, because the absence of alt text is one of the things this whole project exists to push back against.

![A simple line drawing of a pie with a slice removed, used here as a placeholder during early development of the demo vault.](attachments/pie.svg)

If you've read this far, thank you. The next post will be about something concrete.
