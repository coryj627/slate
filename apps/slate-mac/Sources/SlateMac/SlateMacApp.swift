// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Entry point for the Slate Mac app.
///
/// The single window hosts a `RootView` that picks between the welcome
/// screen and the open-vault split view based on `AppState`. App-level
/// commands replace the default File menu. File ▸ Open is ⌘O = "Quick
/// Open…" (the documents users open all day are notes; #863) — on the
/// welcome screen it falls through to the vault picker, so the chord
/// works globally; "Open Vault…" itself lives on ⇧⌘O.
@main
struct SlateMacApp: App {
    @StateObject private var appState = AppState()

    init() {
        // Install the slate-core diagnostics sink once at startup (#507).
        // slate-core routes its non-fatal warnings through the Rust `log`
        // facade, which is a no-op until a host installs a sink; this is the
        // host's opt-in. `verbose` is DEBUG-only: release builds see
        // warn-level records (which carry no vault paths / note names),
        // while a debug build additionally gets the debug lines that do
        // carry paths, for local troubleshooting. Idempotent on the Rust
        // side, so a second `init()` (e.g. under SwiftUI previews) is safe.
        #if DEBUG
        initHostLogging(verbose: true)
        #else
        initHostLogging(verbose: false)
        #endif

        // Opt out of native window tabbing (windows.md "system windows
        // behave as people expect"): Slate ships its own workspace tab
        // strip, and the system's Show Tab Bar / Merge All Windows
        // items would nest that custom tab UI inside a native tab —
        // two tab metaphors in one window frame. Revisit if a real
        // multi-window story lands (Milestone W parity work).
        NSWindow.allowsAutomaticWindowTabbing = false
    }

