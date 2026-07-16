// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

enum BaseEmbedKind: Equatable, Hashable {
    case file
    case inlineBase
    case savedQuery
    case dataview
}

enum BaseEmbedOpenDestination: Equatable, Hashable {
    case baseFile(path: String)
    case savedQuery(reference: String)
    case sourceNote(path: String)

    static func == (lhs: BaseEmbedOpenDestination, rhs: BaseEmbedOpenDestination) -> Bool {
        switch (lhs, rhs) {
        case (.baseFile(let lhs), .baseFile(let rhs)),
            (.savedQuery(let lhs), .savedQuery(let rhs)),
            (.sourceNote(let lhs), .sourceNote(let rhs)):
            return BaseExactIdentity.matches(lhs, rhs)
        default:
            return false
        }
    }

    func hash(into hasher: inout Hasher) {
        switch self {
        case .baseFile(let path):
            hasher.combine(0)
            BaseExactIdentity.hash(path, into: &hasher)
        case .savedQuery(let reference):
            hasher.combine(1)
            BaseExactIdentity.hash(reference, into: &hasher)
        case .sourceNote(let path):
            hasher.combine(2)
            BaseExactIdentity.hash(path, into: &hasher)
        }
    }
}

struct BaseEmbedRecoveryAction: Equatable {
    let title: String
    let destination: BaseEmbedOpenDestination
    let accessibilityHint: String
}

/// A normalized embedded-Base source. Parsing is intentionally small:
/// Rust owns Bases/DQL semantics; this type only recognizes which FFI
/// opener to call and preserves the source text that opener must parse.
struct BaseEmbedRequest: Equatable, Hashable {
    private enum Source: Equatable, Hashable {
        case file(path: String, viewName: String?)
        case inlineBase(source: String)
        case savedQuery(reference: String, viewName: String?)
        case dataview(source: String)
        case invalidSlateQuery(source: String, message: String)

        static func == (lhs: Source, rhs: Source) -> Bool {
            switch (lhs, rhs) {
            case (.file(let lhsPath, let lhsView), .file(let rhsPath, let rhsView)):
                return BaseExactIdentity.matches(lhsPath, rhsPath)
                    && BaseExactIdentity.matches(lhsView, rhsView)
            case (.inlineBase(let lhs), .inlineBase(let rhs)),
                (.dataview(let lhs), .dataview(let rhs)):
                return BaseExactIdentity.matches(lhs, rhs)
            case (.invalidSlateQuery(let lhsSource, let lhsMessage),
                .invalidSlateQuery(let rhsSource, let rhsMessage)):
                return BaseExactIdentity.matches(lhsSource, rhsSource)
                    && BaseExactIdentity.matches(lhsMessage, rhsMessage)
            case (.savedQuery(let lhsReference, let lhsView),
                .savedQuery(let rhsReference, let rhsView)):
                return BaseExactIdentity.matches(lhsReference, rhsReference)
                    && BaseExactIdentity.matches(lhsView, rhsView)
            default:
                return false
            }
        }

        func hash(into hasher: inout Hasher) {
            switch self {
            case .file(let path, let viewName):
                hasher.combine(0)
                BaseExactIdentity.hash(path, into: &hasher)
                BaseExactIdentity.hash(viewName, into: &hasher)
            case .inlineBase(let source):
                hasher.combine(1)
                BaseExactIdentity.hash(source, into: &hasher)
            case .savedQuery(let reference, let viewName):
                hasher.combine(2)
                BaseExactIdentity.hash(reference, into: &hasher)
                BaseExactIdentity.hash(viewName, into: &hasher)
            case .dataview(let source):
                hasher.combine(3)
                BaseExactIdentity.hash(source, into: &hasher)
            case .invalidSlateQuery(let source, let message):
                hasher.combine(4)
                BaseExactIdentity.hash(source, into: &hasher)
                BaseExactIdentity.hash(message, into: &hasher)
            }
        }
    }

    private let source: Source

    var kind: BaseEmbedKind {
        switch source {
        case .file: return .file
        case .inlineBase: return .inlineBase
        case .savedQuery: return .savedQuery
        case .dataview: return .dataview
        case .invalidSlateQuery: return .inlineBase
        }
    }

    var targetPath: String? {
        if case .file(let path, _) = source { return path }
        return nil
    }

    var viewName: String? {
        switch source {
        case .file(_, let viewName), .savedQuery(_, let viewName):
            return viewName
        case .inlineBase, .dataview, .invalidSlateQuery:
            return nil
        }
    }

    var inlineSource: String {
        switch source {
        case .inlineBase(let source), .dataview(let source),
            .invalidSlateQuery(let source, _):
            return source
        case .file, .savedQuery:
            return ""
        }
    }

    var savedQueryReference: String? {
        if case .savedQuery(let reference, _) = source { return reference }
        return nil
    }

