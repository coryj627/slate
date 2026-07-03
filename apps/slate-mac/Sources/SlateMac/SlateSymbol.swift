// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// The single source of truth for SF Symbols in Slate (Milestone U0-1, #450).
///
/// Every glyph is chosen once here, by *semantic role*, so call sites never
/// name a raw symbol string. Two things the layer guarantees:
///
///  1. **Graceful degradation below macOS 26.** Each role resolves through
///     `names` = `(v7, fallback)`. On macOS 26+ (SF Symbols 7) we return the
///     `v7` glyph; on the macOS 15–25 floor we return the `fallback`. The
///     `fallback` MUST be a symbol that exists on macOS 15 — enforced by
///     `SlateSymbolTests.testEveryFallbackIsFloorSafe` (an OS-independent
///     check against a curated macOS-15-safe fixture) plus
///     `testEveryFallbackLoadsOnCurrentOS` (loads each via
///     `NSImage(systemSymbolName:)`). Today most roles
///     use one glyph on both paths; the split exists so adopting a v7-only
///     glyph later is a one-line change that can't regress older macOS.
///
///  2. **An icon can't ship unlabeled.** The builders are the only supported
///     way to render a role: `label(_:)` pairs the glyph with text (the text
///     is the accessible name); `image(label:)` attaches a VoiceOver label
///     that *defaults to the role's `title`*; `decorative` is the single,
///     explicitly-named escape hatch that hides the glyph from VoiceOver
///     (used only when an adjacent element already names the control).
///
/// Sizing/weight/rendering-mode stay consistent because they inherit from the
/// enclosing control's font and the app's `.symbolRenderingMode` defaults;
/// call sites pass no per-glyph styling.
enum SlateSymbol: CaseIterable {
    // In-use roles (rendered today).
    case save
    case search
    case newFromTemplate
    case tasksReview
    case citationSummary
    case bibliography
    case math
    case code
    case warning
    case expandInline
    case moreActions
    case clearSearch
    case addProperty
    case bulkRename
    case taskComplete
    case taskIncomplete

    // Forward-looking roles named in #450, consumed by U1–U5. Defined now so
    // the icon vocabulary is settled; not yet rendered by any call site.
    case newTab
    case closeTab
    case splitRight
    case readingMode
    case editingMode
    case folder
    case folderOpen
    /// Row disclosure triangle for the file tree (U2-4, #462). Rendered
    /// `decorative` and rotated by the row per expand state; the folder row's
    /// AX value states expanded/collapsed, so the glyph itself is unlabeled.
    case disclosure

    /// The resolved SF Symbol name for the running OS. `private` so call
    /// sites can't reach past the labeled/decorative builders to name a raw
    /// symbol (only the builders below, in this file, use it).
    private var systemName: String {
        Self.symbolName(for: self, macOS26: Self.isMacOS26Available)
    }

    /// Concise VoiceOver label for the role: capitalized, no trailing period,
    /// and no role words ("icon"/"button") — VoiceOver announces the trait.
    /// Used as the default label by `image(label:)` and as `label(_:)`'s text.
    var title: String {
        switch self {
        case .save: return "Save"
        case .search: return "Search"
        case .newFromTemplate: return "New from Template"
        case .tasksReview: return "Tasks Review"
        case .citationSummary: return "Citation Summary"
        case .bibliography: return "Bibliography"
        case .math: return "Math"
        case .code: return "Code"
        case .warning: return "Warning"
        case .expandInline: return "Expand"
        case .moreActions: return "More actions"
        case .clearSearch: return "Clear search"
        case .addProperty: return "Add property"
        case .bulkRename: return "Rename property across vault"
        case .taskComplete: return "Completed"
        case .taskIncomplete: return "Not completed"
        case .newTab: return "New tab"
        case .closeTab: return "Close tab"
        case .splitRight: return "Split right"
        case .readingMode: return "Reading mode"
        case .editingMode: return "Editing mode"
        case .folder: return "Folder"
        case .folderOpen: return "Open folder"
        case .disclosure: return "Disclosure"
        }
    }

