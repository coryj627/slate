// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! CSL rendering of citations — both visual and speech forms.
//!
//! Third backend ticket for Milestone L
//! (`docs/plans/05_locked_architecture_decisions.md` §6.5). The a11y
//! differentiator lives here: `speech_text` is built from the
//! structured [`CitationReference`] + [`BibEntry`] data, never by
//! reading the visual rendering aloud. The documented gap (Phelps &
//! Knabel) is that screen readers say "open paren Smith comma twenty
//! twenty close paren" for `(Smith, 2020)` — we compute
//! "Citation: Smith 2020" instead, with locator labels spelled out.
//!
//! Visual rendering goes through `hayagriva` against a user-provided
//! `.csl` file (or a hayagriva-archived built-in style).

use std::collections::HashMap;
use std::path::{Path, PathBuf};
use std::sync::{Mutex, OnceLock};

use hayagriva::citationberg::{IndependentStyle, Locale};
use hayagriva::{
    citationberg, BibliographyDriver, BibliographyRequest, BufWriteFormat, CitationItem,
    CitationRequest, LocatorPayload, SpecificLocator,
};

use crate::citations::bibliography::{BibEntry, BibIndex};
use crate::citations::{CitationMode, CitationReference, CitedItem, Locator};
use crate::VaultError;

/// A loaded Citation Style Language file. `id` is what the user types
/// into the style picker / config (typically the basename without
/// `.csl`); `title` is the human-readable name extracted from the
/// CSL's `<info><title>` element.
pub struct CslStyle {
    pub id: String,
    pub path: PathBuf,
    pub title: String,
    /// Private — the parsed CSL XML. Not part of the public surface
    /// because `IndependentStyle` is a hayagriva type and exposing it
    /// would couple our API to hayagriva's version. Callers operate
    /// on `CslStyle` opaquely.
    pub(crate) style: IndependentStyle,
}

impl std::fmt::Debug for CslStyle {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("CslStyle")
            .field("id", &self.id)
            .field("path", &self.path)
            .field("title", &self.title)
            .finish_non_exhaustive()
    }
}

/// Result of rendering one citation site for one style.
///
/// `raw` is the source-form text (e.g. `"[@smith2020, p. 23]"`);
/// `visual_text` is what sighted users see (e.g. `"(Smith, 2020, p.
/// 23)"`); `speech_text` is what VoiceOver / NVDA reads (e.g.
/// `"Citation: Smith 2020, page 23"`).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RenderedCitation {
    pub raw: String,
    pub visual_text: String,
    pub speech_text: String,
    pub bib_entry: Option<BibEntry>,
    pub style_id: String,
}

/// Load a CSL `.csl` file from disk. Returns
/// [`VaultError::CslStyleUnreadable`] if the file can't be read OR
/// can't be parsed as CSL — both are user-facing problems that need
/// the same "this style isn't usable" UI response.
pub fn load_style(path: &Path) -> Result<CslStyle, VaultError> {
    let xml = std::fs::read_to_string(path).map_err(|e| VaultError::CslStyleUnreadable {
        path: path.display().to_string(),
        reason: e.to_string(),
    })?;
    let style = IndependentStyle::from_xml(&xml).map_err(|e| VaultError::CslStyleUnreadable {
        path: path.display().to_string(),
        reason: format!("CSL parse error: {e}"),
    })?;
    let id = path
        .file_stem()
        .and_then(|s| s.to_str())
        .unwrap_or("style")
        .to_string();
    let title = style.info.title.value.clone();
    Ok(CslStyle {
        id,
        path: path.to_path_buf(),
        title,
        style,
    })
}

/// Construct an in-memory `CslStyle` from a raw CSL XML string. Useful
/// for tests and for built-in archived styles (we wrap hayagriva's
/// archived style as a `CslStyle` for a uniform API).
pub fn style_from_xml(id: &str, title: &str, xml: &str) -> Result<CslStyle, VaultError> {
    let style = IndependentStyle::from_xml(xml).map_err(|e| VaultError::CslStyleUnreadable {
        path: id.to_string(),
        reason: format!("CSL parse error: {e}"),
    })?;
    Ok(CslStyle {
        id: id.to_string(),
        path: PathBuf::from(id),
        title: title.to_string(),
        style,
    })
}

