// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The `.slate/graph.json` schema and merge policy — W6-2 PR 0b (#746),
//! §W-G row F (contract 0b-12).
//!
//! The schema, its defaults and clamps, the version rule, the
//! unknown-key preservation and the refuse-to-clobber rules used to live
//! in `GraphConfigStore.swift`'s codec. They are core's now, as pure
//! functions of JSON text; the file I/O — atomic temp-and-rename, the
//! single-writer actor, its generation gate — stays host-designated
//! (0bD-3). Group precedence (first match wins) and the ring/palette
//! cycle for a new group are here too.
//!
//! The writer is CANONICAL by an explicit pass: the workspace enables
//! `serde_json/preserve_order`, so a `Map` is insertion-ordered and
//! nothing sorts implicitly. Every object's keys are sorted recursively
//! before serde's pretty printer runs (the recorded formatting
//! divergence from Foundation's printer is 0b-D5).

use crate::a11y::{GraphSurfaceMode, GraphVerbosity};
use crate::graph_queries::label_matches;
use serde_json::{Map, Value, json};

/// The schema version this code writes and the highest it reads.
pub const VERSION: u64 = 1;

/// The eight-slot palette (`GraphConfig.swift:109–126`). The colours
/// themselves are a rendering and stay host-side; the tokens, their
/// persistence tags and their titles are core's.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum GraphColorToken {
    Red,
    Orange,
    Yellow,
    Green,
    Teal,
    Blue,
    Purple,
    Pink,
}

impl GraphColorToken {
    pub const ALL: [GraphColorToken; 8] = [
        GraphColorToken::Red,
        GraphColorToken::Orange,
        GraphColorToken::Yellow,
        GraphColorToken::Green,
        GraphColorToken::Teal,
        GraphColorToken::Blue,
        GraphColorToken::Purple,
        GraphColorToken::Pink,
    ];

    pub fn tag(self) -> &'static str {
        match self {
            GraphColorToken::Red => "red",
            GraphColorToken::Orange => "orange",
            GraphColorToken::Yellow => "yellow",
            GraphColorToken::Green => "green",
            GraphColorToken::Teal => "teal",
            GraphColorToken::Blue => "blue",
            GraphColorToken::Purple => "purple",
            GraphColorToken::Pink => "pink",
        }
    }

    pub fn from_tag(tag: &str) -> Option<Self> {
        GraphColorToken::ALL
            .iter()
            .copied()
            .find(|t| t.tag() == tag)
    }

    /// The picker option (T71): the tag capitalised.
    pub fn title(self) -> &'static str {
        match self {
            GraphColorToken::Red => "Red",
            GraphColorToken::Orange => "Orange",
            GraphColorToken::Yellow => "Yellow",
            GraphColorToken::Green => "Green",
            GraphColorToken::Teal => "Teal",
            GraphColorToken::Blue => "Blue",
            GraphColorToken::Purple => "Purple",
            GraphColorToken::Pink => "Pink",
        }
    }
}

/// The ring styles that carry group membership without colour
/// (`GraphConfig.swift:130–142`); the dash patterns are a rendering.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum GraphRingStyle {
    Solid,
    Dashed,
    Double,
    Dotted,
}

impl GraphRingStyle {
    pub const ALL: [GraphRingStyle; 4] = [
        GraphRingStyle::Solid,
        GraphRingStyle::Dashed,
        GraphRingStyle::Double,
        GraphRingStyle::Dotted,
    ];

    pub fn tag(self) -> &'static str {
        match self {
            GraphRingStyle::Solid => "solid",
            GraphRingStyle::Dashed => "dashed",
            GraphRingStyle::Double => "double",
            GraphRingStyle::Dotted => "dotted",
        }
    }

    pub fn from_tag(tag: &str) -> Option<Self> {
        GraphRingStyle::ALL.iter().copied().find(|s| s.tag() == tag)
    }

    /// The picker option (T72): the tag capitalised.
    pub fn title(self) -> &'static str {
        match self {
            GraphRingStyle::Solid => "Solid",
            GraphRingStyle::Dashed => "Dashed",
            GraphRingStyle::Double => "Double",
            GraphRingStyle::Dotted => "Dotted",
        }
    }
}

/// One entry of the ordered palette (0b-12, design B): the token, its
/// persistence tag and its picker title. A host builds its picker and
/// its `allCases` from the vector, never from a literal.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphColorTokenSpec {
    pub token: GraphColorToken,
    pub tag: String,
    pub title: String,
}

/// One entry of the ordered ring styles (0b-12, design B).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphRingStyleSpec {
    pub style: GraphRingStyle,
    pub tag: String,
    pub title: String,
}

/// The ordered palette: `red, orange, yellow, green, teal, blue, purple,
/// pink` (T71).
pub fn color_tokens() -> Vec<GraphColorTokenSpec> {
    GraphColorToken::ALL
        .iter()
        .map(|t| GraphColorTokenSpec {
            token: *t,
            tag: t.tag().to_string(),
            title: t.title().to_string(),
        })
        .collect()
}

/// One entry of the ordered surface modes (0b-12, design B): the mode,
/// its `graph.json` tag and its switcher title — the shipped mac strings.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphSurfaceModeSpec {
    pub mode: GraphSurfaceMode,
    pub tag: String,
    pub title: String,
}

