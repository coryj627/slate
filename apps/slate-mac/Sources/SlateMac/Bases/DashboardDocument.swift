// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Order-independent, multiplicity-preserving membership for a Bases result.
/// A task row is distinct from its owning note (and from sibling tasks) by its
/// optional task ordinal. Keeping this reusable prevents tab, dashboard, and
/// dock refresh paths from drifting back to ordered-array or plain-set checks.
struct BaseRowMembership: Equatable {
    struct Identity: Hashable {
        let path: String
        let taskOrdinal: UInt64?

        static func == (lhs: Identity, rhs: Identity) -> Bool {
            BaseExactIdentity.matches(lhs.path, rhs.path)
                && lhs.taskOrdinal == rhs.taskOrdinal
        }

        func hash(into hasher: inout Hasher) {
            BaseExactIdentity.hash(path, into: &hasher)
            hasher.combine(taskOrdinal)
        }
    }

    static let empty = BaseRowMembership(rows: [])

    private(set) var counts: [Identity: Int]

    init(rows: [BasesRow]) {
        counts = rows.reduce(into: [:]) { counts, row in
            let identity = Identity(path: row.filePath, taskOrdinal: row.taskOrdinal)
            counts[identity, default: 0] += 1
        }
    }

    var isEmpty: Bool { counts.isEmpty }
}

enum BasesDockTarget: Equatable {
    case base(path: String, name: String)
    case savedQuery(id: String, name: String)
    case dashboard(id: String, name: String)

    var displayName: String {
        switch self {
        case .base(_, let name), .savedQuery(_, let name), .dashboard(_, let name):
            return name
        }
    }

    fileprivate enum StableIdentity: Equatable {
        case base(String)
        case savedQuery(String)
        case dashboard(String)

        static func == (lhs: StableIdentity, rhs: StableIdentity) -> Bool {
            switch (lhs, rhs) {
            case (.base(let lhs), .base(let rhs)),
                (.savedQuery(let lhs), .savedQuery(let rhs)),
                (.dashboard(let lhs), .dashboard(let rhs)):
                return BaseExactIdentity.matches(lhs, rhs)
            default:
                return false
            }
        }
    }

    fileprivate var stableIdentity: StableIdentity {
        switch self {
        case .base(let path, _): return .base(path)
        case .savedQuery(let id, _): return .savedQuery(id)
        case .dashboard(let id, _): return .dashboard(id)
        }
    }

    static func == (lhs: BasesDockTarget, rhs: BasesDockTarget) -> Bool {
        switch (lhs, rhs) {
        case (.base(let lhsPath, let lhsName), .base(let rhsPath, let rhsName)):
            return BaseExactIdentity.matches(lhsPath, rhsPath)
                && BaseExactIdentity.matches(lhsName, rhsName)
        case (.savedQuery(let lhsID, let lhsName), .savedQuery(let rhsID, let rhsName)),
            (.dashboard(let lhsID, let lhsName), .dashboard(let rhsID, let rhsName)):
            return BaseExactIdentity.matches(lhsID, rhsID)
                && BaseExactIdentity.matches(lhsName, rhsName)
        default:
            return false
        }
    }
}

struct BasesDockState: Equatable {
    var target: BasesDockTarget?
    var thisPath: String?
    var lastMembershipSignature: BaseRowMembership = .empty
    private(set) var hasPublishedBaseline = false

    mutating func setTarget(_ newTarget: BasesDockTarget?) {
        if target?.stableIdentity != newTarget?.stableIdentity {
            lastMembershipSignature = .empty
            hasPublishedBaseline = false
        }
        target = newTarget
    }

    /// Publish one settled follow-active result. The first publication is a
    /// baseline even when it is empty; only later multiset changes announce.
    mutating func publishMembership(_ membership: BaseRowMembership) -> Bool {
        let changed = hasPublishedBaseline && lastMembershipSignature != membership
        lastMembershipSignature = membership
        hasPublishedBaseline = true
        return changed
    }

    /// Adopt membership after an out-of-band reload (dashboard edit, saved-query
    /// mutation, in-app write) without attributing that change to note following.
    mutating func rebaseMembership(_ membership: BaseRowMembership) {
        lastMembershipSignature = membership
        hasPublishedBaseline = true
    }
}

@MainActor
final class DashboardDocument: ObservableObject {
    enum LoadState: Equatable {
        case loading
        case ready
        case failed(String)
    }

    let id: String
    @Published private(set) var name: String
    @Published private(set) var state: LoadState = .loading
    @Published private(set) var dashboard: Dashboard?
    @Published private(set) var sections: [DashboardSectionDocument] = []

    init(id: String, name: String) {
        self.id = id
        self.name = name
    }

    func load(session: VaultSession, thisPath: String? = nil) {
        close(session: session)
        state = .loading
        do {
            let loaded = try session.getDashboard(id: id)
            name = loaded.name
            dashboard = loaded
            sections = loaded.sections.enumerated().map { offset, status in
                DashboardSectionDocument(index: offset, status: status)
            }
            for section in sections {
                section.load(session: session, thisPath: thisPath)
            }
            state = .ready
        } catch {
            dashboard = nil
            sections = []
            state = .failed("Dashboard could not be opened: \(error.localizedDescription)")
        }
    }

    func refresh(session: VaultSession, thisPath: String? = nil) {
        if dashboard == nil {
            load(session: session, thisPath: thisPath)
            return
        }
        for section in sections {
            section.refresh(session: session, thisPath: thisPath)
        }
    }

