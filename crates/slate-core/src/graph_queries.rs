// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Graph structural queries — W6-2 PR 0b (#746), §W-G rows B–K.
//!
//! Every rule in here was derived in Swift inside the mac graph
//! (`docs/plans/18_windows_port/specs/w6_2_graph_spec.md` §2): the
//! visible set and its label-priority subset, the Connections tree, the
//! stable node key, the visible neighbours, the name filter, the table
//! rows with their cells and their total order, the constants, the
//! action set, the spatial and structural steps, the ghost's note path.
//! Each is now one pure function of the session's graph payloads (or of
//! its arguments), so the two hosts answer the same question the same
//! way and neither owns a copy (R-D, "no re-derivation"). The contracts
//! are `docs/plans/35_graph_contracts.md` §"PR 0b"; the mac citations
//! sit beside each rule.
//!
//! Nothing here touches SQLite or the index lock: these are functions
//! of `GraphSnapshot` / `GraphNeighborhood`, and the session layer is
//! one `graph_snapshot` away from each of them. The session wraps the
//! answers in generation-tagged records (contract 0b-2b) so a host can
//! detect an answer that belongs to an index it no longer holds.

use std::cmp::Ordering;

use crate::a11y::GRAPH_NEIGHBOR_LABEL_CAP;
use crate::graph::{
    EdgeKind, GraphFilter, GraphNeighborhood, GraphNode, GraphSnapshot, NodeKey, NodeKind,
    ghost_key,
};

/// The FFI names of the whole 0b query surface (contract 0b-15). A core
/// test proves each names a `pub fn` here, in `graph_config` or on the
/// session; the uniffi crate's tripwire proves each is exported.
pub const GRAPH_QUERY_SURFACE: &[&str] = &[
    "graph_visibility",
    "graph_topology",
    "graph_neighbors",
    "graph_connections_tree",
    "graph_table_rows",
    "graph_stable_key_for_path",
    "graph_label_matches",
    "graph_table_columns",
    "graph_table_default_sort",
    "graph_constants",
    "graph_node_diameter",
    "graph_row_actions",
    "graph_spatial_step",
    "graph_structural_step",
    "graph_ghost_note_path",
    "graph_config_default",
    "graph_config_decode",
    "graph_config_encode",
    "graph_config_matching_group",
    "graph_config_next_group_style",
    "graph_color_tokens",
    "graph_ring_styles",
    "graph_surface_modes",
    "graph_verbosities",
];

// ---------------------------------------------------------------------------
// Row H — the constants (contract 0b-8)

/// Above this many VISIBLE nodes the diagram drops to a tiled summary
/// and routes accessibility to the Table (`GraphDiagramModel.swift:58`).
/// Inclusive: 1,500 is tier A, 1,501 tier B.
pub const TIER_B_THRESHOLD: u32 = 1500;
/// The diagram labels at most this many visible nodes, ranked by
/// in-links (`GraphDiagramView.swift:643`).
pub const LABEL_CAP: u32 = 200;
/// The Connections depth window (`AppState+Connections.swift:31–33`).
pub const CONNECTIONS_DEPTH_MIN: u32 = 1;
pub const CONNECTIONS_DEPTH_MAX: u32 = 3;
/// Node diameter bounds in layout units (`GraphDiagramView.swift:461–464`).
pub const NODE_DIAMETER_MIN: f64 = 8.0;
pub const NODE_DIAMETER_MAX: f64 = 28.0;

/// The constants with accessible meaning, as one record — every field
/// is the named constant above (a host that re-types one has re-derived
/// it; `graph_constants_are_the_named_constants` asserts the mapping).
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct GraphConstants {
    pub tier_b_threshold: u32,
    pub label_cap: u32,
    pub connections_depth_min: u32,
    pub connections_depth_max: u32,
    pub node_diameter_min: f64,
    pub node_diameter_max: f64,
    pub neighbor_label_cap: u32,
}

pub fn constants() -> GraphConstants {
    GraphConstants {
        tier_b_threshold: TIER_B_THRESHOLD,
        label_cap: LABEL_CAP,
        connections_depth_min: CONNECTIONS_DEPTH_MIN,
        connections_depth_max: CONNECTIONS_DEPTH_MAX,
        node_diameter_min: NODE_DIAMETER_MIN,
        node_diameter_max: NODE_DIAMETER_MAX,
        neighbor_label_cap: GRAPH_NEIGHBOR_LABEL_CAP as u32,
    }
}

/// Node diameter in layout units: `8 + 6·ln(1 + in_links)`, clamped to
/// 8..=28 (spec §P2-3).
pub fn node_diameter(in_links: u32) -> f64 {
    let d = 8.0 + 6.0 * (1.0 + f64::from(in_links)).ln();
    d.clamp(NODE_DIAMETER_MIN, NODE_DIAMETER_MAX)
}

/// The Connections depth, clamped into the window.
pub fn clamp_depth(depth: u32) -> u32 {
    depth.clamp(CONNECTIONS_DEPTH_MIN, CONNECTIONS_DEPTH_MAX)
}

// ---------------------------------------------------------------------------
// Row C — the stable key (contract 0b-3)

/// The cross-projection, cross-generation identity of a node: `p:` +
/// the vault path for a file, `g:` + the percent-encoded ghost key for
/// an unresolved target — the SAME folded string `NodeKey::Ghost`
/// carries. Two disjoint namespaces (`GraphViewState.swift:57–78`, now
/// deleted; the recorded divergences from it are 0b-D1).
pub fn stable_key(key: &NodeKey) -> String {
    match key {
        NodeKey::Path(path) => stable_key_for_path(path),
        NodeKey::Ghost(folded) => format!("g:{}", percent_encode_non_alphanumeric(folded)),
    }
}

/// The `p:` form for a path the host holds without a node.
pub fn stable_key_for_path(path: &str) -> String {
    format!("p:{path}")
}

/// Every UTF-8 byte of a character that is not Unicode alphanumeric is
/// written `%XX` (uppercase hex); alphanumerics pass through. Rust's
/// `char::is_alphanumeric` — the recorded divergence from Foundation's
/// `.alphanumerics`, which also admits combining marks (0b-D1).
pub fn percent_encode_non_alphanumeric(text: &str) -> String {
    let mut out = String::with_capacity(text.len());
    for ch in text.chars() {
        if ch.is_alphanumeric() {
            out.push(ch);
        } else {
            let mut buf = [0u8; 4];
            for byte in ch.encode_utf8(&mut buf).bytes() {
                out.push_str(&format!("%{byte:02X}"));
            }
        }
    }
    out
}

/// The stable key of a payload node: a path node keys on its path, a
/// ghost on its label's ghost key (every authored variant of a target
/// folds to the one key the index stores, so the label — one such
/// variant — folds to it too).
pub fn stable_key_of(node: &GraphNode) -> String {
    match &node.path {
        Some(path) => stable_key_for_path(path),
        None => format!(
            "g:{}",
            percent_encode_non_alphanumeric(&ghost_key(&node.label))
        ),
    }
}

// ---------------------------------------------------------------------------
// Row E — the name filter, the visible set (contract 0b-6)

/// The ONE fold (0b-6): NFD, combining marks dropped, lower-cased.
/// Locale-free. The recorded divergence from Foundation's
/// `.caseInsensitive, .diacriticInsensitive` is 0b-D3.
pub fn name_filter_fold(text: &str) -> String {
    use unicode_normalization::UnicodeNormalization;
    use unicode_normalization::char::is_combining_mark;
    text.nfd()
        .filter(|c| !is_combining_mark(*c))
        .collect::<String>()
        .to_lowercase()
}

/// `label` matches `needle` when the folded label contains the folded,
/// trimmed needle (Rust `trim`: Unicode `White_Space`, newlines
/// included — 0b-D3); an empty needle matches everything.
pub fn label_matches(label: &str, needle: &str) -> bool {
    let needle = name_filter_fold(needle.trim());
    if needle.is_empty() {
        return true;
    }
    name_filter_fold(label).contains(&needle)
}

/// The one visibility record both projections hold (0b-2): the backend
/// filter, the name needle, and the preset's kind overlay
/// (`kind_only = Ghost` is the Unresolved preset, which `GraphFilter`
/// cannot express).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphVisibilityQuery {
    pub filter: GraphFilter,
    pub name_query: String,
    pub kind_only: Option<NodeKind>,
}

impl GraphVisibilityQuery {
    pub fn passes(&self, node: &GraphNode) -> bool {
        if self.kind_only.is_some_and(|kind| node.kind != kind) {
            return false;
        }
        label_matches(&node.label, &self.name_query)
    }
}

/// The visible set under a query (0b-6), generation-tagged (0b-2b).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphVisibility {
    pub generation: u64,
    /// The node count under the backend filter alone.
    pub total: u64,
    /// The nodes that also pass the needle and the kind overlay, in the
    /// snapshot's node order.
    pub ids: Vec<u64>,
    /// The ids that take a label slot: all of `ids` when at most
    /// `LABEL_CAP`, else the top `LABEL_CAP` by in-links descending with
    /// the label order as the tie-break, in that ranked order.
    pub labeled: Vec<u64>,
}

/// The visible set of a snapshot (fetched under `q.filter`) under the
/// needle and the kind overlay.
pub fn visibility(snapshot: &GraphSnapshot, q: &GraphVisibilityQuery) -> GraphVisibility {
    let visible: Vec<&GraphNode> = snapshot.nodes.iter().filter(|n| q.passes(n)).collect();
    let ids: Vec<u64> = visible.iter().map(|n| n.id).collect();
    let labeled = if visible.len() <= LABEL_CAP as usize {
        ids.clone()
    } else {
        let mut ranked: Vec<&GraphNode> = visible.clone();
        ranked.sort_by(|a, b| {
            b.in_links.cmp(&a.in_links).then_with(|| {
                label_order(
                    (&a.label, &stable_key_of(a), a.id),
                    (&b.label, &stable_key_of(b), b.id),
                )
            })
        });
        ranked
            .iter()
            .take(LABEL_CAP as usize)
            .map(|n| n.id)
            .collect()
    };
    GraphVisibility {
        generation: snapshot.generation,
        total: snapshot.nodes.len() as u64,
        ids,
        labeled,
    }
}

// ---------------------------------------------------------------------------
// Row D — visible neighbours (contract 0b-6b; the content cap is 0a's)

/// One visible neighbour: the id for the step, the key and the label
/// for the accessible content.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphNeighbor {
    pub id: u64,
    pub stable_key: String,
    pub label: String,
}

/// The visible neighbours of one node, generation-tagged.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphNeighbors {
    pub generation: u64,
    pub neighbors: Vec<GraphNeighbor>,
}

