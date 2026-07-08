// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! One module per `slate` sub-command (M-4, #535). Each `run` is a thin
//! wrapper over `slate_core::VaultSession` (or the M-1/M-2 detectors for
//! `sync-check`) — no business logic lives in the CLI layer.
//!
//! `completions` (#639) is the one exception to "wraps a vault session":
//! it is a meta-command (like `--help`) that generates a shell-completion
//! script from clap's grammar and touches no vault.

pub mod completions;
pub mod links;
pub mod list;
pub mod open;
pub mod properties;
pub mod query;
pub mod read;
pub mod render_template;
pub mod search;
pub mod sync_check;
pub mod tasks;
pub mod write;
