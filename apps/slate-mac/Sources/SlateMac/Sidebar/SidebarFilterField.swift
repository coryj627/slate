// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// FL4-2 (#663): the persistent filter field — the topmost sidebar
/// control, above Shortcuts, Recents, and the tree. Always mounted, so
/// it also hosts the day-rollover / time-zone observers that keep an
/// active date-term query honest across a DST transition (spec rule 7).
struct SidebarFilterField: View {
  @ObservedObject var model: SidebarFilterModel
  @FocusState.Binding var isFocused: Bool
  /// ↓ from the field enters the result list at row 1 (spec rule 3).
  var moveFocusToResults: () -> Void

  /// Discoverability menu (spec rule 1): each entry inserts the
  /// operator into the field as a fresh term and leaves the caret in
  /// the field; the ordinary debounce applies.
  private static let operators: [(insert: String, label: String)] = [
    ("#", "# — tag"),
    ("@today", "@today — modified today"),
    ("@yesterday", "@yesterday — modified yesterday"),
    ("@last7d", "@last7d — modified in the last 7 days"),
    ("@last30d", "@last30d — modified in the last 30 days"),
    ("has:task", "has:task — has open tasks"),
    ("ext:", "ext: — file extension"),
    ("path:", "path: — folder path"),
    ("-", "- — exclude the next term"),
  ]

  var body: some View {
    VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
      HStack(spacing: Tokens.Spacing.xs) {
        SlateSymbol.search.decorative
          .foregroundStyle(Tokens.ColorRole.textSecondary)
        TextField("Filter", text: $model.fieldText)
          .textFieldStyle(.plain)
          .font(Tokens.Typography.body)
          .focused($isFocused)
          .onSubmit { model.commitNow() }
          .onExitCommand { model.escapeInField() }
          .onMoveCommand { direction in
            guard direction == .down, model.isActive else { return }
            moveFocusToResults()
          }
          .accessibilityLabel("Filter files")
          .accessibilityHint(
            "Narrows the sidebar to matching files. Down arrow enters the results; Escape clears.")
        if !model.fieldText.isEmpty {
          Button {
            model.escapeInField()
          } label: {
            SlateSymbol.clearSearch.decorative
              .foregroundStyle(Tokens.ColorRole.textSecondary)
          }
          .buttonStyle(.borderless)
          .accessibilityLabel("Clear filter")
        }
        Menu {
          ForEach(Self.operators, id: \.insert) { entry in
            Button(entry.label) {
              model.insertOperator(entry.insert)
              isFocused = true
            }
          }
        } label: {
          SlateSymbol.moreActions.decorative
            .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .menuStyle(.borderlessButton)
        .menuIndicator(.hidden)
        .fixedSize()
        .accessibilityLabel("Insert filter operator")
        .accessibilityHint("Inserts a tag, date, task, extension, or path term.")
      }
      .padding(.horizontal, Tokens.Spacing.sm)
      .padding(.vertical, Tokens.Spacing.xs)
      .background(
        RoundedRectangle(cornerRadius: Tokens.Radius.control)
          .fill(Tokens.ColorRole.surfaceSecondary))
      .overlay(
        RoundedRectangle(cornerRadius: Tokens.Radius.control)
          .strokeBorder(
            isFocused
              ? Color(nsColor: .keyboardFocusIndicatorColor)
              : Tokens.ColorRole.separator,
            lineWidth: 1))
      if let error = model.inlineError {
        // Parse errors render inline naming the bad term while the
        // previous good results stay visible (spec rule 4). Polite
        // announcement channel = the model's announce seam is NOT used
        // here; the text itself is the AX element VoiceOver reads when
        // it lands, and updates re-post through the live text change.
        HStack(alignment: .top, spacing: Tokens.Spacing.xs) {
          SlateSymbol.warning.decorative
            .foregroundStyle(Tokens.ColorRole.warningText)
          Text(error)
            .font(Tokens.Typography.caption)
            .foregroundStyle(Tokens.ColorRole.warningText)
            .fixedSize(horizontal: false, vertical: true)
        }
        .accessibilityElement(children: .combine)
      }
    }
    .padding(.horizontal, Tokens.Spacing.sm)
    .padding(.vertical, Tokens.Spacing.xs)
    .onChange(of: model.fieldText) { _, _ in
      model.fieldTextChanged()
    }
    .onReceive(
      NotificationCenter.default
        .publisher(for: .NSCalendarDayChanged)
        .receive(on: RunLoop.main)
    ) { _ in
      model.handleDayRolloverOrTimeZoneChange()
    }
    .onReceive(
      NotificationCenter.default
        .publisher(for: NSNotification.Name.NSSystemTimeZoneDidChange)
        .receive(on: RunLoop.main)
    ) { _ in
      model.handleDayRolloverOrTimeZoneChange()
    }
  }
}