    var body: some Scene {
        WindowGroup("Slate") {
            RootView()
                .environmentObject(appState)
                .frame(minWidth: 640, minHeight: 480)
        }
        .commands {
            CommandGroup(replacing: .newItem) {
                // ⇧⌘O (#863; was ⌘O): opening a vault is app-level and
                // rare — bare ⌘O now belongs to Quick Open below, the
                // Obsidian-parity quick-switcher chord. The welcome
                // screen's buttons and File ▸ Open Recent keep vault
                // opening one gesture away.
                Button("Open Vault…") {
                    appState.pickAndOpenVault()
                }
                .keyboardShortcut("o", modifiers: [.command, .shift])

                // Standard File ▸ Open Recent submenu (HIG: apps that
                // open documents/folders provide one, with Clear Menu
                // last). Reuses the same store + `openRecent` route as
                // the welcome screen and the utility-bar switcher.
                // Disabled (not hidden) when empty, like the system's.
                Menu("Open Recent") {
                    ForEach(appState.recentVaults) { entry in
                        Button(entry.displayName) {
                            appState.openRecent(entry)
                        }
                        .help(entry.path)
                    }
                    Divider()
                    Button("Clear Menu") {
                        appState.clearRecentVaults()
                    }
                }
                .disabled(appState.recentVaults.isEmpty)

                Divider()

                // File-management commands (U2-5, #463). Act on the file tree's
                // selected node; disabled without a vault (rename/move also
                // without a selection) so the chords aren't silent no-ops. ⌘⌫
                // (delete) is deliberately NOT here — it's tree-focused-only,
                // delivered by the tree's own key handling (spec §U2-5).
                Button("New Note") {
                    appState.newNoteCommand()
                }
                .keyboardShortcut("n", modifiers: [.command])
                .disabled(!appState.isVaultOpen)

                // ⇧⌘N migrated from the toolbar button's registration
                // (the #422 dead-zone: a toolbar keyboardShortcut is
                // unreachable with sidebar focus — the ⌘F lesson). The
                // toolbar button remains as the click/AX affordance;
                // the menu item is the single chord owner. Label
                // matches the palette registration (menu↔palette
                // naming parity).
                Button("New from Template…") {
                    appState.openTemplatePicker()
                }
                .keyboardShortcut("n", modifiers: [.command, .shift])
                .disabled(!appState.isVaultOpen)

                // No ellipsis (menus.md): creation is immediate
                // (inline rename follows) — the New Note flow.
                Button("New Folder") {
                    appState.newFolderCommand()
                }
                .disabled(!appState.isVaultOpen)

                Divider()

                // File ▸ Save (HIG: the File menu carries Save). ⌘S
                // previously lived ONLY on the toolbar button — the
                // same #422 dead-zone as ⌘F: dead with sidebar focus,
                // exactly when a keyboard user finishes tree work and
                // reflexively saves. Enablement mirrors the toolbar
                // button (a no-op Save stays visibly disabled).
                Button("Save") {
                    appState.saveCurrentNote()
                }
                .keyboardShortcut("s", modifiers: .command)
                .disabled(
                    appState.loadedFilePath == nil
                        || appState.isSaving
                        || !appState.hasUnsavedChanges
                )

                Divider()

                Button("Rename…") {
                    appState.renameSelectedCommand()
                }
                .keyboardShortcut("r", modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen || appState.treeSelectedNode == nil)

                Button("Move To…") {
                    appState.moveSelectedCommand()
                }
                .keyboardShortcut("m", modifiers: [.command, .shift])
                .disabled(!appState.isVaultOpen || appState.treeSelectedNode == nil)

                // Inspection pair — the primary-UI home the context-menu
                // rule requires (context-menus.md: context items must
                // also exist in the main interface). Selection-scoped
                // like Rename/Move To.
                Button("Reveal in Finder") {
                    appState.revealSelectedInFinderCommand()
                }
                .disabled(!appState.isVaultOpen || appState.treeSelectedNode == nil)

                Button("Copy Path") {
                    appState.copySelectedPathCommand()
                }
                .disabled(!appState.isVaultOpen || appState.treeSelectedNode == nil)

                // Duplicate (#853): file-only (folders are out of the
                // issue's scope), selection-scoped like the pair above.
                // No chord — ⌘D belongs to nothing here yet and the
                // #863 chord map stays untouched. The palette row and
                // the tree context menu are the other two homes
                // (context-menus.md redundancy rule).
                Button("Duplicate") {
                    appState.duplicateSelectedCommand()
                }
                .disabled(
                    !appState.isVaultOpen
                        || appState.treeSelectedNode == nil
                        || appState.treeSelectedNode?.isDirectory == true
                )

                Divider()

                // Workspace tab lifecycle (U1-2, #454). Menu items beat the
                // window's implicit ⌘W (performClose:) in AppKit's key-
                // equivalent order, which is exactly the override we want
                // inside a vault; Close Window remains reachable at ⌘⇧W.
                // Disabled (not hidden) without a vault so the shortcuts
                // aren't silent no-ops on the welcome screen.
                // Quick switcher (#495). ⌘O fuzzy-opens a note by name —
                // Obsidian's default quick-switcher chord AND the HIG-truer
                // File ▸ Open (#863 superseded #495's ⌘T choice; Obsidian
                // itself keeps ⌘T for the tab family). Enabled ALWAYS: with
                // no vault, `openQuickSwitcher()` falls through to the
                // vault picker (the requestCommandPalette welcome-guard
                // pattern, upgraded from an announcement), so ⌘O is never
                // dead on the welcome screen.
                Button("Quick Open…") {
                    appState.openQuickSwitcher()
                }
                .keyboardShortcut("o", modifiers: [.command])

                // ⌘T (#863): returned to the tab family as Duplicate Tab —
                // Slate's "new tab" verb, since a tab always hosts an item
                // (u1_spec §U1-2: there is no empty-tab page; if one ever
                // lands, it inherits ⌘T).
                Button("Duplicate Tab") {
                    appState.newTab()
                }
                .keyboardShortcut("t", modifiers: [.command])
                .disabled(!appState.isVaultOpen || appState.workspace.activeTab == nil)

                Button("Close Tab") {
                    appState.requestCloseTab()
                }
                .keyboardShortcut("w", modifiers: [.command])
                .disabled(!appState.isVaultOpen || appState.workspace.activeTab == nil)

                // ⇧⌘T (#863): Reopen Closed Tab, the macOS/Obsidian
                // convention next to Close Tab. Pops the per-vault-session
                // closed-tab stack (WorkspaceState.closedTabs) through the
                // standard open funnel — dedup and pane placement honored.
                // Disabled when the stack is empty; `canReopenClosedTab`
                // is AppState's published mirror of that emptiness
                // (predates the #868 objectWillChange bridge, which now
                // also forwards workspace publishes to this menu; the
                // mirror stays as the tested, named Bool surface).
                Button("Reopen Closed Tab") {
                    appState.reopenClosedTab()
                }
                .keyboardShortcut("t", modifiers: [.command, .shift])
                .disabled(!appState.isVaultOpen || !appState.canReopenClosedTab)

                Button("Close Window") {
                    NSApplication.shared.keyWindow?.performClose(nil)
                }
                .keyboardShortcut("w", modifiers: [.command, .shift])

                // Close Vault's menu-bar home (HIG: a Close-family
                // command for the primary open document belongs in
                // File). The utility-bar switcher and the palette
                // command remain; all three route through
                // `closeVaultFromUserAction` (U4-3 #472 funnel). No
                // chord — closing a vault is deliberate, not muscle-
                // memory (same rationale as the palette registration).
                Button("Close Vault") {
                    appState.closeVaultFromUserAction()
                }
                .disabled(!appState.isVaultOpen)
            }
            // Tab navigation lives under View alongside the palette/search
            // items — one menu for "where am I looking" commands.
            // #372: ⌘Z / ⇧⌘Z route by focus — the canvas undo stack
            // when a canvas surface owns the tab, the standard responder
            // chain (NSTextView's NSUndoManager) everywhere else. One
            // owner for the chord; the buttons forward faithfully so
            // note-editor undo behaves exactly as before.
            // #867: the titles carry the action name — NSUndoManager's
            // own localized composition on the responder path ("Undo
            // Typing"), the canvas stack's recorded verb when a canvas
            // surface owns the chord ("Undo Create card"). Rule
            // (undo-and-redo.md): "In menu items, use descriptive
            // labels"; the-menu-bar.md: "Undo | Append action name".
            // Focus ROUTING is #372, byte-for-byte unchanged — only
            // title and enablement are new. Re-render pulse:
            // appState.undoMenuTick (the #867 notification pipeline)
            // plus every other appState publish; the titles are
            // computed live at render.
            CommandGroup(replacing: .undoRedo) {
                Button(appState.undoMenuItemTitle) {
                    if appState.undoTargetsCanvas {
                        appState.canvasUndo()
                    } else {
                        NSApp.sendAction(Selector(("undo:")), to: nil, from: nil)
                    }
                }
                .keyboardShortcut("z", modifiers: [.command])
                .disabled(!appState.undoMenuItemEnabled)

                Button(appState.redoMenuItemTitle) {
                    if appState.undoTargetsCanvas {
                        appState.canvasRedo()
                    } else {
                        NSApp.sendAction(Selector(("redo:")), to: nil, from: nil)
                    }
                }
                .keyboardShortcut("z", modifiers: [.command, .shift])
                .disabled(!appState.redoMenuItemEnabled)
            }

            CommandGroup(after: .toolbar) {
                // U3-2 (#466): the single ⌘⇧E registration — the per-group
                // strip buttons carry the visual affordance without a
                // shortcut (duplicate shortcuts across split panes are
                // undefined in SwiftUI). Menu-bar placement also keeps the
                // chord alive with sidebar focus (the #422 lesson).
                // #868: changeable label (menus.md — "a single item
                // whose label changes with state"; the-menu-bar.md:
                // show/hide titles must reflect current state). The
                // title names the DIRECTION the action will take, read
                // from the ACTIVE tab's mode; re-renders via the #868
                // workspace→appState objectWillChange bridge. No tab
                // reads as .editing → "Enter Reading Mode" (the action
                // no-ops there; enablement scope unchanged from #466).
                // The PALETTE keeps the static "Toggle Reading Mode"
                // noun — searchable and state-free; the menu↔palette
                // registry invariant is CHORD parity, not label parity.
                Button(
                    appState.activeTabIsReading
                        ? "Exit Reading Mode" : "Enter Reading Mode"
                ) {
                    appState.toggleViewMode()
                }
                .keyboardShortcut("e", modifiers: [.command, .shift])
                .disabled(!appState.isVaultOpen)

                // U3-4 (#468): single ⌘⇧D owner, same rationale as ⌘⇧E.
                // #868 changeable label: Show ⇄ Hide from the published
                // mirror of the widget's view-local source-mode @State
                // (appState.propertiesSourceShowing; NotePropertiesHeader
                // remains the single mutator). No note → mirror false →
                // "Show Properties Source". Palette noun stays static,
                // as above.
                Button(
                    appState.propertiesSourceShowing
                        ? "Hide Properties Source" : "Show Properties Source"
                ) {
                    appState.togglePropertiesSourceCommand()
                }
                .keyboardShortcut("d", modifiers: [.command, .shift])
                .disabled(!appState.isVaultOpen)

                Button("Show Next Tab") {
                    appState.selectNextTab()
                }
                .keyboardShortcut("]", modifiers: [.command, .shift])
                .disabled(!appState.isVaultOpen)

                Button("Show Previous Tab") {
                    appState.selectPreviousTab()
                }
                .keyboardShortcut("[", modifiers: [.command, .shift])
                .disabled(!appState.isVaultOpen)

                Button("Move Tab Left") {
                    appState.moveActiveTabLeft()
                }
                .keyboardShortcut(.leftArrow, modifiers: [.control, .command])
                .disabled(!appState.isVaultOpen)

                Button("Move Tab Right") {
                    appState.moveActiveTabRight()
                }
                .keyboardShortcut(.rightArrow, modifiers: [.control, .command])
                .disabled(!appState.isVaultOpen)

                // M-3 (#534): panel-scoped command's menu home (the
                // registry invariant is menu↔palette unification; the
                // workspace-tabs precedent above is the normative home
                // for View-section commands). No hotkey — refresh is a
                // rare, deliberate action.
                // Title-style capitalization (menus.md) — these two were
                // sentence-case among Title-Case siblings.
                Button("Refresh Sync Diagnostics") {
                    appState.refreshSyncDiagnostics()
                }
                .disabled(!appState.isVaultOpen)

                // O-5 (#543): the history panel's menu home — same
                // View-section rule as the sync-diagnostics item above.
                Button("Show History Panel") {
                    appState.showHistoryPanel()
                }
                .disabled(!appState.isVaultOpen)

                Divider()

                // ⌘R / ⇧⌘J / ⌘J migrated from toolbar-button
                // registrations (the #422 dead-zone — see the File ▸
                // Save note). The toolbar buttons remain click/AX
                // affordances; each chord's single owner is its menu
                // item here. Labels match the palette registrations.
                // Enablement mirrors the corresponding toolbar button.
                // Verb-first menu labels (menus.md "verb or verb phrase
                // for action items") — the palette keeps the noun forms
                // ("Tasks Review") as its search-friendly names; the
                // registry invariant is chord parity, not label parity.
                // ⌘R (#863; was ⇧⌘T, freed for Reopen Closed Tab): bare
                // ⌘R was unbound, carries no system-wide macOS claim in
                // a non-browser app, and R = Review. Its R-family
                // neighbors are all differently scoped (⌥⌘R Rename,
                // ⇧⌘R Bulk Rename Properties, ⌃⌘R Canvas Resize).
                Button("Show Tasks Review") {
                    appState.openTasksReview()
                }
                .keyboardShortcut("r", modifiers: [.command])
                .disabled(appState.currentSession == nil)

                Button("Show Citation Summary") {
                    appState.isCitationSummaryOpen = true
                }
                .keyboardShortcut("j", modifiers: [.command, .shift])
                .disabled(appState.selectedFilePath == nil)

                Button("Jump to Bibliography") {
                    appState.jumpToBibliographyFromExpandedCitation()
                }
                .keyboardShortcut("j", modifiers: .command)
                .disabled(appState.expandedCitation == nil)

                Divider()

                // Milestone T (#518): the ⌃⌘I Where-am-I chord lives in
                // the menu bar (single owner, survives focus changes —
                // the #422 lesson). Palette equivalents exist for every
                // canvas command (program rule R1).
                Button("Canvas: Where Am I?") {
                    appState.canvasWhereAmI()
                }
                .keyboardShortcut("i", modifiers: [.control, .command])
                .disabled(!appState.isVaultOpen)

                // #368: ⌥⌘N New Card — canvas-scoped (⌘N stays New
                // Note; the allocation table keeps ⌘N free for notes).
                Button("Canvas: New Card") {
                    appState.canvasNewCard()
                }
                .keyboardShortcut("n", modifiers: [.command, .option])
                .disabled(appState.activeCanvasDocument == nil)

                Button("Canvas: Move Mode") {
                    appState.canvasEnterMoveMode()
                }
                .keyboardShortcut("g", modifiers: [.control, .command])
                .disabled(appState.activeCanvasDocument == nil)

                Button("Canvas: Resize Mode") {
                    appState.canvasCommitOrEnterResize()
                }
                .keyboardShortcut("r", modifiers: [.control, .command])
                .disabled(appState.activeCanvasDocument == nil)

                Button("Canvas: Connect To…") {
                    appState.canvasOpenConnectPicker()
                }
                .keyboardShortcut("c", modifiers: [.control, .command])
                .disabled(appState.activeCanvasDocument == nil)

                Button("Canvas: Toggle Mark") {
                    appState.canvasToggleMark()
                }
                .keyboardShortcut("m", modifiers: [.control, .command])
                .disabled(appState.activeCanvasDocument == nil)

                Button("Canvas: Create Connected Card") {
                    appState.canvasCreateConnectedCard()
                }
                .keyboardShortcut("n", modifiers: [.control, .option, .command])
                .disabled(appState.activeCanvasDocument == nil)

                // ⌘= / ⌘- / ⌘0 (#848; formerly the canvas-scoped "Canvas:
                // Zoom …" items, #520): the app's conventional zoom
                // chords, focus-routed EXACTLY like Undo/Redo above —
                // a canvas tab owns them (`canvasZoomIn()` etc., the
                // #520 viewport behavior unchanged); every other surface
                // gets editor text zoom (`editorZoomIn()` etc., a
                // persisted scale over the body-style base size).
                // One menu owner per chord; the registry keeps the
                // canvas-scoped commands as the palette equivalents
                // (canvas program rule R1) with these chords as their
                // hotkeyHints. Enabled whenever a vault is open, so ⌘=
                // is never a silent no-op while note-editing (#848's
                // founding complaint). Still one modifier apart from
                // the ⌥⌘=/⌥⌘- pane-grow chords.
                //
                // ZOOM BOUNDARY (documented decision, #848): editor
                // text zoom scales the monospaced EDITING surfaces —
                // the NSTextView note editor (active and parked panes),
                // the code-blocks panel, the properties-source YAML
                // editor, and the canvas card editor. Reading mode
                // deliberately does NOT zoom: its prose is Dynamic-Type-
                // backed and already tracks the system Text Size
                // (WCAG 1.4.4), so zooming it too would double-scale.
                // Obsidian's zoom is window-wide; Slate trades that
                // parity for not fighting the system setting.
                Button("Zoom In") {
                    if appState.activeCanvasDocument != nil {
                        appState.canvasZoomIn()
                    } else {
                        appState.editorZoomIn()
                    }
                }
                .keyboardShortcut("=", modifiers: [.command])
                .disabled(!appState.isVaultOpen)

                Button("Zoom Out") {
                    if appState.activeCanvasDocument != nil {
                        appState.canvasZoomOut()
                    } else {
                        appState.editorZoomOut()
                    }
                }
                .keyboardShortcut("-", modifiers: [.command])
                .disabled(!appState.isVaultOpen)

                Button("Actual Size") {
                    if appState.activeCanvasDocument != nil {
                        appState.canvasActualSize()
                    } else {
                        appState.editorActualSize()
                    }
                }
                .keyboardShortcut("0", modifiers: [.command])
                .disabled(!appState.isVaultOpen)

                Divider()

                // ⌘1…⌘9 select tab N (9 = last, macOS convention).
                ForEach(1..<10, id: \.self) { ordinal in
                    Button("Tab \(ordinal == 9 ? "9 (Last)" : String(ordinal))") {
                        appState.selectTab(ordinal: ordinal)
                    }
                    .keyboardShortcut(
                        KeyEquivalent(Character("\(ordinal)")), modifiers: [.command]
                    )
                    .disabled(!appState.isVaultOpen)
                }

                Divider()

                // Split panes (U1-3, #455).
                Button("Split Right") {
                    appState.splitActivePane(axis: .horizontal)
                }
                .keyboardShortcut("\\", modifiers: [.command])
                .disabled(!appState.isVaultOpen)

                Button("Split Down") {
                    appState.splitActivePane(axis: .vertical)
                }
                .keyboardShortcut("\\", modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen)

                Button("Focus Pane Left") {
                    appState.focusPane(.left)
                }
                .keyboardShortcut(.leftArrow, modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen)

                Button("Focus Pane Right") {
                    appState.focusPane(.right)
                }
                .keyboardShortcut(.rightArrow, modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen)

                Button("Focus Pane Above") {
                    appState.focusPane(.up)
                }
                .keyboardShortcut(.upArrow, modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen)

                Button("Focus Pane Below") {
                    appState.focusPane(.down)
                }
                .keyboardShortcut(.downArrow, modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen)

                Button("Grow Pane") {
                    appState.growFocusedPane()
                }
                .keyboardShortcut("=", modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen)

                Button("Shrink Pane") {
                    appState.shrinkFocusedPane()
                }
                .keyboardShortcut("-", modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen)
            }
            // Command palette — Milestone Q #313. The menu item
            // provides both the ⌘⇧P chord and the discoverable
            // "Show Command Palette…" entry. Closing is handled
            // exclusively by Esc inside the sheet (matches Xcode
            // / Sublime / TextMate convention; the alternative —
            // a hidden in-sheet ⌘⇧P button — would rely on SwiftUI
            // shortcut routing through the sheet's responder chain
            // and can't be unit-tested without XCUITest infra).
            //
            // Stays ENABLED on the welcome screen so ⌘⇧P isn't a
            // silent no-op there. The vault-scoped guard lives in
            // `requestCommandPalette()`: it only flips
            // `isCommandPaletteOpen` when a vault is open (no sheet
            // is mounted on the welcome screen, so flipping the bool
            // would re-trigger the palette on the next vault open —
            // the #313/#328 hazard); otherwise it announces "open a
            // vault first" so keyboard/VoiceOver users get feedback.
            CommandGroup(after: .sidebar) {
                Button("Show Command Palette…") {
                    appState.requestCommandPalette()
                }
                .keyboardShortcut("p", modifiers: [.command, .shift])
            }

            // Edit-menu additions. HIG places Find under Edit (Edit ▸
            // Find ▸ Find… ⌘F); the #422 requirement is only that the
            // chord be MENU-BAR-homed (reachable regardless of focus),
            // which any menu satisfies — so the HIG-correct Edit
            // placement costs nothing.
            CommandGroup(after: .textEditing) {
                // #422 (F-E1): Cmd+F lived only on the toolbar
                // button's keyboardShortcut, which proved
                // unreachable with focus in the sidebar (VO test).
                // AppKit's actual order is key-window sweep FIRST,
                // menu bar LAST — this works because nothing in the
                // app claims bare ⌘F during the sweep (no find
                // bar/panel is enabled; grep keyboardShortcut). If a
                // future change enables NSTextView's find bar, IT
                // will win ⌘F with editor focus and this menu item
                // needs revisiting. Vault-scoped guard pattern as
                // the palette item above (requestSearchOverlay).
                Button("Search Vault…") {
                    appState.requestFindInFocusedSurface()
                }
                .keyboardShortcut("f", modifiers: [.command])

                Divider()

                // ⇧⌘R migrated from NotePropertiesHeader's hidden
                // opacity-0 button (the #422 dead-zone pattern, plus
                // the chord silently vanished whenever the properties
                // header wasn't mounted). A vault-wide bulk edit is an
                // Edit-menu verb; label matches the palette
                // registration.
                Button("Bulk Rename Properties…") {
                    appState.isBulkRenameSheetOpen = true
                }
                .keyboardShortcut("r", modifiers: [.command, .shift])
                .disabled(appState.currentSession == nil)

                Divider()

                // #855: opt-in live spell checking. A menu-hosted
                // Toggle renders as a checkmark item; the pref is
                // `@Published` on appState, so the checkmark re-renders
                // live. No chord — Edit menu + palette
                // (`slate.editor.toggleSpellCheck`) only. Default OFF:
                // Markdown source red-squiggles fences/wikilinks/keys,
                // so prose writers opt in. Applied live through
                // `NoteEditorView.updateNSView`; the YAML
                // properties-source editor (`PlainTextEditor`) stays
                // always-off — structured text is never prose.
                Toggle(
                    "Check Spelling While Typing",
                    isOn: Binding(
                        get: { appState.editorSpellCheckEnabled },
                        set: { _ in appState.toggleEditorSpellCheck() }
                    )
                )
            }

            // Help ▸ Slate Help (HIG: the Help menu's first item is
            // "<AppName> Help"). Routes through the same `openHelp`
            // the palette and utility bar use. `replacing: .help`
            // rather than `after:` — replacing swaps only the ITEM
            // group; the system menu-command search field is AppKit-
            // injected into the app's help menu at display time and
            // SURVIVES the replacement (measured via AX on the
            // running app: AXTextField "search text field" + results
            // table render above "Slate Help"). `after:` would keep
            // SwiftUI's dead default "<App> Help" stub next to ours.
            CommandGroup(replacing: .help) {
                Button("Slate Help") {
                    appState.openHelp()
                }
            }
        }

        // Settings scene (#224) — Cmd+, opens it from anywhere.
        // SwiftUI auto-installs the "Slate ▸ Settings…" menu item
        // and the keyboard shortcut when the App declares a
        // `Settings` scene.
        Settings {
            SettingsView()
                .environmentObject(appState)
        }
    }
}

/// Top-level router: welcome screen until a vault is open, then the
/// split view. Lives next to the App entry point so the routing logic
/// is visible at a glance.
struct RootView: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        if appState.isVaultOpen {
            MainSplitView()
        } else {
            WelcomeView()
        }
    }
}
