# Graph

> Shortcuts shown are the shipped defaults; the in-app Command Palette (⌘⇧P) is always the authoritative list.

The Graph tab shows your vault as a **link graph** and — because the graph is a projection of a structured model, not a picture painted on a canvas — it is navigable, announced, and actionable end to end, on the visual surface itself and through fully accessible list projections. This guide covers the model, the projections, filters/presets/groups/forces, the keyboard and VoiceOver reference, and the on-disk config format.

## What the graph shows

- **Nodes** are the things in your vault:
  - **Note** — a real Markdown file.
  - **Attachment** — any indexed non-Markdown file in the vault (images, PDFs, …). Attachment nodes are hidden by default; show them with the **Attachments** filter.
  - **Ghost** (unresolved) — a `[[wikilink]]` whose target doesn't exist yet. A ghost isn't a file; it's a placeholder you can turn into a real note with one action ("Create note").
- **Edges** are the references between nodes, and they are **typed**: a plain `[[wikilink]]` is a *link* edge, an `![[embed]]` is an *embed* edge. The Table reports links-in/out and embeds-in/out as **separate reference counts**, so linking and embedding are distinct facts rather than one blurred number. (These are reference *multiplicities* — several `[[links]]` to the same target from one note add up.)
- **Orphans** are notes with no links either way; **components** are the disconnected islands of the graph; a node's size in the Diagram grows with its **incoming reference count** (its hubness).

## The projections — one model, three views

- **Table** (the default) — every node as a sortable, filterable row: links in/out, embeds in/out, component, folder, kind, modified. This is the **complete, navigable list**: the whole graph, keyboard- and VoiceOver-first, at any scale.
- **Diagram** — the visual force-directed layout: nodes as circles (sized by incoming references), edges as lines. Not an opaque image — **every node is a real accessibility element** with a spoken description (its label and link counts), reachable by keyboard and VoiceOver, with per-node actions.
- **Connections** (a right-pane leaf, not a tab mode) — the *local* graph around the note you're on: its immediate neighbourhood as in/out lists you can walk and re-root, to depth 1–3.

The **Table and Diagram** are two views of the *same* graph under the *same* backend filter, so a node keeps its identity across them. (The **Connections** leaf shows a note's local neighbourhood under its own fixed filter — attachments and unresolved shown — independent of the tab's filter bar.) Switch between Table and Diagram with the **Table / Diagram** control at the top of the tab. **Selection is shared:** selecting a row and switching to Diagram lands you on the same node's element (and vice-versa); re-rooting Connections on a node selects it in the Table/Diagram too.

> **The Diagram is one view among equals, not the main event.** On a large vault the Table is the better tool. Past the point where a picture stops helping (~1,500 visible nodes), the Diagram drops to a density **summary** and points you back to the Table — which always has every node, navigable. This is a deliberate, honest ceiling, not a bug: see the [research brief](../plans/11_graph/01_research_brief.md) §8.

## Filters, presets, groups

- **Filters** — a name query (case/diacritic-insensitive substring) plus **Attachments** (include attachment nodes), **Unresolved** (include ghosts), and **Orphans only** (notes with no links either way). They live in the **filter bar** and in the Diagram **inspector** (the panel toggle at the trailing edge, available in both modes). Both the Table and the Diagram apply the same name predicate and backend toggles, so they always show the same node set.
- **Presets** — one-command answers to the questions people actually ask a graph: **Orphaned notes**, **Unresolved links**, **Most linked**. They're **Command-Palette commands** (search "Graph:"), each of which parameterizes the same Table/Diagram — not a separate surface.
- **Groups** — colour rules in the inspector that tint matching nodes **in the Diagram**: a query → a palette colour **plus a ring style** (solid / dashed / double / dotted). **First match wins** (Obsidian parity). Colour is never the *only* signal — a grouped node's ring is heavier than an ungrouped one, distinguished by a dash pattern (dashed / dotted) or extra width (double), so membership reads without relying on colour (WCAG 1.4.1). The palette's eight slots each clear APCA contrast against the graph background in both light and dark.

## Display and forces (Diagram)

The inspector's **Display** section tunes the drawing: **Arrows** (draw edge direction), **Text-fade zoom** (the zoom below which labels hide), **Node-size multiplier**, and **Link thickness**. The **Forces** section is the four Obsidian-parity sliders (0–1): **Center**, **Repel**, **Link**, and **Link distance**. Dragging a force re-heats the layout and it re-settles live; the changed value and the settled state are announced. The layout kernel is deterministic (same graph + forces ⇒ same layout) and runs in Rust off the main thread, with Barnes–Hut scaling for large graphs.

## Keyboard

On the Diagram surface (when it holds focus):

