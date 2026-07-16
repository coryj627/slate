// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

enum BaseDocumentSource: Hashable, Sendable {
    case file(path: String)
    case savedQuery(id: String, name: String)

    init?(item: EditorItem) {
        switch item {
        case .base(let path):
            self = .file(path: path)
        case .savedQuery(let id, let name):
            self = .savedQuery(id: id, name: name)
        default:
            return nil
        }
    }

    var key: String {
        switch self {
        case .file(let path):
            return BaseExactIdentity.registryKey(prefix: "base-file", value: path)
        case .savedQuery(let id, _):
            return BaseExactIdentity.registryKey(prefix: "saved-query", value: id)
        }
    }

    static func == (lhs: BaseDocumentSource, rhs: BaseDocumentSource) -> Bool {
        switch (lhs, rhs) {
        case (.file(let lhs), .file(let rhs)):
            return BaseExactIdentity.matches(lhs, rhs)
        case (.savedQuery(let lhsID, let lhsName), .savedQuery(let rhsID, let rhsName)):
            return BaseExactIdentity.matches(lhsID, rhsID)
                && BaseExactIdentity.matches(lhsName, rhsName)
        default:
            return false
        }
    }

    func hash(into hasher: inout Hasher) {
        switch self {
        case .file(let path):
            hasher.combine(0)
            BaseExactIdentity.hash(path, into: &hasher)
        case .savedQuery(let id, let name):
            hasher.combine(1)
            BaseExactIdentity.hash(id, into: &hasher)
            BaseExactIdentity.hash(name, into: &hasher)
        }
    }

    var displayName: String {
        switch self {
        case .file(let path):
            let name = (path as NSString).lastPathComponent
            return (name as NSString).deletingPathExtension
        case .savedQuery(_, let name):
            return name
        }
    }

    var selectionKey: String {
        switch self {
        case .file(let path):
            return path
        case .savedQuery(let id, _):
            return "saved-query:\(id)"
        }
    }

    var filePath: String? {
        if case .file(let path) = self { return path }
        return nil
    }
}

enum BaseRetargetNativePhase: Sendable, Equatable {
    case closeReplaced
    case open
    case views
    case sort
    case execute
    case closePrepared
    case listSavedQueries
    case listBases
    case listDashboards
    case export
    case dqlConversion
    case refreshOpen
    case refreshViews
    case refreshSort
    case refreshExecute
    case refreshCloseReplaced
    case refreshClosePrepared
}

struct BaseRetargetNativeExecutionEvent: Sendable, Equatable {
    let phase: BaseRetargetNativePhase
    let ranOnMainThread: Bool
}

typealias BaseRetargetNativeExecutionObserver =
    @Sendable (BaseRetargetNativeExecutionEvent) -> Void

enum BaseRetargetThreadProbe {
    nonisolated static func isMainThread() -> Bool { Thread.isMainThread }
}

/// Immutable inputs needed to reproduce the current Base view against a new
/// path-bound handle. UI objects and mutable document state never cross the
/// main-actor boundary.
struct BasePreparedLoadRequest: Sendable {
    let source: BaseDocumentSource
    let previousViewName: String?
    let previousViewIndex: Int
    let quickFilter: String?
    let sortColumnID: String?
    let sortAscending: Bool
    let thisPath: String?
}

enum BasePreparedLoad: @unchecked Sendable {
    case ready(
        handle: UInt64,
        views: [BaseViewSummary],
        result: BasesResultSet?,
        activeViewIndex: Int,
        appliedQuickFilter: String?
    )
    case failed(String)

    var retainedHandle: UInt64? {
        guard case .ready(let handle, _, _, _, _) = self else { return nil }
        return handle
    }
}

typealias BaseRetargetPreloadRunner =
    @Sendable (
        VaultSession,
        BasePreparedLoadRequest,
        BaseRetargetNativeExecutionObserver?
    ) -> BasePreparedLoad

