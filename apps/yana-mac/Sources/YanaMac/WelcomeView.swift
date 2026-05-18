import AppKit
import SwiftUI

/// Pre-vault landing surface. Hosts the "Open Vault…" entry point, the
/// recent-vaults shortcut list, and the error alert for failed opens.
/// Focus lands on the Open button so a VoiceOver user can Return-to-open
/// immediately on launch.
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
            .accessibilityHint(
                "Shows a folder picker. The selected folder becomes your active vault."
            )

            if !appState.recentVaults.isEmpty {
                recentVaultsSection
            }

            Spacer()
        }
        .padding(40)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .onAppear {
            openButtonFocused = true
            postAccessibilityAnnouncement(welcomeAnnouncement)
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
        .alert(
            "Vault not found",
            isPresented: missingVaultAlertPresented,
            presenting: appState.missingRecentVault
        ) { entry in
            Button("Remove from recent vaults", role: .destructive) {
                appState.removeRecent(path: entry.path)
                appState.missingRecentVault = nil
                postAccessibilityAnnouncement(
                    "Removed \(entry.displayName) from recent vaults."
                )
            }
            Button("Keep in list", role: .cancel) {
                appState.missingRecentVault = nil
            }
        } message: { entry in
            Text(
                "\(entry.displayName) is no longer at \(entry.path). "
                    + "Remove it from your recent vaults?"
            )
        }
    }

    // MARK: - Subviews

    private var heading: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("Welcome to YANA")
                .font(.largeTitle)
                .accessibilityAddTraits(.isHeader)
            Text("Open a folder of Markdown files to start.")
                .font(.body)
                .foregroundStyle(.secondary)
        }
    }

    private var recentVaultsSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Recent Vaults")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)

            VStack(alignment: .leading, spacing: 4) {
                ForEach(appState.recentVaults) { entry in
                    recentVaultRow(entry)
                }
            }
        }
    }

    private func recentVaultRow(_ entry: RecentVault) -> some View {
        Button(action: { appState.openRecent(entry) }) {
            VStack(alignment: .leading, spacing: 2) {
                Text(entry.displayName)
                    .font(.body)
                Text("Last opened \(relativeDate(for: entry.lastOpenedMs))")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        // Tooltip surfaces the full path to sighted mouse users; the
        // accessibility hint does the same for VoiceOver. Keeping the
        // path out of the visible row avoids truncation at larger
        // Dynamic Type sizes (WCAG 1.4.4).
        .help(entry.path)
        .accessibilityLabel(accessibilityLabel(for: entry))
        .accessibilityHint("Opens this vault. Full path: \(entry.path)")
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

    private var missingVaultAlertPresented: Binding<Bool> {
        Binding(
            get: { appState.missingRecentVault != nil },
            set: { showing in
                if !showing { appState.missingRecentVault = nil }
            }
        )
    }

    // MARK: - Helpers

    private var welcomeAnnouncement: String {
        let base =
            "Welcome to YANA. Open Vault button focused. "
            + "Press Return or Command-O to choose a folder of Markdown files."
        let count = appState.recentVaults.count
        if count == 0 {
            return base
        }
        let noun = count == 1 ? "vault" : "vaults"
        return base + " \(count) recent \(noun) listed below."
    }

    private func accessibilityLabel(for entry: RecentVault) -> String {
        "\(entry.displayName), last opened \(relativeDate(for: entry.lastOpenedMs))"
    }

    private func relativeDate(for lastOpenedMs: Int64) -> String {
        let date = Date(timeIntervalSince1970: TimeInterval(lastOpenedMs) / 1000)
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .full
        return formatter.localizedString(for: date, relativeTo: Date())
    }
}

/// Post a polite VoiceOver announcement at the app level.
///
/// SwiftUI's `AccessibilityNotification.Announcement` was added in
/// macOS 14; the app targets macOS 13, so this routes through AppKit's
/// `NSAccessibility.post` with `.announcementRequested`. Posting against
/// `NSApp` rather than a specific window lets the announcement fire even
/// before the main window has been keyed.
///
/// In a unit-test runner there's no `NSApp` (no run loop, no app
/// shared instance) — calling through anyway crashes with an
/// implicitly-unwrapped optional. Guard explicitly so `AppState`
/// tests that drive scan-progress events can run without crashing.
func postAccessibilityAnnouncement(_ message: String) {
    guard let element: Any = NSApp?.mainWindow ?? NSApp else { return }
    let userInfo: [NSAccessibility.NotificationUserInfoKey: Any] = [
        .announcement: message,
        .priority: NSAccessibilityPriorityLevel.medium.rawValue,
    ]
    NSAccessibility.post(
        element: element,
        notification: .announcementRequested,
        userInfo: userInfo
    )
}
