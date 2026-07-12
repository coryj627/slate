// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! In-memory adjacency mirror of the `links` table (Milestone P, #550).
//!
//! One `GraphIndex` per session, built lazily on first graph query and
//! maintained incrementally afterwards by replaying exactly what the
//! session just wrote to SQLite (`docs/plans/11_graph/specs/p0_spec.md`
//! is the normative contract). The graph never re-reads note files
//! from disk and never re-implements resolver policy: hooks hand it
//! the rows SQLite committed, and it mirrors them.
//!
//! Determinism (DoD §P-C): `build` iterates in explicit SQL `ORDER BY`
//! (`files.id`, then `(source_file_id, ordinal)`); `by_key` is storage
//! only — anything that enumerates nodes sorts by [`NodeKey`].

use std::collections::{BTreeMap, HashMap};

use petgraph::Direction;
use petgraph::stable_graph::{NodeIndex, StableDiGraph};
use petgraph::visit::EdgeRef;
use rusqlite::Connection;

use crate::VaultError;

/// Node identity. Path-keyed for real files; ghost-keyed for
/// unresolved (or dangling-resolved) targets.
#[derive(Debug, Clone, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub enum NodeKey {
    /// Vault-relative path of an indexed file (notes AND attachments).
    Path(String),
    /// Normalized unresolved target — see [`ghost_key`].
    Ghost(String),
}

/// Normalize an authored link target into a ghost key: trim, strip a
/// leading `./` or `/`, then `str::to_lowercase()` — EXACTLY the
/// resolver's comparison convention (`link_resolver::find_exact` /
/// `collect_basename_matches`: full Unicode lowercase, no NFC, no
/// simple case-fold), so a ghost and the note that later materializes
/// it collide on intent. `target_raw` is already anchor-stripped by
/// `links::extract_links`.
pub fn ghost_key(target_raw: &str) -> String {
    let trimmed = target_raw.trim();
    let stripped = trimmed
        .strip_prefix("./")
        .or_else(|| trimmed.strip_prefix('/'))
        .unwrap_or(trimmed);
    stripped.to_lowercase()
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
pub enum NodeKind {
    Note,
    Attachment,
    Ghost,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct NodeData {
    pub key: NodeKey,
    pub kind: NodeKind,
    /// Display label. Notes: file stem (extension dropped).
    /// Attachments: final path component WITH extension (stems collide
    /// across extensions). Ghosts: the lexicographically-smallest
    /// currently-authored variant of `target_raw` among live
    /// references — derived from the in-edges' variant refcounts, so
    /// it is stable under insertion-order permutation and under
    /// incremental maintenance (p0_spec, NodeData rule).
    pub label: String,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub enum EdgeKind {
    Link,
    Embed,
}

/// One edge per `(source, target, kind)`; parallel references
/// collapse into `count`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct EdgeData {
    pub kind: EdgeKind,
    pub count: u32,
    /// For Ghost-targeted edges only: authored `target_raw` variants
    /// (anchor-stripped, original case) → reference count, summing to
    /// `count`. Feeds the ghost's deterministic label. Empty for
    /// Path-targeted edges.
    variants: BTreeMap<String, u32>,
}

/// The graph-relevant projection of one `links` row, as written by
/// `links_db::replace_links_for_file` (or as read back for replay).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphLinkRow {
    pub target_path: Option<String>,
    pub target_raw: String,
    pub is_embed: bool,
    pub is_external: bool,
}

/// One `links` row pointing AT a path of interest, captured
/// in-transaction (before a CASCADE delete, or at file-add time for
/// the delete-then-recreate case).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct InboundRow {
    pub source_path: String,
    pub target_raw: String,
    pub is_embed: bool,
}

