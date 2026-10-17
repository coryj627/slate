// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import CryptoKit
import Foundation
import XCTest

@testable import SlateMac

/// W6-1 §H TH-3 (H3): the Swift twin of the canvas scenario harness
/// (`apps/slate-windows/tools/ParityHarness/CanvasScenarioHarness.cs`).
/// The scenario scripts are DATA
/// (`crates/slate-core/tests/fixtures/canvas_scenario_golden/scenarios.json`),
/// executed here verbatim: each scenario seeds ONE fixture into a fresh
/// temp vault as `board.canvas`, opens it, applies each step as one
/// `CanvasAction` through the REAL vault apply, then walks the returned
/// inverses backward. The driver enforces E18's three rules itself and
/// writes the same canonical artifact the C# driver writes, so both
/// lanes green proves cross-platform byte-identity against the goldens
/// Windows regenerates (the regen authority).
final class CanvasScenarioTests: XCTestCase {
    private static var repoRoot: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .deletingLastPathComponent()  // apps
            .deletingLastPathComponent()  // repo root
    }

    private static var goldenDir: URL {
        repoRoot.appendingPathComponent("crates/slate-core/tests/fixtures/canvas_scenario_golden")
    }

    private static var scenariosPath: URL {
        goldenDir.appendingPathComponent("scenarios.json")
    }

    // MARK: - The facts

    /// The artifacts equal the committed goldens byte for byte — the same
    /// name set, the same bytes — on the mac lane, which is what makes the
    /// scenarios file a cross-platform contract rather than a Windows one.
    func testCanvasScenarioArtifactsMatchCommittedGoldensByteForByte() throws {
        let produced = try CanvasScenarioDriver.runAll(scenariosPath: Self.scenariosPath)

        let goldenNames = try FileManager.default
            .contentsOfDirectory(atPath: Self.goldenDir.path)
            .filter { $0.hasSuffix(".json") && $0 != "scenarios.json" }
            .sorted { Array($0.utf16).lexicographicallyPrecedes(Array($1.utf16)) }
        XCTAssertEqual(
            goldenNames,
            produced.map { $0.name + ".json" }
                .sorted { Array($0.utf16).lexicographicallyPrecedes(Array($1.utf16)) })

        for (name, artifact) in produced {
            let golden = try Data(contentsOf: Self.goldenDir.appendingPathComponent(name + ".json"))
            XCTAssertEqual(
                Data(artifact.utf8), golden,
                "artifact \(name) differs from golden — the mac and Windows drivers have "
                    + "drifted; fix the divergence (or regenerate goldens deliberately with "
                    + "the Windows harness) before merging")
        }
    }

    /// The driver's E18 gates have teeth: a canonical fixture mislabelled
    /// foreign must make the driver throw the foreign-survived error, so
    /// the round-trip comparison really runs.
    func testTheForeignGateRefusesACanonicalFixtureMarkedForeign() throws {
        let data = try Data(contentsOf: Self.scenariosPath)
        guard var root = try JSONSerialization.jsonObject(with: data) as? [String: Any],
            var scenarios = root["scenarios"] as? [[String: Any]]
        else {
            XCTFail("scenarios.json is not an object with a scenarios array")
            return
        }
        let baseDir = Self.scenariosPath.deletingLastPathComponent()
        for index in scenarios.indices {
            let fixture = scenarios[index]["fixture"] as? String ?? ""
            scenarios[index]["fixture"] = baseDir.appendingPathComponent(fixture)
                .standardizedFileURL.path
            if scenarios[index]["name"] as? String == "c2_nested_groups_geometry" {
                scenarios[index]["foreign"] = true
            }
        }
        root["scenarios"] = scenarios

        let tmp = FileManager.default.temporaryDirectory
            .appendingPathComponent("canvas-scenario-teeth-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tmp, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tmp) }
        let mislabelled = tmp.appendingPathComponent("scenarios.json")
        try JSONSerialization.data(withJSONObject: root).write(to: mislabelled)

        XCTAssertThrowsError(try CanvasScenarioDriver.runAll(scenariosPath: mislabelled)) { error in
            guard let driverError = error as? CanvasScenarioDriverError else {
                XCTFail("expected the driver's own error, got \(error)")
                return
            }
            XCTAssertTrue(
                driverError.message.contains("foreign formatting survived"),
                "unexpected message: \(driverError.message)")
        }
    }

    /// Two in-process runs produce identical bytes, or the goldens pin luck.
    func testCanvasScenarioHarnessIsDeterministicAcrossRuns() throws {
        let first = try CanvasScenarioDriver.runAll(scenariosPath: Self.scenariosPath)
        let second = try CanvasScenarioDriver.runAll(scenariosPath: Self.scenariosPath)
        XCTAssertEqual(first.map { $0.name }, second.map { $0.name })
        for (a, b) in zip(first, second) {
            XCTAssertEqual(a.artifact, b.artifact, "artifact \(a.name) differs between runs")
        }
    }
}