/// One entry of the ordered verbosity levels: the level, its `graph.json`
/// tag and its menu title (t0 §1.2's level names).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphVerbositySpec {
    pub verbosity: GraphVerbosity,
    pub tag: String,
    pub title: String,
}

pub const SURFACE_MODES: [GraphSurfaceMode; 2] =
    [GraphSurfaceMode::Table, GraphSurfaceMode::Diagram];
pub const VERBOSITIES: [GraphVerbosity; 3] = [
    GraphVerbosity::Terse,
    GraphVerbosity::Standard,
    GraphVerbosity::Verbose,
];

pub fn mode_title(mode: GraphSurfaceMode) -> &'static str {
    match mode {
        GraphSurfaceMode::Table => "Table",
        GraphSurfaceMode::Diagram => "Diagram",
    }
}

pub fn verbosity_title(verbosity: GraphVerbosity) -> &'static str {
    match verbosity {
        GraphVerbosity::Terse => "Terse",
        GraphVerbosity::Standard => "Standard",
        GraphVerbosity::Verbose => "Verbose",
    }
}

/// The ordered modes: `table, diagram`.
pub fn surface_modes() -> Vec<GraphSurfaceModeSpec> {
    SURFACE_MODES
        .iter()
        .map(|m| GraphSurfaceModeSpec {
            mode: *m,
            tag: mode_tag(*m).to_string(),
            title: mode_title(*m).to_string(),
        })
        .collect()
}

/// The ordered levels: `terse, standard, verbose`.
pub fn verbosities() -> Vec<GraphVerbositySpec> {
    VERBOSITIES
        .iter()
        .map(|v| GraphVerbositySpec {
            verbosity: *v,
            tag: verbosity_tag(*v).to_string(),
            title: verbosity_title(*v).to_string(),
        })
        .collect()
}

/// The ordered ring styles: `solid, dashed, double, dotted` (T72).
pub fn ring_styles() -> Vec<GraphRingStyleSpec> {
    GraphRingStyle::ALL
        .iter()
        .map(|s| GraphRingStyleSpec {
            style: *s,
            tag: s.tag().to_string(),
            title: s.title().to_string(),
        })
        .collect()
}

#[derive(Debug, Clone, PartialEq)]
pub struct GraphFilterConfig {
    pub include_attachments: bool,
    pub include_ghosts: bool,
    pub orphans_only: bool,
    /// The client-side label needle; empty = all.
    pub name_query: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphGroup {
    pub query: String,
    pub color_token: GraphColorToken,
    pub ring_style: GraphRingStyle,
}

#[derive(Debug, Clone, PartialEq)]
pub struct GraphDisplay {
    pub arrows: bool,
    pub text_fade_zoom: f64,
    pub node_size_multiplier: f64,
    pub link_thickness: f64,
}

#[derive(Debug, Clone, PartialEq)]
pub struct GraphForcesConfig {
    pub center: f64,
    pub repel: f64,
    pub link: f64,
    pub link_distance: f64,
}

/// The persisted graph-tab configuration, schema v1 plus the
/// `verbosity` key 0aD-6 reserved for it.
#[derive(Debug, Clone, PartialEq)]
pub struct GraphConfig {
    pub filters: GraphFilterConfig,
    pub groups: Vec<GraphGroup>,
    pub display: GraphDisplay,
    pub forces: GraphForcesConfig,
    pub mode: GraphSurfaceMode,
    /// Clamped 1..=3.
    pub connections_depth: u32,
    pub verbosity: GraphVerbosity,
}

impl Default for GraphConfig {
    fn default() -> Self {
        GraphConfig {
            filters: GraphFilterConfig {
                include_attachments: false,
                include_ghosts: true,
                orphans_only: false,
                name_query: String::new(),
            },
            groups: Vec::new(),
            display: GraphDisplay {
                arrows: false,
                text_fade_zoom: 0.55,
                node_size_multiplier: 1.0,
                link_thickness: 1.0,
            },
            forces: GraphForcesConfig {
                center: 0.5,
                repel: 0.5,
                link: 0.5,
                link_distance: 0.5,
            },
            mode: GraphSurfaceMode::Table,
            connections_depth: 1,
            verbosity: GraphVerbosity::Standard,
        }
    }
}

/// The default, as the free function the FFI exports; an absent file is
/// the host's case and it uses this.
pub fn default() -> GraphConfig {
    GraphConfig::default()
}

/// Why a config text could not be decoded or an existing file could
/// not be rewritten.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum GraphConfigError {
    /// Not a JSON object at the top level, not JSON at all, or a
    /// `version` that cannot be classified.
    Unparseable { reason: String },
    /// A newer Slate wrote this file; it is neither decoded nor rewritten.
    NewerVersion { version: u64 },
}

/// A decoded config with the top-level keys the schema does not own,
/// preserved for the next write.
#[derive(Debug, Clone, PartialEq)]
pub struct GraphConfigRead {
    pub config: GraphConfig,
    /// The canonical (sorted, compact) JSON text of an object holding the
    /// foreign top-level keys (`{}` when none).
    pub unknown_json: String,
}

