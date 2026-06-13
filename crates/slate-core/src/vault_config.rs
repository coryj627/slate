// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Vault-root `slate.json` reader — the vault-shipped declarative
//! config (#411).
//!
//! Two config surfaces exist per vault:
//!
//! - `<vault>/slate.json` — authored WITH the vault and shipped
//!   alongside the notes (the demo vault documents this contract in
//!   its README). Declarative, portable, checked into whatever sync
//!   the vault uses.
//! - `<vault>/.slate/prefs.json` — written by the app (settings
//!   panel) into the cache directory. Machine-local overrides.
//!
//! Precedence: `.slate/prefs.json` wins wherever it speaks (it is
//! the user's explicit app-side choice); `slate.json` fills in
//! wherever prefs are silent. Before #411 the app read ONLY
//! `.slate/prefs.json`, so a vault shipping its config at the root —
//! like the demo vault — never registered a bibliography source and
//! every citation reported "Unresolved".
//!
//! V1 honors two `slate.json` sections:
//!
//! - `citations`: `bibliography` (path), `cite_style` (style id or
//!   path), `available_styles` (ids/paths), `csl_directory` (joined
//!   onto bare style ids).
//! - `templates.directory`: overrides the Obsidian-convention
//!   `Templates/` auto-detect.
//!
//! The `rendering` section is recognized but carries no options in
//! V1 — math/diagrams/code renderers are fixed (MathCAT / Mermaid /
//! tree-sitter). Unknown keys are tolerated silently for forward
//! compatibility, matching the prefs.json rule.

use std::path::Path;

use crate::VaultError;
use crate::citations::bibliography::{BibFormat, BibliographySource};
use crate::citations::prefs::CitationsPrefs;

/// The slice of `<vault>/slate.json` V1 understands.
#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub(crate) struct RootVaultConfig {
    /// Citations config mapped into the same shape `.slate/prefs.json`
    /// produces, so downstream consumers don't care which file fed it.
    /// `None` when `slate.json` is absent or has no `citations` key.
    pub citations: Option<CitationsPrefs>,
    /// `templates.directory`, verbatim. `None` when absent.
    pub templates_directory: Option<String>,
}

/// Read `<vault>/slate.json` if present. Absence is not an error —
/// most vaults won't ship one.
///
/// A present-but-malformed file DEGRADES to the default config
/// rather than erroring (red-team M1 on #411): `slate.json` is
/// foreign-authored and may be mid-sync (the demo vault lives in
/// iCloud), and failing the whole vault open over it would deny
/// access to the user's notes — strictly worse than the pre-#411
/// behavior where the file wasn't read at all. The app-written
/// `.slate/prefs.json` keeps its typed-error policy. Trade-off: a
/// typo'd slate.json silently yields no citations; the Settings
/// panel shows "No sources configured", which is the discoverable
/// breadcrumb until a vault-health surface exists.
pub(crate) fn read_root_vault_config(vault_root: &Path) -> RootVaultConfig {
    let path = vault_root.join("slate.json");
    let contents = match std::fs::read_to_string(&path) {
        Ok(s) => s,
        Err(_) => return RootVaultConfig::default(),
    };
    parse_root_vault_config(&contents, &path.display().to_string()).unwrap_or_default()
}

/// Parse a `slate.json` document. Split from the read for tests.
pub(crate) fn parse_root_vault_config(
    contents: &str,
    path_for_error: &str,
) -> Result<RootVaultConfig, VaultError> {
    let value: serde_json::Value =
        serde_json::from_str(contents).map_err(|e| VaultError::PrefsUnreadable {
            path: path_for_error.to_string(),
            reason: format!("JSON parse error: {e}"),
        })?;

    let templates_directory = value
        .get("templates")
        .and_then(|t| t.get("directory"))
        .and_then(|d| d.as_str())
        .map(str::to_string);

    let citations = match value.get("citations") {
        None | Some(serde_json::Value::Null) => None,
        Some(c) => {
            let obj = c.as_object().ok_or_else(|| VaultError::PrefsUnreadable {
                path: path_for_error.to_string(),
                reason: "`citations` must be a JSON object".to_string(),
            })?;
            Some(map_citations_section(obj))
        }
    };

    Ok(RootVaultConfig {
        citations,
        templates_directory,
    })
}

