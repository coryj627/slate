// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit

/// The editor's span semantics for VoiceOver (#1224, plan 43 PR 17): the mac
/// twin of the Windows editor peer's text-range attributes (W7-1, #747;
/// contract 37 E-4 and E-9; `EditorSemanticText.cs`).
///
/// **One source.** The canonical spans the colorizer paints
/// (`editorHighlightSpans`, windowed by #379) are the only input; nothing
/// here classifies Markdown. `facets(for:)` is the pure map from a span kind
/// to what it means, mirroring the frozen Windows table row for row:
///
/// | Kind | Windows (E-4, E-9) | mac |
/// |---|---|---|
/// | heading(level) | StyleId 70000+N; StyleName "Heading N" | custom text "Heading N" |
/// | wikilink, link, embed, image | Hyperlink; StyleName | AXLink (a link element); custom text name; the Links rotor |
/// | tag, citation | Hyperlink; StyleName | custom text name |
/// | inlineCode, codeFence, code | StyleName "Code" | custom text "Code" |
/// | blockQuote | StyleId 70014; StyleName "Quote" | custom text "Quote" |
/// | emphasis, strong, strikethrough | IsItalic, FontWeight 700, StrikethroughStyle | AXFontItalic, AXFontBold (macOS 26), AXStrikethrough |
/// | comment, frontmatter | StyleName | custom text name |
///
/// Tags and citations are links on Windows because they activate there
/// (Ctrl+Enter, W2-3); the mac editor activates no span, and the PR 17 spec
/// maps them as styles. Heading level has no AppKit text attribute (macOS 26
/// added a heading role and level for elements, not for text ranges), so it
/// travels as the custom text "Heading N" — the platform limit recorded in
/// gap analysis G29.
///
/// **Copy.** The style names are accessibility attribute values, not
/// announcements: the twin of the Windows `StyleName` values, host-side by
/// design (W7 spec R-1, decision 4), pinned lock-step with the contract 37
/// E-4 table by `EditorAccessibilityAttributesTests`.
///
/// **Mechanism** (the PR 17 spike). Issue #63's rule stands: `NSTextView`'s
/// native reading is kept and attributes are added to it.
/// 1. Off-main, `plan(_:)` turns the colorizer's window of spans into
///    disjoint runs in document order (`computeHighlight`).
/// 2. On main, `applyHighlight` stamps them beside the colour into the
///    coordinator's `EditorSemanticStore`, over the same window, so cost
///    tracks the window, not the document.
/// 3. `SlateEditorTextView.accessibilityAttributedString(for:)` reads the
///    store back for the queried range only (`annotate`) and adds AppKit's
///    text attributes to the native string; its `.link` custom rotor walks
///    the link runs (`linkRange`), which is what VoiceOver's Links rotor
///    (VO-U) and VO-Command-L list and follow.
enum EditorAccessibilityAttributes {

    // MARK: - The map (pure)

    /// What one canonical span contributes, one field per Windows E-4
    /// attribute. Nested spans each contribute; for one field the most
    /// specific (shortest) span wins, so a link inside a heading keeps the
    /// heading level and a heading inside a quote keeps its own (E-4).
    struct Facets: Hashable {
        /// Heading level 1–6: the twin of Windows `StyleId` 70000 + N.
        var headingLevel: Int?
        /// A block quote: the twin of Windows `StyleId` 70014.
        var isQuote: Bool
        /// The Windows `StyleName`, verbatim.
        var styleName: String?
        /// A link: an `AXLink` run and an item of the Links rotor.
        var isLink: Bool
        var isItalic: Bool
        var isBold: Bool
        var isStrikethrough: Bool

        init(
            headingLevel: Int? = nil, isQuote: Bool = false, styleName: String? = nil,
            isLink: Bool = false, isItalic: Bool = false, isBold: Bool = false,
            isStrikethrough: Bool = false
        ) {
            self.headingLevel = headingLevel
            self.isQuote = isQuote
            self.styleName = styleName
            self.isLink = isLink
            self.isItalic = isItalic
            self.isBold = isBold
            self.isStrikethrough = isStrikethrough
        }
    }

