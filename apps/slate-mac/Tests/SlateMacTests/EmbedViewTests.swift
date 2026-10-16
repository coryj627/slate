// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import ApplicationServices
import SwiftUI
import XCTest

@testable import SlateMac

/// Tests for `EmbedView`: the byte-range splicer that interleaves nested
/// embeds into a parent's body text, the embed-depth constant, and the card
/// titles (#1286), which are core's `resolved_embed_title`, read where
/// VoiceOver reads them: the hosted view's accessibility tree.
@MainActor
final class EmbedViewTests: XCTestCase {

    // XCTest does not enter NSApplication.run(), so `shared` alone leaves the
    // worker without a launched AppKit accessibility server (the
    // TrashProgressStripTests setup; a second launch in one worker is
    // harmless).
    private static let hostApplication: NSApplication = {
        let application = NSApplication.shared
        XCTAssertTrue(application.setActivationPolicy(.accessory))
        application.finishLaunching()
        return application
    }()

    // MARK: - splice

    func testSpliceWithNoNestedEmbedsReturnsOriginalText() {
        let text = "Just plain text with no embeds."
        let segments = splice(text: text, nested: [])
        XCTAssertEqual(segments.count, 1)
        guard case .text(let s) = segments[0] else {
            XCTFail("Expected .text, got \(segments[0])")
            return
        }
        XCTAssertEqual(s, text)
    }

    func testSpliceWithOneNestedEmbedPartitionsAroundIt() {
        // 0-based byte layout:
        // "alpha ![[beta]] gamma"
        //  0     6         16
        let text = "alpha ![[beta]] gamma"
        let nested = NestedEmbed(
            rawTarget: "beta",
            byteOffsetInParent: 6,
            byteEndInParent: 15,
            resolution: EmbedResolution.unresolved(
                reason: .targetNotFound(target: "beta")
            )
        )
        let segments = splice(text: text, nested: [nested])
        XCTAssertEqual(segments.count, 3, "expected text + embed + text, got \(segments)")
        guard case .text(let leading) = segments[0] else { XCTFail(); return }
        XCTAssertEqual(leading, "alpha ")
        guard case .embed(let ne) = segments[1] else { XCTFail(); return }
        XCTAssertEqual(ne.rawTarget, "beta")
        guard case .text(let trailing) = segments[2] else { XCTFail(); return }
        // `![[beta]]` is exactly `5 + "beta".utf8.count` = 9 bytes
        // (`![[` + target + `]]`); after the fix in audit #199 the
        // trailing text starts cleanly after the closing `]]`.
        XCTAssertEqual(trailing, " gamma")
    }

    func testSpliceWithMultipleNestedEmbedsKeepsThemInOffsetOrder() {
        // Offsets deliberately given out of order to confirm the
        // sort in splice() works.
        let text = "A ![[one]] B ![[two]] C"
        let a = NestedEmbed(
            rawTarget: "two",
            byteOffsetInParent: 13,
            byteEndInParent: 21,
            resolution: .unresolved(reason: .targetNotFound(target: "two"))
        )
        let b = NestedEmbed(
            rawTarget: "one",
            byteOffsetInParent: 2,
            byteEndInParent: 10,
            resolution: .unresolved(reason: .targetNotFound(target: "one"))
        )
        let segments = splice(text: text, nested: [a, b])
        // Filter to .embed segments and check order.
        let embedTargets: [String] = segments.compactMap {
            if case .embed(let ne) = $0 { return ne.rawTarget }
            return nil
        }
        XCTAssertEqual(embedTargets, ["one", "two"])
    }