    var accessibilityLabel: String {
        let label: String
        switch source {
        case .file(let path, let viewName):
            let name = ((path as NSString).lastPathComponent as NSString).deletingPathExtension
            label = "Embedded base: \(name)" + viewSuffix(viewName)
        case .inlineBase:
            label = "Embedded base: inline base"
        case .savedQuery(let reference, let viewName):
            label = "Embedded base: saved query \(reference)" + viewSuffix(viewName)
        case .dataview:
            label = "Embedded base: Dataview"
        case .invalidSlateQuery:
            label = "Embedded base: invalid slate-query"
        }
        return label
    }

    func recoveryAction(thisPath: String?) -> BaseEmbedRecoveryAction? {
        switch source {
        case .file(let path, _):
            return BaseEmbedRecoveryAction(
                title: "Open base in tab",
                destination: .baseFile(path: path),
                accessibilityHint: "Opens the base file in a tab where editing is available.")
        case .savedQuery(let reference, _):
            return BaseEmbedRecoveryAction(
                title: "Open saved query in tab",
                destination: .savedQuery(reference: reference),
                accessibilityHint: "Opens the saved query in a tab where its query can be edited.")
        case .inlineBase, .dataview, .invalidSlateQuery:
            guard let thisPath else { return nil }
            return BaseEmbedRecoveryAction(
                title: "Edit source note",
                destination: .sourceNote(path: thisPath),
                accessibilityHint: "Switches the source note containing this query to editing mode.")
        }
    }

    func readOnlyHint(thisPath: String?) -> String {
        switch source {
        case .file:
            return "read-only in embeds — open the base file in a tab to edit"
        case .savedQuery:
            return "read-only in embeds — open the saved query in a tab to edit"
        case .inlineBase:
            return thisPath == nil
                ? "read-only in embeds — edit the source block to change this query"
                : "read-only in embeds — open the source note to edit this query block"
        case .dataview:
            return thisPath == nil
                ? "read-only in embeds — convert this Dataview block to a .base file to edit"
                : "read-only in embeds — open the source note to edit or convert this Dataview block"
        case .invalidSlateQuery:
            return thisPath == nil
                ? "invalid slate-query — edit the source block"
                : "invalid slate-query — open the source note to correct the query block"
        }
    }

    var cacheKey: String {
        switch source {
        case .file(let path, let viewName):
            return BaseExactIdentity.key(
                prefix: "embed-file", components: [path, viewName])
        case .inlineBase(let source):
            return BaseExactIdentity.key(prefix: "embed-inline", components: [source])
        case .savedQuery(let reference, let viewName):
            return BaseExactIdentity.key(
                prefix: "embed-saved-query", components: [reference, viewName])
        case .dataview(let source):
            return BaseExactIdentity.key(prefix: "embed-dql", components: [source])
        case .invalidSlateQuery(let source, let message):
            return BaseExactIdentity.key(
                prefix: "embed-invalid-slate-query", components: [source, message])
        }
    }

    static func wikilinkTarget(_ target: String) -> BaseEmbedRequest? {
        let trimmed = target.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        let split = splitBaseTarget(trimmed)
        guard split.path.lowercased().hasSuffix(".base") else { return nil }
        return BaseEmbedRequest(source: .file(path: split.path, viewName: split.viewName))
    }

    static func codeFence(language: String, source: String) -> BaseEmbedRequest? {
        let tag = language.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        let body = ReadingBlockSource.fenceInteriorVerbatim(source)
        switch tag {
        case "base":
            return BaseEmbedRequest(source: .inlineBase(source: body))
        case "slate-query":
            do {
                let classification = try classifySlateQueryFence(source: body)
                if let query = classification.query {
                    return BaseEmbedRequest(
                        source: .savedQuery(
                            reference: query, viewName: classification.view))
                }
                return BaseEmbedRequest(source: .inlineBase(source: body))
            } catch let VaultError.InvalidQuery(message) {
                return BaseEmbedRequest(
                    source: .invalidSlateQuery(source: body, message: message))
            } catch {
                return BaseEmbedRequest(
                    source: .invalidSlateQuery(
                        source: body, message: error.localizedDescription))
            }
        case "dataview":
            return BaseEmbedRequest(source: .dataview(source: body))
        default:
            return nil
        }
    }

    static func requests(in text: String) -> [BaseEmbedRequest] {
        previews(in: text).map(\.request)
    }

