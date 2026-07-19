// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit

/// File ▸ Print… (#869). Prints the CURRENT note's rendered reading content
/// via `NSPrintOperation` — which also hands the user Save-as-PDF for free.
///
/// The reading render is authored NOWHERE else at print time: we re-segment
/// the loaded note through the pure block source and style it into one
/// `NSAttributedString` (`ReadingPrintComposer`), then run the print panel on
/// a print-configured `NSTextView`. Printing happens regardless of the tab's
/// current mode — reading OR editing — because we always source
/// `currentNoteText` (the loaded note) and compose its rendered reading form.
extension AppState {

    /// Print the loaded note. Never inert: with no note open it announces a
    /// nudge (mirroring the other never-silent guards), so a keyboard /
    /// VoiceOver user who fires ⌘P on the welcome screen or an empty tab gets
    /// feedback instead of a dead keystroke. The menu item's `.disabled`
    /// gate (`loadedFilePath == nil`) is the primary affordance; this guard is
    /// the belt-and-braces path for the palette row and the raw chord.
    func printCurrentNote() {
        guard let text = currentNoteText, let path = loadedFilePath else {
            announcer.post(.printNeedsNote)
            return
        }
        let name = (path as NSString).lastPathComponent

        // Pure composition first (issue hard-constraint #1: the builder is a
        // standalone static function, unit-tested without ever presenting a
        // panel). Math / diagram degrade gracefully inside the composer.
        let document = ReadingPrintComposer.attributedString(
            text: text, citations: currentNoteCitations)

        // Resolve the stable DOCUMENT window to sheet onto. `printTargetWindow`
        // climbs out of any transient sheet (the command palette, which
        // dismisses right after invoking a command — Codex round 1) to its
        // parent. No window (XCTest / inactive app) → no-op, and crucially NO
        // announcement of a print that never opened a dialog (#869 red-team).
        guard let window = ReadingPrintComposer.printTargetWindow() else { return }

        // Defer one runloop turn so a presenting sheet (the palette) is gone
        // before we sheet the print panel onto the document window, and
        // announce only AFTER we actually kick off presentation — the sheet is
        // async and the user may still Cancel, so "Printing X." would falsely
        // tell a VoiceOver user the document already printed.
        DispatchQueue.main.async { [announcer] in
            ReadingPrintComposer.present(document, jobName: name, on: window)
            announcer.post(.printDialogOpened(name: name))
        }
    }
}
