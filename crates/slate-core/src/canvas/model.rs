// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Canvas model derivation — Milestone T, Wave 1 (#360).
//!
//! Turns a parsed [`Canvas`] into the deterministic, navigable
//! [`CanvasModel`] every accessible surface reads: the structured
//! equivalent of the 2D plane (locked principle 05 §5.2). This is the
//! core accessibility-enabling step — outline, table, navigator,
//! renderer, Voice Control, and the announcement coordinator all agree
//! because they all read one derivation.
//!
//! ## Normative rules (t1 spec — census-gated)
//!
//! 1. **Containment is by node center point**, strictly inside the group
//!    rect; a center on the boundary is *not* contained. A node whose
//!    center lies in several groups belongs to the **smallest-area**
//!    group; equal areas → the **later group in document order**. Nested
//!    groups form the tree by the same rule group-to-group, with one
//!    cycle-safety refinement: a group's parent must be strictly greater
//!    in the `(area, document order)` total order, so mutually
//!    containing groups (possible with overlapping rects) can never
//!    parent each other both ways.
//! 2. **Reading order** is a depth-first walk of the group tree (a group
//!    precedes its children); within a container, siblings sort by
//!    `(y, x, document order)` — document order is the final total-order
//!    tiebreak, so coincident and degenerate geometry stays
//!    deterministic.
//! 3. Zero-size, negative-coordinate, and negative-size nodes are legal
//!    inputs. Rects are normalized to min/max corners for geometry
//!    queries; a rect with no interior contains nothing.
//! 4. `derive(parse(s))` is a pure function of `s`.
//!
//! ## Title derivation (t0 §1.1 — backend-owned)
//!
//! `CardSummary.display_title` is the one string all surfaces (and
//! Voice Control) share. Frontmatter-based note titles are injected via
//! [`FileTitleSource`] so this module stays pure; the session layer
//! (#361) passes its note index, tests pass [`NoFileTitles`].

use std::collections::HashMap;

use super::{Canvas, Edge, EdgeId, EndStyle, Node, NodeId, NodeKind, Side, color_name};

/// Axis-aligned rectangle normalized to min/max corners.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Rect {
    pub x0: f64,
    pub y0: f64,
    pub x1: f64,
    pub y1: f64,
}

impl Rect {
    pub fn from_node(node: &Node) -> Rect {
        Rect::new(node.x, node.y, node.width, node.height)
    }

    /// Build from origin + size, normalizing negative sizes.
    pub fn new(x: f64, y: f64, width: f64, height: f64) -> Rect {
        let (x0, x1) = if width < 0.0 {
            (x + width, x)
        } else {
            (x, x + width)
        };
        let (y0, y1) = if height < 0.0 {
            (y + height, y)
        } else {
            (y, y + height)
        };
        Rect { x0, y0, x1, y1 }
    }

    pub fn width(&self) -> f64 {
        self.x1 - self.x0
    }

    pub fn height(&self) -> f64 {
        self.y1 - self.y0
    }

    pub fn area(&self) -> f64 {
        self.width() * self.height()
    }

    /// Total-order comparison of two rects' areas, robust at the f64
    /// extremes (red-team findings on #360):
    ///
    /// - Areas whose product overflows to `inf` are compared in log
    ///   space, so a 4e320 area still ranks below a 4e400 one instead
    ///   of tying and falling through to the document-order tiebreak.
    /// - A NaN area (constructible from an `inf × 0` degenerate rect)
    ///   ranks as `+inf`: such a rect has no strict interior, contains
    ///   nothing, and deterministically loses every smallest-area
    ///   contest.
    pub fn area_cmp(a: &Rect, b: &Rect) -> std::cmp::Ordering {
        let key = |r: &Rect| {
            let area = r.area();
            if area.is_nan() { f64::INFINITY } else { area }
        };
        let (ka, kb) = (key(a), key(b));
        if ka == f64::INFINITY && kb == f64::INFINITY {
            let log = |r: &Rect| r.width().log2() + r.height().log2();
            return log(a).total_cmp(&log(b));
        }
        ka.total_cmp(&kb)
    }

