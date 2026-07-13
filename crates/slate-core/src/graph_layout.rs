// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Deterministic force-directed graph layout (Milestone P, P2-1 #557).
//!
//! A seeded Fruchterman–Reingold solver with gravity, temperature
//! cooling, pinning, and a Barnes–Hut repulsion tier for large graphs —
//! all in `f64`, single-threaded, RNG-free at init. The contract
//! (DoD §P-C): the same graph + seed + forces + budgets yields
//! bit-identical positions on a given platform. Determinism comes from
//! (1) golden-angle initial placement (no `thread_rng`), (2) node
//! iteration in the key-sorted order [`GraphIndex::filtered_nodes`]
//! emits — the SAME order P0-3 snapshots use, so positions align with
//! snapshot rows by index — (3) edge iteration in `(source, target,
//! kind)` order, (4) no rayon in the force pass, and (5) a fixed
//! iteration budget whose only early stop is the deterministic
//! convergence predicate.
//!
//! The four `LayoutForces` map 1:1 onto the Obsidian-parity sliders;
//! the lerp ranges below are normative (p2_spec §P2-1).

use crate::graph::{GraphFilter, GraphIndex, NodeKey};
use crate::graph_metrics::MetricsSnapshot;
use std::collections::HashMap;

/// Golden angle in radians (`π * (3 − √5)`) — the initial-placement
/// spiral increment, giving maximally-even, coincidence-free seeding.
const GOLDEN_ANGLE: f64 = 2.399_963_229_728_653;
/// Node count above which the Barnes–Hut repulsion tier auto-engages.
const BARNES_HUT_THRESHOLD: usize = 1_500;
/// Barnes–Hut opening criterion `θ`.
const BH_THETA: f64 = 0.9;
/// Temperature multiplier per iteration.
const COOLING: f64 = 0.97;
/// Coincidence epsilon: pairs closer than this separate along a seeded
/// (not random) direction rather than dividing force by ~0.
const EPS: f64 = 1e-6;

/// User-tunable forces, matching the Obsidian-parity sliders 1:1 (all
/// `0.0..=1.0`, default `0.5`). The mapping to physical constants lives
/// in the force pass and is normative (p2_spec §P2-1).
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct LayoutForces {
    /// Gravity toward the origin (frames disconnected components).
    pub center: f32,
    /// Pairwise repulsion strength.
    pub repel: f32,
    /// Per-edge attraction strength.
    pub link: f32,
    /// Ideal edge length (maps to `k`).
    pub link_distance: f32,
}

impl Default for LayoutForces {
    fn default() -> Self {
        LayoutForces {
            center: 0.5,
            repel: 0.5,
            link: 0.5,
            link_distance: 0.5,
        }
    }
}

/// Solve budgets and the jitter seed.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct LayoutConfig {
    /// Reserved for deterministic jitter derivation; same seed ⇒ same
    /// layout.
    pub seed: u64,
    /// Cold-solve iteration budget.
    pub max_iterations: u32,
    /// Warm-start iteration budget after a `warm_update`.
    pub warm_iterations: u32,
}

impl Default for LayoutConfig {
    fn default() -> Self {
        LayoutConfig {
            seed: 0,
            max_iterations: 300,
            warm_iterations: 60,
        }
    }
}

/// Result of a [`LayoutEngine::step`] call.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct StepReport {
    /// Total iterations run since construction.
    pub iteration: u32,
    /// Largest single-node displacement in the final iteration.
    pub max_displacement: f64,
    /// Whether the convergence predicate held.
    pub converged: bool,
}

/// Result of a [`LayoutEngine::warm_update`].
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct WarmReport {
    /// Nodes whose position carried over from the prior layout.
    pub carried: usize,
    /// New nodes seeded (neighbor centroid or golden-angle ring).
    pub seeded: usize,
    /// Nodes left awake (the ≤2-hop neighborhood of changes).
    pub awake: usize,
}

/// The FFI-facing topology of a live layout (P2-2 #558): the backend
/// node id for each position slot (in `keys()` / `positions()` order)
/// plus the id-keyed collapsed edges, tagged with the graph
/// `generation` those ids belong to. A generation change may reassign
/// ids, so the FFI re-derives this whenever [`LayoutEngine::warm_update`]
/// runs and carries `generation` on every frame so stale buffers are
/// detectable.
#[derive(Debug, Clone, PartialEq)]
pub struct LayoutTopology {
    /// Backend id (the `filtered_nodes` id space) per position slot;
    /// `ids[i]` names `positions()[i]`.
    pub ids: Vec<u64>,
    /// Collapsed edges among `ids`, in the deterministic
    /// `(source key, target key, kind)` order P0-3 snapshots use.
    pub edges: Vec<crate::graph::GraphEdge>,
    /// Full node metadata (labels, link counts, kind, metrics …) for the
    /// same `ids`, in the SAME order — computed under one graph lock with
    /// the topology so the diagram's labels can never come from a
    /// different generation than its ids (P2-3 #559 review). Empty when
    /// [`layout_topology`] builds a bare topology; the session fills it.
    pub nodes: Vec<crate::graph::GraphNode>,
    /// The graph generation `ids`/`nodes` were derived against.
    pub generation: u64,
}

/// Which repulsion solver the force pass uses.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum LayoutTier {
    /// O(n²) exact pairwise repulsion.
    Exact,
    /// O(n log n) Barnes–Hut quadtree approximation.
    BarnesHut,
}

/// A `splitmix64` step — a seeded, platform-stable scrambler for the
/// coincidence-separation direction (never `thread_rng`).
fn splitmix64(mut x: u64) -> u64 {
    x = x.wrapping_add(0x9E37_79B9_7F4A_7C15);
    let mut z = x;
    z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
    z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
    z ^ (z >> 31)
}

/// FNV-1a over a key's bytes — a small, platform-stable hash (unlike
/// `DefaultHasher`, whose output is not guaranteed stable), so the
/// coincidence-jitter direction is reproducible across builds.
fn key_hash(key: &NodeKey) -> u64 {
    let mut h: u64 = 0xcbf2_9ce4_8422_2325;
    let (tag, s) = match key {
        NodeKey::Path(p) => (0u8, p.as_str()),
        NodeKey::Ghost(g) => (1u8, g.as_str()),
    };
    for b in std::iter::once(tag).chain(s.bytes()) {
        h ^= b as u64;
        h = h.wrapping_mul(0x0000_0100_0000_01B3);
    }
    h
}

fn lerp(a: f64, b: f64, t: f32) -> f64 {
    a + (b - a) * f64::from(t.clamp(0.0, 1.0))
}

/// Deterministic UNIT separation direction for a coincident pair,
/// derived from the smaller key's hash (p2_spec §P2-1 — seeded, never
/// `thread_rng`). Shared by the exact tier and the Barnes–Hut bucket so
/// both separate coincident points along the SAME direction.
fn coincident_dir(seed: u64, key_a: &NodeKey, key_b: &NodeKey) -> [f64; 2] {
    let min_key = if key_a <= key_b { key_a } else { key_b };
    let h = splitmix64(seed ^ key_hash(min_key));
    let angle = (h as f64 / u64::MAX as f64) * std::f64::consts::TAU;
    [angle.cos(), angle.sin()]
}

/// A deterministic force-directed layout over the filtered graph.
pub struct LayoutEngine {
    /// Node keys in `filtered_nodes` (key-sorted) order; `positions[i]`
    /// is the node `keys[i]`.
    keys: Vec<NodeKey>,
    key_index: HashMap<NodeKey, usize>,
    positions: Vec<[f64; 2]>,
    /// `(a, b, weight)` per graph edge (both kinds), weight `ln(1+count)`.
    edges: Vec<(usize, usize, f64)>,
    pinned: Vec<bool>,
    /// A node moves only when awake AND not pinned. Cold solve: all
    /// awake; `warm_update` narrows to the ≤2-hop change neighborhood.
    awake: Vec<bool>,
    temperature: f64,
    iteration: u32,
    forces: LayoutForces,
    config: LayoutConfig,
    /// Ideal edge length `k = L`.
    k: f64,
    /// `None` ⇒ auto-select by node count; `Some` ⇒ test-forced tier.
    forced_tier: Option<LayoutTier>,
}