    fileprivate func openHandle(
        session: VaultSession,
        thisPath: String?,
        resolvedSavedQueryID: String? = nil
    ) throws -> OpenedBaseEmbedHandle {
        switch source {
        case .file(let path, _):
            return OpenedBaseEmbedHandle(
                handle: try session.openBase(path: path),
                savedQueryID: nil)
        case .inlineBase(let source):
            return OpenedBaseEmbedHandle(
                handle: try session.openBaseInline(source: source, thisPath: thisPath),
                savedQueryID: nil)
        case .savedQuery(let reference, let viewName):
            let query = try resolvedSavedQueryID
                ?? Self.savedQueryID(reference: reference, session: session)
            let saved = try session.getSavedQuery(id: query)
            let queryJSON = try Self.savedQueryJSON(
                fromEnvelope: saved.queryJson, viewOverride: viewName)
            return OpenedBaseEmbedHandle(
                handle: try session.openQuery(queryJson: queryJSON, thisPath: thisPath),
                savedQueryID: query)
        case .dataview(let source):
            return OpenedBaseEmbedHandle(
                handle: try session.openDql(source: source, thisPath: thisPath),
                savedQueryID: nil)
        case .invalidSlateQuery(_, let message):
            throw BaseEmbedDocumentError.message(message)
        }
    }

    fileprivate var requestedViewName: String? { viewName }

    fileprivate func displayViews(from views: [BaseViewSummary]) -> [BaseViewSummary] {
        guard case .savedQuery(_, let viewName) = source,
            let viewName,
            views.count == 1,
            let view = views.first
        else { return views }
        return [
            BaseViewSummary(
                name: viewName,
                viewType: Self.savedQueryRendererOverride(viewName) ?? view.viewType,
                source: view.source,
                status: view.status,
                slateStateJson: view.slateStateJson)
        ]
    }

    static func savedQuerySummary(
        reference: String,
        in summaries: [SavedQuerySummary]
    ) -> SavedQuerySummary? {
        summaries.first(where: { BaseExactIdentity.matches($0.id, reference) })
            ?? summaries.first(where: { BaseExactIdentity.matches($0.name, reference) })
    }

    fileprivate static func savedQueryID(reference: String, session: VaultSession) throws -> String {
        let summaries = try session.listSavedQueries()
        if let match = savedQuerySummary(reference: reference, in: summaries) {
            return match.id
        }
        let available = summaries.map(\.name)
        throw BaseEmbedDocumentError.message(
            "Unknown saved query \(reference). Available saved queries: \(availableList(available)).")
    }

    private static func splitBaseTarget(_ target: String) -> (path: String, viewName: String?) {
        guard let hash = target.firstIndex(of: "#") else {
            return (target, nil)
        }
        let path = String(target[..<hash])
        let view = String(target[target.index(after: hash)...])
            .trimmingCharacters(in: .whitespacesAndNewlines)
        return (path, view.isEmpty ? nil : view)
    }

    private static func savedQueryJSON(
        fromEnvelope envelope: String,
        viewOverride: String?
    ) throws -> String {
        let data = Data(envelope.utf8)
        let object = try JSONSerialization.jsonObject(with: data)
        var query = ((object as? [String: Any])?["query"] as? [String: Any])
            ?? (object as? [String: Any])
            ?? [:]
        if let override = savedQueryRendererOverride(viewOverride) {
            let key = override == "list" ? "List" : "Table"
            query["view"] = [key: ["fallback_from": NSNull()]]
        }
        let normalized = try JSONSerialization.data(
            withJSONObject: query, options: [.sortedKeys])
        guard let json = String(data: normalized, encoding: .utf8) else {
            throw BaseEmbedDocumentError.message("Saved query could not be decoded as UTF-8.")
        }
        return json
    }

    private static func savedQueryRendererOverride(_ viewName: String?) -> String? {
        switch viewName?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() {
        case "table": return "table"
        case "list": return "list"
        default: return nil
        }
    }
}

fileprivate struct OpenedBaseEmbedHandle {
    let handle: UInt64
    let savedQueryID: String?
}

struct BaseEmbedDocumentRefreshSnapshot: @unchecked Sendable {
    let id: ObjectIdentifier
    let generation: UInt64
    let previousViewName: String?
    let previousViewIndex: Int
    let quickFilter: String?
    let sortSelection: BaseGridSortSelection?
}

struct BaseEmbedHandleRefreshReservation: @unchecked Sendable {
    let generation: UInt64
    let replacedHandle: UInt64
    let request: BaseEmbedRequest
    let thisPath: String?
    let resolvedSavedQueryID: String?
    let documents: [BaseEmbedDocumentRefreshSnapshot]
}

struct BaseEmbedPreparedDocument: @unchecked Sendable {
    let id: ObjectIdentifier
    let views: [BaseViewSummary]
    let activeViewIndex: Int
    let result: BasesResultSet?
    let appliedQuickFilter: String?
    let sortSelection: BaseGridSortSelection?
    let failure: String?
}

enum BaseEmbedPreparedRefresh: @unchecked Sendable {
    case ready(
        handle: UInt64,
        views: [BaseViewSummary],
        resolvedSavedQueryID: String?,
        documents: [BaseEmbedPreparedDocument]
    )
    case failed(String)

