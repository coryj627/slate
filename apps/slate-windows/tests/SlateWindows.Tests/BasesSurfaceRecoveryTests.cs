// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using SlateWindows.Bases;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-14 (#1397; mac's <c>BaseLoadFailedView</c>, WD8 as recommended): a base
/// that cannot show rows shows a group — the heading, core's message, the
/// repair sentence or why editing is unavailable, and Edit — on a tab surface
/// over a file-backed base. The read-only dock and a failed saved query show
/// the heading and the message alone, and a tab surface's Edit is addressed
/// to its own tab.
/// </summary>
public sealed class BasesSurfaceRecoveryTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    private readonly List<A11yEvent> _announced = [];

    public BasesSurfaceRecoveryTests()
    {
        _fixture = FixtureVault.Create(1, "bases-recovery");
        File.WriteAllText(Path.Combine(_fixture.Root, "Empty.base"), string.Empty);
        File.WriteAllText(Path.Combine(_fixture.Root, "Other.base"), string.Empty);
        File.WriteAllText(Path.Combine(_fixture.Root, "Gone.base"), OneView);
        File.WriteAllText(Path.Combine(_fixture.Root, "Notes.base"), OneView);
        File.WriteAllText(Path.Combine(_fixture.Root, "a.md"), "# A\n");
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    private const string OneView =
        "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n";

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    private WorkspaceViewModel NewWorkspace() =>
        new(_session, _fixture.Root, () => [], _announced.Add, startInteractionBackgroundWork: false);

    /// <summary>Mac's twin: every flavour of a base with nothing to show —
    /// saved empty, missing, an execute that failed before any rows — shows
    /// the group on its tab's surface, with something to say and Edit enabled
    /// exactly when the definition can be edited; never the empty-results
    /// line. A loading base shows no group. A failed saved query shows the
    /// heading and its message, with no repair sentence over an Edit it could
    /// not run.</summary>
    [Fact]
    public void NoBlankSurfaceForAFailedBase() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        WorkspaceTabViewModel empty = OpenTab(workspace, "Empty.base");
        WorkspaceTabViewModel missing = OpenTab(workspace, "Missing.base");
        BaseDocumentViewModel notes = workspace.BaseDocumentFor("Notes.base", load: false);
        notes.ExecuteFaultForTests = () => new VaultException.Db("database is locked");
        WorkspaceTabViewModel unloadable = OpenTab(workspace, "Notes.base");
        notes.Load();
        Assert.Equal(BaseLoadState.Degraded, notes.State);
        Assert.Null(notes.Result);

        foreach (WorkspaceTabViewModel tab in new[] { empty, missing, unloadable })
        {
            BaseDocumentViewModel document = Assert.IsType<BaseDocumentViewModel>(tab.Base);
            var surface = new BaseSurfaceView { Tab = tab, Model = document };
            string what = document.Path;
            Assert.True(surface.FailedGroupForTests.Visibility == Visibility.Visible, what + " shows no failed group");
            Assert.False(string.IsNullOrEmpty(FailedMessage(surface)), what);
            Assert.Equal(BasePhrase.LoadFailedHeading, FailedHeading(surface));
            Assert.Equal(Visibility.Visible, surface.FailedRepairForTests.Visibility);
            Assert.Equal(
                document.DefinitionEditingDisabledReason ?? BasePhrase.RepairSentence,
                surface.FailedRepairForTests.Text);
            Assert.Equal(Visibility.Visible, surface.FailedEditForTests.Visibility);
            Assert.Equal(document.DefinitionEditingDisabledReason is null, surface.FailedEditForTests.IsEnabled);
            Assert.Equal(Visibility.Collapsed, EmptyState(surface).Visibility);
            // The message is read once, in the group — never also as a banner.
            Assert.Equal(string.Empty, surface.StateBannerForTests.Text);
            // The group's description says why, for a reader landing on Edit.
            Assert.Equal(
                $"{FailedMessage(surface)} {surface.FailedRepairForTests.Text}",
                System.Windows.Automation.AutomationProperties.GetHelpText(surface.FailedGroupForTests));
        }

        // The surface's landing: a failed base that offers repair lands on
        // its Edit.
        var landing = new BaseSurfaceView { Tab = empty, Model = empty.Base };
        var window = new Window
        {
            Content = landing,
            Width = 640,
            Height = 480,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        try
        {
            window.Show();
            window.UpdateLayout();
            Assert.True(landing.LandInSurface());
            Assert.Same(landing.FailedEditForTests, System.Windows.Input.Keyboard.FocusedElement);
        }
        finally
        {
            window.Close();
        }

        // Loading: no group.
        var loading = new BaseDocumentViewModel(_session, "Gone.base", _ => { }, synchronousForTests: true);
        var loadingSurface = new BaseSurfaceView { Model = loading };
        Assert.Equal(BaseLoadState.Loading, loading.State);
        Assert.Equal(Visibility.Collapsed, loadingSurface.FailedGroupForTests.Visibility);

        // A failed saved query: heading and message, no repair.
        var query = BaseDocumentViewModel.ForSavedQuery(_session, "missing", "Missing", _ => { }, synchronousForTests: true);
        query.Load();
        Assert.Equal(BaseLoadState.Failed, query.State);
        var querySurface = new BaseSurfaceView { Model = query };
        Assert.Equal(Visibility.Visible, querySurface.FailedGroupForTests.Visibility);
        Assert.Equal(query.StateMessage, FailedMessage(querySurface));
        Assert.Equal(Visibility.Collapsed, querySurface.FailedRepairForTests.Visibility);
        Assert.Equal(Visibility.Collapsed, querySurface.FailedEditForTests.Visibility);
    });

    /// <summary>The read-only dock offers no repair: its document is not the
    /// active tab's and has no editing seam, so beside an editable failed base
    /// it shows the heading and its own message, with no repair sentence and
    /// no Edit — and none after a note becomes the active tab.</summary>
    [Fact]
    public void AFailedDockBesideAnEditableBaseOffersNoEdit() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        WorkspaceTabViewModel tab = OpenTab(workspace, "Empty.base");
        BaseDocumentViewModel active = Assert.IsType<BaseDocumentViewModel>(tab.Base);
        Assert.Equal(BaseLoadState.Failed, active.State);
        Assert.Null(active.DefinitionEditingDisabledReason);
        Assert.True(workspace.BasesEditViewFiltersCommand.CanExecute(null));
        workspace.DockBaseFileToSidebar("Other.base", "Other");
        BaseDocumentViewModel docked = Assert.IsType<BaseDocumentViewModel>(workspace.BasesDockDocument);
        Assert.Equal(BaseLoadState.Failed, docked.State);
        Assert.Equal(BasePhrase.NoExecutableViews, docked.StateMessage);
        Assert.Null(docked.DefinitionEditingDisabledReason);

        var dock = new BaseSurfaceView { IsReadOnlySurface = true, Model = docked };
        var tabSurface = new BaseSurfaceView { Tab = tab, Model = active };

        AssertTheDockOffersNoRepair(dock);
        Assert.Equal(
            "BaseLoadFailedGroup",
            System.Windows.Automation.AutomationProperties.GetAutomationId(tabSurface.FailedGroupForTests));
        Assert.Equal(BasePhrase.RepairSentence, tabSurface.FailedRepairForTests.Text);
        Assert.Equal(Visibility.Visible, tabSurface.FailedEditForTests.Visibility);
        Assert.True(tabSurface.FailedEditForTests.IsEnabled);

        workspace.OpenPath("a.md", WorkspaceOpenTarget.NewTab);
        Assert.False(workspace.BasesEditViewFiltersCommand.CanExecute(null));
        AssertTheDockOffersNoRepair(dock);
    });

    /// <summary>One document hosted by base tabs in two split groups: the
    /// inactive group's surface reads its OWN document's state, and its Edit
    /// makes its group and tab active before the builder opens on its own
    /// definition. Bound to the workspace command instead, it would take the
    /// active group's state and edit the active group's base.</summary>
    [Fact]
    public void AnInactiveGroupsFailedBaseEditsItsOwnDefinition() => RunSta(() =>
    {
        using (WorkspaceViewModel workspace = NewWorkspace())
        {
            WorkspaceTabViewModel first = OpenTab(workspace, "Empty.base");
            WorkspaceGroupViewModel firstGroup = workspace.ActiveGroup;
            workspace.SplitRightCommand.Execute(null);
            Assert.NotSame(firstGroup, workspace.ActiveGroup);
            workspace.OpenPath("a.md");
            Assert.Null(workspace.ActiveBaseDocument);
            Assert.False(workspace.BasesEditViewFiltersCommand.CanExecute(null));

            var surface = new BaseSurfaceView { Tab = first, Model = first.Base };
            Assert.Equal(BasePhrase.RepairSentence, surface.FailedRepairForTests.Text);
            Assert.True(surface.FailedEditForTests.IsEnabled, "the inactive group's Edit reads its own base");

            var peer = new ButtonAutomationPeer(surface.FailedEditForTests);
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
            PumpedDispatcher.Drain();

            Assert.Same(firstGroup, workspace.ActiveGroup);
            Assert.Same(first, firstGroup.ActiveTab);
            BaseQueryBuilderViewModel builder =
                Assert.IsType<BaseQueryBuilderViewModel>(workspace.BaseQueryBuilderSheet);
            Assert.Equal("Empty.base", builder.Context.ViewPath);
            Assert.Equal(0, builder.Context.ViewIndex);
        }

        using (WorkspaceViewModel workspace = NewWorkspace())
        {
            WorkspaceTabViewModel first = OpenTab(workspace, "Gone.base");
            Assert.Equal(BaseLoadState.Ready, first.Base!.State);
            workspace.SplitRightCommand.Execute(null);
            workspace.OpenPath("Empty.base");
            Assert.True(workspace.BasesEditViewFiltersCommand.CanExecute(null), "the active base is editable");
            File.Delete(Path.Combine(_fixture.Root, "Gone.base"));
            first.Base.Refresh();
            Assert.Equal(BaseLoadState.Failed, first.Base.State);
            string reason = Assert.IsType<string>(first.Base.DefinitionEditingDisabledReason);

            var surface = new BaseSurfaceView { Tab = first, Model = first.Base };
            Assert.Equal(reason, surface.FailedRepairForTests.Text);
            Assert.False(surface.FailedEditForTests.IsEnabled, "the inactive group's Edit reads its own, unavailable, base");
            Assert.True(workspace.BasesEditViewFiltersCommand.CanExecute(null));
        }
    });

    private static void AssertTheDockOffersNoRepair(BaseSurfaceView dock)
    {
        Assert.Equal(
            "BasesDockLoadFailedGroup",
            System.Windows.Automation.AutomationProperties.GetAutomationId(dock.FailedGroupForTests));
        Assert.Equal(Visibility.Visible, dock.FailedGroupForTests.Visibility);
        Assert.Equal(BasePhrase.LoadFailedHeading, FailedHeading(dock));
        Assert.Equal(BasePhrase.NoExecutableViews, FailedMessage(dock));
        Assert.Equal(Visibility.Collapsed, dock.FailedRepairForTests.Visibility);
        Assert.Equal(Visibility.Collapsed, dock.FailedEditForTests.Visibility);
    }

    private static WorkspaceTabViewModel OpenTab(WorkspaceViewModel workspace, string path)
    {
        workspace.OpenPath(path, WorkspaceOpenTarget.NewTab);
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.IsType<BaseDocumentViewModel>(tab.Base);
        return tab;
    }

    private static string FailedHeading(BaseSurfaceView surface) =>
        ((System.Windows.Controls.TextBlock)surface.FailedGroupForTests.Children[0]).Text;

    private static string FailedMessage(BaseSurfaceView surface) =>
        ((System.Windows.Controls.TextBlock)surface.FailedGroupForTests.Children[1]).Text;

    private static System.Windows.Controls.TextBlock EmptyState(BaseSurfaceView surface) =>
        surface.EmptyStateForTests;

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
