// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.FileManagement;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W6-2 PR A (#746, contract A-8; AD-8): the create funnel's workspace
/// half — the invocation, addressed to the graph's pane, and the
/// completion, keyed by the workspace's lifecycle generation. This is the
/// ONE site outside <c>Graph/</c> that posts graph-family events, and it
/// sits outside the walled directory on purpose (IPA-7): the directory
/// censuses wall <c>Graph/</c> against announcing, and the seam census
/// names this file's completion as the only exception.
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    /// <summary>The graph asked for a ghost's note: the address is
    /// activated NOW (IGA-41/56 — every action captures its tab and group
    /// and activates them at invocation; a completion never activates),
    /// the lifecycle generation is captured, and the create runs on the
    /// workspace's own worker, so a graph closed meanwhile cannot swallow
    /// the landing.</summary>
    private void CreateGraphNoteFromSurface(string path)
    {
        if (GraphNoteCreator is not { } creator || !FocusGraphAddress())
        {
            return;
        }
        (WorkspaceGroupViewModel Group, WorkspaceTabViewModel Tab)? address = GraphAddress();
        int generation = LifecycleGeneration();
        _graphNoteCreation.Run(
            () => creator.TryCreateNote(path, string.Empty),
            result => GraphNoteCreationCompleted(path, address, generation, creator, result));
    }

    /// <summary>The completion (AD-8), whatever the graph's liveness —
    /// while the lifecycle generation is still the one the create was
    /// started under: the landing's bookkeeping, the open when the address
    /// is still current, ONE <c>NoteCreated</c> after the attempt, then
    /// the caveat; or the failure. A completion from an earlier lifecycle
    /// is dropped whole: its session, sidebar and workspace are gone.</summary>
    private void GraphNoteCreationCompleted(
        string path,
        (WorkspaceGroupViewModel Group, WorkspaceTabViewModel Tab)? address,
        int generation,
        ISurfaceNoteCreator creator,
        NoteCreateResult result)
    {
        if (generation != LifecycleGeneration())
        {
            return;
        }
        switch (result)
        {
            case NoteCreateResult.Landed landed:
                creator.NoteLanded(path);
                // Both identities of the captured address (IPC-4): the same
                // tab, still owned by the same group, that group active and
                // the tab its active one.
                bool addressCurrent = address is { } captured
                    && Groups.Contains(captured.Group)
                    && captured.Group.Tabs.Contains(captured.Tab)
                    && ReferenceEquals(ActiveGroup, captured.Group)
                    && ReferenceEquals(captured.Group.ActiveTab, captured.Tab)
                    && captured.Tab.IsGraph;
                if (addressCurrent)
                {
                    RunWorkspaceMutation(() => _ = OpenPathCore(path, WorkspaceOpenTarget.CurrentTab));
                }
                _announce(new A11yEvent.Graph(new GraphA11yEvent.GraphStatus(
                    new GraphStatusNote.NoteCreated(System.IO.Path.GetFileName(path)))));
                if (landed.Caveat is { } caveat)
                {
                    creator.SpeakCaveat(caveat);
                }
                break;
            case NoteCreateResult.Exists exists:
                _announce(new A11yEvent.Graph(new GraphA11yEvent.GraphBlocked(
                    new GraphBlockedReason.NoteCreateFailed(exists.Message))));
                break;
            case NoteCreateResult.Failed failed:
                _announce(new A11yEvent.Graph(new GraphA11yEvent.GraphBlocked(
                    new GraphBlockedReason.NoteCreateFailed(failed.Message))));
                break;
            default:
                // Unavailable: the session is shutting down — a retired-token drop.
                break;
        }
    }
}
