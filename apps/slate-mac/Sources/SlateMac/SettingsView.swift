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
        // Audit #262 M2: previously we wrapped the TabView with
        // `.accessibilityLabel("Slate preferences")` — that
        // overrode the TabView's native AT container role, so VO
        // announced "Slate preferences, group" without
        // distinguishing the tab strip from the panel below.
        // Apply the label to a wrapping VStack instead so the
        // TabView keeps its native tab-interface AT shape and we
        // still get a window-level title for AT.
        VStack(spacing: 0) {
            TabView {
                MathSettingsTab()
                    .tabItem {
                        Label("Math", systemImage: "function")
                    }
                CodeSettingsTab()
                    .tabItem {
                        Label(
                            "Code",
                            systemImage: "chevron.left.forwardslash.chevron.right"
                        )
                    }
            }
        }
        .frame(minWidth: 500, minHeight: 400)
        .padding(20)
        .navigationTitle("Slate preferences")
    }
}

// MARK: - Math tab

struct MathSettingsTab: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        Form {
            Section {
                // Audit #262 M1: segmented Pickers expose each
                // segment as an `NSSegmentedControl` cell; VO
                // navigation reads "ClearSpeak, 1 of 2, selected"
                // without the "Speech style" Picker label. Wrap
                // each Picker in an `.accessibilityElement(
                // children: .contain)` container with an explicit
                // label so VO groups segments under the labelled
                // shell.
                //
                // Red-team H1 follow-up: confirm on a real VO
                // session that swipe-right from the labelled shell
                // still steps into each segment with its own
                // "selected" state. If VO instead announces the
                // whole shell as one stop, swap `.contain` for
                // `.combine` and re-test — there's a known SwiftUI
                // shape ambiguity here on macOS 13/14.
                Picker("Speech style", selection: $appState.mathPrefs.speechStyle) {
                    ForEach(MathSpeechStyle.allCases, id: \.self) { style in
                        Text(style.displayName).tag(style)
                    }
                }
                .pickerStyle(.segmented)
                .accessibilityElement(children: .contain)
                .accessibilityLabel("Speech style")

                Picker("Verbosity", selection: $appState.mathPrefs.verbosity) {
                    ForEach(MathVerbosity.allCases, id: \.self) { v in
                        Text(v.displayName).tag(v)
                    }
                }
                .pickerStyle(.segmented)
                .accessibilityElement(children: .contain)
                .accessibilityLabel("Verbosity")

                Picker("Braille code", selection: $appState.mathPrefs.brailleCode) {
                    ForEach(BrailleCode.allCases, id: \.self) { code in
                        Text(code.displayName).tag(code)
                    }
                }
                .pickerStyle(.segmented)
                .accessibilityElement(children: .contain)
                .accessibilityLabel("Braille code")
            } header: {
                // CI's a11y linter (slate-a11y-check) enforces
                // `.accessibilityAddTraits(.isHeader)` on every
                // heading-styled Text per WCAG 2.4.6, even when
                // the surrounding Form Section header role would
                // *also* fire natively. The lint's `heading-
                // trait-missing` rule is structural (font-only),
                // so the explicit trait is the way to keep CI
                // green. A pre-push red-team flagged this as
                // potentially double-announcing, but the codebase
                // policy is to defer to the linter.
                Text("Math accessibility")
                    .font(.headline)
                    .accessibilityAddTraits(.isHeader)
            } footer: {
                // Audit #262 M3: was `.foregroundStyle(.secondary)`
                // which lands ~3.2:1 against the Form's grouped-
                // section background in light mode — below AA's
                // 4.5:1. The footer carries non-decorative info
                // ("Changes apply immediately…"), so promote to
                // `.primary` and rely on `.font(.caption)` size
                // to keep the visual hierarchy.
                //
                // Same Swift-type-checker workaround as the Code
                // tab — string is built via a computed property
                // to avoid timing out on long `+` operator chains
                // inside a deep view body.
                Text(mathFooterText)
                    .font(.caption)
                    .foregroundStyle(.primary)
            }