/// One staged mutation, replaying a committed SQLite change. Staged
/// by the session's hook points while the write transaction is open;
/// applied (in stage order) only after the commit succeeds
/// (p0_spec P0-1 rule 3a).
#[derive(Debug, Clone)]
pub enum GraphOp {
    /// `replace_links_for_file` ran for `source_path` (save, scan slow
    /// path, purge with empty rows, re-resolve replay).
    LinksetChanged {
        source_path: String,
        rows: Vec<GraphLinkRow>,
    },
    /// A `files` row was inserted. `resolved_inbound` carries any
    /// pre-existing rows with `target_path == path` (dangling rows
    /// from an earlier delete of the same path) so their edges move
    /// back from ghosts to the reborn Path node.
    FileAdded {
        path: String,
        is_markdown: bool,
        resolved_inbound: Vec<InboundRow>,
    },
    /// A `files` row was deleted (explicit delete or scan prune). The
    /// FK cascade erased the file's own outgoing rows; `inbound` is
    /// the pre-delete snapshot of rows pointing at it, which SQLite
    /// leaves resolved-but-dangling — the graph maps each to a ghost
    /// keyed on its own `target_raw` (p0_spec P0-1 rule 1a).
    FileRemoved {
        path: String,
        inbound: Vec<InboundRow>,
    },
    /// A move/rename: the `files` row's path changed and
    /// `finish_structural_move` bulk-repointed inbound
    /// `links.target_path` old → new. In-graph edges follow the node,
    /// so only identity/kind/label update here.
    FileRenamed {
        old_path: String,
        new_path: String,
        is_markdown: bool,
    },
}

/// Stack-owned staging buffer for one mutation's graph ops
/// (p0_spec P0-1 rule 3a). Created by the session at the top of each
/// hooked mutation, threaded through the write helpers, and consumed
/// by `VaultSession::graph_apply` only after the enclosing commit
/// succeeds — dropping the sink on any error path drops the staged
/// ops with it, so a rolled-back transaction can never leak into the
/// index.
///
/// `live == false` (index never built) makes every stage call a
/// no-op, including the closure that would run extra SQL — cold
/// sessions pay zero cost (DoD §P-E).
pub struct GraphOpSink {
    live: bool,
    /// A hooked path hit an error it reports-and-continues on (scan's
    /// prune/re-resolve error accumulation): the replay is incomplete,
    /// so the applied index must be dropped and lazily rebuilt.
    poisoned: bool,
    ops: Vec<GraphOp>,
}

impl GraphOpSink {
    pub(crate) fn new(live: bool) -> Self {
        GraphOpSink {
            live,
            poisoned: false,
            ops: Vec::new(),
        }
    }

    pub(crate) fn live(&self) -> bool {
        self.live
    }

    pub(crate) fn stage(&mut self, op: impl FnOnce() -> GraphOp) {
        if self.live {
            self.ops.push(op());
        }
    }

    /// Stage an op whose construction runs fallible SQL (inbound-row
    /// snapshots). The closure is skipped entirely when not live.
    pub(crate) fn stage_with(
        &mut self,
        op: impl FnOnce() -> Result<GraphOp, VaultError>,
    ) -> Result<(), VaultError> {
        if self.live {
            let op = op()?;
            self.ops.push(op);
        }
        Ok(())
    }

    pub(crate) fn poison(&mut self) {
        if self.live {
            self.poisoned = true;
        }
    }

    pub(crate) fn into_parts(self) -> (bool, bool, Vec<GraphOp>) {
        (self.live, self.poisoned, self.ops)
    }
}

/// A replayed op did not fit the current graph — the incremental
/// index and SQLite have drifted, and the only safe response is to
/// drop the index and rebuild lazily.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct ReplayMismatch;

/// Canonical edge form: `(source_key, target_key, kind, count,
/// ghost-variant refcounts)` — see [`GraphIndex::canonical_edges`].
pub type CanonicalEdge = (NodeKey, NodeKey, EdgeKind, u32, BTreeMap<String, u32>);

/// In-memory adjacency mirror. See module docs.
pub struct GraphIndex {
    graph: StableDiGraph<NodeData, EdgeData>,
    by_key: HashMap<NodeKey, NodeIndex>,
    /// Bumps once per applied mutation batch (not per op) — cheap
    /// "did anything change" probe for the FFI layer (P0-3).
    generation: u64,
}

