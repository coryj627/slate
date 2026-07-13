# Graph

> Shortcuts shown are the shipped defaults; the in-app Command Palette (⌘⇧P) is always the authoritative list.

The Graph tab shows your vault as a link graph — every note a node, every `[[wikilink]]` an edge, and every unresolved link a "ghost" node you can turn into a real note. It has **two projections of one model**:

- **Table** — the accessible-first grid: every node as a sortable, filterable row (links in/out, embeds, component, folder, kind). This is the complete, navigable list and the default view.
- **Diagram** — the visual force-directed layout: nodes as circles sized by how many notes link to them, edges as lines. Not an opaque picture — every node is a real accessibility element with the same spoken description the Table gives it, reachable by keyboard and VoiceOver.

Switch projections with the **Table / Diagram** control at the top of the tab. Both are views onto the same graph, under the same **backend filter** (the Attachments / Unresolved / Orphans-only toggles in the filter bar). The Table can narrow further with client-side filters that are Table-only for now — a name filter, and presets like *Unresolved Links* that show ghosts only — so switching such a Table view to Diagram shows the full backend set; the Diagram's own filters arrive with a later update. (Carrying the current selection across the switch — so focus lands on the same node in the other view — also arrives later; for now each projection tracks its own selection.)

> The Diagram is one view among equals, not the main event. On a very large vault the Table is the better tool: past the point where a picture stops helping, the Diagram drops to a summary and points you back to the Table, which always has every node.

## Filtering

The filter bar applies to both projections' node set: **Attachments** (include attachment nodes), **Unresolved** (include ghost/unresolved-link nodes), and **Orphans only** (notes with no links either way). The Table additionally has a client-side name filter; the Diagram's own filters and colour groups arrive in a later update.

## Keyboard

On the Diagram surface (when it holds focus):

| Shortcut | Action |
| --- | --- |
| Arrow keys | Move selection to the nearest node in that direction — graph neighbours first, then the nearest visible node. |
| Tab / ⇧Tab | Move to the next / previous node in reading (key) order. |
| Type a name | Jump to the first node whose label starts with what you type. |
| Return | Open the selected node (a ghost becomes a new note). |
| ⌃⌘I | "Where am I?" — reads the selected node, its component, the zoom level, and the active filters. |
| ⌘= | **Graph: Zoom In** — zoom the visual diagram in (the zoom level is announced). |
| ⌘- | **Graph: Zoom Out** — zoom the visual diagram out. |
| ⌘0 | **Graph: Actual Size** — reset the diagram zoom to 100 percent. |
| ⌥⌘0 | **Graph: Fit Graph** — zoom so every node is visible. |

The zoom chords are focus-routed: they drive the visual canvas on a canvas tab, the visual diagram on a graph tab in Diagram mode, and editor text zoom everywhere else. Each also has a Command-Palette entry (search "Graph: Zoom").

Every node exposes **Open**, **Show connections** (re-roots the Connections panel on it), and **Pin** (freezes it in place) as actions — available from the keyboard, VoiceOver's actions rotor, and Voice Control.

## Reduce Motion

With Reduce Motion on, the Diagram skips the settle animation entirely: the layout is computed to completion and drawn in a single step, with no drift.

---

*This reference covers the graph's controls and keyboard. The fuller guide — the graph model and ghost semantics, the three-projection VoiceOver walkthrough, presets, groups, force tuning, and the `.slate/graph.json` format — lands with the Milestone P documentation pass (#562).*
