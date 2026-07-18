// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

enum SidebarImportProgressPhase: Equatable {
    case importing
    case cancelling
    case moving
    case finishing
}

/// Deterministic count copy for Slate's current English-only UI contract.
/// `String(Int)` intentionally keeps the digits ASCII and ungrouped so they
/// aren't locale-formatted beside the still-English word "of". When string
/// catalogs land, localize this entire phrase atomically instead of formatting
/// either integer independently.
enum SidebarImportProgressCountText {
    static func make(
        completedProviderCount: Int,
        totalProviderCount: Int
    ) -> String {
        String(completedProviderCount)
            + " of "
            + String(totalProviderCount)
    }
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
        SidebarImportProgressCountText.make(
            completedProviderCount: completedProviderCount,
            totalProviderCount: totalProviderCount)
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

            SidebarImportProgressControls(
                completedProviderCount: progress.completedProviderCount,
                totalProviderCount: progress.totalProviderCount,
                cancellationEnabled:
                    !cancellationGate.isCancellationRequested
                        && progress.canRequestCancellation,
                cancellationHint: Self.cancellationHint(
                    phase: progress.phase,
                    available: progress.canRequestCancellation),
                onCancel: requestCancellation)
                .frame(maxWidth: .infinity)
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

/// Defensive values at the AppKit progress-control boundary. Production
/// callers arrive through `SidebarImportProgressModel`, whose positive-total
/// precondition remains the public invariant; this last-mile clamp prevents an
/// invalid future caller from giving `NSProgressIndicator` an inverted range
/// or an out-of-range value, while VoiceOver still gets coherent semantic
/// counts.
struct SidebarImportProgressControlValues: Equatable {
    let normalizedCompletedProviderCount: Int
    let normalizedTotalProviderCount: Int
    let progressIndicatorMaximum: Int

    var accessibilityValue: String {
        SidebarImportProgressCountText.make(
            completedProviderCount: normalizedCompletedProviderCount,
            totalProviderCount: normalizedTotalProviderCount)
    }

    init(
        completedProviderCount: Int,
        totalProviderCount: Int
    ) {
        let normalizedTotalProviderCount = max(0, totalProviderCount)
        self.normalizedCompletedProviderCount = min(
            max(0, completedProviderCount), normalizedTotalProviderCount)
        self.normalizedTotalProviderCount = normalizedTotalProviderCount
        progressIndicatorMaximum = max(1, normalizedTotalProviderCount)
    }
}

/// Native controls keep the determinate value and cancellation action
/// inspectable through AppKit as well as VoiceOver. SwiftUI's virtualized
/// controls don't expose a stable hosted view subtree, which makes it
/// impossible to exercise the real press/update behavior in-process.
private struct SidebarImportProgressControls: NSViewRepresentable {
    let completedProviderCount: Int
    let totalProviderCount: Int
    let cancellationEnabled: Bool
    let cancellationHint: String
    let onCancel: () -> Void

    func makeNSView(context: Context) -> ControlsView {
        ControlsView()
    }

    func updateNSView(_ nsView: ControlsView, context: Context) {
        nsView.update(
            completedProviderCount: completedProviderCount,
            totalProviderCount: totalProviderCount,
            cancellationEnabled: cancellationEnabled,
            cancellationHint: cancellationHint,
            onCancel: onCancel)
    }

    final class ControlsView: NSStackView {
        let progressIndicator = NSProgressIndicator()
        let cancelButton = NSButton()

        private var onCancel: () -> Void = {}

        override init(frame frameRect: NSRect) {
            super.init(frame: frameRect)
            orientation = .horizontal
            alignment = .centerY
            spacing = Tokens.Spacing.sm
            distribution = .fill

            progressIndicator.style = .bar
            progressIndicator.controlSize = .small
            progressIndicator.isIndeterminate = false
            progressIndicator.minValue = 0
            progressIndicator.setAccessibilityRole(.progressIndicator)
            progressIndicator.setAccessibilityIdentifier("sidebar.import.progress")
            progressIndicator.setAccessibilityLabel("Import progress")

            cancelButton.title = "Cancel"
            cancelButton.bezelStyle = .rounded
            cancelButton.controlSize = .small
            cancelButton.target = self
            cancelButton.action = #selector(cancelPressed)
            cancelButton.keyEquivalent = "\u{1b}"
            cancelButton.keyEquivalentModifierMask = []
            cancelButton.setAccessibilityRole(.button)
            cancelButton.setAccessibilityIdentifier("sidebar.import.cancel")
            cancelButton.setAccessibilityLabel("Cancel")

            progressIndicator.setContentHuggingPriority(.defaultLow, for: .horizontal)
            progressIndicator.setContentCompressionResistancePriority(
                .defaultLow,
                for: .horizontal)
            cancelButton.setContentHuggingPriority(.required, for: .horizontal)
            addArrangedSubview(progressIndicator)
            addArrangedSubview(cancelButton)
        }

        @available(*, unavailable)
        required init?(coder: NSCoder) {
            nil
        }

        override var intrinsicContentSize: NSSize {
            NSSize(
                width: NSView.noIntrinsicMetric,
                height: max(
                    progressIndicator.intrinsicContentSize.height,
                    cancelButton.intrinsicContentSize.height))
        }

        func update(
            completedProviderCount: Int,
            totalProviderCount: Int,
            cancellationEnabled: Bool,
            cancellationHint: String,
            onCancel: @escaping () -> Void
        ) {
            let progressValues = SidebarImportProgressControlValues(
                completedProviderCount: completedProviderCount,
                totalProviderCount: totalProviderCount)
            progressIndicator.maxValue = Double(
                progressValues.progressIndicatorMaximum)
            progressIndicator.doubleValue = Double(
                progressValues.normalizedCompletedProviderCount)
            progressIndicator.setAccessibilityValueDescription(
                progressValues.accessibilityValue)

            cancelButton.isEnabled = cancellationEnabled
            cancelButton.toolTip = cancellationHint
            cancelButton.setAccessibilityHelp(cancellationHint)
            self.onCancel = onCancel
        }

        @objc private func cancelPressed() {
            onCancel()
        }
    }
}