    /// The map. One exhaustive switch: a new `EditorSpanKind` is a compile
    /// error here, never a silent omission.
    static func facets(for kind: EditorSpanKind) -> Facets {
        switch kind {
        case .heading(let level):
            return Facets(headingLevel: Int(level), styleName: "Heading \(level)")
        case .wikilink: return Facets(styleName: "Wikilink", isLink: true)
        case .link: return Facets(styleName: "Link", isLink: true)
        case .embed: return Facets(styleName: "Embed", isLink: true)
        case .image: return Facets(styleName: "Image", isLink: true)
        case .tag: return Facets(styleName: "Tag")
        case .citation: return Facets(styleName: "Citation")
        case .inlineCode, .codeFence, .code: return Facets(styleName: "Code")
        case .blockQuote: return Facets(isQuote: true, styleName: "Quote")
        case .emphasis: return Facets(isItalic: true)
        case .strong: return Facets(isBold: true)
        case .strikethrough: return Facets(isStrikethrough: true)
        case .comment: return Facets(styleName: "Comment")
        case .frontmatter: return Facets(styleName: "Frontmatter")
        }
    }

    /// The custom text attribute's strings for a stretch: its structure (the
    /// heading level, else the quote — the twin of Windows `StyleId`) and its
    /// most specific style name, without repeating one. Both from the map.
    static func customText(_ facets: Facets) -> [String] {
        var items: [String] = []
        if let level = facets.headingLevel,
            let name = Self.facets(for: .heading(level: UInt8(clamping: level))).styleName
        {
            items.append(name)
        } else if facets.isQuote, let name = Self.facets(for: .blockQuote).styleName {
            items.append(name)
        }
        if let name = facets.styleName, !items.contains(name) { items.append(name) }
        return items
    }

    // MARK: - The plan (pure; runs off-main in `computeHighlight`)

    enum Facet: Hashable {
        /// Everything but the link, merged over a stretch of text.
        case text(Facets)
        /// One link span. `nested`: a link inside another link (an image in
        /// a link's text), under its own key so the outer link keeps its
        /// whole run.
        case link(nested: Bool)
    }

    struct Run: Equatable {
        let range: NSRange
        let facet: Facet
    }

    /// The runs of `spans`: the text runs (disjoint, document order, each
    /// stretch merged from every span covering it with the most specific
    /// winning per field, touching equal runs joined), then the outer links,
    /// then the nested links (each disjoint, document order). Links are
    /// classified on the way: a link inside one other link is nested; a
    /// deeper or crossing one is dropped and the earlier outer interval kept
    /// (E-9's crossing rule). Pure; it runs off-main with the span
    /// computation, over input core already orders by start, so the sort is
    /// near-linear and the sweep is linear in spans times nesting depth.
    static func plan(_ spans: [(range: NSRange, kind: EditorSpanKind)]) -> [Run] {
        let order = spans.indices.filter { spans[$0].range.length > 0 }.sorted { a, b in
            let ra = spans[a].range, rb = spans[b].range
            if ra.location != rb.location { return ra.location < rb.location }
            if ra.length != rb.length { return ra.length > rb.length }
            return a < b
        }
        guard !order.isEmpty else { return [] }

        var text: [Run] = [], links: [Run] = [], nested: [Run] = []
        // The open spans with a text facet; `cursor` is where emitting resumes.
        var open: [(end: Int, length: Int, facets: Facets)] = []
        var cursor = 0
        func merged() -> Facets {
            var out = Facets()
            var headingLength = Int.max, styleLength = Int.max
            for span in open {
                let facets = span.facets
                if let level = facets.headingLevel, span.length < headingLength {
                    out.headingLevel = level
                    headingLength = span.length
                }
                if let name = facets.styleName, span.length < styleLength {
                    out.styleName = name
                    styleLength = span.length
                }
                out.isQuote = out.isQuote || facets.isQuote
                out.isItalic = out.isItalic || facets.isItalic
                out.isBold = out.isBold || facets.isBold
                out.isStrikethrough = out.isStrikethrough || facets.isStrikethrough
            }
            return out
        }
        func emit(upTo position: Int) {
            while cursor < position {
                open.removeAll { $0.end <= cursor }
                guard let stop = open.map(\.end).min() else {
                    cursor = position
                    return
                }
                let end = min(position, stop)
                let range = NSRange(location: cursor, length: end - cursor)
                let facet = Facet.text(merged())
                if let last = text.last, last.facet == facet, NSMaxRange(last.range) == range.location {
                    text[text.count - 1] = Run(
                        range: NSRange(location: last.range.location, length: NSMaxRange(range) - last.range.location),
                        facet: facet)
                } else {
                    text.append(Run(range: range, facet: facet))
                }
                cursor = end
            }
        }

        var openLinks: [NSRange] = []
        for index in order {
            let range = spans[index].range
            var meaning = facets(for: spans[index].kind)
            emit(upTo: range.location)
            if meaning.isLink {
                while let last = openLinks.last, NSMaxRange(last) <= range.location { openLinks.removeLast() }
                if openLinks.isEmpty {
                    links.append(Run(range: range, facet: .link(nested: false)))
                    openLinks.append(range)
                } else if openLinks.count == 1, NSMaxRange(range) <= NSMaxRange(openLinks[0]) {
                    nested.append(Run(range: range, facet: .link(nested: true)))
                    openLinks.append(range)
                }
                // else: crossing an open link, or nested two deep — dropped.
                meaning.isLink = false
            }
            if meaning != Facets() { open.append((NSMaxRange(range), range.length, meaning)) }
        }
        emit(upTo: Int.max)
        return text + links + nested
    }

