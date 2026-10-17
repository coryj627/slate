// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

// Reading-view lists as VoiceOver lists (G20, #1048; plan 43 PR 16).
//
// Each list is one accessibility element with role list and subrole
// content list, its items are its child groups in document order, and a
// nested list sits inside the item that owns it: the shape WebKit gives
// `<ul>`/`<ol>`. VoiceOver speaks it from structure alone: "content list"
// on the list, "(2 of 3)" on each item, "In content list" and "Out of
// content list" across a nesting level. The app composes none of that
// speech.
//
// SwiftUI has no list role on macOS (its traits stop at header, button,
// link, image, toggle and the like, and `List` is a table of selectable
// rows), so the container is the one AppKit piece: a hosting view whose
// own element reports the list role. Its SwiftUI content is the reading
// view's own item rows, so every item keeps its real text leaf (selectable
// per leaf, links and all) and a task keeps its toggle as a child control.
// The PR 16 spike measured the alternatives with VoiceOver: an outline is
// a selectable table ("Row 1 of 5 ... selected"), and SwiftUI groups need
// host-composed labels that hide each item's text behind a second step.

/// One list the reading view renders as a single accessibility container
/// (G20, #1048): its items in document order, each item's nested lists
/// under it.
struct ReadingList: Equatable {
    var ordered: Bool
    var items: [ReadingListItem]

    /// Every block this list covers, nested lists included, in document
    /// order.
    var blockIndices: [Int] {
        items.flatMap { [$0.blockIndex] + $0.nested.flatMap(\.blockIndices) }
    }
}

/// One item of a `ReadingList`: the `.listItem` block it renders, and the
/// lists nested under it (usually none or one; two when the marker kind
/// changes inside it, the Windows builder's rule).
struct ReadingListItem: Equatable {
    var blockIndex: Int
    var nested: [ReadingList]
}

/// What the reading view's eager stack renders, in document order: a
/// single block, or a whole top-level list.
enum ReadingUnit: Equatable {
    case block(Int)
    case list(ReadingList)

    /// The unit's scroll identity: the index of its first block, so a
    /// park (#856) taken on a list restores to the list.
    var firstBlockIndex: Int {
        switch self {
        case .block(let index): return index
        case .list(let list): return list.items.first?.blockIndex ?? 0
        }
    }
}

/// Rebuilds list structure from core's flattened blocks. Core emits one
/// `.listItem` block per item with its nesting `depth` (`reading.rs`, the
/// linear-reading rationale); the hosts rebuild the tree. This is the
/// Windows depth-stack rule (`ReadingDocumentBuilder.AppendListItem`),
/// kept identical so both hosts expose the same lists:
///
/// - a level nests inside the last item of the level above it;
/// - a depth can open at most one level below the deepest open one (core
///   never emits a deeper jump; the clamp is defensive);
/// - an item closes every deeper level;
/// - a change between ordered and unordered at a level closes and
///   replaces that level only (two lists, two navigation stops);
/// - any other block closes every open list.
enum ReadingListStructure {

    static func units(for blocks: [ReadingBlock]) -> [ReadingUnit] {
        var units: [UnitBox] = []
        var stack: [(list: ListBox, lastItem: ItemBox?)] = []
        for (index, block) in blocks.enumerated() {
            guard case .listItem(let authoredDepth, let ordered, _) = block.kind else {
                stack.removeAll()
                units.append(.block(index))
                continue
            }
            let depth = min(Int(authoredDepth), stack.count)
            if stack.count > depth + 1 {
                stack.removeSubrange((depth + 1)...)
            }
            if stack.count == depth + 1, stack[depth].list.ordered != ordered {
                stack.remove(at: depth)
            }
            if stack.count == depth {
                let list = ListBox(ordered: ordered)
                if depth > 0, let parent = stack[depth - 1].lastItem {
                    parent.nested.append(list)
                } else {
                    units.append(.list(list))
                }
                stack.append((list, nil))
            }
            let item = ItemBox(blockIndex: index)
            stack[depth].list.items.append(item)
            stack[depth].lastItem = item
        }
        return units.map { unit in
            switch unit {
            case .block(let index): return .block(index)
            case .list(let list): return .list(list.value)
            }
        }
    }

