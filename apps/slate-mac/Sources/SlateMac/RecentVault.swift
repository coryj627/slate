// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Darwin
import Foundation

/// One entry in the welcome screen's "Recent Vaults" list.
///
/// Persisted to disk in JSON. The `path` is the absolute filesystem
/// path the user picked; `displayName` is the folder's last path
/// component (cached at add-time so we don't re-derive it during list
/// rendering). `lastOpenedMs` is Unix epoch milliseconds — used for
/// "last opened <relative date>" labeling and for LRU sort on add.
/// `rootIdentity` (#1442) is the folder the open admitted, so a launch can
/// tell that folder from another one that later appears at the same path.
public struct RecentVault: Codable, Equatable, Identifiable {
    public let path: String
    public let displayName: String
    public let lastOpenedMs: Int64
    /// The identity of the folder this entry opened, recorded at the open
    /// (#1442). Nil in entries written before #1442: they still decode, and
    /// launch-restore refuses only a folder holding nothing but `.slate` for
    /// them. An entry without one encodes without the key, exactly as before.
    public let rootIdentity: RootIdentity?

    public var id: String { path }

    public init(
        path: String, displayName: String, lastOpenedMs: Int64,
        rootIdentity: RootIdentity? = nil
    ) {
        self.path = path
        self.displayName = displayName
        self.lastOpenedMs = lastOpenedMs
        self.rootIdentity = rootIdentity
    }

    /// Build from a vault URL and the current wall clock.
    public init(url: URL, now: Date = Date(), rootIdentity: RootIdentity? = nil) {
        self.init(
            path: url.path,
            displayName: url.lastPathComponent,
            lastOpenedMs: Int64(now.timeIntervalSince1970 * 1000),
            rootIdentity: rootIdentity
        )
    }
}

extension RecentVault {
    /// The identity of a vault root as a recent entry stores it (#1442).
    ///
    /// `device` and `inode` are the pair the open admits and the core session
    /// anchors (`SidebarVaultPrefsStore.RootIdentity`). `birth` and `volume`
    /// are what the sidebar write journal (PR 23, #944) already records beside
    /// that pair to recognise the same root across launches, read by the same
    /// helper (`SidebarWriteJournal.fingerprint`).
    public struct RootIdentity: Codable, Equatable, Sendable {
        public let device: UInt64
        public let inode: UInt64
        /// The root's birth time in nanoseconds; nil where unknown.
        public let birth: Int64?
        /// The root's volume UUID; nil where the volume has none.
        public let volume: String?

        public init(device: UInt64, inode: UInt64, birth: Int64?, volume: String?) {
            self.device = device
            self.inode = inode
            self.birth = birth
            self.volume = volume
        }

        init(
            _ identity: VaultStoreDirectory.RootIdentity,
            fingerprint: (birth: Int64?, volume: String?)
        ) {
            self.init(
                device: identity.device, inode: identity.inode,
                birth: fingerprint.birth, volume: fingerprint.volume)
        }

        /// What an open records: the identity it admitted, with the birth time
        /// and volume read while the path still resolves to that identity.
        /// Nil when the open admitted none.
        static func recorded(
            at root: URL, admitted identity: VaultStoreDirectory.RootIdentity?
        ) -> RootIdentity? {
            guard let identity else { return nil }
            return RootIdentity(
                identity,
                fingerprint: SidebarWriteJournal.fingerprint(of: root, expecting: identity))
        }

        /// What is at `root` now; nil when nothing can be observed there.
        static func observed(at root: URL) -> RootIdentity? {
            guard let identity = AppState.vaultRootIdentity(of: root) else { return nil }
            return RootIdentity(
                identity,
                fingerprint: SidebarWriteJournal.fingerprint(of: root, expecting: identity))
        }

        /// Whether `current`, the folder now at the entry's path, is the
        /// folder this identity was recorded for. The journal's rule, so the
        /// app has one answer to "the same vault across launches": the same
        /// device and inode is the same folder unless both birth times are
        /// known and differ (a replacement that reuses the inode); under
        /// another device number (an external or network drive mounted again)
        /// it is the same folder only when the volume UUID, the inode and the
        /// birth time are all known and equal.
        func identifiesSameFolder(as current: RootIdentity) -> Bool {
            guard inode == current.inode else { return false }
            if device == current.device {
                if let birth, let currentBirth = current.birth {
                    return birth == currentBirth
                }
                return true
            }
            guard let volume, let currentVolume = current.volume, volume == currentVolume,
                let birth, let currentBirth = current.birth, birth == currentBirth
            else { return false }
            return true
        }
    }

    /// What launch-restore found at this entry's path when the folder there
    /// is not the vault the entry remembers (#1442); nil when it is, or when
    /// it cannot tell (then the restore goes ahead as before). The caller has
    /// already checked that a folder is there.
    ///
    /// An entry with a recorded identity is compared with what is there now.
    /// An entry written before #1442 has none, so only the ghost an older
    /// build's stores left behind is refused for it.
    static func launchRestoreMismatch(_ entry: RecentVault) -> RecentVaultMismatch? {
        let root = URL(fileURLWithPath: entry.path, isDirectory: true)
        guard let remembered = entry.rootIdentity else {
            return holdsOnlySlateFolder(root) ? .onlySlateFolder : nil
        }
        guard let current = RootIdentity.observed(at: root),
            !remembered.identifiesSameFolder(as: current)
        else { return nil }
        return holdsOnlySlateFolder(root) ? .onlySlateFolder : .differentFolder
    }

    /// Whether `folder` holds nothing but a real `.slate` folder. Finder's
    /// `.DS_Store` is ignored: it appears as soon as someone looks at the
    /// folder in Finder. An unreadable folder is not counted: uncertainty
    /// keeps today's behaviour. Nothing here changes the folder.
    static func holdsOnlySlateFolder(_ folder: URL) -> Bool {
        guard let names = try? FileManager.default.contentsOfDirectory(atPath: folder.path),
            names.filter({ $0 != ".DS_Store" }) == [VaultStoreDirectory.directoryName]
        else { return false }
        var info = stat()
        let slate = folder.appendingPathComponent(VaultStoreDirectory.directoryName)
        guard lstat(slate.path, &info) == 0 else { return false }
        return info.st_mode & S_IFMT == S_IFDIR
    }
}

/// Why launch-restore did not reopen the most recent vault although a folder
/// is at its path (#1442). The "Vault Not Found" alert says which.
enum RecentVaultMismatch: Equatable, Sendable {
    /// The folder holds nothing but Slate's own `.slate` folder: what a save
    /// that recreates a renamed or moved vault's old path leaves there (the
    /// host stores did before #1442; core's own writers still can, #1474).
    case onlySlateFolder
    /// The folder is not the one the entry recorded.
    case differentFolder
}
