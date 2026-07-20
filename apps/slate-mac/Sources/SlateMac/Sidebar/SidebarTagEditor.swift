// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// FL5-3b (#666): the Add Tag… / Remove Tag… editor sheet. Add offers
/// autocomplete over the vault's tag inventory with free entry allowed;
/// Remove offers the frozen selection's own tags with distinct-file
/// counts (DB truth — the app never parses note bodies). Commit runs
/// the batch API and announces ONE consolidated result through
/// AppState's mutation funnel.
struct SidebarTagEditor: View {
  @EnvironmentObject private var appState: AppState
  let request: AppState.SidebarTagEditorRequest

  @State private var tagText = ""
  @State private var selectionTags: [TagCount] = []
  @State private var suggestions: [String] = []
  @FocusState private var fieldFocused: Bool

  private var isAdd: Bool { request.kind == .add }

  private var fileCountLabel: String {
    CountCopy.counted(request.paths.count, "file", "files")
  }

  /// Prefix-matched suggestions for Add; empty text shows the whole
  /// inventory (bounded below for render sanity).
  private var visibleSuggestions: [String] {
    let needle = normalizedInput
    let matches =
      needle.isEmpty
      ? suggestions
      : suggestions.filter { $0.hasPrefix(needle) && $0 != needle }
    return Array(matches.prefix(12))
  }

  private var normalizedInput: String {
    let trimmed = tagText.trimmingCharacters(in: .whitespaces)
    return (trimmed.hasPrefix("#") ? String(trimmed.dropFirst()) : trimmed)
      .lowercased()
  }

  var body: some View {
    VStack(alignment: .leading, spacing: Tokens.Spacing.md) {
      Text(isAdd ? "Add Tag to \(fileCountLabel)" : "Remove Tag from \(fileCountLabel)")
        .font(Tokens.Typography.sectionHeader)
        .accessibilityAddTraits(.isHeader)

      if isAdd {
        TextField("Tag", text: $tagText)
          .textFieldStyle(.roundedBorder)
          .focused($fieldFocused)
          .onSubmit(commit)
          .accessibilityLabel("Tag to add")
          .accessibilityHint(
            "Type a tag, or pick a suggestion. Slashes nest, like projects/reading.")
        if !visibleSuggestions.isEmpty {
          VStack(alignment: .leading, spacing: 0) {
            ForEach(visibleSuggestions, id: \.self) { suggestion in
              Button {
                tagText = suggestion
              } label: {
                HStack(spacing: Tokens.Spacing.xs) {
                  SlateSymbol.tag.decorative
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                  Text(suggestion)
                    .font(Tokens.Typography.body)
                  Spacer(minLength: 0)
                }
                .contentShape(Rectangle())
              }
              .buttonStyle(.plain)
              .padding(.vertical, Tokens.Spacing.xxs)
              .accessibilityHint("Fills the tag field.")
            }
          }
        }
      } else {
        if selectionTags.isEmpty {
          Text("The selected files carry no tags.")
            .font(Tokens.Typography.caption)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
        } else {
          VStack(alignment: .leading, spacing: 0) {
            ForEach(selectionTags, id: \.tag) { count in
              Button {
                tagText = count.tag
              } label: {
                HStack(spacing: Tokens.Spacing.xs) {
                  SlateSymbol.tag.decorative
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                  Text(count.tag)
                    .font(Tokens.Typography.body)
                    .fontWeight(tagText == count.tag ? .semibold : .regular)
                  Spacer(minLength: 0)
                  Text(CountCopy.counted(count.fileCount, "file", "files"))
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                }
                .contentShape(Rectangle())
              }
              .buttonStyle(.plain)
              .padding(.vertical, Tokens.Spacing.xxs)
              .accessibilityLabel(count.tag)
              .accessibilityValue(
                CountCopy.counted(count.fileCount, "file", "files"))
              .accessibilityHint("Selects this tag for removal.")
            }
          }
        }
      }

      HStack {
        Spacer(minLength: 0)
        Button("Cancel") {
          appState.sidebarTagEditorRequest = nil
        }
        .keyboardShortcut(.cancelAction)
        Button(isAdd ? "Add Tag" : "Remove Tag", action: commit)
          .keyboardShortcut(.defaultAction)
          .disabled(normalizedInput.isEmpty)
      }
    }
    .padding(Tokens.Spacing.lg)
    .frame(minWidth: 320)
    .onAppear {
      if isAdd {
        suggestions = appState.sidebarTagSuggestions()
        fieldFocused = true
      } else {
        let paths = request.paths
        Task { @MainActor in
          selectionTags = await appState.sidebarSelectionTags(for: paths)
        }
      }
    }
  }

  private func commit() {
    let tag = normalizedInput
    guard !tag.isEmpty else { return }
    appState.commitSidebarTagEdit(request: request, tag: tag)
  }
}
