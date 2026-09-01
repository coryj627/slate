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
            "{\n\t\"nodes\":[\n\t\t{\"id\":\"a\",\"type\":\"text\",\"text\":\"Alpha\","
            + "\"x\":0,\"y\":0,\"width\":260,\"height\":140}\n\t],\n\t\"edges\":[]\n}\n");
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

    private void Undo(CanvasDocumentViewModel document)
    {
        CanvasHistorySnapshot snapshot = document.UndoStack.SnapshotUndo()!;
        Assert.True(document.UndoStack.TryCheckOut(snapshot));
        CanvasApplyResult result = _session.CanvasApply(
            document.AppliedPublication!.Loaded!.Lease
                is { } _ ? HandleOf(document) : 0,
            snapshot.Entry.Inverse);
        document.UndoStack.CommitCheckout(
            new CanvasHistoryEntry(
                snapshot.Entry.Name, result.Inverse, result.NewContentHash),
            result.NewContentHash);
    }

    private static ulong HandleOf(CanvasDocumentViewModel document)
    {
        ulong handle = 0;
        CanvasHandleLease lease = document.AppliedPublication!.Loaded!.Lease;
        Assert.True(lease.Invoke(() => true, h => handle = h));
        return handle;
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
}
