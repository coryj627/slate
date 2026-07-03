// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit

/// Shared APCA-W3 v0.1.9 contrast helper for the Mac test target.
///
/// Two consumers as of #313: `EditorSyntaxPaletteTests` and
/// `CommandPaletteViewTests`. Extracted from the original
/// `EditorSyntaxPaletteTests` private copy so the constants
/// (`normBG 0.56`, `normTXT 0.57`, `revTXT 0.62`, `revBG 0.65`,
/// soft-clamp `blkThrs 0.022 / blkClmp 1.414`) live in one place
/// — silently drifting copies would misreport contrast.
///
/// Reference: https://github.com/Myndex/apca-w3 (G-4g constants).
///
/// Project standard: `|Lc| > 75` (APCA's "small body text" bucket)
/// — see `feedback_contrast_apca.md` in the project memory.
enum APCAContrast {

    /// APCA `Lc` value: positive for dark text on light bg (BoW),
    /// negative for light text on dark bg (WoB). Compare
    /// `abs(lc)` against the project's `> 75` threshold for
    /// pass/fail.
    static func lc(text: NSColor, background: NSColor) -> Double {
        let blkThrs = 0.022
        let blkClmp = 1.414
        let deltaYmin = 0.0005
        let loClip = 0.1
        let loBoWoffset = 0.027
        let loWoBoffset = 0.027
        let scaleBoW = 1.14
        let scaleWoB = 1.14
        let normBG = 0.56
        let normTXT = 0.57
        let revTXT = 0.62
        let revBG = 0.65

        func softClamp(_ y: Double) -> Double {
            y > blkThrs ? y : y + pow(blkThrs - y, blkClmp)
        }

        let txt = softClamp(screenLuminance(text))
        let bg = softClamp(screenLuminance(background))

        if abs(bg - txt) < deltaYmin { return 0.0 }

        let sapc: Double
        let output: Double
        if bg > txt {
            sapc = (pow(bg, normBG) - pow(txt, normTXT)) * scaleBoW
            output = sapc < loClip ? 0.0 : sapc - loBoWoffset
        } else {
            sapc = (pow(bg, revBG) - pow(txt, revTXT)) * scaleWoB
            output = sapc > -loClip ? 0.0 : sapc + loWoBoffset
        }
        return output * 100.0
    }

    /// APCA "screen luminance" Y: sRGB channels raised to the
    /// 2.4 display TRC and weighted by Rec. 709 coefficients.
    /// Simple-exponent form (not WCAG's piecewise inverse
    /// companding).
    static func screenLuminance(_ c: NSColor) -> Double {
        let r = pow(Double(c.redComponent), 2.4)
        let g = pow(Double(c.greenComponent), 2.4)
        let b = pow(Double(c.blueComponent), 2.4)
        return 0.2126729 * r + 0.7151522 * g + 0.0721750 * b
    }

    /// APCA `Lc` for a token pairing resolved in a specific appearance (#451).
    /// `text`/`background` may be dynamic colors; both are resolved to sRGB
    /// under `appearance` (via `performAsCurrentDrawingAppearance`) before
    /// measuring, so the same pairing can be checked in Aqua and DarkAqua.
    static func lc(text: NSColor, background: NSColor, for appearance: NSAppearance) -> Double {
        var result = 0.0
        appearance.performAsCurrentDrawingAppearance {
            let t = text.usingColorSpace(.sRGB) ?? text
            let b = background.usingColorSpace(.sRGB) ?? background
            result = lc(text: t, background: b)
        }
        return result
    }
}