    /// Reopen only sections that consume the changed saved-query identity.
    /// Other sections keep their handles and result state; a failed reopen is
    /// localized to the matching section's existing error surface.
    func reloadSavedQuery(id: String, session: VaultSession, thisPath: String? = nil) {
        for section in sections {
            section.reloadSavedQuery(id: id, session: session, thisPath: thisPath)
        }
    }

    /// Re-execute every live section and return membership announcements for
    /// sections whose counted row identities changed. Callers coalesce these
    /// strings across dashboard/tab/dock surfaces before posting them.
    func refreshAfterInAppWrite(session: VaultSession, thisPath: String? = nil) -> [String] {
        let previous = Dictionary(
            uniqueKeysWithValues: sections.map { ($0.id, $0.membership) })
        refresh(session: session, thisPath: thisPath)
        return sections.compactMap { section in
            guard (previous[section.id] ?? .empty) != section.membership,
                let summary = section.result?.audioSummary
            else { return nil }
            return "Updated: \(summary)"
        }
    }

    func close(session: VaultSession) {
        for section in sections {
            section.close(session: session)
        }
    }

    func retargetName(_ newName: String) {
        name = newName
    }

    var membershipSignature: BaseRowMembership {
        BaseRowMembership(rows: sections.flatMap { $0.result?.rows ?? [] })
    }

    /// Exact editable section order and overrides represented by this document.
    /// Missing-section actions send this snapshot back so duplicate query IDs
    /// cannot make a stale row callback target a different section after reorder.
    var editableSectionsSnapshot: [DashboardSection] {
        sections.map { section in
            DashboardSection(
                savedQueryId: section.status.savedQueryId,
                headingOverride: section.status.headingOverride,
                viewOverride: section.status.viewOverride)
        }
    }
}

@MainActor
final class DashboardSectionDocument: ObservableObject, Identifiable {
    enum LoadState: Equatable {
        case loading
        case ready
        case missing
        case failed(String)
    }

    let id: String
    let index: Int
    @Published private(set) var status: DashboardSectionStatus
    @Published private(set) var state: LoadState = .loading
    @Published private(set) var result: BasesResultSet?
    @Published private(set) var authoredRenderer: BaseRendererMode? = nil

    private var handle: UInt64?
    private var activeViewIndex = 0

    init(index: Int, status: DashboardSectionStatus) {
        self.index = index
        self.status = status
        self.id = "\(index)-\(status.savedQueryId)"
    }

    var title: String {
        if let heading = status.headingOverride?.trimmingCharacters(in: .whitespacesAndNewlines),
            !heading.isEmpty
        {
            return heading
        }
        if let name = status.savedQueryName?.trimmingCharacters(in: .whitespacesAndNewlines),
            !name.isEmpty
        {
            return name
        }
        return "Missing saved query"
    }

    var isMissing: Bool {
        status.missing
    }

    var rendererOverride: BaseRendererMode? {
        guard let value = normalizedViewOverride else { return nil }
        return BaseRendererMode(rawValue: value.lowercased())
    }

    var resolvedRenderer: BaseRendererMode? {
        rendererOverride ?? authoredRenderer
    }

    func load(session: VaultSession, thisPath: String? = nil) {
        close(session: session)
        result = nil
        authoredRenderer = nil
        guard !status.missing else {
            state = .missing
            return
        }
        state = .loading
        if let value = normalizedViewOverride,
            rendererOverride == nil
        {
            state = .failed(
                "Unsupported dashboard view override \"\(value)\". "
                    + "Choose Default, Table, or List.")
            return
        }
        do {
            let opened = try session.openSavedQuery(id: status.savedQueryId)
            handle = opened
            activeViewIndex = 0
            if let view = try session.baseViews(handle: opened).first {
                authoredRenderer = view.viewType == "list" ? .list : .table
            }
            execute(session: session, thisPath: thisPath)
        } catch {
            close(session: session)
            state = .failed("Section could not be opened: \(error.localizedDescription)")
        }
    }

    func refresh(session: VaultSession, thisPath: String? = nil) {
        guard !status.missing else {
            result = nil
            state = .missing
            return
        }
        if handle == nil {
            load(session: session, thisPath: thisPath)
        } else {
            execute(session: session, thisPath: thisPath)
        }
    }

    func reloadSavedQuery(id: String, session: VaultSession, thisPath: String? = nil) {
        guard status.savedQueryId == id else { return }
        load(session: session, thisPath: thisPath)
    }

    func close(session: VaultSession) {
        if let handle {
            session.closeBase(handle: handle)
        }
        handle = nil
    }

    var membership: BaseRowMembership {
        BaseRowMembership(rows: result?.rows ?? [])
    }

    private var normalizedViewOverride: String? {
        guard let value = status.viewOverride?.trimmingCharacters(in: .whitespacesAndNewlines),
            !value.isEmpty
        else { return nil }
        return value
    }

    private func execute(session: VaultSession, thisPath: String? = nil) {
        guard let handle else {
            result = nil
            state = .failed("Section has no open query.")
            return
        }
        do {
            let executed = try session.baseExecute(
                handle: handle,
                view: UInt32(activeViewIndex),
                thisPath: thisPath,
                quickFilter: nil,
                cancel: CancelToken())
            result = executed
            if let message = executed.viewError,
                !message.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            {
                state = .failed(message)
            } else {
                state = .ready
            }
        } catch {
            result = nil
            state = .failed("Section could not run: \(error.localizedDescription)")
        }
    }
}
