using System.Windows.Input;
// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using SlateWindows.Tests.Support;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 §E TE-5b: the first REAL-VAULT verbs through the funnel —
/// model mutation on disk, the typed confirmation, and the inverse
/// restoring the exact prior bytes (the C-unit bar, per verb).
/// </summary>
public sealed class CanvasMutationTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    private readonly List<RenderedAnnouncement> _announced = [];

    public CanvasMutationTests()
    {
        _fixture = FixtureVault.Create(2, "canvas-mutation");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "board.canvas"),
            """
            {
            	"nodes":[
            		{"id":"a","type":"text","text":"Alpha","x":0,"y":0,"width":260,"height":140},
            		{"id":"grp","type":"group","label":"Ideas","x":600,"y":0,"width":400,"height":300},
            		{"id":"cramped","type":"group","label":"Cramped","x":1200,"y":0,"width":50,"height":40},
            		{"id":"blocker","type":"text","text":"Blocker","x":1220,"y":40,"width":260,"height":140},
            		{"id":"essay","type":"text","text":"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx","x":5000,"y":0,"width":260,"height":140}
            	],
            	"edges":[
            		{"id":"e1","fromNode":"a","fromSide":"right","toNode":"blocker","toSide":"left","color":"2","label":"feeds"}
            	]
            }

            """);
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    private CanvasDocumentViewModel Open()
    {
        var document = new CanvasDocumentViewModel(
            _session,
            "board.canvas",
            new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
            synchronousForTests: true);
        document.Load();
        return document;
    }

    private string DiskBytes() =>
        File.ReadAllText(Path.Combine(_fixture.Root, "board.canvas"));

    /// <summary>§E TE-11: the REAL verb - every per-verb undo fact
    /// now runs the funnel's checkout path end to end, so a regression
    /// anywhere in gate, checkout, apply or refresh fails the verb's
    /// own fact rather than slipping past test plumbing.</summary>
    private static void Undo(CanvasDocumentViewModel document) =>
        document.CanvasUndo();

    private static ulong HandleOf(CanvasDocumentViewModel document)
    {
        ulong handle = 0;
        CanvasHandleLease lease = document.AppliedPublication!.Loaded!.Lease;
        Assert.True(lease.Invoke(() => true, h => handle = h));
        return handle;
    }

    /// <summary>§E TE-11 (ED-1): undo and redo as VERBS - Ctrl+Z's
    /// target. The receipt crosses stacks: undo restores the prior
    /// bytes and speaks core's Undid sentence; redo re-lands the write
    /// and speaks Redid; the redo pile survives the undo (the verb
    /// path's clear-redo rule must not fire here).</summary>
    [Fact]
    public void UndoAndRedoVerbsRestoreBytesAndSpeakTheHistorySentence()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();
        document.CanvasNewCard();
        string after = DiskBytes();
        Assert.NotEqual(before, after);

        document.CanvasUndo();
        Assert.Equal(before, DiskBytes());
        Assert.Contains(
            _announced, a => a.Text.Contains("Undid", StringComparison.Ordinal));

        document.CanvasRedo();
        Assert.Equal(after, DiskBytes());
        Assert.Contains(
            _announced, a => a.Text.Contains("Redid", StringComparison.Ordinal));
        document.Shutdown();
    }

    /// <summary>§E TE-11: the empty-stack arms speak the status
    /// sentence - never silence (E8a's never-silent table).</summary>
    [Fact]
    public void HistoryVerbsOnEmptyStacksSpeakTheStatusArms()
    {
        CanvasDocumentViewModel document = Open();
        document.CanvasUndo();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("Nothing to undo.", StringComparison.Ordinal));
        document.CanvasRedo();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("Nothing to redo.", StringComparison.Ordinal));
        document.Shutdown();
    }

    /// <summary>§E TE-11 (ED-1/IE-9): an undo against a disk that
    /// moved WITHOUT a reload hits the write conflict, the entry
    /// returns exactly where it was, and the blocked arm speaks -
    /// never silence, never a lost entry.</summary>
    [Fact]
    public void UndoAgainstAMovedDiskBlocksAndRetainsTheEntry()
    {
        CanvasDocumentViewModel document = Open();
        document.CanvasNewCard();
        Assert.NotNull(document.UndoStack.SnapshotUndo());

        // The disk moves under the entry - an external editor, no
        // reload observed yet.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "board.canvas"),
            DiskBytes().Replace("\"nodes\"", "\"nodes\" ", StringComparison.Ordinal));

        document.CanvasUndo();

        Assert.Contains(
            _announced,
            a => a.Text.Contains("blocked", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(document.UndoStack.SnapshotUndo());
        document.Shutdown();
    }

    /// <summary>§E TE-11c (E8a): the never-silent table's verb
    /// cells - every pre-funnel exit a surface can reach speaks its
    /// exact existing arm. "Returns without deleting ALWAYS has a
    /// sentence."</summary>
    [Fact]
    public void EveryReachableGuardExitSpeaksItsCell()
    {
        CanvasDocumentViewModel document = Open();
        document.SelectNode(null, announce: false);
        _announced.Clear();

        // No selection: the acting verbs' shared cell.
        document.CanvasDeleteSelection();
        document.CanvasSetColor("1");
        document.CanvasMoveIntoGroup("grp");
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal(3, CountOf("Nothing selected."));

        // Unknown group: the group verbs' cell.
        _announced.Clear();
        document.CanvasUngroup("no-such-group");
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal(1, CountOf("not a group"));

        // A vanished endpoint or card: gone is gone.
        _announced.Clear();
        document.CanvasConnect("ghost", "also-ghost", null);
        document.CanvasLocateFile("ghost", "note0.md");
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal(2, CountOf("Nothing selected."));
        document.Shutdown();
    }

    /// <summary>§E TE-11c: the verbs' no-basis short-circuit speaks
    /// the SAME typed refusal the funnel's ladder would - one shared
    /// derivation, exercised through a still-loading document.</summary>
    [Fact]
    public void AVerbOnAnUnreadyDocumentSpeaksTheRefusal()
    {
        // The ctor WITHOUT Load(): the publication stays Loading.
        CanvasDocumentViewModel document =
            new CanvasDocumentViewModel(
            _session,
            "board.canvas",
            new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
            synchronousForTests: true);
        document.CanvasNewCard();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("still opening", StringComparison.Ordinal));
        document.Shutdown();
    }

    private int CountOf(string fragment) =>
        _announced.Count(a => a.Text.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    /// <summary>§F TF-2 (F1/F2a): the holder captures reading
    /// order and a TOTAL bijection under one never-silent read - a
    /// member without scene geometry builds NOTHING.</summary>
    [Fact]
    public void TheHolderCapturesReadingOrderAndRefusesAGhost()
    {
        CanvasDocumentViewModel document = Open();
        CanvasLoaded loaded = document.CurrentLoadedForModeEntry!;
        CanvasTransientHolder holder = Assert.IsType<CanvasTransientHolder>(
            CanvasTransientHolder.TryCapture(
                _session, loaded, ["blocker", "a"], isResize: false));
        Assert.Equal(holder.Ids.Length, holder.Originals.Count);
        Assert.All(holder.Ids, id => Assert.True(holder.Originals.ContainsKey(id)));
        Assert.Same(loaded, holder.Identity);

        Assert.Null(
            CanvasTransientHolder.TryCapture(
                _session, loaded, ["a", "ghost"], isResize: false));
        document.Shutdown();
    }

    /// <summary>§F TF-2 (IF-1): the identity is the LOADED triple -
    /// a selection publish swaps the publication but keeps the triple,
    /// and the mode STANDS; a reload installs a new triple and F1a
    /// cancels with the machine's own restoration.</summary>
    [Fact]
    public void ASelectionPublishKeepsTheModeAndAReloadCancelsIt()
    {
        CanvasDocumentViewModel document = Open();
        CanvasLoaded loaded = document.CurrentLoadedForModeEntry!;
        var spec = new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("A"),
            () => CanvasModeCommitResult.Committed(),
            () => new CanvasModeRestoration.BackAt("a"));
        var pane = new object();
        Assert.True(document.Modes.Enter(spec, pane));
        document.InstallTransient(
            CanvasTransientHolder.TryCapture(
                _session, loaded, ["a"], isResize: false)!);

        document.SelectNode("blocker", announce: false);
        Assert.True(document.Modes.IsActive);
        Assert.NotNull(document.Transient);

        document.Load();
        Assert.False(document.Modes.IsActive);
        Assert.Null(document.Transient);
        document.Shutdown();
    }

    /// <summary>§F TF-2 (IF-2): the own-commit exemption - while a
    /// commit is PENDING the completion is the one arbiter, so the
    /// commit's own refresh must not cancel the mode it completes.</summary>
    [Fact]
    public void TheModesOwnPendingCommitDoesNotCancelItself()
    {
        CanvasDocumentViewModel document = Open();
        CanvasLoaded loaded = document.CurrentLoadedForModeEntry!;
        var id = new object();
        var spec = new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("A"),
            () => CanvasModeCommitResult.Pending(id),
            () => new CanvasModeRestoration.BackAt("a"));
        Assert.True(document.Modes.Enter(spec, new object()));
        document.InstallTransient(
            CanvasTransientHolder.TryCapture(
                _session, loaded, ["a"], isResize: false)!);
        Assert.False(document.Modes.Commit());

        // The commit's refresh: a verb through the funnel republishes
        // a NEW Loaded while the commit is pending - the watcher must
        // stand down.
        document.CanvasSetColor("2");
        Assert.True(document.Modes.IsActive);
        Assert.NotNull(document.Transient);

        document.Modes.ResolveCommit(id, CanvasModeCommitResult.Committed());
        Assert.False(document.Modes.IsActive);
        document.DiscardTransient();
        document.Shutdown();
    }

    /// <summary>§G TG-6 (G6/GD-4): a mark write during a held mode is
    /// FREE and never alters the holder — the captured set stands,
    /// cancel leaves the marks alone, and only the next entry reads
    /// the new set.</summary>
    [Fact]
    public void MarkWritesDuringAHeldModeNeverAlterTheHolder()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        document.SeatSelectionSilently("grp");
        document.ToggleMark();
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());
        Assert.Equal(2, document.Transient!.Ids.Length);

        document.SeatSelectionSilently("blocker");
        document.ToggleMark();

        Assert.Equal(3, document.AppliedPublication!.MarkedIntent.Count);
        Assert.Equal(2, document.Transient!.Ids.Length);
        Assert.True(document.Modes.Cancel());
        Assert.Equal(3, document.AppliedPublication!.MarkedIntent.Count);
        Assert.True(document.Navigator.EnterMoveMode());
        Assert.Equal(3, document.Transient!.Ids.Length);
        Assert.True(document.Modes.Cancel());
        document.Shutdown();
    }

    /// <summary>§G TG-6 (G6, F4d): a bulk verb under a held mode is a
    /// funnel verb — ModeHeld, the ModeBusy sentence, nothing written,
    /// the marks and the mode standing.</summary>
    [Fact]
    public void BulkVerbsDuringAHeldModeRefuseModeHeld()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());
        string before = DiskBytes();
        _announced.Clear();

        document.CanvasDeleteMarked();

        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("in progress", StringComparison.Ordinal));
        Assert.Equal(before, DiskBytes());
        Assert.Contains("a", document.AppliedPublication!.MarkedIntent);
        Assert.True(document.Modes.IsActive);
        Assert.True(document.Modes.Cancel());
        document.Shutdown();
    }

    /// <summary>§G TG-6 (G6, TF-10's column): under suspension a bulk
    /// verb answers the ladder's ConflictPending — the conflict
    /// sentence, nothing written.</summary>
    [Fact]
    public void BulkVerbsUnderSuspensionAnswerTheConflictSentence()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());
        Assert.True(document.Navigator.ModeStep(1, 0, large: false));
        File.WriteAllText(
            Path.Combine(_fixture.Root, "board.canvas"),
            DiskBytes() + "\n");
        Assert.False(document.Modes.Commit());
        Assert.True(document.Funnel.ModeSuspended);
        string before = DiskBytes();
        _announced.Clear();

        document.CanvasDeleteMarked();

        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("changed on disk", StringComparison.Ordinal));
        Assert.Equal(before, DiskBytes());
        Assert.True(document.Modes.Cancel());
        document.Shutdown();
    }

    /// <summary>§G TG-6 (G6): a mode commit KEEPS the marks — the
    /// moving set survives its own move.</summary>
    [Fact]
    public void AModeCommitKeepsTheMarks()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        document.SeatSelectionSilently("grp");
        document.ToggleMark();
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());
        Assert.True(document.Navigator.ModeStep(1, 0, large: false));

        Assert.True(document.Modes.Commit());

        Assert.False(document.Modes.IsActive);
        Assert.Equal(2, document.AppliedPublication!.MarkedIntent.Count);
        document.Shutdown();
    }

    /// <summary>§G TG-6 (G8/GD-7): undo of a bulk verb speaks core's
    /// render verbatim and applies the one inverse — colors restored,
    /// a created group removed — while the marks are NOT history.</summary>
    [Fact]
    public void UndoOfABulkVerbSpeaksCoresRenderAndRestores()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        document.SeatSelectionSilently("blocker");
        document.ToggleMark();
        document.CanvasColorMarked("2");
        _announced.Clear();

        document.CanvasUndo();

        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("Undid: color 2 cards", StringComparison.Ordinal));
        CanvasPopulation restored = document.AppliedPublication!.Loaded!.Population;
        Assert.Null(restored.SceneByNode["a"].Color);
        Assert.Equal(2, document.AppliedPublication!.MarkedIntent.Count);

        var prompt = (CanvasGroupMarkedPrompt)CanvasPromptViewModel.GroupMarked(document);
        prompt.Draft = "Bundle";
        Assert.Equal(CanvasPromptSubmit.Pending, prompt.Submit(() => { }));
        Assert.Empty(document.AppliedPublication!.MarkedIntent);
        _announced.Clear();

        document.CanvasUndo();

        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("Undid: group 2 cards", StringComparison.Ordinal));
        Assert.DoesNotContain(
            document.AppliedPublication!.Loaded!.Population.SceneNodes,
            n => n.Kind == "group" && n.Title == "Bundle");
        Assert.Empty(document.AppliedPublication!.MarkedIntent);
        document.Shutdown();
    }

    /// <summary>§G TG-5 (G5/IG-21): Group Marked wraps the set in ONE
    /// group at core's padded frame — every CreateGroup argument from
    /// the returned rect — named by CountNoun, the captured marks
    /// leaving with the refreshed rows, the sentence naming the label.</summary>
    [Fact]
    public void GroupMarkedWrapsTheSetInOneGroup()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        document.SeatSelectionSilently("blocker");
        document.ToggleMark();
        CanvasPopulation before = document.AppliedPublication!.Loaded!.Population;
        CanvasSceneNode a = before.SceneByNode["a"];
        CanvasSceneNode blocker = before.SceneByNode["blocker"];
        var prompt = (CanvasGroupMarkedPrompt)CanvasPromptViewModel.GroupMarked(document);
        prompt.Draft = "Bundle";

        Assert.Equal(CanvasPromptSubmit.Pending, prompt.Submit(() => { }));

        CanvasPopulation after = document.AppliedPublication!.Loaded!.Population;
        CanvasSceneNode group = Assert.Single(
            after.SceneNodes, n => n.Kind == "group" && n.Title == "Bundle");
        Assert.True(group.X <= Math.Min(a.X, blocker.X));
        Assert.True(group.Y <= Math.Min(a.Y, blocker.Y));
        Assert.True(group.X + group.Width >= Math.Max(a.X + a.Width, blocker.X + blocker.Width));
        Assert.True(group.Y + group.Height >= Math.Max(a.Y + a.Height, blocker.Y + blocker.Height));
        Assert.Equal("group 2 cards", document.UndoStack.OfferedUndo!.Name);
        Assert.Empty(document.AppliedPublication!.MarkedIntent);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("Grouped 2 cards", StringComparison.Ordinal)
                && x.Text.Contains("Bundle", StringComparison.Ordinal));
        document.Shutdown();
    }

    /// <summary>§G TG-5 (G5): an empty label is an UNLABELED group —
    /// null on disk, "Untitled" in the sentence.</summary>
    [Fact]
    public void AnEmptyLabelMeansAnUnlabeledGroup()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        var prompt = (CanvasGroupMarkedPrompt)CanvasPromptViewModel.GroupMarked(document);

        Assert.Equal(CanvasPromptSubmit.Pending, prompt.Submit(() => { }));

        string disk = DiskBytes().Replace(" ", "").Replace("\t", "");
        Assert.DoesNotContain("\"label\":\"\"", disk);
        CanvasPopulation after = document.AppliedPublication!.Loaded!.Population;
        Assert.Contains(after.SceneNodes, n => n.Kind == "group" && n.NodeId != "grp" && n.NodeId != "cramped");
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("Untitled", StringComparison.Ordinal));
        document.Shutdown();
    }

    /// <summary>§G TG-5 (G7): the front door refuses NoMarks on an
    /// empty store and opens nothing.</summary>
    [Fact]
    public void GroupMarkedRefusesAtTheDoorWithoutMarks()
    {
        CanvasDocumentViewModel document = Open();
        bool requested = false;
        document.GroupMarkedRequested += () => requested = true;

        document.RequestGroupMarked();

        Assert.False(requested);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("No marks", StringComparison.Ordinal));
        document.Shutdown();
    }

    /// <summary>§G TG-4 (G5/GD-2/G8): Delete Marked is ONE action over
    /// the reading-ordered set — a marked group goes by the algebra's
    /// one removal — named by CountNoun, the captured marks removed
    /// with the refreshed rows, the selection dropped by resolution;
    /// one undo restores the structure and the marks stay cleared
    /// (marks are not history).</summary>
    [Fact]
    public void DeleteMarkedIsOneActionAndUndoRestoresIt()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        document.SeatSelectionSilently("grp");
        document.ToggleMark();
        string before = DiskBytes();

        document.CanvasDeleteMarked();

        string after = DiskBytes();
        Assert.DoesNotContain("\"id\":\"a\"", after.Replace(" ", ""));
        Assert.DoesNotContain("\"id\":\"grp\"", after.Replace(" ", ""));
        Assert.Equal("delete 2 cards", document.UndoStack.OfferedUndo!.Name);
        Assert.Empty(document.AppliedPublication!.MarkedIntent);
        Assert.Null(document.Selection.Selected);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("Deleted 2 cards", StringComparison.Ordinal));

        document.CanvasUndo();
        Assert.Contains("\"id\":\"a\"", DiskBytes().Replace(" ", ""));
        Assert.Contains("\"id\":\"grp\"", DiskBytes().Replace(" ", ""));
        Assert.Empty(document.AppliedPublication!.MarkedIntent);
        Assert.NotEqual(before, after);
        document.Shutdown();
    }

    /// <summary>§G TG-4 (G5): Color Marked colors every marked node in
    /// one action and KEEPS the marks; the typed color speaks.</summary>
    [Fact]
    public void ColorMarkedKeepsTheMarks()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        document.SeatSelectionSilently("blocker");
        document.ToggleMark();

        document.CanvasColorMarked("2");

        // Typed, per node - the fixture's edge e1 carries color 2 too,
        // so a byte count would read three.
        CanvasPopulation population = document.AppliedPublication!.Loaded!.Population;
        Assert.Equal("2", population.SceneByNode["a"].Color);
        Assert.Equal("2", population.SceneByNode["blocker"].Color);
        Assert.Equal("color 2 cards", document.UndoStack.OfferedUndo!.Name);
        Assert.Equal(2, document.AppliedPublication!.MarkedIntent.Count);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("Set 2 cards", StringComparison.Ordinal));
        document.Shutdown();
    }

    /// <summary>§G TG-4 (G5/G7): an EMPTY projection — the store holds
    /// only a ghost — refuses NoMarks after admission, under the gate;
    /// nothing writes and the ghost stays.</summary>
    [Fact]
    public void AnEmptyProjectionRefusesNoMarksAfterAdmission()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("essay");
        document.ToggleMark();
        document.CanvasDeleteSelection();
        Assert.Contains("essay", document.AppliedPublication!.MarkedIntent);
        string before = DiskBytes();
        CanvasHistoryEntry? offered = document.UndoStack.OfferedUndo;
        _announced.Clear();

        document.CanvasDeleteMarked();

        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("No marks", StringComparison.Ordinal));
        Assert.Equal(before, DiskBytes());
        Assert.Same(offered, document.UndoStack.OfferedUndo);
        Assert.Contains("essay", document.AppliedPublication!.MarkedIntent);
        document.Shutdown();
    }

    /// <summary>§G TG-4 (GD-5, IG-55): the Color Marked prompt carries
    /// NO count in its title and routes its choice to the bulk verb.</summary>
    [Fact]
    public void TheColorMarkedPromptRoutesToTheBulkVerbWithoutACount()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        var prompt = (CanvasSetColorPrompt)CanvasPromptViewModel.SetColorMarked(document);
        Assert.True(prompt.Marked);
        Assert.DoesNotContain(prompt.Title, char.IsDigit);

        prompt.SelectedChoice = prompt.Choices[2];
        Assert.Equal(CanvasPromptSubmit.Pending, prompt.Submit(() => { }));

        Assert.Contains("\"color\":\"3\"", DiskBytes().Replace(" ", "").Replace("\t", ""));
        Assert.Contains("a", document.AppliedPublication!.MarkedIntent);
        document.Shutdown();
    }

    /// <summary>§G TG-3 (GD-7/IG-16): the mark effect lands in the SAME
    /// publication as the refreshed rows — the one that carries the
    /// write's new geometry already carries the removed marks — and
    /// applies once.</summary>
    [Fact]
    public void TheMarkEffectAppliesWithTheRefreshedRows()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        document.SeatSelectionSilently("grp");
        document.ToggleMark();
        CanvasPublication before = document.AppliedPublication!;
        Assert.Equal(2, before.MarkedIntent.Count);
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("color captured"),
            document,
            "a",
            before.Loaded!,
            CanvasMutationEffect.KeepSelection,
            markEffect: CanvasMarkEffect.RemoveCaptured,
            capturedMarks: before.MarkEpochs);
        CanvasPublication? carrier = null;
        document.PublicationApplied += applied =>
        {
            if (carrier is null && !ReferenceEquals(applied.Loaded, before.Loaded))
            {
                carrier = applied;
            }
        };

        Assert.Equal(
            CanvasMutationAdmission.Admitted,
            document.Funnel.Apply(
                operation,
                _ => new CanvasAction("color", [new CanvasOp.SetNodeColor("a", "2")]),
                "color"));

        Assert.NotNull(carrier);
        Assert.Empty(carrier!.MarkedIntent);
        Assert.Empty(document.Selection.Marked);
        Assert.True(operation.MarkEffectApplied);
        document.Shutdown();
    }

    /// <summary>§G TG-3 (IG-45): epochs — an id unmarked and RE-MARKED
    /// after the capture is a newer write and survives RemoveCaptured;
    /// the untouched captured id leaves.</summary>
    [Fact]
    public void ARemarkedIdSurvivesRemoveCaptured()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        document.SeatSelectionSilently("grp");
        document.ToggleMark();
        CanvasPublication captured = document.AppliedPublication!;
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("color captured"),
            document,
            "a",
            captured.Loaded!,
            CanvasMutationEffect.KeepSelection,
            markEffect: CanvasMarkEffect.RemoveCaptured,
            capturedMarks: captured.MarkEpochs);

        // A later local write: "a" unmarked and marked again — a new epoch.
        _ = document.Unmark("a");
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        Assert.NotEqual(captured.MarkEpochs["a"], document.AppliedPublication!.MarkEpochs["a"]);

        // The basis moved with those publishes? No — mark publishes keep
        // the loaded triple (IF-1), so the operation is still current.
        Assert.Equal(
            CanvasMutationAdmission.Admitted,
            document.Funnel.Apply(
                operation,
                _ => new CanvasAction("color", [new CanvasOp.SetNodeColor("a", "3")]),
                "color"));

        Assert.True(document.Selection.IsMarked("a"));
        Assert.False(document.Selection.IsMarked("grp"));
        document.Shutdown();
    }

    /// <summary>§G TG-0 (G1, IG-31): Toggle Mark transforms the ONE
    /// authority — the publication's marked intent — the mirror
    /// follows in the apply, and the sentence speaks the store's count
    /// after the write, both ways.</summary>
    [Fact]
    public void ToggleMarkPublishesTheAuthorityAndSpeaksTheLiveCount()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");

        document.ToggleMark();

        Assert.Contains("a", document.AppliedPublication!.MarkedIntent);
        Assert.True(document.Selection.IsMarked("a"));
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("Marked \"Alpha\"", StringComparison.Ordinal)
                && x.Text.Contains("1 marked", StringComparison.Ordinal));

        _announced.Clear();
        document.ToggleMark();

        Assert.DoesNotContain("a", document.AppliedPublication!.MarkedIntent);
        Assert.False(document.Selection.IsMarked("a"));
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("Unmarked \"Alpha\"", StringComparison.Ordinal)
                && x.Text.Contains("0 marked", StringComparison.Ordinal));
        document.Shutdown();
    }

    /// <summary>§G TG-0 (IG-35): the order — a plainly absent selection
    /// refuses NothingSelected, and so does an id the admitted
    /// population cannot resolve; nothing publishes either way.</summary>
    [Fact]
    public void TheOrderIsSelectionThenAdmissionThenResolution()
    {
        CanvasDocumentViewModel document = Open();
        document.SelectNode(null, announce: false);

        document.ToggleMark();
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("Nothing selected", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(document.AppliedPublication!.MarkedIntent);

        _announced.Clear();
        document.SeatSelectionSilently("never-existed");
        document.ToggleMark();
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("Nothing selected", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(document.AppliedPublication!.MarkedIntent);
        document.Shutdown();
    }

    /// <summary>§G TG-0 (IG-34): Unmark resolves ONLY its explicit id —
    /// the selection may be absent — and is idempotent: the second
    /// call answers the count and speaks nothing.</summary>
    [Fact]
    public void UnmarkIsIdempotentAndNeverConsultsTheSelection()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        document.SelectNode(null, announce: false);
        _announced.Clear();

        Assert.Equal(0, document.Unmark("a"));
        Assert.False(document.Selection.IsMarked("a"));
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("Unmarked \"Alpha\"", StringComparison.Ordinal));

        _announced.Clear();
        Assert.Equal(0, document.Unmark("a"));
        document.AnnouncerForTests.FlushForTests();
        Assert.Empty(_announced);
        document.Shutdown();
    }

    /// <summary>§G TG-0 (G3, IG-33): Clear All speaks the PRE-CLEAR
    /// store count, and the render's own zero arm afterward.</summary>
    [Fact]
    public void ClearSpeaksThePreClearCount()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        document.SeatSelectionSilently("grp");
        document.ToggleMark();
        _announced.Clear();

        document.ClearMarks();
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("Cleared 2 marks", StringComparison.Ordinal));
        Assert.Empty(document.AppliedPublication!.MarkedIntent);
        Assert.Empty(document.Selection.Marked);

        _announced.Clear();
        document.ClearMarks();
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            x => x.Text.Contains("No marks", StringComparison.Ordinal));
        document.Shutdown();
    }

    /// <summary>§G TG-0 (G1/G7): Ctrl+Alt+M relays to the verb.</summary>
    [Fact]
    public void TheMarkChordRoutesToTheVerb()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        var pane = new FakePane();
        document.Navigator.AttachPresenter(pane);

        Assert.True(document.Navigator.HandleKey(
            System.Windows.Input.Key.M,
            System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt,
            pane));

        Assert.True(document.Selection.IsMarked("a"));
        Assert.Contains("a", document.AppliedPublication!.MarkedIntent);
        document.Shutdown();
    }

    /// <summary>§F review round 1 (F4b/IF-2, codoki's prescription):
    /// a DISPLACED outcome while the watcher stands down must not
    /// wedge the mode — the displaced resolution cancels it, the
    /// transient discards through the cancel's own closure, and a
    /// fresh mode entry admits afterward.</summary>
    [Fact]
    public void ADisplacedCommitCancelsTheModeAndFreesTheDocument()
    {
        CanvasDocumentViewModel document = Open();
        CanvasLoaded loaded = document.CurrentLoadedForModeEntry!;
        var id = new object();
        var spec = new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("A"),
            () => CanvasModeCommitResult.Pending(id),
            () =>
            {
                document.DiscardTransient();
                return new CanvasModeRestoration.CardsReturned(1);
            });
        Assert.True(document.Modes.Enter(spec, new object()));
        document.InstallTransient(
            CanvasTransientHolder.TryCapture(
                _session, loaded, ["a"], isResize: false)!);
        Assert.False(document.Modes.Commit());
        Assert.True(document.Modes.HasPendingCommitForTests);

        document.Modes.ResolveCommitDisplaced(id);

        Assert.False(document.Modes.IsActive);
        Assert.False(document.Modes.HasPendingCommitForTests);
        Assert.Null(document.Transient);

        // The document is FREE: a real mode enters and a verb admits.
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());
        Assert.True(document.Modes.Cancel());
        document.Shutdown();
    }

    /// <summary>§F review round 1: the connect half — the displaced
    /// resolution clears the origin memory through the cancel and the
    /// document stays free.</summary>
    [Fact]
    public void ADisplacedConnectCommitCancelsAndClearsTheOrigin()
    {
        CanvasDocumentViewModel document = Open();
        CanvasLoaded loaded = document.CurrentLoadedForModeEntry!;
        var id = new object();
        var spec = new CanvasModeSpec(
            CanvasMode.Connect,
            new CanvasModeObject.Card("A"),
            () => CanvasModeCommitResult.Pending(id),
            () =>
            {
                document.ClearConnectOrigin();
                return new CanvasModeRestoration.Unstated();
            });
        Assert.True(document.Modes.Enter(spec, new object()));
        document.InstallConnectOrigin(
            new CanvasConnectOrigin("a", "Alpha", loaded));
        Assert.False(document.Modes.Commit());

        document.Modes.ResolveCommitDisplaced(id);

        Assert.False(document.Modes.IsActive);
        Assert.Null(document.ConnectOrigin);
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterConnectMode());
        Assert.True(document.Modes.Cancel());
        document.Shutdown();
    }

    /// <summary>§F TF-10 (IF-30): the suspended column — a conflicted
    /// Return freezes the transient, and steps and presets REFUSE
    /// with the ladder's own conflict sentence, moving nothing.</summary>
    [Fact]
    public void ASuspendedModeFreezesItsSteps()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterResizeMode());
        Assert.True(document.Navigator.ModeStep(1, 0, large: false));
        File.WriteAllText(
            Path.Combine(_fixture.Root, "board.canvas"),
            DiskBytes() + "\n");
        Assert.False(document.Modes.Commit());
        Assert.True(document.Funnel.ModeSuspended);
        CanvasRect frozen = document.Transient!.Rects["a"];
        _announced.Clear();

        Assert.True(document.Navigator.ModeStep(1, 0, large: false));
        Assert.False(document.Navigator.ResizeDefaultSize());

        Assert.Equal(frozen, document.Transient!.Rects["a"]);
        document.AnnouncerForTests.FlushForTests();
        Assert.True(
            _announced.Count(a => a.Text.Contains(
                "changed on disk", StringComparison.Ordinal)) >= 2,
            "the step and the preset must each speak the conflict sentence");
        Assert.True(document.Modes.Cancel());
        document.Shutdown();
    }

    /// <summary>§F TF-10 (IF-30): a cancel during suspension forgets
    /// the yielded identity — nothing lingers, and a fresh mode
    /// admits.</summary>
    [Fact]
    public void ASuspendedCancelForgetsTheIdentity()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());
        Assert.True(document.Navigator.ModeStep(1, 0, large: false));
        File.WriteAllText(
            Path.Combine(_fixture.Root, "board.canvas"),
            DiskBytes() + "\n");
        Assert.False(document.Modes.Commit());
        Assert.True(document.Funnel.ModeSuspended);

        Assert.True(document.Modes.Cancel());

        Assert.False(document.Funnel.ModeSuspended);
        Assert.False(document.Modes.IsActive);
        document.Shutdown();
    }

    /// <summary>§F TF-10 (IF-30): a second Return during suspension
    /// refuses THROUGH THE LADDER — the conflict sentence, the mode
    /// standing, the frozen transient untouched.</summary>
    [Fact]
    public void CommitDuringSuspensionRefusesThroughTheLadder()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());
        Assert.True(document.Navigator.ModeStep(1, 0, large: false));
        File.WriteAllText(
            Path.Combine(_fixture.Root, "board.canvas"),
            DiskBytes() + "\n");
        Assert.False(document.Modes.Commit());
        _announced.Clear();

        Assert.False(document.Modes.Commit());

        Assert.True(document.Modes.IsActive);
        Assert.NotNull(document.Transient);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("changed on disk", StringComparison.Ordinal));
        Assert.True(document.Modes.Cancel());
        document.Shutdown();
    }

    /// <summary>§F TF-10 (F9a): the stored action names are mac's
    /// VERBATIM — part of Ctrl+Z's observable sentence, pinned
    /// byte-exact across all four verbs.</summary>
    [Fact]
    public void TheActionNamesAreMacsVerbatim()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        var pane = new FakePane();
        document.Navigator.AttachPresenter(pane);

        Assert.True(document.Navigator.EnterMoveMode());
        Assert.True(document.Navigator.ModeStep(1, 0, large: false));
        Assert.True(document.Modes.Commit());
        Assert.Equal("move \"Alpha\"", document.UndoStack.OfferedUndo!.Name);

        Assert.True(document.Navigator.EnterResizeMode());
        Assert.True(document.Navigator.ModeStep(1, 0, large: false));
        Assert.True(document.Modes.Commit());
        Assert.Equal("resize \"Alpha\"", document.UndoStack.OfferedUndo!.Name);

        document.OpenCardPicker(CanvasCardPickerPurpose.AlignWith);
        Assert.True(document.HandleCardPick(
            document.LastCardPickerRequestForTests!, "blocker"));
        Assert.Equal("align \"Alpha\"", document.UndoStack.OfferedUndo!.Name);

        CanvasConnectStage? staged = null;
        document.ConnectPromptRequested += stage => staged = stage;
        document.OpenCardPicker(CanvasCardPickerPurpose.ConnectTo);
        Assert.True(document.HandleCardPick(
            document.LastCardPickerRequestForTests!, "grp"));
        document.CanvasConnect(staged!, null);
        Assert.Equal(
            "connect \"Alpha\" to \"Ideas\"",
            document.UndoStack.OfferedUndo!.Name);
        document.Shutdown();
    }

    /// <summary>§F TF-9 (F8): connect mode end to end — the origin
    /// remembered, the reader's own movement steps the candidate,
    /// Return applies F7's staged connect with label NULL, one entry,
    /// the connected sentence after the clear.</summary>
    [Fact]
    public void ConnectModeConnectsTheReadersCard()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterConnectMode());
        Assert.NotNull(document.ConnectOrigin);
        Assert.Null(document.Transient);

        document.SeatSelectionSilently("grp");
        Assert.True(document.Modes.Commit());

        Assert.False(document.Modes.IsActive);
        Assert.Null(document.ConnectOrigin);
        string disk = DiskBytes().Replace(" ", "").Replace("\t", "");
        Assert.Contains("\"fromSide\":\"right\",\"toNode\":\"grp\"", disk);
        Assert.DoesNotContain("\"toNode\":\"grp\",\"toSide\":\"left\",\"label\"", disk);
        Assert.NotNull(document.UndoStack.OfferedUndo);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("onnected", StringComparison.Ordinal));
        document.Shutdown();
    }

    /// <summary>§F TF-9 (F8): Return with no movement ends without
    /// effect — nothing written, and the token is FREE (a later verb
    /// admits).</summary>
    [Fact]
    public void ReturnOnTheOriginEndsWithoutEffect()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        string before = DiskBytes();
        Assert.True(document.Navigator.EnterConnectMode());

        Assert.True(document.Modes.Commit());

        Assert.False(document.Modes.IsActive);
        Assert.Equal(before, DiskBytes());
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("no target", StringComparison.OrdinalIgnoreCase));

        // The token yielded: an ordinary verb admits.
        document.CanvasNewCard();
        Assert.NotEqual(before, DiskBytes());
        document.Shutdown();
    }

    /// <summary>§F TF-9 (IF-29): Esc returns SELECTION AND READER
    /// FOCUS to the origin — the restoration addresses the owning
    /// presenter, and the seat is the fallback.</summary>
    [Fact]
    public void EscReturnsSelectionAndFocusToTheOrigin()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        var pane = new RecordingPane();
        document.Navigator.AttachPresenter(pane);
        Assert.True(document.Navigator.EnterConnectMode());

        document.SeatSelectionSilently("blocker");
        Assert.True(document.Modes.Cancel());

        Assert.Equal("a", document.Selection.Selected);
        Assert.Contains("a", pane.FocusedRows);
        Assert.Null(document.ConnectOrigin);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("Alpha", StringComparison.Ordinal)
                && a.Text.Contains("ancel", StringComparison.Ordinal));
        document.Shutdown();
    }

    /// <summary>§F TF-9 (F1a): a displacement — a publish whose loaded
    /// reference is not the origin's — cancels connect mode and clears
    /// the memory.</summary>
    [Fact]
    public void ADisplacementCancelsConnectMode()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterConnectMode());

        // A reload installs a new loaded triple (the TF-2 idiom).
        document.Load();

        Assert.False(document.Modes.IsActive);
        Assert.Null(document.ConnectOrigin);
        document.Shutdown();
    }

    private sealed class RecordingPane : ICanvasSurfacePresenter
    {
        public List<string> FocusedRows { get; } = [];

        public CanvasSurfaceKind Projection => CanvasSurfaceKind.Outline;

        public bool ProjectionHasFocus => true;

        public bool CanMoveWithinProjection(bool forward) => true;

        public bool DismissTransientRegion() => false;

        public object? Owner => null;

        public bool ViewportCommand(CanvasViewportVerb verb) => false;

        public bool FocusRow(string nodeId)
        {
            FocusedRows.Add(nodeId);
            return true;
        }

        public bool FocusProjection() => false;
    }

    /// <summary>§F TF-8 (F7): the staged connect applies ONCE with
    /// every generated parameter spelled — core's sides, None/Arrow
    /// ends, the label — one action, one undo, the connected
    /// sentence.</summary>
    [Fact]
    public void StagedConnectAppliesOnceWithEveryParameterSpelled()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        CanvasConnectStage? staged = null;
        document.ConnectPromptRequested += stage => staged = stage;
        document.OpenCardPicker(CanvasCardPickerPurpose.ConnectTo);
        CanvasCardPickerRequest request = document.LastCardPickerRequestForTests!;

        Assert.True(document.HandleCardPick(request, "grp"));
        Assert.NotNull(staged);
        Assert.Equal("a", staged!.OriginId);
        Assert.Equal("grp", staged.TargetId);

        document.CanvasConnect(staged, "feeds2");

        // The fragments are UNIQUE to the new edge (the fixture's e1
        // carries the same side values toward blocker), so a swapped
        // pair cannot hide behind existing bytes.
        string disk = DiskBytes().Replace(" ", "").Replace("\t", "");
        Assert.Contains("\"label\":\"feeds2\"", disk);
        Assert.Contains("\"fromSide\":\"right\",\"toNode\":\"grp\"", disk);
        Assert.Contains("\"toNode\":\"grp\",\"toSide\":\"left\"", disk);
        Assert.NotNull(document.UndoStack.OfferedUndo);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("onnected", StringComparison.Ordinal));
        document.Shutdown();
    }

    /// <summary>§F TF-8 (IF-27): an empty submitted label normalizes
    /// to NULL — Enter-skips and click-with-empty-field serialize
    /// identically, and no empty "labelled" clause can render.</summary>
    [Fact]
    public void AnEmptyLabelNormalizesToNull()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        CanvasConnectStage? staged = null;
        document.ConnectPromptRequested += stage => staged = stage;
        document.OpenCardPicker(CanvasCardPickerPurpose.ConnectTo);
        Assert.True(document.HandleCardPick(
            document.LastCardPickerRequestForTests!, "grp"));

        document.CanvasConnect(staged!, string.Empty);

        string disk = DiskBytes().Replace(" ", "").Replace("\t", "");
        Assert.DoesNotContain("\"label\":\"\"", disk);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("onnected", StringComparison.Ordinal)
                && !a.Text.Contains("label", StringComparison.OrdinalIgnoreCase));
        document.Shutdown();
    }

    /// <summary>§F TF-8 (IF-26): connecting a card to itself refuses
    /// with mac's sentence — a TARGET problem, not a moving-set one —
    /// and stages nothing.</summary>
    [Fact]
    public void ASelfPickRefusesDifferentTarget()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        bool prompted = false;
        document.ConnectPromptRequested += _ => prompted = true;
        string before = DiskBytes();
        document.OpenCardPicker(CanvasCardPickerPurpose.ConnectTo);

        Assert.False(document.HandleCardPick(
            document.LastCardPickerRequestForTests!, "a"));

        Assert.False(prompted);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("different", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(before, DiskBytes());
        document.Shutdown();
    }

    /// <summary>§F TF-8 (IF-24/IF-25 dispositions): a target deleted
    /// after staging makes the eventual operation STALE — frozen §E
    /// deliberately drops it silently; nothing writes, no success
    /// speaks, and the immutable stage still names what was staged.</summary>
    [Fact]
    public void AVanishedTargetGoesSilentlyStale()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        CanvasConnectStage? staged = null;
        document.ConnectPromptRequested += stage => staged = stage;
        document.OpenCardPicker(CanvasCardPickerPurpose.ConnectTo);
        Assert.True(document.HandleCardPick(
            document.LastCardPickerRequestForTests!, "blocker"));

        document.SeatSelectionSilently("blocker");
        document.CanvasDeleteSelection();
        string afterDelete = DiskBytes();
        _announced.Clear();

        document.CanvasConnect(staged!, "late");

        Assert.Equal(afterDelete, DiskBytes());
        document.AnnouncerForTests.FlushForTests();
        Assert.DoesNotContain(
            _announced,
            a => a.Text.Contains("onnected", StringComparison.Ordinal));
        Assert.Equal("blocker", staged!.TargetId);
        document.Shutdown();
    }

    /// <summary>§F TF-8 (FD-4): the carried Rename Group front door —
    /// the request seeds the draft with the CURRENT title, and the
    /// submit rides §E's shipped verb onto disk.</summary>
    [Fact]
    public void RenameGroupPromptSeedsAndCommitsThroughTheShippedVerb()
    {
        CanvasDocumentViewModel document = Open();
        (string GroupId, string Current)? asked = null;
        document.GroupRenameRequested += (groupId, current) =>
            asked = (groupId, current);

        document.RequestGroupRename("grp");
        Assert.Equal(("grp", "Ideas"), asked);

        document.CanvasRenameGroup("grp", "Ideas 2");
        Assert.Contains("Ideas 2", DiskBytes());
        document.Shutdown();
    }

    /// <summary>§F TF-8 (FD-4): the Set Color prompt's choices are
    /// CORE's names verbatim (never a host copy of the table), and a
    /// chosen preset colors the selection on disk.</summary>
    [Fact]
    public void SetColorPromptChoicesAreCoresNamesAndColorTheCard()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        var prompt = (CanvasSetColorPrompt)CanvasPromptViewModel.SetColor(document);

        Assert.False(prompt.HasTextField);
        for (byte preset = 1; preset <= 6; preset++)
        {
            Assert.Equal(
                SlateUniffiMethods.CanvasColorName(new CanvasColor.Preset(preset)),
                prompt.Choices[preset - 1].Name);
        }
        Assert.Null(prompt.Choices[^1].Value);

        prompt.SelectedChoice = prompt.Choices[2];
        Assert.Equal(CanvasPromptSubmit.Pending, prompt.Submit(() => { }));

        Assert.Contains(
            "\"color\":\"3\"",
            DiskBytes().Replace(" ", "").Replace("\t", ""));
        document.Shutdown();
    }

    /// <summary>§F TF-8 (F5): the picker sheet refilters WITHOUT
    /// reordering — the rows stay core's proximity order, narrowed;
    /// clearing the filter restores the whole set.</summary>
    [Fact]
    public void ThePickerSheetRefiltersWithoutReordering()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        CanvasCardPickerModel? model = null;
        document.CardPickerRequested += (_, m) => model = m;
        document.OpenCardPicker(CanvasCardPickerPurpose.PlaceBelow);
        var sheet = new CanvasCardPickerViewModel(
            document, document.LastCardPickerRequestForTests!, model!);
        string[] all = [.. sheet.Rows.Select(r => r.NodeId)];

        sheet.Filter = "Blo";
        Assert.Equal(
            all.Where(id => id == "blocker"),
            sheet.Rows.Select(r => r.NodeId));

        sheet.Filter = string.Empty;
        Assert.Equal(all, sheet.Rows.Select(r => r.NodeId).ToArray());
        Assert.NotNull(sheet.SelectedRow);
        document.Shutdown();
    }

    /// <summary>§F TF-7 (F5): picker-anchored engine placement, one
    /// action end to end — the pick routes through the request, the
    /// engine computes the slot inside prepare-under-the-gate, disk
    /// geometry moves, ONE history entry, the placed sentence
    /// speaks.</summary>
    [Fact]
    public void PlaceBelowLandsOneActionWithCoresSlot()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        string before = DiskBytes();
        document.OpenCardPicker(CanvasCardPickerPurpose.PlaceBelow);
        CanvasCardPickerRequest request = document.LastCardPickerRequestForTests!;
        Assert.Equal(["a"], request.Moving.ToArray());

        document.HandleCardPick(request, "blocker");

        Assert.NotEqual(before, DiskBytes());
        Assert.NotNull(document.UndoStack.OfferedUndo);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("Moved", StringComparison.Ordinal));
        document.Shutdown();
    }

    /// <summary>§F TF-7 (F5): a marked set moves as a RIGID UNIT —
    /// boxes in reading order, the positional Origins mapped back by
    /// that same order, one action, the bulk sentence.</summary>
    [Fact]
    public void ASetPlacesRigidlyByReadingOrder()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.SeatSelectionSilently("grp");
        document.ToggleMark();
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        document.OpenCardPicker(CanvasCardPickerPurpose.PlaceRightOf);
        CanvasCardPickerRequest request = document.LastCardPickerRequestForTests!;

        // Reading order, not mark order: "a" (0,0) precedes "grp" (600,0).
        Assert.Equal(["a", "grp"], request.Moving.ToArray());

        double offsetBefore =
            request.Rects["grp"].X - request.Rects["a"].X;
        document.HandleCardPick(request, "blocker");

        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("2 cards", StringComparison.Ordinal));
        CanvasPopulation population =
            document.AppliedPublication!.Loaded!.Population;
        double offsetAfter =
            population.SceneByNode["grp"].X - population.SceneByNode["a"].X;
        Assert.Equal(offsetBefore, offsetAfter);
        Assert.NotNull(document.UndoStack.OfferedUndo);
        document.Shutdown();
    }

    /// <summary>§F TF-7 (F5): the refusal table is TOTAL and
    /// state-keeping — a pick inside the moving set and a vanished
    /// target each refuse with their exact sentence and write
    /// nothing.</summary>
    [Fact]
    public void TheRefusalTableIsTotal()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        string before = DiskBytes();
        document.OpenCardPicker(CanvasCardPickerPurpose.PlaceBelow);
        CanvasCardPickerRequest request = document.LastCardPickerRequestForTests!;

        document.HandleCardPick(request, "a");
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("outside", StringComparison.OrdinalIgnoreCase));

        _announced.Clear();
        document.HandleCardPick(request, "never-existed");
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("different", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(before, DiskBytes());
        Assert.Null(document.UndoStack.OfferedUndo);
        document.Shutdown();
    }

    /// <summary>§F TF-7 (F6): align is the same-axis top-edge slot
    /// with a total table — an occupied slot refuses, success moves Y
    /// only, and doing it again answers NoChanges writing
    /// nothing.</summary>
    [Fact]
    public void AlignSetsTheTopEdgeOrRefuses()
    {
        CanvasDocumentViewModel document = Open();

        // Occupied: cramped's slot at blocker's Y collides with blocker.
        document.SeatSelectionSilently("cramped");
        string before = DiskBytes();
        document.OpenCardPicker(CanvasCardPickerPurpose.AlignWith);
        document.HandleCardPick(
            document.LastCardPickerRequestForTests!, "blocker");
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("overlap", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(before, DiskBytes());

        // Success: "a" aligns to blocker's top edge — Y moves, X stays.
        document.SeatSelectionSilently("a");
        double x0 = document.AppliedPublication!.Loaded!
            .Population.SceneByNode["a"].X;
        document.OpenCardPicker(CanvasCardPickerPurpose.AlignWith);
        document.HandleCardPick(
            document.LastCardPickerRequestForTests!, "blocker");
        CanvasPopulation population =
            document.AppliedPublication!.Loaded!.Population;
        Assert.Equal(40, population.SceneByNode["a"].Y);
        Assert.Equal(x0, population.SceneByNode["a"].X);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("Aligned", StringComparison.Ordinal));

        // Already aligned: NoChanges, nothing written.
        string aligned = DiskBytes();
        _announced.Clear();
        document.OpenCardPicker(CanvasCardPickerPurpose.AlignWith);
        document.HandleCardPick(
            document.LastCardPickerRequestForTests!, "blocker");
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("No changes", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(aligned, DiskBytes());
        document.Shutdown();
    }

    /// <summary>§F TF-7 (IF-19): the request carries its immutable
    /// context — reading-ordered movers, their rects, the loaded
    /// identity — and a confirm against a FOREIGN identity refuses
    /// PickDifferentTarget writing nothing.</summary>
    [Fact]
    public void ThePickerRequestCarriesItsContext()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        document.SeatSelectionSilently("grp");
        document.ToggleMark();
        document.OpenCardPicker(CanvasCardPickerPurpose.PlaceBelow);
        CanvasCardPickerRequest request = document.LastCardPickerRequestForTests!;
        Assert.Equal(["a", "grp"], request.Moving.ToArray());
        Assert.Equal(0, request.Rects["a"].X);
        Assert.Equal(600, request.Rects["grp"].X);

        CanvasDocumentViewModel other = Open();
        CanvasCardPickerRequest foreign = request with
        {
            Identity = other.AppliedPublication!.Loaded!,
        };
        string before = DiskBytes();
        document.HandleCardPick(foreign, "blocker");

        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("different", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(before, DiskBytes());
        other.Shutdown();
        document.Shutdown();
    }

    /// <summary>§F TF-7 (IF-20): the F5 picker anchors proximity at
    /// the PRIMARY MOVER, not the bare selection — the model's rows
    /// are core's answer VERBATIM for that anchor.</summary>
    [Fact]
    public void TheProximityAnchorIsThePrimaryMover()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("essay");
        document.SeatSelectionSilently("a");
        document.ToggleMark();
        document.SeatSelectionSilently("essay");
        document.ToggleMark();
        CanvasCardPickerModel? shown = null;
        document.CardPickerRequested += (_, model) => shown = model;

        document.OpenCardPicker(CanvasCardPickerPurpose.PlaceBelow);

        // The primary mover is "a" (reading order), though the far
        // "essay" is selected — the two anchors ORDER DIFFERENTLY
        // ("grp" is nearest to "a" and farthest from "essay"), so an
        // anchor quietly reverted to the selection cannot pass.
        Assert.Equal(
            ["a", "essay"],
            document.LastCardPickerRequestForTests!.Moving.ToArray());
        string[] expected = [];
        ulong handle = HandleOf(document);
        expected = _session.CanvasProximityOrder(handle, "a", ["a", "essay"]);
        Assert.Equal(expected, shown!.Rows.Select(r => r.NodeId).ToArray());
        document.Shutdown();
    }

    /// <summary>§F TF-5 (F10): one derived install moves the whole
    /// surface — the admitted rects land in the state, the placement
    /// (pixels and a11y frames alike) moves, and the effective rect
    /// answers hit-testing's question. Wired exactly as the renderer
    /// wires it: the aggregate observable into CommitTransient.</summary>
    [Fact]
    public void ATransientStepMovesTheWholeSurfaceInOnePass()
    {
        CanvasDocumentViewModel document = Open();
        var engine = new CanvasPresentationEngine(synchronousForTests: true);
        document.ModeVisibleChanged += () => engine.CommitTransient(document.Transient);
        engine.OnPublicationApplied(document.AppliedPublication!);
        engine.CommitViewport(v => v.WithViewSize(800, 600));
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());
        double x0 = document.Transient!.Originals["a"].X;

        Assert.True(document.Navigator.ModeStep(1, 0, large: false));

        double step = SlateUniffiMethods.CanvasConstants().GridStep;
        CanvasPresentationState state = engine.Current!;
        Assert.NotNull(state.TransientRects);
        Assert.Equal(x0 + step, state.TransientRects!["a"].X);
        CanvasPeerPlacement placement =
            state.Topology.Placements[CanvasPeerKey.Card("a")];
        Assert.Equal(x0 + step, placement.X);
        CanvasSceneNode node =
            document.AppliedPublication!.Loaded!.Population.SceneByNode["a"];
        Assert.Equal(x0 + step, state.NodeRect(node).X);
        Assert.True(document.Modes.Cancel());
        document.Shutdown();
    }

    /// <summary>§F TF-5 (F10): the identity check — a holder whose
    /// identity is NOT this state's loaded reference derives NOTHING:
    /// a sibling pane on another publication renders committed
    /// truth.</summary>
    [Fact]
    public void AForeignIdentityRendersCommittedTruth()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());
        CanvasTransientHolder held = document.Transient!;
        Assert.True(document.Navigator.ModeStep(1, 0, large: false));

        // A second engine fed a DIFFERENT loaded (a fresh open of the
        // same vault) — the sibling-pane shape.
        CanvasDocumentViewModel sibling = Open();
        var engine = new CanvasPresentationEngine(synchronousForTests: true);
        engine.OnPublicationApplied(sibling.AppliedPublication!);
        engine.CommitTransient(held);

        CanvasPresentationState state = engine.Current!;
        Assert.Null(state.TransientRects);
        CanvasSceneNode node =
            sibling.AppliedPublication!.Loaded!.Population.SceneByNode["a"];
        Assert.Equal(node.X, state.NodeRect(node).X);
        Assert.True(document.Modes.Cancel());
        sibling.Shutdown();
        document.Shutdown();
    }

    /// <summary>§F TF-5 (F10): virtualization treats a transient card
    /// as MATERIALIZED — the essay sits far outside the window, and
    /// its override materializes the placement anyway.</summary>
    [Fact]
    public void TheTransientMaterializesAnOffWindowCard()
    {
        CanvasDocumentViewModel document = Open();
        var engine = new CanvasPresentationEngine(synchronousForTests: true);
        document.ModeVisibleChanged += () => engine.CommitTransient(document.Transient);
        engine.OnPublicationApplied(document.AppliedPublication!);
        engine.CommitViewport(v => v.WithViewSize(400, 300));

        // Without a transient the essay (x=5000) is unmaterialized.
        Assert.False(engine.Current!.Topology.Placements.ContainsKey(
            CanvasPeerKey.Card("essay")));

        document.SeatSelectionSilently("essay");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());

        CanvasPeerPlacement placement =
            engine.Current!.Topology.Placements[CanvasPeerKey.Card("essay")];
        Assert.Equal(CanvasPeerCell.Materialized, placement.Cell);
        Assert.True(document.Modes.Cancel());
        document.Shutdown();
    }

    /// <summary>§F TF-5 (F10): ONE aggregate observable, one change
    /// per transition — entry, each step, teardown; never a half
    /// state.</summary>
    [Fact]
    public void OneEventFiresPerModeTransition()
    {
        CanvasDocumentViewModel document = Open();
        int fired = 0;
        bool halfState = false;
        document.ModeVisibleChanged += () =>
        {
            fired++;
            // At every observation the pair is CONSISTENT: an active
            // machine with a holder, or an idle machine without one.
            halfState |= document.Modes.IsActive == (document.Transient is null);
        };
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());
        Assert.Equal(1, fired);
        Assert.True(document.Navigator.ModeStep(1, 0, large: false));
        Assert.Equal(2, fired);
        Assert.True(document.Navigator.ModeStep(0, 1, large: true));
        Assert.Equal(3, fired);
        Assert.True(document.Modes.Cancel());
        Assert.Equal(4, fired);
        Assert.False(halfState, "an observer saw a half state");
        document.Shutdown();
    }

    /// <summary>§F TF-5 (F10): a committed mode tears the authority
    /// down — after Return the engine derives committed truth from
    /// the refreshed publication, no transient attached.</summary>
    [Fact]
    public void ACommittedModeRendersTheCommittedTruth()
    {
        CanvasDocumentViewModel document = Open();
        var engine = new CanvasPresentationEngine(synchronousForTests: true);
        document.ModeVisibleChanged += () => engine.CommitTransient(document.Transient);
        document.PublicationApplied += engine.OnPublicationApplied;
        engine.OnPublicationApplied(document.AppliedPublication!);
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());
        double x0 = document.Transient!.Originals["a"].X;
        Assert.True(document.Navigator.ModeStep(1, 0, large: false));

        Assert.True(document.Modes.Commit());

        double step = SlateUniffiMethods.CanvasConstants().GridStep;
        CanvasPresentationState state = engine.Current!;
        Assert.Null(state.TransientRects);
        CanvasSceneNode node = state.Source.Loaded!.Population.SceneByNode["a"];
        Assert.Equal(x0 + step, node.X);
        Assert.Equal(x0 + step, state.NodeRect(node).X);
        document.PublicationApplied -= engine.OnPublicationApplied;
        document.Shutdown();
    }

    /// <summary>§F TF-4 (F3/F4a): resize mode end to end — enter on
    /// the selected card, one width step, Return commits ONE action;
    /// the disk width moves by GridStep and core's Resized sentence
    /// speaks after the clear.</summary>
    [Fact]
    public void ResizeModeEntersStepsAndCommitsOneAction()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterResizeMode());
        Assert.True(document.Transient!.IsResize);
        double w0 = document.Transient!.Originals["a"].Width;

        Assert.True(document.Navigator.ModeStep(1, 0, large: false));

        Assert.True(document.Modes.Commit());
        Assert.False(document.Modes.IsActive);
        Assert.Null(document.Transient);
        double step = SlateUniffiMethods.CanvasConstants().GridStep;
        Assert.Contains(
            $"\"width\":{(int)(w0 + step)}",
            DiskBytes().Replace(" ", ""));
        Assert.NotNull(document.UndoStack.OfferedUndo);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("Resized", StringComparison.Ordinal));
        document.Shutdown();
    }

    /// <summary>§F TF-4 (F3): REJECT-THE-STEP — a step that would
    /// cross MinCardSize refuses with the clamped sentence and
    /// changes NOTHING; the mode stands.</summary>
    [Fact]
    public void AStepBelowTheMinimumRefusesWholly()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterResizeMode());

        bool clamped = false;
        for (int i = 0; i < 60 && !clamped; i++)
        {
            CanvasRect before = document.Transient!.Rects["a"];
            _announced.Clear();
            Assert.True(document.Navigator.ModeStep(-1, 0, large: true));
            document.AnnouncerForTests.FlushForTests();
            if (_announced.Any(a => a.Text.Contains(
                    "small", StringComparison.OrdinalIgnoreCase)
                || a.Text.Contains("minimum", StringComparison.OrdinalIgnoreCase)))
            {
                clamped = true;
                Assert.Equal(before, document.Transient!.Rects["a"]);
            }
        }

        Assert.True(clamped, "the clamp never spoke");
        Assert.True(document.Modes.IsActive);
        Assert.True(document.Modes.Cancel());
        document.Shutdown();
    }

    /// <summary>§F TF-4 (F3): presets land through the SAME overlap
    /// machine as steps — Default Size on a card jammed against a
    /// neighbor speaks the onset in its geometry sentence.</summary>
    [Fact]
    public void PresetsRouteThroughTheOverlapMachine()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("cramped");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterResizeMode());
        document.AnnouncerForTests.FlushForTests();
        _announced.Clear();

        Assert.True(document.Navigator.ResizeDefaultSize());

        CanvasConstants constants = SlateUniffiMethods.CanvasConstants();
        Assert.Equal(
            constants.DefaultCardW, document.Transient!.Rects["cramped"].Width);
        document.AnnouncerForTests.FlushForTests();
        RenderedAnnouncement geometry = Assert.Single(_announced);
        Assert.Contains("by", geometry.Text, StringComparison.Ordinal);
        Assert.Contains(
            "overlap", geometry.Text, StringComparison.OrdinalIgnoreCase);
        Assert.True(document.Modes.Cancel());
        document.Shutdown();
    }

    /// <summary>§F TF-4 (F3): Fit to Content is the D-5 placeholder
    /// formula at default width; a non-text node's refused read keeps
    /// the transient — the VM's own table already spoke.</summary>
    [Fact]
    public void FitContentUsesTheFormulaAndKeepsTheTransientOnRefusal()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterResizeMode());

        string text = document.NodeTextOf("a")!;
        int lines = Math.Max(
            1, (text.Length / 32) + text.Count(c => c == '\n'));
        CanvasConstants constants = SlateUniffiMethods.CanvasConstants();
        double expected = Math.Min(
            600, Math.Max((lines * 24) + 40, constants.MinCardSize));

        Assert.True(document.Navigator.ResizeFitContent());
        Assert.Equal(expected, document.Transient!.Rects["a"].Height);
        Assert.Equal(
            constants.DefaultCardW, document.Transient!.Rects["a"].Width);
        Assert.True(document.Modes.Cancel());

        // The CAP half (the formula's one live guard beside the
        // width: MinCardSize is 40, below any one-line text's 64,
        // so the floor is belt-and-braces by arithmetic): an 800-char
        // essay wants 25 lines' height and the cap holds it at 600.
        document.SeatSelectionSilently("essay");
        Assert.True(document.Navigator.EnterResizeMode());
        Assert.True(document.Navigator.ResizeFitContent());
        Assert.Equal(600, document.Transient!.Rects["essay"].Height);
        Assert.True(document.Modes.Cancel());

        // The refusal half: a group has no text; the read speaks its
        // own sentence, the preset does nothing, the mode stands.
        document.SeatSelectionSilently("grp");
        Assert.True(document.Navigator.EnterResizeMode());
        CanvasRect before = document.Transient!.Rects["grp"];
        Assert.False(document.Navigator.ResizeFitContent());
        Assert.Equal(before, document.Transient!.Rects["grp"]);
        Assert.True(document.Modes.IsActive);
        Assert.True(document.Modes.Cancel());
        document.Shutdown();
    }

    /// <summary>§F TF-4 (IF-18 reconciled): the resize chord is mac's
    /// quick loop — it enters, and during resize it COMMITS; a
    /// DIFFERENT active mode still gets frozen C's M7 rejection.</summary>
    [Fact]
    public void TheResizeChordCommitsTheActiveLoop()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        var pane = new FakePane();
        document.Navigator.AttachPresenter(pane);

        Assert.True(document.Navigator.HandleKey(
            System.Windows.Input.Key.R,
            System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt,
            pane));
        Assert.True(document.Modes.IsActive);
        Assert.True(document.Navigator.ModeStep(1, 0, large: false));

        Assert.True(document.Navigator.HandleKey(
            System.Windows.Input.Key.R,
            System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt,
            pane));
        Assert.False(document.Modes.IsActive);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("Resized", StringComparison.Ordinal));

        // Cross-mode: move active, the resize chord is REJECTED by M7.
        Assert.True(document.Navigator.EnterMoveMode());
        _announced.Clear();
        Assert.True(document.Navigator.HandleKey(
            System.Windows.Input.Key.R,
            System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt,
            pane));
        Assert.Equal(CanvasMode.Move, document.Modes.Active!.Mode);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("is active", StringComparison.Ordinal));
        Assert.True(document.Modes.Cancel());
        document.Shutdown();
    }

    /// <summary>§F TF-4 (F3): the minting rule at its edges — the
    /// material rule, not a cast: non-finite mints 0, negatives clamp
    /// to 0, huge values saturate, fractions round.</summary>
    [Fact]
    public void SafeUintMintsTheEdges()
    {
        Assert.Equal(0u, CanvasNavigator.CanvasSafeUint(double.NaN));
        Assert.Equal(0u, CanvasNavigator.CanvasSafeUint(double.PositiveInfinity));
        Assert.Equal(0u, CanvasNavigator.CanvasSafeUint(-5));
        Assert.Equal(uint.MaxValue, CanvasNavigator.CanvasSafeUint(1e300));
        Assert.Equal(uint.MaxValue, CanvasNavigator.CanvasSafeUint(9e15));
        Assert.Equal(4u, CanvasNavigator.CanvasSafeUint(3.6));
        Assert.Equal(120u, CanvasNavigator.CanvasSafeUint(120.0));
    }

    /// <summary>§F TF-3 (F2/F4a/F9a): move mode end to end — enter on
    /// the selection, one grid step right, Return commits ONE action
    /// through the bridge; the disk moves by GridStep, one history
    /// entry lands, and the committed sentence speaks after the
    /// clear.</summary>
    [Fact]
    public void MoveModeEntersNudgesAndCommitsOneAction()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        var pane = new FakePane();
        document.Navigator.AttachPresenter(pane);
        Assert.True(document.Navigator.EnterMoveMode());
        Assert.NotNull(document.Transient);
        double x0 = document.Transient!.Originals["a"].X;

        Assert.True(document.Navigator.ModeStep(1, 0, large: false));

        // The inline test runner completes while OnCommit is on the
        // stack; the controller's early-resolution memory lands it the
        // moment the mark exists, so Commit answers applied.
        Assert.True(document.Modes.Commit());
        Assert.False(document.Modes.IsActive);
        Assert.Null(document.Transient);

        double step = SlateUniffiMethods.CanvasConstants().GridStep;
        Assert.Contains(
            $"\"x\":{(int)(x0 + step)}",
            DiskBytes().Replace(" ", ""));
        Assert.NotNull(document.UndoStack.OfferedUndo);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("Placed", StringComparison.Ordinal));
        document.Shutdown();
    }

    /// <summary>§F TF-3 (F1): Esc restores the exact prior bytes with
    /// no backend call, and the restoration speaks.</summary>
    [Fact]
    public void EscRestoresExactBytesWithNoWrite()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());
        Assert.True(document.Navigator.ModeStep(1, 0, large: true));
        Assert.True(document.Modes.Cancel());

        Assert.Equal(before, DiskBytes());
        Assert.Null(document.Transient);
        document.Shutdown();
    }

    /// <summary>§F TF-3 (F2): the overlap machine speaks TRANSITIONS —
    /// onset once when overlap begins, silence while it holds,
    /// cleared once when it ends.</summary>
    [Fact]
    public void TheOverlapMachineSpeaksTransitionsOnly()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());
        document.AnnouncerForTests.FlushForTests();
        _announced.Clear();

        // "a" sits left of "cramped"'s blocker geometry: step right
        // until overlap onsets, keep stepping (still overlapped),
        // then step back out.
        int onsets = 0;
        int cleareds = 0;
        void Count()
        {
            document.AnnouncerForTests.FlushForTests();
            foreach (RenderedAnnouncement line in _announced)
            {
                if (line.Text.Contains("overlap", StringComparison.OrdinalIgnoreCase)
                    && line.Text.Contains("now", StringComparison.OrdinalIgnoreCase))
                {
                    onsets++;
                }
                if (line.Text.Contains("clear", StringComparison.OrdinalIgnoreCase))
                {
                    cleareds++;
                }
            }
            _announced.Clear();
        }
        for (int i = 0; i < 12; i++)
        {
            Assert.True(document.Navigator.ModeStep(1, 0, large: false));
        }
        Count();
        int onsetsAfterIn = onsets;
        for (int i = 0; i < 12; i++)
        {
            Assert.True(document.Navigator.ModeStep(-1, 0, large: false));
        }
        Count();
        Assert.True(onsetsAfterIn <= 1, $"onsets spoken {onsetsAfterIn} times");
        Assert.True(cleareds <= 1, $"cleared spoken {cleareds} times");
        Assert.Equal(onsetsAfterIn, cleareds);
        Assert.True(document.Modes.Cancel());
        document.Shutdown();
    }

    /// <summary>§F TF-3 (FD-3): a held transient owns the arrows and
    /// Shift is the large step; without a mode the arrows keep their
    /// §C meaning.</summary>
    [Fact]
    public void ArrowsRouteToTheModeAndShiftIsLarge()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        var pane = new FakePane();
        document.Navigator.AttachPresenter(pane);
        Assert.True(document.Navigator.EnterMoveMode());
        double x0 = document.Transient!.Rects["a"].X;

        Assert.True(document.Navigator.HandleKey(
            System.Windows.Input.Key.Right, System.Windows.Input.ModifierKeys.None, pane));
        CanvasConstants constants = SlateUniffiMethods.CanvasConstants();
        Assert.Equal(x0 + constants.GridStep, document.Transient!.Rects["a"].X);

        Assert.True(document.Navigator.HandleKey(
            System.Windows.Input.Key.Right, System.Windows.Input.ModifierKeys.Shift, pane));
        Assert.Equal(
            x0 + constants.GridStep + constants.GridStepLarge,
            document.Transient!.Rects["a"].X);
        Assert.True(document.Modes.Cancel());
        document.Shutdown();
    }

    /// <summary>§F TF-3 (F4b): Return without a change ends without
    /// effect — nothing applies, the sentence says so.</summary>
    [Fact]
    public void ANoEffectReturnSaysSo()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());

        Assert.True(document.Modes.Commit());
        Assert.False(document.Modes.IsActive);
        Assert.Equal(before, DiskBytes());
        Assert.Null(document.UndoStack.OfferedUndo);
        document.AnnouncerForTests.FlushForTests();
        Assert.Contains(
            _announced,
            a => a.Text.Contains("nothing changed", StringComparison.OrdinalIgnoreCase));
        document.Shutdown();
    }

    /// <summary>§F TF-3 (FD-5): a conflicted Return SUSPENDS — the
    /// mode and transient stand frozen, the token yields so the
    /// recovery's writes admit.</summary>
    [Fact]
    public void AConflictedReturnSuspendsTheMode()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        document.Navigator.AttachPresenter(new FakePane());
        Assert.True(document.Navigator.EnterMoveMode());
        Assert.True(document.Navigator.ModeStep(1, 0, large: false));

        // The disk moves under the entry — an external writer.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "board.canvas"),
            DiskBytes() + "\n");

        Assert.False(document.Modes.Commit());
        Assert.True(document.Modes.IsActive);
        Assert.False(document.Modes.HasPendingCommitForTests);
        Assert.NotNull(document.Transient);
        Assert.NotNull(document.Funnel.Conflict);
        document.Shutdown();
    }

    private sealed class FakePane : ICanvasSurfacePresenter
    {
        public CanvasSurfaceKind Projection => CanvasSurfaceKind.Outline;

        public bool ProjectionHasFocus => true;

        public bool CanMoveWithinProjection(bool forward) => true;

        public bool DismissTransientRegion() => false;

        public object? Owner => null;

        public bool ViewportCommand(CanvasViewportVerb verb) => false;

        public bool FocusRow(string nodeId) => false;

        public bool FocusProjection() => false;
    }

    /// <summary>New Card: a text card lands on disk at core's
    /// placement, the confirmation speaks core's relative phrase, the
    /// created card is SELECTED, and the inverse restores the exact
    /// prior bytes.</summary>
    [Fact]
    public void NewCardCreatesSelectsAnnouncesAndUndoes()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();

        document.CanvasNewCard();

        string after = DiskBytes();
        Assert.NotEqual(before, after);
        Assert.Contains("\"text\":\"\"", after.Replace(" ", ""));
        Assert.Contains(
            _announced, spoken => spoken.Text.StartsWith("Created text card"));
        string? created = document.Selection.Selected;
        Assert.NotNull(created);
        Assert.NotEqual("a", created);
        Assert.NotNull(document.UndoStack.OfferedUndo);

        Undo(document);
        Assert.Equal(before, DiskBytes());
    }

    /// <summary>Edit commit: one SetNodeContent, the typed update
    /// confirmation, and the inverse restores the prior bytes.</summary>
    [Fact]
    public void CommitCardEditWritesAnnouncesAndUndoes()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();

        document.CanvasCommitCardEdit("a", "Alpha rewritten");

        Assert.Contains("Alpha rewritten", DiskBytes());
        Assert.Contains(
            _announced, spoken => spoken.Text.StartsWith("Updated \""));
        Undo(document);
        Assert.Equal(before, DiskBytes());
    }

    /// <summary>Delete's card arm: the row leaves the disk, the
    /// confirmation carries the undo hint, the selection CLEARS
    /// (mac's behavior, typed in the effect), and the inverse — a
    /// positioned restore — brings back the exact bytes.</summary>
    [Fact]
    public void DeleteCardClearsSelectionAnnouncesAndUndoes()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();
        document.SelectNode("a");

        document.CanvasDeleteSelection();

        Assert.DoesNotContain("Alpha", DiskBytes());
        Assert.Null(document.Selection.Selected);
        Assert.Contains(
            _announced,
            spoken => spoken.Text.StartsWith("Deleted ")
                && spoken.Text.Contains(CanvasPhrase.UndoChord));
        Undo(document);
        Assert.Equal(before, DiskBytes());
    }

    /// <summary>§G2 TG2-4 (G2-9, IG2-22): Create Connected Card is ONE
    /// action carrying the exact tuple — a fresh text card at the engine's
    /// placement and a fresh edge from the ORIGIN to it with no from-end
    /// and an arrow to-end, no label — the new card seated, the sentence
    /// naming the origin, and ONE undo restoring the bytes.</summary>
    [Fact]
    public void CreateConnectedCardIsOneActionWithTheExactTuple()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();
        document.SeatSelectionSilently("a");
        _announced.Clear();

        CanvasMutationOperation? operation = document.CanvasCreateConnectedCard();

        Assert.NotNull(operation);
        string created = Assert.IsType<string>(document.Selection.Selected);
        Assert.NotEqual("a", created);
        CanvasPopulation population = document.AppliedPublication!.Loaded!.Population;
        Assert.Equal("text", population.SceneByNode[created].Kind);
        CanvasSceneEdge edge = Assert.Single(population.SceneEdges, e => e.ToNode == created);
        Assert.Equal("a", edge.FromNode);
        Assert.False(edge.FromArrow);
        Assert.True(edge.ToArrow);
        Assert.Null(edge.Label);
        Assert.Contains(_announced, s => s.Text.Contains("Alpha", StringComparison.Ordinal));

        Undo(document);

        Assert.Equal(before, DiskBytes());
        document.Shutdown();
    }

    /// <summary>§G2 TG2-4 (G2-5/G2-9): the direction is the engine's HINT —
    /// Left places the new card left of the origin, Above above it.</summary>
    [Fact]
    public void CreateConnectedCardHonorsTheDirectionHint()
    {
        CanvasDocumentViewModel document = Open();
        CanvasSceneNode origin = document.AppliedPublication!.Loaded!.Population.SceneByNode["blocker"];
        document.SeatSelectionSilently("blocker");

        Assert.NotNull(document.CanvasCreateConnectedCard(CanvasPlaceDirection.LeftOf));
        string left = document.Selection.Selected!;
        Assert.True(document.AppliedPublication!.Loaded!.Population.SceneByNode[left].X < origin.X);

        document.SeatSelectionSilently("blocker");
        Assert.NotNull(document.CanvasCreateConnectedCard(CanvasPlaceDirection.Above));
        string above = document.Selection.Selected!;
        Assert.True(document.AppliedPublication!.Loaded!.Population.SceneByNode[above].Y < origin.Y);
        document.Shutdown();
    }

    /// <summary>§G2 TG2-4 (G2-1, IG2-34): Ctrl+Alt+Shift+N is the navigator's
    /// chord and the presenter's OWNER rides into the operation — the
    /// editor receipt names that owner, at most once.</summary>
    [Fact]
    public void TheConnectedCardChordCarriesThePresentersOwner()
    {
        CanvasDocumentViewModel document = Open();
        var tab = new object();
        var pane = new OwnedPane(tab);
        document.Navigator.AttachPresenter(pane);
        document.SeatSelectionSilently("a");
        var receipts = new List<(object Owner, string NodeId)>();
        document.CreatedEditorRequested += (owner, nodeId) => receipts.Add((owner, nodeId));

        Assert.True(document.Navigator.HandleKey(
            Key.N, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, pane));

        (object owner, string nodeId) = Assert.Single(receipts);
        Assert.Same(tab, owner);
        Assert.Equal(document.Selection.Selected, nodeId);
        document.Shutdown();
    }

    /// <summary>§G2 TG2-4 (G2-8, R22, IG2-40): Remove from Group places the
    /// card outside its enclosing group by the engine and speaks the
    /// group's label; a card in no group refuses NotInAGroup with its
    /// title; one undo restores the bytes.</summary>
    [Fact]
    public void RemoveFromGroupPlacesOutsideAndRefusesACardInNoGroup()
    {
        CanvasDocumentViewModel document = Open();
        document.SeatSelectionSilently("a");
        Assert.NotNull(document.CanvasMoveIntoGroup("grp"));
        Assert.Contains("Ideas", document.Outline.First(r => r.NodeId == "a").GroupPath);
        string inside = DiskBytes();
        _announced.Clear();

        CanvasMutationOperation? removed = document.CanvasRemoveFromGroup();

        Assert.NotNull(removed);
        Assert.Empty(document.Outline.First(r => r.NodeId == "a").GroupPath);
        Assert.Contains(_announced, s => s.Text.Contains("Removed from group \"Ideas\"", StringComparison.Ordinal));

        Undo(document);
        Assert.Equal(inside, DiskBytes());

        document.SeatSelectionSilently("blocker");
        _announced.Clear();
        Assert.Null(document.CanvasRemoveFromGroup());
        Assert.Contains(_announced, s => s.Text.Contains("Blocker", StringComparison.Ordinal)
            && s.Text.Contains("group", StringComparison.OrdinalIgnoreCase));
        document.Shutdown();
    }

    private sealed class OwnedPane(object owner) : ICanvasSurfacePresenter
    {
        public CanvasSurfaceKind Projection => CanvasSurfaceKind.Outline;

        public bool ProjectionHasFocus => true;

        public bool CanMoveWithinProjection(bool forward) => true;

        public bool DismissTransientRegion() => false;

        public object? Owner => owner;

        public bool ViewportCommand(CanvasViewportVerb verb) => false;

        public bool FocusRow(string nodeId) => false;

        public bool FocusProjection() => false;
    }

    /// <summary>§G2 TG2-0 (G2-2, IG2-34): the seven §E verbs answer with
    /// their OPERATION — minted with the invoking owner when one is
    /// passed (the document otherwise), carrying the completion, and
    /// null when the opener refused.</summary>
    [Fact]
    public void TheFrontDoorVerbsAnswerWithTheirOperationAndOwner()
    {
        CanvasDocumentViewModel document = Open();
        var tab = new object();
        var landed = new List<CanvasOperationOutcome>();
        document.SeatSelectionSilently("a");

        CanvasMutationOperation? group = document.CanvasNewGroup(
            "Q3", owner: tab, completion: landed.Add);

        Assert.NotNull(group);
        Assert.Same(tab, group.Owner);
        Assert.Contains(CanvasOperationOutcome.Installed, landed);

        document.SeatSelectionSilently("a");
        CanvasMutationOperation? moved = document.CanvasMoveIntoGroup("grp");
        Assert.NotNull(moved);
        Assert.Same(document, moved.Owner);

        Assert.NotNull(document.CanvasAddFileCard("note.md", null, owner: tab));
        Assert.NotNull(document.CanvasAddLinkCard("https://example.org/x", owner: tab));
        Assert.NotNull(document.CanvasLocateFile("a", "other.md", owner: tab));
        Assert.NotNull(document.CanvasEditConnection(
            "e1", "again", CanvasConnectionDirection.Both, owner: tab));
        Assert.NotNull(document.CanvasDeleteConnection("e1", owner: tab));

        // The openers' refusals answer null, the sentence spoken.
        _announced.Clear();
        Assert.Null(document.CanvasDeleteConnection("ghost"));
        Assert.Contains(_announced, s => s.Text.Contains("no connections", StringComparison.OrdinalIgnoreCase));
        Assert.Null(document.CanvasMoveIntoGroup("ghost"));
        document.Shutdown();
    }

    /// <summary>§G2 TG2-0 (G2-2, IG2-21): every front-door verb whose
    /// preparation queries the session CATCHES its own refusal inside
    /// the prepare lambda — the funnel's transaction catches only the
    /// apply's exception, so a placement or lookup that throws would
    /// otherwise escape the gate. Pinned as a SOURCE census: no session
    /// fault is inducible from a real vault (an unknown anchor does not
    /// throw — core places anyway), so the clause is asserted on the
    /// verbs' text, the way the chord scrape asserts the navigator's.</summary>
    [Fact]
    public void EveryQueryingFrontDoorPrepareCatchesItsOwnRefusal()
    {
        CSharpSource source = CSharpSource.Load("Canvas", "CanvasDocumentViewModel.cs");
        foreach (string verb in (string[])["CanvasNewGroup", "CanvasMoveIntoGroup", "CanvasAddFileCard", "CanvasAddLinkCard"])
        {
            Microsoft.CodeAnalysis.SyntaxNode method = source.Method(verb);
            var catches = method.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.CatchClauseSyntax>()
                .Where(c => c.Declaration?.Type.ToString() == "VaultException")
                .ToArray();
            Assert.True(catches.Length >= 1, verb + " prepares without catching VaultException");
            Assert.Contains("CanvasActionFailed", catches[0].Block.ToString());
            Assert.Contains("return null", catches[0].Block.ToString());
        }
    }

    /// <summary>§G2 TG2-0 (G2-3): the surfaced verbs' openers over the
    /// selection — Set Color and Edit Card Text refuse
    /// NothingSelected, Rename Group refuses NotAGroup on a card and on
    /// nothing — each before any sheet.</summary>
    [Fact]
    public void TheSurfacedVerbsOpenersRefuseBeforeAnySheet()
    {
        CanvasDocumentViewModel document = Open();
        int colorRequests = 0;
        int renameRequests = 0;
        int editorRequests = 0;
        document.SetColorRequested += () => colorRequests++;
        document.GroupRenameRequested += (_, _) => renameRequests++;
        document.CardEditorRequested += _ => editorRequests++;
        // Load seats the landing selection (A14); the openers are
        // exercised over NO selection first.
        document.Selection.Selected = null;
        _announced.Clear();

        document.RequestSetColor();
        document.RequestGroupRenameForSelection();
        document.RequestCardEditorForSelection();

        Assert.Equal(0, colorRequests + renameRequests + editorRequests);
        Assert.Equal(
            2, _announced.Count(s => s.Text.Contains("Nothing selected", StringComparison.Ordinal)));
        Assert.Contains(_announced, s => s.Text.Contains("not a group", StringComparison.OrdinalIgnoreCase));

        document.SeatSelectionSilently("a");
        _announced.Clear();
        document.RequestGroupRenameForSelection();
        Assert.Equal(0, renameRequests);
        Assert.Contains(_announced, s => s.Text.Contains("not a group", StringComparison.OrdinalIgnoreCase));

        document.RequestSetColor();
        document.RequestCardEditorForSelection();
        Assert.Equal(1, colorRequests);
        Assert.Equal(1, editorRequests);
        document.SeatSelectionSilently("grp");
        document.RequestGroupRenameForSelection();
        Assert.Equal(1, renameRequests);
        document.Shutdown();
    }

    /// <summary>New Group: core's group defaults at the anchor, the
    /// created group selected, exact-bytes undo.</summary>
    [Fact]
    public void NewGroupCreatesSelectsAndUndoes()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();
        document.CanvasNewGroup("Q3");
        Assert.Contains("\"label\":\"Q3\"", DiskBytes());
        Assert.Contains(_announced, s => s.Text.StartsWith("Created group"));
        Assert.NotNull(document.Selection.Selected);
        Undo(document);
        Assert.Equal(before, DiskBytes());
    }

    /// <summary>Rename Group rides the REAL op (IE-23) and undoes.</summary>
    [Fact]
    public void RenameGroupWritesAnnouncesAndUndoes()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();
        document.CanvasRenameGroup("grp", "Sparks");
        Assert.Contains("\"label\":\"Sparks\"", DiskBytes());
        Assert.Contains(_announced, s => s.Text.StartsWith("Renamed group"));
        Undo(document);
        Assert.Equal(before, DiskBytes());
    }

    /// <summary>Ungroup — the algebra's one group removal: the frame
    /// goes, the CARDS stay, and the positioned restore undoes it.</summary>
    [Fact]
    public void UngroupRemovesTheFrameKeepsCardsAndUndoes()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();
        document.CanvasUngroup("grp");
        string after = DiskBytes();
        Assert.DoesNotContain("Ideas", after);
        Assert.Contains("Alpha", after);
        Assert.Contains(
            _announced,
            s => s.Text.Contains("Ungrouped") || s.Text.StartsWith("Deleted"));
        Undo(document);
        Assert.Equal(before, DiskBytes());
    }

    /// <summary>Move into Group, the Placed arm: core's slot inside
    /// the roomy group commits and speaks the group's label.</summary>
    [Fact]
    public void MoveIntoRoomyGroupCommitsAndAnnounces()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();
        document.SelectNode("a");
        document.CanvasMoveIntoGroup("grp");
        Assert.NotEqual(before, DiskBytes());
        Assert.Contains(_announced, s => s.Text.Contains("Moved into group"));
        Undo(document);
        Assert.Equal(before, DiskBytes());
    }

    /// <summary>The refusal arm: a group too small for one slot whose
    /// inset is OCCUPIED refuses audibly with the label, and nothing
    /// half-happens — no write, no history entry.</summary>
    [Fact]
    public void MoveIntoCrampedGroupRefusesAudiblyAndWritesNothing()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();
        document.SelectNode("a");
        document.CanvasMoveIntoGroup("cramped");
        Assert.Equal(before, DiskBytes());
        Assert.Null(document.UndoStack.OfferedUndo);
        Assert.Contains(
            _announced,
            s => s.Text.Contains("No free space") && s.Text.Contains("Cramped"));
    }

    /// <summary>Set Color: a preset writes and speaks the NAME (t1 —
    /// never the number); an invalid hex never reaches the funnel.</summary>
    [Fact]
    public void SetColorWritesTheNameAndRefusesInvalidHex()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();
        document.SelectNode("a");
        document.CanvasSetColor("1");
        Assert.Contains("\"color\":\"1\"", DiskBytes());
        Assert.Contains(
            _announced,
            s => s.Text.Contains("red") && !s.Text.Contains("\"1\""));
        Undo(document);
        Assert.Equal(before, DiskBytes());

        document.CanvasSetColor("#zz");
        Assert.Equal(before, DiskBytes());
    }

    /// <summary>Connect: one edge, sides from core over the two
    /// rects, the typed confirmation, exact-bytes undo.</summary>
    [Fact]
    public void ConnectAddsASidedEdgeAndUndoes()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();
        document.CanvasConnect("a", "blocker", "supports");
        Assert.Contains("\"label\":\"supports\"", DiskBytes());
        Assert.Contains(_announced, s => s.Text.StartsWith("Connected"));
        Undo(document);
        Assert.Equal(before, DiskBytes());
    }

    /// <summary>Edit Connection: label and end styles change; the
    /// author's SIDES and COLOR survive untouched (IE-24).</summary>
    [Fact]
    public void EditConnectionPreservesSidesAndColor()
    {
        CanvasDocumentViewModel document = Open();
        document.CanvasEditConnection(
            "e1", "renamed", CanvasConnectionDirection.Both);
        string after = DiskBytes();
        Assert.Contains("\"label\":\"renamed\"", after);
        Assert.Contains("\"fromSide\":\"right\"", after);
        Assert.Contains("\"toSide\":\"left\"", after);
        Assert.Contains("\"color\":\"2\"", after);
        Assert.Contains(_announced, s => s.Text.StartsWith("Connection"));
    }

    /// <summary>Delete Connection, both arms: a live edge deletes and
    /// undoes; a MISSING edge refuses AUDIBLY before any apply (the
    /// 0a-2 rule) with bytes untouched.</summary>
    [Fact]
    public void DeleteConnectionRemovesOrRefusesAudibly()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();
        document.CanvasDeleteConnection("e1");
        Assert.DoesNotContain("\"id\":\"e1\"", DiskBytes());
        Undo(document);
        Assert.Equal(before, DiskBytes());

        int spoken = _announced.Count;
        document.CanvasDeleteConnection("ghost-edge");
        Assert.Equal(before, DiskBytes());
        Assert.True(
            _announced.Count > spoken,
            "a missing connection was refused in silence: the lookup must "
            + "answer audibly before any apply.");
    }

    /// <summary>Add Link: a valid URL lands selected; an unparseable
    /// one refuses audibly and never reaches the funnel.</summary>
    [Fact]
    public void AddLinkCardValidatesTheUrl()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();
        document.CanvasAddLinkCard("https://example.org/spec");
        Assert.Contains("example.org", DiskBytes());
        Undo(document);
        Assert.Equal(before, DiskBytes());

        document.CanvasAddLinkCard("not a url");
        Assert.Equal(before, DiskBytes());
        Assert.Contains(_announced, s => s.Text.Contains("link"));
    }

    /// <summary>Add Note + Locate: a file card lands; repointing a
    /// missing target retargets with the typed confirmation.</summary>
    [Fact]
    public void AddFileCardAndLocateRetarget()
    {
        CanvasDocumentViewModel document = Open();
        string before = DiskBytes();
        document.CanvasAddFileCard("missing-note.md", null);
        string? created = document.Selection.Selected;
        Assert.NotNull(created);
        Assert.Contains("missing-note.md", DiskBytes());
        Assert.Contains(
            _announced,
            s => s.Text.StartsWith("Created file card \"missing-note\""));

        document.CanvasLocateFile(created!, "note-0.md");
        Assert.Contains("note-0.md", DiskBytes());
        Assert.DoesNotContain("missing-note.md", DiskBytes());
        Assert.Contains(_announced, s => s.Text.Contains("now points at"));

        Undo(document);
        Undo(document);
        Assert.Equal(before, DiskBytes());
    }

    /// <summary>TE-6: the card picker factory hands back CORE's
    /// proximity order verbatim — no host comparator — with the
    /// excluded id absent and the labels palette-shaped.</summary>
    [Fact]
    public void TheCardPickerFactorySpeaksCoresOrderVerbatim()
    {
        CanvasDocumentViewModel document = Open();
        document.SelectNode("a");
        CanvasCardPickerModel model = document.BuildCardPickerModel("blocker");

        ulong handle = HandleOf(document);
        string[] expected = _session.CanvasProximityOrder(handle, "a", ["blocker"]);
        Assert.Equal(expected, model.Rows.Select(r => r.NodeId));
        Assert.DoesNotContain(model.Rows, r => r.NodeId == "blocker");
        Assert.Contains(
            model.Rows, r => r.Label.StartsWith("Group \"Ideas\", in canvas"));
    }
}
