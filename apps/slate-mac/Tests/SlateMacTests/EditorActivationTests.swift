// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Combine
import SwiftUI
import XCTest

@testable import SlateMac

/// #1431: the editor's activate-at-cursor verb, the mac twin of Windows'
/// `slate.editor.activateAtCaret` (`EditorInteractions.ActivateAt`;
/// `W2EditorInteractionTests`). Four layers: the pure rule
/// (`EditorActivation`), the resolution against core's spans for a live
/// buffer, AppState's actions over a real vault, and the editor's triggers
/// (⌘↩, the activation request, the text area's and the link element's
/// presses, focus). XCTest
/// proves where an activation lands; the PR's field pass records the speech.
@MainActor
final class EditorActivationTests: XCTestCase {

    final class RecordingAnnouncer: AnnouncementPosting, @unchecked Sendable {
        var posts: [String] = []
        func post(_ message: String, priority: AnnouncementPriority) { posts.append(message) }
    }

    private typealias Target = EditorActivation.Target
    private var tempDirs: [URL] = []

    override func tearDown() {
        for dir in tempDirs { try? FileManager.default.removeItem(at: dir) }
        tempDirs = []
        super.tearDown()
    }

    private static func rendered(_ event: A11yEvent) -> String { a11yRender(event: event).text }

    // MARK: - The rule (pure)

    private static let spans: [(range: NSRange, kind: EditorSpanKind)] = [
        (NSRange(location: 0, length: 40), .link),  // [outer [[inner]]](https://…)
        (NSRange(location: 7, length: 9), .wikilink),  // [[inner]]
        (NSRange(location: 50, length: 6), .tag),
        (NSRange(location: 60, length: 20), .codeFence),
        (NSRange(location: 64, length: 5), .tag),  // a "tag" inside the fence
    ]

    /// The innermost actionable span containing the location wins (E-9's
    /// tree: the wikilink nested in a Markdown link's label is the inner
    /// child), and a code fence around the location activates nothing.
    func testTheInnermostSpanWinsAndACodeFenceSuppressesActivation() {
        XCTAssertEqual(
            EditorActivation.target(at: 9, origin: .keyboard, spans: Self.spans, mathRanges: []),
            .link(range: NSRange(location: 7, length: 9), kind: .wikilink))
        XCTAssertEqual(
            EditorActivation.target(at: 3, origin: .keyboard, spans: Self.spans, mathRanges: []),
            .link(range: NSRange(location: 0, length: 40), kind: .link))
        XCTAssertEqual(
            EditorActivation.target(at: 52, origin: .keyboard, spans: Self.spans, mathRanges: []),
            .tag(range: NSRange(location: 50, length: 6)))
        XCTAssertEqual(
            EditorActivation.target(at: 66, origin: .keyboard, spans: Self.spans, mathRanges: []),
            .task(location: 66, origin: .keyboard),
            "inside a fence nothing is actionable, so the keyboard falls back to the line's task")
    }

    /// Display math activates nothing at all, before any span is read
    /// (Windows returns before `TryGetActionableSpan`).
    func testDisplayMathActivatesNothing() {
        let math = [NSRange(location: 45, length: 20)]
        XCTAssertEqual(
            EditorActivation.target(at: 52, origin: .keyboard, spans: Self.spans, mathRanges: math),
            .nothing)
        XCTAssertEqual(
            EditorActivation.target(at: 52, origin: .press, spans: Self.spans, mathRanges: math),
            .nothing)
    }

    /// The caret just past a span's last character still activates it from
    /// the keyboard (the caret sits after `]]` once a link is typed), never
    /// from a press or the pointer; a location strictly inside a span beats
    /// one that only touches another's end.
    func testTheCaretJustPastASpanCountsFromTheKeyboardOnly() {
        let spans: [(range: NSRange, kind: EditorSpanKind)] = [
            (NSRange(location: 0, length: 6), .tag), (NSRange(location: 6, length: 9), .wikilink),
        ]
        XCTAssertEqual(
            EditorActivation.target(at: 15, origin: .keyboard, spans: spans, mathRanges: []),
            .link(range: NSRange(location: 6, length: 9), kind: .wikilink))
        XCTAssertEqual(
            EditorActivation.target(at: 15, origin: .press, spans: spans, mathRanges: []), .nothing)
        XCTAssertEqual(
            EditorActivation.target(at: 15, origin: .pointer, spans: spans, mathRanges: []),
            .task(location: 15, origin: .pointer))
        XCTAssertEqual(
            EditorActivation.target(at: 6, origin: .keyboard, spans: spans, mathRanges: []),
            .link(range: NSRange(location: 6, length: 9), kind: .wikilink),
            "strictly inside the wikilink beats touching the tag's end")
    }