fn note_label(path: &str) -> String {
    let name = path.rsplit('/').next().unwrap_or(path);
    match name.rsplit_once('.') {
        Some((stem, _ext)) if !stem.is_empty() => stem.to_string(),
        _ => name.to_string(),
    }
}

fn attachment_label(path: &str) -> String {
    path.rsplit('/').next().unwrap_or(path).to_string()
}

fn path_label(path: &str, is_markdown: bool) -> String {
    if is_markdown {
        note_label(path)
    } else {
        attachment_label(path)
    }
}

fn path_kind(is_markdown: bool) -> NodeKind {
    if is_markdown {
        NodeKind::Note
    } else {
        NodeKind::Attachment
    }
}

impl GraphIndex {
    /// Full build from SQLite. One pass over `files`, one pass over
    /// `links` (external rows excluded). Resolved rows whose target
    /// is missing from `files` (dangling after a delete) map to
    /// ghosts keyed on their own `target_raw` — exactly what the row
    /// would produce after its source re-scans (rule 1a).
    pub fn build(conn: &Connection) -> Result<GraphIndex, VaultError> {
        let mut index = GraphIndex {
            graph: StableDiGraph::new(),
            by_key: HashMap::new(),
            generation: 0,
        };

        let mut files_stmt = conn.prepare("SELECT path, is_markdown FROM files ORDER BY id ASC")?;
        let files: Vec<(String, bool)> = files_stmt
            .query_map([], |row| {
                Ok((row.get::<_, String>(0)?, row.get::<_, i64>(1)? != 0))
            })?
            .collect::<Result<Vec<_>, _>>()?;
        for (path, is_markdown) in files {
            index.insert_path_node(path, is_markdown);
        }

        let mut links_stmt = conn.prepare(
            "SELECT f.path, l.target_path, l.target_raw, l.is_embed
             FROM links l
             JOIN files f ON f.id = l.source_file_id
             WHERE l.is_external = 0
             ORDER BY l.source_file_id ASC, l.ordinal ASC",
        )?;
        let rows: Vec<(String, Option<String>, String, bool)> = links_stmt
            .query_map([], |row| {
                Ok((
                    row.get::<_, String>(0)?,
                    row.get::<_, Option<String>>(1)?,
                    row.get::<_, String>(2)?,
                    row.get::<_, i64>(3)? != 0,
                ))
            })?
            .collect::<Result<Vec<_>, _>>()?;
        for (source_path, target_path, target_raw, is_embed) in rows {
            let source = match index.by_key.get(&NodeKey::Path(source_path.clone())) {
                Some(&idx) => idx,
                // A links row whose source has no files row cannot
                // exist (FK enforced); defensive skip keeps build
                // total rather than panicking on a corrupt cache.
                None => {
                    debug_assert!(false, "links row with no source files row");
                    continue;
                }
            };
            let kind = if is_embed {
                EdgeKind::Embed
            } else {
                EdgeKind::Link
            };
            index.add_reference(source, &target_path, &target_raw, kind);
        }
        Ok(index)
    }

    pub fn generation(&self) -> u64 {
        self.generation
    }

    /// Apply one batch of staged ops in order, then bump the
    /// generation once. Returns [`ReplayMismatch`] on an
    /// internal-consistency violation (a replayed op that does not
    /// fit the current graph); the caller responds by dropping the
    /// index (lazy rebuild repairs) — corruption is never left
    /// standing.
    pub fn apply_batch(&mut self, ops: Vec<GraphOp>) -> Result<(), ReplayMismatch> {
        for op in ops {
            match op {
                GraphOp::LinksetChanged { source_path, rows } => {
                    self.apply_linkset_change(&source_path, &rows)?;
                }
                GraphOp::FileAdded {
                    path,
                    is_markdown,
                    resolved_inbound,
                } => {
                    self.apply_file_added(&path, is_markdown, &resolved_inbound)?;
                }
                GraphOp::FileRemoved { path, inbound } => {
                    self.apply_file_removed(&path, &inbound)?;
                }
                GraphOp::FileRenamed {
                    old_path,
                    new_path,
                    is_markdown,
                } => {
                    self.apply_file_renamed(&old_path, &new_path, is_markdown)?;
                }
            }
        }
        self.generation += 1;
        Ok(())
    }

