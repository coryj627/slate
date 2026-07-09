# Hello world

Welcome to the first post on this blog. I wanted to start with something low-stakes — a casual note that doubles as a tour of the markdown surfaces I expect to use most often. If you're reading this in Slate, the heading rotor should already show three levels of nesting, and the reading flow should feel like ordinary prose underneath.

## Why a "hello world" still matters

It's tempting to skip the introductory post and jump straight to the substantive writing. I've done that before and regretted it. A hello-world entry sets the **tone** for everything that follows, gives you a *safe* place to shake out formatting bugs, and — honestly — makes the archive feel less intimidating when there's already something sitting in it. There's a reason every programming tutorial starts the same way.

I used to think the convention was ~~quaint and outdated~~ a relic of a less serious era of computing. I've come around. The point isn't the literal phrase; it's the act of proving the pipeline works end-to-end before you trust it with anything you actually care about.

### What this blog is for

Short answer: a place to think out loud about accessibility, tooling, and the small frustrations that accumulate when you build software for a living. I'm not aiming for any particular publication cadence. If a post takes three weeks to feel right, it takes three weeks.

### What it isn't

It isn't a newsletter, it isn't optimized for search, and it isn't going to have a comments section. If you want to respond, email is fine.

## A quick technical detour

Since this is also a test post, let me drop in a code sample. Here's the smallest useful shell snippet I reach for when I'm setting up a new machine:

```bash
#!/usr/bin/env bash
set -euo pipefail

mkdir -p ~/work ~/scratch
cd ~/work
git config --global init.defaultBranch main
echo "machine ready"
```

Nothing exciting, but it's the kind of thing I want to be able to paste from a note into a terminal without thinking. Inline, the command I run most often is `git status --short` — short enough that I've stopped reading the output and just feel whether it's "clean" or "dirty" by the shape of it.

> The best tool is the one you've internalized to the point of invisibility. Everything else is friction.

That quote isn't from anyone in particular. I think I made it up in the shower. It still feels true.

---

## Things I want to write about, in no particular order

A short ordered list to set expectations:

1. How screen reader users actually navigate documentation, and where most docs sites fall short.
2. The difference between *accessible* and *usable*, which is bigger than most teams acknowledge.
3. Home lab projects that aren't really about the lab.

And an unordered list of smaller topics I'll probably get to eventually:

- Keyboard shortcuts I've redefined and why
- The case for plain text as a long-term storage format
- Why I keep coming back to markdown after trying every fancier alternative

## A picture, because the spec says so

Here's a placeholder image referenced from the attachments folder. In a real post it would be a screenshot or a diagram; for now it's just proof that the embed pipeline works.

![pie illustration](attachments/pie.svg)

That's it for the first post. If you're seeing this rendered cleanly — headings nested correctly, code block syntax-highlighted, the image loaded — then the pipeline works. More to come. You can find the markdown spec I mostly follow at [CommonMark](https://commonmark.org/), which is worth a read if you've never sat down with it.
