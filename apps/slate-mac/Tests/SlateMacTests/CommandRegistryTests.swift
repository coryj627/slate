// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// FFI round-trip tests for the `CommandRegistry` exposed in
/// Milestone Q issue #312. Exercises the callback-interface
/// surface end-to-end: Swift implements `CommandAction`, registers
/// it through the FFI, and observes the side-effect when the
/// registry invokes it from Rust.
///
/// These tests are the only place in the codebase that uses the
/// `CommandAction` callback interface today — production wiring
/// lands in issue #314 (menu bridge).
final class CommandRegistryTests: XCTestCase {

    // MARK: - Fixture action

    /// Swift-side `CommandAction` that bumps a counter on each
    /// invoke. Mirrors the in-Rust `CountingAction` fixture in
    /// `commands.rs` tests so the FFI behaviour is observably
    /// equivalent.
    ///
    /// `@unchecked Sendable` because the Rust `CommandAction` trait
    /// is `Send + Sync` and the FFI invokes from a Rust-managed
    /// thread; the `NSLock` makes the mutable state safe. Future
    /// production actions (menu bridge #314, plugin commands V1.x)
    /// must follow the same pattern — see the `Sendable contract`
    /// note on the FFI trait's doc comment.
    final class CountingAction: CommandAction, @unchecked Sendable {
        private let lock = NSLock()
        private var _invocationCount: Int = 0
        private let failWith: CommandError?

        init(failWith: CommandError? = nil) {
            self.failWith = failWith
        }

        var invocationCount: Int {
            lock.lock()
            defer { lock.unlock() }
            return _invocationCount
        }

        func invoke() throws {
            lock.lock()
            _invocationCount += 1
            lock.unlock()
            if let err = failWith {
                throw err
            }
        }
    }

    /// Action that returns an oversized `ActionFailed.message` so
    /// we can validate the Rust-side truncation at the foreign
    /// trust boundary.
    final class HostileAction: CommandAction, @unchecked Sendable {
        let payload: String
        init(byteCount: Int) {
            self.payload = String(repeating: "x", count: byteCount)
        }
        func invoke() throws {
            throw CommandError.ActionFailed(message: payload)
        }
    }

    private func fixtureCommand(
        id: String,
        section: CommandSection = .file
    ) -> Command {
        Command(
            id: id,
            label: id,
            accessibilityHint: nil,
            hotkeyHint: nil,
            section: section
        )
    }

    // MARK: - Empty + list

    func testNewRegistryStartsEmpty() {
        let reg = CommandRegistry()
        XCTAssertTrue(reg.list().isEmpty)
    }

    func testListReturnsRegisteredCommands() {
        let reg = CommandRegistry()
        reg.register(
            command: fixtureCommand(id: "slate.test.alpha"),
            action: CountingAction()
        )
        let listed = reg.list()
        XCTAssertEqual(listed.count, 1)
        XCTAssertEqual(listed[0].id, "slate.test.alpha")
        XCTAssertEqual(listed[0].section, .file)
    }

    func testListIsSortedBySectionThenId() {
        // Mirror of the Rust core test; confirms the deterministic
        // ordering survives the FFI boundary intact.
        let reg = CommandRegistry()
        reg.register(command: fixtureCommand(id: "z", section: .file), action: CountingAction())
        reg.register(command: fixtureCommand(id: "a", section: .file), action: CountingAction())
        reg.register(command: fixtureCommand(id: "m", section: .view), action: CountingAction())
        reg.register(command: fixtureCommand(id: "b", section: .plugins), action: CountingAction())
        let ids = reg.list().map(\.id)
        XCTAssertEqual(ids, ["a", "z", "m", "b"])
    }

    // MARK: - findById

    func testFindByIdHitsAndMisses() {
        let reg = CommandRegistry()
        reg.register(command: fixtureCommand(id: "alpha"), action: CountingAction())
        XCTAssertEqual(reg.findById(id: "alpha")?.id, "alpha")
        XCTAssertNil(reg.findById(id: "missing"))
    }

    func testUnregisterRemovesCommandAcrossFFIBoundary() {
        let reg = CommandRegistry()
        let action = CountingAction()
        reg.register(command: fixtureCommand(id: "alpha"), action: action)

        XCTAssertTrue(reg.unregister(id: "alpha"))
        XCTAssertFalse(reg.unregister(id: "alpha"), "second removal is a miss")
        XCTAssertNil(reg.findById(id: "alpha"))
        XCTAssertTrue(reg.list().isEmpty)
        XCTAssertThrowsError(try reg.invokeById(id: "alpha")) { error in
            guard case CommandError.UnknownId(let id) = error else {
                XCTFail("expected UnknownId, got \(error)")
                return
            }
            XCTAssertEqual(id, "alpha")
        }
        XCTAssertEqual(action.invocationCount, 0, "removed action is never invoked")
    }