struct BaseRetargetReservation: Sendable {
    let generation: UInt64
    let replacedHandle: UInt64?
    let request: BasePreparedLoadRequest
}

struct BaseContentRefreshReservation: Sendable {
    let generation: UInt64
    let replacedHandle: UInt64?
    let request: BasePreparedLoadRequest
}

enum BaseContentRefreshApplication: Sendable {
    case applied(replacedHandle: UInt64?)
    case failed
    case stale
}

/// Entire Base open/views/sort/execute boundary for retargets. Ready results
/// transfer one native handle to the MainActor document; every other path
/// closes its temporary handle here.
enum BasePreparedLoader {
    nonisolated static func prepare(
        session: VaultSession,
        request: BasePreparedLoadRequest,
        observer: BaseRetargetNativeExecutionObserver?
    ) -> BasePreparedLoad {
        var openedHandle: UInt64?
        var transferredHandle = false
        defer {
            if let handle = openedHandle, !transferredHandle {
                close(
                    handle: handle,
                    session: session,
                    phase: .closePrepared,
                    observer: observer)
            }
        }

        do {
            observe(.open, observer: observer)
            let handle: UInt64
            switch request.source {
            case .file(let path):
                handle = try session.openBase(path: path)
            case .savedQuery(let id, _):
                handle = try session.openSavedQuery(id: id)
            }
            openedHandle = handle

            observe(.views, observer: observer)
            let views = try session.baseViews(handle: handle)
            let activeViewIndex: Int
            if let previousViewName = request.previousViewName,
                let matchingIndex = views.firstIndex(where: {
                    BaseExactIdentity.matches($0.name, previousViewName)
                })
            {
                activeViewIndex = matchingIndex
            } else {
                activeViewIndex = views.isEmpty
                    ? 0 : min(request.previousViewIndex, views.count - 1)
            }

            let result: BasesResultSet?
            if views.indices.contains(activeViewIndex) {
                if let sortColumnID = request.sortColumnID {
                    observe(.sort, observer: observer)
                    try session.baseSetTransientSort(
                        handle: handle,
                        view: UInt32(activeViewIndex),
                        columnId: sortColumnID,
                        ascending: request.sortAscending)
                }
                observe(.execute, observer: observer)
                result = try session.baseExecute(
                    handle: handle,
                    view: UInt32(activeViewIndex),
                    thisPath: request.thisPath,
                    quickFilter: request.quickFilter,
                    cancel: CancelToken())
            } else {
                result = nil
            }

            transferredHandle = true
            return .ready(
                handle: handle,
                views: views,
                result: result,
                activeViewIndex: activeViewIndex,
                appliedQuickFilter: request.quickFilter)
        } catch {
            return .failed(BaseDocument.friendlyMessage(source: request.source, for: error))
        }
    }

    nonisolated static func release(
        _ prepared: BasePreparedLoad,
        session: VaultSession,
        observer: BaseRetargetNativeExecutionObserver?
    ) {
        guard let handle = prepared.retainedHandle else { return }
        close(
            handle: handle,
            session: session,
            phase: .closePrepared,
            observer: observer)
    }

    nonisolated static func closeReplaced(
        handle: UInt64,
        session: VaultSession,
        observer: BaseRetargetNativeExecutionObserver?
    ) {
        close(
            handle: handle,
            session: session,
            phase: .closeReplaced,
            observer: observer)
    }

    private nonisolated static func close(
        handle: UInt64,
        session: VaultSession,
        phase: BaseRetargetNativePhase,
        observer: BaseRetargetNativeExecutionObserver?
    ) {
        observe(phase, observer: observer)
        session.closeBase(handle: handle)
    }

    nonisolated static func observe(
        _ phase: BaseRetargetNativePhase,
        observer: BaseRetargetNativeExecutionObserver?
    ) {
        observer?(
            BaseRetargetNativeExecutionEvent(
                phase: phase,
                ranOnMainThread: BaseRetargetThreadProbe.isMainThread()))
    }
}

