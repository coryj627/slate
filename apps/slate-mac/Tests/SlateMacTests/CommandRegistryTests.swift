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
