// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// FL3-3 (#660): vault-local shortcuts schema — decode, exact-op
/// mutations against the RAW authored array, reserved-kind preservation,
/// and structural-transform integrity.
final class SidebarShortcutsTests: XCTestCase {
  private func entry(_ kind: String, _ path: String) -> [String: Any] {
    ["kind": kind, "path": path]
  }

  func testDecodeKeepsKnownKindsSkipsReservedAndDedupes() {
    let root: [String: Any] = [
      "shortcuts": [
        entry("file", "Projects/a.md"),
        entry("tag", "research"),
        entry("folder", "Projects"),
        entry("file", "Projects/a.md"),
        entry("untagged", ""),
        ["kind": "file"],
      ]
    ]
    XCTAssertEqual(
      SidebarOrganizationSchema.decodeShortcuts(root: root),
      [
        SidebarShortcut(kind: .file, path: "Projects/a.md"),
        SidebarShortcut(kind: .folder, path: "Projects"),
      ])
  }

  func testShapeGuardAcceptsReservedKindsAndRejectsMalformedEntries() {
    XCTAssertTrue(
      SidebarOrganizationSchema.knownSectionShapesAreValid(root: [
        "shortcuts": [
          entry("file", "a.md"), entry("tag", "x"), entry("untagged", ""),
        ]
      ]))
    XCTAssertFalse(
      SidebarOrganizationSchema.knownSectionShapesAreValid(root: [
        "shortcuts": ["not an object"]
      ]))
    XCTAssertFalse(
      SidebarOrganizationSchema.knownSectionShapesAreValid(root: [
        "shortcuts": [["kind": "file"]]
      ]))
    XCTAssertFalse(
      SidebarOrganizationSchema.knownSectionShapesAreValid(root: [
        "shortcuts": "nope"
      ]))
    XCTAssertFalse(
      SidebarOrganizationSchema.knownSectionShapesAreValid(root: [
        "shortcuts": Array(
          repeating: entry("file", "a.md"),
          count: SidebarOrganizationSchema.maxShortcuts + 1)
      ]))
    XCTAssertFalse(
      SidebarOrganizationSchema.knownSectionShapesAreValid(root: [
        "shortcuts": [
          entry("file", String(repeating: "a", count: 4_097))
        ]
      ]))
    XCTAssertTrue(
      SidebarOrganizationSchema.knownSectionShapesAreValid(root: [
        "shortcuts": Array(
          repeating: entry("file", "a.md"),
          count: SidebarOrganizationSchema.maxShortcuts)
      ]))
  }

  func testAddRemoveMutateOnlyTheirEntryAndPreserveEverythingElse() {
    var root: [String: Any] = [
      "version": 1,
      "future": ["x": true],
      "shortcuts": [
        entry("tag", "research"),
        ["kind": "file", "path": "Projects/a.md", "color": "red"],
      ],
    ]
    SidebarOrganizationSchema.addShortcut(
      &root, kind: "folder", path: "Projects")
    SidebarOrganizationSchema.addShortcut(
      &root, kind: "folder", path: "Projects")

    var raw = root["shortcuts"] as? [Any]
    XCTAssertEqual(raw?.count, 3)
    XCTAssertEqual((raw?[0] as? [String: Any])?["kind"] as? String, "tag")
    XCTAssertEqual(
      (raw?[1] as? [String: Any])?["color"] as? String, "red",
      "unknown entry keys survive untouched")
    XCTAssertEqual((raw?[2] as? [String: Any])?["path"] as? String, "Projects")
    XCTAssertEqual((root["future"] as? [String: Any])?["x"] as? Bool, true)

    SidebarOrganizationSchema.removeShortcut(
      &root, kind: "file", path: "Projects/a.md")
    raw = root["shortcuts"] as? [Any]
    XCTAssertEqual(raw?.count, 2)
    XCTAssertEqual((raw?[0] as? [String: Any])?["kind"] as? String, "tag")
    XCTAssertEqual((raw?[1] as? [String: Any])?["kind"] as? String, "folder")
  }

