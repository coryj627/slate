// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Pure source-slice helpers for the reading view's block renderers
/// (U3-1, #465).
///
/// A `ReadingBlock.source` slice still carries its authored *chrome* — `#`
/// heading markers, list markers, `>` quote prefixes, code-fence lines.
/// Reading mode renders content, not chrome, so each renderer strips exactly
/// its own marker form here. These helpers deliberately do NOT re-classify
/// anything (the Rust segmentation already decided each block's kind); they
/// only remove the marker syntax that the classification proves is present,
/// and they degrade to the verbatim slice when the expected marker isn't
/// found — never dropping authored bytes.
enum ReadingBlockSource {

    // MARK: - Headings

    /// Strip ATX `#` markers (and an optional closing `#` run) or a setext
    /// underline, returning the heading's text content.
    static func headingText(_ source: String) -> String {
        let lines = source.split(separator: "\n", omittingEmptySubsequences: false)
        guard let first = lines.first else { return source }
        let trimmedFirst = first.trimmingCharacters(in: .whitespaces)

        // ATX: 1–6 `#` then space(s). Also trim a trailing closing sequence
        // (` ###`) per CommonMark.
        var hashes = 0
        for ch in trimmedFirst {
            if ch == "#" { hashes += 1 } else { break }
        }
        if (1...6).contains(hashes) {
            var text = String(trimmedFirst.dropFirst(hashes))
            guard text.isEmpty || text.first == " " || text.first == "\t" else {
                // `#not-a-heading` — the classifier said Heading, so this is
                // a setext or unusual form; fall through to setext handling.
                return setextOrVerbatim(lines: lines, source: source)
            }
            text = text.trimmingCharacters(in: .whitespaces)
            while text.hasSuffix("#") { text = String(text.dropLast()) }
            return text.trimmingCharacters(in: .whitespaces)
        }
        return setextOrVerbatim(lines: lines, source: source)
    }

