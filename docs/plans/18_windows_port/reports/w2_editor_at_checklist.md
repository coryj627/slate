# W2 EDITOR — manual AT checklist (#750)

Source: [wave spec](../specs/w2_spec.md) acceptance lines and the
[conformance matrix](../w_c_matrix.md) keyboard routes. Run on a disposable
fixture vault. Use [_at_pass_template.md](_at_pass_template.md) for a named,
dated record and transcript. NVDA and JAWS are independent passes with stock
settings; Narrator is W8-6 smoke scope. Automated twins do not prove speech.

**Tester:** Pending · **AT:** Pending (exact version per run) · **OS:** Pending (edition and build)
**Build:** Pending (branch and verified commit) · **Corpus:** Pending (fixture vault and notes)
**Method:** Pending (Speech Viewer / JAWS history, braille viewer where required)
**Run date:** Pending · **Evidence reference:** Pending

| # | Spec item | Check | How (the UIA route) | Observable outcome | Automated twin | Narrator | NVDA | JAWS |
|---|---|---|---|---|---|---|---|---|
| 1 | W2-1 | Document, selection and undo | Open a fixture note; move by character, word and line; select text; type, undo and redo; Ctrl+S. | Document name, text, selection and caret remain synchronized; save feedback is heard. | `AvalonDocumentBufferCensus` | Pending | Pending | Pending |
| 2 | W2-1 | IME composition | Using an installed IME, compose and commit text; cancel another composition; undo the committed edit. | Composition does not duplicate committed text or lose caret tracking. | `AvalonDocumentBufferCensus` | Pending | Pending | Pending |
| 3 | W2-2 | Canonical span display | Open a note with headings, links, code and math; inspect while editing and scrolling. | Highlighting follows the buffer without a stale span or text mismatch; spoken semantic additions are tested after #747. | `CanonicalHighlightCensus` | Pending | Pending | Pending |
| 4 | W2-3 | Semantic activation and preview | At a link, tag, citation and embed use Ctrl+Enter; use Ctrl+E for preview and Escape to return. | The correct target or canonical unresolved result is announced; preview content is readable and focus returns. | `W2EditorInteractionTests` | Pending | Pending | Pending |
| 5 | W2-4 / W2-5 | Feature-conditional inventory | Inspect the parity matrix entries for autocomplete and LaTeX authoring aids before claiming these routes. | Dropped or reserved features stay explicitly designated; no nonexistent popup is called verified. | `CommandDriftTests` | Pending | Pending | Pending |

Residual: every unexecuted row remains Pending. Record findings and the fix commit on retest; never replace a human pass with the automated twin.
