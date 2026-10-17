// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// #1077 Phase 4, the mac half (#1147): canonical file identity's host
/// rules, docs/plans/32_canonical_file_identity_contracts.md I6 to I8. The
/// twins of Windows' `WorkspaceViewModel.ReseatMissingTabs` (with
/// `KeepsBufferOnReseat`) and `WriteForSave`.
///
/// Every identity comparison on mac stays exact (`BaseExactIdentity`). That
/// is the I6 rule: a host where some comparisons fold case and others do not
/// is worse than one that is consistently exact. On a volume where `Ghost.md`
/// and `ghost.md` name one file, exact comparison can do harm in two places,
/// and each is handled once. After every publication that creates or renames
/// a path, a tab whose note is missing asks core for the spelling the
/// filesystem stores and re-seats to it (I6), and an answer core cannot give
/// leaves the tab as it is (I7). A save whose buffer never loaded bytes from
/// its path is a create, never an unconditional write (I8), so a stale tab
/// cannot bury the note that came back, even before a re-seat has run.
extension AppState {
    /// One Markdown tab the re-seat considers. `holdsBuffer`: the tab keeps
    /// unsaved text (the Trash flip retained it for recovery), so the
    /// re-seat retargets it but never reloads it.
    struct MissingTabReseatCandidate: Equatable {
        let tabID: TabID
        let path: String
        let isActive: Bool
        let holdsBuffer: Bool
    }

    /// I6: re-seat every Markdown tab whose note is missing and has come
    /// back, under its own spelling or another the volume treats as the
    /// same file. `publishTreeMutation` runs it after every publication that
    /// creates or renames a path; it is the sweep a rescan reuses (PR 19,
    /// #1333), as Windows' rescan reuses its own.
    ///
    /// Per missing path, once: ask core for the stored spelling
    /// (`canonicalPathProbe`). A thrown error leaves the tabs missing (I7,
    /// fail closed), and so does nil (the note is still gone). A spelling
    /// that differs retargets every tab on the path, as a rename does (path,
    /// title, parked identity, path-owned drafts), and a tab there that holds
    /// a buffer stops being missing and keeps the buffer, never reloaded over
    /// it. Under its own spelling such a tab keeps its recovery state, as
    /// Windows' `KeepsBufferOnReseat` leaves it missing: there is no identity
    /// to correct, and its save stays refused. The active tab with no buffer
    /// reloads through the same-path reload under either spelling, so its
    /// missing state and content hash stop being stale. A parked tab with no
    /// buffer reads its path when it is next activated.
    func reseatMissingTabs() {
        guard let session = currentSession else { return }
        var paths: [String] = []
        var candidatesByPath: [String: [MissingTabReseatCandidate]] = [:]
        for candidate in missingTabReseatCandidates() {
            let key = Self.reseatPathKey(candidate.path)
            if candidatesByPath[key] == nil { paths.append(candidate.path) }
            candidatesByPath[key, default: []].append(candidate)
        }
        for path in paths {
            let candidates = candidatesByPath[Self.reseatPathKey(path)] ?? []
            let stored: String?
            do {
                stored = try canonicalPathProbe(session, path)
            } catch {
                // I7: an identity core cannot read is left as it is.
                continue
            }
            guard let stored else { continue }
            if !BaseExactIdentity.matches(stored, path) {
                retargetTabsForReseat(from: path, to: stored)
                if candidates.contains(where: \.holdsBuffer) {
                    releaseMissingNoteForReseat(at: stored)
                }
            }
            if candidates.contains(where: { $0.isActive && !$0.holdsBuffer }) {
                reloadActiveNoteForReseat(path: stored)
            }
        }
    }

