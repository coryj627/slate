# FL6 executable spec — Folder notes

Issue: FL6-1 ([#667](https://github.com/coryj627/slate/issues/667)). Milestone: [GH 31](https://github.com/coryj627/slate/milestone/31). One PR.
Independent; any time after Wave 2 (uses FL2's menu conventions). Program: [00_program.md](../00_program.md) (locked decision 10; DoD §FL-A). U §A–§G apply.

Baseline facts (verified 2026-07-05):

- Convention locked: a folder's note is **`<Folder>/<Folder>.md`** — exact stem match with the folder name, no `index.md` fallback (decision 10; one censusable convention).
- Folder rows: FileTreeSidebar.swift:1019–1084 (label click vs chevron click are already distinct code paths — the #643 tap-gesture fix separates them); folder AX value :1486–1492.
- Rename/move flow rewrites links via `link_rewrite::plan_rewrites` + affected-file re-scan (fl0/p0 baseline); `TreeMutation` kinds per fl2 baseline; `DirListing` supplies each level's files, so folder-note presence is computable per level with no extra query.
- Tabs retarget on rename/move (U2-5).

---

## FL6-1 · Folder notes (#667) — PR 1

### Model

1. `TreeNode` folders gain `folderNotePath: String?`, derived in the view model while building a level: set iff the *fetched children* of that folder contain a markdown file whose stem equals the folder name (case per filesystem comparison used elsewhere in the tree — match the existing name-collision convention). Because children are lazily fetched, the folder-note state for a *collapsed, never-fetched* folder is unknown — **derive it from the parent's listing instead**: when level N is fetched, each child folder's note presence is checked against level N+1? No — that reintroduces eager fetching. **Rule:** presence derives from the folder's own fetched children when available; otherwise the FFI supplies it — extend `DirNodeSummary` with `has_folder_note: bool` computed in the same `list_dir_children` statement (one EXISTS subquery; additive field, same CLI note as FL0-2). The Swift side prefers the FFI flag; the child-derived path is only a consistency assert in debug.
2. The folder-note file row is **hidden** from the folder's expanded children (it is represented by the folder row itself; duplicate rows double VO walks). It still counts in `child_file_count` (changing count semantics ripples into U2 tests; the AX phrase "N items" stays honest either way — note in PR).

### Interaction

3. **Label activation** (click on label / Return on a selected folder row) on a folder **with** a note opens the note (existing open seam, tab rules unchanged); the chevron (and ←/→, Space) still discloses. Folders **without** a note keep today's behavior exactly (label click selects; Return toggles disclosure — no behavior change for the common case).
4. Context menu additions (folder rows): **Create Folder Note** (absent: creates `<Folder>/<Folder>.md` via the existing create path — template picker NOT wired in v1, plain note — opens it), **Open Folder Note** (present; explicit path for discoverability), **Delete Folder Note** (present; existing delete confirmation + trash path). No "detach": the convention is name-based, so detaching *is* renaming the note (the rename flow already exists on the note itself via Open → rename).
5. **Rename folder** ⇒ the folder note renames with it **atomically in the same core operation** — extend the existing folder-rename flow so the note's stem follows the folder name, riding the same link-rewrite plan (backlinks to the note update; this is the whole point of the convention). Move-folder needs no special casing (the note moves with its folder; links rewrite per the existing move path). Delete folder already deletes children.
6. If a file rename/create *makes* or *breaks* the stem match, presence updates via the normal level invalidation — no special watcher.

### Presentation & AX

7. Folder-with-note renders the folder glyph with a small note-badge overlay (SlateSymbol composition; APCA-checked both appearances); AX value appends `", has folder note"`. When the folder note is the active editor file, the folder row shows the active-row highlight (the mirror seam :791–810 maps the note path to the folder row since the file row is hidden).
8. Announce on create: existing `createNote` announcement suffices (`"Created note <Folder>."`).

### Tests

- FFI: `has_folder_note` EXISTS correctness (stem match, case sensitivity, non-markdown ignored); CLI-additive note.
- VM: hidden child row; count phrase honesty; presence updates on rename-in/rename-out fixtures.
- Interaction: label-open vs chevron-disclose (incl. the #643 regression suite); Return semantics with/without note; menu verbs.
- Rename atomicity: folder rename renames note + rewrites backlinks (fixture with inbound links to both folder-note and sibling files); tab retarget on the note.
- AX: value string; active-highlight mapping; a11y 100/100 on tip.

- [ ] `has_folder_note` FFI + VM model + hidden row
- [ ] Label-activation split + menu verbs
- [ ] Atomic folder+note rename with link rewrite
- [ ] Badge + AX + tests
