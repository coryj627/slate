// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Activation routing for the reading view's inline links (U3-1, #465).
///
/// The inline pipeline (`ReadingInlineMapper`) rewrites wikilinks / embeds /
/// tags / citations into markdown links carrying custom schemes; `ReadingView`
/// installs `route(_:)` as its `\.openURL` action so activating any of those
/// runs lands here. The router itself is **closure-based** so `ReadingView`
/// stays mountable without an `AppState` (tests use recording fakes; U3-2
/// mounts it with `.live(appState:)`).
///
/// Scheme table (targets percent-encoded by `encodedURL`):
///
///   slate-wiki://<target>    wikilink        → open the target note
///   slate-embed://<target>   embed (`![[…]]`) → open the embed's source note
///   slate-tag://<name>       tag             → search overlay, prefiltered
///   slate-cite://<raw>       citation        → expand the citation popover
///
/// Internal markdown links like `[t](note.md)` parse as scheme-less URLs;
/// the mapper's style pass rewrites those onto `slate-wiki` (Slate markdown
/// destinations are vault-rooted/basename — the same `target_raw` form the
/// wiki path already matches), so they activate like wikilinks.
///
/// Anything else: `http`/`https`/`mailto` pass through to the system (the
/// same allowlist `AppState.openLink` enforces — `file:`/`javascript:`/custom
/// schemes must NOT be handed to LaunchServices, where a typo'd markdown link
/// would hand control to whatever app registered the scheme). Non-allowlisted
/// URLs are discarded — and the mapper strips the link affordance from every
/// discard-class run, so nothing renders as activatable and then dead-clicks.
struct ReadingLinkRouter {

    static let wikiScheme = "slate-wiki"
    static let embedScheme = "slate-embed"
    static let tagScheme = "slate-tag"
    static let citeScheme = "slate-cite"
    /// Codex round 2: internal MARKDOWN destinations rewritten by the
    /// mapper ride their own scheme so the routed value retains its
    /// source grammar — `^` is an anchor marker in wikilink grammar but
    /// a legal path character in a markdown destination, and one shared
    /// scheme made `[[note^block]]` able to activate a sibling
    /// `[m](note^block)` record.
    static let wikiMarkdownScheme = "slate-wikimd"

    /// Which authoring grammar produced a wiki-routed target — decides
    /// the anchor-cut rules `candidateKeys` applies.
    enum WikiTargetGrammar: Equatable {
        case wikilink
        case markdownDestination
    }

    /// Does a record's authoring kind (`links_db.rs`: "wikilink" /
    /// "markdown") match the grammar that routed the activation? Codex
    /// round 3: matching on `targetRaw` alone let an UNSAVED
    /// `[[note^block]]` activate a saved `[m](note^block)` record
    /// through the verbatim arm — a cross-grammar record hit is always
    /// the wrong record.
    static func recordKindMatches(
        _ kind: String, grammar: WikiTargetGrammar
    ) -> Bool {
        switch grammar {
        case .wikilink: return kind == "wikilink"
        case .markdownDestination: return kind == "markdown"
        }
    }

    /// Kind-partitioned record sets for the styling classifier (Codex
    /// round 3 — one flat set let a run of one grammar classify
    /// against the other grammar's records). The EMPTY value is the
    /// honest "no records for this note" classification (every run
    /// unresolved), used while `currentOutgoingLinks` still belongs to
    /// a previous note.
    struct LinkRecordSets: Equatable {
        var knownWikilink: Set<String> = []
        var unresolvedWikilink: Set<String> = []
        var knownMarkdown: Set<String> = []
        var unresolvedMarkdown: Set<String> = []

        init() {}

        init(records: [OutgoingLink]) {
            for record in records where !record.isEmbed && !record.isExternal {
                switch record.kind {
                case "wikilink":
                    knownWikilink.insert(record.targetRaw)
                    if record.isUnresolved {
                        unresolvedWikilink.insert(record.targetRaw)
                    }
                case "markdown":
                    knownMarkdown.insert(record.targetRaw)
                    if record.isUnresolved {
                        unresolvedMarkdown.insert(record.targetRaw)
                    }
                default:
                    break
                }
            }
        }

