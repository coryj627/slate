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
    let (props, _warnings) = extract_frontmatter(markdown_source);
    if props.is_empty() {
        return Ok(());
    }
    let mut stmt = tx.prepare_cached(
        "INSERT INTO properties (file_id, ordinal, key, value_kind, value_text)
         VALUES (?1, ?2, ?3, ?4, ?5)",
    )?;
    for (ordinal, prop) in props.into_iter().enumerate() {
        let (kind, value_text) = serialize_value(&prop.value);
        stmt.execute(params![file_id, ordinal as i64, prop.key, kind, value_text,])?;
    }
    Ok(())
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
pub(crate) fn files_with_property(
    conn: &Connection,
    key: &str,
    value: &str,
    paging: Paging,
) -> Result<Page<FileSummary>, VaultError> {
    let limit = paging.limit.clamp(1, 1000);
    let after_path = paging.cursor.clone();
    let target = value.to_lowercase();

    // The same file can match multiple times if it has more than one
    // matching list element under the same key. DISTINCT keeps the
    // result set deduped at the file level so paging stays
    // predictable.
    let sql = "SELECT DISTINCT files.path, files.name, files.mtime_ms,
                              files.size_bytes, files.is_markdown
               FROM files
               JOIN properties p ON p.file_id = files.id
               LEFT JOIN json_each(p.value_text) elem
                 ON p.value_kind IN ('list', 'tag_list')
               WHERE p.key = ?1
                 AND (
                     (p.value_kind NOT IN ('list', 'tag_list')
                      AND lower(IFNULL(json_extract(p.value_text, '$'), p.value_text)) = ?2)
                     OR (p.value_kind IN ('list', 'tag_list')
                         AND lower(elem.value) = ?2)
                 )
                 AND (?3 IS NULL OR files.path > ?3)
               ORDER BY files.path ASC
               LIMIT ?4";
    let mut stmt = conn.prepare_cached(sql)?;
    let rows: Vec<FileSummary> = stmt
        .query_map(params![key, target, after_path, limit as i64 + 1], |row| {
            Ok(FileSummary {
                path: row.get::<_, String>(0)?,
                name: row.get::<_, String>(1)?,
                mtime_ms: row.get::<_, i64>(2)?,
                size_bytes: row.get::<_, i64>(3)? as u64,
                is_markdown: row.get::<_, i64>(4)? != 0,
            })
        })?
        .collect::<Result<Vec<_>, _>>()?;

    // Total-filtered is the same DISTINCT count without pagination.
    let total_filtered: u64 = conn.query_row(
        "SELECT COUNT(DISTINCT files.id)
         FROM files
         JOIN properties p ON p.file_id = files.id
         LEFT JOIN json_each(p.value_text) elem
           ON p.value_kind IN ('list', 'tag_list')
         WHERE p.key = ?1
           AND (
               (p.value_kind NOT IN ('list', 'tag_list')
                AND lower(IFNULL(json_extract(p.value_text, '$'), p.value_text)) = ?2)
               OR (p.value_kind IN ('list', 'tag_list')
                   AND lower(elem.value) = ?2)
           )",
        params![key, target],
        |row| row.get::<_, i64>(0),
    )? as u64;

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
}