/// Map the `slate.json` `citations` section onto [`CitationsPrefs`].
///
/// - `bibliography` → one source; format inferred from the
///   extension (`.json` → CSL-JSON, everything else → BibTeX).
/// - `cite_style` → `default_style`, resolved via `style_path`.
/// - `available_styles` → `additional_styles`, minus the default
///   (the style picker chains default + additional, and a duplicate
///   entry would double-list it).
fn map_citations_section(obj: &serde_json::Map<String, serde_json::Value>) -> CitationsPrefs {
    let csl_directory = obj
        .get("csl_directory")
        .and_then(|v| v.as_str())
        .map(str::trim);

    let mut sources = Vec::new();
    if let Some(bib) = obj
        .get("bibliography")
        .and_then(|v| v.as_str())
        .map(str::trim)
        .filter(|s| !s.is_empty())
    {
        let format = if Path::new(bib)
            .extension()
            .and_then(|e| e.to_str())
            .is_some_and(|e| e.eq_ignore_ascii_case("json"))
        {
            BibFormat::CslJson
        } else {
            BibFormat::BibTeX
        };
        sources.push(BibliographySource {
            path: bib.to_string(),
            format,
            watch: false,
        });
    }

    let default_style = obj
        .get("cite_style")
        .and_then(|v| v.as_str())
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .map(|s| style_path(s, csl_directory));

    let additional_styles = obj
        .get("available_styles")
        .and_then(|v| v.as_array())
        .map(|arr| {
            arr.iter()
                .filter_map(|v| v.as_str())
                .map(str::trim)
                .filter(|s| !s.is_empty())
                .map(|s| style_path(s, csl_directory))
                .filter(|p| Some(p) != default_style.as_ref())
                .collect()
        })
        .unwrap_or_default();

    CitationsPrefs {
        sources,
        default_style,
        additional_styles,
    }
}

