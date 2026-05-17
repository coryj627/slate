import AppKit
import SwiftUI

/// Pre-vault landing surface. Hosts the "Open Vault…" entry point and
/// the error alert for failed opens. Focus lands on the Open button so
/// a VoiceOver user can Return-to-open immediately on launch.
struct WelcomeView: View {
    @EnvironmentObject private var appState: AppState
    @FocusState private var openButtonFocused: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 24) {
            heading

            Button("Open Vault…") {
                appState.pickAndOpenVault()
            }
            .controlSize(.large)
            .focused($openButtonFocused)
            .accessibilityLabel("Open vault")
            .accessibilityHint(
                "Shows a folder picker. The selected folder becomes your active vault."
            )

            Spacer()
        }
        .padding(40)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .onAppear {
            openButtonFocused = true
            postAccessibilityAnnouncement(
                "Welcome to YANA. Open Vault button focused. "
                    + "Press Return or Command-O to choose a folder of Markdown files."
            )
        }
        .alert(
            "Could not open vault",
            isPresented: errorAlertPresented,
            presenting: appState.lastError
        ) { _ in
            Button("OK", role: .cancel) { appState.lastError = nil }
        } message: { message in
            Text(message)
        }
    }

    // MARK: - Subviews

    private var heading: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("Welcome to YANA")
                .font(.largeTitle)
                .accessibilityAddTraits(.isHeader)
            Text("Open a folder of Markdown files to start.")
                .font(.title3)
                .foregroundStyle(.secondary)
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Welcome to YANA. Open a folder of Markdown files to start.")
    }

    // MARK: - Bindings

    private var errorAlertPresented: Binding<Bool> {
        Binding(
            get: { appState.lastError != nil },
            set: { showing in
                if !showing { appState.lastError = nil }
            }
        )
    }
}

/// Post a polite VoiceOver announcement at the app level.
///
/// SwiftUI's `AccessibilityNotification.Announcement` was added in
/// macOS 14; the app targets macOS 13, so this routes through AppKit's
/// `NSAccessibility.post` with `.announcementRequested`. Posting against
/// `NSApp` rather than a specific window lets the announcement fire even
/// before the main window has been keyed.
func postAccessibilityAnnouncement(_ message: String) {
    let userInfo: [NSAccessibility.NotificationUserInfoKey: Any] = [
        .announcement: message,
        .priority: NSAccessibilityPriorityLevel.medium.rawValue,
    ]
    let element: Any = NSApp.mainWindow ?? (NSApp as Any)
    NSAccessibility.post(
        element: element,
        notification: .announcementRequested,
        userInfo: userInfo
    )
}
