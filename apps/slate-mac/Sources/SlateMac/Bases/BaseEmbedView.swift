// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

private enum DataviewConversionOutcome: Sendable {
    case success(String)
    case failure(String)
}

struct BaseEmbedQuickFilterSelectionState: Equatable {
    private(set) var anchorRowID: String? = nil
    private(set) var isActive = false

    mutating func beginIfNeeded(currentRowID: String?) {
        guard !isActive else { return }
        isActive = true
        anchorRowID = currentRowID
    }

    func preferredRowID(currentRowID: String?) -> String? {
        isActive ? anchorRowID : currentRowID
    }

    mutating func finish(currentRowID: String?) -> String? {
        let preferred = preferredRowID(currentRowID: currentRowID)
        reset()
        return preferred
    }

    mutating func reset() {
        anchorRowID = nil
        isActive = false
    }

    mutating func finishAfterApplying(filterText: String) {
        guard filterText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return
        }
        reset()
    }
}

/// Read-only embedded Bases renderer for reading and editor surfaces.
/// Source forms differ, but every request lands here after it has an
/// executable Bases handle.
struct BaseEmbedView: View {
    let request: BaseEmbedRequest
    let session: VaultSession?
    let thisPath: String?
    let onOpenInTab: (BaseEmbedOpenDestination) -> Void
    /// Retained for source compatibility with embed hosts. Dataview conversion
    /// now routes through AppState's owned save-panel writer so stale sessions,
    /// structural admission, refresh, and barriers are one atomic contract.
    let onWroteSaveDestination: (URL, Bool) -> Void

    @EnvironmentObject private var appState: AppState
    @StateObject private var document: BaseEmbedDocument
    @State private var interaction = BaseGridInteractionState()
    @State private var quickFilterSelection = BaseEmbedQuickFilterSelectionState()
    @State private var quickFilterTask: Task<Void, Never>?
    @State private var dataviewConversionTask: Task<Void, Never>?
    @State private var dataviewConversionGeneration: UInt64 = 0
    @State private var resultFocusToken = 0
    @FocusState private var quickFilterFocused: Bool