    var retainedHandle: UInt64? {
        guard case .ready(let handle, _, _, _) = self else { return nil }
        return handle
    }
}

enum BaseEmbedRefreshApplication: Sendable {
    case applied(replacedHandle: UInt64)
    case failed
    case stale
}

enum BaseEmbedPreparedLoader {
    nonisolated static func prepare(
        session: VaultSession,
        reservation: BaseEmbedHandleRefreshReservation,
        observer: BaseRetargetNativeExecutionObserver?
    ) -> BaseEmbedPreparedRefresh {
        var openedHandle: UInt64?
        var transferred = false
        defer {
            if let openedHandle, !transferred {
                BasePreparedLoader.observe(.refreshClosePrepared, observer: observer)
                session.closeBase(handle: openedHandle)
            }
        }

        do {
            BasePreparedLoader.observe(.refreshOpen, observer: observer)
            let opened = try reservation.request.openHandle(
                session: session,
                thisPath: reservation.thisPath,
                resolvedSavedQueryID: reservation.resolvedSavedQueryID)
            openedHandle = opened.handle
            BasePreparedLoader.observe(.refreshViews, observer: observer)
            let rawViews = try session.baseViews(handle: opened.handle)
            let displayViews = reservation.request.displayViews(from: rawViews)
            var preparedDocuments: [BaseEmbedPreparedDocument] = []
            preparedDocuments.reserveCapacity(reservation.documents.count)

            for snapshot in reservation.documents {
                let activeViewIndex: Int
                if let requested = reservation.request.requestedViewName,
                    let requestedIndex = displayViews.firstIndex(where: {
                        BaseExactIdentity.matches($0.name, requested)
                    })
                {
                    activeViewIndex = requestedIndex
                } else if let previousName = snapshot.previousViewName,
                    let matchingIndex = displayViews.firstIndex(where: {
                        BaseExactIdentity.matches($0.name, previousName)
                    })
                {
                    activeViewIndex = matchingIndex
                } else {
                    activeViewIndex = displayViews.isEmpty
                        ? 0 : min(snapshot.previousViewIndex, displayViews.count - 1)
                }

                guard displayViews.indices.contains(activeViewIndex) else {
                    preparedDocuments.append(
                        BaseEmbedPreparedDocument(
                            id: snapshot.id,
                            views: displayViews,
                            activeViewIndex: activeViewIndex,
                            result: nil,
                            appliedQuickFilter: snapshot.quickFilter,
                            sortSelection: nil,
                            failure: "No executable base views were found."))
                    continue
                }

                do {
                    BasePreparedLoader.observe(.refreshSort, observer: observer)
                    try session.baseSetTransientSort(
                        handle: opened.handle,
                        view: UInt32(activeViewIndex),
                        columnId: snapshot.sortSelection?.columnID,
                        ascending: snapshot.sortSelection?.ascending ?? true)
                    BasePreparedLoader.observe(.refreshExecute, observer: observer)
                    let result = try session.baseExecute(
                        handle: opened.handle,
                        view: UInt32(activeViewIndex),
                        thisPath: reservation.thisPath,
                        quickFilter: snapshot.quickFilter,
                        cancel: CancelToken())
                    let survivingSort = snapshot.sortSelection.flatMap {
                        $0.sortState(in: result) == nil ? nil : $0
                    }
                    preparedDocuments.append(
                        BaseEmbedPreparedDocument(
                            id: snapshot.id,
                            views: displayViews,
                            activeViewIndex: activeViewIndex,
                            result: result,
                            appliedQuickFilter: snapshot.quickFilter,
                            sortSelection: survivingSort,
                            failure: nil))
                } catch {
                    preparedDocuments.append(
                        BaseEmbedPreparedDocument(
                            id: snapshot.id,
                            views: displayViews,
                            activeViewIndex: activeViewIndex,
                            result: nil,
                            appliedQuickFilter: snapshot.quickFilter,
                            sortSelection: snapshot.sortSelection,
                            failure: error.localizedDescription))
                }
            }

            transferred = true
            return .ready(
                handle: opened.handle,
                views: rawViews,
                resolvedSavedQueryID: opened.savedQueryID,
                documents: preparedDocuments)
        } catch {
            return .failed(error.localizedDescription)
        }
    }

    nonisolated static func release(
        _ prepared: BaseEmbedPreparedRefresh,
        session: VaultSession,
        observer: BaseRetargetNativeExecutionObserver?
    ) {
        guard let handle = prepared.retainedHandle else { return }
        BasePreparedLoader.observe(.refreshClosePrepared, observer: observer)
        session.closeBase(handle: handle)
    }

    nonisolated static func closeReplaced(
        handle: UInt64,
        session: VaultSession,
        observer: BaseRetargetNativeExecutionObserver?
    ) {
        BasePreparedLoader.observe(.refreshCloseReplaced, observer: observer)
        session.closeBase(handle: handle)
    }
}

