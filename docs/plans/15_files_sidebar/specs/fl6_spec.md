# FL6 executable spec — Folder notes

Issue: FL6-1 ([#667](https://github.com/coryj627/slate/issues/667)). Milestone: [GH 31](https://github.com/coryj627/slate/milestone/31). Grouped delivery: FL-12 closes #667 after FL-01 and FL-04.
Program: [00_program.md](../00_program.md) (locked decisions 10–11; DoD §FL-A). U §A–§G apply.

Baseline facts (verified 2026-07-14 at `origin/main` `6aa9fce`):

- Convention locked: a folder's note is **`<Folder>/<Folder>.md`** — exact stem match with the folder name, no `index.md` fallback (decision 10; one censusable convention).
- Folder label and chevron activation are already separate in `FileTreeSidebar`; preserve the #643 tap regression and existing folder AX conventions. Exact July 5 line references are obsolete.
- Rename/move uses the core structural/link-rewrite path and AppState publishes `TreeMutation`. Folder+note rename must extend that core operation; Swift must not sequence two independent renames.
- Tabs retarget on rename/move (U2-5).

---

## FL6-1 · Folder notes (#667) — closing PR FL-12

### Model

1. Extend `DirNodeSummary` additively with `has_folder_note`, computed in the existing `list_dir_children` statement (no eager child fetch and no per-row query). Swift maps the flag to `folderNotePath`; a debug consistency check may compare fetched children when available.
2. The folder-note file row is **hidden** from the folder's expanded children (it is represented by the folder row itself; duplicate rows double VO walks). It still counts in `child_file_count` (changing count semantics ripples into U2 tests; the AX phrase "N items" stays honest either way — note in PR).

### Interaction

3. **Label activation** (click on label / Return on a selected folder row) on a folder **with** a note opens the note (existing open seam, tab rules unchanged); the chevron (and ←/→, Space) still discloses. Folders **without** a note keep today's behavior exactly (label click selects; Return toggles disclosure — no behavior change for the common case).
4. Context menu additions (folder rows): **Create Folder Note** (absent: creates `<Folder>/<Folder>.md` via the existing create path — template picker NOT wired in v1, plain note — opens it), **Open Folder Note** (present; explicit path for discoverability), **Delete Folder Note** (present; existing delete confirmation + trash path). No "detach": the convention is name-based, so detaching *is* renaming the note (the rename flow already exists on the note itself via Open → rename).
5. **Rename folder** ⇒ the folder note renames with it as one core compound operation. Core owns complete collision/link-rewrite preflight, both mutations, progress journal, rollback/error report, backlink repair, and tab-retarget data. Do not describe the filesystem pair as crash-atomic; rollback failure is reported honestly. Move-folder needs no separate note step because the note moves with its folder.
6. If a file rename/create *makes* or *breaks* the stem match, presence updates via the normal level invalidation — no special watcher.

### Presentation & AX

7. Folder-with-note renders the folder glyph with a small note-badge overlay (SlateSymbol composition; APCA-checked both appearances); AX value appends `", has folder note"`. When the note is active, the existing editor-to-sidebar mirror maps it to the folder row because the file row is hidden.
8. Announce on create: existing `createNote` announcement suffices (`"Created note <Folder>."`).

### Tests

- FFI: `has_folder_note` EXISTS correctness (stem match, case sensitivity, non-markdown ignored); CLI-additive note.
- VM: hidden child row; count phrase honesty; presence updates on rename-in/rename-out fixtures.
- Interaction: label-open vs chevron-disclose (incl. the #643 regression suite); Return semantics with/without note; menu verbs.
- Compound rename: folder rename renames note + rewrites backlinks (fixture with inbound links to both folder-note and sibling files); injected second-step failure/rollback and rollback-failure honesty; tab retarget on the note.
- AX: value string; active-highlight mapping; a11y 100/100 on tip.

- [ ] `has_folder_note` FFI + VM model + hidden row
- [ ] Label-activation split + menu verbs
- [ ] Atomic folder+note rename with link rewrite
- [ ] Badge + AX + tests
