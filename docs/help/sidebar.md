# Files Sidebar

> Shortcuts shown are the shipped defaults; the in-app Command Palette (⌘⇧P) is always the authoritative list.

The Files sidebar is Slate's vault navigator: a metadata-rich file tree with multi-select and batch operations, per-folder sorting/grouping/pins, shortcuts and recents, a deterministic filter, a tag tree with batch tagging, folder notes, and an optional dual-pane layout. Every surface is keyboard- and VoiceOver-first: everything the mouse can do has a command, a chord or a rotor action, and a spoken announcement.

## What a row shows

A file row leads with the note's **effective name**: the frontmatter `title` when the note has one, otherwise the filename stem. (The real filename is always one hover away as the tooltip, and VoiceOver reads a titled note as "Title — file name.md" so the two never blur.) Under the name, a metadata line and optional extras — all configurable in **Settings ▸ Sidebar**:

- **Date** — modified or created (your choice), relative ("2 days ago") or absolute. "Created" prefers the note's authored date property, then the parsed creation time; a note with neither shows its modified date **labeled Modified** — the row never dresses an mtime up as a creation date.
- **Preview** — the note's first lines of body text, 0–3 lines (off by default).
- **Task badge** — on notes containing tasks: "2/5" open-of-total, switching to a checkmark glyph when everything's done (on by default).
- **Word count** — appended to the metadata line (off by default).
- **Pin badge** — pinned notes carry a pin glyph.

**Density** (Standard / Compact) controls how much of that renders visually: Compact shows just the name and pin — but VoiceOver keeps speaking the **full** date, preview, and task summary either way, so screen-reader users lose nothing to visual minimalism. In flat lists (filter results, subtree listings) rows add a containing-folder subtitle, spoken as "in folder".

## Selecting files and acting on them

The tree is a real macOS multi-select list: ⇧-click (or ⇧↓/⇧↑) extends a range, ⌘-click toggles membership, and on a folder-note folder a ⌘/⇧ click selects without opening or disclosing. Whatever you select becomes the **one shared target** for every action surface — context menu, File and View menus, Command Palette, keyboard chords, and VoiceOver rotor actions all read the same selection and evaluate availability from the same catalog. How they *present* an unavailable verb differs by surface, deliberately: the menu bar, Command Palette, and keyboard keep it visible and **speak the reason** ("Select exactly one folder to manage its folder note.", "Open is available only for files."), while context menus and the VoiceOver rotor stay concise and omit what doesn't apply.

The verbs, and what they need:

| Action | Selection | Notes |
| --- | --- | --- |
| Open (⏎) | One or more **files** | Multi-open opens each file. |
| New Note (⌘N), New Folder, New Note from Template… (⇧⌘N) | At most one item | The selection picks the creation location: a selected folder creates inside it; a selected file creates beside it. |
| Import Files and Folders… | At most one item | Choose files/folders in the open panel. External items are **copied** into the vault; items already in this vault are **moved**. Per-item results are reported. |
| Rename… (⌥⌘R, F2) | Exactly one item | Inline editor in the row; ⏎ commits, Esc cancels. |
| Move To… (⇧⌘M) | One or more items | Folder picker; announces the move; also available by dragging. |
| Duplicate | Exactly one file | Creates "copy" beside the original. |
| Reveal in Finder / Copy Path | Exactly one item | |
| Copy Wikilink | Exactly one **Markdown** file | |
| Add Tag… / Remove Tag… | One or more **files** | See Tags below. |
| Move to Trash | One or more items | Destructive; macOS Trash, so recoverable there — but not ⌘Z-undoable, and the menus never promise otherwise. |

Batch operations report per item: anything that couldn't be done is listed with its reason instead of failing the whole batch silently. Drags work out of the app (real file URLs — drop into Finder or another app) and into the tree from Finder (same copy-vs-move semantics as Import).

## Sort, grouping, and pins

