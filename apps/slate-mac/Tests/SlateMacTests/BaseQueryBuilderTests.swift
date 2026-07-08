// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

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
            """.utf8
        ).write(to: vault.appendingPathComponent("Projects/Alpha.md"))
        let session = try VaultSession.openFilesystem(rootPath: vault.path)
        try session.scanInitial(cancel: CancelToken())
        return (vault, session)
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

    func testSavedRecentAndLinkedSourcesReopenAsPickerSources() throws {
        let (_, session) = try makeSession()
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

        XCTAssertTrue(source.contains(".file(.size)"), source)
        XCTAssertTrue(source.contains(".file(.inDegree)"), source)
        XCTAssertTrue(source.contains(".file(.outDegree)"), source)
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
        XCTAssertTrue(source.contains("advancedExpressionValidations"))
    }

    func testAppStateOwnsBuilderPreviewAndSaveOrchestration() throws {
        let source = try Self.sourceFile("Sources/SlateMac/Bases/AppState+Bases.swift")

        XCTAssertTrue(source.contains("basesBuilderSchedulePreview"))
        XCTAssertTrue(source.contains("openQuery"))
        XCTAssertTrue(source.contains("baseApplyEdit"))
        XCTAssertTrue(source.contains("saveQueryAsBase"))
        XCTAssertTrue(source.contains("saveQuery("))
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
}
