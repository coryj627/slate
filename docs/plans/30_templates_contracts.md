# W5-3 — Templates: picker + prompt flow (#743): contracts

Scope (spec §W5-3): the template picker, the variable prompt flow, and
create-from-template over core `render_template` /
`extract_template_metadata` / `list_templates`; mac prompt/cursor
behavior parity. Written BEFORE implementation per
`24_red_team_protocol.md` §0. The divergence (TD) and accepted-risk (TR)
registers are deliberate owner-recorded decisions and are **off-limits
for review re-litigation** — a reviewer may report that an entry is
factually wrong, not that the trade-off should be re-made.

Contract numbering is per-wave. "T3" here is unrelated to any other
document's contract 3.

## The findings that shape this issue

**1. There is no "insert template into the editor" anywhere in Slate.**
The issue's delivery spec says "the editor inserts the rendered text"
and names the commands "insert-template at minimum". Measured against
mac (`TemplatePicker.swift`, `TemplatePromptSheet.swift`,
`AppState.swift:22867-23624`) and the parity matrix: the one command row
this issue owns is **`slate.file.newFromTemplate` — "New Note from
Template…" ⇧⌘N** (parity_matrix.md:134), and the flow is
**create-from-template**: picker → prompts (if any) → name sheet →
`render_template` + `create_exclusive` of a **new** note → open it →
park the caret. Rendered text is never inserted into an existing
buffer on either platform. The "buffer discipline" concern the issue
raises translates here to: the only write is an **exclusive create** of
a path no tab can already hold, and the subsequent open is an ordinary
fresh-from-disk tab load (T8's stale-tab rule covers the parked-tab
corner).

**2. Every mac template announcement is `HostComposed` residue.** mac's
`announceTemplate` (`AppState.swift:23615-23624`) posts host-composed
text with a `// W0.5-3 residue:` marker; core's a11y vocabulary has no
template family at all. The issue's "Announcements canonical" therefore
means **this issue adds core's first template events** (T10) — with the
4-place edit that implies (a11y.rs, corpus.json regen, the uniffi
mirror, the mac ordered census list) — and records the availability/busy
copy as residue exactly the way `a11y.rs`'s own header carves out
double-duty availability copy. mac's call sites are NOT converted here
(mac keeps its marker; its conversion is a mac follow-up in the #1113
mold).

