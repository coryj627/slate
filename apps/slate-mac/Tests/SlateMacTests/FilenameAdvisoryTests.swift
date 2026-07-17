// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

/// FL04-A Task 3: one deterministic, non-blocking filename advisory model,
/// one reusable warning view, and one quiet live-announcement policy across
/// inline rename/create naming flows.
@MainActor
final class FilenameAdvisoryTests: XCTestCase {
    private static let filesystemPrefix =
        "Some file systems may reject these characters: "
    private static let wikilinkPrefix =
        "These characters can make wikilinks less portable: "

    private static let sourceRoot = URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .appendingPathComponent("Sources/SlateMac")

    private func advisory(_ name: String) -> FilenameAdvisory {
        FilenameAdvisory(name)
    }

    func testEveryRiskCharacterUsesTheLockedCategoryAndCopy() {
        let cases: [(
            name: String,
            filesystem: [String],
            wikilink: [String],
            message: String,
            accessibilityMessage: String
        )] = [
            (
                "a/b", ["/"], [], Self.filesystemPrefix + "/.",
                Self.filesystemPrefix + "forward slash."
            ),
            (
                #"a\b"#, [#"\"#], [], Self.filesystemPrefix + #"\."#,
                Self.filesystemPrefix + "backslash."
            ),
            (
                "a:b", [":"], [], Self.filesystemPrefix + ":.",
                Self.filesystemPrefix + "colon."
            ),
            ("a\u{0}b", ["null character"], [],
                Self.filesystemPrefix + "null character.",
                Self.filesystemPrefix + "null character."),
            (
                "a[b", [], ["["], Self.wikilinkPrefix + "[.",
                Self.wikilinkPrefix + "opening bracket."
            ),
            (
                "a]b", [], ["]"], Self.wikilinkPrefix + "].",
                Self.wikilinkPrefix + "closing bracket."
            ),
            (
                "a#b", [], ["#"], Self.wikilinkPrefix + "#.",
                Self.wikilinkPrefix + "number sign."
            ),
            (
                "a^b", [], ["^"], Self.wikilinkPrefix + "^.",
                Self.wikilinkPrefix + "caret."
            ),
            (
                "a|b", [], ["|"], Self.wikilinkPrefix + "|.",
                Self.wikilinkPrefix + "vertical bar."
            ),
        ]

        for value in cases {
            let result = advisory(value.name)
            XCTAssertEqual(result.filesystemCharacters, value.filesystem, value.name)
            XCTAssertEqual(result.wikilinkCharacters, value.wikilink, value.name)
            XCTAssertEqual(result.message, value.message, value.name)
            XCTAssertEqual(
                result.accessibilityMessage,
                value.accessibilityMessage,
                value.name
            )
            XCTAssertFalse(result.isEmpty, value.name)
        }
    }

