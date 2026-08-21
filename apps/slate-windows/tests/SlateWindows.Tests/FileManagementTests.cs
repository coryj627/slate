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

    [Fact]
    public async Task ADuplicateWalksTheFinderNamerPastOccupiedNames()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-duplicate");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "The source bytes.\n");
        File.WriteAllText(Path.Combine(fixture.Root, "b copy.md"), "Occupied.\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        // Arm the undo stack so the duplicate's BARRIER is observable.
        rig.Sidebar.SelectedNode = Node(rig, "a.md");
        rig.Sidebar.MutationName = "b.md";
        Assert.True(rig.Sidebar.TryRenameSelected());
        await rig.Settle();

        rig.Sidebar.SelectedNode = Node(rig, "b.md");
        rig.Sidebar.DuplicateCommand.Execute(null);

        // F5: CreateExclusive advances on typed DestinationExists —
        // never a pre-check — so the occupied "b copy.md" stays
        // untouched and the copy lands on the next candidate.
        Assert.Equal(
            "Occupied.\n",
            File.ReadAllText(Path.Combine(fixture.Root, "b copy.md")));
        Assert.Equal(
            "The source bytes.\n",
            File.ReadAllText(Path.Combine(fixture.Root, "b copy 2.md")));
        Assert.Contains(
            "Duplicated b.md as b copy 2.md.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));

        rig.Sidebar.UndoStructural();
        Assert.Contains(
            "Nothing to undo.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));
        Assert.True(File.Exists(Path.Combine(fixture.Root, "b.md")));
    }

    [Theory]
    [InlineData("a.md", "a copy.md", "a copy 2.md")]
    [InlineData("a copy.md", "a copy.md", "a copy 2.md")]
    [InlineData("b copy 3.md", "b copy.md", "b copy 2.md")]
    [InlineData("sub/c.md", "sub/c copy.md", "sub/c copy 2.md")]
    [InlineData("noext", "noext copy", "noext copy 2")]
    public void TheNamerReusesAnExistingCopyStem(
        string source, string first, string second)
    {
        // mac's duplicateName semantics verbatim: strip an existing
        // " copy"/" copy N" suffix, then walk "{base} copy",
        // "{base} copy 2", … — the LOWEST free name wins (a " copy"
        // source's own slot is occupied by the source, so the walk
        // advances past it via DestinationExists; never
        // "a copy copy.md").
        string[] candidates = [.. FilesSidebarViewModel
            .DuplicateCandidates(source).Take(2)];
        Assert.Equal([first, second], candidates);
    }

    [Fact]
    public async Task AFolderSelectionSpeaksTheCanonicalDuplicateRefusal()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-duplicate-folder");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "sub"));
        File.WriteAllText(Path.Combine(fixture.Root, "sub", "inner.md"), "I\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        rig.Sidebar.SelectedNode = Node(rig, "sub");
        rig.Sidebar.DuplicateCommand.Execute(null);

        // F5/F11: the canonical event — adopted on Windows here for
        // the first time — not host-composed prose.
        A11yEvent refusal = Assert.Single(
            announced.OfType<A11yEvent.DuplicateFilesOnly>());
        Assert.Equal(
            "Duplicate applies to files only.",
            SlateUniffiMethods.A11yRender(refusal).Text);
        Assert.Equal("Duplicate applies to files only.", rig.Sidebar.Status);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "sub copy")));
    }

    [Fact]
    public async Task CopyPathCopiesTheVaultRelativePathThroughTheSeam()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-copypath");
        File.WriteAllText(Path.Combine(fixture.Root, "note.md"), "N\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        var copied = new List<string>();
        SidebarRig rig = await NewSidebar(session, fixture, announced, copied.Add);

        rig.Sidebar.SelectedNode = Node(rig, "note.md");
        rig.Sidebar.CopyPathCommand.Execute(null);

        // F7: the VAULT-RELATIVE tree path (mac's semantics), plus the
        // canonical SelectionCopied — the CopyWikilink pattern.
        Assert.Equal(["note.md"], copied);
        Assert.Single(announced.OfType<A11yEvent.SelectionCopied>());
    }

    [Fact]
    public async Task RevealRoutesTheResolvedAbsolutePathThroughTheSeam()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-reveal");
        File.WriteAllText(Path.Combine(fixture.Root, "note.md"), "N\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        var revealed = new List<string>();
        SidebarRig rig = await NewSidebar(session, fixture, announced);
        rig.Sidebar.RevealRequested = revealed.Add;

        rig.Sidebar.SelectedNode = Node(rig, "note.md");
        int spokenBefore = announced.Count;
        rig.Sidebar.RevealCommand.Execute(null);

        // F8: the vault-resolved ABSOLUTE path, and no announcement —
        // the OS surface change is the feedback.
        Assert.Equal(
            [Path.Combine(fixture.Root, "note.md")], revealed);
        Assert.Equal(spokenBefore, announced.Count);
    }

    [Fact]
    public async Task CreatesWalkTheUntitledSequenceAndHandOffToInlineRename()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-create");
        File.WriteAllText(Path.Combine(fixture.Root, "Untitled.md"), "Taken.\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        SidebarRig rig = await NewSidebar(session, fixture, announced);
        int renameArms = 0;
        rig.Sidebar.InlineRenameRequested += () => renameArms++;
        var opened = new List<string>();
        rig.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);

        rig.Sidebar.CreateNoteCommand.Execute(null);
        await rig.Settle();

        // F1: the occupied "Untitled.md" advances the sequence (typed
        // DestinationExists, no pre-check, nothing clobbered); the new
        // note opens, the published node is selected, and the rename
        // flow re-arms with the field carrying the new name.
        Assert.Equal("Taken.\n", File.ReadAllText(Path.Combine(fixture.Root, "Untitled.md")));
        Assert.True(File.Exists(Path.Combine(fixture.Root, "Untitled 2.md")));
        Assert.Contains(
            "Created note Untitled 2.md.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));
        Assert.Contains("Untitled 2.md", opened);
        Assert.Equal(1, renameArms);
        Assert.Equal("Untitled 2.md", rig.Sidebar.SelectedNode?.Path);
        Assert.Equal("Untitled 2.md", rig.Sidebar.MutationName);

        // F2: the folder twin.
        rig.Sidebar.SelectedNode = null;
        rig.Sidebar.CreateFolderCommand.Execute(null);
        await rig.Settle();
        Assert.True(Directory.Exists(Path.Combine(fixture.Root, "Untitled Folder")));
        Assert.Contains(
            "Created folder Untitled Folder.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));
        Assert.Equal(2, renameArms);
        Assert.Equal("Untitled Folder", rig.Sidebar.SelectedNode?.Path);

        // The folder sequence advances on a typed collision too —
        // CreateFolder is the structural verb, not an idempotent
        // mkdir: the occupied name is skipped, never silently reused.
        rig.Sidebar.SelectedNode = null;
        rig.Sidebar.CreateFolderCommand.Execute(null);
        await rig.Settle();
        Assert.True(
            Directory.Exists(Path.Combine(fixture.Root, "Untitled Folder 2")));
        Assert.Equal(3, renameArms);
    }

    [Fact]
    public async Task DeleteIsImmediateForFilesAndEmptyFoldersAndStagedForFullOnes()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-delete-parity");
        File.WriteAllText(Path.Combine(fixture.Root, "note.md"), "N\n");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "empty"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "full"));
        File.WriteAllText(Path.Combine(fixture.Root, "full", "one.md"), "1\n");
        File.WriteAllText(Path.Combine(fixture.Root, "full", "two.md"), "2\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        SidebarRig rig = await NewSidebar(session, fixture, announced);
        var staged = new List<(string Title, string Message)>();
        rig.Sidebar.ConfirmRecycle = request =>
        {
            staged.Add(request);
            return false;
        };

        // A FILE trashes immediately — the seam never fires (Finder
        // parity, F6).
        rig.Sidebar.SelectedNode = Node(rig, "note.md");
        OnSta(() => rig.Sidebar.DeleteCommand.Execute(null));
        Assert.Empty(staged);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "note.md")));
        Assert.Contains(
            "Moved note.md to the Recycle Bin.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));

        // An EMPTY folder trashes immediately too.
        await rig.Settle();
        rig.Sidebar.SelectedNode = Node(rig, "empty");
        OnSta(() => rig.Sidebar.DeleteCommand.Execute(null));
        Assert.Empty(staged);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "empty")));

        // A NON-EMPTY folder stages the mac-verbatim confirmation
        // (Recycle Bin adaptation, curly quotes, recursive count) and
        // a refusal keeps it.
        await rig.Settle();
        rig.Sidebar.SelectedNode = Node(rig, "full");
        OnSta(() => rig.Sidebar.DeleteCommand.Execute(null));
        (string title, string message) = Assert.Single(staged);
        Assert.Equal("Move “full” to the Recycle Bin?", title);
        Assert.Equal(
            "Move “full” and its 2 items to the Recycle Bin. "
            + "Slate can't undo this action.",
            message);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "full", "one.md")));
    }

    [Fact]
    public async Task ABatchWithANonEmptyFolderStagesTheBatchCopy()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-batch-delete");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "A\n");
        File.WriteAllText(Path.Combine(fixture.Root, "b.md"), "B\n");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "full"));
        File.WriteAllText(Path.Combine(fixture.Root, "full", "inner.md"), "I\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        SidebarRig rig = await NewSidebar(session, fixture, announced);
        var staged = new List<(string Title, string Message)>();
        rig.Sidebar.ConfirmRecycle = request =>
        {
            staged.Add(request);
            return false;
        };

        // Files-only batch: straight to the Recycle Bin, no staging.
        Node(rig, "a.md").IsBatchSelected = true;
        Node(rig, "b.md").IsBatchSelected = true;
        OnSta(() => rig.Sidebar.BatchTrashCommand.Execute(null));
        Assert.Empty(staged);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "a.md")));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "b.md")));

        // A batch carrying a non-empty folder stages the batch copy
        // with the folder clause.
        await rig.Settle();
        Node(rig, "full").IsBatchSelected = true;
        OnSta(() => rig.Sidebar.BatchTrashCommand.Execute(null));
        (string title, string message) = Assert.Single(staged);
        Assert.Equal("Move 1 item to the Recycle Bin?", title);
        Assert.Equal(
            "Move 1 item, including 1 folder with contents, to the "
            + "Recycle Bin. Slate can't undo this action.",
            message);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "full", "inner.md")));
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
        VaultSession session,
        FixtureVault fixture,
        List<A11yEvent> announced,
        Action<string>? copyText = null)
    {
        var sidebar = new FilesSidebarViewModel(
            session,
            announced.Add,
            copyText: copyText,
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
