# U2 — File tree + full file management

**Goal.** Replace the flat file list with a real collapsible folder tree, and give it full file management: create folders, move and rename files and folders — and, critically, **rewrite links when things move** so a reorganization never breaks the vault's wikilinks, embeds, or backlinks. Keyboard-first throughout; drag is an enhancement, never the only path.

**Depends on:** U0. **Parallel:** U1 (disjoint surfaces — core + left sidebar vs. center workspace).

**Milestone-level risk:** high on the core side. U2-3 link-integrity-on-move is the correctness lynchpin — census it hardest. All mutations go through the atomic write + oplog + conflict-detection machinery; no shortcuts.

## Issues

### U2-1 · Backend: directory-tree API over the scan `backend`
- Expose a hierarchical view (folders → children) over the existing file index with stable node ids, sorted deterministically; paged/lazy-friendly per the mobile-API discipline (no whole-vault materialization).
- **Tests / census:** tree shape over deep + wide + unicode-named vaults; stable ids across rescans.
- **Acceptance:** the Swift tree can be built lazily from the API for a 10k-file, deeply-nested vault without loading everything.

### U2-2 · Backend: directory mutations — create / move / rename folder `backend` `schema`
- `create_folder`, `move(path, newParent)`, `rename_folder` on `VaultSession`, mirrored through uniffi. Atomic; each mutation is an op-log entry (reversible); external-change / collision conflicts detected and surfaced (same conflict contract as file save).
- **Tests / census:** adversarial census on mutation sequences — path integrity, no lost files, collisions rejected safely, op-log reversibility. Never overwrite in place.
- **Acceptance:** folder create/move/rename are atomic, reversible, and conflict-safe under concurrent external change.

### U2-3 · Backend: link integrity on move / rename `backend` `test`
- When a file or folder moves or is renamed, rewrite affected wikilinks, markdown links, embeds, and update the backlink/link tables so no reference dangles (Obsidian's "update links on move" behavior). Transactional with the move.
- **Tests / RED-TEAM census:** census over a link graph — after arbitrary move/rename sequences, every previously-resolvable link still resolves and no unrelated link is touched. Round-trip: move then undo restores byte-identical link text.
- **Acceptance:** reorganizing the vault never breaks a link; the rewrite is exact and reversible.

### U2-4 · Mac UI: `FileTreeSidebar` — collapsible tree `swift-ui` `a11y`
- Replace the flat `List` with a lazy collapsible tree; expand/collapse by keyboard and disclosure; preserve selection + the existing per-file open/rename/delete.
- **DoD focus:** tree accessibility — outline/tree traits, level, expanded state, position-in-set; VoiceOver announces "<folder>, expanded, N items"; selection announced (reuse the #418 pattern); light/dark; folder icons via `SlateSymbol`.
- **Tests:** tree AX properties; lazy rendering under 10k files; keyboard expand/collapse/navigate; appearance snapshots. a11y-check 100/100.
- **Acceptance:** a keyboard/VoiceOver user can navigate the folder hierarchy, understand nesting depth, and expand/collapse without a mouse.

### U2-5 · Mac UI: accessible file-management commands `swift-ui` `a11y`
- "New Folder", "Move to Folder…", "Rename Folder", "Move file to…" via context menu + command palette; a keyboard-accessible move target picker. Drag-to-move is added as an enhancement whose behavior is defined by these commands.
- **DoD focus:** every management action is keyboard-operable and palette-reachable; drag has a non-drag equivalent (DoD §E).
- **Acceptance:** a user can fully reorganize the vault — new folders, moves, renames — using only the keyboard.

### U2-6 · Mac UI: announcements + focus preservation across tree mutations `swift-ui` `a11y`
- After create/move/rename/delete, focus lands somewhere sensible and VoiceOver announces the result ("Moved <file> to <folder>", "Deleted <file>"); no focus loss to the window root.
- **Acceptance:** every mutation leaves focus predictable and announces its outcome; verified for each command.