const KNOWN_KEYS: [&str; 8] = [
    "version",
    "filters",
    "groups",
    "display",
    "forces",
    "mode",
    "connectionsDepth",
    "verbosity",
];

pub fn mode_tag(mode: GraphSurfaceMode) -> &'static str {
    match mode {
        GraphSurfaceMode::Table => "table",
        GraphSurfaceMode::Diagram => "diagram",
    }
}

pub fn mode_from_tag(tag: &str) -> Option<GraphSurfaceMode> {
    match tag {
        "table" => Some(GraphSurfaceMode::Table),
        "diagram" => Some(GraphSurfaceMode::Diagram),
        _ => None,
    }
}

pub fn verbosity_tag(verbosity: GraphVerbosity) -> &'static str {
    match verbosity {
        GraphVerbosity::Terse => "terse",
        GraphVerbosity::Standard => "standard",
        GraphVerbosity::Verbose => "verbose",
    }
}

pub fn verbosity_from_tag(tag: &str) -> Option<GraphVerbosity> {
    match tag {
        "terse" => Some(GraphVerbosity::Terse),
        "standard" => Some(GraphVerbosity::Standard),
        "verbose" => Some(GraphVerbosity::Verbose),
        _ => None,
    }
}

fn parse_object(json: &str) -> Result<Map<String, Value>, GraphConfigError> {
    let root: Value = serde_json::from_str(json).map_err(|e| GraphConfigError::Unparseable {
        reason: format!("invalid JSON: {e}"),
    })?;
    match root {
        Value::Object(map) => Ok(map),
        _ => Err(GraphConfigError::Unparseable {
            reason: "expected a JSON object at the top level".into(),
        }),
    }
}

/// The version rule (0b-12): absent → 1; a non-negative integral JSON
/// number → that value; anything else cannot be classified and is
/// `Unparseable`; a value above `VERSION` is `NewerVersion`.
fn check_version(root: &Map<String, Value>) -> Result<u64, GraphConfigError> {
    let version = match root.get("version") {
        None => VERSION,
        Some(Value::Number(n)) => match n.as_u64() {
            Some(v) => v,
            None => match n.as_f64() {
                Some(f)
                    if f.is_finite() && f >= 0.0 && f.fract() == 0.0 && f <= u64::MAX as f64 =>
                {
                    f as u64
                }
                _ => {
                    return Err(GraphConfigError::Unparseable {
                        reason: format!("version {n} is not a non-negative integer"),
                    });
                }
            },
        },
        Some(other) => {
            return Err(GraphConfigError::Unparseable {
                reason: format!("version {other} is not a number"),
            });
        }
    };
    if version > VERSION {
        return Err(GraphConfigError::NewerVersion { version });
    }
    Ok(version)
}

/// A number clamped into `lo..=hi`, `fallback` when absent, non-numeric
/// or non-finite (`GraphConfigStore.swift:196–202`).
fn clamp_d(value: Option<&Value>, lo: f64, hi: f64, fallback: f64) -> f64 {
    match value.and_then(Value::as_f64) {
        Some(d) if d.is_finite() => d.clamp(lo, hi),
        _ => fallback,
    }
}

/// The outbound clamp: `NaN` → the default, `±∞` → the bound, so the
/// file is always in range.
fn clamp_out(value: f64, lo: f64, hi: f64, fallback: f64) -> f64 {
    if value.is_nan() {
        fallback
    } else {
        value.clamp(lo, hi)
    }
}

fn bool_or(value: Option<&Value>, fallback: bool) -> bool {
    value.and_then(Value::as_bool).unwrap_or(fallback)
}

