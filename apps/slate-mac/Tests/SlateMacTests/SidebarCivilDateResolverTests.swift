// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

final class SidebarCivilDateResolverTests: XCTestCase {
    private func calendar(
        _ identifier: Calendar.Identifier = .gregorian,
        timeZone: String
    ) -> Calendar {
        var calendar = Calendar(identifier: identifier)
        calendar.timeZone = TimeZone(identifier: timeZone)!
        return calendar
    }

    private func gregorianComponents(_ date: Date, timeZone: TimeZone) -> DateComponents {
        var gregorian = Calendar(identifier: .gregorian)
        gregorian.timeZone = timeZone
        return gregorian.dateComponents([.year, .month, .day, .hour, .minute, .second], from: date)
    }

    func testAcceptsCanonicalLeapDayAndRejectsNormalizedOrNonASCIIShapes() {
        let utc = calendar(timeZone: "UTC")
        XCTAssertNotNil(SidebarCivilDateResolver.resolve("2024-02-29", calendar: utc))
        XCTAssertNil(SidebarCivilDateResolver.resolve("2023-02-29", calendar: utc))
        XCTAssertNil(SidebarCivilDateResolver.resolve("2024-2-29", calendar: utc))
        XCTAssertNil(SidebarCivilDateResolver.resolve("2024-02-29 ", calendar: utc))
        XCTAssertNil(SidebarCivilDateResolver.resolve("２０２４-０２-２９", calendar: utc))
    }

    func testProlepticGregorianCutoverAndSupportedYearRangeMatchChrono() throws {
        let utc = calendar(timeZone: "UTC")
        let expectedUnixSeconds: [(String, TimeInterval)] = [
            ("0001-01-01", -62_135_596_800),
            ("1582-10-04", -12_220_243_200),
            ("1582-10-10", -12_219_724_800),
            ("1582-10-15", -12_219_292_800),
        ]

        for (value, expected) in expectedUnixSeconds {
            let resolved = try XCTUnwrap(
                SidebarCivilDateResolver.resolve(value, calendar: utc),
                "\(value) must exist in the proleptic Gregorian calendar")
            XCTAssertEqual(resolved.timeIntervalSince1970, expected, accuracy: 0.5)
        }

        XCTAssertNotNil(SidebarCivilDateResolver.resolve("9999-12-31", calendar: utc))
        XCTAssertNil(SidebarCivilDateResolver.resolve("0000-01-01", calendar: utc))
    }

    func testPositiveAndNegativeOffsetsResolveToGregorianLocalStartNotUTCMidnight() throws {
        for zoneName in ["Pacific/Kiritimati", "Pacific/Honolulu"] {
            let injected = calendar(timeZone: zoneName)
            let resolved = try XCTUnwrap(
                SidebarCivilDateResolver.resolve("2024-07-14", calendar: injected))
            let components = gregorianComponents(resolved, timeZone: injected.timeZone)
            XCTAssertEqual(components.year, 2024)
            XCTAssertEqual(components.month, 7)
            XCTAssertEqual(components.day, 14)
            XCTAssertEqual(components.hour, 0)
            XCTAssertEqual(components.minute, 0)
            XCTAssertEqual(components.second, 0)

            let utcComponents = gregorianComponents(resolved, timeZone: TimeZone(secondsFromGMT: 0)!)
            XCTAssertFalse(
                utcComponents.hour == 0 && utcComponents.day == 14,
                "a civil date in \(zoneName) must not be encoded as UTC midnight")
        }
    }

    func testDSTBoundaryDaysRoundTripToTheAuthoredGregorianDay() throws {
        let eastern = calendar(timeZone: "America/New_York")
        for value in ["2024-03-10", "2024-11-03"] {
            let resolved = try XCTUnwrap(
                SidebarCivilDateResolver.resolve(value, calendar: eastern))
            let components = gregorianComponents(resolved, timeZone: eastern.timeZone)
            XCTAssertEqual(
                String(format: "%04d-%02d-%02d", components.year!, components.month!, components.day!),
                value)
            XCTAssertEqual(components.hour, 0)
        }
    }

    func testMidnightDSTGapReturnsTheCivilDaysFirstRepresentableInstant() throws {
        // Sao Paulo advanced from 00:00 to 01:00 on this date. A valid
        // civil-day resolver must return Calendar.startOfDay rather than reject
        // the day merely because its first representable local hour is 1.
        let saoPaulo = calendar(timeZone: "America/Sao_Paulo")
        let resolved = try XCTUnwrap(
            SidebarCivilDateResolver.resolve("2018-11-04", calendar: saoPaulo))
        let components = gregorianComponents(resolved, timeZone: saoPaulo.timeZone)
        XCTAssertEqual(components.year, 2018)
        XCTAssertEqual(components.month, 11)
        XCTAssertEqual(components.day, 4)
        XCTAssertEqual(components.hour, 1)
    }

    func testInjectedNonGregorianCalendarsCannotReinterpretISOComponents() throws {
        let zoneName = "America/Los_Angeles"
        let expected = try XCTUnwrap(
            SidebarCivilDateResolver.resolve(
                "2024-02-29", calendar: calendar(.gregorian, timeZone: zoneName)))

        for identifier in [
            Calendar.Identifier.buddhist,
            Calendar.Identifier.hebrew,
            Calendar.Identifier.islamicUmmAlQura,
        ] {
            let resolved = try XCTUnwrap(
                SidebarCivilDateResolver.resolve(
                    "2024-02-29", calendar: calendar(identifier, timeZone: zoneName)))
            XCTAssertEqual(resolved, expected)
        }
    }
}
