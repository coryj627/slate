// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// FL4-2 (#663): the sidebar filter's app-side state machine and the
/// host-built date-window boundaries. Core determinism was proven in
/// `sidebar_filter_exec.rs`; these tests prove the app supplies exact
/// boundary instants (byte-for-byte, DST included) and honors the
/// lifecycle rules: debounce, wholesale replacement, inline errors with
/// retained results, announce dedup, restore-not-apply, and
/// date-term-gated rollover refresh.
@MainActor
final class SidebarFilterModelTests: XCTestCase {

    // MARK: - Window builder (spec rule 7)

    private let newYork = TimeZone(identifier: "America/New_York")!

    /// 2026-03-08 spring-forward (23 h local day), the exact instants
    /// `sidebar_filter_exec.rs` pins: 2026-03-08T00:00:00-05:00 =
    /// 1772946000000 → 2026-03-09T00:00:00-04:00 = 1773028800000.
    private let springStartMs: Int64 = 1_772_946_000_000
    private let springEndMs: Int64 = 1_773_028_800_000
    /// 2026-11-01 fall-back (25 h): 1793505600000 → 1793595600000.
    private let fallStartMs: Int64 = 1_793_505_600_000
    private let fallEndMs: Int64 = 1_793_595_600_000

    private func windows(
        _ requirements: [String], nowMs: Int64, timeZone: TimeZone
    ) -> [SidebarFilterDateWindow]? {
        SidebarFilterWindowBuilder.windows(
            forRequirements: requirements,
            now: Date(timeIntervalSince1970: Double(nowMs) / 1000),
            timeZone: timeZone,
            resolver: SidebarProductionCivilDateResolver())
    }

    func testTodayWindowOnSpringForwardDayIsExactly23Hours() throws {
        // 01:00 EST, before the jump — any instant inside the local day.
        let built = try XCTUnwrap(windows(
            ["@today"], nowMs: springStartMs + 3_600_000, timeZone: newYork))
        XCTAssertEqual(built, [
            SidebarFilterDateWindow(
                term: "@today", startMs: springStartMs, endMs: springEndMs)
        ])
        XCTAssertEqual(built[0].endMs - built[0].startMs, 23 * 3_600_000)
    }

    func testTodayWindowOnFallBackDayIsExactly25Hours() throws {
        let built = try XCTUnwrap(windows(
            ["@today"], nowMs: fallStartMs + 3_600_000, timeZone: newYork))
        XCTAssertEqual(built, [
            SidebarFilterDateWindow(
                term: "@today", startMs: fallStartMs, endMs: fallEndMs)
        ])
        XCTAssertEqual(built[0].endMs - built[0].startMs, 25 * 3_600_000)
    }

    func testYesterdayFromMarchNinthIsTheSpringForwardDay() throws {
        // Now = 2026-03-09 noon EDT (16:00Z).
        let built = try XCTUnwrap(windows(
            ["@yesterday"], nowMs: 1_773_072_000_000, timeZone: newYork))
        XCTAssertEqual(built, [
            SidebarFilterDateWindow(
                term: "@yesterday", startMs: springStartMs, endMs: springEndMs)
        ])
    }

    func testLast7dSpanningSpringForwardLosesExactlyOneHour() throws {
        // Now = 2026-03-10 noon EDT. Window = Mar 4 00:00 EST →
        // Mar 11 00:00 EDT: seven local days, one of them 23 h.
        let built = try XCTUnwrap(windows(
            ["@last7d"], nowMs: 1_773_158_400_000, timeZone: newYork))
        XCTAssertEqual(built, [
            SidebarFilterDateWindow(
                term: "@last7d",
                startMs: 1_772_600_400_000,
                endMs: 1_773_201_600_000)
        ])
        XCTAssertEqual(
            built[0].endMs - built[0].startMs, 7 * 86_400_000 - 3_600_000)
    }

    func testLiteralDateWindowUsesTheSharedResolver() throws {
        // The literal spring-forward day resolves through FL-01's
        // resolver to the same 23-hour pair — not now-relative.
        let built = try XCTUnwrap(windows(
            ["@2026-03-08"], nowMs: fallStartMs, timeZone: newYork))
        XCTAssertEqual(built, [
            SidebarFilterDateWindow(
                term: "@2026-03-08", startMs: springStartMs, endMs: springEndMs)
        ])
    }

