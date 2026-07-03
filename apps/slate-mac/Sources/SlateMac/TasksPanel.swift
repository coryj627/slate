// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Per-note tasks panel — mirrors `BacklinksPanel` /
/// `OutgoingLinksPanel` / `PropertiesPanel` in shape. Reads
/// `AppState.currentNoteTasks` (refreshed by the same load + save
/// paths as headings/links/properties) and groups rows by
/// completion state.
///
/// Each row carries an accessible checkbox (button labelled
/// "Mark complete" / "Mark incomplete") and an activation gesture
/// that asks the editor to scroll to the task's line. Status is
/// communicated through accessibility text + a trait, not via
/// colour — WCAG 1.4.1 (color is not the only visual means of
/// conveying information).
struct TasksPanel: View {
    @EnvironmentObject private var appState: AppState
    @State private var isExpanded = true

    var body: some View {
        // Hide entirely when no note is selected. Matches
        // `BacklinksPanel`'s behaviour so VoiceOver doesn't
        // enumerate an empty section.
        if appState.selectedFilePath == nil {
            EmptyView()
        } else {
            DisclosureGroup(isExpanded: $isExpanded) {
                content
            } label: {
                header
            }
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
        }
    }

    private var header: some View {
        let total = appState.currentNoteTasks.count
        let open = appState.currentNoteTasks.filter { !$0.completed }.count
        let suffix = total == 1 ? "task" : "tasks"
        let label = total == 0
            ? "Tasks, none"
            : "Tasks, \(open) open of \(total) \(suffix)"
        return Text(label)
            .font(.headline)
            .accessibilityAddTraits(.isHeader)
    }

    @ViewBuilder
    private var content: some View {
        if appState.isLoadingTasks && appState.currentNoteTasks.isEmpty {
            HStack(spacing: 8) {
                ProgressView()
                    .controlSize(.small)
                Text("Loading tasks…")
                    .foregroundStyle(.secondary)
            }
            .padding(.vertical, 4)
            .accessibilityElement(children: .combine)
            .accessibilityLabel("Loading tasks.")
        } else if appState.currentNoteTasks.isEmpty {
            Text("No tasks in this note.")
                .font(.callout)
                .foregroundStyle(.secondary)
                .padding(.vertical, 4)
                .accessibilityLabel("No tasks in this note.")
        } else {
            VStack(alignment: .leading, spacing: 8) {
                section(
                    title: "Open",
                    tasks: appState.currentNoteTasks.filter { !$0.completed }
                )
                section(
                    title: "Done",
                    tasks: appState.currentNoteTasks.filter { $0.completed }
                )
            }
        }
    }

    @ViewBuilder
    private func section(title: String, tasks: [TaskItem]) -> some View {
        if !tasks.isEmpty {
            VStack(alignment: .leading, spacing: 2) {
                Text("\(title) (\(tasks.count))")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                    .accessibilityAddTraits(.isHeader)
                    .padding(.bottom, 2)
                ForEach(tasks, id: \.ordinal) { task in
                    row(for: task)
                }
            }
        }
    }

    private func row(for task: TaskItem) -> some View {
        HStack(alignment: .top, spacing: 8) {
            checkbox(for: task)
            Button {
                // Activation → ask the editor to scroll to the
                // task's source line. Line is 1-based per
                // `TaskItem.line`'s contract; `lineScrollRequest`
                // expects an Int (NoteEditorView's coordinator
                // converts to the NSTextView's 0-based glyph
                // index internally).
                appState.lineScrollRequest.send(Int(task.line))
            } label: {
                taskBody(task)
            }
            .buttonStyle(.plain)
            .accessibilityLabel(rowAccessibilityLabel(task))
            .accessibilityHint("Scrolls the editor to this task's line.")
        }
        .padding(.vertical, 2)
    }

