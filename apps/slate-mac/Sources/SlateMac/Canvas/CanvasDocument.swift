// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

enum CanvasNewFileNativePhase: Sendable, Equatable {
    case create
    case activationLoad
    case open
    case outline
    case table
    case scene
    case closePrepared
    case closeReplaced
}

struct CanvasNewFileNativeExecutionEvent: Sendable, Equatable {
    let phase: CanvasNewFileNativePhase
    let ranOnMainThread: Bool
}

typealias CanvasNewFileNativeExecutionObserver =
    @Sendable (CanvasNewFileNativeExecutionEvent) -> Void

enum CanvasNewFileThreadProbe {
    nonisolated static func isMainThread() -> Bool { Thread.isMainThread }
}

/// Immutable result of opening a newly-created canvas away from the main
/// actor. A ready snapshot owns its native handle until it is either installed
/// into the per-path `CanvasDocument` or explicitly released.
enum CanvasPreparedLoad: @unchecked Sendable {
    case ready(
        handle: UInt64,
        warnings: [CanvasLoadWarning],
        outline: [CanvasOutlineRow],
        tableRows: [CanvasTableRow],
        scene: CanvasScene
    )
    case degraded(warnings: [CanvasLoadWarning], message: String)
    case failed(String)

    var retainedHandle: UInt64? {
        guard case .ready(let handle, _, _, _, _) = self else { return nil }
        return handle
    }
}

typealias CanvasNewFilePreloadRunner =
    @Sendable (
        VaultSession,
        String,
        CanvasNewFileNativeExecutionObserver?
    ) -> CanvasPreparedLoad

/// Main-actor-only ownership token for one physical-path retarget. The old
/// native handle is detached synchronously so no edit can reach the moved-away
/// path; its close and any replacement preparation happen off-main.
struct CanvasRetargetReservation: Sendable {
    let generation: UInt64
    let replacedHandle: UInt64?
}

/// Native preparation and cleanup for New Canvas. Every method is
/// `nonisolated` so callers can keep the entire native boundary off-main.
enum CanvasPreparedLoader {
    nonisolated static func prepare(
        session: VaultSession,
        path: String,
        observer: CanvasNewFileNativeExecutionObserver?
    ) -> CanvasPreparedLoad {
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
            let info = try session.openCanvas(path: path)
            openedHandle = info.handle
            if info.degraded {
                let detail =
                    info.warnings.first { $0.kind == .parseFailed }?.detail
                    ?? "the file is not valid JSON Canvas"
                return .degraded(warnings: info.warnings, message: detail)
            }

            observe(.outline, observer: observer)
            let outline = try session.canvasOutline(handle: info.handle)
            observe(.table, observer: observer)
            let tableRows = try session.canvasTableRows(handle: info.handle)
            observe(.scene, observer: observer)
            let scene = try session.canvasScene(handle: info.handle)
            transferredHandle = true
            return .ready(
                handle: info.handle,
                warnings: info.warnings,
                outline: outline,
                tableRows: tableRows,
                scene: scene)
        } catch {
            return .failed(CanvasDocument.friendlyMessage(path: path, for: error))
        }
    }

    nonisolated static func release(
        _ prepared: CanvasPreparedLoad,
        session: VaultSession,
        observer: CanvasNewFileNativeExecutionObserver?
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
        observer: CanvasNewFileNativeExecutionObserver?
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
        phase: CanvasNewFileNativePhase,
        observer: CanvasNewFileNativeExecutionObserver?
    ) {
        observe(phase, observer: observer)
        session.closeCanvas(handle: handle)
    }

    private nonisolated static func observe(
        _ phase: CanvasNewFileNativePhase,
        observer: CanvasNewFileNativeExecutionObserver?
    ) {
        observer?(
            CanvasNewFileNativeExecutionEvent(
                phase: phase,
                ranOnMainThread: CanvasNewFileThreadProbe.isMainThread()))
    }
}