        func known(for grammar: WikiTargetGrammar) -> Set<String> {
            grammar == .wikilink ? knownWikilink : knownMarkdown
        }

        func unresolved(for grammar: WikiTargetGrammar) -> Set<String> {
            grammar == .wikilink ? unresolvedWikilink : unresolvedMarkdown
        }
    }

    /// Wikilink target (anchor form, e.g. `Note#Section`), decoded.
    var openWikiLink: (String, WikiTargetGrammar) -> Void
    /// Embed target (cache-key form `target#suffix`), decoded.
    var openEmbed: (String) -> Void
    /// Tag name WITHOUT the leading `#`.
    var openTag: (String) -> Void
    /// The citation's raw source text (e.g. `[@key, p. 23]`) — the stable
    /// key `RenderedCitation.raw` carries (it has no byte offset field).
    var expandCitation: (String) -> Void

    /// A router whose slate-scheme activations do nothing. Used by previews /
    /// fixtures; U3-1 ships `ReadingView` unmounted, so nothing user-facing
    /// routes through this.
    static let inert = ReadingLinkRouter(
        openWikiLink: { _, _ in },
        openEmbed: { _ in },
        openTag: { _ in },
        expandCitation: { _ in }
    )

    // MARK: - URL codec

    /// Build a routing URL: `<scheme>://<percent-encoded target>`.
    ///
    /// The target is encoded with a strict unreserved-only allowed set so
    /// `/`, `#`, `|`, spaces, and unicode all survive as percent-octets in
    /// the authority slot — `decodedTarget` reverses it without ever
    /// consulting Foundation's host parsing (whose reg-name rules differ
    /// across OS versions).
    static func encodedURL(scheme: String, target: String) -> URL? {
        let unreserved = CharacterSet.alphanumerics
            .union(CharacterSet(charactersIn: "-._~"))
        guard
            let encoded = target.addingPercentEncoding(
                withAllowedCharacters: unreserved)
        else { return nil }
        return URL(string: "\(scheme)://\(encoded)")
    }

    /// Reverse of `encodedURL`: strip `<scheme>://` and percent-decode.
    static func decodedTarget(from url: URL) -> String {
        let absolute = url.absoluteString
        guard let separator = absolute.range(of: "://") else { return "" }
        let encoded = String(absolute[separator.upperBound...])
        return encoded.removingPercentEncoding ?? ""
    }

    /// Strip a WIKILINK anchor suffix: `#heading` first, `^block` only
    /// when no `#` exists — the exact `links.rs::split_wikilink_body`
    /// precedence (Codex review: a first-marker cut turned
    /// `note^draft#sec` into `note`). Display/name derivations for
    /// wiki-grammar strings (embed titles) use this; RECORD MATCHING
    /// must go through `candidateKeys(for:)`, which also covers the
    /// markdown-destination grammar where `^` is a path character.
    static func baseTarget(of target: String) -> String {
        // Trim mirrors links.rs (red-team: padded targets left styling
        // and activation disagreeing).
        let trimmed = target.trimmingCharacters(in: .whitespaces)
        if let hash = trimmed.firstIndex(of: "#") {
            return String(trimmed[trimmed.startIndex..<hash])
                .trimmingCharacters(in: .whitespaces)
        }
        if let caret = trimmed.firstIndex(of: "^") {
            return String(trimmed[trimmed.startIndex..<caret])
                .trimmingCharacters(in: .whitespaces)
        }
        return trimmed
    }

