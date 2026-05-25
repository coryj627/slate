import Foundation

/// Swift-side mirror of `slate_core::math::MathPrefs`. The FFI
/// surfaces the enum components (`MathSpeechStyle`, `MathVerbosity`,
/// `BrailleCode`) but not the wrapping struct — defined here so
/// `AppState.mathPrefs` has a single binding for UI panels to drive
/// + observe.
///
/// Settings panel (#224) consumes this; the backend's
/// `get_math_blocks` reads the equivalent from its own
/// `SessionConfig.math_prefs` snapshot. Until #224 lands the
/// session-side setter, changes to this struct re-fire the load
/// but the rendered output uses session defaults — see the docstring
/// on `AppState.mathPrefs`.
struct MathPrefs: Equatable, Codable {
    var speechStyle: MathSpeechStyle = .clearSpeak
    var verbosity: MathVerbosity = .medium
    var brailleCode: BrailleCode = .nemeth
}

// MARK: - Codable + UI labels for the FFI enums
//
// uniffi-generated enums are declared in `slate_uniffi.swift` and
// aren't Codable / CaseIterable by default. Swift can't synthesize
// Codable for an enum across files, so we hand-roll init(from:) +
// encode(to:) using a stable string identifier (independent of the
// user-facing displayName so renaming the UI label doesn't migrate
// stored preferences).

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
