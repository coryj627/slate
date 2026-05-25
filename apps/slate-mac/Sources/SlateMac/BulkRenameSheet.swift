import SwiftUI

/// Sheet for renaming a property key across every file in the vault.
/// Two-step flow: Preview → Apply. The preview renders the
/// `RenameReport` in the accessible data grid; Apply re-runs the
/// rename with `dryRun = false`. Esc cancels an in-flight call and
/// closes the sheet.
///
/// Default focus is on Preview (not Apply) so an accidental Return
/// can't fire the destructive write step.
struct BulkRenameSheet: View {
    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss

    @State private var oldKey: String = ""
    @State private var newKey: String = ""
    /// True when the user has hit Preview at least once. Apply is
    /// disabled until then. Cleared when the user edits either key
    /// (the preview becomes stale).
    @State private var previewLoaded: Bool = false
    /// True after the user hits Apply. Drives the summary footer's
    /// "X renamed / X skipped / X failed" wording.
    @State private var applied: Bool = false

    @FocusState private var focusedField: Field?

    enum Field: Hashable {
        case oldKey
        case newKey
        case preview
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Rename property across the vault")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)

            HStack(spacing: 12) {
                VStack(alignment: .leading, spacing: 4) {
                    Text("Old key")
                        .font(.caption)
                    TextField("e.g. author", text: $oldKey)
                        .textFieldStyle(.roundedBorder)
                        .accessibilityLabel("Old property key")
                        .focused($focusedField, equals: .oldKey)
                        .onChange(of: oldKey) { _ in invalidatePreview() }
                }
                VStack(alignment: .leading, spacing: 4) {
                    Text("New key")
                        .font(.caption)
                    TextField("e.g. by", text: $newKey)
                        .textFieldStyle(.roundedBorder)
                        .accessibilityLabel("New property key")
                        .focused($focusedField, equals: .newKey)
                        .onChange(of: newKey) { _ in invalidatePreview() }
                }
            }

            if appState.isRenameInFlight {
                ProgressView(applied ? "Applying rename…" : "Computing preview…")
                    .accessibilityLabel(applied ? "Applying rename" : "Computing preview")
            }

            if let err = appState.renameError {
                Text(err)
                    .font(.caption)
                    .foregroundStyle(.red)
                    .accessibilityLabel("Error: \(err)")
            }

            if let report = appState.pendingRenameReport {
                AccessibleDataGrid<RenameGridRow>(
                    columns: [
                        .init("Path") { $0.path },
                        .init("Status") { $0.status },
                        .init("Before") { $0.before },
                        .init("After") { $0.after },
                    ],
                    rows: gridRows(from: report),
                    summary: summary(from: report)
                )
            } else {
                // WCAG 2.5.3: the AX label must begin with the
                // visible text so speech-control activation matches
                // the visible string. (CI's a11y-check catches this
                // when prefix-before-visible-text drops the score.)
                Text("Run a preview to see which files would change.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .accessibilityLabel("Run a preview to see which files would change.")
            }

            HStack {
                Spacer()
                Button("Cancel", role: .cancel) {
                    appState.cancelPendingRename()
                    dismiss()
                }
                .keyboardShortcut(.cancelAction)
                Button("Preview") {
                    runPreview()
                }
                .disabled(
                    appState.isRenameInFlight
                        || oldKey.trimmingCharacters(in: .whitespaces).isEmpty
                        || newKey.trimmingCharacters(in: .whitespaces).isEmpty
                )
                .focused($focusedField, equals: .preview)
                Button("Apply") {
                    runApply()
                }
                .disabled(!previewLoaded || appState.isRenameInFlight)
            }
        }
        .padding(20)
        .frame(minWidth: 720, minHeight: 460)
        .onAppear {
            // Prefill old-key from the currently-focused properties
            // row if any. Falls back to empty.
            if oldKey.isEmpty {
                oldKey = appState.currentNoteProperties.first?.key ?? ""
            }
            focusedField = .oldKey
            postAccessibilityAnnouncement("Bulk rename property", priority: .high)
        }
        .onDisappear {
            appState.cancelPendingRename()
        }
    }

    private func runPreview() {
        let o = oldKey.trimmingCharacters(in: .whitespaces)
        let n = newKey.trimmingCharacters(in: .whitespaces)
        guard !o.isEmpty, !n.isEmpty else { return }
        applied = false
        appState.previewPropertyRename(oldKey: o, newKey: n)
        previewLoaded = true
    }

    private func runApply() {
        let o = oldKey.trimmingCharacters(in: .whitespaces)
        let n = newKey.trimmingCharacters(in: .whitespaces)
        guard !o.isEmpty, !n.isEmpty else { return }
        applied = true
        appState.applyPropertyRename(oldKey: o, newKey: n)
    }

    private func invalidatePreview() {
        // Don't clear the existing report mid-edit — the grid stays
        // visible while the user adjusts the key — but disable the
        // Apply button so they can't apply against stale preview
        // data after retyping.
        previewLoaded = false
    }

    private func gridRows(from report: RenameReport) -> [RenameGridRow] {
        var out: [RenameGridRow] = []
        for a in report.affected {
            out.append(
                RenameGridRow(
                    id: "affected-\(a.path)",
                    path: a.path,
                    status: a.applied ? "Applied" : "Will apply",
                    before: a.beforeExcerpt,
                    after: a.afterExcerpt
                )
            )
        }
        for s in report.skipped {
            out.append(
                RenameGridRow(
                    id: "skipped-\(s.path)",
                    path: s.path,
                    status: "Skipped: \(label(for: s.reason))",
                    before: "",
                    after: ""
                )
            )
        }
        for f in report.failed {
            out.append(
                RenameGridRow(
                    id: "failed-\(f.path)",
                    path: f.path,
                    status: "Failed: \(label(for: f.kind)): \(f.message)",
                    before: "",
                    after: ""
                )
            )
        }
        return out
    }

    private func summary(from report: RenameReport) -> String {
        if applied {
            let renamed = report.affected.filter { $0.applied }.count
            let skipped = report.skipped.count
            let failed = report.failed.count
            return "\(renamed) renamed, \(skipped) skipped, \(failed) failed."
        } else {
            let will = report.affected.count
            let skipped = report.skipped.count
            return "\(will) \(will == 1 ? "file" : "files") will be renamed, \(skipped) skipped, 0 errors."
        }
    }

    private func label(for reason: RenameSkipReason) -> String {
        switch reason {
        case .noSuchKey: return "key not present"
        case .keyCollision: return "new key already exists"
        case .tagsKeyTypeDrift: return "would change tags / list type"
        }
    }

    private func label(for kind: RenameFailureKind) -> String {
        switch kind {
        case .writeConflict: return "external write"
        case .malformedFrontmatter: return "malformed YAML"
        case .cancelled: return "cancelled"
        case .other: return "error"
        }
    }
}

/// One row in the preview / post-apply grid. `id` is a stable
/// composite of the kind + path so SwiftUI's `ForEach` doesn't
/// recycle cells across status changes.
struct RenameGridRow: Identifiable {
    let id: String
    let path: String
    let status: String
    let before: String
    let after: String
}
