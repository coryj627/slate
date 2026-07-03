// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// A right-pane leaf: one selectable panel in the trailing utility rail
/// (Milestone U4-1, #470).
///
/// The full ten-case vocabulary is declared now so the enum, its persistence
/// string, and the rail's iteration order are settled once. Only the three
/// leaves whose content has been ported (`.outline`, `.citations`,
/// `.bibliography` — today's detail-column tabs) are `registered`; the other
/// seven arrive in U4-2 when their panels move into the leaf host. The rail
/// renders ONLY registered leaves, so this PR shows three icons and the pane
/// is behavior-equivalent to the segmented picker it replaces, minus the
/// picker.
///
/// `rawValue` is the persistence token stored in `workspace.json`; an unknown
/// token decodes to `.outline` (see `Leaf.init(persisted:)`).
enum Leaf: String, CaseIterable, Identifiable, Codable {
    case outline
    case backlinks
    case outgoingLinks
    case embeds
    case math
    case code
    case diagrams
    case tasks
    case citations
    case bibliography

    var id: String { rawValue }

    /// Human-readable name — the rail item's accessibility label, help text,
    /// and the "\(title) panel." switch announcement.
    var title: String {
        switch self {
        case .outline: return "Outline"
        case .backlinks: return "Backlinks"
        case .outgoingLinks: return "Outgoing links"
        case .embeds: return "Embeds"
        case .math: return "Math"
        case .code: return "Code"
        case .diagrams: return "Diagrams"
        case .tasks: return "Tasks"
        case .citations: return "Citations"
        case .bibliography: return "Bibliography"
        }
    }

    /// The rail glyph, by semantic role. Only the three REGISTERED leaves are
    /// ever rendered in U4-1, and each maps to a role that exists today:
    /// `.outline` (new this PR), and `.citations`/`.bibliography` reusing the
    /// existing `.citationSummary`/`.bibliography` roles (u4_spec SlateSymbol
    /// table).
    ///
    /// The seven not-yet-registered leaves return a neutral placeholder
    /// (`.moreActions`) because their dedicated roles (`.backlinks`,
    /// `.outgoingLinks`, `.embed`, `.diagram`, `.tasksLeaf`) land in U4-2 with
    /// their content — the u4_spec deliberately defers those SlateSymbol rows.
    /// The rail never renders these leaves (it iterates `Leaf.registered`), so
    /// the placeholder is never shown; U4-2 replaces each arm with its real
    /// role as the leaf registers.
    var symbol: SlateSymbol {
        switch self {
        case .outline: return .outline
        case .citations: return .citationSummary
        case .bibliography: return .bibliography
        case .math: return .math
        case .code: return .code
        case .backlinks, .outgoingLinks, .embeds, .diagrams, .tasks:
            return .moreActions
        }
    }

    /// Leaves whose content is live in this PR, in rail order (outline first —
    /// most used, matches the old default tab). U4-2 grows this to all ten as
    /// each panel moves into the host; the rail iterates exactly this list, so
    /// an unregistered leaf can never present a selectable-but-blank icon.
    static let registered: [Leaf] = [.outline, .citations, .bibliography]

    var isRegistered: Bool { Self.registered.contains(self) }

    /// Decode a persisted token, tolerating anything unknown (a value written
    /// by a future build, or a leaf not yet registered) by falling back to the
    /// default leaf — persistence must never resurrect a blank pane.
    init(persisted raw: String?) {
        guard let raw, let leaf = Leaf(rawValue: raw), leaf.isRegistered else {
            self = .outline
            return
        }
        self = leaf
    }

    // MARK: Rail keyboard navigation (pure — unit-tested)

    /// Move the rail highlight one step along the registered leaves for an
    /// `up`/`down` arrow, clamped at the ends (no wrap — matches a segmented
    /// picker's arrow-within behavior). `left`/`right` don't move the vertical
    /// rail. Returns `nil` when there is no move (already at the edge, an
    /// unregistered origin, or a non-vertical command) so the caller leaves the
    /// highlight untouched.
    ///
    /// Pure and total over the registered order so the mapping is testable
    /// without a rendered view (`RightPaneViewTests`).
    static func railMove(
        from current: Leaf, _ direction: MoveCommandDirection,
        in order: [Leaf] = Leaf.registered
    ) -> Leaf? {
        guard let index = order.firstIndex(of: current) else { return nil }
        switch direction {
        case .up:
            return index > 0 ? order[index - 1] : nil
        case .down:
            return index < order.count - 1 ? order[index + 1] : nil
        default:
            return nil
        }
    }
}

