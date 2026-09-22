# W3 CONTENT — manual AT checklist (#750)

Source: [wave spec](../specs/w3_spec.md) acceptance lines and the
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
| 1 | W3-1 | Reading, activation and remaining ceiling check | Toggle reading with Ctrl+Shift+E; use say-all and heading/link/list/table chords; repeat above the 2,000-block ceiling. | Content is read in authored order; landings and misses are announced; the ceiling announces degradation. The historic NVDA pass remains separate. | `ReadingViewTests` | Pending | Pending | Pending |
| 2 | W3-2 | Math speech and braille | Tab/object-navigate to display math; use Ctrl+Alt+M; Enter rereads; Ctrl+Enter retrieves braille. | Canonical speech is heard; source and braille are inspectable; record actual JAWS behavior independently. | `ReadingMathTests` | Pending | Pending | Pending |
| 3 | W3-2 | Math preferences and degraded content | Change speech style and braille code using menus; visit unsupported and over-budget formulas. | The changed artifact is readable and unsupported content retains its source. | `ReadingMathTests` | Pending | Pending | Pending |
| 4 | W3-3 | Diagram descriptions and failures | Tab to a diagram; use Ctrl+Alt+D; reread with Enter; visit invalid diagram source. | Description and authored source remain available; failure text is read in the document stream. | `ReadingDiagramTests` | Pending | Pending | Pending |
| 5 | W3-4 | Code preamble, interior and copy | Say-all across fenced code; Ctrl+Alt+C; Tab to Copy code and invoke. | Language and line-count preamble precedes readable code; copy speaks its completion. | `ReadingViewTests` | Pending | Pending | Pending |
| 6 | W3-5 | Resolved and unresolved embeds | Say-all across note, section, block, image and unresolved embeds; activate Jump to source. | Header, body, alt text and unresolved explanation read in order; activation opens the correct source. | `ReadingEmbedTests` | Pending | Pending | Pending |

Residual: every unexecuted row remains Pending. Record findings and the fix commit on retest; never replace a human pass with the automated twin.

Agent-operated NVDA evidence for every row here (not a human cell; no cell above changed): [2026-09-22 matrix pass](nvda_agent_matrix_pass_2026-09-22.md), with per-row results, findings F1–F14 and the rows it could not execute.
