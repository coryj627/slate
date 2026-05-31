// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// #379 PR 2 — the CONCURRENCY surface `NoteEditorRangedHighlightTests`'
/// `assertWindowedMatchesWholeDoc` cannot reach, because it `await`s each
/// pass to completion (so cancellation, the staleness drop, and the
/// `subtract` residual-carry-forward never actually fire). Here we
/// interleave edits WITH in-flight tasks: schedule, then mutate again
/// before the prior pass applies, then settle and assert no recolor is
/// lost vs a clean whole-document apply on the final buffer. (Authored by
/// the PR-2 implementation red-team to confirm the dropped-pass /
/// `subtract`-residual / swap-while-typing paths hold; folded into the
/// committed suite to guard them in CI.)
@MainActor
final class NoteEditorRangedHighlightConcurrencyTests: XCTestCase {

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

    /// After whatever interleaving the caller did, drive the coordinator to
    /// quiescence (await any in-flight task), snapshot the windowed map, then
    /// force a clean whole-doc apply and compare. Dumps the first divergence.
    private func assertSettledMatchesWholeDoc(
        _ coordinator: NoteEditorView.Coordinator,
        _ textView: NSTextView,
        file: StaticString = #filePath,
        line: UInt = #line
    ) async {
        // The real app schedules exactly ONE debounced pass per keystroke
        // (textDidChange), each cancelling the prior. So the honest "settled"
        // state is: drain whatever is in flight, then do ONE trailing
        // debounced pass (the last keystroke's), and await it. NO healing
        // loop — a single windowed pass that strands must be caught here, not
        // papered over by repeated re-applies.
        await coordinator.highlightTask?.value
        let lm = textView.layoutManager!
        coordinator.scheduleHighlight(debounced: true)
        await coordinator.highlightTask?.value

        let len = textView.textStorage!.length
        let wFg = foregroundMap(lm, len)
        let wUl = underlineMap(lm, len)
        coordinator.scheduleHighlight(debounced: false)
        await coordinator.highlightTask?.value
        let fullFg = foregroundMap(lm, len)
        let fullUl = underlineMap(lm, len)
        if wFg != fullFg || wUl != fullUl {
            let ns = textView.string as NSString
            for i in 0..<len where wFg[i] != fullFg[i] || wUl[i] != fullUl[i] {
                print(
                    "CONCURRENCY DIVERGE idx \(i) char=\(ns.substring(with: NSRange(location: i, length: 1)).debugDescription) "
                        + "windowed(fg=\(String(describing: wFg[i])),ul=\(String(describing: wUl[i]))) "
                        + "whole(fg=\(String(describing: fullFg[i])),ul=\(String(describing: fullUl[i]))) "
                        + "buf=\(textView.string.debugDescription)")
                break
            }
        }
        XCTAssertEqual(wFg, fullFg, "settled FG != whole-doc", file: file, line: line)
        XCTAssertEqual(wUl, fullUl, "settled UL != whole-doc", file: file, line: line)
    }

    /// Schedule a debounced pass, then mutate the buffer AGAIN before it
    /// applies (the pass is sleeping in its 40ms debounce). The first pass
    /// must drop (staleness guard) and the edit's dirt must survive. We do
    /// NOT await between the schedule and the second edit.
    func testEditDuringInFlightDebounceDropsStaleAndKeepsDirt() async {
        let (coordinator, textView, storage) = makeCoordinator(
            text: "head\n\nalpha `c` beta\n\ngamma [[L]] delta\n\nfoot\n")
        coordinator.scheduleHighlight(debounced: false)
        await coordinator.highlightTask?.value

        // Edit 1 → dirtyRange set. Schedule (begins 40ms sleep).
        let a = (storage.string as NSString).range(of: "alpha").location
        storage.replaceCharacters(in: NSRange(location: a, length: 0), with: "**x** ")
        coordinator.scheduleHighlight(debounced: true)

        // Edit 2 in a DIFFERENT block, immediately (task still sleeping).
        let g = (storage.string as NSString).range(of: "gamma").location
        storage.replaceCharacters(in: NSRange(location: g, length: 0), with: "![[E]] ")
        // No second schedule yet — simulate didProcessEditing growing dirty
        // before textDidChange reschedules. Then reschedule (cancels pass 1).
        coordinator.scheduleHighlight(debounced: true)

        await assertSettledMatchesWholeDoc(coordinator, textView)
    }

