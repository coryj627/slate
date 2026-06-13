// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Bibliography source loading + multi-source merge + debounce.
//!
//! Reads bibliography source files from disk (BibTeX / BibLaTeX /
//! CSL-JSON), parses them into a uniform [`BibEntry`] shape, and
//! merges multiple sources by citation key. Implements the second
//! backend ticket for Milestone L (`05_locked_architecture_decisions.md`
//! §6.5).
//!
//! The on-disk filesystem watch is intentionally NOT wired here —
//! the vault scanner's real notify-based watcher hasn't landed yet
//! (`vault::fs::FsVaultProvider::watch` returns `None`). What this
//! module *does* land is the debouncer that consumes change pulses
//! from any source (real watcher, fake test channel, manual refresh)
//! and emits one consolidated change pulse per 500ms quiet period.
//! The session-side wire-up between the future disk watcher and this
//! debouncer lives in #278.

use std::collections::{BTreeMap, HashMap};
use std::path::{Path, PathBuf};
use std::sync::Arc;
use std::sync::mpsc::{Receiver, RecvTimeoutError, Sender};
use std::thread;
use std::time::{Duration, Instant};

use crate::VaultError;

/// One bibliography source as configured by the user (typically via
/// `.slate/prefs.json` in #278). `path` is interpreted relative to
/// the vault root, or as an absolute path when the user picks a
/// file outside the vault.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BibliographySource {
    /// Vault-relative or absolute path to the source file.
    pub path: String,
    /// File format. Drives which parser is invoked on load.
    pub format: BibFormat,
    /// If true, the session subscribes to filesystem changes on this
    /// file. Honoured by the debouncer in this module; the actual
    /// notify wire-up lives in #278.
    pub watch: bool,
}

/// Supported bibliography file formats. `BibTeX` and `BibLaTeX` are
/// both handled by the same parser (hayagriva's biblatex backend);
/// the distinction is preserved on the source so the UI can label
/// it correctly for the user. The parser itself is permissive.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BibFormat {
    /// Classic BibTeX `.bib` file. Same parser as `BibLaTeX`.
    BibTeX,
    /// BibLaTeX `.bib` file with the extended type set (e.g.
    /// `@online`).
    BibLaTeX,
    /// CSL-JSON `.json` file (an array of CSL-JSON items).
    CslJson,
}

/// Parsed bibliography entry. Uniform across all input formats.
///
/// `raw_csl_json` is a serialised CSL-JSON representation of the
/// entry — for CSL-JSON sources it round-trips through `serde_json`;
/// for BibTeX/BibLaTeX sources it's synthesised from the fields
/// extracted via hayagriva. Either way the resulting string parses
/// as valid CSL-JSON and is consumable by hayagriva's renderer in
/// #277.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BibEntry {
    pub key: String,
    pub item_type: String,
    pub title: String,
    pub authors: Vec<Author>,
    pub year: Option<i32>,
    pub journal: Option<String>,
    pub doi: Option<String>,
    pub url: Option<String>,
    pub publisher: Option<String>,
    pub abstract_text: Option<String>,
    pub raw_csl_json: String,
}

/// One author of a bibliography entry. `family` is the surname (or
/// the literal name for institutional authors with no given name);
/// `given` is the given/first name when distinguished from `family`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Author {
    pub family: String,
    pub given: Option<String>,
}

/// A duplicate citation key across two sources during a merge. The
/// first source listed wins; the loser is recorded here so the UI
/// can surface the ambiguity.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct KeyCollision {
    pub key: String,
    pub winning_source: String,
    pub losing_source: String,
}

/// Non-fatal parse warning emitted alongside a successful
/// [`load_source`] call. A malformed entry inside an otherwise-valid
/// source produces a warning; the source still loads and the
/// surrounding entries still come through.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BibLoadWarning {
    /// Path of the source that produced the warning.
    pub source_path: String,
    /// Human-readable description of what went wrong.
    pub message: String,
}

/// Result of loading a single source. `entries` carries the
/// successfully-parsed entries; `warnings` carries the recoverable
/// problems (e.g. one malformed entry inside an otherwise-valid
/// `.bib`).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct LoadResult {
    pub entries: Vec<BibEntry>,
    pub warnings: Vec<BibLoadWarning>,
}

