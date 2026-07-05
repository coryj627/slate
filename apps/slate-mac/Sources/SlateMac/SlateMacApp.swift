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

                Button("New Folder…") {
                    appState.newFolderCommand()
                }
                .disabled(!appState.isVaultOpen)

                Button("Rename…") {
                    appState.renameSelectedCommand()
                }
                .keyboardShortcut("r", modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen || appState.treeSelectedNode == nil)

                Button("Move to…") {
                    appState.moveSelectedCommand()
                }
                .keyboardShortcut("m", modifiers: [.command, .shift])
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
                Button("Refresh sync diagnostics") {
                    appState.refreshSyncDiagnostics()
                }
                .disabled(!appState.isVaultOpen)

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
                    // #373 allocation: ⌘F is the in-canvas filter while
                    // a canvas has focus; the vault overlay otherwise.
                    if appState.activeCanvasDocument != nil {
                        appState.canvasFocusFilter()
                    } else {
                        appState.requestSearchOverlay()
                    }
                }
                .keyboardShortcut("f", modifiers: [.command])
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
