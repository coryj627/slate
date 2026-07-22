// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using SlateWindows;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class W1HostLoggingPrivacyTests
{
    [Fact]
    public void DurableDiagnostics_OmitExceptionMessagesAndVaultPaths()
    {
        const string sentinelPath = @"C:\Vaults\Private\medical-notes.md";
        var output = new StringWriter();
        TextWriter original = Console.Error;
        try
        {
            Console.SetError(output);
            foreach (HostDiagnosticEvent diagnosticEvent in Enum.GetValues<HostDiagnosticEvent>())
            {
                HostLog.Write(
                    diagnosticEvent,
                    new IOException($"Could not access {sentinelPath}: authored detail"));
            }
        }
        finally
        {
            Console.SetError(original);
        }

        string logged = output.ToString();
        Assert.DoesNotContain(sentinelPath, logged, StringComparison.Ordinal);
        Assert.DoesNotContain("authored detail", logged, StringComparison.Ordinal);
        Assert.Contains(nameof(IOException), logged, StringComparison.Ordinal);
        foreach (HostDiagnosticEvent diagnosticEvent in Enum.GetValues<HostDiagnosticEvent>())
        {
            Assert.Contains($"SlateWindows.{diagnosticEvent}", logged, StringComparison.Ordinal);
        }
    }
}

public sealed class W1SidebarHardeningTests
{
    [Fact]
    public void DirectoryListing_DrainsPagesAndReportsTheMaterializationBound()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-pages");
        for (int index = 0; index < FilesSidebarViewModel.MaxMaterializedDirectoryItems + 1; index++)
        {
            File.WriteAllText(Path.Combine(fixture.Root, $"note-{index:D5}.md"), string.Empty);
        }

        using VaultSession session = OpenScanned(fixture.Root);
        var sidebar = CreateSidebar(session, fixture.Root);

        Assert.Equal(
            FilesSidebarViewModel.MaxMaterializedDirectoryItems,
            sidebar.RootNodes.Count(node => !node.IsPlaceholder));
        FileTreeNodeViewModel overflow = Assert.Single(
            sidebar.RootNodes,
            node => node.IsPlaceholder);
        Assert.Contains("More than 5,000 items", overflow.Name, StringComparison.Ordinal);
        Assert.Contains("Showing the first 5,000 items", sidebar.Status, StringComparison.Ordinal);

        sidebar.IsDualPaneEnabled = true;
        Assert.Equal(
            FilesSidebarViewModel.MaxMaterializedDirectoryItems,
            sidebar.DualPaneFiles.Count(node => !node.IsPlaceholder));
        Assert.Single(sidebar.DualPaneFiles, node => node.IsPlaceholder);
    }

    [Fact]
    public void MixedDirectoryAndFinalFilePage_ReportsFilesOmittedInsideThePage()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-mixed-overflow");
        const int files = 100;
        int directories = FilesSidebarViewModel.MaxMaterializedDirectoryItems - (files / 2);
        for (int index = 0; index < directories; index++)
        {
            Directory.CreateDirectory(Path.Combine(fixture.Root, $"folder-{index:D5}"));
        }

        for (int index = 0; index < files; index++)
        {
            File.WriteAllText(Path.Combine(fixture.Root, $"note-{index:D3}.md"), string.Empty);
        }

        using VaultSession session = OpenScanned(fixture.Root);
        var sidebar = CreateSidebar(session, fixture.Root);

        Assert.Equal(
            FilesSidebarViewModel.MaxMaterializedDirectoryItems,
            sidebar.RootNodes.Count(node => !node.IsPlaceholder));
        Assert.Single(sidebar.RootNodes, node => node.IsPlaceholder);
        Assert.Equal(files / 2, sidebar.RootNodes.Count(node => !node.IsDirectory && !node.IsPlaceholder));
        Assert.Contains("Showing the first 5,000 items", sidebar.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshSnapshotsLiveExpansions_AndExpandLoadedUsesAPreMaterializedSnapshot()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-expansion");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "A", "B", "C"));
        File.WriteAllText(Path.Combine(fixture.Root, "A", "B", "C", "note.md"), "# Note\n");

        using VaultSession session = OpenScanned(fixture.Root);
        var sidebar = CreateSidebar(session, fixture.Root);
        FileTreeNodeViewModel firstA = Assert.Single(sidebar.RootNodes, node => node.Path == "A");
        firstA.IsExpanded = true;

        sidebar.Refresh();

        FileTreeNodeViewModel restoredA = Assert.Single(sidebar.RootNodes, node => node.Path == "A");
        Assert.True(restoredA.IsExpanded);
        Assert.Contains("A", sidebar.RestoredExpandedPaths);

        var freshSidebar = CreateSidebar(session, fixture.Root);
        FileTreeNodeViewModel freshA = Assert.Single(freshSidebar.RootNodes, node => node.Path == "A");
        freshSidebar.ExpandLoadedCommand.Execute(null);
        await freshSidebar.ExpandLoadedCompletion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(freshA.IsExpanded);
        FileTreeNodeViewModel materializedB = Assert.Single(
            freshA.Children,
            node => node.Path == "A/B");
        Assert.False(materializedB.IsExpanded);
    }

    [Fact]
    public void FilterDebouncePublishesOnlyTheLatestGeneration()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "sidebar-filter-generation");
        using VaultSession session = OpenScanned(fixture.Root);
        var context = new PumpSynchronizationContext();
        SynchronizationContext? prior = SynchronizationContext.Current;
        FilesSidebarViewModel sidebar;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            sidebar = CreateSidebar(session, fixture.Root, context);
            sidebar.FilterText = "note0";
            sidebar.FilterText = "note1";
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(prior);
        }

        Assert.True(SpinWait.SpinUntil(
            () =>
            {
                context.Drain();
                return sidebar.FilterResults.Count > 0;
            },
            TimeSpan.FromSeconds(5)));
        FileTreeNodeViewModel result = Assert.Single(sidebar.FilterResults);
        Assert.Equal("note1.md", result.Path);
    }

    private static VaultSession OpenScanned(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    private static FilesSidebarViewModel CreateSidebar(
        VaultSession session,
        string root,
        SynchronizationContext? filterUiContext = null) => new(
        session,
        _ => { },
        vaultRoot: root,
        localAppDataRoot: Path.Combine(root, "device-state"),
        filterUiContext: filterUiContext);

    private sealed class PumpSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = [];

        public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

        public void Drain()
        {
            while (_queue.TryDequeue(out var work))
            {
                work.Callback(work.State);
            }
        }
    }
}

