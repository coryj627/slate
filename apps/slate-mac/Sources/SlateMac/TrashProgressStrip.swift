// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Window-owned progress remains reachable when Trash originates from the
/// menu or palette with the Files sidebar hidden.
struct TrashProgressStrip: View {
    static let cancellationHint =
        "Stops remaining work. The current system operation may finish. Completed items remain in Trash."

    let progress: AppState.TrashProgress
    let onCancel: () -> Void

    private var isStopping: Bool { progress.phase == .stopping }

    var body: some View {
        HStack(spacing: Tokens.Spacing.sm) {
            ProgressView()
                .controlSize(.small)
                .accessibilityHidden(true)
            Text(progress.phase.title)
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .accessibilityLabel(progress.phase.title)
            Spacer(minLength: Tokens.Spacing.sm)
            Button("Cancel Trash") { requestCancellation() }
                .keyboardShortcut(".", modifiers: [.command])
                .disabled(isStopping)
                .accessibilityHint(Self.cancellationHint)
                .help(Self.cancellationHint)
        }
        .padding(Tokens.Spacing.sm)
        .onExitCommand { requestCancellation() }
    }

    private func requestCancellation() {
        guard !isStopping else { return }
        onCancel()
    }
}
