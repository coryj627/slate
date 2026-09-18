// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
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
            var link = host.Provider.Links.Enclosing(host.Text.IndexOf(marker, StringComparison.Ordinal), host.Text.IndexOf(marker, StringComparison.Ordinal) + 1);
            Assert.True(link is not null, marker + " | " + string.Join("; ", host.Session.InspectInRange(0, host.Text.Length).Spans.Select(x => $"{x.Kind}:{x.StartUtf16},{x.LengthUtf16}")));
            Assert.Contains(marker, link.GetName());
            Assert.Same(AutomationElement.NotSupported, host.At(marker).GetAttributeValue(EditorSemanticTextRange.LinkAttribute));
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
        Assert.Null(document.FindAttribute(EditorSemanticTextRange.LinkAttribute, true, false));
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
        Assert.Equal(new[] { AutomationEvents.TextPatternOnTextChanged, AutomationEvents.TextPatternOnTextSelectionChanged }, events.Where(item => item != AutomationEvents.StructureChanged));
        coordinator.FlushSemanticChanges();
        Assert.Equal(2, events.Count);
        host.Session.Document.Insert(host.Session.Document.TextLength, "next");
        coordinator.Dispose();
        coordinator.FlushSemanticChanges();
        Assert.Equal(2, events.Count);
    });

    [Fact]
    public void StartupPaintDoesNotCancelTheFirstTwentyEditNotificationBatch() => OnSta(() =>
    {
        using var host = new Host("## Heading\n", show: true, drainStartup: false);
        var events = new List<AutomationEvents>();
        host.Editor.AutomationEventForCensus = events.Add;
        for (int index = 0; index < 20; index++) { host.Session.Document.Insert(host.Session.Document.TextLength, "x"); }
        PumpUntil(() => events.Count >= 2);
        Assert.Equal(new[] { AutomationEvents.TextPatternOnTextChanged, AutomationEvents.TextPatternOnTextSelectionChanged }, events.Where(item => item != AutomationEvents.StructureChanged));
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
        Assert.NotEmpty(host.Provider.GetVisibleRanges());
        Assert.NotNull(host.Provider.RangeFromPoint(host.Editor.TextArea.TextView.PointToScreen(new Point(8, 8))));
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
        Assert.Equal(new[] { AutomationEvents.TextPatternOnTextChanged, AutomationEvents.TextPatternOnTextSelectionChanged }, events.Where(item => item != AutomationEvents.StructureChanged));

        void SetComposition(string property, string value) => typeof(TextComposition).GetProperty(property)!
            .GetSetMethod(nonPublic: true)!.Invoke(composition, [value]);
    });

    [Fact]
    public void QueriesRejectForeignThreadsAndDoNotReadAReplacementDocument() => OnSta(() =>
    {
        using var host = new Host("## Original", show: true);
        ITextRangeProvider old = host.Provider.DocumentRange;
        Exception? error = Task.Run(() => Record.Exception(() => old.GetAttributeValue(EditorSemanticTextRange.StyleIdAttribute))).GetAwaiter().GetResult();
        Assert.IsType<InvalidOperationException>(error);
        using var replacement = new AvalonDocumentBufferSession("# Replacement\n\nSecond line", _ => { });
        host.Editor.Document = replacement.Document;
        host.Editor.HighlightSession = replacement;
        Assert.Same(AutomationElement.NotSupported, old.GetAttributeValue(EditorSemanticTextRange.StyleIdAttribute));
        Assert.Throws<ElementNotAvailableException>(() => host.Provider.RangeFromPoint(new Point(8, 40)));
        Assert.Throws<ElementNotAvailableException>(() => host.Provider.GetVisibleRanges());
        Assert.Throws<ElementNotAvailableException>(() => host.Provider.DocumentRange);
        Assert.Throws<ElementNotAvailableException>(() => host.Provider.GetSelection());
        Assert.Throws<ElementNotAvailableException>(() => old.GetText(-1));
        Assert.Throws<ElementNotAvailableException>(() => old.GetChildren());
        Assert.Throws<ElementNotAvailableException>(() => old.Select());
        ITextProvider current = Assert.IsAssignableFrom<ITextProvider>(host.Peer.GetPattern(PatternInterface.Text));
        Assert.Equal(70001, current.DocumentRange.FindText("Replacement", false, false)!.GetAttributeValue(EditorSemanticTextRange.StyleIdAttribute));
    });

    [Fact]
    public void VisibleRangeIncludesTheCharacterClippedAtTheRightEdge() => OnSta(() =>
    {
        using var host = new Host(new string('W', 300), show: true);
        host.Editor.Width = 101.25;
        host.Editor.UpdateLayout();
        var view = host.Editor.TextArea.TextView;
        view.EnsureVisualLines();
        var edge = new Point(view.HorizontalOffset + view.ActualWidth, view.VisualLines[0].Height / 2);
        var position = view.GetPositionFloor(edge)!.Value;
        Assert.True(view.GetVisualPosition(position, ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineTop).X < edge.X);
        int clippedCharacter = host.Session.Document.GetOffset(position.Location);
        var visible = Assert.IsType<EditorSemanticTextRange>(Assert.Single(host.Provider.GetVisibleRanges()));
        Assert.Equal(clippedCharacter + 1, visible.Bounds.End);
    });

    [Fact]
    public void VisibleAndPointRangesBelongToThePublicEditorPeer() => OnSta(() =>
    {
        using var host = new Host(Fixture, show: true);
        Assert.NotEmpty(host.Provider.GetVisibleRanges());
        host.Session.BeginPeerUpdate();
        Assert.NotEmpty(host.Provider.GetVisibleRanges());
        host.Session.EndPeerUpdate();
        Point screen = host.Editor.TextArea.TextView.PointToScreen(new Point(8, 8));
        var point = Assert.IsType<EditorSemanticTextRange>(host.Provider.RangeFromPoint(screen));
        Assert.Equal(point.Bounds.Start, point.Bounds.End);
        Assert.Same(host.Provider.EnclosingElement, point.GetEnclosingElement());
        Assert.NotEmpty(host.Peer.GetChildren()!);
        Assert.Same(host.Peer, UIElementAutomationPeer.FromElement(host.Editor.TextArea).EventsSource);
    });

    [Fact]
    public void RetainedSelectionTracksDeletionUndoAndSubsequentMovement() => OnSta(() =>
    {
        using var host = new Host("Before [[Target]] after.\nEmbed ![[Target]] ends.");
        const int start = 7;
        const int length = 11;
        host.Editor.Select(start, length);
        ITextProvider nativeProvider = (ITextProvider)UIElementAutomationPeer.CreatePeerForElement(host.Editor.TextArea).GetPattern(PatternInterface.Text);
        ITextRangeProvider nativeSelection = Assert.Single(nativeProvider.GetSelection());
        ITextRangeProvider selected = Assert.Single(host.Provider.GetSelection());
        ITextRangeProvider cloned = selected.Clone();
        ITextRangeProvider native = AvalonTextRangeAccess.Create(host.Editor.TextArea, host.Session.Document, start, length);
        Assert.Equal("[[Target]] ", selected.GetText(-1));
        host.Session.Document.Remove(start, length);
        Assert.Equal("Before after.\nEmbed ![[Target]] ends.", host.Session.Document.Text);
        // Compare the native baseline before attributing NVDA speech to the decorator.
        Assert.Equal(string.Empty, native.GetText(-1));
        Assert.Equal("after.\nEmbe", nativeSelection.GetText(-1));
        Assert.Equal(string.Empty, selected.GetText(-1));
        Assert.Equal(string.Empty, cloned.GetText(-1));
        host.Session.Document.UndoStack.Undo();
        Assert.Equal(host.Text, host.Session.Document.Text);
        Assert.Equal(string.Empty, selected.GetText(-1));
        selected.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 1);
        Assert.Equal("a", selected.GetText(-1));
    });

    [Theory]
    [InlineData(TextUnit.Line)]
    [InlineData(TextUnit.Paragraph)]
    public void ExpandedNativeRangesStillTrackDeletedLines(TextUnit unit) => OnSta(() =>
    {
        using var host = new Host("First line\nSecond line\nLast line");
        ITextRangeProvider range = host.Provider.DocumentRange.FindText("Second", false, false)!;
        range.ExpandToEnclosingUnit(unit);
        host.Session.Document.Remove(11, 12);
        Assert.Equal(string.Empty, range.GetText(-1));
        host.Session.Document.UndoStack.Undo();
        range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 1);
        Assert.Equal("L", range.GetText(-1));
    });

    [Fact]
    public void EofMovementStillTracksDeletionAndSelection() => OnSta(() =>
    {
        using var host = new Host("[[Target]]");
        host.Editor.Select(0, host.Text.Length);
        ITextRangeProvider range = Assert.Single(host.Provider.GetSelection());
        range.Move(TextUnit.Character, 100);
        host.Session.Document.Remove(0, host.Text.Length);
        Assert.Equal(string.Empty, range.GetText(-1));
        Assert.Empty(range.GetChildren());
        range.Select();
        Assert.Equal(0, host.Editor.CaretOffset);
    });

    [Fact]
    public void HyperlinkNamesDoNotSplitSurrogatePairs() => OnSta(() =>
    {
        using var host = new Host("[[" + new string('a', 509) + "😀]]");
        string name = Assert.Single(host.Peer.GetChildren()!).GetName();
        Assert.EndsWith("…", name);
        Assert.DoesNotContain(name, character => char.IsSurrogate(character));
    });

    [Fact]
    public void EmptyCompositionCompletionInvalidatesTheUnavailableChildCache() => OnSta(() =>
    {
        using var host = new Host("[[Target]]", show: true);
        Assert.Single(host.Peer.GetChildren()!);
        var composition = new TextComposition(InputManager.Current, host.Editor.TextArea,
            string.Empty, TextCompositionAutoComplete.Off);
        TextCompositionManager.StartComposition(composition);
        Assert.True(host.Editor.IsComposing);
        Assert.Empty(host.Peer.GetChildren()!);
        TextCompositionManager.CompleteComposition(composition);
        Assert.False(host.Editor.IsComposing);
        Assert.Equal("[[Target]]", Assert.Single(host.Peer.GetChildren()!).GetName());
        Assert.Equal(host.Text, host.Session.Document.Text);
    });

    [Fact]
    public void HyperlinkContainmentRoundtripsAndOrdinaryOperandsAgree() => OnSta(() =>
    {
        using var host = new Host("Before [outer [[Inner]]](https://example.org) after.\n\n[[Last]]", show: true);
        ITextRangeProvider outer = host.Provider.DocumentRange.FindText("[outer [[Inner]]](https://example.org)", false, false)!;
        ITextRangeProvider inner = host.Provider.DocumentRange.FindText("[[Inner]]", false, false)!;
        nint handle = new System.Windows.Interop.WindowInteropHelper(Window.GetWindow(host.Editor)).Handle;
        Task connect = Task.Run(() => AutomationElement.FromHandle(handle));
        PumpUntil(() => connect.IsCompleted);
        connect.GetAwaiter().GetResult();
        IRawElementProviderSimple outerElement = outer.GetEnclosingElement();
        Assert.NotNull(outerElement);
        IRawElementProviderSimple innerElement = inner.GetEnclosingElement();
        Assert.False(ReferenceEquals(outerElement, innerElement), string.Join("; ", host.Session.InspectInRange(0, host.Text.Length).Spans.Select(x => $"{x.Kind}:{x.StartUtf16},{x.LengthUtf16}")));
        Assert.Same(innerElement, Assert.Single(outer.GetChildren()));
        Assert.Empty(inner.GetChildren());
        Assert.Equal(2, host.Provider.DocumentRange.GetChildren().Length);
        ITextRangeProvider roundtrip = host.Provider.RangeFromChild(innerElement);
        Assert.True(inner.Compare(roundtrip));
        Assert.True(roundtrip.Compare(inner));
        Assert.True(roundtrip.Clone().Compare(inner));
        roundtrip.MoveEndpointByRange(TextPatternRangeEndpoint.End, inner, TextPatternRangeEndpoint.Start);
        Assert.Equal(string.Empty, roundtrip.GetText(-1));
        Assert.Equal(2, host.Peer.GetChildren()!.Count);
        AutomationPeer child = Assert.Single(host.Peer.GetChildren()![0].GetChildren()!);
        Assert.Equal("[[Inner]]", child.GetName());
        Assert.Same(host.Peer.GetChildren()![0], child.GetParent());
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LinksRetainIdentityButRejectRemovedSemanticsAndReplacedSessions(bool show) => OnSta(() =>
    {
        using var host = new Host("Before [[Target]] after.\n\n[[Other]]", show: show);
        EditorHyperlinkPeer first = Assert.IsType<EditorHyperlinkPeer>(host.Peer.GetChildren()![0]);
        string id = first.GetAutomationId();
        host.Session.Document.Insert(0, "prefix ");
        Assert.Equal("[[Target]]", first.GetName());
        Assert.Equal(id, first.GetAutomationId());
        host.Editor.PublishSemanticTextChanged();
        Assert.Same(first, host.Peer.GetChildren()![0]);
        host.Session.Document.Remove(first.Start, 1);
        Assert.Throws<ElementNotAvailableException>(() => first.GetName());
        Assert.Throws<ElementNotAvailableException>(() => first.IsEnabled());
        Assert.Throws<ElementNotAvailableException>(() => first.Invoke());
        host.Editor.PublishSemanticTextChanged();
        EditorHyperlinkPeer other = Assert.IsType<EditorHyperlinkPeer>(Assert.Single(host.Peer.GetChildren()!));
        other.Invoke(); // Queued work must cancel after the old session disappears.
        using var replacement = new AvalonDocumentBufferSession("[[Replacement]]", _ => { });
        host.Editor.Document = replacement.Document;
        host.Editor.HighlightSession = replacement;
        host.Editor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.Equal("[[Replacement]]", Assert.Single(host.Peer.GetChildren()!).GetName());
        Assert.Throws<ElementNotAvailableException>(() => other.GetName());
        Assert.Throws<ElementNotAvailableException>(() => other.GetPattern(PatternInterface.Invoke));
    });

    [Fact]
    public void StructuralEditsRevalidateDistantLinksAndWpfChildCaches() => OnSta(() =>
    {
        using var host = new Host("- item\n\n  continuation\n\n    [link](x)\n\nTail\n", show: true);
        EditorHyperlinkPeer link = Assert.IsType<EditorHyperlinkPeer>(Assert.Single(host.Peer.GetChildren()!));
        host.Session.Document.Remove(0, 2);
        Assert.Throws<ElementNotAvailableException>(() => link.GetName());
        host.Editor.PublishSemanticTextChanged();
        Assert.Empty(host.Peer.GetChildren()!);
        host.Session.Document.Insert(host.Session.Document.TextLength, "\n[[New]]");
        host.Editor.PublishSemanticTextChanged();
        Assert.Equal("[[New]]", Assert.Single(host.Peer.GetChildren()!).GetName());
    });

    [Fact]
    public void TagExtensionRetainsIdentityWithoutAbsorbingAdjacentTokens() => OnSta(() =>
    {
        using var host = new Host("#tag");
        AutomationPeer tag = Assert.Single(host.Peer.GetChildren()!);
        host.Session.Document.Insert(4, "x");
        Assert.Equal("#tagx", tag.GetName());
        host.Editor.PublishSemanticTextChanged();
        Assert.Same(tag, Assert.Single(host.Peer.GetChildren()!));
        host.Session.Document.Insert(5, " #next");
        host.Editor.PublishSemanticTextChanged();
        Assert.Equal(2, host.Peer.GetChildren()!.Count);
        Assert.Same(tag, host.Peer.GetChildren()![0]);
        Assert.Equal("#tagx", tag.GetName());
        Assert.Equal("#next", host.Peer.GetChildren()![1].GetName());
    });

    [Fact]
    public void FirstChildEnumerationDuringPeerUpdateDoesNotCacheAnEmptyTree() => OnSta(() =>
    {
        using var host = new Host("[[Target]]");
        host.Session.BeginPeerUpdate();
        long before = host.Session.SemanticQueryCountForCensus;
        Assert.Empty(host.Peer.GetChildren()!);
        Assert.Equal(before, host.Session.SemanticQueryCountForCensus);
        host.Session.ApplyPeerEdit(new EditorDocumentChange(0, 0, "prefix "));
        host.Session.EndPeerUpdate();
        host.Editor.PublishSemanticTextChanged();
        Assert.Equal("[[Target]]", Assert.Single(host.Peer.GetChildren()!).GetName());
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstChildEnumerationDuringEmptyUpdatesRecoversWithoutTextChanges(bool peerUpdate) => OnSta(() =>
    {
        using var host = new Host("[[Target]]");
        using var highlighting = new AvalonHighlightingCoordinator(host.Editor, host.Session);
        var events = new List<AutomationEvents>();
        host.Editor.AutomationEventForCensus = events.Add;
        if (peerUpdate) { host.Session.BeginPeerUpdate(); }
        else { host.Session.Document.BeginUpdate(); }
        long before = host.Session.SemanticQueryCountForCensus;
        Assert.Empty(host.Peer.GetChildren()!);
        if (peerUpdate) { host.Session.EndPeerUpdate(); }
        else { host.Session.Document.EndUpdate(); }
        Assert.True(host.Session.SemanticReadsAvailable);
        highlighting.FlushSemanticChanges();
        Assert.Equal(before, host.Session.SemanticQueryCountForCensus);
        Assert.Empty(events);
        List<AutomationPeer> children = host.Peer.GetChildren()!;
        Assert.Equal("[[Target]]", Assert.Single(children).GetName());
        host.Peer.ResumeSemanticAvailability();
        host.Peer.ResumeSemanticAvailability();
        Assert.Same(children, host.Peer.GetChildren());
    });

    [Fact]
    public void DenseInventoryUsesCachedMembershipAndLocalQueriesAfterEdits() => OnSta(() =>
    {
        using var host = new Host(string.Concat(Enumerable.Range(0, 2000).Select(index => $"[[Link{index}]]\n\n")));
        List<AutomationPeer> all = host.Provider.Links.RootChildren();
        Assert.Equal(2000, all.Count);
        long before = host.Session.SemanticQueryCountForCensus;
        foreach (AutomationPeer peer in all) { Assert.StartsWith("[[Link", peer.GetName()); }
        Assert.Equal(before, host.Session.SemanticQueryCountForCensus);
        host.Session.Document.Insert(0, "prefix\n\n");
        Assert.Equal("[[Link1500]]", all[1500].GetName());
        Assert.Equal(before + 1, host.Session.SemanticQueryCountForCensus);
        Assert.Equal("[[Link1500]]", all[1500].GetName());
        Assert.Equal(before + 1, host.Session.SemanticQueryCountForCensus);
    });

    private sealed class Host : IDisposable
    {
        private readonly Window? _window;
        internal Host(string text, bool show = false, bool drainStartup = true)
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
                if (drainStartup) { Editor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); }
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

    private static void PumpUntil(Func<bool> complete)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        { Interval = TimeSpan.FromMilliseconds(10) };
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        timer.Tick += (_, _) => { if (complete() || elapsed.Elapsed > TimeSpan.FromSeconds(10)) { frame.Continue = false; } };
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        Assert.True(complete(), "The dispatched editor operation did not complete.");
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