**3. No new FFI, and the Windows model is synchronous.**
`render_template`, `list_templates`, `extract_template_metadata` are
bound, `public`, and CLI-exercised in `windows.yml` since W0-3. On
Windows every established neighbor of this flow — sidebar
`CreateExclusive` (`FilesSidebarViewModel.cs:1040`), tab loads
(`VaultLifecycleViewModel.cs:1331-1338` "the Windows tab load is
synchronous"), history Restore As — runs its FFI synchronously on the
UI thread; only W5-2's `FullTextSearch` hops threads, because FTS is
slow. Template enumeration and rendering are small bounded reads. The
whole flow is therefore **synchronous on the UI thread**, which
deletes the mac async-race machinery (availability generations,
selection supersession, deferred cursor landing, destination
owner-generation guards) **by construction, not by omission** — a
reviewer hunting for the Windows twin of those guards should find this
paragraph. What survives of that machinery is exactly two things: the
flow-reset-on-vault-transition sweep (T13) and the stale-tab fresh-read
rule (T8).

## Contracts

**T1 — Core owns discovery, metadata extraction, and rendering; the
host re-implements none.**

- `ListTemplates(cancel)` returns rows **already sorted**
  (case-insensitive by name, then ordinal — `session.rs:8155-8161`);
  the host never re-sorts. `path` is vault-relative with forward
  slashes; `name` is the file stem; `description` is core's pick
  (frontmatter `description:` else first non-blank body line, ≤120
  chars) — the host displays it verbatim and composes nothing.
- A missing/vanished templates directory is **`Ok([])`, not an error**
  (`session.rs:8084-8095`); unreadable, oversized, non-UTF-8, and
  symlink-escaping entries are silently dropped per-entry. A real error
  (permission denied on the directory itself, IO) propagates and is the
  picker's `failed` state.
- `ExtractTemplateMetadata(source)` yields prompts **in declaration
  order**, deduped by label, keys slugified with `_2`/`_3` collision
  suffixes (`templates.rs:144-164`). The host preserves order and never
  re-derives keys.
- `RenderTemplate(templatePath, context)` **never fails on content**:
  unknown variables, bad chrono formats, and malformed markers survive
  verbatim (`templates.rs:180-230`). Every failure it CAN return is a
  **read failure** — `InvalidPath` (traversal/symlink escape),
  `Io` (deleted between list and render), `FileTooLarge`,
  `InvalidUtf8` — and T7 relays core's message for them.

**T2 — The flow is create-from-template, three steps, strictly
forward.** Picker → prompt step (skipped entirely when
`ExtractTemplateMetadata` returns zero prompts — the no-variable fast
path, pinned) → name step → create. There is no backward navigation
(mac has none). Exactly one flow may exist at a time (T9's modal
admission enforces this; there is no mac-style structural-mutation gate
on Windows until W5-4, and this issue does not build one).

**T3 — The picker.** An in-window sheet (never a `Popup` — W4-5 D-1),
Fluent-styled on Slate tokens, presented from
`slate.file.newFromTemplate` (chord, File menu, palette).

- Content: header "Choose a template" (heading semantics) + destination
  subtitle; the template list (each row: name, description when
  present); footer Cancel. Row accessible name is
  `"{name}. {description}."` when a description exists, else the name
  (mac `rowAccessibilityLabel` verbatim).
- States: `loading` (progress + "Loading templates…"), `empty`
  ("No templates found." + create-a-file guidance + Try Again),
  `failed` ("Couldn't load templates" + check-folder guidance + Try
  Again), `available` (the list). mac strings verbatim; the subtitle's
  chord text renders the **Windows** chord (program decision 12).
- Focus on present: first row when available; Try Again when
  empty/failed; Cancel while loading. Esc cancels from every state;
  Enter activates the focused row; arrows/Tab traverse.
- The list is enumerated **fresh on every open** (mac re-fetches per
  open). Try Again re-enumerates without dismissing (Esc stays live).
- Activating a row reads the template source (`ReadText`), extracts
  metadata, closes the picker, and presents the flow sheet at the
  prompt step (or name step per T2). A read failure at this point
  (template deleted between list and select) **cancels the whole flow**
  (mac `performSelectTemplate`'s terminal reset — not an error sheet).

**T4 — The prompt step.** One labelled text box per prompt, in
declaration order, labels verbatim from `TemplatePrompt.label` (never
the slug key). Every value is **seeded as the empty string** — mac's
defaults — so an untouched field substitutes empty, and **no prompt
marker whose label was asked ever survives literally** (the literal
fallback in core exists for UNASKED keys, which T5 covers). First
field focused on present. Footer: Cancel / Next; Next is the default
button (Enter submits from inside a text field); no validation on
prompt values — any string including empty is a valid answer.

**T5 — Metadata/render coherence.** Metadata is extracted from a
host-read snapshot of the source; the render re-reads by path at create
time. A template edited in the gap can therefore carry prompts the user
was never asked (their markers survive literally — core's benign
fallback) or lose prompts the user answered (the stale values are
ignored). This is byte-for-byte mac's exposure
(`performSelectTemplate` reads; `renderTemplate` re-reads) and is
recorded as TR-2 rather than "fixed" with a second prompting pass;
`render_template_with_metadata` exists for single-read CLI strictness,
not for an interactive flow that must prompt between the two reads.

**T6 — The name step.** Text box seeded with the default name:
`"{template.name}.md"`, except templates whose name starts with the
standalone **word** `daily` (case-insensitive; `Daily`, `Daily
Standup`, `Daily-notes` qualify; `Dailyness` does not) get
`"{template.name} {yyyy-MM-dd}.md"` with the date in **UTC** — mac's
`defaultNewNoteName`/`isDailyTemplateName` verbatim, including the UTC
choice (it matches `{{date}}`'s UTC rendering; recorded, not
re-litigated). On a create failure the field is re-seeded with the
user's exact prior entry (T7).

- Pre-validation (inline error, no render attempted): empty →
  "Note name cannot be empty."; `.`/`..` → "Note name cannot be `.` or
  `..`."; leading `/` → "Note name must be vault-relative, not
  absolute."; any `..` segment → "Note name cannot contain `..`
  segments." — mac strings verbatim, plus the Windows-only
  platform-absolute arm in TD-4.
- `.md` is appended unless the path extension is already `md`
  case-insensitively (`archive.tar.MD` is left alone; `Note.MD` keeps
  its casing — mac's `pathExtension` rule).
- The creation path is `destination.isEmpty ? name :
  "{destination}/{name}"`, forward slashes.
- The inline error text is focusable, named "Validation error:
  {error}" for AT (mac's shape); a validation failure is **not** an
  announcement on either platform.

**T7 — Create.** On Create (button or Enter):

- `TemplateContext`: `nowMs` = the creation instant (UTC millis);
  `title` = the **new note's file stem** (never the template's name);
  `vaultName` = the vault root's basename; `promptValues` from T4.
- `RenderTemplate` then `CreateExclusive(path, body)` — the no-clobber
  discipline; an occupied destination is a typed conflict and nothing
  is overwritten. Both run synchronously (finding 3).
- Success: sidebar refresh, the canonical created announcement (T10,
  High priority — mac F-H1: it must outlive the tab-switch
  announcement that follows), the flow sheet closes, the new note
  opens in the **current tab**, caret per T8.
- Failure: the name step is **re-presented** with the user's exact
  prior name and a focusable inline error relaying **core's message
  verbatim** (the recorded refusal presentation; no host-composed
  retelling). Cancel from the re-presented sheet remains a full T2
  cancel.
- **Cancellation writes nothing.** Cancel/Esc at the picker, the
  prompt step, or the name step leaves the vault byte-identical and
  the workspace untouched — the only write in the whole flow is the
  explicit Create. Pinned by a fact that walks all three cancels.

**T8 — Cursor placement.** `RenderedTemplate.cursorByteOffset` is a
UTF-8 byte offset into the whole rendered file, guaranteed to fall on a
char boundary (uniffi doc), first marker wins, all markers stripped.

- Windows buffers are **whole-file** (the W4-4 divergence — frontmatter
  is hand-editable in the buffer), so the offset needs **no body
  rebase**: convert UTF-8 byte offset → UTF-16 code-unit index over the
  rendered body and park with
  `tab.EditorInteractions.RequestCaret(charIndex)`. mac's
  `bodyByte(fromFileByte:)` conversion exists because its buffer is
  body-only; porting it would be wrong here. (U3-5's law — frontmatter
  geometry is never re-derived host-side — is not implicated: no
  frontmatter arithmetic happens at all.)
- **No marker → caret at the end of the document** (explicit
  `RequestCaret(text length)`). This is mac's observable resting state
  (#421: an undelivered park leaves the caret at end-of-text) and the
  issue's "else at the insertion end", made deliberate.
- Focus follows content: after the park the editor holds keyboard
  focus; the sheet-close focus restore **stands down** when focus was
  claimed outside the sheet (the SD-2 / `FocusClaimedOutside…`
  pattern, applied to the flow sheet's restore).
- The park applies only when the open actually landed: if the
  dirty-navigation prompt refuses the open, the note exists and the
  created announcement fired, but no caret is parked and no deferred
  landing is retained (TD-3 — mac defers across the prompt; the
  Windows sync model drops it, the S9 posture).
- If a **parked tab already exists at the created path** — via
  workspace persistence of a previously deleted file's tab, OR the
  mid-session `InvalidatePath` sweep after an external delete (the
  red team's second provenance; the original text claimed persistence
  was the only route) — the open must land the **fresh disk read**,
  never a restored stale buffer: a CLEAN landed tab whose text is not
  the rendered body reloads from disk before the park; a DIRTY tab is
  never reloaded (the user's unsaved buffer outranks the render) and
  the caret guard stands down for it. This is mac's "authoritative
  template target load" rule surviving the sync translation, and it
  needs explicit code — the same-group open arm activates an existing
  tab with no read, and the cross-group arm mirrors the peer's buffer
  over its fresh read.

**T9 — Modal admission (the #1118 rule, done right).** Two new
`ModalSurface` members, declared LAST in paint order:
`TemplatePicker`, then `TemplateFlow` (the prompt/name sheet).
`OwnsTextField`: false for the picker (it hosts no text field — mac's
has none either), true for the flow sheet.

- Every opener — the chord, the File-menu item, the palette row —
  reaches ONE admission: it runs INSIDE the workspace's open, and it
  consults `DecideTemplateOpen(topmost)`: `null` → Open; `QuickOpen` →
  DismissQuickOpenThenOpen; `SearchOverlay` → DismissSearchThenOpen
  (SD-5 supersession, focus lineage adopted exactly as the palette's
  and search's arms do); `CommandPalette` →
  **DismissPaletteThenOpen** — mac's rule at its registry dispatch
  (retire the action launcher, then stage the sheet;
  AppState.swift:1980-1989) — consuming the palette's focus lineage;
  `TemplatePicker`/`TemplateFlow` → **Refuse** (no re-entrant flow;
  mac's `templateFlowBusyReason` moment); any sheet → **Refuse**.
  Unlike #1118's Ctrl+Shift+R/J, the chord NEVER presents beneath a
  higher surface. *(Amended after the red team: the original text
  said palette → Refuse with an UNGUARDED registered command in
  toggleSearch's mold. That pairing cannot serve the palette row —
  P9 invokes before dismissing, so an unguarded command would present
  beneath the open palette, and a Refuse arm would kill the row as an
  opener. The shipped design guards the command once and retires the
  palette, which is also what mac does.)*
- A palette-invoked `newFromTemplate` therefore rides P9's transient
  INTO the same admission, whose palette arm performs the retire; the
  chord pressed while the palette is open takes a PD-2-style
  carve-out in the palette's key branch to reach that same arm (the
  blanket swallow would otherwise eat it silently). The reactive
  `Workspace_SheetPresented` observer (round-11 machinery) remains
  the backstop that retires the palette/Quick Open/search when the
  picker presents, with search **Superseded**, not Closed.
- The picker→flow transition happens while the flow owns the screen
  (picker closes in the same dispatcher turn the flow sheet opens);
  the observer arms cover both new properties.
- Focus: `CapturePreSheetFocus` on first present of the flow's FIRST
  surface; the restore runs once, at flow end (cancel or
  create-failure-abandon), through the shared topmost-search backstop
  (`TryFocusSearchIfTopmost` first), and stands down per T8 after a
  successful create.

**T10 — Announcements: the canonical/residue partition.** Two NEW core
events — the first template family (finding 2):

- `TemplatePickerOpened { count }` → mac's
  `templatePickerOpenAnnouncement` verbatim, all three arms: 0 →
  "Template picker opened. No templates found. Add a Markdown file to
  the configured template folder."; 1 → "Template picker opened. 1
  template available."; n → "Template picker opened. {n} templates
  available." Medium. Fired when the picker presents with rows
  (count ≥ 1). (The 0 arm is carried for template completeness; mac's
  empty present speaks the availability reason instead — see residue.)
- `TemplateNoteCreated { name, template }` → "Created {name} from
  {template}." **High** (mac #421 F-H1: must win over the follow-on
  tab-switch announcement). Fired on create success, before the open.

Adding them is the 4-place edit: `a11y.rs` (enum + priority arm +
render arm + `corpus()` + ordered goldens), `corpus.json` regen
(`SLATE_REGENERATE_FIXTURES=1`, then a clean re-run proving the pin),
the `slate-uniffi` mirror enum + `From` arms, and the mac
`A11yCorpusCensusTests` ordered list — the two uniffi censuses exist to
catch the last two.

Residue (Windows `HostComposed`, each site marked
`// W0.5-3 residue:`, counted by whatever census pins the suite):
availability and busy copy, mac strings verbatim — the empty-present
announcement ("Add a Markdown file to this vault's configured template
folder to create from a template."), the failed-present announcement
("Slate couldn't load templates. Check the configured template folder
and try again."), and the refusal announcements, which keep mac's
TWO-string partition (AppState.swift:7901-7904): a refusal whose
topmost surface IS the template flow speaks `templateFlowBusyReason`
("Finish or cancel the current template note before starting
another."), any other sheet speaks `templateDialogBusyReason`
("Finish or cancel the current dialog before creating from a
template."). This is `a11y.rs`'s own carve-out (availability copy
serving double duty as dialog/hint text), and it is the same class the
owner already approved for the W5-1 bridge. mac's window-admission
copy (`templateOtherWindowReason`) has no Windows referent —
single-window app — and is not ported.

Create-failure and name-validation errors are **inline focusable text,
not announcements**, on both platforms (T6/T7).

**T11 — Commands, chord, menu.** ChordTable row:
`Reg(Ids.NewFromTemplate = "slate.file.newFromTemplate", "New Note from
Template…", CommandSection.Sidebar, "Choose a template for a new
note.", "⇧⌘N", "Ctrl+Shift+N")` — a clean ⌘→Ctrl mapping, no
divergence note (Ctrl+Shift+N is unclaimed in the table and in
`chords.json`; the W5-1 collision scan holds). Scope Global. The
registrar maps the id to the workspace-owned command; PINV-7's
enumeration picks it up with no list edits. File menu gets the item
with `InputGestureText` per the W4 convention, after Quick Open —
the Windows File menu has no New Note/New Folder items to sit after
(those are sidebar actions; the original wording borrowed mac's menu
geography). The command binding resolves against `Workspace.*`, so
with no vault open the item is inert the way every workspace-bound
menu item is — availability (empty/failed) is the picker's job
(TD-2), and admission is the invoke-time `DecideTemplateOpen` (T9),
not `CanExecute` (a stale `CanExecute` under WPF's requery timing
would reintroduce #1118's class as a race).

**T12 — Destination.** The creation destination is frozen when the
picker opens, from the sidebar's creation-parent rule — the selected
node when it is a directory, else its parent, else the vault root
(`FilesSidebarViewModel.CreateNote`'s exact rule, which is mac's
`canonicalSidebarCreationParent` semantics) — and every sheet's
destination subtitle renders it ("the vault root" for empty). It does
not track later selection changes (mac freezes it in
`TemplateCreationDestinationOwner`). The name field may still contain
`/` segments to create deeper (relative to the destination), mirroring
mac's "Name relative to {destination}" copy.

**T13 — Lifecycle.** Vault close/switch tears the whole flow down:
sheets close, state resets, nothing is written. Because every step runs
synchronously on the UI thread (finding 3), there is no mid-create or
mid-enumeration interleave with a vault transition — the teardown is
**workspace disposal itself**: the sheet view models are properties of
the per-vault `WorkspaceViewModel`, so they die with it, and the
window's unwire clears its observers and the captured focus token.
*(Amended after the red team: the original text named the W5-2
`ResetForVaultTransition` slot, which is the search overlay's — the
overlay outlives vaults; the template sheets do not, so their teardown
mechanism is the workspace swap.)* The picker holds no session-derived
state beyond the summaries list and the frozen destination string,
both discarded with the workspace.

## Divergence register (owner-recorded; off-limits for re-litigation)

- **TD-1 — No passive availability refresh; the command stays enabled.**
  mac debounces file events/foreground activation into background
  re-enumeration to keep `newFromTemplate`'s enabled-state and
  per-command palette reasons current. Windows enumerates fresh on
  every open and keeps the command enabled whenever a vault is open;
  empty/failed are presented INSIDE the picker with Try Again.
  Rationale: the per-command reason model this would feed is exactly
  what W5-1 recorded as deferred (§2.6 pending owner decision), and a
  fresh-on-open list can never be stale in the way a cached
  enabled-state can. The user-visible cost is a picker that opens and
  immediately says "No templates found." where mac would have disabled
  the command with a hint — the picker route is keyboard-complete and
  self-explaining.
- **TD-2 — Empty/failed availability lives in the picker, not on the
  command** (the presentation half of TD-1; recorded separately so a
  reviewer comparing against mac's `templateAvailabilityDisabledReason`
  finds the answer).
- **TD-3 — A dirty-navigation refusal drops the caret park.** mac
  retains a `DeferredTemplateCursorLanding` across the Save/Discard
  prompt and delivers it after the eventual load. Windows parks only
  when the open lands now; a refused open keeps the current note, and
  re-opening the created note later is a new navigation with no park —
  the S9 search-activation posture, and the honest observable under
  synchronous loads.
- **TD-4 — Windows adds platform-absolute name validation.** mac's
  pre-validation rejects only `/`-prefixed absolutes; on Windows
  `C:\…`, `\…`, and UNC forms are also refused with the same
  "vault-relative, not absolute" message. Core would refuse them
  anyway (`InvalidPath` at create); catching them inline is the same
  courtesy mac extends to `/`.
- **TD-5 — Two sheets, one surface each; mac's three.** mac presents
  picker, prompt sheet, and name sheet as three sequential sheets.
  Windows presents the picker sheet and ONE flow sheet hosting the
  prompt step and the name step in sequence. Step order, skip rule,
  seeded values, focus-per-step, and cancel semantics are identical;
  only the presentation mechanics differ (the W4-5 in-window sheet
  idiom favors fewer surfaces; the modal registry gets two members
  instead of three).
- **TD-6 — Windows binds Ctrl+Shift+N.** mac's ⇧⌘N maps cleanly; no
  Windows convention claims the chord in-app. Recorded because
  `slate.file.newNote` is deliberately chord-UNBOUND on Windows — the
  asymmetry (template-create chorded, plain create not) is mac's own
  asymmetry (⌘N is mac-chorded but Windows declined it for the
  inline-rename flow; the template flow has no such conflict).

## Accepted-risk register (owner-recorded; off-limits for re-litigation)

- **TR-1 — Synchronous FFI on the UI thread.** Enumeration does one
  bounded read per template (cap: `large_file_refuse_bytes` each);
  render+create is one read + one exclusive write. A pathological
  templates directory (hundreds of large files) stalls the UI for the
  duration; core's own doc records the same scale assumption ("if
  vaults ever ship hundreds of templates we'd want a description
  column in the index"). Matches every shipped Windows structural
  operation; not worth the thread-hop machinery until core grows an
  index.
- **TR-2 — The metadata/render TOCTOU** (T5): a template edited
  between select and create renders with core's benign literal
  fallback for unasked prompts. mac-identical exposure; the failure
  mode is cosmetic (literal `{{prompt:…}}` text in the new note),
  never a wrong write.
- **TR-3 — No structural-mutation serialization.** A template create
  can race a sidebar rename/import lease-wise; `CreateExclusive` is
  atomic and no-clobber, so the worst case is a typed conflict
  presented per T7. W5-4 owns the cross-surface mutation gate; this
  issue does not pre-build it.
- **TR-4 — `{{date}}`/`{{time}}`/daily-default render in UTC**, not
  local time (core treats `now_ms` as UTC; mac pins UTC in
  `defaultNewNoteName`). A user in UTC-8 creating a daily note at
  17:00 gets tomorrow's date. Cross-platform-identical and pinned by
  core tests; changing it is a product decision for another issue.

## Mac details recorded while reading (not this issue's to fix)

- `templatePickerOpenAnnouncement`'s 0-count arm is unreachable on mac
  (`AppState.swift:23070-23083` announces the availability reason for
  the empty present instead); the Windows event carries the arm anyway.
- mac's picker subtitle hardcodes "Command-Shift-N." in UI copy rather
  than rendering the chord per platform (program decision 12 concerns
  announcements, so this is a wording note, not a violation). The
  Windows subtitle reads its chord FROM the chord table, and platform
  KEY NAMES localize in ported UI copy the way the chord did: mac's
  "Return." in the Next/Create hints renders as "Enter." on Windows.
- mac's `announceTemplate` residue marker (`AppState.swift:23620`)
  makes the whole template family host-composed; conversion to the T10
  events is a mac follow-up candidate (file in the #1113 mold when the
  events exist).