    /// The Markdown tabs a re-seat considers, in workspace order. The active
    /// tab is missing when its path failed to load (the Trash flip, or a
    /// read that failed) with no read or save of it in flight: a save owns
    /// its note until it lands, and the next publication re-seats it then.
    /// A parked tab is missing when its document is marked so, or when it
    /// has no document at all: the Trash flip drops a clean parked tab's
    /// document, and a tab never activated has none, and either reads its
    /// path when activated, so its spelling is all a re-seat corrects.
    func missingTabReseatCandidates() -> [MissingTabReseatCandidate] {
        let activeID = workspace.model.activeGroup.activeTabID
        var candidates: [MissingTabReseatCandidate] = []
        for tab in workspace.model.allTabs {
            guard case .markdown(let path) = tab.item, !path.isEmpty else { continue }
            if tab.id == activeID {
                guard noteLoadError != nil, !isLoadingNote, !isSaving else { continue }
                candidates.append(
                    MissingTabReseatCandidate(
                        tabID: tab.id,
                        path: path,
                        isActive: true,
                        holdsBuffer: BaseExactIdentity.matches(loadedFilePath, path)
                            && currentNoteText != nil))
            } else if let document = workspace.document(for: tab.id) {
                guard document.isMissingFromDisk else { continue }
                candidates.append(
                    MissingTabReseatCandidate(
                        tabID: tab.id, path: path, isActive: false, holdsBuffer: true))
            } else {
                candidates.append(
                    MissingTabReseatCandidate(
                        tabID: tab.id, path: path, isActive: false, holdsBuffer: false))
            }
        }
        return candidates
    }

    private static func reseatPathKey(_ path: String) -> String {
        BaseExactIdentity.registryKey(prefix: "reseat-path", value: path)
    }

    /// I8: the one core write of a note save. A buffer with a content hash
    /// saves through `save_composed` as before, a compare-and-swap on that
    /// hash. A buffer with none has never loaded bytes from the path it
    /// names, so its save is an exclusive create of the note core composes
    /// from it (`composeNote`, the composer `save_composed` runs). It lands
    /// only on a free path, on either landed arm (PR 9's
    /// `createReportingLanding`), and never as `save_composed` with a nil
    /// hash, which core treats as unconditional. A taken path refuses with
    /// `DestinationExists`, which is the save conflict, carrying the
    /// occupant's own hash so Keep Mine is a compare-and-swap over exactly
    /// the bytes the conflict is about. An occupant that cannot be read
    /// leaves that hash empty, and Keep Mine then recreates or conflicts
    /// again on core's answer.
    nonisolated static func writeNoteSave(
        session: VaultSession,
        path: String,
        fmSource: String,
        body: String,
        expectedContentHash: String?,
        displayName: String
    ) throws -> NoteSaveLanding {
        guard let expectedContentHash else {
            do {
                let landing = try createReportingLanding(
                    session: session,
                    path: path,
                    content: composeNote(fmSource: fmSource, body: body),
                    displayName: displayName)
                return NoteSaveLanding(
                    newContentHash: landing.contentHash, caveat: landing.caveat)
            } catch VaultError.DestinationExists {
                let occupant = try? session.readNoteParts(path: path)
                throw VaultError.WriteConflict(
                    currentContentHash: occupant?.contentHash ?? "",
                    expectedContentHash: "",
                    currentMtimeMs: occupant?.mtimeMs ?? 0)
            }
        }
        let report = try session.saveComposed(
            path: path,
            fmSource: fmSource,
            body: body,
            expectedContentHash: expectedContentHash)
        return NoteSaveLanding(newContentHash: report.newContentHash, caveat: nil)
    }
}

/// What a note save landed: the content hash of the bytes on disk and, for a
/// hashless save whose create landed but was not indexed, core's caveat
/// (`CreatePublishedUnindexed`), spoken after the saved sentence.
struct NoteSaveLanding {
    let newContentHash: String
    let caveat: A11yEvent?
}

extension AppState.TreeMutation.Kind {
    /// I6's trigger: whether this publication creates or renames a path, so
    /// that a missing tab's note may be back. These are the publications
    /// Windows' lifecycle re-seats on, its Created and Renamed events: a
    /// note created, a path renamed or moved, one at a time or in a batch,
    /// and an import, which creates. An empty new folder, a removal and a
    /// reconcile with no truthful transform bring nothing back. Exhaustive,
    /// so a new kind must say which it is.
    var publishesCreatedOrRenamedPaths: Bool {
        switch self {
        case .createNote, .rename, .move, .batchMove, .importBatch:
            return true
        case .createFolder, .delete, .batchTrash, .batchReconcile:
            return false
        }
    }
}
