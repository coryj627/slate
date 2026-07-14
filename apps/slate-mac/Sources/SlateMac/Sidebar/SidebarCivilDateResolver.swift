// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Resolves an authored canonical Gregorian civil date without reinterpreting
/// its numeric components through the user's display calendar.
enum SidebarCivilDateResolver {
    static func resolve(
        _ canonicalDate: String,
        calendar injectedCalendar: Calendar = .current
    ) -> Date? {
        let bytes = Array(canonicalDate.utf8)
        guard bytes.count == 10,
              bytes[4] == 45,
              bytes[7] == 45
        else {
            return nil
        }

        guard bytes.enumerated().allSatisfy({ index, byte in
            index == 4 || index == 7 || (48...57).contains(byte)
        }) else {
            return nil
        }

        func component(_ range: Range<Int>) -> Int {
            range.reduce(0) { value, index in value * 10 + Int(bytes[index] - 48) }
        }

        let year = component(0..<4)
        let month = component(5..<7)
        let day = component(8..<10)
        guard (1...9999).contains(year) else { return nil }

        var gregorian = Calendar(identifier: .gregorian)
        gregorian.timeZone = injectedCalendar.timeZone

        // Foundation's Gregorian Calendar uses the historical Julian/Gregorian
        // cutover for ancient component construction. The core contract is
        // chrono's proleptic Gregorian calendar, so use a strict formatter with
        // its cutover pushed into the distant past for pre-cutover dates. Noon
        // is an unambiguous anchor even when local midnight is skipped by DST.
        if canonicalDate < "1582-10-15" {
            let formatter = DateFormatter()
            formatter.locale = Locale(identifier: "en_US_POSIX")
            formatter.calendar = gregorian
            formatter.timeZone = gregorian.timeZone
            formatter.gregorianStartDate = .distantPast
            formatter.isLenient = false
            formatter.dateFormat = "yyyy-MM-dd HH:mm:ss"
            guard let localNoon = formatter.date(from: "\(canonicalDate) 12:00:00") else {
                return nil
            }
            let localStart = gregorian.startOfDay(for: localNoon)
            formatter.dateFormat = "yyyy-MM-dd"
            guard formatter.string(from: localStart) == canonicalDate else { return nil }
            return localStart
        }

        var components = DateComponents()
        components.calendar = gregorian
        components.timeZone = gregorian.timeZone
        components.year = year
        components.month = month
        components.day = day
        components.hour = 0
        components.minute = 0
        components.second = 0

        guard let constructed = gregorian.date(from: components) else { return nil }
        let localStart = gregorian.startOfDay(for: constructed)
        let roundTrip = gregorian.dateComponents(
            [.year, .month, .day],
            from: localStart)
        guard roundTrip.year == year,
              roundTrip.month == month,
              roundTrip.day == day
        else {
            return nil
        }
        return localStart
    }
}
