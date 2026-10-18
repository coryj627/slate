// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// #1431: what the editor's activate-at-cursor verb does with a target
/// (`EditorActivation`), through the same AppState actions the reading view
/// and the panels use, and core's words for every outcome. The Windows twin
/// is `EditorInteractionCoordinator.ActivateCanonicalSpan`.
extension AppState {

    /// The `slate.editor.activateAtCaret` command (⌘↩, the palette row, the
    /// Edit menu): activate at the active editor's caret. With no editor on
    /// screen (reading mode, a canvas, a base, a note still loading) there is
    /// no cursor, and the verb says so rather than going silent.
    func activateAtCursor() {
        guard isNoteEditorMounted else {
            announceNothingToActivateAtCursor()
            return
        }
        activateAtCursorRequest.send()
    }

    /// Act on one activation the editor resolved.
    func performEditorActivation(_ request: EditorActivationRequest) {
        switch request.target {
        case .link(_, let kind):
            followEditorLink(request, embed: kind == .image)
        case .embed:
            // ⌘E's preview, Windows' Ctrl+Enter on an embed.
            guard let target = request.embedTarget else {
                announceNothingToActivateAtCursor()
                return
            }
            requestEmbedPreview(target: target, sourceLine: request.line)
        case .tag:
            // Windows' editor tag path: the Files sidebar filtered to the
            // tag (the reading view's opens tag-scoped search, SD-4). Core's
            // span owns the tag's boundaries; dropping its one `#` marker is
            // boundary marshalling, not a second tag parser.
            guard request.spanText.hasPrefix("#"), request.spanText.count > 1 else {
                announceNothingToActivateAtCursor()
                return
            }
            activateSidebarTagScope(full: String(request.spanText.dropFirst()))
        case .citation:
            openEditorCitation(raw: request.spanText, caretByte: request.byteOffset)
        case .task(_, let origin):
            toggleEditorTask(request, origin: origin)
        case .nothing:
            announceNothingToActivateAtCursor()
        }
    }

    private func announceNothingToActivateAtCursor() {
        announcer.post(.nothingToActivateAtCursor)
    }

    /// The saved note's link record for the span: core's own record for
    /// exactly these authored bytes, as Windows' `LinkRecordFor` matches
    /// it. Only while the records belong to this note, and only where the
    /// saved text still carries this span's text at these bytes (an edit
    /// above the span moves it; the saved record then describes other
    /// bytes).
    func editorLinkRecord(for request: EditorActivationRequest, embed: Bool) -> OutgoingLink? {
        guard let bytes = request.spanBytes, !bytes.isEmpty,
            ReadingLinkRouter.recordsBelongToNote(
                recordsPath: currentOutgoingLinksPath, notePath: selectedFilePath),
            let baseline = savedBaselineText,
            Self.utf8Slice(baseline, bytes) == request.spanText
        else { return nil }
        let start = UInt32(clamping: bytes.lowerBound + bodyByteOffset)
        let end = UInt32(clamping: bytes.upperBound + bodyByteOffset)
        return currentOutgoingLinks.first {
            $0.spanStart == start && $0.spanEnd == end && $0.isEmbed == embed
        }
    }

    private static func utf8Slice(_ text: String, _ bytes: Range<Int>) -> String? {
        let utf8 = text.utf8
        guard bytes.lowerBound >= 0, bytes.upperBound <= utf8.count else { return nil }
        let lower = utf8.index(utf8.startIndex, offsetBy: bytes.lowerBound)
        let upper = utf8.index(lower, offsetBy: bytes.count)
        return String(utf8[lower..<upper])
    }

    /// Follow a wikilink, Markdown link or image. The saved record, when it
    /// is this span's, goes through `openLink` exactly as an Outgoing links
    /// row does: external links open in the browser (or say why not),
    /// unresolved ones say so, the rest navigate. The editor navigates the
    /// current tab, as on Windows (G22: editor-pane navigation is current-tab
    /// either way), whatever modifier the chord carried.
    private func followEditorLink(_ request: EditorActivationRequest, embed: Bool) {
        if let record = editorLinkRecord(for: request, embed: embed) {
            openEditorLink(record)
            return
        }
        followEditorLinkByTarget(request)
    }

    /// `openLink` in the current tab. When it navigates into a note, the
    /// opened note's editor takes keyboard focus with the caret at its
    /// start (#1472), and the editor stays on screen while that note loads,
    /// so focus never leaves it.
    private func openEditorLink(_ record: OutgoingLink) {
        guard !record.isExternal, !record.isUnresolved, let path = record.targetPath else {
            openLink(record, target: .currentTab)
            return
        }
        claimEditorFocus(for: path, caretByte: 0, keepsEditorThroughLoad: true)
        openLink(record, target: .currentTab)
        dropEditorFocusClaimUnlessOpened(path)
    }