public sealed class W1SidebarSettingsHardeningTests
{
    [Fact]
    public void GroupedOldestSortDirectionsRoundTrip()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-settings-sort");
        var store = new SidebarSettingsStore(fixture.Root);
        store.Load();

        store.SetOrganization(SidebarSortMode.CreatedOldest, groupByDate: true);
        SidebarSettingsSnapshot created = new SidebarSettingsStore(fixture.Root).Load();
        Assert.True(created.GroupByDate);
        Assert.Equal(SidebarSortMode.CreatedOldest, created.SortMode);

        store.SetOrganization(SidebarSortMode.ModifiedOldest, groupByDate: true);
        SidebarSettingsSnapshot modified = new SidebarSettingsStore(fixture.Root).Load();
        Assert.True(modified.GroupByDate);
        Assert.Equal(SidebarSortMode.ModifiedOldest, modified.SortMode);
    }

    [Fact]
    public void PinAndShortcutBoundsFailWithoutSilentlyDroppingState()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-settings-bounds");
        string slateDirectory = Path.Combine(fixture.Root, ".slate");
        Directory.CreateDirectory(slateDirectory);
        string settingsPath = Path.Combine(slateDirectory, "sidebar.json");
        var reserved = Enumerable.Range(0, SidebarSettingsStore.MaxShortcuts)
            .Select(index => new { kind = "future", path = $"reserved-{index}" })
            .ToArray();
        File.WriteAllText(
            settingsPath,
            JsonSerializer.Serialize(new { version = SidebarSettingsStore.SchemaVersion, shortcuts = reserved }));
        byte[] original = File.ReadAllBytes(settingsPath);
        var store = new SidebarSettingsStore(fixture.Root);
        store.Load();

        Assert.Throws<InvalidOperationException>(() => store.SetShortcuts(
            [new SidebarShortcutState("file", "visible.md")]));
        Assert.Equal(original, File.ReadAllBytes(settingsPath));

        string[] excessivePins = Enumerable.Range(0, SidebarSettingsStore.MaxPinsPerFolder + 1)
            .Select(index => $"folder/note-{index}.md")
            .ToArray();
        Assert.Throws<InvalidOperationException>(() => store.ReplacePins(excessivePins));
        Assert.Equal(original, File.ReadAllBytes(settingsPath));

        Dictionary<string, string[]> saturatedPins = Enumerable.Range(0, 10).ToDictionary(
            folder => $"folder-{folder}",
            folder => Enumerable.Range(0, SidebarSettingsStore.MaxPinsPerFolder)
                .Select(index => $"folder-{folder}/note-{index}.md")
                .ToArray());
        File.WriteAllText(
            settingsPath,
            JsonSerializer.Serialize(new
            {
                version = SidebarSettingsStore.SchemaVersion,
                pins = saturatedPins,
            }));
        byte[] saturated = File.ReadAllBytes(settingsPath);
        var saturatedStore = new SidebarSettingsStore(fixture.Root);
        Assert.Null(saturatedStore.Load().ReadOnlyReason);
        Assert.Throws<InvalidOperationException>(() => saturatedStore.SetPinsForFolder(
            "one-too-many",
            ["one-too-many/note.md"]));
        Assert.Equal(saturated, File.ReadAllBytes(settingsPath));

        Assert.Throws<InvalidOperationException>(() => saturatedStore.SetPinsForFolder(
            new string('f', SidebarSettingsStore.MaxPathLength + 1),
            []));
        Assert.Equal(saturated, File.ReadAllBytes(settingsPath));
    }
}