    func testSpliceDropsEmbedsWithOutOfRangeOffsets() {
        // An offset past the text length is treated as invalid and
        // the embed is dropped silently — defensive against a
        // future backend bug that reports a stale offset.
        let text = "short"
        let bad = NestedEmbed(
            rawTarget: "x",
            byteOffsetInParent: 100,
            byteEndInParent: 101,
            resolution: .unresolved(reason: .targetNotFound(target: "x"))
        )
        let segments = splice(text: text, nested: [bad])
        XCTAssertEqual(segments.count, 1)
        guard case .text(let s) = segments[0] else { XCTFail(); return }
        XCTAssertEqual(s, text)
    }

    // MARK: - image titles

    /// #419 (WCAG 1.1.1): the author's alt text IS the AT
    /// description for image embeds; the filename is only a
    /// fallback (and empty/whitespace alt falls back too — audit
    /// #198 contract). The rule is core's (#1286): an image card is
    /// titled by `resolvedEmbedTitle(resolved: .image(...))`.
    func testImageEmbedTitleUsesAltTextWhenPresent() {
        XCTAssertEqual(
            resolvedEmbedTitle(resolved: .image(
                targetPath: "attachments/pie.svg",
                alt: "A simple line drawing of a pie"
            )),
            "Embedded image: A simple line drawing of a pie"
        )
    }

    func testImageEmbedTitleFallsBackToFilename() {
        XCTAssertEqual(
            resolvedEmbedTitle(resolved: .image(targetPath: "attachments/pie.svg", alt: nil)),
            "Embedded image: pie.svg"
        )
        XCTAssertEqual(
            resolvedEmbedTitle(resolved: .image(targetPath: "a/b/cover.png", alt: "   ")),
            "Embedded image: cover.png"
        )
        XCTAssertEqual(
            resolvedEmbedTitle(resolved: .image(targetPath: "x.png", alt: "")),
            "Embedded image: x.png"
        )
    }

    // MARK: - depth constant

    func testEmbedDepthLimitMatchesBackendConstant() {
        // The Swift-side guard is defense-in-depth against a
        // misbehaving backend. Pin the constant so any future bump
        // on the Rust side is mirrored here intentionally (and the
        // test forces the consideration).
        XCTAssertEqual(EmbedView.embedDepthLimit, 3)
    }

    // MARK: - card titles are core's (#1286)

    /// Every resolved embed card is titled by core (`resolved_embed_title`;
    /// locked §1.1, contract 38, #1286). Each `EmbedView` variant builds the
    /// `ResolvedEmbed` it resolved to, and the label its disclosure exposes,
    /// read from the hosted accessibility tree, is core's rendering of it, as
    /// is the picture's own label; the base card's title is core's `.base`.
    /// Expectations are core's at test time, with handwritten spot checks.
    /// The " \n " alt is where the deleted Swift rule disagreed with core: it
    /// trimmed spaces but not newlines, so that card was titled
    /// "Embedded image: \n" instead of falling back to the file name (#198).
    func testEveryResolvedEmbedLabelIsCoreRendered() async throws {
        let png = try Self.onePixelPNG()
        let rows: [(resolution: EmbedResolution, resolved: ResolvedEmbed)] = [
            (.fullNote(targetPath: "a/b.md", text: "Body.", nested: []),
             .note(targetPath: "a/b.md")),
            (.section(targetPath: "a/b.md", heading: "Plan", text: "Body.", nested: []),
             .section(targetPath: "a/b.md", heading: "Plan")),
            (.block(targetPath: "a/b.md", blockId: "step-1", text: "Body."),
             .block(targetPath: "a/b.md")),
            (.image(targetPath: "img/cover.png", bytes: png, mime: "image/png", alt: " Cover "),
             .image(targetPath: "img/cover.png", alt: " Cover ")),
            (.image(targetPath: "img/cover.png", bytes: png, mime: "image/png", alt: nil),
             .image(targetPath: "img/cover.png", alt: nil)),
            (.image(targetPath: "img/cover.png", bytes: png, mime: "image/png", alt: " \n "),
             .image(targetPath: "img/cover.png", alt: " \n ")),
        ]
        let exposed = try await hostedLabels(of: rows.map(\.resolution))
        let titles = rows.map { resolvedEmbedTitle(resolved: $0.resolved) }
        XCTAssertEqual(exposed.disclosures, titles)
        XCTAssertEqual(
            exposed.images, Array(titles[3...]),
            "each picture's own label is its card's title")
        XCTAssertEqual(
            Array(titles[...2]),
            ["Embedded note: a/b.md", "Embedded section: Plan from a/b.md",
             "Embedded block from a/b.md"])
        XCTAssertEqual(
            Array(titles[3...]),
            ["Embedded image: Cover", "Embedded image: cover.png", "Embedded image: cover.png"])

        // The base card: core's `.base` title, with the host's view locator
        // after it when the embed names a view.
        let base = resolvedEmbedTitle(resolved: .base(targetPath: "lists/Reading.base"))
        XCTAssertEqual(base, "Embedded base: Reading")
        XCTAssertEqual(
            BaseEmbedRequest.wikilinkTarget("lists/Reading.base")?.accessibilityLabel, base)
        XCTAssertEqual(
            BaseEmbedRequest.wikilinkTarget("lists/Reading.base#Shelf")?.accessibilityLabel,
            base + ", view Shelf")
    }

