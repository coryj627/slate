// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Text;
using System.Windows.Documents;
using System.Windows.Input;
using SlateWindows.Reading;

namespace SlateWindows.Tests;

/// <summary>
/// CB-21 (#1330): Narrator said nothing on Ctrl+A in the reading view. WPF's text range answers
/// <c>GetChildren</c> with null when the range holds no element that is an automation child, the
/// reading view's decorator passed the null on, and Narrator abandons its selection announcement
/// on it (measured: with an empty array in its place Narrator says "body selected"). UI
/// Automation's classic client API turns the null into an empty array for every client, so no
/// out-of-process journey can see it: these facts read the provider itself.
/// <para>
/// With an array, Narrator goes on to ask for the whole selection's bounding rectangles, which WPF
/// answers by walking every line of the range before it clips each to the view (1.8 to 2.5 s on
/// a 2,000-item note, measured). The decorator asks WPF only for the part of the range on screen,
/// and the rectangles stay WPF's own.
/// </para>
/// <para>
/// Every WPF text field in the app (text boxes, the palette's field, the search fields) still
/// serves WPF's own adaptor and its null; that is a separate issue, outside the reading view.
/// </para>
/// </summary>
public sealed class ReadingTextRangeArrayTests
{
    private const string Trailing = "body\n\n#\n";

