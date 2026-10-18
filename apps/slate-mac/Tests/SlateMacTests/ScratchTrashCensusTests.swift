// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #1455: the mac suite never moves its fixtures into the `~/.Trash` of
/// whoever runs it. Before it, one `swift test --parallel` sent every
/// fixture a test trashed through core (`CanonicalIdentityTests`' `Ghost.md`,
/// and every Trash fact on a real session) to the running user's Trash,
/// where same-named items from parallel workers also contended (#1450).
///
/// `SlateMacTestBootstrap` routes each test process's Trash into its own
/// scratch bin as the bundle loads (`ScratchTrashBin`). These facts check,
/// before anything is trashed, that the routing is in place, then count the
/// bin's entries, never the user's Trash. They also hold the shipping app to
/// the system Trash: no production source routes it.
@MainActor
final class ScratchTrashCensusTests: XCTestCase {
    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("scratch-trash-census-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    private struct NotRouted: Error, CustomStringConvertible {
        let description: String
    }

    /// The bin this process routes into. It throws, failing the fact, unless
    /// core reports the routing: no fact here trashes anything before that.
    private func routedBin() throws -> URL {
        guard let bin = ScratchTrashBin.url else {
            throw NotRouted(
                description:
                    "SlateMacTestBootstrap did not route this process's Trash into a scratch bin")
        }
        guard scratchTrashBinForTests() == bin.path else {
            throw NotRouted(
                description: "core does not route this process's Trash into \(bin.path)")
        }
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: bin.path, isDirectory: &isDirectory),
            isDirectory.boolValue
        else {
            throw NotRouted(description: "the scratch bin \(bin.path) is not a folder")
        }
        return bin
    }

    /// A vault holding one uniquely named note, so the bin's count for that
    /// name is this fact's own whatever else the process has trashed.
    private func makeVault() throws -> (vault: URL, note: String) {
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        let note = "census-1455-\(UUID().uuidString).md"
        try "# Census\n".write(
            to: vault.appendingPathComponent(note), atomically: true, encoding: .utf8)
        return (vault, note)
    }

    func testTheBootstrapRoutedThisProcessIntoAScratchBin() throws {
        let bin = try routedBin()
        XCTAssertTrue(
            bin.lastPathComponent.hasPrefix("slate-scratch-trash-\(getpid())-"), bin.path)
        XCTAssertEqual(
            bin.deletingLastPathComponent().standardizedFileURL.path,
            FileManager.default.temporaryDirectory.standardizedFileURL.path,
            "the bin must share the temporary directory's volume with the tests' vaults")
    }

    /// The path most facts take: a vault opened through AppState's own open,
    /// a note trashed through `deleteEntry`, as the app does.
    func testATrashThroughAppStateLandsInTheScratchBin() async throws {
        let bin = try routedBin()
        let (vault, note) = try makeVault()
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        let before = try ScratchTrashBin.contents().filter { $0 == note }.count

        await state.deleteEntry(path: note, isDirectory: false)?.value

        XCTAssertFalse(
            FileManager.default.fileExists(atPath: vault.appendingPathComponent(note).path),
            "the note is still in the vault")
        XCTAssertEqual(before, 0)
        XCTAssertEqual(
            try ScratchTrashBin.contents().filter { $0 == note }.count, 1,
            "the note did not land in the scratch bin \(bin.path)")
    }

    /// Some facts open a session themselves; it is routed the same way.
    func testATrashThroughASessionATestOpensLandsInTheScratchBin() throws {
        _ = try routedBin()
        let (vault, note) = try makeVault()
        let session = try VaultSession.openFilesystem(rootPath: vault.path)
        let cancel = CancelToken()
        _ = try session.scanInitial(cancel: cancel)

        try session.deleteFile(path: note)

        XCTAssertFalse(
            FileManager.default.fileExists(atPath: vault.appendingPathComponent(note).path))
        XCTAssertEqual(try ScratchTrashBin.contents().filter { $0 == note }.count, 1)
    }

    /// The shipping app keeps the system Trash: no production source routes
    /// the Trash or asks where it is routed.
    func testNoProductionSourceRoutesTheTrash() throws {
        let sources = Self.packageRoot().appendingPathComponent("Sources/SlateMac")
        let files = try Self.swiftFiles(under: sources)
            // The generated bindings define the functions; they call nothing.
            .filter { $0.lastPathComponent != "slate_uniffi.swift" }
        XCTAssertGreaterThan(files.count, 100, "the walk must see the whole app")
        let routing = try files.filter { file in
            let source = try String(contentsOf: file, encoding: .utf8)
            return source.contains("routeTrashToScratchBinForTests")
                || source.contains("scratchTrashBinForTests")
        }
        XCTAssertEqual(
            routing.map(\.lastPathComponent), [],
            "production code must never route the Trash into a scratch bin (#1455)")
    }

    /// The bootstrap alone routes the process, once, as it loads: a test that
    /// routed again could only fail or point the bin somewhere else.
    func testOnlyTheBootstrapRoutesTheTrash() throws {
        let tests = Self.packageRoot().appendingPathComponent("Tests/SlateMacTests")
        let routing = try Self.swiftFiles(under: tests)
            .filter { $0.lastPathComponent != "ScratchTrashCensusTests.swift" }
            .filter { file in
                try String(contentsOf: file, encoding: .utf8)
                    .contains("routeTrashToScratchBinForTests(")
            }
            .map(\.lastPathComponent)
        XCTAssertEqual(routing, ["ScratchTrashBootstrap.swift"])
    }

    private static func packageRoot() -> URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
    }

    private static func swiftFiles(under root: URL) throws -> [URL] {
        guard
            let enumerator = FileManager.default.enumerator(
                at: root, includingPropertiesForKeys: nil)
        else {
            throw CocoaError(.fileReadNoSuchFile, userInfo: [NSFilePathErrorKey: root.path])
        }
        return enumerator.compactMap { $0 as? URL }
            .filter { $0.pathExtension == "swift" }
            .sorted { $0.path < $1.path }
    }
}
