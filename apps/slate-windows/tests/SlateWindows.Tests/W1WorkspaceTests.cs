// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;
using System.Windows.Input;
using System.Text.Json;

namespace SlateWindows.Tests;

public sealed class W1WorkspacePersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"slate-w1-workspace-{Guid.NewGuid():N}");

    public W1WorkspacePersistenceTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".slate"));
    }

    [Theory]
    [InlineData("mac-v1.json", "horizontal", "tasksReview", 6)]
    [InlineData("windows-v1.json", "vertical", "outline", 2)]
    public void CrossPlatformFixtures_LoadAndRoundTrip(
        string fixture,
        string axis,
        string activeLeaf,
        int tabCount)
    {
        CopyFixture(fixture);
        var store = new WorkspacePersistence(_root);

        WorkspaceSnapshot snapshot = Assert.IsType<WorkspaceSnapshot>(store.Load());
        WorkspaceSplitState split = Assert.IsType<WorkspaceSplitState>(snapshot.Root);
        Assert.Equal(axis, split.Axis);
        Assert.Equal(activeLeaf, snapshot.ActiveLeaf);
        Assert.Equal(tabCount, Tabs(snapshot.Root).Count());

        store.Save(snapshot);
        WorkspaceSnapshot roundTripped = Assert.IsType<WorkspaceSnapshot>(store.Load());
        Assert.Equal(snapshot.ActiveGroup, roundTripped.ActiveGroup);
        Assert.Equal(
            Tabs(snapshot.Root).Select(tab => tab.Item.Kind),
            Tabs(roundTripped.Root).Select(tab => tab.Item.Kind));
        Assert.Equal(snapshot.ExpandedDirPaths, roundTripped.ExpandedDirPaths);
    }

    [Fact]
    public void UnknownTab_DropsOnlyThatTab_AndHostileExpandedPathsAreRejected()
    {
        CopyFixture("unknown-tab-v1.json");

        WorkspaceSnapshot snapshot = Assert.IsType<WorkspaceSnapshot>(
            new WorkspacePersistence(_root).Load());
        WorkspaceTabState tab = Assert.Single(Tabs(snapshot.Root));
        Assert.Equal("Survives.md", tab.Item.Path);
        Assert.Equal(tab.Id, Assert.IsType<WorkspaceGroupState>(snapshot.Root).ActiveTab);
        Assert.Equal(["Good"], snapshot.ExpandedDirPaths);
    }

    [Fact]
    public void OversizedOrUnknownVersion_DegradesToFreshWorkspace()
    {
        string path = Path.Combine(_root, ".slate", "workspace.json");
        File.WriteAllBytes(path, new byte[WorkspacePersistence.MaxFileBytes + 1]);
        Assert.Null(new WorkspacePersistence(_root).Load());

        File.WriteAllText(path, "{\"version\":2,\"activeGroup\":\"00000000-0000-0000-0000-000000000000\",\"root\":{}}");
        Assert.Null(new WorkspacePersistence(_root).Load());
    }

    [Fact]
    public void ExpandedDirectories_RetainTheNewestBoundedTailInOrder()
    {
        string[] paths = Enumerable.Range(0, WorkspacePersistence.MaxExpandedDirectories + 3)
            .Select(index => $"Folder-{index:D3}")
            .ToArray();

        IReadOnlyList<string> normalized = WorkspacePersistence.NormalizeExpandedPaths(paths);

        Assert.Equal(WorkspacePersistence.MaxExpandedDirectories, normalized.Count);
        Assert.Equal(paths[^WorkspacePersistence.MaxExpandedDirectories..], normalized);
    }

    private void CopyFixture(string name) => File.Copy(
        Path.Combine(RepoRoot(), "tests", "fixtures", "workspace", name),
        Path.Combine(_root, ".slate", "workspace.json"),
        overwrite: true);

    private static IEnumerable<WorkspaceTabState> Tabs(WorkspaceNodeState node)
    {
        if (node is WorkspaceGroupState group)
        {
            return group.Tabs;
        }

        return ((WorkspaceSplitState)node).Children.SelectMany(Tabs);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Cargo.toml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

public sealed class W1WorkspaceModelTests
{
    [Fact]
    public void TabsSplitsLeavesReopenAndPersistence_RoundTripThroughRealSession()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "workspace-model");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        var announcements = new List<A11yEvent>();
        var workspace = new WorkspaceViewModel(session, fixture.Root, () => [], announcements.Add);

        workspace.OpenPath("note0.md");
        Assert.Single(workspace.ActiveGroup.Tabs);
        ((ICommand)workspace.DuplicateTabCommand).Execute(null);
        Assert.Equal(2, workspace.ActiveGroup.Tabs.Count);
        ((ICommand)workspace.SplitRightCommand).Execute(null);
        Assert.Equal(2, workspace.Groups.Count);
        Assert.True(workspace.Root.IsSplit);

        workspace.ActiveLeaf = WorkspaceViewModel.Leaves.Single(leaf => leaf.Id == "syncDiagnostics");
        Assert.Equal(16, workspace.LeafOptions.Count);
        workspace.FocusPaneLeftCommand.Execute(null);
        Assert.NotNull(workspace.ActiveGroup.ActiveTab);
        workspace.SplitDownCommand.Execute(null);
        Assert.Equal(3, workspace.Groups.Count);
        workspace.FocusPaneAboveCommand.Execute(null);
        workspace.ClosePaneCommand.Execute(null);
        Assert.Equal(2, workspace.Groups.Count);
        workspace.FocusPreviousPaneCommand.Execute(null);
        WorkspaceTabViewModel active = Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        workspace.CloseTabCommand.Execute(active);
        Assert.True(workspace.ReopenClosedTabCommand.CanExecute(null));
        workspace.ReopenClosedTabCommand.Execute(null);
        Assert.Contains(announcements, item => item is A11yEvent.ReopenedFile);
        workspace.Dispose();

        WorkspaceSnapshot persisted = Assert.IsType<WorkspaceSnapshot>(
            new WorkspacePersistence(fixture.Root).Load());
        Assert.Equal("syncDiagnostics", persisted.ActiveLeaf);
        Assert.Single(Groups(persisted.Root));

        WorkspaceFocusBoundary? boundary = null;
        workspace.FocusBoundaryRequested += (_, requested) => boundary = requested;
        workspace.FocusPaneLeftCommand.Execute(null);
        Assert.Equal(WorkspaceFocusBoundary.Files, boundary);
        workspace.IsRightPaneVisible = false;
        workspace.FocusPaneRightCommand.Execute(null);
        Assert.Equal(WorkspaceFocusBoundary.RightPane, boundary);
        Assert.True(workspace.IsRightPaneVisible);
    }

    [Fact]
    public void DirtyMarkdownSave_UsesConflictTokenAndClearsDirtyState()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "workspace-save");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        using var workspace = new WorkspaceViewModel(session, fixture.Root, () => [], _ => { });
        workspace.OpenPath("note0.md");
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);

        tab.Text += "\nExact edit.\n";
        Assert.True(tab.IsDirty);
        Assert.True(tab.Save());
        Assert.False(tab.IsDirty);
        Assert.EndsWith("Exact edit.\n", File.ReadAllText(Path.Combine(fixture.Root, "note0.md")));
    }

    [Fact]
    public void EditorAutomationName_IncludesExtensionAndTracksCurrentTabReplacement()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "workspace-editor-a11y-name");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        using var workspace = new WorkspaceViewModel(session, fixture.Root, () => [], _ => { });
        workspace.OpenPath("note0.md");
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        var changed = new List<string?>();
        tab.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        Assert.Equal("note0.md editor", tab.EditorAutomationName);
        workspace.OpenPath("note1.md");

        Assert.Same(tab, workspace.ActiveGroup.ActiveTab);
        Assert.Equal("note1.md editor", tab.EditorAutomationName);
        Assert.Contains(nameof(WorkspaceTabViewModel.EditorAutomationName), changed);
    }

    private static IEnumerable<WorkspaceGroupState> Groups(WorkspaceNodeState node) =>
        node is WorkspaceGroupState group
            ? [group]
            : ((WorkspaceSplitState)node).Children.SelectMany(Groups);
}