/// Read-only lookup table over the merged bibliography. Built once
/// from the output of [`merge_sources`] and consumed by the renderer
/// in #277. `version` increments on every rebuild so the renderer's
/// per-process cache can be keyed on `(reference, style, version)`
/// — bumping the index invalidates all cached renders implicitly.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BibIndex {
    by_key: HashMap<String, BibEntry>,
    version: u64,
}

impl BibIndex {
    /// Build an index from a flat entry list (the first element of
    /// [`merge_sources`]'s return). Duplicate keys keep the first
    /// occurrence — `merge_sources` has already de-duped, so in
    /// practice this matters only for callers that bypass the merge.
    pub fn build(entries: Vec<BibEntry>, version: u64) -> Self {
        let mut by_key = HashMap::with_capacity(entries.len());
        for entry in entries {
            by_key.entry(entry.key.clone()).or_insert(entry);
        }
        Self { by_key, version }
    }

    /// Construct an empty index. Useful in tests and as the initial
    /// session state before a bibliography is configured.
    pub fn empty() -> Self {
        Self {
            by_key: HashMap::new(),
            version: 0,
        }
    }

    /// Lookup an entry by key.
    pub fn get(&self, key: &str) -> Option<&BibEntry> {
        self.by_key.get(key)
    }

    /// Number of entries in the index.
    pub fn len(&self) -> usize {
        self.by_key.len()
    }

    /// True if the index has no entries.
    pub fn is_empty(&self) -> bool {
        self.by_key.is_empty()
    }

    /// Monotonic counter incremented every time the index is rebuilt.
    /// Used as the third component of the renderer's cache key.
    pub fn version(&self) -> u64 {
        self.version
    }

    /// Iterate every entry. Order is unspecified.
    pub fn iter(&self) -> impl Iterator<Item = &BibEntry> {
        self.by_key.values()
    }
}

/// Load a single bibliography source. Resolves `source.path`
/// against `vault_root` (absolute paths are honoured as-is) and
/// dispatches to the format-specific parser.
///
/// File-can't-be-opened returns [`VaultError::BibSourceUnreadable`]
/// so the caller can distinguish "config points at a missing file"
/// from "config points at a file that has parse problems" (the
/// latter surfaces as warnings on a successful return).
pub fn load_source(
    source: &BibliographySource,
    vault_root: &Path,
) -> Result<LoadResult, VaultError> {
    let resolved = resolve_source_path(&source.path, vault_root);
    let contents =
        std::fs::read_to_string(&resolved).map_err(|e| VaultError::BibSourceUnreadable {
            path: source.path.clone(),
            reason: e.to_string(),
        })?;

    match source.format {
        BibFormat::BibTeX | BibFormat::BibLaTeX => Ok(load_biblatex(&contents, &source.path)),
        BibFormat::CslJson => Ok(load_csl_json(&contents, &source.path)),
    }
}

/// Merge multiple per-source entry vectors into a single set.
/// First-source-wins on duplicate keys; every collision is recorded
/// in the returned `KeyCollision` list so the UI can surface
/// ambiguous keys to the user.
///
/// Input vectors must be paired with their source path so the
/// collision report can attribute the winning and losing source.
pub fn merge_sources(sources: &[(String, Vec<BibEntry>)]) -> (Vec<BibEntry>, Vec<KeyCollision>) {
    let mut merged: HashMap<String, (String, BibEntry)> = HashMap::new();
    let mut collisions = Vec::new();
    for (source_path, entries) in sources {
        for entry in entries {
            match merged.get(&entry.key) {
                Some((winning_source, _)) => {
                    collisions.push(KeyCollision {
                        key: entry.key.clone(),
                        winning_source: winning_source.clone(),
                        losing_source: source_path.clone(),
                    });
                }
                None => {
                    merged.insert(entry.key.clone(), (source_path.clone(), entry.clone()));
                }
            }
        }
    }
    let mut out: Vec<BibEntry> = merged.into_values().map(|(_, e)| e).collect();
    // Stable order so callers (and tests) don't rely on HashMap
    // iteration order.
    out.sort_by(|a, b| a.key.cmp(&b.key));
    (out, collisions)
}

/// Default debounce window. Matches the value called out in the
/// Milestone L issue description (Better BibTeX's auto-export
/// rewrites a `.bib` quickly enough that 500ms collapses a burst
/// to one reload).
pub const DEFAULT_DEBOUNCE: Duration = Duration::from_millis(500);

/// Sink invoked once per debounced change burst. The implementation
/// re-loads the bibliography from disk and emits the session-level
/// `BibliographyChanged` event (wire-up in #278).
pub trait BibliographyChangeSink: Send + Sync {
    fn on_debounced_change(&self);
}

