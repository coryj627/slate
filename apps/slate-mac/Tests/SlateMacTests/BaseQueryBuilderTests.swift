// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

private actor BasePreviewTestGate {
    private var continuation: CheckedContinuation<Void, Never>?
    private var isReleased = false

    func wait() async {
        guard !isReleased else { return }
        await withCheckedContinuation { continuation = $0 }
    }

    func release() {
        isReleased = true
        continuation?.resume()
        continuation = nil
    }
}

/// N4-1 (#707): accessible Bases query-builder core. These tests stay at
/// the draft/model boundary so keyboard and VoiceOver contracts are
/// executable without depending on a fragile SwiftUI snapshot.
@MainActor
final class BaseQueryBuilderTests: XCTestCase {
    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-base-builder-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private func makeSession() throws -> (URL, VaultSession) {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Projects"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Queries"), withIntermediateDirectories: true)
        try Data(
            """
            ---
            tags: [project, shared]
            status: active
            priority: 3
            ---
            # Alpha
            #inline
            - [ ] Project task
            """.utf8
        ).write(to: vault.appendingPathComponent("Projects/Alpha.md"))
        try Data("# Zeta\n\n- [ ] Outside task\n".utf8)
            .write(to: vault.appendingPathComponent("Zeta.md"))
        let session = try VaultSession.openFilesystem(rootPath: vault.path)
        try session.scanInitial(cancel: CancelToken())
        return (vault, session)
    }

    private func makeAppState() async throws -> (URL, AppState, VaultSession) {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Projects"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Queries"), withIntermediateDirectories: true)
        try Data(
            """
            ---
            status: active
            priority: 3
            ---
            # Alpha
            """.utf8
        ).write(to: vault.appendingPathComponent("Projects/Alpha.md"))
        try Data("# Zeta\n".utf8).write(to: vault.appendingPathComponent("Zeta.md"))

        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return (vault, state, try XCTUnwrap(state.currentSession))
    }

    private static func sourceFile(_ relativePath: String) throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        while cursor.path != "/" {
            let candidate = cursor.appendingPathComponent(relativePath)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor.deleteLastPathComponent()
        }
        throw CocoaError(.fileNoSuchFile)
    }

    private static func jsonObject(_ json: String) throws -> [String: Any] {
        let data = try XCTUnwrap(json.data(using: .utf8))
        return try XCTUnwrap(
            JSONSerialization.jsonObject(with: data) as? [String: Any])
    }

    private static func jsonString(_ object: [String: Any]) throws -> String {
        let data = try JSONSerialization.data(withJSONObject: object, options: [.sortedKeys])
        return try XCTUnwrap(String(data: data, encoding: .utf8))
    }

    private static func hasViewFilterEdit(_ edits: [BaseEdit]) -> Bool {
        edits.contains { edit in
            if case .setViewFilters = edit { return true }
            if case .removeViewKey(_, let key) = edit, key == "filters" { return true }
            return false
        }
    }

    private static func hasViewKeyEdit(_ edits: [BaseEdit], key expectedKey: String) -> Bool {
        edits.contains { edit in
            if case .setViewKey(_, let key, _) = edit, key == expectedKey { return true }
            if case .removeViewKey(_, let key) = edit, key == expectedKey { return true }
            return false
        }
    }

    private static func hasSlateStateEdit(_ edits: [BaseEdit]) -> Bool {
        edits.contains { edit in
            if case .setSlateState = edit { return true }
            return false
        }
    }

    private static func hasSlateSortEdit(_ edits: [BaseEdit]) -> Bool {
        edits.contains { edit in
            if case .setSlateSort = edit { return true }
            return false
        }
    }

    private static func hasRemoveFormulaEdit(
        _ edits: [BaseEdit],
        named expectedName: String
    ) -> Bool {
        edits.contains { edit in
            if case .removeFormula(let name) = edit, name == expectedName { return true }
            return false
        }
    }

    private static func readyResult(_ state: BaseQueryPreviewState) -> BasesResultSet? {
        guard case .ready(let result) = state else { return nil }
        return result
    }

    private static func semanticJSON(_ value: Any) throws -> String {
        func removingSpans(_ value: Any) -> Any {
            if let object = value as? [String: Any] {
                return object.reduce(into: [String: Any]()) { result, item in
                    guard item.key != "span" else { return }
                    result[item.key] = removingSpans(item.value)
                }
            }
            if let values = value as? [Any] {
                return values.map(removingSpans)
            }
            return value
        }
        let data = try JSONSerialization.data(
            withJSONObject: removingSpans(value),
            options: [.sortedKeys])
        return try XCTUnwrap(String(data: data, encoding: .utf8))
    }

    private static func statementExpressions(in filter: Any?) -> [Any] {
        guard let object = filter as? [String: Any] else { return [] }
        if let expression = object["Stmt"] { return [expression] }
        for key in ["And", "Or", "Not"] {
            if let children = object[key] as? [Any] {
                return children.flatMap { statementExpressions(in: $0) }
            }
        }
        return []
    }

    func testBuilderSourceAndConditionCompileToSaveableBaseFilters() throws {
        let (vault, session) = try makeSession()
        var draft = BaseQueryBuilderDraft()
        draft.source = .folder("Projects")
        draft.rows = [
            .condition(
                BaseQueryCondition(
                    property: .note("status"),
                    operator: .equals,
                    value: .text("active")))
        ]

        let queryJSON = try draft.queryJSON()
        try session.saveQueryAsBase(queryJson: queryJSON, path: "Queries/Active.base")
        let saved = try String(
            contentsOf: vault.appendingPathComponent("Queries/Active.base"),
            encoding: .utf8)

        XCTAssertTrue(saved.contains(#"file.inFolder(\"Projects\")"#), saved)
        XCTAssertTrue(saved.contains(#"(status == \"active\")"#), saved)
        let handle = try session.openQuery(queryJson: queryJSON, thisPath: nil)
        let result = try session.baseExecute(
            handle: handle,
            view: 0,
            thisPath: nil,
            quickFilter: nil,
            cancel: CancelToken())
        XCTAssertEqual(result.rows.map(\.filePath), ["Projects/Alpha.md"])
    }

    func testKeyboardCommandsAndVoiceOverRowsStayStructured() throws {
        let model = BaseQueryBuilderModel()

        model.perform(.addCondition)
        XCTAssertEqual(model.rows.count, 1)
        XCTAssertEqual(
            model.rows[0].accessibilityLabel(index: 0),
            "Condition 1: status equals active")
        XCTAssertEqual(model.conditionsListAccessibilityValue, "Combined with AND")

        model.perform(.editCondition(index: 0))
        XCTAssertEqual(model.editingRowIndex, 0)

        model.perform(.removeCondition(index: 0))
        XCTAssertTrue(model.rows.isEmpty)
        XCTAssertNil(model.editingRowIndex)

        model.perform(.addGroup)
        XCTAssertEqual(model.rows.count, 1)
        model.perform(.setGroupCombinator(index: 0, combinator: .none))
        model.perform(.addConditionToGroup(index: 0))
        guard case .group(let group) = model.rows[0] else {
            return XCTFail("expected add group command to append a group row")
        }
        XCTAssertEqual(group.combinator, .none)
        XCTAssertEqual(group.rows.count, 2)
    }

    func testGroupsAndAdvancedChipsAreNavigableWithoutFreeTextPrimarySurface() throws {
        var draft = BaseQueryBuilderDraft()
        draft.combinator = .any
        draft.rows = [
            .condition(
                BaseQueryCondition(
                    property: .file(.name),
                    operator: .startsWith,
                    value: .text("Project"))),
            .group(
                BaseQueryConditionGroup(
                    combinator: .none,
                    rows: [
                        .condition(
                            BaseQueryCondition(
                                property: .note("status"),
                                operator: .equals,
                                value: .text("archived")))
                    ])),
            .advanced(rawExpression: "formula.score / priority > 2", filterJSON: nil),
        ]

        XCTAssertEqual(draft.conditionsListAccessibilityValue, "Combined with OR")
        XCTAssertEqual(
            draft.rows[1].accessibilityLabel(index: 1),
            "Group 2: NONE of 1 condition")
        XCTAssertEqual(
            draft.rows[2].accessibilityLabel(index: 2),
            "Advanced condition: formula.score / priority > 2")
        XCTAssertTrue(
            try draft.queryJSON().contains("\"Or\""),
            "top-level ANY must compile to a SlateQuery OR filter")
        XCTAssertTrue(
            try draft.queryJSON().contains("\"Not\""),
            "one-level NONE groups must compile to a SlateQuery NOT filter")
    }

    func testNestedGroupsDecodeAsAdvancedChipsInsteadOfEditableStructure() throws {
        var nested = BaseQueryBuilderDraft()
        nested.rows = [
            .condition(
                BaseQueryCondition(
                    property: .file(.name),
                    operator: .contains,
                    value: .text("Alpha"))),
            .group(
                BaseQueryConditionGroup(
                    combinator: .any,
                    rows: [
                        .group(
                            BaseQueryConditionGroup(
                                combinator: .all,
                                rows: [
                                    .condition(
                                        BaseQueryCondition(
                                            property: .note("status"),
                                            operator: .equals,
                                            value: .text("active")))
                                ]))
                    ]))
        ]

        let decoded = try BaseQueryBuilderDraft(queryJSON: nested.queryJSON())

        guard decoded.rows.count == 2, case .group(let group) = decoded.rows[1] else {
            return XCTFail("expected one top-level editable group")
        }
        XCTAssertEqual(group.combinator, .any)
        guard case .advanced(let raw, let filterJSON) = group.rows.first else {
            return XCTFail("nested group should be a read-only advanced chip")
        }
        XCTAssertTrue(raw.contains("\"And\""), raw)
        XCTAssertNotNil(filterJSON)
        XCTAssertTrue(
            try decoded.queryJSON().contains("\"And\""),
            "advanced nested group must preserve its original filter node")
    }

    func testExistingBaseViewLoadsCanonicalSourceAndStructuredConditions() throws {
        let (_, session) = try makeSession()
        try session.saveText(
            path: "Queries/Existing.base",
            contents:
                #"""
                filters:
                  and:
                    - "file.inFolder(\"Projects\")"
                    - "status == \"active\""
                views:
                  - type: table
                    name: Existing
                    order:
                      - file.name
                      - status
                """#,
            expectedContentHash: nil)

        let handle = try session.openBase(path: "Queries/Existing.base")
        let queryJSON = try session.baseViewQueryJson(handle: handle, view: 0)
        let draft = try BaseQueryBuilderDraft(queryJSON: queryJSON)

        XCTAssertEqual(draft.source, .folder("Projects"))
        XCTAssertEqual(draft.rows.count, 1)
        XCTAssertEqual(
            draft.rows[0].accessibilityLabel(index: 0),
            "Condition 1: status equals active")
    }

    func testEditingViewFiltersLoadsOnlyTheActiveViewFilterBlock() throws {
        let (vault, session) = try makeSession()
        try session.saveText(
            path: "Queries/Scoped.base",
            contents:
                #"""
                filters: "file.inFolder(\"Projects\")"
                views:
                  - type: table
                    name: Scoped
                    filters: "status == \"active\""
                    order:
                      - file.name
                      - status
                """#,
            expectedContentHash: nil)

        let handle = try session.openBase(path: "Queries/Scoped.base")
        let draft = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewEditQueryJson(handle: handle, view: 0))

        XCTAssertEqual(draft.source, .allNotes)
        XCTAssertEqual(draft.rows.count, 1)
        XCTAssertEqual(
            draft.rows[0].accessibilityLabel(index: 0),
            "Condition 1: status equals active")

        for edit in try draft.baseEditsForView(0, replacing: draft) {
            try session.baseApplyEdit(handle: handle, edit: edit)
        }
        let saved = try String(
            contentsOf: vault.appendingPathComponent("Queries/Scoped.base"),
            encoding: .utf8)
        XCTAssertEqual(saved.components(separatedBy: "file.inFolder").count - 1, 1, saved)
        XCTAssertFalse(saved.contains(#"    filters: "file.inFolder"#), saved)
    }

    func testBuilderRoundTripPreservesOpaqueRootFacetsWhileEditingOwnedFields() throws {
        var root = try Self.jsonObject(BaseQueryBuilderDraft().queryJSON())
        root["limit"] = 25
        root["summaries"] = [
            ["file.size", ["Builtin": "Sum"]]
        ]
        root["custom_summaries"] = [
            [
                "ratio",
                [
                    "kind": [
                        "Lit": ["Number": 0.5]
                    ]
                ],
            ]
        ]
        root["future_facet"] = [
            "nested": [
                "items": [1, 2, 3],
                "enabled": true,
            ]
        ]

        var draft = try BaseQueryBuilderDraft(queryJSON: Self.jsonString(root))
        draft.rows = [
            .condition(
                BaseQueryCondition(
                    property: .note("status"),
                    operator: .equals,
                    value: .text("active")))
        ]
        let encoded = try Self.jsonObject(draft.queryJSON())

        XCTAssertEqual(encoded["limit"] as? Int, 25)
        XCTAssertEqual(
            encoded["summaries"] as? NSArray,
            root["summaries"] as? NSArray)
        XCTAssertEqual(
            encoded["custom_summaries"] as? NSArray,
            root["custom_summaries"] as? NSArray)
        XCTAssertEqual(
            encoded["future_facet"] as? NSDictionary,
            root["future_facet"] as? NSDictionary)
        XCTAssertNotNil(encoded["filters"] as? [String: Any])
    }

    func testDualContextDraftPreviewsEffectiveQueryButWritesOnlyLocalViewFilters() throws {
        let (vault, session) = try makeSession()
        try session.saveText(
            path: "Queries/DualContext.base",
            contents:
                #"""
                filters: "file.inFolder(\"Projects\")"
                views:
                  - type: table
                    name: Scoped
                    filters: "status == \"active\""
                    order:
                      - file.name
                      - status
                """#,
            expectedContentHash: nil)

        let handle = try session.openBase(path: "Queries/DualContext.base")
        let effectiveJSON = try session.baseViewQueryJson(handle: handle, view: 0)
        let localJSON = try session.baseViewEditQueryJson(handle: handle, view: 0)
        let draft = try BaseQueryBuilderDraft(
            effectiveQueryJSON: effectiveJSON,
            localQueryJSON: localJSON)

        XCTAssertEqual(draft.source, .allNotes)
        XCTAssertEqual(draft.rows.count, 1)
        XCTAssertEqual(
            draft.rows[0].accessibilityLabel(index: 0),
            "Condition 1: status equals active")

        let encoded = try Self.jsonObject(draft.queryJSON())
        let effectiveFilter = try XCTUnwrap(encoded["filters"] as? [String: Any])
        let effectiveNodes = try XCTUnwrap(effectiveFilter["And"] as? [Any])
        XCTAssertEqual(effectiveNodes.count, 2)

        let previewHandle = try session.openQuery(queryJson: draft.queryJSON(), thisPath: nil)
        defer { session.closeBase(handle: previewHandle) }
        let preview = try session.baseExecute(
            handle: previewHandle,
            view: 0,
            thisPath: nil,
            quickFilter: nil,
            cancel: CancelToken())
        XCTAssertEqual(preview.rows.map(\.filePath), ["Projects/Alpha.md"])

        for edit in try draft.baseEditsForView(0, replacing: draft) {
            try session.baseApplyEdit(handle: handle, edit: edit)
        }
        let saved = try String(
            contentsOf: vault.appendingPathComponent("Queries/DualContext.base"),
            encoding: .utf8)
        XCTAssertEqual(saved.components(separatedBy: "file.inFolder").count - 1, 1, saved)
        XCTAssertEqual(saved.components(separatedBy: #"status == \"active\""#).count - 1, 1, saved)
    }

    func testDualContextDraftFailsClosedWhenEffectiveFiltersAreNotRustComposition() throws {
        let (_, session) = try makeSession()
        var effective = try Self.jsonObject(BaseQueryBuilderDraft().queryJSON())
        var local = effective
        let effectiveValidation = try XCTUnwrap(
            session.validateBaseExpression(source: #"status == "active""#).exprJson)
        let localValidation = try XCTUnwrap(
            session.validateBaseExpression(source: #"priority >= 2"#).exprJson)
        effective["filters"] = ["Stmt": try Self.jsonObject(effectiveValidation)]
        local["filters"] = ["Stmt": try Self.jsonObject(localValidation)]

        XCTAssertThrowsError(
            try BaseQueryBuilderDraft(
                effectiveQueryJSON: Self.jsonString(effective),
                localQueryJSON: Self.jsonString(local)))
    }

    func testDualContextWithNoLocalFilterPreviewsGlobalWithoutWritingItToView() throws {
        let (vault, session) = try makeSession()
        try session.saveText(
            path: "Queries/GlobalOnly.base",
            contents:
                #"""
                filters: "file.inFolder(\"Projects\")"
                views:
                  - type: table
                    name: Global only
                    order:
                      - file.name
                """#,
            expectedContentHash: nil)
        let handle = try session.openBase(path: "Queries/GlobalOnly.base")
        let draft = try BaseQueryBuilderDraft(
            effectiveQueryJSON: session.baseViewQueryJson(handle: handle, view: 0),
            localQueryJSON: session.baseViewEditQueryJson(handle: handle, view: 0))

        XCTAssertTrue(draft.rows.isEmpty)
        XCTAssertEqual(draft.source, .allNotes)
        let effective = try Self.jsonObject(draft.queryJSON())
        XCTAssertNotNil(effective["filters"] as? [String: Any])
        let previewHandle = try session.openQuery(queryJson: draft.queryJSON(), thisPath: nil)
        defer { session.closeBase(handle: previewHandle) }
        let preview = try session.baseExecute(
            handle: previewHandle,
            view: 0,
            thisPath: nil,
            quickFilter: nil,
            cancel: CancelToken())
        XCTAssertEqual(preview.rows.map(\.filePath), ["Projects/Alpha.md"])

        let edits = try draft.baseEditsForView(0, replacing: draft)
        XCTAssertFalse(edits.contains { edit in
            if case .setViewFilters = edit { return true }
            if case .removeViewKey(_, let key) = edit, key == "filters" { return true }
            return false
        })
        for edit in edits {
            try session.baseApplyEdit(handle: handle, edit: edit)
        }
        let saved = try String(
            contentsOf: vault.appendingPathComponent("Queries/GlobalOnly.base"),
            encoding: .utf8)
        XCTAssertEqual(saved.components(separatedBy: "file.inFolder").count - 1, 1, saved)
        XCTAssertFalse(saved.contains(#"    filters: "file.inFolder"#), saved)
    }

    func testSavedRecentAndLinkedSourcesReopenAsPickerSources() throws {
        let (vault, session) = try makeSession()
        var recent = BaseQueryBuilderDraft()
        recent.source = .recent(days: 14)
        try session.saveQueryAsBase(
            queryJson: recent.queryJSON(),
            path: "Queries/Recent.base")
        var linked = BaseQueryBuilderDraft()
        linked.source = .linked(fromPath: "Projects/Alpha.md")
        try session.saveQueryAsBase(
            queryJson: linked.queryJSON(),
            path: "Queries/Linked.base")

        let recentHandle = try session.openBase(path: "Queries/Recent.base")
        let savedRecent = try String(
            contentsOf: vault.appendingPathComponent("Queries/Recent.base"),
            encoding: .utf8)
        XCTAssertTrue(savedRecent.contains("file.mtime >= now() - duration"), savedRecent)
        XCTAssertFalse(savedRecent.contains("file.mtime > now() - duration"), savedRecent)
        let recentDraft = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: recentHandle, view: 0))
        let linkedHandle = try session.openBase(path: "Queries/Linked.base")
        let linkedDraft = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: linkedHandle, view: 0))

        XCTAssertEqual(recentDraft.source, .recent(days: 14))
        XCTAssertTrue(recentDraft.rows.isEmpty)
        XCTAssertEqual(linkedDraft.source, .linked(fromPath: "Projects/Alpha.md"))
        XCTAssertTrue(linkedDraft.rows.isEmpty)
    }

    func testNonCanonicalSourceLikeMultiTagStaysAdvanced() throws {
        let (_, session) = try makeSession()
        try session.saveText(
            path: "Queries/MultiTag.base",
            contents:
                #"""
                filters: "file.hasTag(\"project\", \"shared\")"
                views:
                  - type: table
                    name: Tags
                    order:
                      - file.name
                """#,
            expectedContentHash: nil)

        let handle = try session.openBase(path: "Queries/MultiTag.base")
        let draft = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: handle, view: 0))

        XCTAssertEqual(draft.source, .allNotes)
        guard draft.rows.count == 1 else {
            return XCTFail("expected one advanced row, got \(draft.rows.count)")
        }
        guard case .advanced(let raw, let preservedJSON) = draft.rows[0] else {
            return XCTFail("multi-argument tag source must stay an advanced chip")
        }
        XCTAssertTrue(raw.contains("HasTag"), raw)
        XCTAssertNotNil(preservedJSON)
        XCTAssertTrue(try draft.queryJSON().contains("shared"))
    }

    func testKnownMethodCallsWithWrongArityStayAdvancedAndPreserveJSON() throws {
        let (_, session) = try makeSession()
        try session.saveText(
            path: "Queries/AdvancedCalls.base",
            contents:
                #"""
                filters:
                  and:
                    - "status.contains(\"active\", priority)"
                    - "status.isEmpty(\"x\")"
                views:
                  - type: table
                    name: Calls
                    order:
                      - file.name
                """#,
            expectedContentHash: nil)

        let handle = try session.openBase(path: "Queries/AdvancedCalls.base")
        let draft = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: handle, view: 0))

        XCTAssertEqual(draft.rows.count, 2)
        for row in draft.rows {
            guard case .advanced = row else {
                return XCTFail("wrong-arity known methods must stay advanced")
            }
        }
        let reencoded = try draft.queryJSON()
        XCTAssertTrue(reencoded.contains("priority"), reencoded)
        XCTAssertTrue(reencoded.contains(#""x""#), reencoded)
    }

    func testAdvancedViewFiltersSkipNoOpAndPreserveAllArgumentsAndNestedGroups() throws {
        let (_, session) = try makeSession()
        try session.saveText(
            path: "Queries/AdvancedNested.base",
            contents:
                #"""
                views:
                  - type: table
                    name: Advanced nested
                    filters:
                      and:
                        - "file.hasTag(\"project\", \"shared\")"
                        - or:
                            - "status == \"active\""
                            - and:
                                - "priority >= 2"
                                - "file.name.contains(\"Alpha\")"
                    order:
                      - file.name
                """#,
            expectedContentHash: nil)

        let handle = try session.openBase(path: "Queries/AdvancedNested.base")
        let previous = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewEditQueryJson(handle: handle, view: 0))

        XCTAssertFalse(
            Self.hasViewFilterEdit(try previous.baseEditsForView(0, replacing: previous)),
            "an unchanged advanced filter must not be rewritten")

        var edited = previous
        edited.rows.append(
            .condition(
                BaseQueryCondition(
                    property: .file(.name),
                    operator: .contains,
                    value: .text("Alpha"))))
        for edit in try edited.baseEditsForView(0, replacing: previous) {
            try session.baseApplyEdit(handle: handle, edit: edit)
        }

        let reopenedJSON = try session.baseViewQueryJson(handle: handle, view: 0)
        XCTAssertTrue(reopenedJSON.contains("project"), reopenedJSON)
        XCTAssertTrue(reopenedJSON.contains("shared"), reopenedJSON)
        XCTAssertTrue(reopenedJSON.contains(#""Or""#), reopenedJSON)
        XCTAssertGreaterThanOrEqual(
            reopenedJSON.components(separatedBy: #""And""#).count - 1,
            2,
            reopenedJSON)

        let preview = try session.baseExecute(
            handle: handle,
            view: 0,
            thisPath: nil,
            quickFilter: nil,
            cancel: CancelToken())
        XCTAssertEqual(preview.rows.map(\.filePath), ["Projects/Alpha.md"])
    }

    func testAdvancedViewFilterRendererPreservesEveryV1ExpressionShapeAndPrecedence() throws {
        let (_, session) = try makeSession()
        let sources = [
            #"status.containsAny(["active", "queued"])"#,
            #"status.containsAny(["a\"b", "c\\d"])"#,
            #"-priority > 2"#,
            #"file.tags[0] == "project""#,
            #"file.name.matches(/Alpha/)"#,
            #"file.name.matches(/A\/B/)"#,
            #"{status: "active"} == {status: "active"}"#,
            #"{"status label": "a\"b"} == {"status label": "a\"b"}"#,
            #"file.tags.filter(value == "project").isEmpty()"#,
            #"file.tags.map(index).isEmpty()"#,
            #"file.tags.reduce(acc + value, "") == "project""#,
            #"file.hasLink(this)"#,
            #"this.status == status"#,
            #"this.file.name == file.name"#,
            #"(a + b) * c > 0"#,
            #"a - (b - c) > 0"#,
        ]
        let expressions: [[String: Any]] = try sources.map { source in
            let validation = session.validateBaseExpression(source: source)
            XCTAssertTrue(validation.valid, "\(source): \(validation.message ?? "invalid")")
            return try Self.jsonObject(XCTUnwrap(validation.exprJson))
        }
        var root = try Self.jsonObject(BaseQueryBuilderDraft().queryJSON())
        root["filters"] = ["And": expressions.map { ["Stmt": $0] }]
        let previous = try BaseQueryBuilderDraft(queryJSON: Self.jsonString(root))
        var edited = previous
        edited.rows.append(
            .condition(
                BaseQueryCondition(
                    property: .file(.name),
                    operator: .contains,
                    value: .text("Alpha"))))

        try session.saveText(
            path: "Queries/AllExpressions.base",
            contents:
                """
                views:
                  - type: table
                    name: All expressions
                    order:
                      - file.name
                """,
            expectedContentHash: nil)
        let handle = try session.openBase(path: "Queries/AllExpressions.base")
        for edit in try edited.baseEditsForView(0, replacing: previous) {
            try session.baseApplyEdit(handle: handle, edit: edit)
        }

        let reopened = try Self.jsonObject(
            session.baseViewQueryJson(handle: handle, view: 0))
        let reopenedExpressions = try Set(
            Self.statementExpressions(in: reopened["filters"]).map(Self.semanticJSON))
        for expression in expressions {
            let semanticExpression = try Self.semanticJSON(expression)
            XCTAssertTrue(
                reopenedExpressions.contains(semanticExpression),
                "regenerated filter changed AST semantics for \(semanticExpression)")
        }
    }

    func testTasksViewKeepsCanonicalFolderScopeAdvancedAcrossPreviewAndSaveToView() throws {
        let (_, session) = try makeSession()
        try session.saveText(
            path: "Queries/ScopedTasks.base",
            contents:
                #"""
                views:
                  - type: table
                    name: Scoped tasks
                    source: tasks
                    filters: "file.inFolder(\"Projects\")"
                    order:
                      - task.text
                      - task.file
                """#,
            expectedContentHash: nil)

        let handle = try session.openBase(path: "Queries/ScopedTasks.base")
        let previous = try BaseQueryBuilderDraft(
            effectiveQueryJSON: session.baseViewQueryJson(handle: handle, view: 0),
            localQueryJSON: session.baseViewEditQueryJson(handle: handle, view: 0))

        XCTAssertEqual(previous.source, .tasks)
        guard previous.rows.count == 1, case .advanced = previous.rows[0] else {
            return XCTFail("a Tasks view folder filter must remain an advanced local condition")
        }

        let previewHandle = try session.openQuery(queryJson: previous.queryJSON(), thisPath: nil)
        defer { session.closeBase(handle: previewHandle) }
        let preview = try session.baseExecute(
            handle: previewHandle,
            view: 0,
            thisPath: nil,
            quickFilter: nil,
            cancel: CancelToken())
        XCTAssertEqual(preview.rows.map(\.filePath), ["Projects/Alpha.md"])

        var edited = previous
        edited.rows.append(
            .condition(
                BaseQueryCondition(
                    property: .task(.completed),
                    operator: .equals,
                    value: .bool(false))))
        for edit in try edited.baseEditsForView(0, replacing: previous) {
            try session.baseApplyEdit(handle: handle, edit: edit)
        }

        let persisted = try session.baseExecute(
            handle: handle,
            view: 0,
            thisPath: nil,
            quickFilter: nil,
            cancel: CancelToken())
        XCTAssertEqual(persisted.rows.map(\.filePath), ["Projects/Alpha.md"])
    }

    func testUnsupportedViewTypesStayOpaqueUntilExplicitlyChanged() throws {
        let (vault, session) = try makeSession()
        try session.saveText(
            path: "Queries/OpaqueViews.base",
            contents:
                #"""
                views:
                  - type: cards
                    name: Cards
                  - type: map
                    name: Map
                  - type: plugin-grid
                    name: Plugin
                """#,
            expectedContentHash: nil)

        let handle = try session.openBase(path: "Queries/OpaqueViews.base")
        var firstPrevious: BaseQueryBuilderDraft?
        for index in 0..<3 {
            let queryJSON = try session.baseViewEditQueryJson(
                handle: handle,
                view: UInt32(index))
            let originalRoot = try Self.jsonObject(queryJSON)
            let previous = try BaseQueryBuilderDraft(queryJSON: queryJSON)

            XCTAssertNotEqual(previous.viewType, .table)
            let encodedRoot = try Self.jsonObject(previous.queryJSON())
            XCTAssertEqual(
                encodedRoot["view"] as? NSDictionary,
                originalRoot["view"] as? NSDictionary)
            XCTAssertFalse(
                Self.hasViewKeyEdit(
                    try previous.baseEditsForView(UInt32(index), replacing: previous),
                    key: "type"))
            if index == 0 { firstPrevious = previous }
        }

        let previous = try XCTUnwrap(firstPrevious)
        XCTAssertThrowsError(
            try session.saveQueryAsBase(
                queryJson: previous.queryJSON(),
                path: "Queries/CardsCopy.base"))
        var filterEdited = previous
        filterEdited.rows.append(
            .condition(
                BaseQueryCondition(
                    property: .file(.name),
                    operator: .contains,
                    value: .text("Alpha"))))
        let filterEdits = try filterEdited.baseEditsForView(0, replacing: previous)
        XCTAssertTrue(Self.hasViewFilterEdit(filterEdits))
        XCTAssertFalse(Self.hasViewKeyEdit(filterEdits, key: "type"))
        for edit in filterEdits {
            try session.baseApplyEdit(handle: handle, edit: edit)
        }

        var changed = previous
        changed.viewType = .table
        XCTAssertTrue(
            Self.hasViewKeyEdit(
                try changed.baseEditsForView(0, replacing: previous),
                key: "type"))

        let saved = try String(
            contentsOf: vault.appendingPathComponent("Queries/OpaqueViews.base"),
            encoding: .utf8)
        XCTAssertTrue(saved.contains("type: cards"), saved)
        XCTAssertTrue(saved.contains("type: map"), saved)
        XCTAssertTrue(saved.contains("type: plugin-grid"), saved)
    }

    func testUnsupportedSourceAndGroupByStayOpaqueUntilExplicitlyChanged() throws {
        var root = try Self.jsonObject(BaseQueryBuilderDraft().queryJSON())
        root["source"] = ["Unsupported": "plugin source"]
        root["group_by"] = [
            "property": ["Future": "cluster"],
            "ascending": false,
            "plugin": ["layout": "radial"],
        ]
        let originalJSON = try Self.jsonString(root)
        let previous = try BaseQueryBuilderDraft(queryJSON: originalJSON)

        XCTAssertNotEqual(previous.source, .allNotes)
        XCTAssertNil(previous.groupBy)
        let unchanged = try Self.jsonObject(previous.queryJSON())
        XCTAssertEqual(unchanged["source"] as? NSDictionary, root["source"] as? NSDictionary)
        XCTAssertEqual(
            unchanged["group_by"] as? NSDictionary,
            root["group_by"] as? NSDictionary)

        var filterEdited = previous
        filterEdited.rows = [
            .condition(
                BaseQueryCondition(
                    property: .file(.name),
                    operator: .contains,
                    value: .text("Alpha")))
        ]
        let filterEditedRoot = try Self.jsonObject(filterEdited.queryJSON())
        XCTAssertEqual(
            filterEditedRoot["source"] as? NSDictionary,
            root["source"] as? NSDictionary)
        XCTAssertEqual(
            filterEditedRoot["group_by"] as? NSDictionary,
            root["group_by"] as? NSDictionary)

        let (_, session) = try makeSession()
        XCTAssertThrowsError(
            try session.saveQueryAsBase(
                queryJson: filterEdited.queryJSON(),
                path: "Queries/Unsupported.base"))

        var changed = previous
        changed.source = .allNotes
        changed.groupBy = BaseQueryGroupBy(property: .file(.folder), ascending: true)
        let encoded = try Self.jsonObject(changed.queryJSON())
        XCTAssertEqual(encoded["source"] as? String, "All")
        XCTAssertNotEqual(
            encoded["group_by"] as? NSDictionary,
            root["group_by"] as? NSDictionary)
    }

    func testNoncanonicalRecognizedSourcesRemainOpaqueUntilExplicitRetarget() throws {
        let noncanonicalSources: [[String: Any]] = [
            ["Linked": ["from_path": "Projects/Alpha.md", "depth": 2]],
            ["Recent": ["days": 0]],
            ["Folder": "Projects", "future": ["scope": "children"]],
        ]

        for originalSource in noncanonicalSources {
            var root = try Self.jsonObject(BaseQueryBuilderDraft().queryJSON())
            root["source"] = originalSource
            var draft = try BaseQueryBuilderDraft(queryJSON: Self.jsonString(root))
            guard case .unsupported = draft.source else {
                return XCTFail("noncanonical source must be read only: \(originalSource)")
            }
            var encoded = try Self.jsonObject(draft.queryJSON())
            XCTAssertEqual(encoded["source"] as? NSDictionary, originalSource as NSDictionary)

            draft.rows.append(
                .condition(
                    BaseQueryCondition(
                        property: .file(.name),
                        operator: .contains,
                        value: .text("Alpha"))))
            encoded = try Self.jsonObject(draft.queryJSON())
            XCTAssertEqual(encoded["source"] as? NSDictionary, originalSource as NSDictionary)

            draft.source = .linked(fromPath: "Projects/Alpha.md")
            encoded = try Self.jsonObject(draft.queryJSON())
            XCTAssertEqual(
                encoded["source"] as? NSDictionary,
                ["Linked": ["from_path": "Projects/Alpha.md", "depth": 1]] as NSDictionary)
        }
    }

    func testTypedValuesKeepLiteralKindsAfterEditingText() throws {
        let (_, session) = try makeSession()
        try session.saveText(
            path: "Queries/TypedValues.base",
            contents:
                #"""
                filters:
                  and:
                    - "priority >= 2.5"
                    - "task.completed == true"
                views:
                  - type: table
                    name: Typed
                    order:
                      - file.name
                """#,
            expectedContentHash: nil)

        let handle = try session.openBase(path: "Queries/TypedValues.base")
        var draft = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: handle, view: 0))

        guard case .condition(var numberCondition) = draft.rows[0],
            case .condition(var boolCondition) = draft.rows[1]
        else { return XCTFail("expected typed conditions") }
        XCTAssertEqual(numberCondition.value.editingText, "2.5")
        numberCondition.value = numberCondition.value.replacingEditingText("3.25")
        boolCondition.value = boolCondition.value.replacingEditingText("false")
        draft.rows[0] = .condition(numberCondition)
        draft.rows[1] = .condition(boolCondition)
        let reencoded = try draft.queryJSON()

        XCTAssertTrue(reencoded.contains(#""Number":3.25"#), reencoded)
        XCTAssertTrue(reencoded.contains(#""Bool":false"#), reencoded)
        XCTAssertFalse(reencoded.contains(#""String":"3.25""#), reencoded)
        XCTAssertFalse(reencoded.contains(#""String":"false""#), reencoded)
    }

    func testNewlyAuthoredTypedPropertiesSerializeByPropertyKind() throws {
        var draft = BaseQueryBuilderDraft()
        draft.rows = [
            .condition(
                BaseQueryCondition(
                    property: .file(.size),
                    operator: .greaterThan,
                    value: .text("3.25"))),
            .condition(
                BaseQueryCondition(
                    property: .task(.completed),
                    operator: .equals,
                    value: .text("false"))),
        ]

        let reencoded = try draft.queryJSON()

        XCTAssertTrue(reencoded.contains(#""Number":3.25"#), reencoded)
        XCTAssertTrue(reencoded.contains(#""Bool":false"#), reencoded)
        XCTAssertFalse(reencoded.contains(#""String":"3.25""#), reencoded)
        XCTAssertFalse(reencoded.contains(#""String":"false""#), reencoded)
    }

    func testRetargetedTypedPropertiesSerializeByNewPropertyKind() throws {
        var condition = BaseQueryCondition(
            property: .task(.priority),
            operator: .equals,
            value: .number(1))
        condition.property = .task(.completed)

        var draft = BaseQueryBuilderDraft()
        draft.rows = [.condition(condition)]
        let reencoded = try draft.queryJSON()

        XCTAssertTrue(reencoded.contains(#""Bool":true"#), reencoded)
        XCTAssertFalse(reencoded.contains(#""Number":1"#), reencoded)
    }

    func testRetargetedIncompatibleTypedPropertiesStayInNewLiteralFamily() throws {
        var boolCondition = BaseQueryCondition(
            property: .task(.priority),
            operator: .equals,
            value: .number(2))
        boolCondition.property = .task(.completed)
        var numberCondition = BaseQueryCondition(
            property: .task(.completed),
            operator: .equals,
            value: .bool(true))
        numberCondition.property = .file(.size)

        var draft = BaseQueryBuilderDraft()
        draft.rows = [.condition(boolCondition), .condition(numberCondition)]
        let reencoded = try draft.queryJSON()

        XCTAssertTrue(reencoded.contains(#""Bool":false"#), reencoded)
        XCTAssertTrue(reencoded.contains(#""Number":0"#), reencoded)
        XCTAssertFalse(reencoded.contains(#""String":"2""#), reencoded)
        XCTAssertFalse(reencoded.contains(#""String":"true""#), reencoded)
    }

    func testPropertyChoicesCarryIndexedKindsWithoutChangingPropertyIdentity() throws {
        let number = BaseQueryPropertyChoice(
            summary: PropertyKeySummary(
                key: "score",
                fileCount: 2,
                valueKinds: ["number"]))
        let mixed = BaseQueryPropertyChoice(
            summary: PropertyKeySummary(
                key: "mixed",
                fileCount: 3,
                valueKinds: ["boolean", "number"]))

        XCTAssertEqual(number.property, .note("score"))
        XCTAssertEqual(number.kind, .number)
        XCTAssertEqual(mixed.property, .note("mixed"))
        XCTAssertEqual(mixed.kind, .mixedOrUnknown)
        XCTAssertEqual(
            Set(BaseQueryPropertyChoice.fileChoices.map(\.property)),
            Set(BaseQueryFileField.allCases.map(BaseQueryProperty.file)))
        XCTAssertEqual(
            Set(BaseQueryPropertyChoice.taskChoices.map(\.property)),
            Set(BaseQueryTaskField.allCases.map(BaseQueryProperty.task)))
        XCTAssertEqual(
            BaseQueryPropertyChoice.fileChoices.first { $0.property == .file(.properties) }?.kind,
            .object)
        XCTAssertEqual(
            BaseQueryPropertyChoice.fileChoices.first { $0.property == .file(.file) }?.kind,
            .file)
        XCTAssertEqual(
            BaseQueryPropertyChoice.taskChoices.first { $0.property == .task(.completed) }?.kind,
            .boolean)
    }

    func testOperatorAndEditorMatricesAreKindSpecific() throws {
        let equalityAndEmpty: [BaseQueryOperator] = [.equals, .notEquals, .isEmpty]
        let ordered: [BaseQueryOperator] = [
            .equals, .notEquals, .greaterThan, .greaterThanOrEqual,
            .lessThan, .lessThanOrEqual, .isEmpty,
        ]

        let cases: [(
            kind: BaseQueryValueKind,
            operators: [BaseQueryOperator],
            editor: BaseQueryEditorDescriptor
        )] = [
            (
                .text,
                [.equals, .notEquals, .contains, .startsWith, .endsWith, .isEmpty],
                .text),
            (.number, ordered, .number),
            (.boolean, equalityAndEmpty, .toggle),
            (.date, ordered, .dateAndRelative),
            (.datetime, ordered, .dateAndRelative),
            (.list, [.equals, .notEquals, .contains, .isEmpty], .tokenList),
            (.tagList, [.equals, .notEquals, .contains, .isEmpty], .tokenList),
            (.wikilink, equalityAndEmpty, .link),
            (
                .file,
                [.equals, .notEquals, .hasTag, .hasLink, .matches, .isEmpty],
                .link),
            (.object, equalityAndEmpty, .text),
            (.mixedOrUnknown, equalityAndEmpty, .text),
            (.formula, equalityAndEmpty, .text),
        ]

        for testCase in cases {
            XCTAssertEqual(
                BaseQueryOperator.options(for: testCase.kind),
                testCase.operators,
                "operator matrix for \(testCase.kind)")
            XCTAssertEqual(
                BaseQueryEditorDescriptor.forKind(testCase.kind),
                testCase.editor,
                "editor matrix for \(testCase.kind)")
        }
    }

    func testMethodOperatorInputKindsMatchExecutableArgumentFamilies() throws {
        let cases: [(
            receiver: BaseQueryValueKind,
            op: BaseQueryOperator,
            operand: BaseQueryValueKind
        )] = [
            (.file, .hasTag, .text),
            (.file, .hasLink, .file),
            (.file, .matches, .text),
            (.list, .contains, .text),
            (.tagList, .contains, .text),
            (.text, .contains, .text),
        ]

        for testCase in cases {
            XCTAssertEqual(
                BaseQueryCondition.inputKind(
                    for: testCase.receiver,
                    operator: testCase.op),
                testCase.operand,
                "operand kind for \(testCase.receiver).\(testCase.op)")
        }
    }

    private func assertFileSpecialOperatorRoundTrip(
        _ op: BaseQueryOperator,
        value: BaseQueryValue,
        baseName: String
    ) throws {
        let (_, session) = try makeSession()
        _ = try session.saveText(
            path: "Projects/Alpha.md",
            contents:
                """
                ---
                tags: [project, shared]
                status: active
                priority: 3
                ---
                # Alpha
                #inline
                [[Zeta]]
                - [ ] Project task
                """,
            expectedContentHash: nil)

        var draft = BaseQueryBuilderDraft()
        draft.source = .folder("Projects")
        draft.rows = [
            .condition(
                BaseQueryCondition(
                    property: .file(.file),
                    operator: op,
                    value: value))
        ]
        let queryJSON = try draft.queryJSON()
        let liveHandle = try session.openQuery(queryJson: queryJSON, thisPath: nil)
        let live = try session.baseExecute(
            handle: liveHandle,
            view: 0,
            thisPath: nil,
            quickFilter: nil,
            cancel: CancelToken())
        session.closeBase(handle: liveHandle)
        XCTAssertEqual(live.rows.map(\.filePath), ["Projects/Alpha.md"])

        let path = "Queries/\(baseName).base"
        try session.saveQueryAsBase(queryJson: queryJSON, path: path)
        let savedHandle = try session.openBase(path: path)
        defer { session.closeBase(handle: savedHandle) }
        let saved = try session.baseExecute(
            handle: savedHandle,
            view: 0,
            thisPath: nil,
            quickFilter: nil,
            cancel: CancelToken())
        XCTAssertEqual(saved.rows.map(\.filePath), live.rows.map(\.filePath))

        let reopened = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: savedHandle, view: 0))
        guard reopened.rows.count == 1,
            case .condition(let condition) = reopened.rows[0]
        else {
            return XCTFail("\(op) must reopen as one structured condition")
        }
        XCTAssertEqual(condition.property, .file(.file))
        XCTAssertEqual(condition.op, op)
        XCTAssertEqual(condition.value, value)
    }

    func testRootFolderSourceKeepsFileHasTagAsStructuredFilterRow() throws {
        var draft = BaseQueryBuilderDraft()
        draft.source = .folder("Projects")
        draft.rows = [
            .condition(
                BaseQueryCondition(
                    property: .file(.file),
                    operator: .hasTag,
                    value: .text("project")))
        ]

        let reopened = try BaseQueryBuilderDraft(queryJSON: draft.queryJSON())

        XCTAssertEqual(reopened.source, .folder("Projects"))
        guard reopened.rows.count == 1,
            case .condition(let condition) = reopened.rows[0]
        else {
            return XCTFail("root Folder source must not consume the hasTag filter row")
        }
        XCTAssertEqual(condition.property, .file(.file))
        XCTAssertEqual(condition.op, .hasTag)
        XCTAssertEqual(condition.value, .text("project"))
    }

    func testSavedBaseAndExtractsOnlyFolderAndKeepsFileHasTagStructured() throws {
        let (_, session) = try makeSession()
        let folderExpression = try XCTUnwrap(
            session.validateBaseExpression(source: #"file.inFolder("Projects")"#).exprJson)
        let tagExpression = try XCTUnwrap(
            session.validateBaseExpression(source: #"file.file.hasTag("project")"#).exprJson)
        var root = try Self.jsonObject(BaseQueryBuilderDraft().queryJSON())
        root["filters"] = [
            "And": [
                ["Stmt": try Self.jsonObject(folderExpression)],
                ["Stmt": try Self.jsonObject(tagExpression)],
            ]
        ]

        let reopened = try BaseQueryBuilderDraft(queryJSON: Self.jsonString(root))

        XCTAssertEqual(reopened.source, .folder("Projects"))
        guard reopened.rows.count == 1,
            case .condition(let condition) = reopened.rows[0]
        else {
            return XCTFail("only the first canonical source may be extracted from saved filters")
        }
        XCTAssertEqual(condition.property, .file(.file))
        XCTAssertEqual(condition.op, .hasTag)
        XCTAssertEqual(condition.value, .text("project"))
    }

    func testSavedBaseAndDoesNotExtractCanonicalSourceAfterFirstCondition() throws {
        var draft = BaseQueryBuilderDraft()
        draft.rows = [
            .condition(
                BaseQueryCondition(
                    property: .note("status"),
                    operator: .equals,
                    value: .text("active"))),
            .condition(
                BaseQueryCondition(
                    property: .file(.file),
                    operator: .hasTag,
                    value: .text("project"))),
        ]

        let reopened = try BaseQueryBuilderDraft(queryJSON: draft.queryJSON())

        XCTAssertEqual(reopened.source, .allNotes)
        guard reopened.rows.count == 2,
            case .condition(let status) = reopened.rows[0],
            case .condition(let tag) = reopened.rows[1]
        else {
            return XCTFail("a later canonical-looking filter must remain a structured row")
        }
        XCTAssertEqual(status.property, .note("status"))
        XCTAssertEqual(status.op, .equals)
        XCTAssertEqual(status.value, .text("active"))
        XCTAssertEqual(tag.property, .file(.file))
        XCTAssertEqual(tag.op, .hasTag)
        XCTAssertEqual(tag.value, .text("project"))
    }

    func testFileHasTagRoundTripsThroughJSONEngineAndBaseAsStructuredCondition() throws {
        try assertFileSpecialOperatorRoundTrip(
            .hasTag,
            value: .text("project"),
            baseName: "HasTag")
    }

    func testFileHasLinkRoundTripsThroughJSONEngineAndBaseAsStructuredCondition() throws {
        try assertFileSpecialOperatorRoundTrip(
            .hasLink,
            value: .file("Zeta.md"),
            baseName: "HasLink")
    }

    func testFileMatchesRoundTripsThroughJSONEngineAndBaseAsStructuredCondition() throws {
        try assertFileSpecialOperatorRoundTrip(
            .matches,
            value: .text("Alpha"),
            baseName: "Matches")
    }

    func testFileSpecialMethodsOnNonFileReceiversStayAdvanced() throws {
        let (_, session) = try makeSession()
        let sources = [
            #"status.hasTag("project")"#,
            #"status.hasLink(file("Zeta.md"))"#,
            #"status.matches("Alpha")"#,
        ]

        for source in sources {
            let validation = session.validateBaseExpression(source: source)
            var root = try Self.jsonObject(BaseQueryBuilderDraft().queryJSON())
            root["filters"] = ["Stmt": try Self.jsonObject(XCTUnwrap(validation.exprJson))]

            let decoded = try BaseQueryBuilderDraft(queryJSON: Self.jsonString(root))

            guard case .advanced = decoded.rows.first else {
                XCTFail("\(source) must not become a typed file-special condition")
                continue
            }
        }
    }

    func testListContainsEncodesAndExecutesWithOneScalarNeedle() throws {
        let (_, session) = try makeSession()
        var draft = BaseQueryBuilderDraft()
        draft.rows = [
            .condition(
                BaseQueryCondition(
                    property: .file(.tags),
                    operator: .contains,
                    value: .text("project")))
        ]

        let queryJSON = try draft.queryJSON()
        XCTAssertTrue(queryJSON.contains(#""String":"project""#), queryJSON)
        XCTAssertFalse(queryJSON.contains(#""List""#), queryJSON)
        let handle = try session.openQuery(queryJson: queryJSON, thisPath: nil)
        defer { session.closeBase(handle: handle) }
        let result = try session.baseExecute(
            handle: handle,
            view: 0,
            thisPath: nil,
            quickFilter: nil,
            cancel: CancelToken())
        XCTAssertEqual(result.rows.map(\.filePath), ["Projects/Alpha.md"])
    }

    func testRetargetingToObjectKeepsDisplayedAndEncodedValueAligned() throws {
        var condition = BaseQueryCondition(
            property: .file(.size),
            operator: .equals,
            value: .number(2))

        condition.retarget(
            to: BaseQueryPropertyChoice(property: .file(.properties), kind: .object))

        XCTAssertEqual(condition.value, .text("2"))
        var draft = BaseQueryBuilderDraft()
        draft.rows = [.condition(condition)]
        let queryJSON = try draft.queryJSON()
        XCTAssertTrue(queryJSON.contains(#""String":"2""#), queryJSON)
        XCTAssertFalse(queryJSON.contains(#""Number":2"#), queryJSON)
    }

    func testLoadedIncompatibleStaticConditionBecomesPreservedAdvancedExpression() throws {
        let (_, session) = try makeSession()
        let validation = session.validateBaseExpression(source: #"file.size.contains("large")"#)
        let expression = try XCTUnwrap(validation.exprJson)
        var root = try Self.jsonObject(BaseQueryBuilderDraft().queryJSON())
        root["filters"] = ["Stmt": try Self.jsonObject(expression)]

        let decoded = try BaseQueryBuilderDraft(queryJSON: Self.jsonString(root))

        guard case .advanced(_, let preservedJSON) = decoded.rows.first else {
            return XCTFail("an operator incompatible with a static kind must fail closed")
        }
        XCTAssertNotNil(preservedJSON)
        let reencoded = try Self.jsonObject(decoded.queryJSON())
        XCTAssertEqual(
            reencoded["filters"] as? NSDictionary,
            root["filters"] as? NSDictionary)
    }

    func testApplyingMixedNoteInventoryFailsClosedToAdvancedExpression() throws {
        let (_, session) = try makeSession()
        let validation = session.validateBaseExpression(source: #"mixed.contains("x")"#)
        let expression = try XCTUnwrap(validation.exprJson)
        var root = try Self.jsonObject(BaseQueryBuilderDraft().queryJSON())
        root["filters"] = ["Stmt": try Self.jsonObject(expression)]
        let initial = try BaseQueryBuilderDraft(queryJSON: Self.jsonString(root))
        let model = BaseQueryBuilderModel(draft: initial)

        model.applyPropertyChoices([
            BaseQueryPropertyChoice(
                summary: PropertyKeySummary(
                    key: "mixed",
                    fileCount: 2,
                    valueKinds: ["boolean", "text"]))
        ])

        guard case .advanced(_, let preservedJSON) = model.rows.first else {
            return XCTFail("mixed note kinds must not guess a majority operator family")
        }
        XCTAssertNotNil(preservedJSON)
        XCTAssertFalse(
            Self.hasViewFilterEdit(try model.baseEditsForView(0)),
            "representation-only fail-closed conversion must not rewrite an unchanged filter")
    }

    func testBuilderKeepsCanonicallyEquivalentPropertyIDsDistinct() {
        let composed = "é"
        let decomposed = "e\u{301}"
        let composedProperty = BaseQueryProperty.note(composed)
        let decomposedProperty = BaseQueryProperty.note(decomposed)
        var draft = BaseQueryBuilderDraft()
        draft.rows = [
            .condition(
                BaseQueryCondition(
                    property: composedProperty,
                    operator: .contains,
                    value: .text("x"))),
            .condition(
                BaseQueryCondition(
                    property: decomposedProperty,
                    operator: .contains,
                    value: .text("x"))),
        ]
        draft.columns = [
            BaseQueryColumn(property: composedProperty, displayName: "Composed"),
            BaseQueryColumn(property: decomposedProperty, displayName: "Decomposed"),
        ]
        let model = BaseQueryBuilderModel(draft: draft)

        XCTAssertNotEqual(composedProperty, decomposedProperty)
        XCTAssertEqual(Set([composedProperty, decomposedProperty]).count, 2)
        XCTAssertEqual(model.columnIndex(for: composedProperty), 0)
        XCTAssertEqual(model.columnIndex(for: decomposedProperty), 1)

        model.applyPropertyChoices([
            BaseQueryPropertyChoice(property: composedProperty, kind: .text),
            BaseQueryPropertyChoice(property: decomposedProperty, kind: .number),
        ])

        guard case .condition = model.rows[0], case .advanced = model.rows[1] else {
            return XCTFail("each exact property must receive its own inventory kind")
        }
    }

    func testBuilderStringInventoriesKeepCanonicalUTF8ChoicesDistinct() {
        let composed = "é"
        let decomposed = "e\u{301}"
        let choices = BaseExactStringChoice.make(
            [composed, decomposed], prefix: "test-choice")

        XCTAssertNotEqual(choices[0].id, choices[1].id)
        XCTAssertTrue(choices[0].matches(composed))
        XCTAssertFalse(choices[0].matches(decomposed))
        XCTAssertFalse(choices[1].matches(composed))
        XCTAssertTrue(choices[1].matches(decomposed))
    }

    func testBuilderSaveDiffKeepsCanonicallyEquivalentUTF8Changes() throws {
        let (_, session) = try makeSession()
        let composed = "é"
        let decomposed = "e\u{301}"

        var previousFilter = BaseQueryBuilderDraft()
        previousFilter.rows = [
            .condition(
                BaseQueryCondition(
                    property: .note(composed),
                    operator: .equals,
                    value: .text(composed)))
        ]
        var editedFilter = previousFilter
        editedFilter.rows = [
            .condition(
                BaseQueryCondition(
                    property: .note(decomposed),
                    operator: .equals,
                    value: .text(decomposed)))
        ]
        XCTAssertTrue(
            Self.hasViewFilterEdit(
                try editedFilter.baseEditsForView(0, replacing: previousFilter)))

        var previousGroup = BaseQueryBuilderDraft()
        previousGroup.groupBy = BaseQueryGroupBy(
            property: .note(composed), ascending: true)
        var editedGroup = previousGroup
        editedGroup.groupBy = BaseQueryGroupBy(
            property: .note(decomposed), ascending: true)
        XCTAssertTrue(
            Self.hasViewKeyEdit(
                try editedGroup.baseEditsForView(0, replacing: previousGroup),
                key: "groupBy"))

        // Canonically reordered combining marks have identical UTF-8 lengths,
        // so Core emits the same spans and the JSON strings differ only in the
        // scalar byte order Swift's native equality intentionally collapses.
        let orderedExpressionValue = "a\u{327}\u{301}"
        let reorderedExpressionValue = "a\u{301}\u{327}"
        XCTAssertEqual(orderedExpressionValue, reorderedExpressionValue)
        XCTAssertFalse(
            BaseExactIdentity.matches(orderedExpressionValue, reorderedExpressionValue))
        let composedExpressionSource = "\"\(orderedExpressionValue)\""
        let decomposedExpressionSource = "\"\(reorderedExpressionValue)\""
        let composedExpression = session.validateBaseExpression(source: composedExpressionSource)
        let decomposedExpression = session.validateBaseExpression(source: decomposedExpressionSource)
        let composedExpressionJSON = try XCTUnwrap(composedExpression.exprJson)
        let decomposedExpressionJSON = try XCTUnwrap(decomposedExpression.exprJson)
        XCTAssertEqual(composedExpressionJSON, decomposedExpressionJSON)
        XCTAssertFalse(
            BaseExactIdentity.matches(composedExpressionJSON, decomposedExpressionJSON))
        var previousFormula = BaseQueryBuilderDraft()
        previousFormula.formulas = [
            try BaseQueryFormula(
                name: "label",
                expression: composedExpressionSource,
                expressionJSON: composedExpressionJSON)
        ]
        var editedFormula = previousFormula
        editedFormula.formulas = [
            try BaseQueryFormula(
                name: "label",
                expression: decomposedExpressionSource,
                expressionJSON: decomposedExpressionJSON)
        ]
        XCTAssertTrue(
            try editedFormula.baseEditsForView(0, replacing: previousFormula).contains {
                if case .setFormula(let name, _) = $0 { return name == "label" }
                return false
            })

        var previousDisplay = BaseQueryBuilderDraft()
        previousDisplay.columns = [
            BaseQueryColumn(property: .note("status"), displayName: composed)
        ]
        var editedDisplay = previousDisplay
        editedDisplay.columns[0].displayName = decomposed
        XCTAssertTrue(
            try editedDisplay.baseEditsForView(0, replacing: previousDisplay).contains {
                if case .setDisplayName(let property, let displayName) = $0 {
                    return property == "status"
                        && BaseExactIdentity.matches(displayName, decomposed)
                }
                return false
            })
    }

    func testTasksRowSourcePreservesIndependentUnsupportedQuerySource() throws {
        var root = try Self.jsonObject(BaseQueryBuilderDraft().queryJSON())
        root["row_source"] = "Tasks"
        root["source"] = ["Unsupported": "task plugin scope"]
        let originalSource = try XCTUnwrap(root["source"] as? NSDictionary)

        var draft = try BaseQueryBuilderDraft(queryJSON: Self.jsonString(root))
        XCTAssertEqual(draft.source, .tasks)
        var encoded = try Self.jsonObject(draft.queryJSON())
        XCTAssertEqual(encoded["source"] as? NSDictionary, originalSource)

        draft.rows.append(
            .condition(
                BaseQueryCondition(
                    property: .task(.completed),
                    operator: .equals,
                    value: .bool(false))))
        encoded = try Self.jsonObject(draft.queryJSON())
        XCTAssertEqual(encoded["source"] as? NSDictionary, originalSource)

        draft.source = .allNotes
        encoded = try Self.jsonObject(draft.queryJSON())
        XCTAssertEqual(encoded["source"] as? String, "All")
        XCTAssertEqual(encoded["row_source"] as? String, "Files")
    }

    func testAbsoluteAndRelativeDateValuesRoundTripThroughCanonicalAST() throws {
        var draft = BaseQueryBuilderDraft()
        draft.rows = [
            .condition(
                BaseQueryCondition(
                    property: .file(.mtime),
                    operator: .greaterThanOrEqual,
                    value: .absoluteDate("2026-07-09"))),
            .condition(
                BaseQueryCondition(
                    property: .task(.due),
                    operator: .greaterThanOrEqual,
                    value: .relativeDays(7))),
        ]

        let encoded = try draft.queryJSON()
        XCTAssertTrue(encoded.contains(#""Global":"Date""#), encoded)
        XCTAssertTrue(encoded.contains(#""Global":"Now""#), encoded)
        XCTAssertTrue(encoded.contains(#""Global":"Duration""#), encoded)
        XCTAssertTrue(encoded.contains(#""op":"Gte""#), encoded)

        let decoded = try BaseQueryBuilderDraft(queryJSON: encoded)
        guard case .condition(let absolute) = decoded.rows[0],
            case .condition(let relative) = decoded.rows[1]
        else { return XCTFail("date forms must remain structured") }
        XCTAssertEqual(absolute.value, .absoluteDate("2026-07-09"))
        XCTAssertEqual(relative.value, .relativeDays(7))
        XCTAssertEqual(try decoded.queryJSON(), encoded)
    }

    func testDateOnlyCodecUsesTheSuppliedPickerTimeZoneWithoutDayDrift() throws {
        for identifier in ["America/New_York", "Pacific/Kiritimati"] {
            let timeZone = try XCTUnwrap(TimeZone(identifier: identifier))
            var calendar = Calendar(identifier: .gregorian)
            calendar.timeZone = timeZone
            let noon = try XCTUnwrap(
                calendar.date(
                    from: DateComponents(
                        timeZone: timeZone,
                        year: 2026,
                        month: 7,
                        day: 9,
                        hour: 12)))

            XCTAssertEqual(
                BaseQueryDateCodec.string(from: noon, timeZone: timeZone),
                "2026-07-09")
            let parsed = try XCTUnwrap(
                BaseQueryDateCodec.date(from: "2026-07-09", timeZone: timeZone))
            XCTAssertEqual(
                BaseQueryDateCodec.string(from: parsed, timeZone: timeZone),
                "2026-07-09")
            XCTAssertEqual(calendar.component(.day, from: parsed), 9)
        }
    }

    func testFormulaCompletionUsesPinnedEvaluatorInventoryWithoutRandom() throws {
        XCTAssertTrue(BaseFormulaCompletion.names.contains("if"))
        XCTAssertTrue(BaseFormulaCompletion.names.contains("today"))
        XCTAssertTrue(BaseFormulaCompletion.names.contains("average"))
        XCTAssertFalse(BaseFormulaCompletion.names.contains("random"))
        XCTAssertEqual(BaseFormulaCompletion.inserting("if", into: "i"), "if()")
        XCTAssertEqual(
            BaseFormulaCompletion.inserting("number", into: "sum(value) + num"),
            "sum(value) + number()")
    }

    func testNonFiniteNumericInputUsesFiniteTypedFallback() throws {
        var draft = BaseQueryBuilderDraft()
        draft.rows = ["nan", "inf", "infinity"].map { value in
            .condition(
                BaseQueryCondition(
                    property: .file(.size),
                    operator: .greaterThan,
                    value: .text(value)))
        }

        let reencoded = try draft.queryJSON()

        XCTAssertFalse(reencoded.contains(#""String":"nan""#), reencoded)
        XCTAssertFalse(reencoded.contains(#""String":"inf""#), reencoded)
        XCTAssertFalse(reencoded.contains(#""String":"infinity""#), reencoded)
        XCTAssertFalse(reencoded.localizedCaseInsensitiveContains("NaN"), reencoded)
        XCTAssertFalse(reencoded.localizedCaseInsensitiveContains("Infinity"), reencoded)
    }

    func testNonFiniteEditOnUntypedNumberKeepsPreviousFiniteValue() throws {
        var value = BaseQueryValue.number(2.5)
        value = value.replacingEditingText("nan")
        var draft = BaseQueryBuilderDraft()
        draft.rows = [
            .condition(
                BaseQueryCondition(
                    property: .note("priority"),
                    operator: .greaterThan,
                    value: value))
        ]

        let reencoded = try draft.queryJSON()

        XCTAssertTrue(reencoded.contains(#""Number":2.5"#), reencoded)
        XCTAssertFalse(reencoded.localizedCaseInsensitiveContains("NaN"), reencoded)
    }

    func testNonNowRecentLikeExpressionStaysAdvanced() throws {
        let (_, session) = try makeSession()
        try session.saveText(
            path: "Queries/NotRecent.base",
            contents:
                #"""
                filters: "file.mtime > file.ctime - duration(\"14d\")"
                views:
                  - type: table
                    name: Not Recent
                    order:
                      - file.name
                """#,
            expectedContentHash: nil)

        let handle = try session.openBase(path: "Queries/NotRecent.base")
        let draft = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: handle, view: 0))

        XCTAssertEqual(draft.source, .allNotes)
        guard draft.rows.count == 1 else {
            return XCTFail("expected one advanced row, got \(draft.rows.count)")
        }
        guard case .advanced(let raw, let preservedJSON) = draft.rows[0] else {
            return XCTFail("non-now recent-like expression must stay advanced")
        }
        XCTAssertTrue(raw.contains("Ctime"), raw)
        XCTAssertNotNil(preservedJSON)
    }

    func testSheetRowsRenderCapturedRowsForAccessibilityLabels() throws {
        let source = try Self.sourceFile("Sources/SlateMac/Bases/BaseQueryBuilderSheet.swift")

        XCTAssertFalse(
            source.contains("model.rows[index].accessibilityLabel"),
            "row views must render labels from the captured ForEach row so stale SwiftUI indices cannot trap"
        )
    }

    func testSheetPropertyPickerExposesTypedFileFields() throws {
        let source = try Self.sourceFile("Sources/SlateMac/Bases/BaseQueryBuilderSheet.swift")

        XCTAssertTrue(source.contains("BaseQueryPropertyChoice.fileChoices"), source)
        XCTAssertTrue(source.contains("BaseQueryPropertyChoice.taskChoices"), source)
        XCTAssertTrue(source.contains("conditionControls("), source)
        XCTAssertTrue(source.contains("BaseFormulaCompletion.names"), source)
        XCTAssertTrue(source.contains("DatePicker("), source)
        XCTAssertTrue(source.contains("Stepper("), source)
        XCTAssertTrue(source.contains("Toggle("), source)

        let appStateSource = try Self.sourceFile("Sources/SlateMac/Bases/AppState+Bases.swift")
        XCTAssertTrue(
            appStateSource.contains("func basesLoadPropertyKeys() async -> [PropertyKeySummary]"),
            appStateSource)
        XCTAssertFalse(appStateSource.contains("listPropertyKeys().map(\\.key)"), appStateSource)
    }

    func testSheetExposesCompletionSectionsAndSaveActions() throws {
        let source = try Self.sourceFile("Sources/SlateMac/Bases/BaseQueryBuilderSheet.swift")

        XCTAssertTrue(source.contains("Sort"))
        XCTAssertTrue(source.contains("Group"))
        XCTAssertTrue(source.contains("Columns"))
        XCTAssertTrue(source.contains("View type"))
        XCTAssertTrue(source.contains("Formulas"))
        XCTAssertTrue(source.contains("Live preview"))
        XCTAssertTrue(source.contains("Save to view"))
        XCTAssertTrue(source.contains("Save as .base"))
        XCTAssertTrue(source.contains("Save as saved query"))
        XCTAssertTrue(source.contains("AccessibleDataGrid("))
        XCTAssertTrue(source.contains("Builder preview table"))
        XCTAssertTrue(source.contains("advancedExpressionValidation(rawExpression:"))
        XCTAssertTrue(source.contains("validateAdvancedExpressionInput(value)"))
        XCTAssertTrue(source.contains("validateAdvancedExpressionInput(rawExpression)"))
        XCTAssertTrue(source.contains("Expression invalid"))
        XCTAssertFalse(source.contains("advancedExpressionValidations"))
    }

    func testAppStateOwnsBuilderPreviewAndSaveOrchestration() throws {
        let source = try Self.sourceFile("Sources/SlateMac/Bases/AppState+Bases.swift")

        XCTAssertTrue(source.contains("basesBuilderSchedulePreview"))
        XCTAssertTrue(source.contains("openQuery"))
        XCTAssertTrue(source.contains("session.baseApplyEdits("))
        XCTAssertTrue(source.contains("saveQueryAsBase"))
        XCTAssertTrue(source.contains("saveQuery("))
        XCTAssertTrue(source.contains("baseViewEditQueryJson("), source)
        XCTAssertTrue(source.contains("baseQueryBuilderPreviewCancelToken?.cancel()"), source)
        XCTAssertTrue(source.contains("let cancelToken = CancelToken()"), source)
        XCTAssertTrue(source.contains("cancel: cancelToken"), source)
        XCTAssertFalse(source.contains("cancel: CancelToken()"), source)
        XCTAssertTrue(source.contains("Task.detached"), source)
        XCTAssertTrue(source.contains("baseQueryBuilderPreviewGeneration"), source)

        let appStateSource = try Self.sourceFile("Sources/SlateMac/AppState.swift")
        XCTAssertTrue(appStateSource.contains("baseQueryBuilderPreviewCancelToken"), appStateSource)
        XCTAssertTrue(appStateSource.contains("baseQueryBuilderPreviewGeneration"), appStateSource)
        XCTAssertTrue(appStateSource.contains("baseQueryBuilderPreviewExecutionObserver"), appStateSource)
    }

    func testBuilderReorderKeysAreScopedToFocusedSortAndIncludedColumnRows() throws {
        let source = try Self.sourceFile("Sources/SlateMac/Bases/BaseQueryBuilderSheet.swift")

        XCTAssertTrue(source.contains("@FocusState private var focusedSortRow"))
        XCTAssertTrue(source.contains("@FocusState private var focusedColumnRowID"))
        XCTAssertTrue(source.contains("handleSortReorder("))
        XCTAssertTrue(source.contains("handleColumnReorder("))
        XCTAssertTrue(source.contains("BaseRowReorderCommand"))
        XCTAssertTrue(source.contains("BaseRowReorderCommand.route("))
        XCTAssertTrue(source.contains("isFocused: focusedSortRow == index"))
        XCTAssertTrue(
                source.contains(
                "isFocused: focusedColumnRowID == property.exactIdentityKey"))
        XCTAssertTrue(source.contains(".focused($focusedSortRow, equals: index)"))
        XCTAssertTrue(
                source.contains(
                ".focused($focusedColumnRowID, equals: property.exactIdentityKey)"))
        XCTAssertGreaterThanOrEqual(
            source.components(separatedBy: ".focusable()").count - 1,
            2,
            "both builder row families must be keyboard-focusable")
        XCTAssertTrue(source.contains(".onKeyPress(.upArrow, phases: .down)"))
        XCTAssertTrue(source.contains(".onKeyPress(.downArrow, phases: .down)"))
        XCTAssertTrue(
            source.contains(
                "index: index, direction: .up, modifiers: press.modifiers"))
        XCTAssertTrue(
            source.contains(
                "index: index, direction: .down, modifiers: press.modifiers"))
        XCTAssertTrue(
            source.contains(
                "property: property, direction: .up, modifiers: press.modifiers"))
        XCTAssertTrue(
            source.contains(
                "property: property, direction: .down, modifiers: press.modifiers"))
        XCTAssertTrue(source.contains("retainFocus: { focusedSortRow = $0 }"))
        XCTAssertTrue(
                source.contains(
                "retainFocus: { _ in focusedColumnRowID = property.exactIdentityKey }"))
        XCTAssertGreaterThanOrEqual(
            source.components(
                separatedBy: "announce: { postAccessibilityAnnouncement($0, priority: .medium) }")
                .count - 1,
            2,
            "both builder reorder handlers must announce through the shared funnel")
    }

    func testOptionArrowsReorderFocusedBuilderRowsOnceAndPreserveFocus() throws {
        var sorts = ["status", "priority"]
        var sortMoveCount = 0
        var sortDestination: Int?
        var focusedSortIndex: Int?
        var sortAnnouncements: [String] = []

        let sortHandled = BaseRowReorderCommand.route(
            isFocused: true,
            direction: .up,
            modifiers: .option,
            index: 1,
            count: sorts.count,
            label: "Sort 2",
            move: { destination in
                sortMoveCount += 1
                sortDestination = destination
                sorts.swapAt(1, destination)
            },
            retainFocus: { focusedSortIndex = $0 },
            announce: { sortAnnouncements.append($0) })

        XCTAssertTrue(sortHandled)
        XCTAssertEqual(sorts, ["priority", "status"])
        XCTAssertEqual(sortMoveCount, 1)
        XCTAssertEqual(focusedSortIndex, 0)
        XCTAssertEqual(sortDestination, 0)
        XCTAssertEqual(sortAnnouncements, ["Sort 2 moved up to position 1 of 2."])

        var columns = ["status", "priority"]
        let focusedColumnID = columns[0]
        var columnMoveCount = 0
        var columnDestination: Int?
        var retainedColumnID: String?
        var columnAnnouncements: [String] = []

        let columnHandled = BaseRowReorderCommand.route(
            isFocused: true,
            direction: .down,
            modifiers: .option,
            index: 0,
            count: columns.count,
            label: "status column",
            move: { destination in
                columnMoveCount += 1
                columnDestination = destination
                columns.swapAt(0, destination)
            },
            retainFocus: { _ in retainedColumnID = focusedColumnID },
            announce: { columnAnnouncements.append($0) })

        XCTAssertTrue(columnHandled)
        XCTAssertEqual(columns, ["priority", "status"])
        XCTAssertEqual(columnMoveCount, 1)
        XCTAssertEqual(retainedColumnID, "status")
        XCTAssertEqual(columnDestination, 1)
        XCTAssertEqual(
            columnAnnouncements,
            ["status column moved down to position 2 of 2."])

        var boundaryMoves = 0
        var boundaryFocus: Int?
        var boundaryAnnouncements: [String] = []
        let boundaryHandled = BaseRowReorderCommand.route(
            isFocused: true,
            direction: .up,
            modifiers: .option,
            index: 0,
            count: 2,
            label: "Sort 1",
            move: { _ in boundaryMoves += 1 },
            retainFocus: { boundaryFocus = $0 },
            announce: { boundaryAnnouncements.append($0) })

        XCTAssertTrue(boundaryHandled)
        XCTAssertEqual(boundaryMoves, 0)
        XCTAssertEqual(boundaryFocus, 0)
        XCTAssertEqual(boundaryAnnouncements, ["Sort 1 is already first."])
        var ignoredCallbacks = 0
        let voiceOverHandled = BaseRowReorderCommand.route(
            isFocused: true,
            direction: .down,
            modifiers: [.control, .option],
            index: 0,
            count: 2,
            label: "Sort 1",
            move: { _ in ignoredCallbacks += 1 },
            retainFocus: { _ in ignoredCallbacks += 1 },
            announce: { _ in ignoredCallbacks += 1 })
        XCTAssertFalse(
            voiceOverHandled,
            "Control-Option-Down belongs to VoiceOver Quick Nav")
        let unfocusedHandled = BaseRowReorderCommand.route(
            isFocused: false,
            direction: .down,
            modifiers: .option,
            index: 0,
            count: 2,
            label: "Sort 1",
            move: { _ in ignoredCallbacks += 1 },
            retainFocus: { _ in ignoredCallbacks += 1 },
            announce: { _ in ignoredCallbacks += 1 })
        XCTAssertFalse(unfocusedHandled)
        XCTAssertEqual(ignoredCallbacks, 0)
        XCTAssertNil(BaseRowReorderCommand(direction: .down, modifiers: []))
        XCTAssertNil(
            BaseRowReorderCommand(
                direction: .down,
                modifiers: [.option, .shift]))
        XCTAssertNil(
            BaseRowReorderCommand(
                direction: .down,
                modifiers: [.option, .command]))
    }

    func testBuilderPreviewPublishRequiresCurrentSessionModelTokenAndGeneration() async throws {
        let (_, state, session) = try await makeAppState()
        let result = BasesResultSet(
            columns: [],
            rows: [],
            groups: [],
            summaries: [],
            totalCount: 1,
            shownCount: 1,
            unfilteredShownCount: 1,
            executedAtMs: 0,
            warnings: [],
            viewError: nil,
            audioSummary: "Preview returned 1 row.")

        let currentModel = BaseQueryBuilderModel()
        let currentToken = CancelToken()
        currentModel.previewState = .loading
        state.activeBaseQueryBuilder = currentModel
        state.baseQueryBuilderPreviewCancelToken = currentToken
        let generation = state.baseQueryBuilderPreviewGeneration

        state.basesBuilderPublishPreview(
            result: result,
            for: currentModel,
            session: session,
            cancelToken: currentToken,
            generation: generation)
        XCTAssertEqual(currentModel.previewState, .ready(result))

        let staleTokenModel = BaseQueryBuilderModel()
        let supersedingToken = CancelToken()
        staleTokenModel.previewState = .loading
        state.activeBaseQueryBuilder = staleTokenModel
        state.baseQueryBuilderPreviewCancelToken = supersedingToken

        state.basesBuilderPublishPreview(
            result: result,
            for: staleTokenModel,
            session: session,
            cancelToken: CancelToken(),
            generation: generation)
        XCTAssertEqual(staleTokenModel.previewState, .loading)

        let replacedModel = BaseQueryBuilderModel()
        let replacement = BaseQueryBuilderModel()
        replacedModel.previewState = .loading
        state.activeBaseQueryBuilder = replacement
        state.baseQueryBuilderPreviewCancelToken = supersedingToken

        state.basesBuilderPublishPreview(
            result: result,
            for: replacedModel,
            session: session,
            cancelToken: supersedingToken,
            generation: generation)
        XCTAssertEqual(replacedModel.previewState, .loading)

        replacement.previewState = .loading
        state.activeBaseQueryBuilder = replacement
        state.baseQueryBuilderPreviewCancelToken = supersedingToken
        state.basesBuilderPublishPreview(
            result: result,
            for: replacement,
            session: session,
            cancelToken: supersedingToken,
            generation: generation + 1)
        XCTAssertEqual(replacement.previewState, .loading)

        let (_, staleSession) = try makeSession()
        state.basesBuilderPublishPreview(
            result: result,
            for: replacement,
            session: staleSession,
            cancelToken: supersedingToken,
            generation: generation)
        XCTAssertEqual(replacement.previewState, .loading)

        supersedingToken.cancel()
        replacement.previewState = .loading
        state.activeBaseQueryBuilder = replacement
        state.baseQueryBuilderPreviewCancelToken = supersedingToken

        state.basesBuilderPublishPreviewFailure(
            message: "cancelled",
            for: replacement,
            session: session,
            cancelToken: supersedingToken,
            generation: generation)
        XCTAssertEqual(replacement.previewState, .loading)
    }

    func testBuilderPreviewNativeLifecycleRunsOffMainActorAndClosesHandle() async throws {
        let (_, state, session) = try await makeAppState()
        state.basesNewQuery()
        let model = try XCTUnwrap(state.activeBaseQueryBuilder)
        let (events, continuation) = AsyncStream.makeStream(
            of: BaseQueryBuilderPreviewExecutionEvent.self)
        state.baseQueryBuilderPreviewExecutionObserver = { event in
            continuation.yield(event)
        }

        state.basesBuilderSchedulePreview(delayNanoseconds: 0)
        let previewTask = try XCTUnwrap(state.baseQueryBuilderPreviewTask)
        await previewTask.value
        continuation.finish()

        var recorded: [BaseQueryBuilderPreviewExecutionEvent] = []
        for await event in events { recorded.append(event) }
        XCTAssertEqual(recorded.map(\.phase), [.opened, .executed, .closed])
        XCTAssertTrue(
            recorded.allSatisfy { !$0.ranOnMainThread },
            "openQuery, baseExecute, and closeBase must all execute away from MainActor")
        XCTAssertEqual(
            Self.readyResult(model.previewState)?.rows.map(\.filePath),
            ["Projects/Alpha.md", "Zeta.md"])
        let handle = try XCTUnwrap(recorded.last?.handle)
        XCTAssertThrowsError(try session.baseViews(handle: handle), "the preview handle must be closed")
        state.baseQueryBuilderPreviewExecutionObserver = nil
    }

    func testBaseViewBuilderPreviewPreservesOriginatingThisPath() async throws {
        let (vault, state, _) = try await makeAppState()
        try Data(
            #"""
            views:
              - type: table
                name: Context
                filters: "file.path == this.path"
                order: [file.name]
            """#.utf8
        ).write(to: vault.appendingPathComponent("Queries/Context.base"))

        state.openFile("Queries/Context.base", target: .currentTab)
        state.basesEditViewFilters()
        let model = try XCTUnwrap(state.activeBaseQueryBuilder)

        XCTAssertEqual(model.previewThisPath, "Queries/Context.base")
        XCTAssertNotNil(model.editingBaseView)

        state.basesBuilderSchedulePreview(delayNanoseconds: 0)
        await state.baseQueryBuilderPreviewTask?.value

        guard case .ready = model.previewState else {
            return XCTFail("base-context preview failed: \(model.previewState)")
        }
    }

    func testContextlessBuilderPreviewSurfacesEngineViewError() async throws {
        let (_, state, session) = try await makeAppState()
        state.basesNewQuery()
        let model = try XCTUnwrap(state.activeBaseQueryBuilder)
        let expressionJSON = try XCTUnwrap(
            session.validateBaseExpression(source: "file.hasLink(this)").exprJson)
        model.rows = [
            .advanced(
                rawExpression: "file.hasLink(this)",
                filterJSON: #"{"Stmt":\#(expressionJSON)}"#)
        ]

        state.basesBuilderSchedulePreview(delayNanoseconds: 0)
        await state.baseQueryBuilderPreviewTask?.value

        guard case .failed(let message) = model.previewState else {
            return XCTFail("contextless engine error must fail loud: \(model.previewState)")
        }
        XCTAssertTrue(message.contains("this is unavailable in this evaluation context"), message)

        let source = try Self.sourceFile("Sources/SlateMac/Bases/BaseQueryBuilderSheet.swift")
        XCTAssertTrue(source.contains("Preview error"), source)
    }

    func testSavedQueryTabRoutesBuilderToUpdateTargetNotViewSplice() async throws {
        let (_, state, session) = try await makeAppState()
        let id = try session.saveQuery(
            name: "Open saved query",
            description: nil,
            queryJson: BaseQueryBuilderDraft().queryJSON(),
            sourceSyntax: .builder)
        state.openSavedQuery(id: id, name: "Open saved query")

        state.basesEditViewFilters()

        let model = try XCTUnwrap(state.activeBaseQueryBuilder)
        XCTAssertEqual(model.editingSavedQuery?.id, id)
        XCTAssertNil(model.editingBaseView)
        XCTAssertNil(model.previewThisPath)
    }

    func testSupersededPreviewCancelsNativeTokenClosesOldHandleAndCannotPublishStaleResult()
        async throws
    {
        let (_, state, session) = try await makeAppState()
        state.basesNewQuery()
        let model = try XCTUnwrap(state.activeBaseQueryBuilder)
        let oldGeneration = state.baseQueryBuilderPreviewGeneration + 1
        let oldGate = BasePreviewTestGate()
        let (events, continuation) = AsyncStream.makeStream(
            of: BaseQueryBuilderPreviewExecutionEvent.self)
        state.baseQueryBuilderPreviewExecutionObserver = { event in
            continuation.yield(event)
            if event.phase == .executed, event.generation == oldGeneration {
                await oldGate.wait()
            }
        }
        var iterator = events.makeAsyncIterator()

        state.basesBuilderSchedulePreview(delayNanoseconds: 0)
        let oldTask = try XCTUnwrap(state.baseQueryBuilderPreviewTask)
        let oldToken = try XCTUnwrap(state.baseQueryBuilderPreviewCancelToken)
        let firstEvent = await iterator.next()
        let oldOpened = try XCTUnwrap(firstEvent)
        XCTAssertEqual(oldOpened.phase, .opened)
        XCTAssertEqual(oldOpened.generation, oldGeneration)
        let secondEvent = await iterator.next()
        let oldExecuted = try XCTUnwrap(secondEvent)
        XCTAssertEqual(oldExecuted.phase, .executed)
        XCTAssertEqual(oldExecuted.generation, oldGeneration)

        model.source = .folder("Projects")
        state.basesBuilderSchedulePreview(delayNanoseconds: 0)
        let newTask = try XCTUnwrap(state.baseQueryBuilderPreviewTask)
        let newGeneration = state.baseQueryBuilderPreviewGeneration
        var newEvents: [BaseQueryBuilderPreviewExecutionEvent] = []
        while newEvents.last?.phase != .closed {
            let nextEvent = await iterator.next()
            let event = try XCTUnwrap(nextEvent)
            if event.generation == newGeneration { newEvents.append(event) }
        }
        await newTask.value

        XCTAssertTrue(oldTask.isCancelled, "superseding must cancel the Swift task")
        XCTAssertTrue(oldToken.isCancelled(), "superseding must cancel the Rust token")
        XCTAssertEqual(newEvents.map(\.phase), [.opened, .executed, .closed])
        XCTAssertEqual(
            Self.readyResult(model.previewState)?.rows.map(\.filePath),
            ["Projects/Alpha.md"])

        await oldGate.release()
        var oldClosed: BaseQueryBuilderPreviewExecutionEvent?
        while oldClosed == nil {
            let nextEvent = await iterator.next()
            let event = try XCTUnwrap(nextEvent)
            if event.generation == oldGeneration, event.phase == .closed { oldClosed = event }
        }
        await oldTask.value
        continuation.finish()

        XCTAssertEqual(
            Self.readyResult(model.previewState)?.rows.map(\.filePath),
            ["Projects/Alpha.md"],
            "the older all-files result must not overwrite the newer folder result")
        XCTAssertThrowsError(try session.baseViews(handle: oldOpened.handle))
        XCTAssertThrowsError(try session.baseViews(handle: try XCTUnwrap(oldClosed).handle))
        state.baseQueryBuilderPreviewExecutionObserver = nil
    }

    func testStalePreviewCompletionCannotClearNewGenerationToken() async throws {
        let (_, state, _) = try await makeAppState()
        state.basesNewQuery()
        let model = try XCTUnwrap(state.activeBaseQueryBuilder)
        let oldGeneration = state.baseQueryBuilderPreviewGeneration + 1
        let newGeneration = oldGeneration + 1
        let oldGate = BasePreviewTestGate()
        let newGate = BasePreviewTestGate()
        let (events, continuation) = AsyncStream.makeStream(
            of: BaseQueryBuilderPreviewExecutionEvent.self)
        state.baseQueryBuilderPreviewExecutionObserver = { event in
            continuation.yield(event)
            if event.generation == oldGeneration, event.phase == .executed {
                await oldGate.wait()
            } else if event.generation == newGeneration, event.phase == .opened {
                await newGate.wait()
            }
        }
        var iterator = events.makeAsyncIterator()

        state.basesBuilderSchedulePreview(delayNanoseconds: 0)
        let oldTask = try XCTUnwrap(state.baseQueryBuilderPreviewTask)
        let oldOpened = await iterator.next()
        XCTAssertEqual(oldOpened?.phase, .opened)
        let oldExecuted = await iterator.next()
        XCTAssertEqual(oldExecuted?.phase, .executed)

        model.source = .folder("Projects")
        state.basesBuilderSchedulePreview(delayNanoseconds: 0)
        let newTask = try XCTUnwrap(state.baseQueryBuilderPreviewTask)
        let newToken = try XCTUnwrap(state.baseQueryBuilderPreviewCancelToken)
        let nextNewEvent = await iterator.next()
        let newOpened = try XCTUnwrap(nextNewEvent)
        XCTAssertEqual(newOpened.generation, newGeneration)
        XCTAssertEqual(newOpened.phase, .opened)

        await oldGate.release()
        let nextOldEvent = await iterator.next()
        let oldClosed = try XCTUnwrap(nextOldEvent)
        XCTAssertEqual(oldClosed.generation, oldGeneration)
        XCTAssertEqual(oldClosed.phase, .closed)
        await oldTask.value

        XCTAssertTrue(state.baseQueryBuilderPreviewCancelToken === newToken)
        XCTAssertFalse(newToken.isCancelled())
        XCTAssertEqual(state.baseQueryBuilderPreviewGeneration, newGeneration)

        await newGate.release()
        let newExecuted = await iterator.next()
        XCTAssertEqual(newExecuted?.phase, .executed)
        let newClosed = await iterator.next()
        XCTAssertEqual(newClosed?.phase, .closed)
        await newTask.value
        continuation.finish()

        XCTAssertNil(state.baseQueryBuilderPreviewCancelToken)
        state.baseQueryBuilderPreviewExecutionObserver = nil
    }

    func testPreviewGenerationAdvancesForScheduleBuilderCloseAndVaultClose() async throws {
        let (_, state, _) = try await makeAppState()
        state.basesNewQuery()
        let initial = state.baseQueryBuilderPreviewGeneration

        state.basesBuilderSchedulePreview(delayNanoseconds: 10_000_000_000)
        XCTAssertEqual(state.baseQueryBuilderPreviewGeneration, initial + 1)
        state.basesCloseQueryBuilder()
        XCTAssertEqual(state.baseQueryBuilderPreviewGeneration, initial + 2)
        state.closeVault()
        XCTAssertEqual(state.baseQueryBuilderPreviewGeneration, initial + 3)
    }

    func testDirectBuilderSheetDismissalInvalidatesAndCancelsPreview() async throws {
        let (_, state, _) = try await makeAppState()
        state.basesNewQuery()
        state.basesBuilderSchedulePreview(delayNanoseconds: 10_000_000_000)
        let scheduledGeneration = state.baseQueryBuilderPreviewGeneration
        let task = try XCTUnwrap(state.baseQueryBuilderPreviewTask)
        let token = try XCTUnwrap(state.baseQueryBuilderPreviewCancelToken)

        state.activeBaseQueryBuilder = nil

        XCTAssertEqual(state.baseQueryBuilderPreviewGeneration, scheduledGeneration + 1)
        XCTAssertTrue(task.isCancelled, "interactive sheet dismissal must cancel the Swift task")
        XCTAssertTrue(token.isCancelled(), "interactive sheet dismissal must cancel the Rust token")
        XCTAssertNil(state.baseQueryBuilderPreviewTask)
        XCTAssertNil(state.baseQueryBuilderPreviewCancelToken)
    }

    func testReplacingBuilderInvalidatesOldPreviewWithoutClearingReplacement() async throws {
        let (_, state, _) = try await makeAppState()
        state.basesNewQuery()
        state.basesBuilderSchedulePreview(delayNanoseconds: 10_000_000_000)
        let scheduledGeneration = state.baseQueryBuilderPreviewGeneration
        let task = try XCTUnwrap(state.baseQueryBuilderPreviewTask)
        let token = try XCTUnwrap(state.baseQueryBuilderPreviewCancelToken)
        let replacement = BaseQueryBuilderModel()

        state.activeBaseQueryBuilder = replacement

        XCTAssertTrue(state.activeBaseQueryBuilder === replacement)
        XCTAssertEqual(state.baseQueryBuilderPreviewGeneration, scheduledGeneration + 1)
        XCTAssertTrue(task.isCancelled)
        XCTAssertTrue(token.isCancelled())
        XCTAssertNil(state.baseQueryBuilderPreviewTask)
        XCTAssertNil(state.baseQueryBuilderPreviewCancelToken)
    }

    func testOrFolderFilterDoesNotBecomeSourcePickerScope() throws {
        let (_, session) = try makeSession()
        try session.saveText(
            path: "Queries/OrSource.base",
            contents:
                #"""
                filters:
                  or:
                    - "file.inFolder(\"Projects\")"
                    - "status == \"active\""
                views:
                  - type: table
                    name: Existing
                    order:
                      - file.name
                """#,
            expectedContentHash: nil)

        let handle = try session.openBase(path: "Queries/OrSource.base")
        let queryJSON = try session.baseViewQueryJson(handle: handle, view: 0)
        let draft = try BaseQueryBuilderDraft(queryJSON: queryJSON)

        XCTAssertEqual(draft.source, .allNotes)
        XCTAssertEqual(draft.combinator, .any)
        XCTAssertEqual(draft.rows.count, 2)
        guard case .advanced = draft.rows[0] else {
            return XCTFail("folder filters under OR cannot be safely promoted to a source")
        }
        XCTAssertEqual(
            draft.rows[1].accessibilityLabel(index: 1),
            "Condition 2: status equals active")
    }

    func testCompletionFacetsEncodeAndRoundTripThroughSaveAsBase() throws {
        let (vault, session) = try makeSession()
        let scoreValidation = session.validateBaseExpression(source: "number(priority) * 2")
        XCTAssertTrue(scoreValidation.valid)
        let scoreExpressionJSON = try XCTUnwrap(scoreValidation.exprJson)

        var draft = BaseQueryBuilderDraft()
        draft.viewType = .list
        draft.groupBy = BaseQueryGroupBy(property: .note("status"), ascending: true)
        draft.sortKeys = [BaseQuerySortKey(property: .note("priority"), ascending: false)]
        draft.formulas = [
            try BaseQueryFormula(
                name: "score",
                expression: "number(priority) * 2",
                expressionJSON: scoreExpressionJSON)
        ]
        draft.columns = [
            BaseQueryColumn(property: .file(.name), displayName: "Title"),
            BaseQueryColumn(property: .note("status"), displayName: nil),
            BaseQueryColumn(property: .formula("score"), displayName: "Score"),
        ]

        let queryJSON = try draft.queryJSON()
        try session.saveQueryAsBase(queryJson: queryJSON, path: "Queries/Complete.base")
        let saved = try String(
            contentsOf: vault.appendingPathComponent("Queries/Complete.base"),
            encoding: .utf8)

        XCTAssertTrue(saved.contains("type: list"), saved)
        XCTAssertTrue(saved.contains("groupBy:"), saved)
        XCTAssertTrue(saved.contains("property: status"), saved)
        XCTAssertTrue(saved.contains("direction: ASC"), saved)
        XCTAssertTrue(saved.contains("formula.score"), saved)
        XCTAssertTrue(saved.contains("displayName: Score"), saved)
        XCTAssertTrue(saved.contains("slate:"), saved)
        XCTAssertTrue(saved.contains("direction: desc"), saved)

        let handle = try session.openBase(path: "Queries/Complete.base")
        let decoded = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: handle, view: 0))
        XCTAssertEqual(decoded.viewType, .list)
        XCTAssertEqual(decoded.groupBy, BaseQueryGroupBy(property: .note("status"), ascending: true))
        XCTAssertEqual(decoded.sortKeys, [
            BaseQuerySortKey(property: .note("priority"), ascending: false)
        ])
        XCTAssertEqual(decoded.columns.map(\.id), ["file.name", "status", "formula.score"])
        XCTAssertEqual(decoded.columns[0].displayName, "Title")
        XCTAssertEqual(decoded.columns[2].displayName, "Score")
        XCTAssertEqual(decoded.formulas.map(\.name), ["score"])
    }

    func testCompletedDraftAppliesToExistingViewAndSavedQuery() throws {
        let (_, session) = try makeSession()
        try session.saveText(
            path: "Queries/Editable.base",
            contents:
                #"""
                views:
                  - type: table
                    name: Editable
                    order:
                      - file.name
                """#,
            expectedContentHash: nil)
        let scoreValidation = session.validateBaseExpression(source: "number(priority) + 1")
        let scoreExpressionJSON = try XCTUnwrap(scoreValidation.exprJson)

        var draft = BaseQueryBuilderDraft()
        draft.viewType = .list
        draft.groupBy = BaseQueryGroupBy(property: .note("status"), ascending: false)
        draft.sortKeys = [BaseQuerySortKey(property: .formula("score"), ascending: false)]
        draft.formulas = [
            try BaseQueryFormula(
                name: "score",
                expression: "number(priority) + 1",
                expressionJSON: scoreExpressionJSON)
        ]
        draft.columns = [
            BaseQueryColumn(property: .file(.name), displayName: nil),
            BaseQueryColumn(property: .formula("score"), displayName: "Score"),
        ]

        let handle = try session.openBase(path: "Queries/Editable.base")
        for edit in try draft.baseEditsForView(0) {
            try session.baseApplyEdit(handle: handle, edit: edit)
        }
        let reopened = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: handle, view: 0))
        XCTAssertEqual(reopened.viewType, .list)
        XCTAssertEqual(reopened.groupBy, BaseQueryGroupBy(property: .note("status"), ascending: false))
        XCTAssertEqual(reopened.columns.map(\.id), ["file.name", "formula.score"])
        XCTAssertEqual(reopened.columns[1].displayName, "Score")
        XCTAssertEqual(reopened.sortKeys, [
            BaseQuerySortKey(property: .formula("score"), ascending: false)
        ])

        let savedID = try session.saveQuery(
            name: "Complete Query",
            description: "Built from the accessible query builder.",
            queryJson: draft.queryJSON(),
            sourceSyntax: .builder)
        let saved = try session.getSavedQuery(id: savedID)
        XCTAssertEqual(saved.name, "Complete Query")
        XCTAssertEqual(saved.sourceSyntax, .builder)
        XCTAssertNil(saved.warning)
    }

    func testCompletedDraftWrapsMultiClauseFiltersWhenSavingToView() throws {
        let (vault, session) = try makeSession()
        try session.saveText(
            path: "Queries/Filters.base",
            contents:
                #"""
                views:
                  - type: table
                    name: Filters
                    order:
                      - file.name
                """#,
            expectedContentHash: nil)

        var draft = BaseQueryBuilderDraft()
        draft.source = .folder("Projects")
        draft.rows = [
            .condition(
                BaseQueryCondition(
                    property: .note("status"),
                    operator: .equals,
                    value: .text("active")))
        ]

        let handle = try session.openBase(path: "Queries/Filters.base")
        for edit in try draft.baseEditsForView(0) {
            try session.baseApplyEdit(handle: handle, edit: edit)
        }

        let saved = try String(
            contentsOf: vault.appendingPathComponent("Queries/Filters.base"),
            encoding: .utf8)
        XCTAssertFalse(saved.contains("filters: and:"), saved)
        XCTAssertTrue(saved.contains("    filters:\n      and:"), saved)
        XCTAssertTrue(saved.contains("file.inFolder"), saved)
        XCTAssertTrue(saved.contains("status == \\\"active\\\""), saved)

        let reopened = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: handle, view: 0))
        XCTAssertEqual(reopened.source, .folder("Projects"))
        XCTAssertEqual(reopened.rows.count, 1)
    }

    func testCompletedDraftDoesNotRewriteUnchangedComplexFormula() throws {
        let (vault, session) = try makeSession()
        try session.saveText(
            path: "Queries/ComplexFormula.base",
            contents:
                #"""
                formulas:
                  neg: "-priority"
                views:
                  - type: table
                    name: Complex
                    order:
                      - file.name
                      - formula.neg
                """#,
            expectedContentHash: nil)

        let handle = try session.openBase(path: "Queries/ComplexFormula.base")
        let previous = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: handle, view: 0))
        var draft = previous
        draft.viewType = .list

        for edit in try draft.baseEditsForView(0, replacing: previous) {
            try session.baseApplyEdit(handle: handle, edit: edit)
        }

        let saved = try String(
            contentsOf: vault.appendingPathComponent("Queries/ComplexFormula.base"),
            encoding: .utf8)
        XCTAssertTrue(saved.contains("  neg: \"-priority\""), saved)
        XCTAssertFalse(saved.contains(#""kind":"#), saved)
        XCTAssertTrue(saved.contains("  - type: list"), saved)
    }

    func testComplexAdvancedSortSkipsUnrelatedSaveAndPreservesASTWhenDirectionChanges() throws {
        let (_, session) = try makeSession()
        try session.saveText(
            path: "Queries/ComplexSort.base",
            contents:
                #"""
                views:
                  - type: table
                    name: Complex sort
                    order:
                      - file.name
                    slate:
                      sort:
                        - expr: "(a + b) * c"
                          direction: asc
                """#,
            expectedContentHash: nil)

        let handle = try session.openBase(path: "Queries/ComplexSort.base")
        let previous = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewEditQueryJson(handle: handle, view: 0))
        XCTAssertEqual(previous.sortKeys.count, 1)
        let originalExpression = try Self.jsonObject(
            XCTUnwrap(previous.sortKeys.first?.expressionJSON))

        var filterEdited = previous
        filterEdited.rows.append(
            .condition(
                BaseQueryCondition(
                    property: .file(.name),
                    operator: .contains,
                    value: .text("Alpha"))))
        let unrelatedEdits = try filterEdited.baseEditsForView(0, replacing: previous)
        XCTAssertFalse(
            Self.hasSlateSortEdit(unrelatedEdits),
            "an unrelated filter edit must not rewrite advanced sort source")
        for edit in unrelatedEdits {
            try session.baseApplyEdit(handle: handle, edit: edit)
        }

        var sortEdited = filterEdited
        sortEdited.sortKeys[0].ascending = false
        let sortEdits = try sortEdited.baseEditsForView(0, replacing: filterEdited)
        XCTAssertTrue(Self.hasSlateSortEdit(sortEdits))
        XCTAssertFalse(Self.hasSlateStateEdit(sortEdits))
        for edit in sortEdits {
            try session.baseApplyEdit(handle: handle, edit: edit)
        }

        let reopened = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: handle, view: 0))
        XCTAssertEqual(reopened.sortKeys.count, 1)
        XCTAssertFalse(try XCTUnwrap(reopened.sortKeys.first).ascending)
        let reopenedExpression = try Self.jsonObject(
            XCTUnwrap(reopened.sortKeys.first?.expressionJSON))
        XCTAssertEqual(
            try Self.semanticJSON(reopenedExpression),
            try Self.semanticJSON(originalExpression))
    }

    func testBuilderSortSavePreservesUnknownSlateStateCommentsAndFormatting() throws {
        let (vault, session) = try makeSession()
        let source =
            #"""
            views:
              - type: table
                name: Preserve
                order: [file.name, status]
                slate:
                  pluginState: {opaque: keep} # keep plugin formatting
                  listMarker: dash
                  secondaryProperties:
                    - status
                  # keep comment before sort
                  sort:
                    - expr: file.name
                      direction: asc
                  # keep comment after sort
            """#
        try session.saveText(
            path: "Queries/BuilderPreserve.base",
            contents: source,
            expectedContentHash: nil)
        let handle = try session.openBase(path: "Queries/BuilderPreserve.base")
        let previous = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewEditQueryJson(handle: handle, view: 0))
        var edited = previous
        edited.sortKeys = [BaseQuerySortKey(property: .note("status"), ascending: false)]

        let edits = try edited.baseEditsForView(0, replacing: previous)
        XCTAssertEqual(edits.count, 1)
        XCTAssertTrue(Self.hasSlateSortEdit(edits))
        XCTAssertFalse(Self.hasSlateStateEdit(edits))
        try session.baseApplyEdits(handle: handle, edits: edits)

        let saved = try String(
            contentsOf: vault.appendingPathComponent("Queries/BuilderPreserve.base"),
            encoding: .utf8)
        XCTAssertEqual(
            saved,
            #"""
            views:
              - type: table
                name: Preserve
                order: [file.name, status]
                slate:
                  pluginState: {opaque: keep} # keep plugin formatting
                  listMarker: dash
                  secondaryProperties:
                    - status
                  # keep comment before sort
                  sort:
                    - expr: status
                      direction: desc
                  # keep comment after sort
            """#)
    }

    func testBuilderClearOnlySortRemovesEmptySlateParent() throws {
        let (vault, session) = try makeSession()
        let source =
            #"""
            views:
              - type: table
                name: Clear sort
                order: [file.name, status]
                # before slate
                slate:
                  sort:
                    - expr: status
                      direction: desc
                # after slate
            """#
        try session.saveText(
            path: "Queries/ClearOnlySort.base",
            contents: source,
            expectedContentHash: nil)
        let handle = try session.openBase(path: "Queries/ClearOnlySort.base")
        let previous = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewEditQueryJson(handle: handle, view: 0))
        var edited = previous
        edited.sortKeys = []

        let edits = try edited.baseEditsForView(0, replacing: previous)
        XCTAssertEqual(edits.count, 1)
        XCTAssertTrue(Self.hasSlateSortEdit(edits))
        try session.baseApplyEdits(handle: handle, edits: edits)

        let saved = try String(
            contentsOf: vault.appendingPathComponent("Queries/ClearOnlySort.base"),
            encoding: .utf8)
        XCTAssertEqual(
            saved,
            #"""
            views:
              - type: table
                name: Clear sort
                order: [file.name, status]
                # before slate
                # after slate
            """#)
    }

    func testDirectSaveSortPreservesUnknownSlateStateCommentsAndFormatting() throws {
        let (vault, session) = try makeSession()
        let source =
            #"""
            views:
              - type: table
                name: Preserve
                order: [file.name, status]
                slate:
                  pluginState: {opaque: keep} # keep plugin formatting
                  listMarker: dash
                  secondaryProperties:
                    - status
                  # keep comment before sort
                  sort:
                    - property: file.name
                      direction: ASC
                  # keep comment after sort
            """#
        try session.saveText(
            path: "Queries/DirectPreserve.base",
            contents: source,
            expectedContentHash: nil)
        let document = BaseDocument(path: "Queries/DirectPreserve.base")
        document.load(session: session)
        let result = try XCTUnwrap(document.result)
        let statusColumn = try XCTUnwrap(result.columns.firstIndex { $0.id == "status" })
        XCTAssertTrue(
            document.setTransientSort(
                DataGridSortState(columnIndex: statusColumn, ascending: false),
                session: session))

        XCTAssertNotNil(try document.saveSortToView(session: session))

        let saved = try String(
            contentsOf: vault.appendingPathComponent("Queries/DirectPreserve.base"),
            encoding: .utf8)
        XCTAssertEqual(
            saved,
            #"""
            views:
              - type: table
                name: Preserve
                order: [file.name, status]
                slate:
                  pluginState: {opaque: keep} # keep plugin formatting
                  listMarker: dash
                  secondaryProperties:
                    - status
                  # keep comment before sort
                  sort:
                    - property: "status"
                      direction: DESC
                  # keep comment after sort
            """#)
    }

    func testOrderSpliceTracksEffectiveColumnIDsOnly() throws {
        var previous = BaseQueryBuilderDraft()
        previous.columns = [
            BaseQueryColumn(property: .file(.name), displayName: "Title"),
            BaseQueryColumn(property: .note("status"), displayName: nil),
        ]

        XCTAssertFalse(
            Self.hasViewKeyEdit(
                try previous.baseEditsForView(0, replacing: previous),
                key: "order"))

        var filterEdited = previous
        filterEdited.rows.append(
            .condition(
                BaseQueryCondition(
                    property: .note("status"),
                    operator: .equals,
                    value: .text("active"))))
        XCTAssertFalse(
            Self.hasViewKeyEdit(
                try filterEdited.baseEditsForView(0, replacing: previous),
                key: "order"),
            "an unrelated filter edit must preserve authored order syntax")

        var reordered = filterEdited
        reordered.columns.swapAt(0, 1)
        XCTAssertTrue(
            Self.hasViewKeyEdit(
                try reordered.baseEditsForView(0, replacing: filterEdited),
                key: "order"))

        let filesDefault = BaseQueryBuilderDraft()
        var tasksDefault = filesDefault
        tasksDefault.source = .tasks
        XCTAssertTrue(
            Self.hasViewKeyEdit(
                try tasksDefault.baseEditsForView(0, replacing: filesDefault),
                key: "order"),
            "source-dependent empty-column defaults must still update order")
    }

    @MainActor
    func testRemovingFormulaPrunesHiddenBuilderReferences() throws {
        let (_, session) = try makeSession()
        let validation = session.validateBaseExpression(source: "number(priority)")
        let formula = try BaseQueryFormula(
            name: "score",
            expression: "number(priority)",
            expressionJSON: XCTUnwrap(validation.exprJson))
        let model = BaseQueryBuilderModel()
        model.formulas = [formula]
        model.columns = [BaseQueryColumn(property: .formula("score"), displayName: nil)]
        model.sortKeys = [BaseQuerySortKey(property: .formula("score"), ascending: false)]
        model.groupBy = BaseQueryGroupBy(property: .formula("score"), ascending: true)

        model.removeFormula(named: "score")

        XCTAssertTrue(model.formulas.isEmpty)
        XCTAssertTrue(model.columns.isEmpty)
        XCTAssertTrue(model.sortKeys.isEmpty)
        XCTAssertNil(model.groupBy)
    }

    @MainActor
    func testRemovingFormulaTargetsExactUTF8Name() throws {
        let (_, session) = try makeSession()
        let composed = "é"
        let decomposed = "e\u{301}"
        let expressionJSON = try XCTUnwrap(
            session.validateBaseExpression(source: "number(priority)").exprJson)
        let model = BaseQueryBuilderModel()
        model.formulas = [
            try BaseQueryFormula(
                name: composed,
                expression: "number(priority)",
                expressionJSON: expressionJSON),
            try BaseQueryFormula(
                name: decomposed,
                expression: "number(priority)",
                expressionJSON: expressionJSON),
        ]
        model.columns = [
            BaseQueryColumn(property: .formula(composed), displayName: nil),
            BaseQueryColumn(property: .formula(decomposed), displayName: nil),
        ]
        model.sortKeys = [
            BaseQuerySortKey(property: .formula(composed), ascending: true),
            BaseQuerySortKey(property: .formula(decomposed), ascending: false),
        ]
        model.groupBy = BaseQueryGroupBy(property: .formula(composed), ascending: true)

        model.removeFormula(named: decomposed)

        XCTAssertEqual(model.formulas.count, 1)
        XCTAssertTrue(BaseExactIdentity.matches(try XCTUnwrap(model.formulas.first?.name), composed))
        XCTAssertEqual(model.columns.count, 1)
        XCTAssertTrue(BaseExactIdentity.matches(try XCTUnwrap(model.columns.first?.id), "formula.\(composed)"))
        XCTAssertEqual(model.sortKeys.count, 1)
        XCTAssertEqual(model.sortKeys.first?.property, .formula(composed))
        XCTAssertEqual(model.groupBy?.property, .formula(composed))
    }

    @MainActor
    func testRemovingFormulaPrunesAdvancedSortExpressionReferences() throws {
        let (_, session) = try makeSession()
        let formulaValidation = session.validateBaseExpression(source: "number(priority)")
        let formulaExpressionJSON = try XCTUnwrap(formulaValidation.exprJson)
        let sortValidation = session.validateBaseExpression(source: "formula.score + 1")
        let sortExpressionJSON = try XCTUnwrap(sortValidation.exprJson)
        let draft = try BaseQueryBuilderDraft(
            queryJSON:
                """
                {
                  "columns": [{"display_name": null, "id": "file.name"}],
                  "custom_summaries": [],
                  "filters": null,
                  "formulas": [["score", \(formulaExpressionJSON)]],
                  "group_by": null,
                  "limit": null,
                  "row_source": "Files",
                  "sort": [{"ascending": true, "expr": \(sortExpressionJSON)}],
                  "source": "All",
                  "summaries": [],
                  "view": {"Table": {"fallback_from": null}}
                }
                """)
        let model = BaseQueryBuilderModel(draft: draft)

        XCTAssertEqual(model.sortKeys.count, 1)
        XCTAssertNil(model.sortKeys.first?.property)

        model.removeFormula(named: "score")

        XCTAssertTrue(model.formulas.isEmpty)
        XCTAssertTrue(model.sortKeys.isEmpty)
    }

    func testSuccessfulSaveRebasePreventsReplayingRemovedFormulaEdit() throws {
        let (_, session) = try makeSession()
        let validation = session.validateBaseExpression(source: "number(priority)")
        var draft = BaseQueryBuilderDraft()
        draft.formulas = [
            try BaseQueryFormula(
                name: "score",
                expression: "number(priority)",
                expressionJSON: XCTUnwrap(validation.exprJson))
        ]
        let model = BaseQueryBuilderModel(draft: draft)
        model.removeFormula(named: "score")

        XCTAssertTrue(
            Self.hasRemoveFormulaEdit(try model.baseEditsForView(0), named: "score"))

        model.rebaseAfterSuccessfulSave()

        XCTAssertFalse(
            Self.hasRemoveFormulaEdit(try model.baseEditsForView(0), named: "score"),
            "a successful save must make the saved draft the next comparison baseline")
    }

    func testSaveToViewUsesOneBatchAndRebasesBeforeARepeatedSave() async throws {
        let (vault, state, _) = try await makeAppState()
        let baseURL = vault.appendingPathComponent("Queries/Formula.base")
        try Data(
            #"""
            formulas:
              score: "number(priority)"
            views:
              - type: table
                name: Formula
                order:
                  - file.name
                  - formula.score
            """#.utf8
        ).write(to: baseURL)

        state.openFile("Queries/Formula.base", target: .currentTab)
        state.basesEditViewFilters()
        let model = try XCTUnwrap(state.activeBaseQueryBuilder)
        model.removeFormula(named: "score")
        XCTAssertTrue(
            Self.hasRemoveFormulaEdit(try model.baseEditsForView(0), named: "score"))

        state.basesBuilderSaveToView()

        XCTAssertFalse(
            Self.hasRemoveFormulaEdit(try model.baseEditsForView(0), named: "score"),
            "the successful AppState save must rebase before another save is allowed")
        let afterFirstSave = try String(contentsOf: baseURL, encoding: .utf8)
        XCTAssertFalse(afterFirstSave.contains("score:"), afterFirstSave)

        state.basesBuilderSaveToView()

        XCTAssertEqual(
            try String(contentsOf: baseURL, encoding: .utf8),
            afterFirstSave,
            "an empty repeated save must be a safe single batch, not a replayed RemoveFormula")
    }

    func testFailedSaveToViewDoesNotRebasePendingEdits() async throws {
        let (vault, state, session) = try await makeAppState()
        try Data(
            #"""
            formulas:
              score: "number(priority)"
            views:
              - type: table
                name: Formula
                order:
                  - file.name
                  - formula.score
            """#.utf8
        ).write(to: vault.appendingPathComponent("Queries/Formula.base"))

        state.openFile("Queries/Formula.base", target: .currentTab)
        state.basesEditViewFilters()
        let model = try XCTUnwrap(state.activeBaseQueryBuilder)
        model.removeFormula(named: "score")
        let staleHandle = try XCTUnwrap(state.activeBaseDocument?.handle)
        session.closeBase(handle: staleHandle)

        state.basesBuilderSaveToView()

        XCTAssertTrue(
            Self.hasRemoveFormulaEdit(try model.baseEditsForView(0), named: "score"),
            "a native save failure must leave the draft comparison baseline untouched")
    }

    func testSaveToViewOrchestrationHasOnePluralCallAndNoSingularLoop() throws {
        let source = try Self.sourceFile("Sources/SlateMac/Bases/AppState+Bases.swift")
        let start = try XCTUnwrap(source.range(of: "func basesBuilderSaveToView()"))
        let end = try XCTUnwrap(
            source.range(of: "func basesBuilderSaveAsBase", range: start.upperBound..<source.endIndex))
        let body = String(source[start.lowerBound..<end.lowerBound])

        XCTAssertEqual(
            body.components(separatedBy: "session.baseApplyEdits(").count - 1,
            1,
            body)
        XCTAssertFalse(body.contains("session.baseApplyEdit("), body)
        XCTAssertFalse(body.contains("for edit in"), body)
        XCTAssertTrue(body.contains("model.rebaseAfterSuccessfulSave()"), body)
    }

    func testUnrebasedBuilderPreservesPendingRemovedFormulaEdit() throws {
        let (_, session) = try makeSession()
        let validation = session.validateBaseExpression(source: "number(priority)")
        var draft = BaseQueryBuilderDraft()
        draft.formulas = [
            try BaseQueryFormula(
                name: "score",
                expression: "number(priority)",
                expressionJSON: XCTUnwrap(validation.exprJson))
        ]
        let model = BaseQueryBuilderModel(draft: draft)
        model.removeFormula(named: "score")

        let firstAttempt = try model.baseEditsForView(0)
        let retryAfterFailure = try model.baseEditsForView(0)

        XCTAssertTrue(Self.hasRemoveFormulaEdit(firstAttempt, named: "score"))
        XCTAssertTrue(
            Self.hasRemoveFormulaEdit(retryAfterFailure, named: "score"),
            "without an explicit success rebase, failed-save edits must remain pending")
    }

    func testCompletedDraftCanClearExistingViewFacets() throws {
        let (vault, session) = try makeSession()
        try session.saveText(
            path: "Queries/Clearable.base",
            contents:
                #"""
                formulas:
                  score: "number(priority) + 1"
                properties:
                  status:
                    displayName: Status
                views:
                  - type: table
                    name: Clearable
                    filters: "status != \"done\""
                    groupBy:
                      property: status
                      direction: DESC
                    order:
                      - status
                      - formula.score
                    slate:
                      sort:
                        - expr: formula.score
                          direction: desc
                """#,
            expectedContentHash: nil)

        let handle = try session.openBase(path: "Queries/Clearable.base")
        let previous = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: handle, view: 0))
        var draft = previous
        draft.rows = []
        draft.groupBy = nil
        draft.sortKeys = []
        draft.formulas = []
        draft.columns = [
            BaseQueryColumn(property: .note("status"), displayName: nil)
        ]

        for edit in try draft.baseEditsForView(0, replacing: previous) {
            try session.baseApplyEdit(handle: handle, edit: edit)
        }

        let saved = try String(
            contentsOf: vault.appendingPathComponent("Queries/Clearable.base"),
            encoding: .utf8)
        XCTAssertFalse(saved.contains("filters:"), saved)
        XCTAssertFalse(saved.contains("groupBy:"), saved)
        XCTAssertFalse(saved.contains("score:"), saved)
        XCTAssertFalse(saved.contains("displayName:"), saved)
        XCTAssertFalse(saved.contains("slate:"), saved)
        XCTAssertTrue(saved.contains("order:\n      - status"), saved)

        let reopened = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: handle, view: 0))
        XCTAssertTrue(reopened.rows.isEmpty)
        XCTAssertNil(reopened.groupBy)
        XCTAssertTrue(reopened.sortKeys.isEmpty)
        XCTAssertTrue(reopened.formulas.isEmpty)
        XCTAssertEqual(reopened.columns.map(\.id), ["status"])
        XCTAssertNil(reopened.columns.first?.displayName)
    }

    func testCompletedDraftWritesAndClearsTaskViewSource() throws {
        let (vault, session) = try makeSession()
        try session.saveText(
            path: "Queries/Tasks.base",
            contents:
                #"""
                views:
                  - type: table
                    name: Tasks
                    order:
                      - file.name
                """#,
            expectedContentHash: nil)
        let handle = try session.openBase(path: "Queries/Tasks.base")

        var tasksDraft = BaseQueryBuilderDraft()
        tasksDraft.source = .tasks
        tasksDraft.columns = [
            BaseQueryColumn(property: .task(.text), displayName: nil)
        ]
        for edit in try tasksDraft.baseEditsForView(0) {
            try session.baseApplyEdit(handle: handle, edit: edit)
        }
        var saved = try String(
            contentsOf: vault.appendingPathComponent("Queries/Tasks.base"),
            encoding: .utf8)
        XCTAssertTrue(saved.contains("source: tasks"), saved)

        let previous = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: handle, view: 0))
        var filesDraft = previous
        filesDraft.source = .allNotes
        filesDraft.columns = [
            BaseQueryColumn(property: .file(.name), displayName: nil)
        ]
        for edit in try filesDraft.baseEditsForView(0, replacing: previous) {
            try session.baseApplyEdit(handle: handle, edit: edit)
        }

        saved = try String(
            contentsOf: vault.appendingPathComponent("Queries/Tasks.base"),
            encoding: .utf8)
        XCTAssertFalse(saved.contains("source: tasks"), saved)
        let reopened = try BaseQueryBuilderDraft(
            queryJSON: session.baseViewQueryJson(handle: handle, view: 0))
        XCTAssertEqual(reopened.source, .allNotes)
        XCTAssertEqual(reopened.columns.map(\.id), ["file.name"])
    }

    func testExpressionValidationAndPreviewAnnouncementUseRustQueryResult() throws {
        let (_, session) = try makeSession()
        let valid = session.validateBaseExpression(source: "number(priority) + 1")
        XCTAssertTrue(valid.valid)
        XCTAssertNotNil(valid.exprJson)
        XCTAssertNil(valid.message)

        let invalid = session.validateBaseExpression(source: "number(")
        XCTAssertFalse(invalid.valid)
        XCTAssertNil(invalid.exprJson)
        XCTAssertEqual(invalid.spanStart, 7)
        XCTAssertGreaterThanOrEqual(invalid.spanEnd, invalid.spanStart)
        XCTAssertNotNil(invalid.message)

        var draft = BaseQueryBuilderDraft()
        draft.columns = [
            BaseQueryColumn(property: .file(.name), displayName: nil)
        ]
        let handle = try session.openQuery(queryJson: draft.queryJSON(), thisPath: nil)
        let result = try session.baseExecute(
            handle: handle,
            view: 0,
            thisPath: nil,
            quickFilter: nil,
            cancel: CancelToken())
        let preview = BaseQueryPreviewState.ready(result)

        XCTAssertTrue(preview.accessibilityAnnouncement.contains(result.audioSummary))
        XCTAssertTrue(preview.accessibilityAnnouncement.contains("First result:"))
        XCTAssertTrue(preview.accessibilityAnnouncement.contains("Alpha"))
    }

    func testPreviewNumericHeaderSortUsesTypedOrdering() throws {
        let vault = tempDir.appendingPathComponent("numeric-sort-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("---\npriority: 10\n---\n# Ten\n".utf8)
            .write(to: vault.appendingPathComponent("Ten.md"))
        try Data("---\npriority: 2\n---\n# Two\n".utf8)
            .write(to: vault.appendingPathComponent("Two.md"))
        try Data("# Missing\n".utf8)
            .write(to: vault.appendingPathComponent("Missing.md"))
        let session = try VaultSession.openFilesystem(rootPath: vault.path)
        try session.scanInitial(cancel: CancelToken())
        var draft = BaseQueryBuilderDraft()
        draft.columns = [
            BaseQueryColumn(property: .note("priority"), displayName: nil)
        ]
        let handle = try session.openQuery(queryJson: draft.queryJSON(), thisPath: nil)
        let result = try session.baseExecute(
            handle: handle,
            view: 0,
            thisPath: nil,
            quickFilter: nil,
            cancel: CancelToken())
        let rows = result.rows.enumerated().map {
            BaseGridRow(row: $0.element, ordinal: $0.offset)
        }

        XCTAssertEqual(
            rows.sorted { $0.sortsBefore($1, at: 0) }.map { $0.value(at: 0) },
            ["2", "10", ""],
            "builder preview sorting must use the engine-authored typed sort key, not display text")
        XCTAssertEqual(
            rows.sorted { $0.sortsBefore($1, at: 0, ascending: false) }
                .map { $0.value(at: 0) },
            ["10", "2", ""],
            "null values remain last when the user reverses typed preview sorting")
    }

    func testPreviewEqualSortKeysUseRustUTF8PathTiebreak() {
        func value() -> BasesValue {
            BasesValue(
                rawKind: "text",
                sortKey: "04:73616d65",
                display: "same",
                text: "same",
                number: nil,
                boolValue: nil,
                dateEpochMs: nil,
                dateHasTime: false,
                linkTarget: nil,
                linkDisplay: nil,
                list: [],
                error: nil)
        }
        let composed = BasesRow(
            filePath: "é.md",
            taskOrdinal: nil,
            values: [value()],
            audioDescription: "composed")
        let decomposed = BasesRow(
            filePath: "e\u{301}.md",
            taskOrdinal: nil,
            values: [value()],
            audioDescription: "decomposed")
        let rows = [
            BaseGridRow(row: composed, ordinal: 0),
            BaseGridRow(row: decomposed, ordinal: 1),
        ]
        let expected = [composed.filePath, decomposed.filePath].sorted {
            $0.utf8.lexicographicallyPrecedes($1.utf8)
        }

        XCTAssertEqual(
            rows.sorted { $0.sortsBefore($1, at: 0) }.map { $0.row.filePath },
            expected)
        XCTAssertNotEqual(
            BaseGridRow.id(for: composed),
            BaseGridRow.id(for: decomposed),
            "selection identity must not canonically collapse distinct UTF-8 paths")
    }

    func testPreviewSelectionAndSortFollowColumnIdentityAfterReorder() throws {
        let (_, session) = try makeSession()
        var draft = BaseQueryBuilderDraft()
        draft.columns = [
            BaseQueryColumn(property: .file(.name), displayName: nil),
            BaseQueryColumn(property: .note("priority"), displayName: nil),
        ]
        let handle = try session.openQuery(queryJson: draft.queryJSON(), thisPath: nil)
        let initial = try session.baseExecute(
            handle: handle,
            view: 0,
            thisPath: nil,
            quickFilter: nil,
            cancel: CancelToken())
        let rowID = BaseGridRow.id(for: try XCTUnwrap(initial.rows.first))
        var interaction = BaseGridInteractionState()
        interaction.setCellPosition(.init(rowID: rowID, columnIndex: 1), in: initial)
        interaction.setSortState(
            DataGridSortState(columnIndex: 1, ascending: false),
            in: initial)

        var reordered = initial
        reordered.columns.swapAt(0, 1)
        for index in reordered.rows.indices { reordered.rows[index].values.swapAt(0, 1) }
        interaction.reconcile(with: reordered)

        XCTAssertEqual(interaction.cellPosition(in: reordered)?.columnIndex, 0)
        XCTAssertEqual(
            interaction.sortState(in: reordered),
            DataGridSortState(columnIndex: 0, ascending: false))

        reordered.columns = []
        reordered.rows = reordered.rows.map { row in
            var row = row
            row.values = []
            return row
        }
        interaction.reconcile(with: reordered)
        XCTAssertNil(interaction.selectedCell)
        XCTAssertNil(interaction.sortSelection)
    }
}
