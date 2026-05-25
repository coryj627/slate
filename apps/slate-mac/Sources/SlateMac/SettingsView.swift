import SwiftUI

/// Settings window (Cmd+,). Two tabs:
/// - **Math** — speech style, verbosity, braille code, with a live
///   preview of a sample formula so the user hears their selection
///   in context.
/// - **Code** — preamble verbosity for the code-block AT label.
///
/// All pickers are text-labelled (no icon-only segments) per WCAG
/// 2.5.3. Live preview re-renders on every change so the user gets
/// immediate confirmation that the setting took effect.
struct SettingsView: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        TabView {
            MathSettingsTab()
                .tabItem {
                    Label("Math", systemImage: "function")
                }
            CodeSettingsTab()
                .tabItem {
                    Label("Code", systemImage: "chevron.left.forwardslash.chevron.right")
                }
        }
        .frame(minWidth: 500, minHeight: 400)
        .padding(20)
        .accessibilityLabel("Slate preferences")
    }
}

// MARK: - Math tab

struct MathSettingsTab: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        Form {
            Section {
                Picker("Speech style", selection: $appState.mathPrefs.speechStyle) {
                    ForEach(MathSpeechStyle.allCases, id: \.self) { style in
                        Text(style.displayName).tag(style)
                    }
                }
                .pickerStyle(.segmented)
                .accessibilityHint(
                    "Choose how math expressions are read aloud."
                )

                Picker("Verbosity", selection: $appState.mathPrefs.verbosity) {
                    ForEach(MathVerbosity.allCases, id: \.self) { v in
                        Text(v.displayName).tag(v)
                    }
                }
                .pickerStyle(.segmented)
                .accessibilityHint(
                    "Choose how detailed the spoken math is."
                )

                Picker("Braille code", selection: $appState.mathPrefs.brailleCode) {
                    ForEach(BrailleCode.allCases, id: \.self) { code in
                        Text(code.displayName).tag(code)
                    }
                }
                .pickerStyle(.segmented)
                .accessibilityHint(
                    "Choose the braille standard for math output."
                )
            } header: {
                Text("Math accessibility")
                    .font(.headline)
                    .accessibilityAddTraits(.isHeader)
            } footer: {
                Text("Changes apply immediately to math in the read pane.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Section {
                MathLivePreview()
            } header: {
                Text("Live preview")
                    .font(.headline)
                    .accessibilityAddTraits(.isHeader)
            }
        }
        .formStyle(.grouped)
    }
}

/// Renders a sample formula via the same MathView the read pane uses,
/// so the user hears + sees their preference choices in context.
///
/// The MathBlock is built locally with the current prefs reflected
/// in the speech / braille fields — but the actual MathCAT render is
/// done by the backend at note-load time, not on this surface. For
/// V1 we render a static formula with placeholder speech derived
/// from the selected style so the user gets an immediate "this is
/// what changed" affordance. Once the session-side setter ships
/// (audit #259), this preview will route through the real pipeline.
struct MathLivePreview: View {
    @EnvironmentObject private var appState: AppState

    /// Sample formula. Picked for its mix of structure (sum, fraction,
    /// scripted identifier) so the speech style differences between
    /// ClearSpeak and MathSpeak are audible.
    private let sampleSource = "\\sum_{i=0}^{n} \\frac{i^2}{2}"

    private var sampleBlock: MathBlock {
        MathBlock(
            source: sampleSource,
            displayStyle: .block,
            mathml: "<math><mrow></mrow></math>",
            speech: previewSpeech,
            braille: Data(previewBraille.utf8),
            line: 1,
            byteOffset: 0
        )
    }

    /// Style-derived placeholder speech so the live preview actually
    /// changes when the user flips the picker. Full ClearSpeak /
    /// MathSpeak strings differ in cadence + identifier expansion;
    /// this short rendering hints at that without invoking MathCAT
    /// from the UI thread.
    private var previewSpeech: String {
        let head: String
        switch appState.mathPrefs.speechStyle {
        case .clearSpeak:
            head = "The sum from i equals 0 to n of i squared over 2"
        case .mathSpeak:
            head = "sum from i equals 0 to n of fraction i squared over 2 end fraction"
        }
        switch appState.mathPrefs.verbosity {
        case .terse:
            return head
        case .medium:
            return "\(head)."
        case .verbose:
            return "Math expression. \(head). End math."
        }
    }

    private var previewBraille: String {
        switch appState.mathPrefs.brailleCode {
        case .nemeth: return "Sample Nemeth braille for the formula"
        case .ueb: return "Sample UEB braille for the formula"
        }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            MathView(block: sampleBlock)
                .frame(maxWidth: .infinity, alignment: .center)
            Text(
                "Currently: \(appState.mathPrefs.speechStyle.displayName), "
                    + "\(appState.mathPrefs.verbosity.displayName), "
                    + "\(appState.mathPrefs.brailleCode.displayName)"
            )
            .font(.caption)
            .foregroundStyle(.secondary)
            .accessibilityLabel(
                "Current settings: speech style "
                    + "\(appState.mathPrefs.speechStyle.displayName), "
                    + "verbosity \(appState.mathPrefs.verbosity.displayName), "
                    + "braille code \(appState.mathPrefs.brailleCode.displayName)."
            )
        }
    }
}

// MARK: - Code tab

struct CodeSettingsTab: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        Form {
            Section {
                Picker(
                    "Preamble verbosity",
                    selection: $appState.codePrefs.verbosity
                ) {
                    ForEach(CodeVerbosity.allCases, id: \.self) { v in
                        Text(v.displayName).tag(v)
                    }
                }
                .pickerStyle(.menu)
                .accessibilityHint(
                    "Choose how detailed the code-block accessibility preamble is."
                )
            } header: {
                Text("Code accessibility")
                    .font(.headline)
                    .accessibilityAddTraits(.isHeader)
            } footer: {
                Text(
                    "Affects the preamble that screen readers hear before"
                        + " a code block. Font and color preferences land later."
                )
                .font(.caption)
                .foregroundStyle(.secondary)
            }

            Section {
                VStack(alignment: .leading, spacing: 8) {
                    Text("Currently: \(appState.codePrefs.verbosity.displayName)")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    Text(previewPreamble)
                        .font(.callout.weight(.medium))
                        .accessibilityLabel(
                            "Example preamble: \(previewPreamble)"
                        )
                }
            } header: {
                Text("Example preamble")
                    .font(.headline)
                    .accessibilityAddTraits(.isHeader)
            }
        }
        .formStyle(.grouped)
    }

    private var previewPreamble: String {
        switch appState.codePrefs.verbosity {
        case .preambleOnly:
            return "Code block, rust, 5 lines."
        case .preambleFirstLine:
            return "Code block, rust, 5 lines. First line: fn main() {"
        case .preambleAllTokens:
            return
                "Code block, rust, 5 lines. Tokens: keyword fn, identifier main, "
                + "punctuation (, ), {, return, …"
        }
    }
}
