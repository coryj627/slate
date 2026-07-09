---
title: Slate
status: in-progress
priority: 1
start_date: 2026-04-12
active: true
tags: [pkm, accessibility, rust/ffi]
stakeholders:
  - name: Cory Joseph
    role: founder
links:
  - https://github.com/coryj627/slate
---

# Slate

Slate is an accessibility-first personal knowledge management application — a notes app whose entire pipeline is designed around the assumption that the screen reader path is the primary path, not an accommodation bolted on after the visual design is finished. Most notes apps treat assistive technology as a compliance checkbox; Slate inverts that, and the rest of the architecture follows.

The current branch is the L-milestone work on citations: a Pandoc-style parser front end, hayagriva for the bibliographic render, and CSL style switching that affects both the visual text and the speech text emitted to assistive tech. The earlier shipped milestones cover vault open, headings, links and backlinks, frontmatter, full-text search, edit, tasks, templates, property editing, and embeds; the K-milestone visual-rendering work for math, Mermaid, and code blocks is in progress in parallel.

Project lead is [[Cory Joseph]] — the founder named in frontmatter, also reachable through the `CJ` alias on his person note. See the disambiguation pair at [[Index]] for the same-name resolution test.