    /// The store's attribute keys.
    enum Key {
        /// An `EditorTextSemantics` per text run.
        static let text = NSAttributedString.Key("SlateEditorAXText")
        /// An `EditorAccessibilityLink` per link span.
        static let link = NSAttributedString.Key("SlateEditorAXLink")
        static let nestedLink = NSAttributedString.Key("SlateEditorAXNestedLink")
        static let all = [text, link, nestedLink]
    }

    // MARK: - Reading back (main actor; the AX attributed string)

    /// `native` (the text view's own attributed string for `range`) with
    /// the span semantics added over exactly the stored runs inside `range`:
    /// AXLink, the custom text names, strikethrough, and on macOS 26 bold
    /// and italic. The native text and attributes are kept. Cost is the runs
    /// inside `range`, added in one pass in document order (every link
    /// character lies in a text run, since every link kind has a style
    /// name); a range with none returns `native` as is.
    @MainActor
    static func annotate(
        _ native: NSAttributedString, range: NSRange, store: EditorSemanticStore, textView: NSTextView
    ) -> NSAttributedString {
        let fullRange = NSRange(location: 0, length: store.length)
        let query = NSIntersectionRange(
            NSRange(location: range.location, length: min(range.length, native.length)), fullRange)
        guard query.length > 0 else { return native }

        let text = store.runs(Key.text, in: query)
        guard !text.isEmpty else { return native }
        let links = store.runs(Key.link, in: query)
        let nested = store.runs(Key.nestedLink, in: query)
        var linkCursor = 0, nestedCursor = 0
        /// The element of the link run of `key` covering `location`, if any.
        func element(
            at location: Int, _ key: NSAttributedString.Key, in found: [(range: NSRange, value: Any)],
            _ cursor: inout Int
        ) -> EditorLinkAccessibilityElement? {
            while cursor < found.count, NSMaxRange(found[cursor].range) <= location { cursor += 1 }
            guard cursor < found.count, found[cursor].range.location <= location,
                let link = found[cursor].value as? EditorAccessibilityLink
            else { return nil }
            let whole = store.run(key, at: found[cursor].range.location)?.range ?? found[cursor].range
            return link.element(in: textView, range: whole)
        }

        let out = NSMutableAttributedString(attributedString: native)
        for (run, value) in text {
            guard let facets = (value as? EditorTextSemantics)?.facets else { continue }
            var attributes: [NSAttributedString.Key: Any] = [:]
            let items = customText(facets)
            if !items.isEmpty { attributes[.accessibilityCustomText] = items }
            if facets.isStrikethrough { attributes[.accessibilityStrikethrough] = true }
            if #available(macOS 26.0, *) {
                if facets.isItalic { attributes[.accessibilityFontItalicAttribute] = true }
                if facets.isBold { attributes[.accessibilityFontBoldAttribute] = true }
            }
            // A nested link (an image in a link's text) is the more specific.
            if let link = element(at: run.location, Key.nestedLink, in: nested, &nestedCursor)
                ?? element(at: run.location, Key.link, in: links, &linkCursor)
            {
                attributes[.accessibilityLink] = link
            }
            guard !attributes.isEmpty else { continue }
            out.addAttributes(
                attributes, range: NSRange(location: run.location - range.location, length: run.length))
        }
        return out
    }

    // MARK: - The Links rotor's search (main actor)

    /// The next (or previous) link run after (or before) `location` — the
    /// start of the rotor's current item; `nil` starts from the document's
    /// edge — whose text contains `filter` (VoiceOver's type-ahead), in
    /// document order with an outer link before a link nested in it. Walks
    /// the stored link runs from `location`, one lookup per run.
    @MainActor
    static func linkRange(
        from location: Int?, direction: NSAccessibilityCustomRotor.SearchDirection, filter: String,
        store: EditorSemanticStore, text: NSString
    ) -> NSRange? {
        let length = min(store.length, text.length)
        guard length > 0 else { return nil }
        func matches(_ range: NSRange) -> Bool {
            filter.isEmpty || text.substring(with: range).localizedCaseInsensitiveContains(filter)
        }
        func search(_ key: NSAttributedString.Key) -> NSRange? {
            switch direction {
            case .next:
                let after = location ?? -1
                var index = after + 1
                while index < length {
                    let found = store.segment(key, at: index)
                    if found.value != nil, found.range.location > after, matches(found.range) { return found.range }
                    index = max(NSMaxRange(found.range), index + 1)
                }
            case .previous:
                let before = location ?? length
                var index = min(before, length) - 1
                while index >= 0 {
                    let found = store.segment(key, at: index)
                    if found.value != nil, found.range.location < before, matches(found.range) { return found.range }
                    index = min(found.range.location, index) - 1
                }
            @unknown default:
                return nil
            }
            return nil
        }
        let outer = search(Key.link)
        let inner = search(Key.nestedLink)
        guard let outer else { return inner }
        guard let inner else { return outer }
        switch direction {
        case .previous: return inner.location >= outer.location ? inner : outer
        default: return inner.location > outer.location ? outer : inner
        }
    }

    /// A link's accessible name and rotor label: its source text, as the
    /// Windows Hyperlink peer names it (`GetNameCore`) — at most 512 UTF-16
    /// units, never splitting a surrogate pair, "…" when cut.
    static func name(of range: NSRange, in text: NSString) -> String {
        let range = NSIntersectionRange(range, NSRange(location: 0, length: text.length))
        var length = min(range.length, 512)
        if length < range.length, length > 0,
            UTF16.isLeadSurrogate(text.character(at: range.location + length - 1))
        {
            length -= 1
        }
        let name = text.substring(with: NSRange(location: range.location, length: length))
        return length < range.length ? name + "…" : name
    }
}