    /// Structural equality on the canonical (sorted-by-key) node and
    /// edge multisets — the census seam (`census_graph_matches_rebuild`
    /// compares the incrementally-maintained index against a fresh
    /// `build` after every op). Ignores `generation` and petgraph
    /// index assignment.
    pub fn deep_equals(&self, other: &GraphIndex) -> bool {
        self.canonical_nodes() == other.canonical_nodes()
            && self.canonical_edges() == other.canonical_edges()
    }

    /// Sorted `(key, kind, label)` triples.
    pub fn canonical_nodes(&self) -> Vec<(NodeKey, NodeKind, String)> {
        let mut nodes: Vec<_> = self
            .graph
            .node_indices()
            .map(|i| {
                let n = &self.graph[i];
                (n.key.clone(), n.kind, n.label.clone())
            })
            .collect();
        nodes.sort();
        nodes
    }

    /// Sorted `(source_key, target_key, kind, count, variants)` tuples.
    pub fn canonical_edges(&self) -> Vec<CanonicalEdge> {
        let mut edges: Vec<_> = self
            .graph
            .edge_indices()
            .map(|e| {
                let (a, b) = self.graph.edge_endpoints(e).expect("live edge");
                let d = &self.graph[e];
                (
                    self.graph[a].key.clone(),
                    self.graph[b].key.clone(),
                    d.kind,
                    d.count,
                    d.variants.clone(),
                )
            })
            .collect();
        edges.sort();
        edges
    }

    pub fn node_count(&self) -> usize {
        self.graph.node_count()
    }

    pub fn edge_count(&self) -> usize {
        self.graph.edge_count()
    }

    // --- internal construction/mutation ---------------------------------

    fn insert_path_node(&mut self, path: String, is_markdown: bool) -> NodeIndex {
        let key = NodeKey::Path(path.clone());
        debug_assert!(
            !self.by_key.contains_key(&key),
            "duplicate Path node insert"
        );
        let idx = self.graph.add_node(NodeData {
            label: path_label(&path, is_markdown),
            kind: path_kind(is_markdown),
            key: key.clone(),
        });
        self.by_key.insert(key, idx);
        idx
    }

    fn ensure_ghost_node(&mut self, key_str: &str) -> NodeIndex {
        let key = NodeKey::Ghost(key_str.to_string());
        if let Some(&idx) = self.by_key.get(&key) {
            return idx;
        }
        let idx = self.graph.add_node(NodeData {
            key: key.clone(),
            kind: NodeKind::Ghost,
            // Provisional; recomputed from in-edge variants as soon as
            // the referencing edge lands (`recompute_ghost_label`).
            label: key_str.to_string(),
        });
        self.by_key.insert(key, idx);
        idx
    }

    /// Map one row to its target node key: resolved-and-present →
    /// Path; resolved-but-dangling or unresolved → Ghost keyed on the
    /// row's own `target_raw`. External rows must be filtered by the
    /// caller.
    fn row_target_key(&self, target_path: &Option<String>, target_raw: &str) -> NodeKey {
        if let Some(tp) = target_path {
            let key = NodeKey::Path(tp.clone());
            if self.by_key.contains_key(&key) {
                return key;
            }
        }
        NodeKey::Ghost(ghost_key(target_raw))
    }