    /// Midpoint computed as `x0/2 + x1/2` so coordinates near the f64
    /// limit don't overflow to `inf` (red-team finding: `(x0 + x1)/2`
    /// broke containment for rects around ±1.7e308).
    pub fn center(&self) -> (f64, f64) {
        (self.x0 / 2.0 + self.x1 / 2.0, self.y0 / 2.0 + self.y1 / 2.0)
    }

    /// Strict interior containment: a point on the boundary is outside
    /// (rule 1). A degenerate rect has no interior.
    pub fn contains_point_strict(&self, x: f64, y: f64) -> bool {
        self.x0 < x && x < self.x1 && self.y0 < y && y < self.y1
    }

    /// Positive-area intersection: rects that merely touch don't overlap.
    pub fn overlaps(&self, other: &Rect) -> bool {
        self.x0 < other.x1 && other.x0 < self.x1 && self.y0 < other.y1 && other.y0 < self.y1
    }
}

/// Group containment resolved into a forest.
#[derive(Debug, Clone, PartialEq, Default)]
pub struct GroupTree {
    /// Nodes with no containing group, in sibling reading order.
    pub roots: Vec<NodeId>,
    /// Group → ordered children (cards and sub-groups, sibling order).
    pub children: HashMap<NodeId, Vec<NodeId>>,
    /// Node → containing group (absent for roots).
    pub parent: HashMap<NodeId, NodeId>,
}

/// Direction of a connection as seen from one of its endpoints, derived
/// from `fromEnd`/`toEnd` (t0 §1.2 phrases through this: "Connects to" /
/// "Connected from" / "Linked with").
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum EdgeDirection {
    Outgoing,
    Incoming,
    /// Arrows on both ends.
    Bidirectional,
    /// No arrows at all.
    Undirected,
}

/// One entry in a node's adjacency list. Exposes the raw directional
/// data (ends + which endpoint we are) so the announcement layer (#518)
/// can phrase without re-deriving geometry or direction.
#[derive(Debug, Clone, PartialEq)]
pub struct Neighbor {
    pub edge: EdgeId,
    pub other: NodeId,
    /// Direction from the owning node's perspective.
    pub direction: EdgeDirection,
    /// Attachment side on the owning node.
    pub self_side: Option<Side>,
    /// Attachment side on the other node.
    pub other_side: Option<Side>,
    /// True when the owning node is the edge's `fromNode`.
    pub self_is_from: bool,
    pub from_end: EndStyle,
    pub to_end: EndStyle,
    pub label: Option<String>,
}

pub type AdjacencyMap = HashMap<NodeId, Vec<Neighbor>>;

/// The per-card digest every surface shares (t0 §1.1/§1.2 data).
#[derive(Debug, Clone, PartialEq)]
pub struct CardSummary {
    /// Type word for announcements: "text" | "file" | "image" | "link"
    /// | "group" (image = file card with an image extension).
    pub kind_label: &'static str,
    /// The one display/speakable title (t0 §1.1 derivation).
    pub display_title: String,
    /// Ancestor group titles, root → immediate parent.
    pub group_path: Vec<String>,
    /// Immediate containing group, if any.
    pub container: Option<NodeId>,
    /// 1-based position among siblings ("n of m in ⟨group‖canvas⟩").
    pub position_in_container: usize,
    /// Sibling count in the container (m).
    pub container_size: usize,
    /// Distinct connections touching this node (dangling excluded).
    pub connection_count: usize,
    /// Connections pointing in (incoming + bidirectional + undirected).
    pub in_count: usize,
    /// Connections pointing out (outgoing + bidirectional + undirected).
    pub out_count: usize,
    /// Pinned color name (t0 §1.1), when the node has a color.
    pub color_name: Option<String>,
}

/// Spatial queries over the canvas: overlap checks for placement (#517)
/// and move/resize warnings (#521). Linear scan — 2,000 nodes is a few
/// microseconds per query, far below the §K budget; swap in a grid if a
/// profile ever says otherwise.
#[derive(Debug, Clone, PartialEq, Default)]
pub struct SpatialIndex {
    entries: Vec<SpatialEntry>,
}

#[derive(Debug, Clone, PartialEq)]
struct SpatialEntry {
    id: NodeId,
    rect: Rect,
    is_group: bool,
}

