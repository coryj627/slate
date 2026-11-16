// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

/// Plan 43 PR 29 §3, §5: what travels with a buffer's content hash, taken
/// from the same core result that gave the hash (a `NotePartsBundle`, a
/// `SaveReport`, a create's landing, a kept entry, or another buffer's
/// baseline), never from a separate identity read. Every statement that
/// assigns a content hash assigns this beside it
/// (`HotExitIdentityCensusTests`).
struct BaselineIdentity: Equatable {
    /// The identity of the file version the hash describes, as core reports
    /// it (`file_identity`); nil where core has none.
    var fileIdentity: String?
    /// The baseline came from a kept hot-exit entry: saves check the identity
    /// as well as the hash (`save_text_checked`) until one lands, so a file
    /// put at the path since is refused (§5).
    var identityEnforced = false
    /// The baseline's text is not known (a restored buffer whose disk text
    /// is not its baseline): the buffer stays dirty until a save lands or a
    /// reload replaces it, so emptying it never reads as clean (§5).
    var textUnknown = false

    /// A baseline read from or written to disk by this process.
    static func fromDisk(_ fileIdentity: String?) -> BaselineIdentity {
        BaselineIdentity(fileIdentity: fileIdentity)
    }

    /// No baseline (a cleared buffer, a note never saved).
    static let none = BaselineIdentity()
}