    /// Add one reference from `source` (used by `build`): find or
    /// create the target node, then find or create the collapsed
    /// edge and bump its count/variants.
    fn add_reference(
        &mut self,
        source: NodeIndex,
        target_path: &Option<String>,
        target_raw: &str,
        kind: EdgeKind,
    ) {
        let target_key = self.row_target_key(target_path, target_raw);
        let is_ghost = matches!(target_key, NodeKey::Ghost(_));
        let target = match &target_key {
            NodeKey::Path(_) => self.by_key[&target_key],
            NodeKey::Ghost(g) => {
                let g = g.clone();
                self.ensure_ghost_node(&g)
            }
        };
        let existing = self
            .graph
            .edges_connecting(source, target)
            .find(|e| e.weight().kind == kind)
            .map(|e| e.id());
        match existing {
            Some(e) => {
                let data = &mut self.graph[e];
                data.count += 1;
                if is_ghost {
                    *data.variants.entry(target_raw.to_string()).or_insert(0) += 1;
                }
            }
            None => {
                let mut variants = BTreeMap::new();
                if is_ghost {
                    variants.insert(target_raw.to_string(), 1);
                }
                self.graph.add_edge(
                    source,
                    target,
                    EdgeData {
                        kind,
                        count: 1,
                        variants,
                    },
                );
            }
        }
        if is_ghost {
            self.recompute_ghost_label(target);
        }
    }

    /// Recompute the source's collapsed out-edge multiset from `rows`,
    /// diff against current out-edges, apply. A ghost node is removed
    /// when its last in-edge goes; a Path node is never removed by
    /// link changes.
    fn apply_linkset_change(
        &mut self,
        source_path: &str,
        rows: &[GraphLinkRow],
    ) -> Result<(), ReplayMismatch> {
        let source = match self.by_key.get(&NodeKey::Path(source_path.to_string())) {
            Some(&idx) => idx,
            None => {
                // The files row exists (it was just written to) but the
                // node is missing — replay drift. Fail the batch; the
                // session rebuilds lazily.
                debug_assert!(false, "linkset change for unknown source node");
                return Err(ReplayMismatch);
            }
        };

        // New multiset: (target key, kind) → (count, variants).
        let mut wanted: BTreeMap<(NodeKey, EdgeKind), (u32, BTreeMap<String, u32>)> =
            BTreeMap::new();
        for row in rows {
            if row.is_external {
                continue;
            }
            let kind = if row.is_embed {
                EdgeKind::Embed
            } else {
                EdgeKind::Link
            };
            let key = self.row_target_key(&row.target_path, &row.target_raw);
            let entry = wanted
                .entry((key.clone(), kind))
                .or_insert((0, BTreeMap::new()));
            entry.0 += 1;
            if matches!(key, NodeKey::Ghost(_)) {
                *entry.1.entry(row.target_raw.clone()).or_insert(0) += 1;
            }
        }

        // Current out-edges of source.
        let current: Vec<(petgraph::stable_graph::EdgeIndex, NodeIndex, EdgeKind)> = self
            .graph
            .edges_directed(source, Direction::Outgoing)
            .map(|e| (e.id(), e.target(), e.weight().kind))
            .collect();

        let mut touched_ghosts: Vec<NodeIndex> = Vec::new();
        for (edge_id, target, kind) in current {
            let target_key = self.graph[target].key.clone();
            match wanted.remove(&(target_key.clone(), kind)) {
                Some((count, variants)) => {
                    let data = &mut self.graph[edge_id];
                    data.count = count;
                    data.variants = variants;
                    if matches!(target_key, NodeKey::Ghost(_)) {
                        touched_ghosts.push(target);
                    }
                }
                None => {
                    self.graph.remove_edge(edge_id);
                    if matches!(target_key, NodeKey::Ghost(_)) {
                        touched_ghosts.push(target);
                    }
                }
            }
        }
        // Remaining wanted entries are new edges.
        for ((target_key, kind), (count, variants)) in wanted {
            let target = match &target_key {
                NodeKey::Path(_) => self.by_key[&target_key],
                NodeKey::Ghost(g) => {
                    let g = g.clone();
                    self.ensure_ghost_node(&g)
                }
            };
            self.graph.add_edge(
                source,
                target,
                EdgeData {
                    kind,
                    count,
                    variants,
                },
            );
            if matches!(target_key, NodeKey::Ghost(_)) {
                touched_ghosts.push(target);
            }
        }

        for ghost in touched_ghosts {
            self.gc_or_relabel_ghost(ghost);
        }
        Ok(())
    }

