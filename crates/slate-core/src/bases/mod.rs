// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Bases query parsing and execution primitives.
//!
//! Milestone N lands this module in waves. N0-1 owns only the expression
//! language parser; `.base` YAML parsing, serialization, scanner indexing,
//! and execution arrive in later issues.

pub mod expr;