    @MainActor init(
        request: BaseEmbedRequest,
        session: VaultSession?,
        thisPath: String?,
        sharedHandle: BaseEmbedHandle,
        onOpenInTab: @escaping (BaseEmbedOpenDestination) -> Void,
        onWroteSaveDestination: @escaping (URL, Bool) -> Void = { _, _ in }
    ) {
        self.request = request
        self.session = session
        self.thisPath = thisPath
        self.onOpenInTab = onOpenInTab
        self.onWroteSaveDestination = onWroteSaveDestination
        _document = StateObject(
            wrappedValue: BaseEmbedDocument(
                request: request, thisPath: thisPath, sharedHandle: sharedHandle))
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            if request.kind == .dataview,
                let reason = appState.structuralMutationDisabledReason
            {
                Text(reason)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .padding(.bottom, Tokens.Spacing.xs)
                    .accessibilityLabel(reason)
            }
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
        .onKeyPress(.escape) {
            guard quickFilterFocused || document.quickFilterActive else { return .ignored }
            clearQuickFilter()
            return .handled
        }
        .onAppear {
            document.acquireRefreshLease()
            guard let session,
                document.needsInitialLoad || document.handle == nil
            else { return }
            appState.loadBaseEmbedDocumentIfAllowed(document, session: session)
        }
        .onDisappear {
            document.releaseRefreshLease()
            quickFilterTask?.cancel()
            quickFilterTask = nil
            dataviewConversionTask?.cancel()
            dataviewConversionTask = nil
        }
        .onChange(of: document.quickFilterText) { _, _ in
            guard quickFilterFocused else { return }
            if document.quickFilterActive {
                quickFilterSelection.beginIfNeeded(currentRowID: interaction.selectedRowID)
            }
            scheduleQuickFilterExecution()
        }
        .onChange(of: document.result) { _, result in
            interaction.reconcile(with: result)
        }
        .onChange(of: document.activeViewIndex) { _, _ in
            quickFilterTask?.cancel()
            quickFilterTask = nil
            quickFilterSelection.reset()
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
                .disabled(batchTrashInteractionDisabledReason != nil)
                .accessibilityHint(
                    batchTrashInteractionDisabledReason
                        ?? "Choose the active embedded Base view.")
                .help(
                    batchTrashInteractionDisabledReason
                        ?? "Choose the active embedded Base view")
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
                    .focused($quickFilterFocused)
                    .accessibilityLabel(
                        "Quick filter — temporary, does not change the embedded base")
                    .disabled(batchTrashInteractionDisabledReason != nil)
                    .accessibilityHint(
                        batchTrashInteractionDisabledReason
                            ?? "Temporarily filter the embedded Base results.")
                    .help(
                        batchTrashInteractionDisabledReason
                            ?? "Quick filter")
            }
            if let recovery = document.recoveryAction {
                Button(recovery.title) {
                    onOpenInTab(recovery.destination)
                }
                .accessibilityHint(recovery.accessibilityHint)
            }
            if request.kind == .dataview {
                let convertDisabledReason = appState.structuralMutationDisabledReason
                Button("Convert to .base") {
                    convertDataview()
                }
                .disabled(session == nil || convertDisabledReason != nil)
                .accessibilityHint(
                    convertDisabledReason
                        ?? "Converts this Dataview query to a .base file when lossless.")
                .help(
                    convertDisabledReason
                        ?? "Convert this Dataview query to a .base file")
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
            if let result = document.result {
                switch BaseResultContentState(result: result) {
                case .empty:
                    placeholder("No base results.")
                case .rowOnly:
                    resultList(result)
                case .tabular:
                    resultRenderer(result)
                }
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
                get: { interaction.selectedRowID },
                set: { interaction.setSelectedRowID($0, in: result) }),
            cellSelection: Binding(
                get: { interaction.cellPosition(in: result) },
                set: { interaction.setCellPosition($0, in: result) }),
            sortState: Binding(
                get: { document.sortState },
                set: { sort in
                    guard let session else { return }
                    document.setTransientSort(sort, session: session)
                }),
            cellNavigation: true,
            sortsRowsLocally: false,
            rowAccessibilityDescription: { $0.row.audioDescription },
            focusRequest: resultFocusToken)
    }

    private func resultList(_ result: BasesResultSet) -> some View {
        let projection = BaseListProjection(
            result: result,
            options: BaseListOptions(slateStateJson: activeView?.slateStateJson),
            isQuickFiltered: document.quickFilterActive)
        return BaseListView(
            projection: projection,
            selection: Binding(
                get: { interaction.selectedRowID },
                set: { interaction.setSelectedRowID($0, in: result) }),
            focusRequest: resultFocusToken,
            onActivate: { _ in },
            rowActions: [])
    }

