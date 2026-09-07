// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Bases;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR B2 (#746), B2-5: the Bases' Show connections — the mac's
/// reserved row action (Bases gap O15) — named by core's title, addressed
/// by the invoking document being the active hosted one, entering the
/// workspace's re-root funnel on the row's note; and from a base tab no
/// effective root, so nothing is pushed and Back falls through (B2-D7).
/// </summary>
public sealed class ConnectionsReRootBasesTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    private readonly List<A11yEvent> _announced = [];

    public ConnectionsReRootBasesTests()
    {
        _fixture = FixtureVault.Create(3, "connections-bases");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "Notes.base"),
            "filters: 'file.ext == \"md\"'\n" +
            "views:\n" +
            "  - type: table\n" +
            "    name: Main\n" +
            "    order:\n" +
            "      - file.name\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "Other.base"),
            "filters: 'file.ext == \"md\"'\n" +
            "views:\n" +
            "  - type: table\n" +
            "    name: Main\n");
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    private WorkspaceViewModel Workspace() =>
        new(_session, _fixture.Root, () => [], _announced.Add, startInteractionBackgroundWork: false);

    private static void Settle(WorkspaceViewModel workspace)
    {
        PumpedDispatcher.PumpUntilDrained(workspace.Connections.WhenAllWorkDrained());
        PumpedDispatcher.Drain();
    }

    /// <summary>B2-5: the action's name is core's title for the action, from
    /// the Note vector the leaf fetched once — installed on the document by
    /// the workspace beside the seam.</summary>
    [Fact]
    public void TheBasesRowActionIsNamedByCoresTitle()
    {
        PumpedDispatcher.Run(() =>
        {
            using WorkspaceViewModel workspace = Workspace();
            workspace.OpenPath("Notes.base");
            BaseDocumentViewModel document = Assert.IsType<BaseDocumentViewModel>(workspace.ActiveGroup.ActiveTab!.Base);
            string expected = SlateUniffiMethods.GraphRowActions(GraphNodeKind.Note)
                .Single(spec => spec.Action == GraphRowAction.ShowConnections).Title;
            Assert.Equal(expected, document.ShowConnectionsTitle);
            Assert.Equal(expected, workspace.Connections.ActionTitle(GraphRowAction.ShowConnections));
            Assert.NotNull(document.ShowConnectionsFromSurface);
        });
    }

    /// <summary>B2-5 (IGJ-9): the entrance is addressed — the invoking
    /// document must be the ACTIVE hosted one; then the funnel pins the
    /// leaf on the row's note, and from a base tab nothing was pushed
    /// (B2-D7), so the first Back falls through.</summary>
    [Fact]
    public void TheBasesShowConnectionsIsAddressedByTheActiveDocument()
    {
        PumpedDispatcher.Run(() =>
        {
            using WorkspaceViewModel workspace = Workspace();
            workspace.OpenPath("Notes.base");
            BaseDocumentViewModel notes = Assert.IsType<BaseDocumentViewModel>(workspace.ActiveGroup.ActiveTab!.Base);
            Assert.Equal(BaseLoadState.Ready, notes.State);
            BasesRow row = notes.Result!.Rows[0];

            // Another base active: the invoking SOURCE is the Notes tab —
            // hosted, inactive — and the entrance is ADDRESSED to it: the
            // tab is made active, then the funnel pins (B2-5; codex
            // post-implementation pass 2, IPC-9). A source that is not hosted
            // — a tab no group holds — invokes nothing.
            WorkspaceTabViewModel notesTab = workspace.ActiveGroup.ActiveTab!;
            workspace.OpenPath("Other.base", WorkspaceOpenTarget.NewTab);
            WorkspaceTabViewModel otherTab = workspace.ActiveGroup.ActiveTab!;
            Assert.NotSame(notes, otherTab.Base);
            // A source that hosts another document invokes nothing.
            Assert.False(workspace.BasesShowConnectionsFor(otherTab, notes, row));
            Assert.Null(workspace.Connections.Pin);
            // A source that is gone — a duplicate of the Notes tab, hosting the
            // same document, then closed — invokes nothing.
            workspace.ActiveGroup.ActiveTab = notesTab;
            workspace.DuplicateTabCommand.Execute(null);
            WorkspaceTabViewModel closing = workspace.ActiveGroup.ActiveTab!;
            Assert.NotSame(notesTab, closing);
            Assert.Same(notes, closing.Base);
            workspace.CloseActiveTabCommand.Execute(null);
            Assert.DoesNotContain(closing, workspace.ActiveGroup.Tabs);
            Assert.False(workspace.BasesShowConnectionsFor(closing, notes, row));
            Assert.Null(workspace.Connections.Pin);
            workspace.ActiveGroup.ActiveTab = otherTab;
            Assert.NotSame(notesTab, workspace.ActiveGroup.ActiveTab);

            // The Notes tab as the source while another is in view: made
            // active first, then the funnel pins the leaf on the row's note;
            // from a base tab there was no effective root, so nothing was
            // pushed and Back falls through.
            Assert.Null(workspace.Connections.Root);
            Assert.True(workspace.BasesShowConnectionsFor(notesTab, notes, row));
            Settle(workspace);
            Assert.Equal(row.FilePath, workspace.Connections.Pin);
            Assert.Equal(row.FilePath, workspace.Connections.Root);
            Assert.Equal(row.FilePath, workspace.ActiveGroup.ActiveTab!.Path);
            Assert.Empty(workspace.Connections.BackStack);
            Assert.Equal(SlateUniffiMethods.GraphStableKeyForPath(row.FilePath), workspace.GraphViewStateForTests.SelectedKey);
            Assert.False(workspace.ConnectionsBack());
            // The command shares the route: the base reopened (the re-root's
            // open had replaced its tab, so the funnel re-seats a document)
            // and a row selected, it pins too.
            workspace.OpenPath("Notes.base");
            BaseDocumentViewModel reopened = Assert.IsType<BaseDocumentViewModel>(workspace.ActiveGroup.ActiveTab!.Base);
            Assert.Equal(BaseLoadState.Ready, reopened.State);
            reopened.SelectedRow = reopened.Result!.Rows[1];
            Assert.True(workspace.BasesShowConnectionsCommand.CanExecute(null));
            workspace.BasesShowConnectionsCommand.Execute(null);
            Settle(workspace);
            Assert.Equal(reopened.Result!.Rows[1].FilePath, workspace.Connections.Pin);
        });
    }

    /// <summary>B2-5 (IGJ-9; codex post-implementation pass 2, IPC-9): ONE
    /// document hosted by base tabs in TWO groups — a surface per tab — and
    /// an action invoked from the surface whose group is NOT active: the
    /// source tab's group and the tab are made active first, then the funnel
    /// pins, so the re-root's open lands in the invoking group, never the
    /// other; the document-level check alone could not tell them apart.</summary>
    [Fact]
    public void AnActionFromTheInactiveGroupsSurfaceAddressesThatGroupFirst()
    {
        PumpedDispatcher.Run(() =>
        {
            using WorkspaceViewModel workspace = Workspace();
            workspace.OpenPath("Notes.base");
            BaseDocumentViewModel notes = Assert.IsType<BaseDocumentViewModel>(workspace.ActiveGroup.ActiveTab!.Base);
            WorkspaceTabViewModel first = workspace.ActiveGroup.ActiveTab!;
            WorkspaceGroupViewModel firstGroup = workspace.ActiveGroup;
            // The split duplicates the tab into a new, active group: the same
            // document, a second tab, a second surface.
            workspace.SplitRightCommand.Execute(null);
            WorkspaceGroupViewModel secondGroup = workspace.ActiveGroup;
            Assert.NotSame(firstGroup, secondGroup);
            WorkspaceTabViewModel second = secondGroup.ActiveTab!;
            Assert.Same(notes, second.Base);
            Assert.Same(notes, first.Base);
            Assert.Same(notes, workspace.ActiveBaseDocument);
            BasesRow row = notes.Result!.Rows[0];

            // The FIRST group's surface invokes while the second is active.
            Assert.True(workspace.BasesShowConnectionsFor(first, notes, row));
            Settle(workspace);
            Assert.Same(firstGroup, workspace.ActiveGroup);
            Assert.Equal(row.FilePath, workspace.Connections.Pin);
            // The re-root's open landed in the invoking group: its tab now
            // shows the note; the second group's tab still shows the base.
            Assert.Equal(row.FilePath, firstGroup.ActiveTab!.Path);
            Assert.Same(notes, secondGroup.ActiveTab!.Base);
        });
    }
}