impl LayoutEngine {
    /// Build a cold layout: golden-angle initial placement, temperature
    /// seeded to `t₀ = 0.1·√n·k`, all nodes awake.
    pub fn new(
        graph: &GraphIndex,
        filter: &GraphFilter,
        forces: LayoutForces,
        config: LayoutConfig,
    ) -> Self {
        let metrics = MetricsSnapshot::compute(graph);
        let (keys, edges) = Self::project(graph, filter, &metrics);
        let n = keys.len();
        let k = lerp(20.0, 200.0, forces.link_distance);
        let positions = Self::golden_angle_placement(n, k);
        let key_index = keys
            .iter()
            .enumerate()
            .map(|(i, key)| (key.clone(), i))
            .collect();
        LayoutEngine {
            keys,
            key_index,
            positions,
            edges,
            pinned: vec![false; n],
            awake: vec![true; n],
            temperature: Self::initial_temperature(n, k),
            iteration: 0,
            forces,
            config,
            k,
            forced_tier: None,
        }
    }

    /// Project the filtered graph into the key-sorted node list + the
    /// edge list (dense local indices), matching P0-3's node order.
    fn project(
        graph: &GraphIndex,
        filter: &GraphFilter,
        metrics: &MetricsSnapshot,
    ) -> (Vec<NodeKey>, Vec<(usize, usize, f64)>) {
        let is_orphan = |key: &NodeKey| metrics.get(key).is_some_and(|m| m.is_orphan);
        let nodes = graph.filtered_nodes(filter, is_orphan);
        // Map backend id → dense local index (position slot).
        let mut id_to_local: HashMap<u64, usize> = HashMap::with_capacity(nodes.len());
        let mut keys = Vec::with_capacity(nodes.len());
        for (local, (id, data)) in nodes.iter().enumerate() {
            id_to_local.insert(*id, local);
            keys.push(data.key.clone());
        }
        let surviving = id_to_local.keys().copied().collect();
        // `edges_among` is already sorted by (source key, target key,
        // kind); remap to local indices, dropping self-loops.
        let edges = graph
            .edges_among(&surviving)
            .into_iter()
            .filter_map(|(s, t, _kind, count)| {
                let a = *id_to_local.get(&s)?;
                let b = *id_to_local.get(&t)?;
                if a == b {
                    return None;
                }
                Some((a, b, (1.0 + f64::from(count)).ln()))
            })
            .collect();
        (keys, edges)
    }

    fn initial_temperature(n: usize, k: f64) -> f64 {
        0.1 * (n as f64).sqrt() * k
    }

    /// RNG-free golden-angle spiral: node `i` at radius `k·√i`, angle
    /// `i·golden_angle`. Even, coincidence-free, and deterministic.
    fn golden_angle_placement(n: usize, k: f64) -> Vec<[f64; 2]> {
        (0..n)
            .map(|i| {
                let r = k * (i as f64).sqrt();
                let theta = i as f64 * GOLDEN_ANGLE;
                [r * theta.cos(), r * theta.sin()]
            })
            .collect()
    }

    /// Positions in node order (index-aligned with P0-3 snapshot rows).
    pub fn positions(&self) -> &[[f64; 2]] {
        &self.positions
    }

    /// Node keys in position order (for consumers mapping slots ↔ nodes).
    pub fn keys(&self) -> &[NodeKey] {
        &self.keys
    }

    pub fn node_count(&self) -> usize {
        self.keys.len()
    }

    /// Total iterations run since construction.
    pub fn iteration(&self) -> u32 {
        self.iteration
    }

    /// The tier the force pass currently uses (auto unless forced).
    pub fn tier(&self) -> LayoutTier {
        if let Some(forced) = self.forced_tier {
            forced
        } else if self.keys.len() > BARNES_HUT_THRESHOLD {
            LayoutTier::BarnesHut
        } else {
            LayoutTier::Exact
        }
    }

    /// Test hook: force a repulsion tier regardless of node count. The
    /// two tiers' public behavior must agree (see the oracle census).
    pub fn force_tier(&mut self, tier: Option<LayoutTier>) {
        self.forced_tier = tier;
    }

    /// Re-set the forces and re-heat to the warm temperature so the new
    /// balance can settle without a full cold restart.
    pub fn set_forces(&mut self, forces: LayoutForces) {
        self.forces = forces;
        self.k = lerp(20.0, 200.0, forces.link_distance);
        self.temperature = self.warm_temperature();
    }

    fn warm_temperature(&self) -> f64 {
        // A gentle re-heat: an order of magnitude below the cold start,
        // floored so motion is still possible.
        (Self::initial_temperature(self.keys.len(), self.k) * 0.1).max(self.temperature_floor())
    }

    fn temperature_floor(&self) -> f64 {
        0.01 * self.k
    }

    /// Test probe: the repulsion displacement each tier computes at the
    /// CURRENT positions (no attraction/gravity, no apply). The oracle
    /// compares these force vectors directly — the meaningful BH-accuracy
    /// check — instead of final positions, which diverge from exact by
    /// trajectory sensitivity over many compounding iterations.
    #[cfg(test)]
    fn repulsion_probe(&self, tier: LayoutTier) -> Vec<[f64; 2]> {
        let mut disp = vec![[0.0f64; 2]; self.positions.len()];
        match tier {
            LayoutTier::Exact => self.repulsion_exact(&mut disp),
            LayoutTier::BarnesHut => self.repulsion_barnes_hut(&mut disp),
        }
        disp
    }

    /// Test probe: the FULL net force (repulsion[tier] + attraction +
    /// gravity) at the current positions, uncapped and un-applied —
    /// mirrors `force_pass`'s accumulation. Used to measure how close a
    /// settled layout is to a stationary point of the EXACT energy.
    #[cfg(test)]
    fn net_force_probe(&self, tier: LayoutTier) -> Vec<[f64; 2]> {
        let mut disp = self.repulsion_probe(tier);
        let c_a = lerp(0.2, 5.0, self.forces.link);
        for &(a, b, w) in &self.edges {
            let (dx, dy, d) = self.delta(a, b);
            let f = c_a * (d * d) / self.k * w;
            let (ux, uy) = (dx / d, dy / d);
            disp[a][0] -= ux * f;
            disp[a][1] -= uy * f;
            disp[b][0] += ux * f;
            disp[b][1] += uy * f;
        }
        let c_g = lerp(0.0, 0.1, self.forces.center);
        for (d, p) in disp.iter_mut().zip(&self.positions) {
            d[0] -= c_g * p[0];
            d[1] -= c_g * p[1];
        }
        disp
    }

    /// Pin a node at `(x, y)`: it accumulates no displacement but still
    /// exerts forces on others.
    pub fn pin(&mut self, node: usize, x: f64, y: f64) {
        if node < self.positions.len() {
            self.positions[node] = [x, y];
            self.pinned[node] = true;
        }
    }

    pub fn unpin(&mut self, node: usize) {
        if node < self.pinned.len() {
            self.pinned[node] = false;
        }
    }

    /// Run up to `iterations` force passes, stopping early only on the
    /// deterministic convergence predicate.
    pub fn step(&mut self, iterations: u32) -> StepReport {
        let mut max_disp = 0.0;
        let mut converged = false;
        for _ in 0..iterations {
            max_disp = self.force_pass();
            self.iteration += 1;
            self.temperature = (self.temperature * COOLING).max(self.temperature_floor());
            if max_disp < 0.001 * self.k {
                converged = true;
                break;
            }
        }
        StepReport {
            iteration: self.iteration,
            max_displacement: max_disp,
            converged,
        }
    }