/// The VISIBLE neighbours of `id` — both directions, unique, in the
/// snapshot's edge order (`GraphDiagramView.swift:856–874`, `:1221–1228`).
/// A neighbour outside `visible` is never surfaced; an `id` that is not
/// itself visible has none.
pub fn neighbors(snapshot: &GraphSnapshot, visible: &[u64], id: u64) -> Vec<GraphNeighbor> {
    use std::collections::{HashMap, HashSet};
    let visible: HashSet<u64> = visible.iter().copied().collect();
    if !visible.contains(&id) {
        return Vec::new();
    }
    let by_id: HashMap<u64, &GraphNode> = snapshot.nodes.iter().map(|n| (n.id, n)).collect();
    let mut seen = HashSet::new();
    let mut out = Vec::new();
    for edge in &snapshot.edges {
        let other = if edge.source_id == id {
            edge.target_id
        } else if edge.target_id == id {
            edge.source_id
        } else {
            continue;
        };
        if other == id || !visible.contains(&other) {
            continue;
        }
        let Some(node) = by_id.get(&other) else {
            continue;
        };
        if seen.insert(other) {
            out.push(GraphNeighbor {
                id: other,
                stable_key: stable_key_of(node),
                label: node.label.clone(),
            });
        }
    }
    out
}

// ---------------------------------------------------------------------------
// The topology — one record per rebuild (contract 0b-6b, design B)

/// One visible node with everything the diagram renders, speaks or acts
/// on: the key, the label, the path, the kind, the four degrees, the
/// component, the orphan flag, core's diameter (unscaled — the display
/// multiplier is a rendering), the matching group, the label slot, and
/// the visible neighbours in edge order — so the row copy, Where-am-I,
/// the navigation actions and the styling read one record.
#[derive(Debug, Clone, PartialEq)]
pub struct GraphTopologyNode {
    pub id: u64,
    pub stable_key: String,
    pub label: String,
    pub path: Option<String>,
    pub kind: NodeKind,
    pub in_links: u32,
    pub out_links: u32,
    pub in_embeds: u32,
    pub out_embeds: u32,
    pub component: u32,
    pub is_orphan: bool,
    pub diameter: f64,
    pub group: Option<u32>,
    pub labeled: bool,
    pub neighbors: Vec<GraphNeighbor>,
}

/// The visible nodes under a query with their per-node data, and the
/// visible edges, generation-tagged: the diagram crosses the FFI once
/// per semantic epoch (design B).
#[derive(Debug, Clone, PartialEq)]
pub struct GraphTopology {
    pub generation: u64,
    /// The node count under the backend filter alone.
    pub total: u64,
    /// In the snapshot's node order.
    pub nodes: Vec<GraphTopologyNode>,
    /// The snapshot's edges whose BOTH endpoints are visible, in the
    /// snapshot's edge order, self-edges included (they are edges, not
    /// neighbours — 0b-D9); `rebuildEdges` draws them as given.
    pub edges: Vec<crate::graph::GraphEdge>,
}

/// The topology of a snapshot (fetched under `q.filter`) under the
/// needle, the kind overlay and the config's groups. Each node's
/// `neighbors` equal `neighbors(snapshot, &visible, id)` — one pass over
/// the edges in order builds every list at once.
pub fn topology(
    snapshot: &GraphSnapshot,
    q: &GraphVisibilityQuery,
    config: &crate::graph_config::GraphConfig,
) -> GraphTopology {
    use std::collections::{HashMap, HashSet};
    let v = visibility(snapshot, q);
    let visible: HashSet<u64> = v.ids.iter().copied().collect();
    let labeled: HashSet<u64> = v.labeled.iter().copied().collect();
    let by_id: HashMap<u64, &GraphNode> = snapshot.nodes.iter().map(|n| (n.id, n)).collect();
    let mut lists: HashMap<u64, Vec<u64>> = HashMap::new();
    let mut seen: HashMap<u64, HashSet<u64>> = HashMap::new();
    for edge in &snapshot.edges {
        if edge.source_id == edge.target_id {
            continue;
        }
        for (node, other) in [
            (edge.source_id, edge.target_id),
            (edge.target_id, edge.source_id),
        ] {
            if visible.contains(&node)
                && visible.contains(&other)
                && by_id.contains_key(&other)
                && seen.entry(node).or_default().insert(other)
            {
                lists.entry(node).or_default().push(other);
            }
        }
    }
    let nodes = v
        .ids
        .iter()
        .filter_map(|id| by_id.get(id).copied())
        .map(|n| GraphTopologyNode {
            id: n.id,
            stable_key: stable_key_of(n),
            label: n.label.clone(),
            path: n.path.clone(),
            kind: n.kind,
            in_links: n.in_links,
            out_links: n.out_links,
            in_embeds: n.in_embeds,
            out_embeds: n.out_embeds,
            component: n.component,
            is_orphan: n.is_orphan,
            diameter: node_diameter(n.in_links),
            group: crate::graph_config::matching_group(config, &n.label).map(|i| i as u32),
            labeled: labeled.contains(&n.id),
            neighbors: lists
                .get(&n.id)
                .into_iter()
                .flatten()
                .filter_map(|other| by_id.get(other))
                .map(|o| GraphNeighbor {
                    id: o.id,
                    stable_key: stable_key_of(o),
                    label: o.label.clone(),
                })
                .collect(),
        })
        .collect();
    let edges = snapshot
        .edges
        .iter()
        .filter(|e| visible.contains(&e.source_id) && visible.contains(&e.target_id))
        .copied()
        .collect();
    GraphTopology {
        generation: snapshot.generation,
        total: v.total,
        nodes,
        edges,
    }
}

// ---------------------------------------------------------------------------
// Row G — label order and the table rows (contracts 0b-5, 0b-7)

/// One segment of a folded label: a maximal run of ASCII digits, or a
/// maximal run of anything else.
fn segments(s: &str) -> Vec<(bool, &str)> {
    let mut out = Vec::new();
    let mut start = 0;
    let mut digit: Option<bool> = None;
    for (i, ch) in s.char_indices() {
        let d = ch.is_ascii_digit();
        match digit {
            Some(prev) if prev == d => {}
            Some(prev) => {
                out.push((prev, &s[start..i]));
                start = i;
                digit = Some(d);
            }
            None => digit = Some(d),
        }
    }
    if let Some(d) = digit {
        out.push((d, &s[start..]));
    }
    out
}

/// Two digit runs, parse-free (0b-5): strip leading zeros; more
/// remaining digits is greater; equal length → bytewise; equal value →
/// fewer leading zeros first (`2` < `02`, `10` < `010`, `0` < `00`).
fn digit_run_cmp(a: &str, b: &str) -> Ordering {
    let ta = a.trim_start_matches('0');
    let tb = b.trim_start_matches('0');
    ta.len()
        .cmp(&tb.len())
        .then_with(|| ta.cmp(tb))
        .then_with(|| a.len().cmp(&b.len()))
}

/// Natural comparison of two folded strings, segment-wise: digit runs
/// parse-free, non-digit runs bytewise, a digit run before a non-digit
/// run at the same position, then the shorter segment list first.
pub fn natural_cmp(a: &str, b: &str) -> Ordering {
    let sa = segments(a);
    let sb = segments(b);
    for (x, y) in sa.iter().zip(sb.iter()) {
        let c = match (x.0, y.0) {
            (true, true) => digit_run_cmp(x.1, y.1),
            (true, false) => Ordering::Less,
            (false, true) => Ordering::Greater,
            (false, false) => x.1.as_bytes().cmp(y.1.as_bytes()),
        };
        if c != Ordering::Equal {
            return c;
        }
    }
    sa.len().cmp(&sb.len())
}

/// The one label order (0b-5): the folded labels naturally, then the
/// raw labels bytewise, then the stable key, then the node id — a
/// strict total order on distinct rows unconditionally. The recorded
/// divergence from `localizedStandardCompare` and the mac's tie-breaks
/// (`GraphTableView.swift:535–543`) is 0b-D2.
pub fn label_order(a: (&str, &str, u64), b: (&str, &str, u64)) -> Ordering {
    natural_cmp(&name_filter_fold(a.0), &name_filter_fold(b.0))
        .then_with(|| a.0.as_bytes().cmp(b.0.as_bytes()))
        .then_with(|| a.1.cmp(b.1))
        .then_with(|| a.2.cmp(&b.2))
}

/// The nine sortable columns in display order (`GraphTableView.swift:516–525`).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum GraphTableColumn {
    Note,
    LinksIn,
    LinksOut,
    EmbedsIn,
    EmbedsOut,
    Component,
    Modified,
    Folder,
    Kind,
}

impl GraphTableColumn {
    pub const ALL: [GraphTableColumn; 9] = [
        GraphTableColumn::Note,
        GraphTableColumn::LinksIn,
        GraphTableColumn::LinksOut,
        GraphTableColumn::EmbedsIn,
        GraphTableColumn::EmbedsOut,
        GraphTableColumn::Component,
        GraphTableColumn::Modified,
        GraphTableColumn::Folder,
        GraphTableColumn::Kind,
    ];

    pub fn header(self) -> &'static str {
        match self {
            GraphTableColumn::Note => "Note",
            GraphTableColumn::LinksIn => "Links in",
            GraphTableColumn::LinksOut => "Links out",
            GraphTableColumn::EmbedsIn => "Embeds in",
            GraphTableColumn::EmbedsOut => "Embeds out",
            GraphTableColumn::Component => "Component",
            GraphTableColumn::Modified => "Modified",
            GraphTableColumn::Folder => "Folder",
            GraphTableColumn::Kind => "Kind",
        }
    }

    /// The column's index into `GraphTableRow::cells`.
    pub fn index(self) -> usize {
        GraphTableColumn::ALL
            .iter()
            .position(|c| *c == self)
            .expect("every column is listed")
    }
}

/// One column of the ordered column model (0b-7, design B): a grid is
/// built from the vector, its sort index is the vector index, and a
/// row's cell for that index is `cells[index]`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphTableColumnSpec {
    pub column: GraphTableColumn,
    pub header: String,
}

/// The nine columns in display order with their headers.
pub fn table_columns() -> Vec<GraphTableColumnSpec> {
    GraphTableColumn::ALL
        .iter()
        .map(|c| GraphTableColumnSpec {
            column: *c,
            header: c.header().to_string(),
        })
        .collect()
}

/// A sort request: the column and its direction. The default is
/// `LinksIn` descending — hubs first (spec §P1-2).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct GraphTableSort {
    pub column: GraphTableColumn,
    pub ascending: bool,
}

