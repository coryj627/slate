// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// End-to-end "Milestone J shipped" coverage. One fixture vault
/// exercises every embed shape — full note, section, block, image,
/// unresolved-target, and a recursive chain — plus the editor's
/// span-detection path used by the Cmd+E preview popover.
///
/// Closes #189 / Milestone J. Mirrors the same Cancun-style
/// "single pass with assertions" shape as
/// `MilestoneIIntegrationTests`: any regression in scan →
/// outgoing-links → resolve_embed → editor-span surfaces here as a
/// single failure, even when the per-layer unit tests still pass.
///
/// Architecture note: the shipped editor highlights embeds via
/// regex spans (`EditorEmbedSpans.swift`) + a Cmd+E popover, not
/// via NSTextAttachment cursor-unit stepping. That scoping was
/// chosen during PR #206; the original spec's "one Right-arrow step
/// crosses the span" cell becomes "spans line up 1:1 with the
/// rendered embeds and `embedSpanContaining` locates each one" —
/// the data path the popover actually traverses.
///
/// Wall-clock budget: under 5 seconds on local + CI runners,
/// matching the Milestone I integration test's contract.
@MainActor
final class MilestoneJIntegrationTests: XCTestCase {
    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-milestone-j-\(UUID().uuidString)")
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

    func testMilestoneJEndToEndEmbedResolution() async throws {
        let vault = tempDir.appendingPathComponent("milestone-j-integration")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)

        // === Fixture vault layout ===
        //
        // image.png — minimal valid PNG header (signature + zeroed
        //   IHDR start). MIME inference reads the magic bytes, not
        //   the full image; 16 bytes is enough to pin "image/png".
        // host.md — embeds each of: full note, section, block,
        //   image, unresolved-target, recursive depth chain.
        // target.md — heading + block anchor + inner `![[deep1]]`.
        //   The inner embed gives the FullNote-of-target case its
        //   own nested chain to walk, distinct from host.md's
        //   direct `![[deep1]]`.
        // deepN.md (1..3) — chained `![[deepN+1]]`. deep4.md does
        //   NOT exist on disk — the depth limit fires at depth 3
        //   before the missing-target check would even run.
        let pngBytes = Data([
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x00, 0x00, 0x00, 0x00,
        ])
        try pngBytes.write(to: vault.appendingPathComponent("image.png"))

        let hostBody = """
            # Host

            full note: ![[target]]

            section: ![[target#Heading One]]

            block: ![[target^block-1]]

            image: ![[image.png]]

            unresolved: ![[missing]]

            recursive: ![[deep1]]
            """
        try hostBody.data(using: .utf8)!
            .write(to: vault.appendingPathComponent("host.md"))

        let targetBody = """
            # Heading One

            heading-one body text

            # Heading Two

            paragraph with a block anchor ^block-1

            inner reference: ![[deep1]]
            """
        try targetBody.data(using: .utf8)!
            .write(to: vault.appendingPathComponent("target.md"))

        try "deep1 body ![[deep2]]\n".data(using: .utf8)!
            .write(to: vault.appendingPathComponent("deep1.md"))
        try "deep2 body ![[deep3]]\n".data(using: .utf8)!
            .write(to: vault.appendingPathComponent("deep2.md"))
        try "deep3 body ![[deep4]]\n".data(using: .utf8)!
            .write(to: vault.appendingPathComponent("deep3.md"))

        let state = makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value

        let start = Date()
        state.selectedFilePath = "host.md"
        await state.noteLoadTask?.value
        // `linksLoadTask` chains `loadCurrentNoteEmbedResolutions`
        // inside its closure, so awaiting it covers both legs.
        await state.linksLoadTask?.value

        // === All six embed targets resolved ===
        let resolutions = state.currentNoteEmbedResolutions
        let expectedKeys: Set<String> = [
            "target",
            "target#Heading One",
            "target^block-1",
            "image.png",
            "missing",
            "deep1",
        ]
        XCTAssertEqual(
            Set(resolutions.keys), expectedKeys,
            "expected one resolution per `![[…]]` reference in host.md"
        )

        // === FullNote variant: target_path + body shape ===
        switch try XCTUnwrap(resolutions["target"]) {
        case let .fullNote(targetPath, text, _):
            XCTAssertEqual(targetPath, "target.md")
            XCTAssertTrue(
                text.contains("heading-one body text"),
                "FullNote text should include target.md's body; got \(text)"
            )
        case let other:
            XCTFail("expected FullNote for `target`, got \(other)")
        }

        // === Section variant: heading + slice stops at next H1 ===
        switch try XCTUnwrap(resolutions["target#Heading One"]) {
        case let .section(targetPath, heading, text, _):
            XCTAssertEqual(targetPath, "target.md")
            XCTAssertEqual(heading, "Heading One")
            XCTAssertTrue(text.contains("heading-one body text"))
            XCTAssertFalse(
                text.contains("Heading Two"),
                "section slice must end at the next same-level heading; got \(text)"
            )
        case let other:
            XCTFail("expected Section for `target#Heading One`, got \(other)")
        }

        // === Block variant: block_id + anchored paragraph text ===
        switch try XCTUnwrap(resolutions["target^block-1"]) {
        case let .block(targetPath, blockId, text):
            XCTAssertEqual(targetPath, "target.md")
            XCTAssertEqual(blockId, "block-1")
            XCTAssertTrue(
                text.contains("paragraph with a block anchor"),
                "Block text should contain the anchored paragraph; got \(text)"
            )
        case let other:
            XCTFail("expected Block for `target^block-1`, got \(other)")
        }

