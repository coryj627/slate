// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Text;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class EditorSemanticTextRangeTests
{
    private static string Fixture => File.ReadAllText(Path.Combine(SourceText.RepoRoot(),
        "crates", "slate-core", "tests", "fixtures", "markdown", "editor_semantics.md"));

    [Fact]
    public void EveryCanonicalKindHasItsContractAttributeThroughTheNativePeer() => OnSta(() =>
    {
        using var host = new Host(Fixture);
        EditorHighlightWindow canonical = host.Session.InspectInRange(0, host.Text.Length);
        Assert.Equal(16, canonical.Spans.Select(span => span.Kind.GetType()).Distinct().Count());
        AssertAttribute("Heading one", EditorSemanticTextRange.StyleIdAttribute, 70001);
        AssertAttribute("Heading two", EditorSemanticTextRange.StyleNameAttribute, "Heading 2");
        AssertAttribute("Quoted heading", EditorSemanticTextRange.StyleIdAttribute, 70002);
        AssertAttribute("Quoted text", EditorSemanticTextRange.StyleIdAttribute, 70014);
        foreach (string marker in new[] { "[[Target]]", "![[Target]]", "#project", "[@smith2020]", "[Website]", "![Picture]" })
        {
            var link = Assert.IsType<EditorSemanticTextRange>(host.At(marker).GetAttributeValue(EditorSemanticTextRange.LinkAttribute));
            Assert.Contains(marker, link.GetText(-1));
        }
        AssertAttribute("inline code", EditorSemanticTextRange.StyleNameAttribute, "Code");
        AssertAttribute("```rust", EditorSemanticTextRange.StyleNameAttribute, "Code");
        AssertAttribute("let answer", EditorSemanticTextRange.StyleNameAttribute, "Code");
        AssertAttribute("Emphasis", EditorSemanticTextRange.IsItalicAttribute, true);
        AssertAttribute("Strong", EditorSemanticTextRange.FontWeightAttribute, 700);
        AssertAttribute("Strike", EditorSemanticTextRange.StrikethroughStyleAttribute, (int)TextDecorationLineStyle.Single);
        AssertAttribute("Comment text", EditorSemanticTextRange.StyleNameAttribute, "Comment");
        AssertAttribute("title:", EditorSemanticTextRange.StyleNameAttribute, "Frontmatter");

        void AssertAttribute(string marker, int attribute, object expected) =>
            Assert.Equal(expected, host.At(marker).GetAttributeValue(attribute));
    });

    [Fact]
    public void MixedPlainAndOffscreenReadsUseOneCanonicalQueryAndPreserveThePaintWindow() => OnSta(() =>
    {
        using var host = new Host(Fixture);
        EditorHighlightWindow paint = host.Session.HighlightInRange(0, 3);
        long before = host.Session.SemanticQueryCountForCensus;
        Assert.Same(TextPattern.MixedAttributeValue,
            host.Provider.DocumentRange.GetAttributeValue(EditorSemanticTextRange.StyleIdAttribute));
        Assert.Equal(before + 1, host.Session.SemanticQueryCountForCensus);
        Assert.Same(AutomationElement.NotSupported,
            host.At("Plain text").GetAttributeValue(EditorSemanticTextRange.StyleIdAttribute));
        Assert.Same(AutomationElement.NotSupported, host.At("Plain text").GetAttributeValue(-1));
        Assert.Equal("Comment", host.At("Comment text").GetAttributeValue(EditorSemanticTextRange.StyleNameAttribute));
        Assert.Same(paint, host.Session.LatestHighlightWindow);

        ITextRangeProvider boundary = host.Provider.DocumentRange.FindText("*Emphasis* and", false, false)!;
        Assert.Same(TextPattern.MixedAttributeValue, boundary.GetAttributeValue(EditorSemanticTextRange.IsItalicAttribute));
    });

    [Fact]
    public void FindAttributeHonoursDirectionIdentityAndQueryBoundaries() => OnSta(() =>
    {
        using var host = new Host(Fixture);
        ITextRangeProvider document = host.Provider.DocumentRange;
        Assert.Contains("Heading two", document.FindAttribute(EditorSemanticTextRange.StyleIdAttribute, 70002, false)!.GetText(-1));
        Assert.Contains("Quoted heading", document.FindAttribute(EditorSemanticTextRange.StyleIdAttribute, 70002, true)!.GetText(-1));
        Assert.Null(document.FindAttribute(EditorSemanticTextRange.StyleIdAttribute, 70006, false));
        Assert.Null(document.FindAttribute(EditorSemanticTextRange.IsItalicAttribute, true, false));
        Assert.Contains("[[Target]]", document.FindAttribute(EditorSemanticTextRange.LinkAttribute, true, false)!.GetText(-1));
        Assert.Contains("quoted link", document.FindAttribute(EditorSemanticTextRange.LinkAttribute, true, true)!.GetText(-1));
        object link = host.At("[Website]").GetAttributeValue(EditorSemanticTextRange.LinkAttribute);
        Assert.Equal("[Website](https://example.org)", document.FindAttribute(EditorSemanticTextRange.LinkAttribute, link, true)!.GetText(-1));
        Assert.Null(host.At("Plain text").FindAttribute(EditorSemanticTextRange.LinkAttribute, true, false));
        Assert.Equal("Web", host.Provider.DocumentRange.FindText("Web", false, false)!
            .FindAttribute(EditorSemanticTextRange.LinkAttribute, true, false)!.GetText(-1));
    });

    [Fact]
    public void NativeRangeOffsetsSurviveUnicodeMovementCloningAndRangeOperands() => OnSta(() =>
    {
        using var host = new Host("😀 é before\n\n## Heading\n\n[[link]]");
        var range = Assert.IsType<EditorSemanticTextRange>(host.Provider.DocumentRange.FindText("Heading", false, false));
        Assert.Equal(host.Text.IndexOf("Heading", StringComparison.Ordinal), range.Bounds.Start);
        ITextRangeProvider clone = range.Clone();
        Assert.True(range.Compare(clone));
        Assert.Equal(0, range.CompareEndpoints(TextPatternRangeEndpoint.End, clone, TextPatternRangeEndpoint.End));
        clone.MoveEndpointByRange(TextPatternRangeEndpoint.End, range, TextPatternRangeEndpoint.Start);
        Assert.Equal(70002, clone.GetAttributeValue(EditorSemanticTextRange.StyleIdAttribute));
        clone.ExpandToEnclosingUnit(TextUnit.Line);
        Assert.Contains("Heading", clone.GetText(-1));
        clone.Move(TextUnit.Character, 1);
        clone.Move(TextUnit.Word, 1);
        Assert.IsType<EditorSemanticTextRange>(clone.Clone());
        Assert.Empty(range.GetChildren());
        Assert.Throws<ArgumentException>(() => host.Provider.RangeFromChild(null!));
    });

    [Fact]
    public void PeerUpdatesRefuseQueriesAndTwentyEditsPublishOneOrderedBatch() => OnSta(() =>
    {
        using var host = new Host("## Heading\n");
        using var coordinator = new AvalonHighlightingCoordinator(host.Editor, host.Session, TimeSpan.FromHours(1));
        var events = new List<AutomationEvents>();
        host.Editor.AutomationEventForCensus = events.Add;
        host.Session.BeginPeerUpdate();
        long before = host.Session.SemanticQueryCountForCensus;
        Assert.Same(AutomationElement.NotSupported, host.Provider.DocumentRange.GetAttributeValue(EditorSemanticTextRange.StyleIdAttribute));
        Assert.Null(host.Provider.DocumentRange.FindAttribute(EditorSemanticTextRange.StyleIdAttribute, 70002, false));
        Assert.Equal(before, host.Session.SemanticQueryCountForCensus);
        for (int index = 0; index < 20; index++)
        {
            host.Session.ApplyPeerEdit(new EditorDocumentChange(host.Session.Document.TextLength, 0, "x"));
            coordinator.FlushSemanticChanges();
            Assert.Empty(events);
        }
        host.Session.EndPeerUpdate();
        coordinator.FlushSemanticChanges();
        Assert.Equal(new[] { AutomationEvents.TextPatternOnTextChanged, AutomationEvents.TextPatternOnTextSelectionChanged }, events);
        coordinator.FlushSemanticChanges();
        Assert.Equal(2, events.Count);
        host.Session.Document.Insert(host.Session.Document.TextLength, "next");
        coordinator.Dispose();
        coordinator.FlushSemanticChanges();
        Assert.Equal(2, events.Count);
    });

    [Fact]
    public void CompositionReadsStayUnavailableUntilTheCommittedNativeEdit() => OnSta(() =>
    {
        using var host = new Host("## Heading\n\n", show: true);
        AvalonHighlightingCoordinator coordinator = Assert.IsType<AvalonHighlightingCoordinator>(host.Editor.HighlightingForCensus);
        var events = new List<AutomationEvents>();
        host.Editor.AutomationEventForCensus = events.Add;
        host.Editor.CaretOffset = host.Session.Document.TextLength;
        var composition = new TextComposition(InputManager.Current, host.Editor.TextArea, string.Empty, TextCompositionAutoComplete.Off);
        SetComposition(nameof(TextComposition.CompositionText), "に");
        TextCompositionManager.StartComposition(composition);
        Assert.True(host.Editor.IsComposing);
        long before = host.Session.SemanticQueryCountForCensus;
        Assert.Same(AutomationElement.NotSupported, host.Provider.DocumentRange.GetAttributeValue(EditorSemanticTextRange.StyleIdAttribute));
        Assert.Equal(before, host.Session.SemanticQueryCountForCensus);
        coordinator.FlushSemanticChanges();
        Assert.Empty(events);
        SetComposition(nameof(TextComposition.CompositionText), string.Empty);
        SetComposition(nameof(TextComposition.Text), "日本語");
        TextCompositionManager.CompleteComposition(composition);
        Assert.False(host.Editor.IsComposing);
        Assert.EndsWith("日本語", host.Session.Document.Text);
        coordinator.FlushSemanticChanges();
        Assert.Equal(new[] { AutomationEvents.TextPatternOnTextChanged, AutomationEvents.TextPatternOnTextSelectionChanged }, events);

        void SetComposition(string property, string value) => typeof(TextComposition).GetProperty(property)!
            .GetSetMethod(nonPublic: true)!.Invoke(composition, [value]);
    });

    [Fact]
    public void QueriesRejectForeignThreadsAndDoNotReadAReplacementDocument() => OnSta(() =>
    {
        using var host = new Host("## Original");
        ITextRangeProvider old = host.Provider.DocumentRange;
        Exception? error = Task.Run(() => Record.Exception(() => old.GetAttributeValue(EditorSemanticTextRange.StyleIdAttribute))).GetAwaiter().GetResult();
        Assert.IsType<InvalidOperationException>(error);
        using var replacement = new AvalonDocumentBufferSession("# Replacement", _ => { });
        host.Editor.Document = replacement.Document;
        host.Editor.HighlightSession = replacement;
        Assert.Same(AutomationElement.NotSupported, old.GetAttributeValue(EditorSemanticTextRange.StyleIdAttribute));
        ITextProvider current = Assert.IsAssignableFrom<ITextProvider>(host.Peer.GetPattern(PatternInterface.Text));
        Assert.Equal(70001, current.DocumentRange.GetAttributeValue(EditorSemanticTextRange.StyleIdAttribute));
    });

    [Fact]
    public void VisibleAndPointRangesBelongToThePublicEditorPeer() => OnSta(() =>
    {
        using var host = new Host(Fixture, show: true);
        Assert.NotEmpty(host.Provider.GetVisibleRanges());
        Point screen = host.Editor.TextArea.TextView.PointToScreen(new Point(8, 8));
        var point = Assert.IsType<EditorSemanticTextRange>(host.Provider.RangeFromPoint(screen));
        Assert.Equal(point.Bounds.Start, point.Bounds.End);
        Assert.Same(host.Provider.EnclosingElement, point.GetEnclosingElement());
        Assert.Null(host.Peer.GetChildren());
        Assert.Same(host.Peer, UIElementAutomationPeer.FromElement(host.Editor.TextArea).EventsSource);
    });

    private sealed class Host : IDisposable
    {
        private readonly Window? _window;
        internal Host(string text, bool show = false)
        {
            Text = text;
            Session = new AvalonDocumentBufferSession(text, _ => { });
            Editor = new SlateTextEditor { Document = Session.Document, HighlightSession = Session };
            foreach (string key in new[] { EditorSyntaxPalette.HeadingBrushKey, EditorSyntaxPalette.CodeBrushKey,
                EditorSyntaxPalette.WikilinkBrushKey, EditorSyntaxPalette.TagBrushKey, EditorSyntaxPalette.MetadataBrushKey })
            {
                Editor.Resources[key] = Brushes.Black;
            }
            if (show)
            {
                _window = new Window
                {
                    Content = Editor,
                    Width = 500,
                    Height = 250,
                    Left = -10000,
                    Top = -10000,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.ToolWindow
                };
                _window.Show();
                _window.UpdateLayout();
                _window.Activate();
                Editor.FocusInputOwner();
                Editor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }
            Peer = Assert.IsType<SlateTextEditorAutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(Editor));
            Provider = Assert.IsType<EditorSemanticTextProvider>(Peer.GetPattern(PatternInterface.Text));
        }
        internal string Text { get; }
        internal AvalonDocumentBufferSession Session { get; }
        internal SlateTextEditor Editor { get; }
        internal SlateTextEditorAutomationPeer Peer { get; }
        internal EditorSemanticTextProvider Provider { get; }
        internal ITextRangeProvider At(string marker) => Provider.Range(Text.IndexOf(marker, StringComparison.Ordinal), Text.IndexOf(marker, StringComparison.Ordinal) + 1);
        public void Dispose() { _window?.Close(); Session.Dispose(); }
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromMinutes(2)), "Editor peer test did not finish.");
        if (failure is not null) { ExceptionDispatchInfo.Capture(failure).Throw(); }
    }
}