    fn apply_file_added(
        &mut self,
        path: &str,
        is_markdown: bool,
        resolved_inbound: &[InboundRow],
    ) -> Result<(), ReplayMismatch> {
        let key = NodeKey::Path(path.to_string());
        if self.by_key.contains_key(&key) {
            debug_assert!(false, "file added twice");
            return Err(ReplayMismatch);
        }
        let target = self.insert_path_node(path.to_string(), is_markdown);

        // Delete-then-recreate healing: rows that keep `target_path ==
        // path` were mapped to ghosts when the old file died (rule
        // 1a); a fresh build would map them to this Path node again,
        // so the incremental index must move them back.
        for row in resolved_inbound {
            let source = match self.by_key.get(&NodeKey::Path(row.source_path.clone())) {
                Some(&idx) => idx,
                None => {
                    debug_assert!(false, "inbound row from unknown source");
                    return Err(ReplayMismatch);
                }
            };
            let kind = if row.is_embed {
                EdgeKind::Embed
            } else {
                EdgeKind::Link
            };
            let gkey = NodeKey::Ghost(ghost_key(&row.target_raw));
            let Some(&ghost) = self.by_key.get(&gkey) else {
                debug_assert!(false, "dangling inbound row had no ghost edge");
                return Err(ReplayMismatch);
            };
            let ghost_edge = self
                .graph
                .edges_connecting(source, ghost)
                .find(|e| e.weight().kind == kind)
                .map(|e| e.id());
            let Some(ghost_edge) = ghost_edge else {
                debug_assert!(false, "dangling inbound row had no ghost edge");
                return Err(ReplayMismatch);
            };
            // Move ONE reference (this row) from the ghost edge to the
            // reborn Path edge.
            {
                let data = &mut self.graph[ghost_edge];
                data.count -= 1;
                if let Some(v) = data.variants.get_mut(&row.target_raw) {
                    *v -= 1;
                    if *v == 0 {
                        data.variants.remove(&row.target_raw);
                    }
                }
                if data.count == 0 {
                    self.graph.remove_edge(ghost_edge);
                }
            }
            let path_edge = self
                .graph
                .edges_connecting(source, target)
                .find(|e| e.weight().kind == kind)
                .map(|e| e.id());
            match path_edge {
                Some(e) => self.graph[e].count += 1,
                None => {
                    self.graph.add_edge(
                        source,
                        target,
                        EdgeData {
                            kind,
                            count: 1,
                            variants: BTreeMap::new(),
                        },
                    );
                }
            }
            self.gc_or_relabel_ghost(ghost);
        }
        Ok(())
    }

    fn apply_file_removed(
        &mut self,
        path: &str,
        inbound: &[InboundRow],
    ) -> Result<(), ReplayMismatch> {
        let key = NodeKey::Path(path.to_string());
        let Some(victim) = self.by_key.remove(&key) else {
            debug_assert!(false, "removed file had no node");
            return Err(ReplayMismatch);
        };
        // Ghost out-neighbors may lose their last in-edge with the
        // victim's removal.
        let out_ghosts: Vec<NodeIndex> = self
            .graph
            .neighbors_directed(victim, Direction::Outgoing)
            .filter(|&n| matches!(self.graph[n].kind, NodeKind::Ghost))
            .collect();

        self.graph.remove_node(victim);

        // Inbound rows stay resolved-but-dangling in SQLite; mirror
        // them as ghost references keyed on each row's target_raw.
        for row in inbound {
            // Skip rows whose source died earlier in this same batch
            // (folder delete replays file-by-file; a later victim's
            // inbound snapshot may still name an earlier one).
            let Some(&source) = self.by_key.get(&NodeKey::Path(row.source_path.clone())) else {
                continue;
            };
            let kind = if row.is_embed {
                EdgeKind::Embed
            } else {
                EdgeKind::Link
            };
            let gkey_str = ghost_key(&row.target_raw);
            let ghost = self.ensure_ghost_node(&gkey_str);
            let existing = self
                .graph
                .edges_connecting(source, ghost)
                .find(|e| e.weight().kind == kind)
                .map(|e| e.id());
            match existing {
                Some(e) => {
                    let data = &mut self.graph[e];
                    data.count += 1;
                    *data.variants.entry(row.target_raw.clone()).or_insert(0) += 1;
                }
                None => {
                    let mut variants = BTreeMap::new();
                    variants.insert(row.target_raw.clone(), 1);
                    self.graph.add_edge(
                        source,
                        ghost,
                        EdgeData {
                            kind,
                            count: 1,
                            variants,
                        },
                    );
                }
            }
            self.recompute_ghost_label(ghost);
        }

        for ghost in out_ghosts {
            self.gc_or_relabel_ghost(ghost);
        }
        Ok(())
    }

