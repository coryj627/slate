# Agent-operated NVDA pass over the whole §W-C matrix — 2026-09-22

An agent-operated production run across every NVDA row of the
[§W-C matrix](../w_c_matrix.md) and the W1–W6 and W7-2 checklists. It is
evidence, not acceptance. Like the [W7-1](w7_1_nvda_production_verification.md)
and [W7-6](w7_6_nvda_production_run_2026-09-20.md) runs, it fills no human
cell. Every matrix and checklist cell stays as it was. The checklists cite
this record as production evidence. The owner's human NVDA and licensed JAWS
passes remain the acceptance targets, and Narrator stays W8-6 smoke scope.

**Tester:** Claude (agent, Opus 5.5), keystrokes injected with `SendInput` from a script that refuses to type unless a Slate window is in the foreground. Screen control (computer use) was declined for Slate and NVDA, so there are no screenshots and no visual checks.
**AT:** NVDA 2026.2 AMD64, OneCore (David), started by the agent from a shell. It ran on the owner's installed profile, which is not stock: laptop layout, speak command keys on, symbol level "most", rate 56, debug logging on (including the UIA debug category). No add-ons, and no Slate app module in the scratchpad.
**OS:** Windows 11 Pro 25H2, 10.0.26200.9550
**Build:** `claude/at-matrix-nvda-testing-3ef005` = main at `039dc40fab66bac7b9be0f126b6f06c55d5279fd`, Release `SlateWindows.exe`; `GridConformanceHost.exe 10000` for W4 #9
**Corpus:** a disposable copy of `demo-vault` (plus `editor_semantics.md`, a 2,100-paragraph `Ceiling test.md` and `Bad diagram.md`), a 2,000-note generated vault (opened at launch and again after launch as a fresh copy), and a three-note vault whose `.slate/sidebar.json` was `{"version":1,"sort":"bogus"}`
**Method:** keyboard only. Speech comes from NVDA's log: `Speaking` lines, plus the UIA `handleNotificationEvent` debug lines that show what the app raised. UI Automation queries (System.Windows.Automation) showed focus, names, values and item status.
**Run date:** 2026-09-22, 17:04–18:36 local (America/New_York)
**Evidence reference:** [nvda_agent_matrix_pass_2026-09-22_nvda_speech.txt](nvda_agent_matrix_pass_2026-09-22_nvda_speech.txt). Section headers carry the checklist key and the batch end time quoted in each row below.
**Audio:** OneCore initialised. No human confirmed audibility.

## Headline findings

