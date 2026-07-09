---
title: The future of personal knowledge management
tags: [pkm, essay, accessibility]
cite_style: ieee
---

# The future of personal knowledge management

Personal knowledge management is in a strange moment. The category has been around in some form for at least four decades — every major productivity wave has produced its own variant, from outliner programs in the 1980s through wiki-style note-takers in the 2000s through the current generation of bidirectional-link tools — and yet the underlying questions are no closer to being settled than they were the first time someone tried to systematize them. What follows is a working theory of where the field is going, framed against the literature where I can, and acknowledged as opinion where I can't.

## The unfinished business of capture

The capture problem — how do you reliably get a thought out of your head and into a system in the moment it occurs — has been studied since at least the 1980s, when Stallman and the GNU project published the first integrated outline-and-note systems for Emacs [@stallman1985, Chapter 3]. The interesting thing about Stallman's framing, in retrospect, is how much of it has held up. The fundamental claim — that capture latency is the single biggest determinant of whether a note actually gets written — is the same claim that current PKM advocates are still making. The tooling has changed; the underlying ergonomic problem has not.

The literature on capture has been polite about how little progress there has been [@johnson2018]. Most PKM tools optimize for the wrong axis: they invest heavily in retrieval and synthesis, which are downstream of capture, while leaving the capture experience itself essentially identical to what it was in the 1990s — keyboard shortcut, blank text box, no scaffolding. The single significant exception in the consumer space has been voice capture, which a few research groups have studied seriously [@chen2022]. The results are encouraging but qualified; voice solves the latency problem at the cost of introducing a transcription problem, and the transcription problem cascades into a structuring problem that current systems handle badly.

## The retrieval mirage

If capture is underinvested, retrieval is overinvested — and most of the investment has gone into the wrong places. The dominant assumption in the current crop of tools is that retrieval is fundamentally a search problem, and that the right solution is to make the search engine smarter: vector embeddings, semantic similarity, retrieval-augmented generation, and so on. The empirical record on this approach is mixed [@smith2019; @smith2021]. The first of the two Smith papers — the 2019 one, by Smith and the team at Stanford — found that adding semantic search to a PKM tool improved retrieval accuracy on benchmark queries but had essentially no effect on user-reported satisfaction. The second, by a different Smith working at MIT two years later, replicated the accuracy result and pushed harder on the satisfaction question, concluding that the bottleneck is not retrieval accuracy at all but rather the *prompt* — the user's ability to remember the existence of a relevant note in the first place.

That second finding has not, to my knowledge, been seriously absorbed by the field. The implication is significant: if the bottleneck is in the user's recall of what they've written, then no amount of investment in the search engine will move the needle, because the user never types the query. The right intervention is something closer to spaced repetition over one's own notes — a topic that Nielsen has been writing about for years in his work on memory systems for primary literature [@nielsen2020]. Nielsen's framing is that notes are inert by default and that the act of resurfacing them, on a schedule the user doesn't control, is what makes them useful over a timescale of years rather than days.

## Accessibility as a forcing function

I want to make a more specific claim now, which is that accessibility — taken seriously, not as a compliance afterthought — is a forcing function for the design problems the rest of the field has been ducking. A PKM tool that works well for a screen reader user is a tool whose structure is exposed semantically rather than visually, whose navigation is keyboard-first by necessity, and whose content is parseable enough that assistive technology can do something useful with it. Each of those constraints turns out to be a constraint that improves the tool for *all* users, not just screen reader users. The Mack et al. interview study makes the point directly [-@mack2021we]: the interaction patterns that disabled users complain about most are not specialized to disability; they are general usability failures that disabled users happen to notice first because their tools amplify them.

The implication for PKM specifically is that the next generation of tools should be designed with screen reader support as a primary path, not a parallel path. This is the framing Kane has been arguing for in a slightly different context [@kane2020atai]. His argument generalizes — the right way to think about assistive technology is not as a separate channel that has to be maintained alongside the visual channel, but as a constraint on the architecture that produces the visual channel. Tools built that way are smaller, more consistent, and easier to reason about. Tools built the other way are perpetually playing catch-up.

## The unresolved questions

Three questions seem to me genuinely open. First, what is the right unit of granularity for a note? The current consensus, descending from the Zettelkasten tradition, is that notes should be small and atomic. But there's an undercurrent in the literature [@williams2017] that argues atomic notes are a productivity trap — they feel productive to write because each one feels finished, but they fragment knowledge into pieces that don't recombine usefully. The same author has revisited the argument in two later papers [@williams2017] and a 2020 reanalysis [@williams2017], reaching essentially the same conclusion each time: atomicity is a comfort, not a virtue.

Second, what is the role of automated synthesis? Tools that summarize one's own notes — pull out the through-lines, identify the patterns, generate the "obvious" connections you didn't notice — are technically feasible now and getting cheaper. Whether they are *desirable* is a different question. There's a real risk that automated synthesis substitutes for the thinking the user should be doing themselves, in which case the tool is actively counterproductive even though every individual output it produces looks useful.

Third, what does PKM look like at the scale of decades? Almost no tool in the current crop has been usable for more than ten years. The data formats change, the apps get acquired or abandoned, the export paths degrade. Matsumoto has a recent paper on this, arguing — convincingly, to my mind — that the only durable storage layer is plain text on a filesystem you control, and that everything else should be treated as a view onto that layer rather than the layer itself [@matsumoto2023]. The implication for tool design is that the file format should be the product, and the application should be a renderer over the format. Most current tools have this backwards.

There's a fourth question I want to acknowledge but not try to answer here, which is the question of what happens to PKM if and when language models can do most of the synthesis work better than the user can. I don't have a strong view yet — the empirical record is too thin — but I'll point at one more reference for completeness [@notinbib2099] and leave the question open.

## Closing thought

The convergence I see across these strands is that PKM is becoming a problem in *interface design constrained by data durability*, rather than a problem in either pure software engineering or pure information architecture. The early image-captioning work on accessibility tools turned out to be more prescient than it looked at the time [@bigham2010vizwiz, p. 333] — not for its specific technical contributions, but for the broader lesson that the right kind of constraint produces the right kind of system. Constrain a PKM tool to be screen-reader-first, plain-text-first, and renderer-over-format, and you end up with something defensible across a span of decades. Drop any of those constraints and you end up with the current crop, none of which will be usable in 2040.

## References
