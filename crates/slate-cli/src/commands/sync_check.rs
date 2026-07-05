// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `slate sync-check <vault-path>` (M-4, #535).
//!
//! The one command that builds no index: it runs the M-1 detector and
//! the M-2 LiveSync config reader directly against the vault root
//! ([`detect_sync_providers`] + [`read_livesync_config`]) — no session
//! open, no scan. Detection is information, not failure, so the exit
//! code stays 0 even when sync systems are present.
//!
//! `data` shape (the `slate.cli.v1` stability contract):
//! ```json
//! { "supported": bool,
//!   "providers": [{ "kind": "livesync|icloud-drive|dropbox|onedrive|
//!                            google-drive|git|syncthing",
//!                   "risk": "low|medium|high",
//!                   "evidence": [String], "recommendation": String }],
//!   "multi_sync_warning": String|null,
//!   "livesync_config": { "status": "not-present|parsed|malformed", … } }
//! ```
//! The `kind`/`risk` wire strings are part of the contract and are
//! **distinct** from `SyncProviderKind::display_name()` (which is the
//! human/UI label). The slug mapping lives here in the CLI, per the
//! spec's explicit note.

use std::path::Path;

use slate_core::sync_detect::{
    DetectedSyncProvider, LiveSyncConfig, LiveSyncConfigStatus, RiskLevel, SyncDetectionReport,
    SyncProviderKind, detect_sync_providers, read_livesync_config,
};

use crate::output::{CommandOutput, tsv_row};
use crate::session::{CliError, abs_display};

/// Run `slate sync-check`. Validates the vault path is a directory
/// (exit 1 `not a vault directory: <path>` otherwise — same rule as the
/// session helper), then probes. Never opens a session.
pub fn run(raw_path: &Path) -> Result<(String, CommandOutput), CliError> {
    // sync-check skips the session, so the directory check that
    // `open_vault` would have done is done here directly.
    if !raw_path.is_dir() {
        return Err(CliError::NotAVaultDirectory {
            path: raw_path.display().to_string(),
        });
    }
    let abs_path = abs_display(raw_path);

    let report = detect_sync_providers(raw_path);
    let livesync = read_livesync_config(raw_path);

    let data = serde_json::json!({
        "supported": report.supported,
        "providers": report
            .providers
            .iter()
            .map(provider_json)
            .collect::<Vec<_>>(),
        "multi_sync_warning": report.multi_sync_warning,
        "livesync_config": livesync_json(&livesync),
    });

    let human = render_human(&report, &livesync);
    let tsv = render_tsv(&report);

    Ok((
        abs_path,
        CommandOutput {
            data,
            human,
            tsv,
            human_verbatim: false,
        },
    ))
}

// --- Wire-string slugs (the `slate.cli.v1` contract) -----------------

/// Map a provider kind to its wire slug. Distinct from `display_name()`
/// — the slug is the machine-stable identifier in json/tsv output.
fn kind_slug(kind: SyncProviderKind) -> &'static str {
    match kind {
        SyncProviderKind::LiveSync => "livesync",
        SyncProviderKind::ICloudDrive => "icloud-drive",
        SyncProviderKind::Dropbox => "dropbox",
        SyncProviderKind::OneDrive => "onedrive",
        SyncProviderKind::GoogleDrive => "google-drive",
        SyncProviderKind::Git => "git",
        SyncProviderKind::Syncthing => "syncthing",
    }
}

/// Map a risk level to its wire slug.
fn risk_slug(risk: RiskLevel) -> &'static str {
    match risk {
        RiskLevel::Low => "low",
        RiskLevel::Medium => "medium",
        RiskLevel::High => "high",
    }
}

// --- json shaping ----------------------------------------------------

fn provider_json(p: &DetectedSyncProvider) -> serde_json::Value {
    serde_json::json!({
        "kind": kind_slug(p.kind),
        "risk": risk_slug(p.risk_level),
        "evidence": p.evidence_paths,
        "recommendation": p.recommendation,
    })
}

fn livesync_json(status: &LiveSyncConfigStatus) -> serde_json::Value {
    match status {
        LiveSyncConfigStatus::NotPresent => serde_json::json!({ "status": "not-present" }),
        LiveSyncConfigStatus::Malformed { reason } => serde_json::json!({
            "status": "malformed",
            "reason": reason,
        }),
        LiveSyncConfigStatus::Parsed(cfg) => livesync_config_json(cfg),
    }
}

