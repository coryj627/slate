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
//! same rows. The census asks SQLite which indexes are partial, reads each
//! one's condition in full, and fails on a condition it cannot read: no
//! partial index is passed over. It then refuses a product line that
//! compares a column with something bound where a condition compares that
//! column with a literal. By SQLite's rules a parameter stands in for a
//! literal only as one side of a binary operator, so a column held in a
//! list (`IN`, `BETWEEN`) or tested for NULL names nothing to refuse; SQLite
//! is asked for each shape in
//! `the_condition_reader_agrees_with_sqlite_on_each_planted_condition`. An
//! index on an expression is matched the same way and is not guarded: a
//! statement would have to bind a value where the indexed expression holds
//! a literal.
//!
//! Its ceiling. It reads lines, not statements: it sees the spelling
//! `Connection::open…` (called, or passed as a value) and the word `GLOB`
//! in either case, as the operator or as the function `glob(pattern, text)`.
//! A connection reached through an alias of the type, or a pattern operator
//! assembled from pieces of a word, is not seen. Because it reads lines, a
//! `GLOB` in a literal, in a `format!` template or in one clause of SQL
//! assembled at run time is seen alike. The partial-index rule sees a
//! column's name, an operator and a parameter (or, in a list, a `format!`
//! placeholder) on one line. A comparison split over two lines, a column
//! name put in at run time, and a comparison with a `format!` placeholder
//! outside a list are not seen. It reads no table: a column of the same
//! name in another table is refused too. An assignment written on a line of
//! its own, away from its `SET`, reads as a comparison and is refused. A
//! parameter inside an expression (`col = +?1`) is passed, and rightly:
//! SQLite never read such a value.

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

/// One token of a `CREATE INDEX` statement: as much of SQL's lexing as an
/// index's condition needs.
#[derive(Debug, Clone, PartialEq, Eq)]
enum SqlToken {
    /// A bare word: a keyword or an unquoted name.
    Word(String),
    /// A quoted name: `"name"`, `[name]` or `` `name` ``.
    Name(String),
    /// A number, a string or a blob.
    Literal(String),
    /// An operator or a piece of punctuation.
    Mark(String),
}

/// `sql` as tokens, comments dropped. A quote doubled inside a string or a
/// quoted name is one quote.
fn sql_tokens(sql: &str) -> Vec<SqlToken> {
    let chars: Vec<char> = sql.chars().collect();
    let mut tokens = Vec::new();
    let mut at = 0;
    while at < chars.len() {
        let c = chars[at];
        let next = chars.get(at + 1).copied();
        if c.is_whitespace() {
            at += 1;
        } else if c == '-' && next == Some('-') {
            while at < chars.len() && chars[at] != '\n' {
                at += 1;
            }
        } else if c == '/' && next == Some('*') {
            at += 2;
            while at < chars.len() && !(chars[at] == '*' && chars.get(at + 1) == Some(&'/')) {
                at += 1;
            }
            at += 2;
        } else if c == '\'' || c == '"' || c == '`' || c == '[' {
            let close = if c == '[' { ']' } else { c };
            let mut text = String::new();
            at += 1;
            while let Some(&inside) = chars.get(at) {
                at += 1;
                if inside != close {
                    text.push(inside);
                } else if close != ']' && chars.get(at) == Some(&close) {
                    text.push(close);
                    at += 1;
                } else {
                    break;
                }
            }
            tokens.push(if c == '\'' {
                SqlToken::Literal(format!("'{text}'"))
            } else {
                SqlToken::Name(text)
            });
        } else if matches!(c, 'x' | 'X') && next == Some('\'') {
            let start = at;
            at += 2;
            while at < chars.len() && chars[at] != '\'' {
                at += 1;
            }
            at += 1;
            tokens.push(SqlToken::Literal(
                chars[start..at.min(chars.len())].iter().collect(),
            ));
        } else if c.is_ascii_digit() || (c == '.' && next.is_some_and(|n| n.is_ascii_digit())) {
            let start = at;
            let hex = c == '0' && matches!(next, Some('x' | 'X'));
            while at < chars.len() {
                let part = chars[at];
                let exponent_sign = !hex
                    && matches!(part, '+' | '-')
                    && at > start
                    && matches!(chars[at - 1], 'e' | 'E');
                if part.is_ascii_alphanumeric() || part == '.' || part == '_' || exponent_sign {
                    at += 1;
                } else {
                    break;
                }
            }
            tokens.push(SqlToken::Literal(chars[start..at].iter().collect()));
        } else if c.is_alphabetic() || c == '_' {
            let start = at;
            while at < chars.len()
                && (chars[at].is_alphanumeric() || chars[at] == '_' || chars[at] == '$')
            {
                at += 1;
            }
            tokens.push(SqlToken::Word(chars[start..at].iter().collect()));
        } else {
            let two: String = [c, next.unwrap_or(' ')].iter().collect();
            if ["==", "!=", "<>", "<=", ">=", "||", "<<", ">>"].contains(&two.as_str()) {
                tokens.push(SqlToken::Mark(two));
                at += 2;
            } else {
                tokens.push(SqlToken::Mark(c.to_string()));
                at += 1;
            }
        }
    }
    tokens
}

