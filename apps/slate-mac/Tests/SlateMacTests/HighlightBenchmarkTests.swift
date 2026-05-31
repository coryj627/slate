// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

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
