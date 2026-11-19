// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using SlateWindows.FileManagement;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-15a (#1890): every Files sidebar failure sentence built from a
/// caught failure says core's words (<c>FailureDetail.Of</c>, core's
/// <c>vault_error_detail</c> for a vault error), never the binding's field
/// dump (<c>@message=…</c>, <c>@path=…, @reason=…</c>). One fact per vault
/// site, each driving a real core refusal there and asserting the exact
/// status line and the one High announcement (or the canvas or graph event's
/// text), and no field token. The oracles are literals where core's text is
/// fixed and locale-free; for an operating-system error (<c>Io</c>) the
/// oracle is the lead and core's detail of the same call made directly.
/// Four sites have no fact here. <c>FilesSidebarViewModel.TreeOperations.cs</c>'s
/// synchronous refresh is the path the app never takes (it always passes a
/// dispatcher context), and no seam reaches it. The three settings sites
/// (saving the sidebar's organization, pins and shortcuts) catch ordinary I/O
/// errors and keep their text, which SidebarSettingsRetryTests pins by
/// prefix. ShellFailureDetailCensus holds all four to FailureDetail.Of.
/// Also here: Copy link is for exactly one Markdown file, as on the Mac; on a
/// Markdown file core makes no wikilink for it says the Mac's sentence
/// instead of nothing; and Copy link, Pin and Unpin do nothing on a row that
/// is not a file.
/// </summary>
public sealed class FilesSidebarFailureDetailTests
{
    /// <summary>A binding field token: a uniffi exception's Message, never
    /// core's sentence.</summary>
    private const string FieldToken = @"@\w+=";

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    // ------------------------------------------------ FilesSidebarViewModel

    /// <summary>The tag walk (LoadTags): every page core is asked for is
    /// refused as stale, past the walk's restarts.</summary>
    [Fact]
    public async Task ATagLoadCoreRefusesSaysCoresDetail()
    {
        using Rig rig = await Rig.Open("cb15a-tags", vault =>
        {
            vault.Write("a.md", "#alpha\n");
            vault.Write("b.md", "#beta\n");
        });
        string? cursor;
        using (var cancel = new CancelToken())
        {
            cursor = rig.Session.TagTreePage(new Paging(null, 1), cancel).NextCursor;
        }
        Assert.NotNull(cursor);
        _ = rig.Session.CreateExclusive("gamma.md", "#gamma\n");
        int pages = 0;
        rig.Sidebar.TagTreePageForTests = (paging, cancel) =>
        {
            Interlocked.Increment(ref pages);
            return rig.Session.TagTreePage(new Paging(cursor, paging.Limit), cancel);
        };

        rig.Sidebar.ShowTags = false;
        rig.Spoken.Clear();
        rig.Sidebar.ShowTags = true;
        await rig.Settle();

        Assert.True(pages > 1, $"premise: the walk restarted after core's refusal ({pages} page(s) asked).");
        rig.AssertReported("Could not load tags: tag tree page cursor is stale; restart from the first page");
    }

    [Fact]
    public async Task ACreateFolderCoreRefusesSaysCoresDetail()
    {
        using Rig rig = await UnderAFolderReplacedByAFile("cb15a-create-folder");

        rig.Sidebar.CreateFolderCommand.Execute(null);
        await rig.Settle();

        rig.AssertReported(
            "Could not create folder Untitled Folder: Invalid path sub/Untitled Folder: "
            + "\"sub\" is not a folder, so no folder was created");
    }

    [Fact]
    public async Task ACreateNoteCoreRefusesSaysCoresDetail()
    {
        using Rig rig = await UnderAFolderReplacedByAFile("cb15a-create-note");

        rig.Sidebar.CreateNoteCommand.Execute(null);
        await rig.Settle();

        rig.AssertReported(
            "Could not create note Untitled.md: Invalid path sub/Untitled.md: \"sub\" is not a folder, so nothing was created");
    }

