import SwiftUI

/// Sidebar listing of Markdown files in the open vault.
///
/// SwiftUI's `List` is lazy under the hood (NSCollectionView on macOS),
/// so a vault with 10k+ files only renders the visible rows. Selection
/// is single-row; the selected file's path lives on `AppState` so a
/// future content view can react. Arrow keys, Return, and Tab to the
/// next region all work out of the box once the rows have a stable
/// `id` and bind to a selection binding.
struct FileListSidebar: View {
    @EnvironmentObject private var appState: AppState
    @State private var didAnnounceCount = false

    var body: some View {
        Group {
            if appState.isScanning && appState.files.isEmpty {
                scanningState
            } else if let error = appState.scanError {
                errorState(error)
            } else if appState.files.isEmpty {
                emptyState
            } else {
                fileList
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .navigationTitle("Files")
        // Two-arg .onChange(of:_:) is macOS 14+; sticking with the
        // one-arg form so the app keeps its declared macOS 13 minimum.
        .onChange(of: appState.isScanning) { scanning in
            // Announce once the scan finishes — at that point `files`
            // has been populated and N items is the count VoiceOver
            // should hear.
            if !scanning && !didAnnounceCount && appState.scanError == nil {
                didAnnounceCount = true
                postAccessibilityAnnouncement(
                    "File list, \(appState.files.count) "
                        + (appState.files.count == 1 ? "item" : "items")
                )
            }
        }
        .onChange(of: appState.currentVaultURL) { _ in
            // Each new vault gets its own count announcement.
            didAnnounceCount = false
        }
    }

    // MARK: - States

    private var scanningState: some View {
        VStack(spacing: 12) {
            ProgressView()
            Text("Scanning vault…")
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Scanning vault. The file list will appear when the scan finishes.")
    }

    private func errorState(_ message: String) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Could not load files")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Text(message)
                .foregroundStyle(.secondary)
        }
        .padding()
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    private var emptyState: some View {
        VStack(spacing: 8) {
            Text("No Markdown files in this vault.")
                .foregroundStyle(.secondary)
        }
        .padding()
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityLabel("No Markdown files in this vault.")
    }

    private var fileList: some View {
        List(appState.files, id: \.path, selection: $appState.selectedFilePath) { file in
            row(for: file)
        }
        .listStyle(.sidebar)
    }

    private func row(for file: FileSummary) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(file.name)
            Text("Modified \(relativeDate(for: file.mtimeMs))")
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(file.name), modified \(relativeDate(for: file.mtimeMs))")
        .help(file.path)
    }

    // MARK: - Helpers

    /// Cached so a vault of 10k rows doesn't allocate 10k formatters.
    /// RelativeDateTimeFormatter is thread-safe for `localizedString`
    /// reads, and we only mutate `unitsStyle` once at init.
    private static let relativeFormatter: RelativeDateTimeFormatter = {
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .full
        return formatter
    }()

    private func relativeDate(for mtimeMs: Int64) -> String {
        let date = Date(timeIntervalSince1970: TimeInterval(mtimeMs) / 1000)
        return Self.relativeFormatter.localizedString(for: date, relativeTo: Date())
    }
}