impl SpatialIndex {
    /// Node ids whose rect has positive-area overlap with `rect`, in
    /// document order. `include_groups: false` skips group frames (a new
    /// card *inside* a group area is not an overlap — that's how cards
    /// are placed into groups).
    pub fn overlapping(&self, rect: Rect, exclude: &[NodeId], include_groups: bool) -> Vec<NodeId> {
        self.entries
            .iter()
            .filter(|e| (include_groups || !e.is_group) && !exclude.contains(&e.id))
            .filter(|e| e.rect.overlaps(&rect))
            .map(|e| e.id.clone())
            .collect()
    }

    pub fn any_overlap(&self, rect: Rect, exclude: &[NodeId], include_groups: bool) -> bool {
        self.entries
            .iter()
            .filter(|e| (include_groups || !e.is_group) && !exclude.contains(&e.id))
            .any(|e| e.rect.overlaps(&rect))
    }

    /// Rect of one node, if present.
    pub fn rect_of(&self, id: &NodeId) -> Option<Rect> {
        self.entries.iter().find(|e| e.id == *id).map(|e| e.rect)
    }

    /// True when the canvas has no nodes at all.
    pub fn is_empty(&self) -> bool {
        self.entries.is_empty()
    }

    /// Bounding box of all nodes (None for an empty canvas).
    pub fn bounds(&self) -> Option<Rect> {
        let mut it = self.entries.iter();
        let first = it.next()?.rect;
        Some(it.fold(first, |acc, e| Rect {
            x0: acc.x0.min(e.rect.x0),
            y0: acc.y0.min(e.rect.y0),
            x1: acc.x1.max(e.rect.x1),
            y1: acc.y1.max(e.rect.y1),
        }))
    }
}

/// The derived model — what every surface reads (t1 shared architecture).
#[derive(Debug, Clone, PartialEq, Default)]
pub struct CanvasModel {
    pub tree: GroupTree,
    /// Every node (groups included) exactly once; groups precede their
    /// children.
    pub reading_order: Vec<NodeId>,
    pub adjacency: AdjacencyMap,
    pub summaries: HashMap<NodeId, CardSummary>,
    pub spatial: SpatialIndex,
}

/// Source of note titles / media alt text for `file` cards (frontmatter
/// `title`, image alt). The session layer passes its note index; the
/// pure fallback is the humanized filename — never a raw path.
pub trait FileTitleSource {
    fn title_for(&self, vault_path: &str) -> Option<String>;
}

/// No external titles: filename-derived titles only (tests, pure use).
pub struct NoFileTitles;

impl FileTitleSource for NoFileTitles {
    fn title_for(&self, _vault_path: &str) -> Option<String> {
        None
    }
}

/// Derive the model with filename-derived file-card titles.
pub fn derive(canvas: &Canvas) -> CanvasModel {
    derive_with(canvas, &NoFileTitles)
}

