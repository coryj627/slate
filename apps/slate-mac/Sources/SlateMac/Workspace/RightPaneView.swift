// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// A right-pane leaf: one selectable panel in the trailing utility rail
/// (Milestone U4-1, #470; all ten U-program leaves live as of U4-2, #471;
/// M-3 #534 adds `.syncDiagnostics`; N4-3 #709 adds `.queries`).
///
/// The full vocabulary is the rail's iteration + persistence order. All
/// registered leaves are the three former detail-column tabs
/// (`.outline`, `.citations`, `.bibliography`) that U4-1 seeded, the
/// seven panels U4-2 ported out of the retired left-sidebar stack
/// (`.backlinks`, `.outgoingLinks`, `.embeds`, `.math`, `.code`, `.diagrams`,
/// `.tasks`), N4-3's `.queries`, N4-4's `.basesDock`, plus M-3's
/// vault-level `.syncDiagnostics`.
/// The rail renders every registered leaf, so it shows all icons and replaces
/// both the old segmented picker AND the sidebar stack.
///
/// `rawValue` is the persistence token stored in `workspace.json`; an unknown
/// token decodes to `.outline` (see `Leaf.init(persisted:)`).
enum Leaf: String, CaseIterable, Identifiable, Codable {
    case outline
    case backlinks
    case outgoingLinks
    /// Local graph neighborhood of the active note, in/out (Milestone P,
    /// P1-1 #554) — registered beside its siblings `.backlinks`/
    /// `.outgoingLinks`.
    case connections
    case embeds
    case math
    case code
    case diagrams
    case tasks
    /// Vault-wide, filterable, paginated task browser (#879) — folded out of
    /// its former modal `.sheet` into the leaf system (sheets.md:35: a complex
    /// paginated browser blocks the workspace it reports on; a non-modal leaf
    /// reports beside it). Registered right AFTER the note-scoped `.tasks` leaf
    /// so the quick per-note panel and the vault-wide review coexist. Revealed
    /// by View ▸ Show Tasks Review (⌘R) via `AppState.openTasksReview()`.
    case tasksReview
    /// Per-note version history + deleted-file recovery (Milestone O-5,
    /// #543) — registered BEFORE `.citations` (usage-frequency order:
    /// content leaves, then history, then citations/bibliography).
    case history
    case citations
    case bibliography
    case queries
    case basesDock
    /// Vault-level sync diagnostics (Milestone M-3, #534) — vault-scoped like
    /// `.bibliography`.
    case syncDiagnostics

    var id: String { rawValue }

    /// Human-readable name — the rail item's accessibility label, help text,
    /// and the "\(title) panel." switch announcement.
    var title: String {
        switch self {
        case .outline: return "Outline"
        case .backlinks: return "Backlinks"
        case .outgoingLinks: return "Outgoing links"
        case .connections: return "Connections"
        case .embeds: return "Embeds"
        case .math: return "Math"
        case .code: return "Code"
        case .diagrams: return "Diagrams"
        case .tasks: return "Tasks"
        case .tasksReview: return "Tasks Review"
        case .history: return "History"
        case .citations: return "Citations"
        case .bibliography: return "Bibliography"
        case .queries: return "Queries"
        case .basesDock: return "Base dock"
        case .syncDiagnostics: return "Sync"
        }
    }

    /// The rail glyph, by semantic role. Every leaf maps to a dedicated role
    /// in the `SlateSymbol` vocabulary (u4_spec SlateSymbol table): `.outline`
    /// and the five U4-2 roles (`.backlinks`/`.outgoingLinks`/`.embed`/
    /// `.diagram`/`.tasksLeaf`), plus `.math`/`.code`/`.citationSummary`/
    /// `.bibliography` reused from U0. `.tasksLeaf` shares `.tasksReview`'s
    /// glyph on purpose (same metaphor — DoD §B).
    var symbol: SlateSymbol {
        switch self {
        case .outline: return .outline
        case .backlinks: return .backlinks
        case .outgoingLinks: return .outgoingLinks
        case .connections: return .connections
        case .embeds: return .embed
        case .math: return .math
        case .code: return .code
        case .diagrams: return .diagram
        case .tasks: return .tasksLeaf
        case .tasksReview: return .tasksReviewLeaf
        case .history: return .history
        case .citations: return .citationSummary
        case .bibliography: return .bibliography
        case .queries: return .base
        case .basesDock: return .base
        case .syncDiagnostics: return .syncDiagnostics
        }
    }

