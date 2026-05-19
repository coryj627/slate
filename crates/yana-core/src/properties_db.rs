//! SQLite storage + query surface for frontmatter properties.
//!
//! Schema lives in `migrations/005_properties.sql`. Rows are
//! produced exclusively by the scanner's slow path; queries serve
//! `get_file_metadata`'s `properties` field and the
//! `files_with_property` audit / search API.

use rusqlite::{params, Connection, Transaction};
use serde_json::Value as JsonValue;

use crate::frontmatter::{extract_frontmatter, Property, PropertyValue};
use crate::session::{FileSummary, Page, Paging};
use crate::VaultError;

const KIND_TEXT: &str = "text";
const KIND_NUMBER: &str = "number";
const KIND_BOOLEAN: &str = "boolean";
const KIND_DATE: &str = "date";
const KIND_DATETIME: &str = "datetime";
const KIND_WIKILINK: &str = "wikilink";
const KIND_LIST: &str = "list";
const KIND_TAG_LIST: &str = "tag_list";

/// Atomically replace the property rows for `file_id` with the
/// frontmatter parsed from `markdown_source`. Called by the
/// scanner's slow path.
///
/// Warnings from the parser are deliberately swallowed at this
/// layer: the indexer can surface them later through a dedicated
/// "frontmatter issues" query (left as a #D3 follow-up if testers
/// need it). The acceptance criteria treat partial parsing as
/// success — we store what we got.
pub(crate) fn replace_properties_for_file(
    tx: &Transaction,
    file_id: i64,
    markdown_source: &str,
) -> Result<(), VaultError> {
    tx.execute(
        "DELETE FROM properties WHERE file_id = ?1",
        params![file_id],
    )?;
    tx.execute(
        "DELETE FROM properties_list_values WHERE file_id = ?1",
        params![file_id],
    )?;
    let (props, _warnings) = extract_frontmatter(markdown_source);
    if props.is_empty() {
        return Ok(());
    }
    let mut prop_stmt = tx.prepare_cached(
        "INSERT INTO properties (file_id, ordinal, key, value_kind, value_text, value_text_norm)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
    )?;
    let mut list_stmt = tx.prepare_cached(
        "INSERT INTO properties_list_values (file_id, key, value_norm)
         VALUES (?1, ?2, ?3)",
    )?;
    for (ordinal, prop) in props.into_iter().enumerate() {
        let (kind, value_text) = serialize_value(&prop.value);
        let value_text_norm = normalize_atomic_value(&prop.value);
        prop_stmt.execute(params![
            file_id,
            ordinal as i64,
            prop.key,
            kind,
            value_text,
            value_text_norm,
        ])?;
        // Expand list / tag_list into the side table so
        // files_with_property can hit a direct (key, value_norm)
        // index instead of LEFT JOIN json_each. Empty lists
        // produce no side rows.
        for element_norm in list_elements_norm(&prop.value) {
            list_stmt.execute(params![file_id, prop.key, element_norm])?;
        }
    }
    Ok(())
}

/// Pre-lowercased, JSON-unwrapped form of an atomic `PropertyValue`.
/// Returns an empty string for list / tag_list (they're matched via
/// `properties_list_values` instead). The caller writes the result
/// into the `value_text_norm` column so the partial composite index
/// `idx_properties_key_norm` can serve case-insensitive equality
/// lookups directly (#92 item 3).
fn normalize_atomic_value(value: &PropertyValue) -> String {
    match value {
        PropertyValue::Text(s)
        | PropertyValue::Date(s)
        | PropertyValue::Datetime(s)
        | PropertyValue::Wikilink(s) => s.to_lowercase(),
        PropertyValue::Integer(i) => i.to_string(),
        PropertyValue::Float(f) => {
            // Match the JSON serialisation: finite floats render as
            // their JSON form, non-finite as the Rust string form
            // (`NaN`, `inf`, `-inf`). Both branches lowercase — a
            // user typing `nan` in a `files_with_property` query
            // wouldn't match a stored `NaN` otherwise (Codoki PR
            // 100 callout).
            if f.is_finite() {
                JsonValue::from(*f).to_string().to_lowercase()
            } else {
                f.to_string().to_lowercase()
            }
        }
        PropertyValue::Boolean(b) => {
            if *b {
                "true".to_string()
            } else {
                "false".to_string()
            }
        }
        PropertyValue::List(_) | PropertyValue::TagList(_) => String::new(),
    }
}

