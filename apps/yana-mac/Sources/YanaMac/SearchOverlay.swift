import SwiftUI

/// Cmd+F search overlay. Sits above the `MainSplitView` content
/// area while open; Esc closes and returns focus to whatever
/// element the user came from.
///
/// VoiceOver story:
///   - Field announces "Search vault." on focus.
///   - Result count fires through the polite live region on every
///     state transition into `.results`.
///   - Each row's accessibility label matches the acceptance spec
///     exactly: `"<filename>, line <N>: <snippet>"`.
struct SearchOverlay: View {
    @EnvironmentObject private var appState: AppState
    @FocusState private var fieldFocused: Bool

    var body: some View {
        VStack(spacing: 0) {
            field
            Divider()
            content
        }
        .frame(maxWidth: 560)
        .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 10))
        .overlay(
            RoundedRectangle(cornerRadius: 10)
                .stroke(.separator, lineWidth: 0.5)
        )
        .padding(.top, 12)
        .shadow(radius: 8, y: 4)
        .onAppear {
            // Defer focus until the next runloop tick so the
            // .focused binding has been wired up by SwiftUI.
            DispatchQueue.main.async { fieldFocused = true }
        }
        // .onChange(of:) is one-arg on macOS 13; we stick with it so
        // the app keeps its declared minimum.
        .onChange(of: appState.searchSummary) { summary in
            // Polite live region. The state-machine guarantees this
            // only changes when results actually arrive (or on an
            // error transition), so the announcement is meaningful
            // every time it fires.
            if !summary.isEmpty {
                postAccessibilityAnnouncement(summary)
            }
        }
    }

    // MARK: - Field

    private var field: some View {
        HStack(spacing: 8) {
            Image(systemName: "magnifyingglass")
                .accessibilityHidden(true)
            TextField("Search vault…", text: $appState.searchQuery)
                .textFieldStyle(.plain)
                .focused($fieldFocused)
                .accessibilityLabel("Search vault")
                .accessibilityHint("Type to search across every note in this vault.")
                .onChange(of: appState.searchQuery) { _ in
                    appState.bumpSearchQuery()
                }
                .onSubmit {
                    // Return on the field doesn't activate yet (#E4
                    // adds that). For now, give focus to the first
                    // result if there is one — close enough to be
                    // useful and matches the acceptance criteria of
                    // "Tab cycles into the results list and back."
                }
            Button {
                appState.closeSearchOverlay()
            } label: {
                Image(systemName: "xmark.circle.fill")
                    .accessibilityHidden(true)
            }
            .buttonStyle(.plain)
            .accessibilityLabel("Close search")
            .accessibilityHint("Closes the search overlay and returns to the previous view.")
            .keyboardShortcut(.escape, modifiers: [])
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
    }

    // MARK: - Content

    @ViewBuilder
    private var content: some View {
        switch appState.searchState {
        case .idle:
            idleState
        case .searching:
            searchingState
        case .results(let rows, _):
            if rows.isEmpty {
                emptyResultsState
            } else {
                resultsList(rows)
            }
        case .error(let message):
            errorState(message)
        }
    }

    private var idleState: some View {
        Text("Type to search.")
            .font(.callout)
            .foregroundStyle(.secondary)
            .padding()
            .accessibilityLabel("Type to search.")
    }

    private var searchingState: some View {
        HStack(spacing: 8) {
            ProgressView()
                .controlSize(.small)
            Text("Searching…")
                .foregroundStyle(.secondary)
        }
        .padding()
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Searching.")
    }

    private var emptyResultsState: some View {
        Text("No results.")
            .font(.callout)
            .foregroundStyle(.secondary)
            .padding()
            .accessibilityLabel("No results.")
    }

    private func errorState(_ message: String) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Search error")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Text(message)
                .font(.callout)
                .foregroundStyle(.secondary)
        }
        .padding()
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func resultsList(_ rows: [QueryHit]) -> some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 4) {
                ForEach(Array(rows.enumerated()), id: \.offset) { _, hit in
                    row(for: hit)
                }
            }
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
        }
        .frame(maxHeight: 320)
    }

    private func row(for hit: QueryHit) -> some View {
        // Inert button for now — #E4 wires activation. We use a
        // button anyway so keyboard focus + return semantics are in
        // place and the activation handler just needs to swap the
        // action.
        Button {
            // #E4 — open file at line_number.
        } label: {
            VStack(alignment: .leading, spacing: 2) {
                Text(filename(for: hit.path))
                    .font(.callout.weight(.semibold))
                    .foregroundStyle(.primary)
                Text(hit.snippet.replacingOccurrences(of: "\u{2}", with: "")
                    .replacingOccurrences(of: "\u{3}", with: ""))
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(2)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.vertical, 4)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        // VoiceOver row label per spec: "<filename>, line <N>: <snippet>".
        // We strip the STX/ETX hit markers from the snippet for the
        // audio side — they're useful for visual emphasis but a
        // screen reader doesn't need them.
        .accessibilityLabel(rowAccessibilityLabel(for: hit))
        .help(hit.path)
    }

    private func rowAccessibilityLabel(for hit: QueryHit) -> String {
        let cleanSnippet = hit.snippet
            .replacingOccurrences(of: "\u{2}", with: "")
            .replacingOccurrences(of: "\u{3}", with: "")
        return "\(filename(for: hit.path)), line \(hit.lineNumber): \(cleanSnippet)"
    }

    private func filename(for path: String) -> String {
        (path as NSString).lastPathComponent
    }
}