    /// The six E-9 kinds are the actionable ones, each to its own target.
    func testEachActionableKindHasItsTarget() {
        let range = NSRange(location: 0, length: 4)
        func target(_ kind: EditorSpanKind) -> Target {
            EditorActivation.target(
                at: 1, origin: .press, spans: [(range, kind)], mathRanges: [])
        }
        XCTAssertEqual(target(.wikilink), .link(range: range, kind: .wikilink))
        XCTAssertEqual(target(.link), .link(range: range, kind: .link))
        XCTAssertEqual(target(.image), .link(range: range, kind: .image))
        XCTAssertEqual(target(.embed), .embed(range: range))
        XCTAssertEqual(target(.tag), .tag(range: range))
        XCTAssertEqual(target(.citation), .citation(range: range))
        for kind: EditorSpanKind in [.heading(level: 2), .emphasis, .inlineCode, .blockQuote, .comment] {
            XCTAssertEqual(target(kind), .nothing, "\(kind) is not activatable")
        }
    }

    // MARK: - Resolution against core's spans

    /// Windows' interaction fixture (`InteractionFixture.Create`), the
    /// source note's body.
    static let source = """
        # Source

        [[target#Destination]]
        [[target^block-id]]
        #project
        [@doe]
        ![[target#Destination]]
        - [ ] task

        ```text
        #not-a-tag
        ```

        $$
        #not-math
        $$
        """

    /// A location two characters into `needle` (Windows' `Inside`).
    static func inside(_ text: String, _ needle: String) -> Int {
        let range = (text as NSString).range(of: needle)
        precondition(range.location != NSNotFound, "fixture token is missing: \(needle)")
        return range.location + min(2, range.length - 1)
    }

    static func resolve(
        _ text: String, at location: Int, origin: EditorActivation.Origin = .keyboard
    ) -> EditorActivationRequest {
        NoteEditorView.Coordinator.resolveActivation(
            at: location, origin: origin, buffer: DocumentBuffer(text: text), snapshot: text)
    }

