import AppKit
import SwiftUI
import UniformTypeIdentifiers

// Main content view.
//
// All Markdown parsing happens in the Rust core (via the
// auto-generated `extractHeadings` / `readHeadings` bindings).
// Swift only owns the UI shell, the file picker, and the
// accessibility annotations on each row.
//
// Accessibility approach (per docs/plans/05 §6.4 and §8 of this
// project's locked architecture): every interactive element has an
// explicit label and hint; each row in the heading list announces
// "Level N heading: <text>" rather than reading the row's visual
// chrome verbatim.

struct ContentView: View {
    @State private var headings: [Heading] = []
    @State private var sourceDescription: String = "Embedded sample"
    @State private var lastError: String? = nil

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            header

            if let lastError {
                errorBanner(lastError)
            }

            if headings.isEmpty {
                Text("No headings found in this source.")
                    .foregroundStyle(.secondary)
                    .accessibilityLabel("No headings found in the current source.")
            } else {
                Text("\(headings.count) heading\(headings.count == 1 ? "" : "s")")
                    .font(.subheadline)
                    .accessibilityLabel("\(headings.count) headings found.")

                List(headings.indices, id: \.self) { idx in
                    headingRow(headings[idx])
                }
            }
        }
        .padding()
        .onAppear(perform: loadEmbeddedSample)
    }

    // MARK: - Subviews

    private var header: some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text("Source")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Text(sourceDescription)
                    .font(.headline)
            }
            .accessibilityElement(children: .combine)
            .accessibilityLabel("Source: \(sourceDescription)")

            Spacer()

            Button("Open Markdown File…") {
                openFile()
            }
            .keyboardShortcut("o", modifiers: [.command])
            .accessibilityHint("Pick a Markdown file. The Rust core parses it and lists its headings.")
        }
    }

    private func errorBanner(_ message: String) -> some View {
        Text(message)
            .foregroundStyle(.red)
            .font(.callout)
            .padding(8)
            .background(Color.red.opacity(0.1))
            .accessibilityLabel("Error: \(message)")
    }

    private func headingRow(_ heading: Heading) -> some View {
        HStack(alignment: .firstTextBaseline) {
            Text("H\(heading.level)")
                .font(.system(.caption, design: .monospaced))
                .foregroundStyle(.secondary)
                .frame(width: 32, alignment: .leading)
                .accessibilityHidden(true)
            Text(heading.text)
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Level \(heading.level) heading: \(heading.text)")
    }

    // MARK: - Actions

    private func loadEmbeddedSample() {
        headings = extractHeadings(source: embeddedSample)
        sourceDescription = "Embedded sample"
        lastError = nil
    }

    private func openFile() {
        let panel = NSOpenPanel()
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false
        if let md = UTType(filenameExtension: "md") {
            panel.allowedContentTypes = [md, .plainText]
        }

        guard panel.runModal() == .OK, let url = panel.url else { return }

        do {
            let parsed = try readHeadings(path: url.path)
            headings = parsed
            sourceDescription = url.lastPathComponent
            lastError = nil
        } catch let error as VaultError {
            switch error {
            case .Io(let message),
                 .Db(let message),
                 .Trash(let message):
                lastError = message
            case .InvalidPath(let path, let reason):
                lastError = "Invalid path \(path): \(reason)"
            }
        } catch {
            lastError = error.localizedDescription
        }
    }
}

private let embeddedSample = """
# Hello, YANA

This is a paragraph.

## A subheading

With `inline code` in it.

### Deeper still

End of sample.
"""

#Preview {
    ContentView()
        .frame(width: 600, height: 400)
}