struct BaseEmbedCacheKey: Equatable, Hashable {
    let request: BaseEmbedRequest
    let thisPath: String?

    static func == (lhs: BaseEmbedCacheKey, rhs: BaseEmbedCacheKey) -> Bool {
        lhs.request == rhs.request && BaseExactIdentity.matches(lhs.thisPath, rhs.thisPath)
    }

    func hash(into hasher: inout Hasher) {
        hasher.combine(request)
        BaseExactIdentity.hash(thisPath, into: &hasher)
    }

    var exactIdentityKey: String {
        BaseExactIdentity.key(
            prefix: "embed-cache", components: [request.cacheKey, thisPath])
    }
}

struct BaseEmbedPreview: Equatable {
    let request: BaseEmbedRequest
    let sourceLine: Int
}

extension BaseEmbedRequest {
    static func previews(in text: String) -> [BaseEmbedPreview] {
        let lineStarts = ReadingBlockSource.lineStartOffsets(of: text)
        return readingBlocksSource(source: text).compactMap { block in
            let request: BaseEmbedRequest?
            switch block.kind {
            case .paragraph:
                request = ReadingInlineMapper.blockEmbedTarget(inSlice: block.source)
                    .flatMap(wikilinkTarget)
            case .codeFence(let language, _):
                // `BaseEmbedRequest.codeFence` re-parses the RAW fenced source
                // (language line + delimiters) to detect base/dataview queries,
                // so the authoritative `interior` isn't used here.
                request = codeFence(language: language, source: block.source)
            default:
                request = nil
            }
            guard let request else { return nil }
            return BaseEmbedPreview(
                request: request,
                sourceLine: ReadingBlockSource.lineNumber(
                    forByteOffset: Int(block.byteStart), lineStarts: lineStarts))
        }
    }
}

private func viewSuffix(_ viewName: String?) -> String {
    guard let viewName, !viewName.isEmpty else { return "" }
    return ", view \(viewName)"
}

private func availableList(_ values: [String]) -> String {
    values.isEmpty ? "none" : values.joined(separator: ", ")
}

enum BaseEmbedDocumentError: LocalizedError {
    case message(String)

    var errorDescription: String? {
        switch self {
        case .message(let text): return text
        }
    }
}

@MainActor
final class BaseEmbedHandle {
    let request: BaseEmbedRequest
    let thisPath: String?

    private(set) var handle: UInt64?
    private(set) var views: [BaseViewSummary] = []
    private(set) var resolvedSavedQueryID: String?
    private(set) weak var session: VaultSession?
    private var documents: [ObjectIdentifier: WeakBaseEmbedDocument] = [:]
    private var mountedLeaseCount = 0
    private var contentRefreshGeneration: UInt64 = 0

    init(request: BaseEmbedRequest, thisPath: String?) {
        self.request = request
        self.thisPath = thisPath
    }

    func loadIfNeeded(session: VaultSession) throws {
        if handle != nil {
            guard self.session === session else {
                throw BaseEmbedDocumentError.message(
                    "Embedded base belongs to a replaced vault session.")
            }
            return
        }
        do {
            contentRefreshGeneration &+= 1
            let opened = try request.openHandle(
                session: session,
                thisPath: thisPath,
                resolvedSavedQueryID: resolvedSavedQueryID)
            handle = opened.handle
            resolvedSavedQueryID = opened.savedQueryID
            self.session = session
            views = try session.baseViews(handle: opened.handle)
        } catch {
            close(session: session)
            throw error
        }
    }

    func reload(session: VaultSession) throws {
        close(session: session)
        try loadIfNeeded(session: session)
    }

    func register(_ document: BaseEmbedDocument) {
        pruneDocuments()
        documents[ObjectIdentifier(document)] = WeakBaseEmbedDocument(document)
    }

    func unregister(_ document: BaseEmbedDocument) {
        documents[ObjectIdentifier(document)] = nil
        pruneDocuments()
        closeIfUnleased()
    }

    func acquireMountedLease() {
        mountedLeaseCount += 1
    }

    func releaseMountedLease() {
        guard mountedLeaseCount > 0 else { return }
        mountedLeaseCount -= 1
        closeIfUnleased()
    }

    var hasLiveLease: Bool {
        pruneDocuments()
        return mountedLeaseCount > 0 || !documents.isEmpty
    }

    private func closeIfUnleased() {
        guard mountedLeaseCount == 0, documents.isEmpty, let session else { return }
        close(session: session)
    }

    var liveDocuments: [BaseEmbedDocument] {
        pruneDocuments()
        return documents.values.compactMap(\.document)
    }

    func close(session: VaultSession) {
        contentRefreshGeneration &+= 1
        if let handle {
            session.closeBase(handle: handle)
        }
        handle = nil
        views = []
        self.session = nil
    }