    func testUnresolvableLiteralDateReturnsNil() {
        XCTAssertNil(windows(
            ["@2026-02-30"], nowMs: springStartMs, timeZone: newYork))
    }

    // MARK: - Model harness

    private final class Recorder {
        var performCalls: [(query: String, windows: [SidebarFilterDateWindow], paging: Paging)] = []
        var announcements: [String] = []
        var pages: [String: SidebarFilterPage] = [:]
        var cursorPages: [String: SidebarFilterPage] = [:]
        var errors: [String: Error] = [:]
        var requirementsByQuery: [String: [String]] = [:]
        var nowMs: Int64 = 1_772_949_600_000
    }

    private func makeModel(
        _ recorder: Recorder,
        debounceNanoseconds: UInt64 = 0,
        defaults: UserDefaults
    ) -> SidebarFilterModel {
        let model = SidebarFilterModel()
        model.bind(SidebarFilterModel.Dependencies(
            requirements: { query in
                recorder.requirementsByQuery[query] ?? []
            },
            perform: { query, windows, paging in
                recorder.performCalls.append((query, windows, paging))
                if let error = recorder.errors[query] { throw error }
                if let cursor = paging.cursor {
                    if let page = recorder.cursorPages[cursor] { return page }
                    XCTFail("unexpected cursor \(cursor)")
                }
                if let page = recorder.pages[query] { return page }
                return SidebarFilterPage(
                    files: [], nextCursor: nil, total: 0,
                    audioSummary: "No results.")
            },
            announce: { recorder.announcements.append($0) },
            now: { Date(timeIntervalSince1970: Double(recorder.nowMs) / 1000) },
            timeZone: { self.newYork },
            resolver: SidebarProductionCivilDateResolver(),
            defaults: defaults,
            debounceNanoseconds: debounceNanoseconds))
        return model
    }

    private func freshDefaults(_ name: String = #function) -> UserDefaults {
        let suite = "SidebarFilterModelTests.\(name).\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suite)!
        defaults.removePersistentDomain(forName: suite)
        return defaults
    }

    private func file(_ path: String) -> FileSummary {
        FileSummary(
            path: path, name: (path as NSString).lastPathComponent, mtimeMs: 0,
            sizeBytes: 0, isMarkdown: true, displayName: nil, createdDate: nil,
            createdMs: nil, wordCount: nil, preview: nil, taskTotal: 0,
            taskOpen: 0)
    }

    private func page(
        _ paths: [String], nextCursor: String? = nil, total: UInt64? = nil,
        summary: String? = nil
    ) -> SidebarFilterPage {
        SidebarFilterPage(
            files: paths.map(file),
            nextCursor: nextCursor,
            total: total ?? UInt64(paths.count),
            audioSummary: summary ?? "\(paths.count) results.")
    }

    // MARK: - Lifecycle (spec rule 3)

    func testDebounceCancelsSupersededEditsAndCommitsTrimmedText() async {
        let recorder = Recorder()
        recorder.pages["alpha"] = page(["a.md"])
        let model = makeModel(recorder, defaults: freshDefaults())

        // A superseded edit's pending commit is cancelled: with an
        // effectively-infinite debounce, the first task must die when
        // the second edit arrives, and nothing runs.
        model.bind(SidebarFilterModel.Dependencies(
            requirements: { _ in [] },
            perform: { query, windows, paging in
                recorder.performCalls.append((query, windows, paging))
                return self.page([])
            },
            announce: { recorder.announcements.append($0) },
            now: { Date(timeIntervalSince1970: 0) },
            timeZone: { self.newYork },
            resolver: SidebarProductionCivilDateResolver(),
            defaults: freshDefaults(),
            debounceNanoseconds: 3_600_000_000_000))
        model.fieldText = "al"
        model.fieldTextChanged()
        let first = model.pendingCommitTaskForTesting
        model.fieldText = "alpha"
        model.fieldTextChanged()
        await first?.value
        XCTAssertTrue(recorder.performCalls.isEmpty)

        // Zero-debounce path: the pending task commits the latest text.
        let defaults = freshDefaults()
        let liveModel = makeModel(recorder, defaults: defaults)
        liveModel.fieldText = "  alpha  "
        liveModel.fieldTextChanged()
        await liveModel.pendingCommitTaskForTesting?.value
        XCTAssertEqual(recorder.performCalls.map(\.query), ["alpha"])
        XCTAssertEqual(recorder.performCalls[0].paging.cursor, nil)
        XCTAssertEqual(
            recorder.performCalls[0].paging.limit, SidebarFilterModel.pageLimit)
        XCTAssertEqual(liveModel.committedQuery, "alpha")
    }