    /// Every kind resolves from core's own spans and display-math ranges,
    /// with the live buffer's facts the host needs: the span's source and
    /// bytes, the line, and an embed's ⌘E target.
    func testResolutionReadsCoresSpansAndMathForEveryKind() {
        let text = Self.source
        func at(_ needle: String) -> EditorActivationRequest {
            Self.resolve(text, at: Self.inside(text, needle))
        }
        let wikilink = at("[[target#Destination]]")
        guard case .link(_, .wikilink) = wikilink.target else {
            return XCTFail("a wikilink: \(wikilink)")
        }
        XCTAssertEqual(wikilink.spanText, "[[target#Destination]]")
        let ns = text as NSString
        let start = ns.substring(to: ns.range(of: "[[target#Destination]]").location).utf8.count
        XCTAssertEqual(wikilink.spanBytes, start..<(start + "[[target#Destination]]".utf8.count))
        XCTAssertEqual(wikilink.line, 3)

        guard case .tag = at("#project").target else { return XCTFail("a tag") }
        XCTAssertEqual(at("#project").spanText, "#project")
        guard case .citation = at("[@doe]").target else { return XCTFail("a citation") }
        XCTAssertEqual(at("[@doe]").spanText, "[@doe]")
        let embed = at("![[target#Destination]]")
        guard case .embed = embed.target else { return XCTFail("an embed: \(embed)") }
        XCTAssertEqual(embed.embedTarget, "target#Destination")
        guard case .task(_, .keyboard) = at("- [ ] task").target else { return XCTFail("a task line") }
        XCTAssertEqual(at("- [ ] task").line, 8)
        guard case .task = at("#not-a-tag").target else {
            return XCTFail("a fenced tag is not actionable: \(at("#not-a-tag"))")
        }
        XCTAssertEqual(at("#not-math").target, .nothing, "display math activates nothing")
    }

    // MARK: - The actions, over a real vault

    private func makeVault(source: String = EditorActivationTests.source) throws -> URL {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-editor-activation-\(UUID().uuidString)")
        tempDirs.append(dir)
        let vault = dir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# Lead\n\n## Destination\n\nSection body.\n\nBlock body ^block-id\n".utf8)
            .write(to: vault.appendingPathComponent("target.md"))
        try Data(source.utf8).write(to: vault.appendingPathComponent("source.md"))
        return vault
    }

    private func makeAppState(
        announcer: RecordingAnnouncer, externalOpener: @escaping (URL) -> Bool = { _ in true }
    ) throws -> AppState {
        let dir = try XCTUnwrap(tempDirs.last)
        return AppState(
            recentsStore: RecentVaultsStore(fileURL: dir.appendingPathComponent("recents.json")),
            externalOpener: externalOpener,
            preferencesStore: PreferencesStore(defaults: UserDefaults(suiteName: UUID().uuidString)!),
            announcer: announcer)
    }

    /// Open the vault and `source.md`, with every artifact an activation
    /// reads loaded.
    private func openSource(_ state: AppState, vault: URL) async {
        state.openVault(at: vault)
        await state.scanTask?.value
        await select("source.md", in: state)
    }

    private func select(_ path: String, in state: AppState) async {
        state.selectedFilePath = path
        await state.noteLoadTask?.value
        await state.linksLoadTask?.value
        await state.tasksLoadTask?.value
        await state.citationsLoadTask?.value
        await state.embedsLoadTask?.value
    }

    private func activate(_ needle: String, in state: AppState) {
        let text = state.currentNoteText ?? ""
        state.performEditorActivation(Self.resolve(text, at: Self.inside(text, needle)))
    }

    /// The twin of Windows' `CoreBackedActions_CoverLinksTagsCitationsEmbedsTasksAndProtectedRegions`:
    /// a wikilink navigates (current tab), a tag filters the Files sidebar,
    /// a citation opens its popover, an embed its preview, a fenced or math
    /// "tag" activates nothing and says so, and a task toggles on disk and
    /// says what it became, both ways.
    func testCoreBackedActionsCoverLinksTagsCitationsEmbedsTasksAndProtectedRegions() async throws {
        let vault = try makeVault()
        let announcer = RecordingAnnouncer()
        let state = try makeAppState(announcer: announcer)
        await openSource(state, vault: vault)
        XCTAssertNotNil(state.currentNoteText)

        activate("[[target#Destination]]", in: state)
        XCTAssertEqual(state.lastActivatedLinkOutcome, .openedInternal("target.md"))
        XCTAssertEqual(state.selectedFilePath, "target.md")
        await select("source.md", in: state)

        activate("#project", in: state)
        XCTAssertEqual(state.sidebarFilterModel.fieldText, "#project")

        activate("[@doe]", in: state)
        XCTAssertEqual(state.expandedCitation?.raw, "[@doe]")
        XCTAssertFalse(state.expandedCitationRowAnchored)
        state.expandedCitation = nil

        activate("![[target#Destination]]", in: state)
        XCTAssertEqual(state.pendingEmbedPreview?.target, "target#Destination")
        state.dismissEmbedPreview()

        let nothing = Self.rendered(.nothingToActivateAtCursor)
        announcer.posts.removeAll()
        activate("#not-a-tag", in: state)
        activate("#not-math", in: state)
        XCTAssertEqual(announcer.posts, [nothing, nothing], "a fence or math says there is nothing")
        XCTAssertEqual(state.sidebarFilterModel.fieldText, "#project", "no tag activated")

        activate("- [ ] task", in: state)
        await state.taskToggleTask?.value
        let onDisk = try String(
            contentsOf: vault.appendingPathComponent("source.md"), encoding: .utf8)
        XCTAssertTrue(onDisk.contains("- [x] task"), onDisk)
        XCTAssertEqual(announcer.posts.last, Self.rendered(.taskToggled(completed: true)))

        activate("- [x] task", in: state)
        await state.taskToggleTask?.value
        let reopened = try String(
            contentsOf: vault.appendingPathComponent("source.md"), encoding: .utf8)
        XCTAssertTrue(reopened.contains("- [ ] task"), reopened)
        XCTAssertEqual(announcer.posts.last, Self.rendered(.taskToggled(completed: false)))
    }

    /// A toggle whose write fails says so in core's words, with the error,
    /// rather than leaving the task as it was in silence.
    func testATaskWhoseWriteFailsSaysItCouldNotBeToggled() async throws {
        let vault = try makeVault()
        let announcer = RecordingAnnouncer()
        let state = try makeAppState(announcer: announcer)
        await openSource(state, vault: vault)
        let fm = FileManager.default
        try fm.setAttributes([.posixPermissions: 0o555], ofItemAtPath: vault.path)
        try fm.setAttributes(
            [.posixPermissions: 0o444], ofItemAtPath: vault.appendingPathComponent("source.md").path)
        defer {
            try? fm.setAttributes(
                [.posixPermissions: 0o644],
                ofItemAtPath: vault.appendingPathComponent("source.md").path)
            try? fm.setAttributes([.posixPermissions: 0o755], ofItemAtPath: vault.path)
        }
        announcer.posts.removeAll()

        activate("- [ ] task", in: state)
        await state.taskToggleTask?.value

        let onDisk = try String(
            contentsOf: vault.appendingPathComponent("source.md"), encoding: .utf8)
        XCTAssertTrue(onDisk.contains("- [ ] task"), "the write failed: \(onDisk)")
        let detail = try XCTUnwrap(state.tasksLoadError)
        XCTAssertEqual(announcer.posts, [Self.rendered(.taskToggleFailed(detail: detail))])
    }

    /// The twin of Windows' `ActivationAtTheCaretFollowsTheInnermostLinkAsTheHyperlinkTreeDoes`.
    func testActivationAtTheCaretFollowsTheInnermostLinkAsTheHyperlinkTreeDoes() async throws {
        let authored = "[outer [[target#Destination]]](https://example.org/outer)\n"
        let vault = try makeVault(source: authored)
        var opened: [URL] = []
        let state = try makeAppState(
            announcer: RecordingAnnouncer(), externalOpener: { opened.append($0); return true })
        await openSource(state, vault: vault)

        activate("[[target#Destination]]", in: state)
        XCTAssertEqual(state.lastActivatedLinkOutcome, .openedInternal("target.md"))
        XCTAssertEqual(opened, [])
        await select("source.md", in: state)

        activate("outer ", in: state)
        XCTAssertEqual(opened, [URL(string: "https://example.org/outer")!])
        XCTAssertEqual(state.lastActivatedLinkOutcome, .openedExternal("https://example.org/outer"))
    }

    /// An unsaved edit above a link moves it off its saved record's bytes:
    /// the link still follows by its target, as the reading view resolves
    /// the live buffer, and an unsaved link to nowhere says it is
    /// unresolved in core's words.
    func testAnEditedBufferFollowsByTargetAndAnUnsavedLinkSaysUnresolved() async throws {
        let vault = try makeVault()
        let announcer = RecordingAnnouncer()
        let state = try makeAppState(announcer: announcer)
        await openSource(state, vault: vault)
        let edited = "Inserted line with [[nowhere]].\n\n" + (state.currentNoteText ?? "")
        state.updateEditorText(edited)
        XCTAssertTrue(state.hasUnsavedChanges)

        let moved = Self.resolve(edited, at: Self.inside(edited, "[[target#Destination]]"))
        XCTAssertNil(state.editorLinkRecord(for: moved, embed: false), "the saved bytes moved")
        announcer.posts.removeAll()
        activate("[[nowhere]]", in: state)
        XCTAssertEqual(announcer.posts, [Self.rendered(.linkUnresolved(target: "nowhere"))])
    }

    /// The command with no editor on screen has no cursor, and says so.
    func testActivateAtCursorWithNoEditorMountedSaysThereIsNothingToActivate() throws {
        _ = try makeVault()
        let announcer = RecordingAnnouncer()
        let state = try makeAppState(announcer: announcer)
        XCTAssertFalse(state.isNoteEditorMounted)
        state.activateAtCursor()
        XCTAssertEqual(announcer.posts, [Self.rendered(.nothingToActivateAtCursor)])
    }

    /// The command: Windows' id, label and hint, ⌘↩, in the Editor section,
    /// and through the registry it runs the verb.
    func testTheCommandIsRegisteredWithWindowsIdLabelHintAndCommandReturn() throws {
        _ = try makeVault()
        let announcer = RecordingAnnouncer()
        let state = try makeAppState(announcer: announcer)
        let command = try XCTUnwrap(state.commandRegistry.findById(id: "slate.editor.activateAtCaret"))
        XCTAssertEqual(command.label, "Activate at Cursor")
        XCTAssertEqual(command.hotkeyHint, "⌘↩")
        XCTAssertEqual(
            command.accessibilityHint,
            "Activate the link, tag, citation, embed, or task under the cursor.")
        XCTAssertEqual(command.section, .editor)
        XCTAssertNil(state.invokeRegisteredCommand(SlateCommandID.activateAtCaret))
        XCTAssertEqual(announcer.posts, [Self.rendered(.nothingToActivateAtCursor)])
        XCTAssertEqual(HotkeySpoken.spoken(for: "⌘↩"), "Command Return")
    }

    // MARK: - The editor's triggers

    private final class Activations {
        var requests: [EditorActivationRequest] = []
        var focus: [Bool] = []
    }

    /// A live, windowed editor whose activations and focus reports are
    /// recorded, with one whole-document highlight settled.
    private func makeEditor(
        _ text: String, wired: Bool = true
    ) async throws -> (NoteEditorView.Coordinator, SlateEditorTextView, NSWindow, Activations) {
        let recorded = Activations()
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 640, height: 480), styleMask: [.titled],
            backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false
        let textView = SlateEditorTextView(frame: NSRect(x: 0, y: 0, width: 640, height: 480))
        textView.isRichText = false
        textView.string = text
        window.contentView = textView
        let coordinator = NoteEditorView.Coordinator(
            text: Binding(get: { text }, set: { _ in }), onSave: {}, previewEmbedAtCursor: nil)
        if wired {
            coordinator.onActivate = { recorded.requests.append($0) }
            coordinator.onEditorFocusChange = { recorded.focus.append($0) }
        }
        textView.coordinator = coordinator
        coordinator.attach(textView: textView)
        coordinator.scheduleHighlight(debounced: false)
        let applied = await coordinator.settleHighlight()
        XCTAssertTrue(applied, "the seed pass painted")
        return (coordinator, textView, window, recorded)
    }

