// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using ICSharpCode.AvalonEdit.Document;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>WB-22 (#1112): the find bar's view model over a headless editor
/// surface — counting, stepping with wrap, the options, Replace on the
/// tracked hit only, the debounced recount and the restore on close.</summary>
public sealed class FindInNoteViewModelTests
{
    private sealed class Rig : IDisposable
    {
        public Rig(string text, int caret = 0)
        {
            Document = new TextDocument(text);
            Surface = new EditorFindSurface(Document, editor: null);
            Surface.SetHeadlessSelection(caret, 0);
            Find = new FindInNoteViewModel(Heard.Add);
            Find.Open();
            Find.AttachSurface(Surface);
        }

        public TextDocument Document { get; }

        public EditorFindSurface Surface { get; }

        public FindInNoteViewModel Find { get; }

        public List<A11yEvent> Heard { get; } = [];

        public List<string> Spoken => Heard.Select(spoken => SlateUniffiMethods.A11yRender(spoken).Text).ToList();

        public void Search(string query)
        {
            Find.Query = query;
            Find.FlushPendingForTests();
        }

        public void Dispose() => Find.Dispose();
    }

    [Fact]
    public void TypingFindsAllMatchesAndReportsOneOfM()
    {
        using var rig = new Rig("a b a");
        rig.Search("a");

        Assert.Equal(2, rig.Find.TotalCount);
        Assert.Equal(1, rig.Find.CurrentIndex);
        Assert.Equal((0, 1), rig.Surface.Selection);
        Assert.Equal(new[] { (0, 1), (4, 1) }, rig.Surface.Highlighted);
        Assert.Equal(new[] { "1 of 2" }, rig.Spoken);
    }

    [Fact]
    public void NextWrapsToTheTopAndSaysSo()
    {
        using var rig = new Rig("a b a");
        rig.Search("a");
        rig.Find.Next();
        Assert.Equal(2, rig.Find.CurrentIndex);
        rig.Heard.Clear();

        rig.Find.Next();

        Assert.Equal(1, rig.Find.CurrentIndex);
        Assert.Equal((0, 1), rig.Surface.Selection);
        Assert.Equal(new[] { "1 of 2. Wrapped to the top" }, rig.Spoken);
    }

    [Fact]
    public void PreviousWrapsToTheEndAndSaysSo()
    {
        using var rig = new Rig("a b a");
        rig.Search("a");
        rig.Heard.Clear();

        rig.Find.Previous();

        Assert.Equal(2, rig.Find.CurrentIndex);
        Assert.Equal((4, 1), rig.Surface.Selection);
        Assert.Equal(new[] { "2 of 2. Wrapped to the end" }, rig.Spoken);
    }