public sealed class W1SidebarAndRecentsTests
{
    [Fact]
    public void DateWindows_HonorDstAndLiteralDates()
    {
        TimeZoneInfo eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        SidebarFilterDateWindow spring = Assert.Single(FilesSidebarViewModel.BuildDateWindows(
            ["@today"],
            new DateTimeOffset(2026, 3, 8, 12, 0, 0, TimeSpan.FromHours(-4)),
            eastern));
        Assert.Equal((long)TimeSpan.FromHours(23).TotalMilliseconds, spring.EndMs - spring.StartMs);

        SidebarFilterDateWindow fall = Assert.Single(FilesSidebarViewModel.BuildDateWindows(
            ["@today"],
            new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.FromHours(-5)),
            eastern));
        Assert.Equal((long)TimeSpan.FromHours(25).TotalMilliseconds, fall.EndMs - fall.StartMs);

        SidebarFilterDateWindow literal = Assert.Single(FilesSidebarViewModel.BuildDateWindows(
            ["@2026-07-20"],
            null,
            eastern));
        Assert.True(literal.EndMs > literal.StartMs);
    }

    [Fact]
    public void FileRecents_AreBoundedDedupedAndDeviceLocal()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents");
        string deviceRoot = Path.Combine(fixture.Root, "device-state");
        var store = new FileRecentsStore(fixture.Root, localAppDataRoot: deviceRoot);
        for (int index = 0; index < 60; index++)
        {
            store.Add($"note{index}.md");
        }
        store.Add("NOTE42.md");

        IReadOnlyList<string> recents = store.Load();
        Assert.Equal(FileRecentsStore.MaxEntries, recents.Count);
        Assert.Equal("NOTE42.md", recents[0]);
        Assert.Equal(recents.Count, recents.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.False(File.Exists(Path.Combine(fixture.Root, ".slate", "file-recents.json")));
    }

    [Fact]
    public void FileRecents_LegacyCleanupToleratesAProtectedOrNonFilePath()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "legacy-recents-cleanup");
        string legacyPath = Path.Combine(fixture.Root, ".slate", "file-recents.json");
        Directory.CreateDirectory(legacyPath);
        var store = new FileRecentsStore(
            fixture.Root,
            localAppDataRoot: Path.Combine(fixture.Root, "device-state"));

        Assert.Empty(store.Load());
        Assert.True(Directory.Exists(legacyPath));
    }

    [Fact]
    public async Task Sidebar_UsesCoreForFilterBatchTagsExclusiveCreateAndFolderNotes()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "sidebar");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        var announcements = new List<A11yEvent>();
        var sidebar = new FilesSidebarViewModel(
            session,
            announcements.Add,
            vaultRoot: fixture.Root,
            localAppDataRoot: Path.Combine(Path.GetTempPath(), $"slate-sidebar-recents-{Guid.NewGuid():N}"));

        FileTreeNodeViewModel note = sidebar.RootNodes.Single(node => node.Path == "note0.md");
        note.IsBatchSelected = true;
        sidebar.TagInput = "windows";
        sidebar.AddTagCommand.Execute(null);
        Assert.Contains(session.TagsForFiles(["note0.md"]), tag => tag.Tag == "windows");

        sidebar.FilterText = "note0";
        await sidebar.FilterCompletion;
        Assert.Contains(sidebar.FilterResults, row => row.Path == "note0.md");
        sidebar.FilterText = string.Empty;

        sidebar.MutationName = "Created";
        sidebar.CreateNoteCommand.Execute(null);
        string created = Path.Combine(fixture.Root, "Created.md");
        Assert.True(File.Exists(created));
        File.WriteAllText(created, "must survive");
        sidebar.CreateNoteCommand.Execute(null);
        Assert.Equal("must survive", File.ReadAllText(created));

        session.CreateFolderExclusive("Folder");
        sidebar.Refresh();
        sidebar.SelectedNode = sidebar.RootNodes.Single(node => node.Path == "Folder");
        sidebar.CreateFolderNoteCommand.Execute(null);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "Folder", "Folder.md")));
        Assert.Equal("# Folder\n", File.ReadAllText(Path.Combine(fixture.Root, "Folder", "Folder.md")));
        Assert.Contains(announcements, item => item is A11yEvent.ItemsSelected);
    }
}

