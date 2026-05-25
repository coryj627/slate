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
struct MathPrefs: Equatable {
    var speechStyle: MathSpeechStyle = .clearSpeak
    var verbosity: MathVerbosity = .medium
    var brailleCode: BrailleCode = .nemeth
}