    func testCommitReplacesResultsWholesaleAndPersistsCommittedQuery() {
        let recorder = Recorder()
        recorder.pages["alpha"] = page(["a.md", "b.md"])
        recorder.pages["beta"] = page(["c.md"])
        let defaults = freshDefaults()
        let model = makeModel(recorder, defaults: defaults)

        model.fieldText = "alpha"
        model.commit()
        XCTAssertEqual(model.results?.files.map(\.path), ["a.md", "b.md"])
        XCTAssertEqual(
            defaults.string(forKey: SidebarFilterModel.persistedQueryKey),
            "alpha")

        model.fieldText = "beta"
        model.commit()
        XCTAssertEqual(model.results?.files.map(\.path), ["c.md"])
        XCTAssertTrue(model.isActive)
        XCTAssertEqual(
            defaults.string(forKey: SidebarFilterModel.persistedQueryKey),
            "beta")
    }

    func testEmptyCommitDeactivatesAndReturnsToTree() {
        let recorder = Recorder()
        recorder.pages["alpha"] = page(["a.md"])
        let defaults = freshDefaults()
        let model = makeModel(recorder, defaults: defaults)

        model.fieldText = "alpha"
        model.commit()
        XCTAssertTrue(model.isActive)

        model.fieldText = "   "
        model.commit()
        XCTAssertFalse(model.isActive)
        XCTAssertNil(model.results)
        XCTAssertNil(model.inlineError)
        XCTAssertEqual(
            defaults.string(forKey: SidebarFilterModel.persistedQueryKey), "")
        // Deactivation performs no query.
        XCTAssertEqual(recorder.performCalls.count, 1)
    }

    func testEscapeInFieldClearsQueryAndPersistedState() {
        let recorder = Recorder()
        recorder.pages["alpha"] = page(["a.md"])
        let defaults = freshDefaults()
        let model = makeModel(recorder, defaults: defaults)

        model.fieldText = "alpha"
        model.commit()
        model.escapeInField()
        XCTAssertEqual(model.fieldText, "")
        XCTAssertFalse(model.isActive)
        XCTAssertNil(model.results)
        XCTAssertEqual(
            defaults.string(forKey: SidebarFilterModel.persistedQueryKey), "")
    }

    // MARK: - Errors (spec rule 4)

    func testInvalidQueryRendersInlineAndRetainsPriorResultsAndPersistence() {
        let recorder = Recorder()
        recorder.pages["alpha"] = page(["a.md"])
        recorder.errors["ext:"] = VaultError.InvalidQuery(
            message: "\"ext:\" needs an extension, like ext:md.")
        let defaults = freshDefaults()
        let model = makeModel(recorder, defaults: defaults)

        model.fieldText = "alpha"
        model.commit()
        model.fieldText = "ext:"
        model.commit()

        XCTAssertEqual(
            model.inlineError, "\"ext:\" needs an extension, like ext:md.")
        XCTAssertEqual(
            model.results?.files.map(\.path), ["a.md"],
            "previous good results stay visible")
        XCTAssertEqual(model.committedQuery, "alpha")
        XCTAssertEqual(
            defaults.string(forKey: SidebarFilterModel.persistedQueryKey),
            "alpha",
            "a query that never committed is never the restored text")

        // A following good commit clears the inline error.
        model.fieldText = "alpha"
        model.commit()
        XCTAssertNil(model.inlineError)
    }

    // MARK: - Announce (spec rule 5)

