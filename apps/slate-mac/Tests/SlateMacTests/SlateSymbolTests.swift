// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// Guards the `SlateSymbol` icon layer (#450):
///  - every fallback glyph actually exists on the macOS 15 floor,
///  - resolution is total and valid on both the v7 and fallback paths,
///  - the v7/fallback split is genuinely exercised,
///  - the toolbar migration didn't change which glyph renders, and
///  - every role's default label is a clean VoiceOver name.
final class SlateSymbolTests: XCTestCase {

    /// The safety guarantee: on the macOS 15–25 range we render the
    /// `fallback`, so every fallback MUST resolve to a real system symbol on
    /// the minimum OS. `NSImage(systemSymbolName:)` returns nil for a symbol
    /// the running OS doesn't ship — the CI runner is the floor, so this
    /// catches a fallback that only exists on a newer OS.
    func testEveryFallbackLoadsOnMinimumOS() {
        for symbol in SlateSymbol.allCases {
            let name = SlateSymbol.symbolName(for: symbol, macOS26: false)
            XCTAssertNotNil(
                NSImage(systemSymbolName: name, accessibilityDescription: nil),
                "Fallback SF Symbol '\(name)' for \(symbol) does not exist on this (floor) OS."
            )
        }
    }

    /// Resolution is total and produces syntactically-valid symbol identifiers
    /// on BOTH paths — verifiable without running on macOS 26.
    func testResolveIsValidOnBothPaths() {
        let identifier = try! NSRegularExpression(pattern: "^[a-z0-9.]+$")
        for symbol in SlateSymbol.allCases {
            for macOS26 in [true, false] {
                let name = SlateSymbol.symbolName(for: symbol, macOS26: macOS26)
                XCTAssertFalse(name.isEmpty, "\(symbol) macOS26=\(macOS26) resolved to empty.")
                let range = NSRange(name.startIndex..., in: name)
                XCTAssertNotNil(
                    identifier.firstMatch(in: name, range: range),
                    "\(symbol) macOS26=\(macOS26) resolved to invalid symbol name '\(name)'."
                )
            }
        }
    }

    /// The v7/fallback mechanism is real, not dead code: roles with a split
    /// resolve to distinct glyphs depending on the OS flag.
    func testV7PathIsExercised() {
        for role in [SlateSymbol.readingMode, .editingMode] {
            XCTAssertNotEqual(
                role.names.v7, role.names.fallback,
                "\(role) is expected to demonstrate the v7/fallback split."
            )
            XCTAssertEqual(SlateSymbol.symbolName(for: role, macOS26: true), role.names.v7)
            XCTAssertEqual(SlateSymbol.symbolName(for: role, macOS26: false), role.names.fallback)
        }
    }

    /// "Toolbar snapshot identical pre/post": the migration must not have
    /// changed which glyph each already-shipping role renders on macOS 15–25.
    func testMigratedRolesRenderTheSameGlyph() {
        let expected: [SlateSymbol: String] = [
            .save: "square.and.arrow.down",
            .search: "magnifyingglass",
            .newFromTemplate: "doc.badge.plus",
            .tasksReview: "checklist",
            .citationSummary: "quote.bubble.fill",
            .bibliography: "books.vertical",
            .math: "function",
            .code: "chevron.left.forwardslash.chevron.right",
            .warning: "exclamationmark.triangle.fill",
            .expandInline: "arrow.down.right.and.arrow.up.left",
            .moreActions: "ellipsis",
            .clearSearch: "xmark.circle.fill",
            .addProperty: "plus.circle",
            .bulkRename: "rectangle.2.swap",
            .taskComplete: "checkmark.square",
            .taskIncomplete: "square",
        ]
        for (symbol, name) in expected {
            XCTAssertEqual(
                SlateSymbol.symbolName(for: symbol, macOS26: false), name,
                "\(symbol) now renders a different glyph than before the migration."
            )
        }
    }

    /// Every role's default label is a usable VoiceOver name: non-empty, no
    /// trailing period, and no role words (VoiceOver announces the trait).
    func testTitlesAreCleanAccessibilityLabels() {
        let banned = ["icon", "button", "image", "graphic"]
        for symbol in SlateSymbol.allCases {
            let title = symbol.title
            XCTAssertFalse(title.isEmpty, "\(symbol) has an empty title.")
            XCTAssertFalse(title.hasSuffix("."), "\(symbol) title '\(title)' ends with a period.")
            for word in banned {
                XCTAssertFalse(
                    title.lowercased().contains(word),
                    "\(symbol) title '\(title)' contains the role word '\(word)'."
                )
            }
        }
    }
}
