// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Combine
import XCTest

@testable import SlateMac

/// #796 — the new-note and template-create flows must never truncate
/// an existing file on a name race (a file created externally after
/// the scan, invisible to the unique-name picker). Both flows route
/// through `create_exclusive`, the O-3 no-clobber primitive.
@MainActor
final class CreateNoteRaceTests: XCTestCase {
    private var tempDirs: [URL] = []

    private actor SuspensionGate {
        private var entered = false
        private var entranceWaiters: [CheckedContinuation<Void, Never>] = []
        private var releaseWaiter: CheckedContinuation<Void, Never>?

        func enter() async {
            entered = true
            for waiter in entranceWaiters { waiter.resume() }
            entranceWaiters = []
            await withCheckedContinuation { releaseWaiter = $0 }
        }

        func waitUntilEntered() async {
            guard !entered else { return }
            await withCheckedContinuation { entranceWaiters.append($0) }
        }

        func release() {
            releaseWaiter?.resume()
            releaseWaiter = nil
        }
    }

    func addTempDir(_ dir: URL) {
        tempDirs.append(dir)
    }

    override func tearDown() {
        for dir in tempDirs {
            try? FileManager.default.removeItem(at: dir)
        }
        tempDirs = []
        super.tearDown()
    }

    private func openVault(plant: (URL) throws -> Void = { _ in }) async throws
        -> (AppState, URL)
    {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("create-race-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: dir, withIntermediateDirectories: true)
        tempDirs.append(dir)
        try plant(dir)
        let state = AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!)
        )
        state.openVault(at: dir)
        await state.scanTask?.value
        return (state, dir)
    }

    func testCreateNoteNeverTruncatesARacedExistingFile() async throws {
        let (state, dir) = try await openVault()

        // The race: the file appears on disk AFTER the scan, so the
        // unique-name picker (which only sees the index) chooses its
        // exact name.
        let raced = dir.appendingPathComponent("Untitled.md")
        try "precious contents\n".write(to: raced, atomically: true, encoding: .utf8)

        await state.createNote(in: "")?.value

        XCTAssertEqual(
            try String(contentsOf: raced, encoding: .utf8),
            "precious contents\n",
            "the raced file's bytes survive"
        )
        XCTAssertNotNil(state.lastError, "the collision surfaces as an error")
        XCTAssertTrue(
            state.lastError?.contains("already exists") == true,
            "the standard name-collision copy: \(state.lastError ?? "nil")"
        )
    }

    func testNewNoteRechecksOwnershipAfterRefreshBeforeLandingInSamePathNewVault()
        async throws
    {
        let (state, vaultA) = try await openVault()
        let vaultB = FileManager.default.temporaryDirectory
            .appendingPathComponent("create-race-vault-b-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: vaultB, withIntermediateDirectories: true)
        tempDirs.append(vaultB)
        try "# Existing in B\n".write(
            to: vaultB.appendingPathComponent("Untitled.md"),
            atomically: true,
            encoding: .utf8)

        let refresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await refresh.enter() }

        let oldCreate = try XCTUnwrap(state.requestCreateNote(in: ""))
        await refresh.waitUntilEntered()

        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vaultA.appendingPathComponent("Untitled.md").path),
            "vault A's native create finishes before the shared refresh")
        XCTAssertNil(
            state.lastMutationAnnouncement,
            "success must not be announced before refresh and ownership revalidation")

        state.openVault(at: vaultB)
        await state.scanTask?.value
        state.selectedFilePath = "Untitled.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.currentNoteText, "# Existing in B\n")

        await refresh.release()
        await oldCreate.value

        XCTAssertEqual(state.selectedFilePath, "Untitled.md")
        XCTAssertEqual(state.loadedFilePath, "Untitled.md")
        XCTAssertEqual(state.currentNoteText, "# Existing in B\n")
        XCTAssertNil(
            state.renamingNode,
            "vault A's stale create must not stage rename UI on vault B's same path")
        XCTAssertFalse(
            state.lastMutationAnnouncement?.contains("Created note") == true,
            "vault A's stale create must not announce success in vault B")
    }

    func testTemplateCreateNeverTruncatesARacedExistingFile() async throws {
        let (state, dir) = try await openVault { dir in
            let templates = dir.appendingPathComponent("Templates")
            try FileManager.default.createDirectory(
                at: templates, withIntermediateDirectories: true)
            try "template body\n".write(
                to: templates.appendingPathComponent("Note.md"),
                atomically: true, encoding: .utf8)
        }
        guard let session = state.currentSession else {
            return XCTFail("no session")
        }

        let raced = dir.appendingPathComponent("FromTemplate.md")
        try "precious contents\n".write(to: raced, atomically: true, encoding: .utf8)

        // Drive the same primitive the template flow calls (the flow's
        // rendering plumbing is covered elsewhere; the race contract is
        // the no-clobber write).
        XCTAssertThrowsError(
            try session.createExclusive(path: "FromTemplate.md", content: "template body\n")
        ) { error in
            guard case VaultError.DestinationExists = error else {
                return XCTFail("expected DestinationExists, got \(error)")
            }
        }
        XCTAssertEqual(
            try String(contentsOf: raced, encoding: .utf8),
            "precious contents\n"
        )
    }
}

