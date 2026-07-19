// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

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
            // Prominent, not merely large (buttons.md: "use a prominent
            // style for the most likely action… distinguish preferred
            // actions by style, not size") — this is the screen's single
            // primary action and was the app's only unprominent primary.
            .buttonStyle(.borderedProminent)
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
            // #872: on a cold launch that's about to auto-restore the
            // last vault, WelcomeView renders for a single frame before
            // the vault takes over — don't announce "Open Vault focused"
            // into that flash (the vault-open scan announcement follows).
            // The not-found and no-restore paths genuinely land here, so
            // they keep the announcement.
            if !appState.willRestoreVaultOnLaunch {
                postAccessibilityAnnouncement(
                    .welcomeShown(
                        recentVaultCount: UInt32(appState.recentVaults.count)))
            }
        }
        .alert(
            "Could Not Open Vault",
            isPresented: errorAlertPresented,
            presenting: appState.lastError
        ) { _ in
            Button("OK", role: .cancel) { appState.lastError = nil }
        } message: { message in
            Text(message)
        }
        .alert(
            "Vault Not Found",
            isPresented: missingVaultAlertPresented,
            presenting: appState.missingRecentVault
        ) { entry in
            Button("Remove from Recent Vaults", role: .destructive) {
                appState.removeRecent(path: entry.path)
                appState.missingRecentVault = nil
                postAccessibilityAnnouncement(
                    .removedRecentVault(displayName: entry.displayName))
            }
            Button("Keep in List", role: .cancel) {
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
            Text("Welcome to Slate")
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
/// Routes through AppKit's `NSAccessibility.post` with
/// `.announcementRequested` rather than SwiftUI's
/// `AccessibilityNotification.Announcement`. Posting against `NSApp`
/// rather than a specific window lets the announcement fire even
/// before the main window has been keyed — and keeps the test-runner
/// guard below workable. Both reasons stand at the macOS 15 floor, so
/// this stays AppKit even though the SwiftUI API is now available.
///
/// In a unit-test runner there's no `NSApp` (no run loop, no app
/// shared instance) — calling through anyway crashes with an
/// implicitly-unwrapped optional. Guard explicitly so `AppState`
/// tests that drive scan-progress events can run without crashing.
func postAccessibilityAnnouncement(
    _ message: String,
    priority: NSAccessibilityPriorityLevel = .medium
) {
    guard let element: Any = NSApp?.mainWindow ?? NSApp else { return }
    let userInfo: [NSAccessibility.NotificationUserInfoKey: Any] = [
        .announcement: message,
        .priority: priority.rawValue,
    ]
    NSAccessibility.post(
        element: element,
        notification: .announcementRequested,
        userInfo: userInfo
    )
}
