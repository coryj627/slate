// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using System.Windows.Threading;
using SlateWindows.Canvas;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-15b (#1890): every canvas failure sentence built from a caught
/// failure says core's detail, never the binding's field dump
/// (<c>@message=…</c>, <c>@path=…</c>). One fact per read the shell census
/// kept for the canvas, each driving the read's real catch with a real core
/// refusal over a real vault. Where ordinary input reaches the refusal, the
/// fact uses it (a canvas file that is not UTF-8, one that is held or
/// read-only, a locked index); elsewhere it constructs the state core refuses:
/// the canvas's core handle closed behind the document's lease, which still
/// names it (bad_handle), or one core apply around the funnel, so core loses
/// a node the rows still hold (bad_node). Each oracle is core's own words: a
/// literal where core's text is fixed and locale-free, or core's renderer
/// over the same core call made directly where it holds a handle number or
/// operating-system text.
/// </summary>
/// <remarks>
/// <c>CanvasRemoveFromGroup</c> has no fact here. Its catch wraps only
/// <c>CanvasPlaceNew</c>, which refuses nothing but bad_handle, and
/// <c>CanvasParentOf</c> runs first on the same handle in the same lease hold,
/// so only a close that bypasses the lease's lock in the middle of that hold
/// reaches it: a race the lease forbids in production. Its read goes through
/// <c>FailureDetail.Of</c> like the others, and ShellFailureDetailCensus holds
/// it there (the lead's ruling on CB-15b's Call 8).
/// </remarks>
public sealed class CanvasFailureDetailTests : IDisposable
{
    private const string Board =
        """
        {
          "nodes": [
            {"id":"grp","type":"group","x":-40,"y":-40,"width":560,"height":400,"label":"Research"},
            {"id":"question","type":"text","text":"# Core question\nCan it be accessible?","x":0,"y":0,"width":240,"height":140,"color":"1"},
            {"id":"evidence","type":"text","text":"Evidence so far","x":260,"y":0,"width":220,"height":140},
            {"id":"note","type":"file","file":"note0.md","subpath":"#Note 0","x":0,"y":180,"width":240,"height":140},
            {"id":"link","type":"link","url":"https://example.org/spec","x":640,"y":0,"width":240,"height":140},
            {"id":"loose","type":"text","text":"Unfiled thought","x":0,"y":460,"width":200,"height":100}
          ],
          "edges": [
            {"id":"e1","fromNode":"question","fromSide":"right","toNode":"evidence","toSide":"left","label":"supports"}
          ]
        }
        """;

    /// <summary>A binding field token: a uniffi exception's Message, never
    /// core's sentence.</summary>
    private const string FieldToken = @"@\w+=";

    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    private readonly List<RenderedAnnouncement> _announced = [];

    public CanvasFailureDetailTests()
    {
        _fixture = FixtureVault.Create(3, "canvas-failure-detail");
        File.WriteAllText(Path.Combine(_fixture.Root, "board.canvas"), Board);
        // Not UTF-8: a lone 0xFF byte inside otherwise valid JSON.
        byte[] lead = System.Text.Encoding.UTF8.GetBytes("{\"nodes\":[{\"id\":\"a\",\"type\":\"text\",\"text\":\"");
        byte[] tail = System.Text.Encoding.UTF8.GetBytes("\",\"x\":0,\"y\":0,\"width\":10,\"height\":10}],\"edges\":[]}");
        File.WriteAllBytes(Path.Combine(_fixture.Root, "bad.canvas"), [.. lead, 0xFF, .. tail]);
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    private CanvasDocumentViewModel Open(string path = "board.canvas")
    {
        var document = new CanvasDocumentViewModel(
            _session,
            path,
            new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
            synchronousForTests: true);
        document.Load();
        return document;
    }

    private WorkspaceViewModel NewWorkspace() =>
        new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            _ => { },
            startInteractionBackgroundWork: false,
            announceRendered: _announced.Add).WithShellLandings();

    private static ulong HandleOf(CanvasDocumentViewModel document)
    {
        ulong handle = 0;
        Assert.True(document.AppliedPublication!.Loaded!.Lease.Invoke(() => true, h => handle = h));
        return handle;
    }

    /// <summary>bad_handle: core's handle closed behind the lease, which
    /// still names it, so the document's admission passes and core
    /// refuses.</summary>
    private ulong CloseBehindTheLease(CanvasDocumentViewModel document)
    {
        ulong handle = HandleOf(document);
        _session.CloseCanvas(handle);
        return handle;
    }

    /// <summary>bad_node: one core apply on the document's own handle, around
    /// the funnel, so core's model loses the node the rows still hold.</summary>
    private void ApplyBehindTheDocument(CanvasDocumentViewModel document, CanvasOp op) =>
        _ = _session.CanvasApply(HandleOf(document), new CanvasAction("behind the document", [op]));

    private static string Render(CanvasA11yEvent @event) =>
        SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(@event)).Text;

    /// <summary>Core's words for an unknown handle, through core's renderer
    /// over the same refusal made directly.</summary>
    private static string BadHandle(Func<object> call) =>
        SlateUniffiMethods.VaultErrorDetail(Assert.Throws<VaultException.InvalidArgument>(call));

    /// <summary>The document spoke exactly the action-failed sentence with
    /// core's detail, at High, and no field token.</summary>
    private void AssertSaid(CanvasDocumentViewModel document, CanvasA11yEvent expected)
    {
        document.AnnouncerForTests.FlushForTests();
        RenderedAnnouncement said = Assert.Single(_announced);
        Assert.Equal(Render(expected), said.Text);
        Assert.Equal(A11yPriority.High, said.Priority);
        Assert.DoesNotMatch(FieldToken, said.Text);
    }

    [Fact]
    public void WhereAmIOnACardCoreNoLongerHoldsSaysCoresDetail()
    {
        CanvasDocumentViewModel document = Open();
        ApplyBehindTheDocument(document, new CanvasOp.DeleteNode("loose"));
        document.SeatSelectionSilently("loose");
        _announced.Clear();

        document.Navigator.WhereAmI();

        AssertSaid(document, new CanvasA11yEvent.CanvasActionFailed(
            CanvasFailedAction.WhereAmI, "canvas node \"loose\" not found in this canvas"));
        document.Shutdown();
    }

    [Fact]
    public void ANewCardCoreRefusesSaysCoresDetail()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("question");
        ulong handle = CloseBehindTheLease(document);
        _announced.Clear();

        document.CanvasNewCard();

        AssertSaid(document, new CanvasA11yEvent.CanvasActionFailed(
            CanvasFailedAction.NewCard, BadHandle(() => _session.CanvasPlaceNew(handle, "question", 260, 140, null, []))));
        document.Shutdown();
    }

    [Fact]
    public void ANewGroupCoreRefusesSaysCoresDetail()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("loose");
        ulong handle = CloseBehindTheLease(document);
        _announced.Clear();

        _ = document.CanvasNewGroup("Probe");

        AssertSaid(document, new CanvasA11yEvent.CanvasActionFailed(
            CanvasFailedAction.NewGroup, BadHandle(() => _session.CanvasPlaceNew(handle, "loose", 400, 300, null, []))));
        document.Shutdown();
    }

    [Fact]
    public void AMoveIntoAGroupCoreNoLongerHoldsSaysCoresDetail()
    {
        CanvasDocumentViewModel document = Open();
        ApplyBehindTheDocument(document, new CanvasOp.Ungroup("grp"));
        document.SeatSelectionSilently("loose");
        _announced.Clear();

        _ = document.CanvasMoveIntoGroup("grp");

        AssertSaid(document, new CanvasA11yEvent.CanvasActionFailed(
            CanvasFailedAction.MoveIntoGroup, "canvas node \"grp\" not found in this canvas"));
        document.Shutdown();
    }

    [Fact]
    public void AFileCardCoreRefusesSaysCoresDetail()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("loose");
        ulong handle = CloseBehindTheLease(document);
        _announced.Clear();

        _ = document.CanvasAddFileCard("note1.md", null);

        AssertSaid(document, new CanvasA11yEvent.CanvasActionFailed(
            CanvasFailedAction.Create, BadHandle(() => _session.CanvasPlaceNew(handle, "loose", 260, 140, null, []))));
        document.Shutdown();
    }

    [Fact]
    public void ALinkCardCoreRefusesSaysCoresDetail()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("loose");
        ulong handle = CloseBehindTheLease(document);
        _announced.Clear();

        _ = document.CanvasAddLinkCard("https://example.org/probe");

        AssertSaid(document, new CanvasA11yEvent.CanvasActionFailed(
            CanvasFailedAction.Create, BadHandle(() => _session.CanvasPlaceNew(handle, "loose", 260, 140, null, []))));
        document.Shutdown();
    }

    [Fact]
    public void AConnectedCardCoreRefusesSaysCoresDetail()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("loose");
        ulong handle = CloseBehindTheLease(document);
        _announced.Clear();

        _ = document.CanvasCreateConnectedCard();

        AssertSaid(document, new CanvasA11yEvent.CanvasActionFailed(
            CanvasFailedAction.CreateConnectedCard,
            BadHandle(() => _session.CanvasPlaceNew(handle, "loose", 260, 140, CanvasPlaceDirection.Below, []))));
        document.Shutdown();
    }

    [Fact]
    public void ADuplicateCoreRefusesSaysCoresDetail()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("loose");
        ulong handle = CloseBehindTheLease(document);
        _announced.Clear();

        _ = document.CanvasDuplicate();

        AssertSaid(document, new CanvasA11yEvent.CanvasActionFailed(
            CanvasFailedAction.Duplicate, BadHandle(() => _session.CanvasOrderNodes(handle, ["loose"]))));
        document.Shutdown();
    }

    [Fact]
    public void ConvertingACardCoreNoLongerHoldsSaysCoresDetail()
    {
        CanvasDocumentViewModel document = Open();
        var creator = new UnreachedCreator();
        ApplyBehindTheDocument(document, new CanvasOp.DeleteNode("loose"));
        _announced.Clear();

        _ = document.CanvasConvertToNote("loose", "Unfiled thought.md", creator);

        AssertSaid(document, new CanvasA11yEvent.CanvasBlocked(
            new CanvasBlockedReason.NoteReadFailed("canvas node \"loose\" not found in this canvas")));
        Assert.Equal(0, creator.Calls);
        document.Shutdown();
    }

    private sealed class UnreachedCreator : ICanvasNoteCreator
    {
        public int Calls { get; private set; }

        public CanvasNoteCreateResult TryCreateNote(string path, string content)
        {
            Calls++;
            return new CanvasNoteCreateResult.Unavailable();
        }

        public void NoteLanded(string path, string? caveat) => Calls++;
    }

    /// <summary>The vault picker's load is core's file list, which refuses
    /// only a SQLite failure. Every WAL read-lock byte of the index's
    /// shared-memory file is held by a second handle, the lock another SQLite
    /// user would hold, so core's read cannot begin: the list returns
    /// SQLITE_PROTOCOL once SQLite's own retry loop gives up (about 10 s).
    /// The fact waits for that answer, never on a clock of its own; the
    /// timer only releases the locks if SQLite never answers.</summary>
    [Fact]
    public void AVaultPickerOverALockedIndexSaysCoresDetail()
    {
        CanvasDocumentViewModel document = Open();
        string shm = Path.Combine(_fixture.Root, ".slate", "cache.sqlite-shm");
        Assert.True(File.Exists(shm), "premise: the index runs in WAL mode with a shared-memory file");
        _announced.Clear();

        using (var held = new FileStream(shm, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            // WAL_READ_LOCK(0..4): bytes 123..127 (the shm lock base 120, plus 3).
            held.Lock(123, 5);
            using var release = new System.Threading.Timer(
                _ =>
                {
                    // A callback queued as the stream closes meets a disposed
                    // stream; on a pool thread that must not end the run.
                    try
                    {
                        held.Unlock(123, 5);
                    }
                    catch (Exception unlock) when (unlock is IOException or ObjectDisposedException)
                    {
                    }
                },
                null,
                TimeSpan.FromSeconds(60),
                Timeout.InfiniteTimeSpan);
            try
            {
                document.RequestVaultPick(CanvasVaultPickPurpose.Note);
            }
            finally
            {
                try
                {
                    held.Unlock(123, 5);
                }
                catch (Exception unlock) when (unlock is IOException or ObjectDisposedException)
                {
                }
            }
        }

        AssertSaid(document, new CanvasA11yEvent.CanvasActionFailed(
            CanvasFailedAction.Create, "sqlite error: locking protocol"));
        document.Shutdown();
    }

    [Fact]
    public void DeletingMarkedCardsCoreRefusesSaysCoresDetail()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("loose");
        document.ToggleMark();
        document.SeatSelectionSilently("evidence");
        document.ToggleMark();
        ulong handle = CloseBehindTheLease(document);
        _announced.Clear();

        document.CanvasDeleteMarked();

        AssertSaid(document, new CanvasA11yEvent.CanvasActionFailed(
            CanvasFailedAction.CanvasAction, BadHandle(() => _session.CanvasOrderNodes(handle, ["loose", "evidence"]))));
        document.Shutdown();
    }

    [Fact]
    public void GroupingMarkedCardsCoreRefusesSaysCoresDetail()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("loose");
        document.ToggleMark();
        document.SeatSelectionSilently("link");
        document.ToggleMark();
        ulong handle = CloseBehindTheLease(document);
        _announced.Clear();

        _ = document.SubmitGroupMarked("Probe");

        AssertSaid(document, new CanvasA11yEvent.CanvasActionFailed(
            CanvasFailedAction.NewGroup, BadHandle(() => _session.CanvasOrderNodes(handle, ["loose", "link"]))));
        document.Shutdown();
    }

    /// <summary>A canvas whose bytes are not UTF-8, opened as the Files
    /// sidebar opens one: the failed state's banner says core's
    /// detail.</summary>
    [Fact]
    public void ACanvasThatIsNotUtf8SaysCoresDetailInItsBanner()
    {
        using WorkspaceViewModel workspace = NewWorkspace();

        workspace.OpenPath("bad.canvas");

        CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(workspace.ActiveGroup.ActiveTab!.Canvas);
        Assert.Equal(CanvasLoadState.Failed, document.State);
        Assert.Equal(
            "This canvas could not be opened: File at bad.canvas is not valid UTF-8. (bad.canvas)",
            document.StateMessage);
    }

    /// <summary>A canvas another program holds with no sharing: the banner
    /// says the operating system's words, which core passes through.</summary>
    [Fact]
    public void AHeldCanvasSaysCoresDetailInItsBanner()
    {
        string boardPath = Path.Combine(_fixture.Root, "board.canvas");
        using (new FileStream(boardPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            CanvasDocumentViewModel held = Open();
            Assert.Equal(CanvasLoadState.Failed, held.State);
            string detail = SlateUniffiMethods.VaultErrorDetail(
                Assert.ThrowsAny<VaultException>(() => _session.OpenCanvas("board.canvas")));
            Assert.Equal($"This canvas could not be opened: {detail} (board.canvas)", held.StateMessage);
            Assert.DoesNotMatch(FieldToken, held.StateMessage!);
            held.Shutdown();
        }
    }

    /// <summary>A canvas that is not UTF-8, renamed: the retarget's reopen
    /// refuses the same way, and the banner says core's detail after the
    /// move. (That the banner speaks of a previous snapshot when the canvas
    /// never opened is #2019.)</summary>
    [Fact]
    public void ARenamedCanvasThatIsNotUtf8SaysCoresDetailInItsBanner()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("bad.canvas");
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        File.Move(Path.Combine(_fixture.Root, "bad.canvas"), Path.Combine(_fixture.Root, "renamed.canvas"));

        workspace.RetargetPath("bad.canvas", "renamed.canvas");

        CanvasDocumentViewModel after = Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        Assert.Equal(CanvasLoadState.RetargetAbsent, after.State);
        Assert.Equal(
            CanvasAnnouncer.RenderLabel(new CanvasA11yEvent.CanvasBlocked(new CanvasBlockedReason.ReopenFailed(
                "bad.canvas moved to renamed.canvas: File at renamed.canvas is not valid UTF-8."))),
            after.StateMessage);
    }

    /// <summary>A canvas marked read-only on disk: New Card's apply is
    /// refused, and the sentence says the operating system's words, which
    /// core passes through.</summary>
    [Fact]
    public void ANewCardOnAReadOnlyCanvasSaysCoresDetail()
    {
        string boardPath = Path.Combine(_fixture.Root, "board.canvas");
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("loose");
        File.SetAttributes(boardPath, FileAttributes.ReadOnly);
        try
        {
            _announced.Clear();

            document.CanvasNewCard();

            string detail = SlateUniffiMethods.VaultErrorDetail(Assert.Throws<VaultException.Io>(() => _session.CanvasApply(
                HandleOf(document),
                new CanvasAction("direct", [new CanvasOp.CreateNode("direct-node", new CanvasNodeContent.Text(string.Empty), 4000, 4000, 100, 100, null)]))));
            AssertSaid(document, new CanvasA11yEvent.CanvasActionFailed(CanvasFailedAction.CanvasAction, detail));
        }
        finally
        {
            File.SetAttributes(boardPath, FileAttributes.Normal);
        }
        document.Shutdown();
    }

    /// <summary>A card edit on a card core no longer holds: core's apply
    /// rejects the action whole, and the sentence says core's
    /// detail.</summary>
    [Fact]
    public void AnEditCoreRejectsSaysCoresDetail()
    {
        CanvasDocumentViewModel document = Open();
        ApplyBehindTheDocument(document, new CanvasOp.DeleteNode("loose"));
        _announced.Clear();

        _ = document.CanvasCommitCardEdit("loose", "Edited");

        AssertSaid(document, new CanvasA11yEvent.CanvasActionFailed(
            CanvasFailedAction.CanvasAction, "canvas action \"edit \\\"Unfiled thought\\\"\" rejected: unknown node \"loose\""));
        document.Shutdown();
    }

    /// <summary>Plan 47 CB-15c, Find 4: the board opened in a workspace, as
    /// the Files sidebar opens one, with the <c>loose</c> card removed in core
    /// behind the document, so its row still stands.</summary>
    private CanvasDocumentViewModel OpenWithTheLooseCardGoneInCore(WorkspaceViewModel workspace)
    {
        workspace.OpenPath("board.canvas");
        CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(workspace.ActiveGroup.ActiveTab!.Canvas);
        Assert.Equal(CanvasLoadState.Ready, document.State);
        document.SeatSelectionSilently("loose");
        ApplyBehindTheDocument(document, new CanvasOp.DeleteNode("loose"));
        document.AnnouncerForTests.FlushForTests();
        _announced.Clear();
        return document;
    }

    /// <summary>The card editor did not open, and the document said, once,
    /// the canvas's existing sentence for a card whose text cannot be
    /// read.</summary>
    private void AssertTheCardTextIsUnreadable(WorkspaceViewModel workspace, CanvasDocumentViewModel document)
    {
        Assert.Null(workspace.CanvasCardEditorSheet);
        document.AnnouncerForTests.FlushForTests();
        RenderedAnnouncement said = Assert.Single(_announced);
        RenderedAnnouncement expected = SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
            new CanvasA11yEvent.CanvasBlocked(new CanvasBlockedReason.CardTextUnreadable())));
        Assert.Equal(expected.Text, said.Text);
        Assert.Equal(expected.Priority, said.Priority);
        Assert.DoesNotMatch(FieldToken, said.Text);
    }

    /// <summary>Find 4: a row's Edit Card Text on a card core no longer holds
    /// (its deletion committed in core before its publication applied here).
    /// Core's refusal of the editor's seed read is caught: nothing escapes to
    /// the dispatcher, no editor opens, and the canvas says the card's text
    /// cannot be read.</summary>
    [Fact]
    public void RowActivationOnACardCoreNoLongerHoldsSaysItsTextIsUnreadable()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        CanvasDocumentViewModel document = OpenWithTheLooseCardGoneInCore(workspace);

        Exception? thrown = Record.Exception(() => document.RequestCardEditor("loose"));

        Assert.Null(thrown);
        AssertTheCardTextIsUnreadable(workspace, document);
    }

    /// <summary>Find 4 through the palette: Canvas: Edit Card Text… on the
    /// selected card core no longer holds, invoked through the palette's own
    /// path. The command runs and fails no action: no editor opens, and the
    /// canvas says the card's text cannot be read.</summary>
    [Fact]
    public void ThePalettesEditCardTextOnACardCoreNoLongerHoldsSaysItsTextIsUnreadable()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        CanvasDocumentViewModel document = OpenWithTheLooseCardGoneInCore(workspace);
        var host = new WorkspaceCommandHost(workspace);
        Assert.Null(SlateCommandRegistrar.DisabledReason(host, ChordTable.Ids.CanvasEditCard));
        using var registry = new CommandRegistry();
        SlateCommandRegistrar.RegisterAll(registry, host, Dispatcher.CurrentDispatcher);

        Exception? thrown = Record.Exception(() => registry.InvokeById(ChordTable.Ids.CanvasEditCard));

        Assert.Null(thrown);
        AssertTheCardTextIsUnreadable(workspace, document);
    }

    /// <summary>The palette's command host over one workspace; the shell's
    /// own commands do nothing here.</summary>
    private sealed class WorkspaceCommandHost(WorkspaceViewModel workspace) : ISlateCommandHost
    {
        private static readonly ICommand Inert = new RelayCommand(_ => { }, _ => true);

        public WorkspaceViewModel? Workspace => workspace;

        public FilesSidebarViewModel? FileSidebar => null;

        public QuickSwitcherViewModel? QuickSwitcher => null;

        public ICommand OpenVaultCommand => Inert;

        public ICommand CloseVaultCommand => Inert;

        public ICommand ToggleSearchCommand => Inert;

        public ICommand FindInNoteCommand => Inert;

        public ICommand OpenSettingsCommand => Inert;

        public ICommand SwitchThemeCommand => Inert;

        public bool IsVaultOpen => true;
    }
}