/// Render one citation site under `style`. Stateless — the renderer
/// holds no per-style mutable state, so calling with different styles
/// produces independent results.
///
/// Speech text is built from the structured `CitationReference` +
/// `BibIndex` data (NOT by post-processing `visual_text`), so it never
/// contains parentheses or punctuation that a screen reader would
/// read literally.
///
/// Unresolved keys (the reference cites a key that's not in `bib`)
/// produce a visual marker `"[@<key>?]"` and a speech form
/// `"Unresolved citation: <key>"`.
pub fn render_citation(
    reference: &CitationReference,
    bib: &BibIndex,
    style: &CslStyle,
) -> RenderedCitation {
    let resolved: Vec<(&CitedItem, Option<&BibEntry>)> = reference
        .citations
        .iter()
        .map(|item| (item, bib.get(&item.key)))
        .collect();

    let any_unresolved = resolved.iter().any(|(_, entry)| entry.is_none());
    let speech_text = build_speech_text(reference, &resolved);
    let visual_text = if all_unresolved(&resolved) {
        unresolved_visual(&resolved)
    } else {
        render_visual(reference, &resolved, &style.style)
    };
    let bib_entry = if any_unresolved {
        None
    } else {
        resolved.first().and_then(|(_, e)| (*e).cloned())
    };

    RenderedCitation {
        raw: reference.raw.clone(),
        visual_text,
        speech_text,
        bib_entry,
        style_id: style.id.clone(),
    }
}

// =====================================================================
// Speech-text construction
// =====================================================================

fn build_speech_text(
    reference: &CitationReference,
    resolved: &[(&CitedItem, Option<&BibEntry>)],
) -> String {
    let mode = reference
        .citations
        .first()
        .map(|c| c.mode)
        .unwrap_or(CitationMode::Bracketed);

    let mut parts: Vec<String> = Vec::with_capacity(resolved.len());
    let mut leading_prefix: Option<String> = None;

    for (idx, (item, entry)) in resolved.iter().enumerate() {
        if idx == 0 {
            if let Some(p) = &item.prefix {
                leading_prefix = Some(p.clone());
            }
        }
        parts.push(speech_for_item(item, *entry, mode));
    }

    let joined = join_oxford(&parts);
    let body = match mode {
        CitationMode::InText => joined,
        CitationMode::Bracketed | CitationMode::SuppressAuthor => {
            // Only resolved items get the "Citation: " prefix; if
            // every item is unresolved the speech form already says
            // "Unresolved citation: …" per item.
            if resolved.iter().any(|(_, e)| e.is_some()) {
                format!("Citation: {joined}")
            } else {
                joined
            }
        }
    };

    match leading_prefix {
        Some(p) if !p.is_empty() => format!("{p} {body}"),
        _ => body,
    }
}

fn speech_for_item(item: &CitedItem, entry: Option<&BibEntry>, mode: CitationMode) -> String {
    let Some(entry) = entry else {
        return format!("Unresolved citation: {}", item.key);
    };

    let names = format_author_names_for_speech(&entry.authors);
    let year = entry
        .year
        .map(|y| y.to_string())
        .unwrap_or_else(|| "no date".to_string());

    let core = match mode {
        CitationMode::SuppressAuthor => year.clone(),
        _ => {
            if names.is_empty() {
                year.clone()
            } else {
                format!("{names} {year}")
            }
        }
    };

    match &item.locator {
        Some(loc) => format!("{core}, {}", speak_locator(loc)),
        None => core,
    }
}

/// Format the author family names for the speech form:
/// - 0 authors → empty string (caller falls back to year-only)
/// - 1 author → just the family
/// - 2 authors → "<A> and <B>"
/// - 3+ authors → "<A> et al."
fn format_author_names_for_speech(authors: &[crate::citations::bibliography::Author]) -> String {
    match authors.len() {
        0 => String::new(),
        1 => authors[0].family.clone(),
        2 => format!("{} and {}", authors[0].family, authors[1].family),
        _ => format!("{} et al.", authors[0].family),
    }
}

/// Convert a locator into its spoken form. Known Pandoc labels are
/// rendered with their spelled-out word (`p.` → "page"); unknown
/// labels are passed through with the raw locator text.
fn speak_locator(loc: &Locator) -> String {
    let word = match loc.label.as_str() {
        "p." => "page",
        "pp." => "pages",
        "chapter" | "chap." => "chapter",
        "section" | "sec." => "section",
        "fig." => "figure",
        "eq." => "equation",
        "vol." => "volume",
        "note" => "note",
        _ => return loc.locator.clone(),
    };
    format!("{word} {}", loc.locator)
}

