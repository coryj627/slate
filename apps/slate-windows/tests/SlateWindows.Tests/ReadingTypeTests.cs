// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>DS-3: the reading view is Open Sans 16/26, with headings at 32, 24, 20 and
/// 18, and a measure of 72 characters, as design/windows/tokens/typography.css says.</summary>
public sealed class ReadingTypeTests
{
    private const string Words =
        "The quick brown fox jumps over the lazy dog, and then it does it all over again so that this "
        + "paragraph runs on for long enough to fill several lines at any width a reader might choose.";

    [Theory]
    [InlineData("Body", "body")]
    [InlineData("H1", "h1")]
    [InlineData("H2", "h2")]
    [InlineData("H3", "h3")]
    [InlineData("H4", "h4")]
    [InlineData("Small", "small")]
    public void EveryReaderStepIsItsToken(string name, string token)
    {
        string css = File.ReadAllText(Path.Combine(SourceText.RepoRoot(), "design", "windows", "tokens", "typography.css"));
        ReaderStep step = TypeRamp.ReaderSteps.Single(candidate => candidate.Name == name);
        Assert.Equal(double.Parse(Regex.Match(css, $@"--reader-{token}-size:(\d+)px").Groups[1].Value, CultureInfo.InvariantCulture), step.Size);
        Assert.Equal(double.Parse(Regex.Match(css, $@"--reader-{token}-lh:(\d+)px").Groups[1].Value, CultureInfo.InvariantCulture), step.LineHeight);
    }

    /// <summary>A measured line, because a LineHeight nobody lays out proves nothing.</summary>
    [Fact]
    public void TheBodyIsSixteenOnTwentySix()
    {
        (double size, double advance, LineStackingStrategy stacking) = OnSurface(600, surface =>
        {
            Paragraph paragraph = Show(surface, Words);
            return (paragraph.FontSize, LineAdvance(paragraph), paragraph.LineStackingStrategy);
        });

        Assert.Equal(16, size);
        Assert.Equal(26, advance, 1);
        Assert.Equal(LineStackingStrategy.MaxHeight, stacking);
    }

    [Theory]
    [InlineData(1, 32, 40, 700)]
    [InlineData(2, 24, 32, 600)]
    [InlineData(3, 20, 28, 600)]
    [InlineData(4, 18, 26, 600)]
    [InlineData(5, 18, 26, 600)]
    [InlineData(6, 18, 26, 600)]
    public void HeadingsFollowTheScale(int level, double size, double lineHeight, int weight)
    {
        (double shownSize, double shownLine, double advance, int shownWeight) = OnSurface(300, surface =>
        {
            string marks = new('#', level);
            surface.ApplyBuiltDocument(BuildSource($"{marks} {Words}\n"));
            surface.UpdateLayout();
            Pump();
            Paragraph heading = surface.Document.Blocks.OfType<Paragraph>().First();
            return (heading.FontSize, heading.LineHeight, LineAdvance(heading), heading.FontWeight.ToOpenTypeWeight());
        });

        Assert.Equal(size, shownSize);
        Assert.Equal(lineHeight, shownLine);
        Assert.Equal(lineHeight, advance, 1);
        Assert.Equal(weight, shownWeight);
    }

    /// <summary>A block line height fixes every line's advance, so an object in the line
    /// would overlap what follows. A heading holding one keeps the line height as a
    /// floor, as body text does.</summary>
    [Fact]
    public void AHeadingHoldingAnObjectKeepsItsLineGrowing()
    {
        (LineStackingStrategy plain, LineStackingStrategy holding) = OnSurface(600, _ =>
        {
            var words = new Paragraph(new Run("Words"));
            ReadingDocumentBuilder.ApplyHeadingType(words, 2);
            var withObject = new Paragraph(new Run("Words"));
            withObject.Inlines.Add(new InlineUIContainer(new Border { Width = 10, Height = 10 }));
            ReadingDocumentBuilder.ApplyHeadingType(withObject, 2);
            return (words.LineStackingStrategy, withObject.LineStackingStrategy);
        });

        Assert.Equal(LineStackingStrategy.BlockLineHeight, plain);
        Assert.Equal(LineStackingStrategy.MaxHeight, holding);
    }

    /// <summary>The body's line height is a floor, so a tall inline grows its line
    /// instead of drawing over the next paragraph.</summary>
    [Fact]
    public void AMathBlockDoesNotOverlapWhatFollows()
    {
        (double objectBottom, double nextTop) = OnSurface(600, surface =>
        {
            var tall = new Border { Width = 40, Height = 120 };
            surface.Document.Blocks.Add(new Paragraph(new InlineUIContainer(tall)));
            var next = new Paragraph(new Run("What follows."));
            surface.Document.Blocks.Add(next);
            surface.UpdateLayout();
            Pump();
            return (
                tall.TranslatePoint(new Point(0, tall.ActualHeight), surface).Y,
                next.ContentStart.GetCharacterRect(LogicalDirection.Forward).Top);
        });

        Assert.True(nextTop >= objectBottom, $"the next paragraph starts at {nextTop:F0} and the object ends at {objectBottom:F0}.");
    }