/// Immutable ownership snapshot for Base export. The FFI call runs detached;
/// MainActor publication is allowed only while the document still owns the
/// same native handle, view, filter, and transient sort.
struct BaseExportSnapshot: @unchecked Sendable {
    let source: BaseDocumentSource
    let handle: UInt64
    let viewIndex: Int
    let format: ExportFormat
    /// The document filter that owned this snapshot. This remains populated
    /// when an unfiltered export intentionally passes `nil` to native code.
    let documentQuickFilter: String?
    let quickFilter: String?
    let sortState: DataGridSortState?
}

enum BaseNativeExporter {
    nonisolated static func run(
        session: VaultSession,
        snapshot: BaseExportSnapshot,
        observer: BaseRetargetNativeExecutionObserver?
    ) throws -> String {
        BasePreparedLoader.observe(.export, observer: observer)
        return try session.baseExport(
            handle: snapshot.handle,
            view: UInt32(snapshot.viewIndex),
            format: snapshot.format,
            quickFilter: snapshot.quickFilter)
    }
}

/// Per-open-base state (Milestone N, #702): loads the `.base` file over
/// the FFI, owns the handle, and stores the active view result. One
/// `BaseDocument` is shared by every tab/pane showing the same path.
@MainActor
final class BaseDocument: ObservableObject {
    @Published private(set) var source: BaseDocumentSource

    enum LoadState: Equatable {
        case loading
        case ready
        case degraded(String)
        case failed(String)
    }

    @Published private(set) var state: LoadState = .loading
    @Published private(set) var views: [BaseViewSummary] = []
    @Published private(set) var result: BasesResultSet?
    @Published private(set) var activeViewIndex: Int = 0
    @Published var quickFilterText = ""
    @Published var sortState: DataGridSortState?
    @Published private(set) var focusedColumnIndex: Int = 0

    private(set) var handle: UInt64?
    private var appliedQuickFilterText: String?
    private var retargetGeneration: UInt64 = 0
    @Published private var retargetPreparationPending = false
    @Published private var retargetPreparationInFlight = false
    private var pendingRetargetRequest: BasePreparedLoadRequest?
    private var contentRefreshGeneration: UInt64 = 0

    init(path: String) {
        self.source = .file(path: path)
    }

    init(source: BaseDocumentSource) {
        self.source = source
    }

    var activeViewName: String? {
        guard views.indices.contains(activeViewIndex) else { return nil }
        return views[activeViewIndex].name
    }

    var displayName: String {
        source.displayName
    }

    var selectionKey: String {
        source.selectionKey
    }

    var path: String {
        source.filePath ?? source.selectionKey
    }

    var quickFilterActive: Bool {
        editedQuickFilterArgument != nil || appliedQuickFilterText != nil
    }

    var hasPendingRetargetPreparation: Bool { retargetPreparationPending }

    var isRetargetPreparationInFlight: Bool { retargetPreparationInFlight }

    func load(session: VaultSession, thisPath: String? = nil) {
        contentRefreshGeneration &+= 1
        invalidateRetargetPreparation()
        let previousViewName = activeViewName
        if let stale = handle {
            session.closeBase(handle: stale)
            handle = nil
        }
        state = .loading
        clearQuickFilterState()
        do {
            let opened: UInt64
            switch source {
            case .file(let path):
                opened = try session.openBase(path: path)
            case .savedQuery(let id, _):
                opened = try session.openSavedQuery(id: id)
            }
            handle = opened
            views = try session.baseViews(handle: opened)
            if let previousViewName,
                let matchingIndex = views.firstIndex(where: {
                    BaseExactIdentity.matches($0.name, previousViewName)
                })
            {
                activeViewIndex = matchingIndex
            } else {
                activeViewIndex = views.isEmpty ? 0 : min(activeViewIndex, views.count - 1)
            }
            sortState = nil
            focusedColumnIndex = 0
            executeActiveView(session: session, thisPath: thisPath)
        } catch {
            handle = nil
            views = []
            result = nil
            state = .failed(friendlyMessage(for: error))
        }
    }

