// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Search;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-22 (#1112) PR 2: the reading view as a find surface. The search runs
/// over the text the surface's range holds (math blocks by their source,
/// the owner's call of 2026-10-26), the current match is the surface's
/// selection, and the matches are drawn in the render scope's own adorner
/// layer — the reading document is never touched.
/// </summary>
public sealed class ReadingFindSurfaceTests
{
    /// <summary>One note in reading mode, its surface bound and hosted in a
    /// shown window, with its find surface; the diagram stage gated on
    /// request.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly FixtureVault _fixture;
        private readonly CancelToken _cancel;
        private readonly Window _window;

        public Harness(string name, string markdown, bool gated = false, int notes = 1, string? live = null)
        {
            _fixture = FixtureVault.Create(notes, name);
            File.WriteAllText(Path.Combine(_fixture.Root, "note0.md"), markdown);
            Session = VaultSession.OpenFilesystem(_fixture.Root);
            _cancel = new CancelToken();
            Session.ScanInitial(_cancel);
            Tab = new WorkspaceTabViewModel(
                Session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                announce: Heard.Add,
                startInteractionBackgroundWork: false);
            if (gated)
            {
                Tab.ReadingCreatedForTests = model => model.DiagramStageGateForTests = Gate;
            }
            if (live is not null)
            {
                // An unsaved edit: the saved file's math artifacts no longer
                // match the live source.
                Tab.Text = live;
            }
            Tab.ToggleViewMode();
            Surface = new ReadingSurface { Model = Tab.Reading };
            _window = new Window
            {
                Content = Surface,
                Width = 600,
                Height = 400,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -2000,
                Top = -2000,
            };
            _window.Show();
            _window.UpdateLayout();
            PumpedDispatcher.Drain();
            Find = new ReadingFindSurface(Surface);
        }

        public VaultSession Session { get; }

        public WorkspaceTabViewModel Tab { get; }

        public ReadingSurface Surface { get; }

        public ReadingFindSurface Find { get; }

        public ManualResetEventSlim Gate { get; } = new(false);

        public List<A11yEvent> Heard { get; } = [];

        public ReadingContentViewModel Model => Tab.Reading!;

        public string IndexText => Find.Text.Text;

        public List<(int Offset, int Length)> Matches(string query) => SearchStrategyFactory
            .Create(query, ignoreCase: true, matchWholeWords: false, SearchMode.Normal)
            .FindAll(Find.Text, 0, Find.Text.TextLength)
            .Select(result => (result.Offset, result.Length))
            .ToList();