/// The VoiceOver semantics' own run storage (#1224): the temporary attributes
/// of a second `NSLayoutManager` on the editor's text storage, one that is
/// never given a text container and so lays nothing out. AppKit shifts its
/// runs through every edit exactly as it does the colour pass's on the text
/// view's own layout manager (inserted text carries nothing until the next
/// pass repaints its window), so there is no mirror to keep in lockstep, and
/// a windowed pass clears and restamps only its window.
///
/// Why a second layout manager rather than the text view's own, beside the
/// colour: one run storage shared by both made a whole 8 MB pass cost the
/// colour 1.5× its own time and the semantics another 1.9×; apart, the
/// colour pays nothing and the semantics about what the colour does. (A
/// plain attributed-string mirror was measured too: it made each keystroke's
/// window cost O(document) — 4.6 ms at 8 MB, against 0.02 ms here. All
/// release-build numbers for this PR.)
@MainActor
final class EditorSemanticStore {
    private let layout = NSLayoutManager()
    private weak var storage: NSTextStorage?

    init() {
        layout.backgroundLayoutEnabled = false
    }

    var length: Int { storage?.length ?? 0 }

    /// Whether these runs describe `storage`'s text.
    func rides(_ storage: NSTextStorage?) -> Bool {
        storage != nil && storage === self.storage
    }

    /// Follow the editor's text storage (the coordinator's attach and
    /// re-bind). The new text carries no semantics until the
    /// whole-document pass that follows every attach stamps them.
    func attach(to storage: NSTextStorage?) {
        guard storage !== self.storage else { return }
        self.storage?.removeLayoutManager(layout)
        self.storage = storage
        storage?.addLayoutManager(layout)
    }