    private func checkbox(for task: TaskItem) -> some View {
        // #158: Block toggle interaction while the editor has
        // unsaved changes. The toggle's post-save reload would
        // overwrite the buffer otherwise; AppState.toggleCurrentTask
        // also no-ops in this case but disabling here gives the
        // user a visible "save first" signal and routes VoiceOver
        // away from a button that would do nothing.
        let blocked = appState.hasUnsavedChanges
        return Button {
            appState.toggleCurrentTask(task)
        } label: {
            // Single-character placeholder; the real visual is a
            // system-styled toggle below via `.toggleStyle`. SF
            // Symbol changes by completion state but the label
            // (and AX trait) carries the actual semantics.
            (task.completed ? SlateSymbol.taskComplete : SlateSymbol.taskIncomplete).decorative
                .imageScale(.large)
        }
        .buttonStyle(.plain)
        .disabled(blocked)
        .accessibilityLabel(task.completed ? "Mark incomplete" : "Mark complete")
        .accessibilityHint(
            blocked
                ? "Save the note first. Toggle is disabled while the editor has unsaved changes."
                : "Toggles the task between open and done."
        )
        .accessibilityIsSelected(task.completed)
        // WCAG 1.4.1: status NOT communicated via colour alone.
        // The accessibility label carries the explicit state; the
        // SF Symbol carries the visual; both flip together.
        .help(
            blocked
                ? "Save the note first. Toggle is disabled while the editor has unsaved changes."
                : (task.completed ? "Mark incomplete" : "Mark complete")
        )
    }

    private func taskBody(_ task: TaskItem) -> some View {
        VStack(alignment: .leading, spacing: 1) {
            Text(task.text)
                .font(.callout)
                .foregroundStyle(.primary)
                .strikethrough(task.completed, color: .secondary)
            if let metadata = inlineMetadata(for: task) {
                Text(metadata)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .contentShape(Rectangle())
    }

    /// Compose the secondary line for a task row: due date,
    /// priority, recurrence. Returns `nil` when none are set so
    /// the empty caption row collapses (no trailing whitespace
    /// to clutter sighted layout).
    private func inlineMetadata(for task: TaskItem) -> String? {
        var parts: [String] = []
        if let dueMs = task.dueMs {
            parts.append("Due \(Self.formatDueDate(dueMs))")
        }
        if let priority = task.priority {
            parts.append("Priority \(Self.priorityLabel(priority))")
        }
        if let rec = task.recurrence, !rec.isEmpty {
            parts.append("Repeats \(rec)")
        }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }

    /// Compose the VoiceOver row label. Reads:
    /// `"<status>. <text>. Due <date>. Priority <level>. Open task."`
    /// — per the accessibility checkpoints in #113. Status phrasing
    /// distinguishes `[/]` / `[-]` (#423).
    private func rowAccessibilityLabel(_ task: TaskItem) -> String {
        var parts: [String] = []
        parts.append(task.statusWord)
        parts.append(task.text)
        if let dueMs = task.dueMs {
            parts.append("Due \(Self.formatDueDate(dueMs))")
        }
        if let priority = task.priority {
            parts.append("Priority \(Self.priorityLabel(priority))")
        }
        if let rec = task.recurrence, !rec.isEmpty {
            parts.append("Repeats \(rec)")
        }
        parts.append(task.statusPhrase)
        return parts.joined(separator: ". ")
    }

    /// Format a UTC-midnight `due_ms` value as `YYYY-MM-DD`.
    /// Matches the on-disk Tasks-plugin syntax so VoiceOver reads
    /// the same value the user authored.
    ///
    /// The formatter is cached because both `TasksPanel` and
    /// `TasksReviewView` call this in their row builders — a vault
    /// with several hundred dated tasks would otherwise allocate a
    /// new `DateFormatter` per row on every redraw. `DateFormatter`
    /// is documented thread-safe for `string(from:)` reads after
    /// initialisation, and we only mutate it once inside the
    /// `static let` initialiser.
    static func formatDueDate(_ ms: Int64) -> String {
        let date = Date(timeIntervalSince1970: TimeInterval(ms) / 1000.0)
        return dueDateFormatter.string(from: date)
    }

    private static let dueDateFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(identifier: "UTC")
        formatter.dateFormat = "yyyy-MM-dd"
        return formatter
    }()

    /// Human-readable priority label. Maps the backend's signed
    /// scale (`-2`…`2`) to the Tasks-plugin emoji terminology.
    static func priorityLabel(_ priority: Int32) -> String {
        switch priority {
        case 2: return "highest"
        case 1: return "high"
        case -1: return "low"
        case -2: return "lowest"
        default: return "\(priority)"
        }
    }
}
