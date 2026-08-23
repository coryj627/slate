// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// The canvas mode stack (t0 §2, shipped with #364 as shared
/// infrastructure — #521 move/resize and #523 connect consume it).
///
/// Contract, M1–M7:
/// - **M1** entry announces the mode, the object, and the exits.
/// - **M2** Return commits (confirmation announced by the mode's
///   `onCommit`); Esc cancels, restores prior state, and announces the
///   restoration text.
/// - **M3** while active, the canvas container's `accessibilityValue`
///   carries "⟨Mode⟩: ⟨card⟩" — state is inspectable (braille rule
///   t0 §3), never merely announced.
/// - **M4** focus departure = tab switch / pane move. DELIBERATELY not
///   the palette: resize presets and Commit/Cancel Mode are palette
///   commands, so opening it must keep the mode alive. Out-of-band
///   mutations mid-mode are refused at `canvasApply`/undo/redo instead
///   (red-team #521).
///   auto-cancels with restoration + announcement. No mode survives
///   without focus; no keyboard trap (WCAG 2.1.2).
/// - **M5** the Esc ladder consumes exactly one rung per press:
///   active mode → active filter (#373 registers a rung later) →
///   surface → workspace. `handleEscape()` returns whether it consumed.
/// - **M6** visible controls: modes are entered/committed/cancelled via
///   on-screen controls too — the controller exposes plain methods so
///   toolbars/context menus bind directly (Switch Control never
///   depends on the keyboard path).
/// - **M7** entering a mode while one is active is rejected with an
///   announcement naming the active mode; nothing commits.
///
/// Since W6-1 PR 0a the SENTENCES are core's: a spec carries the typed
/// `CanvasMode` and `CanvasModeObject`, and the mode name, the exit
/// instructions and every lifecycle phrasing come from
/// `slate_core::a11y`. This controller owns the stack, not the copy.
@MainActor
final class CanvasModeController: ObservableObject {
    /// One modal interaction (move / resize / connect …).
    struct ModeSpec {
        /// The mode. Core owns its spoken name and its exit
        /// instructions (t0 §2 M1).
        let mode: CanvasMode
        /// What the mode acts on — one titled card or a count.
        let object: CanvasModeObject
        /// Commit side effect; returns the confirmation EVENT
        /// (t0 §1.3), or nil to stay silent (the action announces).
        let onCommit: () -> CanvasA11yEvent?
        /// Cancel side effect (restore prior state); returns what was
        /// put back, which core phrases (`… — card returned.`).
        let onCancel: () -> CanvasModeRestoration
    }

    @Published private(set) var active: ModeSpec?

    /// Extra Esc rungs between mode and surface (M5). #373 registers
    /// the filter rung: return true when the rung consumed the press.
    var escapeRungs: [() -> Bool] = []

    private let announce: (CanvasA11yEvent) -> Void

    init(announce: @escaping (CanvasA11yEvent) -> Void) {
        self.announce = announce
    }

    /// The M3 inspectable state for the canvas container's AX value.
    ///
    /// This is §W-C LABEL class, never spoken (contracts doc 0a-13:
    /// the vocabulary carries announcements plus exactly two
    /// label-grade events, and the M3 value is neither), so it is
    /// composed here from the SAME typed fields the spoken entry
    /// carries. It is the one host-side spelling of the mode names
    /// that survives PR 0a; a label-grade accessor on PR 0b's query
    /// surface is where it would collapse.
    var containerAXValue: String? {
        active.map { "\(Self.label(of: $0.mode)): \(Self.label(of: $0.object))" }
    }

    private static func label(of mode: CanvasMode) -> String {
        switch mode {
        case .move: return "Move mode"
        case .resize: return "Resize mode"
        case .connect: return "Connect mode"
        }
    }

    private static func label(of object: CanvasModeObject) -> String {
        switch object {
        case .card(let title): return "\"\(title)\""
        case .cards(let count): return "\(count) cards"
        }
    }

    /// M1 + M7. Returns false when rejected because a mode is active.
    @discardableResult
    func enter(_ spec: ModeSpec) -> Bool {
        if let current = active {
            announce(.canvasModeRejected(activeMode: current.mode))
            return false
        }
        active = spec
        announce(.canvasModeEntered(mode: spec.mode, object: spec.object))
        return true
    }

    /// M2 commit (Return or a visible control).
    @discardableResult
    func commit() -> Bool {
        guard let spec = active else { return false }
        active = nil
        if let confirmation = spec.onCommit() {
            announce(confirmation)
        }
        return true
    }

    /// M2 cancel (Esc rung 1 or a visible control).
    @discardableResult
    func cancel() -> Bool {
        guard let spec = active else { return false }
        active = nil
        announce(
            .canvasModeCancelled(mode: spec.mode, restoration: spec.onCancel()))
        return true
    }

    /// M4: any focus departure cancels outright.
    func handleFocusDeparture() {
        _ = cancel()
    }

    /// M5: one Esc press, one rung. Returns whether the press was
    /// consumed (false → the caller lets Esc bubble to the surface /
    /// workspace rungs).
    func handleEscape() -> Bool {
        if cancel() { return true }
        for rung in escapeRungs where rung() {
            return true
        }
        return false
    }
}
