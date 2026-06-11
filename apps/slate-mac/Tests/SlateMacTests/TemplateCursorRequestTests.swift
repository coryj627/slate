// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Combine
import XCTest

@testable import SlateMac

/// #421 (F-H1): the `{{cursor}}` park must survive the
/// publish-before-subscribe race. The create-from-template flow
/// sends the offset right after the note load resolves — often a
/// runloop tick before SwiftUI materializes the editor and
/// subscribes. A PassthroughSubject dropped that event silently
/// (caret landed at end-of-text in the VO test); the
/// CurrentValueSubject replays it on subscribe, and the
/// NoteContentView delivery chain clears it so it's one-shot.
@MainActor
final class TemplateCursorRequestTests: XCTestCase {

    private func makeState() throws -> AppState {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-cursor-req-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let store = RecentVaultsStore(fileURL: dir.appendingPathComponent("recents.json"))
        return AppState(recentsStore: store, externalOpener: { _ in true })
    }

    /// The exact delivery chain NoteContentView builds.
    private func deliveryChain(for state: AppState) -> AnyPublisher<Int, Never> {
        state.cursorByteOffsetRequest
            .compactMap { $0 }
            .handleEvents(receiveOutput: { [state] _ in
                state.clearPendingCursorByteOffset()
            })
            .eraseToAnyPublisher()
    }

    func testOffsetSentBeforeSubscribeStillDelivers() throws {
        let state = try makeState()
        // Send FIRST — no subscriber yet (the race the VO test hit).
        state.cursorByteOffsetRequest.send(42)

        var received: [Int] = []
        var cancellables = Set<AnyCancellable>()
        deliveryChain(for: state)
            .sink { received.append($0) }
            .store(in: &cancellables)

        XCTAssertEqual(received, [42], "replay-on-subscribe must deliver the pre-subscription park")
        XCTAssertNil(
            state.cursorByteOffsetRequest.value,
            "delivery must clear the pending value — the park is one-shot"
        )

        // A LATER subscriber (editor re-attach) must not re-park.
        var late: [Int] = []
        deliveryChain(for: state)
            .sink { late.append($0) }
            .store(in: &cancellables)
        XCTAssertTrue(late.isEmpty, "a consumed park must not replay on re-subscribe")
    }

    func testLiveSendAfterSubscribeDeliversOnce() throws {
        let state = try makeState()
        var received: [Int] = []
        var cancellables = Set<AnyCancellable>()
        deliveryChain(for: state)
            .sink { received.append($0) }
            .store(in: &cancellables)

        state.cursorByteOffsetRequest.send(7)
        XCTAssertEqual(received, [7])
        XCTAssertNil(state.cursorByteOffsetRequest.value)
    }

    /// Red-team note 4: the selection-change clear — a pending park
    /// must never ride into a different note.
    func testSelectionChangeClearsPendingPark() async throws {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-cursor-sel-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let vault = dir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# A\n".utf8).write(to: vault.appendingPathComponent("a.md"))
        try Data("# B\n".utf8).write(to: vault.appendingPathComponent("b.md"))
        let store = RecentVaultsStore(fileURL: dir.appendingPathComponent("recents.json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value

        state.cursorByteOffsetRequest.send(3)
        state.selectedFilePath = "b.md"

        XCTAssertNil(
            state.cursorByteOffsetRequest.value,
            "switching notes must clear a pending {{cursor}} park"
        )
    }

    func testClearPendingIsIdempotent() throws {
        let state = try makeState()
        state.clearPendingCursorByteOffset()
        state.cursorByteOffsetRequest.send(9)
        state.clearPendingCursorByteOffset()
        state.clearPendingCursorByteOffset()
        XCTAssertNil(state.cursorByteOffsetRequest.value)
    }
}
