// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// The History leaf (Milestone O-5, #543): per-note version history +
/// vault-wide deleted-file recovery, in two segments (the
/// `BibliographyPanel.segment` pattern). Diffs render as a flat
/// operation list — one plain AX element per operation, never a
/// side-by-side textual diff (the §7.3 sequential-walkthrough
/// contract).
struct HistoryPanel: View {
    @EnvironmentObject var appState: AppState

    /// Per-vault-session, deliberately NOT persisted (matches
    /// BibliographyPanel).
    @State private var segment: Segment = .thisNote
    /// Marker rows (renames) are hidden by default; the section-header
    /// menu reveals them — they explain gaps ("Renamed from X").
    @State private var showMarkers = false
    /// "Select for comparison" toggles, by position (hashes repeat
    /// across A→B→A histories; position is the row identity).
    @State private var comparePositions: [UInt32] = []
    /// The inline compare disclosure: which row's diff is open, and
    /// what it resolved to.
    @State private var inlineDiff: InlineDiff?
    /// AT focus by row position (WCAG 2.4.3): alert dismissals and
    /// restore success return focus into the version list — the new
    /// head row (position 0) after a restore.
    @AccessibilityFocusState private var focusedVersion: UInt32?
    /// The Restore As… destination being edited (#795); seeded from
    /// the prompt's suggestion when it appears.
    @State private var restoreAsDestination: String = ""
    /// Day groups the user collapsed (#798), keyed by `DayGroup.id` —
    /// unique per consecutive run, so collapsing one run never hides a
    /// distant same-day run.
    @State private var collapsedDayGroups: Set<String> = []

    enum Segment: Hashable {
        case thisNote
        case deleted
    }

    struct InlineDiff: Equatable {
        /// Row position the disclosure hangs under (or `nil` for the
        /// two-version compare, which renders under the header).
        let anchorPosition: UInt32?
        let result: Result<StructuredDiff, DisplayError>

        static func == (lhs: InlineDiff, rhs: InlineDiff) -> Bool {
            lhs.anchorPosition == rhs.anchorPosition
                && (try? lhs.result.get()) == (try? rhs.result.get())
        }
    }

    var body: some View {
        VStack(spacing: 0) {
            segmentPicker
            Divider()
            content
        }
        .alert(
            "Restore version?",
            isPresented: restorePresented,
            presenting: appState.historyRestoreRequest
        ) { request in
            Button("Cancel", role: .cancel) {
                appState.historyRestoreRequest = nil
                focusedVersion = visibleVersions.first?.positionFromTail
            }
            Button("Restore", role: .destructive) {
                appState.historyRestoreRequest = nil
                // Success re-asserts onto the NEW head via the focus
                // token; this returns focus into the list either way.
                focusedVersion = visibleVersions.first?.positionFromTail
                Task { await appState.performRestore(request) }
            }
        } message: { request in
            Text(
                "Restore the version from \(request.formattedDate)? This replaces the current content of \(appState.filename(of: request.path)). The replaced state remains available in version history."
            )
        }
        .alert(
            appState.historyAlert?.title ?? "History",
            isPresented: historyAlertPresented,
            presenting: appState.historyAlert
        ) { _ in
            Button("OK", role: .cancel) {
                appState.historyAlert = nil
                focusedVersion = visibleVersions.first?.positionFromTail
            }
        } message: { alert in
            Text(alert.message)
        }
        // Restore As… destination prompt (#795): raised on an occupied
        // recovery destination or from a version row's Restore As….
        .alert(
            "Restore As…",
            isPresented: restoreAsPresented,
            presenting: appState.historyRestoreAsPrompt
        ) { prompt in
            TextField("Destination path", text: $restoreAsDestination)
                .accessibilityLabel("Destination path")
            Button("Cancel", role: .cancel) {
                appState.historyRestoreAsPrompt = nil
                focusedVersion = visibleVersions.first?.positionFromTail
            }
            Button("Restore") {
                appState.historyRestoreAsPrompt = nil
                let destination = restoreAsDestination
                Task { await appState.performRestoreAs(prompt, destination: destination) }
            }
        } message: { prompt in
            switch prompt.source {
            case .deletedFile(let path):
                Text(
                    "A file already exists at \(path). Restore the deleted file to a different location."
                )
            case .version(_, _, let date):
                Text("Save a copy of the version from \(date) to a new file.")
            }
        }
        .onChange(of: appState.historyRestoreAsPrompt) { _, prompt in
            if let prompt {
                restoreAsDestination = prompt.suggestedPath
            }
        }
        // Per-note view state resets on EVERY selection transition,
        // including → nil (vault close/switch). This observer must
        // live on the always-mounted root: inside the selected-note
        // subtree it unmounts with the selection, missing exactly the
        // transition that carries stale state into the next vault
        // (codex round 2 on #798).
        .onChange(of: appState.selectedFilePath) { _, _ in
            comparePositions = []
            inlineDiff = nil
            collapsedDayGroups = []
        }
    }