/// Decode a config text (`GraphConfigStore.swift:131–169`): a non-object
/// root is `Unparseable`; the version rule applies; every missing or
/// malformed section takes its default; numbers clamp; within `groups`
/// each invalid element is skipped on its own (0b-D6).
pub fn decode(json: &str) -> Result<GraphConfigRead, GraphConfigError> {
    let root = parse_object(json)?;
    check_version(&root)?;
    let mut config = GraphConfig::default();
    if let Some(Value::Object(f)) = root.get("filters") {
        config.filters = GraphFilterConfig {
            include_attachments: bool_or(f.get("includeAttachments"), false),
            include_ghosts: bool_or(f.get("includeGhosts"), true),
            orphans_only: bool_or(f.get("orphansOnly"), false),
            name_query: f
                .get("nameQuery")
                .and_then(Value::as_str)
                .unwrap_or("")
                .to_string(),
        };
    }
    if let Some(Value::Array(groups)) = root.get("groups") {
        config.groups = groups
            .iter()
            .filter_map(|g| {
                let g = g.as_object()?;
                let query = g.get("query")?.as_str()?.to_string();
                let color_token = g
                    .get("colorToken")
                    .and_then(Value::as_str)
                    .and_then(GraphColorToken::from_tag)
                    .unwrap_or(GraphColorToken::Blue);
                let ring_style = g
                    .get("ringStyle")
                    .and_then(Value::as_str)
                    .and_then(GraphRingStyle::from_tag)
                    .unwrap_or(GraphRingStyle::Solid);
                Some(GraphGroup {
                    query,
                    color_token,
                    ring_style,
                })
            })
            .collect();
    }
    if let Some(Value::Object(d)) = root.get("display") {
        config.display = GraphDisplay {
            arrows: bool_or(d.get("arrows"), false),
            text_fade_zoom: clamp_d(d.get("textFadeZoom"), 0.1, 4.0, 0.55),
            node_size_multiplier: clamp_d(d.get("nodeSizeMultiplier"), 0.5, 2.0, 1.0),
            link_thickness: clamp_d(d.get("linkThickness"), 0.5, 4.0, 1.0),
        };
    }
    if let Some(Value::Object(fo)) = root.get("forces") {
        config.forces = GraphForcesConfig {
            center: clamp_d(fo.get("center"), 0.0, 1.0, 0.5),
            repel: clamp_d(fo.get("repel"), 0.0, 1.0, 0.5),
            link: clamp_d(fo.get("link"), 0.0, 1.0, 0.5),
            link_distance: clamp_d(fo.get("linkDistance"), 0.0, 1.0, 0.5),
        };
    }
    if let Some(mode) = root
        .get("mode")
        .and_then(Value::as_str)
        .and_then(mode_from_tag)
    {
        config.mode = mode;
    }
    // The depth keeps the mac's `as? Int` classification (0b-12): an
    // INTEGRAL value within i64 (Swift's `Int` bridge) is clamped; a
    // fractional value, an integral value beyond i64, or a non-number is
    // the default.
    // An integer token classifies exactly (`as_i64`: `9223372036854775807`
    // is in, `9223372036854775808` is out); a float token by its integral
    // value within the same range (`2.0` is in, `1e20` is out).
    config.connections_depth = match root.get("connectionsDepth") {
        Some(v) => match v.as_i64() {
            Some(i) => i.clamp(1, 3) as u32,
            None => match v.as_f64() {
                Some(d)
                    if d.is_finite()
                        && d.fract() == 0.0
                        && d.abs() < 9_223_372_036_854_775_808.0 =>
                {
                    d.clamp(1.0, 3.0) as u32
                }
                _ => 1,
            },
        },
        None => 1,
    };
    if let Some(verbosity) = root
        .get("verbosity")
        .and_then(Value::as_str)
        .and_then(verbosity_from_tag)
    {
        config.verbosity = verbosity;
    }
    let unknown: Map<String, Value> = root
        .iter()
        .filter(|(k, _)| !KNOWN_KEYS.contains(&k.as_str()))
        .map(|(k, v)| (k.clone(), v.clone()))
        .collect();
    Ok(GraphConfigRead {
        config,
        unknown_json: canonical(Value::Object(unknown)).to_string(),
    })
}

/// Every object's keys sorted by Unicode scalar order, recursively — the
/// explicit pass `preserve_order` makes necessary.
fn canonical(value: Value) -> Value {
    match value {
        Value::Object(map) => {
            let sorted: std::collections::BTreeMap<String, Value> =
                map.into_iter().map(|(k, v)| (k, canonical(v))).collect();
            Value::Object(sorted.into_iter().collect())
        }
        Value::Array(items) => Value::Array(items.into_iter().map(canonical).collect()),
        other => other,
    }
}

fn known_sections(config: &GraphConfig) -> Map<String, Value> {
    let mut root = Map::new();
    root.insert("version".into(), json!(VERSION));
    root.insert(
        "filters".into(),
        json!({
            "includeAttachments": config.filters.include_attachments,
            "includeGhosts": config.filters.include_ghosts,
            "orphansOnly": config.filters.orphans_only,
            "nameQuery": config.filters.name_query,
        }),
    );
    root.insert(
        "groups".into(),
        Value::Array(
            config
                .groups
                .iter()
                .map(|g| {
                    json!({
                        "query": g.query,
                        "colorToken": g.color_token.tag(),
                        "ringStyle": g.ring_style.tag(),
                    })
                })
                .collect(),
        ),
    );
    root.insert(
        "display".into(),
        json!({
            "arrows": config.display.arrows,
            "textFadeZoom": clamp_out(config.display.text_fade_zoom, 0.1, 4.0, 0.55),
            "nodeSizeMultiplier": clamp_out(config.display.node_size_multiplier, 0.5, 2.0, 1.0),
            "linkThickness": clamp_out(config.display.link_thickness, 0.5, 4.0, 1.0),
        }),
    );
    root.insert(
        "forces".into(),
        json!({
            "center": clamp_out(config.forces.center, 0.0, 1.0, 0.5),
            "repel": clamp_out(config.forces.repel, 0.0, 1.0, 0.5),
            "link": clamp_out(config.forces.link, 0.0, 1.0, 0.5),
            "linkDistance": clamp_out(config.forces.link_distance, 0.0, 1.0, 0.5),
        }),
    );
    root.insert("mode".into(), json!(mode_tag(config.mode)));
    root.insert(
        "connectionsDepth".into(),
        json!(config.connections_depth.clamp(1, 3)),
    );
    root.insert("verbosity".into(), json!(verbosity_tag(config.verbosity)));
    root
}