impl Default for GraphTableSort {
    fn default() -> Self {
        GraphTableSort {
            column: GraphTableColumn::LinksIn,
            ascending: false,
        }
    }
}

/// The preset-free default sort, FETCHED by both hosts (W6-2 PR A, AD-1):
/// an inventory of one is still an inventory (0bD-13), so no host types
/// `LinksIn, descending` for itself.
pub fn table_default_sort() -> GraphTableSort {
    GraphTableSort::default()
}

/// One graph-table row: the nine cells core-formatted in column order,
/// plus the raw values for actions and logic (`GraphTableView.swift:444–512`).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphTableRow {
    pub stable_key: String,
    pub node_id: u64,
    pub label: String,
    pub path: Option<String>,
    pub kind: NodeKind,
    /// Exactly nine: label, the five counts in decimal, the modified
    /// text, the folder, the kind label.
    pub cells: Vec<String>,
    pub links_in: u32,
    pub links_out: u32,
    pub embeds_in: u32,
    pub embeds_out: u32,
    pub component: u32,
    pub modified_ms: Option<i64>,
}

/// The rows under a query and a sort, generation-tagged.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphTableRows {
    pub generation: u64,
    /// The node count under the backend filter alone.
    pub total: u64,
    pub rows: Vec<GraphTableRow>,
}

/// The Kind cell (`GraphTableView.swift:469–475`).
pub fn kind_label(kind: NodeKind) -> &'static str {
    match kind {
        NodeKind::Note => "Note",
        NodeKind::Attachment => "Attachment",
        NodeKind::Ghost => "Unresolved",
    }
}

/// The Folder cell: the path up to its last `/`, empty at the vault
/// root (`AppState+GraphTable.swift:418–420`).
pub fn folder_of(path: &str) -> String {
    match path.rfind('/') {
        Some(at) => path[..at].to_string(),
        None => String::new(),
    }
}

/// The Modified cell: `YYYY-MM-DD HH:MM` in UTC, empty when absent — a
/// §W-A cell cannot carry a locale or a zone (the recorded divergence
/// from mac's locale medium date and short time is 0b-D4).
pub fn modified_text(modified_ms: Option<i64>) -> String {
    use chrono::{DateTime, Utc};
    match modified_ms.and_then(DateTime::<Utc>::from_timestamp_millis) {
        Some(when) => when.format("%Y-%m-%d %H:%M").to_string(),
        None => String::new(),
    }
}

fn row_of(node: &GraphNode) -> GraphTableRow {
    let folder = node.path.as_deref().map(folder_of).unwrap_or_default();
    GraphTableRow {
        stable_key: stable_key_of(node),
        node_id: node.id,
        label: node.label.clone(),
        path: node.path.clone(),
        kind: node.kind,
        cells: vec![
            node.label.clone(),
            node.in_links.to_string(),
            node.out_links.to_string(),
            node.in_embeds.to_string(),
            node.out_embeds.to_string(),
            node.component.to_string(),
            modified_text(node.modified_ms),
            folder,
            kind_label(node.kind).to_string(),
        ],
        links_in: node.in_links,
        links_out: node.out_links,
        embeds_in: node.in_embeds,
        embeds_out: node.out_embeds,
        component: node.component,
        modified_ms: node.modified_ms,
    }
}

fn by_label(a: &GraphTableRow, b: &GraphTableRow) -> Ordering {
    label_order(
        (&a.label, &a.stable_key, a.node_id),
        (&b.label, &b.stable_key, b.node_id),
    )
}

/// The directional comparator (`GraphTableView.swift:548–581`): a
/// numeric primary in the requested direction with the label order as
/// the tie-break ALWAYS ascending; `Note` is the label order in the
/// requested direction; `Modified` orders a missing value lowest;
/// `Folder` orders the folders by the fold then falls through; `Kind`
/// orders the kind labels bytewise then falls through.
pub fn compare_rows(sort: GraphTableSort, a: &GraphTableRow, b: &GraphTableRow) -> Ordering {
    let directed = |o: Ordering| if sort.ascending { o } else { o.reverse() };
    let numeric = |l: u32, r: u32| {
        if l != r {
            directed(l.cmp(&r))
        } else {
            by_label(a, b)
        }
    };
    match sort.column {
        GraphTableColumn::Note => directed(by_label(a, b)),
        GraphTableColumn::LinksIn => numeric(a.links_in, b.links_in),
        GraphTableColumn::LinksOut => numeric(a.links_out, b.links_out),
        GraphTableColumn::EmbedsIn => numeric(a.embeds_in, b.embeds_in),
        GraphTableColumn::EmbedsOut => numeric(a.embeds_out, b.embeds_out),
        GraphTableColumn::Component => numeric(a.component, b.component),
        GraphTableColumn::Modified => {
            let l = a.modified_ms.unwrap_or(i64::MIN);
            let r = b.modified_ms.unwrap_or(i64::MIN);
            if l != r {
                directed(l.cmp(&r))
            } else {
                by_label(a, b)
            }
        }
        GraphTableColumn::Folder => {
            let fa = &a.cells[GraphTableColumn::Folder.index()];
            let fb = &b.cells[GraphTableColumn::Folder.index()];
            if fa != fb {
                directed(natural_cmp(&name_filter_fold(fa), &name_filter_fold(fb)))
                    .then_with(|| directed(fa.as_bytes().cmp(fb.as_bytes())))
            } else {
                by_label(a, b)
            }
        }
        GraphTableColumn::Kind => {
            let ka = kind_label(a.kind);
            let kb = kind_label(b.kind);
            if ka != kb {
                directed(ka.as_bytes().cmp(kb.as_bytes()))
            } else {
                by_label(a, b)
            }
        }
    }
}

/// The visible nodes of a snapshot as table rows in the requested order.
pub fn table_rows(
    snapshot: &GraphSnapshot,
    q: &GraphVisibilityQuery,
    sort: GraphTableSort,
) -> Vec<GraphTableRow> {
    let mut rows: Vec<GraphTableRow> = snapshot
        .nodes
        .iter()
        .filter(|n| q.passes(n))
        .map(row_of)
        .collect();
    rows.sort_by(|a, b| compare_rows(sort, a, b));
    rows
}

// ---------------------------------------------------------------------------
// Row I — the action set (contract 0b-9)

/// The canonical node-action set every projection builds its menus and
/// custom actions from (`GraphViewState.swift:15–48`). Pin/Unpin is
/// diagram-only and deliberately absent.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum GraphRowAction {
    Open,
    OpenInNewTab,
    ShowConnections,
    Reveal,
    CreateNote,
}

impl GraphRowAction {
    pub const ALL: [GraphRowAction; 5] = [
        GraphRowAction::Open,
        GraphRowAction::OpenInNewTab,
        GraphRowAction::ShowConnections,
        GraphRowAction::Reveal,
        GraphRowAction::CreateNote,
    ];
}

/// The user-facing label — the VoiceOver / UIA action name AND the
/// context-menu title, identical across projections.
pub fn row_action_title(action: GraphRowAction) -> &'static str {
    match action {
        GraphRowAction::Open => "Open",
        GraphRowAction::OpenInNewTab => "Open in New Tab",
        GraphRowAction::ShowConnections => "Show connections",
        GraphRowAction::Reveal => "Reveal in File Tree",
        GraphRowAction::CreateNote => "Create note",
    }
}

/// One entry of the ordered eligibility vector (0b-9, design B): the
/// action and its title; a host builds its menus, custom actions and
/// `allCases` from the vectors, never from a literal.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphRowActionSpec {
    pub action: GraphRowAction,
    pub title: String,
}

/// The ordered actions a node of `kind` is ELIGIBLE for, with their
/// titles: the four navigation actions need a real file, `CreateNote`
/// applies only to a ghost. Whether one can run NOW is the host's
/// admission (0bD-8).
pub fn row_actions(kind: NodeKind) -> Vec<GraphRowActionSpec> {
    let ghost = matches!(kind, NodeKind::Ghost);
    GraphRowAction::ALL
        .iter()
        .copied()
        .filter(|a| (*a == GraphRowAction::CreateNote) == ghost)
        .map(|a| GraphRowActionSpec {
            action: a,
            title: row_action_title(a).to_string(),
        })
        .collect()
}

// ---------------------------------------------------------------------------
// Row K — the ghost's note path (contract 0b-11)

/// Map an authored ghost target to a vault path, total over `/`-strings
/// (a backslash is an ordinary character): trim, strip ONE leading `./`
/// or `/`, and append `.md` unless the last component's extension is a
/// Markdown one (`AppState+Connections.swift:306–315`).
pub fn ghost_note_path(target_raw: &str) -> String {
    let trimmed = target_raw.trim();
    let stripped = trimmed
        .strip_prefix("./")
        .or_else(|| trimmed.strip_prefix('/'))
        .unwrap_or(trimmed);
    let last = stripped.rsplit('/').next().unwrap_or(stripped);
    let has_markdown_ext = match last.rfind('.') {
        Some(0) | None => false,
        Some(at) => {
            let ext = last[at + 1..].to_ascii_lowercase();
            matches!(ext.as_str(), "md" | "markdown" | "mdown" | "mkd")
        }
    };
    if has_markdown_ext {
        stripped.to_string()
    } else {
        format!("{stripped}.md")
    }
}

// ---------------------------------------------------------------------------
// Row J — the spatial and structural steps (contract 0b-10)

/// A node's layout position, as the host holds it.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct GraphPoint {
    pub id: u64,
    pub x: f64,
    pub y: f64,
}

fn best_in_direction(
    by_id: &std::collections::HashMap<u64, (f64, f64)>,
    from: (f64, f64),
    dir: (f64, f64),
    candidates: impl Iterator<Item = u64>,
) -> Option<u64> {
    let mut best: Option<(u64, f64)> = None;
    for id in candidates {
        let Some(&(px, py)) = by_id.get(&id) else {
            continue;
        };
        let (vx, vy) = (px - from.0, py - from.1);
        let dist = vx.hypot(vy);
        if dist <= 0.0001 {
            continue;
        }
        let proj = (vx * dir.0 + vy * dir.1) / dist;
        if proj <= 0.1 {
            continue;
        }
        let score = dist / proj.max(0.0001);
        best = match best {
            Some((bid, bscore)) if score > bscore || (score == bscore && id >= bid) => {
                Some((bid, bscore))
            }
            _ => Some((id, score)),
        };
    }
    best.map(|(id, _)| id)
}