    private func pruneDocuments() {
        documents = documents.filter { $0.value.document != nil }
    }

    func beginContentRefresh() -> BaseEmbedHandleRefreshReservation? {
        pruneDocuments()
        guard let handle, session != nil, !documents.isEmpty else { return nil }
        let liveDocuments = documents.values.compactMap(\.document)
            .filter { !$0.needsInitialLoad }
        guard !liveDocuments.isEmpty else { return nil }
        contentRefreshGeneration &+= 1
        return BaseEmbedHandleRefreshReservation(
            generation: contentRefreshGeneration,
            replacedHandle: handle,
            request: request,
            thisPath: thisPath,
            resolvedSavedQueryID: resolvedSavedQueryID,
            documents: liveDocuments.map { $0.beginContentRefresh() })
    }

    func applyContentRefresh(
        _ prepared: BaseEmbedPreparedRefresh,
        reservation: BaseEmbedHandleRefreshReservation,
        session: VaultSession
    ) -> BaseEmbedRefreshApplication {
        pruneDocuments()
        let liveByID = Dictionary(
            uniqueKeysWithValues: documents.values.compactMap(\.document).map {
                (ObjectIdentifier($0), $0)
            })
        guard self.session === session,
            contentRefreshGeneration == reservation.generation,
            handle == reservation.replacedHandle,
            request == reservation.request,
            reservation.documents.allSatisfy({ snapshot in
                liveByID[snapshot.id]?.ownsContentRefresh(snapshot) == true
            })
        else { return .stale }

        switch prepared {
        case .ready(
            let preparedHandle,
            let preparedViews,
            let preparedSavedQueryID,
            let preparedDocuments
        ):
            let replacedHandle = reservation.replacedHandle
            handle = preparedHandle
            views = preparedViews
            resolvedSavedQueryID = preparedSavedQueryID
            self.session = session
            let preparedByID = Dictionary(
                uniqueKeysWithValues: preparedDocuments.map { ($0.id, $0) })
            for snapshot in reservation.documents {
                guard let document = liveByID[snapshot.id],
                    let result = preparedByID[snapshot.id]
                else { continue }
                document.applyContentRefresh(result, snapshot: snapshot)
            }
            contentRefreshGeneration &+= 1
            return .applied(replacedHandle: replacedHandle)
        case .failed(let message):
            reservation.documents.forEach { snapshot in
                liveByID[snapshot.id]?.applyContentRefreshFailure(
                    message, snapshot: snapshot)
            }
            contentRefreshGeneration &+= 1
            return .failed
        }
    }
}

@MainActor
private final class WeakBaseEmbedDocument {
    weak var document: BaseEmbedDocument?

    init(_ document: BaseEmbedDocument) {
        self.document = document
    }
}

/// Per-embed executable state. Unlike `BaseDocument`, this is deliberately
/// read-only: it never calls `base_apply_edit` or property write APIs.
@MainActor
final class BaseEmbedDocument: ObservableObject {
    enum LoadState: Equatable {
        case idle
        case loading
        case ready
        case degraded(String)
        case failed(String)
    }

    let request: BaseEmbedRequest
    let thisPath: String?
    private let sharedHandle: BaseEmbedHandle

    @Published private(set) var state: LoadState = .idle
    @Published private(set) var views: [BaseViewSummary] = []
    @Published private(set) var result: BasesResultSet?
    @Published private(set) var activeViewIndex: Int = 0
    @Published var quickFilterText = ""
    @Published private(set) var sortSelection: BaseGridSortSelection?
    private var appliedQuickFilterText: String?
    private var contentRefreshGeneration: UInt64 = 0

    var handle: UInt64? { sharedHandle.handle }

    var needsInitialLoad: Bool {
        if case .idle = state { return true }
        return false
    }

    init(
        request: BaseEmbedRequest,
        thisPath: String?,
        sharedHandle: BaseEmbedHandle? = nil
    ) {
        self.request = request
        self.thisPath = thisPath
        self.sharedHandle = sharedHandle ?? BaseEmbedHandle(request: request, thisPath: thisPath)
        self.sharedHandle.register(self)
    }

    var activeViewName: String? {
        guard views.indices.contains(activeViewIndex) else { return nil }
        return views[activeViewIndex].name
    }

    var cellEditingAccessibilityHint: String { request.readOnlyHint(thisPath: thisPath) }

    var recoveryAction: BaseEmbedRecoveryAction? {
        guard let action = request.recoveryAction(thisPath: thisPath) else { return nil }
        guard case .savedQuery(_) = action.destination,
            let resolvedSavedQueryID = sharedHandle.resolvedSavedQueryID
        else { return action }
        return BaseEmbedRecoveryAction(
            title: action.title,
            destination: .savedQuery(reference: resolvedSavedQueryID),
            accessibilityHint: action.accessibilityHint)
    }