            Section {
                MathLivePreview()
            } header: {
                // See "Math accessibility" header above for the
                // CI lint rationale on keeping `.isHeader`.
                Text("Live preview")
                    .font(.headline)
                    .accessibilityAddTraits(.isHeader)
            }
        }
        .formStyle(.grouped)
    }

    /// Footer guidance for the Math tab. Extracted to a property
    /// so the Swift type checker doesn't time out building the
    /// surrounding Form body.
    private var mathFooterText: String {
        "Changes apply immediately to math in the read pane. "
            + "Speech style controls how math is read aloud (ClearSpeak: "
            + "intuitive; MathSpeak: precise / verbatim). Verbosity sets "
            + "how detailed the spoken math is. Braille code switches "
            + "between Nemeth and UEB encodings."
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
            // Audit #261 H2 — canonical phrasing for the status
            // line. WCAG 2.5.3 (Label in Name) technically only
            // governs *named UI components* (buttons, links,
            // pickers), not status text — but matching visible to
            // AT text here is still the right hygiene: VO reads
            // the visible Text directly, and an earlier version
            // diverged AT label from visible text which read
            // confusingly back-to-back during continuous-read.
            Text(
                "Currently: speech style "
                    + "\(appState.mathPrefs.speechStyle.displayName), "
                    + "verbosity \(appState.mathPrefs.verbosity.displayName), "
                    + "braille code \(appState.mathPrefs.brailleCode.displayName)."
            )
            // Audit #262 M3: `.foregroundStyle(.secondary)` drops
            // below 4.5:1 against the form background. Promote to
            // `.primary` and rely on `.font(.caption)` size to
            // distinguish from body text.
            .font(.caption)
            .foregroundStyle(.primary)
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
                // Audit #262 M4: `.pickerStyle(.menu)` becomes an
                // `NSPopUpButton`; VoiceOver treats
                // `.accessibilityHint` inconsistently on pop-ups.
                // Moved the substantive guidance into the visible
                // footer Text below, which IS reliably announced
                // as section content.
            } header: {
                // See "Math accessibility" header above for the
                // CI lint rationale on keeping `.isHeader`.
                Text("Code accessibility")
                    .font(.headline)
                    .accessibilityAddTraits(.isHeader)
            } footer: {
                // Audit #262 M3 + M4: `.primary` foreground so the
                // ~4.5:1 contrast against the form background is
                // safe (was `.secondary` which read ~3.2:1 in light
                // mode), and the guidance now describes ALL three
                // verbosity levels so AT users hear what each
                // means without relying on the Picker's
                // unreliable hint announcement.
                //
                // The string is built via a computed property
                // (not inline `+`-concat) because Swift's type
                // checker times out on long operator chains
                // inside a deep SwiftUI view body — locally builds
                // but CI hit "compiler is unable to type-check
                // this expression in reasonable time" on macOS
                // Swift 5.10.
                Text(codeFooterText)
                    .font(.caption)
                    .foregroundStyle(.primary)
            }

            Section {
                VStack(alignment: .leading, spacing: 8) {
                    Text("Currently: \(appState.codePrefs.verbosity.displayName)")
                        .font(.caption)
                        .foregroundStyle(.primary)
                    Text(previewPreamble)
                        .font(.callout.weight(.medium))
                        // Audit #262 H2-style cleanup: visible Text
                        // is the AT label; no override needed.
                }
            } header: {
                // See "Math accessibility" header above for the
                // CI lint rationale on keeping `.isHeader`.
                Text("Example preamble")
                    .font(.headline)
                    .accessibilityAddTraits(.isHeader)
            }
        }
        .formStyle(.grouped)
    }

    /// Footer guidance for the Code tab. Extracted to a property
    /// so the Swift type checker doesn't time out building the
    /// surrounding Form body.
    private var codeFooterText: String {
        "Affects the preamble screen readers hear before a code block. "
            + "\"Preamble only\" reads \"Code block, <language>, N lines.\" "
            + "\"Preamble + first line\" adds the signature/first non-blank "
            + "line. \"Preamble + all tokens\" reads every token (useful for "
            + "braille display work). Font and color preferences land later."
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
