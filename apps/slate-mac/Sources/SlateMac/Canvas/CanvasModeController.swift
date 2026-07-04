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
/// - **M4** focus departure (tab switch, palette, pane-focus chord)
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
@MainActor
final class CanvasModeController: ObservableObject {
    /// One modal interaction (move / resize / connect …).
    struct ModeSpec {
        /// Mode name for announcements and the AX value ("Move mode").
        let name: String
        /// The object being acted on (display title).
        let object: String
        /// The exit instructions appended to the entry announcement,
        /// e.g. "Arrows to move, Return to place, Escape to cancel."
        let exits: String
        /// Commit side effect; returns the confirmation announcement
        /// (t0 §1.3), or nil to stay silent (the action announces).
        let onCommit: () -> String?
        /// Cancel side effect (restore prior state); returns the
        /// restoration announcement, e.g. "Move cancelled — card returned."
        let onCancel: () -> String
    }

    @Published private(set) var active: ModeSpec?

    /// Extra Esc rungs between mode and surface (M5). #373 registers
    /// the filter rung: return true when the rung consumed the press.
    var escapeRungs: [() -> Bool] = []

    private let announce: (CanvasEvent) -> Void

    init(announce: @escaping (CanvasEvent) -> Void) {
        self.announce = announce
    }

    /// The M3 inspectable state for the canvas container's AX value.
    var containerAXValue: String? {
        active.map { "\($0.name): \($0.object)" }
    }

    /// M1 + M7. Returns false when rejected because a mode is active.
    @discardableResult
    func enter(_ spec: ModeSpec) -> Bool {
        if let current = active {
            announce(
                .error("\(current.name) is active. Return to commit or Escape to cancel first."))
            return false
        }
        active = spec
        announce(.mode("\(spec.name) — \(spec.object). \(spec.exits)"))
        return true
    }

    /// M2 commit (Return or a visible control).
    @discardableResult
    func commit() -> Bool {
        guard let spec = active else { return false }
        active = nil
        if let confirmation = spec.onCommit() {
            announce(.confirmation(confirmation))
        }
        return true
    }

    /// M2 cancel (Esc rung 1 or a visible control).
    @discardableResult
    func cancel() -> Bool {
        guard let spec = active else { return false }
        active = nil
        announce(.mode(spec.onCancel()))
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
