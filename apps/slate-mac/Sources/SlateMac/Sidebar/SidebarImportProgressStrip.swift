// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

enum SidebarImportProgressPhase: Equatable {
    case importing
    case cancelling
    case moving
    case finishing
}

/// Admission-owned import progress. The denominator is immutable for the
/// lifetime of one batch; terminal provider outcomes can only move completion
/// forward, never change the meaning of an earlier progress value.
@MainActor
final class SidebarImportProgressModel: ObservableObject {
    let totalProviderCount: Int
    @Published private(set) var completedProviderCount: Int
    @Published private(set) var phase: SidebarImportProgressPhase = .importing
    @Published private(set) var canRequestCancellation = true

    init(
        admittedProviderCount: Int,
        completedProviderCount: Int = 0
    ) {
        precondition(admittedProviderCount > 0)
        totalProviderCount = admittedProviderCount
        self.completedProviderCount = min(
            max(0, completedProviderCount),
            admittedProviderCount)
    }

    func recordCompletedProviderCount(_ count: Int) {
        let bounded = min(max(0, count), totalProviderCount)
        completedProviderCount = max(completedProviderCount, bounded)
    }

    func setPhase(_ phase: SidebarImportProgressPhase) {
        self.phase = phase
    }

    func setCancellationAvailability(_ available: Bool) {
        canRequestCancellation = available
    }

    var accessibilityValue: String {
        "\(completedProviderCount.formatted()) of \(totalProviderCount.formatted())"
    }

    var hasRemainingProviders: Bool {
        completedProviderCount < totalProviderCount
    }
}

/// Shared by the visible button and both standard cancel-command deliveries.
/// Marking the request before invoking the callback also closes re-entrant
/// double delivery.
struct SidebarImportCancellationGate {
    private(set) var isCancellationRequested = false

    mutating func request(_ action: () -> Void) {
        guard !isCancellationRequested else { return }
        isCancellationRequested = true
        action()
    }
}

/// Compact, native macOS progress for one admitted Finder import batch.
struct SidebarImportProgressStrip: View {
    static func title(
        phase: SidebarImportProgressPhase,
        hasRemainingProviders: Bool
    ) -> String {
        switch phase {
        case .importing:
            return hasRemainingProviders ? "Importing…" : "Finishing import…"
        case .cancelling:
            return "Cancelling import…"
        case .moving:
            return "Moving items…"
        case .finishing:
            return "Finishing import…"
        }
    }
    static let cancelAccessibilityHint =
        "Stops remaining imports. Completed copies remain in the vault."
    static let noImportInProgressHint = "No import is in progress."

    static func cancellationHint(
        phase: SidebarImportProgressPhase,
        available: Bool
    ) -> String {
        guard !available else { return cancelAccessibilityHint }
        switch phase {
        case .cancelling:
            return "Cancellation requested. Completed copies remain."
        case .moving:
            return "The in-vault move is already being applied and can’t be cancelled."
        case .finishing, .importing:
            return "The import is being finalized and can’t be cancelled."
        }
    }

    @ObservedObject var progress: SidebarImportProgressModel
    let onCancel: () -> Void

    @State private var cancellationGate = SidebarImportCancellationGate()

    var body: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.xs) {
            HStack(alignment: .firstTextBaseline, spacing: Tokens.Spacing.sm) {
                ProgressView()
                    .controlSize(.mini)
                    .accessibilityHidden(true)
                Text(Self.title(
                    phase: progress.phase,
                    hasRemainingProviders: progress.hasRemainingProviders))
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textPrimary)
                Spacer(minLength: Tokens.Spacing.sm)
                Text(progress.accessibilityValue)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .monospacedDigit()
                    .accessibilityHidden(true)
            }

            HStack(spacing: Tokens.Spacing.sm) {
                ProgressView(
                    value: Double(progress.completedProviderCount),
                    total: Double(progress.totalProviderCount)
                )
                .progressViewStyle(.linear)
                .accessibilityLabel("Import progress")
                .accessibilityValue(progress.accessibilityValue)

                Button("Cancel") {
                    requestCancellation()
                }
                .controlSize(.small)
                .keyboardShortcut(.cancelAction)
                .disabled(
                    cancellationGate.isCancellationRequested
                        || !progress.canRequestCancellation)
                .accessibilityHint(Self.cancellationHint(
                    phase: progress.phase,
                    available: progress.canRequestCancellation))
                .help(Self.cancellationHint(
                    phase: progress.phase,
                    available: progress.canRequestCancellation))
            }
        }
        .padding(.horizontal, Tokens.Spacing.md)
        .padding(.vertical, Tokens.Spacing.xs)
        .accessibilityElement(children: .contain)
        // Exit-command delivery covers Escape while focus remains in this
        // sidebar subtree. Command-period is owned by the app's File menu so it
        // remains available regardless of current focus.
        .onExitCommand {
            requestCancellation()
        }
    }

    private func requestCancellation() {
        guard progress.canRequestCancellation else { return }
        cancellationGate.request(onCancel)
    }
}
