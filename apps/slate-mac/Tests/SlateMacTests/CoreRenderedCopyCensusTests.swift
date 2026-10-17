// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Provenance census for the copy core renders and mac once composed (plan
/// 43 PR 8; locked §1.1, contract 38 "core owns copy"): a resolved embed's
/// card title (`resolved_embed_title`, #1286), the code-block preamble
/// (`code_block_preamble`, #1053) and the template picker's opened sentence
/// (`A11yEvent.templatePickerOpened`, #1122). No Swift source under
/// `Sources/SlateMac` spells one of those shapes in a string literal, beyond
/// the registered exceptions below; comments may name them.
///
/// The secondary net, as on Windows (`EmbedPreviewTitleCensus`): a phrase
/// census cannot see a title split across literals ("Embedded " + "note: "),
/// so the primary witnesses read what VoiceOver reads —
/// `EmbedViewTests.testEveryResolvedEmbedLabelIsCoreRendered` and
/// `testAnOverlongHeadingIsBoundedByCore` from the hosted accessibility tree,
/// and `CodeBlockViewTests` from the preamble label.
final class CoreRenderedCopyCensusTests: XCTestCase {

    /// A resolved embed's card-title shapes, as core renders them.
    static let cardTitleShapes = [
        "Embedded note:", "Embedded section:", "Embedded block from",
        "Embedded image:", "Embedded base:",
    ]
    /// The code-block preamble's shape ("Code block, {language}, …").
    static let codePreambleShapes = ["Code block,"]
    /// The template picker's opened sentence, all three arms.
    static let templatePickerShapes = ["Template picker opened"]

