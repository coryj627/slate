// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Vault-wide Tasks Review leaf (#114, folded out of its modal sheet into the
/// right-pane leaf system in #879). The `Leaf.tasksReview` rail entry mounts
/// this; it shows every task in the vault filtered by `TaskReviewFilter`, lets
/// the user toggle a task from the review, and routes row activation into the
/// editor at the task's line — now NON-MODALLY, so the review reports beside
/// the workspace instead of blocking it (sheets.md:35 — a complex, paginated
/// browser is exactly what a sheet shouldn't be).
///
/// It lives alongside the note-scoped `TasksPanel` (`Leaf.tasks`): that panel
/// is this note's tasks, this leaf is the whole vault's, filterable + paged.
///
/// **Reveal / focus / announce live in `AppState.openTasksReview()`**, not an
/// `.onAppear` here: every leaf is permanently mounted in the `RightPaneView`
/// ZStack (state-retention), so `.onAppear` fires once at app launch, not on
/// reveal. The reveal command selects the leaf, un-hides the pane, kicks the
/// load, and posts the announcement; the leaf-focus anchor ("Tasks Review
/// panel") lands VoiceOver on entry — the uniform leaf entry pattern.
///
/// **Accessibility shape.** The filter chips are radio-equivalent buttons with
/// `.isSelected` traits + a count on the active chip (kept as the deliberate
/// idiom over `.pickerStyle(.segmented)` — see `filterChips`). The result list
/// is a VStack of `Button` rows; each row's label reads as "<file>. <task
/// text>. Due <date>. Priority <level>. <status>." Tab cycles the filter chips
/// → result rows → "Load more".
struct TasksReviewPanel: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        // Leaf shape (LeafChrome parity): a labeled header row + the persistent
        // filter control pinned above a fill-the-pane, vertically-scrolling
        // result list. The chips stay fixed (a filter that scrolled away would
        // be unreachable mid-list); `content` owns its own ScrollView.
        VStack(alignment: .leading, spacing: 0) {
            header
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.horizontal, Tokens.Spacing.sm)
                .padding(.vertical, Tokens.Spacing.xs)
            filterChips
                .padding(.horizontal, Tokens.Spacing.sm)
                .padding(.bottom, Tokens.Spacing.xs)
            Divider()
            content
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        .navigationTitle("Tasks Review")
        // #879 red-team: this leaf is permanently mounted (the retention
        // ZStack) and reachable via the rail / keyboard / layout restore,
        // none of which call `openTasksReview`. Kick a reveal-agnostic
        // load when the leaf becomes active — and once on mount for a
        // restore that seeded `activeLeaf = .tasksReview` before this view
        // appeared. `ensureVaultTasksLoaded` is idempotent, so this never
        // double-fires with the ⌘R path. Post-update (#448-safe).
        .onChange(of: appState.workspace.activeLeaf) { _, leaf in
            if leaf == .tasksReview { appState.ensureVaultTasksLoaded() }
        }
        .task {
            if appState.workspace.activeLeaf == .tasksReview {
                appState.ensureVaultTasksLoaded()
            }
        }
    }

    // MARK: - Subviews

    /// Leaf header: the leaf's name + its count, one `.isHeader` element —
    /// matching the sibling leaves' "<Name>, N …" headers (BacklinksPanel,
    /// TasksPanel). WCAG 2.5.3 (label-in-name) holds because the visible text
    /// IS the accessible label. #160: a paginated result reads "showing N of M"
    /// to signal the truncation the "Load more" row below resolves.
    private var header: some View {
        Text(headerLabel)
            .font(Tokens.Typography.sectionHeader)
            .foregroundStyle(Tokens.ColorRole.textPrimary)
            .accessibilityAddTraits(.isHeader)
    }

    /// "Tasks Review, N shown" when fully loaded; "Tasks Review, showing N of
    /// M" when the user can still page forward. Singular handling matters
    /// because "1 tasks" reads wrong both visually and to VoiceOver.
    private var headerLabel: String {
        let shown = appState.vaultTasks.count
        let total = appState.vaultTasksTotalFiltered
        if appState.vaultTasksNextCursor != nil {
            return "Tasks Review, showing \(shown) of \(total)"
        }
        return shown == 1 ? "Tasks Review, 1 shown" : "Tasks Review, \(shown) shown"
    }

    /// The filter control. **#883 design call — capsule chips are the
    /// deliberate idiom here, NOT `.pickerStyle(.segmented)`**, even though
    /// segmented-controls.md:34-35 flags mutually-exclusive-choices-affecting-a-
    /// view as the textbook segmented case. Three reasons segmented would
    /// regress this specific control:
    ///
    ///  1. **Counts can't ride the segments.** Only the ACTIVE filter's total
    ///     is knowable — the query returns `vaultTasksTotalFiltered` for it;
    ///     the other filters' totals would each need their own vault query. A
    ///     segmented control shows every segment at once, so only one segment
    ///     could carry a count ("Overdue (3)") while the rest stay bare — the
    ///     "avoid some segments full and others sparse / keep similar content
    ///     sizes" anti-pattern (segmented-controls.md:38,43). Chips annotate
    ///     only the active one cleanly because they're independent controls.
    ///  2. **Segmented commits on every arrow.** `.pickerStyle(.segmented)`
    ///     changes selection as focus traverses, and each filter change here
    ///     cancels + re-runs a vault-wide query and announces "Filter set to
    ///     …" (`applyTaskReviewFilter`). Chips are focus-then-activate
    ///     (Tab to move, Space/Return to choose), which fits a query-per-filter
    ///     cost — one query, one announcement, when the user commits.
    ///  3. **The chips already model the exact AX** VoiceOver reads: a radio
    ///     group via per-chip `.isSelected` + a count-bearing label on the
    ///     active chip ("Due today, 3 tasks, selected"). Segmented supplies
    ///     generic segment semantics but can't annotate the selected segment's
    ///     count without the sparse-label problem above.
    ///
    /// So: keep the chips, recorded here as the intentional idiom (the lower-
    /// risk outcome the issue invites). Each chip is a `Button` with an
    /// explicit `.isSelected` trait — the WCAG-compliant radio-group model in
    /// SwiftUI without a custom AX role; the count suffix is part of the AX
    /// label so VoiceOver says "Due today, 3 tasks, selected".
    private var filterChips: some View {
        HStack(spacing: Tokens.Spacing.sm) {
            ForEach(TaskReviewFilter.allCases) { filter in
                chip(for: filter)
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Filter")
    }

    private func chip(for filter: TaskReviewFilter) -> some View {
        let isActive = appState.taskReviewFilter == filter
        // #883 red-team: the active chip's count is the filter TOTAL
        // (`vaultTasksTotalFiltered`, what the header shows), NOT the
        // loaded-page count (`vaultTasks.count`) — after "Load more" those
        // diverge, and the page count would misrepresent the filter.
        let count = isActive ? Int(appState.vaultTasksTotalFiltered) : nil
        return Button {
            appState.applyTaskReviewFilter(filter)
        } label: {
            Text(filter.displayName)
                .font(Tokens.Typography.callout)
                .foregroundStyle(Tokens.ColorRole.textPrimary)
                .padding(.horizontal, 10)
                .padding(.vertical, 4)
                // Selection is shape + tint, never color alone (WCAG 1.4.1):
                // the active chip fills with the faint `accentFill` tint AND
                // draws a full-strength `accentText` border; inactive chips are
                // clear-filled with a subtle `separator` outline. APCA-gated
                // tokens (DesignTokens.contrastPairings), never raw literals.
                .background(
                    Capsule().fill(
                        isActive
                            ? Tokens.ColorRole.accentFill.opacity(0.18)
                            : Color.clear
                    )
                )
                .overlay(
                    Capsule().stroke(
                        isActive
                            ? Tokens.ColorRole.accentText
                            : Tokens.ColorRole.separator,
                        lineWidth: 1
                    )
                )
        }
        .buttonStyle(.plain)
        .accessibilityLabel(
            count.map { "\(filter.displayName), \(CountCopy.counted($0, "task", "tasks"))" }
                ?? filter.displayName
        )
        .accessibilityIsSelected(isActive)
        .accessibilityHint("Filter the review to \(filter.displayName.lowercased()) tasks.")
    }

    @ViewBuilder
    private var content: some View {
        if appState.isLoadingVaultTasks && appState.vaultTasks.isEmpty {
            HStack(spacing: Tokens.Spacing.sm) {
                ProgressView()
                    .controlSize(.small)
                Text("Loading tasks…")
                    .font(Tokens.Typography.body)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .center)
            .accessibilityElement(children: .combine)
            .accessibilityLabel("Loading tasks.")
        } else if let error = appState.vaultTasksLoadError {
            VStack(alignment: .leading, spacing: Tokens.Spacing.xs) {
                // WCAG 2.4.6 (Headings and Labels): anything rendered
                // with a headline font has to advertise the heading
                // trait so VoiceOver rotor-by-heading lands on it.
                Text("Couldn't load tasks.")
                    .font(.headline)
                    .foregroundStyle(Tokens.ColorRole.textPrimary)
                    .accessibilityAddTraits(.isHeader)
                Text(error)
                    .font(Tokens.Typography.callout)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .accessibilityLabel(error)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
            .padding(.horizontal, Tokens.Spacing.sm)
            .padding(.vertical, Tokens.Spacing.xs)
        } else if appState.vaultTasks.isEmpty {
            Text("No tasks matching \(appState.taskReviewFilter.displayName).")
                .font(Tokens.Typography.callout)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
                .padding(.horizontal, Tokens.Spacing.sm)
                .padding(.vertical, Tokens.Spacing.xs)
                .accessibilityLabel(
                    "No tasks matching \(appState.taskReviewFilter.displayName)."
                )
        } else {
            ScrollView {
                VStack(alignment: .leading, spacing: Tokens.Spacing.xs) {
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
                .padding(.horizontal, Tokens.Spacing.sm)
                .padding(.vertical, Tokens.Spacing.xs)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
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

    private func rowView(for row: TaskWithLocation) -> some View {
        // #158: Only block the row whose file matches the editor's
        // dirty buffer — toggling other files from the review
        // surface is safe because there's no live buffer to lose.
        let dirtyReason =
            "Save \(row.fileName) first. Toggle is disabled while the editor has unsaved changes."
        let blockedReason = appState.noteAuthoringDisabledReason(for: row.path)
            ?? ((row.path == appState.loadedFilePath && appState.hasUnsavedChanges)
                ? dirtyReason : nil)
        return HStack(alignment: .top, spacing: 8) {
            Button {
                appState.toggleVaultTask(row)
            } label: {
                (row.task.completed ? SlateSymbol.taskComplete : SlateSymbol.taskIncomplete).decorative
                    .imageScale(.large)
            }
            .buttonStyle(.plain)
            .disabled(blockedReason != nil)
            .accessibilityLabel(row.task.completed ? "Mark incomplete" : "Mark complete")
            .accessibilityHint(
                blockedReason ?? "Toggles the task between open and done."
            )
            .help(
                blockedReason
                    ?? (row.task.completed ? "Mark incomplete" : "Mark complete"))
            .accessibilityIsSelected(row.task.completed)

            Button {
                appState.openTaskRowInEditor(row)
            } label: {
                VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
                    Text(row.fileName)
                        .font(Tokens.Typography.caption)
                        .foregroundStyle(Tokens.ColorRole.textSecondary)
                    Text(row.task.text)
                        .font(Tokens.Typography.callout)
                        .foregroundStyle(Tokens.ColorRole.textPrimary)
                        .strikethrough(row.task.completed, color: Tokens.ColorRole.textSecondary)
                    if let metadata = inlineMetadata(for: row.task) {
                        Text(metadata)
                            .font(Tokens.Typography.caption)
                            .foregroundStyle(Tokens.ColorRole.textSecondary)
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
}

// `TaskWithLocation` needs to be `Hashable` for the `ForEach(id:
// \.self)` use above. The FFI-generated type is `Equatable` +
// `Hashable` already because all its members are, so this is
// automatic in Swift — no extra conformance needed.
