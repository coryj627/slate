// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #264 acceptance: user-facing copy routed through the
/// localization layer must round-trip unchanged while the app ships
/// English-only (no string catalogs). `String(localized:)` with no
/// catalog falls back to the development-language literal — these
/// tests pin that contract so the i18n routing can't silently alter
/// shipped copy, and so a future catalog that DOES change copy fails
/// loudly here instead of drifting unnoticed.
///
/// Scope note: SwiftUI literal inits (`Text("…")`, `Toggle("…")`,
/// `Picker("…")`, `.accessibilityLabel("…")`) are
/// `LocalizedStringKey`-backed by the framework — they are already
/// "localization-routed" and need no wrapping. The only literals
/// outside that path were `String`-typed properties; those are what
/// #264 wrapped.
final class SettingsLocalizationTests: XCTestCase {

    func testMathFooterCopyRoundTripsUnchanged() {
        XCTAssertEqual(
            MathSettingsTab.mathFooterText,
            "Changes apply immediately to math in the read pane. "
                + "Speech style controls how math is read aloud (ClearSpeak: "
                + "intuitive; MathSpeak: precise / verbatim). Verbosity sets "
                + "how detailed the spoken math is. Braille code switches "
                + "between Nemeth and UEB encodings.",
            "math footer must round-trip unchanged through String(localized:)"
        )
    }

    func testCodeFooterCopyRoundTripsUnchanged() {
        XCTAssertEqual(
            CodeSettingsTab.codeFooterText,
            "Affects the preamble screen readers hear before a code block. "
                + "\"Preamble only\" reads \"Code block, <language>, N lines.\" "
                + "\"Preamble + first line\" adds the signature/first non-blank "
                + "line. \"Preamble + all tokens\" reads every token (useful for "
                + "braille display work). Font and color preferences land later.",
            "code footer must round-trip unchanged through String(localized:)"
        )
    }
}