/// Arrow-key spatial navigation: first among `neighbors` that have a
/// point, then among every other point; a candidate scores only past a
/// tiny distance and past `cos θ > 0.1`; the score is `|v| / cos θ`;
/// the lowest wins, ties to the lower id (`GraphDiagramView.swift:1230–1250`).
/// Total: a non-finite point is ignored, a duplicated id keeps its first
/// point, and a missing origin or a zero / non-finite direction steps
/// nowhere.
pub fn spatial_step(
    points: &[GraphPoint],
    neighbors: &[u64],
    from: u64,
    dx: f64,
    dy: f64,
) -> Option<u64> {
    let len = dx.hypot(dy);
    if !len.is_finite() || len == 0.0 {
        return None;
    }
    let dir = (dx / len, dy / len);
    let mut by_id: std::collections::HashMap<u64, (f64, f64)> = std::collections::HashMap::new();
    let mut order: Vec<u64> = Vec::new();
    for p in points {
        if !(p.x.is_finite() && p.y.is_finite()) {
            continue;
        }
        if let std::collections::hash_map::Entry::Vacant(slot) = by_id.entry(p.id) {
            slot.insert((p.x, p.y));
            order.push(p.id);
        }
    }
    let origin = *by_id.get(&from)?;
    if let Some(best) = best_in_direction(&by_id, origin, dir, neighbors.iter().copied()) {
        return Some(best);
    }
    best_in_direction(
        &by_id,
        origin,
        dir,
        order.into_iter().filter(|id| *id != from),
    )
}

/// Tab / Shift-Tab: the next or previous id in `visible`, wrapping, from
/// the FIRST index of `from`; the first (or last) when `from` is absent
/// or not listed; `None` for an empty list (`GraphDiagramView.swift:1254–1262`).
pub fn structural_step(visible: &[u64], from: Option<u64>, forward: bool) -> Option<u64> {
    if visible.is_empty() {
        return None;
    }
    let Some(idx) = from.and_then(|f| visible.iter().position(|id| *id == f)) else {
        return Some(if forward {
            visible[0]
        } else {
            visible[visible.len() - 1]
        });
    };
    let n = visible.len();
    Some(if forward {
        visible[(idx + 1) % n]
    } else {
        visible[(idx + n - 1) % n]
    })
}

// ---------------------------------------------------------------------------
// Row B — the Connections tree (contract 0b-4)

/// One row of the flat pre-order Connections tree.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphConnectionRow {
    /// The occurrence path built from stable keys (0bD-10): `in` or
    /// `out`, then `/` + the percent-encoded key of each row from the
    /// first hop down to this one.
    pub id: String,
    /// 1 for a first-hop row; `depth` at most.
    pub level: u32,
    pub parent_id: Option<String>,
    pub node_id: u64,
    pub stable_key: String,
    pub label: String,
    pub path: Option<String>,
    pub target_raw: String,
    pub kind: NodeKind,
    /// Reached by embed edges only (no Link joined the two).
    pub embed_only: bool,
    pub in_links: u32,
    pub out_links: u32,
    /// `in_links + in_embeds` — the count the row copy speaks for a ghost.
    pub references: u32,
}

/// The tree for one centre: the first hop split by centre incidence,
/// each list pre-order with its nested hops, the neighbourhood's own
/// summary counts travelling with it so the leaf reads ONE record per
/// load (design A); generation-tagged by the session (0b-2b).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphConnectionsTree {
    pub generation: u64,
    pub center_id: u64,
    pub center_key: String,
    pub depth: u32,
    pub summary_counts: crate::graph::GraphNeighborhoodCounts,
    pub incoming: Vec<GraphConnectionRow>,
    pub outgoing: Vec<GraphConnectionRow>,
}

