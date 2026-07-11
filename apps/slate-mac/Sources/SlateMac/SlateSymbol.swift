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
    case refresh
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
    case moveUp
    case moveDown
    case bulkRename
    case taskComplete
    case taskIncomplete

    // Forward-looking roles named in #450, consumed by U1–U5. Defined now so
    // the icon vocabulary is settled; not yet rendered by any call site.
    case newTab
    case closeTab
    /// Canvas tab glyph (Milestone T, #369) — marks `.canvas` tabs in
    /// the tab strip so the file kind is visible at a glance (the tab's
    /// AX value carries "canvas" for VoiceOver).
    case canvas
    /// Base tab glyph (Milestone N, #702) — marks `.base` tabs in the
    /// tab strip; the tab's AX value carries "base" for VoiceOver.
    case base
    /// Pinned saved-query glyph (Milestone N, #709).
    case pin
    case splitRight
    case readingMode
    case editingMode
    case folder
    case folderOpen
    /// Outline leaf in the right-pane rail (U4-1, #470). The other nine leaf
    /// roles either already exist (`.citationSummary`/`.bibliography`/`.math`/
    /// `.code`, reused by the rail) or land here in U4-2 (below) with their
    /// leaves' content — the rail only renders leaves whose content is
    /// registered.
    case outline
    /// Row disclosure triangle for the file tree (U2-4, #462). Rendered
    /// `decorative` and rotated by the row per expand state; the folder row's
    /// AX value states expanded/collapsed, so the glyph itself is unlabeled.
    case disclosure
    // Right-pane leaf roles landed with their content in U4-2 (#471). The
    // remaining four leaf roles (`.math`/`.code`/`.citationSummary`/
    // `.bibliography`) already exist above and are reused by the rail.
    /// Backlinks leaf — inbound links to the active note.
    case backlinks
    /// Outgoing-links leaf — links FROM the active note.
    case outgoingLinks
    /// Embeds leaf — `![[…]]` transclusions in the active note.
    case embed
    /// Diagrams leaf — Mermaid diagram blocks in the active note.
    case diagram
    /// Tasks leaf. Deliberately shares `.tasksReview`'s `checklist` glyph
    /// (same metaphor, same glyph — the DoD §B consistency rule); the two
    /// roles stay distinct so their labels differ ("Tasks" vs "Tasks Review").
    case tasksLeaf
    /// Show-source YAML toggle in the properties widget header (U3-4,
    /// #468). `v7 == fallback` (curlybraces exists on the macOS 15 floor).
    case showSource
    /// Bottom-left utility rail roles (U4-3, #472). Each renders in the
    /// `SidebarUtilityBar` as a labeled glyph; `v7 == fallback` (all three
    /// glyphs exist on the macOS 15 floor). Per the u4_spec SlateSymbol table.
    case settings
    case help
    case vaultSwitch
    /// "Currently selected" leading checkmark (U4-3, #472): marks the open
    /// vault's row in the vault-switcher menu. A supporting role for the U4-3
    /// surface — the source-lint funnels every glyph through this layer, so the
    /// menu's selection cue lives here rather than as a raw `systemImage:`.
    case checkmark
    // File-management command roles (U2-5, #463). Each renders in the tree's
    // context menu + the command palette; `v7 == fallback` (all five glyphs
    // exist on the macOS 15 floor). Per the u2_spec SlateSymbol additions table.
    /// "New Folder" — creates a child directory in the selected folder / root.
    case newFolder
    /// "New Note" — creates an untitled `.md` in the selected folder / root.
    case newNote
    /// "Move to…" — opens the folder picker for a file/folder move.
    case moveTo
    /// "Rename" — renames the selected file or folder in place.
    case rename
    /// "Move to Trash" — sends the selected file/folder to the system trash.
    case trash

    // Milestone M sync diagnostics (M-3, #534) — per the m_spec SlateSymbol
    // additions table. `.warning` above is reused for the High-risk badge and
    // the multi-sync warning row (reuse, don't add a non-fill twin).
    /// Sync-diagnostics leaf in the right-pane rail.
    case syncDiagnostics
    /// Medium-risk badge glyph — shape+text, never color alone (m_spec §M-3).
    case riskMedium
    /// Low-risk badge glyph — shape+text, never color alone (m_spec §M-3).
    case riskLow

    // Milestone O local history (O-5, #543) — per the o_spec SlateSymbol
    // additions table. The three diff* roles cover add/remove/edit tinting
    // in operation-list diff rows (never color alone — icon shape + text).
    /// History leaf in the right-pane rail.
    case history
    /// "Restore…" — restores a version / recovers a deleted file.
    case restore
    /// "Compare" — diffs a version against the current content.
    case compare
    /// Added-content diff operation row.
    case diffAdded
    /// Removed-content diff operation row.
    case diffRemoved
    /// Edited-content diff operation row.
    case diffEdited

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
        case .refresh: return "Refresh"
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
        case .moveUp: return "Move up"
        case .moveDown: return "Move down"
        case .bulkRename: return "Rename property across vault"
        case .taskComplete: return "Completed"
        case .taskIncomplete: return "Not completed"
        case .newTab: return "New tab"
        case .canvas: return "Canvas"
        case .base: return "Base"
        case .pin: return "Pin"
        case .closeTab: return "Close tab"
        case .splitRight: return "Split right"
        case .readingMode: return "Reading mode"
        case .editingMode: return "Editing mode"
        case .folder: return "Folder"
        case .folderOpen: return "Open folder"
        case .disclosure: return "Disclosure"
        case .outline: return "Outline"
        case .backlinks: return "Backlinks"
        case .outgoingLinks: return "Outgoing links"
        case .embed: return "Embed"
        case .diagram: return "Diagram"
        case .tasksLeaf: return "Tasks"
        case .showSource: return "Show source"
        case .settings: return "Settings"
        case .help: return "Help"
        case .vaultSwitch: return "Switch vault"
        case .checkmark: return "Current"
        case .newFolder: return "New folder"
        case .newNote: return "New note"
        case .moveTo: return "Move to"
        case .rename: return "Rename"
        case .trash: return "Move to Trash"
        case .syncDiagnostics: return "Sync"
        case .riskMedium: return "Medium risk"
        case .riskLow: return "Low risk"
        case .history: return "History"
        case .restore: return "Restore"
        case .compare: return "Compare"
        case .diffAdded: return "Added"
        case .diffRemoved: return "Removed"
        case .diffEdited: return "Edited"
        }
    }

    /// `(v7, fallback)` glyph names. `v7` is used on macOS 26+, `fallback` on
    /// macOS 15–25. `fallback` must exist on the macOS 15 floor (tested).
    ///
    /// **U5-1 v7 audit (#474).** Every role below was audited against the SF
    /// Symbols 7 catalogue (macOS 26 = the `2025` release in the SF Symbols app's
    /// `name_availability.plist`; 645 new symbols). Outcome: no role has a
    /// materially better v7-only glyph, so `v7 == fallback` stays for all of
    /// them. The two roles that already diverge (`readingMode`, `editingMode`)
    /// were reconfirmed sensible and kept. The seam is the point: when SF
    /// Symbols later adds a better glyph for a role, its name drops into the `v7`
    /// slot as a one-liner and the floor-safe `fallback` keeps macOS 15–25
    /// correct — the audit just found nothing to change this cycle.
    var names: (v7: String, fallback: String) {
        switch self {
        case .save: return ("square.and.arrow.down", "square.and.arrow.down")
        case .search: return ("magnifyingglass", "magnifyingglass")
        case .refresh: return ("arrow.clockwise", "arrow.clockwise")
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
        case .moveUp: return ("arrow.up", "arrow.up")
        case .moveDown: return ("arrow.down", "arrow.down")
        case .bulkRename: return ("rectangle.2.swap", "rectangle.2.swap")
        case .taskComplete: return ("checkmark.square", "checkmark.square")
        case .taskIncomplete: return ("square", "square")
        // Forward-looking roles. The `v7` slot holds the preferred glyph and
        // `fallback` a conservative macOS-15-safe one; both are valid today so
        // nothing can break when these first render. When a genuinely v7-only
        // glyph is chosen for a role, it drops into the `v7` slot the same way
        // and the `fallback` keeps macOS 15–25 correct.
        case .newTab: return ("plus", "plus")
        case .canvas: return ("rectangle.3.group", "rectangle.3.group")
        case .base: return ("tablecells", "tablecells")
        case .pin: return ("pin", "pin")
        case .closeTab: return ("xmark", "xmark")
        case .splitRight: return ("rectangle.split.2x1", "rectangle.split.2x1")
        case .readingMode: return ("text.book.closed", "book")
        case .editingMode: return ("square.and.pencil", "pencil")
        case .folder: return ("folder", "folder")
        case .folderOpen: return ("folder.fill", "folder.fill")
        case .disclosure: return ("chevron.right", "chevron.right")
        case .outline: return ("list.bullet.indent", "list.bullet.indent")
        // U4-2 leaf roles (u4_spec SlateSymbol table). Each glyph exists on the
        // macOS 15 floor, so v7 == fallback until a v7-only glyph is preferred.
        case .backlinks: return ("arrow.uturn.backward", "arrow.uturn.backward")
        case .outgoingLinks: return ("arrow.up.right", "arrow.up.right")
        case .embed: return ("photo.on.rectangle", "photo.on.rectangle")
        case .diagram:
            return ("point.3.connected.trianglepath.dotted",
                    "point.3.connected.trianglepath.dotted")
        // Shares `.tasksReview`'s glyph deliberately (DoD §B).
        case .tasksLeaf: return ("checklist", "checklist")
        case .showSource: return ("curlybraces", "curlybraces")
        case .settings: return ("gearshape", "gearshape")
        case .help: return ("questionmark.circle", "questionmark.circle")
        case .vaultSwitch: return ("externaldrive", "externaldrive")
        case .checkmark: return ("checkmark", "checkmark")
        // U2-5 file-management roles (u2_spec SlateSymbol additions table). Each
        // glyph exists on the macOS 15 floor, so v7 == fallback.
        case .newFolder: return ("folder.badge.plus", "folder.badge.plus")
        case .newNote: return ("square.and.pencil", "square.and.pencil")
        case .moveTo: return ("arrow.turn.down.right", "arrow.turn.down.right")
        case .rename: return ("pencil", "pencil")
        case .trash: return ("trash", "trash")
        // Milestone M (M-3, #534): the leaf glyph is the one genuinely
        // v7-divergent role today (the trianglehead arrows are SF Symbols 7);
        // arrow.triangle.2.circlepath is SF Symbols 1-era, floor-safe. The two
        // badge glyphs predate SF Symbols 7, so v7 == fallback.
        case .syncDiagnostics:
            return ("arrow.trianglehead.2.clockwise.rotate.90",
                    "arrow.triangle.2.circlepath")
        case .riskMedium: return ("exclamationmark.circle", "exclamationmark.circle")
        case .riskLow: return ("info.circle", "info.circle")

        // Milestone O (O-5, #543): the leaf glyph is v7-divergent (the
        // trianglehead counterclockwise clock arrow is SF Symbols 7);
        // clock.arrow.circlepath is floor-safe. The other five predate
        // SF Symbols 7, so v7 == fallback (o_spec SlateSymbol table).
        case .history:
            return ("clock.arrow.trianglehead.counterclockwise.rotate.90",
                    "clock.arrow.circlepath")
        case .restore:
            return ("arrow.uturn.backward.circle", "arrow.uturn.backward.circle")
        case .compare: return ("arrow.left.arrow.right", "arrow.left.arrow.right")
        case .diffAdded: return ("plus.circle", "plus.circle")
        case .diffRemoved: return ("minus.circle", "minus.circle")
        case .diffEdited: return ("pencil.circle", "pencil.circle")
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

// MARK: - Per-surface rendering mode (U5-1, #474; DoD §B consistency)

extension SlateSymbol {
    /// The chrome surfaces that host `SlateSymbol` glyphs, each pinned to ONE
    /// symbol-rendering mode so a glyph's fill treatment is a per-surface
    /// *decision*, not whatever happened to inherit (DoD §B "rendering-mode
    /// consistency per surface"). Applied once at the container level via
    /// `.slateSymbolSurface(_:)`, which sets the SwiftUI environment so every
    /// descendant `Image(systemName:)` — i.e. every `label`/`image`/`decorative`
    /// glyph in that surface — renders the same way.
    ///
    /// The mode assignment is the u5_spec table verbatim:
    ///   - `toolbar`   → `.monochrome` (flat, single-weight command glyphs)
    ///   - `tabStrip`  → `.monochrome` (matches the toolbar; the strip reads as
    ///                    one continuous command band)
    ///   - `rail`      → `.hierarchical` (the leaf/utility rail's larger glyphs
    ///                    gain depth from a single accent — the Obsidian-rail feel)
    ///   - `tree`      → `.hierarchical` (folder glyphs get the same subtle depth;
    ///                    open/closed folders read as a family)
    enum Surface {
        case toolbar
        case tabStrip
        case rail
        case tree

        /// The one rendering mode this surface pins its glyphs to.
        var renderingMode: SymbolRenderingMode {
            switch self {
            case .toolbar, .tabStrip: return .monochrome
            case .rail, .tree: return .hierarchical
            }
        }
    }
}

extension View {
    /// Pin every `SlateSymbol` glyph inside this container to its surface's
    /// rendering mode (U5-1). `.symbolRenderingMode` is an environment modifier,
    /// so applying it once at the container level reaches all descendant
    /// `Image(systemName:)` glyphs — the "applied at the container level" the
    /// u5_spec calls for. No per-glyph styling at the call sites.
    func slateSymbolSurface(_ surface: SlateSymbol.Surface) -> some View {
        symbolRenderingMode(surface.renderingMode)
    }
}

// MARK: - macOS 26 (Tahoe) control material (U5-1, #474)

extension View {
    /// Adopt the macOS 26 Liquid Glass control material for a custom chrome
    /// container (tab strip, leaf rail, utility bar), falling back BELOW 26 to
    /// the exact solid token background the surface shipped with — so the
    /// macOS 15–25 appearance is byte-for-byte unchanged (u5_spec: "the 15–25
    /// path pinned by existing snapshots"; "No conditional layout differences").
    ///
    /// Both branches are backgrounds, not layout, so switching between them
    /// never moves a pixel of the surface's own geometry — the identity the
    /// snapshot smoke tests rely on. `glassEffect` is a no-op on the layout
    /// pass; the fallback `.background` matches today's call.
    ///
    /// Native `.toolbar { }` is deliberately NOT routed through here: on
    /// macOS 26 SwiftUI already renders the window toolbar in Liquid Glass, and
    /// below 26 it already uses the system toolbar material — there is no custom
    /// background to swap, and forcing one would fight the system chrome.
    @ViewBuilder
    func slateChromeMaterial(fallback: Color) -> some View {
        if #available(macOS 26, *) {
            glassEffect(.regular, in: .rect)
        } else {
            background(fallback)
        }
    }
}