    // MARK: - invokeById

    func testInvokeByIdDispatchesToSwiftAction() throws {
        let reg = CommandRegistry()
        let action = CountingAction()
        reg.register(command: fixtureCommand(id: "alpha"), action: action)
        try reg.invokeById(id: "alpha")
        try reg.invokeById(id: "alpha")
        XCTAssertEqual(action.invocationCount, 2)
    }

    func testInvokeByIdReturnsUnknownIdForMissingCommand() {
        let reg = CommandRegistry()
        XCTAssertThrowsError(try reg.invokeById(id: "missing")) { error in
            guard case CommandError.UnknownId(let id) = error else {
                XCTFail("expected UnknownId, got \(error)")
                return
            }
            XCTAssertEqual(id, "missing")
        }
    }

    func testInvokeByIdPropagatesActionError() {
        let reg = CommandRegistry()
        let failing = CountingAction(failWith: .ActionFailed(message: "fixture failure"))
        let replaced = reg.register(command: fixtureCommand(id: "alpha"), action: failing)
        XCTAssertFalse(replaced, "first registration is not a replacement")
        XCTAssertThrowsError(try reg.invokeById(id: "alpha")) { error in
            guard case CommandError.ActionFailed(let message) = error else {
                XCTFail("expected ActionFailed, got \(error)")
                return
            }
            XCTAssertEqual(message, "fixture failure")
            XCTAssertEqual(failing.invocationCount, 1, "action ran exactly once before raising")
        }
    }

    // MARK: - register replace flag

    func testRegisterReturnsReplacedFlag() {
        let reg = CommandRegistry()
        let first = reg.register(command: fixtureCommand(id: "alpha"), action: CountingAction())
        let second = reg.register(command: fixtureCommand(id: "alpha"), action: CountingAction())
        XCTAssertFalse(first, "first registration is not a replacement")
        XCTAssertTrue(second, "second registration of the same id signals replacement")
    }

    // MARK: - Trust boundary: ActionFailed.message truncation

    /// Foreign-supplied action error messages are truncated by the
    /// Rust adapter so a hostile or buggy plugin can't flood logs.
    /// This test exercises the truncation end-to-end across the
    /// FFI boundary — Swift action returns an oversized message,
    /// Rust truncates, Swift observes the bounded result.
    func testActionFailedMessageIsTruncatedAtFFIBoundary() {
        let reg = CommandRegistry()
        // 1 MiB payload — far above any reasonable error message
        // and large enough to detect even a sloppy multi-step
        // truncation regression.
        let oversized = HostileAction(byteCount: 1 << 20)
        let replaced = reg.register(command: fixtureCommand(id: "hostile"), action: oversized)
        XCTAssertFalse(replaced)

        XCTAssertThrowsError(try reg.invokeById(id: "hostile")) { error in
            guard case CommandError.ActionFailed(let message) = error else {
                XCTFail("expected ActionFailed, got \(error)")
                return
            }
            // Truncated payload: well under the 1 MiB we sent, but
            // far above 0 — and ends with the "(truncated)" marker
            // the Rust adapter appends so renderers can show it
            // verbatim without being misleading.
            XCTAssertLessThan(
                message.utf8.count,
                2048,
                "truncated message must be bounded (got \(message.utf8.count) bytes)"
            )
            XCTAssertTrue(
                message.hasSuffix("(truncated)"),
                "truncated message must carry the truncation marker"
            )
        }
    }
}

// MARK: - The Slate catalog: Canvas: Color Marked Cards… (#1165, D-4)

/// W6-1 owner decision D-4: parity is the registry, so the bulk color verb
/// gets the front door every other canvas verb has — a command id in the
/// canvas section (the id Windows' chord table already mints), a palette
/// row, a hint and a menu item — and it behaves as Windows'
/// `RequestColorMarked` does: nothing marked says so, marked cards open the
/// Set Color sheet over the marked set, and the sheet's choice is ONE
/// action with ONE summary.
@MainActor
extension CommandRegistryTests {
    private static let colorMarkedID = "slate.canvas.colorMarked"

