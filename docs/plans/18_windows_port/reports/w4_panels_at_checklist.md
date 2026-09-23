# W4 PANELS — manual AT checklist (#750)

Source: [wave spec](../specs/w4_spec.md) acceptance lines and the
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
| 1 | W4-1 | Grid headers, rows and actions | Enter a reading table; arrow through cells; sort with Ctrl+Alt+S; open row actions with Shift+F10; Escape. | Row and column identity, values and sort changes are spoken; Escape returns to the original surface. | `AccessibleDataGridTests` | Pending | Pending | Pending |
| 2 | W4-2 | Outline, links and embeds | Toggle the right pane with Ctrl+Alt+I; select Outline, Backlinks, Outgoing links and Embeds; invoke a row. | Panel names and rows are readable; invocation navigates to the intended note or heading. | `RightPanePanelsTests` | Pending | Pending | Pending |
| 3 | W4-3 | Tasks and review | Open Tasks; toggle a task; open review with Ctrl+R; walk each due-status section. | Task status and due summaries follow core copy; toggling updates the source and announcement. | `TasksPanelTests` | Pending | Pending | Pending |
| 4 | W4-4 | Properties and sheets | Focus a property row; edit its value; add a property and use the rename sheet; cancel a draft. | Type, value and validation are spoken; sheet focus returns and cancellation retains committed values. | `NotePropertiesTests` | Pending | Pending | Pending |
| 5 | W4-5 | Citations and bibliography | Open citation details, summary and Files Citing; walk both bibliography segments and return. | Grids and sheets expose labels, counts and actions; each close restores the invoker. | `CitationsPanelTests` | Pending | Pending | Pending |
| 6 | W4-6 | Bases, builder and dashboards | Open a base; switch views; filter and sort; open the builder; walk dashboard sections and the dock. | Results, grouped headers and disabled-action reasons are readable with native grid navigation. | `BasesDocumentTests` | Pending | Pending | Pending |
| 7 | W4-7 | History and restoration | On a disposable vault open History; compare versions; inspect structured diff; Restore As; inspect Deleted. | Version labels, diff descriptions and restoration outcomes are spoken; cancellation preserves the current note. | `HistoryPanelTests` | Pending | Pending | Pending |
| 8 | W4-8 | Sync diagnostics | Select Sync Diagnostics; walk report sections; activate Refresh and inspect a degraded fixture. | Section labels, warning reasons and refreshed status are available without a focus jump. | `SyncDiagnosticsPanelTests` | Pending | Pending | Pending |
| 9 | W4-1 §8.7 large-fixture acceptance | Ten-thousand-row virtualization | Launch `GridConformanceHost.exe 10000` (the fixture used by VirtualizationTrapProbeSurvivesTenThousandRows); navigate to initially unrealized rows near the middle and end, then return to the first row; repeat independently with NVDA and JAWS. | Row identity and headers remain correct after realization; focus and selection survive scrolling, and no UIA traversal crash or silent dead end occurs. | `VirtualizationTrapProbeSurvivesTenThousandRows` | Pending | Pending | Pending |

Residual: every unexecuted row remains Pending. Record findings and the fix commit on retest; never replace a human pass with the automated twin.

Agent-operated NVDA evidence for every row here (not a human cell; no cell above changed): [2026-09-22 matrix pass](nvda_agent_matrix_pass_2026-09-22.md), with per-row results, findings F1–F14 and the rows it could not execute.
