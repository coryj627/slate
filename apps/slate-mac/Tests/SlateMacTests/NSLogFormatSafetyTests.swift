// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

/// Source-scanning regression guard for the `NSLog` format-string hazard.
///
/// `NSLog(_ format: String, _ args: CVarArg...)` treats its FIRST argument as
/// a printf format string. Passing a message built by Swift interpolation —
/// `NSLog("… \(path): \(error)")` — or any computed string is a latent bug: a
/// `%` in the path, error text, or a user's search query is read as an
/// unsupplied format specifier (`%@` prints `(null)`; `%s`/`%n` dereference
/// junk off the varargs stack). The safe convention, used everywhere in the
/// app, is a FIXED format string — build the message into a `let` and log it
/// via `NSLog("%@", message)`, or use a static literal with matching `%` args
/// (e.g. `NSLog("… %d …", count)`).
///
/// Enforces that convention app-wide so the fixes in PR #915
/// (`GraphConfigWriter`) and its follow-up (the `AppState` / recents-store
/// persistence logs) can't silently regress. See [[project_milestone_p_p2_wave]].
///
/// **Implementation.** Rather than a bespoke lexer, this reuses the project's
/// reviewed `SwiftSourceStripping` helper (see `SwiftSourceStrippingTests`) to
/// blank comments + string *content* while preserving byte offsets. Structural
/// parsing (locating real `NSLog(` calls and their first-argument span) then
/// runs on the STRIPPED text — where a comma/paren/quote hidden inside a
/// string literal can't desync it — and the interpolation check runs on the
/// ORIGINAL span at the same offsets. A format is flagged when it is either
/// **dynamic** (any non-literal code survives stripping in the arg — a bare
/// variable `NSLog(msg)` or a concatenation with a variable `"x " + p`) or
/// **interpolated** (`\(` in the original literal).
///
/// **Inherited limitations** (documented on `SwiftSourceStripping`, shared by
/// every source-scanning test here; fail-closed and not present in the scanned
/// sources): raw string literals `#"…"#` and multiline `"""…"""` aren't
/// modelled, and scanning is grapheme- not scalar-based. These edges can only
/// mis-handle deliberately pathological source, not a realistic accidental
/// `NSLog("… \(x)")` regression, which is exactly what this guards.
final class NSLogFormatSafetyTests: XCTestCase {

    func testEveryNSLogFormatInSourcesIsAStaticLiteral() throws {
        let root = try Self.sourcesRoot()
        let fm = FileManager.default
        guard let walker = fm.enumerator(at: root, includingPropertiesForKeys: nil) else {
            return XCTFail("could not enumerate \(root.path)")
        }
        var scanned = 0
        var offenders: [String] = []
        for case let url as URL in walker where url.pathExtension == "swift" {
            // `slate_uniffi.swift` is generated FFI glue, not hand-authored
            // app code — excluded from architectural-intent scans.
            if url.lastPathComponent == "slate_uniffi.swift" { continue }
            let text = try String(contentsOf: url, encoding: .utf8)
            scanned += 1
            for bad in Self.offendingNSLogFormats(in: text) {
                offenders.append("\(url.lastPathComponent): NSLog(\(bad)…)")
            }
        }
        XCTAssertGreaterThan(scanned, 0, "scanned no Swift sources — path resolution broke")
        XCTAssertTrue(
            offenders.isEmpty,
            "NSLog must take a STATIC format string. Build interpolated/computed text into a "
                + "`let message` and call `NSLog(\"%@\", message)` instead:\n  "
                + offenders.joined(separator: "\n  "))
    }

    // MARK: - Detector unit tests (table-driven, non-vacuity per case)

    /// Every demonstrated hazard shape must be flagged, and every safe shape
    /// must pass — so the guard above can't silently rot into a no-op.
    func testDetectorFlagsHazardsAndAcceptsSafeForms() {
        // Flagged: interpolation / dynamic format.
        for bad in [
            #"NSLog("failed for '\(path)': \(error)")"#,  // interpolation
            #"NSLog(message)"#,  // bare variable format
            #"NSLog("prefix " + suffix)"#,  // literal + variable concat
            "NSLog(\n  \"a \\(x)\")",  // multi-line interpolation
            #"NSLog ("failed \(x)")"#,  // whitespace before the arg list
        ] {
            XCTAssertFalse(
                Self.offendingNSLogFormats(in: bad).isEmpty, "should flag: \(bad)")
        }
        // Accepted: static literal formats (incl. `%` specifiers + concat).
        for ok in [
            #"NSLog("%@", message)"#,
            #"NSLog("a static message with no args")"#,
            #"NSLog("count %d over %d", a, b)"#,  // correct printf usage
            "NSLog(\n  \"line one \"\n  + \"line two %d.\", n)",  // literal-only concat
        ] {
            XCTAssertTrue(
                Self.offendingNSLogFormats(in: ok).isEmpty, "should accept: \(ok)")
        }
        // Decoys: the token inside a comment or a string must NOT be scanned,
        // and `NSLog` inside a longer identifier is a DIFFERENT function.
        for decoy in [
            #"// NSLog("should not \(trip)") in a comment"#,
            #"let s = "contains NSLog(\(x)) inside a string literal""#,
            "/* NSLog(\"\\(y)\") in a block comment */",
            "/* outer /* nested */ NSLog still commented \\(z) */",
            #"fooNSLog(dynamicArg)"#,  // NSLog inside a longer identifier — not the global call
            #"NSLogger(dynamicArg)"#,  // trailing identifier char — not `NSLog`
        ] {
            XCTAssertTrue(
                Self.offendingNSLogFormats(in: decoy).isEmpty, "decoy must be ignored: \(decoy)")
        }
    }

