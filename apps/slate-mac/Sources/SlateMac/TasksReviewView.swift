import SwiftUI

/// Vault-wide Tasks Review surface (#114). Sheet presented from
/// `MainSplitView` via the toolbar button or Cmd+Shift+T. Shows
/// every task in the vault filtered by `TaskReviewFilter`, lets
/// the user toggle from the review, and routes row activation
/// back into the editor at the task's line.
///
/// **Accessibility shape.** The filter chips are radio-equivalent
/// buttons with `.isSelected` traits + count-bearing labels. The
/// result list is a VStack of `Button` rows; each row's label
/// reads as "<file>. <task text>. Due <date>. Priority <level>.
/// <status>." Tab/Shift+Tab cycle the filter chips → result rows
/// → close button.
struct TasksReviewView: View {
    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            header
            filterChips
            Divider()
            content
            Divider()
            footer
        }
        .padding(16)
        .frame(minWidth: 520, idealWidth: 640, minHeight: 360, idealHeight: 480)
        .onAppear {
            // Re-announce on appear so VoiceOver users land in the
            // right context even when the sheet is presented
            // programmatically (e.g. via Cmd+Shift+T).
            postAccessibilityAnnouncement(
                "Tasks review opened. \(appState.taskReviewFilter.displayName). \(taskCountAnnouncement(appState.vaultTasks.count))",
                priority: .high
            )
        }
    }

    // MARK: - Subviews

    private var header: some View {
        HStack {
            Text("Tasks Review")
                .font(.title2.bold())
                .accessibilityAddTraits(.isHeader)
            Spacer()
            Text("\(appState.vaultTasks.count) shown")
                .font(.callout)
                .foregroundStyle(.secondary)
                .accessibilityLabel("\(appState.vaultTasks.count) tasks shown")
        }
    }

    private var filterChips: some View {
        // Each chip is a Button with an explicit `.isSelected`
        // trait — that's the WCAG-compliant way to model a
        // radio group in SwiftUI without a custom AX role. The
        // count suffix is part of the AX label so VoiceOver
        // says "Due today, 3 tasks, selected" or similar.
        HStack(spacing: 8) {
            ForEach(TaskReviewFilter.allCases) { filter in
                chip(for: filter)
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Filter")
    }

    private func chip(for filter: TaskReviewFilter) -> some View {
        let isActive = appState.taskReviewFilter == filter
        let count = isActive ? appState.vaultTasks.count : nil
        return Button {
            appState.applyTaskReviewFilter(filter)
        } label: {
            Text(filter.displayName)
                .padding(.horizontal, 10)
                .padding(.vertical, 4)
                .background(
                    Capsule().fill(isActive ? Color.accentColor.opacity(0.18) : Color.clear)
                )
                .overlay(
                    Capsule().stroke(
                        isActive ? Color.accentColor : Color.secondary.opacity(0.35),
                        lineWidth: 1
                    )
                )
        }
        .buttonStyle(.plain)
        .accessibilityLabel(
            count.map { "\(filter.displayName), \($0) tasks" } ?? filter.displayName
        )
        .accessibilityAddTraits(isActive ? [.isSelected] : [])
        .accessibilityHint("Filter the review to \(filter.displayName.lowercased()) tasks.")
    }

    @ViewBuilder
    private var content: some View {
        if appState.isLoadingVaultTasks && appState.vaultTasks.isEmpty {
            HStack(spacing: 8) {
                ProgressView()
                    .controlSize(.small)
                Text("Loading tasks…")
                    .foregroundStyle(.secondary)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .center)
            .accessibilityElement(children: .combine)
            .accessibilityLabel("Loading tasks.")
        } else if let error = appState.vaultTasksLoadError {
            VStack(alignment: .leading, spacing: 8) {
                Text("Couldn't load tasks.")
                    .font(.headline)
                Text(error)
                    .font(.callout)
                    .foregroundStyle(.secondary)
                    .accessibilityLabel(error)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        } else if appState.vaultTasks.isEmpty {
            Text("No tasks matching \(appState.taskReviewFilter.displayName).")
                .font(.callout)
                .foregroundStyle(.secondary)
                .frame(maxWidth: .infinity, alignment: .leading)
                .accessibilityLabel(
                    "No tasks matching \(appState.taskReviewFilter.displayName)."
                )
        } else {
            ScrollView {
                VStack(alignment: .leading, spacing: 6) {
                    ForEach(appState.vaultTasks, id: \.self) { row in
                        rowView(for: row)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }
        }
    }

    private var footer: some View {
        HStack {
            Spacer()
            Button("Close") {
                appState.closeTasksReview()
                dismiss()
            }
            .keyboardShortcut(.escape, modifiers: [])
            .accessibilityHint("Close the tasks review. Esc.")
        }
    }

    private func rowView(for row: TaskWithLocation) -> some View {
        HStack(alignment: .top, spacing: 8) {
            Button {
                appState.toggleVaultTask(row)
            } label: {
                Image(systemName: row.task.completed ? "checkmark.square" : "square")
                    .imageScale(.large)
            }
            .buttonStyle(.plain)
            .accessibilityLabel(row.task.completed ? "Mark incomplete" : "Mark complete")
            .accessibilityHint("Toggles the task between open and done.")
            .accessibilityAddTraits(row.task.completed ? [.isSelected] : [])

            Button {
                appState.openTaskRowInEditor(row)
            } label: {
                VStack(alignment: .leading, spacing: 1) {
                    Text(row.fileName)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    Text(row.task.text)
                        .font(.callout)
                        .foregroundStyle(.primary)
                        .strikethrough(row.task.completed, color: .secondary)
                    if let metadata = inlineMetadata(for: row.task) {
                        Text(metadata)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .accessibilityLabel(rowAccessibilityLabel(row))
            .accessibilityHint("Opens the source note at this task's line.")
            .help(row.path)
        }
        .padding(.vertical, 2)
    }

    // MARK: - Formatting helpers
    //
    // These mirror the ones in `TasksPanel` so per-row metadata
    // reads identically in both surfaces. Kept private to each
    // view so the AX-label phrasing can diverge if either side
    // wants to specialise — e.g. the review's row label leads
    // with the filename, the panel's doesn't.

    private func inlineMetadata(for task: TaskItem) -> String? {
        var parts: [String] = []
        if let dueMs = task.dueMs {
            parts.append("Due \(TasksPanel.formatDueDate(dueMs))")
        }
        if let priority = task.priority {
            parts.append("Priority \(TasksPanel.priorityLabel(priority))")
        }
        if let rec = task.recurrence, !rec.isEmpty {
            parts.append("Repeats \(rec)")
        }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }

    private func rowAccessibilityLabel(_ row: TaskWithLocation) -> String {
        var parts: [String] = []
        parts.append(row.fileName)
        parts.append(row.task.text)
        if let dueMs = row.task.dueMs {
            parts.append("Due \(TasksPanel.formatDueDate(dueMs))")
        }
        if let priority = row.task.priority {
            parts.append("Priority \(TasksPanel.priorityLabel(priority))")
        }
        if let rec = row.task.recurrence, !rec.isEmpty {
            parts.append("Repeats \(rec)")
        }
        parts.append(row.task.completed ? "Done task." : "Open task.")
        return parts.joined(separator: ". ")
    }

    private func taskCountAnnouncement(_ count: Int) -> String {
        switch count {
        case 0: return "No tasks shown."
        case 1: return "1 task shown."
        default: return "\(count) tasks shown."
        }
    }
}

// `TaskWithLocation` needs to be `Hashable` for the `ForEach(id:
// \.self)` use above. The FFI-generated type is `Equatable` +
// `Hashable` already because all its members are, so this is
// automatic in Swift — no extra conformance needed.