    func testOrderingDeduplicationAndCombinedSentenceOrderAreDeterministic() {
        let value = advisory("x|#][^[|:\u{0}\\//::")

        XCTAssertEqual(
            value.filesystemCharacters,
            ["/", #"\"#, ":", "null character"])
        XCTAssertEqual(value.wikilinkCharacters, ["[", "]", "#", "^", "|"])
        XCTAssertEqual(
            value.message,
            "Some file systems may reject these characters: /, \\, :, "
                + "null character. These characters can make wikilinks less "
                + "portable: [, ], #, ^, |.")
        XCTAssertEqual(
            value.accessibilityMessage,
            "Some file systems may reject these characters: forward slash, backslash, "
                + "colon, null character. These characters can make wikilinks less "
                + "portable: opening bracket, closing bracket, number sign, caret, "
                + "vertical bar."
        )

        XCTAssertEqual(
            advisory("////").filesystemCharacters,
            ["/"],
            "repeated characters are represented once")
        XCTAssertEqual(
            advisory("[[[[").wikilinkCharacters,
            ["["],
            "repeated characters are represented once")
    }

    func testOrdinaryNamesAndClearingHaveNoAdvisory() {
        for name in [
            "", "Note.md", "Meeting notes 2026.md", "Überblick.MD",
            "Folder/File name".replacingOccurrences(of: "/", with: "-"),
        ] {
            let value = advisory(name)
            XCTAssertEqual(value.filesystemCharacters, [String](), name)
            XCTAssertEqual(value.wikilinkCharacters, [String](), name)
            XCTAssertNil(value.message, name)
            XCTAssertNil(value.accessibilityMessage, name)
            XCTAssertTrue(value.isEmpty, name)
        }
    }

    func testSemanticIdentityDependsOnMatchedRiskSetNotOrdinaryText() {
        XCTAssertEqual(advisory("first/name"), advisory("another/name"))
        XCTAssertEqual(advisory("first#name"), advisory("another#name"))
        XCTAssertNotEqual(advisory("first/name"), advisory("first#name"))
        XCTAssertNotEqual(advisory("first/name"), advisory("first/:name"))
    }

    func testOpeningBracketWarnsButCurrentFormatterStillAcceptsIt() throws {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("filename-advisory-\(UUID().uuidString)")
        defer { try? FileManager.default.removeItem(at: root) }
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        let path = "Topic[1.md"
        try "# Topic\n".write(
            to: root.appendingPathComponent(path), atomically: true, encoding: .utf8)

        let session = try VaultSession.openFilesystem(rootPath: root.path)
        _ = try session.scanInitial(cancel: CancelToken())

        XCTAssertEqual(advisory(path).wikilinkCharacters, ["["])
        XCTAssertEqual(
            try session.wikilinkForPath(path: path),
            "[[Topic[1]]",
            "the portability advisory must not become a formatter refusal")
    }

    func testAnnouncementGateIsSilentForInitialSafeOrPrefilledWarningState() {
        let safe = advisory("Note.md")
        var safeGate = FilenameAdvisoryAnnouncementGate(initial: safe)
        XCTAssertNil(safeGate.announcement(for: safe))

        let prefilled = advisory("Draft#1.md")
        var warningGate = FilenameAdvisoryAnnouncementGate(initial: prefilled)
        XCTAssertNil(warningGate.announcement(for: prefilled))
    }

    func testAnnouncementGatePostsOncePerNonemptySemanticChange() {
        var gate = FilenameAdvisoryAnnouncementGate(initial: advisory("Note.md"))
        let filesystem = advisory("Note/name.md")
        let sameFilesystemMeaning = advisory("Another/name.md")
        let combined = advisory("Another/name#.md")

        XCTAssertEqual(
            gate.announcement(for: filesystem),
            filesystem.accessibilityMessage
        )
        XCTAssertNil(
            gate.announcement(for: sameFilesystemMeaning),
            "ordinary keystrokes that preserve the matched set stay silent")
        XCTAssertEqual(
            gate.announcement(for: combined),
            combined.accessibilityMessage
        )
        XCTAssertNil(gate.announcement(for: combined))
    }

    func testAnnouncementGateClearsSilentlyAndRearms() {
        let warning = advisory("Note#1.md")
        var gate = FilenameAdvisoryAnnouncementGate(initial: advisory("Note.md"))

        XCTAssertEqual(
            gate.announcement(for: warning),
            warning.accessibilityMessage
        )
        XCTAssertNil(
            gate.announcement(for: advisory("Note.md")),
            "clearing has no repository-wide announcement convention")
        XCTAssertEqual(
            gate.announcement(for: warning),
            warning.accessibilityMessage,
            "reintroducing a cleared risk is a new semantic advisory")
    }

    func testReusableWarningViewHasVisibleNonColorMeaningAndOneAXElement() throws {
        let source = try normalizedSource("Sidebar/FilenameAdvisory.swift")

        XCTAssertTrue(source.contains("struct FilenameAdvisoryView: View"))
        XCTAssertTrue(source.contains("SlateSymbol.warning.decorative"))
        XCTAssertTrue(source.contains("if let message = advisory.message"))
        XCTAssertTrue(source.contains("Text(message)"))
        XCTAssertTrue(source.contains("Tokens.ColorRole.warningText"))
        XCTAssertTrue(source.contains("Tokens.Typography.caption"))
        XCTAssertTrue(source.contains(".accessibilityElement(children: .ignore)"))
        XCTAssertTrue(source.contains(#".accessibilityLabel("Filename warning")"#))
        XCTAssertTrue(source.contains(".accessibilityValue(accessibilityMessage)"))
        XCTAssertTrue(source.contains(".accessibilityHint("))
        XCTAssertTrue(
            source.contains(
                #""This advisory does not block submission; the name is still validated when you continue.""#))
    }

    func testReusableWarningViewWrapsAndHasNoFixedWidth() throws {
        let source = try normalizedSource("Sidebar/FilenameAdvisory.swift")

        XCTAssertTrue(
            source.contains(".fixedSize(horizontal: false, vertical: true)"),
            "warning copy must wrap vertically in narrow rows and sheets")
        XCTAssertTrue(source.contains(".lineLimit(nil)"))
        XCTAssertFalse(source.contains(".frame(width:"))
        XCTAssertFalse(source.contains(".frame(minWidth:"))
        XCTAssertFalse(source.contains("Color("), "reuse the warningText token")
        XCTAssertFalse(source.contains("foregroundStyle(.red)"))
    }

    func testReusableViewSeedsAnnouncementBaselineAndPostsMediumChangesOnly() throws {
        let source = try normalizedSource("Sidebar/FilenameAdvisory.swift")

        XCTAssertTrue(source.contains("init(name: String)"))
        XCTAssertTrue(source.contains("FilenameAdvisory(name)"))
        XCTAssertTrue(
            source.contains(
                "FilenameAdvisoryAnnouncementGate(initial: advisory)"),
            "a prefilled warning must be the silent baseline, not an empty-to-warning change")
        XCTAssertTrue(source.contains(".onChange(of: advisory)"))
        XCTAssertTrue(source.contains("announcementGate.announcement(for: newValue)"))
        XCTAssertTrue(
            source.contains(
                "postAccessibilityAnnouncement(accessibilityMessage, priority: .medium)"))
    }

    func testRenameAndInlineCreateShareOneAdvisoryWithoutBreakingFieldContracts() throws {
        let source = try normalizedSource("FileTreeSidebar.swift")
        let appState = try normalizedSource("AppState.swift")

        XCTAssertEqual(
            occurrences(of: "FilenameAdvisoryView(name: text)", in: source), 1,
            "rename, new-note, and new-folder naming must reuse RenameField")
        XCTAssertEqual(
            occurrences(of: "RenameField(", in: source), 1,
            "there is one shared inline naming component")
        XCTAssertTrue(appState.contains("requestCreateNote"))
        XCTAssertTrue(appState.contains("requestCreateFolder"))
        XCTAssertGreaterThanOrEqual(
            occurrences(of: "requestRename(", in: appState), 3,
            "both create funnels and explicit rename must converge on the inline field")

        XCTAssertTrue(source.contains("_text = State(initialValue: initialName)"))
        XCTAssertFalse(source.contains("@State private var text: String = \"\""))
        XCTAssertTrue(source.contains(".focused($focused)"))
        XCTAssertTrue(source.contains(".onSubmit { onCommit(text) }"))
        XCTAssertTrue(source.contains(".onExitCommand { onCancel() }"))
        XCTAssertTrue(source.contains("if !isFocused"))
        XCTAssertTrue(source.contains("error == nil"))
        XCTAssertTrue(source.contains("selectBaseName()"))
        XCTAssertTrue(
            source.contains("HStack(alignment: .firstTextBaseline"),
            "a wrapped warning must not pull the row icon below the name field"
        )
        XCTAssertTrue(source.contains("if let error = error"))
        XCTAssertTrue(source.contains(#".accessibilityLabel("Rename error. \(error)")"#))
        assertAppearsInOrder(
            [
                #"TextField("Name", text: $text)"#,
                "if let error = error",
                "FilenameAdvisoryView(name: text)",
            ],
            in: source,
            message: "the actionable validation error must precede the advisory"
        )
    }

    private func rawSource(_ relativePath: String) throws -> String {
        try String(
            contentsOf: Self.sourceRoot.appendingPathComponent(relativePath),
            encoding: .utf8)
    }

    private func normalizedSource(_ relativePath: String) throws -> String {
        strippingCommentsPreservingStrings(try rawSource(relativePath))
            .split(whereSeparator: { $0.isWhitespace })
            .joined(separator: " ")
    }

    private func occurrences(of needle: String, in source: String) -> Int {
        guard !needle.isEmpty else { return 0 }
        var count = 0
        var cursor = source.startIndex
        while let range = source.range(of: needle, range: cursor..<source.endIndex) {
            count += 1
            cursor = range.upperBound
        }
        return count
    }

    private func assertAppearsInOrder(
        _ needles: [String],
        in source: String,
        message: String,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        var cursor = source.startIndex
        for needle in needles {
            guard let range = source.range(of: needle, range: cursor..<source.endIndex) else {
                XCTFail("\(message): missing \(needle)", file: file, line: line)
                return
            }
            cursor = range.upperBound
        }
    }

    private func strippingCommentsPreservingStrings(_ source: String) -> String {
        enum State {
            case code
            case lineComment
            case blockComment(Int)
            case string
        }

        let characters = Array(source)
        var output = ""
        output.reserveCapacity(source.utf8.count)
        var state = State.code
        var index = 0

        func blank(_ character: Character) -> Character {
            character == "\n" || character == "\r" ? character : " "
        }

        while index < characters.count {
            let character = characters[index]
            let next = index + 1 < characters.count ? characters[index + 1] : nil
            switch state {
            case .code:
                if character == "/", next == "/" {
                    output.append("  ")
                    state = .lineComment
                    index += 2
                } else if character == "/", next == "*" {
                    output.append("  ")
                    state = .blockComment(1)
                    index += 2
                } else {
                    output.append(character)
                    if character == "\"" { state = .string }
                    index += 1
                }
            case .lineComment:
                output.append(blank(character))
                if character == "\n" { state = .code }
                index += 1
            case .blockComment(let depth):
                if character == "/", next == "*" {
                    output.append("  ")
                    state = .blockComment(depth + 1)
                    index += 2
                } else if character == "*", next == "/" {
                    output.append("  ")
                    state = depth == 1 ? .code : .blockComment(depth - 1)
                    index += 2
                } else {
                    output.append(blank(character))
                    index += 1
                }
            case .string:
                output.append(character)
                if character == "\\", let next {
                    output.append(next)
                    index += 2
                } else {
                    if character == "\"" { state = .code }
                    index += 1
                }
            }
        }
        return output
    }
}