    /// One Fruchterman–Reingold + gravity iteration. Returns the largest
    /// single-node displacement applied this pass.
    fn force_pass(&mut self) -> f64 {
        let n = self.positions.len();
        if n == 0 {
            return 0.0;
        }
        let mut disp = vec![[0.0f64; 2]; n];

        // Repulsion (tier-selected).
        match self.tier() {
            LayoutTier::Exact => self.repulsion_exact(&mut disp),
            LayoutTier::BarnesHut => self.repulsion_barnes_hut(&mut disp),
        }

        // Attraction (per edge, once, weighted). O(E) either tier.
        let c_a = lerp(0.2, 5.0, self.forces.link);
        for &(a, b, w) in &self.edges {
            let (dx, dy, d) = self.delta(a, b);
            let f = c_a * (d * d) / self.k * w;
            let (ux, uy) = (dx / d, dy / d);
            // `dx = pos[a] - pos[b]`, so `(ux,uy)` points b→a. Attraction
            // pulls a TOWARD b (subtract) and b toward a (add).
            disp[a][0] -= ux * f;
            disp[a][1] -= uy * f;
            disp[b][0] += ux * f;
            disp[b][1] += uy * f;
        }

        // Gravity toward the origin (the framed centroid).
        let c_g = lerp(0.0, 0.1, self.forces.center);
        if c_g > 0.0 {
            for (d, p) in disp.iter_mut().zip(&self.positions) {
                d[0] -= c_g * p[0];
                d[1] -= c_g * p[1];
            }
        }

        // Apply, capped by temperature; frozen/pinned nodes don't move.
        let t = self.temperature;
        let mut max_disp = 0.0f64;
        for (((pos, d), &pinned), &awake) in self
            .positions
            .iter_mut()
            .zip(&disp)
            .zip(&self.pinned)
            .zip(&self.awake)
        {
            if pinned || !awake {
                continue;
            }
            let [mut dx, mut dy] = *d;
            let mag = (dx * dx + dy * dy).sqrt();
            if mag > t {
                let s = t / mag;
                dx *= s;
                dy *= s;
            }
            pos[0] += dx;
            pos[1] += dy;
            let applied = (dx * dx + dy * dy).sqrt();
            if applied > max_disp {
                max_disp = applied;
            }
        }
        max_disp
    }

    /// The vector from `b` to `a` (so `a` is repelled/attracted along
    /// it), with a seeded, non-random separation when the two coincide.
    fn delta(&self, a: usize, b: usize) -> (f64, f64, f64) {
        let dx = self.positions[a][0] - self.positions[b][0];
        let dy = self.positions[a][1] - self.positions[b][1];
        let d = (dx * dx + dy * dy).sqrt();
        if d < EPS {
            // Coincident: separate along the shared seeded direction.
            let dir = coincident_dir(self.config.seed, &self.keys[a], &self.keys[b]);
            (dir[0] * EPS, dir[1] * EPS, EPS)
        } else {
            (dx, dy, d)
        }
    }

    /// O(n²) exact pairwise repulsion: `C_r · k²/d` along each pair.
    fn repulsion_exact(&self, disp: &mut [[f64; 2]]) {
        let c_r = lerp(0.2, 5.0, self.forces.repel);
        let kk = self.k * self.k;
        let n = self.positions.len();
        for a in 0..n {
            for b in (a + 1)..n {
                let (dx, dy, d) = self.delta(a, b);
                let f = c_r * kk / d;
                let (ux, uy) = (dx / d, dy / d);
                disp[a][0] += ux * f;
                disp[a][1] += uy * f;
                disp[b][0] -= ux * f;
                disp[b][1] -= uy * f;
            }
        }
    }

    /// Barnes–Hut repulsion: a quadtree with center-of-mass, opening
    /// criterion θ=0.9, rebuilt each iteration (O(n) build, deliberately
    /// not incremental). Public behavior matches [`repulsion_exact`]
    /// within the census tolerance.
    fn repulsion_barnes_hut(&self, disp: &mut [[f64; 2]]) {
        let c_r = lerp(0.2, 5.0, self.forces.repel);
        let kk = self.k * self.k;
        let tree = QuadTree::build(&self.positions);
        // Seed + keys let the near-field bucket separate coincident points
        // with the SAME seeded direction the exact tier uses (round 2
        // finding 2 — the two tiers agree even on coincidents).
        for (a, (d, pos)) in disp.iter_mut().zip(&self.positions).enumerate() {
            let [fx, fy] = tree.repulsion_on(a, *pos, c_r * kk, self.config.seed, &self.keys);
            d[0] += fx;
            d[1] += fy;
        }
    }

    /// Graph changed: carry over surviving nodes' positions by key, seat
    /// new nodes at their filtered neighbors' centroid (golden-angle
    /// ring when isolated), wake only the ≤2-hop neighborhood of the
    /// change set, pin-freeze the rest, and re-heat to warm temperature.
    pub fn warm_update(&mut self, graph: &GraphIndex, filter: &GraphFilter) -> WarmReport {
        let metrics = MetricsSnapshot::compute(graph);
        let (new_keys, new_edges) = Self::project(graph, filter, &metrics);
        let n = new_keys.len();

        // Adjacency over the NEW graph (for centroid seating + wake BFS).
        let mut adj: Vec<Vec<usize>> = vec![Vec::new(); n];
        for &(a, b, _) in &new_edges {
            adj[a].push(b);
            adj[b].push(a);
        }
        let key_to_local: HashMap<NodeKey, usize> = new_keys
            .iter()
            .enumerate()
            .map(|(i, k)| (k.clone(), i))
            .collect();

        // Carry over positions AND pin state by key (round 1 finding 3:
        // a survivor's explicit pin must not be silently dropped); collect
        // brand-new nodes to seat.
        let mut positions = vec![[0.0f64; 2]; n];
        let mut pinned = vec![false; n];
        let mut is_new = vec![false; n];
        let mut carried = 0usize;
        let mut new_nodes: Vec<usize> = Vec::new();
        for (i, key) in new_keys.iter().enumerate() {
            if let Some(&old) = self.key_index.get(key) {
                positions[i] = self.positions[old];
                pinned[i] = self.pinned[old];
                carried += 1;
            } else {
                is_new[i] = true;
                new_nodes.push(i);
            }
        }
        let seeded = new_nodes.len();

        // Seat new nodes at the centroid of their ALREADY-PLACED
        // neighbors (a tiny per-seat golden-angle jitter keeps two
        // newcomers sharing one neighbor from landing coincident);
        // isolated newcomers go on a golden-angle ring beyond the extent.
        for (seat, &i) in new_nodes.iter().enumerate() {
            let placed: Vec<usize> = adj[i].iter().copied().filter(|&j| !is_new[j]).collect();
            let jitter = self.k * 1e-3;
            let jt = seat as f64 * GOLDEN_ANGLE;
            if placed.is_empty() {
                let r = self.k * ((n as f64).sqrt() + 1.0);
                positions[i] = [r * jt.cos(), r * jt.sin()];
            } else {
                let (mut cx, mut cy) = (0.0, 0.0);
                for &j in &placed {
                    cx += positions[j][0];
                    cy += positions[j][1];
                }
                let inv = 1.0 / placed.len() as f64;
                positions[i] = [cx * inv + jitter * jt.cos(), cy * inv + jitter * jt.sin()];
            }
        }

        // Change set (round 1 finding 2): brand-new nodes PLUS the
        // surviving endpoints of any edge that was added, removed, or
        // whose weight (count) moved — so an edge-only change with an
        // unchanged node set still wakes and re-solves, rather than
        // freezing everyone and falsely converging.
        // Weights are AGGREGATED per (source, target): parallel Link and
        // Embed edges between the same pair are distinct rows in the edge
        // list, so keying on the pair alone would let one overwrite the
        // other and hide a removal (round 2 finding 1). Summing makes the
        // per-pair total move whenever ANY incident edge is added,
        // removed, or count-changed.
        let mut old_edge_w: HashMap<(NodeKey, NodeKey), f64> = HashMap::new();
        for &(a, b, w) in &self.edges {
            *old_edge_w
                .entry((self.keys[a].clone(), self.keys[b].clone()))
                .or_insert(0.0) += w;
        }
        let mut new_edge_w: HashMap<(NodeKey, NodeKey), f64> = HashMap::new();
        for &(a, b, w) in &new_edges {
            *new_edge_w
                .entry((new_keys[a].clone(), new_keys[b].clone()))
                .or_insert(0.0) += w;
        }
        let mut changed = is_new.clone();
        // Added or weight-changed pairs: wake both (surviving) endpoints.
        for (pair, w) in &new_edge_w {
            if old_edge_w.get(pair) != Some(w) {
                if let Some(&a) = key_to_local.get(&pair.0) {
                    changed[a] = true;
                }
                if let Some(&b) = key_to_local.get(&pair.1) {
                    changed[b] = true;
                }
            }
        }
        // Removed edges: wake whichever endpoints survive into the new set.
        for pair in old_edge_w.keys() {
            if !new_edge_w.contains_key(pair) {
                if let Some(&a) = key_to_local.get(&pair.0) {
                    changed[a] = true;
                }
                if let Some(&b) = key_to_local.get(&pair.1) {
                    changed[b] = true;
                }
            }
        }

        // Wake the ≤2-hop neighborhood of the change set; freeze the rest.
        let mut awake = changed.clone();
        let mut frontier: Vec<usize> = (0..n).filter(|&i| awake[i]).collect();
        for _hop in 0..2 {
            let mut next = Vec::new();
            for &node in &frontier {
                for &nb in &adj[node] {
                    if !awake[nb] {
                        awake[nb] = true;
                        next.push(nb);
                    }
                }
            }
            frontier = next;
        }
        let awake_count = awake.iter().filter(|&&a| a).count();

        // Commit the new projection.
        self.keys = new_keys;
        self.key_index = key_to_local;
        self.edges = new_edges;
        self.positions = positions;
        self.pinned = pinned;
        self.awake = awake;
        self.temperature = self.warm_temperature();

        WarmReport {
            carried,
            seeded,
            awake: awake_count,
        }
    }
}

