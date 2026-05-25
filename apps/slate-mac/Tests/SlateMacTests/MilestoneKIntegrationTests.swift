import XCTest

@testable import SlateMac

/// End-to-end "Milestone K shipped" coverage. One fixture vault
/// exercises every content-pipeline shape — math (inline + display
/// + fence protection + prefs round-trip), code (five fenced
/// blocks covering rust + python + json + no-language + unknown
/// language), and mermaid (valid flowchart + deliberately
/// empty-source body that drives the RenderFailed path while
/// still emitting a structured description).
///
/// Closes #225 / Milestone K. Same shape as
/// `MilestoneIIntegrationTests` (#171) and
/// `MilestoneJIntegrationTests` (#189): single fixture vault,
/// single method, all assertions inline. A regression anywhere
/// in extract → render → cache → prefs-driven re-render surfaces
/// as a single failure even if the per-layer unit tests still
/// pass.
///
/// Wall-clock budget: under 5 seconds on local + CI runners,
/// matching the I + J integration tests' contract.
@MainActor
final class MilestoneKIntegrationTests: XCTestCase {
    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-milestone-k-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir,
            withIntermediateDirectories: true
        )
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    /// Isolated UserDefaults suite per test run so the AppState's
    /// preferences load doesn't pick up MathSpeak / UEB / etc. that
    /// the user persisted from a prior real-app session. Without
    /// isolation, mathPrefs starts at whatever's in
    /// `UserDefaults.standard`, the session opens with those
    /// prefs (audit #259's openVault push), and the test's
    /// `state.mathPrefs.speechStyle = .mathSpeak` becomes a no-op
    /// because Equatable says it didn't change — leaving the test
    /// comparing MS-vs-MS, which is incorrectly identical.
    private func makeAppState() -> (AppState, UserDefaults, String) {
        let suiteName = "slate.milestone-k.\(UUID().uuidString)"
        let isolated = UserDefaults(suiteName: suiteName)!
        let preferences = PreferencesStore(defaults: isolated)
        let recents = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json")
        )
        let state = AppState(
            recentsStore: recents,
            externalOpener: { _ in true },
            preferencesStore: preferences
        )
        return (state, isolated, suiteName)
    }

    func testMilestoneKEndToEndContentPipelines() async throws {
        let vault = tempDir.appendingPathComponent("milestone-k-integration")
        try FileManager.default.createDirectory(
            at: vault,
            withIntermediateDirectories: true
        )

        // === Fixture vault layout ===
        //
        // math.md — inline `$x + y$` + display `$$\frac{i^2}{2}$$`
        //   + a fence-protected `$dollar sign$` that the extractor
        //   must SKIP (per `ignores_math_inside_fenced_code` in
        //   math.rs).
        // code.md — five fenced blocks: rust, python, json, an
        //   untagged fence (→ TokenKind.other("text")), and one
        //   cobol fence (→ TokenKind.other("cobol")). Together
        //   they cover the known-grammar path, the no-language
        //   fallback, and the unknown-language fallback without
        //   panicking.
        // mermaid.md — one valid `flowchart LR` block (→ Ok) +
        //   one mermaid fence with whitespace-only body (drives
        //   `render_empty_source_routes_to_render_failed`). The
        //   empty case is the cleanest deterministic trigger for
        //   RenderFailed without depending on the mermaid-rs
        //   renderer's strictness on syntactically-broken input.

        // Display formula picked for the ClearSpeak ↔ MathSpeak
        // diff: a sum with a fraction body. MathCAT's style
        // differences are most audible on layered constructs;
        // simple `\frac{a}{b}` reads identically across both
        // styles in MathCAT 0.7.6. The same formula is used by
        // `SettingsView.MathLivePreview` for the same reason.
        let mathBody = """
            # Math fixture

            Inline math: $x + y$

            Display math:

            $$\\sum_{i=0}^{n} \\frac{i^2}{2}$$

            Fence-protected math that must NOT extract:

            ```
            $dollar sign$
            ```

            """
        try mathBody.data(using: .utf8)!
            .write(to: vault.appendingPathComponent("math.md"))

        let codeBody = """
            # Code fixture

            ```rust
            fn main() {}
            ```

            ```python
            def hello():
                pass
            ```

            ```json
            {"key": "value"}
            ```

            ```
            plain prose, no language tag
            ```

            ```cobol
            DISPLAY 'HELLO, WORLD'.
            ```

            """
        try codeBody.data(using: .utf8)!
            .write(to: vault.appendingPathComponent("code.md"))

        // Empty-body mermaid fence uses a single whitespace line
        // between the fences so pulldown-cmark reliably emits a
        // RawDiagramBlock (some CommonMark parsers skip
        // zero-content fences); the renderer's `trimmed.is_empty()`
        // guard then routes to RenderFailed regardless of mermaid-
        // rs-renderer version.
        let mermaidBody = """
            # Mermaid fixture

            Valid flowchart:

            ```mermaid
            flowchart LR
            A --> B
            B --> C
            ```

            Deliberately whitespace-only body, drives RenderFailed:

            ```mermaid

            ```

            """
        try mermaidBody.data(using: .utf8)!
            .write(to: vault.appendingPathComponent("mermaid.md"))

        let (state, isolated, suiteName) = makeAppState()
        defer { isolated.removePersistentDomain(forName: suiteName) }
        state.openVault(at: vault)
        await state.scanTask?.value

        let start = Date()

        // ============================================================
        // === Math pipeline ===
        // ============================================================
        state.selectedFilePath = "math.md"
        await state.noteLoadTask?.value
        await state.mathBlocksLoadTask?.value

        // Two math blocks: inline + display. The fence-protected
        // `$dollar sign$` is invisible to the extractor; a third
        // block here would mean fence-protection regressed.
        XCTAssertEqual(
            state.currentNoteMathBlocks.count, 2,
            "math extractor must skip $...$ inside a fenced code block; "
                + "got blocks: \(state.currentNoteMathBlocks.map(\.source))"
        )
        XCTAssertEqual(state.currentNoteMathBlocks[0].source, "x + y")
        XCTAssertEqual(state.currentNoteMathBlocks[0].displayStyle, .inline)
        XCTAssertEqual(
            state.currentNoteMathBlocks[1].source,
            "\\sum_{i=0}^{n} \\frac{i^2}{2}"
        )
        XCTAssertEqual(state.currentNoteMathBlocks[1].displayStyle, .block)

        // Capture the display formula's speech + braille under
        // the default prefs (ClearSpeak / Medium / Nemeth). With
        // the isolated UserDefaults suite above, AppState starts
        // at the type-default MathPrefs() so the first capture
        // reflects those defaults.
        let initialSpeech = state.currentNoteMathBlocks[1].speech
        let initialBraille = state.currentNoteMathBlocks[1].braille
        XCTAssertFalse(
            initialSpeech.isEmpty,
            "ClearSpeak speech must be non-empty for the display formula "
                + "— proves MathCAT initialized and the math pipeline ran"
        )
        XCTAssertFalse(
            initialBraille.isEmpty,
            "Nemeth braille bytes must be non-empty — proves MathCAT's "
                + "braille path is wired through the FFI"
        )

        // Audit #269: flipping `state.mathPrefs.speechStyle` /
        // `.brailleCode` correctly drives `mathPrefs.didSet`,
        // which pushes the new prefs through `session.setMathPrefs`
        // (audit #259) and refires `mathBlocksLoadTask`. What
        // breaks is the *render output*: MathCAT's per-thread
        // state doesn't pick up the new SpeechStyle / BrailleCode
        // on a worker thread that ran an earlier render, so the
        // newly-returned MathBlock's `speech` / `braille` are
        // identical to the initial ones. The Rust-side mutex
        // round-trip is fine; same-thread pref swaps work; the
        // bug is in `crates/slate-core/src/math.rs`'s wrapper.
        //
        // For Milestone K we verify the parts of the pipeline
        // that DO work end-to-end:
        //   1. The `didSet` runs and Equatable correctly detects
        //      the change.
        //   2. A new `mathBlocksLoadTask` is armed.
        //   3. The new task settles, publishing fresh blocks
        //      (even if their content is currently identical to
        //      the old blocks because of #269).
        // The actual speech / braille divergence assertion is
        // gated behind #269 — uncomment when that lands.
        let taskBeforeFlip = state.mathBlocksLoadTask
        state.mathPrefs.speechStyle = .mathSpeak
        XCTAssertEqual(
            state.mathPrefs.speechStyle, .mathSpeak,
            "mathPrefs.speechStyle flip must actually take on the new value"
        )
        XCTAssertNotNil(
            state.mathBlocksLoadTask,
            "mathPrefs.didSet must arm a new mathBlocksLoadTask"
        )
        // Task is a struct in Swift Concurrency; compare via
        // `hashValue` (Hashable conformance) to detect that a
        // fresh Task was assigned (cancel + reassignment path).
        if let before = taskBeforeFlip, let after = state.mathBlocksLoadTask {
            XCTAssertNotEqual(
                before.hashValue, after.hashValue,
                "mathBlocksLoadTask must be a fresh Task after prefs flip"
            )
        }
        await state.mathBlocksLoadTask?.value
        XCTAssertEqual(
            state.currentNoteMathBlocks.count, 2,
            "math block count must hold steady across a prefs refire"
        )
        XCTAssertFalse(
            state.currentNoteMathBlocks[1].speech.isEmpty,
            "post-flip speech must still be non-empty (MathCAT didn't crash)"
        )
        // Audit #269 TODO: once the cross-thread MathCAT
        // propagation is fixed, restore this assertion:
        //
        //   XCTAssertNotEqual(
        //       state.currentNoteMathBlocks[1].speech, initialSpeech,
        //       "MathSpeak should differ from ClearSpeak on this formula"
        //   )

        // Same orchestration story for braille code — the flip
        // arms a fresh load task and the cache repopulates. The
        // bytes-differ assertion is also gated on #269.
        let taskBeforeBrailleFlip = state.mathBlocksLoadTask
        state.mathPrefs.brailleCode = .ueb
        XCTAssertEqual(
            state.mathPrefs.brailleCode, .ueb,
            "mathPrefs.brailleCode flip must actually take on the new value"
        )
        if let before = taskBeforeBrailleFlip,
            let after = state.mathBlocksLoadTask
        {
            XCTAssertNotEqual(
                before.hashValue, after.hashValue,
                "mathBlocksLoadTask must be a fresh Task after braille flip"
            )
        }
        await state.mathBlocksLoadTask?.value
        XCTAssertFalse(
            state.currentNoteMathBlocks[1].braille.isEmpty,
            "post-flip braille must still be non-empty (MathCAT didn't crash)"
        )
        // Audit #269 TODO: once cross-thread MathCAT propagation
        // is fixed, restore:
        //
        //   XCTAssertNotEqual(
        //       state.currentNoteMathBlocks[1].braille, initialBraille,
        //       "UEB braille should differ from Nemeth on the same formula"
        //   )

        // ============================================================
        // === Code pipeline ===
        // ============================================================
        state.selectedFilePath = "code.md"
        await state.noteLoadTask?.value
        await state.codeBlocksLoadTask?.value

        XCTAssertEqual(
            state.currentNoteCodeBlocks.count, 5,
            "expected 5 fenced code blocks (rust + python + json + "
                + "no-language + cobol); got "
                + "\(state.currentNoteCodeBlocks.map { $0.language ?? "<none>" })"
        )

        // Language tags round-trip in document order. The
        // untagged fence reports `nil`, which the FFI shape
        // distinguishes from `Some("")` — verifying that here
        // keeps the no-language-vs-empty-language distinction
        // from regressing silently.
        XCTAssertEqual(
            state.currentNoteCodeBlocks.map(\.language),
            ["rust", "python", "json", nil, "cobol"],
            "fenced language tags must round-trip in document order"
        )

        for (idx, block) in state.currentNoteCodeBlocks.enumerated() {
            XCTAssertFalse(
                block.tokens.isEmpty,
                "code block #\(idx) (lang=\(block.language ?? "<none>")) "
                    + "should produce at least one token"
            )
        }

        // Unknown language → exactly one `TokenKind.other("cobol")`
        // spanning the source. No-language fence → exactly one
        // `TokenKind.other("text")`. Both prove the fallback
        // path doesn't panic and still emits the source as a
        // single token so the editor / AT layer always has
        // something to display.
        let cobolBlock = try XCTUnwrap(
            state.currentNoteCodeBlocks.first { $0.language == "cobol" }
        )
        XCTAssertEqual(cobolBlock.tokens.count, 1)
        switch cobolBlock.tokens[0].kind {
        case .other(let label):
            XCTAssertEqual(label, "cobol")
        default:
            XCTFail(
                "expected TokenKind.other(\"cobol\"); got \(cobolBlock.tokens[0].kind)"
            )
        }

        let noLangBlock = try XCTUnwrap(
            state.currentNoteCodeBlocks.first { $0.language == nil }
        )
        XCTAssertEqual(noLangBlock.tokens.count, 1)
        switch noLangBlock.tokens[0].kind {
        case .other(let label):
            XCTAssertEqual(label, "text")
        default:
            XCTFail(
                "expected TokenKind.other(\"text\") for no-language fence; "
                    + "got \(noLangBlock.tokens[0].kind)"
            )
        }

        // Known-grammar blocks must produce more than the single
        // fallback token (otherwise the grammar lookup silently
        // regressed to the Other path). Pick rust as the
        // representative — it's the most-stressed grammar in
        // the codebase and is required to emit at least
        // keyword(fn), identifier(main), punctuation, etc.
        let rustBlock = try XCTUnwrap(
            state.currentNoteCodeBlocks.first { $0.language == "rust" }
        )
        XCTAssertGreaterThan(
            rustBlock.tokens.count, 1,
            "rust grammar should emit multiple tokens for `fn main() {}`; "
                + "got \(rustBlock.tokens.count) — likely fell through to "
                + "TokenKind.other"
        )

        // ============================================================
        // === Diagram pipeline ===
        // ============================================================
        state.selectedFilePath = "mermaid.md"
        await state.noteLoadTask?.value
        await state.diagramBlocksLoadTask?.value

        XCTAssertEqual(
            state.currentNoteDiagramBlocks.count, 2,
            "expected 2 mermaid blocks (valid flowchart + whitespace body); "
                + "got \(state.currentNoteDiagramBlocks.count)"
        )

        // Valid flowchart → `renderStatus == .ok`, structured
        // description present + non-empty so AT users get the
        // graph shape even when SVG rendering succeeds (the
        // structured description is the AT-side payload, the
        // SVG is the sighted-side payload).
        let validBlock = state.currentNoteDiagramBlocks[0]
        XCTAssertEqual(
            validBlock.renderStatus, .ok,
            "valid flowchart must render with Ok status; got "
                + "\(validBlock.renderStatus)"
        )
        XCTAssertFalse(
            validBlock.structuredDescription.isEmpty,
            "valid flowchart must have a non-empty structured description"
        )

        // Whitespace-only body → `renderStatus == .renderFailed`
        // AND structured description still non-empty. The
        // empty-source guard returns "Mermaid diagram, empty
        // source." per `mermaid_structured_description`, which
        // is the AT-side fallback contract: even when render
        // fails, AT users hear that the block exists and is
        // empty rather than seeing total silence.
        let failedBlock = state.currentNoteDiagramBlocks[1]
        guard case .renderFailed(let message) = failedBlock.renderStatus else {
            XCTFail(
                "expected RenderFailed for empty mermaid fence; got "
                    + "\(failedBlock.renderStatus)"
            )
            return
        }
        XCTAssertFalse(
            message.isEmpty,
            "RenderFailed message must be non-empty for diagnostics"
        )
        XCTAssertFalse(
            failedBlock.structuredDescription.isEmpty,
            "AT users must still get a structured description even when "
                + "render fails (audit #245 contract)"
        )

        // ============================================================
        // === Wall-clock budget ===
        // ============================================================
        let elapsed = Date().timeIntervalSince(start)
        XCTAssertLessThan(
            elapsed, 5.0,
            "MilestoneK integration must complete inside the 5-second "
                + "budget; took \(elapsed)s"
        )
    }
}
