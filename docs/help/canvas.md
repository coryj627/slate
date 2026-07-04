# Canvas

> **Draft** — this guide describes the Canvas feature as specified for Milestone T. Shortcuts shown are the planned defaults; the in-app Command Palette (⌘⇧P) is always the authoritative list.

A canvas is a spatial board where you arrange cards — snippets of text, notes from your vault, images, and links — and draw labelled connections between them. Slate reads and writes the same `.canvas` files as Obsidian ([JSON Canvas](https://jsoncanvas.org)), so your canvases stay portable.

Slate's canvas is built so that **you never need to see it to use it**. Every card, group, and connection is available as a structured outline and a sortable table; every action — creating, connecting, arranging, deleting — has a named command that works from the keyboard, the Command Palette, and Voice Control. The visual board is one view among equals, not the main event.

**Two things work differently from Obsidian, on purpose:**

- **Link cards don't embed live websites.** A link card shows the page title and site, and *Open in Browser* opens it where the web is actually accessible. (Obsidian renders the website inside the canvas.)
- **Undo is real.** Every canvas change is recorded and reversible with ⌘Z, with a spoken description of what was undone. (Obsidian's canvas undo is separate toolbar buttons.)

## Opening a canvas

Canvas files appear in your file tree and in Quick Open (⌘O) like any note. Opening one creates a canvas tab. To create a new canvas, use **New Canvas** from the Command Palette or the File menu.

When a canvas opens, focus lands on the **outline** — a structured view of everything on the board, in a predictable reading order (top-to-bottom, left-to-right, group by group). From there you can switch views with the **Show Outline / Show Table / Show Visual / Show Navigator** commands. Whatever you select stays selected when you switch — the views are four windows onto the same board.

If the canvas is empty, you'll land on a short message telling you exactly how to create your first card. If a file has content Slate doesn't understand, you'll hear how many items were preserved-but-hidden; nothing is ever deleted from the file.

## Getting oriented

- **Where am I? (⌃⌘I)** — at any moment, this reads back your full context: the card you're on, its group, its position ("4 of 14"), its connections, its color, and whether it's marked. The same information appears in a small panel you can read with a braille display.
- **Verbosity** — in Settings, choose how much detail navigation announcements carry: *terse* (just the title), *standard* (title, type, position), or *verbose* (adds connections, color, marks).
- Your position, marks, active mode, and any filter are always readable from the element under your cursor — announcements are a convenience, never the only copy.

## Navigating

In the outline and navigator:

- **↑ / ↓** — previous / next card in reading order. You'll hear "Card 4 of 14 in group Research"-style context.
- **Enter / exit group** — step into a group's cards or back out.
- **→ / ←** (navigator) — follow a connection forward, or back the way you came. Connections read with their direction and label: "Connects to *Ideas*, labelled *supports*".
- **Jump to connected card** — pick from the card's connections by number.
- **Trace path** — walk the chain of connections from the current card, hearing each hop.

**VoiceOver rotors** let you jump by *card*, *group*, or *connection*. The table view (sortable by type, title, group, target, connections, or color) is often the fastest answer to "what's in here?".

## Finding things

Press **⌘F** in a canvas to filter it. Type part of a title, a type ("image"), or a group name; the outline and table narrow as you type and you'll hear the match count ("3 cards match"). Esc clears the filter first, then leaves the canvas — you'll hear each step.

## Creating cards

- **New Card (⌥⌘N)** — creates a text card next to your current card (below it if there's room) and puts you straight into editing. On an empty canvas it starts at the origin. You'll hear where it landed: "Created text card below 'Research'."
- **New Group** — creates a labelled group; you'll be prompted for the label.
- **Create Connected Card (⌃⌥⌘N)** — the mind-mapping move: creates a new card *already connected* to the current one and starts editing. Variants let you choose the direction (below, right, above, left).
- **Note, image, and link cards** — use *Add Note to Canvas* (picks a note from your vault), *Add Media*, or paste a URL with *Add Link Card*.

## Editing

- **Text cards** open in Slate's real note editor — the same editor, shortcuts, and VoiceOver behavior as your notes. Esc saves and returns you to the card.
- **Rename Group** and **Edit Connection Label** are commands on the group/connection rows and in the palette.
- **Set Color** assigns one of the six named colors (or a custom one). Colors are always announced and shown by *name* — color is never the only way information is conveyed.
- **Convert Card to Note** turns a text card into a real note file in your vault; the card becomes a link to the new note.

## Arranging

You never need to drag anything, and you never need coordinates:

- **Placement commands** — *Place Below…*, *Place Right of…*, *Align With…* open a card picker (nearest cards first — type to filter). Slate finds a clear spot and tells you where the card went.
- **Move Mode (⌃⌘G)** — for fine control. Arrows nudge the card one grid step (hold ⇧ for big steps). You'll hear its position relative to its neighbors — "Below 'Research', right of 'Ideas'" — and a warning if it starts overlapping another card. **Return** places it; **Esc** puts it back exactly where it was.
- **Resize Mode (⌃⌘R)** — same idea: ←/→ adjust width, ↑/↓ height, with *Fit to Content* and *Default Size* one command away.

Every mode announces how to get out when you enter it, shows its state on the canvas element itself, and cancels safely if you switch away.

## Connecting cards

- **Connect To… (⌃⌘C)** — opens the card picker; choose the target and the connection is made, attaching at the nearest edges automatically. An optional details step lets you set the sides, the direction (one-way, both ways, or no arrows), and a label.
- **Connect Mode** — or navigate there instead: enter connect mode, move to the target card the way you normally navigate, and press Return to confirm.
- Deleting a connection, changing its direction, or renaming its label all happen from the card's connection list or the palette.

## Working with several cards

Selection moves; **marks stick**. Press **⌃⌘M** on any card to mark it ("Marked — 3 cards marked"), on any view. The **Marks List** shows everything marked, lets you unmark or jump, and *Clear All Marks* resets.

*Group Marked Cards*, *Move Marked Cards*, *Delete Marked Cards*, and *Set Color of Marked Cards* act on the whole set at once — one announcement, and one ⌘Z brings it all back.

## The visual view

The visual board pans, zooms, and shows the same selection as everywhere else:

- **⌘= / ⌘- / ⌘0** — zoom in / out / actual size.
- **⇧1 / ⇧2** — fit the whole canvas / zoom to the selection (same keys as Obsidian).
- **Viewport Follows Selection** (on by default) keeps whatever you select in view, whichever view you selected it from.

The visual view is fully accessible too: VoiceOver reads each card as a real element, and Voice Control's **"Show numbers"** puts a number on every card so you can act on it by voice.

## Deleting and undoing

*Delete Card*, *Delete Connection*, and *Delete Group* (which offers *Ungroup* — keep the cards, drop the box) all confirm what happened and remind you: "Deleted card 'Research' — ⌘Z to undo."

**⌘Z / ⇧⌘Z** undo and redo any canvas change, each with a description ("Undid: move 'Research'"). Changes save automatically as you make them; if another app changed the file underneath you, Slate tells you immediately and offers to reload, overwrite, or save a copy — it never silently loses either version.

## Command reference

Every canvas command lives in the Command Palette (⌘⇧P) under **Canvas** — that list is always current. Planned default shortcuts:

| Command | Shortcut |
|---|---|
| New Card | ⌥⌘N |
| Create Connected Card | ⌃⌥⌘N |
| Where am I? | ⌃⌘I |
| Toggle Mark | ⌃⌘M |
| Move Mode | ⌃⌘G |
| Resize Mode | ⌃⌘R |
| Connect To… | ⌃⌘C |
| Filter Canvas | ⌘F |
| Zoom In / Out / Actual Size | ⌘= / ⌘- / ⌘0 |
| Fit Canvas / Zoom to Selection | ⇧1 / ⇧2 |
| Next / Previous Card | ↓ / ↑ |
| Follow Connection Forward / Back | → / ← |
| Undo / Redo | ⌘Z / ⇧⌘Z |

*(Arrow and ⇧-number shortcuts apply while a canvas view has focus. If you use VoiceOver Quick Nav, use the named commands — the palette reaches everything.)*

## Troubleshooting

- **"N unsupported items are preserved in the file but not shown"** — the canvas contains node types or fields Slate doesn't model yet. They remain untouched in the file and survive every edit you make.
- **"File not found" on a card** — the note or image it points to moved outside Slate. The card stays navigable; use *Locate…* to repoint it.
- **A conflict message when saving** — the file changed on disk (sync, another editor). Pick *Reload* to take the disk version, *Overwrite* to keep yours, or *Save a Copy* to keep both.
