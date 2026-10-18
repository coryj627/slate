// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// The editor's activate-at-cursor verb (#1431): the mac twin of Windows'
/// `slate.editor.activateAtCaret` (W2-3; `EditorInteractions.ActivateAt`),
/// "Activate the link, tag, citation, embed, or task under the cursor."
///
/// **Triggers.** ⌘↩ in the editor and the palette row act at the caret. A
/// press acts at a link: VoiceOver's VO-Space presses the text area while
/// the insertion point is inside one (VO-Command-L leaves it at the link's
/// start; `SlateEditorTextView.accessibilityPerformPress`), and an assistive
/// client may press the link's own element
/// (`EditorLinkAccessibilityElement`, the AXLink runs of #1224). All of them
/// resolve here, from core's canonical spans
/// (`DocumentBuffer.highlightInRange`, the spans the colorizer paints and the
/// link elements are built from) and core's display-math ranges, and nothing
/// here classifies Markdown. The pointer origin is the rule's third leg,
/// Windows' Ctrl+click; its mac gesture, ⌘-click, is #1466.
///
/// **The rule is Windows'** (`TryGetActionableSpan`): the innermost span that
/// contains the location wins, a span the location sits strictly inside before
/// one it only touches at its end; the caret just past a span's last character
/// still counts from the keyboard (the caret sits after `]]` once a link is
/// typed), never from a press or the pointer; a location inside a code fence
/// or display math activates nothing. With no span, the keyboard toggles the
/// task on the caret's line and the pointer the task whose checkbox it is on;
/// a press is always on a link.
///
/// What each target does is the host's (`AppState.performEditorActivation`):
/// the same AppState actions the reading view and the panels use, with
/// core's words for every outcome, including the refusal when nothing is
/// there (`NothingToActivateAtCursor`).
enum EditorActivation {

    enum Origin: Equatable {
        /// ⌘↩ or the palette row, at the caret.
        case keyboard
        /// A press on a link (VoiceOver's VO-Space on the text area, or a
        /// press of the link's element), at the link: never a task.
        case press
        /// A click, at the character under the pointer: Windows' Ctrl+click
        /// (`EditorInteractionOrigin.Pointer`). No mac gesture sends it yet;
        /// ⌘-click is #1466.
        case pointer
    }

    /// What one activation acts on.
    enum Target: Equatable {
        /// A wikilink, a Markdown link or an image: followed.
        case link(range: NSRange, kind: EditorSpanKind)
        /// An `![[…]]` embed: previewed, as ⌘E does (Windows' Ctrl+Enter
        /// on an embed opens the same interaction popover as Ctrl+E).
        case embed(range: NSRange)
        /// A `#tag`: the Files sidebar filtered to it (Windows' editor path;
        /// the reading view's tag opens tag-scoped search, contract SD-4).
        case tag(range: NSRange)
        /// A `[@key]` citation: its popover.
        case citation(range: NSRange)
        /// No span: the task under the location, if there is one.
        case task(location: Int, origin: Origin)
        /// Inside display math or a code fence.
        case nothing
    }

    /// The span kinds an activation acts on: the six E-9 link kinds.
    static func isActionable(_ kind: EditorSpanKind) -> Bool {
        switch kind {
        case .wikilink, .embed, .tag, .citation, .link, .image: return true
        default: return false
        }
    }

    /// The span at `location` an activation acts on, by Windows'
    /// `TryGetActionableSpan` rule (see the type doc); nil when there is
    /// none or a code fence contains the location.
    static func actionableSpan(
        at location: Int, origin: Origin, in spans: [(range: NSRange, kind: EditorSpanKind)]
    ) -> (range: NSRange, kind: EditorSpanKind)? {
        let includeRightEdge = origin == .keyboard
        let containing = spans.filter { span in
            let range = span.range
            guard range.location <= location else { return false }
            let end = NSMaxRange(range)
            return location < end || (includeRightEdge && range.length > 0 && location == end)
        }
        if containing.contains(where: { $0.kind == .codeFence }) { return nil }
        // Strictly inside before touching the end; then the innermost.
        let ordered = containing.sorted { a, b in
            let aInside = location < NSMaxRange(a.range)
            let bInside = location < NSMaxRange(b.range)
            if aInside != bInside { return aInside }
            return a.range.length < b.range.length
        }
        return ordered.first { isActionable($0.kind) }
    }

    /// Whether `location` lies inside one of `ranges` (display math).
    static func isInside(_ location: Int, anyOf ranges: [NSRange]) -> Bool {
        ranges.contains { $0.location <= location && location < NSMaxRange($0) }
    }

    /// The target at `location`: math first (Windows returns before reading
    /// spans), then the actionable span, then the task fallback a press
    /// never takes.
    static func target(
        at location: Int, origin: Origin, spans: [(range: NSRange, kind: EditorSpanKind)],
        mathRanges: [NSRange]
    ) -> Target {
        if isInside(location, anyOf: mathRanges) { return .nothing }
        guard let span = actionableSpan(at: location, origin: origin, in: spans) else {
            return origin == .press ? .nothing : .task(location: location, origin: origin)
        }
        switch span.kind {
        case .embed: return .embed(range: span.range)
        case .tag: return .tag(range: span.range)
        case .citation: return .citation(range: span.range)
        default: return .link(range: span.range, kind: span.kind)
        }
    }
}

/// One resolved activation, handed from the editor to `AppState`: the target
/// and the live-buffer facts the host needs to act on it without reaching
/// into AppKit.
struct EditorActivationRequest: Equatable {
    let target: EditorActivation.Target
    /// The span's authored source, from the live buffer ("[[Note|alias]]",
    /// "#tag", "[@key, p. 2]"); empty for a task or nothing.
    let spanText: String
    /// The span's body-relative UTF-8 byte range, for matching the saved
    /// note's link records by their authored span.
    let spanBytes: Range<Int>?
    /// The 1-based body line of the activation's location.
    let line: Int
    /// The activation's location as a body-relative UTF-8 byte offset (a
    /// pointer activation's task checkbox match).
    let byteOffset: Int
    /// For an embed: the target ⌘E previews (`findEditorEmbedSpans`).
    let embedTarget: String?
}