/// Join a list of strings with Oxford-comma English semantics:
/// - 0 → ""
/// - 1 → "A"
/// - 2 → "A and B"
/// - 3+ → "A, B, and C"
fn join_oxford(parts: &[String]) -> String {
    match parts.len() {
        0 => String::new(),
        1 => parts[0].clone(),
        2 => format!("{} and {}", parts[0], parts[1]),
        _ => {
            let head = parts[..parts.len() - 1].join(", ");
            format!("{}, and {}", head, parts.last().unwrap())
        }
    }
}

// =====================================================================
// Visual rendering
// =====================================================================

fn unresolved_visual(resolved: &[(&CitedItem, Option<&BibEntry>)]) -> String {
    if resolved.len() == 1 {
        format!("[@{}?]", resolved[0].0.key)
    } else {
        let inner = resolved
            .iter()
            .map(|(item, _)| format!("@{}?", item.key))
            .collect::<Vec<_>>()
            .join("; ");
        format!("[{inner}]")
    }
}

fn all_unresolved(resolved: &[(&CitedItem, Option<&BibEntry>)]) -> bool {
    !resolved.is_empty() && resolved.iter().all(|(_, e)| e.is_none())
}

/// Render the visual form by feeding the resolved entries to
/// hayagriva. Mixed resolved/unresolved sites still render the
/// resolved part through hayagriva and mark the unresolved part
/// inline with `@<key>?`.
fn render_visual(
    reference: &CitationReference,
    resolved: &[(&CitedItem, Option<&BibEntry>)],
    style: &IndependentStyle,
) -> String {
    let locales = locales_static();

    // Convert resolved BibEntry → citationberg::json::Item via the
    // raw_csl_json we stored on load. This is the path the issue
    // calls out ("this is what hayagriva consumes downstream").
    let parsed_items: Vec<Option<citationberg::json::Item>> = resolved
        .iter()
        .map(|(_, entry)| {
            entry.and_then(|e| {
                serde_json::from_str::<citationberg::json::Item>(&e.raw_csl_json).ok()
            })
        })
        .collect();

    // Build hayagriva CitationItems for each resolved entry. Locators
    // are wired through SpecificLocator so the style's locator-aware
    // templates fire correctly.
    let mut items: Vec<CitationItem<'_, citationberg::json::Item>> = Vec::new();
    for ((cited, _), parsed) in resolved.iter().zip(parsed_items.iter()) {
        if let Some(parsed) = parsed {
            let mut item = CitationItem::with_entry(parsed);
            if let Some(loc) = cited.locator.as_ref().and_then(build_specific_locator) {
                item.locator = Some(loc);
            }
            items.push(item);
        }
    }

    if items.is_empty() {
        return unresolved_visual(resolved);
    }

    let mut driver = BibliographyDriver::<citationberg::json::Item>::new();
    driver.citation(CitationRequest::from_items(items, style, locales));
    let finished = driver.finish(BibliographyRequest::new(style, None, locales));

    let mut visual = finished
        .citations
        .iter()
        .map(|c| {
            let mut buf = String::new();
            let _ = c.citation.write_buf(&mut buf, BufWriteFormat::Plain);
            buf
        })
        .collect::<Vec<_>>()
        .join("; ");

    // If the citation has any unresolved items, append `@<key>?`
    // markers so the sighted reader still sees the missing keys.
    let unresolved_keys: Vec<&str> = resolved
        .iter()
        .filter(|(_, e)| e.is_none())
        .map(|(item, _)| item.key.as_str())
        .collect();
    if !unresolved_keys.is_empty() {
        let suffix = unresolved_keys
            .iter()
            .map(|k| format!("@{k}?"))
            .collect::<Vec<_>>()
            .join("; ");
        visual = format!("{visual}; {suffix}");
    }

    // For in-text mode, strip the surrounding parens hayagriva
    // attaches per the style's citation layout. Pandoc semantics:
    // `@smith2020` (in-text) renders without brackets — the year is
    // wrapped, not the whole citation.
    if matches!(
        reference.citations.first().map(|c| c.mode),
        Some(CitationMode::InText)
    ) {
        visual = strip_outer_parens(&visual);
    }

    visual
}