Ranked by user impact. Every one reproduced at least twice unless it says otherwise. Section tags refer to the transcript. Each finding is filed as an issue (#1244–#1257), linked in the first column.

| # | Finding | Repro / evidence | Likely cause (pointer, not verified fix) |
|---|---|---|---|
| F1 ([#1244](https://github.com/coryj627/slate/issues/1244)) | **No app announcement reaches NVDA after Slate launches until a popup (any menu) has been opened once.** After that, every typed announcement arrives normally. Scan start and finish at launch are therefore never heard. | Fresh launch, then Ctrl+Alt+= / Ctrl+Alt+Left and Right: no `handleNotificationEvent` at all (`notif probe after relaunch`, 17:28:19–24). Alt+W, Escape, same keys: "Pane resized, 60 percent." (`notif probe after menu`, 17:28:35). Also reproduced in the first session (nothing received from 17:04 until the first menu use at 17:15), on the 17:57 fresh launch (Ctrl+R's "Tasks review. All." missing from `F7 clean repro`, heard at 17:57:33 after unlocking), and on the 2,000-note vault launch before the sidebar test (no scan lines). Explains the W7-6 "notifications not received" residual. | NVDA registers for notifications globally. WPF raises `Notification` only once its event map knows a listener is registered, and a popup HwndSource seems to be what first advertises it. Worth checking whether the main window's provider receives `AdviseEventAdded` for Notification at startup. |
| F2 ([#1245](https://github.com/coryj627/slate/issues/1245)) | **The Files tree can't be walked.** Down from a row lands on that row's "Select … for batch actions" check box. The next Down selects the next row, and selecting a file opens it and moves focus into the editor. Every arrow onto a file steals focus. | `W1#3 tree` 17:11:19; `W1#3 tree row-to-row` 17:19:38–42 ("Selected: document.pdf" … "document.pdf editor, document"). | `FilesSidebarViewModel.SelectedNode` setter → `RequestOpen(value.Path)`, plus a focusable CheckBox in the tree item template. |
| F3 ([#1246](https://github.com/coryj627/slate/issues/1246)) | **Unnamed items read as .NET type names or record dumps.** Filter results ("SlateWindows.FileTreeNodeViewModel"), split panes ("SlateWindows.WorkspacePaneNodeViewModel", on every pane move and at launch), Recent vaults ("RecentVault { Path = …, LastOpenedMs = 1790112463550 }"), property rows ("SlateWindows.Panels.PropertyRowViewModel"), Bases rows ("SlateWindows.Bases.BaseGridRowViewModel"), bibliography rows, canvas table rows ("CanvasTableRow { NodeId = grp-research, … GroupPath = System.String[] … }"), template prompt rows ("SlateWindows.Templates.TemplatePromptFieldViewModel"), the bulk-rename type combo ("KeyTypeChoice { Label = Any key type, Kind = }") and GridConformanceHost rows ("FixtureRow { … }"). Graph table rows are named correctly. | `W1#4 filter results` 17:20:35, `W1#5 split via menu`, `W1 recent vaults` 17:29:40, `W4#4 properties`, `W4#6 base walk`, `W4#5 bibliography`, `W6-1#2 table`, `W5#3 prompts cancel`, `W4#4 add property`, `W4#9 10k grid` | Item containers with no `AutomationProperties.Name` fall back to `ToString()`. For grids this is the shared AccessibleDataGrid row peer, since GraphTable sets a name and the others don't. |
| F4 ([#1247](https://github.com/coryj627/slate/issues/1247)) | **Arrow keys jump from container landings into the menu bar.** When focus is on a container with no current item (the empty Editor workspace F6 stop, the Right pane panels list after Ctrl+R, the Citations list after Shift+F6, the Tasks Review radio buttons, the tab control after "View as List"), Up/Down/Left/Right move focus onto a top-level menu (Canvas or Graph). | `W1#8 empty editor arrows clean` 17:17:58. Fresh launch with no Alt key pressed: `F7 clean repro` 17:57:16 ("All, 56 tasks, radio button" → Down → "Graph, collapsed"). Also `W4#3 review`, `W4#5 citation details`, `W4#6 view as list`. | W7-5 took `MainMenu` out of Tab (`TabNavigation=None`) but left directional navigation able to enter it. |
| F5 ([#1248](https://github.com/coryj627/slate/issues/1248)) | **Template prompt sheet leaks keystrokes into the editor behind it.** Tab in the "Topic" field doesn't move to "Attendees". Each Tab is typed into the open note behind the sheet (Search bait became "unsaved changes", three tab characters). Enter jumps straight to the note-name field, skipping the second prompt. | `W5#3 prompts cancel` 18:12:05, `W5#3 prompts focus`, `W5#3 complete`. The stray edits were discarded afterwards and the file on disk is byte-identical. | The sheet doesn't fence keyboard input, and the prompt list's Tab navigation doesn't reach the next row's field. |
| F6 ([#1249](https://github.com/coryj627/slate/issues/1249)) | **The save-conflict announcement reads internal diagnostics.** "Save blocked. Could not save Grocery list.md: @currentContentHash=74ab9cd8…ee1e, @expectedContentHash=1f2c357d…1ebc, @currentMtimeMs=1790116253121. Your edits remain in the editor." The behaviour itself is right: blocked, local edits kept, disk version not overwritten. | `W7-2#9 save` 18:30:55 | The raw core error message is composed into the D-10 announcement. |
| F7 ([#1250](https://github.com/coryj627/slate/issues/1250)) | **The tag filter never matches.** Choosing a tag in the tag tree (for example "essay, 1 file") sets `tag:"essay"` and says "File list, 0 items". Typed `tag:essay`, `tag:project` and `tag:accessibility` all give 0. Ctrl+Enter on `#project` in the editor gives "Filtered files by tag project." then "File list, 0 items". `path:graph` gives 20. | `W1#3 tag tree walk` 17:21:27, `W1#4 tag filter manual`, `W1#4 tag filter inline`, `W2#4 tag activate 2` | Confirmed in source: `FilesSidebarViewModel.ActivateTag` writes `tag:"x"`, but core's filter grammar (`parse_sidebar_filter`) spells tags `#x`, so `tag:` falls through as a plain name word. The field's help text advertises `tag:` too. |
| F8 ([#1251](https://github.com/coryj627/slate/issues/1251)) | **Popover and sheet outcomes aren't spoken.** Ctrl+Enter / Ctrl+E popovers, Citation Summary and citation details open with focus on Close or the first button. The outcome text ("Unresolved citation: smith2020", "Target not found: Target", "This note has 3 citations referencing 3 unique sources", "Embedded note: …") lives only in the pane's Name and is never read. | `W2#4 tag + citation activate` 17:32:36, `W2#4 tag activate`, `W2#4 embed preview`, `W4#5 citations` 18:03:17, `W4#5 citation details 2` | A Pane's name isn't announced on a focus change. A dialog role, or an announcement on open, would carry it. |
| F9 ([#1252](https://github.com/coryj627/slate/issues/1252)) | **External file changes aren't picked up.** Notes created on disk (`Ceiling test.md`, `Bad diagram.md`) never appeared in the tree, Quick Open or the filter until Slate restarted, in a reopened session or a fresh one. Files Sidebar → Refresh is silent and doesn't find them. | `W3#1 ceiling`, `refresh`, `filter ceiling`, `QO bad diagram retry`, `refresh fresh session 2` | Watcher / refresh path (not an AT issue, but it blocks fixture-based checks). |
| F10 ([#1253](https://github.com/coryj627/slate/issues/1253)) | **Reading-view focus route.** Ctrl+Shift+E into reading, and opening a note already in reading mode, leave focus on the "Workspace tabs" tab control or the TabItem. Reaching the document takes about six Tab presses (via Close, the Properties header, Rename and Add property). In reading mode, F6 skips the document entirely (tab bar → right pane). | `W3#5 code reading` 17:39:22, `W3 reach reading surface 2`, `W3#1 reading chords`, `W4#2 outline` | The reading surface isn't the editor region's F6 landing, and the view toggle doesn't focus it. |
| F11 ([#1254](https://github.com/coryj627/slate/issues/1254)) | **Command palette stalls, and stale announcements win.** The first keystroke blocks the UI for about 2.5 s (NVDA echoes the typed letters late). Meanwhile NVDA drops notifications because it can't resolve the sender's window ("dropping notification event at request of appModule slatewindows"), and stale "Selected: New Canvas" / "87 commands matching "b"" lines are spoken before or instead of the final count. | `W5#1 palette` 18:08:33, `W5#1 palette retry` 18:09:27–30, `W7-2#2 urgent` | Palette filtering runs on the UI thread. |
| F12 ([#1255](https://github.com/coryj627/slate/issues/1255)) | **Canvas visual navigator.** After switching to Visual, Down says "End of canvas." and Up says "Start of canvas." from the same position, and Right/Left (follow connection) say nothing. Where am I meanwhile reports a seated card, and invoking a card peer selects it ("File card "canvas research", 3 of 4 in Research"), yet the next Down still says "End of canvas." | `W6-1#4 navigator` 18:23:45–52, `W6-1#3 invoke card` 18:24:29 | The navigator's seat isn't restored when the view changes. |
| F13 ([#1256](https://github.com/coryj627/slate/issues/1256)) | **Shift+F10 opens the wrong menu on canvas rows and the board.** Shift+F10 on a canvas outline row or the visual board opens the workspace tab's context menu (Duplicate Tab, Close Tab, Split Right, Split Down, Close Pane). The Applications key opens the correct card menu (Open … Toggle Mark … Delete). | `W5#9 canvas ctx menu`, `W5#9 outline ctx 2`, `W5#9 apps key` | A tab-level ContextMenu handles Shift+F10 before the row's menu does. |
| F14 ([#1257](https://github.com/coryj627/slate/issues/1257)) | **Connections leaf: Enter opens the note instead of re-rooting.** Enter on a child row opens it in the Graph tab itself, replacing the graph ("Opened Bravo relay.md."). Ctrl+[ then does nothing. The checklist expects Enter to re-root. | `W6-2#2 walk reroot back` 18:28:05 | Either the checklist or the leaf's activation needs changing. This is an owner call. **Corrected checklist in #TBD (OD-5):** the activation stands. The graph spec, contract 35 B-9 and the mac all open the note on Enter and re-root only through Show connections. Checklist item 2 now says so, and a note row's hint names the new-tab chord. |

### Focus lost after an action

Each of these leaves focus on the bare "Slate window", or on a container with no current item, so the next keystroke goes nowhere:

- closing the last tab with Ctrl+W (17:11:30, 17:19:17);
- closing the tab of a split pane (18:11:03);
- committing a property with Enter (17:58:41);
- deleting a canvas card from its menu (18:26:33), which also makes the offered "Ctrl+Z to undo" do nothing until focus is back in the canvas;
- the successful sidebar-settings Retry, whose button disappears (18:33:13);
- completing a task with Space (focus goes to the "Open tasks" list container, 17:56:05);
- Ctrl+R (focus goes to the "Right pane panels" list container);
- Move to (focus goes to the Files tree container, 18:16:44).

### Lower-severity findings

- Tab switching (Ctrl+Shift+] / [, Ctrl+Tab) lands on the "Properties, N properties" group instead of the editor (17:23:20, 17:25:54).
- The Properties header is a double tab stop (grouping, then toggle button).
- The editor split handle is focusable but unreachable by Tab: the editor keeps Tab, and Shift+Tab from pane 2's tab bar skips the handle. Grow/Shrink Pane (Ctrl+Alt+= / -) work and are announced.
- The handle reports "45% of split space" right after "Pane resized, 55 percent.", without saying which pane is measured.
- The Files sidebar and right-pane resize thumbs expose no value and are silent under arrow keys.
- The File sort order combo reads "NameAscending".
- Menus reuse access keys:
  - File: C for both New Canvas and Close Vault;
  - Files Sidebar: S ×3, R ×3, C ×2, F ×2;
  - Workspace: R ×5, plus repeats of D, G and P.
- Moving between top-level menus with Left/Right doesn't speak the new menu's name.
- "unavailable" is spoken as Quick Open, the palette and Search open, because the previously focused element is disabled before focus moves.
- Several announcements start with a stray space (" Apple pie, tab 2 of 2.").
- The welcome button reads "Open Vault…, button, …, carriage return".
- Reopening a vault from Recent Vaults says "Scan complete. 0 files indexed." (the count means newly indexed files).
- Down through a soft-wrapped paragraph in the editor re-reads the whole paragraph on every press.
- The embed preview's "Embedded content" field never moves past its first line under Down.
- In reading view, Space doesn't activate Copy code (Enter does). One Enter got "Could not copy code. Try again." while the clipboard was busy.
- Ctrl+Alt+K misses a link inside a table cell ("No next link." from the top of Markdown features).
- In reading caret navigation a diagram reads only as "grouping". The summary comes through Ctrl+Alt+D or Tab, and Tab also reads the whole source.
- A block embed's raw id "^method-step-2" is read at the end of Say All.
- The Bases summary reads "formula.ppu average: 9.495000000000001".
- The close button of the "brief_example" tab reads "Close briefexample" (the underscore is taken as a mnemonic).
- "Walk through citations" returns to the editor and says "Switch to the Citations sidebar tab" (mac wording, and focus doesn't move).
- History's "Deleted" segment is silent when chosen, and its empty text isn't focusable.
- The Sync leaf's first F6 stop is an unnamed "pane", and Refresh Sync Diagnostics is silent.
- Palette "Selected: …" lines leave out the spoken chord that the item Names carry.
- Search results say only "Selected: <file>" while walking. The snippet is heard only on activation.
- F2 (Rename, Global in the chord table) does nothing from the editor. It works from the Files tree.
- Ctrl+Shift+O does nothing from the Files tree. File → Open Vault… works.
- "1 destination." is spoken three times while typing in the Move picker.
- The batch-tag buttons are named just "Add" and "Remove".
- Batch Add Tag writes `tags: ["#agenttest"]`, with the hash, to frontmatter.
- Canvas colour names are only in ItemStatus ("3 of 4 in Research, green"). The outline walk and selection sentence never speak them; Where am I does ("red").
- Arrowing to a radio button (canvas and graph view switchers) doesn't select it; Space does.
- The first Ctrl+Alt+S in the canvas table produced no "Sorted by …" announcement; the second said "Sorted by Type, descending".
- Tab and Shift+Tab walk every grid cell (54 in the canvas table) before leaving.
- "1 references" and "1 links in" should be singular.
- Graph zoom is spoken twice ("Zoom 125 percent" and "Zoom 125 percent.").
- "Pinned." and "Unpinned." don't name the node.
- Quick Open onto a note deleted meanwhile opens a blank editor marked Saved, with no warning.
- Reopen Closed Tab on a deleted file names the tab "…, missing from disk", but the announcement is the ordinary " AT pass renamed copy, tab 5 of 5.".
- Undo after Delete (to the Recycle Bin) says "Nothing to undo."
- Undo and redo in the editor are silent (common to NVDA editors; noted only).

### Environment, not product

- Ctrl+\ (Split Right) is taken globally by 1Password on this machine and opened its unlock prompt. Split Right was done through the Workspace menu.
- Ctrl+Alt+L is taken globally, as recorded before, so Ctrl+Alt+U was used.

## Checklist results (agent run; human cells unchanged)

Result words follow the template. **Verified** means the check's observable outcome was heard. **Finding** means it wasn't, or a defect was hit. **Pending** means the check was not executed, or only part of it ran; the part that did not run is named.

| Matrix surface / checklist item | Result | Heard/observed behavior | Transcript reference | Finding / retest |
|---|---|---|---|---|
| w1_shell_at_checklist.md#1 | Finding | Window, menus, menu items and accelerators all read ("Open Vault…, O, Ctrl+Shift+O, 1 of 7"). Welcome reads "Vault closed. Returned to the welcome screen." then "Open Vault…, button". File → Open Vault… opens the "Open vault folder" dialog. Recent vaults read as record dumps. | `W5#5 File menu`, `W1#1 welcome`, `W1 recent vaults`, `open vault menu` | F3, repeated access keys, "carriage return" |
| w1_shell_at_checklist.md#2 | Finding | At launch there was no scan speech at all (F1). Opening from recents gave "Vault at-vault opened. Scanning files for the sidebar." and "Scanning vault. 91 files to index.". Opening an unindexed 2,000-note vault after launch ended with "Scan complete. 2000 files indexed.", in under 2 s, so no progress lines were due. | `W1 reopen from recent + scan`, `W7-2#4 scan fresh 2000 go` | F1, "0 files indexed" wording |
| w1_shell_at_checklist.md#3 | Finding | Levels, expanded state and "n of m" are spoken ("level 1, attachments, folder, expanded, 1 of 23"; tags "accessibility, 3 files"), but arrowing opens files and steals focus, and tag activation filters to 0. | `W1#3 tree arrows`, `W1#3 tree row-to-row`, `W1#3 tag tree walk` | F2, F7 |
| w1_shell_at_checklist.md#4 | Finding | Ctrl+Alt+F reads "Filter files, edit, Filter by words, tag:, path:, or @date."; counts are spoken ("File list, 2 items"); sort, group, tag-tree and dual-pane controls read with state. Result rows are unnamed, and Down onto one opens it. | `W1#4 filter`, `W1#4 filter tab order`, `W1#4 filter results walk` | F3, F2, "NameAscending" |
| w1_shell_at_checklist.md#5 | Finding | Tabs announce ("Apple pie, tab 2 of 2."). Panes announce ("Editor pane 2 of 2, Apple pie."). Reopen Closed Tab works. Tab switching lands on Properties, closing the last tab loses focus, and panes are unnamed items. | `W1#5 tabs`, `W1#5 split via menu`, `W1#5 close reopen tab` | F3, focus loss, Properties landing; Ctrl+\ taken by 1Password |
| w1_shell_at_checklist.md#6 | Finding | Restore brought back both panes, the tabs and the split ratio, with focus in pane 1's editor. The handle can't be reached by Tab; Grow/Shrink Pane speak "Pane resized, 55 percent.". | `W1#6 grow shrink`, `W1#6 reach handle 2` | Handle not in Tab route; thumbs silent |
| w1_shell_at_checklist.md#7 | Verified | "78 recent files"; "4 files matching "project""; "Selected: Project Beta plan" on each move; Enter opens; Escape returns to "Graph hub.md editor"; Ctrl+Enter opens a new tab; Ctrl+Alt+Enter opens a right split. | `W1#7 Quick Open multi`, `W5#7 QO split` | "unavailable" noise on open |
| w1_shell_at_checklist.md#8 | Verified | Once announcements were unlocked (F1), every landing plus its typed line was heard: "Files." "Editor pane. Empty." "Outline panel." "Right pane panels." "Status bar. Scan finished: 91 files indexed." "Menu bar." "Tab bar. Graph hub, tab 2 of 2." "Editor pane 1 of 2, Graph hub.". The ring wraps both ways; the right pane is skipped when hidden ("Right pane hidden."). The status bar landing is long: "Status bar, status bar" + help + typed line. | `W1#8 F6 ring no tab`, `W1#8 F6 ring with tabs`, `W1#8 hidden right pane` | Typed lines are inaudible until F1 is fixed; arrows from the empty-editor stop (F4) |
| w2_editor_at_checklist.md#1 | Verified | "Apple pie.md editor, document, heading level 1, # Apple pie". Typing is echoed; undo and redo change the text silently; "Saved Apple pie.md." | `W2#1 type undo redo save` | Undo/redo silent (noted only) |
| w2_editor_at_checklist.md#2 | Pending | Not executed: no IME installed in this session. | — | — |
| w2_editor_at_checklist.md#3 | Pending | Not executed: visual (highlight versus buffer), and screen capture was declined. | — | — |
| w2_editor_at_checklist.md#4 | Finding | Unresolved wikilink: "Target is unresolved. Cannot open." Tag: filters to 0 (F7). Citation and embed popovers open silently on "Close, button" (F8). Ctrl+E embed preview: the content field reads only its first line. Escape returns to the editor line. | `W2#4 activate unresolved`, `W2#4 tag + citation activate`, `W2#4 embed preview read`, `W2#4 embed content` | F7, F8, preview content |
| w2_editor_at_checklist.md#5 | Verified | `parity_matrix.md` rows 329–330 designate W2-4 autocomplete and W2-5 LaTeX aids as unshipped (Milestones V/X), and no popup exists. | — | — |
| w2_editor_at_checklist.md#6 | Verified | Heading levels: "heading level 2, ## Heading two". Links: "link, [[Target]], and, link, ![[Target]], and, link, #project, and, link, [@smith2020]". Quote, comment and code lines are read. Character and word moves say "link, left bracket" and "out of link". | `W2#6 open fixture + line read`, `W2#6 line read 2`, `W2#8 link boundaries` | Wrapped-paragraph re-read |
| w2_editor_at_checklist.md#7 | Verified | NVDA+A read the fixture to "let answer = 42;" and ```` ``` ````, then `say-all:stop`. In an 8-line-high window it read on past the first screen through "## Second H2 immediately following". | `W2#7 say all tail`, `W2#7 say all offscreen` | — |
| w2_editor_at_checklist.md#8 | Verified | Entry "link, left bracket"; "out of link, and"; Shift+Right "left bracket selected"; Ctrl+Shift+Right "Target selected"; Shift+Left "t unselected"; NVDA+K "Target". Deleting a selected link and undoing it was not repeated in this run. | `W2#8 link boundaries` | Partial: deletion/undo not executed |
| w2_editor_at_checklist.md#9 | Pending | Braille owner-deferred (2026-09-18). | — | — |
| w2_editor_at_checklist.md#10 | Pending | Not executed: no IME. | — | — |
| w2_editor_at_checklist.md#11 | Verified | NVDA consumes Heading 1/2 and Link during line reading, on the owner's profile (not stock). | `W2#6 open fixture + line read` | Stock-profile run still owed |
| w3_content_at_checklist.md#1 | Finding | Headings: "Callouts, level 2 heading." / "Footnotes, level 2 heading."; misses: "No next link.", "No next table."; lists via Ctrl+Alt+U: "…, list."; tables: "Feature, table." then "table with 5 rows and 4 columns"; ceiling: "Reading view shows the first 2,000 blocks of this note. Switch to the editor for the full text."; Say All reads in order. Focus route and the table-cell link miss are wrong. | `W3#1 reading chords`, `W3#1 table back`, `W3#1 ceiling`, `W3#1 reading say all` | F10, table-cell link |
| w3_content_at_checklist.md#2 | Verified | "the sum from i is equal to 0, to n of; i squared; … denominator 6, math."; Enter re-reads; Ctrl+Enter returns the Nemeth string. | `W3#2 math` | — |
| w3_content_at_checklist.md#3 | Verified | "Math speech style: SimpleSpeak." then SimpleSpeak wording ("fraction, … over 6, end fraction", "3 equations"); the malformed `$\frac{a$` keeps its source. Style was reset to ClearSpeak afterwards. | `W3#3 math prefs change`, `W3#3 simplespeak compare`, `W3#3 malformed` | — |
| w3_content_at_checklist.md#4 | Finding | "Flowchart with 5 steps, diagram."; Tab reads the summary plus the full source. In caret reading a diagram is just "grouping". An invalid-looking source parsed as "Flowchart with 1 step", so the failure path was never reached. | `W3#4 diagrams`, `W3#4 tab to diagram`, `W3#4 invalid diagram read` | Bare "grouping"; failure path not exercised |
| w3_content_at_checklist.md#5 | Finding | The chord lands on the first code line; the preamble "Code block, javascript, 7 lines." is on the line above; Enter on Copy code says "Code copied."; Space does nothing. | `W3#5 code chords`, `W3#5 preamble`, `W3#5 copy code retry2` | Space activation |
| w3_content_at_checklist.md#6 | Verified | "Embedded block from recipes/Whipped cream.md." / "Embedded note: …"; Say All reads header, "Jump to source" and body in order; Jump to source opens Whipped cream at the step ("wikilink Whipped cream.md."). | `W3#6 embeds reading 2`, `W3#6 say all embeds`, `W3#6 jump activate` | Block id read aloud |
| w4_panels_at_checklist.md#1 | Finding | Tested on the Bases grid (reading tables are document tables, not the W4-1 grid). Cells read with row/column identity; "Sorted by Extension, ascending/descending"; the Shift+F10 row menu (Open … Edit property) returns to the cell on Escape. Rows are unnamed, and Tab walks cells. | `W4#1 grid arrows sort`, `W4#1 row actions` | F3, Tab per cell |
| w4_panels_at_checklist.md#2 | Verified | "Outline panel." / "Level 2 heading: Ingredients" → Enter: "Scrolled to Ingredients."; Outgoing links: "Link to Apple pie.md, Opens the linked note." → "Opened Apple pie.md."; Backlinks rows are named with context. The Embeds leaf was not opened. | `W4#2 outline 2`, `W4#2 outgoing content` | Partial: Embeds leaf |
| w4_panels_at_checklist.md#3 | Finding | Rows read "Open. Submit grant. Due 2026-06-01. Priority high. Repeats every year. Open task."; Space: "Task completed." and the file shows `[x]`; review: "Tasks review. All." and "Filter set to Overdue." / "Overdue, 1 task". Focus goes to containers, and arrows from the review radios jump to the menu. | `W4#3 toggle`, `W4#3 review walk`, `W4#3 review overdue`, `F7 clean repro` | F4, focus loss |
| w4_panels_at_checklist.md#4 | Finding | "Property status, text, editable, edit, in-progress"; Enter: "Property status updated."; Escape: "Reverted changes to status."; Add property sheet fields read and "Property reviewer updated."; the rename sheet cancels back to its invoker. Rows are unnamed, commit loses focus, and the type combo reads a record dump. | `W4#4 edit value`, `W4#4 esc revert 3`, `W4#4 add prop commit`, `W4#4 rename sheet cancel` | F3, focus loss |
| w4_panels_at_checklist.md#5 | Finding | Citation rows: "Citation: Mack et al. 2021, page 42"; details field list is readable and close restores the row; bibliography segments, search and grid work. The summary sheet's content is never spoken, and Walk through citations returns to the editor. Files Citing was not executed. | `W4#5 citations`, `W4#5 citation details 2`, `W4#5 details close`, `W4#5 bibliography` | F8, F3, walk-through |
| w4_panels_at_checklist.md#6 | Finding | Quick filter "1 of 2 results"; "Base view as list."; Where am I: "Base: brief_example, view: My table"; diagnostics list item "arithmetic on incompatible operands evaluated to Null". Builder, dashboards and dock were not executed. | `W4#6 base walk`, `W4#6 quick filter`, `W4#6 view as list` | F3, float summary, focus to tab control, F4 |
| w4_panels_at_checklist.md#7 | Finding | "Today, 1 version, expanded"; "September 22, 2026 at 5:35 PM, snapshot, 2504 bytes"; Compare says "No changes." / "No differences."; the Deleted segment is silent. Restore As (a file dialog) was not executed. | `W4#7 history`, `W4#7 compare`, `W4#7 deleted` | Deleted segment |
| w4_panels_at_checklist.md#8 | Finding | "Sync panel."; "No sync systems detected."; the first stop is an unnamed "pane"; refresh is silent. No degraded fixture was available. | `W4#8 sync`, `W4#8 sync refresh` | Unnamed stop |
| w4_panels_at_checklist.md#9 | Verified | "Summary: 10000 rows, 3 columns."; PageDown to row 295; Ctrl+End to "row 10000"; Up; Ctrl+Home back to "row 1"; headers correct and no crash. Rows carry the fixture's record text (F3). | `W4#9 10k grid` | — |
| w5_commands_at_checklist.md#1 | Finding | "Selected: Toggle Right Pane"; Enter ran it ("Right pane hidden."); Escape restored the Sync row. Typing stalls about 2.5 s and counts are dropped or stale. | `W5#1 palette retry`, `W5#1 invoke + escape` | F11 |
| w5_commands_at_checklist.md#2 | Finding | "Search returned 2 results."; "Selected: Search bait.md"; Enter: "Opened Search bait.md, line 33: …the kestrel's nest…". No snippet is spoken while walking. | `W5#2 search`, `W5#2 activate` | Snippets |
| w5_commands_at_checklist.md#3 | Finding | "Template picker opened. 2 templates available."; rows read their first lines; "Created AT pass meeting.md from meeting-note." opens at the cursor; Escape cancels to the editor. Keystrokes leak and the second prompt is skipped. | `W5#3 templates`, `W5#3 prompts focus`, `W5#3 create` | F5, F3 |
| w5_commands_at_checklist.md#4 | Verified | "Renamed AT pass meeting.md to AT pass renamed.md."; "Duplicated … as AT pass renamed copy.md."; the Move picker ("Move AT pass renamed copy.md: loading destination folders. Escape to cancel.") cancels to its invoker, then "Moved … to work."; "Moved AT pass renamed.md to the Recycle Bin."; "Undid rename to AT pass renamed copy.md.". | `W5#4 rename commit`, `W5#4 duplicate`, `W5#4 move commit`, `W5#4 delete`, `W5#4 undo rename` | Delete not undoable in-app; F2 from editor inert; repeated "1 destination." |
| w5_commands_at_checklist.md#5 | Verified | All six menus read label, access key, accelerator and position ("Toggle Right Pane, P, Ctrl+Alt+I, 17 of 22"); the editor context menu reads "Activate at Cursor, A, Ctrl+Enter". | `W5#5 …`, `W2 editor context menu` | Repeated access keys; menu name on Left/Right |
| w5_commands_at_checklist.md#6 | Finding | Item Names carry the spoken chord ("Toggle Right Pane, Control Alt I"), but walking speaks only "Selected: Toggle Right Pane". | `W5#1 palette`, UIA tree 18:10 | Chord not heard |
| w5_commands_at_checklist.md#7 | Verified | Quick Open help reads the new-tab and both split instructions, and Ctrl+Enter / Ctrl+Alt+Enter did what they say. Reading help reads H/K/L/T chords that work (L is taken on this machine; U works). Ctrl+Alt+Shift+Enter was not tried. | `W1#7 Quick Open`, `W5#7 QO split`, `W3#1 reading chords` | — |
| w5_commands_at_checklist.md#8 | Pending | Checked the check box ("1 item selected"), then Batch Add tag gave "Tagged 1 file with #agenttest."; Move checked was not run; F2 rename covered in #4. Folder-duplicate refusal, shortcuts, template-in-folder and folder notes were not executed. | `W5#8 batch check`, `W5#8 add tag` | "Add"/"Remove" names; `#` stored in frontmatter |
| w5_commands_at_checklist.md#9 | Finding | The Applications key opens the card menu; Delete: "Deleted File card "canvas research" — Ctrl+Z to undo"; undo from the outline: "Undid: delete "canvas research"". Shift+F10 opens the tab menu, and focus is lost after Delete. Edit Connection was not executed. | `W5#9 apps key`, `W5#9 delete undo`, `W5#9 undo in outline` | F13, focus loss |
| w5_commands_at_checklist.md#10 | Verified | Node menu: Open, Open in New Tab, Show connections, Reveal in File Tree, Pin → "Pinned."; the menu then shows "Unpin" → "Unpinned.". | `W5#10 graph node menu`, `W5#10 pin unpin` | Node not named in the announcement |
| w6_1_canvas_at_checklist.md#1 | Finding | "Text card "Core question", expanded, 1 of 4"; "Connects to Text card "Evidence so far", labelled "supports""; "Connected from …"; "Linked with …"; "End of canvas." Colour names are never spoken. | `W6-1#1 outline walk`, `W6-1#1 outline walk 2` | Colour only in ItemStatus |
| w6_1_canvas_at_checklist.md#2 | Finding | Headers Type, Title, Group, Target, Connections, Color; "Sorted by Type, descending". Rows are record dumps, and the first sort was silent. | `W6-1#2 table`, `W6-1#2 sort` | F3 |
| w6_1_canvas_at_checklist.md#3 | Pending | "Canvas visual view, grouping, Zoom 100 percent"; object navigation over card peers ("Research, button", "Core question, button, selected"); NVDA+Enter invoke selects and speaks. The edge pan was not exercised (no visual check). | `W6-1#3 visual`, `W6-1#3 escape + objnav`, `W6-1#3 invoke card` | Edge pan pending |
| w6_1_canvas_at_checklist.md#4 | Finding | Outline Down/Up follow the tree; the visual navigator is broken. | `W6-1#4 navigator` | F12 |
| w6_1_canvas_at_checklist.md#5 | Pending | Header stops read (filter help, "Canvas view" radios); the grid walks every cell with Tab; move mode fences keys. Card editor, prompt and picker sheets were not opened. | `W6-1#5 header tab`, `W6-1#5 grid exit` | Tab per cell |
| w6_1_canvas_at_checklist.md#6 | Pending | Voice Access: human only. | — | — |
| w6_1_canvas_at_checklist.md#7 | Verified | Keyboard as switch: "Move mode — "canvas research". Arrows to move, …"; nudges "Below "Core question", left of "interaction › Announcement grammar". Overlapping another card"; "Placed …"; "Move cancelled — back at …". Visible Commit/Cancel controls were not checked visually. | `W6-1#7 move mode` | — |
| w6_1_canvas_at_checklist.md#8 | Pending | Braille owner-deferred. The UIA values it would read are present: status 'marked', Value 'Zoom 100 percent', and the filter Value. "Marked "canvas research". 1 marked." was heard. | `W6-1#8 mark` | — |
| w6_1_canvas_at_checklist.md#9 | Pending | Text scaling is a system setting, which the agent may not change. | — | — |
| w6_1_canvas_at_checklist.md#10 | Pending | Contrast themes are a system setting. | — | — |
| w6_1_canvas_at_checklist.md#11 | Pending | Reduce Motion is a system setting. | — | — |
| w6_2_graph_at_checklist.md#1 | Verified | Headers Note, Links in, Links out, Embeds in, Embeds out, Component, Modified, Folder, Kind; "Sorted by Note, ascending" / "descending", "74 of 74 shown", with the selection kept. | `W6-2#1 open graph`, `W6-2#1 sort` | "1 references" |
| w6_2_graph_at_checklist.md#2 | Finding | "Connections panel."; "Linked from, 9 notes, expanded"; rows "Bravo relay, 3 links in, 2 links out, Opens the note."; Enter opened the note in the Graph tab. Depth control and Create note were not executed. | `W6-2#2 show connections`, `W6-2#2 walk reroot back` | F14 |
| w6_2_graph_at_checklist.md#3 | Pending | "Diagram mode." then "Graph, visual diagram, grouping, Zoom 100 percent"; Tab walks nodes; zoom is announced. Edge pan and the 1,501-note tier B were not executed. | `W6-2#3 to diagram` | Zoom spoken twice |
| w6_2_graph_at_checklist.md#4 | Verified | Diagram arrows step spatially ("Fresh mozzarella, 0 links in, 0 links out"); the letter b jumps to "Bad diagram"; table Down/Up and leaf tree arrows work. | `W6-2#4 diagram arrows` | — |
| w6_2_graph_at_checklist.md#5 | Pending | The switcher reads; Where am I: "Bad diagram, 0 links in, 0 links out, component 0, zoom 100 percent, filters: unresolved shown."; Escape returns to the diagram. The diagram keeps Tab (by design), and the header, inspector and leaf controls were not walked. | `W6-2#4 diagram arrows`, `W6-2#5 header tab` | — |
| w6_2_graph_at_checklist.md#6 | Pending | Human only. | — | — |
| w6_2_graph_at_checklist.md#7 | Pending | Not executed. | — | — |
| w6_2_graph_at_checklist.md#8 | Pending | Braille owner-deferred. | — | — |
| w6_2_graph_at_checklist.md#9 | Pending | System setting. | — | — |
| w6_2_graph_at_checklist.md#10 | Pending | System setting. | — | — |
| w6_2_graph_at_checklist.md#11 | Pending | System setting. | — | — |
| w7_2_notification_etiquette_checklist.md#1 | Pending | Sidebar filter counts are heard in full ("File list, 2 items", "File list, 20 items"). Three states while speech was still active were not staged. | `W1#4 filter`, `W1#4 tag filter inline` | F1 blocks this after launch |
| w7_2_notification_etiquette_checklist.md#2 | Pending | "This command is not available right now." is raised as ImportantMostRecent and spoken. Interrupting a long polite line was not staged. | `W7-2#2 urgent` | — |
| w7_2_notification_etiquette_checklist.md#3 | Pending | Not executed. | — | — |
| w7_2_notification_etiquette_checklist.md#4 | Finding | A launch scan is silent (F1). An open after launch speaks start and "Scan complete. 2000 files indexed." (under 2 s, so no progress lines). | `W7-2#4 scan fresh 2000 go` | F1 |
| w7_2_notification_etiquette_checklist.md#5 | Finding | Palette: a stale "87 commands matching "b"" is spoken before "2 commands matching "bibliography""; the Move picker says "1 destination." ×3. | `W7-2#2 urgent`, `W5#4 move commit` | F11 |
| w7_2_notification_etiquette_checklist.md#6 | Verified | "78 recent files" once; one "1 file matching "editor_semantics"" after the burst. | `W2#6 open fixture + line read` | — |
| w7_2_notification_etiquette_checklist.md#7 | Pending | Not executed. | — | — |
| w7_2_notification_etiquette_checklist.md#8 | Pending | Not executed. | — | — |
| w7_2_notification_etiquette_checklist.md#9 | Finding | Blocked once, identifies the note, keeps the edits, doesn't overwrite, but reads out content hashes. Save All and save-before-close were not run. | `W7-2#9 save` | F6 |
| w7_2_notification_etiquette_checklist.md#10 | Finding | The missing file keeps its tab, named "…, missing from disk", but the spoken line is the generic " AT pass renamed copy, tab 5 of 5.". Unreadable-file reopen was not executed. | `W7-2#10 reopen missing` | Generic line; blank Saved editor via Quick Open |
| w7_2_notification_etiquette_checklist.md#11 | Pending | Not executed. | — | — |
| w7_2_notification_etiquette_checklist.md#12 | Finding | The notice reads "Sidebar settings are malformed and are read-only."; Retry is reachable by Tab; "Sidebar settings still use defaults. Sidebar settings are malformed and are read-only."; after repair, "Sidebar settings reloaded." and focus is lost. The stale-references variant was not executed. | `W7-2#12 retry` | Focus loss |

## Matrix rows (NVDA column) — agent disposition

| Matrix surface | Agent result | Basis |
|---|---|---|
| Main window and menu bar | Finding | F4, repeated access keys, menu name on Left/Right; menus and accelerators otherwise read correctly |
| Welcome / Open Vault | Verified | W1 #1 (welcome, "Open vault folder" dialog) |
| Recent vaults | Finding | F3 (record dump names) |
| Vault scan status | Finding | F1, W1 #2 |
| Files tree | Finding | F2 |
| File filter/results | Finding | F3, F7 |
| Tag tree | Finding | F7 (tree reads well; activation filters to 0) |
| Sidebar organization and actions | Finding | "NameAscending", F2 inert from the editor, "Add"/"Remove" names |
| Workspace tabs | Finding | Properties landing, last-tab focus loss, "Close briefexample" |
| Split workspace | Finding | F3 (pane items), handle not in the Tab route |
| Editor document | Verified | W2 #1, #6, #7, #8 |
| Editor semantic menu and popover | Finding | F8, preview content, the menu itself reads correctly |
| Right-pane leaf registry | Finding | Names and "n of 17" are fine; F4 from the list container; unnamed Sync stop |
| Reading view (W3-1) | Finding | F10, table-cell link |
| Code blocks (W3-4) | Finding | Space activation |
| Math blocks (W3-2) | Verified | W3 #2, #3 |
| Diagram blocks (W3-3) | Finding | Bare "grouping" in caret reading |
| Embed cards (W3-5) | Verified | W3 #6 |
| Data grids (W4-1 substrate) | Finding | F3 row names, Tab per cell; 10k virtualization verified |
| Link & structure panels (W4-2) | Verified | W4 #2 (Embeds leaf not opened) |
| Tasks panel + review flow (W4-3) | Finding | Focus loss, F4 |
| Properties header + sheets (W4-4) | Finding | F3, commit focus loss |
| Citation surfaces (W4-5) | Finding | F8, F3 |
| Bases surfaces (W4-6) | Finding | F3, float summary |
| History panel (W4-7) | Finding | Silent Deleted segment |
| Sync diagnostics (W4-8) | Finding | Unnamed stop, silent refresh |
| Quick Open | Verified | W1 #7 |
| Command palette (W5-1) | Finding | F11, chords not heard |
| Search overlay (W5-2) | Finding | Snippets not heard while walking |
| Templates (W5-3) | Finding | F5 |
| File management (W5-4) | Verified | W5 #4 |
| Canvas outline (W6-1 PR A) | Finding | Colour names |
| Canvas table (W6-1 PR B) | Finding | F3 |
| Graph table (W6-2 PR A) | Verified | W6-2 #1 |
| Graph navigator, filter and Where-am-I (W6-2 PR C) | Pending | Where am I and arrows verified; filter field not exercised |
| Graph diagram (W6-2 PR D) | Pending | Tab/arrows/zoom verified; edge pan and tier B not exercised |
| Graph inspector (W6-2 PR E) | Pending | Not exercised in this run; the W7-5 human finding stands |
| Graph connections leaf (W6-2 PR B) | Finding | F14 |
| Canvas navigator, filter and Where-am-I (W6-1 PR C) | Finding | F12 |
| Canvas visual (W6-1 §D) | Pending | Peers, invoke and value verified; edge pan not exercised |
| Canvas card editor (W6-1 §E) | Pending | Not executed |
| Canvas prompt sheets (W6-1 §F, §G, §G2) | Pending | Not executed |
| Canvas pickers (W6-1 §E, §F, §G2) | Pending | Not executed |
| Canvas context menus and row actions (W6-1 §E, §G2) | Finding | F13, focus loss after Delete |

## Not covered by this run

- JAWS and Narrator (NVDA only was requested).
- Braille (owner-deferred).
- IME.
- Voice Access and switch hardware.
- Text scaling, Contrast themes and Reduce Motion: system settings the agent may not change.
- Anything that needs a visual judgement, since screen capture was declined.
- The unexecuted parts named row by row above.

## Side effects left on the machine

- The disposable vaults live in the session scratchpad.
- One disposable note, `AT pass renamed.md`, is in the Recycle Bin.
- Slate's Recent Vaults list now holds the scratch vaults.
- The Math speech style was restored to ClearSpeak.
- The Slate window's size and position were restored.
- NVDA's settings were not changed.
