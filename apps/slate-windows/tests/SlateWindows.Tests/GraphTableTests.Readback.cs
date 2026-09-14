// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed partial class GraphTableTests
{
    private static GraphWhereAmISelection.Node ReadbackNode(GraphDocumentViewModel document, GraphTableRow row)
    {
        GraphA11yEvent.GraphWhereAmI readback = Assert.IsType<GraphA11yEvent.GraphWhereAmI>(document.TableWhereAmI());
        var node = Assert.IsType<GraphWhereAmISelection.Node>(readback.Selection);
        Assert.Equal(new GraphRowCopy(row.Label, row.Kind, row.LinksIn, row.LinksOut, row.LinksIn, false), node.Row);
        Assert.Equal(row.Component, node.Component);
        Assert.Null(readback.ZoomPercent);
        return node;
    }

    [Theory]
    [InlineData("preset")]
    [InlineData("needle")]
    [InlineData("escape")]
    public void ASilentSingleRowLandingReadsTheRowWithoutSelectingOrPostingAMove(string route)
    {
        RunSta(() =>
        {
            FixtureVault vault = FixtureVault.Create(0, "graph-seated-" + route);
            File.WriteAllText(Path.Combine(vault.Root, "alone.md"), "# Alone\nNo links.\n");
            using var host = new Host(vault);
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            PumpLoadedState();
            Assert.True(view.FilterFieldForTests.Focus());
            if (route == "preset")
            {
                host.Workspace.GraphNavigator.RunPreset(GraphPreset.Orphans);
            }
            else
            {
                view.FilterFieldForTests.Text = route == "escape" ? "no-such-note" : "alone";
            }
            host.Settle(document);
            if (route == "escape")
            {
                Assert.Empty(document.Publication.Rows);
                PressPreview(view.FilterFieldForTests, Key.Escape);
                host.Settle(document);
            }
            host.GraphLines.Clear();
            host.Workspace.RequestActiveEditorFocus();
            window.UpdateLayout();
            GraphTableRow row = Assert.Single(document.Publication.Rows);
            Assert.True(GridHasTheKeys(view));
            Assert.Same(row, view.TableForTests.GridForTests.Grid.CurrentCell.Item);
            Assert.Null(document.ViewState.SelectedKey);
            _ = ReadbackNode(document, row);
            Assert.Empty(host.GraphLines);

            Assert.True(host.Workspace.GraphNavigator.WhereAmI());
            Assert.Equal(WhereAmIRender(document.TableWhereAmI()!), host.GraphLines.Single());
            Assert.Equal(host.GraphLines.Single(), view.WhereAmIReadbackForTests.Text);
            Assert.Null(document.ViewState.SelectedKey);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void WithoutANativeSeatOnlyASoleCurrentRowIsUnambiguous(int count)
    {
        RunSta(() =>
        {
            using var host = new Host(count, "graph-no-native-seat");
            GraphDocumentViewModel document = host.Open();
            Assert.Null(host.Workspace.GraphNavigator.PresenterForTests);
            Assert.Equal(count, document.Publication.Rows.Count);
            Assert.Null(document.ViewState.SelectedKey);
            if (count == 1)
            {
                _ = ReadbackNode(document, Assert.Single(document.Publication.Rows));
            }
            else
            {
                Assert.IsType<GraphWhereAmISelection.NoSelection>(document.TableWhereAmI()!.Selection);
            }
        });
    }

    [Fact]
    public void ARepublishedNativeSeatWinsOverTheFirstOfSeveralRows()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-seat-republish");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.Request(new GraphRequest.Sort(new GraphTableSort(GraphTableColumn.Note, true))));
            host.Settle(document);
            GraphWhereAmISelection? beforeTableBind = null;
            document.PublicationInstalled += _ => beforeTableBind = document.TableWhereAmI()?.Selection;
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            PumpLoadedState();
            host.Workspace.RequestActiveEditorFocus();
            window.UpdateLayout();
            var landed = Assert.IsType<GraphTableRow>(view.TableForTests.GridForTests.Grid.CurrentCell.Item);
            Assert.Null(document.ViewState.SelectedKey);
            Assert.True(document.Request(new GraphRequest.Sort(new GraphTableSort(GraphTableColumn.Note, false))));
            host.Settle(document);
            window.UpdateLayout();
            var current = Assert.IsType<GraphTableRow>(view.TableForTests.GridForTests.Grid.CurrentCell.Item);
            Assert.Equal(landed.StableKey, current.StableKey);
            Assert.NotEqual(document.Publication.Rows[0].StableKey, current.StableKey);
            Assert.IsType<GraphWhereAmISelection.NoSelection>(beforeTableBind);
            host.GraphLines.Clear();
            _ = ReadbackNode(document, current);
            Assert.Null(document.ViewState.SelectedKey);
            Assert.Empty(host.GraphLines);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AHiddenOrMissingSharedKeyNeverFallsBackToAnotherSoleRow(bool missing)
    {
        RunSta(() =>
        {
            using var host = new Host(3, "graph-hidden-key-seat");
            GraphDocumentViewModel document = host.Open();
            GraphTableRow hidden = document.Publication.Rows.Single(row => row.Path == "note0.md");
            host.Workspace.GraphNavigator.SetNameQuery("note1");
            host.Settle(document);
            document.ViewState.SelectedKey = missing ? "p:missing.md" : hidden.StableKey;
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            PumpLoadedState();
            host.Workspace.RequestActiveEditorFocus();
            window.UpdateLayout();
            GraphTableRow other = Assert.Single(document.Publication.Rows);
            Assert.Same(other, view.TableForTests.GridForTests.Grid.CurrentCell.Item);
            string? key = document.ViewState.SelectedKey;
            Assert.NotNull(key);
            Assert.IsType<GraphWhereAmISelection.NoSelection>(document.TableWhereAmI()!.Selection);
            Assert.Equal(key, document.ViewState.SelectedKey);
        });
    }

    [Fact]
    public void ACellRetainedFromAnOlderPublicationCannotSupplyReadback()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "graph-stale-seat");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            PumpLoadedState();
            host.Workspace.RequestActiveEditorFocus();
            GraphPublication previous = document.Publication;
            GraphTableRow old = previous.Rows[0];
            Assert.True(document.Request(new GraphRequest.Needle()));
            host.Settle(document);
            window.UpdateLayout();
            Assert.NotSame(previous, document.Publication);
            Assert.Null(view.ReadTableSeat(document, previous));

            // WPF can retain currency when items are replaced. Even an old
            // row with equal field values is not one of this publication's rows.
            DataGrid grid = view.TableForTests.GridForTests.Grid;
            grid.ItemsSource = new[] { old };
            grid.CurrentCell = new DataGridCellInfo(old, grid.Columns[0]);
            document.ViewState.SelectedKey = null;
            Assert.DoesNotContain(document.Publication.Rows, row => ReferenceEquals(row, old));
            Assert.Null(view.ReadTableSeat(document, document.Publication));
            Assert.IsType<GraphWhereAmISelection.NoSelection>(document.TableWhereAmI()!.Selection);
        });
    }

    [Fact]
    public void PendingAndFailedQueriesCannotReadThePreviouslySeatedRow()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "graph-seat-query-currency");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            PumpLoadedState();
            host.Workspace.RequestActiveEditorFocus();
            Assert.IsType<GraphWhereAmISelection.Node>(document.TableWhereAmI()!.Selection);
            using (var park = new ParkedFetch(document, 1))
            {
                host.Workspace.GraphNavigator.SetNameQuery("note0");
                park.WaitReached();
                Assert.Null(document.TableWhereAmI());
                Assert.False(host.Workspace.GraphNavigator.CanWhereAmI);
                park.Release();
                host.Settle(document);
            }
            _ = ReadbackNode(document, Assert.Single(document.Publication.Rows));
            document.FetchGateForTests = () => throw new InvalidOperationException("injected readback query failure");
            host.Workspace.GraphNavigator.SetNameQuery("note1");
            host.Settle(document);
            Assert.Null(document.TableWhereAmI());
            Assert.False(host.Workspace.GraphNavigator.WhereAmI());
        });
    }

    [Fact]
    public void AnUnloadedOrHiddenPresenterCannotSupplyANativeSeat()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "graph-seat-unload");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            Assert.Null(view.ReadTableSeat(document, document.Publication));
            var stack = new StackPanel();
            stack.Children.Add(view);
            using HostedWindow window = HostInWindow(stack);
            PumpLoadedState();
            host.Workspace.RequestActiveEditorFocus();
            Assert.NotNull(view.ReadTableSeat(document, document.Publication));
            document.ViewState.Mode = GraphSurfaceMode.Diagram;
            Assert.Null(view.ReadTableSeat(document, document.Publication));
            document.ViewState.Mode = GraphSurfaceMode.Table;
            view.Visibility = Visibility.Hidden;
            Assert.Null(view.ReadTableSeat(document, document.Publication));
            Assert.IsType<GraphWhereAmISelection.NoSelection>(document.TableWhereAmI()!.Selection);
            view.Visibility = Visibility.Visible;
            stack.Children.Remove(view);
            window.UpdateLayout();
            PumpLoadedState();
            Assert.Null(host.Workspace.GraphNavigator.PresenterForTests);
            Assert.Null(view.ReadTableSeat(document, document.Publication));
            Assert.IsType<GraphWhereAmISelection.NoSelection>(document.TableWhereAmI()!.Selection);
        });
    }

    [Fact]
    public void AReplacementPresenterSurvivesTheOldPresentersLateDetach()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "graph-seat-replacement");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView old = SurfaceFor(host, document);
            GraphSurfaceView current = SurfaceFor(host, document);
            var stack = new StackPanel();
            stack.Children.Add(old);
            stack.Children.Add(current);
            using HostedWindow window = HostInWindow(stack);
            PumpLoadedState();
            Assert.True(old.TableForTests.FocusProjection());
            Assert.Same(old, host.Workspace.GraphNavigator.PresenterForTests);
            Assert.True(current.TableForTests.FocusProjection());
            Assert.Same(current, host.Workspace.GraphNavigator.PresenterForTests);
            stack.Children.Remove(old);
            window.UpdateLayout();
            PumpLoadedState();
            Assert.Same(current, host.Workspace.GraphNavigator.PresenterForTests);
            _ = ReadbackNode(document, Assert.IsType<GraphTableRow>(current.TableForTests.GridForTests.Grid.CurrentCell.Item));

            // A template rebound to another document cannot lend its old
            // native seat to either workspace's current readback.
            using var other = new Host(3, "graph-seat-other-model");
            GraphDocumentViewModel replacement = other.Open();
            current.Model = replacement;
            Assert.Null(current.ReadTableSeat(document, document.Publication));
            Assert.Null(current.ReadTableSeat(replacement, replacement.Publication));
            Assert.IsType<GraphWhereAmISelection.NoSelection>(document.TableWhereAmI()!.Selection);
        });
    }

    [Fact]
    public void AGraphInAnInactiveSplitCannotLendItsNativeSeat()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "graph-seat-inactive-pane");
            GraphDocumentViewModel document = host.Open();
            string note = document.Publication.Rows[0].Path!;
            host.Workspace.OpenPath(note, WorkspaceOpenTarget.NewTab);
            host.Workspace.SplitRightCommand.Execute(null);
            WorkspaceGroupViewModel other = host.Workspace.ActiveGroup;
            host.Workspace.OpenGraph();
            host.Settle(document);
            WorkspaceGroupViewModel graphGroup = host.Workspace.ActiveGroup;
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            PumpLoadedState();
            host.Workspace.RequestActiveEditorFocus();
            Assert.NotNull(view.ReadTableSeat(document, document.Publication));
            host.Workspace.SelectGroupFromKeyboardFocus(other);
            Assert.False(document.IsEffective);
            Assert.Null(view.ReadTableSeat(document, document.Publication));
            Assert.False(host.Workspace.GraphNavigator.WhereAmI());
            host.Workspace.SelectGroupFromKeyboardFocus(graphGroup);
            host.Settle(document);
            host.Workspace.RequestActiveEditorFocus();
            Assert.IsType<GraphWhereAmISelection.Node>(document.TableWhereAmI()!.Selection);
        });
    }
}