    /// Stamp a pass's plan: each key cleared over the pass's window, widened
    /// over that key's own runs straddling the window's edges (the #379
    /// stranding guard, per key), then the runs added in plan order. A
    /// whole-document pass clears everything first.
    func stamp(_ plan: [EditorAccessibilityAttributes.Run], window: NSRange, wholeDocument: Bool) {
        let full = NSRange(location: 0, length: length)
        guard full.length > 0 else { return }
        for key in EditorAccessibilityAttributes.Key.all {
            let clear = wholeDocument ? full : widen(NSIntersectionRange(window, full), over: key)
            layout.removeTemporaryAttribute(key, forCharacterRange: clear)
        }
        var values: [EditorAccessibilityAttributes.Facets: EditorTextSemantics] = [:]
        for run in plan {
            let range = NSIntersectionRange(run.range, full)
            guard range.length > 0 else { continue }
            switch run.facet {
            case .text(let facets):
                let value = values[facets] ?? EditorTextSemantics(facets)
                values[facets] = value
                layout.addTemporaryAttribute(EditorAccessibilityAttributes.Key.text, value: value, forCharacterRange: range)
            case .link(let nested):
                // A fresh identity per link span: two adjacent links stay two.
                layout.addTemporaryAttribute(
                    nested ? EditorAccessibilityAttributes.Key.nestedLink : EditorAccessibilityAttributes.Key.link,
                    value: EditorAccessibilityLink(), forCharacterRange: range)
            }
        }
    }

    /// `range` widened to whole runs of `key` straddling either edge. Each
    /// probe asks first whether a run is there: the longest effective range
    /// of an absent key is the whole stretch without it, work this never uses.
    private func widen(_ range: NSRange, over key: NSAttributedString.Key) -> NSRange {
        var lo = range.location
        var hi = NSMaxRange(range)
        if lo > 0, let run = run(key, at: lo - 1) { lo = min(lo, run.range.location) }
        if hi < length, let run = run(key, at: hi) { hi = max(hi, NSMaxRange(run.range)) }
        return NSRange(location: lo, length: hi - lo)
    }

    /// The run of `key` at `index` (its whole extent), or nil when none is there.
    func run(_ key: NSAttributedString.Key, at index: Int) -> (range: NSRange, value: Any)? {
        guard index >= 0, index < length,
            layout.temporaryAttribute(key, atCharacterIndex: index, effectiveRange: nil) != nil
        else { return nil }
        let found = segment(key, at: index)
        return found.value.map { (found.range, $0) }
    }

    /// The maximal stretch around `index` over which `key` has one value
    /// (nil included).
    func segment(_ key: NSAttributedString.Key, at index: Int) -> (range: NSRange, value: Any?) {
        var range = NSRange(location: index, length: 1)
        let value = layout.temporaryAttribute(
            key, atCharacterIndex: index, longestEffectiveRange: &range, in: NSRange(location: 0, length: length))
        return (range, value)
    }

    /// The runs of `key` inside `range`, in order, clipped to it.
    func runs(_ key: NSAttributedString.Key, in range: NSRange) -> [(range: NSRange, value: Any)] {
        var out: [(range: NSRange, value: Any)] = []
        let range = NSIntersectionRange(range, NSRange(location: 0, length: length))
        var index = range.location
        let end = NSMaxRange(range)
        while index < end {
            var effective = NSRange(location: index, length: 0)
            let value = layout.temporaryAttribute(
                key, atCharacterIndex: index, longestEffectiveRange: &effective, in: range)
            if let value { out.append((effective, value)) }
            index = max(NSMaxRange(effective), index + 1)
        }
        return out
    }
}

/// The value stored under `EditorAccessibilityAttributes.Key.text`: one
/// stretch's merged facets. Equal by value, so touching runs of the same
/// meaning join.
final class EditorTextSemantics: NSObject {
    let facets: EditorAccessibilityAttributes.Facets

    init(_ facets: EditorAccessibilityAttributes.Facets) {
        self.facets = facets
        super.init()
    }

    override func isEqual(_ object: Any?) -> Bool {
        (object as? EditorTextSemantics)?.facets == facets
    }

