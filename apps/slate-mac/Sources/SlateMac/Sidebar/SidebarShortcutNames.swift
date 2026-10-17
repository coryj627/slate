// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// One row of the sidebar's Shortcuts section (#1332, plan 43 PR 25, owner
/// decision D6: converge to the Windows slot model).
///
/// A row IS its slot: it is identified by its 1-based position, never by
/// its target, so two slots on one note are two rows — two SwiftUI
/// identities, two names — and the ⌃-number bindings resolve by the same
/// position. `spokenName` is the row's accessibility label, composed among
/// all the rows by the R-4 shared-name rule (`SidebarShortcutNames`); a
/// slot's removal announces this same name, captured before the slot
/// leaves, so the announcement and the row it named never disagree.
struct SidebarShortcutRow: Identifiable, Equatable {
  let slot: Int
  let shortcut: SidebarShortcut
  let spokenName: String

  var id: Int { slot }

  /// The published slots as rows, in order.
  static func rows(for shortcuts: [SidebarShortcut]) -> [SidebarShortcutRow] {
    let names = SidebarShortcutNames.spokenNames(for: shortcuts)
    return shortcuts.enumerated().map { index, shortcut in
      SidebarShortcutRow(
        slot: index + 1, shortcut: shortcut, spokenName: names[index])
    }
  }

  /// The Shortcuts section's status line when decode stopped at its cap
  /// (`SidebarOrganizationSchema.maxDecodedShortcuts`): how many slots the
  /// file holds past it, none of which is shown. Nil when there are none.
  /// A visible row's text, read where the section's rows are read — not an
  /// announcement, the same kind of section copy as the empty section's
  /// placeholder row.
  static func overflowStatus(count: Int) -> String? {
    guard count > 0 else { return nil }
    return "Showing the first \(SidebarOrganizationSchema.maxDecodedShortcuts) "
      + "shortcuts. \(count) more \(CountCopy.noun(count, "isn't", "aren't")) shown."
  }
}

/// Contract R-4 on mac for the Shortcuts section: the twin of Windows'
/// `SiblingNames.Compose` (`apps/slate-windows/src/SlateWindows/
/// SiblingNames.cs`) as the `SidebarShortcuts` list declares it in
/// `MainWindow.xaml` — name `SidebarShortcutViewModel.AutomationName`,
/// distinguisher `Path`, noun "shortcut", no state, no prefix — with names
/// compared by Windows' `SpeechKey` (`SpeechKey.cs`). Lock-step with both:
/// a change to the rule changes both hosts, so one vault's shortcuts read
/// the same on each.
enum SidebarShortcutNames {
  /// The noun the ordinal pass takes (`SiblingNames.Noun`).
  static let noun = "shortcut"

  /// `SidebarShortcutViewModel.DisplayName`: a file or folder reads its
  /// last path component — a file WITH its extension, as Windows speaks
  /// it (the visible row keeps the stem, which the spoken name starts
  /// with). Tag and Untagged, kinds Windows does not show, read as their
  /// rows do.
  static func displayName(for shortcut: SidebarShortcut) -> String {
    switch shortcut.kind {
    case .file, .folder:
      return (shortcut.path as NSString).lastPathComponent
    case .tag, .untagged:
      return SidebarSectionsView.shortcutPresentation(for: shortcut).label
    }
  }

  /// `SidebarShortcutViewModel.AutomationName`: "{name}, {kind} shortcut".
  static func spokenName(for shortcut: SidebarShortcut) -> String {
    "\(displayName(for: shortcut)), "
      + SidebarSectionsView.shortcutPresentation(for: shortcut).value
  }

  /// What tells two alike rows apart before the ordinal does: a file or
  /// folder's path (`DistinguisherPath="Path"`). A tag or Untagged row has
  /// no path beyond its name, so a repeated one takes the ordinal at once.
  static func distinguisher(for shortcut: SidebarShortcut) -> String? {
    switch shortcut.kind {
    case .file, .folder: return shortcut.path
    case .tag, .untagged: return nil
    }
  }

  /// Every row's composed spoken name, in slot order.
  static func spokenNames(for shortcuts: [SidebarShortcut]) -> [String] {
    compose(
      names: shortcuts.map(spokenName(for:)),
      distinguishers: shortcuts.map(distinguisher(for:)),
      noun: noun)
  }