    private func columns(from result: BasesResultSet) -> [AccessibleDataGrid<BaseGridRow>.Column] {
        result.columns.enumerated().map { columnIndex, column in
            AccessibleDataGrid<BaseGridRow>.Column(
                column.label,
                cell: { row in row.value(at: columnIndex) },
                sort: { lhs, rhs in lhs.sortsBefore(rhs, at: columnIndex) },
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
        let preferredSelection = quickFilterSelection.preferredRowID(
            currentRowID: interaction.selectedRowID)
        let filterText = document.quickFilterText
        quickFilterTask = Task { @MainActor in
            do {
                try await Task.sleep(nanoseconds: 150_000_000)
            } catch {
                return
            }
            guard !Task.isCancelled, let session else { return }
            let announcement = document.applyQuickFilter(filterText, session: session)
            restoreSelection(previous: preferredSelection)
            quickFilterSelection.finishAfterApplying(filterText: filterText)
            // W0.5-3 residue: BaseEmbedDocument.applyQuickFilter
            postAccessibilityAnnouncement(.hostComposed(text: announcement, priority: .medium))
        }
    }

    private func clearQuickFilter() {
        quickFilterTask?.cancel()
        quickFilterTask = nil
        quickFilterFocused = false
        let preferredSelection = quickFilterSelection.finish(
            currentRowID: interaction.selectedRowID)
        if let announcement = document.clearQuickFilter(session: session) {
            restoreSelection(previous: preferredSelection)
            // W0.5-3 residue: BaseEmbedDocument.clearQuickFilter
            postAccessibilityAnnouncement(.hostComposed(text: announcement, priority: .medium))
        }
        resultFocusToken &+= 1
    }

    private func restoreSelection(previous: String?) {
        guard let result = document.result else {
            interaction.reconcile(with: nil)
            return
        }
        let restored = BaseSelectionRestorer.restoredSelection(
            previous: previous,
            current: interaction.selectedRowID,
            availableIDs: result.rows.map { BaseGridRow.id(for: $0) })
        interaction.setSelectedRowID(restored, in: result)
    }

    private func convertDataview() {
        guard request.kind == .dataview, let originSession = session else { return }
        guard appState.currentSession === originSession else { return }
        guard appState.admitStructuralMutationRequest() else { return }
        dataviewConversionGeneration &+= 1
        let generation = dataviewConversionGeneration
        let source = request.inlineSource
        let observer = appState.baseRetargetNativeExecutionObserverForTesting
        dataviewConversionTask?.cancel()
        dataviewConversionTask = Task { @MainActor in
            let outcome: DataviewConversionOutcome = await Task.detached(
                priority: .userInitiated
            ) {
                do {
                    BasePreparedLoader.observe(.dqlConversion, observer: observer)
                    return .success(try originSession.dqlAsBase(source: source))
                } catch {
                    return .failure(error.localizedDescription)
                }
            }.value

            guard !Task.isCancelled,
                dataviewConversionGeneration == generation,
                appState.currentSession === originSession
            else { return }

            switch outcome {
            case .success(let text):
                let panel = NSSavePanel()
                panel.nameFieldStringValue = "Converted.base"
                panel.begin { response in
                    guard response == .OK, let url = panel.url else { return }
                    Task { @MainActor in
                        guard dataviewConversionGeneration == generation,
                            appState.currentSession === originSession
                        else { return }
                        _ = appState.performBaseSavePanelWrite(
                            text: text,
                            to: url,
                            originSession: originSession,
                            successMessage: "Converted Dataview block to .base.",
                            failurePrefix: "Dataview conversion could not be saved")
                    }
                }
            case .failure(let message):
                postAccessibilityAnnouncement(.dataviewConversionFailed(detail: message))
            }
        }
    }

    private var activeViewBinding: Binding<Int> {
        Binding(
            get: { document.activeViewIndex },
            set: { index in
                guard batchTrashInteractionDisabledReason == nil else { return }
                guard let session else { return }
                document.selectView(index: index, session: session)
            })
    }

    private var batchTrashInteractionDisabledReason: String? {
        guard let path = request.targetPath,
            case .readOnly(let reason) = appState.batchTrashPathCapability(for: path)
        else { return nil }
        return reason
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

/// Keeps the lightweight embed landmark in the non-lazy reading tree so
/// VoiceOver can enumerate document structure, while deferring the native
/// query handle and result grid until the landmark reaches the scroll viewport.
struct BaseEmbedVisibilityState: Equatable {
    private(set) var hasBecomeVisible = false

    mutating func observe(isVisible: Bool) {
        if isVisible {
            hasBecomeVisible = true
        }
    }
}

struct VisibilityGatedBaseEmbed: View {
    let request: BaseEmbedRequest
    let session: VaultSession?
    let thisPath: String?
    let sharedHandle: BaseEmbedHandle
    let onOpenInTab: (BaseEmbedOpenDestination) -> Void
    /// #871: forwarded to `BaseEmbedView` so a Dataview → .base save-panel
    /// write can be barriered by AppState. Defaults to a no-op for callers
    /// (previews / tests) that never convert.
    var onWroteSaveDestination: (URL, Bool) -> Void = { _, _ in }

    @State private var visibility = BaseEmbedVisibilityState()
    @State private var ownsMountedLease = false

    var body: some View {
        Group {
            if visibility.hasBecomeVisible {
                BaseEmbedView(
                    request: request,
                    session: session,
                    thisPath: thisPath,
                    sharedHandle: sharedHandle,
                    onOpenInTab: onOpenInTab,
                    onWroteSaveDestination: onWroteSaveDestination)
            } else {
                deferredPlaceholder
            }
        }
        .onScrollVisibilityChange(threshold: 0.01) { isVisible in
            visibility.observe(isVisible: isVisible)
        }
        .onAppear {
            guard !ownsMountedLease else { return }
            sharedHandle.acquireMountedLease()
            ownsMountedLease = true
        }
        .onDisappear {
            guard ownsMountedLease else { return }
            sharedHandle.releaseMountedLease()
            ownsMountedLease = false
        }
        .accessibilityElement(children: .contain)
    }

    private var deferredPlaceholder: some View {
        HStack(spacing: Tokens.Spacing.sm) {
            SlateSymbol.base.decorative
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
                Text(request.accessibilityLabel)
                    .font(Tokens.Typography.callout.weight(.semibold))
                    .foregroundStyle(Tokens.ColorRole.textPrimary)
                Text("Deferred embedded base — scroll into view to load.")
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
            }
        }
        .padding(Tokens.Spacing.sm)
        .frame(maxWidth: .infinity, minHeight: 64, alignment: .leading)
        .background(
            RoundedRectangle(cornerRadius: 4)
                .fill(Tokens.ColorRole.surfaceSecondary)
        )
        .accessibilityElement(children: .combine)
        .accessibilityLabel(
            "\(request.accessibilityLabel). Deferred embedded base; scroll into view to load.")
    }
}

struct BaseEmbedPreviewList: View {
    let text: String
    let session: VaultSession?
    let thisPath: String?
    let onOpenInTab: (BaseEmbedOpenDestination) -> Void
    let onJumpToSource: (Int) -> Void
    let handleProvider: @MainActor (BaseEmbedRequest, String?) -> BaseEmbedHandle
    /// #871: forwarded to each embed so a Dataview → .base save-panel write is
    /// barriered by AppState.
    let onWroteSaveDestination: (URL, Bool) -> Void

    init(
        text: String,
        session: VaultSession?,
        thisPath: String?,
        onOpenInTab: @escaping (BaseEmbedOpenDestination) -> Void,
        onJumpToSource: @escaping (Int) -> Void = { _ in },
        handleProvider: @escaping @MainActor (BaseEmbedRequest, String?) -> BaseEmbedHandle =
            { request, thisPath in BaseEmbedHandle(request: request, thisPath: thisPath) },
        onWroteSaveDestination: @escaping (URL, Bool) -> Void = { _, _ in }
    ) {
        self.text = text
        self.session = session
        self.thisPath = thisPath
        self.onOpenInTab = onOpenInTab
        self.onJumpToSource = onJumpToSource
        self.handleProvider = handleProvider
        self.onWroteSaveDestination = onWroteSaveDestination
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
                            VisibilityGatedBaseEmbed(
                                request: preview.request,
                                session: session,
                                thisPath: thisPath,
                                sharedHandle: handleProvider(preview.request, thisPath),
                                onOpenInTab: onOpenInTab,
                                onWroteSaveDestination: onWroteSaveDestination)
                        }
                        .id(
                            BaseExactIdentity.key(
                                prefix: "editor-base-embed",
                                components: [
                                    String(index), preview.request.cacheKey, thisPath,
                                ]))
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
