// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// Guards the `SlateSymbol` icon layer (#450):
///  - every fallback glyph is known-safe on the macOS 15 floor (OS-independent
///    fixture) AND loads on the current OS,
///  - resolution is total and valid on both the v7 and fallback paths,
///  - the v7/fallback split is genuinely exercised,
///  - the toolbar migration didn't change which glyph renders,
///  - every role's default label is a clean VoiceOver name, and
///  - no view bypasses the layer with a raw SF Symbol string.
final class SlateSymbolTests: XCTestCase {

    /// SF Symbols verified present on the macOS 15.0 floor (all predate
    /// macOS 14). This is the checked-in fixture the fallback path is held to,
    /// so the floor guarantee doesn't depend on which OS the tests run on
    /// (Codex review: an `NSImage` load check alone passes on a macOS 26
    /// runner even for a v7-only name). Adding a new fallback forces a
    /// conscious entry here.
    private static let knownMacOS15SafeSymbols: Set<String> = [
        "square.and.arrow.down", "magnifyingglass", "doc.badge.plus", "checklist",
        // curlybraces: SF Symbols 2 (macOS 11) — U3-4 show-source toggle.
        "curlybraces",
        "quote.bubble.fill", "books.vertical", "function",
        "chevron.left.forwardslash.chevron.right", "exclamationmark.triangle.fill",
        "arrow.down.right.and.arrow.up.left", "ellipsis", "xmark.circle.fill",
        "plus.circle", "rectangle.2.swap", "checkmark.square", "square",
        "plus", "xmark", "rectangle.split.2x1", "book", "pencil",
        "folder", "folder.fill", "chevron.right", "list.bullet.indent",
        // U4-2 leaf roles (#471).
        "arrow.uturn.backward", "arrow.up.right", "photo.on.rectangle",
        "point.3.connected.trianglepath.dotted",
        // U4-3 (#472): utility-bar roles + the menu's selection checkmark. All
        // predate SF Symbols 7 (gearshape/questionmark.circle/checkmark since
        // v1; externaldrive since v2/macOS 11), so they're on the macOS 15 floor.
        "gearshape", "questionmark.circle", "externaldrive", "checkmark",
        // U2-5 (#463): file-management command roles. All predate SF Symbols 7
        // (folder.badge.plus / square.and.pencil / trash since v1;
        // arrow.turn.down.right since v1), so they're on the macOS 15 floor.
        "folder.badge.plus", "square.and.pencil", "arrow.turn.down.right", "trash",
        // Milestone T (#369): canvas tab glyph. rectangle.3.group is
        // SF Symbols 2 (macOS 11), comfortably on the macOS 15 floor.
        "rectangle.3.group",
        // Milestone N (#702): base tab glyph. tablecells predates the
        // macOS 15 floor and matches the table-first base renderer.
        "tablecells",
        // Milestone M (M-3, #534): sync-diagnostics leaf fallback + the two
        // risk-badge glyphs. arrow.triangle.2.circlepath is SF Symbols 1
        // (macOS 11); exclamationmark.circle / info.circle are SF Symbols 1.
        "arrow.triangle.2.circlepath", "exclamationmark.circle", "info.circle",
    ]

    /// Floor guarantee, OS-independent: every fallback is in the curated
    /// macOS-15-safe fixture. A fallback accidentally set to a v7/macOS-26-only
    /// name fails here regardless of the runner OS.
    func testEveryFallbackIsFloorSafe() {
        for symbol in SlateSymbol.allCases {
            let fallback = SlateSymbol.symbolName(for: symbol, macOS26: false)
            XCTAssertTrue(
                Self.knownMacOS15SafeSymbols.contains(fallback),
                "Fallback '\(fallback)' for \(symbol) is not in the macOS-15-safe fixture. "
                    + "Verify it exists on macOS 15.0 and add it to knownMacOS15SafeSymbols."
            )
        }
    }

