// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

enum BaseEmbedKind: Equatable, Hashable {
    case file
    case inlineBase
    case savedQuery
    case dataview
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
    }

    private let source: Source

    var kind: BaseEmbedKind {
        switch source {
        case .file: return .file
        case .inlineBase: return .inlineBase
        case .savedQuery: return .savedQuery
        case .dataview: return .dataview
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
        case .inlineBase, .dataview:
            return nil
        }
    }

    var inlineSource: String {
        switch source {
        case .inlineBase(let source), .dataview(let source):
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
        }
        return label
    }

    var cacheKey: String {
        switch source {
        case .file(let path, let viewName):
            return "file:\(path)#\(viewName ?? "")"
        case .inlineBase(let source):
            return "inline:\(source)"
        case .savedQuery(let reference, let viewName):
            return "saved:\(reference)#\(viewName ?? "")"
        case .dataview(let source):
            return "dql:\(source)"
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
        let body = ReadingBlockSource.fenceInterior(source)
            .trimmingCharacters(in: .whitespacesAndNewlines)
        switch tag {
        case "base":
            return BaseEmbedRequest(source: .inlineBase(source: body))
        case "slate-query":
            let fields = topLevelFields(in: body)
            if let query = fields["query"], !query.isEmpty {
                return BaseEmbedRequest(
                    source: .savedQuery(reference: query, viewName: fields["view"]))
            }
            return BaseEmbedRequest(source: .inlineBase(source: body))
        case "dataview":
            return BaseEmbedRequest(source: .dataview(source: body))
        default:
            return nil
        }
    }

    static func requests(in text: String) -> [BaseEmbedRequest] {
        previews(in: text).map(\.request)
    }

    fileprivate func openHandle(session: VaultSession, thisPath: String?) throws -> UInt64 {
        switch source {
        case .file(let path, _):
            return try session.openBase(path: path)
        case .inlineBase(let source):
            return try session.openBaseInline(source: source, thisPath: thisPath)
        case .savedQuery(let reference, let viewName):
            let query = try Self.savedQueryID(reference: reference, session: session)
            let saved = try session.getSavedQuery(id: query)
            let queryJSON = try Self.savedQueryJSON(
                fromEnvelope: saved.queryJson, viewOverride: viewName)
            return try session.openQuery(queryJson: queryJSON, thisPath: thisPath)
        case .dataview(let source):
            return try session.openDql(source: source, thisPath: thisPath)
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

    fileprivate static func savedQueryID(reference: String, session: VaultSession) throws -> String {
        let summaries = try session.listSavedQueries()
        if let match = summaries.first(where: { $0.id == reference || $0.name == reference }) {
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

    private static func topLevelFields(in body: String) -> [String: String] {
        var fields: [String: String] = [:]
        for rawLine in body.split(separator: "\n", omittingEmptySubsequences: false) {
            let line = String(rawLine)
            guard line.first?.isWhitespace != true,
                let colon = line.firstIndex(of: ":")
            else { continue }
            let key = line[..<colon].trimmingCharacters(in: .whitespacesAndNewlines)
                .lowercased()
            let rawValue = line[line.index(after: colon)...]
                .trimmingCharacters(in: .whitespacesAndNewlines)
            guard key == "query" || key == "view" else { continue }
            fields[key] = unquote(rawValue)
        }
        return fields
    }

    private static func unquote(_ value: String) -> String {
        guard value.count >= 2 else { return value }
        if (value.hasPrefix("\"") && value.hasSuffix("\""))
            || (value.hasPrefix("'") && value.hasSuffix("'"))
        {
            return String(value.dropFirst().dropLast())
        }
        return value
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

struct BaseEmbedCacheKey: Equatable, Hashable {
    let request: BaseEmbedRequest
    let thisPath: String?
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
            case .codeFence(let language):
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

    init(request: BaseEmbedRequest, thisPath: String?) {
        self.request = request
        self.thisPath = thisPath
    }

    func loadIfNeeded(session: VaultSession) throws {
        guard handle == nil else { return }
        do {
            let opened = try request.openHandle(session: session, thisPath: thisPath)
            handle = opened
            views = try session.baseViews(handle: opened)
        } catch {
            close(session: session)
            throw error
        }
    }

    func close(session: VaultSession) {
        if let handle {
            session.closeBase(handle: handle)
        }
        handle = nil
        views = []
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

    static let readOnlyHint = "read-only in embeds — open in tab to edit"

    let request: BaseEmbedRequest
    let thisPath: String?
    private let sharedHandle: BaseEmbedHandle

    @Published private(set) var state: LoadState = .idle
    @Published private(set) var views: [BaseViewSummary] = []
    @Published private(set) var result: BasesResultSet?
    @Published private(set) var activeViewIndex: Int = 0
    @Published var quickFilterText = ""
    @Published var sortState: DataGridSortState?

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
    }

    var activeViewName: String? {
        guard views.indices.contains(activeViewIndex) else { return nil }
        return views[activeViewIndex].name
    }

    var cellEditingAccessibilityHint: String { Self.readOnlyHint }

    var quickFilterActive: Bool {
        !quickFilterText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    var quickFilterResultAnnouncement: String {
        guard let result else { return "0 of 0 results" }
        return "\(result.shownCount) of \(result.totalCount) results"
    }

    func load(session: VaultSession) {
        state = .loading
        result = nil
        views = []
        activeViewIndex = 0
        sortState = nil
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
        activeViewIndex = index
        sortState = nil
        executeActiveView(session: session)
    }

    func executeActiveView(session: VaultSession) {
        guard let handle = sharedHandle.handle, views.indices.contains(activeViewIndex) else {
            fail("No executable base views were found.")
            return
        }
        do {
            let executed = try session.baseExecute(
                handle: handle,
                view: UInt32(activeViewIndex),
                thisPath: thisPath,
                quickFilter: quickFilterArgument,
                cancel: CancelToken())
            result = executed
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

    @discardableResult
    func applyQuickFilter(_ text: String, session: VaultSession) -> String {
        if quickFilterText != text {
            quickFilterText = text
        }
        executeActiveView(session: session)
        return quickFilterResultAnnouncement
    }

    func close(session: VaultSession) {
        sharedHandle.close(session: session)
    }

    private var quickFilterArgument: String? {
        quickFilterActive ? quickFilterText : nil
    }

    private func selectRequestedView() throws {
        guard let requested = request.requestedViewName else { return }
        guard let index = views.firstIndex(where: { $0.name == requested }) else {
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
