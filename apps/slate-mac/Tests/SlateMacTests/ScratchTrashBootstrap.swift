// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

@testable import SlateMac

/// #1455: this test process's scratch Trash bin. Every vault the suite opens
/// trashes into it, whether through AppState's own open (the shipping
/// `VaultSession.openFilesystem`) or a session a test opens itself, and so
/// does every other core path that trashes (a single delete, a batch, undoing
/// a new folder). Nothing reaches the `~/.Trash` of whoever runs the suite.
///
/// `SlateMacTestBootstrap` routes the process here as the test bundle loads,
/// before XCTest runs a test (`slateMacTestsRouteTrashToScratchBin`), and
/// `ScratchTrashCensusTests` keeps it so.
enum ScratchTrashBin {
    /// The bin's folder: in this process's temporary directory, the volume
    /// the tests' vaults live on, and removed when the process exits.
    nonisolated(unsafe) private(set) static var url: URL?

    /// The bin's entry names, sorted.
    static func contents() throws -> [String] {
        guard let url else { return [] }
        return try FileManager.default.contentsOfDirectory(atPath: url.path).sorted()
    }

    fileprivate static func install(_ bin: URL) {
        url = bin
    }
}

/// The Swift half of `SlateMacTestBootstrap` (bootstrap.c), called once by
/// its constructor as the test bundle loads. It makes the bin and routes the
/// process's Trash into it through core. A process that cannot route would
/// move every fixture it trashes into the user's Trash, so it stops here,
/// before any test runs.
@_cdecl("slate_mac_tests_route_trash_to_scratch_bin")
func slateMacTestsRouteTrashToScratchBin() {
    let bin = FileManager.default.temporaryDirectory.appendingPathComponent(
        "slate-scratch-trash-\(getpid())-\(UUID().uuidString)", isDirectory: true)
    do {
        try FileManager.default.createDirectory(at: bin, withIntermediateDirectories: true)
        try routeTrashToScratchBinForTests(binPath: bin.path)
    } catch {
        fatalError(
            "#1455: could not route this test process's Trash into a scratch bin at "
                + "\(bin.path), so its tests would move their fixtures into your Trash: \(error)")
    }
    ScratchTrashBin.install(bin)
    atexit {
        if let bin = ScratchTrashBin.url {
            try? FileManager.default.removeItem(at: bin)
        }
    }
}
