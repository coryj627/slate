// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

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
}

struct BasesDockState: Equatable {
    var target: BasesDockTarget?
    var thisPath: String?
    var lastMembershipSignature: [String] = []
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

    func close(session: VaultSession) {
        for section in sections {
            section.close(session: session)
        }
    }

    func retargetName(_ newName: String) {
        name = newName
    }

    var membershipSignature: [String] {
        sections.flatMap(\.membershipSignature)
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

    func load(session: VaultSession, thisPath: String? = nil) {
        close(session: session)
        result = nil
        guard !status.missing else {
            state = .missing
            return
        }
        state = .loading
        do {
            let opened = try session.openSavedQuery(id: status.savedQueryId)
            handle = opened
            activeViewIndex = viewIndex(session: session, handle: opened)
            execute(session: session, thisPath: thisPath)
        } catch {
            handle = nil
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

    func close(session: VaultSession) {
        if let handle {
            session.closeBase(handle: handle)
        }
        handle = nil
    }

    var membershipSignature: [String] {
        result?.rows.map { row in
            row.taskOrdinal.map { "\(row.filePath)#\($0)" } ?? row.filePath
        } ?? []
    }

    private func viewIndex(session: VaultSession, handle: UInt64) -> Int {
        guard let override = status.viewOverride?.trimmingCharacters(in: .whitespacesAndNewlines),
            !override.isEmpty,
            let views = try? session.baseViews(handle: handle),
            let index = views.firstIndex(where: {
                $0.name.localizedCaseInsensitiveCompare(override) == .orderedSame
            })
        else { return 0 }
        return index
    }

    private func execute(session: VaultSession, thisPath: String? = nil) {
        guard let handle else {
            result = nil
            state = .failed("Section has no open query.")
            return
        }
        do {
            result = try session.baseExecute(
                handle: handle,
                view: UInt32(activeViewIndex),
                thisPath: thisPath,
                quickFilter: nil,
                cancel: CancelToken())
            state = .ready
        } catch {
            result = nil
            state = .failed("Section could not run: \(error.localizedDescription)")
        }
    }
}