public sealed class W1SidebarCompletionTests : IDisposable
{
    private readonly string _scratch = Path.Combine(
        Path.GetTempPath(),
        $"slate-w1-completion-{Guid.NewGuid():N}");

    public W1SidebarCompletionTests() => Directory.CreateDirectory(_scratch);

    [Fact]
    public void SidebarSettings_PreserveUnknownAndReservedData_AndFailClosedForward()
    {
        string vault = Path.Combine(_scratch, "prefs-vault");
        Directory.CreateDirectory(Path.Combine(vault, ".slate"));
        string path = Path.Combine(vault, ".slate", "sidebar.json");
        File.WriteAllText(path, """
            {
              "version": 1,
              "futureSibling": { "kept": true },
              "sort": { "field": "created", "direction": "desc", "future": 7 },
              "grouping": "dateBuckets",
              "pins": { "": ["one.md"] },
              "shortcuts": [
                { "kind": "tag", "path": "research", "future": "kept" },
                { "kind": "file", "path": "one.md", "future": "kept" }
              ]
            }
            """);

        var store = new SidebarSettingsStore(vault);
        SidebarSettingsSnapshot loaded = store.Load();
        Assert.Equal(SidebarSortMode.CreatedNewest, loaded.SortMode);
        Assert.True(loaded.GroupByDate);
        Assert.Contains("one.md", loaded.Pins);
        Assert.Single(loaded.Shortcuts);

        store.SetOrganization(SidebarSortMode.ModifiedOldest, groupByDate: false);
        store.SetShortcuts([new SidebarShortcutState("folder", "Projects")]);
        using (JsonDocument saved = JsonDocument.Parse(File.ReadAllBytes(path)))
        {
            JsonElement root = saved.RootElement;
            Assert.True(root.GetProperty("futureSibling").GetProperty("kept").GetBoolean());
            JsonElement[] shortcuts = root.GetProperty("shortcuts").EnumerateArray().ToArray();
            Assert.Contains(shortcuts, item => item.GetProperty("kind").GetString() == "tag"
                && item.GetProperty("future").GetString() == "kept");
            Assert.Contains(shortcuts, item => item.GetProperty("kind").GetString() == "folder"
                && item.GetProperty("path").GetString() == "Projects");
        }

        File.WriteAllText(path, "{\"version\":99,\"future\":true}");
        SidebarSettingsSnapshot forward = store.Load();
        Assert.NotNull(forward.ReadOnlyReason);
        Assert.Throws<InvalidOperationException>(() =>
            store.SetOrganization(SidebarSortMode.NameAscending, groupByDate: false));
        Assert.Contains("\"version\":99", File.ReadAllText(path));
    }