    /// Setext form: `Title\n====` / `Title\n----` → the first line.
    private static func setextOrVerbatim(
        lines: [Substring], source: String
    ) -> String {
        if lines.count >= 2 {
            let underline = lines[1].trimmingCharacters(in: .whitespaces)
            if !underline.isEmpty,
                underline.allSatisfy({ $0 == "=" }) || underline.allSatisfy({ $0 == "-" })
            {
                return lines[0].trimmingCharacters(in: .whitespaces)
            }
        }
        return source.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    // MARK: - List items

    struct ListItemParts: Equatable {
        /// The authored marker, verbatim (`-`, `*`, `+`, `3.`, `12)`). The
        /// renderer shows `•` for unordered markers but the ORDERED number is
        /// displayed verbatim — the source carries the real ordinal, so no
        /// re-derivation (and no wrong renumbering) is possible.
        var marker: String
        /// Inline content after the marker (and after the `[x]` checkbox for
        /// task items), continuation lines preserved verbatim.
        var content: String
        /// The task status char between `[` and `]`, when present.
        var taskChar: String?
    }

    /// Split a list-item slice into marker / optional task box / content.
    /// Returns nil when no marker is found (degrade: render slice verbatim).
    ///
    /// `stripTaskBox` gates the `[c]` removal and must be true ONLY when the
    /// Rust block kind already says this item IS a task — taskhood belongs to
    /// the classifier, not this splitter. A plain list item that merely looks
    /// boxy keeps its bracket text verbatim: `1. [v] Visible` (ordered items
    /// are never tasks) and `- [v]x` (no space after the box) both reach the
    /// plain-list renderer, and unconditional stripping lost that authored
    /// content (Codoki, #514).
    static func listItemParts(
        _ source: String, stripTaskBox: Bool = false
    ) -> ListItemParts? {
        let firstLineEnd = source.firstIndex(of: "\n") ?? source.endIndex
        let firstLine = source[source.startIndex..<firstLineEnd]
        let rest = firstLineEnd < source.endIndex
            ? String(source[source.index(after: firstLineEnd)...]) : ""

        var index = firstLine.startIndex
        // Leading indentation (nested items keep their indent in the slice).
        while index < firstLine.endIndex,
            firstLine[index] == " " || firstLine[index] == "\t"
        {
            index = firstLine.index(after: index)
        }
        guard index < firstLine.endIndex else { return nil }

        var marker = ""
        let ch = firstLine[index]
        if ch == "-" || ch == "*" || ch == "+" {
            marker = String(ch)
            index = firstLine.index(after: index)
        } else if ch.isNumber {
            var digitsEnd = index
            while digitsEnd < firstLine.endIndex, firstLine[digitsEnd].isNumber {
                digitsEnd = firstLine.index(after: digitsEnd)
            }
            guard digitsEnd < firstLine.endIndex,
                firstLine[digitsEnd] == "." || firstLine[digitsEnd] == ")"
            else { return nil }
            marker = String(firstLine[index...digitsEnd])
            index = firstLine.index(after: digitsEnd)
        } else {
            return nil
        }

        // Exactly the marker-terminating whitespace.
        while index < firstLine.endIndex,
            firstLine[index] == " " || firstLine[index] == "\t"
        {
            index = firstLine.index(after: index)
        }

        var content = String(firstLine[index...])
        var taskChar: String? = nil
        // Task box: `[c] ` — same shape the Rust tasks grammar recognizes.
        // Only split it off when the caller vouched (via `stripTaskBox`)
        // that the classifier marked this item a task.
        if stripTaskBox, content.hasPrefix("["), content.count >= 3 {
            let afterOpen = content.index(after: content.startIndex)
            let closeIndex = content.index(afterOpen, offsetBy: 1)
            if content[closeIndex] == "]" {
                taskChar = String(content[afterOpen])
                var remainder = String(content[content.index(after: closeIndex)...])
                if remainder.hasPrefix(" ") { remainder.removeFirst() }
                content = remainder
            }
        }

        if !rest.isEmpty {
            content += "\n" + rest
        }
        return ListItemParts(marker: marker, content: content, taskChar: taskChar)
    }

    // MARK: - Block quotes

    /// Strip up to `depth` `>` markers (each with one optional following
    /// space) from the start of every line.
    static func quoteContent(_ source: String, depth: UInt8) -> String {
        let lines = source.split(separator: "\n", omittingEmptySubsequences: false)
        let stripped = lines.map { line -> String in
            var view = Substring(line)
            for _ in 0..<max(1, Int(depth)) {
                let lead = view.drop(while: { $0 == " " || $0 == "\t" })
                guard lead.first == ">" else { break }
                view = lead.dropFirst()
                if view.first == " " { view = view.dropFirst() }
            }
            return String(view)
        }
        return stripped.joined(separator: "\n")
    }

    // MARK: - Code fences

    /// Drop the opening/closing fence lines (``` / ~~~), returning the code
    /// interior for the fallback `CodeBlock` when no pipeline-extracted block
    /// matched. Indented (non-fenced) blocks dedent four spaces.
    static func fenceInterior(_ source: String) -> String {
        var lines = source.split(separator: "\n", omittingEmptySubsequences: false)
            .map(String.init)
        guard let first = lines.first else { return source }
        let trimmedFirst = first.trimmingCharacters(in: .whitespaces)
        if trimmedFirst.hasPrefix("```") || trimmedFirst.hasPrefix("~~~") {
            lines.removeFirst()
            if let last = lines.last {
                let trimmedLast = last.trimmingCharacters(in: .whitespaces)
                if trimmedLast.hasPrefix("```") || trimmedLast.hasPrefix("~~~") {
                    lines.removeLast()
                }
            }
            return lines.joined(separator: "\n")
        }
        // Indented code block: strip the 4-space (or tab) indent.
        return lines.map { line in
            if line.hasPrefix("    ") { return String(line.dropFirst(4)) }
            if line.hasPrefix("\t") { return String(line.dropFirst(1)) }
            return line
        }.joined(separator: "\n")
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