/// Per-open-canvas state (t2 shared architecture, #369): loads the
/// model over the FFI and owns the handle, load state, and the shared
/// [`CanvasSelection`]. One `CanvasDocument` per open path (U1
/// `NoteDocument` registry pattern) — every pane/tab showing that
/// canvas shares it, so selection and marks stay in sync across panes.
///
/// Canvas tabs are never "dirty": mutations write through on commit
/// (each committed action serializes + saves via the backend
/// `canvas_apply` pipeline), so the U1 close gate is bypassed for
/// canvas tabs (#369 decision 4; conflicts surface per t0 §5).
@MainActor
final class CanvasDocument: ObservableObject {
    /// Vault-relative path of the `.canvas` file.
    private(set) var path: String

    enum LoadState: Equatable {
        case loading
        /// Loaded and navigable (possibly with entry-level warnings).
        case ready
        /// The file could not be loaded as a canvas at all (t0 §5
        /// error state). Read-only; the message names the failure.
        case degraded(String)
        /// Filesystem/session failure (missing file, IO, UTF-8…).
        case failed(String)
        /// A moved live canvas could not reopen at its new path. The last
        /// published snapshot remains visible but has no writable native handle;
        /// retrying the same retarget generation can restore it.
        case retargetFailed(String)
    }

    @Published private(set) var state: LoadState = .loading
    /// Depth-first outline rows in reading order (the structured
    /// equivalent every surface starts from).
    @Published private(set) var outline: [CanvasOutlineRow] = []
    /// Entry-level load warnings (skipped entries, dangling
    /// connections…) — phrased by the t0 §5 banner.
    @Published private(set) var warnings: [CanvasLoadWarning] = []

    /// Flat table rows (Type/Title/Group/Target/Connections/Color) —
    /// the #363 surface reads these; fetched once at load.
    @Published private(set) var tableRows: [CanvasTableRow] = []

    /// Per-node activation targets (file path / URL) derived from the
    /// table rows — activation never re-queries.
    private var targets: [String: String] = [:]

    /// Per-node adjacency, fetched lazily on first selection and
    /// cached (invalidated on reload).
    private var neighborsCache: [String: [CanvasNeighbor]] = [:]

    /// The row whose activation opened a card — focus restoration
    /// target when the user returns (WCAG 2.4.3, #362).
    var lastActivatedNode: String?

    /// Session-scoped undo/redo stacks (#372, t3 layer 2): named
    /// inverse actions returned by canvas_apply. Deliberately NOT
    /// persisted — undo does not survive app restart (the NSUndoManager
    /// contract notes follow); the op-log journal remains the durable
    /// audit record.
    var undoStack: [(name: String, inverse: CanvasAction)] = []
    var redoStack: [(name: String, inverse: CanvasAction)] = []

    /// Shared selection + marks for every pane showing this canvas.
    let selection = CanvasSelection()

    /// Zoom/pan state for the visual surface (#367/#520), shared by
    /// panes showing this canvas.
    let viewport = CanvasViewport()

    /// Render scene for the visual surface (geometry in document
    /// order + edges).
    @Published private(set) var scene = CanvasScene(nodes: [], edges: [])

    /// In-canvas filter (#373): a VIEW over the outline/table — never
    /// a mutation; filtered-out cards stay in the file. Esc clears it
    /// (the t0 M5 ladder rung between mode and surface).
    @Published var filterText: String = ""

    /// True when a non-empty filter narrows the surfaces.
    var filterActive: Bool {
        !filterText.trimmingCharacters(in: .whitespaces).isEmpty
    }

    /// Rows matching the filter (title / kind / group / target — the
    /// quick-open-style contains match). Full outline when inactive.
    var filteredOutline: [CanvasOutlineRow] {
        guard filterActive else { return outline }
        return outline.filter(matchesFilter)
    }

    func matchesFilter(_ row: CanvasOutlineRow) -> Bool {
        let needle = filterText.trimmingCharacters(in: .whitespaces)
        guard !needle.isEmpty else { return true }
        if row.title.localizedCaseInsensitiveContains(needle) { return true }
        if row.kind.localizedCaseInsensitiveContains(needle) { return true }
        if row.groupPath.contains(where: { $0.localizedCaseInsensitiveContains(needle) }) {
            return true
        }
        return target(of: row.nodeId).localizedCaseInsensitiveContains(needle)
    }

