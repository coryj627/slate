// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// #379 PR 2 — the ranged (windowed) highlight path made live.
///
/// The load-bearing arbiter is `assertWindowedMatchesWholeDoc`: drive a
/// **sequence** of windowed edits through the coordinator, then assert the
/// resulting temporary-attribute map over the WHOLE document is identical
/// to a single whole-document apply on the same final buffer. A sequence
/// (not a single edit) is essential — the stale-attribute stranding the
/// windowed apply guards against only manifests across ≥2 windowed passes
/// (red-team C1/C2/H4). Plus unit tests for the coalescing / subtract math.
@MainActor
final class NoteEditorRangedHighlightTests: XCTestCase {

    private func makeCoordinator(text: String) -> (
        NoteEditorView.Coordinator, NSTextView, NSTextStorage
    ) {
        let textView = NSTextView(frame: NSRect(x: 0, y: 0, width: 400, height: 200))
        textView.string = text
        let storage = textView.textStorage!
        let captured = text
        let binding = Binding<String>(get: { captured }, set: { _ in })
        let coordinator = NoteEditorView.Coordinator(text: binding, onSave: {}, previewEmbedAtCursor: nil)
        coordinator.attach(textView: textView)
        return (coordinator, textView, storage)
    }

    private func foregroundMap(_ lm: NSLayoutManager, _ length: Int) -> [NSColor?] {
        (0..<length).map {
            lm.temporaryAttribute(.foregroundColor, atCharacterIndex: $0, effectiveRange: nil) as? NSColor
        }
    }

    private func underlineMap(_ lm: NSLayoutManager, _ length: Int) -> [Int?] {
        (0..<length).map {
            lm.temporaryAttribute(.underlineStyle, atCharacterIndex: $0, effectiveRange: nil) as? Int
        }
    }

    /// Apply `editGroups` (each group = edits applied back-to-back, then ONE
    /// debounced windowed pass — so a multi-edit group exercises burst
    /// coalescing), then assert the whole-document temp-attribute map equals
    /// a fresh whole-document apply on the final buffer.
    private func assertWindowedMatchesWholeDoc(
        initial: String,
        editGroups: [[(NSRange, String)]],
        file: StaticString = #filePath,
        line: UInt = #line
    ) async {
        let (coordinator, textView, storage) = makeCoordinator(text: initial)
        let lm = textView.layoutManager!

        coordinator.scheduleHighlight(debounced: false)  // whole-doc seed
        await coordinator.highlightTask?.value

        for group in editGroups {
            for (range, replacement) in group {
                storage.replaceCharacters(in: range, with: replacement)
            }
            coordinator.scheduleHighlight(debounced: true)  // windowed
            await coordinator.highlightTask?.value
        }

        let len = storage.length
        let windowedFg = foregroundMap(lm, len)
        let windowedUl = underlineMap(lm, len)

        // Force a clean whole-doc apply on the identical final buffer.
        coordinator.scheduleHighlight(debounced: false)
        await coordinator.highlightTask?.value
        let wholeFg = foregroundMap(lm, len)
        let wholeUl = underlineMap(lm, len)

        XCTAssertEqual(
            windowedFg, wholeFg, "windowed foreground != whole-doc for \(storage.string.debugDescription)",
            file: file, line: line)
        XCTAssertEqual(
            windowedUl, wholeUl, "windowed underline != whole-doc for \(storage.string.debugDescription)",
            file: file, line: line)
    }

    // MARK: - The differential matrix

    func testScatteredProseEditsMatchWholeDoc() async {
        // Three paragraphs; edit them out of order with length changes so a
        // prior window's attrs shift-track — the core stranding scenario.
        let doc = "para one with **bold** here\n\nsecond [[Wiki]] para\n\nthird #tag para\n"
        await assertWindowedMatchesWholeDoc(
            initial: doc,
            editGroups: [
                [(NSRange(location: doc.utf16Offset(of: "third"), length: 0), "EXTRA ")],
                [(NSRange(location: doc.utf16Offset(of: "para one"), length: 0), "INSERTED **x** ")],
                [(NSRange(location: 0, length: 0), "# Heading added\n\n")],
            ])
    }

    func testRecolorByEditingMarkupMatchesWholeDoc() async {
        // Make + break inline markup in different blocks across passes.
        let doc = "alpha bold here\n\nbeta [[L]] gamma\n\ndelta code epsilon\n"
        await assertWindowedMatchesWholeDoc(
            initial: doc,
            editGroups: [
                // wrap "bold" in ** ** (creates a Strong span)
                [(NSRange(location: doc.utf16Offset(of: "bold"), length: 0), "**"),
                 (NSRange(location: doc.utf16Offset(of: "bold") + 6, length: 0), "**")],
                // wrap "code" in backticks (creates InlineCode) in a later block
                [(NSRange(location: doc.utf16Offset(of: "code") + 8 + 4, length: 0), "`")],
            ])
    }

    func testSetextSplitClearsStrandedHeadingColor() async {
        // Red-team C2: a setext heading; inserting a blank inside it orphans
        // the `=====` underline, whose old Heading colour must NOT strand
        // outside the new blank-bounded window.
        let doc = "Title line\ncontinued\n=====\n\nbody paragraph here\n"
        await assertWindowedMatchesWholeDoc(
            initial: doc,
            editGroups: [
                [(NSRange(location: doc.utf16Offset(of: "=====") - 1, length: 0), "\n")]
            ])
    }

