// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// #1421: VoiceOver's press on a reading-view link reaches the link router,
/// never LaunchServices.
///
/// In the app, VO-Space presses AppKit's text-link element, which opens the
/// run's URL with `NSWorkspace.open(_:configuration:completionHandler:)`;
/// `ReadingLinkSystemOpen` catches it there. The press itself cannot be sent
/// from XCTest: SwiftUI chooses its selectable-text implementation by the
/// SDK the main executable records, and the test runner (`xctest`, SDK 27)
/// gets SwiftUI's own text, whose link elements carry no press, while the
/// app's SwiftPM binary (SDK 15) gets the AppKit text the press goes
/// through. So these facts make the press's NSWorkspace call themselves,
/// with the URLs the reading leaves stamp, and the PR's VoiceOver field pass
/// proves the press reaches that call. The system-open seam stands in for
/// NSWorkspace's own implementation, so no fact opens the browser.
@MainActor
final class ReadingLinkPressTests: XCTestCase {

    private final class Recorder {
        var routed: [ReadingLinkRouter.Disposition] = []
        var system: [URL] = []
    }

    /// The field pass's three links: a paragraph's, a list item's, and an
    /// external one.
    private static let fixture = """
        # Link check

        A paragraph that links to [[List fixture]] outside any list.

        - An item that links to [[Item target]] from inside the list.
        - A second item.

        An external [example link](https://example.com/) in prose.
        """

    /// The URL every activatable run of `text` carries: what a leaf stamps
    /// as the run's link, and so what AppKit's press on it opens.
    private static func runURLs(_ text: String) -> [URL] {
        readingInlineSegmentsSource(source: text, citations: [], records: [])
            .flatMap(\.segments)
            .flatMap(\.runs)
            .compactMap { ReadingInlineMapper.activationURL(for: $0.kind) }
    }

    private func router(_ recorder: Recorder) -> ReadingLinkRouter {
        ReadingLinkRouter(
            openWikiLink: { recorder.routed.append(.wiki($0, $1)) },
            openEmbed: { recorder.routed.append(.embed($0)) },
            openTag: { recorder.routed.append(.tag($0)) },
            expandCitation: { recorder.routed.append(.citation($0)) })
    }

    /// NSWorkspace's entry point AppKit's text-link press calls.
    private func pressOpen(_ url: URL) {
        NSWorkspace.shared.open(url, configuration: NSWorkspace.OpenConfiguration()) { _, _ in }
    }

    override func tearDown() async throws {
        ReadingLinkSystemOpen.systemOpenForTesting = nil
        try await super.tearDown()
    }

    /// A mounted reading view receives the opens its links' presses make:
    /// the paragraph's and the list item's wikilinks reach its router with
    /// their targets and grammar, as clicks do, and never the system; the
    /// external link's goes on to the system, as before.
    func testAMountedReadingViewReceivesWhatItsLinksPressesOpen() async throws {
        let recorder = Recorder()
        ReadingLinkSystemOpen.systemOpenForTesting = { recorder.system.append($0) }
        let window = HostedAccessibilityTree.host(
            ReadingView(text: Self.fixture, pathLabel: "Link check.md", router: router(recorder)),
            size: NSSize(width: 640, height: 600))
        defer { window.close() }
        _ = try await HostedAccessibilityTree.snapshot(of: window) {
            $0.elements(role: "AXLink").count == 3
        }

        let urls = Self.runURLs(Self.fixture)
        XCTAssertEqual(
            urls.map(\.absoluteString),
            ["slate-wiki://List%20fixture", "slate-wiki://Item%20target", "https://example.com/"])
        urls.forEach(pressOpen)

        XCTAssertEqual(
            recorder.routed, [.wiki("List fixture", .wikilink), .wiki("Item target", .wikilink)])
        XCTAssertEqual(
            recorder.system, [URL(string: "https://example.com/")!],
            "only the external link reaches the system")
    }

