// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Presentation-only source helpers for the reading view (U3-1, #465).
///
/// **The inline-content strippers are gone (#967).** Heading ATX/setext
/// text, the list marker + task-box split, and the blockquote depth strip
/// used to live here and were re-derived in Swift; they are now computed
/// once in `slate-core` and arrive as
/// `ReadingInlineSegment.content` / `ReadingBlockInlines.listMarker`
/// (executable spec
/// `docs/plans/18_windows_port/specs/w3_inline_runs_spec.md` §2). What
/// remains is deliberately presentation- or index-only: nothing here
/// classifies or splits authored syntax.
enum ReadingBlockSource {

    // MARK: - Code fences

    /// Return the bytes authored between the opening and closing fence lines.
    ///
    /// The code-block *interior* is carried authoritatively from the Rust
    /// parser (`ReadingBlockKind.codeFence`/`.diagram`'s `interior`), so no
    /// Swift-side fence-stripping heuristic exists for it. This helper stays
    /// only for YAML block scalars, where it deliberately preserves the line
    /// ending immediately before the closing fence — that byte distinguishes
    /// clip/strip/keep chomping semantics.
    static func fenceInteriorVerbatim(_ source: String) -> String {
        guard let openingLineEnd = source.firstIndex(of: "\n") else { return source }
        let openingLine = source[..<openingLineEnd]
            .trimmingCharacters(in: .whitespacesAndNewlines)
        let marker: String
        if openingLine.hasPrefix("```") {
            marker = "```"
        } else if openingLine.hasPrefix("~~~") {
            marker = "~~~"
        } else {
            return source
        }

        let contentStart = source.index(after: openingLineEnd)
        var logicalEnd = source.endIndex
        if logicalEnd > contentStart,
            source[source.index(before: logicalEnd)] == "\n"
        {
            logicalEnd = source.index(before: logicalEnd)
        }
        let closingLineStart = source[..<logicalEnd].lastIndex(of: "\n")
            .map { source.index(after: $0) }
            ?? source.startIndex
        let closingLine = source[closingLineStart..<logicalEnd]
            .trimmingCharacters(in: .whitespacesAndNewlines)
        guard closingLine.hasPrefix(marker), closingLineStart >= contentStart else {
            return String(source[contentStart...])
        }
        return String(source[contentStart..<closingLineStart])
    }

    // MARK: - Line numbers

    /// Byte offsets of every line start in `text` (UTF-8). Computed once per
    /// parse so task rows can map `ReadingBlock.byteStart` → the 1-based line
    /// number `TaskItem.line` uses.
    static func lineStartOffsets(of text: String) -> [Int] {
        var starts = [0]
        var offset = 0
        for byte in text.utf8 {
            offset += 1
            if byte == UInt8(ascii: "\n") {
                starts.append(offset)
            }
        }
        return starts
    }

    /// 1-based line number containing UTF-8 byte `offset` (binary search
    /// over `lineStartOffsets`).
    static func lineNumber(forByteOffset offset: Int, lineStarts: [Int]) -> Int {
        var low = 0
        var high = lineStarts.count - 1
        while low < high {
            let mid = (low + high + 1) / 2
            if lineStarts[mid] <= offset {
                low = mid
            } else {
                high = mid - 1
            }
        }
        return low + 1
    }
}