    func testAnnounceDedupsOnQueryAndTotal() {
        let recorder = Recorder()
        recorder.pages["alpha"] = page(["a.md", "b.md"], summary: "2 results.")
        recorder.pages["beta"] = page(["c.md", "d.md"], summary: "2 results.")
        let model = makeModel(recorder, defaults: freshDefaults())

        model.fieldText = "alpha"
        model.commit()
        model.commit()
        XCTAssertEqual(recorder.announcements, ["2 results."])

        // Same query, changed total: announce again.
        recorder.pages["alpha"] = page(["a.md"], summary: "1 result.")
        model.commit()
        XCTAssertEqual(recorder.announcements, ["2 results.", "1 result."])

        // Different query, same total as its predecessor: announce.
        model.fieldText = "beta"
        model.commit()
        XCTAssertEqual(
            recorder.announcements, ["2 results.", "1 result.", "2 results."])
    }

    // MARK: - Persistence (spec rule 6)

    func testPersistedQueryRestoresIntoFieldButIsNotApplied() {
        let recorder = Recorder()
        let defaults = freshDefaults()
        defaults.set("has:task", forKey: SidebarFilterModel.persistedQueryKey)
        let model = makeModel(recorder, defaults: defaults)

        XCTAssertEqual(model.fieldText, "has:task")
        XCTAssertFalse(model.isActive)
        XCTAssertNil(model.results)
        XCTAssertTrue(
            recorder.performCalls.isEmpty,
            "restore must not run the query")

        // The view forwards EVERY fieldText change into
        // fieldTextChanged(), including the restore's own write. The
        // suppression token must swallow exactly that delivery — or the
        // restored query would silently apply.
        model.fieldTextChanged()
        XCTAssertNil(model.pendingCommitTaskForTesting)
        XCTAssertTrue(recorder.performCalls.isEmpty)

        // The NEXT (real) edit debounces normally.
        model.fieldText = "has:task alpha"
        model.fieldTextChanged()
        XCTAssertNotNil(model.pendingCommitTaskForTesting)
    }

    func testEscapeClearWriteDoesNotReadAsAKeystroke() {
        let recorder = Recorder()
        recorder.pages["alpha"] = page(["a.md"])
        let model = makeModel(recorder, defaults: freshDefaults())

        model.fieldText = "alpha"
        model.commit()
        model.escapeInField()
        // The view's onChange fires for the Esc clear's own write; the
        // token swallows it (no "" commit task is scheduled).
        model.fieldTextChanged()
        XCTAssertNil(model.pendingCommitTaskForTesting)
        XCTAssertEqual(recorder.performCalls.count, 1)
    }

    func testResetForVaultCloseKeepsDevicePersistedQuery() {
        let recorder = Recorder()
        recorder.pages["alpha"] = page(["a.md"])
        let defaults = freshDefaults()
        let model = makeModel(recorder, defaults: defaults)

        model.fieldText = "alpha"
        model.commit()
        model.resetForVaultClose()

        XCTAssertEqual(model.fieldText, "")
        XCTAssertFalse(model.isActive)
        XCTAssertNil(model.results)
        XCTAssertEqual(
            defaults.string(forKey: SidebarFilterModel.persistedQueryKey),
            "alpha",
            "vault close is not a clear; the query restores next bind")
    }

    // MARK: - Paging

    func testLoadNextPageAppendsWithIdenticalWindowsAndNoNewAnnouncement() {
        let recorder = Recorder()
        recorder.requirementsByQuery["@today x"] = ["@today"]
        recorder.pages["@today x"] = SidebarFilterPage(
            files: [file("a.md"), file("b.md")], nextCursor: "c1", total: 3,
            audioSummary: "3 results.")
        recorder.cursorPages["c1"] = SidebarFilterPage(
            files: [file("c.md")], nextCursor: nil, total: 3,
            audioSummary: "3 results.")
        let model = makeModel(recorder, defaults: freshDefaults())

        model.fieldText = "@today x"
        model.commit()
        // Advance the clock across midnight between pages: page 2 must
        // still run with page 1's exact windows, not recomputed ones.
        recorder.nowMs += 86_400_000
        model.loadNextPage()

        XCTAssertEqual(
            model.results?.files.map(\.path), ["a.md", "b.md", "c.md"])
        XCTAssertNil(model.results?.nextCursor)
        XCTAssertEqual(recorder.performCalls.count, 2)
        XCTAssertEqual(recorder.performCalls[1].paging.cursor, "c1")
        XCTAssertEqual(
            recorder.performCalls[1].windows, recorder.performCalls[0].windows)
        XCTAssertEqual(recorder.announcements, ["3 results."])

        // No cursor left: loadNextPage is a no-op.
        model.loadNextPage()
        XCTAssertEqual(recorder.performCalls.count, 2)
    }