    func testFenceMakeAndBreakFallsBackAndMatches() async {
        // Acceptance (b): typing/removing a fence delimiter must recolor
        // correctly (via the whole-doc fallback). Two passes: open then close.
        let doc = "intro paragraph\n\nfn body line\n\noutro paragraph\n"
        await assertWindowedMatchesWholeDoc(
            initial: doc,
            editGroups: [
                [(NSRange(location: doc.utf16Offset(of: "fn body"), length: 0), "```rust\n")],
                [(NSRange(location: doc.utf16Offset(of: "fn body") + 8 + "fn body line".utf16.count, length: 0), "\n```")],
            ])
    }

    func testFrontmatterEditFallsBackAndMatches() async {
        let doc = "---\ntitle: x\ntags: [a]\n---\n\nbody with [[L]] here\n\ntail\n"
        await assertWindowedMatchesWholeDoc(
            initial: doc,
            editGroups: [
                [(NSRange(location: doc.utf16Offset(of: "title"), length: 0), "draft: true\n")],
                [(NSRange(location: doc.utf16Offset(of: "tail"), length: 0), "#tag ")],
            ])
    }

    func testPasteLargeInsertMatches() async {
        let doc = "head para\n\nmiddle [[L]]\n\nfoot para\n"
        let big = String(repeating: "pasted **b** [[W]] #t line\n\n", count: 40)
        await assertWindowedMatchesWholeDoc(
            initial: doc,
            editGroups: [
                [(NSRange(location: doc.utf16Offset(of: "middle"), length: 0), big)]
            ])
    }

    func testNetZeroEditBurstDoesNotLoseRecolor() async {
        // Red-team C4: within one debounced burst, delete then retype an
        // identical wider region. dirtyRange grows past the second edit; the
        // surplus must still recolor.
        let doc = "x para a\n\ny [[Link]] para b\n\nz para c\n"
        let wikiStart = doc.utf16Offset(of: "[[Link]]")
        await assertWindowedMatchesWholeDoc(
            initial: doc,
            editGroups: [
                [
                    (NSRange(location: wikiStart, length: 8), ""),  // delete [[Link]]
                    (NSRange(location: wikiStart, length: 0), "[[Link]]"),  // retype identical
                ]
            ])
    }

    func testMultibyteAndCRLFEditsMatch() async {
        let doc = "café para\n\n中文 [[Lïnk]] 😀\n\nплан #tag\n"
        await assertWindowedMatchesWholeDoc(
            initial: doc,
            editGroups: [
                [(NSRange(location: doc.utf16Offset(of: "中文"), length: 0), "X")],
                [(NSRange(location: doc.utf16Offset(of: "план"), length: 0), "**b** ")],
            ])
    }

    // MARK: - Coalescing / subtract math (unit)

    func testShiftAndUnionFromNilIsTheEdit() {
        let r = NoteEditorView.Coordinator.shiftAndUnion(
            nil, editedRange: NSRange(location: 5, length: 3), delta: 3)
        XCTAssertEqual(r, NSRange(location: 5, length: 3))
    }

    func testShiftAndUnionInsertionShiftsPriorAfterEdit() {
        // prior dirty [100,150); insert 10 at 50 → prior shifts to [110,160);
        // union with edited [50,60) → [50,160).
        let r = NoteEditorView.Coordinator.shiftAndUnion(
            NSRange(location: 100, length: 50), editedRange: NSRange(location: 50, length: 10), delta: 10)
        XCTAssertEqual(r, NSRange(location: 50, length: 110))
    }

    func testShiftAndUnionLargeDeleteStaysNonInverted() {
        // Red-team H1: prior [100,150); delete 80 at 90 (edited [90,0), delta
        // -80). Shifted bounds could invert; length must clamp ≥ 0.
        let r = NoteEditorView.Coordinator.shiftAndUnion(
            NSRange(location: 100, length: 50), editedRange: NSRange(location: 90, length: 0), delta: -80)
        XCTAssertGreaterThanOrEqual(r.length, 0)
        XCTAssertLessThan(r.length, 1_000_000)  // not a wrapped negative
        XCTAssertEqual(r.location, 90)
    }

    func testSubtractFullyCoveredIsNil() {
        XCTAssertNil(
            NoteEditorView.Coordinator.subtract(
                applied: NSRange(location: 0, length: 100), from: NSRange(location: 20, length: 30)))
    }

    func testSubtractLeavesRightResidual() {
        // dirty [20,80); applied [0,50) → residual [50,80).
        let r = NoteEditorView.Coordinator.subtract(
            applied: NSRange(location: 0, length: 50), from: NSRange(location: 20, length: 60))
        XCTAssertEqual(r, NSRange(location: 50, length: 30))
    }

    func testSubtractTwoSidedKeepsWhole() {
        // applied strictly inside dirty → keep whole dirty (conservative).
        let r = NoteEditorView.Coordinator.subtract(
            applied: NSRange(location: 40, length: 10), from: NSRange(location: 20, length: 60))
        XCTAssertEqual(r, NSRange(location: 20, length: 60))
    }
}

extension String {
    /// UTF-16 offset of the first occurrence of `needle` — test convenience
    /// for building NSRanges against a fixture.
    fileprivate func utf16Offset(of needle: String) -> Int {
        guard let r = range(of: needle) else { return 0 }
        return utf16.distance(from: utf16.startIndex, to: r.lowerBound.samePosition(in: utf16)!)
    }
}