    private func waitForActivation(_ recorded: Activations, count: Int = 1) async throws {
        let deadline = ContinuousClock.now.advanced(by: .seconds(10))
        while recorded.requests.count < count, ContinuousClock.now < deadline {
            try await Task.sleep(for: .milliseconds(10))
        }
        XCTAssertEqual(recorded.requests.count, count, "the activation reached onActivate")
    }

    private static let editorText = "A paragraph with [[Wiki Target]] and #alpha here.\n"

    private static func commandReturn(
        in window: NSWindow, flags: NSEvent.ModifierFlags = .command, keyCode: UInt16 = 36
    ) -> NSEvent {
        let enter = keyCode == 76 ? "\u{3}" : "\r"
        return NSEvent.keyEvent(
            with: .keyDown, location: .zero, modifierFlags: flags, timestamp: 0,
            windowNumber: window.windowNumber, context: nil, characters: enter,
            charactersIgnoringModifiers: enter, isARepeat: false, keyCode: keyCode)!
    }

    /// ⌘↩ in the focused editor activates the span at the caret, and is
    /// left alone when the editor does not hold keyboard focus (⌘↩ there
    /// belongs to the focused surface).
    func testCommandReturnInTheFocusedEditorActivatesAtTheCaret() async throws {
        let (coordinator, textView, window, recorded) = try await makeEditor(Self.editorText)
        defer { window.close(); withExtendedLifetime(coordinator) {} }
        textView.setSelectedRange(
            NSRange(location: Self.inside(Self.editorText, "[[Wiki Target]]"), length: 0))

        window.makeFirstResponder(nil)
        XCTAssertFalse(textView.performKeyEquivalent(with: Self.commandReturn(in: window)))
        XCTAssertTrue(window.makeFirstResponder(textView))
        XCTAssertTrue(textView.performKeyEquivalent(with: Self.commandReturn(in: window)))
        try await waitForActivation(recorded)
        guard case .link(_, .wikilink) = recorded.requests.first?.target else {
            return XCTFail("the caret's wikilink: \(recorded.requests)")
        }
        XCTAssertEqual(recorded.requests.first?.spanText, "[[Wiki Target]]")
    }