    /// Ordered record-match keys for one routed target, EXACT per
    /// grammar (Codex round 2 — a grammar-blind list let a wikilink
    /// activate a markdown sibling's record): wikilink grammar cuts the
    /// anchor at the first `#`, else the first `^` (legacy block ref) —
    /// `links.rs::split_wikilink_body`; markdown-destination grammar
    /// cuts ONLY at `#`, `^` is a legal path character
    /// (`links.rs::split_markdown_target`). The verbatim target closes
    /// each list as the pre-#509 defense (rows still carrying a
    /// fragment in `targetRaw`). One list for the live router's record
    /// match AND the mapper's styling classification — agreement by
    /// construction.
    static func candidateKeys(
        for target: String, grammar: WikiTargetGrammar
    ) -> [String] {
        let trimmed = target.trimmingCharacters(in: .whitespaces)
        var keys: [String] = []
        func push(_ key: String) {
            if !key.isEmpty, !keys.contains(key) { keys.append(key) }
        }
        switch grammar {
        case .wikilink:
            push(baseTarget(of: trimmed))
        case .markdownDestination:
            if let hash = trimmed.firstIndex(of: "#") {
                push(
                    String(trimmed[trimmed.startIndex..<hash])
                        .trimmingCharacters(in: .whitespaces))
            }
        }
        push(trimmed)
        return keys
    }

    /// Codex round 2: `AppState.currentOutgoingLinks` is intentionally
    /// retained from the PREVIOUS note while the incoming note's query
    /// runs (#90 panel anti-flicker). The reading surface must never
    /// classify or activate against another note's records — until
    /// ownership matches, every run is treated as record-less
    /// (unresolved), on BOTH the styling and activation sides. The
    /// window is the link query's IO — typically a few milliseconds.
    static func recordsBelongToNote(
        recordsPath: String?, notePath: String?
    ) -> Bool {
        recordsPath != nil && recordsPath == notePath
    }

    // MARK: - Dispatch

    /// Where one URL goes. Split from `route(_:)` because
    /// `OpenURLAction.Result` is not `Equatable` — the routing TABLE is this
    /// pure, assertable function; `route` merely executes it.
    enum Disposition: Equatable {
        case wiki(String, WikiTargetGrammar)
        case embed(String)
        case tag(String)
        case citation(String)
        /// Allowlisted external scheme — hand to the system.
        case external
        /// Everything else (`file:`, `javascript:`, unknown schemes, and any
        /// scheme-less URL the mapper chose not to rewrite) — dropped, never
        /// LaunchServices. The mapper strips the link affordance from these,
        /// so a discard here is defense in depth, not a reachable dead end.
        case discard
    }

    static func disposition(for url: URL) -> Disposition {
        guard let scheme = url.scheme?.lowercased() else { return .discard }
        switch scheme {
        case Self.wikiScheme:
            return .wiki(Self.decodedTarget(from: url), .wikilink)
        case Self.wikiMarkdownScheme:
            return .wiki(Self.decodedTarget(from: url), .markdownDestination)
        case Self.embedScheme: return .embed(Self.decodedTarget(from: url))
        case Self.tagScheme: return .tag(Self.decodedTarget(from: url))
        case Self.citeScheme: return .citation(Self.decodedTarget(from: url))
        case "http", "https", "mailto": return .external
        default: return .discard
        }
    }

    /// The `\.openURL` handler. Slate schemes dispatch to their closure and
    /// report `.handled`; allowlisted external schemes pass to the system;
    /// everything else is discarded (see the type doc's safety rationale).
    func route(_ url: URL) -> OpenURLAction.Result {
        switch Self.disposition(for: url) {
        case .wiki(let target, let grammar):
            openWikiLink(target, grammar)
            return .handled
        case .embed(let target):
            openEmbed(target)
            return .handled
        case .tag(let name):
            openTag(name)
            return .handled
        case .citation(let raw):
            expandCitation(raw)
            return .handled
        case .external:
            return .systemAction
        case .discard:
            return .discarded
        }
    }
}

// MARK: - Live wiring (what U3-2 mounts)

extension ReadingLinkRouter {