**Sort Sidebar By** (View menu, a folder's context menu, or the palette) offers six orders — Name (A to Z / Z to A), Created (Newest / Oldest First), Modified (Newest / Oldest First) — plus **Group by Date** and **Use Vault Default Sort**. Applied with nothing (or the vault root) selected, a sort becomes the **vault default**; applied to a selected folder, it becomes that folder's **override**, and "Use Vault Default Sort" clears it (it's enabled exactly when the folder has one). Notes without a created value sort last under a Created order rather than pretending to be old or new.

**Group by Date** sections each level into buckets — Today, Yesterday, Previous 7 Days, Previous 30 Days, then month and year sections, with a No Date bucket at the end — as real, announced headers. Grouping implies the matching date sort, newest first; sorts and groupings that would contradict each other aren't offered.

**Pins** keep chosen notes at the top of their folder under a "Pinned" header, in the order you pinned them (not re-sorted). A pin is per-folder: renaming the note keeps it pinned; moving it to another folder drops it. Pin/Unpin live on the note's context menu ("Pin to Top of Folder" / "Unpin"), with "Unpin All in Folder" on the folder. Pins whose files vanished outside Slate are pruned quietly, at most once per session, and only once the file is confirmed gone.

None of these are content edits — they're preference changes, applied instantly, announced, and **not** part of ⌘Z history (no menu item claims otherwise).

## Shortcuts, Recents, and history

**Shortcuts** is a hand-ordered list of up to 200 destinations at the top of the sidebar: files, folders, tags, or Untagged ("Add to Shortcuts" / "Remove from Shortcuts" on any of their rows). Activating one does what its kind means — a file opens, a folder reveals (or becomes the dual-pane container), a tag or Untagged applies its scope. The first nine are reachable as **⌃1–⌃9 while the sidebar has focus** (deliberately focus-scoped, so the chords never steal from the editor; the palette's "Open Shortcut N" commands work from anywhere). Reordering moves an entry past its visible neighbor. Shortcuts are stored **in the vault**, so they follow it across machines.

**Recents** lists your last-opened files — the ten most recent, excluding the file you're in — with **Clear Recents** underneath. Recents are **device-local** (up to 50 remembered per vault, on this Mac): what you opened on another machine doesn't leak in.

**History**: the sidebar keeps a selection history like a browser — **⌃⌘[** goes back, **⌃⌘]** goes forward, with announcements. **Collapse All Folders** and **Expand Loaded Folders** (View menu) reset the tree's disclosure in one move.

## The filter

**⌥⌘F** focuses the filter field at the top of the sidebar. Typing filters as you go (committed after a short pause, or immediately with ⏎); a committed query replaces the tree below the field with a flat result list, while your tree expansion and selection wait underneath, untouched, for **Esc**. The last committed query is remembered per device and restored **into the field** on relaunch — shown, not re-applied, until you commit it.

The filter searches the vault **index** (what's been saved and scanned) and matches file **names** — not file contents. Full-text content search stays in ⌘F, deliberately: the sidebar filter answers "which files", the search overlay answers "which passages".

### The grammar

A query is a sequence of space-separated terms; a file must match **all** of them (AND — there is no OR, no parentheses, no quoting; a `"` is just a character). Any term can be negated with a leading `-`.

| Term | Matches | Example |
| --- | --- | --- |
| `word` | The file's **effective name** contains it: the authored `title` property when the note has one, else the filename stem. Case-insensitive; diacritics are significant (`cafe` does not match `café`). | `roadmap` |
| `#tag` | Files carrying the tag **or any nested child** of it (`#project` includes `project/alpha`). Case-insensitive. | `#project` |
| `@today` / `@yesterday` / `@last7d` / `@last30d` | Files **modified** in the named window. | `-@last30d` (untouched for a month) |
| `@YYYY-MM-DD` | Files modified on that calendar day. | `@2026-07-01` |
| `has:task` | Files containing at least one **open** task (completed ones don't count). | `has:task` |
| `ext:pdf` | Files with the extension (dot optional, case-insensitive). | `ext:png` |
| `path:folder/` | Files under the vault-relative folder prefix. **Case-sensitive**, exact path characters — `%` and `_` are literal. | `path:research/papers` |

Examples: `#project @last7d has:task` — files tagged project, modified this week, with open tasks. `-#archive report` — files named like "report" not tagged archive. `path:inbox/ -@last7d` — inbox files untouched for a week.

Notes on honest edges:

- A note with an authored `title` matches on that title **instead of** its filename — searching the stem of a titled note finds nothing, by design.
- Dates always mean **modified** time; there is no created-date operator.
- `has:` supports only `has:task`; `@` accepts only the four named windows and literal days.
- In dual-pane layout, a committed query scopes to the selected container (see below).

### Results and errors

Results come back in one deterministic order — effective name (case-folded), ties broken by path — and page in as you scroll. VoiceOver hears a summary on every commit: "No results.", "42 results.", or "42 results in papers." when scoped to a folder. ↓ from the field enters the results at the first row; each result row carries the same context menu as its tree row, plus a containing-folder subtitle.

A malformed term shows (and speaks) a specific inline error naming the term — for example `'@' takes today, yesterday, last7d, last30d, or a YYYY-MM-DD date`, or `'has:' supports only has:task` — and your previous results stay on screen while you fix it.

## Tags

The **Tags** section (below the tree; collapsed by default, and it remembers your choice per device) shows every tag in the vault as a tree. Tags nest on `/` — `projects/reading` appears as `reading` inside `projects` — and intermediate levels appear even when no note carries them exactly. Tags are stored and matched lowercase; `#Project` and `#project` are the same tag.

Each tag row speaks its name, its note count, its expanded/collapsed state, and its level. The count is **distinct notes carrying the tag or any nested child** — a note tagged both `a` and `a/b` counts once toward `a`. The section header summarizes the vault ("12 tags, 3 untagged notes." — the untagged clause only when there are any); the tag count is real tags only, not synthesized intermediate levels.

Activating a tag row shows its files: in tree mode as a filtered file list (plain tags fill the filter field as `#tag`, editable like any query; a tag containing spaces — legal in frontmatter — can't round-trip through the grammar, so it scopes like Untagged instead: field empty, results scoped, summary naming the whole tag), in dual-pane as the Files-pane container. An **Untagged** row at the bottom (present when any Markdown note has no tags at all) does the same for untagged notes. Both tag rows and Untagged can be added to Shortcuts from their context menus.

### Batch tagging

Select one or more files, then **Add Tag…** or **Remove Tag…** (context menu, File menu, or palette). Add suggests from the vault's existing tags as you type (prefix-matched, normalized); slashes nest. Remove lists exactly the tags the selected files actually carry — read from the index, with per-tag file counts — so you can't ask to remove something that isn't there.

Both editors edit the **frontmatter `tags:` list only**, one file at a time, atomically per file:

- Adding is idempotent — files that already carry the tag (under any spelling; matching is normalized) aren't rewritten. A file whose only occurrence is inline gains a frontmatter entry.
- Removing the last tag removes the `tags:` key entirely — and an emptied frontmatter block goes with it; no `tags: []` debris.
- Other frontmatter keys, their order, and the key's own spelling (`Tags:` stays `Tags:`) are preserved.

The report is honest about what didn't happen. Files that can't be edited safely are **skipped rather than mangled** — not indexed yet, not a Markdown note, changed on disk since it was indexed, a `tags:` property that isn't a tag list (a nested mapping or a scalar), or duplicate case-variant `tags:` keys — and the announcement reports how many: "Tagged 4 files with #draft. 2 skipped." (The per-file reasons exist in the underlying report; v1 speaks the count.) And because the editors touch frontmatter only, removing a tag that also appears as `#tag` in note bodies reports the **inline remainder**: "Removed #draft from 4 files. 2 still have it inline." — the body occurrences are intentionally untouched, and the announcement says so instead of claiming a clean sweep.

## Folder notes

A **folder note** is a note that *is* its folder: `Projects/Projects.md` (exact name match, case-sensitive, Markdown only). Folders with one get a small note badge, announce "has folder note" in their VoiceOver value, and change how activation works:

- Clicking the folder's **label** (or pressing ⏎ on the row) opens the note.
- The **chevron** becomes a dedicated disclosure target — it only expands/collapses. (On folders without a note, the whole row toggles as usual.) VoiceOver keeps Expand/Collapse as rotor actions either way, and the row's hint says which behavior you'll get.
- The note itself disappears from the tree as a separate row — the folder **represents** it. Counts stay honest (it still counts as a child), and while the note is open in the editor, the folder row shows as the selected row.

Lifecycle commands live on the folder's context menu, the File menu, and the palette — each needs **exactly one folder** selected (the reason is spoken otherwise):

- **Create Folder Note** creates and opens it. Creation is exclusive: if anything already occupies that name (on disk or in the index, even by a case variant), it refuses and nothing is written.
- **Open Folder Note** opens it; **Delete Folder Note** moves it to the Trash (destructive, so it confirms its target the way Move to Trash does).

**Renaming the folder renames the note with it**, as one operation: `Projects` → `Ideas` also carries `Projects/Projects.md` → `Ideas/Ideas.md`, with backlinks rewritten for both. It's honest at the edges: if the target name is already taken, it refuses **before** touching anything; if the second half fails midway, it rolls the folder back and the error says exactly what was restored (or where things ended up if the rollback itself failed); undo covers both halves. A folder without a note renames plainly — no phantom note appears.

## Dual-pane layout

The sidebar ships in two layouts. **Tree** (the default) is the single file tree described above. **Dual-pane** splits the sidebar into a **Folders** pane above and a **Files** pane below — the Notebook-Navigator/mail-client shape: pick a container on top, work its flat file list underneath.

Switch layouts in **Settings ▸ Sidebar ▸ Layout** (a Tree / Dual-pane picker) or with **Toggle Sidebar Layout** in the Command Palette or the View menu. The switch is announced ("Dual-pane sidebar." / "Tree sidebar."), takes effect immediately, and is **device-local** — it never writes into the vault, so the same vault can be tree-mode on one Mac and dual-pane on another. Switching preserves state in both directions: the tree, filter, selection, and expansion survive a round trip.

### The two panes

- The **Folders** pane is the navigation half: Shortcuts, Recents, a **folders-only** projection of the same tree (files are presented in the list pane instead), and the Tags section. It is one accessibility group labeled "Folders"; focus arriving in a pane announces its name.
- The **Files** pane lists the contents of the selected **container** — a folder, a tag, or Untagged. Its header shows the count ("N files", or "first N files" when a very large container is cut off at the 10,000-file ceiling — the cut is always labeled, never silent). Selecting nothing yet shows "Select a folder or tag."; an empty folder shows "No files here." with a **New Note** button that creates directly into that folder.
- Between them sits an **adjustable divider** ("Pane divider"): drag it, or use VoiceOver's increment/decrement on it, to change the split. It clamps between 20% and 80% (neither pane can vanish), speaks its position as a percentage, and its position is remembered per device.

### Containers versus leaves

Rows in the Folders pane divide into **containers** (folders, tags, Untagged — selecting one retargets the Files pane) and **leaves** (files in Shortcuts/Recents — activating one opens it and never retargets the list). Opening any note — from Recents, a shortcut, search, or Quick Open — mirrors back into the panes: the containing folder is selected above, and the note's row is selected below.

### The file list

A folder container lists its **immediate children** by default. Turn on **Include Subfolders** (in the list-pane header's display menu, or a folder row's context menu in the Folders pane) to list the whole subtree instead; subtree rows carry a small containing-folder subtitle so lookalike names stay distinguishable. Tag containers always list every file carrying the tag anywhere in the vault; Untagged lists every Markdown file with no tags at all.

The list applies the same organization as the tree — pinned notes lead under a "Pinned" header, then the folder's effective sort, with date-group headers when grouping is on — and file rows are the same component as tree rows, honoring the same date/preview/density settings and per-folder overrides.

Multi-select works like any macOS list (⇧-click ranges, ⌘-click toggles). A single selected row opens on selection; a multi-selection is a **batch target** — the context menu and every menu-bar/palette action operate on exactly the highlighted set, in visible order. Row context menus, drags to folders or out of the app, and drops into the Folders pane are all the same machinery the tree uses — same verbs, same confirmations, same announcements.

### Per-folder display overrides

The list-pane header's display menu sets, for the current folder only: **Preview Lines** (0–3), **Density** (Standard / Compact), and **Include Subfolders**. "Use Vault Default" clears an override back to inheritance. The same Preview Lines and Density items also sit in a folder's context menu in **both** layouts; Include Subfolders appears only where a file list can honor it — the list-pane header and the dual-pane Folders pane's folder rows. All three are stored in the vault's `.slate/sidebar.json` alongside the sort/grouping overrides, so they travel with the vault. Preview Lines and Density are **row** settings and apply on both surfaces — the list pane and the tree's rows for that folder. **Include Subfolders** is a *scope* setting for the dual-pane list only; the tree always shows one level per folder and discloses the rest.

### Keyboard in dual-pane

| Key | Where | Action |
| --- | --- | --- |
| → | Folders pane | On a collapsed folder: disclose it. On an expanded or childless container: move focus into the Files pane (Navigator convention). On a leaf: nothing. |
| ← | Files pane (container list) | Return focus to the Folders pane. |
| Esc | Files pane | Return focus to the Folders pane — including from filter results, where Esc is the way back. |
| ↓ | Filter field | Enter the Files pane at its first row. |
| ⏎ | Files pane | Open the selected note. |

The filter field stays topmost in both layouts; in dual-pane a committed query **scopes to the selected container** (a folder scopes the search to that folder; a tag container scopes structurally — the tag never re-enters the query grammar, so tags the grammar can't express, like ones containing spaces, still scope exactly) and its results replace the Files pane only — the Folders pane stays navigable. Untagged cannot be textually composed, so committing a query under it clears the container selection and searches the whole vault (the selection visibly reflects that).

## Keyboard and VoiceOver reference

| Keys | Context | Action |
| --- | --- | --- |
| ↑ ↓ | Tree / lists | Move selection. |
| ⇧↓ / ⇧↑ | Tree | Extend the selection. |
| ⌘A | Tree | Select the visible level. |
| ← → | Tree | Collapse / expand (→ hands focus to the Files pane in dual-pane; see above). |
| ⏎ | Tree / lists | Open the selected note (on a folder with a folder note: opens the note). |
| Space | Tree | Toggle disclosure. |
| F2 or ⌥⌘R | Row focused | Rename inline. |
| ⌘N / ⇧⌘N | Sidebar | New note / from template, into the selected location. |
| ⇧⌘M | Selection | Move To… |
| ⌥⌘F | Anywhere | Focus the filter field. |
| ↓ (in filter) | Filter field | Enter results at the first row. |
| Esc | Filter / lists | Clear the filter and restore the tree; in dual-pane lists (container rows and filter results alike), return to the Folders pane. |
| ⌃1–⌃9 | Sidebar focused | Activate Shortcut 1–9. |
| ⌃⌘[ / ⌃⌘] | Anywhere | Selection history back / forward. |

Typing letters in the tree performs type-select on the effective name. Every announcement in this guide comes through one announcement seam, so repeated operations speak once each, in order; every action surface (menus, palette, keyboard, rotor) reads the same selection and speaks the same availability reasons.

## Where things are stored

**In the vault** (`.slate/sidebar.json` — versioned, written atomically, unknown keys preserved): the vault-default sort and grouping, per-folder overrides (sort, grouping, preview lines, density, Include Subfolders), pins, and shortcuts. These describe the vault and travel with it. If the file is unreadable, malformed, or from a newer Slate, the sidebar runs read-only on defaults and says so — it never overwrites what it can't parse.

**On this device** (per-vault where it matters): the row presentation settings (date source/format, preview lines, task counts, word count, density), the sidebar layout (tree / dual-pane) and divider position, Recents, section expansion, and the filter field's restored query. These describe *your view* and stay on the machine.
