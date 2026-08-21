// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W5-4 (#744) Phase A: structural-report consumption, the hardened
/// rename, and the structural undo domain. Contracts:
/// docs/plans/31_file_management_contracts.md (F3, F9, F10).
/// </summary>
public sealed class FileManagementTests
{
    [Fact]
    public async Task ARenameConsumesTheReportRetargetsAndSpeaksTheLinksSuffix()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-rename-report");
        File.WriteAllText(
            Path.Combine(fixture.Root, "a.md"), "Points at [[b]] twice: [[b]].\n");
        File.WriteAllText(Path.Combine(fixture.Root, "b.md"), "# Target\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        var retargets = new List<(string Old, string New)>();
        SidebarRig rig = await NewSidebar(session, fixture, announced);
        rig.Sidebar.RetargetRequested =
            (oldPath, newPath) => retargets.Add((oldPath, newPath));

        FileTreeNodeViewModel target = Node(rig, "b.md");
        string displayName = target.DisplayName;
        rig.Sidebar.SelectedNode = target;
        rig.Sidebar.MutationName = "c.md";
        Assert.True(rig.Sidebar.TryRenameSelected());

        // F9: the report retargeted synchronously and the links suffix
        // spoke the DISTINCT rewritten count (a.md links twice → one
        // note). The sentence is mac's shape with the suffix replacing
        // the period.
        Assert.Contains(("b.md", "c.md"), retargets);
        string sentence = announced
            .OfType<A11yEvent.HostComposed>()
            .Select(item => SlateUniffiMethods.A11yRender(item).Text)
            .Single(text => text.StartsWith("Renamed", StringComparison.Ordinal));
        Assert.Equal(
            $"Renamed {displayName} to c.md, updated links in 1 note.", sentence);
        Assert.Contains("[[c]]", File.ReadAllText(Path.Combine(fixture.Root, "a.md")));
        Assert.True(File.Exists(Path.Combine(fixture.Root, "c.md")));
    }