    /// Hypothetical geometry while a move/resize mode is active
    /// (#521): the renderer draws these instead of the scene rects;
    /// nil when no mode holds a transient.
    @Published var transientRects: [String: CanvasRect]?

    /// FFI handle; valid while non-nil. Node ids are unique per file,
    /// so every canvas call routes through this.
    private(set) var handle: UInt64?

    /// New Canvas can reserve an existing missing-file document while its tree
    /// refresh suspends. Activation must not fall back to synchronous native
    /// loading during that interval or immediately after a prepared failure.
    private var awaitingPreparedLoad = false
    private var preparedActivationPending = false

    /// A physical move keeps this Swift document's interaction identity but
    /// invalidates its path-bound native handle. Published content remains in
    /// place while a live document prepares off-main; parked documents keep
    /// this reservation until their next activation.
    private var retargetGeneration: UInt64 = 0
    @Published private var retargetPreparationPending = false
    @Published private var retargetPreparationInFlight = false

    var hasPreparedLoadReservation: Bool { awaitingPreparedLoad }
    var hasPendingRetargetPreparation: Bool { retargetPreparationPending }
    var isRetargetPreparationInFlight: Bool { retargetPreparationInFlight }

    init(path: String) {
        self.path = path
    }

    /// A physical rename/move rekeys this live Swift document in place while
    /// replacing the native handle. Core's open-canvas state owns the path it
    /// was opened with, so retaining that handle would make the next edit save
    /// through the moved-away path. Selection, viewport, mode ownership,
    /// filter, and undo stacks remain on this object across the reload.
    func retarget(to path: String, session: VaultSession?) {
        invalidateRetargetPreparation()
        if let session {
            close(session: session)
        } else {
            handle = nil
        }
        self.path = path
        if let session {
            load(session: session)
        }
    }

    /// Land a physical path change without performing native work. The old
    /// snapshot, selection, viewport, mode, filter, and undo stacks remain
    /// visible and stable until an immutable prepared replacement is ready.
    func beginBatchRetarget(to path: String) -> CanvasRetargetReservation {
        retargetGeneration &+= 1
        let reservation = CanvasRetargetReservation(
            generation: retargetGeneration,
            replacedHandle: handle)
        handle = nil
        self.path = path
        retargetPreparationPending = true
        retargetPreparationInFlight = false
        return reservation
    }

    /// Claims this generation's single preparation. Repeated activations or a
    /// split-pane duplicate cannot schedule a second native open.
    func claimRetargetPreparation() -> UInt64? {
        guard retargetPreparationPending, !retargetPreparationInFlight else {
            return nil
        }
        retargetPreparationInFlight = true
        return retargetGeneration
    }

    func ownsRetargetPreparation(generation: UInt64, path: String) -> Bool {
        retargetPreparationPending
            && retargetPreparationInFlight
            && retargetGeneration == generation
            && BaseExactIdentity.matches(self.path, path)
    }

    /// Publish a background-prepared replacement without resetting any
    /// interaction state. On a transient failure the old visible snapshot is
    /// retained and the reservation becomes retryable instead of flashing a
    /// blank/error pane.
    @discardableResult
    func applyRetargetPreparation(
        _ prepared: CanvasPreparedLoad,
        generation: UInt64,
        path: String
    ) -> Bool {
        guard ownsRetargetPreparation(generation: generation, path: path) else {
            return false
        }
        retargetPreparationInFlight = false

        switch prepared {
        case .ready(let preparedHandle, let preparedWarnings, let preparedOutline,
            let preparedTableRows, let preparedScene):
            handle = preparedHandle
            warnings = preparedWarnings
            outline = preparedOutline
            tableRows = preparedTableRows
            scene = preparedScene
            targets = Dictionary(
                uniqueKeysWithValues: preparedTableRows.map { ($0.nodeId, $0.target) })
            neighborsCache = [:]
            state = .ready
            retargetPreparationPending = false
        case .degraded(_, let message):
            // Keep the last good content visible and read-only. Activation can
            // retry; no stale path handle is restored.
            state = .retargetFailed(
                "\(displayName) could not be read as a canvas. \(message)")
            retargetPreparationPending = true
        case .failed(let message):
            state = .retargetFailed(message)
            retargetPreparationPending = true
        }
        return true
    }