    /// `(v7, fallback)` glyph names. `v7` is used on macOS 26+, `fallback` on
    /// macOS 15–25. `fallback` must exist on the macOS 15 floor (tested).
    var names: (v7: String, fallback: String) {
        switch self {
        case .save: return ("square.and.arrow.down", "square.and.arrow.down")
        case .search: return ("magnifyingglass", "magnifyingglass")
        case .newFromTemplate: return ("doc.badge.plus", "doc.badge.plus")
        case .tasksReview: return ("checklist", "checklist")
        case .citationSummary: return ("quote.bubble.fill", "quote.bubble.fill")
        case .bibliography: return ("books.vertical", "books.vertical")
        case .math: return ("function", "function")
        case .code:
            return ("chevron.left.forwardslash.chevron.right",
                    "chevron.left.forwardslash.chevron.right")
        case .warning: return ("exclamationmark.triangle.fill", "exclamationmark.triangle.fill")
        case .expandInline:
            return ("arrow.down.right.and.arrow.up.left",
                    "arrow.down.right.and.arrow.up.left")
        case .moreActions: return ("ellipsis", "ellipsis")
        case .clearSearch: return ("xmark.circle.fill", "xmark.circle.fill")
        case .addProperty: return ("plus.circle", "plus.circle")
        case .bulkRename: return ("rectangle.2.swap", "rectangle.2.swap")
        case .taskComplete: return ("checkmark.square", "checkmark.square")
        case .taskIncomplete: return ("square", "square")
        // Forward-looking roles. The `v7` slot holds the preferred glyph and
        // `fallback` a conservative macOS-15-safe one; both are valid today so
        // nothing can break when these first render. When a genuinely v7-only
        // glyph is chosen for a role, it drops into the `v7` slot the same way
        // and the `fallback` keeps macOS 15–25 correct.
        case .newTab: return ("plus", "plus")
        case .closeTab: return ("xmark", "xmark")
        case .splitRight: return ("rectangle.split.2x1", "rectangle.split.2x1")
        case .readingMode: return ("text.book.closed", "book")
        case .editingMode: return ("square.and.pencil", "pencil")
        case .folder: return ("folder", "folder")
        case .folderOpen: return ("folder.fill", "folder.fill")
        case .disclosure: return ("chevron.right", "chevron.right")
        }
    }

    /// True on macOS 26+ (SF Symbols 7). Isolated so the branch is trivial to
    /// reason about; the pure `symbolName(for:macOS26:)` is what tests drive.
    static var isMacOS26Available: Bool {
        if #available(macOS 26, *) { return true } else { return false }
    }

    /// Pure resolution — exercised on both paths by tests without needing to
    /// run on macOS 26.
    static func symbolName(for symbol: SlateSymbol, macOS26: Bool) -> String {
        let names = symbol.names
        return macOS26 ? names.v7 : names.fallback
    }
}

// MARK: - View builders

extension SlateSymbol {
    /// Glyph + text, for buttons and tab items where the visible text is the
    /// accessible name. Pass `title` to override the role default in context
    /// (e.g. the same `bibliography` glyph is "Bibliography" in Settings but
    /// "Jump to Bibliography" in the toolbar).
    func label(_ title: String? = nil) -> Label<Text, Image> {
        Label(title ?? self.title, systemImage: systemName)
    }

    /// A standalone glyph that itself conveys meaning — always labeled for
    /// VoiceOver. `label` defaults to the role's `title`, so a meaningful icon
    /// can't ship unlabeled.
    func image(label: String? = nil) -> some View {
        Image(systemName: systemName)
            .accessibilityLabel(label ?? title)
    }

    /// A purely decorative glyph, hidden from VoiceOver. The ONLY way to get
    /// an unlabeled glyph — use it only when an adjacent element already names
    /// the control (e.g. a search field's own label).
    var decorative: some View {
        Image(systemName: systemName)
            .accessibilityHidden(true)
    }
}