// MARK: - File-card activation (plan 43 PR 2: #1169, CD-38)

/// The Swift twins of `CanvasDocumentTests.TheProductionMediaSeamOpensMediaAndRefusesEverythingElse`
/// and `CanvasDocumentTests.AMediaFileThatExistsButCannotBeOpenedRefusesRatherThanClaimingItIsMissing`.
/// A canvas is untrusted input — it arrives over sync, from a shared
/// vault, from Obsidian — so a file card reaches the default-app opener
/// only when core classifies its target as media (`canvasMediaClass`),
/// and every other outcome is a corpus sentence core renders, spoken
/// through the canvas announcer. Each fact drives the one activation
/// the outline and the table share, through a real `AppState`, a real
/// vault and the real FFI session; only the opener is injected.
@MainActor
extension CanvasScenarioTests {
    /// The corpus row (`tests/fixtures/a11y/corpus.json`), verbatim.
    private static let setupExeRefusal =
        "setup.exe is not a note or media file, so it was not opened from the canvas."

    func testASetupExeFileCardIsRefusedAndNeverReachesTheOpener() async throws {
        let fixture = try await CanvasActivationFixture(
            card: #"{"id":"exe","type":"file","file":"setup.exe","x":0,"y":0,"width":200,"height":100}"#,
            files: ["setup.exe": Data("MZ".utf8)],
            opener: { url in
                XCTFail("a non-media file card reached the default-app opener: \(url.path)")
                return true
            })
        defer { fixture.tearDown() }
        // The premise: the target is genuinely THERE and is not media, so
        // neither "missing" nor an opener call can be right by accident.
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("setup.exe").path))
        XCTAssertNil(canvasMediaClass(path: "setup.exe"))

        try fixture.activate("exe")

        XCTAssertEqual(fixture.posted.map(\.text), [Self.setupExeRefusal])
        XCTAssertEqual(fixture.posted.first?.priority, .high, "a refusal is assertive")
        // Core's sentence, not a host composition of it.
        XCTAssertEqual(
            Self.setupExeRefusal,
            a11yRender(
                event: .canvas(
                    event: .canvasBlocked(reason: .fileTypeNotOpenable(target: "setup.exe")))
            ).text)
    }

    func testAMediaCardWhoseOpenFailsSpeaksAFailedActionNotFileNotFound() async throws {
        var handed: [URL] = []
        let fixture = try await CanvasActivationFixture(
            card: #"{"id":"clip","type":"file","file":"media/clip.mp4","x":0,"y":0,"width":200,"height":100}"#,
            files: ["media/clip.mp4": Data([0, 0, 0, 24])],
            opener: { url in
                handed.append(url)
                return false
            })
        defer { fixture.tearDown() }
        XCTAssertEqual(canvasMediaClass(path: "media/clip.mp4"), .video)

        try fixture.activate("clip")

        // The opener WAS asked — the file exists and is media — and declined.
        XCTAssertEqual(
            handed.map(\.path), [fixture.vault.appendingPathComponent("media/clip.mp4").path])
        let failed = a11yRender(
            event: .canvas(
                event: .canvasActionFailed(action: .canvasAction, detail: "media/clip.mp4"))
        ).text
        XCTAssertEqual(failed, "Canvas action failed: media/clip.mp4")
        XCTAssertEqual(fixture.posted.map(\.text), [failed])
        XCTAssertEqual(fixture.posted.first?.priority, .high)
        // Never the absence sentence: the file is there, and Locate File
        // would send the user to repoint a card that is fine.
        let missing = a11yRender(
            event: .canvas(event: .canvasFileNotFound(target: "media/clip.mp4"))
        ).text
        XCTAssertFalse(fixture.posted.contains { $0.text == missing }, "\(fixture.posted)")
    }

    /// Media opens as before: the default app is handed the in-vault file
    /// and the sentence says so.
    func testAMediaCardOpensInItsDefaultAppAndSaysSo() async throws {
        var handed: [URL] = []
        let fixture = try await CanvasActivationFixture(
            card: #"{"id":"pic","type":"file","file":"diagram.png","x":0,"y":0,"width":200,"height":100}"#,
            files: ["diagram.png": Data([0x89, 0x50, 0x4E, 0x47])],
            opener: { url in
                handed.append(url)
                return true
            })
        defer { fixture.tearDown() }
        let row = try XCTUnwrap(fixture.document.outline.first { $0.nodeId == "pic" })
        XCTAssertEqual(row.kind, "image")

        try fixture.activate("pic")

        XCTAssertEqual(
            handed.map(\.path), [fixture.vault.appendingPathComponent("diagram.png").path])
        XCTAssertEqual(
            fixture.posted.map(\.text),
            [
                a11yRender(
                    event: .canvas(event: .canvasOpened(title: row.title, target: .defaultApp))
                ).text
            ])
    }

    func testAMarkdownCardStillOpensInTheTab() async throws {
        let fixture = try await CanvasActivationFixture(
            card: #"{"id":"note","type":"file","file":"notes/target.md","x":0,"y":0,"width":200,"height":100}"#,
            files: ["notes/target.md": Data("# Target\n".utf8)],
            opener: { url in
                XCTFail("a Markdown card is a note, never a default-app open: \(url.path)")
                return true
            })
        defer { fixture.tearDown() }

        try fixture.activate("note")
        await fixture.state.noteLoadTask?.value

        XCTAssertEqual(fixture.state.selectedFilePath, "notes/target.md")
        guard case .markdown(let path) = fixture.state.workspace.activeTab?.item else {
            return XCTFail(
                "expected a note tab, got \(String(describing: fixture.state.workspace.activeTab))")
        }
        XCTAssertEqual(path, "notes/target.md")
        XCTAssertTrue(fixture.posted.isEmpty, "a note opening is not a refusal: \(fixture.posted)")
    }
}