    [Fact]
    public void Sidebar_GroupsMovesAndTransformsPersistedPinsAndShortcuts()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "sidebar-move");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        var sidebar = new FilesSidebarViewModel(
            session,
            _ => { },
            vaultRoot: fixture.Root,
            localAppDataRoot: Path.Combine(_scratch, "recents"));
        FileTreeNodeViewModel note = sidebar.RootNodes.Single(node => node.Path == "note0.md");
        sidebar.SelectedNode = note;
        sidebar.PinCommand.Execute(null);
        sidebar.AddShortcutCommand.Execute(null);

        session.CreateFolderExclusive("Destination");
        sidebar.Refresh();
        note = sidebar.RootNodes.Single(node => node.Path == "note0.md");
        note.IsBatchSelected = true;
        sidebar.MoveDestination = "Destination";
        sidebar.BatchMoveCommand.Execute(null);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "Destination", "note0.md")));

        SidebarSettingsSnapshot persisted = new SidebarSettingsStore(fixture.Root).Load();
        Assert.Contains("Destination/note0.md", persisted.Pins);
        Assert.Contains(persisted.Shortcuts, item => item.Path == "Destination/note0.md");

        sidebar.GroupByDate = true;
        Assert.Contains(sidebar.RootNodes, node => node.IsGroupHeader && node.Children.Count > 0);
        Assert.Equal(SidebarSortMode.ModifiedNewest, sidebar.SortMode);
    }

    [Fact]
    public void Sidebar_DeleteRequiresConfirmation_AndImportUsesExclusiveCollisionNames()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "sidebar-import");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        string source = Path.Combine(_scratch, "note0.md");
        File.WriteAllText(source, "imported bytes");
        var sidebar = new FilesSidebarViewModel(
            session,
            _ => { },
            vaultRoot: fixture.Root,
            confirmDestructive: _ => false,
            pickImportSources: () => Task.FromResult<IReadOnlyList<string>>([source]),
            localAppDataRoot: Path.Combine(_scratch, "import-recents"));

        sidebar.SelectedNode = sidebar.RootNodes.Single(node => node.Path == "note0.md");
        sidebar.DeleteCommand.Execute(null);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "note0.md")));

        sidebar.SelectedNode = null;
        sidebar.ImportCommand.Execute(null);
        Assert.True(SpinWait.SpinUntil(
            () => !sidebar.IsImporting && File.Exists(Path.Combine(fixture.Root, "note0 2.md")),
            TimeSpan.FromSeconds(5)));
        Assert.Equal("imported bytes", File.ReadAllText(Path.Combine(fixture.Root, "note0 2.md")));
        Assert.NotEqual("imported bytes", File.ReadAllText(Path.Combine(fixture.Root, "note0.md")));
    }

    [Fact]
    public void Sidebar_ImportSourceLimitReportsEveryOmittedItem()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "sidebar-import-limit");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        string[] sources = Enumerable.Range(0, 257)
            .Select(index => Path.Combine(_scratch, $"missing-{index}.md"))
            .ToArray();
        var sidebar = new FilesSidebarViewModel(
            session,
            _ => { },
            vaultRoot: fixture.Root,
            pickImportSources: () => Task.FromResult<IReadOnlyList<string>>(sources),
            localAppDataRoot: Path.Combine(_scratch, "import-limit-recents"));

        sidebar.ImportCommand.Execute(null);

        Assert.True(SpinWait.SpinUntil(
            () => !sidebar.IsImporting,
            TimeSpan.FromSeconds(5)));
        Assert.Contains("257 items were not imported", sidebar.Status, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

public sealed class W1QuickSwitcherAndChordTests
{
    [Fact]
    public void QuickSwitcher_UsesCoreRankingTypedCountsTargetsWrapAndIncrementalChanges()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "quick-switcher");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        var announcements = new List<A11yEvent>();
        var quick = new QuickSwitcherViewModel(
            session,
            fixture.Root,
            announcements.Add,
            [new SwitcherFile("alpha.md", "alpha.md"), new SwitcherFile("beta.md", "beta.md")],
            Path.Combine(Path.GetTempPath(), $"slate-quick-recents-{Guid.NewGuid():N}"),
            debounceRanking: false);

        quick.Open();
        Assert.Equal(2, quick.TotalResults);
        Assert.IsType<A11yEvent.QuickSwitcherCount>(announcements[^1]);
        quick.Query = "alp";
        Assert.Equal("alpha.md", Assert.Single(quick.Results).Path);
        quick.MoveSelection(-1);
        Assert.Equal("alpha.md", quick.SelectedRow?.Path);

        (string Path, WorkspaceOpenTarget Target)? opened = null;
        quick.OpenRequested += (_, request) => opened = request;
        quick.OpenSelected(WorkspaceOpenTarget.SplitDown);
        Assert.Equal(("alpha.md", WorkspaceOpenTarget.SplitDown), opened);
        Assert.False(quick.IsOpen);
        Assert.Empty(quick.Results);
        Assert.Null(quick.SelectedRow);

        quick.ApplyFileChange(new FileChangeEvent(FileChangeKind.Created, "gamma.md", null));
        quick.Open();
        Assert.Contains(quick.Results, item => item.Path == "gamma.md");
    }

    [Fact]
    public void ChordTable_HasEveryW1NamedChord_AndNoActiveCollision()
    {
        string path = Path.Combine(RepoRoot(), "apps", "slate-windows", "chords.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement[] commands = document.RootElement.GetProperty("commands").EnumerateArray().ToArray();
        string[] required =
        [
            "slate.vault.open", "slate.workspace.quickOpen", "slate.workspace.newTab",
            "slate.workspace.closeTab", "slate.workspace.reopenClosedTab",
            "slate.workspace.splitRight", "slate.workspace.splitDown",
            "slate.workspace.focusPaneLeft", "slate.workspace.focusPaneRight",
            "slate.workspace.focusPaneAbove", "slate.workspace.focusPaneBelow",
            "slate.workspace.moveTabLeft", "slate.workspace.moveTabRight",
            "slate.workspace.growPane", "slate.workspace.shrinkPane",
            "slate.view.toggleRightPane", "slate.sidebar.focusFilter",
            "slate.sidebar.historyBack", "slate.sidebar.historyForward",
            "slate.file.cancelImport",
        ];
        Assert.All(required, id => Assert.Contains(commands, command => command.GetProperty("id").GetString() == id));

        string[] active = commands
            .Where(command => command.GetProperty("windows").ValueKind == JsonValueKind.String)
            .Select(command => command.GetProperty("windows").GetString()!)
            .ToArray();
        Assert.Equal(active.Length, active.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Cargo.toml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
