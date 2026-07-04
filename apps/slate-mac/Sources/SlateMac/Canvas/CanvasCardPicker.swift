// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// What a card pick is for (#522; #523 reuses the same component for
/// its connect flow — one picker, one interaction model).
enum CanvasCardPickerPurpose: Equatable {
    case placeBelow
    case placeRightOf
    case placeAbove
    case placeLeftOf
    case alignWith

    var title: String {
        switch self {
        case .placeBelow: return "Place Below"
        case .placeRightOf: return "Place Right Of"
        case .placeAbove: return "Place Above"
        case .placeLeftOf: return "Place Left Of"
        case .alignWith: return "Align With"
        }
    }

    var directionHint: CanvasPlaceDirection? {
        switch self {
        case .placeBelow: return .below
        case .placeRightOf: return .rightOf
        case .placeAbove: return .above
        case .placeLeftOf: return .leftOf
        case .alignWith: return nil
        }
    }
}

/// A pending card-picker request (sheet-presented, M6 visible control).
struct CanvasCardPickerRequest: Identifiable, Equatable {
    let id = UUID()
    let purpose: CanvasCardPickerPurpose

    static func == (lhs: Self, rhs: Self) -> Bool { lhs.id == rhs.id }
}

/// The reusable card picker (#522): command-palette interaction model —
/// type to filter, arrows to move, Return to pick. Candidates sort by
/// **proximity to the anchor card's center** (model geometry, t4 spec
/// pin); rows read "⟨Type⟩ card '⟨title⟩', in ⟨group‖canvas⟩".
struct CanvasCardPicker: View {
    @EnvironmentObject var appState: AppState
    @ObservedObject var document: CanvasDocument
    let purpose: CanvasCardPickerPurpose
    /// Node ids excluded from candidacy (the moving card / marked set).
    let excluded: Set<String>
    let onPick: (String) -> Void

    @State private var query = ""
    @State private var highlighted: String?
    @FocusState private var filterFocused: Bool

    struct Candidate: Identifiable {
        let id: String
        let label: String
        let distance: Double
    }

    /// Proximity-ordered candidates (the t4 pin), stable-tied by
    /// reading order via the outline sequence.
    var candidates: [Candidate] {
        let anchorId = document.selection.selected
        let anchorCenter: (Double, Double)? = anchorId
            .flatMap { id in document.scene.nodes.first { $0.nodeId == id } }
            .map { ($0.x + $0.width / 2, $0.y + $0.height / 2) }
        var out: [Candidate] = []
        for row in document.outline where !excluded.contains(row.nodeId) {
            guard row.nodeId != anchorId else { continue }
            guard let node = document.scene.nodes.first(where: { $0.nodeId == row.nodeId })
            else { continue }
            let label = pickerLabel(row)
            if !query.isEmpty,
                !label.localizedCaseInsensitiveContains(query)
            {
                continue
            }
            let distance: Double
            if let (ax, ay) = anchorCenter {
                let dx = node.x + node.width / 2 - ax
                let dy = node.y + node.height / 2 - ay
                distance = (dx * dx + dy * dy).squareRoot()
            } else {
                distance = Double(out.count)  // no anchor: reading order
            }
            out.append(Candidate(id: row.nodeId, label: label, distance: distance))
        }
        return out.sorted { $0.distance < $1.distance }
    }

    private func pickerLabel(_ row: CanvasOutlineRow) -> String {
        let type = row.kind == "group" ? "Group" : "\(row.kind.capitalized) card"
        return "\(type) \"\(row.title)\", in \(row.groupPath.last ?? "canvas")"
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            Text("\(purpose.title)…")
                .font(Tokens.Typography.body.weight(.semibold))
            TextField("Filter cards", text: $query)
                .textFieldStyle(.roundedBorder)
                .focused($filterFocused)
                .accessibilityLabel("Filter cards")
                .accessibilityHint(
                    "Type to narrow the list. Cards are ordered nearest-first from the selected card."
                )
                .onSubmit { pickHighlightedOrFirst() }
            List(candidates, selection: $highlighted) { candidate in
                Text(candidate.label)
                    .accessibilityLabel(candidate.label)
                    .accessibilityAddTraits(.isButton)
                    .onTapGesture(count: 2) { pick(candidate.id) }
                    .tag(candidate.id)
            }
            .frame(minHeight: 180, maxHeight: 280)
            .accessibilityLabel("Cards, nearest first")
            HStack {
                Spacer()
                Button("Cancel") { appState.canvasCardPicker = nil }
                    .keyboardShortcut(.cancelAction)
                Button(purpose.title) { pickHighlightedOrFirst() }
                    .keyboardShortcut(.defaultAction)
                    .disabled(candidates.isEmpty)
            }
        }
        .padding(Tokens.Spacing.lg)
        .frame(minWidth: 380)
        .accessibilityElement(children: .contain)
        .accessibilityLabel("\(purpose.title): pick a card")
        .onAppear { filterFocused = true }
    }

    private func pickHighlightedOrFirst() {
        if let highlighted, candidates.contains(where: { $0.id == highlighted }) {
            pick(highlighted)
        } else if let first = candidates.first {
            pick(first.id)
        }
    }

    private func pick(_ nodeId: String) {
        appState.canvasCardPicker = nil
        onPick(nodeId)
    }
}
