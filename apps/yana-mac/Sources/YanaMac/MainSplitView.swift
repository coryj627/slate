import SwiftUI

/// Split view shown once a vault is open.
///
/// Sidebar carries the accessible file list (#11); detail pane is
/// `NoteContentView` (#45), which loads the selected note's raw
/// Markdown source. Outline panel + heading rotor land in #46.
struct MainSplitView: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        NavigationSplitView {
            FileListSidebar()
        } detail: {
            NoteContentView()
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

    private var vaultTitle: String {
        appState.currentVaultURL?.lastPathComponent ?? "Vault"
    }
}