    /// The nastier variant: edit 2 returns the buffer to a DIFFERENT text but
    /// dirtyRange has grown. Then the pass for snapshot-1 must drop because
    /// string != snapshot. Here edit 2 does NOT reschedule at all — only the
    /// settle loop saves it. If the implementation depended on a trailing
    /// reschedule that never came, this would strand.
    func testGrowDirtyWithoutRescheduleThenSettle() async {
        let (coordinator, textView, storage) = makeCoordinator(
            text: "p0\n\nfoo bar baz\n\nqux quux\n\nend\n")
        coordinator.scheduleHighlight(debounced: false)
        await coordinator.highlightTask?.value

        let f = (storage.string as NSString).range(of: "foo").location
        storage.replaceCharacters(in: NSRange(location: f, length: 0), with: "`x` ")
        coordinator.scheduleHighlight(debounced: true)  // pass for snapshot S1

        // Grow dirty via a direct storage edit (fires didProcessEditing) but
        // do NOT reschedule — the in-flight S1 pass is now stale.
        let q = (storage.string as NSString).range(of: "qux").location
        storage.replaceCharacters(in: NSRange(location: q, length: 0), with: "![[Z]] ")

        await assertSettledMatchesWholeDoc(coordinator, textView)
    }

    /// Net-zero buffer but grown dirty: schedule pass for S1, then within the
    /// debounce delete+reinsert so the BUFFER returns to S1 (staleness guard
    /// passes!) but dirtyRange grew. The stale pass now APPLIES (string==S1)
    /// against the OLD window; subtract must leave the surplus dirt. Tests the
    /// exact race the C4 comment claims `subtract` handles.
    func testNetZeroBufferRestoreWithGrownDirtyApplies() async {
        let (coordinator, textView, storage) = makeCoordinator(
            text: "top\n\nleft `c` mid right\n\nbottom para\n")
        coordinator.scheduleHighlight(debounced: false)
        await coordinator.highlightTask?.value

        let r = (storage.string as NSString).range(of: "right").location
        storage.replaceCharacters(in: NSRange(location: r, length: 0), with: "QQ ")
        coordinator.scheduleHighlight(debounced: true)  // S1 = "...QQ right..."

        // Within the debounce: insert then delete the SAME text elsewhere so
        // buffer == S1 again, but dirtyRange unions the touched region.
        let b = (storage.string as NSString).range(of: "bottom").location
        storage.replaceCharacters(in: NSRange(location: b, length: 0), with: "ZZ")
        storage.replaceCharacters(in: NSRange(location: b, length: 2), with: "")
        // buffer now == S1, dirtyRange has grown to include the bottom block.

        await assertSettledMatchesWholeDoc(coordinator, textView)
    }

    /// Many rapid scheduled passes with edits between each, never awaiting —
    /// stress the cancel/coalesce so most passes are cancelled mid-flight.
    func testRapidEditsNeverAwaitingUntilEnd() async {
        let (coordinator, textView, storage) = makeCoordinator(
            text: "h\n\nL1 word one\n\nL2 word two\n\nL3 word three\n\nL4 word four\n\nf\n")
        coordinator.scheduleHighlight(debounced: false)
        await coordinator.highlightTask?.value

        let targets = ["L1", "L2", "L3", "L4", "L1", "L3"]
        let inserts = ["**a** ", "`b` ", "![[E]] ", "[[w]] ", "#t ", "X "]
        for (t, ins) in zip(targets, inserts) {
            let loc = (storage.string as NSString).range(of: t).location
            storage.replaceCharacters(in: NSRange(location: loc, length: 0), with: ins)
            coordinator.scheduleHighlight(debounced: true)  // cancels prior
            // deliberately do NOT await — let them pile up / cancel
        }
        await assertSettledMatchesWholeDoc(coordinator, textView)
    }

    /// updateNSView-style swap WHILE a debounced typing pass is in flight:
    /// the swap (suppressed) resets dirtyRange and schedules whole-doc, which
    /// must cancel the stale typing pass and not strand the old buffer's attrs
    /// onto the new one.
    func testSwapWhileTypingPassInFlight() async {
        let (coordinator, textView, storage) = makeCoordinator(text: "old `c` body\n\ntwo\n")
        coordinator.scheduleHighlight(debounced: false)
        await coordinator.highlightTask?.value

        // Begin a typing pass.
        storage.replaceCharacters(in: NSRange(location: 0, length: 0), with: "Z")
        coordinator.scheduleHighlight(debounced: true)  // sleeping

        // Now an external swap, exactly as updateNSView does it.
        coordinator.withSuppressedDirtyTracking {
            textView.string = "new **bold** doc\n\nsecond ![[E]] block\n\nthird [[L]] block\n"
        }
        coordinator.scheduleHighlight(debounced: false)  // cancels typing pass

        await assertSettledMatchesWholeDoc(coordinator, textView)
    }
}
