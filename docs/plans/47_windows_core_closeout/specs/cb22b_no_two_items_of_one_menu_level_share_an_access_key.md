# CB-22b: no two items of one menu level share an access key, and a name with an underscore shows whole

Issues: none closed (found by CB-22's census; Refs #1955, comment 7066667520, whose line 1 this chunk ends). Size S as planned; M with the second underscore member (Call 5). Lanes: windows (the app job's unit facts; no gate journey). AT: NVDA spot check on base and head (the changed keys, a repeated-key sequence, and the two underscore members). Wave: C10, after CB-17b (both edit `MainWindow.xaml`). Decision: the owner's letters, as one table (Call 1), put before any letter changes. Review: an Opus red team on this spec (done, below) and on the diff, then Codex adversarial-review rounds until it approves, then the lead's merge judge.

Plan row, verbatim: "No two items of one menu level share an access key: an access-key sweep ends the sixteen dated rows of `MenuAccessKeyCensus`, with the letters put to the owner as one table before any of them changes; a folder name with an underscore shows whole in the note header's Folders menu, where a folder `my_notes` shows "mynotes" today."

Every `path:line` below is as of `main` at 38f6edde, read on 2026-11-23. `S/` is `apps/slate-windows/src/SlateWindows/`, `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`, `MW` is `S/MainWindow.xaml`. The step 0 reports, probes, logs and NVDA transcripts are in the principal's working notes, outside the repository, not committed.

## The owner's answers and the lead's rulings (2026-11-23), binding on the build

The owner's answers, verbatim:

- Call 1: "Approve the table (Recommended)". All 21 changes are as tabled. The PR body names every changed key and the single-press and typed-sequence cost.
- Call 2: "A' (Recommended)". Reopen Closed Tab, Split Down, Grow Pane and Shrink Pane carry no key, and Show Tasks Review takes K.

The lead's rulings, all as recommended:

3. Focus Filter keeps F, and Forward takes W.
4. The waiver mechanism goes entirely, and the 37 ruled letters are pinned.
5. Both class 1 members are fixed here with `TextBlock` headers, and `EmbedPreviewTitleCensus` changes as trialled. The size moves to M.
6. No underscore census.
7. "Jump to source" is fixed here; the rest of class 2 is filed from the draft as one issue.
8. The Folders rows carry no access key.
9. The silent-commands issue is filed from the draft.
10. The hosted menu facts skip on #1680 desktops.

The calls below are kept as they were put.

## Calls for the lead

Calls 1 and 2 are for the owner. The others are the lead's.

