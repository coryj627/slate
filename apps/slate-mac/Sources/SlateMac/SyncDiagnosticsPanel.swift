// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Sync diagnostics leaf (Milestone M-3, #534) — the vault-level view
/// of `detect_sync` + `livesync_config` (m_spec §M-3).
///
/// A tester learns whether their vault is being managed by an external
/// sync system — and what risk that carries — the moment they open it:
/// the report loads once per vault open from the post-scan
/// continuation, and High-risk / multi-sync states announce assertively
/// (once per vault, via AppState's `AnnouncementPosting` seam). The
/// panel itself is the browsable surface: per-provider risk badges
/// (shape + text, never color alone), the exact recommendation copy
/// from the normative detector table, evidence paths behind a
/// disclosure, and the credential-free LiveSync config when that
/// plugin is detected.
/// The panel's five states (m_spec §M-3), selected by the pure
/// `SyncDiagnosticsPanel.state(report:error:)` so the precedence
/// matrix is unit-testable without rendering.
enum SyncDiagnosticsPanelState: Equatable {
    case unsupported
    case loading
    case error(String)
    case empty
    case populated(SyncDetectionReport)
}

struct SyncDiagnosticsPanel: View {
    @EnvironmentObject private var appState: AppState

    /// State precedence per m_spec §M-3: unsupported → error →
    /// loading → empty → populated. Unsupported wins over everything
    /// (the report exists and says so); a refresh failure shows the
    /// error + Retry even when a stale report is retained underneath.
    static func state(
        report: SyncDetectionReport?, error: String?
    ) -> SyncDiagnosticsPanelState {
        if let report, !report.supported { return .unsupported }
        if let error { return .error(error) }
        guard let report else { return .loading }
        return report.providers.isEmpty ? .empty : .populated(report)
    }

