// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Shared geometry for every Settings pane (#862).
///
/// macOS settings.md: a "toolbar-based multi-pane layout" people "open
/// and close quickly via keyboard" with "no need to dock or resize it"
/// — so the panes are a FIXED width with a UNIFORM inset, applied ONCE
/// at the container, never per-tab. Before #862 the outer container
/// used a resizable `minWidth`, and `BibliographySettingsTab` wrapped
/// its own `.padding(20)` + `.frame` on top of it, so tabs sat at
/// different insets and the window resized as you switched tabs.
/// This enum is the single source of truth for the chosen VALUES;
/// `SettingsLayoutTests` additionally pins the WIRING (fixed min==max
/// width, one container inset, no per-tab title/padding) by source
/// inspection, so re-introducing that drift fails a test.
enum SettingsLayout {
    /// Fixed content width for every pane. A single fixed width (not a
    /// resizable `minWidth`) stops the window from resizing as tabs
    /// change — the #862 complaint. 520pt fits the widest pane (the
    /// Bibliography source rows) without leaving the sparse panes
    /// (Canvas, Code) too roomy.
    static let paneWidth: CGFloat = 520
    /// Uniform inset around the pane content, applied ONCE at the
    /// TabView container so every tab sits at the same inset.
    static let inset: CGFloat = 20
    /// Baseline pane height so short panes don't collapse; taller panes
    /// (Bibliography with many sources) grow past it. The window fits
    /// each pane's height — standard macOS settings behavior.
    static let paneMinHeight: CGFloat = 400
}

/// Settings window (Cmd+,). Toolbar-based multi-pane layout (macOS
/// settings.md), with a fixed pane width + uniform inset applied once
/// here (`SettingsLayout`, #862). Panes:
/// - **General** — app-wide launch behavior (reopen last vault, #872).
/// - **Math** — speech style, verbosity, braille code, with a live
///   preview of a sample formula so the user hears their selection
///   in context.
/// - **Code** — preamble verbosity for the code-block AT label.
/// - **Bibliography** — per-vault citation sources + styles.
/// - **Canvas** — announcement verbosity.
/// - **History** — retention window + since-open summary toggle.
///
/// All pickers are text-labelled (no icon-only segments) per WCAG
/// 2.5.3. Live previews re-render on every change so the user gets
/// immediate confirmation that a setting took effect.
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
                // General is the conventional leading pane (macOS
                // settings). #872's launch toggle lives here.
                GeneralSettingsTab()
                    .tabItem {
                        SlateSymbol.settings.label("General")
                    }
                MathSettingsTab()
                    .tabItem {
                        SlateSymbol.math.label()
                    }
                CodeSettingsTab()
                    .tabItem {
                        SlateSymbol.code.label()
                    }
                BibliographySettingsTab()
                    .tabItem {
                        SlateSymbol.bibliography.label()
                    }
                CanvasSettingsTab()
                    .tabItem {
                        SlateSymbol.canvas.label()
                    }
                HistorySettingsTab()
                    .tabItem {
                        SlateSymbol.history.label()
                    }
            }
        }
        // #862: ONE fixed width + ONE uniform inset for every pane
        // (macOS settings.md — a fixed, non-resizable multi-pane
        // window: "no need to … resize it"). Was
        // `.frame(minWidth: 500 …)`, which let the window resize per
        // tab, compounded by BibliographySettingsTab's own
        // `.padding(20)` + `.frame`. The inset now lives here alone;
        // no tab re-pads.
        // Pin the width (min == max) so it's fixed/non-resizable, while
        // a `minHeight` baseline lets taller panes grow — the fixed-
        // width overload can't also take a flexible `minHeight`.
        .frame(
            minWidth: SettingsLayout.paneWidth,
            maxWidth: SettingsLayout.paneWidth,
            minHeight: SettingsLayout.paneMinHeight
        )
        .padding(SettingsLayout.inset)
        // #862: a single stable window title for every pane. It used
        // to appear only on the History tab (which set its own
        // `.navigationTitle("History")`, diverging from the rest);
        // handling it once here keeps all tabs consistent
        // (settings.md: the settings window carries one title).
        .navigationTitle("Slate preferences")
    }
}

