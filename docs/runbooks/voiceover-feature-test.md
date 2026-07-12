# Runbook: VoiceOver feature-completion test

End-to-end verification that a VoiceOver user can complete every shipped feature's documented task against a real vault. **The keyboard drives the app; VoiceOver observes** — a step passes only when VO's actual speech (and, for mutations, the file on disk) says it did. This is how a blind user experiences the app, and it catches what AX-tree inspection cannot: wrong utterances, silent actions, focus limbo, live-region races.

First executed 2026-06-10 against `main @ 798d536` (verdicts below). Driver: [`scripts/vo.sh`](../../scripts/vo.sh).

## 1. Preconditions

Run all four before any feature step. The first run of this suite lost an hour to these.

**1. Build and launch the `.app` bundle — never the bare binary.**

```sh
./scripts/build-mac-app.sh --bundle
open -n apps/slate-mac/.build/debug/SlateMac.app
```

The bare SwiftPM binary is invisible to VoiceOver — only a LaunchServices-registered bundle attaches to the AX system (documented in `build-mac-app.sh`).

**2. Accessibility permission for your terminal/driver process.**

```sh
osascript -e 'tell application "System Events" to get name of first application process whose frontmost is true'
```

If this errors, grant the calling app in System Settings → Privacy & Security → Accessibility. Without it, every synthetic keystroke fails.

**3. Enable "Allow VoiceOver to be controlled with AppleScript."**

The pref alone is **not** sufficient on current macOS — `defaults write com.apple.VoiceOver4/default SCREnableAppleScript -bool true` round-trips but VO still rejects commands with error `-1708` ("doesn't understand the message"), and the old root sentinel was migrated away (`/private/var/db/Accessibility/.VoiceOverAppleScriptEnabled-Migrated`). What works is the **VoiceOver Utility → General checkbox**. Headless path (no admin auth required):

```applescript
tell application "System Events"
  tell process "VoiceOver Utility"  -- open -a "VoiceOver Utility" first
    set cb to checkbox 2 of splitter group 1 of window 1
    -- checkbox 2 = "Allow VoiceOver to be controlled with AppleScript"
    perform action "AXPress" of cb   -- `click` does NOT toggle it; AXPress does
  end tell
end tell
```

