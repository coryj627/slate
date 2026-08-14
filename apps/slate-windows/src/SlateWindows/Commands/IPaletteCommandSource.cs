// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using uniffi.slate_uniffi;

namespace SlateWindows.Commands;

/// <summary>
/// Everything the command palette needs from the command layer, and
/// nothing else. The palette view model ranks and renders; this seam
/// owns the registry, the availability rules, and recents persistence.
/// </summary>
/// <remarks>
/// <para>
/// Split out so the palette can be built and tested against a fake while
/// the registration bridge lands separately, and so the palette cannot
/// reach past it into workspace state. Every member is
/// <b>dispatcher-affine</b> per contract P15: <c>InvokeById</c> is a
/// synchronous FFI call that runs the foreign action on the calling
/// thread, so command invocation happens on the UI thread and the
/// implementation asserts it.
/// </para>
/// </remarks>
internal interface IPaletteCommandSource
{
    /// <summary>
    /// The command snapshot the palette ranks, taken once when the
    /// palette opens (contract P4). Sorted by <c>(section, id)</c> —
    /// clones every command, so this is never called per keystroke.
    /// </summary>
    Command[] ListCommands();

    /// <summary>
    /// The sidebar action catalog's id order, passed to
    /// <c>palette_sections</c> so the Sidebar section renders in catalog
    /// order rather than alphabetically (contract P16). Data, not policy.
    /// </summary>
    string[] SidebarPinnedOrder { get; }

    /// <summary>
    /// The reason <paramref name="commandId"/> cannot run right now, or
    /// <see langword="null"/> when it can. ONE resolver serves the row
    /// state, the selection announcement, and the Enter gate so those
    /// three cannot disagree (contract P8); it is re-evaluated at invoke
    /// time rather than trusted from render time.
    /// </summary>
    string? DisabledReason(string commandId);

    /// <summary>
    /// Invoke through the core registry (never by calling an
    /// <see cref="System.Windows.Input.ICommand"/> directly — contract
    /// PINV-4). Throws <c>CommandException</c>; the palette maps
    /// <c>UnknownId</c> and <c>ActionFailed</c> onto their canonical
    /// announcements and stays open on either (contract P9).
    /// </summary>
    void Invoke(string commandId);

    /// <summary>
    /// The recent command ids, most-recent first, as the palette's
    /// snapshot at open (contract P4).
    /// </summary>
    string[] LoadRecents();

    /// <summary>
    /// Record a successful invocation. Called only on success, and only
    /// after the action returned (contract P9). Routes the LRU through
    /// core's <c>PaletteRecentsAdd</c> rather than a hand-rolled list
    /// operation (PD-3), and persists atomically (contract P11).
    /// Persistence failure is non-fatal: the in-memory list still moves,
    /// so the open palette stays consistent with what the user just did.
    /// </summary>
    void RecordInvocation(string commandId);

    /// <summary>
    /// Whether a vault is open. The palette refuses to open without one
    /// and announces <c>CommandPaletteNeedsVault</c> instead; the
    /// open-flag must never be set in that case or the next vault open
    /// auto-presents an empty palette (contract P14).
    /// </summary>
    bool IsVaultOpen { get; }

    /// <summary>
    /// Whether <paramref name="message"/> — carried by a thrown
    /// <c>CommandException.ActionFailed</c> — is an availability
    /// rejection rather than an operation failure. Rejections announce
    /// as <c>PaletteCommandUnavailable</c> (the reason verbatim, no
    /// prefix); failures announce as <c>PaletteCommandFailed</c>
    /// (contract P10).
    /// </summary>
    /// <remarks>
    /// The distinction lives here because the availability vocabulary
    /// belongs to the command layer, not the palette. Mac makes the same
    /// call by comparing against one host constant; a Windows palette
    /// hardcoding that string would misreport its own bridge's
    /// rejections — which emit different copy — as "{label} failed:
    /// {rejection}".
    /// </remarks>
    bool IsAvailabilityRejection(string message);
}