    /// <summary>A new canvas's refusal is core's CanvasActionFailed event,
    /// announced as itself, whose detail now carries core's words.</summary>
    [Fact]
    public async Task ANewCanvasCoreRefusesSaysCoresDetail()
    {
        using Rig rig = await UnderAFolderReplacedByAFile("cb15a-new-canvas");

        rig.Sidebar.CreateCanvasCommand.Execute(null);
        await rig.Settle();

        const string detail =
            "Untitled Canvas.canvas: Invalid path sub/Untitled Canvas.canvas: \"sub\" is not a folder, so nothing was created";
        Assert.Equal("New canvas failed: " + detail, rig.Sidebar.Status);
        A11yEvent.Canvas said = Assert.Single(rig.Spoken.OfType<A11yEvent.Canvas>());
        Assert.Equal(new CanvasA11yEvent.CanvasActionFailed(CanvasFailedAction.NewCanvas, detail), said.Event);
        Assert.Equal(A11yPriority.High, SlateUniffiMethods.A11yRender(said).Priority);
        Assert.DoesNotContain(rig.Spoken.OfType<A11yEvent.HostComposed>(), item => item.Priority == A11yPriority.High);
        Assert.DoesNotMatch(FieldToken, rig.Sidebar.Status);
    }

    /// <summary>Delete: the file leaves the disk after core staged the trash
    /// and before the staged delete runs (TrashStagedForTesting).</summary>
    [Fact]
    public async Task ADeleteCoreRefusesSaysCoresDetail()
    {
        using Rig rig = await Rig.Open("cb15a-delete", vault =>
        {
            vault.Write("a.md", "# A\n");
            vault.Write("keep.md", "# Keep\n");
        });
        string file = Path.Combine(rig.Root, "a.md");
        rig.Sidebar.SelectedNode = rig.Node("a.md");
        rig.Sidebar.TrashStagedForTesting = _ => File.Delete(file);
        rig.Spoken.Clear();

        rig.Sidebar.DeleteCommand.Execute(null);
        await rig.Sidebar.TrashCompletion.WaitAsync(Wait);
        await rig.Settle();
        rig.Sidebar.TrashStagedForTesting = null;

        rig.AssertReported("Delete failed: Files changed or Trash confirmation expired. Request deletion again");
    }

    /// <summary>Create folder note: the note's name was taken on disk after
    /// the scan, and core's disk gate refuses it.</summary>
    [Fact]
    public async Task ACreateFolderNoteCoreRefusesSaysCoresDetail()
    {
        using Rig rig = await Rig.Open("cb15a-create-folder-note", vault => vault.Write("docs/other.md", "# Other\n"));
        FileTreeNodeViewModel docs = rig.Node("docs");
        Assert.False(docs.HasFolderNote, "premise: docs has no folder note when scanned.");
        rig.Sidebar.SelectedNode = docs;
        rig.Write("docs/docs.md", "# Written outside Slate\n");
        rig.Spoken.Clear();

        rig.Sidebar.CreateFolderNoteCommand.Execute(null);
        await rig.Settle();

        rig.AssertReported("Could not create folder note: Something named docs/docs.md already exists there.");
    }

    /// <summary>Delete folder note: the note left the disk underneath the
    /// session. The detail is the operating system's text (Io), so the oracle
    /// is core's detail for the same call made directly.</summary>
    [Fact]
    public async Task ADeleteFolderNoteCoreRefusesSaysCoresDetail()
    {
        using Rig rig = await Rig.Open("cb15a-delete-folder-note", vault =>
        {
            vault.Write("docs/docs.md", "# docs\n");
            vault.Write("docs/other.md", "# Other\n");
        });
        FileTreeNodeViewModel docs = rig.Node("docs");
        Assert.True(docs.HasFolderNote, "premise: docs/docs.md is the folder's note.");
        rig.Sidebar.SelectedNode = docs;
        File.Delete(Path.Combine(rig.Root, "docs", "docs.md"));
        rig.Spoken.Clear();

        rig.Sidebar.DeleteFolderNoteCommand.Execute(null);
        await rig.Settle();

        string detail = CoresDetail(() => rig.Session.DeleteFile("docs/docs.md"));
        rig.AssertReported("Could not delete folder note: " + detail);
    }

