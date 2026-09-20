# NVDA field verification record — W7-5 shell pass, 2026-09-20

Human NVDA pass over the Windows shell on the demo vault. Six findings, all
reproduced against the speech log and the source; fixes tracked under
[W7-5 (#1239)](https://github.com/coryj627/slate/issues/1239), with F6
region cycling split out as a new expectation under
[W7-6 (#1240)](https://github.com/coryj627/slate/issues/1240).

**Tester:** Cory Joseph
**AT:** NVDA 2026.2 (stock settings; log level Debug; synth oneCore; no braille display; laptop keyboard layout)
**OS:** Windows 11 Pro 25H2, build 10.0.26200.9457
**Build:** `main` at `6b5cf96` (chore(windows): build the app with zero warnings, #1238), Release executable, dark app theme
**Corpus:** `C:\dev\slate\demo-vault` (90 files indexed; no tabs restored at launch)
**Method:** keyboard only (Tab, Shift+Tab, arrows, Alt+letter, F6, Shift+F6, Ctrl+F6); NVDA speech log as transcript; two screenshots for the visual findings
**Run date:** 2026-09-20, 12:09–12:27 local (America/New_York)
**Evidence reference:** [w7_5_nvda_field_pass_2026-09-20_nvda_speech.txt](w7_5_nvda_field_pass_2026-09-20_nvda_speech.txt) (Slate slice of `nvda.log`; the full 133 MB log and the screenshots are kept in the tester's `slate-testing/2026-09-20-1220` folder)

| Matrix surface / checklist item | Result | Heard/observed behavior | Transcript reference | Finding / retest |
|---|---|---|---|---|
| Welcome / Open Vault | Finding | On window activation with no tabs restored: "Slate window", "Workspace tabs tab control". Down arrow then spoke "Canvas collapsed Alt+N 5 of 7" — focus had wandered from the empty tab control into the menu bar. | [speech log](w7_5_nvda_field_pass_2026-09-20_nvda_speech.txt) 12:09:45.915; 12:09:59.180; 12:11:48.385–12:11:51.604 | Launch focus lands on the Files tree when no tab is open; the empty tab control is not focusable. Fix: [W7-5 (#1239)](https://github.com/coryj627/slate/issues/1239). |
| Main window and menu bar | Finding | Alt+F: "File collapsed Alt+F 1 of 7" with no flyout; Down arrow was needed to open it. Alt+W, Alt+B, Alt+E and Alt+G each opened their menu and spoke the first enabled item. Walking the bar with Right arrow ended on "Files collapsed Alt+F 7 of 7", a second top-level menu on the same access key. In the dark theme the Editor menu's items that took keyboard focus were drawn dim and the skipped ones bright (items 1, 2 and 4 of 15 visited, 3 "Toggle Reading Mode" skipped as disabled; no "unavailable" heard), and "Activate at Cursor" and "Preview Embed" were enabled with no file open. | [speech log](w7_5_nvda_field_pass_2026-09-20_nvda_speech.txt) 12:12:07.319–12:12:08.888; 12:12:44.741–12:12:51.499; 12:13:04.884; 12:22:48.900–12:23:30.597; screenshot `Screenshot 2026-09-20 122344.png` | Files is now a "Files Sidebar" submenu of File; every top-level menu has a unique mnemonic and an AutomationId (MenuBarCensus). Enabled menu text was drawn in a fixed dark grey regardless of theme (MenuItemForegroundTests reproduces it); the two items resolved to a null command with no tab. Fix: [W7-5 (#1239)](https://github.com/coryj627/slate/issues/1239). |
| `w1_shell_at_checklist.md#1` | Finding | Window, menu and button names were spoken, but Alt+F did not open File and the launch focus sat on an empty, unnamed-state tab control (see the two rows above). | [speech log](w7_5_nvda_field_pass_2026-09-20_nvda_speech.txt) 12:09:45.915; 12:12:07.319–12:12:08.888 | Retest after [W7-5 (#1239)](https://github.com/coryj627/slate/issues/1239) lands. |
| Right pane rail | Finding | From "Right pane panels list, Graph inspector 5 of 17", Tab spoke "File collapsed Alt+F 1 of 7"; further Tabs cycled Workspace, Base, Editor, Canvas, Graph, Files and back to File without ever leaving the menu bar. Escape returned to the rail. Repeated five times. | [speech log](w7_5_nvda_field_pass_2026-09-20_nvda_speech.txt) 12:17:20.692–12:18:05.417; 12:22:23.704–12:22:31.733 | WPF's Menu defaults Tab navigation to Cycle; the bar now sets TabNavigation=None and Focusable=False, so Tab never enters it and it is reached by Alt, Alt+letter and F6. Fix: [W7-5 (#1239)](https://github.com/coryj627/slate/issues/1239). |
| Landmarks | Finding | F6, Shift+F6 and Ctrl+F6 produced no focus change and no speech on four attempts. | [speech log](w7_5_nvda_field_pass_2026-09-20_nvda_speech.txt) 12:13:15.933; 12:13:18.815; 12:17:33.742; 12:22:43.593–12:22:46.295 | No F6 binding exists (`focusNextPane`/`focusPreviousPane` are chordless). New expectation: [W7-6 (#1240)](https://github.com/coryj627/slate/issues/1240). |
| Graph inspector (W6-2 PR E) | Finding | Visual: with the inspector leaf shown, the placeholder heading "Graph inspector" and "This panel is docked and ready for its feature surface." painted over the inspector's own "Open the graph to change these settings." and "Filters" heading (screenshot `graph-inspector-side-pane-layout.png`); the leaf itself was announced as "Graph inspector panel." | [speech log](w7_5_nvda_field_pass_2026-09-20_nvda_speech.txt) 12:17:15.685 | The placeholder now collapses for the inspector leaf (RightPaneLeafBodyCensus). Fix: [W7-5 (#1239)](https://github.com/coryj627/slate/issues/1239). |
| Files tree, tabs and graph tab | Verified | Selecting a file spoke "Selected: Golf gateway.md", "Workspace tabs tab control", "Golf gateway tab selected 1 of 1", "Golf gateway.md editor document heading level 1 # Golf gateway". Opening the graph spoke the tab, the data grid and the first row with its link counts; Diagram mode announced "Zoom 100 percent". | [speech log](w7_5_nvda_field_pass_2026-09-20_nvda_speech.txt) 12:26:38.686–12:26:55.271; 12:27:04.465–12:27:12.955 | None |

Unexecuted in this run: checklist rows 2 (scan progress was heard once,
"Scan finished: 90 files indexed", but not timed), 4, 5, 6 and 7 of
`w1_shell_at_checklist.md`; every W2–W6 checklist. They stay Pending. The
"Files tree, tabs and graph tab" row is an observation, not a certification
of any matrix surface: no matrix cell links to it.