1. **The owner's letters (for the owner, as one table).** The table is in "The owner's table" below: every item of the six menu levels that hold a shared key, its letter today, the letter proposed and why. The rules, in order:
   - (a) Where a shared key today runs one item at once in the state a reader usually meets (step 0), that item keeps it, so a learned key keeps doing what it did.
   - (b) A Windows standard access key (Microsoft's table) where the item's label begins with the table's command and no sibling beginning with the same command holds that letter: Close C (Close Vault, Close Tab), New N (New Canvas), Open O (Open Graph). Copy C is the one place it is not applied: Copy Link taking C would push Sort by Column onto its only other free letter, the descender y.
   - (c) The same command in two menus takes the same letter where it is free there (Zoom Out T, as in the Editor menu since CB-22).
   - (d) The first letter of the first or second word; else a distinctive consonant or vowel early in the label; a wide letter over i and l, and no descender (g, j, p, q, y) where there is a choice.
   - (e) Only in the Workspace menu (Call 2): the least used items give up their key, as Microsoft's guidance orders.

   21 items change: 17 take a new letter and 4 lose theirs (with Call 2 as recommended). Recommended: as tabled.

   Cost, for the owner. Every changed key, and every kept key that was shared, now runs its item at once on its first press (step 0: a shared key only moved the keys while two carriers were on). The column "What the key does now" names each. Three costs come with it:
   - The sharpest single presses: Alt+F, C closes the vault at once, where today it moves to New Canvas; Alt+W, C closes the tab at once; Alt+W, D duplicates the tab at once.
   - A learned sequence that repeated a shared key now runs the first item and sends the rest of the sequence where the keys land. On `main`, a reader reached a later carrier by pressing the letter again and then Enter. Now Alt+W, R, R, R, R, Enter (Show Tasks Review on `main`) splits the pane and types "rrr" and a line break into the new pane's note; Alt+W, C, C, Enter closes the tab and types "c" and a line break into the next tab's note; Alt+F, C, C, Enter closes the vault and sends "c" and Enter to the welcome view. The levels where a reader reached a carrier by repeating the key: File C, Files Sidebar S, R, C and F (in an import or with forward history), Workspace D, C, R, P, G, S and H, Base C and K, Canvas T, Graph O (Diagram mode). The NVDA pass records one such sequence on head (step 4).
   - Three items now run on a letter of their own with no sentence: Collapse All (C, as today), Refresh Sync Diagnostics (S, as today with one pane), and Clear Recents (N, new), which also cannot be undone (`S/FilesSidebarViewModel.cs:2623-2629` clears the store and sets only a status line). Call 9.

   CB-22 made the same single-press trade for the Editor menu's A and O, and the lead accepted it with a line for the owner.
2. **The Workspace menu has more items than letters (for the owner).** It has 28 items; 26 carry a key today and 2 (Move Tab Left and Right) carry none. Its labels hold 23 different letters (none holds J, Q or Z), so at least 3 keyed items must lose their key. Microsoft's guidance for menus asks for keys on all items and unique keys within a level ("Assign access keys to all menu items. No exceptions." and "Assign unique access keys within a menu level."); 28 items with 23 letters cannot meet both. For controls that cannot all have unique keys it says to start with the least frequently used and leave "the remaining controls ... [to] Tab and arrow key navigation". The options, each with the keyless items it leaves (Move Tab Left and Right are keyless in all):
   - (A') Recommended: Reopen Closed Tab, Split Down, Grow Pane and Shrink Pane lose their key; Show Tasks Review takes K ("Show Tas_ks Review"). Grow and Shrink are pane sizing, with four other sizing items in the menu and chords of their own; Reopen and Split Down have chords and no free letter. Cost: one key fewer than B'.
   - (A) Reopen Closed Tab, Split Down, Grow Pane and Show Tasks Review lose their key; Shrink Pane takes K. Same count as A'; Tasks Review, a view a reader opens, loses its key to a sizing command.
   - (B') The most keys, 23: Reopen Closed Tab, Split Down and Grow Pane lose theirs; Show History Panel takes Y ("Show Histor_y Panel", a descender late in the word), Shrink Pane takes H ("S_hrink Pane") and Show Tasks Review K. Cost: History's natural H goes to Shrink, and Y is hard to find by eye.
   - (B) Dominated by B' and not recommended: Refresh Sync Diagnostics takes Y. It also breaks Alt+W, S, which runs Refresh Sync Diagnostics today with one pane, and contract 27's SD7, which names the item "Refresh _Sync Diagnostics" (`docs/plans/27_sync_diagnostics_contracts.md:85`).
   - (C) Restructure the menu into submenus (for example a Focus submenu with its six Focus items), so every item has a key. Cost: an owner design call, every reader's arrow positions in the menu change ("x of 28"), and more tests; outside this row.
3. **Rule (a) above rule (b).** They disagree once: in the Files Sidebar menu F runs Focus Filter today, and Microsoft's table gives Forward the F. Recommended: Focus Filter keeps F by rule (a); Forward takes W ("For_ward"). Alternative: Forward F, Focus Filter L ("Focus Fi_lter", a narrow letter, the only free one in its label). Cost either way: one item moves.
4. **The census's waiver.** With no shared key left, `MenuAccessKeyCensus.Exemptions` is empty. (A) Remove the `Exemption` record, the array and its two checks, and the `Covers` asserts of the planted fact, so a shared key can never be waived without a reviewed change that brings the mechanism back. (B) Keep the mechanism with an empty array. Recommended: A. Cost: about 60 lines of test code go.
5. **The underscore class has two members, not one, and both are fixed here.** Step 0 (worker B) measured the class: a string a user supplies reaching a presenter that recognises access keys. Members: the Folders menu's rows (`S/NoteHeaderView.cs:384`), and the embed cards' headers in the Embeds leaf and the Ctrl+E popover (`S/EditorEmbedPreview.cs:79`, Fluent's `Expander` template). The second is the sharper: while the Embeds leaf shows a card titled "Embedded note: my_notes.md", the window's source registers N, so Alt+N no longer opens the Canvas menu (it moves the keys into the right pane when two cards hold N, or collapses the one card that does), and an embed of `a_b_c.md` takes Alt+B from the Base menu. Recommended: both sites build their header as a `TextBlock`, which draws the name whole, registers no key, and leaves the peer's name as set (the spec red team simulated it on the shown shell: painted and named whole, nothing registered, Alt+B and Alt+N open their menus, foreground inherited at rest, highlighted and disabled, font and row height unchanged). Cost: the chunk is M; it edits `S/EditorEmbedPreview.cs` and contract 38's provenance census `T/Censuses/EmbedPreviewTitleCensus*.cs`, whose renderer rule must admit a `TextBlock` header that carries a card's Title alone (red-team finding 1); the DS-2 title wrap (`S/EditorEmbedPreview.cs:92-105`), which misses an underscore title today, is set on the `TextBlock` directly and its tree walk goes.
6. **No census for the underscore class.** A source census over C# `Header =` cannot prove the class: 31 of 38 such writes under `S/` are not literals, five of them carry a user's text into the peer-name class (Call 7) where a `TextBlock` cannot go, the scrape also meets non-WPF members (`S/SidebarWriteJournal.cs:636`, a `byte[]`), and it misses `Content =`, XAML bindings and templates (red-team finding 2). (A) A Roslyn census by type with a XAML half, as `EmbedPreviewTitleCensus` is built, with about 14 rows and its ceiling written ("reads writes of Header and Content on these types; does not follow a string to its source"). (B) No census: the two members' facts, and an underscore title in the DS-2 wrap fact. Recommended: B. Cost: a later data header can bring the class back unseen; A costs more than the chunk.
7. **The peer-name class is filed, not fixed, except one member here.** Step 0 found a second mechanism: WPF's `ButtonBaseAutomationPeer` and `MenuItemAutomationPeer` drop the first single underscore from the element's name whenever its `Content` or `Header` is a string, an explicit `AutomationProperties.Name` included, whatever the template draws. Nothing is drawn wrong and no key is registered; a screen reader hears "Save changes to duedate" for `due_date`. Members (measured): base column headers, every grid's row headers, the embed card's "Jump to source", the tab's Close button, the property rows' Save, Delete, Remove and Add item buttons; by reading, the property rows' Revert, Pick and stepper buttons, the dashboard editor's Up, Down and Remove, and the other grids. Recommended: fix "Jump to source" here (a `TextBlock` content, in the file Call 5 changes; after Call 5 the card's header says "my_notes" and its own button "mynotes", measured by the red team), and file the rest as one issue (labels bug, windows): it reaches more than three other non-test files across subsystems (`S/Grids/AccessibleDataGrid.cs`, `S/WorkspaceTemplates.xaml`, `MW`'s dashboard editor, `S/Reading/ReadingTableGrid.cs`) and changes what a screen reader hears in each, so it is high by AGENTS.md. Cost: three more `EmbedPreviewTitleCensus` registrations, in the rule Call 5 changes anyway.
8. **The Folders menu's rows carry no access key.** (A) None, as Call 5 gives (recommended: the row says what it shows, and a folder without an underscore has no key today either). (B) Numeric keys 1 to 9, which Microsoft's guidance asks for dynamic menu items ("For dynamic menu items (such as recently used files), assign access keys numerically"). Cost of B: a number drawn and spoken before each folder name, a new phrase for NVDA, and the Mac's menu has none.
9. **Three menu commands run with nothing said.** Collapse All, Refresh Sync Diagnostics and Clear Recents run from the menu and say nothing (step 0 rows 7, 27, 11); Clear Recents cannot be undone. This chunk changes none of them (two keep their key; Clear Recents moves from R to N). Recommended: one issue (labels bug, windows), since each fix is a new spoken sentence, which AGENTS.md never sizes small or medium. Cost: one issue; the owner sees Clear Recents' key with the table.
10. **Where the facts live.** The census and a pin of all 37 ruled letters read `MW` as source text (unit facts, every lane). The behaviour (an item's own key runs nothing else while the item is off; a changed item runs on its key) is a hosted theory on a shown shell under the desktop lock, which skips where the desktop refuses the menu (#1680), as CB-22's three menu facts do; the hosted runners will skip it. The underscore facts each have a part every desktop runs (painted text, the peer, the window source's and the menu scope's registrations) and a key-press part with a premise that skips where the desktop will not let the menu hold the keys. Recommended: as listed under Tests.

## The owner's table

"Today" is the access key on `main` 38f6edde; a shared key is in bold. "Proposed" is the new letter; "none" means the item carries no key (its chord or the arrows reach it); a blank means unchanged. "What the key does now" is what pressing the proposed letter does in the open menu, in the state a reader usually meets, where it differs from today (step 0's measured rows, numbered as in "Step 0, as run"). The Workspace rows are option A' of Call 2.

The levels with no shared key keep every letter: the app menu (F, W, B, E, N, G), the Editor menu and its Math and Code Preamble Verbosity submenus, the Canvas Verbosity submenu, the Graph Verbosity submenu (built in code, no keys), the two right-pane list context menus and the tab strip's context menu (no keys), and the editor's context menu (A, E, S). A parent level's letters do not reach inside an open submenu (step 0 rows 7 and 13: Files Sidebar C and F run their own items though the File menu and the app menu hold C and F); Alt and a menu's letter inside an open submenu switches menus, as DS-13 designed.

### File (Alt+F)

| Item | Today | Proposed | Why | What the key does now |
|---|---|---|---|---|
| Open Vault… | O | | | |
| Quick Open… | Q | | | |
| New Note from Template… | T | | | |
| New Canvas | **C** | N | (b) New is N; N is free | N makes a new canvas at once; on `main` C moved the keys here first (row 1) |
| Close Vault | **C** | C | (b) Close is C | C closes the vault at once; on `main` it took a second C (row 1) |
| Files Sidebar | S | | | |
| Settings… | E | | | |
| Exit | X | | | |

### Files Sidebar (Alt+F, then S)

| Item | Today | Proposed | Why | What the key does now |
|---|---|---|---|---|
| Open | O | | | |
| Open in New Tab | T | | | |
| Open in Split | **S** | S | (d) first letter of "Split"; the first S item. The table's Split letter, p, is Pin Note's | S opens the selected note in a split at once; with nothing selected it does nothing (rows 3, 4) |
| Import Files and Folders… | I | | | |
| Cancel Import | **C** | M ("Cancel I_mport") | C stays with Collapse All (a); I is taken; M is the wide consonant of "Import" | M stops an import at once; off otherwise |
| Refresh | **R** | R | (a) R runs it today with no recents (row 9) | R refreshes at once also with recents (row 10 moved) |
| Collapse All | **C** | C | (a) C runs it today (row 7) | C collapses at once also during an import (row 8 moved); silent (Call 9) |
| Expand Loaded | E | | | |
| Focus Filter | **F** | F | (a) F runs it today (row 13). Call 3 | F focuses the filter at once also with forward history (row 14 moved) |
| Back | B | | | |
| Forward | **F** | W ("For_ward") | F stays with Focus Filter (Call 3); W is the wide consonant of "Forward" | W goes forward at once when there is forward history |
| Pin Note | P | | | |
| Unpin Note | U | | | |
| Unpin All in Folder | A | | | |
| Add to Shortcuts | **S** | D ("A_dd to Shortcuts") | (d) A, T and S are taken; D is the consonant of its first word | D adds the selected row to the shortcuts at once |
| Remove Shortcut | **R** | H ("Remove S_hortcut") | R stays with Refresh, M goes to Cancel Import and V to Use Vault Default Sort; H is its only other free letter | H removes the selected shortcut at once |
| Use Vault Default Sort | **S** | V ("Use _Vault Default Sort") | (d) first letter of its second word | V restores the vault's sort at once when it was changed |
| Clear Recents | **R** | N ("Clear Rece_nts") | C and R are taken; N over the narrow L | N clears the recents at once, silently, and cannot be undone (Call 9) |

### Workspace (Alt+W), option A'

| Item | Today | Proposed | Why | What the key does now |
|---|---|---|---|---|
| Duplicate Tab | **D** | D | (d) first letter; Alt+W lands on it | D duplicates the tab at once; on `main` the first D moved to Split Down (row 15) |
| Close Tab | **C** | C | (b) Close is C | C closes the tab at once; on `main` it moved (row 19). With no tab it does nothing (row 20 ran Show Changes) |
| Reopen Closed Tab | **R** | none | (e); its only free letter, O, goes to Close Pane, which has no chord; Ctrl+Shift+T reaches it | |
| Split Right | **R** | R | (d) first letter of its second word; the first R item a reader reaches today; no other letter of its label is free | R splits right at once; on `main` it moved (row 21) |
| Split Down | **D** | none | (e); no free letter; Ctrl+Alt+\ reaches it | |
| Close Pane | **P** | O ("Cl_ose Pane") | P stays with Toggle Right Pane (a); Close Pane has no chord, so it keeps a key | O closes the pane at once with two panes; off with one |
| Focus Pane Left | L | | | |
| Focus Pane Right | **R** | U ("Foc_us Pane Right") | The free vowel of its first word | U moves the keys to the pane on the right; with one pane, into the right pane, showing it when hidden (`S/WorkspaceViewModel.Layout.cs:1234-1240`) |
| Focus Pane Above | A | | | |
| Focus Pane Below | B | | | |
| Focus Next Region | **R** | X ("Focus Ne_xt Region") | The distinctive consonant of "Next" | X moves to the next region at once (F6) |
| Focus Previous Region | **G** | G | (a) G runs it today with one pane (row 25) | G runs it at once also with two panes (row 26 moved) |
| Move Tab Left | none | | | |
| Move Tab Right | none | | | |
| Widen Files Sidebar | F | | | |
| Narrow Files Sidebar | I | | | |
| Widen Right Pane | N | | | |
| Narrow Right Pane | E | | | |
| Grow Pane | **G** | none | (e); G stays with Focus Previous Region and no other letter is free; Ctrl+Alt+= reaches it | |
| Shrink Pane | **S** | none | (e); S stays with Refresh Sync Diagnostics (a); Ctrl+Alt+- reaches it | |
| Toggle Files Sidebar | T | | | |
| Toggle Right Pane | **P** | P | (a) P runs it today with one pane (row 23) | P toggles at once also with two panes (row 24 moved) |
| Switch Theme… | **H** | M ("Switch The_me…") | H goes to Show History Panel; M is the wide consonant of "Theme" | M opens the theme choice at once |
| Search Vault… | V | | | |
| Show Tasks Review | **R** | K ("Show Tas_ks Review") | R stays with Split Right; K is the distinctive consonant of "Tasks" | K opens Tasks Review at once |
| Show History Panel | **H** | H | (d) first letter of its second word (Switch Theme's h is mid-word) | H shows the History panel at once; on `main` the first H moved to Switch Theme (row 29) |
| Show Changes Since Last Open | **C** | W ("Sho_w Changes Since Last Open") | C stays with Close Tab; no chord, so it keeps a key; W is early and wide | W toggles the setting at once (row 20's run, now on its own key) |
| Refresh Sync Diagnostics | **S** | S | (a) S runs it today with one pane (row 27); contract 27's SD7 names it "Refresh _Sync Diagnostics" | S refreshes at once also with two panes (row 28 moved); silent (Call 9) |

### Base (Alt+B)

| Item | Today | Proposed | Why | What the key does now |
|---|---|---|---|---|
| Refresh | R | | | |
| Where Am I? | W | | | |
| Results | U | | | |
| Next View | N | | | |
| Previous View | P | | | |
| Open View Switcher | S | | | |
| View as Table | T | | | |
| View as List | L | | | |
| Quick Filter | F | | | |
| Sort by Column | **C** | C | (d) first letter of "Column"; the first C item; its only other free letter is the descender y | C sorts at once on a base; on `main` it moved (row 30) |
| Save Sort to View | V | | | |
| Open Row | O | | | |
| Copy Link | **K** | K | The first K item; (b)'s Copy C is not applied (rule b); its only other free letter is y | K copies the link at once; on `main` it moved (row 32) |
| Show Backlinks | B | | | |
| Edit Property | E | | | |
| Export View as CSV… | **C** | X ("E_xport View as CSV…") | E is taken; X is the distinctive consonant of "Export" | X opens the export at once |
| Export View as Markdown Table… | M | | | |
| Copy View as Markdown | **K** | D ("Copy View as Mark_down") | The distinctive consonant of "Markdown" (M is taken) | D copies the view at once |
| New Query… | Q | | | |
| Edit View Filters… | I | | | |

### Canvas (Alt+N)

| Item | Today | Proposed | Why | What the key does now |
|---|---|---|---|---|
| Where Am I? | W | | | |
| New Card | D | | | |
| Create Connected Card | **T** | R ("C_reate Connected Card") | T stays with Trace Path; R is the consonant of "Create" | R makes a connected card at once |
| Next Card | N | | | |
| Previous Card | P | | | |
| Enter Group | E | | | |
| Exit Group | X | | | |
| Follow Connection Forward | F | | | |
| Follow Connection Back | B | | | |
| Trace Path from Selected Card | **T** | T | (d) first letter of its first word | T traces at once on a canvas; on `main` the first T moved to Create Connected Card (row 35) |
| Filter Cards… | I | | | |
| Clear Filter | C | | | |
| Commit Mode | M | | | |
| Cancel Mode | L | | | |
| Toggle Viewport Follows Selection | S | | | |
| Verbosity (submenu) | V | | | |

### Graph (Alt+G)

| Item | Today | Proposed | Why | What the key does now |
|---|---|---|---|---|
| Open Graph | **O** | O | (a) O runs it today (rows 37, 38); (b) Open is O | O opens the graph at once also in Diagram mode (row 39 moved) |
| Orphaned Notes | N | | | |
| Unresolved Links | U | | | |
| Most Linked Notes | M | | | |
| Where Am I? | W | | | |
| Zoom In | I | | | |
| Zoom Out | **O** | T ("Zoom Ou_t") | (c) the Editor menu's Zoom Out has had T since CB-22; Z, the first letter, is also free here | T zooms out in Diagram mode; off in Table mode |
| Actual Size | A | | Not shared, so not moved; by rule (c) it could take Z, the Editor menu's Actual Size key since CB-22, if the owner wants the two menus alike | |
| Fit Graph | F | | | |
| Verbosity (submenu) | V | | | |

## Step 0, as run

Two workers measured on `main` 38f6edde with a Release build, on 2026-11-23, under the desktop lock. Nothing was committed.

**The sixteen shared keys** (worker A; `step0-report.md`, in the principal's working notes, not committed). A probe of 42 cases (`Cb22bStep0Probe.cs`, all passed) on a shown shell with a real Files sidebar attached, menus opened by the window's own Alt route (`MainWindow.OpenAppMenu`), each carrier read with the menu open, the letter sent as text input, every `Click` of the level and every announcement logged. And an NVDA pass (NVDA 2026.3.1, started with `--debug-logging` and quit by the pass, no setting changed) on main's Release build through `scripts/windows/launch-for-field-pass.ps1 -Root`, every phrase from NVDA's log: all sixteen keys in the usual state, two panes for Workspace P, G and S, no tab for Workspace D, C and R and Graph O, and a graph tab for Graph O.

What holds for every key, by running: with the menu open each carrier's `IsEnabled` equalled its command's `CanExecute` in all 108 readings, and every carrier was visible. Two or more enabled carriers: the letter moves the keys to the next enabled carrier in menu order, wrapping, runs nothing, and the menu stays open. One: the letter runs it at once and the menu closes. None: nothing happens and NVDA says only the letter. With the menu closed every carrier reads enabled, whatever its command says (CB-22's M5).

| # | Key | State | Carriers on (menu open) | The key's first press | NVDA |
|---|---|---|---|---|---|
| 1 | File C | usual | New Canvas, Close Vault | moves to New Canvas; a second C to Close Vault | "New Canvas", "C", "4 of 8"; "Close Vault", "C", "5 of 8" |
| 2 | File C | an import running | Close Vault | runs Close Vault: "Import cancellation requested. Close the vault again after completed copies finish reconciling." | probe only |
| 3 | Sidebar S | usual, opened by Quick Open | none | nothing | "s" |
| 4 | Sidebar S | usual, opened from the tree | Open in Split, Add to Shortcuts | moves | probe only |
| 5 | Sidebar S | nothing selected, sort changed | Use Vault Default Sort | runs it: "Sorted by name, A to Z." | probe only |
| 6 | Sidebar S | a folder row without a folder note | Add to Shortcuts | runs it: "Added Projects, folder shortcut to shortcuts." | probe only |
| 7 | Sidebar C | usual | Collapse All | runs it; nothing said about the collapse | the focus lines back to the editor |
| 8 | Sidebar C | an import running | Cancel Import, Collapse All | moves | probe only |
| 9 | Sidebar R | usual, no recents | Refresh | runs it | the focus lines, then "Files refreshed. No changes." |
| 10 | Sidebar R | usual, one recent | Refresh, Clear Recents | moves | probe only |
| 11 | Sidebar R | an import, one recent, no shortcut | Clear Recents | runs it (recents 1 to 0), silent | probe only |
| 12 | Sidebar R | an import, a shortcut selected | Remove Shortcut | runs it: "Removed Projects, folder shortcut from shortcuts." | probe only |
| 13 | Sidebar F | usual | Focus Filter | runs it; the keys in the filter box | "Filter files", "edit", the help, "blank" |
| 14 | Sidebar F | forward history | Focus Filter, Forward | moves | probe only |
| 15 | Workspace D | usual | Duplicate Tab, Split Down | moves to Split Down (Alt+W landed on Duplicate Tab) | "Split Down", "D", "Ctrl+Alt+backslash", "5 of 28" |
| 16 | Workspace D | six panes | Duplicate Tab | runs it | probe only |
| 17, 18 | Workspace D | a graph tab; no tab | none | nothing | "d" |
| 19 | Workspace C | usual | Close Tab, Show Changes Since Last Open | moves | "Close Tab", "C", "Ctrl+W", "2 of 28"; "Show Changes Since Last Open", "not checked", "C", "27 of 28" |
| 20 | Workspace C | no tab | Show Changes Since Last Open | runs it (the preference turns on) | "Editor workspace", "Changes since last open shown in History." |
| 21 | Workspace R | usual | Split Right, Focus Pane Right, Focus Next Region, Show Tasks Review | moves, through all four | "Split Right", "R", "Ctrl+backslash", "4 of 28", and so on |
| 22 | Workspace R | no tab | three or four | moves | as 21 |
| 23 | Workspace P | one pane | Toggle Right Pane | runs it | the focus lines, then "Right pane hidden." |
| 24 | Workspace P | two panes | Close Pane, Toggle Right Pane | moves | "Close Pane", "P", "6 of 28"; "Toggle Right Pane", "P", "Ctrl+Alt+I", "22 of 28" |
| 25 | Workspace G | one pane | Focus Previous Region | runs it | "Tab bar.  beta, tab 1 of 1." |
| 26 | Workspace G | two panes | Focus Previous Region, Grow Pane | moves | "Focus Previous Region", "G", "Shift+F6", "12 of 28"; "Grow Pane", "G", "Ctrl+Alt+equals", "19 of 28" |
| 27 | Workspace S | one pane | Refresh Sync Diagnostics | runs it; nothing says so | the focus lines only |
| 28 | Workspace S | two panes | Shrink Pane, Refresh Sync Diagnostics | moves | "Shrink Pane", "S", "Ctrl+Alt+dash", "20 of 28"; "Refresh Sync Diagnostics", "S", "28 of 28" |
| 29 | Workspace H | usual | Switch Theme…, Show History Panel | moves | "Switch Theme…", "H", "23 of 28"; "Show History Panel", "H", "26 of 28" |
| 30, 31 | Base C | a base tab; a note tab | both; none | moves; nothing | "Sort by Column", "C", "Ctrl+Alt+S", "10 of 20"; "Export View as CSV…", "C", "16 of 20" |
| 32 to 34 | Base K | a base tab, with and without a row; a note tab | both; both; none | moves; moves; nothing | "Copy Link", "K", "13 of 20"; "Copy View as Markdown", "K", "18 of 20" |
| 35, 36 | Canvas T | a canvas tab; a note tab | both; none | moves; nothing | "Create Connected Card", "T", "Ctrl+Alt+Shift+N", "3 of 16"; "Trace Path from Selected Card", "T", "10 of 16" |
| 37, 38, 40 | Graph O | a note tab; a graph tab in Table mode; no tab | Open Graph | runs Open Graph | the graph tab's lines, "Graph.", "3 notes, 3 links." |
| 39 | Graph O | a graph tab in Diagram mode | Open Graph, Zoom Out | moves | probe only |

So eleven of the sixteen keys run another item's command at once in some state a reader meets (File C, Sidebar S, C, R and F, Workspace D, C, P, G and S, Graph O), three only move (Workspace R and H, and Base K), and two (Base C, Canvas T) only move because their two items share one enable rule.

**The census rows' "Alone" claims** (CB-22's, by reading). Confirmed: File C (an import), Sidebar C, R and F, Workspace C, R, P, G, S and H. Wrong or incomplete, five: Sidebar S (a folder row is a second alone state); Workspace D (only Duplicate Tab is ever on alone, at six panes); Base K (with no row both stay on, and K only moves); Base C and Canvas T, "not settled" (settled: never alone); Graph O (a graph tab opens in Table mode, where Zoom Out is off, so O runs Open Graph again; it only moves in Diagram mode).

**Drift.** Every census line is 16 higher than on 38f6edde (CB-17b's #2045 removed 16 net lines above the menus). The lines on 38f6edde: File 285, 289; Sidebar 309 to 357; Workspace 371 to 449; Base 490 to 517; Canvas 688, 715; Graph 776, 798.

**The underscore class** (worker B; `underscore-report.md`, probe `Cb22bUnderscoreProbe.cs`, in the principal's working notes, not committed; two runs under the lock). A survey of every place a user's or a vault's text reaches a presenter (38 places), then a probe on the shown shell for each member and each unsure one. Two mechanisms:

- *Class 1, the access-key presenter.* `ContentPresenter.RecognizesAccessKey` is true in the app's menu rows (`S/Themes/Slate.Menus.xaml:85-90`, `:177-183`), `Slate.ButtonBase` (`S/WorkspaceTemplates.xaml:80-83`), the workspace tab (`:1108-1111`), and Fluent's `Expander` header; false in Fluent's `DataGridColumnHeader` (measured). Members, measured:
  - The Folders menu (`S/NoteHeaderView.cs:384`). Rows for `my_notes/due_date/a_b_c/Note.md` paint "mynotes", "duedate", "ab_c"; the peers are named the same (the explicit `AutomationProperties.Name` "my_notes" is stripped too) and carry N, D, B, registered in the menu's scope. A letter press in the open menu clicks that row (reveals the folder); two folders that give one letter (`more_notes/my_notes`) take the presses in turn.
  - The embed cards (`S/EditorEmbedPreview.cs:79`, `Header = node.Title`, core's "Embedded note: {path}" and its siblings). "Embedded note: my_notes.md" paints "…mynotes.md"; its header button's peer is named so with key N; the window's source registers B, D and N while the Embeds leaf shows. Then `AppMenuKeys.Decide` leaves Alt+B and Alt+N to the cards, so the Base and Canvas menus do not open: Alt+N moved the keys into the right pane (two N cards), Alt+D and Alt+B collapsed a card. The Ctrl+E popover builds the same view (by reading).
  - Not members (measured): the workspace tab header (a view model drawn by a `TextBlock`), the base view picker. By reading (worker B, and the spec red team independently): every menu built in code with fixed labels, every `Slate.ButtonBase` button (fixed labels), every C# `Content =` on a code-built button, check box or radio button and every bound XAML `Content` (phrases), the palette, Quick Open, the lists and trees (TextBlock templates), the template picker, saved queries, recents, tags, the crumb buttons (`RecognizesAccessKey="False"`), tooltips, the history expanders (dates). No `ItemsSource` menu, no bound `AccessText`, and no `Label`, `GroupBox` or data `TabItem` header exists.
- *Class 2, the peer name only* (Call 7). Measured: base column headers ("due_date" named "duedate"; drawn whole, no key), row headers ("mynotes.md"), the embed card's "Jump to source: mynotes.md", the tab's "Close ab_c", the property rows' "Save changes to duedate", "Delete property duedate", "Remove item 1 from ab_c", "Add item to ab_c".

**Menus built in code** (worker B, by reading): the note header's ⋯ and Folders menus, Sort and group, More panels, Graph Verbosity, the Connections row menu, the graph node menu, the canvas menus and the grid row-actions menus. No fixed row carries an access key; only the Folders menu's data rows do, and they share one whenever two folders give one letter (measured). No code-built level mixes a fixed row with a data row.

**Measuring caveats for the tests:** an `AccessText`'s inner `TextBlock` keeps the raw string ("my_notes") as its one run while it paints "mynotes", so a fact reads the painted glyphs or the peer, not the `TextBlock`'s text. With another window active, the global key query (`AccessKeyManager.IsKeyRegistered(null, …)`) finds nothing and `AppMenuKeys.Decide` opens the Base and Canvas menus even on `main`, while the window's own source still registers B and N (the spec red team, measured); so a fact asserts the window's source. An explicit `TextBlock` takes Fluent's implicit `TextBlock` style (`TextWrapping=NoWrap`, `TextTrimming=None`), which a presenter's generated one does not, so the card header's local `TextWrapping = Wrap` is load-bearing (measured).

## Current state on main

**The census.** `T/Censuses/MenuAccessKeyCensus.cs`: `NoTwoItemsOfOneMenuLevelShareAnAccessKey` reads every `Menu`, `ContextMenu` and item-holding `MenuItem` in the shell's XAML, keys by the first single underscore, and passes the sixteen keys through sixteen dated `Exemption` rows (2026-11-21), each matched by file, level, key and its exact items. `TheCensusSeesAPlantedPairAndReadsHeadersAsWpfDoes` proves the parse. Menus built in code are outside it.

**The menus.** All sixteen keys are in `MW`: File (`:261-368`), its Files Sidebar submenu (`:301-358`), Workspace (`:369-457`), Base (`:458-527`), Canvas (`:672-772`), Graph (`:774-830`). No carrier's visibility is bound. The app menu opens a level with Alt and the level's letter and lands the keys on its first enabled, visible row (`S/MainWindow.AppMenu.cs:24-80`).

**Tests that pin a header.** `T/GraphMenuTests.cs:117` and `:124` pin "_Open Graph" and "Zoom _Out" as source text; the second changes with this chunk. No other test, help document or register names a changing header with its underscore (searched), except contract 27's SD7, which names "Refresh _Sync Diagnostics", kept. The Mac has no access keys; the design export's menus (`design/windows/ui_kits/slate-windows/Chrome.jsx:4-5`) draw no underlines.

**The two underscore sites, and the census that reads one.** `S/NoteHeaderView.cs:375-388` builds each Folders row as `new MenuItem { Header = name, Icon = … }` with the sibling-rule name from `SiblingNames.Compose`. `S/EditorEmbedPreview.cs:77-84` builds a disclosure node as `new Expander { Header = node.Title, … }` with `AutomationProperties.Name` set to the title; `WrapTitle` (`:92-105`) looks for the `TextBlock` a `ContentPresenter` made under the header `ToggleButton` and sets it to wrap; "Jump to source" (`:170-176`) is a button with a string `Content` and an explicit name. `T/Censuses/EmbedPreviewTitleCensus.cs` and `.Sinks.cs` (contract 38's provenance witness for `EmbedPreviewShown`, `docs/plans/38_notification_dispatcher_contracts.md:1334`) register every text the renderer writes (`RendererTexts`, `.Sinks.cs:634-642`, including `("Content", "\"Jump to source\"")`), and a mutation row edits the literal `Header = node.Title,` (`EmbedPreviewTitleCensus.cs:275`). `T/NarrowWindowLayoutTests.cs:105-124` (DS-2) finds the title's `TextBlock` by its text and checks it wraps. `T/EmbedTitleRealizedSurfaceTests.cs:656-658` reads an `AccessText`'s or a `TextBlock`'s text.

## Design

1. **The letters.** `MW` takes the owner's letters as ruled on Calls 1 and 2 (as tabled: 17 headers gain a new underscore and 4 lose theirs; nothing else in the XAML moves). Each header keeps its words and its ellipsis.
2. **The census ends its rows.** `Exemptions` is emptied; by Call 4 (A) the waiver mechanism goes with it. The planted fact keeps every parse assert. The class comment says the sweep is done and names this chunk.
3. **A pin of the letters.** One table-driven fact reads `MW` and pins all 37 ruled rows of the owner's table (the 21 changed and the 16 kept winners of a shared key) by automation id, or by command binding where an item has no id, so a later edit that moves any ruled letter fails by name.
4. **The underscore class (Call 5).** `S/NoteHeaderView.cs:384`: `Header = new TextBlock { Text = name }`. `S/EditorEmbedPreview.cs:79`: `Header = new TextBlock { Text = node.Title, TextWrapping = TextWrapping.Wrap }`, and `WrapTitle` and its `Loaded` hook go. "Jump to source" (Call 7): `Content = new TextBlock { Text = "Jump to source" }`. The `AutomationProperties.Name` lines stay. A reader hears the underscore come back, and the stray key ("N", "B") that NVDA spoke with the row or the card's header stops; no access key is registered by a user's text.
5. **The census follows the renderer (Call 5's cost).** `EmbedPreviewTitleCensus`'s renderer rule admits a `Header` (and the Jump button's `Content`) that is a `new TextBlock { … }` whose only text is a card's Title (or the registered "Jump to source") and whose only other setter is `TextWrapping`; any other setter, `Inlines`, style, template or format there is a failure. Its mutation row at `:275` is rewritten against the new line, and a new row composes inside the `TextBlock` (`Text = "Embedded " + node.Title`) and must turn the census red. The title read count stays 6 (`Text = node.Title` in an initializer is a registered form, `.Sinks.cs:589-593`). Contract 38's row needs no word (it names no header shape).

What a reader gets. In every menu, each item's letter is its own: pressing it runs that item when it is on and does nothing when it is off; it never moves the keys and never runs another item. Four Workspace items are reached by their chords and the arrows, not a letter. A folder or an embed named with an underscore is drawn and spoken whole with no key, the card's Jump button says the same name, and Alt+N and Alt+B open their menus with the Embeds leaf showing.

What a reader loses. A key that only moved the keys on `main` now runs its item at once (the table's last column), and a sequence that repeated a shared key now runs its first item and sends the rest where the keys land (Call 1). Four Workspace items lose their letter.

Not delivered, and said so in the PR: the rest of the peer-name class (Call 7, its own issue); the three silent commands (Call 9, its own issue); numeric keys for the Folders rows (Call 8, B); a restructured Workspace menu (Call 2, C); a census of the underscore class (Call 6); a census of menus built in code (none carries a key today).

## Tests

Each red first on `main`'s product code, then green; each mutation applied, its fact seen red, and reverted. Facts that show a window run on an STA thread with a pumped dispatcher, under the desktop lock for the local runs.

| Fact | Where | Asserts | Red on main by | Mutation that turns it red again |
|---|---|---|---|---|
| `NoTwoItemsOfOneMenuLevelShareAnAccessKey` (existing) | `T/Censuses/MenuAccessKeyCensus.cs`, rows gone | No shared key in any XAML menu level | The sixteen keys, with the rows gone | One header back to its old letter |
| `TheMenuLettersAreTheOwners` (new, theory over 37 rows) | the same file | Each ruled item's header in `MW` is the ruled one; the four keyless ones carry no underscore | 21 rows differ | One header changed, kept or moved |
| `AnItemsOwnKeyRunsNothingElseWhileItIsOff` (new, hosted theory, 16 rows) | new `T/MenuAccessKeyTests.cs`, `ShownShell` with the Files sidebar attached, worker A's state builders | Premise: the item is off and a former twin on. Its own key, read from its live `AccessText`, typed into the open level; no `Click` on any item of the level, nothing announced, the menu still open. Rows: New Canvas in an import; Open in Split and Add to Shortcuts with nothing selected and the sort changed; Open in Split and Use Vault Default Sort on a folder row; Cancel Import, Remove Shortcut, Clear Recents and Forward in the usual state opened by Quick Open (no recents); Refresh and Remove Shortcut in an import with one recent; Refresh and Clear Recents in an import with a shortcut selected; Close Tab with no tab; Close Pane with one pane; Zoom Out on a note tab | Each row: the twin's `Click` fires (step 0 rows 2, 5, 6, 7, 9, 11, 12, 13, 20, 23, 37) | One header back: its rows red. Skips through `SkipWhereTheDesktopRefusesTheMenu` (#1680) |
| `AChangedItemRunsOnItsOwnKey` (new, hosted theory) | the same file | A changed item that is on runs at once on its own key, and no other item: New Canvas (N), Forward (W), Switch Theme (M), Show Tasks Review (K), Export View as CSV (X, the picker answered with cancel), Zoom Out (T, Diagram mode) | On `main` those letters run nothing or another item | The header back |
| `AFolderWithAnUnderscoreShowsWholeInTheFoldersMenu` (new, theory: `my_notes`, `due_date`, `a_b_c`) | new `T/DataHeaderAccessKeyTests.cs`, a shown shell 500 wide | Every desktop: painted glyphs and peer name equal the composed name; the row has no `AccessText` and its peer no access key; the menu's scope registers no letter. With the premise that the keys are on a row of the open menu (else skip, #1680): the letter typed clicks nothing | Step 0: "mynotes", key N, a click | `Header = name` back |
| `TwoFoldersThatGiveOneLetterTakeNoKeys` | the same | `more_notes/my_notes`: the menu's scope registers no N; with the same premise, N typed moves nothing and clicks nothing | Step 0: N registered; the keys move | The same |
| `AnEmbedTitleWithAnUnderscoreShowsWholeAndTakesNoWindowKey` | the same, the Embeds leaf shown | Every desktop: each card's painted header, its header button's peer name and its Jump button's peer name carry core's title whole; the window's source (`PresentationSource.FromVisual(shell)`) registers none of B, D, N. With the premise that the shell is the keyboard's active source (else skip that half): `AppMenuKeys.Decide` opens the Base menu for Alt+B and the Canvas menu for Alt+N | Step 0 and the red team: "…mynotes.md", "Jump to source: mynotes.md", B, D, N registered on the window's source | `Header = node.Title` back; the Jump `Content` back to a string (its name assert only) |
| `AnEmbedTitleWithAnUnderscoreWraps` | the same, or a row in `T/NarrowWindowLayoutTests.cs`'s DS-2 fact | The title of an underscore card wraps, read as painted glyphs | Step 0: NoWrap | `TextWrapping` removed (Fluent's style then gives NoWrap) |
| `EmbedPreviewTitleCensus` (existing, rule changed) | `T/Censuses/EmbedPreviewTitleCensus.cs`, `.Sinks.cs` | Design 5 | Red on Design 4 without Design 5 (the red team ran it) | The composing row |

Edited existing tests: `T/GraphMenuTests.cs:124` ("Zoom Ou_t"); `EmbedPreviewTitleCensus`'s rule and two rows.

Regression classes before the whole run: `MenuAccessKeyCensus`, `GraphMenuTests`, `AppMenuKeysTests`, `EditorSurfaceCommandTests`, `NoteHeader*`, `EmbedTitleRealizedSurfaceTests`, `EmbedPreviewTitleCensus`, `NarrowWindowLayoutTests`, `MenuBarCensus`, `MenuRowCommandTests`, `ItemContainerNameCensus`, `CanvasContextMenuTests`, `RightPane*`, `CommandDriftTests`, `FilesSidebar*`.

## NVDA spot check (base and head)

Launcher `scripts/windows/launch-for-field-pass.ps1 -Exe -Vault -Root`, under the desktop lock, NVDA speech from its log, NVDA's punctuation level recorded. Base is main's Release build (`base-app`, in the principal's working notes, 38f6edde); worker A's pass is base for the sixteen keys, and base is run again for steps 4 to 6. Vault: worker A's (`Projects/alpha.md`, `beta.md`, `gamma.md`, `Notes.base`, `board.canvas`) plus `my_notes/due_date/deep.md`, `more_notes.md`, `a_b_c.md`, and `host.md`, which embeds `my_notes/due_date/deep.md`, `more_notes.md` and `a_b_c.md` (two cards hold N, one holds B).

1. **Each changed item's line.** For each of the 21 items, the menu opened by its Alt chord and the item reached by the arrows. Head, expected: its name, its new letter (or no letter), its chord and its position, for example "New Canvas", "N", "4 of 8"; "Reopen Closed Tab", "Ctrl+Shift+T", "3 of 28". Base: the old letter (worker A's pass).
2. **Each new key pressed once where its item is on**, and once where it is off. Head, expected: the item runs at once, as the table's last column says, with that command's own sentence (for example Alt+W, W: "Changes since last open shown in History."); off, only the letter.
3. **The kept keys that ran another item.** Head, in step 0's alone states reachable by keys: one pane, Alt+W, O (Close Pane is off): only "o"; no tab, Alt+W, C: only "c"; a note tab, Alt+G, T: only "t". Base: worker A's rows 20, 23, 37.
4. **The kept keys that now run at once, and a repeated sequence.** Head: Alt+W, D; Alt+W, R; Alt+W, H; on a base, Alt+B, C and K; on a canvas, Alt+N, T. Then, on base and head, the sequence Alt+W, R, R, Enter: base, expected: the keys reach Focus Pane Right and Enter runs it; head, expected: R splits right, and what the second R and the Enter do and say is recorded with the note's text read before and after. Last, Alt+F, C (Close Vault runs).
5. **The Folders menu.** The window made narrow (SetWindowPos) so the header compacts; `deep.md` open; the Folders button; Down through its rows. Base, expected: "mynotes" and "duedate", each with its key ("N", "D"). Head, expected: "my_notes" and "due_date" as NVDA reads an underscore at the recorded level, with no key; N typed in the open menu does nothing.
6. **The embed cards.** `host.md` open, the Embeds leaf shown; Tab to the card headers and the Jump buttons. Base, expected: "Embedded note: mynotes…" with a key, "Jump to source: mynotes…"; Alt+N moves the keys to the other N card; Alt+B collapses the `a_b_c` card. Head, expected: the titles whole and no key, the Jump button's name whole; Alt+N opens the Canvas menu (on a note tab its one enabled row: "Verbosity", "V", "16 of 16"); Alt+B opens the Base menu.

## Files it edits

- `MW`: 21 headers in the File, Files Sidebar, Workspace, Base, Canvas and Graph menus.
- `S/NoteHeaderView.cs`: one line (`Header`).
- `S/EditorEmbedPreview.cs`: the card header, the Jump button's content, and `WrapTitle` with its hook removed.
- `T/Censuses/MenuAccessKeyCensus.cs`: the rows and the waiver go; the pin added.
- `T/Censuses/EmbedPreviewTitleCensus.cs`, `.Sinks.cs` (and the `.Xaml.cs` comment that says the headers are strings): the renderer rule, the Jump registration, two mutation rows.
- `T/MenuAccessKeyTests.cs` (new), `T/DataHeaderAccessKeyTests.cs` (new): license header through the script, LF endings checked after it (CB-22's #1955 line on the script's CRLF).
- `T/GraphMenuTests.cs`: one row.
- No core, FFI or Swift file; no chord, command, help line or automation id; no `A11yEvent` construction site, so the trigger ledger and the parity matrix should not move (`--check` run on both); the focus manifest is untouched.

## Overlap

- CB-17b (merged as #2041 and #2045) held `MW`; this chunk starts from its merge.
- CB-23 (Principal Q, wave C12; branch `claude/plan-47-cb23-empty-panel-lines` at 76394cd0, not pushed) edits `MW` at the right pane (hunks from `:2747` and `:2803`), `S/MainWindow.ShellRegions.cs`, `S/WorkspaceViewModel*.cs`, three censuses, core and `crates/slate-uniffi/src/lib.rs`. This chunk's `MW` hunks are at `:285-798`, so the file overlaps and the hunks do not; no other file is shared (checked on 76394cd0). Whichever merges second takes `main` in; if CB-23 lands first, this branch regenerates the bindings after the merge (its FFI change).
- CB-42 (Principal Q, tests only) shares no file.
- CB-16 (C11) takes `MW` after this chunk, as §6 orders.

## #1954 and #1955

- #1954 (Mac work owed): no line. The Mac's menus have no access keys, its Folders menu and embed cards draw titles as text, and no core or Swift file changes.
- #1955 (deferred finds): this PR ends line 1 of comment 7066667520 (the sixteen keys and the Folders member); line 2 (`scripts/apply-license-header.py`'s CRLF) stays. The PR body says so with "Refs #1955". No new line if Calls 7 and 9 are ruled as recommended (issues instead).

## Found on the way (step 0 and the spec red team)

| # | Find | Size | What happens to it |
|---|---|---|---|
| 1 | The embed cards' headers register a user's underscore letter on the window's source and take Alt+N and Alt+B from the app menu | A member of this row's class | Fixed here, red first (Call 5) |
| 2 | The DS-2 title wrap misses a title with an underscore (an `AccessText` draws it) | Small, in a file this PR changes | Fixed here by the same change, red first |
| 3 | The peer-name class (class 2) | High, but for the one member in this PR's file | "Jump to source" fixed here; the rest one issue (Call 7) |
| 4 | Three menu commands run with nothing said; Clear Recents cannot be undone | High (new sentences) | One issue (Call 9) |
| 5 | Five of the census's "Alone" claims were wrong or incomplete | Wrong claims, not defects | Corrected above; the rows go |
| 6 | Two folders that give one letter share a key in the Folders menu | Part of the row's member | Ended by Design 4 |

## Verification

- Red run of the new facts on `main`'s product code, each failing line kept; green run; each mutation.
- `dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include` each edited C# file.
- `python scripts/a11y_trigger_ledger.py --check`; `python3 scripts/generate-parity-matrix.py --check`; `python scripts/check_windows_census_trait.py`.
- The regression classes, then the whole Windows unit run under the desktop lock.
- The NVDA spot check on base and head, phrases in the PR body.
- The diff red team (Opus, xhigh), then Codex rounds to approve.

## Acceptance

- No two items of any menu level in the shell's XAML share an access key, and the census holds no dated row and no waiver.
- Each ruled item carries the owner's letter; an item's own key never runs another item.
- A folder name or an embed title with an underscore is drawn and named whole, the card's Jump button names it whole, and no access key is registered by it.
- No sentence is added or changed; 21 headers change; the existing tests edited are `GraphMenuTests` (one row) and `EmbedPreviewTitleCensus` (its renderer rule and two rows).

## Verified by running / by reading only

By running (step 0, on `main`): every row of the step 0 table marked probe or NVDA; the underscore members, both mechanisms, the controls that are not members (the tab header, the view picker), Alt+N and Alt+B taken by the cards, the wrap. By running (the spec red team, on `main` with Design 4 simulated at runtime): the Folders rows and the cards painted and named whole, nothing registered, Alt+B and Alt+N open their menus, foreground, font, row height and UIA children unchanged; Fluent's implicit `TextBlock` style; the window source and the global registrations with another window active; `EmbedPreviewTitleCensus` red on Design 4 alone and on the Jump change; the count of C# `Header =` sites.

By reading only: the enable rules behind "never alone" (Open in Split, Split Down, Base C, Canvas T); the trash state; a base that is loading or failed; the Ctrl+E popover's cards; the class 2 members marked read; every menu built in code; dark and high-contrast templates (the `TextBlock` header does not depend on them); the head behaviour of every proposed letter (nothing is built yet); NVDA's speech for an underscore.

## Red team record

2026-11-23, a fresh Opus agent at xhigh effort, by `docs/plans/24_red_team_protocol.md`, against this spec's first draft and the tree at 38f6edde. Its report, probe (`Cb22bRedTeamProbe.cs`, two runs under the desktop lock, 15:01:58 to 15:02:12 and 15:04:30 to 15:04:35) and logs are kept in the principal's working notes (`spec-redteam-report.md`), not committed. No blocker; five major, ten minor, nine notes. All are folded in:

| # | Severity | Finding | What changed here |
|---|---|---|---|
| 1 | Major | Design 4 turns `EmbedPreviewTitleCensus` red (measured), and its mutation row loses its line | Call 5's cost, Design 5, Files, Tests, Acceptance |
| 2 | Major | The class guard by a `Header =` scrape cannot be built as written (31 sites, five class 2 sites, non-WPF matches, `Content` and XAML missed) | Call 6 now recommends no census |
| 3 | Major | Call 2 offered a costlier "3 lose" option than B', and dropped keys by chord where Microsoft says least used | Call 2 rewritten (A', A, B', B dominated, C); rule (e); the Workspace table is A' |
| 4 | Major | A learned repeated-key sequence now runs the first item and types the rest into a note | Call 1's cost; "What a reader loses"; NVDA step 4 |
| 5 | Major | The underscore facts pass on `main` where the shell is not the active source or the menu cannot hold the keys | Tests: the window source asserted; premises and skips |
| 6 | Minor | Six missing alone-state rows; two rows red only in the Quick Open state | The theory has 16 rows, its usual state pinned |
| 7 | Minor | Rule (b) applied inconsistently with the table cited | Rules (b) and (c) restated; Copy C named as not applied; "Show S" and "Find F" dropped |
| 8 | Minor | Four table cells wrong | Remove Shortcut, Collapse All, Focus Filter, Clear Recents and Focus Pane Right cells corrected |
| 9 | Minor | NVDA step 6's expectations wrong for its vault | Step 6 and its vault corrected; the punctuation level recorded |
| 10 | Minor | "Jump to source" left reading the other way after Call 5 | Fixed here (Call 7) |
| 11 | Minor | Two empty regression patterns, five classes missing | The list corrected |
| 12 | Minor | The pin covered only the 21 changed items | All 37 ruled rows |
| 13 | Minor | The stray key also stops being spoken | Design 4; NVDA steps 5 and 6 |
| 14 | Minor | CB-23's head and hunks were stale | Overlap: 76394cd0, `:2747`, the FFI note |
| 15 | Minor | The #1955 comment has two lines | "#1954 and #1955" |
| N1 to N9 | Notes | Design 4 holds where measured; Fluent's TextBlock style; class 1 complete by reading; plain letters toggle cards on `main` too; `NoteHeaderTests` failure messages print the header's type; M and X sit beside a descender p; three silent runs; Alt+W inside Workspace switches menus; the letter sets verified | Folded where they change the spec (the caveats, Call 9, the parent-level note); N5, N6 and N8 recorded here only |

## Diff red team record

2026-11-23, a fresh Opus agent at xhigh effort, by `docs/plans/24_red_team_protocol.md`, against the branch at 46e16971 (the tests, then the product change), in a worktree of its own. Its report is `diff-redteam-report.md`, in the principal's working notes, not committed. No blocker; two major, three minor, five notes:

| # | Severity | Finding | What changed |
|---|---|---|---|
| 1 | Major | `GlyphsNameNothingCensus` fails on head: `DataHeaderAccessKeyTests` typed the private-use bounds as literal characters (measured; the only failure in all 1,595 census cases) | Escapes `'\uE000'` and `'\uF8FF'`; every census is in the green runs from here on |
| 2 | Major | Neither provenance witness of the card title saw text added to the header `TextBlock` through its `Inlines` (measured: the census passed 68 of 68 and `EmbedTitleRealizedSurfaceTests` passed in three themes with a Run added) | The census refuses inline text in the renderer (`Inlines`, any `Inline` type by name, a `TextPointer` insert), with three mutation rows and a constructor-argument row; `EmbedTitleRealizedSurfaceTests` reads a `TextBlock`'s drawn text through a `TextRange`. Product mutation D (a Run added) now turns both red, and mutation E (the rule without the `TextPointer` insert) turns its row red |
| 3 | Minor | The underscore facts asserted their readings first, so on `main` the key-press and Alt-and-letter parts never ran | The three facts gather every part into one list; on a desktop that refuses the menu they fail on any reading and skip only when the readings are clean. Seen red on `main` in every part |
| 4 | Minor | The Folders fact's "the window registers nothing" passed on `main` and would break on any later fixed key | Removed |
| 5 | Minor | An explicit `TextBlock` takes its foreground from its logical parent, so Fluent's presenter-targeted triggers no longer dim the Jump label while pressed or a disabled card's title | Accepted and named in the PR: no ancestor disables either today, the pressed dimming lasts a press, and Fluent's HC brushes give the same foreground at rest and on hover (measured from the theme); a real high-contrast session is not verified |
| N6 | Note | Three branches of the census's TextBlock rule had no mutation row | A constructor-argument row added (two other branches cannot compile) |
| N7 | Note | The Tests table gave the wrong reason `AChangedItemRunsOnItsOwnKey` fails on `main` | Corrected under "What delivery changed" |
| N8 | Note | Plan 47's line on the owner's pending letters goes stale; `NoteHeaderTests` printed a `TextBlock`'s type name | The plan line is the lead's wave record; `NoteHeaderTests` names a row by its UIA name where its header is a `TextBlock` |
| N9, N10 | Notes | The shell journeys and the census comment need nothing; the CB-23 overlap holds | None |

## What delivery changed

Built on branch `claude/plan-47-cb22b-access-keys` from `main` 5e0846f5: the failing tests (eb208184), the product change (46e16971), the diff red team's test fixes (b9d8e2b4). The text above is the spec as ruled; this section says where the delivered change differs from it.

1. **Every part of the underscore facts is seen red on `main`.** The facts first asserted their readings and only then pressed a key, so on `main` the key-press and Alt-and-letter parts never ran (the diff red team's finding 3). Each fact now gathers every part into one list; where the desktop refuses the menu or the shell is not the keyboard's active source, the fact fails on any reading and skips only when the readings are clean. On `main` every part failed: the letter clicked the row and closed the menu, two folders that give N moved the keys between them, and Alt+B and Alt+N opened nothing while the Embeds leaf showed. The Folders fact's "the window's source registers nothing" check is gone (it passed on `main`).
2. **`EmbedPreviewTitleCensus` refuses inline text in the renderer**, which Design 5 did not have: a `TextBlock` header leaves a route the assignment reading cannot see (its `Inlines`, an `Inline` built there, a `TextPointer` insert). Seven mutation rows guard the new rule (the spec planned two). `EmbedTitleRealizedSurfaceTests` reads a `TextBlock`'s drawn text through a `TextRange`, not its `Text`.
3. **An accepted visual difference:** an explicit `TextBlock` takes its foreground from its logical parent, so Fluent's presenter triggers no longer dim the Jump label while it is pressed or a disabled card's title. Nothing disables either today; the PR names it.
4. **Wrong in this spec:** the Tests table says `AChangedItemRunsOnItsOwnKey` is red on `main` because "those letters run nothing or another item"; it reads each item's live key, so on `main` it types the old shared letter, which only moved the keys. And `T/NoteHeaderTests.cs` changes too (a row's name in two failure lines), which the spec's "existing tests edited" left out.
5. **Reviews.** The diff red team (Opus, xhigh): no blocker, two major, three minor, five notes (record above). Codex: "Verdict: approve" at round 1 on b9d8e2b4, no findings.
6. **Verification as run.** Red on `main`: 55 of 140 (the first run), then the restructured underscore facts 6 of 6 with every part failing. Green: 953 of 953 across the new facts and the regression classes; every census (1,682 cases with the touched facts). Mutations: sets A to E, each seen red and reverted. The whole Windows unit run with the app job's filter: 8,950 passed of 8,952, 2 skipped (the two `LayoutItemsControlTests` that skip on `main`), 0 failed.
7. **NVDA, as run** (worker A, NVDA 2026.3.1, punctuation "most"; four lock takes from 21:10 to 21:46; the report is `nvda-report.md`, in the principal's working notes, not committed; its phrases in PR #2055). Every expectation of the six steps held on head. Corrections to the spec's expectations: the embed card's Tab stop is its Expander group, whose own name is whole on both builds, so the stripped names on base are the header buttons and the Jump buttons; on head the second R of Alt+W, R, R, Enter typed "r" and the Enter made that line a setext heading ("heading level 2"), not a line break. Forward runs from the menu with nothing said beyond the focus lines, the same class as Clear Recents; it is added to #2051. Not reached by keys: New Canvas's key while an import runs and Cancel Import's while one runs (the hosted facts cover both).
8. **The PR.** #2055, opened as a draft at b9d8e2b4, `main` ef0c780f (a workflow time limit only) merged in as 11d2c1d5, ready for review after the NVDA pass. No closing references.
9. **The merge judge: MERGE AFTER FIXES** (`pr2055-judge.md`; the lead's rulings on it). F1 withdrew the acceptance of the diff red team's minor 5: the card's title and Jump label `TextBlock`s inherited their Expander's and Button's colour, so Fluent's pointer-over, pressed and disabled colours (High Contrast's Highlight and GrayText included) never reached them. Fixed red first: `AnEmbedCardsTitleAndJumpLabelPaintWhatTheirPresenterHolds` was red on 6ec5a8ca for all three readings (pressed Jump #FF171717 against the presenter's #FF3A3A3A; the disabled title and Jump #FF171717 against #FF6B6B6B); each `TextBlock` now binds its `Foreground` to the ancestor `ContentPresenter`'s `TextElement.Foreground`, set after construction so `EmbedPreviewTitleCensus` stays as strict; removing either binding turns that label's readings red (mutations F and G). The Folders rows are unaffected: `Slate.Menus.xaml`'s row template sets `Foreground` on the `MenuItem` itself (`:281-283`), which the row's `TextBlock` inherits. N1: the census's inline tripwire reads syntax (an Inline type where a type is named, a creation, and the members `Inlines` and `Insert*`), so `FontWeights.Bold` and `TextDecorations.Underline` pass (a fact red on the identifier tripwire) while `new Bold()` and a `Span?` declaration still fail. N2: the PR body gives the local command and result for the facts that skip on the hosted runners. F2 is the lead's, on #2050. Before these, `main` f363a597 (CB-23, an FFI change) was merged in as 6ec5a8ca and the bindings regenerated. Local after the fixes: 1,939 of 1,939 across every census and the touched classes, `EmbedTitleRealizedSurfaceTests` in all three themes. Head 7dec5b97. Count: found on the way 6, fixed here 5 (one of them in part), deferred 0, filed 2.