/// The condition of the partial index `sql` creates: the tokens after the
/// `WHERE` that follows the closing parenthesis of its indexed columns.
/// `None` when no `WHERE` stands there.
fn condition_tokens(sql: &str) -> Option<Vec<SqlToken>> {
    let tokens = sql_tokens(sql);
    let is_mark =
        |token: &SqlToken, mark: &str| matches!(token, SqlToken::Mark(found) if found == mark);
    let mut at = tokens.iter().position(|token| is_mark(token, "("))?;
    let mut depth = 0;
    loop {
        let token = tokens.get(at)?;
        if is_mark(token, "(") {
            depth += 1;
        } else if is_mark(token, ")") {
            depth -= 1;
            if depth == 0 {
                break;
            }
        }
        at += 1;
    }
    match tokens.get(at + 1) {
        Some(SqlToken::Word(word)) if word.eq_ignore_ascii_case("WHERE") => {
            Some(tokens[at + 2..].to_vec())
        }
        _ => None,
    }
}

/// How a partial index's condition holds one of its columns. It decides
/// whether a bound value could ever have proved the condition, by SQLite's
/// own rules (`sqlite3ExprImpliesExpr` and `sqlite3ExprCompare` in
/// `sqlite3.c`): a parameter stands in for a literal only where it is one
/// side of a binary operator. SQLite compares the members of a list (`IN`,
/// `BETWEEN`, a function's arguments) without looking at bound values, and
/// proves `IS NOT NULL` from the shape of a term, whatever is bound.
/// `the_condition_reader_agrees_with_sqlite_on_each_planted_condition` asks
/// SQLite for each.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum Held {
    /// Compared with a literal by a binary operator (`=`, `==`, `!=`, `<>`,
    /// `<`, `<=`, `>`, `>=`, `IS`, `IS NOT`), on either side. A statement
    /// that binds the same value where the literal stands reached the index
    /// before the guarantee and does not under it.
    ToALiteral,
    /// In a list: `IN`, `NOT IN`, `BETWEEN`. No bound value proves it, with
    /// the guarantee or without.
    InAList,
    /// `IS NULL`, `IS NOT NULL`, `ISNULL`, `NOTNULL`, `NOT NULL`. No value
    /// is involved.
    ForNull,
    /// Compared with another column, or standing alone as a truth value.
    WithNoLiteral,
}

impl Held {
    fn in_words(self) -> &'static str {
        match self {
            Held::ToALiteral => "compared with a literal",
            Held::InAList => "in a list",
            Held::ForNull => "tested for NULL",
            Held::WithNoLiteral => "with no literal",
        }
    }
}

enum Operand {
    Column(String),
    Literal,
}

/// Words that cannot be a column where an operand stands. With them, an
/// expression this reader does not know is refused and not taken for a
/// column.
const NOT_AN_OPERAND: [&str; 22] = [
    "AND", "OR", "NOT", "IS", "IN", "BETWEEN", "LIKE", "GLOB", "REGEXP", "MATCH", "CASE", "WHEN",
    "THEN", "ELSE", "END", "CAST", "COLLATE", "EXISTS", "SELECT", "DISTINCT", "FROM", "ESCAPE",
];