/// The right pane (Milestone U4-1, #470): the active leaf's content filling the
/// column, a hairline separator, and the vertical icon rail pinned to the
/// trailing edge (Obsidian parity — the pane collapses leftward). Replaces the
/// segmented-picker `DetailSidebarColumn` wholesale; the three detail panels it
/// hosted (outline / citations / bibliography) mount here unchanged.
///
/// `workspace` is observed directly (not via AppState's publisher): `activeLeaf`
/// is a nested `@Published` on `WorkspaceState`, and AppState's own publisher
/// doesn't forward it — same reason `WorkspaceView` holds the tree's
/// `@ObservedObject`. The child panels still read `AppState` from the
/// environment.
struct RightPaneView: View {
    @ObservedObject var workspace: WorkspaceState

    /// The rail is a single Tab stop; ↑/↓ move this highlight inside it and
    /// Space/Return activates the highlighted leaf — matching the segmented
    /// picker's arrow-within, Tab-out interaction. Nil until the rail takes
    /// focus, at which point it seeds to the active leaf.
    @FocusState private var railHighlight: Leaf?

    var body: some View {
        HStack(spacing: 0) {
            activeLeafContent
            Divider()
            LeafRailView(
                activeLeaf: workspace.activeLeaf,
                railHighlight: $railHighlight,
                onActivate: activate
            )
            .frame(width: 40)
        }
        // The COLUMN needs a name for VoiceOver's pane navigation
        // (⌘⌥arrows between split-view columns) — without it, focus lands on
        // a mangled NSHostingView type name (the issue the old detail
        // column's "Sidebar" label existed to fix). "Panels" matches the
        // leaf vocabulary; the rail and each leaf carry their own labels.
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Panels")
    }