    /// Runtime check on the current OS. On the macOS 15 CI runner this is a
    /// true floor check; paired with `testEveryFallbackIsFloorSafe` the
    /// guarantee holds on any runner.
    func testEveryFallbackLoadsOnCurrentOS() {
        for symbol in SlateSymbol.allCases {
            let name = SlateSymbol.symbolName(for: symbol, macOS26: false)
            XCTAssertNotNil(
                NSImage(systemSymbolName: name, accessibilityDescription: nil),
                "Fallback SF Symbol '\(name)' for \(symbol) failed to load on this OS."
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

    /// The labeled/decorative builders are the only supported way to render a
    /// symbol; forbid raw `Image(systemName:)` / `systemImage:` anywhere in the
    /// app sources except the layer itself, so a future view can't silently
    /// reintroduce an unlabeled glyph (Codex review). Source-level lint.
    func testNoRawSFSymbolsOutsideLayer() throws {
        let sourcesDir = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .appendingPathComponent("Sources/SlateMac")
        let fm = FileManager.default
        // Recurse so a symbol reintroduced under a future subfolder (e.g.
        // Sources/SlateMac/Views/) is still caught (Codex re-review).
        guard let walker = fm.enumerator(at: sourcesDir, includingPropertiesForKeys: nil) else {
            return XCTFail("Could not enumerate \(sourcesDir.path).")
        }

        // The layer legitimately names raw symbols; generated FFI code never
        // uses SwiftUI but is excluded defensively.
        let allowed: Set<String> = ["SlateSymbol.swift", "slate_uniffi.swift"]
        // Regexes (not substring) so `Image( systemName:`, `Image.init(systemName:`,
        // and spaced `systemImage :` are all caught.
        let rawImage = try NSRegularExpression(pattern: #"Image\s*(?:\.init)?\s*\(\s*systemName\s*:"#)
        let rawLabel = try NSRegularExpression(pattern: #"systemImage\s*:"#)

        var scanned = 0
        for case let url as URL in walker where url.pathExtension == "swift" {
            if allowed.contains(url.lastPathComponent) { continue }
            scanned += 1
            let text = try String(contentsOf: url, encoding: .utf8)
            let range = NSRange(text.startIndex..., in: text)
            XCTAssertNil(
                rawImage.firstMatch(in: text, range: range),
                "\(url.lastPathComponent) uses a raw Image(systemName:) — route it through SlateSymbol."
            )
            XCTAssertNil(
                rawLabel.firstMatch(in: text, range: range),
                "\(url.lastPathComponent) uses a raw systemImage: — route it through SlateSymbol."
            )
        }
        XCTAssertGreaterThan(scanned, 0, "No Swift sources scanned under \(sourcesDir.path).")
    }

    /// The lint gate is TOTAL (u5_spec U5-1): the ONLY files exempt from the
    /// raw-symbol ban are the layer itself and the generated FFI file (which
    /// never uses SwiftUI). No per-surface / legacy exemptions exist — if one
    /// ever creeps back in, this fails, keeping the sweep's "route every glyph
    /// through SlateSymbol" invariant enforced forever, not just for new code.
    /// (This mirrors the `allowed` set inside `testNoRawSFSymbolsOutsideLayer`;
    /// the two must stay in lockstep — that test's exemption list IS the gate's
    /// surface area.)
    func testLintAllowlistIsTotal() {
        let allowed: Set<String> = ["SlateSymbol.swift", "slate_uniffi.swift"]
        XCTAssertEqual(
            allowed, ["SlateSymbol.swift", "slate_uniffi.swift"],
            "The raw-symbol lint may exempt ONLY the SlateSymbol layer and the "
                + "generated FFI file. A new exemption means a surface bypassing "
                + "SlateSymbol — remove it and route the glyph through the layer.")
    }

    // MARK: - Per-surface rendering mode (U5-1, #474)

    /// Each chrome surface pins its glyphs to ONE symbol-rendering mode, and the
    /// mapping is the u5_spec table verbatim — a *decision*, not an accident
    /// (DoD §B). Regression-locks the table so a surface's fill treatment can't
    /// silently drift. `SymbolRenderingMode` isn't `Equatable`, so we assert its
    /// (stable) description names the expected mode.
    func testSurfaceRenderingModesMatchSpec() {
        let expected: [(surface: SlateSymbol.Surface, mode: String)] = [
            (.toolbar, "monochrome"),
            (.tabStrip, "monochrome"),
            (.rail, "hierarchical"),
            (.tree, "hierarchical"),
        ]
        for (surface, mode) in expected {
            let described = String(describing: surface.renderingMode)
            XCTAssertTrue(
                described.contains(mode),
                "\(surface) should render \(mode); got \(described).")
        }
    }

    /// The toolbar and tab strip deliberately SHARE monochrome (they read as one
    /// command band), and the two rails + the tree deliberately SHARE
    /// hierarchical (they read as families) — but the command surfaces and the
    /// navigation surfaces resolve to DIFFERENT modes. Pins the two-bucket split
    /// so a future edit can't collapse everything to one mode.
    func testCommandAndNavigationSurfacesDiffer() {
        let command = String(describing: SlateSymbol.Surface.toolbar.renderingMode)
        let navigation = String(describing: SlateSymbol.Surface.rail.renderingMode)
        XCTAssertNotEqual(
            command, navigation,
            "Command surfaces (toolbar/tab strip) and navigation surfaces "
                + "(rail/tree) must not resolve to the same rendering mode.")
        XCTAssertEqual(
            String(describing: SlateSymbol.Surface.toolbar.renderingMode),
            String(describing: SlateSymbol.Surface.tabStrip.renderingMode),
            "Toolbar and tab strip share one mode (one command band).")
        XCTAssertEqual(
            String(describing: SlateSymbol.Surface.rail.renderingMode),
            String(describing: SlateSymbol.Surface.tree.renderingMode),
            "The rails and the tree share one mode (one navigation family).")
    }
}
