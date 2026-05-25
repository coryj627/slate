// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

// MARK: - MathPrefs extension (FFI struct + Swift-side niceties)
//
// `MathPrefs` itself is now an FFI-generated struct in
// `slate_uniffi.swift` (Record). This file adds the Swift-side
// niceties on top:
// - A default-arguments init so callers can write `MathPrefs()` /
//   `MathPrefs(speechStyle: .mathSpeak)` without spelling out every
//   field.
// - `Codable` for the `PreferencesStore` JSON persistence chain.
// - `Equatable` is implied by uniffi's `Record` derivation.

extension MathPrefs {
    /// Default-initialized prefs. Mirrors the Rust-side
    /// `MathPrefs::default()` (ClearSpeak / Medium / Nemeth).
    init() {
        self.init(
            speechStyle: .clearSpeak,
            verbosity: .medium,
            brailleCode: .nemeth
        )
    }
}

// Codable conformance for MathPrefs. The FFI struct itself isn't
// Codable, but we can extend it with a single-container encoding
// that mirrors the field shape so JSON round-trips through
// PreferencesStore.

extension MathPrefs: Codable {
    private enum CodingKeys: String, CodingKey {
        case speechStyle
        case verbosity
        case brailleCode
    }

    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        self.init(
            speechStyle: try c.decode(MathSpeechStyle.self, forKey: .speechStyle),
            verbosity: try c.decode(MathVerbosity.self, forKey: .verbosity),
            brailleCode: try c.decode(BrailleCode.self, forKey: .brailleCode)
        )
    }

    public func encode(to encoder: Encoder) throws {
        var c = encoder.container(keyedBy: CodingKeys.self)
        try c.encode(speechStyle, forKey: .speechStyle)
        try c.encode(verbosity, forKey: .verbosity)
        try c.encode(brailleCode, forKey: .brailleCode)
    }
}

// MARK: - Codable + UI labels for the FFI enums
//
// uniffi-generated enums are declared in `slate_uniffi.swift` and
// aren't Codable / CaseIterable by default. Swift can't synthesize
// Codable for an enum across files, so we hand-roll init(from:) +
// encode(to:) using a stable string identifier (independent of the
// user-facing displayName so renaming the UI label doesn't migrate
// stored preferences).
//
// **Persistence-tag stability — DO NOT RENAME.** The string literals
// inside every `persistenceTag` switch below are written into users'
// UserDefaults via `PreferencesStore`. Changing any of them (e.g.
// the Rust FFI renaming `.terse` to `.short`) silently corrupts
// stored prefs on the next launch — the JSON decode falls into the
// `default:` branch and throws DataCorrupted, and we lose the user's
// choice back to whatever the type's Swift default is. If you must
// rename an FFI case, add a migration path that reads the old tag
// first; never just bump the tag.

extension MathSpeechStyle: Codable, CaseIterable {
    public static var allCases: [MathSpeechStyle] {
        [.clearSpeak, .mathSpeak]
    }

    var displayName: String {
        switch self {
        case .clearSpeak: return "ClearSpeak"
        case .mathSpeak: return "MathSpeak"
        }
    }

    private var persistenceTag: String {
        switch self {
        case .clearSpeak: return "clearSpeak"
        case .mathSpeak: return "mathSpeak"
        }
    }

    public init(from decoder: Decoder) throws {
        let value = try decoder.singleValueContainer().decode(String.self)
        switch value {
        case "clearSpeak": self = .clearSpeak
        case "mathSpeak": self = .mathSpeak
        default:
            throw DecodingError.dataCorruptedError(
                in: try decoder.singleValueContainer(),
                debugDescription: "Unknown MathSpeechStyle: \(value)"
            )
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode(persistenceTag)
    }
}

extension MathVerbosity: Codable, CaseIterable {
    public static var allCases: [MathVerbosity] {
        [.terse, .medium, .verbose]
    }

    var displayName: String {
        switch self {
        case .terse: return "Short"
        case .medium: return "Medium"
        case .verbose: return "Long"
        }
    }

    private var persistenceTag: String {
        switch self {
        case .terse: return "terse"
        case .medium: return "medium"
        case .verbose: return "verbose"
        }
    }

    public init(from decoder: Decoder) throws {
        let value = try decoder.singleValueContainer().decode(String.self)
        switch value {
        case "terse": self = .terse
        case "medium": self = .medium
        case "verbose": self = .verbose
        default:
            throw DecodingError.dataCorruptedError(
                in: try decoder.singleValueContainer(),
                debugDescription: "Unknown MathVerbosity: \(value)"
            )
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode(persistenceTag)
    }
}

extension BrailleCode: Codable, CaseIterable {
    public static var allCases: [BrailleCode] {
        [.nemeth, .ueb]
    }

    var displayName: String {
        switch self {
        case .nemeth: return "Nemeth"
        case .ueb: return "UEB"
        }
    }

    private var persistenceTag: String {
        switch self {
        case .nemeth: return "nemeth"
        case .ueb: return "ueb"
        }
    }

    public init(from decoder: Decoder) throws {
        let value = try decoder.singleValueContainer().decode(String.self)
        switch value {
        case "nemeth": self = .nemeth
        case "ueb": self = .ueb
        default:
            throw DecodingError.dataCorruptedError(
                in: try decoder.singleValueContainer(),
                debugDescription: "Unknown BrailleCode: \(value)"
            )
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode(persistenceTag)
    }
}
