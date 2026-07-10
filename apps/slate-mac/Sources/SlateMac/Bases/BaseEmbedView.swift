// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

/// Read-only embedded Bases renderer for reading and editor surfaces.
/// Source forms differ, but every request lands here after it has an
/// executable Bases handle.
struct BaseEmbedView: View {
    let request: BaseEmbedRequest
    let session: VaultSession?
    let thisPath: String?
    let onOpenInTab: (String) -> Void

    @StateObject private var document: BaseEmbedDocument
    @State private var selectedRow: String?
    @State private var selectedCell: AccessibleDataGrid<BaseGridRow>.CellPosition?
    @State private var quickFilterTask: Task<Void, Never>?

    @MainActor init(
        request: BaseEmbedRequest,
        session: VaultSession?,
        thisPath: String?,
        sharedHandle: BaseEmbedHandle,
        onOpenInTab: @escaping (String) -> Void
    ) {
        self.request = request
        self.session = session
        self.thisPath = thisPath
        self.onOpenInTab = onOpenInTab
        _document = StateObject(
            wrappedValue: BaseEmbedDocument(
                request: request, thisPath: thisPath, sharedHandle: sharedHandle))
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            banners
            content
        }
        .padding(Tokens.Spacing.sm)
        .background(
            RoundedRectangle(cornerRadius: 4)
                .fill(Tokens.ColorRole.surfaceSecondary)
        )
        .accessibilityElement(children: .contain)
        .accessibilityLabel(request.accessibilityLabel)
        .onAppear {
            document.acquireRefreshLease()
            guard let session,
                document.needsInitialLoad || document.handle == nil
            else { return }
            document.load(session: session)
        }
        .onDisappear {
            document.releaseRefreshLease()
            quickFilterTask?.cancel()
            quickFilterTask = nil
        }
        .onChange(of: document.quickFilterText) { _, _ in
            scheduleQuickFilterExecution()
        }
    }

    private var header: some View {
        HStack(spacing: Tokens.Spacing.sm) {
            SlateSymbol.base.decorative
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            Text(request.accessibilityLabel)
                .font(Tokens.Typography.callout.weight(.semibold))
                .foregroundStyle(Tokens.ColorRole.textPrimary)
                .lineLimit(2)
            if document.views.count > 1 {
                Picker("View", selection: activeViewBinding) {
                    ForEach(Array(document.views.enumerated()), id: \.offset) { index, view in
                        Text(view.name).tag(index)
                    }
                }
                .labelsHidden()
                .frame(maxWidth: 180)
            }
            Spacer(minLength: 0)
            if let result = document.result {
                Text(
                    "\(result.shownCount) of \(document.quickFilterActive ? result.unfilteredShownCount : result.totalCount)"
                )
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
            }
            if document.result != nil {
                TextField("Quick filter", text: $document.quickFilterText)
                    .textFieldStyle(.roundedBorder)
                    .frame(width: 160)
                    .accessibilityLabel(
                        "Quick filter — temporary, does not change the embedded base")
            }
            if let path = request.targetPath {
                Button("Open in tab") {
                    onOpenInTab(path)
                }
                .accessibilityHint("Opens the base file in a tab where editing is available.")
            }
            if request.kind == .dataview {
                Button("Convert to .base") {
                    convertDataview()
                }
                .disabled(session == nil)
                .accessibilityHint("Converts this Dataview query to a .base file when lossless.")
            }
        }
        .padding(.bottom, Tokens.Spacing.xs)
        .slateSymbolSurface(.toolbar)
    }

    @ViewBuilder
    private var banners: some View {
        if let message = stateMessage {
            banner(message)
        }
        if let result = document.result {
            ForEach(Array(result.warnings.enumerated()), id: \.offset) { _, warning in
                banner(warning)
            }
            if let error = result.viewError, !error.isEmpty {
                banner(error)
            }
        }
    }

    @ViewBuilder
    private var content: some View {
        switch document.state {
        case .idle, .loading:
            placeholder("Loading embedded base...")
        case .failed(let message):
            placeholder(message)
        case .ready, .degraded:
            if let result = document.result, !result.columns.isEmpty {
                resultRenderer(result)
            } else {
                placeholder("No base results.")
            }
        }
    }

    @ViewBuilder
    private func resultRenderer(_ result: BasesResultSet) -> some View {
        switch BaseRendererMode.resolved(view: activeView, override: nil) {
        case .table:
            resultGrid(result)
        case .list:
            resultList(result)
        }
    }

    private func resultGrid(_ result: BasesResultSet) -> some View {
        AccessibleDataGrid(
            columns: columns(from: result),
            rows: rows(from: result),
            summary: BaseSummaryFormatter.summaryText(
                result, isQuickFiltered: document.quickFilterActive),
            accessibilityLabel: "Embedded base table",
            groups: groups(from: result),
            selection: Binding(
                get: { selectedRow },
                set: { selectedRow = $0 }),
            cellSelection: Binding(
                get: { selectedCell },
                set: { selectedCell = $0 }),
            sortState: Binding(
                get: { document.sortState },
                set: { sort in
                    guard let session else { return }
                    document.setTransientSort(sort, session: session)
                }),
            cellNavigation: true,
            rowAccessibilityDescription: { $0.row.audioDescription })
    }

    private func resultList(_ result: BasesResultSet) -> some View {
        let projection = BaseListProjection(
            result: result,
            options: BaseListOptions(slateStateJson: activeView?.slateStateJson),
            isQuickFiltered: document.quickFilterActive)
        return BaseListView(
            projection: projection,
            selection: Binding(
                get: { selectedRow },
                set: { selectedRow = $0 }),
            onActivate: { _ in },
            rowActions: [])
    }

    private func columns(from result: BasesResultSet) -> [AccessibleDataGrid<BaseGridRow>.Column] {
        result.columns.enumerated().map { columnIndex, column in
            AccessibleDataGrid<BaseGridRow>.Column(
                column.label,
                cell: { row in row.value(at: columnIndex) },
                sort: { lhs, rhs in
                    let ascending = document.sortState?.ascending ?? true
                    return ascending
                        ? lhs.sortsBefore(rhs, at: columnIndex, ascending: true)
                        : rhs.sortsBefore(lhs, at: columnIndex, ascending: false)
                },
                accessibilityHint: { _ in document.cellEditingAccessibilityHint })
        }
    }

    private func rows(from result: BasesResultSet) -> [BaseGridRow] {
        result.rows.enumerated().map { rowIndex, row in
            BaseGridRow(row: row, ordinal: rowIndex)
        }
    }

    private func groups(from result: BasesResultSet) -> [AccessibleDataGrid<BaseGridRow>.Group] {
        result.groups.map {
            .init(
                label: $0.label,
                rowStart: Int($0.rowStart),
                rowCount: Int($0.rowCount),
                summary: BaseSummaryFormatter.summaryText(
                    summaries: $0.summaries, columns: result.columns))
        }
    }

    private func scheduleQuickFilterExecution() {
        quickFilterTask?.cancel()
        quickFilterTask = Task { @MainActor in
            do {
                try await Task.sleep(nanoseconds: 150_000_000)
            } catch {
                return
            }
            guard !Task.isCancelled, let session else { return }
            let announcement = document.applyQuickFilter(document.quickFilterText, session: session)
            postAccessibilityAnnouncement(announcement, priority: .medium)
        }
    }

    private func convertDataview() {
        guard request.kind == .dataview, let session else { return }
        do {
            let text = try session.dqlAsBase(source: request.inlineSource)
            let panel = NSSavePanel()
            panel.nameFieldStringValue = "Converted.base"
            panel.begin { response in
                guard response == .OK, let url = panel.url else { return }
                DispatchQueue.global(qos: .userInitiated).async {
                    let message: String
                    do {
                        try text.write(to: url, atomically: true, encoding: .utf8)
                        message = "Converted Dataview block to .base."
                    } catch {
                        message = "Dataview conversion could not be saved: \(error.localizedDescription)"
                    }
                    DispatchQueue.main.async {
                        postAccessibilityAnnouncement(message, priority: .medium)
                    }
                }
            }
        } catch {
            postAccessibilityAnnouncement(
                "Dataview conversion failed: \(error.localizedDescription)",
                priority: .medium)
        }
    }

    private var activeViewBinding: Binding<Int> {
        Binding(
            get: { document.activeViewIndex },
            set: { index in
                guard let session else { return }
                document.selectView(index: index, session: session)
            })
    }

    private var activeView: BaseViewSummary? {
        guard document.views.indices.contains(document.activeViewIndex) else { return nil }
        return document.views[document.activeViewIndex]
    }

    private var stateMessage: String? {
        if case .degraded(let message) = document.state { return message }
        return nil
    }

    private func banner(_ text: String) -> some View {
        HStack(spacing: Tokens.Spacing.xs) {
            SlateSymbol.warning.decorative
            Text(text)
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textPrimary)
                .lineLimit(2)
            Spacer(minLength: 0)
        }
        .padding(.vertical, Tokens.Spacing.xs)
        .accessibilityLabel(text)
    }

    private func placeholder(_ text: String) -> some View {
        Text(text)
            .font(Tokens.Typography.callout)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.vertical, Tokens.Spacing.sm)
    }

}