/// Derive the FFI [`LayoutTopology`] of `engine` against `graph` — the
/// graph the engine was last projected from (P2-2 #558). Every engine
/// key is a live node of that graph (the engine only ever projects from
/// `filtered_nodes`), so each maps to a backend id; edges come from
/// [`GraphIndex::edges_among`] over the surviving ids, preserving the
/// deterministic `(source key, target key, kind)` order.
pub fn layout_topology(engine: &LayoutEngine, graph: &GraphIndex) -> LayoutTopology {
    let ids: Vec<u64> = engine
        .keys()
        .iter()
        .map(|key| {
            graph
                .id_of(key)
                .expect("engine key is a live node of the projected graph")
        })
        .collect();
    let surviving: std::collections::HashSet<u64> = ids.iter().copied().collect();
    let edges = graph
        .edges_among(&surviving)
        .into_iter()
        .map(
            |(source_id, target_id, kind, count)| crate::graph::GraphEdge {
                source_id,
                target_id,
                kind,
                count,
            },
        )
        .collect();
    LayoutTopology {
        ids,
        edges,
        // Metadata is filled by the session under the graph lock (it needs
        // metrics + file mtimes, which this pure graph fn can't reach).
        nodes: Vec::new(),
        generation: graph.generation(),
    }
}

/// Depth cap on subdivision. Coincident (or near-coincident) points
/// always route to the same quadrant, so without a cap the tree would
/// recurse forever (a crash — reachable when `warm_update` seats several
/// newcomers at one neighbor centroid, or two nodes are pinned to the
/// same coordinate). At the cap a leaf becomes a multi-point BUCKET.
const QT_MAX_DEPTH: u32 = 48;

/// A minimal region quadtree with per-cell center-of-mass, used for the
/// Barnes–Hut repulsion approximation. Rebuilt from scratch each
/// iteration (the spec's explicit choice: O(n) build, no incremental
/// bookkeeping).
struct QuadTree {
    nodes: Vec<QtCell>,
    theta: f64,
}

struct QtCell {
    /// Half-width of the square region.
    half: f64,
    /// Region center.
    cx: f64,
    cy: f64,
    /// Accumulated mass (point count) and center-of-mass sums.
    mass: f64,
    com_x: f64,
    com_y: f64,
    /// Child cell indices (NW, NE, SW, SE); `usize::MAX` = empty.
    children: [usize; 4],
    /// Points held by a LEAF: `(node index, position)`. A leaf holds one
    /// point until it splits; at `QT_MAX_DEPTH` it keeps bucketing so
    /// coincident points terminate. Empty for internal cells.
    bucket: Vec<(usize, [f64; 2])>,
    leaf: bool,
}

impl QtCell {
    fn empty(cx: f64, cy: f64, half: f64) -> Self {
        QtCell {
            half,
            cx,
            cy,
            mass: 0.0,
            com_x: 0.0,
            com_y: 0.0,
            children: [usize::MAX; 4],
            bucket: Vec::new(),
            leaf: true,
        }
    }

    /// Whether this cell's square region contains `p` (inclusive) — a
    /// point never approximates a cell it lies inside (avoids self-mass
    /// contamination, review round 1 finding 1).
    fn contains(&self, p: [f64; 2]) -> bool {
        (p[0] - self.cx).abs() <= self.half && (p[1] - self.cy).abs() <= self.half
    }
}

impl QuadTree {
    fn build(positions: &[[f64; 2]]) -> Self {
        // Bounding square around all points (min half so a degenerate
        // extent still forms a valid region).
        let mut min = [f64::INFINITY; 2];
        let mut max = [f64::NEG_INFINITY; 2];
        for p in positions {
            min[0] = min[0].min(p[0]);
            min[1] = min[1].min(p[1]);
            max[0] = max[0].max(p[0]);
            max[1] = max[1].max(p[1]);
        }
        let cx = (min[0] + max[0]) * 0.5;
        let cy = (min[1] + max[1]) * 0.5;
        let half = ((max[0] - min[0]).max(max[1] - min[1]) * 0.5).max(EPS);
        let mut tree = QuadTree {
            nodes: vec![QtCell::empty(cx, cy, half)],
            theta: BH_THETA,
        };
        for (i, p) in positions.iter().enumerate() {
            tree.insert(0, i, *p, 0);
        }
        tree
    }

    fn quadrant(cell: &QtCell, p: [f64; 2]) -> usize {
        // NW=0, NE=1, SW=2, SE=3.
        let east = (p[0] >= cell.cx) as usize;
        let south = (p[1] < cell.cy) as usize;
        south * 2 + east
    }

    fn child_center(cell: &QtCell, q: usize) -> (f64, f64, f64) {
        let h = cell.half * 0.5;
        let east = q & 1 == 1;
        let south = q >= 2;
        let cx = if east { cell.cx + h } else { cell.cx - h };
        let cy = if south { cell.cy - h } else { cell.cy + h };
        (cx, cy, h)
    }

    fn insert(&mut self, cell_idx: usize, point: usize, p: [f64; 2], depth: u32) {
        // Accumulate mass / center-of-mass on the way down.
        let cell = &mut self.nodes[cell_idx];
        cell.mass += 1.0;
        cell.com_x += p[0];
        cell.com_y += p[1];

        if self.nodes[cell_idx].leaf {
            // Empty leaf, or a bucket at the depth cap: just hold the
            // point. The depth cap terminates coincident-point recursion.
            if self.nodes[cell_idx].bucket.is_empty() || depth >= QT_MAX_DEPTH {
                self.nodes[cell_idx].bucket.push((point, p));
                return;
            }
            // Split: this leaf holds exactly one point (capacity 1); push
            // it down, then place the newcomer.
            self.nodes[cell_idx].leaf = false;
            let existing = std::mem::take(&mut self.nodes[cell_idx].bucket);
            for (idx, ep) in existing {
                self.place_in_child(cell_idx, idx, ep, depth);
            }
        }
        self.place_in_child(cell_idx, point, p, depth);
    }