    [Fact]
    public async Task AFailedRenameKeepsTheFieldStateWithCoresReason()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-rename-fail");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "A\n");
        File.WriteAllText(Path.Combine(fixture.Root, "taken.md"), "Occupied\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        rig.Sidebar.SelectedNode = Node(rig, "a.md");
        rig.Sidebar.MutationName = "taken.md";

        // F3: the refusal returns false — the WINDOW keeps focus in the
        // field on false — with MutationName untouched and core's
        // message relayed in Status; nothing on disk changed.
        Assert.False(rig.Sidebar.TryRenameSelected());
        Assert.Equal("taken.md", rig.Sidebar.MutationName);
        Assert.StartsWith(
            "Rename failed:", rig.Sidebar.Status, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "a.md")));
        Assert.Equal(
            "Occupied\n", File.ReadAllText(Path.Combine(fixture.Root, "taken.md")));
    }

    [Fact]
    public void FolderRenamesRideTheCompoundFfiUnconditionally()
    {
        // Finding 4: the host-side HasFolderNote branch was the raced
        // probe mac's review removed — presence is core's call under
        // the structural lock. Structural pin: the rename names the
        // compound FFI and no probe survives.
        string rename = CSharpSource.Normalize(
            CSharpSource.Load("FilesSidebarViewModel.FileManagement.cs")
                .Method("TryRenameSelected"));
        Assert.Contains(
            "_session.RenameFolderWithNote", rename, StringComparison.Ordinal);
        Assert.DoesNotContain("HasFolderNote", rename, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "_session.RenameFolder(", rename, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UndoRestoresARenameByteExactAndRedoReplaysIt()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-undo-rename");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "The bytes.\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        rig.Sidebar.SelectedNode = Node(rig, "a.md");
        rig.Sidebar.MutationName = "b.md";
        Assert.True(rig.Sidebar.TryRenameSelected());
        Assert.True(File.Exists(Path.Combine(fixture.Root, "b.md")));

        rig.Sidebar.UndoStructural();
        Assert.True(File.Exists(Path.Combine(fixture.Root, "a.md")));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "b.md")));
        Assert.Equal(
            "The bytes.\n", File.ReadAllText(Path.Combine(fixture.Root, "a.md")));
        Assert.Contains(
            "Undid rename to a.md.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));

        rig.Sidebar.RedoStructural();
        Assert.True(File.Exists(Path.Combine(fixture.Root, "b.md")));
        Assert.Equal(
            "The bytes.\n", File.ReadAllText(Path.Combine(fixture.Root, "b.md")));
        Assert.Contains(
            "Redid rename to b.md.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));
    }

    [Fact]
    public async Task TheEmptyStacksStillSpeak()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "fm-undo-empty");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        rig.Sidebar.UndoStructural();
        rig.Sidebar.RedoStructural();

        string[] spoken = [.. announced
            .OfType<A11yEvent.HostComposed>()
            .Select(item => SlateUniffiMethods.A11yRender(item).Text)];
        Assert.Contains("Nothing to undo.", spoken);
        Assert.Contains("Nothing to redo.", spoken);
    }

    [Fact]
    public async Task CreatesAndTrashAreHistoryBarriers()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-barriers");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "A\n");
        File.WriteAllText(Path.Combine(fixture.Root, "victim.md"), "V\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        // Arm the stack with a rename, then CREATE — the barrier must
        // clear it (mac's table: a stale inverse must never target a
        // path a barrier op now owns).
        rig.Sidebar.SelectedNode = Node(rig, "a.md");
        rig.Sidebar.MutationName = "renamed.md";
        Assert.True(rig.Sidebar.TryRenameSelected());
        rig.Sidebar.MutationName = "fresh.md";
        rig.Sidebar.CreateNoteCommand.Execute(null);
        rig.Sidebar.UndoStructural();
        Assert.Contains(
            "Nothing to undo.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));
        Assert.True(File.Exists(Path.Combine(fixture.Root, "renamed.md")));

        // Re-arm, then TRASH — same barrier.
        announced.Clear();
        await rig.Settle();
        rig.Sidebar.SelectedNode = Node(rig, "renamed.md");
        rig.Sidebar.MutationName = "again.md";
        Assert.True(rig.Sidebar.TryRenameSelected());
        await rig.Settle();
        rig.Sidebar.SelectedNode = Node(rig, "victim.md");
        // The system trash requires an STA apartment (the DeleteOnSta
        // pattern) — xunit facts run MTA.
        OnSta(() => rig.Sidebar.DeleteCommand.Execute(null));
        rig.Sidebar.UndoStructural();
        Assert.Contains(
            "Nothing to undo.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));
        Assert.True(File.Exists(Path.Combine(fixture.Root, "again.md")));
    }

    [Fact]
    public async Task ChangedFilesDropTheSuspectHistory()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-undo-changed");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "A\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        rig.Sidebar.SelectedNode = Node(rig, "a.md");
        rig.Sidebar.MutationName = "b.md";
        Assert.True(rig.Sidebar.TryRenameSelected());

        // The renamed file vanishes out-of-band: the preflight drops
        // the suspect history wholesale rather than replaying an
        // inverse against a stranger (mac's rule).
        File.Delete(Path.Combine(fixture.Root, "b.md"));
        rig.Sidebar.UndoStructural();
        Assert.Contains(
            "Can't undo — the files have changed.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));

        announced.Clear();
        rig.Sidebar.UndoStructural();
        Assert.Contains(
            "Nothing to undo.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));
    }

    [Fact]
    public async Task ABatchMoveUndoesThroughTheDedicatedEndpointAndRedoes()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-undo-batch");
        File.WriteAllText(Path.Combine(fixture.Root, "one.md"), "1\n");
        File.WriteAllText(Path.Combine(fixture.Root, "two.md"), "2\n");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "sub"));
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        Node(rig, "one.md").IsBatchSelected = true;
        Node(rig, "two.md").IsBatchSelected = true;
        rig.Sidebar.MoveDestination = "sub";
        rig.Sidebar.BatchMoveCommand.Execute(null);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "sub", "one.md")));

        rig.Sidebar.UndoStructural();
        Assert.True(File.Exists(Path.Combine(fixture.Root, "one.md")));
        Assert.True(File.Exists(Path.Combine(fixture.Root, "two.md")));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "sub", "one.md")));
        Assert.Contains(
            "Undid move of 2 items.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));

        rig.Sidebar.RedoStructural();
        Assert.True(File.Exists(Path.Combine(fixture.Root, "sub", "one.md")));
        Assert.True(File.Exists(Path.Combine(fixture.Root, "sub", "two.md")));
        Assert.Contains(
            "Redid move of 2 items.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));
    }

    // ---- Helpers ------------------------------------------------------

    private sealed record SidebarRig(FilesSidebarViewModel Sidebar)
    {
        /// <summary>Settle the tree after a mutation's Refresh —
        /// <c>Refresh()</c> swaps the completion task synchronously,
        /// so awaiting right after the mutation returns observes the
        /// NEW refresh, and the pump context runs posted publication
        /// on the thread pool (the W1 hardening pattern).</summary>
        public Task Settle() =>
            Sidebar.TreeRefreshCompletion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw failure;
        }
    }

    private static FileTreeNodeViewModel Node(SidebarRig rig, string path) =>
        Assert.Single(rig.Sidebar.RootNodes, node => node.Path == path);

    private static async Task<SidebarRig> NewSidebar(
        VaultSession session, FixtureVault fixture, List<A11yEvent> announced)
    {
        var sidebar = new FilesSidebarViewModel(
            session,
            announced.Add,
            vaultRoot: fixture.Root,
            localAppDataRoot: Path.Combine(fixture.Root, "device-state"),
            treeUiContext: new PumpSynchronizationContext());
        var rig = new SidebarRig(sidebar);
        await rig.Settle();
        return rig;
    }

    private static VaultSession OpenScanned(string root)
    {
        var session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    private sealed class PumpSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) =>
            ThreadPool.QueueUserWorkItem(_ => callback(state));
    }
}
