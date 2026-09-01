// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W5-4 (#744) H1: the mac half of the mutation-harness two-oracle
// mechanism. The scenario scripts are DATA
// (crates/slate-core/tests/fixtures/mutation_golden/scenarios.json),
// executed verbatim by this driver and the C# one
// (apps/slate-windows/tools/ParityHarness/MutationHarness.cs), and the
// artifacts are asserted byte-for-byte against the SAME committed
// goldens — both lanes green proves cross-platform byte-identity
// transitively. Windows is the regen authority.
//
// The fault env vars are process-global; XCTest runs the methods of
// one class serially, and no other Swift test touches
// SLATE_TEST_FAULT_*, so this file is its own serialization domain —
// revisit if a second fault-seam Swift test ever appears.

import CryptoKit
import Foundation
import XCTest

@testable import SlateMac

final class MutationHarnessTests: XCTestCase {
    private static var repoRoot: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .deletingLastPathComponent()  // apps
            .deletingLastPathComponent()  // repo root
    }

    private static var goldenDir: URL {
        repoRoot.appendingPathComponent("crates/slate-core/tests/fixtures/mutation_golden")
    }

    private static var scenariosURL: URL {
        goldenDir.appendingPathComponent("scenarios.json")
    }

    func testMutationArtifactsMatchCommittedGoldensByteForByte() throws {
        let produced = try Self.runAll()

        let goldenNames = try FileManager.default
            .contentsOfDirectory(atPath: Self.goldenDir.path)
            .filter { $0.hasSuffix(".json") && $0 != "scenarios.json" }
            .sorted { Array($0.utf16).lexicographicallyPrecedes(Array($1.utf16)) }
        XCTAssertEqual(
            goldenNames,
            produced.map { $0.name + ".json" }
                .sorted { Array($0.utf16).lexicographicallyPrecedes(Array($1.utf16)) })

        for (name, artifact) in produced {
            let golden = try Data(
                contentsOf: Self.goldenDir.appendingPathComponent(name + ".json"))
            XCTAssertEqual(
                Data(artifact.utf8), golden,
                "artifact \(name) differs from golden — the mac and Windows mutation "
                    + "serializations have drifted; fix the divergence (or regenerate "
                    + "goldens deliberately with the Windows harness) before merging")
        }
    }

    func testMutationHarnessIsDeterministicAcrossRuns() throws {
        let first = try Self.runAll()
        let second = try Self.runAll()
        XCTAssertEqual(first.map(\.name), second.map(\.name))
        for (a, b) in zip(first, second) {
            XCTAssertEqual(a.artifact, b.artifact, "artifact \(a.name) not deterministic")
        }
    }

    // MARK: - Scenario data

    private struct Seed: Decodable {
        let path: String
        let content: String
    }

    private struct Op: Decodable {
        let op: String
        let label: String?
        let path: String?
        let content: String?
        let newName: String?
        let newParent: String?
        let items: [String]?
        let destination: String?
        let opRef: Int?
        let name: String?
        let value: String?
        let expect: String?
    }

    private struct Scenario: Decodable {
        let name: String
        let seed: [Seed]
        let untouched: [String]?
        let assertEqual: [[String]]?
        let terminalTreeMatches: String?
        let ops: [Op]
    }

    private struct ScenarioFile: Decodable {
        let scenarios: [Scenario]
    }

    private struct DriverError: Error, CustomStringConvertible {
        let description: String
        init(_ message: String) { description = message }
    }

    private struct TreeEntry: Equatable {
        let path: String
        let size: Int64
        let sha256: String
    }

    // MARK: - Driver

    private static func runAll() throws -> [(name: String, artifact: String)] {
        let data = try Data(contentsOf: scenariosURL)
        let file = try JSONDecoder().decode(ScenarioFile.self, from: data)
        var artifacts: [(name: String, artifact: String)] = []
        var terminalTrees: [String: [TreeEntry]] = [:]
        for scenario in file.scenarios {
            let (artifact, terminal) = try run(scenario)
            artifacts.append((scenario.name, artifact))
            terminalTrees[scenario.name] = terminal
        }
        for scenario in file.scenarios {
            guard let twin = scenario.terminalTreeMatches else { continue }
            guard terminalTrees[scenario.name] == terminalTrees[twin] else {
                throw DriverError(
                    "\(scenario.name): terminal tree differs from \(twin) — the "
                        + "retry-after-conflict run is not idempotent (invariant 6).")
            }
        }
        return artifacts
    }

    private static let faultNames = [
        "PLANT_MARKERS", "PRE_WRITE", "AFTER_WRITE", "RECOVERY_FINALIZE",
    ]

    private static func setFault(_ name: String, _ value: String?) throws {
        guard faultNames.contains(name) else {
            throw DriverError("unknown fault '\(name)'")
        }
        if let value {
            setenv("SLATE_TEST_FAULT_\(name)", value, 1)
        } else {
            unsetenv("SLATE_TEST_FAULT_\(name)")
        }
    }

    private static func run(
        _ scenario: Scenario
    ) throws -> (artifact: String, terminal: [TreeEntry]) {
        let fm = FileManager.default
        var vaultRoot = fm.temporaryDirectory
            .appendingPathComponent("mutation-harness-\(UUID().uuidString)")
        try fm.createDirectory(at: vaultRoot, withIntermediateDirectories: true)
        // macOS's temporaryDirectory is /var/folders/…, a symlink to
        // /private/var/… — the enumerator hands back RESOLVED urls,
        // so the relative-path prefix check against the unresolved
        // root matched nothing and every tree entry stayed absolute
        // (the first CI run's "untouched file missing at 'pre'").
        vaultRoot = vaultRoot.resolvingSymlinksInPath()
        defer {
            for name in faultNames { unsetenv("SLATE_TEST_FAULT_\(name)") }
            try? fm.removeItem(at: vaultRoot)
        }

        for seed in scenario.seed {
            let absolute = vaultRoot.appendingPathComponent(seed.path)
            try fm.createDirectory(
                at: absolute.deletingLastPathComponent(),
                withIntermediateDirectories: true)
            try Data(seed.content.utf8).write(to: absolute)
        }

        var session = try VaultSession.openFilesystem(rootPath: vaultRoot.path)
        _ = try session.scanInitial(cancel: CancelToken())

        var checkpoints: [(label: String, tree: [TreeEntry])] = []
        var reports: [(opIndex: Int, body: String)] = []
        var refusals: [(opIndex: Int, kind: String, path: String?)] = []
        var structuralByIndex: [Int: StructuralReport] = [:]
        var batchByIndex: [Int: BatchMoveReport] = [:]
        var opIdOrder: [Int64] = []

        for (index, op) in scenario.ops.enumerated() {
            switch op.op {
            case "checkpoint":
                checkpoints.append((op.label!, try captureTree(vaultRoot)))
                continue
            case "fault":
                try setFault(op.name!, op.value)
                continue
            case "reopen":
                session = try VaultSession.openFilesystem(rootPath: vaultRoot.path)
                _ = try session.scanInitial(cancel: CancelToken())
                continue
            default:
                break
            }

            do {
                let body = try execute(
                    session, op,
                    structuralByIndex: &structuralByIndex,
                    batchByIndex: &batchByIndex,
                    index: index,
                    opIdOrder: &opIdOrder)
                if op.expect != nil {
                    throw DriverError(
                        "\(scenario.name) op \(index) (\(op.op)) expected "
                            + "\(op.expect!) but succeeded.")
                }
                if let body {
                    reports.append((index, body))
                }
            } catch let error as VaultError {
                let (kind, refusedPath) = refusalKind(error)
                guard let expect = op.expect else {
                    throw DriverError(
                        "\(scenario.name) op \(index) (\(op.op)) threw \(kind) "
                            + "with no expect.")
                }
                guard kind == expect else {
                    throw DriverError(
                        "\(scenario.name) op \(index) (\(op.op)) expected "
                            + "\(expect) but threw \(kind).")
                }
                refusals.append((index, kind, refusedPath))
            }
        }

        try enforceCheckpointInvariants(scenario, checkpoints)
        let terminal = try captureTree(vaultRoot)
        let artifact = try artifactJson(
            scenario.name,
            checkpoints: checkpoints,
            reports: reports,
            refusals: refusals,
            terminal: terminal,
            session: session)
        return (artifact, terminal)
    }

    private static func execute(
        _ session: VaultSession,
        _ op: Op,
        structuralByIndex: inout [Int: StructuralReport],
        batchByIndex: inout [Int: BatchMoveReport],
        index: Int,
        opIdOrder: inout [Int64]
    ) throws -> String? {
        switch op.op {
        case "createExclusive":
            let save = try session.createExclusive(path: op.path!, content: op.content!)
            return normalizeSave(save)
        case "createFolder":
            let report = try session.createFolder(path: op.path!)
            structuralByIndex[index] = report
            return normalizeStructural(report, &opIdOrder)
        case "renameFile":
            let report = try session.renameFile(path: op.path!, newName: op.newName!)
            structuralByIndex[index] = report
            return normalizeStructural(report, &opIdOrder)
        case "renameFolderWithNote":
            let report = try session.renameFolderWithNote(
                path: op.path!, newName: op.newName!)
            structuralByIndex[index] = report
            return normalizeStructural(report, &opIdOrder)
        case "moveFile":
            let report = try session.moveFile(path: op.path!, newParent: op.newParent!)
            structuralByIndex[index] = report
            return normalizeStructural(report, &opIdOrder)
        case "moveFolder":
            let report = try session.moveFolder(path: op.path!, newParent: op.newParent!)
            structuralByIndex[index] = report
            return normalizeStructural(report, &opIdOrder)
        case "batchMove":
            let report = try session.batchMove(
                request: BatchMoveRequest(
                    items: op.items!.map {
                        StructuralBatchItem(path: $0, isDirectory: false)
                    },
                    newParent: op.destination!))
            batchByIndex[index] = report
            return normalizeBatch(report, &opIdOrder)
        case "deleteFile":
            try session.deleteFile(path: op.path!)
            return nil
        case "undoReport":
            guard let forward = structuralByIndex[op.opRef!] else {
                throw DriverError("op \(op.opRef!): no structural report recorded")
            }
            var last: StructuralReport?
            for opId in forward.undoOpIds {
                last = try session.undoStructural(opId: opId)
            }
            if let last {
                structuralByIndex[index] = last
                return normalizeStructural(last, &opIdOrder)
            }
            return nil
        case "undoBatchMove":
            guard let forward = batchByIndex[op.opRef!], let opId = forward.opId else {
                throw DriverError("op \(op.opRef!): batch report has no OpId to undo")
            }
            let report = try session.undoBatchMove(opId: opId)
            batchByIndex[index] = report
            return normalizeBatch(report, &opIdOrder)
        default:
            throw DriverError("unknown op '\(op.op)'")
        }
    }

    /// The refusal kind names are the C# exception class names — the
    /// committed goldens spell them that way.
    private static func refusalKind(_ error: VaultError) -> (String, String?) {
        switch error {
        case .Io: return ("Io", nil)
        case .Db: return ("Db", nil)
        case .InvalidPath: return ("InvalidPath", nil)
        case .Trash: return ("Trash", nil)
        case .Cancelled: return ("Cancelled", nil)
        case .InvalidUtf8: return ("InvalidUtf8", nil)
        case .FileTooLarge: return ("FileTooLarge", nil)
        case .InvalidQuery: return ("InvalidQuery", nil)
        case .Unsupported: return ("Unsupported", nil)
        case .InvalidArgument: return ("InvalidArgument", nil)
        case let .DestinationExists(path): return ("DestinationExists", path)
        case .WriteConflict: return ("WriteConflict", nil)
        case .SavedButUnindexed: return ("SavedButUnindexed", nil)
        case .HistoryUnavailable: return ("HistoryUnavailable", nil)
        case .MalformedFrontmatter: return ("MalformedFrontmatter", nil)
        case .BibSourceUnreadable: return ("BibSourceUnreadable", nil)
        case .CslStyleUnreadable: return ("CslStyleUnreadable", nil)
        case .PrefsUnreadable: return ("PrefsUnreadable", nil)
        }
    }

    private static func enforceCheckpointInvariants(
        _ scenario: Scenario,
        _ checkpoints: [(label: String, tree: [TreeEntry])]
    ) throws {
        func tree(_ label: String) throws -> [TreeEntry] {
            for (candidate, tree) in checkpoints where candidate == label {
                return tree
            }
            throw DriverError(
                "\(scenario.name): assertEqual names unknown checkpoint '\(label)'")
        }
        for pair in scenario.assertEqual ?? [] {
            guard try tree(pair[0]) == tree(pair[1]) else {
                throw DriverError(
                    "\(scenario.name): checkpoints '\(pair[0])' and '\(pair[1])' "
                        + "differ — a rollback/undo was not byte-exact "
                        + "(invariants 3/7).")
            }
        }
        for path in scenario.untouched ?? [] {
            var hash: String?
            for (label, tree) in checkpoints {
                guard let entry = tree.first(where: { $0.path == path }) else {
                    throw DriverError(
                        "\(scenario.name): untouched file '\(path)' missing at "
                            + "checkpoint '\(label)' (invariant 1).")
                }
                if hash == nil { hash = entry.sha256 }
                guard entry.sha256 == hash else {
                    throw DriverError(
                        "\(scenario.name): untouched file '\(path)' changed by "
                            + "checkpoint '\(label)' (invariant 1).")
                }
            }
        }
    }

    private static func captureTree(_ vaultRoot: URL) throws -> [TreeEntry] {
        // The STRING enumerator yields RELATIVE subpaths by
        // construction — no prefix math against the root, which the
        // /var vs /private/var symlink quirks defeated twice (the URL
        // enumerator returns resolved paths while Foundation's
        // resolvingSymlinksInPath deliberately leaves /var alone).
        let fm = FileManager.default
        var entries: [TreeEntry] = []
        guard let enumerator = fm.enumerator(atPath: vaultRoot.path) else {
            throw DriverError("could not enumerate \(vaultRoot.path)")
        }
        for case let relative as String in enumerator {
            if relative == ".slate" || relative.hasPrefix(".slate/") {
                continue
            }
            var isDirectory: ObjCBool = false
            let full = vaultRoot.appendingPathComponent(relative)
            guard fm.fileExists(atPath: full.path, isDirectory: &isDirectory),
                !isDirectory.boolValue
            else { continue }
            let bytes = try Data(contentsOf: full)
            entries.append(
                TreeEntry(
                    path: relative,
                    size: Int64(bytes.count),
                    sha256: sha256Hex(bytes)))
        }
        entries.sort {
            Array($0.path.utf16).lexicographicallyPrecedes(Array($1.path.utf16))
        }
        return entries
    }

    private static func sha256Hex(_ data: Data) -> String {
        SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined()
    }

    // MARK: - Normalization (H2 — mirror MutationHarness.cs exactly)

    private static func relativeOpId(_ opId: Int64, _ order: inout [Int64]) -> Int64 {
        if let index = order.firstIndex(of: opId) {
            return Int64(index)
        }
        order.append(opId)
        return Int64(order.count - 1)
    }

    private static func normalizeSave(_ save: SaveReport) -> String {
        let j = CanonicalJsonM()
        j.raw("{\"kind\":\"save\",\"newContentHash\":").str(save.newContentHash)
            .raw(",\"newSizeBytes\":").num(save.newSizeBytes)
            .raw("}")
        return j.output
    }

    private static func normalizeStructural(
        _ report: StructuralReport, _ opIdOrder: inout [Int64]
    ) -> String {
        let j = CanonicalJsonM()
        j.raw("{\"kind\":\"structural\",\"opId\":")
            .num(relativeOpId(report.opId, &opIdOrder))
        j.raw(",\"undoOpIds\":[")
        for (i, opId) in report.undoOpIds.enumerated() {
            if i > 0 { j.raw(",") }
            j.num(relativeOpId(opId, &opIdOrder))
        }
        j.raw("],\"moved\":[")
        for (i, moved) in report.moved.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"old\":").str(moved.oldPath)
                .raw(",\"new\":").str(moved.newPath)
                .raw("}")
        }
        j.raw("],\"rewritten\":[")
        for (i, rewrite) in report.rewritten.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"path\":").str(rewrite.path)
                .raw(",\"hashBefore\":").str(rewrite.hashBefore)
                .raw(",\"hashAfter\":").str(rewrite.hashAfter)
                .raw("}")
        }
        j.raw("],\"failed\":[")
        for (i, failure) in report.failed.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"path\":").str(failure.path)
                .raw(",\"kind\":").str(failure.kind.kind)
                .raw("}")
        }
        j.raw("]}")
        return j.output
    }

    /// C# serializes enum members with their PascalCase names; Swift
    /// cases are lowerCamel — explicit maps, never String(describing:).
    private static func batchStateName(_ state: BatchMoveState) -> String {
        switch state {
        case .rejected: return "Rejected"
        case .noOp: return "NoOp"
        case .succeeded: return "Succeeded"
        case .rolledBack: return "RolledBack"
        case .rollbackIncomplete: return "RollbackIncomplete"
        }
    }

    private static func stageName(_ stage: BatchFailureStage) -> String {
        // Mirror C# BatchFailureStage.ToString() — the exhaustive
        // switch breaks the build when core grows a stage.
        switch stage {
        case .preflight: return "Preflight"
        case .move: return "Move"
        case .index: return "Index"
        case .linkRewrite: return "LinkRewrite"
        case .linkRewriteRestore: return "LinkRewriteRestore"
        case .journal: return "Journal"
        case .rollback: return "Rollback"
        case .trash: return "Trash"
        case .reconciliation: return "Reconciliation"
        case .recoveryBarrier: return "RecoveryBarrier"
        }
    }

    private static func opKindName(_ kind: OpKind) -> String {
        switch kind {
        case .wholeFileReplace: return "WholeFileReplace"
        case .editBatch: return "EditBatch"
        case .canvasApply: return "CanvasApply"
        case .annotated: return "Annotated"
        }
    }

    private static func normalizeBatch(
        _ report: BatchMoveReport, _ opIdOrder: inout [Int64]
    ) -> String {
        let j = CanonicalJsonM()
        j.raw("{\"kind\":\"batchMove\",\"state\":").str(batchStateName(report.state))
            .raw(",\"opId\":")
        if let opId = report.opId {
            j.num(relativeOpId(opId, &opIdOrder))
        } else {
            j.null()
        }
        j.raw(",\"standing\":[")
        for (i, change) in report.standing.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"old\":").str(change.oldPath)
                .raw(",\"new\":").str(change.newPath)
                .raw(",\"dir\":").bool(change.isDirectory)
                .raw("}")
        }
        j.raw("],\"rolledBack\":[")
        for (i, change) in report.rolledBack.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"old\":").str(change.oldPath)
                .raw(",\"new\":").str(change.newPath)
                .raw(",\"dir\":").bool(change.isDirectory)
                .raw("}")
        }
        j.raw("],\"failureStage\":")
        if let failure = report.failure {
            j.str(stageName(failure.stage))
        } else {
            j.null()
        }
        j.raw(",\"rollbackFailureStages\":[")
        for (i, failure) in report.rollbackFailures.enumerated() {
            if i > 0 { j.raw(",") }
            j.str(stageName(failure.stage))
        }
        j.raw("],\"requiresRescan\":").bool(report.requiresRescan)
            .raw("}")
        return j.output
    }

    // MARK: - Artifact

    private static func artifactJson(
        _ scenarioName: String,
        checkpoints: [(label: String, tree: [TreeEntry])],
        reports: [(opIndex: Int, body: String)],
        refusals: [(opIndex: Int, kind: String, path: String?)],
        terminal: [TreeEntry],
        session: VaultSession
    ) throws -> String {
        let j = CanonicalJsonM()
        j.raw("{\"scenario\":").str(scenarioName)

        j.raw(",\"checkpoints\":[")
        for (i, checkpoint) in checkpoints.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"label\":").str(checkpoint.label).raw(",\"tree\":")
            writeTree(j, checkpoint.tree)
            j.raw("}")
        }

        j.raw("],\"reports\":[")
        for (i, report) in reports.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"op\":").num(Int64(report.opIndex))
                .raw(",\"report\":").raw(report.body)
                .raw("}")
        }

        j.raw("],\"refusals\":[")
        for (i, refusal) in refusals.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"op\":").num(Int64(refusal.opIndex))
                .raw(",\"kind\":").str(refusal.kind)
                .raw(",\"path\":")
            if let path = refusal.path {
                j.str(path)
            } else {
                j.null()
            }
            j.raw("}")
        }

        j.raw("],\"tree\":")
        writeTree(j, terminal)

        j.raw(",\"oplogs\":[")
        let markdownFiles = terminal.map(\.path).filter { $0.hasSuffix(".md") }
        for (f, path) in markdownFiles.enumerated() {
            if f > 0 { j.raw(",") }
            j.raw("{\"path\":").str(path).raw(",\"entries\":[")
            let entries = try session.readOplog(path: path)
            for (i, entry) in entries.enumerated() {
                if i > 0 { j.raw(",") }
                j.raw("{\"opKind\":").str(opKindName(entry.opKind))
                    .raw(",\"actor\":").str(entry.userActorId)
                    .raw(",\"hashBefore\":").str(entry.contentHashBefore)
                    .raw(",\"hashAfter\":").str(entry.contentHashAfter)
                    .raw(",\"payloadSha256\":").str(sha256Hex(entry.payloadBytes))
                    .raw("}")
            }
            j.raw("]}")
        }

        j.raw("],\"links\":").raw(
            try linksArtifact(session: session, relPaths: markdownFiles))

        j.raw("}\n")
        return j.output
    }

    private static func writeTree(_ j: CanonicalJsonM, _ tree: [TreeEntry]) {
        j.raw("[")
        for (i, entry) in tree.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"path\":").str(entry.path)
                .raw(",\"size\":").num(entry.size)
                .raw(",\"sha256\":").str(entry.sha256)
                .raw("}")
        }
        j.raw("]")
    }

    /// The read harness's links artifact re-emitted over the terminal
    /// vault (H5-4) — a structural mirror of
    /// SurfaceSerializer.LinksArtifact, kept byte-compatible with the
    /// copy in ParityHarnessTests.swift.
    private static func linksArtifact(
        session: VaultSession, relPaths: [String]
    ) throws -> String {
        let j = CanonicalJsonM()
        j.raw("{\"files\":[")
        for (f, rel) in relPaths.enumerated() {
            if f > 0 { j.raw(",") }
            j.raw("{\"file\":").str(rel)

            j.raw(",\"outgoing\":[")
            let outgoing = try session.outgoingLinks(path: rel)
            for (i, o) in outgoing.enumerated() {
                if i > 0 { j.raw(",") }
                j.raw("{\"target\":")
                if let target = o.targetPath {
                    j.str(target)
                } else {
                    j.null()
                }
                j.raw(",\"raw\":").str(o.targetRaw)
                    .raw(",\"kind\":").str(o.kind)
                    .raw(",\"embed\":").bool(o.isEmbed)
                    .raw(",\"external\":").bool(o.isExternal)
                    .raw(",\"unresolved\":").bool(o.isUnresolved)
                    .raw(",\"ordinal\":").num(UInt64(o.ordinal))
                    .raw("}")
            }
            j.raw("]")

            j.raw(",\"backlinks\":[")
            let backlinks = try session.backlinks(
                path: rel, paging: Paging(cursor: nil, limit: 500)
            ).items
            for (i, b) in backlinks.enumerated() {
                if i > 0 { j.raw(",") }
                j.raw("{\"source\":").str(b.sourcePath)
                    .raw(",\"snippet\":").str(b.snippet)
                    .raw(",\"ordinal\":").num(UInt64(b.ordinal))
                    .raw(",\"kind\":").str(b.kind)
                    .raw(",\"embed\":").bool(b.isEmbed)
                    .raw("}")
            }
            j.raw("]}")
        }
        j.raw("]}")
        return j.output
    }
}

/// Canonical JSON writer — the Swift half of the fixed serialization
/// algorithm defined in `apps/slate-windows/tools/ParityHarness/
/// CanonicalJson.cs`, duplicated from ParityHarnessTests.swift (both
/// files are private-scope by design; change all implementations
/// together, never one).
private final class CanonicalJsonM {
    private(set) var output = ""

    @discardableResult
    func raw(_ s: String) -> CanonicalJsonM {
        output += s
        return self
    }

    @discardableResult
    func str(_ value: String) -> CanonicalJsonM {
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

    @discardableResult
    func num(_ value: Int64) -> CanonicalJsonM {
        output += String(value)
        return self
    }

    @discardableResult
    func num(_ value: UInt64) -> CanonicalJsonM {
        output += String(value)
        return self
    }

    @discardableResult
    func bool(_ value: Bool) -> CanonicalJsonM {
        output += value ? "true" : "false"
        return self
    }

    @discardableResult
    func null() -> CanonicalJsonM {
        output += "null"
        return self
    }
}
