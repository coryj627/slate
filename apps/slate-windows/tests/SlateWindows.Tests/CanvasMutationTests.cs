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
            		{"id":"blocker","type":"text","text":"Blocker","x":1220,"y":40,"width":260,"height":140}
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