  /// `SiblingNames.Compose` without a state or a prefix. Each identity
  /// starts as its name, verbatim (a silent one reads "{Noun} {n}"), and
  /// grows only where siblings read alike: (1) colliding names add their
  /// distinguisher, "{name}, {distinguisher}"; (2) still-colliding ones add
  /// ", {noun} {n}", n the 1-based place among ALL the rows; (3) the final
  /// pass gives any name that still collides its ordinal, "{Noun} {n}".
  static func compose(
    names: [String], distinguishers: [String?], noun: String
  ) -> [String] {
    let capitalized =
      noun.isEmpty ? "Item" : noun.prefix(1).uppercased() + noun.dropFirst()
    func ordinal(_ index: Int) -> String { "\(capitalized) \(index + 1)" }
    var identity = names.indices.map {
      isSilent(names[$0]) ? ordinal($0) : names[$0]
    }
    var keys = identity.map(readAlikeKey)
    func rename(_ index: Int, to name: String) {
      identity[index] = name
      keys[index] = readAlikeKey(name)
    }
    for index in colliding(keys) {
      if index < distinguishers.count, let distinguisher = distinguishers[index],
        !isSilent(distinguisher)
      {
        rename(index, to: "\(identity[index]), \(distinguisher)")
      }
    }
    for index in colliding(keys) {
      rename(index, to: "\(identity[index]), \(noun) \(index + 1)")
    }
    // Ordinal names never read alike, so each pass settles at least one
    // more row: within `count` passes no two names collide.
    var pass = 0
    while pass <= names.count {
      let still = colliding(keys)
      if still.isEmpty { break }
      for index in still { rename(index, to: ordinal(index)) }
      pass += 1
    }
    return identity
  }

  /// `SpeechKey.Of`: what a name SOUNDS like — its letter runs and digit
  /// runs, after NFKC normalization and case folding, joined by single
  /// spaces. A reader speaks no case, no encoding, no whitespace between
  /// words and little punctuation, so "Note.md" and "note.md" read alike,
  /// as do "note-a" and "note a". A change between letters and digits
  /// starts a new run ("Note1" reads like "Note 1"); a decimal digit of
  /// any script is its value; a combining mark stays with the letter it
  /// sits on; a format character is silent and splits nothing.
  static func readAlikeKey(_ text: String) -> String {
    guard !text.isEmpty else { return "" }
    let folded = text.precomposedStringWithCompatibilityMapping.lowercased()
      .precomposedStringWithCompatibilityMapping
    enum Run { case none, letters, digits }
    let space: Unicode.Scalar = " "
    var key = String.UnicodeScalarView()
    var previous = Run.none
    for scalar in folded.unicodeScalars {
      let category = scalar.properties.generalCategory
      switch category {
      case .format:
        continue
      case .nonspacingMark, .spacingMark, .enclosingMark:
        if previous != .none { key.append(scalar) }
      case .uppercaseLetter, .lowercaseLetter, .titlecaseLetter,
        .modifierLetter, .otherLetter:
        if previous != .letters, !key.isEmpty { key.append(space) }
        key.append(scalar)
        previous = .letters
      case .decimalNumber, .letterNumber, .otherNumber:
        if previous != .digits, !key.isEmpty { key.append(space) }
        if category == .decimalNumber,
          let value = scalar.properties.numericValue,
          (0.0...9.0).contains(value)
        {
          key.append(Unicode.Scalar(UInt8(48 + Int(value))))
        } else {
          key.append(scalar)
        }
        previous = .digits
      default:
        previous = .none
      }
    }
    return String(key)
  }

  /// `SpeechKey.IsSilent`: a name of nothing but whitespace, control and
  /// format characters says nothing at all — it is no name.
  static func isSilent(_ text: String?) -> Bool {
    guard let text, !text.isEmpty else { return true }
    return text.unicodeScalars.allSatisfy { scalar in
      scalar.properties.isWhitespace
        || scalar.properties.generalCategory == .control
        || scalar.properties.generalCategory == .format
    }
  }

  /// Every index whose key another index shares, ascending.
  private static func colliding(_ keys: [String]) -> [Int] {
    var counts: [String: Int] = [:]
    for key in keys { counts[key, default: 0] += 1 }
    return keys.indices.filter { counts[keys[$0], default: 0] > 1 }
  }
}
