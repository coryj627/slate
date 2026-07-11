// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Entry point for the Slate Mac app.
///
/// The single window hosts a `RootView` that picks between the welcome
/// screen and the open-vault split view based on `AppState`. App-level
/// commands replace the default File menu so the only first-class
/// action is "Open Vault…" (Cmd+O), which works globally regardless of
/// what's focused.
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
                Button("Open Vault…") {
                    appState.pickAndOpenVault()
                }
                .keyboardShortcut("o", modifiers: [.command])

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

                Divider()

                // Workspace tab lifecycle (U1-2, #454). Menu items beat the
                // window's implicit ⌘W (performClose:) in AppKit's key-
                // equivalent order, which is exactly the override we want
                // inside a vault; Close Window remains reachable at ⌘⇧W.
                // Disabled (not hidden) without a vault so the shortcuts
                // aren't silent no-ops on the welcome screen.
                // Quick switcher (#495). ⌘T fuzzy-opens a note by name.
                // Enabled whenever a vault is open — it doesn't need an
                // active tab (unlike Duplicate Tab below); `openQuickSwitcher()`
                // self-guards on the vault too.
                Button("Quick Open…") {
                    appState.openQuickSwitcher()
                }
                .keyboardShortcut("t", modifiers: [.command])
                .disabled(!appState.isVaultOpen)

                // ⌘T moved to Quick Open (#495); this keeps the duplicate-
                // current-note-into-a-new-tab behavior under a clearer label
                // with no hotkey.
                Button("Duplicate Tab") {
                    appState.newTab()
                }
                .disabled(!appState.isVaultOpen || appState.workspace.activeTab == nil)

                Button("Close Tab") {
                    appState.requestCloseTab()
                }
                .keyboardShortcut("w", modifiers: [.command])
                .disabled(!appState.isVaultOpen || appState.workspace.activeTab == nil)

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
            CommandGroup(replacing: .undoRedo) {
                Button("Undo") {
                    if appState.undoTargetsCanvas {
                        appState.canvasUndo()
                    } else {
                        NSApp.sendAction(Selector(("undo:")), to: nil, from: nil)
                    }
                }
                .keyboardShortcut("z", modifiers: [.command])

                Button("Redo") {
                    if appState.undoTargetsCanvas {
                        appState.canvasRedo()
                    } else {
                        NSApp.sendAction(Selector(("redo:")), to: nil, from: nil)
                    }
                }
                .keyboardShortcut("z", modifiers: [.command, .shift])
            }

            CommandGroup(after: .toolbar) {
                // U3-2 (#466): the single ⌘⇧E registration — the per-group
                // strip buttons carry the visual affordance without a
                // shortcut (duplicate shortcuts across split panes are
                // undefined in SwiftUI). Menu-bar placement also keeps the
                // chord alive with sidebar focus (the #422 lesson).
                Button("Toggle Reading Mode") {
                    appState.toggleViewMode()
                }
                .keyboardShortcut("e", modifiers: [.command, .shift])
                .disabled(!appState.isVaultOpen)

                // U3-4 (#468): single ⌘⇧D owner, same rationale as ⌘⇧E.
                Button("Show Properties Source") {
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

                // ⇧⌘T / ⇧⌘J / ⌘J migrated from toolbar-button
                // registrations (the #422 dead-zone — see the File ▸
                // Save note). The toolbar buttons remain click/AX
                // affordances; each chord's single owner is its menu
                // item here. Labels match the palette registrations.
                // Enablement mirrors the corresponding toolbar button.
                // Verb-first menu labels (menus.md "verb or verb phrase
                // for action items") — the palette keeps the noun forms
                // ("Tasks Review") as its search-friendly names; the
                // registry invariant is chord parity, not label parity.
                Button("Show Tasks Review") {
                    appState.openTasksReview()
                }
                .keyboardShortcut("t", modifiers: [.command, .shift])
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

                // #520 viewport chords: one modifier apart from the
                // ⌥⌘=/⌥⌘- pane-grow chords — the drift test asserts
                // both exist and differ. Disabled unless a canvas tab
                // is active, so note-editing keeps the keys free.
                Button("Canvas: Zoom In") {
                    appState.canvasZoomIn()
                }
                .keyboardShortcut("=", modifiers: [.command])
                .disabled(appState.activeCanvasDocument == nil)

                Button("Canvas: Zoom Out") {
                    appState.canvasZoomOut()
                }
                .keyboardShortcut("-", modifiers: [.command])
                .disabled(appState.activeCanvasDocument == nil)

                Button("Canvas: Actual Size") {
                    appState.canvasActualSize()
                }
                .keyboardShortcut("0", modifiers: [.command])
                .disabled(appState.activeCanvasDocument == nil)

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
