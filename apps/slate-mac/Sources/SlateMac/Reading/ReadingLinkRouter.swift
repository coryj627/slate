// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Activation routing for the reading view's inline links (U3-1, #465).
///
/// Core computes the inline runs (`reading_inline_segments_source`);
/// `ReadingInlineMapper` stamps each activatable run with a custom-scheme
/// URL, and `ReadingView` installs `route(_:)` as its `\.openURL` action
/// so activating any of those runs lands here. The router itself is
/// **closure-based** so `ReadingView` stays mountable without an
/// `AppState` (tests use recording fakes; U3-2 mounts it with
/// `.live(appState:)`).
///
/// Scheme table (targets percent-encoded by `encodedURL`):
///
///   slate-wiki://<target>    wikilink        → open the target note
///   slate-wikimd://<target>  markdown dest.  → same, markdown grammar
///   slate-embed://<key>      embed (`![[…]]`) → open the embed's source note
///   slate-tag://<name>       tag             → search overlay, prefiltered
///   slate-cite://<raw>       citation        → expand the citation popover
///
/// The two wiki schemes exist because the routed value must retain its
/// SOURCE grammar: `^` is an anchor marker in wikilink grammar but a
/// legal path character in a markdown destination, and one shared scheme
/// let `[[note^block]]` activate a sibling `[m](note^block)` record
/// (Codex round 2).
///
/// **Record matching lives in core (#967).** `candidateKeys`,
/// `recordKindMatches`, and `LinkRecordSets` were deleted: the ordered
/// per-grammar key list is `reading_match_link`, the ONE implementation
/// the render-time `resolved` classifier also uses, so styling and
/// activation can never disagree about the same run (#849). What stays
/// here is trigger ownership — schemes, the URL codec, note-ownership
/// gating, dispositions, and every navigation/announcement action.
///
/// Anything else: `http`/`https`/`mailto` pass through to the system (the
/// same allowlist `AppState.openLink` enforces — `file:`/`javascript:`/custom
/// schemes must NOT be handed to LaunchServices, where a typo'd markdown link
/// would hand control to whatever app registered the scheme). Non-allowlisted
/// URLs are discarded — and core already emits those labels as plain `Text`
/// runs, so nothing renders as activatable and then dead-clicks.
struct ReadingLinkRouter {

    static let wikiScheme = "slate-wiki"
    static let embedScheme = "slate-embed"
    static let tagScheme = "slate-tag"
    static let citeScheme = "slate-cite"
    /// Internal MARKDOWN destinations ride their own scheme so the routed
    /// value retains its source grammar (see the type doc).
    static let wikiMarkdownScheme = "slate-wikimd"

    /// Wikilink target (anchor form, e.g. `Note#Section`), decoded.
    var openWikiLink: (String, ReadingWikiGrammar) -> Void
    /// Embed cache key (`target#suffix`), decoded.
    var openEmbed: (String) -> Void
    /// Tag name WITHOUT the leading `#`.
    var openTag: (String) -> Void
    /// The citation's raw source text (e.g. `[@key, p. 23]`) — the stable
    /// key `RenderedCitation.raw` carries (it has no byte offset field).
    var expandCitation: (String) -> Void

    /// A router whose slate-scheme activations do nothing. Used by previews /
    /// fixtures.
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
        case wiki(String, ReadingWikiGrammar)
        case embed(String)
        case tag(String)
        case citation(String)
        /// Allowlisted external scheme — hand to the system.
        case external
        /// Everything else (`file:`, `javascript:`, unknown schemes, and any
        /// scheme-less URL) — dropped, never LaunchServices. Core emits those
        /// labels as plain text runs, so a discard here is defense in depth,
        /// not a reachable dead end.
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
                    postAccessibilityAnnouncement(.linkUnresolved(target: target))
                    return
                }
                // Core owns the ordered candidate-key match, including the
                // per-grammar anchor-cut rules and the cross-grammar
                // prohibition (#967). Reuse `openLink` wholesale — it
                // resolves, navigates via `openFile(_:target:)` honoring
                // `openTargetFromCurrentEvent`, announces, and records the
                // outcome seam.
                let records = appState.currentOutgoingLinks
                if let index = readingMatchLink(
                    target: target, grammar: grammar, embed: false,
                    records: records),
                    Int(index) < records.count
                {
                    appState.openLink(records[Int(index)])
                } else {
                    // The live buffer can hold a link the saved-state link
                    // index hasn't seen (reading mode renders unsaved text).
                    // Same message shape as `openLink`'s unresolved branch.
                    postAccessibilityAnnouncement(.linkUnresolved(target: target))
                }
            },
            openEmbed: { [weak appState] target in
                guard let appState else { return }
                let records = appState.currentOutgoingLinks
                // Embed bodies are always wikilink grammar (`![[…]]`).
                if Self.recordsBelongToNote(
                    recordsPath: appState.currentOutgoingLinksPath,
                    notePath: appState.selectedFilePath),
                    let index = readingMatchLink(
                        target: target, grammar: .wikilink, embed: true,
                        records: records),
                    Int(index) < records.count,
                    let path = records[Int(index)].targetPath
                {
                    // Same entry point the embed panel + preview popover use:
                    // navigates + announces "Opened embed source".
                    appState.openEmbedTarget(path)
                } else {
                    postAccessibilityAnnouncement(.linkUnresolved(target: target))
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
                // Set `expandedCitation` to open the CitationPopover (full
                // Milestone L speech treatment). Unlike a CitationsPanel row,
                // an inline Reading-mode glyph is an NSTextView attachment with
                // no SwiftUI anchor, so this stays NOT row-anchored (#878):
                // `MainSplitView`'s detached fallback presents it, gated on
                // `expandedCitationRowAnchored == false`.
                // `RenderedCitation.raw` is the stable lookup key.
                guard
                    let citation = appState.currentNoteCitations.first(
                        where: { $0.raw == raw })
                else {
                    postAccessibilityAnnouncement(.citationNotLoaded)
                    return
                }
                appState.expandedCitationRowAnchored = false
                appState.expandedCitation = citation
            }
        )
    }
}