    /// <summary>LineHeight inherits, which is why the body's isn't on the document: it
    /// would follow into the text blocks of cards and tables.</summary>
    [Fact]
    public void ACardsTextKeepsItsOwnLineHeight()
    {
        double inherited = OnSurface(600, surface =>
        {
            var card = new TextBlock { Text = "In a card." };
            surface.Document.Blocks.Add(new BlockUIContainer(card));
            surface.UpdateLayout();
            Pump();
            return card.LineHeight;
        });

        Assert.True(double.IsNaN(inherited), $"a card's text inherited a line height of {inherited}.");
    }

    [Fact]
    public void AWideSurfaceHoldsTheMeasure()
    {
        (double padding, double measure, double surfaceWidth, double longest, double digit) = OnSurface(1400, surface =>
        {
            Paragraph paragraph = Show(surface, string.Concat(Enumerable.Repeat(Words + " ", 6)));
            double zero = new FormattedText(
                "0",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(surface.Document.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                surface.Document.FontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(surface).PixelsPerDip).WidthIncludingTrailingWhitespace;
            return (surface.Document.PagePadding.Left, 72 * zero, surface.ActualWidth, FirstLineWidth(paragraph), zero);
        });

        Assert.True(padding > 16, $"the padding is {padding:F0}.");
        Assert.Equal(surfaceWidth - (2 * padding), measure, 0);
        Assert.InRange(longest, measure - (12 * digit), measure + digit);
    }

    [Fact]
    public void ANarrowSurfaceKeepsItsPadding()
    {
        (Thickness padding, double lineWidth) = OnSurface(400, surface =>
        {
            Paragraph paragraph = Show(surface, Words);
            return (surface.Document.PagePadding, FirstLineWidth(paragraph));
        });

        Assert.Equal(new Thickness(16), padding);
        Assert.True(lineWidth < 400, $"the line is {lineWidth:F0} wide.");
    }

    /// <summary>The rule at ReadingSurface's document: replacing or rebuilding it breaks
    /// the UIA binding, so a screen reader sees it blank and caret moves go silent.</summary>
    [Fact]
    public void AResizeKeepsTheCaret()
    {
        (bool sameDocument, int before, int after, double paddingBefore, double paddingAfter) = OnSurface(600, surface =>
        {
            Show(surface, Words);
            FlowDocument document = surface.Document;
            surface.CaretPosition = document.ContentStart.GetPositionAtOffset(25)!;
            int offsetBefore = document.ContentStart.GetOffsetToPosition(surface.CaretPosition);
            double left = document.PagePadding.Left;

            ((Window)surface.Parent).Width = 1400;
            surface.UpdateLayout();
            Pump();
            return (
                ReferenceEquals(document, surface.Document),
                offsetBefore,
                document.ContentStart.GetOffsetToPosition(surface.CaretPosition),
                left,
                document.PagePadding.Left);
        });

        Assert.True(sameDocument, "the resize replaced the document.");
        Assert.Equal(before, after);
        Assert.True(paddingAfter > paddingBefore, $"the padding stayed at {paddingAfter:F0}.");
    }

    private static Paragraph Show(ReadingSurface surface, string text)
    {
        var paragraph = new Paragraph(new Run(text));
        surface.Document.Blocks.Add(paragraph);
        surface.UpdateLayout();
        Pump();
        return paragraph;
    }

    /// <summary>The distance between the tops of a paragraph's first two lines.</summary>
    private static double LineAdvance(Paragraph paragraph)
    {
        TextPointer first = paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        TextPointer second = first.GetLineStartPosition(1)
            ?? throw new InvalidOperationException("the paragraph didn't wrap.");
        return second.GetCharacterRect(LogicalDirection.Forward).Top - first.GetCharacterRect(LogicalDirection.Forward).Top;
    }

    private static double FirstLineWidth(Paragraph paragraph)
    {
        TextPointer first = paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        TextPointer next = first.GetLineStartPosition(1) ?? paragraph.ContentEnd;
        TextPointer lastOnLine = next.GetNextInsertionPosition(LogicalDirection.Backward) ?? next;
        return lastOnLine.GetCharacterRect(LogicalDirection.Backward).Right - first.GetCharacterRect(LogicalDirection.Forward).Left;
    }

    private static FlowDocument BuildSource(string source)
    {
        ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(source);
        ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
            source, Array.Empty<RenderedCitation>(), Array.Empty<OutgoingLink>());
        return ReadingDocumentBuilder.Build([.. blocks.Zip(inlines, (block, line) => (block, line))]).Document;
    }

    private static T OnSurface<T>(double width, Func<ReadingSurface, T> body) =>
        StaThread.Run(() =>
        {
            RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
            var surface = new ReadingSurface();
            var window = new Window
            {
                Width = width,
                Height = 700,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
                Left = -10_000,
                Top = -10_000,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Content = surface,
            };
            foreach (ResourceDictionary layer in ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false))
            {
                window.Resources.MergedDictionaries.Add(layer);
            }

            try
            {
                window.Show();
                window.UpdateLayout();
                Pump();
                return body(surface);
            }
            finally
            {
                window.Close();
            }
        });

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