    /// The chord is ⌘ with Return or the keypad's Enter, whatever Caps Lock
    /// says; another modifier beside ⌘ makes it some other chord.
    func testCommandEnterAndCapsLockStillActivateAndAnotherModifierDoesNot() async throws {
        let (coordinator, textView, window, recorded) = try await makeEditor(Self.editorText)
        defer { window.close(); withExtendedLifetime(coordinator) {} }
        textView.setSelectedRange(
            NSRange(location: Self.inside(Self.editorText, "[[Wiki Target]]"), length: 0))
        XCTAssertTrue(window.makeFirstResponder(textView))

        XCTAssertFalse(
            textView.performKeyEquivalent(
                with: Self.commandReturn(in: window, flags: [.command, .shift])))
        XCTAssertFalse(
            textView.performKeyEquivalent(
                with: Self.commandReturn(in: window, flags: [.command, .option])))
        XCTAssertTrue(
            textView.performKeyEquivalent(
                with: Self.commandReturn(in: window, flags: [.command, .capsLock])))
        try await waitForActivation(recorded)
        XCTAssertTrue(
            textView.performKeyEquivalent(
                with: Self.commandReturn(
                    in: window, flags: [.command, .numericPad, .function], keyCode: 76)))
        try await waitForActivation(recorded, count: 2)
        XCTAssertEqual(recorded.requests.map(\.spanText), ["[[Wiki Target]]", "[[Wiki Target]]"])
    }

