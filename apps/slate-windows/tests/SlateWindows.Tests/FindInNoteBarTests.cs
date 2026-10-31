// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using SlateWindows.Canvas;
using SlateWindows.Grids;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-22 PR 1 (#1112): the find bar in the shipped shell — Ctrl+F's
/// routing, the bar's own keys, Ctrl+G from the bar and the editor text with
/// focus kept, and Replace's guard against stale text.
/// </summary>
public sealed class FindInNoteBarTests
{
    private const string BoardCanvas =
        "{\"nodes\":[{\"id\":\"alpha\",\"type\":\"text\",\"text\":\"Alpha\",\"x\":0,\"y\":0,\"width\":200,\"height\":100}],\"edges\":[]}";

    /// <summary>A note open in the shipped shell, its text replaced.</summary>
    private sealed class Shell : IDisposable
    {
        public Shell(string? text = "a b a b a", string path = "note0.md", bool savedQuery = false, Action<string>? seed = null)
        {
            Host = new CommandPaletteTests.ShippedShellHost(shown: true, savedQuery: savedQuery, seed: seed);
            Workspace = Host.AttachWorkspace(Heard.Add, path);
            if (text is not null)
            {
                Tab.Text = text;
            }
            Settle();
        }

        public CommandPaletteTests.ShippedShellHost Host { get; }

        public WorkspaceViewModel Workspace { get; }

        public List<A11yEvent> Heard { get; } = [];

        public WorkspaceTabViewModel Tab => Workspace.ActiveGroup.ActiveTab!;

        public FindInNoteViewModel Find => Tab.FindInNote;

        public SlateTextEditor Editor => Assert.Single(
            Descendants<SlateTextEditor>(Host.Shell),
            editor => ReferenceEquals(editor.DataContext, Tab) && editor.IsVisible);

        public FindInNoteBar Bar => Assert.Single(
            Descendants<FindInNoteBar>(Host.Shell),
            bar => ReferenceEquals(bar.DataContext, Tab.FindInNote));

        public List<string> Spoken => Heard
            .OfType<A11yEvent.HostComposed>()
            .Select(line => line.Text)
            .ToList();

        public bool SearchIsOpen => Host.Lifecycle.Search.IsOpen;

        public void Settle()
        {
            Host.Shell.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        public void FocusEditor()
        {
            Assert.True(Editor.FocusInputOwner());
            PumpedDispatcher.Drain();
            Assert.True(Editor.TextArea.IsKeyboardFocused, "premise: the editor text holds the keys");
        }

        public bool Press(UIElement target, Key key, ModifierKeys modifiers = ModifierKeys.None)
        {
            bool handled = Host.Press(target, key, modifiers);
            Settle();
            return handled;
        }

        /// <summary>Ctrl+F from the editor text, then the query typed and
        /// the debounce flushed. A negative caret keeps the editor's own
        /// selection.</summary>
        public void Open(string query, int caret = 0)
        {
            if (caret >= 0)
            {
                Editor.Select(caret, 0);
            }
            FocusEditor();
            Press(Editor.TextArea, Key.F, ModifierKeys.Control);
            Assert.True(Find.IsOpen, "premise: Ctrl+F opened the bar");
            Assert.True(Bar.Query.IsKeyboardFocused, "premise: the query holds the keys");
            Bar.Query.Text = query;
            Find.FlushPendingForTests();
            Settle();
        }

        public T Child<T>(string automationId)
            where T : FrameworkElement => Assert.Single(
                Descendants<T>(Bar),
                element => System.Windows.Automation.AutomationProperties.GetAutomationId(element) == automationId);

        public int Renderers => Editor.TextArea.TextView.BackgroundRenderers.OfType<FindMatchBackgroundRenderer>().Count();

        public ReadingSurface ReadingSurface => Assert.Single(
            Descendants<ReadingSurface>(Host.Shell),
            surface => ReferenceEquals(surface.DataContext, Tab) && surface.IsVisible);

        public bool Press(IInputElement target, Key key, ModifierKeys modifiers = ModifierKeys.None)
        {
            bool handled = Host.Press(target, key, modifiers);
            Settle();
            return handled;
        }

        /// <summary>The note in reading mode, its projection applied.</summary>
        public void ToReading()
        {
            Tab.ToggleViewMode();
            Settle();
            Assert.True(ReadingSurface.ShowsSearchableContent, "premise: the reading projection applied");
        }

        public void FocusReading()
        {
            Assert.True(ReadingSurface.Focus());
            Settle();
            Assert.True(ReadingSurface.IsKeyboardFocused, "premise: the reading text holds the keys");
        }

        /// <summary>Ctrl+F from the reading text, then the query typed and
        /// the debounce flushed.</summary>
        public void OpenReading(string query)
        {
            FocusReading();
            Press(ReadingSurface, Key.F, ModifierKeys.Control);
            Assert.True(Find.IsOpen, "premise: Ctrl+F opened the bar");
            Bar.Query.Text = query;
            Find.FlushPendingForTests();
            Settle();
        }

        /// <summary>The rings on <paramref name="surface"/> (by default the
        /// shown reading surface).</summary>
        public FindMatchAdorner? ReadingAdorner(ReadingSurface? surface = null)
        {
            if ((surface ?? ReadingSurface).DocumentRenderScope is not { } scope)
            {
                return null;
            }
            return AdornerLayer.GetAdornerLayer(scope)?.GetAdorners(scope)?.OfType<FindMatchAdorner>().SingleOrDefault();
        }

        public void Dispose() => Host.Dispose();
    }

    /// <summary>The headless rig the stale-text facts use (no editor, so
    /// nothing but the view model can move the selection).</summary>
    private sealed class Headless : IDisposable
    {
        public Headless(string text)
        {
            Document = new TextDocument(text);
            Surface = new EditorFindSurface(Document, editor: null);
            Find = new FindInNoteViewModel(Heard.Add);
            Find.Open();
            Find.AttachSurface(Surface);
        }

        public TextDocument Document { get; }

        public EditorFindSurface Surface { get; }

        public FindInNoteViewModel Find { get; }

        public List<A11yEvent> Heard { get; } = [];

        public List<string> Spoken => Heard.OfType<A11yEvent.HostComposed>().Select(line => line.Text).ToList();

        public void Dispose() => Find.Dispose();
    }

    [Fact]
    public void CtrlFInANoteFocusesTheQueryAndSelectsAll() => RunSta(() =>
    {
        using var shell = new Shell();
        shell.Open("a");
        Assert.Equal(3, shell.Find.TotalCount);

        // Ctrl+F again with the bar open, from the editor text: the query
        // takes the keys back with its text selected (mac's Reactivate).
        shell.FocusEditor();
        shell.Press(shell.Editor.TextArea, Key.F, ModifierKeys.Control);

        Assert.True(shell.Bar.Query.IsKeyboardFocused);
        Assert.Equal("a", shell.Bar.Query.SelectedText);
        Assert.False(shell.SearchIsOpen);
    });

    [Fact]
    public void CtrlFOnABaseTabReachesTheQuickFilterNotTheBar() => RunSta(() =>
    {
        using var shell = new Shell(text: null, path: "Notes.base", savedQuery: true);
        AccessibleDataGrid grid = Assert.Single(Descendants<AccessibleDataGrid>(shell.Host.Shell), candidate => candidate.IsVisible);

        Assert.True(shell.Press(grid, Key.F, ModifierKeys.Control));

        Assert.Equal(
            "BaseQuickFilter",
            System.Windows.Automation.AutomationProperties.GetAutomationId((DependencyObject)Keyboard.FocusedElement));
        Assert.False(shell.Find.IsOpen);
        Assert.False(shell.SearchIsOpen);
    });

    [Fact]
    public void CtrlFOnTheWelcomeViewWithNoWorkspaceReachesVaultSearch() => RunSta(() =>
    {
        using var host = new CommandPaletteTests.ShippedShellHost(shown: true);
        Assert.Null(host.Lifecycle.Workspace);

        Assert.True(host.Press(host.Shell, Key.F, ModifierKeys.Control));
        PumpedDispatcher.Drain();

        Assert.True(host.Lifecycle.Search.IsOpen);
    });

    [Fact]
    public void CtrlFWithNoNoteSurfaceOpensVaultSearch() => RunSta(() =>
    {
        // A clean tab: closing a dirty one would raise the unsaved prompt.
        using var shell = new Shell(text: null);

        // A graph tab.
        shell.Workspace.OpenGraph();
        shell.Settle();
        shell.Press(shell.Host.Shell, Key.F, ModifierKeys.Control);
        Assert.True(shell.SearchIsOpen);
        shell.Host.Lifecycle.Search.Toggle();

        // No tab at all.
        while (shell.Workspace.ActiveGroup.ActiveTab is not null)
        {
            shell.Workspace.CloseActiveTabCommand.Execute(null);
            shell.Settle();
        }
        shell.Press(shell.Host.Shell, Key.F, ModifierKeys.Control);
        Assert.True(shell.SearchIsOpen);
    });

    [Fact]
    public void CtrlFOnACanvasReachesTheCanvasFilterNotTheBar() => RunSta(() =>
    {
        using var shell = new Shell(
            text: null,
            path: "board.canvas",
            seed: root => File.WriteAllText(Path.Combine(root, "board.canvas"), BoardCanvas));
        CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(shell.Tab.Canvas);
        CanvasSurfaceView surface = Assert.Single(Descendants<CanvasSurfaceView>(shell.Host.Shell), candidate => candidate.IsVisible);
        Assert.Null(document.FilterFocusRequest);

        Assert.True(shell.Press(surface, Key.F, ModifierKeys.Control));

        // The canvas's own filter took the key (its request is consumed the
        // moment the pane serves it, so focus is the witness).
        Assert.Equal(
            "CanvasFilterField",
            System.Windows.Automation.AutomationProperties.GetAutomationId((DependencyObject)Keyboard.FocusedElement));
        Assert.Null(document.FilterFocusRequest);
        Assert.False(shell.Find.IsOpen);
        Assert.False(shell.SearchIsOpen);
    });

    public static TheoryData<string, string, int, int, string, string, int> StaleEdits => new()
    {
        // text, query, edit offset, edit length, edit text, expected text, live count
        { "a b a", "a", 0, 1, "x", "x b a", 1 },
        { "a b c", "a", 0, 1, "x", "x b c", 0 },
        { "abc d abc", "abc", 1, 1, "x", "axc d abc", 1 },
    };

    [Theory]
    [MemberData(nameof(StaleEdits))]
    public void AnEditBeforeTheDebounceNeverReplacesStaleText(
        string text, string query, int editOffset, int editLength, string editText, string expected, int live)
    {
        using var rig = new Headless(text);
        rig.Find.Query = query;
        rig.Find.FlushPendingForTests();
        Assert.Equal((0, query.Length), rig.Surface.Selection);
        rig.Find.ReplaceText = "q";
        rig.Heard.Clear();

        // The reader's own edit, inside the 150 ms window (no flush).
        rig.Document.Replace(editOffset, editLength, editText);
        rig.Find.Replace();

        Assert.Equal(expected, rig.Document.Text);
        Assert.Equal(live, rig.Find.TotalCount);
        Assert.Equal(0, rig.Find.CurrentIndex);
        Assert.Equal(new[] { "Match changed; nothing replaced." }, rig.Spoken);
        Assert.Equal((0, query.Length), rig.Surface.Selection);
    }

    [Fact]
    public void AnEditElsewhereBeforeTheDebounceStillReplacesTheTrackedHit()
    {
        using var rig = new Headless("a b a");
        rig.Find.Query = "a";
        rig.Find.FlushPendingForTests();
        rig.Find.Next();
        Assert.Equal((4, 1), rig.Surface.Selection);
        rig.Find.ReplaceText = "q";
        rig.Heard.Clear();

        rig.Document.Insert(0, "zz");
        rig.Find.Replace();

        Assert.Equal("zza b q", rig.Document.Text);
        Assert.DoesNotContain("Match changed; nothing replaced.", rig.Spoken);
    }

    [Fact]
    public void AnOptionChangeBeforeReplaceRecountsFirst()
    {
        using var rig = new Headless("A a A");
        rig.Find.Query = "a";
        rig.Find.FlushPendingForTests();
        Assert.Equal(3, rig.Find.TotalCount);
        rig.Find.ReplaceText = "q";

        rig.Find.MatchCase = true;
        rig.Find.ReplaceAll();

        Assert.Equal("A q A", rig.Document.Text);
    }

    /// <summary>Red team round 1: Replace after an option change inside
    /// the debounce validates the tracked hit against the LIVE options.</summary>
    [Fact]
    public void AReplaceAfterAnOptionChangeInsideTheDebounceUsesTheLiveOptions()
    {
        using var rig = new Headless("A a");
        rig.Find.Query = "a";
        rig.Find.FlushPendingForTests();
        Assert.Equal((0, 1), rig.Surface.Selection);
        rig.Find.ReplaceText = "q";
        rig.Heard.Clear();

        rig.Find.MatchCase = true;
        rig.Find.Replace();

        Assert.Equal("A a", rig.Document.Text);
        Assert.Equal(new[] { "Match changed; nothing replaced." }, rig.Spoken);
        Assert.Equal(1, rig.Find.TotalCount);
    }

    private const string ReadingNote = "# alpha head\n\n- an alpha item\n\nA [alpha link](note1.md) here.\n";

    /// <summary>PR 2: Ctrl+F in a reading-mode note opens the bar over the
    /// reading view, never vault search.</summary>
    [Fact]
    public void CtrlFInReadingModeOpensTheBarOverTheReadingView() => RunSta(() =>
    {
        using var shell = new Shell(ReadingNote);
        shell.ToReading();
        ReadingSurface surface = shell.ReadingSurface;
        shell.FocusReading();

        shell.Press(surface, Key.F, ModifierKeys.Control);

        Assert.True(shell.Find.IsOpen);
        Assert.Equal(FindInNoteMode.Reading, shell.Find.Mode);
        Assert.True(shell.Bar.Query.IsKeyboardFocused);
        Assert.False(shell.SearchIsOpen);
        shell.Bar.Query.Text = "alpha";
        shell.Find.FlushPendingForTests();
        shell.Settle();
        Assert.IsType<ReadingFindSurface>(shell.Find.Surface);
        Assert.Equal(3, shell.Find.TotalCount);
    });

    /// <summary>PR 2: Ctrl+G from the reading text steps the selection with
    /// the keys kept, and opening, drawing and stepping change nothing in
    /// the reading document: its text, its elements, its peers, its
    /// revision.</summary>
    [Fact]
    public void CtrlGFromTheReadingSurfaceMovesTheSelectionAndLeavesTheDocumentAlone() => RunSta(() =>
    {
        using var shell = new Shell(ReadingNote);
        shell.ToReading();
        ReadingSurface surface = shell.ReadingSurface;
        string text = DocumentText(surface);
        string structure = Fingerprint(surface.Document);
        int revision = surface.ContentRevision;
        int changes = 0;
        surface.TextChanged += (_, _) => changes++;
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(surface);
        List<AutomationPeer> peers = peer.GetChildren() ?? [];

        shell.OpenReading("alpha");
        Assert.Equal(1, shell.Find.CurrentIndex);
        shell.FocusReading();
        shell.Heard.Clear();

        Assert.True(shell.Press(surface, Key.G, ModifierKeys.Control));
        Assert.Equal("alpha", surface.Selection.Text);
        Assert.Equal(2, shell.Find.CurrentIndex);
        Assert.Equal(["2 of 3"], shell.Spoken);
        Assert.True(surface.IsKeyboardFocused, "the step took the keys from the reading text");
        shell.Press(surface, Key.G, ModifierKeys.Control);
        shell.Press(surface, Key.G, ModifierKeys.Control);
        Assert.Equal(1, shell.Find.CurrentIndex);

        Assert.Equal(text, DocumentText(surface));
        Assert.Equal(structure, Fingerprint(surface.Document));
        Assert.Equal(0, changes);
        Assert.Equal(revision, surface.ContentRevision);
        List<AutomationPeer> after = peer.GetChildren() ?? [];
        Assert.Equal(peers.Count, after.Count);
        Assert.All(peers.Zip(after), pair => Assert.Same(pair.First, pair.Second));
    });

    /// <summary>PR 2: Ctrl+G from a link inside the reading document — a
    /// content element, reached through its logical parent — steps the
    /// surface's selection, the keys staying on the link.</summary>
    [Fact]
    public void CtrlGFromALinkInTheReadingDocumentSteps() => RunSta(() =>
    {
        using var shell = new Shell(ReadingNote);
        shell.ToReading();
        ReadingSurface surface = shell.ReadingSurface;
        shell.OpenReading("alpha");
        Hyperlink link = Assert.Single(
            LogicalDescendants<Hyperlink>(surface.Document),
            candidate => new TextRange(candidate.ContentStart, candidate.ContentEnd).Text.Contains("alpha", StringComparison.Ordinal));
        Assert.True(link.Focus(), "premise: the link takes the keys");
        Assert.True(link.IsKeyboardFocused);

        Assert.True(shell.Press(link, Key.G, ModifierKeys.Control));

        Assert.Equal(2, shell.Find.CurrentIndex);
        Assert.Equal("alpha", surface.Selection.Text);
        Assert.True(link.IsKeyboardFocused, "the step took the keys from the link");
    });

    /// <summary>PR 2: with the note's bar closed — never opened, or opened
    /// and closed — Ctrl+G from the reading text is left unhandled.</summary>
    [Fact]
    public void CtrlGWithTheBarClosedPassesThroughTheReadingSurface() => RunSta(() =>
    {
        using var shell = new Shell(ReadingNote);
        shell.ToReading();
        ReadingSurface surface = shell.ReadingSurface;
        for (int pass = 0; pass < 2; pass++)
        {
            if (pass == 1)
            {
                shell.OpenReading("alpha");
                shell.Press(shell.Bar.Query, Key.Escape);
                Assert.False(shell.Find.IsOpen);
            }
            shell.FocusReading();
            string selection = surface.Selection.Text;
            shell.Heard.Clear();

            Assert.False(shell.Press(surface, Key.G, ModifierKeys.Control));
            Assert.False(shell.Press(surface, Key.G, ModifierKeys.Control | ModifierKeys.Shift));

            Assert.Equal(selection, surface.Selection.Text);
            Assert.Empty(shell.Spoken);
            Assert.False(shell.Find.IsOpen);
        }
    });

    /// <summary>PR 2: Escape in the reading text never reaches the bar, so it
    /// stays open with its matches drawn.</summary>
    [Fact]
    public void EscapeInTheReadingSurfaceLeavesTheBarOpen() => RunSta(() =>
    {
        using var shell = new Shell(ReadingNote);
        shell.ToReading();
        shell.OpenReading("alpha");
        shell.FocusReading();

        shell.Press(shell.ReadingSurface, Key.Escape);

        Assert.True(shell.Find.IsOpen);
        Assert.Equal(3, Assert.IsType<FindMatchAdorner>(shell.ReadingAdorner()).Count);
    });

    /// <summary>PR 2: Escape in the bar over the reading view closes it,
    /// takes the rings away, gives the selection back and returns the keys
    /// to the reading text.</summary>
    [Fact]
    public void EscapeClosesAndReturnsFocusToTheReadingSurface() => RunSta(() =>
    {
        using var shell = new Shell(ReadingNote);
        shell.ToReading();
        ReadingSurface surface = shell.ReadingSurface;
        surface.CaretPosition = surface.Document.ContentStart.GetPositionAtOffset(5)!;
        int caret = surface.Document.ContentStart.GetOffsetToPosition(surface.CaretPosition);
        shell.OpenReading("alpha");
        shell.Press(shell.Bar.Query, Key.Enter);
        Assert.Equal("alpha", surface.Selection.Text);

        // Read before the dispatcher drains: a later focus guard must not be
        // what puts the keys back.
        shell.Host.Press(shell.Bar.Query, Key.Escape, ModifierKeys.None);
        Assert.True(surface.IsKeyboardFocused, "Escape did not return the keys to the reading text");
        shell.Settle();

        Assert.False(shell.Find.IsOpen);
        Assert.Null(shell.ReadingAdorner());
        Assert.True(surface.Selection.IsEmpty, "the selection opening found was not given back");
        Assert.Equal(caret, surface.Document.ContentStart.GetOffsetToPosition(surface.CaretPosition));
        Assert.True(surface.IsKeyboardFocused, "the keys left the reading text after Escape");
    });

    /// <summary>PR 2: a mode switch with the bar open keeps its query and
    /// options and moves it to the other surface: the editor's fill goes,
    /// the reading view's rings come, the set is recounted over the reading
    /// text silently with no caret or selection moved, and back again the
    /// editor's still-selected match is current.</summary>
    [Fact]
    public void AModeSwitchWithTheBarOpenKeepsTheQueryAndMovesTheHighlights() => RunSta(() =>
    {
        using var shell = new Shell("alpha beta alpha");
        shell.Open("alpha");
        shell.Press(shell.Bar.Query, Key.Enter);
        Assert.Equal(2, shell.Find.CurrentIndex);
        SlateTextEditor editor = shell.Editor;
        Assert.Equal(1, shell.Renderers);
        shell.Heard.Clear();

        shell.Press(shell.Bar.Query, Key.E, ModifierKeys.Control | ModifierKeys.Shift);
        shell.Find.FlushPendingForTests();
        shell.Settle();

        Assert.True(shell.Find.IsOpen);
        Assert.Equal(FindInNoteMode.Reading, shell.Find.Mode);
        Assert.Equal("alpha", shell.Find.Query);
        Assert.Empty(editor.TextArea.TextView.BackgroundRenderers.OfType<FindMatchBackgroundRenderer>());
        ReadingSurface surface = shell.ReadingSurface;
        Assert.Equal(2, Assert.IsType<FindMatchAdorner>(shell.ReadingAdorner()).Count);
        Assert.Equal(2, shell.Find.TotalCount);
        Assert.Equal(0, shell.Find.CurrentIndex);
        Assert.True(surface.Selection.IsEmpty, "the switch moved the reading selection");
        Assert.Equal(Visibility.Collapsed, shell.Child<Button>("FindInNoteReplace").Visibility);
        Assert.Empty(shell.Spoken);
        // The toggle's own landing: the keys go to the surface it shows.
        Assert.True(surface.IsKeyboardFocused, "the keys did not land on the reading text");

        shell.Press(shell.Bar.Query, Key.E, ModifierKeys.Control | ModifierKeys.Shift);
        shell.Find.FlushPendingForTests();
        shell.Settle();

        Assert.Equal(FindInNoteMode.Editor, shell.Find.Mode);
        Assert.Null(shell.ReadingAdorner(surface));
        Assert.Equal(1, shell.Renderers);
        Assert.Equal(2, shell.Find.CurrentIndex);
        Assert.Empty(shell.Spoken);

        // The match shows as current but was not heard here: Replace steps
        // and speaks before it writes anything.
        shell.Find.ReplaceText = "omega";
        shell.Find.Replace();
        Assert.Equal("alpha beta alpha", shell.Tab.Text);
        Assert.Single(shell.Spoken);
    });

    /// <summary>Red team: the reading surface is attached only once its
    /// model, as well as its tab, is the bar's — a model that is another
    /// tab's, or arrives late with no content change, is answered when it
    /// does arrive.</summary>
    [Fact]
    public void TheBarWaitsForTheReadingSurfacesOwnModel() => RunSta(() =>
    {
        using var shell = new Shell(ReadingNote);
        WorkspaceTabViewModel own = shell.Tab;
        shell.Workspace.OpenPath("note1.md", WorkspaceOpenTarget.NewTab);
        shell.Settle();
        WorkspaceTabViewModel other = shell.Tab;
        other.ToggleViewMode();
        shell.Settle();
        shell.Workspace.ActiveGroup.ActiveTab = own;
        shell.Settle();
        shell.ToReading();
        shell.OpenReading("alpha");
        ReadingSurface surface = shell.ReadingSurface;
        Assert.IsType<ReadingFindSurface>(shell.Find.Surface);

        surface.Model = other.Reading;
        shell.Settle();
        Assert.Null(shell.Find.Surface);

        surface.ClearValue(ReadingSurface.ModelProperty);
        shell.Settle();
        Assert.Same(own.Reading, surface.Model);
        Assert.IsType<ReadingFindSurface>(shell.Find.Surface);
        Assert.Equal(3, shell.Find.TotalCount);
    });

    /// <summary>PR 2: a switch to reading mode collapses the replace
    /// controls; one holding the keys hands them to the query first, so a
    /// switch with no landing of its own (the tab's toggle; the command's
    /// shortcut then lands them on the reading text) never leaves them on a
    /// collapsed control.</summary>
    [Theory]
    [InlineData("ReplaceBox")]
    [InlineData("ReplaceButton")]
    [InlineData("ReplaceAllButton")]
    public void AModeSwitchFromAReplaceControlKeepsTheKeysInTheBar(string control) => RunSta(() =>
    {
        using var shell = new Shell("alpha beta alpha");
        shell.Open("alpha");
        var replace = Assert.IsAssignableFrom<FrameworkElement>(shell.Bar.FindName(control));
        Assert.True(replace.Focus());

        shell.Tab.ToggleViewMode();
        shell.Settle();

        Assert.Equal(FindInNoteMode.Reading, shell.Find.Mode);
        Assert.True(shell.Bar.Query.IsKeyboardFocused, "the keys left the bar with the collapsed control");
    });

    /// <summary>PR 2: reading mode has no replace — its three controls are
    /// collapsed, absent from the landmark's children, and their commands
    /// cannot run; editor mode has them before Close.</summary>
    [Fact]
    public void TheReadingModeBarHidesReplace() => RunSta(() =>
    {
        using var shell = new Shell(ReadingNote);
        shell.Open("alpha");
        Assert.Equal(
            ["FindInNoteQuery", "FindInNoteCount", "FindInNotePrevious", "FindInNoteNext", "FindInNoteMatchCase",
             "FindInNoteWholeWord", "FindInNoteReplaceText", "FindInNoteReplace", "FindInNoteReplaceAll", "FindInNoteClose"],
            LandmarkChildren(shell));

        shell.ToReading();

        foreach (string id in new[] { "FindInNoteReplaceText", "FindInNoteReplace", "FindInNoteReplaceAll" })
        {
            Assert.Equal(Visibility.Collapsed, shell.Child<FrameworkElement>(id).Visibility);
        }
        Assert.False(shell.Find.ReplaceCommand.CanExecute(null));
        Assert.False(shell.Find.ReplaceAllCommand.CanExecute(null));
        Assert.Equal(
            ["FindInNoteQuery", "FindInNoteCount", "FindInNotePrevious", "FindInNoteNext", "FindInNoteMatchCase",
             "FindInNoteWholeWord", "FindInNoteClose"],
            LandmarkChildren(shell));
    });

    /// <summary>The landmark's children a reader reaches: WPF keeps a
    /// collapsed element as an offscreen peer, which screen readers
    /// skip.</summary>
    private static List<string> LandmarkChildren(Shell shell)
    {
        AutomationPeer landmark = UIElementAutomationPeer.CreatePeerForElement(shell.Child<FrameworkElement>("FindInNote"));
        landmark.ResetChildrenCache();
        return (landmark.GetChildren() ?? [])
            .Where(child => !child.IsOffscreen())
            .Select(child => child.GetAutomationId())
            .ToList();
    }

    private static string DocumentText(ReadingSurface surface) =>
        new TextRange(surface.Document.ContentStart, surface.Document.ContentEnd).Text;

    /// <summary>The document's elements in pre-order, each run with its
    /// text: what a highlight written INTO the document would change.</summary>
    private static string Fingerprint(FlowDocument document)
    {
        var text = new System.Text.StringBuilder();
        foreach (DependencyObject element in LogicalDescendants<DependencyObject>(document))
        {
            text.Append(element.GetType().Name);
            if (element is Run run)
            {
                text.Append('[').Append(run.Text).Append(']');
            }
            text.Append(' ');
        }
        return text.ToString();
    }

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

    /// <summary>Red team round 1: the bar belongs to the note it searched —
    /// an in-place change of item closes it and clears its count, while a
    /// tab round trip keeps it, and its restore point, on its own note.</summary>
    [Fact]
    public void AnInPlaceNavigationClosesTheBarAndATabRoundTripKeepsIt() => RunSta(() =>
    {
        using var shell = new Shell("a b a b a");
        WorkspaceTabViewModel first = shell.Tab;
        shell.Open("a", caret: 2);
        Assert.Equal(4, shell.Editor.SelectionStart);

        // Another note in its own tab, then back.
        shell.Workspace.OpenPath("note1.md", WorkspaceOpenTarget.NewTab);
        shell.Settle();
        Assert.NotSame(first, shell.Tab);
        Assert.Equal(0, shell.Renderers);
        shell.Workspace.ActiveGroup.ActiveTab = first;
        shell.Settle();
        Assert.True(first.FindInNote.IsOpen);
        Assert.Equal(1, shell.Renderers);
        shell.Press(shell.Bar.Query, Key.Escape);
        Assert.Equal((2, 0), (shell.Editor.SelectionStart, shell.Editor.SelectionLength));

        // In place: the bar closes and its count clears.
        shell.Open("a");
        first.ReplaceItem(new WorkspaceItemState(WorkspaceItemKind.Markdown, "note1.md"));
        shell.Settle();
        Assert.False(first.FindInNote.IsOpen);
        Assert.Equal(string.Empty, first.FindInNote.CountText);
        Assert.Equal(0, first.FindInNote.TotalCount);
        Assert.Equal(0, shell.Renderers);
    });

    /// <summary>Codex round 1: under a sheet, Ctrl+F opens neither the bar
    /// nor vault search, and Ctrl+G steps nothing.</summary>
    [Fact]
    public void CtrlFAndCtrlGUnderASheetDoNothingBeneathIt() => RunSta(() =>
    {
        using var shell = new Shell("a b a b a");
        shell.Open("a");
        shell.Find.Close();
        shell.Workspace.OpenAddPropertySheet(synchronousForTests: true);
        shell.Settle();
        Assert.NotNull(shell.Workspace.AddPropertySheet);
        shell.Heard.Clear();

        shell.Press(shell.Host.Shell, Key.F, ModifierKeys.Control);

        Assert.False(shell.Find.IsOpen);
        Assert.False(shell.SearchIsOpen);
        Assert.Empty(shell.Spoken);
    });

    /// <summary>Codex round 1: a same-item reload swaps the document; Close
    /// still restores where the bar opened, followed by later edits.</summary>
    [Fact]
    public void ASameItemReloadKeepsTheRestorePointOnTheLiveDocument() => RunSta(() =>
    {
        using var shell = new Shell(
            text: null,
            seed: root => File.WriteAllText(Path.Combine(root, "note0.md"), "a b a b a"));
        shell.Open("a", caret: 2);
        TextDocument before = shell.Tab.EditorDocument!;

        shell.Tab.ReplaceItem(shell.Tab.Item);
        shell.Settle();
        Assert.NotSame(before, shell.Tab.EditorDocument);
        Assert.True(shell.Find.IsOpen);
        shell.Tab.EditorDocument!.Insert(0, "zz");
        shell.Settle();
        shell.Press(shell.Bar.Query, Key.Escape);

        Assert.Equal((4, 0), (shell.Editor.SelectionStart, shell.Editor.SelectionLength));
    });

    /// <summary>Codex round 2: a rectangular selection comes back as the
    /// same rectangle, so a Delete after Escape removes only what the
    /// reader selected.</summary>
    [Fact]
    public void ARectangularSelectionComesBackAsTheSameRectangle() => RunSta(() =>
    {
        using var shell = new Shell("abcd\nefgh\nijkl");
        ICSharpCode.AvalonEdit.Editing.TextArea area = shell.Editor.TextArea;
        shell.FocusEditor();
        area.Selection = new ICSharpCode.AvalonEdit.Editing.RectangleSelection(
            area,
            new ICSharpCode.AvalonEdit.TextViewPosition(1, 2),
            new ICSharpCode.AvalonEdit.TextViewPosition(3, 4));
        // Where an Alt+drag leaves the caret: the rectangle's active end.
        area.Caret.Position = new ICSharpCode.AvalonEdit.TextViewPosition(3, 4);
        Assert.IsType<ICSharpCode.AvalonEdit.Editing.RectangleSelection>(area.Selection);
        string selected = area.Selection.GetText();
        Assert.Equal(new[] { "bc", "fg", "jk" }, selected.Split(["\r\n", "\n"], StringSplitOptions.None));

        OpenKeepingTheSelection(shell, "e");
        shell.Press(shell.Bar.Query, Key.Escape);

        Assert.IsType<ICSharpCode.AvalonEdit.Editing.RectangleSelection>(area.Selection);
        Assert.Equal(selected, area.Selection.GetText());
        area.Selection.ReplaceSelectionWithText(string.Empty);
        Assert.Equal("ad\neh\nil", shell.Tab.Text.Replace("\r\n", "\n"));
    });

    /// <summary>Codex round 2: a backward selection keeps its active end
    /// (the caret) where the reader left it.</summary>
    [Fact]
    public void ABackwardSelectionKeepsItsActiveEnd() => RunSta(() =>
    {
        using var shell = new Shell("a b a b a");
        ICSharpCode.AvalonEdit.Editing.TextArea area = shell.Editor.TextArea;
        shell.FocusEditor();
        area.Caret.Offset = 1;
        area.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(area, 4, 1);
        Assert.False(area.Selection.IsEmpty);

        OpenKeepingTheSelection(shell, "a");
        shell.Press(shell.Bar.Query, Key.Escape);

        Assert.Equal(1, area.Caret.Offset);
        Assert.Equal(4, shell.Tab.EditorDocument!.GetOffset(area.Selection.StartPosition.Location));
        Assert.Equal(1, shell.Tab.EditorDocument!.GetOffset(area.Selection.EndPosition.Location));
    });

    /// <summary>Codex round 3: a rectangle wholly past its lines' ends
    /// (AvalonEdit calls it empty) comes back, so typing still edits every
    /// line.</summary>
    [Fact]
    public void AVirtualSpaceRectangleComesBackAndStillTypesOnEveryLine() => RunSta(() =>
    {
        using var shell = new Shell("abcd\nefgh\nijkl");
        ICSharpCode.AvalonEdit.Editing.TextArea area = shell.Editor.TextArea;
        shell.FocusEditor();
        area.Selection = new ICSharpCode.AvalonEdit.Editing.RectangleSelection(
            area,
            new ICSharpCode.AvalonEdit.TextViewPosition(1, 5, 6),
            new ICSharpCode.AvalonEdit.TextViewPosition(3, 5, 6));
        area.Caret.Position = new ICSharpCode.AvalonEdit.TextViewPosition(3, 5, 6);
        Assert.IsType<ICSharpCode.AvalonEdit.Editing.RectangleSelection>(area.Selection);

        OpenKeepingTheSelection(shell, "e");
        shell.Press(shell.Bar.Query, Key.Escape);

        Assert.IsType<ICSharpCode.AvalonEdit.Editing.RectangleSelection>(area.Selection);
        area.PerformTextInput("X");
        Assert.Equal("abcd  X\nefgh  X\nijkl  X", shell.Tab.Text.Replace("\r\n", "\n"));
    });

    /// <summary>Codex round 3: a caret left in virtual space keeps its
    /// column.</summary>
    [Fact]
    public void ACaretInVirtualSpaceKeepsItsColumn() => RunSta(() =>
    {
        using var shell = new Shell("abcd\nefgh\nijkl");
        ICSharpCode.AvalonEdit.Editing.TextArea area = shell.Editor.TextArea;
        shell.FocusEditor();
        area.Selection = new ICSharpCode.AvalonEdit.Editing.RectangleSelection(
            area,
            new ICSharpCode.AvalonEdit.TextViewPosition(1, 2, 1),
            new ICSharpCode.AvalonEdit.TextViewPosition(3, 5, 8));
        area.Caret.Position = new ICSharpCode.AvalonEdit.TextViewPosition(3, 5, 8);
        Assert.True(area.Caret.IsInVirtualSpace, "premise: the caret sits past the line's end");

        OpenKeepingTheSelection(shell, "e");
        shell.Press(shell.Bar.Query, Key.Escape);

        Assert.True(area.Caret.IsInVirtualSpace);
        Assert.Equal(8, area.Caret.VisualColumn);
        Assert.Equal(3, area.Caret.Line);
    });

    /// <summary>Codex round 4 (the design pass): with the text unchanged,
    /// Close gives back AvalonEdit's own state verbatim — here the caret's
    /// desired column, which a Down onto a short line keeps for the next
    /// long one.</summary>
    [Fact]
    public void AnUnchangedNoteGetsBackTheCaretsDesiredColumn() => RunSta(() =>
    {
        string longLine = new string('x', 30);
        using var shell = new Shell(longLine + "\nab\n" + longLine);
        ICSharpCode.AvalonEdit.Editing.TextArea area = shell.Editor.TextArea;
        shell.FocusEditor();
        area.Caret.Position = new ICSharpCode.AvalonEdit.TextViewPosition(1, 22);
        System.Windows.Documents.EditingCommands.MoveDownByLine.Execute(null, area);
        Assert.Equal(2, area.Caret.Line);
        Assert.False(double.IsNaN(area.Caret.DesiredXPos), "premise: Down kept a desired column");

        OpenKeepingTheSelection(shell, "ab");
        shell.Press(shell.Bar.Query, Key.Escape);
        System.Windows.Documents.EditingCommands.MoveDownByLine.Execute(null, area);

        Assert.Equal(3, area.Caret.Line);
        Assert.Equal(22, area.Caret.Column);
    });

    /// <summary>A text view with no height has valid but empty visual
    /// lines; drawing the matches then draws nothing instead of throwing.</summary>
    [Fact]
    public void MatchesDrawNothingOnATextViewWithNoVisualLines() => RunSta(() =>
    {
        var document = new TextDocument("a b a");
        var view = new ICSharpCode.AvalonEdit.Rendering.TextView { Document = document };
        view.Measure(new Size(200, 0));
        Assert.True(view.VisualLinesValid, "premise: the layout ran");
        Assert.Empty(view.VisualLines);
        var renderer = new FindMatchBackgroundRenderer(view, document);
        renderer.SetMatches([(0, 1), (4, 1)]);

        var visual = new DrawingVisual();
        using (DrawingContext context = visual.RenderOpen())
        {
            renderer.Draw(view, context);
        }

        Assert.Null(visual.Drawing);
    });

    /// <summary>Codex round 7: the bar must not depend on every tab switch
    /// changing the editor's document. Note tabs own separate documents
    /// today (a duplicate mirrors its source's state into its own), so the
    /// editor's document is pinned here to reach the schedule: the editor's
    /// data context leaves for another tab and comes back with no document
    /// change, and the bar detaches and reattaches on the context alone.</summary>
    [Fact]
    public void TheBarFollowsTheEditorsContextWithoutADocumentChange() => RunSta(() =>
    {
        using var shell = new Shell("x a b a");
        WorkspaceTabViewModel own = shell.Tab;
        shell.Workspace.OpenPath("note1.md", WorkspaceOpenTarget.NewTab);
        shell.Settle();
        WorkspaceTabViewModel other = shell.Tab;
        Assert.NotSame(own, other);
        shell.Workspace.ActiveGroup.ActiveTab = own;
        shell.Settle();
        shell.Open("a", caret: 0);
        SlateTextEditor editor = shell.Editor;
        TextDocument document = editor.Document;
        Assert.Same(document, own.FindInNote.Surface?.Document);
        editor.Document = document;

        editor.DataContext = other;
        shell.Settle();
        Assert.Null(own.FindInNote.Surface);
        editor.ClearValue(FrameworkElement.DataContextProperty);
        shell.Settle();
        Assert.Same(own, editor.DataContext);
        Assert.Same(document, own.FindInNote.Surface?.Document);

        shell.FocusEditor();
        shell.Press(editor.TextArea, Key.G, ModifierKeys.Control);
        Assert.Equal((6, 1), (editor.SelectionStart, editor.SelectionLength));
        shell.Press(shell.Bar.Query, Key.Escape);
        Assert.Equal((0, 0), (editor.SelectionStart, editor.SelectionLength));
        Assert.True(editor.TextArea.IsKeyboardFocused, "Escape did not return focus to the text");
    });

    /// <summary>Codex round 5: a tab switch may rebind the editor's document
    /// before or after its data context; a document that is not this tab's
    /// is never attached, and the tab's own restore state survives until
    /// its document arrives.</summary>
    [Fact]
    public void ADocumentFromAnotherTabIsNeverAttached() => RunSta(() =>
    {
        using var shell = new Shell(new string('x', 45) + " a b");
        shell.Open("a", caret: 40);
        TextDocument own = shell.Editor.Document;
        Assert.NotNull(shell.Find.Surface);

        // The other tab's (short) document arrives first.
        shell.Editor.Document = new TextDocument("a");
        shell.Settle();
        Assert.Null(shell.Find.Surface);

        shell.Editor.Document = own;
        shell.Settle();
        Assert.Same(own, shell.Find.Surface?.Document);
        shell.Press(shell.Bar.Query, Key.Escape);

        Assert.Equal((40, 0), (shell.Editor.SelectionStart, shell.Editor.SelectionLength));
    });

    /// <summary>Codex round 6: emptying the query hides the count readout
    /// at once, before Tab could reach it, so no debounce or step can later
    /// collapse it under the keys.</summary>
    [Fact]
    public void AnEmptiedQueryHidesTheCountAtOnce() => RunSta(() =>
    {
        using var shell = new Shell("a b a b a");
        shell.Open("a");
        var count = Assert.IsType<TextBlock>(shell.Bar.FindName("CountReadout"));
        Assert.Same(shell.Child<TextBlock>("FindInNoteCount"), count);
        Assert.True(count.IsVisible);

        shell.Bar.Query.Text = string.Empty;
        shell.Host.Shell.UpdateLayout();

        Assert.False(count.IsVisible, "the count stayed reachable after the query emptied");
        Assert.Equal(string.Empty, shell.Find.CountText);
        Assert.True(shell.Bar.Query.IsKeyboardFocused);
        shell.Press(shell.Bar.Query, Key.G, ModifierKeys.Control);
        Assert.True(shell.Bar.Query.IsKeyboardFocused);
    });

    [Fact]
    public void CloseAfterAShortenedDocumentRestoresAClampedCaret() => RunSta(() =>
    {
        using var shell = new Shell(text: new string('x', 45) + " a b");
        shell.Open("a", caret: 40);

        shell.Tab.EditorDocument!.Remove(10, shell.Tab.EditorDocument.TextLength - 10);
        shell.Press(shell.Bar.Query, Key.Escape);

        Assert.False(shell.Find.IsOpen);
        Assert.Equal(10, shell.Editor.SelectionStart);
        Assert.Equal(0, shell.Editor.SelectionLength);
    });

    [Fact]
    public void EnterAndCtrlGStepNextAndShiftStepsPrevious() => RunSta(() =>
    {
        using var shell = new Shell();
        shell.Open("a");
        Assert.Equal(1, shell.Find.CurrentIndex);

        Assert.True(shell.Press(shell.Bar.Query, Key.Enter));
        Assert.Equal(2, shell.Find.CurrentIndex);
        Assert.True(shell.Press(shell.Bar.Query, Key.G, ModifierKeys.Control));
        Assert.Equal(3, shell.Find.CurrentIndex);
        Assert.True(shell.Press(shell.Bar.Query, Key.Enter, ModifierKeys.Shift));
        Assert.Equal(2, shell.Find.CurrentIndex);
        Assert.True(shell.Press(shell.Bar.Query, Key.G, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.Equal(1, shell.Find.CurrentIndex);
        Assert.True(shell.Bar.Query.IsKeyboardFocused);
    });

    [Fact]
    public void CtrlGFromTheEditorTextStepsAndKeepsFocusInTheEditor() => RunSta(() =>
    {
        using var shell = new Shell("a b a b a");
        shell.Open("a");
        Assert.Equal(1, shell.Find.CurrentIndex);
        shell.FocusEditor();
        shell.Heard.Clear();

        Assert.True(shell.Press(shell.Editor.TextArea, Key.G, ModifierKeys.Control));

        Assert.Equal((4, 1), (shell.Editor.SelectionStart, shell.Editor.SelectionLength));
        Assert.Equal(2, shell.Find.CurrentIndex);
        Assert.Equal(new[] { "2 of 3" }, shell.Spoken);
        Assert.True(shell.Editor.TextArea.IsKeyboardFocused);
        Assert.Equal("a b a b a", shell.Tab.Text);

        shell.Heard.Clear();
        Assert.True(shell.Press(shell.Editor.TextArea, Key.G, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.Equal((0, 1), (shell.Editor.SelectionStart, shell.Editor.SelectionLength));
        Assert.Equal(new[] { "1 of 3" }, shell.Spoken);
        Assert.True(shell.Editor.TextArea.IsKeyboardFocused);
    });

    [Fact]
    public void CtrlGWithTheBarClosedPassesThroughTheEditor() => RunSta(() =>
    {
        using var shell = new Shell("a b a b a");

        void AssertPassesThrough()
        {
            shell.Editor.Select(2, 0);
            shell.FocusEditor();
            shell.Heard.Clear();
            Assert.False(shell.Press(shell.Editor.TextArea, Key.G, ModifierKeys.Control));
            Assert.False(shell.Press(shell.Editor.TextArea, Key.G, ModifierKeys.Control | ModifierKeys.Shift));
            Assert.Equal((2, 0), (shell.Editor.SelectionStart, shell.Editor.SelectionLength));
            Assert.Empty(shell.Heard);
            Assert.False(shell.Find.IsOpen);
            Assert.False(shell.SearchIsOpen);
            Assert.Equal("a b a b a", shell.Tab.Text);
        }

        AssertPassesThrough();
        shell.Open("a");
        shell.Press(shell.Bar.Query, Key.Escape);
        Assert.False(shell.Find.IsOpen);
        AssertPassesThrough();
    });

    [Fact]
    public void CtrlGFromOutsideTheNoteSurfacesPassesThrough() => RunSta(() =>
    {
        using var shell = new Shell("a b a b a");
        shell.Open("a");
        (int, int) selection = (shell.Editor.SelectionStart, shell.Editor.SelectionLength);
        FrameworkElement properties = Assert.Single(
            Descendants<FrameworkElement>(shell.Host.Shell),
            element => System.Windows.Automation.AutomationProperties.GetAutomationId(element) == "PropertiesHeader"
                && ReferenceEquals(element.DataContext, shell.Tab));
        shell.Heard.Clear();

        Keyboard.Focus(properties);
        PumpedDispatcher.Drain();
        Assert.True(properties.IsKeyboardFocusWithin, "premise: the properties header holds the keys");
        Assert.False(shell.Press(properties, Key.G, ModifierKeys.Control));

        Assert.Equal(1, shell.Find.CurrentIndex);
        Assert.Equal(selection, (shell.Editor.SelectionStart, shell.Editor.SelectionLength));
        Assert.Empty(shell.Heard);
    });

    [Fact]
    public void TheBarsEnterIsTheQueryFieldsOnly() => RunSta(() =>
    {
        using var shell = new Shell("a b a");
        shell.Open("a");
        shell.Find.ReplaceText = "q";
        Button replaceAll = shell.Child<Button>("FindInNoteReplaceAll");
        Keyboard.Focus(replaceAll);
        PumpedDispatcher.Drain();
        Assert.True(replaceAll.IsKeyboardFocused);

        shell.Press(replaceAll, Key.Enter);

        Assert.Equal("q b q", shell.Tab.Text);
        shell.Tab.EditorDocument!.UndoStack.Undo();
        Assert.Equal("a b a", shell.Tab.Text);

        // Enter in the editor text types, with the bar open.
        shell.Find.FlushPendingForTests();
        int current = shell.Find.CurrentIndex;
        shell.Editor.Select(0, 0);
        shell.FocusEditor();
        shell.Press(shell.Editor.TextArea, Key.Enter);
        Assert.StartsWith("\n", shell.Tab.Text.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.True(shell.Find.IsOpen);
        Assert.Equal(current, shell.Find.CurrentIndex);
    });

    [Fact]
    public void EscapeInTheEditorTextLeavesTheBarOpen() => RunSta(() =>
    {
        using var shell = new Shell(
            text: null,
            seed: root => File.WriteAllText(Path.Combine(root, "note0.md"), "a b ![[note1]] a"));
        shell.Open("a");
        shell.Editor.Select(0, 0);
        shell.FocusEditor();

        shell.Press(shell.Editor.TextArea, Key.Escape);

        Assert.True(shell.Find.IsOpen);
        Assert.Equal(1, shell.Renderers);

        // With an embed preview open, Escape closes the popover only.
        shell.Editor.Select(shell.Tab.Text.IndexOf("![[", StringComparison.Ordinal) + 3, 0);
        shell.Tab.EditorInteractions!.RefreshArtifactCacheForTests();
        shell.FocusEditor();
        shell.Press(shell.Editor.TextArea, Key.E, ModifierKeys.Control);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => shell.Tab.EditorInteractions?.IsPopoverOpen == true, TimeSpan.FromSeconds(10)),
            "premise: Ctrl+E opened the embed preview");
        shell.FocusEditor();
        shell.Press(shell.Editor.TextArea, Key.Escape);
        Assert.False(shell.Tab.EditorInteractions?.IsPopoverOpen);
        Assert.True(shell.Find.IsOpen);
    });

    [Fact]
    public void EscapeClosesClearsHighlightsAndReturnsFocusToTheEditor() => RunSta(() =>
    {
        using var shell = new Shell("a b a b a");
        shell.Open("a", caret: 2);
        Assert.Equal(1, shell.Renderers);
        Assert.Equal(4, shell.Editor.SelectionStart);

        FrameworkElement landmark = shell.Child<FrameworkElement>("FindInNote");
        Assert.True(landmark.IsVisible);

        // Pressed without draining: Close itself hands the keys back, at
        // once, rather than leaving a later focus guard to find them.
        Assert.True(shell.Host.Press(shell.Bar.Query, Key.Escape, ModifierKeys.None));
        Assert.True(shell.Editor.TextArea.IsKeyboardFocused, "Close did not return the keys to the editor");
        shell.Settle();

        // The bar collapses under the keys only after Close handed them back.
        Assert.False(landmark.IsVisible);
        Assert.False(shell.Find.IsOpen);
        Assert.Equal(0, shell.Renderers);
        Assert.True(shell.Editor.TextArea.IsKeyboardFocused);
        Assert.Equal((2, 0), (shell.Editor.SelectionStart, shell.Editor.SelectionLength));
    });

    [Fact]
    public void TheBarIsASiblingOfTheEditorPeerNotAChild() => RunSta(() =>
    {
        using var shell = new Shell("a b a b a");
        AutomationPeer editorPeer = UIElementAutomationPeer.CreatePeerForElement(shell.Editor);
        int childrenBefore = editorPeer.GetChildren()?.Count ?? 0;

        shell.Open("a");

        Assert.False(shell.Bar.IsDescendantOf(shell.Editor));
        Assert.False(shell.Editor.IsDescendantOf(shell.Bar));
        Assert.Equal(childrenBefore, editorPeer.GetChildren()?.Count ?? 0);
        FrameworkElement landmark = shell.Child<FrameworkElement>("FindInNote");
        AutomationPeer landmarkPeer = UIElementAutomationPeer.CreatePeerForElement(landmark);
        Assert.Equal("Find in note", landmarkPeer.GetName());
        for (AutomationPeer? parent = landmarkPeer.GetParent(); parent is not null; parent = parent.GetParent())
        {
            Assert.NotSame(editorPeer, parent);
        }
    });

    [Fact]
    public void TypingAnnouncesOnlyTheFinalCount() => RunSta(() =>
    {
        using var shell = new Shell("a b a b a");
        shell.Open(string.Empty);
        shell.Heard.Clear();

        shell.Bar.Query.Text = "a";
        shell.Bar.Query.Text = "a ";
        shell.Bar.Query.Text = "a b";
        shell.Find.FlushPendingForTests();

        Assert.Equal(new[] { "1 of 2" }, shell.Spoken);
    });

    /// <summary>Ctrl+F from the focused editor without touching its
    /// selection, then the query typed and the debounce flushed.</summary>
    private static void OpenKeepingTheSelection(Shell shell, string query)
    {
        Assert.True(shell.Editor.TextArea.IsKeyboardFocused);
        shell.Press(shell.Editor.TextArea, Key.F, ModifierKeys.Control);
        Assert.True(shell.Find.IsOpen, "premise: Ctrl+F opened the bar");
        shell.Bar.Query.Text = query;
        shell.Find.FlushPendingForTests();
        shell.Settle();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }
            foreach (T descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(120), "STA test body timed out.");
}