    [Fact]
    public void ASelectAllWithNoEmbeddedElementAnswersAnEmptyChildrenArray() => RunSta(() =>
    {
        using var host = new ShownShell(("trailing.md", Trailing));
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "trailing.md", "body");

        ApplicationCommands.SelectAll.Execute(null, surface);
        host.Settle();
        ITextRangeProvider selection = Assert.Single(provider.GetSelection());
        Assert.True(
            selection.GetText(-1) == provider.DocumentRange.GetText(-1) && selection.GetText(-1).StartsWith("body", StringComparison.Ordinal),
            $"premise: Ctrl+A did not select the note; the selection reads '{selection.GetText(-1)}'");

        IRawElementProviderSimple[]? children = selection.GetChildren();
        Assert.NotNull(children);
        Assert.Empty(children);
    });

    [Fact]
    public void ACaretAndTheDocumentRangeAnswerAnEmptyChildrenArray() => RunSta(() =>
    {
        using var host = new ShownShell(("trailing.md", Trailing));
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "trailing.md", "body");

        EditingCommands.MoveToDocumentStart.Execute(null, surface);
        host.Settle();
        ITextRangeProvider caret = Assert.Single(provider.GetSelection());
        Assert.True(caret.CompareEndpoints(TextPatternRangeEndpoint.Start, caret, TextPatternRangeEndpoint.End) == 0, "premise: the selection is not a caret");

        Assert.Empty(Assert.IsAssignableFrom<IRawElementProviderSimple[]>(caret.GetChildren()));
        Assert.Empty(Assert.IsAssignableFrom<IRawElementProviderSimple[]>(provider.DocumentRange.GetChildren()));
    });

    /// <summary>A range that holds a link keeps WPF's own children: the fix answers an empty array
    /// only where WPF answers null.</summary>
    [Fact]
    public void ARangeHoldingALinkKeepsWpfsChildren() => RunSta(() =>
    {
        using var host = new ShownShell(("linked.md", "See [[other]] here.\n\nAfter.\n"), ("other.md", "# Other\n"));
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "linked.md", "After.");
        Hyperlink link = Hyperlinks(surface.Document).First();

        surface.Selection.Select(link.ElementStart, link.ElementEnd);
        host.Settle();
        var wrapped = Assert.IsType<HeadingStyleTextRange>(Assert.Single(provider.GetSelection()));
        IRawElementProviderSimple[] wpfs = wrapped.Inner.GetChildren();
        Assert.True(wpfs is { Length: > 0 }, "premise: WPF answers no child for a selection over the link");

        IRawElementProviderSimple[] served = wrapped.GetChildren();
        Assert.Equal(wpfs.Length, served.Length);
        for (int index = 0; index < wpfs.Length; index++)
        {
            // In process, with no client connected, WPF answers its child slots without names
            // (measured), so a slot is matched by what it holds and, where it has one, its name.
            Assert.True(
                ReferenceEquals(wpfs[index], served[index])
                    || (wpfs[index] is not null && served[index] is not null && Name(wpfs[index]) == Name(served[index])),
                $"child {index}: WPF answers '{Name(wpfs[index])}', the decorator '{Name(served[index])}'");
        }
    });

    /// <summary>Every array-returning member of the Text pattern's two interfaces, called through
    /// the decorator on a caret, a selection without children, a range wholly off screen and the
    /// document range, answers an array. A member added to either interface is called by the same
    /// loop.</summary>
    [Fact]
    public void NoInterfaceMemberOfTheReadingTextPatternAnswersANullArray() => RunSta(() =>
    {
        using var host = new ShownShell(("long.md", LongNote(160)));
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "long.md", "Paragraph 160.");
        var wrong = new List<string>();

        foreach (MethodInfo member in ArrayMembers(typeof(ITextProvider)))
        {
            if (member.Invoke(provider, null) is null)
            {
                wrong.Add($"ITextProvider.{member.Name}");
            }
        }

        EditingCommands.MoveToDocumentStart.Execute(null, surface);
        host.Settle();
        Assert.True(
            surface.VerticalOffset == 0 && surface.ExtentHeight > surface.ViewportHeight * 2,
            $"premise: the note does not scroll, or the view is not at its top (offset {surface.VerticalOffset}, extent {surface.ExtentHeight}, viewport {surface.ViewportHeight})");
        CallEach(provider.GetSelection()[0], "the caret", wrong);
        CallEach(provider.DocumentRange, "the document range", wrong);
        CallEach(LastParagraph(provider, "Paragraph 160."), "a range wholly below the view", wrong);

        surface.Selection.Select(surface.Document.ContentStart, surface.Document.ContentEnd);
        host.Settle();
        CallEach(provider.GetSelection()[0], "the select-all selection", wrong);

        Assert.True(wrong.Count == 0, "These answered null: " + string.Join("; ", wrong));
    });

    private static void CallEach(ITextRangeProvider range, string name, List<string> wrong)
    {
        foreach (MethodInfo member in ArrayMembers(typeof(ITextRangeProvider)))
        {
            if (member.Invoke(range, null) is null)
            {
                wrong.Add($"ITextRangeProvider.{member.Name} on {name}");
            }
        }
    }

    /// <summary>UI Automation's contract, which WPF keeps: a range's rectangles are those of its
    /// lines that are visible, fully or in part. The decorator asks WPF only for the visible part
    /// of a range, and its answer stays WPF's: for a select-all on a note scrolled to its middle,
    /// exactly WPF's own rectangles, the partly visible lines at both edges included; for a
    /// paragraph wholly above or below the view, none, as WPF answers.</summary>
    [Fact]
    public void TheSelectAllRectanglesAreWpfsOwnThatMeetTheView() => RunSta(() =>
    {
        using var host = new ShownShell(("long.md", LongNote(300)));
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "long.md", "Paragraph 300.");
        ShowTheMiddle(host, surface, provider);
        Rect view = ViewOnScreen(surface);
        foreach ((string where, ITextRangeProvider range) in new[] { ("above", FirstParagraph(provider)), ("below", LastParagraph(provider, "Paragraph 300.")) })
        {
            double[] rectangles = range.GetBoundingRectangles();
            double[] wpfsOwn = ((HeadingStyleTextRange)range).Inner.GetBoundingRectangles();
            Assert.True(
                rectangles.Length == 0 && wpfsOwn.Length == 0,
                $"the paragraph {where} the view ('{Shown(range.GetText(40))}') answered {string.Join(", ", rectangles)}, WPF's own {string.Join(", ", wpfsOwn)}; the view is {view}");
        }

        // Selecting everything scrolls the caret to the note's end; the middle is shown again.
        surface.Selection.Select(surface.Document.ContentStart, surface.Document.ContentEnd);
        host.Settle();
        ShowTheMiddle(host, surface, provider);
        var all = Assert.IsType<HeadingStyleTextRange>(provider.GetSelection()[0]);
        Assert.Contains("Paragraph 300.", all.GetText(-1), StringComparison.Ordinal);
        double[] wpfs = all.Inner.GetBoundingRectangles();
        Assert.True(
            wpfs.Length >= 8 && Meeting(wpfs, view).Length == wpfs.Length,
            $"premise: WPF answers {wpfs.Length / 4} rectangles, {Meeting(wpfs, view).Length / 4} of them meeting the view {view}");
        AssertSameRectangles(wpfs, all.GetBoundingRectangles(), "the select-all range");
    });

    /// <summary>Paragraph 150 brought into view, then 7 px more, so the view's top edge cuts a
    /// line.</summary>
    private static void ShowTheMiddle(ShownShell host, ReadingSurface surface, ITextProvider provider)
    {
        Paragraph middle = Paragraphs(surface.Document).Single(paragraph => TextOf(paragraph) == "Paragraph 150.");
        middle.BringIntoView();
        host.Settle();
        surface.ScrollToVerticalOffset(surface.VerticalOffset + 7);
        host.Settle();
        string seen = string.Join(" ", provider.GetVisibleRanges().Select(span => span.GetText(-1)));
        Assert.True(
            seen.Contains("Paragraph 150.", StringComparison.Ordinal) && !seen.Contains("Long", StringComparison.Ordinal) && !seen.Contains("Paragraph 300.", StringComparison.Ordinal),
            $"premise: the view does not show the note's middle; it shows '{Shown(seen)}'");
    }
    private static string Shown(string text) => text.Replace("\r", "\\r").Replace("\n", "\\n");

    private static IEnumerable<Paragraph> Paragraphs(FlowDocument document) =>
        document.Blocks.SelectMany(block => block switch
        {
            Paragraph paragraph => [paragraph],
            Section section => Paragraphs(section),
            _ => Enumerable.Empty<Paragraph>(),
        });

    private static IEnumerable<Paragraph> Paragraphs(Section section) =>
        section.Blocks.SelectMany(block => block switch
        {
            Paragraph paragraph => [paragraph],
            Section inner => Paragraphs(inner),
            _ => Enumerable.Empty<Paragraph>(),
        });

    private static string TextOf(Paragraph paragraph) =>
        new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Trim();

    public static TheoryData<string> Fixtures => new() { "ltr", "rtl", "mixed", "wrap", "table" };

    private static string Fixture(string kind) => kind switch
    {
        "ltr" => "# Long\n\n" + Repeat(120, index => $"Paragraph {index}.\n\n") + "End mark.\n",
        "rtl" => "# Long\n\n" + Repeat(120, index => $"פסקה מספר {index} כאן בעברית.\n\n") + "End mark.\n",
        "mixed" => "# Long\n\n" + Repeat(120, index => $"Paragraph {index} ends in שלום עולם\n\n") + "End mark.\n",
        "wrap" => "# Long\n\n" + Repeat(40, index => $"Paragraph {index}: " + string.Concat(Enumerable.Repeat("lorem ipsum dolor sit amet ", 12 + (index % 9))) + "\n\n") + "End mark.\n",
        "table" => "# Table\n\nIntro.\n\n| A | B | C |\n|---|---|---|\n" + Repeat(60, index => (index % 3) switch
        {
            0 => $"| r{index} " + string.Concat(Enumerable.Repeat("long first column words ", 6)) + $"| s{index} | t{index} |\n",
            1 => $"| r{index} | s{index} | " + string.Concat(Enumerable.Repeat("long last column words ", 6)) + "|\n",
            _ => $"| r{index} | " + string.Concat(Enumerable.Repeat("middle words ", 10)) + $"| t{index} |\n",
        }) + "\nEnd mark.\n",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string Repeat(int count, Func<int, string> line) =>
        string.Concat(Enumerable.Range(1, count).Select(line));

    /// <summary>The decorator's rectangles are WPF's own at every scroll offset over two
    /// paragraph pitches, in 4-DIP steps, so each edge of the view cuts a line at some offset:
    /// for the select-all and for every line from three above the view to three below it, over
    /// left-to-right, right-to-left, mixed, wrapped and table text. WPF's visible ranges are two
    /// hit tests at the view's corners, and right-to-left text puts those at the far end of a
    /// line, so the decorator gives them a line's slack at each edge (the diff red team's
    /// finding: without it a Hebrew top line came back as a 9 px sliver).</summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void TheRectanglesAreWpfsOwnAtEveryScrollOffset(string kind) => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", Fixture(kind)));
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "note.md", "End mark.");
        surface.Selection.Select(surface.Document.ContentStart, surface.Document.ContentEnd);
        host.Settle();
        var all = Assert.IsType<HeadingStyleTextRange>(provider.GetSelection()[0]);
        var wrong = new List<string>();
        int compared = 0;
        for (double offset = 0; offset <= 160; offset += 4)
        {
            surface.ScrollToVerticalOffset(offset);
            host.Settle();
            if (!SameAsWpf(all, out string? why))
            {
                wrong.Add($"offset {offset}, the select-all: {why}");
            }

            ITextRangeProvider[] visible = provider.GetVisibleRanges();
            ITextRangeProvider cursor = visible[0].Clone();
            cursor.MoveEndpointByRange(TextPatternRangeEndpoint.End, cursor, TextPatternRangeEndpoint.Start);
            cursor.Move(TextUnit.Line, -3);
            int past = 0;
            for (int step = 0; step < 120; step++)
            {
                var line = Assert.IsType<HeadingStyleTextRange>(cursor.Clone());
                line.ExpandToEnclosingUnit(TextUnit.Line);
                compared++;
                if (!SameAsWpf(line, out string? lineWhy))
                {
                    wrong.Add($"offset {offset}, the line '{Shown(line.GetText(30))}': {lineWhy}");
                }

                if ((line.CompareEndpoints(TextPatternRangeEndpoint.Start, visible[^1], TextPatternRangeEndpoint.End) > 0 && ++past > 3)
                    || cursor.Move(TextUnit.Line, 1) == 0)
                {
                    break;
                }
            }
        }

        Assert.True(compared > 100, $"premise: only {compared} lines were compared");
        Assert.True(wrong.Count == 0, $"{wrong.Count} answers differ from WPF's ({kind}): " + string.Join("; ", wrong.Take(8)));
    });

    /// <summary>A reading view about one line tall over right-to-left text: WPF's visible
    /// ranges throw there (their top-left hit lies after their bottom-right one), and the
    /// decorator answers WPF's own rectangles rather than the throw.</summary>
    [Fact]
    public void AViewOneLineTallOverRightToLeftTextAnswersWpfsRectangles() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Long\n\n" + Repeat(60, index => $"פסקה מספר {index} כאן בעברית\n\n") + "End mark.\n"));
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "note.md", "End mark.");
        surface.Selection.Select(surface.Document.ContentStart, surface.Document.ContentEnd);
        host.Settle();
        var all = Assert.IsType<HeadingStyleTextRange>(provider.GetSelection()[0]);
        surface.Height = 36;
        host.Settle();
        var wrong = new List<string>();
        int threw = 0;
        for (double offset = 60; offset <= 400; offset += 2)
        {
            surface.ScrollToVerticalOffset(offset);
            host.Settle();
            try
            {
                _ = provider.GetVisibleRanges();
            }
            catch (ArgumentException)
            {
                threw++;
            }

            try
            {
                if (!SameAsWpf(all, out string? why))
                {
                    wrong.Add($"offset {offset}: {why}");
                }
            }
            catch (ArgumentException exception)
            {
                wrong.Add($"offset {offset}: threw {exception.Message}");
            }
        }

        Assert.True(threw > 0, "premise: WPF's visible ranges never threw over this view");
        Assert.True(wrong.Count == 0, $"{wrong.Count} offsets went wrong: " + string.Join("; ", wrong.Take(6)));
    });

    /// <summary>The same one-line view over a long right-to-left note: where WPF's visible
    /// ranges throw, the select-all's rectangles still come back without WPF walking every line
    /// of the note, and they are WPF's own (Codex, round 1: the first answer to the throw was
    /// WPF's whole-range walk).</summary>
    [Fact]
    public void AOneLineViewOverALongRightToLeftNoteAnswersQuickly() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Long\n\n" + Repeat(1200, index => $"פסקה מספר {index} כאן בעברית\n\n") + "End mark.\n"));
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "note.md", "End mark.");
        surface.Selection.Select(surface.Document.ContentStart, surface.Document.ContentEnd);
        host.Settle();
        var all = Assert.IsType<HeadingStyleTextRange>(provider.GetSelection()[0]);
        surface.Height = 36;
        host.Settle();
        var timed = new List<string>();
        for (double offset = 60; offset <= 400 && timed.Count < 4; offset += 2)
        {
            surface.ScrollToVerticalOffset(offset);
            host.Settle();
            try
            {
                _ = provider.GetVisibleRanges();
                continue;
            }
            catch (ArgumentException)
            {
                // WPF's corners come out of order here: the case under test.
            }

            // Timed against WPF's own walk over the same range at the same offset, so the
            // comparison holds on a faster or slower machine.
            var clock = Stopwatch.StartNew();
            double[] served = all.GetBoundingRectangles();
            long servedMs = clock.ElapsedMilliseconds;
            clock.Restart();
            double[] wpfs = all.Inner.GetBoundingRectangles();
            long wpfMs = clock.ElapsedMilliseconds;
            timed.Add($"offset {offset}: {served.Length / 4} rectangles in {servedMs} ms, WPF's own {wpfs.Length / 4} in {wpfMs} ms");
            Assert.True(wpfMs >= 200, "premise: WPF's own walk is too quick to compare with: " + string.Join("; ", timed));
            Assert.True(servedMs * 4 < wpfMs, "the select-all's rectangles cost what WPF's walk does: " + string.Join("; ", timed));
            Assert.True(
                SameAsWpf(all, out string? why),
                $"offset {offset}: the decorator's rectangles are not WPF's own: {why}");
        }

        Assert.True(timed.Count > 0, "premise: WPF's visible ranges never threw over this view");
    });

    private static bool SameAsWpf(HeadingStyleTextRange range, out string? why)
    {
        double[] wpfs = range.Inner.GetBoundingRectangles();
        double[] served = range.GetBoundingRectangles();
        why = null;
        if (wpfs.Length != served.Length)
        {
            why = $"WPF answers {wpfs.Length / 4} rectangles, the decorator {served.Length / 4}";
            return false;
        }

        for (int index = 0; index < wpfs.Length; index++)
        {
            if (Math.Abs(wpfs[index] - served[index]) > 0.5)
            {
                why = $"value {index} is {served[index]:F1}, WPF's is {wpfs[index]:F1}";
                return false;
            }
        }

        return true;
    }

    /// <summary>The reading surface's scroll viewport in screen pixels, the space WPF's rectangles
    /// are in.</summary>
    private static Rect ViewOnScreen(ReadingSurface surface)
    {
        var viewport = ShownShell.Descendants(surface).OfType<System.Windows.Controls.ScrollContentPresenter>().First();
        Point topLeft = viewport.PointToScreen(new Point(0, 0));
        Point bottomRight = viewport.PointToScreen(new Point(viewport.ActualWidth, viewport.ActualHeight));
        return new Rect(topLeft, bottomRight);
    }

    /// <summary>The (x, y, width, height) quads of <paramref name="rectangles"/> that meet
    /// <paramref name="view"/>.</summary>
    private static double[] Meeting(double[] rectangles, Rect view)
    {
        var kept = new List<double>();
        for (int index = 0; index + 3 < rectangles.Length; index += 4)
        {
            var line = new Rect(rectangles[index], rectangles[index + 1], rectangles[index + 2], rectangles[index + 3]);
            if (line.IntersectsWith(view) && line.Bottom > view.Top && line.Top < view.Bottom)
            {
                kept.AddRange(rectangles.AsSpan(index, 4).ToArray());
            }
        }

        return kept.ToArray();
    }

    /// <summary>WB-11's 2,000-item note: a range over the whole note answers its rectangles
    /// without WPF walking every line of it (1.7 to 2.6 s in process on <c>main</c>, 5 s with
    /// this fact's first measurement), however the range was made: the selection, a clone of it
    /// (clients clone constantly), the document range, and ranges grown from a found text, a
    /// found attribute and a point.</summary>
    [Fact]
    public void ARangeOverALongNoteAnswersItsRectanglesQuickly() => RunSta(() =>
    {
        using var host = new ShownShell(("big-list.md", ListNote(1996)));
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "big-list.md", "Closing paragraph.");
        surface.Selection.Select(surface.Document.ContentStart, surface.Document.ContentEnd);
        host.Settle();
        ITextRangeProvider all = provider.GetSelection()[0];
        Assert.Contains("item 1996", all.GetText(-1), StringComparison.Ordinal);
        ITextRangeProvider document = provider.DocumentRange;
        Rect view = ViewOnScreen(surface);

        var ranges = new List<(string Name, ITextRangeProvider Range)>
        {
            ("the select-all selection", all),
            ("a clone of the selection", all.Clone()),
            ("the document range", document),
            ("a found text grown to the note's end", ToTheEnd(document.FindText("Intro line.", false, false), document)),
            ("a found heading grown to the note's end", ToTheEnd(document.FindAttribute(HeadingStyleTextProvider.StyleIdAttribute, HeadingStyleTextProvider.StyleIdHeading1, false), document)),
            ("a found upright run grown to the note's end", ToTheEnd(document.FindAttribute(TextPattern.IsItalicAttribute.Id, false, false), document)),
            ("a point's range grown to the note's end", ToTheEnd(provider.RangeFromPoint(new Point(view.Left + 40, view.Top + 40)), document)),
        };
        var slow = new List<string>();
        foreach ((string name, ITextRangeProvider range) in ranges)
        {
            var clock = Stopwatch.StartNew();
            double[] rectangles = range.GetBoundingRectangles();
            clock.Stop();
            if (rectangles.Length == 0 || clock.ElapsedMilliseconds >= 500)
            {
                slow.Add($"{name}: {rectangles.Length / 4} rectangles in {clock.ElapsedMilliseconds} ms");
            }
        }

        Assert.True(slow.Count == 0, "On a 2,000-item note: " + string.Join("; ", slow));
    });

    private static ITextRangeProvider ToTheEnd(ITextRangeProvider? range, ITextRangeProvider document)
    {
        Assert.NotNull(range);
        range.MoveEndpointByRange(TextPatternRangeEndpoint.End, document, TextPatternRangeEndpoint.End);
        return range;
    }

    /// <summary>The text range types the app itself defines are the two that a fact holds to
    /// array answers: this one's, and the editor's, whose children arrays
    /// <c>EditorSemanticTextRangeTests</c> pins (its rectangles are AvalonEdit's and have no fact:
    /// that test's windowless host has no layout, and AvalonEdit throws there). A third fails
    /// here until it joins a fact. WPF's own adaptor, which every WPF text field still serves
    /// (#2067), is not one of the app's types.</summary>
    [Fact]
    public void TheAppsOwnTextRangeTypesAreCoveredByAnArrayFact()
    {
        string[] types = typeof(ReadingSurface).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ITextRangeProvider).IsAssignableFrom(type))
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { typeof(EditorSemanticTextRange).FullName!, typeof(HeadingStyleTextRange).FullName! }.OrderBy(name => name, StringComparer.Ordinal),
            types);
    }

    private static IEnumerable<MethodInfo> ArrayMembers(Type contract) =>
        contract.GetMethods().Where(member => member.ReturnType.IsArray && member.GetParameters().Length == 0);

    private static void AssertSameRectangles(double[] expected, double[] actual, string what)
    {
        Assert.True(expected.Length == actual.Length, $"{what}: WPF answers {expected.Length / 4} rectangles, the decorator {actual.Length / 4}");
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.True(
                Math.Abs(expected[index] - actual[index]) <= 0.5,
                $"{what}: value {index} is {actual[index]}, WPF's is {expected[index]}");
        }
    }

    private static ITextRangeProvider FirstParagraph(ITextProvider provider)
    {
        ITextRangeProvider range = provider.DocumentRange;
        range.MoveEndpointByRange(TextPatternRangeEndpoint.End, range, TextPatternRangeEndpoint.Start);
        range.ExpandToEnclosingUnit(TextUnit.Paragraph);
        return range;
    }

    private static ITextRangeProvider LastParagraph(ITextProvider provider, string text)
    {
        ITextRangeProvider range = provider.DocumentRange;
        range.MoveEndpointByRange(TextPatternRangeEndpoint.Start, range, TextPatternRangeEndpoint.End);
        range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Paragraph, -1);
        Assert.True(
            range.GetText(-1).StartsWith(text, StringComparison.Ordinal),
            $"premise: the last paragraph reads '{Shown(range.GetText(40))}'");
        return range;
    }

    private static string? Name(IRawElementProviderSimple? child) =>
        child?.GetPropertyValue(AutomationElementIdentifiers.NameProperty.Id) as string;

    private static string LongNote(int paragraphs)
    {
        var note = new System.Text.StringBuilder("# Long\n\n");
        for (int index = 1; index <= paragraphs; index++)
        {
            note.Append("Paragraph ").Append(index).Append(".\n\n");
        }

        return note.ToString();
    }

    /// <summary>WB-11's <c>big-list.md</c> shape.</summary>
    private static string ListNote(int items)
    {
        var note = new System.Text.StringBuilder("# Big list\n\nIntro line.\n\n");
        for (int index = 1; index <= items; index++)
        {
            note.Append("- item ").Append(index).Append('\n');
        }

        note.Append("\n## After the list\n\nClosing paragraph.\n");
        return note.ToString();
    }

    private static IEnumerable<Hyperlink> Hyperlinks(FlowDocument document)
    {
        for (TextPointer? at = document.ContentStart; at is not null; at = at.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (at.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart
                && at.GetAdjacentElement(LogicalDirection.Forward) is Hyperlink link)
            {
                yield return link;
            }
        }
    }

    /// <summary>The note opened in the shown shell and switched to reading mode, with the keys on
    /// the reading surface (as <c>ReadingLineUnitTests</c> opens it).</summary>
    private static (ReadingSurface Surface, ITextProvider Provider) OpenReading(ShownShell host, string path, string lastText)
    {
        host.Workspace.OpenPath(path);
        host.Settle();
        host.Workspace.ToggleReadingModeCommand.Execute(null);
        ReadingSurface? surface = null;
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () =>
                {
                    host.Settle();
                    surface = ShownShell.Descendants(host.Shell).OfType<ReadingSurface>().FirstOrDefault(s => s.IsVisible);
                    return surface is not null
                        && new TextRange(surface.Document.ContentStart, surface.Document.ContentEnd).Text.Contains(lastText, StringComparison.Ordinal);
                },
                TimeSpan.FromSeconds(60)),
            "the reading view never showed the note");
        Assert.True(surface!.Focus(), "premise: the reading view refused the keys");
        host.Settle();
        var provider = Assert.IsAssignableFrom<ITextProvider>(
            UIElementAutomationPeer.CreatePeerForElement(surface).GetPattern(PatternInterface.Text));
        return (surface, provider);
    }

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(120), "STA test body timed out.");
}
