import SwiftUI

/// Split view shown once a vault is open.
///
/// Sidebar carries the accessible file list (#11); the detail area is
/// still a placeholder until Milestone B brings the per-note reader.
/// What this view owns directly: the vault name in the toolbar title,
/// the Close Vault button, and the broader "vault X opened" VoiceOver
/// announcement.
struct MainSplitView: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        NavigationSplitView {
            FileListSidebar()
        } detail: {
            detailPlaceholder
        }
        .navigationTitle(vaultTitle)
        .toolbar {
            ToolbarItem(placement: .automatic) {
                Button("Close Vault") {
                    appState.closeVault()
                    postAccessibilityAnnouncement(
                        "Vault closed. Returned to the welcome screen."
                    )
                }
                .accessibilityHint(
                    "Returns to the welcome screen. The vault on disk is not modified."
                )
            }
        }
        .onAppear {
            postAccessibilityAnnouncement(
                "Vault \(vaultTitle) opened. Scanning files for the sidebar."
            )
        }
    }

    // MARK: - Subviews

    private var detailPlaceholder: some View {
        VStack(spacing: 8) {
            Spacer()
            if let selectedPath = appState.selectedFilePath {
                Text("Selected: \(selectedPath)")
                    .font(.headline)
                    .accessibilityAddTraits(.isHeader)
                Text("Reading lands in a follow-up milestone.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            } else {
                Text("Select a file to read.")
                    .foregroundStyle(.secondary)
                Text("Reading lands in a follow-up milestone.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Spacer()
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding()
    }

    private var vaultTitle: String {
        appState.currentVaultURL?.lastPathComponent ?? "Vault"
    }
}