    /// The production router: every scheme lands on the existing `AppState`
    /// activation path for that affordance, so announcements, the
    /// `lastActivatedLinkOutcome` seam, ⌘-click open-in-new-tab
    /// (`openTargetFromCurrentEvent`, inside `openLink`'s `navigate`), and
    /// conflict/unresolved handling all behave exactly like the panels.
    @MainActor
    static func live(appState: AppState) -> ReadingLinkRouter {
        ReadingLinkRouter(
            openWikiLink: { [weak appState] target, grammar in
                guard let appState else { return }
                guard Self.recordsBelongToNote(
                    recordsPath: appState.currentOutgoingLinksPath,
                    notePath: appState.selectedFilePath)
                else {
                    // Mid-transition (stale records) = record-less: the
                    // same announce the missing-record arm below gives.
                    postAccessibilityAnnouncement(
                        "\(target) is unresolved. Cannot open.")
                    return
                }
                // Match the note's own outgoing-link record and reuse
                // `openLink` wholesale — it resolves, navigates via
                // `openFile(_:target:)` honoring `openTargetFromCurrentEvent`,
                // announces, and records the outcome seam. `candidateKeys`
                // carries the anchor-strip grammar for BOTH origins the wiki
                // scheme routes (wikilinks cut at `#`, `^` only as the
                // no-`#` legacy block ref; markdown keeps `^` in the path)
                // plus the pre-migration verbatim defense.
                if let link = Self.candidateKeys(for: target, grammar: grammar)
                    .lazy.compactMap({ key in
                        appState.currentOutgoingLinks.first {
                            !$0.isEmbed && $0.targetRaw == key
                                && Self.recordKindMatches(
                                    $0.kind, grammar: grammar)
                        }
                    }).first
                {
                    appState.openLink(link)
                } else {
                    // The live buffer can hold a link the saved-state link
                    // index hasn't seen (reading mode renders unsaved text).
                    // Same message shape as `openLink`'s unresolved branch.
                    postAccessibilityAnnouncement(
                        "\(target) is unresolved. Cannot open.")
                }
            },
            openEmbed: { [weak appState] target in
                guard let appState else { return }
                // Embed bodies are always wikilink grammar (`![[…]]`).
                if Self.recordsBelongToNote(
                    recordsPath: appState.currentOutgoingLinksPath,
                    notePath: appState.selectedFilePath),
                    let link = Self.candidateKeys(
                        for: target, grammar: .wikilink)
                    .lazy.compactMap({ key in
                        appState.currentOutgoingLinks.first {
                            $0.isEmbed && $0.targetRaw == key
                                && Self.recordKindMatches(
                                    $0.kind, grammar: .wikilink)
                        }
                    }).first, let path = link.targetPath
                {
                    // Same entry point the embed panel + preview popover use:
                    // navigates + announces "Opened embed source".
                    appState.openEmbedTarget(path)
                } else {
                    postAccessibilityAnnouncement(
                        "\(target) is unresolved. Cannot open.")
                }
            },
            openTag: { [weak appState] tag in
                guard let appState else { return }
                // Real tag scope (#508): `SearchScope::Tag` now filters the
                // `file_tags` dimension (inline `#tag`s + frontmatter `tags:`),
                // and an EMPTY query under that scope lists every file with the
                // tag. So activation opens the overlay scoped to the tag with a
                // blank query — the exact set the tag names, not the old
                // approximate "bare tag name through vault-wide FTS" (which also
                // matched the word outside tag position). `setSearchScope`
                // re-arms the search; the empty query is honored under `.tag`.
                appState.searchQuery = ""
                if !appState.isSearchOpen {
                    appState.toggleSearchOverlay()
                }
                appState.setSearchScope(.tag(name: tag))
            },
            expandCitation: { [weak appState] raw in
                guard let appState else { return }
                // Same activation as a CitationsPanel row: set
                // `expandedCitation`; MainSplitView's sheet presents the
                // CitationPopover (full Milestone L speech treatment).
                // `RenderedCitation.raw` is the stable lookup key.
                guard
                    let citation = appState.currentNoteCitations.first(
                        where: { $0.raw == raw })
                else {
                    postAccessibilityAnnouncement(
                        "Citation is not loaded yet.")
                    return
                }
                appState.expandedCitation = citation
            }
        )
    }
}