struct BaseEmbedPreviewList: View {
    let text: String
    let session: VaultSession?
    let thisPath: String?
    let onOpenInTab: (String) -> Void
    let onJumpToSource: (Int) -> Void
    let handleProvider: @MainActor (BaseEmbedRequest, String?) -> BaseEmbedHandle

    init(
        text: String,
        session: VaultSession?,
        thisPath: String?,
        onOpenInTab: @escaping (String) -> Void,
        onJumpToSource: @escaping (Int) -> Void = { _ in },
        handleProvider: @escaping @MainActor (BaseEmbedRequest, String?) -> BaseEmbedHandle =
            { request, thisPath in BaseEmbedHandle(request: request, thisPath: thisPath) }
    ) {
        self.text = text
        self.session = session
        self.thisPath = thisPath
        self.onOpenInTab = onOpenInTab
        self.onJumpToSource = onJumpToSource
        self.handleProvider = handleProvider
    }

    private var previews: [BaseEmbedPreview] {
        BaseEmbedRequest.previews(in: text)
    }

    var body: some View {
        if !previews.isEmpty {
            ScrollView {
                VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
                    ForEach(Array(previews.enumerated()), id: \.offset) { index, preview in
                        VStack(alignment: .leading, spacing: Tokens.Spacing.xs) {
                            HStack(spacing: Tokens.Spacing.sm) {
                                Text("Embedded base preview — source line \(preview.sourceLine)")
                                    .font(Tokens.Typography.caption)
                                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                                Button("Jump to source") {
                                    onJumpToSource(preview.sourceLine)
                                }
                                .buttonStyle(.link)
                                .accessibilityLabel(
                                    "Jump to source — line \(preview.sourceLine). Embedded base.")
                            }
                            BaseEmbedView(
                                request: preview.request,
                                session: session,
                                thisPath: thisPath,
                                sharedHandle: handleProvider(preview.request, thisPath),
                                onOpenInTab: onOpenInTab)
                        }
                        .id("\(index)|\(preview.request.cacheKey)|\(thisPath ?? "")")
                    }
                }
                .padding(Tokens.Spacing.sm)
            }
            .frame(maxHeight: 360)
            .background(Tokens.ColorRole.surface)
            .accessibilityElement(children: .contain)
            .accessibilityLabel("Embedded bases preview.")
        }
    }
}