    /// Registered exceptions, by exact literal text on the line that spells
    /// the shape; each must match exactly once, so a stale one fails.
    ///
    /// The query-block cards (```base, ```slate-query and ```dataview fences,
    /// and an invalid slate-query) share the base card's "Embedded base:"
    /// shape but are not resolved embeds: core's `ResolvedEmbed` has no kind
    /// for them and Windows renders no such card, so they stay host-composed
    /// until core owns them (#1388).
    static let queryBlockCardTitles: [(file: String, literal: String)] = [
        ("Bases/BaseEmbedDocument.swift", #""Embedded base: inline base""#),
        ("Bases/BaseEmbedDocument.swift", #""Embedded base: saved query \(reference)""#),
        ("Bases/BaseEmbedDocument.swift", #""Embedded base: Dataview""#),
        ("Bases/BaseEmbedDocument.swift", #""Embedded base: invalid slate-query""#),
    ]

    /// Settings ▸ Code quotes the preamble as fixed sample copy for its
    /// verbosity picker (the footer and "Example preamble"). It is no code
    /// block's preamble and composes nothing from data; the setting itself
    /// changes nothing yet (#1389).
    static let settingsPreambleSamples: [(file: String, literal: String)] = [
        ("SettingsView.swift", #""Preamble only" reads "Code block, <language>, N lines.""#),
        ("SettingsView.swift", #""Code block, rust, 5 lines.""#),
        ("SettingsView.swift", #""Code block, rust, 5 lines. First line: fn main() {""#),
        ("SettingsView.swift", "Code block, rust, 5 lines. Tokens: keyword fn, identifier main,"),
    ]

    /// Twin of the Windows `EmbedPreviewTitleCensus.NoHostSourceSpellsACardTitle`.
    func testNoHostSourceSpellsACardTitle() throws {
        let failures = Self.spellingFailures(
            in: try Self.productionSources(), shapes: Self.cardTitleShapes,
            registered: Self.queryBlockCardTitles)
        XCTAssertTrue(
            failures.isEmpty,
            "a resolved embed's title is core's `resolvedEmbedTitle(resolved:)`, "
                + "never a Swift literal:\n" + failures.joined(separator: "\n"))
    }

    func testNoHostSourceSpellsACodeBlockPreamble() throws {
        let failures = Self.spellingFailures(
            in: try Self.productionSources(), shapes: Self.codePreambleShapes,
            registered: Self.settingsPreambleSamples)
        XCTAssertTrue(
            failures.isEmpty,
            "the code-block preamble is core's `codeBlockPreamble(language:source:)`, "
                + "never a Swift literal:\n" + failures.joined(separator: "\n"))
    }

    func testNoHostSourceSpellsTheTemplatePickerOpenedSentence() throws {
        let failures = Self.spellingFailures(
            in: try Self.productionSources(), shapes: Self.templatePickerShapes, registered: [])
        XCTAssertTrue(
            failures.isEmpty,
            "the picker-open sentence is core's `.templatePickerOpened(count:)`, "
                + "never a Swift literal:\n" + failures.joined(separator: "\n"))
    }

    /// The census's teeth, twin of the Windows
    /// `TheSpellingCensusNamesEveryHostTitle`: a shape in any kind of string
    /// literal is named at its file and line, whatever its case; a shape in a
    /// comment is not; a registered literal is excused only in its own file
    /// and only once; and a registration that matches nothing is stale.
    func testTheSpellingCensusNamesEveryHostTitle() {
        let named: [(source: String, line: Int)] = [
            (#"let a = "Embedded note: \(path)""#, 1),
            ("let b = \"a\\\"b Embedded section: \\(h) from \\(p)\"", 1),
            ("// a comment\nlet c = \"Embedded block from \\(path)\"", 2),
            (##"let d = #"Embedded image: \#(alt)"#"##, 1),
            ("let e = \"\"\"\n    Embedded base: \\(name)\n    \"\"\"", 2),
            (#"let f = "\(f("x")) embedded NOTE: y""#, 1),
            (#"let g = "https://example.test Code block, \(language)""#, 1),
            (#"let h = "Template picker opened. \(n) templates available.""#, 1),
            (#"/* "Embedded note:" */ let i = "Embedded image: \(alt)""#, 1),
        ]
        let shapes = Self.cardTitleShapes + Self.codePreambleShapes + Self.templatePickerShapes
        for (source, line) in named {
            let failures = Self.spellingFailures(
                in: [("Synthetic.swift", source)], shapes: shapes, registered: [])
            XCTAssertEqual(failures.count, 1, "not named exactly once: \(source)\n\(failures)")
            XCTAssertTrue(
                failures.first?.hasPrefix("Synthetic.swift:\(line): ") == true,
                "named at the wrong place: \(failures)")
        }

        let clean = [
            "// \"Embedded note: \\(path)\" in a line comment",
            "/* outer /* \"Embedded section: x\" */ still a comment */ let a = 1",
            "/// The container carries a \"Code block, rust, 5 lines\" preamble.",
            #"let a = resolvedEmbedTitle(resolved: .note(targetPath: path))"#,
            #"let b = "Embedded base table""#,
            #"let c = "Jump to source — line \(line). Embedded base.""#,
            #"let d = "Embedded bases preview.""#,
            #"let e = "Copy code""#,
        ]
        for source in clean {
            XCTAssertEqual(
                Self.spellingFailures(in: [("Synthetic.swift", source)], shapes: shapes, registered: []),
                [], "named a clean source: \(source)")
        }

        let registered = [(file: "A.swift", literal: #""Embedded base: inline base""#)]
        XCTAssertEqual(
            Self.spellingFailures(
                in: [("A.swift", #"label = "Embedded base: inline base""#)],
                shapes: shapes, registered: registered),
            [], "a registered literal in its file is excused")
        XCTAssertEqual(
            Self.spellingFailures(
                in: [("A.swift", ""), ("B.swift", #"label = "Embedded base: inline base""#)],
                shapes: shapes, registered: registered).count,
            2, "a registered literal elsewhere is named, and its registration is stale")
        XCTAssertEqual(
            Self.spellingFailures(
                in: [("A.swift", #"x = "Embedded base: inline base"; y = "Embedded base: inline base""#)],
                shapes: shapes, registered: registered).count,
            1, "a registered literal is excused once")
    }

    // MARK: - scanning

    /// Every shape spelled in code (comments blanked, string literals kept),
    /// as `file:line: …`, less the registered literals, plus each registration
    /// that excused nothing.
    static func spellingFailures(
        in sources: [(path: String, text: String)],
        shapes: [String],
        registered: [(file: String, literal: String)]
    ) -> [String] {
        var unused = registered.map { _ in true }
        var failures: [String] = []
        for source in sources {
            let lines = strippingComments(source.text)
                .split(separator: "\n", omittingEmptySubsequences: false)
            for (index, line) in lines.enumerated() {
                var remaining = String(line)
                for (slot, entry) in registered.enumerated()
                where unused[slot] && entry.file == source.path {
                    if let range = remaining.range(of: entry.literal) {
                        remaining.replaceSubrange(range, with: "")
                        unused[slot] = false
                    }
                }
                for shape in shapes
                where remaining.range(of: shape, options: .caseInsensitive) != nil {
                    failures.append(
                        "\(source.path):\(index + 1): spells \"\(shape)\" — "
                            + line.trimmingCharacters(in: .whitespaces).prefix(120))
                }
            }
        }
        for (slot, entry) in registered.enumerated() where unused[slot] {
            failures.append("\(entry.file): the registered literal \(entry.literal) excuses nothing (stale)")
        }
        return failures
    }

    /// Every production source under `Sources/SlateMac`, by path relative to
    /// it. The uniffi-generated bindings (materialized by the build, not
    /// tracked) are machinery, and core's own doc comments there name the
    /// shapes they render.
    static func productionSources() throws -> [(path: String, text: String)] {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .appendingPathComponent("Sources/SlateMac")
        let enumerator = try XCTUnwrap(
            FileManager.default.enumerator(at: root, includingPropertiesForKeys: nil))
        var sources: [(path: String, text: String)] = []
        for case let url as URL in enumerator where url.pathExtension == "swift" {
            if url.lastPathComponent == "slate_uniffi.swift" { continue }
            let path = String(url.standardizedFileURL.path.dropFirst(root.standardizedFileURL.path.count + 1))
            sources.append((path, try String(contentsOf: url, encoding: .utf8)))
        }
        XCTAssertGreaterThan(sources.count, 100, "too few sources under \(root.path)")
        XCTAssertTrue(sources.contains { $0.path == "EmbedView.swift" })
        XCTAssertTrue(sources.contains { $0.path == "Bases/BaseEmbedDocument.swift" })
        return sources.sorted { $0.path < $1.path }
    }

    /// `source` with every comment blanked (newlines kept) and every string
    /// literal kept: ordinary, multi-line `"""` and raw `#"…"#` strings, with
    /// interpolations lexed as code, so `//` inside a string is not a comment.
    static func strippingComments(_ source: String) -> String {
        enum Context {
            case code(parens: Int)
            case string(hashes: Int, multiline: Bool)
        }
        let c = Array(source)
        var out = String()
        out.reserveCapacity(c.count)
        var stack: [Context] = [.code(parens: 0)]
        var i = 0
        func at(_ k: Int) -> Character? { k < c.count ? c[k] : nil }
        func hashes(from k: Int) -> Int {
            var n = 0
            while at(k + n) == "#" { n += 1 }
            return n
        }
        func blank(_ ch: Character) -> Character { ch == "\n" || ch == "\r" ? ch : " " }

        while i < c.count {
            let ch = c[i]
            switch stack[stack.count - 1] {
            case .code(let parens):
                if ch == "/", at(i + 1) == "/" {
                    while i < c.count, c[i] != "\n" {
                        out.append(blank(c[i]))
                        i += 1
                    }
                } else if ch == "/", at(i + 1) == "*" {
                    var depth = 0
                    repeat {
                        if c[i] == "/", at(i + 1) == "*" {
                            depth += 1
                            out.append("  ")
                            i += 2
                        } else if c[i] == "*", at(i + 1) == "/" {
                            depth -= 1
                            out.append("  ")
                            i += 2
                        } else {
                            out.append(blank(c[i]))
                            i += 1
                        }
                    } while depth > 0 && i < c.count
                } else if ch == "\"" || (ch == "#" && at(i + hashes(from: i)) == "\"") {
                    let n = hashes(from: i)
                    let multiline = at(i + n + 1) == "\"" && at(i + n + 2) == "\""
                    let opener = n + (multiline ? 3 : 1)
                    out.append(contentsOf: c[i..<min(c.count, i + opener)])
                    i += opener
                    stack.append(.string(hashes: n, multiline: multiline))
                } else {
                    if ch == "(" {
                        stack[stack.count - 1] = .code(parens: parens + 1)
                    } else if ch == ")" {
                        if parens == 0, stack.count > 1 {
                            stack.removeLast()  // closes an interpolation
                        } else {
                            stack[stack.count - 1] = .code(parens: max(0, parens - 1))
                        }
                    }
                    out.append(ch)
                    i += 1
                }
            case .string(let n, let multiline):
                if ch == "\\", hashes(from: i + 1) >= n {
                    let escaped = i + 1 + n
                    let end = min(c.count, escaped + 1)
                    out.append(contentsOf: c[i..<end])
                    i = end
                    if at(escaped) == "(" { stack.append(.code(parens: 0)) }
                } else if ch == "\"",
                    !multiline || (at(i + 1) == "\"" && at(i + 2) == "\""),
                    hashes(from: i + (multiline ? 3 : 1)) >= n
                {
                    let end = min(c.count, i + (multiline ? 3 : 1) + n)
                    out.append(contentsOf: c[i..<end])
                    i = end
                    stack.removeLast()
                } else {
                    out.append(ch)
                    i += 1
                }
            }
        }
        return out
    }
}
