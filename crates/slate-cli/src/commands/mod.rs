// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! One module per `slate` sub-command (M-4, #535). Each `run` is a thin
//! wrapper over `slate_core::VaultSession` (or the M-1/M-2 detectors for
//! `sync-check`) — no business logic lives in the CLI layer.

pub mod open;
pub mod render_template;
pub mod sync_check;
pub mod tasks;
