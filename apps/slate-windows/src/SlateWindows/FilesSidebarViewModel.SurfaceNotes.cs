// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.FileManagement;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>W6-2 PR A (#746), contracts A-8 and AD-11: the sidebar as
/// every surface's note creator (the canvas's seam generalised with typed
/// outcomes) and as the target of "Reveal in File Tree" — select the
/// note in the tree, the mac's meaning, distinct from the sidebar's own
/// "Reveal in File Explorer" (FD-5).</summary>
internal sealed partial class FilesSidebarViewModel : ISurfaceNoteCreator
{
    private string? _pendingSurfaceSelectPath;

    // Explicit: the canvas seam's TryCreateNote shares the parameter list
    // and answers with its own record; both stay one implementation.
    NoteCreateResult ISurfaceNoteCreator.TryCreateNote(string path, string content)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            if (!TryRunSessionWork(
                () => CreateOutcomes.CreateReporting(
                    _session, path, content, System.IO.Path.GetFileName(path)),
                out string? caveat))
            {
                return new NoteCreateResult.Unavailable();
            }
            return new NoteCreateResult.Landed(caveat);
        }
        catch (VaultException.DestinationExists exception)
        {
            return new NoteCreateResult.Exists(exception.Message);
        }
        catch (VaultException exception)
        {
            return new NoteCreateResult.Failed(exception.Message);
        }
    }

    public void NoteLanded(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        StructuralHistoryBarrier();
        Refresh();
    }

    public void SpeakCaveat(string caveat)
    {
        ArgumentNullException.ThrowIfNull(caveat);
        ReportFailure(caveat);
    }

    /// <summary>Select the node for a vault-relative path: expand its
    /// ancestors, select it, and hand focus to the tree. A node the
    /// tree has not materialised yet (a collapsed deep parent loading its
    /// children) is selected when the next publication lists it.</summary>
    internal void SelectPathFromSurface(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _pendingSurfaceSelectPath = path;
        foreach (FileTreeNodeViewModel node in Flatten(RootNodes).ToList())
        {
            if (node.IsDirectory
                && node.Path.Length > 0
                && path.StartsWith(node.Path + "/", StringComparison.Ordinal))
            {
                node.IsExpanded = true;
            }
        }
        ConsumePendingSurfaceSelection();
    }

    /// <summary>Consumed at tree publication and at the request itself.</summary>
    private void ConsumePendingSurfaceSelection()
    {
        if (_pendingSurfaceSelectPath is not string pending)
        {
            return;
        }
        FileTreeNodeViewModel? target = Flatten(RootNodes)
            .FirstOrDefault(node => node.Path == pending && !node.IsPlaceholder);
        if (target is null)
        {
            return;
        }
        _pendingSurfaceSelectPath = null;
        SelectedNode = target;
        TreeSelectionRestored?.Invoke();
    }
}