    /// <summary>Copy wikilink: core refuses only a malformed path. A file row's
    /// path is always well formed, and the rows whose path is empty (a
    /// date-group header, a placeholder) are not files, so Copy link is
    /// unavailable on them (CopyLinkPinAndUnpinDoNothingOnARowThatIsNotAFile).
    /// The row here is constructed: it drives the real catch with a real core
    /// refusal.</summary>
    [Fact]
    public async Task ACopyWikilinkCoreRefusesSaysCoresDetail()
    {
        using Rig rig = await Rig.Open("cb15a-copy-wikilink-refused", vault => vault.Write("a.md", "# A\n"));
        rig.Sidebar.SelectedNode = new FileTreeNodeViewModel(
            rig.Sidebar, @"Notes\Card.md", "Card.md", isDirectory: false, level: 1, hasChildren: false);
        rig.Spoken.Clear();

        rig.Sidebar.CopyWikilinkCommand.Execute(null);
        await rig.Settle();

        Assert.Empty(rig.Copied);
        rig.AssertReported(
            @"Could not copy wikilink: Invalid path Notes\Card.md: backslash separators are not allowed; vault paths use '/'");
    }

    /// <summary>Batch move: core's fault seam fails the marker plant after the
    /// batch's in-flight row is written.</summary>
    [Fact]
    public async Task ABatchMoveCoreRefusesSaysCoresDetail()
    {
        const string trigger = "zqcb15aplant";
        using Rig rig = await Rig.Open("cb15a-batch-move", vault =>
        {
            vault.Write($"{trigger}-one.md", "# One\n");
            vault.Write($"{trigger}-two.md", "# Two\n");
            vault.Write("sub/keep.md", "# Keep\n");
        });
        rig.Node($"{trigger}-one.md").IsBatchSelected = true;
        rig.Node($"{trigger}-two.md").IsBatchSelected = true;
        rig.Sidebar.MoveDestination = "sub";
        rig.Spoken.Clear();

        // The env lock is a Monitor, so it is released before any await: its
        // exit must run on the thread that took it (as FileManagementTests
        // does). The batch move runs inside Execute, so core has refused it
        // before the lock goes.
        using (EnvFaultLock.Acquire())
        {
            Environment.SetEnvironmentVariable("SLATE_TEST_FAULT_PLANT_MARKERS", trigger);
            try
            {
                rig.Sidebar.BatchMoveCommand.Execute(null);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SLATE_TEST_FAULT_PLANT_MARKERS", null);
            }
        }
        await rig.Settle();

        rig.AssertReported("Move failed: test fault: injected marker plant failure");
    }

    /// <summary>Batch trash: a checked file leaves the disk after core staged
    /// the trash (TrashStagedForTesting).</summary>
    [Fact]
    public async Task ABatchTrashCoreRefusesSaysCoresDetail()
    {
        using Rig rig = await Rig.Open("cb15a-batch-trash", vault =>
        {
            vault.Write("one.md", "# One\n");
            vault.Write("two.md", "# Two\n");
        });
        string two = Path.Combine(rig.Root, "two.md");
        rig.Node("one.md").IsBatchSelected = true;
        rig.Node("two.md").IsBatchSelected = true;
        rig.Sidebar.TrashStagedForTesting = _ => File.Delete(two);
        rig.Spoken.Clear();

        rig.Sidebar.BatchTrashCommand.Execute(null);
        await rig.Sidebar.TrashCompletion.WaitAsync(Wait);
        await rig.Settle();
        rig.Sidebar.TrashStagedForTesting = null;

        rig.AssertReported("Trash failed: Files changed or Trash confirmation expired. Request deletion again");
    }

    /// <summary>The dual pane's folder listing: a directory row whose path
    /// core refuses, constructed (no tree row carries one), selected with the
    /// dual pane on.</summary>
    [Fact]
    public async Task ALoadFolderFilesCoreRefusesSaysCoresDetail()
    {
        using Rig rig = await Rig.Open("cb15a-dual-pane", vault => vault.Write("a.md", "# A\n"));
        rig.Sidebar.IsDualPaneEnabled = true;
        await rig.Settle();
        var row = new FileTreeNodeViewModel(
            rig.Sidebar, "../outside", "outside", isDirectory: true, level: 1, hasChildren: false);
        rig.Spoken.Clear();

        rig.Sidebar.SelectedNode = row;
        await rig.Settle();

        rig.AssertReported(
            "Could not load folder files: Invalid path ../outside: parent-directory references (..) are not allowed");
    }