    /// #1286: a heading and an alt of 5 000 characters reach VoiceOver through
    /// core's 4 096-character display bound. The label each hosted disclosure
    /// exposes is core's bounded rendering, character for character, where the
    /// deleted Swift composition exposed all 5 000 (a 5 030-character section
    /// title).
    func testAnOverlongHeadingIsBoundedByCore() async throws {
        let heading = String(repeating: "h", count: 5000)
        let alt = String(repeating: "a", count: 5000)
        let exposed = try await hostedLabels(of: [
            .section(targetPath: "a/b.md", heading: heading, text: "Body.", nested: []),
            .image(
                targetPath: "img/cover.png", bytes: try Self.onePixelPNG(),
                mime: "image/png", alt: alt),
        ])
        let section = resolvedEmbedTitle(resolved: .section(targetPath: "a/b.md", heading: heading))
        let image = resolvedEmbedTitle(resolved: .image(targetPath: "img/cover.png", alt: alt))

        XCTAssertEqual(exposed.disclosures, [section, image])
        XCTAssertEqual(exposed.disclosures.map(\.count), [section.count, image.count])
        XCTAssertEqual(exposed.images, [image])
        // The bound is real: 4 096 characters of the authored text, then one
        // ellipsis; the path stays whole.
        XCTAssertEqual(
            section,
            "Embedded section: " + String(repeating: "h", count: 4096) + "\u{2026} from a/b.md")
        XCTAssertEqual(image, "Embedded image: " + String(repeating: "a", count: 4096) + "\u{2026}")
    }

    // MARK: - hosted accessibility

    private struct ExposedLabels {
        /// Each disclosure's label, in document order.
        var disclosures: [String] = []
        /// Each picture's label, in document order.
        var images: [String] = []
    }

    private struct HostedAccessibilityError: Error, CustomStringConvertible {
        let description: String
    }