/// Derive the model, resolving `file` card titles through `titles`.
pub fn derive_with(canvas: &Canvas, titles: &dyn FileTitleSource) -> CanvasModel {
    let nodes = &canvas.nodes;
    let doc_index: HashMap<&NodeId, usize> =
        nodes.iter().enumerate().map(|(i, n)| (&n.id, i)).collect();

    // --- Rule 1: containment -------------------------------------------------
    struct GroupInfo<'a> {
        id: &'a NodeId,
        rect: Rect,
        doc: usize,
    }
    let groups: Vec<GroupInfo> = nodes
        .iter()
        .enumerate()
        .filter(|(_, n)| matches!(n.kind, NodeKind::Group { .. }))
        .map(|(doc, n)| GroupInfo {
            id: &n.id,
            rect: Rect::from_node(n),
            doc,
        })
        .collect();

    let mut parent: HashMap<NodeId, NodeId> = HashMap::new();
    for (doc, node) in nodes.iter().enumerate() {
        let self_rect = Rect::from_node(node);
        let (cx, cy) = self_rect.center();
        let is_group = matches!(node.kind, NodeKind::Group { .. });
        let best = groups
            .iter()
            .filter(|g| g.doc != doc && g.rect.contains_point_strict(cx, cy))
            // Cycle safety for group-in-group: the parent must be
            // strictly greater in the (area, doc) total order.
            .filter(|g| {
                if !is_group {
                    return true;
                }
                match Rect::area_cmp(&g.rect, &self_rect) {
                    std::cmp::Ordering::Greater => true,
                    std::cmp::Ordering::Equal => g.doc > doc,
                    std::cmp::Ordering::Less => false,
                }
            })
            // Smallest area wins; equal areas → later document order.
            .min_by(|a, b| Rect::area_cmp(&a.rect, &b.rect).then_with(|| b.doc.cmp(&a.doc)));
        if let Some(g) = best {
            parent.insert(node.id.clone(), g.id.clone());
        }
    }

    // --- Rule 2: sibling order + reading order -------------------------------
    let sibling_key = |id: &NodeId| {
        let idx = doc_index[id];
        let n = &nodes[idx];
        (n.y, n.x, idx)
    };
    let sort_siblings = |ids: &mut Vec<NodeId>| {
        ids.sort_by(|a, b| {
            let (ay, ax, ad) = sibling_key(a);
            let (by, bx, bd) = sibling_key(b);
            ay.total_cmp(&by)
                .then_with(|| ax.total_cmp(&bx))
                .then_with(|| ad.cmp(&bd))
        });
    };

    let mut children: HashMap<NodeId, Vec<NodeId>> = HashMap::new();
    let mut roots: Vec<NodeId> = Vec::new();
    for node in nodes {
        match parent.get(&node.id) {
            Some(p) => children.entry(p.clone()).or_default().push(node.id.clone()),
            None => roots.push(node.id.clone()),
        }
    }
    sort_siblings(&mut roots);
    for ids in children.values_mut() {
        sort_siblings(ids);
    }

    let mut reading_order: Vec<NodeId> = Vec::with_capacity(nodes.len());
    let mut stack: Vec<&NodeId> = roots.iter().rev().collect();
    while let Some(id) = stack.pop() {
        reading_order.push(id.clone());
        if let Some(kids) = children.get(id) {
            stack.extend(kids.iter().rev());
        }
    }

    let tree = GroupTree {
        roots,
        children,
        parent,
    };

    // --- Adjacency (dangling edges excluded) ---------------------------------
    let mut adjacency: AdjacencyMap = nodes.iter().map(|n| (n.id.clone(), Vec::new())).collect();
    for edge in &canvas.edges {
        let (from_id, to_id) = (&edge.from.0, &edge.to.0);
        if !doc_index.contains_key(from_id) || !doc_index.contains_key(to_id) {
            continue; // dangling: flagged by the parser, invisible to navigation
        }
        let push = |adj: &mut AdjacencyMap, own: &NodeId, is_from: bool, e: &Edge| {
            let (self_side, other_side) = if is_from {
                (e.from.1, e.to.1)
            } else {
                (e.to.1, e.from.1)
            };
            adj.get_mut(own).expect("all nodes seeded").push(Neighbor {
                edge: e.id.clone(),
                other: if is_from {
                    e.to.0.clone()
                } else {
                    e.from.0.clone()
                },
                direction: direction_from(is_from, e.from_end, e.to_end),
                self_side,
                other_side,
                self_is_from: is_from,
                from_end: e.from_end,
                to_end: e.to_end,
                label: e.label.clone(),
            });
        };
        push(&mut adjacency, from_id, true, edge);
        if from_id != to_id {
            push(&mut adjacency, to_id, false, edge);
        }
    }

    // --- Summaries ------------------------------------------------------------
    let link_host_counts: HashMap<String, usize> = nodes
        .iter()
        .filter_map(|n| match &n.kind {
            NodeKind::Link { url } => Some(url_host(url)),
            _ => None,
        })
        .fold(HashMap::new(), |mut acc, host| {
            *acc.entry(host).or_default() += 1;
            acc
        });
    let derived_titles: Vec<Option<String>> = nodes
        .iter()
        .map(|node| {
            let derived = match &node.kind {
                NodeKind::Text { text } => text_title(text),
                NodeKind::File { file, subpath } => file_title(file, subpath.as_deref(), titles),
                NodeKind::Link { url } => {
                    let host = url_host(url);
                    let ambiguous = link_host_counts.get(&host).copied().unwrap_or(0) > 1;
                    Some(link_title(url, ambiguous))
                }
                NodeKind::Group { label, .. } => label.clone().filter(|l| !l.trim().is_empty()),
            };
            derived.filter(|t| !t.is_empty())
        })
        .collect();
    // Untitled ordinals: document order at load, 1-based, and always the
    // next *free* ordinal (t0 §1.1) — a card literally titled
    // "Untitled 2" never collides with a generated placeholder (Voice
    // Control speakable names must be unique).
    let taken: std::collections::HashSet<&str> =
        derived_titles.iter().filter_map(|t| t.as_deref()).collect();
    let mut next_ordinal = 1usize;
    let mut base_titles: HashMap<NodeId, String> = HashMap::new();
    for (node, derived) in nodes.iter().zip(derived_titles.iter()) {
        let title = match derived {
            Some(t) => t.clone(),
            None => loop {
                let candidate = format!("Untitled {next_ordinal}");
                next_ordinal += 1;
                if !taken.contains(candidate.as_str()) {
                    break candidate;
                }
            },
        };
        base_titles.insert(node.id.clone(), title);
    }

    let group_title = |id: &NodeId| base_titles[id].clone();
    let mut summaries: HashMap<NodeId, CardSummary> = HashMap::with_capacity(nodes.len());
    for node in nodes {
        // Ancestor path, root → parent.
        let mut path: Vec<String> = Vec::new();
        let mut cursor = tree.parent.get(&node.id);
        while let Some(p) = cursor {
            path.push(group_title(p));
            cursor = tree.parent.get(p);
        }
        path.reverse();

        let container = tree.parent.get(&node.id).cloned();
        let siblings: &[NodeId] = match &container {
            Some(g) => &tree.children[g],
            None => &tree.roots,
        };
        let position = siblings
            .iter()
            .position(|s| s == &node.id)
            .expect("node is among its container's siblings")
            + 1;

        let neighbors = &adjacency[&node.id];
        let (mut in_count, mut out_count) = (0usize, 0usize);
        for nb in neighbors {
            // A self-loop both leaves and arrives at this node, so it
            // counts on both sides regardless of arrowheads.
            if nb.other == node.id {
                in_count += 1;
                out_count += 1;
                continue;
            }
            match nb.direction {
                EdgeDirection::Outgoing => out_count += 1,
                EdgeDirection::Incoming => in_count += 1,
                EdgeDirection::Bidirectional | EdgeDirection::Undirected => {
                    in_count += 1;
                    out_count += 1;
                }
            }
        }

        summaries.insert(
            node.id.clone(),
            CardSummary {
                kind_label: kind_label(node),
                display_title: base_titles[&node.id].clone(),
                group_path: path,
                container,
                position_in_container: position,
                container_size: siblings.len(),
                connection_count: neighbors.len(),
                in_count,
                out_count,
                color_name: node.color.as_ref().map(color_name),
            },
        );
    }

    let spatial = SpatialIndex {
        entries: nodes
            .iter()
            .map(|n| SpatialEntry {
                id: n.id.clone(),
                rect: Rect::from_node(n),
                is_group: matches!(n.kind, NodeKind::Group { .. }),
            })
            .collect(),
    };

    CanvasModel {
        tree,
        reading_order,
        adjacency,
        summaries,
        spatial,
    }
}

