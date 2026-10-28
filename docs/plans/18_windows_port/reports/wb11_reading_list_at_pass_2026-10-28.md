# WB-11 reading-list AT pass, 2026-10-28

**Tester:** Claude (agent-driven, plan 44 delivery run; the owner freed the desktop)
**AT:** NVDA 2026.2 (AMD64), stock settings, `--debug-logging`; Narrator, Windows 11, stock settings
**OS:** Windows 11 Pro, 10.0.26300.9711
**Build:** `claude/wb-11-reading-list-semantics` at 7ccdefdf, Release `SlateWindows.exe`; the comparison is `origin/main` 37ba55dc, Release, built in a scratch worktree
**Corpus:** `big-list.md`, which is `# Big list`, `Intro line.`, `- item 1` … `- item 1996`, `## After the list` and `Closing paragraph.` (2,000 blocks, at the reading view's cap); the same shape with 50 items; `ordered.md`, which is `# Ordered`, `Before.`, `3. Third`, `4. Fourth` and `5. Fifth`; `trailing.md`, which is `body`, a blank line and `#`
**Method:** keys sent with SendInput only while Slate held the foreground. App launched by `scripts/windows/launch-for-field-pass.ps1` on a scratch device-state root. NVDA speech read from its debug log; each time runs from the key press to the first log line holding the move's expected text. Narrator speech read with Narrator+Ctrl+X.
**Run date:** 2026-10-28 (America/New_York)
**Evidence reference:** this record, and the WB-11 PR body

## NVDA: #1329 timing on `big-list.md`

| Key | `main` 37ba55dc | WB-11 7ccdefdf | NVDA said (WB-11) |
|---|---|---|---|
| Ctrl+Shift+E (entry) | the reading view did not speak within 60 s; NVDA's watchdog logged 29 core-alive timeouts, 11:15:51 to 11:25:24 | 795 ms | "Reading view, document, …, heading level 1, Big list" |
| Ctrl+End | not reached: every key read the editor's "# Big list" | 266 ms | "Closing paragraph." |
| Up | — | 264 ms | "heading level 2, After the list / Closing paragraph." |
| Up | — | 262 ms | "item 1996 / heading level 2, After the list" |
| Up | — | 263 ms | "item 1995 / •item 1996" |
| Down | — | 266 ms | "item 1996 / heading level 2, After the list" |
| Down | — | 266 ms | "heading level 2, After the list / Closing paragraph." |
| Down | — | 266 ms | "Closing paragraph." |
| Down | — | 267 ms | "Closing paragraph." |

The WB-11 run logged no watchdog line of its own. Its log's only watchdog lines are NVDA's recovery from `main`'s freeze, written before the WB-11 run's first speech. About 260 ms of each time is the driver's 250 ms settle after a key, plus NVDA's own speech pipeline.

**Finding (pre-existing):** each arrow speaks the caret's line and the next. WPF's `ExpandToEnclosingUnit(Line)` reaches over the next line. `main` does the same on a 50-item list (Up: "item 49 / •item 50"), where it is fast enough to measure. Filed as #1687; out of WB-11's scope.

## NVDA: #1422 authored markers on `ordered.md`

| Key | NVDA said (WB-11) |
|---|---|
| Ctrl+Shift+E | "Reading view, document, …, heading level 1, Ordered" |
| Down | "Before. / 3. Third" |
| Down | "3. Third / 4. Fourth" |
| Down | "4. Fourth / 5. Fifth" |
| Down | "5. Fifth" |
| Ctrl+Alt+Shift+L | "Third, list." |

The author's ordinals are read, never renumbered. The list landing names the list by its first item's content, as it does a bullet list.

## Narrator: #1330 Ctrl+A on `trailing.md`

| Key | `main` 37ba55dc | WB-11 7ccdefdf |
|---|---|---|
| Ctrl+Home, then Ctrl+A | last phrase "a"; no selection announced | last phrase "a"; no selection announced |
| Narrator+Shift+Down | nothing new spoken | nothing new spoken |

**Finding (not fixed):** Narrator stays silent on both builds. Over UIA, Ctrl+A raises `TextSelectionChanged` on the surface, and `GetSelection()` reads back the whole note, on `main` and here (`ReadingSelectAll_RaisesTextSelectionChangedAndReturnsTheNote`). So the silence is not the missing event, and not the provider identity WB-11 made stable. WPF's `TextAdaptor` implements `ITextProvider` only, with no TextPattern2, so the StyleId decorator hides nothing. #1330 stays open with these findings.
