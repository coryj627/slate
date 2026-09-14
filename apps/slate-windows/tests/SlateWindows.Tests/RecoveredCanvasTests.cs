// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>Issue 1173: recovered content is readable through the real
/// native handle, while authoring and history cannot rewrite its source.</summary>
public sealed class RecoveredCanvasTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    private readonly string _recovered;
    private readonly List<RenderedAnnouncement> _announced = [];
    private readonly List<CanvasDocumentViewModel> _documents = [];

    public RecoveredCanvasTests()
    {
        _fixture = FixtureVault.Create(1, "recovered-canvas");
        _recovered = File.ReadAllText(Path.Combine(
            SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures",
            "canvas", "recovered-readonly.canvas"));
        File.WriteAllText(CanvasPath, _recovered);
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    private string CanvasPath => Path.Combine(_fixture.Root, "board.canvas");

    public void Dispose()
    {
        foreach (CanvasDocumentViewModel document in _documents)
        {
            document.Shutdown();
        }
        _session.Dispose();
        _fixture.Dispose();
    }

    private CanvasDocumentViewModel OpenDocument()
    {
        var document = new CanvasDocumentViewModel(
            _session, "board.canvas",
            new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
            synchronousForTests: true);
        _documents.Add(document);
        document.Load();
        return document;
    }

    private void RepairFile()
    {
        JsonNode source = JsonNode.Parse(_recovered)!;
        source["edges"] = new JsonArray();
        File.WriteAllText(CanvasPath, source.ToJsonString());
    }

    private static string ReadOnlyRefusal => CanvasAnnouncer.RenderLabel(
        new CanvasA11yEvent.CanvasMutationRefused(CanvasMutationRefusal.ReadOnly));

    [Fact]
    public void RecoveredContentSupportsNavigationFilterGroupAndTextReadback()
    {
        CanvasDocumentViewModel document = OpenDocument();
        Assert.Equal(CanvasLoadState.Ready, document.State);
        Assert.True(document.IsReadOnly);
        Assert.True(document.IsRecoveredReadOnly);
        Assert.Equal(3, document.Outline.Count);
        Assert.Equal(1, document.PreservedItemCount);
        Assert.Equal(document.Outline.Select(row => row.NodeId),
            document.TableRows.Select(row => row.NodeId));
        Assert.Null(document.EmptyOnboardingText);

        document.SeatSelectionSilently("recovered-group");
        document.Navigator.EnterGroup();
        Assert.Equal("recovered-text", document.Selection.Selected);
        document.Navigator.NextCard();
        Assert.Equal("recovered-file", document.Selection.Selected);
        document.Navigator.ExitGroup();
        Assert.Equal("recovered-group", document.Selection.Selected);

        document.FilterText = "Readable";
        Assert.Equal("recovered-text", Assert.Single(document.FilteredOutline).NodeId);
        document.SeatSelectionSilently("recovered-text");
        document.Navigator.WhereAmI();
        Assert.Contains("Readable original text", document.WhereAmIText);
        Assert.Contains("Recovered", document.WhereAmIText);
        Assert.Empty(document.NeighborsOf("recovered-text"));
        Assert.Equal("Readable original text", document.NodeTextOf("recovered-text"));
        Assert.True(document.TryChildrenOf("recovered-group", out IReadOnlyList<string> children));
        Assert.Equal(["recovered-text", "recovered-file"], children);
        document.FilterText = string.Empty;
        Assert.Equal(3, document.FilteredOutline.Count);
        Assert.Equal(_recovered, File.ReadAllText(CanvasPath));
    }

    [Fact]
    public void TwoPanesShareOneRecoveryNoticeAndItsAccessibleBannerText()
    {
        using var workspace = new WorkspaceViewModel(
            _session, _fixture.Root, () => [], _ => { },
            startInteractionBackgroundWork: false, announceRendered: _announced.Add);
        workspace.OpenPath("board.canvas");
        var first = Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(first.Canvas);
        ((ICommand)workspace.SplitRightCommand).Execute(null);
        workspace.OpenPath("board.canvas");
        Assert.Same(document,
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab).Canvas);

        document.AnnouncerForTests.FlushForTests();
        string expected = CanvasAnnouncer.RenderLabel(new CanvasA11yEvent.CanvasLoadedReadOnly(3));
        Assert.Equal(expected, document.ReadOnlyBannerText);
        Assert.Single(_announced, line => line.Text == expected);
        Assert.DoesNotContain(_announced, line => line.Text.Contains("unsupported", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecoveredReloadRefusesWritesConvertAndHistoryWithoutChangingTheirState(bool hasRedo)
    {
        RepairFile();
        CanvasDocumentViewModel document = OpenDocument();
        document.SeatSelectionSilently("recovered-text");
        Assert.NotNull(document.CanvasSetColor("1"));
        Assert.NotNull(document.UndoStack.OfferedUndo);
        if (hasRedo)
        {
            document.CanvasUndo();
            Assert.NotNull(document.UndoStack.OfferedRedo);
        }
        File.WriteAllText(CanvasPath, _recovered);
        document.Load();
        Assert.True(document.IsRecoveredReadOnly);
        Assert.True(hasRedo ? document.UndoStack.RedoQuarantined : document.UndoStack.UndoQuarantined);
        int epoch = document.UndoStack.Epoch;
        string? basis = document.PublishedBasis;
        var creator = new RefusedNoteCreator();
        document.AnnouncerForTests.FlushForTests();
        _announced.Clear();

        document.CanvasNewCard();
        document.CanvasDeleteSelection();
        Assert.Null(document.CanvasDuplicate());
        Assert.Null(document.CanvasSetColor("2"));
        document.CanvasCommitCardEdit("recovered-text", "must not land");
        Assert.Null(document.CanvasConvertToNote("recovered-text", "forbidden.md", creator));
        document.CanvasUndo();
        document.CanvasRedo();
        document.AnnouncerForTests.FlushForTests();

        Assert.NotEmpty(_announced);
        Assert.All(_announced, line => Assert.Equal(ReadOnlyRefusal, line.Text));
        Assert.Equal(0, creator.Calls);
        Assert.False(File.Exists(Path.Combine(_fixture.Root, "forbidden.md")));
        Assert.Equal(epoch, document.UndoStack.Epoch);
        Assert.Equal(basis, document.PublishedBasis);
        Assert.True(hasRedo ? document.UndoStack.RedoQuarantined : document.UndoStack.UndoQuarantined);
        Assert.Equal(3, document.Outline.Count);
        Assert.Equal(_recovered, File.ReadAllText(CanvasPath));
    }

    [Fact]
    public void RecoveredSelectionAndMarksCannotOpenAuthoringPrompts()
    {
        CanvasDocumentViewModel document = OpenDocument();
        int prompts = 0;
        document.GroupRenameRequested += (_, _) => prompts++;
        document.SetColorRequested += () => prompts++;
        document.ColorMarkedRequested += () => prompts++;
        document.GroupMarkedRequested += () => prompts++;
        document.SeatSelectionSilently("recovered-group");
        document.ToggleMark();
        document.AnnouncerForTests.FlushForTests();
        _announced.Clear();

        document.RequestGroupRenameForSelection();
        document.RequestSetColor();
        document.RequestColorMarked();
        document.RequestGroupMarked();
        document.AnnouncerForTests.FlushForTests();

        Assert.Equal(0, prompts);
        Assert.Equal(4, _announced.Count);
        Assert.All(_announced, line => Assert.Equal(ReadOnlyRefusal, line.Text));
        Assert.Equal(_recovered, File.ReadAllText(CanvasPath));

        RepairFile();
        document.Load();
        document.RequestGroupRenameForSelection();
        document.RequestSetColor();
        document.RequestColorMarked();
        document.RequestGroupMarked();
        Assert.Equal(4, prompts);
    }

    [Fact]
    public void FreshInspectionCannotWriteEvenAfterTheDocumentIsRepaired()
    {
        CanvasDocumentViewModel document = OpenDocument();
        CanvasCardEditorViewModel inspection = Assert.IsType<CanvasCardEditorViewModel>(
            document.OpenCardEditor("recovered-text"));
        Assert.True(inspection.InspectionOnly);
        Assert.Equal("Readable original text", inspection.Draft);
        Assert.Contains("read-only", inspection.TextName);
        Assert.Contains("Escape closes", inspection.EditorHint);
        inspection.Draft = "a caller cannot turn inspection into an edit";
        Assert.True(inspection.CommitOnEscape());
        Assert.Equal(_recovered, File.ReadAllText(CanvasPath));

        RepairFile();
        document.Load();
        Assert.False(document.IsReadOnly);
        string repaired = File.ReadAllText(CanvasPath);
        int epoch = document.UndoStack.Epoch;
        Assert.True(inspection.CommitOnEscape());
        Assert.Equal(repaired, File.ReadAllText(CanvasPath));
        Assert.Equal(epoch, document.UndoStack.Epoch);
        Assert.Equal("Readable original text", document.NodeTextOf("recovered-text"));
    }

    [Fact]
    public void AnExistingDirtyDraftSurvivesTheReadOnlyReloadAndRefusedCommit()
    {
        RepairFile();
        CanvasDocumentViewModel document = OpenDocument();
        CanvasCardEditorViewModel editor = Assert.IsType<CanvasCardEditorViewModel>(
            document.OpenCardEditor("recovered-text"));
        Assert.False(editor.InspectionOnly);
        editor.Draft = "The user's only unsaved copy";
        string seedBasis = editor.SeedBasis;
        File.WriteAllText(CanvasPath, _recovered);
        document.Load();
        int epoch = document.UndoStack.Epoch;
        document.AnnouncerForTests.FlushForTests();
        _announced.Clear();

        Assert.False(editor.CommitOnEscape());
        Assert.Equal("The user's only unsaved copy", editor.Draft);
        Assert.Equal(seedBasis, editor.SeedBasis);
        Assert.False(editor.Conflicted);
        Assert.Equal(epoch, document.UndoStack.Epoch);
        Assert.Equal(_recovered, File.ReadAllText(CanvasPath));
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal(ReadOnlyRefusal, Assert.Single(_announced).Text);
    }

    [Fact]
    public void ADirtyDraftSurvivesMutationAdmissionRefusalAndCanRetry() => RunSta(() =>
    {
        RepairFile();
        CanvasDocumentViewModel document = OpenDocument();
        document.SeatSelectionSilently("recovered-text");
        CanvasCardEditorViewModel editor = Assert.IsType<CanvasCardEditorViewModel>(
            document.OpenCardEditor("recovered-text"));
        editor.Draft = "Retained until authoring is admitted";
        string seedBasis = editor.SeedBasis;
        string source = File.ReadAllText(CanvasPath);
        int epoch = document.UndoStack.Epoch;
        var surface = new CanvasSurfaceView { Model = document };
        document.Navigator.AttachPresenter(surface);
        Assert.True(document.Navigator.EnterMoveMode());
        Assert.True(document.Modes.IsActive);

        Assert.False(document.IsReadOnly);
        Assert.False(editor.CommitOnEscape());
        Assert.Equal("Retained until authoring is admitted", editor.Draft);
        Assert.Equal(seedBasis, editor.SeedBasis);
        Assert.Equal(source, File.ReadAllText(CanvasPath));
        Assert.Equal(epoch, document.UndoStack.Epoch);
        Assert.Null(document.UndoStack.OfferedUndo);
        Assert.Null(document.UndoStack.OfferedRedo);
        Assert.True(document.Modes.IsActive);

        Assert.True(document.Navigator.CancelMode());
        Assert.False(document.Modes.IsActive);
        Assert.True(editor.CommitOnEscape());
        Assert.Equal(editor.Draft, document.NodeTextOf("recovered-text"));
        Assert.NotNull(document.UndoStack.OfferedUndo);
    });

    [Fact]
    public void ReloadCanStayRecoveredBecomeEditableAndBecomeRecoveredAgain()
    {
        CanvasDocumentViewModel document = OpenDocument();
        string recoveryNotice = document.ReadOnlyBannerText!;
        document.Load();
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal(2, _announced.Count(line => line.Text == recoveryNotice));

        RepairFile();
        document.Load();
        Assert.Equal(CanvasLoadState.Ready, document.State);
        Assert.False(document.IsReadOnly);
        Assert.Null(document.ReadOnlyBannerText);
        document.SeatSelectionSilently("recovered-text");
        Assert.NotNull(document.CanvasSetColor("1"));
        Assert.NotNull(document.UndoStack.OfferedUndo);

        File.WriteAllText(CanvasPath, _recovered);
        document.Load();
        Assert.True(document.IsRecoveredReadOnly);
        Assert.Equal(recoveryNotice, document.ReadOnlyBannerText);
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal(3, _announced.Count(line => line.Text == recoveryNotice));
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("{\"nodes\":[],\"edges\":{}}")]
    public void UnavailableContentKeepsTheErrorSurfaceInsteadOfPretendingToRecover(string source)
    {
        File.WriteAllText(CanvasPath, source);
        CanvasDocumentViewModel document = OpenDocument();
        Assert.Equal(CanvasLoadState.ParseError, document.State);
        Assert.True(document.IsReadOnly);
        Assert.False(document.IsRecoveredReadOnly);
        Assert.Empty(document.Outline);
        Assert.Null(document.ReadOnlyBannerText);
        Assert.Null(document.EmptyOnboardingText);
        Assert.Equal(source, File.ReadAllText(CanvasPath));
    }

    [Fact]
    public void MountedRecoveryBannerAndRetryRemainReachableAndRetryLoadsTheRepair() => RunSta(() =>
    {
        CanvasDocumentViewModel document = OpenDocument();
        var surface = new CanvasSurfaceView { Model = document };
        var window = new Window
        {
            Content = surface,
            Width = 900,
            Height = 700,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None,
        };
        try
        {
            window.Show();
            window.UpdateLayout();
            PumpDispatcher();
            Assert.True(surface.ReadOnlyBannerForTests.IsVisible);
            Assert.True(surface.ReadOnlyBannerForTests.Focusable);
            Assert.True(KeyboardNavigation.GetIsTabStop(surface.ReadOnlyBannerForTests));
            Assert.Equal(document.ReadOnlyBannerText, surface.ReadOnlyBannerForTests.Text);
            AutomationPeer banner = UIElementAutomationPeer.CreatePeerForElement(surface.ReadOnlyBannerForTests)!;
            Assert.Equal(document.ReadOnlyBannerText, banner.GetName());
            Assert.True(surface.RetryLoadForTests.IsVisible);
            Assert.True(surface.RetryLoadForTests.IsEnabled);
            Assert.True(surface.RetryLoadForTests.IsTabStop);
            AutomationPeer retry = UIElementAutomationPeer.CreatePeerForElement(surface.RetryLoadForTests)!;
            Assert.Equal("Retry opening canvas", retry.GetName());
            IInvokeProvider invoke = Assert.IsAssignableFrom<IInvokeProvider>(retry.GetPattern(PatternInterface.Invoke));
            Assert.Equal("CanvasRetryLoad", AutomationProperties.GetAutomationId(surface.RetryLoadForTests));
            Assert.True(surface.OutlineForTests.IsVisible);
            Assert.False(surface.OnboardingForTests.IsVisible);
            Assert.NotEmpty(surface.WarningRowsForTests.Items);

            RepairFile();
            invoke.Invoke();
            PumpDispatcher();
            window.UpdateLayout();
            Assert.False(document.IsReadOnly);
            Assert.False(surface.ReadOnlyBannerForTests.IsVisible);
            Assert.False(surface.RetryLoadForTests.IsVisible);
            Assert.True(surface.OutlineForTests.IsVisible);
        }
        finally
        {
            window.Close();
            document.Shutdown();
        }
    });

    [Fact]
    public void AlreadyBoundCloseButtonEnablesWhenInspectionOpensAndClosesIt() => RunSta(() =>
    {
        using var workspace = new WorkspaceViewModel(
            _session, _fixture.Root, () => [], _ => { },
            startInteractionBackgroundWork: false, announceRendered: _announced.Add);
        var close = new Button { Content = "Close" };
        _ = close.SetBinding(Button.CommandProperty,
            new Binding(nameof(WorkspaceViewModel.CloseCanvasInspectionCommand)) { Source = workspace });
        var window = new Window
        {
            Content = close,
            Width = 300,
            Height = 150,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None,
        };
        try
        {
            window.Show();
            window.UpdateLayout();
            PumpDispatcher();
            Assert.Null(workspace.CanvasCardEditorSheet);
            Assert.False(close.IsEnabled);
            workspace.OpenPath("board.canvas");
            CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(
                Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab).Canvas);
            document.SeatSelectionSilently("recovered-text");

            workspace.OpenCanvasCardEditor();

            Assert.True(Assert.IsType<CanvasCardEditorViewModel>(workspace.CanvasCardEditorSheet).InspectionOnly);
            Assert.True(close.IsEnabled);
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(close)!;
            IInvokeProvider invoke = Assert.IsAssignableFrom<IInvokeProvider>(peer.GetPattern(PatternInterface.Invoke));
            invoke.Invoke();
            PumpDispatcher();
            Assert.Null(workspace.CanvasCardEditorSheet);
            Assert.False(close.IsEnabled);
            Assert.Equal(_recovered, File.ReadAllText(CanvasPath));
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public void AcceptedRecoveredLeaseStaysOwnedUntilReplacementAndTeardown()
    {
        var source = new CanvasFakeLoadSource { Disposition = CanvasLoadDisposition.RecoveredReadOnly };
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var pipeline = new CanvasLoadPipeline(slot, source);
        Assert.Equal(CanvasLoadOutcome.Accepted, pipeline.Deliver(pipeline.Request()!));
        CanvasHandleLease first = Assert.IsType<CanvasHandleLease>(slot.Current.Lease);
        Assert.False(first.IsClosed);
        Assert.Equal(CanvasLoadState.Ready, slot.Current.LoadState);
        Assert.Equal(CanvasLoadDisposition.RecoveredReadOnly, slot.Current.Population!.Disposition);
        Assert.Equal(2, slot.Current.Population.Count);
        Assert.Equal(0, source.TotalCloses);

        Assert.Equal(CanvasLoadOutcome.Accepted, pipeline.Deliver(pipeline.Request()!));
        Assert.True(first.IsClosed);
        Assert.Equal(1, source.TotalCloses);
        Assert.Equal(CanvasLoadDisposition.RecoveredReadOnly, slot.Current.Population!.Disposition);
        _ = slot.Publish(current => current.WithRetired());
        CanvasHandleLease terminal = Assert.IsType<CanvasHandleLease>(CanvasLeaseTransfer.Terminalize(slot));
        Assert.True(terminal.Close());
        Assert.False(terminal.Close());
        Assert.Equal(2, source.TotalCloses);
    }

    [Fact]
    public void FailedRecoveryPopulationReadClosesItsUnpublishedHandleOnce()
    {
        var source = new CanvasFakeLoadSource
        {
            Disposition = CanvasLoadDisposition.RecoveredReadOnly,
            ReadFault = new InvalidOperationException("recovered outline failed"),
        };
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var pipeline = new CanvasLoadPipeline(slot, source);
        Assert.Equal(CanvasLoadOutcome.Faulted, pipeline.Deliver(pipeline.Request()!));
        Assert.Equal(1, source.Opens);
        Assert.Equal(1, source.TotalCloses);
        Assert.Null(slot.Current.Loaded);
        Assert.Equal(CanvasLoadState.Failed, slot.Current.LoadState);
    }

    [Fact]
    public void SupersededRecoveredLoadClosesOnlyItsOwnHandleAndLeavesRetryPending()
    {
        var source = new CanvasFakeLoadSource { Disposition = CanvasLoadDisposition.RecoveredReadOnly };
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var probe = new CanvasLoadProbeForTests();
        var pipeline = new CanvasLoadPipeline(slot, source, probeForTests: probe);
        CanvasLoadRequest first = pipeline.Request()!;
        CanvasLoadRequest? retry = null;
        probe.OnPoint = point =>
        {
            if (point == CanvasLoadPoint.Built && retry is null)
            {
                retry = pipeline.Request();
            }
        };
        Assert.Equal(CanvasLoadOutcome.Refused, pipeline.Deliver(first));
        Assert.Equal(1, source.TotalCloses);
        Assert.Null(slot.Current.Loaded);
        Assert.NotNull(retry);
        Assert.True(slot.Current.Loads.Admits(retry.Identity));
        probe.OnPoint = null;
        Assert.Equal(CanvasLoadOutcome.Accepted, pipeline.Deliver(retry));
        Assert.Equal(1, source.TotalCloses);
        Assert.Equal(CanvasLoadDisposition.RecoveredReadOnly, slot.Current.Population!.Disposition);
        _ = slot.Publish(current => current.WithRetired());
        Assert.True(CanvasLeaseTransfer.Terminalize(slot)!.Close());
        Assert.Equal(2, source.TotalCloses);
    }

    private sealed class RefusedNoteCreator : ICanvasNoteCreator
    {
        internal int Calls { get; private set; }

        public CanvasNoteCreateResult TryCreateNote(string path, string content)
        {
            Calls++;
            return new CanvasNoteCreateResult.Failed("read-only admission must run first");
        }

        public void NoteLanded(string path, string? caveat) =>
            throw new InvalidOperationException("A recovered canvas cannot create a note.");
    }

    private static void PumpDispatcher()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Recovered canvas STA test timed out.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
