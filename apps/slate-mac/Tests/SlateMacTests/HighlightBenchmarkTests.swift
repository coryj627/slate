// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI
import XCTest

@testable import SlateMac

/// `SLATE_BENCH`-gated benchmark of the per-keystroke editor highlight cost
/// (#375).
///
/// The hot path the editor runs off the debounced `scheduleHighlight`
/// (`NoteEditorView`): the canonical Rust `editorHighlightSpans` FFI
/// (#376/#377 — replacing the retired Swift `findEditorSyntaxSpans`) plus
/// the Swift `findEditorEmbedSpans` overlay. This times both across
/// 100 KB → 8 MB so the editor work (#379's incremental highlighting and
/// beyond) has a committed baseline and can't silently regress.
///
/// Gated behind `SLATE_BENCH=1` (`XCTSkip` otherwise) so a normal
/// `swift test` / CI run never pays the multi-hundred-ms 8 MB cost. Run:
///
///     SLATE_BENCH=1 swift test -c release --filter HighlightBenchmarkTests
///
/// (Use `-c release` for representative `-O` numbers; debug is ~10× slower.)
/// The numbers print as a table; record them in `BENCHMARKS.md`.
final class HighlightBenchmarkTests: XCTestCase {

    func testHighlightThroughput() throws {
        try XCTSkipUnless(
            ProcessInfo.processInfo.environment["SLATE_BENCH"] == "1",
            "highlight benchmark is gated; set SLATE_BENCH=1 to run it"
        )

        // (label, target bytes, timed iterations). Fewer iterations at the
        // large sizes keeps the whole run to a few seconds while staying
        // stable; warm-up runs are excluded from the timing below.
        let cases: [(label: String, bytes: Int, iters: Int)] = [
            ("100 KB", 100 * 1024, 30),
            ("1 MB", 1024 * 1024, 10),
            ("2 MB", 2 * 1024 * 1024, 6),
            ("8 MB", 8 * 1024 * 1024, 3),
        ]

        print("\n#375 highlight benchmark (build with -c release for -O numbers)")
        print(
            String(
                format: "%-8@ %12@ %12@ %16@", "size" as NSString, "syntax" as NSString,
                "embed" as NSString, "total/keystroke" as NSString))

        for c in cases {
            let doc = Self.representativeMarkdown(targetBytes: c.bytes)
            // Warm up (allocator, dylib page-in, caches) — not timed.
            for _ in 0..<2 {
                _ = editorHighlightSpans(text: doc)
                _ = findEditorEmbedSpans(in: doc)
            }
            let syntax = Self.bench(c.iters) { editorHighlightSpans(text: doc).count }
            let embed = Self.bench(c.iters) { findEditorEmbedSpans(in: doc).count }
            print(
                String(
                    format: "%-8@ %10.1f ms %10.1f ms %14.1f ms",
                    c.label as NSString, syntax, embed, syntax + embed))
        }
    }