/// Spawn a background thread that consumes raw change pulses from
/// `rx` and invokes `sink.on_debounced_change()` once per quiet
/// period of `window`. Pulses arriving within the window collapse
/// into a single sink call.
///
/// Returns immediately. The thread exits when `rx` is disconnected
/// (i.e. when the matching `Sender` is dropped) — that's the
/// session's signal to tear down the watcher.
pub fn spawn_debouncer(
    rx: Receiver<()>,
    window: Duration,
    sink: Arc<dyn BibliographyChangeSink>,
) -> thread::JoinHandle<()> {
    thread::spawn(move || debounce_loop(rx, window, sink))
}

fn debounce_loop(rx: Receiver<()>, window: Duration, sink: Arc<dyn BibliographyChangeSink>) {
    loop {
        // Block until the first pulse — no idle-CPU cost while the
        // user isn't editing their bibliography.
        match rx.recv() {
            Ok(()) => {}
            Err(_) => return, // sender dropped; we're done.
        }
        // Drain bursts within `window`. Each newly-arriving pulse
        // resets the deadline so a continuous stream of writes
        // (Better BibTeX rewriting in chunks) keeps the timer rolling
        // until quiet.
        let mut deadline = Instant::now() + window;
        loop {
            let remaining = deadline.saturating_duration_since(Instant::now());
            if remaining.is_zero() {
                break;
            }
            match rx.recv_timeout(remaining) {
                Ok(()) => {
                    deadline = Instant::now() + window;
                }
                Err(RecvTimeoutError::Timeout) => break,
                Err(RecvTimeoutError::Disconnected) => {
                    // One last fire so any final buffered burst is
                    // not lost when the session tears down.
                    sink.on_debounced_change();
                    return;
                }
            }
        }
        sink.on_debounced_change();
    }
}

/// Convenience for tests + callers that don't want to write a `Sink`:
/// returns a `(Sender<()>, JoinHandle<()>)` pair where the sender
/// feeds raw pulses and `on_change` is invoked once per debounced
/// burst.
pub fn spawn_debouncer_with_callback<F>(
    window: Duration,
    on_change: F,
) -> (Sender<()>, thread::JoinHandle<()>)
where
    F: Fn() + Send + Sync + 'static,
{
    struct CallbackSink<F: Fn() + Send + Sync>(F);
    impl<F: Fn() + Send + Sync> BibliographyChangeSink for CallbackSink<F> {
        fn on_debounced_change(&self) {
            (self.0)()
        }
    }
    let (tx, rx) = std::sync::mpsc::channel();
    let sink: Arc<dyn BibliographyChangeSink> = Arc::new(CallbackSink(on_change));
    let handle = spawn_debouncer(rx, window, sink);
    (tx, handle)
}

// =====================================================================
// Internal: parsers
// =====================================================================

fn resolve_source_path(path: &str, vault_root: &Path) -> PathBuf {
    let p = Path::new(path);
    if p.is_absolute() {
        p.to_path_buf()
    } else {
        vault_root.join(p)
    }
}

fn load_biblatex(contents: &str, source_path: &str) -> LoadResult {
    use hayagriva::io::{BibLaTeXError, from_biblatex_str};

    let mut warnings = Vec::new();
    let mut entries = Vec::new();

    match from_biblatex_str(contents) {
        Ok(library) => {
            for entry in library.iter() {
                entries.push(hayagriva_to_bib_entry(entry));
            }
        }
        Err(errors) => {
            // hayagriva returns Vec<BibLaTeXError> when any entry
            // fails — we still try to extract the salvageable ones
            // via a per-entry retry below. Each error becomes a
            // warning, and we keep the entries that did parse.
            for e in errors {
                warnings.push(BibLoadWarning {
                    source_path: source_path.to_string(),
                    message: match e {
                        BibLaTeXError::Parse(p) => format!("parse error: {p}"),
                        BibLaTeXError::Type(t) => format!("type error: {t}"),
                    },
                });
            }
            // Salvage pass: split the input by entry boundary and try
            // to parse each one independently. This lets a single
            // malformed entry coexist with valid entries in one .bib
            // — matching the "loader is resilient" requirement in
            // the Milestone L issue.
            for fragment in split_biblatex_entries(contents) {
                if let Ok(library) = from_biblatex_str(&fragment) {
                    for entry in library.iter() {
                        entries.push(hayagriva_to_bib_entry(entry));
                    }
                }
            }
            // De-duplicate by key (the salvage pass can produce
            // duplicates when hayagriva's bulk parse partially
            // succeeded for some entries).
            let mut seen = std::collections::HashSet::new();
            entries.retain(|e| seen.insert(e.key.clone()));
        }
    }

    LoadResult { entries, warnings }
}

