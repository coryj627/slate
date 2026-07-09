---
title: AI for accessibility — short reflection
tags: [accessibility, ai, reflection]
---

# AI for accessibility — short reflection

A short note thinking through where AI is actually useful for accessibility work and where it's mostly hype. This isn't a literature review — it's a one-page sketch — but it leans on three sources I keep returning to, all listed below.

The most defensible claim for AI in accessibility is that it lowers the floor on tasks that previously required either expert knowledge or unpaid volunteer labor. Image description is the canonical example. Before the current generation of vision-language models, every image on the web that lacked alt text was effectively a hole in the document for screen reader users; now there's at least a plausible fallback, even if the descriptions are still inconsistent enough that real authoring discipline remains the right answer [@bigham2010vizwiz]. The hole is narrower than it was. That's a real improvement, and it's worth being honest about even from a position of skepticism.

The harder claim — that AI will replace the structural accessibility work, the kind that lives in semantic HTML, ARIA roles, focus management — is much less defensible. Mack and her collaborators are explicit about this in their interview work with disabled users: the most common complaints aren't about content the user couldn't access, they're about *interaction patterns* that made content technically reachable but practically unusable [@mack2021we, p. 42]. That distinction matters. An AI that fills in missing alt text doesn't fix a tab order that traps focus in a modal, and the field has not yet figured out how to evaluate AI assistance for the second class of problem in any rigorous way.

The author whose framing I find most useful here is Shaun Kane, who has argued for years that the relationship between assistive technology and AI should be treated as a co-design problem rather than a substitution problem [@kane2020atai]. The frame is unfashionable in the current moment — most of the discourse is about replacement, on every side of the debate — but I think it's the one that will age best, because it doesn't depend on any particular generation of models being especially good.

## References