fn direction_from(is_from: bool, from_end: EndStyle, to_end: EndStyle) -> EdgeDirection {
    match (from_end, to_end) {
        (EndStyle::Arrow, EndStyle::Arrow) => EdgeDirection::Bidirectional,
        (EndStyle::None, EndStyle::None) => EdgeDirection::Undirected,
        (EndStyle::None, EndStyle::Arrow) => {
            if is_from {
                EdgeDirection::Outgoing
            } else {
                EdgeDirection::Incoming
            }
        }
        (EndStyle::Arrow, EndStyle::None) => {
            if is_from {
                EdgeDirection::Incoming
            } else {
                EdgeDirection::Outgoing
            }
        }
    }
}

/// Announcement type word (t0 §1.1): image files phrase as Image cards;
/// other media self-identify through the title prefix.
fn kind_label(node: &Node) -> &'static str {
    match &node.kind {
        NodeKind::Text { .. } => "text",
        NodeKind::Link { .. } => "link",
        NodeKind::Group { .. } => "group",
        NodeKind::File { file, .. } => match media_class(file) {
            Some(MediaClass::Image) => "image",
            _ => "file",
        },
    }
}

enum MediaClass {
    Image,
    Audio,
    Video,
}

/// Media class from the basename's real extension: a file with no `.`
/// in its basename (even one literally named `mov`) is not media.
fn media_class(path: &str) -> Option<MediaClass> {
    let base = path.rsplit(['/', '\\']).next().unwrap_or(path);
    let (stem, ext) = base.rsplit_once('.')?;
    if stem.is_empty() {
        return None; // dotfile like `.mov` — hidden file, not media
    }
    let ext = ext.to_ascii_lowercase();
    match ext.as_str() {
        "png" | "jpg" | "jpeg" | "gif" | "svg" | "webp" | "bmp" | "heic" | "avif" | "tiff" => {
            Some(MediaClass::Image)
        }
        "mp3" | "wav" | "m4a" | "flac" | "ogg" | "aac" => Some(MediaClass::Audio),
        "mp4" | "mov" | "mkv" | "webm" | "m4v" => Some(MediaClass::Video),
        _ => None,
    }
}