/// One vault holding `board.canvas` with a single card, the card's
/// target files written beside it BEFORE the scan, opened in a canvas
/// tab, with a capturing announcer installed after the open so only the
/// activation's own sentences land in `posted`.
@MainActor
private final class CanvasActivationFixture {
    let root: URL
    let vault: URL
    let state: AppState
    let document: CanvasDocument
    var posted: [(text: String, priority: NSAccessibilityPriorityLevel)] = []

    init(card: String, files: [String: Data], opener: @escaping (URL) -> Bool) async throws {
        let fm = FileManager.default
        root = fm.temporaryDirectory
            .appendingPathComponent("canvas-activation-\(UUID().uuidString)")
            .resolvingSymlinksInPath()
        vault = root.appendingPathComponent("vault")
        try fm.createDirectory(at: vault, withIntermediateDirectories: true)
        for (path, data) in files {
            let url = vault.appendingPathComponent(path)
            try fm.createDirectory(
                at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try data.write(to: url)
        }
        try Data(#"{"nodes":[\#(card)],"edges":[]}"#.utf8)
            .write(to: vault.appendingPathComponent("board.canvas"))
        let store = RecentVaultsStore(fileURL: root.appendingPathComponent("recents.json"))
        state = AppState(recentsStore: store, externalOpener: opener)
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("board.canvas", target: .currentTab)
        document = state.canvasDocument(for: "board.canvas")
        state.canvasAnnouncer = CanvasAnnouncer(verbosity: .standard, coalesceWindow: 60) {
            [weak self] text, priority in
            self?.posted.append((text, priority))
        }
    }

    /// Activate the card the way the container's activation switch does.
    func activate(_ nodeId: String) throws {
        let row = try XCTUnwrap(document.outline.first { $0.nodeId == nodeId })
        state.canvasActivateFileCard(nodeId: nodeId, title: row.title, in: document)
        state.canvasAnnouncer.flushForTests()
    }

    func tearDown() {
        try? FileManager.default.removeItem(at: root)
    }
}

/// #1166 (contract 34, "A stale selection survives undo"): a mutation's
/// reload never leaves the shared selection naming a node the refreshed
/// outline no longer holds. This scenario runs through the real verbs
/// and the real funnel rather than the harness's bare applies, because
/// the defect is the HOST's: core's undo removes the card correctly, and
/// it was the selection the host kept afterwards that let a verb whose
/// guard tests the raw selection act on nothing.
extension CanvasScenarioTests {
    @MainActor
    func testUndoingACreateDropsTheSelectionAndDuplicateRefuses() async throws {
        let tmp = FileManager.default.temporaryDirectory
            .appendingPathComponent("canvas-undo-create-\(UUID().uuidString)")
        let vault = tmp.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tmp) }
        try Data(
            #"{"nodes":[{"id":"a","type":"text","text":"Alpha","x":0,"y":0,"width":200,"height":100}],"edges":[]}"#
                .utf8
        ).write(to: vault.appendingPathComponent("board.canvas"))
        let state = AppState(
            recentsStore: RecentVaultsStore(fileURL: tmp.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("board.canvas", target: .currentTab)
        var heard: [String] = []
        state.canvasAnnouncer = CanvasAnnouncer(verbosity: .standard, coalesceWindow: 60) {
            text, _ in heard.append(text)
        }
        let doc = try XCTUnwrap(state.activeCanvasDocument)

        // Create a card: the verb selects it and opens its editor, which
        // the user then closes.
        state.canvasNewCard()
        let created = try XCTUnwrap(doc.selection.selected, "a new card is selected")
        XCTAssertTrue(doc.outline.contains { $0.nodeId == created })
        state.dismissCanvasCardEditor()

        // Undo it: the card is gone, and so is the selection that named it.
        state.canvasUndo()
        XCTAssertFalse(doc.outline.contains { $0.nodeId == created }, "the undo removed the card")
        XCTAssertNil(
            doc.selection.selected,
            "the reload dropped a selection the refreshed outline cannot resolve")
        XCTAssertEqual(doc.undoStack.map(\.name), [], "the create was the only entry")
        state.canvasAnnouncer.flushForTests()

        // Duplicate now has nothing selected, and says so with the arm
        // every selection verb already speaks.
        let beforeDuplicate = heard.count
        state.canvasDuplicate()
        state.canvasAnnouncer.flushForTests()
        XCTAssertEqual(
            Array(heard[beforeDuplicate...]),
            [a11yRender(event: .canvas(event: .canvasStatus(note: .nothingSelected))).text])
        XCTAssertEqual(Array(heard[beforeDuplicate...]), ["Nothing selected."])
        XCTAssertEqual(doc.undoStack.map(\.name), [], "a refused duplicate writes no undo entry")
        XCTAssertFalse(doc.undoStack.contains { $0.name == "duplicate 0 cards" })
        XCTAssertFalse(
            heard.contains { $0.contains("Duplicated 0 cards") },
            "a zero count is never spoken: \(heard)")
    }
}

/// The driver's own failure — E18's rules, refused audibly.
struct CanvasScenarioDriverError: Error {
    let message: String
}

/// The Swift twin of `CanvasScenarioDriver` in `CanvasScenarioHarness.cs`.
/// Same steps, same op vocabulary, same artifact; change both together.
enum CanvasScenarioDriver {
    struct Scenario {
        let name: String
        let fixturePath: URL
        let foreign: Bool
        let steps: [Step]
    }

    struct Step {
        let name: String
        let ops: [CanvasOp]
    }

    static func runAll(scenariosPath: URL) throws -> [(name: String, artifact: String)] {
        try load(scenariosPath: scenariosPath).map { scenario in
            (name: scenario.name, artifact: try run(scenario))
        }
    }

    static func load(scenariosPath: URL) throws -> [Scenario] {
        let baseDir = scenariosPath.standardizedFileURL.deletingLastPathComponent()
        let data = try Data(contentsOf: scenariosPath)
        guard let root = try JSONSerialization.jsonObject(with: data) as? [String: Any],
            let scenarios = root["scenarios"] as? [[String: Any]]
        else {
            throw CanvasScenarioDriverError(message: "scenarios.json has no scenarios array")
        }
        return try scenarios.map { scenario in
            guard let name = scenario["name"] as? String,
                let fixture = scenario["fixture"] as? String
            else {
                throw CanvasScenarioDriverError(message: "a scenario lacks its name or fixture")
            }
            let steps = try (scenario["steps"] as? [[String: Any]] ?? []).map { step -> Step in
                guard let stepName = step["name"] as? String else {
                    throw CanvasScenarioDriverError(message: "\(name): a step lacks its name")
                }
                let ops = try (step["ops"] as? [[String: Any]] ?? []).map(parseOp)
                return Step(name: stepName, ops: ops)
            }
            let fixturePath: URL =
                fixture.hasPrefix("/")
                ? URL(fileURLWithPath: fixture)
                : baseDir.appendingPathComponent(fixture).standardizedFileURL
            return Scenario(
                name: name,
                fixturePath: fixturePath,
                foreign: scenario["foreign"] as? Bool ?? false,
                steps: steps)
        }
    }

    static func run(_ scenario: Scenario) throws -> String {
        let fm = FileManager.default
        // macOS's temporaryDirectory is /var/folders/…, a symlink to
        // /private/var/… — resolve it so the vault root the session
        // opens is the path the files are written under.
        let vaultRoot = fm.temporaryDirectory
            .appendingPathComponent("canvas-scenario-\(UUID().uuidString)")
            .resolvingSymlinksInPath()
        try fm.createDirectory(at: vaultRoot, withIntermediateDirectories: true)
        defer { try? fm.removeItem(at: vaultRoot) }

        let original = try String(contentsOf: scenario.fixturePath, encoding: .utf8)
        let board = vaultRoot.appendingPathComponent("board.canvas")
        try original.write(to: board, atomically: true, encoding: .utf8)

        let session = try VaultSession.openFilesystem(rootPath: vaultRoot.path)
        let cancel = CancelToken()
        _ = try session.scanInitial(cancel: cancel)

        let info = try session.openCanvas(path: "board.canvas")
        defer { session.closeCanvas(handle: info.handle) }

        var steps: [(name: String, hash: String)] = []
        var inverses: [CanvasAction] = []
        for step in scenario.steps {
            let result = try session.canvasApply(
                handle: info.handle, action: CanvasAction(name: step.name, ops: step.ops))
            steps.append((name: step.name, hash: result.newContentHash))
            inverses.append(result.inverse)
        }

        let terminal = try String(contentsOf: board, encoding: .utf8)
        for inverse in inverses.reversed() {
            _ = try session.canvasApply(handle: info.handle, action: inverse)
        }

        let roundTrip = try String(contentsOf: board, encoding: .utf8)
        // Core's own canonicalizer: the empty detached apply.
        let canonicalOfOriginal = try canvasApplyDetached(
            text: original, action: CanvasAction(name: "canonicalize", ops: []))
        if roundTrip != canonicalOfOriginal {
            throw CanvasScenarioDriverError(
                message: "\(scenario.name): the inverse walk did not restore core's "
                    + "canonical serialization of the original content.")
        }
        if !scenario.foreign && roundTrip != original {
            throw CanvasScenarioDriverError(
                message: "\(scenario.name): a canonical fixture must round-trip "
                    + "byte-identical (E18).")
        }
        if scenario.foreign && roundTrip == original {
            throw CanvasScenarioDriverError(
                message: "\(scenario.name): the foreign formatting survived the "
                    + "round-trip; the first write must canonicalize (E18).")
        }

        return artifact(
            scenario: scenario, original: original, steps: steps,
            terminal: terminal, roundTrip: roundTrip)
    }

    // MARK: - The artifact

    private static func artifact(
        scenario: Scenario,
        original: String,
        steps: [(name: String, hash: String)],
        terminal: String,
        roundTrip: String
    ) -> String {
        let j = CanonicalJsonC()
        j.raw("{\"scenario\":").str(scenario.name)
        j.raw(",\"foreign\":").raw(scenario.foreign ? "true" : "false")
        j.raw(",\"originalSha256\":").str(sha(original))
        j.raw(",\"steps\":[")
        for (index, step) in steps.enumerated() {
            if index > 0 {
                j.raw(",")
            }
            j.raw("{\"name\":").str(step.name)
                .raw(",\"contentHash\":").str(step.hash).raw("}")
        }
        j.raw("],\"terminalSha256\":").str(sha(terminal))
        j.raw(",\"terminalBytes\":").str(terminal)
        j.raw(",\"roundTripSha256\":").str(sha(roundTrip))
        j.raw("}\n")
        return j.output
    }

    private static func sha(_ text: String) -> String {
        SHA256.hash(data: Data(text.utf8)).map { String(format: "%02x", $0) }.joined()
    }

    // MARK: - The op vocabulary (the C# ParseOp, arm for arm)

    private static func parseOp(_ op: [String: Any]) throws -> CanvasOp {
        let kind = op["kind"] as? String ?? ""
        switch kind {
        case "createNode":
            return .createNode(
                id: try string(op, "id"),
                content: .text(text: try string(op, "text")),
                x: try double(op, "x"),
                y: try double(op, "y"),
                width: try double(op, "width"),
                height: try double(op, "height"),
                color: optional(op, "color"))
        case "cloneNode":
            return .cloneNode(
                sourceId: try string(op, "sourceId"),
                id: try string(op, "id"),
                x: try double(op, "x"),
                y: try double(op, "y"))
        case "createGroup":
            return .createGroup(
                id: try string(op, "id"),
                label: optional(op, "label"),
                x: try double(op, "x"),
                y: try double(op, "y"),
                width: try double(op, "width"),
                height: try double(op, "height"),
                color: optional(op, "color"))
        case "updateNodeGeometry":
            return .updateNodeGeometry(
                id: try string(op, "id"),
                x: try double(op, "x"),
                y: try double(op, "y"),
                width: try double(op, "width"),
                height: try double(op, "height"))
        case "setNodeColor":
            return .setNodeColor(id: try string(op, "id"), color: optional(op, "color"))
        case "setNodeContent":
            return .setNodeContent(
                id: try string(op, "id"),
                content: .text(text: try string(op, "text")))
        case "deleteNode":
            return .deleteNode(id: try string(op, "id"))
        case "addEdge":
            return .addEdge(
                id: try string(op, "id"),
                fromNode: try string(op, "fromNode"),
                fromSide: try side(op, "fromSide"),
                toNode: try string(op, "toNode"),
                toSide: try side(op, "toSide"),
                fromEnd: end(op, "fromEnd"),
                toEnd: end(op, "toEnd"),
                label: optional(op, "label"),
                color: optional(op, "color"))
        case "updateEdge":
            return .updateEdge(
                id: try string(op, "id"),
                fromSide: try side(op, "fromSide"),
                toSide: try side(op, "toSide"),
                fromEnd: end(op, "fromEnd"),
                toEnd: end(op, "toEnd"),
                label: optional(op, "label"),
                color: optional(op, "color"))
        case "deleteEdge":
            return .deleteEdge(id: try string(op, "id"))
        default:
            throw CanvasScenarioDriverError(message: "unknown canvas op '\(kind)'")
        }
    }

    private static func string(_ op: [String: Any], _ name: String) throws -> String {
        guard let value = op[name] as? String else {
            throw CanvasScenarioDriverError(message: "op lacks the string field '\(name)'")
        }
        return value
    }

    private static func double(_ op: [String: Any], _ name: String) throws -> Double {
        guard let value = op[name] as? NSNumber else {
            throw CanvasScenarioDriverError(message: "op lacks the number field '\(name)'")
        }
        return value.doubleValue
    }

    /// A missing key or a JSON null reads as nil — the C# `Optional`.
    private static func optional(_ op: [String: Any], _ name: String) -> String? {
        op[name] as? String
    }

    /// A missing or null side is nil; an unknown side throws — the C# `Side`.
    private static func side(_ op: [String: Any], _ name: String) throws -> CanvasSide? {
        switch optional(op, name) {
        case nil: return nil
        case "top"?: return .top
        case "right"?: return .right
        case "bottom"?: return .bottom
        case "left"?: return .left
        case let other?: throw CanvasScenarioDriverError(message: "unknown side '\(other)'")
        }
    }

    /// Anything but "arrow" — a missing key included — is None; the C# `End`.
    private static func end(_ op: [String: Any], _ name: String) -> CanvasEndStyle {
        optional(op, name) == "arrow" ? CanvasEndStyle.arrow : CanvasEndStyle.none
    }
}

/// Canonical JSON writer — the Swift half of the fixed serialization
/// algorithm defined in `apps/slate-windows/tools/ParityHarness/
/// CanonicalJson.cs`, duplicated from ParityHarnessTests.swift (the
/// harness writers are private-scope by design; change all
/// implementations together, never one). The scenario artifact uses
/// only `raw` and `str`.
private final class CanonicalJsonC {
    private(set) var output = ""

    @discardableResult
    func raw(_ s: String) -> CanonicalJsonC {
        output += s
        return self
    }

    @discardableResult
    func str(_ value: String) -> CanonicalJsonC {
        output += "\""
        for scalar in value.unicodeScalars {
            switch scalar {
            case "\"": output += "\\\""
            case "\\": output += "\\\\"
            case "\n": output += "\\n"
            case "\r": output += "\\r"
            case "\t": output += "\\t"
            default:
                if scalar.value < 0x20 {
                    output += String(format: "\\u%04x", scalar.value)
                } else {
                    output.unicodeScalars.append(scalar)
                }
            }
        }
        output += "\""
        return self
    }
}
