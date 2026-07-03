# U3 executable spec — Reading/editing modes + inline properties

Issues: #465 (U3-1) · #466 (U3-2) · #467 (U3-3) · #468 (U3-4) · #469 (U3-5).
Milestone: GH 26. Depends on U1 (per-tab mode + per-tab documents). Parallel with U4.
One PR per issue. Program DoD applies.

**Execution order: U3-5 → U3-1 → U3-2 → U3-3 → U3-4.** The body-only buffer (U3-5) is
the data-model ground the other four stand on; landing it first (backend + editor
plumbing, behavior-visible change deferred behind the widget's arrival) avoids building
the widget on the whole-file buffer and immediately rebuilding it. U3-5's Rust half has
no UI dependency at all.

Baseline facts (gap_analysis.md G5/G6): editor buffer is whole-file today; DocumentBuffer
tracks `fm_end`/`body_structure`; property edits are per-key immediate whole-file saves;
no block-segmentation API; `EditorSpanKind` covers inline syntax spans; MathView/
CodeBlockView/MermaidView/EmbedView/task rows all exist and are reused, not rebuilt.

---

## U3-5 · Body-only source buffer + composed save (#469) — PR 1 (backend + editor plumbing)

### Rust (slate-core + uniffi)

New module functions in `frontmatter.rs` (pure, censusable):

```rust
pub struct NoteParts { pub fm_source: String, pub body: String }
// fm_source = the exact bytes BETWEEN the delimiters (no ---), "" when absent.
pub fn split_note(source: &str) -> NoteParts            // total: any input splits
pub fn compose_note(fm_source: &str, body: &str) -> String
```

**Byte-exactness rules (normative, encoded as unit fixtures):**
- `compose(split(s)) == s` for every s the scanner accepts as having well-formed
  frontmatter, AND for every s without frontmatter (`fm_source == ""` → compose returns
  body unchanged). This is the round-trip law; the existing `body_after_frontmatter`
  boundary logic is the single source of truth for the split point (reuse it — no second
  parser).
- `fm_source != ""` → compose emits `---\n{fm_source}\n---\n{body}` with these
  normalizations ONLY when the fm text itself changed relative to the loaded parts
  (pass-through saves are byte-identical): trailing newline inside `fm_source` collapsed
  to none (delimiter owns it), CRLF preserved as authored per-line (no conversion).
- Malformed YAML: `split_note` is syntactic (delimiter-based, matching the scanner);
  YAML *validity* is checked only where U3-4 writes fm (`set_frontmatter_source`,
  below) — reading never fails.
- BOM: stays attached to the start of `fm-block-or-body` exactly as
  `body_after_frontmatter` treats it today (fixture pins current behavior).

Session APIs (uniffi-mirrored):

```rust
pub fn read_note_parts(&self, path: &str) -> Result<NotePartsBundle, VaultError>
// { fm_source, body, content_hash, mtime_ms }  — one read, one hash: the tab-open call.
pub fn save_composed(&self, path: &str, fm_source: &str, body: &str,
                     expected_content_hash: Option<String>) -> Result<SaveReport, VaultError>
// compose_note → the existing save_text machinery verbatim (conflict detection, atomic
// write, index refresh, op-log). No second write path.
pub fn set_frontmatter_source(&self, path: &str, fm_source: &str,
                     expected_content_hash: Option<String>) -> Result<SaveReport, VaultError>
// Validates fm_source parses as a YAML mapping (or is empty) → MalformedFrontmatter
// with a line/column message otherwise (non-destructive: nothing written). Then
// read-current-body + compose + save_text. U3-4's commit path.
```

### Censuses (Rust, release-run)

- `census_split_compose_round_trip` — 100k random documents (random fm presence, CRLF
  mix, BOM, unclosed fences, `---` inside code blocks and body, unicode) →
  `compose(split(s)) == s` byte-exact. Exhaustive over the delimiter edge-case fixture
  set (empty fm, fm-only file, no trailing newline, `---` at EOF, frontmatter-like block
  mid-file).
- `census_widget_body_edit_interleave` — random sequences of {set_property, delete_property,
  set_frontmatter_source, save_composed(body-edit)} against a reference model (a plain
  in-memory string mutated by the same pure functions): on-disk bytes ==
  reference after every op; content-hash chain never conflicts when the sequence is
  serial (proves the hash handoff contract the Swift side relies on).
- Perf: `bench_doc_buffer_keystroke` unchanged (body-only text through the same
  DocumentBuffer — `fm_end == 0` path); assert no regression vs. the #404 baseline in
  the PR (run the bench, paste numbers).

### Swift plumbing (lands here, visible behavior unchanged until U3-3)

- `NoteDocument` gains `fmSource: String` (from `read_note_parts`) and its `text` becomes
  the **body**; `load()` switches `read_text` → `read_note_parts`; `save()` switches to
  `save_composed(fmSource, text, expectedHash)`. Baseline/dirty semantics unchanged
  (baseline is baseline-body now).
- Property edits (`setProperty`/`deleteProperty`/rename): on success AppState already
  refreshes hash; now ALSO refresh `fmSource` via `read_note_parts` (single read) so the
  next composed save can't resurrect stale fm. (The census above is the Rust-side proof;
  this is the Swift handoff.)