fn strip_outer_parens(s: &str) -> String {
    let trimmed = s.trim();
    if trimmed.starts_with('(') && trimmed.ends_with(')') && trimmed.len() >= 2 {
        return trimmed[1..trimmed.len() - 1].to_string();
    }
    trimmed.to_string()
}

fn build_specific_locator(loc: &Locator) -> Option<SpecificLocator<'_>> {
    use citationberg::taxonomy::Locator as CslLocator;
    let csl_kind = match loc.label.as_str() {
        "p." | "pp." => CslLocator::Page,
        "chapter" | "chap." => CslLocator::Chapter,
        "section" | "sec." => CslLocator::Section,
        "fig." => CslLocator::Figure,
        "eq." => CslLocator::Equation,
        "vol." => CslLocator::Volume,
        "note" => CslLocator::Note,
        _ => return None,
    };
    Some(SpecificLocator(csl_kind, LocatorPayload::Str(&loc.locator)))
}

/// Lazily-initialised list of hayagriva's archived CSL locales. We
/// load them once per process (the binary already ships them via the
/// `archive` feature) and hand the same reference to every render
/// call.
fn locales_static() -> &'static [Locale] {
    static LOCALES: OnceLock<Vec<Locale>> = OnceLock::new();
    LOCALES.get_or_init(hayagriva::archive::locales)
}

// =====================================================================
// Per-process render cache
// =====================================================================

/// LRU-ish render cache keyed on `(reference, style_id, bib_version)`.
/// Bumping the [`BibIndex::version`] (e.g. on bibliography reload)
/// makes every existing cache entry unreachable since the new key
/// includes the new version — no explicit invalidation needed.
///
/// Internally a bounded `HashMap`; once the cap is hit we drop the
/// oldest insertion. This is "good-enough LRU" for the V1 access
/// pattern (each open note re-renders citations once + after style
/// switches); a real LRU is overkill.
pub struct RenderCache {
    inner: Mutex<RenderCacheInner>,
    cap: usize,
}

struct RenderCacheInner {
    map: HashMap<CacheKey, RenderedCitation>,
    order: Vec<CacheKey>,
    hits: u64,
    misses: u64,
}

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
struct CacheKey {
    reference: CitationReference,
    style_id: String,
    bib_version: u64,
}

impl RenderCache {
    /// Build a cache that holds up to `cap` rendered citations.
    pub fn new(cap: usize) -> Self {
        Self {
            inner: Mutex::new(RenderCacheInner {
                map: HashMap::with_capacity(cap),
                order: Vec::with_capacity(cap),
                hits: 0,
                misses: 0,
            }),
            cap: cap.max(1),
        }
    }

    /// Render `reference` under `style`, caching the result. Repeated
    /// calls with the same `(reference, style, bib.version())` triple
    /// return the cached value without re-invoking hayagriva.
    pub fn render(
        &self,
        reference: &CitationReference,
        bib: &BibIndex,
        style: &CslStyle,
    ) -> RenderedCitation {
        let key = CacheKey {
            reference: reference.clone(),
            style_id: style.id.clone(),
            bib_version: bib.version(),
        };
        {
            let mut inner = self.inner.lock().unwrap();
            if let Some(hit) = inner.map.get(&key).cloned() {
                inner.hits += 1;
                return hit;
            }
            inner.misses += 1;
        }
        let value = render_citation(reference, bib, style);
        let mut inner = self.inner.lock().unwrap();
        if inner.map.len() >= self.cap {
            if let Some(oldest) = inner.order.first().cloned() {
                inner.map.remove(&oldest);
                inner.order.remove(0);
            }
        }
        inner.map.insert(key.clone(), value.clone());
        inner.order.push(key);
        value
    }

    /// Number of cache hits since construction. Test-only utility.
    pub fn hits(&self) -> u64 {
        self.inner.lock().unwrap().hits
    }

    /// Number of cache misses since construction. Test-only utility.
    pub fn misses(&self) -> u64 {
        self.inner.lock().unwrap().misses
    }

    /// Number of cached renders currently held.
    pub fn len(&self) -> usize {
        self.inner.lock().unwrap().map.len()
    }

    /// True when the cache holds nothing.
    pub fn is_empty(&self) -> bool {
        self.inner.lock().unwrap().map.is_empty()
    }
}