    // ------------------------------------ FilesSidebarViewModel.FileManagement

    /// <summary>Move To: the destination folder already holds a file of the
    /// moved file's name.</summary>
    [Fact]
    public async Task AMoveToCoreRefusesSaysCoresDetail()
    {
        using Rig rig = await Rig.Open("cb15a-move", vault =>
        {
            vault.Write("a.md", "# A\n");
            vault.Write("sub/a.md", "# Another A\n");
        });
        rig.Sidebar.SelectedNode = rig.Node("a.md");
        rig.Sidebar.MoveToCommand.Execute(null);
        await rig.Sidebar.MoveToCompletion.WaitAsync(Wait);
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(rig.Sidebar.MoveToSheet);
        rig.Spoken.Clear();

        picker.ActivateCommand.Execute(
            picker.Rows.Single(row => row.Kind == MoveToRowKind.Folder && row.Destination == "sub"));
        await rig.Settle();

        rig.AssertReported("Move failed: Something named sub/a.md already exists there.");
    }

    /// <summary>Move To's new folder: the typed name is a note's.</summary>
    [Fact]
    public async Task AMoveToNewFolderCoreRefusesSaysCoresDetail()
    {
        using Rig rig = await Rig.Open("cb15a-move-new-folder", vault =>
        {
            vault.Write("a.md", "# A\n");
            vault.Write("blocker.md", "# Blocker\n");
        });
        rig.Sidebar.SelectedNode = rig.Node("a.md");
        rig.Sidebar.MoveToCommand.Execute(null);
        await rig.Sidebar.MoveToCompletion.WaitAsync(Wait);
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(rig.Sidebar.MoveToSheet);
        picker.FilterText = "blocker.md";
        MoveToRowViewModel create = Assert.Single(picker.Rows, row => row.Kind == MoveToRowKind.NewFolder);
        rig.Spoken.Clear();

        picker.ActivateCommand.Execute(create);
        await rig.Settle();

        rig.AssertReported("Could not create folder blocker.md: Something named blocker.md already exists there.");
    }

    /// <summary>Duplicate: the file left the disk underneath the session, so
    /// its read is refused with the operating system's text (Io).</summary>
    [Fact]
    public async Task ADuplicateCoreRefusesSaysCoresDetail()
    {
        using Rig rig = await Rig.Open("cb15a-duplicate", vault => vault.Write("a.md", "# A\n"));
        rig.Sidebar.SelectedNode = rig.Node("a.md");
        File.Delete(Path.Combine(rig.Root, "a.md"));
        rig.Spoken.Clear();

        rig.Sidebar.DuplicateCommand.Execute(null);
        await rig.Settle();

        string detail = CoresDetail(() => _ = rig.Session.ReadText("a.md"));
        rig.AssertReported("Could not duplicate a.md: " + detail);
    }

    // ------------------------------------ FilesSidebarViewModel.TreeOperations

    /// <summary>The app's tree refresh (a dispatcher context and the tree
    /// worker): the worker seam first makes core's own listing call with a
    /// continuation the index has since outdated, and core's refusal faults
    /// the worker.</summary>
    [Fact]
    public async Task ATreeRefreshCoreRefusesSaysCoresDetail()
    {
        using var armed = new ManualResetEventSlim(false);
        string? staleCursor = null;
        using Rig rig = await Rig.Open(
            "cb15a-tree-refresh",
            vault =>
            {
                vault.Write("a.md", "# A\n");
                vault.Write("b.md", "# B\n");
            },
            session => (work, token) => Task.Run(
                () =>
                {
                    if (armed.IsSet)
                    {
                        using var cancel = new CancelToken();
                        _ = session.ListDirChildrenPage(string.Empty, new Paging(staleCursor, 1), cancel);
                    }
                    work();
                },
                token));
        using (var cancel = new CancelToken())
        {
            staleCursor = rig.Session.ListDirChildrenPage(string.Empty, new Paging(null, 1), cancel).NextCursor;
        }
        Assert.NotNull(staleCursor);
        _ = rig.Session.CreateExclusive("stale-maker.md", "# x\n");
        armed.Set();
        rig.Spoken.Clear();

        rig.Sidebar.Refresh();
        await rig.Settle();
        armed.Reset();

        rig.AssertReported("Could not load files: directory page cursor is stale; restart from the first page");
    }

