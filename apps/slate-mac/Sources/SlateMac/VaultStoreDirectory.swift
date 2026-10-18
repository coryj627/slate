// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Darwin
import Foundation

/// The one rule every host store that writes inside `<vault>/.slate/` follows
/// before it writes (#1442): `WorkspaceStore`, `SearchRecentsStore`,
/// `PrefsJsonStore` and `GraphConfigStore`. `SidebarVaultPrefsStore` is
/// already descriptor-anchored end to end and keeps its own path.
///
/// A store never creates the vault root or any folder above it. It creates at
/// most `.slate`, and only inside a root that still exists **and** is still
/// the folder the session opened: the root is opened once, its identity is
/// read with `fstat` on that descriptor and compared with the identity the
/// open admitted (`AppState.sidebarVaultRootIdentity`, which the open proved
/// equal to the core session's own anchor), and `.slate` is made with
/// `mkdirat` relative to that verified descriptor.
///
/// Before #1442 each store called `createDirectory(withIntermediateDirectories:
/// true)` on a path captured at open. After the open vault's folder was renamed
/// or moved, the next save recreated the OLD path as a folder holding only
/// `.slate/<store>.json`, and the next launch restored that folder as an empty
/// vault.
///
/// A refusal throws `Refusal`. Every caller already reports store errors (a
/// log line, or the Settings error for the bibliography) and never interrupts
/// the user, so a refused write is told, not silent.
///
/// Residual, accepted by the small fix (PR 22 design §5.4 and Q8): the write
/// that follows still names `<root>/.slate/<file>` by pathname. A root renamed
/// inside that window makes the write fail, because nothing creates a folder
/// any more; a different folder put at the path inside that window could
/// receive the file in its own existing `.slate`. PR 22's shared `fs_root`
/// convention, carried into the host stores once core has it, replaces this
/// helper with writes relative to a held descriptor.
enum VaultStoreDirectory {
    /// The vault root's (device, inode): the sidebar store's identity type,
    /// the one the open admits and compares with the core session's anchor.
    typealias RootIdentity = SidebarVaultPrefsStore.RootIdentity

    static let directoryName = ".slate"

    /// Why a store did not write. Each case names the folder it is about.
    enum Refusal: Error, Equatable, LocalizedError, CustomStringConvertible {
        /// Nothing that is a folder is at the vault's path any more: it was
        /// renamed, moved, deleted or unmounted.
        case vaultRootMissing(path: String)
        /// A different folder now sits at the vault's path.
        case vaultRootReplaced(path: String)
        /// The session has no admitted identity to compare with.
        case vaultRootUnverified(path: String)
        /// The root could not be opened or inspected.
        case vaultRootUnavailable(path: String, reason: String)
        /// `.slate` could not be made, or is not a folder.
        case slateDirectoryUnavailable(path: String, reason: String)

        var errorDescription: String? {
            switch self {
            case .vaultRootMissing(let path):
                return "The vault folder is no longer at \(path), so Slate didn’t "
                    + "recreate it there."
            case .vaultRootReplaced(let path):
                return "The folder at \(path) is no longer the vault Slate opened, "
                    + "so Slate didn’t write to it."
            case .vaultRootUnverified(let path):
                return "Slate couldn’t confirm that the folder at \(path) is the "
                    + "vault it opened, so it didn’t write to it."
            case .vaultRootUnavailable(let path, let reason):
                return "The vault folder at \(path) couldn’t be opened: \(reason)."
            case .slateDirectoryUnavailable(let path, let reason):
                return "The settings folder \(path) couldn’t be used: \(reason)."
            }
        }

        /// Logs interpolate the error (`"…: \(error)"`), so the log line reads
        /// the same sentence the Settings error shows.
        var description: String { errorDescription ?? "" }
    }

    /// The vault's `.slate` folder, ready for one write.
    ///
    /// Opens `vaultRoot` once (following a symlinked root, as the open did),
    /// refuses unless its identity equals `expectedRootIdentity`, then makes
    /// `.slate` inside that descriptor if it is missing. A `.slate` that is a
    /// symlink to a folder is followed, as the stores' pathname writes always
    /// did.
    @discardableResult
    static func prepare(
        vaultRoot: URL, expectedRootIdentity: RootIdentity?
    ) throws -> URL {
        let rootPath = vaultRoot.path
        let slateURL = vaultRoot.appendingPathComponent(directoryName, isDirectory: true)

        let rootFD = open(rootPath, O_RDONLY | O_DIRECTORY | O_CLOEXEC)
        guard rootFD >= 0 else {
            let code = errno
            // ENOTDIR: a file now sits at the path, or above it. Either way the
            // folder the session opened is not here, and is never recreated.
            if code == ENOENT || code == ENOTDIR {
                throw Refusal.vaultRootMissing(path: rootPath)
            }
            throw Refusal.vaultRootUnavailable(path: rootPath, reason: posixReason(code))
        }
        defer { close(rootFD) }

        var rootInfo = stat()
        guard fstat(rootFD, &rootInfo) == 0 else {
            throw Refusal.vaultRootUnavailable(path: rootPath, reason: posixReason(errno))
        }
        guard let expectedRootIdentity else {
            throw Refusal.vaultRootUnverified(path: rootPath)
        }
        let observed = RootIdentity(
            device: UInt64(rootInfo.st_dev), inode: UInt64(rootInfo.st_ino))
        guard observed == expectedRootIdentity else {
            throw Refusal.vaultRootReplaced(path: rootPath)
        }

        // `.slate` alone, inside the verified descriptor: never an
        // intermediate, never the root.
        if mkdirat(rootFD, directoryName, 0o755) != 0 {
            let code = errno
            guard code == EEXIST else {
                throw Refusal.slateDirectoryUnavailable(
                    path: slateURL.path, reason: posixReason(code))
            }
        }
        var slateInfo = stat()
        guard fstatat(rootFD, directoryName, &slateInfo, 0) == 0 else {
            throw Refusal.slateDirectoryUnavailable(
                path: slateURL.path, reason: posixReason(errno))
        }
        guard slateInfo.st_mode & S_IFMT == S_IFDIR else {
            throw Refusal.slateDirectoryUnavailable(
                path: slateURL.path, reason: "it is not a folder")
        }
        return slateURL
    }

    private static func posixReason(_ code: Int32) -> String {
        String(cString: strerror(code))
    }
}