    /// Every run kind's URL — wikilink, markdown destination, embed, tag,
    /// citation — reaches the router through the press's call, with the
    /// disposition a click gives it.
    func testEveryActivatableRunKindRoutesFromThePressAsFromAClick() async throws {
        let recorder = Recorder()
        ReadingLinkSystemOpen.systemOpenForTesting = { recorder.system.append($0) }
        let text = "See [[Note One]], [m](notes/two.md), ![[pic.png]], #alpha and [@smith2020].\n"
        let window = HostedAccessibilityTree.host(
            ReadingView(text: text, pathLabel: "Kinds.md", router: router(recorder)))
        defer { window.close() }
        _ = try await HostedAccessibilityTree.snapshot(of: window) {
            !$0.elements(role: "AXLink").isEmpty
        }

        let urls = Self.runURLs(text)
        XCTAssertEqual(urls.count, 5, "\(urls)")
        urls.forEach(pressOpen)
        XCTAssertEqual(recorder.routed, urls.map(ReadingLinkRouter.disposition(for:)))
        XCTAssertEqual(
            recorder.routed.map(\.kindName), ["wiki", "wiki", "embed", "tag", "citation"])
        XCTAssertEqual(recorder.system, [])
    }

    /// The guard itself, at both NSWorkspace entry points: every router
    /// scheme, in any case, is delivered to the mounted reading view and
    /// never to the system, and the completion handler hears "handled";
    /// http, https and mailto go on to the system untouched.
    func testSlateSchemesNeverReachTheSystemAndEveryOtherSchemeDoes() {
        final class Completions { var handled = 0 }
        var routed: [URL] = []
        var system: [URL] = []
        let completions = Completions()
        let registration = ReadingLinkSystemOpen.register { routed.append($0) }
        defer { ReadingLinkSystemOpen.unregister(registration) }
        ReadingLinkSystemOpen.systemOpenForTesting = { system.append($0) }

        let slate = [
            "slate-wiki://Note%20One", "slate-wikimd://note.md", "slate-embed://pic.png",
            "slate-tag://alpha", "slate-cite://%5B%40k%5D", "SLATE-WIKI://Upper",
        ].compactMap(URL.init(string:))
        XCTAssertEqual(slate.count, 6)
        for url in slate {
            XCTAssertTrue(NSWorkspace.shared.open(url), "\(url)")
            NSWorkspace.shared.open(url, configuration: NSWorkspace.OpenConfiguration()) {
                application, error in
                if application == nil, error == nil { completions.handled += 1 }
            }
        }
        XCTAssertEqual(routed, slate.flatMap { [$0, $0] })
        XCTAssertEqual(completions.handled, slate.count)
        XCTAssertEqual(system, [], "no slate URL reaches the system")

        let external = ["https://example.com/", "http://example.com/", "mailto:a@example.com"]
            .compactMap(URL.init(string:))
        for url in external {
            XCTAssertTrue(NSWorkspace.shared.open(url), "\(url)")
        }
        XCTAssertEqual(system, external, "every other scheme goes on to the system")
        XCTAssertEqual(routed.count, slate.count * 2, "the router sees no external URL")
    }

    /// The newest mounted reading view receives a caught open, and an
    /// unmounted one never does (a remount briefly overlaps two).
    func testTheNewestMountedReadingViewReceivesACaughtOpen() {
        var first: [URL] = []
        var second: [URL] = []
        let url = URL(string: "slate-wiki://Note")!
        let a = ReadingLinkSystemOpen.register { first.append($0) }
        let b = ReadingLinkSystemOpen.register { second.append($0) }
        _ = NSWorkspace.shared.open(url)
        XCTAssertEqual(first, [])
        XCTAssertEqual(second, [url], "the newest mounted view receives it")

        ReadingLinkSystemOpen.unregister(b)
        _ = NSWorkspace.shared.open(url)
        XCTAssertEqual(first, [url])
        XCTAssertEqual(second, [url], "an unmounted view hears nothing more")

        ReadingLinkSystemOpen.unregister(a)
        _ = NSWorkspace.shared.open(url)
        XCTAssertEqual(first, [url], "an unmounted view hears nothing more")
    }

