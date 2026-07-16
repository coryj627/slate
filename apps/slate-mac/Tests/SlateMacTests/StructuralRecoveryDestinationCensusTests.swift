// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

/// The destination-recovery barrier is an architectural funnel, not a fix for
/// one rename call site. This ledger makes every native create/move destination
/// an explicit review surface when a new writer is added.
final class StructuralRecoveryDestinationCensusTests: XCTestCase {
    private struct Funnel {
        let file: String
        let declaration: String
        let requiredCode: [String]
    }

    private static let sourceRoot = URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .appendingPathComponent("Sources/SlateMac")

    private func functionBody(declaration: String, in source: String) -> String? {
        guard let declarationRange = source.range(of: declaration) else { return nil }
        let suffix = String(source[declarationRange.lowerBound...])
        let stripped = SwiftSourceStripping.strippingCommentsAndStrings(suffix)
        guard let open = stripped.firstIndex(of: "{") else { return nil }
        var depth = 0
        var cursor = open
        while cursor < stripped.endIndex {
            switch stripped[cursor] {
            case "{": depth += 1
            case "}":
                depth -= 1
                if depth == 0 { return String(stripped[open...cursor]) }
            default: break
            }
            cursor = stripped.index(after: cursor)
        }
        return nil
    }

    func testEveryStructuralDestinationUsesCentralRecoveryAdmission() throws {
        let gate = "admitStructuralRecoveryDestination("
        let recreationGate = "admitStructuralRecoveryRecreation("
        let retargetGate = "admitStructuralRecoveryRetargets("
        let appState = "AppState.swift"
        let reservation = "recoveryReservation:"
        let installReservation = "installStructuralRecoveryReservation("
        let funnels = [
            Funnel(
                file: appState, declaration: "func createFolder(",
                requiredCode: [gate, reservation]),
            Funnel(
                file: appState, declaration: "func createNote(",
                requiredCode: [gate, reservation]),
            Funnel(
                file: appState, declaration: "func duplicateEntry(",
                requiredCode: [gate, installReservation]),
            Funnel(
                file: appState, declaration: "func renameEntry(",
                requiredCode: [retargetGate, reservation]),
            Funnel(
                file: appState, declaration: "func moveEntry(",
                requiredCode: [retargetGate, reservation]),
            Funnel(
                file: appState,
                declaration: "func batchMove(\n        _ items: [TreeSelection],\n        to newParent: String,",
                requiredCode: [retargetGate, reservation]),
            Funnel(
                file: appState,
                declaration: "func executeBatchMoveInverse(",
                requiredCode: [retargetGate, reservation]),
            Funnel(
                file: appState, declaration: "func importEntry(",
                requiredCode: [gate, reservation]),
            Funnel(
                file: appState,
                declaration: "func createFolderThenMove(",
                requiredCode: [gate, retargetGate]),
            Funnel(
                file: appState,
                declaration: "func createFolderThenBatchMove(",
                requiredCode: [gate, retargetGate]),
            Funnel(
                file: appState,
                declaration: "func submitTemplateNoteName(",
                requiredCode: [gate, reservation]),
            Funnel(
                file: appState,
                declaration: "func resolveSaveConflictKeepMine(",
                requiredCode: [recreationGate, reservation]),
            Funnel(
                file: appState,
                declaration: "func performSave(",
                requiredCode: [recreationGate, installReservation]),
            Funnel(
                file: "AppState+History.swift",
                declaration: "func requestRecoverDeleted(",
                requiredCode: [gate, reservation]),
            Funnel(
                file: "AppState+History.swift",
                declaration: "func commitRestoreAs(",
                requiredCode: [gate, reservation]),
            Funnel(
                file: "Graph/AppState+Connections.swift",
                declaration: "func createNoteFromGhost(",
                requiredCode: [gate, reservation]),
            Funnel(
                file: "Canvas/AppState+CanvasExtras.swift",
                declaration: "func canvasConvertToNote(",
                requiredCode: [gate, reservation]),
            Funnel(
                file: "Canvas/AppState+CanvasActions.swift",
                declaration: "func canvasNewCanvasFile()",
                requiredCode: [gate, installReservation]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func exportSavedQuery(",
                requiredCode: [gate, reservation]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func basesBuilderSaveAsBase(",
                requiredCode: [gate, reservation]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func performBaseSavePanelWrite(",
                requiredCode: [gate, reservation]),
        ]

        var sources: [String: String] = [:]
        for funnel in funnels {
            let text: String
            if let cached = sources[funnel.file] {
                text = cached
            } else {
                text = try String(
                    contentsOf: Self.sourceRoot.appendingPathComponent(funnel.file),
                    encoding: .utf8)
                sources[funnel.file] = text
            }
            guard let body = functionBody(declaration: funnel.declaration, in: text) else {
                XCTFail("Missing structural funnel \(funnel.file):\(funnel.declaration)")
                continue
            }
            for required in funnel.requiredCode {
                XCTAssertTrue(
                    body.contains(required),
                    "\(funnel.file):\(funnel.declaration) must retain `\(required)`")
            }
        }
    }
}
