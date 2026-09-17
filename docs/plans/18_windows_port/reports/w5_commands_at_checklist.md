# W5 COMMANDS — manual AT checklist (#750)

Source: [wave spec](../specs/w5_spec.md) acceptance lines and the
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
| 1 | W5-1 | Palette filter and dispatch | Open the command palette; type a query; move selection; invoke; reopen and Escape. | Selected command and count are spoken; invocation runs the named command and Escape restores focus. | `CommandPaletteTests` | Pending | Pending | Pending |
| 2 | W5-2 | Search and activation | Open Search; type a query; walk note and content results; activate a result; reopen and Escape. | Result snippets and counts are readable; activation lands in the intended note and location. | `SearchOverlayViewModelTests` | Pending | Pending | Pending |
| 3 | W5-3 | Templates and prompt focus | Create from template; select a template; fill required prompts; cancel once, then complete. | Prompt names and validation are spoken; cancellation restores focus; completion opens the created note. | `TemplateFlowTests` | Pending | Pending | Pending |
| 4 | W5-4 | File verbs and cancellation | On a disposable fixture create, rename, duplicate, move and delete a note; cancel the move picker; undo a mutation. | Each action names its target and outcome; cancellation is reachable and undo restores the expected files. | `FileManagementTests` | Pending | Pending | Pending |

Residual: every unexecuted row remains Pending. Record findings and the fix commit on retest; never replace a human pass with the automated twin.
