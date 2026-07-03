# U4 — Right-hand leaves + utility rail

**Goal.** Give the workspace an Obsidian-style right pane: a set of switchable "leaves" chosen from a vertical icon rail, hosting the panels that used to stack in the left sidebar (now that Properties has moved into the note). Move the bottom-left utilities (Settings, Help, Vault switcher) to icon buttons. The left sidebar becomes, cleanly, just the file tree.

**Depends on:** U1 (leaves reflect the active tab; live in the workspace). **Parallel:** U3.

**Milestone-level risk:** medium. State retention across leaf switches and focus routing between the editor pane and the right pane are the tricky parts — reuse the established mounted-`ZStack` retention pattern from the current sidebar tabs.

## Issues

### U4-1 · Mac UI: right-pane leaf container + vertical icon rail `swift-ui` `a11y` `design`
- A right pane with a vertical icon rail selecting the active leaf; leaves stay mounted for state retention (reuse the `opacity` + `allowsHitTesting` + `accessibilityHidden` gating already used for the Outline/Citations/Bibliography tabs, which was chosen specifically to preserve per-panel `@State` and avoid re-fire IO). Leaf content reflects the focused editor tab.
- **DoD focus:** each rail item is an icon (via `SlateSymbol`) + accessible label + `help` tooltip; the rail is a labeled region with a segmented/radio semantic; only the visible leaf is in the AX tree; light/dark.
- **Tests:** state retained across switches (no re-fire of loads); only-visible-leaf AX scoping; rail keyboard navigation; appearance snapshots. a11y-check 100/100.
- **Acceptance:** switching leaves is keyboard-navigable, retains each leaf's state, and never leaks hidden leaves into VoiceOver.

### U4-2 · Mac UI: port panels to leaves; retire the left-sidebar stack `swift-ui` `a11y`
- Move Outline, Backlinks, Outgoing links, Embeds, Math/Code/Diagrams, Tasks, Citations, Bibliography into leaves. Remove the left-sidebar `ScrollView` panel stack. Preserve each panel's existing behavior and AX.
- **DoD focus:** each leaf keeps its current accessibility and interactions; no regression to Milestone K/L surfaces; the left sidebar now contains only the file tree.
- **Acceptance:** every panel that lived in the left sidebar is reachable as a right-pane leaf with identical capability; the left sidebar is just the tree.

### U4-3 · Mac UI: bottom-left utility icon buttons `swift-ui` `a11y` `design`
- Settings, Help, and Vault switcher become icon buttons (menus/popovers) at the bottom of the left sidebar — mirroring the Obsidian layout. Reuse the existing Settings scene and vault-switch flows.
- **DoD focus:** each icon button labeled + `help`; menus keyboard-operable; light/dark; icons via `SlateSymbol`.
- **Acceptance:** the utilities are reachable as labeled icon buttons by keyboard and VoiceOver; behavior unchanged from today's menu/command paths.

### U4-4 · Mac UI: split/tab-aware leaf context + editor↔right-pane focus routing `swift-ui` `a11y`
- Leaves recompute against the focused editor tab (switching tabs/panes updates Outline/Backlinks/etc.); ⌘⌥arrow moves focus between the editor pane and the right pane consistently with U1-3.
- **Acceptance:** leaves always describe the focused document; focus moves predictably between editor and right pane by keyboard, with no trap.