    var body: some View {
        Group {
            switch Self.state(
                report: appState.syncReport, error: appState.syncDiagnosticsError)
            {
            case .unsupported:
                // The copy is the report's own pre-rendered
                // `audioSummary` for the unsupported case — kept in
                // lock-step with sync_detect.rs by the state test.
                LeafEmptyState(
                    message: "Sync detection isn't available for this vault type.")
            case .error(let message):
                errorState(message)
            case .loading:
                loadingState
            case .empty:
                LeafEmptyState(message: "No sync systems detected.")
            case .populated(let report):
                populated(report)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .navigationTitle("Sync")
    }

    // MARK: - Loading / error states

    private var loadingState: some View {
        VStack(spacing: Tokens.Spacing.sm) {
            Spacer()
            ProgressView()
            Spacer()
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Loading sync diagnostics")
    }

    private func errorState(_ message: String) -> some View {
        VStack(spacing: Tokens.Spacing.sm) {
            Spacer()
            Text(verbatim: "Could not load sync diagnostics: \(message)")
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .multilineTextAlignment(.center)
                .padding(.horizontal, Tokens.Spacing.md)
            Button("Retry") {
                appState.refreshSyncDiagnostics()
            }
            Spacer()
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    // MARK: - Populated

    private func populated(_ report: SyncDetectionReport) -> some View {
        LeafSection {
            header(count: report.providers.count)
        } content: {
            VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
                // Multi-sync warning row FIRST when present — it is the
                // single most consequential line in the report.
                if let warning = report.multiSyncWarning {
                    multiSyncWarningRow(warning)
                }
                ForEach(report.providers, id: \.kind) { provider in
                    providerRow(provider)
                }
                // LiveSync config section, only when that provider is
                // in the report (m_spec §M-3 point 3).
                if report.providers.contains(where: { $0.kind == .liveSync }),
                    let status = appState.liveSyncConfig
                {
                    liveSyncConfigSection(status)
                }
            }
        }
    }

    private func header(count: Int) -> some View {
        HStack(spacing: Tokens.Spacing.xs) {
            Text(verbatim: "Sync, \(count) \(count == 1 ? "system" : "systems") detected")
                .font(Tokens.Typography.sectionHeader)
                .accessibilityAddTraits(.isHeader)
            Spacer()
            // WCAG 2.5.3 label-in-name: the visible "Refresh" is a
            // contiguous prefix of the accessible name, so Voice
            // Control "click Refresh" matches.
            Button("Refresh") {
                appState.refreshSyncDiagnostics()
            }
            .font(Tokens.Typography.caption)
            .accessibilityLabel("Refresh sync diagnostics")
        }
    }

    private func multiSyncWarningRow(_ warning: String) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: Tokens.Spacing.xs) {
            // The existing `.warning` role (reuse, don't add a
            // non-fill twin — m_spec §M-3). Decorative: the row's
            // combined label below already says "Warning:".
            SlateSymbol.warning.decorative
                .foregroundStyle(Tokens.ColorRole.warningText)
            Text(verbatim: warning)
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.warningText)
                .fixedSize(horizontal: false, vertical: true)
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Warning: \(warning)")
    }

    private func providerRow(_ provider: DetectedSyncProvider) -> some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
            // Badge + name + recommendation combine into ONE AX
            // element with the normative row label; the evidence
            // disclosure stays a separate, operable sibling.
            VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
                HStack(spacing: Tokens.Spacing.xs) {
                    riskBadge(provider.riskLevel)
                    Text(verbatim: provider.displayName)
                        .font(Tokens.Typography.sectionHeader)
                }
                Text(verbatim: provider.recommendation)
                    .font(Tokens.Typography.body)
                    .foregroundStyle(Tokens.ColorRole.textPrimary)
                    .fixedSize(horizontal: false, vertical: true)
            }
            .accessibilityElement(children: .combine)
            .accessibilityLabel(
                "\(provider.displayName): \(riskText(provider.riskLevel)). \(provider.recommendation)"
            )

            DisclosureGroup("Evidence") {
                VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
                    ForEach(provider.evidencePaths, id: \.self) { path in
                        Text(verbatim: path)
                            .font(Tokens.Typography.code)
                            .textSelection(.enabled)
                    }
                }
                .padding(.leading, Tokens.Spacing.sm)
            }
            .font(Tokens.Typography.caption)
        }
        .padding(.vertical, Tokens.Spacing.xxs)
    }

    /// Risk badge — icon + text so the level reads by SHAPE and word,
    /// never color alone (m_spec §M-3). Colors ride the APCA-gated
    /// text roles: High → destructive family, Medium → `warningText`
    /// (U5-3's gated role), Low → `textSecondary`.
    @ViewBuilder
    private func riskBadge(_ risk: RiskLevel) -> some View {
        HStack(spacing: Tokens.Spacing.xxs) {
            switch risk {
            case .high:
                SlateSymbol.warning.decorative
                    .foregroundStyle(Tokens.ColorRole.destructiveText)
            case .medium:
                SlateSymbol.riskMedium.decorative
                    .foregroundStyle(Tokens.ColorRole.warningText)
            case .low:
                SlateSymbol.riskLow.decorative
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
            }
            Text(verbatim: riskText(risk))
                .font(Tokens.Typography.caption)
                .foregroundStyle(riskColor(risk))
        }
    }

    private func riskText(_ risk: RiskLevel) -> String {
        switch risk {
        case .high: return "High risk"
        case .medium: return "Medium risk"
        case .low: return "Low risk"
        }
    }

    private func riskColor(_ risk: RiskLevel) -> Color {
        switch risk {
        case .high: return Tokens.ColorRole.destructiveText
        case .medium: return Tokens.ColorRole.warningText
        case .low: return Tokens.ColorRole.textSecondary
        }
    }

    // MARK: - LiveSync config section

    @ViewBuilder
    private func liveSyncConfigSection(_ status: LiveSyncConfigStatus) -> some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
            Text(verbatim: "LiveSync configuration")
                .font(Tokens.Typography.sectionHeader)
                .accessibilityAddTraits(.isHeader)
            switch status {
            case .parsed(let config):
                configRow("Server host", config.serverHost ?? "Unknown")
                configRow("Database", config.database ?? "Unknown")
                configRow("Live sync", onOff(config.liveSyncEnabled))
                configRow("Sync on save", onOff(config.syncOnSave))
                configRow("Sync on start", onOff(config.syncOnStart))
                configRow("End-to-end encryption", onOff(config.endToEndEncryption))
            case .malformed(let reason):
                Text(verbatim: "LiveSync config could not be read: \(reason)")
                    .font(Tokens.Typography.body)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .fixedSize(horizontal: false, vertical: true)
            case .notPresent:
                Text(verbatim: "LiveSync plugin present; no config found.")
                    .font(Tokens.Typography.body)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
            }
        }
        .padding(.top, Tokens.Spacing.xs)
    }

    private func configRow(_ label: String, _ value: String) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: Tokens.Spacing.xs) {
            Text(verbatim: "\(label):")
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            Text(verbatim: value)
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textPrimary)
                .textSelection(.enabled)
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(label): \(value)")
    }

    /// Booleans render "On"/"Off"; absent (schema drift) renders
    /// "Unknown" (m_spec §M-3).
    private func onOff(_ value: Bool?) -> String {
        switch value {
        case .some(true): return "On"
        case .some(false): return "Off"
        case .none: return "Unknown"
        }
    }
}