    var sortState: DataGridSortState? {
        guard let result else { return nil }
        return sortSelection?.sortState(in: result)
    }

    var quickFilterActive: Bool {
        editedQuickFilterArgument != nil || appliedQuickFilterText != nil
    }

    var quickFilterResultAnnouncement: String {
        guard let result else { return "0 of 0 results" }
        let total = quickFilterActive ? result.unfilteredShownCount : result.totalCount
        return "\(result.shownCount) of \(total) results"
    }

    func load(session: VaultSession) {
        contentRefreshGeneration &+= 1
        state = .loading
        result = nil
        views = []
        activeViewIndex = 0
        sortSelection = nil
        do {
            try sharedHandle.loadIfNeeded(session: session)
            views = request.displayViews(from: sharedHandle.views)
            try selectRequestedView()
            executeActiveView(session: session)
        } catch let error as BaseEmbedDocumentError {
            fail(error.localizedDescription)
        } catch {
            fail(friendlyMessage(for: error))
        }
    }

    func selectView(index: Int, session: VaultSession) {
        guard views.indices.contains(index), activeViewIndex != index else { return }
        guard let handle = sharedHandle.handle else { return }
        contentRefreshGeneration &+= 1
        do {
            try session.baseSetTransientSort(
                handle: handle,
                view: UInt32(activeViewIndex),
                columnId: nil,
                ascending: true)
        } catch {
            fail(friendlyMessage(for: error))
            return
        }
        activeViewIndex = index
        sortSelection = nil
        clearQuickFilterState()
        executeActiveView(session: session)
    }

    func executeActiveView(session: VaultSession) {
        contentRefreshGeneration &+= 1
        // A quarantined file-backed embed keeps its last truthful result while
        // the shared native handle is detached. A raced interaction must be a
        // no-op, not erase the snapshot with a false execution failure.
        guard let handle = sharedHandle.handle else { return }
        guard views.indices.contains(activeViewIndex) else {
            fail("No executable base views were found.")
            return
        }
        do {
            try session.baseSetTransientSort(
                handle: handle,
                view: UInt32(activeViewIndex),
                columnId: sortSelection?.columnID,
                ascending: sortSelection?.ascending ?? true)
            let appliedFilter = quickFilterArgument
            let executed = try session.baseExecute(
                handle: handle,
                view: UInt32(activeViewIndex),
                thisPath: thisPath,
                quickFilter: quickFilterArgument,
                cancel: CancelToken())
            result = executed
            appliedQuickFilterText = appliedFilter
            if sortSelection?.sortState(in: executed) == nil {
                sortSelection = nil
            }
            let view = views[activeViewIndex]
            if view.status == .fallback {
                state = .degraded("Using fallback view for \(view.name).")
            } else if view.status == .error {
                state = .degraded("View \(view.name) has errors.")
            } else if let message = executed.viewError, !message.isEmpty {
                state = .degraded(message)
            } else {
                state = .ready
            }
        } catch {
            fail(friendlyMessage(for: error))
        }
    }

    /// Re-execute an indexed note/property write without disturbing this
    /// embed's view, quick filter, or transient sort state.
    func refreshAfterInAppWrite(session: VaultSession) {
        guard !needsInitialLoad, sharedHandle.session === session else { return }
        executeActiveView(session: session)
    }

    /// Adopt a handle that the owner registry reopened after its `.base`
    /// definition or resolved saved-query AST changed. UI state stays local to
    /// this embed and is restored by stable view/column identity.
    func refreshAfterSharedHandleReload(session: VaultSession) {
        guard !needsInitialLoad, sharedHandle.session === session else { return }
        let previousViewName = activeViewName
        let previousViewIndex = activeViewIndex
        let previousSortSelection = sortSelection

        views = request.displayViews(from: sharedHandle.views)
        if let requested = request.requestedViewName {
            guard let requestedIndex = views.firstIndex(where: {
                BaseExactIdentity.matches($0.name, requested)
            }) else {
                fail(
                    "Unknown base view \(requested). Available views: "
                        + "\(availableList(views.map(\.name))).")
                return
            }
            activeViewIndex = requestedIndex
        } else if let previousViewName,
            let matchingIndex = views.firstIndex(where: {
                BaseExactIdentity.matches($0.name, previousViewName)
            })
        {
            activeViewIndex = matchingIndex
        } else {
            activeViewIndex = views.isEmpty ? 0 : min(previousViewIndex, views.count - 1)
        }

        // Establish the reopened view first, then remap transient sort by its
        // stable column id before executing the sorted result.
        sortSelection = nil
        executeActiveView(session: session)
        if let previousSortSelection,
            let result,
            previousSortSelection.sortState(in: result) != nil
        {
            sortSelection = previousSortSelection
            executeActiveView(session: session)
        }
    }

    func failRefresh(_ error: Error) {
        fail(friendlyMessage(for: error))
    }

