# W1 SHELL — manual AT checklist (#750)

Source: [wave spec](../specs/w1_spec.md) acceptance lines and the
[conformance matrix](../w_c_matrix.md) keyboard routes. Run on a disposable
fixture vault. Use [_at_pass_template.md](_at_pass_template.md) for a named,
dated record and transcript. NVDA and JAWS are independent passes with stock
settings; Narrator is W8-6 smoke scope. Automated twins do not prove speech.

**Tester:** Cory Joseph (NVDA run; JAWS and Narrator runs still to come) · **AT:** NVDA 2026.2, stock settings · **OS:** Windows 11 Pro 25H2, build 10.0.26200.9457
**Build:** main at 6b5cf96, Release executable · **Corpus:** demo-vault (90 files)
**Method:** keyboard only; NVDA speech log at Debug as the transcript
**Run date:** 2026-09-20 (America/New_York) · **Evidence reference:** [w7_5_nvda_field_pass_2026-09-20.md](w7_5_nvda_field_pass_2026-09-20.md)

| # | Spec item | Check | How (the UIA route) | Observable outcome | Automated twin | Narrator | NVDA | JAWS |
|---|---|---|---|---|---|---|---|---|
| 1 | W1-1 | Welcome and menu names | Start with no vault; Tab to Open Vault; use Alt to walk the File and Workspace menus. | Window, menu and button names are spoken; focus remains visible and activation opens the folder picker. | `FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean` | Pending | Finding [record](w7_5_nvda_field_pass_2026-09-20.md) | Pending |
| 2 | W1-1 | Scan progress and completion | Open a fixture vault with enough files to observe scanning; listen until completion. Then (W7-7, #1252) create a note outside Slate and use File ▸ Files Sidebar ▸ Refresh; alt-tab away for a few seconds, change a note outside Slate, and come back. | Progress is announced without a focus jump and the final counts are heard — "Scan complete. N files, M new or changed." After Refresh exactly one "Files refreshed. 1 new or changed, 0 removed." and the new note is in the tree; the return to the window speaks one "Files refreshed. …" line for the change and nothing when nothing changed. | `W1ShellAccessibilityContractTests`; `RefreshSpeaksExactlyOneCompletion`; `ForegroundRescanIsThrottledAndSilentWhenNothingChanged`; `ExternalFiles_AppearAfterRefresh` | Pending | Pending | Pending |
| 3 | W1-2 | Tree and tag navigation | Tab to Files; use arrows and Right/Left, open a row with Enter; toggle Tags and repeat. | Levels, expanded state and selection are spoken; activation opens the named file. | `FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean` | Pending | Pending | Pending |
| 4 | W1-2 | Filter and sidebar actions | Focus the file filter with Alt+Ctrl+F; type a query; walk results; tab through sort and organization controls. | Counts and result names agree; control names, values and expanded/toggled states are readable. | `FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean` | Pending | Pending | Pending |
| 5 | W1-3 | Tabs, splits and focus routing | Open two notes; split with Ctrl+Backslash; move between panes with Ctrl+Alt+arrows; close and reopen a tab. | Tab selection and pane focus are announced; the active editor is reachable in each split. | `W1WorkspaceTests` | Pending | Pending | Pending |
| 6 | W1-3 | Resize and restore | Tab to a split handle; resize with arrows; close and reopen the app on the fixture vault. | Handle name and size are inspectable; restored tabs and panes keep their expected focus route. | `W1WorkspaceTests` | Pending | Pending | Pending |
| 7 | W1-4 | Quick Open | Ctrl+O; type a query; Down through results; Enter opens; reopen and Escape. | Result count and selected name are spoken; Escape restores the invoking editor. | `QuickSwitcherRankCoordinatorTests` | Pending | Pending | Pending |
| 8 | W7-6 | Region cycling | From the Files tree press F6 repeatedly through a full loop, then Shift+F6 back; hide the right pane (Ctrl+Alt+I) and repeat; repeat once on a vault with no tab open. | Each landing is spoken (Files, tab bar with the tab, editor, leaf panel, "Right pane panels", "Status bar" with its text, "Menu bar", "Editor pane. Empty"); hidden right-pane stops are skipped; the ring wraps both ways. | `ShellRegions_F6CyclesForwardAndShiftF6Back` | Pending | Pending | Pending |

Production agent-operated evidence for row 8 and the W7-5 launch-focus retest: [2026-09-20 NVDA run](w7_6_nvda_production_run_2026-09-20.md) (not a human cell; the typed region announcements were not received in that session and stay Pending for the owner's pass).

Residual: every unexecuted row remains Pending. Record findings and the fix commit on retest; never replace a human pass with the automated twin.

Agent-operated NVDA evidence for every row here (not a human cell; no cell above changed): [2026-09-22 matrix pass](nvda_agent_matrix_pass_2026-09-22.md), with per-row results, findings F1–F14 and the rows it could not execute.