    // MARK: - Detection

    /// The original first-argument text of every real `NSLog(` call whose
    /// format is not a static string literal. Comments + string bodies are
    /// blanked first (offset-preserving) so structure is parsed on inert text.
    static func offendingNSLogFormats(in original: String) -> [String] {
        let stripped = Array(SwiftSourceStripping.strippingCommentsAndStrings(original))
        let orig = Array(original)
        // The stripper is length/offset preserving; if that ever breaks we
        // can't map spans back safely. FAIL CLOSED (Codoki review): surface an
        // unverifiable sentinel so the assertion fails loudly, rather than
        // silently returning "no offenders" and hiding a possible hazard.
        guard stripped.count == orig.count else {
            return ["<unverifiable: source-stripping length mismatch>"]
        }

        let name = Array("NSLog")
        var offenders: [String] = []
        var i = 0
        while i < stripped.count {
            guard i + name.count <= stripped.count,
                Array(stripped[i..<i + name.count]) == name,
                // Word boundary: don't match `NSLog` inside a longer
                // identifier (`fooNSLog`, `NSLogger`).
                (i == 0 || !Self.isIdentifierChar(stripped[i - 1]))
            else {
                i += 1
                continue
            }
            // Swift permits whitespace between the callee and its argument
            // list — `NSLog ("…")` is still a call — so skip it before the
            // required `(` (Codoki review: the bare-`NSLog(` match missed this).
            var k = i + name.count
            while k < stripped.count, stripped[k].isWhitespace { k += 1 }
            guard k < stripped.count, stripped[k] == "(",
                // Trailing identifier char (e.g. `NSLogging`) => not our call.
                (i + name.count == stripped.count || !Self.isIdentifierChar(stripped[i + name.count]))
            else {
                i += 1
                continue
            }
            let (start, end) = Self.firstArgumentSpan(stripped, from: k + 1)
            let strippedArg = String(stripped[start..<end])
            let originalArg = String(orig[start..<end])
            // After blanking string content, a static-literal format leaves
            // only whitespace and `+` (literal concatenation). Anything else
            // surviving is code — a variable or expression => dynamic format.
            let dynamic = strippedArg.contains { !$0.isWhitespace && $0 != "+" }
            // Interpolation lives inside the literal, blanked in `stripped`, so
            // it's checked on the original span.
            let interpolated = originalArg.contains("\\(")
            if dynamic || interpolated {
                offenders.append(
                    String(
                        originalArg.trimmingCharacters(in: .whitespacesAndNewlines).prefix(60)))
            }
            i = end
        }
        return offenders
    }

    /// Swift identifier continuation char (letter, digit, or `_`) — used for
    /// the `NSLog` word-boundary checks. ASCII-only is sufficient: the callee
    /// name we match is ASCII.
    private static func isIdentifierChar(_ c: Character) -> Bool {
        c == "_" || c.isLetter || c.isNumber
    }

    /// The `[start, end)` span of the first call argument, starting just after
    /// `NSLog(`: up to the first top-level comma or the matching close paren.
    /// Runs on STRIPPED text, so string literals are already spaces and can't
    /// hide a `,`/`)`/`(`; only real code parens affect depth.
    private static func firstArgumentSpan(_ s: [Character], from start: Int) -> (Int, Int) {
        var depth = 1
        var j = start
        while j < s.count, depth > 0 {
            let c = s[j]
            if c == "(" {
                depth += 1
            } else if c == ")" {
                depth -= 1
                if depth == 0 { break }
            } else if c == "," && depth == 1 {
                break
            }
            j += 1
        }
        return (start, j)
    }

    /// Walk up from this test file to the `Sources/SlateMac` directory.
    private static func sourcesRoot() throws -> URL {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        while cursor.path != "/" {
            let candidate = cursor.appendingPathComponent("Sources/SlateMac")
            var isDir: ObjCBool = false
            if FileManager.default.fileExists(atPath: candidate.path, isDirectory: &isDir),
                isDir.boolValue
            {
                return candidate
            }
            cursor.deleteLastPathComponent()
        }
        throw CocoaError(.fileNoSuchFile)
    }
}