/// The mac's `ConnectionsModel` (`ConnectionsPanel.swift:375–499`) as
/// one function of the neighbourhood payload. `generation` is the
/// caller's (the session's, read under the same lock).
pub fn connections_tree(hood: &GraphNeighborhood, generation: u64) -> GraphConnectionsTree {
    use std::collections::{BTreeMap, BTreeSet, HashMap, HashSet};
    let nodes_by_id: HashMap<u64, &GraphNode> = hood.nodes.iter().map(|n| (n.id, n)).collect();
    let center = hood.center_id;

    // Undirected adjacency: node → neighbour → edge kinds (a node reached
    // by a link AND an embed is ONE neighbour with both kinds).
    let mut adjacency: HashMap<u64, HashMap<u64, BTreeSet<EdgeKind>>> = HashMap::new();
    let mut incoming_ids: BTreeMap<u64, BTreeSet<EdgeKind>> = BTreeMap::new();
    let mut outgoing_ids: BTreeMap<u64, BTreeSet<EdgeKind>> = BTreeMap::new();
    for edge in &hood.edges {
        adjacency
            .entry(edge.source_id)
            .or_default()
            .entry(edge.target_id)
            .or_default()
            .insert(edge.kind);
        adjacency
            .entry(edge.target_id)
            .or_default()
            .entry(edge.source_id)
            .or_default()
            .insert(edge.kind);
        if edge.source_id == center && edge.target_id != center {
            outgoing_ids
                .entry(edge.target_id)
                .or_default()
                .insert(edge.kind);
        }
        if edge.target_id == center && edge.source_id != center {
            incoming_ids
                .entry(edge.source_id)
                .or_default()
                .insert(edge.kind);
        }
        // A self-edge is not a connection to another note: omitted.
    }

    let leaf =
        |id: u64, kinds: &BTreeSet<EdgeKind>, level: u32, parent: Option<&str>, prefix: &str| {
            let node = nodes_by_id.get(&id)?;
            let key = stable_key_of(node);
            let embed_only = !kinds.contains(&EdgeKind::Link) && kinds.contains(&EdgeKind::Embed);
            Some(GraphConnectionRow {
                id: format!("{prefix}/{}", percent_encode_non_alphanumeric(&key)),
                level,
                parent_id: parent.map(str::to_string),
                node_id: node.id,
                stable_key: key,
                label: node.label.clone(),
                path: node.path.clone(),
                target_raw: node.label.clone(),
                kind: node.kind,
                embed_only,
                in_links: node.in_links,
                out_links: node.out_links,
                references: node.in_links + node.in_embeds,
            })
        };

    let ordered = |rows: &mut Vec<GraphConnectionRow>| {
        rows.sort_by(|a, b| {
            label_order(
                (&a.label, &a.stable_key, a.node_id),
                (&b.label, &b.stable_key, b.node_id),
            )
        });
    };

    /// A row builder: `(node id, edge kinds, level, parent id, id prefix)`.
    type Leaf<'a> = dyn Fn(u64, &BTreeSet<EdgeKind>, u32, Option<&str>, &str) -> Option<GraphConnectionRow>
        + 'a;

    // Children of `id`, excluding the ancestors on the current path (the
    // cycle guard), `remaining` further levels, pre-order.
    #[allow(clippy::too_many_arguments)]
    fn children(
        adjacency: &HashMap<u64, HashMap<u64, BTreeSet<EdgeKind>>>,
        leaf: &Leaf<'_>,
        ordered: &dyn Fn(&mut Vec<GraphConnectionRow>),
        id: u64,
        ancestors: &HashSet<u64>,
        remaining: u32,
        level: u32,
        parent_id: &str,
        out: &mut Vec<GraphConnectionRow>,
    ) {
        if remaining == 0 {
            return;
        }
        let mut next_ancestors = ancestors.clone();
        next_ancestors.insert(id);
        let mut rows: Vec<GraphConnectionRow> = Vec::new();
        if let Some(neighbors) = adjacency.get(&id) {
            for (neighbor, kinds) in neighbors {
                if next_ancestors.contains(neighbor) {
                    continue;
                }
                if let Some(row) = leaf(*neighbor, kinds, level, Some(parent_id), parent_id) {
                    rows.push(row);
                }
            }
        }
        ordered(&mut rows);
        for row in rows {
            let row_id = row.id.clone();
            let node_id = row.node_id;
            out.push(row);
            children(
                adjacency,
                leaf,
                ordered,
                node_id,
                &next_ancestors,
                remaining - 1,
                level + 1,
                &row_id,
                out,
            );
        }
    }

    let nest_depth = hood.depth.saturating_sub(1);
    let ancestors: HashSet<u64> = HashSet::from([center]);
    let first_hop = |ids: &BTreeMap<u64, BTreeSet<EdgeKind>>, prefix: &str| {
        let mut rows: Vec<GraphConnectionRow> = ids
            .iter()
            .filter_map(|(id, kinds)| leaf(*id, kinds, 1, None, prefix))
            .collect();
        ordered(&mut rows);
        let mut out = Vec::new();
        for row in rows {
            let row_id = row.id.clone();
            let node_id = row.node_id;
            out.push(row);
            children(
                &adjacency, &leaf, &ordered, node_id, &ancestors, nest_depth, 2, &row_id, &mut out,
            );
        }
        out
    };

    GraphConnectionsTree {
        generation,
        center_id: center,
        center_key: nodes_by_id
            .get(&center)
            .map(|n| stable_key_of(n))
            .unwrap_or_default(),
        depth: hood.depth,
        summary_counts: hood.summary_counts.clone(),
        incoming: first_hop(&incoming_ids, "in"),
        outgoing: first_hop(&outgoing_ids, "out"),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::graph::{GraphEdge, GraphFilter, GraphSnapshot, GraphSnapshotCounts};

    fn node(id: u64, label: &str, path: Option<&str>, kind: NodeKind) -> GraphNode {
        let key = match path {
            Some(p) => NodeKey::Path(p.to_string()),
            None => NodeKey::Ghost(ghost_key(label)),
        };
        GraphNode {
            id,
            stable_key: stable_key(&key),
            path: path.map(str::to_string),
            label: label.into(),
            kind,
            in_links: 0,
            out_links: 0,
            in_embeds: 0,
            out_embeds: 0,
            component: 0,
            is_orphan: false,
            pagerank: 0.0,
            modified_ms: None,
        }
    }

    fn with_degrees(mut n: GraphNode, in_links: u32, out_links: u32, in_embeds: u32) -> GraphNode {
        n.in_links = in_links;
        n.out_links = out_links;
        n.in_embeds = in_embeds;
        n
    }

    fn edge(source: u64, target: u64, kind: EdgeKind) -> GraphEdge {
        GraphEdge {
            source_id: source,
            target_id: target,
            kind,
            count: 1,
        }
    }

    fn hood(
        center: u64,
        depth: u32,
        nodes: Vec<GraphNode>,
        edges: Vec<GraphEdge>,
    ) -> GraphNeighborhood {
        GraphNeighborhood {
            center_id: center,
            depth,
            nodes,
            edges,
            audio_summary: String::new(),
            summary_counts: crate::graph::GraphNeighborhoodCounts {
                center_label: String::new(),
                in_links: 0,
                out_links: 0,
                note_count: 0,
                depth,
            },
        }
    }

    fn snapshot(nodes: Vec<GraphNode>, edges: Vec<GraphEdge>) -> GraphSnapshot {
        GraphSnapshot {
            nodes,
            edges,
            generation: 1,
            audio_summary: String::new(),
            summary_counts: GraphSnapshotCounts {
                notes: 0,
                links: 0,
                orphans: 0,
                unresolved: 0,
                filtered: false,
            },
        }
    }

    fn all() -> GraphVisibilityQuery {
        GraphVisibilityQuery {
            filter: GraphFilter::default(),
            name_query: String::new(),
            kind_only: None,
        }
    }

    fn needle(q: &str) -> GraphVisibilityQuery {
        GraphVisibilityQuery {
            name_query: q.into(),
            ..all()
        }
    }

    // --- Row B: the tree (0b-4) — ConnectionsPanelTests as Rust facts ------

    /// Depth-1 in/out split: incoming = edges into the centre, outgoing =
    /// edges from it; a ghost target is an unresolved row; an embed-only
    /// edge flags the row. Ids are built from stable keys (0bD-10).
    #[test]
    fn tree_splits_incoming_and_outgoing_with_ghosts_and_embeds() {
        let h = hood(
            1,
            1,
            vec![
                with_degrees(
                    node(1, "Center", Some("center.md"), NodeKind::Note),
                    1,
                    2,
                    0,
                ),
                with_degrees(node(2, "Inbound", Some("in.md"), NodeKind::Note), 0, 1, 0),
                with_degrees(node(3, "Outbound", Some("out.md"), NodeKind::Note), 1, 0, 0),
                with_degrees(node(4, "Missing", None, NodeKind::Ghost), 1, 0, 0),
                with_degrees(
                    node(5, "pic.png", Some("pic.png"), NodeKind::Attachment),
                    0,
                    0,
                    1,
                ),
            ],
            vec![
                edge(2, 1, EdgeKind::Link),
                edge(1, 3, EdgeKind::Link),
                edge(1, 4, EdgeKind::Link),
                edge(1, 5, EdgeKind::Embed),
            ],
        );
        let tree = connections_tree(&h, 7);
        assert_eq!(tree.generation, 7);
        assert_eq!(
            tree.summary_counts, h.summary_counts,
            "the leaf reads one record (design A)"
        );
        let labels =
            |rows: &[GraphConnectionRow]| rows.iter().map(|r| r.label.clone()).collect::<Vec<_>>();
        assert_eq!(labels(&tree.incoming), vec!["Inbound"]);
        assert_eq!(
            labels(&tree.outgoing),
            vec!["Missing", "Outbound", "pic.png"]
        );
        let ghost = tree.outgoing.iter().find(|r| r.label == "Missing").unwrap();
        assert_eq!(ghost.kind, NodeKind::Ghost);
        assert!(ghost.path.is_none());
        assert_eq!(ghost.references, 1);
        assert_eq!(ghost.stable_key, "g:missing");
        assert_eq!(ghost.id, "out/g%3Amissing");
        let pic = tree.outgoing.iter().find(|r| r.label == "pic.png").unwrap();
        assert!(pic.embed_only, "embed edge → embed-only row");
        assert_eq!(pic.kind, NodeKind::Attachment);
        assert_eq!(pic.references, 1, "in_links + in_embeds");
        let inbound = &tree.incoming[0];
        assert_eq!((inbound.in_links, inbound.out_links), (0, 1));
        assert_eq!(inbound.id, "in/p%3Ain%2Emd");
        assert_eq!(inbound.level, 1);
        assert_eq!(inbound.parent_id, None);
        assert_eq!(tree.center_key, "p:center.md");
        for row in tree.incoming.iter().chain(tree.outgoing.iter()) {
            assert!(
                !row.id.contains(&row.node_id.to_string()) || row.id.contains("%3A"),
                "no numeric id in {}",
                row.id
            );
        }
    }

    /// Centre → A → B: at depth 2 the A row is followed by B at level 2.
    #[test]
    fn tree_nests_the_second_hop_under_its_parent() {
        let h = hood(
            1,
            2,
            vec![
                node(1, "Center", Some("center.md"), NodeKind::Note),
                node(2, "A", Some("a.md"), NodeKind::Note),
                node(3, "B", Some("b.md"), NodeKind::Note),
            ],
            vec![edge(1, 2, EdgeKind::Link), edge(2, 3, EdgeKind::Link)],
        );
        let tree = connections_tree(&h, 1);
        let ids: Vec<(String, u32, Option<String>)> = tree
            .outgoing
            .iter()
            .map(|r| (r.id.clone(), r.level, r.parent_id.clone()))
            .collect();
        assert_eq!(
            ids,
            vec![
                ("out/p%3Aa%2Emd".to_string(), 1, None),
                (
                    "out/p%3Aa%2Emd/p%3Ab%2Emd".to_string(),
                    2,
                    Some("out/p%3Aa%2Emd".to_string())
                ),
            ]
        );
    }

    /// Depth 3 renders the third hop; the cycle guard stops C from
    /// re-expanding back to B or A.
    #[test]
    fn tree_renders_the_third_hop_with_the_cycle_guard() {
        let h = hood(
            1,
            3,
            vec![
                node(1, "Center", Some("center.md"), NodeKind::Note),
                node(2, "A", Some("a.md"), NodeKind::Note),
                node(3, "B", Some("b.md"), NodeKind::Note),
                node(4, "C", Some("c.md"), NodeKind::Note),
            ],
            vec![
                edge(1, 2, EdgeKind::Link),
                edge(2, 3, EdgeKind::Link),
                edge(3, 4, EdgeKind::Link),
            ],
        );
        let tree = connections_tree(&h, 1);
        let ids: Vec<&str> = tree.outgoing.iter().map(|r| r.id.as_str()).collect();
        assert_eq!(
            ids,
            vec![
                "out/p%3Aa%2Emd",
                "out/p%3Aa%2Emd/p%3Ab%2Emd",
                "out/p%3Aa%2Emd/p%3Ab%2Emd/p%3Ac%2Emd"
            ]
        );
        assert_eq!(tree.outgoing[2].level, 3);
    }

    /// A neighbour reached by BOTH a link and an embed is ONE row, not
    /// embed-only.
    #[test]
    fn tree_collapses_link_and_embed_to_one_row() {
        let h = hood(
            1,
            1,
            vec![
                node(1, "Center", Some("center.md"), NodeKind::Note),
                node(2, "Both", Some("both.md"), NodeKind::Note),
            ],
            vec![edge(1, 2, EdgeKind::Link), edge(1, 2, EdgeKind::Embed)],
        );
        let tree = connections_tree(&h, 1);
        assert_eq!(
            tree.outgoing.len(),
            1,
            "one row per neighbour, not per edge"
        );
        assert!(!tree.outgoing[0].embed_only, "a real link ⇒ not embed-only");
    }

    /// A self-edge is not a connection to another note.
    #[test]
    fn tree_omits_a_self_edge_from_both_lists() {
        let h = hood(
            1,
            1,
            vec![node(1, "Center", Some("center.md"), NodeKind::Note)],
            vec![edge(1, 1, EdgeKind::Link)],
        );
        let tree = connections_tree(&h, 1);
        assert!(tree.incoming.is_empty() && tree.outgoing.is_empty());
    }

    /// Each level is ordered by the label order: a diamond descendant is
    /// distinct under each parent (occurrence ids), and rows sort naturally.
    #[test]
    fn tree_orders_each_level_and_keeps_diamond_occurrences_distinct() {
        let h = hood(
            1,
            2,
            vec![
                node(1, "Center", Some("center.md"), NodeKind::Note),
                node(2, "beta", Some("beta.md"), NodeKind::Note),
                node(3, "Alpha", Some("alpha.md"), NodeKind::Note),
                node(4, "Shared", Some("shared.md"), NodeKind::Note),
            ],
            vec![
                edge(1, 2, EdgeKind::Link),
                edge(1, 3, EdgeKind::Link),
                edge(2, 4, EdgeKind::Link),
                edge(3, 4, EdgeKind::Link),
            ],
        );
        let tree = connections_tree(&h, 1);
        let ids: Vec<&str> = tree.outgoing.iter().map(|r| r.id.as_str()).collect();
        assert_eq!(
            ids,
            vec![
                "out/p%3Aalpha%2Emd",
                "out/p%3Aalpha%2Emd/p%3Ashared%2Emd",
                "out/p%3Abeta%2Emd",
                "out/p%3Abeta%2Emd/p%3Ashared%2Emd"
            ]
        );
    }

    // --- Row C: the stable key (0b-3) --------------------------------------

    #[test]
    fn stable_key_is_the_two_namespaces() {
        assert_eq!(
            stable_key(&NodeKey::Path("notes/Alpha.md".into())),
            "p:notes/Alpha.md"
        );
        assert_eq!(stable_key_for_path("notes/Alpha.md"), "p:notes/Alpha.md");
        assert_eq!(
            stable_key(&NodeKey::Ghost(ghost_key("Missing Note"))),
            "g:missing%20note"
        );
        // A real file literally named `g:X` keys as `p:g:X`, never the ghost.
        assert_eq!(stable_key(&NodeKey::Path("g:X".into())), "p:g:X");
        assert_eq!(stable_key(&NodeKey::Ghost(ghost_key("g:X"))), "g:g%3Ax");
        // The SOURCE is the normalised ghost key, not the label (0b-D1 i):
        // every authored variant is one node under one key.
        assert_eq!(stable_key(&NodeKey::Ghost(ghost_key("./Foo"))), "g:foo");
        assert_eq!(stable_key(&NodeKey::Ghost(ghost_key("/Bar"))), "g:bar");
        assert_eq!(
            stable_key(&NodeKey::Ghost(ghost_key("FOO"))),
            stable_key(&NodeKey::Ghost(ghost_key("Foo")))
        );
        // Unicode lowercase, not en_US_POSIX (0b-D1 ii).
        assert_eq!(
            stable_key(&NodeKey::Ghost(ghost_key("İstanbul"))),
            "g:i%CC%87stanbul"
        );
        // A payload node folds its label to the key the index stores.
        let ghost = node(7, "Missing Note", None, NodeKind::Ghost);
        assert_eq!(stable_key_of(&ghost), "g:missing%20note");
        let real = node(42, "Alpha", Some("notes/Alpha.md"), NodeKind::Note);
        assert_eq!(stable_key_of(&real), "p:notes/Alpha.md");
    }

    #[test]
    fn stable_key_percent_encodes_by_rust_alphanumerics() {
        // Letters outside ASCII pass through; combining marks encode —
        // so the two normalization forms stay byte-distinct (0b-D1 iii).
        assert_eq!(percent_encode_non_alphanumeric("café"), "café");
        assert_eq!(percent_encode_non_alphanumeric("cafe\u{301}"), "cafe%CC%81");
        assert_ne!(
            stable_key(&NodeKey::Ghost(ghost_key("caf\u{e9}"))),
            stable_key(&NodeKey::Ghost(ghost_key("cafe\u{301}")))
        );
        assert_eq!(percent_encode_non_alphanumeric("a b/c"), "a%20b%2Fc");
        assert_eq!(percent_encode_non_alphanumeric("p:hub.md"), "p%3Ahub%2Emd");
    }

    // --- Row E: the filter and the visible set (0b-6) ------------------------

    #[test]
    fn filter_ids_fold_case_and_marks() {
        let snap = snapshot(
            vec![
                node(1, "Café", Some("cafe.md"), NodeKind::Note),
                node(2, "alpha", Some("alpha.md"), NodeKind::Note),
                node(3, "Alpine", Some("alpine.md"), NodeKind::Note),
                node(4, "Missing", None, NodeKind::Ghost),
            ],
            vec![],
        );
        let ids = |q: &GraphVisibilityQuery| visibility(&snap, q).ids;
        assert_eq!(ids(&needle("cafe")), vec![1]);
        assert_eq!(ids(&needle(" ALP ")), vec![2, 3]);
        assert_eq!(ids(&needle("")), vec![1, 2, 3, 4]);
        assert_eq!(ids(&needle("   ")), vec![1, 2, 3, 4]);
        assert_eq!(
            ids(&needle("\ncafe\n")),
            vec![1],
            "Rust trim drops a newline (0b-D3)"
        );
        assert_eq!(
            ids(&needle("\u{3000}alp\u{3000}")),
            vec![2, 3],
            "and an ideographic space"
        );
        assert!(ids(&needle("zzz")).is_empty());
        assert!(label_matches("Straße", "stra"));
        assert!(
            !label_matches("Straße", "strasse"),
            "not a case fold (0b-D3)"
        );
        assert!(
            label_matches("İstanbul", "istanbul"),
            "the dotted I lowers to i + a mark, dropped"
        );
        // The kind overlay: the Unresolved preset.
        let ghosts = GraphVisibilityQuery {
            kind_only: Some(NodeKind::Ghost),
            ..all()
        };
        assert_eq!(ids(&ghosts), vec![4]);
        let v = visibility(&snap, &needle("alp"));
        assert_eq!(
            (v.generation, v.total),
            (1, 4),
            "total counts the backend-filtered set"
        );
        assert_eq!(
            v.labeled, v.ids,
            "under the cap every visible node is labeled"
        );
    }

    #[test]
    fn labeled_is_the_capped_priority_set() {
        let mut nodes: Vec<GraphNode> = (0..=LABEL_CAP)
            .map(|i| {
                let label = format!("n{i:03}");
                with_degrees(
                    node(
                        u64::from(i) + 1,
                        &label,
                        Some(&format!("{label}.md")),
                        NodeKind::Note,
                    ),
                    1,
                    0,
                    0,
                )
            })
            .collect();
        nodes[7].in_links = 5;
        let snap = snapshot(nodes, vec![]);
        let v = visibility(&snap, &all());
        assert_eq!(v.ids.len(), LABEL_CAP as usize + 1);
        assert_eq!(
            v.labeled.len(),
            LABEL_CAP as usize,
            "201 visible → 200 labeled"
        );
        assert_eq!(v.labeled[0], 8, "the hub ranks first");
        assert!(
            !v.labeled.contains(&(u64::from(LABEL_CAP) + 1)),
            "the last by label order loses the slot"
        );
        assert_eq!(v.labeled[1], 1, "ties by the label order, deterministic");
    }

    // --- Row D: neighbours (0b-6b) -----------------------------------------

    #[test]
    fn neighbors_are_unique_visible_and_ordered() {
        let snap = snapshot(
            vec![
                node(1, "a", Some("a.md"), NodeKind::Note),
                node(2, "b", Some("b.md"), NodeKind::Note),
                node(3, "c", Some("c.md"), NodeKind::Note),
                node(4, "d", Some("d.md"), NodeKind::Note),
            ],
            vec![
                edge(1, 2, EdgeKind::Link),
                edge(3, 1, EdgeKind::Embed),
                edge(1, 2, EdgeKind::Embed),
                edge(1, 4, EdgeKind::Link),
                edge(1, 1, EdgeKind::Link),
            ],
        );
        let ids = |visible: &[u64], id: u64| {
            neighbors(&snap, visible, id)
                .into_iter()
                .map(|n| n.id)
                .collect::<Vec<_>>()
        };
        assert_eq!(
            ids(&[1, 2, 3, 4], 1),
            vec![2, 3, 4],
            "edge order, Link+Embed once"
        );
        assert_eq!(
            ids(&[1, 2, 4], 1),
            vec![2, 4],
            "a hidden neighbour never surfaces"
        );
        assert!(ids(&[1], 1).is_empty(), "a self-edge is not a neighbour");
        assert!(
            ids(&[2, 3, 4], 1).is_empty(),
            "a hidden centre has no neighbours"
        );
        assert!(ids(&[1, 2], 99).is_empty(), "an unknown centre has none");
        let first = &neighbors(&snap, &[1, 2, 3, 4], 1)[0];
        assert_eq!(
            (first.stable_key.as_str(), first.label.as_str()),
            ("p:b.md", "b")
        );
    }

    // --- The topology (0b-6b, design B) ----------------------------------------

    #[test]
    fn topology_is_the_visible_nodes_with_their_neighbours() {
        use crate::graph_config::{GraphColorToken, GraphConfig, GraphGroup, GraphRingStyle};
        let snap = snapshot(
            vec![
                with_degrees(node(1, "hub", Some("hub.md"), NodeKind::Note), 3, 1, 0),
                node(2, "beta", Some("beta.md"), NodeKind::Note),
                node(3, "café", Some("cafe.md"), NodeKind::Note),
                node(4, "Missing", None, NodeKind::Ghost),
            ],
            vec![
                edge(2, 1, EdgeKind::Link),
                edge(1, 3, EdgeKind::Link),
                edge(1, 3, EdgeKind::Embed),
                edge(1, 4, EdgeKind::Link),
                edge(1, 1, EdgeKind::Link),
            ],
        );
        let config = GraphConfig {
            groups: vec![
                GraphGroup {
                    query: "caf".into(),
                    color_token: GraphColorToken::Red,
                    ring_style: GraphRingStyle::Solid,
                },
                GraphGroup {
                    query: "b".into(),
                    color_token: GraphColorToken::Blue,
                    ring_style: GraphRingStyle::Dashed,
                },
            ],
            ..GraphConfig::default()
        };
        let q = all();
        let t = topology(&snap, &q, &config);
        let v = visibility(&snap, &q);
        assert_eq!((t.generation, t.total), (v.generation, v.total));
        assert_eq!(
            t.nodes.iter().map(|n| n.id).collect::<Vec<_>>(),
            v.ids,
            "the visible nodes in order"
        );
        for n in &t.nodes {
            assert_eq!(n.labeled, v.labeled.contains(&n.id));
            assert_eq!(
                n.diameter,
                node_diameter(n.in_links),
                "core's curve, unscaled"
            );
            assert_eq!(
                n.group,
                crate::graph_config::matching_group(&config, &n.label).map(|i| i as u32)
            );
            assert_eq!(
                n.neighbors,
                neighbors(&snap, &v.ids, n.id),
                "each list equals the single-node query's"
            );
            assert_eq!(
                n.stable_key,
                stable_key_of(snap.nodes.iter().find(|x| x.id == n.id).unwrap())
            );
        }
        let hub = &t.nodes[0];
        assert_eq!(
            hub.neighbors.iter().map(|n| n.id).collect::<Vec<_>>(),
            vec![2, 3, 4],
            "edge order, Link+Embed once, no self"
        );
        assert_eq!(
            (hub.group, hub.in_links),
            (Some(1), 3),
            "`hub` matches `b` — first match wins over the ordered groups"
        );
        assert_eq!(
            (
                hub.path.as_deref(),
                hub.out_links,
                hub.component,
                hub.is_orphan
            ),
            (Some("hub.md"), 1, 0, false),
            "the payload the row copy, Where-am-I and the actions read"
        );
        assert_eq!(t.nodes[2].group, Some(0));
        // Under a needle the hidden nodes vanish from every list.
        let t = topology(&snap, &needle("a"), &config);
        assert_eq!(
            t.nodes.iter().map(|n| n.label.as_str()).collect::<Vec<_>>(),
            vec!["beta", "café"]
        );
        assert!(
            t.nodes.iter().all(|n| n.neighbors.is_empty()),
            "the hub is hidden, so nobody has a visible neighbour"
        );
        assert!(t.edges.is_empty(), "no edge has both endpoints visible");
    }

    /// The edges are exactly the snapshot's edges among the visible ids,
    /// in the snapshot's order, the self-edge kept (0b-6b, 0b-D9).
    #[test]
    fn topology_carries_the_visible_edges() {
        let snap = snapshot(
            vec![
                node(1, "hub", Some("hub.md"), NodeKind::Note),
                node(2, "beta", Some("beta.md"), NodeKind::Note),
                node(3, "Missing", None, NodeKind::Ghost),
            ],
            vec![
                edge(2, 1, EdgeKind::Link),
                edge(1, 1, EdgeKind::Link),
                edge(1, 3, EdgeKind::Embed),
                edge(1, 2, EdgeKind::Embed),
            ],
        );
        let config = crate::graph_config::GraphConfig::default();
        let t = topology(&snap, &all(), &config);
        assert_eq!(
            t.edges, snap.edges,
            "everything is visible: every edge, in order, the self-edge included"
        );
        assert!(
            t.nodes[0].neighbors.iter().all(|n| n.id != 1),
            "the self-edge is an edge, not a neighbour"
        );
        let q = GraphVisibilityQuery {
            kind_only: Some(NodeKind::Note),
            ..all()
        };
        let t = topology(&snap, &q, &config);
        assert_eq!(
            t.edges
                .iter()
                .map(|e| (e.source_id, e.target_id))
                .collect::<Vec<_>>(),
            vec![(2, 1), (1, 1), (1, 2)],
            "the ghost's edge drops with the ghost"
        );
    }

    // --- Row G: order and rows (0b-5, 0b-7) — GraphTableViewTests as facts --

    #[test]
    fn label_order_is_natural_folded_and_total() {
        let cmp = |a: &str, b: &str| label_order((a, "", 0), (b, "", 0));
        assert_eq!(cmp("alpha", "Beta"), Ordering::Less);
        assert_eq!(
            cmp("Édith", "zoo"),
            Ordering::Less,
            "the fold drops the mark"
        );
        assert_eq!(
            cmp("Ångström", "angstrom"),
            Ordering::Greater,
            "equal folds fall through to the bytes"
        );
        assert_eq!(
            cmp("note 2", "note 10"),
            Ordering::Less,
            "digit runs compare by value"
        );
        assert_eq!(
            cmp("note 10", "note 010"),
            Ordering::Less,
            "fewer leading zeros first"
        );
        assert_eq!(cmp("2", "02"), Ordering::Less);
        assert_eq!(cmp("02", "002"), Ordering::Less);
        assert_eq!(cmp("0", "00"), Ordering::Less, "all-zero runs");
        assert_eq!(cmp("a1", "a01"), Ordering::Less);
        assert_eq!(
            cmp("a1", "ab"),
            Ordering::Less,
            "a digit run before a non-digit run"
        );
        assert_eq!(
            cmp("a", "a1"),
            Ordering::Less,
            "the shorter segment list first"
        );
        assert_eq!(
            cmp("a", "a0"),
            Ordering::Less,
            "the exhausted prefix sorts first"
        );
        assert_eq!(cmp("1", "1a"), Ordering::Less);
        assert_eq!(cmp("a", "ab"), Ordering::Less);
        let long = format!("1{}", "0".repeat(1000));
        let nines = "9".repeat(999);
        assert_eq!(
            cmp(&long, &nines),
            Ordering::Greater,
            "a thousand-digit run, no parse"
        );
        assert_eq!(
            cmp("a", "A"),
            Ordering::Greater,
            "equal folds fall through to the bytes"
        );
        assert_eq!(
            cmp("٣", "3"),
            Ordering::Greater,
            "a non-ASCII digit is an ordinary character"
        );
        // Same label, distinct keys: the key breaks the tie both ways.
        assert_eq!(
            label_order(("Note", "p:a/Note.md", 1), ("Note", "p:b/Note.md", 2)),
            Ordering::Less
        );
        assert_eq!(
            label_order(("Note", "p:b/Note.md", 2), ("Note", "p:a/Note.md", 1)),
            Ordering::Greater
        );
        // Same label and key: the id.
        assert_eq!(label_order(("x", "k", 1), ("x", "k", 2)), Ordering::Less);
    }

    fn rows_with_in_links(pairs: &[(u64, &str, &str, u32)]) -> GraphSnapshot {
        snapshot(
            pairs
                .iter()
                .map(|(id, label, path, in_links)| {
                    with_degrees(
                        node(*id, label, Some(path), NodeKind::Note),
                        *in_links,
                        0,
                        0,
                    )
                })
                .collect(),
            vec![],
        )
    }

    #[test]
    fn table_rows_sort_is_the_mac_comparator() {
        let snap = rows_with_in_links(&[
            (1, "Beta", "b.md", 5),
            (2, "Alpha", "a.md", 5),
            (3, "Gamma", "g.md", 9),
        ]);
        let labels =
            |rows: Vec<GraphTableRow>| rows.into_iter().map(|r| r.label).collect::<Vec<_>>();
        // Descending (the default): 9 first, then the two 5s by label.
        assert_eq!(
            labels(table_rows(&snap, &all(), GraphTableSort::default())),
            vec!["Gamma", "Alpha", "Beta"]
        );
        // Ascending flips the number and keeps the label tie-break.
        let asc = GraphTableSort {
            column: GraphTableColumn::LinksIn,
            ascending: true,
        };
        assert_eq!(
            labels(table_rows(&snap, &all(), asc)),
            vec!["Alpha", "Beta", "Gamma"]
        );
        // The visible set is the query's.
        assert_eq!(
            labels(table_rows(&snap, &needle("a"), GraphTableSort::default())),
            vec!["Gamma", "Alpha", "Beta"]
        );
        assert_eq!(
            labels(table_rows(&snap, &needle("gam"), GraphTableSort::default())),
            vec!["Gamma"]
        );
        // Same label, distinct paths: a strict total order, the key decides.
        let snap = rows_with_in_links(&[(1, "Note", "a/Note.md", 2), (2, "Note", "b/Note.md", 2)]);
        let rows = table_rows(&snap, &all(), GraphTableSort::default());
        assert_eq!(rows[0].stable_key, "p:a/Note.md");
        assert_eq!(
            compare_rows(GraphTableSort::default(), &rows[0], &rows[1]),
            Ordering::Less
        );
        assert_eq!(
            compare_rows(GraphTableSort::default(), &rows[1], &rows[0]),
            Ordering::Greater
        );
        // The Note column reverses the label order when descending.
        let by_note_desc = GraphTableSort {
            column: GraphTableColumn::Note,
            ascending: false,
        };
        let snap = rows_with_in_links(&[(1, "Beta", "b.md", 0), (2, "Alpha", "a.md", 0)]);
        assert_eq!(
            labels(table_rows(&snap, &all(), by_note_desc)),
            vec!["Beta", "Alpha"]
        );
        // The Modified column orders a missing value lowest.
        let mut snap = rows_with_in_links(&[(1, "Old", "o.md", 0), (2, "Ghostly", "g.md", 0)]);
        snap.nodes[0].modified_ms = Some(1_000);
        snap.nodes[1].modified_ms = None;
        let by_modified_asc = GraphTableSort {
            column: GraphTableColumn::Modified,
            ascending: true,
        };
        assert_eq!(
            labels(table_rows(&snap, &all(), by_modified_asc)),
            vec!["Ghostly", "Old"]
        );
        // Folder orders by the fold, then the raw folder bytes, then falls
        // through to the label (0b-D2): `A` and `a` fold equal and the bytes
        // put `A` first; a different fold (`b`) comes after both.
        let snap = rows_with_in_links(&[
            (1, "z", "b/z.md", 0),
            (2, "y", "A/y.md", 0),
            (3, "x", "a/x.md", 0),
            (4, "w", "a/w.md", 0),
        ]);
        let by_folder = GraphTableSort {
            column: GraphTableColumn::Folder,
            ascending: true,
        };
        assert_eq!(
            labels(table_rows(&snap, &all(), by_folder)),
            vec!["y", "w", "x", "z"]
        );
        let by_folder_desc = GraphTableSort {
            column: GraphTableColumn::Folder,
            ascending: false,
        };
        assert_eq!(
            labels(table_rows(&snap, &all(), by_folder_desc)),
            vec!["z", "w", "x", "y"],
            "the fold and the bytes reverse; the label tie stays ascending"
        );
        // Kind orders the labels bytewise: Attachment < Note < Unresolved.
        let mut snap = rows_with_in_links(&[(1, "n", "n.md", 0), (2, "a.png", "a.png", 0)]);
        snap.nodes[1].kind = NodeKind::Attachment;
        snap.nodes.push(node(3, "g", None, NodeKind::Ghost));
        let by_kind = GraphTableSort {
            column: GraphTableColumn::Kind,
            ascending: true,
        };
        assert_eq!(
            labels(table_rows(&snap, &all(), by_kind)),
            vec!["a.png", "n", "g"]
        );
    }

    #[test]
    fn table_cells_are_nine_and_formatted() {
        let mut n = with_degrees(
            node(1, "Alpha", Some("notes/Alpha.md"), NodeKind::Note),
            3,
            2,
            1,
        );
        n.component = 4;
        n.modified_ms = Some(0);
        let rows = table_rows(
            &snapshot(vec![n], vec![]),
            &all(),
            GraphTableSort::default(),
        );
        let row = &rows[0];
        assert_eq!(
            (row.links_in, row.links_out, row.embeds_in, row.component),
            (3, 2, 1, 4)
        );
        assert_eq!(
            row.cells,
            vec![
                "Alpha",
                "3",
                "2",
                "1",
                "0",
                "4",
                "1970-01-01 00:00",
                "notes",
                "Note"
            ]
        );
        assert_eq!(row.cells[GraphTableColumn::Folder.index()], "notes");
        let ghost = table_rows(
            &snapshot(vec![node(2, "Missing", None, NodeKind::Ghost)], vec![]),
            &all(),
            GraphTableSort::default(),
        );
        assert_eq!(ghost[0].cells[GraphTableColumn::Kind.index()], "Unresolved");
        assert_eq!(
            ghost[0].cells[GraphTableColumn::Modified.index()],
            "",
            "ghosts have no modified date"
        );
        assert_eq!(ghost[0].cells[GraphTableColumn::Folder.index()], "");
        assert_eq!(ghost[0].cells.len(), 9);
        assert_eq!(kind_label(NodeKind::Attachment), "Attachment");
        assert_eq!(folder_of("root.md"), "");
        assert_eq!(folder_of("a/b/c.md"), "a/b");
    }

    #[test]
    fn table_columns_are_the_ordered_specs() {
        let specs = table_columns();
        assert_eq!(
            specs.iter().map(|s| s.header.as_str()).collect::<Vec<_>>(),
            vec![
                "Note",
                "Links in",
                "Links out",
                "Embeds in",
                "Embeds out",
                "Component",
                "Modified",
                "Folder",
                "Kind"
            ]
        );
        assert_eq!(
            specs.iter().map(|s| s.column).collect::<Vec<_>>(),
            GraphTableColumn::ALL.to_vec()
        );
        // The vector index IS the sort index and the cell index.
        for (i, spec) in specs.iter().enumerate() {
            assert_eq!(spec.column.index(), i);
        }
        let row = table_rows(
            &snapshot(
                vec![with_degrees(
                    node(1, "Alpha", Some("n/Alpha.md"), NodeKind::Note),
                    2,
                    0,
                    0,
                )],
                vec![],
            ),
            &all(),
            GraphTableSort::default(),
        )
        .remove(0);
        assert_eq!(row.cells[specs[1].column.index()], "2");
        assert_eq!(row.cells[specs[7].column.index()], "n");
    }

    #[test]
    fn modified_text_is_utc_minutes() {
        // 1_788_802_944_000 ms is 2026-09-07T17:42:24Z: the minute, in UTC,
        // no seconds, no zone, no locale.
        assert_eq!(modified_text(Some(1_788_802_944_000)), "2026-09-07 17:42");
        assert_eq!(
            modified_text(Some(86_400_000 + 60_000 * 61)),
            "1970-01-02 01:01"
        );
        assert_eq!(modified_text(None), "");
    }

    // --- Row H: constants (0b-8) -------------------------------------------

    #[test]
    fn graph_constants_are_the_named_constants() {
        let c = constants();
        assert_eq!(c.tier_b_threshold, TIER_B_THRESHOLD);
        assert_eq!(c.label_cap, LABEL_CAP);
        assert_eq!(
            (c.connections_depth_min, c.connections_depth_max),
            (CONNECTIONS_DEPTH_MIN, CONNECTIONS_DEPTH_MAX)
        );
        assert_eq!(
            (c.node_diameter_min, c.node_diameter_max),
            (NODE_DIAMETER_MIN, NODE_DIAMETER_MAX)
        );
        assert_eq!(c.neighbor_label_cap as usize, GRAPH_NEIGHBOR_LABEL_CAP);
        assert_eq!((TIER_B_THRESHOLD, LABEL_CAP), (1500, 200));
        assert_eq!((CONNECTIONS_DEPTH_MIN, CONNECTIONS_DEPTH_MAX), (1, 3));
        assert_eq!(
            (
                clamp_depth(0),
                clamp_depth(1),
                clamp_depth(3),
                clamp_depth(99)
            ),
            (1, 1, 3, 3)
        );
    }

    #[test]
    fn node_diameter_is_the_spec_curve_clamped() {
        assert_eq!(node_diameter(0), 8.0);
        let d5 = node_diameter(5);
        assert!((d5 - (8.0 + 6.0 * (6.0f64).ln())).abs() < 1e-9);
        assert_eq!(node_diameter(1_000_000), 28.0);
        assert!(node_diameter(5) > node_diameter(1));
    }

    #[test]
    fn tier_boundary_is_inclusive_at_1500() {
        let c = constants();
        let visible_a: u32 = 1500;
        let visible_b: u32 = 1501;
        assert!(visible_a <= c.tier_b_threshold, "1500 is tier A");
        assert!(visible_b > c.tier_b_threshold, "1501 is tier B");
    }

    // --- Row I: actions (0b-9) ---------------------------------------------

    #[test]
    fn row_actions_by_kind_are_the_parity_set() {
        let titles = |kind| {
            row_actions(kind)
                .into_iter()
                .map(|s| s.title)
                .collect::<Vec<_>>()
        };
        let nav = vec![
            "Open",
            "Open in New Tab",
            "Show connections",
            "Reveal in File Tree",
        ];
        assert_eq!(titles(NodeKind::Note), nav);
        assert_eq!(titles(NodeKind::Attachment), nav);
        assert_eq!(titles(NodeKind::Ghost), vec!["Create note"]);
        assert_eq!(GraphRowAction::ALL.len(), 5);
        // The vectors are the arms in declaration order (design B): the
        // union over the kinds is every action, each title its own.
        let mut union: Vec<GraphRowAction> = row_actions(NodeKind::Note)
            .into_iter()
            .map(|s| s.action)
            .collect();
        union.extend(row_actions(NodeKind::Ghost).into_iter().map(|s| s.action));
        assert_eq!(union, GraphRowAction::ALL.to_vec());
        for spec in row_actions(NodeKind::Note)
            .into_iter()
            .chain(row_actions(NodeKind::Ghost))
        {
            assert_eq!(spec.title, row_action_title(spec.action));
        }
    }

    // --- Row J: the steps (0b-10) ------------------------------------------

    /// The mac fixed-layout case: a at the origin, b right, c below, d far
    /// right; a's neighbours are b and c; d is an orphan.
    #[test]
    fn spatial_step_prefers_neighbours_then_falls_back() {
        let points = [
            GraphPoint {
                id: 1,
                x: 0.0,
                y: 0.0,
            },
            GraphPoint {
                id: 2,
                x: 100.0,
                y: 0.0,
            },
            GraphPoint {
                id: 3,
                x: 0.0,
                y: 100.0,
            },
            GraphPoint {
                id: 4,
                x: 300.0,
                y: 0.0,
            },
        ];
        assert_eq!(
            spatial_step(&points, &[2, 3], 1, 1.0, 0.0),
            Some(2),
            "→ picks the right-hand neighbour"
        );
        assert_eq!(
            spatial_step(&points, &[2, 3], 1, 0.0, 1.0),
            Some(3),
            "↓ picks the below neighbour"
        );
        assert_eq!(
            spatial_step(&points, &[], 4, -1.0, 0.0),
            Some(2),
            "fallback: the nearest in-direction node"
        );
        assert_eq!(
            spatial_step(&points, &[], 1, -1.0, 0.0),
            None,
            "nothing to the left of the origin"
        );
        assert_eq!(
            spatial_step(&points, &[2, 3], 1, 5.0, 0.0),
            Some(2),
            "any non-zero vector is a direction"
        );
        // Ties break to the lower id; the cosine threshold excludes a
        // candidate barely off-axis at a far distance relative to a nearer one.
        let tie = [
            GraphPoint {
                id: 9,
                x: 0.0,
                y: 0.0,
            },
            GraphPoint {
                id: 5,
                x: 10.0,
                y: 0.0,
            },
            GraphPoint {
                id: 4,
                x: 10.0,
                y: 0.0,
            },
        ];
        assert_eq!(spatial_step(&tie, &[], 9, 1.0, 0.0), Some(4));
    }

    #[test]
    fn spatial_step_is_total_over_bad_input() {
        let points = [
            GraphPoint {
                id: 1,
                x: 0.0,
                y: 0.0,
            },
            GraphPoint {
                id: 2,
                x: f64::NAN,
                y: 0.0,
            },
            GraphPoint {
                id: 3,
                x: 10.0,
                y: 0.0,
            },
            GraphPoint {
                id: 3,
                x: -10.0,
                y: 0.0,
            },
        ];
        assert_eq!(
            spatial_step(&points, &[], 1, 1.0, 0.0),
            Some(3),
            "a non-finite point is ignored"
        );
        assert_eq!(
            spatial_step(&points, &[], 1, -1.0, 0.0),
            None,
            "a duplicate id keeps its first point"
        );
        assert_eq!(
            spatial_step(&points, &[], 99, 1.0, 0.0),
            None,
            "an unknown origin steps nowhere"
        );
        assert_eq!(
            spatial_step(&points, &[], 1, 0.0, 0.0),
            None,
            "a zero direction steps nowhere"
        );
        assert_eq!(
            spatial_step(&points, &[], 1, f64::INFINITY, 0.0),
            None,
            "a non-finite direction steps nowhere"
        );
        assert_eq!(
            spatial_step(&points, &[], 2, 1.0, 0.0),
            None,
            "a non-finite origin has no point"
        );
    }

    #[test]
    fn structural_step_wraps() {
        let ids = [3, 1, 2];
        assert_eq!(structural_step(&ids, Some(3), true), Some(1));
        assert_eq!(
            structural_step(&ids, Some(2), true),
            Some(3),
            "wraps forward"
        );
        assert_eq!(
            structural_step(&ids, Some(3), false),
            Some(2),
            "wraps backward"
        );
        assert_eq!(structural_step(&ids, None, true), Some(3));
        assert_eq!(structural_step(&ids, None, false), Some(2));
        assert_eq!(
            structural_step(&ids, Some(42), true),
            Some(3),
            "an unlisted origin starts over"
        );
        assert_eq!(structural_step(&[], Some(1), true), None);
        assert_eq!(
            structural_step(&[1, 2, 1, 3], Some(1), true),
            Some(2),
            "a duplicate resolves to its first index"
        );
    }

    // --- Row K: the ghost path (0b-11) -------------------------------------

    #[test]
    fn ghost_note_path_honours_folder_and_extension() {
        assert_eq!(ghost_note_path("Missing Note"), "Missing Note.md");
        assert_eq!(ghost_note_path("  Missing Note  "), "Missing Note.md");
        assert_eq!(ghost_note_path("notes/Foo"), "notes/Foo.md");
        assert_eq!(ghost_note_path("./notes/Foo"), "notes/Foo.md");
        assert_eq!(ghost_note_path("/Foo"), "Foo.md");
        assert_eq!(ghost_note_path("Foo.md"), "Foo.md");
        assert_eq!(ghost_note_path("Foo.MARKDOWN"), "Foo.MARKDOWN");
        assert_eq!(ghost_note_path("notes/Foo.MD"), "notes/Foo.MD");
        assert_eq!(
            ghost_note_path("a.b/Foo"),
            "a.b/Foo.md",
            "a dotted folder is not an extension"
        );
        assert_eq!(ghost_note_path("./a/b"), "a/b.md");
        assert_eq!(ghost_note_path("././a"), "./a.md", "one prefix stripped");
        assert_eq!(ghost_note_path("dir/"), "dir/.md");
        assert_eq!(
            ghost_note_path(".md"),
            ".md.md",
            "a dotfile has no extension"
        );
        assert_eq!(
            ghost_note_path("a\\b"),
            "a\\b.md",
            "a backslash is an ordinary character"
        );
        assert_eq!(ghost_note_path(""), ".md");
        assert_eq!(ghost_note_path("\nFoo\n"), "Foo.md", "Rust trim (0b-D3)");
    }

    // --- 0b-15: the surface list names real functions ------------------------

    #[test]
    fn graph_query_surface_names_pub_fns() {
        let queries = include_str!("graph_queries.rs");
        let config = include_str!("graph_config.rs");
        let session = include_str!("session.rs");
        for name in GRAPH_QUERY_SURFACE {
            let short = name.strip_prefix("graph_").unwrap();
            let config_short = name.strip_prefix("graph_config_").unwrap_or(short);
            let found = session.contains(&format!("pub fn {name}("))
                || queries.contains(&format!("pub fn {short}("))
                || config.contains(&format!("pub fn {config_short}("))
                || config.contains(&format!("pub fn {short}("));
            assert!(
                found,
                "{name} names no pub fn in graph_queries, graph_config or the session"
            );
        }
        assert_eq!(GRAPH_QUERY_SURFACE.len(), 24);
    }

    // --- W6-2 PR A, A-5 / AD-1: the default sort is fetched ------------------

    #[test]
    fn table_default_sort_is_the_default() {
        assert_eq!(table_default_sort(), GraphTableSort::default());
        assert_eq!(table_default_sort().column, GraphTableColumn::LinksIn);
        assert!(!table_default_sort().ascending, "hubs first (spec §P1-2)");
        assert!(
            GRAPH_QUERY_SURFACE.contains(&"graph_table_default_sort"),
            "the surface names it"
        );
    }
}