/// Pre-lowercased string form of every element of a list / tag_list,
/// for population of `properties_list_values`. Returns an empty vec
/// for atomic kinds (they go through the `properties.value_text_norm`
/// path).
fn list_elements_norm(value: &PropertyValue) -> Vec<String> {
    match value {
        PropertyValue::List(items) => items
            .iter()
            .map(|item| match item {
                PropertyValue::Text(s)
                | PropertyValue::Date(s)
                | PropertyValue::Datetime(s)
                | PropertyValue::Wikilink(s) => s.to_lowercase(),
                PropertyValue::Integer(i) => i.to_string(),
                PropertyValue::Float(f) => {
                    if f.is_finite() {
                        JsonValue::from(*f).to_string().to_lowercase()
                    } else {
                        f.to_string().to_lowercase()
                    }
                }
                PropertyValue::Boolean(b) => if *b { "true" } else { "false" }.to_string(),
                // Nested lists / tag-lists shouldn't occur (the parser
                // flattens to text), but be safe: skip them.
                _ => String::new(),
            })
            .filter(|s| !s.is_empty())
            .collect(),
        PropertyValue::TagList(tags) => tags.iter().map(|t| t.to_lowercase()).collect(),
        _ => Vec::new(),
    }
}

/// Fetch every property row for a file in document order. Used by
/// `get_file_metadata` so the Properties Panel renders in the same
/// order the user authored.
pub(crate) fn properties_for_file(
    conn: &Connection,
    file_id: i64,
) -> Result<Vec<Property>, VaultError> {
    let mut stmt = conn.prepare_cached(
        "SELECT key, value_kind, value_text
         FROM properties
         WHERE file_id = ?1
         ORDER BY ordinal ASC",
    )?;
    let rows = stmt.query_map(params![file_id], |row| {
        Ok((
            row.get::<_, String>(0)?,
            row.get::<_, String>(1)?,
            row.get::<_, String>(2)?,
        ))
    })?;
    let mut out = Vec::new();
    for r in rows {
        let (key, kind, value_text) = r?;
        // Deserialize failures fall back to Text so the Properties
        // Panel still surfaces the row (with the raw JSON visible)
        // instead of dropping it silently.
        let value = deserialize_value(&kind, &value_text)
            .unwrap_or_else(|| PropertyValue::Text(value_text.clone()));
        out.push(Property { key, value });
    }
    Ok(out)
}