Then verify: `scripts/vo.sh ping` → must print nothing and exit 0 (any `-1708` means the checkbox didn't take; restart VO and re-check).

**4. Start VO and back up the vault.**

```sh
scripts/vo.sh start-vo
VAULT="${VAULT:-$HOME/Library/Mobile Documents/com~apple~CloudDocs/demo-vault}"
cp -R "$VAULT" "/tmp/demo-vault-backup-$(date +%Y%m%d-%H%M%S)"
```

The demo vault is the canonical fixture — its own `README.md` is a manifest of what each note exercises, **including three deliberately broken things** (a broken wikilink, an unresolved cite key, malformed LaTeX). Read it first; build probes from it.

## 2. Driver vocabulary (`scripts/vo.sh`)

| Command | What it does |
|---|---|
| `start-vo` / `stop-vo` | Start/stop VoiceOver (open -a, Cmd+F5 fallback / quit, pkill fallback) |
| `ping` | Prove AppleScript control is live (`-1708` = checkbox off) |
| `last` | VO's last spoken phrase — the primary assertion source |
| `under` | Text under the VO cursor — position-stable, survives live-region chatter |
| `vo-move <dir> [n]` | Move the VO cursor (right/left/up/down), print landing text |
| `vo-into` / `vo-out` | Interact into/out of a container (tables, lists, sheets) |
| `vo-act` | Press the item under the VO cursor (VO+Space) — the reliable activation |
| `vo-first` | Jump the VO cursor to the window's first item (walk anchor) |
| `activate` | Bring the app frontmost (`SLATE_APP` env, default `SlateMac`) |
| `keys "<char>" [mods]` / `key <code> [mods]` | Synthetic keystrokes (codes: 36 Return, 48 Tab, 53 Esc, 49 Space, 125 Down, 126 Up) |
| `wait-phrase <substr> [s]` | Poll `last` until a substring lands (live-region announcements) |

## 3. Cross-cutting gotchas

These six cost the most time on the first run. Internalize before scripting.

1. **VO AppleScript syntax hangs off the cursor object.** `tell application "VoiceOver" to tell vo cursor to move right` / `move into item` / `perform action`. The flat form `move vo cursor right` is a syntax error (-2740). Enum names come from `sdef /System/Library/CoreServices/VoiceOver.app`.
2. **`last phrase` returns the current visual line (~50 chars, soft-wrapped) inside text views** — not the file line. Arrow-down counts don't map to file lines in wrapped paragraphs; match on substrings of the target line instead of counting.
3. **Typing echoes overwrite live regions.** Every synthetic keystroke becomes a spoken echo that lands in `last phrase`, racing announcements like "Search returned N results." Poll fast (0.4–0.6 s), match substrings, and treat a missed announcement as "not captured," not "did not fire" — re-test before filing.
4. **AX `set focused to true` does not reliably make NSTextView first responder.** Typing then goes elsewhere while the AX tree looks right. A real `click at {x,y}` (center of the text area via AX position/size) always works. **Verify input landed by reading the buffer back** (`value of AXTextArea contains <sentinel>`) before asserting anything about save behavior.
5. **Walk conditionally, never by fixed step-count.** Panel stacks reorder and resize per note (properties/backlinks/tasks counts change the walk length). Anchor with `vo-first`, walk with a substring stop condition, and expect disclosure groups to be collapsed (the Outgoing-links group starts collapsed; `vo-act` on its heading expands it — the heading announces its count either way).
6. **Keyboard selection is silent at HEAD** (file list, palette results, search results — issue #414). Arrow keys move selection without VO announcing the row; only side-effect live regions speak ("Outline, N headings."). Drive row-level assertions through the VO cursor (`vo-into` + `vo-move down`), which reads rows correctly.

## 3b. Dynamic Type & Reduce Motion (behavioral, cross-cutting)

These are **behavioral** checks that unit tests can't do (the `\.dynamicTypeSize` override isn't honored by headless `ImageRenderer`/`NSHostingView`, and animation timing isn't observable from XCTest) and that `a11y-check` only catches as *static* anti-patterns (`fixed-font-size`, `line-limit-1`, `animation-missing-reduce-motion`). Run them manually on every new U1–U5 surface — a surface can pass those static rules and still truncate at XXL or animate under Reduce Motion.

1. **Dynamic Type reflow.** System Settings → Accessibility → Display → **Text size**, raise to the largest step (or the app's largest supported size). Walk each new surface: text must **reflow/wrap, never truncate or clip**; controls stay reachable; no horizontal-scroll trap. Reset after.
2. **Reduce Motion.** System Settings → Accessibility → Display → **Reduce Motion** ON. Trigger every animated transition on the surface (sheet present/dismiss, scroll-to-anchor, expand/collapse, mode/tab switch): motion must be **removed or replaced with a crossfade**, never a large translate/scale. Confirm the view reads `@Environment(\.accessibilityReduceMotion)` and honors it. Reset after.

Report PASS/FAIL per surface alongside the VO paths in §7.

## 4. Navigation primitives (the paths that work)

- **Open any note: search-open.** Focus the editor region (see next bullet), `keys f cmd`, type a phrase unique to the target note (pull one from the note via grep), `wait-phrase "returned"`, then `key 36` — **Return on the field activates the top result** and announces `"Opened <file>, line N: …"`. This is the workhorse; it replaced fragile list walking entirely. (Return on a focused result *row* is a no-op — Space activates rows; #414.)
- **Recover editor focus: palette round-trip.** `keys p cmd shift` then `key 53` (Esc) — the palette restores focus to the prior first responder and VO announces it ("Note content for <file>. edit text"). Works whenever the editor was the last responder; otherwise Tab once from the window (Tab 1 reaches the editor after a search-open).
- **Select a file row deterministically (plumbing, not evidence):** AX-set `selected` on the matching row of the Files sidebar outline (match on the row's static-text concatenation). Selection opens the note. Use only to position; collect evidence via VO afterward.
- **Open the vault from the welcome screen:** focus lands on "Open Vault…" at launch (VO announces it with its hint). `key 49` (Space) → panel announces "Choose a folder of Markdown files to open as a vault." → `key 5 cmd shift` (Go to Folder) → type the vault path → Return → click/act the panel's "Open" button (`button "Open" of splitter group 1 of window "Open vault"`).
- **Caret placement inside the editor: use `scripts/ax-set-caret.swift`, NEVER System Events.** System Events' `set value of attribute "AXSelectedTextRange" of … to {offset, 0}` **silently no-ops** — the AppleScript list never marshals into an AXValue CFRange, and SE's read-back of the same attribute is garbage, so a "verify" through SE confirms nothing. This exact trap produced the false finding behind #412: the caret stayed at end-of-document, and Cmd+E truthfully announced "No embed at cursor" for a caret that was never inside the embed (raw-AX probe 2026-06-10: app-side `AXUIElementSetAttributeValue` succeeds and the caret moves; the same write through SE leaves it untouched). Park carets with:

  ```sh
  swift scripts/ax-set-caret.swift SlateMac "![[Whipped cream]]" 5
  ```

  which sets the caret via raw `AXUIElementSetAttributeValue` (the raw AX interface assistive clients drive) and exits non-zero unless an **independent raw-AX read-back** confirms the caret landed at the requested offset. Treat a non-zero exit as "caret not parked" and stop — do not proceed to assert on caret-dependent behavior.

## 5. Mutation hygiene

1. Back up the vault before anything (§1.4); record the file count.
2. Order read-only milestones before mutating ones: **A → B → C → D → J → K → L → E → Q first; G → F → H → I last.**
3. Pair **every** mutating step with a disk probe in the same breath: VO phrase **and** `grep`/`diff` on the target file. A VO announcement without the disk diff proves nothing (the editor-save bug shipped exactly that way — silence *and* no write).
4. Snapshot per-file before toggles (`cp file /tmp/before.md`), diff after, restore intended-but-unneeded toggles at the end.
5. Close out with the audit: `diff -rq <backup> "$VAULT"` — the only acceptable differences are the app's `.slate/` cache and artifacts you intended to create (e.g., a template-created note). List them in the report.
6. Restore machine state: `scripts/vo.sh stop-vo`, quit the app, note that the VO Utility AppleScript checkbox stays enabled.

## 6. Per-milestone test paths

Each path: steps → expected utterance (quote what VO must speak) → probe. Adjudication: PASS requires the quoted phrase captured; mutations additionally require the disk probe. A documented FAIL with evidence is a completed test.

### A — Vault + file list (M1)
1. Launch bundle; expect focus announce: **"Open Vault… button"** + folder-picker hint.
2. Space → panel: **"Choose a folder of Markdown files to open as a vault."**; Go-to-Folder → vault path → Open. Window renames to the vault.
3. `vo-first`, walk right to **"Files sidebar table"**, `vo-into`: rows speak **"<name>.md, modified <relative date> cell"**.
4. Probe: AX row count of the sidebar outline == `.md` count in the vault (was 35/35).

### B — Read + heading navigation (M2)
1. Search-open `Heading depth test.md`; Tab once → **"Note content for Heading depth test.md. edit text Insertion at …"**; arrow through lines — each speaks its visual line.
2. Right-sidebar Outline tab → `vo-into` the list: rows speak **"Level N heading: <text>"** (#420 — the trait alone is not voiced for Button rows); activating one scrolls and announces **"Scrolled to <heading>."** (focus stays in the panel by design).
3. Baseline FAILs to re-check: entries do **not** speak "Level N" (trait-only, #414) and activation is silent.

### C — Backlinks + outgoing links (M3)
1. Open a linked note (`Weekly ToDos.md`, `Index.md`, or `Linear algebra lecture 3.md`). Walk the left panel stack to **"Backlinks, N entries"**; entries: **"Backlink from <file>, context: <snippet>"**.
2. `vo-act` a backlink → content pane switches (probe: content-area AX label = "Note content for <target>."). Audible cue at baseline is only the outline live-region — listen for whether a target announce was added.
3. **"Outgoing links, N entries"** heading → expand if collapsed (`vo-act`) → entries: **"Link to <target>.md"**; the planted broken link in `Linear algebra lecture 3.md` must read **"Unresolved link: Linear algebra supplementary"**.

### D — Frontmatter properties, read (M4)
1. Open `Weekly ToDos.md`. Walk to **"Properties, 2 items"**.
2. Rows speak name + type + editability: **"<value> — Property title, text, editable"**; list property: **"Property notes, list, editable"** with **"item 1 of 2"** indexing.
3. The vault's YAML-trap line (a task-shaped string inside frontmatter) must appear here as a list item and **never** in any Tasks surface.

### E — Full-text search (M5)
1. From editor focus: `keys f cmd` → **"Search vault edit text"** + hint. (Cmd+F is focus-dependent at baseline — no-op from the sidebar tabs; #414.)
2. Type `xyzzyplover` (planted ×3 in `Search bait.md`) → `wait-phrase "Search returned"` → **"Search returned N results."**
3. Tab three times when a query is present (Clear and Close buttons sit between field and list; Clear appears only with text) → row speaks **"<file>.md: …<snippet>"**.
4. Return **on the field** → **"Opened <file>, line N: …"**.

### F — Editing (M6) — baseline FAIL #409, re-run after fix
1. Open any note; focus editor by **mouse-click into the text area** (gotcha 4); `key 125 cmd` (end), type a sentinel, **verify it in the buffer via AX value**.
2. `keys s cmd` → expect **"Saved"** announcement; probe `grep <sentinel> <file>` on disk.
3. Also exercise: toolbar **"Save"** button; the save-state toolbar item (**"Saved. Editor matches the on-disk file."** vs unsaved); switch-away-and-back (unsaved changes must not silently vanish); external-edit conflict (touch the file externally, then save → typed conflict dialog, default Cancel).
4. Baseline: buffer verified, then **no write, no announcement, no error** via both paths; switching notes discarded the edit. Toggle/property/template writes prove the session write path is healthy — the regression is the editor pipeline.

### G — Tasks (M7)
1. Open `Weekly ToDos.md`; walk to **"Tasks, N open of M tasks"**; rows: **"Open. Submit grant. Due 2026-06-01. Priority high. Repeats every year. Open task."**
2. Vault-wide: `keys r cmd` → **"N tasks shown"**; filter chips **"All, N tasks selected button"**, "Due today", "Overdue", "This week"; rows prefixed by source file.
3. Toggle: snapshot file → `vo-act` a **"Mark complete button"** → diff shows `- [ ]` → `- [x]` on that line. Restore after.
4. Anti-probes: the fence-trap and YAML-trap task strings (see vault README) never appear; `[/]` and `[-]` statuses — baseline reads both as "Open task" (tester question, #414).

### H — Templates (M8)
1. `keys n cmd shift` → picker rows speak name + first line: **"daily-note. # {{date:YYYY-MM-DD}}. button"**, **"meeting-note. # {{prompt:Topic}} — {{date}}. button"**.
2. `vo-act` meeting-note → prompt sheet, VO lands on **"Topic edit text"**; fill; Tab → **"Attendees edit text"**; fill; Tab to **"Next button"**, Space.
3. Name field (**"New note name edit text"**, default preselected) → type name → Tab past Cancel to **"Create button"**, Space.
4. Probes: file exists with prompts + `{{date}}` substituted and no literal `{{…}}` left; baseline gaps to re-check: no **"Created <file> from <template>"** announcement, and insertion point lands at end-of-text instead of the template's `{{cursor}}` (#414).

### I — Property editing (M9)
1. On a note without frontmatter, walk to **"Add property button"**, `vo-act` → sheet: **"Property key edit text"** + **"Property type pop up button"** → key in a name, Tab to **"Add button"**, Space → **"Property <key> updated."** + disk gains the YAML block.
2. Edit: `vo-act` the row (**"Property <key>, text, editable edit text"**), type value, Return → disk shows `<key>: <value>`.
3. Delete: row's **"Delete property <key> button"** → dialog speaks **"Delete property `<key>`? …"** with VO landing on **"Cancel button"** (default) → move to **"Delete button"**, `vo-act` → **"Property <key> deleted."** + frontmatter gone from disk.

### J — Embeds (M10)
1. Open `Linear algebra lecture 3.md`; walk to **"Embeds, 1 entry"** → **"Embedded note: learning/Linear algebra lecture 2.md"** disclosure with the embedded content readable inside and **"Jump to source: <path> button"**.
2. `Apple pie.md` exercises all three forms: whole-note (resolves), block-ref `#^method-step-2` (baseline FAIL: **"Unresolved embed: … The heading wasn't found"** — #413), markdown image (baseline FAIL: alt text dropped, only `AXHelp="Embedded image: pie.svg"` — #414).
3. Cmd+E preview: park the caret inside `![[…]]` with `swift scripts/ax-set-caret.swift SlateMac "![[<target>]]" 5` (§4 — System Events CANNOT set the caret; the 2026-06-10 "always No embed at cursor" finding (#412) was this harness trap, not an app bug: the caret was still at end-of-document). With the caret genuinely inside the embed, `keys e cmd` must open the preview popover — VO speaks its accessibility label **"Embed preview for <target>, source line N."** (the visual header reads "Preview for `<target>`" but VO announces the AX label; `wait-phrase "Embed preview for"` is the correct match). **"No embed at cursor."** is correct only when the *insertion point* is genuinely outside every embed — note that reading a line with the VO cursor does NOT move the insertion point, so Cmd+E after a VO-cursor-only walk truthfully reports no embed. Markdown images (`![alt](src)`) are out of Cmd+E scope by design — the fallback announcement on an image line is correct behavior (pinned by `EmbedPreviewCmdEIntegrationTests`).

### K — Math / code / Mermaid (M11) — baseline FAIL #410, re-run after wiring
1. `Math sampler.md`: caret onto the display-math line — must speak MathCAT speech ("the sum from i equals 0 to n…"), **not** raw `$$\sum_{i=0}^{n}…$$` (baseline: raw).
2. `Code cookbook.md`: crossing a fence must produce **"Code block, <language>, N lines"** (baseline: raw "```go").
3. `Mermaid sampler.md`: diagram must speak a structured description ("Flowchart with N nodes…") (baseline: raw "```mermaid").
4. The planted malformed `$\frac{a$` must fail gracefully (typed render status, no crash).

### L — Citations + bibliography (M12) — baseline FAIL #411 (config), re-run after contract decision
1. Precondition: citation config reachable by the app (baseline mismatch: app reads `.slate/prefs.json`; vault ships root `slate.json` → nothing loads).
2. Open `AI for accessibility — short reflection.md` (3 cites). Citations sidebar tab rows: **"Citation: <key>"**; expanding must yield field-level nodes (title/authors/year) — baseline: **"Unresolved citation: kane2020atai."** for a key present in `library.bib`.
3. Toolbar **"Citation Summary"** → sheet speaks **"This note has N citations referencing M unique sources."** (works at baseline) + **"Walk through citations button"**.
4. Bibliography tab: entries with per-field content (baseline: empty). The planted `[@notinbib2099]` must read as unresolved even after the fix.

### Q — Command palette (M17)
1. `keys p cmd shift` → VO lands in **"Search commands edit text"** with hint **"Arrow up and down to move selection. Return runs the selected command."**
2. Type a filter → live region **"N command(s) matching \"<filter>\""**; rows (via VO cursor) speak **"<command>, <hotkey> … button"** with per-command hints; section headings ("Navigation").
3. Return invokes the selection (observable effect, e.g. **"N tasks shown"**); Esc closes and announces focus restore to the prior responder.

### U — Workspace shell: tabs, splits, modes, properties, leaves, file management (Milestones U1–U5)

Scripted end-to-end path per u5_spec §U5-4. Fixture: the demo vault plus one
note containing every block kind (headings/list/task/code/math/mermaid/embed/
citation/wikilink — `reading-fixture.md`; create it from the U3-1 test fixture
if the vault lacks one). **2026-07 baseline column**: `auto-PASS` rows were
verified by the automated suites (the utterance string is pinned by a unit
test); `pending` rows need the human VO pass — every FAIL becomes an `audit`
issue; the milestone does not close with an open FAIL.

| # | Step | Expected utterance / outcome | Baseline 2026-07 |
|---|---|---|---|
| U1 | Open vault → tree: expand two folder levels (→ key) | Folder rows: **"<name>, expanded/collapsed, N items, level N"**; → expands, ← collapses (onMoveCommand mapping) | pending |
| U2 | Open note (Return) → ⌘-click a second note → tab strip | **"Showing <file>."** then tab items: **"<name>, tab 2 of 2, selected"**; dirty tab appends **", edited"** | pending |
| U3 | Reorder tab (⌃⌘→) → ⌘1/⌘2 direct select | Reorder announces the new position; ⌘N selects + **"Showing <file>."** | pending |
| U4 | Split right (⌘\) → ⌘⌥arrows across panes → resize (⌘⌥+/-) | **"Split. Editor pane 2 of 2, <title>."** · moves announce **"Editor pane N of M, <title>."** · resize: **"Pane resized, N percent."** | pending |
| U5 | ⌘⌥← from leftmost pane → tree; ⌘⌥→ back; ⌘⌥→ from rightmost → leaf; ⌘⌥← back | **"Files."** → **"Editor pane …"** → **"<leaf> panel."** → same pane returns (anchor); Tab-into-tree then ⌘⌥→ also returns (passive region mirror) | pending |
| U6 | Reading mode (⌘⇧E) on the fixture note → continuous read (VO+A) | **"Reading mode."**; continuous read flows: headings speak as headings (rotor VO+H walks document order), list depth as **"list item, level N"**, task rows expose **"Mark complete/incomplete"** buttons, code/math/mermaid speak their existing panel contracts, thematic break silent | pending |
| U7 | Activate a wikilink in reading mode → land per open rules; activate an internal markdown link | **"Opened <file>."** (+ unresolved links announce **"<target> is unresolved. Cannot open."**, never silent) | pending |
| U8 | Toggle a task in reading mode (buffer clean) | Checkbox toggles; disabled-while-dirty carries the hint **"Save the note first…"** | pending |
| U9 | Properties: edit a text property → add one → show source (⌘⇧D) → apply a YAML edit | **"Property <key> updated."** · sheet flow per M4 · **"Showing source"** value on the toggle · apply: **"Properties updated."**; malformed YAML: inline error spoken, nothing written | pending |
| U10 | Scripted external edit → apply YAML → conflict alert → Keep Mine | The property-conflict alert (three buttons) with focus returned to the toggle on resolve | pending |
| U11 | Leaves: rail arrows across all 10 → each leaf announces + renders | **"<leaf> panel."** per switch; rail is ONE Tab stop (single-focus-stop) | pending |
| U12 | File management: new folder → rename (inline) → move (picker) → delete. **Delete branches (#860):** files/empty folders direct; a NON-EMPTY folder stages "Delete folder …?" (Move to Trash destructive / Cancel; Escape = nothing deleted; focus returns to the tree on both buttons) | **"Created folder <name>."** · **"Renamed <old> to <new>."** (+ **", updated links in N notes."** when inbound links exist) · **"Moved <name> to <folder>."** · **"Moved <name> to Trash."**; focus per U2-6 (create→new row, delete→sibling) | pending |
| U12b | Tree power keys (#850/#853): type a filename prefix → selection announces (folder landings: **"Selected: <name>, folder"**); F2 on a selected row → rename field focused; Duplicate → **"Duplicated <src> as <copy>."** with selection on the copy | pending |
| U6b | Code block Copy button (#854): VO reads **"Copy code button"** + hint; activation announces **"Code copied."**; pasteboard holds the plain source (context-menu path unchanged) | pending |
| U9b | Date property (#857): conforming value exposes a date picker (**"<key> date picker"**); picking commits + **"Property <key> updated."**; a malformed stored value stays a text field and round-trips verbatim | pending |
| U9c | Spell check (#855): Edit ▸ Check Spelling While Typing toggles squiggles live in the editor (checkmark reflects state; the native context-menu item routes to the same pref); the YAML source editor never spell-checks | pending |
| U13 | Verify a wikilink to the moved/renamed file still resolves (open the citer) | Link opens the moved target (**referential stability** — the U2-3 invariant, census-guaranteed) | auto-PASS (censuses; spot-check live) |
| U14 | Quit → relaunch → layout + per-tab modes restored; externally-deleted tabbed file | Same panes/tabs/modes (workspace.json v1); deleted file's tab shows the error pane, never silent | pending |
| U15 | Dynamic Type XXL over U1–U14 surfaces | No truncation/clip; properties header scrolls internally; tab strip scrolls horizontally | pending |
| U16 | Reduce Motion pass | Mode/leaf swaps are instant (no animation exists — inherently safe); scroll routing jumps respect the setting | auto-PASS (structural: swaps are unanimated `if` mounts) |
| U17 | Keyboard-only pass: complete U1–U14 with no pointer | Every step completes; no focus limbo (the U4-4 no-trap tests are the structural guarantee; this is the live confirmation) | pending |
| U18 | macOS 26 only: Liquid Glass visual check — tab strip / leaf rail / utility bar in BOTH appearances | Glass renders legibly over bleed-through content (esp. the tab strip); <26 fallback is byte-identical tokens (no check needed) | pending |

**Known chatter items to adjudicate during the pass** (from U4-4 review; fix as
`audit` issues only if they read as noise in practice): same-pane terminal
return re-announces "Editor pane N of M"; empty-vault tree entry is a silent
no-op (⌘⌥→ still exits).

## 7. Reporting

Per milestone: PASS / PARTIAL / FAIL + the quoted utterances + disk probes. File findings as `audit`-labeled issues; one issue per root cause, umbrella for announcement-polish items. Re-run only the FAIL/PARTIAL paths after fixes land, plus A (smoke) — full sweep per release.

**Baseline (2026-06-10, main @ 798d536):** 7 PASS (A, C, D, E, G, I, Q) · 3 PARTIAL (B, H, J) · 3 FAIL (F, K, L). Filed: [#409](https://github.com/coryj627/slate/issues/409) editor save silent data loss · [#410](https://github.com/coryj627/slate/issues/410) K surfaces unwired · [#411](https://github.com/coryj627/slate/issues/411) citation config contract · [#412](https://github.com/coryj627/slate/issues/412) Cmd+E dead · [#413](https://github.com/coryj627/slate/issues/413) `#^` block-refs · [#414](https://github.com/coryj627/slate/issues/414) announcement/focus umbrella.