    private var restoreAsPresented: Binding<Bool> {
        Binding(
            get: { appState.historyRestoreAsPrompt != nil },
            set: { if !$0 { appState.historyRestoreAsPrompt = nil } }
        )
    }

    private var restorePresented: Binding<Bool> {
        Binding(
            get: { appState.historyRestoreRequest != nil },
            set: { if !$0 { appState.historyRestoreRequest = nil } }
        )
    }

    private var historyAlertPresented: Binding<Bool> {
        Binding(
            get: { appState.historyAlert != nil },
            set: { if !$0 { appState.historyAlert = nil } }
        )
    }

    private var segmentPicker: some View {
        Picker("History scope", selection: $segment) {
            Text("This note").tag(Segment.thisNote)
            Text("Deleted").tag(Segment.deleted)
        }
        .pickerStyle(.segmented)
        .labelsHidden()
        .padding(8)
        .accessibilityLabel("History scope")
        .accessibilityValue(segment == .thisNote ? "This note" : "Deleted")
        .onChange(of: segment) { _, newValue in
            if newValue == .deleted {
                Task { await appState.loadDeletedFiles() }
            }
        }
    }

    @ViewBuilder private var content: some View {
        switch segment {
        case .thisNote:
            thisNoteSegment
        case .deleted:
            deletedSegment
        }
    }

    // MARK: - "This note"

    @ViewBuilder private var thisNoteSegment: some View {
        if appState.selectedFilePath == nil {
            LeafEmptyState(message: "Select a note to see its history.")
        } else {
            ScrollView {
                VStack(alignment: .leading, spacing: 12) {
                    sinceOpenSection
                    versionsSection
                }
                .padding(.vertical, 8)
            }
            .onChange(of: appState.historyFocusHeadToken) { _, _ in
                // Restore success: focus the new head row (position 0
                // — the restored state; WCAG 2.4.3).
                focusedVersion = 0
            }
        }
    }