    override var hash: Int { facets.hashValue }

    override var description: String { "\(facets)" }
}

/// One link span's identity in the store: the value stored under the link
/// keys, a fresh one per span per pass, so two adjacent links stay two runs.
/// It vends the span's accessibility element on first read.
final class EditorAccessibilityLink: NSObject {
    private var element: EditorLinkAccessibilityElement?

    @MainActor
    func element(in textView: NSTextView, range: NSRange) -> EditorLinkAccessibilityElement {
        if let element {
            element.range = range
            return element
        }
        let element = EditorLinkAccessibilityElement(textView: textView, range: range)
        self.element = element
        return element
    }
}

/// The value of an `AXLink` run: a link named by its source text, whose
/// parent is the editor. VoiceOver resolves the run to it (the spike: VO-Space
/// on a word inside a link pressed it). It carries no press action — the
/// mac editor activates no span (Windows' Ctrl+Enter and Invoke have no mac
/// counterpart yet, #1431) — so VoiceOver offers none.
final class EditorLinkAccessibilityElement: NSAccessibilityElement {
    weak var textView: NSTextView?
    /// The link's range at its last read; the store shifts the run through
    /// edits and every read refreshes this.
    var range: NSRange

    init(textView: NSTextView, range: NSRange) {
        self.textView = textView
        self.range = range
        super.init()
    }

    override func accessibilityRole() -> NSAccessibility.Role? { .link }

    override func accessibilityLabel() -> String? {
        guard let text = textView?.textStorage?.string else { return nil }
        return EditorAccessibilityAttributes.name(of: range, in: text as NSString)
    }

    override func accessibilityParent() -> Any? { textView }

    override func isAccessibilityElement() -> Bool { true }

    override func accessibilityFrame() -> NSRect {
        guard let textView, let layoutManager = textView.layoutManager,
            let container = textView.textContainer, let window = textView.window
        else { return .zero }
        let length = textView.textStorage?.length ?? 0
        let range = NSIntersectionRange(range, NSRange(location: 0, length: length))
        guard range.length > 0 else { return .zero }
        let glyphs = layoutManager.glyphRange(forCharacterRange: range, actualCharacterRange: nil)
        var union = NSRect.null
        layoutManager.enumerateEnclosingRects(
            forGlyphRange: glyphs, withinSelectedGlyphRange: NSRange(location: NSNotFound, length: 0),
            in: container
        ) { rect, _ in
            union = union.union(rect)
        }
        guard !union.isNull else { return .zero }
        let origin = textView.textContainerOrigin
        let local = union.offsetBy(dx: origin.x, dy: origin.y)
        return window.convertToScreen(textView.convert(local, to: nil))
    }
}

/// The Links rotor's search: VoiceOver asks it for the next or previous
/// link from its current item, and it answers with the editor and the
/// link's range, labelled by its text.
final class EditorLinkRotorSearch: NSObject, NSAccessibilityCustomRotorItemSearchDelegate {
    weak var textView: SlateEditorTextView?

    init(textView: SlateEditorTextView) {
        self.textView = textView
        super.init()
    }

    func rotor(
        _ rotor: NSAccessibilityCustomRotor,
        resultFor searchParameters: NSAccessibilityCustomRotor.SearchParameters
    ) -> NSAccessibilityCustomRotor.ItemResult? {
        guard let textView else { return nil }
        let current = searchParameters.currentItem?.targetRange.location
        let direction = searchParameters.searchDirection
        let filter = searchParameters.filterString
        // AppKit asks on the main thread.
        let found: (range: NSRange, label: String)? = MainActor.assumeIsolated {
            guard let store = textView.coordinator?.semanticStore, let storage = textView.textStorage,
                store.rides(storage)
            else { return nil }
            let text = storage.string as NSString
            guard
                let range = EditorAccessibilityAttributes.linkRange(
                    from: current == NSNotFound ? nil : current, direction: direction,
                    filter: filter, store: store, text: text)
            else { return nil }
            return (range, EditorAccessibilityAttributes.name(of: range, in: text))
        }
        guard let found else { return nil }
        let result = NSAccessibilityCustomRotor.ItemResult(targetElement: textView)
        result.targetRange = found.range
        result.customLabel = found.label
        return result
    }
}