/// Crude but adequate splitter: BibTeX entries start at `@type{` at
/// column 0 (or after whitespace). We chunk the source into per-entry
/// fragments so the salvage pass can isolate failures.
fn split_biblatex_entries(contents: &str) -> Vec<String> {
    let mut out = Vec::new();
    let mut current = String::new();
    for line in contents.lines() {
        let trimmed = line.trim_start();
        if trimmed.starts_with('@') && !current.trim().is_empty() {
            out.push(std::mem::take(&mut current));
        }
        current.push_str(line);
        current.push('\n');
    }
    if !current.trim().is_empty() {
        out.push(current);
    }
    out
}

fn load_csl_json(contents: &str, source_path: &str) -> LoadResult {
    let mut warnings = Vec::new();
    let mut entries = Vec::new();

    let parsed: Result<Vec<serde_json::Value>, _> = serde_json::from_str(contents);
    let items = match parsed {
        Ok(v) => v,
        Err(e) => {
            // The file isn't an array — try a single-object form
            // (some exporters emit a bare `{...}` for one entry).
            match serde_json::from_str::<serde_json::Value>(contents) {
                Ok(serde_json::Value::Object(_)) => vec![serde_json::from_str(contents).unwrap()],
                _ => {
                    warnings.push(BibLoadWarning {
                        source_path: source_path.to_string(),
                        message: format!("CSL-JSON parse error: {e}"),
                    });
                    return LoadResult { entries, warnings };
                }
            }
        }
    };

    for item in items {
        match csl_json_to_bib_entry(&item) {
            Ok(entry) => entries.push(entry),
            Err(msg) => warnings.push(BibLoadWarning {
                source_path: source_path.to_string(),
                message: msg,
            }),
        }
    }

    LoadResult { entries, warnings }
}

fn csl_json_to_bib_entry(value: &serde_json::Value) -> Result<BibEntry, String> {
    let obj = value
        .as_object()
        .ok_or_else(|| "expected JSON object for CSL-JSON entry".to_string())?;

    let key = obj
        .get("id")
        .and_then(|v| {
            v.as_str()
                .map(str::to_string)
                .or_else(|| v.as_i64().map(|n| n.to_string()))
        })
        .ok_or_else(|| "CSL-JSON entry missing required \"id\" field".to_string())?;

    let item_type = obj
        .get("type")
        .and_then(|v| v.as_str())
        .unwrap_or("article")
        .to_string();

    let title = obj
        .get("title")
        .and_then(|v| v.as_str())
        .unwrap_or("")
        .to_string();

    let authors = obj
        .get("author")
        .and_then(|v| v.as_array())
        .map(|arr| {
            arr.iter()
                .filter_map(|a| {
                    let o = a.as_object()?;
                    let family = o
                        .get("family")
                        .and_then(|x| x.as_str())
                        .or_else(|| o.get("literal").and_then(|x| x.as_str()))?
                        .to_string();
                    let given = o.get("given").and_then(|x| x.as_str()).map(str::to_string);
                    Some(Author { family, given })
                })
                .collect()
        })
        .unwrap_or_default();

    let year = obj
        .get("issued")
        .and_then(|v| v.as_object())
        .and_then(|o| o.get("date-parts"))
        .and_then(|v| v.as_array())
        .and_then(|outer| outer.first())
        .and_then(|inner| inner.as_array())
        .and_then(|parts| parts.first())
        .and_then(|y| y.as_i64())
        .map(|y| y as i32);

    let journal = obj
        .get("container-title")
        .and_then(|v| v.as_str())
        .map(str::to_string);

    let doi = obj.get("DOI").and_then(|v| v.as_str()).map(str::to_string);

    let url = obj.get("URL").and_then(|v| v.as_str()).map(str::to_string);

    let publisher = obj
        .get("publisher")
        .and_then(|v| v.as_str())
        .map(str::to_string);

    let abstract_text = obj
        .get("abstract")
        .and_then(|v| v.as_str())
        .map(str::to_string);

    // Round-trippable raw form: serialise back through serde_json so
    // the test's "round-trips byte-stable" property holds (we don't
    // promise byte-equality with the source file — keys may be
    // re-ordered).
    let raw_csl_json = serde_json::to_string(value).unwrap_or_default();

    Ok(BibEntry {
        key,
        item_type,
        title,
        authors,
        year,
        journal,
        doi,
        url,
        publisher,
        abstract_text,
        raw_csl_json,
    })
}