    /// "Since you last opened" — rendered only when the pref is on AND
    /// the verdict is `diff` (Unchanged/NoBaseline render nothing;
    /// BaselineCompacted renders one caption row).
    @ViewBuilder private var sinceOpenSection: some View {
        switch appState.sinceOpenChanges {
        case .diff(let diff):
            sectionBox(header: "Since you last opened") {
                DiffOperationList(diff: diff)
            }
        case .baselineCompacted:
            sectionBox(header: "Since you last opened") {
                Text("Earlier changes have been compacted.")
                    .font(.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .padding(.horizontal, 12)
            }
        case .noBaseline, .unchanged, nil:
            EmptyView()
        }
    }

    /// One day's slice of the visible version list (#798). Groups are
    /// consecutive runs: the input is newest-first by position, and a
    /// backwards system clock can interleave days — runs preserve list
    /// order exactly (two same-day runs render as two sections) rather
    /// than reordering rows under the position-based features (compare
    /// endpoints, inline-diff anchors, AX focus).
    struct DayGroup: Equatable {
        /// Stable across pagination: local-day key + the run's first
        /// (newest) position. Appending older same-day versions extends
        /// the run without changing its id, so collapse state holds.
        let id: String
        /// "Today", "Yesterday", or the formatted local date.
        let title: String
        var versions: [VersionSummary]
    }

    /// Consecutive-run day grouping (pure, unit-tested).
    static func dayGroups(
        _ versions: [VersionSummary],
        calendar: Calendar = .current,
        now: Date = Date()
    ) -> [DayGroup] {
        var groups: [DayGroup] = []
        var currentDay: Date?
        for version in versions {
            let date = Date(timeIntervalSince1970: Double(version.timestampMs) / 1000)
            let day = calendar.startOfDay(for: date)
            if day != currentDay {
                currentDay = day
                groups.append(
                    DayGroup(
                        id: "\(Int(day.timeIntervalSince1970))#\(version.positionFromTail)",
                        title: dayTitle(day: day, calendar: calendar, now: now),
                        versions: []))
            }
            groups[groups.count - 1].versions.append(version)
        }
        return groups
    }

    /// "Today" / "Yesterday" / full local date (day names help temporal
    /// orientation for eyes-free review).
    static func dayTitle(day: Date, calendar: Calendar, now: Date) -> String {
        if calendar.isDate(day, inSameDayAs: now) { return "Today" }
        if let yesterday = calendar.date(
            byAdding: .day, value: -1, to: calendar.startOfDay(for: now)),
            calendar.isDate(day, inSameDayAs: yesterday)
        {
            return "Yesterday"
        }
        let formatter = DateFormatter()
        formatter.dateStyle = .full
        formatter.timeStyle = .none
        formatter.calendar = calendar
        // The formatter must render in the CALENDAR's zone — its own
        // default is the process-local zone, which shifts a
        // calendar-midnight Date across the date line (unit tests
        // inject UTC and caught exactly that).
        formatter.timeZone = calendar.timeZone
        if let locale = calendar.locale { formatter.locale = locale }
        return formatter.string(from: day)
    }

    /// The header's spoken contract (pure, unit-tested): title, count,
    /// and collapse state in one utterance.
    static func groupHeaderLabel(title: String, count: Int, collapsed: Bool) -> String {
        let versions = count == 1 ? "1 version" : "\(count) versions"
        return "\(title), \(versions), \(collapsed ? "collapsed" : "expanded")"
    }

    private var visibleVersions: [VersionSummary] {
        Self.visible(appState.historyVersions, showMarkers: showMarkers)
    }

    /// Pure marker filter (unit-tested): markers are hidden by default
    /// and revealed by the section-menu toggle.
    static func visible(_ versions: [VersionSummary], showMarkers: Bool)
        -> [VersionSummary]
    {
        showMarkers ? versions : versions.filter { !$0.isMarker }
    }

    @ViewBuilder private var versionsSection: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack {
                Text("Version history, \(appState.historyTotalFiltered) versions")
                    .font(.headline)
                    .accessibilityAddTraits(.isHeader)
                Spacer()
                if comparePositions.count == 2 {
                    Button("Compare selected versions") {
                        compareSelected()
                    }
                }
                Menu {
                    Toggle("Show markers", isOn: $showMarkers)
                } label: {
                    SlateSymbol.moreActions.image()
                }
                .menuStyle(.borderlessButton)
                .fixedSize()
                .accessibilityLabel("Version list options")
            }
            .padding(.horizontal, 12)

            if let error = appState.historyLoadError {
                Text(error)
                    .font(.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .padding(.horizontal, 12)
            } else if visibleVersions.isEmpty {
                Text("No versions yet. Versions are recorded as you save.")
                    .font(.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .padding(.horizontal, 12)
            } else {
                if let inline = inlineDiff, inline.anchorPosition == nil {
                    inlineDiffView(inline)
                }
                ForEach(Self.dayGroups(visibleVersions), id: \.id) { group in
                    dayGroupHeader(group)
                    if !collapsedDayGroups.contains(group.id) {
                        ForEach(group.versions, id: \.positionFromTail) { version in
                            versionRow(version)
                            if let inline = inlineDiff,
                                inline.anchorPosition == version.positionFromTail
                            {
                                inlineDiffView(inline)
                            }
                        }
                    }
                }
                if appState.historyNextCursor != nil {
                    Button("Show older versions") {
                        Task { await appState.loadOlderVersions() }
                    }
                    .buttonStyle(.plain)
                    .foregroundStyle(.tint)
                    .padding(.horizontal, 12)
                }
            }
        }
    }

    /// Collapsible day header (#798). AX: a real header (VoiceOver's
    /// headings rotor jumps day to day) whose label speaks title,
    /// count, and collapse state; the chevron is decorative, mirroring
    /// the file-tree disclosure convention.
    @ViewBuilder private func dayGroupHeader(_ group: DayGroup) -> some View {
        let collapsed = collapsedDayGroups.contains(group.id)
        Button {
            if collapsed {
                collapsedDayGroups.remove(group.id)
            } else {
                collapsedDayGroups.insert(group.id)
            }
        } label: {
            HStack(spacing: Tokens.Spacing.xs) {
                SlateSymbol.disclosure.decorative
                    .rotationEffect(.degrees(collapsed ? 0 : 90))
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                Text(group.title)
                    .font(.subheadline.weight(.semibold))
                    .foregroundStyle(Tokens.ColorRole.textPrimary)
                Text("\(group.versions.count)")
                    .font(.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                Spacer()
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .padding(.horizontal, 12)
        .padding(.top, 6)
        .accessibilityLabel(
            Self.groupHeaderLabel(
                title: group.title, count: group.versions.count, collapsed: collapsed))
        .accessibilityAddTraits(.isHeader)
    }

    @ViewBuilder private func versionRow(_ version: VersionSummary) -> some View {
        let date = Self.formattedDate(ms: version.timestampMs)
        let relative = Self.relativeDate(ms: version.timestampMs)
        let annotationText = version.annotations.map(\.display).joined(separator: ", ")
        VStack(alignment: .leading, spacing: 2) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 1) {
                    Text(date).font(.body)
                    Text(relative).font(.caption).foregroundStyle(Tokens.ColorRole.textSecondary)
                }
                Spacer()
                Toggle(
                    "Select for comparison",
                    isOn: compareBinding(for: version.positionFromTail)
                )
                .toggleStyle(.checkbox)
                .labelsHidden()
                .accessibilityLabel("Select for comparison")
                Button {
                    Task { await compareAgainstCurrent(version) }
                } label: {
                    SlateSymbol.compare.label("Compare")
                }
                .buttonStyle(.borderless)
                Button {
                    appState.requestRestore(
                        versionHash: version.contentHashAfter, formattedDate: date)
                } label: {
                    SlateSymbol.restore.label("Restore…")
                }
                .buttonStyle(.borderless)
                Button {
                    appState.requestRestoreAs(
                        versionHash: version.contentHashAfter, formattedDate: date)
                } label: {
                    SlateSymbol.restore.label("Restore As…")
                }
                .buttonStyle(.borderless)
            }
            Text(version.audioFragment)
                .font(.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            if !annotationText.isEmpty {
                AnnotationChips(annotations: version.annotations)
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 4)
        .accessibilityElement(children: .contain)
        .accessibilityLabel(
            "\(date), \(version.audioFragment)"
                + (annotationText.isEmpty ? "" : ", \(annotationText)"))
        .accessibilityFocused($focusedVersion, equals: version.positionFromTail)
    }

    private func compareBinding(for position: UInt32) -> Binding<Bool> {
        Binding(
            get: { comparePositions.contains(position) },
            set: { selected in
                comparePositions = Self.compareSelection(
                    afterToggling: position, on: selected,
                    current: comparePositions)
            }
        )
    }

    /// Pure selection-model step (unit-tested): with two rows already
    /// selected, selecting a third replaces the OLDER selection
    /// (higher `positionFromTail` = older). Duplicate-hash rows never
    /// interact — identity is position. Deselect always just removes.
    static func compareSelection(
        afterToggling position: UInt32, on selected: Bool, current: [UInt32]
    ) -> [UInt32] {
        var next = current
        if selected {
            guard !next.contains(position) else { return next }
            next.append(position)
            while next.count > 2 {
                // Drop the oldest OTHER selection, keeping the newly
                // toggled row.
                let oldest = next.filter { $0 != position }.max()
                next.removeAll { $0 == oldest }
            }
        } else {
            next.removeAll { $0 == position }
        }
        return next
    }

    private func compareSelected() {
        guard let path = appState.selectedFilePath,
            let endpoints = Self.compareEndpoints(
                positions: comparePositions, in: appState.historyVersions)
        else { return }
        Task {
            let result = await appState.historyDiff(
                path: path, fromHash: endpoints.from.contentHashAfter,
                toHash: endpoints.to.contentHashAfter)
            inlineDiff = InlineDiff(anchorPosition: nil, result: result)
        }
    }

    /// Pure two-version orientation (unit-tested): the OLDER selection
    /// (higher position-from-tail) is `from`, the newer is `to`.
    static func compareEndpoints(
        positions: [UInt32], in versions: [VersionSummary]
    ) -> (from: VersionSummary, to: VersionSummary)? {
        guard positions.count == 2 else { return nil }
        let sorted = positions.sorted(by: >)
        guard
            let from = versions.first(where: { $0.positionFromTail == sorted[0] }),
            let to = versions.first(where: { $0.positionFromTail == sorted[1] })
        else { return nil }
        return (from, to)
    }

    private func compareAgainstCurrent(_ version: VersionSummary) async {
        guard let path = appState.selectedFilePath,
            let currentHash = appState.currentNoteContentHash
        else { return }
        let result = await appState.historyDiff(
            path: path, fromHash: version.contentHashAfter, toHash: currentHash)
        inlineDiff = InlineDiff(
            anchorPosition: version.positionFromTail, result: result)
    }

    @ViewBuilder private func inlineDiffView(_ inline: InlineDiff) -> some View {
        switch inline.result {
        case .success(let diff):
            DiffOperationList(diff: diff)
                .padding(.leading, 12)
        case .failure(let error):
            Text(error.message)
                .font(.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .padding(.horizontal, 24)
        }
    }

    @ViewBuilder private func sectionBox<Content: View>(
        header: String, @ViewBuilder content: () -> Content
    ) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(header)
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
                .padding(.horizontal, 12)
            content()
        }
    }

    // MARK: - "Deleted"

    @ViewBuilder private var deletedSegment: some View {
        VStack(spacing: 0) {
            if let error = appState.deletedLoadError {
                Text(error)
                    .font(.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .padding(12)
                Spacer()
            } else if appState.deletedFiles.isEmpty {
                LeafEmptyState(message: "No recently deleted files.")
            } else {
                ScrollView {
                    VStack(alignment: .leading, spacing: 4) {
                        ForEach(appState.deletedFiles, id: \.path) { entry in
                            deletedRow(entry)
                        }
                    }
                    .padding(.vertical, 8)
                }
            }
            Divider()
            Text("Files deleted before Slate saved them go to the system Trash.")
                .font(.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .padding(8)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .task {
            await appState.loadDeletedFiles()
        }
    }

    @ViewBuilder private func deletedRow(_ entry: DeletedFileEntry) -> some View {
        let deletedText =
            entry.deletedAtMs.map { "Deleted \(Self.relativeDate(ms: $0))" }
            ?? "Deletion time unknown"
        HStack(alignment: .firstTextBaseline) {
            VStack(alignment: .leading, spacing: 1) {
                Text(entry.path).font(.body)
                HStack(spacing: 6) {
                    Text(deletedText)
                    if entry.recoverable, let size = entry.sizeBytes {
                        Text(Self.formattedSize(bytes: size))
                    }
                }
                .font(.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            }
            Spacer()
            if entry.recoverable {
                Button {
                    Task { await appState.recoverDeleted(path: entry.path) }
                } label: {
                    SlateSymbol.restore.label("Restore")
                }
                .buttonStyle(.borderless)
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 4)
        .accessibilityElement(children: .contain)
        .accessibilityLabel(
            "\(entry.path), \(deletedText.lowercased()), "
                + (entry.recoverable ? "restorable" : "not restorable"))
    }

    // MARK: - Formatting (pure, unit-tested)

    /// Absolute date+time, medium/short — the primary text (never bare
    /// relative time; relative is the secondary line).
    static func formattedDate(ms: Int64) -> String {
        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        formatter.timeStyle = .short
        return formatter.string(
            from: Date(timeIntervalSince1970: Double(ms) / 1000))
    }

    static func relativeDate(ms: Int64, now: Date = Date()) -> String {
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .full
        return formatter.localizedString(
            for: Date(timeIntervalSince1970: Double(ms) / 1000), relativeTo: now)
    }

    static func formattedSize(bytes: UInt64) -> String {
        ByteCountFormatter.string(
            fromByteCount: Int64(bytes), countStyle: .file)
    }
}

// MARK: - Shared diff rendering

/// The operation-list diff renderer shared by Compare and the
/// since-open section: header row = `audioSummary`, then one plain AX
/// element per operation, in order — icon (SlateSymbol by class
/// family) + `semanticDescription` + optional `detail` secondary.
/// NEVER a side-by-side textual diff (§7.3).
struct DiffOperationList: View {
    let diff: StructuredDiff

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(diff.audioSummary)
                .font(.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .padding(.horizontal, 12)
            if diff.operations.isEmpty {
                Text("No differences.")
                    .font(.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .padding(.horizontal, 12)
            } else {
                ForEach(Array(diff.operations.enumerated()), id: \.offset) { _, op in
                    operationRow(op)
                }
            }
        }
    }

    @ViewBuilder private func operationRow(_ op: DiffOperation) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: 6) {
            Self.symbol(for: op.kind).decorative
            VStack(alignment: .leading, spacing: 1) {
                Text(op.semanticDescription).font(.callout)
                if let detail = op.detail, !detail.isEmpty {
                    Text(detail)
                        .font(.caption)
                        .foregroundStyle(Tokens.ColorRole.textSecondary)
                        .lineLimit(2)
                }
            }
        }
        .padding(.horizontal, 12)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(
            op.detail.map { "\(op.semanticDescription). \($0)" }
                ?? op.semanticDescription)
    }

    /// Pure class-family → icon mapping (unit-tested totality).
    static func symbol(for kind: DiffOpClass) -> SlateSymbol {
        switch kind {
        case .headingAdded, .paragraphAdded, .listItemAdded:
            return .diffAdded
        case .headingRemoved, .paragraphRemoved, .listItemRemoved:
            return .diffRemoved
        case .headingEdited, .paragraphEdited, .listItemEdited:
            return .diffEdited
        case .propertySet, .propertyRemoved:
            return .addProperty
        case .taskStatusChanged:
            return .tasksLeaf
        case .codeBlockEdited:
            return .code
        case .mathBlockEdited:
            return .math
        case .diagramEdited:
            return .diagram
        case .tableEdited, .other:
            return .diffEdited
        }
    }
}

/// Annotation chips — small capsule labels for the semantic
/// annotations riding a version (SetProperty, ToggleTask, …).
/// Text-on-material with APCA-checked contrast; each chip is its own
/// AX element so VO reads them in row order.
struct AnnotationChips: View {
    let annotations: [OpAnnotationSummary]

    var body: some View {
        HStack(spacing: 4) {
            ForEach(Array(annotations.enumerated()), id: \.offset) { _, annotation in
                Text(annotation.display)
                    .font(.caption2)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .padding(.horizontal, 6)
                    .padding(.vertical, 2)
                    .background(Tokens.ColorRole.surfaceSecondary, in: Capsule())
            }
        }
    }
}