extension CreateNoteRaceTests {
    /// The New-Canvas flow uses the exclusive write itself as its live
    /// collision probe. An externally-created first candidate survives and
    /// the flow advances to the next numbered name.
    @MainActor
    func testNewCanvasNeverTruncatesARacedExistingFile() async throws {
        let (state, dir) = try await openVaultForCanvasRace()

        let raced = dir.appendingPathComponent("Untitled Canvas.canvas")
        try #"{"nodes":[{"id":"keep"}]}"#.write(
            to: raced, atomically: true, encoding: .utf8)

        try await XCTUnwrap(state.canvasNewCanvasFile()).value
        XCTAssertEqual(
            try String(contentsOf: raced, encoding: .utf8),
            #"{"nodes":[{"id":"keep"}]}"#,
            "the raced canvas bytes survive"
        )
        XCTAssertEqual(
            try String(
                contentsOf: dir.appendingPathComponent("Untitled Canvas 2.canvas"),
                encoding: .utf8),
            "{}\n")
    }

    private func openVaultForCanvasRace() async throws -> (AppState, URL) {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("create-race-canvas-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: dir, withIntermediateDirectories: true)
        addTempDir(dir)
        let state = AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!)
        )
        state.openVault(at: dir)
        await state.scanTask?.value
        return (state, dir)
    }
}

// MARK: - The landed-but-unindexed create (#1140)

/// #1140 (#1123 follow-up; contract 30 TR-5): a create-if-absent write that
/// LANDS and then fails to index is a create. Each funnel finishes it as
/// one: its ordinary created sentence, then core's `CreatePublishedUnindexed`
/// caveat, the note open (Duplicate selects its copy instead, as it always
/// has), and never a failure presentation or a retry under another name.
/// Driven by core's `SLATE_TEST_FAULT_AFTER_WRITE` seam the way `save_text`'s
/// facts are (`AfterWriteFault`). Before #1140 every funnel caught the
/// landed arm as a failure (the "Could not create …" sentence, the error
/// alert, the template's name sheet) while the bytes sat on disk.
extension CreateNoteRaceTests {
    final class PostedAnnouncements: AnnouncementPosting, @unchecked Sendable {
        var posts: [(message: String, priority: AnnouncementPriority)] = []
        func post(_ message: String, priority: AnnouncementPriority) {
            posts.append((message, priority))
        }
    }

    /// Every structural-mutation sentence a funnel speaks, in order:
    /// `lastMutationAnnouncement` records each one as it is posted.
    private func recordMutationSentences(
        _ state: AppState
    ) -> (subscription: AnyCancellable, sentences: () -> [String]) {
        var sentences: [String] = []
        let subscription = state.$lastMutationAnnouncement
            .dropFirst()
            .compactMap { $0 }
            .sink { sentences.append($0) }
        return (subscription, { sentences })
    }