    /// Host one top-level `EmbedView` per resolution in a real window and
    /// read the labels its accessibility tree exposes: one disclosure per
    /// card, and an image for each picture that decodes.
    private func hostedLabels(of resolutions: [EmbedResolution]) async throws -> ExposedLabels {
        _ = Self.hostApplication
        let appeared = expectation(description: "hosted embeds appeared")
        let host = NSHostingView(rootView: AnyView(
            VStack(alignment: .leading, spacing: 8) {
                ForEach(Array(resolutions.enumerated()), id: \.offset) { _, resolution in
                    EmbedView(resolution: resolution, jumpToSourceAction: nil)
                }
            }
            .frame(width: 560, alignment: .topLeading)
            .onAppear { appeared.fulfill() }))
        host.frame = NSRect(x: 0, y: 0, width: 600, height: 240 * CGFloat(resolutions.count))
        let window = NSWindow(
            contentRect: host.frame, styleMask: [.titled], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false
        window.contentView = host
        window.makeKeyAndOrderFront(nil)
        await fulfillment(of: [appeared], timeout: 30)
        host.layoutSubtreeIfNeeded()

        // A real client request materializes SwiftUI's lazy accessibility
        // tree; run the self-process request off the main actor so AppKit
        // can answer it (the TrashProgressStripTests pattern).
        let initialized = await Task.detached {
            let application = AXUIElementCreateApplication(ProcessInfo.processInfo.processIdentifier)
            var windows: CFTypeRef?
            return AXUIElementCopyAttributeValue(application, kAXWindowsAttribute as CFString, &windows)
        }.value
        guard initialized == .success else {
            window.close()
            throw HostedAccessibilityError(description:
                "self-process accessibility initialization failed: AXError \(initialized.rawValue)")
        }

        // SwiftUI publishes the tree on a later pass: wait for every card.
        let deadline = ContinuousClock.now.advanced(by: .seconds(30))
        var exposed = ExposedLabels()
        repeat {
            window.contentView?.layoutSubtreeIfNeeded()
            exposed = Self.exposedLabels(in: window)
            if exposed.disclosures.count >= resolutions.count { break }
            try await Task.sleep(for: .milliseconds(20))
        } while ContinuousClock.now < deadline
        window.close()
        guard exposed.disclosures.count == resolutions.count else {
            throw HostedAccessibilityError(description:
                "expected \(resolutions.count) disclosures, found \(exposed.disclosures.count): "
                + "\(exposed.disclosures.map { String($0.prefix(60)) })")
        }
        return exposed
    }

    /// A depth-first walk of the window's accessibility tree, children before
    /// a node's next sibling, so labels come back in document order.
    private static func exposedLabels(in window: NSWindow) -> ExposedLabels {
        var exposed = ExposedLabels()
        var visited = Set<ObjectIdentifier>()
        var stack: [Any] = [window]
        while let node = stack.popLast() {
            let element = node as AnyObject
            guard visited.count < 2000, visited.insert(ObjectIdentifier(element)).inserted
            else { continue }
            // AppKit supports these accessibility methods without formal
            // protocol conformance; the optional calls dispatch them.
            let role = element.accessibilityRole?()
            if role == .disclosureTriangle, let label = element.accessibilityLabel?() {
                exposed.disclosures.append(label)
            } else if role == .image, let label = element.accessibilityLabel?() {
                exposed.images.append(label)
            }
            var children = accessibilityChildren(of: element)
            if let view = element as? NSView {
                children += view.subviews.map { $0 as Any }
            } else if let window = element as? NSWindow, let content = window.contentView {
                children.append(content)
            }
            stack.append(contentsOf: children.reversed())
        }
        return exposed
    }

    /// The raw NSArray of `accessibilityChildren`, read through the Objective-C
    /// getter: the Swift-typed getter traps bridging a reparenting proxy.
    private static func accessibilityChildren(of element: AnyObject) -> [Any] {
        let selector = #selector(NSAccessibilityProtocol.accessibilityChildren)
        guard let object = element as? any NSObjectProtocol,
            object.responds(to: selector),
            let array = object.perform(selector)?.takeUnretainedValue() as? NSArray
        else { return [] }
        return (0..<array.count).map { array.object(at: $0) }
    }

    /// A decodable one-pixel PNG, so an image card renders its picture (and
    /// the picture's own label) rather than the decode-failure notice.
    private static func onePixelPNG() throws -> Data {
        let rep = try XCTUnwrap(NSBitmapImageRep(
            bitmapDataPlanes: nil, pixelsWide: 1, pixelsHigh: 1, bitsPerSample: 8,
            samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
            colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0))
        return try XCTUnwrap(rep.representation(using: .png, properties: [:]))
    }
}