    /// A caught open goes to the router the mounted view last rendered with,
    /// as a click's `\.openURL` action does, not the one it mounted with.
    func testACaughtOpenGoesToTheRouterTheViewLastRenderedWith() async throws {
        let mounted = Recorder()
        let rendered = Recorder()
        ReadingLinkSystemOpen.systemOpenForTesting = { rendered.system.append($0) }
        let window = HostedAccessibilityTree.host(
            ReadingView(text: Self.fixture, pathLabel: "Link check.md", router: router(mounted)))
        defer { window.close() }
        _ = try await HostedAccessibilityTree.snapshot(of: window) {
            $0.elements(role: "AXLink").count == 3
        }
        let host = try XCTUnwrap(window.contentView as? NSHostingView<ReadingView>)
        host.rootView = ReadingView(
            text: Self.fixture + "\n", pathLabel: "Link check.md", router: router(rendered))
        _ = try await HostedAccessibilityTree.snapshot(of: window) {
            $0.elements(role: "AXLink").count == 3
        }
        host.layoutSubtreeIfNeeded()

        pressOpen(URL(string: "slate-wiki://List%20fixture")!)
        XCTAssertEqual(rendered.routed, [.wiki("List fixture", .wikilink)])
        XCTAssertEqual(mounted.routed, [])
        XCTAssertEqual(rendered.system, [])
    }

    /// A reading view that leaves the window gives up its claim: a later
    /// open is not delivered to its router.
    func testAReadingViewThatUnmountsStopsReceivingOpens() async throws {
        let recorder = Recorder()
        ReadingLinkSystemOpen.systemOpenForTesting = { recorder.system.append($0) }
        let window = HostedAccessibilityTree.host(
            ReadingView(text: Self.fixture, pathLabel: "Link check.md", router: router(recorder)))
        defer { window.close() }
        _ = try await HostedAccessibilityTree.snapshot(of: window) {
            $0.elements(role: "AXLink").count == 3
        }
        let other = Recorder()
        let registration = ReadingLinkSystemOpen.register { _ in other.routed.append(.discard) }
        defer { ReadingLinkSystemOpen.unregister(registration) }

        window.contentView = NSView()
        _ = try await HostedAccessibilityTree.snapshot(of: window) { _ in true }
        try await Task.sleep(for: .milliseconds(50))
        pressOpen(URL(string: "slate-wiki://List%20fixture")!)

        XCTAssertEqual(recorder.routed, [], "the unmounted view's router hears nothing")
        XCTAssertEqual(other.routed, [.discard], "the open went to the view still mounted")
        XCTAssertEqual(recorder.system, [])
    }

    /// A click on an external link is unchanged: the router answers
    /// `.systemAction`, and SwiftUI's system action reaches NSWorkspace
    /// through the guard untouched, while a slate URL on the same action
    /// never does.
    func testAClicksSystemActionForAnExternalLinkStillReachesTheSystem() async throws {
        let recorder = Recorder()
        ReadingLinkSystemOpen.systemOpenForTesting = { recorder.system.append($0) }
        ReadingLinkSystemOpen.install()
        struct Probe: View {
            let urls: [URL]
            @Environment(\.openURL) private var openURL
            var body: some View {
                Text("probe").onAppear { urls.forEach { openURL($0) } }
            }
        }
        let external = URL(string: "https://example.com/")!
        let slate = URL(string: "slate-wiki://List%20fixture")!
        let route = router(recorder)
        let window = HostedAccessibilityTree.host(
            Probe(urls: [external, slate])
                .environment(\.openURL, OpenURLAction { route.route($0) }))
        defer { window.close() }
        _ = try await HostedAccessibilityTree.snapshot(of: window) { _ in
            !recorder.system.isEmpty && !recorder.routed.isEmpty
        }
        XCTAssertEqual(recorder.system, [external])
        XCTAssertEqual(recorder.routed, [.wiki("List fixture", .wikilink)])
    }
}

extension ReadingLinkRouter.Disposition {
    /// The case alone, for facts that compare kinds.
    fileprivate var kindName: String {
        switch self {
        case .wiki: return "wiki"
        case .embed: return "embed"
        case .tag: return "tag"
        case .citation: return "citation"
        case .external: return "external"
        case .discard: return "discard"
        }
    }
}