    /// Number of skipped-but-preserved entries (t0 §5: "N unsupported
    /// items are preserved in the file but not shown").
    var preservedItemCount: Int {
        warnings.filter { $0.kind == .skippedEntry }.count
    }

    /// Open (or re-open) the canvas over the FFI. Synchronous: the
    /// full open path is ~5.6 ms at the 2,000-node §K budget, so a
    /// canvas opens within a keystroke's latency envelope.
    func load(session: VaultSession) {
        invalidateRetargetPreparation()
        awaitingPreparedLoad = false
        preparedActivationPending = false
        if let stale = handle {
            session.closeCanvas(handle: stale)
            handle = nil
        }
        do {
            let info = try session.openCanvas(path: path)
            warnings = info.warnings
            if info.degraded {
                // Degraded = read-only error state: nothing will use
                // the handle, so release it immediately rather than
                // retaining native resources until teardown (Codoki
                // #608).
                session.closeCanvas(handle: info.handle)
                handle = nil
                let detail =
                    info.warnings.first { $0.kind == .parseFailed }?.detail
                    ?? "the file is not valid JSON Canvas"
                state = .degraded(detail)
                outline = []
                return
            }
            handle = info.handle
            outline = try session.canvasOutline(handle: info.handle)
            tableRows = try session.canvasTableRows(handle: info.handle)
            scene = try session.canvasScene(handle: info.handle)
            targets = Dictionary(
                uniqueKeysWithValues: tableRows.map { ($0.nodeId, $0.target) })
            neighborsCache = [:]
            state = .ready
        } catch {
            handle = nil
            warnings = []
            outline = []
            tableRows = []
            scene = CanvasScene(nodes: [], edges: [])
            targets = [:]
            neighborsCache = [:]
            state = .failed(Self.friendlyMessage(path: path, for: error))
        }
    }

    /// Reserve this object for a prepared New Canvas result. The object stays
    /// stable for any already-open missing-file tab; a stale native handle is
    /// detached so it can be closed off-main by the creator.
    func beginPreparedReplacement() -> UInt64? {
        let replacedHandle = handle
        handle = nil
        awaitingPreparedLoad = true
        preparedActivationPending = false
        state = .loading
        return replacedHandle
    }

    /// Install a background-prepared snapshot without any native call. A
    /// physical file creation is a new document identity even when it reuses a
    /// missing-file Swift object, so every session-scoped interaction state is
    /// reset before the result becomes visible.
    func applyPreparedLoad(_ prepared: CanvasPreparedLoad) {
        awaitingPreparedLoad = false
        preparedActivationPending = true
        resetForNewDocumentIdentity()

        switch prepared {
        case .ready(let preparedHandle, let preparedWarnings, let preparedOutline,
            let preparedTableRows, let preparedScene):
            handle = preparedHandle
            warnings = preparedWarnings
            outline = preparedOutline
            tableRows = preparedTableRows
            scene = preparedScene
            targets = Dictionary(
                uniqueKeysWithValues: preparedTableRows.map { ($0.nodeId, $0.target) })
            state = .ready
        case .degraded(let preparedWarnings, let message):
            handle = nil
            warnings = preparedWarnings
            outline = []
            tableRows = []
            scene = CanvasScene(nodes: [], edges: [])
            targets = [:]
            state = .degraded(message)
        case .failed(let message):
            handle = nil
            warnings = []
            outline = []
            tableRows = []
            scene = CanvasScene(nodes: [], edges: [])
            targets = [:]
            state = .failed(message)
        }
        neighborsCache = [:]
    }

