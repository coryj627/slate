// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Maps the canonical Rust editor spans (`editorHighlightSpans`,
/// #377/#391) from their UTF-8 byte-offset coordinate space into the
/// UTF-16 `NSRange` space that `NSTextView` / `NSLayoutManager` use.
///
/// The backend computes spans over **UTF-8 byte offsets** (the same
/// space `slate_core`'s code tokens use); AppKit's text system is
/// **UTF-16**-indexed. This boundary conversion is the one piece of
/// glue #376 needs — done in a single O(n) pass over the source's
/// unicode scalars rather than O(n × spans): a per-span
/// `String.Index(utf16Offset:)` walk would re-scan the prefix for every
/// span and go quadratic on a large note with many spans.
///
/// Pure and `NSWorkspace`-free so it runs off the main thread inside
/// the coordinator's compute task and is unit-testable in isolation.
enum EditorSpanMapping {

    /// Build a UTF-8-byte-offset → UTF-16-offset lookup for exactly the
    /// `needed` byte offsets (the span boundaries), in one pass over
    /// `text`'s unicode scalars.
    ///
    /// For each scalar we advance a running UTF-8 byte count and a
    /// UTF-16 code-unit count; whenever the byte count lands on a needed
    /// boundary we record the matching UTF-16 offset. Offset 0 maps to 0
    /// and is seeded before the walk. A needed offset that doesn't fall
    /// on a scalar boundary (should never happen — backend spans are
    /// char-aligned) simply never gets inserted, so the caller can treat
    /// "absent from the map" as "drop this span" (defensive).
    static func utf16Offsets(forUtf8 needed: Set<Int>, in text: String) -> [Int: Int] {
        var map: [Int: Int] = [:]
        if needed.contains(0) { map[0] = 0 }
        var utf8Count = 0
        var utf16Count = 0
        for scalar in text.unicodeScalars {
            let value = scalar.value
            utf8Count += value < 0x80 ? 1 : value < 0x800 ? 2 : value < 0x1_0000 ? 3 : 4
            utf16Count += value < 0x1_0000 ? 1 : 2
            if needed.contains(utf8Count) { map[utf8Count] = utf16Count }
        }
        return map
    }

    /// Convert byte-offset `EditorSpan`s into `(UTF-16 NSRange, kind)`
    /// pairs. Builds the boundary map once, then looks up each span's
    /// start/end. Spans whose boundaries aren't in the map, or that are
    /// empty/inverted after conversion, are dropped (defensive — the
    /// backend already guarantees ordered, char-aligned, non-degenerate
    /// spans, except for `code(token:)` tokens which legitimately nest
    /// inside their `codeFence`).
    ///
    /// `text` MUST be the exact source the spans were computed from. The
    /// coordinator snapshots the buffer before going off-main and
    /// re-checks `textView.string == snapshot` before applying, so the
    /// offsets always line up with the live buffer at apply time.
    static func utf16Spans(
        from spans: [EditorSpan],
        in text: String
    ) -> [(range: NSRange, kind: EditorSpanKind)] {
        guard !spans.isEmpty else { return [] }
        var needed = Set<Int>()
        needed.reserveCapacity(spans.count * 2)
        for span in spans {
            needed.insert(Int(span.startByte))
            needed.insert(Int(span.endByte))
        }
        let map = utf16Offsets(forUtf8: needed, in: text)
        var out: [(range: NSRange, kind: EditorSpanKind)] = []
        out.reserveCapacity(spans.count)
        for span in spans {
            guard
                let lo = map[Int(span.startByte)],
                let hi = map[Int(span.endByte)],
                hi > lo
            else { continue }
            out.append((NSRange(location: lo, length: hi - lo), span.kind))
        }
        return out
    }
}