    /// A navigation the funnel refused (a property edit in flight, say)
    /// opened nothing, so its claim goes.
    private func dropEditorFocusClaimUnlessOpened(_ path: String) {
        if let claim = editorFocusClaim, BaseExactIdentity.matches(claim.path, path),
            !BaseExactIdentity.matches(selectedFilePath, path)
        {
            editorTookFocusClaim(claim.token)
        }
    }

    /// A link the saved records do not place here (an unsaved edit moved or
    /// added it): the reading view's resolution, on the span's own source.
    /// Core's reading pipeline parses the span into its run, and the run's
    /// target matches a record by core's grammar-exact key
    /// (`reading_match_link`), exactly as `ReadingLinkRouter.live` does; a
    /// target with no record is unresolved, as the reading view says of an
    /// unsaved link.
    private func followEditorLinkByTarget(_ request: EditorActivationRequest) {
        let owned = ReadingLinkRouter.recordsBelongToNote(
            recordsPath: currentOutgoingLinksPath, notePath: selectedFilePath)
        let records = owned ? currentOutgoingLinks : []
        let url = readingInlineSegmentsSource(source: request.spanText, citations: [], records: records)
            .lazy.flatMap(\.segments).flatMap(\.runs)
            .compactMap { ReadingInlineMapper.activationURL(for: $0.kind) }
            .first
        switch url.map(ReadingLinkRouter.disposition(for:)) {
        case .wiki(let target, let grammar):
            if let index = readingMatchLink(
                target: target, grammar: grammar, embed: false, records: records),
                Int(index) < records.count
            {
                openEditorLink(records[Int(index)])
            } else {
                announcer.post(.linkUnresolved(target: target))
            }
        case .embed(let key):
            if let index = readingMatchLink(
                target: key, grammar: .wikilink, embed: true, records: records),
                Int(index) < records.count,
                let path = records[Int(index)].targetPath
            {
                claimEditorFocus(for: path, caretByte: 0, keepsEditorThroughLoad: true)
                openEmbedTarget(path)
                dropEditorFocusClaimUnlessOpened(path)
            } else {
                announcer.post(.linkUnresolved(target: key))
            }
        case .external:
            // Core's pipeline marks only http, https and mailto activatable;
            // `openLink`'s external branch owns the allowlist, the opener and
            // the announcement, so a record for the destination goes there.
            if let url {
                openLink(Self.externalLinkRecord(url), target: .currentTab)
            }
        case .tag, .citation, .discard, nil:
            announcer.post(.linkUnresolved(target: request.spanText))
        }
    }

    /// An external destination as the record `openLink` takes.
    private static func externalLinkRecord(_ url: URL) -> OutgoingLink {
        OutgoingLink(
            targetPath: nil, targetRaw: url.absoluteString, targetAnchor: nil, kind: "markdown",
            isEmbed: false, isExternal: true, isUnresolved: false, snippet: "", ordinal: 0,
            spanStart: 0, spanEnd: 0, displayText: nil)
    }

    /// A citation's popover, as the reading view opens it: the note's
    /// rendered citation for this raw source, detached (an editor span has
    /// no SwiftUI anchor), or core's refusal while citations are loading.
    /// Its close returns keyboard focus to the editor, with the caret where
    /// the activation found it (`closeExpandedCitation`, #1472).
    private func openEditorCitation(raw: String, caretByte: Int) {
        guard let citation = currentNoteCitations.first(where: { $0.raw == raw }) else {
            announcer.post(.citationNotLoaded)
            return
        }
        expandedCitationRowAnchored = false
        expandedCitation = citation
        if let path = loadedFilePath {
            expandedCitationEditorReturn = (citation, path, caretByte)
        }
    }

    /// The task under the activation: on the caret's line from the keyboard,
    /// under the pointer's checkbox from a click — Windows' `CachedTaskAt`.
    /// `toggleCurrentTask` owns the dirty-buffer refusal (core's
    /// `TaskToggleUnsaved`), and says what the write did, as Windows' editor
    /// toggle does: "Task completed.", "Task reopened." or why it failed.
    private func toggleEditorTask(_ request: EditorActivationRequest, origin: EditorActivation.Origin) {
        let fileLine = request.line + bodyLineOffset
        let fileByte = UInt32(clamping: request.byteOffset + bodyByteOffset)
        let task = currentNoteTasks.first { task in
            switch origin {
            case .pointer:
                return task.checkboxStartByte <= fileByte && fileByte < task.checkboxEndByte
            case .keyboard, .press:
                return Int(task.line) == fileLine
            }
        }
        guard let task else {
            announceNothingToActivateAtCursor()
            return
        }
        _ = toggleCurrentTask(task, announcesOutcome: true)
    }
}
