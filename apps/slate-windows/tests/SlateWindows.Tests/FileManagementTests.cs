// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.FileManagement;
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
        var announced = new SynchronizedAnnouncements();
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
        var announced = new SynchronizedAnnouncements();
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
        var announced = new SynchronizedAnnouncements();
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
        var announced = new SynchronizedAnnouncements();
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
        var announced = new SynchronizedAnnouncements();
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
        var announced = new SynchronizedAnnouncements();
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
        var announced = new SynchronizedAnnouncements();
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
        var announced = new SynchronizedAnnouncements();
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
        var announced = new SynchronizedAnnouncements();
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
        var announced = new SynchronizedAnnouncements();
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
        var announced = new SynchronizedAnnouncements();
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

    /// <summary>
    /// #1136: a publication landing while the rename is armed — the
    /// lifecycle's watcher-driven refresh ~150 ms after a create, or any
    /// refresh — re-seats the selection on the fresh SAME-PATH node. It
    /// must not reset the name field the user is typing into: the shell
    /// gate's FileManagement journey flaked on exactly this (the commit
    /// renamed Untitled.md to itself and journey.md never landed). A
    /// re-seat on a DIFFERENT node — the rename's own new path — does
    /// carry that node's name into the field.
    /// </summary>
    [Fact]
    public async Task APublicationDuringInlineRenameKeepsTheTypedName()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-rename-draft");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);
        rig.Sidebar.CreateNoteCommand.Execute(null);
        await rig.Settle();
        Assert.Equal("Untitled.md", rig.Sidebar.SelectedNode?.Path);
        Assert.Equal("Untitled.md", rig.Sidebar.MutationName);

        // The user types; then the watcher's refresh lands.
        rig.Sidebar.MutationName = "journey.md";
        rig.Sidebar.Refresh();
        await rig.Settle();

        Assert.Equal("Untitled.md", rig.Sidebar.SelectedNode?.Path);
        Assert.Equal("journey.md", rig.Sidebar.MutationName);

        // The commit renames to the draft, and the rename's own
        // publication re-seats on the NEW path — whose name the field
        // now carries.
        Assert.True(rig.Sidebar.TryRenameSelected());
        await rig.Settle();
        Assert.True(File.Exists(Path.Combine(fixture.Root, "journey.md")));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "Untitled.md")));
        Assert.Equal("journey.md", rig.Sidebar.SelectedNode?.Path);
        Assert.Equal("journey.md", rig.Sidebar.MutationName);
    }

    [Fact]
    public async Task CreatesWalkTheUntitledSequenceAndHandOffToInlineRename()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-create");
        File.WriteAllText(Path.Combine(fixture.Root, "Untitled.md"), "Taken.\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
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
        var announced = new SynchronizedAnnouncements();
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
        var announced = new SynchronizedAnnouncements();
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

    [Fact]
    public async Task TheMoveToPickerFiltersIllegalDestinationsAndMovesTheSelection()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-moveto");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "# A\n");
        // Basename-resolving links survive a move UNREWRITTEN (core's
        // no-churn rule, link_rewrite.rs — qualified and markdown-path
        // forms rewrite, gated core-side); the suffix composition is
        // shared with rename and suffix-gated there.
        File.WriteAllText(Path.Combine(fixture.Root, "b.md"), "Points at [[a]].\n");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "sub", "deep"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "other"));
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        rig.Sidebar.SelectedNode = Node(rig, "a.md");
        rig.Sidebar.MoveToCommand.Execute(null);
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(
            rig.Sidebar.MoveToSheet);

        // F4: a.md's current parent is the ROOT, so the pinned Vault
        // root row is filtered before the pick; every real folder
        // lists.
        Assert.DoesNotContain(
            picker.Rows, row => row.Kind == MoveToRowKind.VaultRoot);
        Assert.Equal(
            ["other", "sub", "sub/deep"],
            picker.Rows.Where(row => row.Kind == MoveToRowKind.Folder)
                .Select(row => row.Destination).OrderBy(path => path, StringComparer.Ordinal));

        picker.ActivateCommand.Execute(
            picker.Rows.Single(row => row.Destination == "sub"));

        // The single-entry FFI with F9 consumption: the sheet closed,
        // the inverse landed on the undo stack, and the still-
        // resolving basename link stayed byte-identical (no suffix —
        // nothing was rewritten).
        Assert.Null(rig.Sidebar.MoveToSheet);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "sub", "a.md")));
        Assert.Equal(
            "Points at [[a]].\n",
            File.ReadAllText(Path.Combine(fixture.Root, "b.md")));
        Assert.Contains(
            "Moved a.md to sub.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));

        rig.Sidebar.UndoStructural();
        Assert.True(File.Exists(Path.Combine(fixture.Root, "a.md")));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "sub", "a.md")));
        Assert.Contains(
            "Undid move of a.md.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));
    }

    [Fact]
    public async Task AMovingFoldersOwnSubtreeNeverAppearsInThePick()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-moveto-subtree");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "sub", "deep"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "other"));
        File.WriteAllText(Path.Combine(fixture.Root, "sub", "inner.md"), "I\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        rig.Sidebar.SelectedNode = Node(rig, "sub");
        rig.Sidebar.MoveToCommand.Execute(null);
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(
            rig.Sidebar.MoveToSheet);

        // F4: the folder itself, its whole subtree, and its current
        // parent are all filtered BEFORE the pick.
        Assert.Equal(
            ["other"],
            picker.Rows.Where(row => row.Kind == MoveToRowKind.Folder)
                .Select(row => row.Destination));
        Assert.DoesNotContain(
            picker.Rows, row => row.Kind == MoveToRowKind.VaultRoot);
        picker.CancelCommand.Execute(null);
        Assert.Null(rig.Sidebar.MoveToSheet);
    }

    [Fact]
    public async Task TheTypedNewFolderRowCreatesThenMovesInOneGesture()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-moveto-newfolder");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "A\n");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "decoy"));
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        rig.Sidebar.SelectedNode = Node(rig, "a.md");
        rig.Sidebar.MoveToCommand.Execute(null);
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(
            rig.Sidebar.MoveToSheet);

        // The typed filter IS the new folder's path (one user
        // gesture, two core ops); the row hides while the text names
        // an existing folder verbatim.
        picker.FilterText = "decoy";
        Assert.DoesNotContain(
            picker.Rows, row => row.Kind == MoveToRowKind.NewFolder);
        picker.FilterText = "fresh";
        MoveToRowViewModel create = Assert.Single(
            picker.Rows, row => row.Kind == MoveToRowKind.NewFolder);
        picker.ActivateCommand.Execute(create);

        Assert.Null(rig.Sidebar.MoveToSheet);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "fresh", "a.md")));
        Assert.Contains(
            "Moved a.md to fresh.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));
    }

    [Fact]
    public async Task ABatchPickRidesBatchMoveWithOneSummary()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-moveto-batch");
        File.WriteAllText(Path.Combine(fixture.Root, "one.md"), "1\n");
        File.WriteAllText(Path.Combine(fixture.Root, "two.md"), "2\n");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "sub"));
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        Node(rig, "one.md").IsBatchSelected = true;
        Node(rig, "two.md").IsBatchSelected = true;
        rig.Sidebar.MoveToCommand.Execute(null);
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(
            rig.Sidebar.MoveToSheet);
        Assert.Equal("2 items", picker.ItemNoun);

        picker.ActivateCommand.Execute(
            picker.Rows.Single(row => row.Destination == "sub"));

        Assert.Null(rig.Sidebar.MoveToSheet);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "sub", "one.md")));
        Assert.True(File.Exists(Path.Combine(fixture.Root, "sub", "two.md")));
        Assert.Contains(
            "Moved 2 items to sub.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));

        rig.Sidebar.UndoStructural();
        Assert.True(File.Exists(Path.Combine(fixture.Root, "one.md")));
        Assert.True(File.Exists(Path.Combine(fixture.Root, "two.md")));
    }

    [Fact]
    public async Task TheAdmissionSeamGatesTheOpenAndTheFilterNarrowsRows()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-moveto-admission");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "A\n");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "alpha"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "beta"));
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        // T9's shape: the admission refused → no sheet.
        rig.Sidebar.MoveToOpenAdmission = () => false;
        rig.Sidebar.SelectedNode = Node(rig, "a.md");
        rig.Sidebar.MoveToCommand.Execute(null);
        Assert.Null(rig.Sidebar.MoveToSheet);

        rig.Sidebar.MoveToOpenAdmission = () => true;
        rig.Sidebar.MoveToCommand.Execute(null);
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(
            rig.Sidebar.MoveToSheet);

        // Filter-as-you-type narrows to substring matches.
        picker.FilterText = "alp";
        Assert.Equal(
            ["alpha"],
            picker.Rows.Where(row => row.Kind == MoveToRowKind.Folder)
                .Select(row => row.Destination));
        picker.FilterText = string.Empty;
        Assert.Equal(
            2,
            picker.Rows.Count(row => row.Kind == MoveToRowKind.Folder));
    }

    [Fact]
    public async Task ARejectedBatchUndoNeverAnnouncesSuccessOrInvertsTheStacks()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-batch-undo-rejected");
        File.WriteAllText(Path.Combine(fixture.Root, "one.md"), "1\n");
        File.WriteAllText(Path.Combine(fixture.Root, "two.md"), "2\n");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "sub"));
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        Node(rig, "one.md").IsBatchSelected = true;
        Node(rig, "two.md").IsBatchSelected = true;
        rig.Sidebar.MoveDestination = "sub";
        rig.Sidebar.BatchMoveCommand.Execute(null);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "sub", "one.md")));

        // An external collision at the undo's destination: core
        // reports Rejected as a STATE, not an exception (red team,
        // correctness 1) — the undo must drop the suspect history and
        // say so, never announce success or record a redo.
        File.WriteAllText(Path.Combine(fixture.Root, "one.md"), "intruder\n");
        rig.Sidebar.UndoStructural();
        string[] spoken = [.. announced
            .OfType<A11yEvent.HostComposed>()
            .Select(item => SlateUniffiMethods.A11yRender(item).Text)];
        Assert.Contains("Can't undo — the files have changed.", spoken);
        Assert.DoesNotContain("Undid move of 2 items.", spoken);
        Assert.Equal(
            "intruder\n", File.ReadAllText(Path.Combine(fixture.Root, "one.md")));

        announced.Clear();
        rig.Sidebar.RedoStructural();
        Assert.Contains(
            "Nothing to redo.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));
    }

    [Fact]
    public async Task ANewerStepPurgesTheDeadBatchEntryInsteadOfLying()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-batch-purge");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "A\n");
        File.WriteAllText(Path.Combine(fixture.Root, "one.md"), "1\n");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "sub"));
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        Node(rig, "one.md").IsBatchSelected = true;
        rig.Sidebar.MoveDestination = "sub";
        rig.Sidebar.BatchMoveCommand.Execute(null);
        await rig.Settle();

        // A rename lands ON TOP of the batch entry: UndoBatchMove
        // admits only the journal's latest row and every undo
        // journals, so the batch entry is dead the moment the rename
        // journals — reaching it later destroyed both stacks under a
        // false "files have changed" (red team, correctness 3). The
        // push purges it instead.
        rig.Sidebar.SelectedNode = Node(rig, "a.md");
        rig.Sidebar.MutationName = "b.md";
        Assert.True(rig.Sidebar.TryRenameSelected());

        rig.Sidebar.UndoStructural();
        Assert.True(File.Exists(Path.Combine(fixture.Root, "a.md")));
        announced.Clear();
        rig.Sidebar.UndoStructural();
        string[] spoken = [.. announced
            .OfType<A11yEvent.HostComposed>()
            .Select(item => SlateUniffiMethods.A11yRender(item).Text)];
        Assert.Contains("Nothing to undo.", spoken);
        Assert.DoesNotContain("Can't undo — the files have changed.", spoken);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "sub", "one.md")));
    }

    [Fact]
    public async Task AFolderNoteRenameCorrectsStoredPathsBeyondPrefixMath()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-fnote-stored");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "docs"));
        File.WriteAllText(Path.Combine(fixture.Root, "docs", "docs.md"), "# docs\n");
        File.WriteAllText(Path.Combine(fixture.Root, "docs", "other.md"), "O\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        FileTreeNodeViewModel docs = Node(rig, "docs");
        docs.IsExpanded = true;
        Assert.True(
            SpinWait.SpinUntil(
                () => docs.Children.Any(child => child.Path == "docs/docs.md"),
                TimeSpan.FromSeconds(5)),
            "the folder's children never materialized.");
        rig.Sidebar.SelectedNode =
            docs.Children.Single(child => child.Path == "docs/docs.md");
        rig.Sidebar.PinCommand.Execute(null);
        rig.Sidebar.SelectedNode =
            docs.Children.Single(child => child.Path == "docs/other.md");
        rig.Sidebar.PinCommand.Execute(null);

        rig.Sidebar.SelectedNode = Node(rig, "docs");
        rig.Sidebar.MutationName = "guide";
        Assert.True(rig.Sidebar.TryRenameSelected());

        // Red team (correctness 2): the compound rename moves the
        // NOTE'S LEAF beyond prefix math — docs/docs.md lands at
        // guide/guide.md, not guide/docs.md; every stored path must
        // follow the report's real pair while siblings ride the
        // prefix. The persisted pins are the observable store.
        Assert.True(File.Exists(Path.Combine(fixture.Root, "guide", "guide.md")));
        System.Collections.Generic.IReadOnlySet<string> pins =
            new SidebarSettingsStore(fixture.Root).Load().Pins;
        Assert.Contains("guide/guide.md", pins);
        Assert.Contains("guide/other.md", pins);
        Assert.DoesNotContain("guide/docs.md", pins);
    }

    [Fact]
    public async Task TheTypedNewFolderRowObeysTheSameLegalityAsTheList()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-newfolder-legality");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "moving", "inner"));
        File.WriteAllText(
            Path.Combine(fixture.Root, "moving", "note.md"), "N\n");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "elsewhere"));
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        rig.Sidebar.SelectedNode = Node(rig, "moving");
        rig.Sidebar.MoveToCommand.Execute(null);
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(
            rig.Sidebar.MoveToSheet);

        // Red team (correctness 4): the typed path must not resurface
        // an illegal or existing destination as a "create" — a fresh
        // path INSIDE the moving folder's own subtree, the current
        // parent, and any known folder all hide the row.
        picker.FilterText = "moving/new";
        Assert.DoesNotContain(
            picker.Rows, row => row.Kind == MoveToRowKind.NewFolder);
        picker.FilterText = "moving/inner";
        Assert.DoesNotContain(
            picker.Rows, row => row.Kind == MoveToRowKind.NewFolder);
        picker.FilterText = "elsewhere";
        Assert.DoesNotContain(
            picker.Rows, row => row.Kind == MoveToRowKind.NewFolder);
        picker.FilterText = "fresh";
        Assert.Single(picker.Rows, row => row.Kind == MoveToRowKind.NewFolder);
    }

    [Fact]
    public void TheVaultRootRowStaysPinnedUnderAFilter()
    {
        var announced = new SynchronizedAnnouncements();
        var picker = new MoveToPickerViewModel(
            ["alpha", "beta"],
            rootIsLegal: true,
            itemNoun: "a.md",
            confirmed: _ => { },
            createAndMove: _ => { },
            cancelled: () => { },
            newFolderPathAllowed: _ => true,
            announce: announced.Add);

        // Pinned (red team, contracts 6): a typed query must never
        // make the root unreachable — and the DEFAULT selection under
        // a query prefers the query's own first match, so Enter never
        // silently retargets the root.
        Assert.Contains(picker.Rows, row => row.Kind == MoveToRowKind.VaultRoot);
        picker.FilterText = "alp";
        Assert.Contains(picker.Rows, row => row.Kind == MoveToRowKind.VaultRoot);
        Assert.Equal(MoveToRowKind.Folder, picker.SelectedRow?.Kind);
        picker.FilterText = "zzz-no-match";
        Assert.Contains(picker.Rows, row => row.Kind == MoveToRowKind.VaultRoot);
        Assert.Equal(MoveToRowKind.NewFolder, picker.SelectedRow?.Kind);
    }

    [Fact]
    public void ThePickerSpeaksItsSelectionAndItsFilterLandings()
    {
        var announced = new SynchronizedAnnouncements();
        var picker = new MoveToPickerViewModel(
            ["alpha", "beta"],
            rootIsLegal: false,
            itemNoun: "a.md",
            confirmed: _ => { },
            createAndMove: _ => { },
            cancelled: () => { },
            newFolderPathAllowed: _ => true,
            announce: announced.Add);

        // Red team (a11y 1): arrow-driven selection speaks the row;
        // each filter landing speaks the count — the quick-open
        // pattern's two halves.
        announced.Clear();
        picker.SelectedRow = picker.Rows[1];
        Assert.Single(announced.OfType<A11yEvent.RowSelected>());

        announced.Clear();
        picker.FilterText = "alp";
        Assert.Contains(
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text),
            text => text.EndsWith("destinations.", StringComparison.Ordinal)
                || text.EndsWith("destination.", StringComparison.Ordinal));
        // The rebuild's own default selection stays silent (only the
        // user's arrows speak rows).
        Assert.Empty(announced.OfType<A11yEvent.RowSelected>());
    }

    [Fact]
    public async Task DeleteRefusesPlaceholdersAndGroupHeaders()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-delete-guard");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "A\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        rig.Sidebar.GroupByDate = true;
        await rig.Settle();
        FileTreeNodeViewModel header = rig.Sidebar.RootNodes
            .First(node => node.IsGroupHeader);
        rig.Sidebar.SelectedNode = header;

        // Red team: a selected date header reached DeleteFile("") and
        // spoke a spurious failure; the guard matches the sibling
        // verbs.
        Assert.False(rig.Sidebar.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task AFolderRenameRetargetsEveryOpenDescendantSynchronously()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-folder-retarget");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "fold"));
        File.WriteAllText(Path.Combine(fixture.Root, "fold", "fold.md"), "# note\n");
        File.WriteAllText(Path.Combine(fixture.Root, "fold", "other.md"), "O\n");
        File.WriteAllText(Path.Combine(fixture.Root, "fold", "third.md"), "T\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        var retargets = new List<(string Old, string New)>();
        SidebarRig rig = await NewSidebar(session, fixture, announced);
        rig.Sidebar.RetargetRequested =
            (oldPath, newPath) => retargets.Add((oldPath, newPath));

        rig.Sidebar.SelectedNode = Node(rig, "fold");
        rig.Sidebar.MutationName = "gold";
        Assert.True(rig.Sidebar.TryRenameSelected());

        // Codex round 1 (refuted, pinned): core's StructuralReport
        // lists EVERY contained file for a folder move — "a folder
        // move lists each contained file; the folder itself is
        // implied" — so ordinary descendants retarget synchronously
        // at the mutation site, not only the folder note.
        Assert.Contains(("fold/fold.md", "gold/gold.md"), retargets);
        Assert.Contains(("fold/other.md", "gold/other.md"), retargets);
        Assert.Contains(("fold/third.md", "gold/third.md"), retargets);
    }

    [Fact]
    public async Task RewriteFailureDetailSurvivesTheSuccessSentenceInStatus()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-failed-detail");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "# A\n");
        File.WriteAllText(
            Path.Combine(fixture.Root, "linker.md"), "See [[a]].\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        // Fault the linker's rewrite mid-save: the rename lands, the
        // rewrite records a per-file failure (core's rule — never an
        // abort).
        using IDisposable envLock = EnvFaultLock.Acquire();
        Environment.SetEnvironmentVariable(
            "SLATE_TEST_FAULT_AFTER_WRITE", "linker");
        try
        {
            rig.Sidebar.SelectedNode = Node(rig, "a.md");
            rig.Sidebar.MutationName = "b.md";
            Assert.True(rig.Sidebar.TryRenameSelected());
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_AFTER_WRITE", null);
        }

        // Codex round 1: the success sentence must not bury the
        // failure — the FINAL Status carries both halves with the
        // affected path inspectable, while speech got the High
        // warning plus the sentence.
        Assert.StartsWith("Renamed a.md to b.md", rig.Sidebar.Status, StringComparison.Ordinal);
        Assert.Contains("could not be updated", rig.Sidebar.Status, StringComparison.Ordinal);
        Assert.Contains("linker.md", rig.Sidebar.Status, StringComparison.Ordinal);
        Assert.Contains(
            "Links in 1 note could not be updated.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));
    }

    [Fact]
    public async Task UndoRefusesAReplacementAtTheRecordedPath()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-undo-replacement");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "Original.\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        rig.Sidebar.SelectedNode = Node(rig, "a.md");
        rig.Sidebar.MutationName = "b.md";
        Assert.True(rig.Sidebar.TryRenameSelected());

        // A sync client deletes b.md and creates an UNRELATED b.md:
        // existence alone would let Ctrl+Z rename the stranger (codex
        // round 2) — the filesystem identity captured at push must
        // refuse it.
        File.Delete(Path.Combine(fixture.Root, "b.md"));
        File.WriteAllText(Path.Combine(fixture.Root, "b.md"), "Stranger.\n");
        announced.Clear();
        rig.Sidebar.UndoStructural();
        Assert.Contains(
            "Can't undo — the files have changed.",
            announced.OfType<A11yEvent.HostComposed>()
                .Select(item => SlateUniffiMethods.A11yRender(item).Text));
        Assert.Equal(
            "Stranger.\n", File.ReadAllText(Path.Combine(fixture.Root, "b.md")));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "a.md")));
    }

    [Fact]
    public async Task UndoStillSpansOrdinaryContentEdits()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-undo-edited");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "Original.\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        rig.Sidebar.SelectedNode = Node(rig, "a.md");
        rig.Sidebar.MutationName = "b.md";
        Assert.True(rig.Sidebar.TryRenameSelected());

        // An in-place edit keeps the file's identity: undo must still
        // apply (the identity check rejects replacements, never
        // ordinary edits — mac's model).
        File.AppendAllText(Path.Combine(fixture.Root, "b.md"), "Edited.\n");
        rig.Sidebar.UndoStructural();
        Assert.True(File.Exists(Path.Combine(fixture.Root, "a.md")));
        Assert.Equal(
            "Original.\nEdited.\n",
            File.ReadAllText(Path.Combine(fixture.Root, "a.md")));
    }

    [Fact]
    public async Task BatchChecksSurviveAPublicationAndTheCountStaysTrue()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-batch-refresh");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "A\n");
        File.WriteAllText(Path.Combine(fixture.Root, "b.md"), "B\n");
        File.WriteAllText(Path.Combine(fixture.Root, "c.md"), "C\n");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "sub"));
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        Node(rig, "a.md").IsBatchSelected = true;
        Node(rig, "b.md").IsBatchSelected = true;
        Assert.Equal(2, rig.Sidebar.BatchSelectionCount);

        // Any refresh republished all-new nodes with every checkbox
        // cleared while the count kept its stale value (codex round
        // 4) — Move To then silently targeted only the focused item.
        // Publication now rebinds surviving checks and recomputes.
        rig.Sidebar.Refresh();
        await rig.Settle();
        Assert.Equal(2, rig.Sidebar.BatchSelectionCount);
        Assert.True(Node(rig, "a.md").IsBatchSelected);
        Assert.True(Node(rig, "b.md").IsBatchSelected);
        Assert.False(Node(rig, "c.md").IsBatchSelected);

        rig.Sidebar.SelectedNode = Node(rig, "c.md");
        rig.Sidebar.MoveToCommand.Execute(null);
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(
            rig.Sidebar.MoveToSheet);
        Assert.Equal("2 items", picker.ItemNoun);
        picker.CancelCommand.Execute(null);

        // Checks on vanished paths drop with their files: the count
        // follows the published tree (vacate the checked path through
        // core — a raw disk delete is invisible to the index until a
        // rescan).
        _ = session.RenameFile("b.md", "z.md");
        rig.Sidebar.Refresh();
        await rig.Settle();
        Assert.Equal(1, rig.Sidebar.BatchSelectionCount);
        Assert.False(Node(rig, "z.md").IsBatchSelected);
    }

    [Fact]
    public async Task ACollapsedCheckedDescendantSurvivesPublication()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-collapsed-check");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        File.WriteAllText(Path.Combine(fixture.Root, "folder", "a.md"), "A\n");
        File.WriteAllText(Path.Combine(fixture.Root, "c.md"), "C\n");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "sub"));
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        FileTreeNodeViewModel folder = Node(rig, "folder");
        folder.IsExpanded = true;
        Assert.True(
            SpinWait.SpinUntil(
                () => folder.Children.Any(child => child.Path == "folder/a.md"),
                TimeSpan.FromSeconds(5)));
        folder.Children.Single(child => child.Path == "folder/a.md")
            .IsBatchSelected = true;
        folder.IsExpanded = false;
        Assert.Equal(1, rig.Sidebar.BatchSelectionCount);

        // Codex round 5: the fresh tree holds only the collapsed
        // folder's PLACEHOLDER — the checked descendant is merely
        // unmaterialized, not gone; the authoritative set keeps it
        // and Move To still targets the batch.
        rig.Sidebar.Refresh();
        await rig.Settle();
        Assert.Equal(1, rig.Sidebar.BatchSelectionCount);

        rig.Sidebar.SelectedNode = Node(rig, "c.md");
        rig.Sidebar.MoveToCommand.Execute(null);
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(
            rig.Sidebar.MoveToSheet);
        Assert.Equal("a.md", picker.ItemNoun);
        picker.CancelCommand.Execute(null);
    }

    [Fact]
    public async Task AnActiveFilterDoesNotEraseTheMutationStatus()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-filtered-status");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "# A\n");
        File.WriteAllText(
            Path.Combine(fixture.Root, "linker.md"), "See [[a]].\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        rig.Sidebar.FilterText = "linker";
        await rig.Sidebar.FilterCompletion;

        // A faulted rewrite under an ACTIVE filter: the automatic
        // refilter the mutation's publication schedules must preserve
        // the final status (the failed path stays inspectable) and
        // must not speak a redundant count (codex round 5). The env
        // lock releases BEFORE any await — Monitor.Exit must run on
        // the acquiring thread, and the fault is already cleared by
        // scope end.
        using (EnvFaultLock.Acquire())
        {
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_AFTER_WRITE", "linker");
            try
            {
                rig.Sidebar.SelectedNode = Node(rig, "a.md");
                rig.Sidebar.MutationName = "b.md";
                Assert.True(rig.Sidebar.TryRenameSelected());
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    "SLATE_TEST_FAULT_AFTER_WRITE", null);
            }
        }

        await rig.Settle();
        await rig.Sidebar.FilterCompletion;
        int countsBefore = announced.OfType<A11yEvent.FileListCount>().Count();
        Assert.StartsWith(
            "Renamed a.md to b.md", rig.Sidebar.Status, StringComparison.Ordinal);
        Assert.Contains("linker.md", rig.Sidebar.Status, StringComparison.Ordinal);
        // Exactly the USER filter's one count — the automatic
        // refilter stayed quiet.
        Assert.Equal(1, countsBefore);
    }

    [Fact]
    public async Task ReExpansionProjectsTheAuthoritativeCheckOntoFreshNodes()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-reexpand-check");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        File.WriteAllText(Path.Combine(fixture.Root, "folder", "a.md"), "A\n");
        File.WriteAllText(Path.Combine(fixture.Root, "folder", "b.md"), "B\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        FileTreeNodeViewModel folder = Node(rig, "folder");
        folder.IsExpanded = true;
        Assert.True(
            SpinWait.SpinUntil(
                () => folder.Children.Any(child => child.Path == "folder/a.md"),
                TimeSpan.FromSeconds(5)));
        folder.Children.Single(child => child.Path == "folder/a.md")
            .IsBatchSelected = true;
        folder.IsExpanded = false;

        rig.Sidebar.Refresh();
        await rig.Settle();
        Assert.Equal(1, rig.Sidebar.BatchSelectionCount);

        // Codex round 6: re-expanding materializes FRESH child nodes —
        // without projection the checkbox showed unchecked while the
        // batch verbs still targeted the path (a visually unchecked
        // file could be trashed without any visible selection).
        FileTreeNodeViewModel freshFolder = Node(rig, "folder");
        freshFolder.IsExpanded = true;
        Assert.True(
            SpinWait.SpinUntil(
                () => freshFolder.Children.Any(child =>
                    child.Path == "folder/a.md" && child.IsBatchSelected),
                TimeSpan.FromSeconds(5)),
            "the re-expanded child never projected its authoritative check.");
        Assert.False(freshFolder.Children
            .Single(child => child.Path == "folder/b.md").IsBatchSelected);
        Assert.Equal(1, rig.Sidebar.BatchSelectionCount);
    }

    [Fact]
    public async Task ClearingTheFilterDropsThePendingMutationReassert()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-filter-clear");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "# A\n");
        File.WriteAllText(Path.Combine(fixture.Root, "other.md"), "O\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        rig.Sidebar.FilterText = "other";
        await rig.Sidebar.FilterCompletion;

        rig.Sidebar.SelectedNode = Node(rig, "a.md");
        rig.Sidebar.MutationName = "b.md";
        Assert.True(rig.Sidebar.TryRenameSelected());

        // The USER clears the filter while the automatic refilter is
        // pending (codex round 6): the pending reassert must clear
        // with it, or a later organic refresh resurrects the obsolete
        // mutation status.
        rig.Sidebar.FilterText = string.Empty;
        await rig.Settle();
        await rig.Sidebar.FilterCompletion;

        rig.Sidebar.Refresh();
        await rig.Settle();
        Assert.DoesNotContain("Renamed", rig.Sidebar.Status, StringComparison.Ordinal);
    }

    // ---- Helpers ------------------------------------------------------

    /// <summary>Announcement sink safe against the rig's ThreadPool
    /// pump (CI caught "Collection was modified" — a publication's
    /// selection announcement appended from a pool thread while a
    /// fact enumerated). Adds lock; enumeration walks a
    /// snapshot.</summary>
    private sealed class SynchronizedAnnouncements
        : System.Collections.Generic.IEnumerable<A11yEvent>
    {
        private readonly List<A11yEvent> _items = [];

        public void Add(A11yEvent item)
        {
            lock (_items)
            {
                _items.Add(item);
            }
        }

        public void Clear()
        {
            lock (_items)
            {
                _items.Clear();
            }
        }

        public int Count
        {
            get
            {
                lock (_items)
                {
                    return _items.Count;
                }
            }
        }

        public System.Collections.Generic.IEnumerator<A11yEvent> GetEnumerator()
        {
            A11yEvent[] snapshot;
            lock (_items)
            {
                snapshot = [.. _items];
            }

            return ((System.Collections.Generic.IEnumerable<A11yEvent>)snapshot)
                .GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }

    /// <summary>§E TE-10 (IE-21/IE-22): New Canvas is the vault-scoped
    /// create — core's canonical bytes, never a host literal; the new
    /// document gets its OWN tab (mac's rule: replacing the current tab
    /// could destroy an unsaved buffer's only owner); the sentence is
    /// <summary>§E TE-10 (IE-21/IE-22): New Canvas is the vault-scoped
    /// create - core's canonical bytes, never a host literal; the new
    /// document gets its OWN tab (mac's rule: replacing the current tab
    /// could destroy an unsaved buffer's only owner); the sentence is
    /// core's CanvasFileCreated render, spoken once.</summary>
    [Fact]
    public async Task NewCanvasCreatesTheCanonicalDocumentAndOpensItInANewTab()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-new-canvas");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);
        var opened = new List<(string Path, WorkspaceOpenTarget Target)>();
        rig.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request);

        rig.Sidebar.CreateCanvasCommand.Execute(null);
        await rig.Settle();

        Assert.Equal(
            SlateUniffiMethods.CanvasCanonicalEmptyText(),
            File.ReadAllText(Path.Combine(fixture.Root, "Untitled Canvas.canvas")));
        (string path, WorkspaceOpenTarget target) = Assert.Single(opened);
        Assert.Equal("Untitled Canvas.canvas", path);
        Assert.Equal(WorkspaceOpenTarget.NewTab, target);
        Assert.Equal("Created canvas \"Untitled Canvas\".", rig.Sidebar.Status);
        _ = Assert.Single(announced.OfType<A11yEvent.Canvas>());
    }

    /// <summary>§E TE-10: the unique-untitled sequence advances on the
    /// typed DestinationExists - never a pre-check, never a clobber.</summary>
    [Fact]
    public async Task NewCanvasAdvancesPastAnOccupiedNameByTheTypedRefusal()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-new-canvas-adv");
        File.WriteAllText(
            Path.Combine(fixture.Root, "Untitled Canvas.canvas"), "occupied\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);

        rig.Sidebar.CreateCanvasCommand.Execute(null);
        await rig.Settle();

        Assert.Equal(
            "occupied\n",
            File.ReadAllText(Path.Combine(fixture.Root, "Untitled Canvas.canvas")));
        Assert.Equal(
            SlateUniffiMethods.CanvasCanonicalEmptyText(),
            File.ReadAllText(Path.Combine(fixture.Root, "Untitled Canvas 2.canvas")));
    }

    /// <summary>§E TE-10 (IE-23, #1123): the terminal outcome table. A
    /// landed-but-unindexed create FINALIZES - the caveat is spoken
    /// after the created sentence, the file opens by path, and the flow
    /// never retries under another name; the NEXT invoke advances past
    /// the landed file by the disk gate's typed refusal.</summary>
    [Fact]
    public async Task ALandedButUnindexedCanvasOpensAndIsNeverRecreated()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "fm-new-canvas-landed");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new SynchronizedAnnouncements();
        SidebarRig rig = await NewSidebar(session, fixture, announced);
        var opened = new List<string>();
        rig.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);

        using (EnvFaultLock.Acquire())
        {
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_AFTER_WRITE", "Untitled Canvas");
            try
            {
                rig.Sidebar.CreateCanvasCommand.Execute(null);
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    "SLATE_TEST_FAULT_AFTER_WRITE", null);
            }
        }
        await rig.Settle();

        // Finalized ONCE: the landed file, opened, with no second name.
        Assert.Equal(
            SlateUniffiMethods.CanvasCanonicalEmptyText(),
            File.ReadAllText(Path.Combine(fixture.Root, "Untitled Canvas.canvas")));
        Assert.Equal("Untitled Canvas.canvas", Assert.Single(opened));
        Assert.False(
            File.Exists(Path.Combine(fixture.Root, "Untitled Canvas 2.canvas")),
            "the landed arm advanced the sequence - a duplicate create (IE-23).");
        _ = Assert.Single(announced.OfType<A11yEvent.Canvas>());
        Assert.Contains(
            announced.OfType<A11yEvent.HostComposed>(),
            e => e.Text.Contains("was written but not indexed", StringComparison.Ordinal));

        // The NEXT create is a NEW create: the landed file's name is
        // occupied on disk, so the typed refusal advances - no retry.
        rig.Sidebar.CreateCanvasCommand.Execute(null);
        await rig.Settle();
        Assert.Equal(
            SlateUniffiMethods.CanvasCanonicalEmptyText(),
            File.ReadAllText(Path.Combine(fixture.Root, "Untitled Canvas 2.canvas")));
    }

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
        SynchronizedAnnouncements announced,
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