/// A reader of the conditions a partial index is written with: columns
/// (bare, or quoted in any of SQLite's three ways) compared with literals,
/// tested for NULL or held in lists, joined by `AND`, `OR`, `NOT` and
/// parentheses. Anything else is an error, and the census fails on it: a
/// function, arithmetic, `LIKE`, `COLLATE`, `CASE`, a qualified name.
struct ConditionReader<'a> {
    tokens: &'a [SqlToken],
    at: usize,
    terms: Vec<(String, Held)>,
}

impl ConditionReader<'_> {
    fn take_word(&mut self, word: &str) -> bool {
        let found = matches!(self.tokens.get(self.at), Some(SqlToken::Word(next)) if next.eq_ignore_ascii_case(word));
        self.at += usize::from(found);
        found
    }

    fn take_mark(&mut self, mark: &str) -> bool {
        let found = matches!(self.tokens.get(self.at), Some(SqlToken::Mark(next)) if next == mark);
        self.at += usize::from(found);
        found
    }

    fn either(&mut self) -> Result<(), String> {
        self.both()?;
        while self.take_word("OR") {
            self.both()?;
        }
        Ok(())
    }

    fn both(&mut self) -> Result<(), String> {
        self.negated()?;
        while self.take_word("AND") {
            self.negated()?;
        }
        Ok(())
    }

    fn negated(&mut self) -> Result<(), String> {
        if self.take_word("NOT") {
            return self.negated();
        }
        self.term()
    }

    fn term(&mut self) -> Result<(), String> {
        if self.take_mark("(") {
            self.either()?;
            return if self.take_mark(")") {
                Ok(())
            } else {
                Err("a parenthesis is not closed".to_string())
            };
        }
        let left = self.operand()?;
        for compares in ["=", "==", "!=", "<>", "<", "<=", ">", ">="] {
            if self.take_mark(compares) {
                let right = self.operand()?;
                return self.compared(left, right);
            }
        }
        if self.take_word("IS") {
            self.take_word("NOT");
            if self.take_word("NULL") {
                return self.held(left, Held::ForNull);
            }
            let right = self.operand()?;
            return self.compared(left, right);
        }
        if self.take_word("ISNULL") || self.take_word("NOTNULL") {
            return self.held(left, Held::ForNull);
        }
        let negated = self.take_word("NOT");
        if negated && self.take_word("NULL") {
            return self.held(left, Held::ForNull);
        }
        if self.take_word("IN") {
            if !self.take_mark("(") {
                return Err("IN without a list".to_string());
            }
            loop {
                if let Operand::Column(column) = self.operand()? {
                    return Err(format!("the column {column} in an IN list"));
                }
                if !self.take_mark(",") {
                    break;
                }
            }
            if !self.take_mark(")") {
                return Err("an IN list is not closed".to_string());
            }
            return self.held(left, Held::InAList);
        }
        if self.take_word("BETWEEN") {
            let low = self.operand()?;
            if !self.take_word("AND") {
                return Err("BETWEEN without its AND".to_string());
            }
            let high = self.operand()?;
            if matches!(low, Operand::Column(_)) || matches!(high, Operand::Column(_)) {
                return Err("a column as a bound of BETWEEN".to_string());
            }
            return self.held(left, Held::InAList);
        }
        if negated {
            return Err("NOT before something that is not NULL, IN or BETWEEN".to_string());
        }
        self.held(left, Held::WithNoLiteral)
    }

    fn operand(&mut self) -> Result<Operand, String> {
        let Some(token) = self.tokens.get(self.at).cloned() else {
            return Err("the condition ends where an operand should be".to_string());
        };
        self.at += 1;
        match token {
            SqlToken::Literal(_) => Ok(Operand::Literal),
            SqlToken::Name(name) => Ok(Operand::Column(name.to_lowercase())),
            SqlToken::Mark(sign) if sign == "+" || sign == "-" => {
                let number = matches!(
                    self.tokens.get(self.at),
                    Some(SqlToken::Literal(text)) if !text.starts_with(['\'', 'x', 'X'])
                );
                if number {
                    self.at += 1;
                    Ok(Operand::Literal)
                } else {
                    Err("a sign before something that is not a number".to_string())
                }
            }
            SqlToken::Mark(mark) => Err(format!("{mark} where an operand should be")),
            SqlToken::Word(word) => {
                let upper = word.to_ascii_uppercase();
                if ["NULL", "TRUE", "FALSE"].contains(&upper.as_str()) {
                    return Ok(Operand::Literal);
                }
                if NOT_AN_OPERAND.contains(&upper.as_str()) {
                    return Err(format!("{word} where an operand should be"));
                }
                match self.tokens.get(self.at) {
                    Some(SqlToken::Mark(mark)) if mark == "(" => {
                        Err(format!("the function {word}"))
                    }
                    Some(SqlToken::Mark(mark)) if mark == "." => {
                        Err(format!("the qualified name {word}."))
                    }
                    _ => Ok(Operand::Column(word.to_lowercase())),
                }
            }
        }
    }

    fn compared(&mut self, left: Operand, right: Operand) -> Result<(), String> {
        match (left, right) {
            (Operand::Column(column), Operand::Literal)
            | (Operand::Literal, Operand::Column(column)) => {
                self.terms.push((column, Held::ToALiteral));
            }
            (Operand::Column(one), Operand::Column(other)) => {
                self.terms.push((one, Held::WithNoLiteral));
                self.terms.push((other, Held::WithNoLiteral));
            }
            (Operand::Literal, Operand::Literal) => {
                return Err("two literals compared".to_string());
            }
        }
        Ok(())
    }

    fn held(&mut self, operand: Operand, held: Held) -> Result<(), String> {
        match operand {
            Operand::Column(column) => {
                self.terms.push((column, held));
                Ok(())
            }
            Operand::Literal => Err("a literal where a column should be".to_string()),
        }
    }
}

