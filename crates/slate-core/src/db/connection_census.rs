// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1917: every connection core opens is configured in one place, so two
//! things are held over the source of the three crates.
//!
//! **Product code opens a SQLite connection only in `db.rs`.** A connection
//! opened anywhere else would run without the query planner stability
//! guarantee, and its cached statements would be compiled again on every
//! use, as all of core's were before #1917.
//!
//! **No statement binds a `GLOB` pattern, and nothing switches `LIKE` to
//! case-sensitive.** Under the guarantee the planner does not look at bound
//! values, and the one thing that costs is an index prefix search for a
//! bound pattern: `col GLOB ?` on an indexed column scans instead. A `LIKE`
//! could take such a search only if it were case-sensitive (a pragma this
//! census refuses) or its column's index were NOCASE (which
//! `db::tests::no_like_can_use_an_index_prefix` refuses, by asking SQLite).
//!
//! Its ceiling. It reads lines, not statements: it sees the spelling
//! `Connection::open…(` and the upper-case word `GLOB`, as every SQL keyword
//! in this codebase is written. A connection reached through an alias of the
//! type, or a pattern operator assembled from pieces of a word, is not seen.
//! Because it reads lines, a `GLOB` in a literal, in a `format!` template or
//! in one clause of SQL assembled at run time is seen alike.

use std::path::{Path, PathBuf};

/// The source trees that hold product code, relative to `crates/`.
const PRODUCT_TREES: [&str; 3] = ["slate-core/src", "slate-cli/src", "slate-uniffi/src"];

/// This census spells what it looks for.
const THIS_FILE: &str = "slate-core/src/db/connection_census.rs";

/// The only file, and the only functions in it, that open a connection.
const WHERE_CONNECTIONS_OPEN: &str = "slate-core/src/db.rs";
const FUNCTIONS_THAT_OPEN: [&str; 2] = ["open_unconfigured", "open_in_memory"];

fn crates_root() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("..")
}

/// Every file beneath `dir` with the extension `extension`, sorted.
fn sources(dir: &Path, extension: &str) -> Vec<PathBuf> {
    let mut found = Vec::new();
    let mut pending = vec![dir.to_path_buf()];
    while let Some(next) = pending.pop() {
        let Ok(entries) = std::fs::read_dir(&next) else {
            continue;
        };
        for entry in entries {
            let path = entry.unwrap().path();
            if path.is_dir() {
                pending.push(path);
            } else if path.extension().is_some_and(|found| found == extension) {
                found.push(path);
            }
        }
    }
    found.sort();
    found
}

/// `path` relative to `base`, with forward slashes.
fn relative(path: &Path, base: &Path) -> String {
    path.strip_prefix(base)
        .unwrap_or(path)
        .to_string_lossy()
        .replace('\\', "/")
}

/// Whether a file is test code as a whole.
fn is_a_test_file(name: &str) -> bool {
    let leaf = name.rsplit('/').next().unwrap_or(name);
    name.contains("/tests/")
        || leaf == "tests.rs"
        || leaf.ends_with("_tests.rs")
        || leaf.starts_with("test_")
}

/// A product code line: its 1-based number, its text, and the function it
/// sits in.
#[derive(Debug, PartialEq, Eq)]
struct ProductLine<'a> {
    number: usize,
    text: &'a str,
    function: Option<String>,
}

/// The lines of `source` that are product code. Comment lines are dropped,
/// so prose about a name is not a use of it. Each top-level inline test
/// module (`#[cfg(test)]` then `mod … {`, down to its closing brace in the
/// first column) is skipped. An out-of-line `#[cfg(test)] mod tests;` skips
/// nothing: product code may follow it.
fn product_lines(source: &str) -> Vec<ProductLine<'_>> {
    let function = regex::Regex::new(
        r"^\s*(?:pub(?:\([a-z]+\))?\s+)?(?:(?:const|async|unsafe)\s+)*fn\s+(\w+)",
    )
    .unwrap();
    let inline_module = regex::Regex::new(r"^(?:pub(?:\([a-z]+\))?\s+)?mod\s+\w+\s*\{").unwrap();
    let lines: Vec<&str> = source.lines().collect();
    let mut product = Vec::new();
    let mut enclosing: Option<String> = None;
    let mut index = 0;
    while index < lines.len() {
        let line = lines[index];
        if line == "#[cfg(test)]" {
            // The attributes of the item, then the item.
            let mut item = index + 1;
            while item < lines.len() && lines[item].starts_with("#[") {
                item += 1;
            }
            if item < lines.len() && inline_module.is_match(lines[item]) {
                index = item + 1;
                while index < lines.len() && lines[index] != "}" {
                    index += 1;
                }
                index += 1;
                continue;
            }
        }
        if let Some(found) = function.captures(line) {
            enclosing = Some(found[1].to_string());
        }
        if !line.trim_start().starts_with("//") {
            product.push(ProductLine {
                number: index + 1,
                text: line,
                function: enclosing.clone(),
            });
        }
        index += 1;
    }
    product
}