    func refresh(session: VaultSession, thisPath: String? = nil) {
        load(session: session, thisPath: thisPath)
    }

    func selectView(index: Int, session: VaultSession) {
        guard views.indices.contains(index) else { return }
        guard activeViewIndex != index else { return }
        guard let handle else { return }
        contentRefreshGeneration &+= 1
        do {
            try session.baseSetTransientSort(
                handle: handle,
                view: UInt32(activeViewIndex),
                columnId: nil,
                ascending: true)
        } catch {
            result = nil
            state = .failed(friendlyMessage(for: error))
            return
        }
        activeViewIndex = index
        sortState = nil
        clearQuickFilterState()
        focusedColumnIndex = 0
        executeActiveView(session: session)
    }

    func selectNextView(session: VaultSession) {
        guard !views.isEmpty else { return }
        selectView(index: min(activeViewIndex + 1, views.count - 1), session: session)
    }

    func selectPreviousView(session: VaultSession) {
        guard !views.isEmpty else { return }
        selectView(index: max(activeViewIndex - 1, 0), session: session)
    }

    func executeActiveView(session: VaultSession, thisPath: String? = nil) {
        contentRefreshGeneration &+= 1
        // A batch-Trash unknown outcome deliberately detaches the handle while
        // preserving the last truthful snapshot. Do not replace that snapshot
        // with a fabricated no-views state merely because an interaction raced
        // the disabled UI.
        guard let handle else { return }
        guard views.indices.contains(activeViewIndex) else {
            result = nil
            state = .degraded("No executable base views were found.")
            return
        }
        do {
            let appliedFilter = quickFilterArgument
            let executed = try session.baseExecute(
                handle: handle,
                view: UInt32(activeViewIndex),
                thisPath: thisPath,
                quickFilter: quickFilterArgument,
                cancel: CancelToken())
            result = executed
            appliedQuickFilterText = appliedFilter
            let view = views[activeViewIndex]
            if view.status == .fallback {
                state = .degraded("Using fallback view for \(view.name).")
            } else if view.status == .error {
                state = .degraded("View \(view.name) has errors.")
            } else if let message = executed.viewError, !message.isEmpty {
                state = .degraded(friendlyViewErrorMessage(message))
            } else if let message = firstSavedQueryThisContextError(in: executed) {
                state = .degraded(friendlyViewErrorMessage(message))
            } else {
                state = .ready
            }
        } catch {
            result = nil
            state = .failed(friendlyMessage(for: error))
        }
    }

    func exportSnapshot(
        format: ExportFormat,
        includeQuickFilter: Bool = true
    ) throws -> BaseExportSnapshot {
        guard let handle, views.indices.contains(activeViewIndex) else {
            throw BaseDocumentError.noExecutableView
        }
        return BaseExportSnapshot(
            source: source,
            handle: handle,
            viewIndex: activeViewIndex,
            format: format,
            documentQuickFilter: quickFilterArgument,
            quickFilter: includeQuickFilter ? quickFilterArgument : nil,
            sortState: sortState)
    }

    func ownsExportSnapshot(_ snapshot: BaseExportSnapshot) -> Bool {
        source == snapshot.source
            && handle == snapshot.handle
            && activeViewIndex == snapshot.viewIndex
            && quickFilterArgument == snapshot.documentQuickFilter
            && sortState == snapshot.sortState
    }

    func export(
        format: ExportFormat,
        session: VaultSession,
        includeQuickFilter: Bool = true
    ) async throws -> String {
        let snapshot = try exportSnapshot(
            format: format,
            includeQuickFilter: includeQuickFilter)
        let text = try await Task.detached(priority: .userInitiated) {
            try BaseNativeExporter.run(
                session: session,
                snapshot: snapshot,
                observer: nil)
        }.value
        guard ownsExportSnapshot(snapshot) else {
            throw BaseDocumentError.staleExport
        }
        return text
    }

