# W7-6 production NVDA run — F6 / Shift+F6 shell region cycling, 2026-09-20

An agent-operated production run, separate from the owner's human NVDA field
pass ([2026-09-20 record](w7_5_nvda_field_pass_2026-09-20.md)) and from any
licensed JAWS sign-off. It does not fill a human checklist cell; it is cited
from the checklist as production evidence, the way the W7-1 run is cited from
the W2 checklist.

**Tester:** Claude (agent), keystrokes injected by the FlaUI journeys and by `SendKeys`; no screen control
**AT:** NVDA 2026.2 AMD64, OneCore synthesizer, laptop keyboard layout, stock settings, log level Debug, started by the agent from a shell and quit with `nvda.exe -q`
**OS:** Windows 11 Pro 25H2, build 10.0.26200.9457
**Build:** branch `w7-6-f6-region-cycling` at `bee1ede` (stacked on W7-5 `bc6b75a`, PR #1241), Release executable
**Corpus:** the journeys' disposable vaults (`alpha.md`; `note.md` + `Folder/child.md`) and, for the SendKeys passes, `C:\dev\slate\demo-vault` with its restored workspace (two tabs, Graph tab active, right pane on Math)
**Method:** three FlaUI journeys run with NVDA listening (`ShellRegions_F6CyclesForwardAndShiftF6Back`, `SpokenChords_MenusPaletteAndOverlays_MatchTheTable`, `FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean`), then two SendKeys passes (F6 ×4, Shift+F6, Ctrl+Alt+Right/Left) on the demo vault; NVDA's speech log as the transcript
**Run date:** 2026-09-20, 15:48–15:57 local (America/New_York)
**Evidence reference:** [w7_6_nvda_production_run_2026-09-20_nvda_speech.txt](w7_6_nvda_production_run_2026-09-20_nvda_speech.txt) (journey window); the two SendKeys passes are quoted below from the same NVDA session family
**Audio:** OneCore initialised; audibility not confirmed by a human.

| Check | Observed result | Evidence / limits |
|---|---|---|
| F6 ring, no tab open (journey) | From "Files tree view": F6 → "Editor workspace" (the empty editor landmark) → "Outline list" (the leaf's first stop) → "Right pane panels list, Outline 1 of 17" → "Status bar, status bar, F6 moves to the next region: menu bar, files, tab bar, editor, right pane, status bar. Shift F6 moves back." → "File collapsed Alt+F 1 of 6" → "Files tree view". Seven landings, wrap included. | transcript 15:48:15.252–15:48:16.747 |
| F6 ring with a note open, and Shift+F6 back (journey) | F6 → "Workspace tabs tab control, alpha tab selected 1 of 1" → "alpha.md editor document heading level 1 # Alpha"; Shift+F6 → "alpha tab selected 1 of 1" → "Files tree view, level 1 alpha.md file 1 of 1" → "File collapsed Alt+F 1 of 6"; Escape returned to the tree. | transcript 15:48:17.389–15:48:20.306 |
| Hidden right pane skipped (journey) | After Ctrl+Alt+I, F6 from the editor → "Status bar …" directly. | transcript 15:48:20.790–15:48:21.452 |
| F6 from the menu bar in menu mode | "File collapsed" → F6 → "Files tree view" (the `MainMenu_PreviewKeyDown` hand-off). | transcript 15:48:16.475–15:48:16.747 |
| Launch focus, no restored tabs (W7-5 retest, fix `bc6b75a`) | "Slate window", "Files tree view" on activation; the empty tab control took no focus. | transcript 15:48:48.986–15:48:48.999 |
| Launch focus, restored tabs (W7-5, SendKeys pass) | "Workspace tabs tab control, Graph tab selected 2 of 2, Graph, visual diagram grouping, Zoom 13 percent": the active tab's surface, as specified. First F6 then went to "Right pane panels list, Math 7 of 17" (the Math leaf has no focusable stop, so the content region was skipped), then "Status bar …", "File collapsed Alt+F 1 of 6", "Files tree view"; Shift+F6 → "File collapsed". | second session 15:55:33.062–15:55:45.129 |
| Menu bar is six menus (W7-5 retest) | Every menu-bar landing reads "1 of 6"; the seventh "Files" menu is gone. Alt+F itself was not pressed in this run. | transcript, all "File collapsed" lines |
| Typed region announcements ("Menu bar.", "Editor pane. Empty.", "Right pane panels.", "Status bar. …") | **Not received.** NVDA's log shows no UIA notification event during any F6 landing. In the same session family the pre-existing Ctrl+Alt+Right announcement ("Math panel.") was equally absent (third session, 15:57:08), while the palette's "Selected: …" notifications in a different instance were received (15:48:33). Every landing was still spoken through focus. | NVDA log: zero `HandleNotificationEvent` entries in the F6 windows; see the ruling in the W7-6 ledger. Human retest of `w1_shell_at_checklist.md#8` decides whether the typed lines are audible in production. |
| Shell gate journey with NVDA listening | Passed the launch-focus assertion, then failed at its caret-movement step; the same failure reproduces on unmodified `main` with a screen reader running. | `FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean`, 15:48:45–15:49:04 |

## Bounded transcript, SendKeys pass on the demo vault (15:55)

```
15:55:33.062 | Speaking ['Workspace tabs', 'tab control']
15:55:33.072 | Speaking ['Graph', 'tab', 'selected', '2 of 2']
15:55:33.080 | Speaking ['Graph, visual diagram', 'grouping', 'Zoom 13 percent']
15:55:41.335 | Input: kb(laptop):f6
15:55:41.385 | Speaking ['Right pane panels', 'list']
15:55:41.397 | Speaking ['Math', '7 of 17']
15:55:42.277 | Input: kb(laptop):f6
15:55:42.304 | Speaking ['Status bar', 'status bar', 'F6 moves to the next region: menu bar, files, tab bar, editor, right pane, status bar. Shift F6 moves back.']
15:55:43.215 | Input: kb(laptop):f6
15:55:43.245 | Speaking ['File', 'collapsed', 'Alt+', 'F', '1 of 6']
15:55:44.154 | Input: kb(laptop):f6
15:55:44.189 | Speaking ['Files', 'tree view']
15:55:45.103 | Input: kb(laptop):shift+f6
15:55:45.129 | Speaking ['File', 'collapsed', 'Alt+', 'F', '1 of 6']
```

Pending for the owner's human pass: audibility of the typed region lines,
`w1_shell_at_checklist.md#8` as a whole, Alt+F and Tab-from-the-rail under
W7-5, JAWS. NVDA reads the status bar as "Status bar, status bar" followed
by the whole F6 help sentence on every landing (Name duplicates the control
type; HelpText is verbose): the owner decides whether D-5/§2 keep that.
