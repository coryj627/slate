// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `.slate/prefs.json` reader — the per-vault config file introduced
//! by Milestone L. V1 carries only a `bibliography` section; future
//! milestones may extend the schema. Forward-compatibility rule:
//! unknown top-level keys are tolerated silently so older Slate
//! versions don't choke on a vault saved by a newer one.

use std::path::Path;

use crate::citations::bibliography::{BibFormat, BibliographySource};
use crate::VaultError;

/// Top-level `.slate/prefs.json` shape that V1 understands. The
/// `bibliography` section is optional — its absence means "no
/// bibliography configured", not an error.
#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct CitationsPrefs {
    pub sources: Vec<BibliographySource>,
    /// Vault-relative or absolute path to the default CSL style.
    /// `None` means the renderer uses a hayagriva-archived default
    /// (currently APA 7th).
    pub default_style: Option<String>,
    /// Additional `.csl` paths the user wants available for ad-hoc
    /// switching (style picker).
    pub additional_styles: Vec<String>,
}

/// Read `<vault>/.slate/prefs.json` if it exists. Returns an empty
/// `CitationsPrefs` when:
///
/// - The file doesn't exist.
/// - The file exists but has no `bibliography` key (the file may
///   carry other prefs for unrelated subsystems).
///
/// Returns [`VaultError::PrefsUnreadable`] when the file exists but
/// can't be opened OR its JSON doesn't parse. Unknown top-level keys
/// are silently ignored for forward compatibility.
pub fn read_citations_prefs(vault_root: &Path) -> Result<CitationsPrefs, VaultError> {
    let path = vault_root.join(".slate").join("prefs.json");
    let contents = match std::fs::read_to_string(&path) {
        Ok(s) => s,
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => return Ok(CitationsPrefs::default()),
        Err(e) => {
            return Err(VaultError::PrefsUnreadable {
                path: path.display().to_string(),
                reason: e.to_string(),
            });
        }
    };
    parse_citations_prefs(&contents, &path.display().to_string())
}

/// Parse a JSON string into [`CitationsPrefs`]. Public for tests
/// and for the slate-mac settings panel (#281), which round-trips
/// edits through the same shape.
pub fn parse_citations_prefs(
    contents: &str,
    path_for_error: &str,
) -> Result<CitationsPrefs, VaultError> {
    let value: serde_json::Value =
        serde_json::from_str(contents).map_err(|e| VaultError::PrefsUnreadable {
            path: path_for_error.to_string(),
            reason: format!("JSON parse error: {e}"),
        })?;
    let Some(bib) = value.get("bibliography") else {
        return Ok(CitationsPrefs::default());
    };
    let bib = bib.as_object().ok_or_else(|| VaultError::PrefsUnreadable {
        path: path_for_error.to_string(),
        reason: "`bibliography` must be a JSON object".to_string(),
    })?;

    let mut sources = Vec::new();
    if let Some(arr) = bib.get("sources").and_then(|v| v.as_array()) {
        for raw in arr {
            if let Some(src) = parse_one_source(raw) {
                sources.push(src);
            }
        }
    }
    let default_style = bib
        .get("default_style")
        .and_then(|v| v.as_str())
        .map(str::to_string);
    let additional_styles = bib
        .get("additional_styles")
        .and_then(|v| v.as_array())
        .map(|arr| {
            arr.iter()
                .filter_map(|v| v.as_str().map(str::to_string))
                .collect()
        })
        .unwrap_or_default();

    Ok(CitationsPrefs {
        sources,
        default_style,
        additional_styles,
    })
}

fn parse_one_source(value: &serde_json::Value) -> Option<BibliographySource> {
    let obj = value.as_object()?;
    let path = obj.get("path").and_then(|v| v.as_str())?.to_string();
    let format = match obj
        .get("format")
        .and_then(|v| v.as_str())
        .unwrap_or("BibTeX")
    {
        "BibLaTeX" | "biblatex" => BibFormat::BibLaTeX,
        "CslJson" | "csl-json" | "CSL-JSON" => BibFormat::CslJson,
        _ => BibFormat::BibTeX,
    };
    let watch = obj.get("watch").and_then(|v| v.as_bool()).unwrap_or(false);
    Some(BibliographySource {
        path,
        format,
        watch,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;

    #[test]
    fn missing_file_returns_default_prefs() {
        let dir = TempDir::new().unwrap();
        let prefs = read_citations_prefs(dir.path()).unwrap();
        assert_eq!(prefs, CitationsPrefs::default());
    }

    #[test]
    fn missing_bibliography_key_returns_default_prefs() {
        let dir = TempDir::new().unwrap();
        std::fs::create_dir_all(dir.path().join(".slate")).unwrap();
        std::fs::write(
            dir.path().join(".slate").join("prefs.json"),
            r#"{ "ui": { "theme": "dark" } }"#,
        )
        .unwrap();
        let prefs = read_citations_prefs(dir.path()).unwrap();
        assert!(prefs.sources.is_empty());
        assert!(prefs.default_style.is_none());
    }

    #[test]
    fn parses_full_bibliography_section() {
        let json = r#"{
          "bibliography": {
            "sources": [
              { "path": "library.bib", "format": "BibTeX", "watch": true },
              { "path": "extra.json", "format": "CSL-JSON" }
            ],
            "default_style": "styles/apa-7th.csl",
            "additional_styles": ["styles/chicago.csl", "styles/ieee.csl"]
          }
        }"#;
        let prefs = parse_citations_prefs(json, "fixture").unwrap();
        assert_eq!(prefs.sources.len(), 2);
        assert_eq!(prefs.sources[0].path, "library.bib");
        assert_eq!(prefs.sources[0].format, BibFormat::BibTeX);
        assert!(prefs.sources[0].watch);
        assert_eq!(prefs.sources[1].format, BibFormat::CslJson);
        assert!(!prefs.sources[1].watch);
        assert_eq!(prefs.default_style.as_deref(), Some("styles/apa-7th.csl"));
        assert_eq!(prefs.additional_styles.len(), 2);
    }

    #[test]
    fn unknown_top_level_keys_are_ignored() {
        let json = r#"{
          "bibliography": { "sources": [] },
          "future_feature": { "anything": true }
        }"#;
        let prefs = parse_citations_prefs(json, "fixture").unwrap();
        assert!(prefs.sources.is_empty());
    }

    #[test]
    fn malformed_json_returns_prefs_unreadable() {
        let json = r#"{ not valid json"#;
        let err = parse_citations_prefs(json, "fixture").unwrap_err();
        match err {
            VaultError::PrefsUnreadable { path, .. } => assert_eq!(path, "fixture"),
            other => panic!("expected PrefsUnreadable, got {other:?}"),
        }
    }

    #[test]
    fn unknown_format_string_falls_back_to_bibtex() {
        let json = r#"{
          "bibliography": {
            "sources": [{ "path": "x.bib", "format": "MysteryFormat" }]
          }
        }"#;
        let prefs = parse_citations_prefs(json, "fixture").unwrap();
        assert_eq!(prefs.sources.len(), 1);
        assert_eq!(prefs.sources[0].format, BibFormat::BibTeX);
    }
}
