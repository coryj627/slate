// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 §E TE-7: the card editor's M8 semantics over a real vault —
/// the seed token's binding (IE-4/IE-18), the in-sheet conflict that
/// keeps the draft (IE-19/IE-9), the no-change arm, and the commit.
/// </summary>
public sealed class CanvasCardEditorTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    private readonly List<RenderedAnnouncement> _announced = [];

    public CanvasCardEditorTests()
    {
        _fixture = FixtureVault.Create(1, "canvas-editor");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "board.canvas"),
            """
            {
            	"nodes":[
            		{"id":"a","type":"text","text":"Alpha","x":0,"y":0,"width":260,"height":140}
            	],
            	"edges":[]
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

    /// <summary>The factory hands back the SEED TOKEN whole — the
    /// text and the basis from one locked read — and the holder the
    /// modal flat-read watches.</summary>
    [Fact]
    public void TheFactorySeedsTextAndBasisTogether()
    {
        CanvasDocumentViewModel document = Open();
        CanvasCardEditorViewModel editor = document.OpenCardEditor("a")!;
        Assert.Equal("Alpha", editor.Draft);
        Assert.Equal(document.PublishedBasis, editor.SeedBasis);

        // A missing node refuses with mac's arm and opens nothing.
        Assert.Null(document.OpenCardEditor("ghost"));
    }

    /// <summary>M8's no-change arm: Escape commits NOTHING for an
    /// untouched buffer — "No changes.", no history entry, the sheet
    /// may close.</summary>
    [Fact]
    public void AnUntouchedBufferSpeaksNoChangesAndWritesNothing()
    {
        CanvasDocumentViewModel document = Open();
        CanvasCardEditorViewModel editor = document.OpenCardEditor("a")!;
        Assert.True(editor.CommitOnEscape());
        Assert.Contains(_announced, s => s.Text == "No changes.");
        Assert.Null(document.UndoStack.OfferedUndo);
    }

    /// <summary>Escape COMMITS a changed draft — one SetNodeContent,
    /// the update confirmation, the entry recorded.</summary>
    [Fact]
    public void EscapeCommitsTheChangedDraft()
    {
        CanvasDocumentViewModel document = Open();
        CanvasCardEditorViewModel editor = document.OpenCardEditor("a")!;
        editor.Draft = "Alpha, revised in the sheet";
        Assert.True(editor.CommitOnEscape());
        Assert.Contains(
            "Alpha, revised in the sheet",
            File.ReadAllText(Path.Combine(_fixture.Root, "board.canvas")));
        Assert.NotNull(document.UndoStack.OfferedUndo);
    }

    /// <summary>IE-18/IE-19: a draft whose seed basis the document no
    /// longer holds does NOT apply — the conflict surfaces IN the
    /// sheet, the draft survives, and the sheet refuses to close.</summary>
    [Fact]
    public void AMovedBasisConflictsInTheSheetAndKeepsTheDraft()
    {
        CanvasDocumentViewModel document = Open();
        CanvasCardEditorViewModel editor = document.OpenCardEditor("a")!;
        editor.Draft = "typed against the old revision";

        // The document moves under the draft: another verb commits.
        document.SelectNode("a");
        document.CanvasSetColor("2");
        Assert.NotEqual(document.PublishedBasis, editor.SeedBasis);

        Assert.False(
            editor.CommitOnEscape(),
            "the sheet closed over a draft whose basis moved (IE-19).");
        Assert.True(editor.Conflicted);
        Assert.Equal("typed against the old revision", editor.Draft);
        Assert.Contains(_announced, s => s.Text.Contains("conflict") || s.Text.Contains("changed"));
        Assert.DoesNotContain(
            "typed against the old revision",
            File.ReadAllText(Path.Combine(_fixture.Root, "board.canvas")));
    }
}