/// Resolve a style entry to a vault-relative `.csl` path. Bare ids
/// (`"ieee"`) join onto `csl_directory` and gain the extension;
/// anything that already looks like a path (contains `/` or ends in
/// `.csl`) is taken as-is, with the extension appended if missing.
fn style_path(style: &str, csl_directory: Option<&str>) -> String {
    let with_ext = if style.to_ascii_lowercase().ends_with(".csl") {
        style.to_string()
    } else {
        format!("{style}.csl")
    };
    if style.contains('/') || style.to_ascii_lowercase().ends_with(".csl") {
        return with_ext;
    }
    match csl_directory {
        Some(dir) if !dir.is_empty() => format!("{}/{with_ext}", dir.trim_end_matches('/')),
        _ => with_ext,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    /// The exact shape the demo vault ships (README-documented
    /// contract that #411 was filed against).
    const DEMO_VAULT_SLATE_JSON: &str = r#"{
      "vault": { "name": "Slate demo vault", "version": 1 },
      "citations": {
        "bibliography": "library.bib",
        "cite_style": "ieee",
        "available_styles": ["ieee", "chicago-author-date", "apa"],
        "csl_directory": "csl"
      },
      "templates": {
        "directory": "templates",
        "daily_note": "daily-note.md",
        "meeting_note": "meeting-note.md"
      },
      "rendering": { "math": "mathcat", "diagrams": "mermaid", "code": "tree-sitter" }
    }"#;

    #[test]
    fn parses_demo_vault_shape() {
        let cfg = parse_root_vault_config(DEMO_VAULT_SLATE_JSON, "slate.json").unwrap();
        let citations = cfg.citations.expect("citations section present");
        assert_eq!(citations.sources.len(), 1);
        assert_eq!(citations.sources[0].path, "library.bib");
        assert_eq!(citations.sources[0].format, BibFormat::BibTeX);
        assert!(!citations.sources[0].watch);
        assert_eq!(citations.default_style.as_deref(), Some("csl/ieee.csl"));
        assert_eq!(
            citations.additional_styles,
            vec![
                "csl/chicago-author-date.csl".to_string(),
                "csl/apa.csl".to_string()
            ],
            "default style must not double-list in additional_styles"
        );
        assert_eq!(cfg.templates_directory.as_deref(), Some("templates"));
    }

    #[test]
    fn absent_file_is_default() {
        let tmp = tempfile::tempdir().unwrap();
        let cfg = read_root_vault_config(tmp.path());
        assert_eq!(cfg, RootVaultConfig::default());
    }

    #[test]
    fn malformed_file_degrades_to_default_on_read() {
        // Red-team M1: a foreign-authored (possibly mid-sync)
        // slate.json must never block vault open.
        let tmp = tempfile::tempdir().unwrap();
        std::fs::write(tmp.path().join("slate.json"), "{nope").unwrap();
        assert_eq!(
            read_root_vault_config(tmp.path()),
            RootVaultConfig::default()
        );
    }

    #[test]
    fn citations_null_means_absent() {
        let cfg = parse_root_vault_config(r#"{ "citations": null }"#, "x").unwrap();
        assert!(cfg.citations.is_none());
    }

    #[test]
    fn empty_bibliography_and_style_strings_are_unset() {
        let cfg = parse_root_vault_config(
            r#"{ "citations": { "bibliography": "", "cite_style": " " } }"#,
            "x",
        )
        .unwrap();
        let c = cfg.citations.unwrap();
        assert!(c.sources.is_empty());
        assert!(c.default_style.is_none());
    }

    #[test]
    fn malformed_json_is_typed_error() {
        let err = parse_root_vault_config("{nope", "slate.json").unwrap_err();
        match err {
            VaultError::PrefsUnreadable { .. } => {}
            other => panic!("expected PrefsUnreadable, got {other:?}"),
        }
    }

    #[test]
    fn missing_sections_are_none() {
        let cfg = parse_root_vault_config(r#"{ "vault": { "version": 1 } }"#, "x").unwrap();
        assert!(cfg.citations.is_none());
        assert!(cfg.templates_directory.is_none());
    }

    #[test]
    fn whitespace_around_paths_and_styles_is_trimmed() {
        // Codoki PR #427: untrimmed values caused format
        // misdetection ("refs.JSON ") and unresolvable styles
        // ("ieee " → "ieee .csl").
        let cfg = parse_root_vault_config(
            r#"{ "citations": {
                "bibliography": " refs.JSON ",
                "cite_style": " ieee ",
                "available_styles": [" apa ", "  "],
                "csl_directory": " csl "
            } }"#,
            "x",
        )
        .unwrap();
        let c = cfg.citations.unwrap();
        assert_eq!(c.sources[0].path, "refs.JSON");
        assert_eq!(c.sources[0].format, BibFormat::CslJson);
        assert_eq!(c.default_style.as_deref(), Some("csl/ieee.csl"));
        assert_eq!(c.additional_styles, vec!["csl/apa.csl".to_string()]);
    }

    #[test]
    fn csl_json_bibliography_format_inferred_from_extension() {
        let cfg =
            parse_root_vault_config(r#"{ "citations": { "bibliography": "refs.JSON" } }"#, "x")
                .unwrap();
        let c = cfg.citations.unwrap();
        assert_eq!(c.sources[0].format, BibFormat::CslJson);
    }

    #[test]
    fn style_paths_already_pathlike_pass_through() {
        let cfg = parse_root_vault_config(
            r#"{ "citations": { "cite_style": "styles/custom.csl", "csl_directory": "csl" } }"#,
            "x",
        )
        .unwrap();
        assert_eq!(
            cfg.citations.unwrap().default_style.as_deref(),
            Some("styles/custom.csl")
        );
    }

    #[test]
    fn bare_style_without_directory_gains_extension_only() {
        let cfg =
            parse_root_vault_config(r#"{ "citations": { "cite_style": "ieee" } }"#, "x").unwrap();
        assert_eq!(
            cfg.citations.unwrap().default_style.as_deref(),
            Some("ieee.csl")
        );
    }
}