        // === Image variant: target_path + MIME + byte-count ===
        switch try XCTUnwrap(resolutions["image.png"]) {
        case let .image(targetPath, bytes, mime, _):
            XCTAssertEqual(targetPath, "image.png")
            XCTAssertEqual(mime, "image/png")
            XCTAssertEqual(
                bytes.count, pngBytes.count,
                "Image bytes should round-trip without truncation"
            )
        case let other:
            XCTFail("expected Image for `image.png`, got \(other)")
        }

        // === Unresolved (TargetNotFound) carries the raw target ===
        switch try XCTUnwrap(resolutions["missing"]) {
        case let .unresolved(reason):
            guard case let .targetNotFound(target) = reason else {
                XCTFail("expected TargetNotFound, got \(reason)")
                return
            }
            XCTAssertEqual(target, "missing")
        case let other:
            XCTFail("expected Unresolved for `missing`, got \(other)")
        }

        // === Recursive chain bottoms out at DepthLimitReached ===
        // host.md's `![[deep1]]` is the depth-0 resolve. The
        // resolver pre-expands up to MAX_EMBED_DEPTH (3): deep1
        // (depth 0) → deep2 (depth 1) → deep3 (depth 2) → deep4
        // (depth 3) becomes Unresolved(DepthLimitReached) — not
        // earlier, not via stack overflow, and not via "deep4.md
        // missing" (the depth check runs before the file lookup).
        let deep1Resolution = try XCTUnwrap(resolutions["deep1"])
        let nestedAtDepth1 = try extractFullNoteNested(deep1Resolution, at: "deep1")
        let deep2Entry = try XCTUnwrap(
            nestedAtDepth1.first { $0.rawTarget == "deep2" },
            "deep1's FullNote must include the nested deep2 embed"
        )
        let nestedAtDepth2 = try extractFullNoteNested(deep2Entry.resolution, at: "deep2")
        let deep3Entry = try XCTUnwrap(
            nestedAtDepth2.first { $0.rawTarget == "deep3" },
            "deep2's FullNote must include the nested deep3 embed"
        )
        let nestedAtDepth3 = try extractFullNoteNested(deep3Entry.resolution, at: "deep3")
        let deep4Entry = try XCTUnwrap(
            nestedAtDepth3.first { $0.rawTarget == "deep4" },
            "deep3's FullNote must include the nested deep4 embed"
        )
        XCTAssertEqual(
            deep4Entry.resolution,
            .unresolved(reason: .depthLimitReached),
            "deep4 (depth 3) must bottom out as DepthLimitReached"
        )

        // === Editor span detection lines up 1:1 with resolutions ===
        // The shipped editor uses regex-based span detection rather
        // than NSTextAttachment cursor-unit stepping (PR #206). The
        // closest end-to-end check we can run from XCTest is: the
        // span finder over the saved buffer must surface exactly
        // the same `![[…]]` references the embed cache holds, and
        // `embedSpanContaining(cursor:)` must locate each one when
        // the cursor lands inside it — that's the path the Cmd+E
        // popover takes.
        let spans = findEditorEmbedSpans(in: hostBody)
        XCTAssertEqual(
            Set(spans.map(\.target)), expectedKeys,
            "editor spans should match the embed cache keys 1:1"
        )
        for span in spans {
            let midCursor = span.range.location + span.range.length / 2
            XCTAssertEqual(
                embedSpanContaining(cursor: midCursor, in: spans)?.target,
                span.target,
                "cursor inside each span must locate that span's target"
            )
        }

        // === Save with one embed removed → resolutions shrink ===
        // External-write + rescan path: write the trimmed body
        // directly, ask the backend for a fresh scan (cheaper than
        // re-opening the vault), then reselect to drive a fresh
        // links + embeds load chain. Mirrors the user flow "an
        // external editor changed my file; reload it."
        let trimmedBody = hostBody.replacingOccurrences(
            of: "unresolved: ![[missing]]\n\n",
            with: ""
        )
        try trimmedBody.data(using: .utf8)!
            .write(to: vault.appendingPathComponent("host.md"))
        let session = try XCTUnwrap(state.currentSession)
        try await Task.detached(priority: .userInitiated) {
            _ = try session.scanInitial(cancel: CancelToken())
        }.value
        state.selectedFilePath = nil
        state.selectedFilePath = "host.md"
        await state.noteLoadTask?.value
        await state.linksLoadTask?.value

        let trimmedResolutions = state.currentNoteEmbedResolutions
        XCTAssertEqual(
            trimmedResolutions.count, 5,
            "removing one `![[…]]` from host.md must drop one resolution"
        )
        XCTAssertNil(
            trimmedResolutions["missing"],
            "the removed embed's key must be absent after refresh"
        )

        // === Wall-clock budget ===
        let elapsed = Date().timeIntervalSince(start)
        XCTAssertLessThan(
            elapsed, 5.0,
            "MilestoneJ integration must complete inside the 5-second budget; took \(elapsed)s"
        )
    }

    /// Unwrap a `FullNote` resolution and return its `nested` array.
    /// Single-use helper — the recursion walk would be unreadable
    /// inline if every level repeated the same `switch`.
    private func extractFullNoteNested(
        _ resolution: EmbedResolution,
        at label: String
    ) throws -> [NestedEmbed] {
        guard case let .fullNote(_, _, nested) = resolution else {
            XCTFail("expected FullNote at \(label), got \(resolution)")
            throw ExpectedFullNoteError(label: label)
        }
        return nested
    }

    private struct ExpectedFullNoteError: Error {
        let label: String
    }
}