/// Every place `source` opens a SQLite connection itself: the line and the
/// function it is in.
fn connection_opens(source: &str) -> Vec<(usize, Option<String>)> {
    let opens = regex::Regex::new(r"Connection::(?:open\w*|from_handle\w*)\s*\(").unwrap();
    product_lines(source)
        .into_iter()
        .filter(|line| opens.is_match(line.text))
        .map(|line| (line.number, line.function))
        .collect()
}

/// Whether an open at `function` in the file `name` is one of the allowed
/// ones.
fn may_open(name: &str, function: Option<&str>) -> bool {
    name == WHERE_CONNECTIONS_OPEN
        && function.is_some_and(|function| FUNCTIONS_THAT_OPEN.contains(&function))
}

/// The lines among `lines` that bind a `GLOB` pattern: the word `GLOB` not
/// followed by a quoted literal pattern.
fn glob_with_a_bound_pattern<'a>(lines: impl Iterator<Item = (usize, &'a str)>) -> Vec<usize> {
    let glob = regex::Regex::new(r"\bGLOB\b\s*(.?)").unwrap();
    lines
        .filter(|(_, text)| glob.captures_iter(text).any(|found| &found[1] != "'"))
        .map(|(number, _)| number)
        .collect()
}

/// The lines among `lines` that name the pragma that makes `LIKE`
/// case-sensitive.
fn names_case_sensitive_like<'a>(lines: impl Iterator<Item = (usize, &'a str)>) -> Vec<usize> {
    lines
        .filter(|(_, text)| text.to_ascii_lowercase().contains("case_sensitive_like"))
        .map(|(number, _)| number)
        .collect()
}

/// The code lines of a migration: SQL comment lines dropped.
fn migration_lines(source: &str) -> impl Iterator<Item = (usize, &str)> {
    source
        .lines()
        .enumerate()
        .filter(|(_, line)| !line.trim_start().starts_with("--"))
        .map(|(index, line)| (index + 1, line))
}

/// Every product source of the three crates, as `(name, text)`.
fn product_sources() -> Vec<(String, String)> {
    let crates = crates_root();
    let mut found = Vec::new();
    for tree in PRODUCT_TREES {
        for file in sources(&crates.join(tree), "rs") {
            let name = relative(&file, &crates);
            if is_a_test_file(&name) || name == THIS_FILE {
                continue;
            }
            found.push((name, std::fs::read_to_string(&file).unwrap()));
        }
    }
    assert!(
        found.len() > 100,
        "the walk must see the three crates: {} sources",
        found.len()
    );
    found
}

#[test]
fn product_code_opens_a_connection_only_through_db() {
    let mut allowed = Vec::new();
    let mut strays = Vec::new();
    for (name, source) in product_sources() {
        for (line, function) in connection_opens(&source) {
            if may_open(&name, function.as_deref()) {
                allowed.push(format!("{name}:{line}"));
            } else {
                strays.push(format!(
                    "{name}:{line} in {}",
                    function.as_deref().unwrap_or("no function")
                ));
            }
        }
    }
    assert!(
        strays.is_empty(),
        "product code opens a SQLite connection outside {FUNCTIONS_THAT_OPEN:?} in \
         {WHERE_CONNECTIONS_OPEN}; open it with db::open_connection, which configures it \
         (#1917):\n  {}",
        strays.join("\n  ")
    );
    assert_eq!(
        allowed.len(),
        3,
        "the two branches of open_unconfigured and open_in_memory: {allowed:?}"
    );
}

#[test]
fn the_connection_census_sees_a_planted_open() {
    let planted = "\
pub fn open_unconfigured(path: &Path) -> rusqlite::Result<Connection> {
    Connection::open(path)
}

fn product(path: &Path) {
    // Connection::open(path) in a comment is prose.
    let other = rusqlite::Connection::open_with_flags(path, flags).unwrap();
}

#[cfg(test)]
mod tests;