    func invalidateMovedToTrash() {
        contentRefreshGeneration &+= 1
        let name = ((request.targetPath ?? "Base") as NSString).lastPathComponent
        fail("\(name) was moved to Trash and is no longer available.")
    }

    func acquireRefreshLease() {
        sharedHandle.register(self)
    }

    func releaseRefreshLease() {
        sharedHandle.unregister(self)
    }

    @discardableResult
    func applyQuickFilter(_ text: String, session: VaultSession) -> String {
        guard sharedHandle.handle != nil else {
            return quickFilterResultAnnouncement
        }
        if quickFilterText != text {
            quickFilterText = text
        }
        executeActiveView(session: session)
        return quickFilterResultAnnouncement
    }

    func setTransientSort(_ newSort: DataGridSortState?, session: VaultSession) {
        guard sharedHandle.handle != nil else { return }
        guard let result else { return }
        sortSelection = newSort.flatMap {
            BaseGridSortSelection(sortState: $0, result: result)
        }
        executeActiveView(session: session)
    }

    @discardableResult
    func clearQuickFilter(session: VaultSession?) -> String? {
        guard quickFilterActive else { return nil }
        guard sharedHandle.handle != nil else { return nil }
        clearQuickFilterState()
        guard let session else { return "Quick filter cleared." }
        executeActiveView(session: session)
        return quickFilterResultAnnouncement
    }

    func close(session: VaultSession) {
        contentRefreshGeneration &+= 1
        sharedHandle.close(session: session)
    }

    func beginContentRefresh() -> BaseEmbedDocumentRefreshSnapshot {
        contentRefreshGeneration &+= 1
        return BaseEmbedDocumentRefreshSnapshot(
            id: ObjectIdentifier(self),
            generation: contentRefreshGeneration,
            previousViewName: activeViewName,
            previousViewIndex: activeViewIndex,
            quickFilter: editedQuickFilterArgument ?? appliedQuickFilterText,
            sortSelection: sortSelection)
    }

    func ownsContentRefresh(_ snapshot: BaseEmbedDocumentRefreshSnapshot) -> Bool {
        contentRefreshGeneration == snapshot.generation
            && ObjectIdentifier(self) == snapshot.id
            && activeViewIndex == snapshot.previousViewIndex
            && activeViewName == snapshot.previousViewName
            && (editedQuickFilterArgument ?? appliedQuickFilterText) == snapshot.quickFilter
            && sortSelection == snapshot.sortSelection
    }

    func applyContentRefresh(
        _ prepared: BaseEmbedPreparedDocument,
        snapshot: BaseEmbedDocumentRefreshSnapshot
    ) {
        guard ownsContentRefresh(snapshot) else { return }
        views = prepared.views
        activeViewIndex = prepared.activeViewIndex
        if let message = prepared.failure {
            state = .degraded(message)
        } else {
            result = prepared.result
            appliedQuickFilterText = prepared.appliedQuickFilter
            sortSelection = prepared.sortSelection
            if let view = views.indices.contains(activeViewIndex)
                ? views[activeViewIndex] : nil,
                view.status == .fallback
            {
                state = .degraded("Using fallback view for \(view.name).")
            } else if let view = views.indices.contains(activeViewIndex)
                ? views[activeViewIndex] : nil,
                view.status == .error
            {
                state = .degraded("View \(view.name) has errors.")
            } else if let message = prepared.result?.viewError, !message.isEmpty {
                state = .degraded(message)
            } else {
                state = .ready
            }
        }
        contentRefreshGeneration &+= 1
    }

    func applyContentRefreshFailure(
        _ message: String,
        snapshot: BaseEmbedDocumentRefreshSnapshot
    ) {
        guard ownsContentRefresh(snapshot) else { return }
        state = .degraded(message)
        contentRefreshGeneration &+= 1
    }

    private var quickFilterArgument: String? {
        editedQuickFilterArgument
    }

    private var editedQuickFilterArgument: String? {
        guard !quickFilterText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return nil
        }
        return quickFilterText
    }

    private func clearQuickFilterState() {
        if !quickFilterText.isEmpty {
            quickFilterText = ""
        }
        appliedQuickFilterText = nil
    }

    private func selectRequestedView() throws {
        guard let requested = request.requestedViewName else { return }
        guard let index = views.firstIndex(where: {
            BaseExactIdentity.matches($0.name, requested)
        }) else {
            throw BaseEmbedDocumentError.message(
                "Unknown base view \(requested). Available views: \(availableList(views.map(\.name))).")
        }
        activeViewIndex = index
    }

    private func fail(_ message: String) {
        result = nil
        state = .failed(message)
    }

    private func friendlyMessage(for error: Error) -> String {
        if let localized = error as? LocalizedError,
            let description = localized.errorDescription
        {
            return description
        }
        return error.localizedDescription
    }
}