fn hayagriva_to_bib_entry(entry: &hayagriva::Entry) -> BibEntry {
    let key = entry.key().to_string();
    let item_type = format!("{:?}", entry.entry_type()).to_lowercase();
    let title = entry
        .title()
        .map(|t| t.value.to_string())
        .unwrap_or_default();
    let authors: Vec<Author> = entry
        .authors()
        .map(|persons| {
            persons
                .iter()
                .map(|p| Author {
                    family: p.name.clone(),
                    given: p.given_name.clone(),
                })
                .collect()
        })
        .unwrap_or_default();
    let year = entry.date().map(|d| d.year);
    // For an article, the journal is on the parent of type Periodical.
    let journal = entry
        .parents()
        .iter()
        .find_map(|p| p.title().map(|t| t.value.to_string()))
        .filter(|s| !s.is_empty());
    let doi = entry.doi().map(str::to_string);
    let url = entry.url_any().map(|q| q.value.to_string());
    let publisher = entry
        .publisher()
        .and_then(|p| p.name().map(|n| n.value.to_string()))
        .filter(|s| !s.is_empty());
    let abstract_text = entry.abstract_().map(|a| a.value.to_string());

    let raw_csl_json = synthesize_csl_json(
        &key,
        &item_type,
        &title,
        &authors,
        year,
        journal.as_deref(),
        doi.as_deref(),
        url.as_deref(),
        publisher.as_deref(),
        abstract_text.as_deref(),
    );

    BibEntry {
        key,
        item_type,
        title,
        authors,
        year,
        journal,
        doi,
        url,
        publisher,
        abstract_text,
        raw_csl_json,
    }
}

/// Build a CSL-JSON-shaped string from the fields we extracted. The
/// result is *valid* CSL-JSON (parseable by hayagriva via the
/// `csl-json` feature), not a byte-perfect round-trip of the original
/// BibTeX source. That trade-off is explicit in the Milestone L issue.
#[allow(clippy::too_many_arguments)]
fn synthesize_csl_json(
    key: &str,
    item_type: &str,
    title: &str,
    authors: &[Author],
    year: Option<i32>,
    journal: Option<&str>,
    doi: Option<&str>,
    url: Option<&str>,
    publisher: Option<&str>,
    abstract_text: Option<&str>,
) -> String {
    let mut map: BTreeMap<&str, serde_json::Value> = BTreeMap::new();
    map.insert("id", serde_json::Value::String(key.to_string()));
    map.insert(
        "type",
        serde_json::Value::String(map_item_type_to_csl(item_type)),
    );
    if !title.is_empty() {
        map.insert("title", serde_json::Value::String(title.to_string()));
    }
    if !authors.is_empty() {
        let arr: Vec<serde_json::Value> = authors
            .iter()
            .map(|a| {
                let mut o = serde_json::Map::new();
                o.insert(
                    "family".to_string(),
                    serde_json::Value::String(a.family.clone()),
                );
                if let Some(g) = &a.given {
                    o.insert("given".to_string(), serde_json::Value::String(g.clone()));
                }
                serde_json::Value::Object(o)
            })
            .collect();
        map.insert("author", serde_json::Value::Array(arr));
    }
    if let Some(y) = year {
        let issued = serde_json::json!({ "date-parts": [[y]] });
        map.insert("issued", issued);
    }
    if let Some(j) = journal {
        map.insert("container-title", serde_json::Value::String(j.to_string()));
    }
    if let Some(d) = doi {
        map.insert("DOI", serde_json::Value::String(d.to_string()));
    }
    if let Some(u) = url {
        map.insert("URL", serde_json::Value::String(u.to_string()));
    }
    if let Some(p) = publisher {
        map.insert("publisher", serde_json::Value::String(p.to_string()));
    }
    if let Some(a) = abstract_text {
        map.insert("abstract", serde_json::Value::String(a.to_string()));
    }
    serde_json::to_string(&map).unwrap_or_default()
}

