// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// §W-D parity census (W0.5-3, #719): the mac half of the canonical
/// a11y-event corpus check. Constructs every representative event from
/// `slate_core::a11y::corpus()` in the SAME order, renders each through
/// the FFI (`a11yRender` — the exact path `postAccessibilityAnnouncement(
/// _ event:)` posts through), and asserts (priority, text) against the
/// committed artifact `tests/fixtures/a11y/corpus.json`. Each entry's
/// `event` field (core's Debug identity, via `a11yEventIdentity`) is
/// asserted too, so the mirror must construct the SAME semantic event —
/// not merely one that happens to render identical text. The Windows
/// census will consume the same file, which is what makes the corpus the
/// cross-platform anchor: if this test and its Windows twin are green,
/// both hosts speak identical announcements for identical events.
///
/// Corpus changes are DELIBERATE: regenerate the artifact core-side
/// (`SLATE_REGENERATE_FIXTURES=1 cargo test -p slate-core a11y` — that
/// run fails by design after rewriting; re-run clean to prove the pin),
/// update the mirrored construction below, and review the diff as a
/// §W-D delta.
final class A11yCorpusCensusTests: XCTestCase {

    private struct CorpusEntry: Decodable {
        let event: String
        let priority: String
        let text: String
    }

    /// Repo-root fixture path derived from this file's location — the first
    /// delete strips the filename, then four directory hops
    /// (SlateMacTests → Tests → slate-mac → apps) reach the repo root.
    private static var corpusURL: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // strips the filename → SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .deletingLastPathComponent()  // apps
            .deletingLastPathComponent()  // repo root
            .appendingPathComponent("tests/fixtures/a11y/corpus.json")
    }

    /// The Swift mirror of `slate_core::a11y::corpus()` — same events,
    /// same sample values, same order.
    private static var corpus: [A11yEvent] {
        [
            .filesRegionFocused,
            .leafPanelShown(title: "Outline"),
            .editorPaneFocused(ordinal: 2, total: 3, title: "notes.md", prefix: ""),
            .tabFocused(prefix: "Now", filename: "notes.md", index: 1, count: 4),
            .tabClosed(closedTitle: "draft.md", successor: "notes.md"),
            .tabClosed(closedTitle: "draft.md", successor: nil),
            .noSplitPanesToResize,
            .paneResized(percent: 60),
            .graphOpensSinglePane,
            .rightPaneShown,
            .rightPaneHidden,
            .historyPanelShown,
            .reopenTargetMissing(filename: "gone.md"),
            .reopenedFile(filename: "notes.md"),
            .reopenedNamed(name: "Open tasks"),
            .reopenedGraph,
            .vaultOpened(vaultTitle: "Garden", sidebarNotice: ""),
            .removedRecentVault(displayName: "Garden"),
            .welcomeShown(recentVaultCount: 0),
            .welcomeShown(recentVaultCount: 1),
            .welcomeShown(recentVaultCount: 2),
            .commandPaletteNeedsVault,
            .searchNeedsVault,
            .searchResultOpened(filename: "notes.md", line: 12, snippet: "the quick brown fox"),
            .externalLinkUnsupported(target: "ftp://example.com"),
            .externalLinkOpened,
            .externalLinkFailed(target: "https://example.com"),
            .linkUnresolved(target: "Missing Note"),
            .helpOpened,
            .helpFailed,
            .internalNavigated(kind: "Opened", filename: "notes.md"),
            .citationNotLoaded,
            .noResolvedEmbedAtCursor,
            .noEmbedAtCursor,
            .headingNotFound,
            .headingScrollFailed(heading: "Roadmap"),
            .scrolledToHeading(heading: "Roadmap"),
            .scrolledToLine(filename: "notes.md", line: 40),
            .openedAtLine(filename: "notes.md", line: 40),
            .openedFile(filename: "notes.md"),
            .showingNote(displayName: "notes"),
            .taskToggleUnsaved(filename: "notes.md"),
            .taskToggleConflict(filename: "notes.md"),
            .tasksReviewShown(filterName: "Open tasks"),
            .tasksFilterSet(filterName: "All tasks"),
            .noteSaved(filename: "notes.md"),
            .saveConflict(filename: "notes.md"),
            .restoredVersionFrom(formattedDate: "July 19, 2026 at 9:41 AM"),
            .restoredFile(filename: "notes.md"),
            .restoredFileAs(sourceName: "notes.md", filename: "notes-restored.md"),
            .printNeedsNote,
            .printDialogOpened(name: "notes.md"),
            .batchCheckStarted(formattedCount: "1,024", actionName: "Move"),
            .selectionCopied,
            .sidebarSettingsStillDefaults(detail: "the file is malformed."),
            .sidebarSettingsReloadedStaleRefs,
            .sidebarSettingsReloaded,
            .vaultClosed,
            .vaultClosedAllSaved,
            .vaultClosedChangesDiscarded,
            .propertiesUpdated,
            .propertyChanged(key: "tags", deleted: false),
            .propertyChanged(key: "tags", deleted: true),
            .propertyEditConflict(filename: "notes.md"),
            .propertiesSourceRejected(reason: "the YAML does not parse"),
            .propertyEditFailed(detail: "io error"),
            .propertiesReloaded,
            .propertiesReloadedBodyChanged,
            .noteChangedAgain(detail: nil),
            .noteChangedAgain(detail: "The note changed while saving."),
            .propertiesReloadFailed(reason: "io error"),
            .propertyRetainedCopied,
            .propertyRecoveryUnverified(displayName: "notes"),
            .propertyRetainedDiscarded,
            .propertyRetainedReapplyFailed(detail: nil),
            .propertyReloadStillFailed(reason: "io error"),
            .propertyLoadCurrentFailed(reason: "io error"),
            .addPropertySheetShown,
            .sourceChangesDiscarded,
            .bulkRenameSheetShown,
            .renameReloadFailed(detail: nil),
            .renameFailed(detail: "io error"),
            .renameSummary(applied: true, renamed: 3, skipped: 1, failed: 0),
            .renameSummary(applied: false, renamed: 1, skipped: 0, failed: 0),
            .renameSummary(applied: false, renamed: 3, skipped: 2, failed: 0),
            .duplicateFilesOnly,
            .mathSpeechStyle(name: "ClearSpeak"),
            .mathVerbosity(name: "Verbose"),
            .mathBrailleCode(name: "Nemeth"),
            .codePreambleVerbosity(name: "Concise"),
            .editorTextSize(percent: 110),
            .spellCheckToggled(enabled: true),
            .spellCheckToggled(enabled: false),
            .citationStyleChanged(title: "APA"),
            .citationsCount(count: 1),
            .citationsCount(count: 3),
            .outlineCount(count: 1),
            .outlineCount(count: 5),
            .fileListCount(count: 1),
            .fileListCount(count: 12),
            .itemsSelected(count: 4),
            .itemsSelected(count: 1),
            .noItemsSelected,
            .treeFolderSelected(name: "Archive"),
            .rowSelected(name: "notes"),
            .switcherRecentCount(count: 2),
            .switcherRecentCount(count: 1),
            .switcherRecentCount(count: 0),
            .switcherNoMatches(query: "zzz"),
            .switcherMatchCount(count: 2, query: "foo"),
            .switcherMatchCount(count: 1, query: "foo"),
            .paletteCommandSelected(label: "Save", disabledReason: nil),
            .paletteCommandSelected(
                label: "Save",
                disabledReason: "A structural operation is in progress."
            ),
            .recentSearchFocused(query: "fox"),
            .quickSwitcherCount(count: 2, query: nil),
            .quickSwitcherCount(count: 1, query: nil),
            .quickSwitcherCount(count: 2, query: "foo"),
            .quickSwitcherCount(count: 1, query: "foo"),
            .quickSwitcherCount(count: 0, query: "zzz"),
            .baseViewMode(mode: "cards"),
            .baseViewSwitcher(viewCount: 1),
            .baseViewSwitcher(viewCount: 2),
            .basesNewQueryBuilder,
            .basesEditingFilters(viewName: "Table"),
            .basesFiltersOpenFailed(detail: "io error"),
            .basesPreviewFailed(detail: "bad expression"),
            .basesBuilderSaved,
            .basesViewSaveFailed(detail: "io error"),
            .basesSavedQueryNameNeeded,
            .basesSavedQueryCreated(name: "Open tasks"),
            .basesSavedQueryCreateFailed(detail: "io error"),
            .basesSavedQueryUpdated(name: "Open tasks"),
            .basesSavedQueryUpdateFailed(detail: "io error"),
            .basesViewSelected(name: "Cards"),
            .basesSortSaveFailed(detail: "io error"),
            .baseRefreshed,
            .dataviewConversionFailed(detail: "unsupported query"),
            .citationInsertUnavailable,
            .citationWalkThrough,
            .codeCopied,
            .hostComposed(text: "Composed by a host engine.", priority: .high),
        ]
    }

    func testEveryCorpusEventRendersTheCommittedTextAndPriority() throws {
        let data = try Data(contentsOf: Self.corpusURL)
        let entries = try JSONDecoder().decode([CorpusEntry].self, from: data)
        let corpus = Self.corpus

        XCTAssertEqual(
            corpus.count, entries.count,
            "Swift corpus mirror and the committed artifact must stay in lockstep"
        )

        for (index, (event, entry)) in zip(corpus, entries).enumerated() {
            XCTAssertEqual(
                a11yEventIdentity(event: event), entry.event,
                "event identity mismatch at corpus[\(index)] — the mirror "
                    + "constructed a different variant or parameters than the artifact pins"
            )
            let rendered = a11yRender(event: event)
            XCTAssertEqual(
                rendered.text, entry.text,
                "text mismatch at corpus[\(index)] (\(entry.event))"
            )
            let priority = rendered.priority == .high ? "high" : "medium"
            XCTAssertEqual(
                priority, entry.priority,
                "priority mismatch at corpus[\(index)] (\(entry.event))"
            )
        }
    }

    /// The poster path itself: posting an event routes core's rendered
    /// (text, priority) into the platform primitive verbatim.
    func testEventPosterUsesTheCanonicalRendering() {
        final class RecordingPoster: AnnouncementPosting {
            var posted: [(String, AnnouncementPriority)] = []
            func post(_ message: String, priority: AnnouncementPriority) {
                posted.append((message, priority))
            }
        }
        let poster = RecordingPoster()
        poster.post(A11yEvent.noteSaved(filename: "notes.md"))
        poster.post(A11yEvent.commandPaletteNeedsVault)

        XCTAssertEqual(poster.posted.count, 2)
        XCTAssertEqual(poster.posted[0].0, "Saved notes.md.")
        XCTAssertEqual(poster.posted[0].1, .medium)
        XCTAssertEqual(poster.posted[1].0, "Open a vault to use the command palette.")
        XCTAssertEqual(poster.posted[1].1, .high)
    }
}
