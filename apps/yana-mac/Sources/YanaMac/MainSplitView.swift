import SwiftUI

/// Split view shown once a vault is open.
///
/// Three-column layout: file list (#11) | note content (#45) | outline (#46).
/// `NavigationSplitView` with the `(sidebar, content, detail)` initializer
/// gives us system-styled column dividers, keyboard navigation between
/// columns (Cmd+1/2/3 via the System menu), and per-column collapse/
/// resize behaviour for free on macOS.
struct MainSplitView: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        NavigationSplitView {
            FileListSidebar()
        } content: {
            NoteContentView()
        } detail: {
            OutlineSidebar()
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