    /// VoiceOver presses the text area itself: VO-Command-L moves the
    /// insertion point onto a link and VO-Space presses the element under
    /// VoiceOver's cursor, which is the text area. It offers that press only
    /// while the insertion point is inside a link (elsewhere VO-Space keeps
    /// the text view's own actions), and the press activates the link.
    func testTheTextAreaOffersAPressOnlyInsideALinkAndThePressActivatesIt() async throws {
        let (coordinator, textView, window, recorded) = try await makeEditor(Self.editorText)
        defer { window.close(); withExtendedLifetime(coordinator) {} }
        let press = #selector(NSView.accessibilityPerformPress)

        textView.setSelectedRange(NSRange(location: 2, length: 0))
        XCTAssertFalse(textView.isAccessibilitySelectorAllowed(press), "plain prose offers no press")
        XCTAssertFalse(textView.accessibilityPerformPress())

        // Where Find Next Link leaves the insertion point: the link's start.
        let link = (Self.editorText as NSString).range(of: "[[Wiki Target]]")
        textView.setSelectedRange(NSRange(location: NSMaxRange(link), length: 0))
        XCTAssertFalse(
            textView.isAccessibilitySelectorAllowed(press), "just past the link is not on it")
        textView.setSelectedRange(NSRange(location: link.location, length: 0))
        XCTAssertTrue(textView.isAccessibilitySelectorAllowed(press))
        XCTAssertTrue(textView.accessibilityPerformPress())
        try await waitForActivation(recorded)
        guard case .link(_, .wikilink) = recorded.requests.first?.target else {
            return XCTFail("the wikilink under the insertion point: \(recorded.requests)")
        }
        XCTAssertEqual(recorded.requests.first?.spanText, "[[Wiki Target]]")

        let tag = (Self.editorText as NSString).range(of: "#alpha")
        textView.setSelectedRange(NSRange(location: tag.location + 1, length: 0))
        XCTAssertTrue(textView.isAccessibilitySelectorAllowed(press), "a tag is a link")
    }

