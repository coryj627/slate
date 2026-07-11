// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

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

    /// Initial keyboard focus: seeded onto the ACTIVE filter chip on
    /// appear, so first-Tab starts from a meaningful control instead
    /// of SwiftUI's arbitrary default (every other sheet in the app
    /// seeds focus — palette/switcher/Add-Property/Move each land on
    /// their first field; this sheet's equivalent is its filter row).
    @FocusState private var focusedChip: TaskReviewFilter?

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
            // After the announcement, not before — the async hop keeps
            // the focus move out of the presentation transaction (the
            // #448 publish-in-view-update lesson).
            DispatchQueue.main.async {
                focusedChip = appState.taskReviewFilter
            }
        }
    }

    // MARK: - Subviews

    private var header: some View {
        HStack {
            Text("Tasks Review")
                .font(.title2.bold())
                .accessibilityAddTraits(.isHeader)
            Spacer()
            // WCAG 2.5.3 (label-in-name): the visible text and the AX
            // label must start with the same string so dictation /
            // "click on" voice commands match what sighted users
            // read. Both `headerCountText` variants begin with the
            // visible numeric content so no separate AX label is
            // needed.
            //
            // #160: when the result set is paginated
            // (`vaultTasksNextCursor != nil`), the count text
            // changes to "Showing N of M tasks" to signal the
            // truncation. The "Load more" button below the rows
            // gives the user a way to access the remaining pages.
            Text(headerCountText)
                .font(.callout)
                .foregroundStyle(.secondary)
        }
    }

    /// "N tasks shown" when the result is fully loaded; "Showing N of M
    /// tasks" when the user can still page forward. Singular handling
    /// matters because "1 tasks" reads wrong both visually and to
    /// VoiceOver.
    private var headerCountText: String {
        let shown = appState.vaultTasks.count
        let total = appState.vaultTasksTotalFiltered
        if appState.vaultTasksNextCursor != nil {
            return "Showing \(shown) of \(total) tasks"
        }
        return shown == 1 ? "1 task shown" : "\(shown) tasks shown"
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
        .focused($focusedChip, equals: filter)
        .accessibilityLabel(
            count.map { "\(filter.displayName), \($0) tasks" } ?? filter.displayName
        )
        .accessibilityIsSelected(isActive)
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
                // WCAG 2.4.6 (Headings and Labels): anything rendered
                // with a headline font has to advertise the heading
                // trait so VoiceOver rotor-by-heading lands on it.
                Text("Couldn't load tasks.")
                    .font(.headline)
                    .accessibilityAddTraits(.isHeader)
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
                    // #160: Show the "Load more" affordance when
                    // the FFI returned a cursor for the next page.
                    // Lives inside the same ScrollView so the
                    // button sits below the last visible row and
                    // VoiceOver rotor-by-control reaches it
                    // naturally after the rows.
                    //
                    // Hidden during the initial load so a filter
                    // switch that's mid-flight can't trigger
                    // "Load more" with a stale cursor against the
                    // new filter (the cursor + total stay at the
                    // old values until the new load's success
                    // arm overwrites them).
                    if appState.vaultTasksNextCursor != nil
                        && !appState.isLoadingVaultTasks
                    {
                        loadMoreRow
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }
        }
    }

    /// "Load more" button + an in-flight spinner. Disabled while a
    /// page is fetching so the user can't stack overlapping requests
    /// (`AppState.loadMoreVaultTasks` also guards re-entrancy, but
    /// disabling here gives a visible signal).
    private var loadMoreRow: some View {
        let loading = appState.isLoadingMoreVaultTasks
        // Clamp to ≥0 so a transient desync between `vaultTasks.count`
        // and `vaultTasksTotalFiltered` (e.g. an in-flight toggle
        // that flipped a row out of the filter) doesn't produce
        // "-3 remaining" in the a11y announcement (#164 Codoki).
        let remaining = max(0, Int(appState.vaultTasksTotalFiltered) - appState.vaultTasks.count)
        return HStack(spacing: 8) {
            Button {
                appState.loadMoreVaultTasks()
            } label: {
                Text(loading ? "Loading more tasks…" : "Load more tasks")
            }
            .disabled(loading)
            .accessibilityLabel(
                loading
                    ? "Loading more tasks"
                    : "Load more tasks. \(remaining) remaining."
            )
            .accessibilityHint(
                "Fetches the next page of vault tasks matching the active filter."
            )
            if loading {
                ProgressView()
                    .controlSize(.small)
            }
            Spacer()
        }
        .padding(.top, 4)
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
        // #158: Only block the row whose file matches the editor's
        // dirty buffer — toggling other files from the review
        // surface is safe because there's no live buffer to lose.
        let blocked = (row.path == appState.loadedFilePath && appState.hasUnsavedChanges)
        return HStack(alignment: .top, spacing: 8) {
            Button {
                appState.toggleVaultTask(row)
            } label: {
                (row.task.completed ? SlateSymbol.taskComplete : SlateSymbol.taskIncomplete).decorative
                    .imageScale(.large)
            }
            .buttonStyle(.plain)
            .disabled(blocked)
            .accessibilityLabel(row.task.completed ? "Mark incomplete" : "Mark complete")
            .accessibilityHint(
                blocked
                    ? "Save \(row.fileName) first. Toggle is disabled while the editor has unsaved changes."
                    : "Toggles the task between open and done."
            )
            .accessibilityIsSelected(row.task.completed)

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
        parts.append(row.task.statusPhrase)
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