    /// #1224 (plan 43 PR 17): what the VoiceOver semantics add, through the
    /// real entry points. `plan` runs off-main with the span computation;
    /// `stamp` runs on main in `applyHighlight`, for a whole-document pass
    /// and for one keystroke's window, beside the colour pass's own stamping
    /// for scale; `edit` is one keystroke mirrored into the store; `read` is
    /// `accessibilityAttributedString(for:)` on one prose line and on the
    /// whole document, against AppKit's native read of the same range.
    @MainActor
    func testAccessibilitySemanticsThroughput() throws {
        try XCTSkipUnless(
            ProcessInfo.processInfo.environment["SLATE_BENCH"] == "1",
            "highlight benchmark is gated; set SLATE_BENCH=1 to run it"
        )
        let cases: [(label: String, bytes: Int, iters: Int)] = [
            ("100 KB", 100 * 1024, 20),
            ("1 MB", 1024 * 1024, 6),
            ("2 MB", 2 * 1024 * 1024, 4),
            ("8 MB", 8 * 1024 * 1024, 2),
        ]
        print("\n#1224 VoiceOver semantics, ms (build with -c release for -O numbers)")
        print(
            "size      plan  colour-doc  stamp-doc  colour-win  stamp-win  edit-plain   edit  line-native  line-ax  doc-native  doc-ax"
        )
        for c in cases {
            let doc = Self.representativeMarkdown(targetBytes: c.bytes)
            let ns = doc as NSString
            let full = NSRange(location: 0, length: ns.length)
            let mapped = EditorSpanMapping.utf16Spans(from: editorHighlightSpans(text: doc), in: doc)
            let runs = EditorAccessibilityAttributes.plan(mapped)
            let textView = SlateEditorTextView(frame: NSRect(x: 0, y: 0, width: 640, height: 480))
            textView.string = doc
            let binding = Binding<String>(get: { doc }, set: { _ in })
            let coordinator = NoteEditorView.Coordinator(text: binding, onSave: {}, previewEmbedAtCursor: nil)
            textView.coordinator = coordinator
            coordinator.attach(textView: textView)
            let store = coordinator.semanticStore
            let layoutManager = try XCTUnwrap(textView.layoutManager)
            // One keystroke's window: the section around the middle.
            let middle = ns.range(
                of: "## Section", options: [], range: NSRange(location: full.length / 2, length: full.length / 2))
            let next = ns.range(
                of: "## Section", options: [],
                range: NSRange(location: NSMaxRange(middle), length: full.length - NSMaxRange(middle)))
            let window = NSRange(location: middle.location, length: next.location - middle.location)
            let windowSpans = mapped.filter { NSIntersectionRange($0.range, window).length == $0.range.length }
            let windowRuns = EditorAccessibilityAttributes.plan(windowSpans)
            let line = ns.lineRange(
                for: NSRange(location: ns.range(of: "Prose with", options: [], range: window).location, length: 0))

            // The colour pass's own stamping (what applyHighlight already does).
            func colour(_ spans: [(range: NSRange, kind: EditorSpanKind)], _ clear: NSRange) -> Int {
                layoutManager.removeTemporaryAttribute(.foregroundColor, forCharacterRange: clear)
                for (range, kind) in spans {
                    guard let color = EditorSyntaxPalette.color(for: kind, increaseContrast: false) else { continue }
                    layoutManager.addTemporaryAttribute(.foregroundColor, value: color, forCharacterRange: range)
                }
                return spans.count
            }
            let plan = Self.bench(c.iters) { EditorAccessibilityAttributes.plan(mapped).count }
            let colourDoc = Self.bench(c.iters) { colour(mapped, full) }
            let stampDoc = Self.bench(c.iters) {
                store.stamp(runs, window: full, wholeDocument: true)
                return runs.count
            }
            let colourWindow = Self.bench(c.iters * 10) { colour(windowSpans, window) }
            let stampWindow = Self.bench(c.iters * 10) {
                store.stamp(windowRuns, window: window, wholeDocument: false)
                return windowRuns.count
            }
            // A keystroke and its undo through the text storage: the editor's
            // (its second layout manager carrying the semantics) against a
            // plain text view's, both coloured, the coordinator's own
            // per-edit work left out.
            let plain = NSTextView(frame: NSRect(x: 0, y: 0, width: 640, height: 480))
            plain.string = doc
            if let plainLayout = plain.layoutManager {
                for (range, kind) in mapped {
                    guard let color = EditorSyntaxPalette.color(for: kind, increaseContrast: false) else { continue }
                    plainLayout.addTemporaryAttribute(.foregroundColor, value: color, forCharacterRange: range)
                }
            }
            func keystroke(_ storage: NSTextStorage?) -> Int {
                storage?.replaceCharacters(in: NSRange(location: line.location, length: 0), with: "x")
                storage?.replaceCharacters(in: NSRange(location: line.location, length: 1), with: "")
                return storage?.length ?? 0
            }
            store.stamp(runs, window: full, wholeDocument: true)
            textView.textStorage?.delegate = nil
            let editPlain = Self.bench(c.iters * 10) { keystroke(plain.textStorage) } / 2
            let edit = Self.bench(c.iters * 10) { keystroke(textView.textStorage) } / 2
            textView.textStorage?.delegate = coordinator
            let lineNative = Self.bench(c.iters * 10) {
                textView.nativeAccessibilityAttributedStringForTesting(for: line)?.length ?? 0
            }
            let lineAX = Self.bench(c.iters * 10) { textView.accessibilityAttributedString(for: line)?.length ?? 0 }
            let docNative = Self.bench(c.iters) {
                textView.nativeAccessibilityAttributedStringForTesting(for: full)?.length ?? 0
            }
            let docAX = Self.bench(c.iters) { textView.accessibilityAttributedString(for: full)?.length ?? 0 }
            print(
                String(
                    format: "%-8@ %5.1f %11.1f %10.1f %11.3f %10.3f %11.3f %6.3f %12.3f %8.3f %11.1f %7.1f",
                    c.label as NSString, plan, colourDoc, stampDoc, colourWindow, stampWindow, editPlain, edit,
                    lineNative, lineAX, docNative, docAX))
            withExtendedLifetime(coordinator) {}
        }
    }

    /// Time `body` over `iters` runs, returning mean ms/run. `body` returns
    /// a span count that is summed into a sink the optimiser can't discard,
    /// so the highlight call can't be elided as dead code.
    private static func bench(_ iters: Int, _ body: () -> Int) -> Double {
        var sink = 0
        let start = DispatchTime.now().uptimeNanoseconds
        for _ in 0..<iters { sink = sink &+ body() }
        let elapsed = DispatchTime.now().uptimeNanoseconds - start
        XCTAssertGreaterThanOrEqual(sink, 0)  // consume the sink
        return Double(elapsed) / 1_000_000.0 / Double(iters)
    }

    /// A representative Markdown note ≈ `targetBytes`: frontmatter, then
    /// repeated mixed blocks (heading, a prose paragraph carrying a
    /// wikilink / inline code / bold / tag / citation / link, a blockquote,
    /// and a fenced code block every 4th block) — the same shape as the
    /// Rust `scan_bench` fixture so the two baselines are comparable.
    static func representativeMarkdown(targetBytes: Int) -> String {
        var s = "---\ntitle: Bench Note\ntags: [bench, editor]\n---\n\n"
        s.reserveCapacity(targetBytes + 512)
        var i = 0
        while s.utf8.count < targetBytes {
            s += "## Section \(i)\n\n"
            s +=
                "Prose with a [[Wikilink \(i)]] and inline `code \(i)` plus **bold \(i)** and a #tag\(i).\n"
            s +=
                "A citation [@source\(i)] sits mid-sentence; see [external](https://example.com/\(i)).\n\n"
            s += "> A blockquote line for section \(i).\n\n"
            if i % 4 == 0 {
                s += "```rust\nfn section_\(i)() -> usize { \(i) * 2 + 1 }\n```\n\n"
            }
            i += 1
        }
        return s
    }
}