    /// The scroll identity of the unit that renders `blockIndex`: the
    /// block's own index outside a list, the list's first block inside
    /// one. A park (#856) taken before lists were units, or on a block a
    /// live edit has since folded into a list, restores to its unit.
    static func scrollIdentity(forBlock blockIndex: Int, in units: [ReadingUnit]) -> Int {
        var low = 0
        var high = units.count - 1
        var found = units.first?.firstBlockIndex ?? blockIndex
        while low <= high {
            let mid = (low + high) / 2
            if units[mid].firstBlockIndex <= blockIndex {
                found = units[mid].firstBlockIndex
                low = mid + 1
            } else {
                high = mid - 1
            }
        }
        return found
    }

    // Reference boxes for the build only: an item gains nested lists after
    // it is appended, so the walk mutates through references and freezes
    // the result into values at the end.
    private enum UnitBox {
        case block(Int)
        case list(ListBox)
    }

    private final class ListBox {
        let ordered: Bool
        var items: [ItemBox] = []
        init(ordered: Bool) { self.ordered = ordered }
        var value: ReadingList {
            ReadingList(ordered: ordered, items: items.map(\.value))
        }
    }

    private final class ItemBox {
        let blockIndex: Int
        var nested: [ListBox] = []
        init(blockIndex: Int) { self.blockIndex = blockIndex }
        var value: ReadingListItem {
            ReadingListItem(blockIndex: blockIndex, nested: nested.map(\.value))
        }
    }
}

// MARK: - The list container

/// One list's accessibility element: a hosting view that reports the list
/// role, so the item groups its SwiftUI content publishes are the list's
/// children and their parent is the list (an AppKit view wrapped around a
/// hosting view would put an extra hosting group between them).
final class ReadingListHostingView: NSHostingView<ReadingListRows> {
    override func accessibilityRole() -> NSAccessibility.Role? { .list }
    override func accessibilitySubrole() -> NSAccessibility.Subrole? { .contentList }
}

/// A list's rows inside its hosting view, pinned to the width the list was
/// offered, so the hosting view's ideal size is the height the rows wrap
/// to.
///
/// **The environment needs no bridge.** The hosting view sits in the
/// reading view's SwiftUI graph through `ReadingListContainer`, and SwiftUI
/// carries that graph's environment into it. That covers everything the
/// rows read: the link router every link run calls (`openURL`), a
/// colour-scheme or type-size override above the reading view, and any
/// value a row reads later. It arrives on first render and on every change.
/// Measured while building PR 16: with nothing passed across, the hosted
/// rows saw all three values at two nesting levels, and a scheme change
/// made after the first render reached them.
/// `ReadingViewTests.testAListsRowsInheritTheReadingViewsEnvironment` pins
/// that. If a release stops carrying the environment into a hosted view,
/// that fact fails, and the values the rows read must be passed across here
/// explicitly. A row that starts to read a new value needs nothing more
/// than a line in that fact if the value matters.
struct ReadingListRows: View {
    let rows: AnyView
    let width: CGFloat?

    var body: some View {
        rows.frame(width: width, alignment: .leading)
    }
}

/// Places one list in the reading view's eager stack: the rows render in a
/// `ReadingListHostingView`, sized to the width SwiftUI offers.
struct ReadingListContainer: NSViewRepresentable {
    let rows: AnyView

    final class Coordinator {
        /// The width the rows are pinned to, the last real width offered.
        var width: CGFloat?
    }

    func makeCoordinator() -> Coordinator { Coordinator() }

    func makeNSView(context: Context) -> ReadingListHostingView {
        let view = ReadingListHostingView(rootView: ReadingListRows(rows: rows, width: nil))
        view.sizingOptions = [.intrinsicContentSize]
        return view
    }

    func updateNSView(_ view: ReadingListHostingView, context: Context) {
        view.rootView = ReadingListRows(rows: rows, width: context.coordinator.width)
    }

    /// The height the rows take at the offered width. A probe with no real
    /// width (an ideal-size or a zero-width minimum probe) is answered at
    /// the current pin rather than re-pinning: a zero pin would wrap every
    /// word onto its own line only to be thrown away.
    func sizeThatFits(
        _ proposal: ProposedViewSize, nsView view: ReadingListHostingView, context: Context
    ) -> CGSize? {
        if let width = proposal.width, width.isFinite, width > 1,
            width != context.coordinator.width
        {
            context.coordinator.width = width
            view.rootView = ReadingListRows(rows: rows, width: width)
        }
        let ideal = view.intrinsicContentSize
        return CGSize(width: context.coordinator.width ?? ideal.width, height: ideal.height)
    }
}