    // ------------------------------- the canvas's and the graph's note creates

    /// <summary>A canvas's Convert Card to Note, through the sidebar's
    /// creator, with a typed path core refuses. The canvas flow is not driven:
    /// this asserts the result and core's render of the event the canvas
    /// builds from it (CanvasDocumentViewModel's NoteCreateFailed arm).</summary>
    [Fact]
    public async Task ACanvasNoteCoreRefusesCarriesCoresDetail()
    {
        using Rig rig = await Rig.Open("cb15a-canvas-note", vault => vault.Write("a.md", "# A\n"));
        const string typed = @"Notes\Card.md";

        CanvasNoteCreateResult result = ((ICanvasNoteCreator)rig.Sidebar).TryCreateNote(typed, "Card text\n");

        const string detail = @"Invalid path Notes\Card.md: backslash separators are not allowed; vault paths use '/'";
        CanvasNoteCreateResult.Failed failed = Assert.IsType<CanvasNoteCreateResult.Failed>(result);
        Assert.Equal(detail, failed.Message);
        RenderedAnnouncement said = SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(new CanvasA11yEvent.CanvasBlocked(
            new CanvasBlockedReason.NoteCreateFailed(typed, failed.Message))));
        Assert.Equal(@"Could not create Notes\Card.md: " + detail, said.Text);
        Assert.DoesNotMatch(FieldToken, said.Text);
    }

    /// <summary>A surface's new note whose name is taken: the Exists arm.
    /// GraphEndToEndTests drives the same arm through the Connections leaf;
    /// this asserts the result and core's render of the event the workspace
    /// builds from it (WorkspaceViewModel.GraphCreate's relay).</summary>
    [Fact]
    public async Task ASurfaceNoteWhoseNameIsTakenCarriesCoresDetail()
    {
        using Rig rig = await Rig.Open("cb15a-surface-exists", vault => vault.Write("Idea.md", "# Idea\n"));

        NoteCreateResult result = ((ISurfaceNoteCreator)rig.Sidebar).TryCreateNote("Idea.md", string.Empty);

        NoteCreateResult.Exists exists = Assert.IsType<NoteCreateResult.Exists>(result);
        Assert.Equal("Something named Idea.md already exists there.", exists.Message);
        Assert.Equal(
            "Couldn't create note: Something named Idea.md already exists there.",
            GraphRelay(exists.Message));
    }

    /// <summary>A surface's new note at a path core refuses (a dot-prefixed
    /// folder). The graph flow is not driven: this asserts the result and
    /// core's render of the event the workspace builds from it.</summary>
    [Fact]
    public async Task ASurfaceNoteCoreRefusesCarriesCoresDetail()
    {
        using Rig rig = await Rig.Open("cb15a-surface-failed", vault => vault.Write("a.md", "Links to [[.drafts/Idea]].\n"));

        NoteCreateResult result = ((ISurfaceNoteCreator)rig.Sidebar).TryCreateNote(".drafts/Idea.md", string.Empty);

        const string detail =
            "Invalid path .drafts/Idea.md: dot-prefixed path components are reserved for Slate/tool state and are never indexed";
        NoteCreateResult.Failed failed = Assert.IsType<NoteCreateResult.Failed>(result);
        Assert.Equal(detail, failed.Message);
        Assert.Equal("Couldn't create note: " + detail, GraphRelay(failed.Message));
    }

    // ------------------------------------------------------------ the tag edit

    /// <summary>The tag edit, through the workspace as the lifecycle wires it:
    /// core refuses a tag its normalizer leaves empty. The workspace composes
    /// the failure's text and the sidebar publishes it. No wait: the notes'
    /// chains are idle, so the coordinator runs the admitted edit inside the
    /// command (WorkspaceSaveCoordinator.Enqueue), the edit publishes before
    /// it returns (EditTagAtItsTurn runs the sidebar's publication as its last
    /// step), and its ticket completes there. When Execute returns, the
    /// operation has ended and its sentence has been said.</summary>
    [Fact]
    public async Task ATagEditCoreRefusesSaysCoresDetail()
    {
        using Rig rig = await Rig.Open("cb15a-tag-edit", vault => vault.Write("a.md", "# A\n"));
        using var workspace = new WorkspaceViewModel(rig.Session, rig.Root, () => [], rig.Spoken.Add);
        rig.Sidebar.TagEditRequested = workspace.EditTagAdmitted;
        rig.Node("a.md").IsBatchSelected = true;
        rig.Sidebar.TagInput = "#";
        Assert.True(workspace.SavesIdle, "premise: no save or operation is admitted before the tag edit.");
        rig.Spoken.Clear();

        rig.Sidebar.AddTagCommand.Execute(null);

        Assert.True(workspace.SavesIdle, "the tag edit's admitted operation had not ended when the command returned.");
        rig.AssertReported("Tag edit failed: \"#\" is not a valid tag.");
    }

    // ------------------------------------------------------------- Copy link

    /// <summary>The merge judge's minor 1: Copy link is for exactly one
    /// Markdown file, as on the Mac (SidebarActionCatalog's
    /// exactlyOneMarkdownFile). On a canvas or an image it is unavailable (the
    /// palette gives its generic reason), and run anyway it copies nothing and
    /// says nothing. It used to say "Could not create a wikilink for this
    /// file." there, where the Mac offers no action.</summary>
    [Theory]
    [InlineData("board.canvas")]
    [InlineData("picture.png")]
    public async Task CopyLinkIsUnavailableOnAFileThatIsNotMarkdown(string file)
    {
        using Rig rig = await Rig.Open("cb15a-copy-link-not-markdown", vault =>
        {
            vault.Write("a.md", "# A\n");
            vault.Write("board.canvas", SlateUniffiMethods.CanvasCanonicalEmptyText());
            vault.WriteBytes("picture.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        });
        FileTreeNodeViewModel row = rig.Node(file);
        Assert.NotNull(row.Summary);
        Assert.False(row.Summary.IsMarkdown, "premise: core's summary says the file is not Markdown.");
        rig.Sidebar.SelectedNode = row;
        string status = rig.Sidebar.Status;
        rig.Spoken.Clear();

        Assert.False(rig.Sidebar.CopyWikilinkCommand.CanExecute(null), $"Copy link is available on {file}.");
        rig.Sidebar.CopyWikilinkCommand.Execute(null);
        await rig.Settle();

        Assert.Empty(rig.Copied);
        Assert.Equal(status, rig.Sidebar.Status);
        Assert.Empty(rig.Spoken.OfType<A11yEvent.HostComposed>());
    }

    /// <summary>A Markdown file core makes no wikilink for (here, the note left
    /// the disk after the scan, so core's lookup finds no link) keeps Copy link
    /// available, and the command says why it copied nothing, in the Mac's
    /// sentence (AppState.swift's copy wikilink), once at High.</summary>
    [Fact]
    public async Task CopyLinkOnAMarkdownFileCoreMakesNoLinkForSaysWhy()
    {
        using Rig rig = await Rig.Open("cb15a-copy-link-no-link", vault =>
        {
            vault.Write("a.md", "# A\n");
            vault.Write("b.md", "# B\n");
        });
        FileTreeNodeViewModel row = rig.Node("a.md");
        Assert.True(row.Summary?.IsMarkdown, "premise: core's summary says the note is Markdown.");
        File.Delete(Path.Combine(rig.Root, "a.md"));
        Assert.Null(rig.Session.WikilinkForPath("a.md"));
        rig.Sidebar.SelectedNode = row;
        rig.Spoken.Clear();

        Assert.True(rig.Sidebar.CopyWikilinkCommand.CanExecute(null), "Copy link is unavailable on a Markdown file.");
        rig.Sidebar.CopyWikilinkCommand.Execute(null);
        await rig.Settle();

        Assert.Empty(rig.Copied);
        rig.AssertReported("Could not create a wikilink for this file.");
    }

    /// <summary>A Markdown row copies its link as before.</summary>
    [Fact]
    public async Task CopyLinkOnANoteCopiesItsLink()
    {
        using Rig rig = await Rig.Open("cb15a-copy-link-note", vault => vault.Write("a.md", "# A\n"));
        string? link = rig.Session.WikilinkForPath("a.md");
        Assert.NotNull(link);
        rig.Sidebar.SelectedNode = rig.Node("a.md");
        rig.Spoken.Clear();

        rig.Sidebar.CopyWikilinkCommand.Execute(null);
        await rig.Settle();

        Assert.Equal(link, Assert.Single(rig.Copied));
        Assert.Equal($"Copied wikilink for {rig.Node("a.md").DisplayName}.", rig.Sidebar.Status);
        Assert.DoesNotContain(rig.Spoken.OfType<A11yEvent.HostComposed>(), item => item.Priority == A11yPriority.High);
    }

    /// <summary>The diff red team's major 1 and minor 7: a date-group header
    /// and a placeholder are selectable rows whose path is empty. Copy link,
    /// Pin and Unpin are unavailable on them, as every sibling verb is, and
    /// run anyway (the palette's command, a stale state) they copy, speak,
    /// pin and unpin nothing. Copy link on one reached core with the empty
    /// path and spoke its refusal ("Could not copy wikilink: Invalid path :
    /// ..."), and Pin wrote the empty path into the pins.</summary>
    [Theory]
    [InlineData("a date-group header")]
    [InlineData("a placeholder")]
    public async Task CopyLinkPinAndUnpinDoNothingOnARowThatIsNotAFile(string kind)
    {
        using Rig rig = await Rig.Open("cb15a-not-a-file", vault => vault.Write("a.md", "# A\n"));
        FileTreeNodeViewModel row;
        if (kind == "a date-group header")
        {
            rig.Sidebar.GroupByDate = true;
            await rig.Settle();
            row = rig.Sidebar.RootNodes.First(node => node.IsGroupHeader);
        }
        else
        {
            row = FileTreeNodeViewModel.Loading();
        }
        Assert.Equal(string.Empty, row.Path);
        Assert.False(row.IsDirectory, "premise: the row is not a folder, so the old guard let it through.");
        rig.Sidebar.SelectedNode = row;
        string status = rig.Sidebar.Status;
        rig.Spoken.Clear();

        Assert.False(rig.Sidebar.CopyWikilinkCommand.CanExecute(null), $"Copy link is available on {kind}.");
        Assert.False(rig.Sidebar.PinCommand.CanExecute(null), $"Pin is available on {kind}.");
        Assert.False(rig.Sidebar.UnpinCommand.CanExecute(null), $"Unpin is available on {kind}.");

        rig.Sidebar.CopyWikilinkCommand.Execute(null);
        rig.Sidebar.PinCommand.Execute(null);
        rig.Sidebar.UnpinCommand.Execute(null);
        await rig.Settle();

        Assert.Empty(rig.Copied);
        Assert.DoesNotContain(string.Empty, rig.Sidebar.PinnedForTests);
        Assert.Equal(status, rig.Sidebar.Status);
        Assert.Empty(rig.Spoken.OfType<A11yEvent.HostComposed>());
    }

    // ================================================================ helpers

    private static async Task<Rig> UnderAFolderReplacedByAFile(string label)
    {
        Rig rig = await Rig.Open(label, vault =>
        {
            vault.Write("a.md", "# A\n");
            vault.Write("sub/inner.md", "# Inner\n");
        });
        // The folder is selected, then replaced on disk by a file of its name:
        // the index still lists the folder, and the verb creates inside it.
        rig.Sidebar.SelectedNode = rig.Node("sub");
        string folder = Path.Combine(rig.Root, "sub");
        Directory.Delete(folder, recursive: true);
        File.WriteAllText(folder, "a file where the folder was\n");
        rig.Spoken.Clear();
        return rig;
    }

    /// <summary>Core's detail for the refusal of a call made directly.</summary>
    private static string CoresDetail(Action call)
    {
        VaultException failure = Assert.ThrowsAny<VaultException>(call);
        string detail = SlateUniffiMethods.VaultErrorDetail(failure);
        Assert.False(string.IsNullOrWhiteSpace(detail), "premise: core gives a detail.");
        return detail;
    }

    private static string GraphRelay(string message) =>
        SlateUniffiMethods.A11yRender(new A11yEvent.Graph(
            new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.NoteCreateFailed(message)))).Text;

    /// <summary>A vault's files written before the scan.</summary>
    private sealed class VaultFiles(string root)
    {
        public void Write(string relative, string text) => File.WriteAllText(Prepared(relative), text);

        public void WriteBytes(string relative, byte[] bytes) => File.WriteAllBytes(Prepared(relative), bytes);

        private string Prepared(string relative)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return path;
        }
    }

    /// <summary>A scanned vault, its session and a sidebar on the app's
    /// path: a dispatcher context that runs posts on the pool and the tree
    /// worker.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly FixtureVault _fixture;
        private readonly FixtureVault _deviceState;

        private Rig(FixtureVault fixture, FixtureVault deviceState, VaultSession session)
        {
            _fixture = fixture;
            _deviceState = deviceState;
            Session = session;
        }

        public VaultSession Session { get; }

        public FilesSidebarViewModel Sidebar { get; private set; } = null!;

        public Spoken Spoken { get; } = new();

        public List<string> Copied { get; } = [];

        public string Root => _fixture.Root;

        public static async Task<Rig> Open(
            string label,
            Action<VaultFiles> files,
            Func<VaultSession, Func<Action, CancellationToken, Task>>? treeWorker = null)
        {
            FixtureVault fixture = FixtureVault.Create(0, label);
            files(new VaultFiles(fixture.Root));
            var session = VaultSession.OpenFilesystem(fixture.Root);
            using (var cancel = new CancelToken())
            {
                _ = session.ScanInitial(cancel);
            }
            var rig = new Rig(fixture, FixtureVault.Create(0, label + "-device"), session);
            rig.Sidebar = new FilesSidebarViewModel(
                session,
                rig.Spoken.Add,
                copyText: text =>
                {
                    lock (rig.Copied)
                    {
                        rig.Copied.Add(text);
                    }
                },
                vaultRoot: fixture.Root,
                localAppDataRoot: Path.Combine(rig._deviceState.Root, "device-state"),
                treeUiContext: new PumpSynchronizationContext(),
                treeWorker: treeWorker?.Invoke(session),
                writeJournal: new SidebarWriteJournal(Path.Combine(rig._deviceState.Root, "journal")));
            await rig.Settle();
            return rig;
        }

        public Task Settle() => Sidebar.TreeRefreshCompletion.WaitAsync(Wait);

        public FileTreeNodeViewModel Node(string path) =>
            Assert.Single(Sidebar.RootNodes, node => node.Path == path);

        public void Write(string relative, string text) => new VaultFiles(Root).Write(relative, text);

        /// <summary>The status line says <paramref name="sentence"/>, and so
        /// does the one High announcement, with no binding field token.</summary>
        public void AssertReported(string sentence)
        {
            Assert.Equal(sentence, Sidebar.Status);
            A11yEvent.HostComposed said = Assert.Single(
                Spoken.OfType<A11yEvent.HostComposed>(), item => item.Priority == A11yPriority.High);
            Assert.Equal(sentence, said.Text);
            Assert.DoesNotMatch(FieldToken, Sidebar.Status);
        }

        public void Dispose()
        {
            Session.Dispose();
            _fixture.Dispose();
            _deviceState.Dispose();
        }
    }

    /// <summary>Announcement sink safe against the pump's pool threads.</summary>
    private sealed class Spoken : IEnumerable<A11yEvent>
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

        public IEnumerator<A11yEvent> GetEnumerator()
        {
            A11yEvent[] snapshot;
            lock (_items)
            {
                snapshot = [.. _items];
            }
            return ((IEnumerable<A11yEvent>)snapshot).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class PumpSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) =>
            ThreadPool.QueueUserWorkItem(_ => d(state));
    }
}