- **Transitional rendering note:** until U3-3 ships the widget, the editor showing
  body-only would *hide* frontmatter with no replacement UI. Unacceptable even
  transiently — so this PR keeps `NoteEditorView` bound to a computed
  `wholeTextForTransition` (compose of fmSource+body via a Swift mirror of the compose
  rule? **No** — two composers diverge). **Resolved:** this PR changes NO editor binding;
  it lands `NoteDocument.fmSource`+`bodyText` as *parallel* state populated by
  `read_note_parts`, keeps `text` = whole file (today's binding), and adds an assertion
  test that `fmSource ⊕ bodyText == text` on load. U3-3 flips the editor binding to
  `bodyText` in the same PR that mounts the widget. (The Rust APIs + censuses are the
  bulk of this PR; the Swift flip is deliberately deferred to the widget PR.)

## U3-1 · Reading view (#465) — PR 2

### Rust: `reading_blocks(path)`

```rust
pub enum ReadingBlockKind {
    Heading { level: u8 }, Paragraph, ListItem { depth: u8, ordered: bool,
        task: Option<TaskStateChar> }, BlockQuote { depth: u8 }, CodeFence { language: String },
    MathBlock, Diagram { dialect: String }, Table, ThematicBreak, Html,
}
pub struct ReadingBlock { pub kind: ReadingBlockKind, pub byte_start: u64, pub byte_end: u64,
    pub source: String /* the slice — saves a round trip */ }
pub fn reading_blocks(&self, path: &str) -> Result<Vec<ReadingBlock>, VaultError>
```

One pulldown-cmark walk over the **body** (frontmatter never renders — the widget owns
it), top-level block segmentation with list items and quote children flattened in
document order carrying `depth` (VoiceOver reads linearly; nesting is conveyed by the AX
value "list item, level 2", not by view nesting). Table kept as a raw block this
milestone: rendered via `AccessibleDataGrid` if trivially mappable, else as styled
monospace source — decided at implementation by whether `AccessibleDataGrid` accepts
string cells without new plumbing; either way the AX label announces "table". Fixtures
pin: every specialized block kind, nested lists/quotes, task items with every status
char, adjacent blocks with no blank line, HTML blocks (rendered as monospace source,
never interpreted).

### Swift: `ReadingView.swift`

- Structure: `ScrollView { VStack(alignment: .leading, spacing: Tokens.Spacing.md) }` —
  **eager** (ContentBlockPanels discipline: VoiceOver must enumerate; documented perf
  boundary: notes > 2,000 blocks log a perf note; virtualization is the recorded
  follow-up, measured in U5-4).
- Block renderers:
  - Heading → `Text` with `Tokens.Typography` scale by level + `.accessibilityAddTraits(.isHeader)`
    + `.accessibilityHeading(.h(level))` — the rotor walks in document order because the
    VStack is document-ordered.
  - Paragraph/ListItem/Quote → inline pipeline below.
  - CodeFence → `CodeBlockView` (existing, incl. preamble speech + highlight).
  - MathBlock → `MathView` (existing MathCAT speech).
  - Diagram → `MermaidView` (existing description contract).
  - Task ListItem → checkbox row reusing TasksPanel's toggle semantics
    (`document.toggleTask(ordinal:)` routed to `toggle_task_status`; disabled while the
    body buffer is dirty — same rule as the panel, same explanation in the `help`).
  - Embed (`![[…]]` paragraph-level) → `EmbedView` with expand/collapse (existing),
    images carry alt text (existing EmbedView behavior; alt = link display text
    fallback to target name — the U3-1 fixture asserts non-empty AX labels for image
    embeds).
  - ThematicBreak → `Divider()` + hidden from AX (decorative).
- **Inline pipeline** (paragraph-family blocks): pre-process the source slice —
  wikilinks/embeds/tags/citations replaced by markdown links with custom schemes
  (`slate-wiki://<target>`, `slate-tag://…`, `slate-cite://<key>`) — then
  `AttributedString(markdown:, options: .init(interpretedSyntax:
  .inlineOnlyPreservingWhitespace))`, then map scheme runs to styled+labeled runs
  (accent `Tokens.ColorRole.accentText`, underline; AX label "link, <display>",
  citations get their **speech text** as the AX label — the Milestone L contract).
  Activation: `.environment(\.openURL, OpenURLAction { … })` routes wiki links →
  `appState.openFile(target:, from: readingView)` (U1-5 targets incl. ⌘-click new tab),
  external links → existing external-open path, citations → `expandedCitation` popover,
  tags → search overlay pre-filtered (existing search scope). Every link is a real
  hyperlink run: VO announces "link", Tab cycles links… (macOS `Text` links are
  VO-activatable; keyboard activation parity comes from the links ALSO being listed in
  the outgoing-links leaf — recorded explicitly in the PR as the keyboard path, per DoD
  "keyboard equivalent exists").
- **Continuous read:** every leaf is a distinct `Text`/control in one flat VStack —
  VO reads top-to-bottom across blocks (the SwiftUI textSelection-at-leaf learning:
  `.textSelection(.enabled)` applied per leaf `Text`, never on containers).
- Mode plumbing (used by U3-2): `NoteContentView` renders `ReadingView(document:)` vs
  editor per the tab's mode.
- States: loading (spinner row), error (specific message + Retry), empty body ("This
  note is empty." + "Switch to Editing" button), populated.

Tests: `reading_blocks` fixtures (Rust); Swift — rotor order = document order (heading
AX traits in sequence), link/task/embed activation routing (unit: OpenURLAction handler
table), inline mapper (wikilink+alias, tag, citation → expected runs + labels),
continuous-read scoping (leaf-level selection modifiers — assert via view inspection
helper), both-appearance renders + APCA on the reading text styles (PresentationReady),
a11y-check 100.

## U3-2 · Mode toggle (#466) — PR 3

- `NoteViewMode { reading, editing }` lives per tab: `WorkspaceState.viewMode[TabID]`
  (default `.editing` — today's behavior; a "default mode" preference is a recorded
  follow-up for Milestone R/settings). Persisted in `workspace.json` v1 schema (U1-6
  gains `"mode": "reading"` per tab — schema addition is backward-compatible: absent =
  editing).
- Toolbar control: a two-state button at the leading edge of the tab-group toolbar
  region showing the *target* mode glyph (`SlateSymbol.readingMode`/`.editingMode`),
  label "Reading mode"/"Editing mode", `accessibilityValue` = current mode ("Currently
  editing"), `help` tooltip with the shortcut. NOT a segmented picker — Obsidian
  parity is a toggle, and the two-state button reads better in a dense toolbar; the
  value string carries current state for VO.
- Shortcut **⌘⇧E** + command `slate.editor.toggleViewMode` (registry + palette; drift
  test).
- Switch mechanics: mode flips → `NoteContentView` swaps subview. **Focus:** lands in
  the newly shown surface — editing → `NSTextView` becomes first responder (existing
  makeNSView caret discipline: caret preserved from last editing session of this tab,
  else {0,0}); reading → AX focus to the first block (`@AccessibilityFocusState` on the
  reading root). **Announcement:** "Reading mode." / "Editing mode." (.medium).
  Each mode is one AX tree: the hidden mode is **unmounted** (`if mode == …` — NOT the
  ZStack retention pattern: an offscreen full editor duplicates the whole text tree for
  VO and double-fires publishers; reading-view scroll position is cheap to lose, editor
  caret is preserved in `NoteDocument.lastSelection` on switch-away).
- Dirty interaction: mode switch never prompts (same document, same buffer); reading
  view renders from `bodyText` (the live buffer — unsaved edits visible in reading
  mode; blocks recomputed via a **local** parse: `reading_blocks` operates on a path…
  **Resolved:** add `reading_blocks_source(source: String)` uniffi variant (pure, no IO)
  so reading mode renders the live buffer, not stale disk bytes. Cost: one parse per
  toggle — acceptable; no per-keystroke recompute because the editor is unmounted in
  reading mode.)

Tests: mode persistence per tab (switch tab A→B→A keeps modes), focus landing (first
responder / AX focus asserts), announcement strings, live-buffer rendering (edit →
toggle → reading shows the edit), unmount discipline (editing tree absent from AX while
reading — view-inspection assert), a11y-check, appearance snapshots.

## U3-3 · Inline properties widget (#467) — PR 4

- `NotePropertiesHeader.swift`: pinned (non-scrolling) region at the top of the tab
  content, shown in **both** modes above ReadingView/editor. Structure:
  `DisclosureGroup` ("Properties", default expanded, expansion state per tab in
  `WorkspaceState.propertiesExpanded[TabID]`, persisted like mode) containing the
  existing `PropertyEditorRow` list + `AddPropertySheet` trigger — the rows and sheet
  move **unchanged** (same bindings, same conflict alerts routed via MainSplitView, same
  draft discipline). Region: `.accessibilityElement(children: .contain)`, label
  "Properties, N properties". Header shows count + Add button (`SlateSymbol.addProperty`)
  + show-source toggle placeholder (U3-4).
- **This PR flips the editor to body-only** (the U3-5 deferred flip):
  `NoteEditorView` binds `document.bodyText`; `DocumentBuffer` receives body text
  (fm_end=0); the `fmSource ⊕ bodyText == whole` load assertion is retired along with
  `NoteDocument.text`-as-whole-file. Saving = `save_composed`. The editor's
  accessibility label stays "Editor for <name>" — VO users hear properties as a labeled
  region *before* the editor in the reading order, which is the plan's contract.
- Remove `PropertiesPanel` from `FileTreeSidebar`'s panel stack (only Properties — G7);
  delete `PropertiesPanel.swift` outer shell, keep `PropertyValueDisplay` (reused by
  rows). Sidebar tests updated to assert the stack no longer contains it.
- Scroll behavior: the header is OUTSIDE the editor scroll view (pinned; collapse to
  reclaim space). At accessibility text sizes the header itself scrolls (maxHeight 40%
  of pane with internal scroll) — no clipped rows (DoD Dynamic Type).
- Conflict paths: unchanged by construction (same `setProperty` flow). New interaction —
  property edit while body dirty: allowed (fm save doesn't touch body; hash handoff per
  U3-5 keeps the chain); the reverse (body save while a property row has an uncommitted
  draft) leaves the draft alone (drafts are row-local until commit — today's semantics).

Tests: widget renders in both modes (mount matrix test), region label + reading order
(properties precede content in AX order — snapshot of accessibility hierarchy), rows
function identically (re-point the existing PropertyEditorRow tests at the new host),
sidebar no longer hosts properties, editor-is-body assertions (type in editor → save →
on-disk bytes composed correctly — integration test through a temp vault), both-mode
appearance snapshots, a11y-check 100.

## U3-4 · Show-source YAML toggle (#468) — PR 5

- Toggle button in the widget header (`SlateSymbol.showSource`, label "Show source",
  value "Showing fields"/"Showing source", shortcut **⌘⇧D**, command
  `slate.editor.togglePropertiesSource`).
- Source mode: the rows are replaced by a plain `TextEditor` (monospace
  `Tokens.Typography.code`, labeled "Properties source, YAML") bound to a local draft of
  `fmSource`; Commit on **explicit** action — "Apply" button (⌘Return) — never on blur
  (blur-commit loses work to mis-clicks; Esc/"Cancel" reverts the draft, announced).
  Apply calls `set_frontmatter_source(path, draft, expectedHash)`:
  - success → refresh `fmSource` + hash + properties list (one `read_note_parts` +
    `properties_for_file`), switch back to fields view, announce "Properties updated."
  - `MalformedFrontmatter` → inline error under the editor with the line/column message,
    draft preserved, focus stays in the editor (non-destructive, specific — DoD §F).
  - `WriteConflict` → the existing property-conflict alert flow (same three buttons),
    draft preserved on Cancel.
- Fields ⇄ source with an uncommitted source draft: switching to fields with a dirty
  draft prompts (Apply / Discard / Cancel — small alert, focus-returned); switching to
  source is always safe (fields commit per-row already).
- Round-trip guarantee: fields view after apply reflects exactly the YAML (it re-reads
  from disk state — single source of truth; no client-side YAML parsing anywhere in
  Swift).

Tests: apply/refresh cycle (integration, temp vault), malformed YAML error surfaced +
nothing written (disk bytes unchanged — assert), conflict path, draft-guard prompt
matrix, announcement strings, AX labels/values, a11y-check, appearance snapshots.

---

## SlateSymbol additions

| Role | v7 | fallback | PR |
|---|---|---|---|
| `.showSource` | `curlybraces` | `curlybraces` | U3-4 |
| `.properties` | `list.bullet.rectangle` | `list.bullet.rectangle` | U3-3 |

(`.readingMode`/`.editingMode` exist from U0.)

## Follow-ups filed during U3

- Default-view-mode preference (Settings) — file with U3-2.
- Reading-view virtualization for >2k-block notes if U5-4 measurement warrants — file
  with U3-1, decided by U5-4 data.
- Reading-view in-document find (⌘F currently opens vault search; in-note find parity) —
  file with U3-1 as `enhancement`.
