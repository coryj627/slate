// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

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
    let path: String

    enum LoadState: Equatable {
        case loading
        /// Loaded and navigable (possibly with entry-level warnings).
        case ready
        /// The file could not be loaded as a canvas at all (t0 §5
        /// error state). Read-only; the message names the failure.
        case degraded(String)
        /// Filesystem/session failure (missing file, IO, UTF-8…).
        case failed(String)
    }

    @Published private(set) var state: LoadState = .loading
    /// Depth-first outline rows in reading order (the structured
    /// equivalent every surface starts from).
    @Published private(set) var outline: [CanvasOutlineRow] = []
    /// Entry-level load warnings (skipped entries, dangling
    /// connections…) — phrased by the t0 §5 banner.
    @Published private(set) var warnings: [CanvasLoadWarning] = []

    /// Shared selection + marks for every pane showing this canvas.
    let selection = CanvasSelection()

    /// FFI handle; valid while non-nil. Node ids are unique per file,
    /// so every canvas call routes through this.
    private(set) var handle: UInt64?

    init(path: String) {
        self.path = path
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
            state = .ready
        } catch {
            handle = nil
            outline = []
            state = .failed(friendlyMessage(for: error))
        }
    }

    /// Release the FFI handle (idempotent).
    func close(session: VaultSession) {
        if let handle {
            session.closeCanvas(handle: handle)
        }
        handle = nil
    }

    private func friendlyMessage(for error: Error) -> String {
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

    /// Filename without extension — never a raw path in UI copy.
    var displayName: String {
        let name = (path as NSString).lastPathComponent
        return (name as NSString).deletingPathExtension
    }
}