fn livesync_config_json(cfg: &LiveSyncConfig) -> serde_json::Value {
    serde_json::json!({
        "status": "parsed",
        "server_host": cfg.server_host,
        "database": cfg.database,
        "live_sync_enabled": cfg.live_sync_enabled,
        "sync_on_save": cfg.sync_on_save,
        "sync_on_start": cfg.sync_on_start,
        "end_to_end_encryption": cfg.end_to_end_encryption,
    })
}

// --- human format ----------------------------------------------------

/// Human format (m_spec §M-4): the `audio_summary` line, then one
/// indented block per provider (display name, risk, recommendation,
/// evidence lines), then the LiveSync config block when parsed.
fn render_human(report: &SyncDetectionReport, livesync: &LiveSyncConfigStatus) -> String {
    let mut lines = vec![report.audio_summary.clone()];

    for p in &report.providers {
        lines.push(String::new());
        lines.push(format!("  {}", p.kind.display_name()));
        lines.push(format!("    Risk: {}", risk_human(p.risk_level)));
        lines.push(format!("    {}", p.recommendation));
        if !p.evidence_paths.is_empty() {
            lines.push("    Evidence:".to_string());
            for ev in &p.evidence_paths {
                lines.push(format!("      {ev}"));
            }
        }
    }

    if let Some(warning) = &report.multi_sync_warning {
        lines.push(String::new());
        lines.push(format!("Warning: {warning}"));
    }

    // LiveSync config block, only meaningful once LiveSync is detected;
    // print whatever the reader returned (parsed rows, malformed
    // reason, or nothing when not present).
    render_livesync_human(&mut lines, report, livesync);

    lines.join("\n")
}

fn render_livesync_human(
    lines: &mut Vec<String>,
    report: &SyncDetectionReport,
    livesync: &LiveSyncConfigStatus,
) {
    let livesync_detected = report
        .providers
        .iter()
        .any(|p| p.kind == SyncProviderKind::LiveSync);
    if !livesync_detected {
        return;
    }
    match livesync {
        LiveSyncConfigStatus::Parsed(cfg) => {
            lines.push(String::new());
            lines.push("LiveSync config:".to_string());
            lines.push(format!("  Server host: {}", opt(&cfg.server_host)));
            lines.push(format!("  Database: {}", opt(&cfg.database)));
            lines.push(format!("  Live sync: {}", opt_bool(cfg.live_sync_enabled)));
            lines.push(format!("  Sync on save: {}", opt_bool(cfg.sync_on_save)));
            lines.push(format!("  Sync on start: {}", opt_bool(cfg.sync_on_start)));
            lines.push(format!(
                "  End-to-end encryption: {}",
                opt_bool(cfg.end_to_end_encryption)
            ));
        }
        LiveSyncConfigStatus::Malformed { reason } => {
            lines.push(String::new());
            lines.push(format!("LiveSync config could not be read: {reason}"));
        }
        LiveSyncConfigStatus::NotPresent => {
            lines.push(String::new());
            lines.push("LiveSync plugin present; no config found.".to_string());
        }
    }
}

fn risk_human(risk: RiskLevel) -> &'static str {
    match risk {
        RiskLevel::Low => "Low",
        RiskLevel::Medium => "Medium",
        RiskLevel::High => "High",
    }
}

fn opt(value: &Option<String>) -> &str {
    value.as_deref().unwrap_or("Unknown")
}

fn opt_bool(value: Option<bool>) -> &'static str {
    match value {
        Some(true) => "On",
        Some(false) => "Off",
        None => "Unknown",
    }
}

// --- tsv format ------------------------------------------------------

/// TSV format (m_spec §M-4): header `kind risk evidence recommendation`,
/// one row per provider, evidence joined with `";"`. The
/// `multi_sync_warning` and LiveSync config are json/human-only ("use
/// --format json for the full report").
fn render_tsv(report: &SyncDetectionReport) -> String {
    let mut rows = vec![tsv_row(["kind", "risk", "evidence", "recommendation"])];
    for p in &report.providers {
        let evidence = p.evidence_paths.join(";");
        rows.push(tsv_row([
            kind_slug(p.kind),
            risk_slug(p.risk_level),
            &evidence,
            &p.recommendation,
        ]));
    }
    rows.join("\n")
}
