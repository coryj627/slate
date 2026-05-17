import SwiftUI

/// Skeleton split view shown once a vault is open.
///
/// Sidebar and detail area are intentional placeholders for this
/// milestone — the accessible file list lands in issue #11 and per-note
/// reading lands in Milestone B. What this view does carry already:
/// the vault name in the toolbar title, a Close Vault button, and
/// announcement labels so VoiceOver users hear what regions exist even
/// while they're empty.
struct MainSplitView: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        NavigationSplitView {
            sidebarPlaceholder
                .navigationTitle("Files")
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
                "Vault \(vaultTitle) opened. File list region is empty for this build."
            )
        }
    }

    // MARK: - Subviews

    private var sidebarPlaceholder: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("File list")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Text("Accessible file list lands in a follow-up issue.")
                .foregroundStyle(.secondary)
            Spacer()
        }
        .padding()
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("File list, not yet implemented.")
    }

    private var detailPlaceholder: some View {
        VStack(spacing: 8) {
            Spacer()
            Text("Select a file to read.")
                .foregroundStyle(.secondary)
            Text("Reading lands in a follow-up milestone.")
                .font(.caption)
                .foregroundStyle(.secondary)
            Spacer()
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Detail area; file viewing is not yet implemented.")
    }

    private var vaultTitle: String {
        appState.currentVaultURL?.lastPathComponent ?? "Vault"
    }
}
