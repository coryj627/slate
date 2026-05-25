import XCTest

@testable import SlateMac

/// End-to-end "Milestone I shipped" coverage. Walks the property-
/// edit surface in a single sequence so an accidental regression
/// in any seam (set → reindex → properties refresh → conflict, or
/// rename → dry-run → apply → reindex) surfaces as a single
/// failure here even when the atomic tests in AppStateTests still
/// pass.
///
/// Closing checkpoint per #171: a 5-file fixture vault with mixed
/// frontmatter shapes, edits per variant, add-to-no-frontmatter,
/// delete-last-key, bulk-rename dry-run-vs-apply, KeyCollision
/// skip, conflict-dialog path, and an op-log entry-count check.
/// Wall-clock budget: under 5 seconds on local + CI runners.
@MainActor
final class MilestoneIIntegrationTests: XCTestCase {
    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-milestone-i-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private func makeAppState() -> AppState {
        let store = RecentVaultsStore(fileURL: tempDir.appendingPathComponent("recents.json"))
        return AppState(recentsStore: store, externalOpener: { _ in true })
    }

    func testMilestoneIEndToEndRoundTrip() async throws {
        // === Vault layout ===
        //
        // rich1.md  — frontmatter with text / number / boolean / list
        // rich2.md  — frontmatter with date / wikilink / tag_list
        // plain.md  — no frontmatter; receives a synthesized block
        // collide1.md — has both `author` and `by` (for KeyCollision)
        // collide2.md — has just `author` (rename target without conflict)
        let vault = tempDir.appendingPathComponent("milestone-i-integration")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)

        try """
            ---
            title: Rich
            count: 7
            active: true
            tags:
              - foo
              - bar
            ---
            # Body
            paragraph text
            """
            .data(using: .utf8)!
            .write(to: vault.appendingPathComponent("rich1.md"))

        try """
            ---
            published: 2026-05-24
            ref: "[[Other Note]]"
            topics:
              - alpha
              - beta
            ---
            content
            """
            .data(using: .utf8)!
            .write(to: vault.appendingPathComponent("rich2.md"))

        try "# Just a heading\n\nNo frontmatter.\n"
            .data(using: .utf8)!
            .write(to: vault.appendingPathComponent("plain.md"))

        try "---\nauthor: A\nby: Existing\n---\n"
            .data(using: .utf8)!
            .write(to: vault.appendingPathComponent("collide1.md"))

        try "---\nauthor: C\n---\nclean content\n"
            .data(using: .utf8)!
            .write(to: vault.appendingPathComponent("collide2.md"))

        let state = makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        // === Edit each variant on rich1.md, asserting body byte-equal ===
        state.selectedFilePath = "rich1.md"
        await state.noteLoadTask?.value
        await state.linksLoadTask?.value
        let bodySuffix1 = "# Body\nparagraph text"

        await state.setProperty(
            path: "rich1.md",
            key: "title",
            value: PropertyValue.text(value: "Updated")
        )?.value
        await state.setProperty(
            path: "rich1.md",
            key: "count",
            value: PropertyValue.integer(value: 42)
        )?.value
        await state.setProperty(
            path: "rich1.md",
            key: "active",
            value: PropertyValue.boolean(value: false)
        )?.value
        await state.setProperty(
            path: "rich1.md",
            key: "tags",
            value: PropertyValue.tagList(tags: ["renamed"])
        )?.value

        let rich1 = try String(
            contentsOf: vault.appendingPathComponent("rich1.md"),
            encoding: .utf8
        )
        XCTAssertTrue(
            rich1.hasSuffix(bodySuffix1) || rich1.hasSuffix(bodySuffix1 + "\n"),
            "rich1.md body must remain byte-identical after frontmatter edits; got: \(rich1)"
        )
        XCTAssertTrue(rich1.contains("title: Updated"))
        XCTAssertTrue(rich1.contains("count: 42"))
        XCTAssertTrue(rich1.contains("active: false"))

        // === Edit date / wikilink / tag-list variants on rich2.md ===
        state.selectedFilePath = "rich2.md"
        await state.noteLoadTask?.value
        await state.linksLoadTask?.value
        let bodySuffix2 = "content"

        await state.setProperty(
            path: "rich2.md",
            key: "published",
            value: PropertyValue.date(value: "2026-06-01")
        )?.value
        await state.setProperty(
            path: "rich2.md",
            key: "ref",
            value: PropertyValue.wikilink(target: "Updated Target")
        )?.value
        await state.setProperty(
            path: "rich2.md",
            key: "topics",
            value: PropertyValue.list(items: [
                PropertyValue.text(value: "one"),
                PropertyValue.text(value: "two"),
            ])
        )?.value

