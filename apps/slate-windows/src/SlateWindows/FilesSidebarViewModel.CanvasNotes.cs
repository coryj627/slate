// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>§G2 TG2-6 (G2-11, IG2-6): the sidebar as the canvas's note
/// creator — Convert Card to Note… writes through the SAME create path
/// every sidebar create takes (the session-work lease, the typed create
/// outcomes with their unindexed caveat, the structural history barrier,
/// the tree refresh), so a note born on the canvas is a note the sidebar
/// made. The worker-safe half runs inside the canvas gate; the UI half
/// runs where the canvas posts it.</summary>
internal sealed partial class FilesSidebarViewModel : ICanvasNoteCreator
{
    public CanvasNoteCreateResult TryCreateNote(string path, string content)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            if (!TryRunSessionWork(
                // The caveat names the LEAF, never a path (the #1123 rule).
                () => CreateOutcomes.CreateReporting(
                    _session, path, content, System.IO.Path.GetFileName(path)),
                out string? caveat))
            {
                // IG2-56: session work refused at shutdown — nothing ran.
                return new CanvasNoteCreateResult.Unavailable();
            }
            return new CanvasNoteCreateResult.Landed(caveat);
        }
        catch (VaultException.DestinationExists)
        {
            return new CanvasNoteCreateResult.Exists();
        }
        catch (VaultException exception)
        {
            return new CanvasNoteCreateResult.Failed(exception.Message);
        }
    }

    /// <summary>On the dispatcher (IG2-55): the note bypassed the tree's
    /// own mutation path, so it is a structural-history BARRIER; the tree
    /// refreshes to list it; the unindexed caveat, when there is one, is
    /// spoken the way every sidebar create speaks it — after the canvas's
    /// own confirmation.</summary>
    public void NoteLanded(string path, string? caveat)
    {
        ArgumentNullException.ThrowIfNull(path);
        StructuralHistoryBarrier();
        Refresh();
        if (caveat is not null)
        {
            ReportFailure(caveat);
        }
    }
}