| Shortcut | Action |
| --- | --- |
| Arrow keys | Move selection to the nearest node in that direction — graph neighbours first, then the nearest visible node. |
| Tab / ⇧Tab | Move to the next / previous node in reading (key) order. |
| Type a name | Jump to the first node whose label starts with what you type. |
| Return | Open the selected node (a ghost becomes a new note). |
| ⌃⌘I | **Graph: Where Am I?** — reads the selected node, its component, the zoom level, and the active filters (backend toggles + any name query / preset). |
| ⌘= | **Graph: Zoom In** — zoom the visual diagram in (the zoom level is announced). |
| ⌘- | **Graph: Zoom Out** — zoom the visual diagram out. |
| ⌘0 | **Graph: Actual Size** — reset the diagram zoom to 100 percent. |
| ⌥⌘0 | **Graph: Fit Graph** — zoom so every node is visible. |

The zoom chords **and ⌃⌘I** are focus-routed: they drive the active surface — the visual canvas on a canvas tab, the visual diagram on a graph tab in Diagram mode (⌃⌘I also the Bases grid), and editor text zoom everywhere else. Each also has a Command-Palette entry (search "Graph:").

Each node's actions come from one **canonical set** shared across the projections: a real note offers **Open**, **Open in New Tab**, **Show connections** (re-roots the Connections panel on it), and **Reveal in File Tree**; a ghost offers **Create note** (materialise it). They're available from the keyboard, VoiceOver's actions rotor, and Voice Control. The Diagram adds **Pin** (freeze a node in place); pinning is diagram-only.

## VoiceOver walkthrough

A pure-VoiceOver pass across the projections:

1. Command Palette → **"Graph: orphaned notes"** — the Graph tab opens (Table, Orphans preset) and the orphan count is announced.
2. Move to the **first row** — VoiceOver reads the row's cells (label, links in, links out, and the rest of the columns).
3. Switch to **Diagram** — the selection ring and VoiceOver focus land on the *same* node; "Diagram mode." is announced.
4. **Arrow** to a neighbour — spatial navigation prefers graph neighbours; you hear the node's spoken copy (*"{label}, {n} links in, {m} links out"*; ghosts: *"{label}, unresolved, {n} references"*), and its neighbours ride a "Connects to" custom-content field.
5. **⌃⌘I "Where am I?"** — node copy + component + zoom + active filters, spoken as one assertive summary.
6. Node action **"Show connections"** — the Connections leaf re-roots on that node; set depth to 2; the neighbourhood's audio summary plays.
7. **⌘[** — step back to the previous root.
8. Switch back to **Table** — focus lands on the shared-selected node's row.

Every graph announcement routes through one graph announcer.

## Reduce Motion

With Reduce Motion on, the Diagram skips the settle animation entirely: the layout is computed to completion and drawn in a single step, with no drift.

## Where it's stored — `.slate/graph.json`

Your graph-tab settings persist per vault in `.slate/graph.json` (schema v1):

```json
{
  "version": 1,
  "filters": { "includeAttachments": false, "includeGhosts": true, "orphansOnly": false, "nameQuery": "" },
  "groups": [ { "query": "project", "colorToken": "green", "ringStyle": "dashed" } ],
  "display": { "arrows": false, "textFadeZoom": 0.55, "nodeSizeMultiplier": 1.0, "linkThickness": 1.0 },
  "forces": { "center": 0.5, "repel": 0.5, "link": 0.5, "linkDistance": 0.5 },
  "mode": "table",
  "connectionsDepth": 1
}
```

It's plain, human-readable JSON written **atomically** (temp file + rename, so a crash never leaves a half-written file). Unknown top-level keys a newer Slate might add are **preserved** on rewrite, and an unreadable or newer-version file is **never overwritten** — your settings can't be silently clobbered. Only Slate writes this file, so there's no lock (unlike `prefs.json`).

## Obsidian parity — and the deliberate differences

Slate mirrors the controls Obsidian users already know (center / repel / link / link-distance forces, filters, colour groups), stored vault-locally in plain JSON — no lock-in. Where Slate diverges, it's on purpose (rationale in the [research brief](../plans/11_graph/01_research_brief.md)):

- **Accessibility-first ordering** (§8). The Table — a complete, navigable list — is the default, and the visual Diagram is a peer projection with real per-node accessibility, not an inaccessible centrepiece. This is the category's first screen-reader-usable graph.
- **Typed embed edges** (§3). Links and embeds are distinct edge kinds and counted separately, rather than merged into one undifferentiated line count.
- **No decorative build-up animation** (§2). The layout settle is functional (you can watch it or, with Reduce Motion, skip straight to the result); there's no "Animate" toggle whose only job is a reveal flourish. The kernel is deterministic and census-tested — the same graph always lays out the same way — which a non-deterministic force sim (e.g. ForceAtlas2) can't promise.
- **An honest scale ceiling** (§8). Past the cognitive ceiling the Diagram summarises and defers to the Table, rather than pretending a hairball is insight.
