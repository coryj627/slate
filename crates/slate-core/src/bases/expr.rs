// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Bases expression parser.
//!
//! The parser is deliberately pure and I/O-free. It keeps a byte span on every
//! AST node so later `.base` serialization can splice untouched expressions
//! without reformatting user-authored YAML.

use std::fmt;

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub struct Span {
    pub start: u32,
    pub end: u32,
}

impl Span {
    fn join(self, other: Span) -> Span {
        Span {
            start: self.start,
            end: other.end,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Expr {
    pub kind: ExprKind,
    pub span: Span,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum ExprKind {
    Lit(Lit),
    Prop(PropertyRef),
    Index {
        base: Box<Expr>,
        index: Box<Expr>,
    },
    Field {
        base: Box<Expr>,
        name: String,
    },
    Unary {
        op: UnaryOp,
        rhs: Box<Expr>,
    },
    Binary {
        op: BinaryOp,
        lhs: Box<Expr>,
        rhs: Box<Expr>,
    },
    Call {
        callee: Callee,
        args: Vec<Expr>,
    },
    ListExpr {
        base: Box<Expr>,
        kind: ListExprKind,
        body: Box<Expr>,
        init: Option<Box<Expr>>,
    },
    Unsupported {
        raw: String,
        reason: String,
    },
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum Lit {
    String(String),
    Number(f64),
    Bool(bool),
    List(Vec<Expr>),
    Object(Vec<(String, Expr)>),
    Regex { pattern: String, flags: String },
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum Callee {
    Global(GlobalFn),
    Method {
        receiver: Box<Expr>,
        name: MethodName,
    },
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum UnaryOp {
    Not,
    Neg,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum BinaryOp {
    Add,
    Sub,
    Mul,
    Div,
    Mod,
    Eq,
    Ne,
    Gt,
    Lt,
    Gte,
    Lte,
    And,
    Or,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum ListExprKind {
    Filter,
    Map,
    Reduce,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum PropertyRef {
    Note(String),
    File(FileField),
    Formula(String),
    This,
    ThisNote(String),
    ThisFile(FileField),
    TaskField(TaskField),
    ImplicitValue,
    ImplicitIndex,
    ImplicitAcc,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum FileField {
    Name,
    Basename,
    Path,
    Folder,
    Ext,
    Size,
    Properties,
    Tags,
    Links,
    Backlinks,
    Embeds,
    File,
    Ctime,
    Mtime,
    InDegree,
    OutDegree,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum TaskField {
    Text,
    Status,
    Completed,
    Due,
    Scheduled,
    Priority,
    File,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum GlobalFn {
    Date,
    Duration,
    EscapeHtml,
    File,
    Html,
    Icon,
    If,
    Image,
    Link,
    List,
    Max,
    Min,
    Now,
    Number,
    Random,
    Today,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum MethodName {
    IsTruthy,
    IsType,
    ToString,
    Date,
    Format,
    Time,
    Relative,
    IsEmpty,
    Contains,
    ContainsAll,
    ContainsAny,
    StartsWith,
    EndsWith,
    Lower,
    Title,
    Trim,
    Reverse,
    Repeat,
    Slice,
    Split,
    Replace,
    Abs,
    Ceil,
    Floor,
    Round,
    ToFixed,
    Join,
    Flat,
    Sort,
    Unique,
    AsFile,
    LinksTo,
    AsLink,
    HasLink,
    HasProperty,
    HasTag,
    InFolder,
    Keys,
    Values,
    Matches,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ExprParseError {
    pub message: String,
    pub span: Span,
}

impl fmt::Display for ExprParseError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            f,
            "{} at byte {}..{}",
            self.message, self.span.start, self.span.end
        )
    }
}

impl std::error::Error for ExprParseError {}

pub fn parse_expr(source: &str) -> Result<Expr, ExprParseError> {
    let tokens = Lexer::new(source).lex()?;
    let mut parser = Parser {
        source,
        tokens,
        pos: 0,
        implicit: ImplicitBindings::default(),
    };
    let expr = parser.parse_expression(0)?;
    if !parser.at(TokenKind::Eof) {
        return Err(parser.error_here("unexpected token after expression"));
    }
    Ok(expr)
}

#[derive(Debug, Clone, PartialEq)]
struct Token {
    kind: TokenKind,
    span: Span,
}

#[derive(Debug, Clone, PartialEq)]
enum TokenKind {
    Ident(String),
    Number(f64),
    String(String),
    Regex { pattern: String, flags: String },
    True,
    False,
    Bang,
    Minus,
    Plus,
    Star,
    Slash,
    Percent,
    EqEq,
    BangEq,
    Gt,
    Lt,
    Gte,
    Lte,
    AndAnd,
    OrOr,
    Dot,
    Comma,
    Colon,
    LParen,
    RParen,
    LBracket,
    RBracket,
    LBrace,
    RBrace,
    Eof,
}

struct Lexer<'a> {
    source: &'a str,
    bytes: &'a [u8],
    pos: usize,
    expect_value: bool,
}

impl<'a> Lexer<'a> {
    fn new(source: &'a str) -> Self {
        Self {
            source,
            bytes: source.as_bytes(),
            pos: 0,
            expect_value: true,
        }
    }

    fn lex(mut self) -> Result<Vec<Token>, ExprParseError> {
        let mut tokens = Vec::new();
        while self.pos < self.bytes.len() {
            let b = self.bytes[self.pos];
            if b.is_ascii_whitespace() {
                self.pos += 1;
                continue;
            }
            let token = match b {
                b'(' => self.single(TokenKind::LParen, true),
                b')' => self.single(TokenKind::RParen, false),
                b'[' => self.single(TokenKind::LBracket, true),
                b']' => self.single(TokenKind::RBracket, false),
                b'{' => self.single(TokenKind::LBrace, true),
                b'}' => self.single(TokenKind::RBrace, false),
                b'.' => self.single(TokenKind::Dot, true),
                b',' => self.single(TokenKind::Comma, true),
                b':' => self.single(TokenKind::Colon, true),
                b'+' => self.single(TokenKind::Plus, true),
                b'-' => self.single(TokenKind::Minus, true),
                b'*' => self.single(TokenKind::Star, true),
                b'%' => self.single(TokenKind::Percent, true),
                b'!' if self.peek_byte(1) == Some(b'=') => self.double(TokenKind::BangEq, true),
                b'!' => self.single(TokenKind::Bang, true),
                b'=' if self.peek_byte(1) == Some(b'=') => self.double(TokenKind::EqEq, true),
                b'>' if self.peek_byte(1) == Some(b'=') => self.double(TokenKind::Gte, true),
                b'>' => self.single(TokenKind::Gt, true),
                b'<' if self.peek_byte(1) == Some(b'=') => self.double(TokenKind::Lte, true),
                b'<' => self.single(TokenKind::Lt, true),
                b'&' if self.peek_byte(1) == Some(b'&') => self.double(TokenKind::AndAnd, true),
                b'|' if self.peek_byte(1) == Some(b'|') => self.double(TokenKind::OrOr, true),
                b'/' if self.expect_value => self.regex()?,
                b'/' => self.single(TokenKind::Slash, true),
                b'\'' | b'"' => self.string(b)?,
                b'0'..=b'9' => self.number()?,
                _ if is_ident_start(b) => self.ident(),
                _ => {
                    return Err(ExprParseError {
                        message: format!("unexpected character {:?}", b as char),
                        span: span(self.pos, self.pos + 1),
                    });
                }
            };
            tokens.push(token);
        }
        tokens.push(Token {
            kind: TokenKind::Eof,
            span: span(self.pos, self.pos),
        });
        Ok(tokens)
    }

    fn peek_byte(&self, offset: usize) -> Option<u8> {
        self.bytes.get(self.pos + offset).copied()
    }

    fn single(&mut self, kind: TokenKind, expect_value: bool) -> Token {
        let start = self.pos;
        self.pos += 1;
        self.expect_value = expect_value;
        Token {
            kind,
            span: span(start, self.pos),
        }
    }

    fn double(&mut self, kind: TokenKind, expect_value: bool) -> Token {
        let start = self.pos;
        self.pos += 2;
        self.expect_value = expect_value;
        Token {
            kind,
            span: span(start, self.pos),
        }
    }

    fn ident(&mut self) -> Token {
        let start = self.pos;
        self.pos += 1;
        while self
            .bytes
            .get(self.pos)
            .is_some_and(|b| is_ident_continue(*b))
        {
            self.pos += 1;
        }
        let text = &self.source[start..self.pos];
        self.expect_value = false;
        Token {
            kind: match text {
                "true" => TokenKind::True,
                "false" => TokenKind::False,
                _ => TokenKind::Ident(text.to_string()),
            },
            span: span(start, self.pos),
        }
    }

    fn number(&mut self) -> Result<Token, ExprParseError> {
        let start = self.pos;
        self.pos += 1;
        while self.bytes.get(self.pos).is_some_and(|b| b.is_ascii_digit()) {
            self.pos += 1;
        }
        if self.peek_byte(0) == Some(b'.') && self.peek_byte(1).is_some_and(|b| b.is_ascii_digit())
        {
            self.pos += 1;
            while self.bytes.get(self.pos).is_some_and(|b| b.is_ascii_digit()) {
                self.pos += 1;
            }
        }
        let text = &self.source[start..self.pos];
        let value = text.parse::<f64>().map_err(|_| ExprParseError {
            message: format!("invalid number {text:?}"),
            span: span(start, self.pos),
        })?;
        self.expect_value = false;
        Ok(Token {
            kind: TokenKind::Number(value),
            span: span(start, self.pos),
        })
    }

    fn string(&mut self, quote: u8) -> Result<Token, ExprParseError> {
        let start = self.pos;
        self.pos += 1;
        let mut value = String::new();
        while self.pos < self.bytes.len() {
            let b = self.bytes[self.pos];
            if b == quote {
                self.pos += 1;
                self.expect_value = false;
                return Ok(Token {
                    kind: TokenKind::String(value),
                    span: span(start, self.pos),
                });
            }
            if b == b'\\' {
                self.pos += 1;
                let Some(next) = self.source[self.pos..].chars().next() else {
                    break;
                };
                self.pos += next.len_utf8();
                match next {
                    '"' => value.push('"'),
                    '\'' => value.push('\''),
                    '\\' => value.push('\\'),
                    'n' => value.push('\n'),
                    't' => value.push('\t'),
                    other => {
                        value.push('\\');
                        value.push(other);
                    }
                }
            } else {
                let ch = self.source[self.pos..]
                    .chars()
                    .next()
                    .expect("pos is in bounds");
                self.pos += ch.len_utf8();
                value.push(ch);
            }
        }
        Err(ExprParseError {
            message: "unterminated string".to_string(),
            span: span(start, self.pos),
        })
    }

    fn regex(&mut self) -> Result<Token, ExprParseError> {
        let start = self.pos;
        self.pos += 1;
        let mut pattern = String::new();
        let mut escaped = false;
        while self.pos < self.bytes.len() {
            let b = self.bytes[self.pos];
            if escaped {
                let ch = self.source[self.pos..]
                    .chars()
                    .next()
                    .expect("pos is in bounds");
                self.pos += ch.len_utf8();
                pattern.push('\\');
                pattern.push(ch);
                escaped = false;
                continue;
            }
            if b == b'\\' {
                self.pos += 1;
                escaped = true;
                continue;
            }
            if b == b'/' {
                self.pos += 1;
                let flags_start = self.pos;
                while self
                    .bytes
                    .get(self.pos)
                    .is_some_and(|b| b.is_ascii_alphabetic())
                {
                    self.pos += 1;
                }
                let flags = self.source[flags_start..self.pos].to_string();
                self.expect_value = false;
                return Ok(Token {
                    kind: TokenKind::Regex { pattern, flags },
                    span: span(start, self.pos),
                });
            }
            let ch = self.source[self.pos..]
                .chars()
                .next()
                .expect("pos is in bounds");
            self.pos += ch.len_utf8();
            pattern.push(ch);
        }
        Err(ExprParseError {
            message: "unterminated regex".to_string(),
            span: span(start, self.pos),
        })
    }
}

#[derive(Debug, Clone, Copy, Default)]
struct ImplicitBindings {
    value: bool,
    index: bool,
    acc: bool,
}

struct Parser<'a> {
    source: &'a str,
    tokens: Vec<Token>,
    pos: usize,
    implicit: ImplicitBindings,
}

impl<'a> Parser<'a> {
    fn parse_expression(&mut self, min_prec: u8) -> Result<Expr, ExprParseError> {
        let mut lhs = self.parse_unary()?;
        while let Some((op, prec)) = self.peek_binary() {
            if prec < min_prec {
                break;
            }
            self.pos += 1;
            let rhs = self.parse_expression(prec + 1)?;
            let span = lhs.span.join(rhs.span);
            lhs = Expr {
                kind: ExprKind::Binary {
                    op,
                    lhs: Box::new(lhs),
                    rhs: Box::new(rhs),
                },
                span,
            };
        }
        Ok(lhs)
    }

    fn parse_unary(&mut self) -> Result<Expr, ExprParseError> {
        if self.at(TokenKind::Bang) {
            let start = self.advance().span;
            let rhs = self.parse_unary()?;
            return Ok(Expr {
                span: start.join(rhs.span),
                kind: ExprKind::Unary {
                    op: UnaryOp::Not,
                    rhs: Box::new(rhs),
                },
            });
        }
        if self.at(TokenKind::Minus) {
            let start = self.advance().span;
            let rhs = self.parse_unary()?;
            return Ok(Expr {
                span: start.join(rhs.span),
                kind: ExprKind::Unary {
                    op: UnaryOp::Neg,
                    rhs: Box::new(rhs),
                },
            });
        }
        let primary = self.parse_primary()?;
        self.parse_postfix(primary)
    }

    fn parse_primary(&mut self) -> Result<Expr, ExprParseError> {
        let token = self.advance().clone();
        match token.kind {
            TokenKind::Number(value) => Ok(Expr {
                span: token.span,
                kind: ExprKind::Lit(Lit::Number(value)),
            }),
            TokenKind::String(value) => Ok(Expr {
                span: token.span,
                kind: ExprKind::Lit(Lit::String(value)),
            }),
            TokenKind::Regex { pattern, flags } => {
                if flags.is_empty() || flags == "g" {
                    Ok(Expr {
                        span: token.span,
                        kind: ExprKind::Lit(Lit::Regex { pattern, flags }),
                    })
                } else {
                    Ok(Expr {
                        span: token.span,
                        kind: ExprKind::Unsupported {
                            raw: self.source[token.span.start as usize..token.span.end as usize]
                                .to_string(),
                            reason: format!("unsupported regex flag(s) {flags}"),
                        },
                    })
                }
            }
            TokenKind::True => Ok(Expr {
                span: token.span,
                kind: ExprKind::Lit(Lit::Bool(true)),
            }),
            TokenKind::False => Ok(Expr {
                span: token.span,
                kind: ExprKind::Lit(Lit::Bool(false)),
            }),
            TokenKind::Ident(name) => self.parse_identifier(name, token.span),
            TokenKind::LParen => {
                let mut expr = self.parse_expression(0)?;
                let end = self.expect(TokenKind::RParen, "expected ')'")?;
                expr.span = token.span.join(end);
                Ok(expr)
            }
            TokenKind::LBracket => self.parse_list_literal(token.span),
            TokenKind::LBrace => self.parse_object_literal(token.span),
            _ => Err(ExprParseError {
                message: "expected expression".to_string(),
                span: token.span,
            }),
        }
    }

    fn parse_identifier(&mut self, name: String, start: Span) -> Result<Expr, ExprParseError> {
        if self.at(TokenKind::LParen) {
            return self.parse_global_call(name, start);
        }

        match name.as_str() {
            "note" => self.parse_note_property(start),
            "formula" => self.parse_formula_property(start),
            "file" => self.parse_file_property_or_object(start),
            "this" => self.parse_this_property(start),
            "task" => self.parse_task_property(start),
            "value" if self.implicit.value => Ok(Expr {
                span: start,
                kind: ExprKind::Prop(PropertyRef::ImplicitValue),
            }),
            "index" if self.implicit.index => Ok(Expr {
                span: start,
                kind: ExprKind::Prop(PropertyRef::ImplicitIndex),
            }),
            "acc" if self.implicit.acc => Ok(Expr {
                span: start,
                kind: ExprKind::Prop(PropertyRef::ImplicitAcc),
            }),
            _ => Ok(Expr {
                span: start,
                kind: ExprKind::Prop(PropertyRef::Note(name)),
            }),
        }
    }

    fn parse_postfix(&mut self, mut expr: Expr) -> Result<Expr, ExprParseError> {
        loop {
            if self.at(TokenKind::LBracket) {
                self.advance();
                let index = self.parse_expression(0)?;
                let end = self.expect(TokenKind::RBracket, "expected ']'")?;
                let span = expr.span.join(end);
                expr = Expr {
                    kind: ExprKind::Index {
                        base: Box::new(expr),
                        index: Box::new(index),
                    },
                    span,
                };
                continue;
            }

            if !self.at(TokenKind::Dot) {
                break;
            }
            self.advance();
            let (name, name_span) = self.expect_ident("expected member name after '.'")?;
            if self.at(TokenKind::LParen) {
                if let Some(kind) = list_expr_kind(&name) {
                    expr = self.parse_list_expr(expr, kind, name_span)?;
                } else {
                    let args = self.parse_arg_list()?;
                    let end = self.previous_span();
                    let span = expr.span.join(end);
                    if let Some(method) = method_name(&name) {
                        expr = Expr {
                            kind: ExprKind::Call {
                                callee: Callee::Method {
                                    receiver: Box::new(expr),
                                    name: method,
                                },
                                args,
                            },
                            span,
                        };
                    } else {
                        expr = Expr {
                            span,
                            kind: ExprKind::Unsupported {
                                raw: self.source[span.start as usize..span.end as usize]
                                    .to_string(),
                                reason: format!("unknown method {name}"),
                            },
                        };
                    }
                }
            } else {
                let span = expr.span.join(name_span);
                expr = Expr {
                    kind: ExprKind::Field {
                        base: Box::new(expr),
                        name,
                    },
                    span,
                };
            }
        }
        Ok(expr)
    }

    fn parse_global_call(&mut self, name: String, start: Span) -> Result<Expr, ExprParseError> {
        let args = self.parse_arg_list()?;
        let end = self.previous_span();
        let span = start.join(end);
        let Some(global) = global_fn(&name) else {
            return Ok(Expr {
                span,
                kind: ExprKind::Unsupported {
                    raw: self.source[span.start as usize..span.end as usize].to_string(),
                    reason: format!("unknown function {name}"),
                },
            });
        };
        Ok(Expr {
            span,
            kind: ExprKind::Call {
                callee: Callee::Global(global),
                args,
            },
        })
    }

    fn parse_arg_list(&mut self) -> Result<Vec<Expr>, ExprParseError> {
        self.expect(TokenKind::LParen, "expected '('")?;
        let mut args = Vec::new();
        if self.at(TokenKind::RParen) {
            self.advance();
            return Ok(args);
        }
        loop {
            args.push(self.parse_expression(0)?);
            if self.at(TokenKind::Comma) {
                self.advance();
                continue;
            }
            self.expect(TokenKind::RParen, "expected ')' after arguments")?;
            return Ok(args);
        }
    }

    fn parse_list_expr(
        &mut self,
        base: Expr,
        kind: ListExprKind,
        _method_span: Span,
    ) -> Result<Expr, ExprParseError> {
        self.expect(TokenKind::LParen, "expected '('")?;
        let saved = self.implicit;
        self.implicit = ImplicitBindings {
            value: true,
            index: true,
            acc: kind == ListExprKind::Reduce,
        };
        let body = self.parse_expression(0)?;
        let init = if kind == ListExprKind::Reduce {
            self.expect(TokenKind::Comma, "expected reduce initializer")?;
            Some(Box::new(self.parse_expression(0)?))
        } else {
            None
        };
        self.implicit = saved;
        let end = self.expect(TokenKind::RParen, "expected ')' after list expression")?;
        Ok(Expr {
            span: base.span.join(end),
            kind: ExprKind::ListExpr {
                base: Box::new(base),
                kind,
                body: Box::new(body),
                init,
            },
        })
    }

    fn parse_note_property(&mut self, start: Span) -> Result<Expr, ExprParseError> {
        let (key, end) = self.parse_property_key_after_namespace()?;
        Ok(Expr {
            span: start.join(end),
            kind: ExprKind::Prop(PropertyRef::Note(key)),
        })
    }

    fn parse_formula_property(&mut self, start: Span) -> Result<Expr, ExprParseError> {
        let (key, end) = self.parse_property_key_after_namespace()?;
        Ok(Expr {
            span: start.join(end),
            kind: ExprKind::Prop(PropertyRef::Formula(key)),
        })
    }

    fn parse_file_property_or_object(&mut self, start: Span) -> Result<Expr, ExprParseError> {
        if self.at(TokenKind::Dot) {
            let save = self.pos;
            self.advance();
            let (name, end) = self.expect_ident("expected file field")?;
            if self.at(TokenKind::LParen) {
                self.pos = save;
                return Ok(Expr {
                    span: start,
                    kind: ExprKind::Prop(PropertyRef::File(FileField::File)),
                });
            }
            let Some(field) = file_field(&name) else {
                return Ok(Expr {
                    span: start.join(end),
                    kind: ExprKind::Unsupported {
                        raw: self.source[start.start as usize..end.end as usize].to_string(),
                        reason: format!("unknown file field {name}"),
                    },
                });
            };
            return Ok(Expr {
                span: start.join(end),
                kind: ExprKind::Prop(PropertyRef::File(field)),
            });
        }
        Ok(Expr {
            span: start,
            kind: ExprKind::Prop(PropertyRef::File(FileField::File)),
        })
    }

    fn parse_this_property(&mut self, start: Span) -> Result<Expr, ExprParseError> {
        if !self.at(TokenKind::Dot) {
            return Ok(Expr {
                span: start,
                kind: ExprKind::Prop(PropertyRef::This),
            });
        }
        self.advance();
        let (name, first_end) = self.expect_ident("expected field after this.")?;
        if name == "file" {
            self.expect(TokenKind::Dot, "expected this.file.<field>")?;
            let (field_name, end) = self.expect_ident("expected file field")?;
            let Some(field) = file_field(&field_name) else {
                return Ok(Expr {
                    span: start.join(end),
                    kind: ExprKind::Unsupported {
                        raw: self.source[start.start as usize..end.end as usize].to_string(),
                        reason: format!("unknown this.file field {field_name}"),
                    },
                });
            };
            return Ok(Expr {
                span: start.join(end),
                kind: ExprKind::Prop(PropertyRef::ThisFile(field)),
            });
        }
        Ok(Expr {
            span: start.join(first_end),
            kind: ExprKind::Prop(PropertyRef::ThisNote(name)),
        })
    }

    fn parse_task_property(&mut self, start: Span) -> Result<Expr, ExprParseError> {
        self.expect(TokenKind::Dot, "expected task.<field>")?;
        let (name, end) = self.expect_ident("expected task field")?;
        let Some(field) = task_field(&name) else {
            return Ok(Expr {
                span: start.join(end),
                kind: ExprKind::Unsupported {
                    raw: self.source[start.start as usize..end.end as usize].to_string(),
                    reason: format!("unknown task field {name}"),
                },
            });
        };
        Ok(Expr {
            span: start.join(end),
            kind: ExprKind::Prop(PropertyRef::TaskField(field)),
        })
    }

    fn parse_property_key_after_namespace(&mut self) -> Result<(String, Span), ExprParseError> {
        if self.at(TokenKind::Dot) {
            self.advance();
            return self.expect_ident("expected property name");
        }
        if self.at(TokenKind::LBracket) {
            self.advance();
            let token = self.advance().clone();
            let TokenKind::String(key) = token.kind else {
                return Err(ExprParseError {
                    message: "expected string property key".to_string(),
                    span: token.span,
                });
            };
            let end = self.expect(TokenKind::RBracket, "expected ']' after property key")?;
            return Ok((key, end));
        }
        Err(self.error_here("expected property key"))
    }

    fn parse_list_literal(&mut self, start: Span) -> Result<Expr, ExprParseError> {
        let mut values = Vec::new();
        if self.at(TokenKind::RBracket) {
            let end = self.advance().span;
            return Ok(Expr {
                span: start.join(end),
                kind: ExprKind::Lit(Lit::List(values)),
            });
        }
        loop {
            values.push(self.parse_expression(0)?);
            if self.at(TokenKind::Comma) {
                self.advance();
                continue;
            }
            let end = self.expect(TokenKind::RBracket, "expected ']'")?;
            return Ok(Expr {
                span: start.join(end),
                kind: ExprKind::Lit(Lit::List(values)),
            });
        }
    }

    fn parse_object_literal(&mut self, start: Span) -> Result<Expr, ExprParseError> {
        let mut values = Vec::new();
        if self.at(TokenKind::RBrace) {
            let end = self.advance().span;
            return Ok(Expr {
                span: start.join(end),
                kind: ExprKind::Lit(Lit::Object(values)),
            });
        }
        loop {
            let key_token = self.advance().clone();
            let key = match key_token.kind {
                TokenKind::String(key) | TokenKind::Ident(key) => key,
                _ => {
                    return Err(ExprParseError {
                        message: "expected object key".to_string(),
                        span: key_token.span,
                    });
                }
            };
            self.expect(TokenKind::Colon, "expected ':' after object key")?;
            let value = self.parse_expression(0)?;
            values.push((key, value));
            if self.at(TokenKind::Comma) {
                self.advance();
                continue;
            }
            let end = self.expect(TokenKind::RBrace, "expected '}'")?;
            return Ok(Expr {
                span: start.join(end),
                kind: ExprKind::Lit(Lit::Object(values)),
            });
        }
    }

    fn peek_binary(&self) -> Option<(BinaryOp, u8)> {
        let op = match self.current().kind {
            TokenKind::OrOr => (BinaryOp::Or, 1),
            TokenKind::AndAnd => (BinaryOp::And, 2),
            TokenKind::EqEq => (BinaryOp::Eq, 3),
            TokenKind::BangEq => (BinaryOp::Ne, 3),
            TokenKind::Gt => (BinaryOp::Gt, 3),
            TokenKind::Lt => (BinaryOp::Lt, 3),
            TokenKind::Gte => (BinaryOp::Gte, 3),
            TokenKind::Lte => (BinaryOp::Lte, 3),
            TokenKind::Plus => (BinaryOp::Add, 4),
            TokenKind::Minus => (BinaryOp::Sub, 4),
            TokenKind::Star => (BinaryOp::Mul, 5),
            TokenKind::Slash => (BinaryOp::Div, 5),
            TokenKind::Percent => (BinaryOp::Mod, 5),
            _ => return None,
        };
        Some(op)
    }

    fn at(&self, kind: TokenKind) -> bool {
        std::mem::discriminant(&self.current().kind) == std::mem::discriminant(&kind)
    }

    fn current(&self) -> &Token {
        &self.tokens[self.pos]
    }

    fn advance(&mut self) -> &Token {
        let pos = self.pos;
        if !matches!(self.tokens[pos].kind, TokenKind::Eof) {
            self.pos += 1;
        }
        &self.tokens[pos]
    }

    fn previous_span(&self) -> Span {
        self.tokens[self.pos.saturating_sub(1)].span
    }

    fn expect(&mut self, kind: TokenKind, message: &str) -> Result<Span, ExprParseError> {
        if self.at(kind) {
            Ok(self.advance().span)
        } else {
            Err(self.error_here(message))
        }
    }

    fn expect_ident(&mut self, message: &str) -> Result<(String, Span), ExprParseError> {
        let token = self.advance().clone();
        match token.kind {
            TokenKind::Ident(name) => Ok((name, token.span)),
            _ => Err(ExprParseError {
                message: message.to_string(),
                span: token.span,
            }),
        }
    }

    fn error_here(&self, message: &str) -> ExprParseError {
        ExprParseError {
            message: message.to_string(),
            span: self.current().span,
        }
    }
}

fn span(start: usize, end: usize) -> Span {
    Span {
        start: start as u32,
        end: end as u32,
    }
}

fn is_ident_start(b: u8) -> bool {
    b.is_ascii_alphabetic() || b == b'_'
}

fn is_ident_continue(b: u8) -> bool {
    b.is_ascii_alphanumeric() || b == b'_'
}

fn global_fn(name: &str) -> Option<GlobalFn> {
    Some(match name {
        "date" => GlobalFn::Date,
        "duration" => GlobalFn::Duration,
        "escapeHTML" => GlobalFn::EscapeHtml,
        "file" => GlobalFn::File,
        "html" => GlobalFn::Html,
        "icon" => GlobalFn::Icon,
        "if" => GlobalFn::If,
        "image" => GlobalFn::Image,
        "link" => GlobalFn::Link,
        "list" => GlobalFn::List,
        "max" => GlobalFn::Max,
        "min" => GlobalFn::Min,
        "now" => GlobalFn::Now,
        "number" => GlobalFn::Number,
        "random" => GlobalFn::Random,
        "today" => GlobalFn::Today,
        _ => return None,
    })
}

fn method_name(name: &str) -> Option<MethodName> {
    Some(match name {
        "isTruthy" => MethodName::IsTruthy,
        "isType" => MethodName::IsType,
        "toString" => MethodName::ToString,
        "date" => MethodName::Date,
        "format" => MethodName::Format,
        "time" => MethodName::Time,
        "relative" => MethodName::Relative,
        "isEmpty" => MethodName::IsEmpty,
        "contains" => MethodName::Contains,
        "containsAll" => MethodName::ContainsAll,
        "containsAny" => MethodName::ContainsAny,
        "startsWith" => MethodName::StartsWith,
        "endsWith" => MethodName::EndsWith,
        "lower" => MethodName::Lower,
        "title" => MethodName::Title,
        "trim" => MethodName::Trim,
        "reverse" => MethodName::Reverse,
        "repeat" => MethodName::Repeat,
        "slice" => MethodName::Slice,
        "split" => MethodName::Split,
        "replace" => MethodName::Replace,
        "abs" => MethodName::Abs,
        "ceil" => MethodName::Ceil,
        "floor" => MethodName::Floor,
        "round" => MethodName::Round,
        "toFixed" => MethodName::ToFixed,
        "join" => MethodName::Join,
        "flat" => MethodName::Flat,
        "sort" => MethodName::Sort,
        "unique" => MethodName::Unique,
        "asFile" => MethodName::AsFile,
        "linksTo" => MethodName::LinksTo,
        "asLink" => MethodName::AsLink,
        "hasLink" => MethodName::HasLink,
        "hasProperty" => MethodName::HasProperty,
        "hasTag" => MethodName::HasTag,
        "inFolder" => MethodName::InFolder,
        "keys" => MethodName::Keys,
        "values" => MethodName::Values,
        "matches" => MethodName::Matches,
        _ => return None,
    })
}

fn list_expr_kind(name: &str) -> Option<ListExprKind> {
    Some(match name {
        "filter" => ListExprKind::Filter,
        "map" => ListExprKind::Map,
        "reduce" => ListExprKind::Reduce,
        _ => return None,
    })
}

fn file_field(name: &str) -> Option<FileField> {
    Some(match name {
        "name" => FileField::Name,
        "basename" => FileField::Basename,
        "path" => FileField::Path,
        "folder" => FileField::Folder,
        "ext" => FileField::Ext,
        "size" => FileField::Size,
        "properties" => FileField::Properties,
        "tags" => FileField::Tags,
        "links" => FileField::Links,
        "backlinks" => FileField::Backlinks,
        "embeds" => FileField::Embeds,
        "file" => FileField::File,
        "ctime" => FileField::Ctime,
        "mtime" => FileField::Mtime,
        "inDegree" => FileField::InDegree,
        "outDegree" => FileField::OutDegree,
        _ => return None,
    })
}

fn task_field(name: &str) -> Option<TaskField> {
    Some(match name {
        "text" => TaskField::Text,
        "status" => TaskField::Status,
        "completed" => TaskField::Completed,
        "due" => TaskField::Due,
        "scheduled" => TaskField::Scheduled,
        "priority" => TaskField::Priority,
        "file" => TaskField::File,
        _ => return None,
    })
}