    @discardableResult
    func applyQuickFilter(_ text: String, session: VaultSession) -> String {
        guard handle != nil else { return quickFilterResultAnnouncement }
        if quickFilterText != text {
            quickFilterText = text
        }
        executeActiveView(session: session)
        return quickFilterResultAnnouncement
    }

    @discardableResult
    func clearQuickFilter(session: VaultSession?) -> String? {
        guard quickFilterActive else { return nil }
        guard handle != nil else { return nil }
        clearQuickFilterState()
        if let session {
            executeActiveView(session: session)
            return quickFilterResultAnnouncement
        }
        return nil
    }

    func focusColumn(_ columnIndex: Int) {
        guard columnIndex >= 0 else { return }
        focusedColumnIndex = columnIndex
    }

    @discardableResult
    func sortFocusedColumn(session: VaultSession) -> String? {
        guard let result, result.columns.indices.contains(focusedColumnIndex) else {
            return nil
        }
        let ascending: Bool
        if sortState?.columnIndex == focusedColumnIndex {
            ascending = !(sortState?.ascending ?? false)
        } else {
            ascending = true
        }
        guard setTransientSort(
            DataGridSortState(columnIndex: focusedColumnIndex, ascending: ascending),
            session: session)
        else { return nil }
        let direction = ascending ? "ascending" : "descending"
        return "Sorted by \(result.columns[focusedColumnIndex].label), \(direction)"
    }

    @discardableResult
    func setTransientSort(_ newSort: DataGridSortState?, session: VaultSession) -> Bool {
        guard let handle, views.indices.contains(activeViewIndex) else { return false }
        let columnID: String?
        if let newSort {
            guard let result, result.columns.indices.contains(newSort.columnIndex) else {
                return false
            }
            columnID = result.columns[newSort.columnIndex].id
        } else {
            columnID = nil
        }
        do {
            try session.baseSetTransientSort(
                handle: handle,
                view: UInt32(activeViewIndex),
                columnId: columnID,
                ascending: newSort?.ascending ?? true)
            sortState = newSort
            executeActiveView(session: session)
            return true
        } catch {
            result = nil
            state = .failed(friendlyMessage(for: error))
            return false
        }
    }

    @discardableResult
    func saveSortToView(session: VaultSession) throws -> String? {
        guard let handle,
            let result,
            let sortState,
            result.columns.indices.contains(sortState.columnIndex)
        else { return nil }
        let column = result.columns[sortState.columnIndex]
        try session.baseApplyEdit(
            handle: handle,
            edit: .setSlateSort(
                view: UInt32(activeViewIndex),
                yaml: slateSortYAML(columnID: column.id, ascending: sortState.ascending)))
        try session.baseSetTransientSort(
            handle: handle,
            view: UInt32(activeViewIndex),
            columnId: nil,
            ascending: true)
        views = try session.baseViews(handle: handle)
        executeActiveView(session: session)
        let direction = sortState.ascending ? "ascending" : "descending"
        return "Saved sort by \(column.label), \(direction)."
    }

    func close(session: VaultSession) {
        contentRefreshGeneration &+= 1
        invalidateRetargetPreparation()
        if let handle {
            session.closeBase(handle: handle)
        }
        handle = nil
    }

    /// Reconciliation proved that this file was physically moved to Trash.
    /// Retain the document object for its mounted tab and any open builder,
    /// while removing native write capability and publishing a terminal state.
    func markMovedToTrash(session: VaultSession?) {
        contentRefreshGeneration &+= 1
        invalidateRetargetPreparation()
        if let handle, let session {
            session.closeBase(handle: handle)
        }
        handle = nil
        state = .failed(
            "\(displayName) was moved to Trash and is no longer available. "
                + "Choose Refresh if the file is restored.")
    }

