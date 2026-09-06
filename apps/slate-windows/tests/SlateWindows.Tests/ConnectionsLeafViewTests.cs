// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR B, slice B1 (#746), contracts B-6, B-8, B-9, B-13, B-16: the
/// leaf's view — the four patterns on the DATA peer at both nesting levels,
/// the occurrence as the identity, expansion restored across a refresh and
/// cleared on a root change, the model's release, the state labels per
/// state, the depth control, the row's Name / ItemStatus / HelpText, and
/// the row menu with Show connections disabled and its reason.
/// </summary>
public sealed class ConnectionsLeafViewTests
{
    private const string Hub = "hub.md";
    private const string Two = "2.md";

    private sealed class Host : IDisposable
    {
        public string Root { get; }
        public VaultSession Session { get; }
        public WorkspaceViewModel Workspace { get; }
        public List<string> RelayLines { get; } = [];

        public Host(string label)
        {
            string source = Path.Combine(SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures", "graph_vault");
            Root = Path.Combine(Path.GetTempPath(), $"slate-connections-view-{label}-{Guid.NewGuid():N}");
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(Root, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            Session = VaultSession.OpenFilesystem(Root);
            using var cancel = new CancelToken();
            Session.ScanInitial(cancel);
            Workspace = new WorkspaceViewModel(
                Session,
                Root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: false,
                announceRendered: line => RelayLines.Add(line.Text));
        }

        public ConnectionsLeafViewModel Leaf => Workspace.Connections;

        public void ActivateLeaf() =>
            Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "connections");

        public void OpenNote(string path) => Workspace.OpenPath(path, WorkspaceOpenTarget.CurrentTab);

        public void Settle()
        {
            PumpedDispatcher.PumpUntilDrained(Leaf.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
        }

        public void Dispose()
        {
            Workspace.Dispose();
            Session.Dispose();
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(body);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    /// <summary>A window hosts the view so containers realise and peers
    /// project (a detached control has no automation tree).</summary>
    private static (Window Window, ConnectionsLeafView View) Show(ConnectionsLeafViewModel model)
    {
        var view = new ConnectionsLeafView { Model = model };
        var window = new Window { Width = 400, Height = 600, Content = view, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        window.Show();
        view.UpdateLayout();
        return (window, view);
    }

    /// <summary>The ITEM peers a parent projects — the rows — apart from the
    /// container's own visuals (the expander toggle, the header text).</summary>
    private static List<AutomationPeer> DataPeers(AutomationPeer parent) =>
        [.. (parent.GetChildren() ?? []).OfType<ItemAutomationPeer>()];

    /// <summary>The four patterns as PROVIDERS — a peer that answers a
    /// pattern with an object that does not implement it answers nothing
    /// a UIA client can call.</summary>
    private static void AssertTheFourPatterns(AutomationPeer peer, string what)
    {
        Assert.IsType<ConnectionsRowDataPeer>(peer);
        Assert.IsAssignableFrom<System.Windows.Automation.Provider.IInvokeProvider>(peer.GetPattern(PatternInterface.Invoke));
        Assert.IsAssignableFrom<System.Windows.Automation.Provider.ISelectionItemProvider>(peer.GetPattern(PatternInterface.SelectionItem));
        Assert.IsAssignableFrom<System.Windows.Automation.Provider.IExpandCollapseProvider>(peer.GetPattern(PatternInterface.ExpandCollapse));
        Assert.IsAssignableFrom<System.Windows.Automation.Provider.IScrollItemProvider>(peer.GetPattern(PatternInterface.ScrollItem));
        Assert.True(peer.GetAutomationId().Length > 0, what + " has no automation id");
    }

    [Fact]
    public void TheFourPatternsAreOnTheDataPeerAtBothNestingLevelsAndTheOccurrenceIsTheIdentity()
    {
        RunSta(() =>
        {
            using var host = new Host("patterns");
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Leaf.SetDepth(2);
            host.Settle();
            (Window window, ConnectionsLeafView view) = Show(host.Leaf);
            try
            {
                AutomationPeer treePeer = UIElementAutomationPeer.CreatePeerForElement(view.TreeForTests)!;
                Assert.Equal(AutomationControlType.Tree, treePeer.GetAutomationControlType());
                Assert.NotNull(treePeer.GetPattern(PatternInterface.Selection));
                Assert.Equal("ConnectionsTree", treePeer.GetAutomationId());

                // Two group rows at the top; each projects its children through
                // the item peer's factory — the same Invoke-carrying data peer.
                List<AutomationPeer> groups = DataPeers(treePeer);
                Assert.Equal(2, groups.Count);
                AutomationPeer outgoing = groups[1];
                AssertTheFourPatterns(outgoing, "the outgoing group");
                Assert.Equal(ConnectionsPhrase.GroupHeader(ConnectionsPhrase.OutgoingTitle, host.Leaf.Publication.Tree!.Outgoing.Count(r => r.Level == 1)), outgoing.GetName());

                // Every first-hop row realised: a top-level connection row.
                foreach (ConnectionsRowViewModel row in view.RootsForTests[1].Children)
                {
                    Assert.NotNull(view.RealizeContainer(row));
                }
                view.UpdateLayout();
                List<AutomationPeer> rows = DataPeers(outgoing);
                Assert.NotEmpty(rows);
                AutomationPeer first = rows[0];
                AssertTheFourPatterns(first, "the first row");
                GraphConnectionRow core = host.Leaf.Publication.Tree!.Outgoing.First(r => r.Level == 1);
                Assert.Equal(core.Id, first.GetAutomationId());
                Assert.Equal(host.Leaf.RowName(core), first.GetName());
                Assert.Equal(ConnectionsRowViewModel.Badge(core), first.GetItemStatus());
                Assert.Equal(host.Leaf.RowHint(core), first.GetHelpText());

                // A NESTED row (level 2) — the item peer's factory, one level down.
                ConnectionsRowViewModel? parentRow = view.RootsForTests[1].Children.FirstOrDefault(r => r.Children.Count > 0);
                Assert.NotNull(parentRow);
                Assert.NotNull(view.RealizeContainer(parentRow.Children[0]));
                view.UpdateLayout();
                AutomationPeer parentPeer = rows.Single(p => p.GetAutomationId() == parentRow.Id);
                List<AutomationPeer> nested = DataPeers(parentPeer);
                Assert.NotEmpty(nested);
                AssertTheFourPatterns(nested[0], "the nested row");
                Assert.Equal(parentRow.Children[0].Id, nested[0].GetAutomationId());
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ExpansionSurvivesASameRootRefreshAndClearsOnARootChange()
    {
        RunSta(() =>
        {
            using var host = new Host("expansion");
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Leaf.SetDepth(2);
            host.Settle();
            (Window window, ConnectionsLeafView view) = Show(host.Leaf);
            try
            {
                ConnectionsRowViewModel parentRow = view.RootsForTests[1].Children.First(r => r.Children.Count > 0);
                parentRow.IsExpanded = false;
                Assert.False(view.ExpansionForTests[parentRow.Id]);

                // A same-root refresh (a Show) rebuilds and RESTORES the memory.
                host.Workspace.ShowConnections();
                host.Settle();
                ConnectionsRowViewModel rebuilt = view.RootsForTests[1].Children.First(r => r.Id == parentRow.Id);
                Assert.False(rebuilt.IsExpanded);

                // A same-root refresh that makes the SELECTED occurrence vanish
                // prunes — the surviving parent's memory is kept (B-6): the
                // document's cleared selection is not a clear of the view.
                host.Leaf.SelectedOccurrence = parentRow.Children[0].Id;
                host.Leaf.SetDepth(1);
                host.Settle();
                Assert.Null(host.Leaf.SelectedOccurrence);
                Assert.True(view.ExpansionForTests.ContainsKey(parentRow.Id));

                // A root change CLEARS it (B-6).
                host.OpenNote(Two);
                host.Settle();
                Assert.Empty(view.ExpansionForTests);
                Assert.Null(view.PendingFocusForTests);

                // A collapse of the pane CLEARS it (Term 2).
                host.Leaf.SetDepth(2);
                host.Settle();
                ConnectionsRowViewModel other = view.RootsForTests.SelectMany(g => g.Children).First(r => r.Children.Count > 0);
                other.IsExpanded = false;
                Assert.NotEmpty(view.ExpansionForTests);
                host.Workspace.IsRightPaneVisible = false;
                Assert.Empty(view.ExpansionForTests);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>B-6: the occurrence id names the path from the first hop, not
    /// the root, so two roots sharing a neighbour reuse an id — a root
    /// change must CLEAR the memory, not prune it (the prune would keep the
    /// shared id's entry).</summary>
    [Fact]
    public void ARootChangeClearsTheExpansionEvenForAnOccurrenceTheNewRootShares()
    {
        RunSta(() =>
        {
            using var host = new Host("shared-clear");
            File.WriteAllText(Path.Combine(host.Root, "left.md"), "# Left\n\n[[shared]]\n");
            File.WriteAllText(Path.Combine(host.Root, "right.md"), "# Right\n\n[[shared]]\n");
            File.WriteAllText(Path.Combine(host.Root, "shared.md"), "# Shared\n\n[[hub]]\n");
            using (var cancel = new CancelToken())
            {
                host.Session.ScanInitial(cancel);
            }
            host.ActivateLeaf();
            host.OpenNote("left.md");
            host.Leaf.SetDepth(2);
            host.Settle();
            (Window window, ConnectionsLeafView view) = Show(host.Leaf);
            try
            {
                ConnectionsRowViewModel shared = view.RootsForTests[1].Children.Single(r => r.Row!.Path == "shared.md");
                Assert.NotEmpty(shared.Children);
                shared.IsExpanded = false;
                string sharedId = shared.Id;
                Assert.False(view.ExpansionForTests[sharedId]);

                host.OpenNote("right.md");
                host.Settle();

                // The same occurrence id under the new root — and no memory.
                ConnectionsRowViewModel again = view.RootsForTests[1].Children.Single(r => r.Row!.Path == "shared.md");
                Assert.Equal(sharedId, again.Id);
                Assert.True(again.IsExpanded);
                Assert.False(view.ExpansionForTests.ContainsKey(sharedId));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ReleasingTheModelDropsTheRowsAndTheStateLabelsFollowThePresentation()
    {
        RunSta(() =>
        {
            using var host = new Host("states");
            (Window window, ConnectionsLeafView view) = Show(host.Leaf);
            try
            {
                // NoNote: T1 on the state, the anchor visible, the tree not.
                Assert.Equal(ConnectionsPhrase.NoNote, view.StateForTests.Text);
                Assert.Equal(ConnectionsPhrase.NoNote, AutomationProperties.GetName(view.AnchorForTests));
                Assert.Equal(Visibility.Visible, view.AnchorForTests.Visibility);
                Assert.Equal(Visibility.Collapsed, view.TreeForTests.Visibility);
                Assert.Equal(ConnectionsPhrase.Title, view.HeadingForTests.Text);
                Assert.False(view.DepthForTests.IsEnabled);

                // Loading: T2 visible, T3 accessible.
                host.ActivateLeaf();
                host.OpenNote(Hub);
                Assert.Equal(ConnectionsPhrase.LoadingVisible, view.StateForTests.Text);
                Assert.Equal(ConnectionsPhrase.LoadingAccessible, AutomationProperties.GetName(view.StateForTests));
                Assert.Equal("hub.md", view.HeadingForTests.Text);
                host.Settle();

                // Ready: the tree, the summary region core's render.
                Assert.Equal(Visibility.Visible, view.TreeForTests.Visibility);
                Assert.Equal(
                    SlateUniffiMethods.A11yRender(new A11yEvent.Graph(new GraphA11yEvent.GraphNeighborhoodSummary(host.Leaf.Publication.Tree!.SummaryCounts))).Text,
                    view.SummaryForTests.Text);
                Assert.True(view.DepthForTests.IsEnabled);

                // Error: T5.
                host.Workspace.OpenPath("missing-note.md", WorkspaceOpenTarget.NewTab);
                host.Settle();
                Assert.Equal(ConnectionsPhrase.Error(host.Leaf.Publication.Failure!), view.StateForTests.Text);

                // Released: no rows, no crash, the labels neutral.
                view.Model = null;
                Assert.Empty(view.RootsForTests);
                Assert.Equal(ConnectionsPhrase.NoNote, view.StateForTests.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Codex post-implementation pass 2 (IPB-6; B-9, B-13): a row
    /// the view dropped — after a root change, and after the model's
    /// release — is INERT for a cached automation peer: its activation
    /// delegate is gone, so invoking it opens nothing; its expansion
    /// handler is detached, so toggling it writes no expansion memory.</summary>
    [Fact]
    public void ADroppedRowIsInertForACachedPeer()
    {
        RunSta(() =>
        {
            using var host = new Host("dropped-row");
            (Window window, ConnectionsLeafView view) = Show(host.Leaf);
            try
            {
                host.ActivateLeaf();
                host.OpenNote(Hub);
                host.Settle();
                ConnectionsRowViewModel cached = view.RootsForTests
                    .SelectMany(group => group.Children)
                    .First(row => row.Row is { Kind: GraphNodeKind.Note, Path: not null } && row.Row.Path != Two);
                Assert.NotNull(cached.Activate);

                // The root moved: the old tree's rows were dropped.
                host.OpenNote(Two);
                host.Settle();
                Assert.Null(cached.Activate);
                cached.RaiseActivate();
                host.Settle();
                Assert.Equal(Two, host.Workspace.ActiveGroup.ActiveTab!.Path);
                Assert.Equal(Two, host.Leaf.Root);
                bool wasExpanded = cached.IsExpanded;
                cached.IsExpanded = !wasExpanded;
                // A fresh render of the same root keeps its own memory: the
                // detached row wrote none.
                host.Workspace.ConnectionsDeeperCommand.Execute(null);
                host.Settle();
                Assert.All(
                    view.RootsForTests.SelectMany(group => group.Children).Where(row => row.Row?.Id == cached.Row?.Id),
                    row => Assert.True(row.IsExpanded));

                // The model's release drops the live rows the same way.
                ConnectionsRowViewModel live = view.RootsForTests.SelectMany(group => group.Children).First(row => row.Row is not null);
                Assert.NotNull(live.Activate);
                view.Model = null;
                Assert.Null(live.Activate);
                live.RaiseActivate();
                Assert.Equal(Two, host.Workspace.ActiveGroup.ActiveTab!.Path);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void TheEmptyNeighbourhoodShowsT4UnderTheSummary()
    {
        RunSta(() =>
        {
            using var host = new Host("empty");
            File.WriteAllText(Path.Combine(host.Root, "lonely.md"), "# Lonely\n");
            using (var cancel = new CancelToken())
            {
                host.Session.ScanInitial(cancel);
            }
            host.ActivateLeaf();
            host.OpenNote("lonely.md");
            host.Settle();
            (Window window, ConnectionsLeafView view) = Show(host.Leaf);
            try
            {
                Assert.Equal(ConnectionsPhrase.Empty, view.StateForTests.Text);
                Assert.Equal(Visibility.Visible, view.SummaryForTests.Visibility);
                Assert.Equal(Visibility.Collapsed, view.TreeForTests.Visibility);
                Assert.True(view.FocusAnchor());
                Assert.True(view.AnchorForTests.IsKeyboardFocused);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void TheDepthControlIsNamedHintedAndMapsTagsToCoresDepth()
    {
        RunSta(() =>
        {
            using var host = new Host("depth");
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            (Window window, ConnectionsLeafView view) = Show(host.Leaf);
            try
            {
                ComboBox depth = view.DepthForTests;
                Assert.Equal(ConnectionsPhrase.DepthName, AutomationProperties.GetName(depth));
                Assert.Equal(ConnectionsPhrase.DepthHint, AutomationProperties.GetHelpText(depth));
                Assert.Equal("ConnectionsDepth", AutomationProperties.GetAutomationId(depth));
                Assert.Equal(ConnectionsPhrase.DepthTags, depth.Items.Cast<string>());
                Assert.Equal("Links", depth.SelectedItem);

                depth.SelectedItem = "2 links away";
                Assert.Equal(2u, host.Leaf.Depth);
                host.Settle();
                Assert.Equal(2u, host.Leaf.Publication.Tree!.Depth);

                // The model's own change (the palette's Deeper) follows back.
                host.Workspace.ConnectionsDeeperCommand.Execute(null);
                host.Settle();
                Assert.Equal("3 links away", depth.SelectedItem);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void TheRowMenuIsCoresInventoryWithShowConnectionsDisabledAndItsReasonOnTheAction()
    {
        RunSta(() =>
        {
            using var host = new Host("menu");
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            (Window window, ConnectionsLeafView view) = Show(host.Leaf);
            try
            {
                ConnectionsRowViewModel note = view.RootsForTests[1].Children.First(r => r.Row!.Kind == GraphNodeKind.Note);
                ContextMenu menu = view.BuildRowMenu(note);
                string[] titles = [.. menu.Items.Cast<MenuItem>().Select(i => (string)i.Header)];
                Assert.Equal(host.Leaf.ActionSpecs(GraphNodeKind.Note).Select(s => s.Title), titles);
                MenuItem show = menu.Items.Cast<MenuItem>().Single(i => (string)i.Header == host.Leaf.ActionSpecs(GraphNodeKind.Note).Single(s => s.Action == GraphRowAction.ShowConnections).Title);
                Assert.False(show.IsEnabled);
                Assert.Equal(ConnectionsPhrase.ShowConnectionsUnavailable, AutomationProperties.GetHelpText(show));
                // The ROW's hint is its activation's, never the action's reason (B-9).
                Assert.Equal(ConnectionsPhrase.NoteHint, note.Hint);
                MenuItem open = menu.Items.Cast<MenuItem>().First();
                Assert.True(open.IsEnabled);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