/// Encode a config over an existing file text (`GraphConfigStore.swift:60–117`):
/// an existing text that is not a JSON object → `Unparseable` (never
/// clobbered); a newer version → `NewerVersion` (never downgraded);
/// otherwise the known sections replace theirs and every unknown
/// top-level key is preserved semantically. The output is canonical:
/// keys sorted recursively, serde's pretty printer (two-space indent,
/// `"key": value`, `\n` line ends, no trailing newline) — 0b-D5.
pub fn encode(
    config: &GraphConfig,
    existing_json: Option<&str>,
) -> Result<String, GraphConfigError> {
    let mut root = match existing_json {
        Some(text) => {
            let existing = parse_object(text)?;
            check_version(&existing)?;
            existing
        }
        None => Map::new(),
    };
    for (key, value) in known_sections(config) {
        root.insert(key, value);
    }
    serde_json::to_string_pretty(&canonical(Value::Object(root))).map_err(|e| {
        GraphConfigError::Unparseable {
            reason: format!("serialization failed: {e}"),
        }
    })
}

/// The index of the first group whose trimmed query matches `label`
/// under the filter's predicate; a blank query never matches
/// (`GraphConfig.swift:34–40`). Label-only by owner ruling (0bD-9).
pub fn matching_group(config: &GraphConfig, label: &str) -> Option<usize> {
    config.groups.iter().position(|g| {
        let q = g.query.trim();
        !q.is_empty() && label_matches(label, q)
    })
}

/// The style a new group takes, by its index: the ring cycles
/// `solid → dashed → double → dotted`, the palette its eight slots
/// (`AppState+GraphConfig.swift:172–177`).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct GraphGroupStyle {
    pub color_token: GraphColorToken,
    pub ring_style: GraphRingStyle,
}

