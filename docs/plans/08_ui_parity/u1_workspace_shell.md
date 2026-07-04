# U1 — Workspace shell: tabs + split panes

**Status: ✅ Complete (2026-07-03).** All six issues shipped and merged: U1-1 WorkspaceModel (#453 → PR #490), U1-4 MainSplitView migration (#456 → #491), U1-2 tab bar + per-tab documents (#454 → #492), U1-3 split panes + focus routing (#455 → #494), U1-5 open-in targets (#457 → #496), U1-6 session restore (#458 → #498, promoted from stretch). Executed order U1-1→U1-4→U1-2→U1-3→U1-5→U1-6 (migration before tab UI). Architecture as amended in specs/u1_spec.md: AppState's single-note fields ARE the active tab's document; parked `NoteDocument`s + the `activateTab` identity funnel make the buffer-under-wrong-tab class structurally impossible. Quick switcher deferred to #495. 800-seed model censuses (two real bugs pre-merge: clamp oscillation → sticky waterfill; I5 breach → global 6-group cap).

**Goal.** Replace the single-note center column with a real workspace: multiple documents as tabs, side-by-side split panes, and a typed tab-content abstraction so today's markdown editor — and tomorrow's Bases / Canvas / Graph — are just *kinds* of tab. This is the largest structural change in the program and the seam that lets Milestones N/T/P plug in without bespoke windows.

**Depends on:** U0. **Parallel:** U2. **Unblocks:** U3, U4.

**Milestone-level risk:** high. Split-pane focus routing (U1-3) is the single most a11y-sensitive surface in the program — census it hardest. The migration (U1-4) must be strictly behavior-preserving.

## Issues

### U1-1 · Model: `WorkspaceModel` — split-tree / tab-groups / tabs + typed `EditorItem` `swift-ui` `test`
- A pure model: a tree of split nodes (H/V) → tab groups → ordered tabs, each tab holding a typed `EditorItem`. Ship the `.markdown(path)` case now; **reserve `.base`, `.canvas`, `.graph`** cases (Milestones N/T/P) so those milestones add a renderer, not a shell.
- Invariants: exactly one active tab per group, exactly one active group, no orphan/empty split nodes (collapse on last-tab-close), focus always resolvable.
- **Tests / census:** adversarial census over open/close/split/collapse/move-tab sequences asserting the invariants hold (random + exhaustive small-N). No `debug_assert`-only guarantees.
- **Acceptance:** the model can represent every layout U1-2/U1-3 need and never reaches an unfocusable or orphaned state.

### U1-2 · Mac UI: tab bar + tab lifecycle `swift-ui` `a11y` `design`
- Tab strip: open, close (⌘W), new (⌘T), reorder, next/prev (⌘⇧[ / ⌘⇧]), select N (⌘1…⌘9); per-tab dirty indicator reusing the Modified/Saved semantics.
- **DoD focus:** tab semantics + traits, "tab N of M, <title>, modified" VoiceOver value, keyboard reorder alternative to drag, focus routing to a sensible neighbor on close; light/dark correct; icons via `SlateSymbol`.
- **Tests:** lifecycle + shortcut coverage; VoiceOver value strings; reorder without a mouse; appearance snapshots (light/dark). a11y-check 100/100.
- **Acceptance:** a keyboard/VoiceOver user can open, switch, reorder, and close tabs and always knows which tab is active and whether it's modified.

### U1-3 · Mac UI: split panes (H/V, focus routing, keyboard-resizable) `swift-ui` `a11y`
- Split the active group horizontally/vertically into side-by-side tab groups; move focus between panes (⌘⌥←/→/↑/↓); resize dividers by keyboard as well as drag; close/collapse a pane.
- **DoD focus:** each pane is a labeled region; focus movement is predictable and announced; no keyboard trap between panes; save-status/toolbar reflect the *focused* pane's active tab.
- **Tests / RED-TEAM census:** adversarial census on focus routing across arbitrary split/close/move sequences — focus must never be lost, duplicated, or land on a hidden pane. Divider keyboard-resize bounds.
- **Acceptance:** a non-visual user can create a split, move between panes by keyboard, and never lose track of where focus is.

### U1-4 · Mac UI: migrate `MainSplitView` center into the workspace region (behavior-preserving) `swift-ui` `a11y`
- Reparent today's single-note flow so the center column is the workspace with exactly one tab holding the selected note; all existing behavior (save, conflict alerts, embed popover, outline scroll) preserved.
- **Acceptance:** with a single tab and no split, the app is behaviorally identical to today (regression suite green); the change is purely structural.

### U1-5 · Mac UI: open-in-new-tab / open-in-split affordances + active-tab wiring `swift-ui` `a11y`
- File tree rows, wikilinks/backlinks, and command palette gain "open", "open in new tab", "open in split" (keyboard + context menu + palette). Toolbar Save/Search/status bind to the focused tab.
- Register the new actions in the command registry (palette-mirrored) so they're keyboard-reachable.
- **Acceptance:** every navigation entry point can target current tab / new tab / split by keyboard; toolbar acts on the right document.

### U1-6 · Mac UI: session restoration — persist & restore tabs + splits `swift-ui` *(optional / stretch)*
- Persist the workspace layout per vault; restore open tabs and split arrangement on relaunch; degrade gracefully if a file is gone.
- **Acceptance:** relaunching a vault restores the prior tab/split layout; missing files surface a clear per-tab error state, not a crash.