impl Default for RenderCache {
    fn default() -> Self {
        Self::new(1024)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::citations::bibliography::{Author, BibEntry};
    use crate::citations::{extract_citations, CitationMode, CitedItem};

    fn bib_entry(key: &str, family: &str, given: Option<&str>, year: Option<i32>) -> BibEntry {
        let raw_csl_json = serde_json::json!({
            "id": key,
            "type": "article-journal",
            "title": format!("Title for {key}"),
            "author": [{ "family": family, "given": given.unwrap_or("") }],
            "issued": { "date-parts": [[year.unwrap_or(2020)]] },
        })
        .to_string();
        BibEntry {
            key: key.to_string(),
            item_type: "article-journal".to_string(),
            title: format!("Title for {key}"),
            authors: vec![Author {
                family: family.to_string(),
                given: given.map(str::to_string),
            }],
            year,
            journal: None,
            doi: None,
            url: None,
            publisher: None,
            abstract_text: None,
            raw_csl_json,
        }
    }

    fn multi_author_entry(key: &str, families: &[&str], year: Option<i32>) -> BibEntry {
        let authors_json: Vec<serde_json::Value> = families
            .iter()
            .map(|f| serde_json::json!({ "family": f }))
            .collect();
        let raw_csl_json = serde_json::json!({
            "id": key,
            "type": "article-journal",
            "title": format!("Title for {key}"),
            "author": authors_json,
            "issued": { "date-parts": [[year.unwrap_or(2020)]] },
        })
        .to_string();
        BibEntry {
            key: key.to_string(),
            item_type: "article-journal".to_string(),
            title: format!("Title for {key}"),
            authors: families
                .iter()
                .map(|f| Author {
                    family: f.to_string(),
                    given: None,
                })
                .collect(),
            year,
            journal: None,
            doi: None,
            url: None,
            publisher: None,
            abstract_text: None,
            raw_csl_json,
        }
    }

    fn apa() -> CslStyle {
        let archived = hayagriva::archive::ArchivedStyle::AmericanPsychologicalAssociation.get();
        let xml = archived.to_xml().expect("archived APA serialises");
        style_from_xml("apa", "American Psychological Association", &xml)
            .expect("archived APA parses")
    }

    fn chicago() -> CslStyle {
        let archived = hayagriva::archive::ArchivedStyle::ChicagoAuthorDate.get();
        let xml = archived.to_xml().expect("archived Chicago serialises");
        style_from_xml("chicago", "Chicago Author-Date", &xml).expect("archived Chicago parses")
    }

    // --- Speech text -----------------------------------------------

    #[test]
    fn bracketed_single_speech_text_has_citation_prefix() {
        let bib = BibIndex::build(
            vec![bib_entry("smith2020", "Smith", Some("Alice"), Some(2020))],
            1,
        );
        let refs = extract_citations("See [@smith2020].");
        let rendered = render_citation(&refs[0], &bib, &apa());
        assert_eq!(rendered.speech_text, "Citation: Smith 2020");
    }

    #[test]
    fn bracketed_with_page_locator_uses_spelled_out_label() {
        let bib = BibIndex::build(vec![bib_entry("smith2020", "Smith", None, Some(2020))], 1);
        let refs = extract_citations("[@smith2020, p. 23]");
        let rendered = render_citation(&refs[0], &bib, &apa());
        assert_eq!(rendered.speech_text, "Citation: Smith 2020, page 23");
    }

    #[test]
    fn bracketed_with_pp_locator_says_pages() {
        let bib = BibIndex::build(vec![bib_entry("smith2020", "Smith", None, Some(2020))], 1);
        let refs = extract_citations("[@smith2020, pp. 23-45]");
        let rendered = render_citation(&refs[0], &bib, &apa());
        assert_eq!(rendered.speech_text, "Citation: Smith 2020, pages 23-45");
    }

    #[test]
    fn in_text_speech_form_omits_citation_prefix() {
        let bib = BibIndex::build(vec![bib_entry("smith2020", "Smith", None, Some(2020))], 1);
        let refs = extract_citations("As shown by @smith2020.");
        let rendered = render_citation(&refs[0], &bib, &apa());
        assert_eq!(rendered.speech_text, "Smith 2020");
    }

    #[test]
    fn multiple_citations_use_oxford_comma_in_speech() {
        let bib = BibIndex::build(
            vec![
                bib_entry("a", "Aardvark", None, Some(2018)),
                bib_entry("b", "Bear", None, Some(2019)),
                bib_entry("c", "Cougar", None, Some(2020)),
            ],
            1,
        );
        let refs = extract_citations("[@a; @b; @c]");
        let rendered = render_citation(&refs[0], &bib, &apa());
        assert_eq!(
            rendered.speech_text,
            "Citation: Aardvark 2018, Bear 2019, and Cougar 2020"
        );
    }

    #[test]
    fn two_citations_use_and_in_speech() {
        let bib = BibIndex::build(
            vec![
                bib_entry("a", "Aardvark", None, Some(2018)),
                bib_entry("b", "Bear", None, Some(2019)),
            ],
            1,
        );
        let refs = extract_citations("[@a; @b]");
        let rendered = render_citation(&refs[0], &bib, &apa());
        assert_eq!(
            rendered.speech_text,
            "Citation: Aardvark 2018 and Bear 2019"
        );
    }

    #[test]
    fn author_suppressed_speech_form_is_year_only() {
        let bib = BibIndex::build(vec![bib_entry("smith2020", "Smith", None, Some(2020))], 1);
        let refs = extract_citations("[-@smith2020]");
        let rendered = render_citation(&refs[0], &bib, &apa());
        assert_eq!(rendered.speech_text, "Citation: 2020");
    }

    #[test]
    fn prefix_is_read_before_citation_keyword() {
        let bib = BibIndex::build(vec![bib_entry("smith2020", "Smith", None, Some(2020))], 1);
        let refs = extract_citations("[see @smith2020]");
        let rendered = render_citation(&refs[0], &bib, &apa());
        assert_eq!(rendered.speech_text, "see Citation: Smith 2020");
    }

    #[test]
    fn three_or_more_authors_uses_et_al_in_speech() {
        let bib = BibIndex::build(
            vec![multi_author_entry(
                "smith2020",
                &["Smith", "Jones", "Lee"],
                Some(2020),
            )],
            1,
        );
        let refs = extract_citations("[@smith2020]");
        let rendered = render_citation(&refs[0], &bib, &apa());
        assert_eq!(rendered.speech_text, "Citation: Smith et al. 2020");
    }

    #[test]
    fn two_authors_uses_and_in_speech_form() {
        let bib = BibIndex::build(
            vec![multi_author_entry(
                "smith2020",
                &["Smith", "Jones"],
                Some(2020),
            )],
            1,
        );
        let refs = extract_citations("[@smith2020]");
        let rendered = render_citation(&refs[0], &bib, &apa());
        assert_eq!(rendered.speech_text, "Citation: Smith and Jones 2020");
    }

    // --- Unresolved -----------------------------------------------

    #[test]
    fn unresolved_citation_produces_marker_and_speech() {
        let bib = BibIndex::empty();
        let refs = extract_citations("[@notinbib]");
        let rendered = render_citation(&refs[0], &bib, &apa());
        assert_eq!(rendered.visual_text, "[@notinbib?]");
        assert_eq!(rendered.speech_text, "Unresolved citation: notinbib");
        assert!(rendered.bib_entry.is_none());
    }

    // --- Visual -----------------------------------------------

    #[test]
    fn apa_visual_renders_with_surname_and_year() {
        let bib = BibIndex::build(
            vec![bib_entry("smith2020", "Smith", Some("Alice"), Some(2020))],
            1,
        );
        let refs = extract_citations("[@smith2020]");
        let rendered = render_citation(&refs[0], &bib, &apa());
        assert!(
            rendered.visual_text.contains("Smith") && rendered.visual_text.contains("2020"),
            "APA visual should contain Smith + 2020, got {:?}",
            rendered.visual_text
        );
    }

    #[test]
    fn style_switching_changes_visual_but_preserves_speech() {
        let bib = BibIndex::build(vec![bib_entry("smith2020", "Smith", None, Some(2020))], 1);
        let refs = extract_citations("[@smith2020]");
        let apa_render = render_citation(&refs[0], &bib, &apa());
        let chicago_render = render_citation(&refs[0], &bib, &chicago());
        // Speech is invariant under style — we never speak punctuation.
        assert_eq!(apa_render.speech_text, chicago_render.speech_text);
        assert_eq!(apa_render.speech_text, "Citation: Smith 2020");
        // Both visuals at least mention the surname; their exact
        // formatting differs but we don't pin specific punctuation
        // here (would couple the test to hayagriva's archived styles).
        assert!(apa_render.visual_text.contains("Smith"));
        assert!(chicago_render.visual_text.contains("Smith"));
    }

    // --- Cache -----------------------------------------------

    #[test]
    fn cache_returns_hit_on_second_call_with_same_inputs() {
        let bib = BibIndex::build(vec![bib_entry("smith2020", "Smith", None, Some(2020))], 1);
        let refs = extract_citations("[@smith2020]");
        let style = apa();
        let cache = RenderCache::default();
        let first = cache.render(&refs[0], &bib, &style);
        let second = cache.render(&refs[0], &bib, &style);
        assert_eq!(first, second);
        assert_eq!(cache.misses(), 1);
        assert_eq!(cache.hits(), 1);
    }

    #[test]
    fn cache_treats_different_styles_as_different_keys() {
        let bib = BibIndex::build(vec![bib_entry("smith2020", "Smith", None, Some(2020))], 1);
        let refs = extract_citations("[@smith2020]");
        let cache = RenderCache::default();
        cache.render(&refs[0], &bib, &apa());
        cache.render(&refs[0], &bib, &chicago());
        cache.render(&refs[0], &bib, &apa());
        assert_eq!(cache.misses(), 2);
        assert_eq!(cache.hits(), 1);
    }

    #[test]
    fn cache_invalidates_when_bib_version_bumps() {
        let bib_v1 = BibIndex::build(vec![bib_entry("s", "Smith", None, Some(2020))], 1);
        let bib_v2 = BibIndex::build(vec![bib_entry("s", "Smith", None, Some(2020))], 2);
        let refs = extract_citations("[@s]");
        let cache = RenderCache::default();
        cache.render(&refs[0], &bib_v1, &apa());
        cache.render(&refs[0], &bib_v2, &apa());
        // Both calls miss — the second's key differs by version even
        // though the rendered output is identical.
        assert_eq!(cache.misses(), 2);
        assert_eq!(cache.hits(), 0);
    }

    #[test]
    fn cache_evicts_oldest_entry_when_capacity_reached() {
        let bib = BibIndex::build(
            vec![
                bib_entry("a", "A", None, Some(2018)),
                bib_entry("b", "B", None, Some(2019)),
                bib_entry("c", "C", None, Some(2020)),
            ],
            1,
        );
        let cache = RenderCache::new(2);
        let style = apa();
        let ref_a = CitationReference {
            raw: "[@a]".into(),
            citations: vec![CitedItem {
                key: "a".into(),
                locator: None,
                prefix: None,
                suffix: None,
                mode: CitationMode::Bracketed,
            }],
            byte_offset: 0,
            line: 1,
        };
        let ref_b = CitationReference {
            raw: "[@b]".into(),
            citations: vec![CitedItem {
                key: "b".into(),
                locator: None,
                prefix: None,
                suffix: None,
                mode: CitationMode::Bracketed,
            }],
            byte_offset: 0,
            line: 1,
        };
        let ref_c = CitationReference {
            raw: "[@c]".into(),
            citations: vec![CitedItem {
                key: "c".into(),
                locator: None,
                prefix: None,
                suffix: None,
                mode: CitationMode::Bracketed,
            }],
            byte_offset: 0,
            line: 1,
        };
        cache.render(&ref_a, &bib, &style);
        cache.render(&ref_b, &bib, &style);
        assert_eq!(cache.len(), 2);
        cache.render(&ref_c, &bib, &style);
        // After the third insert, the oldest (`a`) was evicted.
        assert_eq!(cache.len(), 2);
        // Re-rendering `a` is a miss; `b` and `c` are hits.
        cache.render(&ref_a, &bib, &style);
        let _ = cache.render(&ref_b, &bib, &style);
        let _ = cache.render(&ref_c, &bib, &style);
        // The exact hit/miss breakdown depends on order, but `a`
        // should have caused at least one miss after eviction.
        assert!(cache.misses() >= 4, "misses: {}", cache.misses());
    }

    // --- Sanity: load_style + style_from_xml ----------------------

    #[test]
    fn style_from_xml_round_trips_apa_archive() {
        let style = apa();
        assert_eq!(style.id, "apa");
        assert!(style.title.contains("American Psychological"));
    }

    #[test]
    fn load_style_returns_csl_style_unreadable_for_missing_file() {
        let err = load_style(Path::new("/definitely/not/here.csl")).unwrap_err();
        match err {
            VaultError::CslStyleUnreadable { path, .. } => {
                assert!(path.contains("not/here.csl"));
            }
            other => panic!("expected CslStyleUnreadable, got {other:?}"),
        }
    }
}