    func testInlineErrorsAnnouncePolitelyAndDedupPerMessage() {
        let recorder = Recorder()
        recorder.pages["alpha"] = page(["a.md"], summary: "1 result.")
        recorder.errors["ext:"] = VaultError.InvalidQuery(
            message: "\"ext:\" needs an extension, like ext:md.")
        recorder.errors["#"] = VaultError.InvalidQuery(
            message: "\"#\" needs a tag name.")
        let model = makeModel(recorder, defaults: freshDefaults())

        model.fieldText = "ext:"
        model.commit()
        model.commit()
        XCTAssertEqual(
            recorder.announcements,
            ["\"ext:\" needs an extension, like ext:md."],
            "the error speaks once per distinct message, not per keystroke")

        model.fieldText = "#"
        model.commit()
        XCTAssertEqual(recorder.announcements.count, 2)

        // Success clears the token: the SAME error later re-announces.
        model.fieldText = "alpha"
        model.commit()
        model.fieldText = "ext:"
        model.commit()
        XCTAssertEqual(recorder.announcements.count, 4)
        XCTAssertEqual(
            recorder.announcements.last,
            "\"ext:\" needs an extension, like ext:md.")
    }

    func testRefreshAfterStructuralMutationRerunsOnlyWhileActive() {
        let recorder = Recorder()
        recorder.pages["alpha"] = page(["a.md"])
        let model = makeModel(recorder, defaults: freshDefaults())

        model.refreshAfterStructuralMutation()
        XCTAssertTrue(
            recorder.performCalls.isEmpty,
            "no committed query: mutation completions are ignored")

        model.fieldText = "alpha"
        model.commit()
        XCTAssertEqual(recorder.performCalls.count, 1)
        model.refreshAfterStructuralMutation()
        XCTAssertEqual(
            recorder.performCalls.count, 2,
            "an active overlay re-runs so renamed/deleted rows can't linger")

        model.escapeInField()
        model.refreshAfterStructuralMutation()
        XCTAssertEqual(recorder.performCalls.count, 2)
    }

    // MARK: - Rollover / time-zone change (spec rule 7)

    func testRolloverRefreshRunsOnlyWhileADateTermIsActive() {
        let recorder = Recorder()
        recorder.pages["has:task"] = page(["a.md"])
        recorder.requirementsByQuery["@today"] = ["@today"]
        recorder.pages["@today"] = page(["b.md"])
        let model = makeModel(recorder, defaults: freshDefaults())

        model.fieldText = "has:task"
        model.commit()
        XCTAssertEqual(recorder.performCalls.count, 1)
        model.handleDayRolloverOrTimeZoneChange()
        XCTAssertEqual(
            recorder.performCalls.count, 1,
            "no date term: rollover must not rerun")

        model.fieldText = "@today"
        model.commit()
        XCTAssertEqual(recorder.performCalls.count, 2)
        let beforeRollover = recorder.performCalls[1].windows
        recorder.nowMs += 86_400_000
        model.handleDayRolloverOrTimeZoneChange()
        XCTAssertEqual(recorder.performCalls.count, 3)
        XCTAssertNotEqual(
            recorder.performCalls[2].windows, beforeRollover,
            "rollover recomputes boundaries from the moved clock")

        // Inactive filter: rollover is a no-op regardless of terms.
        model.escapeInField()
        model.handleDayRolloverOrTimeZoneChange()
        XCTAssertEqual(recorder.performCalls.count, 3)
    }
}