    func retarget(to newPath: String, session: VaultSession?) {
        contentRefreshGeneration &+= 1
        invalidateRetargetPreparation()
        if let session {
            close(session: session)
        } else {
            handle = nil
        }
        source = .file(path: newPath)
        if let session {
            load(session: session)
        }
    }

    func retarget(to newSource: BaseDocumentSource, session: VaultSession?) {
        contentRefreshGeneration &+= 1
        invalidateRetargetPreparation()
        if let session {
            close(session: session)
        } else {
            handle = nil
        }
        source = newSource
        if let session {
            load(session: session)
        }
    }

    /// Rekey without closing, opening, or querying. The previous result stays
    /// visible while the detached handle is closed and a live replacement is
    /// prepared away from the main actor.
    func beginBatchRetarget(
        to newSource: BaseDocumentSource,
        thisPath: String? = nil
    ) -> BaseRetargetReservation {
        contentRefreshGeneration &+= 1
        let sortColumnID = sortState.flatMap { sort in
            result?.columns.indices.contains(sort.columnIndex) == true
                ? result?.columns[sort.columnIndex].id
                : nil
        }
        let filter = editedQuickFilterArgument ?? appliedQuickFilterText
        retargetGeneration &+= 1
        let reservation = BaseRetargetReservation(
            generation: retargetGeneration,
            replacedHandle: handle,
            request: BasePreparedLoadRequest(
                source: newSource,
                previousViewName: activeViewName,
                previousViewIndex: activeViewIndex,
                quickFilter: filter,
                sortColumnID: sortColumnID,
                sortAscending: sortState?.ascending ?? true,
                thisPath: thisPath))
        pendingRetargetRequest = reservation.request
        handle = nil
        source = newSource
        retargetPreparationPending = true
        retargetPreparationInFlight = false
        return reservation
    }

    func preparedRetargetRequest(thisPath: String?) -> BasePreparedLoadRequest {
        let base = pendingRetargetRequest ?? BasePreparedLoadRequest(
            source: source,
            previousViewName: activeViewName,
            previousViewIndex: activeViewIndex,
            quickFilter: editedQuickFilterArgument ?? appliedQuickFilterText,
            sortColumnID: sortState.flatMap { sort in
                result?.columns.indices.contains(sort.columnIndex) == true
                    ? result?.columns[sort.columnIndex].id
                    : nil
            },
            sortAscending: sortState?.ascending ?? true,
            thisPath: thisPath)
        return BasePreparedLoadRequest(
            source: base.source,
            previousViewName: base.previousViewName,
            previousViewIndex: base.previousViewIndex,
            quickFilter: base.quickFilter,
            sortColumnID: base.sortColumnID,
            sortAscending: base.sortAscending,
            thisPath: thisPath)
    }

    func claimRetargetPreparation() -> UInt64? {
        guard retargetPreparationPending, !retargetPreparationInFlight else {
            return nil
        }
        retargetPreparationInFlight = true
        return retargetGeneration
    }

    func ownsRetargetPreparation(
        generation: UInt64,
        source: BaseDocumentSource
    ) -> Bool {
        retargetPreparationPending
            && retargetPreparationInFlight
            && retargetGeneration == generation
            && self.source == source
    }

    @discardableResult
    func applyRetargetPreparation(
        _ prepared: BasePreparedLoad,
        generation: UInt64,
        source: BaseDocumentSource
    ) -> Bool {
        guard ownsRetargetPreparation(generation: generation, source: source) else {
            return false
        }
        retargetPreparationInFlight = false
        contentRefreshGeneration &+= 1

        switch prepared {
        case .ready(let preparedHandle, let preparedViews, let preparedResult,
            let preparedActiveViewIndex, let preparedFilter):
            handle = preparedHandle
            views = preparedViews
            result = preparedResult
            activeViewIndex = preparedActiveViewIndex
            appliedQuickFilterText = preparedFilter
            state = preparedState(
                views: preparedViews,
                result: preparedResult,
                activeViewIndex: preparedActiveViewIndex)
            retargetPreparationPending = false
            pendingRetargetRequest = nil
        case .failed(let message):
            // Base renders degraded results with its previous data still
            // present, avoiding a blank pane while keeping the document
            // read-only and retryable.
            state = .degraded(message)
            retargetPreparationPending = true
        }
        return true
    }