/// The columns a condition names, in the order written, and how it holds
/// each.
type Terms = Vec<(String, Held)>;

/// A partial index: its name, and its condition read, or why it could not
/// be.
type PartialIndex = (String, Result<Terms, String>);

/// Every column the condition names and how it holds it, in the order
/// written, or why the condition could not be read.
fn read_condition(tokens: &[SqlToken]) -> Result<Terms, String> {
    let mut reader = ConditionReader {
        tokens,
        at: 0,
        terms: Vec::new(),
    };
    reader.either()?;
    match tokens.get(reader.at) {
        None => Ok(reader.terms),
        Some(left_over) => Err(format!("{left_over:?} is not part of a condition it knows")),
    }
}

/// Every partial index of `conn`'s main schema, by name, with its condition
/// read. SQLite says which indexes are partial (`PRAGMA index_list` marks
/// them); the condition is the `WHERE` after the indexed columns in the
/// index's own SQL. An index SQLite calls partial comes back as an error
/// unless every term of its condition was read: none is passed over.
fn partial_index_conditions(conn: &rusqlite::Connection) -> Vec<PartialIndex> {
    let names: Vec<String> = conn
        .prepare(
            "SELECT l.name FROM sqlite_master t, pragma_index_list(t.name) l
             WHERE t.type = 'table' AND l.\"partial\" = 1 ORDER BY l.name",
        )
        .unwrap()
        .query_map([], |row| row.get(0))
        .unwrap()
        .collect::<Result<_, _>>()
        .unwrap();
    names
        .into_iter()
        .map(|name| {
            let sql: Option<String> = conn
                .query_row(
                    "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = ?1",
                    [&name],
                    |row| row.get(0),
                )
                .unwrap();
            let sql = sql.unwrap_or_default();
            let read = match condition_tokens(&sql) {
                None => Err(format!(
                    "SQLite calls it partial and no WHERE follows its columns, in: {sql}"
                )),
                Some(tokens) => read_condition(&tokens).map_err(|why| format!("{why}, in: {sql}")),
            };
            (name, read)
        })
        .collect()
}

/// The columns whose literal a bound value could stand in for, among the
/// conditions read: sorted, each once.
fn columns_held_to_a_literal(conditions: &[PartialIndex]) -> Vec<String> {
    let columns: std::collections::BTreeSet<String> = conditions
        .iter()
        .filter_map(|(_, read)| read.as_ref().ok())
        .flatten()
        .filter(|(_, held)| *held == Held::ToALiteral)
        .map(|(column, _)| column.clone())
        .collect();
    columns.into_iter().collect()
}