fn after_an_out_of_line_test_module() {
    let raw = unsafe { Connection::from_handle(handle) };
}

#[cfg(test)]
mod inline_tests {
    fn a_test() {
        let raw = Connection::open_in_memory().unwrap();
    }
}

fn after_an_inline_test_module() {
    let memory = Connection::open_in_memory();
}
";
    let opens = connection_opens(planted);
    assert_eq!(
        opens,
        vec![
            (2, Some("open_unconfigured".to_string())),
            (7, Some("product".to_string())),
            (14, Some("after_an_out_of_line_test_module".to_string())),
            (25, Some("after_an_inline_test_module".to_string())),
        ]
    );
    // The allowed function's name is not enough: the file has to be db.rs.
    assert!(may_open(WHERE_CONNECTIONS_OPEN, Some("open_unconfigured")));
    assert!(may_open(WHERE_CONNECTIONS_OPEN, Some("open_in_memory")));
    assert!(!may_open(
        "slate-core/src/session.rs",
        Some("open_unconfigured")
    ));
    assert!(!may_open(WHERE_CONNECTIONS_OPEN, Some("open_connection")));
    assert!(!may_open(WHERE_CONNECTIONS_OPEN, None));

    assert!(is_a_test_file("slate-core/src/session/tests/scan.rs"));
    assert!(is_a_test_file("slate-core/src/canvas/model_tests.rs"));
    assert!(is_a_test_file("slate-core/src/vault/test_trash_census.rs"));
    assert!(!is_a_test_file("slate-core/src/db.rs"));
}

#[test]
fn no_product_statement_binds_a_glob_pattern() {
    let crates = crates_root();
    let mut found = Vec::new();
    for (name, source) in product_sources() {
        let lines = product_lines(&source);
        let numbered = || lines.iter().map(|line| (line.number, line.text));
        for line in glob_with_a_bound_pattern(numbered()) {
            found.push(format!(
                "{name}:{line}: GLOB with a pattern that is not a literal"
            ));
        }
        for line in names_case_sensitive_like(numbered()) {
            found.push(format!("{name}:{line}: case_sensitive_like"));
        }
    }
    let migrations = sources(&crates.join("slate-core/migrations"), "sql");
    assert!(
        migrations.len() >= 42,
        "the walk must see the migrations: {}",
        migrations.len()
    );
    for file in migrations {
        let name = relative(&file, &crates);
        let source = std::fs::read_to_string(&file).unwrap();
        for line in glob_with_a_bound_pattern(migration_lines(&source)) {
            found.push(format!(
                "{name}:{line}: GLOB with a pattern that is not a literal"
            ));
        }
        for line in names_case_sensitive_like(migration_lines(&source)) {
            found.push(format!("{name}:{line}: case_sensitive_like"));
        }
    }
    assert!(
        found.is_empty(),
        "core's connections run under the query planner stability guarantee (#1917), which \
         gives up the index prefix search for a bound pattern: a bound GLOB on an indexed \
         column scans, and so would a LIKE made case-sensitive. Bind a range (`col >= ? AND \
         col < ?`) instead:\n  {}",
        found.join("\n  ")
    );
}

#[test]
fn the_pattern_census_sees_each_planted_shape() {
    let planted = "\
        \"SELECT path FROM files WHERE path GLOB ?1\",
        \"SELECT path FROM files WHERE path GLOB 'n*'\",
        // WHERE path GLOB ?1, in a comment
        format!(\"SELECT path FROM {table} WHERE path GLOB\"),
        \"WHERE name GLOB :pattern AND path NOT GLOB   'x*'\",
        conn.pragma_update(None, \"case_sensitive_like\", true)?;
        let glob_patterns = GLOBAL_LIMIT;
";
    let lines = product_lines(planted);
    let numbered = || lines.iter().map(|line| (line.number, line.text));
    assert_eq!(glob_with_a_bound_pattern(numbered()), vec![1, 4, 5]);
    assert_eq!(names_case_sensitive_like(numbered()), vec![6]);

    let migration = "\
-- a GLOB ?1 in a comment
CREATE INDEX i ON t(x) WHERE x GLOB 'a*';
SELECT 1 WHERE x GLOB ?1;
PRAGMA case_sensitive_like = ON;
";
    assert_eq!(
        glob_with_a_bound_pattern(migration_lines(migration)),
        vec![3]
    );
    assert_eq!(
        names_case_sensitive_like(migration_lines(migration)),
        vec![4]
    );
}