/// First non-empty line, heading markers stripped. None for blank text.
fn text_title(text: &str) -> Option<String> {
    let line = text.lines().map(str::trim).find(|l| !l.is_empty())?;
    let stripped = line.trim_start_matches('#').trim();
    let title = if stripped.is_empty() { line } else { stripped };
    Some(title.to_string())
}

/// Last path component without its extension — never a raw path.
fn humanize_filename(path: &str) -> String {
    let base = path.rsplit(['/', '\\']).next().unwrap_or(path);
    match base.rsplit_once('.') {
        Some((stem, _)) if !stem.is_empty() => stem.to_string(),
        _ => base.to_string(),
    }
}

/// `None` when no usable title can be derived (e.g. a path that ends in
/// `/`), so the caller falls back to an "Untitled N" ordinal.
fn file_title(path: &str, subpath: Option<&str>, titles: &dyn FileTitleSource) -> Option<String> {
    let base = titles.title_for(path).unwrap_or_else(|| {
        let name = humanize_filename(path);
        match media_class(path) {
            Some(MediaClass::Image) => format!("Image: {name}"),
            Some(MediaClass::Audio) => format!("Audio: {name}"),
            Some(MediaClass::Video) => format!("Video: {name}"),
            None => name,
        }
    });
    let base = (!base.trim().is_empty()).then_some(base);
    let heading = subpath
        .map(|sp| sp.trim_start_matches('#').trim())
        .filter(|h| !h.is_empty());
    match (base, heading) {
        (Some(b), Some(h)) => Some(format!("{b} › {h}")),
        (Some(b), None) => Some(b),
        (None, Some(h)) => Some(h.to_string()),
        (None, None) => None,
    }
}

/// Host portion of a URL (no scheme, userinfo, query, or fragment).
fn url_host(url: &str) -> String {
    let after_scheme = match url.find("://") {
        Some(i) => &url[i + 3..],
        None => url,
    };
    let authority = after_scheme
        .split(['/', '?', '#'])
        .next()
        .unwrap_or(after_scheme);
    let host = match authority.rsplit_once('@') {
        Some((_, h)) => h,
        None => authority,
    };
    host.to_string()
}

/// Link label = host, plus the first path segment when the host alone is
/// ambiguous on this canvas (t0 §1.1). The full URL lives in AX detail.
fn link_title(url: &str, ambiguous_host: bool) -> String {
    let host = url_host(url);
    if host.is_empty() {
        return url.to_string();
    }
    if !ambiguous_host {
        return host;
    }
    let after_scheme = match url.find("://") {
        Some(i) => &url[i + 3..],
        None => url,
    };
    let path = after_scheme
        .split(['?', '#'])
        .next()
        .unwrap_or(after_scheme);
    let first_segment = path.split('/').nth(1).filter(|s| !s.is_empty());
    match first_segment {
        Some(seg) => format!("{host}/{seg}"),
        None => host,
    }
}

#[cfg(test)]
#[path = "model_tests.rs"]
mod tests;
