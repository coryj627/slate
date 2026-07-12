// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Property DatePicker plumbing (#857): the parse/serialize round
/// trips behind the date/datetime picker editors, and the
/// never-destroy-data contract — malformed or non-conforming stored
/// values fail parse (→ the raw TextField stays mounted) and pass
/// through the existing commit path verbatim.
final class PropertyDatePickerTests: XCTestCase {

    // MARK: Date round trip

    func testDateParsesAndSerializesRoundTrip() {
        let raw = "2026-07-11"
        let date = PropertyDateEditing.date(fromDateString: raw)
        XCTAssertNotNil(date)
        XCTAssertEqual(PropertyDateEditing.dateString(from: date!), raw)
    }

    /// `looksLikeDate` accepts shape-valid nonsense like `2026-13-40`;
    /// the DatePicker gate must NOT — the non-lenient calendar parse
    /// rejects it, so the row keeps the raw TextField.
    func testShapeValidNonsenseFailsParse() {
        XCTAssertNil(PropertyDateEditing.date(fromDateString: "2026-13-40"))
        XCTAssertNil(PropertyDateEditing.date(fromDateString: "2026-02-30"))
        XCTAssertNil(PropertyDateEditing.date(fromDateString: "not-a-date"))
        XCTAssertNil(PropertyDateEditing.date(fromDateString: ""))
    }

    // MARK: Datetime round trip (form-preserving)

    func testIso8601DatetimeRoundTripsVerbatim() {
        let raw = "2026-07-11T09:30:00Z"
        guard let parsed = PropertyDateEditing.datetime(fromString: raw) else {
            return XCTFail("ISO-8601 with timezone must parse")
        }
        XCTAssertEqual(parsed.form, .iso8601)
        XCTAssertEqual(
            PropertyDateEditing.datetimeString(from: parsed.date, form: parsed.form),
            raw,
            "the Z form round-trips verbatim")
    }

    func testLocalNaiveDatetimeRoundTripsVerbatim() {
        let raw = "2026-07-11T09:30:00"
        guard let parsed = PropertyDateEditing.datetime(fromString: raw) else {
            return XCTFail("the naive local form must parse")
        }
        XCTAssertEqual(parsed.form, .localNaive)
        XCTAssertEqual(
            PropertyDateEditing.datetimeString(from: parsed.date, form: parsed.form),
            raw,
            "the naive local form round-trips verbatim — no silent dialect rewrite")
    }

    func testOffsetDatetimeParsesAsIso8601() {
        guard
            let parsed = PropertyDateEditing.datetime(
                fromString: "2026-07-11T11:30:00+02:00")
        else {
            return XCTFail("offset forms must parse")
        }
        XCTAssertEqual(parsed.form, .iso8601)
        XCTAssertEqual(
            PropertyDateEditing.datetimeString(from: parsed.date, form: parsed.form),
            "2026-07-11T09:30:00Z",
            "offset forms serialize as UTC — same instant, canonical suffix")
    }

    func testMalformedDatetimeFailsParse() {
        XCTAssertNil(PropertyDateEditing.datetime(fromString: "yesterday"))
        XCTAssertNil(PropertyDateEditing.datetime(fromString: "2026-07-11"))
        XCTAssertNil(PropertyDateEditing.datetime(fromString: ""))
    }

    // MARK: Malformed passthrough (never destroy data)

    /// A stored value the picker can't represent keeps the TextField —
    /// and committing it flows through the EXISTING validation
    /// verbatim: shape-valid strings still commit unchanged
    /// (`2026-13-40` — backend re-emits verbatim), shape-invalid ones
    /// still surface the same inline error as before.
    func testMalformedDatePassesThroughCommitVerbatim() {
        let shapeValid = PropertyEditDraft.scalarText(
            ScalarTextKind(kind: "date", value: "2026-13-40"))
        XCTAssertEqual(
            shapeValid.toPropertyValue(),
            .success(PropertyValue.date(value: "2026-13-40")),
            "shape-valid non-calendar values commit verbatim — no data destruction")

        let shapeInvalid = PropertyEditDraft.scalarText(
            ScalarTextKind(kind: "date", value: "July 11"))
        XCTAssertEqual(
            shapeInvalid.toPropertyValue(),
            .failure(.init(message: "Date must be YYYY-MM-DD.")),
            "the pre-existing shape validation is unchanged")
    }

    func testMalformedDatetimePassesThroughCommitVerbatim() {
        let draft = PropertyEditDraft.scalarText(
            ScalarTextKind(kind: "datetime", value: "somewhen soon"))
        XCTAssertEqual(
            draft.toPropertyValue(),
            .success(PropertyValue.datetime(value: "somewhen soon")),
            "datetime commits stay passthrough — exactly the pre-#857 behavior")
    }

    /// The picker's own serialization always satisfies the commit
    /// path's shape validation — a picked date can never bounce.
    /// Codex review: picker eligibility gates on the STORED value,
    /// never the in-flight draft — correcting malformed `2026-02-30`
    /// inside the raw TextField must not swap in a DatePicker mid-edit.
    func testPickerEligibilityReadsStoredValueOnly() {
        let malformedStored = PropertyEditDraft.scalarText(
            ScalarTextKind(kind: "date", value: "2026-02-30"))
        XCTAssertFalse(
            PropertyEditorRow.storedValueTakesDatePicker(malformedStored, kind: "date"),
            "malformed stored value keeps the raw TextField")

        let validStored = PropertyEditDraft.scalarText(
            ScalarTextKind(kind: "date", value: "2026-03-01"))
        XCTAssertTrue(
            PropertyEditorRow.storedValueTakesDatePicker(validStored, kind: "date"))

        let naive = PropertyEditDraft.scalarText(
            ScalarTextKind(kind: "datetime", value: "2026-07-11T09:30:00"))
        XCTAssertTrue(
            PropertyEditorRow.storedValueTakesDatePicker(naive, kind: "datetime"))
        XCTAssertFalse(
            PropertyEditorRow.storedValueTakesDatePicker(naive, kind: "date"),
            "kind mismatch never takes the picker")
    }

    func testPickerSerializationAlwaysCommits() {
        let date = PropertyDateEditing.date(fromDateString: "2026-07-11")!
        let draft = PropertyEditDraft.scalarText(
            ScalarTextKind(
                kind: "date", value: PropertyDateEditing.dateString(from: date)))
        XCTAssertEqual(
            draft.toPropertyValue(),
            .success(PropertyValue.date(value: "2026-07-11")))
    }
}