        let rich2 = try String(
            contentsOf: vault.appendingPathComponent("rich2.md"),
            encoding: .utf8
        )
        XCTAssertTrue(rich2.hasSuffix(bodySuffix2) || rich2.hasSuffix(bodySuffix2 + "\n"))
        XCTAssertTrue(rich2.contains("published: 2026-06-01"))
        XCTAssertTrue(rich2.contains("Updated Target"))

        // === Add to no-frontmatter file: synthesizes block at top ===
        state.selectedFilePath = "plain.md"
        await state.noteLoadTask?.value
        await state.linksLoadTask?.value

        await state.setProperty(
            path: "plain.md",
            key: "title",
            value: PropertyValue.text(value: "Newly Titled")
        )?.value

        let plain = try String(
            contentsOf: vault.appendingPathComponent("plain.md"),
            encoding: .utf8
        )
        XCTAssertTrue(plain.hasPrefix("---\n"))
        XCTAssertTrue(plain.contains("title: Newly Titled"))
        XCTAssertTrue(plain.hasSuffix("# Just a heading\n\nNo frontmatter.\n"))

        // === Delete last key strips `---` block entirely ===
        await state.deleteProperty(path: "plain.md", key: "title")?.value
        let plainAfterDelete = try String(
            contentsOf: vault.appendingPathComponent("plain.md"),
            encoding: .utf8
        )
        XCTAssertEqual(plainAfterDelete, "# Just a heading\n\nNo frontmatter.\n")

        // === Bulk rename dry-run matches apply ===
        await state.previewPropertyRename(oldKey: "author", newKey: "by")?.value
        let preview = try XCTUnwrap(state.pendingRenameReport)
        let previewWill = preview.affected.map(\.path).sorted()
        let previewSkipped = preview.skipped.map(\.path)

        // collide1.md skipped (KeyCollision); collide2.md will rename.
        XCTAssertEqual(previewWill, ["collide2.md"])
        XCTAssertEqual(previewSkipped, ["collide1.md"])
        XCTAssertTrue(preview.affected.allSatisfy { !$0.applied })

        await state.applyPropertyRename(oldKey: "author", newKey: "by")?.value
        let applied = try XCTUnwrap(state.pendingRenameReport)
        XCTAssertEqual(applied.affected.map(\.path).sorted(), previewWill)
        XCTAssertEqual(applied.skipped.map(\.path), previewSkipped)
        XCTAssertEqual(
            applied.skipped[0].reason,
            RenameSkipReason.keyCollision
        )
        XCTAssertTrue(applied.affected.allSatisfy { $0.applied })

        let collide1 = try String(
            contentsOf: vault.appendingPathComponent("collide1.md"),
            encoding: .utf8
        )
        XCTAssertEqual(collide1, "---\nauthor: A\nby: Existing\n---\n")
        let collide2 = try String(
            contentsOf: vault.appendingPathComponent("collide2.md"),
            encoding: .utf8
        )
        XCTAssertTrue(collide2.contains("by:") && !collide2.contains("author:"))

        // === Conflict path: stale contentHash → dialog presented ===
        state.selectedFilePath = "rich1.md"
        await state.noteLoadTask?.value
        await state.linksLoadTask?.value

        // External writer mutates the file while the editor's
        // cached hash is from before the mutation.
        let mutated =
            (try String(contentsOf: vault.appendingPathComponent("rich1.md"), encoding: .utf8))
            + "\nappended externally\n"
        try Data(mutated.utf8)
            .write(to: vault.appendingPathComponent("rich1.md"))

        await state.setProperty(
            path: "rich1.md",
            key: "title",
            value: PropertyValue.text(value: "Race-attempt")
        )?.value

        XCTAssertNotNil(
            state.currentPropertyEditConflict,
            "stale contentHash must surface as a property-edit conflict"
        )

        // === Op-log: each edit produced one entry per file ===
        // rich1.md had 4 set + 1 conflict-attempt (the conflict
        // attempt didn't write, so no op-log entry). 4 entries.
        let session = try XCTUnwrap(state.currentSession)
        let rich1Log = try session.readOplog(path: "rich1.md")
        XCTAssertEqual(rich1Log.count, 4)
        let rich2Log = try session.readOplog(path: "rich2.md")
        XCTAssertEqual(rich2Log.count, 3)
        // plain.md: add + delete = 2 entries.
        let plainLog = try session.readOplog(path: "plain.md")
        XCTAssertEqual(plainLog.count, 2)
        // collide1.md skipped — no op-log entry.
        let collide1Log = try session.readOplog(path: "collide1.md")
        XCTAssertEqual(collide1Log.count, 0)
        // collide2.md got the rename — 1 entry.
        let collide2Log = try session.readOplog(path: "collide2.md")
        XCTAssertEqual(collide2Log.count, 1)
    }
}
