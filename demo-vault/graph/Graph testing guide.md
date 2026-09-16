# Graph testing guide

This folder is the material for testing the graph view by hand — the
table, the Connections leaf, the diagram, the inspector — against the
W6-2 accessibility checklist (`docs/plans/18_windows_port/reports/w6_2_graph_at_checklist.md`).
Start at [[Graph hub]]: it is the most-linked note in the vault, its
spokes fan out one letter per name for type-ahead, and its chain runs
three links deep for the Connections leaf's depth control.

## What is here, and which check it serves

- **The hub and its eight spokes** ([[Graph hub]], Alpha station through
  Hotel harbour): the table walk and every sort — the spokes differ in
  links in, links out, embeds in and embeds out, so each column orders
  the rows differently; the Most linked preset lands on the hub.
- **The depth chain** (Graph hub → Bravo relay → Bravo relay detail →
  Bravo relay detail deep): the leaf's depth 1, 2 and 3 grow the tree
  by one ring each; Enter on Bravo relay re-roots, Ctrl+[ comes back.
- **The island** (Island north, south, east): a component of its own,
  so the Component column reads one number for the three island notes
  and another for the hub's cluster (every orphan is a component too,
  so the column has many values; sorting by it groups them).
- **The orphans** (Orphan lighthouse, deep/Deep orphan): rows for the
  Orphans preset and the orphans-only filter.
- **The ghosts** (four unresolved links from Foxtrot beacon and Project
  Beta plan): rows for the Unresolved preset and the ghosts filter; on
  the Connections leaf a ghost row's Create note action creates the file
  — `Café ghost` at the vault root, `Ghost in a folder` under
  `graph/missing/` — delete them again to reset.
- **The attachments** (the photo and the document linked from the hub):
  toggle the Attachments filter to see two rows appear and vanish.
- **The colour groups**: in the inspector add a group whose query is
  `relay` (three notes), another `Project` (three notes and one ghost),
  a third `island` (three notes) — the group matches the label,
  case-insensitively, anywhere in it. Item 10's Contrast-theme check
  wants one group configured and a node selected.
- **Type-ahead** in the diagram: the names begin with distinct letters
  (A, B, C, D, E, F, G, H, I, O, P) — press a letter to jump.
- **The pan at the edge**: at actual size the vault's sixty-eight rows
  (sixty-three notes and five ghosts; seventy with the attachments
  shown) spread past the window; walk the diagram's nodes with the
  screen-reader cursor past the edge and the view pans.
- **Tier B** is not in this vault: generate it with
  `python scripts/make_graph_tier_b_vault.py <folder>` (1,500 notes
  and a hub: 1,501 nodes) and open that folder — "Large graph" is
  spoken once and one summary element stands in for the per-node peers;
  `--notes 1499` gives the 1,500-node tier-A comparison.

Everything else in the vault stays as it was: the recipes, the linear
algebra cluster and the two Index notes join this cluster through the
hub's links to [[Apple pie]] and the spokes' links to [[Cory Joseph]]
and [[Slate]], so the whole vault is one component plus the island.
