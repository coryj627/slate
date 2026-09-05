// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.FileManagement;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>The SOURCE a ghost create is addressed by (W6-2 PR B, B-11):
/// the graph tab's address (PR A, AD-8) or the leaf's root and epoch.</summary>
internal abstract record GraphCreateSource
{
    /// <summary>PR A's address: the singleton tab, its group and the
    /// document seated on it, compared by every identity at completion.</summary>
    internal sealed record GraphTab(
        WorkspaceGroupViewModel Group,
        WorkspaceTabViewModel Tab,
        Graph.GraphDocumentViewModel Document) : GraphCreateSource;

    /// <summary>The leaf's address: the root the ghost belonged to and the
    /// root EPOCH at invocation — A → B → A advances it twice, so a create
    /// parked across the excursion lands and speaks but opens nothing.
    /// The leaf's ACTIVE state is not consulted (IGG-8; B-D10).</summary>
    internal sealed record Leaf(string Root, int Epoch) : GraphCreateSource;
}

/// <summary>
/// W6-2 PR A (#746, contract A-8; AD-8) and PR B (B-11): the create
/// funnel's workspace half — the invocation, addressed by its SOURCE, and
/// the completion, keyed by the workspace's lifecycle generation. This is
/// the ONE site outside <c>Graph/</c> that posts graph-family events, and
/// it sits outside the walled directory on purpose (IPA-7): the directory
/// censuses wall <c>Graph/</c> against announcing, and the seam census
/// names this file's completion as the only exception.
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    /// <summary>The graph tab asked for a ghost's note (PR A): the address
    /// is activated NOW (IGA-41/56 — every action captures its tab and group
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
        GraphCreateSource? source = GraphAddress() is { } address
            ? new GraphCreateSource.GraphTab(address.Group, address.Tab, address.Document)
            : null;
        StartGraphNoteCreation(path, source, creator);
    }

    /// <summary>The leaf asked for a ghost's note (B-11): no graph tab is
    /// needed; the address is the root and its epoch at invocation.</summary>
    private void CreateConnectionsNoteFromSurface(string path, string root, int epoch)
    {
        if (GraphNoteCreator is not { } creator)
        {
            return;
        }
        StartGraphNoteCreation(path, new GraphCreateSource.Leaf(root, epoch), creator);
    }

    private void StartGraphNoteCreation(string path, GraphCreateSource? source, ISurfaceNoteCreator creator)
    {
        int generation = LifecycleGeneration();
        _graphNoteCreation.Run(
            () => creator.TryCreateNote(path, string.Empty),
            result => GraphNoteCreationCompleted(path, source, generation, creator, result));
    }

    /// <summary>Whether the captured source is still current at completion:
    /// the graph tab's every identity (IPC-4, IPD-2), or the leaf's same
    /// root at the exact epoch (B-11).</summary>
    private bool GraphCreateSourceIsCurrent(GraphCreateSource? source) => source switch
    {
        GraphCreateSource.GraphTab captured =>
            Groups.Contains(captured.Group)
            && captured.Group.Tabs.Contains(captured.Tab)
            && ReferenceEquals(ActiveGroup, captured.Group)
            && ReferenceEquals(captured.Group.ActiveTab, captured.Tab)
            && captured.Tab.IsGraph
            && ReferenceEquals(captured.Tab.Graph, captured.Document)
            && !captured.Document.IsRetired,
        GraphCreateSource.Leaf leaf =>
            !Connections.IsRetired
            && string.Equals(Connections.Root, leaf.Root, StringComparison.Ordinal)
            && Connections.RootEpoch == leaf.Epoch,
        _ => false,
    };

    /// <summary>The completion (AD-8), whatever the source's liveness —
    /// while the lifecycle generation is still the one the create was
    /// started under: the landing's bookkeeping, the open when the source
    /// is still current, ONE <c>NoteCreated</c> after the attempt, then
    /// the caveat; or the failure. A completion from an earlier lifecycle
    /// is dropped whole: its session, sidebar and workspace are gone.</summary>
    private void GraphNoteCreationCompleted(
        string path,
        GraphCreateSource? source,
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
                if (GraphCreateSourceIsCurrent(source))
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
                // The failure is a HIGH graph event and rides the workspace's
                // one relay, whose High flush drops every pending class across
                // both surfaces (A-10 as amended); the NoteCreated line above
                // is A-8's one direct post (codex post-implementation pass 1,
                // IPB-1: the failure arms used to bypass the flush).
                _graphRelay.Announce(new GraphA11yEvent.GraphBlocked(
                    new GraphBlockedReason.NoteCreateFailed(exists.Message)));
                break;
            case NoteCreateResult.Failed failed:
                _graphRelay.Announce(new GraphA11yEvent.GraphBlocked(
                    new GraphBlockedReason.NoteCreateFailed(failed.Message)));
                break;
            default:
                // Unavailable: the session is shutting down — a retired-token drop.
                break;
        }
    }
}
