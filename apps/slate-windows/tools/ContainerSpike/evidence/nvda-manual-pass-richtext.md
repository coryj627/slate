# NVDA manual pass — `richtext` variant

Tester: repository owner · 2026-07-26 · NVDA 2026.1.1 · Windows 11 build 26200
Build: `spike/w3-1-uia-container` @ `d95a764` · `ContainerSpike.exe --container richtext`

Captured with NVDA Speech Viewer. Verbatim.

## Caret navigation — WORKS

Every `down arrow` advanced one line and announced it. This is the behaviour
`FlowDocumentScrollViewer` could not produce at all: there, arrows re-announced
the first line indefinitely, because that control has no keyboard caret.

```
Reading view  document  Reading container spike
up arrow      → Reading container spike
down arrow    → A paragraph with a   link    resolved note   link, an   link    absent note   link,
                 a   link    #tag  , a citation   link    (Smith, 2020)  , and bold plus code spans.
down arrow    → Second level heading
down arrow    → first bullet
down arrow    → second bullet
down arrow    → nested bullet
down arrow    → ordered one
down arrow    → ordered two
down arrow    → check box  not checked       an open task
down arrow    → check box  checked       a done task
down arrow    → a block quote with a   link    resolved note   link
down arrow    → blank
down arrow    → table  with 2 rows and 2 columns  header a
down arrow    → cell 1
down arrow    → out of table  separator  unavailable
down arrow    → button
down arrow    → button
```

## What this establishes

- **Caret navigation through the whole note works**, line by line, in authored order.
- **Links are announced inline as links** — `link    resolved note   link` — confirming again that a container with no `Hyperlink` control types still surfaces them through the text pattern.
- **Native table semantics appear**: `table  with 2 rows and 2 columns`, `header a`, `cell 1`, and `out of table` on exit. Neither `FlowDocumentScrollViewer` variant produced these.
- **Task checkboxes announce inline and in place**, with state: `check box  not checked       an open task`.
- **The code fence is still silent.** Between `blank` and `table` there is no `fn main`, matching the measured `landmarks missing: fn main` — `BlockUIContainer` content stays outside the text range. W3-4 inherits this.
- **The embed card reads only as `button`**, with no name spoken in this pass.

## What does NOT work

- **`k` (next link) does nothing.** No movement, no announcement.
- **Tab does nothing useful** — `tab`, `shift+tab`, `tab`, `tab` produced no announcements, and focus ultimately left the window (the next thing in the transcript is `PowerShell`). Links inside the document are not in the tab order.

## Working hypothesis for the quick-nav failure

NVDA appears to treat the read-only `RichTextBox` as an **editable text field**, so it stays in **focus mode**. Single-letter quick-nav (`h`/`k`/`l`/`i`) exists only in **browse mode**, which NVDA reserves for document types it has a tree interceptor for. If that is right, quick-nav is unavailable in *any* WPF container, and §W3-1 item 4's "heading/link/list navigation works natively" is not achievable as written.

**Unverified.** Under investigation before any change to the requirement or the container choice.