    /// Leaves whose content is live, in rail order (outline first — most used,
    /// matches the old default tab; the order is the registry declaration
    /// order per u4_spec §U4-2). Ten leaves as of U4-2 (#471): the seven
    /// former sidebar-stack panels ported into the leaf host alongside the
    /// three detail tabs U4-1 seeded. M-3 (#534) appends `.syncDiagnostics`
    /// LAST — vault-level diagnostics, least-frequently visited. The rail
    /// iterates exactly this list, so an unregistered leaf can never present
    /// a selectable-but-blank icon.
    /// #879 inserts `.tasksReview` right after `.tasks` — the vault-wide review
    /// sits beside the note-scoped panel so both task leaves are adjacent.
    static let registered: [Leaf] = [
        .outline, .backlinks, .outgoingLinks, .connections, .embeds, .math, .code,
        .diagrams, .tasks, .tasksReview, .history, .citations, .bibliography, .queries,
        .basesDock, .syncDiagnostics,
    ]

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
    /// `up`/`down` arrow, clamped at the ends (no wrap). This is a roving
    /// highlight, NOT a segmented control: a segmented control commits its
    /// value on each arrow, whereas the rail only MOVES the highlight here and
    /// waits for Return/Space to activate (#859). `left`/`right` don't move the
    /// vertical rail. Returns `nil` when there is no move (already at the edge,
    /// an unregistered origin, or a non-vertical command) so the caller leaves
    /// the highlight untouched.
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
/// trailing edge. Obsidian parity — the pane collapses leftward: `MainSplitView`
/// drives that collapse by forcing the detail column to zero width when
/// `AppState.isRightPaneVisible` is false (View ▸ Hide Right Pane / ⌥⌘I, #882),
/// since `NavigationSplitView`'s `columnVisibility` can't hide the detail
/// column. Replaces the segmented-picker `DetailSidebarColumn` wholesale; the
/// three detail panels it hosted (outline / citations / bibliography) mount
/// here unchanged.
///
/// `workspace` is observed directly (not via AppState's publisher): `activeLeaf`
/// is a nested `@Published` on `WorkspaceState`, and AppState's own publisher
/// doesn't forward it — same reason `WorkspaceView` holds the tree's
/// `@ObservedObject`. The child panels still read `AppState` from the
/// environment.
struct RightPaneView: View {
    @ObservedObject var workspace: WorkspaceState

    /// The rail is a single Tab stop; ↑/↓ move this roving highlight inside it
    /// (Tab-out to leave) and Space/Return activates the highlighted leaf —
    /// listbox/toolbar semantics, NOT a segmented control (which would commit
    /// on each arrow; #859). Nil until the rail takes focus, at which point it
    /// seeds to the active leaf, and `LeafRailView` paints a focus ring on the
    /// highlighted item so a sighted keyboard user sees their position before
    /// activating (WCAG 2.4.7).
    /// Codex red-team: the rail's keyboard focus is a plain Boolean
    /// `@FocusState` (`railFocused`), NOT a `@FocusState<Leaf?>`. A
    /// value-keyed focus binding (`.focused($x, equals: activeLeaf)`)
    /// registers the rail for ONE value; the moment ↑/↓ moves the
    /// highlight to a different leaf, SwiftUI finds no view registered
    /// for that value and DROPS focus — so Return/Space never reached
    /// the rail. The highlight POSITION is ordinary `@State`; the ring
    /// and activation read it while `railFocused` owns the focus.
    @State private var railHighlight: Leaf?
    @FocusState private var railFocused: Bool

    /// Keyboard focus for the leaf-content region (U4-4, #473). ⌘⌥→ off the
    /// rightmost editor group routes focus INTO the leaf; SwiftUI moves
    /// keyboard focus here (the leaf's focus anchor), giving the pane a
    /// first-responder home so a subsequent ⌘⌥← has a defined origin and the
    /// pane visibly holds focus.
    @FocusState private var leafFocused: Bool

    /// VoiceOver focus for the leaf's first element (U4-4, #473). Bound to the
    /// leaf focus anchor so entering the leaf lands the VO cursor on a labeled
    /// "<leaf> panel." element — the AX half of the routing the spec requires,
    /// and the uniform fallback for empty-state leaves that have no natural
    /// first element (the U4-2 report's flagged gap).
    @AccessibilityFocusState private var leafAXFocused: Bool

