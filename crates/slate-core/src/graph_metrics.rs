// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Graph metrics over a [`GraphIndex`] (Milestone P #551, p0_spec §P0-2).
//!
//! Computed on demand, cached per generation by the session (the
//! `(generation, snapshot)`-under-one-mutex shape, `remnant_logs`
//! precedent). Everything here is deterministic (DoD §P-C): node
//! iteration in key order, no rayon, f64 accumulation in a fixed
//! order. PageRank is a hand-rolled sparse power iteration — see
//! `pagerank_link_subgraph` for why petgraph's `algo::page_rank` is
//! not used.

use std::collections::HashMap;

use crate::graph::{EdgeKind, GraphIndex, NodeKey, NodeKind};

/// Per-node metrics. Degrees are reference-distinct (sum of
/// `EdgeData.count` — equals the links-table row count), split by
/// edge kind. NOTE: Milestone N's Bases `file.inDegree`/`outDegree`
/// fold links+embeds into one number — a deliberate delta recorded in
/// the program doc; do not "unify" here.
#[derive(Debug, Clone, PartialEq)]
pub struct NodeMetrics {
    pub in_links: u32,
    pub out_links: u32,
    pub in_embeds: u32,
    pub out_embeds: u32,
    /// Component id = index of this node's undirected connected
    /// component when components are ordered by their
    /// lexicographically-smallest member key — stable across
    /// insertion-order permutation, census-checkable.
    pub component: u32,
    /// Note nodes with zero Link-kind edges in either direction —
    /// embeds don't rescue (matches Obsidian's orphan filter intent).
    /// Always false for attachments and ghosts.
    pub is_orphan: bool,
    pub pagerank: f64,
}

/// One snapshot of every node's metrics plus vault totals.
#[derive(Debug, Clone, PartialEq)]
pub struct MetricsSnapshot {
    by_key: HashMap<NodeKey, NodeMetrics>,
    pub note_count: u32,
    pub attachment_count: u32,
    pub ghost_count: u32,
    /// Reference-distinct edge total (sum of counts), external
    /// excluded by construction.
    pub edge_count: u64,
    pub orphan_count: u32,
    pub component_count: u32,
}

impl MetricsSnapshot {
    pub fn get(&self, key: &NodeKey) -> Option<&NodeMetrics> {
        self.by_key.get(key)
    }

    pub fn len(&self) -> usize {
        self.by_key.len()
    }

    pub fn is_empty(&self) -> bool {
        self.by_key.is_empty()
    }