    func testColorMarkedIsRegisteredInTheCanvasSectionWithAHint() throws {
        let appState = AppState()
        let command = try XCTUnwrap(
            appState.commandRegistry.list().first { $0.id == Self.colorMarkedID },
            "the bulk color verb has no registry row")
        XCTAssertEqual(command.label, "Canvas: Color Marked Cards…")
        XCTAssertEqual(command.section, .canvas)
        XCTAssertFalse((command.accessibilityHint ?? "").isEmpty, "the palette row needs a hint")
        XCTAssertNil(command.hotkeyHint, "mac allocates no chord to the marked-set verbs")
        // Palette availability mirrors Group/Delete Marked: one canvas
        // authoring reason when the canvas cannot be edited.
        XCTAssertTrue(SlateCommandID.canvasMutationCommands.contains(Self.colorMarkedID))
        XCTAssertTrue(SlateCommandID.all.contains(Self.colorMarkedID))
    }

    func testColorMarkedWithNothingMarkedSaysSoAndOpensNoSheet() async throws {
        let (state, cleanup) = try await Self.canvasState()
        defer { cleanup() }
        var posted: [String] = []
        state.canvasAnnouncer = CanvasAnnouncer(verbosity: .standard, coalesceWindow: 60) {
            text, _ in posted.append(text)
        }

        try state.commandRegistry.invokeById(id: Self.colorMarkedID)
        state.canvasAnnouncer.flushForTests()

        XCTAssertNil(state.canvasPrompt, "nothing to color opens nothing")
        XCTAssertEqual(
            posted, [a11yRender(event: .canvas(event: .canvasStatus(note: .noMarks))).text])
    }

    func testColorMarkedOpensTheColorSheetOverTheMarkedSet() async throws {
        let (state, cleanup) = try await Self.canvasState()
        defer { cleanup() }
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        doc.selection.marked = ["a", "b"]
        var posted: [String] = []
        state.canvasAnnouncer = CanvasAnnouncer(verbosity: .standard, coalesceWindow: 60) {
            text, _ in posted.append(text)
        }

        try state.commandRegistry.invokeById(id: Self.colorMarkedID)
        XCTAssertEqual(state.canvasPrompt?.id, "colorMarked", "the marked set's color sheet")

        // The sheet's choice: the bulk verb through the prompt-commit seam.
        let undoBefore = doc.undoStack.count
        XCTAssertTrue(state.commitCanvasPromptMutation { state.canvasColorMarked(preset: 4) })
        state.canvasAnnouncer.flushForTests()
        XCTAssertEqual(doc.undoStack.count, undoBefore + 1, "one action, one undo")
        XCTAssertEqual(posted, ["Set 2 cards to green."])
        XCTAssertEqual(doc.tableRows.first { $0.nodeId == "a" }?.colorName, "green")
        XCTAssertEqual(doc.tableRows.first { $0.nodeId == "b" }?.colorName, "green")
        XCTAssertEqual(doc.selection.marked, ["a", "b"], "coloring keeps the marks")
    }

    /// The menu item: mac's Canvas verbs live in the View menu's toolbar
    /// group (the group `MacCatalogParityTests` reads as mac's Canvas
    /// menu); Color Marked sits there with the registry's label and opens
    /// the same sheet the palette row does.
    func testTheCanvasMenuCarriesColorMarkedBesideTheMarkVerb() throws {
        let app = try Self.macSource("SlateMacApp.swift")
        let start = try XCTUnwrap(app.range(of: "CommandGroup(after: .toolbar)"))
        let rest = app[start.upperBound...]
        let group = rest.range(of: "CommandGroup(").map { rest[..<$0.lowerBound] } ?? rest
        XCTAssertTrue(group.contains("Button(\"Canvas: Color Marked Cards…\")"), "no menu item")
        XCTAssertTrue(group.contains("appState.canvasPromptColorMarked()"))
        XCTAssertTrue(group.contains("appState.canvasToggleMark()"))
    }

    // MARK: Fixtures

    private static func canvasState() async throws -> (AppState, () -> Void) {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("color-marked-\(UUID().uuidString)")
        let vault = root.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data(
            """
            {"nodes":[
            {"id":"a","type":"text","text":"Alpha","x":0,"y":0,"width":200,"height":100},
            {"id":"b","type":"text","text":"Beta","x":220,"y":0,"width":200,"height":100},
            {"id":"c","type":"text","text":"Gamma","x":440,"y":0,"width":200,"height":100}
            ],"edges":[]}
            """.utf8
        ).write(to: vault.appendingPathComponent("board.canvas"))
        let store = RecentVaultsStore(fileURL: root.appendingPathComponent("recents.json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("board.canvas", target: .currentTab)
        return (state, { try? FileManager.default.removeItem(at: root) })
    }

    private static func macSource(_ name: String) throws -> String {
        let url = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .appendingPathComponent("Sources/SlateMac")
            .appendingPathComponent(name)
        return try String(contentsOf: url, encoding: .utf8)
    }
}
