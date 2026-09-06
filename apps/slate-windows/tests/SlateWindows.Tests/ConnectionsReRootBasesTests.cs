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

            // Another base active: the Notes document is not the invoking
            // one's host — refused, nothing pinned.
            workspace.OpenPath("Other.base", WorkspaceOpenTarget.NewTab);
            Assert.NotSame(notes, workspace.ActiveGroup.ActiveTab!.Base);
            Assert.False(workspace.BasesShowConnectionsFor(notes, row));
            Assert.Null(workspace.Connections.Pin);

            // The Notes tab active: the funnel pins the leaf on the row's
            // note; from a base tab there was no effective root, so nothing
            // was pushed and Back falls through.
            workspace.ActiveGroup.ActiveTab = workspace.ActiveGroup.Tabs.First(tab => ReferenceEquals(tab.Base, notes));
            Assert.Same(notes, workspace.ActiveGroup.ActiveTab!.Base);
            Assert.Null(workspace.Connections.Root);
            Assert.True(workspace.BasesShowConnectionsFor(notes, row));
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
}