// MARK: - General tab (#872)

/// App-wide launch behavior. Today one control: whether launch reopens
/// the most-recent vault (launching.md — "Restore previous state on
/// restart … avoid making people retrace steps"). Holding ⌥ at launch
/// is the transient override; this toggle is the persistent,
/// discoverable one (and the only escape hatch a user who can't hold a
/// modifier at launch has). Structured like the Canvas/History tabs:
/// grouped Form, `.isHeader` section header, APCA-safe token footer.
struct GeneralSettingsTab: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        Form {
            Section {
                Toggle(
                    "Reopen last vault at launch",
                    isOn: Binding(
                        get: { appState.restoreVaultOnLaunch },
                        set: { appState.setRestoreVaultOnLaunch($0) }
                    )
                )
            } header: {
                // See the Math tab header for the CI a11y-lint rationale
                // on keeping the explicit `.isHeader` trait.
                Text("Launch")
                    .font(.headline)
                    .accessibilityAddTraits(.isHeader)
            } footer: {
                Text(
                    "On launch, Slate reopens the vault you had open last so you don't retrace your steps. Hold Option (⌥) while launching to show the welcome screen instead. If the last vault has moved, Slate opens the welcome screen and offers to remove it from your recent vaults."
                )
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            }
        }
        .formStyle(.grouped)
        .accessibilityLabel("General settings")
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
                // segmented-Picker shape ambiguity to watch here.
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
                // tab — string is hoisted to a static let to
                // avoid timing out on long `+` operator chains
                // inside a deep view body.
                Text(Self.mathFooterText)
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

    /// Footer guidance for the Math tab.
    ///
    /// Stored as a `static let` for two reasons:
    /// 1. The string never changes per-instance — `static let` is
    ///    the canonical Swift shape for "this is a constant" and
    ///    drops even the (negligible) computed-property overhead.
    /// 2. The `+`-concat chain has to live OUTSIDE the SwiftUI view
    ///    body — keeping it inline tripped CI's Swift 5.10
    ///    type-checker with "compiler is unable to type-check this
    ///    expression in reasonable time" (PR #263 fixup). Keep the
    ///    hoisted form so a future visitor doesn't re-inline it
    ///    and re-trip the type-checker.
    ///
    /// Routed through `String(localized:)` (#264) so the copy picks
    /// up translations automatically once string catalogs land; with
    /// no catalogs present it round-trips the literal unchanged.
    /// Single multiline literal (not `+` concatenation) because
    /// `String.LocalizationValue` needs one literal for key
    /// extraction. Internal (not private) so the round-trip test can
    /// assert the copy survives the localization layer.
    static let mathFooterText: String = String(
        localized: """
            Changes apply immediately to math in the read pane. \
            Speech style controls how math is read aloud (ClearSpeak: \
            intuitive; MathSpeak: precise / verbatim). Verbosity sets \
            how detailed the spoken math is. Braille code switches \
            between Nemeth and UEB encodings.
            """
    )
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

    /// Visible status line under the preview. Single interpolated
    /// literal so the copy is localization-routed (red-team #264 F1
    /// — the previous `+`-concat bypassed LocalizedStringKey) while
    /// staying out of the view body for the type-checker budget.
    private var statusText: String {
        String(
            localized:
                "Currently: speech style \(appState.mathPrefs.speechStyle.displayName), verbosity \(appState.mathPrefs.verbosity.displayName), braille code \(appState.mathPrefs.brailleCode.displayName)."
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
            head = String(localized: "The sum from i equals 0 to n of i squared over 2")
        case .mathSpeak:
            head = String(
                localized: "sum from i equals 0 to n of fraction i squared over 2 end fraction"
            )
        }
        switch appState.mathPrefs.verbosity {
        case .terse:
            return head
        case .medium:
            return String(localized: "\(head).")
        case .verbose:
            return String(localized: "Math expression. \(head). End math.")
        }
    }

    private var previewBraille: String {
        // The braille CELLS aren't localizable, but these are
        // placeholder descriptions of them (real braille arrives via
        // MathCAT at note-load), so they route like any other copy.
        switch appState.mathPrefs.brailleCode {
        case .nemeth: return String(localized: "Sample Nemeth braille for the formula")
        case .ueb: return String(localized: "Sample UEB braille for the formula")
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
            // Red-team #264 F1: a `+`-concat types as String and
            // falls through to Text's verbatim (StringProtocol)
            // overload — it is NOT localization-routed, unlike a
            // single (optionally interpolated) literal. Route it
            // explicitly.
            Text(statusText)
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
                // The string is hoisted to a static let (not an
                // inline `+`-concat) because Swift's type checker
                // times out on long operator chains inside a deep
                // SwiftUI view body — locally builds but CI hit
                // "compiler is unable to type-check this expression
                // in reasonable time" on macOS Swift 5.10.
                Text(Self.codeFooterText)
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

    /// Footer guidance for the Code tab.
    ///
    /// See `MathSettingsTab.mathFooterText` for the full rationale
    /// — same shape (static let, hoisted out of the view body to
    /// keep the Swift 5.10 type checker under its time budget on
    /// the CI macOS runner). Routed through `String(localized:)`
    /// (#264); single multiline literal for key extraction; internal
    /// for the round-trip test.
    static let codeFooterText: String = String(
        localized: """
            Affects the preamble screen readers hear before a code block. \
            "Preamble only" reads "Code block, <language>, N lines." \
            "Preamble + first line" adds the signature/first non-blank \
            line. "Preamble + all tokens" reads every token (useful for \
            braille display work). Font and color preferences land later.
            """
    )

    private var previewPreamble: String {
        switch appState.codePrefs.verbosity {
        case .preambleOnly:
            return String(localized: "Code block, rust, 5 lines.")
        case .preambleFirstLine:
            return String(localized: "Code block, rust, 5 lines. First line: fn main() {")
        case .preambleAllTokens:
            return String(
                localized: """
                    Code block, rust, 5 lines. Tokens: keyword fn, identifier main, \
                    punctuation (, ), {, return, …
                    """
            )
        }
    }
}

// MARK: - Bibliography tab (Milestone L, #281)

/// Per-vault bibliography settings. Drives `.slate/prefs.json`
/// through `PrefsJsonStore` + `AppState.applyBibliographyPrefs`.
/// Three sections:
///
/// - Sources — list of `.bib` / `.json` files contributing entries.
///   Add via NSOpenPanel; remove via Delete on a selected row.
/// - Default style — picker over the configured CSL files.
/// - Additional styles — `.csl` paths available for ad-hoc switching.
///
/// The full event-driven hot-reload from the disk-side notify
/// watcher lives in a separate ticket alongside the vault scanner's
/// real watcher; today, Add / Remove writes prefs.json + immediately
/// pushes the new sources through `setBibliographySources`.
struct BibliographySettingsTab: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        // #862: no per-tab `.padding(20)` + `.frame` here. The single
        // uniform inset + fixed width now live once on the SettingsView
        // container (`SettingsLayout`). This tab used to double-inset and
        // stretch to `maxWidth: .infinity`, which is exactly what made
        // the window jump width between tabs. The grouped `form` now
        // fills the fixed pane like every other tab, and `noVaultState`
        // keeps its own centering frame.
        Group {
            if appState.currentVaultURL == nil {
                noVaultState
            } else {
                form
            }
        }
    }

    private var noVaultState: some View {
        VStack(spacing: 12) {
            Spacer()
            // Audit #262 M3 policy (as the Math/Code tabs): SwiftUI
            // `.secondary` lands ~3.2:1 on the grouped Form background,
            // below AA. The APCA-gated token role is the compliant
            // secondary (the Canvas tab's approach).
            Text("Open a vault to configure its bibliography.")
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .multilineTextAlignment(.center)
            Spacer()
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Open a vault to configure its bibliography.")
    }

    private var form: some View {
        Form {
            sourcesSection
            defaultStyleSection
            additionalStylesSection
            if let error = appState.bibliographySettingsError {
                Section {
                    Text(error)
                        .font(.caption)
                        .foregroundStyle(Tokens.ColorRole.destructiveText)
                        .accessibilityLabel("Bibliography settings error: \(error)")
                }
            }
        }
        .formStyle(.grouped)
    }

    // MARK: - Sources

    private var sourcesSection: some View {
        Section {
            if appState.bibliographyPrefs.sources.isEmpty {
                Text("No sources configured yet.")
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .accessibilityLabel("No sources configured yet.")
            } else {
                ForEach(
                    Array(appState.bibliographyPrefs.sources.enumerated()),
                    id: \.offset
                ) { index, source in
                    sourceRow(index: index, source: source)
                }
            }
            HStack {
                Button("Add source…") { addSource() }
                    .accessibilityHint(
                        "Opens a file picker to add a .bib or CSL-JSON source."
                    )
                Spacer()
            }
        } header: {
            Text("Sources")
                .accessibilityAddTraits(.isHeader)
        } footer: {
            Text(
                "BibTeX / BibLaTeX / CSL-JSON files. Drop your library export anywhere in the vault, then add it here. Multiple sources merge by citation key — first-source wins on duplicates."
            )
            .font(.caption)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
    }

    private func sourceRow(index: Int, source: BibliographySource) -> some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text(source.path)
                    .lineLimit(2)
                Text(formatLabel(source.format))
                    .font(.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .accessibilityHidden(true)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .accessibilityElement(children: .combine)
            .accessibilityLabel(
                "Source: \(source.path), format \(formatLabel(source.format))."
            )
            Toggle(
                "Watch",
                isOn: Binding(
                    get: { source.watch },
                    set: { newValue in
                        var prefs = appState.bibliographyPrefs
                        if prefs.sources.indices.contains(index) {
                            prefs.sources[index].watch = newValue
                            persist(prefs)
                        }
                    }
                )
            )
            .toggleStyle(.switch)
            // Visible label (toggles.md: "clearly identify what's being
            // toggled — macOS apps may supply an explicit label"): the
            // row context (path + format) says nothing about WATCHING,
            // so a bare switch beside a labeled Remove button was
            // ambiguous to sighted users. The AX label below stays the
            // richer AT phrasing.
            .accessibilityLabel(
                "Watch for changes, \(source.watch ? "on" : "off"), \(source.path)."
            )
            Button("Remove") {
                var prefs = appState.bibliographyPrefs
                if prefs.sources.indices.contains(index) {
                    prefs.sources.remove(at: index)
                    persist(prefs)
                }
            }
            .accessibilityLabel("Remove source \(source.path)")
        }
    }

    /// Format names are proper nouns (BibTeX / BibLaTeX / CSL-JSON)
    /// — deliberately NOT localization-routed (#264 sweep exemption).
    private func formatLabel(_ format: BibFormat) -> String {
        switch format {
        case .bibTeX: return "BibTeX"
        case .bibLaTeX: return "BibLaTeX"
        case .cslJson: return "CSL-JSON"
        }
    }

    // MARK: - Default style

    private var defaultStyleSection: some View {
        Section {
            let bound = Binding(
                get: { appState.bibliographyPrefs.defaultStyle ?? "" },
                set: { newPath in
                    var prefs = appState.bibliographyPrefs
                    prefs.defaultStyle = newPath.isEmpty ? nil : newPath
                    persist(prefs)
                }
            )
            Picker("Default style", selection: bound) {
                Text("None").tag("")
                ForEach(allConfiguredStyles(), id: \.self) { path in
                    Text(styleDisplayName(for: path)).tag(path)
                }
            }
            .pickerStyle(.menu)
            .accessibilityLabel("Default citation style")

            HStack {
                Button("Add style…") { addAdditionalStyle() }
                    .accessibilityHint(
                        "Opens a file picker to add a .csl style file."
                    )
                Spacer()
            }
        } header: {
            Text("Default style")
                .accessibilityAddTraits(.isHeader)
        } footer: {
            Text(
                "Renders citations against this style by default. Use View → Citation Style to switch styles on a specific note."
            )
            .font(.caption)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
    }

    // MARK: - Additional styles

    private var additionalStylesSection: some View {
        Section {
            if appState.bibliographyPrefs.additionalStyles.isEmpty {
                Text("No additional styles configured.")
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .accessibilityLabel("No additional styles configured.")
            } else {
                ForEach(
                    Array(appState.bibliographyPrefs.additionalStyles.enumerated()),
                    id: \.offset
                ) { index, path in
                    HStack {
                        Text(path)
                            .lineLimit(2)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .accessibilityLabel("Additional style: \(path).")
                        Button("Remove") {
                            var prefs = appState.bibliographyPrefs
                            if prefs.additionalStyles.indices.contains(index) {
                                prefs.additionalStyles.remove(at: index)
                                persist(prefs)
                            }
                        }
                        .accessibilityLabel("Remove style \(path)")
                    }
                }
            }
        } header: {
            Text("Additional styles")
                .accessibilityAddTraits(.isHeader)
        }
    }

    // MARK: - Actions

    private func addSource() {
        let panel = NSOpenPanel()
        panel.canChooseFiles = true
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false
        panel.allowedContentTypes = []
        panel.message = String(localized: "Choose a .bib or .json bibliography file")
        guard panel.runModal() == .OK, let url = panel.url else { return }
        let relPath = vaultRelative(url) ?? url.path
        let format = inferFormat(from: url)
        var prefs = appState.bibliographyPrefs
        prefs.sources.append(
            BibliographySource(path: relPath, format: format, watch: false)
        )
        persist(prefs)
    }

    private func addAdditionalStyle() {
        let panel = NSOpenPanel()
        panel.canChooseFiles = true
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false
        panel.message = String(localized: "Choose a .csl Citation Style Language file")
        guard panel.runModal() == .OK, let url = panel.url else { return }
        let relPath = vaultRelative(url) ?? url.path
        var prefs = appState.bibliographyPrefs
        if !prefs.additionalStyles.contains(relPath) {
            prefs.additionalStyles.append(relPath)
        }
        persist(prefs)
    }

    private func persist(_ prefs: BibliographyPrefs) {
        Task { await appState.applyBibliographyPrefs(prefs) }
    }

    // MARK: - Helpers

    private func allConfiguredStyles() -> [String] {
        var paths: [String] = []
        if let d = appState.bibliographyPrefs.defaultStyle, !d.isEmpty {
            paths.append(d)
        }
        for s in appState.bibliographyPrefs.additionalStyles {
            if !paths.contains(s) {
                paths.append(s)
            }
        }
        return paths
    }

    private func styleDisplayName(for path: String) -> String {
        let basename = (path as NSString).lastPathComponent
            .replacingOccurrences(of: ".csl", with: "")
        if let style = appState.availableCslStyles.first(where: { $0.id == basename })
        {
            return style.title
        }
        return basename
    }

    private func inferFormat(from url: URL) -> BibFormat {
        let ext = url.pathExtension.lowercased()
        if ext == "json" { return .cslJson }
        return .bibTeX
    }

    private func vaultRelative(_ url: URL) -> String? {
        guard let vault = appState.currentVaultURL else { return nil }
        let vaultPath = vault.standardizedFileURL.path
        let filePath = url.standardizedFileURL.path
        guard filePath.hasPrefix(vaultPath + "/") else { return nil }
        return String(filePath.dropFirst(vaultPath.count + 1))
    }
}


// MARK: - Canvas tab (Milestone T, #518)

/// Canvas announcement verbosity (t0 §1.2). Live-switchable: the
/// announcer phrases the very next event at the new level.
struct CanvasSettingsTab: View {
    @EnvironmentObject var appState: AppState

    var body: some View {
        Form {
            Section {
                Picker("Canvas announcement verbosity", selection: verbosityBinding) {
                    ForEach(CanvasVerbosity.allCases, id: \.self) { level in
                        Text(level.title).tag(level)
                    }
                }
                .pickerStyle(.segmented)
                .accessibilityElement(children: .contain)
                .accessibilityLabel("Canvas announcement verbosity")
            } footer: {
                Text(
                    "Terse announces card titles only. Standard adds position and container. Verbose adds connections, color, and marks. \"Where am I?\" (⌃⌘I) is always verbose."
                )
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            }
        }
        .formStyle(.grouped)
        .accessibilityLabel("Canvas settings")
    }

    private var verbosityBinding: Binding<CanvasVerbosity> {
        Binding(
            get: { appState.canvasAnnouncer.verbosity },
            set: { appState.setCanvasVerbosity($0) }
        )
    }
}


// MARK: - History tab (Milestone O-5, #543)

/// Retention window (per-vault, persisted to `.slate/prefs.json`
/// through the session so unknown keys survive) + the since-open
/// toggle (host preference — UI + mark writes only).
struct HistorySettingsTab: View {
    @EnvironmentObject var appState: AppState
    @State private var retentionDays: UInt32 = 90

    /// The picker's fixed menu (o_spec §O-5): 30 / 90 (default) /
    /// 180 / 365 days.
    static let retentionChoices: [UInt32] = [30, 90, 180, 365]

    static let sinceOpenFooter =
        "Adds a summary of what changed to the History panel when you open a note."

    var body: some View {
        Form {
            Section {
                // Persistence rides the binding SETTER — only a user
                // gesture goes through it. Programmatic reseeds (vault
                // switch below) write the @State directly, so there is
                // no suppression flag to get out of sync under
                // coalesced or rapid switches (adversarial round 2).
                Picker(
                    "Keep edit history for",
                    selection: Binding(
                        get: { retentionDays },
                        set: { newValue in
                            retentionDays = newValue
                            Task {
                                await appState.applyHistoryRetention(days: newValue)
                            }
                        }
                    )
                ) {
                    ForEach(Self.retentionChoices, id: \.self) { days in
                        Text("\(days) days").tag(days)
                    }
                }
                .disabled(!appState.isVaultOpen)
            } footer: {
                if !appState.isVaultOpen {
                    Text("Open a vault to change its history retention.")
                }
            }
            Section {
                Toggle(
                    "Show changes since last open",
                    isOn: Binding(
                        get: { appState.historyShowChangesSinceOpen },
                        set: { appState.setHistoryShowChangesSinceOpen($0) }
                    )
                )
            } footer: {
                Text(Self.sinceOpenFooter)
            }
        }
        .formStyle(.grouped)
        .onAppear {
            retentionDays = appState.currentHistoryRetentionDays()
        }
        .onChange(of: appState.currentVaultURL) { _, _ in
            // Settings can stay mounted across a vault switch; reseed
            // from the NEW session (or back to the default when the
            // vault closed). Direct @State write — the persistence
            // path is the picker binding's setter, which only user
            // gestures reach.
            retentionDays = appState.currentHistoryRetentionDays()
        }
        // #862: the per-tab `.navigationTitle("History")` was removed —
        // it diverged from every other pane (which showed the
        // container's title). The single window title now lives once on
        // the SettingsView container.
    }
}