  func testMoveSwapsVisibleNeighborsAcrossReservedEntriesAndClampsAtEdges() {
    var root: [String: Any] = [
      "shortcuts": [
        entry("file", "a.md"),
        entry("tag", "research"),
        entry("file", "b.md"),
        entry("folder", "F"),
      ]
    ]
    // b.md moves up: swaps with a.md, hopping the reserved tag entry,
    // which keeps its raw position.
    SidebarOrganizationSchema.moveShortcut(
      &root, kind: "file", path: "b.md", delta: -1)
    var paths = (root["shortcuts"] as? [Any])?.compactMap {
      ($0 as? [String: Any])?["path"] as? String
    }
    XCTAssertEqual(paths, ["b.md", "research", "a.md", "F"])

    // Top edge clamps.
    SidebarOrganizationSchema.moveShortcut(
      &root, kind: "file", path: "b.md", delta: -1)
    paths = (root["shortcuts"] as? [Any])?.compactMap {
      ($0 as? [String: Any])?["path"] as? String
    }
    XCTAssertEqual(paths, ["b.md", "research", "a.md", "F"])

    // Bottom edge clamps.
    SidebarOrganizationSchema.moveShortcut(
      &root, kind: "folder", path: "F", delta: 1)
    paths = (root["shortcuts"] as? [Any])?.compactMap {
      ($0 as? [String: Any])?["path"] as? String
    }
    XCTAssertEqual(paths, ["b.md", "research", "a.md", "F"])
  }

  func testTransformRetargetsRenamesRespectingKindNamespaces() {
    var transform = SidebarStructuralTransform()
    transform.renames.append(
      .init(oldPath: "Projects", newPath: "Archive", isDirectory: nil))
    var root: [String: Any] = [
      "shortcuts": [
        entry("folder", "Projects"),
        entry("file", "Projects/a.md"),
        entry("folder", "Projects/Sub"),
        entry("tag", "Projects"),
        entry("file", "Other/b.md"),
      ]
    ]
    transform.applyRaw(to: &root)
    let raw = (root["shortcuts"] as? [Any])?.compactMap { $0 as? [String: Any] }
    XCTAssertEqual(raw?[0]["path"] as? String, "Archive")
    XCTAssertEqual(raw?[1]["path"] as? String, "Archive/a.md")
    XCTAssertEqual(raw?[2]["path"] as? String, "Archive/Sub")
    XCTAssertEqual(
      raw?[3]["path"] as? String, "Projects",
      "reserved kinds are untouched by path transforms")
    XCTAssertEqual(raw?[4]["path"] as? String, "Other/b.md")
  }

  func testTransformKindSpecificExactRename() {
    // A FILE rename must not retarget a folder shortcut sharing the string.
    var transform = SidebarStructuralTransform()
    transform.renames.append(
      .init(oldPath: "Notes", newPath: "Journal", isDirectory: false))
    var shortcuts = [
      SidebarShortcut(kind: .folder, path: "Notes"),
      SidebarShortcut(kind: .file, path: "Notes"),
    ]
    transform.apply(to: &shortcuts)
    XCTAssertEqual(
      shortcuts,
      [
        SidebarShortcut(kind: .folder, path: "Notes"),
        SidebarShortcut(kind: .file, path: "Journal"),
      ])
  }

  func testTransformDeletesRemoveTargetsAndDescendants() {
    var transform = SidebarStructuralTransform()
    transform.deletedFolders.append("Projects")
    transform.deletedFiles.append("Other/gone.md")
    var shortcuts = [
      SidebarShortcut(kind: .folder, path: "Projects"),
      SidebarShortcut(kind: .file, path: "Projects/a.md"),
      SidebarShortcut(kind: .folder, path: "Projects/Sub"),
      SidebarShortcut(kind: .file, path: "Other/gone.md"),
      SidebarShortcut(kind: .file, path: "Other/kept.md"),
    ]
    transform.apply(to: &shortcuts)
    XCTAssertEqual(
      shortcuts, [SidebarShortcut(kind: .file, path: "Other/kept.md")])
  }

  func testTransformConvergenceDedupesFirstOccurrenceWins() {
    var transform = SidebarStructuralTransform()
    transform.renames.append(
      .init(oldPath: "A/x.md", newPath: "B/x.md", isDirectory: false))
    var root: [String: Any] = [
      "shortcuts": [
        entry("file", "B/x.md"),
        entry("file", "A/x.md"),
      ]
    ]
    transform.applyRaw(to: &root)
    let raw = (root["shortcuts"] as? [Any])?.compactMap { $0 as? [String: Any] }
    XCTAssertEqual(raw?.count, 1)
    XCTAssertEqual(raw?[0]["path"] as? String, "B/x.md")
  }
}
