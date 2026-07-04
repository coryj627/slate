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

    /// Wikilink target (anchor form, e.g. `Note#Section`), decoded.
    var openWikiLink: (String) -> Void
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
        openWikiLink: { _ in },
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

    /// Strip a wikilink anchor suffix (`#heading` / `^block`) so the
    /// remainder matches `OutgoingLink.targetRaw`, which the links pipeline
    /// stores WITHOUT the anchor (split into `targetAnchor` —
    /// `links.rs::split_wikilink_body`).
    static func baseTarget(of target: String) -> String {
        if let cut = target.firstIndex(where: { $0 == "#" || $0 == "^" }) {
            return String(target[target.startIndex..<cut])
        }
        return target
    }

    // MARK: - Dispatch

    /// Where one URL goes. Split from `route(_:)` because
    /// `OpenURLAction.Result` is not `Equatable` — the routing TABLE is this
    /// pure, assertable function; `route` merely executes it.
    enum Disposition: Equatable {
        case wiki(String)
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
        case Self.wikiScheme: return .wiki(Self.decodedTarget(from: url))
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
        case .wiki(let target):
            openWikiLink(target)
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
            openWikiLink: { [weak appState] target in
                guard let appState else { return }
                // Match the note's own outgoing-link record and reuse
                // `openLink` wholesale — it resolves, navigates via
                // `openFile(_:target:)` honoring `openTargetFromCurrentEvent`,
                // announces, and records the outcome seam. Both wikilink and
                // markdown records are now anchor-STRIPPED (links.rs #509), so
                // the base form is the match. Keep the verbatim arm as defense
                // for any pre-migration rows still carrying a `#fragment` in
                // targetRaw mid-transition.
                let base = Self.baseTarget(of: target)
                if let link = appState.currentOutgoingLinks.first(where: {
                    !$0.isEmbed
                        && ($0.targetRaw == base || $0.targetRaw == target)
                }) {
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
                let base = Self.baseTarget(of: target)
                if let link = appState.currentOutgoingLinks.first(where: {
                    $0.isEmbed && $0.targetRaw == base
                }), let path = link.targetPath {
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