        public void Settle()
        {
            _window.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        /// <summary>Releases the gate and pumps until the stage landed.</summary>
        public void Land()
        {
            Gate.Set();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => !Model.DiagramsPendingForTests, TimeSpan.FromSeconds(30)),
                "the diagram stage never landed");
            PumpedDispatcher.Drain();
        }

        public FindMatchAdorner? Adorner()
        {
            FrameworkElement scope = Assert.IsAssignableFrom<FrameworkElement>(Surface.DocumentRenderScope);
            return AdornerLayer.GetAdornerLayer(scope)?.GetAdorners(scope)?.OfType<FindMatchAdorner>().SingleOrDefault();
        }

        public void Dispose()
        {
            Gate.Set();
            Find.Detach();
            _window.Close();
            Tab.Dispose();
            Session.Dispose();
            _cancel.Dispose();
            _fixture.Dispose();
        }
    }

    private const string IndexNote =
        "# Alpha heading\n\n"
        + "1. first item\n2. second item\n\n"
        + "A [link text](note0.md) here and **fo**o end\n\n"
        + "beta starts here.\n\n"
        + "| col | two |\n| --- | --- |\n| cell | data |\n\n"
        + "- [ ] task words\n\n"
        + "$$\\nabla^2 f$$\n\n"
        + "$$\\mathbb{R}$$\n";

    [Fact]
    public void TheIndexCoversTheSurfaceRangeAndNothingEmbedded() => RunSta(() =>
    {
        using var harness = new Harness("reading-find-index", IndexNote);
        Assert.True(harness.Find.IsSearchable, "premise: the projection applied");

        // The range's text, each math paragraph's speech standing in its
        // source's place, is the index's text (whitespace and the embedded
        // element's U+FFFC aside).
        var expected = new StringBuilder();
        List<ReadingMathElement> math = [];
        foreach (Block block in harness.Surface.Document.Blocks)
        {
            if (block is Paragraph paragraph && ReadingSemantics.IsMathBlock(paragraph))
            {
                ReadingMathElement element = paragraph.Inlines.OfType<InlineUIContainer>()
                    .Select(container => container.Child).OfType<ReadingMathElement>().Single();
                math.Add(element);
                expected.Append(element.Source);
                Assert.DoesNotContain(ReadingSemantics.MathSpeechOf(paragraph), harness.IndexText, StringComparison.Ordinal);
            }
            else
            {
                expected.Append(new TextRange(block.ContentStart, block.ContentEnd).Text);
            }
        }
        Assert.Equal(2, math.Count);
        Assert.Equal(Squeeze(expected.ToString()), Squeeze(harness.IndexText));

        // A word split across formatting runs is one word, and the selection
        // a reveal makes reads it.
        (int offset, int length) = Assert.Single(harness.Matches("foo"));
        harness.Find.Reveal(offset, length);
        Assert.Equal("foo", harness.Surface.Selection.Text);
        Assert.Single(harness.Matches("link text"));
        Assert.Single(harness.Matches("second item"));
        Assert.Single(harness.Matches("cell"));

        // A math block is found by its source and marked whole.
        (int nabla, int nablaLength) = Assert.Single(harness.Matches("nabla"));
        Assert.Same(math[0], harness.Find.UnitAt(nabla));
        harness.Find.Reveal(nabla, nablaLength);
        var container = (InlineUIContainer)math[0].Parent;
        // The selection covers the element and no text beside it (WPF snaps
        // its ends to the insertion positions around the element's tags).
        TextSelection selection = harness.Surface.Selection;
        Assert.True(selection.Start.CompareTo(container.ElementStart) <= 0, "the selection starts inside the math element");
        Assert.True(selection.End.CompareTo(container.ElementEnd) >= 0, "the selection ends inside the math element");
        Assert.Equal(string.Empty, new TextRange(selection.Start, container.ElementStart).Text);
        Assert.Equal(string.Empty, new TextRange(container.ElementEnd, selection.End).Text);
        Assert.Single(harness.Matches("mathbb"));

        // No match spans a block boundary: one "\n" stands between blocks.
        Assert.Contains("foo end\nbeta starts", harness.IndexText, StringComparison.Ordinal);
        Assert.Empty(harness.Matches("endbeta"));
        Assert.Empty(harness.Matches("end beta"));
        Assert.Empty(harness.Matches("heading 1"));
    });

    public static TheoryData<string> ContentPaths => ["merge", "switch", "append", "completion", "rebind"];

    [Theory]
    [MemberData(nameof(ContentPaths))]
    public void ContentChangedFiresOnceForEachContentPath(string path) => RunSta(() =>
    {
        using var harness = new Harness(
            "reading-find-content-" + path,
            path == "append" ? LargeNote() : DiagramNote,
            gated: path == "completion",
            notes: 2);
        ReadingSurface surface = harness.Surface;
        List<ReadingContentChange> heard = [];
        surface.ContentChanged += heard.Add;
        int revision = surface.ContentRevision;

        switch (path)
        {
            case "merge":
                surface.ApplyBuiltDocument(Build("# Merged\n\nalpha\n"));
                Assert.Equal([ReadingContentChange.Replaced], heard);
                Assert.True(surface.ShowsSearchableContent);
                break;
            case "switch":
                {
                    var tab = new WorkspaceTabViewModel(
                        harness.Session,
                        new WorkspaceTabState(
                            Guid.NewGuid(),
                            new WorkspaceItemState(WorkspaceItemKind.Markdown, "note1.md")),
                        startInteractionBackgroundWork: false);
                    using var model = new ReadingContentViewModel(harness.Session, tab, _ => { });
                    surface.Model = model;
                    Assert.Equal([ReadingContentChange.Replaced], heard);
                    Assert.False(surface.ShowsSearchableContent, "the placeholder is not searchable");
                    surface.Model = null;
                    tab.Dispose();
                    break;
                }
            case "append":
                {
                    var tab = new WorkspaceTabViewModel(
                        harness.Session,
                        new WorkspaceTabState(
                            Guid.NewGuid(),
                            new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                        startInteractionBackgroundWork: false);
                    using var model = new ReadingContentViewModel(harness.Session, tab, _ => { });
                    int fragments = 0;
                    model.BlocksAppended += _ => fragments++;
                    surface.Model = model;
                    heard.Clear();
                    revision = surface.ContentRevision;
                    model.Refresh();
                    Assert.True(PumpedDispatcher.PumpUntil(() => model.ProjectionComplete, TimeSpan.FromSeconds(60)), "the stream never finished");
                    PumpedDispatcher.Drain();
                    Assert.True(fragments > 0, "premise: the note streamed");
                    Assert.Equal(fragments, heard.Count(change => change == ReadingContentChange.Appended));
                    Assert.Equal(1, heard.Count(change => change == ReadingContentChange.Replaced));
                    surface.Model = null;
                    tab.Dispose();
                    break;
                }
            case "completion":
                harness.Land();
                Assert.Equal([ReadingContentChange.CompletedInPlace], heard);
                break;
            case "rebind":
                {
                    ReadingContentViewModel model = harness.Model;
                    Assert.True(model.ProjectionComplete, "premise: the projection is complete");
                    surface.Model = null;
                    surface.Model = model;
                    Assert.Empty(heard);
                    break;
                }
        }
        Assert.Equal(revision + heard.Count, surface.ContentRevision);
    });

    [Fact]
    public void TheCurrentMatchIsTheSelectionAndIsBroughtIntoView() => RunSta(() =>
    {
        var note = new StringBuilder("needle at the top.\n\n");
        for (int i = 0; i < 80; i++)
        {
            note.Append("Filler paragraph ").Append(i).Append(".\n\n");
        }
        note.Append("The second needle is far down.\n");
        using var harness = new Harness("reading-find-reveal", note.ToString());
        IInputElement? focused = Keyboard.FocusedElement;
        List<(int Offset, int Length)> matches = harness.Matches("needle");
        Assert.Equal(2, matches.Count);

        harness.Find.Reveal(matches[1].Offset, matches[1].Length);
        harness.Settle();

        Assert.Equal("needle", harness.Surface.Selection.Text);
        Assert.Equal(matches[1], harness.Find.Selection);
        Rect rect = harness.Surface.Selection.Start.GetCharacterRect(LogicalDirection.Forward);
        // A scroll brings the match just inside the viewport's edge.
        Assert.True(rect.Top >= 0 && rect.Bottom <= harness.Surface.ActualHeight + 0.5, $"the match is outside the viewport: {rect}; height {harness.Surface.ActualHeight}; viewport {harness.Surface.ContentScrollViewer?.ViewportHeight}; offset {harness.Surface.ContentScrollViewer?.VerticalOffset}");
        Assert.Same(focused, Keyboard.FocusedElement);
    });

    [Fact]
    public void TheAdornerHasNoPeerAndLivesInTheSurfacesOwnLayer() => RunSta(() =>
    {
        using var harness = new Harness("reading-find-adorner", "alpha one\n\nalpha two\n");
        List<(int Offset, int Length)> matches = harness.Matches("alpha");
        harness.Find.Highlight(matches, 0);
        harness.Settle();

        FindMatchAdorner adorner = Assert.IsType<FindMatchAdorner>(harness.Adorner());
        Assert.Equal(2, adorner.Count);
        Assert.Null(UIElementAutomationPeer.CreatePeerForElement(adorner));
        AdornerLayer layer = Assert.IsType<AdornerLayer>(VisualTreeHelper.GetParent(adorner));
        Assert.IsType<ScrollContentPresenter>(VisualTreeHelper.GetParent(layer));
        Assert.True(harness.Surface.IsAncestorOf(layer), "the layer is not the surface's own");
        Assert.False(adorner.IsHitTestVisible);
        Assert.False(adorner.Focusable);

        harness.Find.ClearHighlights();
        Assert.Null(harness.Adorner());
    });

    /// <summary>The owner's call of 2026-10-26: a match in a math block's
    /// source is drawn as one ring around the whole math element, never
    /// over the hidden speech run.</summary>
    [Fact]
    public void AMathMatchRingsTheWholeMathElement() => RunSta(() =>
    {
        using var harness = new Harness("reading-find-math-ring", "Words before.\n\n$$\\nabla^2 f$$\n");
        ReadingMathElement element = Assert.Single(LogicalDescendants<ReadingMathElement>(harness.Surface.Document));
        (int offset, int length) = Assert.Single(harness.Matches("nabla"));
        UIElement scope = Assert.IsAssignableFrom<UIElement>(harness.Surface.DocumentRenderScope);

        Rect ring = Assert.Single(harness.Find.RectsOf(offset, length, scope, harness.Surface.TransformToDescendant(scope)));

        Assert.Equal(element.TransformToVisual(scope).TransformBounds(new Rect(element.RenderSize)), ring);
        harness.Find.Highlight([(offset, length)], 0);
        harness.Settle();
        Assert.Equal(1, Assert.IsType<FindMatchAdorner>(harness.Adorner()).Count);
    });

    /// <summary>Red team: a math block with no core artifact (an unsaved
    /// formula edit) shows its fenced source as a visible run and an empty
    /// element; a match is found, selected and ringed in that visible run,
    /// never on the empty element.</summary>
    [Fact]
    public void AnUnmatchedMathBlockIsFoundInItsVisibleSource() => RunSta(() =>
    {
        using var harness = new Harness(
            "reading-find-math-unmatched",
            "Words before.\n\n$$\\nabla^2 f$$\n",
            live: "Words before.\n\n$$\\alpha^2 f$$\n");
        Paragraph math = Assert.Single(harness.Surface.Document.Blocks.OfType<Paragraph>(), ReadingSemantics.IsMathBlock);
        Assert.Null(ReadingSemantics.MathCopyTextOf(math));

        (int offset, int length) = Assert.Single(harness.Matches("alpha"));
        Assert.Null(harness.Find.UnitAt(offset));
        harness.Find.Reveal(offset, length);
        harness.Settle();

        Assert.Equal("alpha", harness.Surface.Selection.Text);
        UIElement scope = Assert.IsAssignableFrom<UIElement>(harness.Surface.DocumentRenderScope);
        Rect ring = Assert.Single(harness.Find.RectsOf(offset, length, scope, harness.Surface.TransformToDescendant(scope)));
        Assert.True(ring.Width > 4 && ring.Height > 4, $"the ring is a dot: {ring}");
    });

    /// <summary>Red team: a mode switch asks nothing — a query the loading
    /// reading view held is not answered on the editor, whose caret stays
    /// and which speaks nothing.</summary>
    [Fact]
    public void AModeSwitchAsksNothingWhileTheReadingViewLoads() => RunSta(() =>
    {
        using var harness = new Harness("reading-find-switch-waiting", "The reading note.\n", notes: 2);
        var tab = new WorkspaceTabViewModel(
            harness.Session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
            startInteractionBackgroundWork: false);
        using var model = new ReadingContentViewModel(harness.Session, tab, _ => { });
        harness.Surface.Model = model;
        var heard = new List<A11yEvent>();
        using var find = new FindInNoteViewModel(heard.Add);
        find.Open();
        find.AttachSurface(new ReadingFindSurface(harness.Surface));
        find.Query = "reading";
        find.FlushPendingForTests();
        Assert.Equal([FindInNotePhrase.WaitingForReadingView], Spoken(heard));

        var document = new ICSharpCode.AvalonEdit.Document.TextDocument("the reading note");
        var editor = new EditorFindSurface(document, editor: null);
        find.Mode = FindInNoteMode.Editor;
        find.AttachSurface(editor);
        find.FlushPendingForTests();

        Assert.Equal([FindInNotePhrase.WaitingForReadingView], Spoken(heard));
        Assert.Equal((0, 0), editor.Selection);
        Assert.Equal(1, find.TotalCount);
        find.AttachSurface(null);
        harness.Surface.Model = null;
        tab.Dispose();
    });

    /// <summary>Codex round 1: the bar re-attaches inside the Mode change's
    /// notification, so a mode switch asks nothing only if its questions are
    /// dropped before that notification — a query the loading reading view
    /// held, a query typed inside the debounce, and (on an in-place change of
    /// item) a held query are never answered on the next surface.</summary>
    [Fact]
    public void AModeSwitchAsksNothingEvenWhenTheBarReattachesInItsNotification() => RunSta(() =>
    {
        using var harness = new Harness("reading-find-switch-notified", "The reading note.\n", notes: 2);
        var tab = new WorkspaceTabViewModel(
            harness.Session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
            startInteractionBackgroundWork: false);
        using var model = new ReadingContentViewModel(harness.Session, tab, _ => { });
        harness.Surface.Model = model;
        var loading = new ReadingFindSurface(harness.Surface);
        var heard = new List<A11yEvent>();
        using var find = new FindInNoteViewModel(heard.Add);
        var editor = new EditorFindSurface(new ICSharpCode.AvalonEdit.Document.TextDocument("the reading note"), editor: null);
        // The bar's shape: it re-attaches synchronously on the Mode change.
        find.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FindInNoteViewModel.Mode))
            {
                find.AttachSurface(find.Mode == FindInNoteMode.Editor ? editor : loading);
            }
        };
        find.Mode = FindInNoteMode.Reading;
        find.Open();
        find.AttachSurface(loading);
        find.Query = "reading";
        find.FlushPendingForTests();
        Assert.Equal([FindInNotePhrase.WaitingForReadingView], Spoken(heard));

        // A held question.
        find.Mode = FindInNoteMode.Editor;
        find.FlushPendingForTests();
        Assert.Equal([FindInNotePhrase.WaitingForReadingView], Spoken(heard));
        Assert.Equal((0, 0), editor.Selection);
        Assert.Equal(1, find.TotalCount);

        // A query typed inside the debounce.
        find.Query = "note";
        find.Mode = FindInNoteMode.Reading;
        find.Mode = FindInNoteMode.Editor;
        find.FlushPendingForTests();
        Assert.Equal([FindInNotePhrase.WaitingForReadingView], Spoken(heard));
        Assert.Equal((0, 0), editor.Selection);

        // An in-place change of item, in the same mode, drops a held question
        // too: the next note's projection answers nothing.
        find.Mode = FindInNoteMode.Reading;
        find.Query = "reading";
        find.FlushPendingForTests();
        Assert.Equal(2, Spoken(heard).Count);
        find.CloseForModeSwitch();
        model.Refresh();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => harness.Surface.ShowsSearchableContent, TimeSpan.FromSeconds(30)),
            "the projection never applied");
        find.Open();
        find.AttachSurface(new ReadingFindSurface(harness.Surface));
        find.FlushPendingForTests();
        Assert.Equal(2, Spoken(heard).Count);
        Assert.True(harness.Surface.Selection.IsEmpty, "the next note's projection answered the held question");

        find.AttachSurface(null);
        harness.Surface.Model = null;
        tab.Dispose();
    });

    /// <summary>Codex round 1: a step pressed after the projection applied
    /// but before the first recount speaks one line: it answers the held
    /// question itself.</summary>
    [Fact]
    public void AStepBeforeTheFirstRecountSpeaksOnce() => RunSta(() =>
    {
        using var harness = new Harness("reading-find-step-waiting", "One reading and two reading.\n", notes: 2);
        var tab = new WorkspaceTabViewModel(
            harness.Session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
            startInteractionBackgroundWork: false);
        using var model = new ReadingContentViewModel(harness.Session, tab, _ => { });
        harness.Surface.Model = model;
        var surface = new ReadingFindSurface(harness.Surface);
        var heard = new List<A11yEvent>();
        using var find = new FindInNoteViewModel(heard.Add);
        find.Open();
        find.AttachSurface(surface);
        find.Query = "reading";
        find.FlushPendingForTests();
        model.Refresh();
        Assert.True(PumpedDispatcher.PumpUntil(() => surface.IsSearchable, TimeSpan.FromSeconds(30)), "the projection never applied");

        find.Next();

        Assert.Equal([FindInNotePhrase.WaitingForReadingView, "1 of 2"], Spoken(heard));
        Assert.Equal(1, find.CurrentIndex);
        find.AttachSurface(null);
        harness.Surface.Model = null;
        tab.Dispose();
    });

    private const string MathAndDiagramNote =
        "Words before.\n\n$$q + q$$\n\n```mermaid\nweirdDiagram\nstuff\n```\n";

    /// <summary>Codex round 1: a diagram completing elsewhere leaves the
    /// current math match current — the whole-element selection still says
    /// which of the element's matches it is.</summary>
    [Fact]
    public void ADiagramCompletingElsewhereKeepsTheCurrentMathMatch() => RunSta(() =>
    {
        using var harness = new Harness("reading-find-math-completion", MathAndDiagramNote, gated: true);
        var heard = new List<A11yEvent>();
        using var find = new FindInNoteViewModel(heard.Add);
        find.Open();
        find.AttachSurface(harness.Find);
        find.Query = "q";
        find.FlushPendingForTests();
        Assert.Equal((1, 2), (find.CurrentIndex, find.TotalCount));

        harness.Land();
        find.FlushPendingForTests();
        Assert.Equal(1, find.CurrentIndex);

        find.Next();
        Assert.Equal(2, find.CurrentIndex);
        Assert.Equal("2 of 2", Spoken(heard)[^1]);
        find.AttachSurface(null);
    });

    /// <summary>Codex round 1: a restore point taken inside a diagram that
    /// then completes in place is dropped, as a replacement drops one — the
    /// completion rewrote the runs its pointers were in.</summary>
    [Fact]
    public void ARestorePointInsideACompletingDiagramIsDropped() => RunSta(() =>
    {
        using var harness = new Harness(
            "reading-find-restore-completion",
            "Before words.\n\n```mermaid\nweirdDiagram\nalpha\n```\n\nAfter words.\n",
            gated: true);
        (int offset, int length) = Assert.Single(harness.Matches("alpha"));
        harness.Find.Reveal(offset, length);
        Assert.Equal("alpha", harness.Surface.Selection.Text);
        var heard = new List<A11yEvent>();
        using var find = new FindInNoteViewModel(heard.Add);
        find.Open();
        find.AttachSurface(harness.Find);
        find.Query = "after";
        find.FlushPendingForTests();
        Assert.Equal("After", harness.Surface.Selection.Text);

        harness.Land();
        find.FlushPendingForTests();
        find.Close();

        Assert.Equal("After", harness.Surface.Selection.Text);
    });

    /// <summary>Codex round 2: a pane split or collapse rebuilds the reading
    /// surface while the tab's find bar stays open. The restore point taken
    /// on the old surface never selects in the new one's document: the new
    /// surface takes its own, and Close neither throws nor moves its
    /// selection.</summary>
    [Fact]
    public void ARestorePointFromAnotherReadingSurfaceIsNeverRestoredInto() => RunSta(() =>
    {
        using var harness = new Harness("reading-find-rebuilt-surface", "alpha one\n\nalpha two\n");
        var heard = new List<A11yEvent>();
        using var find = new FindInNoteViewModel(heard.Add);
        find.Open();
        find.AttachSurface(harness.Find);
        find.Query = "alpha";
        find.FlushPendingForTests();

        // The rebuilt surface, its replacement count made equal to the old one's.
        var rebuilt = new ReadingSurface();
        var window = new Window { Content = rebuilt, Width = 600, Height = 400, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -2000, Top = -2000 };
        window.Show();
        try
        {
            do
            {
                rebuilt.ApplyBuiltDocument(Build("alpha three\n\nalpha four\n"));
            }
            while (rebuilt.ReplacedRevision < harness.Surface.ReplacedRevision);
            Assert.Equal(harness.Surface.ReplacedRevision, rebuilt.ReplacedRevision);
            window.UpdateLayout();
            int caret = rebuilt.Document.ContentStart.GetOffsetToPosition(rebuilt.CaretPosition);
            var surface = new ReadingFindSurface(rebuilt);
            find.AttachSurface(surface);

            find.Close();

            Assert.False(find.IsOpen);
            Assert.True(rebuilt.Selection.IsEmpty);
            Assert.Equal(caret, rebuilt.Document.ContentStart.GetOffsetToPosition(rebuilt.CaretPosition));
            surface.Detach();
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>Codex round 2: a math element the viewport's bottom edge cuts
    /// still has every match in its source drawn — the visible range runs to
    /// the end of a unit it ends inside.</summary>
    [Fact]
    public void AMathElementCutByTheViewportKeepsEveryMatchVisible() => RunSta(() =>
    {
        var note = new StringBuilder();
        for (int i = 0; i < 40; i++)
        {
            note.Append("Filler paragraph ").Append(i).Append(".\n\n");
        }
        note.Append("$$q + q + q$$\n\n");
        for (int i = 0; i < 40; i++)
        {
            note.Append("Tail paragraph ").Append(i).Append(".\n\n");
        }
        using var harness = new Harness("reading-find-math-cut", note.ToString());
        ReadingMathElement element = Assert.Single(LogicalDescendants<ReadingMathElement>(harness.Surface.Document));
        List<(int Offset, int Length)> matches = harness.Matches("q");
        Assert.Equal(3, matches.Count);
        ScrollViewer viewer = Assert.IsType<ScrollViewer>(harness.Surface.ContentScrollViewer);

        double top = element.TransformToVisual(harness.Surface).Transform(new Point()).Y;
        viewer.ScrollToVerticalOffset(viewer.VerticalOffset + top + (element.ActualHeight / 2) - harness.Surface.ActualHeight);
        harness.Settle();
        double cutTop = element.TransformToVisual(harness.Surface).Transform(new Point()).Y;
        Assert.True(
            cutTop < harness.Surface.ActualHeight && cutTop + element.ActualHeight > harness.Surface.ActualHeight,
            $"premise: the viewport's bottom cuts the math element (top {cutTop}, height {element.ActualHeight}, viewport {harness.Surface.ActualHeight})");

        (int first, int last) = harness.Find.VisibleOffsets();

        Assert.True(first <= matches[0].Offset, $"visible from {first}");
        Assert.True(last >= matches[2].Offset, $"visible to {last}, the last match at {matches[2].Offset}");
    });

    /// <summary>Codex round 3: the bar builds a new find surface on every
    /// attach (a mode round trip, a tab round trip). The math match a reveal
    /// made current stays current across them while the selection is still
    /// the one the reveal made: the memory lives on the reading surface.</summary>
    [Fact]
    public void AReattachKeepsTheCurrentMathMatch() => RunSta(() =>
    {
        using var harness = new Harness("reading-find-math-reattach", "Words before.\n\n$$q + q$$\n");
        var heard = new List<A11yEvent>();
        using var find = new FindInNoteViewModel(heard.Add);
        find.Open();
        find.AttachSurface(harness.Find);
        find.Query = "q";
        find.FlushPendingForTests();
        Assert.Equal((1, 2), (find.CurrentIndex, find.TotalCount));

        find.AttachSurface(null);
        var again = new ReadingFindSurface(harness.Surface);
        find.AttachSurface(again);
        Assert.Equal(1, find.CurrentIndex);

        find.Next();
        Assert.Equal(2, find.CurrentIndex);
        Assert.Equal("2 of 2", Spoken(heard)[^1]);
        find.AttachSurface(null);
    });

    /// <summary>Codex round 3: the visible range is logical — whole visible
    /// lines — so a right-to-left line, whose top-left point is its logical
    /// end, keeps its matches drawn.</summary>
    [Fact]
    public void ARightToLeftLineKeepsItsMatchesVisible() => RunSta(() =>
    {
        const string hello = "\u0645\u0631\u062D\u0628\u0627";
        using var harness = new Harness(
            "reading-find-rtl",
            hello + " \u0628\u0627\u0644\u0639\u0627\u0644\u0645 " + hello + "\n");
        List<(int Offset, int Length)> matches = harness.Matches(hello);
        Assert.Equal(2, matches.Count);

        (int first, int last) = harness.Find.VisibleOffsets();

        Assert.True(first <= matches[0].Offset && last >= matches[1].Offset, $"visible {first}..{last}; matches at {matches[0].Offset} and {matches[1].Offset}");
        harness.Find.Highlight(matches, 0);
        harness.Settle();
        Assert.Equal(2, Assert.IsType<FindMatchAdorner>(harness.Adorner()).Count);
    });

    /// <summary>Codex round 3: a match running across a change of direction
    /// is ringed around every glyph it holds, not between its two logical
    /// ends (which need not bound it visually).</summary>
    [Fact]
    public void AMixedDirectionMatchIsRingedAroundEveryGlyph() => RunSta(() =>
    {
        using var harness = new Harness("reading-find-bidi", "abc \u05D0\u05D1\u05D2\u05D3 def\n");
        (int offset, int length) = Assert.Single(harness.Matches("c \u05D0\u05D1"));
        UIElement scope = Assert.IsAssignableFrom<UIElement>(harness.Surface.DocumentRenderScope);
        GeneralTransform transform = harness.Surface.TransformToDescendant(scope)!;

        Rect ring = Assert.Single(harness.Find.RectsOf(offset, length, scope, transform));

        for (int i = 0; i < length; i++)
        {
            TextPointer start = harness.Find.Index.StartOf(offset + i);
            TextPointer end = harness.Find.Index.EndOf(offset + i + 1);
            Rect glyph = transform.TransformBounds(Rect.Union(
                start.GetCharacterRect(LogicalDirection.Forward),
                end.GetCharacterRect(LogicalDirection.Backward)));
            Assert.True(
                glyph.Left >= ring.Left - 0.5 && glyph.Right <= ring.Right + 0.5,
                $"glyph {i} at {glyph.Left:F1}..{glyph.Right:F1} lies outside the ring {ring.Left:F1}..{ring.Right:F1}");
        }
    });

    /// <summary>Codex round 4: in decomposed text a match can end (or start)
    /// inside a grapheme, where WPF snaps a selection. The reveal selects the
    /// whole graphemes the match touches, and the surface still knows which
    /// match that selection is, so Next moves on.</summary>
    [Fact]
    public void AMatchInsideAGraphemeStepsOn() => RunSta(() =>
    {
        using var harness = new Harness("reading-find-grapheme", "Cafe\u0301 and cafe\u0301 again.\n");
        Assert.Contains("e\u0301", harness.IndexText, StringComparison.Ordinal);
        List<(int Offset, int Length)> matches = harness.Matches("e");
        Assert.Equal(2, matches.Count);
        var heard = new List<A11yEvent>();
        using var find = new FindInNoteViewModel(heard.Add);
        find.Open();
        find.AttachSurface(harness.Find);
        find.Query = "e";
        find.FlushPendingForTests();
        Assert.Equal(1, find.CurrentIndex);
        Assert.Equal("e\u0301", harness.Surface.Selection.Text);
        AssertTheRingEnclosesTheSelection(harness, matches[0]);

        find.Next();

        Assert.Equal(2, find.CurrentIndex);
        Assert.Equal(matches[1], harness.Find.Selection);
        Assert.Equal("e\u0301", harness.Surface.Selection.Text);
        find.Next();
        Assert.Equal(1, find.CurrentIndex);

        // A combining-mark-only search steps the same way.
        find.Query = "\u0301";
        find.FlushPendingForTests();
        Assert.Equal(2, find.TotalCount);
        find.Next();
        Assert.Equal(2, find.CurrentIndex);
        AssertTheRingEnclosesTheSelection(harness, harness.Matches("\u0301")[1]);
        find.AttachSurface(null);
    });

    /// <summary>The match's ring encloses what the reveal selected: the whole
    /// graphemes a match inside one touches, never a zero-width stroke.</summary>
    private static void AssertTheRingEnclosesTheSelection(Harness harness, (int Offset, int Length) match)
    {
        harness.Settle();
        UIElement scope = Assert.IsAssignableFrom<UIElement>(harness.Surface.DocumentRenderScope);
        GeneralTransform transform = harness.Surface.TransformToDescendant(scope)!;
        Rect ring = Assert.Single(harness.Find.RectsOf(match.Offset, match.Length, scope, transform));
        Rect selected = transform.TransformBounds(Rect.Union(
            harness.Surface.Selection.Start.GetCharacterRect(LogicalDirection.Forward),
            harness.Surface.Selection.End.GetCharacterRect(LogicalDirection.Backward)));
        Assert.True(selected.Width > 1, $"premise: the selected grapheme has width ({selected})");
        Assert.True(
            ring.Left <= selected.Left + 0.5 && ring.Right >= selected.Right - 0.5,
            $"the ring {ring.Left:F1}..{ring.Right:F1} misses the selected grapheme {selected.Left:F1}..{selected.Right:F1}");
    }

    /// <summary>Codex round 4: a table row taller than the viewport shows
    /// lines of every cell; the visible range covers all of them, not only
    /// the first cell's.</summary>
    [Fact]
    public void ALaterTableCellsMatchesStayVisible() => RunSta(() =>
    {
        var tall = new StringBuilder();
        for (int i = 0; i < 160; i++)
        {
            tall.Append("filler").Append(i).Append(' ');
        }
        using var harness = new Harness(
            "reading-find-table",
            "| first | second |\n| --- | --- |\n| " + tall + "| needle in the second cell |\n");
        (int offset, _) = Assert.Single(harness.Matches("needle"));
        TextPointer needle = harness.Find.Index.StartOf(offset);
        Rect glyph = needle.GetCharacterRect(LogicalDirection.Forward);
        Assert.True(glyph.Top >= 0 && glyph.Bottom <= harness.Surface.ActualHeight, $"premise: the needle is on screen at {glyph}");

        (int first, int last) = harness.Find.VisibleOffsets();

        Assert.True(first <= offset && last >= offset, $"visible {first}..{last}; the needle at {offset}");
    });

    /// <summary>Two matches inside one math source are two stops, each
    /// selecting the whole element: the surface remembers which one it
    /// revealed while the selection is still the one the reveal made.</summary>
    [Fact]
    public void TwoMatchesInOneMathBlockAreBothStepped() => RunSta(() =>
    {
        using var harness = new Harness("reading-find-math-step", "Words before.\n\n$$y + y$$\n");
        var heard = new List<A11yEvent>();
        using var find = new FindInNoteViewModel(heard.Add);
        find.Open();
        find.AttachSurface(harness.Find);
        find.Query = "y";
        find.FlushPendingForTests();
        Assert.Equal(2, find.TotalCount);
        Assert.Equal(1, find.CurrentIndex);

        find.Next();

        Assert.Equal(2, find.CurrentIndex);
        Assert.Equal(harness.Matches("y")[1], harness.Find.Selection);
        find.Next();
        Assert.Equal(1, find.CurrentIndex);
        Assert.Equal(["1 of 2", "2 of 2", "1 of 2. Wrapped to the top"], Spoken(heard));
        find.AttachSurface(null);
    });

    [Fact]
    public void TheLoadingPlaceholderIsNeverSearched() => RunSta(() =>
    {
        using var harness = new Harness("reading-find-placeholder", "The reading note.\n", notes: 2);
        var tab = new WorkspaceTabViewModel(
            harness.Session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
            startInteractionBackgroundWork: false);
        using var model = new ReadingContentViewModel(harness.Session, tab, _ => { });
        harness.Surface.Model = model;
        Assert.False(harness.Find.IsSearchable, "premise: the placeholder shows");

        var heard = new List<A11yEvent>();
        using var find = new FindInNoteViewModel(heard.Add);
        var surface = new ReadingFindSurface(harness.Surface);
        find.Open();
        find.AttachSurface(surface);
        find.Query = "reading";
        find.FlushPendingForTests();

        Assert.Equal([FindInNotePhrase.WaitingForReadingView], Spoken(heard));
        Assert.Equal(0, find.TotalCount);
        Assert.True(harness.Surface.Selection.IsEmpty);

        model.Refresh();
        Assert.True(PumpedDispatcher.PumpUntil(() => surface.IsSearchable, TimeSpan.FromSeconds(30)), "the projection never applied");
        find.FlushPendingForTests();
        Assert.Equal([FindInNotePhrase.WaitingForReadingView, "1 of 1"], Spoken(heard));
        Assert.Equal(1, find.TotalCount);
        surface.Detach();
        harness.Surface.Model = null;
        tab.Dispose();
    });

    [Fact]
    public void AReadingRepublishReRunsTheSearch() => RunSta(() =>
    {
        using var harness = new Harness("reading-find-republish", "alpha one\n\nalpha two\n");
        var heard = new List<A11yEvent>();
        using var find = new FindInNoteViewModel(heard.Add);
        find.Open();
        find.AttachSurface(harness.Find);
        find.Query = "alpha";
        find.FlushPendingForTests();
        Assert.Equal(2, find.TotalCount);
        heard.Clear();

        harness.Surface.ApplyBuiltDocument(Build("alpha one\n\nalpha two\n\nalpha three\n"));
        Assert.Equal(0, harness.Adorner()?.Count ?? 0);
        int caret = harness.Surface.Document.ContentStart.GetOffsetToPosition(harness.Surface.CaretPosition);

        find.FlushPendingForTests();
        Assert.Equal(3, find.TotalCount);
        Assert.Empty(heard);
        Assert.Equal(caret, harness.Surface.Document.ContentStart.GetOffsetToPosition(harness.Surface.CaretPosition));
        find.AttachSurface(null);
    });

    private const string DiagramNote =
        "# Diagram\n\nBefore the diagram, after words.\n\n"
        + "```mermaid\nweirdDiagram\nstuff\n```\n\n"
        + "After the diagram.\n";

    [Fact]
    public void AnInPlaceDiagramCompletionReRunsTheSearch() => RunSta(() =>
    {
        using var harness = new Harness("reading-find-diagram", DiagramNote, gated: true);
        var heard = new List<A11yEvent>();
        using var find = new FindInNoteViewModel(heard.Add);
        find.Open();
        find.AttachSurface(harness.Find);
        // The pending shape holds only the source.
        find.Query = "dialect";
        find.FlushPendingForTests();
        Assert.Equal(0, find.TotalCount);

        harness.Land();
        find.FlushPendingForTests();

        // The failure shape's header is an in-range run.
        Assert.True(find.TotalCount == 1, $"counted {find.TotalCount} over: {harness.IndexText}");
        Assert.Contains("Diagram dialect not supported", harness.IndexText, StringComparison.Ordinal);
        find.AttachSurface(null);
    });

    private static IEnumerable<T> LogicalDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject node)
            {
                continue;
            }
            if (node is T match)
            {
                yield return match;
            }
            foreach (T descendant in LogicalDescendants<T>(node))
            {
                yield return descendant;
            }
        }
    }

    private static string LargeNote()
    {
        var text = new StringBuilder();
        for (int i = 0; i < 600; i++)
        {
            text.Append("Paragraph number ").Append(i).Append(".\n\n");
        }
        return text.ToString();
    }

    /// <summary>The text without whitespace, the embedded element's U+FFFC
    /// and the disc marker WPF's plain-text serialization writes for a
    /// bulleted list (no document content: WB-11's ordered markers are runs,
    /// and are compared).</summary>
    private static string Squeeze(string text) =>
        new(text.Where(c => !char.IsWhiteSpace(c) && c != '\uFFFC' && c != '\u2022').ToArray());

    private static List<string> Spoken(List<A11yEvent> heard) =>
        heard.Select(spoken => SlateUniffiMethods.A11yRender(spoken).Text).ToList();

    /// <summary>A projection built from source, as the model builds one.</summary>
    private static FlowDocument Build(string source)
    {
        ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(source);
        ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
            source, Array.Empty<RenderedCitation>(), Array.Empty<OutgoingLink>());
        var model = new List<(ReadingBlock, ReadingBlockInlines)>();
        for (int i = 0; i < blocks.Length && i < inlines.Length; i++)
        {
            model.Add((blocks[i], inlines[i]));
        }
        return ReadingDocumentBuilder.Build(model).Document;
    }

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(120), "STA test body timed out.");
}
