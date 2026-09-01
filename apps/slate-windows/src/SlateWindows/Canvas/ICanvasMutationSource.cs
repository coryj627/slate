// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 §E TE-5: the funnel's two FFI writes-and-reads beyond the
/// load source's refresh trio (`ICanvasLoadSource` supplies Outline,
/// TableRows and Scene — the funnel takes BOTH seams rather than
/// duplicating the trio). Implemented by the document's session
/// source; faked in the funnel battery.
/// </summary>
internal interface ICanvasMutationSource
{
    /// <summary>`canvas_apply` — throws the typed VaultException
    /// family: WriteConflict for the CAS refusal, SavedButUnindexed
    /// for a landed write whose index step failed (TE-0's receipt),
    /// InvalidArgument for a rejected action.</summary>
    CanvasApplyResult Apply(ulong handle, CanvasAction action);

    /// <summary>`canvas_current_text` — the handle's pre-conflict
    /// snapshot (TE-3), taken AT REFUSAL while the gate is held.</summary>
    CanvasEditorSeed CurrentText(ulong handle);
}
