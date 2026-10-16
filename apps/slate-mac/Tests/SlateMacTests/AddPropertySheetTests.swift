// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI
import XCTest

@testable import SlateMac

/// The Add Property sheet as an assistive-technology client reads it.
@MainActor
final class AddPropertySheetTests: XCTestCase {

    /// #446 (WCAG 1.3.1): the visible "Key" and "Type" captions sit beside
    /// a text field and a picker that carry their own names, so a client
    /// that met the captions as standalone static texts read each label
    /// twice. The captions stay on screen; only AT stops meeting them.
    func testCaptionsBesideNamedControlsAreHiddenFromAssistiveTechnology() async throws {
        let window = HostedAccessibilityTree.host(
            AddPropertySheet().environmentObject(AppState()))
        defer { window.close() }
        let tree = try await HostedAccessibilityTree.snapshot(of: window) { tree in
            tree.subtree.contains { $0.role == "AXTextField" && $0.name == "Property key" }
                && tree.subtree.contains { $0.role == "AXPopUpButton" }
        }

        XCTAssertFalse(
            tree.staticTexts.contains("Key"),
            "the Key caption must not be a standalone AT element:\n\(tree)")
        XCTAssertFalse(
            tree.staticTexts.contains("Type"),
            "the Type caption must not be a standalone AT element:\n\(tree)")
        XCTAssertTrue(
            tree.elements(role: "AXPopUpButton").contains {
                ($0.name ?? "").hasPrefix("Property type")
            },
            "the type picker still carries its own name:\n\(tree)")
    }
}
