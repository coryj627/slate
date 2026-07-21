// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class W1WorkspaceRedTeamTests
{
    [Fact]
    public void CurrentTab_ReplacesCleanTabButDefaultDecisionPreservesDirtyContent()
    {
        using FixtureVault fixture = FixtureVault.Create(3, "w1-current-tab");
        using VaultSession session = OpenScannedSession(fixture.Root);
        using var workspace = NewWorkspace(session, fixture.Root);

        workspace.OpenPath("note0.md");
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Guid originalId = tab.Id;

        workspace.OpenPath("note1.md");
        Assert.Single(workspace.ActiveGroup.Tabs);
        Assert.Equal(originalId, workspace.ActiveGroup.ActiveTab!.Id);
        Assert.Equal("note1.md", workspace.ActiveGroup.ActiveTab.Path);

        tab.Text += "\nUnsaved content that must survive.";
        string dirtyText = tab.Text;
        workspace.OpenPath("note2.md");

        Assert.Equal("note1.md", tab.Path);
        Assert.Equal(dirtyText, tab.Text);
        Assert.True(tab.IsDirty);
    }

    [Fact]
    public void CurrentTab_UsesExplicitDirtyNavigationDecision()
    {
        AssertDirtyNavigationDecision(WorkspaceDirtyNavigationDecision.Discard);
        AssertDirtyNavigationDecision(WorkspaceDirtyNavigationDecision.Save);
    }

    [Fact]
    public void DirtyTabCloseDefaultsToCancel_AndHonorsDiscardDecision()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "w1-dirty-close");
        using VaultSession session = OpenScannedSession(fixture.Root);
        using (WorkspaceViewModel preserving = NewWorkspace(session, fixture.Root))
        {
            preserving.OpenPath("note0.md");
            WorkspaceTabViewModel tab = preserving.ActiveGroup.ActiveTab!;
            tab.Text += "\nUnsaved.";
            preserving.CloseActiveTabCommand.Execute(null);
            Assert.Same(tab, preserving.ActiveGroup.ActiveTab);
            Assert.True(tab.IsDirty);
        }

        using WorkspaceViewModel discarding = NewWorkspace(
            session,
            fixture.Root,
            dirtyCloseDecision: _ => WorkspaceDirtyNavigationDecision.Discard);
        discarding.OpenPath("note0.md");
        WorkspaceTabViewModel discardTab = discarding.ActiveGroup.ActiveTab!;
        discardTab.Text += "\nDiscard me.";
        discarding.CloseActiveTabCommand.Execute(null);
        Assert.Empty(discarding.ActiveGroup.Tabs);
    }

    private static void AssertDirtyNavigationDecision(WorkspaceDirtyNavigationDecision decision)
    {
        using FixtureVault fixture = FixtureVault.Create(2, $"w1-dirty-{decision}");
        using VaultSession session = OpenScannedSession(fixture.Root);
        WorkspaceTabViewModel? decidedTab = null;
        WorkspaceItemState? decidedItem = null;
        using var workspace = NewWorkspace(
            session,
            fixture.Root,
            (tab, item) =>
            {
                decidedTab = tab;
                decidedItem = item;
                return decision;
            });

        workspace.OpenPath("note0.md");
        WorkspaceTabViewModel original = Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        original.Text += "\nDecision boundary edit.";
        workspace.OpenPath("note1.md");

        Assert.Same(original, decidedTab);
        Assert.Equal("note1.md", decidedItem!.Path);
        Assert.Same(original, workspace.ActiveGroup.ActiveTab);
        Assert.Equal("note1.md", original.Path);
        Assert.False(original.IsDirty);
        if (decision == WorkspaceDirtyNavigationDecision.Save)
        {
            Assert.Contains("Decision boundary edit.", File.ReadAllText(Path.Combine(fixture.Root, "note0.md")));
        }
    }

    [Fact]
    public void NewTabDeduplicatesWithinGroup_WhileExplicitDuplicateStillDuplicatesExceptGraph()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "w1-new-tab");
        using VaultSession session = OpenScannedSession(fixture.Root);
        using var workspace = NewWorkspace(session, fixture.Root);

        workspace.OpenPath("note0.md");
        workspace.OpenPath("note1.md", WorkspaceOpenTarget.NewTab);
        workspace.OpenPath("note1.md", WorkspaceOpenTarget.NewTab);
        Assert.Equal(2, workspace.ActiveGroup.Tabs.Count);

        workspace.DuplicateTabCommand.Execute(null);
        Assert.Equal(3, workspace.ActiveGroup.Tabs.Count);
        Assert.Equal(2, workspace.ActiveGroup.Tabs.Count(tab => tab.Path == "note1.md"));

        workspace.OpenGraph();
        workspace.OpenGraph();
        Assert.Single(workspace.Groups.SelectMany(group => group.Tabs)
, tab => tab.Item.Kind == WorkspaceItemKind.Graph);

        int tabsBeforeGraphDuplicate = workspace.Groups.Sum(group => group.Tabs.Count);
        int groupsBeforeGraphSplit = workspace.Groups.Count;
        workspace.DuplicateTabCommand.Execute(null);
        workspace.SplitRightCommand.Execute(null);
        Assert.Equal(tabsBeforeGraphDuplicate, workspace.Groups.Sum(group => group.Tabs.Count));
        Assert.Equal(groupsBeforeGraphSplit, workspace.Groups.Count);
    }

    [Fact]
    public void DuplicateAndSplitTabsMirrorTheLiveSamePathDocumentState()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "w1-shared-buffer");
        using VaultSession session = OpenScannedSession(fixture.Root);
        using var workspace = NewWorkspace(session, fixture.Root);
        workspace.OpenPath("note0.md");
        WorkspaceTabViewModel original = workspace.ActiveGroup.ActiveTab!;
        original.Text += "\nUnsaved before duplicate.";

        workspace.DuplicateTabCommand.Execute(null);
        WorkspaceTabViewModel duplicate = workspace.ActiveGroup.ActiveTab!;
        Assert.NotSame(original, duplicate);
        Assert.Equal(original.Text, duplicate.Text);
        Assert.True(duplicate.IsDirty);

        duplicate.Text += "\nEdit through duplicate.";
        Assert.Equal(duplicate.Text, original.Text);
        Assert.True(original.IsDirty);

        workspace.SplitRightCommand.Execute(null);
        WorkspaceTabViewModel split = workspace.ActiveGroup.ActiveTab!;
        Assert.Equal(duplicate.Text, split.Text);
        split.Text += "\nEdit through split.";
        Assert.Equal(split.Text, original.Text);
        Assert.Equal(split.Text, duplicate.Text);

        Assert.True(split.Save());
        Assert.All(workspace.Groups.SelectMany(group => group.Tabs), tab => Assert.False(tab.IsDirty));
        Assert.Contains("Edit through split.", File.ReadAllText(Path.Combine(fixture.Root, "note0.md")));
    }

    [Fact]
    public void CurrentTabSelectsExistingTarget_AndPathIdentityIsCaseSensitive()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "w1-current-dedupe");
        using VaultSession session = OpenScannedSession(fixture.Root);
        using var workspace = NewWorkspace(session, fixture.Root);
        workspace.OpenPath("note0.md");
        WorkspaceTabViewModel note0 = workspace.ActiveGroup.ActiveTab!;
        workspace.OpenPath("note1.md", WorkspaceOpenTarget.NewTab);
        WorkspaceTabViewModel note1 = workspace.ActiveGroup.ActiveTab!;

        workspace.OpenPath("note0.md", WorkspaceOpenTarget.CurrentTab);
        Assert.Equal(2, workspace.ActiveGroup.Tabs.Count);
        Assert.Same(note0, workspace.ActiveGroup.ActiveTab);
        Assert.Contains(note1, workspace.ActiveGroup.Tabs);

        workspace.OpenPath("Case.md", WorkspaceOpenTarget.NewTab);
        WorkspaceTabViewModel upper = workspace.ActiveGroup.ActiveTab!;
        workspace.OpenPath("case.md", WorkspaceOpenTarget.NewTab);
        WorkspaceTabViewModel lower = workspace.ActiveGroup.ActiveTab!;
        Assert.NotSame(upper, lower);
        Assert.Equal(4, workspace.ActiveGroup.Tabs.Count);

        workspace.RetargetPath("Case.md", "Moved.md");
        Assert.Equal("Moved.md", upper.Path);
        Assert.Equal("case.md", lower.Path);
        workspace.InvalidatePath("case.md");
        Assert.False(upper.IsMissingFromDisk);
        Assert.True(lower.IsMissingFromDisk);
    }

    [Fact]
    public void SplitsStopAtSix_AndRemovingOneOfManySiblingsPreservesTheOthers()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "w1-pane-cap");
        using VaultSession session = OpenScannedSession(fixture.Root);
        using var workspace = NewWorkspace(session, fixture.Root);
        workspace.OpenPath("note0.md");

        for (int index = 1; index < WorkspacePersistence.MaxGroups; index++)
        {
            workspace.SplitRightCommand.Execute(null);
        }

        Assert.Equal(WorkspacePersistence.MaxGroups, workspace.Groups.Count);
        Assert.Equal(WorkspacePersistence.MaxGroups, workspace.Root.Children.Count);
        workspace.SplitRightCommand.Execute(null);
        Assert.Equal(WorkspacePersistence.MaxGroups, workspace.Groups.Count);

        workspace.ClosePaneCommand.Execute(null);

        Assert.Equal(WorkspacePersistence.MaxGroups - 1, workspace.Groups.Count);
        Assert.True(workspace.Root.IsSplit);
        Assert.Equal(WorkspacePersistence.MaxGroups - 1, workspace.Root.Children.Count);
        Assert.All(workspace.Root.Children, child =>
            Assert.Equal(1d / (WorkspacePersistence.MaxGroups - 1), child.Weight, precision: 6));
    }

    [Fact]
    public void ClosingOneOfManySiblingsPreservesSurvivorWeightRatios()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "w1-pane-ratios");
        using VaultSession session = OpenScannedSession(fixture.Root);
        using var workspace = NewWorkspace(session, fixture.Root);
        workspace.OpenPath("note0.md");
        workspace.SplitRightCommand.Execute(null);
        workspace.SplitRightCommand.Execute(null);
        Assert.Equal(3, workspace.Root.Children.Count);
        workspace.Root.Children[0].Weight = 0.2;
        workspace.Root.Children[1].Weight = 0.3;
        workspace.Root.Children[2].Weight = 0.5;

        workspace.ClosePaneCommand.Execute(null);

        Assert.Equal(2, workspace.Root.Children.Count);
        Assert.Equal(0.4, workspace.Root.Children[0].Weight, precision: 6);
        Assert.Equal(0.6, workspace.Root.Children[1].Weight, precision: 6);
    }

    [Fact]
    public void DirectionalFocusUsesGeometryAndAnnouncesVerticalTerminalMoves()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "w1-focus-geometry");
        using VaultSession session = OpenScannedSession(fixture.Root);
        var announcements = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            session,
            fixture.Root,
            () => [],
            announcements.Add);
        workspace.OpenPath("note0.md");
        WorkspaceGroupViewModel topLeft = workspace.ActiveGroup;
        workspace.SplitRightCommand.Execute(null);
        WorkspaceGroupViewModel right = workspace.ActiveGroup;
        Assert.True(workspace.FocusDirectionalPane("horizontal", -1));
        workspace.SplitDownCommand.Execute(null);
        WorkspaceGroupViewModel bottomLeft = workspace.ActiveGroup;
        Assert.NotSame(topLeft, bottomLeft);

        Assert.True(workspace.FocusDirectionalPane("horizontal", 1));
        Assert.Same(right, workspace.ActiveGroup);
        Assert.True(workspace.FocusDirectionalPane("horizontal", -1));
        Assert.Same(topLeft, workspace.ActiveGroup);

        announcements.Clear();
        Assert.False(workspace.FocusDirectionalPane("vertical", -1));
        Assert.Contains(announcements, item => item is A11yEvent.HostComposed);
    }

    [Fact]
    public void RenameMoveAndDeleteReconcileFolderDescendantsWithoutLosingDirtyBuffers()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "w1-reconcile");
        string folder = Path.Combine(fixture.Root, "Folder");
        Directory.CreateDirectory(folder);
        File.Move(Path.Combine(fixture.Root, "note0.md"), Path.Combine(folder, "note0.md"));
        File.Move(Path.Combine(fixture.Root, "note1.md"), Path.Combine(folder, "note1.md"));

        using VaultSession session = OpenScannedSession(fixture.Root);
        var announcements = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            session,
            fixture.Root,
            () => [],
            announcements.Add);
        workspace.OpenPath("Folder/note0.md");
        WorkspaceTabViewModel dirty = Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        dirty.Text += "\nPreserve this editor buffer.";
        string dirtyText = dirty.Text;

        workspace.OpenPath("Folder/note1.md", WorkspaceOpenTarget.NewTab);
        WorkspaceTabViewModel closed = Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        workspace.CloseTabCommand.Execute(closed);
        Directory.Move(folder, Path.Combine(fixture.Root, "Renamed"));

        workspace.RetargetPath("Folder", "Renamed");
        Assert.Equal("Renamed/note0.md", dirty.Path);
        Assert.Equal(dirtyText, dirty.Text);
        Assert.True(dirty.IsDirty);

        workspace.ReopenClosedTabCommand.Execute(null);
        Assert.Equal("Renamed/note1.md", workspace.ActiveGroup.ActiveTab!.Path);
        workspace.InvalidatePath("Renamed");

        Assert.All(workspace.ActiveGroup.Tabs, tab => Assert.True(tab.IsMissingFromDisk));
        Assert.Equal(dirtyText, dirty.Text);
        Assert.True(dirty.IsDirty);
        Assert.Contains(announcements, item => item is A11yEvent.HostComposed);
        WorkspaceSnapshot persisted = Assert.IsType<WorkspaceSnapshot>(
            new WorkspacePersistence(fixture.Root).Load());
        Assert.Contains(Tabs(persisted.Root), tab => tab.Item.Path == "Renamed/note0.md");
    }

    [Fact]
    public void PaneNavigationRequestsRealFocus_AndKeyboardFocusCanSelectTheActiveGroup()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "w1-focus");
        using VaultSession session = OpenScannedSession(fixture.Root);
        using var workspace = NewWorkspace(session, fixture.Root);
        workspace.OpenPath("note0.md");
        workspace.SplitRightCommand.Execute(null);
        WorkspaceGroupViewModel right = workspace.ActiveGroup;
        WorkspaceGroupViewModel left = workspace.Groups[0];
        WorkspaceGroupViewModel? requested = null;
        workspace.EditorPaneFocusRequested += (_, group) => requested = group;

        Assert.True(workspace.FocusDirectionalPane("horizontal", -1));
        Assert.Same(left, workspace.ActiveGroup);
        Assert.Same(left, requested);

        workspace.SelectGroupFromKeyboardFocus(right);
        Assert.Same(right, workspace.ActiveGroup);
    }

    [Fact]
    public void RestoreRejectsMoreThanSixGroups_AndDeduplicatesGraphGlobally()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "w1-restore-bounds");
        var persistence = new WorkspacePersistence(fixture.Root);
        WorkspaceGroupState[] excessiveGroups = Enumerable.Range(0, WorkspacePersistence.MaxGroups + 1)
            .Select(_ => EmptyGroup())
            .ToArray();
        persistence.Save(new WorkspaceSnapshot(
            WorkspacePersistence.SchemaVersion,
            excessiveGroups[0].Id,
            new WorkspaceSplitState(
                "horizontal",
                Enumerable.Repeat(1d / excessiveGroups.Length, excessiveGroups.Length).ToArray(),
                excessiveGroups),
            null,
            []));
        Assert.Null(persistence.Load());

        WorkspaceItemState graph = new(WorkspaceItemKind.Graph, "graph:singleton");
        WorkspaceGroupState first = new(
            Guid.NewGuid(),
            null,
            [new WorkspaceTabState(Guid.NewGuid(), graph)]);
        WorkspaceGroupState second = new(
            Guid.NewGuid(),
            null,
            [new WorkspaceTabState(Guid.NewGuid(), graph)]);
        persistence.Save(new WorkspaceSnapshot(
            WorkspacePersistence.SchemaVersion,
            second.Id,
            new WorkspaceSplitState("horizontal", [0.5, 0.5], [first, second]),
            null,
            []));

        using VaultSession session = OpenScannedSession(fixture.Root);
        using var workspace = NewWorkspace(session, fixture.Root);
        Assert.Single(workspace.Groups.SelectMany(group => group.Tabs)
, tab => tab.Item.Kind == WorkspaceItemKind.Graph);
        WorkspaceGroupViewModel survivingGroup = Assert.Single(workspace.Groups);
        Assert.Same(survivingGroup, workspace.ActiveGroup);
        Assert.Equal(WorkspaceItemKind.Graph, workspace.ActiveGroup.ActiveTab!.Item.Kind);
    }

    [Fact]
    public void PersistenceRejectsDuplicateIdsEmptySplitGroupsSameAxisNestingAndInvalidWeights()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "w1-structural-persistence");
        var persistence = new WorkspacePersistence(fixture.Root);
        WorkspaceItemState note = new(WorkspaceItemKind.Markdown, "note0.md");

        Guid duplicateGroupId = Guid.NewGuid();
        WorkspaceGroupState duplicateGroupA = GroupWith(note, groupId: duplicateGroupId);
        WorkspaceGroupState duplicateGroupB = GroupWith(note, groupId: duplicateGroupId);
        SaveSplit(persistence, duplicateGroupA, duplicateGroupB);
        Assert.Null(persistence.Load());

        Guid duplicateTabId = Guid.NewGuid();
        WorkspaceGroupState duplicateTabA = GroupWith(note, tabId: duplicateTabId);
        WorkspaceGroupState duplicateTabB = GroupWith(note, tabId: duplicateTabId);
        SaveSplit(persistence, duplicateTabA, duplicateTabB);
        Assert.Null(persistence.Load());

        SaveSplit(persistence, EmptyGroup(), GroupWith(note));
        Assert.Null(persistence.Load());

        WorkspaceGroupState nestedA = GroupWith(note);
        WorkspaceGroupState nestedB = GroupWith(note);
        WorkspaceGroupState nestedC = GroupWith(note);
        WorkspaceSplitState inner = new("horizontal", [0.5, 0.5], [nestedA, nestedB]);
        persistence.Save(new WorkspaceSnapshot(
            WorkspacePersistence.SchemaVersion,
            nestedA.Id,
            new WorkspaceSplitState("horizontal", [0.5, 0.5], [inner, nestedC]),
            null,
            []));
        Assert.Null(persistence.Load());

        WorkspaceGroupState light = GroupWith(note);
        WorkspaceGroupState heavy = GroupWith(note);
        SaveSplit(persistence, light, heavy, weights: [0.1, 0.9]);
        Assert.Null(persistence.Load());
    }

    [Fact]
    public void OversizedPersistenceSnapshotDoesNotEscapeWorkspaceOperations()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "w1-persist-boundary");
        using VaultSession session = OpenScannedSession(fixture.Root);
        IReadOnlyList<string> oversizedExpandedState = Enumerable.Range(0, 500)
            .Select(index => new string('x', 600) + index)
            .ToArray();
        using var workspace = new WorkspaceViewModel(
            session,
            fixture.Root,
            () => oversizedExpandedState,
            _ => { });

        Exception? exception = Record.Exception(() => workspace.OpenPath("note0.md"));

        Assert.Null(exception);
        Assert.Single(workspace.ActiveGroup.Tabs);
    }

    private static WorkspaceViewModel NewWorkspace(
        VaultSession session,
        string root,
        Func<WorkspaceTabViewModel, WorkspaceItemState, WorkspaceDirtyNavigationDecision>?
            dirtyNavigationDecision = null,
        Func<WorkspaceTabViewModel, WorkspaceDirtyNavigationDecision>?
            dirtyCloseDecision = null) =>
        new(session, root, () => [], _ => { }, dirtyNavigationDecision, dirtyCloseDecision);

    private static VaultSession OpenScannedSession(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    private static WorkspaceGroupState EmptyGroup() => new(Guid.NewGuid(), null, []);

    private static WorkspaceGroupState GroupWith(
        WorkspaceItemState item,
        Guid? groupId = null,
        Guid? tabId = null)
    {
        WorkspaceTabState tab = new(tabId ?? Guid.NewGuid(), item);
        return new WorkspaceGroupState(groupId ?? Guid.NewGuid(), tab.Id, [tab]);
    }

    private static void SaveSplit(
        WorkspacePersistence persistence,
        WorkspaceGroupState first,
        WorkspaceGroupState second,
        IReadOnlyList<double>? weights = null) =>
        persistence.Save(new WorkspaceSnapshot(
            WorkspacePersistence.SchemaVersion,
            first.Id,
            new WorkspaceSplitState("horizontal", weights ?? [0.5, 0.5], [first, second]),
            null,
            []));

    private static IEnumerable<WorkspaceTabState> Tabs(WorkspaceNodeState node) =>
        node is WorkspaceGroupState group
            ? group.Tabs
            : ((WorkspaceSplitState)node).Children.SelectMany(Tabs);
}