    [Theory]
    [InlineData(false, false, 3)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 1)]
    public void MatchCaseAndWholeWordNarrowTheMatches(bool matchCase, bool wholeWord, int expected)
    {
        using var rig = new Rig("Cat cat category");
        rig.Find.MatchCase = matchCase;
        rig.Find.WholeWord = wholeWord;
        rig.Search("cat");

        Assert.Equal(expected, rig.Find.TotalCount);
    }

    [Fact]
    public void ReplaceReplacesTheCurrentHitAndSteps()
    {
        using var rig = new Rig("a b a");
        rig.Search("a");
        rig.Find.ReplaceText = "q";
        rig.Heard.Clear();

        rig.Find.Replace();

        Assert.Equal("q b a", rig.Document.Text);
        Assert.Equal(1, rig.Find.TotalCount);
        Assert.Equal(1, rig.Find.CurrentIndex);
        Assert.Equal((4, 1), rig.Surface.Selection);
        Assert.Equal(new[] { "1 of 1" }, rig.Spoken);
    }

    [Fact]
    public void AReplaceThatWrapsSaysSo()
    {
        using var rig = new Rig("a b a");
        rig.Search("a");
        rig.Find.Next();
        rig.Find.ReplaceText = "q";
        rig.Heard.Clear();

        rig.Find.Replace();

        Assert.Equal("a b q", rig.Document.Text);
        Assert.Equal((0, 1), rig.Surface.Selection);
        Assert.Equal(new[] { "1 of 1. Wrapped to the top" }, rig.Spoken);
    }

    /// <summary>Codex round 1: a query typed just before a tab switch is
    /// answered on the switch back — the detached tick does not eat it.</summary>
    [Fact]
    public void AQueryPendingAcrossADetachIsAnsweredOnTheReattach()
    {
        using var rig = new Rig("a b a");
        rig.Find.Query = "a";
        rig.Find.AttachSurface(null);
        rig.Find.FlushPendingForTests();
        Assert.Empty(rig.Heard);

        rig.Find.AttachSurface(rig.Surface);

        Assert.Equal(1, rig.Find.CurrentIndex);
        Assert.Equal(2, rig.Find.TotalCount);
        Assert.Equal((0, 1), rig.Surface.Selection);
        Assert.Equal(new[] { "1 of 2" }, rig.Spoken);
    }

    [Fact]
    public void ReplaceAllIsOneUndoStep()
    {
        using var rig = new Rig("a b a");
        rig.Search("a");
        rig.Find.ReplaceText = "q";
        rig.Heard.Clear();

        rig.Find.ReplaceAll();

        Assert.Equal("q b q", rig.Document.Text);
        Assert.Equal("Replaced 2 matches.", rig.Spoken[0]);
        rig.Document.UndoStack.Undo();
        Assert.Equal("a b a", rig.Document.Text);
    }

    [Fact]
    public void ADocumentEditWhileOpenRecountsOnTheDebounce()
    {
        using var rig = new Rig("a b a b a");
        rig.Search("a");
        rig.Find.Next();
        Assert.Equal((4, 1), rig.Surface.Selection);
        rig.Heard.Clear();

        // An edit elsewhere: the tracked hit still validates, its index moves.
        rig.Document.Insert(0, "a ");
        rig.Find.FlushPendingForTests();
        Assert.Equal(4, rig.Find.TotalCount);
        Assert.Equal(3, rig.Find.CurrentIndex);
        Assert.Equal((4, 1), rig.Surface.Selection);

        // An edit that breaks the hit (now at offset 6) clears it.
        rig.Document.Replace(6, 1, "x");
        rig.Find.FlushPendingForTests();
        Assert.Equal(3, rig.Find.TotalCount);
        Assert.Equal(0, rig.Find.CurrentIndex);
        Assert.Equal((4, 1), rig.Surface.Selection);
        Assert.Empty(rig.Heard);
    }

    [Fact]
    public void ReplaceWithNoCurrentHitRevealsTheNextAndWritesNothing()
    {
        using var rig = new Rig("a b a");
        rig.Search("a");
        rig.Document.Replace(0, 1, "x");
        rig.Find.FlushPendingForTests();
        Assert.Equal(0, rig.Find.CurrentIndex);
        rig.Find.ReplaceText = "q";
        rig.Heard.Clear();

        rig.Find.Replace();

        Assert.Equal("x b a", rig.Document.Text);
        Assert.Equal((4, 1), rig.Surface.Selection);
        Assert.Equal(new[] { "1 of 1" }, rig.Spoken);

        rig.Find.Replace();
        Assert.Equal("x b q", rig.Document.Text);
    }

    [Fact]
    public void AnEmptyQueryClearsHighlightsAndCountsNothing()
    {
        using var rig = new Rig("a b a");
        rig.Search("a");
        Assert.Equal(2, rig.Surface.Highlighted.Count);

        rig.Search(string.Empty);

        Assert.Empty(rig.Surface.Highlighted);
        Assert.Equal(0, rig.Find.TotalCount);
        Assert.Equal(0, rig.Find.CurrentIndex);
    }

    [Fact]
    public void CloseRestoresTheCaretAndSelectionTheOpenCaptured()
    {
        using var rig = new Rig("a b a", caret: 2);
        rig.Search("a");
        Assert.Equal((4, 1), rig.Surface.Selection);

        rig.Find.Close();

        Assert.False(rig.Find.IsOpen);
        Assert.Equal((2, 0), rig.Surface.Selection);
        Assert.Empty(rig.Surface.Highlighted);
    }
}