pub fn next_group_style(group_count: usize) -> GraphGroupStyle {
    GraphGroupStyle {
        color_token: GraphColorToken::ALL[group_count % GraphColorToken::ALL.len()],
        ring_style: GraphRingStyle::ALL[group_count % GraphRingStyle::ALL.len()],
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn full_config() -> GraphConfig {
        GraphConfig {
            filters: GraphFilterConfig {
                include_attachments: true,
                include_ghosts: false,
                orphans_only: true,
                name_query: "café".into(),
            },
            groups: vec![
                GraphGroup {
                    query: "project".into(),
                    color_token: GraphColorToken::Green,
                    ring_style: GraphRingStyle::Dashed,
                },
                GraphGroup {
                    query: "archive".into(),
                    color_token: GraphColorToken::Purple,
                    ring_style: GraphRingStyle::Dotted,
                },
            ],
            display: GraphDisplay {
                arrows: true,
                text_fade_zoom: 0.8,
                node_size_multiplier: 1.5,
                link_thickness: 2.0,
            },
            forces: GraphForcesConfig {
                center: 0.2,
                repel: 0.9,
                link: 0.3,
                link_distance: 0.7,
            },
            mode: GraphSurfaceMode::Diagram,
            connections_depth: 3,
            verbosity: GraphVerbosity::Verbose,
        }
    }

    /// GraphConfigTests.testRoundTripsAllSections as a Rust fact.
    #[test]
    fn round_trips_every_section() {
        let cfg = full_config();
        let text = encode(&cfg, None).unwrap();
        let read = decode(&text).unwrap();
        assert_eq!(read.config, cfg);
        assert_eq!(read.unknown_json, "{}");
        assert!(text.contains("\"version\": 1"));
        assert!(text.contains("\"verbosity\": \"verbose\""));
    }

    /// The default is what a missing file decodes to — the host maps the
    /// absent file to `graph_config_default()` (testMissingFileReadsDefault).
    #[test]
    fn defaults_are_the_mac_defaults_and_verbosity_is_standard() {
        let d = default();
        assert_eq!(d, GraphConfig::default());
        assert!(
            !d.filters.include_attachments && d.filters.include_ghosts && !d.filters.orphans_only
        );
        assert_eq!(d.filters.name_query, "");
        assert!(d.groups.is_empty());
        assert_eq!(
            (
                d.display.text_fade_zoom,
                d.display.node_size_multiplier,
                d.display.link_thickness
            ),
            (0.55, 1.0, 1.0)
        );
        assert_eq!(
            (
                d.forces.center,
                d.forces.repel,
                d.forces.link,
                d.forces.link_distance
            ),
            (0.5, 0.5, 0.5, 0.5)
        );
        assert_eq!(d.mode, GraphSurfaceMode::Table);
        assert_eq!(d.connections_depth, 1);
        assert_eq!(d.verbosity, GraphVerbosity::Standard);
        // An empty object decodes to the default too.
        assert_eq!(decode("{}").unwrap().config, d);
    }

    /// testPreservesUnknownTopLevelKeys: a future Slate's key survives a
    /// rewrite; the known section is replaced.
    #[test]
    fn encode_preserves_unknown_keys_and_refuses_the_two_cases() {
        let existing = r#"{"version":1,"futureThing":{"keep":true},"mode":"table"}"#;
        let read = decode(existing).unwrap();
        assert_eq!(read.config.mode, GraphSurfaceMode::Table);
        assert_eq!(read.unknown_json, r#"{"futureThing":{"keep":true}}"#);
        let mut cfg = read.config;
        cfg.mode = GraphSurfaceMode::Diagram;
        let out = encode(&cfg, Some(existing)).unwrap();
        let root: serde_json::Value = serde_json::from_str(&out).unwrap();
        assert_eq!(root["futureThing"]["keep"], serde_json::Value::Bool(true));
        assert_eq!(root["mode"], "diagram");
        // Keys sorted (0b-D5).
        let keys: Vec<&str> = root
            .as_object()
            .unwrap()
            .keys()
            .map(String::as_str)
            .collect();
        let mut sorted = keys.clone();
        sorted.sort_unstable();
        assert_eq!(keys, sorted);

        // testRefusesToClobberUnparseableFile
        let garbage = "this is not json {{{";
        assert!(matches!(
            encode(&cfg, Some(garbage)),
            Err(GraphConfigError::Unparseable { .. })
        ));
        assert!(matches!(
            decode(garbage),
            Err(GraphConfigError::Unparseable { .. })
        ));
        assert!(matches!(
            decode("[1,2]"),
            Err(GraphConfigError::Unparseable { .. })
        ));

        // testRefusesToDowngradeANewerVersionFile
        let future = r#"{"version":999,"mode":"diagram","futureSection":{"x":1}}"#;
        assert_eq!(
            decode(future),
            Err(GraphConfigError::NewerVersion { version: 999 })
        );
        assert_eq!(
            encode(&cfg, Some(future)),
            Err(GraphConfigError::NewerVersion { version: 999 })
        );
    }

    /// The writer's bytes (0b-12, 0b-D5): keys sorted recursively, serde's
    /// pretty form, unknown values re-emitted canonically, no trailing
    /// newline. The workspace's `preserve_order` makes this an explicit
    /// pass, so the golden is the proof.
    #[test]
    fn encode_is_canonical_bytes() {
        let existing = r#"{"zeta":{"b":1,"a":[3,{"y":2,"x":1}]},"alpha":"é\n"}"#;
        let read = decode(existing).unwrap();
        assert_eq!(
            read.unknown_json,
            r#"{"alpha":"é\n","zeta":{"a":[3,{"x":1,"y":2}],"b":1}}"#
        );
        let out = encode(&GraphConfig::default(), Some(existing)).unwrap();
        let expected = r#"{
  "alpha": "é\n",
  "connectionsDepth": 1,
  "display": {
    "arrows": false,
    "linkThickness": 1.0,
    "nodeSizeMultiplier": 1.0,
    "textFadeZoom": 0.55
  },
  "filters": {
    "includeAttachments": false,
    "includeGhosts": true,
    "nameQuery": "",
    "orphansOnly": false
  },
  "forces": {
    "center": 0.5,
    "link": 0.5,
    "linkDistance": 0.5,
    "repel": 0.5
  },
  "groups": [],
  "mode": "table",
  "verbosity": "standard",
  "version": 1,
  "zeta": {
    "a": [
      3,
      {
        "x": 1,
        "y": 2
      }
    ],
    "b": 1
  }
}"#;
        assert_eq!(out, expected);
        assert!(!out.ends_with('\n'));
        assert!(!out.contains('\r'));
    }

    /// testClampsOutOfRangeValues, plus the malformed shapes that take
    /// their defaults.
    #[test]
    fn clamps_out_of_range_values_and_defaults_the_malformed() {
        let text = r#"{"forces":{"repel":9.0,"center":-3},"connectionsDepth":99,"display":{"nodeSizeMultiplier":100}}"#;
        let cfg = decode(text).unwrap().config;
        assert_eq!(cfg.forces.repel, 1.0);
        assert_eq!(cfg.forces.center, 0.0);
        assert_eq!(cfg.connections_depth, 3);
        assert_eq!(cfg.display.node_size_multiplier, 2.0);
        assert_eq!(
            cfg.display.text_fade_zoom, 0.55,
            "an absent knob keeps its default"
        );
        let odd = r#"{"forces":{"link":"a lot"},"display":{"textFadeZoom":null},"mode":"hologram","connectionsDepth":0,"verbosity":"loud","groups":[{"colorToken":"blue"},{"query":"x","colorToken":"mauve","ringStyle":"wavy"}]}"#;
        let cfg = decode(odd).unwrap().config;
        assert_eq!(
            cfg.forces.link, 0.5,
            "a non-numeric force takes the default"
        );
        assert_eq!(cfg.display.text_fade_zoom, 0.55);
        assert_eq!(
            cfg.mode,
            GraphSurfaceMode::Table,
            "an unknown mode tag keeps the default"
        );
        assert_eq!(cfg.connections_depth, 1);
        assert_eq!(
            cfg.verbosity,
            GraphVerbosity::Standard,
            "an unknown verbosity keeps the default"
        );
        assert_eq!(cfg.groups.len(), 1, "a group without a query is dropped");
        assert_eq!(
            cfg.groups[0].color_token,
            GraphColorToken::Blue,
            "an unknown colour reads blue"
        );
        assert_eq!(
            cfg.groups[0].ring_style,
            GraphRingStyle::Solid,
            "an unknown ring reads solid"
        );
    }

    /// The decode truth table (0b-12): every version category, sections
    /// of the wrong shape, a mixed groups array, every number category,
    /// and non-finite outbound values.
    #[test]
    fn decode_truth_table() {
        let ok = |text: &str| decode(text).unwrap().config;
        // Versions.
        assert_eq!(ok(r#"{}"#), GraphConfig::default(), "absent → 1");
        assert_eq!(ok(r#"{"version":1}"#), GraphConfig::default());
        assert_eq!(
            ok(r#"{"version":1.0}"#),
            GraphConfig::default(),
            "an integral float is an integer"
        );
        assert_eq!(ok(r#"{"version":0}"#), GraphConfig::default());
        assert_eq!(
            decode(r#"{"version":2}"#),
            Err(GraphConfigError::NewerVersion { version: 2 })
        );
        assert_eq!(
            ok(r#"{"version":1e0}"#),
            GraphConfig::default(),
            "an exponent form of one is one"
        );
        for bad in [
            r#"{"version":1.5}"#,
            r#"{"version":-1}"#,
            r#"{"version":true}"#,
            r#"{"version":"1"}"#,
            r#"{"version":null}"#,
            r#"{"version":1e30}"#,
            r#"{"version":1e400}"#,
            "{\"forces\":{\"repel\":1e400}}",
        ] {
            assert!(
                matches!(decode(bad), Err(GraphConfigError::Unparseable { .. })),
                "{bad} cannot be classified"
            );
            assert!(
                matches!(
                    encode(&GraphConfig::default(), Some(bad)),
                    Err(GraphConfigError::Unparseable { .. })
                ),
                "{bad} is never rewritten"
            );
        }
        // Sections of the wrong shape are ignored whole.
        assert_eq!(
            ok(r#"{"filters":5,"display":[],"forces":"x","groups":{"query":"a"}}"#),
            GraphConfig::default()
        );
        // A mixed groups array: each invalid element skipped on its own (0b-D6).
        let cfg = ok(
            r#"{"groups":[{"query":"a"},7,{"colorToken":"red"},null,{"query":"b","ringStyle":"dotted"}]}"#,
        );
        assert_eq!(
            cfg.groups
                .iter()
                .map(|g| g.query.as_str())
                .collect::<Vec<_>>(),
            vec!["a", "b"]
        );
        assert_eq!(cfg.groups[1].ring_style, GraphRingStyle::Dotted);
        // Booleans of the wrong type take their defaults.
        let cfg =
            ok(r#"{"filters":{"includeGhosts":"no","orphansOnly":1},"display":{"arrows":"yes"}}"#);
        assert!(cfg.filters.include_ghosts && !cfg.filters.orphans_only && !cfg.display.arrows);
        // Number categories.
        assert_eq!(
            ok(r#"{"connectionsDepth":2.0}"#).connections_depth,
            2,
            "an integral value is clamped"
        );
        assert_eq!(
            ok(r#"{"connectionsDepth":2.7}"#).connections_depth,
            1,
            "a fractional value is the default — the mac's classification"
        );
        assert_eq!(
            ok(r#"{"connectionsDepth":"3"}"#).connections_depth,
            1,
            "a string is non-numeric"
        );
        assert_eq!(ok(r#"{"connectionsDepth":-5}"#).connections_depth, 1);
        assert_eq!(
            ok(r#"{"connectionsDepth":-0}"#).connections_depth,
            1,
            "negative zero clamps to one"
        );
        assert_eq!(ok(r#"{"connectionsDepth":1e0}"#).connections_depth, 1);
        assert_eq!(ok(r#"{"connectionsDepth":1.5}"#).connections_depth, 1);
        assert_eq!(ok(r#"{"connectionsDepth":99}"#).connections_depth, 3);
        assert_eq!(
            ok(r#"{"connectionsDepth":9223372036854775807}"#).connections_depth,
            3,
            "the i64 maximum bridges to Int"
        );
        assert_eq!(
            ok(r#"{"connectionsDepth":9223372036854775808}"#).connections_depth,
            1,
            "beyond i64 the mac's bridge failed"
        );
        assert_eq!(ok(r#"{"connectionsDepth":1e20}"#).connections_depth, 1);
        // The version rule classifies the PARSED value (0bD-14): a token
        // f64 cannot distinguish reads as the value it parses to.
        assert_eq!(
            ok(r#"{"version":1.0000000000000001}"#),
            GraphConfig::default(),
            "1.0000000000000001 is 1.0 in f64"
        );
        assert_eq!(
            decode(&format!(r#"{{"version":{}}}"#, u64::MAX)).unwrap_err(),
            GraphConfigError::NewerVersion { version: u64::MAX },
            "the u64 boundary is a newer version, not an error of classification"
        );
        assert_eq!(
            ok(r#"{"display":{"textFadeZoom":1e300}}"#)
                .display
                .text_fade_zoom,
            4.0,
            "a huge finite value clamps"
        );
        assert_eq!(
            ok(r#"{"forces":{"repel":true}}"#).forces.repel,
            0.5,
            "a boolean is non-numeric"
        );
        // Non-finite outbound values: NaN → the default, ±∞ → the bound.
        let mut cfg = GraphConfig::default();
        cfg.forces.repel = f64::NAN;
        cfg.display.text_fade_zoom = f64::INFINITY;
        cfg.display.link_thickness = f64::NEG_INFINITY;
        cfg.connections_depth = 9;
        let back = decode(&encode(&cfg, None).unwrap()).unwrap().config;
        assert_eq!(back.forces.repel, 0.5);
        assert_eq!(back.display.text_fade_zoom, 4.0);
        assert_eq!(back.display.link_thickness, 0.5);
        assert_eq!(back.connections_depth, 3);
    }

    /// testGroupPrecedenceIsFirstMatchWins.
    #[test]
    fn group_precedence_is_first_match_wins() {
        let mut cfg = GraphConfig {
            groups: vec![
                GraphGroup {
                    query: "note".into(),
                    color_token: GraphColorToken::Blue,
                    ring_style: GraphRingStyle::Solid,
                },
                GraphGroup {
                    query: "meeting".into(),
                    color_token: GraphColorToken::Red,
                    ring_style: GraphRingStyle::Dashed,
                },
            ],
            ..GraphConfig::default()
        };
        assert_eq!(
            matching_group(&cfg, "meeting-note"),
            Some(0),
            "the first rule wins"
        );
        assert_eq!(matching_group(&cfg, "weekly meeting"), Some(1));
        assert_eq!(matching_group(&cfg, "unrelated"), None);
        assert_eq!(
            matching_group(&cfg, "MEETING café"),
            Some(1),
            "the fold is the filter's"
        );
        cfg.groups = vec![GraphGroup {
            query: "  ".into(),
            color_token: GraphColorToken::Pink,
            ring_style: GraphRingStyle::Solid,
        }];
        assert_eq!(
            matching_group(&cfg, "anything"),
            None,
            "a blank query never swallows every node"
        );
    }

    /// addGraphGroup's cycling and the two option sets (T71, T72).
    #[test]
    fn next_group_style_cycles_ring_and_palette() {
        assert_eq!(
            next_group_style(0),
            GraphGroupStyle {
                color_token: GraphColorToken::Red,
                ring_style: GraphRingStyle::Solid
            }
        );
        assert_eq!(
            next_group_style(3),
            GraphGroupStyle {
                color_token: GraphColorToken::Green,
                ring_style: GraphRingStyle::Dotted
            }
        );
        assert_eq!(
            next_group_style(4),
            GraphGroupStyle {
                color_token: GraphColorToken::Teal,
                ring_style: GraphRingStyle::Solid
            }
        );
        assert_eq!(next_group_style(8).color_token, GraphColorToken::Red);
    }

    /// The option vectors are the enums' arms in declaration order, with
    /// the tags round-tripping and the titles capitalised (0b-12, design B).
    #[test]
    fn option_vectors_are_the_enum_arms() {
        let tokens = color_tokens();
        assert_eq!(
            tokens.iter().map(|s| s.token).collect::<Vec<_>>(),
            GraphColorToken::ALL.to_vec()
        );
        assert_eq!(
            tokens.iter().map(|s| s.title.as_str()).collect::<Vec<_>>(),
            vec![
                "Red", "Orange", "Yellow", "Green", "Teal", "Blue", "Purple", "Pink"
            ]
        );
        for s in &tokens {
            assert_eq!(GraphColorToken::from_tag(&s.tag), Some(s.token));
            assert_eq!(s.tag, s.title.to_lowercase());
        }
        let rings = ring_styles();
        assert_eq!(
            rings.iter().map(|s| s.style).collect::<Vec<_>>(),
            GraphRingStyle::ALL.to_vec()
        );
        assert_eq!(
            rings.iter().map(|s| s.title.as_str()).collect::<Vec<_>>(),
            vec!["Solid", "Dashed", "Double", "Dotted"]
        );
        for s in &rings {
            assert_eq!(GraphRingStyle::from_tag(&s.tag), Some(s.style));
        }
        // The vectors are what the enum tripwire counts (8, 4); a new arm
        // without its entry fails here.
        assert_eq!((tokens.len(), rings.len()), (8, 4));
        // The modes and the levels: the shipped mac tags and titles.
        let modes = surface_modes();
        assert_eq!(
            modes.iter().map(|s| s.mode).collect::<Vec<_>>(),
            SURFACE_MODES.to_vec()
        );
        assert_eq!(
            modes
                .iter()
                .map(|s| (s.tag.as_str(), s.title.as_str()))
                .collect::<Vec<_>>(),
            vec![("table", "Table"), ("diagram", "Diagram")]
        );
        for s in &modes {
            assert_eq!(mode_from_tag(&s.tag), Some(s.mode));
        }
        let levels = verbosities();
        assert_eq!(
            levels.iter().map(|s| s.verbosity).collect::<Vec<_>>(),
            VERBOSITIES.to_vec()
        );
        assert_eq!(
            levels
                .iter()
                .map(|s| (s.tag.as_str(), s.title.as_str()))
                .collect::<Vec<_>>(),
            vec![
                ("terse", "Terse"),
                ("standard", "Standard"),
                ("verbose", "Verbose")
            ]
        );
        for s in &levels {
            assert_eq!(verbosity_from_tag(&s.tag), Some(s.verbosity));
        }
    }
}
