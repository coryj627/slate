# Milestone N — manual AT smoke checklist

Run at close-out on a real Mac with the shipped build, VoiceOver, Full Keyboard Access, Voice Control, and a representative vault. Automated gates cover parser/engine correctness, a11y static checks, APCA, and CLI E2E; this list covers the user experience that only a human with real assistive tech can judge. Record run date, macOS version, Slate commit, vault fixture, tester, and result per item.

| # | Check | How | Expected | Result |
|---|---|---|---|---|
| 1 | Grid entry | Open a `.base` table view from the file tree. Move VO into the grid. | Base title, view name, row count, and column count are discoverable without relying on a transient announcement. | ☐ |
| 2 | Column header announcement | Move to each header and toggle sort. | Header reads as `Column: <name>, sortable, current sort: <direction>`; sort change announces `Sorted by <column>, <direction>`. | ☐ |
| 3 | Cell movement | Move cell-by-cell with arrows, Home/End, Page Up/Down. | Each cell reads `<column header>: <cell value>` and row/column position remains understandable. | ☐ |
| 4 | Summary row | Open a view with summaries and move past the data rows. | Summary row is separately addressable from data rows and each summary names its column and value. | ☐ |
| 5 | Grouped view | Open a grouped table. | Group headings read as headings with group label and row count; rows inside the group remain reachable. | ☐ |
| 6 | List view | Switch to list view with **Bases: View as List**. | Row navigation is linear, row actions remain available, and no table-only command traps focus. | ☐ |
| 7 | Quick filter | Focus **Bases: Quick filter**, type a term, clear it, then inspect the `.base` file. | Count updates are spoken; filter is temporary; file bytes do not change. | ☐ |
| 8 | Builder condition rows | Open **Bases: New Query**, add conditions and a group using the keyboard. | Each filter is a structured node, not a free-text blob; VO hears property, operator, value, and group combinator. | ☐ |
| 9 | Builder validation | Enter an invalid formula, then fix it. | Error text is reachable and names the offending expression; valid state is announced without stealing focus. | ☐ |
| 10 | Property editing | Edit an editable grid property cell and re-run the query. | Disk bytes update only the target frontmatter property; grid refreshes; immutable/file/formula columns do not present editable controls. | ☐ |
| 11 | Saved query pin | Pin a saved query in the Queries sidebar. | VO reads `<query name>, saved query`; pin order persists after app relaunch. | ☐ |
| 12 | Dashboard hierarchy | Open a dashboard tab. | Dashboard title is an H1-equivalent heading, sections are H2-equivalent headings, and each section grid is independently navigable. | ☐ |
| 13 | Missing dashboard section | Delete a saved query referenced by a dashboard. | Section remains visible as `Missing saved query` with actions to remove or replace; no silent drop. | ☐ |
| 14 | Base dock `this` | Dock a `file.hasLink(this.file)` saved query, then switch active notes. | Dock re-runs against the active note; membership changes are announced once; switching to a non-note reports no active note rather than using the wrong file. | ☐ |
| 15 | Dataview conversion | Open a supported `dataview` fence and convert it to `.base`; try an unsupported `GROUP BY`. | Supported conversion produces a readable `.base`; unsupported conversion fails loud and names the unsupported construct. | ☐ |
| 16 | Voice Control | Use "Show numbers" on grid rows, builder buttons, saved-query rows, and dashboard sections. | Numbered targets are actionable and labels are unique enough to disambiguate. | ☐ |
| 17 | Switch Control | Cycle through grid, builder, Queries leaf, and Base dock. | No keyboard trap; Escape exits inner modes before closing the surface. | ☐ |
| 18 | Braille inspectability | Use a braille display or VO braille viewer on grid cells, quick filter, pinned saved query, and dashboard section. | Current value/state is exposed on the focused element, not only in live-region speech. | ☐ |
| 19 | Dynamic Type | Run through table, list, builder, Queries leaf, and dashboard at largest accessibility text size. | Text reflows without clipped labels, overlapping controls, or hidden action buttons. | ☐ |
| 20 | Increase Contrast / Reduce Motion | Repeat core grid and dashboard navigation with both settings enabled. | Contrast remains readable; selection/focus state is visible; updates do not depend on motion. | ☐ |

Residual: the GitHub milestone remains open until this checklist has a human PASS record or follow-up issues for every failed item. Automated CI cannot replace this run.
