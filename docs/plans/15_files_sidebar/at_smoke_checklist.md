# Milestone FL — Human AT Smoke Checklist

Status: **OPEN — not yet executed by a human.** Automated evidence (XCTest,
Accessibility Check 100.0) covers structure and labels; it cannot stand in
for a human VoiceOver pass. Do not mark any item below from automated runs.

Setup: macOS VoiceOver (⌘F5), a test vault with ≥2 nested folders, ≥20 notes,
several tags (including nested `a/b` tags), one folder note, and one folder
with >500 files. Run each flow end to end; an item passes only if every
spoken string is heard and no focus dead-end occurs.

## Tree fundamentals

- [ ] VO-walk the tree: every file row speaks name, date, and detail summary
      (task/word counts when enabled); compact density still speaks the full
      value while showing only the name visually.
- [ ] F2 rename on a focused row: editor is announced, ⏎ commits with the
      rename announcement, Esc cancels and restores focus to the row.
- [ ] Multi-select with ⇧↓/⇧↑ then open the context menu: batch verbs speak
      their target count; a disabled verb speaks its reason.
- [ ] Drag a row (VO+space alternative: Move To… ⇧⌘M): the move announces
      source and destination; sidebar focus lands on the moved note.

## Organization

- [ ] Sort submenu on a folder: applying "Sort by Name (Z to A)" announces the
      effective sort; the folder's context menu shows the override with "Use
      Vault Default Sort" enabled.
- [ ] Group by Date on: date headers ("Today", "Yesterday", …) are announced
      as headers while arrowing through rows.
- [ ] Pin a note: "Pinned." announced, row moves under the "Pinned" header;
      unpin restores it to sort position.

## Shortcuts, Recents, filter

- [ ] Add a folder to Shortcuts; ⌃2 (its slot) activates it and announces the
      activation; in dual-pane it selects the container instead of opening.
- [ ] ⌥⌘F focuses the filter field (field announced); typing shows live
      results; ↓ enters results at row 1; Esc clears and returns to the tree.
- [ ] Commit a query with an operator (e.g. `tag:project modified:today`) —
      the result summary is announced ("N results …"); an invalid query
      announces its error without moving focus.
- [ ] ⌃⌘[ / ⌃⌘] walk selection history back/forward with announcements.

## Tags and batch tagging

- [ ] Expand the Tags section: nested tags disclose; each row speaks name and
      distinct-file count.
- [ ] Activate a tag row: tree mode shows its file list surface; dual-pane
      selects it as the Files-pane container ("Files" pane announced on
      focus arrival).
- [ ] Select 3 notes → Add Tag… (editor sheet announced) → commit: the batch
      report is announced; any skipped file is listed with its reason.
- [ ] A frontmatter tag containing a space (e.g. `project alpha`): activating
      its row (tree AND dual-pane) shows exactly its files — the summary
      names the whole tag ("N results for #project alpha.") and a file
      merely NAMED "alpha" stays out.
- [ ] Remove a tag that also appears inline in one note's body: the report
      speaks the inline-remainder honesty (frontmatter removed, body
      occurrence remains) rather than claiming full removal.

## Folder notes

- [ ] A folder with a folder note: the folder row's AX value announces the
      note's presence; the represented note row is absent from the tree.
- [ ] Label-activation opens the folder note; the chevron (dedicated 28pt
      target) only discloses; ⏎ opens the note.
- [ ] Rename the folder: one announcement covers folder + note rename; undo
      restores both.
- [ ] Delete Folder Note moves the note to Trash with announcement; Create
      Folder Note on a folder that regained none recreates and opens it.

## Dual-pane

- [ ] Settings ▸ Sidebar ▸ Layout → Dual-pane: "Dual-pane sidebar."
      announced; panes labeled "Folders" and "Files"; the transition
      announces the pane focus lands in.
- [ ] → on an expanded folder moves focus into the Files pane; ← / Esc
      returns; the pane transition is announced each way.
- [ ] The divider: VO increment/decrement adjusts it and speaks the
      percentage; it clamps at 20%/80%.
- [ ] Include Subfolders on: subtree rows speak their containing-folder
      subtitle; header count updates; the >500-file folder still lists to
      completion (header says "N files", or "first 10,000 files" if capped).
- [ ] Multi-select in the Files pane → context menu: identical verbs and
      spoken reasons as the same selection in tree mode (§FL-D parity spot
      check).
- [ ] Toggle back to Tree with the palette command: "Tree sidebar."
      announced; tree selection/expansion state intact.

## Recording

| Item | Pass/Fail | Tester | Date | Notes |
| --- | --- | --- | --- | --- |
| (fill per row above) | | | | |