    var body: some View {
        HStack(spacing: 0) {
            activeLeafContent
            Divider()
            LeafRailView(
                activeLeaf: workspace.activeLeaf,
                railHighlight: $railHighlight,
                railFocused: $railFocused,
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
        // U4-4 (#473): honor the leaf-focus request AppState raises on ⌘⌥→ off
        // the rightmost group. `.onChange` is a post-update mutation point
        // (#448 — never publish inside the update transaction); it pulls both
        // keyboard and VoiceOver focus to the leaf anchor.
        .onChange(of: workspace.leafFocusRequest) {
            leafFocused = true
            leafAXFocused = true
        }
        // U4-4 review: mirror REAL right-pane focus into the region
        // bookkeeping — native Tab lands on the rail (its single focus
        // stop) or the leaf anchor without any routing command, and the
        // next ⌘⌥← must "return to editor" per spec. Rail and anchor are
        // both leaf-region entries; either losing focus only demotes the
        // region when the other doesn't hold it (the state machine in
        // WorkspaceState converges). Post-update (#448-safe).
        .onChange(of: leafFocused) { _, focused in
            workspace.noteLeafFocusChanged(focused || railFocused)
        }
        // Codex red-team: rail focus OWNERSHIP is `railFocused` (Bool),
        // not "a highlight exists" — the highlight is plain state that
        // can outlive focus. On gaining focus, seed the highlight to the
        // active leaf so ↑/↓ start from what's shown; on losing it, drop
        // the highlight so no stale ring paints.
        .onChange(of: railFocused) { _, focused in
            workspace.noteLeafFocusChanged(focused || leafFocused)
            railHighlight = focused ? workspace.activeLeaf : nil
        }
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
        VStack(spacing: 0) {
            leafFocusAnchor
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
    }

    /// The leaf-content region's focus entry point (U4-4, #473): a
    /// zero-footprint, labeled anchor the keyboard + VoiceOver focus land on
    /// when ⌘⌥→ enters the leaf. It adds NO visible chrome (each leaf already
    /// renders its own `.isHeader` count header — "Backlinks, N entries" etc.
    /// — so a second visible header would duplicate it); it exists purely to
    /// give the pane a first-responder home (so a subsequent ⌘⌥← has a defined
    /// origin) and a uniform AX entry element that works for EVERY leaf,
    /// including empty-state leaves with no natural focusable first element
    /// (the U4-2 report's flagged fallback). Its `.accessibilityLabel` orients
    /// VoiceOver ("<leaf> panel"); the user then arrows down into the leaf's
    /// real header + rows. Invisible (`Color.clear`, zero height) so layout is
    /// untouched.
    private var leafFocusAnchor: some View {
        Color.clear
            .frame(height: 0)
            .focusable()
            .focused($leafFocused)
            .accessibilityLabel("\(workspace.activeLeaf.title) panel")
            .accessibilityFocused($leafAXFocused)
    }

    /// Maps a leaf to its panel view. All leaves are registered, so the switch
    /// is exhaustive over `Leaf` — no default arm. The
    /// seven panels
    /// ported here from the retired sidebar stack are unchanged in binding and
    /// AX; only their outer self-hiding `EmptyView` gates became labeled leaf
    /// empty states (a leaf must never be a blank rectangle — DoD §A). Registry
    /// order (embeds → math → code → diagrams → tasks) keeps the content-block
    /// leaves grouped between embeds and tasks exactly as the stack did.
    @ViewBuilder
    private func leafContent(_ leaf: Leaf) -> some View {
        switch leaf {
        case .outline:
            OutlineSidebar()
        case .backlinks:
            BacklinksPanel()
        case .outgoingLinks:
            OutgoingLinksPanel()
        case .connections:
            ConnectionsPanel()
        case .embeds:
            EmbedsPanel()
        case .math:
            MathBlocksPanel()
        case .code:
            CodeBlocksPanel()
        case .diagrams:
            DiagramsPanel()
        case .tasks:
            TasksPanel()
        case .tasksReview:
            TasksReviewPanel()
        case .history:
            HistoryPanel()
        case .citations:
            CitationsPanel()
        case .bibliography:
            BibliographyPanel()
        case .queries:
            BaseQueriesPanel()
        case .basesDock:
            BasesDockPanel()
        case .syncDiagnostics:
            SyncDiagnosticsPanel()
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
/// `@FocusState` highlight inside it (shown as a 2pt accent focus ring on the
/// highlighted item — WCAG 2.4.7), Return/Space activates (#859).
private struct LeafRailView: View {
    let activeLeaf: Leaf
    /// Highlight POSITION — plain state (Codex red-team; see RightPaneView).
    @Binding var railHighlight: Leaf?
    /// Rail keyboard-focus OWNERSHIP — a single Boolean focus stop.
    var railFocused: FocusState<Bool>.Binding
    let onActivate: (Leaf) -> Void

    var body: some View {
        VStack(spacing: 0) {
            ForEach(Leaf.registered) { leaf in
                railItem(leaf)
            }
            Spacer(minLength: 0)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        // macOS 26 Liquid Glass on the rail; the exact `surface` token below 26
        // (U5-1) — appearance-only, no layout change.
        .slateChromeMaterial(fallback: Tokens.ColorRole.surface)
        // Rail glyphs render hierarchical so the larger leaf icons gain depth
        // from a single accent — the Obsidian-rail feel (U5-1, DoD §B).
        .slateSymbolSurface(.rail)
        // One focus stop for the whole rail: the container is the single
        // focusable, and `railFocused` (Bool) tracks whether it owns
        // keyboard focus; the item Buttons are not individually
        // focus-reachable by Tab (arrow-within, Tab-out — the segmented
        // picker it replaces behaved the same). RightPaneView seeds the
        // highlight to the active leaf when the rail gains focus so ↑/↓
        // start from what's shown.
        .focusable()
        .focused(railFocused)
        .onMoveCommand { direction in
            let origin = railHighlight ?? activeLeaf
            if let next = Leaf.railMove(from: origin, direction) {
                railHighlight = next
            }
        }
        // Return / Space activate the highlighted leaf for a NON-VoiceOver
        // keyboard user (#859, WCAG 2.1.1): the item Buttons aren't individual
        // focus stops (arrow-within, Tab-out), so bare Return/Space on the rail
        // container would otherwise reach nothing — the `.accessibilityAction(
        // .default)` below is VoiceOver's activate path only. onKeyPress fires
        // only while the rail holds keyboard focus, so it can't steal the key
        // from other surfaces. Falls back to the active leaf if focus just
        // landed and the highlight hasn't seeded yet.
        .onKeyPress(.return) {
            onActivate(railHighlight ?? activeLeaf)
            return .handled
        }
        .onKeyPress(.space) {
            onActivate(railHighlight ?? activeLeaf)
            return .handled
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Panel rail")
        .accessibilityHint("Choose which panel is shown")
        .accessibilityAction(.default) {
            onActivate(railHighlight ?? activeLeaf)
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
                // idiom (TasksPanel, TasksReviewPanel), never a frozen
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
                .overlay {
                    // Focus indicator (#859, WCAG 2.4.7): the rail is ONE focus
                    // stop, so the system ring outlines the whole rail and can't
                    // show WHICH leaf the ↑/↓ highlight is on. Paint a 2pt accent
                    // ring on the highlighted item — distinct from the active
                    // leaf's leading bar — so a sighted keyboard user sees their
                    // position before pressing Return/Space. `accentText` is the
                    // APCA-gated accent foreground role (never a raw literal);
                    // decorative (hit-testing off, AX-hidden — the highlight's
                    // meaning rides the focus/selection semantics VoiceOver reads).
                    // Gated on `railFocused` so a highlight that outlived focus
                    // (plain state) never paints a ring while focus is elsewhere.
                    if railFocused.wrappedValue, railHighlight == leaf {
                        RoundedRectangle(cornerRadius: Tokens.Radius.small)
                            .strokeBorder(Tokens.ColorRole.accentText, lineWidth: 2)
                            .allowsHitTesting(false)
                            .accessibilityHidden(true)
                    }
                }
                .contentShape(Rectangle())
        }
        // Shared rest/hover/pressed affordance (U5-2). Selection stays the 2pt
        // leading bar + tint above; focus stays the system ring on the rail.
        .buttonStyle(.interactiveRow(cornerRadius: Tokens.Radius.small))
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
