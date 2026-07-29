// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// W3-3: a same-position unsaved edit keeps byte containment, and
/// showing the OLD diagram — or speaking its old description — would
/// misinform AT users. The saved artifact applies only under
/// live-source coherence; the Windows twin pins the identical rule
/// end-to-end (`UnsavedDiagramEditsNeverShowTheStaleDiagram`).
final class ReadingDiagramCoherenceTests: XCTestCase {
    func testIdenticalSourceIsCoherent() {
        XCTAssertTrue(
            ReadingDiagramCoherence.isCoherent(
                artifactSource: "flowchart LR\nA --> B\n",
                interior: "flowchart LR\nA --> B\n"))
    }

    func testTrailingNewlineDifferencesAreCoherent() {
        XCTAssertTrue(
            ReadingDiagramCoherence.isCoherent(
                artifactSource: "flowchart LR\nA --> B\n",
                interior: "flowchart LR\nA --> B"))
        XCTAssertTrue(
            ReadingDiagramCoherence.isCoherent(
                artifactSource: "flowchart LR\nA --> B",
                interior: "flowchart LR\nA --> B\n"))
    }

    func testCrlfSavedSourceNormalizes() {
        XCTAssertTrue(
            ReadingDiagramCoherence.isCoherent(
                artifactSource: "flowchart LR\r\nA --> B\r\n",
                interior: "flowchart LR\nA --> B\n"))
    }

    func testSameLengthUnsavedEditIsNotCoherent() {
        // The stale-artifact case: byte containment still holds, the
        // content does not.
        XCTAssertFalse(
            ReadingDiagramCoherence.isCoherent(
                artifactSource: "flowchart LR\nA --> B\n",
                interior: "flowchart LR\nA --> Z\n"))
    }
}