/// Map hayagriva's `EntryType` (debug-formatted, lowercased) to the
/// closest standard CSL-JSON type. CSL-JSON's controlled vocabulary
/// is a superset of BibTeX's; for unmapped types we pass through the
/// lowercased name, which hayagriva tolerates.
fn map_item_type_to_csl(item_type: &str) -> String {
    match item_type {
        "article" => "article-journal".to_string(),
        "book" => "book".to_string(),
        "chapter" => "chapter".to_string(),
        "inproceedings" | "conference" => "paper-conference".to_string(),
        "thesis" => "thesis".to_string(),
        "report" => "report".to_string(),
        "online" | "web" | "webpage" => "webpage".to_string(),
        other => other.to_string(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::{Arc, Mutex};

    // --- BibTeX --------------------------------------------------------

    const TWO_ENTRY_BIB: &str = r#"
@article{smith2020,
  title  = {On the Nature of Reading},
  author = {Smith, Alice},
  journal = {Journal of Knowledge},
  year   = {2020},
  doi    = {10.1234/abc},
}

@book{jones2019,
  title     = {A Survey of Surveys},
  author    = {Jones, Robert and Lee, Hana},
  year      = {2019},
  publisher = {Academic Press},
}
"#;

    fn fixture_root() -> std::path::PathBuf {
        // Tests pass absolute paths so vault_root is irrelevant.
        std::path::PathBuf::from("/")
    }

    fn write_temp(name: &str, contents: &str) -> std::path::PathBuf {
        let dir = std::env::temp_dir().join(format!("slate-bib-test-{}", std::process::id()));
        std::fs::create_dir_all(&dir).unwrap();
        let path = dir.join(name);
        std::fs::write(&path, contents).unwrap();
        path
    }

    #[test]
    fn loads_simple_bibtex_with_multiple_entries() {
        let path = write_temp("simple.bib", TWO_ENTRY_BIB);
        let src = BibliographySource {
            path: path.to_string_lossy().into_owned(),
            format: BibFormat::BibTeX,
            watch: false,
        };
        let result = load_source(&src, &fixture_root()).unwrap();
        assert!(
            result.warnings.is_empty(),
            "warnings: {:?}",
            result.warnings
        );
        assert_eq!(result.entries.len(), 2);
        let smith = result
            .entries
            .iter()
            .find(|e| e.key == "smith2020")
            .unwrap();
        assert_eq!(smith.title, "On the Nature of Reading");
        assert_eq!(smith.authors.len(), 1);
        assert_eq!(smith.authors[0].family, "Smith");
        assert_eq!(smith.year, Some(2020));
        assert_eq!(smith.doi.as_deref(), Some("10.1234/abc"));
        assert_eq!(smith.journal.as_deref(), Some("Journal of Knowledge"));

        let jones = result
            .entries
            .iter()
            .find(|e| e.key == "jones2019")
            .unwrap();
        assert_eq!(jones.authors.len(), 2);
        assert_eq!(jones.authors[0].family, "Jones");
        assert_eq!(jones.authors[1].family, "Lee");
        assert_eq!(jones.publisher.as_deref(), Some("Academic Press"));
    }

    #[test]
    fn raw_csl_json_for_bibtex_entry_is_valid_csl_json() {
        let path = write_temp("simple-csl.bib", TWO_ENTRY_BIB);
        let src = BibliographySource {
            path: path.to_string_lossy().into_owned(),
            format: BibFormat::BibTeX,
            watch: false,
        };
        let result = load_source(&src, &fixture_root()).unwrap();
        let smith = result
            .entries
            .iter()
            .find(|e| e.key == "smith2020")
            .unwrap();
        let parsed: serde_json::Value = serde_json::from_str(&smith.raw_csl_json).unwrap();
        assert_eq!(parsed["id"], "smith2020");
        assert_eq!(parsed["type"], "article-journal");
        assert_eq!(parsed["title"], "On the Nature of Reading");
        assert_eq!(parsed["DOI"], "10.1234/abc");
        assert_eq!(parsed["issued"]["date-parts"][0][0], 2020);
    }

    #[test]
    fn loads_biblatex_with_online_entry() {
        const SRC: &str = r#"
@online{web2024,
  title  = {A Web Resource},
  author = {Walker, Maya},
  year   = {2024},
  url    = {https://example.com/notes},
}
"#;
        let path = write_temp("online.bib", SRC);
        let src = BibliographySource {
            path: path.to_string_lossy().into_owned(),
            format: BibFormat::BibLaTeX,
            watch: false,
        };
        let result = load_source(&src, &fixture_root()).unwrap();
        assert!(
            result.warnings.is_empty(),
            "warnings: {:?}",
            result.warnings
        );
        assert_eq!(result.entries.len(), 1);
        let entry = &result.entries[0];
        assert_eq!(entry.key, "web2024");
        assert_eq!(entry.url.as_deref(), Some("https://example.com/notes"));
    }

    // --- CSL-JSON ------------------------------------------------------

    #[test]
    fn loads_csl_json_array_and_extracts_fields() {
        const SRC: &str = r#"[
          {
            "id": "smith2020",
            "type": "article-journal",
            "title": "On the Nature of Reading",
            "author": [{ "family": "Smith", "given": "Alice" }],
            "issued": { "date-parts": [[2020]] },
            "container-title": "Journal of Knowledge",
            "DOI": "10.1234/abc"
          }
        ]"#;
        let path = write_temp("a.json", SRC);
        let src = BibliographySource {
            path: path.to_string_lossy().into_owned(),
            format: BibFormat::CslJson,
            watch: false,
        };
        let result = load_source(&src, &fixture_root()).unwrap();
        assert_eq!(result.entries.len(), 1);
        let e = &result.entries[0];
        assert_eq!(e.key, "smith2020");
        assert_eq!(e.title, "On the Nature of Reading");
        assert_eq!(e.year, Some(2020));
        assert_eq!(e.doi.as_deref(), Some("10.1234/abc"));
        assert_eq!(e.authors[0].family, "Smith");
        assert_eq!(e.authors[0].given.as_deref(), Some("Alice"));
    }

    #[test]
    fn csl_json_raw_field_round_trips_through_serde() {
        const SRC: &str = r#"[
          {
            "id": "smith2020",
            "type": "article-journal",
            "title": "On the Nature of Reading"
          }
        ]"#;
        let path = write_temp("rt.json", SRC);
        let src = BibliographySource {
            path: path.to_string_lossy().into_owned(),
            format: BibFormat::CslJson,
            watch: false,
        };
        let result = load_source(&src, &fixture_root()).unwrap();
        let raw = &result.entries[0].raw_csl_json;
        let parsed: serde_json::Value = serde_json::from_str(raw).unwrap();
        assert_eq!(parsed["id"], "smith2020");
        assert_eq!(parsed["title"], "On the Nature of Reading");
        // Re-serialising and re-parsing yields an equivalent value.
        let again: serde_json::Value =
            serde_json::from_str(&serde_json::to_string(&parsed).unwrap()).unwrap();
        assert_eq!(parsed, again);
    }

    // --- Resilience ----------------------------------------------------

    #[test]
    fn malformed_entry_in_a_valid_bib_produces_warning_not_failure() {
        const SRC: &str = r#"
@article{good1,
  title  = {Good One},
  author = {Author, One},
  year   = {2020},
}

@article{bad1,
  this entry is completely malformed and should fail to parse
}

@article{good2,
  title  = {Good Two},
  author = {Author, Two},
  year   = {2021},
}
"#;
        let path = write_temp("mixed.bib", SRC);
        let src = BibliographySource {
            path: path.to_string_lossy().into_owned(),
            format: BibFormat::BibTeX,
            watch: false,
        };
        let result = load_source(&src, &fixture_root()).unwrap();
        // Good entries make it through.
        let keys: Vec<&str> = result.entries.iter().map(|e| e.key.as_str()).collect();
        assert!(
            keys.contains(&"good1") && keys.contains(&"good2"),
            "expected good1 + good2, got {keys:?}"
        );
        // The malformed entry generates at least one warning.
        assert!(
            !result.warnings.is_empty(),
            "expected a warning for the malformed entry"
        );
    }

    #[test]
    fn missing_file_returns_bibsourceunreadable() {
        let src = BibliographySource {
            path: "/definitely/not/a/real/path.bib".to_string(),
            format: BibFormat::BibTeX,
            watch: false,
        };
        let err = load_source(&src, &fixture_root()).unwrap_err();
        match err {
            VaultError::BibSourceUnreadable { path, .. } => {
                assert!(path.contains("not/a/real/path.bib"));
            }
            other => panic!("expected BibSourceUnreadable, got {other:?}"),
        }
    }

    // --- Merge ---------------------------------------------------------

    fn entry(key: &str) -> BibEntry {
        BibEntry {
            key: key.to_string(),
            item_type: "article-journal".to_string(),
            title: format!("Title for {key}"),
            authors: vec![],
            year: None,
            journal: None,
            doi: None,
            url: None,
            publisher: None,
            abstract_text: None,
            raw_csl_json: format!("{{\"id\":\"{key}\"}}"),
        }
    }

    #[test]
    fn merge_first_source_wins_and_records_collision() {
        let a = ("a.bib".to_string(), vec![entry("x"), entry("y")]);
        let b = ("b.bib".to_string(), vec![entry("y"), entry("z")]);
        let (merged, collisions) = merge_sources(&[a, b]);
        let keys: Vec<&str> = merged.iter().map(|e| e.key.as_str()).collect();
        assert_eq!(keys, vec!["x", "y", "z"]);
        assert_eq!(collisions.len(), 1);
        assert_eq!(collisions[0].key, "y");
        assert_eq!(collisions[0].winning_source, "a.bib");
        assert_eq!(collisions[0].losing_source, "b.bib");
    }

    #[test]
    fn merge_of_disjoint_sources_has_no_collisions() {
        let a = ("a.bib".to_string(), vec![entry("x")]);
        let b = ("b.bib".to_string(), vec![entry("y")]);
        let (merged, collisions) = merge_sources(&[a, b]);
        assert_eq!(merged.len(), 2);
        assert!(collisions.is_empty());
    }

    // --- Debounce ------------------------------------------------------

    // These two synchronize on the **actual debounced events** (signalled
    // from the callback over a channel) rather than sleeping a fixed
    // duration and assuming the window has fired. The previous fixed-sleep
    // form raced the real timer: under CI load the debouncer thread could be
    // starved past the next `send`, collapsing two bursts into one event
    // (observed flake — `left: 1, right: 2`). Waiting for the event removes
    // the race; the multi-second `recv_timeout` is only a deadlock guard.

    /// Drain any debounced events the thread emitted, with a generous
    /// deadlock guard — `recv` blocks however long the window + scheduling
    /// actually takes under load.
    fn recv_event(rx: &std::sync::mpsc::Receiver<()>, label: &str) {
        rx.recv_timeout(Duration::from_secs(5))
            .unwrap_or_else(|_| panic!("debounced event not received: {label}"));
    }

    #[test]
    fn debouncer_collapses_burst_into_one_event() {
        let (events_tx, events_rx) = std::sync::mpsc::channel::<()>();
        let (tx, handle) = spawn_debouncer_with_callback(Duration::from_millis(50), move || {
            events_tx.send(()).unwrap();
        });
        // Five pulses sent back-to-back (no inter-pulse sleep) are queued in
        // the channel before the window can elapse, so the debouncer drains
        // them into a single burst deterministically.
        for _ in 0..5 {
            tx.send(()).unwrap();
        }
        recv_event(&events_rx, "the burst");
        // Dropping the sender exits the loop from its outer `recv()` with no
        // extra event; after join the callback's `events_tx` is dropped, so
        // `try_recv` confirms no second event was emitted.
        drop(tx);
        handle.join().unwrap();
        assert!(
            events_rx.try_recv().is_err(),
            "burst should collapse to exactly one debounced event"
        );
    }

    #[test]
    fn debouncer_separated_bursts_emit_separate_events() {
        let (events_tx, events_rx) = std::sync::mpsc::channel::<()>();
        let (tx, handle) = spawn_debouncer_with_callback(Duration::from_millis(50), move || {
            events_tx.send(()).unwrap();
        });
        // Wait for the first burst's event to FIRE before sending the second
        // pulse, so the two are guaranteed-separate bursts regardless of how
        // the scheduler treats the debouncer thread.
        tx.send(()).unwrap();
        recv_event(&events_rx, "first burst");
        tx.send(()).unwrap();
        recv_event(&events_rx, "second burst");
        drop(tx);
        handle.join().unwrap();
        assert!(
            events_rx.try_recv().is_err(),
            "exactly two debounced events — no extra beyond the two bursts"
        );
    }

    #[test]
    fn debouncer_thread_exits_when_sender_dropped() {
        let captured: Arc<Mutex<usize>> = Arc::new(Mutex::new(0));
        let captured_clone = captured.clone();
        let (tx, handle) = spawn_debouncer_with_callback(Duration::from_millis(20), move || {
            *captured_clone.lock().unwrap() += 1;
        });
        drop(tx);
        // Joining must terminate within a reasonable time without
        // having seen any pulses.
        handle.join().unwrap();
        assert_eq!(*captured.lock().unwrap(), 0);
    }
}