    /// Capture immutable state for a normal post-write refresh without
    /// detaching the current handle. The existing result remains visible until
    /// a fully prepared replacement is ready and still owned.
    func beginContentRefresh(thisPath: String?) -> BaseContentRefreshReservation {
        contentRefreshGeneration &+= 1
        let generation = contentRefreshGeneration
        return BaseContentRefreshReservation(
            generation: generation,
            replacedHandle: handle,
            request: BasePreparedLoadRequest(
                source: source,
                previousViewName: activeViewName,
                previousViewIndex: activeViewIndex,
                quickFilter: editedQuickFilterArgument ?? appliedQuickFilterText,
                sortColumnID: currentSortColumnID,
                sortAscending: sortState?.ascending ?? true,
                thisPath: thisPath))
    }

    func ownsContentRefresh(_ reservation: BaseContentRefreshReservation) -> Bool {
        contentRefreshGeneration == reservation.generation
            && source == reservation.request.source
            && handle == reservation.replacedHandle
            && activeViewIndex == reservation.request.previousViewIndex
            && activeViewName == reservation.request.previousViewName
            && (editedQuickFilterArgument ?? appliedQuickFilterText)
                == reservation.request.quickFilter
            && currentSortColumnID == reservation.request.sortColumnID
            && (sortState?.ascending ?? true) == reservation.request.sortAscending
    }

    func applyContentRefresh(
        _ prepared: BasePreparedLoad,
        reservation: BaseContentRefreshReservation
    ) -> BaseContentRefreshApplication {
        guard ownsContentRefresh(reservation) else { return .stale }
        switch prepared {
        case .ready(
            let preparedHandle,
            let preparedViews,
            let preparedResult,
            let preparedActiveViewIndex,
            let preparedFilter
        ):
            let replacedHandle = handle
            handle = preparedHandle
            views = preparedViews
            result = preparedResult
            activeViewIndex = preparedActiveViewIndex
            appliedQuickFilterText = preparedFilter
            if let columnID = reservation.request.sortColumnID,
                let preparedResult,
                let index = preparedResult.columns.firstIndex(where: {
                    BaseExactIdentity.matches($0.id, columnID)
                })
            {
                sortState = DataGridSortState(
                    columnIndex: index,
                    ascending: reservation.request.sortAscending)
            } else {
                sortState = nil
            }
            focusedColumnIndex = preparedResult.map {
                min(focusedColumnIndex, max($0.columns.count - 1, 0))
            } ?? 0
            state = preparedState(
                views: preparedViews,
                result: preparedResult,
                activeViewIndex: preparedActiveViewIndex)
            contentRefreshGeneration &+= 1
            return .applied(replacedHandle: replacedHandle)
        case .failed(let message):
            // Preserve the prior rows and native handle. This is both more
            // usable and retryable than blanking a visible Base on a transient
            // refresh failure.
            state = .degraded(message)
            contentRefreshGeneration &+= 1
            return .failed
        }
    }

    func retargetSavedQueryName(_ name: String) {
        guard case .savedQuery(let id, _) = source else { return }
        source = .savedQuery(id: id, name: name)
    }

    private var quickFilterArgument: String? {
        editedQuickFilterArgument
    }

    private var currentSortColumnID: String? {
        guard let sortState,
            let result,
            result.columns.indices.contains(sortState.columnIndex)
        else { return nil }
        return result.columns[sortState.columnIndex].id
    }

