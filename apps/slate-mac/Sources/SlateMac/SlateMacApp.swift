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
                    appState.requestSearchOverlay()
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