/// The lines among `lines` that compare `column` with something bound: a
/// parameter (`?`, `?1`, `:name`, `@name`, `$name`) on either side of a
/// comparison, `IS` or `IS NOT`; or `IN` and `BETWEEN` with a parameter, or
/// a list that opens with a `format!` placeholder. The column may be quoted
/// and in any case. `SET column = ?` on the `SET`'s own line assigns and is
/// passed.
fn binds_a_partial_index_condition<'a>(
    column: &str,
    lines: impl Iterator<Item = (usize, &'a str)>,
) -> Vec<usize> {
    const BOUND: &str = r"(?:\?|:\w|@\w|\$\w)";
    const COMPARES: &str = r"(?:==|=|!=|<>|<=|>=|<|>|IS(?:\s+NOT)?)";
    let on_the_right = regex::Regex::new(&format!(
        r#"(?i)\b{column}\b\\?["\]`]?\s*(?:{COMPARES}|(?:NOT\s+)?IN|(?:NOT\s+)?BETWEEN)\s*(?:\(\s*(?:{BOUND}|\{{)|{BOUND})"#
    ))
    .unwrap();
    let on_the_left = regex::Regex::new(&format!(
        r#"(?i)(?:^|[^:\w])(?:\?\d*|[:@$]\w+)\s*{COMPARES}\s*(?:\w+\.)?\\?["\[`]?{column}\b"#
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

/// Every partial index in the schema today and how its condition holds its
/// columns. A new partial index, or a changed condition, moves this list,
/// and its author reads why the census cares.
const PARTIAL_INDEX_CONDITIONS: [&str; 5] = [
    "files_oplog_name_unique: oplog_name tested for NULL",
    "idx_links_target_name_fold: target_path tested for NULL, is_external compared with a literal",
    "idx_links_target_path_fold: target_path tested for NULL, is_external compared with a literal",
    "idx_properties_key_norm: value_kind in a list",
    "idx_tasks_priority: priority tested for NULL",
];

/// The columns a bound value could stand in for a literal on: `is_external
/// = 0`, on the two link fold indexes. `value_kind NOT IN ('list',
/// 'tag_list')` is a list, which no bound value proves.
const COLUMNS_HELD_TO_A_LITERAL: [&str; 1] = ["is_external"];

fn in_words(conditions: &[PartialIndex]) -> Vec<String> {
    conditions
        .iter()
        .map(|(name, read)| match read {
            Ok(terms) => format!(
                "{name}: {}",
                terms
                    .iter()
                    .map(|(column, held)| format!("{column} {}", held.in_words()))
                    .collect::<Vec<_>>()
                    .join(", ")
            ),
            Err(why) => format!("{name}: NOT READ: {why}"),
        })
        .collect()
}

#[test]
fn no_product_statement_binds_a_partial_index_condition() {
    let mut conn = super::open_in_memory(64).unwrap();
    super::migrate(&mut conn).unwrap();
    let conditions = partial_index_conditions(&conn);

    // Fail closed: every index SQLite calls partial was read in full.
    let unread: Vec<String> = conditions
        .iter()
        .filter_map(|(name, read)| read.as_ref().err().map(|why| format!("{name}: {why}")))
        .collect();
    assert!(
        unread.is_empty(),
        "a partial index's condition this census cannot read, so it cannot say whether a bound \
         value could have proved it (#1917). Teach `read_condition` the shape, with a planted \
         case SQLite judges in \
         `the_condition_reader_agrees_with_sqlite_on_each_planted_condition`:\n  {}",
        unread.join("\n  ")
    );
    assert_eq!(
        in_words(&conditions),
        PARTIAL_INDEX_CONDITIONS,
        "the schema's partial indexes and how each condition holds its columns"
    );
    let columns = columns_held_to_a_literal(&conditions);
    assert_eq!(
        columns, COLUMNS_HELD_TO_A_LITERAL,
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

/// The plan SQLite picks for `sql` once `value` is bound to `?1`: binding
/// expires a statement whose plan looks at the value, so the step compiles
/// it again with the value in hand.
fn plan_once_bound(
    conn: &rusqlite::Connection,
    sql: &str,
    value: &rusqlite::types::Value,
) -> String {
    let mut explain = conn.prepare(&format!("EXPLAIN QUERY PLAN {sql}")).unwrap();
    explain.raw_bind_parameter(1, value).unwrap();
    let mut rows = explain.raw_query();
    let mut plan = Vec::new();
    while let Some(row) = rows.next().unwrap() {
        plan.push(row.get::<_, String>(3).unwrap());
    }
    plan.join(" / ")
}

/// SQLite is the judge of what the reader says. Each planted condition goes
/// into a partial index on a scratch table, and the same condition with its
/// literal bound goes into a query, planned without the guarantee and with
/// it. Where SQLite uses the index without the guarantee and gives it up
/// with it, the reader must list a column held to a literal: that is the
/// statement the census exists to refuse. Where the reader lists none,
/// SQLite must show the guarantee changed nothing. The reader may hold a
/// column SQLite would not have proved (a literal written on the left);
/// that way round costs only a refusal.
#[test]
fn the_condition_reader_agrees_with_sqlite_on_each_planted_condition() {
    use rusqlite::types::Value;
    let int = Value::Integer;
    let text = |value: &str| Value::Text(value.to_string());
    // (the index's condition, the query's term with ?1 for the literal, the
    // value, whether the bound statement uses the index without the
    // guarantee, the column the reader must hold to a literal)
    let planted: Vec<(&str, &str, Value, bool, &[&str])> = vec![
        ("c = 0", "c = ?1", int(0), true, &["c"]),
        ("c = -1", "c = ?1", int(-1), true, &["c"]),
        ("c = +1", "c = ?1", int(1), true, &["c"]),
        ("c = 1.5", "c = ?1", Value::Real(1.5), true, &["c"]),
        ("c = 0x10", "c = ?1", int(16), true, &["c"]),
        ("c = 'it''s'", "c = ?1", text("it's"), true, &["c"]),
        ("c == 0", "c == ?1", int(0), true, &["c"]),
        ("c IS 0", "c IS ?1", int(0), true, &["c"]),
        ("c IS NOT 0", "c IS NOT ?1", int(0), true, &["c"]),
        ("c != 0", "c != ?1", int(0), true, &["c"]),
        ("c <> 0", "c <> ?1", int(0), true, &["c"]),
        ("c > 5", "c > ?1", int(5), true, &["c"]),
        ("c >= 5", "c >= ?1", int(5), true, &["c"]),
        ("c < 5", "c < ?1", int(5), true, &["c"]),
        ("c <= 5", "c <= ?1", int(5), true, &["c"]),
        ("c = 0 OR d = 1", "c = ?1", int(0), true, &["c", "d"]),
        (
            "b IS NULL AND c = 0",
            "b IS NULL AND c = ?1",
            int(0),
            true,
            &["c"],
        ),
        ("(c = 0)", "c = ?1", int(0), true, &["c"]),
        ("NOT (c = 0)", "NOT (c = ?1)", int(0), true, &["c"]),
        ("\"c\" = 0", "c = ?1", int(0), true, &["c"]),
        ("[c] = 0", "c = ?1", int(0), true, &["c"]),
        ("`c` = 0", "c = ?1", int(0), true, &["c"]),
        ("C = 0", "c = ?1", int(0), true, &["c"]),
        // The parameter on the left of the statement's term: SQLite turns the
        // term round, and the bound value proves the condition all the same.
        ("c = 0", "?1 = c", int(0), true, &["c"]),
        // A literal on the left of the index's condition. SQLite proves
        // neither of these from a bound value, with the guarantee or without;
        // the reader holds the column all the same, since it cannot lose by
        // it.
        ("0 = c", "?1 = c", int(0), false, &["c"]),
        ("0 = c", "c = ?1", int(0), false, &["c"]),
        // Lists: no bound value proves them, with the guarantee or without.
        ("c IN (1, 2)", "c IN (?1, 2)", int(1), false, &[]),
        (
            "c NOT IN ('x', 'y')",
            "c NOT IN (?1, 'y')",
            text("x"),
            false,
            &[],
        ),
        (
            "c BETWEEN 1 AND 5",
            "c BETWEEN ?1 AND 5",
            int(1),
            false,
            &[],
        ),
        // NULL tests: no value is involved.
        ("c IS NULL", "c IS ?1", Value::Null, false, &[]),
        ("c IS NOT NULL", "c = ?1", int(3), true, &[]),
        ("c NOTNULL", "c = ?1", int(3), true, &[]),
    ];

    let mut wrong = Vec::new();
    for (condition, term, value, uses_it_without, held_to_a_literal) in planted {
        let schema =
            format!("CREATE TABLE t (a, b, c, d); CREATE INDEX planted ON t(a) WHERE {condition};");
        let query = format!("SELECT COUNT(*) FROM t WHERE a = 'k' AND {term}");
        let free = rusqlite::Connection::open_in_memory().unwrap();
        free.execute_batch(&schema).unwrap();
        let guaranteed = rusqlite::Connection::open_in_memory().unwrap();
        guaranteed.execute_batch(&schema).unwrap();
        guaranteed
            .set_db_config(
                rusqlite::config::DbConfig::SQLITE_DBCONFIG_ENABLE_QPSG,
                true,
            )
            .unwrap();
        let without = plan_once_bound(&free, &query, &value).contains("planted");
        let with = plan_once_bound(&guaranteed, &query, &value).contains("planted");

        // The reader, through the same walk the census uses.
        let conditions = partial_index_conditions(&free);
        let listed = columns_held_to_a_literal(&conditions);
        if let [(_, Err(why))] = conditions.as_slice() {
            wrong.push(format!("{condition}: not read: {why}"));
            continue;
        }
        if without != uses_it_without {
            wrong.push(format!(
                "{condition}: without the guarantee `{term}` uses the index: {without}, and \
                 this table says {uses_it_without}"
            ));
        }
        let given_up = without && !with;
        if given_up && listed.is_empty() {
            wrong.push(format!(
                "{condition}: the guarantee gives the index up for `{term}` and the reader \
                 lists no column"
            ));
        }
        if listed.is_empty() && without != with {
            wrong.push(format!(
                "{condition}: the reader says no bound value matters, and SQLite's plan moved"
            ));
        }
        if listed != held_to_a_literal {
            wrong.push(format!(
                "{condition}: the reader holds {listed:?} to a literal, not {held_to_a_literal:?}"
            ));
        }
        if !held_to_a_literal.is_empty() && uses_it_without && !given_up {
            wrong.push(format!(
                "{condition}: planted as a condition a bound value proves, and SQLite's plan \
                 for `{term}` is the same with the guarantee and without (uses the index: \
                 {without}, {with})"
            ));
        }
    }
    assert!(wrong.is_empty(), "{}", wrong.join("\n"));
}

#[test]
fn the_partial_index_census_sees_each_planted_shape() {
    // The product side: each spelling of a bound comparison.
    let planted = "\
        \"WHERE links.target_path IS NULL AND links.is_external = ?2\",
        \"WHERE l.is_external = 0 AND l.target_path = ?1\",
        \"AND p.is_external NOT IN (?1, ?2)\",
        \"AND p.is_external NOT IN (0, 1)\",
        \"UPDATE links SET is_external = ?3 WHERE rowid = ?1\",
        \"UPDATE links SET target_raw = ?2 WHERE is_external = ?3\",
        let is_external = looks_external(&url_string);
        \"WHERE ?2 = is_external\",
        format!(\"AND is_external IN ({placeholders})\"),
        \"AND is_external = :external\",
        // WHERE is_external = ?2, in a comment
        \"WHERE is_external IS ?1 OR Is_External == @external\",
        \"ON CONFLICT DO UPDATE SET is_external = ?4\",
        \"WHERE is_external >= $low AND 1 = 1\",
        \"WHERE is_external BETWEEN ?1 AND ?2\",
        \"WHERE \\\"is_external\\\" = ?\",
        \"WHERE [is_external] <> ?7\",
        \"WHERE is_external = (?1)\",
        \"WHERE :x != l.`is_external` OR ?3 = \\\"is_external\\\"\",
        \"WHERE is_external = +?1 AND is_external < 2\",
        \"WHERE is_external IS NOT ?\",
";
    let lines = product_lines(planted);
    let numbered = || lines.iter().map(|line| (line.number, line.text));
    assert_eq!(
        binds_a_partial_index_condition("is_external", numbered()),
        vec![1, 3, 6, 8, 9, 10, 12, 14, 15, 16, 17, 18, 19, 21]
    );

    // The schema walk, on a scratch schema: SQLite says which indexes are
    // partial, each is read in full or comes back as an error, and a
    // negative literal names its column as any literal does.
    let scratch = rusqlite::Connection::open_in_memory().unwrap();
    scratch
        .execute_batch(
            "CREATE TABLE t (a, b, c, d, e, \"where\" TEXT);
             CREATE INDEX plain ON t(a);
             CREATE INDEX on_an_expression ON t(lower(a), b);
             CREATE INDEX not_null ON t(a) WHERE b IS NOT NULL;
             CREATE INDEX by_number ON t(a) WHERE c = 0 AND b IS NULL;
             CREATE INDEX by_list ON t(a)
             WHERE d NOT IN ('x', 'y');
             CREATE INDEX by_negative ON t(a) WHERE e = -1;
             CREATE INDEX by_a_column_named_where ON t(\"where\", lower(a)) -- WHERE e = 9
             WHERE \"where\" > 'm';
             CREATE INDEX by_a_function ON t(a) WHERE lower(d) = 'x';
             CREATE INDEX by_arithmetic ON t(a) WHERE c + 1 = 2;
             CREATE INDEX by_like ON t(a) WHERE d LIKE 'a%';",
        )
        .unwrap();
    let conditions = partial_index_conditions(&scratch);
    let read = in_words(&conditions);
    assert_eq!(read.len(), 8, "{read:#?}");
    assert_eq!(
        read[..5],
        [
            "by_a_column_named_where: where compared with a literal",
            "by_a_function: NOT READ: the function lower, in: CREATE INDEX by_a_function ON t(a) \
             WHERE lower(d) = 'x'",
            "by_arithmetic: NOT READ: Mark(\"+\") is not part of a condition it knows, in: \
             CREATE INDEX by_arithmetic ON t(a) WHERE c + 1 = 2",
            "by_like: NOT READ: Word(\"LIKE\") is not part of a condition it knows, in: CREATE \
             INDEX by_like ON t(a) WHERE d LIKE 'a%'",
            "by_list: d in a list",
        ]
    );
    assert_eq!(
        read[5..],
        [
            "by_negative: e compared with a literal",
            "by_number: c compared with a literal, b tested for NULL",
            "not_null: b tested for NULL",
        ]
    );
    assert_eq!(columns_held_to_a_literal(&conditions), ["c", "e", "where"]);
    // And the product rule refuses a bound comparison on the column the
    // negative literal named.
    let bound = "        \"SELECT 1 FROM t WHERE e = ?1\",\n";
    let lines = product_lines(bound);
    assert_eq!(
        binds_a_partial_index_condition("e", lines.iter().map(|line| (line.number, line.text))),
        vec![1]
    );

    // What the reader refuses to guess at.
    for unknown in [
        "lower(c) = 'x'",
        "c + 1 = 2",
        "c LIKE 'a%'",
        "c = 0 AND length(d) > 1",
        "c COLLATE NOCASE = 'a'",
        "CASE WHEN c THEN 1 END",
        "c IS DISTINCT FROM 0",
        "t.c = 0",
        "c IN (d, 1)",
        "c BETWEEN d AND 5",
        "1 = 1",
        "c = - 'x'",
        "(c = 0",
        "c = 0 d = 1",
        "c =",
    ] {
        assert!(
            read_condition(&sql_tokens(unknown)).is_err(),
            "{unknown}: read as {:?}",
            read_condition(&sql_tokens(unknown))
        );
    }
    // And what it reads without a literal in it.
    assert_eq!(
        read_condition(&sql_tokens("c = d AND b")),
        Ok(vec![
            ("c".to_string(), Held::WithNoLiteral),
            ("d".to_string(), Held::WithNoLiteral),
            ("b".to_string(), Held::WithNoLiteral),
        ])
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
