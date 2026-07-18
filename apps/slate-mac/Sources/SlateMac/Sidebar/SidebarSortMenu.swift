// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Shared renderer for the FL-06 sort/group command set (#658). One item
/// builder serves the View menu, the folder context menu, and the sidebar
/// toolbar so every surface projects the same catalog verbs with the same
/// radio state, disabled reasons, and AppState dispatch funnel.
struct SidebarSortMenuItems: View {
  /// Evaluations from the rendering surface's own catalog projection —
  /// context menus pass their concise row-targeted projection (unavailable
  /// verbs already omitted); menu bar and toolbar pass the full inventory
  /// (unavailable verbs disabled with their reason).
  let evaluations: [SidebarActionEvaluation]
  /// The target container's effective, normalized choice — drives the radio
  /// checkmarks truthfully (a grouped name sort reads as its forced date
  /// sort, because that is what the tree actually shows).
  let effectiveChoice: SidebarOrganizationChoice
  let dispatch: (SidebarActionInvocationIntent) -> Void

  private static let sortOptions: [(id: String, option: SidebarSortOption)] = [
    (SlateCommandID.sidebarSortNameAsc, SidebarSortOption(field: .name, direction: .asc)),
    (SlateCommandID.sidebarSortNameDesc, SidebarSortOption(field: .name, direction: .desc)),
    (
      SlateCommandID.sidebarSortCreatedDesc,
      SidebarSortOption(field: .created, direction: .desc)
    ),
    (
      SlateCommandID.sidebarSortCreatedAsc,
      SidebarSortOption(field: .created, direction: .asc)
    ),
    (
      SlateCommandID.sidebarSortModifiedDesc,
      SidebarSortOption(field: .modified, direction: .desc)
    ),
    (
      SlateCommandID.sidebarSortModifiedAsc,
      SidebarSortOption(field: .modified, direction: .asc)
    ),
  ]

  var body: some View {
    ForEach(Self.sortOptions, id: \.id) { entry in
      if let evaluation = evaluations.first(where: { $0.id == entry.id }) {
        Toggle(isOn: radioBinding(for: evaluation, option: entry.option)) {
          Text(evaluation.label)
        }
        .disabled(evaluation.disabledReason != nil)
        .help(evaluation.disabledReason ?? evaluation.definition.accessibilityHint)
        .accessibilityHint(
          evaluation.disabledReason ?? evaluation.definition.accessibilityHint)
      }
    }
    if let grouping = evaluations.first(
      where: { $0.id == SlateCommandID.sidebarToggleDateGrouping })
    {
      Divider()
      Toggle(isOn: groupingBinding(for: grouping)) {
        Text(grouping.label)
      }
      .disabled(grouping.disabledReason != nil)
      .help(grouping.disabledReason ?? grouping.definition.accessibilityHint)
      .accessibilityHint(
        grouping.disabledReason ?? grouping.definition.accessibilityHint)
    }
    if let useDefault = evaluations.first(
      where: { $0.id == SlateCommandID.sidebarUseVaultDefaultSort })
    {
      Divider()
      Button {
        guard let intent = useDefault.intent else { return }
        dispatch(intent)
      } label: {
        Text(useDefault.label)
      }
      .disabled(useDefault.disabledReason != nil)
      .help(useDefault.disabledReason ?? useDefault.definition.accessibilityHint)
      .accessibilityHint(
        useDefault.disabledReason ?? useDefault.definition.accessibilityHint)
    }
  }

  /// Radio semantics: setting an already-checked option re-dispatches (a
  /// harmless idempotent write); unchecking is not a menu gesture. The
  /// binding never mutates state directly — the catalog intent is the only
  /// mutation path.
  private func radioBinding(
    for evaluation: SidebarActionEvaluation, option: SidebarSortOption
  ) -> Binding<Bool> {
    Binding(
      get: { effectiveChoice.sort == option },
      set: { _ in
        guard let intent = evaluation.intent else { return }
        dispatch(intent)
      })
  }

  private func groupingBinding(
    for evaluation: SidebarActionEvaluation
  ) -> Binding<Bool> {
    Binding(
      get: { effectiveChoice.grouping == .dateBuckets },
      set: { _ in
        guard let intent = evaluation.intent else { return }
        dispatch(intent)
      })
  }
}

/// The View-menu home for the sort set (Finder's View ▸ Sort By convention).
/// Targets the published selection's container and keeps the stable full
/// inventory, disabling unavailable verbs with their one deterministic reason.
struct SidebarSortMenu: View {
  @EnvironmentObject private var appState: AppState

  var body: some View {
    Menu("Sort Sidebar By") {
      SidebarSortMenuItems(
        evaluations: appState.sidebarActionProjection(surface: .menuBar),
        effectiveChoice: appState.sidebarOrganizationMenuTargetChoice,
        dispatch: { intent in
          do {
            _ = try appState.dispatchSidebarAction(intent)
          } catch {
            appState.postMutationAnnouncement(error.sidebarActionAnnouncement)
          }
        })
    }
    .disabled(!appState.isVaultOpen)
  }
}
