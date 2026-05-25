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