public sealed class W1ImportSafeguardTests
{
    [Fact]
    public void ImportEntryBudgetStopsAtExactlyTenThousand()
    {
        int visited = 0;
        for (int index = 0; index < FilesSidebarViewModel.MaxImportEntries; index++)
        {
            Assert.True(FilesSidebarViewModel.TryReserveImportEntry(ref visited));
        }

        Assert.Equal(10_000, visited);
        Assert.False(FilesSidebarViewModel.TryReserveImportEntry(ref visited));
        Assert.Equal(10_000, visited);
    }

    [Fact]
    public void ReparseCheckWalksEveryAncestorUntilItFindsOne()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "slate-import-walk"));
        string reparseAncestor = Path.Combine(root, "linked");
        string source = Path.Combine(reparseAncestor, "nested", "note.md");
        var inspected = new List<string>();

        bool found = FilesSidebarViewModel.HasReparsePointInPath(
            source,
            path =>
            {
                inspected.Add(path);
                return string.Equals(path, reparseAncestor, StringComparison.OrdinalIgnoreCase)
                    ? FileAttributes.Directory | FileAttributes.ReparsePoint
                    : FileAttributes.Normal;
            });

        Assert.True(found);
        Assert.Equal(source, inspected[0]);
        Assert.Contains(reparseAncestor, inspected, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(root, inspected, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportSizeGuardRechecksTheBytesRead()
    {
        FilesSidebarViewModel.ValidateImportFileSize(
            FilesSidebarViewModel.MaxImportFileBytes,
            afterRead: false);
        FilesSidebarViewModel.ValidateImportFileSize(
            FilesSidebarViewModel.MaxImportFileBytes,
            afterRead: true);

        IOException beforeRead = Assert.Throws<IOException>(() =>
            FilesSidebarViewModel.ValidateImportFileSize(
                FilesSidebarViewModel.MaxImportFileBytes + 1,
                afterRead: false));
        Assert.Contains("exceeds", beforeRead.Message, StringComparison.Ordinal);

        IOException afterRead = Assert.Throws<IOException>(() =>
            FilesSidebarViewModel.ValidateImportFileSize(
                FilesSidebarViewModel.MaxImportFileBytes + 1,
                afterRead: true));
        Assert.Contains("grew beyond", afterRead.Message, StringComparison.Ordinal);
    }
}

public sealed class W1SingleInstanceHardeningTests
{
    [Fact]
    public async Task StalledConnectionTimesOutAndTheNextActivationIsAccepted()
    {
        string identity = $"slate-single-instance-deadline-{Guid.NewGuid():N}";
        using var primary = new SingleInstanceCoordinator(identity);
        using var secondary = new SingleInstanceCoordinator(identity);
        Assert.True(primary.IsPrimary);
        Assert.False(secondary.IsPrimary);
        var received = new TaskCompletionSource<string[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        primary.StartListening(arguments => received.TrySetResult(arguments));

        using var stalled = new NamedPipeClientStream(
            ".",
            primary.PipeNameForTesting,
            PipeDirection.Out,
            PipeOptions.Asynchronous);
        await stalled.ConnectAsync(5_000);
        byte[] header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, 32);
        await stalled.WriteAsync(header);
        await stalled.WriteAsync(new byte[] { (byte)'[' });
        await stalled.FlushAsync();

        await Task.Delay(SingleInstanceCoordinator.ConnectionReadTimeout + TimeSpan.FromMilliseconds(300));
        string[] expected = ["--from-test", @"C:\Vaults\Recovered"];
        Assert.True(secondary.SendActivation(expected, TimeSpan.FromSeconds(5)));
        Assert.Equal(expected, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