    fn apply_file_renamed(
        &mut self,
        old_path: &str,
        new_path: &str,
        is_markdown: bool,
    ) -> Result<(), ReplayMismatch> {
        let old_key = NodeKey::Path(old_path.to_string());
        let new_key = NodeKey::Path(new_path.to_string());
        let Some(idx) = self.by_key.remove(&old_key) else {
            debug_assert!(false, "renamed file had no node");
            return Err(ReplayMismatch);
        };
        if self.by_key.contains_key(&new_key) {
            debug_assert!(false, "rename target node already exists");
            return Err(ReplayMismatch);
        }
        let node = &mut self.graph[idx];
        node.key = new_key.clone();
        node.kind = path_kind(is_markdown);
        node.label = path_label(new_path, is_markdown);
        self.by_key.insert(new_key, idx);
        Ok(())
    }

    /// Remove a ghost with no remaining in-edges; otherwise recompute
    /// its label from the surviving in-edge variants.
    fn gc_or_relabel_ghost(&mut self, ghost: NodeIndex) {
        if !self.graph.contains_node(ghost) {
            return;
        }
        if !matches!(self.graph[ghost].kind, NodeKind::Ghost) {
            return;
        }
        if self
            .graph
            .edges_directed(ghost, Direction::Incoming)
            .next()
            .is_none()
        {
            let key = self.graph[ghost].key.clone();
            self.by_key.remove(&key);
            self.graph.remove_node(ghost);
        } else {
            self.recompute_ghost_label(ghost);
        }
    }

    /// Ghost label = lexicographically-smallest currently-authored
    /// variant across all in-edges (deterministic under permutation
    /// and incremental maintenance — p0_spec NodeData rule).
    fn recompute_ghost_label(&mut self, ghost: NodeIndex) {
        let smallest = self
            .graph
            .edges_directed(ghost, Direction::Incoming)
            .flat_map(|e| e.weight().variants.keys())
            .min()
            .cloned();
        if let Some(label) = smallest {
            self.graph[ghost].label = label;
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn ghost_key_matches_resolver_convention() {
        assert_eq!(ghost_key("Foo"), "foo");
        assert_eq!(ghost_key("  Foo  "), "foo");
        assert_eq!(ghost_key("./notes/Foo"), "notes/foo");
        assert_eq!(ghost_key("/Foo"), "foo");
        assert_eq!(ghost_key("İstanbul"), "İstanbul".to_lowercase());
        // NFD vs NFC stay distinct — the resolver never normalizes, so
        // neither does the ghost key (graph replays SQLite).
        let nfd = "Cafe\u{0301}";
        let nfc = "Caf\u{00e9}";
        assert_ne!(ghost_key(nfd), ghost_key(nfc));
    }

    #[test]
    fn labels_note_stem_attachment_full_ghost_smallest_variant() {
        assert_eq!(note_label("notes/My Note.md"), "My Note");
        assert_eq!(note_label("noext"), "noext");
        assert_eq!(attachment_label("img/pic.png"), "pic.png");
    }
}
