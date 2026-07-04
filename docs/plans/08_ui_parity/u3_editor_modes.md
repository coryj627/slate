# U3 — Editor: reading/editing modes + inline properties

**Status: ✅ Complete (2026-07-05).** All five issues shipped and merged: U3-5 body-only buffer, Rust half (#469 → PR #500; Swift flip deliberately deferred) — completed by U3-3; U3-1 reading blocks + ReadingView (#465 → #504 Rust, #514 view); U3-2 mode toggle ⌘⇧E (#466 → #515); U3-3 in-note properties widget + the U3-5 editor flip (#467+#469 → #528; NotePartsBundle gained exact body offsets — the one conversion authority); U3-4 show-source YAML toggle ⌘⇧D (#468 → #530). Round-trip law + widget/body interleave censused at 100k (release). Reading follow-ups: #508 tag scope, #509 anchored destinations, #510 table grid, #511 in-place embeds.

**Goal.** Give the editor the two things Obsidian users expect: a **Reading view** (fully rendered, navigable, read-only) and an **Editing view** (today's source `NSTextView`), toggled per tab — and move **Properties into the note** as a rich editable widget at the top, with a "show source" YAML escape hatch. Reading view finally puts the math/Mermaid/code/citation pipelines *inline* instead of only in side panels.

**Depends on:** U1 (mode + properties are per-tab). **Parallel:** U4. **Unblocks:** U5.

**Milestone-level risk:** medium-high. Reading-view VoiceOver continuous-read across rendered blocks is the a11y-sensitive part (apply `textSelection`/labels at the leaf `Text` level, per the prior learning — container scope breaks continuous read). The body-only source buffer must round-trip byte-identically with the properties widget.

## Issues

### U3-1 · Mac UI: Reading view — rendered, navigable, read-only `swift-ui` `a11y` `editor`
- A rendered document view composing the existing pipelines (pulldown-cmark structure, MathCAT speech, code preambles/highlight, Mermaid descriptions, embeds, citations) into inline content — not side panels.
- **DoD focus:** VoiceOver continuous read across blocks (leaf-level `Text` scope); heading rotor (VO+H) walks headings in document order; links activatable; task checkboxes toggle-able; embeds expandable; images carry alt. Light/dark; Dynamic Type reflow.
- **Tests:** rotor order; link/task/embed activation; continuous-read scope; math/code/mermaid surfaced inline; appearance snapshots. a11y-check 100/100.
- **Acceptance:** a VoiceOver user can read a fully-rendered note top-to-bottom continuously, navigate by heading, and activate links/tasks/embeds — parity with the source view's navigability.

### U3-2 · Mac UI: Reading ↔ Editing mode toggle (per tab) `swift-ui` `a11y` `editor`
- A per-tab mode toggle (toolbar control + shortcut) that swaps between Reading (U3-1) and the source editor; mode persists per tab/note; the switch is announced; focus lands in the newly-shown view.
- **DoD focus:** toggle labeled with current + target state; keyboard shortcut registered in the command registry (palette-mirrored); each mode is one coherent AX tree (no leakage between them).
- **Acceptance:** switching modes is one keystroke, announced, and lands focus correctly; each tab remembers its mode.

### U3-3 · Mac UI: inline Properties widget at top of note (both modes) `swift-ui` `a11y`
- Move the editable properties experience out of the left sidebar to a pinned widget at the top of the note, shown in both Reading and Editing. Reuse `PropertyEditorRow`, add/rename, and the existing structured property-edit + conflict paths. Remove `PropertiesPanel` from the sidebar.
- **DoD focus:** widget is a labeled region above the content; each property row keeps its current editability + AX; conflict alert path preserved; light/dark; collapsible.
- **Acceptance:** properties are edited in-note with the same capability and conflict-safety as today, in both modes, and no longer occupy the left sidebar.

### U3-4 · Mac UI: "show source" YAML toggle within the properties widget `swift-ui` `a11y`
- A toggle inside the widget that reveals/edits the raw YAML frontmatter as text; edits round-trip through the same structured path so the widget and source never diverge; conflict-safe.
- **DoD focus:** toggle labeled; raw-YAML editor is accessible text; switching back reflects edits; malformed YAML surfaces a clear, non-destructive error.
- **Acceptance:** a user can drop to raw YAML and back without divergence or data loss; the structured widget reflects raw edits and vice-versa.

### U3-5 · Editor/Backend: body-only source buffer alignment with DocumentBuffer/save `editor` `backend` `test`
- Ensure the source editor's buffer (body-owned) and the properties widget (frontmatter-owned) compose into the correct on-disk bytes through the DocumentBuffer/save pipeline, preserving the Milestone-#404 incremental-structure performance.
- **Tests / census:** round-trip census — for arbitrary frontmatter + body, widget-edit ⊕ body-edit saves to byte-identical expected output; no per-keystroke regression vs. #404 baseline.
- **Acceptance:** saving a note edited via both surfaces yields exactly the right file bytes, with keystroke latency unchanged from the #404 baseline.