    /// Returns true while activation must trust the background-prepared state.
    /// The post-install skip is one-shot so a later revisit can retry a failed
    /// or degraded load through the normal lifecycle.
    func shouldSkipSynchronousActivationLoad() -> Bool {
        if retargetPreparationPending { return true }
        if awaitingPreparedLoad { return true }
        if preparedActivationPending {
            preparedActivationPending = false
            return true
        }
        return false
    }

    /// Same-tab ready activation takes the fast path before consulting the
    /// loading guard; consume the one-shot marker there as well.
    func consumePreparedActivationIfNeeded() {
        preparedActivationPending = false
    }

    /// Cancellation can abandon a reservation without installing its snapshot.
    /// Clear the guard so the normal activation funnel can retry from disk.
    func abandonPreparedReplacement() {
        awaitingPreparedLoad = false
        preparedActivationPending = false
        state = .failed("\(displayName) could not finish opening.")
    }

    private func resetForNewDocumentIdentity() {
        selection.selected = nil
        selection.marked = []
        lastActivatedNode = nil
        undoStack = []
        redoStack = []
        neighborsCache = [:]
        filterText = ""
        transientRects = nil
        viewport.scale = 1.0
        viewport.offset = .zero
        viewport.followSelection = true
    }

    /// Refresh outline/table/targets after a committed mutation —
    /// same fetches as load, but the handle (and stacks) survive.
    func reloadAfterMutation(session: VaultSession) {
        guard let handle else { return }
        outline = (try? session.canvasOutline(handle: handle)) ?? outline
        tableRows = (try? session.canvasTableRows(handle: handle)) ?? tableRows
        scene = (try? session.canvasScene(handle: handle)) ?? scene
        targets = Dictionary(
            uniqueKeysWithValues: tableRows.map { ($0.nodeId, $0.target) })
        neighborsCache = [:]
    }

    /// Release the FFI handle (idempotent).
    func close(session: VaultSession) {
        invalidateRetargetPreparation()
        if let handle {
            session.closeCanvas(handle: handle)
        }
        handle = nil
        awaitingPreparedLoad = false
        preparedActivationPending = false
    }

    /// Reconciliation proved that this file was physically moved to Trash.
    /// Keep the Swift document identity so an already-mounted tab or sheet
    /// cannot manufacture a fresh `.loading` document, but detach every native
    /// write capability and publish a terminal, truthful state.
    func markMovedToTrash(session: VaultSession?) {
        invalidateRetargetPreparation()
        if let handle, let session {
            session.closeCanvas(handle: handle)
        }
        handle = nil
        awaitingPreparedLoad = false
        preparedActivationPending = false
        state = .failed(
            "\(displayName) was moved to Trash and is no longer available.")
    }

    private func invalidateRetargetPreparation() {
        retargetGeneration &+= 1
        retargetPreparationPending = false
        retargetPreparationInFlight = false
    }

    nonisolated static func friendlyMessage(path: String, for error: Error) -> String {
        let name = (path as NSString).lastPathComponent
        let displayName = (name as NSString).deletingPathExtension
        if let vaultError = error as? VaultError {
            switch vaultError {
            case .Io: return "\(displayName) could not be read — it may have been moved or deleted."
            case .FileTooLarge:
                return "\(displayName) is too large to open."
            case .InvalidUtf8:
                return "\(displayName) is not valid UTF-8 text."
            default: break
            }
        }
        return "\(displayName) could not be opened: \(error.localizedDescription)"
    }

    /// Activation target for a node ("" when none).
    func target(of nodeId: String) -> String {
        targets[nodeId] ?? ""
    }

    /// Adjacency for one node (cached; empty on any failure — the
    /// outline degrades to no connection rows, never an error state).
    func neighbors(of nodeId: String, session: VaultSession?) -> [CanvasNeighbor] {
        if let cached = neighborsCache[nodeId] { return cached }
        guard let session, let handle else { return [] }
        let result = (try? session.canvasNeighbors(handle: handle, nodeId: nodeId)) ?? []
        neighborsCache[nodeId] = result
        return result
    }

    /// Filename without extension — never a raw path in UI copy.
    var displayName: String {
        let name = (path as NSString).lastPathComponent
        return (name as NSString).deletingPathExtension
    }
}