/// Paged list of files that have property `key` whose value matches
/// `value` (case-insensitive). For list and tag_list properties,
/// each element is searched independently via SQLite's
/// `json_each` — matching any element counts as a hit on the file.
///
/// Matching semantics for atomic kinds:
///   - text / date / datetime / wikilink: case-insensitive string
///     equality against the unwrapped value.
///   - number: numeric equality via SQLite's coercion (so `"42"` and
///     `42` both match the stored `42`).
///   - boolean: matches `"true"` / `"false"` against the stored JSON
///     literal (also tolerates `"1"` / `"0"` because the JSON-extract
///     branch coerces booleans to integers).
///
/// Non-finite floats are stored as JSON strings (NaN/Inf aren't legal
/// JSON numbers); they're searchable via their string form.
pub(crate) fn files_with_property(
    conn: &Connection,
    key: &str,
    value: &str,
    paging: Paging,
) -> Result<Page<FileSummary>, VaultError> {
    let limit = paging.limit.clamp(1, 1000);
    let after_path = paging.cursor.clone();
    let target = value.to_lowercase();

    // Two index-backed paths into one CTE so the JOIN logic runs
    // once and both the row fetch and the COUNT(*) window function
    // share the materialized matches. Previous shape ran two
    // separate queries (one for rows, one for COUNT(DISTINCT)) each
    // doing the same expensive `LEFT JOIN json_each` (#92 item 3).
    //
    //   - Atomic branch: hits `idx_properties_key_norm` (partial,
    //     covers `key + value_text_norm` for non-list rows).
    //   - List branch: hits `idx_properties_list_lookup` against
    //     the `properties_list_values` side table.
    //
    // UNION (not UNION ALL) dedupes file_ids when a vault has
    // multiple matching property rows on the same file.
    let sql = "
        WITH matches AS (
            SELECT files.id, files.path, files.name, files.mtime_ms,
                   files.size_bytes, files.is_markdown
            FROM files
            JOIN properties p ON p.file_id = files.id
            WHERE p.key = ?1
              AND p.value_kind NOT IN ('list', 'tag_list')
              AND p.value_text_norm = ?2
            UNION
            SELECT files.id, files.path, files.name, files.mtime_ms,
                   files.size_bytes, files.is_markdown
            FROM files
            JOIN properties_list_values plv ON plv.file_id = files.id
            WHERE plv.key = ?1 AND plv.value_norm = ?2
        )
        SELECT path, name, mtime_ms, size_bytes, is_markdown,
               (SELECT COUNT(*) FROM matches) AS total_filtered
        FROM matches
        WHERE (?3 IS NULL OR path > ?3)
        ORDER BY path ASC
        LIMIT ?4
    ";
    let mut stmt = conn.prepare_cached(sql)?;

    let mut total_filtered: u64 = 0;
    let rows: Vec<FileSummary> = stmt
        .query_map(params![key, target, after_path, limit as i64 + 1], |row| {
            let summary = FileSummary {
                path: row.get::<_, String>(0)?,
                name: row.get::<_, String>(1)?,
                mtime_ms: row.get::<_, i64>(2)?,
                size_bytes: row.get::<_, i64>(3)? as u64,
                is_markdown: row.get::<_, i64>(4)? != 0,
            };
            // The (SELECT COUNT(*) FROM matches) subquery returns
            // the same total on every row — pick up the first
            // value we see and ignore subsequent reads.
            let count: i64 = row.get(5)?;
            total_filtered = count as u64;
            Ok(summary)
        })?
        .collect::<Result<Vec<_>, _>>()?;

    // When there were no matching rows the SELECT yielded zero
    // rows, so `total_filtered` was never assigned by the closure.
    // 0 is correct in that case — no matches means total is 0.
    //
    // Edge case: paging cursor lands past the last match. The
    // count subquery wouldn't fire (no outer rows), so we'd report
    // total_filtered=0 even though earlier pages had matches.
    // Resolve by re-running just the COUNT branch if the row
    // fetch was empty AND a cursor was provided.
    if rows.is_empty() && after_path.is_some() {
        total_filtered = conn.query_row(
            "
            WITH matches AS (
                SELECT files.id
                FROM files
                JOIN properties p ON p.file_id = files.id
                WHERE p.key = ?1
                  AND p.value_kind NOT IN ('list', 'tag_list')
                  AND p.value_text_norm = ?2
                UNION
                SELECT files.id
                FROM files
                JOIN properties_list_values plv ON plv.file_id = files.id
                WHERE plv.key = ?1 AND plv.value_norm = ?2
            )
            SELECT COUNT(*) FROM matches
            ",
            params![key, target],
            |row| row.get::<_, i64>(0),
        )? as u64;
    }

    let has_more = rows.len() > limit as usize;
    let items: Vec<FileSummary> = rows.into_iter().take(limit as usize).collect();
    let next_cursor = if has_more {
        items.last().map(|f| f.path.clone())
    } else {
        None
    };
    Ok(Page {
        items,
        next_cursor,
        total_filtered,
    })
}

// --- JSON serialization ---

/// Encode a `PropertyValue` into `(kind, value_text)` for storage.
///
/// `value_text` is always valid JSON so `files_with_property` can
/// safely call `json_extract` / `json_each` on it.
fn serialize_value(value: &PropertyValue) -> (&'static str, String) {
    match value {
        PropertyValue::Text(s) => (KIND_TEXT, JsonValue::String(s.clone()).to_string()),
        PropertyValue::Integer(i) => (KIND_NUMBER, JsonValue::from(*i).to_string()),
        PropertyValue::Float(f) => (KIND_NUMBER, finite_float_to_json(*f)),
        PropertyValue::Boolean(b) => (KIND_BOOLEAN, JsonValue::Bool(*b).to_string()),
        PropertyValue::Date(s) => (KIND_DATE, JsonValue::String(s.clone()).to_string()),
        PropertyValue::Datetime(s) => (KIND_DATETIME, JsonValue::String(s.clone()).to_string()),
        PropertyValue::Wikilink(t) => (KIND_WIKILINK, JsonValue::String(t.clone()).to_string()),
        PropertyValue::List(items) => {
            let arr: Vec<JsonValue> = items.iter().map(property_value_to_json).collect();
            (KIND_LIST, JsonValue::Array(arr).to_string())
        }
        PropertyValue::TagList(tags) => {
            let arr: Vec<JsonValue> = tags.iter().cloned().map(JsonValue::String).collect();
            (KIND_TAG_LIST, JsonValue::Array(arr).to_string())
        }
    }
}

