// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 7 (#1252, R-9): the host's side of core's rescan delta ledger
/// (<c>crates/slate-core/src/scan_delta.rs</c>). A rescan leaves its delta
/// as a Pending generation; the host reads it in bounded, removal-first
/// pages, applies each page's effects, reports the page applied, and —
/// after the last page — releases the ledger into the spoken counts.
/// </summary>
/// <remarks>
/// Every call is a synchronous core call the host makes OFF the dispatcher
/// (locked decision 05 §4.1). Production binds this to the session one to
/// one
/// (<see cref="SessionScanDeltaChannel"/>). It is a seam only so the
/// reconciliation facts can wrap that SAME binding to fail a page or
/// record when each call arrives relative to the host's effects; the
/// effects themselves always run through the real rescan → delta → tab
/// path.
/// </remarks>
internal interface IScanDeltaChannel
{
    /// <summary>The Pending generation and the cursor its effects reached.</summary>
    ScanDeltaPending? Pending();

    /// <summary>One bounded page of the Pending generation from
    /// <paramref name="cursor"/> (null = the first row). A cancelled
    /// <paramref name="cancel"/> fails the read closed
    /// (<c>VaultException.Cancelled</c>) before it reads.</summary>
    ScanDeltaPage ReadPage(ulong generation, string? cursor, uint limit, CancelToken cancel);


    /// <summary>Every entry before <paramref name="nextCursor"/> has had its
    /// effects applied; null after the LAST page marks the generation
    /// Applied (and coalesces it, in core, into the retained Applied
    /// generation).</summary>
    void PageApplied(ulong generation, string? nextCursor);

    /// <summary>Reduce the Applied generation into the spoken counts and
    /// release it.</summary>
    ScanDeltaOutcome Release();
}

/// <summary>The production channel: the session's own ledger calls.</summary>
internal sealed class SessionScanDeltaChannel(VaultSession session) : IScanDeltaChannel
{
    public ScanDeltaPending? Pending() => session.ScanDeltaPending();

    public ScanDeltaPage ReadPage(ulong generation, string? cursor, uint limit, CancelToken cancel) =>
        session.ScanDeltaPage(generation, new Paging(cursor, limit), cancel);


    public void PageApplied(ulong generation, string? nextCursor) =>
        session.ScanDeltaPageApplied(generation, nextCursor);

    public ScanDeltaOutcome Release() => session.ScanDeltaRelease();
}