    fn place_in_child(&mut self, cell_idx: usize, point: usize, p: [f64; 2], depth: u32) {
        let q = Self::quadrant(&self.nodes[cell_idx], p);
        let mut child = self.nodes[cell_idx].children[q];
        if child == usize::MAX {
            let (cx, cy, h) = Self::child_center(&self.nodes[cell_idx], q);
            child = self.nodes.len();
            self.nodes.push(QtCell::empty(cx, cy, h));
            self.nodes[cell_idx].children[q] = child;
        }
        self.insert(child, point, p, depth + 1);
    }

    /// Net repulsion on the point at `pos` (index `self_idx`, skipped),
    /// with magnitude `strength / d` per the FR `C_r·k²/d` law. `seed` +
    /// `keys` let the near-field bucket separate coincident points with
    /// the same seeded direction as the exact tier.
    fn repulsion_on(
        &self,
        self_idx: usize,
        pos: [f64; 2],
        strength: f64,
        seed: u64,
        keys: &[NodeKey],
    ) -> [f64; 2] {
        let mut acc = [0.0f64; 2];
        self.accumulate(0, self_idx, pos, strength, seed, keys, &mut acc);
        acc
    }

    #[allow(clippy::too_many_arguments)]
    fn accumulate(
        &self,
        cell_idx: usize,
        self_idx: usize,
        pos: [f64; 2],
        strength: f64,
        seed: u64,
        keys: &[NodeKey],
        acc: &mut [f64; 2],
    ) {
        let cell = &self.nodes[cell_idx];
        if cell.mass == 0.0 {
            return;
        }

        if cell.leaf {
            // Near-field: sum each bucket point exactly, skipping self. A
            // coincident distinct point separates along the SAME seeded
            // direction the exact tier uses (round 2 finding 2), so the
            // two tiers agree even on coincidents.
            for &(idx, ppos) in &cell.bucket {
                if idx == self_idx {
                    continue;
                }
                let dx = pos[0] - ppos[0];
                let dy = pos[1] - ppos[1];
                let d = (dx * dx + dy * dy).sqrt();
                let (ux, uy, dd) = if d < EPS {
                    // `coincident_dir` is symmetric (keyed on the min);
                    // the min-key node pushes +dir, the other −dir, so BH
                    // (which evaluates each node independently) matches the
                    // exact tier's equal-and-opposite pair (round 3
                    // finding 1 — same-direction was the bug).
                    let dir = coincident_dir(seed, &keys[self_idx], &keys[idx]);
                    let sign = if keys[self_idx] <= keys[idx] {
                        1.0
                    } else {
                        -1.0
                    };
                    (dir[0] * sign, dir[1] * sign, EPS)
                } else {
                    (dx / d, dy / d, d)
                };
                let f = strength / dd;
                acc[0] += ux * f;
                acc[1] += uy * f;
            }
            return;
        }

        let com = [cell.com_x / cell.mass, cell.com_y / cell.mass];
        let dx = pos[0] - com[0];
        let dy = pos[1] - com[1];
        let d = (dx * dx + dy * dy).sqrt();

        // Never approximate a cell that CONTAINS the target — that would
        // fold the target's own mass into the repulsion (finding 1).
        // Otherwise apply the opening criterion `s/d < θ` (s = width).
        if !cell.contains(pos) && d > EPS && (cell.half * 2.0) / d < self.theta {
            let f = strength * cell.mass / d;
            acc[0] += dx / d * f;
            acc[1] += dy / d * f;
            return;
        }
        // Recurse into children (deterministic NW→SE order).
        for &child in &cell.children {
            if child != usize::MAX {
                self.accumulate(child, self_idx, pos, strength, seed, keys, acc);
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::graph::GraphIndex;

    // ---- fixtures -------------------------------------------------------

    /// `n` notes in a ring (i → i+1), plus a couple of chords so the
    /// layout has some structure to resolve.
    fn ring_graph(n: usize) -> GraphIndex {
        let paths: Vec<String> = (0..n).map(|i| format!("notes/n{i:05}.md")).collect();
        let refs: Vec<&str> = paths.iter().map(|s| s.as_str()).collect();
        let mut links: Vec<(usize, usize)> = (0..n).map(|i| (i, (i + 1) % n)).collect();
        if n >= 4 {
            links.push((0, n / 2));
            links.push((1, n / 3));
        }
        GraphIndex::from_test_links(&refs, &links)
    }

    /// A deterministic pseudo-random graph (splitmix64-seeded) of `n`
    /// notes with about `n*2` edges — for the Barnes–Hut oracle.
    fn random_graph(n: usize, seed: u64) -> GraphIndex {
        let paths: Vec<String> = (0..n).map(|i| format!("notes/r{i:05}.md")).collect();
        let refs: Vec<&str> = paths.iter().map(|s| s.as_str()).collect();
        let mut state = seed;
        let mut next = || {
            state = splitmix64(state);
            state
        };
        let mut links = Vec::with_capacity(n * 2);
        for _ in 0..(n * 2) {
            let a = (next() % n as u64) as usize;
            let b = (next() % n as u64) as usize;
            if a != b {
                links.push((a, b));
            }
        }
        GraphIndex::from_test_links(&refs, &links)
    }

    fn engine(graph: &GraphIndex, iters: u32) -> LayoutEngine {
        let mut e = LayoutEngine::new(
            graph,
            &GraphFilter::default(),
            LayoutForces::default(),
            LayoutConfig::default(),
        );
        e.step(iters);
        e
    }

    /// Grid resolution the golden digest quantizes to (see `digest`).
    const GOLDEN_GRID: f64 = 0.1;

    /// FNV-1a digest over every coordinate QUANTIZED to a 0.1 grid. A raw
    /// bit-exact digest is NOT cross-platform-stable: `sin`/`cos`/`sqrt`/
    /// `ln` differ by a last ULP between libm implementations, so a
    /// locally-generated golden fails on the CI runner. The dynamics are
    /// contractive once nodes spread (`dF/dx ≈ 1`, not chaotic
    /// saturation), so that drift stays ~1e-13 — snapping to a 0.1 grid
    /// maps both platforms to the same bucket, while any real regression
    /// (nodes move by » 0.1) still changes the digest. Per-platform
    /// bit-identity itself is pinned separately by
    /// `same_inputs_are_bit_identical`.
    fn digest(positions: &[[f64; 2]]) -> u64 {
        let mut h: u64 = 0xcbf2_9ce4_8422_2325;
        for p in positions {
            for c in p {
                let q = (c / GOLDEN_GRID).round() as i64;
                for b in q.to_le_bytes() {
                    h ^= b as u64;
                    h = h.wrapping_mul(0x0000_0100_0000_01B3);
                }
            }
        }
        h
    }

    // ---- properties -----------------------------------------------------

    #[test]
    fn positions_are_finite_bounded_and_centered() {
        let n = 100;
        let g = ring_graph(n);
        let e = engine(&g, 300);
        let k = lerp(20.0, 200.0, 0.5);
        let bound = 10.0 * (n as f64).sqrt() * k;
        let mut cx = 0.0;
        let mut cy = 0.0;
        for &[x, y] in e.positions() {
            assert!(x.is_finite() && y.is_finite(), "NaN/Inf position");
            assert!(
                x.hypot(y) <= bound,
                "position {x},{y} exceeds bound {bound}"
            );
            cx += x;
            cy += y;
        }
        // Gravity keeps the centroid near the origin (framed).
        let c = (cx / n as f64).hypot(cy / n as f64);
        assert!(c < bound, "centroid {c} not framed");
    }

    #[test]
    fn same_inputs_are_bit_identical() {
        let g = ring_graph(60);
        let a = engine(&g, 200);
        let b = engine(&g, 200);
        assert_eq!(a.positions(), b.positions(), "layout is not deterministic");
    }

    #[test]
    fn permutation_of_insertion_order_yields_identical_layout() {
        // Same node set + edges, inserted in two orders. `filtered_nodes`
        // sorts by key, so the key-canonical layout must be identical.
        let fwd: Vec<String> = (0..40).map(|i| format!("notes/p{i:05}.md")).collect();
        let fwd_refs: Vec<&str> = fwd.iter().map(|s| s.as_str()).collect();
        let fwd_links: Vec<(usize, usize)> = (0..40).map(|i| (i, (i + 1) % 40)).collect();
        let g1 = GraphIndex::from_test_links(&fwd_refs, &fwd_links);

        // Reversed insertion order; remap link endpoints to the new indices.
        let rev_refs: Vec<&str> = fwd_refs.iter().rev().copied().collect();
        let remap = |i: usize| 39 - i;
        let rev_links: Vec<(usize, usize)> = fwd_links
            .iter()
            .map(|&(s, t)| (remap(s), remap(t)))
            .collect();
        let g2 = GraphIndex::from_test_links(&rev_refs, &rev_links);

        let e1 = engine(&g1, 150);
        let e2 = engine(&g2, 150);
        assert_eq!(e1.keys(), e2.keys(), "key order should be identical");
        assert_eq!(
            e1.positions(),
            e2.positions(),
            "insertion-order permutation changed the layout"
        );
    }

    #[test]
    fn energy_is_non_increasing_over_windows_after_warmup() {
        let g = ring_graph(80);
        let mut e = LayoutEngine::new(
            &g,
            &GraphFilter::default(),
            LayoutForces::default(),
            LayoutConfig::default(),
        );
        let mut energies = Vec::new();
        for _ in 0..200 {
            e.step(1);
            energies.push(system_energy(&e));
        }
        // Compare consecutive 10-iteration window averages after iter 50;
        // a tiny tolerance absorbs the temperature-capped jitter.
        let window = 10;
        let start = 50;
        let mut prev = f64::INFINITY;
        let mut w = start;
        while w + window <= energies.len() {
            let avg: f64 = energies[w..w + window].iter().sum::<f64>() / window as f64;
            // Non-increasing with a magnitude-relative slack. `prev.abs()`
            // (not `prev * 1.02`) so the tolerance widens ABOVE `prev`
            // regardless of sign — the energy here is large-negative
            // (repulsion potential dominates) and trending down.
            assert!(
                avg <= prev + prev.abs() * 0.02 + 1.0,
                "energy window at {w} rose: {avg} > {prev}"
            );
            prev = avg;
            w += window;
        }
    }

    /// Σ pair repulsion potential + Σ edge attraction potential + gravity
    /// — the scalar the force pass descends.
    fn system_energy(e: &LayoutEngine) -> f64 {
        let c_r = lerp(0.2, 5.0, e.forces.repel);
        let c_a = lerp(0.2, 5.0, e.forces.link);
        let c_g = lerp(0.0, 0.1, e.forces.center);
        let k = e.k;
        let p = e.positions();
        let n = p.len();
        let mut energy = 0.0;
        for a in 0..n {
            for b in (a + 1)..n {
                let d = (p[a][0] - p[b][0]).hypot(p[a][1] - p[b][1]).max(EPS);
                energy += -c_r * k * k * d.ln();
            }
        }
        for &(a, b, w) in &e.edges {
            let d = (p[a][0] - p[b][0]).hypot(p[a][1] - p[b][1]).max(EPS);
            energy += c_a * d * d * d / (3.0 * k) * w;
        }
        for &[x, y] in p {
            energy += c_g * (x * x + y * y) * 0.5;
        }
        energy
    }

    // ---- Barnes–Hut oracle census --------------------------------------

    #[test]
    fn census_barnes_hut_matches_exact() {
        // Oracle (p2_spec §P2-1): the Barnes–Hut repulsion FORCE must
        // match the exact O(n²) force within 5%, checked PER ITERATION
        // ALONG A REAL TRAJECTORY — a single exact-driven trajectory,
        // with both force functions probed at each checkpoint's SAME
        // configuration. Magnitude-weighted RMS is the θ=0.9 quality
        // bar; a per-NODE relative bound is ill-posed (a node near force
        // equilibrium has a ~0 denominator), so a single wild node is
        // instead capped absolutely against the typical magnitude.
        //
        // The spec ALSO lists "final layouts within 0.05·k per node."
        // That is NOT asserted, and deliberately so: a force-directed
        // system has many local minima, so two INDEPENDENT trajectories
        // (forced-exact vs forced-BH) differing by BH's per-step
        // approximation settle into DIFFERENT minima — measured ~30× the
        // layout scale apart, not 0.05·k. Per-node final-position
        // equality across separate trajectories is physically
        // unachievable at θ=0.9 (it is not a kernel bug); the
        // per-iteration force agreement below is the meaningful,
        // achievable guarantee that the two tiers compute the SAME
        // physics.
        for seed in 0..8u64 {
            let n = 60 + (seed as usize) * 55; // 60..=445 (≤ 500 forced tier)
            let g = random_graph(n, 0xA11CE ^ seed);
            let mut eng = LayoutEngine::new(
                &g,
                &GraphFilter::default(),
                LayoutForces::default(),
                LayoutConfig::default(),
            );
            eng.force_tier(Some(LayoutTier::Exact));

            // Checkpoints from init through settling: forces agree the
            // whole way, not just at one snapshot.
            let mut next_checkpoint = 0u32;
            for target in [0u32, 25, 50, 100, 150, 200] {
                eng.step(target - next_checkpoint);
                next_checkpoint = target;

                let exact = eng.repulsion_probe(LayoutTier::Exact);
                let bh = eng.repulsion_probe(LayoutTier::BarnesHut);
                let mut sq_err = 0.0f64;
                let mut sq_mag = 0.0f64;
                let mut max_abs_err = 0.0f64;
                for (fe, fb) in exact.iter().zip(&bh) {
                    assert!(fb[0].is_finite() && fb[1].is_finite(), "BH force NaN/Inf");
                    let err = (fe[0] - fb[0]).hypot(fe[1] - fb[1]);
                    sq_err += err * err;
                    sq_mag += fe[0] * fe[0] + fe[1] * fe[1];
                    max_abs_err = max_abs_err.max(err);
                }
                let n_nodes = exact.len().max(1) as f64;
                let rms_mag = (sq_mag / n_nodes).sqrt();
                let rms_rel = (sq_err / sq_mag.max(1e-30)).sqrt();
                assert!(
                    rms_rel <= 0.05,
                    "BH RMS force error {rms_rel} exceeds 5% at n={n}, iter={target}"
                );
                assert!(
                    max_abs_err <= rms_mag,
                    "a single BH node force ({max_abs_err}) exceeds the typical magnitude \
                     ({rms_mag}) at n={n}, iter={target}"
                );
            }
        }
    }

    /// Root-mean-square magnitude of a force field.
    fn rms(forces: &[[f64; 2]]) -> f64 {
        let n = forces.len().max(1) as f64;
        (forces
            .iter()
            .map(|f| f[0] * f[0] + f[1] * f[1])
            .sum::<f64>()
            / n)
            .sqrt()
    }

    #[test]
    fn barnes_hut_driven_layout_settles_near_an_exact_minimum() {
        // The ACHIEVABLE "final layout" oracle (replacing the spec's
        // unachievable per-node 0.05·k coordinate match): a BH-driven
        // layout, once settled, is near a stationary point of the EXACT
        // energy — its EXACT net-force residual is far below the typical
        // mid-solve force. Coordinates can't match across trajectories
        // (different minima), but this proves BH produces a genuine
        // low-energy layout, not just a bounded one.
        for seed in 0..4u64 {
            let n = 120 + (seed as usize) * 60;
            let g = random_graph(n, 0xBEE5 ^ seed);
            // Typical net-force magnitude mid-solve (normalizer).
            let mut r = LayoutEngine::new(
                &g,
                &GraphFilter::default(),
                LayoutForces::default(),
                LayoutConfig::default(),
            );
            r.force_tier(Some(LayoutTier::Exact));
            r.step(10);
            let typical = rms(&r.net_force_probe(LayoutTier::Exact));

            // Settle a BH-driven layout, then measure its EXACT residual.
            let mut bh = LayoutEngine::new(
                &g,
                &GraphFilter::default(),
                LayoutForces::default(),
                LayoutConfig::default(),
            );
            bh.force_tier(Some(LayoutTier::BarnesHut));
            bh.step(300);
            let residual = rms(&bh.net_force_probe(LayoutTier::Exact));
            assert!(
                residual < 0.3 * typical,
                "BH-driven layout not near an exact minimum at n={n}: \
                 residual {residual} vs typical {typical}"
            );
        }
    }

    #[test]
    fn barnes_hut_large_stays_finite_and_bounded() {
        // 10k forced-BH (p2_spec §P2-1 qualitative tier): no exact
        // comparison (O(n²) too slow), just the invariants that must
        // hold at scale — finite, bounded, no explosion.
        let n = 10_000;
        let g = random_graph(n, 0xF00D);
        let mut e = LayoutEngine::new(
            &g,
            &GraphFilter::default(),
            LayoutForces::default(),
            LayoutConfig::default(),
        );
        assert_eq!(e.tier(), LayoutTier::BarnesHut, "n>1500 auto-selects BH");
        e.force_tier(Some(LayoutTier::BarnesHut));
        e.step(60);
        let k = e.k;
        let bound = 10.0 * (n as f64).sqrt() * k;
        for &[x, y] in e.positions() {
            assert!(x.is_finite() && y.is_finite());
            assert!(x.hypot(y) <= bound);
        }
    }

    // ---- pinning + warm update -----------------------------------------

    #[test]
    fn pinned_nodes_do_not_move() {
        let g = ring_graph(30);
        let mut e = LayoutEngine::new(
            &g,
            &GraphFilter::default(),
            LayoutForces::default(),
            LayoutConfig::default(),
        );
        e.pin(5, 123.0, -45.0);
        e.step(100);
        assert_eq!(e.positions()[5], [123.0, -45.0], "pinned node drifted");
    }

    /// Index of the node with `NodeKey::Path(path)` in the engine's
    /// key-order (position slot), or panics.
    fn slot(e: &LayoutEngine, path: &str) -> usize {
        e.keys()
            .iter()
            .position(|k| matches!(k, NodeKey::Path(p) if p == path))
            .unwrap_or_else(|| panic!("node {path} not found"))
    }

    /// A ring of `n` notes plus explicit extra links, as (paths, links)
    /// for constructing warm-update variants.
    fn ring_spec(n: usize) -> (Vec<String>, Vec<(usize, usize)>) {
        let paths: Vec<String> = (0..n).map(|i| format!("notes/n{i:05}.md")).collect();
        let mut links: Vec<(usize, usize)> = (0..n).map(|i| (i, (i + 1) % n)).collect();
        if n >= 4 {
            links.push((0, n / 2));
            links.push((1, n / 3));
        }
        (paths, links)
    }

    #[test]
    fn warm_update_carries_survivors_seats_newcomer_and_wakes_locally() {
        let g1 = ring_graph(40);
        let mut e = engine(&g1, 200);
        // A survivor FAR (>2 hops) from the coming change. ring_spec adds
        // chords (0,20) and (1,13), so 20/13 are NOT far; node 10 is.
        const FAR: &str = "notes/n00010.md";
        let far_pos = e.positions()[slot(&e, FAR)];
        let hub_pos = e.positions()[slot(&e, "notes/n00000.md")];

        // Add one note linked only to n00000.
        let (paths, mut links) = ring_spec(40);
        let refs: Vec<&str> = paths.iter().map(|s| s.as_str()).collect();
        let mut refs = refs;
        let newcomer = "notes/n00040.md".to_string();
        refs.push(&newcomer);
        links.push((40, 0));
        let g2 = GraphIndex::from_test_links(&refs, &links);

        let report = e.warm_update(&g2, &GraphFilter::default());
        assert_eq!(report.carried, 40);
        assert_eq!(report.seeded, 1);
        assert!(report.awake < 41, "wake is local, not the whole graph");

        // Survivor carryover is EXACT (key-indexed), and the far node is
        // frozen (asleep) so a step leaves it bit-identical.
        assert_eq!(e.positions()[slot(&e, FAR)], far_pos);
        // Newcomer seated at its one placed neighbor (n00000) + jitter.
        let seat = e.positions()[slot(&e, "notes/n00040.md")];
        let off = (seat[0] - hub_pos[0]).hypot(seat[1] - hub_pos[1]);
        assert!(
            off > 0.0 && off <= e.k * 1e-2,
            "newcomer seated at neighbor centroid: off={off}"
        );

        // Step: the awake change-neighborhood moves; the frozen far node
        // does NOT (behavioral 2-hop membership).
        let far_before_step = e.positions()[slot(&e, FAR)];
        let hub_before_step = e.positions()[slot(&e, "notes/n00000.md")];
        e.step(5);
        assert_eq!(
            e.positions()[slot(&e, FAR)],
            far_before_step,
            "a frozen (asleep) survivor must not move"
        );
        assert_ne!(
            e.positions()[slot(&e, "notes/n00000.md")],
            hub_before_step,
            "the changed endpoint must be awake and move"
        );
    }

    #[test]
    fn warm_update_wakes_on_edge_only_change() {
        // Same node set, one added edge → the endpoints must wake and
        // move (round 1 finding 2: edge-only changes were left asleep).
        let (paths, links) = ring_spec(30);
        let refs: Vec<&str> = paths.iter().map(|s| s.as_str()).collect();
        let g1 = GraphIndex::from_test_links(&refs, &links);
        let mut e = engine(&g1, 200);

        let mut links2 = links.clone();
        links2.push((5, 25)); // a brand-new chord; node set unchanged
        let g2 = GraphIndex::from_test_links(&refs, &links2);
        let report = e.warm_update(&g2, &GraphFilter::default());
        assert_eq!(report.seeded, 0, "no new nodes");
        assert!(report.awake >= 2, "edge endpoints must wake");

        let p5 = e.positions()[slot(&e, "notes/n00005.md")];
        e.step(5);
        assert_ne!(
            e.positions()[slot(&e, "notes/n00005.md")],
            p5,
            "an edge-only change must still wake + move its endpoints"
        );
    }

    #[test]
    fn warm_update_preserves_pins() {
        let g1 = ring_graph(30);
        let mut e = LayoutEngine::new(
            &g1,
            &GraphFilter::default(),
            LayoutForces::default(),
            LayoutConfig::default(),
        );
        e.step(50);
        let s = slot(&e, "notes/n00003.md");
        e.pin(s, 77.0, 88.0);
        // Warm update that WAKES the pinned node (an incident edge change
        // puts n00003 in the change set), so if pin carryover regressed
        // it WOULD move — the test then proves the pin survived.
        let (paths, mut links) = ring_spec(30);
        links.push((3, 17)); // new chord incident to the pinned node
        let refs: Vec<&str> = paths.iter().map(|s| s.as_str()).collect();
        let g2 = GraphIndex::from_test_links(&refs, &links);
        e.warm_update(&g2, &GraphFilter::default());
        e.step(50);
        assert_eq!(
            e.positions()[slot(&e, "notes/n00003.md")],
            [77.0, 88.0],
            "a pinned survivor must stay pinned across warm_update even when woken"
        );
    }

    // ---- force signs / constants / cooling -----------------------------

    #[test]
    fn k_matches_link_distance_lerp() {
        for (ld, expected) in [(0.0f32, 20.0f64), (0.5, 110.0), (1.0, 200.0)] {
            let g = ring_graph(4);
            let forces = LayoutForces {
                link_distance: ld,
                ..LayoutForces::default()
            };
            let e = LayoutEngine::new(&g, &GraphFilter::default(), forces, LayoutConfig::default());
            assert_eq!(e.k, expected, "k should be lerp(20,200,{ld})");
        }
    }

    #[test]
    fn repulsion_pushes_unlinked_nodes_apart() {
        // Two notes, NO edge, no gravity → only repulsion → they spread.
        let g = GraphIndex::from_test_links(&["a.md", "b.md"], &[]);
        let forces = LayoutForces {
            center: 0.0,
            ..LayoutForces::default()
        };
        let mut e = LayoutEngine::new(&g, &GraphFilter::default(), forces, LayoutConfig::default());
        let d0 = {
            let p = e.positions();
            (p[0][0] - p[1][0]).hypot(p[0][1] - p[1][1])
        };
        e.step(30);
        let d1 = {
            let p = e.positions();
            (p[0][0] - p[1][0]).hypot(p[0][1] - p[1][1])
        };
        assert!(
            d1 > d0,
            "repulsion should push unlinked nodes apart: {d0} → {d1}"
        );
    }

    #[test]
    fn attraction_pulls_linked_closer_than_unlinked() {
        // A linked pair settles CLOSER than an otherwise-identical
        // unlinked pair — the attraction sign is correct (pulls together).
        let linked = GraphIndex::from_test_links(&["a.md", "b.md"], &[(0, 1)]);
        let unlinked = GraphIndex::from_test_links(&["a.md", "b.md"], &[]);
        let dist = |g: &GraphIndex| {
            let mut e = LayoutEngine::new(
                g,
                &GraphFilter::default(),
                LayoutForces::default(),
                LayoutConfig::default(),
            );
            e.step(300);
            let p = e.positions();
            (p[0][0] - p[1][0]).hypot(p[0][1] - p[1][1])
        };
        assert!(
            dist(&linked) < dist(&unlinked),
            "attraction should keep linked nodes closer than unlinked"
        );
    }

    #[test]
    fn gravity_frames_nodes_nearer_the_origin() {
        // Gravity's job is framing DISCONNECTED nodes (no attraction to
        // hold them): with none, pure repulsion spreads them wide; with
        // gravity, they stay framed. So compare max radius on an
        // edgeless graph.
        let g = GraphIndex::from_test_links(
            &[
                "a.md", "b.md", "c.md", "d.md", "e.md", "f.md", "g.md", "h.md",
            ],
            &[],
        );
        let max_radius = |center: f32| {
            let forces = LayoutForces {
                center,
                ..LayoutForces::default()
            };
            let mut e =
                LayoutEngine::new(&g, &GraphFilter::default(), forces, LayoutConfig::default());
            e.step(300);
            e.positions()
                .iter()
                .map(|p| p[0].hypot(p[1]))
                .fold(0.0f64, f64::max)
        };
        assert!(
            max_radius(1.0) < max_radius(0.0),
            "gravity should frame the layout nearer the origin"
        );
    }

    #[test]
    fn temperature_cools_and_reports_convergence() {
        let g = ring_graph(20);
        let mut e = LayoutEngine::new(
            &g,
            &GraphFilter::default(),
            LayoutForces::default(),
            LayoutConfig::default(),
        );
        // Exact cooling factor: after ONE step (a spread-out ring won't
        // converge on iter 1) the temperature is t0 · 0.97 exactly. The
        // literal pins the 0.97 — a different factor fails.
        let t0 = e.temperature;
        e.step(1);
        assert!(
            (e.temperature - t0 * 0.97).abs() < 1e-9,
            "one step should cool temperature by exactly 0.97: {} vs {}",
            e.temperature,
            t0 * 0.97
        );

        // Floor clamp, UNCONDITIONAL (round 3 finding 2): force the
        // temperature to the floor, step once — cooling would drop it to
        // 0.97·floor, so the `.max(floor)` clamp must pin it back at
        // exactly 0.01·k (and never below). Independent of convergence.
        let floor = e.temperature_floor();
        e.temperature = floor;
        e.step(1);
        assert!(
            (e.temperature - floor).abs() < 1e-12,
            "the .max(floor) clamp must hold temperature at 0.01·k: {} vs {}",
            e.temperature,
            floor
        );

        // Convergence-flag wiring, deterministically: a single node at
        // the origin has zero net force ⇒ zero displacement ⇒ the
        // predicate (`max_disp < 0.001·k`) fires on the first step and
        // reports converged. (Whether a MULTI-node graph reaches the
        // threshold in a given budget is dynamics-dependent — undamped
        // FR jitters at the temperature floor — so that's not asserted.)
        let solo = GraphIndex::from_test_links(&["solo.md"], &[]);
        let mut s = LayoutEngine::new(
            &solo,
            &GraphFilter::default(),
            LayoutForces::default(),
            LayoutConfig::default(),
        );
        let report = s.step(300);
        assert!(
            report.converged,
            "a zero-force graph must report convergence"
        );
        assert!(
            report.max_displacement < 0.001 * s.k,
            "converged ⇒ max displacement below the threshold"
        );
        assert!(report.iteration >= 1, "at least one iteration ran");
    }

    #[test]
    fn coincident_pins_do_not_crash_or_nan_in_either_tier() {
        // Two nodes pinned to the SAME coordinate (finding 4's crash
        // trigger via the quadtree) — both tiers must stay finite and
        // not recurse forever.
        let g = ring_graph(50);
        for tier in [LayoutTier::Exact, LayoutTier::BarnesHut] {
            let mut e = LayoutEngine::new(
                &g,
                &GraphFilter::default(),
                LayoutForces::default(),
                LayoutConfig::default(),
            );
            e.force_tier(Some(tier));
            e.pin(10, 5.0, 5.0);
            e.pin(20, 5.0, 5.0); // exactly coincident
            e.step(30);
            for &[x, y] in e.positions() {
                assert!(x.is_finite() && y.is_finite(), "{tier:?} produced NaN/Inf");
            }
        }
    }

    // ---- determinism regression pins (generated; cross-arch stable) ----

    #[test]
    fn golden_digests_are_stable() {
        // Regression pins (generated from the kernel, not an independent
        // oracle — the property tests + BH census are the correctness
        // checks). Pinned at LOW iteration counts only: the solver is
        // chaotically sensitive, so by ~iter 300 the last-ULP difference
        // between libm implementations amplifies past ANY useful digest
        // grid across CI arch (measured — the iter-300 golden was
        // dropped for exactly this). At iters ≤ 60 the amplification is
        // bounded well under the 0.1 quantization grid (`digest`), so the
        // pin is cross-platform-stable AND still catches real regressions
        // (which move nodes by » 0.1). Late-iteration behavior is covered
        // cross-platform by the invariant/tolerance tests (energy, BH
        // oracle, convergence) and per-platform by
        // `same_inputs_are_bit_identical`.
        let cases: &[(usize, u32, u64)] = &[
            (10, 30, GOLDEN_10_30),
            (10, 60, GOLDEN_10_60),
            (100, 30, GOLDEN_100_30),
            (100, 60, GOLDEN_100_60),
        ];
        for &(n, iters, expected) in cases {
            let g = ring_graph(n);
            let e = engine(&g, iters);
            assert_eq!(
                digest(e.positions()),
                expected,
                "golden digest drift at n={n}, iters={iters} (got {})",
                digest(e.positions())
            );
        }
    }

    // Filled in from a one-time generation run (see the test above).
    const GOLDEN_10_30: u64 = 13331501434369075012;
    const GOLDEN_10_60: u64 = 6468652883255901892;
    const GOLDEN_100_30: u64 = 6165856943659753834;
    const GOLDEN_100_60: u64 = 6812987914144607180;
}