    /// The mounted-ZStack retention pattern, carried over VERBATIM (rationale
    /// and all) from the old `DetailSidebarColumn` — the leaf host's mechanism
    /// is unchanged from the detail column's.
    ///
    /// Retention: keep every registered leaf mounted via `ZStack`, with
    /// `.opacity` + `.allowsHitTesting` + `.accessibilityHidden` gating which
    /// one is visible. A `switch` over the active leaf would destroy and
    /// re-create panels per switch, silently regressing: `BibliographyPanel.
    /// segment` reset, re-fired `loadBibliographyEntries` IO, and
    /// `announcedFilePath` re-announcements. Hidden panels stay in the
    /// hierarchy purely for state retention; `accessibilityHidden(true)` +
    /// `allowsHitTesting(false)` keep VoiceOver and pointer focus scoped to the
    /// visible one. This is today's cost envelope — the three detail panels are
    /// all permanently mounted already.
    private var activeLeafContent: some View {
        ZStack {
            ForEach(Leaf.registered) { leaf in
                leafContent(leaf)
                    .opacity(workspace.activeLeaf == leaf ? 1 : 0)
                    .allowsHitTesting(workspace.activeLeaf == leaf)
                    .accessibilityHidden(workspace.activeLeaf != leaf)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    /// Maps a registered leaf to its panel view. U4-2 extends this switch as it
    /// ports the remaining seven panels (see the file-level seam note); an
    /// unregistered leaf is never rendered (the `ForEach` iterates
    /// `Leaf.registered`), so no default arm is needed.
    @ViewBuilder
    private func leafContent(_ leaf: Leaf) -> some View {
        switch leaf {
        case .outline:
            OutlineSidebar()
        case .citations:
            CitationsPanel()
        case .bibliography:
            BibliographyPanel()
        default:
            // Unreachable: `ForEach(Leaf.registered)` never yields an
            // unregistered leaf. A blank arm here would violate the DoD
            // empty-state rule if it ever rendered; U4-2 replaces it with the
            // real panels as they register.
            EmptyView()
        }
    }

    /// Select `leaf`, persist-on-change (through the workspace publisher), and
    /// post the switch announcement that replaces the segmented picker's native
    /// one. No-op when already active so re-activating doesn't re-announce.
    private func activate(_ leaf: Leaf) {
        guard workspace.activeLeaf != leaf else { return }
        workspace.activeLeaf = leaf
        postAccessibilityAnnouncement("\(leaf.title) panel.", priority: .medium)
    }
}

/// The vertical utility rail (U4-1, #470): one `Button` per registered leaf, a
/// large Dynamic-Type-scaling glyph (~28pt at the default size) in a 40×36
/// target, the active leaf marked with an `accentText` tint AND a 2pt leading
/// `accentFill` selection bar (shape + color, never color alone). The whole
/// rail is one Tab stop and one AX container labeled "Panel rail"; ↑/↓ move a
/// `@FocusState` highlight inside it, Space/Return activates.
private struct LeafRailView: View {
    let activeLeaf: Leaf
    var railHighlight: FocusState<Leaf?>.Binding
    let onActivate: (Leaf) -> Void

    var body: some View {
        VStack(spacing: 0) {
            ForEach(Leaf.registered) { leaf in
                railItem(leaf)
            }
            Spacer(minLength: 0)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        .background(Tokens.ColorRole.surface)
        // One focus stop for the whole rail: the container is focusable and
        // owns the highlight; the item Buttons are not individually
        // focus-reachable by Tab (arrow-within, Tab-out — the segmented picker
        // it replaces behaved the same). Seed the highlight to the active leaf
        // when the rail takes focus so ↑/↓ start from what's shown.
        .focusable()
        .focused(railHighlight, equals: activeLeaf)
        .onMoveCommand { direction in
            let origin = railHighlight.wrappedValue ?? activeLeaf
            if let next = Leaf.railMove(from: origin, direction) {
                railHighlight.wrappedValue = next
            }
        }
        // Space / Return activate the highlighted leaf (falling back to the
        // active leaf if focus just landed). `.defaultAction` is Return; the
        // Button's own key handling covers Space when highlighted.
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Panel rail")
        .accessibilityHint("Choose which panel is shown")
        .accessibilityAction(.default) {
            onActivate(railHighlight.wrappedValue ?? activeLeaf)
        }
    }

    /// A single rail button: labeled glyph, selection affordance, help, and the
    /// `.isSelected` trait when active. The 2pt leading bar is the shape half of
    /// the redundant selection cue (color-independent).
    private func railItem(_ leaf: Leaf) -> some View {
        Button {
            onActivate(leaf)
        } label: {
            leaf.symbol.image(label: leaf.title)
                // A large, Dynamic-Type-scaling glyph (WCAG 1.4.4): a semantic
                // style + `.imageScale(.large)` is the project's icon-sizing
                // idiom (TasksPanel, TasksReviewView), never a frozen
                // `.system(size:)`. `.title2` + `.large` renders ~28pt at the
                // default size — the u4_spec glyph size — while still scaling.
                .font(.title2)
                .imageScale(.large)
                .frame(width: 40, height: 36)
                .foregroundStyle(
                    activeLeaf == leaf
                        ? Tokens.ColorRole.accentText
                        : Tokens.ColorRole.textSecondary
                )
                .overlay(alignment: .leading) {
                    // Shape cue: a 2pt bar on the leading edge marks the active
                    // leaf independently of the tint (WCAG 1.4.1 — never color
                    // alone). Zero-width for inactive items keeps layout stable.
                    Rectangle()
                        .fill(Tokens.ColorRole.accentFill)
                        .frame(width: activeLeaf == leaf ? 2 : 0)
                }
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .help(help(for: leaf))
        // The active leaf carries `.isSelected` so VoiceOver announces
        // "Outline, selected" — the value half of the radio-group semantics.
        .accessibilityAddTraits(activeLeaf == leaf ? [.isSelected] : [])
    }

    /// Help/tooltip text: the leaf title plus a keyboard hint when one exists.
    /// No leaf has a dedicated shortcut in U4-1 (leaf focus routing + any
    /// bindings arrive in U4-4), so today this is just the title; the seam is
    /// here so U4-4 drops the hint in without touching call sites.
    private func help(for leaf: Leaf) -> String {
        leaf.title
    }
}
