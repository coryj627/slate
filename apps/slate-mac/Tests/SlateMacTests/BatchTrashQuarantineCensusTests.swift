// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

/// Source-level regression ledger for the host paths that can write a vault
/// item or reacquire a path-bound native document. Behavioral tests prove the
/// gate's semantics; this census makes adding a new bypass an explicit review
/// event instead of relying on reviewers to remember every funnel.
final class BatchTrashQuarantineCensusTests: XCTestCase {
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

    private func source(_ relativePath: String) throws -> String {
        try String(
            contentsOf: Self.sourceRoot.appendingPathComponent(relativePath),
            encoding: .utf8)
    }

    /// Extract a declaration body after blanking comments and strings. Starting
    /// the stripper at the declaration avoids an unrelated raw/multiline string
    /// earlier in a large source file confusing the intentionally small lexer.
    private func functionBody(
        declaration: String,
        in source: String
    ) -> String? {
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
                if depth == 0 {
                    return String(stripped[open...cursor])
                }
            default: break
            }
            cursor = stripped.index(after: cursor)
        }
        return nil
    }

    private func assertFunnels(
        _ funnels: [Funnel],
        file: StaticString = #filePath,
        line: UInt = #line
    ) throws {
        var cachedSources: [String: String] = [:]
        for funnel in funnels {
            let text: String
            if let cached = cachedSources[funnel.file] {
                text = cached
            } else {
                text = try source(funnel.file)
                cachedSources[funnel.file] = text
            }
            guard let body = functionBody(
                declaration: funnel.declaration,
                in: text)
            else {
                XCTFail(
                    "Missing writer/load funnel \(funnel.file):\(funnel.declaration)",
                    file: file,
                    line: line)
                continue
            }
            for required in funnel.requiredCode {
                XCTAssertTrue(
                    body.contains(required),
                    "\(funnel.file):\(funnel.declaration) must retain `\(required)`",
                    file: file,
                    line: line)
            }
        }
    }

    func testEveryKnownPathWriterUsesTheCentralUnknownTrashAdmission() throws {
        let appState = "AppState.swift"
        let pathGate = "admitBatchTrashWrite("
        let mutationGate = "admitBatchTrashMutation("
        let vaultGate = "admitBatchTrashVaultWideWrite("
        try assertFunnels([
            Funnel(file: appState, declaration: "func toggleCurrentTask", requiredCode: [pathGate]),
            Funnel(file: appState, declaration: "func performToggleCurrentTask", requiredCode: [pathGate]),
            Funnel(file: appState, declaration: "func toggleVaultTask", requiredCode: [pathGate]),
            Funnel(file: appState, declaration: "func performToggleVaultTask", requiredCode: [pathGate]),
            Funnel(file: appState, declaration: "func saveCurrentNote", requiredCode: [pathGate]),
            Funnel(file: appState, declaration: "func performSave", requiredCode: [pathGate]),
            Funnel(file: appState, declaration: "func resolveSaveConflictKeepMine", requiredCode: [pathGate]),
            Funnel(file: appState, declaration: "func createFolder", requiredCode: [pathGate]),
            Funnel(file: appState, declaration: "func createNote", requiredCode: [pathGate]),
            Funnel(
                file: appState,
                declaration: "func duplicateEntry",
                requiredCode: [pathGate, "batchTrashUnknownSiblingNames("]),
            Funnel(file: appState, declaration: "func renameEntry", requiredCode: [pathGate, vaultGate]),
            Funnel(file: appState, declaration: "func moveEntry", requiredCode: [pathGate, vaultGate]),
            Funnel(
                file: appState,
                declaration: "func batchMove(\n        _ items: [TreeSelection],\n        to newParent: String,",
                requiredCode: [pathGate, vaultGate]),
            Funnel(file: appState, declaration: "func importEntry", requiredCode: [pathGate]),
            Funnel(
                file: appState,
                declaration: "func requestDeleteEntry",
                requiredCode: [mutationGate]),
            Funnel(
                file: appState,
                declaration: "func requestBatchDelete",
                requiredCode: [mutationGate]),
            Funnel(
                file: appState,
                declaration: "func batchDelete(\n        _ items: [TreeSelection],\n        preferredFocusPath:",
                requiredCode: [mutationGate]),
            Funnel(
                file: appState,
                declaration: "func deleteEntry",
                requiredCode: [mutationGate]),
            Funnel(
                file: appState,
                declaration: "func createFolderThenMove",
                requiredCode: [pathGate, mutationGate, vaultGate]),
            Funnel(
                file: appState,
                declaration: "func createFolderThenBatchMove",
                requiredCode: [pathGate, mutationGate, vaultGate]),
            Funnel(file: appState, declaration: "func setProperty", requiredCode: [pathGate]),
            Funnel(file: appState, declaration: "func applyPropertiesSource", requiredCode: [pathGate]),
            Funnel(file: appState, declaration: "func deleteProperty", requiredCode: [pathGate]),
            Funnel(file: appState, declaration: "func performPropertyEdit", requiredCode: [pathGate]),
            Funnel(
                file: appState,
                declaration: "func resolvePropertyEditConflictKeepMine",
                requiredCode: [pathGate]),
            Funnel(
                file: appState,
                declaration: "func runRename",
                requiredCode: [vaultGate, "admitStructuralMutationRequest(", "beginStructuralMutation("]),
            Funnel(
                file: appState,
                declaration: "func performRename",
                requiredCode: [vaultGate, "ownsStructuralMutation("]),
            Funnel(file: appState, declaration: "func submitTemplateNoteName", requiredCode: [pathGate]),
            Funnel(
                file: "AppState+History.swift",
                declaration: "func performRestore",
                requiredCode: [pathGate]),
            Funnel(
                file: "AppState+History.swift",
                declaration: "func requestRecoverDeleted",
                requiredCode: [pathGate]),
            Funnel(
                file: "AppState+History.swift",
                declaration: "func commitRestoreAs",
                requiredCode: [pathGate]),
            Funnel(
                file: "Canvas/AppState+CanvasActions.swift",
                declaration: "func canvasNewCanvasFile",
                requiredCode: [pathGate, "writableCandidatePaths", "batchTrashPathCapability("]),
            Funnel(
                file: "Canvas/AppState+CanvasExtras.swift",
                declaration: "func canvasConvertToNote",
                requiredCode: [pathGate]),
            Funnel(
                file: "Graph/AppState+Connections.swift",
                declaration: "func createNoteFromGhost",
                requiredCode: [pathGate]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func exportSavedQuery",
                requiredCode: [pathGate]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func basesBuilderSaveToView",
                requiredCode: [pathGate]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func basesBuilderSaveAsBase",
                requiredCode: [pathGate]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func basesSaveSortToView",
                requiredCode: [pathGate]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func basesApplyProperty",
                requiredCode: [pathGate]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func performBaseSavePanelWrite",
                requiredCode: [pathGate]),
        ])
    }

    func testEveryKnownNativeDocumentReopenUsesTheCentralQuarantine() throws {
        let quarantine = "isBatchTrashPathQuarantined"
        let baseLoad = "loadBaseDocumentIfAllowed("
        let baseInteractionGate = "baseDocumentInteractionDisabledReason(for:"
        let builderInteractionGate = "baseQueryBuilderSaveToViewDisabledReason"
        try assertFunnels([
            Funnel(
                file: "Canvas/AppState+Canvas.swift",
                declaration: "func activateCanvasTab",
                requiredCode: [quarantine]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func loadBaseDocumentIfAllowed",
                requiredCode: [quarantine]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func loadBaseEmbedDocumentIfAllowed",
                requiredCode: [quarantine]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func activateBaseDocumentTab",
                requiredCode: [quarantine, baseLoad]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func refreshBasesDockTarget",
                requiredCode: [quarantine, baseLoad]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func refreshVisibleBasesAfterInAppWrite",
                requiredCode: [quarantine]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func basesEditViewFilters",
                requiredCode: [baseInteractionGate]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func basesBuilderSaveToView",
                requiredCode: [builderInteractionGate]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func basesRefresh",
                requiredCode: [baseLoad]),
            Funnel(
                file: "AppState.swift",
                declaration: "func resumePresentBatchTrashDocuments",
                requiredCode: [quarantine]),
            Funnel(
                file: "Bases/AppState+Bases.swift",
                declaration: "func resumePresentBaseEmbedHandles",
                requiredCode: [quarantine]),
        ])

        let container = try source("Bases/BaseContainerView.swift")
        XCTAssertTrue(container.contains(".onAppear"))
        XCTAssertTrue(container.contains("loadBaseDocumentIfAllowed("))

        let embed = try source("Bases/BaseEmbedView.swift")
        XCTAssertTrue(embed.contains(".onAppear"))
        XCTAssertTrue(embed.contains("loadBaseEmbedDocumentIfAllowed("))
    }

    func testPersistentRecoveryIsRenderedAfterTheOneShotAlertCanBeDismissed() throws {
        let sidebar = try source("FileTreeSidebar.swift")
        XCTAssertTrue(sidebar.contains("batchTrashQuarantineRecovery"))
        XCTAssertTrue(sidebar.contains("appState.batchTrashQuarantineNotice"))
        XCTAssertTrue(sidebar.contains("Button(AppState.BatchTrashCopy.checkAgainLabel)"))
        XCTAssertTrue(sidebar.contains("retryBatchTrashUnknownReconciliation()"))
        XCTAssertTrue(sidebar.contains(".accessibilityHint(AppState.BatchTrashCopy.checkAgainHint)"))

        let split = try source("MainSplitView.swift")
        let splitCore = try XCTUnwrap(
            functionBody(declaration: "private var splitViewCore", in: split))
        XCTAssertTrue(
            splitCore.contains(".safeAreaInset(edge: .top"),
            "recovery must be mounted in the always-visible window shell")
        XCTAssertTrue(splitCore.contains("batchTrashQuarantineRecovery"))

        let recovery = try XCTUnwrap(
            functionBody(
                declaration: "private var batchTrashQuarantineRecovery",
                in: split))
        XCTAssertTrue(recovery.contains("appState.batchTrashQuarantineNotice"))
        XCTAssertTrue(recovery.contains("Button(AppState.BatchTrashCopy.checkAgainLabel)"))
        XCTAssertTrue(recovery.contains("retryBatchTrashUnknownReconciliation()"))
        XCTAssertTrue(
            recovery.contains(".accessibilityHint("))
        XCTAssertTrue(recovery.contains("AppState.BatchTrashCopy.checkAgainHint"))
    }
}