fn finite_float_to_json(f: f64) -> String {
    // JSON has no NaN / Inf. Stringify to text fallback so we don't
    // produce an unparseable value_text (which would break
    // `files_with_property`).
    if f.is_finite() {
        JsonValue::from(f).to_string()
    } else {
        JsonValue::String(f.to_string()).to_string()
    }
}

fn property_value_to_json(value: &PropertyValue) -> JsonValue {
    match value {
        PropertyValue::Text(s) => JsonValue::String(s.clone()),
        PropertyValue::Integer(i) => JsonValue::from(*i),
        PropertyValue::Float(f) => {
            if f.is_finite() {
                JsonValue::from(*f)
            } else {
                JsonValue::String(f.to_string())
            }
        }
        PropertyValue::Boolean(b) => JsonValue::Bool(*b),
        PropertyValue::Date(s) | PropertyValue::Datetime(s) | PropertyValue::Wikilink(s) => {
            JsonValue::String(s.clone())
        }
        PropertyValue::List(items) => {
            JsonValue::Array(items.iter().map(property_value_to_json).collect())
        }
        PropertyValue::TagList(tags) => {
            JsonValue::Array(tags.iter().cloned().map(JsonValue::String).collect())
        }
    }
}

/// Decode `(kind, value_text)` back into a `PropertyValue`. Returns
/// `None` if the JSON is malformed or doesn't match the recorded
/// kind — the caller falls back to a Text representation so the row
/// stays visible.
fn deserialize_value(kind: &str, value_text: &str) -> Option<PropertyValue> {
    let v: JsonValue = serde_json::from_str(value_text).ok()?;
    match kind {
        KIND_TEXT => Some(PropertyValue::Text(v.as_str()?.to_string())),
        KIND_NUMBER => {
            if let Some(i) = v.as_i64() {
                Some(PropertyValue::Integer(i))
            } else {
                v.as_f64().map(PropertyValue::Float)
            }
        }
        KIND_BOOLEAN => Some(PropertyValue::Boolean(v.as_bool()?)),
        KIND_DATE => Some(PropertyValue::Date(v.as_str()?.to_string())),
        KIND_DATETIME => Some(PropertyValue::Datetime(v.as_str()?.to_string())),
        KIND_WIKILINK => Some(PropertyValue::Wikilink(v.as_str()?.to_string())),
        KIND_LIST => {
            let arr = v.as_array()?;
            let items: Vec<PropertyValue> = arr.iter().map(json_to_property_value).collect();
            Some(PropertyValue::List(items))
        }
        KIND_TAG_LIST => {
            let arr = v.as_array()?;
            let tags: Vec<String> = arr
                .iter()
                .filter_map(|t| t.as_str().map(String::from))
                .collect();
            Some(PropertyValue::TagList(tags))
        }
        _ => None,
    }
}