    /// The palette row and the Edit-menu item reach the editor through the
    /// activation request.
    func testTheActivationRequestActivatesAtTheCaret() async throws {
        let (coordinator, textView, window, recorded) = try await makeEditor(Self.editorText)
        defer { window.close(); withExtendedLifetime(coordinator) {} }
        let request = PassthroughSubject<Void, Never>()
        coordinator.subscribe(
            scrollAnchorRequest: Empty().eraseToAnyPublisher(),
            lineScrollRequest: Empty().eraseToAnyPublisher(),
            findInNoteRequest: Empty().eraseToAnyPublisher(),
            cursorByteOffsetRequest: Empty().eraseToAnyPublisher(),
            activateAtCursorRequest: request.eraseToAnyPublisher())
        textView.setSelectedRange(NSRange(location: Self.inside(Self.editorText, "#alpha"), length: 0))
        request.send()
        try await waitForActivation(recorded)
        XCTAssertEqual(
            recorded.requests.first?.target,
            .tag(range: (Self.editorText as NSString).range(of: "#alpha")))
    }

    /// VO-Space on an editor link: VoiceOver presses the link element the
    /// text's AXLink run carries (PR 17's spike), and the press activates
    /// that link wherever the caret is. Tags and citations are links too.
    func testAPressOnAnEditorLinkElementActivatesThatLink() async throws {
        let (coordinator, textView, window, recorded) = try await makeEditor(Self.editorText)
        defer { window.close(); withExtendedLifetime(coordinator) {} }
        textView.setSelectedRange(NSRange(location: 0, length: 0))
        let ns = Self.editorText as NSString
        let ax = try XCTUnwrap(
            textView.accessibilityAttributedString(for: NSRange(location: 0, length: ns.length)))
        func element(_ needle: String) throws -> EditorLinkAccessibilityElement {
            try XCTUnwrap(
                ax.attribute(.accessibilityLink, at: ns.range(of: needle).location + 1, effectiveRange: nil)
                    as? EditorLinkAccessibilityElement, "\(needle) carries a link element")
        }

        XCTAssertTrue(try element("[[Wiki Target]]").accessibilityPerformPress())
        try await waitForActivation(recorded)
        XCTAssertEqual(
            recorded.requests.last?.target,
            .link(range: ns.range(of: "[[Wiki Target]]"), kind: .wikilink))

        XCTAssertTrue(try element("#alpha").accessibilityPerformPress())
        try await waitForActivation(recorded, count: 2)
        XCTAssertEqual(recorded.requests.last?.target, .tag(range: ns.range(of: "#alpha")))
    }

    /// An editor with no activation (an inactive pane) refuses the press,
    /// so VoiceOver is not told a link was followed.
    func testAnEditorWithNoActivationRefusesThePress() async throws {
        let (coordinator, textView, window, _) = try await makeEditor(Self.editorText, wired: false)
        defer { window.close(); withExtendedLifetime(coordinator) {} }
        let ns = Self.editorText as NSString
        let ax = try XCTUnwrap(
            textView.accessibilityAttributedString(for: NSRange(location: 0, length: ns.length)))
        let link = try XCTUnwrap(
            ax.attribute(.accessibilityLink, at: ns.range(of: "[[Wiki").location + 1, effectiveRange: nil)
                as? EditorLinkAccessibilityElement)
        XCTAssertFalse(link.accessibilityPerformPress())
    }

    /// The editor reports gaining and losing keyboard focus, which the
    /// Edit-menu item's enablement follows.
    func testTheEditorReportsItsKeyboardFocus() async throws {
        let (coordinator, textView, window, recorded) = try await makeEditor(Self.editorText)
        defer { window.close(); withExtendedLifetime(coordinator) {} }
        recorded.focus.removeAll()
        XCTAssertTrue(window.makeFirstResponder(textView))
        XCTAssertTrue(window.makeFirstResponder(nil))
        XCTAssertEqual(recorded.focus, [true, false])
    }
}