    /// Compute a full snapshot. Deterministic: all iteration is in
    /// canonical key order regardless of petgraph index assignment.
    pub fn compute(index: &GraphIndex) -> MetricsSnapshot {
        // Canonical node order (sorted by key) is the spine for every
        // pass below.
        let nodes = index.canonical_nodes();
        let edges = index.canonical_edges();
        let key_pos: HashMap<&NodeKey, usize> = nodes
            .iter()
            .enumerate()
            .map(|(i, (k, _, _))| (k, i))
            .collect();

        let n = nodes.len();
        let mut in_links = vec![0u32; n];
        let mut out_links = vec![0u32; n];
        let mut in_embeds = vec![0u32; n];
        let mut out_embeds = vec![0u32; n];
        let mut edge_count: u64 = 0;

        // Union-find over canonical positions for undirected components.
        let mut parent: Vec<usize> = (0..n).collect();
        fn find(parent: &mut Vec<usize>, mut x: usize) -> usize {
            while parent[x] != x {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            x
        }

        // Link-kind adjacency for PageRank (structural: one entry per
        // collapsed edge), built in canonical order.
        let mut link_out: Vec<Vec<usize>> = vec![Vec::new(); n];

        for (source_key, target_key, kind, count, _variants) in &edges {
            let s = key_pos[source_key];
            let t = key_pos[target_key];
            edge_count += u64::from(*count);
            match kind {
                EdgeKind::Link => {
                    out_links[s] += count;
                    in_links[t] += count;
                    link_out[s].push(t);
                }
                EdgeKind::Embed => {
                    out_embeds[s] += count;
                    in_embeds[t] += count;
                }
            }
            let (rs, rt) = (find(&mut parent, s), find(&mut parent, t));
            if rs != rt {
                // Union by canonical order: smaller position wins, so
                // the eventual roots are deterministic.
                let (lo, hi) = if rs < rt { (rs, rt) } else { (rt, rs) };
                parent[hi] = lo;
            }
        }

        // Component labeling: roots surface in canonical order, and
        // because union always keeps the smallest position as root,
        // the "ordered by lexicographically-smallest member" rule
        // falls out of first-encounter order over sorted nodes.
        let mut component = vec![0u32; n];
        let mut root_label: HashMap<usize, u32> = HashMap::new();
        let mut next_label = 0u32;
        for i in 0..n {
            let root = find(&mut parent, i);
            let label = *root_label.entry(root).or_insert_with(|| {
                let l = next_label;
                next_label += 1;
                l
            });
            component[i] = label;
        }

        let pagerank = pagerank_link_subgraph(&link_out);

        let mut by_key = HashMap::with_capacity(n);
        let mut note_count = 0u32;
        let mut attachment_count = 0u32;
        let mut ghost_count = 0u32;
        let mut orphan_count = 0u32;
        for (i, (key, kind, _label)) in nodes.iter().enumerate() {
            match kind {
                NodeKind::Note => note_count += 1,
                NodeKind::Attachment => attachment_count += 1,
                NodeKind::Ghost => ghost_count += 1,
            }
            let is_orphan = matches!(kind, NodeKind::Note) && in_links[i] == 0 && out_links[i] == 0;
            if is_orphan {
                orphan_count += 1;
            }
            by_key.insert(
                key.clone(),
                NodeMetrics {
                    in_links: in_links[i],
                    out_links: out_links[i],
                    in_embeds: in_embeds[i],
                    out_embeds: out_embeds[i],
                    component: component[i],
                    is_orphan,
                    pagerank: pagerank[i],
                },
            );
        }

        MetricsSnapshot {
            by_key,
            note_count,
            attachment_count,
            ghost_count,
            edge_count,
            orphan_count,
            component_count: next_label,
        }
    }
}

/// Canonical PageRank by sparse power iteration: damping 0.85, fixed
/// 40 iterations, f64, uniform teleport, dangling mass redistributed
/// uniformly each iteration. O(E) per iteration.
///
/// Deliberately NOT `petgraph::algo::page_rank` (p0_spec §P0-2,
/// amended 2026-07-11): the 0.8.3 implementation is O(V²·E) per
/// iteration — it rescans candidate sources' out-edges for every node
/// pair — and uses a nonstandard random-jump term. Every ghost node
/// is dangling here, so the dangling-mass handling is structural, not
/// a corner case. Determinism: fixed iteration count, index-order
/// accumulation over a canonical-order adjacency, no rayon.
fn pagerank_link_subgraph(link_out: &[Vec<usize>]) -> Vec<f64> {
    const DAMPING: f64 = 0.85;
    const ITERATIONS: u32 = 40;
    let n = link_out.len();
    if n == 0 {
        return Vec::new();
    }
    let n_f = n as f64;
    let mut ranks = vec![1.0 / n_f; n];
    let mut next = vec![0.0f64; n];
    for _ in 0..ITERATIONS {
        let mut dangling_mass = 0.0f64;
        next.fill(0.0);
        for (i, outs) in link_out.iter().enumerate() {
            if outs.is_empty() {
                dangling_mass += ranks[i];
            } else {
                let share = ranks[i] / outs.len() as f64;
                for &t in outs {
                    next[t] += share;
                }
            }
        }
        let teleport = (1.0 - DAMPING) / n_f;
        let dangling_share = DAMPING * dangling_mass / n_f;
        for r in next.iter_mut() {
            *r = teleport + DAMPING * *r + dangling_share;
        }
        std::mem::swap(&mut ranks, &mut next);
    }
    ranks
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn pagerank_uniform_on_symmetric_cycle() {
        // 3-cycle: perfectly symmetric, ranks must be exactly uniform
        // and sum to 1.
        let adj = vec![vec![1], vec![2], vec![0]];
        let r = pagerank_link_subgraph(&adj);
        assert!((r.iter().sum::<f64>() - 1.0).abs() < 1e-12);
        assert!((r[0] - r[1]).abs() < 1e-12 && (r[1] - r[2]).abs() < 1e-12);
    }

    #[test]
    fn pagerank_dangling_mass_is_conserved() {
        // a -> b, b dangling: without redistribution the sum decays.
        let adj = vec![vec![1], vec![]];
        let r = pagerank_link_subgraph(&adj);
        assert!(
            (r.iter().sum::<f64>() - 1.0).abs() < 1e-9,
            "dangling mass must be redistributed, got sum {}",
            r.iter().sum::<f64>()
        );
        assert!(r[1] > r[0], "the pointed-at node outranks the pointer");
    }

    #[test]
    fn pagerank_empty_graph() {
        assert!(pagerank_link_subgraph(&[]).is_empty());
    }
}
