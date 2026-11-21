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
//! values (`configure_connection` in `db.rs` lists the five uses it gives
//! up). One of them is an index prefix search for a bound pattern:
//! `col GLOB ?` on an indexed column scans instead. A `LIKE`
//! could take such a search only if it were case-sensitive (a pragma this
//! census refuses) or its column's index were NOCASE (which
//! `db::tests::no_like_can_use_an_index_prefix` refuses, by asking SQLite).
//!
//! **The open has one exit, through the configuration.** Only macOS takes
//! the anchored branch of the open, and no CI lane runs these tests there,
//! so a configuration moved into one branch would be seen by no run of
//! `every_connection_core_opens_is_configured`. The type in `db.rs` makes
//! such an open fail to compile, and this census holds the shape as text.
//!
//! **No statement binds a value where a partial index's condition has a
//! literal.** That is the second of the five, and the other one a statement
//! could lean on unnoticed: `is_external = ?1` no longer proves
//! `is_external = 0`, so the statement loses the index and scans, with the
//! same rows. The census reads the conditions from the migrated schema and
//! refuses a product line that compares one of their columns with something
//! bound. A condition that needs no value (`IS NULL`, `IS NOT NULL`) is not
//! one of them. An index on an expression is matched the same way and is
//! not guarded: a statement would have to bind a value where the indexed
//! expression holds a literal.
//!
//! Its ceiling. It reads lines, not statements: it sees the spelling
//! `Connection::open…` (called, or passed as a value) and the word `GLOB`
//! in either case, as the operator or as the function `glob(pattern, text)`.
//! A connection reached through an alias of the type, or a pattern operator
//! assembled from pieces of a word, is not seen. Because it reads lines, a
//! `GLOB` in a literal, in a `format!` template or in one clause of SQL
//! assembled at run time is seen alike. The partial-index rule sees a
//! column's name beside an operator and a parameter (or, in a list, a
//! `format!` placeholder) on one line; a column name put in at run time is
//! not seen, and an assignment written on a line of its own, away from its
//! `SET`, reads as a comparison and is refused.

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
/// first column, or that one line when the module closes on it) is skipped.
/// An out-of-line `#[cfg(test)] mod tests;` skips nothing: product code may
/// follow it.
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
                // `mod tests {}` closes on its own line: nothing follows it
                // down to a brace in the first column.
                let closes_on_its_line =
                    lines[item].matches('{').count() == lines[item].matches('}').count();
                if !closes_on_its_line {
                    while index < lines.len() && lines[index] != "}" {
                        index += 1;
                    }
                    index += 1;
                }
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