fn json_to_property_value(value: &JsonValue) -> PropertyValue {
    match value {
        JsonValue::String(s) => PropertyValue::Text(s.clone()),
        JsonValue::Bool(b) => PropertyValue::Boolean(*b),
        JsonValue::Number(n) => {
            if let Some(i) = n.as_i64() {
                PropertyValue::Integer(i)
            } else if let Some(f) = n.as_f64() {
                PropertyValue::Float(f)
            } else {
                PropertyValue::Text(n.to_string())
            }
        }
        JsonValue::Null => PropertyValue::Text(String::new()),
        other => PropertyValue::Text(other.to_string()),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn round_trip(value: PropertyValue) -> PropertyValue {
        let (kind, text) = serialize_value(&value);
        deserialize_value(kind, &text).expect("deserialize failed")
    }

    #[test]
    fn text_round_trips() {
        assert_eq!(
            round_trip(PropertyValue::Text("hello".to_string())),
            PropertyValue::Text("hello".to_string())
        );
    }

    #[test]
    fn integer_round_trips_as_integer() {
        assert_eq!(
            round_trip(PropertyValue::Integer(42)),
            PropertyValue::Integer(42)
        );
    }

    #[test]
    fn float_round_trips_as_float() {
        match round_trip(PropertyValue::Float(3.5)) {
            PropertyValue::Float(f) => assert!((f - 3.5).abs() < 1e-9),
            other => panic!("expected Float, got {other:?}"),
        }
    }

    #[test]
    fn boolean_round_trips() {
        assert_eq!(
            round_trip(PropertyValue::Boolean(true)),
            PropertyValue::Boolean(true)
        );
    }

    #[test]
    fn date_round_trips() {
        let v = PropertyValue::Date("2024-01-02".to_string());
        assert_eq!(round_trip(v.clone()), v);
    }

    #[test]
    fn datetime_round_trips() {
        let v = PropertyValue::Datetime("2024-01-02T03:04:05Z".to_string());
        assert_eq!(round_trip(v.clone()), v);
    }

    #[test]
    fn wikilink_round_trips() {
        let v = PropertyValue::Wikilink("Alpha".to_string());
        assert_eq!(round_trip(v.clone()), v);
    }

    #[test]
    fn list_round_trips() {
        let v = PropertyValue::List(vec![
            PropertyValue::Text("alpha".to_string()),
            PropertyValue::Text("beta".to_string()),
        ]);
        assert_eq!(round_trip(v.clone()), v);
    }

    #[test]
    fn tag_list_round_trips() {
        let v = PropertyValue::TagList(vec!["alpha".to_string(), "beta".to_string()]);
        assert_eq!(round_trip(v.clone()), v);
    }

    #[test]
    fn special_characters_in_text_round_trip() {
        // JSON escaping must handle quotes, backslashes, newlines.
        let v = PropertyValue::Text("hello \"world\"\nline 2 \\path".to_string());
        assert_eq!(round_trip(v.clone()), v);
    }

    #[test]
    fn non_finite_float_falls_back_to_text() {
        // NaN/Inf can't be JSON numbers; we encode them as strings so
        // the row stays parseable. Deserialize routes through the
        // text path because the JSON value is a string.
        let (kind, text) = serialize_value(&PropertyValue::Float(f64::NAN));
        assert_eq!(kind, KIND_NUMBER);
        // The text should parse as JSON and yield a string value.
        let v: JsonValue = serde_json::from_str(&text).unwrap();
        assert!(v.is_string());
    }

    // --- value_text_norm + properties_list_values normalisation (#92 item 3) ---

    #[test]
    fn normalize_atomic_lowercases_text() {
        assert_eq!(
            normalize_atomic_value(&PropertyValue::Text("Hello World".to_string())),
            "hello world"
        );
    }

    #[test]
    fn normalize_atomic_integer_renders_as_decimal() {
        assert_eq!(normalize_atomic_value(&PropertyValue::Integer(42)), "42");
    }

    #[test]
    fn normalize_atomic_boolean_renders_as_true_false() {
        assert_eq!(
            normalize_atomic_value(&PropertyValue::Boolean(true)),
            "true"
        );
        assert_eq!(
            normalize_atomic_value(&PropertyValue::Boolean(false)),
            "false"
        );
    }

    #[test]
    fn normalize_atomic_list_kinds_return_empty() {
        // list / tag_list go through properties_list_values, not
        // value_text_norm. Anything non-empty here would corrupt the
        // partial-index covering condition.
        assert_eq!(
            normalize_atomic_value(&PropertyValue::List(vec![PropertyValue::Text(
                "a".to_string()
            )])),
            ""
        );
        assert_eq!(
            normalize_atomic_value(&PropertyValue::TagList(vec!["x".to_string()])),
            ""
        );
    }

    #[test]
    fn list_elements_norm_lowercases_each_element() {
        let v = PropertyValue::List(vec![
            PropertyValue::Text("Alpha".to_string()),
            PropertyValue::Text("BETA".to_string()),
        ]);
        assert_eq!(list_elements_norm(&v), vec!["alpha", "beta"]);
    }

    #[test]
    fn list_elements_norm_handles_tag_list() {
        let v = PropertyValue::TagList(vec!["Science".to_string(), "Math".to_string()]);
        assert_eq!(list_elements_norm(&v), vec!["science", "math"]);
    }

    #[test]
    fn list_elements_norm_atomic_returns_empty() {
        assert_eq!(
            list_elements_norm(&PropertyValue::Text("x".to_string())),
            Vec::<String>::new()
        );
    }

    #[test]
    fn normalize_atomic_finite_float() {
        // Match the JSON form (no trailing zeros, no leading `+`) so
        // a user typing `1.5` in a query hits stored `1.5`.
        assert_eq!(normalize_atomic_value(&PropertyValue::Float(1.5)), "1.5");
        assert_eq!(
            normalize_atomic_value(&PropertyValue::Float(1.0e-3)),
            "0.001"
        );
    }

    #[test]
    fn normalize_atomic_non_finite_float_is_lowercase() {
        // Codoki PR 100: prior shape returned `NaN` / `inf` /
        // `-inf` from `f.to_string()`; case-mismatching against
        // `target.to_lowercase()` in the query path meant users
        // typing `nan` got no results.
        assert_eq!(
            normalize_atomic_value(&PropertyValue::Float(f64::NAN)),
            "nan"
        );
        assert_eq!(
            normalize_atomic_value(&PropertyValue::Float(f64::INFINITY)),
            "inf"
        );
        assert_eq!(
            normalize_atomic_value(&PropertyValue::Float(f64::NEG_INFINITY)),
            "-inf"
        );
    }

    #[test]
    fn list_elements_norm_non_finite_float_is_lowercase() {
        let v = PropertyValue::List(vec![
            PropertyValue::Float(f64::NAN),
            PropertyValue::Float(f64::INFINITY),
        ]);
        assert_eq!(list_elements_norm(&v), vec!["nan", "inf"]);
    }

    #[test]
    fn migration_007_backfills_boolean_value_text_norm_to_true_false() {
        // Codoki PR 100 (high): the generic
        // `lower(IFNULL(json_extract(value_text, '$'), value_text))`
        // landed `"1"` / `"0"` in value_text_norm for boolean rows
        // because json_extract coerces JSON booleans to SQLite
        // ints. A boolean carve-out in the CASE keeps them on the
        // raw value_text (`true` / `false`). This test stands up
        // the pre-migration shape, runs the migration SQL, and
        // asserts the post-migration normalisation.
        use rusqlite::Connection;
        let conn = Connection::open_in_memory().unwrap();
        // Minimal pre-migration shape — files + properties + the
        // index migration 007 drops. Migrations 1–6 do more than
        // this, but only `files` and `properties` are touched by
        // migration 007's UPDATE/INSERT.
        conn.execute_batch(
            "CREATE TABLE files (
                id INTEGER PRIMARY KEY,
                path TEXT,
                name TEXT,
                mtime_ms INTEGER,
                size_bytes INTEGER,
                is_markdown INTEGER
            );
            CREATE TABLE properties (
                file_id INTEGER NOT NULL,
                ordinal INTEGER NOT NULL,
                key TEXT NOT NULL,
                value_kind TEXT NOT NULL,
                value_text TEXT NOT NULL,
                PRIMARY KEY (file_id, ordinal)
            );
            CREATE INDEX idx_properties_key_value ON properties(key, value_text);
            INSERT INTO files (id, path, name, mtime_ms, size_bytes, is_markdown)
            VALUES (1, 'a.md', 'a.md', 0, 0, 1);
            INSERT INTO properties (file_id, ordinal, key, value_kind, value_text) VALUES
                (1, 0, 'published', 'boolean', 'true'),
                (1, 1, 'draft', 'boolean', 'false'),
                (1, 2, 'title', 'text', '\"Hello\"'),
                (1, 3, 'count', 'number', '42');",
        )
        .unwrap();

        let migration_sql = include_str!("../migrations/007_properties_value_norm.sql");
        conn.execute_batch(migration_sql).unwrap();

        let row = |key: &str| -> String {
            conn.query_row(
                "SELECT value_text_norm FROM properties WHERE key = ?1",
                rusqlite::params![key],
                |r| r.get::<_, String>(0),
            )
            .unwrap()
        };
        assert_eq!(row("published"), "true");
        assert_eq!(row("draft"), "false");
        // Non-boolean atomic kinds keep going through the
        // json_extract path: text rows shed their quote wrap, number
        // rows pass through.
        assert_eq!(row("title"), "hello");
        assert_eq!(row("count"), "42");
    }
}