    private var editedQuickFilterArgument: String? {
        guard !quickFilterText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return nil
        }
        return quickFilterText
    }

    var quickFilterResultAnnouncement: String {
        guard let result else { return "0 of 0 results" }
        let total = quickFilterActive ? result.unfilteredShownCount : result.totalCount
        return "\(result.shownCount) of \(total) results"
    }

    var whereAmIReadback: String {
        var parts = ["Base: \(displayName)"]
        if let activeViewName {
            parts.append("view: \(activeViewName)")
        }
        if quickFilterActive {
            parts.append("quick filter: \(editedQuickFilterArgument ?? appliedQuickFilterText ?? "")")
        }
        return parts.joined(separator: ", ")
    }

    private func clearQuickFilterState() {
        if !quickFilterText.isEmpty {
            quickFilterText = ""
        }
        appliedQuickFilterText = nil
    }

    private func invalidateRetargetPreparation() {
        retargetGeneration &+= 1
        retargetPreparationPending = false
        retargetPreparationInFlight = false
        pendingRetargetRequest = nil
    }

    private func preparedState(
        views: [BaseViewSummary],
        result: BasesResultSet?,
        activeViewIndex: Int
    ) -> LoadState {
        guard views.indices.contains(activeViewIndex) else {
            return .degraded("No executable base views were found.")
        }
        let view = views[activeViewIndex]
        if view.status == .fallback {
            return .degraded("Using fallback view for \(view.name).")
        }
        if view.status == .error {
            return .degraded("View \(view.name) has errors.")
        }
        if let message = result?.viewError, !message.isEmpty {
            return .degraded(friendlyViewErrorMessage(message))
        }
        if let result, let message = firstSavedQueryThisContextError(in: result) {
            return .degraded(friendlyViewErrorMessage(message))
        }
        return .ready
    }

    private enum BaseDocumentError: LocalizedError {
        case noExecutableView
        case staleExport

        var errorDescription: String? {
            switch self {
            case .noExecutableView:
                return "No executable base view."
            case .staleExport:
                return "The Base view changed before the export finished."
            }
        }
    }

    private func slateSortYAML(columnID: String, ascending: Bool) -> String {
        [
            "- property: \(quoteYAMLString(columnID))",
            "  direction: \(ascending ? "ASC" : "DESC")",
        ].joined(separator: "\n")
    }

    private func quoteYAMLString(_ value: String) -> String {
        let escaped = value
            .replacingOccurrences(of: "\\", with: "\\\\")
            .replacingOccurrences(of: "\n", with: "\\n")
            .replacingOccurrences(of: "\r", with: "\\r")
            .replacingOccurrences(of: "\t", with: "\\t")
            .replacingOccurrences(of: "\"", with: "\\\"")
        return "\"\(escaped)\""
    }

    nonisolated static func friendlyMessage(
        source: BaseDocumentSource,
        for error: Error
    ) -> String {
        let displayName = source.displayName
        if let vaultError = error as? VaultError {
            switch vaultError {
            case .Io:
                return "\(displayName) could not be read — it may have been moved or deleted."
            case .FileTooLarge:
                return "\(displayName) is too large to open."
            case .InvalidUtf8:
                return "\(displayName) is not valid UTF-8 text."
            default:
                break
            }
        }
        return "\(displayName) could not be opened: \(error.localizedDescription)"
    }

    private func friendlyMessage(for error: Error) -> String {
        Self.friendlyMessage(source: source, for: error)
    }

    private func friendlyViewErrorMessage(_ message: String) -> String {
        guard case .savedQuery = source,
            message.contains("this is unavailable in this evaluation context"),
            !message.contains("Dock to sidebar to follow the active note.")
        else { return message }
        return "\(message) Dock to sidebar to follow the active note."
    }

    private func firstSavedQueryThisContextError(in result: BasesResultSet) -> String? {
        guard case .savedQuery = source else { return nil }
        for row in result.rows {
            for value in row.values {
                if let error = value.error,
                    error.contains("this is unavailable in this evaluation context")
                {
                    return error
                }
            }
        }
        return nil
    }
}
