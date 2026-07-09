---
title: Weekly ToDos
notes:
  - quick reminders
  - "- [ ] yaml-task that must not appear in the task panel"
---

# Weekly ToDos

The everyday task surface. Grouped under Work / Personal / Errands, with a mix of statuses, priorities, and recurrence so the task panel has something representative to render.

## Work

- [ ] Submit grant 📅 2026-06-01 🔼 🔁 every year
- [/] Draft proposal ⏳ 2026-05-30 ⏫
- [ ] Review pull requests in the citations branch
    - [ ] Walk through the Pandoc parser changes
    - [ ] Cross-check `list_unresolved_citations` output against expectations
        - [ ] Confirm `[@notinbib2099]` surfaces correctly
        - [x] Confirm resolved citations don't surface
- [x] File timesheet for the previous week
- [-] Schedule design review for the L-milestone branch
- [ ] Update documentation for the heading rotor 🔽
- [ ] Reply to the accessibility audit thread ⏬

## Personal

- [ ] Refill prescription before Friday
- [x] Book dentist appointment for next month
- [/] Read through the Mind Lab Pro interaction notes again before deciding
- [?] Look into the Jabra Evolve2 75 for the work laptop
- [ ] Back up the Immich library to the secondary pool

## Errands

- [ ] Pick up dry cleaning
- [x] Drop off the package at the post office
- [ ] Grocery run — see `Grocery list.md`
    - [ ] Confirm the recipe link before shopping
    - [ ] Bring the insulated bag this time

## Exclusion regressions

The two blocks below contain task-shaped lines that must **not** appear in the task panel. The frontmatter block at the top of this file contains one (`- [ ] yaml-task`), and the fenced block below contains another.

```text
- [ ] inside fence
- [x] also inside fence
```

If either of those lines shows up in the task panel, the corresponding exclusion rule has regressed.