    /// A directory's visible entries (the `.slate` cache excluded), sorted.
    private func entries(of directory: URL) throws -> [String] {
        try FileManager.default.contentsOfDirectory(atPath: directory.path)
            .filter { !$0.hasPrefix(".") }
            .sorted()
    }

    /// The folder-note create runs on an untracked task; it is settled
    /// once the funnel releases its structural owner.
    private func waitUntilStructuralMutationEnds(_ state: AppState) async throws {
        var attempts = 0
        while state.isMutatingStructure, attempts < 1_000 {
            try await Task.sleep(nanoseconds: 10_000_000)
            attempts += 1
        }
        XCTAssertFalse(
            state.isMutatingStructure, "the funnel never released its structural owner")
    }

    private func openTemplatesVault(
        announcer: AnnouncementPosting
    ) async throws -> (AppState, URL) {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("create-landed-template-\(UUID().uuidString)")
        let templates = dir.appendingPathComponent("Templates")
        try FileManager.default.createDirectory(
            at: templates, withIntermediateDirectories: true)
        addTempDir(dir)
        try "template body\n".write(
            to: templates.appendingPathComponent("Note.md"),
            atomically: true, encoding: .utf8)
        let state = AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!),
            announcer: announcer
        )
        state.openVault(at: dir)
        await state.scanTask?.value
        return (state, dir)
    }

    /// Open the picker and choose its one template, so the name sheet is up.
    private func presentTheNameSheet(_ state: AppState) async throws {
        state.openTemplatePicker()
        await state.templatePickerTask?.value
        state.selectTemplate(try XCTUnwrap(state.availableTemplates.first))
        await state.templateSelectionTask?.value
        guard case .needsName = state.pendingTemplateFlow else {
            return XCTFail("expected the name sheet, got \(state.pendingTemplateFlow)")
        }
    }

    /// ⌘N's untitled note: `createNote(in:)`, named by `uniqueUntitledName`.
    func testAnUnindexedUntitledCreateFinishesAsACreate() async throws {
        let folder = "landed-\(UUID().uuidString)"
        let (state, dir) = try await openVault { dir in
            try FileManager.default.createDirectory(
                at: dir.appendingPathComponent(folder), withIntermediateDirectories: true)
        }
        let path = "\(folder)/Untitled.md"
        let (subscription, sentences) = recordMutationSentences(state)
        defer { subscription.cancel() }

        await AfterWriteFault.during(folder) {
            await state.createNote(in: folder)?.value
        }
        await state.noteLoadTask?.value

        // The note exists: the landed bytes, on disk and through core.
        XCTAssertTrue(
            FileManager.default.fileExists(atPath: dir.appendingPathComponent(path).path))
        XCTAssertEqual(try state.currentSession?.readText(path: path), "")
        // The created sentence, then the caveat: in that order, nothing else.
        XCTAssertEqual(
            sentences(),
            ["Created note Untitled.md.", AfterWriteFault.caveat(for: "Untitled.md")])
        // The note is open, and the flow finished as a committed create
        // does: the funnel's own scan indexed it, the inline rename is up.
        XCTAssertEqual(state.selectedFilePath, path)
        XCTAssertEqual(state.loadedFilePath, path)
        XCTAssertTrue(state.files.contains { $0.path == path })
        XCTAssertEqual(state.renamingNode?.path, path)
        // No second create: the folder holds the one landed note.
        XCTAssertEqual(try entries(of: dir.appendingPathComponent(folder)), ["Untitled.md"])
        // Never presented as a failure (the error alert stays down).
        XCTAssertNil(state.lastError)
    }

    /// When the funnel's refresh does not index the landed note either (a
    /// persistent index failure, here a refresh that does nothing), the
    /// create still finishes as one, but the rename hand-off waits: a rename
    /// owner with no row to render on would strand invisibly and switch off
    /// the tree's keys.
    func testAnUnindexedUntitledCreateTheRefreshCannotIndexStagesNoRename() async throws {
        let folder = "landed-\(UUID().uuidString)"
        let (state, dir) = try await openVault { dir in
            try FileManager.default.createDirectory(
                at: dir.appendingPathComponent(folder), withIntermediateDirectories: true)
        }
        state.structuralBatchRefreshRunner = { _ in }
        let path = "\(folder)/Untitled.md"
        let (subscription, sentences) = recordMutationSentences(state)
        defer { subscription.cancel() }

        await AfterWriteFault.during(folder) {
            await state.createNote(in: folder)?.value
        }

        XCTAssertTrue(
            FileManager.default.fileExists(atPath: dir.appendingPathComponent(path).path))
        XCTAssertEqual(
            sentences(),
            ["Created note Untitled.md.", AfterWriteFault.caveat(for: "Untitled.md")])
        XCTAssertEqual(state.selectedFilePath, path)
        XCTAssertFalse(state.files.contains { $0.path == path }, "the index never learned it")
        XCTAssertNil(state.renamingNode, "no rename owner without a row to render on")
        XCTAssertNil(state.lastError)
    }

    /// The folder note (`createFolderNote(at:)`, FL6-1 rule 4).
    func testAnUnindexedFolderNoteCreateFinishesAsACreate() async throws {
        let folder = "Project-\(UUID().uuidString)"
        let leaf = "\(folder).md"
        let path = "\(folder)/\(leaf)"
        let (state, dir) = try await openVault { dir in
            try FileManager.default.createDirectory(
                at: dir.appendingPathComponent(folder), withIntermediateDirectories: true)
        }
        let (subscription, sentences) = recordMutationSentences(state)
        defer { subscription.cancel() }

        try await AfterWriteFault.during(leaf) {
            XCTAssertTrue(state.createFolderNote(at: path))
            try await waitUntilStructuralMutationEnds(state)
        }
        await state.noteLoadTask?.value

        XCTAssertTrue(
            FileManager.default.fileExists(atPath: dir.appendingPathComponent(path).path))
        XCTAssertEqual(try state.currentSession?.readText(path: path), "")
        XCTAssertEqual(
            sentences(), ["Created note \(leaf).", AfterWriteFault.caveat(for: leaf)])
        XCTAssertEqual(state.selectedFilePath, path)
        XCTAssertEqual(state.loadedFilePath, path)
        XCTAssertEqual(try entries(of: dir.appendingPathComponent(folder)), [leaf])
        XCTAssertNil(state.lastError)
    }

    /// Duplicate's candidate loop (`duplicateEntry(path:)`): the landed copy
    /// ends the loop — it is never the namer's advance signal.
    func testAnUnindexedDuplicateFinishesAsACreate() async throws {
        let stem = "Dup-\(UUID().uuidString)"
        let source = "\(stem).md"
        let copy = "\(stem) copy.md"
        let (state, dir) = try await openVault { dir in
            try "original\n".write(
                to: dir.appendingPathComponent(source), atomically: true, encoding: .utf8)
        }
        let (subscription, sentences) = recordMutationSentences(state)
        defer { subscription.cancel() }

        await AfterWriteFault.during("\(stem) copy") {
            await state.duplicateEntry(path: source)?.value
        }

        XCTAssertEqual(
            try String(contentsOf: dir.appendingPathComponent(copy), encoding: .utf8),
            "original\n")
        XCTAssertEqual(try state.currentSession?.readText(path: copy), "original\n")
        XCTAssertEqual(
            sentences(),
            ["Duplicated \(source) as \(copy).", AfterWriteFault.caveat(for: copy)])
        // The copy is the selection's landing, as for a committed duplicate,
        // and the funnel's refresh indexed it.
        XCTAssertEqual(state.treeMutation?.kind, .createNote(path: copy))
        XCTAssertTrue(state.files.contains { $0.path == copy })
        // No second create: no "copy 2" was written.
        XCTAssertEqual(try entries(of: dir), [source, copy].sorted())
        XCTAssertNil(state.lastError)
    }

    /// The template flow (`performCreateNoteFromTemplate`).
    func testAnUnindexedTemplateCreateFinishesAsACreate() async throws {
        let announcer = PostedAnnouncements()
        let (state, dir) = try await openTemplatesVault(announcer: announcer)
        try await presentTheNameSheet(state)
        let leaf = "Landed-\(UUID().uuidString).md"
        announcer.posts.removeAll()

        await AfterWriteFault.during(leaf) {
            state.submitTemplateNoteName(leaf)
            await state.templateCreateTask?.value
        }
        await state.noteLoadTask?.value

        XCTAssertEqual(
            try String(contentsOf: dir.appendingPathComponent(leaf), encoding: .utf8),
            "template body\n")
        // The created sentence (High: it wins the tab-switch race), then
        // the caveat (Medium, after it, never instead of it).
        XCTAssertEqual(
            announcer.posts.map(\.message),
            ["Created \(leaf) from Note.", AfterWriteFault.caveat(for: leaf)])
        XCTAssertEqual(announcer.posts.map(\.priority), [.high, .medium])
        XCTAssertEqual(state.selectedFilePath, leaf)
        XCTAssertEqual(state.loadedFilePath, leaf)
        XCTAssertEqual(try entries(of: dir), [leaf, "Templates"].sorted())
        // The name sheet is not re-presented.
        XCTAssertEqual(state.pendingTemplateFlow, .idle)
        XCTAssertNil(state.templateNoteNameError)
        XCTAssertNil(state.templateRetryNoteName)
    }

    /// The regression the helper must not break: a REFUSAL rethrows, so
    /// `DestinationExists` still drives the auto-suffix (Duplicate advances
    /// past a raced candidate) and still presents on the name sheet (the
    /// template flow re-presents it with the refusal's words). Neither
    /// speaks a created sentence or the caveat for the refused name.
    func testARefusedCreateStillPresentsDestinationExists() async throws {
        // The auto-suffix: a copy raced onto disk after the scan.
        let stem = "Raced-\(UUID().uuidString)"
        let (state, dir) = try await openVault { dir in
            try "original\n".write(
                to: dir.appendingPathComponent("\(stem).md"), atomically: true,
                encoding: .utf8)
        }
        try "precious\n".write(
            to: dir.appendingPathComponent("\(stem) copy.md"), atomically: true,
            encoding: .utf8)
        let (subscription, sentences) = recordMutationSentences(state)
        defer { subscription.cancel() }

        await state.duplicateEntry(path: "\(stem).md")?.value

        XCTAssertEqual(
            try String(
                contentsOf: dir.appendingPathComponent("\(stem) copy.md"), encoding: .utf8),
            "precious\n", "the raced candidate's bytes survive")
        XCTAssertEqual(
            try String(
                contentsOf: dir.appendingPathComponent("\(stem) copy 2.md"), encoding: .utf8),
            "original\n", "the refusal advanced the suffix")
        XCTAssertEqual(sentences(), ["Duplicated \(stem).md as \(stem) copy 2.md."])
        XCTAssertNil(state.lastError)

        // The name sheet: a note raced onto disk under the chosen name.
        let announcer = PostedAnnouncements()
        let (templated, vault) = try await openTemplatesVault(announcer: announcer)
        try await presentTheNameSheet(templated)
        let leaf = "Taken-\(UUID().uuidString).md"
        try "precious\n".write(
            to: vault.appendingPathComponent(leaf), atomically: true, encoding: .utf8)
        announcer.posts.removeAll()

        templated.submitTemplateNoteName(leaf)
        await templated.templateCreateTask?.value

        XCTAssertEqual(
            try String(contentsOf: vault.appendingPathComponent(leaf), encoding: .utf8),
            "precious\n", "nothing was written over the raced note")
        guard case .needsName = templated.pendingTemplateFlow else {
            return XCTFail(
                "the refusal must re-present the name sheet, got \(templated.pendingTemplateFlow)")
        }
        XCTAssertTrue(
            templated.templateNoteNameError?.contains("already exists") == true,
            "the DestinationExists words: \(templated.templateNoteNameError ?? "nil")")
        XCTAssertEqual(templated.templateRetryNoteName, leaf)
        XCTAssertTrue(
            announcer.posts.isEmpty, "a refusal speaks nothing: \(announcer.posts)")
    }
}