/// Every place `source` opens a SQLite connection itself, or names the
/// function that would (`.map(Connection::open)` opens one too): the line
/// and the function it is in.
fn connection_opens(source: &str) -> Vec<(usize, Option<String>)> {
    let opens = regex::Regex::new(r"Connection::(?:open|from_handle)\w*").unwrap();
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

/// What is wrong with the way `source` (`db.rs`) configures the connections
/// it opens, one line for each break. The shape it holds:
/// `open_unconfigured` has one caller, `open_connection`; and
/// `configure_connection` is called by `open_connection` and
/// `open_in_memory`, and by nothing else, so never inside one branch of the
/// open.
///
/// It reads text, so it holds on every lane. That matters because only
/// macOS takes the anchored branch of the open, and no CI lane runs core's
/// tests there: a configuration moved into the other branch would pass
/// `every_connection_core_opens_is_configured` everywhere it runs.
fn breaks_of_the_one_exit(source: &str) -> Vec<String> {
    const CONFIGURE: [&str; 2] = ["open_connection", "open_in_memory"];
    let lines = product_lines(source);
    let calls = |callee: &str| -> Vec<(usize, Option<String>)> {
        let call = regex::Regex::new(&format!(r"\b{callee}\s*\(")).unwrap();
        let definition = regex::Regex::new(&format!(r"\bfn\s+{callee}\b")).unwrap();
        lines
            .iter()
            .filter(|line| call.is_match(line.text) && !definition.is_match(line.text))
            .map(|line| (line.number, line.function.clone()))
            .collect()
    };
    let mut breaks = Vec::new();
    let opened = calls("open_unconfigured");
    if !matches!(opened.as_slice(), [(_, Some(caller))] if caller == "open_connection") {
        breaks.push(format!(
            "open_unconfigured has one caller, open_connection, and here its callers are {opened:?}"
        ));
    }
    let configured = calls("configure_connection");
    for (line, caller) in &configured {
        let caller = caller.as_deref().unwrap_or("no function");
        if !CONFIGURE.contains(&caller) {
            breaks.push(format!(
                "line {line}: {caller} calls configure_connection; only {CONFIGURE:?} do, so no \
                 branch of the open configures for itself"
            ));
        }
    }
    for must in CONFIGURE {
        if !configured
            .iter()
            .any(|(_, caller)| caller.as_deref() == Some(must))
        {
            breaks.push(format!("{must} does not call configure_connection"));
        }
    }
    breaks
}

/// The lines among `lines` that bind a `GLOB` pattern: the word `GLOB`, in
/// either case, whose pattern is not a quoted literal. The pattern follows
/// the operator (`path GLOB ?1`) and is the first argument of the function
/// (`glob(?1, path)`); SQLite plans the two alike.
fn glob_with_a_bound_pattern<'a>(lines: impl Iterator<Item = (usize, &'a str)>) -> Vec<usize> {
    let glob = regex::Regex::new(r"(?i)\bglob\b\s*(?:\(\s*)?(.?)").unwrap();
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

/// How many partial indexes `conn`'s schema has, and the columns their
/// conditions compare with a literal (a quoted string or a number), sorted.
/// SQLite reports each index's SQL; `IS NULL` and `IS NOT NULL` name no
/// literal and so no column here.
fn columns_partial_indexes_compare_with_a_literal(
    conn: &rusqlite::Connection,
) -> (usize, Vec<String>) {
    let condition = regex::Regex::new(r"(?is)\bWHERE\b(.*)$").unwrap();
    let compared = regex::Regex::new(
        r"(?i)\b(\w+)\s*(?:=|==|!=|<>|IS(?:\s+NOT)?|(?:NOT\s+)?IN)\s*\(?\s*(?:'|\d)",
    )
    .unwrap();
    let indexes: Vec<String> = conn
        .prepare("SELECT sql FROM sqlite_master WHERE type = 'index' AND sql IS NOT NULL")
        .unwrap()
        .query_map([], |row| row.get(0))
        .unwrap()
        .collect::<Result<_, _>>()
        .unwrap();
    let mut partial = 0;
    let mut columns = std::collections::BTreeSet::new();
    for sql in indexes {
        let Some(found) = condition.captures(&sql) else {
            continue;
        };
        partial += 1;
        for column in compared.captures_iter(&found[1]) {
            columns.insert(column[1].to_ascii_lowercase());
        }
    }
    (partial, columns.into_iter().collect())
}

/// The lines among `lines` that compare `column` with something bound: a
/// parameter on either side of the operator, or a list that opens with a
/// parameter or a `format!` placeholder.
fn binds_a_partial_index_condition<'a>(
    column: &str,
    lines: impl Iterator<Item = (usize, &'a str)>,
) -> Vec<usize> {
    const BOUND: &str = r"(?:\?|:\w|@\w|\$\w)";
    let on_the_right = regex::Regex::new(&format!(
        r"(?i)\b{column}\b\s*(?:=|==|!=|<>|IS(?:\s+NOT)?|(?:NOT\s+)?IN)\s*(?:\(\s*(?:{BOUND}|\{{)|{BOUND})"
    ))
    .unwrap();
    let on_the_left = regex::Regex::new(&format!(
        r"(?i)(?:\?\d*|[:@$]\w+)\s*(?:=|==|!=|<>|IS(?:\s+NOT)?)\s*(?:\w+\.)?{column}\b"
    ))
    .unwrap();
    lines
        .filter(|(_, text)| {
            on_the_left.is_match(text)
                || on_the_right.find_iter(text).any(|found| {
                    // `SET column = ?` assigns; it compares nothing.
                    let before = &text[..found.start()];
                    let assigns = before
                        .rfind("SET ")
                        .is_some_and(|set| before.rfind("WHERE").is_none_or(|filter| filter < set));
                    !assigns
                })
        })
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
fn the_open_has_one_exit_through_configure() {
    let source = std::fs::read_to_string(crates_root().join(WHERE_CONNECTIONS_OPEN)).unwrap();
    let breaks = breaks_of_the_one_exit(&source);
    assert!(
        breaks.is_empty(),
        "every connection core opens is configured at the one exit of db::open_connection \
         (#1917). Only macOS takes the anchored branch and no lane runs these tests there, so \
         a configuration inside one branch would go unseen:\n  {}",
        breaks.join("\n  ")
    );
}

#[test]
fn the_one_exit_census_sees_each_planted_shape() {
    // The shape before the type, and the shape with it: both are one exit.
    let by_reference = "\
pub(crate) fn open_connection(path: &Path) -> rusqlite::Result<Connection> {
    let conn = open_unconfigured(path)?;
    configure_connection(&conn)?;
    Ok(conn)
}
// configure_connection(&conn) in a comment is prose.
fn configure_connection(conn: &Connection) -> rusqlite::Result<()> {
    Ok(())
}
fn open_unconfigured(path: &Path) -> rusqlite::Result<Connection> {
    if anchored {
        return Connection::open_with_flags_and_vfs(path, flags, vfs);
    }
    Connection::open(path)
}
pub fn open_in_memory() -> Result<Connection, DbError> {
    let conn = Connection::open_in_memory()?;
    configure_connection(&conn)?;
    Ok(conn)
}
";
    assert_eq!(breaks_of_the_one_exit(by_reference), Vec::<String>::new());
    let by_type = "\
pub(crate) fn open_connection(path: &Path) -> rusqlite::Result<Connection> {
    configure_connection(open_unconfigured(path)?)
}
mod configured {
    pub(super) fn configure_connection(conn: Unconfigured) -> rusqlite::Result<Connection> {
        Ok(conn.0)
    }
}
fn open_unconfigured(path: &Path) -> rusqlite::Result<Unconfigured> {
    Connection::open(path).map(Unconfigured::opened)
}
pub fn open_in_memory() -> Result<Connection, DbError> {
    let conn = configure_connection(Unconfigured::opened(Connection::open_in_memory()?))?;
    Ok(conn)
}
";
    assert_eq!(breaks_of_the_one_exit(by_type), Vec::<String>::new());

    // The configuration moved into one branch of the open.
    let in_one_branch = by_reference
        .replace(
            "    let conn = open_unconfigured(path)?;\n    configure_connection(&conn)?;\n",
            "    let conn = open_unconfigured(path)?;\n",
        )
        .replace(
            "    Connection::open(path)\n}",
            "    let conn = Connection::open(path)?;\n    configure_connection(&conn)?;\n    Ok(conn)\n}",
        );
    assert_eq!(
        breaks_of_the_one_exit(&in_one_branch),
        vec![
            "line 14: open_unconfigured calls configure_connection; only [\"open_connection\", \
             \"open_in_memory\"] do, so no branch of the open configures for itself"
                .to_string(),
            "open_connection does not call configure_connection".to_string(),
        ]
    );

    // A second way to an unconfigured connection.
    let a_second_caller = format!(
        "{by_reference}fn open_reader(path: &Path) -> rusqlite::Result<Connection> {{\n    \
         open_unconfigured(path)\n}}\n"
    );
    let breaks = breaks_of_the_one_exit(&a_second_caller);
    assert_eq!(breaks.len(), 1, "{breaks:?}");
    assert!(breaks[0].contains("open_reader"), "{breaks:?}");

    // The in-memory open left as SQLite gives it.
    let in_memory_bare = by_reference.replace(
        "    let conn = Connection::open_in_memory()?;\n    configure_connection(&conn)?;\n",
        "    let conn = Connection::open_in_memory()?;\n",
    );
    assert_eq!(
        breaks_of_the_one_exit(&in_memory_bare),
        vec!["open_in_memory does not call configure_connection".to_string()]
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

#[cfg(test)]
mod one_line_tests {}

fn after_a_one_line_test_module() {
    let each = paths.iter().map(rusqlite::Connection::open);
}

fn passes_the_open_as_a_value() {
    let opener = Connection::open;
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
            (32, Some("after_a_one_line_test_module".to_string())),
            (36, Some("passes_the_open_as_a_value".to_string())),
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

/// The columns the schema's partial indexes compare with a literal today:
/// `is_external = 0` (the two link fold indexes) and `value_kind NOT IN
/// ('list', 'tag_list')` (`idx_properties_key_norm`). A new partial index
/// with a literal in its condition moves this list, and its author reads
/// why.
const COLUMNS_A_PARTIAL_INDEX_HOLDS_TO_A_LITERAL: [&str; 2] = ["is_external", "value_kind"];

#[test]
fn no_product_statement_binds_a_partial_index_condition() {
    let mut conn = super::open_in_memory(64).unwrap();
    super::migrate(&mut conn).unwrap();
    let (partial, columns) = columns_partial_indexes_compare_with_a_literal(&conn);
    assert!(
        partial >= 5,
        "the walk must see the schema's partial indexes: {partial}"
    );
    assert_eq!(
        columns, COLUMNS_A_PARTIAL_INDEX_HOLDS_TO_A_LITERAL,
        "the columns a partial index's condition compares with a literal"
    );

    let crates = crates_root();
    let mut found = Vec::new();
    for (name, source) in product_sources() {
        let lines = product_lines(&source);
        for column in &columns {
            let numbered = lines.iter().map(|line| (line.number, line.text));
            for line in binds_a_partial_index_condition(column, numbered) {
                found.push(format!(
                    "{name}:{line}: {column} compared with a bound value"
                ));
            }
        }
    }
    for file in sources(&crates.join("slate-core/migrations"), "sql") {
        let name = relative(&file, &crates);
        let source = std::fs::read_to_string(&file).unwrap();
        for column in &columns {
            for line in binds_a_partial_index_condition(column, migration_lines(&source)) {
                found.push(format!(
                    "{name}:{line}: {column} compared with a bound value"
                ));
            }
        }
    }
    assert!(
        found.is_empty(),
        "core's connections run under the query planner stability guarantee (#1917), which \
         does not use a bound value to prove a partial index's condition: a statement that \
         binds the value loses the index and scans. Write the literal the index's condition \
         has:\n  {}",
        found.join("\n  ")
    );
}

#[test]
fn the_partial_index_census_sees_each_planted_shape() {
    let planted = "\
        \"WHERE links.target_path IS NULL AND links.is_external = ?2\",
        \"WHERE l.is_external = 0 AND l.target_path = ?1\",
        \"AND p.value_kind NOT IN (?1, ?2)\",
        \"AND p.value_kind NOT IN ('list', 'tag_list')\",
        \"UPDATE links SET is_external = ?3 WHERE rowid = ?1\",
        \"UPDATE links SET target_raw = ?2 WHERE is_external = ?3\",
        let is_external = looks_external(&url_string);
        \"WHERE ?2 = is_external\",
        format!(\"AND value_kind IN ({placeholders})\"),
        \"AND value_kind = :kind\",
        // WHERE is_external = ?2, in a comment
        \"WHERE is_external IS ?1 OR value_kind == @kind\",
        \"ON CONFLICT DO UPDATE SET value_kind = ?4\",
";
    let lines = product_lines(planted);
    let numbered = || lines.iter().map(|line| (line.number, line.text));
    assert_eq!(
        binds_a_partial_index_condition("is_external", numbered()),
        vec![1, 6, 8, 12]
    );
    assert_eq!(
        binds_a_partial_index_condition("value_kind", numbered()),
        vec![3, 9, 10, 12]
    );

    // The schema walk: a literal names its column, a NULL test names none.
    let scratch = rusqlite::Connection::open_in_memory().unwrap();
    scratch
        .execute_batch(
            "CREATE TABLE t (a, b, c, d);
             CREATE INDEX plain ON t(a);
             CREATE INDEX not_null ON t(a) WHERE b IS NOT NULL;
             CREATE INDEX by_number ON t(a) WHERE c = 0 AND b IS NULL;
             CREATE INDEX by_list ON t(a)
             WHERE d NOT IN ('x', 'y');",
        )
        .unwrap();
    assert_eq!(
        columns_partial_indexes_compare_with_a_literal(&scratch),
        (3, vec!["c".to_string(), "d".to_string()])
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
        \"SELECT path FROM files WHERE glob(?1, path)\",
        \"SELECT path FROM files WHERE path glob ?1\",
        \"SELECT path FROM files WHERE glob('n*', path) AND path glob 'n*'\",
        \"WHERE path Glob :pattern\",
        \"WHERE GLOB ( ?2 , name )\",
";
    let lines = product_lines(planted);
    let numbered = || lines.iter().map(|line| (line.number, line.text));
    assert_eq!(
        glob_with_a_bound_pattern(numbered()),
        vec![1, 4, 5, 8, 9, 11, 12]
    );
    assert_eq!(names_case_sensitive_like(numbered()), vec![6]);

    let migration = "\
-- a GLOB ?1 in a comment
CREATE INDEX i ON t(x) WHERE x GLOB 'a*';
SELECT 1 WHERE x GLOB ?1;
PRAGMA case_sensitive_like = ON;
SELECT 1 WHERE glob(?1, x);
SELECT 1 WHERE x glob 'a*';
";
    assert_eq!(
        glob_with_a_bound_pattern(migration_lines(migration)),
        vec![3, 5]
    );
    assert_eq!(
        names_case_sensitive_like(migration_lines(migration)),
        vec![4]
    );
}
